using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 编辑器外壳（阶段 0）：标签页 + 菜单栏 + 内容区 + 状态栏的空壳窗口。
// 后续阶段会把 app/* 的菜单、层级树、属性面板、对话框、存档查看逐一填进来。
public class BpEditorWindow : MonoBehaviour
{
	private static BpEditorWindow s_instance;
	public static bool IsOpen => s_instance != null;

	private GameObject m_canvasGo;
	private RectTransform m_content;
	private Text m_status;
	private GameObject m_menuPopup;
	private BpFileDialogs m_dialogs;
	private BpLevelData m_lastLevel;
	private readonly List<string> m_tabs = new List<string>();
	private int m_activeTab;

	public static void Open()
	{
		if (s_instance != null) { Innovation.BP.Feedback("BP> 编辑器已打开"); return; }
		GameObject go = new GameObject("_bp_editor_window");
		Object.DontDestroyOnLoad(go);
		s_instance = go.AddComponent<BpEditorWindow>();
		s_instance.Build();
	}

	public static void Close()
	{
		if (s_instance == null) return;
		s_instance.Teardown();
	}

	private void Build()
	{
		BpPaths.EnsureDirs();

		m_canvasGo = new GameObject("BpEditorCanvas");
		m_canvasGo.transform.SetParent(transform, false);
		Canvas canvas = m_canvasGo.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 30000;
		CanvasScaler scaler = m_canvasGo.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920f, 1080f);
		scaler.matchWidthOrHeight = 0.5f;
		m_canvasGo.AddComponent<GraphicRaycaster>();
		m_dialogs = new BpFileDialogs(m_canvasGo.GetComponent<RectTransform>());

		// 背景
		GameObject bg = BpUi.Node("Background", m_canvasGo.transform);
		BpUi.Stretch(bg);
		bg.AddComponent<UnityEngine.UI.Image>().color = new Color(0.09f, 0.10f, 0.13f, 1f);
		bg.transform.SetAsFirstSibling();

		GameObject colGo = BpUi.Node("Root", m_canvasGo.transform);
		RectTransform col = BpUi.Stretch(colGo);
		VerticalLayoutGroup v = colGo.AddComponent<VerticalLayoutGroup>();
		v.spacing = 0f;
		v.childAlignment = TextAnchor.UpperLeft;
		v.childForceExpandWidth = true;
		v.childForceExpandHeight = false;
		v.childControlWidth = true;
		v.childControlHeight = true;

		BuildMenuBar(col);
		BuildTabBar(col);

		// 内容区（可伸缩）
		GameObject contentGo = BpUi.Node("Content", col);
		LayoutElement cle = contentGo.AddComponent<LayoutElement>();
		cle.flexibleHeight = 1f;
		m_content = BpUi.Stretch(contentGo);
		VerticalLayoutGroup contentLayout = contentGo.AddComponent<VerticalLayoutGroup>();
		contentLayout.spacing = 2f;
		contentLayout.childAlignment = TextAnchor.UpperLeft;
		contentLayout.childForceExpandWidth = true;
		contentLayout.childForceExpandHeight = false;
		contentLayout.childControlWidth = true;
		contentLayout.childControlHeight = true;
		GameObject contentBg = BpUi.Node("ContentBg", m_content);
		BpUi.Stretch(contentBg);
		contentBg.AddComponent<UnityEngine.UI.Image>().color = new Color(0.12f, 0.13f, 0.17f, 1f);
		contentBg.AddComponent<LayoutElement>().ignoreLayout = true;
		BpUi.Label(m_content, "BP 编辑器 — 模块状态（对照 badpiggies-editor 逐模块 1:1 移植）", 22, TextAnchor.UpperLeft, 34f);
		BuildModuleStatus(m_content);
		BpUi.Label(m_content, "文件根目录: " + BpPaths.Root, 18, TextAnchor.UpperLeft, 26f);

		// 状态栏
		GameObject statusGo = BpUi.Node("Status", col);
		LayoutElement sle = statusGo.AddComponent<LayoutElement>();
		sle.minHeight = 28f; sle.preferredHeight = 28f;
		UnityEngine.UI.Image sbg = statusGo.AddComponent<UnityEngine.UI.Image>();
		sbg.color = new Color(0.14f, 0.16f, 0.20f, 1f);
		m_status = BpUi.Label(statusGo.transform, "就绪", 16, TextAnchor.MiddleLeft, 28f);

