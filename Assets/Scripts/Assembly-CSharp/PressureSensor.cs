using System;
using System.Collections.Generic;
using UnityEngine;

public class PressureSensor : ElectricalPart
{
	private Resistor m_resistor;

	private float m_pressure;

	private Vcc m_vccLeft;

	private Vcc m_vccRight;

	private bool m_customMode;

	public override IEnumerable<CircuitElement> ElectricalElements
	{
		get
		{
			if (m_customMode)
			{
				return new CircuitElement[] { m_vccLeft, m_vccRight };
			}
			return m_resistor.ToEnumerable();
		}
	}

	public override void CreateElectricalElements()
	{
		m_resistor = new Resistor(10000.0);
	}

	protected override BitDirection GetConnectionDirection()
	{
		return BitDirection.LeftAndRight.Rotate((int)m_gridRotation);
	}

	protected override Electrode FindElectrode(BitDirection direction)
	{
		direction = direction.Rotate(0 - m_gridRotation);
		if (direction == BitDirection.Left)
		{
			if (m_customMode)
			{
				return m_vccLeft.Electrode;
			}
			return m_resistor.Electrode1;
		}
		if (direction == BitDirection.Right)
		{
			if (m_customMode)
			{
				return m_vccRight.Electrode;
			}
			return m_resistor.Electrode2;
		}
		return null;
	}

	public override void Initialize()
	{
		base.rigidbody.sleepThreshold = 0f;
	}

	public override void Awake()
	{
		base.Awake();
		m_vccLeft = new Vcc(0.0, 0.01);
		m_vccRight = new Vcc(0.0, 0.01);
		m_customMode = false;
	}

	private void FixedUpdate()
	{
		if ((bool)base.contraption && base.contraption.IsRunning)
		{
			float pressure = m_pressure;
			m_pressure = 0f;
			if (m_customMode)
			{
				m_vccLeft.Potential = (double)(-pressure * 0.1f);
				m_vccLeft.Resistance = (double)(pressure * 0.0005f);
				m_vccRight.Potential = (double)(pressure * 0.1f);
				m_vccRight.Resistance = (double)(pressure * 0.0005f);
			}
			else
			{
				m_resistor.Resistance = Math.Min(500f / pressure, 10000f);
			}
		}
	}

	public override void OnPhysicsCollisionEnter(PhysicsCollision collision)
	{
		base.OnPhysicsCollisionEnter(collision);
		HandleCollision(collision);
	}

	public override void OnPhysicsCollisionStay(PhysicsCollision collision)
	{
		base.OnPhysicsCollisionStay(collision);
		HandleCollision(collision);
	}

	private void HandleCollision(PhysicsCollision collision)
	{
		Vector3 position = base.transform.position;
		Vector3 right = base.transform.right;
		bool flag = false;
		PhysicsContactPoint[] contacts = collision.contacts;
		foreach (PhysicsContactPoint contactPoint in contacts)
		{
			Vector3 point = contactPoint.point;
			if (Vector.Cross2(right, point - position) > 0.4f)
			{
				flag = true;
				break;
			}
		}
		if (flag)
		{
			float num = Math.Abs(Vector.Cross2(right, collision.impulse));
			m_pressure += num / Time.fixedDeltaTime;
		}
	}

	protected override void OnTouch()
	{
		if (m_customMode)
		{
			SwitchToOriginal();
		}
		else
		{
			SwitchToCustom();
		}
	}

	private void SwitchToCustom()
	{
		m_customMode = true;
		if (m_connections != null)
		{
			for (int i = 0; i < m_connections.Count; i++)
			{
				ConnectionData connectionData = m_connections[i];
				if (connectionData.Electrode1 != null && connectionData.Electrode2 != null)
				{
					if (connectionData.Electrode1 == m_resistor.Electrode1)
					{
						Electrode electrode = m_vccLeft.Electrode;
						electrode.Connect(connectionData.Electrode2);
						connectionData.Electrode2.Connect(electrode);
					}
					else if (connectionData.Electrode1 == m_resistor.Electrode2)
					{
						Electrode electrode2 = m_vccRight.Electrode;
						electrode2.Connect(connectionData.Electrode2);
						connectionData.Electrode2.Connect(electrode2);
					}
				}
			}
		}
	}

	private void SwitchToOriginal()
	{
		m_customMode = false;
		if (m_connections != null)
		{
			for (int i = 0; i < m_connections.Count; i++)
			{
				ConnectionData connectionData = m_connections[i];
				if (connectionData.Electrode1 != null && connectionData.Electrode2 != null)
				{
					connectionData.Electrode1.Connect(connectionData.Electrode2);
					connectionData.Electrode2.Connect(connectionData.Electrode1);
				}
			}
		}
	}
}
