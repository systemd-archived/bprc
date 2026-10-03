using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// 1:1 移植 badpiggies-editor 的 data/level_db.rs。
//
// 反向查询：`.contraption` 的 SHA1-hex 文件名 → 关卡场景名。
// 基础关卡元数据在运行时从内置 Unity episode prefab 解析：
//   - contraption 文件名词干 = SHA1(level_key) 前 10 字节的大写 hex
//   - sandbox 额外槽位用 `{scene}_{slot}`（slot 1..=40）
//   - race 变体用 `cr_{scene}_{track}`（track 0..=20）
//
// ★ 平台适配（唯一偏差）★
// Rust 用 serde_yaml 解析 prefab YAML；C# 无内置 YAML 库，改为**按这些 prefab 的实际结构定向解析**
// （已用只读探针确认真实文本形状）：
//   --- !u!114 &<fileID>
//   MonoBehaviour:
//     m_label: <int 或 S/R>
//     m_levelInfos:            （episode）
//     - sceneName: Level_21
//       levelLoaderPath: Assets/.../*_loader.prefab
//     m_levels:                （sandbox / race）
//     - m_identifier: S-1
//       m_levelLoaderPath: Assets/.../*_loader.prefab
// 序列在“同级缩进的非 `- ` 字段”处结束。标量按 serde_yaml 的类型语义近似判定
// （`sceneName` 等要求字符串标量，裸数字/布尔/null 视为非字符串 → 跳过该项）。

public enum BpLevelCategory { Episode, Sandbox, Race }

public class BpLevelEntry
{
	public string label = "";
	public string scene = "";
	public BpLevelCategory category;
}

public struct BpLabelScene
{
	public string label;
	public string scene;
}

public static class BpLevelDb
{
	private const string LEVELS_PREFAB_DIR = "Assets/Prefab/";
	private const string LEVELS_PREFAB_SUFFIX = "Levels.prefab";
	private const int SANDBOX_SLOT_COUNT = 40;
	private const int RACE_TRACK_COUNT = 20;

	private static Dictionary<string, BpLabelScene> s_lookup;
	private static Dictionary<string, BpLabelScene> s_sceneLookup;
	private static Dictionary<string, BpLabelScene> s_normalizedSceneLookup;

	public static void Reset()
	{
		s_lookup = null;
		s_sceneLookup = null;
		s_normalizedSceneLookup = null;
	}

	// ---------------- SHA1 ----------------

	// .contraption 文件名词干（20 个大写 hex 字符）
	private static string ContraptionHash(string levelKey)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(levelKey ?? "");
		byte[] hash;
		using (System.Security.Cryptography.SHA1 sha = System.Security.Cryptography.SHA1.Create())
			hash = sha.ComputeHash(bytes);

