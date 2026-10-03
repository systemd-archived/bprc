public class CurrentSource : CircuitElement
{
	public const int AnodeIndex = 0;

	public const int CathodeIndex = 1;

	public double Current { get; set; }

	public double Resistance { get; set; }

	public override int DefaultElectrodeCount => 2;

	public Electrode Anode => m_electrodes[0];

	public Electrode Cathode => m_electrodes[1];

	public CurrentSource(double current, double resistance)
	{
		Current = current;
		Resistance = resistance;
	}
}
