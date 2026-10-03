using System;
using System.Collections.Generic;
using UnityEngine;

// 1:1 移植 badpiggies-editor 的 data/bg_data/{types,tables,parse}.rs。

// ---------------- types.rs ----------------

// ParallaxLayer（含视差速度与渲染顺序）
public enum BpBgLayer
{
	Sky,
	Camera,
	Further,
	Far,
	Near,
	Ground,
	Foreground
}

public static class BpBgLayerExt
{
	public static float ParallaxSpeed(this BpBgLayer layer)
	{
		switch (layer)
		{
			case BpBgLayer.Sky: return 0.8f;
			case BpBgLayer.Camera: return 1.0f;
			case BpBgLayer.Further: return 0.7f;
			case BpBgLayer.Far: return 0.6f;
			case BpBgLayer.Near: return 0.4f;
			case BpBgLayer.Ground: return 0.0f;
			case BpBgLayer.Foreground: return -0.4f;
		}
		return 0.0f;
	}

	// 渲染顺序（越小越先画 = 越靠后）
	public static int Order(this BpBgLayer layer)
	{
		switch (layer)
		{
			case BpBgLayer.Sky: return 0;
			case BpBgLayer.Camera: return 1;
			case BpBgLayer.Further: return 2;
			case BpBgLayer.Far: return 3;
			case BpBgLayer.Near: return 4;
			case BpBgLayer.Ground: return 5;
			case BpBgLayer.Foreground: return 20;
		}
		return 0;
	}
}

// 待渲染的背景精灵
public class BpBgSprite
{
	public string name = "";
	public string atlas;                 // null = 无
	public byte[] fillColor;             // len 3，null = 无
	public string skyTexture;            // null = 无
	public float uvX;
	public float uvY;
	public float gridW;
	public float gridH;
	public float spriteW;
	public float spriteH;
	public float subdiv;
	public float border;
	public float worldX;
	public float worldY;
	public float worldZ;
	public float scaleX;
	public float scaleY;
	public BpBgLayer layer;
	public float localX;
	public float localY;
	public string parentGroup = "";
	public float[] tint = { 1f, 1f, 1f, 1f };
	public BpMaterialShaderKind shaderKind;
	public float[] mainTexSt = { 1f, 1f, 0f, 0f };
	public int? customRenderQueue;
	public bool alphaBlend;
	// 材质 _Cutoff 阈值（显式渲染路径现在依据 shader_kind；alpha_blend 留在数据模型用于背景测试/审计）
	public float cutoff;

	public BpBgSprite Clone()
	{
		BpBgSprite c = (BpBgSprite)MemberwiseClone();
		if (fillColor != null) c.fillColor = (byte[])fillColor.Clone();
		if (tint != null) c.tint = (float[])tint.Clone();
		if (mainTexSt != null) c.mainTexSt = (float[])mainTexSt.Clone();
		return c;
	}
}

// 完整主题数据（精灵按层组织）
public class BpBgTheme
{
	public List<BpBgSprite> sprites = new List<BpBgSprite>();
	public Dictionary<string, float[]> groupDefaults = new Dictionary<string, float[]>();
	// PositionSerializer 子节点 root-order → 组名映射（EP6 背景 prefab 用）
	public List<string> childOrder = new List<string>();
}

// 已解析的 BG overrides：组/精灵名 → 部分位置覆盖
public class BpBgOverrides
{
	public Dictionary<string, float[]> groups = new Dictionary<string, float[]>();   // 每项 3 元素，NaN = 未设置
	public Dictionary<string, float[]> sprites = new Dictionary<string, float[]>();
}

// ---------------- tables.rs ----------------

public static class BpBgTables
{
	public const string BG_SPRITE_SCRIPT_GUID = "b011dfa16a4475b746a1372ea41fdf05";
	public const string BG_TEXTURELOADER_ASSET = "Assets/Resources/prefabs/textureloader.prefab";

