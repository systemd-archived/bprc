using System;
using System.Collections.Generic;
using UnityEngine;

// 1:1 移植 badpiggies-editor 的 data/bg_data/theme.rs。
// 从内置背景 prefab 遍历构建 BgTheme（精灵/组默认位置/子节点顺序）。
//
// ★ 渲染后端替换（egui→Unity）：Rust 用 image crate 解码 PNG 并对 RgbaImage 采样像素；
// Unity 侧用 Texture2D.LoadImage + GetPixels32。像素坐标说明：
//   Rust bg_sprite_image_bounds 的 y 为**左上原点**（image crate 约定）；
//   Unity Color32[] 为**左下原点**，故取像素时用 y_unity = height-1-y_top。

public class BpBgAtlasImage
{
	public int width;
	public int height;
	public Color32[] pixels;   // Unity 顺序：index = y*width + x，y 自下而上

	// 以左上原点取像素（等价 Rust RgbaImage::get_pixel）
	public Color32 PixelTopDown(int x, int y)
	{
		int uy = height - 1 - y;
		if (x < 0 || y < 0 || x >= width || uy < 0 || uy >= height) return new Color32(0, 0, 0, 0);
		return pixels[uy * width + x];
	}
}

public static class BpBgThemeBuilder
{
	private class GroupContext
	{
		public string name;
		public BpBgLayer layer;
		public float[] origin;   // 3
	}

	private class SpriteBuildInput
	{
		public string themeName;
		public GroupContext group;
		public BpBgGameObjectInfo gameObject;
		public float[] worldPos;   // 3
		public float[] worldScale; // 3
		public BpBgSpriteComponent spriteComponent;
		public string materialGuid;
		public Dictionary<string, string> textureloaderMaterials;
	}

	private static Dictionary<string, BpBgTheme> s_themes;
	private static Dictionary<string, BpBgAtlasImage> s_atlasImages;

	// 依赖 theme 命名（BpBgThemeNames）与资源库；重置时一并清除缓存
	public static void Reset()
	{
		s_themes = null;
		s_atlasImages = null;
		BpBgTables.Reset();
	}

	// ---------------- 构建入口 ----------------

	public static BpBgTheme GetTheme(string name)
	{
		if (s_themes == null) s_themes = BuildThemes();
		BpBgTheme theme;
		return s_themes.TryGetValue(name ?? "", out theme) ? theme : null;
	}

	public static Dictionary<string, BpBgTheme> Themes()
	{
		if (s_themes == null) s_themes = BuildThemes();
		return s_themes;
	}

	private static Dictionary<string, BpBgTheme> BuildThemes()
	{
		Dictionary<string, string> textureloaderMaterials = BpBgPrefabParse.LoadTextureloaderMaterials();
		List<string> prefabPaths = BpUnityPackageLoader.ListPathnames("Assets/Resources/environment/background/", ".prefab");
		Dictionary<string, BpBgTheme> themes = new Dictionary<string, BpBgTheme>();

		foreach (string prefabPath in prefabPaths)
		{
			string prefabName;
			try { prefabName = System.IO.Path.GetFileNameWithoutExtension(prefabPath); }
			catch (Exception) { continue; }
			if (string.IsNullOrEmpty(prefabName)) continue;

			string themeName = BpBgThemeNames.ThemeNameForBackgroundPrefab(prefabName);
			if (themeName == null) continue;

			BpBgTheme theme = BuildTheme(themeName, prefabPath, textureloaderMaterials);
			if (theme != null)
				themes[themeName] = theme;
			else
				Debug.LogError("[BPEditor] Failed to build embedded background theme " + themeName + " from " + prefabPath);
		}
		return themes;
	}

	private static BpBgTheme BuildTheme(string themeName, string prefabPath, Dictionary<string, string> textureloaderMaterials)
	{
		string raw = BpBgPrefabParse.ReadEmbeddedText(prefabPath);
		if (raw == null) return null;
		BpBgParsedPrefab prefab = BpBgPrefabParse.ParsePrefab(raw);
		if (prefab == null) return null;

		BpBgTransformInfo rootTransform;
		if (!prefab.transforms.TryGetValue(prefab.rootTransformId, out rootTransform)) return null;

		Dictionary<string, float[]> groupDefaults = new Dictionary<string, float[]>();
		List<string> childOrder = new List<string>();
		List<BpBgSprite> sprites = new List<BpBgSprite>();

		List<string> children = new List<string>(rootTransform.children);
		children.Sort((x, y) => RootOrderOf(prefab, x).CompareTo(RootOrderOf(prefab, y)));

		foreach (string childId in children)
			TraverseGroup(prefab, textureloaderMaterials, themeName, childId,
				new float[] { 0f, 0f, 0f }, new float[] { 1f, 1f, 1f }, null, groupDefaults, childOrder, sprites);

		// 稳定排序：layer.order 升序，world_z 降序（Rust sort_by 是稳定排序）
		List<int> order = new List<int>(sprites.Count);
		for (int i = 0; i < sprites.Count; i++) order.Add(i);
		order.Sort((i, j) =>
		{
			BpBgSprite a = sprites[i];
			BpBgSprite b = sprites[j];
			int c = a.layer.Order().CompareTo(b.layer.Order());
			if (c != 0) return c;
			if (float.IsNaN(a.worldZ) || float.IsNaN(b.worldZ)) return i.CompareTo(j);   // Rust: Equal
			c = b.worldZ.CompareTo(a.worldZ);
			if (c != 0) return c;
			return i.CompareTo(j);
		});
		List<BpBgSprite> sorted = new List<BpBgSprite>(sprites.Count);
		foreach (int i in order) sorted.Add(sprites[i]);

		BpBgTheme theme = new BpBgTheme();
		theme.sprites = sorted;
		theme.groupDefaults = groupDefaults;
		theme.childOrder = childOrder;
		return theme;
	}

