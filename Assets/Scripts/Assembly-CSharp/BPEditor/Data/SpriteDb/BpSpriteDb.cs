using System;
using System.Collections.Generic;
using UnityEngine;

// 1:1 移植 badpiggies-editor 的 data/sprite_db/{mod,types,parse,runtime}.rs。
// 精灵图集 UV/尺寸数据由内置 Unity 原始资源重建：
//   - Assets/Prefab/*.prefab 遍历
//   - Assets/Resources/guisystem/sprites.bytes + spritemapping.bytes
// 世界尺寸公式：pixelSize * prefabScale * WORLD_SCALE（= 10/768）。

public struct BpUvRect
{
	public float x;
	public float y;
	public float w;
	public float h;
}

// types.rs: SpriteInfo
public class BpSpriteInfo
{
	public string atlas = "";
	public BpUvRect uv;
	public float worldW;
	public float worldH;
}

// types.rs: RuntimeSpriteMeta（pub(super)）
public class BpRuntimeSpriteMeta
{
	public string materialId = "";
	public float width;
	public float height;
	public BpUvRect uv;
	public float pivotX;
	public float pivotY;
}

// types.rs: GameObjectInfo
public class BpGameObjectInfo
{
	public bool active;
	public string name = "";
}

// types.rs: TransformInfo
public class BpTransformInfo
{
	public string gameObjectId = "";
	public string father = "0";
	public List<string> children = new List<string>();
}

// types.rs: RendererInfo
public class BpRendererInfo
{
	public string materialGuid = "";
	public bool enabled;
}

// types.rs: RuntimeSpriteComponent
public class BpRuntimeSpriteComponent
{
	public string gameObjectId = "";
	public string spriteId = "";
	public float scaleX;
	public float scaleY;
}

// types.rs: UnmanagedSpriteComponent
public class BpSpriteDbUnmanagedComponent
{
	public BpUvRect uv;
	public float worldW;
	public float worldH;
}

// types.rs: ParsedPrefab
public class BpParsedPrefab
{
	public Dictionary<string, BpGameObjectInfo> gameObjects = new Dictionary<string, BpGameObjectInfo>();
	public Dictionary<string, BpTransformInfo> transforms = new Dictionary<string, BpTransformInfo>();
	public Dictionary<string, BpRendererInfo> renderers = new Dictionary<string, BpRendererInfo>();
	public List<BpRuntimeSpriteComponent> runtimeSprites = new List<BpRuntimeSpriteComponent>();
	public Dictionary<string, BpSpriteDbUnmanagedComponent> unmanagedSprites = new Dictionary<string, BpSpriteDbUnmanagedComponent>();
}

public static class BpSpriteDb
{
	public const float WORLD_SCALE = 10.0f / 768.0f;

	private const string PREFAB_DIR_ASSET = "Assets/Prefab/";
	private const string SPRITES_BYTES_ASSET = "Assets/Resources/guisystem/sprites.bytes";
	private const string SPRITE_MAPPING_ASSET = "Assets/Resources/guisystem/spritemapping.bytes";

	public const string UNMANAGED_ATLAS = "Props_Generic_Sheet_01.png";

	private static Dictionary<string, BpSpriteInfo> s_db;
	private static Dictionary<string, BpRuntimeSpriteMeta> s_runtimeSprites;

	public static void Reset()
	{
		s_db = null;
		s_runtimeSprites = null;
		BpSpriteDbBuilder.Reset();
	}

	public static Dictionary<string, BpRuntimeSpriteMeta> RuntimeSprites()
	{
		if (s_runtimeSprites == null) s_runtimeSprites = LoadRuntimeSprites();
		return s_runtimeSprites;
	}

	// sprite_db()
	public static Dictionary<string, BpSpriteInfo> Db()
	{
		if (s_db == null) s_db = BuildDb();
		return s_db;
	}

	// ---------------- 查询 ----------------

