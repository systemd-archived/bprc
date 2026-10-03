using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BadPiggies.Multiplayer.Online.EasyTier
{
	/// <summary>
	/// Everything <c>easytier-core.exe</c> needs for one virtual LAN instance.
	/// One instance == one room. Plain serializable POCO so it can be persisted with
	/// <c>JsonUtility</c>/PlayerPrefs by the lobby layer.
	/// </summary>
	[Serializable]
	public class EasyTierOptions
	{
		/// <summary>Network name. Required. Everyone in the same room uses the same value.</summary>
		public string NetworkName = "";

		/// <summary>Network secret. Required. Doubles as the room password.</summary>
		public string NetworkSecret = "";

		/// <summary>
		/// e.g. "10.126.126.1". Empty + <see cref="NoTun"/> = false means "let EasyTier
		/// pick an address" (DHCP); see <see cref="BuildArguments"/>.
		/// </summary>
		public string Ipv4 = "";

		/// <summary>Name peers see for this machine.</summary>
		public string Hostname = "";

		/// <summary>Per-room instance name (<c>-m</c>). Lets two instances coexist.</summary>
		public string InstanceName = "";

		/// <summary>Bootstrap peers, each already in EasyTier URL form, e.g. "udp://1.2.3.4:11010".</summary>
		public List<string> Peers = new List<string>();

		/// <summary>
		/// Optional rendezvous node, e.g. "tcp://192.168.1.5:11010". Empty by default: the lobby
		/// never assumes a public node, and normally dials the host's own address as a peer.
		/// </summary>
		public string ExternalNode = "";

		/// <summary>Listener port. 0 = let EasyTier use its own defaults.</summary>
		public ushort ListenerPort;

		/// <summary>Loopback RPC portal port. 0 = EasyTier's default of 15888.</summary>
		public ushort RpcPortalPort;

		/// <summary>Do not create a TUN device. Runs unelevated; no virtual adapter.</summary>
		public bool NoTun;

		/// <summary>Let EasyTier assign the address automatically.</summary>
		public bool Dhcp;

		/// <summary>Prefer the lowest latency path instead of the shortest path.</summary>
		public bool LatencyFirst;

		/// <summary>Disable automatic P2P (only explicitly marked peers connect).</summary>
		public bool DisableP2p;

		/// <summary>
		/// Relay LAN UDP broadcasts across the virtual LAN so LAN game discovery works.
		/// Windows only, and requires administrator rights.
		/// </summary>
		public bool EnableUdpBroadcastRelay;

		/// <summary>Use the multi-threaded runtime.</summary>
		public bool MultiThread;

		/// <summary>Disable peer-to-peer encryption. Every node must agree.</summary>
		public bool DisableEncryption;

		/// <summary>Console log level: error / warn / info / debug / trace.</summary>
		public string ConsoleLogLevel = "warn";

		/// <summary>TUN MTU. 0 = EasyTier default.</summary>
		public int Mtu;

		/// <summary>Deep copy. Safe to mutate afterwards.</summary>
		public EasyTierOptions Clone()
		{
			EasyTierOptions copy = new EasyTierOptions();
			copy.NetworkName = NetworkName;
			copy.NetworkSecret = NetworkSecret;
			copy.Ipv4 = Ipv4;
			copy.Hostname = Hostname;
			copy.InstanceName = InstanceName;
			copy.ExternalNode = ExternalNode;
			copy.ListenerPort = ListenerPort;
			copy.RpcPortalPort = RpcPortalPort;
			copy.NoTun = NoTun;
			copy.Dhcp = Dhcp;
			copy.LatencyFirst = LatencyFirst;
			copy.DisableP2p = DisableP2p;
			copy.EnableUdpBroadcastRelay = EnableUdpBroadcastRelay;
			copy.MultiThread = MultiThread;
			copy.DisableEncryption = DisableEncryption;
			copy.ConsoleLogLevel = ConsoleLogLevel;
			copy.Mtu = Mtu;
			copy.Peers = new List<string>();
			if (Peers != null)
			{
				for (int i = 0; i < Peers.Count; i++)
				{
					copy.Peers.Add(Peers[i]);
				}
			}
			return copy;
		}

		/// <summary>
		/// Returns null when the bag is usable, otherwise a Chinese explanation for the
		/// user. <see cref="BuildArguments"/> performs no validation of its own.
		/// </summary>
		public string Validate()
		{
			if (string.IsNullOrEmpty(NetworkName) || NetworkName.Trim().Length == 0)
			{
				return "EasyTier 网络名（NetworkName）不能为空。";
			}
			if (string.IsNullOrEmpty(NetworkSecret) || NetworkSecret.Trim().Length == 0)
			{
				return "EasyTier 网络密钥（NetworkSecret）不能为空，它同时也是房间密码。";
			}
			if (Mtu < 0)
			{
				return "MTU 不能为负数。";
			}
			return null;
		}

		/// <summary>The RPC portal port actually used (15888 when <see cref="RpcPortalPort"/> is 0).</summary>
		public ushort EffectiveRpcPortalPort
		{
			get { return RpcPortalPort == 0 ? (ushort)15888 : RpcPortalPort; }
		}

		/// <summary>
		/// Builds the exact argv for <c>easytier-core.exe</c>: one list element per argv
		/// item, so the caller never has to quote anything by hand.
		/// <para>
		/// Ordering: every option that takes a value comes first, and the boolean
		/// switches come last, so a bare switch can never swallow a following value.
		/// </para>
		/// <para>
		/// Note: when <see cref="Ipv4"/> is empty, <see cref="NoTun"/> is false and
		/// <see cref="Dhcp"/> is false, <c>--dhcp</c> is emitted anyway — otherwise
		/// EasyTier would create a TUN device with no address at all.
		/// </para>
		/// </summary>
		public static List<string> BuildArguments(EasyTierOptions options)
		{
			List<string> args = new List<string>();
			if (options == null)
			{
				return args;
			}

			if (!string.IsNullOrEmpty(options.NetworkName))
			{
				args.Add("--network-name");
				args.Add(options.NetworkName);
			}
			if (!string.IsNullOrEmpty(options.NetworkSecret))
			{
				args.Add("--network-secret");
				args.Add(options.NetworkSecret);
			}
			if (!string.IsNullOrEmpty(options.Ipv4))
			{
				args.Add("-i");
				args.Add(options.Ipv4.Trim());
			}
			if (!string.IsNullOrEmpty(options.Hostname))
			{
				args.Add("--hostname");
				args.Add(options.Hostname.Trim());
			}
			if (!string.IsNullOrEmpty(options.InstanceName))
			{
				args.Add("-m");
				args.Add(options.InstanceName.Trim());
			}
			if (options.Peers != null)
			{
				for (int i = 0; i < options.Peers.Count; i++)
				{
					string peer = options.Peers[i];
					if (string.IsNullOrEmpty(peer) || peer.Trim().Length == 0)
					{
						continue;
					}
					args.Add("-p");
					args.Add(peer.Trim());
				}
			}
			if (!string.IsNullOrEmpty(options.ExternalNode))
			{
				args.Add("-e");
				args.Add(options.ExternalNode.Trim());
			}
			if (options.ListenerPort != 0)
			{
				// EasyTier accepts a bare port number here and expands it to the
				// tcp/udp/ws/wss/wg listeners on and around that port.
				args.Add("-l");
				args.Add(options.ListenerPort.ToString(CultureInfo.InvariantCulture));
			}
			if (options.RpcPortalPort != 0)
			{
				args.Add("-r");
				args.Add("127.0.0.1:" + options.RpcPortalPort.ToString(CultureInfo.InvariantCulture));
			}
			if (options.Mtu > 0)
			{
				args.Add("--mtu");
				args.Add(options.Mtu.ToString(CultureInfo.InvariantCulture));
			}
			if (!string.IsNullOrEmpty(options.ConsoleLogLevel))
			{
				args.Add("--console-log-level");
				args.Add(options.ConsoleLogLevel.Trim());
			}

			// Boolean switches last: they take an optional value, so keeping them at
			// the end removes any chance of clap consuming the next argument.
			if (options.Dhcp || (!options.NoTun && string.IsNullOrEmpty(options.Ipv4)))
			{
				args.Add("--dhcp");
			}
			if (options.NoTun)
			{
				args.Add("--no-tun");
			}
			if (options.LatencyFirst)
			{
				args.Add("--latency-first");
			}
			if (options.DisableP2p)
			{
				args.Add("--disable-p2p");
			}
			if (options.EnableUdpBroadcastRelay)
			{
				args.Add("--enable-udp-broadcast-relay");
			}
			if (options.MultiThread)
			{
				args.Add("--multi-thread");
			}
			if (options.DisableEncryption)
			{
				args.Add("--disable-encryption");
			}

			return args;
		}

		/// <summary>
		/// Adds the missing scheme to a bare "host:port" peer. EasyTier itself wants
		/// "udp://host:port" / "tcp://host:port"; this helper exists so the lobby UI can
		/// accept what a human types. Values that already contain "://" pass through
		/// unchanged, as does anything unparseable.
		/// </summary>
		public static string NormalizePeer(string peer)
		{
			if (string.IsNullOrEmpty(peer))
			{
				return peer;
			}
			string trimmed = peer.Trim();
			if (trimmed.Length == 0 || trimmed.IndexOf("://", StringComparison.Ordinal) >= 0)
			{
				return trimmed;
			}
			return "udp://" + trimmed;
		}

		/// <summary>Renders an argv list as a single readable command line (for logs only, not for launching).</summary>
		public static string DescribeArguments(List<string> args)
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
				string a = args[i];
				if (a.IndexOf(' ') >= 0)
				{
					sb.Append('"').Append(a).Append('"');
				}
				else
				{
					sb.Append(a);
				}
			}
			return sb.ToString();
		}
	}
}