		m_tabs.Add("Level");
		m_activeTab = 0;
		RefreshTabs();
		SetStatus("根目录: " + BpPaths.Root);

		StartCoroutine(BpPaths.SyncStreamingAssets());
	}

	private void BuildMenuBar(RectTransform parent)
	{
		GameObject bar = BpUi.Node("MenuBar", parent);
		LayoutElement le = bar.AddComponent<LayoutElement>();
		le.minHeight = 34f; le.preferredHeight = 34f;
		bar.AddComponent<UnityEngine.UI.Image>().color = new Color(0.16f, 0.18f, 0.22f, 1f);
		RectTransform row = BpUi.Horizontal(bar.transform, 2f, new RectOffset(6, 6, 2, 2), "MenuRow", 34f);

		Menu(row, "File", new[] { "Open Level...", "Save", "Save As...", "Exit" });
		Menu(row, "Edit", new[] { "Undo", "Redo", "Copy", "Paste", "Duplicate", "Delete" });
		Menu(row, "View", new[] { "Background", "Grid", "Wireframe", "Physics Ground" });
		Menu(row, "Tool", new[] { "Select", "Pan", "Draw Terrain", "Box Select", "Terrain Preset" });
		Menu(row, "Help", new[] { "Shortcuts", "About" });

		// 常驻「关闭」按钮（菜单栏右端）
		BpUi.Button(row, "✕ 关闭", Close, 30f, new Color(0.55f, 0.24f, 0.24f, 0.98f));
	}

	private void Menu(RectTransform row, string title, string[] items)
	{
		BpUi.Button(row, title, () => ShowMenuPopup(title, items), 30f, new Color(0.16f, 0.18f, 0.22f, 1f));
	}

	private void ShowMenuPopup(string title, string[] items)
	{
		HideMenuPopup();
		m_menuPopup = BpUi.Node("MenuPopup", m_canvasGo.transform);
		RectTransform rt = m_menuPopup.AddComponent<RectTransform>();
		rt.anchorMin = new Vector2(0f, 1f);
		rt.anchorMax = new Vector2(0f, 1f);
		rt.pivot = new Vector2(0f, 1f);
		rt.anchoredPosition = new Vector2(8f, -36f);
		rt.sizeDelta = new Vector2(240f, 34f * items.Length + 8f);
		m_menuPopup.AddComponent<UnityEngine.UI.Image>().color = new Color(0.13f, 0.15f, 0.19f, 0.99f);
		RectTransform col = BpUi.VerticalStretch(m_menuPopup.transform, 2f, new RectOffset(4, 4, 4, 4), "Items");
		foreach (string it in items)
		{
			string item = it;
			BpUi.Button(col, item, () =>
			{
				HideMenuPopup();
				OnMenuCommand(title, item);
			}, 30f, new Color(0.16f, 0.18f, 0.22f, 0.0f));
		}
	}

	private void HideMenuPopup()
	{
		if (m_menuPopup != null) Object.Destroy(m_menuPopup);
		m_menuPopup = null;
	}

	private void OnMenuCommand(string menu, string item)
	{
		if (menu == "File" && item == "Open Level...")
		{
			OpenLevelDialog();
			return;
		}
		SetStatus("[" + menu + "] " + item + "  （待后续阶段实现）");
		Debug.Log("[BPEditor] menu " + menu + " > " + item);
	}

	// ---------------- 打开关卡文件（安卓可用） ----------------

	private void OpenLevelDialog()
	{
		m_dialogs.ShowOpenLevel("打开关卡文件", "", path =>
		{
			BpLevelFile.Info info = BpLevelFile.Inspect(path);
			string body = FormatFileInfo(info);
			string status;
			if (info.exists && info.extension == ".bytes")
			{
				byte[] data = BpLevelFile.ReadBytes(path, 0);
				BpLevelData level = null;
				string err = null;
				if (data != null && BpParser.TryParseLevel(data, out level, out err))
				{
					m_lastLevel = level;
					body += "\n" + FormatLevelSummary(level, data.Length);
					byte[] re = BpParser.SerializeLevel(level);
					bool same = re != null && re.Length == data.Length && BytesEqual(re, data);
					body += "\n往返字节一致: " + (same ? "是" : ("否 (原 " + data.Length + " / 重 " + (re != null ? re.Length : 0) + ")"))
						+ "\n" + FormatOverrideProbe(level);
					status = "已解析: " + info.levelKey + "  对象 " + level.objects.Count + "  根 " + level.roots.Count;
				}
				else
				{
					body += "\n\n[解析失败] " + (err ?? "读取失败");
					status = "解析失败: " + info.levelKey;
				}
			}
			else
			{
				status = string.IsNullOrEmpty(info.error)
					? ("已打开: " + info.levelKey + "  (" + info.size + " bytes)")
					: ("打开失败: " + info.error);
			}
			m_dialogs.ShowInfo("文件信息", body, null);
			SetStatus(status);
		}, null);
	}

	private static bool BytesEqual(byte[] a, byte[] b)
	{
		if (a == null || b == null || a.Length != b.Length) return false;
		for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
		return true;
	}

	private static string FormatLevelSummary(BpLevelData level, int byteLength)
	{
		int prefabs = 0, parents = 0, terrainCount = 0, overrideCount = 0, totalNodes = 0;
		int minIdx = int.MaxValue, maxIdx = int.MinValue;
		Dictionary<string, int> nameCounts = new Dictionary<string, int>();
		foreach (BpLevelObject o in level.objects)
		{
			if (o.isPrefab)
			{
				prefabs++;
				BpPrefabInstance p = o.prefab;
				if (p.dataType == BpDataType.Terrain && p.terrainData != null)
				{
					terrainCount++;
					if (p.terrainData.curveMesh.vertices != null)
						totalNodes += p.terrainData.curveMesh.vertices.Count / 2;
				}
				if (p.dataType == BpDataType.PrefabOverrides) overrideCount++;
				if (p.prefabIndex < minIdx) minIdx = p.prefabIndex;
				if (p.prefabIndex > maxIdx) maxIdx = p.prefabIndex;
			}
			else parents++;
			string n = o.Name();
			int c;
			nameCounts[n] = nameCounts.TryGetValue(n, out c) ? c + 1 : 1;
		}

		System.Text.StringBuilder sb = new System.Text.StringBuilder();
		sb.Append("\n=== 关卡解析结果 (.bytes) ===\n");
		sb.Append("字节数: ").Append(byteLength).Append('\n');
		sb.Append("对象总数: ").Append(level.objects.Count)
		  .Append("  (Prefab ").Append(prefabs).Append(" / Parent ").Append(parents).Append(")\n");
		sb.Append("根对象: ").Append(level.roots.Count).Append('\n');
		sb.Append("带地形对象: ").Append(terrainCount).Append("   地形曲线节点合计: ").Append(totalNodes).Append('\n');
		sb.Append("带 Override 对象: ").Append(overrideCount).Append('\n');
		if (prefabs > 0) sb.Append("prefab_index 范围: ").Append(minIdx).Append(" ~ ").Append(maxIdx).Append('\n');

		List<KeyValuePair<string, int>> sorted = new List<KeyValuePair<string, int>>(nameCounts);
		sorted.Sort((x, y) => y.Value != x.Value ? y.Value.CompareTo(x.Value) : string.CompareOrdinal(x.Key, y.Key));
		sb.Append("\n名称 Top10:\n");
		for (int i = 0; i < sorted.Count && i < 10; i++)
			sb.Append("  ").Append(sorted[i].Value).Append(" × ").Append(sorted[i].Key).Append('\n');

		List<BpLevelWarning> warnings = BpLevelWarnings.CollectLevelWarnings(level);
		sb.Append("\n风险检查: ").Append(warnings.Count).Append(" 项\n");
		foreach (BpLevelWarning w in warnings)
			sb.Append("  [").Append(w.Severity()).Append("] ").Append(w.MessageKey())
			  .Append("  ").Append(w.objectName).Append(" ×").Append(w.count).Append('\n');
		return sb.ToString();
	}

	// 用 BpObjectDeserializer（walker）真正驱动一次 BpScene：
	// 对每个 PrefabOverrides 对象做 FromOverrideText，统计合成出的 GameObject/组件/字段数。
	private static string FormatOverrideProbe(BpLevelData level)
	{
		int blocks = 0, parsed = 0, failed = 0, goTotal = 0, compTotal = 0, fieldTotal = 0;
		int typedTotal = 0, unknownTotal = 0;
		foreach (BpLevelObject o in level.objects)
		{
			if (!o.isPrefab) continue;
			BpPrefabInstance p = o.prefab;
			if (p == null || p.dataType != BpDataType.PrefabOverrides || p.overrideData == null) continue;
			blocks++;
			try
			{
				int root;
				BpScene scene = BpScene.FromOverrideText(p.overrideData.rawText, out root);
				if (scene == null) { failed++; continue; }
				parsed++;
				goTotal += scene.GameObjectCount;
				compTotal += scene.ComponentCount;
				fieldTotal += scene.FieldCount;
				for (int i = 0; i < scene.ComponentCount; i++)
				{
					if (scene.Behavior(i) is BpUnknownComponent) unknownTotal++;
					else typedTotal++;
				}
			}
			catch (System.Exception)
			{
				failed++;
			}
		}

		System.Text.StringBuilder sb = new System.Text.StringBuilder();
		sb.Append("\n=== Override 反序列化（BpObjectDeserializer → BpScene）===\n");
		sb.Append("PrefabOverrides 块: ").Append(blocks)
		  .Append("   解析成功: ").Append(parsed)
		  .Append("   失败: ").Append(failed).Append('\n');
		if (parsed > 0)
			sb.Append("合成 GameObject 合计: ").Append(goTotal)
			  .Append("   组件合计: ").Append(compTotal)
			  .Append("   字段合计: ").Append(fieldTotal).Append('\n')
			  .Append("类型化组件: ").Append(typedTotal)
			  .Append("   未知组件(回退 Unknown): ").Append(unknownTotal).Append('\n');
		return sb.ToString();
	}

	private static string FormatFileInfo(BpLevelFile.Info info)
	{
		System.Text.StringBuilder sb = new System.Text.StringBuilder();
		sb.Append("路径: ").Append(info.path).Append('\n');
		sb.Append("存在: ").Append(info.exists ? "是" : "否").Append('\n');
		if (!string.IsNullOrEmpty(info.error)) sb.Append("错误: ").Append(info.error).Append('\n');
		sb.Append("大小: ").Append(info.size).Append(" 字节\n");
		sb.Append("扩展名: ").Append(info.extension).Append('\n');
		sb.Append("level_key: ").Append(info.levelKey).Append('\n');
		sb.Append("首字节(hex): ").Append(info.firstBytesHex).Append('\n');
		if (!string.IsNullOrEmpty(info.textPreview))
			sb.Append("\n--- 文本前几行 ---\n").Append(info.textPreview).Append('\n');
		return sb.ToString();
	}

	private void BuildTabBar(RectTransform parent)
	{
		GameObject bar = BpUi.Node("TabBar", parent);
		LayoutElement le = bar.AddComponent<LayoutElement>();
		le.minHeight = 32f; le.preferredHeight = 32f;
		bar.AddComponent<UnityEngine.UI.Image>().color = new Color(0.13f, 0.15f, 0.19f, 1f);
		BpUi.Horizontal(bar.transform, 2f, new RectOffset(6, 6, 2, 2), "TabRow", 32f);
	}

	private void RefreshTabs()
	{
		// 阶段 0：仅显示当前标签名；后续阶段渲染多标签
		SetStatus("标签: " + (m_tabs.Count > 0 ? m_tabs[m_activeTab] : "-"));
	}

	// 实时模块状态：对照参考工具逐模块显示进度（✓=该模块已存在）
	private void BuildModuleStatus(RectTransform parent)
	{
		string[][] rows =
		{
			new[] { "阶段0", "BpPaths", "路径适配（安卓 persistentDataPath + StreamingAssets 同步）" },
			new[] { "阶段0", "BpUi", "egui→uGUI 兼容层" },
			new[] { "阶段0", "BpEditorWindow", "编辑器外壳（菜单/标签/状态栏）" },
			new[] { "阶段1", "BpLevelData", "domain/types.rs 关卡/对象数据模型" },
			new[] { "阶段1", "TTerrData", "domain/types.rs TerrainData" },
			new[] { "阶段1", "TerrainGen", "domain/terrain_gen/*（strip/fill/control png/regenerate）" },
			new[] { "阶段1", "BpLevelOps", "domain/level/ops.rs（增删/移动/子树复制粘贴）" },
			new[] { "阶段1", "BpPrefabOverride", "domain/prefab_override.rs" },
			new[] { "阶段1", "BpPrefabAssetDocument", "domain/prefab_asset.rs" },
			new[] { "阶段1", "BpLevelWarnings", "domain/level_warning.rs" },
			new[] { "阶段2", "BpParser", "domain/parser.rs（.bytes 关卡二进制读写）" },
			new[] { "阶段2", "BpObjectDeserializer", "domain/object_deserializer.rs（override 反序列化 walker）" },
			new[] { "阶段2", "BpScene", "unity_runtime/*（Scene + RuntimeHost 接入 + 18 个类型化组件 + UnknownComponent 兜底）" },
			new[] { "阶段3", "BpUnityPackageLoader", "data/assets/unitypackage_loader.rs（unitypackage 索引，tar 按需读取）" },
			new[] { "阶段3", "BpAtlasMaterials", "data/assets/atlas_materials.rs（材质 guid → 图集）" },
			new[] { "阶段3", "BpLevelDb", "data/level_db.rs（.contraption SHA1 → 关卡名 / 关卡显示名）" },
			new[] { "阶段3", "BpLevelRefs", "domain/level/refs.rs（关卡引用库：loader/refs/prefab 名/背景）" },
			new[] { "阶段3", "BpMaterialRefs", "domain/level/refs.rs（材质/着色器/纹理 guid 辅助）" },
			new[] { "阶段3", "BpAssets", "data/assets/mod.rs（材质→特效贴图/地形贴图键，不含 theme）" },
			new[] { "阶段3", "BpSpriteDb", "data/sprite_db/*（prefab→图集 UV/尺寸，sprites.bytes/runtime 表）" },
			new[] { "阶段3", "BpTerrainAssets", "data/assets/terrain.rs（地形名→fill/splat0/splat1 贴图）" },
			new[] { "阶段3", "BpTextureCache", "data/assets/texture_cache.rs（贴图缓存 → Unity Texture2D）" },
			new[] { "阶段3", "BpBgThemeNames", "data/assets/theme.rs（背景主题命名与检测，不含取色）" },
			new[] { "阶段3", "BpBgPrefabParse", "data/bg_data/{types,tables,parse}.rs（BgLayer/BgSprite/BgTheme + prefab 解析）" },
			new[] { "阶段3", "BpBgThemeBuilder", "data/bg_data/theme.rs（背景 prefab → BgTheme 遍历构建）" },
			new[] { "阶段3", "BpBgOverridesOps", "data/bg_data/overrides.rs（override 文本/PositionSerializer → 组位置）" },
			new[] { "阶段4", "BpSceneRenderer", "renderer/*（待做）" },
			new[] { "阶段5", "BpAppShell", "app/*（待做）" },
			new[] { "阶段6", "BpTerrainEdit", "renderer/input/*（待做）" }
		};

		int done = 0;
		foreach (string[] r in rows)
		{
			bool exists = TypeExists(r[1]);
			if (exists) done++;
			BpUi.Label(parent,
				(exists ? "✓" : "－") + " [" + r[0] + "] " + r[1] + "  —  " + r[2],
				18, TextAnchor.MiddleLeft, 24f);
		}
		BpUi.Label(parent, "已移植 " + done + "/" + rows.Length + " 个模块（✓=已存在，－=待做）", 19, TextAnchor.MiddleLeft, 28f);
	}

	private static bool TypeExists(string name)
	{
		foreach (System.Type t in typeof(BpEditorWindow).Assembly.GetTypes())
			if (t.Name == name) return true;
		return false;
	}

	private void SetStatus(string text)
	{
		if (m_status != null) m_status.text = text;
	}

	private void Teardown()
	{
		if (m_canvasGo != null) Object.Destroy(m_canvasGo);
		m_canvasGo = null;
		m_content = null;
		m_status = null;
		m_menuPopup = null;
		if (s_instance == this) s_instance = null;
		Destroy(gameObject);
	}

	private void Update()
	{
		if (m_menuPopup == null) return;
		if (Input.GetKeyDown(KeyCode.Escape)) { HideMenuPopup(); return; }
		if (Input.GetMouseButtonDown(0))
		{
			RectTransform rt = m_menuPopup.GetComponent<RectTransform>();
			bool inside = rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, Input.mousePosition, null);
			if (!inside) HideMenuPopup();
		}
	}
}
