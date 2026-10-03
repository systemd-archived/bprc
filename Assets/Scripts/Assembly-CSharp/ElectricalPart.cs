using System;
using System.Collections.Generic;
using UnityEngine;

public class ElectricalPart : BasePart
{
	public enum LogicLevel
	{
		Invalid = 0,
		Low = 1,
		High = 2
	}

	protected struct ConnectionData
	{
		public ElectricalPart Part1;

		public ElectricalPart Part2;

		public Electrode Electrode1;

		public Electrode Electrode2;

		public Joint Joint1;

		public Joint Joint2;

		public BitDirection Direction;

		public bool IsInvalid()
		{
			if (!(Part1 == null) && !(Part2 == null))
			{
				if (Joint1 == null)
				{
					return Joint2 == null;
				}
				return false;
			}
			return true;
		}

		public ConnectionData(ElectricalPart part1, ElectricalPart part2, BitDirection direction)
			: this(part1, part2, null, null, null, null, direction)
		{
		}

		public ConnectionData(ElectricalPart part1, ElectricalPart part2, Electrode electrode1, Electrode electrode2, Joint joint1, Joint joint2, BitDirection direction)
		{
			Part1 = part1;
			Part2 = part2;
			Electrode1 = electrode1;
			Electrode2 = electrode2;
			Joint1 = joint1;
			Joint2 = joint2;
			Direction = direction;
		}
	}

	protected bool m_invalid;

	protected List<ConnectionData> m_connections;

	public virtual IEnumerable<CircuitElement> ElectricalElements { get; }

	public override void Awake()
	{
		m_connections = new List<ConnectionData>();
		m_ZOffset = 0.01f;
		m_jointConnectionDirection = ((this is WirePart || this is FixedWirePart || this is PointChargePart) ? JointConnectionDirection.None : JointConnectionDirection.Any);
	}

	public bool IsValid()
	{
		return !m_invalid;
	}

	public override void CreateCustomJoints()
	{
		BasePart[] array = FindConnectedParts();
		if (array == null)
		{
			return;
		}
		for (int i = 0; i < array.Length; i++)
		{
			BasePart basePart = array[i];
			if (!(basePart == null))
			{
				base.contraption.AddFixedJoint(this, basePart);
				if (basePart is ElectricalPart part)
				{
					BitDirection direction = (BitDirection)(1 << i);
					ConnectionData item = new ConnectionData(this, part, direction);
					m_connections.Add(item);
				}
			}
		}
	}

	protected virtual BitDirection GetConnectionDirection()
	{
		return BitDirection.None;
	}

	protected virtual Electrode FindElectrode(BitDirection direction)
	{
		return null;
	}

	protected BasePart[] FindConnectedParts()
	{
		ElectricalPart electricalPart = FindConnectedPart(1, 0, BitDirection.Right);
		ElectricalPart electricalPart2 = FindConnectedPart(0, 1, BitDirection.Up);
		ElectricalPart electricalPart3 = FindConnectedPart(-1, 0, BitDirection.Left);
		ElectricalPart electricalPart4 = FindConnectedPart(0, -1, BitDirection.Down);
		return new BasePart[4] { electricalPart, electricalPart2, electricalPart3, electricalPart4 };
	}

	protected ElectricalPart FindConnectedPart(int x, int y, BitDirection direction)
	{
		BasePart basePart = base.contraption.FindPartAt(m_coordX + x, m_coordY + y, this);
		if (basePart != null)
		{
			if (basePart.m_enclosedPart != null)
			{
				basePart = basePart.m_enclosedPart;
			}
			if (CanConnectTo(basePart, direction))
			{
				return (ElectricalPart)basePart;
			}
		}
		return null;
	}

	public bool CanConnectTo(BitDirection direction)
	{
		return (GetConnectionDirection() & direction) != 0;
	}

	public bool CanConnectTo(BasePart part, BitDirection direction)
	{
		if (part is ElectricalPart electricalPart)
		{
			return (GetConnectionDirection() & electricalPart.GetConnectionDirection().Reverse() & direction) != 0;
		}
		return false;
	}

