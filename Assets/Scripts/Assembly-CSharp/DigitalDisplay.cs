using System;
using System.Collections.Generic;
using UnityEngine;

public class DigitalDisplay : ElectricalPart
{
	private TextMesh m_text;

	private Wire[] m_inputs;

	private ElectricalPart.LogicLevel[] m_levels;

	private Vcc[] m_outputs;

	private bool m_customMode;

	private int m_value;

	private bool m_pressed;

	private float m_pressTime;

	private BasePart m_targetPart;

	private float m_initialAngle;

	private double m_distanceOutput;

	private double m_angleOutput;

	private double m_angleOutputUp;

	private double m_angleSelf;

	private bool m_initialized;

	private int m_lastFrameIndex;

	private int no90;

	private double[] m_voltages;

	private double m_latchValue;

	private Resistor m_bResistor;

	private VoltageSource m_cSource;

	private Resistor m_cSample;

	private double m_cCurrent;

	private Transform m_visual;

	private bool m_visualSeparated;

	private double m_cResistance;

	private int m_leftFrames;

	private float m_pwmTick;

	private double m_lastTickVoltage;

	private bool m_lastDividerHigh;

	private bool m_outputHigh;

	public override IEnumerable<CircuitElement> ElectricalElements
	{
		get
		{
			if (!m_customMode)
			{
				return m_inputs;
			}
			if (m_value == 2)
			{
				return new List<CircuitElement>
				{
					m_outputs[0],
					m_outputs[1],
					m_outputs[2],
					m_outputs[3]
				};
			}
			if (m_value == 9)
			{
				return new List<CircuitElement>
				{
					m_inputs[0],
					m_inputs[2],
					m_inputs[3],
					m_outputs[1]
				};
			}
			if (m_value == 10)
			{
				return new List<CircuitElement>
				{
					m_inputs[1],
					m_inputs[3],
					m_outputs[0],
					m_outputs[2]
				};
			}
			if (m_value == 11)
			{
				return new List<CircuitElement>
				{
					m_bResistor,
					m_inputs[1],
					m_inputs[3]
				};
			}
			if (m_value == 12)
			{
				return new List<CircuitElement>
				{
					m_cSource,
					m_cSample,
					m_outputs[0],
					m_outputs[2]
				};
			}
			if (m_value == 13 || m_value == 14)
			{
				return new List<CircuitElement>
				{
					m_inputs[1],
					m_inputs[2],
					m_outputs[0],
					m_outputs[3]
				};
			}
			if (m_value != 15 && (no90 != 1 || m_value < 1001 || m_value > 1119))
			{
				return m_outputs;
			}
			List<CircuitElement> list = new List<CircuitElement>();
			if (no90 != 1 || m_value < 1001 || m_value > 1119)
			{
				list.Add(m_inputs[1]);
				list.Add(m_inputs[3]);
				list.Add(m_outputs[0]);
				list.Add(m_outputs[2]);
				return list;
			}
			int num = m_value - 1000;
			if (no90 == 1 && m_value == 1040)
			{
				return new List<CircuitElement>
				{
					m_inputs[1],
					m_inputs[2],
					m_inputs[3],
					m_outputs[0]
				};
			}
			if (num == 30 || num == 32)
			{
				if (num == 32)
				{
					list.Add(m_inputs[2]);
				}
				for (int i = 0; i < 4; i++)
				{
					list.Add(m_outputs[i]);
				}
				return list;
			}
			if (num == 31)
			{
				list.Add(m_inputs[1]);
				list.Add(m_inputs[2]);
				list.Add(m_inputs[3]);
				list.Add(m_outputs[0]);
				return list;
			}
			if (num == 1 || num == 2 || num == 3 || num == 4 || num == 11 || num == 12 || num == 19 || num == 20 || num == 23 || num == 27 || num == 28 || num == 29 || num == 41 || num == 42 || num == 43)
			{
				list.Add(m_inputs[1]);
				list.Add(m_inputs[3]);
			}
			else
			{
				list.Add(m_inputs[2]);
			}
			for (int j = 0; j < 4; j++)
			{
				list.Add(m_outputs[j]);
			}
			return list;
		}
	}

	public override void Awake()
	{
		base.Awake();
		if (m_gridRotation == BasePart.GridRotation.Deg_0 || m_gridRotation == BasePart.GridRotation.Deg_90 || m_gridRotation == BasePart.GridRotation.Deg_180 || m_gridRotation == BasePart.GridRotation.Deg_270)
		{
			no90 = 0;
		}
		else if (m_gridRotation == BasePart.GridRotation.Deg_45 || m_gridRotation == BasePart.GridRotation.Deg_135 || m_gridRotation == BasePart.GridRotation.Deg_225 || m_gridRotation == BasePart.GridRotation.Deg_315)
		{
			no90 = 1;
		}
		m_text = base.transform.Find("Text").GetComponent<TextMesh>();
		m_text.text = "0";
		m_text.color = new Color32(18, 98, 179, byte.MaxValue);
		m_outputs = new Vcc[4];
		for (int i = 0; i < 4; i++)
		{
			m_outputs[i] = new Vcc(0.0, 0.01);
		}
		m_voltages = new double[4];
		m_customMode = false;
		m_value = 0;
		m_pressed = false;
		m_initialized = false;
		m_latchValue = 0.0;
		m_bResistor = new Resistor(10000.0);
		m_cSource = new VoltageSource(1.0, 0.01);
		m_cSample = new Resistor(0.01);
		CircuitFactory.Connect(m_cSource.Anode, m_cSample.Electrode1);
		m_cSample.ElementUpdated += OnElementUpdated;
	}

