using System;
using System.Collections.Generic;
using Innovation;
using UnityEngine;

public class JetEngine : BasePropulsion, IFuelConsumer, IBasePart
{
	private readonly struct PartComparisonData : IComparable<PartComparisonData>
	{
		public readonly BasePart Part;

		public readonly float X;

		public readonly float Y;

		public PartComparisonData(BasePart part, float x, float y)
		{
			Part = part;
			X = x;
			Y = y;
		}

		public int CompareTo(PartComparisonData other)
		{
			if (X < other.X)
			{
				return -1;
			}
			if (X > other.X)
			{
				return 1;
			}
			return 0;
		}
	}

	private class FlameController
	{
		private Transform m_flameTransform;

		private MeshRenderer m_flameRenderer;

		private bool m_enabled;

		private float m_targetLength;

		private float m_renderLength;

		private float m_lengthChangeRate;

		private float m_targetAngle;

		private float m_renderAngle;

		private float m_angleChangeRate;

		private float m_originalLength;

		private Vector3 m_originalLocalPosition;

		public Transform Flame => m_flameTransform;

		public float TargetLength => m_targetLength;

		public float TargetAngle => m_targetAngle;

		public FlameController(Transform flame)
		{
			m_flameTransform = flame;
			m_flameRenderer = flame.GetComponent<MeshRenderer>();
			m_flameRenderer.material.color = new Color(1f, 1f, 1f, 0.8f);
			m_originalLength = 20f;
			m_originalLocalPosition = flame.localPosition;
		}

		public void SetEnabled(bool enabled)
		{
			if (m_enabled != enabled)
			{
				m_enabled = enabled;
				m_flameTransform.gameObject.SetActive(enabled);
				if (!enabled)
				{
					m_targetLength = 0f;
					m_targetAngle = 0f;
				}
			}
		}

		public void SetLength(float length)
		{
			float num = INSettings.GetFloat(INFeature.JetEngineFlameLength);
			m_targetLength = num * length;
		}

		public void SetAngle(float angle)
		{
			m_targetAngle = 0f - angle;
		}

		public void Update()
		{
			float num = s_random.NextSingle(-0.1f, 0.1f);
			float num2 = s_random.NextSingle(-0.05f, 0.05f);
			float fixedDeltaTime = Time.fixedDeltaTime;
			m_renderLength += (m_lengthChangeRate += (-16f * m_lengthChangeRate + 32f * (m_targetLength - m_renderLength)) * fixedDeltaTime) * fixedDeltaTime;
			m_renderAngle += (m_angleChangeRate += (-16f * m_angleChangeRate + 32f * (m_targetAngle - m_renderAngle)) * fixedDeltaTime) * fixedDeltaTime;
			float num3 = 1.25f * INSettings.GetFloat(INFeature.JetEngineFlameWidth);
			float num4 = 0.85f;
			float num5 = m_renderLength * (1f + num);
			float num6 = m_renderLength / m_originalLength;
			float num7 = Math.Min(0.5f * (num6 + 1f), 2f * num6);
			float x = Mathf.Cos(m_renderAngle * (MathF.PI / 180f));
			float y = Mathf.Sin(m_renderAngle * (MathF.PI / 180f));
			Vector3 vector = (0f - num4) * num5 * 0.5f * new Vector3(x, y);
			Vector3 vector2 = new Vector3((0f - num4) * m_originalLength * 0.5f, 0f);
			Vector3 localScale = new Vector3(num6 * (1f + num), num3 * num7 * (1f + num2), 1f);
			Vector3 localPosition = m_originalLocalPosition + vector - vector2;
			m_flameTransform.localScale = localScale;
			m_flameTransform.localPosition = localPosition;
			m_flameTransform.localRotation = Quaternion.AngleAxis(m_renderAngle, Vector3.forward);
		}
	}

	private bool m_enabled;

	private float m_requiredForce;

	private float m_realForce;

	private float m_angle;

	private float m_suppliedFuelAmount;

	private int m_fuelComponentIndex;

	private FlameController m_flameController;

	private static System.Random s_random;

	public float RequiredFuelAmount
	{
		get
		{
			if (!m_enabled)
			{
				return 0f;
			}
			return (Math.Abs(m_requiredForce) / 1000f * 0.2f + 0.1f) * Time.fixedDeltaTime;
		}
	}

	public int FuelComponentIndex
	{
		get
		{
			return m_fuelComponentIndex;
		}
		set
		{
			m_fuelComponentIndex = value;
		}
	}

	static JetEngine()
	{
		s_random = new System.Random();
	}

