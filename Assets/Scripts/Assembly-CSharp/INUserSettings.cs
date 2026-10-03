using System;
using System.Collections.Generic;
using System.IO;
using Innovation;
using Newtonsoft.Json;

[Serializable]
public class INUserSettings
{
	public Version Version { get; set; }

	public GeneralSettings GeneralSettings { get; set; }

	public AppInterfaceSettings AppInterfaceSettings { get; set; }

	public LevelSceneSettings LevelSceneSettings { get; set; }

	public ButtonSettings ButtonSettings { get; set; }

	public ContraptionDataSettings ContraptionDataSettings { get; set; }

	public PartSettings PartSettings { get; set; }

	public StarSeaSettings StarSeaSettings { get; set; }

	public PartAdjustSettings PartAdjustSettings { get; set; }

	[JsonIgnore]
	public IEnumerable<SettingsBase> SettingsList
	{
		get
		{
			yield return GeneralSettings;
			yield return AppInterfaceSettings;
			yield return LevelSceneSettings;
			yield return ButtonSettings;
			yield return ContraptionDataSettings;
			yield return PartSettings;
			yield return StarSeaSettings;
			yield return PartAdjustSettings;
		}
	}

	public static INUserSettings Default { get; private set; }

	public static INUserSettings Instance { get; private set; }

	public INUserSettings()
	{
	}

	public INUserSettings(Version version)
	{
		Version = version;
		GeneralSettings = new GeneralSettings();
		AppInterfaceSettings = new AppInterfaceSettings();
		LevelSceneSettings = new LevelSceneSettings();
		ButtonSettings = new ButtonSettings();
		ContraptionDataSettings = new ContraptionDataSettings();
		PartSettings = new PartSettings();
		StarSeaSettings = new StarSeaSettings();
		PartAdjustSettings = new PartAdjustSettings();
	}

	public INUserSettings(Version version, INUserSettings settings)
		: this(version)
	{
		Update(settings);
	}

	public void Apply()
	{
		foreach (SettingsBase settings in SettingsList)
		{
			settings.Apply();
		}
	}

	public void Update(INUserSettings settings, bool includeStarSea = true, bool includePartAdjust = true)
	{
		if (settings != null)
		{
			GeneralSettings.Update(settings.GeneralSettings);
			AppInterfaceSettings.Update(settings.AppInterfaceSettings);
			LevelSceneSettings.Update(settings.LevelSceneSettings);
			ButtonSettings.Update(settings.ButtonSettings);
			ContraptionDataSettings.Update(settings.ContraptionDataSettings);
			PartSettings.Update(settings.PartSettings);
			if (includeStarSea)
			{
				StarSeaSettings.Update(settings.StarSeaSettings);
			}
			if (includePartAdjust)
			{
				PartAdjustSettings.Update(settings.PartAdjustSettings);
			}
		}
	}

	public static void Load()
	{
		Default = new INUserSettings(INUnity.Version);
		Instance = new INUserSettings(INUnity.Version);
		string path = INUnity.SettingsPath + "/INUserSettings.json";
		try
		{
			if (File.Exists(path))
			{
				using StreamReader reader = new StreamReader(path);
				INUserSettings iNUserSettings = Json.Deserialize<INUserSettings>(reader);
				if (iNUserSettings != null && iNUserSettings.Version != null && iNUserSettings.Version >= new Version(2022, 1, 5))
				{
					Instance.Update(iNUserSettings);
				}
			}
		}
		catch
		{
		}
		Instance.Apply();
	}

	public static void Save()
	{
		string settingsPath = INUnity.SettingsPath;
		string path = INUnity.SettingsPath + "/INUserSettings.json";
		try
		{
			if (!Directory.Exists(settingsPath))
			{
				Directory.CreateDirectory(settingsPath);
			}
			using StreamWriter writer = new StreamWriter(path);
			Json.Serialize(writer, Instance);
		}
		catch
		{
		}
	}

	public static void Reset()
	{
		Instance.Update(Default, includeStarSea: false, includePartAdjust: false);
		Instance.Apply();
	}
}
