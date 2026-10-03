using System;

[Serializable]
public class ButtonSettings : SettingsBase
{
	private bool m_enabled;

	private float m_buttonScale;

	private float m_scrollViewHeightScale;

	private int m_layoutMode;

	private bool m_highPartTypePriority;

	private int m_maxSeparationCount;

	private bool m_displayButtonIndex;

	private bool m_buttonIndexQwerty;

	public bool Enabled
	{
		get
		{
			return m_enabled;
		}
		set
		{
			if (m_enabled != value)
			{
				m_enabled = value;
				OnPropertyChanged("Enabled");
			}
		}
	}

	public float ButtonScale
	{
		get
		{
			return m_buttonScale;
		}
		set
		{
			if (m_buttonScale != value && float.IsFinite(value))
			{
				m_buttonScale = value;
				OnPropertyChanged("ButtonScale");
			}
		}
	}

	public float ScrollViewHeightScale
	{
		get
		{
			return m_scrollViewHeightScale;
		}
		set
		{
			if (m_scrollViewHeightScale != value && float.IsFinite(value))
			{
				m_scrollViewHeightScale = value;
				OnPropertyChanged("ScrollViewHeightScale");
			}
		}
	}

	public int LayoutMode
	{
		get
		{
			return m_layoutMode;
		}
		set
		{
			if (m_layoutMode != value && value >= 0 && value <= 1)
			{
				m_layoutMode = value;
				OnPropertyChanged("LayoutMode");
			}
		}
	}

	public bool HighPartTypePriority
	{
		get
		{
			return m_highPartTypePriority;
		}
		set
		{
			if (m_highPartTypePriority != value)
			{
				m_highPartTypePriority = value;
				OnPropertyChanged("HighPartTypePriority");
			}
		}
	}

	public int MaxSeparationCount
	{
		get
		{
			return m_maxSeparationCount;
		}
		set
		{
			if (m_maxSeparationCount != value && value >= 0)
			{
				m_maxSeparationCount = value;
				OnPropertyChanged("MaxSeparationCount");
			}
		}
	}

	public bool DisplayButtonIndex
	{
		get
		{
			return m_displayButtonIndex;
		}
		set
		{
			if (m_displayButtonIndex != value)
			{
				m_displayButtonIndex = value;
				OnPropertyChanged("DisplayButtonIndex");
			}
		}
	}

	// 部件索引优化：开启后索引字母按键盘顺序(QWERTY...)排列，默认关闭(即 ABCDE...)
	public bool ButtonIndexQwerty
	{
		get
		{
			return m_buttonIndexQwerty;
		}
		set
		{
			if (m_buttonIndexQwerty != value)
			{
				m_buttonIndexQwerty = value;
				OnPropertyChanged("ButtonIndexQwerty");
			}
		}
	}

	public ButtonSettings()
	{
		Enabled = true;
		ButtonScale = 1f;
		ScrollViewHeightScale = 1f;
		MaxSeparationCount = 3;
		DisplayButtonIndex = false;
		ButtonIndexQwerty = false;
	}

	public ButtonSettings(ButtonSettings settings)
		: this()
	{
		Update(settings);
	}

	public void Update(ButtonSettings settings)
	{
		if (settings != null)
		{
			Enabled = settings.Enabled;
			ButtonScale = settings.ButtonScale;
			ScrollViewHeightScale = settings.ScrollViewHeightScale;
			MaxSeparationCount = settings.MaxSeparationCount;
			DisplayButtonIndex = settings.DisplayButtonIndex;
			ButtonIndexQwerty = settings.ButtonIndexQwerty;
		}
	}
}
