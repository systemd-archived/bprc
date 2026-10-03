using System;

[Serializable]
public class PartAdjustSettings : SettingsBase
{
	public PartAdjustSettings()
	{
	}

	public PartAdjustSettings(PartAdjustSettings settings)
		: this()
	{
		Update(settings);
	}

	public override void Apply()
	{
	}

	public void Update(PartAdjustSettings settings)
	{
		if (settings != null)
		{
		}
	}
}
