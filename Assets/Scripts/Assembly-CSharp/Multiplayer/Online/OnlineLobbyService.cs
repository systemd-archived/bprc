using System;
using System.Collections.Generic;
using BadPiggies.Multiplayer.Online.EasyTier;
using Mirror;
using UnityEngine;

namespace BadPiggies.Multiplayer.Online
{
	/// <summary>
	/// The single entry point the UI talks to. It owns the whole chain:
	///
	///   room code  ->  EasyTier virtual LAN  ->  Mirror session  ->  Multiplayer arena
	///
	/// and exposes one flat, observable model (phase, members, chat, room state) so the lobby
	/// screens never have to know which of those layers is currently busy.
	/// </summary>
	public sealed class OnlineLobbyService : MonoBehaviour
	{
		private const int MaxChatEntries = 128;
		private const float ConnectTimeoutSeconds = 25f;
		private const float MemberStatsIntervalSeconds = 2f;
		private const float HelloRetryIntervalSeconds = 1f;

		private static OnlineLobbyService s_instance;

		private readonly List<OnlineMemberView> m_members = new List<OnlineMemberView>();
		private readonly List<OnlineChatEntry> m_chat = new List<OnlineChatEntry>();
		private readonly List<MultiplayerLobbyPlayer> m_playerScratch = new List<MultiplayerLobbyPlayer>();
		private readonly Dictionary<uint, OnlineMemberStat> m_stats = new Dictionary<uint, OnlineMemberStat>();
		private readonly Dictionary<int, string> m_machineNamesByConnection = new Dictionary<int, string>();
		private readonly Dictionary<int, int> m_helloVersions = new Dictionary<int, int>();

		private OnlineSessionPhase m_phase = OnlineSessionPhase.Offline;
		private OnlineRole m_role = OnlineRole.None;
		private string m_status = "";
		private string m_lastError = "";
		private string m_roomCode = "";
		private string m_password = "";
		private OnlineRoomOptions m_options = new OnlineRoomOptions();
		private OnlineRoomInfo m_room = new OnlineRoomInfo();
		private OnlineRoomStateMessage m_roomState;

		private MultiplayerNetworkManager m_manager;
		private MultiplayerLobbyUI m_legacyLobbyUi;

		private bool m_clientHandlersRegistered;
		private bool m_serverHandlersRegistered;
		private bool m_helloSent;
		private float m_connectDeadline;
		private float m_nextHelloAttempt;
		private float m_nextMemberStats;
		private bool m_leaving;

		// ---------------------------------------------------------------- wiring

		public static OnlineLobbyService GetOrCreate()
		{
			if (s_instance != null)
			{
				return s_instance;
			}
			GameObject host = new GameObject("[Online Lobby]");
			UnityEngine.Object.DontDestroyOnLoad(host);
			s_instance = host.AddComponent<OnlineLobbyService>();
			return s_instance;
		}

		public static OnlineLobbyService Instance
		{
			get { return s_instance; }
		}

		private void Awake()
		{
			if (s_instance != null && s_instance != this)
			{
				UnityEngine.Object.Destroy(base.gameObject);
				return;
			}
			s_instance = this;
			OnlinePrefs.EnsureLoaded();
			m_options = OnlinePrefs.Data.LastHostOptions.Clone();
			m_options.Sanitize();
		}

		private void OnDestroy()
		{
			Unsubscribe();
			if (s_instance == this)
			{
				s_instance = null;
			}
		}

		private void OnApplicationQuit()
		{
			LeaveRoom();
		}

		private void Subscribe()
		{
			if (m_manager != null)
			{
				return;
			}
			m_manager = MultiplayerBootstrap.GetOrCreateSession();
			if (m_manager == null)
			{
				return;
			}
			m_manager.SessionChanged += OnMirrorSessionChanged;
			m_manager.SessionEnded += OnMirrorSessionEnded;
			m_legacyLobbyUi = m_manager.GetComponent<MultiplayerLobbyUI>();
			if (m_legacyLobbyUi != null)
			{
				// The room is driven by the new UI now; the old IMGUI panel must stay hidden.
				m_legacyLobbyUi.SetSuppressedByOnlineUi(true);
			}
		}

		private void Unsubscribe()
		{
			if (m_manager != null)
			{
				m_manager.SessionChanged -= OnMirrorSessionChanged;
				m_manager.SessionEnded -= OnMirrorSessionEnded;
				m_manager = null;
			}
		}

		// ---------------------------------------------------------------- observable state

		public OnlineSessionPhase Phase
		{
			get { return m_phase; }
		}

		public OnlineRole Role
		{
			get { return m_role; }
		}

		public string StatusText
		{
			get { return m_status; }
		}

		public string LastError
		{
			get { return m_lastError; }
		}

		public OnlineRoomOptions Options
		{
			get { return m_options; }
		}

		public OnlineRoomInfo Room
		{
			get { return m_room; }
		}

		public string RoomCode
		{
			get { return m_roomCode; }
		}

		public bool IsInRoom
		{
			get { return m_phase == OnlineSessionPhase.InRoom || m_phase == OnlineSessionPhase.InMatch || m_phase == OnlineSessionPhase.Connecting; }
		}

		public bool IsHost
		{
			get { return m_role == OnlineRole.Host; }
		}

		public int MemberCount
		{
			get { return m_members.Count; }
		}

