using System;
using System.Collections.Generic;

public abstract class CircuitElement
{
	protected List<Electrode> m_electrodes;

	public int ElementIndex { get; set; }

	public int CircuitIndex { get; set; }

	public virtual int DefaultElectrodeCount { get; }

	public List<Electrode> Electrodes => m_electrodes;

	public IEnumerable<Electrode> ConnectedElectrodes
	{
		get
		{
			foreach (Electrode electrode in m_electrodes)
			{
				if (electrode.IsConnected)
				{
					yield return electrode;
				}
			}
		}
	}

	public event Action<CircuitSimulator, SimulationResult> ElementUpdated;

	protected CircuitElement()
	{
		CreateElectrodes(DefaultElectrodeCount);
	}

	protected void CreateElectrodes(int count)
	{
		m_electrodes = new List<Electrode>(count);
		for (int i = 0; i < count; i++)
		{
			m_electrodes.Add(new Electrode(this, null));
		}
	}

	public int GetConnectedElectrodeCount()
	{
		int num = 0;
		foreach (Electrode electrode in m_electrodes)
		{
			if (electrode.IsConnected)
			{
				num++;
			}
		}
		return num;
	}

	public int GetElectrodeIndex(Electrode electrode)
	{
		return m_electrodes.IndexOf(electrode);
	}

	public Electrode FindElectrode(CircuitElement connectedElement)
	{
		return m_electrodes.Find((Electrode electrode) => electrode.ConnectedElement == connectedElement);
	}

	public Electrode FindNextElectrode(Electrode electrode)
	{
		return m_electrodes.Find((Electrode next) => next != electrode);
	}

	public Electrode FindNextConnectedElectrode(Electrode electrode)
	{
		return m_electrodes.Find((Electrode next) => next.IsConnected && next != electrode);
	}

	public virtual bool IsNode()
	{
		return false;
	}

	public virtual void Update()
	{
	}

	public virtual void UpdateElectrode(CircuitSimulator simulator, SimulationResult result)
	{
		this.ElementUpdated?.Invoke(simulator, result);
	}
}
