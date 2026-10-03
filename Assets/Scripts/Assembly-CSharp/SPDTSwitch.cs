public class SPDTSwitch : CircuitElement
{
	private bool m_closed;

	public const int PoleIndex = 0;

	public const int Throw1Index = 1;

	public const int Throw2Index = 2;

	public bool IsClosed => m_closed;

	public override int DefaultElectrodeCount => 3;

	public Electrode Pole => m_electrodes[0];

	public Electrode Throw1 => m_electrodes[1];

	public Electrode Throw2 => m_electrodes[2];

	public SPDTSwitch()
	{
		m_closed = false;
		ToggleInternal(m_closed);
	}

	public void Toggle(bool closed)
	{
		if (m_closed != closed)
		{
			m_closed = closed;
			ToggleInternal(closed);
		}
	}

	private void ToggleInternal(bool closed)
	{
		Throw1.Switch(!closed);
		Throw2.Switch(closed);
	}
}
