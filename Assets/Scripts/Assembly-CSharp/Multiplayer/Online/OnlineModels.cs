using System;
using System.Collections.Generic;

namespace BadPiggies.Multiplayer.Online
{
	/// <summary>Where the online session currently is. Drives the lobby UI.</summary>
	public enum OnlineSessionPhase
	{
		Offline,
		PreparingNetwork,
		NetworkReady,
		Connecting,
		InRoom,
		InMatch,
		Closing,
		Failed
	}

	/// <summary>What this peer does inside a room.</summary>
	public enum OnlineRole
	{
		None,
		Host,
		Guest,
		Spectator
	}

	/// <summary>
	/// How a room should be reached.
	///
	/// Both modes are designed to work with no server of any kind: the players' machines talk
	/// to each other directly. Nothing in the online layer requires a matchmaking service, a relay, a
	/// database or any third-party endpoint.
	/// </summary>
	public enum OnlineTransportKind
	{
		/// <summary>
		/// EasyTier peer-to-peer virtual LAN, used only when the guest already knows a literal
		/// address for the host (LAN address, or the host's own forwarded address). No rendezvous
		/// or relay service is used.
		/// </summary>
		VirtualLan,
		/// <summary>Plain direct IP / port: the simplest server-free path, and the default.</summary>
		Direct
	}

	/// <summary>
	/// Everything the host decides when a room is created. Serialised into PlayerPrefs by
	/// <see cref="OnlinePrefs"/> so the next host attempt starts from the same values.
	/// </summary>
	[Serializable]
	public class OnlineRoomOptions
	{
		public string RoomName = "";

		/// <summary>Player name shown in the member list and the chat.</summary>
		public string PlayerName = "";

		/// <summary>Optional room password. Empty means "anyone with the code may join".</summary>
		public string Password = "";

		/// <summary>Room capacity, <see cref="OnlineNetConstants.MinPlayers"/>..<see cref="OnlineNetConstants.MaxRoomPlayers"/>.</summary>
		public int MaxPlayers = 4;

		public ushort GamePort = OnlineNetConstants.DefaultGamePort;

		public OnlineTransportKind Transport = OnlineTransportKind.Direct;

		/// <summary>
		/// Optional EasyTier node to meet at, e.g. "tcp://my.server:11010". Empty by default and
		/// meant to be filled with something the user controls (a LAN address, a self-hosted node).
		/// No third-party public node is ever assumed.
		/// </summary>
		public string ExternalNode = OnlineNetConstants.DefaultExternalNode;

		/// <summary>Optional extra EasyTier peer, e.g. "udp://my.server:11010".</summary>
		public string CustomPeer = "";

		/// <summary>The virtual IPv4 the host claims inside the EasyTier network.</summary>
		public string HostVirtualIpv4 = OnlineNetConstants.DefaultHostVirtualIpv4;

		/// <summary>EasyTier listener port. Two rooms on one machine need different ports.</summary>
		public ushort ListenerPort = OnlineNetConstants.DefaultListenerPort;

		/// <summary>Loopback RPC portal port used by easytier-cli to query this instance.</summary>
		public ushort RpcPortalPort = OnlineNetConstants.DefaultRpcPortalPort;

		/// <summary>Relay LAN UDP broadcasts over the virtual LAN (Windows, needs administrator).</summary>
		public bool RelayLanBroadcast = true;

		public bool LatencyFirst = true;

		/// <summary>Broadcast a LAN beacon so peers on the same network need no room code.</summary>
		public bool AllowLanDiscovery = true;

		/// <summary>Rolling announcement shown at the top of the room for every member.</summary>
		public string Announcement = "";

		/// <summary>Announcement scroll speed multiplier used by the UI.</summary>
		public float AnnouncementSpeed = 1f;

		public OnlineRoomOptions Clone()
		{
			return new OnlineRoomOptions
			{
				RoomName = RoomName,
				PlayerName = PlayerName,
				Password = Password,
				MaxPlayers = MaxPlayers,
				GamePort = GamePort,
				Transport = Transport,
				ExternalNode = ExternalNode,
				CustomPeer = CustomPeer,
				HostVirtualIpv4 = HostVirtualIpv4,
				ListenerPort = ListenerPort,
				RpcPortalPort = RpcPortalPort,
				RelayLanBroadcast = RelayLanBroadcast,
				LatencyFirst = LatencyFirst,
				AllowLanDiscovery = AllowLanDiscovery,
				Announcement = Announcement,
				AnnouncementSpeed = AnnouncementSpeed
			};
		}

