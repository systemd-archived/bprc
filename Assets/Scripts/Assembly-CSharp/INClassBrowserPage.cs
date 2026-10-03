using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UButton = UnityEngine.UI.Button;

// 类目录页面（dnSpy 风格）：程序集 → 命名空间 → 类。
// 点程序集/命名空间 = 展开/折叠；点“类” = 从**代码集**（应用数据目录 Settings/codesets）里查找并载入“代码编辑器”。
// 顶部有搜索框（按类名过滤，跨全部游戏程序集）；列表支持拖动 + 滚轮滚动。
// 仅包含游戏相关程序集（Assembly-CSharp、Innovation.*）。
// 渲染采用“扁平化 + 窗口化行池”，只创建固定数量的行对象，避免大量 UI 物体导致掉帧。
public class INClassBrowserPage : MonoBehaviour
{
	public const string PageName = "ClassBrowserInterface";
	public const string LocaleId = "AppInterface_ClassBrowser";

	public static INClassBrowserPage Instance { get; private set; }

	private class Node
	{
		public string Name;       // 显示名（搜索结果为 命名空间.类名）
		public string Key;        // 真实键（程序集名 / 命名空间 / 简单类名）
		public int Depth;         // 0=程序集 1=命名空间 2=类
		public Assembly Assembly;
		public List<Node> Children; // 懒加载
		public bool Expanded;
	}

	private const int RowCount = 16;
	private const float RowHeight = 34f;
	private const string GlobalNsDisplay = "(全局)";

	private Font m_font;

	private readonly List<Node> m_roots = new List<Node>();
	private readonly List<Node> m_flat = new List<Node>();
	private readonly List<Row> m_rows = new List<Row>();
	private readonly Dictionary<string, List<string>> m_classCache = new Dictionary<string, List<string>>();

	private int m_offset;
	private Text m_indicator;
	private Text m_title;
	private Text m_status;
	private InputField m_search;
	private string m_searchQuery = "";
	private List<Node> m_allClassNodes;

	private class Row
	{
		public GameObject Root;
		public Text Arrow;
		public Text Name;
	}

	private void Awake()
	{
		Instance = this;
		BuildUI();
	}

	private void OnDestroy()
	{
		if (Instance == this)
		{
			Instance = null;
		}
	}

	private void BuildUI()
	{
		if (m_rows.Count > 0)
		{
			return;
		}
		m_font = INUiUtil.UiFont;

		Image bg = NewImage(transform, "Bg", new Color(0.06f, 0.07f, 0.09f, 0.98f));
		Stretch(bg.rectTransform, 0f, 0f, 0f, 0f);

		// 标题
		m_title = NewText(transform, "Title", 22, TextAnchor.MiddleLeft, Color.white);
		AnchorRow(m_title.rectTransform, 0.01f, 0.99f, 6f, 30f);
		m_title.text = "类目录 · 游戏程序集 — 点程序集/命名空间展开；点“类”从代码集载入编辑器（拖拽/滚轮滚动）";

		// 搜索框 + 翻页
		m_search = NewInput(transform, "Search", false);
		AnchorRow((RectTransform)m_search.transform, 0.010f, 0.480f, 40f, 30f);
		m_search.onValueChanged.AddListener(OnSearchChanged);

		m_indicator = NewText(transform, "Indicator", 18, TextAnchor.MiddleLeft, new Color(0.8f, 0.85f, 0.9f, 1f));
		AnchorRow(m_indicator.rectTransform, 0.490f, 0.720f, 40f, 30f);

		Btn("上翻", 0.730f, 0.860f, 40f, 30f, OnPageUp);
		Btn("下翻", 0.870f, 0.990f, 40f, 30f, OnPageDown);

		// 底部状态行
		m_status = NewText(transform, "Status", 18, TextAnchor.MiddleLeft, new Color(0.85f, 0.9f, 0.75f, 1f));
		RectTransform srt = m_status.rectTransform;
		srt.anchorMin = new Vector2(0f, 0f);
		srt.anchorMax = new Vector2(1f, 0f);
		srt.pivot = new Vector2(0.5f, 0f);
		srt.offsetMin = new Vector2(18f, 4f);
		srt.offsetMax = new Vector2(-18f, 32f);
		m_status.horizontalOverflow = HorizontalWrapMode.Overflow;
		m_status.text = "就绪";

		// 行区域（可拖动/滚轮滚动）
		GameObject listGo = new GameObject("List", typeof(RectTransform));
		RectTransform lrt = (RectTransform)listGo.transform;
		lrt.SetParent(transform, false);
		lrt.anchorMin = new Vector2(0f, 0f);
		lrt.anchorMax = new Vector2(1f, 1f);
		lrt.pivot = new Vector2(0.5f, 1f);
		lrt.offsetMin = new Vector2(18f, 40f);
		lrt.offsetMax = new Vector2(-18f, -76f);
		listGo.AddComponent<RectMask2D>();
		INScrollArea scroll = listGo.AddComponent<INScrollArea>();
		scroll.PixelsPerStep = 26f;
		scroll.OnStep = OnListStep;

		for (int i = 0; i < RowCount; i++)
		{
			m_rows.Add(CreateRow(lrt, i));
		}

		BuildTree();
		Refresh();
	}

