public class Ground : CircuitElement
{
	public double Resistance { get; set; }

	public override int DefaultElectrodeCount => 1;

	public Electrode Electrode => m_electrodes[0];

	public Ground()
		: this(0.0)
	{
	}

	public Ground(double resistance)
	{
		Resistance = resistance;
	}
}
