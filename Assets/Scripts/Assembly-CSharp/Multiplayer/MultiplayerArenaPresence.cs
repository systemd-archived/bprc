using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BadPiggies.Multiplayer
{
[RequireComponent(typeof(MultiplayerNetworkManager))]
public class MultiplayerArenaPresence : MonoBehaviour
{
	private const float CloseAnnounceGrace = 0.1f;

	private MultiplayerNetworkManager m_manager;

	private bool m_handlerRegistered;

	private bool m_sentInArena;

	private bool m_hasSent;

	private MultiplayerLobbyPlayer m_sentThrough;

	private bool m_armed;

	private float m_closeAtTime;

	private void Awake()
	{
		m_manager = GetComponent<MultiplayerNetworkManager>();
	}

	private void Update()
	{
		if (!NetworkClient.active && !NetworkServer.active)
		{
			ResetState();
			return;
		}
		RegisterHandler();
		ReportLocalPresence();
		ServerEvaluate();
	}

	private void OnDisable()
	{
		ResetState();
	}

	private void ResetState()
	{
		m_hasSent = false;
		m_sentThrough = null;
		m_armed = false;
		m_closeAtTime = 0f;
		m_handlerRegistered = false;
	}

	private void RegisterHandler()
	{
		if (!m_handlerRegistered && NetworkClient.active)
		{
			NetworkClient.RegisterHandler<MultiplayerSessionClosingMessage>(OnSessionClosing);
			m_handlerRegistered = true;
		}
		else if (m_handlerRegistered && !NetworkClient.active)
		{
			m_handlerRegistered = false;
		}
	}

	private static bool LocalPeerIsInArena()
	{
		return SceneManager.GetActiveScene().name == "LevelStub";
	}

	private void ReportLocalPresence()
	{
		MultiplayerLobbyPlayer multiplayerLobbyPlayer = FindLocalPlayer();
		if (multiplayerLobbyPlayer == null)
		{
			m_hasSent = false;
			m_sentThrough = null;
			return;
		}
		bool flag = LocalPeerIsInArena();
		if (!m_hasSent || !(m_sentThrough == multiplayerLobbyPlayer) || m_sentInArena != flag)
		{
			multiplayerLobbyPlayer.CmdSetInArena(flag);
			m_sentInArena = flag;
			m_sentThrough = multiplayerLobbyPlayer;
			m_hasSent = true;
		}
	}

	private void ServerEvaluate()
	{
		if (!NetworkServer.active)
		{
			return;
		}
		if (m_closeAtTime > 0f)
		{
			if (Time.unscaledTime >= m_closeAtTime)
			{
				m_closeAtTime = 0f;
				Debug.Log("[Multiplayer] Everybody left the arena; closing the session.");
				m_manager.LeaveSession();
			}
			return;
		}
		int num = 0;
		int num2 = 0;
		foreach (NetworkConnectionToClient value in NetworkServer.connections.Values)
		{
			if (value == null || value.identity == null)
			{
				continue;
			}
			MultiplayerLobbyPlayer component = value.identity.GetComponent<MultiplayerLobbyPlayer>();
			if (!(component == null))
			{
				num++;
				if (component.InArena)
				{
					num2++;
				}
			}
		}
		if (num2 > 0)
		{
			m_armed = true;
		}
		else if (m_armed && num != 0)
		{
			NetworkServer.SendToAll<MultiplayerSessionClosingMessage>(default);
			m_armed = false;
			m_closeAtTime = Time.unscaledTime + 0.1f;
		}
	}

	private void OnSessionClosing(MultiplayerSessionClosingMessage message)
	{
		if (!NetworkServer.active)
		{
			Debug.Log("[Multiplayer] Host closed the session — everybody had left the arena.");
			m_manager.LeaveSession();
		}
	}

	public void ServerNotifyArenaLoading()
	{
		if (NetworkServer.active)
		{
			m_armed = false;
			m_closeAtTime = 0f;
		}
	}

	private static MultiplayerLobbyPlayer FindLocalPlayer()
	{
		NetworkIdentity localPlayer = NetworkClient.localPlayer;
		if (!(localPlayer != null))
		{
			return null;
		}
		return localPlayer.GetComponent<MultiplayerLobbyPlayer>();
	}
}
}
