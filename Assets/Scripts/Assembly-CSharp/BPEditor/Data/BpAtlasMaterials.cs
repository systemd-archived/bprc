using System;
using System.Collections.Generic;
using UnityEngine;

// 1:1 移植 badpiggies-editor 的 data/assets/atlas_materials.rs。
// 从 "Assets/Resources/utility/atlasmaterials.prefab" 文本解析 5 个材质列表，
// 每段前 3 个 guid 依次映射到 IngameAtlas.png / IngameAtlas2.png / IngameAtlas3.png
// （键为 guid 前 8 位）。后出现的段覆盖同名键 —— 与 Rust 相同。

public static class BpAtlasMaterials
{
	private const string ATLAS_MATERIALS_ASSET = "Assets/Resources/utility/atlasmaterials.prefab";
	private static readonly string[] ATLAS_FILES = { "IngameAtlas.png", "IngameAtlas2.png", "IngameAtlas3.png" };
	private static readonly string[] SECTIONS =
	{
		"normalMaterials",
		"dimmedRenderQueueMaterials",
		"renderQueueMaterials",
		"partQueueZMaterials",
		"grayMaterials"
	};

	private static Dictionary<string, string> s_atlasByMaterialGuid;

	private static Dictionary<string, string> AtlasByMaterialGuid()
	{
		if (s_atlasByMaterialGuid == null) s_atlasByMaterialGuid = BuildAtlasByMaterialGuid();
		return s_atlasByMaterialGuid;
	}

	// 供资源就绪后重新加载（Rust 用 OnceLock，进程内只建一次；此处提供显式失效）
	public static void Reset() { s_atlasByMaterialGuid = null; }

	private static Dictionary<string, string> BuildAtlasByMaterialGuid()
	{
		Dictionary<string, string> atlasByGuid = new Dictionary<string, string>();

		string text = BpUnityPackageLoader.ReadPathnameText(ATLAS_MATERIALS_ASSET);
		if (text == null)
		{
			Debug.LogError("[BPEditor] Failed to read " + ATLAS_MATERIALS_ASSET + " for atlas material mapping");
			return atlasByGuid;
		}

		Dictionary<string, List<string>> guidsBySection = new Dictionary<string, List<string>>();
		string currentSection = null;

		string[] lines = text.Split('\n');
		foreach (string raw in lines)
		{
			string trimmed = raw.Trim();

			if (trimmed == "normalMaterials:") currentSection = "normalMaterials";
			else if (trimmed == "dimmedRenderQueueMaterials:") currentSection = "dimmedRenderQueueMaterials";
			else if (trimmed == "renderQueueMaterials:") currentSection = "renderQueueMaterials";
			else if (trimmed == "partQueueZMaterials:") currentSection = "partQueueZMaterials";
			else if (trimmed == "grayMaterials:") currentSection = "grayMaterials";
			else if (trimmed.EndsWith(":", StringComparison.Ordinal)) currentSection = null;

			if (!trimmed.StartsWith("- ", StringComparison.Ordinal)) continue;
			if (currentSection == null) continue;
			string guid = ExtractGuid(trimmed);
			if (string.IsNullOrEmpty(guid)) continue;

			List<string> list;
			if (!guidsBySection.TryGetValue(currentSection, out list))
			{
				list = new List<string>();
				guidsBySection[currentSection] = list;
			}
			list.Add(guid);
		}

		foreach (string section in SECTIONS)
		{
			List<string> guids;
			if (!guidsBySection.TryGetValue(section, out guids)) continue;
			int take = guids.Count < ATLAS_FILES.Length ? guids.Count : ATLAS_FILES.Length;
			for (int index = 0; index < take; index++)
				atlasByGuid[GuidPrefix(guids[index])] = ATLAS_FILES[index];
		}

		return atlasByGuid;
	}

	// 行内 "guid: <hex>"，取到第一个非十六进制字符为止
	private static string ExtractGuid(string line)
	{
		int start = line.IndexOf("guid: ", StringComparison.Ordinal);
		if (start < 0) return null;
		start += "guid: ".Length;
		int end = start;
		while (end < line.Length && IsAsciiHexDigit(line[end])) end++;
		if (end == start) return null;
		return line.Substring(start, end - start);
	}

	private static bool IsAsciiHexDigit(char c)
	{
		return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
	}

	private static string GuidPrefix(string materialGuid)
	{
		if (materialGuid == null) return "";
		return materialGuid.Length > 8 ? materialGuid.Substring(0, 8) : materialGuid;
	}

	public static string AtlasForMaterialGuid(string materialGuid)
	{
		if (materialGuid == null) return null;
		string atlas;
		if (AtlasByMaterialGuid().TryGetValue(GuidPrefix(materialGuid), out atlas)) return atlas;
		return null;
	}
}
