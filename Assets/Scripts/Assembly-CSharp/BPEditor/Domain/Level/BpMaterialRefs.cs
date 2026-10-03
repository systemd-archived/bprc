using System;
using System.Collections.Generic;
using UnityEngine;

// 1:1 移植 badpiggies-editor 的 domain/level/refs.rs（后半：材质/着色器/纹理 guid 辅助）。
//
// 全部基于内置资源（BpUnityPackageLoader = Rust data::assets）：
//   - Assets/**/*.mat    → 主贴图 guid / _Color / _Cutoff / _MainTex ST / CustomRenderQueue / 着色器种类
//   - Assets/**/*.shader → Shader "Name" 行 → guid → 着色器名
// 对可碰撞的 8 位 guid 前缀另有 "prefix 表"：同一前缀映射到不同值则该前缀被丢弃（Rust build_unique_prefix_map）。

public enum BpMaterialShaderKind
{
	CustomUnlitMonochrome,
	CustomUnlitColorGeometry,
	CustomUnlitColorTransparentGeometry,
	CustomUnlitAlpha8BitColor,
	BuiltinUnlitTransparent,
	BuiltinUnlitTransparentCutout
}

public static class BpMaterialRefs
{
	// ---------------- 单 guid 查询（直读资源） ----------------

	private static string RuntimeTextureNameForGuid(string guid)
	{
		string pathname = BpUnityPackageLoader.PathnameForGuid(guid);
		if (pathname == null) return null;
		return TextureNameFromAssetPathname(pathname);
	}

	private static string RuntimeMaterialTextureNameForGuid(string guid)
	{
		string pathname = BpUnityPackageLoader.PathnameForGuid(guid);
		if (pathname == null) return null;
		if (!IsMaterialPathname(pathname)) return null;

		string text = BpUnityPackageLoader.ReadGuidText(guid);
		if (text == null) return null;
		string textureGuid = MainTextureGuidFromMaterial(text);
		if (textureGuid == null) return null;
		return TextureNameForGuid(textureGuid);
	}

	private static byte[] RuntimeMaterialColorRgbaForGuid(string guid)
	{
		string pathname = BpUnityPackageLoader.PathnameForGuid(guid);
		if (pathname == null) return null;
		if (!IsMaterialPathname(pathname)) return null;

		string text = BpUnityPackageLoader.ReadGuidText(guid);
		if (text == null) return null;
		return MainColorFromMaterial(text);
	}

	private static bool RuntimeMaterialAlphaBlendForGuid(string guid)
	{
		string pathname = BpUnityPackageLoader.PathnameForGuid(guid);
		if (pathname == null) return false;
		if (!IsMaterialPathname(pathname)) return false;

		string text = BpUnityPackageLoader.ReadGuidText(guid);
		if (text == null) return false;
		return MaterialUsesAlphaBlendShader(text, RuntimeShaderNamesByGuid());
	}

	private static bool RuntimeMaterialAlpha8BitForGuid(string guid)
	{
		string pathname = BpUnityPackageLoader.PathnameForGuid(guid);
		if (pathname == null) return false;
		if (!IsMaterialPathname(pathname)) return false;

		string text = BpUnityPackageLoader.ReadGuidText(guid);
		if (text == null) return false;
		return MaterialUsesAlpha8BitShader(text, RuntimeShaderNamesByGuid());
	}

	private static float? RuntimeMaterialCutoffForGuid(string guid)
	{
		string pathname = BpUnityPackageLoader.PathnameForGuid(guid);
		if (pathname == null) return null;
		if (!IsMaterialPathname(pathname)) return null;

		string text = BpUnityPackageLoader.ReadGuidText(guid);
		if (text == null) return null;
		return CutoffFromMaterial(text);
	}

