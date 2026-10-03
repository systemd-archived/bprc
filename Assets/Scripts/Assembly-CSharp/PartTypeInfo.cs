using System;
using System.Runtime.CompilerServices;

public readonly struct PartTypeInfo : IEquatable<PartTypeInfo>
{
	public readonly BasePart.PartType PartType;

	public readonly int PartIndex;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public PartTypeInfo(BasePart part)
		: this(part.Type, part.Index)
	{
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public PartTypeInfo(BasePart.PartType partType, int partIndex)
	{
		PartType = partType;
		PartIndex = partIndex;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override bool Equals(object other)
	{
		if (other is PartTypeInfo other2)
		{
			return Equals(other2);
		}
		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Equals(PartTypeInfo other)
	{
		if (PartType == other.PartType)
		{
			return PartIndex == other.PartIndex;
		}
		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override int GetHashCode()
	{
		return HashCode.Combine((int)PartType, PartIndex);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Deconstruct(out BasePart.PartType partType, out int partIndex)
	{
		partType = PartType;
		partIndex = PartIndex;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool BelongsTo(PartTypeInfo info)
	{
		if (PartType == info.PartType)
		{
			return PartIndex == info.PartIndex;
		}
		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool BelongsTo(PartRangeInfo info)
	{
		if (PartType == info.PartType && info.PartStartIndex <= PartIndex)
		{
			return PartIndex <= info.PartEndIndex;
		}
		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator ==(PartTypeInfo left, PartTypeInfo right)
	{
		return left.Equals(right);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator !=(PartTypeInfo left, PartTypeInfo right)
	{
		return !(left == right);
	}
}