	private static List<string> s_bgAtlasFiles;
	private static List<string> s_skyTextureFiles;

	public static void Reset()
	{
		s_bgAtlasFiles = null;
		s_skyTextureFiles = null;
	}

	public static BpBgLayer? ExplicitParallaxLayer(string tag)
	{
		switch (tag)
		{
			case "ParallaxLayerSky": return BpBgLayer.Sky;
			case "ParallaxLayerFixedFollowCamera": return BpBgLayer.Camera;
			case "ParallaxLayerFurther": return BpBgLayer.Further;
			case "ParallaxLayerFar": return BpBgLayer.Far;
			case "ParallaxLayerNear": return BpBgLayer.Near;
			case "ParallaxLayerForeground": return BpBgLayer.Foreground;
		}
		return null;
	}

	public static BpBgLayer ClassifyGroupLayer(string tag)
	{
		BpBgLayer? layer = ExplicitParallaxLayer(tag);
		if (layer.HasValue) return layer.Value;
		return BpBgLayer.Ground;   // 含 "Ground" 及其他
	}

	// 所有已知背景图集文件名
	public static List<string> BgAtlasFiles()
	{
		if (s_bgAtlasFiles == null)
		{
			List<string> files = new List<string>();
			foreach (string name in Texture2dPngFilenames())
				if (name.StartsWith("Background_", StringComparison.Ordinal) && name.Contains("_Sheet_"))
					files.Add(name);
			s_bgAtlasFiles = files;
		}
		return s_bgAtlasFiles;
	}

	// 所有已知天空贴图文件名
	public static List<string> SkyTextureFiles()
	{
		if (s_skyTextureFiles == null)
		{
			List<string> files = new List<string>();
			foreach (string name in Texture2dPngFilenames())
			{
				string lower = name.ToLowerInvariant();
				if (lower.EndsWith("_sky_texture.png", StringComparison.Ordinal) || lower.EndsWith("_sky.png", StringComparison.Ordinal))
					files.Add(name);
			}
			s_skyTextureFiles = files;
		}
		return s_skyTextureFiles;
	}

	private static List<string> Texture2dPngFilenames()
	{
		List<string> filenames = new List<string>();
		foreach (string path in BpUnityPackageLoader.ListPathnames("Assets/Texture2D/", ".png"))
		{
			string name;
			try { name = System.IO.Path.GetFileName(path); }
			catch (Exception) { continue; }
			if (!string.IsNullOrEmpty(name)) filenames.Add(name);
		}
		filenames.Sort(StringComparer.Ordinal);
		// dedup
		List<string> deduped = new List<string>(filenames.Count);
		string prev = null;
		foreach (string f in filenames)
		{
			if (prev != null && f == prev) continue;
			deduped.Add(f);
			prev = f;
		}
		return deduped;
	}
}

// ---------------- parse.rs ----------------

public class BpBgGameObjectInfo
{
	public string name = "";
	public string tag = "";
	public bool active = true;
}

public class BpBgTransformInfo
{
	public string gameObjectId = "";
	public float[] localPos = { 0f, 0f, 0f };
	public float[] localScale = { 1f, 1f, 1f };
	public string parentId;   // null = 无父
	public List<string> children = new List<string>();
	public int rootOrder;
}

public class BpBgSpriteComponent
{
	public float spriteWidth;
	public float spriteHeight;
	public float uvX;
	public float uvY;
	public float width;
	public float height;
	public float subdiv;
	public float border;
}

public class BpBgParsedPrefab
{
	public string rootTransformId = "";
	public Dictionary<string, BpBgGameObjectInfo> gameObjects = new Dictionary<string, BpBgGameObjectInfo>();
	public Dictionary<string, BpBgTransformInfo> transforms = new Dictionary<string, BpBgTransformInfo>();
	public Dictionary<string, string> renderers = new Dictionary<string, string>();   // gameObjectId → materialGuid
	public Dictionary<string, BpBgSpriteComponent> sprites = new Dictionary<string, BpBgSpriteComponent>();
}

