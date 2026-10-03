// Temporary diagnostic used by the DSH Unity bridge. Safe to delete.
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public static class DshShaderAudit
{
	private static string Path(Transform t)
	{
		string s = t.name;
		Transform p = t.parent;
		while (p != null) { s = p.name + "/" + s; p = p.parent; }
		return s;
	}

	private static bool IsSceneObject(Component c)
	{
		GameObject go = c.gameObject;
		if (go.hideFlags != HideFlags.None) return false;
		if (!go.scene.IsValid()) return false;
		return true;
	}

	private static string ShaderState(Material m)
	{
		if (m == null) return "NULL_MATERIAL";
		if (m.shader == null) return "NULL_SHADER";
		string n = m.shader.name;
		if (n == "Hidden/InternalErrorShader" || n.Contains("InternalError")) return "ERROR_SHADER";
		if (!m.shader.isSupported) return "UNSUPPORTED_SHADER";
		return null;
	}

	/// <summary>Full audit of the loaded scene(s): broken materials, broken UI graphics, shader census.</summary>
	public static string Audit()
	{
		StringBuilder sb = new StringBuilder();
		int bad = 0;
		Dictionary<string, int> shaderUse = new Dictionary<string, int>();
		Dictionary<string, int> matUse = new Dictionary<string, int>();

		Renderer[] renderers = Resources.FindObjectsOfTypeAll<Renderer>();
		foreach (Renderer r in renderers)
		{
			if (r == null || !IsSceneObject(r)) continue;
			Material[] mats = r.sharedMaterials;
			if (mats == null || mats.Length == 0)
			{
				sb.AppendLine("NO_MATERIAL_SLOT  " + r.GetType().Name + "  " + Path(r.transform));
				bad++;
				continue;
			}
			for (int i = 0; i < mats.Length; i++)
			{
				string st = ShaderState(mats[i]);
				string matName = mats[i] == null ? "<null>" : mats[i].name;
				string shName = (mats[i] == null || mats[i].shader == null) ? "<null>" : mats[i].shader.name;
				matUse[matName] = matUse.TryGetValue(matName, out int mc) ? mc + 1 : 1;
				shaderUse[shName] = shaderUse.TryGetValue(shName, out int sc) ? sc + 1 : 1;
				if (st != null)
				{
					sb.AppendLine(st + "  " + r.GetType().Name + "  " + Path(r.transform) + "  slot=" + i + "  mat=" + matName + "  shader=" + shName);
					bad++;
				}
				else
				{
					// texture-less opaque materials on a renderer usually mean a white/blown-out sprite
					if (mats[i].HasProperty("_MainTex") && mats[i].GetTexture("_MainTex") == null &&
					    !(r is ParticleSystemRenderer) && !(r is LineRenderer) && !(r is TrailRenderer))
					{
						sb.AppendLine("NULL_MAINTEX  " + r.GetType().Name + "  " + Path(r.transform) + "  mat=" + matName + "  shader=" + shName);
						bad++;
					}
				}
			}
		}

		Graphic[] graphics = Resources.FindObjectsOfTypeAll<Graphic>();
		foreach (Graphic g in graphics)
		{
			if (g == null || !IsSceneObject(g)) continue;
			Material m = g.material;
			string st = ShaderState(m);
			string matName = m == null ? "<null>" : m.name;
			string shName = (m == null || m.shader == null) ? "<null>" : m.shader.name;
			matUse[matName] = matUse.TryGetValue(matName, out int mc2) ? mc2 + 1 : 1;
			shaderUse[shName] = shaderUse.TryGetValue(shName, out int sc2) ? sc2 + 1 : 1;
			if (st != null)
			{
				sb.AppendLine(st + "  UI:" + g.GetType().Name + "  " + Path(g.transform) + "  mat=" + matName + "  shader=" + shName);
				bad++;
				continue;
			}
			Image img = g as Image;
			if (img != null && img.sprite == null && img.enabled && img.color.a > 0.01f)
			{
				string extra = "";
				if (img.material != null && img.material.name == "Mask") extra = "  <-- Mask material with no sprite";
				sb.AppendLine("NULL_SPRITE  UI:Image  " + Path(g.transform) + "  color=" + img.color + extra);
				bad++;
			}
			RawImage raw = g as RawImage;
			if (raw != null && raw.texture == null && raw.enabled && raw.color.a > 0.01f)
			{
				sb.AppendLine("NULL_RAWTEX  UI:RawImage  " + Path(g.transform) + "  color=" + raw.color);
				bad++;
			}
		}

		// TextMeshPro components (not Graphics) with a broken material
		foreach (MonoBehaviour mb in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
		{
			if (mb == null || !IsSceneObject(mb)) continue;
			Type t = mb.GetType();
			if (t.FullName != "TMPro.TextMeshProUGUI" && t.FullName != "TMPro.TextMeshPro") continue;
			Renderer rend = mb.GetComponent<Renderer>();
			Material m = rend == null ? null : rend.sharedMaterial;
			string st = ShaderState(m);
			if (st != null)
			{
				sb.AppendLine(st + "  TMP:" + t.Name + "  " + Path(mb.transform) + "  mat=" + (m == null ? "<null>" : m.name));
				bad++;
			}
			else
			{
				string shName = m.shader.name;
				shaderUse[shName] = shaderUse.TryGetValue(shName, out int sc3) ? sc3 + 1 : 1;
			}
		}

		sb.AppendLine();
		sb.AppendLine("=== issues: " + bad + " ===");
		sb.AppendLine("=== shaders in use ===");
		List<KeyValuePair<string, int>> list = new List<KeyValuePair<string, int>>(shaderUse);
		list.Sort((x, y) => y.Value.CompareTo(x.Value));
		foreach (KeyValuePair<string, int> kv in list) sb.AppendLine("  " + kv.Value.ToString().PadLeft(5) + "  " + kv.Key);
		return sb.ToString();
	}

	/// <summary>UI graphics on a Canvas with their screen rectangles, so a screenshot can be matched to objects.</summary>
	public static string Rects()
	{
		StringBuilder sb = new StringBuilder();
		Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
		foreach (Canvas c in canvases)
		{
			if (c == null || !IsSceneObject(c)) continue;
			sb.AppendLine("CANVAS " + Path(c.transform) + "  renderMode=" + c.renderMode + "  sortingOrder=" + c.sortingOrder + "  scale=" + c.scaleFactor + "  active=" + c.gameObject.activeInHierarchy);
		}
		Graphic[] graphics = Resources.FindObjectsOfTypeAll<Graphic>();
		foreach (Graphic g in graphics)
		{
			if (g == null || !IsSceneObject(g)) continue;
			if (!g.gameObject.activeInHierarchy) continue;
			RectTransform rt = g.rectTransform;
			if (rt == null) continue;
			Vector3[] corners = new Vector3[4];
			rt.GetWorldCorners(corners);
			float x0 = corners[0].x, y0 = corners[0].y, x1 = corners[2].x, y1 = corners[2].y;
			if (x1 - x0 < 0.5f || y1 - y0 < 0.5f) continue;
			Material m = g.material;
			string shName = (m == null || m.shader == null) ? "<null>" : m.shader.name;
			string spName = "";
			Image img = g as Image;
			if (img != null) spName = " sprite=" + (img.sprite == null ? "<null>" : img.sprite.name);
			RawImage raw = g as RawImage;
			if (raw != null) spName = " tex=" + (raw.texture == null ? "<null>" : raw.texture.name);
			Text txt = g as Text;
			if (txt != null) spName = " text=\"" + (txt.text == null ? "" : txt.text.Replace("\n", "|")) + "\"";
			sb.AppendLine(string.Format("RECT [{0:F0},{1:F0}-{2:F0},{3:F0}] {4} {5} shader={6}{7}", x0, y0, x1, y1, g.GetType().Name, Path(g.transform), shName, spName));
		}
		return sb.ToString();
	}

	/// <summary>Every shader asset in the project that no loaded renderer/UI graphic uses.</summary>
	public static string UnusedShaders()
	{
		StringBuilder sb = new StringBuilder();
		HashSet<Shader> used = new HashSet<Shader>();
		foreach (Renderer r in Resources.FindObjectsOfTypeAll<Renderer>())
			if (r != null && r.sharedMaterials != null)
				foreach (Material m in r.sharedMaterials)
					if (m != null && m.shader != null) used.Add(m.shader);
		foreach (Graphic g in Resources.FindObjectsOfTypeAll<Graphic>())
			if (g != null && g.material != null && g.material.shader != null) used.Add(g.material.shader);

		string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Shader");
		foreach (string guid in guids)
		{
			string p = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
			Shader s = UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>(p);
			if (s == null) continue;
			if (!used.Contains(s)) sb.AppendLine("UNUSED  " + s.name + "  " + p);
		}
		return sb.ToString();
	}

	/// <summary>Blanks the e2d curve renderers' material the way the broken material asset used to leave them.</summary>
	public static string NullCurve()
	{
		StringBuilder sb = new StringBuilder();
		foreach (MeshRenderer r in Resources.FindObjectsOfTypeAll<MeshRenderer>())
		{
			if (r == null || !IsSceneObject(r)) continue;
			if (r.name != "_curve") continue;
			Material m = r.sharedMaterial;
			sb.AppendLine("was=" + (m == null ? "<null>" : m.name + "/" + (m.shader == null ? "<null>" : m.shader.name)));
			r.sharedMaterials = new Material[] { null };
		}
		return sb.ToString();
	}

	/// <summary>Turns the e2d terrain curve renderers on or off, to show what they contribute to the frame.</summary>
	public static string SetCurve(bool on)
	{
		StringBuilder sb = new StringBuilder();
		foreach (MeshRenderer r in Resources.FindObjectsOfTypeAll<MeshRenderer>())
		{
			if (r == null || !IsSceneObject(r)) continue;
			if (r.name != "_curve") continue;
			r.enabled = on;
			Material m = r.sharedMaterial;
			sb.AppendLine((on ? "on  " : "off ") + Path(r.transform) + "  mat=" + (m == null ? "<null>" : m.name)
				+ " shader=" + (m == null || m.shader == null ? "<null>" : m.shader.name));
		}
		return sb.ToString();
	}

	/// <summary>Assigns a bright stand-in material to every drawn mesh renderer that has none, and names it.</summary>
	public static string PaintNull()
	{
		Material m = new Material(Shader.Find("Unlit/Color"));
		m.color = new Color(1f, 0.45f, 0f, 1f);
		StringBuilder sb = new StringBuilder();
		foreach (MeshRenderer r in Resources.FindObjectsOfTypeAll<MeshRenderer>())
		{
			if (r == null || !IsSceneObject(r)) continue;
			if (!r.gameObject.activeInHierarchy || !r.enabled) continue;
			bool missing = r.sharedMaterials == null || r.sharedMaterials.Length == 0;
			if (!missing)
			{
				missing = true;
				foreach (Material sm in r.sharedMaterials) if (sm != null && sm.shader != null) { missing = false; break; }
			}
			if (!missing) continue;
			r.sharedMaterials = new Material[] { m };
			sb.AppendLine("painted " + Path(r.transform) + "  was=" + (r.sharedMaterials == null ? "null" : "slots"));
		}
		return sb.ToString();
	}

	/// <summary>Everything drawn over one screen pixel, smallest bounds first - material provenance included.</summary>
	public static string HitAt(float px, float py)
	{
		StringBuilder sb = new StringBuilder();
		sb.AppendLine("point screen(" + px + "," + py + ") of " + Screen.width + "x" + Screen.height);
		List<KeyValuePair<float, string>> hits = new List<KeyValuePair<float, string>>();
		foreach (Renderer r in Resources.FindObjectsOfTypeAll<Renderer>())
		{
			if (r == null || !IsSceneObject(r)) continue;
			if (!r.gameObject.activeInHierarchy || !r.enabled) continue;
			Material m = r.sharedMaterial;
			float x0 = 1e9f, y0 = 1e9f, x1 = -1e9f, y1 = -1e9f;
			bool any = false;
			foreach (Camera cam in Camera.allCameras)
			{
				if (!cam.enabled) continue;
				Bounds b = r.bounds;
				for (int i = 0; i < 8; i++)
				{
					Vector3 c = b.center + Vector3.Scale(b.extents, new Vector3(
						(i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
					Vector3 s = cam.WorldToScreenPoint(c);
					if (s.z < 0f) continue;
					any = true;
					if (s.x < x0) x0 = s.x; if (s.x > x1) x1 = s.x;
					if (s.y < y0) y0 = s.y; if (s.y > y1) y1 = s.y;
				}
			}
			if (!any) continue;
			if (px < x0 || px > x1 || py < y0 || py > y1) continue;
			string mn = m == null ? "<null>" : m.name;
			string sn = (m == null || m.shader == null) ? "<null>" : m.shader.name;
			string tex = "";
			if (m != null && m.HasProperty("_MainTex"))
			{
				Texture t = m.GetTexture("_MainTex");
				tex = " tex=" + (t == null ? "<null>" : t.name);
			}
			string extra = "";
			if (r is SpriteRenderer) extra += " sprite=" + (((SpriteRenderer)r).sprite == null ? "<null>" : ((SpriteRenderer)r).sprite.name);
			if (r is ParticleSystemRenderer)
			{
				ParticleSystemRenderer pr = (ParticleSystemRenderer)r;
				ParticleSystem ps = pr.GetComponent<ParticleSystem>();
				extra += " particles=" + (ps == null ? -1 : ps.particleCount) + " trail=" + (pr.trailMaterial == null ? "<null>" : pr.trailMaterial.name);
			}
			float vol = r.bounds.size.x * r.bounds.size.y * r.bounds.size.z;
			hits.Add(new KeyValuePair<float, string>(vol, string.Format("{0}  {1}  mat={2} shader={3}{4}{5}  screen[{6:F0},{7:F0}-{8:F0},{9:F0}]",
				r.GetType().Name, Path(r.transform), mn, sn, tex, extra, x0, y0, x1, y1)));
		}
		hits.Sort((a, b) => a.Key.CompareTo(b.Key));
		foreach (KeyValuePair<float, string> kv in hits) sb.AppendLine(kv.Value);
		return sb.ToString();
	}

	/// <summary>Every particle renderer in the loaded scenes, with material, shader and texture.</summary>
	public static string Particles()
	{
		StringBuilder sb = new StringBuilder();
		foreach (Renderer r in Resources.FindObjectsOfTypeAll<Renderer>())
		{
			if (r == null || !IsSceneObject(r)) continue;
			if (!(r is ParticleSystemRenderer) && !(r is TrailRenderer) && !(r is LineRenderer)) continue;
			Material m = r.sharedMaterial;
			string mn = m == null ? "<null>" : m.name;
			string sn = (m == null || m.shader == null) ? "<null>" : m.shader.name;
			string tex = "tex=";
			if (m != null && m.HasProperty("_MainTex"))
			{
				Texture t = m.GetTexture("_MainTex");
				tex += t == null ? "<null>" : t.name;
			}
			sb.AppendLine(string.Format("{0}  {1}  active={2}  mat={3} shader={4} {5}",
				r.GetType().Name, Path(r.transform), r.gameObject.activeInHierarchy, mn, sn, tex));
		}
		return sb.ToString();
	}

	/// <summary>Renderers whose projected screen rectangle touches a region, with material provenance.</summary>
	public static string Probe()
	{
		string[] unused = null;
		float rx0 = 470f, ry0 = 0f, rx1 = 641f, ry1 = 40f;
		StringBuilder sb = new StringBuilder();
		foreach (Camera cam in Camera.allCameras)
		{
			sb.AppendLine("CAM " + cam.name + " " + cam.pixelWidth + "x" + cam.pixelHeight
				+ " ortho=" + cam.orthographic + " size=" + cam.orthographicSize + " depth=" + cam.depth
				+ " enabled=" + cam.enabled + " culling=" + cam.cullingMask);
		}
		sb.AppendLine("region x[" + rx0 + ".." + rx1 + "] y[" + ry0 + ".." + ry1 + "]  screen=" + Screen.width + "x" + Screen.height);
		foreach (Renderer r in Resources.FindObjectsOfTypeAll<Renderer>())
		{
			if (r == null || !IsSceneObject(r)) continue;
			if (!r.gameObject.activeInHierarchy || !r.enabled) continue;
			Material m = r.sharedMaterial;
			float x0 = 1e9f, y0 = 1e9f, x1 = -1e9f, y1 = -1e9f;
			bool any = false;
			foreach (Camera cam in Camera.allCameras)
			{
				if (!cam.enabled) continue;
				Bounds b = r.bounds;
				for (int i = 0; i < 8; i++)
				{
					Vector3 c = b.center + Vector3.Scale(b.extents, new Vector3(
						(i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
					Vector3 s = cam.WorldToScreenPoint(c);
					if (s.z < 0f) continue;
					any = true;
					if (s.x < x0) x0 = s.x; if (s.x > x1) x1 = s.x;
					if (s.y < y0) y0 = s.y; if (s.y > y1) y1 = s.y;
				}
			}
			if (!any) continue;
			bool hit = x1 >= rx0 && x0 <= rx1 && y1 >= ry0 && y0 <= ry1;
			if (!hit) continue;
			string mn = m == null ? "<null>" : m.name;
			string sn = (m == null || m.shader == null) ? "<null>" : m.shader.name;
			string col = "";
			if (m != null && m.HasProperty("_Color")) col = " _Color=" + m.GetColor("_Color");
			sb.AppendLine(string.Format("{0}  {1}  {2}  screen[{3:F0},{4:F0}-{5:F0},{6:F0}]  mat={7} shader={8}{9}",
				r.GetType().Name, Path(r.transform), r.bounds.center, x0, y0, x1, y1, mn, sn, col));
		}
		return sb.ToString();
	}

	/// <summary>Reports which project shaders still have uncompiled variants (async compilation).</summary>
	public static string Pending()
	{
		StringBuilder sb = new StringBuilder();
		foreach (Shader s in Resources.FindObjectsOfTypeAll<Shader>())
		{
			sb.AppendLine(s.name);
		}
		return sb.ToString();
	}
}
