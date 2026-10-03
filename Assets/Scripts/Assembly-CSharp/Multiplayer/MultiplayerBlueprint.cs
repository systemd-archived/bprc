using System.Collections.Generic;
using UnityEngine;

namespace BadPiggies.Multiplayer
{
public static class MultiplayerBlueprint
{
	public const byte FormatVersion = 1;

	public const int MaxParts = 1024;

	private const int HeaderBytes = 3;

	private const int BytesPerPart = 6;

	private const int RotationMask = 15;

	private const int FlippedBit = 16;

	public static byte[] Serialize(ContraptionDataset dataset)
	{
		List<ContraptionDataset.ContraptionDatasetUnit> list = dataset?.ContraptionDatasetList;
		int num = list?.Count ?? 0;
		if (num > 1024)
		{
			Debug.LogWarning($"[Multiplayer] Blueprint has {num} parts; truncating to {1024}.");
			num = 1024;
		}
		byte[] array = new byte[3 + num * 6];
		array[0] = 1;
		array[1] = (byte)(num & 0xFF);
		array[2] = (byte)((num >> 8) & 0xFF);
		int num2 = 3;
		for (int i = 0; i < num; i++)
		{
			ContraptionDataset.ContraptionDatasetUnit contraptionDatasetUnit = list[i];
			array[num2++] = (byte)(contraptionDatasetUnit.partType & 0xFF);
			array[num2++] = (byte)((contraptionDatasetUnit.partType >> 8) & 0xFF);
			array[num2++] = (byte)Mathf.Clamp(contraptionDatasetUnit.customPartIndex, 0, 255);
			array[num2++] = (byte)(sbyte)Mathf.Clamp(contraptionDatasetUnit.x, -128, 127);
			array[num2++] = (byte)(sbyte)Mathf.Clamp(contraptionDatasetUnit.y, -128, 127);
			int num3 = contraptionDatasetUnit.rot & 0xF;
			if (contraptionDatasetUnit.flipped)
			{
				num3 |= 0x10;
			}
			array[num2++] = (byte)num3;
		}
		return array;
	}

	public static ContraptionDataset Deserialize(byte[] data)
	{
		if (data == null || data.Length < 3)
		{
			Debug.LogWarning("[Multiplayer] Blueprint rejected: payload is empty or truncated.");
			return null;
		}
		if (data[0] != 1)
		{
			Debug.LogWarning($"[Multiplayer] Blueprint rejected: format version {data[0]}, expected {(byte)1}.");
			return null;
		}
		int num = data[1] | (data[2] << 8);
		if (num < 0 || num > 1024)
		{
			Debug.LogWarning($"[Multiplayer] Blueprint rejected: implausible part count {num}.");
			return null;
		}
		if (data.Length < 3 + num * 6)
		{
			Debug.LogWarning("[Multiplayer] Blueprint rejected: payload shorter than its declared part count.");
			return null;
		}
		ContraptionDataset contraptionDataset = new ContraptionDataset();
		int num2 = 3;
		for (int i = 0; i < num; i++)
		{
			int num3 = data[num2] | (data[num2 + 1] << 8);
			int customPartIndex = data[num2 + 2];
			int x = (sbyte)data[num2 + 3];
			int y = (sbyte)data[num2 + 4];
			int num4 = data[num2 + 5];
			num2 += 6;
			if (num3 <= 0 || num3 >= (int)BasePart.PartType.MAX)
			{
				Debug.LogWarning($"[Multiplayer] Blueprint rejected: unknown part type {num3}.");
				return null;
			}
			int num5 = num4 & 0xF;
			if (num5 >= 8)
			{
				Debug.LogWarning($"[Multiplayer] Blueprint rejected: invalid rotation {num5}.");
				return null;
			}
			bool flipped = (num4 & 0x10) != 0;
			contraptionDataset.AddPart(x, y, num3, customPartIndex, (BasePart.GridRotation)num5, flipped);
		}
		return contraptionDataset;
	}

	public static ContraptionDataset CaptureFrom(Contraption contraption)
	{
		ContraptionDataset contraptionDataset = new ContraptionDataset();
		if (contraption == null)
		{
			return contraptionDataset;
		}
		foreach (BasePart part in contraption.Parts)
		{
			if (!(part == null))
			{
				contraptionDataset.AddPart(part.m_coordX, part.m_coordY, (int)part.m_partType, part.customPartIndex, part.m_gridRotation, part.IsFlipped());
			}
		}
		return contraptionDataset;
	}
}
}
