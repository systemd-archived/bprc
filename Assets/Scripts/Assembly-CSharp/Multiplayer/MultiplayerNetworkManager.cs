using System;
using System.Collections.Generic;
using BadPiggies.Multiplayer.Online;
using Mirror;
using UnityEngine;

namespace BadPiggies.Multiplayer
{
/// <summary>
/// Mirror network manager for a multiplayer room.
///
/// A room holds <see cref="OnlineNetConstants.MinPlayers"/>..<see cref="OnlineNetConstants.MaxRoomPlayers"/>
/// members and <em>every</em> member drives a machine — there is no built-in "two fighters plus
/// spectators" split any more. Slots are handed out in join order (the host is always slot 0) and
/// the match starts once every connected member is ready.
/// </summary>
public class MultiplayerNetworkManager : Mirror.NetworkManager
{
	public enum SessionState
	{
		Idle,
		Hosting,
		Connecting,
		InLobby,
		LoadingArena
	}

	private SessionState m_state;

	private string m_lastError;

	private bool m_leaving;

	/// <summary>Room capacity, <see cref="OnlineNetConstants.MinPlayers"/>..<see cref="OnlineNetConstants.MaxRoomPlayers"/>.</summary>
	private int m_maxPlayers = OnlineNetConstants.MaxRoomPlayers;

	public SessionState State => m_state;

	public string LastError => m_lastError;

	/// <summary>Room capacity: how many members may be connected at once.</summary>
	public int MaxPlayers => m_maxPlayers;

	/// <summary>Fewest members a match needs.</summary>
	public int MinPlayers => OnlineNetConstants.MinPlayers;

	public event Action SessionChanged;

	public event Action<string> SessionEnded;

	public override void Awake()
	{
		base.Awake();
		maxConnections = m_maxPlayers;
		autoCreatePlayer = true;
		offlineScene = string.Empty;
		onlineScene = string.Empty;
	}

	/// <summary>
	/// Sets the room capacity. Must be called before <see cref="TryStartHost"/>: Mirror only reads
	/// <c>maxConnections</c> when the server starts listening.
	/// </summary>
	public void SetMaxPlayers(int maxPlayers)
	{
		m_maxPlayers = Mathf.Clamp(maxPlayers, OnlineNetConstants.MinPlayers, OnlineNetConstants.MaxRoomPlayers);
		maxConnections = m_maxPlayers;
	}

	public bool TryStartHost(ushort port, out string error)
	{
		error = null;
		if (m_state != SessionState.Idle)
		{
			error = "A session is already active.";
			return false;
		}
		if (!TryApplyPort(port, out error))
		{
			return false;
		}
		m_lastError = null;
		SetState(SessionState.Hosting);
		StartHost();
		return true;
	}

	public bool TryStartClient(string address, ushort port, out string error)
	{
		error = null;
		if (m_state != SessionState.Idle)
		{
			error = "A session is already active.";
			return false;
		}
		if (string.IsNullOrWhiteSpace(address))
		{
			error = "Address is empty.";
			return false;
		}
		if (!TryApplyPort(port, out error))
		{
			return false;
		}
		m_lastError = null;
		networkAddress = address;
		SetState(SessionState.Connecting);
		StartClient();
		return true;
	}

	public void LeaveSession()
	{
		MultiplayerMatchController component = GetComponent<MultiplayerMatchController>();
		if (component != null)
		{
			component.ResetForNewMatch();
			component.ResetForNewSession();
		}
		m_leaving = true;
		try
		{
			if (NetworkServer.active && NetworkClient.isConnected)
			{
				StopHost();
			}
			else if (NetworkServer.active)
			{
				StopServer();
			}
			else if (NetworkClient.isConnected || NetworkClient.active)
			{
				StopClient();
			}
			SetState(SessionState.Idle);
		}
		finally
		{
			m_leaving = false;
		}
	}

	private bool TryApplyPort(ushort port, out string error)
	{
		error = null;
		if (port == 0)
		{
			error = "Port must be between 1 and 65535.";
			return false;
		}
		if (transport == null)
		{
			error = "No transport is configured on the network manager.";
			return false;
		}
		if (transport is PortTransport portTransport)
		{
			portTransport.Port = port;
			return true;
		}
		error = "Configured transport does not support a port (expected KCP).";
		return false;
	}

	public override void OnStartHost()
	{
		base.OnStartHost();
		SetState(SessionState.Hosting);
	}

