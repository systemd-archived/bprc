using System.Collections.Generic;

public class GroundPart : ElectricalPart
{
	private Ground m_ground;

	public override IEnumerable<CircuitElement> ElectricalElements => m_ground.ToEnumerable();

	protected override BitDirection GetConnectionDirection()
	{
		return BitDirection.Up.Rotate((int)m_gridRotation);
	}

	public override void CreateElectricalElements()
	{
		m_ground = new Ground();
	}

	protected override Electrode FindElectrode(BitDirection direction)
	{
		direction = direction.Rotate(0 - m_gridRotation);
		if (direction == BitDirection.Up)
		{
			return m_ground.Electrode;
		}
		return null;
	}
}
