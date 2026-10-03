using System;
using System.Globalization;
using System.IO;
using System.Text;
using Innovation;

public class INContraptionDataManager
{
	private string m_dataDirectory;

	private StringBuilder m_builder;

	public string DataDirectory => m_dataDirectory;

	public static INContraptionDataManager Instance { get; private set; }

	public static ContraptionDataSettings Settings => INUserSettings.Instance.ContraptionDataSettings;

	public static void Create()
	{
		INContraptionDataManager iNContraptionDataManager = new INContraptionDataManager();
		iNContraptionDataManager.Initialize();
		Instance = iNContraptionDataManager;
	}

	public static void SetContraptionData()
	{
		if (INSettings.GetBool(INFeature.NewContraptionData))
		{
			Create();
		}
		else
		{
			Instance = null;
		}
	}

	public void Initialize()
	{
		int versionType = INSettings.VersionType;
		m_builder = new StringBuilder();
		m_dataDirectory = INUnity.DataPath + "/contraptions" + versionType switch
		{
			2 => "A", 
			1 => "O", 
			0 => "", 
			_ => "B", 
		};
		Directory.CreateDirectory(m_dataDirectory);
	}

	public ContraptionDataset LoadContraptionData(string levelName)
	{
		string dataDirectory = m_dataDirectory;
		if (!Settings.Enabled)
		{
			return WPFPrefs.LoadOriginalContraptionDataset(dataDirectory, levelName);
		}
		string text = dataDirectory + "/" + WPFPrefs.ContraptionFileName(levelName);
		string path = dataDirectory + "/" + levelName;
		ContraptionDataset result;
		if (File.Exists(path))
		{
			TryLoadAndConvert(path, out result);
		}
		else if (File.Exists(text))
		{
			result = WPFPrefs.LoadOriginalContraptionDataset(dataDirectory, levelName);
			if (Settings.BackupOriginalData)
			{
				BackupFile(text, text.Replace(".contraption", ".bak"));
			}
			else
			{
				File.Delete(text);
			}
			Save(path, INContraptionData.Create(result));
		}
		else
		{
			result = new ContraptionDataset();
		}
		return result;
	}

	public void SaveContraptionData(string levelName, ContraptionDataset data)
	{
		string dataDirectory = m_dataDirectory;
		if (!Settings.Enabled)
		{
			WPFPrefs.SaveOriginalContraptionDataset(dataDirectory, levelName, data);
			return;
		}
		string text = dataDirectory + "/" + levelName;
		if (File.Exists(text) && Settings.BackupData)
		{
			BackupFile(text, text + ".bak");
		}
		Save(text, INContraptionData.Create(data));
		if (Settings.SaveAsOriginalData)
		{
			WPFPrefs.SaveOriginalContraptionDataset(dataDirectory, levelName, data);
		}
	}

	private bool TryLoadAndConvert(string path, out ContraptionDataset result)
	{
		try
		{
			result = Load(path).ConvertTo();
			return true;
		}
		catch
		{
			result = new ContraptionDataset();
			return false;
		}
	}

	public INContraptionData Load(string path)
	{
		byte[] bytes = File.ReadAllBytes(path);
		if (BpirSaveConverter.TryConvert(bytes, true, out var imported, out _)) return imported.data;
		string text = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
		switch (Settings.LoadFormat)
		{
		case ContraptionDataSettings.SerializationFormat.ALL:
		{
			if (TryLoadDataFromCsv(text, out var result))
			{
				return result;
			}
			return LoadDataFromJson(text);
		}
		case ContraptionDataSettings.SerializationFormat.CSV:
			return LoadDataFromCsv(text);
		case ContraptionDataSettings.SerializationFormat.JSON:
			return LoadDataFromJson(text);
		default:
			return new INContraptionData();
		}
	}