	public override void SetRotation(BasePart.GridRotation rotation)
	{
		base.SetRotation(rotation);
		if (rotation == BasePart.GridRotation.Deg_0 || rotation == BasePart.GridRotation.Deg_90 || rotation == BasePart.GridRotation.Deg_180 || rotation == BasePart.GridRotation.Deg_270)
		{
			no90 = 0;
		}
		else
		{
			no90 = 1;
		}
		EnsureVisualSeparated();
		ApplyOrientation();
		m_text.transform.rotation = Quaternion.identity;
		if (m_customMode)
		{
			int num = -1;
			if (m_enclosedInto is ColoredFrame)
			{
				num = ((ColoredFrame)m_enclosedInto).customPartIndex;
			}
			if (num != -1)
			{
				int num2 = num - 12;
				if (num2 < 0)
				{
					num2 = 0;
				}
				if (no90 == 1)
				{
					int num3 = num2 + 1001;
					if (num3 < 1001)
					{
						num3 = 1001;
					}
					if (num3 > 1118)
					{
						num3 = 1118;
					}
					m_value = num3;
				}
				else
				{
					m_value = num2 % 16;
				}
				SwitchToCustom();
			}
		}
	}

	private void EnsureVisualSeparated()
	{
		if (m_visualSeparated)
		{
			return;
		}
		MeshFilter meshFilter = GetComponent<MeshFilter>();
		MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
		INSerializedSprite sprite = GetComponent<INSerializedSprite>();
		if (meshFilter == null || meshRenderer == null)
		{
			return;
		}
		if (meshFilter.sharedMesh == null)
		{
			return;
		}
		m_visualSeparated = true;
		GameObject visualObject = new GameObject("Visual");
		visualObject.transform.SetParent(base.transform, worldPositionStays: false);
		MeshFilter visualMeshFilter = visualObject.AddComponent<MeshFilter>();
		visualMeshFilter.sharedMesh = meshFilter.sharedMesh;
		MeshRenderer visualMeshRenderer = visualObject.AddComponent<MeshRenderer>();
		visualMeshRenderer.sharedMaterials = meshRenderer.sharedMaterials;
		if (sprite != null)
		{
			INSerializedSprite visualSprite = visualObject.AddComponent<INSerializedSprite>();
			visualSprite.SpriteName = sprite.SpriteName;
			try
			{
				visualSprite.UpdateMesh();
			}
			catch
			{
			}
		}
		DestroyComponent(meshFilter);
		DestroyComponent(meshRenderer);
		if (sprite != null)
		{
			DestroyComponent(sprite);
		}
		m_visual = visualObject.transform;
	}

	private void DestroyComponent(Component component)
	{
		if (Application.isPlaying)
		{
			Destroy(component);
		}
		else
		{
			DestroyImmediate(component);
		}
	}

	private void ApplyOrientation()
	{
		Transform visualization = m_visual;
		if (no90 == 1)
		{
			GridRotation ortho = (GridRotation)(((int)m_gridRotation - (int)GridRotation.Deg_45) % 4);
		ApplyBuildRotation(Quaternion.AngleAxis(GetRotationAngle(ortho), Vector3.forward), false);
			if ((bool)visualization)
			{
				visualization.localRotation = Quaternion.AngleAxis(45f, Vector3.forward);
			}
		}
		else
		{
		ApplyBuildRotation(Quaternion.AngleAxis(GetRotationAngle(m_gridRotation), Vector3.forward), false);
			if ((bool)visualization)
			{
				visualization.localRotation = Quaternion.identity;
			}
		}
	}

	public override void CreateElectricalElements()
	{
		m_inputs = new Wire[4];
		m_levels = new ElectricalPart.LogicLevel[4];
		for (int i = 0; i < 4; i++)
		{
			Wire wire = new Wire(1);
			wire.ElementUpdated += OnElementUpdated;
			m_inputs[i] = wire;
		}
	}

	protected override BitDirection GetConnectionDirection()
	{
		return BitDirection.Any;
	}

	protected override Electrode FindElectrode(BitDirection direction)
	{
		direction = direction.Rotate(0 - m_gridRotation);
		int num = direction.ToIndex();
		if (num != -1)
		{
			if (m_customMode)
			{
				if (num == 0)
				{
					return m_outputs[0].Electrode;
				}
				if (num == 2)
				{
					return m_outputs[2].Electrode;
				}
				return m_inputs[num].Electrodes[0];
			}
			return m_inputs[num].Electrodes[0];
		}
		return null;
	}

	private void OnElementUpdated(CircuitSimulator simulator, SimulationResult result)
	{
		if (!m_customMode)
		{
			for (int i = 0; i < 4; i++)
			{
				if (m_inputs[i] == result.Element)
				{
					m_levels[i] = ElectricalPart.GetLogicLevel(result.U);
				}
			}
			return;
		}
		for (int j = 0; j < 4; j++)
		{
			if (m_inputs[j] == result.Element)
			{
				m_voltages[j] = result.U;
			}
		}
		if (m_bResistor == result.Element)
		{
			m_cCurrent = result.I;
		}
		if (m_cSample == result.Element)
		{
			m_cCurrent = result.I;
		}
	}