	public bool IsConnected(CircuitElement element)
	{
		foreach (ConnectionData connection in m_connections)
		{
			if (connection.Electrode2 != null && connection.Electrode2.Element == element)
			{
				return true;
			}
		}
		return false;
	}

	public virtual void CreateElectricalElements()
	{
	}

	public virtual void ConnectElectricalElements()
	{
		if (m_connections == null)
		{
			return;
		}
		foreach (ConnectionData connection in m_connections)
		{
			Electrode electrode = connection.Electrode1;
			Electrode electrode2 = connection.Electrode2;
			if (electrode == null || electrode2 == null) continue;
			electrode.Connect(electrode2);
		}
	}

	public virtual void InitializeElectricalElements()
	{
	}

	public virtual void PreUpdateElements()
	{
	}

	public virtual void PostUpdateElements()
	{
	}

	public void InitializeConnections()
	{
		for (int i = 0; i < m_connections.Count; i++)
		{
			ConnectionData value = m_connections[i];
			ElectricalPart part = value.Part1;
			ElectricalPart part2 = value.Part2;
			Rigidbody rigidbody = part.rigidbody;
			Rigidbody rigidbody2 = part2.rigidbody;
			BitDirection direction = value.Direction;
			value.Electrode1 = part.FindElectrode(direction);
			value.Electrode2 = part2.FindElectrode(direction.Reverse());
			value.Joint1 = rigidbody.FindSpecifiedJoint(rigidbody2);
			value.Joint2 = rigidbody2.FindSpecifiedJoint(rigidbody);
			m_connections[i] = value;
		}
	}

	public void DisableAllConnections()
	{
		foreach (CircuitElement electricalElement in ElectricalElements)
		{
			List<Electrode> electrodes = electricalElement.Electrodes;
			for (int i = 0; i < electrodes.Count; i++)
			{
				Electrode electrode = electrodes[i];
				electrode.Switch(closed: false);
				electrodes[i] = electrode;
			}
		}
	}

	public void RemoveAllConnections()
	{
		if (m_connections == null) return;
		foreach (ConnectionData connection in m_connections)
		{
			try { CircuitFactory.Disconnect(connection.Electrode1, connection.Electrode2); } catch { }
		}
		m_connections.Clear();
	}

	public void RemoveInvalidConnections()
	{
		foreach (ConnectionData connection in m_connections)
		{
			if (connection.IsInvalid())
			{
				CircuitFactory.Disconnect(connection.Electrode1, connection.Electrode2);
			}
		}
		m_connections.RemoveAll((ConnectionData connection) => connection.IsInvalid());
	}

	protected void RemoveConnections(Predicate<ConnectionData> match)
	{
		foreach (ConnectionData connection in m_connections)
		{
			if (match(connection))
			{
				CircuitFactory.Disconnect(connection.Electrode1, connection.Electrode2);
			}
		}
		m_connections.RemoveAll(match);
	}

	private void OnDestroy()
	{
		RemoveAllConnections();
	}

	protected void SetInvalid(bool invalid)
	{
		if (m_invalid != invalid)
		{
			m_invalid = invalid;
			ToGray(base.gameObject, invalid);
		}
	}

	protected void ToGray(GameObject gameObject, bool gray)
	{
		Shader shader = INUnity.LoadShader(gray ? "PreAlpha_Unlit_ColorTransparent_Geometry_Gray" : "PreAlpha_Unlit_ColorTransparent_Geometry");
		MeshRenderer[] componentsInChildren = gameObject.GetComponentsInChildren<MeshRenderer>();
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			componentsInChildren[i].material.shader = shader;
		}
	}

	public static LogicLevel GetLogicLevel(double voltage)
	{
		if (-10.0 <= voltage && voltage <= 2.5)
		{
			return LogicLevel.Low;
		}
		if (2.5 < voltage && voltage <= 10.0)
		{
			return LogicLevel.High;
		}
		return LogicLevel.Invalid;
	}
}
