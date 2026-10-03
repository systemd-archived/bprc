using System.Collections.Generic;
using UnityEngine;

namespace BadPiggies.Multiplayer
{
/// <summary>
/// Layer plumbing for every machine in an arena.
///
/// The base game hard-codes <c>LayerMask.NameToLayer("Contraption")</c> wherever it asks "is this
/// thing a contraption?" — rockets, sticky wheels and pressure buttons all do. A networked machine
/// therefore has to live on a layer those tests still recognise. Multiplayer puts every player's
/// machine (local and remote, host and guest) on one shared layer, <c>MultiplayerParts</c>
/// (index 27), and hands the base game a mask that accepts both that layer and the stock
/// <c>Contraption</c> layer.
///
/// One shared layer — rather than one layer per player — is deliberate: Unity has 32 layers and the
/// project only has a handful spare, so a per-player scheme could never cover the 8-player capacity.
/// Nothing in the physics needs the machines separated either; each one is an independent rigid-body
/// assembly, and the only requirement is that machines collide with the world, with themselves and
/// with each other.
///
/// The collision matrix is applied through the physics API at runtime (<see cref="ApplyCollisionMatrix"/>)
/// as well as being serialised into ProjectSettings, so a stale ProjectSettings file cannot silently
/// leave the machines colliding with the HUD or the effect layers.
/// </summary>
public static class MultiplayerLayers
{
	/// <summary>Name of the shared machine layer. Serialised in ProjectSettings/TagManager.asset.</summary>
	public const string PartsLayerName = "MultiplayerParts";

	/// <summary>Index the layer is expected to occupy. Only used for diagnostics.</summary>
	public const int PartsLayerIndex = 27;

	private static readonly int s_stockLayer = LayerMask.NameToLayer("Contraption");

	private static readonly int s_partsLayer = LayerMask.NameToLayer(PartsLayerName);

	private static readonly int s_contraptionMask = BuildContraptionMask();

	/// <summary>
	/// Layers a machine must never collide with: HUD (10), NonCollidingPart (15), Bird (16),
	/// Food (17), Mouth (18), IcePart (22) and Light (23). Taken from the reference build's matrix.
	/// </summary>
	private static readonly int[] s_isolatedLayers = { 10, 15, 16, 17, 18, 22, 23 };

	/// <summary>World layers a machine must keep colliding with; only used for the self check.</summary>
	private static readonly int[] s_worldLayers = { 0, 8, 9, 11, 19, 20 };

	private static bool s_matrixApplied;

	private static bool s_missingLayerReported;

	/// <summary>Layer index of <c>MultiplayerParts</c>, or -1 when the project has no such layer.</summary>
	public static int PartsLayer => s_partsLayer;

	/// <summary>Mask matching every layer a machine can be on (stock contraption + machine layer).</summary>
	public static int ContraptionLayerMask => s_contraptionMask;

	public static bool IsContraptionLayer(int layer)
	{
		if (layer < 0)
		{
			return false;
		}
		return layer == s_stockLayer || layer == s_partsLayer;
	}

	/// <summary>
	/// The layer a machine owned by <paramref name="slot"/> belongs on. Every slot shares one layer;
	/// the argument is kept so call sites stay readable.
	/// </summary>
	public static int ForSlot(int slot)
	{
		return s_partsLayer;
	}

	private static int BuildContraptionMask()
	{
		int mask = 0;
		if (s_stockLayer >= 0)
		{
			mask |= 1 << s_stockLayer;
		}
		if (s_partsLayer >= 0)
		{
			mask |= 1 << s_partsLayer;
		}
		return mask;
	}

	/// <summary>True when the machine layer exists and is usable.</summary>
	public static bool AreConfigured(out string error)
	{
		if (s_partsLayer < 0)
		{
			error = "The machine layer '" + PartsLayerName + "' is missing from TagManager " +
				"(expected at index " + PartsLayerIndex + "). Add it, or run " +
				"Tools/Multiplayer/Apply Multiplayer Layers.";
			return false;
		}
		if (s_partsLayer == s_stockLayer)
		{
			error = "The machine layer '" + PartsLayerName + "' shares the stock contraption layer " +
				"index; the layer mask would be wrong.";
			return false;
		}
		error = null;
		return true;
	}