	private static int? RuntimeMaterialCustomRenderQueueForGuid(string guid)
	{
		string pathname = BpUnityPackageLoader.PathnameForGuid(guid);
		if (pathname == null) return null;
		if (!IsMaterialPathname(pathname)) return null;

		string text = BpUnityPackageLoader.ReadGuidText(guid);
		if (text == null) return null;
		return CustomRenderQueueFromMaterial(text);
	}

	private static BpMaterialShaderKind? RuntimeMaterialShaderKindForGuid(string guid)
	{
		string pathname = BpUnityPackageLoader.PathnameForGuid(guid);
		if (pathname == null) return null;
		if (!IsMaterialPathname(pathname)) return null;

		string text = BpUnityPackageLoader.ReadGuidText(guid);
		if (text == null) return null;
		return MaterialShaderKindOf(text, RuntimeShaderNamesByGuid());
	}

	private static float[] RuntimeMaterialMainTexStForGuid(string guid)
	{
		string pathname = BpUnityPackageLoader.PathnameForGuid(guid);
		if (pathname == null) return null;
		if (!IsMaterialPathname(pathname)) return null;

		string text = BpUnityPackageLoader.ReadGuidText(guid);
		if (text == null) return null;
		return MainTextureStFromMaterial(text);
	}

	// ---------------- prefix 表（惰性） ----------------

	private static Dictionary<string, string> s_texPrefix;
	private static Dictionary<string, byte[]> s_colorPrefix;
	private static Dictionary<string, bool> s_alphaBlendPrefix;
	private static Dictionary<string, bool> s_alpha8BitPrefix;
	private static Dictionary<string, float> s_cutoffPrefix;
	private static Dictionary<string, int> s_renderQueuePrefix;
	private static Dictionary<string, BpMaterialShaderKind> s_shaderKindPrefix;
	private static Dictionary<string, float[]> s_mainTexStPrefix;
	private static Dictionary<string, string> s_shaderNamesByGuid;

	public static void Reset()
	{
		s_texPrefix = null; s_colorPrefix = null; s_alphaBlendPrefix = null; s_alpha8BitPrefix = null;
		s_cutoffPrefix = null; s_renderQueuePrefix = null; s_shaderKindPrefix = null;
		s_mainTexStPrefix = null; s_shaderNamesByGuid = null;
	}

	private static string RuntimeMaterialTextureNameForGuidPrefix(string guid)
	{
		if (s_texPrefix == null) s_texPrefix = BuildRuntimeMaterialTexturePrefixes();
		string v;
		return s_texPrefix.TryGetValue(GuidPrefix(guid), out v) ? v : null;
	}

	private static byte[] RuntimeMaterialColorRgbaForGuidPrefix(string guid)
	{
		if (s_colorPrefix == null) s_colorPrefix = BuildRuntimeMaterialColorPrefixes();
		byte[] v;
		return s_colorPrefix.TryGetValue(GuidPrefix(guid), out v) ? v : null;
	}

	private static bool RuntimeMaterialAlphaBlendForGuidPrefix(string guid)
	{
		if (s_alphaBlendPrefix == null) s_alphaBlendPrefix = BuildRuntimeMaterialAlphaBlendPrefixes();
		bool v;
		return s_alphaBlendPrefix.TryGetValue(GuidPrefix(guid), out v) && v;
	}

	private static bool RuntimeMaterialAlpha8BitForGuidPrefix(string guid)
	{
		if (s_alpha8BitPrefix == null) s_alpha8BitPrefix = BuildRuntimeMaterialAlpha8BitPrefixes();
		bool v;
		return s_alpha8BitPrefix.TryGetValue(GuidPrefix(guid), out v) && v;
	}

	private static float? RuntimeMaterialCutoffForGuidPrefix(string guid)
	{
		if (s_cutoffPrefix == null) s_cutoffPrefix = BuildRuntimeMaterialCutoffPrefixes();
		float v;
		return s_cutoffPrefix.TryGetValue(GuidPrefix(guid), out v) ? (float?)v : null;
	}

