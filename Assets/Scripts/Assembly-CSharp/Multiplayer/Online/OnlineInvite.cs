using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BadPiggies.Multiplayer.Online
{
	/// <summary>
	/// The invite payload. One string carries everything a guest needs: the room code, the game
	/// port, the transport, and — crucially — the host's own literal address, so joining needs no
	/// server that knows where the host is. The same string is what the QR code encodes, so the
	/// text form and the QR form can never drift apart.
	/// </summary>
	public static class OnlineInvite
	{
		public static string Build(OnlineRoomInfo info)
		{
			if (info == null || string.IsNullOrEmpty(info.RoomCode))
			{
				return "";
			}
			StringBuilder builder = new StringBuilder();
			builder.Append(OnlineNetConstants.InviteScheme).Append("://join?");
			Append(builder, "c", OnlineRoomCode.Normalize(info.RoomCode));
			Append(builder, "p", info.GamePort.ToString(CultureInfo.InvariantCulture));
			Append(builder, "t", info.Transport == OnlineTransportKind.Direct ? "d" : "v");
			if (!string.IsNullOrEmpty(info.ExternalNode))
			{
				Append(builder, "e", info.ExternalNode);
			}
			if (!string.IsNullOrEmpty(info.CustomPeer))
			{
				Append(builder, "x", info.CustomPeer);
			}
			if (!string.IsNullOrEmpty(info.RoomName))
			{
				Append(builder, "n", info.RoomName);
			}
			if (!string.IsNullOrEmpty(info.HostName))
			{
				Append(builder, "h", info.HostName);
			}
			if (!string.IsNullOrEmpty(info.DirectAddress))
			{
				Append(builder, "a", info.DirectAddress);
			}
			if (info.ListenerPort != 0 && info.ListenerPort != OnlineNetConstants.DefaultListenerPort)
			{
				Append(builder, "l", info.ListenerPort.ToString(CultureInfo.InvariantCulture));
			}
			if (!string.IsNullOrEmpty(info.HostVirtualIpv4))
			{
				Append(builder, "i", info.HostVirtualIpv4);
			}
			if (info.PasswordRequired)
			{
				Append(builder, "pw", "1");
			}
			Append(builder, "v", info.ProtocolVersion.ToString(CultureInfo.InvariantCulture));
			return builder.ToString();
		}

		/// <summary>
		/// Parses an invite link, or a bare room code, or a longer message that merely contains
		/// one of those (chat text, a forum post, an invite pasted with commentary).
		/// </summary>
		public static bool TryParse(string text, out OnlineRoomInfo info, out string error)
		{
			info = null;
			error = null;
			if (string.IsNullOrWhiteSpace(text))
			{
				error = "邀请内容为空。";
				return false;
			}

			int start = text.IndexOf(OnlineNetConstants.InviteScheme + "://", StringComparison.OrdinalIgnoreCase);
			if (start >= 0)
			{
				int end = start;
				while (end < text.Length && !char.IsWhiteSpace(text[end]))
				{
					end++;
				}
				string link = text.Substring(start, end - start);
				int queryStart = link.IndexOf('?');
				Dictionary<string, string> query = ParseQuery(queryStart < 0 ? "" : link.Substring(queryStart + 1));
				string code = Get(query, "c");
				string normalized = OnlineRoomCode.Normalize(code);
				if (normalized.Length == 0)
				{
					error = "邀请链接里的房间号无效。";
					return false;
				}
				info = new OnlineRoomInfo
				{
					RoomCode = normalized,
					RoomName = Get(query, "n"),
					HostName = Get(query, "h"),
					GamePort = ParsePort(Get(query, "p"), OnlineNetConstants.DefaultGamePort),
					Transport = Get(query, "t") == "d" ? OnlineTransportKind.Direct : OnlineTransportKind.VirtualLan,
					HostVirtualIpv4 = Fallback(Get(query, "i"), OnlineNetConstants.DefaultHostVirtualIpv4),
					ListenerPort = ParsePort(Get(query, "l"), OnlineNetConstants.DefaultListenerPort),
					ExternalNode = Get(query, "e"),
					CustomPeer = Get(query, "x"),
					DirectAddress = Get(query, "a"),
					PasswordRequired = Get(query, "pw") == "1",
					ProtocolVersion = ParseInt(Get(query, "v"), 0)
				};
				return true;
			}

			string bare = OnlineRoomCode.Normalize(text);
			if (bare.Length > 0)
			{
				// A bare room code carries no address: the LAN browser fills it in, or the user
				// types the host address. No server is consulted either way.
				info = new OnlineRoomInfo
				{
					RoomCode = bare,
					GamePort = OnlineNetConstants.DefaultGamePort,
					Transport = OnlineTransportKind.Direct,
					HostVirtualIpv4 = OnlineNetConstants.DefaultHostVirtualIpv4,
					ExternalNode = "",
					ProtocolVersion = OnlineNetConstants.ProtocolVersion
				};
				return true;
			}

			error = "没找到房间号或邀请链接。";
			return false;
		}

		/// <summary>Scans arbitrary text for the first thing that looks like a room code.</summary>
		public static string ExtractCode(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return "";
			}
			string direct = OnlineRoomCode.Normalize(text);
			if (direct.Length > 0)
			{
				return direct;
			}
			StringBuilder token = new StringBuilder();
			for (int i = 0; i <= text.Length; i++)
			{
				char c = i < text.Length ? text[i] : ' ';
				if (char.IsLetterOrDigit(c) || c == '-')
				{
					token.Append(c);
					continue;
				}
				if (token.Length > 0)
				{
					string candidate = OnlineRoomCode.Normalize(token.ToString());
					if (candidate.Length > 0)
					{
						return candidate;
					}
					token.Length = 0;
				}
			}
			return "";
		}

		private static void Append(StringBuilder builder, string key, string value)
		{
			if (string.IsNullOrEmpty(value))
			{
				return;
			}
			if (builder[builder.Length - 1] != '?')
			{
				builder.Append('&');
			}
			builder.Append(key).Append('=').Append(Uri.EscapeDataString(value));
		}

		private static Dictionary<string, string> ParseQuery(string query)
		{
			Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			if (string.IsNullOrEmpty(query))
			{
				return result;
			}
			string[] pairs = query.Split('&');
			for (int i = 0; i < pairs.Length; i++)
			{
				if (pairs[i].Length == 0)
				{
					continue;
				}
				int equals = pairs[i].IndexOf('=');
				string key = equals < 0 ? pairs[i] : pairs[i].Substring(0, equals);
				string value = equals < 0 ? "" : pairs[i].Substring(equals + 1);
				if (key.Length == 0)
				{
					continue;
				}
				result[key] = Uri.UnescapeDataString(value.Replace("+", "%20"));
			}
			return result;
		}

		private static string Get(Dictionary<string, string> query, string key)
		{
			string value;
			return query.TryGetValue(key, out value) ? value : "";
		}

		private static string Fallback(string value, string fallback)
		{
			return string.IsNullOrWhiteSpace(value) ? fallback : value;
		}

		private static ushort ParsePort(string value, ushort fallback)
		{
			ushort parsed;
			if (ushort.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) && parsed != 0)
			{
				return parsed;
			}
			return fallback;
		}

		private static int ParseInt(string value, int fallback)
		{
			int parsed;
			if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
			{
				return parsed;
			}
			return fallback;
		}
	}
}
