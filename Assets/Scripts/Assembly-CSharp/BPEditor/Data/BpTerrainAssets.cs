using System;
using System.Collections.Generic;
using UnityEngine;

// 1:1 移植 badpiggies-editor 的 data/assets/terrain.rs。
// 地形名 → 贴图文件名，由内置 Unity 地形 prefab（"e2dTerrain*"）驱动。

public class BpTerrainTextureSet
{
	public string fill;    // null = 无
	public string splat0;
	public string splat1;
}

public static class BpTerrainAssets
{
	private const string TERRAIN_PREFAB_PREFIX = "e2dTerrain";
	private const string TERRAIN_SCRIPT_GUID = "dec592636f66e19d4a958df992538a81";

	private static Dictionary<string, BpTerrainTextureSet> s_textureSets;
	private static Dictionary<string, string> s_aliases;

	public static void Reset()
	{
		s_textureSets = null;
		s_aliases = null;
	}

	private static Dictionary<string, BpTerrainTextureSet> TextureSets()
	{
		if (s_textureSets == null) s_textureSets = BuildTerrainTextureSets();
		return s_textureSets;
	}

	private static Dictionary<string, string> Aliases()
	{
		if (s_aliases == null) s_aliases = BuildTerrainAliases();
		return s_aliases;
	}

	private static Dictionary<string, BpTerrainTextureSet> BuildTerrainTextureSets()
	{
		Dictionary<string, BpTerrainTextureSet> map = new Dictionary<string, BpTerrainTextureSet>();

		foreach (string assetPath in BpUnityPackageLoader.ListPathnames("Assets/Prefab/", ".prefab"))
		{
			if (!assetPath.StartsWith("Assets/Prefab/", StringComparison.Ordinal)) continue;
			string prefabName = assetPath.Substring("Assets/Prefab/".Length);
			if (!prefabName.EndsWith(".prefab", StringComparison.Ordinal)) continue;
			prefabName = prefabName.Substring(0, prefabName.Length - ".prefab".Length);
			if (!prefabName.StartsWith(TERRAIN_PREFAB_PREFIX, StringComparison.Ordinal)) continue;

			string text = BpUnityPackageLoader.ReadPathnameText(assetPath);
			if (text == null)
			{
				Debug.LogWarning("[BPEditor] Missing embedded terrain prefab: " + assetPath);
				continue;
			}
			BpTerrainTextureSet textureSet = ParseTerrainTextureSet(text);
			if (textureSet == null)
			{
				Debug.LogWarning("[BPEditor] Failed to parse embedded terrain prefab: " + assetPath);
				continue;
			}

			map[prefabName] = textureSet;
		}

		return map;
	}

	private static Dictionary<string, string> BuildTerrainAliases()
	{
		Dictionary<string, string> aliases = new Dictionary<string, string>();

		foreach (string name in TextureSets().Keys)
		{
			aliases[name] = name;
			string canonical = CanonicalizeName(name);
			if (!aliases.ContainsKey(canonical)) aliases[canonical] = name;
		}

		if (TextureSets().ContainsKey("e2dTerrainDark"))
		{
			// 注意：canonicalize 后 "e2dTerrainDark cave" 与 "e2dTerrainDark_cave" 同为
			// "e2dterraindarkcave"，故第二条覆盖第一条（与 Rust 相同）
			aliases[CanonicalizeName("e2dTerrainDark cave")] = "e2dTerrainDark";
			aliases[CanonicalizeName("e2dTerrainDark_cave")] = "e2dTerrainDark";
		}

		return aliases;
	}

	private static BpTerrainTextureSet ParseTerrainTextureSet(string raw)
	{
		string target = "m_Script: {fileID: 11500000, guid: " + TERRAIN_SCRIPT_GUID + ", type: 3}";

		string doc = null;
		foreach (string candidate in BpAssets.SplitYamlDocs(raw))
		{
			foreach (string line in candidate.Split('\n'))
			{
				if (line.Trim() == target) { doc = candidate; break; }
			}
			if (doc != null) break;
		}
		if (doc == null) return null;

		string fill = null;
		foreach (string line in doc.Split('\n'))
		{
			const string key = "FillTexture: ";
			string trimmed = line.Trim();
			if (trimmed.StartsWith(key, StringComparison.Ordinal))
			{
				fill = TextureNameFromReference(trimmed.Substring(key.Length));
				break;
			}
		}

		List<string> curveTextures = new List<string>();
		bool inCurveTextures = false;
		string currentCurveTexture = null;

		foreach (string line in doc.Split('\n'))
		{
			string trimmed = line.Trim();
			if (trimmed == "CurveTextures:")
			{
				inCurveTextures = true;
				continue;
			}
			if (!inCurveTextures) continue;

			if (trimmed.StartsWith("PlasticEdges:", StringComparison.Ordinal))
			{
				if (currentCurveTexture != null) { curveTextures.Add(currentCurveTexture); currentCurveTexture = null; }
				break;
			}

			const string refKey = "- texture: ";
			if (trimmed.StartsWith(refKey, StringComparison.Ordinal))
			{
				if (currentCurveTexture != null) { curveTextures.Add(currentCurveTexture); currentCurveTexture = null; }
				string textureName = TextureNameFromReference(trimmed.Substring(refKey.Length));
				if (textureName != null) currentCurveTexture = textureName;
				continue;
			}
		}

		if (currentCurveTexture != null) curveTextures.Add(currentCurveTexture);

		BpTerrainTextureSet set = new BpTerrainTextureSet();
		set.fill = fill;
		set.splat0 = curveTextures.Count > 0 ? curveTextures[0] : null;
		set.splat1 = curveTextures.Count > 1 ? curveTextures[1] : null;
		return set;
	}

