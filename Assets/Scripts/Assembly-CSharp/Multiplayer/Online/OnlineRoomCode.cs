using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace BadPiggies.Multiplayer.Online
{
	/// <summary>
	/// Room codes. A code is both something a human can read out loud and the seed for the
	/// EasyTier network identity, so two players who type the same code end up on the same
	/// virtual LAN without any server in between.
	///
	/// The alphabet is Crockford base32 minus I/L/O/U: it survives being written down by hand,
	/// and <see cref="Normalize"/> folds the characters people confuse back onto the real ones.
	/// </summary>
	public static class OnlineRoomCode
	{
		private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

		private static readonly Dictionary<char, char> FoldMap = new Dictionary<char, char>
		{
			{ 'O', '0' },
			{ 'I', '1' },
			{ 'L', '1' },
			{ 'U', 'V' }
		};

		/// <summary>Creates a fresh code such as "BP-7K4M-2XQ9".</summary>
		public static string Generate()
		{
			byte[] bytes = new byte[OnlineNetConstants.RoomCodeRandomChars];
			using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
			{
				rng.GetBytes(bytes);
			}
			StringBuilder body = new StringBuilder(OnlineNetConstants.RoomCodeRandomChars);
			for (int i = 0; i < bytes.Length; i++)
			{
				body.Append(Alphabet[bytes[i] % Alphabet.Length]);
			}
			return Format(body.ToString());
		}

		/// <summary>Inserts the "BP-" prefix and the display dashes: "7K4M2XQ9" -> "BP-7K4M-2XQ9".</summary>
		public static string Format(string body)
		{
			if (string.IsNullOrEmpty(body))
			{
				return "";
			}
			string flat = Flatten(body);
			if (flat.Length != OnlineNetConstants.RoomCodeRandomChars)
			{
				return flat;
			}
			return OnlineNetConstants.RoomCodePrefix + "-" + flat.Substring(0, 4) + "-" + flat.Substring(4);
		}

		/// <summary>
		/// Accepts anything a user might paste — "bp-7k4m-2xq9", "7K4M 2XQ9", "BP7K4M2XQ9" — and
		/// returns the canonical "BP-7K4M-2XQ9", or an empty string when it is not a room code.
		/// </summary>
		public static string Normalize(string input)
		{
			string flat = Flatten(input);
			if (flat.Length != OnlineNetConstants.RoomCodeRandomChars)
			{
				return "";
			}
			for (int i = 0; i < flat.Length; i++)
			{
				if (Alphabet.IndexOf(flat[i]) < 0)
				{
					return "";
				}
			}
			return Format(flat);
		}

		public static bool IsValid(string input)
		{
			return Normalize(input).Length > 0;
		}

		/// <summary>Uppercases, strips separators and the optional "BP" prefix, folds look-alikes.</summary>
		public static string Flatten(string input)
		{
			if (string.IsNullOrEmpty(input))
			{
				return "";
			}
			StringBuilder builder = new StringBuilder(input.Length);
			for (int i = 0; i < input.Length; i++)
			{
				char c = char.ToUpperInvariant(input[i]);
				if (c == '-' || c == ' ' || c == '_' || c == '\t' || c == '\r' || c == '\n' || c == '.')
				{
					continue;
				}
				char folded;
				if (FoldMap.TryGetValue(c, out folded))
				{
					c = folded;
				}
				builder.Append(c);
			}
			string flat = builder.ToString();
			if (flat.Length == OnlineNetConstants.RoomCodeRandomChars + 2 && flat.StartsWith(OnlineNetConstants.RoomCodePrefix, StringComparison.Ordinal))
			{
				flat = flat.Substring(2);
			}
			return flat;
		}

		/// <summary>The EasyTier "network name" for a room. Same code on both sides = same LAN.</summary>
		public static string ToNetworkName(string roomCode)
		{
			string flat = Flatten(roomCode);
			return flat.Length == 0 ? "" : "bprc-" + flat.ToLowerInvariant();
		}

		/// <summary>
		/// The EasyTier "network secret". Derived from the code and the optional room password, so
		/// a password-protected room is a genuinely different network, not a cosmetic gate.
		/// </summary>
		public static string ToNetworkSecret(string roomCode, string password)
		{
			string flat = Flatten(roomCode);
			if (flat.Length == 0)
			{
				return "";
			}
			byte[] info = Encoding.UTF8.GetBytes("bprc-onlineroom-v1|" + flat + "|" + (password ?? ""));
			byte[] prk = Hmac(Encoding.UTF8.GetBytes("bprc-onlineroom-salt-v1"), info);
			byte[] okm = Hmac(prk, Encoding.UTF8.GetBytes("easytier-network-secret"));
			return ToBase32(okm, 16);
		}

		/// <summary>Grouping used in the UI: "BP-7K4M" / "2XQ9".</summary>
		public static string DescribeForSpeech(string roomCode)
		{
			string flat = Flatten(roomCode);
			if (flat.Length == 0)
			{
				return "";
			}
			StringBuilder builder = new StringBuilder();
			for (int i = 0; i < flat.Length; i++)
			{
				if (i > 0)
				{
					builder.Append(' ');
				}
				builder.Append(flat[i]);
			}
			return builder.ToString();
		}

		private static byte[] Hmac(byte[] key, byte[] data)
		{
			using (HMACSHA256 hmac = new HMACSHA256(key))
			{
				return hmac.ComputeHash(data);
			}
		}

		private static string ToBase32(byte[] data, int chars)
		{
			StringBuilder builder = new StringBuilder(chars);
			int bitBuffer = 0;
			int bitCount = 0;
			for (int i = 0; i < data.Length && builder.Length < chars; i++)
			{
				bitBuffer = (bitBuffer << 8) | data[i];
				bitCount += 8;
				while (bitCount >= 5 && builder.Length < chars)
				{
					bitCount -= 5;
					builder.Append(Alphabet[(bitBuffer >> bitCount) & 0x1F]);
				}
			}
			return builder.ToString();
		}
	}
}