	private static int RootOrderOf(BpBgParsedPrefab prefab, string transformId)
	{
		BpBgTransformInfo t;
		if (prefab.transforms.TryGetValue(transformId, out t)) return t.rootOrder;
		return 0;
	}

	private static void TraverseGroup(
		BpBgParsedPrefab prefab,
		Dictionary<string, string> textureloaderMaterials,
		string themeName,
		string transformId,
		float[] parentPos,
		float[] parentScale,
		GroupContext group,
		Dictionary<string, float[]> groupDefaults,
		List<string> childOrder,
		List<BpBgSprite> sprites)
	{
		BpBgTransformInfo transform;
		if (!prefab.transforms.TryGetValue(transformId, out transform)) return;
		BpBgGameObjectInfo gameObject;
		if (!prefab.gameObjects.TryGetValue(transform.gameObjectId, out gameObject)) return;
		if (!gameObject.active) return;

		float[] worldPos = CombineWorldPos(parentPos, parentScale, transform.localPos);
		float[] worldScale = CombineWorldScale(parentScale, transform.localScale);

		if (group == null)
		{
			groupDefaults[gameObject.name] = worldPos;
			SetChildOrderEntry(childOrder, transform.rootOrder, gameObject.name);
			GroupContext created = new GroupContext();
			created.name = gameObject.name;
			created.layer = BpBgTables.ClassifyGroupLayer(gameObject.tag);
			created.origin = worldPos;
			group = created;
		}

		BpBgLayer? explicitLayer = BpBgTables.ExplicitParallaxLayer(gameObject.tag);
		if (explicitLayer.HasValue)
		{
			if (!groupDefaults.ContainsKey(gameObject.name)) groupDefaults[gameObject.name] = worldPos;
			GroupContext replaced = new GroupContext();
			replaced.name = gameObject.name;
			replaced.layer = explicitLayer.Value;
			replaced.origin = worldPos;
			group = replaced;
		}

		BpBgSpriteComponent spriteComponent;
		string materialGuid;
		if (prefab.sprites.TryGetValue(transform.gameObjectId, out spriteComponent)
			&& prefab.renderers.TryGetValue(transform.gameObjectId, out materialGuid))
		{
			SpriteBuildInput input = new SpriteBuildInput();
			input.themeName = themeName;
			input.group = group;
			input.gameObject = gameObject;
			input.worldPos = worldPos;
			input.worldScale = worldScale;
			input.spriteComponent = spriteComponent;
			input.materialGuid = materialGuid;
			input.textureloaderMaterials = textureloaderMaterials;

			BpBgSprite sprite = BuildBgSprite(input);
			if (sprite != null) sprites.Add(sprite);
		}

		List<string> children = new List<string>(transform.children);
		children.Sort((x, y) => RootOrderOf(prefab, x).CompareTo(RootOrderOf(prefab, y)));

		foreach (string childId in children)
		{
			GroupContext childGroup = new GroupContext();
			childGroup.name = group.name;
			childGroup.layer = group.layer;
			childGroup.origin = group.origin;
			TraverseGroup(prefab, textureloaderMaterials, themeName, childId, worldPos, worldScale,
				childGroup, groupDefaults, childOrder, sprites);
		}
	}

	private static void SetChildOrderEntry(List<string> childOrder, int rootOrder, string name)
	{
		if (rootOrder < 0) return;   // Rust: usize::try_from 失败即跳过
		while (childOrder.Count <= rootOrder) childOrder.Add("");
		childOrder[rootOrder] = name;
	}

	private static float[] CombineWorldPos(float[] parentPos, float[] parentScale, float[] localPos)
	{
		return new float[]
		{
			parentPos[0] + parentScale[0] * localPos[0],
			parentPos[1] + parentScale[1] * localPos[1],
			parentPos[2] + parentScale[2] * localPos[2],
		};
	}

