using System;
using Mirror;
using UnityEngine;

namespace BadPiggies.Multiplayer
{
public static class MultiplayerInput
{
	public enum BulkKind : byte
	{
		PartType,
		Rockets,
		Engines
	}

	private static int s_localSlot = -1;

	private static bool s_gadgetHandlerInstalled;

	public static bool Active { get; private set; }

	public static void Hook(int localSlot)
	{
		s_localSlot = localSlot;
		Active = true;
		InstallGadgetHandler();
	}

	public static void Unhook()
	{
		if (s_gadgetHandlerInstalled)
		{
			EventManager.Disconnect<GadgetControlEvent>(ReceiveGadgetControlEvent);
			s_gadgetHandlerInstalled = false;
			LevelManager levelManager = WPFMonoBehaviour.levelManager;
			if (levelManager != null)
			{
				EventManager.Disconnect<GadgetControlEvent>(levelManager.ReceiveGadgetControlEvent);
				EventManager.Connect<GadgetControlEvent>(levelManager.ReceiveGadgetControlEvent);
			}
		}
		Active = false;
		s_localSlot = -1;
	}

	public static void Reassert()
	{
		if (Active)
		{
			InstallGadgetHandler();
		}
	}

	private static void InstallGadgetHandler()
	{
		LevelManager levelManager = WPFMonoBehaviour.levelManager;
		if (levelManager != null)
		{
			EventManager.Disconnect<GadgetControlEvent>(levelManager.ReceiveGadgetControlEvent);
		}
		if (!s_gadgetHandlerInstalled)
		{
			EventManager.Connect<GadgetControlEvent>(ReceiveGadgetControlEvent);
			s_gadgetHandlerInstalled = true;
		}
	}

	private static void ReceiveGadgetControlEvent(GadgetControlEvent data)
	{
		if (!Active)
		{
			LevelManager levelManager = WPFMonoBehaviour.levelManager;
			if (levelManager != null)
			{
				levelManager.ReceiveGadgetControlEvent(data);
			}
		}
		else
		{
			SendBulk(BulkKind.PartType, (int)data.partType, (int)data.direction);
		}
	}

	public static bool RouteTouch(Contraption machine, BasePart part, Vector3 worldPosition)
	{
		if (!Active || machine == null || part == null)
		{
			return false;
		}
		if (MultiplayerGameInputBlock.IsBlocked)
		{
			return true;
		}
		MultiplayerMatchController multiplayerMatchController = MultiplayerMatchController.Resolve();
		if (multiplayerMatchController == null)
		{
			return false;
		}
		if (machine != multiplayerMatchController.GetMachine(s_localSlot))
		{
			return true;
		}
		if (NetworkServer.active)
		{
			return false;
		}
		int num = IndexOfPart(multiplayerMatchController, s_localSlot, part);
		if (num < 0)
		{
			Debug.LogWarning("[Multiplayer] Touched part '" + part.name + "' is not in the synced part list; ignored.");
			return true;
		}
		MultiplayerLobbyPlayer multiplayerLobbyPlayer = LocalPlayer();
		if (multiplayerLobbyPlayer != null)
		{
			multiplayerLobbyPlayer.CmdActivatePart(num, worldPosition.x, worldPosition.y);
		}
		return true;
	}

	public static bool RouteBulk(BulkKind kind)
	{
		if (!Active)
		{
			return false;
		}
		return SendBulk(kind, 0, 0);
	}

	private static bool SendBulk(BulkKind kind, int partType, int direction)
	{
		if (NetworkServer.active)
		{
			MultiplayerMatchController multiplayerMatchController = MultiplayerMatchController.Resolve();
			ApplyBulk((multiplayerMatchController != null) ? multiplayerMatchController.GetMachine(s_localSlot) : null, kind, partType, direction);
			return true;
		}
		MultiplayerLobbyPlayer multiplayerLobbyPlayer = LocalPlayer();
		if (multiplayerLobbyPlayer != null)
		{
			multiplayerLobbyPlayer.CmdActivateBulk((byte)kind, partType, direction);
		}
		return true;
	}

	public static void ApplyTouch(Contraption machine, int partIndex, Vector3 worldPosition)
	{
		BasePart basePart = PartAt(machine, partIndex);
		if (basePart == null)
		{
			return;
		}
		try
		{
			basePart.ProcessTouch(worldPosition);
		}
		catch (Exception arg)
		{
			Debug.LogError($"[Multiplayer] Activating part {partIndex} ('{basePart.name}') threw; " + $"the tap is dropped and the session continues. {arg}");
		}
	}

	public static void ApplyBulk(Contraption machine, BulkKind kind, int partType, int direction)
	{
		if (machine == null)
		{
			return;
		}
		try
		{
			ApplyBulkUnguarded(machine, kind, partType, direction);
		}
		catch (Exception arg)
		{
			Debug.LogError($"[Multiplayer] Bulk activation {kind} threw; the request is dropped and the " + $"session continues. {arg}");
		}
	}

	private static void ApplyBulkUnguarded(Contraption machine, BulkKind kind, int partType, int direction)
	{
		switch (kind)
		{
		case BulkKind.PartType:
			machine.ActivatePartType((BasePart.PartType)partType, (BasePart.Direction)direction);
			break;
		case BulkKind.Rockets:
		{
			Rocket[] componentsInChildren2 = machine.GetComponentsInChildren<Rocket>();
			for (int i = 0; i < componentsInChildren2.Length; i++)
			{
				if (componentsInChildren2[i] != null)
				{
					componentsInChildren2[i].ProcessTouch();
				}
			}
			break;
		}
		case BulkKind.Engines:
		{
			Engine[] componentsInChildren = machine.GetComponentsInChildren<Engine>();
			if (componentsInChildren.Length != 0 && componentsInChildren[0] != null)
			{
				componentsInChildren[0].ProcessTouch();
			}
			else if (machine.m_pig != null)
			{
				machine.m_pig.ProcessTouch();
			}
			break;
		}
		}
	}

	private static int IndexOfPart(MultiplayerMatchController controller, int slot, BasePart part)
	{
		MultiplayerMachineAnchor multiplayerMachineAnchor = ((controller != null) ? controller.GetAnchor(slot) : null);
		if (!(multiplayerMachineAnchor != null))
		{
			return -1;
		}
		return multiplayerMachineAnchor.IndexOf(part);
	}

	private static BasePart PartAt(Contraption machine, int partIndex)
	{
		if (machine == null || partIndex < 0)
		{
			return null;
		}
		MultiplayerMatchController multiplayerMatchController = MultiplayerMatchController.Resolve();
		if (multiplayerMatchController == null)
		{
			return null;
		}
		for (int i = 0; i < MultiplayerMatchController.MaxSlots; i++)
		{
			if (!(multiplayerMatchController.GetMachine(i) != machine))
			{
				MultiplayerMachineAnchor anchor = multiplayerMatchController.GetAnchor(i);
				if (!(anchor != null))
				{
					return null;
				}
				return anchor.PartAt(partIndex);
			}
		}
		return null;
	}

	private static MultiplayerLobbyPlayer LocalPlayer()
	{
		NetworkIdentity localPlayer = NetworkClient.localPlayer;
		if (localPlayer == null)
		{
			Debug.LogWarning("[Multiplayer] No local player object; input cannot reach the host.");
			return null;
		}
		return localPlayer.GetComponent<MultiplayerLobbyPlayer>();
	}
}
}
