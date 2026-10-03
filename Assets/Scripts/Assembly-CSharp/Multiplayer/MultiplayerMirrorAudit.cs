using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace BadPiggies.Multiplayer
{
public static class MultiplayerMirrorAudit
{
	private const int MaxDetailLines = 12;

	public static bool Verify(ContraptionDataset original, ContraptionDataset mirrored, int gridXMin, int gridXMax, int slot)
	{
		if (original == null || mirrored == null)
		{
			Debug.LogError("[Multiplayer] Mirror audit skipped: a blueprint was null.");
			return false;
		}
		List<ContraptionDataset.ContraptionDatasetUnit> contraptionDatasetList = original.ContraptionDatasetList;
		List<ContraptionDataset.ContraptionDatasetUnit> contraptionDatasetList2 = mirrored.ContraptionDatasetList;
		if (contraptionDatasetList.Count != contraptionDatasetList2.Count)
		{
			Debug.LogError($"[Multiplayer] Mirror audit FAILED for slot {slot}: {contraptionDatasetList.Count} parts in, " + $"{contraptionDatasetList2.Count} out. The mirror dropped or duplicated parts.");
			return false;
		}
		int num = gridXMin + gridXMax;
		int num2 = 0;
		int num3 = 0;
		int num4 = 0;
		int num5 = 0;
		int num6 = 0;
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < contraptionDatasetList.Count; i++)
		{
			ContraptionDataset.ContraptionDatasetUnit contraptionDatasetUnit = contraptionDatasetList[i];
			ContraptionDataset.ContraptionDatasetUnit contraptionDatasetUnit2 = contraptionDatasetList2[i];
			string text = Identify(contraptionDatasetUnit);
			if (contraptionDatasetUnit.partType != contraptionDatasetUnit2.partType || contraptionDatasetUnit.customPartIndex != contraptionDatasetUnit2.customPartIndex)
			{
				Debug.LogError($"[Multiplayer] Mirror audit FAILED for slot {slot}: part {i} changed identity " + "(" + Identify(contraptionDatasetUnit) + " -> " + Identify(contraptionDatasetUnit2) + ").");
				return false;
			}
			if (contraptionDatasetUnit2.x != num - contraptionDatasetUnit.x || contraptionDatasetUnit2.y != contraptionDatasetUnit.y)
			{
				Debug.LogError($"[Multiplayer] Mirror audit FAILED for slot {slot}: {text} should have moved to " + $"({num - contraptionDatasetUnit.x},{contraptionDatasetUnit.y}) but sits at ({contraptionDatasetUnit2.x},{contraptionDatasetUnit2.y}).");
				num2++;
				continue;
			}
			if (contraptionDatasetUnit.rot != contraptionDatasetUnit2.rot || contraptionDatasetUnit.flipped != contraptionDatasetUnit2.flipped)
			{
				num6++;
			}
			BasePart basePart = MultiplayerPartAxis.ResolvePrefab(contraptionDatasetUnit.partType, contraptionDatasetUnit.customPartIndex);
			if (basePart == null)
			{
				num5++;
				AppendDetail(stringBuilder, text + ": prefab did not resolve, so its orientation was left unmirrored");
				continue;
			}
			if (basePart.m_gridXmin + basePart.m_gridXmax != 0)
			{
				num4++;
				AppendDetail(stringBuilder, $"{text}: grid span [{basePart.m_gridXmin},{basePart.m_gridXmax}] is not " + "symmetric about its anchor, so mirroring shifts it by " + $"{basePart.m_gridXmin + basePart.m_gridXmax} cell(s)");
			}
			BasePart.JointConnectionDirection jointConnectionDirection = MultiplayerPartAxis.WorldSocket(basePart, (BasePart.GridRotation)contraptionDatasetUnit.rot, contraptionDatasetUnit.flipped);
			BasePart.JointConnectionDirection jointConnectionDirection2 = MultiplayerPartAxis.WorldSocket(basePart, (BasePart.GridRotation)contraptionDatasetUnit2.rot, contraptionDatasetUnit2.flipped);
			BasePart.JointConnectionDirection jointConnectionDirection3 = MultiplayerPartAxis.ReflectSocket(jointConnectionDirection);
			if (jointConnectionDirection2 != jointConnectionDirection3)
			{
				num2++;
				AppendDetail(stringBuilder, $"{text}: socket faced {jointConnectionDirection} and should now face " + $"{jointConnectionDirection3}, but faces {jointConnectionDirection2} — this part will look for a neighbour " + "on the wrong side and be built attached to nothing");
			}
			if (ConflictingAxes(basePart, out var socketAxis, out var effectAxis))
			{
				num3++;
				AppendDetail(stringBuilder, $"{text}: mounts on the {socketAxis}° axis but acts on the {effectAxis}° " + "axis; no single rotation can mirror both, so one of them is now wrong");
			}
		}
		bool num7 = num2 == 0 && num3 == 0 && num4 == 0;
		string text2 = $"[Multiplayer] Mirror audit for slot {slot}: {contraptionDatasetList.Count} parts in, {contraptionDatasetList2.Count} out; " + $"{AdjacentPairs(contraptionDatasetList)} adjacent pairs in, {AdjacentPairs(contraptionDatasetList2)} out; " + $"{CountActivatable(contraptionDatasetList)} activatable parts in, {CountActivatable(contraptionDatasetList2)} out; " + $"{num6} part(s) reoriented.";
		if (num7)
		{
			if (num5 > 0)
			{
				Debug.LogWarning($"{text2} {num5} part(s) could not be classified — see above.");
			}
			else
			{
				Debug.Log(text2 + " Sockets and effect axes all reflect correctly.");
			}
			return true;
		}
		Debug.LogError($"{text2} PROBLEMS: {num2} socket(s) face the wrong way, " + $"{num3} axis conflict(s), {num4} asymmetric span(s).\n{stringBuilder}");
		return false;
	}

	private static bool ConflictingAxes(BasePart prefab, out int socketAxis, out int effectAxis)
	{
		effectAxis = 0;
		if (!MultiplayerPartAxis.TryResolveSocketAxis(prefab, out socketAxis))
		{
			return false;
		}
		if (!MultiplayerPartAxis.TryResolveEffectAxis(prefab, out effectAxis))
		{
			return false;
		}
		return ((socketAxis - effectAxis) % 180 + 180) % 180 != 0;
	}

	private static int AdjacentPairs(List<ContraptionDataset.ContraptionDatasetUnit> units)
	{
		int num = 0;
		for (int i = 0; i < units.Count; i++)
		{
			for (int j = i + 1; j < units.Count; j++)
			{
				int num2 = Mathf.Abs(units[i].x - units[j].x);
				int num3 = Mathf.Abs(units[i].y - units[j].y);
				if (num2 + num3 == 1)
				{
					num++;
				}
			}
		}
		return num;
	}

	private static int CountActivatable(List<ContraptionDataset.ContraptionDatasetUnit> units)
	{
		int num = 0;
		for (int i = 0; i < units.Count; i++)
		{
			BasePart basePart = MultiplayerPartAxis.ResolvePrefab(units[i].partType, units[i].customPartIndex);
			if (basePart == null)
			{
				continue;
			}
			try
			{
				if (basePart.IsEngine() || basePart.IsPowered() || basePart.HasOnOffToggle())
				{
					num++;
				}
			}
			catch (Exception)
			{
			}
		}
		return num;
	}

	private static string Identify(ContraptionDataset.ContraptionDatasetUnit unit)
	{
		BasePart.PartType partType = (BasePart.PartType)unit.partType;
		string text = partType.ToString();
		return $"{text}#{unit.customPartIndex} at ({unit.x},{unit.y}) rot {(BasePart.GridRotation)unit.rot}" + (unit.flipped ? " flipped" : string.Empty);
	}

	private static void AppendDetail(StringBuilder detail, string line)
	{
		int num = 0;
		for (int i = 0; i < detail.Length; i++)
		{
			if (detail[i] == '\n')
			{
				num++;
			}
		}
		if (num <= 12)
		{
			if (num == 12)
			{
				detail.Append("  ... further problems suppressed\n");
			}
			else
			{
				detail.Append("  ").Append(line).Append('\n');
			}
		}
	}

	public static int ReportUnattachedParts(Contraption machine, int slot)
	{
		if (machine == null)
		{
			return 0;
		}
		List<BasePart> list = MultiplayerBattleAssembler.OrderedParts(machine);
		if (list.Count <= 1)
		{
			return 0;
		}
		List<Joint> list2 = new List<Joint>();
		int num = 0;
		int num2 = 0;
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < list.Count; i++)
		{
			BasePart basePart = list[i];
			if (!(basePart == null))
			{
				basePart.GetComponents(list2);
				num += list2.Count;
				if (list2.Count <= 0 && !(basePart.enclosedInto != null) && !basePart.m_static)
				{
					num2++;
					AppendDetail(stringBuilder, $"{basePart.m_partType}#{basePart.customPartIndex} at " + $"({basePart.m_coordX},{basePart.m_coordY}) rot {basePart.m_gridRotation}" + (basePart.IsFlipped() ? " flipped" : string.Empty) + $", socket {basePart.GetJointConnectionDirection()}");
				}
			}
		}
		if (num2 == 0)
		{
			Debug.Log($"[Multiplayer] Slot {slot} assembled: {list.Count} parts, {num} joint " + "components, every part attached.");
			return 0;
		}
		Debug.LogError($"[Multiplayer] Slot {slot} assembled with {num2} of {list.Count} part(s) " + $"attached to nothing ({num} joint components in total):\n{stringBuilder}");
		return num2;
	}
}
}
