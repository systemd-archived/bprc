using System;

public struct UIPartButtonInfo : IEquatable<UIPartButtonInfo>
{
	public UIPartButtonType ButtonType;

	public int ButtonIndex;

	public BasePart.PartType PartType;

	public int PartIndex;

	public int ComponentIndex;

	public int ComponentRank;

	public UIPartButtonInfo(UIPartButtonType buttonType, int buttonIndex, BasePart.PartType partType, int partIndex, int componentIndex)
		: this(buttonType, buttonIndex, partType, partIndex, componentIndex, -1)
	{
	}

	public UIPartButtonInfo(UIPartButtonType buttonType, int buttonIndex, BasePart.PartType partType, int partIndex, int componentIndex, int componentRank)
	{
		ButtonType = buttonType;
		ButtonIndex = buttonIndex;
		PartType = partType;
		PartIndex = partIndex;
		ComponentIndex = componentIndex;
		ComponentRank = componentRank;
	}

	public override bool Equals(object other)
	{
		if (other is UIPartButtonInfo other2)
		{
			return Equals(other2);
		}
		return false;
	}

	public bool Equals(UIPartButtonInfo other)
	{
		if (ButtonType == other.ButtonType && ButtonIndex == other.ButtonIndex && PartType == other.PartType && PartIndex == other.PartIndex)
		{
			return ComponentRank == other.ComponentRank;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(ButtonType, ButtonIndex, PartType, PartIndex, ComponentRank);
	}

	public static bool operator ==(UIPartButtonInfo left, UIPartButtonInfo right)
	{
		return left.Equals(right);
	}

	public static bool operator !=(UIPartButtonInfo left, UIPartButtonInfo right)
	{
		return !(left == right);
	}
}
