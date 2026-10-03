using UnityEngine;
using UnityEngine.UI;

// egui -> uGUI 的最小兼容层：提供移植 UI 所需的基础控件与布局（结构 1:1，实现用 uGUI）。
// 注意：工程内存在自定义 Button/Sprite 全局类，必须全限定 UnityEngine.UI.* / UnityEngine.Sprite。
public class BpUi
{
	public readonly RectTransform Root;

	public BpUi(RectTransform root) { Root = root; }

	// ---------------- 布局 ----------------

	public static GameObject Node(string name, Transform parent)
	{
		GameObject go = new GameObject(name);
		go.transform.SetParent(parent, false);
		return go;
	}

	// 竖向布局 + 铺满父级（不加 ContentSizeFitter；适用于固定尺寸父级：窗口内容区/对话框）
	public static RectTransform VerticalStretch(Transform parent, float spacing, RectOffset padding = null, string name = "Col")
	{
		GameObject go = Node(name, parent);
		RectTransform rt = go.AddComponent<RectTransform>();
		rt.anchorMin = Vector2.zero;
		rt.anchorMax = Vector2.one;
		rt.offsetMin = Vector2.zero;
		rt.offsetMax = Vector2.zero;
		VerticalLayoutGroup v = go.AddComponent<VerticalLayoutGroup>();
		v.spacing = spacing;
		v.childAlignment = TextAnchor.UpperLeft;
		v.childForceExpandWidth = true;
		v.childForceExpandHeight = false;
		v.childControlWidth = true;
		v.childControlHeight = true;
		if (padding != null) v.padding = padding;
		return rt;
	}

	public static RectTransform Vertical(Transform parent, float spacing, RectOffset padding = null, string name = "Col")
	{
		GameObject go = Node(name, parent);
		RectTransform rt = go.AddComponent<RectTransform>();
		VerticalLayoutGroup v = go.AddComponent<VerticalLayoutGroup>();
		v.spacing = spacing;
		v.childAlignment = TextAnchor.UpperLeft;
		v.childForceExpandWidth = true;
		v.childForceExpandHeight = false;
		v.childControlWidth = true;
		v.childControlHeight = true;
		if (padding != null) v.padding = padding;
		ContentSizeFitter f = go.AddComponent<ContentSizeFitter>();
		f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		return rt;
	}

	public static RectTransform Horizontal(Transform parent, float spacing, RectOffset padding = null, string name = "Row", float height = 0f)
	{
		GameObject go = Node(name, parent);
		RectTransform rt = go.AddComponent<RectTransform>();
		HorizontalLayoutGroup h = go.AddComponent<HorizontalLayoutGroup>();
		h.spacing = spacing;
		h.childAlignment = TextAnchor.MiddleLeft;
		h.childForceExpandWidth = true;
		h.childForceExpandHeight = false;
		h.childControlWidth = true;
		h.childControlHeight = true;
		if (padding != null) h.padding = padding;
		if (height > 0f)
		{
			LayoutElement le = go.AddComponent<LayoutElement>();
			le.minHeight = height;
			le.preferredHeight = height;
		}
		return rt;
	}

	// 全屏铺满（用于窗口根/内容区）
	public static RectTransform Stretch(GameObject go)
	{
		RectTransform rt = go.GetComponent<RectTransform>();
		if (rt == null) rt = go.AddComponent<RectTransform>();
		rt.anchorMin = Vector2.zero;
		rt.anchorMax = Vector2.one;
		rt.offsetMin = Vector2.zero;
		rt.offsetMax = Vector2.zero;
		return rt;
	}

	// ---------------- 控件 ----------------

	public static Text Label(Transform parent, string text, int size = 20, TextAnchor align = TextAnchor.MiddleLeft, float height = 26f)
	{
		GameObject go = Node("Label", parent);
		LayoutElement le = go.AddComponent<LayoutElement>();
		le.minHeight = height; le.preferredHeight = height;
		Text t = go.AddComponent<Text>();
		t.font = INUnity.ArialFont;
		t.fontSize = size;
		t.alignment = align;
		t.color = new Color(0.82f, 0.86f, 0.92f, 1f);
		t.text = text;
		t.horizontalOverflow = HorizontalWrapMode.Overflow;
		return t;
	}

