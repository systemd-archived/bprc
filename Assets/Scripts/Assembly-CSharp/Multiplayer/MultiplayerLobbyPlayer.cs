using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Mirror;
using Mirror.RemoteCalls;
using UnityEngine;

namespace BadPiggies.Multiplayer
{
/// <summary>
/// One networked player. Every member of a room is a player: there are no fixed fighter slots — the
/// host is slot 0 and everyone else is 1, 2, 3, … in join order, and each slot owns one machine.
///
/// This type doubles as the session's RPC broadcaster: the match controller is a plain
/// MonoBehaviour, so the round's blueprints, the elimination announcements and the match result all
/// travel through a spawned lobby player (see MultiplayerMatchController.ServerFindBroadcaster).
///
/// The Mirror runtime DLL shipped with this port has no Weaver, so everything the Weaver would have
/// generated (the UserCode_*/InvokeUserCode_* pairs, the RemoteProcedureCalls registrations, the
/// dirty-bit SyncVar serialisation and the function hashes) is written out by hand below. See
/// _codex_work/multiplayer/tools/fix_rpc_hashes.ps1 for the hash generator.
/// </summary>
public class MultiplayerLobbyPlayer : NetworkBehaviour
{
	[SyncVar(hook = "OnSlotChanged")]
	private int m_slot = -1;

	[SyncVar(hook = "OnReadyChanged")]
	private bool m_ready;

	[SyncVar(hook = "OnDisplayNameChanged")]
	private string m_displayName = "Player";

	[SyncVar(hook = "OnInArenaChanged")]
	private bool m_inArena;

	private bool m_blueprintSent;

	[SyncVar(hook = "OnRematchChanged")]
	private bool m_rematch;

	public Action<int, int> _Mirror_SyncVarHookDelegate_m_slot;

	public Action<bool, bool> _Mirror_SyncVarHookDelegate_m_ready;

	public Action<string, string> _Mirror_SyncVarHookDelegate_m_displayName;

	public Action<bool, bool> _Mirror_SyncVarHookDelegate_m_inArena;

	public Action<bool, bool> _Mirror_SyncVarHookDelegate_m_rematch;

	public int Slot => m_slot;

	public bool IsReady => m_ready;

	public string DisplayName => m_displayName;

	public bool InArena => m_inArena;

	public bool BlueprintSent => m_blueprintSent;

	public bool WantsRematch => m_rematch;

	public int Networkm_slot
	{
		get
		{
			return m_slot;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref m_slot, 1uL, _Mirror_SyncVarHookDelegate_m_slot);
		}
	}

