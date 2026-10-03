using System;
using System.Collections.Generic;
using BadPiggies.Multiplayer.Online.EasyTier;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BadPiggies.Multiplayer.Online.Ui
{
	/// <summary>
	/// The multiplayer lobby window: a Division inspired glass command surface with a top navigation
	/// strip, blurred MainMenu backdrop, page area and status bar, plus the in-room view (member list,
	/// chat, invite and host controls).
	///
	/// The window is built from code on first use and kept alive across scenes. It is the only thing
	/// that talks to <see cref="OnlineLobbyService"/>; the pages only read the service's model and
	/// call its actions.
	/// </summary>
	public sealed class OnlineLobbyWindow : MonoBehaviour
	{
		public enum Page
		{
			Home,
			Create,
			Join,
			Lan,
			Recent,
			Settings
		}

		private const string CanvasName = "Online Lobby Canvas";
		private const string WindowName = "Window";
		private const string SidebarName = "Sidebar";
		private const string HeaderName = "Header";
		private const string ContentName = "Content";
		private const string StatusBarName = "StatusBar";
		private const string OverlayName = "Overlay";
		private const float RefreshIntervalSeconds = 0.25f;

		private static OnlineLobbyWindow s_instance;

		private readonly List<KeyValuePair<UnityEngine.UI.Button, Page>> m_navButtons = new List<KeyValuePair<UnityEngine.UI.Button, Page>>();
		private readonly List<KeyValuePair<Page, Image>> m_navIcons = new List<KeyValuePair<Page, Image>>();
		private readonly List<KeyValuePair<Page, Image>> m_navBars = new List<KeyValuePair<Page, Image>>();

		private Canvas m_canvas;
        private GameObject m_homeBackdrop;
        private TMP_Text m_homeBrand;
        private TMP_Text m_homeProfile;
        private Image m_homeBrandPlate;
        private Image m_homeProfilePlate;
		private OnlineLobbyBackdrop m_backdrop;
		private RectTransform m_window;
		private RectTransform m_content;
		private TMP_Text m_pageTitle;
		private TMP_Text m_statusText;
		private TMP_Text m_roomCodeText;
		private Image m_statusDot;
		private bool m_built;
		private bool m_open;
		private string m_lastBuildError = "";

		private Page m_page = Page.Home;
		private float m_nextRefresh;

		// Form state, kept here so switching pages does not lose what was typed.
		internal string CreateRoomName = "";
		internal string CreatePassword = "";
		internal int CreateMaxPlayers = 2;
		internal string CreatePort = OnlineNetConstants.DefaultGamePort.ToString();
		internal string CreateNode = OnlineNetConstants.DefaultExternalNode;
		internal string CreateAnnouncement = "";
		internal bool CreateBroadcast = true;
		internal bool CreateRelay = true;
		internal bool CreateLatencyFirst = true;

		internal string JoinText = "";
		internal string JoinPassword = "";
		internal string JoinStatus = "";

		internal string SettingsPlayerName = "";
		internal string SettingsNode = "";
		internal string SettingsNewNode = "";

		private OnlineRoomView m_roomView;
		private OnlineInviteOverlay m_inviteOverlay;
		private OnlineLobbyPages m_pages;

		public static OnlineLobbyWindow GetOrCreate()
		{
			if (s_instance != null)
			{
				return s_instance;
			}
			GameObject host = new GameObject("[Online Lobby UI]");
			if (Application.isPlaying)
			{
				UnityEngine.Object.DontDestroyOnLoad(host);
			}
			s_instance = host.AddComponent<OnlineLobbyWindow>();
			return s_instance;
		}

		public static OnlineLobbyWindow Instance
		{
			get { return s_instance; }
		}

		public bool IsOpen
		{
			get { return m_open; }
		}

		/// <summary>
		/// Why building the window failed, or "" when it built. The editor audit reads this so a
		/// broken build reports its real reason instead of a bare "canvas is null".
		/// </summary>
		public string LastBuildError
		{
			get { return m_lastBuildError; }
		}

		internal Canvas Canvas
		{
			get { return m_canvas; }
		}

		internal RectTransform Content
		{
			get { return m_content; }
		}

		private void Awake()
		{
			if (s_instance != null && s_instance != this)
			{
				UnityEngine.Object.Destroy(base.gameObject);
				return;
			}
			s_instance = this;
		}

		private void OnDestroy()
		{
			OnlineLobbyService service = OnlineLobbyService.Instance;
			if (service != null)
			{
				service.StateChanged -= OnServiceStateChanged;
			}
			if (s_instance == this)
			{
				s_instance = null;
			}
		}

		// ---------------------------------------------------------------- open / close

		public void Open()
		{
			EnsureBuilt();
			if (m_canvas == null)
			{
				return;
			}
			OnlineLobbyService service = OnlineLobbyService.GetOrCreate();
			service.StateChanged -= OnServiceStateChanged;
			service.StateChanged += OnServiceStateChanged;
			OnlinePrefs.EnsureLoaded();
			SettingsPlayerName = OnlinePrefs.PlayerName;
			m_open = true;
			m_canvas.gameObject.SetActive(true);
			if (m_backdrop != null)
			{
				m_backdrop.Refresh();
			}
			ShowPage(service.IsInRoom ? Page.Home : m_page);
			RefreshAll();
		}

		public void Close()
		{
			m_open = false;
			if (m_canvas != null)
			{
				m_canvas.gameObject.SetActive(false);
			}
			if (m_inviteOverlay != null)
			{
				m_inviteOverlay.Hide();
			}
			MultiplayerGameInputBlock.Release(this);
		}

		public void Toggle()
		{
			if (m_open)
			{
				Close();
			}
			else
			{
				Open();
			}
		}

		private void Update()
		{
			MultiplayerGameInputBlock.Apply(this, m_open);
			if (!m_open)
			{
				return;
			}
			if (Input.GetKeyDown(KeyCode.Escape))
			{
				if (m_inviteOverlay != null && m_inviteOverlay.IsVisible)
				{
					m_inviteOverlay.Hide();
				}
				else
				{
					Close();
				}
				return;
			}
			if (Time.unscaledTime >= m_nextRefresh)
			{
				m_nextRefresh = Time.unscaledTime + RefreshIntervalSeconds;
				RefreshAll();
			}
		}

		private void OnServiceStateChanged()
		{
			if (m_open)
			{
				RefreshAll();
			}
		}

		// ---------------------------------------------------------------- construction

		private void EnsureBuilt()
		{
			if (m_built)
			{
				return;
			}
			m_built = true;
			m_lastBuildError = "";
			try
			{
				m_canvas = MctierWidgets.CreateCanvas(CanvasName, 240, transform);
				if (m_canvas == null)
				{
					m_lastBuildError = "CreateCanvas returned null";
					Debug.LogError("[Online] Could not create the lobby canvas.");
					return;
				}
				BuildShell(m_canvas.transform);
				m_roomView = new OnlineRoomView(this);
				m_inviteOverlay = new OnlineInviteOverlay(this);
				m_pages = new OnlineLobbyPages(this);
				m_canvas.gameObject.SetActive(false);
			}
			catch (Exception ex)
			{
				m_lastBuildError = ex.ToString();
				Debug.LogError("[Online] Building the lobby UI failed: " + ex);
				m_canvas = null;
			}
		}

		private void BuildShell(Transform root)
		{
			// The MainMenu camera is captured at half resolution and blurred before the glass shell is
			// drawn. In edit mode the component simply remains a transparent fallback.
			GameObject backdropObject = new GameObject("MainMenuBackdrop", typeof(RectTransform), typeof(RawImage), typeof(OnlineLobbyBackdrop));
			backdropObject.transform.SetParent(root, false);
			RectTransform backdropRect = (RectTransform)backdropObject.transform;
			MctierWidgets.Stretch(backdropRect);
			m_backdrop = backdropObject.GetComponent<OnlineLobbyBackdrop>();
			m_backdrop.Initialize();
            GameObject homeBackground = Resources.Load<GameObject>("HeatUI/Online Home Backdrop");
            if (homeBackground != null)
            {
                m_homeBackdrop = UnityEngine.Object.Instantiate(homeBackground, root, false);
                MctierWidgets.Stretch((RectTransform)m_homeBackdrop.transform);
                m_homeBackdrop.GetComponent<Image>().color = new Color(.64f,.66f,.76f,1f);
                m_homeBackdrop.SetActive(false);
            }


			// The translucent veil preserves the cold blue tone of the Division command surface while
			// still letting the blurred MainMenu image read through the panels.
			Image overlay = MctierWidgets.CreateImage(OverlayName, root, MctierTheme.Overlay);
			MctierWidgets.Stretch((RectTransform)overlay.transform);
			overlay.raycastTarget = true;

			Image window = MctierWidgets.CreatePanel(WindowName, root, MctierTheme.WindowBg, 0);
			m_window = (RectTransform)window.transform;
			MctierWidgets.Stretch(m_window);

			BuildSidebar(m_window);
			BuildHeader(m_window);
			BuildStatusBar(m_window);

			RectTransform content = MctierWidgets.CreateNode(ContentName, m_window);
			// Floating glass panels keep a broad margin around the blurred backdrop and leave room for
			// the compact top command strip and footer telemetry.
			MctierWidgets.SetAnchors(content, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(48f, 72f), new Vector2(-48f, -208f));
			m_content = content;
            m_homeBrandPlate = MctierWidgets.CreateImage("HomeBrandPlate", m_window, MctierTheme.TextPrimary);
            MctierWidgets.SetAnchors((RectTransform)m_homeBrandPlate.transform,new Vector2(0f,1f),new Vector2(0f,1f),new Vector2(48f,-98f),new Vector2(192f,-42f));
            m_homeBrand = MctierWidgets.CreateHeading("HomeBrand", m_window, "BPRC", 30f);
            MctierWidgets.SetAnchors((RectTransform)m_homeBrand.transform, new Vector2(0f,1f), new Vector2(0f,1f), new Vector2(48f,-98f),new Vector2(192f,-42f));
            m_homeBrand.color = MctierTheme.OnAccent;
            m_homeBrand.alignment = TextAlignmentOptions.Center;
            m_homeProfilePlate = MctierWidgets.CreateImage("HomeProfilePlate", m_window, MctierTheme.TextPrimary);
            MctierWidgets.SetAnchors((RectTransform)m_homeProfilePlate.transform,Vector2.zero,Vector2.zero,new Vector2(48f,44f),new Vector2(392f,84f));
            m_homeProfile = MctierWidgets.CreateCaption("HomeProfile", m_window, "", 17f);
            MctierWidgets.SetAnchors((RectTransform)m_homeProfile.transform, Vector2.zero,new Vector2(0f,0f),new Vector2(60f,44f),new Vector2(384f,84f));

		}

        private void BuildSidebar(RectTransform parent)
        {
            RectTransform rail = MctierWidgets.CreateNode(SidebarName, parent);
            MctierWidgets.SetAnchors(rail, new Vector2(0f,1f), Vector2.one, new Vector2(48f,-184f), new Vector2(-48f,-112f));
            RectTransform nav = MctierWidgets.CreateNode("Nav", rail);
            MctierWidgets.Stretch(nav);
            HorizontalLayoutGroup layout = nav.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleLeft;
            AddNavButton(nav, "NavHome", "联机概览", Page.Home, "Home");
            AddNavButton(nav, "NavCreate", "创建房间", Page.Create, "PlayCircle");
            AddNavButton(nav, "NavJoin", "加入房间", Page.Join, "SearchGames");
            AddNavButton(nav, "NavLan", "局域网", Page.Lan, "Multiplayer");
            AddNavButton(nav, "NavRecent", "最近与收藏", Page.Recent, "Chapters");
            AddNavButton(nav, "NavSettings", "联机设置", Page.Settings, "Settings");
            RectTransform status = MctierWidgets.CreateNode("NetworkStatus", parent);
            MctierWidgets.SetAnchors(status, new Vector2(1f,1f),Vector2.one,new Vector2(-360f,-92f),new Vector2(-108f,-42f));
            m_statusDot = MctierWidgets.CreateDot("NetDot", status, MctierTheme.TextMuted, 8f);
            MctierWidgets.SetAnchors((RectTransform)m_statusDot.transform,new Vector2(0f,.5f),new Vector2(0f,.5f),new Vector2(0f,-4f),new Vector2(8f,4f));
            TMP_Text label = MctierWidgets.CreateCaption("NetStatus", status, "直连模式", 13f);
            MctierWidgets.Stretch((RectTransform)label.transform,20f,0f,0f,0f);
        }

		/// <summary>
		/// A rail item in Heat UI's "Button (Panel Alt)" style, the shape its own settings panel uses:
		/// 50px tall, a 6% surface that turns 6% accent when selected, an 18px icon at x=31, the label
		/// from x=64, a 1px bottom hairline, and a 3px accent bar marking the selection.
		/// </summary>
		private void AddNavButton(RectTransform nav, string name, string label, Page page, string icon)
		{
			UnityEngine.UI.Button button = MctierWidgets.CreateGhostButton(name, nav, label, new Action(() => ShowPage(page)), 16f);
			LayoutElement element = MctierWidgets.EnsureLayoutElement(button.gameObject);
			element.minHeight = 54f;
			element.preferredHeight = 54f;
				element.minWidth = 130f;
				element.preferredWidth = 130f;
            element.flexibleWidth = 1f;
			element.flexibleHeight = 0f;

			Image fill = button.GetComponent<Image>();
			if (fill != null)
			{
				fill.color = Color.clear;
			}

            foreach (TMP_Text caption in button.GetComponentsInChildren<TMP_Text>(true))
            {
                caption.fontSize = 18f;
                caption.alignment = TextAlignmentOptions.Center;
            }

			MctierWidgets.AddOutline(button.GetComponent<Image>(), 1, MctierTheme.TextPrimary, 2).name = "HomeFrame";
            Image homeFill = MctierWidgets.CreateImage("HomeTabFill", button.transform, MctierTheme.Accent);
            MctierWidgets.Stretch((RectTransform)homeFill.transform, 5f);
            homeFill.raycastTarget = false;
            TMP_Text homeCaption = MctierWidgets.CreateCaption("HomeTabCaption", button.transform, label, 18f);
            MctierWidgets.Stretch((RectTransform)homeCaption.transform, 8f, 2f, 8f, 2f);
            homeCaption.alignment = TextAlignmentOptions.Center;

            m_navButtons.Add(new KeyValuePair<UnityEngine.UI.Button, Page>(button, page));
		}

		private void BuildHeader(RectTransform parent)
		{
			RectTransform header = MctierWidgets.CreateNode(HeaderName, parent);
			MctierWidgets.SetAnchors(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(48f, -104f), new Vector2(-48f, -24f));

			m_pageTitle = MctierWidgets.CreateHeading("PageTitle", header, "联机概览", 32f);
			RectTransform titleRect = (RectTransform)m_pageTitle.transform;
			MctierWidgets.SetAnchors(titleRect, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(-400f, 0f));
			m_pageTitle.alignment = TextAlignmentOptions.Left;

			UnityEngine.UI.Button close = MctierWidgets.CreateCircleButton("Close", header, "×", new Action(Close), 42f);
			RectTransform closeRect = (RectTransform)close.transform;
			closeRect.anchorMin = new Vector2(1f, 0.5f);
			closeRect.anchorMax = new Vector2(1f, 0.5f);
			closeRect.pivot = new Vector2(1f, 0.5f);
			closeRect.anchoredPosition = new Vector2(0f, 0f);
			closeRect.sizeDelta = new Vector2(42f, 42f);
            Image closeImage = close.GetComponent<Image>();
            closeImage.sprite = MctierWidgets.HeatCircle();
            closeImage.type = Image.Type.Simple;
            closeImage.color = MctierTheme.TextPrimary;
            TMP_Text closeText = close.GetComponentInChildren<TMP_Text>();
            closeText.color = MctierTheme.OnAccent;
            closeText.fontSize = 18f;
            Transform closeOutline = close.transform.Find("Outline");
            if (closeOutline != null) closeOutline.gameObject.SetActive(false);
		}

		private void BuildStatusBar(RectTransform parent)
		{
			Image bar = MctierWidgets.CreatePanel(StatusBarName, parent, MctierTheme.SidebarBg, 0);
			RectTransform rect = (RectTransform)bar.transform;
			MctierWidgets.SetAnchors(rect, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(48f, 0f), new Vector2(-48f, 52f));
			bar.color = Color.clear;
			bar.raycastTarget = false;

			m_statusText = MctierWidgets.CreateCaption("StatusText", rect, "", 15f);
			RectTransform statusRect = (RectTransform)m_statusText.transform;
			MctierWidgets.SetAnchors(statusRect, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(32f, 0f), new Vector2(-280f, 0f));
			m_statusText.alignment = TextAlignmentOptions.Left;

			m_roomCodeText = MctierWidgets.CreateCaption("RoomCode", rect, "", 15f);
			RectTransform codeRect = (RectTransform)m_roomCodeText.transform;
			MctierWidgets.SetAnchors(codeRect, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-280f, 0f), new Vector2(-44f, 0f));
			m_roomCodeText.alignment = TextAlignmentOptions.Right;
			m_roomCodeText.color = MctierTheme.Accent;
		}

		// ---------------------------------------------------------------- pages

		public void ShowPage(Page page)
		{
			m_page = page;
            ApplyHomeShell(page == Page.Home);
			if (m_pageTitle != null)
			{
				m_pageTitle.text = PageTitle(page);
			}
			for (int i = 0; i < m_navButtons.Count; i++)
			{
				bool active = m_navButtons[i].Value == page;
                Michsky.UI.Heat.PanelButton panel = m_navButtons[i].Key.GetComponent<Michsky.UI.Heat.PanelButton>();
                if (panel != null) panel.SetSelected(active);
				Image image = m_navButtons[i].Key.GetComponent<Image>();
				if (image != null)
				{
					// Heat UI marks the selected item with a 6% accent wash, not a solid fill.
					image.color = active ? MctierTheme.WithAlpha(MctierTheme.Accent, 0.06f)
					                     : MctierTheme.WithAlpha(MctierTheme.TextPrimary, 0.025f);
				}
				Michsky.UI.Heat.ButtonManager heat = m_navButtons[i].Key.GetComponentInChildren<Michsky.UI.Heat.ButtonManager>(true);
				if (heat != null)
				{
					if (heat.normalImageObj != null)
					{
						heat.normalImageObj.color = active ? MctierTheme.WithAlpha(MctierTheme.Accent, 0.06f)
							: MctierTheme.WithAlpha(MctierTheme.TextPrimary, 0.025f);
					}
					MctierWidgets.ApplyHeatButtonStateColor(heat, "Normal", active ? MctierTheme.WithAlpha(MctierTheme.Accent, 0.06f) : MctierTheme.WithAlpha(MctierTheme.TextPrimary, 0.025f));
					heat.UpdateUI();
				}
				TMP_Text caption = OnlineLobbyPages.Find(m_navButtons[i].Key.gameObject, MctierWidgets.LabelChildName);
				if (caption != null)
				{
					caption.color = active ? MctierTheme.Accent : MctierTheme.TextPrimary;
					caption.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
				}
			}
			for (int i = 0; i < m_navIcons.Count; i++)
			{
				Image icon = m_navIcons[i].Value;
				if (icon != null)
				{
					icon.color = m_navIcons[i].Key == page ? MctierTheme.Accent : MctierTheme.TextSecondary;
				}
			}
			for (int i = 0; i < m_navBars.Count; i++)
			{
				Image bar = m_navBars[i].Value;
				if (bar != null)
				{
					bar.gameObject.SetActive(m_navBars[i].Key == page);
				}
			}
			if (m_pages != null)
			{
				m_pages.Show(page);
			}
            StyleHomeTabs(page == Page.Home);
		}

        private void StyleHomeTabs(bool home)
        {
            foreach (var entry in m_navButtons)
            {
                bool selected = entry.Value == m_page;
                Transform frame = entry.Key.transform.Find("HomeFrame");
                if (frame != null) { frame.gameObject.SetActive(home); frame.SetAsLastSibling(); }
                Transform fill = entry.Key.transform.Find("HomeTabFill");
                fill.gameObject.SetActive(home && selected);
                fill.SetAsLastSibling();
                entry.Key.GetComponent<Image>().color = Color.clear;
                foreach (TMP_Text caption in entry.Key.GetComponentsInChildren<TMP_Text>(true))
                {
                    bool homeLabel = caption.name == "HomeTabCaption";
                    caption.gameObject.SetActive(homeLabel ? home : !home);
                    caption.color = selected ? (homeLabel ? MctierTheme.OnAccent : MctierTheme.Accent) : MctierTheme.TextPrimary;
                    if (homeLabel) caption.transform.SetAsLastSibling();
                }
                foreach (Image image in entry.Key.GetComponentsInChildren<Image>(true))
                    if (image.name == "Background")
                    {
                        Michsky.UI.Heat.UIManagerImage tint = image.GetComponent<Michsky.UI.Heat.UIManagerImage>();
                        if (tint != null) { tint.useCustomColor = true; tint.enabled = false; }
                        image.color = home ? Color.clear : new Color(1f,1f,1f,.04f);
                    }
            }
        }

        private void ApplyHomeShell(bool home)
        {
            if (m_window == null) return;
            if (m_homeBackdrop != null) m_homeBackdrop.SetActive(home);
            if (m_homeBrand != null) m_homeBrand.gameObject.SetActive(home);
            if (m_homeBrandPlate != null) m_homeBrandPlate.gameObject.SetActive(home);
            if (m_homeProfilePlate != null) m_homeProfilePlate.gameObject.SetActive(home);
            if (m_homeProfile != null)
            {
                m_homeProfile.gameObject.SetActive(home);
                m_homeProfile.text = "玩家 / " + OnlinePrefs.PlayerName;
                m_homeProfile.color = MctierTheme.OnAccent;
            }
            if (m_pageTitle != null) m_pageTitle.gameObject.SetActive(!home);
            RectTransform nav = (RectTransform)m_window.Find(SidebarName);
            MctierWidgets.SetAnchors(nav,new Vector2(0f,1f),Vector2.one,
                home ? new Vector2(210f,-98f) : new Vector2(48f,-184f),
                home ? new Vector2(-112f,-42f) : new Vector2(-48f,-112f));
            if (m_content != null) MctierWidgets.SetAnchors(m_content,Vector2.zero,Vector2.one,
                home ? new Vector2(48f,128f) : new Vector2(48f,72f),
                home ? new Vector2(-48f,-152f) : new Vector2(-48f,-208f));
            Transform network = m_window.Find("NetworkStatus");
            if (network != null) network.gameObject.SetActive(!home);
        }

		internal static string PageTitle(Page page)
		{
			switch (page)
			{
			case Page.Create:
				return "创建房间";
			case Page.Join:
				return "加入房间";
			case Page.Lan:
				return "局域网房间";
			case Page.Recent:
				return "最近与收藏";
			case Page.Settings:
				return "联机设置";
			default:
				return "联机概览";
			}
		}

		// ---------------------------------------------------------------- refresh

		public void RefreshAll()
		{
			if (!m_built)
			{
				return;
			}
			OnlineLobbyService service = OnlineLobbyService.Instance;
			if (service == null)
			{
				return;
			}
			bool inRoom = service.IsInRoom;
            ApplyHomeShell(!inRoom && m_page == Page.Home);
            if (m_pageTitle != null) m_pageTitle.text = inRoom ? "房间 / 集结队伍" : PageTitle(m_page);
            for (int i = 0; i < m_navButtons.Count; i++) m_navButtons[i].Key.interactable = !inRoom;
			if (m_roomView != null)
			{
				m_roomView.SetVisible(inRoom);
			}
			if (m_pages != null)
			{
				m_pages.SetVisible(!inRoom);
				if (!inRoom)
				{
					m_pages.Refresh(m_page);
				}
			}
			if (m_roomView != null && inRoom)
			{
				m_roomView.Refresh();
			}

			if (m_statusText != null)
			{
				m_statusText.text = string.IsNullOrEmpty(service.StatusText) ? "未连接" : service.StatusText;
				m_statusText.color = service.Phase == OnlineSessionPhase.Failed ? MctierTheme.Danger : MctierTheme.TextSecondary;
			}
			if (m_roomCodeText != null)
			{
				m_roomCodeText.text = string.IsNullOrEmpty(service.RoomCode) ? "" : "房间号 " + service.RoomCode;
			}
			if (m_statusDot != null)
			{
				m_statusDot.color = StatusColor(service);
			}
			TMP_Text netStatus = FindLabel(m_window, "NetStatus");
			if (netStatus != null)
			{
				netStatus.text = DescribeNetwork(service);
			}
		}

		private static Color StatusColor(OnlineLobbyService service)
		{
			if (service.Phase == OnlineSessionPhase.Failed)
			{
				return MctierTheme.Danger;
			}
			if (service.Phase == OnlineSessionPhase.InRoom || service.Phase == OnlineSessionPhase.InMatch)
			{
				return MctierTheme.Success;
			}
			if (service.Phase == OnlineSessionPhase.Offline)
			{
				return MctierTheme.TextMuted;
			}
			return MctierTheme.Warning;
		}

		private static string DescribeNetwork(OnlineLobbyService service)
		{
			if (!EasyTierPaths.IsSupportedPlatform)
			{
				return "当前平台不支持 EasyTier，只能直连。";
			}
			if (!EasyTierPaths.IsInstalled)
			{
				return "未找到 EasyTier 运行文件，请先运行 Tools/Online/fetch-easytier.ps1。";
			}
			EasyTierRunner runner = EasyTierRunner.GetOrCreate();
			if (runner == null)
			{
				return "EasyTier 未初始化。";
			}
			switch (runner.State)
			{
			case EasyTierState.Running:
				EasyTierMonitor monitor = EasyTierMonitor.GetOrCreate();
				int peers = monitor == null ? 0 : monitor.Peers.Count;
				return "虚拟局域网已连接（" + peers + " 个节点）";
			case EasyTierState.Starting:
				return "正在启动虚拟局域网…";
			case EasyTierState.Failed:
				return "虚拟局域网启动失败：" + runner.LastError;
			case EasyTierState.NotInstalled:
			case EasyTierState.Unsupported:
				return "当前平台无法使用 EasyTier。";
			default:
				return "虚拟局域网未启动";
			}
		}

		internal static TMP_Text FindLabel(Transform root, string name)
		{
			if (root == null)
			{
				return null;
			}
			TMP_Text[] labels = root.GetComponentsInChildren<TMP_Text>(true);
			for (int i = 0; i < labels.Length; i++)
			{
				if (labels[i].name == name)
				{
					return labels[i];
				}
			}
			return null;
		}

		internal OnlineRoomView RoomView
		{
			get { return m_roomView; }
		}

		internal void ShowInviteOverlay()
		{
			if (m_inviteOverlay != null)
			{
				m_inviteOverlay.Show();
			}
		}
	}
}
