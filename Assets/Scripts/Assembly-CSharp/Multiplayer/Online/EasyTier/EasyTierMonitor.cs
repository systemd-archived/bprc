using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace BadPiggies.Multiplayer.Online.EasyTier
{
	/// <summary>
	/// Polls <c>easytier-cli.exe</c> on a background thread and publishes the result on
	/// the Unity main thread. This is the only member-list/telemetry source the lobby
	/// needs: it turns EasyTier's peer table into <see cref="EasyTierPeer"/> rows
	/// carrying per-member latency and packet loss.
	/// <para>
	/// Requests never overlap (the worker loop is sequential) and the worker only runs
	/// while <see cref="EasyTierRunner.State"/> == <see cref="EasyTierState.Running"/>.
	/// </para>
	/// </summary>
	public sealed class EasyTierMonitor : MonoBehaviour
	{
		/// <summary>Smallest useful poll interval.</summary>
		public const float MinimumIntervalSeconds = 0.5f;

		private static EasyTierMonitor _instance;

		private readonly object _sync = new object();
		private readonly ConcurrentQueue<Snapshot> _pending = new ConcurrentQueue<Snapshot>();

		private EasyTierRunner _runner;
		private float _intervalSeconds = 2f;

		private Thread _worker;
		private CancellationTokenSource _cancellation;

		private IReadOnlyList<EasyTierPeer> _peers = new List<EasyTierPeer>();
		private EasyTierNodeInfo _nodeInfo;
		private string _lastError = "";

		/// <summary>Raised on the main thread after <see cref="Peers"/>/<see cref="NodeInfo"/> are refreshed.</summary>
		public event Action PeersUpdated;

		/// <summary>Creates (or returns) a <c>DontDestroyOnLoad</c> "[EasyTierMonitor]" GameObject.</summary>
		public static EasyTierMonitor GetOrCreate()
		{
			if (_instance != null)
			{
				return _instance;
			}
			GameObject host = new GameObject("[EasyTierMonitor]");
			UnityEngine.Object.DontDestroyOnLoad(host);
			_instance = host.AddComponent<EasyTierMonitor>();
			return _instance;
		}

		private void Awake()
		{
			if (_instance != null && _instance != this)
			{
				UnityEngine.Object.Destroy(this);
				return;
			}
			_instance = this;
		}

		private void OnDestroy()
		{
			Unbind();
			if (_instance == this)
			{
				_instance = null;
			}
		}

		private void OnApplicationQuit()
		{
			Unbind();
		}

		/// <summary>Latest peer table, newest poll. Never null; empty until the first successful poll.</summary>
		public IReadOnlyList<EasyTierPeer> Peers
		{
			get
			{
				lock (_sync)
				{
					return _peers;
				}
			}
		}

		/// <summary>Latest <c>node info</c> for this machine, or null before the first success.</summary>
		public EasyTierNodeInfo NodeInfo
		{
			get
			{
				lock (_sync)
				{
					return _nodeInfo;
				}
			}
		}

		/// <summary>Chinese error from the most recent failed poll. Empty when the last poll succeeded.</summary>
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

		/// <summary>True while the background poll loop is alive.</summary>
		public bool IsPolling
		{
			get
			{
				Thread worker = _worker;
				return worker != null && worker.IsAlive;
			}
		}

		/// <summary>
		/// Starts polling the RPC portal of <paramref name="runner"/>'s instance.
		/// Calling Bind again replaces the previous binding.
		/// </summary>
		public void Bind(EasyTierRunner runner, float intervalSeconds = 2f)
		{
			Unbind();
			if (runner == null)
			{
				return;
			}
			if (intervalSeconds < MinimumIntervalSeconds)
			{
				intervalSeconds = MinimumIntervalSeconds;
			}

			CancellationTokenSource cancellation = new CancellationTokenSource();
			_runner = runner;
			_intervalSeconds = intervalSeconds;
			_cancellation = cancellation;

			Thread worker = new Thread(delegate()
			{
				PollLoop(runner, intervalSeconds, cancellation.Token);
			});
			worker.IsBackground = true;
			worker.Name = "EasyTierMonitor";
			_worker = worker;
			worker.Start();
		}

		/// <summary>Stops polling. Safe to call more than once.</summary>
		public void Unbind()
		{
			CancellationTokenSource cancellation = _cancellation;
			Thread worker = _worker;
			_cancellation = null;
			_worker = null;
			_runner = null;

			if (cancellation != null)
			{
				try
				{
					cancellation.Cancel();
				}
				catch (Exception)
				{
				}
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
			if (cancellation != null)
			{
				try
				{
					cancellation.Dispose();
				}
				catch (Exception)
				{
				}
			}

			lock (_sync)
			{
				_peers = new List<EasyTierPeer>();
				_nodeInfo = null;
				_lastError = "";
			}
		}

		private void Update()
		{
			Snapshot snapshot;
			Snapshot latest = null;
			while (_pending.TryDequeue(out snapshot))
			{
				latest = snapshot;
			}
			if (latest == null)
			{
				return;
			}

			lock (_sync)
			{
				_peers = latest.Peers;
				_nodeInfo = latest.NodeInfo;
				_lastError = latest.Error;
			}

			Action handler = PeersUpdated;
			if (handler != null)
			{
				try
				{
					handler();
				}
				catch (Exception ex)
				{
					Debug.LogWarning("[EasyTier] PeersUpdated 回调异常：" + ex.Message);
				}
			}
			// Keep only the freshest result if the loop outran the main thread.
			while (_pending.TryDequeue(out snapshot))
			{
			}
		}

		// ------------------------------------------------------------------ worker

		private void PollLoop(EasyTierRunner runner, float intervalSeconds, CancellationToken token)
		{
			// Plain System.Math on purpose: this runs off the Unity main thread.
			int intervalMs = Math.Max(250, (int)Math.Round(intervalSeconds * 1000f));
			List<EasyTierPeer> peers = new List<EasyTierPeer>();
			EasyTierNodeInfo nodeInfo = null;
			string error = "";

			while (!token.IsCancellationRequested)
			{
				try
				{
					bool running;
					ushort rpcPort;
					try
					{
						running = runner != null && runner.State == EasyTierState.Running;
						EasyTierOptions options = runner == null ? null : runner.Options;
						rpcPort = options == null ? EasyTierCli.DefaultRpcPortalPort : options.EffectiveRpcPortalPort;
					}
					catch (Exception)
					{
						running = false;
						rpcPort = EasyTierCli.DefaultRpcPortalPort;
					}

					if (running)
					{
						EasyTierCli cli = new EasyTierCli(rpcPort);

						List<EasyTierPeer> fetchedPeers;
						string peerError;
						bool peersOk = cli.TryGetPeers(out fetchedPeers, out peerError, 4000);
						if (peersOk)
						{
							peers = fetchedPeers;
						}

						EasyTierNodeInfo fetchedInfo;
						string infoError;
						bool infoOk = cli.TryGetNodeInfo(out fetchedInfo, out infoError, 4000);
						if (infoOk)
						{
							nodeInfo = fetchedInfo;
						}

						if (peersOk && infoOk)
						{
							error = "";
						}
						else if (!peersOk && !infoOk)
						{
							error = peerError;
						}
						else if (!peersOk)
						{
							error = peerError;
						}
						else
						{
							// Peer list is the important one; keep the instance description stale
							// rather than surfacing a noisy error.
							error = "";
						}

						_pending.Enqueue(new Snapshot(peers, nodeInfo, error));
					}
				}
				catch (Exception ex)
				{
					_pending.Enqueue(new Snapshot(peers, nodeInfo, "EasyTier 状态轮询异常：" + ex.Message));
				}

				int slept = 0;
				while (slept < intervalMs && !token.IsCancellationRequested)
				{
					int step = Math.Min(200, intervalMs - slept);
					try
					{
						if (token.WaitHandle.WaitOne(step))
						{
							return;
						}
					}
					catch (Exception)
					{
						return;
					}
					slept += step;
				}
			}
		}

		private sealed class Snapshot
		{
			public readonly List<EasyTierPeer> Peers;
			public readonly EasyTierNodeInfo NodeInfo;
			public readonly string Error;

			public Snapshot(List<EasyTierPeer> peers, EasyTierNodeInfo nodeInfo, string error)
			{
				Peers = peers == null ? new List<EasyTierPeer>() : peers;
				NodeInfo = nodeInfo;
				Error = error == null ? "" : error;
			}
		}
	}
}
