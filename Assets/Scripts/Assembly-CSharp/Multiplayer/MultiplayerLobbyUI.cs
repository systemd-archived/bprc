using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BadPiggies.Multiplayer
{
/// <summary>
/// The original IMGUI lobby, kept as a fallback for when the uGUI online window cannot be built
/// (no Canvas/EventSystem). It lists every slot the room can hold, so it works for any room size.
/// </summary>
[RequireComponent(typeof(MultiplayerNetworkManager))]
public class MultiplayerLobbyUI : MonoBehaviour
{
	private enum Screen
	{
		Hidden,
		MainChoice,
		JoinEntry,
		Lobby
	}

	private MultiplayerNetworkManager m_manager;

	private Screen m_screen;

	private string m_addressInput = "localhost";

	private string m_portInput = ((ushort)7777).ToString();

	private string m_statusMessage;

	private bool m_suppressedForGameplay;

	/// <summary>
	/// Set while <c>BadPiggies.Multiplayer.Online.OnlineLobbyWindow</c> owns the lobby. The original
	/// IMGUI panel stays compiled in as a fallback, but it must never draw on top of the new UI.
	/// </summary>
	private bool m_suppressedByOnlineUi;

	private readonly List<MultiplayerLobbyPlayer> m_players = new List<MultiplayerLobbyPlayer>();

	public bool IsOpen => m_screen != Screen.Hidden;

	public bool IsSuppressedByOnlineUi => m_suppressedByOnlineUi;

	/// <summary>
	/// Hands the lobby over to the online UI (or takes it back). While suppressed, <see cref="Open"/>
	/// and <see cref="OnGUI"/> do nothing at all.
	/// </summary>
	public void SetSuppressedByOnlineUi(bool suppressed)
	{
		m_suppressedByOnlineUi = suppressed;
		if (suppressed)
		{
			Close();
			MultiplayerGameInputBlock.Release(this);
		}
	}

	private void Awake()
	{
		m_manager = GetComponent<MultiplayerNetworkManager>();
	}

	private void OnEnable()
	{
		m_manager.SessionChanged += OnSessionChanged;
		m_manager.SessionEnded += OnSessionEnded;
		SceneManager.sceneLoaded += OnSceneLoaded;
	}

	private void OnDisable()
	{
		m_manager.SessionChanged -= OnSessionChanged;
		m_manager.SessionEnded -= OnSessionEnded;
		SceneManager.sceneLoaded -= OnSceneLoaded;
		MultiplayerGameInputBlock.Release(this);
	}

	private void Update()
	{
		if (m_suppressedByOnlineUi)
		{
			return;
		}
		MultiplayerGameInputBlock.Apply(this, m_screen != Screen.Hidden && !m_suppressedForGameplay);
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		if (scene.name != "MainMenu")
		{
			HideForGameplay();
		}
	}

	public void Open()
	{
		if (m_suppressedByOnlineUi)
		{
			return;
		}
		m_suppressedForGameplay = false;
		m_statusMessage = null;
		m_screen = Screen.MainChoice;
	}

	public void Close()
	{
		if (m_suppressedByOnlineUi)
		{
			return;
		}
		m_screen = Screen.Hidden;
	}

	public void HideForGameplay()
	{
		Close();
		m_suppressedForGameplay = true;
	}

	private void OnSessionChanged()
	{
		if (!m_suppressedForGameplay && m_manager.State == MultiplayerNetworkManager.SessionState.InLobby)
		{
			m_screen = Screen.Lobby;
		}
	}

	private void OnSessionEnded(string reason)
	{
		if (!m_suppressedForGameplay)
		{
			m_statusMessage = reason;
			m_screen = Screen.MainChoice;
		}
	}

	private void OnGUI()
	{
		if (!m_suppressedByOnlineUi && m_screen != Screen.Hidden && !m_suppressedForGameplay)
		{
			GUILayout.BeginArea(new Rect(((float)UnityEngine.Screen.width - 420f) * 0.5f, ((float)UnityEngine.Screen.height - 320f) * 0.5f, 420f, 320f), GUI.skin.box);
			GUILayout.Label("<b>MULTIPLAYER</b>", RichLabel());
			switch (m_screen)
			{
			case Screen.MainChoice:
				DrawMainChoice();
				break;
			case Screen.JoinEntry:
				DrawJoinEntry();
				break;
			case Screen.Lobby:
				DrawLobby();
				break;
			}
			if (!string.IsNullOrEmpty(m_statusMessage))
			{
				GUILayout.FlexibleSpace();
				GUILayout.Label(m_statusMessage);
			}
			GUILayout.EndArea();
		}
	}

	private static GUIStyle RichLabel()
	{
		return new GUIStyle(GUI.skin.label)
		{
			richText = true,
			alignment = TextAnchor.MiddleCenter,
			fontSize = 18
		};
	}

	private void DrawMainChoice()
	{
		GUILayout.Space(12f);
		GUILayout.Label($"Room capacity: {m_manager.MaxPlayers} (the host sets this in the online lobby)");
		GUILayout.Label("Port");
		m_portInput = GUILayout.TextField(m_portInput, 5);
		GUILayout.Space(8f);
		if (GUILayout.Button("Create game", GUILayout.Height(36f)) && TryReadPort(out var port))
		{
			if (!m_manager.TryStartHost(port, out var error))
			{
				m_statusMessage = error;
			}
			else
			{
				m_statusMessage = $"Hosting on port {port}. Waiting for players…";
			}
		}
		if (GUILayout.Button("Join by IP", GUILayout.Height(36f)))
		{
			m_statusMessage = null;
			m_screen = Screen.JoinEntry;
		}
		GUILayout.Space(8f);
		if (GUILayout.Button("Back"))
		{
			Close();
		}
	}

	private void DrawJoinEntry()
	{
		GUILayout.Space(12f);
		GUILayout.Label("Host address (IP or hostname)");
		m_addressInput = GUILayout.TextField(m_addressInput);
		GUILayout.Label("Port");
		m_portInput = GUILayout.TextField(m_portInput, 5);
		GUILayout.Space(8f);
		GUI.enabled = m_manager.State == MultiplayerNetworkManager.SessionState.Idle;
		if (GUILayout.Button("Connect", GUILayout.Height(36f)))
		{
			if (MultiplayerNetConfig.TryParseEndpoint(m_addressInput, out var host, out var port, out var error))
			{
				ushort num = port;
				if (m_addressInput.IndexOf(':') < 0 && TryReadPort(out var port2))
				{
					num = port2;
				}
				if (!m_manager.TryStartClient(host, num, out var error2))
				{
					m_statusMessage = error2;
				}
				else
				{
					m_statusMessage = $"Connecting to {host}:{num}…";
				}
			}
			else
			{
				m_statusMessage = error;
			}
		}
		GUI.enabled = true;
		if (m_manager.State == MultiplayerNetworkManager.SessionState.Connecting)
		{
			GUILayout.Label("Connecting…");
			if (GUILayout.Button("Cancel"))
			{
				m_manager.LeaveSession();
				m_statusMessage = null;
			}
		}
		GUILayout.Space(8f);
		if (GUILayout.Button("Back"))
		{
			m_statusMessage = null;
			m_screen = Screen.MainChoice;
		}
	}

	private void DrawLobby()
	{
		GUILayout.Space(8f);
		GUILayout.Label($"Role: {(NetworkServer.active ? "Host" : "Client")}   " +
			$"Players: {m_players.Count}/{m_manager.MaxPlayers}");
		MultiplayerLobbyPlayer.CollectInto(m_players);
		GUILayout.Space(6f);
		GUILayout.Label("<b>Players</b>", RichLabel());
		// One row per slot the room can hold, so the host can see who is still missing.
		for (int i = 0; i < m_manager.MaxPlayers; i++)
		{
			MultiplayerLobbyPlayer multiplayerLobbyPlayer = FindAtSlot(i);
			GUILayout.BeginHorizontal(GUI.skin.box);
			if (multiplayerLobbyPlayer == null)
			{
				GUILayout.Label($"Slot {i + 1}: waiting…");
			}
			else
			{
				string arg = (multiplayerLobbyPlayer.isLocalPlayer ? " (you)" : string.Empty);
				GUILayout.Label($"Slot {i + 1}: {multiplayerLobbyPlayer.DisplayName}{arg}");
				GUILayout.FlexibleSpace();
				GUILayout.Label(multiplayerLobbyPlayer.IsReady ? "READY" : "not ready");
			}
			GUILayout.EndHorizontal();
		}
		GUILayout.Space(10f);
		bool flag = m_players.Count >= m_manager.MinPlayers;
		MultiplayerLobbyPlayer multiplayerLobbyPlayer2 = FindLocalPlayer();
		GUI.enabled = (multiplayerLobbyPlayer2 != null) & flag;
		if (GUILayout.Button((multiplayerLobbyPlayer2 != null && multiplayerLobbyPlayer2.IsReady) ? "Cancel ready" : "Ready", GUILayout.Height(34f)))
		{
			multiplayerLobbyPlayer2.ToggleReadyLocal();
		}
		GUI.enabled = true;
		if (m_manager.State == MultiplayerNetworkManager.SessionState.LoadingArena)
		{
			GUILayout.Label("Everybody ready — loading arena…");
		}
		else if (!flag)
		{
			GUILayout.Label($"Waiting for players ({m_players.Count}/{m_manager.MinPlayers} at least)…");
		}
		else
		{
			GUILayout.Label("The round starts once every player is ready.");
		}
		GUILayout.Space(6f);
		if (GUILayout.Button("Leave"))
		{
			m_manager.LeaveSession();
			m_statusMessage = null;
			m_screen = Screen.MainChoice;
		}
	}

	private MultiplayerLobbyPlayer FindAtSlot(int slot)
	{
		for (int i = 0; i < m_players.Count; i++)
		{
			if (m_players[i] != null && m_players[i].Slot == slot)
			{
				return m_players[i];
			}
		}
		return null;
	}

	private MultiplayerLobbyPlayer FindLocalPlayer()
	{
		for (int i = 0; i < m_players.Count; i++)
		{
			if (m_players[i] != null && m_players[i].isLocalPlayer)
			{
				return m_players[i];
			}
		}
		return null;
	}

	private bool TryReadPort(out ushort port)
	{
		if (!ushort.TryParse(m_portInput, out port) || port == 0)
		{
			m_statusMessage = "Port must be a number between 1 and 65535.";
			return false;
		}
		return true;
	}
}
}
