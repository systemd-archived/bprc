using System.Collections.Generic;

public class MagneticFieldGenerator : ElectricalPart
{
	private bool m_enabled;

	public float MagneticFluxDensity => m_gridRotation switch
	{
		GridRotation.Deg_0 => -0.02f, 
		GridRotation.Deg_180 => 0.02f, 
		_ => 0f, 
	};

	public override void Awake()
	{
		base.Awake();
		m_autoAlign = (AutoAlignType)(-1);
	}

	public override bool IsEnabled()
	{
		return m_enabled;
	}

	public override bool IsTriggerable()
	{
		return !base.HasGeneratorRef;
	}

	public override IEnumerable<UIPartTriggerButtonInfo> GetTriggerButtonInfo()
	{
		yield return new UIPartTriggerButtonInfo(UIPartButtonType.Trigger, 0, base.Type, 6, base.ConnectedComponent);
	}

	public override void PostInitialize()
	{
		m_enabled = true;
	}

	protected override void OnTouch()
	{
		m_enabled = !m_enabled;
	}

	public override void SetRotation(int rotation)
	{
		rotation %= 4;
		if (rotation == 1)
		{
			rotation = 2;
		}
		if (rotation == 3)
		{
			rotation = 0;
		}
		SetRotation((GridRotation)rotation);
	}

	public float GetMagneticFluxDensityByDistance(float distance)
	{
		if (!m_enabled)
		{
			return 0f;
		}
		if (!(distance > 8f))
		{
			if (distance > 4f)
			{
				return MagneticFluxDensity / (distance - 3f);
			}
			return MagneticFluxDensity;
		}
		return 0f;
	}
}
