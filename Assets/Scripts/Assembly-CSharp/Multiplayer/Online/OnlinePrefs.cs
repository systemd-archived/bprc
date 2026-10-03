using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace BadPiggies.Multiplayer.Online
{
	/// <summary>Everything persisted between sessions by the online layer.</summary>
	[Serializable]
	public class OnlinePrefsData
	{
		public string PlayerName = "";

		public OnlineRoomOptions LastHostOptions = new OnlineRoomOptions();

		/// <summary>EasyTier nodes the player added by hand.</summary>
		public List<string> CustomNodes = new List<string>();

		/// <summary>Rooms hosted or joined before, newest first. Favourites are flagged in place.</summary>
		public List<OnlineRoomRecord> RoomHistory = new List<OnlineRoomRecord>();

		public bool ShowLanRooms = true;

		public bool EnableLanBroadcast = true;

		/// <summary>
		/// Opt-in EasyTier virtual LAN. Off by default because the online layer must work with no
		/// server of any kind: direct/LAN play never needs this.
		/// </summary>
		public bool EnableVirtualLan = false;

		/// <summary>Last invite the player copied, offered as a "paste" shortcut next session.</summary>
		public string LastInviteText = "";
	}

	/// <summary>
	/// Small JSON store next to the player's other data. Deliberately forgiving: a corrupt or
	/// half-written file resets to defaults instead of breaking the lobby.
	/// </summary>
	public static class OnlinePrefs
	{
		private const int MaxHistory = 20;

		private static OnlinePrefsData s_data;
		private static bool s_loaded;

		public static OnlinePrefsData Data
		{
			get
			{
				EnsureLoaded();
				return s_data;
			}
		}

		public static string FilePath
		{
			get { return Path.Combine(Application.persistentDataPath, OnlineNetConstants.PrefsFileName); }
		}

		public static void EnsureLoaded()
		{
			if (s_loaded)
			{
				return;
			}
			s_loaded = true;
			s_data = ReadFromDisk();
			if (string.IsNullOrWhiteSpace(s_data.PlayerName))
			{
				s_data.PlayerName = DefaultPlayerName();
			}
			if (s_data.LastHostOptions == null)
			{
				s_data.LastHostOptions = new OnlineRoomOptions();
			}
			if (s_data.CustomNodes == null)
			{
				s_data.CustomNodes = new List<string>();
			}
			if (s_data.RoomHistory == null)
			{
				s_data.RoomHistory = new List<OnlineRoomRecord>();
			}
			s_data.LastHostOptions.PlayerName = s_data.PlayerName;
		}

		public static void Save()
		{
			EnsureLoaded();
			try
			{
				string json = JsonConvert.SerializeObject(s_data, Formatting.Indented);
				File.WriteAllText(FilePath, json);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[Online] Could not write " + FilePath + ": " + ex.Message);
			}
		}

		public static string PlayerName
		{
			get { return Data.PlayerName; }
			set
			{
				EnsureLoaded();
				s_data.PlayerName = string.IsNullOrWhiteSpace(value) ? DefaultPlayerName() : value.Trim();
				s_data.LastHostOptions.PlayerName = s_data.PlayerName;
			}
		}

		/// <summary>
		/// Whether new rooms use the EasyTier virtual LAN. Defaults to false: direct/LAN play is
		/// the server-free path and needs nothing installed.
		/// </summary>
		public static bool EnableVirtualLan
		{
			get { return Data.EnableVirtualLan; }
			set
			{
				EnsureLoaded();
				s_data.EnableVirtualLan = value;
			}
		}

		/// <summary>Records a room in the history, de-duplicated by room code.</summary>
		public static OnlineRoomRecord RememberRoom(OnlineRoomInfo info, bool joined)
		{
			EnsureLoaded();
			if (info == null || string.IsNullOrEmpty(info.RoomCode))
			{
				return null;
			}
			string key = info.RoomCode;
			OnlineRoomRecord record = null;
			for (int i = 0; i < s_data.RoomHistory.Count; i++)
			{
				if (s_data.RoomHistory[i] != null && s_data.RoomHistory[i].Key == key)
				{
					record = s_data.RoomHistory[i];
					s_data.RoomHistory.RemoveAt(i);
					break;
				}
			}
			if (record == null)
			{
				record = new OnlineRoomRecord();
			}
			record.Info = info.Clone();
			record.LastSeenUtcTicks = DateTime.UtcNow.Ticks;
			if (joined)
			{
				record.TimesJoined++;
			}
			s_data.RoomHistory.Insert(0, record);
			while (s_data.RoomHistory.Count > MaxHistory)
			{
				int last = s_data.RoomHistory.Count - 1;
				if (s_data.RoomHistory[last].Favorite && last > 0)
				{
					// Never drop a favourite; drop the one before it instead.
					s_data.RoomHistory.RemoveAt(last - 1);
				}
				else
				{
					s_data.RoomHistory.RemoveAt(last);
				}
			}
			Save();
			return record;
		}

		public static bool ToggleFavorite(string roomCode)
		{
			EnsureLoaded();
			OnlineRoomRecord record = Find(roomCode);
			if (record == null)
			{
				return false;
			}
			record.Favorite = !record.Favorite;
			Save();
			return record.Favorite;
		}

		public static OnlineRoomRecord Find(string roomCode)
		{
			EnsureLoaded();
			string key = OnlineRoomCode.Normalize(roomCode);
			if (key.Length == 0)
			{
				key = roomCode;
			}
			for (int i = 0; i < s_data.RoomHistory.Count; i++)
			{
				if (s_data.RoomHistory[i] != null && s_data.RoomHistory[i].Key == key)
				{
					return s_data.RoomHistory[i];
				}
			}
			return null;
		}

		public static void ForgetRoom(string roomCode)
		{
			EnsureLoaded();
			OnlineRoomRecord record = Find(roomCode);
			if (record != null)
			{
				s_data.RoomHistory.Remove(record);
				Save();
			}
		}

		public static List<OnlineRoomRecord> Favorites()
		{
			EnsureLoaded();
			List<OnlineRoomRecord> result = new List<OnlineRoomRecord>();
			for (int i = 0; i < s_data.RoomHistory.Count; i++)
			{
				OnlineRoomRecord record = s_data.RoomHistory[i];
				if (record != null && record.Favorite)
				{
					result.Add(record);
				}
			}
			return result;
		}

		public static void AddCustomNode(string node)
		{
			EnsureLoaded();
			if (string.IsNullOrWhiteSpace(node))
			{
				return;
			}
			string trimmed = node.Trim();
			for (int i = 0; i < s_data.CustomNodes.Count; i++)
			{
				if (string.Equals(s_data.CustomNodes[i], trimmed, StringComparison.OrdinalIgnoreCase))
				{
					return;
				}
			}
			s_data.CustomNodes.Insert(0, trimmed);
			while (s_data.CustomNodes.Count > 12)
			{
				s_data.CustomNodes.RemoveAt(s_data.CustomNodes.Count - 1);
			}
			Save();
		}

		public static void RemoveCustomNode(string node)
		{
			EnsureLoaded();
			for (int i = 0; i < s_data.CustomNodes.Count; i++)
			{
				if (string.Equals(s_data.CustomNodes[i], node, StringComparison.OrdinalIgnoreCase))
				{
					s_data.CustomNodes.RemoveAt(i);
					Save();
					return;
				}
			}
		}

		/// <summary>
		/// Every node worth offering in the UI. There is intentionally no built-in entry: the
		/// online layer must work with no server at all, so only nodes the user entered themselves
		/// ever appear here.
		/// </summary>
		public static List<string> NodeChoices()
		{
			EnsureLoaded();
			List<string> result = new List<string>();
			if (!string.IsNullOrWhiteSpace(OnlineNetConstants.DefaultExternalNode))
			{
				result.Add(OnlineNetConstants.DefaultExternalNode);
			}
			for (int i = 0; i < s_data.CustomNodes.Count; i++)
			{
				string node = s_data.CustomNodes[i];
				if (!string.IsNullOrWhiteSpace(node) && !result.Contains(node))
				{
					result.Add(node);
				}
			}
			return result;
		}

		private static string DefaultPlayerName()
		{
			string machine = "";
			try
			{
				machine = Environment.MachineName;
			}
			catch (Exception)
			{
				machine = "";
			}
			if (string.IsNullOrWhiteSpace(machine))
			{
				machine = "Player";
			}
			if (machine.Length > 14)
			{
				machine = machine.Substring(0, 14);
			}
			return machine;
		}

		private static OnlinePrefsData ReadFromDisk()
		{
			try
			{
				if (File.Exists(FilePath))
				{
					string json = File.ReadAllText(FilePath);
					OnlinePrefsData parsed = JsonConvert.DeserializeObject<OnlinePrefsData>(json);
					if (parsed != null)
					{
						return parsed;
					}
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[Online] Could not read " + FilePath + ", starting fresh: " + ex.Message);
			}
			return new OnlinePrefsData();
		}
	}
}