	private static int? RuntimeMaterialCustomRenderQueueForGuidPrefix(string guid)
	{
		if (s_renderQueuePrefix == null) s_renderQueuePrefix = BuildRuntimeMaterialCustomRenderQueuePrefixes();
		int v;
		return s_renderQueuePrefix.TryGetValue(GuidPrefix(guid), out v) ? (int?)v : null;
	}

	private static BpMaterialShaderKind? RuntimeMaterialShaderKindForGuidPrefix(string guid)
	{
		if (s_shaderKindPrefix == null) s_shaderKindPrefix = BuildRuntimeMaterialShaderKindPrefixes();
		BpMaterialShaderKind v;
		return s_shaderKindPrefix.TryGetValue(GuidPrefix(guid), out v) ? (BpMaterialShaderKind?)v : null;
	}

	private static float[] RuntimeMaterialMainTexStForGuidPrefix(string guid)
	{
		if (s_mainTexStPrefix == null) s_mainTexStPrefix = BuildRuntimeMaterialMainTexStPrefixes();
		float[] v;
		return s_mainTexStPrefix.TryGetValue(GuidPrefix(guid), out v) ? v : null;
	}

	// ---------------- prefix 表构建 ----------------

	// 遍历所有 .mat，用 pick(text) 取一个值；pick 为 null 则跳过
	private static Dictionary<string, string> BuildRuntimeMaterialTexturePrefixes()
	{
		Dictionary<string, string> byGuid = new Dictionary<string, string>();
		foreach (string pathname in BpUnityPackageLoader.ListPathnames("Assets/", ".mat"))
		{
			string guid = BpUnityPackageLoader.GuidForPathname(pathname);
			if (guid == null) continue;
			string text = BpUnityPackageLoader.ReadPathnameText(pathname);
			if (text == null) continue;
			string texGuid = MainTextureGuidFromMaterial(text);
			if (texGuid == null) continue;
			string name = TextureNameForGuid(texGuid);
			if (name == null) continue;
			byGuid[guid] = name;
		}
		return UniquePrefixMap(byGuid, (a, b) => string.Equals(a, b, StringComparison.Ordinal));
	}

	private static Dictionary<string, byte[]> BuildRuntimeMaterialColorPrefixes()
	{
		Dictionary<string, byte[]> byGuid = new Dictionary<string, byte[]>();
		foreach (string pathname in BpUnityPackageLoader.ListPathnames("Assets/", ".mat"))
		{
			string guid = BpUnityPackageLoader.GuidForPathname(pathname);
			if (guid == null) continue;
			string text = BpUnityPackageLoader.ReadPathnameText(pathname);
			if (text == null) continue;
			byte[] color = MainColorFromMaterial(text);
			if (color == null) continue;
			byGuid[guid] = color;
		}
		return UniquePrefixMap(byGuid, (a, b) => a[0] == b[0] && a[1] == b[1] && a[2] == b[2] && a[3] == b[3]);
	}

	private static Dictionary<string, bool> BuildRuntimeMaterialAlphaBlendPrefixes()
	{
		Dictionary<string, string> shaderNames = RuntimeShaderNamesByGuid();
		Dictionary<string, bool> byGuid = new Dictionary<string, bool>();
		foreach (string pathname in BpUnityPackageLoader.ListPathnames("Assets/", ".mat"))
		{
			string guid = BpUnityPackageLoader.GuidForPathname(pathname);
			if (guid == null) continue;
			string text = BpUnityPackageLoader.ReadPathnameText(pathname);
			if (text == null) continue;
			if (!MaterialUsesAlphaBlendShader(text, shaderNames)) continue;
			byGuid[guid] = true;
		}
		return UniquePrefixMap(byGuid, (a, b) => a == b);
	}

