using System;
using System.Collections.Generic;
using UnityEngine;

// 1:1 移植 badpiggies-editor 的 data/sprite_db/{atlas,builder}.rs。

public static class BpSpriteDbAtlas
{
	// atlas.rs: RUNTIME_ATLAS_FILENAMES
	private static readonly HashSet<string> RUNTIME_ATLAS_FILENAMES = new HashSet<string>
	{
		"IngameAtlas.png",
		"IngameAtlas2.png",
		"IngameAtlas3.png",
		"Ingame_Characters_Sheet_01.png",
		"Ingame_Sheet_04.png",
		"MenuAtlas.png",
		"MenuAtlas2.png",
	};

	private static Dictionary<string, string> s_uniqueAtlasByMaterialId;

	public static void Reset() { s_uniqueAtlasByMaterialId = null; }

	public static bool IsRuntimeAtlasFilename(string textureName)
	{
		return textureName != null && RUNTIME_ATLAS_FILENAMES.Contains(textureName);
	}

	public static string RuntimeUniqueAtlasForMaterialId(string materialId)
	{
		if (s_uniqueAtlasByMaterialId == null) s_uniqueAtlasByMaterialId = BuildRuntimeUniqueAtlasByMaterialId();
		string atlas;
		return s_uniqueAtlasByMaterialId.TryGetValue(materialId ?? "", out atlas) ? atlas : null;
	}

	private static Dictionary<string, string> BuildRuntimeUniqueAtlasByMaterialId()
	{
		Dictionary<string, BpRuntimeSpriteMeta> runtimeSprites = BpSpriteDb.RuntimeSprites();
		Dictionary<string, HashSet<string>> atlasesByMaterialId = new Dictionary<string, HashSet<string>>();

		string[] prefixes = { "Assets/Prefab/", "Assets/Resources/ui/" };
		foreach (string prefix in prefixes)
		{
			foreach (string assetPath in BpUnityPackageLoader.ListPathnames(prefix, ".prefab"))
			{
				string text = BpSpriteDb.ReadEmbeddedText(assetPath);
				if (text == null) continue;
				BpParsedPrefab parsed = BpSpriteDb.ParsePrefab(text);

				foreach (BpRuntimeSpriteComponent component in parsed.runtimeSprites)
				{
					BpRuntimeSpriteMeta meta;
					if (!runtimeSprites.TryGetValue(component.spriteId, out meta)) continue;

					BpGameObjectInfo gameObject;
					if (!parsed.gameObjects.TryGetValue(component.gameObjectId, out gameObject)) continue;

					BpRendererInfo renderer;
					if (!parsed.renderers.TryGetValue(component.gameObjectId, out renderer)) continue;

					if (!gameObject.active || !renderer.enabled) continue;

					string textureName = BpAssets.EffectTextureNameForMaterialGuid(renderer.materialGuid);
					if (textureName == null) continue;
					if (!IsRuntimeAtlasFilename(textureName)) continue;

					HashSet<string> set;
					if (!atlasesByMaterialId.TryGetValue(meta.materialId, out set))
					{
						set = new HashSet<string>();
						atlasesByMaterialId[meta.materialId] = set;
					}
					set.Add(textureName);
				}
			}
		}

		// 仅保留“唯一图集”的 material_id（歧义项丢弃）
		Dictionary<string, string> unique = new Dictionary<string, string>();
		foreach (KeyValuePair<string, HashSet<string>> kv in atlasesByMaterialId)
		{
			if (kv.Value.Count != 1) continue;
			foreach (string atlas in kv.Value) { unique[kv.Key] = atlas; break; }
		}
		return unique;
	}

	// is_representative_runtime_sprite_name
	public static bool IsRepresentativeRuntimeSpriteName(string gameObjectName)
	{
		switch (NormalizeRuntimeSpriteName(gameObjectName))
		{
			case "closebutton":
			case "backbutton":
			case "loading":
			case "buybutton":
			case "claimbutton":
			case "watchad":
			case "openpopupbutton":
			case "trackicon":
			case "levelicon":
			case "softcurrencyicon":
			case "okbutton":
			case "infobutton":
				return true;
		}
		return false;
	}

	private static string NormalizeRuntimeSpriteName(string name)
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

	public static string AtlasForMaterialGuid(string materialGuid)
	{
		return BpAssets.AtlasForMaterialGuid(materialGuid);
	}
}

public static class BpSpriteDbBuilder
{
	public static void Reset() { BpSpriteDbAtlas.Reset(); }

