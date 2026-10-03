using System;
using System.Collections.Generic;
using UnityEngine;

// 1:1 移植 badpiggies-editor 的 domain/level/refs.rs（前半：关卡引用库）。
//
// 地形纹理引用、prefab 名、背景 prefab 名都在运行时从内置 Unity loader prefab
// 与 prefab YAML 重建：
//   - Assets/Resources/levels/*_loader.prefab  → 每个关卡的 m_prefabs / m_references
//   - Assets/Prefab/*.prefab                   → fileID → prefab 名（文件名词干）
//   - Assets/Resources/environment/background/*.prefab → m_RootGameObject fileID → 名
//
// 资源访问统一走 BpUnityPackageLoader（= Rust data::assets）。
// Rust 的 OnceLock 惰性单例 → C# 静态惰性初始化 + 失败回退空表（不 panic）。

public class BpLoaderReference
{
	public string guid;      // null = 无
	public string fileId;    // null = 无
	public int refType;
}

public class BpParsedLoaderPrefab
{
	public string levelKey = "";
	public List<string> prefabFileIds = new List<string>();
	public List<BpLoaderReference> references = new List<BpLoaderReference>();
}

public class BpLevelRefsData
{
	// level_key → (ref_index → texture_filename)
	public Dictionary<string, Dictionary<int, string>> refs = new Dictionary<string, Dictionary<int, string>>();
	// level_key → set(ref_index)（loader m_references 该条显式为 null，fileID 0）
	public Dictionary<string, HashSet<int>> nullRefs = new Dictionary<string, HashSet<int>>();
	// level_key → (prefab_index → corrected_name)
	public Dictionary<string, Dictionary<short, string>> prefabs = new Dictionary<string, Dictionary<short, string>>();
	// level_key → (ref_index → background prefab asset stem)
	public Dictionary<string, Dictionary<int, string>> backgroundPrefabs = new Dictionary<string, Dictionary<int, string>>();
}

public static class BpLevelRefs
{
	private const string LOADER_ASSET_PREFIX = "Assets/Resources/levels/";
	private const string PREFAB_ASSET_PREFIX = "Assets/Prefab/";
	private const string BACKGROUND_ASSET_PREFIX = "Assets/Resources/environment/background/";

	private static BpLevelRefsData s_data;

	private static BpLevelRefsData Data()
	{
		if (s_data == null)
		{
			try { s_data = TryLoadLevelRefs(); }
			catch (Exception ex)
			{
				Debug.LogError("[BPEditor] Failed to load level refs: " + ex.Message);
				s_data = new BpLevelRefsData();
			}
		}
		return s_data;
	}

	// 供资源变更后重建
	public static void Reset() { s_data = null; }

	private static BpLevelRefsData TryLoadLevelRefs()
	{
		List<BpParsedLoaderPrefab> loaders = LoadEmbeddedLoaders();
		Dictionary<string, string> prefabNames = LoadPrefabNamesByFileId();
		Dictionary<string, string> backgroundPrefabNames = LoadBackgroundPrefabNamesByRootFileId();

		BpLevelRefsData data = new BpLevelRefsData();
		data.refs = BuildRuntimeLevelRefs(loaders);
		data.nullRefs = BuildRuntimeNullLevelRefs(loaders);
		data.prefabs = BuildRuntimePrefabNames(loaders, prefabNames);
		data.backgroundPrefabs = BuildRuntimeBackgroundPrefabRefs(loaders, backgroundPrefabNames);
		return data;
	}

	// ---------------- null refs ----------------

	private static Dictionary<string, HashSet<int>> BuildRuntimeNullLevelRefs(List<BpParsedLoaderPrefab> loaders)
	{
		Dictionary<string, HashSet<int>> nullRefs = new Dictionary<string, HashSet<int>>();
		foreach (BpParsedLoaderPrefab loader in loaders)
		{
			HashSet<int> levelNullRefs = new HashSet<int>();
			for (int index = 0; index < loader.references.Count; index++)
			{
				BpLoaderReference reference = loader.references[index];
				if (reference.guid == null && reference.fileId == "0")
					levelNullRefs.Add(index);
			}
			nullRefs[loader.levelKey] = levelNullRefs;
		}
		return nullRefs;
	}