	/// <summary>
	/// Forces the machine layer's collision matrix to the documented state: solid against the world
	/// and against itself, isolated from the HUD/effect layers. Safe to call repeatedly; it is a
	/// no-op after the first successful application.
	/// </summary>
	public static void ApplyCollisionMatrix()
	{
		if (s_matrixApplied || s_partsLayer < 0)
		{
			return;
		}
		s_matrixApplied = true;
		for (int other = 0; other < 32; other++)
		{
			bool isolate = false;
			for (int i = 0; i < s_isolatedLayers.Length; i++)
			{
				if (s_isolatedLayers[i] == other)
				{
					isolate = true;
					break;
				}
			}
			Physics.IgnoreLayerCollision(s_partsLayer, other, isolate);
		}
		// The layer has to collide with itself, or a machine would fall apart; and with the stock
		// contraption layer, because a part that has not been re-layered yet is still on it.
		Physics.IgnoreLayerCollision(s_partsLayer, s_partsLayer, false);
		if (s_stockLayer >= 0)
		{
			Physics.IgnoreLayerCollision(s_partsLayer, s_stockLayer, false);
		}
	}

	/// <summary>
	/// Applies the collision matrix and reports a missing layer once. Called from the bootstrap so
	/// the system never depends on a hand-edited ProjectSettings file at runtime.
	/// </summary>
	public static void EnsureConfigured()
	{
		if (!AreConfigured(out string error))
		{
			if (!s_missingLayerReported)
			{
				s_missingLayerReported = true;
				Debug.LogError("[Multiplayer] " + error);
			}
			return;
		}
		s_missingLayerReported = false;
		ApplyCollisionMatrix();
	}

	/// <summary>Collision-matrix self check; reports the first deviation it finds. Used by the audit.</summary>
	public static bool VerifyCollisionMatrix(out string error)
	{
		error = null;
		if (s_partsLayer < 0)
		{
			error = "The machine layer '" + PartsLayerName + "' is missing.";
			return false;
		}
		if (Physics.GetIgnoreLayerCollision(s_partsLayer, s_partsLayer))
		{
			error = PartsLayerName + " ignores itself, so a machine would fall apart.";
			return false;
		}
		foreach (int other in s_isolatedLayers)
		{
			if (!Physics.GetIgnoreLayerCollision(s_partsLayer, other))
			{
				error = PartsLayerName + " still collides with layer " + other + " (" +
					LayerMask.LayerToName(other) + ").";
				return false;
			}
		}
		foreach (int other in s_worldLayers)
		{
			if (Physics.GetIgnoreLayerCollision(s_partsLayer, other))
			{
				error = PartsLayerName + " no longer collides with layer " + other + " (" +
					LayerMask.LayerToName(other) + ").";
				return false;
			}
		}
		return true;
	}

	public static int CountColliders(Contraption machine, int layer)
	{
		if (machine == null || layer < 0)
		{
			return 0;
		}
		int count = 0;
		Collider[] colliders = machine.GetComponentsInChildren<Collider>(includeInactive: true);
		for (int i = 0; i < colliders.Length; i++)
		{
			if (colliders[i] != null && colliders[i].gameObject.layer == layer)
			{
				count++;
			}
		}
		return count;
	}

	public static int Apply(Contraption contraption, int layer)
	{
		if (contraption == null || layer < 0)
		{
			return 0;
		}
		return ApplyRecursive(contraption.transform, layer);
	}

	private static int ApplyRecursive(Transform root, int layer)
	{
		int moved = 0;
		int stockLayer = LayerMask.NameToLayer("Contraption");
		Stack<Transform> stack = new Stack<Transform>();
		stack.Push(root);
		while (stack.Count > 0)
		{
			Transform current = stack.Pop();
			int currentLayer = current.gameObject.layer;
			if (currentLayer != layer && (currentLayer == s_partsLayer || currentLayer == stockLayer))
			{
				current.gameObject.layer = layer;
				moved++;
			}
			for (int i = 0; i < current.childCount; i++)
			{
				stack.Push(current.GetChild(i));
			}
		}
		return moved;
	}

	public static int ApplyToLateObjects(Contraption contraption, int layer)
	{
		return Apply(contraption, layer);
	}
}
}