		public int MaxPlayers
		{
			get { return m_roomState.maxPlayers <= 0 ? m_options.MaxPlayers : m_roomState.maxPlayers; }
		}

		public string Announcement
		{
			get { return m_roomState.announcement ?? ""; }
		}

		public List<OnlineMemberView> Members
		{
			get { return m_members; }
		}

		public List<OnlineChatEntry> Chat
		{
			get { return m_chat; }
		}

		public OnlineRoomStateMessage RoomState
		{
			get { return m_roomState; }
		}

		public bool IsEasyTierRunning
		{
			get
			{
				EasyTierRunner runner = EasyTierRunner.GetOrCreate();
				return runner != null && runner.State == EasyTierState.Running;
			}
		}

		public string EasyTierStatus
		{
			get
			{
				EasyTierRunner runner = EasyTierRunner.GetOrCreate();
				if (runner == null)
				{
					return "未初始化";
				}
				return runner.State.ToString() + (string.IsNullOrEmpty(runner.LastError) ? "" : "：" + runner.LastError);
			}
		}

		/// <summary>Raised whenever phase, status, room state or the member list changes.</summary>
		public event Action StateChanged;

		public event Action<OnlineChatEntry> ChatReceived;

		public event Action<string> Kicked;

		// ---------------------------------------------------------------- hosting

		public bool HostRoom(OnlineRoomOptions options, out string error)
		{
			error = null;
			if (IsInRoom)
			{
				error = "已经在一个房间里了，请先离开。";
				return false;
			}
			OnlineRoomOptions working = options == null ? m_options.Clone() : options.Clone();
			working.Sanitize();
			if (working.Transport == OnlineTransportKind.VirtualLan && !EasyTierPaths.IsSupportedPlatform)
			{
				error = EasyTierPaths.MissingReason;
				return false;
			}
			if (working.Transport == OnlineTransportKind.VirtualLan && !EasyTierPaths.IsInstalled)
			{
				error = "缺少 EasyTier 运行文件。" + EasyTierPaths.MissingReason;
				return false;
			}

			m_options = working;
			m_password = working.Password ?? "";
			m_roomCode = OnlineRoomCode.Generate();
			m_room = BuildRoomInfo(m_roomCode, working, true);
			m_role = OnlineRole.Host;
			m_chat.Clear();
			m_stats.Clear();
			m_machineNamesByConnection.Clear();
			m_helloVersions.Clear();
			m_roomState = new OnlineRoomStateMessage
			{
				roomCode = m_roomCode,
				roomName = working.RoomName,
				hostName = working.PlayerName,
				announcement = working.Announcement ?? "",
				maxPlayers = working.MaxPlayers,
				players = 0,
				inMatch = false,
				round = 0,
				gamePort = working.GamePort
			};

			OnlinePrefs.Data.LastHostOptions = working.Clone();
			OnlinePrefs.Data.LastInviteText = BuildInviteLink();
			OnlinePrefs.Save();
			OnlinePrefs.RememberRoom(m_room, false);

			Subscribe();
			if (m_manager == null)
			{
				error = "无法创建 Multiplayer 会话对象。";
				Fail(error);
				return false;
			}

			AddSystemChat("房间已创建，房间号 " + m_roomCode + "。");
			if (working.Transport == OnlineTransportKind.VirtualLan)
			{
				SetPhase(OnlineSessionPhase.PreparingNetwork, "正在启动 EasyTier 虚拟局域网…");
				if (!StartEasyTier(working, isHost: true, out error))
				{
					Fail(error);
					return false;
				}
			}
			else
			{
				SetPhase(OnlineSessionPhase.NetworkReady, "直连模式：正在监听端口 " + working.GamePort + "…");
				StartMirrorHost(working);
			}
			return true;
		}

		// ---------------------------------------------------------------- joining