	public override void Awake()
	{
		base.Awake();
		Transform flame = base.transform.Find("FlameSprite");
		m_flameController = new FlameController(flame);
		m_requiredForce = INUserSettings.Instance.PartSettings.JetEngineDefaultForce;
	}

	public override bool CanBeEnabled()
	{
		return !base.HasGeneratorRef;
	}

	public override bool IsEnabled()
	{
		return m_enabled;
	}

	public override bool IsTriggerable()
	{
		if (!base.HasGeneratorRef)
		{
			return FuelSystem.Instance.GetFuelComponent(m_fuelComponentIndex).FuelContainerCount > 0;
		}
		return false;
	}

	public override Direction EffectDirection()
	{
		return BasePart.Rotate(Direction.Right, m_gridRotation);
	}

	public override void ChangeVisualConnections()
	{
		bool flag = false;
		bool flag2 = false;
		int gridRotation = (int)m_gridRotation;
		for (int i = 0; i < 3; i++)
		{
			flag |= base.contraption.CanConnectTo(this, (Direction)((gridRotation + i) % 4));
			flag2 |= base.contraption.CanConnectTo(this, (Direction)((gridRotation + i + 2) % 4));
		}
		base.transform.Find("TopFrameSprite").gameObject.SetActive(flag);
		base.transform.Find("BottomFrameSprite").gameObject.SetActive(flag2 || !flag);
	}

	public override IEnumerable<UIPartTriggerButtonInfo> GetTriggerButtonInfo()
	{
		yield return new UIPartTriggerButtonInfo(UIPartButtonType.Trigger, 0, base.Type, 0, base.ConnectedComponent, consistent: true);
		yield return new UIPartTriggerButtonInfo(UIPartButtonType.Trigger, 1, base.Type, m_fuelComponentIndex, base.ConnectedComponent, consistent: true);
	}

	public override IEnumerable<UIPartSliderButtonInfo> GetSliderButtonInfo()
	{
		float jetEngineDefaultForce = INUserSettings.Instance.PartSettings.JetEngineDefaultForce;
		float jetEngineForceStep = INUserSettings.Instance.PartSettings.JetEngineForceStep;
		if (customPartIndex == 0)
		{
			yield return new UIPartSliderButtonInfo(UIPartButtonType.Slider, 2, base.Type, m_fuelComponentIndex, base.ConnectedComponent, new UIPartSliderButton.Range(m_requiredForce, jetEngineDefaultForce, -2500f, 7000f, jetEngineForceStep, 0.02f));
			yield break;
		}
		yield return new UIPartSliderButtonInfo(UIPartButtonType.Slider, 2, base.Type, m_fuelComponentIndex, base.ConnectedComponent, new UIPartSliderButton.Range(m_requiredForce, jetEngineDefaultForce, 0f, 7000f, jetEngineForceStep, 0.02f));
		yield return new UIPartSliderButtonInfo(UIPartButtonType.Slider, 3, base.Type, m_fuelComponentIndex, base.ConnectedComponent, new UIPartSliderButton.Range(m_angle, 0f, -45f, 45f, 2.5f, 0.02f));
	}

	private void FixedUpdate()
	{
		if (!base.contraption || !base.contraption.IsRunning || !m_enabled || Math.Abs(m_realForce) <= 750f)
		{
			return;
		}
		bool flag = false;
		float num = Math.Abs(m_realForce) / 1000f;
		float num2 = Math.Min(5f / 6f * (num - 0.75f), 0.25f * (num + 1f));
		float num3 = 0.8f * Math.Abs(m_flameController.TargetLength);
		float num4 = 3.5f * num2;
		float num5 = 1.5f;
		Vector3 position = base.transform.position;
		Vector3 vector = GetForceDirection();
		List<PartComparisonData> list = new List<PartComparisonData>();
		DiscreteFunction function = new DiscreteFunction(0f - num4, num4, 0.1f);
		function.Set(1f);
		if (m_realForce > 0f)
		{
			vector = -vector;
		}
		else
		{
			num5 = 0.5f;
		}
		foreach (BasePart part in base.contraption.Parts)
		{
			Vector3 vector2 = part.transform.position - position;
			float num6 = vector.x * vector2.x + vector.y * vector2.y - num5;
			float value = vector.x * vector2.y - vector.y * vector2.x;
			value = Math.Abs(value);
			if (num6 > value && num6 < num3 && value < num4 && 1f - num6 / num3 - value / num4 > 0f)
			{
				num6 = ((part is Frame) ? (num6 - 0.1f) : num6);
				list.Add(new PartComparisonData(part, num6, value));
			}
		}
		list.Sort();
		foreach (PartComparisonData item in list)
		{
			float num7 = 1f - item.X / num3 - item.Y / num4;
			float probability = 0.05f * num2 * num7 * Math.Max(function.Get(item.Y), 0.1f);
			flag |= OnPartBurnt(item, probability, ref function);
		}
		if (flag)
		{
			base.contraption.UpdateConnectedComponents();
		}
	}

