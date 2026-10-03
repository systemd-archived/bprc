public class VoltageSource : CircuitElement
{
	public const int AnodeIndex = 0;

	public const int CathodeIndex = 1;

	public double Voltage { get; set; }

	public double Resistance { get; set; }

	public override int DefaultElectrodeCount => 2;

	public Electrode Anode => m_electrodes[0];

	public Electrode Cathode => m_electrodes[1];

	public VoltageSource(double voltage, double resistance)
	{
		Voltage = voltage;
		Resistance = resistance;
	}
}