	public override void PreUpdateElements()
	{
		for (int i = 0; i < 4; i++)
		{
			m_levels[i] = LogicLevel.Invalid;
		}
		m_voltages[0] = 0.0;
		m_voltages[1] = 0.0;
		m_voltages[2] = 0.0;
		m_voltages[3] = 0.0;
	}

	public override void PostUpdateElements()
	{
		if (m_customMode && m_value == 1)
		{
			Vector3 velocity = base.rigidbody.velocity;
			Vector3 vector = base.transform.InverseTransformDirection(velocity);
			m_outputs[0].Potential = (double)velocity.x;
			m_outputs[1].Potential = (double)velocity.y;
			m_outputs[2].Potential = (double)vector.x;
			m_outputs[3].Potential = (double)vector.y;
			for (int i = 0; i < 4; i++)
			{
				m_outputs[i].Resistance = Math.Abs(m_outputs[i].Potential) * 0.1;
			}
			return;
		}
		if (m_customMode && m_value == 2)
		{
			Vector3 position = base.transform.position;
			float num = 16384f;
			BasePart basePart = null;
			RaycastHit[] array = Physics.RaycastAll(position, base.transform.right, 16384f);
			for (int j = 0; j < array.Length; j++)
			{
				BasePart componentInParent = array[j].collider.GetComponentInParent<BasePart>();
				if (componentInParent != null && componentInParent != this && !MarkerManager.IsInSameTeamStatic(this, componentInParent) && array[j].distance < num)
				{
					num = array[j].distance;
					basePart = componentInParent;
				}
			}
			double num2;
			if (num <= 2f)
			{
				num2 = 26.0;
			}
			else if (num >= 16384f)
			{
				num2 = 0.0;
			}
			else if (num <= 64f)
			{
				num2 = 26.0 * (1.0 - Math.Log((double)num / 2.0, 2.0) / 10.0);
			}
			else
			{
				num2 = 13.0 * (1.0 - Math.Log((double)num / 64.0, 2.0) / 8.0);
			}
			if (num2 < 0.0)
			{
				num2 = 0.0;
			}
			double num3 = 0.0;
			if (basePart != null)
			{
				num3 = Math.Sqrt((double)base.contraption.ComponentPartCount(basePart.ConnectedComponent));
			}
			m_outputs[2].Potential = num2;
			m_outputs[0].Potential = num3;
			m_outputs[1].Potential = num3;
			m_outputs[3].Potential = num2;
			for (int k = 0; k < 4; k++)
			{
				m_outputs[k].Resistance = Math.Abs(m_outputs[k].Potential) * 0.01;
			}
			return;
		}
		if (m_customMode && m_value == 3)
		{
			UpdateTargeting(sameTeam: true);
			for (int n = 0; n < 4; n++)
			{
				m_outputs[n].Potential = 0.0;
			}
			m_outputs[0].Potential = m_angleSelf;
			m_outputs[1].Potential = m_distanceOutput;
			m_outputs[2].Potential = m_distanceOutput;
			m_outputs[3].Potential = m_angleOutput;
			return;
		}
		if (m_customMode && m_value == 9)
		{
			double num4 = m_voltages[3];
			double num5 = 0.0;
			float num6 = float.MaxValue;
			foreach (BasePart basePart2 in base.contraption.Parts)
			{
				DigitalDisplay digitalDisplay = basePart2 as DigitalDisplay;
				if (digitalDisplay != null && digitalDisplay.m_customMode && digitalDisplay.m_value == 9 && Math.Abs(digitalDisplay.m_voltages[2] - num4) < 0.001)
				{
					float num7 = Vector3.Distance(digitalDisplay.transform.position, base.transform.position);
					if (num7 < num6)
					{
						num6 = num7;
						num5 = digitalDisplay.m_voltages[0];
					}
				}
			}
			m_outputs[1].Potential = num5;
			m_outputs[1].Resistance = Math.Abs(num5) / 100.0;
			m_outputs[0].Potential = 0.0;
			m_outputs[2].Potential = 0.0;
			m_outputs[3].Potential = 0.0;
			return;
		}
		if (m_customMode && m_value == 10)
		{
			double num8 = m_voltages[1];
			double num9 = m_voltages[3];
			int num11;
			m_leftFrames++;
			m_outputs[2].Potential = ((m_leftFrames <= 1) ? 0.0 : 5.0);
			m_outputs[2].Resistance = 0.05;
			double num10;
			if (num8 > 0.0)
			{
				num10 = num8 / 0.1;
			}
			else if (num8 < 0.0)
			{
				num10 = -num8 / 0.1 * 5.0;
			}
			else
			{
				num10 = 2.0;
			}
			if (double.IsNaN(num10) || double.IsInfinity(num10))
			{
				num10 = 2.0;
			}
			if (num10 < 2.0)
			{
				num10 = 2.0;
			}
			num11 = (int)Math.Ceiling(num10);
			float num12 = 0.5f - (float)num9 * 0.05f;
			if (num12 < 0f)
			{
				num12 = 0f;
			}
			if (num12 > 1f)
			{
				num12 = 1f;
			}
			m_pwmTick += 1f;
			float num13 = m_pwmTick % (float)num11;
			float num14 = (float)num11 * num12;
			m_outputs[0].Potential = ((num13 < num14) ? 5.0 : 0.0);
			m_outputs[0].Resistance = 0.05;
			m_outputs[1].Potential = 0.0;
			m_outputs[3].Potential = 0.0;
			return;
		}
		if (m_customMode && m_value == 11)
		{
			m_bResistor.Resistance = Math.Abs(m_voltages[1] - m_voltages[3]);
			return;
		}
		if (m_customMode && m_value == 12)
		{
			if (m_cCurrent != 0.0)
			{
				m_cResistance = 1.0 / m_cCurrent - 0.02;
			}
			m_outputs[0].Potential = m_cResistance;
			m_outputs[2].Potential = 0.0 - m_cResistance;
			m_outputs[0].Resistance = Math.Abs(m_cResistance) * 0.1;
			m_outputs[2].Resistance = Math.Abs(m_cResistance) * 0.1;
			m_outputs[1].Potential = 0.0;
			m_outputs[3].Potential = 0.0;
			return;
		}
		if (m_customMode && m_value == 13)
		{
			if (m_levels[1] == ElectricalPart.LogicLevel.High)
			{
				m_latchValue = m_voltages[2];
			}
			m_outputs[0].Potential = m_latchValue;
			m_outputs[0].Resistance = Math.Abs(m_latchValue) * 0.1;
			m_outputs[3].Potential = 0.0 - m_latchValue;
			m_outputs[3].Resistance = Math.Abs(m_latchValue) * 0.1;
			m_outputs[1].Potential = 0.0;
			m_outputs[2].Potential = 0.0;
			return;
		}
		if (m_customMode && m_value == 14)
		{
			m_outputs[0].Potential = ((m_voltages[2] > 0.0) ? 5.0 : 0.0);
			m_outputs[3].Potential = ((m_levels[1] == ElectricalPart.LogicLevel.High) ? 1.0 : 0.0);
			m_outputs[0].Resistance = 0.05;
			m_outputs[3].Resistance = 0.05;
			m_outputs[1].Potential = 0.0;
			m_outputs[2].Potential = 0.0;
			return;
		}
		if (m_customMode && m_value == 15)
		{
			bool flag = m_levels[1] == ElectricalPart.LogicLevel.High;
			bool flag2 = m_levels[3] == ElectricalPart.LogicLevel.High;
			bool flag3 = flag ^ flag2;
			m_outputs[0].Resistance = 0.05;
			m_outputs[2].Resistance = 0.05;
			m_outputs[0].Potential = (flag3 ? 0.0 : 5.0);
			m_outputs[2].Potential = (flag3 ? 5.0 : 0.0);
			m_outputs[1].Potential = 0.0;
			m_outputs[3].Potential = 0.0;
			return;
		}
		if (m_customMode && no90 == 1 && m_value >= 1001 && m_value <= 1119)
		{
			int num15 = m_value - 1000;
			if (num15 == 39)
			{
				bool flag4 = m_voltages[2] > 2.5;
				if (!flag4 && m_lastDividerHigh)
				{
					m_outputHigh = !m_outputHigh;
				}
				m_lastDividerHigh = flag4;
				m_outputs[0].Potential = (m_outputHigh ? 5.0 : 0.0);
				m_outputs[0].Resistance = 0.05;
				m_outputs[1].Potential = 0.0;
				m_outputs[2].Potential = 0.0;
				m_outputs[3].Potential = 0.0;
				return;
			}
			if (num15 == 30)
			{
				m_outputs[0].Potential = 2.718281828459045;
				m_outputs[1].Potential = 3.141592653589793;
				m_outputs[2].Potential = 57.29577951308232;
				m_outputs[3].Potential = -1.0 * (double)Physics.gravity.y;
				for (int l = 0; l < 4; l++)
				{
					m_outputs[l].Resistance = Math.Abs(m_outputs[l].Potential) * 0.1;
				}
				return;
			}
			if (num15 == 31)
			{
				double num16 = m_voltages[1];
				double num17 = m_voltages[3];
				if (num17 < num16)
				{
					double num18 = num16;
					num16 = num17;
					num17 = num18;
				}
				double num19 = (double)UnityEngine.Random.Range((float)num16, (float)num17);
				if (m_levels[2] == ElectricalPart.LogicLevel.High)
				{
					num19 = Math.Round(num19);
				}
				m_outputs[0].Potential = num19;
				m_outputs[0].Resistance = Math.Abs(num19) * 0.1;
				m_outputs[1].Potential = 0.0;
				m_outputs[2].Potential = 0.0;
				m_outputs[3].Potential = 0.0;
				return;
			}
			if (num15 == 32)
			{
				double num20 = m_voltages[2];
				double num21 = num20 - m_lastTickVoltage;
				m_lastTickVoltage = num20;
				m_outputs[0].Potential = num21;
				m_outputs[0].Resistance = Math.Abs(num21) * 0.1;
				m_outputs[1].Potential = 0.0;
				m_outputs[2].Potential = 0.0;
				m_outputs[3].Potential = 0.0;
				return;
			}
			if (num15 == 40)
			{
				double num22 = m_voltages[2];
				double num23 = m_voltages[1];
				double num24 = m_voltages[3];
				double num25 = Math.Max(num23, num24);
				double num26 = Math.Min(num23, num24);
				double num27 = num22;
				if (num27 > num25)
				{
					num27 = num25;
				}
				if (num27 < num26)
				{
					num27 = num26;
				}
				m_outputs[0].Potential = num27;
				m_outputs[0].Resistance = Math.Abs(num27) / 10.0;
				m_outputs[1].Potential = 0.0;
				m_outputs[2].Potential = 0.0;
				m_outputs[3].Potential = 0.0;
				return;
			}
			double num28 = ((num15 == 1 || num15 == 2 || num15 == 3 || num15 == 4 || num15 == 11 || num15 == 12 || num15 == 19 || num15 == 20 || num15 == 23 || num15 == 27 || num15 == 28 || num15 == 29 || num15 == 41 || num15 == 42 || num15 == 43) ? m_voltages[1] : m_voltages[2]);
			double num29 = m_voltages[3];
			for (int m = 0; m < 4; m++)
			{
				m_outputs[m].Potential = 0.0;
			}
			if (num15 == 19)
			{
				double num30 = ((num29 == 0.0) ? 0.0 : Math.Floor(num28 / num29));
				double num31 = ((num29 == 0.0) ? 0.0 : (num28 - num30 * num29));
				m_outputs[2].Potential = num31;
				m_outputs[2].Resistance = Math.Abs(num31) / 10.0;
				m_outputs[0].Potential = num30;
				m_outputs[0].Resistance = Math.Abs(num30) / 10.0;
				return;
			}
			if (num15 == 27 || num15 == 28 || num15 == 29)
			{
				bool flag5;
				bool flag6;
				if (num15 == 27)
				{
					flag5 = num28 > num29;
					flag6 = num28 < num29;
				}
				else if (num15 == 28)
				{
					flag5 = num28 >= num29;
					flag6 = num28 <= num29;
				}
				else
				{
					flag5 = num28 != num29;
					flag6 = num28 == num29;
				}
				m_outputs[2].Potential = (flag5 ? 1.0 : 0.0);
				m_outputs[0].Potential = (flag6 ? 1.0 : 0.0);
				m_outputs[2].Resistance = 0.01;
				m_outputs[0].Resistance = 0.01;
				return;
			}
			if (num15 == 41 || num15 == 42 || num15 == 43)
			{
				bool flag7;
				bool flag8;
				if (num15 == 41)
				{
					flag7 = num28 > num29;
					flag8 = num28 < num29;
				}
				else if (num15 == 42)
				{
					flag7 = num28 >= num29;
					flag8 = num28 <= num29;
				}
				else
				{
					flag7 = num28 != num29;
					flag8 = num28 == num29;
				}
				m_outputs[2].Potential = (flag7 ? 5.0 : 0.0);
				m_outputs[0].Potential = (flag8 ? 5.0 : 0.0);
				m_outputs[2].Resistance = 0.01;
				m_outputs[0].Resistance = 0.01;
				m_outputs[1].Potential = 0.0;
				m_outputs[3].Potential = 0.0;
				return;
			}
			double num32 = ComputeOp(num15, num28, num29);
			m_outputs[0].Potential = num32;
			m_outputs[0].Resistance = Math.Abs(num32) / 10.0;
			if (num15 == 2 || num15 == 4 || num15 == 11 || num15 == 12 || num15 == 20 || num15 == 23)
			{
				double num33 = ComputeOp(num15, num29, num28);
				m_outputs[2].Potential = num33;
				m_outputs[2].Resistance = Math.Abs(num33) / 10.0;
				return;
			}
			m_outputs[2].Potential = 0.0;
			m_outputs[2].Resistance = 0.0;
			return;
		}
		if (!m_customMode)
		{
			RefreshOriginal();
			return;
		}
		if (m_value == 0)
		{
			UpdateTargeting();
			for (int n = 0; n < 4; n++)
			{
				m_outputs[n].Potential = 0.0;
			}
			m_outputs[0].Potential = m_angleSelf;
			m_outputs[1].Potential = m_distanceOutput;
			m_outputs[2].Potential = m_distanceOutput;
			m_outputs[3].Potential = m_angleOutput;
			return;
		}
		for (int num34 = 0; num34 < 4; num34++)
		{
			m_outputs[num34].Potential = (((m_value & (1 << num34)) > 0) ? 10.0 : 0.0);
		}
	}

