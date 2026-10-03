using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class INAppInterface : MonoBehaviour
{
	/// <summary>How much of a frame the page warm-up may spend building rows, in seconds.</summary>
	/// <remarks>
	/// A page of 208 rows measures at a little over half a second of row building, so this is what decides
	/// how long the longest page takes to be ready: at this slice it is ready about a second after the
	/// panel appears, and the frames it is spread over stay smooth. Raising it makes pages ready sooner and
	/// the frames it runs on heavier.
	/// </remarks>
	private const float FrameBudget = 0.003f;

	[SerializeField]
	private Canvas m_canvas;

	[SerializeField]
	private GameObject m_panel;

	[SerializeField]
	private GameObject m_main;

	[SerializeField]
	private GameObject m_actionBar;

	[SerializeField]
	private GameObject m_sidebar;

	[SerializeField]
	private UnityEngine.UI.Button m_enterButton;

	[SerializeField]
	private UnityEngine.UI.Button m_backButton;

	[SerializeField]
	private UnityEngine.UI.Button m_menuButton;

	[SerializeField]
	private List<GameObject> m_elements;

	private bool m_enabled;

	private bool m_warmedUp;

	private bool m_paused;

	private TMP_Text m_heatTitle;

	private GameObject m_heatLayout;

	public GameObject Main => m_main;

	public GameObject ActionBar => m_actionBar;

	public GameObject Sidebar => m_sidebar.gameObject;

	public static INAppInterface Instance { get; private set; }

	public event Action AppInterfaceEnabled;

	public event Action AppInterfaceDisabled;

	private void Awake()
	{
		Instance = this;
		UnityEngine.Object.DontDestroyOnLoad(this);
		Initialize();
	}

	private void Initialize()
	{
		INUiScale.Register(m_canvas);
		m_enabled = false;

		// The panel's own background takes Heat UI's palette; a no-op when the pack is not there.
		HeatUiSkin.ApplyPanelSkin(gameObject);

		// The buttons become Heat UI's when the pack is staged. A swap that cannot produce a working
		// button returns null and the original is kept, so the handlers below always land somewhere.
		m_heatTitle = HeatUiPanelLayout.Install(m_panel, m_main, m_sidebar, m_actionBar, m_backButton, m_menuButton, out m_heatLayout);

		m_enterButton.onClick.AddListener(OnEnterButtonClick);
		m_backButton.onClick.AddListener(OnBackButtonClick);
		m_menuButton.onClick.AddListener(OnMenuButtonClick);
		INUserSettings.Instance.AppInterfaceSettings.Apply();
		string text = INLocalization.Instance.GetText("AppInterface_AppName");
		string text2 = INLocalization.Instance.GetText("AppInterface_Name");
		SetTitle(text + " " + text2);
		foreach (GameObject element in m_elements)
		{
			GameObject obj = UnityEngine.Object.Instantiate(element);
			obj.SetActive(false);
			obj.name = element.name;
			obj.transform.SetParent(m_main.transform, worldPositionStays: false);
			if (m_heatLayout != null) HeatUiPanelLayout.AttachElement(obj);
			obj.SetChildrenActive(active: false);
			RectTransform obj2 = (RectTransform)obj.transform;
			obj2.anchorMin = new Vector2(0f, 0f);
			obj2.anchorMax = new Vector2(1f, 1f);
			obj2.anchoredPosition = Vector2.zero;
			obj2.sizeDelta = Vector2.zero;
			obj.SetActive(true);
		}
		if (m_heatLayout != null) m_heatLayout.SetActive(true);
		EventManager.Connect<LocalizationReloaded>(OnLocalizationReloaded);
	}

	/// <summary>
	/// Redraws the panel's text in the language that has just been switched to.
	/// </summary>
	/// <remarks>
	/// A language change reloads the two string tables and announces itself; this is the panel's half of
	/// that. Every label the panel shows is a <see cref="UITextLocale"/>, so re-resolving those covers the
	/// categories, the page headings and the row labels - and re-binds the option help with them, since a
	/// label is what binds it. What that leaves is the text a row builds for itself rather than resolving
	/// from an id, which the settings panels handle, and the title, which names the selected category.
	/// </remarks>
	private void OnLocalizationReloaded(LocalizationReloaded reloaded)
	{
		if (m_panel == null) { return; }
		foreach (UITextLocale locale in m_panel.GetComponentsInChildren<UITextLocale>(includeInactive: true))
		{
			locale.UpdateText();
		}
		foreach (INSettingsInterface page in m_panel.GetComponentsInChildren<INSettingsInterface>(includeInactive: true))
		{
			page.RefreshLocalization();
		}
		if (m_sidebar != null)
		{
			AppInterfaceSidebar sidebar = m_sidebar.GetComponent<AppInterfaceSidebar>();
			if (sidebar != null) { sidebar.RefreshTitle(); }
		}
	}

	private void Start()
	{
		m_panel.gameObject.SetActive(value: false);
	}

	private void Update()
	{
		if (m_enabled && Input.GetKeyDown(KeyCode.Escape)) { SetEnabled(false); }

		// Once the panel is up, every settings page is built, styled and shown once, so that picking a
		// category only has to show a page that is ready. Both halves of that used to land on the click
		// that opened the category; 更多设置 is 208 rows, which is a visible hitch. Triggered here rather
		// than from SetEnabled so that a panel switched on by anything else is covered too.
		if (!m_warmedUp && m_panel != null && m_panel.activeInHierarchy)
		{
			m_warmedUp = true;
            CoroutineRunner.Instance.StartCoroutine(WarmUpPages());
		}
	}

	/// <summary>
	/// Gets every settings page ready while the panel is opening, so that picking a category only has to
	/// show a page that is already built and already awake.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Two things used to land on the click. The first is the styling pass: the panels build their rows
	/// shortly after the panel appears, but the adapter that turns those rows into Heat UI ones only runs
	/// while its page is the open one, so a page's whole pass happened on the frame its category was
	/// clicked - 更多设置 is 208 rows, each replaced by a Heat UI element with its own labels, switch or
	/// input field and bindings. The warm-up below moves that here, a slice of a frame at a time.
	/// </para>
	/// <para>
	/// The second is everything a page pays the first time it is shown: Heat UI's own components waking
	/// up, the text meshes, and the layout of a list that can be two hundred rows long. That part can
	/// only be paid by showing the page, so each one is shown in turn with the panel up but the page
	/// itself invisible and untouchable for two frames. This is the same thing Heat UI's own PanelManager
	/// does when it initializes a canvas before displaying it.
	/// </para>
	/// </remarks>
	private IEnumerator WarmUpPages()
	{
		// The panels build their rows in their own Start, which the frame after the panel appears has run.
		yield return null;

		// Longest page first. The row count is what makes a page expensive, and the player is as likely to
		// open the long ones as the short ones, so the page that would hurt most is the one made ready first.
		List<GameObject> order = new List<GameObject>(m_elements);
		order.Sort((left, right) => PageSize(right).CompareTo(PageSize(left)));

		// A page at a time, and each page a slice of a frame at a time. The warm-up is background work
		// behind a panel the player is already looking at, so what it must not do is spend a whole page's
		// worth of row building inside one frame; it finishes sooner in wall clock time this way, because
		// the frames it is spread over are no longer long ones.
		foreach (GameObject element in order)
		{
			if (this == null) yield break;
			GameObject live = FindElement(element.name);
			if (live == null) { continue; }
			// Wake hidden rows while they are being built, instead of waking hundreds on the first click.
			yield return PreShow(live);
			HeatUiLegacyPanelAdapter adapter = live.GetComponent<HeatUiLegacyPanelAdapter>();
			INSettingsInterface settings = live.GetComponent<INSettingsInterface>();
			INPartAdjustInterface features = live.GetComponent<INPartAdjustInterface>();
			while (adapter != null)
			{
				if (this == null || live == null) yield break;
				if (m_panel == null || !m_panel.activeInHierarchy) { yield return null; continue; }
				bool changed = adapter.WarmUp(FrameBudget * 1000f);
				// A temporarily empty pass is not complete while the page is still generating its rows.
				if (!changed && (settings == null || !settings.IsBuilding) && (features == null || !features.IsBuilding)) break;
				yield return null;
			}

		}
	}

	/// <summary>
	/// Keeps a hidden page awake during incremental construction, then lets its layout settle for two frames.
	/// </summary>
	private IEnumerator PreShow(GameObject live)
	{
		Transform page = live != null ? live.transform.Find("HeatPage") : null;

		// A page that is already showing is left alone: it is awake already, and hiding it for the two
		// frames this takes would blink the page the player is looking at.
		if (page == null || page.gameObject.activeSelf) { yield break; }

		// Hidden with a CanvasGroup rather than by moving the page aside, so that showing it later still
		// needs no layout pass.
		CanvasGroup group = page.GetComponent<CanvasGroup>();
		if (group == null) { group = page.gameObject.AddComponent<CanvasGroup>(); }
		float alpha = group.alpha;
		bool interactable = group.interactable;
		bool blocksRaycasts = group.blocksRaycasts;

		group.alpha = 0f;
		group.interactable = false;
		group.blocksRaycasts = false;
		HeatUiPanelLayout.SetElementActive(live, true);
		HeatUiLegacyPanelAdapter adapter = live.GetComponent<HeatUiLegacyPanelAdapter>();
		INSettingsInterface settings = live.GetComponent<INSettingsInterface>();
		INPartAdjustInterface features = live.GetComponent<INPartAdjustInterface>();
		while (live != null)
		{
			if (this == null) yield break;
			if (m_panel == null || !m_panel.activeInHierarchy) { yield return null; continue; }
			if (IsSelected(live))
			{
				group.alpha = alpha;
				group.interactable = interactable;
				group.blocksRaycasts = blocksRaycasts;
				break;
			}
			bool changed = adapter != null && adapter.WarmUp(FrameBudget * 1000f);
			if (!changed && (settings == null || !settings.IsBuilding) && (features == null || !features.IsBuilding)) break;
			yield return null;
		}
		yield return null;
		yield return null;
		group.alpha = alpha;
		group.interactable = interactable;
		group.blocksRaycasts = blocksRaycasts;

		// The sidebar's toggles are the truth about which page should be showing, so it is asked rather
		// than assumed: a category the player picks while this is running must not be switched off
		// underneath them.
		HeatUiPanelLayout.SetElementActive(live, IsSelected(live));
	}

	/// <summary>True while the sidebar has the category that shows this element selected.</summary>
	private bool IsSelected(GameObject element)
	{
		foreach (AppInterfaceSidebarItem item in m_sidebar.GetComponentsInChildren<AppInterfaceSidebarItem>(includeInactive: true))
		{
			if (item.RelatedName == element.name) { return item.Enabled; }
		}
		return false;
	}

	/// <summary>How many rows a page has, counted from the controls the panel itself built for them.</summary>
	private static int PageSize(GameObject element)
	{
		if (element == null) { return 0; }
		return element.GetComponentsInChildren<ToggleSwitch>(includeInactive: true).Length +
			element.GetComponentsInChildren<InputField>(includeInactive: true).Length;
	}

	public GameObject FindElement(string name)
	{
		Transform element = m_main.transform.Find(name);
		return element != null ? element.gameObject : null;
	}

	public void SetTitle(string text)
	{
		// The action bar's caption is legacy Text today and TextMeshPro once the panel is rebuilt from
		// Heat UI prefabs; SetLabelText writes into whichever of the two is there.
		HeatUiSkin.SetLabelText(m_actionBar, "Text", text);
		if (m_heatTitle != null) m_heatTitle.text = text;
		if (m_heatLayout != null) { m_heatLayout.GetComponent<HeatUiOptionHelpView>()?.SetPage(text); }
	}

	public void SetEnterButtonPosition(int x, int y)
	{
		((RectTransform)m_enterButton.transform).anchoredPosition = new Vector2(x, y);
	}

	public void SetEnterButtonAlpha(float alpha)
	{
		// Heat UI's button keeps its fade groups on its state children rather than on the root.
		CanvasGroup group = m_enterButton.GetComponent<CanvasGroup>();
		if (group == null) group = m_enterButton.GetComponentInChildren<CanvasGroup>(includeInactive: true);
		if (group != null) group.alpha = alpha;
	}

	private void OnEnterButtonClick()
	{
		SetEnabled(enabled: true);
	}

	private void OnBackButtonClick()
	{
		SetEnabled(enabled: false);
	}

	private void OnMenuButtonClick()
	{
		AppInterfaceSidebar component = m_sidebar.GetComponent<AppInterfaceSidebar>();
		component.SetEnabled(!component.Enabled);
	}

	private void SetEnabled(bool enabled)
	{
		if (enabled == m_enabled)
		{
			return;
		}
		m_enabled = enabled;
		m_enterButton.gameObject.SetActive(!enabled);
		m_panel.gameObject.SetActive(enabled);
		Singleton<GuiManager>.Instance.IsEnabled = !m_enabled;
		Singleton<GuiManager>.Instance.gameObject.SetActive(!m_enabled);
		if (m_enabled)
		{
			INContraption.Instance?.OnInterfaceEnabled();
			this.AppInterfaceEnabled?.Invoke();
			m_paused = GameTime.IsPaused();
			if (!m_paused)
			{
				GameTime.Pause(pause: true);
			}
		}
		else
		{
			INContraption.Instance?.OnInterfaceDisabled();
			this.AppInterfaceDisabled?.Invoke();
			// Only hand the time scale back when the game itself is not paused. This panel and the
			// game's own pause (Escape, focus loss, reward ads) both own the pause, and unpausing on
			// top of LevelManager's PausedWhileRunning used to leave the game running with a paused
			// game state — the ported drive, touch control and every other gameState-gated part went
			// dormant while the vehicle kept coasting, which is the "speed drops after closing the
			// pause menu" report. When the game is paused, stay paused and let Continue resume.
			LevelManager levelManager = WPFMonoBehaviour.levelManager;
			bool gamePaused = levelManager != null && (levelManager.gameState == LevelManager.GameState.PausedWhileRunning || levelManager.gameState == LevelManager.GameState.PausedWhileBuilding);
			if (!m_paused && !gamePaused)
			{
				GameTime.Pause(pause: false);
			}
			m_paused = false;
		}
	}
}