	// get_sprite_info(name) —— 含归一化回退
	public static BpSpriteInfo GetSpriteInfo(string name)
	{
		Dictionary<string, BpSpriteInfo> db = Db();
		if (name == null) return null;

		BpSpriteInfo s;
		if (db.TryGetValue(name, out s)) return s;

		// 去掉 " (N)" 重复后缀："Bottle (2)" → "Bottle"
		int paren = name.IndexOf(" (", StringComparison.Ordinal);
		if (paren > 0)
		{
			string baseName = name.Substring(0, paren);
			if (baseName != name && db.TryGetValue(baseName, out s)) return s;
		}

		// 去掉结尾数字："StarBox01" → "StarBox"
		string trimmed = name.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
		if (trimmed != name && trimmed.Length > 0 && db.TryGetValue(trimmed, out s)) return s;

		// 去掉 "_001" 形式后缀
		int underscore = name.LastIndexOf('_');
		if (underscore >= 0)
		{
			string suffix = name.Substring(underscore + 1);
			if (IsAllAsciiDigits(suffix) && suffix.Length > 0)
			{
				string baseName = name.Substring(0, underscore);
				if (db.TryGetValue(baseName, out s)) return s;
			}
		}

		// 常见运行时/prefab 别名："Bird_Black" → "Bird_Black_01"
		string suffixed = name + "_01";
		if (db.TryGetValue(suffixed, out s)) return s;

		return null;
	}

	public static bool RuntimeSpriteDimensions(string spriteId, out BpUvRect uv, out float width, out float height, out string materialId)
	{
		uv = default(BpUvRect);
		width = 0f;
		height = 0f;
		materialId = null;
		BpRuntimeSpriteMeta meta;
		if (!RuntimeSprites().TryGetValue(spriteId ?? "", out meta)) return false;
		uv = meta.uv;
		width = meta.width;
		height = meta.height;
		materialId = meta.materialId;
		return true;
	}

	public static bool RuntimeSpritePivot(string spriteId, out float pivotX, out float pivotY)
	{
		pivotX = 0f;
		pivotY = 0f;
		BpRuntimeSpriteMeta meta;
		if (!RuntimeSprites().TryGetValue(spriteId ?? "", out meta)) return false;
		pivotX = meta.pivotX;
		pivotY = meta.pivotY;
		return true;
	}

	// ---------------- 构建 ----------------

	private static Dictionary<string, BpSpriteInfo> BuildDb()
	{
		Dictionary<string, BpRuntimeSpriteMeta> runtimeSprites = RuntimeSprites();
		Dictionary<string, BpSpriteInfo> map = new Dictionary<string, BpSpriteInfo>();

		foreach (string assetPath in BpUnityPackageLoader.ListPathnames(PREFAB_DIR_ASSET, ".prefab"))
		{
			string filename = assetPath.StartsWith(PREFAB_DIR_ASSET, StringComparison.Ordinal)
				? assetPath.Substring(PREFAB_DIR_ASSET.Length)
				: assetPath;
			if (!filename.EndsWith(".prefab", StringComparison.Ordinal)) continue;
			string name = filename.Substring(0, filename.Length - ".prefab".Length);

			string text = ReadEmbeddedText(assetPath);
			if (text == null)
			{
				Debug.LogWarning("[BPEditor] Missing embedded prefab for sprite database: " + assetPath);
				continue;
			}

			BpParsedPrefab parsed = ParsePrefab(text);
			BpSpriteInfo info = BpSpriteDbBuilder.FindRuntimeSpriteInfo(parsed, runtimeSprites);
			if (info == null) info = BpSpriteDbBuilder.FindUnmanagedSpriteInfo(name, parsed);
			if (info != null) map[name] = info;
		}

		return map;
	}

	// read_embedded_text
	public static string ReadEmbeddedText(string path)
	{
		byte[] bytes = BpUnityPackageLoader.ReadPathname(path);
		return bytes == null ? null : System.Text.Encoding.UTF8.GetString(bytes);
	}

	// ---------------- runtime.rs ----------------