	public override void Initialize()
	{
		m_initialAngle = base.transform.rotation.eulerAngles.z;
	}

	protected override void OnTouch()
	{
		m_pressed = true;
		m_pressTime = Time.time;
	}

	private void Update()
	{
		int num = -1;
		if (m_enclosedInto is ColoredFrame)
		{
			num = ((ColoredFrame)m_enclosedInto).customPartIndex;
		}
		if (num != m_lastFrameIndex)
		{
			m_lastFrameIndex = num;
			if (num != -1)
			{
				int num2 = num - 12;
				if (num2 < 0)
				{
					num2 = 0;
				}
				if (no90 == 1)
				{
					int num3 = num2 + 1001;
					if (num3 < 1001)
					{
						num3 = 1001;
					}
					if (num3 > 1118)
					{
						num3 = 1118;
					}
					m_value = num3;
				}
				else
				{
					m_value = num2 % 16;
				}
				SwitchToCustom();
			}
			else if (m_customMode)
			{
				SwitchToOriginal();
			}
		}
		if (!m_pressed)
		{
			return;
		}
		if (no90 == 1)
		{
			m_pressed = false;
			return;
		}
		if (m_customMode && Time.time - m_pressTime >= 0.5f)
		{
			m_pressed = false;
			SwitchToOriginal();
			return;
		}
		if (!GuiManager.GetPointer().down)
		{
			m_pressed = false;
			if (!m_customMode)
			{
				m_value = 0;
				SwitchToCustom();
				return;
			}
			if (m_value >= 15)
			{
				SwitchToOriginal();
				return;
			}
			m_value++;
			if (m_value == 15)
			{
				SwitchToCustom();
				return;
			}
			RefreshCustom();
		}
	}

