public class Vcc : CircuitElement
{
	public double Potential { get; set; }

	public double Resistance { get; set; }

	public override int DefaultElectrodeCount => 1;

	public Electrode Electrode => m_electrodes[0];

	public Vcc(double potential)
		: this(potential, 0.0)
	{
	}

	public Vcc(double potential, double resistance)
	{
		Potential = potential;
		Resistance = resistance;
	}
}
