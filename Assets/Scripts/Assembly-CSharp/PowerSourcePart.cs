using System;
using System.Collections.Generic;
using UnityEngine;

public class PowerSourcePart : ElectricalPart
{
	private VoltageSource m_powerSource;

	private double m_maxCurrent;

	protected const double CurrentThreshold = 10000.0;

	public override IEnumerable<CircuitElement> ElectricalElements => m_powerSource.ToEnumerable();

	public override void CreateElectricalElements()
	{
		int num = customPartIndex - 12;
		double num2 = 0.0;
		switch (num)
		{
		case 0:
			num2 = 1.0;
			break;
		case 1:
			num2 = 5.0;
			break;
		case 2:
			num2 = 50.0;
			break;
		}
		double num3 = 1.0;
		if (m_enclosedInto is ColoredFrame)
		{
			ColoredFrame coloredFrame = (ColoredFrame)m_enclosedInto;
			if (num == 1)
			{
				num2 = 10.0;
			}
			else if (num == 2)
			{
				num2 = 100.0;
			}
			if (coloredFrame.m_partTier == PartTier.Legendary)
			{
				int num4 = coloredFrame.customPartIndex - 10;
				if (num4 < 0)
				{
					num4 = 0;
				}
				num3 = num4 % 10 + 1;
				num2 *= num3;
			}
			else if (coloredFrame.m_partTier == PartTier.Common)
			{
				int num5 = coloredFrame.customPartIndex - 12;
				if (num5 < 0)
				{
					num5 = 0;
				}
				if (num5 > 35)
				{
					num5 = 35;
				}
				if (num5 <= 8)
				{
					num3 = (num5 + 1) * 1000.0;
				}
				else if (num5 <= 18)
				{
					num3 = (num5 - 8) * 10000.0;
				}
				else if (num5 <= 26)
				{
					num3 = (num5 - 17) * 100000.0;
				}
				else
				{
					num3 = (num5 - 26) * 0.001;
				}
				num2 *= num3;
			}
			else if (coloredFrame.m_partTier == PartTier.Rare)
			{
				double[] array = new double[]
				{
					1.0, 1.2, 1.5, 1.8, 2.2, 2.7, 3.3, 3.9, 4.7, 5.6, 
					6.8, 8.2
				};
				int num6 = coloredFrame.customPartIndex - 48;
				if (num6 < 0)
				{
					num6 = 0;
				}
				if (num6 > 35)
				{
					num6 = 35;
				}
				if (num6 <= 11)
				{
					num3 = array[num6];
				}
				else if (num6 <= 23)
				{
					num3 = array[num6 - 12] * 10000.0;
				}
				else
				{
					num3 = array[num6 - 24] * 0.001;
				}
				num2 *= num3;
			}
		}
		// 允许按部件覆盖电源电压（原本由 customPartIndex / 外框决定）
		num2 = GetCustomParam("SourceVoltage", (float)num2);
		double num7 = 0.05;
		if (num2 > 0.0 && num2 / 500.0 > num7)
		{
			num7 = num2 / 500.0;
		}
		m_powerSource = new VoltageSource(num2, num7);
		m_powerSource.ElementUpdated += OnElementUpdate;
		CreateMultiplierText(num3);
	}

	// 额外可调项：电源电压（原本按 customPartIndex 硬编码 1/5/50V，并受外框影响）
	public override void CollectExtraParams(List<string> keys)
	{
		keys.Add("SourceVoltage");
	}

	public override bool TryGetExtraParamValue(string key, out float value)
	{
		value = 0f;
		if (key == "SourceVoltage")
		{
			if (m_powerSource != null)
			{
				try
				{
					System.Type t = m_powerSource.GetType();
					System.Reflection.PropertyInfo p = t.GetProperty("Voltage");
					if (p == null)
					{
						p = t.GetProperty("U");
					}
					if (p == null)
					{
						p = t.GetProperty("EMF");
					}
					if (p != null && p.CanRead && p.GetIndexParameters().Length == 0)
					{
						value = System.Convert.ToSingle(p.GetValue(m_powerSource, null));
						return true;
					}
				}
				catch
				{
				}
			}
			int idx = customPartIndex - 12;
			value = ((idx == 0) ? 1f : ((idx == 1) ? 5f : 50f));
			return true;
		}
		return base.TryGetExtraParamValue(key, out value);
	}

	private void CreateMultiplierText(double num3)
	{		Transform transform = base.transform.Find("MultiplierText");
		TextMesh textMesh;
		if (transform == null)
		{
			textMesh = new GameObject("MultiplierText")
			{
				transform =
				{
					parent = base.transform,
					localPosition = new Vector3(0f, -0.3f, -0.5f),
					localRotation = Quaternion.identity
				}
			}.AddComponent<TextMesh>();
			textMesh.font = INUnity.ArialFont;
			textMesh.fontSize = 30;
			textMesh.characterSize = 0.08f;
			textMesh.anchor = TextAnchor.MiddleCenter;
			textMesh.alignment = TextAlignment.Center;
			textMesh.color = new Color32(byte.MaxValue, 160, 70, byte.MaxValue);
		}
		else
		{
			textMesh = transform.GetComponent<TextMesh>();
		}
		if (num3 > 1.0 || (num3 > 0.0 && num3 < 1.0))
		{
			if (num3 >= 1000.0)
			{
				textMesh.text = "x" + (num3 / 1000.0).ToString("0.###") + "k";
			}
			else if (num3 < 1.0)
			{
				textMesh.text = "x" + (num3 * 1000.0).ToString("0.###") + "‰";
			}
			else
			{
				textMesh.text = "x" + num3.ToString("0.###");
			}
			textMesh.gameObject.SetActive(true);
			return;
		}
		textMesh.gameObject.SetActive(false);
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
			BitDirection.Right => m_powerSource.Anode, 
			BitDirection.Left => m_powerSource.Cathode, 
			_ => null, 
		};
	}

	public override void PreUpdateElements()
	{
		m_maxCurrent = 0.0;
	}

	private void OnElementUpdate(CircuitSimulator simulator, SimulationResult result)
	{
		if (result.Electrode != null)
		{
			m_maxCurrent = Math.Max(Math.Abs(result.I), m_maxCurrent);
		}
	}

	public override void PostUpdateElements()
	{
		if (m_maxCurrent > 10000.0)
		{
			SetInvalid(invalid: true);
			RemoveAllConnections();
		}
	}
}