	private static Dictionary<string, bool> BuildRuntimeMaterialAlpha8BitPrefixes()
	{
		Dictionary<string, string> shaderNames = RuntimeShaderNamesByGuid();
		Dictionary<string, bool> byGuid = new Dictionary<string, bool>();
		foreach (string pathname in BpUnityPackageLoader.ListPathnames("Assets/", ".mat"))
		{
			string guid = BpUnityPackageLoader.GuidForPathname(pathname);
			if (guid == null) continue;
			string text = BpUnityPackageLoader.ReadPathnameText(pathname);
			if (text == null) continue;
			if (!MaterialUsesAlpha8BitShader(text, shaderNames)) continue;
			byGuid[guid] = true;
		}
		return UniquePrefixMap(byGuid, (a, b) => a == b);
	}

	private static Dictionary<string, float> BuildRuntimeMaterialCutoffPrefixes()
	{
		Dictionary<string, float> byGuid = new Dictionary<string, float>();
		foreach (string pathname in BpUnityPackageLoader.ListPathnames("Assets/", ".mat"))
		{
			string guid = BpUnityPackageLoader.GuidForPathname(pathname);
			if (guid == null) continue;
			string text = BpUnityPackageLoader.ReadPathnameText(pathname);
			if (text == null) continue;
			float? cutoff = CutoffFromMaterial(text);
			if (!cutoff.HasValue) continue;
			byGuid[guid] = cutoff.Value;
		}
		// Rust 用 to_bits 比较
		return UniquePrefixMap(byGuid, (a, b) => FloatBits(a) == FloatBits(b));
	}

	private static Dictionary<string, int> BuildRuntimeMaterialCustomRenderQueuePrefixes()
	{
		Dictionary<string, int> byGuid = new Dictionary<string, int>();
		foreach (string pathname in BpUnityPackageLoader.ListPathnames("Assets/", ".mat"))
		{
			string guid = BpUnityPackageLoader.GuidForPathname(pathname);
			if (guid == null) continue;
			string text = BpUnityPackageLoader.ReadPathnameText(pathname);
			if (text == null) continue;
			int? queue = CustomRenderQueueFromMaterial(text);
			if (!queue.HasValue) continue;
			byGuid[guid] = queue.Value;
		}
		return UniquePrefixMap(byGuid, (a, b) => a == b);
	}

	private static Dictionary<string, BpMaterialShaderKind> BuildRuntimeMaterialShaderKindPrefixes()
	{
		Dictionary<string, string> shaderNames = RuntimeShaderNamesByGuid();
		Dictionary<string, BpMaterialShaderKind> byGuid = new Dictionary<string, BpMaterialShaderKind>();
		foreach (string pathname in BpUnityPackageLoader.ListPathnames("Assets/", ".mat"))
		{
			string guid = BpUnityPackageLoader.GuidForPathname(pathname);
			if (guid == null) continue;
			string text = BpUnityPackageLoader.ReadPathnameText(pathname);
			if (text == null) continue;
			BpMaterialShaderKind? kind = MaterialShaderKindOf(text, shaderNames);
			if (!kind.HasValue) continue;
			byGuid[guid] = kind.Value;
		}
		return UniquePrefixMap(byGuid, (a, b) => a == b);
	}

	private static Dictionary<string, float[]> BuildRuntimeMaterialMainTexStPrefixes()
	{
		Dictionary<string, float[]> byGuid = new Dictionary<string, float[]>();
		foreach (string pathname in BpUnityPackageLoader.ListPathnames("Assets/", ".mat"))
		{
			string guid = BpUnityPackageLoader.GuidForPathname(pathname);
			if (guid == null) continue;
			string text = BpUnityPackageLoader.ReadPathnameText(pathname);
			if (text == null) continue;
			float[] st = MainTextureStFromMaterial(text);
			if (st == null) continue;
			byGuid[guid] = st;
		}
		return UniquePrefixMap(byGuid, (a, b) =>
			FloatBits(a[0]) == FloatBits(b[0]) && FloatBits(a[1]) == FloatBits(b[1]) &&
			FloatBits(a[2]) == FloatBits(b[2]) && FloatBits(a[3]) == FloatBits(b[3]));
	}

