using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Mirror;
using Mirror.RemoteCalls;
using UnityEngine;

namespace BadPiggies.Multiplayer
{
public class MultiplayerMachineAnchor : NetworkBehaviour
{
	private enum OneOff : byte
	{
		PartBroke,
		PilotEjected,
		Explosion,
		PartRemoved
	}

	[SyncVar]
	private int m_slot = -1;

	private Contraption m_machine;

	private readonly List<BasePart> m_parts = new List<BasePart>();

	private readonly List<Transform> m_nodes = new List<Transform>();

	private readonly List<BasePart> m_nodeOwners = new List<BasePart>();

	private readonly List<bool> m_flipCoset = new List<bool>();

	private readonly List<bool> m_effectsLatch = new List<bool>();

	private readonly List<int> m_jointCount = new List<int>();

	private readonly List<bool> m_activeLatch = new List<bool>();

	private readonly List<bool> m_oneOffLatch = new List<bool>();

	private readonly List<bool> m_removedLatch = new List<bool>();

	private int m_pigIndex = -1;

	private bool m_pigWasAttached;

	private bool m_ejected;

	private ushort m_tick;

	private readonly MultiplayerPoseBuffer m_poses = new MultiplayerPoseBuffer();

	private float m_nextSendTime;

	private const float SendInterval = 0.05f;

	private const int OneOffPayloadBytes = 3;

	private static readonly List<Joint> s_jointScratch;

	public int Slot => m_slot;

	public int PartCount => m_parts.Count;

	public bool IsPilotEjected => m_ejected;

	public int Networkm_slot
	{
		get
		{
			return m_slot;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref m_slot, 1uL, null);
		}
	}

	public static event Action<int> PilotEjected;

