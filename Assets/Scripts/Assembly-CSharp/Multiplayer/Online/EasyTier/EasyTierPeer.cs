using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BadPiggies.Multiplayer.Online.EasyTier
{
	/// <summary>
	/// One row of <c>easytier-cli -o json peer list</c>.
	/// <para>
	/// Deliberately free of any <c>UnityEngine</c> dependency so it can be compiled and
	/// exercised by a plain console harness (see <c>Tools/Online/EasyTierHarness/</c>).
	/// </para>
	/// </summary>
	public sealed class EasyTierPeer
	{
		/// <summary>Peer hostname as reported by EasyTier. May be empty.</summary>
		public string Hostname = "";

		/// <summary>Virtual IPv4 of the peer inside the EasyTier network. May be empty.</summary>
		public string Ipv4 = "";

		/// <summary>Proxy CIDR advertised by the peer. May be empty.</summary>
		public string Cidr = "";

		/// <summary>"Local" for this node, otherwise the routing cost as reported (often a number in string form).</summary>
		public string Cost = "";

		/// <summary>Tunnel protocol, e.g. "tcp" / "udp" / "quic". "-" when unknown.</summary>
		public string TunnelProtocol = "";

		/// <summary>NAT type, e.g. "Symmetric" / "PortRestricted" / "Unknown".</summary>
		public string NatType = "";

		/// <summary>Numeric peer id, kept as a string because EasyTier reports it that way.</summary>
		public string PeerId = "";

		/// <summary>Peer's EasyTier version string.</summary>
		public string Version = "";

		/// <summary>Round trip latency in milliseconds. <see cref="float.NaN"/> when unknown ("-").</summary>
		public float LatencyMs = float.NaN;

		/// <summary>Packet loss as a 0..1 fraction. <see cref="float.NaN"/> when unknown ("-").</summary>
		public float LossRate = float.NaN;

		/// <summary>Bytes received. -1 when unknown ("-").</summary>
		public long RxBytes = -1L;

		/// <summary>Bytes sent. -1 when unknown ("-").</summary>
		public long TxBytes = -1L;

		/// <summary>True for this machine's own entry (EasyTier reports cost == "Local").</summary>
		public bool IsLocal;

		/// <summary>True when this row is a real, addressable remote peer.</summary>
		public bool IsRemote
		{
			get { return !IsLocal && !string.IsNullOrEmpty(Ipv4); }
		}

		public bool HasLatency
		{
			get { return !float.IsNaN(LatencyMs) && !float.IsInfinity(LatencyMs); }
		}

		public override string ToString()
		{
			return (string.IsNullOrEmpty(Hostname) ? "(unnamed)" : Hostname) +
				" ipv4=" + (string.IsNullOrEmpty(Ipv4) ? "-" : Ipv4) +
				" id=" + (string.IsNullOrEmpty(PeerId) ? "-" : PeerId) +
				" cost=" + (string.IsNullOrEmpty(Cost) ? "-" : Cost) +
				" lat_ms=" + (HasLatency ? LatencyMs.ToString("0.##", CultureInfo.InvariantCulture) : "-") +
				" local=" + (IsLocal ? "yes" : "no");
		}
	}

	/// <summary>
	/// The subset of <c>easytier-cli -o json node info</c> this project cares about.
	/// Unity-free for the same reason as <see cref="EasyTierPeer"/>.
	/// </summary>
	public sealed class EasyTierNodeInfo
	{
		public long PeerId = -1L;
		public string Ipv4Address = "";
		public string Hostname = "";
		public string InstanceId = "";
		public string Version = "";

		/// <summary>First public IP reported by STUN. Empty when STUN has not answered.</summary>
		public string PublicIp = "";

		/// <summary>UDP NAT type from <c>stun_info</c> (falls back to the TCP one).</summary>
		public string NatType = "";

		public List<string> Listeners = new List<string>();

		/// <summary>Raw <c>feature_flag</c> map, normalised to ints (true =&gt; 1, false =&gt; 0).</summary>
		public Dictionary<string, int> FeatureFlags = new Dictionary<string, int>();

		public override string ToString()
		{
			return "peer_id=" + PeerId.ToString(CultureInfo.InvariantCulture) +
				" ipv4=" + (string.IsNullOrEmpty(Ipv4Address) ? "-" : Ipv4Address) +
				" host=" + (string.IsNullOrEmpty(Hostname) ? "-" : Hostname) +
				" nat=" + (string.IsNullOrEmpty(NatType) ? "-" : NatType) +
				" public=" + (string.IsNullOrEmpty(PublicIp) ? "-" : PublicIp) +
				" version=" + (string.IsNullOrEmpty(Version) ? "-" : Version);
		}
	}

	/// <summary>
	/// Tolerant parsers for the <c>easytier-cli -o json</c> output. Every method is
	/// total: unknown fields, missing fields, "-" placeholders, numbers quoted as
	/// strings and outright malformed input all degrade to an empty list / null instead
	/// of throwing.
	/// </summary>
	public static class EasyTierJson
	{
		private static readonly string[] PeerArrayNames =
		{
			"peers", "peer_list", "peerList", "list", "items", "data", "routes"
		};

		// ------------------------------------------------------------------ peers

		/// <summary>
		/// Parses <c>easytier-cli -o json peer list</c>. Never throws; returns an empty
		/// list when the payload is not usable.
		/// </summary>
		public static List<EasyTierPeer> ParsePeerList(string json)
		{
			List<EasyTierPeer> peers = new List<EasyTierPeer>();
			JToken root = ReadFirstJsonValue(json);
			if (root == null)
			{
				return peers;
			}

			JArray array = root as JArray;
			if (array == null)
			{
				JObject obj = root as JObject;
				if (obj != null)
				{
					for (int i = 0; i < PeerArrayNames.Length && array == null; i++)
					{
						array = GetToken(obj, PeerArrayNames[i]) as JArray;
					}
					if (array == null)
					{
						// A single peer object instead of a list.
						if (GetToken(obj, "hostname", "ipv4", "peer_id", "id") != null)
						{
							peers.Add(ParsePeer(obj));
							return peers;
						}
					}
				}
			}

			if (array == null)
			{
				return peers;
			}

			for (int i = 0; i < array.Count; i++)
			{
				JObject row = array[i] as JObject;
				if (row == null)
				{
					continue;
				}
				peers.Add(ParsePeer(row));
			}
			return peers;
		}

		private static EasyTierPeer ParsePeer(JObject row)
		{
			EasyTierPeer peer = new EasyTierPeer();
			if (row == null)
			{
				return peer;
			}

			peer.Hostname = GetString(row, "hostname", "host_name", "name");
			peer.Ipv4 = GetString(row, "ipv4", "ipv4_addr", "ip", "virtual_ipv4");
			peer.Cidr = GetString(row, "cidr", "proxy_cidrs");
			peer.Cost = GetString(row, "cost", "route_cost");
			peer.TunnelProtocol = GetString(row, "tunnel_proto", "tunnel_protocol", "proto");
			peer.NatType = GetString(row, "nat_type", "nat");
			peer.PeerId = GetString(row, "id", "peer_id", "peerid");
			peer.Version = GetString(row, "version");

			peer.LatencyMs = ParseLatencyMs(row, "lat_ms", "latency_ms", "latency", "rtt_ms");
			peer.LossRate = ParseLossRate(row, "loss_rate", "lossrate", "loss");
			peer.RxBytes = ParseByteCount(row, "rx_bytes", "rxbytes", "rx", "bytes_in");
			peer.TxBytes = ParseByteCount(row, "tx_bytes", "txbytes", "tx", "bytes_out");

			peer.IsLocal = peer.Cost.Equals("Local", StringComparison.OrdinalIgnoreCase);
			return peer;
		}

		// -------------------------------------------------------------- node info

		/// <summary>
		/// Parses <c>easytier-cli -o json node info</c>. Returns null when the payload
		/// cannot be understood.
		/// </summary>
		public static EasyTierNodeInfo ParseNodeInfo(string json)
		{
			JToken rootToken = ReadFirstJsonValue(json);
			if (rootToken == null)
			{
				return null;
			}

			JObject root = rootToken as JObject;
			if (root == null)
			{
				// Some builds wrap the payload: [ { ... } ].
				JArray rootArray = rootToken as JArray;
				if (rootArray != null && rootArray.Count > 0)
				{
					root = rootArray[0] as JObject;
				}
			}
			if (root == null)
			{
				return null;
			}

			EasyTierNodeInfo info = new EasyTierNodeInfo();
			info.PeerId = GetLong(root, -1L, "peer_id", "peerid", "id");
			info.Ipv4Address = GetString(root, "ipv4_addr", "ipv4", "virtual_ipv4");
			info.Hostname = GetString(root, "hostname", "host_name", "name");
			info.InstanceId = GetString(root, "inst_id", "instance_id", "instance_name");
			info.Version = GetString(root, "version");

			// Listeners.
			JArray listeners = GetToken(root, "listeners") as JArray;
			if (listeners != null)
			{
				for (int i = 0; i < listeners.Count; i++)
				{
					string s = TokenToString(listeners[i]);
					if (!string.IsNullOrEmpty(s))
					{
						info.Listeners.Add(s);
					}
				}
			}

			// STUN / NAT.
			JObject stun = GetToken(root, "stun_info", "stun") as JObject;
			if (stun != null)
			{
				info.NatType = GetNatType(stun, "udp_nat_type", "nat_type");
				if (string.IsNullOrEmpty(info.NatType))
				{
					info.NatType = GetNatType(stun, "tcp_nat_type");
				}
				info.PublicIp = GetString(stun, "public_ip", "public_ipv4");
			}
			if (string.IsNullOrEmpty(info.NatType))
			{
				info.NatType = GetNatType(root, "nat_type");
			}
			if (string.IsNullOrEmpty(info.PublicIp))
			{
				info.PublicIp = GetString(root, "public_ip");
			}

			// ip_list.public_ipv4.addr is the newer shape.
			if (string.IsNullOrEmpty(info.PublicIp))
			{
				JObject ipList = GetToken(root, "ip_list") as JObject;
				JObject publicIpv4 = ipList == null ? null : (GetToken(ipList, "public_ipv4", "public_ipv6") as JObject);
				if (publicIpv4 != null)
				{
					info.PublicIp = GetString(publicIpv4, "addr", "address", "ip");
				}
			}

			// Feature flags.
			JObject flags = GetToken(root, "feature_flag", "feature_flags") as JObject;
			if (flags != null)
			{
				foreach (KeyValuePair<string, JToken> pair in flags)
				{
					info.FeatureFlags[pair.Key] = ToInt(pair.Value);
				}
			}

			return info;
		}

		// ----------------------------------------------------------------- helpers

		/// <summary>
		/// Reads the first JSON value in <paramref name="text"/> and ignores anything
		/// before or after it, so CLI banner lines or trailing log output cannot break
		/// parsing.
		/// </summary>
		private static JToken ReadFirstJsonValue(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return null;
			}
			string trimmed = text.Trim();
			if (trimmed.Length == 0)
			{
				return null;
			}
			// Strip a UTF-8 BOM if it survived decoding.
			if (trimmed.Length > 0 && trimmed[0] == '\uFEFF')
			{
				trimmed = trimmed.Substring(1);
			}
			// Skip any noise before the payload starts.
			int start = IndexOfJsonStart(trimmed);
			if (start < 0)
			{
				return null;
			}
			if (start > 0)
			{
				trimmed = trimmed.Substring(start);
			}

			try
			{
				using (StringReader sr = new StringReader(trimmed))
				using (JsonTextReader reader = new JsonTextReader(sr))
				{
					reader.DateParseHandling = DateParseHandling.None;
					reader.FloatParseHandling = FloatParseHandling.Double;
					return JToken.ReadFrom(reader);
				}
			}
			catch (Exception)
			{
				return null;
			}
		}

		private static int IndexOfJsonStart(string text)
		{
			for (int i = 0; i < text.Length; i++)
			{
				char c = text[i];
				if (c == '{' || c == '[')
				{
					return i;
				}
			}
			return -1;
		}

		private static JToken GetToken(JObject obj, params string[] names)
		{
			if (obj == null || names == null)
			{
				return null;
			}
			for (int i = 0; i < names.Length; i++)
			{
				JToken token = obj[names[i]];
				if (token != null && token.Type != JTokenType.Null)
				{
					return token;
				}
			}
			return null;
		}

		private static string GetString(JObject obj, params string[] names)
		{
			return TokenToScalarString(GetToken(obj, names));
		}

		/// <summary>Renders any token as a scalar string; arrays yield their first non-empty element.</summary>
		private static string TokenToScalarString(JToken token)
		{
			if (token == null)
			{
				return "";
			}
			try
			{
				if (token.Type == JTokenType.Array)
				{
					JArray array = (JArray)token;
					for (int i = 0; i < array.Count; i++)
					{
						string s = TokenToScalarString(array[i]);
						if (!string.IsNullOrEmpty(s))
						{
							return s;
						}
					}
					return "";
				}
				if (token.Type == JTokenType.Object)
				{
					return "";
				}
				return TokenToString(token);
			}
			catch (Exception)
			{
				return "";
			}
		}

		private static string TokenToString(JToken token)
		{
			if (token == null || token.Type == JTokenType.Null)
			{
				return "";
			}
			try
			{
				switch (token.Type)
				{
				case JTokenType.String:
					return ((string)token).Trim();
				case JTokenType.Integer:
					return Convert.ToInt64(((JValue)token).Value, CultureInfo.InvariantCulture)
						.ToString(CultureInfo.InvariantCulture);
				case JTokenType.Float:
					return Convert.ToDouble(((JValue)token).Value, CultureInfo.InvariantCulture)
						.ToString("R", CultureInfo.InvariantCulture);
				case JTokenType.Boolean:
					return ((bool)token) ? "true" : "false";
				case JTokenType.Null:
				case JTokenType.Undefined:
					return "";
				default:
					return token.ToString(Formatting.None).Trim();
				}
			}
			catch (Exception)
			{
				return "";
			}
		}

		private static long GetLong(JObject obj, long fallback, params string[] names)
		{
			string raw = GetString(obj, names);
			long parsed;
			if (TryParseLong(raw, out parsed))
			{
				return parsed;
			}
			return fallback;
		}

		/// <summary>
		/// EasyTier reports the NAT type as a *string* in <c>peer list</c> but as an
		/// *integer* in <c>node info</c>'s <c>stun_info</c> while STUN has not resolved.
		/// Observed against v2.6.4: <c>node info</c> returns <c>udp_nat_type: 0</c> at the
		/// same moment <c>peer list</c> reports <c>"nat_type": "Unknown"</c>, so 0 is
		/// translated to "Unknown". Any other integer is kept verbatim as its numeric
		/// string rather than guessed at.
		/// </summary>
		private static string GetNatType(JObject obj, params string[] names)
		{
			JToken token = GetToken(obj, names);
			if (token == null)
			{
				return "";
			}
			if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
			{
				long code;
				if (TryParseLong(TokenToString(token), out code) && code == 0L)
				{
					return "Unknown";
				}
				return TokenToString(token);
			}
			return TokenToScalarString(token);
		}

		private static int ToInt(JToken token)
		{
			if (token == null)
			{
				return 0;
			}
			try
			{
				if (token.Type == JTokenType.Boolean)
				{
					return ((bool)token) ? 1 : 0;
				}
				string s = TokenToString(token);
				double d;
				if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
				{
					return (int)d;
				}
			}
			catch (Exception)
			{
			}
			return 0;
		}

		/// <summary>Accepts a number, a numeric string, or "-" (which means unknown).</summary>
		internal static bool IsUnknown(string raw)
		{
			if (string.IsNullOrEmpty(raw))
			{
				return true;
			}
			string t = raw.Trim();
			return t.Length == 0 || t == "-" || t == "--" || t == "N/A" || t == "n/a" ||
				t.Equals("unknown", StringComparison.OrdinalIgnoreCase) ||
				t.Equals("nan", StringComparison.OrdinalIgnoreCase);
		}

		private static float ParseLatencyMs(JObject row, params string[] names)
		{
			string raw = GetString(row, names);
			if (IsUnknown(raw))
			{
				return float.NaN;
			}
			string cleaned = raw.Trim();
			// Some builds append a unit: "12.5 ms" / "12.5ms".
			int unit = cleaned.IndexOf("ms", StringComparison.OrdinalIgnoreCase);
			if (unit > 0)
			{
				cleaned = cleaned.Substring(0, unit);
			}
			double value;
			if (!TryParseDouble(cleaned, out value))
			{
				return float.NaN;
			}
			if (value < 0d)
			{
				return float.NaN;
			}
			return (float)value;
		}

		/// <summary>
		/// Accepts "0.0312", "3.12%" and "3.12" (percent when greater than 1, because a
		/// loss *fraction* can never exceed 1).
		/// </summary>
		private static float ParseLossRate(JObject row, params string[] names)
		{
			string raw = GetString(row, names);
			if (IsUnknown(raw))
			{
				return float.NaN;
			}
			string cleaned = raw.Trim();
			bool percent = false;
			int pct = cleaned.IndexOf('%');
			if (pct >= 0)
			{
				percent = true;
				cleaned = cleaned.Substring(0, pct);
			}
			double value;
			if (!TryParseDouble(cleaned, out value))
			{
				return float.NaN;
			}
			if (value < 0d)
			{
				return float.NaN;
			}
			if (percent || value > 1d)
			{
				value = value / 100d;
			}
			if (value > 1d)
			{
				value = 1d;
			}
			return (float)value;
		}

		/// <summary>Accepts plain byte counts and human forms such as "1.5 MB" / "812 KiB".</summary>
		private static long ParseByteCount(JObject row, params string[] names)
		{
			string raw = GetString(row, names);
			if (IsUnknown(raw))
			{
				return -1L;
			}
			string cleaned = raw.Trim().Replace(" ", "");
			int i = 0;
			while (i < cleaned.Length && (char.IsDigit(cleaned[i]) || cleaned[i] == '.' || cleaned[i] == ',' ||
				cleaned[i] == '+' || cleaned[i] == '-' || cleaned[i] == 'e' || cleaned[i] == 'E'))
			{
				// Stop at an exponent that is not followed by a digit ("1e" would be a unit).
				if ((cleaned[i] == 'e' || cleaned[i] == 'E') &&
					(i + 1 >= cleaned.Length || !char.IsDigit(cleaned[i + 1])))
				{
					break;
				}
				i++;
			}
			string numberPart = cleaned.Substring(0, i).Replace(",", "");
			string unitPart = cleaned.Substring(i).Trim().ToUpperInvariant();

			double value;
			if (!TryParseDouble(numberPart, out value))
			{
				return -1L;
			}

			double multiplier;
			switch (unitPart)
			{
			case "":
			case "B":
				multiplier = 1d;
				break;
			case "K":
			case "KB":
				multiplier = 1000d;
				break;
			case "KIB":
				multiplier = 1024d;
				break;
			case "M":
			case "MB":
				multiplier = 1000d * 1000d;
				break;
			case "MIB":
				multiplier = 1024d * 1024d;
				break;
			case "G":
			case "GB":
				multiplier = 1000d * 1000d * 1000d;
				break;
			case "GIB":
				multiplier = 1024d * 1024d * 1024d;
				break;
			case "T":
			case "TB":
				multiplier = 1000d * 1000d * 1000d * 1000d;
				break;
			case "TIB":
				multiplier = 1024d * 1024d * 1024d * 1024d;
				break;
			default:
				return -1L;
			}

			double bytes = value * multiplier;
			if (bytes < 0d)
			{
				return -1L;
			}
			if (bytes > 9.2233720368547758E18d)
			{
				return long.MaxValue;
			}
			return (long)bytes;
		}

		private static bool TryParseLong(string raw, out long value)
		{
			value = 0L;
			if (IsUnknown(raw))
			{
				return false;
			}
			string cleaned = raw.Trim();
			if (long.TryParse(cleaned, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
			{
				return true;
			}
			double d;
			if (double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
			{
				value = (long)d;
				return true;
			}
			return false;
		}

		private static bool TryParseDouble(string raw, out double value)
		{
			value = 0d;
			if (string.IsNullOrEmpty(raw))
			{
				return false;
			}
			string cleaned = raw.Trim();
			if (double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
			{
				return true;
			}
			// Last resort: pull the leading numeric run out of something like "12.3ms".
			int end = 0;
			while (end < cleaned.Length &&
				(char.IsDigit(cleaned[end]) || cleaned[end] == '.' || cleaned[end] == '-' || cleaned[end] == '+'))
			{
				end++;
			}
			if (end == 0)
			{
				return false;
			}
			return double.TryParse(cleaned.Substring(0, end), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
		}
	}
}