	public static UnityEngine.UI.Image Button(Transform parent, string label, UnityEngine.Events.UnityAction onClick, float height = 34f, Color? color = null)
	{
		GameObject go = Node("Btn_" + label, parent);
		LayoutElement le = go.AddComponent<LayoutElement>();
		le.minHeight = height; le.preferredHeight = height;
		le.minWidth = 48f;
		UnityEngine.UI.Image img = go.AddComponent<UnityEngine.UI.Image>();
		img.color = color ?? new Color(0.20f, 0.22f, 0.28f, 0.96f);
		UnityEngine.UI.Button b = go.AddComponent<UnityEngine.UI.Button>();
		b.targetGraphic = img;
		b.onClick.AddListener(onClick);
		AddCenteredText(go.transform, label, 18);
		return img;
	}

	public static UnityEngine.UI.Toggle Toggle(Transform parent, string label, bool value, UnityEngine.Events.UnityAction<bool> onChanged, float height = 30f)
	{
		GameObject go = Node("Toggle_" + label, parent);
		LayoutElement le = go.AddComponent<LayoutElement>();
		le.minHeight = height; le.preferredHeight = height;
		UnityEngine.UI.Toggle tg = go.AddComponent<UnityEngine.UI.Toggle>();
		GameObject bg = Node("Background", go.transform);
		RectTransform bgr = bg.AddComponent<RectTransform>();
		bgr.anchorMin = new Vector2(0f, 0.5f); bgr.anchorMax = new Vector2(0f, 0.5f);
		bgr.pivot = new Vector2(0f, 0.5f);
		bgr.anchoredPosition = new Vector2(2f, 0f);
		bgr.sizeDelta = new Vector2(22f, 22f);
		UnityEngine.UI.Image img = bg.AddComponent<UnityEngine.UI.Image>();
		img.color = new Color(0.16f, 0.18f, 0.22f, 1f);
		GameObject chk = Node("Check", bg.transform);
		Stretch(chk);
		UnityEngine.UI.Image cimg = chk.AddComponent<UnityEngine.UI.Image>();
		cimg.color = new Color(0.25f, 0.75f, 0.45f, 1f);
		tg.targetGraphic = img;
		tg.graphic = cimg;
		tg.isOn = value;
		tg.onValueChanged.AddListener(onChanged);
		Text t = Label(go.transform, label, 18, TextAnchor.MiddleLeft, height);
		t.rectTransform.offsetMin = new Vector2(32f, 0f);
		return tg;
	}

	public static UnityEngine.UI.Slider Slider(Transform parent, float min, float max, float value, UnityEngine.Events.UnityAction<float> onChanged, float height = 30f)
	{
		GameObject go = Node("Slider", parent);
		LayoutElement le = go.AddComponent<LayoutElement>();
		le.minHeight = height; le.preferredHeight = height;
		UnityEngine.UI.Slider s = go.AddComponent<UnityEngine.UI.Slider>();
		s.minValue = min; s.maxValue = max; s.value = value;
		GameObject bg = Node("Background", go.transform);
		RectTransform bgr = Stretch(bg);
		UnityEngine.UI.Image bimg = bg.AddComponent<UnityEngine.UI.Image>();
		bimg.color = new Color(0.15f, 0.17f, 0.21f, 1f);
		GameObject fa = Node("FillArea", go.transform);
		RectTransform far = Stretch(fa);
		far.offsetMin = new Vector2(6f, 8f); far.offsetMax = new Vector2(-6f, -8f);
		GameObject f = Node("Fill", fa.transform);
		RectTransform fr = Stretch(f);
		UnityEngine.UI.Image fimg = f.AddComponent<UnityEngine.UI.Image>();
		fimg.color = new Color(0.25f, 0.55f, 0.85f, 1f);
		s.fillRect = fr;
		s.targetGraphic = bimg;
		s.onValueChanged.AddListener(onChanged);
		return s;
	}

