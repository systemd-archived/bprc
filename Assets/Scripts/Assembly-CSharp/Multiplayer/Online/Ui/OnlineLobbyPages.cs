using System;
using System.Collections.Generic;
using BadPiggies.Multiplayer.Online.EasyTier;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BadPiggies.Multiplayer.Online.Ui
{
	/// <summary>
	/// The six pages of the lobby window's navigation rail: 首页, 创建房间, 加入房间, 局域网房间,
	/// 最近与收藏 and 设置. All pages are built once and toggled, so switching is instant and typed
	/// form values survive a round trip.
	/// </summary>
	internal sealed class OnlineLobbyPages
	{
		private readonly OnlineLobbyWindow m_window;
		private readonly Dictionary<OnlineLobbyWindow.Page, RectTransform> m_pages = new Dictionary<OnlineLobbyWindow.Page, RectTransform>();

		/// <summary>Scroll-view roots of scrollable pages, keyed by the page node Show() toggles.</summary>
		private readonly Dictionary<RectTransform, GameObject> m_pageWrappers = new Dictionary<RectTransform, GameObject>();

		// Create page
		private TMP_InputField m_roomNameInput;
		private TMP_InputField m_passwordInput;
		private TMP_InputField m_portInput;
		private TMP_InputField m_nodeInput;
		private TMP_InputField m_announcementInput;
		private TMP_Text m_maxPlayersLabel;
		private Toggle m_broadcastToggle;
		private Toggle m_relayToggle;
		private Toggle m_latencyToggle;

		// Join page
		private TMP_InputField m_joinInput;
		private TMP_InputField m_joinPasswordInput;
		private TMP_Text m_joinStatus;

		// LAN page
		private RectTransform m_lanList;
		private TMP_Text m_lanHint;

		// Recent page
		private RectTransform m_recentList;
		private TMP_Text m_recentHint;

		// Settings page
		private TMP_InputField m_playerNameInput;
		private RectTransform m_nodeList;
		private TMP_InputField m_newNodeInput;
		private Toggle m_settingsBroadcastToggle;
	private Toggle m_settingsVirtualLanToggle;

		// Home page


		public OnlineLobbyPages(OnlineLobbyWindow window)
		{
			m_window = window;
			Transform parent = window.Content;
			m_pages[OnlineLobbyWindow.Page.Home] = BuildHome(parent);
			m_pages[OnlineLobbyWindow.Page.Create] = BuildCreate(parent);
			m_pages[OnlineLobbyWindow.Page.Join] = BuildJoin(parent);
			m_pages[OnlineLobbyWindow.Page.Lan] = BuildLan(parent);
			m_pages[OnlineLobbyWindow.Page.Recent] = BuildRecent(parent);
			m_pages[OnlineLobbyWindow.Page.Settings] = BuildSettings(parent);
		}

		public void SetVisible(bool visible)
		{
			foreach (KeyValuePair<OnlineLobbyWindow.Page, RectTransform> entry in m_pages)
			{
				if (entry.Value != null)
				{
					entry.Value.gameObject.SetActive(visible && entry.Key == m_current);
                    GameObject wrapper;
                    if (m_pageWrappers.TryGetValue(entry.Value, out wrapper)) wrapper.SetActive(visible && entry.Key == m_current);
				}
			}
		}

		private OnlineLobbyWindow.Page m_current = OnlineLobbyWindow.Page.Home;

		public void Show(OnlineLobbyWindow.Page page)
		{
			m_current = page;
			foreach (KeyValuePair<OnlineLobbyWindow.Page, RectTransform> entry in m_pages)
			{
				if (entry.Value != null)
				{
					bool active = entry.Key == page;
					entry.Value.gameObject.SetActive(active);
					// A scrollable page's root is a wrapper above the returned content node, so the
					// wrapper (and its scrollbar) has to follow the content's visibility too.
					GameObject wrapper;
					if (m_pageWrappers.TryGetValue(entry.Value, out wrapper) && wrapper != null)
					{
						wrapper.SetActive(active);
					}
				}
			}
			Refresh(page);
		}

		/// <summary>
		/// A page root. A positive <paramref name="contentHeight"/> wraps the page in a scroll view
		/// whose content keeps that height, so form pages can be taller than the window instead of
		/// stacking their bottom controls on top of the ones above them.
		/// </summary>
		private RectTransform CreatePage(Transform parent, string name, float contentHeight = 0f)
		{
			if (contentHeight <= 0f)
			{
				RectTransform plain = MctierWidgets.CreateNode(name, parent);
				MctierWidgets.Stretch(plain, 0f);
				return plain;
			}
			ScrollRect scroll = MctierWidgets.CreateScrollView(name + "Scroll", parent, out RectTransform content,
				true, false, contentHeight);
			MctierWidgets.Stretch((RectTransform)scroll.transform, 0f);
			content.name = name;
			m_pageWrappers[content] = scroll.gameObject;
			return content;
		}

		internal static TMP_Text Body(Transform parent, string text, float size = 15f)
		{
			TMP_Text label = MctierWidgets.CreateCaption("Hint", parent, text, size);
			label.enableWordWrapping = true;
			label.color = MctierTheme.TextMuted;
			return label;
		}

		/// <summary>
		/// Anchors a band to the top of the parent: <paramref name="top"/> and <paramref name="bottom"/>
		/// are both distances from the parent's top edge, so bottom must be the larger number.
		/// offsetMin is the rect's bottom edge, which is the *further* one in a top-anchored band —
		/// swapping them makes the rect height negative and TextMeshPro then draws nothing at all.
		/// </summary>
		internal static void Place(RectTransform rect, float left, float top, float right, float bottom)
		{
			MctierWidgets.SetAnchors(rect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(left, -bottom), new Vector2(-right, -top));
		}

		// ---------------------------------------------------------------- home

        private RectTransform BuildHome(Transform parent)
        {
            GameObject template = Resources.Load<GameObject>("HeatUI/Online Home Layout");
            if (template == null) throw new InvalidOperationException("Missing Heat UI Online Home Layout. Run HeatUiLayoutExporter.ExportOnlineHome.");
            GameObject instance = UnityEngine.Object.Instantiate(template, parent, false);
            instance.name = "Page_Home";
            RectTransform page = (RectTransform)instance.transform;
            MctierWidgets.Stretch(page);
            instance.AddComponent<OnlineHeatHome>().Initialize(m_window);
            return page;
        }

		// ---------------------------------------------------------------- create

        private RectTransform BuildCreate(Transform parent)
        {
            RectTransform page = CreatePage(parent, "Page_Create", 650f);
            TMP_Text hint = Body(page, "设置房间信息，然后邀请好友加入。房间号会在创建后自动生成。");
            Place((RectTransform)hint.transform, 0f, 0f, 0f, 36f);
            RectTransform basic = FormSection(page, "RoomDetails", "01 / 房间信息", 0f, 0.5f, 52f, 516f);
            RectTransform network = FormSection(page, "NetworkOptions", "02 / 连接选项", 0.5f, 1f, 52f, 516f);
            m_roomNameInput = StackedInput(basic, "RoomName", "房间名称", "例如：随便玩玩", 66f);
            m_passwordInput = StackedInput(basic, "Password", "房间密码 / 可选", "留空则不设置密码", 154f);
            m_passwordInput.contentType = TMP_InputField.ContentType.Password;
            m_announcementInput = StackedInput(basic, "Announcement", "房间公告 / 可选", "给加入的好友留句话", 242f);
            TMP_Text maxLabel = MctierWidgets.CreateCaption("MaxPlayersLabel", basic, "人数上限", 15f);
            Place((RectTransform)maxLabel.transform, 20f, 340f, 20f, 370f);
            UnityEngine.UI.Button minus = MctierWidgets.CreateSmallButton("MaxMinus", basic, "－", MctierTheme.CardBg, MctierTheme.TextPrimary, () => StepMaxPlayers(-1), 16f);
            PlaceFixedAtLeft((RectTransform)minus.transform, 20f, 378f, 42f, 36f);
            m_maxPlayersLabel = MctierWidgets.CreateCaption("MaxPlayersValue", basic, "2 人", 16f);
            m_maxPlayersLabel.alignment = TextAlignmentOptions.Center;
            PlaceFixedAtLeft((RectTransform)m_maxPlayersLabel.transform, 68f, 378f, 80f, 36f);
            UnityEngine.UI.Button plus = MctierWidgets.CreateSmallButton("MaxPlus", basic, "＋", MctierTheme.CardBg, MctierTheme.TextPrimary, () => StepMaxPlayers(1), 16f);
            PlaceFixedAtLeft((RectTransform)plus.transform, 154f, 378f, 42f, 36f);
            TMP_Text maxHint = Body(basic, "前两位上场对战，其余成员观战。", 13f);
            Place((RectTransform)maxHint.transform, 20f, 422f, 20f, 450f);
            m_portInput = StackedInput(network, "Port", "游戏端口", "7777", 66f);
            m_portInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            m_nodeInput = StackedInput(network, "Node", "EasyTier 节点 / 可选", "直连或局域网无需填写", 154f);
            m_broadcastToggle = MctierWidgets.CreateSwitch("ToggleBroadcast", network, true, null);
            PlaceToggle((RectTransform)m_broadcastToggle.transform, "允许局域网发现", 252f);
            m_relayToggle = MctierWidgets.CreateSwitch("ToggleRelay", network, true, null);
            PlaceToggle((RectTransform)m_relayToggle.transform, "中继局域网广播", 320f);
            m_latencyToggle = MctierWidgets.CreateSwitch("ToggleLatency", network, true, null);
            PlaceToggle((RectTransform)m_latencyToggle.transform, "延迟优先", 388f);
            UnityEngine.UI.Button create = MctierWidgets.CreatePrimaryButton("CreateButton", page, "创建房间", new Action(CreateRoom), 18f);
            PlaceFixedAtLeft((RectTransform)create.transform, 0f, 538f, 220f, 48f);
            TMP_Text error = MctierWidgets.CreateCaption("CreateError", page, "", 14f);
            error.color = MctierTheme.Danger;
            error.enableWordWrapping = true;
            Place((RectTransform)error.transform, 0f, 598f, 0f, 646f);
            return page;
        }

        private static RectTransform FormSection(RectTransform parent, string name, string title,
            float left, float right, float top, float bottom)
        {
            Image card = MctierWidgets.CreateCard(name, parent, 2);
            RectTransform rect = (RectTransform)card.transform;
            MctierWidgets.SetAnchors(rect, new Vector2(left,1f), new Vector2(right,1f),
                new Vector2(left == 0f ? 0f : 10f,-bottom), new Vector2(right == 1f ? 0f : -10f,-top));
            TMP_Text label = MctierWidgets.CreateCaption("SectionTitle", rect, title, 16f);
            label.color = MctierTheme.Accent;
            Place((RectTransform)label.transform, 20f, 12f, 20f, 46f);
            Image line = MctierWidgets.CreateSeparator("SectionEdge", rect);
            Place((RectTransform)line.transform, 20f, 52f, 20f, 53f);
            return rect;
        }

        private static TMP_InputField StackedInput(RectTransform parent, string name, string title, string placeholder, float top)
        {
            TMP_Text label = MctierWidgets.CreateCaption(name + "Label", parent, title, 14f);
            Place((RectTransform)label.transform, 20f, top, 20f, top + 26f);
            TMP_InputField input = MctierWidgets.CreateInput(name, parent, placeholder, 16f);
            Place((RectTransform)input.transform, 20f, top + 32f, 20f, top + 74f);
            return input;
        }

		private string m_roomNameField = "";
		private string m_passwordField = "";
		private string m_portField = "";
		private string m_nodeField = "";
		private string m_announcementField = "";
		private string m_playerNameField = "";

		/// <summary>
		/// A Heat UI settings row, the unit its own settings panel is built from: full-width surface
		/// (white at 4%), the label on the left with 20px padding, a 250px control column on the
		/// right, and a 1px hairline along the bottom. Row height and the 20/250 split are Heat UI's
		/// numbers, measured off "Settings Element (Switch)" (1000x60 with a -270 label inset).
		/// </summary>
		private const float PanelRowHeight = 60f;
		private const float PanelRowLabelRight = 450f;
		private const float PanelRowControlWidth = 420f;

		private static RectTransform PanelRow(Transform page, string name, float top, string caption)
		{
			Image row = MctierWidgets.CreateImage(name + "Row", page, MctierTheme.CardBg);
			RectTransform rect = (RectTransform)row.transform;
			Place(rect, 0f, top, 0f, top + PanelRowHeight);

			Image edge = MctierWidgets.CreateImage(name + "Edge", rect, MctierTheme.WithAlpha(MctierTheme.TextPrimary, 0.08f));
			MctierWidgets.SetAnchors((RectTransform)edge.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 1f));
			edge.raycastTarget = false;

			TMP_Text label = MctierWidgets.CreateCaption(name + "Label", rect, caption, 15f);
            label.enableWordWrapping = true;
			MctierWidgets.SetAnchors((RectTransform)label.transform, new Vector2(0f, 0f), new Vector2(1f, 1f),
				new Vector2(20f, 12f), new Vector2(-PanelRowLabelRight, -12f));
			return rect;
		}

		private TMP_InputField LabeledInput(Transform page, string name, string caption, string placeholder, float top, ref string backing)
		{
			RectTransform row = PanelRow(page, name, top, caption);
			TMP_InputField input = MctierWidgets.CreateInput(name, row, placeholder, 16f);
			RectTransform rect = (RectTransform)input.transform;
			PlaceFixedAtRight(rect, 20f, (PanelRowHeight - 36f) * 0.5f, PanelRowControlWidth, 36f);
			backing = input.text;
			string captured = name;
			input.onEndEdit.AddListener(delegate(string value) { OnFieldChanged(captured, value); });
			return input;
		}

		private void OnFieldChanged(string name, string value)
		{
			switch (name)
			{
			case "RoomName":
				m_roomNameField = value;
				break;
			case "Password":
				m_passwordField = value;
				break;
			case "Port":
				m_portField = value;
				break;
			case "Node":
				m_nodeField = value;
				break;
			case "Announcement":
				m_announcementField = value;
				break;
			}
		}

		/// <summary>Turns a switch into a full Heat UI settings row: label left, switch right.</summary>
		private void PlaceToggle(RectTransform toggle, string caption, float top)
		{
			Transform page = toggle.parent;
			RectTransform row = PanelRow(page, toggle.name, top, caption);
			toggle.SetParent(row, false);
            TMP_Text label = Find(row.gameObject, toggle.name + "Label");
            if (label != null) ((RectTransform)label.transform).offsetMax = new Vector2(-92f, -12f);
			PlaceFixedAtRight(toggle, 20f, (PanelRowHeight - 26f) * 0.5f, 50f, 26f);
		}

		private void StepMaxPlayers(int delta)
		{
			int value = Mathf.Clamp(m_window.CreateMaxPlayers + delta, OnlineNetConstants.MinPlayers, OnlineNetConstants.MaxRoomPlayers);
			m_window.CreateMaxPlayers = value;
			if (m_maxPlayersLabel != null)
			{
				m_maxPlayersLabel.text = value + " 人";
			}
		}

		private void CreateRoom()
		{
			// Direct/LAN is the default and needs no server and no extra executable. The virtual
			// LAN is opt-in from Settings, and even then it only uses nodes the user supplied.
			bool useVirtualLan = OnlinePrefs.EnableVirtualLan && EasyTierPaths.IsInstalled && EasyTierPaths.IsSupportedPlatform;
			OnlineRoomOptions options = new OnlineRoomOptions
			{
				RoomName = m_roomNameInput == null ? "" : m_roomNameInput.text,
				PlayerName = OnlinePrefs.PlayerName,
				Password = m_passwordInput == null ? "" : m_passwordInput.text,
				MaxPlayers = m_window.CreateMaxPlayers,
				Transport = useVirtualLan ? OnlineTransportKind.VirtualLan : OnlineTransportKind.Direct,
				ExternalNode = m_nodeInput == null ? "" : m_nodeInput.text.Trim(),
				Announcement = m_announcementInput == null ? "" : m_announcementInput.text,
				RelayLanBroadcast = m_relayToggle == null || m_relayToggle.isOn,
				AllowLanDiscovery = m_broadcastToggle == null || m_broadcastToggle.isOn,
				LatencyFirst = m_latencyToggle == null || m_latencyToggle.isOn
			};
			ushort port;
			if (!ushort.TryParse(m_portInput == null ? "" : m_portInput.text, out port) || port == 0)
			{
				SetCreateError("游戏端口必须是 1-65535 之间的数字。");
				return;
			}
			options.GamePort = port;
			string error;
			if (!OnlineLobbyService.GetOrCreate().HostRoom(options, out error))
			{
				SetCreateError(error);
				return;
			}
			SetCreateError("");
			m_window.RefreshAll();
		}

		private void SetCreateError(string text)
		{
			RectTransform page = m_pages[OnlineLobbyWindow.Page.Create];
			if (page == null)
			{
				return;
			}
			TMP_Text[] labels = page.GetComponentsInChildren<TMP_Text>(true);
			for (int i = 0; i < labels.Length; i++)
			{
				if (labels[i].name == "CreateError")
				{
					labels[i].text = text;
					return;
				}
			}
		}

		// ---------------------------------------------------------------- join

        private RectTransform BuildJoin(Transform parent)
        {
            RectTransform page = CreatePage(parent, "Page_Join", 600f);
            RectTransform form = FormSection(page, "JoinDetails", "01 / 好友邀请", 0f, 0.64f, 0f, 450f);
            RectTransform guide = FormSection(page, "JoinGuide", "02 / 连接指南", 0.64f, 1f, 0f, 450f);
            m_joinInput = StackedInput(form, "JoinInput", "房间号 / 邀请链接", "BP-XXXX-XXXX 或 bprc://join?…", 72f);
            m_joinPasswordInput = StackedInput(form, "JoinPassword", "房间密码 / 可选", "房主未设置密码时留空", 168f);
            m_joinPasswordInput.contentType = TMP_InputField.ContentType.Password;
            m_joinAddressInput = StackedInput(form, "JoinAddress", "房主地址 / 可选", "IP 地址或域名，可附带 :端口", 264f);
            UnityEngine.UI.Button paste = MctierWidgets.CreateButton("PasteButton", form, "粘贴邀请", MctierTheme.CardBg, MctierTheme.TextPrimary, new Action(PasteFromClipboard), 16f);
            PlaceFixedAtLeft((RectTransform)paste.transform, 20f, 370f, 160f, 46f);
            UnityEngine.UI.Button join = MctierWidgets.CreatePrimaryButton("JoinButton", form, "加入房间", new Action(JoinRoom), 18f);
            PlaceFixedAtRight((RectTransform)join.transform, 20f, 370f, 180f, 46f);
            TMP_Text instructions = Body(guide, "同一局域网\n直接输入房间号，或从上方「局域网」查找好友。\n\n跨网络连接\n优先粘贴完整邀请链接；房主地址需从你的网络可达。\n\n需要密码？\n向房主获取后填写，密码不会保存到历史记录。", 15f);
            instructions.color = MctierTheme.TextSecondary;
            instructions.alignment = TextAlignmentOptions.TopLeft;
            Place((RectTransform)instructions.transform, 20f, 76f, 20f, 424f);
            m_joinStatus = MctierWidgets.CreateCaption("JoinStatus", page, "", 15f);
            m_joinStatus.enableWordWrapping = true;
            Place((RectTransform)m_joinStatus.transform, 0f, 468f, 0f, 590f);
            return page;
        }

		private TMP_InputField m_joinAddressInput;

		private void PasteFromClipboard()
		{
			string clipboard = GUIUtility.systemCopyBuffer;
			if (string.IsNullOrEmpty(clipboard))
			{
				m_joinStatus.text = "剪贴板是空的。";
				m_joinStatus.color = MctierTheme.Warning;
				return;
			}
			if (m_joinInput != null)
			{
				m_joinInput.text = clipboard.Trim();
			}
			m_joinStatus.text = "已粘贴：" + (m_joinInput == null ? "" : m_joinInput.text);
			m_joinStatus.color = MctierTheme.TextSecondary;
		}

		private void JoinRoom()
		{
			string text = m_joinInput == null ? "" : m_joinInput.text;
			string password = m_joinPasswordInput == null ? "" : m_joinPasswordInput.text;
			OnlineRoomInfo info;
			string error;
			if (!OnlineInvite.TryParse(text, out info, out error))
			{
				m_joinStatus.text = error;
				m_joinStatus.color = MctierTheme.Danger;
				return;
			}
			string manual = m_joinAddressInput == null ? "" : m_joinAddressInput.text.Trim();
			if (manual.Length > 0)
			{
				// A typed address wins over whatever the invite carried: it is also how players on
				// different networks reach each other with no server in between.
				ushort manualPort;
				int colon = manual.LastIndexOf(':');
				if (colon > 0 && ushort.TryParse(manual.Substring(colon + 1), out manualPort) && manualPort != 0)
				{
					info.DirectAddress = manual.Substring(0, colon).Trim();
					info.GamePort = manualPort;
				}
				else
				{
					info.DirectAddress = manual;
				}
			}
			if (!OnlineLobbyService.GetOrCreate().JoinRoom(info, password, out error))
			{
				m_joinStatus.text = error;
				m_joinStatus.color = MctierTheme.Danger;
				return;
			}
			m_joinStatus.text = "正在加入 " + info.RoomCode + "…";
			m_joinStatus.color = MctierTheme.TextSecondary;
			m_window.RefreshAll();
		}

		// ---------------------------------------------------------------- LAN

		private RectTransform BuildLan(Transform parent)
		{
			RectTransform page = CreatePage(parent, "Page_Lan");

			TMP_Text hint = Body(page, "同一局域网（或同一个虚拟局域网）里正在开房的房间会自动出现在下面，点一下即可加入。");
			Place((RectTransform)hint.transform, 4f, 0f, 4f, 44f);

			UnityEngine.UI.Button refresh = MctierWidgets.CreateButton("LanRefresh", page, "刷新列表", MctierTheme.CardBg, MctierTheme.TextPrimary, new Action(RefreshLanList), 17f);
			MctierWidgets.SetAnchors((RectTransform)refresh.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(4f, -92f), new Vector2(164f, -50f));

			m_lanHint = MctierWidgets.CreateCaption("LanHint", page, "", 14f);
			m_lanHint.color = MctierTheme.TextMuted;
			MctierWidgets.SetAnchors((RectTransform)m_lanHint.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(176f, -92f), new Vector2(-4f, -50f));

			ScrollRect scroll = MctierWidgets.CreateScrollView("LanScroll", page, out m_lanList);
			RectTransform scrollRect = (RectTransform)scroll.transform;
			MctierWidgets.SetAnchors(scrollRect, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(0f, -104f));
			return page;
		}

		private void RefreshLanList()
		{
			if (m_lanList == null)
			{
				return;
			}
			MctierWidgets.DestroyChildren(m_lanList);
			OnlineLanDiscovery discovery = OnlineLanDiscovery.Instance;
			if (discovery == null)
			{
				m_lanHint.text = "未启动局域网监听。";
				return;
			}
			if (!discovery.IsListening)
			{
				discovery.StartListening();
			}
			List<OnlineLanRoom> rooms = discovery.Rooms;
			m_lanHint.text = rooms.Count == 0 ? "暂时没有发现房间（房主需要勾选局域网广播）。" : "发现 " + rooms.Count + " 个房间。";
			for (int i = 0; i < rooms.Count; i++)
			{
				OnlineLanRoom room = rooms[i];
				GameObject row = MctierWidgets.CreateListRow("LanRow_" + room.Info.RoomCode, m_lanList, 64f);
				TMP_Text name = Find(row, "Name");
				if (name != null)
				{
					name.text = room.DisplayName + (room.PasswordRequired ? "  🔒" : "");
				}
				TMP_Text detail = Find(row, "Detail");
				if (detail != null)
				{
					detail.text = room.Address + " · " + room.MemberCount + "/" + Mathf.Max(room.MaxPlayers, 2) + " 人";
				}
				OnlineRoomInfo captured = room.Info;
				MctierWidgets.CreateRowAction(row, "加入", MctierTheme.Accent, MctierTheme.OnAccent, delegate
				{
					string error;
					if (!OnlineLobbyService.GetOrCreate().JoinRoom(captured, "", out error))
					{
						m_lanHint.text = error;
						m_lanHint.color = MctierTheme.Danger;
					}
					else
					{
						m_window.RefreshAll();
					}
				});
			}
		}

		// ---------------------------------------------------------------- recent

		private RectTransform BuildRecent(Transform parent)
		{
			RectTransform page = CreatePage(parent, "Page_Recent");

			TMP_Text hint = Body(page, "加入过的房间会留在这里，可以收藏或直接重连。密码不会保存。");
			Place((RectTransform)hint.transform, 4f, 0f, 4f, 36f);

			ScrollRect scroll = MctierWidgets.CreateScrollView("RecentScroll", page, out m_recentList);
			RectTransform scrollRect = (RectTransform)scroll.transform;
			MctierWidgets.SetAnchors(scrollRect, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(0f, -88f));

			m_recentHint = MctierWidgets.CreateCaption("RecentHint", page, "", 14f);
			m_recentHint.color = MctierTheme.TextMuted;
			MctierWidgets.SetAnchors((RectTransform)m_recentHint.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(4f, -76f), new Vector2(-4f, -48f));
			return page;
		}

		private void RefreshRecentList()
		{
			if (m_recentList == null)
			{
				return;
			}
			MctierWidgets.DestroyChildren(m_recentList);
			List<OnlineRoomRecord> history = OnlinePrefs.Data.RoomHistory;
			if (history.Count == 0)
			{
				m_recentHint.text = "还没有历史房间。";
				return;
			}
			m_recentHint.text = "共 " + history.Count + " 条记录，收藏的会一直保留。";
			for (int i = 0; i < history.Count; i++)
			{
				OnlineRoomRecord record = history[i];
				if (record == null || record.Info == null)
				{
					continue;
				}
				string code = record.Key;
				GameObject row = MctierWidgets.CreateListRow("RecentRow_" + code, m_recentList, 64f);
				TMP_Text name = Find(row, "Name");
				if (name != null)
				{
					name.text = (record.Favorite ? "★ " : "") + record.DisplayName;
				}
				TMP_Text detail = Find(row, "Detail");
				if (detail != null)
				{
					detail.text = code + " · 加入 " + record.TimesJoined + " 次";
				}
				RectTransform actions = MctierWidgets.CreateNode("Actions", row.transform);
                MctierWidgets.Stretch(actions);
                if (name != null) ((RectTransform)name.transform).offsetMax = new Vector2(-304f, ((RectTransform)name.transform).offsetMax.y);
                if (detail != null) ((RectTransform)detail.transform).offsetMax = new Vector2(-304f, ((RectTransform)detail.transform).offsetMax.y);
				OnlineRoomInfo capturedInfo = record.Info;
				string capturedCode = code;
				UnityEngine.UI.Button favorite = MctierWidgets.CreateSmallButton("Favorite", actions, record.Favorite ? "取消收藏" : "收藏",
					MctierTheme.CardBg, MctierTheme.Warning, delegate
					{
						OnlinePrefs.ToggleFavorite(capturedCode);
						RefreshRecentList();
					}, 14f);
				RectTransform favoriteRect = (RectTransform)favorite.transform;
				favoriteRect.anchorMin = new Vector2(1f, 0.5f);
				favoriteRect.anchorMax = new Vector2(1f, 0.5f);
				favoriteRect.pivot = new Vector2(1f, 0.5f);
				favoriteRect.anchoredPosition = new Vector2(-196f, 0f);
				favoriteRect.sizeDelta = new Vector2(88f, 32f);

				UnityEngine.UI.Button forget = MctierWidgets.CreateSmallButton("Forget", actions, "删除", MctierTheme.CardBg, MctierTheme.Danger, delegate
				{
					OnlinePrefs.ForgetRoom(capturedCode);
					RefreshRecentList();
				}, 14f);
				RectTransform forgetRect = (RectTransform)forget.transform;
				forgetRect.anchorMin = new Vector2(1f, 0.5f);
				forgetRect.anchorMax = new Vector2(1f, 0.5f);
				forgetRect.pivot = new Vector2(1f, 0.5f);
				forgetRect.anchoredPosition = new Vector2(-104f, 0f);
				forgetRect.sizeDelta = new Vector2(80f, 32f);

				MctierWidgets.CreateRowAction(row, "加入", MctierTheme.Accent, MctierTheme.OnAccent, delegate
				{
					string error;
					if (!OnlineLobbyService.GetOrCreate().JoinRoom(capturedInfo, "", out error))
					{
						m_recentHint.text = error;
						m_recentHint.color = MctierTheme.Danger;
					}
					else
					{
						m_window.RefreshAll();
					}
				});
			}
		}

		// ---------------------------------------------------------------- settings

		private RectTransform BuildSettings(Transform parent)
		{
			RectTransform page = CreatePage(parent, "Page_Settings", 700f);

			// Heat UI panel order: a row per setting, label left and control right, then a section
			// header for the list part.
			m_playerNameInput = LabeledInput(page, "PlayerName", "玩家名称", "显示在成员列表和聊天里", 0f, ref m_playerNameField);

			RectTransform saveRow = PanelRow(page, "SaveName", 68f, "保存名称");
			UnityEngine.UI.Button save = MctierWidgets.CreatePrimaryButton("SaveName", saveRow, "保存", new Action(SavePlayerName), 15f);
			PlaceFixedAtRight((RectTransform)save.transform, 20f, (PanelRowHeight - 34f) * 0.5f, 120f, 34f);

			m_settingsBroadcastToggle = MctierWidgets.CreateSwitch("SettingsBroadcast", page, OnlinePrefs.Data.EnableLanBroadcast, null);
			PlaceToggle((RectTransform)m_settingsBroadcastToggle.transform, "启动时监听局域网房间", 136f);

			m_settingsVirtualLanToggle = MctierWidgets.CreateSwitch("SettingsVirtualLan", page, OnlinePrefs.EnableVirtualLan, new Action<bool>(SetVirtualLan));
			PlaceToggle((RectTransform)m_settingsVirtualLanToggle.transform, "启用 EasyTier 虚拟局域网（跨网络可选，默认关闭）", 204f);

			TMP_Text nodeTitle = MctierWidgets.CreateCaption("NodeTitle", page, "自定义 EasyTier 节点（可留空）", 16f);
			nodeTitle.color = MctierTheme.TextPrimary;
			nodeTitle.fontStyle = FontStyles.Bold;
			Place((RectTransform)nodeTitle.transform, 4f, 280f, 4f, 310f);
			Image titleEdge = MctierWidgets.CreateImage("NodeTitleEdge", page, MctierTheme.WithAlpha(MctierTheme.TextPrimary, 0.10f));
			Place((RectTransform)titleEdge.transform, 4f, 312f, 4f, 313f);

			TMP_Text nodeHint = Body(page, "默认完全不需要节点：同一局域网或直连即可对战。只有跨网络又不方便直连时，才填写一个由你自己控制的节点地址（自建节点或房主的公网地址）。", 13f);
			Place((RectTransform)nodeHint.transform, 4f, 320f, 4f, 354f);

			ScrollRect scroll = MctierWidgets.CreateScrollView("NodeScroll", page, out m_nodeList);
			RectTransform scrollRect = (RectTransform)scroll.transform;
			MctierWidgets.SetAnchors(scrollRect, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 60f), new Vector2(0f, -358f));

			m_newNodeInput = MctierWidgets.CreateInput("NewNode", page, "udp://my.server:11010", 15f);
			MctierWidgets.SetAnchors((RectTransform)m_newNodeInput.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(4f, 12f), new Vector2(-184f, 48f));

			UnityEngine.UI.Button add = MctierWidgets.CreateButton("AddNode", page, "添加节点", MctierTheme.Accent, MctierTheme.OnAccent, new Action(AddNode), 15f);
			MctierWidgets.SetAnchors((RectTransform)add.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-176f, 12f), new Vector2(-4f, 48f));
			return page;
		}

		private void SavePlayerName()
		{
			OnlinePrefs.PlayerName = m_playerNameInput == null ? "" : m_playerNameInput.text;
			OnlinePrefs.Save();
			if (OnlineLobbyService.Instance != null)
			{
				OnlineLobbyService.Instance.RefreshMembers();
			}
		}

		/// <summary>
		/// Persists the virtual-LAN opt-in. Turning it on is the only thing that makes a new room
		/// use EasyTier; direct/LAN play stays available either way.
		/// </summary>
		private void SetVirtualLan(bool enabled)
		{
			OnlinePrefs.EnableVirtualLan = enabled;
			OnlinePrefs.Save();
		}

		private void AddNode()
		{
			if (m_newNodeInput == null || string.IsNullOrWhiteSpace(m_newNodeInput.text))
			{
				return;
			}
			OnlinePrefs.AddCustomNode(m_newNodeInput.text.Trim());
			m_newNodeInput.text = "";
			RefreshNodeList();
		}

		private void RefreshNodeList()
		{
			if (m_nodeList == null)
			{
				return;
			}
			MctierWidgets.DestroyChildren(m_nodeList);
			List<string> nodes = OnlinePrefs.NodeChoices();
			for (int i = 0; i < nodes.Count; i++)
			{
				string node = nodes[i];
				GameObject row = MctierWidgets.CreateListRow("NodeRow_" + i, m_nodeList, 56f);
				TMP_Text name = Find(row, "Name");
				if (name != null)
				{
					name.text = node;
					name.fontSize = 15f;
				}
				TMP_Text detail = Find(row, "Detail");
				if (detail != null)
				{
					detail.text = i == 0 ? "默认" : "";
				}
				if (i > 0)
				{
					string captured = node;
					MctierWidgets.CreateRowAction(row, "移除", MctierTheme.CardBg, MctierTheme.Danger, delegate
					{
						OnlinePrefs.RemoveCustomNode(captured);
						RefreshNodeList();
					}, 72f);
				}
			}
		}

		// ---------------------------------------------------------------- refresh

		public void Refresh(OnlineLobbyWindow.Page page)
		{
			OnlineLobbyService service = OnlineLobbyService.Instance;
			SyncFormFromState();
			switch (page)
			{
			case OnlineLobbyWindow.Page.Home:
				RefreshHome(service);
				break;
			case OnlineLobbyWindow.Page.Lan:
				RefreshLanList();
				break;
			case OnlineLobbyWindow.Page.Recent:
				RefreshRecentList();
				break;
			case OnlineLobbyWindow.Page.Settings:
				RefreshNodeList();
				break;
			}
		}

		private void SyncFormFromState()
		{
			if (m_maxPlayersLabel != null)
			{
				m_maxPlayersLabel.text = m_window.CreateMaxPlayers + " 人";
			}
			if (m_playerNameInput != null && string.IsNullOrEmpty(m_playerNameInput.text))
			{
				OnlinePrefs.EnsureLoaded();
				m_playerNameInput.text = OnlinePrefs.PlayerName;
			}
			if (m_settingsBroadcastToggle != null)
			{
				m_settingsBroadcastToggle.isOn = OnlinePrefs.Data.EnableLanBroadcast;
			}
		}

        private void RefreshHome(OnlineLobbyService service)
        {
            // The Heat home panel owns its informational carousel; live status stays in the shell.
        }

		private string OnlineLobbyWindow_DescribeNetwork()
		{
			if (!EasyTierPaths.IsSupportedPlatform)
			{
				return "当前平台不支持 EasyTier 虚拟局域网，只能使用直连模式。";
			}
			if (!EasyTierPaths.IsInstalled)
			{
				return "未找到 EasyTier 运行文件：请先执行 Tools/Online/fetch-easytier.ps1 把它们放进 StreamingAssets。";
			}
			EasyTierRunner runner = EasyTierRunner.GetOrCreate();
			if (runner == null)
			{
				return "EasyTier 未初始化。";
			}
			switch (runner.State)
			{
			case EasyTierState.Running:
				return "虚拟局域网已连接。";
			case EasyTierState.Starting:
				return "正在启动虚拟局域网…（首次启动需要管理员权限来创建虚拟网卡）";
			case EasyTierState.Failed:
				return "虚拟局域网启动失败：" + runner.LastError;
			default:
				return "虚拟局域网空闲。不使用任何服务器，双方直接互连。";
			}
		}

		// ---------------------------------------------------------------- helpers

		internal static TMP_Text Find(GameObject row, string childName)
		{
			if (row == null)
			{
				return null;
			}
			TMP_Text[] labels = row.GetComponentsInChildren<TMP_Text>(true);
			for (int i = 0; i < labels.Length; i++)
			{
				if (labels[i].name == childName)
				{
					return labels[i];
				}
			}
			return null;
		}

		internal static void PlaceFixedAtLeft(RectTransform rect, float left, float top, float width, float height)
		{
			rect.anchorMin = new Vector2(0f, 1f);
			rect.anchorMax = new Vector2(0f, 1f);
			rect.pivot = new Vector2(0f, 1f);
			rect.sizeDelta = new Vector2(width, height);
			rect.anchoredPosition = new Vector2(left, -top);
		}

		internal static void PlaceFixedAtRight(RectTransform rect, float right, float top, float width, float height)
		{
			rect.anchorMin = new Vector2(1f, 1f);
			rect.anchorMax = new Vector2(1f, 1f);
			rect.pivot = new Vector2(1f, 1f);
			rect.sizeDelta = new Vector2(width, height);
			rect.anchoredPosition = new Vector2(-right, -top);
		}
	}
}
