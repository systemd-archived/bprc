using System.Collections.Generic;
using UnityEngine;

namespace BadPiggies.Multiplayer
{
/// <summary>
/// Turns submitted blueprints into live machines.
///
/// Every peer builds every machine in the round: its own through the game's native transition
/// (<see cref="EnterNativeBattle"/>) and everybody else's as a replica cloned from the build grid
/// (<see cref="BuildReplicaMachine"/>). On the host all of them are simulated; on a guest they are
/// all kinematic and driven by the snapshot streams the host sends.
/// </summary>
public static class MultiplayerBattleAssembler
{
	/// <summary>
	/// Wheel drive sign for a lane. Mirrored machines have their drive force and spin inverted so
	/// the player's own controls keep pointing the way their machine faces.
	/// </summary>
	public static float DriveSignForLane(int lane, int playerCount)
	{
		return MultiplayerSpawnResolver.IsMirroredLane(lane, playerCount) ? -1f : 1f;
	}

	public static ContraptionDataset PrepareDataset(LevelManager levelManager, byte[] payload, int slot, int lane, int playerCount)
	{
		ContraptionDataset contraptionDataset = MultiplayerBlueprint.Deserialize(payload);
		if (contraptionDataset == null)
		{
			return null;
		}
		if (MultiplayerSpawnResolver.IsMirroredLane(lane, playerCount))
		{
			ContraptionDataset contraptionDataset2 = MultiplayerBlueprintMirror.Mirror(contraptionDataset, levelManager.GridXMin, levelManager.GridXMax);
			MultiplayerMirrorAudit.Verify(contraptionDataset, contraptionDataset2, levelManager.GridXMin, levelManager.GridXMax, lane);
			contraptionDataset = contraptionDataset2;
		}
		return contraptionDataset;
	}

	/// <summary>Builds one remote player's machine as a replica on the build grid.</summary>
	public static Contraption BuildReplicaMachine(LevelManager levelManager, ContraptionDataset dataset, int slot, int lane, int playerCount)
	{
		GameMode currentGameMode = levelManager.CurrentGameMode;
		Contraption contraption = currentGameMode?.ContraptionProto;
		if (contraption == null)
		{
			Debug.LogError("[Multiplayer] No build-grid contraption; cannot build the replica machine.");
			return null;
		}
		if (!contraption.gameObject.activeSelf)
		{
			contraption.SetVisible(visible: true);
		}
		levelManager.ConstructionUI.ClearContraption();
		PurgeDestroyedParts(contraption);
		currentGameMode.BuildContraption(dataset);
		RefreshVisualConnections(contraption);
		Vector3 vector = MultiplayerSpawnResolver.Resolve(levelManager, lane, playerCount);
		contraption.transform.position = vector;
		Contraption contraption2 = contraption.Clone();
		contraption2.name = $"MultiplayerMachine_S{slot + 1}";
		contraption2.transform.position = vector;
		contraption2.MultiplayerDriveSign = DriveSignForLane(lane, playerCount);
		int layer = MultiplayerLayers.ForSlot(slot);
		int num = MultiplayerLayers.Apply(contraption2, layer);
		Contraption contraptionRunning = currentGameMode.ContraptionRunning;
		try
		{
			currentGameMode.MultiplayerSetContraptionRunning(contraption2);
			contraption2.StartContraption();
		}
		finally
		{
			currentGameMode.MultiplayerSetContraptionRunning(contraptionRunning);
		}
		int num2 = MultiplayerLayers.ApplyToLateObjects(contraption2, layer);
		Debug.Log($"[Multiplayer] Built replica machine for slot {slot} (lane {lane}) at {vector} on " +
			$"{LayerMask.LayerToName(layer)} ({num} + {num2} objects).");
		MultiplayerMirrorAudit.ReportUnattachedParts(contraption2, slot);
		return contraption2;
	}

	/// <summary>Starts the local player's machine through the game's own Running transition.</summary>
	public static Contraption EnterNativeBattle(LevelManager levelManager, ContraptionDataset dataset, int slot, int lane, int playerCount)
	{
		GameMode currentGameMode = levelManager.CurrentGameMode;
		Contraption contraption = currentGameMode?.ContraptionProto;
		if (contraption == null)
		{
			Debug.LogError("[Multiplayer] No build-grid contraption; cannot start the local machine.");
			return null;
		}
		if (!contraption.gameObject.activeSelf)
		{
			contraption.SetVisible(visible: true);
		}
		levelManager.ConstructionUI.ClearContraption();
		PurgeDestroyedParts(contraption);
		currentGameMode.BuildContraption(dataset);
		RefreshVisualConnections(contraption);
		Vector3 vector = MultiplayerSpawnResolver.Resolve(levelManager, lane, playerCount);
		contraption.transform.position = vector;
		if (contraption.FindPig() == null)
		{
			Debug.LogError("[Multiplayer] Blueprint has no pig; refusing the native transition.");
			return null;
		}
		levelManager.SetGameState(LevelManager.GameState.Running);
		Contraption contraptionRunning = currentGameMode.ContraptionRunning;
		if (contraptionRunning == null)
		{
			Debug.LogError("[Multiplayer] SetGameState(Running) produced no ContraptionRunning.");
			return null;
		}
		contraptionRunning.name = $"MultiplayerMachine_S{slot + 1}";
		contraptionRunning.MultiplayerDriveSign = DriveSignForLane(lane, playerCount);
		int layer = MultiplayerLayers.ForSlot(slot);
		int num = MultiplayerLayers.Apply(contraptionRunning, layer);
		int num2 = MultiplayerLayers.ApplyToLateObjects(contraptionRunning, layer);
		Debug.Log($"[Multiplayer] Local machine (slot {slot}, lane {lane}) started natively at {vector} on " +
			$"{LayerMask.LayerToName(layer)} ({num} + {num2} objects).");
		MultiplayerMirrorAudit.ReportUnattachedParts(contraptionRunning, slot);
		return contraptionRunning;
	}