public static class BpBgPrefabParse
{
	public static string ReadEmbeddedText(string path)
	{
		byte[] bytes = BpUnityPackageLoader.ReadPathname(path);
		return bytes == null ? null : System.Text.Encoding.UTF8.GetString(bytes);
	}

	private static string FieldValue(string doc, string prefix)
	{
		foreach (string line in doc.Split('\n'))
		{
			string trimmed = line.Trim();
			if (trimmed.StartsWith(prefix, StringComparison.Ordinal))
				return trimmed.Substring(prefix.Length).Trim();
		}
		return null;
	}

	private static bool TryParseDocHeader(string header, out int classId, out string fileId)
	{
		classId = 0;
		fileId = null;
		string h = header.Trim();
		const string tag = "!u!";
		if (!h.StartsWith(tag, StringComparison.Ordinal)) return false;
		string rest = h.Substring(tag.Length);
		int sep = rest.IndexOf(" &", StringComparison.Ordinal);
		if (sep < 0) return false;
		if (!int.TryParse(rest.Substring(0, sep), out classId)) return false;
		fileId = rest.Substring(sep + 2).Trim();
		return true;
	}

	private static string ExtractFileId(string value)
	{
		const string field = "fileID: ";
		int start = value.IndexOf(field, StringComparison.Ordinal);
		if (start < 0) return null;
		start += field.Length;
		int end = start;
		while (end < value.Length && value[end] != ',' && value[end] != '}') end++;
		return value.Substring(start, end - start).Trim();
	}

	private static string ExtractGuid(string value)
	{
		const string field = "guid: ";
		int start = value.IndexOf(field, StringComparison.Ordinal);
		if (start < 0) return null;
		start += field.Length;
		int end = start;
		while (end < value.Length && value[end] != ',' && value[end] != '}') end++;
		return value.Substring(start, end - start).Trim();
	}

	private static float[] ParseVec3(string value)
	{
		float[] outArr = { 0f, 0f, 0f };
		string trimmed = value.Trim().TrimStart('{').TrimEnd('}');
		bool[] seen = { false, false, false };
		foreach (string rawPart in trimmed.Split(','))
		{
			string part = rawPart.Trim();
			int colon = part.IndexOf(':');
			if (colon < 0) return null;
			string axis = part.Substring(0, colon).Trim();
			string raw = part.Substring(colon + 1).Trim();
			int index;
			switch (axis)
			{
				case "x": index = 0; break;
				case "y": index = 1; break;
				case "z": index = 2; break;
				default: continue;
			}
			float v;
			if (!float.TryParse(raw, System.Globalization.NumberStyles.Float,
				System.Globalization.CultureInfo.InvariantCulture, out v)) return null;
			outArr[index] = v;
			seen[index] = true;
		}
		for (int i = 0; i < 3; i++) if (!seen[i]) return null;
		return outArr;
	}

	private static List<string> ParseChildren(string doc)
	{
		List<string> children = new List<string>();
		bool inChildren = false;
		foreach (string line in doc.Split('\n'))
		{
			string trimmed = line.Trim();
			if (trimmed.StartsWith("m_Children:", StringComparison.Ordinal))
			{
				inChildren = true;
				continue;
			}
			if (!inChildren) continue;
			if (trimmed.StartsWith("- ", StringComparison.Ordinal))
			{
				string childId = ExtractFileId(trimmed.Substring(2));
				if (childId != null) children.Add(childId);
				continue;
			}
			if (trimmed.StartsWith("m_Father:", StringComparison.Ordinal)) break;
			if (trimmed.Length != 0) break;
		}
		return children;
	}

	public static string GuidPrefix(string guid)
	{
		if (guid == null) return "";
		return guid.Length > 8 ? guid.Substring(0, 8) : guid;
	}

	private static bool IsEnabled(string doc)
	{
		return FieldValue(doc, "m_Enabled: ") != "0";
	}