	// Rust build_unique_prefix_map：前缀冲突（值不等）则整个前缀丢弃
	private static Dictionary<string, T> UniquePrefixMap<T>(Dictionary<string, T> entries, Func<T, T, bool> equals)
	{
		Dictionary<string, T> byPrefix = new Dictionary<string, T>();
		HashSet<string> ambiguous = new HashSet<string>();

		foreach (KeyValuePair<string, T> kv in entries)
		{
			string prefix = GuidPrefix(kv.Key);
			if (ambiguous.Contains(prefix)) continue;
			T existing;
			if (byPrefix.TryGetValue(prefix, out existing))
			{
				if (equals(existing, kv.Value)) continue;
				ambiguous.Add(prefix);
				byPrefix.Remove(prefix);
				continue;
			}
			byPrefix[prefix] = kv.Value;
		}

		return byPrefix;
	}

	private static Dictionary<string, string> RuntimeShaderNamesByGuid()
	{
		if (s_shaderNamesByGuid == null) s_shaderNamesByGuid = BuildRuntimeShaderNamesByGuid();
		return s_shaderNamesByGuid;
	}

	private static Dictionary<string, string> BuildRuntimeShaderNamesByGuid()
	{
		Dictionary<string, string> shaderNames = new Dictionary<string, string>();
		foreach (string pathname in BpUnityPackageLoader.ListPathnames("Assets/", ".shader"))
		{
			string guid = BpUnityPackageLoader.GuidForPathname(pathname);
			if (guid == null) continue;
			string text = BpUnityPackageLoader.ReadPathnameText(pathname);
			if (text == null) continue;
			string shaderName = ShaderNameFromShaderAsset(text);
			if (shaderName == null) continue;
			shaderNames[guid] = shaderName;
		}
		return shaderNames;
	}

	// ---------------- 路径/文本解析 ----------------

	private static string TextureNameFromAssetPathname(string pathname)
	{
		if (!IsTexturePathname(pathname)) return null;
		return System.IO.Path.GetFileName(pathname);
	}

	private static bool IsTexturePathname(string pathname)
	{
		bool isTextureDir = pathname.StartsWith("Assets/Texture2D/", StringComparison.Ordinal)
			|| pathname.StartsWith("Assets/Resources/", StringComparison.Ordinal);
		if (!isTextureDir) return false;

		string ext = System.IO.Path.GetExtension(pathname);
		if (string.IsNullOrEmpty(ext)) return false;
		ext = ext.TrimStart('.').ToLowerInvariant();
		return ext == "png" || ext == "jpg" || ext == "jpeg";
	}

	private static bool IsMaterialPathname(string pathname)
	{
		if (!pathname.StartsWith("Assets/", StringComparison.Ordinal)) return false;
		string ext = System.IO.Path.GetExtension(pathname);
		if (string.IsNullOrEmpty(ext)) return false;
		return string.Equals(ext.TrimStart('.'), "mat", StringComparison.OrdinalIgnoreCase);
	}

	private static string MainTextureGuidFromMaterial(string text)
	{
		foreach (string raw in text.Split('\n'))
		{
			string trimmed = raw.Trim();
			if (trimmed.StartsWith("m_Texture:", StringComparison.Ordinal))
				return ExtractGuid(trimmed);
		}
		return null;
	}

	private static byte[] MainColorFromMaterial(string text)
	{
		foreach (string raw in text.Split('\n'))
		{
			string trimmed = raw.Trim();
			if (trimmed.StartsWith("- _Color:", StringComparison.Ordinal))
				return ParseRgbaColor(trimmed);
		}
		return null;
	}

	private static float? CutoffFromMaterial(string text)
	{
		foreach (string raw in text.Split('\n'))
		{
			string trimmed = raw.Trim();
			if (trimmed.StartsWith("- _Cutoff:", StringComparison.Ordinal))
				return ParseScalarValue(trimmed);
		}
		return null;
	}

