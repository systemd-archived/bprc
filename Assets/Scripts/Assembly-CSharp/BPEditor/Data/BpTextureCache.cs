using System;
using System.Collections.Generic;
using UnityEngine;

// 移植 badpiggies-editor 的 data/assets/texture_cache.rs —— 贴图缓存。
//
// ★ 渲染后端替换（计划允许的两处替换之一：egui → uGUI/Unity）★
// Rust 侧返回 egui::TextureId 并在上传时做 gamma 空间预乘（Color32::from_rgba_premultiplied）。
// Unity 侧改为 Texture2D 缓存：
//   - 像素按**直通 RGBA**存储，不烘焙预乘；预乘意图记录在 BpCachedTexture.premultiplied 上，
//     由渲染阶段选择预乘混合材质。
//   - wrap 模式用 Texture2D.wrapMode（Clamp / Repeat）表达。
//   - 裁剪坐标：Rust 用 image crate 的**左上原点**（py0 = (1 - uv_y - uv_h) * ah）；
//     Unity Texture2D 为**左下原点**，故等价行为取 py0 = uv_y * ah（内容与显示一致）。
//   - 缓存命中时 `LoadSpriteCropPadded` 的 uv 用已存尺寸按 (tex - pad)/tex 反推
//     —— 与 Rust 保持一致的既有行为（若同一 key 换 pad 重入会有差异）。

public class BpCachedTexture
{
	public Texture2D texture;
	public bool premultiplied;   // 源像素语义标记（对应 Rust `*_premultiplied`）
	public bool repeat;
	public bool flipV;
}

public class BpTextureCache
{
	private readonly Dictionary<string, BpCachedTexture> m_textures = new Dictionary<string, BpCachedTexture>();

	public void Clear()
	{
		foreach (KeyValuePair<string, BpCachedTexture> kv in m_textures)
			if (kv.Value.texture != null) UnityEngine.Object.Destroy(kv.Value.texture);
		m_textures.Clear();
	}

	public int Count { get { return m_textures.Count; } }

	// load_texture
	public BpCachedTexture LoadTexture(string path, string name)
	{
		return LoadCore(path, name, false, false, false);
	}

	// load_texture_repeat
	public BpCachedTexture LoadTextureRepeat(string path, string name)
	{
		return LoadCore(path, name, true, false, false);
	}

	// load_texture_repeat_flipv
	public BpCachedTexture LoadTextureRepeatFlipV(string path, string name)
	{
		return LoadCore(path, name, true, true, false);
	}

	private BpCachedTexture LoadCore(string path, string name, bool repeat, bool flipV, bool premultiplied)
	{
		BpCachedTexture cached;
		if (m_textures.TryGetValue(name ?? "", out cached)) return cached;

		Texture2D texture = LoadTexture2D(path, repeat, flipV);
		if (texture == null) return null;

		BpCachedTexture entry = new BpCachedTexture();
		entry.texture = texture;
		entry.premultiplied = premultiplied;
		entry.repeat = repeat;
		entry.flipV = flipV;
		m_textures[name ?? ""] = entry;
		return entry;
	}

	// load_sprite_crop —— 从图集裁出精灵区域
	public BpCachedTexture LoadSpriteCrop(string key, string atlasPath, float[] uvRect)
	{
		BpCachedTexture cached;
		if (m_textures.TryGetValue(key ?? "", out cached)) return cached;

		Texture2D atlas = LoadTexture2D(atlasPath, false, false);
		if (atlas == null) return null;

		Texture2D crop = CropRegion(atlas, uvRect, 0);
		UnityEngine.Object.Destroy(atlas);
		if (crop == null) return null;

		BpCachedTexture entry = new BpCachedTexture();
		entry.texture = crop;
		m_textures[key ?? ""] = entry;
		return entry;
	}

