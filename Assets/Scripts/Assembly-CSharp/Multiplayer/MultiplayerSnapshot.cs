using System;
using System.Collections.Generic;
using UnityEngine;

namespace BadPiggies.Multiplayer
{
public static class MultiplayerSnapshot
{
	public const float TickInterval = 0.05f;

	public const int HeaderBytes = 4;

	public const int BytesPerPart = 10;

	public const int MaxPartsPerMessage = 100;

	private static readonly Quaternion FlipRotation = Quaternion.AngleAxis(180f, Vector3.up);

	public static int MaskBytes(int count)
	{
		return count + 7 >> 3;
	}

	public static int MessageSize(int count)
	{
		return 4 + count * 10 + MaskBytes(count);
	}

	public static bool IsFlipCoset(Quaternion r)
	{
		return Mathf.Abs(r.z) + Mathf.Abs(r.w) < Mathf.Abs(r.x) + Mathf.Abs(r.y);
	}

	public static float ToAngle(Quaternion r, bool flipCoset)
	{
		if (!flipCoset)
		{
			return 2f * Mathf.Atan2(r.z, r.w) * 57.29578f;
		}
		return 2f * Mathf.Atan2(0f - r.x, r.y) * 57.29578f;
	}

	public static Quaternion FromAngle(float angleDegrees, bool flipCoset)
	{
		Quaternion quaternion = Quaternion.AngleAxis(angleDegrees, Vector3.forward);
		if (!flipCoset)
		{
			return quaternion;
		}
		return quaternion * FlipRotation;
	}

	public static byte[] Write(List<Transform> nodes, List<BasePart> owners, int start, int count, ushort tick, List<bool> flipCoset)
	{
		byte[] array = new byte[MessageSize(count)];
		array[0] = (byte)start;
		array[1] = (byte)count;
		array[2] = (byte)(tick & 0xFF);
		array[3] = (byte)(tick >> 8);
		int num = 4 + count * 10;
		for (int i = 0; i < count; i++)
		{
			int num2 = 4 + i * 10;
			int num3 = start + i;
			Transform transform = nodes[num3];
			if (transform == null)
			{
				WriteFloat(array, num2, float.NaN);
				WriteFloat(array, num2 + 4, float.NaN);
				continue;
			}
			Vector3 position = transform.position;
			WriteFloat(array, num2, position.x);
			WriteFloat(array, num2 + 4, position.y);
			bool flipCoset2 = num3 < flipCoset.Count && flipCoset[num3];
			int num4 = Mathf.RoundToInt(Mathf.Repeat(ToAngle(transform.rotation, flipCoset2), 360f) * 182.04445f) & 0xFFFF;
			array[num2 + 8] = (byte)(num4 & 0xFF);
			array[num2 + 9] = (byte)(num4 >> 8);
			BasePart basePart = ((owners != null && num3 < owners.Count) ? owners[num3] : null);
			if (basePart != null && basePart.IsEnabled())
			{
				array[num + (i >> 3)] |= (byte)(1 << (i & 7));
			}
		}
		return array;
	}

	private static void WriteFloat(byte[] blob, int offset, float value)
	{
		byte[] bytes = BitConverter.GetBytes(value);
		blob[offset] = bytes[0];
		blob[offset + 1] = bytes[1];
		blob[offset + 2] = bytes[2];
		blob[offset + 3] = bytes[3];
	}

	public static bool ReadHeader(byte[] blob, out int start, out int count, out ushort tick)
	{
		start = 0;
		count = 0;
		tick = 0;
		if (blob == null || blob.Length < 4)
		{
			return false;
		}
		start = blob[0];
		count = blob[1];
		if (count == 0 || blob.Length < MessageSize(count))
		{
			return false;
		}
		tick = (ushort)(blob[2] | (blob[3] << 8));
		return true;
	}

	public static bool ReadPart(byte[] blob, int i, out Vector3 position, out float angleDegrees)
	{
		int num = 4 + i * 10;
		float num2 = ReadFloat(blob, num);
		float num3 = ReadFloat(blob, num + 4);
		int num4 = blob[num + 8] | (blob[num + 9] << 8);
		position = new Vector3(num2, num3, 0f);
		angleDegrees = (float)num4 * (45f / 8192f);
		if (!float.IsNaN(num2) && !float.IsNaN(num3) && !float.IsInfinity(num2))
		{
			return !float.IsInfinity(num3);
		}
		return false;
	}

	public static bool ReadActive(byte[] blob, int count, int i)
	{
		int num = 4 + count * 10;
		return (blob[num + (i >> 3)] & (1 << (i & 7))) != 0;
	}

	public static bool IsNewer(ushort candidate, ushort reference)
	{
		if ((ushort)(candidate - reference) != 0)
		{
			return (ushort)(candidate - reference) < 32768;
		}
		return false;
	}

	public static float TickDelta(ushort later, ushort earlier, float tickInterval)
	{
		return (float)(int)(ushort)(later - earlier) * tickInterval;
	}

	private static float ReadFloat(byte[] blob, int offset)
	{
		return BitConverter.ToSingle(blob, offset);
	}
}
}
