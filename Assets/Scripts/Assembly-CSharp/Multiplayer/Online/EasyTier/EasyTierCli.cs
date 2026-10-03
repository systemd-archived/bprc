using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace BadPiggies.Multiplayer.Online.EasyTier
{
	/// <summary>
	/// Thin, synchronous wrapper around <c>easytier-cli.exe</c>.
	/// <para>
	/// Every call spawns the CLI with a hard timeout and parses its
	/// <c>-o json</c> output through <see cref="EasyTierJson"/>. Production code must
	/// NOT call this from the Unity main thread — it blocks for up to
	/// <c>timeoutMs</c>. <see cref="EasyTierMonitor"/> is the main-thread-friendly
	/// wrapper that drives it from a worker thread.
	/// </para>
	/// </summary>
	public sealed class EasyTierCli
	{
		/// <summary>EasyTier's default RPC portal port.</summary>
		public const ushort DefaultRpcPortalPort = 15888;

		private readonly ushort _rpcPortalPort;

		/// <param name="rpcPortalPort">RPC portal port of the instance to query. 0 =&gt; 15888.</param>
		public EasyTierCli(ushort rpcPortalPort)
		{
			_rpcPortalPort = rpcPortalPort == 0 ? DefaultRpcPortalPort : rpcPortalPort;
		}

		/// <summary>The port actually queried (never 0).</summary>
		public ushort RpcPortalPort
		{
			get { return _rpcPortalPort; }
		}

		/// <summary>
		/// Runs <c>peer list</c>. Returns false with a Chinese <paramref name="error"/>
		/// on timeout, non-zero exit, empty output or unparseable JSON.
		/// </summary>
		public bool TryGetPeers(out List<EasyTierPeer> peers, out string error, int timeoutMs = 4000)
		{
			peers = new List<EasyTierPeer>();
			string stdout;
			if (!Run(new string[] { "peer", "list" }, timeoutMs, out stdout, out error))
			{
				return false;
			}

			List<EasyTierPeer> parsed = EasyTierJson.ParsePeerList(stdout);
			if (parsed == null)
			{
				error = "easytier-cli peer list 的 JSON 解析失败（返回值为 null）。";
				return false;
			}
			if (parsed.Count == 0)
			{
				error = "easytier-cli peer list 没有返回任何对等节点（网络可能还没有建立起来）。";
				return false;
			}
			peers = parsed;
			return true;
		}

		/// <summary>
		/// Runs <c>node info</c>. Returns false with a Chinese <paramref name="error"/>
		/// on timeout, non-zero exit, empty output or unparseable JSON.
		/// </summary>
		public bool TryGetNodeInfo(out EasyTierNodeInfo info, out string error, int timeoutMs = 4000)
		{
			info = null;
			string stdout;
			if (!Run(new string[] { "node", "info" }, timeoutMs, out stdout, out error))
			{
				return false;
			}

			EasyTierNodeInfo parsed = EasyTierJson.ParseNodeInfo(stdout);
			if (parsed == null)
			{
				error = "easytier-cli node info 的 JSON 解析失败。";
				return false;
			}
			info = parsed;
			return true;
		}

		/// <summary>
		/// Single-shot TCP probe of the RPC portal — the cheapest "is this instance up?"
		/// check, and what <see cref="EasyTierRunner"/> polls while starting.
		/// </summary>
		public static bool TryTcpProbe(string host, ushort port, int timeoutMs)
		{
			if (timeoutMs <= 0)
			{
				timeoutMs = 250;
			}
			TcpClient client = null;
			try
			{
				client = new TcpClient();
				IAsyncResult result = client.BeginConnect(string.IsNullOrEmpty(host) ? "127.0.0.1" : host, port, null, null);
				if (!result.AsyncWaitHandle.WaitOne(timeoutMs, false))
				{
					return false;
				}
				client.EndConnect(result);
				return client.Connected;
			}
			catch (Exception)
			{
				return false;
			}
			finally
			{
				if (client != null)
				{
					try
					{
						client.Close();
					}
					catch (Exception)
					{
					}
				}
			}
		}

		// ------------------------------------------------------------------ internals

		private bool Run(string[] command, int timeoutMs, out string stdout, out string error)
		{
			stdout = "";
			error = "";

			if (timeoutMs <= 0)
			{
				timeoutMs = 4000;
			}

			if (!EasyTierPaths.IsSupportedPlatform)
			{
				error = EasyTierPaths.MissingReason;
				return false;
			}
			string cliPath = EasyTierPaths.CliExecutable;
			if (!File.Exists(cliPath))
			{
				error = EasyTierPaths.MissingReason;
				return false;
			}

			List<string> args = new List<string>();
			args.Add("-p");
			args.Add("127.0.0.1:" + _rpcPortalPort.ToString(CultureInfo.InvariantCulture));
			args.Add("-o");
			args.Add("json");
			if (command != null)
			{
				for (int i = 0; i < command.Length; i++)
				{
					args.Add(command[i]);
				}
			}

			string commandText = "easytier-cli " + EasyTierOptions.DescribeArguments(args);

			Process proc = null;
			StringBuilder outBuffer = new StringBuilder();
			StringBuilder errBuffer = new StringBuilder();
			DataReceivedEventHandler outHandler = delegate(object sender, DataReceivedEventArgs e)
			{
				if (e.Data == null)
				{
					return;
				}
				lock (outBuffer)
				{
					outBuffer.AppendLine(e.Data);
				}
			};
			DataReceivedEventHandler errHandler = delegate(object sender, DataReceivedEventArgs e)
			{
				if (e.Data == null)
				{
					return;
				}
				lock (errBuffer)
				{
					errBuffer.AppendLine(e.Data);
				}
			};

			try
			{
				ProcessStartInfo psi = new ProcessStartInfo();
				psi.FileName = cliPath;
				psi.Arguments = EasyTierProcessHelper.BuildCommandLine(args);
				psi.WorkingDirectory = EasyTierPaths.BaseDirectory;
				psi.UseShellExecute = false;
				psi.CreateNoWindow = true;
				psi.RedirectStandardOutput = true;
				psi.RedirectStandardError = true;

				proc = new Process();
				proc.StartInfo = psi;
				proc.OutputDataReceived += outHandler;
				proc.ErrorDataReceived += errHandler;
				proc.Start();
				proc.BeginOutputReadLine();
				proc.BeginErrorReadLine();

				if (!proc.WaitForExit(timeoutMs))
				{
					EasyTierProcessHelper.KillProcessTree(proc);
					error = "easytier-cli 查询超时（" + timeoutMs.ToString(CultureInfo.InvariantCulture) +
						" 毫秒）：" + commandText + "。请确认 EasyTier 实例已经启动，且 RPC 端口 " +
						_rpcPortalPort.ToString(CultureInfo.InvariantCulture) + " 没有被别的程序占用。";
					return false;
				}

				// The timed wait does not cover the asynchronous stream readers.
				try
				{
					proc.WaitForExit(1000);
				}
				catch (Exception)
				{
				}

				int exitCode;
				try
				{
					exitCode = proc.ExitCode;
				}
				catch (Exception)
				{
					exitCode = -1;
				}

				lock (outBuffer)
				{
					stdout = outBuffer.ToString();
				}
				string stderrText;
				lock (errBuffer)
				{
					stderrText = errBuffer.ToString();
				}

				if (exitCode != 0)
				{
					string tail = FirstNonEmptyLine(stderrText);
					if (tail.Length == 0)
					{
						tail = FirstNonEmptyLine(stdout);
					}
					error = "easytier-cli 退出码 " + exitCode.ToString(CultureInfo.InvariantCulture) + "（" +
						commandText + "）" + (tail.Length > 0 ? "：" + tail : "。");
					return false;
				}
				if (string.IsNullOrEmpty(stdout) || stdout.Trim().Length == 0)
				{
					error = "easytier-cli 没有输出任何内容（" + commandText + "）。EasyTier 实例可能还没有就绪。";
					return false;
				}
				return true;
			}
			catch (Exception ex)
			{
				error = "调用 easytier-cli 失败（" + commandText + "）：" + ex.Message;
				stdout = "";
				return false;
			}
			finally
			{
				if (proc != null)
				{
					try
					{
						proc.OutputDataReceived -= outHandler;
						proc.ErrorDataReceived -= errHandler;
						proc.Dispose();
					}
					catch (Exception)
					{
					}
				}
			}
		}

		private static string FirstNonEmptyLine(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return "";
			}
			string[] lines = text.Replace("\r\n", "\n").Split('\n');
			for (int i = 0; i < lines.Length; i++)
			{
				string line = lines[i].Trim();
				if (line.Length > 0)
				{
					return line.Length > 300 ? line.Substring(0, 300) : line;
				}
			}
			return "";
		}
	}
}
