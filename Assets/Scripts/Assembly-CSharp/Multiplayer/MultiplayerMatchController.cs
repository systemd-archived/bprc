using System.Collections.Generic;
using BadPiggies.Multiplayer.Online;
using Mirror;
using UnityEngine;

namespace BadPiggies.Multiplayer
{
/// <summary>
/// Runs one round of a many-player match.
///
/// The host is authoritative: every peer builds every machine, but only the host simulates them and
/// streams each machine's poses to the others (MultiplayerMachineAnchor). A round is an elimination
/// round — a player drops out when their pilot is thrown out of the machine or their machine leaves
/// the arena — and it ends when one machine is left standing.
///
/// Slots are the network slots handed out by MultiplayerNetworkManager and stay stable for the life
/// of the session (0..7). A round's machines are held in slot-indexed arrays, while the arena layout
/// uses "lanes": the position of a slot inside the round's ordered player list. Keeping the two
/// apart means a player who leaves mid-session does not shift anybody else's network slot.
/// </summary>
[RequireComponent(typeof(MultiplayerNetworkManager))]
public class MultiplayerMatchController : MonoBehaviour
{
	public enum LossReason : byte
	{
		PilotEjected,
		LeftTheArena,
		/// <summary>The player's connection dropped.</summary>
		LeftTheSession
	}

	/// <summary>Sentinel meaning "nobody won" in a match-over announcement.</summary>
	public const int NoWinner = -1;

	/// <summary>Number of network slots a room can hand out.</summary>
	public const int MaxSlots = OnlineNetConstants.MaxRoomPlayers;

	private readonly byte[][] m_roundBlueprints = new byte[MaxSlots][];

	private readonly GameObject[] m_serverAnchors = new GameObject[MaxSlots];

	private readonly Contraption[] m_localMachines = new Contraption[MaxSlots];

	private readonly MultiplayerMachineAnchor[] m_localAnchors = new MultiplayerMachineAnchor[MaxSlots];

	private readonly Pig[] m_localPigs = new Pig[MaxSlots];

	private readonly bool[] m_eliminated = new bool[MaxSlots];

	private readonly LossReason[] m_lossReasons = new LossReason[MaxSlots];

	private readonly List<int> m_eliminationOrder = new List<int>();

	private readonly List<int> m_roundSlots = new List<int>();

	private bool m_serverAssembled;

	private bool m_localBattleStarted;

	private int m_round;

	private int m_serverRound;

	private bool m_matchOver;

	private int m_winnerSlot = NoWinner;

	private const float KinematicAuditInterval = 0.5f;

	private float m_nextKinematicAudit;

	private byte[] m_lastLocalBlueprint;

	public bool BattleStarted => m_localBattleStarted;

	public int LocalSlot { get; private set; } = -1;

	public int CurrentRound => m_round;

	public bool HasStoredBlueprint => m_lastLocalBlueprint != null;

	/// <summary>True once the round has been decided (by elimination or by a draw).</summary>
	public bool HasResult => m_matchOver;

	/// <summary>Slot of the surviving player, or <see cref="NoWinner"/> for a draw.</summary>
	public int WinnerSlot => m_winnerSlot;

	public bool IsDraw => m_matchOver && m_winnerSlot < 0;

	/// <summary>Players the current round started with.</summary>
	public int RoundPlayers => m_roundSlots.Count;

	public int EliminatedCount => m_eliminationOrder.Count;

	public bool LocalPlayerWon => m_matchOver && m_winnerSlot >= 0 && m_winnerSlot == LocalSlot;

	public bool LocalPlayerEliminated => LocalSlot >= 0 && LocalSlot < MaxSlots && m_eliminated[LocalSlot];

	public LossReason LocalLossReason => (LocalSlot >= 0 && LocalSlot < MaxSlots) ? m_lossReasons[LocalSlot] : LossReason.LeftTheArena;

	/// <summary>
	/// 1-based finishing position, or 0 while the round is still running. The winner is 1st, the
	/// first player eliminated is last.
	/// </summary>
	public int LocalPlacement
	{
		get
		{
			if (!m_matchOver || LocalSlot < 0)
			{
				return 0;
			}
			if (m_winnerSlot == LocalSlot)
			{
				return 1;
			}
			int index = m_eliminationOrder.IndexOf(LocalSlot);
			if (index < 0)
			{
				// Still standing but not the winner: only possible on a draw.
				return m_roundSlots.Count;
			}
			return Mathf.Max(2, m_roundSlots.Count - index);
		}
	}