	public override void OnServerConnect(NetworkConnectionToClient conn)
	{
		base.OnServerConnect(conn);
		if (NetworkServer.connections.Count > m_maxPlayers)
		{
			Debug.Log($"[Multiplayer] Refusing connection {conn?.connectionId}: the room is full " +
				$"({NetworkServer.connections.Count - 1}/{m_maxPlayers}).");
			conn.Disconnect();
		}
	}

	public override void OnServerAddPlayer(NetworkConnectionToClient conn)
	{
		if (playerPrefab == null)
		{
			Debug.LogError("[Multiplayer] playerPrefab is not assigned on MultiplayerNetworkManager.");
			return;
		}
		GameObject gameObject = UnityEngine.Object.Instantiate(playerPrefab);
		gameObject.name = $"MultiplayerLobbyPlayer [conn {conn.connectionId}]";
		MultiplayerLobbyPlayer component = gameObject.GetComponent<MultiplayerLobbyPlayer>();
		if (component != null)
		{
			component.ServerInitialize(NextFreeSlot());
		}
		NetworkServer.AddPlayerForConnection(conn, gameObject);
		SetState(SessionState.InLobby);
	}

	/// <summary>
	/// Slots are handed out in join order: the host is always 0, then 1, 2, … Each slot owns one
	/// machine in the arena.
	/// </summary>
	private int NextFreeSlot()
	{
		int slot = 0;
		foreach (NetworkConnectionToClient value in NetworkServer.connections.Values)
		{
			if (value == null || value.identity == null)
			{
				continue;
			}
			MultiplayerLobbyPlayer component = value.identity.GetComponent<MultiplayerLobbyPlayer>();
			if (component != null && component.Slot >= slot)
			{
				slot = component.Slot + 1;
			}
		}
		if (slot >= m_maxPlayers)
		{
			// Should be unreachable: maxConnections refuses extra peers. Pick the lowest free slot
			// so a stale slot number can never create a machine outside the arena arrays.
			for (int candidate = 0; candidate < m_maxPlayers; candidate++)
			{
				bool taken = false;
				foreach (NetworkConnectionToClient value in NetworkServer.connections.Values)
				{
					if (value == null || value.identity == null)
					{
						continue;
					}
					MultiplayerLobbyPlayer component = value.identity.GetComponent<MultiplayerLobbyPlayer>();
					if (component != null && component.Slot == candidate)
					{
						taken = true;
						break;
					}
				}
				if (!taken)
				{
					slot = candidate;
					break;
				}
			}
			Debug.LogWarning($"[Multiplayer] The room handed out slot {slot}, which is outside " +
				$"0..{m_maxPlayers - 1}.");
		}
		return slot;
	}

	public override void OnServerDisconnect(NetworkConnectionToClient conn)
	{
		int slot = -1;
		bool wasHostConnection = conn != null && conn == NetworkServer.localConnection;
		if (conn != null && conn.identity != null)
		{
			MultiplayerLobbyPlayer leaving = conn.identity.GetComponent<MultiplayerLobbyPlayer>();
			if (leaving != null)
			{
				slot = leaving.Slot;
			}
		}
		bool wasInMatch = m_state == SessionState.LoadingArena;
		base.OnServerDisconnect(conn);
		ServerClearReadyFlags();

		if (wasHostConnection)
		{
			// The host owns the arena simulation; without it there is nothing to replicate.
			if (!m_leaving)
			{
				SessionEnded?.Invoke("房主已离开房间。");
			}
			SetState(NetworkServer.active ? SessionState.InLobby : SessionState.Idle);
			return;
		}

		if (wasInMatch && slot >= 0)
		{
			// A player dropped mid-round: their machine forfeits and the round carries on for
			// everybody else. If too few players are left the match controller ends the round.
			MultiplayerMatchController match = GetComponent<MultiplayerMatchController>();
			if (match != null)
			{
				match.ServerPlayerLeft(slot);
			}
			if (PlayerCount() < OnlineNetConstants.MinPlayers)
			{
				m_leaving = true;
				try
				{
					LeaveSession();
				}
				finally
				{
					m_leaving = false;
				}
			}
			return;
		}

		SetState(NetworkServer.active ? SessionState.InLobby : SessionState.Idle);
	}

	public override void OnStopServer()
	{
		base.OnStopServer();
		SetState(SessionState.Idle);
	}

	public override void OnClientConnect()
	{
		base.OnClientConnect();
		SetState(SessionState.InLobby);
	}

