using BadPiggies.Multiplayer.Online;
using BadPiggies.Multiplayer.Online.Ui;
using UnityEngine;
using kcp2k;

namespace BadPiggies.Multiplayer
{
/// <summary>
/// Entry point for the multiplayer system: installs the main-menu hook and owns the (lazily
/// created, scene-surviving) session object that carries the transport, the network manager and the
/// in-game UI components.
/// </summary>
public static class MultiplayerBootstrap
{
	private const string SessionObjectName = "[Multiplayer Session]";

	public const string LobbyPlayerResourcePath = "Multiplayer/MultiplayerLobbyPlayer";

	public const string MachineAnchorResourcePath = "Multiplayer/MultiplayerMachineAnchor";

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Install()
	{
		// The machine layer and its collision matrix are applied from code so the system does not
		// depend on a hand-edited ProjectSettings file surviving a fresh clone.
		MultiplayerLayers.EnsureConfigured();

		GameObject gameObject = new GameObject("[Multiplayer Bootstrap]");
		gameObject.AddComponent<MultiplayerMenuInjector>();
		Object.DontDestroyOnLoad(gameObject);

		// Bring up the online layer up-front so the LAN browser is already listening when the
		// player opens the multiplayer menu, and so the session survives scene changes.
		OnlineLobbyService.GetOrCreate();
		OnlinePrefs.EnsureLoaded();
		if (OnlinePrefs.Data.EnableLanBroadcast)
		{
			OnlineLanDiscovery.GetOrCreate().StartListening();
		}
	}

	public static MultiplayerNetworkManager GetOrCreateSession()
	{
		GameObject gameObject = GameObject.Find(SessionObjectName);
		if (gameObject != null)
		{
			return gameObject.GetComponent<MultiplayerNetworkManager>();
		}
		GameObject gameObject2 = new GameObject(SessionObjectName);
		gameObject2.SetActive(value: false);
		Object.DontDestroyOnLoad(gameObject2);
		KcpTransport kcpTransport = gameObject2.AddComponent<KcpTransport>();
		kcpTransport.port = MultiplayerNetConfig.DefaultPort;
		MultiplayerNetworkManager multiplayerNetworkManager = gameObject2.AddComponent<MultiplayerNetworkManager>();
		multiplayerNetworkManager.transport = kcpTransport;
		multiplayerNetworkManager.playerPrefab = Resources.Load<GameObject>(LobbyPlayerResourcePath);
		if (multiplayerNetworkManager.playerPrefab == null)
		{
			Debug.LogError("[Multiplayer] Missing lobby player prefab at Resources/" + LobbyPlayerResourcePath +
				". Multiplayer cannot spawn players until it exists.");
		}
		GameObject gameObject3 = Resources.Load<GameObject>(MachineAnchorResourcePath);
		if (gameObject3 != null)
		{
			multiplayerNetworkManager.spawnPrefabs.Add(gameObject3);
		}
		else
		{
			Debug.LogError("[Multiplayer] Missing machine anchor prefab at Resources/" + MachineAnchorResourcePath +
				". Machines will still assemble on the host but carry no network identity.");
		}
		gameObject2.AddComponent<MultiplayerArenaLoader>();
		gameObject2.AddComponent<MultiplayerLobbyUI>();
		gameObject2.AddComponent<MultiplayerMatchController>();
		gameObject2.AddComponent<MultiplayerBuildPhaseUI>();
		gameObject2.AddComponent<MultiplayerResultUI>();
		gameObject2.AddComponent<MultiplayerArenaPresence>();
		gameObject2.SetActive(value: true);
		return multiplayerNetworkManager;
	}

	public static void OpenLobby()
	{
		MultiplayerNetworkManager orCreateSession = GetOrCreateSession();
		if (orCreateSession == null)
		{
			Debug.LogError("[Multiplayer] Could not create the multiplayer session object.");
			return;
		}
		OnlineLobbyService.GetOrCreate();
		OnlineLobbyWindow window = OnlineLobbyWindow.GetOrCreate();
		if (window != null)
		{
			window.Open();
			return;
		}
		// Fallback: the uGUI window could not be built (no Canvas/EventSystem, unexpected platform).
		MultiplayerLobbyUI component = orCreateSession.GetComponent<MultiplayerLobbyUI>();
		if (component != null)
		{
			component.SetSuppressedByOnlineUi(false);
			component.Open();
		}
	}
}
}