		public bool JoinRoom(OnlineRoomInfo info, string password, out string error)
		{
			error = null;
			if (IsInRoom)
			{
				error = "已经在一个房间里了，请先离开。";
				return false;
			}
			if (info == null || string.IsNullOrEmpty(info.RoomCode))
			{
				error = "房间信息不完整。";
				return false;
			}
			if (info.ProtocolVersion != 0 && info.ProtocolVersion != OnlineNetConstants.ProtocolVersion)
			{
				error = "对方的联机协议版本是 " + info.ProtocolVersion + "，本机是 " + OnlineNetConstants.ProtocolVersion + "，请让双方使用同一个版本。";
				return false;
			}

			m_room = info.Clone();
			m_roomCode = OnlineRoomCode.Normalize(info.RoomCode);

			// No server is ever consulted to locate the host. If the invite did not carry a literal
			// address, try the rooms this machine already heard on its own network.
			if (!m_room.HasDirectAddress)
			{
				OnlineLanRoom discovered = OnlineLanDiscovery.FindRoom(m_roomCode);
				if (discovered != null && discovered.Info != null)
				{
					m_room.DirectAddress = discovered.Info.DirectAddress;
					m_room.ListenerPort = discovered.Info.ListenerPort;
					if (!string.IsNullOrEmpty(discovered.Info.RoomName))
					{
						m_room.RoomName = discovered.Info.RoomName;
					}
					if (!string.IsNullOrEmpty(discovered.Info.HostName))
					{
						m_room.HostName = discovered.Info.HostName;
					}
				}
			}
			if (m_room.Transport == OnlineTransportKind.Direct && !m_room.HasDirectAddress)
			{
				error = "没有找到房间 " + m_roomCode + " 的地址。请确认双方在同一个局域网内（或由房主手动告知地址），也可以在下方手动填写房主的 IP。";
				return false;
			}

			m_password = password ?? "";
			m_role = OnlineRole.Guest;
			m_options.PlayerName = OnlinePrefs.PlayerName;
			m_options.GamePort = info.GamePort;
			m_options.Transport = info.Transport;
			m_options.ExternalNode = info.ExternalNode ?? "";
			m_options.CustomPeer = info.CustomPeer;
			m_options.HostVirtualIpv4 = string.IsNullOrEmpty(info.HostVirtualIpv4) ? OnlineNetConstants.DefaultHostVirtualIpv4 : info.HostVirtualIpv4;
			m_chat.Clear();
			m_stats.Clear();
			m_roomState = new OnlineRoomStateMessage
			{
				roomCode = m_roomCode,
				roomName = info.RoomName,
				hostName = info.HostName,
				maxPlayers = OnlineNetConstants.MinPlayers,
				gamePort = info.GamePort
			};

			OnlinePrefs.Data.LastInviteText = OnlineInvite.Build(info);
			OnlinePrefs.Save();
			OnlinePrefs.RememberRoom(info, true);

			Subscribe();
			if (m_manager == null)
			{
				error = "无法创建 Multiplayer 会话对象。";
				Fail(error);
				return false;
			}

			AddSystemChat("正在加入房间 " + m_roomCode + "…");
			if (info.Transport == OnlineTransportKind.VirtualLan)
			{
				SetPhase(OnlineSessionPhase.PreparingNetwork, "正在启动 EasyTier 虚拟局域网…");
				if (!StartEasyTier(m_options, isHost: false, out error))
				{
					Fail(error);
					return false;
				}
			}
			else
			{
				SetPhase(OnlineSessionPhase.NetworkReady, "正在直连 " + info.ResolveConnectAddress() + "…");
				StartMirrorClient(info.ResolveConnectAddress(), info.GamePort);
			}
			return true;
		}

		/// <summary>Convenience wrapper for pasted invite text.</summary>
		public bool JoinFromText(string inviteText, string password, out string error)
		{
			OnlineRoomInfo info;
			if (!OnlineInvite.TryParse(inviteText, out info, out error))
			{
				return false;
			}
			return JoinRoom(info, password, out error);
		}

		// ---------------------------------------------------------------- leaving

		public void LeaveRoom()
		{
			if (m_phase == OnlineSessionPhase.Offline)
			{
				return;
			}
			m_leaving = true;
			try
			{
				SetPhase(OnlineSessionPhase.Closing, "正在离开房间…");
				OnlineLanDiscovery discovery = OnlineLanDiscovery.Instance;
				if (discovery != null)
				{
					discovery.StopBroadcast();
				}
				if (m_manager != null)
				{
					m_manager.LeaveSession();
				}
				EasyTierRunner runner = EasyTierRunner.GetOrCreate();
				if (runner != null)
				{
					runner.StopNetwork();
				}
				m_members.Clear();
				m_stats.Clear();
				m_machineNamesByConnection.Clear();
				m_helloVersions.Clear();
				m_helloSent = false;
				m_role = OnlineRole.None;
				m_roomCode = "";
				m_room = new OnlineRoomInfo();
				m_roomState = default(OnlineRoomStateMessage);
				m_lastError = "";
				SetPhase(OnlineSessionPhase.Offline, "");
			}
			finally
			{
				m_leaving = false;
			}
		}

		// ---------------------------------------------------------------- host controls

		public bool Kick(uint netId, string reason)
		{
			if (m_role != OnlineRole.Host || !NetworkServer.active)
			{
				return false;
			}
			NetworkIdentity identity;
			if (!NetworkServer.spawned.TryGetValue(netId, out identity) || identity == null)
			{
				return false;
			}
			NetworkConnectionToClient connection = identity.connectionToClient;
			if (connection == null)
			{
				return false;
			}
			string text = string.IsNullOrEmpty(reason) ? "你已被房主移出房间。" : reason;
			connection.Send(new OnlineKickMessage
			{
				reason = text
			});
			AddSystemChat((identity.GetComponent<MultiplayerLobbyPlayer>() == null ? "一名成员" : identity.GetComponent<MultiplayerLobbyPlayer>().DisplayName) + " 被移出房间。");
			ServerBroadcastChat(0u, "", text, isSystem: true);
			connection.Disconnect();
			return true;
		}

		public void SetAnnouncement(string text)
		{
			if (m_role != OnlineRole.Host)
			{
				return;
			}
			m_options.Announcement = text ?? "";
			if (m_options.Announcement.Length > 160)
			{
				m_options.Announcement = m_options.Announcement.Substring(0, 160);
			}
			m_roomState.announcement = m_options.Announcement;
			OnlinePrefs.Data.LastHostOptions = m_options.Clone();
			OnlinePrefs.Save();
			ServerBroadcastRoomState();
			RaiseStateChanged();
		}