	private void RefreshOriginal()
	{
		int num = 0;
		for (int i = 0; i < 4; i++)
		{
			num += ((m_levels[i] == ElectricalPart.LogicLevel.High) ? (1 << i) : 0);
		}
		if (num < 10)
		{
			m_text.text = ((char)(48 + num)).ToString();
			m_text.color = new Color32(18, 98, 179, byte.MaxValue);
		}
		else
		{
			m_text.text = ((char)(55 + num)).ToString();
			m_text.color = new Color32(71, 71, 178, byte.MaxValue);
		}
	}

	private void RefreshCustom()
	{
		if (no90 == 1)
		{
			int num = m_value - 1000;
			int num2 = num / 10;
			int num3 = num % 10;
			m_text.text = num2.ToString("X") + num3.ToString();
			m_text.color = new Color32(byte.MaxValue, 160, 70, byte.MaxValue);
			return;
		}
		if (m_value < 10)
		{
			m_text.text = ((char)(48 + m_value)).ToString();
			m_text.color = new Color32(byte.MaxValue, 160, 70, byte.MaxValue);
			return;
		}
		m_text.text = ((char)(55 + m_value)).ToString();
		m_text.color = new Color32(byte.MaxValue, 80, 40, byte.MaxValue);
	}

	private void SwitchToCustom()
	{
		m_customMode = true;
		if (m_connections != null)
		{
			for (int i = 0; i < m_connections.Count; i++)
			{
				ElectricalPart.ConnectionData connectionData = m_connections[i];
				if (connectionData.Electrode1 != null && connectionData.Electrode2 != null)
				{
					int j = 0;
					while (j < 4)
					{
						if (connectionData.Electrode1 == m_inputs[j].Electrodes[0])
						{
							bool flag = false;
							if (no90 == 1 && m_value >= 1001 && m_value <= 1118)
							{
								int num = m_value - 1000;
								if (num != 30)
								{
									if (num == 31)
									{
										flag = j != 0;
									}
									else if (num == 32)
									{
										flag = j == 2;
									}
								}
								if (num == 40)
								{
									flag = j == 1 || j == 2 || j == 3;
								}
								else
								{
									bool flag2 = num == 1 || num == 2 || num == 3 || num == 4 || num == 11 || num == 12 || num == 19 || num == 20 || num == 23 || num == 27 || num == 28 || num == 29 || num == 41 || num == 42 || num == 43;
									flag = (flag2 && (j == 1 || j == 3)) || (!flag2 && j == 2);
								}
							}
							else if (m_value == 9)
							{
								if (j == 0 || j == 2 || j == 3)
								{
									connectionData.Electrode1.Connect(connectionData.Electrode2);
									connectionData.Electrode2.Connect(connectionData.Electrode1);
									break;
								}
								Electrode electrode = m_outputs[1].Electrode;
								electrode.Connect(connectionData.Electrode2);
								connectionData.Electrode2.Connect(electrode);
								break;
							}
							else if (m_value == 11)
							{
								if (j == 0)
								{
									Electrode electrode2 = m_bResistor.Electrode2;
									electrode2.Connect(connectionData.Electrode2);
									connectionData.Electrode2.Connect(electrode2);
								}
								else if (j == 2)
								{
									Electrode electrode3 = m_bResistor.Electrode1;
									electrode3.Connect(connectionData.Electrode2);
									connectionData.Electrode2.Connect(electrode3);
								}
								else
								{
									connectionData.Electrode1.Connect(connectionData.Electrode2);
									connectionData.Electrode2.Connect(connectionData.Electrode1);
								}
							}
							else if (m_value == 12)
							{
								if (j == 1)
								{
									Electrode electrode4 = m_cSample.Electrode2;
									electrode4.Connect(connectionData.Electrode2);
									connectionData.Electrode2.Connect(electrode4);
								}
								else if (j == 3)
								{
									Electrode cathode = m_cSource.Cathode;
									cathode.Connect(connectionData.Electrode2);
									connectionData.Electrode2.Connect(cathode);
								}
								else
								{
									Electrode electrode5 = m_outputs[j].Electrode;
									electrode5.Connect(connectionData.Electrode2);
									connectionData.Electrode2.Connect(electrode5);
								}
							}
							else if (m_value == 13 || m_value == 14)
							{
								flag = j == 1 || j == 2;
							}
							else if (m_value == 15 && (j == 1 || j == 3))
							{
								flag = true;
							}
							if (flag)
							{
								if (INUserSettings.Instance?.StarSeaSettings?.DDNewConnectionLogic ?? true)
								{
									Electrode currentTarget = connectionData.Electrode1.ConnectedElectrode;
									if (currentTarget != null && currentTarget != connectionData.Electrode1
										&& currentTarget != connectionData.Electrode2)
									{
										currentTarget.Connect(connectionData.Electrode1);
									}
									else
									{
										connectionData.Electrode1.Connect(connectionData.Electrode2);
										connectionData.Electrode2.Connect(connectionData.Electrode1);
									}
								}
								else
								{
									connectionData.Electrode1.Connect(connectionData.Electrode2);
									connectionData.Electrode2.Connect(connectionData.Electrode1);
								}
								break;
							}
							if (!flag && m_value != 11 && m_value != 12)
							{
								Electrode electrode6 = m_outputs[j].Electrode;
								electrode6.Connect(connectionData.Electrode2);
								connectionData.Electrode2.Connect(electrode6);
								break;
							}
							break;
						}
						else
						{
							j++;
						}
					}
				}
			}
		}
		RefreshCustom();
	}

