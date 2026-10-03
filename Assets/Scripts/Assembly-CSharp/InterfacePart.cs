using System;
using System.Collections.Generic;
using UnityEngine;

public class InterfacePart : WirePartBase
{
	private struct InterfaceConnectionData : IEquatable<InterfaceConnectionData>
	{
		public InterfacePart ConnectedPart;

		public CircuitElement ConnectedElement;

		public Electrode Electrode;

		public BitDirection Direction;

		public InterfaceConnectionData(InterfacePart connectedPart, CircuitElement connectedElement, BitDirection direction)
			: this(connectedPart, connectedElement, null, direction)
		{
		}

		public InterfaceConnectionData(InterfacePart connectedPart, CircuitElement connectedElement, Electrode electrode, BitDirection direction)
		{
			ConnectedPart = connectedPart;
			ConnectedElement = connectedElement;
			Electrode = electrode;
			Direction = direction;
		}

		public bool Equals(InterfaceConnectionData other)
		{
			return ConnectedElement == other.ConnectedElement;
		}

		public override int GetHashCode()
		{
			return ConnectedElement.GetHashCode();
		}
	}

	private GameObject m_sprite;

	private Wire m_wire;

	private Wire m_wire2;

	private Resistor m_resistor;

	private BitDirection m_direction;

	private List<InterfaceConnectionData> m_dynamicConnections;

	private Dictionary<InterfacePart, InterfaceConnectionData> m_newDynamicConnections;

	public Wire Wire => m_wire;

	public override IEnumerable<CircuitElement> ElectricalElements
	{
		get
		{
			if (IsMicroResistance())
			{
				yield return m_wire;
				yield return m_wire2;
				yield return m_resistor;
			}
			else
			{
				yield return m_wire;
			}
		}
	}

	public override void Awake()
	{
		base.Awake();
		m_autoAlign = (AutoAlignType)(-1);
		m_sprite = base.transform.Find("InterfaceSprite").gameObject;
		m_dynamicConnections = new List<InterfaceConnectionData>();
		m_newDynamicConnections = new Dictionary<InterfacePart, InterfaceConnectionData>();
	}

	public override void SetRotation(GridRotation rotation)
	{
		SetRotation((int)rotation);
	}

	public override void SetRotation(int rotation)
	{
		// BasePart.Awake can restore saved placement before the derived Awake caches this node.
		if (m_sprite == null) m_sprite = transform.Find("InterfaceSprite").gameObject;
		int num = (int)(m_gridRotation = (GridRotation)(rotation % 5));
		int num2;
		switch (num)
		{
		default:
			num2 = 4;
			break;
		case 1:
		case 2:
			num2 = 2;
			break;
		case 0:
			num2 = 0;
			break;
		}
		int num3 = num2 + (IsElectromagnetic() ? 1 : 0);
		INSerializedSprite component = m_sprite.GetComponent<INSerializedSprite>();
		component.SpriteName = "Interface" + (num3 + 1) + "_Sprite";
		component.UpdateMesh();
		int num4 = ((num == 2 || num == 4) ? 90 : 0);
		m_sprite.transform.localRotation = Quaternion.AngleAxis(num4, Vector3.forward);
		ApplyBuildRotation(Quaternion.identity, false);
	}

	protected override BitDirection GetConnectionDirection()
	{
		switch (GetRotation())
		{
		case 0:
			return BitDirection.Any;
		case 1:
		case 3:
			return BitDirection.LeftAndRight;
		case 2:
		case 4:
			return BitDirection.UpAndDown;
		default:
			throw new InvalidOperationException();
		}
	}

	public override bool IsElectromagnetic()
	{
		return customPartIndex == 39;
	}

	private bool IsMicroResistance()
	{
		return GetRotation() >= 3;
	}

	private CircuitElement FindElement(BitDirection direction)
	{
		if (IsMicroResistance())
		{
			if (direction != BitDirection.Right && direction != BitDirection.Up)
			{
				return m_wire;
			}
			return m_wire2;
		}
		return m_wire;
	}

