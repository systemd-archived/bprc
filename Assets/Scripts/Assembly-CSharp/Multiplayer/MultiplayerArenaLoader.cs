using Mirror;
using UnityEngine;

namespace BadPiggies.Multiplayer
{
[RequireComponent(typeof(MultiplayerNetworkManager))]
public class MultiplayerArenaLoader : MonoBehaviour
{
	private MultiplayerNetworkManager m_manager;

	private bool m_handlerRegistered;

	private void Awake()
	{
		m_manager = GetComponent<MultiplayerNetworkManager>();
	}

	private void Update()
	{
		if (!m_handlerRegistered && NetworkClient.active)
		{
			NetworkClient.RegisterHandler<MultiplayerLoadArenaMessage>(OnLoadArena);
			m_handlerRegistered = true;
		}
		else if (m_handlerRegistered && !NetworkClient.active)
		{
			m_handlerRegistered = false;
		}
	}

	private void OnLoadArena(MultiplayerLoadArenaMessage message)
	{
		string text = (string.IsNullOrWhiteSpace(message.sandboxIdentifier) ? "S-VS" : message.sandboxIdentifier);
		GameManager instance = Singleton<GameManager>.Instance;
		if (instance == null)
		{
			Debug.LogError("[Multiplayer] GameManager is unavailable; cannot load the arena.");
			return;
		}
		MultiplayerMatchController component = GetComponent<MultiplayerMatchController>();
		if (component != null)
		{
			component.SetRound(message.round);
		}
		Debug.Log($"[Multiplayer] Loading arena sandbox '{text}' for round {message.round}.");
		MultiplayerLobbyUI component2 = GetComponent<MultiplayerLobbyUI>();
		if (component2 != null)
		{
			component2.HideForGameplay();
		}
		instance.LoadSandboxLevel(text);
	}
}
}