	private void SwitchToOriginal()
	{
		m_customMode = false;
		if (m_connections != null)
		{
			for (int i = 0; i < m_connections.Count; i++)
			{
				ElectricalPart.ConnectionData connectionData = m_connections[i];
				if (connectionData.Electrode1 != null && connectionData.Electrode2 != null)
				{
					connectionData.Electrode1.Connect(connectionData.Electrode2);
					connectionData.Electrode2.Connect(connectionData.Electrode1);
				}
			}
		}
		RefreshOriginal();
	}

	private void UpdateTargeting(bool sameTeam = false)
	{
		if ((bool)base.contraption && base.contraption.IsRunning)
		{
			bool stableMultiblock = INUserSettings.Instance?.StarSeaSettings?.StableMultiblock ?? true;
			Vector3 position = base.transform.position;
			Vector3 right = base.transform.right;
			float num = 10000f;
			int num2 = LayerMask.NameToLayer("Ground");
			float num3 = 0f;
			if (m_targetPart != null)
			{
				if (stableMultiblock)
				{
					try
					{
						Vector3 vector = m_targetPart.transform.position - position;
						vector.z = 0f;
						float num4 = Mathf.Max(vector.magnitude, 1f);
						num3 = (float)base.contraption.ComponentPartCount(m_targetPart.ConnectedComponent) / num4 * 1.5f;
					}
					catch (MissingReferenceException)
					{
						m_targetPart = null;
					}
					catch (NullReferenceException)
					{
						m_targetPart = null;
					}
				}
				else
				{
					Vector3 vector = m_targetPart.transform.position - position;
					vector.z = 0f;
					float num4 = Mathf.Max(vector.magnitude, 1f);
					num3 = (float)base.contraption.ComponentPartCount(m_targetPart.ConnectedComponent) / num4 * 1.5f;
				}
			}
			float num5 = 0f;
			BasePart basePart = null;
			foreach (BasePart basePart2 in base.contraption.Parts)
			{
				bool flag = false;
				bool teamMatch = MarkerManager.IsInSameTeamStatic(this, basePart2);
				if (basePart2 != null && basePart2 != this && basePart2.m_partType != BasePart.PartType.Rope && basePart2.gameObject.layer != num2 && (sameTeam ? teamMatch : !teamMatch))
				{
					Vector3 vector2 = basePart2.transform.position - position;
					vector2.z = 0f;
					float magnitude = vector2.magnitude;
					if (magnitude < num)
					{
						float num6 = (float)base.contraption.ComponentPartCount(basePart2.ConnectedComponent) / Mathf.Max(magnitude, 1f);
						if (num6 > num5 && num6 > num3)
						{
							num5 = num6;
							basePart = basePart2;
						}
						flag = true;
					}
				}
				if (!flag && basePart2 == m_targetPart)
				{
					m_targetPart = null;
				}
			}
			if (basePart != null && basePart != m_targetPart)
			{
				m_targetPart = basePart;
			}
			m_distanceOutput = -4.0;
			m_angleOutput = 0.0;
			m_angleOutputUp = 0.0;
			if (m_targetPart != null)
			{
				if (stableMultiblock)
				{
					try
					{
						Vector3 vector3 = m_targetPart.transform.position - position;
						vector3.z = 0f;
						float magnitude2 = vector3.magnitude;
						double num7;
						if (magnitude2 <= 2f)
						{
							num7 = 20.0;
						}
						else if (magnitude2 >= 8192f)
						{
							num7 = -4.0;
						}
						else if (magnitude2 <= 64f)
						{
							num7 = 20.0 * (1.0 - Math.Log((double)magnitude2 / 2.0, 2.0) / 10.0);
						}
						else
						{
							num7 = 10.0 * (1.0 - Math.Log((double)magnitude2 / 64.0, 2.0) / 5.0);
						}
						m_distanceOutput = num7;
						double num8 = (double)Mathf.Atan2(right.x * vector3.y - right.y * vector3.x, right.x * vector3.x + right.y * vector3.y) * 12.732395447351628 * 0.5;
						if (num8 > 20.0)
						{
							num8 = 20.0;
						}
						if (num8 < -20.0)
						{
							num8 = -20.0;
						}
						m_angleOutput = num8;
						m_angleOutputUp = num8;
					}
					catch (MissingReferenceException)
					{
						m_targetPart = null;
					}
					catch (NullReferenceException)
					{
						m_targetPart = null;
					}
				}
				else
				{
					Vector3 vector3 = m_targetPart.transform.position - position;
					vector3.z = 0f;
					float magnitude2 = vector3.magnitude;
					double num7;
					if (magnitude2 <= 2f)
					{
						num7 = 20.0;
					}
					else if (magnitude2 >= 8192f)
					{
						num7 = -4.0;
					}
					else if (magnitude2 <= 64f)
					{
						num7 = 20.0 * (1.0 - Math.Log((double)magnitude2 / 2.0, 2.0) / 10.0);
					}
					else
					{
						num7 = 10.0 * (1.0 - Math.Log((double)magnitude2 / 64.0, 2.0) / 5.0);
					}
					m_distanceOutput = num7;
					double num8 = (double)Mathf.Atan2(right.x * vector3.y - right.y * vector3.x, right.x * vector3.x + right.y * vector3.y) * 12.732395447351628 * 0.5;
					if (num8 > 20.0)
					{
						num8 = 20.0;
					}
					if (num8 < -20.0)
					{
						num8 = -20.0;
					}
					m_angleOutput = num8;
					m_angleOutputUp = num8;
				}
			}
			float z = base.transform.rotation.eulerAngles.z;
			double num9 = (double)Mathf.DeltaAngle(m_initialAngle, z) * 0.1111111111111111;
			if (num9 > 20.0)
			{
				num9 = 20.0;
			}
			if (num9 < -20.0)
			{
				num9 = -20.0;
			}
			m_angleSelf = num9;
		}
	}

