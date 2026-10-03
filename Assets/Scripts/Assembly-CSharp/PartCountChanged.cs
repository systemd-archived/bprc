public struct PartCountChanged : EventManager.Event
{
	public BasePart.PartType partType;

	public int count;

	public int customPartIndex;

	public PartCountChanged(BasePart.PartType partType, int count)
		: this(partType, count, -1)
	{
	}

	public PartCountChanged(BasePart.PartType partType, int count, int customPartIndex)
	{
		this.partType = partType;
		this.count = count;
		this.customPartIndex = customPartIndex;
	}
}