	private Row CreateRow(Transform parent, int index)
	{
		Row row = new Row();

		GameObject go = new GameObject("Row" + index, typeof(RectTransform));
		RectTransform rt = go.GetComponent<RectTransform>();
		rt.SetParent(parent, false);
		rt.anchorMin = new Vector2(0f, 1f);
		rt.anchorMax = new Vector2(1f, 1f);
		rt.pivot = new Vector2(0.5f, 1f);
		rt.offsetMin = new Vector2(0f, 0f);
		rt.offsetMax = new Vector2(0f, 0f);
		rt.sizeDelta = new Vector2(0f, RowHeight - 4f);
		rt.anchoredPosition = new Vector2(0f, -index * RowHeight);

		Image bg = go.AddComponent<Image>();
		bg.color = (index % 2 == 0) ? new Color(1f, 1f, 1f, 0.05f) : new Color(1f, 1f, 1f, 0.02f);
		UButton btn = go.AddComponent<UButton>();
		btn.targetGraphic = bg;
		int captured = index;
		btn.onClick.AddListener(delegate { OnRowClicked(captured); });
		row.Root = go;

		Text arrow = NewText(go.transform, "Arrow", 20, TextAnchor.MiddleCenter, new Color(0.85f, 0.9f, 1f, 1f));
		RectTransform art = arrow.rectTransform;
		art.anchorMin = new Vector2(0f, 0f);
		art.anchorMax = new Vector2(0f, 1f);
		art.pivot = new Vector2(0f, 0.5f);
		art.sizeDelta = new Vector2(22f, 0f);
		art.anchoredPosition = new Vector2(8f, 0f);
		row.Arrow = arrow;

		Text name = NewText(go.transform, "Name", 20, TextAnchor.MiddleLeft, Color.white);
		RectTransform nrt = name.rectTransform;
		nrt.anchorMin = new Vector2(0f, 0f);
		nrt.anchorMax = new Vector2(1f, 1f);
		nrt.pivot = new Vector2(0f, 0.5f);
		nrt.offsetMin = new Vector2(34f, 0f);
		nrt.offsetMax = new Vector2(-8f, 0f);
		name.horizontalOverflow = HorizontalWrapMode.Overflow;
		row.Name = name;

		return row;
	}

	// ===== 数据 =====

	private static List<Assembly> GetGameAssemblies()
	{
		List<Assembly> list = new List<Assembly>();
		HashSet<string> seen = new HashSet<string>();
		try
		{
			Assembly cs = typeof(BasePart).Assembly; // Assembly-CSharp
			if (cs != null && seen.Add(cs.GetName().Name))
			{
				list.Add(cs);
			}
		}
		catch
		{
		}
		try
		{
			foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
			{
				string n = asm.GetName().Name;
				if (n.StartsWith("Innovation", StringComparison.Ordinal) && seen.Add(n))
				{
					list.Add(asm);
				}
			}
		}
		catch
		{
		}
		list.Sort((a, b) => string.CompareOrdinal(a.GetName().Name, b.GetName().Name));
		return list;
	}

	private static Type[] SafeGetTypes(Assembly asm)
	{
		try
		{
			return asm.GetTypes();
		}
		catch (ReflectionTypeLoadException e)
		{
			List<Type> list = new List<Type>();
			if (e.Types != null)
			{
				foreach (Type t in e.Types)
				{
					if (t != null)
					{
						list.Add(t);
					}
				}
			}
			return list.ToArray();
		}
		catch
		{
			return new Type[0];
		}
	}