	public static UnityEngine.UI.InputField TextField(Transform parent, string value, UnityEngine.Events.UnityAction<string> onEndEdit, float height = 32f, string placeholder = "")
	{
		GameObject go = Node("Input", parent);
		LayoutElement le = go.AddComponent<LayoutElement>();
		le.minHeight = height; le.preferredHeight = height;
		UnityEngine.UI.Image img = go.AddComponent<UnityEngine.UI.Image>();
		img.color = new Color(0.10f, 0.12f, 0.15f, 1f);
		UnityEngine.UI.InputField f = go.AddComponent<UnityEngine.UI.InputField>();
		f.targetGraphic = img;
		f.lineType = UnityEngine.UI.InputField.LineType.SingleLine;
		GameObject t = Node("Text", go.transform);
		RectTransform tr = Stretch(t);
		tr.offsetMin = new Vector2(8f, 0f); tr.offsetMax = new Vector2(-8f, 0f);
		Text txt = t.AddComponent<Text>();
		txt.font = INUnity.ArialFont; txt.fontSize = 18;
		txt.alignment = TextAnchor.MiddleLeft;
		txt.color = Color.white;
		txt.supportRichText = false;
		GameObject ph = Node("Placeholder", go.transform);
		RectTransform phr = Stretch(ph);
		phr.offsetMin = new Vector2(8f, 0f); phr.offsetMax = new Vector2(-8f, 0f);
		Text pht = ph.AddComponent<Text>();
		pht.font = INUnity.ArialFont; pht.fontSize = 18;
		pht.alignment = TextAnchor.MiddleLeft;
		pht.color = new Color(0.5f, 0.55f, 0.6f, 1f);
		pht.text = placeholder;
		f.textComponent = txt;
		f.placeholder = pht;
		f.text = value ?? "";
		f.onEndEdit.AddListener(onEndEdit);
		return f;
	}

	public static UnityEngine.UI.ScrollRect ScrollView(Transform parent, out RectTransform content, float height = 0f)
	{
		GameObject go = Node("ScrollView", parent);
		RectTransform vp = Stretch(go);
		UnityEngine.UI.ScrollRect sr = go.AddComponent<UnityEngine.UI.ScrollRect>();
		GameObject viewport = Node("Viewport", go.transform);
		RectTransform vpr = Stretch(viewport);
		UnityEngine.UI.Image vimg = viewport.AddComponent<UnityEngine.UI.Image>();
		vimg.color = new Color(0f, 0f, 0f, 0.25f);
		GameObject contentGo = Node("Content", viewport.transform);
		RectTransform cr = contentGo.AddComponent<RectTransform>();
		cr.anchorMin = new Vector2(0f, 1f);
		cr.anchorMax = new Vector2(1f, 1f);
		cr.pivot = new Vector2(0.5f, 1f);
		cr.offsetMin = Vector2.zero; cr.offsetMax = Vector2.zero;
		VerticalLayoutGroup v = contentGo.AddComponent<VerticalLayoutGroup>();
		v.spacing = 4f; v.childForceExpandWidth = true;
		v.childControlWidth = true; v.childControlHeight = true;
		ContentSizeFitter csf = contentGo.AddComponent<ContentSizeFitter>();
		csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		sr.content = cr;
		sr.viewport = vpr;
		sr.horizontal = false;
		sr.vertical = true;
		content = cr;
		_ = vp;
		_ = height;
		return sr;
	}