	public static INContraptionData LoadDataFromCsv(string text)
	{
		if (BpirSaveConverter.LooksLikeExtendedText(text))
		{
			if (BpirSaveConverter.TryConvert(Encoding.UTF8.GetBytes(text), true, out var imported, out string error)) return imported.data;
			throw new FormatException(error);
		}
		IFormatProvider invariantInfo = NumberFormatInfo.InvariantInfo;
		string[] array = text.Split(new char[2] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
		int num = array.Length;
		INContraptionData iNContraptionData = new INContraptionData(num);
		for (int i = 0; i < num; i++)
		{
			string[] array2 = array[i].Split(new char[2] { ',', '\t' }, StringSplitOptions.RemoveEmptyEntries);
			INContraptionData.Unit unit = new INContraptionData.Unit();
			unit.Type = int.Parse(array2[0], invariantInfo);
			unit.Index = int.Parse(array2[1], invariantInfo);
			unit.X = int.Parse(array2[2], invariantInfo);
			unit.Y = int.Parse(array2[3], invariantInfo);
			unit.Rotation = int.Parse(array2[4], invariantInfo);
			unit.Flipped = Convert.ToBoolean(int.Parse(array2[5], invariantInfo));
			if (array2.Length > 6)
			{
				unit.CustomParams = array2[6];
			}
			iNContraptionData.Units.Add(unit);
		}
		return iNContraptionData;
	}

	public static bool TryLoadDataFromCsv(string text, out INContraptionData result)
	{
		result = null;
		if (string.IsNullOrWhiteSpace(text)) return false;
		if (BpirSaveConverter.LooksLikeExtendedText(text))
		{
			if (!BpirSaveConverter.TryConvert(Encoding.UTF8.GetBytes(text), true, out var imported, out _)) return false;
			result = imported.data; return true;
		}
		IFormatProvider invariantInfo = NumberFormatInfo.InvariantInfo;
		string[] array = text.Split(new char[2] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
		int num = array.Length;
		INContraptionData iNContraptionData = new INContraptionData(num);
		for (int i = 0; i < num; i++)
		{
			string[] array2 = array[i].Split(new char[2] { ',', '\t' }, StringSplitOptions.RemoveEmptyEntries);
			if (array2.Length != 6 && array2.Length != 7)
			{
				return false;
			}
			if (int.TryParse(array2[0], NumberStyles.Integer, invariantInfo, out var result2) && int.TryParse(array2[1], NumberStyles.Integer, invariantInfo, out var result3) && int.TryParse(array2[2], NumberStyles.Integer, invariantInfo, out var result4) && int.TryParse(array2[3], NumberStyles.Integer, invariantInfo, out var result5) && int.TryParse(array2[4], NumberStyles.Integer, invariantInfo, out var result6) && int.TryParse(array2[5], NumberStyles.Integer, invariantInfo, out var result7))
			{
				INContraptionData.Unit unit = new INContraptionData.Unit(result2, result3, result4, result5, result6, Convert.ToBoolean(result7));
				if (array2.Length > 6)
				{
					unit.CustomParams = array2[6];
				}
				iNContraptionData.Units.Add(unit);
				continue;
			}
			return false;
		}
		result = iNContraptionData;
		return true;
	}

	public static INContraptionData LoadDataFromJson(string text)
	{
		if (BpirSaveConverter.LooksLikeExtendedText(text))
		{
			if (BpirSaveConverter.TryConvert(Encoding.UTF8.GetBytes(text), true, out var imported, out string error)) return imported.data;
			throw new FormatException(error);
		}
		return Json.Deserialize<INContraptionData>(text);
	}

	public void Save(string path, INContraptionData data)
	{
		switch (Settings.SaveFormat)
		{
		case ContraptionDataSettings.SerializationFormat.ALL:
		case ContraptionDataSettings.SerializationFormat.CSV:
			SaveAsCsv(path, data);
			break;
		case ContraptionDataSettings.SerializationFormat.JSON:
			SaveAsJson(path, data);
			break;
		}
	}

	private void SaveAsCsv(string path, INContraptionData data)
	{
		using StreamWriter streamWriter = new StreamWriter(path);
		StringBuilder builder = m_builder;
		builder.Clear();
		foreach (INContraptionData.Unit unit in data.Units)
		{
			string value = ",";
			builder.Append(unit.Type.ToString());
			builder.Append(value);
			builder.Append(unit.Index.ToString());
			builder.Append(value);
			builder.Append(unit.X);
			builder.Append(value);
			builder.Append(unit.Y.ToString());
			builder.Append(value);
			builder.Append(unit.Rotation.ToString());
			builder.Append(value);
			builder.Append(Convert.ToInt32(unit.Flipped).ToString());
			// 单文件模式（配置分离=关）时，自定义参数作为第 7 列写进本体；
			// 配置分离=开时，参数已在 WPFPrefs 里被剥离，这里自然为空。
			if (!string.IsNullOrEmpty(unit.CustomParams))
			{
				builder.Append(value);
				builder.Append(unit.CustomParams);
			}
			builder.AppendLine();
		}
		streamWriter.Write(builder.ToString());
	}

	private void SaveAsJson(string path, INContraptionData data)
	{
		using StreamWriter writer = new StreamWriter(path);
		Json.Serialize(writer, data);
	}

	private static void BackupFile(string srcPath, string destPath)
	{
		if (!File.Exists(srcPath) || string.Equals(srcPath, destPath, StringComparison.OrdinalIgnoreCase))
		{
			return;
		}
		try
		{
			if (File.Exists(destPath))
			{
				File.Delete(destPath);
			}
			File.Move(srcPath, destPath);
		}
		catch
		{
		}
	}
}
