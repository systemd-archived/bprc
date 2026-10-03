using System;
using Innovation;

[Serializable]
public class LevelSceneSettings : SettingsBase
{
	private float m_terrainScale;

	private float m_minCameraScale;

	private float m_maxCameraScale;

	private bool m_hideRuntimeButtons;

	private bool m_hideStarAndDessertCounter;

	private bool m_enablePropertyPanel;

	private bool m_enableEnhancedPropertyPanel;

	private bool m_enableCustomBackgroundColor;

	private HexColor m_customBackgroundColor;

	public float TerrainScale
	{
		get
		{
			return m_terrainScale;
		}
		set
		{
			if (m_terrainScale != value && float.IsFinite(value) && value >= 0f)
			{
				m_terrainScale = value;
				OnPropertyChanged("TerrainScale");
			}
		}
	}

	public float MinCameraScale
	{
		get
		{
			return m_minCameraScale;
		}
		set
		{
			if (m_minCameraScale != value && float.IsFinite(value) && value >= 0f)
			{
				m_minCameraScale = value;
				OnPropertyChanged("MinCameraScale");
			}
		}
	}

	public float MaxCameraScale
	{
		get
		{
			return m_maxCameraScale;
		}
		set
		{
			if (m_maxCameraScale != value && float.IsFinite(value) && value >= 0f)
			{
				m_maxCameraScale = value;
				OnPropertyChanged("MaxCameraScale");
			}
		}
	}

	public bool HideRuntimeButtons
	{
		get
		{
			return m_hideRuntimeButtons;
		}
		set
		{
			if (m_hideRuntimeButtons != value)
			{
				m_hideRuntimeButtons = value;
				OnPropertyChanged("HideRuntimeButtons");
			}
		}
	}

	public bool HideStarAndDessertCounter
	{
		get
		{
			return m_hideStarAndDessertCounter;
		}
		set
		{
			if (m_hideStarAndDessertCounter != value)
			{
				m_hideStarAndDessertCounter = value;
				OnPropertyChanged("HideStarAndDessertCounter");
			}
		}
	}

	public bool EnablePropertyPanel
	{
		get
		{
			return m_enablePropertyPanel;
		}
		set
		{
			if (m_enablePropertyPanel != value)
			{
				m_enablePropertyPanel = value;
				OnPropertyChanged("EnablePropertyPanel");
			}
		}
	}

	public bool EnableEnhancedPropertyPanel
	{
		get
		{
			try
			{
				return INSettings.GetBool(INFeature.EnhancedPropertyPanel);
			}
			catch
			{
				return m_enableEnhancedPropertyPanel;
			}
		}
		set
		{
			if (m_enableEnhancedPropertyPanel != value)
			{
				m_enableEnhancedPropertyPanel = value;
				INSettings.SetValue(INFeature.EnhancedPropertyPanel, new Variant<bool>(value));
				OnPropertyChanged("EnableEnhancedPropertyPanel");
			}
		}
	}

	public bool EnableCustomBackgroundColor
	{
		get
		{
			return m_enableCustomBackgroundColor;
		}
		set
		{
			if (m_enableCustomBackgroundColor != value)
			{
				m_enableCustomBackgroundColor = value;
				OnPropertyChanged("EnableCustomBackgroundColor");
			}
		}
	}

	public HexColor CustomBackgroundColor
	{
		get
		{
			return m_customBackgroundColor;
		}
		set
		{
			if (m_customBackgroundColor != value)
			{
				m_customBackgroundColor = value;
				OnPropertyChanged("CustomBackgroundColor");
			}
		}
	}

	public LevelSceneSettings()
	{
		TerrainScale = 1f;
		MinCameraScale = 1f;
		MaxCameraScale = 1f;
		HideRuntimeButtons = false;
		HideStarAndDessertCounter = false;
		EnablePropertyPanel = true;
		EnableEnhancedPropertyPanel = false;
		EnableCustomBackgroundColor = false;
		CustomBackgroundColor = HexColor.Black;
	}

	public LevelSceneSettings(LevelSceneSettings settings)
		: this()
	{
		Update(settings);
	}

	public override void Apply()
	{
		INSettings.SetValue(INFeature.EnhancedPropertyPanel, new Variant<bool>(EnableEnhancedPropertyPanel));
	}

	public void Update(LevelSceneSettings settings)
	{
		if (settings != null)
		{
			TerrainScale = settings.TerrainScale;
			MinCameraScale = settings.MinCameraScale;
			MaxCameraScale = settings.MaxCameraScale;
			HideRuntimeButtons = settings.HideRuntimeButtons;
			HideStarAndDessertCounter = settings.HideStarAndDessertCounter;
			EnablePropertyPanel = settings.EnablePropertyPanel;
			EnableEnhancedPropertyPanel = settings.EnableEnhancedPropertyPanel;
			EnableCustomBackgroundColor = settings.EnableCustomBackgroundColor;
			CustomBackgroundColor = settings.CustomBackgroundColor;
		}
	}
}