		public void SetMaxPlayers(int maxPlayers)
		{
			if (m_role != OnlineRole.Host)
			{
				return;
			}
			m_options.MaxPlayers = Math.Max(OnlineNetConstants.MinPlayers, Math.Min(OnlineNetConstants.MaxRoomPlayers, maxPlayers));
			m_roomState.maxPlayers = m_options.MaxPlayers;
			if (m_manager != null)
			{
				m_manager.SetMaxPlayers(m_options.MaxPlayers);
			}
			OnlinePrefs.Data.LastHostOptions = m_options.Clone();
			OnlinePrefs.Save();
			ServerBroadcastRoomState();
			RaiseStateChanged();
		}

		// ---------------------------------------------------------------- chat

		/// <summary>
		/// Sends a room message. The bool result lets the UI keep unsent text while Mirror is still
		/// connecting; the old void entry point remains for callers that do not need that feedback.
		/// </summary>
		public void SendChat(string text)
		{
			TrySendChat(text);
		}

		public bool TrySendChat(string text)
		{
			if (string.IsNullOrWhiteSpace(text) || m_phase == OnlineSessionPhase.Offline)
			{
				return false;
			}
			string trimmed = text.Trim();
			if (trimmed.Length > 200)
			{
				trimmed = trimmed.Substring(0, 200);
			}
			long utcTicks = DateTime.UtcNow.Ticks;
			OnlineChatMessage message = new OnlineChatMessage
			{
				senderNetId = LocalNetId(),
				sender = LocalPlayerName(),
				text = trimmed,
				utcTicks = utcTicks,
				isSystem = false
			};
			if (NetworkServer.active)
			{
				// A host does not always receive its own SendToAll packet through the local client
				// loopback. Echo it locally and let AppendChat's stable-key guard remove a loopback
				// duplicate when Mirror does deliver one.
				AppendChat(new OnlineChatEntry
				{
					Sender = message.sender,
					Text = message.text,
					TimestampUtcTicks = utcTicks,
					IsSystem = false,
					IsLocal = true
				});
				ServerBroadcastChat(message.senderNetId, message.sender, message.text, isSystem: false, utcTicks: utcTicks);
				return true;
			}
			if (NetworkClient.active && NetworkClient.isConnected)
			{
				NetworkClient.Send(message);
				return true;
			}
			return false;
		}

		private void ServerBroadcastChat(uint senderNetId, string sender, string text, bool isSystem, long utcTicks = 0L)
		{
			OnlineChatMessage outgoing = new OnlineChatMessage
			{
				senderNetId = senderNetId,
				sender = sender,
				text = text,
				utcTicks = utcTicks <= 0L ? DateTime.UtcNow.Ticks : utcTicks,
				isSystem = isSystem
			};
			NetworkServer.SendToAll(outgoing);
		}

		private void AddSystemChat(string text)
		{
			AppendChat(new OnlineChatEntry
			{
				Sender = "",
				Text = text,
				TimestampUtcTicks = DateTime.UtcNow.Ticks,
				IsSystem = true,
				IsLocal = false
			});
		}

		private void AppendChat(OnlineChatEntry entry)
		{
			if (entry == null)
			{
				return;
			}
			// Host mode can see the same broadcast through both the server and local-client paths.
			// Treat the sender/timestamp/text tuple as the message identity so that path never doubles
			// a line in the conversation.
			for (int i = m_chat.Count - 1; i >= 0 && i >= m_chat.Count - 8; i--)
			{
				OnlineChatEntry previous = m_chat[i];
				if (previous != null && previous.TimestampUtcTicks == entry.TimestampUtcTicks &&
					previous.IsSystem == entry.IsSystem && string.Equals(previous.Sender, entry.Sender, StringComparison.Ordinal) &&
					string.Equals(previous.Text, entry.Text, StringComparison.Ordinal))
				{
					return;
				}
			}
			m_chat.Add(entry);
			while (m_chat.Count > MaxChatEntries)
			{
				m_chat.RemoveAt(0);
			}
			Action<OnlineChatEntry> handler = ChatReceived;
			if (handler != null)
			{
				handler(entry);
			}
		}

		// ---------------------------------------------------------------- invite

		public string BuildInviteLink()
		{
			if (string.IsNullOrEmpty(m_roomCode))
			{
				return "";
			}
			OnlineRoomInfo info = m_room == null || string.IsNullOrEmpty(m_room.RoomCode) ? BuildRoomInfo(m_roomCode, m_options, true) : m_room;
			return OnlineInvite.Build(info);
		}

		public void RememberCurrentInvite(string invite)
		{
			if (string.IsNullOrEmpty(invite))
			{
				return;
			}
			OnlinePrefs.Data.LastInviteText = invite;
			OnlinePrefs.Save();
		}

		// ---------------------------------------------------------------- internals

		private OnlineRoomInfo BuildRoomInfo(string roomCode, OnlineRoomOptions options, bool isHost)
		{
			// Advertise where this host can actually be reached. Carrying the literal address in
			// the invite/beacon is what removes any need for a server that knows where hosts are.
			string address = isHost ? OnlineLanDiscovery.GetPrimaryLanAddress() : "";
			return new OnlineRoomInfo
			{
				RoomCode = roomCode,
				RoomName = options.RoomName,
				HostName = options.PlayerName,
				GamePort = options.GamePort,
				Transport = options.Transport,
				ExternalNode = options.ExternalNode,
				CustomPeer = options.CustomPeer,
				HostVirtualIpv4 = options.HostVirtualIpv4,
				DirectAddress = address,
				ListenerPort = options.ListenerPort,
				PasswordRequired = !string.IsNullOrEmpty(options.Password),
				ProtocolVersion = OnlineNetConstants.ProtocolVersion
			};
		}