	private static string TextureNameFromReference(string reference)
	{
		string guid = ExtractGuid(reference);
		if (guid == null) return null;
		return BpMaterialRefs.TextureNameForGuid(guid);
	}

	private static string ExtractGuid(string text)
	{
		const string field = "guid: ";
		int start = text.IndexOf(field, StringComparison.Ordinal);
		if (start < 0) return null;
		start += field.Length;
		int end = start;
		while (end < text.Length && text[end] != ',' && text[end] != '}') end++;
		return text.Substring(start, end - start).Trim();
	}

	// 把二进制地形对象名归一化到已知 prefab key
	private static string NormalizeTerrain(string raw)
	{
		string n = raw ?? "";

		// 去掉过渡后缀：" _ to ..." / " - to ..."
		int pos = n.IndexOf(" _ to ", StringComparison.Ordinal);
		if (pos < 0) pos = n.IndexOf(" - to ", StringComparison.Ordinal);
		if (pos >= 0) n = n.Substring(0, pos);

		// 去掉尾部注解，如 " EP1"
		pos = n.LastIndexOf(" EP", StringComparison.Ordinal);
		if (pos >= 0) n = n.Substring(0, pos);

		// 去掉尾部 " - ..."
		pos = n.LastIndexOf(" - ", StringComparison.Ordinal);
		if (pos >= 0) n = n.Substring(0, pos);

		// 去掉尾部数字后缀，如 " 131x3"
		string trimmedEnd = n.TrimEnd();
		pos = trimmedEnd.LastIndexOf(' ');
		if (pos >= 0)
		{
			string tail = trimmedEnd.Substring(pos + 1);
			if (tail.Length > 0 && tail[0] >= '0' && tail[0] <= '9') n = n.Substring(0, pos);
		}

		n = n.Trim();

		// 去掉 _X/_x 后缀
		if (n.EndsWith("_X", StringComparison.Ordinal) || n.EndsWith("_x", StringComparison.Ordinal))
			n = n.Substring(0, n.Length - 2);

		// 去掉尾部 " -"
		if (n.EndsWith(" -", StringComparison.Ordinal))
		{
			n = n.Substring(0, n.Length - 2);
			n = n.Trim();
		}

		return n;
	}

	private static string CanonicalizeName(string name)
	{
		if (name == null) return "";
		System.Text.StringBuilder sb = new System.Text.StringBuilder(name.Length);
		for (int i = 0; i < name.Length; i++)
		{
			char c = name[i];
			bool alnum = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
			if (!alnum) continue;
			if (c >= 'A' && c <= 'Z') c = (char)(c + 32);
			sb.Append(c);
		}
		return sb.ToString();
	}

	private static string ResolveTerrainPrefabKey(string name)
	{
		string normalized = NormalizeTerrain(name);
		Dictionary<string, string> aliases = Aliases();
		string key;
		if (aliases.TryGetValue(normalized, out key)) return key;
		if (aliases.TryGetValue(CanonicalizeName(normalized), out key)) return key;
		return null;
	}

	// ---------------- 对外 API ----------------

	public static string GetTerrainFillTexture(string terrainName)
	{
		string key = ResolveTerrainPrefabKey(terrainName);
		if (key == null) return null;
		BpTerrainTextureSet set;
		return TextureSets().TryGetValue(key, out set) ? set.fill : null;
	}

	public static string GetTerrainSplat0(string terrainName)
	{
		string key = ResolveTerrainPrefabKey(terrainName);
		if (key == null) return null;
		BpTerrainTextureSet set;
		return TextureSets().TryGetValue(key, out set) ? set.splat0 : null;
	}

	public static string GetTerrainSplat1(string terrainName)
	{
		string key = ResolveTerrainPrefabKey(terrainName);
		if (key == null) return null;
		BpTerrainTextureSet set;
		return TextureSets().TryGetValue(key, out set) ? set.splat1 : null;
	}

	// Rust 中 level_key 未参与计算（保留签名以对齐）
	public static string GetTerrainSplat1ForLevel(string levelKey, string terrainName)
	{
		return GetTerrainSplat1(terrainName);
	}
}