	[Server]
	public void ServerBind(Contraption machine, int slot)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void BadPiggies.Multiplayer.MultiplayerMachineAnchor::ServerBind(Contraption,System.Int32)' called when server was not active");
			return;
		}
		Networkm_slot = slot;
		BindMachine(machine);
	}

	public override void OnStartClient()
	{
		if (!isServer)
		{
			MultiplayerMatchController multiplayerMatchController = MultiplayerMatchController.Resolve();
			Contraption contraption = ((multiplayerMatchController != null) ? multiplayerMatchController.GetMachine(m_slot) : null);
			if (contraption == null)
			{
				Debug.LogError($"[Multiplayer] Anchor for slot {m_slot} arrived with no local machine to bind to.");
				return;
			}
			BindMachine(contraption);
			Debug.Log($"[Multiplayer] Anchor bound to slot {m_slot}'s machine ({m_parts.Count} parts).");
		}
	}

	private void BindMachine(Contraption machine)
	{
		m_machine = machine;
		m_parts.Clear();
		m_parts.AddRange(MultiplayerBattleAssembler.OrderedParts(machine));
		if (m_machine != null)
		{
			transform.position = m_machine.transform.position;
		}
		m_nodes.Clear();
		m_nodeOwners.Clear();
		for (int i = 0; i < m_parts.Count; i++)
		{
			m_nodes.Add((m_parts[i] != null) ? m_parts[i].transform : null);
			m_nodeOwners.Add(m_parts[i]);
		}
		int count = m_nodes.Count;
		MultiplayerAttachments.CollectInto(m_parts, m_nodes);
		while (m_nodeOwners.Count < m_nodes.Count)
		{
			m_nodeOwners.Add(null);
		}
		m_flipCoset.Clear();
		for (int j = 0; j < m_nodes.Count; j++)
		{
			m_flipCoset.Add(m_nodes[j] != null && MultiplayerSnapshot.IsFlipCoset(m_nodes[j].rotation));
		}
		m_effectsLatch.Clear();
		m_jointCount.Clear();
		m_activeLatch.Clear();
		m_oneOffLatch.Clear();
		m_removedLatch.Clear();
		m_pigIndex = -1;
		m_pigWasAttached = false;
		m_ejected = false;
		for (int k = 0; k < m_parts.Count; k++)
		{
			BasePart basePart = m_parts[k];
			m_effectsLatch.Add(item: false);
			m_jointCount.Add(LiveJointCount(basePart));
			m_activeLatch.Add(item: false);
			m_oneOffLatch.Add(item: false);
			m_removedLatch.Add(item: false);
			if (basePart is Pig)
			{
				m_pigIndex = k;
				m_pigWasAttached = basePart.enclosedInto != null || m_jointCount[k] > 0;
			}
		}
		m_poses.Resize(m_nodes.Count);
		if (m_nodes.Count > count)
		{
			Debug.Log($"[Multiplayer] Slot {m_slot} streams {count} part(s) plus " + $"{m_nodes.Count - count} runtime attachment(s).");
		}
		int num = ((m_nodes.Count > 0) ? ((m_nodes.Count - 1) / 100 * 100) : 0);
		if (num > 255)
		{
			Debug.LogError($"[Multiplayer] Slot {m_slot} has {m_nodes.Count} streamed bodies, which needs a " + $"chunk start of {num} — the wire format only carries one byte for it. " + "Poses beyond the first 256 will be applied to the wrong bodies.");
		}
		MultiplayerMatchController multiplayerMatchController = MultiplayerMatchController.Resolve();
		if (multiplayerMatchController != null)
		{
			multiplayerMatchController.RegisterAnchor(m_slot, this);
		}
	}

	public int IndexOf(BasePart part)
	{
		if (part == null)
		{
			return -1;
		}
		for (int i = 0; i < m_parts.Count; i++)
		{
			if (m_parts[i] == part)
			{
				return i;
			}
		}
		return -1;
	}

	public BasePart PartAt(int index)
	{
		if (index < 0 || index >= m_parts.Count)
		{
			return null;
		}
		return m_parts[index];
	}

	private void LateUpdate()
	{
		if (!(m_machine == null))
		{
			if (isServer)
			{
				ServerPublish();
			}
			else
			{
				ClientReplay();
			}
		}
	}

	[Server]
	private void ServerPublish()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void BadPiggies.Multiplayer.MultiplayerMachineAnchor::ServerPublish()' called when server was not active");
		}
		else if (!(Time.time < m_nextSendTime))
		{
			m_nextSendTime = Time.time + 0.05f;
			m_tick++;
			ServerScanOneOffs();
			transform.position = ResolveOrigin();
			for (int i = 0; i < m_nodes.Count; i += 100)
			{
				int count = Mathf.Min(100, m_nodes.Count - i);
				byte[] array = MultiplayerSnapshot.Write(m_nodes, m_nodeOwners, i, count, m_tick, m_flipCoset);
				RpcSnapshot(array);
				MultiplayerTraffic.CountSent(array.Length);
			}
		}
	}

	[Server]
	private Vector3 ResolveOrigin()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'UnityEngine.Vector3 BadPiggies.Multiplayer.MultiplayerMachineAnchor::ResolveOrigin()' called when server was not active");
			return default;
		}
		bool flag = false;
		float num = 0f;
		float num2 = 0f;
		float num3 = 0f;
		float num4 = 0f;
		for (int i = 0; i < m_parts.Count; i++)
		{
			BasePart basePart = m_parts[i];
			if (basePart == null)
			{
				continue;
			}
			Vector3 position = basePart.transform.position;
			if (!flag)
			{
				num = (num2 = position.x);
				num3 = (num4 = position.y);
				flag = true;
				continue;
			}
			if (position.x < num)
			{
				num = position.x;
			}
			else if (position.x > num2)
			{
				num2 = position.x;
			}
			if (position.y < num3)
			{
				num3 = position.y;
			}
			else if (position.y > num4)
			{
				num4 = position.y;
			}
		}
		if (!flag)
		{
			if (!(m_machine != null))
			{
				return transform.position;
			}
			return m_machine.transform.position;
		}
		return new Vector3((num + num2) * 0.5f, (num3 + num4) * 0.5f, 0f);
	}

	private void ClientReplay()
	{
		m_poses.Advance(Time.deltaTime);
		if (!m_poses.HasPose)
		{
			return;
		}
		for (int i = 0; i < m_nodes.Count; i++)
		{
			Transform transform = m_nodes[i];
			if (!(transform == null) && m_poses.TryGetPose(i, out var position, out var angleDegrees))
			{
				transform.position = position;
				transform.rotation = MultiplayerSnapshot.FromAngle(angleDegrees, m_flipCoset[i]);
			}
		}
		ApplyLocalEffects();
	}

	private void ApplyLocalEffects()
	{
		for (int i = 0; i < m_parts.Count; i++)
		{
			BasePart basePart = m_parts[i];
			if (!(basePart == null))
			{
				bool flag = m_poses.ActiveAt(i);
				if (flag != m_effectsLatch[i])
				{
					m_effectsLatch[i] = flag;
					basePart.SetEnabled(flag);
				}
			}
		}
	}

	[ClientRpc(channel = 1)]
	private void RpcSnapshot(byte[] blob)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBytesAndSize(blob);
		SendRPCInternal("System.Void BadPiggies.Multiplayer.MultiplayerMachineAnchor::RpcSnapshot(System.Byte[])", 19187, writer, 1, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	private void ServerScanOneOffs()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void BadPiggies.Multiplayer.MultiplayerMachineAnchor::ServerScanOneOffs()' called when server was not active");
			return;
		}
		for (int i = 0; i < m_parts.Count; i++)
		{
			BasePart basePart = m_parts[i];
			bool flag = basePart == null || (m_machine != null && !m_machine.Parts.Contains(basePart));
			if (((basePart != null) & flag) && !m_oneOffLatch[i] && basePart is TNT && !(basePart is Rocket))
			{
				ServerFireOneOff(OneOff.Explosion, i);
			}
			if (flag && !m_removedLatch[i])
			{
				m_removedLatch[i] = true;
				ServerFireOneOff(OneOff.PartRemoved, i);
			}
			if (!(basePart == null))
			{
				int num = LiveJointCount(basePart);
				if (num < m_jointCount[i])
				{
					ServerFireOneOff(OneOff.PartBroke, i);
				}
				m_jointCount[i] = num;
				bool flag2 = basePart.IsEnabled();
				if (flag2 != m_activeLatch[i])
				{
					m_activeLatch[i] = flag2;
					ServerCheckRocket(i, basePart, flag2);
				}
			}
		}
		ServerCheckEjection();
	}

	private static int LiveJointCount(BasePart part)
	{
		if (part == null)
		{
			return 0;
		}
		part.GetComponents(s_jointScratch);
		return s_jointScratch.Count;
	}

	[Server]
	private void ServerCheckRocket(int index, BasePart part, bool active)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void BadPiggies.Multiplayer.MultiplayerMachineAnchor::ServerCheckRocket(System.Int32,BasePart,System.Boolean)' called when server was not active");
			return;
		}
		Rocket rocket = part as Rocket;
		if (!(rocket == null) && rocket.m_explodes && (INSettings.GetBool(INFeature.SwitchableCokeSodaRocket) ? active : (!active)))
		{
			ServerFireOneOff(OneOff.Explosion, index);
		}
	}

	[Server]
	private void ServerCheckEjection()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void BadPiggies.Multiplayer.MultiplayerMachineAnchor::ServerCheckEjection()' called when server was not active");
		}
		else if (!m_ejected && m_pigWasAttached && m_pigIndex >= 0)
		{
			BasePart basePart = PartAt(m_pigIndex);
			if (basePart == null || (basePart.enclosedInto == null && m_jointCount[m_pigIndex] == 0))
			{
				ServerFireOneOff(OneOff.PilotEjected, m_pigIndex);
			}
		}
	}

	[Server]
	private void ServerFireOneOff(OneOff kind, int index)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void BadPiggies.Multiplayer.MultiplayerMachineAnchor::ServerFireOneOff(BadPiggies.Multiplayer.MultiplayerMachineAnchor/OneOff,System.Int32)' called when server was not active");
			return;
		}
		if (kind != OneOff.PartBroke && kind != OneOff.PartRemoved && index >= 0 && index < m_oneOffLatch.Count)
		{
			if (m_oneOffLatch[index])
			{
				return;
			}
			m_oneOffLatch[index] = true;
		}
		RpcOneOff((byte)kind, (ushort)Mathf.Max(index, 0));
		MultiplayerTraffic.CountEventSent(3);
		HandleOneOff(kind, index, replayEffects: false);
	}

	[ClientRpc]
	private void RpcOneOff(byte kind, ushort index)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		NetworkWriterExtensions.WriteByte(writer, kind);
		writer.WriteUShort(index);
		SendRPCInternal("System.Void BadPiggies.Multiplayer.MultiplayerMachineAnchor::RpcOneOff(System.Byte,System.UInt16)", 31063, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void HandleOneOff(OneOff kind, int index, bool replayEffects)
	{
		BasePart part = PartAt(index);
		switch (kind)
		{
		case OneOff.PartBroke:
			if (replayEffects)
			{
				PlayBreakEffect(part);
			}
			break;
		case OneOff.Explosion:
			if (replayEffects)
			{
				PlayExplosionEffect(part);
			}
			break;
		case OneOff.PartRemoved:
			if (replayEffects)
			{
				RetirePart(index);
			}
			break;
		case OneOff.PilotEjected:
			if (!m_ejected)
			{
				m_ejected = true;
				Debug.Log($"[Multiplayer] Pilot of slot {m_slot} left the machine.");
				PilotEjected?.Invoke(m_slot);
			}
			break;
		}
	}

	private void RetirePart(int index)
	{
		if (index < 0 || index >= m_parts.Count)
		{
			return;
		}
		BasePart basePart = m_parts[index];
		if (basePart != null)
		{
			basePart.PlayRemovalEffect();
			if (m_machine != null)
			{
				m_machine.RemovePart(basePart);
			}
			UnityEngine.Object.Destroy(basePart.gameObject);
		}
		m_parts[index] = null;
		if (index < m_nodes.Count)
		{
			m_nodes[index] = null;
		}
		if (index < m_nodeOwners.Count)
		{
			m_nodeOwners[index] = null;
		}
	}

	private static void PlayBreakEffect(BasePart part)
	{
		if (!(part == null) && !(WPFMonoBehaviour.effectManager == null) && !(WPFMonoBehaviour.gameData == null))
		{
			part.PlayBreakAudio(part);
			GameObject sprite = ((part.m_partType != BasePart.PartType.WoodenFrame && part.m_partType != BasePart.PartType.Pig) ? WPFMonoBehaviour.gameData.m_snapSprite : WPFMonoBehaviour.gameData.m_krakSprite);
			WPFMonoBehaviour.effectManager.ShowBreakEffect(sprite, part.transform.position + new Vector3(0f, 0f, -10f), Quaternion.AngleAxis(UnityEngine.Random.Range(-30f, 30f), Vector3.forward));
		}
	}

	private static void PlayExplosionEffect(BasePart part)
	{
		if (!(part == null) && !(WPFMonoBehaviour.effectManager == null))
		{
			Vector3 position = part.transform.position;
			GameObject gameObject = (part as Rocket)?.m_smokeCloud ?? (part as TNT)?.smokeCloud;
			if (gameObject != null)
			{
				WPFMonoBehaviour.effectManager.CreateParticles(gameObject, position - Vector3.forward * 5f, force: true);
			}
			if (WPFMonoBehaviour.gameData != null && Singleton<AudioManager>.IsInstantiated())
			{
				Singleton<AudioManager>.Instance.SpawnOneShotEffect(WPFMonoBehaviour.gameData.commonAudioCollection.tntExplosion, position);
			}
		}
	}

	public static bool AnyMachineSpawned()
	{
		foreach (KeyValuePair<uint, NetworkIdentity> item in NetworkClient.spawned)
		{
			NetworkIdentity value = item.Value;
			if (value != null && value.GetComponent<MultiplayerMachineAnchor>() != null)
			{
				return true;
			}
		}
		return false;
	}

	static MultiplayerMachineAnchor()
	{
		s_jointScratch = new List<Joint>();
		RemoteProcedureCalls.RegisterRpc(typeof(MultiplayerMachineAnchor), "System.Void BadPiggies.Multiplayer.MultiplayerMachineAnchor::RpcSnapshot(System.Byte[])", InvokeUserCode_RpcSnapshot__Byte_005B_005D);
		RemoteProcedureCalls.RegisterRpc(typeof(MultiplayerMachineAnchor), "System.Void BadPiggies.Multiplayer.MultiplayerMachineAnchor::RpcOneOff(System.Byte,System.UInt16)", InvokeUserCode_RpcOneOff__Byte__UInt16);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcSnapshot__Byte_005B_005D(byte[] blob)
	{
		if (!isServer)
		{
			MultiplayerTraffic.CountReceived(blob?.Length ?? 0);
			if (MultiplayerSnapshot.ReadHeader(blob, out var start, out var count, out var tick))
			{
				m_poses.Write(blob, start, count, tick);
			}
		}
	}

	protected static void InvokeUserCode_RpcSnapshot__Byte_005B_005D(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSnapshot called on server.");
		}
		else
		{
			((MultiplayerMachineAnchor)obj).UserCode_RpcSnapshot__Byte_005B_005D(reader.ReadBytesAndSize());
		}
	}

	protected void UserCode_RpcOneOff__Byte__UInt16(byte kind, ushort index)
	{
		if (!isServer)
		{
			MultiplayerTraffic.CountEventReceived(3);
			HandleOneOff((OneOff)kind, index, replayEffects: true);
		}
	}

	protected static void InvokeUserCode_RpcOneOff__Byte__UInt16(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcOneOff called on server.");
		}
		else
		{
			((MultiplayerMachineAnchor)obj).UserCode_RpcOneOff__Byte__UInt16(NetworkReaderExtensions.ReadByte(reader), reader.ReadUShort());
		}
	}

	protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
	{
		base.SerializeSyncVars(writer, forceAll);
		if (forceAll)
		{
			writer.WriteVarInt(m_slot);
			return;
		}
		writer.WriteVarULong(syncVarDirtyBits);
		if ((syncVarDirtyBits & 1L) != 0L)
		{
			writer.WriteVarInt(m_slot);
		}
	}

	protected override void DeserializeSyncVars(NetworkReader reader, bool initialState)
	{
		base.DeserializeSyncVars(reader, initialState);
		if (initialState)
		{
			GeneratedSyncVarDeserialize(ref m_slot, null, reader.ReadVarInt());
			return;
		}
		long num = (long)reader.ReadVarULong();
		if ((num & 1L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref m_slot, null, reader.ReadVarInt());
		}
	}
}
}