	// ---------------- loaders ----------------

	private static List<BpParsedLoaderPrefab> LoadEmbeddedLoaders()
	{
		List<BpParsedLoaderPrefab> loaders = new List<BpParsedLoaderPrefab>();

		foreach (string assetPath in BpUnityPackageLoader.ListPathnames(LOADER_ASSET_PREFIX, "_loader.prefab"))
		{
			string text = ReadEmbeddedText(assetPath);
			if (text == null) continue;
			BpParsedLoaderPrefab loader = ParseLoaderPrefab(text);
			if (loader == null) continue;
			loaders.Add(loader);
		}

		return loaders;
	}

	private static Dictionary<string, Dictionary<int, string>> BuildRuntimeLevelRefs(List<BpParsedLoaderPrefab> loaders)
	{
		Dictionary<string, Dictionary<int, string>> refs = new Dictionary<string, Dictionary<int, string>>();

		foreach (BpParsedLoaderPrefab loader in loaders)
		{
			Dictionary<int, string> levelRefs = new Dictionary<int, string>();
			for (int index = 0; index < loader.references.Count; index++)
			{
				BpLoaderReference reference = loader.references[index];
				if (reference.refType != 3) continue;
				if (reference.guid == null) continue;
				string textureName = BpMaterialRefs.TextureNameForGuid(reference.guid);
				if (textureName == null) continue;
				levelRefs[index] = textureName;
			}
			refs[loader.levelKey] = levelRefs;
		}

		return refs;
	}

	// ---------------- prefab 名 ----------------

	private static Dictionary<string, string> LoadPrefabNamesByFileId()
	{
		Dictionary<string, string> namesByFileId = new Dictionary<string, string>();

		foreach (string assetPath in BpUnityPackageLoader.ListPathnames(PREFAB_ASSET_PREFIX, ".prefab"))
		{
			string text = ReadEmbeddedText(assetPath);
			if (text == null) continue;
			string prefabName = AssetStem(assetPath);

			foreach (string fileId in ParsePrefabGameObjectIds(text))
			{
				if (!namesByFileId.ContainsKey(fileId))
					namesByFileId[fileId] = prefabName;
			}
		}

		return namesByFileId;
	}

	private static Dictionary<string, string> LoadBackgroundPrefabNamesByRootFileId()
	{
		Dictionary<string, string> namesByFileId = new Dictionary<string, string>();

		foreach (string assetPath in BpUnityPackageLoader.ListPathnames(BACKGROUND_ASSET_PREFIX, ".prefab"))
		{
			string text = BpUnityPackageLoader.ReadPathnameText(assetPath);
			if (text == null) continue;
			string fileId = ParsePrefabRootGameObjectId(text);
			if (fileId == null) continue;
			namesByFileId[fileId] = AssetStem(assetPath);
		}

		return namesByFileId;
	}

	private static string ParsePrefabRootGameObjectId(string text)
	{
		string[] lines = text.Split('\n');
		foreach (string raw in lines)
		{
			string trimmed = raw.Trim();
			const string prefix = "m_RootGameObject: {fileID: ";
			if (trimmed.StartsWith(prefix, StringComparison.Ordinal))
			{
				string value = trimmed.Substring(prefix.Length).TrimEnd('}').Trim();
				return value;
			}
		}
		return null;
	}

	private static List<string> ParsePrefabGameObjectIds(string text)
	{
		List<string> ids = new List<string>();
		string[] lines = text.Split('\n');
		foreach (string raw in lines)
		{
			string trimmed = raw.Trim();
			const string prefix = "--- !u!1 &";
			if (trimmed.StartsWith(prefix, StringComparison.Ordinal))
				ids.Add(trimmed.Substring(prefix.Length).Trim());
		}
		return ids;
	}

