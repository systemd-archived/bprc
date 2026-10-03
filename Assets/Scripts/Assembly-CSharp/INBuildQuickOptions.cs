using UnityEngine;
using UnityEngine.EventSystems;
using UButton = UnityEngine.UI.Button;
using UImage = UnityEngine.UI.Image;
using UText = UnityEngine.UI.Text;

// 建造界面“快捷继承 / 显示修改”可拖动按钮组（仿辅助菜单）。
// 星海设置里的“快捷继承/显示修改”只控制对应按钮【是否显示】；按钮本身是功能的开关。
public class INBuildQuickOptions : MonoBehaviour
{
	private const string PrefX = "INBuildQuickOptsX";
	private const string PrefY = "INBuildQuickOptsY";
	private const float SlotW = 96f;
	private const float SlotGap = 6f;
	private const float Tall = 38f;

	private static readonly Color s_onBg = new Color(0.15f, 0.5f, 0.15f, 0.95f);
	private static readonly Color s_offBg = new Color(0.25f, 0.25f, 0.25f, 0.9f);
	private static readonly Color s_onText = Color.white;
	private static readonly Color s_offText = new Color(0.75f, 0.75f, 0.75f, 1f);

	private Canvas m_canvas;
	private RectTransform m_rect;
	private GameObject m_inheritObj;
	private GameObject m_showObj;
	private UImage m_inheritBg;
	private UImage m_showBg;
	private UText m_inheritText;
	private UText m_showText;
	private bool m_dragging;
	private bool m_suppressClick;
	private float m_syncTimer;

	private void Awake()
	{
		m_canvas = GetComponentInParent<Canvas>();
		BuildUI();
		ApplyPosition();
	}

	private void OnDestroy()
	{
		INModifiedPartMarker.ClearAll();
	}

	private void BuildUI()
	{
		GameObject root = new GameObject("INBuildQuickOptions");
		root.transform.SetParent(base.transform, false);
		m_rect = root.AddComponent<RectTransform>();
		m_rect.anchorMin = new Vector2(1f, 1f);
		m_rect.anchorMax = new Vector2(1f, 1f);
		m_rect.pivot = new Vector2(1f, 1f);
		m_rect.sizeDelta = new Vector2(SlotW * 2f + SlotGap, Tall);
		UImage rootBg = root.AddComponent<UImage>();
		rootBg.color = new Color(0.08f, 0.08f, 0.08f, 0.25f);
		AddDrag(root);

		m_inheritObj = CreateButton(root.transform, "快捷继承", out m_inheritBg, out m_inheritText, OnToggleInherit);
		m_showObj = CreateButton(root.transform, "显示修改", out m_showBg, out m_showText, OnToggleShow);
		RefreshVisual();
	}

	private GameObject CreateButton(Transform parent, string label, out UImage bg, out UText text, UnityEngine.Events.UnityAction onClick)
	{
		GameObject btn = new GameObject(label);
		btn.transform.SetParent(parent, false);
		RectTransform r = btn.AddComponent<RectTransform>();
		r.anchorMin = new Vector2(0f, 0f);
		r.anchorMax = new Vector2(0f, 1f);
		r.pivot = new Vector2(0f, 0.5f);
		r.sizeDelta = new Vector2(SlotW, -4f);
		r.anchoredPosition = Vector2.zero;
		bg = btn.AddComponent<UImage>();
		bg.color = s_offBg;
		UButton b = btn.AddComponent<UButton>();
		b.targetGraphic = bg;
		b.onClick.AddListener(onClick);
		AddDrag(btn);

		GameObject txt = new GameObject("Text");
		txt.transform.SetParent(btn.transform, false);
		RectTransform tr = txt.AddComponent<RectTransform>();
		tr.anchorMin = Vector2.zero;
		tr.anchorMax = Vector2.one;
		tr.sizeDelta = Vector2.zero;
		text = txt.AddComponent<UText>();
		text.text = label;
		text.font = INUiUtil.UiFont;
		text.fontSize = 15;
		text.alignment = TextAnchor.MiddleCenter;
		text.color = Color.white;
		text.raycastTarget = false;
		return btn;
	}

	private void AddDrag(GameObject go)
	{
		EventTrigger trig = go.AddComponent<EventTrigger>();
		AddTrigger(trig, EventTriggerType.BeginDrag, OnBeginDrag);
		AddTrigger(trig, EventTriggerType.Drag, OnDrag);
		AddTrigger(trig, EventTriggerType.EndDrag, OnEndDrag);
	}

