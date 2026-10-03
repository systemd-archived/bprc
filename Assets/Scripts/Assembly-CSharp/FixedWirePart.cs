using System;
using System.Collections.Generic;
using UnityEngine;

public class FixedWirePart : WirePartBase
{
	private GameObject m_wireObjectX;

	private GameObject m_wireObjectY;

	private Wire m_wireX;

	private Wire m_wireY;

	private bool m_invalidX;

	private bool m_invalidY;

	private double m_maxIX;

	private double m_maxIY;

	public override IEnumerable<CircuitElement> ElectricalElements
	{
		get
		{
			if (m_wireX != null)
			{
				yield return m_wireX;
			}
			if (m_wireY != null)
			{
				yield return m_wireY;
			}
		}
	}

	public override void Awake()
	{
		base.Awake();
		m_autoAlign = (AutoAlignType)(-1);
		m_wireObjectX = base.transform.Find("WireX").gameObject;
		m_wireObjectY = base.transform.Find("WireY").gameObject;
	}

	public override void SetRotation(GridRotation rotation)
	{
		SetRotation((int)rotation);
	}

	public override void SetRotation(int rotation)
	{
		int num = (int)(m_gridRotation = (GridRotation)(rotation % 7));
		int num2;
		float angle;
		if (num <= 1)
		{
			num2 = 0;
			angle = (float)num * 90f;
		}
		else if (num <= 5)
		{
			num2 = 1;
			angle = (float)(num - 2) * 90f;
		}
		else
		{
			num2 = 0;
			angle = 0f;
		}
		INSerializedSprite component = m_wireObjectX.GetComponent<INSerializedSprite>();
		component.SpriteName = "Wire2_Sprite_" + (num2 + 1);
		component.UpdateMesh();
		m_wireObjectX.SetActive(value: true);
		m_wireObjectY.SetActive(num == 6);
		m_wireObjectX.transform.localRotation = Quaternion.AngleAxis(angle, Vector3.forward);
		ApplyBuildRotation(Quaternion.identity, false);
	}

	public override bool IsElectromagnetic()
	{
		return customPartIndex == 3;
	}

	public override void CreateElectricalElements()
	{
		int rotation = GetRotation();
		m_electrodeMap = new Electrode[4];
		if (rotation <= 1)
		{
			m_wireX = new Wire(2);
			m_wireX.ElementUpdated += OnElementUpdatedX;
			m_electrodeMap[rotation] = m_wireX.Electrodes[0];
			m_electrodeMap[rotation + 2] = m_wireX.Electrodes[1];
			return;
		}
		if (rotation <= 5)
		{
			m_wireX = new Wire(2);
			m_wireX.ElementUpdated += OnElementUpdatedX;
			m_electrodeMap[rotation - 2] = m_wireX.Electrodes[0];
			m_electrodeMap[(rotation + 1) % 4] = m_wireX.Electrodes[1];
			return;
		}
		m_wireX = new Wire(2);
		m_wireX.ElementUpdated += OnElementUpdatedX;
		m_wireY = new Wire(2);
		m_wireY.ElementUpdated += OnElementUpdatedY;
		m_electrodeMap[0] = m_wireX.Electrodes[0];
		m_electrodeMap[1] = m_wireY.Electrodes[0];
		m_electrodeMap[2] = m_wireX.Electrodes[1];
		m_electrodeMap[3] = m_wireY.Electrodes[1];
	}

	protected override BitDirection GetConnectionDirection()
	{
		int rotation = GetRotation();
		if (rotation <= 1)
		{
			return BitDirection.LeftAndRight.Rotate(rotation);
		}
		if (rotation <= 5)
		{
			return ((BitDirection)9).Rotate(rotation - 2);
		}
		return BitDirection.Any;
	}

	public override void PreUpdateElements()
	{
		m_maxIX = 0.0;
		m_maxIY = 0.0;
	}

	private void OnElementUpdatedX(CircuitSimulator simulator, SimulationResult result)
	{
		OnElementUpdatedBase(simulator, result);
		m_maxIX = Math.Max(Math.Abs(result.I), m_maxIX);
		float brightness = GetBrightness((float)result.U, result.IsGrounded);
		m_wireObjectX.GetComponent<MeshRenderer>().material.color = new Color(brightness, brightness, brightness, 1f);
	}

	private void OnElementUpdatedY(CircuitSimulator simulator, SimulationResult result)
	{
		OnElementUpdatedBase(simulator, result);
		m_maxIY = Math.Max(Math.Abs(result.I), m_maxIY);
		float brightness = GetBrightness((float)result.U, result.IsGrounded);
		m_wireObjectY.GetComponent<MeshRenderer>().material.color = new Color(brightness, brightness, brightness, 1f);
	}

	public override void PostUpdateElements()
	{
		if (m_maxIX > 100000.0 && !m_invalidX)
		{
			m_invalidX = true;
			ToGray(m_wireObjectX, gray: true);
			RemoveConnections((ConnectionData connection) => (connection.Direction & BitDirection.LeftAndRight) != 0);
		}
		if (m_maxIY > 100000.0 && !m_invalidY)
		{
			m_invalidY = true;
			ToGray(m_wireObjectY, gray: true);
			RemoveConnections((ConnectionData connection) => (connection.Direction & BitDirection.UpAndDown) != 0);
		}
	}
}