	public static BpBgParsedPrefab ParsePrefab(string raw)
	{
		string rootGameObjectId = null;
		Dictionary<string, BpBgGameObjectInfo> gameObjects = new Dictionary<string, BpBgGameObjectInfo>();
		Dictionary<string, BpBgTransformInfo> transforms = new Dictionary<string, BpBgTransformInfo>();
		Dictionary<string, string> renderers = new Dictionary<string, string>();
		Dictionary<string, BpBgSpriteComponent> sprites = new Dictionary<string, BpBgSpriteComponent>();

		foreach (string doc in BpAssets.SplitYamlDocs(raw))
		{
			string[] lines = doc.Split('\n');
			if (lines.Length == 0) continue;
			int classId;
			string fileId;
			if (!TryParseDocHeader(lines[0], out classId, out fileId)) continue;

			if (classId == 1001)
			{
				string rootGo = FieldValue(doc, "m_RootGameObject: ");
				rootGameObjectId = rootGo != null ? ExtractFileId(rootGo) : null;
			}
			else if (classId == 1)
			{
				BpBgGameObjectInfo go = new BpBgGameObjectInfo();
				go.name = FieldValue(doc, "m_Name: ") ?? fileId;
				go.tag = FieldValue(doc, "m_TagString: ") ?? "";
				go.active = FieldValue(doc, "m_IsActive: ") != "0";
				gameObjects[fileId] = go;
			}
			else if (classId == 4)
			{
				string go = FieldValue(doc, "m_GameObject: ");
				if (go == null) continue;
				string gameObjectId = ExtractFileId(go);
				if (gameObjectId == null) continue;

				BpBgTransformInfo t = new BpBgTransformInfo();
				t.gameObjectId = gameObjectId;

				string lp = FieldValue(doc, "m_LocalPosition: ");
				t.localPos = (lp != null ? ParseVec3(lp) : null) ?? new float[] { 0f, 0f, 0f };

				string ls = FieldValue(doc, "m_LocalScale: ");
				t.localScale = (ls != null ? ParseVec3(ls) : null) ?? new float[] { 1f, 1f, 1f };

				string father = FieldValue(doc, "m_Father: ");
				string parentId = father != null ? ExtractFileId(father) : null;
				if (parentId == "0") parentId = null;
				t.parentId = parentId;

				string rootOrder = FieldValue(doc, "m_RootOrder: ");
				int parsedOrder = 0;
				if (rootOrder != null) int.TryParse(rootOrder, out parsedOrder);
				t.rootOrder = parsedOrder;

				t.children = ParseChildren(doc);
				transforms[fileId] = t;
			}
			else if (classId == 23)
			{
				if (!IsEnabled(doc)) continue;
				string go = FieldValue(doc, "m_GameObject: ");
				if (go == null) continue;
				string gameObjectId = ExtractFileId(go);
				if (gameObjectId == null) continue;

				string materialGuid = null;
				foreach (string line in doc.Split('\n'))
				{
					string trimmed = line.Trim();
					if (!trimmed.StartsWith("- ", StringComparison.Ordinal)) continue;
					materialGuid = ExtractGuid(trimmed.Substring(2));
					if (materialGuid != null) break;
				}
				if (materialGuid == null) continue;
				renderers[gameObjectId] = materialGuid;
			}
			else if (classId == 114)
			{
				if (!IsEnabled(doc)) continue;
				string scriptField = FieldValue(doc, "m_Script: ");
				string scriptGuid = scriptField != null ? ExtractGuid(scriptField) : null;
				if (scriptGuid == null || scriptGuid != BpBgTables.BG_SPRITE_SCRIPT_GUID) continue;

				string go = FieldValue(doc, "m_GameObject: ");
				if (go == null) continue;
				string gameObjectId = ExtractFileId(go);
				if (gameObjectId == null) continue;

				// m_textureWidth / m_textureHeight 必须存在
				if (ParseFloatField(doc, "m_textureWidth: ") == null) continue;
				if (ParseFloatField(doc, "m_textureHeight: ") == null) continue;

				float? spriteWidth = ParseFloatField(doc, "m_spriteWidth: ");
				float? spriteHeight = ParseFloatField(doc, "m_spriteHeight: ");
				float? uvX = ParseFloatField(doc, "m_UVx: ");
				float? uvY = ParseFloatField(doc, "m_UVy: ");
				float? width = ParseFloatField(doc, "m_width: ");
				float? height = ParseFloatField(doc, "m_height: ");
				float? subdiv = ParseFloatField(doc, "m_atlasGridSubdivisions: ");
				if (!spriteWidth.HasValue || !spriteHeight.HasValue || !uvX.HasValue || !uvY.HasValue
					|| !width.HasValue || !height.HasValue || !subdiv.HasValue) continue;
				float border = ParseFloatField(doc, "m_border: ") ?? 0f;

				BpBgSpriteComponent sc = new BpBgSpriteComponent();
				sc.spriteWidth = spriteWidth.Value;
				sc.spriteHeight = spriteHeight.Value;
				sc.uvX = uvX.Value;
				sc.uvY = uvY.Value;
				sc.width = width.Value;
				sc.height = height.Value;
				sc.subdiv = subdiv.Value;
				sc.border = border;
				sprites[gameObjectId] = sc;
			}
		}

		// 根 transform 解析
		string rootTransformId = null;
		if (rootGameObjectId != null)
		{
			foreach (KeyValuePair<string, BpBgTransformInfo> kv in transforms)
			{
				if (kv.Value.gameObjectId == rootGameObjectId) { rootTransformId = kv.Key; break; }
			}
		}
		if (rootTransformId == null)
		{
			foreach (KeyValuePair<string, BpBgTransformInfo> kv in transforms)
			{
				if (kv.Value.parentId == null) { rootTransformId = kv.Key; break; }
			}
		}
		if (rootTransformId == null) return null;

		BpBgParsedPrefab parsed = new BpBgParsedPrefab();
		parsed.rootTransformId = rootTransformId;
		parsed.gameObjects = gameObjects;
		parsed.transforms = transforms;
		parsed.renderers = renderers;
		parsed.sprites = sprites;
		return parsed;
	}