	private static Dictionary<string, Dictionary<short, string>> BuildRuntimePrefabNames(
		List<BpParsedLoaderPrefab> loaders, Dictionary<string, string> prefabNames)
	{
		Dictionary<string, Dictionary<short, string>> prefabs = new Dictionary<string, Dictionary<short, string>>();

		foreach (BpParsedLoaderPrefab loader in loaders)
		{
			Dictionary<short, string> levelPrefabs = new Dictionary<short, string>();
			for (int index = 0; index < loader.prefabFileIds.Count; index++)
			{
				if (index > short.MaxValue) continue;   // Rust: i16::try_from(index) 失败即跳过
				string fileId = loader.prefabFileIds[index];
				string name;
				if (!prefabNames.TryGetValue(fileId, out name)) continue;
				levelPrefabs[(short)index] = name;
			}
			prefabs[loader.levelKey] = levelPrefabs;
		}

		return prefabs;
	}

	private static Dictionary<string, Dictionary<int, string>> BuildRuntimeBackgroundPrefabRefs(
		List<BpParsedLoaderPrefab> loaders, Dictionary<string, string> backgroundPrefabNames)
	{
		Dictionary<string, Dictionary<int, string>> prefabs = new Dictionary<string, Dictionary<int, string>>();

		foreach (BpParsedLoaderPrefab loader in loaders)
		{
			Dictionary<int, string> levelPrefabs = new Dictionary<int, string>();
			for (int index = 0; index < loader.references.Count; index++)
			{
				BpLoaderReference reference = loader.references[index];
				if (reference.refType != 2) continue;
				if (reference.fileId == null) continue;
				string name;
				if (!backgroundPrefabNames.TryGetValue(reference.fileId, out name)) continue;
				levelPrefabs[index] = name;
			}
			prefabs[loader.levelKey] = levelPrefabs;
		}

		return prefabs;
	}

	// ---------------- 解析 ----------------

	private static string ReadEmbeddedText(string path)
	{
		byte[] bytes = BpUnityPackageLoader.ReadPathname(path);
		if (bytes == null)
		{
			Debug.LogError("[BPEditor] error_level_refs_missing_asset: " + path);
			return null;
		}
		try { return System.Text.Encoding.UTF8.GetString(bytes); }
		catch (Exception ex)
		{
			Debug.LogError("[BPEditor] error_level_refs_invalid_utf8: " + path + ": " + ex.Message);
			return null;
		}
	}

	private enum LoaderSection { None, Prefabs, References }

	private static BpParsedLoaderPrefab ParseLoaderPrefab(string text)
	{
		string assetName = null;
		List<string> prefabFileIds = new List<string>();
		List<BpLoaderReference> references = new List<BpLoaderReference>();
		LoaderSection section = LoaderSection.None;

		string[] lines = text.Split('\n');
		foreach (string raw in lines)
		{
			string trimmed = raw.Trim();

			if (trimmed.StartsWith("assetName:", StringComparison.Ordinal))
				assetName = trimmed.Substring("assetName:".Length).Trim();

			if (trimmed == "m_prefabs:") { section = LoaderSection.Prefabs; continue; }
			if (trimmed == "m_references:") { section = LoaderSection.References; continue; }

			if (section == LoaderSection.Prefabs)
			{
				if (trimmed.StartsWith("- ", StringComparison.Ordinal))
				{
					string fileId = ExtractLoaderField(trimmed, "fileID: ");
					if (fileId != null) prefabFileIds.Add(fileId);
					continue;
				}
				if (trimmed.StartsWith("m_", StringComparison.Ordinal) || trimmed.StartsWith("assetBundle:", StringComparison.Ordinal))
					section = LoaderSection.None;
			}
			else if (section == LoaderSection.References)
			{
				if (trimmed.StartsWith("- ", StringComparison.Ordinal))
				{
					references.Add(ParseLoaderReference(trimmed));
					continue;
				}
				if (trimmed.StartsWith("m_", StringComparison.Ordinal) || trimmed.StartsWith("assetBundle:", StringComparison.Ordinal))
					section = LoaderSection.None;
			}
		}

		if (assetName == null) return null;
		BpParsedLoaderPrefab loader = new BpParsedLoaderPrefab();
		loader.levelKey = assetName;
		loader.prefabFileIds = prefabFileIds;
		loader.references = references;
		return loader;
	}

