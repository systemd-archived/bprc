using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace BadPiggies.Multiplayer.Online.Ui
{
	/// <summary>
	/// The lobby's visual language: a Division inspired glass command surface with cold white
	/// typography, orange action lines and cyan network telemetry.
	///
	/// Nothing here is loaded from an asset bundle. The rounded/circular sprites are generated once
	/// at runtime and cached, so the lobby looks the same in a build as it does in the editor and
	/// adds no new files to the project. The only external dependency is the CJK-capable
	/// TextMeshPro font the project already stages for Heat UI.
	/// </summary>
	public static class MctierTheme
	{
		// --- palette --------------------------------------------------------------------------
		// Panels are deliberately translucent: OnlineLobbyBackdrop supplies the blurred MainMenu
		// image underneath, while these colours provide the readable glass tint above it.
		public static readonly Color WindowBg = Hex("00000000");
		public static readonly Color SidebarBg = Hex("19232D00");
		public static readonly Color CardBg = Hex("2B30333D");
		public static readonly Color CardBgHover = Hex("4538265C");
		public static readonly Color CardBorder = Hex("ABB5B89E");
		public static readonly Color FieldBg = Hex("FFFFFF08");
		public static readonly Color FieldBorder = Hex("FFFFFFBF");
		public static readonly Color Accent = Hex("FFAF00");
		public static readonly Color AccentHover = Hex("FFFFFF");
		public static readonly Color OnAccent = Hex("19232D");
		public static readonly Color Success = Hex("63D78B");
		public static readonly Color Warning = Hex("F4C54A");
		public static readonly Color Danger = Hex("FF5B62");
		public static readonly Color TextPrimary = Hex("F0F2F0");
		public static readonly Color TextSecondary = Hex("D6DEDB");
		public static readonly Color TextMuted = Hex("B0BABD");
		public static readonly Color Separator = Hex("FFFFFF26");
		public static readonly Color ChatSelf = Hex("8F531DA6");
		public static readonly Color ChatOther = Hex("0C3042B8");
		public static readonly Color Overlay = new Color(0.06f, 0.075f, 0.085f, 0.30f);

		/// <summary>Font used for every label. Never null: falls back to the TMP default asset.</summary>
		public static TMP_FontAsset Font
		{
			get
			{
				if (s_font == null)
				{
					s_font = Resources.Load<TMP_FontAsset>("HeatUI/Chinese Fallback");
				}
				if (s_font == null)
				{
					s_font = TMP_Settings.defaultFontAsset;
				}
				return s_font;
			}
		}

		private static TMP_FontAsset s_font;
		private static readonly Dictionary<int, UnityEngine.Sprite> s_roundedCache = new Dictionary<int, UnityEngine.Sprite>();
		private static readonly Dictionary<int, UnityEngine.Sprite> s_ringCache = new Dictionary<int, UnityEngine.Sprite>();
		private static readonly Dictionary<long, UnityEngine.Sprite> s_outlineCache = new Dictionary<long, UnityEngine.Sprite>();
		private static UnityEngine.Sprite s_circle;
		private static UnityEngine.Sprite s_glassPanel;
		private static UnityEngine.Sprite s_glassButton;
		private static UnityEngine.Sprite s_glassInput;
		private static UnityEngine.Sprite s_glassHeader;
		private static UnityEngine.Sprite s_accentLine;

		/// <summary>
		/// Procedural glass surfaces that reproduce the approved preview's cut corners, soft vertical
		/// highlight and translucent body. No preview screenshot is used as a runtime asset.
		/// </summary>
		public static UnityEngine.Sprite GlassPanel()
		{
			return s_glassPanel ?? (s_glassPanel = ProceduralGlass("Panel", 0.72f, 8));
		}

		public static UnityEngine.Sprite GlassButton()
		{
			return s_glassButton ?? (s_glassButton = ProceduralGlass("Button", 0.42f, 6));
		}

		public static UnityEngine.Sprite GlassInput()
		{
			return s_glassInput ?? (s_glassInput = ProceduralGlass("Input", 0.78f, 5));
		}

		public static UnityEngine.Sprite GlassHeader()
		{
			return s_glassHeader ?? (s_glassHeader = ProceduralGlass("Header", 0.48f, 5));
		}

		public static UnityEngine.Sprite AccentLine()
		{
			if (s_accentLine != null)
			{
				return s_accentLine;
			}
			Texture2D texture = NewTexture(64, 6, "Division Accent Line");
			Color32[] pixels = new Color32[64 * 6];
			for (int y = 0; y < 6; y++)
			{
				float alpha = y < 2 ? 1f : 0.24f;
				for (int x = 0; x < 64; x++)
				{
					pixels[y * 64 + x] = new Color(1f, 0.545f, 0.063f, alpha);
				}
			}
			texture.SetPixels32(pixels);
			texture.Apply(false, false);
			s_accentLine = UnityEngine.Sprite.Create(texture, new Rect(0f, 0f, 64f, 6f),
				new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, new Vector4(1f, 1f, 1f, 1f));
			s_accentLine.name = "DivisionGlass AccentLine";
			s_accentLine.hideFlags = HideFlags.DontSave;
			return s_accentLine;
		}

		/// <summary>
		/// A white, anti-aliased rounded rectangle whose 9-slice border equals <paramref name="radius"/>,
		/// so <c>Image.type = Sliced</c> keeps the corners round at any size.
		/// </summary>
		public static UnityEngine.Sprite RoundedRect(int radius)
		{
			if (radius < 1)
			{
				radius = 1;
			}
			UnityEngine.Sprite cached;
			if (s_roundedCache.TryGetValue(radius, out cached) && cached != null)
			{
				return cached;
			}
			int size = radius * 2 + 8;
			Texture2D texture = NewTexture(size, size, "Mctier Rounded " + radius);
			Color32[] pixels = new Color32[size * size];
			float half = size * 0.5f;
			float inner = half - radius;
			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					float px = x + 0.5f - half;
					float py = y + 0.5f - half;
					float qx = Mathf.Abs(px) - inner;
					float qy = Mathf.Abs(py) - inner;
					float ax = Mathf.Max(qx, 0f);
					float ay = Mathf.Max(qy, 0f);
					float distance = Mathf.Sqrt(ax * ax + ay * ay) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
					byte alpha = (byte)(Mathf.Clamp01(0.5f - distance) * 255f);
					pixels[y * size + x] = new Color32(255, 255, 255, alpha);
				}
			}
			texture.SetPixels32(pixels);
			texture.Apply(false, false);
			UnityEngine.Sprite sprite = UnityEngine.Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0u,
				SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
			sprite.name = "Mctier Rounded " + radius;
			sprite.hideFlags = HideFlags.DontSave;
			s_roundedCache[radius] = sprite;
			return sprite;
		}

		/// <summary>
		/// A hollow, anti-aliased rounded rectangle: a 9-slice <paramref name="thickness"/>px stroke
		/// with a transparent middle. This is what gives cards, fields and buttons a real outline —
		/// a second filled rect behind the first one only tints it, it does not draw an edge.
		/// </summary>
		public static UnityEngine.Sprite RoundedRectOutline(int radius, int thickness)
		{
			if (radius < 1)
			{
				radius = 1;
			}
			if (thickness < 1)
			{
				thickness = 1;
			}
			long key = ((long)radius << 16) | (uint)thickness;
			UnityEngine.Sprite cached;
			if (s_outlineCache.TryGetValue(key, out cached) && cached != null)
			{
				return cached;
			}
			int border = radius + thickness;
			int size = border * 2 + 4;
			Texture2D texture = NewTexture(size, size, "Mctier Outline " + radius + "x" + thickness);
			Color32[] pixels = new Color32[size * size];
			float half = size * 0.5f;
			float inner = half - radius;
			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					float px = x + 0.5f - half;
					float py = y + 0.5f - half;
					float qx = Mathf.Abs(px) - inner;
					float qy = Mathf.Abs(py) - inner;
					float ax = Mathf.Max(qx, 0f);
					float ay = Mathf.Max(qy, 0f);
					// Signed distance to the rounded outline: negative inside the shape.
					float distance = Mathf.Sqrt(ax * ax + ay * ay) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
					// Keep only the band that sits just inside the edge.
					float band = Mathf.Min(-distance, distance + thickness);
					byte alpha = (byte)(Mathf.Clamp01(band + 0.5f) * 255f);
					pixels[y * size + x] = new Color32(255, 255, 255, alpha);
				}
			}
			texture.SetPixels32(pixels);
			texture.Apply(false, false);
			UnityEngine.Sprite sprite = UnityEngine.Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0u,
				SpriteMeshType.FullRect, new Vector4(border, border, border, border));
			sprite.name = "Mctier Outline " + radius + "x" + thickness;
			sprite.hideFlags = HideFlags.DontSave;
			s_outlineCache[key] = sprite;
			return sprite;
		}

		/// <summary>A filled white circle, for status dots.</summary>
		public static UnityEngine.Sprite Circle()
		{
			if (s_circle != null)
			{
				return s_circle;
			}
			const int size = 64;
			Texture2D texture = NewTexture(size, size, "Mctier Circle");
			Color32[] pixels = new Color32[size * size];
			float half = size * 0.5f;
			float radius = half - 1f;
			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					float dx = x + 0.5f - half;
					float dy = y + 0.5f - half;
					float distance = Mathf.Sqrt(dx * dx + dy * dy) - radius;
					byte alpha = (byte)(Mathf.Clamp01(0.5f - distance) * 255f);
					pixels[y * size + x] = new Color32(255, 255, 255, alpha);
				}
			}
			texture.SetPixels32(pixels);
			texture.Apply(false, false);
			s_circle = UnityEngine.Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
			s_circle.name = "Mctier Circle";
			s_circle.hideFlags = HideFlags.DontSave;
			return s_circle;
		}

		/// <summary>A hollow white circle, for "not connected" indicators.</summary>
		public static UnityEngine.Sprite Ring(int thickness)
		{
			if (thickness < 1)
			{
				thickness = 1;
			}
			UnityEngine.Sprite cached;
			if (s_ringCache.TryGetValue(thickness, out cached) && cached != null)
			{
				return cached;
			}
			const int size = 64;
			Texture2D texture = NewTexture(size, size, "Mctier Ring " + thickness);
			Color32[] pixels = new Color32[size * size];
			float half = size * 0.5f;
			float outer = half - 1f;
			float inner = outer - thickness;
			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					float dx = x + 0.5f - half;
					float dy = y + 0.5f - half;
					float distance = Mathf.Sqrt(dx * dx + dy * dy);
					float outerAlpha = Mathf.Clamp01(0.5f - (distance - outer));
					float innerAlpha = Mathf.Clamp01(0.5f - (inner - distance));
					byte alpha = (byte)(Mathf.Clamp01(Mathf.Min(outerAlpha, innerAlpha)) * 255f);
					pixels[y * size + x] = new Color32(255, 255, 255, alpha);
				}
			}
			texture.SetPixels32(pixels);
			texture.Apply(false, false);
			UnityEngine.Sprite sprite = UnityEngine.Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
			sprite.name = "Mctier Ring " + thickness;
			sprite.hideFlags = HideFlags.DontSave;
			s_ringCache[thickness] = sprite;
			return sprite;
		}

		public static Color WithAlpha(Color color, float alpha)
		{
			color.a = alpha;
			return color;
		}

		/// <summary>Drops every generated sprite and the cached font. Used by the editor audit.</summary>
		public static void ClearCache()
		{
			foreach (KeyValuePair<int, UnityEngine.Sprite> entry in s_roundedCache)
			{
				DestroySprite(entry.Value);
			}
			foreach (KeyValuePair<int, UnityEngine.Sprite> entry in s_ringCache)
			{
				DestroySprite(entry.Value);
			}
			foreach (KeyValuePair<long, UnityEngine.Sprite> entry in s_outlineCache)
			{
				DestroySprite(entry.Value);
			}
			s_roundedCache.Clear();
			s_ringCache.Clear();
			s_outlineCache.Clear();
			DestroySprite(s_circle);
			s_circle = null;
			DestroySprite(s_glassPanel);
			DestroySprite(s_glassButton);
			DestroySprite(s_glassInput);
			DestroySprite(s_glassHeader);
			DestroySprite(s_accentLine);
			s_glassPanel = null;
			s_glassButton = null;
			s_glassInput = null;
			s_glassHeader = null;
			s_accentLine = null;
			s_font = null;
		}

		private static UnityEngine.Sprite ProceduralGlass(string name, float fillAlpha, int border)
		{
			const int size = 64;
			const int cut = 6;
			Texture2D texture = NewTexture(size, size, "Division Glass " + name);
			Color32[] pixels = new Color32[size * size];
			for (int y = 0; y < size; y++)
			{
				float vertical = Mathf.Lerp(1.10f, 0.86f, y / (float)(size - 1));
				for (int x = 0; x < size; x++)
				{
					bool cutCorner = (x < cut && y < cut && x + y < cut) ||
						(x >= size - cut && y < cut && (size - 1 - x) + y < cut) ||
						(x < cut && y >= size - cut && x + (size - 1 - y) < cut) ||
						(x >= size - cut && y >= size - cut && (size - 1 - x) + (size - 1 - y) < cut);
					if (cutCorner)
					{
						pixels[y * size + x] = new Color32(255, 255, 255, 0);
						continue;
					}
					int edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(size - 1 - x, size - 1 - y));
					float edgeLift = edge <= 1 ? 1.12f : 1f;
					byte alpha = (byte)Mathf.Clamp(Mathf.RoundToInt(fillAlpha * vertical * edgeLift * 255f), 0, 255);
					pixels[y * size + x] = new Color32(255, 255, 255, alpha);
				}
			}
			texture.SetPixels32(pixels);
			texture.Apply(false, false);
			UnityEngine.Sprite sprite = UnityEngine.Sprite.Create(texture, new Rect(0f, 0f, size, size),
				new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect,
				new Vector4(border, border, border, border));
			sprite.name = "DivisionGlass Procedural " + name;
			sprite.hideFlags = HideFlags.DontSave;
			return sprite;
		}

		private static void DestroySprite(UnityEngine.Sprite sprite)
		{
			if (sprite == null)
			{
				return;
			}
			if (sprite.texture != null)
			{
				Object.DestroyImmediate(sprite.texture);
			}
			Object.DestroyImmediate(sprite);
		}

		private static Texture2D NewTexture(int width, int height, string name)
		{
			Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
			texture.name = name;
			texture.filterMode = FilterMode.Bilinear;
			texture.wrapMode = TextureWrapMode.Clamp;
			texture.hideFlags = HideFlags.DontSave;
			return texture;
		}

		/// <summary>"RRGGBB" or "RRGGBBAA" to a Color.</summary>
		private static Color Hex(string hex)
		{
			if (string.IsNullOrEmpty(hex))
			{
				return Color.white;
			}
			int r = System.Convert.ToInt32(hex.Substring(0, 2), 16);
			int g = System.Convert.ToInt32(hex.Substring(2, 2), 16);
			int b = System.Convert.ToInt32(hex.Substring(4, 2), 16);
			int a = hex.Length >= 8 ? System.Convert.ToInt32(hex.Substring(6, 2), 16) : 255;
			return new Color(r / 255f, g / 255f, b / 255f, a / 255f);
		}
	}
}
