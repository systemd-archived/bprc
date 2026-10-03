using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BadPiggies.Multiplayer.Online.Ui
{
	/// <summary>
	/// The in-room screen, shown while the session is live: member list on the left (ready state,
	/// latency and packet loss, host-only kick), chat on the right, an announcement ticker on top
	/// and the room actions at the bottom.
	/// </summary>
	internal sealed class OnlineRoomView
	{
		private const float MemberRowHeight = 64f;

		private readonly OnlineLobbyWindow m_window;
		private readonly RectTransform m_root;
		private readonly RectTransform m_memberList;
		private readonly RectTransform m_chatContent;
		private readonly ScrollRect m_chatScroll;
		private readonly TMP_InputField m_chatInput;
		private readonly TMP_Text m_announcement;
		private readonly TMP_Text m_roomSummary;
		private readonly TMP_Text m_readyLabel;
		private readonly UnityEngine.UI.Button m_readyButton;
		private readonly UnityEngine.UI.Button m_startButton;
		private readonly UnityEngine.UI.Button m_maxMinus;
		private readonly UnityEngine.UI.Button m_maxPlus;
		private readonly TMP_InputField m_announcementInput;
		private readonly TMP_Text m_memberHint;
		private readonly TMP_Text m_chatHint;

		private int m_lastRenderedChatCount = -1;
		private string m_memberSignature = "";
		private string m_lastAnnouncement;
		private bool m_visible;

		public OnlineRoomView(OnlineLobbyWindow window)
		{
			m_window = window;
			m_root = MctierWidgets.CreateNode("RoomView", window.Content);
			MctierWidgets.Stretch(m_root);
			m_root.gameObject.SetActive(false);

			// --- announcement ticker -------------------------------------------------
			Image announcementCard = MctierWidgets.CreateCard("Announcement", m_root, 12, false);
			RectTransform announcementRect = (RectTransform)announcementCard.transform;
			MctierWidgets.SetAnchors(announcementRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -44f), new Vector2(0f, 0f));
			m_announcement = MctierWidgets.CreateCaption("AnnouncementText", announcementRect, "房主还没有设置公告。", 15f);
			m_announcement.color = MctierTheme.TextSecondary;
			MctierWidgets.Stretch((RectTransform)m_announcement.transform, 16f, 4f, 16f, 4f);

			// --- member list ---------------------------------------------------------
			Image memberCard = MctierWidgets.CreateCard("MemberPanel", m_root, 14);
			RectTransform memberRect = (RectTransform)memberCard.transform;
			MctierWidgets.SetAnchors(memberRect, new Vector2(0f, 0f), new Vector2(0.48f, 1f), new Vector2(0f, 76f), new Vector2(-8f, -60f));

			TMP_Text memberTitle = MctierWidgets.CreateCaption("MemberTitle", memberRect, "房间成员", 17f);
			memberTitle.color = MctierTheme.TextPrimary;
			MctierWidgets.SetAnchors((RectTransform)memberTitle.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -34f), new Vector2(-16f, -8f));

			m_memberHint = MctierWidgets.CreateCaption("MemberHint", memberRect, "", 13f);
			m_memberHint.color = MctierTheme.TextMuted;
			MctierWidgets.SetAnchors((RectTransform)m_memberHint.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -56f), new Vector2(-16f, -36f));

			ScrollRect memberScroll = MctierWidgets.CreateScrollView("MemberScroll", memberRect, out m_memberList);
			RectTransform memberScrollRect = (RectTransform)memberScroll.transform;
			MctierWidgets.SetAnchors(memberScrollRect, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(8f, 104f), new Vector2(-8f, -60f));

			// host controls under the member list
			TMP_Text maxLabel = MctierWidgets.CreateCaption("MaxLabel", memberRect, "人数上限", 14f);
			maxLabel.color = MctierTheme.TextSecondary;
			MctierWidgets.SetAnchors((RectTransform)maxLabel.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(16f, 66f), new Vector2(96f, 90f));
			m_maxMinus = MctierWidgets.CreateSmallButton("RoomMaxMinus", memberRect, "－", MctierTheme.CardBg, MctierTheme.TextPrimary, new Action(() => StepMax(-1)), 15f);
			MctierWidgets.SetAnchors((RectTransform)m_maxMinus.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(96f, 64f), new Vector2(130f, 92f));
			m_maxPlus = MctierWidgets.CreateSmallButton("RoomMaxPlus", memberRect, "＋", MctierTheme.CardBg, MctierTheme.TextPrimary, new Action(() => StepMax(1)), 15f);
			MctierWidgets.SetAnchors((RectTransform)m_maxPlus.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(302f, 64f), new Vector2(336f, 92f));
			m_announcementInput = MctierWidgets.CreateInput("AnnouncementEdit", memberRect, "设置滚动公告…", 14f);
			MctierWidgets.SetAnchors((RectTransform)m_announcementInput.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(16f, 22f), new Vector2(-16f, 58f));
			m_announcementInput.onEndEdit.AddListener(delegate(string value)
			{
				OnlineLobbyService service = OnlineLobbyService.Instance;
				if (service != null)
				{
					service.SetAnnouncement(value);
				}
			});

			// --- chat ---------------------------------------------------------------
			Image chatCard = MctierWidgets.CreateCard("ChatPanel", m_root, 14);
			RectTransform chatRect = (RectTransform)chatCard.transform;
			MctierWidgets.SetAnchors(chatRect, new Vector2(0.50f, 0f), new Vector2(1f, 1f), new Vector2(0f, 76f), new Vector2(0f, -60f));

			TMP_Text chatTitle = MctierWidgets.CreateCaption("ChatTitle", chatRect, "房间聊天", 17f);
			chatTitle.color = MctierTheme.TextPrimary;
			MctierWidgets.SetAnchors((RectTransform)chatTitle.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -34f), new Vector2(-16f, -8f));

			m_chatScroll = MctierWidgets.CreateScrollView("ChatScroll", chatRect, out m_chatContent);
			RectTransform chatScrollRect = (RectTransform)m_chatScroll.transform;
			MctierWidgets.SetAnchors(chatScrollRect, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(10f, 58f), new Vector2(-10f, -40f));

			m_chatInput = MctierWidgets.CreateInput("ChatInput", chatRect, "说点什么…（回车发送）", 15f);
			MctierWidgets.SetAnchors((RectTransform)m_chatInput.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(12f, 12f), new Vector2(-104f, 48f));
			m_chatInput.onSubmit.AddListener(delegate(string value)
			{
				SendChat(value);
			});

			UnityEngine.UI.Button send = MctierWidgets.CreateButton("SendButton", chatRect, "发送", MctierTheme.Accent, MctierTheme.OnAccent, new Action(() => SendChat(m_chatInput == null ? "" : m_chatInput.text)), 15f);
			MctierWidgets.SetAnchors((RectTransform)send.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-96f, 12f), new Vector2(-12f, 48f));

			// --- room actions -------------------------------------------------------
			Image actionsCard = MctierWidgets.CreateCard("RoomActions", m_root, 12, false);
			RectTransform actionsRect = (RectTransform)actionsCard.transform;
			MctierWidgets.SetAnchors(actionsRect, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 64f));

			m_roomSummary = MctierWidgets.CreateCaption("RoomSummary", actionsRect, "", 14f);
			m_roomSummary.color = MctierTheme.TextSecondary;
			MctierWidgets.SetAnchors((RectTransform)m_roomSummary.transform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(14f, 0f), new Vector2(230f, 0f));

			UnityEngine.UI.Button invite = MctierWidgets.CreateSmallButton("InviteButton", actionsRect, "邀请 / 二维码", MctierTheme.CardBg, MctierTheme.Accent, new Action(m_window.ShowInviteOverlay), 14f);
			MctierWidgets.SetAnchors((RectTransform)invite.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(238f, -17f), new Vector2(362f, 17f));

			m_readyButton = MctierWidgets.CreateButton("ReadyButton", actionsRect, "准备", MctierTheme.TextPrimary, MctierTheme.OnAccent, new Action(ToggleReady), 16f);
			MctierWidgets.SetAnchors((RectTransform)m_readyButton.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(370f, -18f), new Vector2(470f, 18f));
			m_readyLabel = m_readyButton.GetComponentInChildren<TMP_Text>(true);

			m_startButton = MctierWidgets.CreateButton("StartButton", actionsRect, "开始对战", MctierTheme.Accent, MctierTheme.OnAccent, new Action(StartMatch), 16f);
			MctierWidgets.SetAnchors((RectTransform)m_startButton.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(478f, -18f), new Vector2(598f, 18f));
			m_startButton.gameObject.SetActive(false);

			UnityEngine.UI.Button leave = MctierWidgets.CreateDangerButton("LeaveButton", actionsRect, "离开房间", new Action(Leave), 15f);
			MctierWidgets.SetAnchors((RectTransform)leave.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-108f, -17f), new Vector2(-12f, 17f));

			m_chatHint = MctierWidgets.CreateCaption("ChatHint", m_root, "", 13f);
			m_chatHint.color = MctierTheme.TextMuted;
			MctierWidgets.SetAnchors((RectTransform)m_chatHint.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 66f), new Vector2(0f, 90f));
		}

		public void SetVisible(bool visible)
		{
			if (m_root == null)
			{
				return;
			}
			if (m_root.gameObject.activeSelf != visible)
			{
				m_root.gameObject.SetActive(visible);
				m_visible = visible;
				m_lastRenderedChatCount = -1;
				m_memberSignature = "";
			}
		}

		public void Refresh()
		{
			OnlineLobbyService service = OnlineLobbyService.Instance;
			if (service == null || !m_visible)
			{
				return;
			}
			RefreshAnnouncement(service);
			RefreshMembers(service);
			RefreshChat(service);
			RefreshActions(service);
		}

		private void RefreshAnnouncement(OnlineLobbyService service)
		{
			string text = service.Announcement;
			if (string.IsNullOrWhiteSpace(text))
			{
				text = "房主还没有设置公告。";
			}
			if (m_announcement != null && m_announcement.text != text)
			{
				m_announcement.text = text;
			}
			if (m_lastAnnouncement != service.Announcement && m_announcementInput != null && !m_announcementInput.isFocused)
			{
				m_lastAnnouncement = service.Announcement;
				if (service.IsHost)
				{
					m_announcementInput.text = service.Announcement ?? "";
				}
			}
		}

		private void RefreshMembers(OnlineLobbyService service)
		{
			List<OnlineMemberView> members = service.Members;
			string signature = BuildMemberSignature(members, service);
			if (signature == m_memberSignature)
			{
				// The cast did not change; only the volatile columns need refreshing, so the rows
				// (and any button the player is about to click) stay where they are.
				UpdateMemberColumns(members, service);
				return;
			}
			m_memberSignature = signature;
			MctierWidgets.DestroyChildren(m_memberList);
			for (int i = 0; i < members.Count; i++)
			{
				OnlineMemberView member = members[i];
				GameObject row = MctierWidgets.CreateListRow("MemberRow_" + member.NetId, m_memberList, MemberRowHeight);
				ApplyMemberRow(row, member, service);
			}
			if (m_memberHint != null)
			{
				m_memberHint.text = members.Count + "/" + Mathf.Max(service.MaxPlayers, OnlineNetConstants.MinPlayers) + " 人 · 全员上场";
			}
		}

		/// <summary>Identity of the member list: name, role and capacity, but not ping or ready.</summary>
		private static string BuildMemberSignature(List<OnlineMemberView> members, OnlineLobbyService service)
		{
			System.Text.StringBuilder builder = new System.Text.StringBuilder();
			builder.Append(members.Count).Append('|').Append(service.MaxPlayers).Append('|').Append(service.IsHost ? 'H' : 'G');
			for (int i = 0; i < members.Count; i++)
			{
				OnlineMemberView member = members[i];
				builder.Append('|').Append(member.NetId).Append(',').Append(member.PlayerName).Append(',').Append(member.IsFighter ? 'F' : '-').Append(',').Append(member.IsHost ? 'h' : 'm');
			}
			return builder.ToString();
		}

		private void UpdateMemberColumns(List<OnlineMemberView> members, OnlineLobbyService service)
		{
			for (int i = 0; i < members.Count; i++)
			{
				OnlineMemberView member = members[i];
				Transform row = m_memberList == null ? null : m_memberList.Find("MemberRow_" + member.NetId);
				if (row == null)
				{
					continue;
				}
				ApplyMemberDetail(row.gameObject, member, service);
			}
		}

		private void ApplyMemberRow(GameObject row, OnlineMemberView member, OnlineLobbyService service)
		{
			Image dot = row.transform.Find("Dot") == null ? null : row.transform.Find("Dot").GetComponent<Image>();
			if (dot != null)
			{
				dot.color = member.IsReady ? MctierTheme.Success : MctierTheme.TextMuted;
			}
			TMP_Text name = OnlineLobbyPages.Find(row, "Name");
			if (name != null)
			{
				name.text = member.PlayerName + (member.IsLocalPlayer ? "（你）" : "");
				name.color = MctierTheme.TextPrimary;
			}
			ApplyMemberDetail(row, member, service);
			if (service.IsHost && !member.IsLocalPlayer)
			{
				uint netId = member.NetId;
				string playerName = member.PlayerName;
				MctierWidgets.CreateRowAction(row, "移出", MctierTheme.CardBg, MctierTheme.Danger, delegate
				{
					OnlineLobbyService instance = OnlineLobbyService.Instance;
					if (instance != null && !instance.Kick(netId, "你已被房主移出房间。"))
					{
						Debug.LogWarning("[Online] Kick failed for " + playerName + ".");
					}
				}, 64f);
			}
		}

		private void ApplyMemberDetail(GameObject row, OnlineMemberView member, OnlineLobbyService service)
		{
			TMP_Text detail = OnlineLobbyPages.Find(row, "Detail");
			if (detail == null)
			{
				return;
			}
			string role = member.RoleLabel;
			string ping = float.IsNaN(member.LatencyMs) ? "--" : Mathf.RoundToInt(member.LatencyMs) + " ms";
			string loss = float.IsNaN(member.LossRate) ? "" : "  丢包 " + Mathf.RoundToInt(member.LossRate * 100f) + "%";
			string ready = member.IsFighter ? (member.IsReady ? "  已准备" : "  未准备") : "";
			detail.text = role + ready + "  ·  " + ping + loss;
			detail.color = !float.IsNaN(member.LatencyMs) && member.LatencyMs > 180f ? MctierTheme.Warning : MctierTheme.TextMuted;
		}

		private void RefreshChat(OnlineLobbyService service)
		{
			List<OnlineChatEntry> chat = service.Chat;
			if (m_lastRenderedChatCount == chat.Count)
			{
				return;
			}
			bool appendOnly = m_lastRenderedChatCount >= 0 && chat.Count > m_lastRenderedChatCount;
			if (!appendOnly)
			{
				MctierWidgets.DestroyChildren(m_chatContent);
			}
			int start = appendOnly ? m_lastRenderedChatCount : 0;
			for (int i = start; i < chat.Count; i++)
			{
				AddChatLine(chat[i]);
			}
			m_lastRenderedChatCount = chat.Count;
			if (m_chatScroll != null)
			{
				Canvas.ForceUpdateCanvases();
				m_chatScroll.verticalNormalizedPosition = 0f;
			}
			if (m_chatHint != null)
			{
				m_chatHint.text = service.Members.Count <= 1 ? "把房间号或二维码发给朋友，他们就能加入。" : "";
			}
		}

		private void AddChatLine(OnlineChatEntry entry)
		{
			if (m_chatContent == null)
			{
				return;
			}
			RectTransform row = MctierWidgets.CreateNode("ChatLine", m_chatContent);
			Image bubble = MctierWidgets.CreateImage("Bubble", row,
				entry.IsSystem ? MctierTheme.WithAlpha(MctierTheme.TextPrimary, 0.035f) :
				(entry.IsLocal ? MctierTheme.ChatSelf : MctierTheme.ChatOther));
			MctierWidgets.Stretch((RectTransform)bubble.transform, 0f, 1f, 0f, 1f);
			bubble.raycastTarget = false;
			TMP_Text line = MctierWidgets.CreateCaption("Message", row, FormatChat(entry), 14f);
			MctierWidgets.Stretch((RectTransform)line.transform, 10f, 2f, 10f, 2f);
			line.enableWordWrapping = true;
			line.overflowMode = TextOverflowModes.Overflow;
			if (entry.IsSystem)
			{
				line.color = MctierTheme.TextMuted;
				line.fontStyle = FontStyles.Italic;
			}
			else if (entry.IsLocal)
			{
				line.color = MctierTheme.Accent;
			}
			else
			{
				line.color = MctierTheme.TextPrimary;
			}
			LayoutElement element = MctierWidgets.EnsureLayoutElement(row.gameObject);
			element.minHeight = 26f;
			element.preferredHeight = 26f;
			element.flexibleHeight = 0f;
		}

		private static string FormatChat(OnlineChatEntry entry)
		{
			if (entry.IsSystem)
			{
				return "· " + entry.Text;
			}
			return "[" + entry.LocalTime.ToString("HH:mm") + "] " + entry.Sender + "：" + entry.Text;
		}

		private void RefreshActions(OnlineLobbyService service)
		{
			if (m_roomSummary != null)
			{
				string code = string.IsNullOrEmpty(service.RoomCode) ? "--" : service.RoomCode;
				m_roomSummary.text = "房间号 " + code + "\n" + (service.IsHost ? "你是房主" : "你是成员");
			}
			MultiplayerLobbyPlayer local = LocalPlayer();
			bool ready = local != null && local.IsReady;
			if (m_readyLabel != null)
			{
				m_readyLabel.text = ready ? "取消准备" : "准备";
			}
			if (m_readyButton != null)
			{
				Image image = m_readyButton.GetComponent<Image>();
				if (image != null)
				{
					image.color = Color.clear;
				}
				Michsky.UI.Heat.ButtonManager heat = m_readyButton.GetComponentInChildren<Michsky.UI.Heat.ButtonManager>(true);
				if (heat != null)
				{
					Color color = ready ? MctierTheme.Accent : MctierTheme.TextPrimary;
                    heat.SetText(ready ? "取消准备" : "准备");
					if (heat.normalImageObj != null) { heat.normalImageObj.color = color; }
					if (heat.highlightImageObj != null) { heat.highlightImageObj.color = Color.Lerp(color, Color.white, 0.12f); }
					MctierWidgets.ApplyHeatButtonStateColor(heat, "Normal", color);
					MctierWidgets.ApplyHeatButtonStateColor(heat, "Highlight", Color.Lerp(color, Color.white, 0.12f));
					heat.UpdateUI();
				}
			}

			bool isHost = service.IsHost;
			if (m_maxMinus != null)
			{
				m_maxMinus.gameObject.SetActive(isHost);
			}
			if (m_maxPlus != null)
			{
				m_maxPlus.gameObject.SetActive(isHost);
			}
			if (m_announcementInput != null)
			{
				m_announcementInput.gameObject.SetActive(isHost);
			}
			if (m_startButton != null)
			{
				bool canStart = isHost && CountReadyPlayers(service) >= OnlineNetConstants.MinPlayers;
				m_startButton.gameObject.SetActive(isHost);
				m_startButton.interactable = canStart;
			}
		}

		private static int CountReadyPlayers(OnlineLobbyService service)
		{
			int count = 0;
			List<OnlineMemberView> members = service.Members;
			for (int i = 0; i < members.Count; i++)
			{
				if (members[i].IsFighter && members[i].IsReady)
				{
					count++;
				}
			}
			return count;
		}

		private static MultiplayerLobbyPlayer LocalPlayer()
		{
			if (Mirror.NetworkClient.localPlayer == null)
			{
				return null;
			}
			return Mirror.NetworkClient.localPlayer.GetComponent<MultiplayerLobbyPlayer>();
		}

		private void ToggleReady()
		{
			MultiplayerLobbyPlayer local = LocalPlayer();
			if (local == null)
			{
				return;
			}
			OnlineLobbyService service = OnlineLobbyService.Instance;
			local.ToggleReadyLocal();
			if (service != null)
			{
				service.RefreshMembers();
			}
		}

		private void SendChat(string text)
		{
			OnlineLobbyService service = OnlineLobbyService.Instance;
			bool sent = service != null && service.TrySendChat(text);
			if (sent && m_chatInput != null)
			{
				m_chatInput.text = "";
				m_chatInput.ActivateInputField();
			}
			else if (!sent && m_chatHint != null)
			{
				m_chatHint.text = "正在连接房间，消息会保留在输入框中。";
			}
		}

		private void StepMax(int delta)
		{
			OnlineLobbyService service = OnlineLobbyService.Instance;
			if (service == null)
			{
				return;
			}
			service.SetMaxPlayers(service.MaxPlayers + delta);
		}

		private void StartMatch()
		{
			MultiplayerNetworkManager session = MultiplayerBootstrap.GetOrCreateSession();
			if (session != null)
			{
				session.ServerBeginArenaLoad();
			}
		}

		private void Leave()
		{
			OnlineLobbyService service = OnlineLobbyService.Instance;
			if (service != null)
			{
				service.LeaveRoom();
			}
			m_window.RefreshAll();
		}
	}
}
