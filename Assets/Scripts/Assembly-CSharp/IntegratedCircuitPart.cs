using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
public class IntegratedCircuitPart : ElectricalPart
{
	// (get) Token: 0x0600165B RID: 5723 RVA: 0x00012A5E File Offset: 0x00010C5E
	public override IEnumerable<CircuitElement> ElectricalElements
	{
		get
		{
			if (m_input1 != null)
			{
				yield return m_input1;
			}
			if (m_input2 != null)
			{
				yield return m_input2;
			}
			if (m_output != null)
			{
				yield return m_output;
			}
			if (m_switch != null)
			{
				yield return m_switch;
			}
			if (m_diode != null)
			{
				yield return m_diode.Source;
			}
		}
	}
	public IntegratedCircuitPart.ICType GetICType()
	{
		return (IntegratedCircuitPart.ICType)(this.customPartIndex - 22);
	}
	public override void Awake()
	{
		if ((this.m_gridRotation == BasePart.GridRotation.Deg_0) | (this.m_gridRotation == BasePart.GridRotation.Deg_90) | (this.m_gridRotation == BasePart.GridRotation.Deg_180) | (this.m_gridRotation == BasePart.GridRotation.Deg_270))
		{
			this.no90 = 0;
		}
		else if ((this.m_gridRotation == BasePart.GridRotation.Deg_45) | (this.m_gridRotation == BasePart.GridRotation.Deg_135) | (this.m_gridRotation == BasePart.GridRotation.Deg_225) | (this.m_gridRotation == BasePart.GridRotation.Deg_315))
		{
			this.no90 = 1;
		}
		base.Awake();
		checked
		{
			int num = (int)(this.GetICType() + 1);
			INSerializedSprite component = base.GetComponent<INSerializedSprite>();
			component.SpriteName = "IntegratedCircuit" + num.ToString() + "_Sprite";
			component.UpdateMesh();
			switch (this.GetICType())
			{
			case IntegratedCircuitPart.ICType.OR2:
			case IntegratedCircuitPart.ICType.NOR2:
			case IntegratedCircuitPart.ICType.AND2:
			case IntegratedCircuitPart.ICType.NAND2:
			case IntegratedCircuitPart.ICType.OPAMP:
				this.m_canBeFlipped = true;
				this.m_autoAlign = (BasePart.AutoAlignType)(-1);
				return;
			case IntegratedCircuitPart.ICType.NOR1:
			case IntegratedCircuitPart.ICType.AND1:
			case IntegratedCircuitPart.ICType.NAND1:
			case IntegratedCircuitPart.ICType.NMOS:
			case IntegratedCircuitPart.ICType.PMOS:
			case IntegratedCircuitPart.ICType.DIODE:
				return;
			case IntegratedCircuitPart.ICType.DELAY1:
				this.m_delay = (int)Math.Round(0.1 * ElectricalSystem.CircuitFrameRate) + 1;
				this.m_delayQueue = new Queue<ElectricalPart.LogicLevel>(this.m_delay - 1);
				return;
			case IntegratedCircuitPart.ICType.DELAY2:
				this.m_delay = (int)Math.Round(0.5 * ElectricalSystem.CircuitFrameRate) + 1;
				this.m_delayQueue = new Queue<ElectricalPart.LogicLevel>(this.m_delay - 1);
				return;
			default:
				return;
			}
		}
	}
	public override void SetRotation(BasePart.GridRotation rotation)
	{
		if (this.m_canBeFlipped)
		{
			this.SetRotation(this.GetRotation(rotation, this.m_flipped));
			return;
		}
		base.SetRotation(rotation);
	}
	public override void SetFlipped(bool flipped)
	{
		if (this.m_canBeFlipped)
		{
			this.SetRotation(this.GetRotation(this.m_gridRotation, flipped));
			return;
		}
		base.SetFlipped(flipped);
	}
	public override int GetRotation()
	{
		return this.GetRotation(this.m_gridRotation, this.m_flipped);
	}
	private int GetRotation(BasePart.GridRotation rotation, bool flipped)
	{
		return (int)rotation * (int)BasePart.GridRotation.Deg_180 + (flipped ? 1 : 0);
	}
	public override void SetRotation(int rotation)
	{
		int num = rotation % 8;
		int num2 = num / 2;
		bool flag = (this.m_flipped = num % 2 == 1);
		this.m_gridRotation = (BasePart.GridRotation)num2;
		int num3 = ((flag && (num2 == 0 || num2 == 2)) ? 180 : 0);
		int num4 = ((flag && (num2 == 1 || num2 == 3)) ? 180 : 0);
		int num5 = 90 * num2;
		ApplyBuildRotation(Quaternion.Euler((float)num3, (float)num4, (float)num5), false);
	}
	public override void CreateElectricalElements()
	{
		IntegratedCircuitPart.ICType ictype = this.GetICType();
		if (ictype == IntegratedCircuitPart.ICType.NMOS || ictype == IntegratedCircuitPart.ICType.PMOS)
		{
			this.m_input1 = new Wire(1);
			this.m_input1.ElementUpdated += this.OnInput1Updated;
			this.m_switch = new Switch();
			return;
		}
		if (ictype != IntegratedCircuitPart.ICType.DIODE)
		{
			if (this.GetInput1Direction() != BitDirection.None)
			{
				this.m_input1 = new Wire(1);
				this.m_input1.ElementUpdated += this.OnInput1Updated;
			}
			if (this.GetInput2Direction() != BitDirection.None)
			{
				this.m_input2 = new Wire(1);
				this.m_input2.ElementUpdated += this.OnInput2Updated;
			}
			if (this.GetOutputDirection() != BitDirection.None)
			{
				this.m_output = new Vcc(0.0, 0.0);
			}
			if (ictype == IntegratedCircuitPart.ICType.DELAY1 || ictype == IntegratedCircuitPart.ICType.DELAY2)
			{
				double num = -1.0;
				ColoredFrame coloredFrame = this.m_enclosedInto as ColoredFrame;
				if (coloredFrame != null)
				{
					double num2 = ((ictype == IntegratedCircuitPart.ICType.DELAY1) ? 1.0 : 3.0);
					switch (coloredFrame.m_partTier)
					{
					case BasePart.PartTier.Common:
					{
						int num3 = checked(coloredFrame.customPartIndex - 12);
						if (num3 >= 0 && num3 <= 35)
						{
							num = (double)(checked(num3 + 1)) * 0.1 * num2;
						}
						break;
					}
					case BasePart.PartTier.Rare:
					{
						int num4 = checked(coloredFrame.customPartIndex - 48);
						if (num4 >= 0 && num4 <= 35)
						{
							num = (double)(checked(num4 + 1)) * 0.02 * num2;
						}
						break;
					}
					case BasePart.PartTier.Legendary:
					{
						int num5 = checked(coloredFrame.customPartIndex - 10);
						if (num5 >= 0 && num5 <= 9)
						{
							num = (double)(checked(num5 + 1)) * num2;
						}
						break;
					}
					}
				}
				checked
				{
					if (num >= 0.0)
					{
						this.m_delay = (int)Math.Round(unchecked(num * ElectricalSystem.CircuitFrameRate)) + 1;
						this.m_delayQueue = new Queue<ElectricalPart.LogicLevel>(this.m_delay - 1);
						Transform transform = base.transform.Find("MultiplierText");
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
							transform.localPosition = new Vector3(0f, -0.3f, -0.5f);
						}
						textMesh.text = ((double)(this.m_delay - 1) / ElectricalSystem.CircuitFrameRate).ToString("0.##") + "s";
						textMesh.gameObject.SetActive(true);
						return;
					}
					Transform transform2 = base.transform.Find("MultiplierText");
					if (transform2 != null)
					{
						transform2.gameObject.SetActive(false);
					}
				}
			}
			return;
		}
		this.m_diode = new IntegratedCircuitPart.Diode();
	}
	protected override BitDirection GetConnectionDirection()
	{
		IntegratedCircuitPart.ICType ictype = this.GetICType();
		BitDirection bitDirection;
		if (ictype == IntegratedCircuitPart.ICType.NMOS || ictype == IntegratedCircuitPart.ICType.PMOS)
		{
			bitDirection = (BitDirection)14;
		}
		else if (ictype != IntegratedCircuitPart.ICType.DIODE)
		{
			bitDirection = this.GetInput1Direction() | this.GetInput2Direction() | this.GetOutputDirection();
		}
		else
		{
			bitDirection = BitDirection.LeftAndRight;
		}
		return bitDirection.Rotate((int)this.m_gridRotation);
	}
	protected override Electrode FindElectrode(BitDirection direction)
	{
		direction = direction.Rotate((int)(checked(BasePart.GridRotation.Deg_0 - this.m_gridRotation)));
		IntegratedCircuitPart.ICType ictype = this.GetICType();
		if (ictype == IntegratedCircuitPart.ICType.NMOS || ictype == IntegratedCircuitPart.ICType.PMOS)
		{
			Electrode electrode;
			if (direction != BitDirection.Up)
			{
				if (direction != BitDirection.Left)
				{
					if (direction != BitDirection.Down)
					{
						electrode = null;
					}
					else
					{
						electrode = this.m_switch.Throw;
					}
				}
				else
				{
					electrode = this.m_input1.Electrodes[0];
				}
			}
			else
			{
				electrode = this.m_switch.Pole;
			}
			return electrode;
		}
		if (ictype == IntegratedCircuitPart.ICType.DIODE)
		{
			Electrode electrode2;
			if (direction != BitDirection.Right)
			{
				if (direction == BitDirection.Left)
				{
					electrode2 = this.m_diode.Source.Anode;
				}
				else
				{
					electrode2 = null;
				}
			}
			else
			{
				electrode2 = this.m_diode.Source.Cathode;
			}
			return electrode2;
		}
		if (direction == this.GetInput1Direction())
		{
			return this.m_input1.Electrodes[0];
		}
		if (direction == this.GetInput2Direction())
		{
			return this.m_input2.Electrodes[0];
		}
		if (direction == this.GetOutputDirection())
		{
			return this.m_output.Electrodes[0];
		}
		return null;
	}
	private BitDirection GetInput1Direction()
	{
		switch (this.GetICType())
		{
		case IntegratedCircuitPart.ICType.BUF:
		case IntegratedCircuitPart.ICType.NOT:
			return BitDirection.Left;
		case IntegratedCircuitPart.ICType.OR1:
		case IntegratedCircuitPart.ICType.NOR1:
		case IntegratedCircuitPart.ICType.AND1:
		case IntegratedCircuitPart.ICType.NAND1:
			return BitDirection.Up;
		case IntegratedCircuitPart.ICType.OR2:
		case IntegratedCircuitPart.ICType.NOR2:
		case IntegratedCircuitPart.ICType.AND2:
		case IntegratedCircuitPart.ICType.NAND2:
			return BitDirection.Left;
		case IntegratedCircuitPart.ICType.OPAMP:
			if (this.m_flipped)
			{
				return BitDirection.Down;
			}
			return BitDirection.Up;
		case IntegratedCircuitPart.ICType.DELAY1:
		case IntegratedCircuitPart.ICType.DELAY2:
			return BitDirection.Left;
		}
		return BitDirection.None;
	}
	private BitDirection GetInput2Direction()
	{
		switch (this.GetICType())
		{
		case IntegratedCircuitPart.ICType.OR1:
		case IntegratedCircuitPart.ICType.NOR1:
		case IntegratedCircuitPart.ICType.AND1:
		case IntegratedCircuitPart.ICType.NAND1:
			return BitDirection.Down;
		case IntegratedCircuitPart.ICType.OR2:
		case IntegratedCircuitPart.ICType.NOR2:
		case IntegratedCircuitPart.ICType.AND2:
		case IntegratedCircuitPart.ICType.NAND2:
			if (this.m_flipped)
			{
				return BitDirection.Down;
			}
			return BitDirection.Up;
		case IntegratedCircuitPart.ICType.OPAMP:
			if (this.m_flipped)
			{
				return BitDirection.Up;
			}
			return BitDirection.Down;
		}
		return BitDirection.None;
	}
	private BitDirection GetOutputDirection()
	{
		return BitDirection.Right;
	}
	private void OnInput1Updated(CircuitSimulator simulator, SimulationResult result)
	{
		if (result.IsGrounded)
		{
			this.m_level1 = ElectricalPart.GetLogicLevel(result.U);
			this.m_U1 = result.U;
		}
	}
	private void OnInput2Updated(CircuitSimulator simulator, SimulationResult result)
	{
		if (result.IsGrounded)
		{
			this.m_level2 = ElectricalPart.GetLogicLevel(result.U);
			this.m_U2 = result.U;
		}
	}
	public override void PreUpdateElements()
	{
		this.m_U1 = double.NaN;
		this.m_U2 = double.NaN;
		this.m_level1 = ElectricalPart.LogicLevel.Invalid;
		this.m_level2 = ElectricalPart.LogicLevel.Invalid;
	}
	public override void PostUpdateElements()
	{
		switch (this.GetICType())
		{
		case IntegratedCircuitPart.ICType.NMOS:
			base.SetInvalid(this.m_level1 == ElectricalPart.LogicLevel.Invalid);
			this.m_switch.Toggle(this.m_level1 == ElectricalPart.LogicLevel.High);
			return;
		case IntegratedCircuitPart.ICType.PMOS:
			base.SetInvalid(this.m_level1 == ElectricalPart.LogicLevel.Invalid);
			this.m_switch.Toggle(this.m_level1 == ElectricalPart.LogicLevel.Low);
			return;
		case IntegratedCircuitPart.ICType.DIODE:
			this.m_diode.Update();
			return;
		case IntegratedCircuitPart.ICType.OPAMP:
			base.SetInvalid(double.IsNaN(this.m_U1) || double.IsNaN(this.m_U2));
			if (!this.m_invalid)
			{
				double num = Math.Clamp((this.m_U1 - this.m_U2) * 1000.0, -10.0, 10.0);
				this.SetOutput(num, 0.05);
			}
			return;
		case IntegratedCircuitPart.ICType.DELAY1:
		case IntegratedCircuitPart.ICType.DELAY2:
		{
			base.SetInvalid(this.m_level1 == ElectricalPart.LogicLevel.Invalid);
			bool flag = false;
			if (this.m_delayQueue.Count == checked(this.m_delay - 1))
			{
				flag = this.m_delayQueue.Dequeue() == ElectricalPart.LogicLevel.High;
			}
			this.m_delayQueue.Enqueue(this.m_level1);
			this.SetOutput(flag);
			return;
		}
		default:
		{
			bool flag2 = false;
			if (this.GetInput1Direction() != BitDirection.None)
			{
				flag2 |= this.m_level1 == ElectricalPart.LogicLevel.Invalid;
			}
			if (this.GetInput2Direction() != BitDirection.None)
			{
				flag2 |= this.m_level2 == ElectricalPart.LogicLevel.Invalid;
			}
			base.SetInvalid(flag2);
			if (!flag2)
			{
				bool flag3 = false;
				bool flag4 = this.m_level1 == ElectricalPart.LogicLevel.High;
				bool flag5 = this.m_level2 == ElectricalPart.LogicLevel.High;
				IntegratedCircuitPart.ICType ictype = this.GetICType();
				switch (ictype)
				{
				case IntegratedCircuitPart.ICType.BUF:
					flag3 = flag4;
					break;
				case IntegratedCircuitPart.ICType.NOT:
					flag3 = !flag4;
					break;
				case IntegratedCircuitPart.ICType.OR1:
				case IntegratedCircuitPart.ICType.OR2:
					flag3 = flag4 || flag5;
					break;
				case IntegratedCircuitPart.ICType.NOR1:
				case IntegratedCircuitPart.ICType.NOR2:
					flag3 = !flag4 && !flag5;
					break;
				case IntegratedCircuitPart.ICType.AND1:
				case IntegratedCircuitPart.ICType.AND2:
					flag3 = flag4 && flag5;
					break;
				case IntegratedCircuitPart.ICType.NAND1:
				case IntegratedCircuitPart.ICType.NAND2:
					flag3 = !flag4 || !flag5;
					break;
				default:
					if (ictype != IntegratedCircuitPart.ICType.XOR)
					{
						if (ictype == IntegratedCircuitPart.ICType.XNOR)
						{
							flag3 = !flag4 ^ flag5;
						}
					}
					else
					{
						flag3 = flag4 ^ flag5;
					}
					break;
				}
				this.SetOutput(flag3);
				return;
			}
			this.SetOutput(false);
			return;
		}
		}
	}
	private void SetOutput(bool value)
	{
		if (value)
		{
			this.SetOutput(5.0, 0.05);
			return;
		}
		this.SetOutput(0.0, 0.0);
	}
	private void SetOutput(double potential, double resistance)
	{
		this.m_output.Potential = potential;
		this.m_output.Resistance = resistance;
	}
	public IntegratedCircuitPart()
	{
	}
	private Wire m_input1;
	private Wire m_input2;
	private Vcc m_output;
	private Switch m_switch;
	private IntegratedCircuitPart.Diode m_diode;
	private double m_U1;
	private double m_U2;
	private int m_delay;
	private ElectricalPart.LogicLevel m_level1;
	private ElectricalPart.LogicLevel m_level2;
	private Queue<ElectricalPart.LogicLevel> m_delayQueue;
	private bool m_canBeFlipped;
	private int no90;
	public enum ICType
	{
		BUF,
		NOT,
		OR1,
		OR2,
		NOR1,
		NOR2,
		AND1,
		AND2,
		NAND1,
		NAND2,
		NMOS,
		PMOS,
		DIODE,
		OPAMP,
		DELAY1,
		DELAY2,
		XOR = 101,
		XNOR
	}
	public class Diode
	{
		// (get) Token: 0x06001670 RID: 5744 RVA: 0x00012BAD File Offset: 0x00010DAD
		public VoltageSource Source
		{
			get
			{
				return this.m_source;
			}
		}
		public Diode()
		{
			this.m_source = new VoltageSource(0.0, 0.0);
			this.m_source.ElementUpdated += this.OnElementUpdated;
			this.m_I = 0.0;
			this.m_U1 = 0.0;
			this.m_U2 = 0.0;
			this.Update();
		}
		private void OnElementUpdated(CircuitSimulator simulator, SimulationResult result)
		{
			if (result.Electrode == this.m_source.Anode)
			{
				this.m_I = result.I;
				this.m_U1 = result.U;
				return;
			}
			this.m_I = 0.0 - result.I;
			this.m_U2 = result.U;
		}
		public void Update()
		{
			double num = this.m_I;
			double num2 = this.m_U1 - this.m_U2;
			if (num >= 10.0)
			{
				num2 = Math.Log(num / 1E-06 + 1.0) * 0.05;
			}
			else
			{
				double num3 = Math.Min(num2, 1.0);
				for (int i = 0; i < 16; i++)
				{
					double num4 = Math.Exp(num3 / 0.05);
					double num5 = (1E-06 * (num4 - 1.0) - num) * (1.9999999999999998E-05 * num4) + num3 - num2;
					double num6 = (1E-06 * (2.0 * num4 - 1.0) - num) * (0.0003999999999999999 * num4) + 1.0;
					double num7 = num5 / num6;
					num3 -= num7;
					if (Math.Abs(num7) < 1E-05)
					{
						break;
					}
				}
				num2 = Math.Max(num3, -0.15);
				num = 1E-06 * (Math.Exp(num2 / 0.05) - 1.0);
			}
			double num8 = 0.05 / (num + 1E-06);
			this.m_source.Resistance = num8;
			this.m_source.Voltage = num2 - num * num8;
		}
		private VoltageSource m_source;
		private double m_I;
		private double m_U1;
		private double m_U2;
	}
}