		private bool StartEasyTier(OnlineRoomOptions options, bool isHost, out string error)
		{
			error = null;
			EasyTierRunner runner = EasyTierRunner.GetOrCreate();
			if (runner == null)
			{
				error = "无法创建 EasyTier 运行器。";
				return false;
			}
			runner.StateChanged -= OnEasyTierStateChanged;
			runner.StateChanged += OnEasyTierStateChanged;

			EasyTierOptions etOptions = new EasyTierOptions
			{
				NetworkName = OnlineRoomCode.ToNetworkName(m_roomCode),
				NetworkSecret = OnlineRoomCode.ToNetworkSecret(m_roomCode, m_password),
				Hostname = SafeMachineName(),
				InstanceName = "bprc-" + OnlineRoomCode.Flatten(m_roomCode).ToLowerInvariant(),
				Ipv4 = isHost ? options.HostVirtualIpv4 : "",
				Dhcp = !isHost,
				ExternalNode = string.IsNullOrEmpty(options.ExternalNode) ? "" : options.ExternalNode,
				ListenerPort = options.ListenerPort,
				RpcPortalPort = options.RpcPortalPort,
				LatencyFirst = options.LatencyFirst,
				EnableUdpBroadcastRelay = isHost && options.RelayLanBroadcast,
				ConsoleLogLevel = "warn",
				NoTun = false
			};
			if (!string.IsNullOrEmpty(options.CustomPeer))
			{
				etOptions.Peers.Add(options.CustomPeer);
			}
			if (!isHost)
			{
				// Dial the host directly. Combined with an empty ExternalNode this forms the
				// network peer-to-peer, with no rendezvous or relay service involved.
				string hostPeer = m_room == null ? "" : m_room.ResolvePeerAddress();
				if (!string.IsNullOrEmpty(hostPeer) && !etOptions.Peers.Contains(hostPeer))
				{
					etOptions.Peers.Add(hostPeer);
				}
			}
			if (!runner.StartNetwork(etOptions, true, out error))
			{
				if (string.IsNullOrEmpty(error))
				{
					error = "EasyTier 启动失败：" + runner.LastError;
				}
				return false;
			}
			m_status = isHost ? "正在启动虚拟局域网（房主）…" : "正在启动虚拟局域网（加入方）…";
			RaiseStateChanged();
			return true;
		}

		private void OnEasyTierStateChanged(EasyTierState state)
		{
			if (m_phase == OnlineSessionPhase.Offline || m_phase == OnlineSessionPhase.Closing)
			{
				return;
			}
			if (state == EasyTierState.Running)
			{
				SetPhase(OnlineSessionPhase.NetworkReady, m_role == OnlineRole.Host ? "虚拟局域网已就绪，正在等待成员…" : "虚拟局域网已就绪，正在连接房主…");
				if (m_role == OnlineRole.Host)
				{
					StartMirrorHost(m_options);
				}
				else
				{
					StartMirrorClient(m_room.ResolveConnectAddress(), m_room.GamePort);
				}
			}
			else if (state == EasyTierState.Failed)
			{
				EasyTierRunner runner = EasyTierRunner.GetOrCreate();
				Fail("虚拟局域网启动失败：" + (runner == null ? "未知原因" : runner.LastError));
			}
		}

		private void StartMirrorHost(OnlineRoomOptions options)
		{
			if (m_manager == null)
			{
				Fail("Multiplayer 会话不可用。");
				return;
			}
			m_manager.SetMaxPlayers(options.MaxPlayers);
			string error;
			if (!m_manager.TryStartHost(options.GamePort, out error))
			{
				// Port already bound: fall back to any free port and tell the guests via the invite.
				Debug.LogWarning("[Online] Host on port " + options.GamePort + " failed (" + error + "), retrying on a random port.");
				ushort fallback = PickFallbackPort(options.GamePort);
				if (!m_manager.TryStartHost(fallback, out error))
				{
					Fail("无法在端口 " + options.GamePort + " 上开启房间：" + error);
					return;
				}
				m_options.GamePort = fallback;
				m_room.GamePort = fallback;
				m_roomState.gamePort = fallback;
				AddSystemChat("默认端口被占用，已改用端口 " + fallback + "。");
			}
			if (options.AllowLanDiscovery && OnlinePrefs.Data.EnableLanBroadcast)
			{
				OnlineLanDiscovery.GetOrCreate().StartBroadcast(m_room, 1, options.MaxPlayers);
			}
			SetPhase(OnlineSessionPhase.InRoom, "房间已开启，等待成员加入…");
		}

		private void StartMirrorClient(string address, ushort port)
		{
			if (m_manager == null)
			{
				Fail("Multiplayer 会话不可用。");
				return;
			}
			string error;
			if (!m_manager.TryStartClient(address, port, out error))
			{
				Fail("无法连接房主：" + error);
				return;
			}
			m_helloSent = false;
			m_connectDeadline = Time.realtimeSinceStartup + ConnectTimeoutSeconds;
			SetPhase(OnlineSessionPhase.Connecting, "正在连接房主 " + address + ":" + port + "…");
		}