	private static float[] MainTextureStFromMaterial(string text)
	{
		bool inMainTex = false;
		float[] scale = null;
		float[] offset = null;

		foreach (string raw in text.Split('\n'))
		{
			string trimmed = raw.Trim();
			if (trimmed == "- _MainTex:") { inMainTex = true; continue; }
			if (!inMainTex) continue;
			if (trimmed.StartsWith("- ", StringComparison.Ordinal)) break;
			if (trimmed.StartsWith("m_Scale:", StringComparison.Ordinal)) { scale = ParseVec2Like(trimmed); continue; }
			if (trimmed.StartsWith("m_Offset:", StringComparison.Ordinal)) { offset = ParseVec2Like(trimmed); continue; }
		}

		if (scale == null || offset == null) return null;
		return new float[] { scale[0], scale[1], offset[0], offset[1] };
	}

	private static int? CustomRenderQueueFromMaterial(string text)
	{
		const string prefix = "m_CustomRenderQueue:";
		foreach (string raw in text.Split('\n'))
		{
			string trimmed = raw.Trim();
			if (!trimmed.StartsWith(prefix, StringComparison.Ordinal)) continue;
			int value;
			if (int.TryParse(trimmed.Substring(prefix.Length).Trim(), out value) && value >= 0)
				return value;
		}
		return null;
	}

	private static bool MaterialUsesAlphaBlendShader(string text, Dictionary<string, string> shaderNames)
	{
		string name = MaterialShaderName(text, shaderNames);
		return name != null && ShaderNameUsesAlphaBlend(name);
	}

	private static bool MaterialUsesAlpha8BitShader(string text, Dictionary<string, string> shaderNames)
	{
		string name = MaterialShaderName(text, shaderNames);
		return name != null && ShaderNameUsesAlpha8Bit(name);
	}

	private static BpMaterialShaderKind? MaterialShaderKindOf(string text, Dictionary<string, string> shaderNames)
	{
		string shaderName = MaterialShaderName(text, shaderNames);
		if (shaderName == null) return null;
		switch (shaderName)
		{
			case "_Custom/Unlit_Monochrome": return BpMaterialShaderKind.CustomUnlitMonochrome;
			case "_Custom/Unlit_Color_Geometry": return BpMaterialShaderKind.CustomUnlitColorGeometry;
			case "_Custom/Unlit_ColorTransparent_Geometry": return BpMaterialShaderKind.CustomUnlitColorTransparentGeometry;
			case "_Custom/Unlit_Alpha8Bit_Color": return BpMaterialShaderKind.CustomUnlitAlpha8BitColor;
			case "Unlit/Transparent": return BpMaterialShaderKind.BuiltinUnlitTransparent;
			case "Unlit/Transparent Cutout": return BpMaterialShaderKind.BuiltinUnlitTransparentCutout;
		}
		return null;
	}

	private static string MaterialShaderName(string text, Dictionary<string, string> shaderNames)
	{
		string line = null;
		foreach (string raw in text.Split('\n'))
		{
			if (raw.Trim().StartsWith("m_Shader:", StringComparison.Ordinal)) { line = raw; break; }
		}
		if (line == null) return null;
		string trimmed = line.Trim();

		string guid = ExtractGuid(trimmed);
		if (guid != null)
		{
			string shaderName;
			if (shaderNames.TryGetValue(guid, out shaderName)) return shaderName;
			string fallback = FallbackShaderNameForGuid(guid);
			if (fallback != null) return fallback;
		}

		string fileId = ExtractFileId(trimmed);
		if (fileId == null) return null;
		return BuiltinShaderName(fileId);
	}

	// 部分内置快照保留材质着色器 guid 但缺对应 shader 资源
	private static string FallbackShaderNameForGuid(string guid)
	{
		switch (guid)
		{
			case "3ee1baa860fa20a74fa3b12a3f0bc258": return "_Custom/Unlit_Monochrome";
			case "f2bf0be753c6f2a3466cb9069962a880": return "_Custom/Unlit_ColorTransparent_Geometry";
		}
		return null;
	}

