using System;

[Serializable]
public class PartSettings : SettingsBase
{
	private bool m_noDrag;

	private bool m_disableAlienPartParticles;

	private bool m_enableWaterSystem;

	private float m_waterLevel;

	private float m_buoyancyCoefficient;

	private bool m_renderDisplacedArea;

	private float m_coloredFrameSaturation1;

	private float m_coloredFrameSaturation2;

	private float m_coloredFrameBrightness1;

	private float m_coloredFrameBrightness2;

	private float m_coloredFrameBrightness3;

	private float m_jetEngineDefaultForce;

	private float m_jetEngineForceStep;

	private bool m_militaryFeatures;

	public bool MilitaryFeatures
	{
		get
		{
			try
			{
				return INSettings.GetBool(INFeature.MilitaryFeatures);
			}
			catch
			{
				return m_militaryFeatures;
			}
		}
		set
		{
			if (m_militaryFeatures != value)
			{
				m_militaryFeatures = value;
				INSettings.SetValue(INFeature.MilitaryFeatures, new Variant<bool>(value));
				OnPropertyChanged("MilitaryFeatures");
			}
		}
	}

	public bool NoDrag
	{
		get
		{
			try
			{
				return INSettings.GetBool(INFeature.NoDrag);
			}
			catch
			{
				return m_noDrag;
			}
		}
		set
		{
			if (m_noDrag != value)
			{
				m_noDrag = value;
				INSettings.SetValue(INFeature.NoDrag, new Variant<bool>(value));
				OnPropertyChanged("NoDrag");
			}
		}
	}

	public bool DisableAlienPartParticles
	{
		get
		{
			try
			{
				return INSettings.GetBool(INFeature.DisableAlienPartParticles);
			}
			catch
			{
				return m_disableAlienPartParticles;
			}
		}
		set
		{
			if (m_disableAlienPartParticles != value)
			{
				m_disableAlienPartParticles = value;
				INSettings.SetValue(INFeature.DisableAlienPartParticles, new Variant<bool>(value));
				OnPropertyChanged("DisableAlienPartParticles");
			}
		}
	}

	public bool EnableWaterSystem
	{
		get
		{
			return m_enableWaterSystem;
		}
		set
		{
			if (m_enableWaterSystem != value)
			{
				m_enableWaterSystem = value;
				OnPropertyChanged("EnableWaterSystem");
			}
		}
	}

	public float WaterLevel
	{
		get
		{
			return m_waterLevel;
		}
		set
		{
			if (m_waterLevel != value && float.IsFinite(value))
			{
				m_waterLevel = value;
				OnPropertyChanged("WaterLevel");
			}
		}
	}

	public float BuoyancyCoefficient
	{
		get
		{
			return m_buoyancyCoefficient;
		}
		set
		{
			if (m_buoyancyCoefficient != value && float.IsFinite(value))
			{
				m_buoyancyCoefficient = value;
				OnPropertyChanged("BuoyancyCoefficient");
			}
		}
	}

	public bool RenderDisplacedArea
	{
		get
		{
			return m_renderDisplacedArea;
		}
		set
		{
			if (m_renderDisplacedArea != value)
			{
				m_renderDisplacedArea = value;
				OnPropertyChanged("RenderDisplacedArea");
			}
		}
	}

	public float ColoredFrameSaturation1
	{
		get
		{
			return m_coloredFrameSaturation1;
		}
		set
		{
			if (m_coloredFrameSaturation1 != value && float.IsFinite(value) && value >= 0f && value <= 1f)
			{
				m_coloredFrameSaturation1 = value;
				OnPropertyChanged("ColoredFrameSaturation1");
			}
		}
	}

	public float ColoredFrameSaturation2
	{
		get
		{
			return m_coloredFrameSaturation2;
		}
		set
		{
			if (m_coloredFrameSaturation2 != value && float.IsFinite(value) && value >= 0f && value <= 1f)
			{
				m_coloredFrameSaturation2 = value;
				OnPropertyChanged("ColoredFrameSaturation2");
			}
		}
	}

	public float ColoredFrameBrightness1
	{
		get
		{
			return m_coloredFrameBrightness1;
		}
		set
		{
			if (m_coloredFrameBrightness1 != value && float.IsFinite(value) && value >= 0f && value <= 1f)
			{
				m_coloredFrameBrightness1 = value;
				OnPropertyChanged("ColoredFrameBrightness1");
			}
		}
	}