	public override void OnClientDisconnect()
	{
		base.OnClientDisconnect();
		bool flag = m_state == SessionState.Connecting;
		SetState(SessionState.Idle);
		if (!NetworkServer.active && !m_leaving)
		{
			SessionEnded?.Invoke(flag ? (m_lastError ?? "Could not connect to host.") : "Disconnected from host.");
		}
	}

	public override void OnClientError(TransportError error, string reason)
	{
		base.OnClientError(error, reason);
		m_lastError = (string.IsNullOrEmpty(reason) ? error.ToString() : reason);
	}

	public override void OnStopClient()
	{
		base.OnStopClient();
		if (!NetworkServer.active)
		{
			SetState(SessionState.Idle);
		}
	}

	/// <summary>Members currently connected, host included.</summary>
	public static int PlayerCount()
	{
		if (!NetworkServer.active)
		{
			return 0;
		}
		int count = 0;
		foreach (NetworkConnectionToClient value in NetworkServer.connections.Values)
		{
			if (value != null && value.identity != null && value.identity.GetComponent<MultiplayerLobbyPlayer>() != null)
			{
				count++;
			}
		}
		return count;
	}

	/// <summary>
	/// True once every connected member is ready and at least
	/// <see cref="OnlineNetConstants.MinPlayers"/> are in the room.
	/// </summary>
	public bool ServerAllPlayersReady()
	{
		if (!NetworkServer.active)
		{
			return false;
		}
		int players = 0;
		int ready = 0;
		foreach (NetworkConnectionToClient value in NetworkServer.connections.Values)
		{
			if (value == null || value.identity == null)
			{
				continue;
			}
			MultiplayerLobbyPlayer component = value.identity.GetComponent<MultiplayerLobbyPlayer>();
			if (component == null)
			{
				return false;
			}
			players++;
			if (component.IsReady)
			{
				ready++;
			}
		}
		return players >= OnlineNetConstants.MinPlayers && ready == players;
	}

	/// <summary>True once enough members are present to run a match.</summary>
	public static bool ServerHasEnoughPlayers()
	{
		return PlayerCount() >= OnlineNetConstants.MinPlayers;
	}

	[Server]
	public void ServerClearReadyFlags()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void BadPiggies.Multiplayer.MultiplayerNetworkManager::ServerClearReadyFlags()' called when server was not active");
			return;
		}
		foreach (NetworkConnectionToClient value in NetworkServer.connections.Values)
		{
			if (value != null && !(value.identity == null))
			{
				MultiplayerLobbyPlayer component = value.identity.GetComponent<MultiplayerLobbyPlayer>();
				if (component != null)
				{
					component.ServerSetReady(ready: false);
				}
			}
		}
	}

	public override void Update()
	{
		base.Update();
		if (m_state == SessionState.InLobby && NetworkServer.active && ServerHasEnoughPlayers() && ServerAllPlayersReady())
		{
			ServerBeginArenaLoad();
		}
	}

	[Server]
	// internal rather than private so the lobby's room view can start the match as the host.
	internal void ServerBeginArenaLoad()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void BadPiggies.Multiplayer.MultiplayerNetworkManager::ServerBeginArenaLoad()' called when server was not active");
			return;
		}
		MultiplayerArenaPresence component = GetComponent<MultiplayerArenaPresence>();
		if (component != null)
		{
			component.ServerNotifyArenaLoading();
		}
		int num = 1;
		MultiplayerMatchController component2 = GetComponent<MultiplayerMatchController>();
		if (component2 != null)
		{
			num = component2.ServerBeginRound();
		}
		SetState(SessionState.LoadingArena);
		Debug.Log($"[Multiplayer] Round {num}: sending all {PlayerCount()} peer(s) into the arena.");
		NetworkServer.SendToAll(new MultiplayerLoadArenaMessage
		{
			sandboxIdentifier = MultiplayerNetConfig.ArenaSandboxIdentifier,
			round = num
		});
	}

	[Server]
	public void ServerRestartArena()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void BadPiggies.Multiplayer.MultiplayerNetworkManager::ServerRestartArena()' called when server was not active");
			return;
		}
		ServerClearReadyFlags();
		ServerBeginArenaLoad();
	}

	private void SetState(SessionState newState)
	{
		if (m_state != newState)
		{
			m_state = newState;
			SessionChanged?.Invoke();
		}
	}
}
}