	private static float[] CombineWorldScale(float[] parentScale, float[] localScale)
	{
		return new float[]
		{
			parentScale[0] * localScale[0],
			parentScale[1] * localScale[1],
			parentScale[2] * localScale[2],
		};
	}

	// ---------------- 精灵构建 ----------------

	private static float[] DefaultMainTexSt()
	{
		return new float[] { 1f, 1f, 0f, 0f };
	}

	private static BpMaterialShaderKind FallbackBgShaderKind(bool hasFillColor, bool skyTexture, bool alphaBlend, bool alpha8bit)
	{
		if (alpha8bit) return BpMaterialShaderKind.CustomUnlitAlpha8BitColor;
		if (alphaBlend)
			return skyTexture ? BpMaterialShaderKind.BuiltinUnlitTransparent : BpMaterialShaderKind.CustomUnlitColorTransparentGeometry;
		if (hasFillColor) return BpMaterialShaderKind.CustomUnlitMonochrome;
		return BpMaterialShaderKind.BuiltinUnlitTransparentCutout;
	}

	private static BpBgSprite BuildBgSprite(SpriteBuildInput input)
	{
		string guid = input.materialGuid;

		byte[] materialFillColor = BpMaterialRefs.MaterialColorForGuid(guid);
		if (materialFillColor == null) materialFillColor = BpMaterialRefs.MaterialColorForGuidPrefix(guid);

		byte[] rawTint = BpMaterialRefs.MaterialColorRgbaForGuid(guid);
		if (rawTint == null) rawTint = BpMaterialRefs.MaterialColorRgbaForGuidPrefix(guid);
		float[] materialTint = rawTint != null
			? new float[] { rawTint[0] / 255f, rawTint[1] / 255f, rawTint[2] / 255f, rawTint[3] / 255f }
			: new float[] { 1f, 1f, 1f, 1f };

		bool legacyAlphaBlend = BpMaterialRefs.MaterialAlphaBlendForGuid(guid)
			|| BpMaterialRefs.MaterialAlphaBlendForGuidPrefix(guid);
		bool legacyAlpha8bit = BpMaterialRefs.MaterialAlpha8BitForGuid(guid)
			|| BpMaterialRefs.MaterialAlpha8BitForGuidPrefix(guid);

		float? cutoffValue = BpMaterialRefs.MaterialCutoffForGuid(guid);
		if (!cutoffValue.HasValue) cutoffValue = BpMaterialRefs.MaterialCutoffForGuidPrefix(guid);
		float materialCutoff = cutoffValue ?? 0.5f;

		int? customRenderQueue = BpMaterialRefs.MaterialCustomRenderQueueForGuid(guid);
		if (!customRenderQueue.HasValue) customRenderQueue = BpMaterialRefs.MaterialCustomRenderQueueForGuidPrefix(guid);

		string atlas = null;
		string skyTexture = null;
		byte[] fillColor = null;

		string assetName;
		if (input.textureloaderMaterials.TryGetValue(guid, out assetName))
		{
			if (BpBgPrefabParse.IsSkyTextureAsset(assetName))
				skyTexture = BpBgPrefabParse.AssetFilename(assetName);
			else
				atlas = BpBgPrefabParse.AssetFilename(assetName);
		}
		else
		{
			string atlasName = BpAssets.AtlasForMaterialGuid(guid);
			if (atlasName != null) atlas = atlasName;
			else if (materialFillColor != null) fillColor = materialFillColor;
			else
			{
				Debug.LogWarning("[BPEditor] Missing background material mapping for theme=" + input.themeName
					+ ", sprite=" + input.gameObject.name + ", guid=" + guid);
				return null;
			}
		}

		BpMaterialShaderKind? kind = BpMaterialRefs.MaterialShaderKindForGuid(guid);
		if (!kind.HasValue) kind = BpMaterialRefs.MaterialShaderKindForGuidPrefix(guid);
		BpMaterialShaderKind shaderKind = kind ?? FallbackBgShaderKind(
			fillColor != null, skyTexture != null, legacyAlphaBlend, legacyAlpha8bit);

		float[] mainTexSt = BpMaterialRefs.MaterialMainTexStForGuid(guid);
		if (mainTexSt == null) mainTexSt = BpMaterialRefs.MaterialMainTexStForGuidPrefix(guid);
		if (mainTexSt == null) mainTexSt = DefaultMainTexSt();

		bool atlasSoftAlphaBlend = atlas != null && SpriteRequiresSoftAlphaBlend(atlas, input.spriteComponent);
		bool materialAlphaBlend =
			shaderKind == BpMaterialShaderKind.CustomUnlitColorTransparentGeometry
			|| shaderKind == BpMaterialShaderKind.CustomUnlitAlpha8BitColor
			|| shaderKind == BpMaterialShaderKind.BuiltinUnlitTransparent;

		bool isGroupRoot = input.gameObject.name == input.group.name;

		BpBgSprite sprite = new BpBgSprite();
		sprite.name = input.gameObject.name;
		sprite.atlas = atlas;
		sprite.fillColor = fillColor;
		sprite.skyTexture = skyTexture;
		sprite.uvX = input.spriteComponent.uvX;
		sprite.uvY = input.spriteComponent.uvY;
		sprite.gridW = input.spriteComponent.width;
		sprite.gridH = input.spriteComponent.height;
		sprite.spriteW = input.spriteComponent.spriteWidth;
		sprite.spriteH = input.spriteComponent.spriteHeight;
		sprite.subdiv = input.spriteComponent.subdiv;
		sprite.border = input.spriteComponent.border;
		sprite.worldX = input.worldPos[0];
		sprite.worldY = input.worldPos[1];
		sprite.worldZ = input.worldPos[2];
		sprite.scaleX = input.worldScale[0];
		sprite.scaleY = input.worldScale[1];
		sprite.layer = input.group.layer;
		sprite.localX = isGroupRoot ? input.worldPos[0] : input.worldPos[0] - input.group.origin[0];
		sprite.localY = isGroupRoot ? input.worldPos[1] : input.worldPos[1] - input.group.origin[1];
		sprite.parentGroup = input.group.name;
		sprite.tint = materialTint;
		sprite.shaderKind = shaderKind;
		sprite.mainTexSt = mainTexSt;
		sprite.customRenderQueue = customRenderQueue;
		sprite.alphaBlend = materialAlphaBlend || input.group.layer == BpBgLayer.Camera || atlasSoftAlphaBlend;
		sprite.cutoff = materialCutoff;
		return sprite;
	}

