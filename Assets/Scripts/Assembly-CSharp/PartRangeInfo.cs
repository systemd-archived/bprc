using System;
using System.Runtime.CompilerServices;

public readonly struct PartRangeInfo : IEquatable<PartRangeInfo>
{
	public readonly BasePart.PartType PartType;

	public readonly int PartStartIndex;

	public readonly int PartEndIndex;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public PartRangeInfo(BasePart part)
		: this(part.Type, part.Index)
	{
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public PartRangeInfo(BasePart.PartType partType, int partIndex)
		: this(partType, partIndex, partIndex)
	{
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public PartRangeInfo(BasePart.PartType partType, int partStartIndex, int partEndIndex)
	{
		PartType = partType;
		PartStartIndex = partStartIndex;
		PartEndIndex = partEndIndex;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override bool Equals(object other)
	{
		if (other is PartRangeInfo other2)
		{
			return Equals(other2);
		}
		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Equals(PartRangeInfo other)
	{
		if (PartType == other.PartType && PartStartIndex == other.PartStartIndex)
		{
			return PartEndIndex == other.PartEndIndex;
		}
		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override int GetHashCode()
	{
		return HashCode.Combine((int)PartType, PartStartIndex, PartEndIndex);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Deconstruct(out BasePart.PartType partType, out int partIndex)
	{
		partType = PartType;
		partIndex = PartStartIndex;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Deconstruct(out BasePart.PartType partType, out int partStartIndex, out int partEndIndex)
	{
		partType = PartType;
		partStartIndex = PartStartIndex;
		partEndIndex = PartEndIndex;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Contains(PartTypeInfo info)
	{
		if (PartType == info.PartType && PartStartIndex <= info.PartIndex)
		{
			return info.PartIndex <= PartEndIndex;
		}
		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Contains(PartRangeInfo info)
	{
		if (PartType == info.PartType && PartStartIndex <= info.PartStartIndex)
		{
			return info.PartEndIndex <= PartEndIndex;
		}
		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator ==(PartRangeInfo left, PartRangeInfo right)
	{
		return left.Equals(right);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator !=(PartRangeInfo left, PartRangeInfo right)
	{
		return !(left == right);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static explicit operator PartRangeInfo(PartTypeInfo info)
	{
		return new PartRangeInfo(info.PartType, info.PartIndex);
	}
}