	private static int RefreshVisualConnections(Contraption proto)
	{
		if (proto == null || proto.Parts == null)
		{
			return 0;
		}
		int num = 0;
		List<BasePart> list = new List<BasePart>(proto.Parts);
		for (int i = 0; i < list.Count; i++)
		{
			BasePart basePart = list[i];
			if (!(basePart == null))
			{
				basePart.ChangeVisualConnections();
				num++;
			}
		}
		return num;
	}

	private static int PurgeDestroyedParts(Contraption proto)
	{
		if (proto == null)
		{
			return 0;
		}
		List<BasePart> parts = proto.Parts;
		BasePart[] componentsInChildren = proto.GetComponentsInChildren<BasePart>(includeInactive: true);
		int num = 0;
		foreach (BasePart basePart in componentsInChildren)
		{
			if (!(basePart == null) && (parts == null || !parts.Contains(basePart)))
			{
				basePart.gameObject.SetActive(value: false);
				basePart.transform.SetParent(null, worldPositionStays: false);
				Object.Destroy(basePart.gameObject);
				num++;
			}
		}
		if (num > 0)
		{
			Debug.Log($"[Multiplayer] Evicted {num} part(s) awaiting destruction before cloning the grid.");
		}
		return num;
	}

	public static List<BasePart> OrderedParts(Contraption machine)
	{
		List<BasePart> list = new List<BasePart>();
		if (machine == null)
		{
			return list;
		}
		foreach (BasePart part in machine.Parts)
		{
			if (part != null)
			{
				list.Add(part);
			}
		}
		list.Sort(ComparePartsByGrid);
		return list;
	}

	private static int ComparePartsByGrid(BasePart a, BasePart b)
	{
		int num = a.m_coordY.CompareTo(b.m_coordY);
		if (num != 0)
		{
			return num;
		}
		int num2 = a.m_coordX.CompareTo(b.m_coordX);
		if (num2 != 0)
		{
			return num2;
		}
		int partType = (int)a.m_partType;
		return partType.CompareTo((int)b.m_partType);
	}

	public static int SetSimulated(Contraption machine, bool simulated)
	{
		if (machine == null)
		{
			return 0;
		}
		Rigidbody[] componentsInChildren = machine.GetComponentsInChildren<Rigidbody>(includeInactive: true);
		foreach (Rigidbody rigidbody in componentsInChildren)
		{
			if (!(rigidbody == null))
			{
				if (simulated)
				{
					rigidbody.isKinematic = false;
				}
				else
				{
					rigidbody.velocity = Vector3.zero;
					rigidbody.angularVelocity = Vector3.zero;
					rigidbody.interpolation = RigidbodyInterpolation.None;
					rigidbody.isKinematic = true;
				}
				rigidbody.sleepThreshold = 0f;
				rigidbody.WakeUp();
			}
		}
		KeepVisibleOffScreen(machine);
		return componentsInChildren.Length;
	}

	private static int KeepVisibleOffScreen(Contraption machine)
	{
		int num = 0;
		Animation[] componentsInChildren = machine.GetComponentsInChildren<Animation>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			if (componentsInChildren[i] != null)
			{
				componentsInChildren[i].cullingType = AnimationCullingType.AlwaysAnimate;
				num++;
			}
		}
		Animator[] componentsInChildren2 = machine.GetComponentsInChildren<Animator>(includeInactive: true);
		for (int j = 0; j < componentsInChildren2.Length; j++)
		{
			if (componentsInChildren2[j] != null)
			{
				componentsInChildren2[j].cullingMode = AnimatorCullingMode.AlwaysAnimate;
				num++;
			}
		}
		ParticleSystem[] componentsInChildren3 = machine.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
		for (int k = 0; k < componentsInChildren3.Length; k++)
		{
			if (!(componentsInChildren3[k] == null))
			{
				ParticleSystem.MainModule main = componentsInChildren3[k].main;
				main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
				num++;
			}
		}
		return num;
	}

	public static int CountDynamicBodies(Contraption machine)
	{
		if (machine == null)
		{
			return 0;
		}
		int num = 0;
		Rigidbody[] componentsInChildren = machine.GetComponentsInChildren<Rigidbody>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			if (componentsInChildren[i] != null && !componentsInChildren[i].isKinematic)
			{
				num++;
			}
		}
		return num;
	}
}
}