	private static Dictionary<string, BpRuntimeSpriteMeta> LoadRuntimeSprites()
	{
		Dictionary<string, BpRuntimeSpriteMeta> spriteData = new Dictionary<string, BpRuntimeSpriteMeta>();

		string text = ReadEmbeddedText(SPRITES_BYTES_ASSET);
		if (text == null)
		{
			Debug.LogError("[BPEditor] Failed to read embedded " + SPRITES_BYTES_ASSET + " for sprite database");
			return spriteData;
		}

		foreach (string rawLine in text.Split('\n'))
		{
			string[] fields = rawLine.Trim().Split('\t');
			if (fields.Length < 14) continue;

			float pivotX, pivotY, width, height;
			if (!TryParseFloat(fields[7], out pivotX)) continue;
			if (!TryParseFloat(fields[8], out pivotY)) continue;
			if (!TryParseFloat(fields[11], out width)) continue;
			if (!TryParseFloat(fields[12], out height)) continue;

			BpRuntimeSpriteMeta meta = new BpRuntimeSpriteMeta();
			meta.materialId = fields[2];
			meta.width = width;
			meta.height = height;
			meta.uv = default(BpUvRect);
			meta.pivotX = pivotX;
			meta.pivotY = pivotY;
			spriteData[fields[0]] = meta;
		}

		text = ReadEmbeddedText(SPRITE_MAPPING_ASSET);
		if (text == null)
		{
			Debug.LogError("[BPEditor] Failed to read embedded " + SPRITE_MAPPING_ASSET + " for sprite database");
			return spriteData;
		}

		foreach (string rawLine in text.Split('\n'))
		{
			string[] fields = rawLine.Trim().Split('\t');
			if (fields.Length < 5) continue;

			BpRuntimeSpriteMeta entry;
			if (!spriteData.TryGetValue(fields[0], out entry)) continue;

			float x, y, w, h;
			if (!TryParseFloat(fields[1], out x)) continue;
			if (!TryParseFloat(fields[2], out y)) continue;
			if (!TryParseFloat(fields[3], out w)) continue;
			if (!TryParseFloat(fields[4], out h)) continue;

			BpUvRect uv = new BpUvRect();
			uv.x = x; uv.y = y; uv.w = w; uv.h = h;
			entry.uv = uv;
		}

		// 仅保留 uv 有效项
		Dictionary<string, BpRuntimeSpriteMeta> filtered = new Dictionary<string, BpRuntimeSpriteMeta>();
		foreach (KeyValuePair<string, BpRuntimeSpriteMeta> kv in spriteData)
			if (kv.Value.uv.w > 0f && kv.Value.uv.h > 0f) filtered[kv.Key] = kv.Value;
		return filtered;
	}

	// ---------------- parse.rs ----------------

	public static BpParsedPrefab ParsePrefab(string text)
	{
		BpParsedPrefab parsed = new BpParsedPrefab();

		foreach (string doc in BpAssets.SplitYamlDocs(text))
		{
			string[] lines = doc.Split('\n');
			if (lines.Length == 0) continue;
			string header = lines[0].Trim();

			int typeId;
			string fileId;
			if (!ParseDocHeader(header, out typeId, out fileId)) continue;

			switch (typeId)
			{
				case 1: ParseGameObject(doc, fileId, parsed.gameObjects); break;
				case 4: ParseTransform(doc, fileId, parsed.transforms); break;
				case 23: ParseRenderer(doc, parsed.renderers); break;
				case 114: ParseMonoBehaviour(doc, parsed); break;
			}
		}

		return parsed;
	}

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

	private static void ParseGameObject(string doc, string fileId, Dictionary<string, BpGameObjectInfo> gameObjects)
	{
		bool active = true;
		string name = "";

		foreach (string raw in doc.Split('\n'))
		{
			string trimmed = raw.Trim();
			const string nameKey = "m_Name:";
			if (trimmed.StartsWith(nameKey, StringComparison.Ordinal))
			{
				name = trimmed.Substring(nameKey.Length).Trim();
				continue;
			}
			const string activeKey = "m_IsActive:";
			if (trimmed.StartsWith(activeKey, StringComparison.Ordinal))
				active = trimmed.Substring(activeKey.Length).Trim() != "0";
		}

		gameObjects[fileId] = new BpGameObjectInfo { active = active, name = name };
	}

	private static void ParseTransform(string doc, string fileId, Dictionary<string, BpTransformInfo> transforms)
	{
		string gameObjectId = null;
		string father = "0";
		List<string> children = new List<string>();
		bool inChildren = false;

		foreach (string raw in doc.Split('\n'))
		{
			string trimmed = raw.Trim();

			const string goKey = "m_GameObject:";
			if (trimmed.StartsWith(goKey, StringComparison.Ordinal))
			{
				gameObjectId = ParseFileId(trimmed.Substring(goKey.Length));
				continue;
			}
			const string fatherKey = "m_Father:";
			if (trimmed.StartsWith(fatherKey, StringComparison.Ordinal))
			{
				string parsedFather = ParseFileId(trimmed.Substring(fatherKey.Length));
				father = parsedFather ?? "0";
				inChildren = false;
				continue;
			}
			if (trimmed.StartsWith("m_Children:", StringComparison.Ordinal))
			{
				inChildren = trimmed.IndexOf("[]", StringComparison.Ordinal) < 0;
				continue;
			}
			if (inChildren)
			{
				if (trimmed.StartsWith("-", StringComparison.Ordinal))
				{
					string childId = ParseFileId(trimmed);
					if (childId != null) children.Add(childId);
					continue;
				}
				if (trimmed.StartsWith("m_", StringComparison.Ordinal)) inChildren = false;
			}
		}

		if (gameObjectId == null) return;

		BpTransformInfo info = new BpTransformInfo();
		info.gameObjectId = gameObjectId;
		info.father = father;
		info.children = children;
		transforms[fileId] = info;
	}