	private static BpLoaderReference ParseLoaderReference(string line)
	{
		BpLoaderReference r = new BpLoaderReference();
		r.guid = ExtractLoaderField(line, "guid: ");
		r.fileId = ExtractLoaderField(line, "fileID: ");
		string typeText = ExtractLoaderField(line, "type: ");
		int parsed = 0;
		if (typeText != null) int.TryParse(typeText, out parsed);
		r.refType = parsed;
		return r;
	}

	// 取 field 后到 ',' 或 '}' 之前的内容（无则到行尾），trim 返回
	private static string ExtractLoaderField(string line, string field)
	{
		int start = line.IndexOf(field, StringComparison.Ordinal);
		if (start < 0) return null;
		start += field.Length;
		int end = line.IndexOf(',', start);
		if (end < 0) end = line.IndexOf('}', start);
		if (end < 0) end = line.Length;
		return line.Substring(start, end - start).Trim();
	}

	private static string AssetStem(string assetPath)
	{
		try
		{
			string stem = System.IO.Path.GetFileNameWithoutExtension(assetPath);
			return string.IsNullOrEmpty(stem) ? assetPath : stem;
		}
		catch (Exception) { return assetPath; }
	}

	// ---------------- 对外查询 ----------------

	// 从文件名推导 level-refs key（去掉 .bytes/.yaml/.yml/.toml，并做大小写不敏感规范化）
	public static string LevelKeyFromFilename(string filename)
	{
		if (filename == null) filename = "";
		string basename = filename;
		int slash = basename.LastIndexOfAny(new char[] { '/', '\\' });
		if (slash >= 0) basename = basename.Substring(slash + 1);

		string levelKey = basename;
		string[] suffixes = { ".bytes", ".yaml", ".yml", ".toml" };
		foreach (string suffix in suffixes)
		{
			if (levelKey.Length > suffix.Length &&
				string.Compare(levelKey, levelKey.Length - suffix.Length, suffix, 0, suffix.Length,
					StringComparison.OrdinalIgnoreCase) == 0)
			{
				levelKey = levelKey.Substring(0, levelKey.Length - suffix.Length);
				break;
			}
		}

		string trimmed = levelKey.Trim();
		if (trimmed.Length == 0) return levelKey;

		BpLevelRefsData data = Data();
		string canonical = FindKeyIgnoreCase(data.refs, trimmed);
		if (canonical != null) return canonical;
		canonical = FindKeyIgnoreCase(data.prefabs, trimmed);
		if (canonical != null) return canonical;
		canonical = FindKeyIgnoreCase(data.backgroundPrefabs, trimmed);
		if (canonical != null) return canonical;

		return levelKey;
	}

	private static string FindKeyIgnoreCase<T>(Dictionary<string, T> map, string key)
	{
		foreach (KeyValuePair<string, T> kv in map)
			if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase)) return kv.Key;
		return null;
	}

	// 按 level key + 引用下标取地形纹理文件名
	public static string GetLevelRef(string levelKey, int refIndex)
	{
		Dictionary<int, string> m;
		string v;
		if (Data().refs.TryGetValue(levelKey, out m) && m.TryGetValue(refIndex, out v)) return v;
		return null;
	}

	// loader m_references 在该下标是否为显式 null（{fileID: 0}）
	public static bool IsLevelRefNull(string levelKey, int refIndex)
	{
		HashSet<int> set;
		return Data().nullRefs.TryGetValue(levelKey, out set) && set.Contains(refIndex);
	}

	// loader prefab 下标 → 解析后的 prefab 名
	public static string GetPrefabOverride(string levelKey, short prefabIndex)
	{
		Dictionary<short, string> m;
		string v;
		if (Data().prefabs.TryGetValue(levelKey, out m) && m.TryGetValue(prefabIndex, out v)) return v;
		return null;
	}

	// loader 引用下标 → 背景 prefab 资源名（文件名词干）
	public static string GetBackgroundPrefabRef(string levelKey, int refIndex)
	{
		Dictionary<int, string> m;
		string v;
		if (Data().backgroundPrefabs.TryGetValue(levelKey, out m) && m.TryGetValue(refIndex, out v)) return v;
		return null;
	}
}