	public float ColoredFrameBrightness2
	{
		get
		{
			return m_coloredFrameBrightness2;
		}
		set
		{
			if (m_coloredFrameBrightness2 != value && float.IsFinite(value) && value >= 0f && value <= 1f)
			{
				m_coloredFrameBrightness2 = value;
				OnPropertyChanged("ColoredFrameBrightness2");
			}
		}
	}

	public float ColoredFrameBrightness3
	{
		get
		{
			return m_coloredFrameBrightness3;
		}
		set
		{
			if (m_coloredFrameBrightness3 != value && float.IsFinite(value) && value >= 0f && value <= 1f)
			{
				m_coloredFrameBrightness3 = value;
				OnPropertyChanged("ColoredFrameBrightness3");
			}
		}
	}

	public float JetEngineDefaultForce
	{
		get
		{
			return m_jetEngineDefaultForce;
		}
		set
		{
			if (m_jetEngineDefaultForce != value && float.IsFinite(value) && value >= 0f && value <= 3000f)
			{
				m_jetEngineDefaultForce = value;
				OnPropertyChanged("JetEngineDefaultForce");
			}
		}
	}

	public float JetEngineForceStep
	{
		get
		{
			return m_jetEngineForceStep;
		}
		set
		{
			if (m_jetEngineForceStep != value && float.IsFinite(value) && value >= 0f)
			{
				m_jetEngineForceStep = value;
				OnPropertyChanged("JetEngineForceStep");
			}
		}
	}

	public PartSettings()
	{
		NoDrag = false;
		DisableAlienPartParticles = false;
		EnableWaterSystem = false;
		WaterLevel = 0f;
		BuoyancyCoefficient = 1f;
		ColoredFrameSaturation1 = 0.7f;
		ColoredFrameSaturation2 = 0.4f;
		ColoredFrameBrightness1 = 0.9f;
		ColoredFrameBrightness2 = 0.6f;
		ColoredFrameBrightness3 = 0.3f;
		JetEngineDefaultForce = 1500f;
		JetEngineForceStep = 100f;
		MilitaryFeatures = false;
	}

	public PartSettings(PartSettings settings)
		: this()
	{
		Update(settings);
	}

	public override void Apply()
	{
		INSettings.SetValue(INFeature.NoDrag, new Variant<bool>(NoDrag));
		INSettings.SetValue(INFeature.DisableAlienPartParticles, new Variant<bool>(DisableAlienPartParticles));
		INSettings.SetValue(INFeature.MilitaryFeatures, new Variant<bool>(MilitaryFeatures));
		if (INSettings.GetBool(INFeature.ColoredFrame) && Singleton<GameManager>.Instance != null && WPFMonoBehaviour.gameData != null)
		{
			for (int i = 12; i <= 129; i++)
			{
				// The colored frames are created by INPartFactoryManager while it builds the runtime part
				// factory, so on the version selection screen - the settings are applied before that manager
				// exists - the lookup has nothing to return. Only colored frames get their color re-applied.
				if (WPFMonoBehaviour.gameData.GetCustomPart(BasePart.PartType.MetalFrame, i) is ColoredFrame coloredFrame)
				{
					coloredFrame.InitializeColor();
				}
			}
		}
	}

	public void Update(PartSettings settings)
	{
		if (settings != null)
		{
			NoDrag = settings.NoDrag;
			DisableAlienPartParticles = settings.DisableAlienPartParticles;
			EnableWaterSystem = settings.EnableWaterSystem;
			WaterLevel = settings.WaterLevel;
			BuoyancyCoefficient = settings.BuoyancyCoefficient;
			ColoredFrameSaturation1 = settings.ColoredFrameSaturation1;
			ColoredFrameSaturation2 = settings.ColoredFrameSaturation2;
			ColoredFrameBrightness1 = settings.ColoredFrameBrightness1;
			ColoredFrameBrightness2 = settings.ColoredFrameBrightness2;
			ColoredFrameBrightness3 = settings.ColoredFrameBrightness3;
			JetEngineDefaultForce = settings.JetEngineDefaultForce;
			JetEngineForceStep = settings.JetEngineForceStep;
			MilitaryFeatures = settings.MilitaryFeatures;
		}
	}
}
