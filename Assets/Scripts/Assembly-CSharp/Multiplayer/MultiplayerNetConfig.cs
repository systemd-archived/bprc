using System;
using BadPiggies.Multiplayer.Online;

namespace BadPiggies.Multiplayer
{
/// <summary>
/// Tuning shared by the multiplayer session layer. This is a many-player system: every member of a
/// room drives a machine, and a match is an elimination round that ends when one machine is left.
/// </summary>
public static class MultiplayerNetConfig
{
	public const ushort DefaultPort = 7777;

	/// <summary>Fewest players a match can start with.</summary>
	public const int MinPlayers = OnlineNetConstants.MinPlayers;

	/// <summary>Most players one room can hold; slots are handed out in join order.</summary>
	public const int MaxPlayers = OnlineNetConstants.MaxRoomPlayers;

	public const string ArenaSandboxIdentifier = "S-VS";

	public const string MenuSceneName = "MainMenu";

	public const string ArenaSceneName = "LevelStub";

	public static bool TryParseEndpoint(string input, out string host, out ushort port, out string error)
	{
		host = null;
		port = 7777;
		error = null;
		if (string.IsNullOrWhiteSpace(input))
		{
			error = "Address is empty.";
			return false;
		}
		string text = input.Trim();
		if (text.StartsWith("[", StringComparison.Ordinal))
		{
			int num = text.IndexOf(']');
			if (num < 0)
			{
				error = "Malformed IPv6 address: missing ']'.";
				return false;
			}
			host = text.Substring(1, num - 1);
			string text2 = text.Substring(num + 1);
			if (text2.Length > 0)
			{
				if (text2[0] != ':')
				{
					error = "Malformed address after ']'.";
					return false;
				}
				if (!TryParsePort(text2.Substring(1), out port, out error))
				{
					return false;
				}
			}
			return ValidateHost(host, ref error);
		}
		int num2 = text.IndexOf(':');
		int num3 = text.LastIndexOf(':');
		if (num2 >= 0 && num2 != num3)
		{
			host = text;
			return ValidateHost(host, ref error);
		}
		if (num2 >= 0)
		{
			host = text.Substring(0, num2);
			if (!TryParsePort(text.Substring(num2 + 1), out port, out error))
			{
				return false;
			}
		}
		else
		{
			host = text;
		}
		return ValidateHost(host, ref error);
	}

	private static bool ValidateHost(string host, ref string error)
	{
		if (string.IsNullOrWhiteSpace(host))
		{
			error = "Host is empty.";
			return false;
		}
		return true;
	}

	private static bool TryParsePort(string text, out ushort port, out string error)
	{
		port = 7777;
		error = null;
		if (string.IsNullOrWhiteSpace(text))
		{
			error = "Port is empty.";
			return false;
		}
		if (!ushort.TryParse(text.Trim(), out var result) || result == 0)
		{
			error = "Port must be a number between 1 and 65535.";
			return false;
		}
		port = result;
		return true;
	}
}
}