	private static bool SpriteRequiresSoftAlphaBlend(string atlasName, BpBgSpriteComponent spriteComponent)
	{
		BpBgAtlasImage image;
		if (!BackgroundAtlasImages().TryGetValue(atlasName, out image)) return false;

		float subdiv = spriteComponent.subdiv;
		if (subdiv <= 0f) return false;

		float width = image.width;
		float height = image.height;
		int x0 = RoundToInt((spriteComponent.uvX / subdiv) * width);
		int x1 = RoundToInt(((spriteComponent.uvX + spriteComponent.width) / subdiv) * width);
		int y0 = RoundToInt(((subdiv - spriteComponent.uvY - spriteComponent.height) / subdiv) * height);
		int y1 = RoundToInt(((subdiv - spriteComponent.uvY) / subdiv) * height);
		x0 = Clamp(x0, 0, image.width);
		x1 = Clamp(x1, 0, image.width);
		y0 = Clamp(y0, 0, image.height);
		y1 = Clamp(y1, 0, image.height);
		if (x0 >= x1 || y0 >= y1) return false;

		bool sawVisiblePixel = false;
		for (int y = y0; y < y1; y++)
		{
			for (int x = x0; x < x1; x++)
			{
				byte alpha = image.PixelTopDown(x, y).a;
				if (alpha == 0) continue;
				sawVisiblePixel = true;
				if (alpha >= 128) return false;
			}
		}
		return sawVisiblePixel;
	}

	private static Dictionary<string, BpBgAtlasImage> BackgroundAtlasImages()
	{
		if (s_atlasImages == null)
		{
			Dictionary<string, BpBgAtlasImage> images = new Dictionary<string, BpBgAtlasImage>();
			foreach (string atlasName in BpBgTables.BgAtlasFiles())
			{
				byte[] data = BpUnityPackageLoader.ReadPathname("Assets/Texture2D/" + atlasName);
				if (data == null) continue;
				BpBgAtlasImage image = LoadImage(data);
				if (image == null) continue;
				images[atlasName] = image;
			}
			s_atlasImages = images;
		}
		return s_atlasImages;
	}

	// 供同模块其它取色逻辑复用（对应 image crate 的 RgbaImage）
	internal static BpBgAtlasImage LoadImage(byte[] data)
	{
		Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
		if (!tex.LoadImage(data))
		{
			UnityEngine.Object.Destroy(tex);
			return null;
		}
		BpBgAtlasImage image = new BpBgAtlasImage();
		image.width = tex.width;
		image.height = tex.height;
		image.pixels = tex.GetPixels32();
		UnityEngine.Object.Destroy(tex);
		return image;
	}

	internal static int RoundToInt(float v)
	{
		return (int)Math.Round((double)v, MidpointRounding.AwayFromZero);
	}

	internal static int Clamp(int v, int min, int max)
	{
		if (v < min) return min;
		if (v > max) return max;
		return v;
	}
}
