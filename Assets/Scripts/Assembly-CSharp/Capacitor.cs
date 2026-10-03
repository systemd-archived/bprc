public class Capacitor : CircuitElement
{
	public const int AnodeIndex = 0;

	public const int CathodeIndex = 1;

	private double m_I;

	private double m_deltaTime;

	public double Capacitance { get; set; }

	public double Charge { get; set; }

	public double Resistance { get; set; }

	public override int DefaultElectrodeCount => 2;

	public Electrode Anode => m_electrodes[0];

	public Electrode Cathode => m_electrodes[1];

	public Capacitor(double capacitance, double resistance)
		: this(capacitance, 0.0, resistance)
	{
	}

	public Capacitor(double capacitance, double charge, double resistance)
	{
		Capacitance = capacitance;
		Charge = charge;
		Resistance = resistance;
		m_I = double.NaN;
	}

	public override void UpdateElectrode(CircuitSimulator simulator, SimulationResult result)
	{
		base.UpdateElectrode(simulator, result);
		bool flag = result.Electrode == Anode;
		m_I = (flag ? result.I : (0.0 - result.I));
		m_deltaTime = simulator.DeltaTime;
	}

	public override void Update()
	{
		base.Update();
		if (!double.IsNaN(m_I))
		{
			Charge += m_I * m_deltaTime;
		}
		m_I = double.NaN;
	}
}