	public bool Networkm_ready
	{
		get
		{
			return m_ready;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref m_ready, 2uL, _Mirror_SyncVarHookDelegate_m_ready);
		}
	}

	public string Networkm_displayName
	{
		get
		{
			return m_displayName;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref m_displayName, 4uL, _Mirror_SyncVarHookDelegate_m_displayName);
		}
	}

	public bool Networkm_inArena
	{
		get
		{
			return m_inArena;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref m_inArena, 8uL, _Mirror_SyncVarHookDelegate_m_inArena);
		}
	}

	public bool Networkm_rematch
	{
		get
		{
			return m_rematch;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref m_rematch, 16uL, _Mirror_SyncVarHookDelegate_m_rematch);
		}
	}

	private void Awake()
	{
		UnityEngine.Object.DontDestroyOnLoad(gameObject);
	}

	public override void OnStartLocalPlayer()
	{
		base.OnStartLocalPlayer();
		string newName;
		if (string.IsNullOrWhiteSpace(SystemInfo.deviceName))
		{
			newName = (isServer ? "Host" : "Guest");
		}
		else
		{
			newName = SystemInfo.deviceName;
		}
		CmdSetName(newName);
	}

	[Server]
	public void ServerInitialize(int slot)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void BadPiggies.Multiplayer.MultiplayerLobbyPlayer::ServerInitialize(System.Int32)' called when server was not active");
			return;
		}
		Networkm_slot = slot;
		Networkm_ready = false;
	}

	[Server]
	public void ServerSetReady(bool ready)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void BadPiggies.Multiplayer.MultiplayerLobbyPlayer::ServerSetReady(System.Boolean)' called when server was not active");
			return;
		}
		Networkm_ready = ready;
	}

	[Command]
	public void CmdSetReady(bool ready)
	{
		if (isServer && isClient)
		{
			UserCode_CmdSetReady__Boolean(ready);
			return;
		}
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(ready);
		SendCommandInternal("System.Void BadPiggies.Multiplayer.MultiplayerLobbyPlayer::CmdSetReady(System.Boolean)", 7930, writer, 0);
		NetworkWriterPool.Return(writer);
	}

	[Command]
	public void CmdSetName(string newName)
	{
		if (isServer && isClient)
		{
			UserCode_CmdSetName__String(newName);
			return;
		}
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteString(newName);
		SendCommandInternal("System.Void BadPiggies.Multiplayer.MultiplayerLobbyPlayer::CmdSetName(System.String)", 33996, writer, 0);
		NetworkWriterPool.Return(writer);
	}

	[Command]
	public void CmdSetInArena(bool inArena)
	{
		if (isServer && isClient)
		{
			UserCode_CmdSetInArena__Boolean(inArena);
			return;
		}
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(inArena);
		SendCommandInternal("System.Void BadPiggies.Multiplayer.MultiplayerLobbyPlayer::CmdSetInArena(System.Boolean)", 941, writer, 0);
		NetworkWriterPool.Return(writer);
	}

	public void ToggleReadyLocal()
	{
		if (isLocalPlayer)
		{
			CmdSetReady(!m_ready);
		}
	}

	public bool SubmitLocalBlueprint()
	{
		if (!isLocalPlayer || m_blueprintSent)
		{
			return false;
		}
		MultiplayerMatchController multiplayerMatchController = MultiplayerMatchController.Resolve();
		if (multiplayerMatchController == null)
		{
			Debug.LogError("[Multiplayer] No MultiplayerMatchController on the network manager; cannot submit a blueprint.");
			return false;
		}
		byte[] array = multiplayerMatchController.CaptureLocalBlueprint();
		if (array == null)
		{
			return false;
		}
		m_blueprintSent = true;
		CmdSubmitBlueprint(array, multiplayerMatchController.CurrentRound);
		return true;
	}

	[Command]
	private void CmdSubmitBlueprint(byte[] payload, int round)
	{
		if (isServer && isClient)
		{
			UserCode_CmdSubmitBlueprint__Byte_005B_005D__Int32(payload, round);
			return;
		}
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBytesAndSize(payload);
		writer.WriteVarInt(round);
		SendCommandInternal("System.Void BadPiggies.Multiplayer.MultiplayerLobbyPlayer::CmdSubmitBlueprint(System.Byte[],System.Int32)", 50279, writer, 0);
		NetworkWriterPool.Return(writer);
	}

	public bool WithdrawLocalBlueprint()
	{
		if (!isLocalPlayer || !m_blueprintSent)
		{
			return false;
		}
		m_blueprintSent = false;
		CmdWithdrawBlueprint();
		if (m_ready)
		{
			CmdSetReady(ready: false);
		}
		return true;
	}

	[Command]
	private void CmdWithdrawBlueprint()
	{
		if (isServer && isClient)
		{
			UserCode_CmdWithdrawBlueprint();
			return;
		}
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendCommandInternal("System.Void BadPiggies.Multiplayer.MultiplayerLobbyPlayer::CmdWithdrawBlueprint()", 50891, writer, 0);
		NetworkWriterPool.Return(writer);
	}

	/// <summary>
	/// Hands one player's blueprint to every peer. Sent once per slot, in slot order, right before
	/// <see cref="RpcBeginBattle"/>: keeping one blueprint per message means the message never grows
	/// with the room size (an eight-player match would otherwise push one large message through the
	/// transport's size limit).
	/// </summary>
	[ClientRpc]
	public void RpcStoreBlueprint(byte slot, byte[] payload)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		NetworkWriterExtensions.WriteByte(writer, slot);
		writer.WriteBytesAndSize(payload);
		SendRPCInternal("System.Void BadPiggies.Multiplayer.MultiplayerLobbyPlayer::RpcStoreBlueprint(System.Byte,System.Byte[])", 47695, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	/// <summary>
	/// Tells every peer which slots are in the round (in lane order) and to assemble them.
	/// </summary>
	[ClientRpc]
	public void RpcBeginBattle(byte[] slots)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBytesAndSize(slots);
		SendRPCInternal("System.Void BadPiggies.Multiplayer.MultiplayerLobbyPlayer::RpcBeginBattle(System.Byte[])", 56433, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[Command]
	public void CmdActivatePart(int partIndex, float x, float y)
	{
		if (isServer && isClient)
		{
			UserCode_CmdActivatePart__Int32__Single__Single(partIndex, x, y);
			return;
		}
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarInt(partIndex);
		writer.WriteFloat(x);
		writer.WriteFloat(y);
		SendCommandInternal("System.Void BadPiggies.Multiplayer.MultiplayerLobbyPlayer::CmdActivatePart(System.Int32,System.Single,System.Single)", 39157, writer, 0);
		NetworkWriterPool.Return(writer);
	}

	[Command]
	public void CmdActivateBulk(byte kind, int partType, int direction)
	{
		if (isServer && isClient)
		{
			UserCode_CmdActivateBulk__Byte__Int32__Int32(kind, partType, direction);
			return;
		}
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		NetworkWriterExtensions.WriteByte(writer, kind);
		writer.WriteVarInt(partType);
		writer.WriteVarInt(direction);
		SendCommandInternal("System.Void BadPiggies.Multiplayer.MultiplayerLobbyPlayer::CmdActivateBulk(System.Byte,System.Int32,System.Int32)", 42945, writer, 0);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	public void ServerSetRematch(bool wants)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void BadPiggies.Multiplayer.MultiplayerLobbyPlayer::ServerSetRematch(System.Boolean)' called when server was not active");
			return;
		}
		Networkm_rematch = wants;
	}

	[Command]
	public void CmdRequestRematch()
	{
		if (isServer && isClient)
		{
			UserCode_CmdRequestRematch();
			return;
		}
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendCommandInternal("System.Void BadPiggies.Multiplayer.MultiplayerLobbyPlayer::CmdRequestRematch()", 45042, writer, 0);
		NetworkWriterPool.Return(writer);
	}

	public void RequestRematchLocal()
	{
		if (isLocalPlayer && !m_rematch)
		{
			CmdRequestRematch();
		}
	}

	/// <summary>Announces that one player's machine is out of the round.</summary>
	[ClientRpc]
	public void RpcElimination(byte slot, byte reason)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		NetworkWriterExtensions.WriteByte(writer, slot);
		NetworkWriterExtensions.WriteByte(writer, reason);
		SendRPCInternal("System.Void BadPiggies.Multiplayer.MultiplayerLobbyPlayer::RpcElimination(System.Byte,System.Byte)", 64100, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	/// <summary>Announces the end of the round. <paramref name="winner"/> is 255 for a draw.</summary>
	[ClientRpc]
	public void RpcMatchOver(byte winner)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		NetworkWriterExtensions.WriteByte(writer, winner);
		SendRPCInternal("System.Void BadPiggies.Multiplayer.MultiplayerLobbyPlayer::RpcMatchOver(System.Byte)", 34571, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	public void RpcPrepareRematch()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void BadPiggies.Multiplayer.MultiplayerLobbyPlayer::RpcPrepareRematch()", 55319, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public void ClearBlueprintSent()
	{
		m_blueprintSent = false;
	}

	public static void CollectInto(List<MultiplayerLobbyPlayer> results)
	{
		results.Clear();
		foreach (KeyValuePair<uint, NetworkIdentity> item in NetworkClient.spawned)
		{
			NetworkIdentity value = item.Value;
			if (!(value == null))
			{
				MultiplayerLobbyPlayer component = value.GetComponent<MultiplayerLobbyPlayer>();
				if (component != null)
				{
					results.Add(component);
				}
			}
		}
		results.Sort((MultiplayerLobbyPlayer a, MultiplayerLobbyPlayer b) => a.Slot.CompareTo(b.Slot));
	}

	private void OnSlotChanged(int oldValue, int newValue)
	{
	}

	private void OnReadyChanged(bool oldValue, bool newValue)
	{
	}

	private void OnDisplayNameChanged(string oldValue, string newValue)
	{
	}

	private void OnInArenaChanged(bool oldValue, bool newValue)
	{
	}

	private void OnRematchChanged(bool oldValue, bool newValue)
	{
	}

	public MultiplayerLobbyPlayer()
	{
		_Mirror_SyncVarHookDelegate_m_slot = OnSlotChanged;
		_Mirror_SyncVarHookDelegate_m_ready = OnReadyChanged;
		_Mirror_SyncVarHookDelegate_m_displayName = OnDisplayNameChanged;
		_Mirror_SyncVarHookDelegate_m_inArena = OnInArenaChanged;
		_Mirror_SyncVarHookDelegate_m_rematch = OnRematchChanged;
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_CmdSetReady__Boolean(bool ready)
	{
		// Any member may ready up at any time: the round starts when everybody is ready and the
		// room holds at least the minimum number of players (MultiplayerNetworkManager checks that).
		Networkm_ready = ready;
	}

	protected static void InvokeUserCode_CmdSetReady__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdSetReady called on client.");
		}
		else
		{
			((MultiplayerLobbyPlayer)obj).UserCode_CmdSetReady__Boolean(reader.ReadBool());
		}
	}

	protected void UserCode_CmdSetName__String(string newName)
	{
		if (!string.IsNullOrWhiteSpace(newName))
		{
			newName = newName.Trim();
			if (newName.Length > 24)
			{
				newName = newName.Substring(0, 24);
			}
			Networkm_displayName = newName;
		}
	}

	protected static void InvokeUserCode_CmdSetName__String(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdSetName called on client.");
		}
		else
		{
			((MultiplayerLobbyPlayer)obj).UserCode_CmdSetName__String(reader.ReadString());
		}
	}

	protected void UserCode_CmdSetInArena__Boolean(bool inArena)
	{
		Networkm_inArena = inArena;
	}

	protected static void InvokeUserCode_CmdSetInArena__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdSetInArena called on client.");
		}
		else
		{
			((MultiplayerLobbyPlayer)obj).UserCode_CmdSetInArena__Boolean(reader.ReadBool());
		}
	}

	protected void UserCode_CmdSubmitBlueprint__Byte_005B_005D__Int32(byte[] payload, int round)
	{
		MultiplayerMatchController multiplayerMatchController = MultiplayerMatchController.Resolve();
		if (multiplayerMatchController == null)
		{
			Debug.LogError("[Multiplayer] Blueprint arrived but the host has no MultiplayerMatchController.");
		}
		else
		{
			multiplayerMatchController.ServerSubmitBlueprint(m_slot, payload, round);
		}
	}

	protected static void InvokeUserCode_CmdSubmitBlueprint__Byte_005B_005D__Int32(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdSubmitBlueprint called on client.");
		}
		else
		{
			((MultiplayerLobbyPlayer)obj).UserCode_CmdSubmitBlueprint__Byte_005B_005D__Int32(reader.ReadBytesAndSize(), reader.ReadVarInt());
		}
	}

	protected void UserCode_CmdWithdrawBlueprint()
	{
		MultiplayerMatchController multiplayerMatchController = MultiplayerMatchController.Resolve();
		if (!(multiplayerMatchController == null))
		{
			multiplayerMatchController.ServerWithdrawBlueprint(m_slot);
		}
	}

	protected static void InvokeUserCode_CmdWithdrawBlueprint(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdWithdrawBlueprint called on client.");
		}
		else
		{
			((MultiplayerLobbyPlayer)obj).UserCode_CmdWithdrawBlueprint();
		}
	}

	protected void UserCode_RpcStoreBlueprint__Byte__Byte_005B_005D(byte slot, byte[] payload)
	{
		MultiplayerMatchController multiplayerMatchController = MultiplayerMatchController.Resolve();
		if (multiplayerMatchController == null)
		{
			Debug.LogError("[Multiplayer] A blueprint arrived but this peer has no MultiplayerMatchController.");
		}
		else
		{
			multiplayerMatchController.StoreRoundBlueprint(slot, payload);
		}
	}

	protected static void InvokeUserCode_RpcStoreBlueprint__Byte__Byte_005B_005D(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcStoreBlueprint called on server.");
		}
		else
		{
			((MultiplayerLobbyPlayer)obj).UserCode_RpcStoreBlueprint__Byte__Byte_005B_005D(NetworkReaderExtensions.ReadByte(reader), reader.ReadBytesAndSize());
		}
	}

	protected void UserCode_RpcBeginBattle__Byte_005B_005D(byte[] slots)
	{
		MultiplayerMatchController multiplayerMatchController = MultiplayerMatchController.Resolve();
		if (multiplayerMatchController == null)
		{
			Debug.LogError("[Multiplayer] Battle start arrived but this peer has no MultiplayerMatchController.");
		}
		else
		{
			multiplayerMatchController.BeginLocalBattle(slots);
		}
	}

	protected static void InvokeUserCode_RpcBeginBattle__Byte_005B_005D(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcBeginBattle called on server.");
		}
		else
		{
			((MultiplayerLobbyPlayer)obj).UserCode_RpcBeginBattle__Byte_005B_005D(reader.ReadBytesAndSize());
		}
	}

	protected void UserCode_CmdActivatePart__Int32__Single__Single(int partIndex, float x, float y)
	{
		MultiplayerMatchController multiplayerMatchController = MultiplayerMatchController.Resolve();
		if (!(multiplayerMatchController == null))
		{
			multiplayerMatchController.ServerApplyTouch(m_slot, partIndex, x, y);
		}
	}

	protected static void InvokeUserCode_CmdActivatePart__Int32__Single__Single(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdActivatePart called on client.");
		}
		else
		{
			((MultiplayerLobbyPlayer)obj).UserCode_CmdActivatePart__Int32__Single__Single(reader.ReadVarInt(), reader.ReadFloat(), reader.ReadFloat());
		}
	}

	protected void UserCode_CmdActivateBulk__Byte__Int32__Int32(byte kind, int partType, int direction)
	{
		MultiplayerMatchController multiplayerMatchController = MultiplayerMatchController.Resolve();
		if (!(multiplayerMatchController == null))
		{
			multiplayerMatchController.ServerApplyBulk(m_slot, kind, partType, direction);
		}
	}

	protected static void InvokeUserCode_CmdActivateBulk__Byte__Int32__Int32(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdActivateBulk called on client.");
		}
		else
		{
			((MultiplayerLobbyPlayer)obj).UserCode_CmdActivateBulk__Byte__Int32__Int32(NetworkReaderExtensions.ReadByte(reader), reader.ReadVarInt(), reader.ReadVarInt());
		}
	}

	protected void UserCode_CmdRequestRematch()
	{
		MultiplayerMatchController multiplayerMatchController = MultiplayerMatchController.Resolve();
		if (!(multiplayerMatchController == null) && multiplayerMatchController.HasResult)
		{
			Networkm_rematch = true;
		}
	}

	protected static void InvokeUserCode_CmdRequestRematch(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdRequestRematch called on client.");
		}
		else
		{
			((MultiplayerLobbyPlayer)obj).UserCode_CmdRequestRematch();
		}
	}

	protected void UserCode_RpcElimination__Byte__Byte(byte slot, byte reason)
	{
		MultiplayerMatchController multiplayerMatchController = MultiplayerMatchController.Resolve();
		if (multiplayerMatchController == null)
		{
			Debug.LogError("[Multiplayer] An elimination arrived but this peer has no MultiplayerMatchController.");
		}
		else
		{
			multiplayerMatchController.ApplyElimination(slot, reason);
		}
	}

	protected static void InvokeUserCode_RpcElimination__Byte__Byte(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcElimination called on server.");
		}
		else
		{
			((MultiplayerLobbyPlayer)obj).UserCode_RpcElimination__Byte__Byte(NetworkReaderExtensions.ReadByte(reader), NetworkReaderExtensions.ReadByte(reader));
		}
	}

	protected void UserCode_RpcMatchOver__Byte(byte winner)
	{
		MultiplayerMatchController multiplayerMatchController = MultiplayerMatchController.Resolve();
		if (multiplayerMatchController == null)
		{
			Debug.LogError("[Multiplayer] A match result arrived but this peer has no MultiplayerMatchController.");
		}
		else
		{
			multiplayerMatchController.ApplyMatchOver(winner);
		}
	}

	protected static void InvokeUserCode_RpcMatchOver__Byte(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcMatchOver called on server.");
		}
		else
		{
			((MultiplayerLobbyPlayer)obj).UserCode_RpcMatchOver__Byte(NetworkReaderExtensions.ReadByte(reader));
		}
	}

	protected void UserCode_RpcPrepareRematch()
	{
		MultiplayerMatchController multiplayerMatchController = MultiplayerMatchController.Resolve();
		if (!(multiplayerMatchController == null))
		{
			multiplayerMatchController.PrepareLocalRematch();
		}
	}

	protected static void InvokeUserCode_RpcPrepareRematch(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcPrepareRematch called on server.");
		}
		else
		{
			((MultiplayerLobbyPlayer)obj).UserCode_RpcPrepareRematch();
		}
	}

	static MultiplayerLobbyPlayer()
	{
		RemoteProcedureCalls.RegisterCommand(typeof(MultiplayerLobbyPlayer), "System.Void BadPiggies.Multiplayer.MultiplayerLobbyPlayer::CmdSetReady(System.Boolean)", InvokeUserCode_CmdSetReady__Boolean, requiresAuthority: true);
		RemoteProcedureCalls.RegisterCommand(typeof(MultiplayerLobbyPlayer), "System.Void BadPiggies.Multiplayer.MultiplayerLobbyPlayer::CmdSetName(System.String)", InvokeUserCode_CmdSetName__String, requiresAuthority: true);
		RemoteProcedureCalls.RegisterCommand(typeof(MultiplayerLobbyPlayer), "System.Void BadPiggies.Multiplayer.MultiplayerLobbyPlayer::CmdSetInArena(System.Boolean)", InvokeUserCode_CmdSetInArena__Boolean, requiresAuthority: true);
		RemoteProcedureCalls.RegisterCommand(typeof(MultiplayerLobbyPlayer), "System.Void BadPiggies.Multiplayer.MultiplayerLobbyPlayer::CmdSubmitBlueprint(System.Byte[],System.Int32)", InvokeUserCode_CmdSubmitBlueprint__Byte_005B_005D__Int32, requiresAuthority: true);
		RemoteProcedureCalls.RegisterCommand(typeof(MultiplayerLobbyPlayer), "System.Void BadPiggies.Multiplayer.MultiplayerLobbyPlayer::CmdWithdrawBlueprint()", InvokeUserCode_CmdWithdrawBlueprint, requiresAuthority: true);
		RemoteProcedureCalls.RegisterCommand(typeof(MultiplayerLobbyPlayer), "System.Void BadPiggies.Multiplayer.MultiplayerLobbyPlayer::CmdActivatePart(System.Int32,System.Single,System.Single)", InvokeUserCode_CmdActivatePart__Int32__Single__Single, requiresAuthority: true);
		RemoteProcedureCalls.RegisterCommand(typeof(MultiplayerLobbyPlayer), "System.Void BadPiggies.Multiplayer.MultiplayerLobbyPlayer::CmdActivateBulk(System.Byte,System.Int32,System.Int32)", InvokeUserCode_CmdActivateBulk__Byte__Int32__Int32, requiresAuthority: true);
		RemoteProcedureCalls.RegisterCommand(typeof(MultiplayerLobbyPlayer), "System.Void BadPiggies.Multiplayer.MultiplayerLobbyPlayer::CmdRequestRematch()", InvokeUserCode_CmdRequestRematch, requiresAuthority: true);
		RemoteProcedureCalls.RegisterRpc(typeof(MultiplayerLobbyPlayer), "System.Void BadPiggies.Multiplayer.MultiplayerLobbyPlayer::RpcStoreBlueprint(System.Byte,System.Byte[])", InvokeUserCode_RpcStoreBlueprint__Byte__Byte_005B_005D);
		RemoteProcedureCalls.RegisterRpc(typeof(MultiplayerLobbyPlayer), "System.Void BadPiggies.Multiplayer.MultiplayerLobbyPlayer::RpcBeginBattle(System.Byte[])", InvokeUserCode_RpcBeginBattle__Byte_005B_005D);
		RemoteProcedureCalls.RegisterRpc(typeof(MultiplayerLobbyPlayer), "System.Void BadPiggies.Multiplayer.MultiplayerLobbyPlayer::RpcElimination(System.Byte,System.Byte)", InvokeUserCode_RpcElimination__Byte__Byte);
		RemoteProcedureCalls.RegisterRpc(typeof(MultiplayerLobbyPlayer), "System.Void BadPiggies.Multiplayer.MultiplayerLobbyPlayer::RpcMatchOver(System.Byte)", InvokeUserCode_RpcMatchOver__Byte);
		RemoteProcedureCalls.RegisterRpc(typeof(MultiplayerLobbyPlayer), "System.Void BadPiggies.Multiplayer.MultiplayerLobbyPlayer::RpcPrepareRematch()", InvokeUserCode_RpcPrepareRematch);
	}

	protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
	{
		base.SerializeSyncVars(writer, forceAll);
		if (forceAll)
		{
			writer.WriteVarInt(m_slot);
			writer.WriteBool(m_ready);
			writer.WriteString(m_displayName);
			writer.WriteBool(m_inArena);
			writer.WriteBool(m_rematch);
			return;
		}
		writer.WriteVarULong(syncVarDirtyBits);
		if ((syncVarDirtyBits & 1L) != 0L)
		{
			writer.WriteVarInt(m_slot);
		}
		if ((syncVarDirtyBits & 2L) != 0L)
		{
			writer.WriteBool(m_ready);
		}
		if ((syncVarDirtyBits & 4L) != 0L)
		{
			writer.WriteString(m_displayName);
		}
		if ((syncVarDirtyBits & 8L) != 0L)
		{
			writer.WriteBool(m_inArena);
		}
		if ((syncVarDirtyBits & 0x10L) != 0L)
		{
			writer.WriteBool(m_rematch);
		}
	}

	protected override void DeserializeSyncVars(NetworkReader reader, bool initialState)
	{
		base.DeserializeSyncVars(reader, initialState);
		if (initialState)
		{
			GeneratedSyncVarDeserialize(ref m_slot, _Mirror_SyncVarHookDelegate_m_slot, reader.ReadVarInt());
			GeneratedSyncVarDeserialize(ref m_ready, _Mirror_SyncVarHookDelegate_m_ready, reader.ReadBool());
			GeneratedSyncVarDeserialize(ref m_displayName, _Mirror_SyncVarHookDelegate_m_displayName, reader.ReadString());
			GeneratedSyncVarDeserialize(ref m_inArena, _Mirror_SyncVarHookDelegate_m_inArena, reader.ReadBool());
			GeneratedSyncVarDeserialize(ref m_rematch, _Mirror_SyncVarHookDelegate_m_rematch, reader.ReadBool());
			return;
		}
		long num = (long)reader.ReadVarULong();
		if ((num & 1L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref m_slot, _Mirror_SyncVarHookDelegate_m_slot, reader.ReadVarInt());
		}
		if ((num & 2L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref m_ready, _Mirror_SyncVarHookDelegate_m_ready, reader.ReadBool());
		}
		if ((num & 4L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref m_displayName, _Mirror_SyncVarHookDelegate_m_displayName, reader.ReadString());
		}
		if ((num & 8L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref m_inArena, _Mirror_SyncVarHookDelegate_m_inArena, reader.ReadBool());
		}
		if ((num & 0x10L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref m_rematch, _Mirror_SyncVarHookDelegate_m_rematch, reader.ReadBool());
		}
	}
}
}