	private static bool IsUsableClass(Type t)
	{
		if (t == null || t.IsNested)
		{
			return false;
		}
		string n = t.Name;
		return !string.IsNullOrEmpty(n) && n[0] != '<';
	}

	private void BuildTree()
	{
		m_roots.Clear();
		foreach (Assembly asm in GetGameAssemblies())
		{
			Node asmNode = new Node();
			asmNode.Name = asm.GetName().Name;
			asmNode.Key = asm.GetName().Name;
			asmNode.Depth = 0;
			asmNode.Assembly = asm;
			m_roots.Add(asmNode);
		}
		m_flat.Clear();
		m_offset = 0;
	}

	private List<Node> GetChildren(Node node)
	{
		if (node.Children != null)
		{
			return node.Children;
		}
		List<Node> children = new List<Node>();
		if (node.Depth == 0)
		{
			HashSet<string> nss = new HashSet<string>();
			foreach (Type t in SafeGetTypes(node.Assembly))
			{
				if (!IsUsableClass(t))
				{
					continue;
				}
				nss.Add(string.IsNullOrEmpty(t.Namespace) ? string.Empty : t.Namespace);
			}
			List<string> sorted = new List<string>(nss);
			sorted.Sort(StringComparer.Ordinal);
			foreach (string ns in sorted)
			{
				Node n = new Node();
				n.Name = string.IsNullOrEmpty(ns) ? GlobalNsDisplay : ns;
				n.Key = ns;
				n.Depth = 1;
				n.Assembly = node.Assembly;
				children.Add(n);
			}
		}
		else if (node.Depth == 1)
		{
			foreach (string cls in GetClasses(node.Assembly, node.Key))
			{
				Node n = new Node();
				n.Name = cls;
				n.Key = cls;
				n.Depth = 2;
				n.Assembly = node.Assembly;
				children.Add(n);
			}
		}
		node.Children = children;
		return children;
	}

	private List<string> GetClasses(Assembly asm, string ns)
	{
		string key = asm.GetName().Name + "\u0000" + (ns ?? string.Empty);
		if (m_classCache.TryGetValue(key, out var cached))
		{
			return cached;
		}
		List<string> list = new List<string>();
		foreach (Type t in SafeGetTypes(asm))
		{
			if (!IsUsableClass(t))
			{
				continue;
			}
			string tns = string.IsNullOrEmpty(t.Namespace) ? string.Empty : t.Namespace;
			if (tns != (ns ?? string.Empty))
			{
				continue;
			}
			list.Add(t.Name);
		}
		list.Sort(StringComparer.Ordinal);
		List<string> uniq = new List<string>(list.Count);
		string prev = null;
		foreach (string s in list)
		{
			if (s != prev)
			{
				uniq.Add(s);
			}
			prev = s;
		}
		m_classCache[key] = uniq;
		return uniq;
	}

	private List<Node> GetAllClassNodes()
	{
		if (m_allClassNodes != null)
		{
			return m_allClassNodes;
		}
		List<Node> list = new List<Node>();
		foreach (Assembly asm in GetGameAssemblies())
		{
			foreach (Type t in SafeGetTypes(asm))
			{
				if (!IsUsableClass(t))
				{
					continue;
				}
				Node n = new Node();
				n.Name = string.IsNullOrEmpty(t.Namespace) ? t.Name : (t.Namespace + "." + t.Name);
				n.Key = t.Name;
				n.Depth = 2;
				n.Assembly = asm;
				list.Add(n);
			}
		}
		list.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
		m_allClassNodes = list;
		return list;
	}

	// ===== 展开/折叠 + 搜索 + 扁平化 =====