	// load_sprite_crop_padded_premultiplied —— 四边透明填充；返回裁剪区在贴图内的 uv 范围
	public bool LoadSpriteCropPadded(string key, string atlasPath, float[] uvRect, int padPx,
		out BpCachedTexture entry, out Vector2 uvMin, out Vector2 uvMax)
	{
		entry = null;
		uvMin = Vector2.zero;
		uvMax = Vector2.one;

		BpCachedTexture cached;
		if (m_textures.TryGetValue(key ?? "", out cached))
		{
			// Rust 命中路径：用已存尺寸反推 uv（保持既有行为）
			float texW = cached.texture.width;
			float texH = cached.texture.height;
			uvMin = new Vector2(padPx / texW, padPx / texH);
			uvMax = new Vector2((texW - padPx) / texW, (texH - padPx) / texH);
			entry = cached;
			return true;
		}

		Texture2D atlas = LoadTexture2D(atlasPath, false, false);
		if (atlas == null) return false;

		Texture2D padded = CropRegion(atlas, uvRect, padPx);
		UnityEngine.Object.Destroy(atlas);
		if (padded == null) return false;

		BpCachedTexture result = new BpCachedTexture();
		result.texture = padded;
		result.premultiplied = true;
		m_textures[key ?? ""] = result;

		int pw = padded.width - padPx * 2;
		int ph = padded.height - padPx * 2;
		float tw = padded.width;
		float th = padded.height;
		uvMin = new Vector2(padPx / tw, padPx / th);
		uvMax = new Vector2((padPx + pw) / tw, (padPx + ph) / th);
		entry = result;
		return true;
	}

	public BpCachedTexture Get(string name)
	{
		BpCachedTexture entry;
		return m_textures.TryGetValue(name ?? "", out entry) ? entry : null;
	}

	public bool TextureSize(string name, out int width, out int height)
	{
		width = 0;
		height = 0;
		BpCachedTexture entry;
		if (!m_textures.TryGetValue(name ?? "", out entry) || entry.texture == null) return false;
		width = entry.texture.width;
		height = entry.texture.height;
		return true;
	}

	// ---------------- 内部 ----------------

	private static Texture2D LoadTexture2D(string path, bool repeat, bool flipV)
	{
		byte[] bytes = BpUnityPackageLoader.ReadPathname(path);
		if (bytes == null) return null;

		Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
		if (!texture.LoadImage(bytes))
		{
			UnityEngine.Object.Destroy(texture);
			return null;
		}
		texture.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
		texture.filterMode = FilterMode.Bilinear;

		if (!flipV)
		{
			texture.Apply(false, false);
			return texture;
		}

		Texture2D flipped = FlipVertical(texture);
		UnityEngine.Object.Destroy(texture);
		return flipped;
	}

	private static Texture2D FlipVertical(Texture2D source)
	{
		int w = source.width;
		int h = source.height;
		Color32[] src = source.GetPixels32();
		Color32[] dst = new Color32[src.Length];
		for (int y = 0; y < h; y++)
		{
			int srcRow = y * w;
			int dstRow = (h - 1 - y) * w;
			Array.Copy(src, srcRow, dst, dstRow, w);
		}
		Texture2D outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
		outTex.SetPixels32(dst);
		outTex.wrapMode = source.wrapMode;
		outTex.filterMode = source.filterMode;
		outTex.Apply(false, false);
		return outTex;
	}

	// padPx = 0 时等价 load_sprite_crop；> 0 时等价 padded 版本（透明填充四周）
	private static Texture2D CropRegion(Texture2D atlas, float[] uvRect, int padPx)
	{
		if (uvRect == null || uvRect.Length < 4) return null;
		float uvX = uvRect[0], uvY = uvRect[1], uvW = uvRect[2], uvH = uvRect[3];

		int aw = atlas.width;
		int ah = atlas.height;

		// Unity 左下原点：py0 = uv_y * ah（等价于 Rust 左上原点的 (1-uv_y-uv_h)*ah）
		int px0 = (int)(uvX * aw);
		int py0 = (int)(uvY * ah);
		int pw = (int)(uvW * aw);
		int ph = (int)(uvH * ah);

		if (pw <= 0 || ph <= 0) return null;
		if (px0 < 0) px0 = 0;
		if (py0 < 0) py0 = 0;
		if (px0 + pw > aw) pw = aw - px0;
		if (py0 + ph > ah) ph = ah - py0;
		if (pw <= 0 || ph <= 0) return null;

		Color32[] src = atlas.GetPixels32();
		Color32[] dst = new Color32[(pw + padPx * 2) * (ph + padPx * 2)];
		// 透明填充
		for (int i = 0; i < dst.Length; i++) dst[i] = new Color32(0, 0, 0, 0);

		for (int y = 0; y < ph; y++)
		{
			int srcRow = (py0 + y) * aw + px0;
			int dstRow = (padPx + y) * (pw + padPx * 2) + padPx;
			Array.Copy(src, srcRow, dst, dstRow, pw);
		}

		Texture2D outTex = new Texture2D(pw + padPx * 2, ph + padPx * 2, TextureFormat.RGBA32, false);
		outTex.SetPixels32(dst);
		outTex.wrapMode = TextureWrapMode.Clamp;
		outTex.filterMode = FilterMode.Bilinear;
		outTex.Apply(false, false);
		return outTex;
	}
}
