using System;
using System.Collections.Generic;
using UnityEngine;

// 1:1 移植 badpiggies-editor 的 data/assets/theme.rs 的**命名与主题检测**部分
// （该部分不依赖 data::bg_data；取色部分 ground_color/sky_top_color 等在 BpBgThemeColors.cs）。
// assets/theme.rs 与 bg_data 互相依赖，拆分顺序：A(本文件) → B(bg_data) → C(取色)。

public static class BpBgThemeNames
{
	// BG_THEME_PRIORITY
	public static readonly string[] BG_THEME_PRIORITY =
	{
		"MayaCave2Dark",
		"MayaCaveDark",
		"MayaCave",
		"MayaTemple",
		"MayaHigh",
		"Maya",
		"Jungle",
		"Plateau",
		"Morning",
		"Night",
		"Halloween",
		"Cave",
	};

	private static Dictionary<string, string> s_aliases;

	public static void Reset() { s_aliases = null; }

	// theme_name_for_background_prefab
	public static string ThemeNameForBackgroundPrefab(string name)
	{
		string normalized = NormalizeBgName(name);
		Dictionary<string, string> aliases = Aliases();
		string theme;
		if (aliases.TryGetValue(normalized, out theme)) return theme;
		return CanonicalBackgroundThemeName(normalized);
	}