		StringBuilder sb = new StringBuilder(20);
		for (int i = 0; i < 10; i++) sb.Append(hash[i].ToString("X2"));
		return sb.ToString();
	}

	// ---------------- lookup 构建 ----------------

	private static Dictionary<string, BpLabelScene> Lookup()
	{
		if (s_lookup == null) s_lookup = BuildLookup();
		return s_lookup;
	}

	private static Dictionary<string, BpLabelScene> BuildLookup()
	{
		List<BpLevelEntry> entries = LoadBaseLevelEntries();
		Dictionary<string, BpLabelScene> map = new Dictionary<string, BpLabelScene>(entries.Count + 750);
		HashSet<string> sandboxScenes = new HashSet<string>();
		HashSet<string> raceScenes = new HashSet<string>();

		foreach (BpLevelEntry entry in entries)
		{
			string hash = ContraptionHash(entry.scene);
			map[hash] = new BpLabelScene { label = entry.label, scene = entry.scene };

			if (entry.category == BpLevelCategory.Sandbox) sandboxScenes.Add(entry.scene);
			else if (entry.category == BpLevelCategory.Race) raceScenes.Add(entry.scene);
		}

		foreach (string scene in sandboxScenes)
		{
			for (int slot = 1; slot <= SANDBOX_SLOT_COUNT; slot++)
			{
				string key = scene + "_" + slot.ToString(System.Globalization.CultureInfo.InvariantCulture);
				string hash = ContraptionHash(key);
				if (!map.ContainsKey(hash))
					map[hash] = new BpLabelScene { label = "", scene = key };
			}
		}

		foreach (string scene in raceScenes)
		{
			for (int track = 0; track <= RACE_TRACK_COUNT; track++)
			{
				string key = "cr_" + scene + "_" + track.ToString(System.Globalization.CultureInfo.InvariantCulture);
				string hash = ContraptionHash(key);
				if (!map.ContainsKey(hash))
					map[hash] = new BpLabelScene { label = "", scene = key };
			}
		}

		return map;
	}

	private static Dictionary<string, BpLabelScene> BuildSceneLookup()
	{
		Dictionary<string, BpLabelScene> map = new Dictionary<string, BpLabelScene>();
		foreach (BpLevelEntry entry in LoadBaseLevelEntries())
			map[entry.scene] = new BpLabelScene { label = entry.label, scene = entry.scene };
		return map;
	}

	// ---------------- 基础条目加载 ----------------

	private static List<BpLevelEntry> LoadBaseLevelEntries()
	{
		List<BpLevelEntry> entries = new List<BpLevelEntry>();

		foreach (string assetKey in BpUnityPackageLoader.ListPathnames(LEVELS_PREFAB_DIR, LEVELS_PREFAB_SUFFIX))
		{
			string filename = assetKey.StartsWith(LEVELS_PREFAB_DIR, StringComparison.Ordinal)
				? assetKey.Substring(LEVELS_PREFAB_DIR.Length)
				: assetKey;
			if (!filename.StartsWith("Episode", StringComparison.Ordinal)) continue;

			string text = BpUnityPackageLoader.ReadPathnameText(assetKey);
			if (text == null)
			{
				Debug.LogWarning("[BPEditor] Missing embedded level prefab for contraption lookup: " + assetKey);
				continue;
			}

			List<BpLevelEntry> parsed = ParseLevelsPrefab(text);
			if (parsed.Count == 0)
			{
				Debug.LogWarning("[BPEditor] Failed to parse base level metadata from embedded prefab: " + assetKey);
				continue;
			}

			entries.AddRange(parsed);
		}

		return entries;
	}

	// ---------------- prefab 定向解析 ----------------

	private static List<BpLevelEntry> ParseLevelsPrefab(string text)
	{
		foreach (string doc in SplitDocs(text))
		{
			string[] lines = doc.Split('\n');
			if (lines.Length == 0) continue;

			string header = lines[0].Trim();
			int typeId;
			string fileId;
			if (!ParseDocHeader(header, out typeId, out fileId)) continue;
			if (typeId != 114) continue;

			List<BpLevelEntry> entries = ParseDocFields(lines);
			if (entries.Count > 0) return entries;
		}
		return new List<BpLevelEntry>();
	}

	// 等价 Rust `text.split("--- ").skip(1)`
	private static List<string> SplitDocs(string text)
	{
		List<string> docs = new List<string>();
		if (text == null) return docs;
		int pos = 0;
		while (true)
		{
			int idx = text.IndexOf("--- ", pos, StringComparison.Ordinal);
			if (idx < 0) break;
			int start = idx + 4;
			int next = text.IndexOf("--- ", start, StringComparison.Ordinal);
			if (next < 0) docs.Add(text.Substring(start));
			else docs.Add(text.Substring(start, next - start));
			if (next < 0) break;
			pos = next;
		}
		return docs;
	}

	// `!u!114 &123456` → (114, "123456")
	private static bool ParseDocHeader(string header, out int typeId, out string fileId)
	{
		typeId = 0;
		fileId = null;
		string[] parts = header.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length < 2) return false;
		const string tag = "!u!";
		if (!parts[0].StartsWith(tag, StringComparison.Ordinal)) return false;
		if (!int.TryParse(parts[0].Substring(tag.Length), out typeId)) return false;
		if (!parts[1].StartsWith("&", StringComparison.Ordinal)) return false;
		fileId = parts[1].Substring(1);
		return true;
	}

	private class SeqItem
	{
		public int index;
		public string scene;
		public string identifier;
		public string loaderPath;
	}

	private static List<BpLevelEntry> ParseDocFields(string[] lines)
	{
		// 根映射键必须是 MonoBehaviour
		int rootIndex = -1;
		for (int i = 1; i < lines.Length; i++)
		{
			if (lines[i].Trim().Length == 0) continue;
			rootIndex = i;
			break;
		}
		if (rootIndex < 0) return new List<BpLevelEntry>();
		if (lines[rootIndex].Trim() != "MonoBehaviour:") return new List<BpLevelEntry>();

		int rootIndent = Indent(lines[rootIndex]);

		// 字段缩进 = 根之后第一个更深缩进行的缩进
		int fieldIndent = -1;
		for (int i = rootIndex + 1; i < lines.Length; i++)
		{
			if (lines[i].Trim().Length == 0) continue;
			int ind = Indent(lines[i]);
			if (ind > rootIndent) { fieldIndent = ind; }
			break;
		}
		if (fieldIndent < 0) return new List<BpLevelEntry>();

		string label = null;
		List<SeqItem> levelInfos = new List<SeqItem>();
		List<SeqItem> levels = new List<SeqItem>();

		string currentKey = null;
		SeqItem cur = null;
		int itemIndex = -1;

		for (int i = rootIndex + 1; i < lines.Length; i++)
		{
			string raw = lines[i];
			string trimmed = raw.Trim();
			if (trimmed.Length == 0) continue;

			int ind = Indent(raw);
			if (ind < fieldIndent) break;

			bool isListLine = trimmed.StartsWith("- ", StringComparison.Ordinal);

			if (ind == fieldIndent && !isListLine)
			{
				// 同级新字段 → 结束当前序列项
				cur = null;
				int colon = trimmed.IndexOf(':');
				if (colon < 0) { currentKey = null; continue; }
				currentKey = trimmed.Substring(0, colon).Trim();
				string val = trimmed.Substring(colon + 1).Trim();
				if (currentKey == "m_label") label = ValueAsScalarString(val);
				continue;
			}

			if (isListLine)
			{
				if (ind != fieldIndent) continue;   // 只处理同级项
				itemIndex++;
				cur = new SeqItem();
				cur.index = itemIndex;
				if (currentKey == "m_levelInfos") levelInfos.Add(cur);
				else if (currentKey == "m_levels") levels.Add(cur);
				else cur = null;

				// `- key: value` 行本身也可能带内容
				string inline = trimmed.Substring(2);
				if (cur != null) ApplyField(cur, inline);
				continue;
			}

			// 更深缩进 → 当前项的续行
			if (cur != null) ApplyField(cur, trimmed);
		}

		// ---- 组装条目（对应 Rust parse_level_entries）----

		if (levelInfos.Count > 0)
		{
			// 前置：m_label 必须是可用作前缀的标量
			if (label == null) return new List<BpLevelEntry>();
			List<BpLevelEntry> outEntries = new List<BpLevelEntry>();
			foreach (SeqItem item in levelInfos)
			{
				if (item.scene == null) continue;
				BpLevelEntry e = new BpLevelEntry();
				e.label = label + "-" + EpisodeLevelCode(item.index);
				e.scene = item.scene;
				e.category = BpLevelCategory.Episode;
				outEntries.Add(e);
			}
			return outEntries;
		}

		if (levels.Count > 0)
		{
			BpLevelCategory category;
			if (label == "S") category = BpLevelCategory.Sandbox;
			else if (label == "R") category = BpLevelCategory.Race;
			else return new List<BpLevelEntry>();

			List<BpLevelEntry> outEntries = new List<BpLevelEntry>();
			foreach (SeqItem item in levels)
			{
				if (item.identifier == null || item.loaderPath == null) continue;
				string scene = SceneNameFromLoaderPath(item.loaderPath);
				if (scene == null) continue;
				BpLevelEntry e = new BpLevelEntry();
				e.label = item.identifier;
				e.scene = scene;
				e.category = category;
				outEntries.Add(e);
			}
			return outEntries;
		}

		return new List<BpLevelEntry>();
	}

	private static void ApplyField(SeqItem cur, string trimmed)
	{
		int colon = trimmed.IndexOf(':');
		if (colon < 0) return;
		string key = trimmed.Substring(0, colon).Trim();
		string val = trimmed.Substring(colon + 1).Trim();
		switch (key)
		{
			case "sceneName": cur.scene = AsYamlString(val); break;
			case "m_identifier": cur.identifier = AsYamlString(val); break;
			case "m_levelLoaderPath": cur.loaderPath = AsYamlString(val); break;
		}
	}

	private static int Indent(string line)
	{
		int n = 0;
		while (n < line.Length && (line[n] == ' ' || line[n] == '\t')) n++;
		return n;
	}

	// m_label 用 Rust value_as_string 语义（字符串或整数都接受）
	private static string ValueAsScalarString(string val)
	{
		if (val == null) return null;
		string v = Unquote(val);
		if (v.Length == 0) return null;
		if (v == "null" || v == "~") return null;
		return v;
	}

	// 序列字段用 Rust as_str() 语义：要求字符串标量（裸数字/布尔/null 视为非字符串）
	private static string AsYamlString(string val)
	{
		if (val == null) return null;
		string v = Unquote(val);
		if (v.Length == 0) return null;
		if (v == "null" || v == "~" || v == "true" || v == "false") return null;
		if (LooksNumeric(v)) return null;
		return v;
	}

	private static bool LooksNumeric(string v)
	{
		bool sawDigit = false;
		for (int i = 0; i < v.Length; i++)
		{
			char c = v[i];
			if (c >= '0' && c <= '9') { sawDigit = true; continue; }
			if (c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E') continue;
			return false;
		}
		return sawDigit;
	}

	private static string Unquote(string v)
	{
		v = v.Trim();
		if (v.Length >= 2 &&
			((v[0] == '"' && v[v.Length - 1] == '"') || (v[0] == '\'' && v[v.Length - 1] == '\'')))
			return v.Substring(1, v.Length - 2);
		return v;
	}

	// ---------------- 编号 ----------------

	private static string EpisodeLevelCode(int levelIndex)
	{
		int oneBased = levelIndex + 1;
		if (oneBased % 5 != 0)
		{
			int normalNumber = oneBased - levelIndex / 5;
			return normalNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
		}
		return RomanNumeral(oneBased / 5);
	}

	private static string RomanNumeral(int value)
	{
		string[] ROMANS =
		{
			"I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X",
			"XI", "XII", "XIII", "XIV", "XV", "XVI"
		};
		if (value > 0 && value <= ROMANS.Length) return ROMANS[value - 1];
		return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
	}

	private static string SceneNameFromLoaderPath(string path)
	{
		if (path == null) return null;
		int slash = path.LastIndexOf('/');
		string filename = slash >= 0 ? path.Substring(slash + 1) : path;
		const string suffix = "_loader.prefab";
		if (filename.Length <= suffix.Length) return null;
		if (!filename.EndsWith(suffix, StringComparison.Ordinal)) return null;
		return filename.Substring(0, filename.Length - suffix.Length);
	}

	// ---------------- 文件名/场景键 ----------------

	private static string StripKnownLevelExtension(string filename)
	{
		string[] suffixes = { ".bytes", ".yaml", ".yml", ".toml" };
		foreach (string suffix in suffixes)
		{
			if (filename.Length > suffix.Length &&
				string.Compare(filename, filename.Length - suffix.Length, suffix, 0, suffix.Length,
					StringComparison.OrdinalIgnoreCase) == 0)
				return filename.Substring(0, filename.Length - suffix.Length);
		}
		return filename;
	}

	private static string LevelSceneKeyFromFilename(string filename)
	{
		string basename = filename ?? "";
		int slash = basename.LastIndexOfAny(new char[] { '/', '\\' });
		if (slash >= 0) basename = basename.Substring(slash + 1);

		string stem = StripKnownLevelExtension(basename);
		const string dataSuffix = "_data";
		if (stem.Length > dataSuffix.Length && stem.EndsWith(dataSuffix, StringComparison.Ordinal))
			return stem.Substring(0, stem.Length - dataSuffix.Length);
		return stem;
	}

	private static string FormatLevelDisplayName(string label, string scene)
	{
		if (string.IsNullOrEmpty(label)) return scene;
		return scene + " (" + label + ")";
	}

	private static string NormalizeSceneKey(string sceneKey)
	{
		string[] parts = (sceneKey ?? "").Trim().Split('_');
		for (int i = 0; i < parts.Length; i++)
		{
			string part = parts[i];
			bool allDigits = part.Length > 0;
			for (int j = 0; j < part.Length; j++)
				if (part[j] < '0' || part[j] > '9') { allDigits = false; break; }

			if (allDigits)
			{
				string stripped = part.TrimStart('0');
				parts[i] = stripped.Length == 0 ? "0" : stripped;
			}
			else
			{
				parts[i] = part.ToLowerInvariant();
			}
		}
		return string.Join("_", parts);
	}

	private static Dictionary<string, BpLabelScene> SceneLookup()
	{
		if (s_sceneLookup == null) s_sceneLookup = BuildSceneLookup();
		return s_sceneLookup;
	}

	private static Dictionary<string, BpLabelScene> NormalizedSceneLookup()
	{
		if (s_normalizedSceneLookup == null)
		{
			Dictionary<string, BpLabelScene> normalized = new Dictionary<string, BpLabelScene>();
			foreach (KeyValuePair<string, BpLabelScene> kv in SceneLookup())
			{
				string key = NormalizeSceneKey(kv.Key);
				if (!normalized.ContainsKey(key)) normalized[key] = kv.Value;
			}
			s_normalizedSceneLookup = normalized;
		}
		return s_normalizedSceneLookup;
	}

	// ---------------- 对外 API ----------------

	// 由场景键查基础关卡场景与编号标签；命中返回 true
	public static bool SceneLevelName(string sceneKey, out string label, out string scene)
	{
		label = null;
		scene = null;

		BpLabelScene hit;
		if (SceneLookup().TryGetValue(sceneKey ?? "", out hit))
		{
			label = hit.label;
			scene = hit.scene;
			return true;
		}
		if (NormalizedSceneLookup().TryGetValue(NormalizeSceneKey(sceneKey), out hit))
		{
			label = hit.label;
			scene = hit.scene;
			return true;
		}
		return false;
	}

	// `Level_21_data.bytes` → `Level_21 (1-1)`
	public static string LevelDisplayNameForFilename(string filename)
	{
		string sceneKey = LevelSceneKeyFromFilename(filename);
		string label, scene;
		if (!SceneLevelName(sceneKey, out label, out scene)) return null;
		return FormatLevelDisplayName(label, scene);
	}

	// `.contraption` 文件名（去扩展名的 20 位 hex）→ (label, scene)；命中返回 true
	public static bool ContraptionLevelName(string filenameStem, out string label, out string scene)
	{
		label = null;
		scene = null;
		string upper = (filenameStem ?? "").ToUpperInvariant();
		BpLabelScene hit;
		if (Lookup().TryGetValue(upper, out hit))
		{
			label = hit.label;
			scene = hit.scene;
			return true;
		}
		return false;
	}
}