		private static ushort PickFallbackPort(ushort preferred)
		{
			for (int candidate = preferred + 1; candidate < preferred + 40; candidate++)
			{
				if (candidate > 0 && candidate <= 65535)
				{
					return (ushort)candidate;
				}
			}
			return (ushort)(preferred == 7777 ? 7877 : 7777);
		}

		private void Update()
		{
			if (m_phase == OnlineSessionPhase.Offline)
			{
				return;
			}
			RegisterHandlers();
			SyncPhaseFromMirror();

			if (m_phase == OnlineSessionPhase.PreparingNetwork || m_phase == OnlineSessionPhase.NetworkReady || m_phase == OnlineSessionPhase.Connecting)
			{
				EasyTierRunner runner = EasyTierRunner.GetOrCreate();
				if (runner != null && runner.State == EasyTierState.Failed && !string.IsNullOrEmpty(runner.LastError))
				{
					Fail("虚拟局域网中断：" + runner.LastError);
					return;
				}
			}

			if (m_phase == OnlineSessionPhase.Connecting && Time.realtimeSinceStartup > m_connectDeadline)
			{
				Fail("连接超时。请确认房间号与密码一致，并且双方的 EasyTier 节点可达。");
				return;
			}

			if (m_phase == OnlineSessionPhase.InRoom || m_phase == OnlineSessionPhase.InMatch)
			{
				TrySendHello();
				RefreshMembers();
				if (NetworkServer.active)
				{
					HostTick();
				}
			}
		}

		private void SyncPhaseFromMirror()
		{
			if (m_manager == null || m_leaving)
			{
				return;
			}
			switch (m_manager.State)
			{
			case MultiplayerNetworkManager.SessionState.InLobby:
				if (m_phase != OnlineSessionPhase.InRoom)
				{
					SetPhase(OnlineSessionPhase.InRoom, m_role == OnlineRole.Host ? "成员已加入房间。" : "已进入房间。");
				}
				break;
			case MultiplayerNetworkManager.SessionState.LoadingArena:
				if (m_phase != OnlineSessionPhase.InMatch)
				{
					SetPhase(OnlineSessionPhase.InMatch, "对局进行中…");
				}
				break;
			case MultiplayerNetworkManager.SessionState.Idle:
				if (m_phase == OnlineSessionPhase.InMatch)
				{
					SetPhase(OnlineSessionPhase.InRoom, "回到房间。");
				}
				break;
			}
		}

		private void RegisterHandlers()
		{
			if (NetworkClient.active && !m_clientHandlersRegistered)
			{
				NetworkClient.RegisterHandler<OnlineRoomStateMessage>(OnClientRoomState);
				NetworkClient.RegisterHandler<OnlineChatMessage>(OnClientChat);
				NetworkClient.RegisterHandler<OnlineKickMessage>(OnClientKick);
				NetworkClient.RegisterHandler<OnlineMemberStatsMessage>(OnClientMemberStats);
				m_clientHandlersRegistered = true;
			}
			else if (!NetworkClient.active && m_clientHandlersRegistered)
			{
				m_clientHandlersRegistered = false;
			}

			if (NetworkServer.active && !m_serverHandlersRegistered)
			{
				NetworkServer.RegisterHandler<OnlineHelloMessage>(OnServerHello);
				NetworkServer.RegisterHandler<OnlineChatMessage>(OnServerChat);
				m_serverHandlersRegistered = true;
			}
			else if (!NetworkServer.active && m_serverHandlersRegistered)
			{
				m_serverHandlersRegistered = false;
			}
		}

		private void TrySendHello()
		{
			if (m_helloSent || !NetworkClient.active || !NetworkClient.isConnected)
			{
				return;
			}
			if (Time.realtimeSinceStartup < m_nextHelloAttempt)
			{
				return;
			}
			m_nextHelloAttempt = Time.realtimeSinceStartup + HelloRetryIntervalSeconds;
			NetworkClient.Send(new OnlineHelloMessage
			{
				displayName = LocalPlayerName(),
				machineName = SafeMachineName(),
				protocolVersion = OnlineNetConstants.ProtocolVersion
			});
			m_helloSent = true;
		}

		private string LocalPlayerName()
		{
			MultiplayerLobbyPlayer local = LocalLobbyPlayer();
			if (local != null && !string.IsNullOrWhiteSpace(local.DisplayName))
			{
				return local.DisplayName;
			}
			return string.IsNullOrWhiteSpace(OnlinePrefs.PlayerName) ? SafeMachineName() : OnlinePrefs.PlayerName;
		}

		private static uint LocalNetId()
		{
			MultiplayerLobbyPlayer local = LocalLobbyPlayer();
			if (local != null && local.netIdentity != null)
			{
				return local.netIdentity.netId;
			}
			return 0u;
		}

		private static MultiplayerLobbyPlayer LocalLobbyPlayer()
		{
			if (NetworkClient.localPlayer == null)
			{
				return null;
			}
			return NetworkClient.localPlayer.GetComponent<MultiplayerLobbyPlayer>();
		}

		private static string SafeMachineName()
		{
			string name = SystemInfo.deviceName;
			if (string.IsNullOrWhiteSpace(name))
			{
				name = Environment.MachineName;
			}
			return string.IsNullOrWhiteSpace(name) ? "PIGGIES-PC" : name;
		}

		// ---------------------------------------------------------------- server side

