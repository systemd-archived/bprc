using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace BadPiggies.Multiplayer.Online.EasyTier
{
	/// <summary>Lifecycle of the managed <c>easytier-core.exe</c> child process.</summary>
	public enum EasyTierState
	{
		/// <summary>Not Windows x64 — the layer can never run here.</summary>
		Unsupported,
		/// <summary>Platform is fine but the staged binaries are missing.</summary>
		NotInstalled,
		/// <summary>Nothing running.</summary>
		Stopped,
		/// <summary>Process launched, waiting for the RPC portal to answer.</summary>
		Starting,
		/// <summary>RPC portal answers; the virtual LAN is up.</summary>
		Running,
		/// <summary>Shutting down.</summary>
		Stopping,
		/// <summary>Start-up or runtime failure. See <see cref="EasyTierRunner.LastError"/>.</summary>
		Failed
	}

	/// <summary>
	/// Owns exactly one <c>easytier-core.exe</c> child process: starts it, waits for it
	/// to become reachable over its RPC portal, streams its output into a bounded ring
	/// buffer, and kills the whole process tree on stop/quit.
	/// <para>
	/// Threading contract: every public member may be called from the Unity main thread
	/// only. The blocking work (process supervision, readiness polling) runs on one
	/// background thread; its results reach the main thread through
	/// <see cref="ConcurrentQueue{T}"/> instances drained in <c>Update()</c>.
	/// </para>
	/// </summary>
	public sealed class EasyTierRunner : MonoBehaviour
	{
		/// <summary>How many log lines <see cref="GetRecentLog"/> can return.</summary>
		public const int LogCapacity = 400;

		private const int ReadinessTimeoutMs = 20000;
		private const int EarlyExitProbeMs = 2500;
		private const int PollIntervalMs = 250;
		private const int ProbeTimeoutMs = 400;
		private const int RequiredConsecutiveProbes = 2;

		private static readonly string[] ElevationHints =
		{
			"wintun",
			"access is denied",
			"拒绝访问",
			"requires admin",
			"administrator",
			"elevat",
			"os error 5",
			"failed to create tun",
			"create tun device",
			"permission denied",
			"权限"
		};

		private static EasyTierRunner _instance;

		private readonly object _sync = new object();
		private readonly object _logSync = new object();
		private readonly Queue<string> _log = new Queue<string>(LogCapacity + 1);
		private readonly ConcurrentQueue<string> _pendingLog = new ConcurrentQueue<string>();
		private readonly ConcurrentQueue<EasyTierState> _pendingStates = new ConcurrentQueue<EasyTierState>();

		private Process _process;
		private Thread _worker;
		private volatile bool _cancelWorker;
		private volatile int _generation;
		private volatile bool _processExitedUnexpectedly;
		private string _unexpectedExitReason = "";

		private EasyTierState _state = EasyTierState.Stopped;
		private EasyTierState _raisedState = EasyTierState.Stopped;
		private string _lastError = "";
		private EasyTierOptions _options;
		private bool _isElevated;

		/// <summary>Raised on the main thread whenever <see cref="State"/> changes.</summary>
		public event Action<EasyTierState> StateChanged;

		/// <summary>
		/// Raised on the main thread for every stdout/stderr line. Lines that came from
		/// stderr are prefixed with "[stderr] ".
		/// </summary>
		public event Action<string> LogLineReceived;

		// ------------------------------------------------------------------ lifecycle

		/// <summary>
		/// Returns the singleton, creating a <c>DontDestroyOnLoad</c> "[EasyTier]"
		/// GameObject on first use.
		/// </summary>
		public static EasyTierRunner GetOrCreate()
		{
			if (_instance != null)
			{
				return _instance;
			}
			GameObject host = new GameObject("[EasyTier]");
			UnityEngine.Object.DontDestroyOnLoad(host);
			_instance = host.AddComponent<EasyTierRunner>();
			return _instance;
		}

		private void Awake()
		{
			if (_instance != null && _instance != this)
			{
				// A stale survivor from a previous domain (or a duplicate host).
				Debug.LogWarning("[EasyTier] 重复的 EasyTierRunner，已销毁多余实例。");
				UnityEngine.Object.Destroy(this);
				return;
			}
			_instance = this;

			if (!EasyTierPaths.IsSupportedPlatform)
			{
				SetState(EasyTierState.Unsupported);
			}
			else if (!EasyTierPaths.IsInstalled)
			{
				SetState(EasyTierState.NotInstalled);
			}
			else
			{
				SetState(EasyTierState.Stopped);
			}
			_raisedState = _state;
		}

		private void OnDestroy()
		{
			StopNetwork();
			if (_instance == this)
			{
				_instance = null;
			}
		}

		private void OnApplicationQuit()
		{
			StopNetwork();
		}

		private void Update()
		{
			DrainPending();
			GuardProcessLiveness();
		}

		// -------------------------------------------------------------------- state

		public EasyTierState State
		{
			get
			{
				lock (_sync)
				{
					return _state;
				}
			}
		}

		public string LastError
		{
			get
			{
				lock (_sync)
				{
					return _lastError;
				}
			}
		}

		/// <summary>The options of the current (or most recent) network. May be null.</summary>
		public EasyTierOptions Options
		{
			get
			{
				lock (_sync)
				{
					return _options;
				}
			}
		}

		/// <summary>True when the running instance was relaunched through UAC (<c>runas</c>).</summary>
		public bool IsElevated
		{
			get
			{
				lock (_sync)
				{
					return _isElevated;
				}
			}
		}

		/// <summary>OS process id of the running instance, or 0 when nothing is running.</summary>
		public int ProcessId
		{
			get
			{
				Process proc;
				lock (_sync)
				{
					proc = _process;
				}
				if (proc == null)
				{
					return 0;
				}
				try
				{
					return proc.HasExited ? 0 : proc.Id;
				}
				catch (Exception)
				{
					return 0;
				}
			}
		}

		// ----------------------------------------------------------------- start/stop

		/// <summary>Starts a network, allowing a UAC elevation retry when TUN needs it.</summary>
		public bool StartNetwork(EasyTierOptions options, out string error)
		{
			return StartNetwork(options, true, out error);
		}

		/// <summary>
		/// Starts a network.
		/// <para>
		/// <paramref name="allowElevation"/> = true permits one automatic relaunch through
		/// <c>ProcessStartInfo { UseShellExecute = true, Verb = "runas" }</c> after the
		/// unelevated attempt fails in the "needs administrator" way. It is never used
		/// when <see cref="EasyTierOptions.NoTun"/> is set, and never for a second time.
		/// </para>
		/// </summary>
		public bool StartNetwork(EasyTierOptions options, bool allowElevation, out string error)
		{
			error = "";

			if (options == null)
			{
				error = "EasyTier 配置为空。";
				return false;
			}
			string validation = options.Validate();
			if (validation != null)
			{
				error = validation;
				return false;
			}

			if (!EasyTierPaths.IsSupportedPlatform)
			{
				SetState(EasyTierState.Unsupported);
				error = EasyTierPaths.MissingReason;
				return false;
			}
			if (!EasyTierPaths.IsInstalled)
			{
				SetState(EasyTierState.NotInstalled);
				error = EasyTierPaths.MissingReason;
				return false;
			}

			bool busy;
			lock (_sync)
			{
				busy = _state == EasyTierState.Starting || _state == EasyTierState.Running;
				if (busy)
				{
					error = _state == EasyTierState.Starting
						? "EasyTier 正在启动中，请稍候。"
						: "EasyTier 已经在运行；请先调用 StopNetwork() 再启动新的房间网络。";
				}
			}
			if (busy)
			{
				return false;
			}

			// Clear anything left over from a previous, failed attempt.
			TeardownWorkerAndProcess();

			EasyTierOptions snapshot = options.Clone();
			int generation;
			lock (_sync)
			{
				_options = snapshot;
				_lastError = "";
				_isElevated = false;
				_cancelWorker = false;
				_processExitedUnexpectedly = false;
				_unexpectedExitReason = "";
				generation = ++_generation;
			}

			List<string> args = EasyTierOptions.BuildArguments(snapshot);
			AppendLog("[runner] easytier-core " + EasyTierOptions.DescribeArguments(args));
			if (snapshot.EnableUdpBroadcastRelay)
			{
				AppendLog("[runner] 已启用 --enable-udp-broadcast-relay：抓包转发需要管理员权限，否则该功能会启动失败。");
			}
			if (snapshot.NoTun)
			{
				AppendLog("[runner] --no-tun：不会创建虚拟网卡，无需管理员权限。");
			}

			SetState(EasyTierState.Starting);

			Thread worker = new Thread(delegate()
			{
				try
				{
					RunWorker(generation, snapshot, allowElevation);
				}
				catch (Exception ex)
				{
					FailWith(generation, "EasyTier 后台线程异常：" + ex.Message);
				}
			});
			worker.IsBackground = true;
			worker.Name = "EasyTierRunner";
			lock (_sync)
			{
				_worker = worker;
			}
			worker.Start();
			return true;
		}

		/// <summary>
		/// Kills the child process tree and always leaves <see cref="State"/> ==
		/// <see cref="EasyTierState.Stopped"/>. Safe to call at any time, from the main
		/// thread only.
		/// </summary>
		public void StopNetwork()
		{
			Process proc;
			Thread worker;
			lock (_sync)
			{
				_cancelWorker = true;
				_generation++;
				proc = _process;
				worker = _worker;
			}

			bool wasActive = proc != null || (worker != null && worker.IsAlive);
			if (wasActive)
			{
				SetState(EasyTierState.Stopping);
			}

			if (proc != null)
			{
				EasyTierProcessHelper.KillProcessTree(proc);
			}
			if (worker != null && worker != Thread.CurrentThread && worker.IsAlive)
			{
				try
				{
					worker.Join(3000);
				}
				catch (Exception)
				{
				}
			}

			lock (_sync)
			{
				_process = null;
				_worker = null;
				_isElevated = false;
				_processExitedUnexpectedly = false;
				_unexpectedExitReason = "";
			}

			if (wasActive)
			{
				AppendLog("[runner] EasyTier 已停止。");
			}
			SetState(StateIsUsable() ? EasyTierState.Stopped : InitialState());
			if (StateIsUsable())
			{
				lock (_sync)
				{
					_lastError = "";
				}
			}
		}

		/// <summary>Snapshot of the most recent log lines, oldest first.</summary>
		public List<string> GetRecentLog(int maxLines)
		{
			if (maxLines <= 0)
			{
				maxLines = LogCapacity;
			}
			List<string> all = new List<string>();
			lock (_logSync)
			{
				foreach (string line in _log)
				{
					all.Add(line);
				}
			}
			if (all.Count <= maxLines)
			{
				return all;
			}
			return all.GetRange(all.Count - maxLines, maxLines);
		}

		// ------------------------------------------------------------------- worker

		private void RunWorker(int generation, EasyTierOptions options, bool allowElevation)
		{
			ushort rpcPort = options.EffectiveRpcPortalPort;
			List<string> args = EasyTierOptions.BuildArguments(options);

			Process proc;
			try
			{
				proc = StartCore(args, false);
			}
			catch (Exception ex)
			{
				FailWith(generation, "启动 easytier-core.exe 失败：" + ex.Message +
					"（路径：" + EasyTierPaths.CoreExecutable + "）");
				return;
			}

			lock (_sync)
			{
				if (generation != _generation)
				{
					EasyTierProcessHelper.KillProcessTree(proc);
					return;
				}
				_process = proc;
			}
			AppendLog("[runner] easytier-core.exe 已启动，PID=" + SafeId(proc) + "。");

			// Phase 1: give it a moment to fail fast (bad arguments, missing Npcap/
			// wintun, no admin rights...).
			int waited = 0;
			while (waited < EarlyExitProbeMs)
			{
				if (IsCancelled(generation))
				{
					EasyTierProcessHelper.KillProcessTree(proc);
					return;
				}
				if (HasExited(proc))
				{
					break;
				}
				Sleep(100);
				waited += 100;
			}

			if (HasExited(proc))
			{
				int exitCode = SafeExitCode(proc);
				string tail = LogTail(12);
				bool hint = LogSuggestsElevationRequired();
				AppendLog("[runner] easytier-core.exe 提前退出，退出码 " + exitCode +
					(hint ? "（日志显示需要管理员权限）" : "") + "。");

				bool canElevate = allowElevation && !options.NoTun && exitCode != 0;
				if (!canElevate)
				{
					FailWith(generation, BuildEarlyExitMessage(exitCode, hint, tail));
					return;
				}

				AppendLog("[runner] 尝试以管理员身份重新启动（UAC）。");
				Process elevated;
				try
				{
					elevated = StartCore(args, true);
				}
				catch (Exception ex)
				{
					FailWith(generation, "以管理员身份启动 easytier-core.exe 失败：" + ex.Message +
						"（用户取消 UAC 授权时会出现这个错误）。" + Environment.NewLine + "原始失败信息：" +
						BuildEarlyExitMessage(exitCode, hint, tail));
					return;
				}

				lock (_sync)
				{
					if (generation != _generation)
					{
						EasyTierProcessHelper.KillProcessTree(elevated);
						return;
					}
					_process = elevated;
					_isElevated = true;
				}
				proc = elevated;
				AppendLog("[runner] 已以管理员身份启动 easytier-core.exe，PID=" + SafeId(proc) +
					"（该进程的 stdout/stderr 无法重定向，日志只能依赖 RPC 探测）。");
			}

			// Phase 2: wait for the RPC portal.
			string failure;
			if (WaitForReady(generation, rpcPort, proc, out failure))
			{
				if (IsCancelled(generation))
				{
					EasyTierProcessHelper.KillProcessTree(proc);
					return;
				}
				AppendLog("[runner] EasyTier 就绪：RPC 门户 127.0.0.1:" + rpcPort +
					" 已响应" + (IsElevated ? "（管理员模式）" : "") + "。");
				SetState(EasyTierState.Running);
				WatchProcess(generation, proc);
				return;
			}

			if (IsCancelled(generation))
			{
				EasyTierProcessHelper.KillProcessTree(proc);
				return;
			}
			EasyTierProcessHelper.KillProcessTree(proc);
			FailWith(generation, failure);
		}

		/// <summary>Polls the RPC portal until it answers, the process dies, or we time out.</summary>
		private bool WaitForReady(int generation, ushort rpcPort, Process proc, out string failure)
		{
			failure = "";
			int elapsed = 0;
			int consecutive = 0;
			while (elapsed < ReadinessTimeoutMs)
			{
				if (IsCancelled(generation))
				{
					failure = "已取消。";
					return false;
				}
				if (HasExited(proc))
				{
					failure = "easytier-core.exe 在就绪之前退出（退出码 " + SafeExitCode(proc) + "）。" +
						Environment.NewLine + LogTail(12);
					return false;
				}

				if (EasyTierCli.TryTcpProbe("127.0.0.1", rpcPort, ProbeTimeoutMs))
				{
					consecutive++;
					if (consecutive >= RequiredConsecutiveProbes)
					{
						return true;
					}
				}
				else
				{
					consecutive = 0;
				}

				Sleep(PollIntervalMs);
				elapsed += PollIntervalMs;
			}

			failure = "等待 EasyTier 就绪超时（" + ReadinessTimeoutMs.ToString(CultureInfo.InvariantCulture) +
				" 毫秒）：RPC 门户 127.0.0.1:" + rpcPort.ToString(CultureInfo.InvariantCulture) +
				" 一直没有响应。" + Environment.NewLine + LogTail(12);
			return false;
		}

		/// <summary>Blocks until the worker is cancelled, keeping an eye on unexpected exits.</summary>
		private void WatchProcess(int generation, Process proc)
		{
			while (!IsCancelled(generation))
			{
				if (HasExited(proc))
				{
					int code = SafeExitCode(proc);
					lock (_sync)
					{
						_processExitedUnexpectedly = true;
						_unexpectedExitReason = "easytier-core.exe 意外退出（退出码 " +
							code.ToString(CultureInfo.InvariantCulture) + "）。" + Environment.NewLine + LogTail(12);
					}
					return;
				}
				Sleep(500);
			}
		}

		// ------------------------------------------------------------------ process

		private Process StartCore(List<string> args, bool elevated)
		{
			ProcessStartInfo psi = new ProcessStartInfo();
			psi.FileName = EasyTierPaths.CoreExecutable;
			psi.WorkingDirectory = EasyTierPaths.BaseDirectory;
			psi.Arguments = EasyTierProcessHelper.BuildCommandLine(args);

			if (elevated)
			{
				// Redirecting streams is impossible when UseShellExecute is true, which is
				// required for the "runas" verb.
				psi.UseShellExecute = true;
				psi.Verb = "runas";
				psi.WindowStyle = ProcessWindowStyle.Hidden;
			}
			else
			{
				psi.UseShellExecute = false;
				psi.CreateNoWindow = true;
				psi.RedirectStandardOutput = true;
				psi.RedirectStandardError = true;
			}

			Process proc = new Process();
			proc.StartInfo = psi;
			if (!elevated)
			{
				proc.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
				{
					if (e.Data == null)
					{
						return;
					}
					AppendLog(e.Data);
				};
				proc.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
				{
					if (e.Data == null)
					{
						return;
					}
					AppendLog("[stderr] " + e.Data);
				};
			}

			proc.Start();
			if (!elevated)
			{
				proc.BeginOutputReadLine();
				proc.BeginErrorReadLine();
			}
			return proc;
		}

		private static bool HasExited(Process proc)
		{
			if (proc == null)
			{
				return true;
			}
			try
			{
				return proc.HasExited;
			}
			catch (Exception)
			{
				return true;
			}
		}

		private static int SafeExitCode(Process proc)
		{
			if (proc == null)
			{
				return -1;
			}
			try
			{
				return proc.ExitCode;
			}
			catch (Exception)
			{
				return -1;
			}
		}

		private static int SafeId(Process proc)
		{
			if (proc == null)
			{
				return -1;
			}
			try
			{
				return proc.Id;
			}
			catch (Exception)
			{
				return -1;
			}
		}

		private static void Sleep(int ms)
		{
			try
			{
				Thread.Sleep(ms);
			}
			catch (ThreadInterruptedException)
			{
			}
		}

		// --------------------------------------------------------------- diagnostics

		/// <summary>True when the recent log lines look like a "needs administrator" failure.</summary>
		private bool LogSuggestsElevationRequired()
		{
			string text;
			lock (_logSync)
			{
				StringBuilder sb = new StringBuilder();
				int skip = Math.Max(0, _log.Count - 40);
				int index = 0;
				foreach (string line in _log)
				{
					if (index++ < skip)
					{
						continue;
					}
					sb.AppendLine(line);
				}
				text = sb.ToString();
			}
			if (text.Length == 0)
			{
				return false;
			}
			string lowered = text.ToLowerInvariant();
			for (int i = 0; i < ElevationHints.Length; i++)
			{
				if (lowered.IndexOf(ElevationHints[i], StringComparison.Ordinal) >= 0)
				{
					return true;
				}
			}
			return false;
		}

		private string BuildEarlyExitMessage(int exitCode, bool hint, string tail)
		{
			StringBuilder sb = new StringBuilder();
			sb.Append("easytier-core.exe 启动失败（退出码 ").Append(exitCode).Append("）。");
			if (hint)
			{
				sb.Append("日志显示需要管理员权限：创建 TUN 虚拟网卡（wintun）或抓包转发广播必须提权，");
				sb.Append("请右键以管理员身份运行游戏，或改用 EasyTierOptions.NoTun。");
			}
			else
			{
				sb.Append("常见原因：Packet.dll（Npcap）缺失或未安装 Npcap、wintun.dll 缺失、");
				sb.Append("RPC 端口被占用、网络名/密钥不合法、可执行文件被杀毒软件拦截。");
			}
			if (!string.IsNullOrEmpty(tail))
			{
				sb.Append(Environment.NewLine).Append(tail);
			}
			return sb.ToString();
		}

		private string LogTail(int maxLines)
		{
			List<string> lines = GetRecentLog(maxLines);
			if (lines.Count == 0)
			{
				return "";
			}
			StringBuilder sb = new StringBuilder();
			sb.Append("--- easytier-core.exe 最近 ").Append(lines.Count).Append(" 行日志 ---");
			for (int i = 0; i < lines.Count; i++)
			{
				sb.Append(Environment.NewLine).Append(lines[i]);
			}
			return sb.ToString();
		}

		// ------------------------------------------------------------- state plumbing

		private void AppendLog(string line)
		{
			if (line == null)
			{
				return;
			}
			string stamped = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + " " + line;
			lock (_logSync)
			{
				_log.Enqueue(stamped);
				while (_log.Count > LogCapacity)
				{
					_log.Dequeue();
				}
			}
			_pendingLog.Enqueue(stamped);
			// Never let the marshalling queue grow unbounded if Update() is not running.
			while (_pendingLog.Count > LogCapacity * 4)
			{
				string dropped;
				if (!_pendingLog.TryDequeue(out dropped))
				{
					break;
				}
			}
		}

		private void SetState(EasyTierState state)
		{
			bool changed;
			lock (_sync)
			{
				changed = _state != state;
				_state = state;
			}
			if (changed)
			{
				_pendingStates.Enqueue(state);
			}
		}

		private void FailWith(int generation, string message)
		{
			if (generation != _generation)
			{
				return;
			}
			lock (_sync)
			{
				_lastError = message;
				_process = null;
				_isElevated = false;
			}
			AppendLog("[runner] 启动失败：" + message);
			SetState(EasyTierState.Failed);
		}

		private void DrainPending()
		{
			string line;
			while (_pendingLog.TryDequeue(out line))
			{
				Action<string> handler = LogLineReceived;
				if (handler != null)
				{
					try
					{
						handler(line);
					}
					catch (Exception ex)
					{
						Debug.LogWarning("[EasyTier] LogLineReceived 回调异常：" + ex.Message);
					}
				}
			}

			EasyTierState latest = _raisedState;
			bool any = false;
			EasyTierState state;
			while (_pendingStates.TryDequeue(out state))
			{
				latest = state;
				any = true;
			}
			if (any && latest != _raisedState)
			{
				_raisedState = latest;
				Action<EasyTierState> handler = StateChanged;
				if (handler != null)
				{
					try
					{
						handler(latest);
					}
					catch (Exception ex)
					{
						Debug.LogWarning("[EasyTier] StateChanged 回调异常：" + ex.Message);
					}
				}
			}
		}

		/// <summary>
		/// Domain-reload / external-kill guard: a Running state whose native process has
		/// vanished must not survive.
		/// </summary>
		private void GuardProcessLiveness()
		{
			bool failed;
			string reason;
			lock (_sync)
			{
				failed = _processExitedUnexpectedly;
				reason = _unexpectedExitReason;
				if (failed)
				{
					_processExitedUnexpectedly = false;
					_unexpectedExitReason = "";
					_lastError = reason;
					_process = null;
				}
			}
			if (failed)
			{
				AppendLog("[runner] " + reason);
				SetState(EasyTierState.Failed);
				return;
			}

			Process proc;
			EasyTierState state;
			lock (_sync)
			{
				proc = _process;
				state = _state;
			}
			if (state == EasyTierState.Running && (proc == null || HasExited(proc)))
			{
				lock (_sync)
				{
					_lastError = "EasyTier 进程已经不存在（可能被外部结束或发生了域重载），虚拟局域网已断开。";
					_process = null;
					_isElevated = false;
				}
				AppendLog("[runner] " + _lastError);
				SetState(EasyTierState.Failed);
			}
		}

		private bool StateIsUsable()
		{
			return EasyTierPaths.IsSupportedPlatform && EasyTierPaths.IsInstalled;
		}

		private EasyTierState InitialState()
		{
			if (!EasyTierPaths.IsSupportedPlatform)
			{
				return EasyTierState.Unsupported;
			}
			if (!EasyTierPaths.IsInstalled)
			{
				return EasyTierState.NotInstalled;
			}
			return EasyTierState.Stopped;
		}

		private bool IsCancelled(int generation)
		{
			return _cancelWorker || generation != _generation;
		}

		private void TeardownWorkerAndProcess()
		{
			Process proc;
			Thread worker;
			lock (_sync)
			{
				_cancelWorker = true;
				_generation++;
				proc = _process;
				worker = _worker;
				_process = null;
				_worker = null;
			}
			if (proc != null)
			{
				EasyTierProcessHelper.KillProcessTree(proc);
			}
			if (worker != null && worker != Thread.CurrentThread && worker.IsAlive)
			{
				try
				{
					worker.Join(2000);
				}
				catch (Exception)
				{
				}
			}
		}
	}

	/// <summary>
	/// Low-level Windows process helpers shared by <see cref="EasyTierRunner"/> and
	/// <see cref="EasyTierCli"/>.
	/// <para>
	/// Two APIs that would make this trivial are NOT part of Unity 2022's .NET 4.7.1
	/// profile, so both are emulated here:
	/// <list type="bullet">
	/// <item><description><c>ProcessStartInfo.ArgumentList</c> — replaced by
	/// <see cref="BuildCommandLine"/>, a full implementation of the MSVCRT quoting
	/// rules, so a value containing spaces/quotes still arrives as one argv element.</description></item>
	/// <item><description><c>Process.Kill(bool entireProcessTree)</c> — tried through
	/// reflection and, when absent, replaced by <c>taskkill /PID &lt;pid&gt; /T /F</c>.</description></item>
	/// </list>
	/// </para>
	/// </summary>
	internal static class EasyTierProcessHelper
	{
		private static readonly MethodInfo KillTreeMethod =
			typeof(Process).GetMethod("Kill", new Type[] { typeof(bool) });

		/// <summary>
		/// Renders argv as a Windows command line, applying
		/// <c>CommandLineToArgvW</c>/MSVCRT quoting rules.
		/// </summary>
		public static string BuildCommandLine(IList<string> args)
		{
			if (args == null || args.Count == 0)
			{
				return "";
			}
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < args.Count; i++)
			{
				if (i > 0)
				{
					sb.Append(' ');
				}
				QuoteArgument(sb, args[i]);
			}
			return sb.ToString();
		}

		private static void QuoteArgument(StringBuilder sb, string argument)
		{
			if (argument == null)
			{
				argument = "";
			}
			bool needsQuotes = argument.Length == 0;
			for (int i = 0; i < argument.Length && !needsQuotes; i++)
			{
				char c = argument[i];
				if (c == ' ' || c == '\t' || c == '\n' || c == '\v' || c == '"')
				{
					needsQuotes = true;
				}
			}
			if (!needsQuotes)
			{
				sb.Append(argument);
				return;
			}

			sb.Append('"');
			int backslashes = 0;
			for (int i = 0; i < argument.Length; i++)
			{
				char c = argument[i];
				if (c == '\\')
				{
					backslashes++;
					continue;
				}
				if (c == '"')
				{
					// Escape every pending backslash plus the quote itself.
					sb.Append('\\', backslashes * 2 + 1);
					sb.Append('"');
					backslashes = 0;
					continue;
				}
				if (backslashes > 0)
				{
					sb.Append('\\', backslashes);
					backslashes = 0;
				}
				sb.Append(c);
			}
			// Trailing backslashes must be doubled before the closing quote.
			if (backslashes > 0)
			{
				sb.Append('\\', backslashes * 2);
			}
			sb.Append('"');
		}

		/// <summary>
		/// Kills a process and its children. Prefers
		/// <c>Process.Kill(entireProcessTree: true)</c> when the runtime exposes it, then
		/// falls back to <c>taskkill /PID &lt;pid&gt; /T /F</c>, then to a plain
		/// <c>Kill()</c>. Never throws.
		/// </summary>
		public static void KillProcessTree(Process proc)
		{
			if (proc == null)
			{
				return;
			}

			int pid = -1;
			try
			{
				if (!proc.HasExited)
				{
					pid = proc.Id;
				}
				else
				{
					return;
				}
			}
			catch (Exception)
			{
				pid = -1;
			}

			if (KillTreeMethod != null)
			{
				try
				{
					KillTreeMethod.Invoke(proc, new object[] { true });
					return;
				}
				catch (Exception)
				{
					// NotSupportedException on some platforms, or the process vanished.
				}
			}

			if (pid > 0)
			{
				if (RunTaskkill(pid))
				{
					return;
				}
			}

			try
			{
				proc.Kill();
			}
			catch (Exception)
			{
			}
		}

		private static bool RunTaskkill(int pid)
		{
			try
			{
				ProcessStartInfo psi = new ProcessStartInfo();
				psi.FileName = "taskkill";
				psi.Arguments = "/PID " + pid.ToString(CultureInfo.InvariantCulture) + " /T /F";
				psi.UseShellExecute = false;
				psi.CreateNoWindow = true;
				psi.RedirectStandardOutput = true;
				psi.RedirectStandardError = true;
				using (Process killer = Process.Start(psi))
				{
					if (killer == null)
					{
						return false;
					}
					killer.WaitForExit(5000);
					return true;
				}
			}
			catch (Exception)
			{
				return false;
			}
		}
	}
}