	private double ComputeOp(int op, double a, double b)
	{
		double num = 0.0;
		switch (op)
		{
		case 1:
			num = a + b;
			break;
		case 2:
			num = a - b;
			break;
		case 3:
			num = a * b;
			break;
		case 4:
			num = ((b == 0.0) ? 0.0 : (a / b));
			break;
		case 5:
			num = Math.Abs(a);
			break;
		case 6:
			num = Factorial(a);
			break;
		case 7:
			num = a * a;
			break;
		case 8:
			num = ((a < 0.0) ? 0.0 : Math.Sqrt(a));
			break;
		case 9:
			num = a * a * a;
			break;
		case 10:
			num = ((a < 0.0) ? (0.0 - Math.Pow(-a, 0.3333333333333333)) : Math.Pow(a, 0.3333333333333333));
			break;
		case 11:
			num = Math.Pow(a, b);
			break;
		case 12:
			if (a < 0.0 && (int)Math.Floor(b) % 2 == 0)
			{
				num = 0.0;
			}
			else if (a < 0.0)
			{
				num = 0.0 - Math.Pow(-a, 1.0 / b);
			}
			else
			{
				num = Math.Pow(a, 1.0 / b);
			}
			break;
		case 13:
			num = Math.Sin(a * 0.017453292519943295);
			break;
		case 14:
			num = Math.Cos(a * 0.017453292519943295);
			break;
		case 15:
			num = Math.Tan(a * 0.017453292519943295);
			break;
		case 16:
			num = Math.Asin(a) * 57.29577951308232;
			break;
		case 17:
			num = Math.Acos(a) * 57.29577951308232;
			break;
		case 18:
			num = Math.Atan(a) * 57.29577951308232;
			break;
		case 20:
			num = ((b == 0.0) ? 0.0 : ((a % b + b) % b));
			break;
		case 21:
			num = ((a <= 0.0) ? 0.0 : Math.Log10(a));
			break;
		case 22:
			num = ((a <= 0.0) ? 0.0 : Math.Log(a, 2.0));
			break;
		case 23:
			num = ((a <= 0.0 || b <= 0.0 || b == 1.0) ? 0.0 : (Math.Log(a) / Math.Log(b)));
			break;
		case 24:
			num = Math.Ceiling(a);
			break;
		case 25:
			num = Math.Floor(a);
			break;
		case 26:
			num = Math.Round(a);
			break;
		case 33:
			num = Math.Sin(a);
			break;
		case 34:
			num = Math.Cos(a);
			break;
		case 35:
			num = Math.Tan(a);
			break;
		case 36:
			num = Math.Asin(a);
			break;
		case 37:
			num = Math.Acos(a);
			break;
		case 38:
			num = Math.Atan(a);
			break;
		case 44:
			num = Math.Pow(10.0, a);
			break;
		case 45:
			num = Math.Pow(2.0, a);
			break;
		}
		if (double.IsNaN(num) || double.IsInfinity(num))
		{
			num = 0.0;
		}
		return num;
	}

	private double Factorial(double x)
	{
		if (x < 0.0)
		{
			return 0.0;
		}
		int num = (int)Math.Floor(x);
		double num2 = 1.0;
		for (int i = 2; i <= num; i++)
		{
			num2 *= (double)i;
		}
		return num2;
	}
}
