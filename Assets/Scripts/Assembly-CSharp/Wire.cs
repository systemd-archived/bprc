public class Wire : CircuitElement
{
	public override int DefaultElectrodeCount => 0;

	public Wire()
		: this(0)
	{
	}

	public Wire(int electrodeCount)
	{
		if (electrodeCount != 0)
		{
			CreateElectrodes(electrodeCount);
		}
	}

	public override bool IsNode()
	{
		return GetConnectedElectrodeCount() >= 3;
	}
}