	private static string ExtractGuid(string line)
	{
		const string field = "guid: ";
		int start = line.IndexOf(field, StringComparison.Ordinal);
		if (start < 0) return null;
		start += field.Length;
		int end = start;
		while (end < line.Length && IsAsciiHexDigit(line[end])) end++;
		if (end == start) return null;
		return line.Substring(start, end - start);
	}

	private static string ExtractFileId(string line)
	{
		const string field = "fileID: ";
		int start = line.IndexOf(field, StringComparison.Ordinal);
		if (start < 0) return null;
		start += field.Length;
		int end = start;
		while (end < line.Length && line[end] >= '0' && line[end] <= '9') end++;
		if (end == start) return null;
		return line.Substring(start, end - start);
	}

	private static bool IsAsciiHexDigit(char c)
	{
		return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
	}

	private static byte[] ParseRgbaColor(string line)
	{
		int open = line.IndexOf('{');
		if (open < 0) return null;
		int close = line.LastIndexOf('}');
		if (close < 0) return null;

		byte[] rgba = new byte[4];
		bool[] seen = new bool[4];
		string body = line.Substring(open + 1, close - open - 1);

		foreach (string rawPart in body.Split(','))
		{
			string part = rawPart.Trim();
			int colon = part.IndexOf(':');
			if (colon < 0) return null;
			string channel = part.Substring(0, colon).Trim();
			string valueText = part.Substring(colon + 1).Trim();

			int index;
			switch (channel)
			{
				case "r": index = 0; break;
				case "g": index = 1; break;
				case "b": index = 2; break;
				case "a": index = 3; break;
				default: continue;
			}
			float value;
			if (!float.TryParse(valueText, System.Globalization.NumberStyles.Float,
				System.Globalization.CultureInfo.InvariantCulture, out value)) return null;
			// Rust: (value.clamp(0.0,1.0) * 255.0) as u8
			float clamped = value < 0f ? 0f : (value > 1f ? 1f : value);
			rgba[index] = (byte)(clamped * 255f);
			seen[index] = true;
		}

		for (int i = 0; i < 4; i++) if (!seen[i]) return null;
		return rgba;
	}

	private static float? ParseScalarValue(string line)
	{
		int colon = line.IndexOf(':');
		if (colon < 0) return null;
		float value;
		if (!float.TryParse(line.Substring(colon + 1).Trim(), System.Globalization.NumberStyles.Float,
			System.Globalization.CultureInfo.InvariantCulture, out value)) return null;
		return value;
	}

	private static float[] ParseVec2Like(string line)
	{
		int open = line.IndexOf('{');
		if (open < 0) return null;
		int close = line.LastIndexOf('}');
		if (close < 0) return null;

		float[] values = new float[2];
		bool[] seen = new bool[2];
		string body = line.Substring(open + 1, close - open - 1);

		foreach (string rawPart in body.Split(','))
		{
			string part = rawPart.Trim();
			int colon = part.IndexOf(':');
			if (colon < 0) return null;
			string axis = part.Substring(0, colon).Trim();
			string valueText = part.Substring(colon + 1).Trim();

			int index;
			switch (axis)
			{
				case "x": index = 0; break;
				case "y": index = 1; break;
				default: continue;
			}
			float value;
			if (!float.TryParse(valueText, System.Globalization.NumberStyles.Float,
				System.Globalization.CultureInfo.InvariantCulture, out value)) return null;
			values[index] = value;
			seen[index] = true;
		}

		for (int i = 0; i < 2; i++) if (!seen[i]) return null;
		return values;
	}

	private static string ShaderNameFromShaderAsset(string text)
	{
		foreach (string line in text.Split('\n'))
		{
			if (!line.TrimStart().StartsWith("Shader \"", StringComparison.Ordinal)) continue;
			int start = line.IndexOf('"');
			if (start < 0) continue;
			int end = line.IndexOf('"', start + 1);
			if (end < 0) continue;
			return line.Substring(start + 1, end - start - 1);
		}
		return null;
	}

