using System;
using System.Collections.Generic;

// 1:1 移植 badpiggies-editor 的 data/assets/mod.rs（**不含** theme.rs —— theme 属后续批次）。
// 提供 effect_texture_name_for_material_guid / terrain_texture_asset_key /
// atlas_for_material_guid 转发，以及 prefab YAML 文档切分公共工具。

public static class BpAssets
{
	// mod.rs: effect_texture_name_for_material_guid
	//   refs::texture_name_for_guid(guid).or_else(refs::material_texture_name_for_guid(guid))
	public static string EffectTextureNameForMaterialGuid(string materialGuid)
	{
		string name = BpMaterialRefs.TextureNameForGuid(materialGuid);
		if (name != null) return name;
		return BpMaterialRefs.MaterialTextureNameForGuid(materialGuid);
	}

	// mod.rs: terrain_texture_asset_key —— Assets/Texture2D/<filename> 存在则返回该 pathname
	public static string TerrainTextureAssetKey(string filename)
	{
		string path = "Assets/Texture2D/" + filename;
		return BpUnityPackageLoader.ReadPathname(path) != null ? path : null;
	}

	// mod.rs 转发：atlas_materials::atlas_for_material_guid
	public static string AtlasForMaterialGuid(string materialGuid)
	{
		return BpAtlasMaterials.AtlasForMaterialGuid(materialGuid);
	}

	// 等价 Rust `text.split("--- ").skip(1)`：返回每个 "--- " 之后到下一个 "--- " 之前的片段。
	// （prefab/Levels prefab YAML 的 doc 切分，level_db / sprite_db 共用同一语义）
	public static List<string> SplitYamlDocs(string text)
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
}
