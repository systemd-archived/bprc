public class Resistor : CircuitElement
{
	public double Resistance { get; set; }

	public override int DefaultElectrodeCount => 2;

	public Electrode Electrode1 => m_electrodes[0];

	public Electrode Electrode2 => m_electrodes[1];

	public Resistor(double resistance)
	{
		Resistance = resistance;
	}
}