	private static string BuiltinShaderName(string fileId)
	{
		switch (fileId)
		{
			case "10750": return "Unlit/Transparent Cutout";
			case "10752": return "Unlit/Transparent";
		}
		return null;
	}

	private static bool ShaderNameUsesAlphaBlend(string shaderName)
	{
		return shaderName == "Unlit/Transparent" || shaderName.Contains("ColorTransparent");
	}

	private static bool ShaderNameUsesAlpha8Bit(string shaderName)
	{
		return shaderName.Contains("Alpha8Bit");
	}

	private static string GuidPrefix(string guid)
	{
		if (guid == null) return "";
		return guid.Length > 8 ? guid.Substring(0, 8) : guid;
	}

	private static int FloatBits(float v)
	{
		return BitConverter.ToInt32(BitConverter.GetBytes(v), 0);
	}

	// ---------------- 对外 API（对应 refs.rs 的 pub(crate) fn） ----------------

	public static string TextureNameForGuid(string guid) { return RuntimeTextureNameForGuid(guid); }
	public static string MaterialTextureNameForGuid(string guid) { return RuntimeMaterialTextureNameForGuid(guid); }
	public static string MaterialTextureNameForGuidPrefix(string guid) { return RuntimeMaterialTextureNameForGuidPrefix(guid); }

	public static byte[] MaterialColorRgbaForGuid(string guid) { return RuntimeMaterialColorRgbaForGuid(guid); }
	public static byte[] MaterialColorRgbaForGuidPrefix(string guid) { return RuntimeMaterialColorRgbaForGuidPrefix(guid); }

	public static byte[] MaterialColorForGuid(string guid)
	{
		byte[] rgba = RuntimeMaterialColorRgbaForGuid(guid);
		return rgba == null ? null : new byte[] { rgba[0], rgba[1], rgba[2] };
	}

	public static byte[] MaterialColorForGuidPrefix(string guid)
	{
		byte[] rgba = RuntimeMaterialColorRgbaForGuidPrefix(guid);
		return rgba == null ? null : new byte[] { rgba[0], rgba[1], rgba[2] };
	}

	public static bool MaterialAlphaBlendForGuid(string guid) { return RuntimeMaterialAlphaBlendForGuid(guid); }
	public static bool MaterialAlphaBlendForGuidPrefix(string guid) { return RuntimeMaterialAlphaBlendForGuidPrefix(guid); }
	public static bool MaterialAlpha8BitForGuid(string guid) { return RuntimeMaterialAlpha8BitForGuid(guid); }
	public static bool MaterialAlpha8BitForGuidPrefix(string guid) { return RuntimeMaterialAlpha8BitForGuidPrefix(guid); }

	public static float? MaterialCutoffForGuid(string guid) { return RuntimeMaterialCutoffForGuid(guid); }
	public static float? MaterialCutoffForGuidPrefix(string guid) { return RuntimeMaterialCutoffForGuidPrefix(guid); }

	public static int? MaterialCustomRenderQueueForGuid(string guid) { return RuntimeMaterialCustomRenderQueueForGuid(guid); }
	public static int? MaterialCustomRenderQueueForGuidPrefix(string guid) { return RuntimeMaterialCustomRenderQueueForGuidPrefix(guid); }

	public static BpMaterialShaderKind? MaterialShaderKindForGuid(string guid) { return RuntimeMaterialShaderKindForGuid(guid); }
	public static BpMaterialShaderKind? MaterialShaderKindForGuidPrefix(string guid) { return RuntimeMaterialShaderKindForGuidPrefix(guid); }

	public static float[] MaterialMainTexStForGuid(string guid) { return RuntimeMaterialMainTexStForGuid(guid); }
	public static float[] MaterialMainTexStForGuidPrefix(string guid) { return RuntimeMaterialMainTexStForGuidPrefix(guid); }
}