	// normalize_bg_name
	public static string NormalizeBgName(string name)
	{
		if (name == null) return "";
		System.Text.StringBuilder sb = new System.Text.StringBuilder(name.Length);
		bool lastWasSeparator = false;

		for (int i = 0; i < name.Length; i++)
		{
			char ch = name[i];
			bool alnum = (ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z');
			if (alnum)
			{
				if (ch >= 'A' && ch <= 'Z') ch = (char)(ch + 32);
				sb.Append(ch);
				lastWasSeparator = false;
			}
			else if (!lastWasSeparator)
			{
				sb.Append('_');
				lastWasSeparator = true;
			}
		}

		// trim_matches('_')
		string s = sb.ToString();
		int start = 0;
		int end = s.Length;
		while (start < end && s[start] == '_') start++;
		while (end > start && s[end - 1] == '_') end--;
		return s.Substring(start, end - start);
	}

	private static Dictionary<string, string> Aliases()
	{
		if (s_aliases == null) s_aliases = BuildBackgroundThemeAliases();
		return s_aliases;
	}

	private static Dictionary<string, string> BuildBackgroundThemeAliases()
	{
		Dictionary<string, string> aliases = new Dictionary<string, string>();
		const string prefix = "Assets/Resources/environment/background/";

		foreach (string prefabPath in BpUnityPackageLoader.ListPathnames(prefix, ".prefab"))
		{
			string filename = prefabPath.StartsWith(prefix, StringComparison.Ordinal)
				? prefabPath.Substring(prefix.Length)
				: prefabPath;
			string prefabName = Stem(filename);
			if (prefabName == null) continue;

			string normalizedPrefabName = NormalizeBgName(prefabName);
			string themeName = ThemeNameFromBackgroundPrefabStem(normalizedPrefabName);
			if (themeName == null) continue;

			aliases[normalizedPrefabName] = themeName;
			aliases[NormalizeBgName(filename)] = themeName;
			aliases[NormalizeBgName(prefabPath)] = themeName;
		}

		return aliases;
	}

	private static string Stem(string filename)
	{
		try
		{
			string stem = System.IO.Path.GetFileNameWithoutExtension(filename);
			return string.IsNullOrEmpty(stem) ? null : stem;
		}
		catch (Exception) { return null; }
	}

	private static string ThemeNameFromBackgroundPrefabStem(string normalized)
	{
		const string prefix = "background_";
		if (normalized.StartsWith(prefix, StringComparison.Ordinal))
			normalized = normalized.Substring(prefix.Length);

		switch (normalized)
		{
			case "cave_01_set_1": return "Cave";
			case "forest_01_set_1": return "Morning";
			case "halloween": return "Halloween";
			case "jungle_01_set": return "Jungle";
			case "mm_01_set": return "Maya";
			case "mm_cave_01_set": return "MayaCave";
			case "mm_cave_01_set_dark": return "MayaCaveDark";
			case "mm_cave_02_set_dark": return "MayaCave2Dark";
			case "mm_high_01_set": return "MayaHigh";
			case "mm_temple_01_set_01": return "MayaTemple";
			case "night_01_set_1": return "Night";
			case "plateau_01_set": return "Plateau";
		}
		return null;
	}

	private static string CanonicalBackgroundThemeName(string normalized)
	{
		foreach (string themeName in BG_THEME_PRIORITY)
			if (NormalizeBgName(themeName) == normalized) return themeName;
		return null;
	}

	public static bool LooksLikeBackgroundName(string normalized)
	{
		return normalized.StartsWith("background_", StringComparison.Ordinal)
			|| normalized.StartsWith("background", StringComparison.Ordinal)
			|| normalized.StartsWith("bg_", StringComparison.Ordinal);
	}

	public static bool LooksLikeCloudSetName(string normalized)
	{
		return normalized.StartsWith("cloud", StringComparison.Ordinal)
			&& normalized.EndsWith("set", StringComparison.Ordinal);
	}

	private static int ThemePriority(string theme)
	{
		for (int i = 0; i < BG_THEME_PRIORITY.Length; i++)
			if (BG_THEME_PRIORITY[i] == theme) return i;
		return int.MaxValue;
	}

	// ---------------- 检测 ----------------

	private static string DetectBgThemeFromNamesBpleOrder(List<string> objectNames, bool isDarkLevel)
	{
		Func<string, bool> hasObject = target =>
		{
			foreach (string name in objectNames)
				if (string.Equals(name, target, StringComparison.OrdinalIgnoreCase)) return true;
			return false;
		};

		if (hasObject("Background_Jungle_01_SET")) return DetectBgThemeFromName("Background_Jungle_01_SET");
		if (hasObject("Background_Plateau_01_SET")) return DetectBgThemeFromName("Background_Plateau_01_SET");
		if (isDarkLevel || hasObject("Background_Cave_01_SET 1")) return DetectBgThemeFromName("Background_Cave_01_SET 1");
		if (hasObject("Background_Night_01_SET 1")) return DetectBgThemeFromName("Background_Night_01_SET 1");
		if (hasObject("Background_Forest_01_SET 1")) return DetectBgThemeFromName("Background_Forest_01_SET 1");
		if (hasObject("Background_Halloween")) return DetectBgThemeFromName("Background_Halloween");
		if (hasObject("Background_MM_01_SET")) return DetectBgThemeFromName("Background_MM_01_SET");
		if (hasObject("Background_MM_Temple_01_SET_01")) return DetectBgThemeFromName("Background_MM_Temple_01_SET_01");

		return null;
	}

	private static string DetectBgThemeFromNames(List<string> objectNames, bool isDarkLevel)
	{
		string bple = DetectBgThemeFromNamesBpleOrder(objectNames, isDarkLevel);
		if (bple != null) return bple;

		// 显式 Background*/bg_* 对象优先于装饰性 cloud set 名
		string bestBackgroundMatch = null;
		foreach (string name in objectNames)
		{
			string normalized = NormalizeBgName(name);
			if (!LooksLikeBackgroundName(normalized)) continue;
			string theme = ThemeNameForBackgroundPrefab(name);
			if (theme == null) continue;
			if (bestBackgroundMatch != null && ThemePriority(bestBackgroundMatch) <= ThemePriority(theme)) continue;
			bestBackgroundMatch = theme;
		}
		if (bestBackgroundMatch != null) return bestBackgroundMatch;

		// 无显式 Background* 根时的回退（cloud set 排除）
		string bestMatch = null;
		foreach (string name in objectNames)
		{
			string normalized = NormalizeBgName(name);
			if (LooksLikeCloudSetName(normalized)) continue;
			string theme = ThemeNameForBackgroundPrefab(name);
			if (theme == null) continue;
			if (theme == "Cave" && !LooksLikeBackgroundName(normalized)) continue;
			if (bestMatch != null && ThemePriority(bestMatch) <= ThemePriority(theme)) continue;
			bestMatch = theme;
		}
		return bestMatch;
	}

	private static string DetectBgThemeFromName(string name)
	{
		return ThemeNameForBackgroundPrefab(name);
	}

	// background_prefab_ref_index
	// 注意：Rust 语义为“先找到第一个含 ObjectReference/prefab 的 root，再对其取整数值”；
	// 若该节点无整数值则整体返回 None（不会继续找下一个 root）。
	public static int? BackgroundPrefabRefIndex(string raw)
	{
		List<BpOverrideNode> nodes = BpPrefabOverride.ParseOverrideText(raw);
		foreach (BpOverrideNode root in nodes)
		{
			BpOverrideNode found = root.FindDescendant(node =>
				node.nodeType == "ObjectReference" && node.name == "prefab");
			if (found != null) return found.ValueAsI32();
		}
		return null;
	}

	// background_override_root_name
	public static string BackgroundOverrideRootName(string raw)
	{
		List<BpOverrideNode> nodes = BpPrefabOverride.ParseOverrideText(raw);
		if (nodes.Count == 0) return null;
		return nodes[0].name;
	}

	// background_prefab_name
	public static string BackgroundPrefabName(string levelKey, string bgOverrideText)
	{
		if (bgOverrideText == null) return null;
		int? refIndex = BackgroundPrefabRefIndex(bgOverrideText);
		if (!refIndex.HasValue) return null;
		return BpLevelRefs.GetBackgroundPrefabRef(levelKey, refIndex.Value);
	}

	private static List<string> DetectionNameCandidates(string levelKey, List<string> objectNames, string bgOverrideText)
	{
		List<string> names = new List<string>(objectNames ?? new List<string>());

		if (bgOverrideText != null)
		{
			string prefabName = BackgroundPrefabName(levelKey, bgOverrideText);
			if (prefabName != null) names.Add(prefabName);
			string rootName = BackgroundOverrideRootName(bgOverrideText);
			if (rootName != null) names.Add(rootName);
		}

		return names;
	}

	// detect_bg_theme
	public static string DetectBgTheme(string levelKey, List<string> objectNames, string bgOverrideText)
	{
		return DetectBgThemeWithDarkLevel(levelKey, objectNames, bgOverrideText, false);
	}

	// detect_bg_theme_with_dark_level
	public static string DetectBgThemeWithDarkLevel(string levelKey, List<string> objectNames, string bgOverrideText, bool isDarkLevel)
	{
		List<string> names = DetectionNameCandidates(levelKey, objectNames, bgOverrideText);
		return DetectBgThemeFromNames(names, isDarkLevel);
	}
}
