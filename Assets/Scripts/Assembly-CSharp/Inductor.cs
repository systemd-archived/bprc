public class Inductor : CircuitElement
{
	public const int AnodeIndex = 0;

	public const int CathodeIndex = 1;

	private double m_U1;

	private double m_U2;

	private double m_deltaTime;

	public double Inductance { get; set; }

	public double MagneticFlux { get; set; }

	public double Resistance { get; set; }

	public override int DefaultElectrodeCount => 2;

	public Electrode Anode => m_electrodes[0];

	public Electrode Cathode => m_electrodes[1];

	public Inductor(double inductance, double resistance)
		: this(inductance, 0.0, resistance)
	{
	}

	public Inductor(double inductance, double magneticFlux, double resistance)
	{
		Inductance = inductance;
		MagneticFlux = magneticFlux;
		Resistance = resistance;
		m_U1 = double.NaN;
		m_U2 = double.NaN;
	}

	public override void UpdateElectrode(CircuitSimulator simulator, SimulationResult result)
	{
		base.UpdateElectrode(simulator, result);
		if (result.Electrode == Anode)
		{
			m_U1 = result.U;
		}
		else
		{
			m_U2 = result.U;
		}
		m_deltaTime = simulator.DeltaTime;
	}

	public override void Update()
	{
		base.Update();
		if (!double.IsNaN(m_U1) && !double.IsNaN(m_U2))
		{
			MagneticFlux += (m_U1 - m_U2) * m_deltaTime;
		}
		m_U1 = double.NaN;
		m_U2 = double.NaN;
	}
}