	// builder.rs: find_runtime_sprite_info
	public static BpSpriteInfo FindRuntimeSpriteInfo(BpParsedPrefab parsed, Dictionary<string, BpRuntimeSpriteMeta> runtimeSprites)
	{
		BpRuntimeSpriteComponent preferred = PreferredRuntimeSpriteComponent(parsed);
		if (preferred != null)
		{
			BpSpriteInfo info = RuntimeSpriteInfoFromComponent(parsed, runtimeSprites, preferred);
			if (info != null) return info;
		}

		foreach (BpRuntimeSpriteComponent component in parsed.runtimeSprites)
		{
			BpSpriteInfo info = RuntimeSpriteInfoFromComponent(parsed, runtimeSprites, component);
			if (info != null) return info;
		}

		return null;
	}

	// preferred_runtime_sprite_component
	public static BpRuntimeSpriteComponent PreferredRuntimeSpriteComponent(BpParsedPrefab parsed)
	{
		foreach (BpRuntimeSpriteComponent component in parsed.runtimeSprites)
		{
			BpGameObjectInfo gameObject;
			if (!parsed.gameObjects.TryGetValue(component.gameObjectId, out gameObject)) continue;
			if (BpSpriteDbAtlas.IsRepresentativeRuntimeSpriteName(gameObject.name)) return component;
		}
		return null;
	}

	// runtime_sprite_info_from_component
	public static BpSpriteInfo RuntimeSpriteInfoFromComponent(
		BpParsedPrefab parsed, Dictionary<string, BpRuntimeSpriteMeta> runtimeSprites, BpRuntimeSpriteComponent component)
	{
		BpRuntimeSpriteMeta meta;
		if (!runtimeSprites.TryGetValue(component.spriteId, out meta)) return null;

		string atlas = PreferredRendererRuntimeAtlas(parsed, component);
		if (atlas == null) atlas = BpSpriteDbAtlas.RuntimeUniqueAtlasForMaterialId(meta.materialId);
		if (atlas == null)
		{
			BpRendererInfo renderer;
			if (parsed.renderers.TryGetValue(component.gameObjectId, out renderer))
				atlas = BpSpriteDbAtlas.AtlasForMaterialGuid(renderer.materialGuid);
		}
		if (atlas == null) return null;

		BpSpriteInfo info = new BpSpriteInfo();
		info.atlas = atlas;
		info.uv = meta.uv;
		info.worldW = meta.width * component.scaleX * BpSpriteDb.WORLD_SCALE;
		info.worldH = meta.height * component.scaleY * BpSpriteDb.WORLD_SCALE;
		return info;
	}

	// preferred_renderer_runtime_atlas
	private static string PreferredRendererRuntimeAtlas(BpParsedPrefab parsed, BpRuntimeSpriteComponent component)
	{
		BpRendererInfo renderer;
		if (!parsed.renderers.TryGetValue(component.gameObjectId, out renderer)) return null;
		string texture = BpAssets.EffectTextureNameForMaterialGuid(renderer.materialGuid);
		if (texture == null) return null;
		return BpSpriteDbAtlas.IsRuntimeAtlasFilename(texture) ? texture : null;
	}

	// builder.rs: find_unmanaged_sprite_info
	public static BpSpriteInfo FindUnmanagedSpriteInfo(string prefabName, BpParsedPrefab parsed)
	{
		// 普通 GoalArea prefab 的布料走专用 GoalSprite 网格，而不是矩形 sprite 组件；
		// 回退到 goal-achievement 子对象会生成错误的方块。
		if (prefabName != null && prefabName.StartsWith("GoalArea", StringComparison.Ordinal)) return null;

		string rootTransformId = null;
		foreach (KeyValuePair<string, BpTransformInfo> kv in parsed.transforms)
		{
			if (kv.Value.father == "0") { rootTransformId = kv.Key; break; }
		}
		if (rootTransformId == null) return null;

		return FindUnmanagedSpriteInfoAt(rootTransformId, parsed);
	}

	private static BpSpriteInfo FindUnmanagedSpriteInfoAt(string transformId, BpParsedPrefab parsed)
	{
		BpTransformInfo transform;
		if (!parsed.transforms.TryGetValue(transformId, out transform)) return null;

		BpGameObjectInfo gameObject;
		if (!parsed.gameObjects.TryGetValue(transform.gameObjectId, out gameObject)) return null;
		if (!gameObject.active) return null;

		BpRendererInfo renderer;
		BpSpriteDbUnmanagedComponent component;
		if (parsed.renderers.TryGetValue(transform.gameObjectId, out renderer)
			&& renderer.enabled
			&& parsed.unmanagedSprites.TryGetValue(transform.gameObjectId, out component))
		{
			BpSpriteInfo info = new BpSpriteInfo();
			info.atlas = BpSpriteDb.UNMANAGED_ATLAS;
			info.uv = component.uv;
			info.worldW = component.worldW;
			info.worldH = component.worldH;
			return info;
		}

		foreach (string childId in transform.children)
		{
			BpSpriteInfo info = FindUnmanagedSpriteInfoAt(childId, parsed);
			if (info != null) return info;
		}

		return null;
	}
}