	public override void CreateElectricalElements()
	{
		int count = m_connections.Count;
		if (IsMicroResistance())
		{
			m_wire = new Wire(1);
			m_wire2 = new Wire(1);
			m_resistor = new Resistor(1E-05);
			m_wire.ElementUpdated += OnElementUpdated;
			m_wire2.ElementUpdated += OnElementUpdated;
			CircuitFactory.Connect(m_wire.Electrodes[0], m_resistor.Electrode1);
			CircuitFactory.Connect(m_wire2.Electrodes[0], m_resistor.Electrode2);
			m_electrodeMap = new Electrode[4];
			{
				foreach (ConnectionData connection in m_connections)
				{
					BitDirection direction = connection.Direction;
					int num = direction.ToIndex();
					CircuitElement circuitElement = FindElement(direction);
					circuitElement.Electrodes.Add(new Electrode(circuitElement, null));
					m_electrodeMap[num] = circuitElement.Electrodes[circuitElement.Electrodes.Count - 1];
				}
				return;
			}
		}
		m_wire = new Wire(count);
		m_wire.ElementUpdated += OnElementUpdated;
		m_electrodeMap = new Electrode[4];
		for (int i = 0; i < count; i++)
		{
			int num2 = m_connections[i].Direction.ToIndex();
			m_electrodeMap[num2] = m_wire.Electrodes[i];
		}
	}

	public void Connect(InterfacePart connectedPart, BitDirection direction)
	{
		if (!m_newDynamicConnections.ContainsKey(connectedPart))
		{
			CircuitElement circuitElement = FindElement(direction);
			CircuitElement circuitElement2 = connectedPart.FindElement(direction.Reverse());
			InterfaceConnectionData interfaceConnectionData = m_dynamicConnections.Find((InterfaceConnectionData connection) => connection.ConnectedPart == connectedPart);
			if (interfaceConnectionData.ConnectedElement != null)
			{
				m_newDynamicConnections.Add(connectedPart, new InterfaceConnectionData(connectedPart, circuitElement2, interfaceConnectionData.Electrode, direction));
			}
			else if (!CircuitFactory.IsConnected(circuitElement, circuitElement2))
			{
				Electrode electrode = new Electrode(circuitElement, null);
				m_newDynamicConnections.Add(connectedPart, new InterfaceConnectionData(connectedPart, circuitElement2, electrode, direction));
			}
		}
	}

	public bool IsDynamicallyConnected(CircuitElement element)
	{
		foreach (InterfaceConnectionData dynamicConnection in m_dynamicConnections)
		{
			if (dynamicConnection.ConnectedElement == element)
			{
				return true;
			}
		}
		return false;
	}

	public void UpdateConnections()
	{
		for (int i = 0; i < m_dynamicConnections.Count; i++)
		{
			InterfaceConnectionData interfaceConnectionData = m_dynamicConnections[i];
			if (!m_newDynamicConnections.ContainsKey(interfaceConnectionData.ConnectedPart) && !IsConnected(interfaceConnectionData.ConnectedElement) && interfaceConnectionData.Electrode.IsConnected)
			{
				Electrode electrode = interfaceConnectionData.Electrode;
				Electrode connectedElectrode = interfaceConnectionData.Electrode.ConnectedElectrode;
				CircuitFactory.Disconnect(electrode, connectedElectrode);
				electrode.Element.Electrodes.Remove(electrode);
				connectedElectrode.Element.Electrodes.Remove(connectedElectrode);
			}
		}
		foreach (KeyValuePair<InterfacePart, InterfaceConnectionData> newDynamicConnection in m_newDynamicConnections)
		{
			InterfaceConnectionData value = newDynamicConnection.Value;
			if (!value.Electrode.IsConnected)
			{
				Electrode electrode2 = value.Electrode;
				Electrode electrode3 = value.ConnectedPart.m_newDynamicConnections[this].Electrode;
				CircuitFactory.Connect(electrode2, electrode3);
				electrode2.Element.Electrodes.Add(electrode2);
				electrode3.Element.Electrodes.Add(electrode3);
			}
		}
		m_dynamicConnections = new List<InterfaceConnectionData>(m_newDynamicConnections.Values);
		m_newDynamicConnections.Clear();
	}

	private void OnElementUpdated(CircuitSimulator simulator, SimulationResult result)
	{
		m_maxCurrent = Math.Max(Math.Abs(result.I), m_maxCurrent);
		if (!IsElectromagnetic() || result.Electrode == null)
		{
			return;
		}
		CircuitElement connectedElement = result.Electrode.ConnectedElement;
		foreach (ConnectionData connection in m_connections)
		{
			if (result.Element == connection.Electrode1.Element && connectedElement == connection.Electrode2.Element)
			{
				int num = connection.Direction.ToIndex();
				m_currents[num] += result.I;
				return;
			}
		}
		foreach (InterfaceConnectionData dynamicConnection in m_dynamicConnections)
		{
			if (connectedElement == dynamicConnection.ConnectedElement)
			{
				int num2 = dynamicConnection.Direction.ToIndex();
				m_currents[num2] += result.I;
				break;
			}
		}
	}
}