	private static void ParseRenderer(string doc, Dictionary<string, BpRendererInfo> renderers)
	{
		string gameObjectId = null;
		bool enabled = true;
		string materialGuid = "";
		bool inMaterials = false;

		foreach (string raw in doc.Split('\n'))
		{
			string trimmed = raw.Trim();

			const string goKey = "m_GameObject:";
			if (trimmed.StartsWith(goKey, StringComparison.Ordinal))
			{
				gameObjectId = ParseFileId(trimmed.Substring(goKey.Length));
				continue;
			}
			const string enabledKey = "m_Enabled:";
			if (trimmed.StartsWith(enabledKey, StringComparison.Ordinal))
			{
				enabled = trimmed.Substring(enabledKey.Length).Trim() != "0";
				continue;
			}
			if (trimmed.StartsWith("m_Materials:", StringComparison.Ordinal))
			{
				inMaterials = true;
				continue;
			}
			if (inMaterials)
			{
				string guid = ParseGuid(trimmed);
				if (guid != null)
				{
					materialGuid = guid;
					inMaterials = false;
					continue;
				}
				if (trimmed.StartsWith("m_", StringComparison.Ordinal)) inMaterials = false;
			}
		}

		if (gameObjectId == null) return;
		renderers[gameObjectId] = new BpRendererInfo { materialGuid = materialGuid, enabled = enabled };
	}

