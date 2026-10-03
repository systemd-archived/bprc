using System.Collections.Generic;

public class ElectricFieldGenerator : ElectricalPart
{
	private bool m_enabled;

	public float ElectricFieldIntensity => 2f;

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
		yield return new UIPartTriggerButtonInfo(UIPartButtonType.Trigger, 0, base.Type, 5, base.ConnectedComponent);
	}

	public override void PostInitialize()
	{
		m_enabled = true;
	}

	protected override void OnTouch()
	{
		m_enabled = !m_enabled;
	}

	public float GetElectricFieldIntensityByDistance(float distance)
	{
		if (!m_enabled)
		{
			return 0f;
		}
		if (distance < 4f)
		{
			return ElectricFieldIntensity;
		}
		return ElectricFieldIntensity / (distance - 3f);
	}
}