	private bool OnPartBurnt(PartComparisonData data, float probability, ref DiscreteFunction function)
	{
		bool result = false;
		BasePart part = data.Part;
		float point = data.Y;
		float factor = GetDefenseFactor(part);
		float num = probability / factor;
		if (num > 0.025f)
		{
			if (part is FuelBox fuelBox && s_random.NextSingle() < (num - 0.05f) * 0.1f)
			{
				result = true;
				fuelBox.Explode();
			}
			foreach (Joint item in base.contraption.FindPartJoints(part))
			{
				if (item != null && s_random.NextSingle() < num - 0.025f)
				{
					result = true;
					UnityEngine.Object.Destroy(item);
				}
			}
			float num2 = 1000f * (num - 0.025f);
			part.rigidbody.EngineAddForce((0f - num2) * GetForceDirection());
		}
		float r = INContraption.GetBounds(part.rigidbody).R;
		function.Set(delegate(float x, float y)
		{
			float num3 = Math.Abs((x - point) / r);
			return (!(num3 < 1f)) ? y : (y * (1f - 0.5f * factor * (1f - num3)));
		});
		return result;
	}

	private float GetDefenseFactor(BasePart part)
	{
		if (part.IsMetalBox())
		{
			return 1f;
		}
		if (part is JetEngine || part is FuelTube)
		{
			return 0.7f;
		}
		switch (part.GetJointConnectionStrength())
		{
		case JointConnectionStrength.Weak:
		case JointConnectionStrength.Normal:
			return 0.3f;
		case JointConnectionStrength.High:
			return 0.4f;
		case JointConnectionStrength.Extreme:
			return 0.5f;
		case JointConnectionStrength.HighlyExtreme:
			return 0.6f;
		default:
			return 0f;
		}
	}

	private Vector3 GetForceDirection()
	{
		Vector3 right = base.transform.right;
		float num = Mathf.Cos((0f - m_angle) * (MathF.PI / 180f));
		float num2 = Mathf.Sin((0f - m_angle) * (MathF.PI / 180f));
		return new Vector3(right.x * num - right.y * num2, right.x * num2 + right.y * num);
	}

	public void ConsumeFuel(float fuelAmount)
	{
		if (!m_enabled)
		{
			return;
		}
		float fixedDeltaTime = Time.fixedDeltaTime;
		m_suppliedFuelAmount = fuelAmount - 0.1f * fixedDeltaTime;
		if (m_suppliedFuelAmount <= 0f)
		{
			m_suppliedFuelAmount = 0f;
			SetEnabled(enabled: false);
			return;
		}
		if (fuelAmount < RequiredFuelAmount)
		{
			SetRealForce(m_requiredForce * m_suppliedFuelAmount / (RequiredFuelAmount - 0.1f * fixedDeltaTime));
		}
		else
		{
			SetRealForce(m_requiredForce);
		}
		Vector3 forceDirection = GetForceDirection();
		float num = INSettings.GetFloat(INFeature.JetEngineForce);
		base.rigidbody.EngineAddForce(num * m_realForce * forceDirection);
	}

	public override void OnSliderButtonTriggered(UIPartSliderButton button)
	{
		switch (button.Info.ButtonIndex)
		{
		case 2:
			SetRequiredForce(button.Value);
			break;
		case 3:
			SetAngle(button.Value);
			break;
		default:
			throw new ArgumentException("button");
		}
	}

	protected override void OnTouch()
	{
		SetEnabled(!m_enabled);
	}

	public override void SetEnabled(bool enabled)
	{
		if (m_enabled != enabled)
		{
			m_enabled = enabled;
			m_flameController.SetEnabled(enabled);
			if (!enabled)
			{
				m_realForce = 0f;
				return;
			}
			m_flameController.SetLength(m_realForce / 100f);
			m_flameController.SetAngle(m_angle);
		}
	}

	private void SetRequiredForce(float force)
	{
		m_requiredForce = force;
	}

	private void SetRealForce(float force)
	{
		force *= Mathf.Cos(m_angle * (MathF.PI / 180f));
		m_realForce = ((force >= 0f) ? force : (0.5f * force));
		m_flameController.SetLength(m_realForce / 100f);
	}

	private void SetAngle(float angle)
	{
		m_angle = angle;
		m_flameController.SetAngle(angle);
	}

	private void Update()
	{
		if ((bool)base.contraption && base.contraption.IsRunning)
		{
			m_flameController.Update();
		}
	}
}
