using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Serialization;
using UnityEngine;

public class WPFPrefs : UnityEngine.Object
{
	private static CryptoUtility m_crypto = new CryptoUtility("3b91A049Ca7HvSjhxT35");

	public static void WriteGhostPlayerData(string filename, GhostPlayer gp)
	{
		XmlSerializer xmlSerializer = new XmlSerializer(typeof(GhostPlayer));
		FileStream fileStream = new FileStream(Application.persistentDataPath + "/" + filename, FileMode.Create);
		xmlSerializer.Serialize(fileStream, gp);
		fileStream.Close();
	}

	public static GhostPlayer ReadGhostPlayerData(string filename)
	{
		XmlSerializer xmlSerializer = new XmlSerializer(typeof(GhostPlayer));
		GhostPlayer result = new GhostPlayer();
		try
		{
			FileStream fileStream = new FileStream(Application.persistentDataPath + "/" + filename, FileMode.Open);
			result = xmlSerializer.Deserialize(fileStream) as GhostPlayer;
			fileStream.Close();
		}
		catch
		{
		}
		return result;
	}

	public static string ContraptionFileName(string levelName)
	{
		return BitConverter.ToString(CryptoUtility.ComputeHash(Encoding.UTF8.GetBytes(levelName))).Substring(0, 30).Replace("-", string.Empty) + ".contraption";
	}

	public static void SaveContraptionDataset(string levelName, ContraptionDataset cds)
	{
		string bodyPath = BodyPath(levelName, out bool newData);
		// “配置分离”开关：开=本体与自定义配置存为两个文件；关=合成一个文件（本体里带参数）。
		if (INParamSystem.Enabled)
		{
			if (GetSeparateConfig())
			{
				SaveParamsSidecar(bodyPath, cds);
			}
			else
			{
				RemoveParamsSidecar(bodyPath);
			}
		}
		if (!newData)
		{
			SaveOriginalContraptionDataset(INUnity.DataPath + "/contraptions", levelName, cds);
		}
		else
		{
			INContraptionDataManager.Instance.SaveContraptionData(levelName, cds);
		}
	}

	// 读取“配置分离”开关（默认关）
	private static bool GetSeparateConfig()
	{
		try
		{
			ContraptionDataSettings settings = (INUserSettings.Instance != null) ? INUserSettings.Instance.ContraptionDataSettings : null;
			return settings != null && settings.SeparateConfig;
		}
		catch
		{
			return false;
		}
	}

	private static void RemoveParamsSidecar(string bodyPath)
	{
		try
		{
			string sidecar = bodyPath + ".params";
			if (File.Exists(sidecar))
			{
				File.Delete(sidecar);
			}
		}
		catch
		{
		}
	}

	// 本体文件路径（仅用于生成旁挂参数文件路径）：<dir>/<levelName> 或 <dir>/<hash>.contraption
	private static string BodyPath(string levelName, out bool newData)
	{
		newData = INSettings.GetBool(INFeature.NewContraptionData);
		if (!newData)
		{
			return INUnity.DataPath + "/contraptions/" + ContraptionFileName(levelName);
		}
		return INContraptionDataManager.Instance.DataDirectory + "/" + levelName;
	}

	// 写旁挂参数文件：每行 "x,y,type,index,rot,flip,params"（仅含有参数的单位）；写完把参数从本体剥离。
	private static void SaveParamsSidecar(string bodyPath, ContraptionDataset cds)
	{
		if (cds == null)
		{
			return;
		}
		string sidecar = bodyPath + ".params";
		StringBuilder builder = new StringBuilder();
		foreach (ContraptionDataset.ContraptionDatasetUnit unit in cds.ContraptionDatasetList)
		{
			if (unit == null || string.IsNullOrEmpty(unit.customParams))
			{
				continue;
			}
			builder.Append(unit.x).Append(',')
				.Append(unit.y).Append(',')
				.Append(unit.partType).Append(',')
				.Append(unit.customPartIndex).Append(',')
				.Append(unit.rot).Append(',')
				.Append(unit.flipped ? 1 : 0).Append(',')
				.Append(unit.customParams).Append('\n');
			// 从本体剥离（本体保持原格式，不含自定义配置）
			unit.customParams = null;
		}
		try
		{
			if (builder.Length > 0)
			{
				string dir = Path.GetDirectoryName(sidecar);
				if (!string.IsNullOrEmpty(dir))
				{
					Directory.CreateDirectory(dir);
				}
				File.WriteAllText(sidecar, builder.ToString());
			}
			else if (File.Exists(sidecar))
			{
				File.Delete(sidecar);
			}
		}
		catch
		{
		}
	}

