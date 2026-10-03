using System;

[Serializable]
public class ContraptionDataSettings : SettingsBase
{
	public enum SerializationFormat
	{
		CSV = 0,
		JSON = 1,
		ALL = -1
	}

	private bool m_enabled;

	private int m_slotCount;

	private SerializationFormat m_loadFormat;

	private SerializationFormat m_saveFormat;

	private bool m_backupData;

	private bool m_backupOriginalData;

	private bool m_saveAsOriginalData;

	// 配置分离：开=本体与自定义配置存为两个文件；关=合成一个文件（默认关）
	private bool m_separateConfig;

	private float m_slotSpacing;

	private float m_slotScale;

	private int m_slotPerRow;

	private float m_slotRowHeight;

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

	public int SlotCount
	{
		get
		{
			return m_slotCount;
		}
		set
		{
			if (m_slotCount != value && value >= 3 && value <= 4096)
			{
				m_slotCount = value;
				OnPropertyChanged("SlotCount");
			}
		}
	}

	public SerializationFormat LoadFormat
	{
		get
		{
			return m_loadFormat;
		}
		set
		{
			if (m_loadFormat != value && Enum.IsDefined(typeof(SerializationFormat), value))
			{
				m_loadFormat = value;
				OnPropertyChanged("LoadFormat");
			}
		}
	}

	public SerializationFormat SaveFormat
	{
		get
		{
			return m_saveFormat;
		}
		set
		{
			if (m_saveFormat != value && Enum.IsDefined(typeof(SerializationFormat), value))
			{
				m_saveFormat = value;
				OnPropertyChanged("SaveFormat");
			}
		}
	}

	public bool BackupData
	{
		get
		{
			return m_backupData;
		}
		set
		{
			if (m_backupData != value)
			{
				m_backupData = value;
				OnPropertyChanged("BackupData");
			}
		}
	}

	public bool BackupOriginalData
	{
		get
		{
			return m_backupOriginalData;
		}
		set
		{
			if (m_backupOriginalData != value)
			{
				m_backupOriginalData = value;
				OnPropertyChanged("BackupOriginalData");
			}
		}
	}

	public bool SaveAsOriginalData
	{
		get
		{
			return m_saveAsOriginalData;
		}
		set
		{
			if (m_saveAsOriginalData != value)
			{
				m_saveAsOriginalData = value;
				OnPropertyChanged("SaveAsOriginalData");
			}
		}
	}

	// 配置分离：开=本体与自定义配置分两个文件；关=合成一个文件（默认关）
	public bool SeparateConfig
	{
		get
		{
			return m_separateConfig;
		}
		set
		{
			if (m_separateConfig != value)
			{
				m_separateConfig = value;
				OnPropertyChanged("SeparateConfig");
			}
		}
	}

	public float SlotSpacing
	{
		get
		{
			return m_slotSpacing;
		}
		set
		{
			if (m_slotSpacing != value && float.IsFinite(value) && value >= 0.1f)
			{
				m_slotSpacing = value;
				OnPropertyChanged("SlotSpacing");
			}
		}
	}

	public float SlotScale
	{
		get
		{
			return m_slotScale;
		}
		set
		{
			if (m_slotScale != value && float.IsFinite(value) && value > 0f)
			{
				m_slotScale = value;
				OnPropertyChanged("SlotScale");
			}
		}
	}

	public int SlotPerRow
	{
		get
		{
			return m_slotPerRow;
		}
		set
		{
			if (m_slotPerRow != value && value >= 1)
			{
				m_slotPerRow = value;
				OnPropertyChanged("SlotPerRow");
			}
		}
	}

	public float SlotRowHeight
	{
		get
		{
			return m_slotRowHeight;
		}
		set
		{
			if (m_slotRowHeight != value && float.IsFinite(value) && value >= 0.1f)
			{
				m_slotRowHeight = value;
				OnPropertyChanged("SlotRowHeight");
			}
		}
	}

	public ContraptionDataSettings()
	{
		Enabled = true;
		SlotCount = 8;
		LoadFormat = SerializationFormat.ALL;
		SaveFormat = SerializationFormat.CSV;
		BackupData = true;
		BackupOriginalData = true;
		SaveAsOriginalData = false;
		SeparateConfig = false;
		SlotSpacing = 2.5f;
		SlotScale = 0.8f;
		SlotPerRow = 10;
		SlotRowHeight = 1.8f;
	}

	public ContraptionDataSettings(ContraptionDataSettings settings)
		: this()
	{
		Update(settings);
	}

	public void Update(ContraptionDataSettings settings)
	{
		if (settings != null)
		{
			Enabled = settings.Enabled;
			SlotCount = settings.SlotCount;
			LoadFormat = settings.LoadFormat;
			SaveFormat = settings.SaveFormat;
			BackupData = settings.BackupData;
			BackupOriginalData = settings.BackupOriginalData;
			SaveAsOriginalData = settings.SaveAsOriginalData;
			SeparateConfig = settings.SeparateConfig;
			SlotSpacing = settings.SlotSpacing;
			SlotScale = settings.SlotScale;
			SlotPerRow = settings.SlotPerRow;
			SlotRowHeight = settings.SlotRowHeight;
		}
	}
}