	private static void ParseMonoBehaviour(string doc, BpParsedPrefab parsed)
	{
		string gameObjectId = null;

		string spriteId = null;
		float? scaleX = null;
		float? scaleY = null;

		float? uvX = null;
		float? uvY = null;
		float? gridW = null;
		float? gridH = null;
		float? spriteW = null;
		float? spriteH = null;
		float? subdivX = null;
		float? subdivY = null;

		foreach (string raw in doc.Split('\n'))
		{
			string trimmed = raw.Trim();

			const string goKey = "m_GameObject:";
			if (trimmed.StartsWith(goKey, StringComparison.Ordinal))
			{
				gameObjectId = ParseFileId(trimmed.Substring(goKey.Length));
				continue;
			}
			const string idKey = "m_id:";
			if (trimmed.StartsWith(idKey, StringComparison.Ordinal))
			{
				spriteId = trimmed.Substring(idKey.Length).Trim();
				continue;
			}
			const string scaleXKey = "m_scaleX:";
			if (trimmed.StartsWith(scaleXKey, StringComparison.Ordinal))
			{
				scaleX = TryParseFloatOrNull(trimmed.Substring(scaleXKey.Length));
				continue;
			}
			const string scaleYKey = "m_scaleY:";
			if (trimmed.StartsWith(scaleYKey, StringComparison.Ordinal))
			{
				scaleY = TryParseFloatOrNull(trimmed.Substring(scaleYKey.Length));
				continue;
			}
			const string uvXKey = "m_UVx:";
			if (trimmed.StartsWith(uvXKey, StringComparison.Ordinal))
			{
				uvX = TryParseFloatOrNull(trimmed.Substring(uvXKey.Length));
				continue;
			}
			const string uvYKey = "m_UVy:";
			if (trimmed.StartsWith(uvYKey, StringComparison.Ordinal))
			{
				uvY = TryParseFloatOrNull(trimmed.Substring(uvYKey.Length));
				continue;
			}
			const string widthKey = "m_width:";
			if (trimmed.StartsWith(widthKey, StringComparison.Ordinal))
			{
				gridW = TryParseFloatOrNull(trimmed.Substring(widthKey.Length));
				continue;
			}
			const string heightKey = "m_height:";
			if (trimmed.StartsWith(heightKey, StringComparison.Ordinal))
			{
				gridH = TryParseFloatOrNull(trimmed.Substring(heightKey.Length));
				continue;
			}
			const string spriteWKey = "m_spriteWidth:";
			if (trimmed.StartsWith(spriteWKey, StringComparison.Ordinal))
			{
				spriteW = TryParseFloatOrNull(trimmed.Substring(spriteWKey.Length));
				continue;
			}
			const string spriteHKey = "m_spriteHeight:";
			if (trimmed.StartsWith(spriteHKey, StringComparison.Ordinal))
			{
				spriteH = TryParseFloatOrNull(trimmed.Substring(spriteHKey.Length));
				continue;
			}
			const string subdivKey = "m_atlasGridSubdivisions:";
			if (trimmed.StartsWith(subdivKey, StringComparison.Ordinal))
			{
				float? parsedValue = TryParseFloatOrNull(trimmed.Substring(subdivKey.Length));
				subdivX = parsedValue;
				subdivY = parsedValue;
				continue;
			}
			const string subdivXKey = "m_subdivisionsX:";
			if (trimmed.StartsWith(subdivXKey, StringComparison.Ordinal))
			{
				subdivX = TryParseFloatOrNull(trimmed.Substring(subdivXKey.Length));
				continue;
			}
			const string subdivYKey = "m_subdivisionsY:";
			if (trimmed.StartsWith(subdivYKey, StringComparison.Ordinal))
				subdivY = TryParseFloatOrNull(trimmed.Substring(subdivYKey.Length));
		}

		if (gameObjectId == null) return;

		if (spriteId != null && scaleX.HasValue && scaleY.HasValue)
		{
			BpRuntimeSpriteComponent component = new BpRuntimeSpriteComponent();
			component.gameObjectId = gameObjectId;
			component.spriteId = spriteId;
			component.scaleX = scaleX.Value;
			component.scaleY = scaleY.Value;
			parsed.runtimeSprites.Add(component);
			return;
		}

		if (!uvX.HasValue || !uvY.HasValue || !gridW.HasValue || !gridH.HasValue || !spriteW.HasValue || !spriteH.HasValue)
			return;

		float sx = subdivX.HasValue ? subdivX.Value : 0f;
		float sy = subdivY.HasValue ? subdivY.Value : sx;
		if (sx <= 0f || sy <= 0f) return;

		BpSpriteDbUnmanagedComponent unmanaged = new BpSpriteDbUnmanagedComponent();
		BpUvRect uv = new BpUvRect();
		uv.x = uvX.Value / sx;
		uv.y = uvY.Value / sy;
		uv.w = gridW.Value / sx;
		uv.h = gridH.Value / sy;
		unmanaged.uv = uv;
		unmanaged.worldW = spriteW.Value * WORLD_SCALE;
		unmanaged.worldH = spriteH.Value * WORLD_SCALE;
		parsed.unmanagedSprites[gameObjectId] = unmanaged;
	}

	// parse_file_id / parse_guid / 数值工具
	public static string ParseFileId(string text)
	{
		if (text == null) return null;
		int find = text.IndexOf("fileID:", StringComparison.Ordinal);
		if (find < 0) return null;
		int start = find + "fileID:".Length;
		while (start < text.Length && (text[start] == ' ' || text[start] == '\t')) start++;
		int end = start;
		while (end < text.Length && text[end] >= '0' && text[end] <= '9') end++;
		if (end == start) return null;
		return text.Substring(start, end - start);
	}

	private static string ParseGuid(string text)
	{
		if (text == null) return null;
		int find = text.IndexOf("guid:", StringComparison.Ordinal);
		if (find < 0) return null;
		int start = find + "guid:".Length;
		while (start < text.Length && (text[start] == ' ' || text[start] == '\t')) start++;
		int end = start;
		while (end < text.Length && IsAsciiHexDigit(text[end])) end++;
		if (end == start) return null;
		return text.Substring(start, end - start);
	}

	public static bool TryParseFloat(string s, out float value)
	{
		return float.TryParse(s, System.Globalization.NumberStyles.Float,
			System.Globalization.CultureInfo.InvariantCulture, out value);
	}

	private static float? TryParseFloatOrNull(string s)
	{
		float v;
		if (TryParseFloat(s, out v)) return v;
		return null;
	}

	public static bool IsAsciiHexDigit(char c)
	{
		return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
	}

	private static bool IsAllAsciiDigits(string s)
	{
		if (string.IsNullOrEmpty(s)) return false;
		for (int i = 0; i < s.Length; i++)
			if (s[i] < '0' || s[i] > '9') return false;
		return true;
	}
}
