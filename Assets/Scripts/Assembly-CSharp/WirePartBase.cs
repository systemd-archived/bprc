using System;

public class WirePartBase : ElectricalPart
{
	protected double m_maxCurrent;

	protected double[] m_currents;

	protected Electrode[] m_electrodeMap;

	protected const double CurrentThreshold = 100000.0;

	public double[] Currents => m_currents;

	public override void Awake()
	{
		base.Awake();
		m_currents = new double[4];
	}

	public virtual bool IsElectromagnetic()
	{
		return false;
	}

	protected override Electrode FindElectrode(BitDirection direction)
	{
		int num = direction.ToIndex();
		if (num != -1)
		{
			return m_electrodeMap[num];
		}
		return null;
	}

	public override void PreUpdateElements()
	{
		m_maxCurrent = 0.0;
		for (int i = 0; i < 4; i++)
		{
			m_currents[i] = 0.0;
		}
	}

	protected void OnElementUpdatedBase(CircuitSimulator simulator, SimulationResult result)
	{
		if (result.Electrode == null)
		{
			return;
		}
		m_maxCurrent = Math.Max(Math.Abs(result.I), m_maxCurrent);
		if (!IsElectromagnetic())
		{
			return;
		}
		Electrode electrode = result.Electrode;
		Electrode connectedElectrode = electrode.ConnectedElectrode;
		foreach (ConnectionData connection in m_connections)
		{
			if (connection.Electrode1 == electrode && connection.Electrode2 == connectedElectrode)
			{
				int num = connection.Direction.ToIndex();
				m_currents[num] = result.I;
			}
		}
	}

	public override void PostUpdateElements()
	{
		if (m_maxCurrent > 100000.0)
		{
			SetInvalid(invalid: true);
			RemoveAllConnections();
		}
	}

	protected float GetBrightness(float U, bool grounded)
	{
		if (!grounded)
		{
			return 0.6f;
		}
		float num = Math.Max(U, 0f);
		return num / (num + 1f) * 0.7f + 0.3f;
	}
}