	public Contraption GetMachine(int slot)
	{
		if (slot < 0 || slot >= MaxSlots)
		{
			return null;
		}
		return m_localMachines[slot];
	}

	public MultiplayerMachineAnchor GetAnchor(int slot)
	{
		if (slot < 0 || slot >= MaxSlots)
		{
			return null;
		}
		return m_localAnchors[slot];
	}

	public void RegisterAnchor(int slot, MultiplayerMachineAnchor anchor)
	{
		if (slot >= 0 && slot < MaxSlots)
		{
			m_localAnchors[slot] = anchor;
		}
	}

	/// <summary>The slots taking part in the round, in lane order.</summary>
	public IList<int> RoundSlots => m_roundSlots;

	[Server]
	public int ServerBeginRound()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Int32 BadPiggies.Multiplayer.MultiplayerMatchController::ServerBeginRound()' called when server was not active");
			return default(int);
		}
		m_serverRound++;
		return m_serverRound;
	}

	public void SetRound(int round)
	{
		if (round <= 0)
		{
			round = 1;
		}
		m_round = round;
	}

	public void ResetForNewSession()
	{
		m_round = 0;
		m_serverRound = 0;
	}

	[Server]
	public void ServerApplyTouch(int slot, int partIndex, float x, float y)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void BadPiggies.Multiplayer.MultiplayerMatchController::ServerApplyTouch(System.Int32,System.Int32,System.Single,System.Single)' called when server was not active");
			return;
		}
		Contraption machine = GetMachine(slot);
		if (!(machine == null))
		{
			MultiplayerInput.ApplyTouch(machine, partIndex, new Vector3(x, y, 0f));
		}
	}

	[Server]
	public void ServerApplyBulk(int slot, byte kind, int partType, int direction)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void BadPiggies.Multiplayer.MultiplayerMatchController::ServerApplyBulk(System.Int32,System.Byte,System.Int32,System.Int32)' called when server was not active");
			return;
		}
		Contraption machine = GetMachine(slot);
		if (!(machine == null))
		{
			MultiplayerInput.ApplyBulk(machine, (MultiplayerInput.BulkKind)kind, partType, direction);
		}
	}

	private void Awake()
	{
		MultiplayerMachineAnchor.PilotEjected -= OnPilotEjected;
		MultiplayerMachineAnchor.PilotEjected += OnPilotEjected;
	}

	private void OnDestroy()
	{
		MultiplayerMachineAnchor.PilotEjected -= OnPilotEjected;
	}

	private void Update()
	{
		if (!m_localBattleStarted)
		{
			return;
		}
		MultiplayerInput.Reassert();
		MultiplayerTraffic.Tick();
		AssertBattleInvariants();
		if (!NetworkServer.active)
		{
			AssertGuestKinematic();
			return;
		}
		if (!m_matchOver)
		{
			ServerScanForLeavers();
			ServerScanForEjection();
		}
		else
		{
			ServerCheckRematch();
		}
	}

	private void AssertGuestKinematic()
	{
		if (Time.unscaledTime < m_nextKinematicAudit)
		{
			return;
		}
		m_nextKinematicAudit = Time.unscaledTime + 0.5f;
		int num = 0;
		for (int i = 0; i < m_localMachines.Length; i++)
		{
			num += MultiplayerBattleAssembler.CountDynamicBodies(m_localMachines[i]);
		}
		if (num != 0)
		{
			for (int j = 0; j < m_localMachines.Length; j++)
			{
				MultiplayerBattleAssembler.SetSimulated(m_localMachines[j], simulated: false);
			}
			Debug.LogWarning($"[Multiplayer] {num} rigidbodies had started simulating on this guest " +
				"and have been made kinematic again; the host's stream is the only authority here.");
		}
	}

	[Server]
	private void ServerScanForEjection()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void BadPiggies.Multiplayer.MultiplayerMatchController::ServerScanForEjection()' called when server was not active");
			return;
		}
		for (int i = 0; i < m_localAnchors.Length; i++)
		{
			MultiplayerMachineAnchor multiplayerMachineAnchor = m_localAnchors[i];
			if (multiplayerMachineAnchor != null && multiplayerMachineAnchor.IsPilotEjected)
			{
				ServerEliminate(i, LossReason.PilotEjected);
			}
		}
	}

	private void AssertBattleInvariants()
	{
		if (!Mathf.Approximately(Time.timeScale, 1f))
		{
			Time.timeScale = 1f;
		}
		for (int i = 0; i < m_localPigs.Length; i++)
		{
			if (m_localPigs[i] != null)
			{
				m_localPigs[i].CheckCameraLimits = false;
			}
		}
	}

	public static MultiplayerMatchController Resolve()
	{
		Mirror.NetworkManager singleton = Mirror.NetworkManager.singleton;
		if (!(singleton != null))
		{
			return null;
		}
		return singleton.GetComponent<MultiplayerMatchController>();
	}

	public byte[] CaptureLocalBlueprint()
	{
		LevelManager levelManager = WPFMonoBehaviour.levelManager;
		if (levelManager == null || levelManager.CurrentGameMode == null)
		{
			Debug.LogError("[Multiplayer] No level/game mode; cannot capture a blueprint.");
			return null;
		}
		Contraption contraptionProto = levelManager.CurrentGameMode.ContraptionProto;
		if (contraptionProto == null)
		{
			Debug.LogError("[Multiplayer] No contraption on the build grid; cannot capture a blueprint.");
			return null;
		}
		ContraptionDataset contraptionDataset = MultiplayerBlueprint.CaptureFrom(contraptionProto);
		if (contraptionDataset.ContraptionDatasetList.Count == 0)
		{
			Debug.LogWarning("[Multiplayer] Refusing to submit an empty machine.");
			return null;
		}
		byte[] array = (m_lastLocalBlueprint = MultiplayerBlueprint.Serialize(contraptionDataset));
		Debug.Log($"[Multiplayer] Captured blueprint: {contraptionDataset.ContraptionDatasetList.Count} parts, {array.Length} bytes.");
		return array;
	}

	public bool RestoreLastBlueprint()
	{
		if (m_lastLocalBlueprint == null)
		{
			return false;
		}
		LevelManager levelManager = WPFMonoBehaviour.levelManager;
		GameMode gameMode = ((levelManager != null) ? levelManager.CurrentGameMode : null);
		if (gameMode == null || gameMode.ContraptionProto == null || levelManager.ConstructionUI == null)
		{
			return false;
		}
		ContraptionDataset contraptionDataset = MultiplayerBlueprint.Deserialize(m_lastLocalBlueprint);
		if (contraptionDataset == null)
		{
			Debug.LogWarning("[Multiplayer] The stored blueprint no longer decodes; starting from an empty grid.");
			m_lastLocalBlueprint = null;
			return false;
		}
		levelManager.ConstructionUI.ClearContraption();
		gameMode.BuildContraption(contraptionDataset);
		Debug.Log("[Multiplayer] Restored last round's machine to the grid " +
			$"({contraptionDataset.ContraptionDatasetList.Count} parts).");
		return true;
	}

	/// <summary>Stores a blueprint that arrived over the wire (every peer, host included, ends up here).</summary>
	public void StoreRoundBlueprint(byte slot, byte[] payload)
	{
		if (slot >= MaxSlots)
		{
			Debug.LogWarning($"[Multiplayer] Ignoring a blueprint for out-of-range slot {slot}.");
			return;
		}
		m_roundBlueprints[slot] = payload;
	}

	public bool ServerSubmitBlueprint(int slot, byte[] payload, int round)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Multiplayer] Blueprint submitted on a peer that is not the host; ignored.");
			return false;
		}
		if (slot < 0 || slot >= MaxSlots)
		{
			Debug.LogWarning($"[Multiplayer] Blueprint from an invalid slot {slot}; ignored.");
			return false;
		}
		if (round != m_serverRound)
		{
			Debug.LogWarning($"[Multiplayer] Slot {slot} submitted a blueprint for round {round} while the " +
				$"host is running round {m_serverRound}; ignored as stale.");
			return false;
		}
		if (m_serverAssembled)
		{
			Debug.LogWarning($"[Multiplayer] Slot {slot} submitted a blueprint after assembly; ignored.");
			return false;
		}
		if (m_roundBlueprints[slot] != null)
		{
			Debug.LogWarning($"[Multiplayer] Slot {slot} already submitted a blueprint; ignored.");
			return false;
		}
		if (MultiplayerBlueprint.Deserialize(payload) == null)
		{
			Debug.LogWarning($"[Multiplayer] Slot {slot} sent a blueprint that failed validation; ignored.");
			return false;
		}
		m_roundBlueprints[slot] = payload;
		Debug.Log($"[Multiplayer] Round {m_serverRound}: blueprint stored for slot {slot} ({payload.Length} bytes).");
		ServerTryAssemble();
		return true;
	}

	[Server]
	public bool ServerWithdrawBlueprint(int slot)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Boolean BadPiggies.Multiplayer.MultiplayerMatchController::ServerWithdrawBlueprint(System.Int32)' called when server was not active");
			return default(bool);
		}
		if (slot < 0 || slot >= MaxSlots)
		{
			return false;
		}
		if (m_serverAssembled)
		{
			Debug.LogWarning($"[Multiplayer] Slot {slot} tried to withdraw after the match was assembled; ignored.");
			return false;
		}
		if (m_roundBlueprints[slot] == null)
		{
			return false;
		}
		m_roundBlueprints[slot] = null;
		Debug.Log($"[Multiplayer] Slot {slot} withdrew its blueprint.");
		return true;
	}

	/// <summary>Slots of the players currently connected, ascending.</summary>
	[Server]
	private List<int> ServerConnectedSlots()
	{
		List<int> list = new List<int>();
		if (!NetworkServer.active)
		{
			return list;
		}
		foreach (NetworkConnectionToClient value in NetworkServer.connections.Values)
		{
			if (value == null || value.identity == null)
			{
				continue;
			}
			MultiplayerLobbyPlayer component = value.identity.GetComponent<MultiplayerLobbyPlayer>();
			if (component != null && component.Slot >= 0 && component.Slot < MaxSlots)
			{
				list.Add(component.Slot);
			}
		}
		list.Sort();
		return list;
	}

	private void ServerTryAssemble()
	{
		if (m_serverAssembled)
		{
			return;
		}
		List<int> slots = ServerConnectedSlots();
		if (slots.Count < OnlineNetConstants.MinPlayers)
		{
			return;
		}
		for (int i = 0; i < slots.Count; i++)
		{
			if (m_roundBlueprints[slots[i]] == null)
			{
				return;
			}
		}
		if (!MultiplayerLayers.AreConfigured(out var error))
		{
			Debug.LogError("[Multiplayer] " + error + " Aborting assembly.");
			return;
		}
		MultiplayerLobbyPlayer broadcaster = ServerFindBroadcaster();
		if (broadcaster == null)
		{
			Debug.LogError("[Multiplayer] No lobby player to broadcast the battle start through; aborting.");
			return;
		}
		m_serverAssembled = true;
		byte[] array = new byte[slots.Count];
		for (int j = 0; j < slots.Count; j++)
		{
			array[j] = (byte)slots[j];
		}
		// One blueprint per message keeps every individual message the size of a single machine,
		// however many players are in the round.
		for (int k = 0; k < slots.Count; k++)
		{
			broadcaster.RpcStoreBlueprint(array[k], m_roundBlueprints[slots[k]]);
		}
		Debug.Log($"[Multiplayer] Round {m_serverRound}: assembling {slots.Count} machine(s) for slots " +
			$"[{string.Join(", ", slots.ConvertAll(s => s.ToString()).ToArray())}].");
		broadcaster.RpcBeginBattle(array);
		BeginLocalBattle(array);
		for (int l = 0; l < slots.Count; l++)
		{
			Contraption machine = m_localMachines[slots[l]];
			if (machine != null)
			{
				ServerSpawnAnchor(machine, slots[l]);
			}
		}
	}

	[Server]
	private static MultiplayerLobbyPlayer ServerFindBroadcaster()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'BadPiggies.Multiplayer.MultiplayerLobbyPlayer BadPiggies.Multiplayer.MultiplayerMatchController::ServerFindBroadcaster()' called when server was not active");
			return null;
		}
		NetworkConnectionToClient localConnection = NetworkServer.localConnection;
		if (localConnection != null && localConnection.identity != null)
		{
			MultiplayerLobbyPlayer component = localConnection.identity.GetComponent<MultiplayerLobbyPlayer>();
			if (component != null)
			{
				return component;
			}
		}
		foreach (NetworkConnectionToClient value in NetworkServer.connections.Values)
		{
			if (value != null && !(value.identity == null))
			{
				MultiplayerLobbyPlayer component2 = value.identity.GetComponent<MultiplayerLobbyPlayer>();
				if (component2 != null)
				{
					return component2;
				}
			}
		}
		return null;
	}

	/// <summary>
	/// Builds every machine of the round on this peer. <paramref name="slotBytes"/> is the host's
	/// ordered slot list; the same list reaches every peer, so every peer derives the same lanes.
	/// </summary>
	public void BeginLocalBattle(byte[] slotBytes)
	{
		if (m_localBattleStarted)
		{
			return;
		}
		List<int> slots = new List<int>();
		if (slotBytes != null)
		{
			for (int i = 0; i < slotBytes.Length; i++)
			{
				if (slotBytes[i] < MaxSlots && !slots.Contains(slotBytes[i]))
				{
					slots.Add(slotBytes[i]);
				}
			}
		}
		slots.Sort();
		if (slots.Count < OnlineNetConstants.MinPlayers)
		{
			Debug.LogError($"[Multiplayer] The arena start lists {slots.Count} player(s); at least " +
				$"{OnlineNetConstants.MinPlayers} are needed. Aborting battle start.");
			return;
		}
		LevelManager levelManager = WPFMonoBehaviour.levelManager;
		if (levelManager == null || levelManager.CurrentGameMode == null)
		{
			Debug.LogError("[Multiplayer] No level/game mode on this peer; cannot start the battle.");
			return;
		}
		if (!MultiplayerLayers.AreConfigured(out var error))
		{
			Debug.LogError("[Multiplayer] " + error + " Aborting battle start.");
			return;
		}
		int num = ResolveLocalSlot();
		if (num < 0 || !slots.Contains(num))
		{
			Debug.LogError($"[Multiplayer] This peer could not match its slot ({num}) to the round's " +
				"player list; aborting battle start.");
			return;
		}
		// Decode and mirror every blueprint before touching the grid, so a bad payload cannot leave
		// the arena half-built.
		ContraptionDataset[] array = new ContraptionDataset[slots.Count];
		int num2 = slots.IndexOf(num);
		for (int j = 0; j < slots.Count; j++)
		{
			byte[] array2 = m_roundBlueprints[slots[j]];
			if (array2 == null)
			{
				Debug.LogError($"[Multiplayer] No blueprint for slot {slots[j]}; aborting battle start.");
				return;
			}
			array[j] = MultiplayerBattleAssembler.PrepareDataset(levelManager, array2, slots[j], j, slots.Count);
			if (array[j] == null)
			{
				Debug.LogError($"[Multiplayer] Slot {slots[j]}'s blueprint failed to decode; aborting battle start.");
				return;
			}
		}
		m_localBattleStarted = true;
		m_roundSlots.Clear();
		m_roundSlots.AddRange(slots);
		// Replicas first: the native transition consumes the build grid.
		for (int k = 0; k < slots.Count; k++)
		{
			if (k != num2)
			{
				m_localMachines[slots[k]] = MultiplayerBattleAssembler.BuildReplicaMachine(
					levelManager, array[k], slots[k], k, slots.Count);
			}
		}
		m_localMachines[num] = MultiplayerBattleAssembler.EnterNativeBattle(
			levelManager, array[num2], num, num2, slots.Count);

		bool simulated = NetworkServer.active;
		int num3 = 0;
		for (int l = 0; l < slots.Count; l++)
		{
			num3 += MultiplayerBattleAssembler.SetSimulated(m_localMachines[slots[l]], simulated);
		}
		if (!simulated)
		{
			int num4 = 0;
			for (int m = 0; m < slots.Count; m++)
			{
				num4 += MultiplayerBattleAssembler.CountDynamicBodies(m_localMachines[slots[m]]);
			}
			if (num4 > 0)
			{
				Debug.LogError($"[Multiplayer] {num4} rigidbodies are still simulating on this " +
					"guest; local physics will fight the snapshot stream over them.");
			}
		}
		VerifyMachinesCollide(slots);
		for (int n = 0; n < slots.Count; n++)
		{
			int slot = slots[n];
			m_localPigs[slot] = ((m_localMachines[slot] != null) ? (m_localMachines[slot].FindPig() as Pig) : null);
			if (m_localPigs[slot] == null)
			{
				Debug.LogWarning($"[Multiplayer] Slot {slot} has no pig; it can neither lose by ejection " +
					"nor be judged out of bounds.");
			}
		}
		LocalSlot = num;
		MultiplayerInput.Hook(num);
		MultiplayerTraffic.Begin();
		Debug.Log($"[Multiplayer] Battle started on this peer (own slot {num}, lane {num2}, " +
			$"{slots.Count} machine(s), " +
			string.Format("{0}, {1} rigidbodies).", simulated ? "simulating locally" : "network-driven", num3));
	}

	private void VerifyMachinesCollide(List<int> slots)
	{
		int partsLayer = MultiplayerLayers.ForSlot(0);
		int stockLayer = LayerMask.NameToLayer("Contraption");
		int num = 0;
		int num2 = 0;
		for (int i = 0; i < slots.Count; i++)
		{
			Contraption machine = m_localMachines[slots[i]];
			int num3 = MultiplayerLayers.CountColliders(machine, partsLayer);
			num += num3;
			num2 += MultiplayerLayers.CountColliders(machine, stockLayer);
			if (num3 == 0)
			{
				Debug.LogError($"[Multiplayer] Slot {slots[i]}'s machine has no colliders on " +
					$"{MultiplayerLayers.PartsLayerName}; it would fall through the arena.");
			}
		}
		if (num2 > 0)
		{
			Debug.LogError($"[Multiplayer] {num2} collider(s) are still on the stock Contraption " +
				"layer; machines on that layer would not be seen by every part's own logic.");
			return;
		}
		Debug.Log($"[Multiplayer] Collision check: {num} colliders across {slots.Count} machine(s) on " +
			$"{LayerMask.LayerToName(partsLayer)}; the matrix lets them all meet.");
	}

	private int ResolveLocalSlot()
	{
		NetworkIdentity localPlayer = NetworkClient.localPlayer;
		if (localPlayer != null)
		{
			MultiplayerLobbyPlayer component = localPlayer.GetComponent<MultiplayerLobbyPlayer>();
			if (component != null && component.Slot >= 0 && component.Slot < MaxSlots)
			{
				return component.Slot;
			}
		}
		if (!NetworkServer.active)
		{
			return -1;
		}
		return 0;
	}

	[Server]
	private void ServerSpawnAnchor(Contraption machine, int slot)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void BadPiggies.Multiplayer.MultiplayerMatchController::ServerSpawnAnchor(Contraption,System.Int32)' called when server was not active");
			return;
		}
		GameObject gameObject = Resources.Load<GameObject>("Multiplayer/MultiplayerMachineAnchor");
		if (gameObject == null)
		{
			Debug.LogError("[Multiplayer] Missing anchor prefab at Resources/Multiplayer/MultiplayerMachineAnchor; " +
				$"slot {slot}'s machine will have no network identity.");
			return;
		}
		GameObject gameObject2 = Object.Instantiate(gameObject, machine.transform.position, Quaternion.identity);
		gameObject2.name = $"MultiplayerMachineAnchor_S{slot + 1}";
		MultiplayerMachineAnchor component = gameObject2.GetComponent<MultiplayerMachineAnchor>();
		if (component == null)
		{
			Debug.LogError("[Multiplayer] Anchor prefab has no MultiplayerMachineAnchor component.");
			Object.Destroy(gameObject2);
			return;
		}
		component.ServerBind(machine, slot);
		NetworkServer.Spawn(gameObject2);
		m_serverAnchors[slot] = gameObject2;
	}

	public void ResetForNewMatch()
	{
		MultiplayerInput.Unhook();
		MultiplayerTraffic.End();
		LocalSlot = -1;
		for (int i = 0; i < m_roundBlueprints.Length; i++)
		{
			m_roundBlueprints[i] = null;
		}
		for (int j = 0; j < m_serverAnchors.Length; j++)
		{
			if (m_serverAnchors[j] != null)
			{
				if (NetworkServer.active)
				{
					NetworkServer.Destroy(m_serverAnchors[j]);
				}
				else
				{
					Object.Destroy(m_serverAnchors[j]);
				}
				m_serverAnchors[j] = null;
			}
		}
		for (int k = 0; k < m_localMachines.Length; k++)
		{
			m_localMachines[k] = null;
		}
		for (int l = 0; l < m_localAnchors.Length; l++)
		{
			m_localAnchors[l] = null;
		}
		for (int m = 0; m < m_localPigs.Length; m++)
		{
			m_localPigs[m] = null;
		}
		for (int n = 0; n < m_eliminated.Length; n++)
		{
			m_eliminated[n] = false;
		}
		m_eliminationOrder.Clear();
		m_roundSlots.Clear();
		m_matchOver = false;
		m_winnerSlot = NoWinner;
		m_serverAssembled = false;
		m_localBattleStarted = false;
	}

	private void OnPilotEjected(int slot)
	{
		if (NetworkServer.active)
		{
			ServerEliminate(slot, LossReason.PilotEjected);
		}
	}

	/// <summary>
	/// Host side: a player's machine is out of the round. The round ends once a single machine is
	/// left (or none are, which is a draw).
	/// </summary>
	[Server]
	public void ServerEliminate(int slot, LossReason reason)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void BadPiggies.Multiplayer.MultiplayerMatchController::ServerEliminate(System.Int32,BadPiggies.Multiplayer.MultiplayerMatchController/LossReason)' called when server was not active");
			return;
		}
		if (!m_localBattleStarted || slot < 0 || slot >= MaxSlots || m_eliminated[slot])
		{
			return;
		}
		m_eliminated[slot] = true;
		m_lossReasons[slot] = reason;
		m_eliminationOrder.Add(slot);
		Debug.Log($"[Multiplayer] Slot {slot} is out of round {m_serverRound} ({reason}); " +
			$"{ServerSurvivorCount()} machine(s) left.");
		MultiplayerLobbyPlayer broadcaster = ServerFindBroadcaster();
		if (broadcaster == null)
		{
			Debug.LogError("[Multiplayer] An elimination happened but there is no lobby player to announce it through.");
		}
		else
		{
			broadcaster.RpcElimination((byte)slot, (byte)reason);
		}
		ServerCheckMatchEnd();
	}

	[Server]
	private int ServerSurvivorCount()
	{
		int num = 0;
		for (int i = 0; i < m_roundSlots.Count; i++)
		{
			if (!m_eliminated[m_roundSlots[i]])
			{
				num++;
			}
		}
		return num;
	}

	[Server]
	private void ServerCheckMatchEnd()
	{
		if (m_matchOver || !NetworkServer.active || !m_localBattleStarted)
		{
			return;
		}
		int num = -1;
		int num2 = 0;
		for (int i = 0; i < m_roundSlots.Count; i++)
		{
			if (!m_eliminated[m_roundSlots[i]])
			{
				num2++;
				num = m_roundSlots[i];
			}
		}
		if (num2 > 1)
		{
			return;
		}
		ServerDeclareWinner(num2 == 1 ? num : NoWinner);
	}

	[Server]
	private void ServerDeclareWinner(int winnerSlot)
	{
		if (!NetworkServer.active || m_matchOver)
		{
			return;
		}
		m_matchOver = true;
		m_winnerSlot = winnerSlot;
		Debug.Log(winnerSlot >= 0
			? $"[Multiplayer] Round {m_serverRound} over: slot {winnerSlot} is the last machine standing."
			: $"[Multiplayer] Round {m_serverRound} over with no survivors.");
		MultiplayerLobbyPlayer broadcaster = ServerFindBroadcaster();
		if (broadcaster == null)
		{
			Debug.LogError("[Multiplayer] Match decided but there is no lobby player to announce it through.");
			return;
		}
		broadcaster.RpcMatchOver((byte)(winnerSlot < 0 ? 255 : winnerSlot));
	}

	/// <summary>Client side: one player's machine is out.</summary>
	public void ApplyElimination(byte slot, byte reason)
	{
		if (slot >= MaxSlots || m_eliminated[slot])
		{
			return;
		}
		m_eliminated[slot] = true;
		m_lossReasons[slot] = (LossReason)reason;
		m_eliminationOrder.Add(slot);
		Debug.Log($"[Multiplayer] Slot {slot} was eliminated ({(LossReason)reason}); " +
			$"this peer plays slot {LocalSlot}.");
	}

	/// <summary>Client side: the round is decided. 255 means nobody won.</summary>
	public void ApplyMatchOver(byte winner)
	{
		if (m_matchOver)
		{
			return;
		}
		m_matchOver = true;
		m_winnerSlot = winner == 255 ? NoWinner : winner;
		Debug.Log($"[Multiplayer] Match over: winner slot {(m_winnerSlot < 0 ? "none" : m_winnerSlot.ToString())}; " +
			$"this peer plays slot {LocalSlot}.");
	}

	/// <summary>Host side: a player's connection dropped. If the round is still collecting blueprints
	/// that player is simply dropped from it; once the round is running their machine forfeits.</summary>
	[Server]
	public void ServerPlayerLeft(int slot)
	{
		if (!NetworkServer.active)
		{
			return;
		}
		Debug.Log($"[Multiplayer] Slot {slot} left the session.");
		if (slot >= 0 && slot < MaxSlots)
		{
			m_roundBlueprints[slot] = null;
		}
		if (!m_localBattleStarted)
		{
			// Re-evaluate: the remaining players may now all have submitted, which the last
			// submission would otherwise never trigger.
			ServerTryAssemble();
			return;
		}
		ServerEliminate(slot, LossReason.LeftTheSession);
	}

	[Server]
	private void ServerScanForLeavers()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void BadPiggies.Multiplayer.MultiplayerMatchController::ServerScanForLeavers()' called when server was not active");
			return;
		}
		LevelManager levelManager = WPFMonoBehaviour.levelManager;
		if (levelManager == null)
		{
			return;
		}
		LevelManager.CameraLimits currentCameraLimits = levelManager.CurrentCameraLimits;
		if (currentCameraLimits == null)
		{
			return;
		}
		for (int i = 0; i < m_localPigs.Length; i++)
		{
			Pig pig = m_localPigs[i];
			if (pig == null || m_eliminated[i] || !m_roundSlots.Contains(i))
			{
				continue;
			}
			Vector3 position = pig.transform.position;
			if (position.y < currentCameraLimits.topLeft.y - currentCameraLimits.size.y ||
				position.x > currentCameraLimits.topLeft.x + currentCameraLimits.size.x * 1.1f ||
				position.x < currentCameraLimits.topLeft.x - currentCameraLimits.size.x * 0.1f)
			{
				ServerEliminate(i, LossReason.LeftTheArena);
			}
		}
	}

	[Server]
	private void ServerCheckRematch()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void BadPiggies.Multiplayer.MultiplayerMatchController::ServerCheckRematch()' called when server was not active");
			return;
		}
		if (!ServerAllWantRematch())
		{
			return;
		}
		Debug.Log($"[Multiplayer] Rematch agreed by every player; tearing down round {m_serverRound}.");
		MultiplayerLobbyPlayer broadcaster = ServerFindBroadcaster();
		if (broadcaster != null)
		{
			broadcaster.RpcPrepareRematch();
		}
		foreach (NetworkConnectionToClient value in NetworkServer.connections.Values)
		{
			if (value != null && !(value.identity == null))
			{
				MultiplayerLobbyPlayer component = value.identity.GetComponent<MultiplayerLobbyPlayer>();
				if (component != null)
				{
					component.ServerSetRematch(wants: false);
				}
			}
		}
		PrepareLocalRematch();
		MultiplayerNetworkManager component2 = GetComponent<MultiplayerNetworkManager>();
		if (component2 == null)
		{
			Debug.LogError("[Multiplayer] Rematch requested but the session object has no MultiplayerNetworkManager.");
			return;
		}
		component2.ServerRestartArena();
	}

	[Server]
	private static bool ServerAllWantRematch()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Boolean BadPiggies.Multiplayer.MultiplayerMatchController::ServerAllWantRematch()' called when server was not active");
			return default(bool);
		}
		int num = 0;
		foreach (NetworkConnectionToClient value in NetworkServer.connections.Values)
		{
			if (value == null || value.identity == null)
			{
				continue;
			}
			MultiplayerLobbyPlayer component = value.identity.GetComponent<MultiplayerLobbyPlayer>();
			if (component == null)
			{
				continue;
			}
			if (!component.WantsRematch)
			{
				return false;
			}
			num++;
		}
		return num >= OnlineNetConstants.MinPlayers;
	}

	public void PrepareLocalRematch()
	{
		ResetForNewMatch();
		NetworkIdentity localPlayer = NetworkClient.localPlayer;
		MultiplayerLobbyPlayer multiplayerLobbyPlayer = ((localPlayer != null) ? localPlayer.GetComponent<MultiplayerLobbyPlayer>() : null);
		if (multiplayerLobbyPlayer != null)
		{
			multiplayerLobbyPlayer.ClearBlueprintSent();
		}
		MultiplayerBuildPhaseUI component = GetComponent<MultiplayerBuildPhaseUI>();
		if (component != null)
		{
			component.ResetToBuilding();
		}
		Debug.Log($"[Multiplayer] Rematch: round {m_round} state cleared, waiting for the arena to reload.");
	}
}
}