	// 读旁挂参数文件，按 (x,y,type,index,rot,flip) 合并回本体单位。
	private static void ApplyParamsSidecar(string bodyPath, ContraptionDataset cds)
	{
		if (cds == null)
		{
			return;
		}
		string sidecar = bodyPath + ".params";
		if (!File.Exists(sidecar))
		{
			return;
		}
		try
		{
			Dictionary<string, Queue<string>> map = new Dictionary<string, Queue<string>>();
			foreach (string line in File.ReadAllLines(sidecar))
			{
				if (string.IsNullOrEmpty(line))
				{
					continue;
				}
				string[] parts = line.Split(',');
				if (parts.Length < 7)
				{
					continue;
				}
				string key = parts[0] + "," + parts[1] + "," + parts[2] + "," + parts[3] + "," + parts[4] + "," + parts[5];
				if (!map.TryGetValue(key, out var queue))
				{
					queue = new Queue<string>();
					map[key] = queue;
				}
				queue.Enqueue(parts[6]);
			}
			foreach (ContraptionDataset.ContraptionDatasetUnit unit in cds.ContraptionDatasetList)
			{
				if (unit == null)
				{
					continue;
				}
				string key = unit.x + "," + unit.y + "," + unit.partType + "," + unit.customPartIndex + "," + unit.rot + "," + (unit.flipped ? 1 : 0);
				if (map.TryGetValue(key, out var queue) && queue.Count > 0)
				{
					unit.customParams = queue.Dequeue();
				}
			}
		}
		catch
		{
		}
	}

	public static void SaveOriginalContraptionDataset(string directory, string levelName, ContraptionDataset cds)
	{
		XmlSerializer xmlSerializer = new XmlSerializer(typeof(ContraptionDataset));
		MemoryStream memoryStream = new MemoryStream();
		StreamWriter textWriter = new StreamWriter(memoryStream, Encoding.UTF8);
		xmlSerializer.Serialize(textWriter, cds);
		byte[] clearTextBytes = memoryStream.ToArray();
		memoryStream.Close();
		byte[] array = m_crypto.Encrypt(clearTextBytes);
		string text = ContraptionFileName(levelName);
		Directory.CreateDirectory(directory);
		FileStream fileStream = new FileStream(directory + "/" + text, FileMode.Create);
		fileStream.Write(array, 0, array.Length);
		fileStream.Close();
	}

	public static void SaveLevelBluePrint(string levelName, ContraptionDataset cds, bool isSuperBlueprint = false, int superBluePrintIndex = 0)
	{
		XmlSerializer xmlSerializer = new XmlSerializer(typeof(ContraptionDataset));
		MemoryStream memoryStream = new MemoryStream();
		StreamWriter textWriter = new StreamWriter(memoryStream, Encoding.UTF8);
		xmlSerializer.Serialize(textWriter, cds);
		byte[] array = memoryStream.ToArray();
		memoryStream.Close();
		string path = ((!isSuperBlueprint) ? "Data/Contraptions" : "Data/SuperContraptions");
		string path2 = levelName + "_contraption" + ((!isSuperBlueprint) ? string.Empty : $"_{superBluePrintIndex + 1:00}") + ".xml";
		FileStream fileStream = new FileStream(Path.Combine(Path.Combine(Application.dataPath, path), path2), FileMode.Create);
		fileStream.Write(array, 0, array.Length);
		fileStream.Close();
	}

	public static ContraptionDataset LoadContraptionDataset(string levelName)
	{
		// 本体与“自定义配置”分离：先按原逻辑读本体，再合并旁挂参数文件。
		string bodyPath = BodyPath(levelName, out bool newData);
		ContraptionDataset result;
		if (!newData)
		{
			result = LoadOriginalContraptionDataset(INUnity.DataPath + "/contraptions", levelName);
		}
		else
		{
			result = INContraptionDataManager.Instance.LoadContraptionData(levelName);
		}
		if (INParamSystem.Enabled) ApplyParamsSidecar(bodyPath, result);
		return result;
	}

	public static ContraptionDataset LoadOriginalContraptionDataset(string directory, string levelName)
	{
		XmlSerializer xmlSerializer = new XmlSerializer(typeof(ContraptionDataset));
		ContraptionDataset result = new ContraptionDataset();
		string text = ContraptionFileName(levelName);
		try
		{
			byte[] array = File.ReadAllBytes(directory + "/" + text);
			if (BpirSaveConverter.TryConvert(array, true, out var imported, out _)) return imported.data.ConvertTo();
			using MemoryStream stream = new MemoryStream(m_crypto.Decrypt(array, 0));
			result = xmlSerializer.Deserialize(stream) as ContraptionDataset;
		}
		catch
		{
		}
		return result;
	}

	public static ContraptionDataset LoadContraptionDataset(TextAsset textAsset)
	{
		XmlSerializer xmlSerializer = new XmlSerializer(typeof(ContraptionDataset));
		ContraptionDataset result = new ContraptionDataset();
		try
		{
			if (BpirSaveConverter.TryConvert(textAsset.bytes, true, out var imported, out _)) return imported.data.ConvertTo();
			using MemoryStream memoryStream = new MemoryStream(textAsset.bytes);
			result = xmlSerializer.Deserialize(memoryStream) as ContraptionDataset;
		}
		catch
		{
		}
		return result;
	}
}