		private void HostTick()
		{
			if (Time.realtimeSinceStartup >= m_nextMemberStats)
			{
				m_nextMemberStats = Time.realtimeSinceStartup + MemberStatsIntervalSeconds;
				ServerBroadcastMemberStats();
			}
			OnlineLanDiscovery discovery = OnlineLanDiscovery.Instance;
			if (discovery != null && discovery.IsBroadcasting)
			{
				discovery.UpdateBroadcast(m_room, m_members.Count <= 0 ? 1 : m_members.Count, m_roomState.maxPlayers);
			}
		}

		private void ServerBroadcastMemberStats()
		{
			EasyTierMonitor monitor = EasyTierMonitor.GetOrCreate();
			List<EasyTierPeer> peers = monitor == null ? null : new List<EasyTierPeer>(monitor.Peers);

			List<OnlineMemberStat> stats = new List<OnlineMemberStat>();
			foreach (KeyValuePair<int, NetworkConnectionToClient> entry in NetworkServer.connections)
			{
				NetworkConnectionToClient connection = entry.Value;
				if (connection == null || connection.identity == null)
				{
					continue;
				}
				string machineName;
				if (!m_machineNamesByConnection.TryGetValue(connection.connectionId, out machineName))
				{
					machineName = "";
				}
				OnlineMemberStat stat = new OnlineMemberStat
				{
					netId = connection.identity.netId,
					machineName = machineName,
					latencyMs = float.NaN,
					lossRate = float.NaN,
					tunnelProtocol = "",
					natType = ""
				};
				EasyTierPeer peer = FindPeer(peers, machineName);
				if (peer != null)
				{
					stat.latencyMs = peer.LatencyMs;
					stat.lossRate = peer.LossRate;
					stat.tunnelProtocol = peer.TunnelProtocol;
					stat.natType = peer.NatType;
				}
				stats.Add(stat);
			}
			NetworkServer.SendToAll(new OnlineMemberStatsMessage
			{
				entries = stats.ToArray()
			});
		}

		private static EasyTierPeer FindPeer(List<EasyTierPeer> peers, string machineName)
		{
			if (peers == null || string.IsNullOrEmpty(machineName))
			{
				return null;
			}
			for (int i = 0; i < peers.Count; i++)
			{
				EasyTierPeer peer = peers[i];
				if (peer == null || peer.IsLocal || string.IsNullOrEmpty(peer.Hostname))
				{
					continue;
				}
				if (string.Equals(peer.Hostname, machineName, StringComparison.OrdinalIgnoreCase))
				{
					return peer;
				}
			}
			return null;
		}

		private void OnServerHello(NetworkConnectionToClient connection, OnlineHelloMessage message)
		{
			if (connection == null)
			{
				return;
			}
			if (message.protocolVersion != OnlineNetConstants.ProtocolVersion)
			{
				connection.Send(new OnlineKickMessage
				{
					reason = "协议版本不一致（对方 " + message.protocolVersion + "，本机 " + OnlineNetConstants.ProtocolVersion + "）。"
				});
				connection.Disconnect();
				return;
			}
			m_machineNamesByConnection[connection.connectionId] = message.machineName ?? "";
			m_helloVersions[connection.connectionId] = message.protocolVersion;
			ServerBroadcastRoomState();
			ServerBroadcastMemberStats();
			string name = message.displayName;
			if (string.IsNullOrWhiteSpace(name))
			{
				name = "一名玩家";
			}
			ServerBroadcastChat(connection.identity == null ? 0u : connection.identity.netId, "", name + " 加入了房间", isSystem: true);
		}

		private void OnServerChat(NetworkConnectionToClient connection, OnlineChatMessage message)
		{
			if (connection == null || connection.identity == null)
			{
				return;
			}
			MultiplayerLobbyPlayer player = connection.identity.GetComponent<MultiplayerLobbyPlayer>();
			string sender = player == null || string.IsNullOrWhiteSpace(player.DisplayName) ? "玩家" : player.DisplayName;
			string text = message.text ?? "";
			if (text.Length > 200)
			{
				text = text.Substring(0, 200);
			}
			if (string.IsNullOrWhiteSpace(text))
			{
				return;
			}
			ServerBroadcastChat(connection.identity.netId, sender, text, isSystem: false);
		}

		private void ServerBroadcastRoomState()
		{
			if (!NetworkServer.active)
			{
				return;
			}
			m_roomState.roomCode = m_roomCode;
			m_roomState.roomName = m_options.RoomName;
			m_roomState.hostName = m_options.PlayerName;
			m_roomState.maxPlayers = m_options.MaxPlayers;
			m_roomState.players = CountPlayers();
			m_roomState.inMatch = m_manager != null && m_manager.State == MultiplayerNetworkManager.SessionState.LoadingArena;
			m_roomState.gamePort = m_options.GamePort;
			NetworkServer.SendToAll(m_roomState);
		}

		private int CountPlayers()
		{
			int count = 0;
			CollectPlayers();
			for (int i = 0; i < m_playerScratch.Count; i++)
			{
				MultiplayerLobbyPlayer player = m_playerScratch[i];
				if (player != null && player.Slot >= 0)
				{
					count++;
				}
			}
			return count;
		}

		// ---------------------------------------------------------------- client side

