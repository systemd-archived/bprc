public class Switch : CircuitElement
{
	private bool m_closed;

	public const int PoleIndex = 0;

	public const int ThrowIndex = 1;

	public bool IsClosed => m_closed;

	public override int DefaultElectrodeCount => 2;

	public Electrode Pole => m_electrodes[0];

	public Electrode Throw => m_electrodes[1];

	public Switch()
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
		Throw.Switch(closed);
	}
}