	private static void AddTrigger(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> callback)
	{
		EventTrigger.Entry entry = new EventTrigger.Entry();
		entry.eventID = type;
		entry.callback.AddListener(callback);
		trigger.triggers.Add(entry);
	}

	private void OnBeginDrag(BaseEventData data)
	{
		m_dragging = true;
	}

	private void OnDrag(BaseEventData data)
	{
		if (!m_dragging || m_rect == null)
		{
			return;
		}
		PointerEventData ped = data as PointerEventData;
		if (ped == null)
		{
			return;
		}
		float scale = (m_canvas != null && m_canvas.scaleFactor > 0f) ? m_canvas.scaleFactor : 1f;
		m_rect.anchoredPosition += ped.delta / scale;
	}

	private void OnEndDrag(BaseEventData data)
	{
		if (!m_dragging)
		{
			return;
		}
		m_dragging = false;
		m_suppressClick = true;
		SavePosition();
	}

	private void SavePosition()
	{
		if (m_rect == null)
		{
			return;
		}
		PlayerPrefs.SetFloat(PrefX, m_rect.anchoredPosition.x);
		PlayerPrefs.SetFloat(PrefY, m_rect.anchoredPosition.y);
		PlayerPrefs.Save();
	}

	private void ApplyPosition()
	{
		if (m_rect == null)
		{
			return;
		}
		m_rect.anchoredPosition = new Vector2(PlayerPrefs.GetFloat(PrefX, -430f), PlayerPrefs.GetFloat(PrefY, -74f));
	}

	private void OnToggleInherit()
	{
		if (m_suppressClick)
		{
			return;
		}
		INQuickInherit.Enabled = !INQuickInherit.Enabled;
		RefreshVisual();
	}

	private void OnToggleShow()
	{
		if (m_suppressClick)
		{
			return;
		}
		INModifiedPartMarker.Enabled = !INModifiedPartMarker.Enabled;
		RefreshVisual();
		INModifiedPartMarker.SyncAll();
	}

	private void RefreshVisual()
	{
		ApplyButton(m_inheritBg, m_inheritText, INQuickInherit.Enabled);
		ApplyButton(m_showBg, m_showText, INModifiedPartMarker.Enabled);
	}

	private static void ApplyButton(UImage bg, UText text, bool on)
	{
		if (bg != null)
		{
			bg.color = on ? s_onBg : s_offBg;
		}
		if (text != null)
		{
			text.color = on ? s_onText : s_offText;
		}
	}

	private void Update()
	{
		if (m_suppressClick)
		{
			m_suppressClick = false;
		}
		bool buildState = IsBuildState();
		StarSeaSettings s = INUserSettings.Instance?.StarSeaSettings;
		bool showInherit = INParamSystem.Enabled && (s?.QuickInheritParams ?? true);
		bool showShow = INParamSystem.Enabled && (s?.ShowModifiedParams ?? true);

		if (m_inheritObj != null && m_inheritObj.activeSelf != showInherit)
		{
			m_inheritObj.SetActive(showInherit);
		}
		if (m_showObj != null && m_showObj.activeSelf != showShow)
		{
			m_showObj.SetActive(showShow);
		}
		// 只有一个按钮时贴左对齐
		if (showShow && !showInherit && m_showObj != null)
		{
			m_showObj.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
		}
		else if (m_showObj != null)
		{
			m_showObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(SlotW + SlotGap, 0f);
		}

		bool any = buildState && (showInherit || showShow);
		if (m_rect != null && m_rect.gameObject.activeSelf != any)
		{
			m_rect.gameObject.SetActive(any);
		}
		if (any)
		{
			RefreshVisual();
		}
		// 标记同步：无论按钮组是否显示都要跑；非建造状态（游戏开始）直接销毁全部标记
		m_syncTimer += Time.unscaledDeltaTime;
		bool buildNow = IsBuildState();
		if (!buildNow)
		{
			m_syncTimer = 0f;
			INModifiedPartMarker.ClearAll();
		}
		else if (m_syncTimer >= 0.2f)
		{
			m_syncTimer = 0f;
			INModifiedPartMarker.SyncAll();
		}
	}

	private static bool IsBuildState()
	{
		LevelManager lm = WPFMonoBehaviour.levelManager;
		if (lm == null)
		{
			return false;
		}
		return lm.gameState == LevelManager.GameState.Building
			|| lm.gameState == LevelManager.GameState.PausedWhileBuilding
			|| lm.gameState == LevelManager.GameState.PreviewWhileBuilding;
	}
}