		private void OnClientRoomState(OnlineRoomStateMessage message)
		{
			m_roomState = message;
			if (!string.IsNullOrEmpty(message.roomCode))
			{
				m_roomCode = OnlineRoomCode.Normalize(message.roomCode);
			}
			m_room.RoomName = message.roomName;
			m_room.HostName = message.hostName;
			m_room.GamePort = message.gamePort == 0 ? m_room.GamePort : message.gamePort;
			RaiseStateChanged();
		}

		private void OnClientChat(OnlineChatMessage message)
		{
			uint localNetId = LocalNetId();
			AppendChat(new OnlineChatEntry
			{
				Sender = message.isSystem ? "" : (message.sender ?? ""),
				Text = message.text ?? "",
				TimestampUtcTicks = message.utcTicks <= 0 ? DateTime.UtcNow.Ticks : message.utcTicks,
				IsSystem = message.isSystem,
				IsLocal = !message.isSystem && message.senderNetId != 0u && message.senderNetId == localNetId
			});
		}

		private void OnClientKick(OnlineKickMessage message)
		{
			string reason = string.IsNullOrEmpty(message.reason) ? "你已被移出房间。" : message.reason;
			Action<string> handler = Kicked;
			if (handler != null)
			{
				handler(reason);
			}
			LeaveRoom();
			m_lastError = reason;
			SetPhase(OnlineSessionPhase.Failed, reason);
		}

		private void OnClientMemberStats(OnlineMemberStatsMessage message)
		{
			m_stats.Clear();
			OnlineMemberStat[] entries = message.entries;
			if (entries == null)
			{
				return;
			}
			for (int i = 0; i < entries.Length; i++)
			{
				m_stats[entries[i].netId] = entries[i];
			}
			RefreshMembers();
		}

		// ---------------------------------------------------------------- member list

		public void RefreshMembers()
		{
			CollectPlayers();
			m_members.Clear();
			for (int i = 0; i < m_playerScratch.Count; i++)
			{
				MultiplayerLobbyPlayer player = m_playerScratch[i];
				if (player == null || player.netIdentity == null)
				{
					continue;
				}
				uint netId = player.netIdentity.netId;
				OnlineMemberView view = new OnlineMemberView
				{
					NetId = netId,
					ConnectionId = player.connectionToClient == null ? -1 : player.connectionToClient.connectionId,
					Slot = player.Slot,
					PlayerName = string.IsNullOrWhiteSpace(player.DisplayName) ? "(未命名)" : player.DisplayName,
					IsReady = player.IsReady,
					InArena = player.InArena,
					IsLocalPlayer = player.isLocalPlayer,
					IsHost = player.Slot == 0
				};
				OnlineMemberStat stat;
				if (m_stats.TryGetValue(netId, out stat))
				{
					view.MachineName = stat.machineName ?? "";
					view.LatencyMs = stat.latencyMs;
					view.LossRate = stat.lossRate;
					view.TunnelProtocol = stat.tunnelProtocol ?? "";
					view.NatType = stat.natType ?? "";
				}
				m_members.Add(view);
			}
			m_members.Sort((OnlineMemberView a, OnlineMemberView b) => a.Slot.CompareTo(b.Slot));
		}

		private void CollectPlayers()
		{
			MultiplayerLobbyPlayer.CollectInto(m_playerScratch);
		}

		// ---------------------------------------------------------------- events

		private void OnMirrorSessionChanged()
		{
			SyncPhaseFromMirror();
			RefreshMembers();
			RaiseStateChanged();
		}

		private void OnMirrorSessionEnded(string reason)
		{
			if (m_leaving || m_phase == OnlineSessionPhase.Offline || m_phase == OnlineSessionPhase.Failed)
			{
				return;
			}
			string text = string.IsNullOrEmpty(reason) ? "与对方断开连接。" : reason;
			EasyTierRunner runner = EasyTierRunner.GetOrCreate();
			if (runner != null && runner.State == EasyTierState.Running && m_phase != OnlineSessionPhase.InMatch)
			{
				// Virtual LAN is still up; only the Mirror session dropped. Surface it, but let
				// the player retry without tearing the whole network down.
				m_lastError = text;
				AddSystemChat(text);
				SetPhase(OnlineSessionPhase.InRoom, text);
				RaiseStateChanged();
				return;
			}
			if (m_phase == OnlineSessionPhase.InMatch)
			{
				AddSystemChat(text);
				SetPhase(OnlineSessionPhase.InRoom, text);
				RaiseStateChanged();
				return;
			}
			Fail(text);
		}

		private void SetPhase(OnlineSessionPhase phase, string status)
		{
			m_phase = phase;
			m_status = status ?? "";
			if (phase == OnlineSessionPhase.Failed)
			{
				m_lastError = status ?? "";
			}
			RaiseStateChanged();
		}

		private void Fail(string reason)
		{
			Debug.LogWarning("[Online] " + reason);
			m_role = OnlineRole.None;
			OnlineLanDiscovery discovery = OnlineLanDiscovery.Instance;
			if (discovery != null)
			{
				discovery.StopBroadcast();
			}
			EasyTierRunner runner = EasyTierRunner.GetOrCreate();
			if (runner != null)
			{
				runner.StopNetwork();
			}
			if (m_manager != null)
			{
				m_manager.LeaveSession();
			}
			m_members.Clear();
			SetPhase(OnlineSessionPhase.Failed, reason);
		}

		private void RaiseStateChanged()
		{
			Action handler = StateChanged;
			if (handler != null)
			{
				handler();
			}
		}
	}
}