		public void Sanitize()
		{
			MaxPlayers = Math.Max(OnlineNetConstants.MinPlayers, Math.Min(OnlineNetConstants.MaxRoomPlayers, MaxPlayers));
			if (GamePort == 0)
			{
				GamePort = OnlineNetConstants.DefaultGamePort;
			}
			if (ListenerPort == 0)
			{
				ListenerPort = OnlineNetConstants.DefaultListenerPort;
			}
			if (RpcPortalPort == 0)
			{
				RpcPortalPort = OnlineNetConstants.DefaultRpcPortalPort;
			}
			if (string.IsNullOrWhiteSpace(HostVirtualIpv4))
			{
				HostVirtualIpv4 = OnlineNetConstants.DefaultHostVirtualIpv4;
			}
			if (string.IsNullOrWhiteSpace(PlayerName))
			{
				PlayerName = Environment.MachineName;
			}
			if (RoomName != null)
			{
				RoomName = RoomName.Trim();
			}
			if (RoomName != null && RoomName.Length > 24)
			{
				RoomName = RoomName.Substring(0, 24);
			}
			if (Announcement != null && Announcement.Length > 160)
			{
				Announcement = Announcement.Substring(0, 160);
			}
		}
	}

	/// <summary>
	/// The minimum a guest needs in order to reach a room. Produced by the host, carried by the
	/// invite link / QR code / LAN beacon.
	/// </summary>
	[Serializable]
	public class OnlineRoomInfo
	{
		public string RoomCode = "";
		public string RoomName = "";
		public string HostName = "";
		public ushort GamePort = OnlineNetConstants.DefaultGamePort;
		public OnlineTransportKind Transport = OnlineTransportKind.Direct;
		public string ExternalNode = "";
		public string CustomPeer = "";
		public string HostVirtualIpv4 = OnlineNetConstants.DefaultHostVirtualIpv4;
		/// <summary>
		/// The host's literal, reachable address: its LAN IPv4 when the players share a network,
		/// or whatever address the host publishes for itself. Carried in the invite and the beacon
		/// so a guest can reach the host without asking any server where it is.
		/// </summary>
		public string DirectAddress = "";
		/// <summary>The host's EasyTier listener port, so a guest can dial it as a peer directly.</summary>
		public ushort ListenerPort = OnlineNetConstants.DefaultListenerPort;
		public bool PasswordRequired;
		/// <summary>Protocol version of the host, so mismatched builds fail with a readable message.</summary>
		public int ProtocolVersion = OnlineNetConstants.ProtocolVersion;

		public OnlineRoomInfo Clone()
		{
			return new OnlineRoomInfo
			{
				RoomCode = RoomCode,
				RoomName = RoomName,
				HostName = HostName,
				GamePort = GamePort,
				Transport = Transport,
				ExternalNode = ExternalNode,
				CustomPeer = CustomPeer,
				HostVirtualIpv4 = HostVirtualIpv4,
				DirectAddress = DirectAddress,
				ListenerPort = ListenerPort,
				PasswordRequired = PasswordRequired,
				ProtocolVersion = ProtocolVersion
			};
		}

		/// <summary>The address Mirror's transport should dial once the network is up.</summary>
		public string ResolveConnectAddress()
		{
			if (Transport == OnlineTransportKind.Direct)
			{
				return string.IsNullOrWhiteSpace(DirectAddress) ? "127.0.0.1" : DirectAddress.Trim();
			}
			return string.IsNullOrWhiteSpace(HostVirtualIpv4) ? OnlineNetConstants.DefaultHostVirtualIpv4 : HostVirtualIpv4.Trim();
		}

		/// <summary>
		/// True when this room can be reached without help from any server: we know a literal
		/// address for the host.
		/// </summary>
		public bool HasDirectAddress
		{
			get { return !string.IsNullOrWhiteSpace(DirectAddress); }
		}

		/// <summary>
		/// The EasyTier peer string that connects straight to the host, e.g.
		/// "udp://192.168.1.5:11010". Empty when no literal host address is known, which is the
		/// only case that would need a rendezvous node.
		/// </summary>
		public string ResolvePeerAddress()
		{
			if (!HasDirectAddress)
			{
				return "";
			}
			ushort port = ListenerPort == 0 ? OnlineNetConstants.DefaultListenerPort : ListenerPort;
			return "udp://" + DirectAddress.Trim() + ":" + port;
		}
	}