	private static float? ParseFloatField(string doc, string key)
	{
		string value = FieldValue(doc, key);
		if (value == null) return null;
		float v;
		return float.TryParse(value, System.Globalization.NumberStyles.Float,
			System.Globalization.CultureInfo.InvariantCulture, out v) ? (float?)v : null;
	}

	public static Dictionary<string, string> LoadTextureloaderMaterials()
	{
		Dictionary<string, string> map = new Dictionary<string, string>();
		string raw = ReadEmbeddedText(BpBgTables.BG_TEXTURELOADER_ASSET);
		if (raw == null)
		{
			Debug.LogError("[BPEditor] Missing embedded background textureloader asset");
			return map;
		}

		string currentGuid = null;
		foreach (string line in raw.Split('\n'))
		{
			string trimmed = line.Trim();
			if (trimmed.StartsWith("- material:", StringComparison.Ordinal))
			{
				currentGuid = ExtractGuid(trimmed);
				continue;
			}
			const string assetPrefix = "assetName: ";
			if (trimmed.StartsWith(assetPrefix, StringComparison.Ordinal))
			{
				if (currentGuid != null)
				{
					map[currentGuid] = trimmed.Substring(assetPrefix.Length).Trim();
					currentGuid = null;
				}
			}
		}
		return map;
	}

	public static string AssetFilename(string assetName)
	{
		return assetName + ".png";
	}

	public static bool IsSkyTextureAsset(string assetName)
	{
		return assetName != null && (assetName.Contains("Sky_Texture") || assetName.Contains("Backgrounds_sky"));
	}
}
