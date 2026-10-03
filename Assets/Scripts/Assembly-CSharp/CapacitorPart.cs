using System;
using System.Collections.Generic;

public class CapacitorPart : ElectricalPart
{
	private Capacitor m_capacitor;

	private double m_maxU;

	public override IEnumerable<CircuitElement> ElectricalElements => m_capacitor.ToEnumerable();

	public override void CreateElectricalElements()
	{
		double capacitance;
		double resistance;
		double maxU;
		switch (customPartIndex)
		{
		case 15:
			capacitance = 0.1;
			resistance = 1.0;
			maxU = 5.0;
			break;
		case 16:
			capacitance = 1.0;
			resistance = 0.1;
			maxU = 50.0;
			break;
		case 17:
			capacitance = 10.0;
			resistance = 0.01;
			maxU = 500.0;
			break;
		default:
			throw new InvalidOperationException();
		}
		m_capacitor = new Capacitor(GetCustomParam("Capacitance", (float)capacitance), GetCustomParam("Resistance", (float)resistance));
		m_maxU = GetCustomParam("MaxVoltage", (float)maxU);
	}

	// 额外可调项：电容器的电容 / 内阻 / 耐压（原本是硬编码值，这里可按部件覆盖）
	public override void CollectExtraParams(List<string> keys)
	{
		keys.Add("Capacitance");
		keys.Add("Resistance");
		keys.Add("MaxVoltage");
	}

	public override bool ApplyExtraParam(string key, float value)
	{
		if (key == "MaxVoltage")
		{
			m_maxU = value;
			return true;
		}
		return base.ApplyExtraParam(key, value);
	}

	// 电容/内阻/耐压的默认值（避免面板显示 0）
	public override bool TryGetExtraParamValue(string key, out float value)
	{
		value = 0f;
		switch (key)
		{
			case "Capacitance":
				value = (customPartIndex == 15) ? 0.1f : ((customPartIndex == 16) ? 1f : 10f);
				return true;
			case "Resistance":
				value = (customPartIndex == 15) ? 1f : ((customPartIndex == 16) ? 0.1f : 0.01f);
				return true;
			case "MaxVoltage":
				if (m_maxU > 0.0)
				{
					value = (float)m_maxU;
				}
				else
				{
					value = (customPartIndex == 15) ? 5f : ((customPartIndex == 16) ? 50f : 500f);
				}
				return true;
		}
		return base.TryGetExtraParamValue(key, out value);
	}

	protected override BitDirection GetConnectionDirection()
	{
		return BitDirection.LeftAndRight.Rotate((int)m_gridRotation);
	}

	protected override Electrode FindElectrode(BitDirection direction)
	{
		direction = direction.Rotate(0 - m_gridRotation);
		return direction switch
		{
			BitDirection.Left => m_capacitor.Anode, 
			BitDirection.Right => m_capacitor.Cathode, 
			_ => null, 
		};
	}

	public override void SetRotation(GridRotation rotation)
	{
		int rotation2 = (int)rotation % 2;
		base.SetRotation((GridRotation)rotation2);
	}

	public override void PostUpdateElements()
	{
		double value = m_capacitor.Charge / m_capacitor.Capacitance;
		if (Math.Abs(value) > m_maxU)
		{
			SetInvalid(invalid: true);
			RemoveAllConnections();
			m_capacitor.Charge = (double)Math.Sign(value) * m_maxU * m_capacitor.Capacitance;
		}
	}
}