	public static UnityEngine.UI.Dropdown ComboBox(Transform parent, string[] options, int index, UnityEngine.Events.UnityAction<int> onChanged, float height = 32f)
	{
		GameObject go = Node("Dropdown", parent);
		LayoutElement le = go.AddComponent<LayoutElement>();
		le.minHeight = height; le.preferredHeight = height;
		UnityEngine.UI.Image img = go.AddComponent<UnityEngine.UI.Image>();
		img.color = new Color(0.18f, 0.20f, 0.25f, 1f);
		UnityEngine.UI.Dropdown d = go.AddComponent<UnityEngine.UI.Dropdown>();
		d.targetGraphic = img;
		GameObject cap = Node("Label", go.transform);
		RectTransform cpr = Stretch(cap);
		cpr.offsetMin = new Vector2(8f, 0f); cpr.offsetMax = new Vector2(-8f, 0f);
		Text t = cap.AddComponent<Text>();
		t.font = INUnity.ArialFont; t.fontSize = 18;
		t.alignment = TextAnchor.MiddleLeft; t.color = Color.white;
		d.captionText = t;
		GameObject tpl = Node("Template", go.transform);
		RectTransform tplr = tpl.AddComponent<RectTransform>();
		tplr.anchorMin = new Vector2(0f, 0f); tplr.anchorMax = new Vector2(1f, 0f);
		tplr.pivot = new Vector2(0.5f, 1f);
		tplr.sizeDelta = new Vector2(0f, 150f);
		tpl.SetActive(false);
		GameObject vp = Node("Viewport", tpl.transform);
		RectTransform vpr2 = Stretch(vp);
		vp.AddComponent<UnityEngine.UI.Image>().color = new Color(0.12f, 0.14f, 0.18f, 1f);
		vp.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = true;
		GameObject c = Node("Content", vp.transform);
		RectTransform cr2 = c.AddComponent<RectTransform>();
		cr2.anchorMin = new Vector2(0f, 1f); cr2.anchorMax = new Vector2(1f, 1f);
		cr2.pivot = new Vector2(0.5f, 1f);
		cr2.offsetMin = Vector2.zero; cr2.offsetMax = Vector2.zero;
		GameObject item = Node("Item", c.transform);
		RectTransform ir = item.AddComponent<RectTransform>();
		ir.anchorMin = new Vector2(0f, 0.5f); ir.anchorMax = new Vector2(1f, 0.5f);
		ir.sizeDelta = new Vector2(0f, 28f);
		UnityEngine.UI.Toggle itg = item.AddComponent<UnityEngine.UI.Toggle>();
		GameObject ig = Node("Item Background", item.transform);
		Stretch(ig);
		ig.AddComponent<UnityEngine.UI.Image>().color = new Color(0.18f, 0.20f, 0.25f, 1f);
		GameObject ic = Node("Item Checkmark", item.transform);
		Stretch(ic);
		ic.AddComponent<UnityEngine.UI.Image>().color = new Color(0.25f, 0.55f, 0.85f, 1f);
		GameObject il = Node("Item Label", item.transform);
		RectTransform ilr = Stretch(il);
		ilr.offsetMin = new Vector2(10f, 0f); ilr.offsetMax = new Vector2(-10f, 0f);
		Text ilt = il.AddComponent<Text>();
		ilt.font = INUnity.ArialFont; ilt.fontSize = 18;
		ilt.alignment = TextAnchor.MiddleLeft; ilt.color = Color.white;
		itg.targetGraphic = ig.GetComponent<UnityEngine.UI.Image>();
		itg.graphic = ic.GetComponent<UnityEngine.UI.Image>();
		d.template = tplr;
		d.captionText = t;
		d.itemText = ilt;
		d.options.Clear();
		if (options != null)
		{
			foreach (string o in options) d.options.Add(new UnityEngine.UI.Dropdown.OptionData(o));
		}
		d.value = Mathf.Clamp(index, 0, Mathf.Max(0, (options != null ? options.Length : 1) - 1));
		d.RefreshShownValue();
		d.onValueChanged.AddListener(onChanged);
		return d;
	}

	public static Text AddCenteredText(Transform parent, string label, int size)
	{
		GameObject go = Node("Text", parent);
		RectTransform rt = Stretch(go);
		Text t = go.AddComponent<Text>();
		t.font = INUnity.ArialFont;
		t.fontSize = size;
		t.alignment = TextAnchor.MiddleCenter;
		t.color = Color.white;
		t.text = label;
		t.horizontalOverflow = HorizontalWrapMode.Overflow;
		_ = rt;
		return t;
	}
}