	private void Flatten()
	{
		m_flat.Clear();
		if (!string.IsNullOrEmpty(m_searchQuery))
		{
			foreach (Node n in GetAllClassNodes())
			{
				if (n.Name.IndexOf(m_searchQuery, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					m_flat.Add(n);
					if (m_flat.Count >= 4000)
					{
						break;
					}
				}
			}
			return;
		}
		foreach (Node root in m_roots)
		{
			FlattenNode(root);
		}
	}

	private void FlattenNode(Node node)
	{
		m_flat.Add(node);
		if (!node.Expanded)
		{
			return;
		}
		foreach (Node child in GetChildren(node))
		{
			FlattenNode(child);
		}
	}

	private void OnSearchChanged(string value)
	{
		m_searchQuery = value != null ? value.Trim() : "";
		m_offset = 0;
		Refresh();
	}

	private void OnListStep(int steps)
	{
		if (steps == 0)
		{
			return;
		}
		int max = Mathf.Max(0, m_flat.Count - RowCount);
		m_offset = Mathf.Clamp(m_offset + steps, 0, max);
		Refresh();
	}

	private void OnRowClicked(int rowIndex)
	{
		int idx = m_offset + rowIndex;
		if (idx < 0 || idx >= m_flat.Count)
		{
			return;
		}
		Node node = m_flat[idx];
		if (node.Depth == 2)
		{
			LoadFromCodeSet(node);
			return;
		}
		node.Expanded = !node.Expanded;
		Refresh();
	}

	// ===== 点“类” → 从代码集载入 =====

	private void LoadFromCodeSet(Node node)
	{
		string cls = node.Key;
		string set;
		string file = INCodeSetStore.FindClassFile(cls, out set);
		if (string.IsNullOrEmpty(file))
		{
			SetStatus("未导入 " + cls + ".cs —— 请先在“代码编辑器”导入包含该类源码的代码集");
			Feedback("BP> 该类代码未导入代码集");
			return;
		}
		SwitchToPage(INCodeEditorPage.LocaleId);
		if (INCodeEditorPage.Instance == null)
		{
			SetStatus("代码编辑器页面不可用");
			return;
		}
		INCodeEditorPage.Instance.LoadExternal(set, file);
		SetStatus("已载入 " + set + " / " + file);
	}

	private static void SwitchToPage(string localeId)
	{
		try
		{
			GameObject sidebar = INAppInterface.Instance != null ? INAppInterface.Instance.Sidebar : null;
			if (sidebar == null)
			{
				return;
			}
			for (int i = 0; i < sidebar.transform.childCount; i++)
			{
				Transform child = sidebar.transform.GetChild(i);
				Text t = child.GetComponentInChildren<Text>(true);
				UITextLocale loc = t != null ? t.GetComponent<UITextLocale>() : null;
				if (loc != null && loc.ID == localeId)
				{
					Toggle toggle = child.GetComponent<Toggle>();
					if (toggle != null)
					{
						toggle.isOn = true;
					}
					return;
				}
			}
		}
		catch (Exception e)
		{
			Debug.LogWarning("[ClassBrowser] switch page failed: " + e.Message);
		}
	}

	private void OnPageUp()
	{
		m_offset = Mathf.Max(0, m_offset - RowCount);
		Refresh();
	}

	private void OnPageDown()
	{
		int maxOffset = Mathf.Max(0, m_flat.Count - RowCount);
		m_offset = Mathf.Min(maxOffset, m_offset + RowCount);
		Refresh();
	}

	private void Refresh()
	{
		Flatten();
		int total = m_flat.Count;
		int maxOffset = Mathf.Max(0, total - RowCount);
		if (m_offset > maxOffset)
		{
			m_offset = maxOffset;
		}
		if (m_offset < 0)
		{
			m_offset = 0;
		}

		for (int i = 0; i < m_rows.Count; i++)
		{
			int idx = m_offset + i;
			Row row = m_rows[i];
			if (idx < total)
			{
				Node node = m_flat[idx];
				row.Root.SetActive(true);

				float indent = node.Depth * 22f;
				row.Arrow.rectTransform.anchoredPosition = new Vector2(8f + indent, 0f);
				row.Name.rectTransform.offsetMin = new Vector2(34f + indent, 0f);

				if (node.Depth < 2)
				{
					row.Arrow.text = node.Expanded ? "-" : "+";
					row.Name.color = Color.white;
				}
				else
				{
					row.Arrow.text = "";
					row.Name.color = new Color(0.75f, 0.85f, 0.75f, 1f);
				}
				row.Name.text = node.Name;
			}
			else
			{
				row.Root.SetActive(false);
			}
		}

		if (m_indicator != null)
		{
			if (total == 0)
			{
				m_indicator.text = string.IsNullOrEmpty(m_searchQuery) ? "无条目" : "无匹配";
			}
			else
			{
				int first = m_offset + 1;
				int last = Mathf.Min(m_offset + RowCount, total);
				m_indicator.text = first + "-" + last + " / " + total;
			}
		}
	}

	private void SetStatus(string text)
	{
		if (m_status != null)
		{
			m_status.text = text;
		}
		Debug.Log("[ClassBrowser] " + text);
	}

	private static void Feedback(string msg)
	{
		try
		{
			Innovation.BP.Feedback(msg);
		}
		catch
		{
		}
	}

	// ===== UI 辅助 =====

	private static readonly Color S_NEUTRAL = new Color(0.35f, 0.35f, 0.4f, 1f);

	private void Stretch(RectTransform rt, float left, float bottom, float right, float top)
	{
		rt.anchorMin = Vector2.zero;
		rt.anchorMax = Vector2.one;
		rt.pivot = new Vector2(0.5f, 0.5f);
		rt.offsetMin = new Vector2(left, bottom);
		rt.offsetMax = new Vector2(-right, -top);
	}

	private void AnchorRow(RectTransform rt, float x0, float x1, float top, float height)
	{
		rt.anchorMin = new Vector2(x0, 1f);
		rt.anchorMax = new Vector2(x1, 1f);
		rt.pivot = new Vector2(0.5f, 1f);
		rt.offsetMin = new Vector2(0f, -(top + height));
		rt.offsetMax = new Vector2(0f, -top);
	}

	private Image NewImage(Transform parent, string name, Color color)
	{
		GameObject go = new GameObject(name, typeof(RectTransform));
		RectTransform rt = go.GetComponent<RectTransform>();
		rt.SetParent(parent, false);
		Image image = go.AddComponent<Image>();
		image.color = color;
		return image;
	}

	private Text NewText(Transform parent, string name, int fontSize, TextAnchor anchor, Color color)
	{
		GameObject go = new GameObject(name, typeof(RectTransform));
		RectTransform rt = go.GetComponent<RectTransform>();
		rt.SetParent(parent, false);
		Text text = go.AddComponent<Text>();
		text.font = m_font;
		text.fontSize = fontSize;
		text.alignment = anchor;
		text.color = color;
		text.raycastTarget = false;
		text.horizontalOverflow = HorizontalWrapMode.Overflow;
		text.verticalOverflow = VerticalWrapMode.Overflow;
		return text;
	}

	private InputField NewInput(Transform parent, string name, bool multiLine)
	{
		GameObject go = new GameObject(name, typeof(RectTransform));
		RectTransform rt = go.GetComponent<RectTransform>();
		rt.SetParent(parent, false);
		Image bg = go.AddComponent<Image>();
		bg.color = new Color(1f, 1f, 1f, 0.10f);
		InputField input = go.AddComponent<InputField>();
		input.targetGraphic = bg;
		input.lineType = InputField.LineType.SingleLine;
		input.contentType = InputField.ContentType.Standard;

		Text text = NewText(go.transform, "Text", 20, TextAnchor.MiddleLeft, Color.white);
		RectTransform trt = text.rectTransform;
		trt.anchorMin = Vector2.zero;
		trt.anchorMax = Vector2.one;
		trt.offsetMin = new Vector2(10f, 4f);
		trt.offsetMax = new Vector2(-10f, -4f);
		text.supportRichText = false;
		input.textComponent = text;

		Text ph = NewText(go.transform, "Placeholder", 20, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.3f));
		RectTransform prt = ph.rectTransform;
		prt.anchorMin = Vector2.zero;
		prt.anchorMax = Vector2.one;
		prt.offsetMin = new Vector2(10f, 4f);
		prt.offsetMax = new Vector2(-10f, -4f);
		ph.text = "搜索类名…";
		input.placeholder = ph;
		return input;
	}

	private void Btn(string label, float x0, float x1, float top, float height, UnityEngine.Events.UnityAction onClick)
	{
		GameObject go = new GameObject("Btn_" + label, typeof(RectTransform));
		RectTransform rt = go.GetComponent<RectTransform>();
		rt.SetParent(transform, false);
		AnchorRow(rt, x0, x1, top, height);
		Image bg = go.AddComponent<Image>();
		bg.color = S_NEUTRAL;
		UButton button = go.AddComponent<UButton>();
		button.targetGraphic = bg;
		button.onClick.AddListener(onClick);
		Text text = NewText(go.transform, "Text", 18, TextAnchor.MiddleCenter, Color.white);
		Stretch(text.rectTransform, 2f, 0f, 2f, 0f);
		text.text = label;
	}
}