	/// <summary>One line in the room chat.</summary>
	[Serializable]
	public class OnlineChatEntry
	{
		public string Sender = "";
		public string Text = "";
		public long TimestampUtcTicks;
		public bool IsSystem;
		public bool IsLocal;

		public DateTime LocalTime
		{
			get
			{
				if (TimestampUtcTicks <= 0L)
				{
					return DateTime.Now;
				}
				return new DateTime(TimestampUtcTicks, DateTimeKind.Utc).ToLocalTime();
			}
		}
	}

	/// <summary>A room the player hosted or joined before, kept for the "recent" / "favourite" lists.</summary>
	[Serializable]
	public class OnlineRoomRecord
	{
		public OnlineRoomInfo Info = new OnlineRoomInfo();
		public bool Favorite;
		public long LastSeenUtcTicks;
		public int TimesJoined;

		public string Key
		{
			get { return Info == null ? "" : Info.RoomCode; }
		}

		public string DisplayName
		{
			get
			{
				if (Info == null)
				{
					return "(unknown)";
				}
				if (!string.IsNullOrWhiteSpace(Info.RoomName))
				{
					return Info.RoomName;
				}
				return string.IsNullOrWhiteSpace(Info.RoomCode) ? "(unknown)" : Info.RoomCode;
			}
		}
	}

	/// <summary>A room seen on the local network by <see cref="OnlineLanDiscovery"/>.</summary>
	[Serializable]
	public class OnlineLanRoom
	{
		public OnlineRoomInfo Info = new OnlineRoomInfo();
		public string Address = "";
		public int MemberCount;
		public int MaxPlayers;
		public bool PasswordRequired;
		public float LastSeenRealtime;

		public float AgeSeconds
		{
			get { return UnityEngine.Time.realtimeSinceStartup - LastSeenRealtime; }
		}

		public string DisplayName
		{
			get
			{
				if (Info == null)
				{
					return "(unknown)";
				}
				if (!string.IsNullOrWhiteSpace(Info.RoomName))
				{
					return Info.RoomName;
				}
				return Info.RoomCode;
			}
		}
	}

	/// <summary>One row of the member list, rebuilt from Mirror state plus EasyTier telemetry.</summary>
	public sealed class OnlineMemberView
	{
		public uint NetId;
		public int ConnectionId = -1;
		public int Slot = -1;
		public string PlayerName = "";
		public string MachineName = "";
		public bool IsReady;
		public bool InArena;
		public bool IsLocalPlayer;
		public bool IsHost;
		public float LatencyMs = float.NaN;
		public float LossRate = float.NaN;
		public string TunnelProtocol = "";
		public string NatType = "";

		/// <summary>True once the host has handed this member a slot — that is, they play.</summary>
		public bool IsFighter
		{
			get { return Slot >= 0; }
		}

		public string RoleLabel
		{
			get { return IsHost ? "房主" : "玩家"; }
		}
	}

	/// <summary>Tuning constants shared by the whole online layer.</summary>
	public static class OnlineNetConstants
	{
		/// <summary>Bumped whenever the wire protocol or invite format changes incompatibly.</summary>
		public const int ProtocolVersion = 2;

		public const ushort DefaultGamePort = 7777;

		/// <summary>Fewest members a match can start with.</summary>
		public const int MinPlayers = 2;

		/// <summary>Most members one room can hold. Every member drives a machine.</summary>
		public const int MaxRoomPlayers = 8;

		public const int LanBeaconPort = 47771;

		public const string LanBeaconMagic = "bprc-lan/1";

		public const string InviteScheme = "bprc";

		/// <summary>
		/// Deliberately empty: the online layer must work with no server of any kind, so no public
		/// rendezvous node is assumed. Users who want EasyTier across networks put their own node
		/// (or the host's literal address) here; guests normally reach the host directly instead.
		/// </summary>
		public const string DefaultExternalNode = "";

		public const string DefaultHostVirtualIpv4 = "10.126.126.1";

		public const ushort DefaultListenerPort = 11010;

		public const ushort DefaultRpcPortalPort = 15888;

		public const string PrefsFileName = "online.json";

		public const string RoomCodePrefix = "BP";

		public const int RoomCodeRandomChars = 8;
	}
}
