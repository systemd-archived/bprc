using System;
using System.Collections.Generic;
using System.Text;
using BadPiggies.Multiplayer.Online.Qr;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BadPiggies.Multiplayer.Online.Ui
{
	/// <summary>
	/// The invitation panel: the room code, the invite link, a scannable QR code, and the exact
	/// address another machine should use. Everything shown here is derived from the host's own
	/// room info, so inviting someone never involves a server.
	///
	/// Built once against the lobby canvas and hidden afterwards; <see cref="Show"/> refreshes it.
	/// </summary>
	internal sealed class OnlineInviteOverlay
	{
		private const float CardWidth = 760f;
		private const float CardHeight = 580f;
		private const int QrModulePixels = 8;
		private const int QrQuietZone = 4;

		private readonly OnlineLobbyWindow m_window;
		private RectTransform m_root;
		private RectTransform m_card;
		private TMP_Text m_codeValue;
		private TMP_Text m_linkValue;
		private TMP_Text m_qrHint;
		private TMP_Text m_connection;
		private TMP_Text m_status;
		private Image m_qrImage;
		private Texture2D m_qrTexture;
		private UnityEngine.Sprite m_qrSprite;
		private string m_renderedSource = "";
		private bool m_visible;

		internal OnlineInviteOverlay(OnlineLobbyWindow window)
		{
			m_window = window;
			if (window == null || window.Canvas == null)
			{
				return;
			}
			Build(window.Canvas.transform);
		}

		internal bool IsVisible
		{
			get { return m_visible; }
		}

		// ---------------------------------------------------------------- construction

		private void Build(Transform parent)
		{
			Image backdrop = MctierWidgets.CreateImage("InviteOverlay", parent, new Color(0.03f, 0.05f, 0.08f, 0.92f));
			m_root = (RectTransform)backdrop.transform;
			MctierWidgets.Stretch(m_root);

			Image card = MctierWidgets.CreateCard("InviteCard", m_root, 18);
			card.color = new Color(0.14f, 0.19f, 0.24f, 1f);
            m_card = (RectTransform)card.transform;
			m_card.anchorMin = new Vector2(0.5f, 0.5f);
			m_card.anchorMax = new Vector2(0.5f, 0.5f);
			m_card.pivot = new Vector2(0.5f, 0.5f);
			m_card.sizeDelta = new Vector2(CardWidth, CardHeight);
			m_card.anchoredPosition = Vector2.zero;

			TMP_Text title = MctierWidgets.CreateHeading("InviteTitle", m_card, "邀请朋友加入", 26f);
			Place((RectTransform)title.transform, 32f, 18f, 420f, 54f);

			UnityEngine.UI.Button close = MctierWidgets.CreateSmallButton("InviteClose", m_card, "关闭", MctierTheme.CardBg, MctierTheme.TextPrimary, new Action(Hide), 15f);
			Place((RectTransform)close.transform, CardWidth - 104f, 26f, 72f, 34f);

			TMP_Text codeCaption = MctierWidgets.CreateCaption("CodeCaption", m_card, "房间号（念给朋友或让对方粘贴）", 15f);
			Place((RectTransform)codeCaption.transform, 32f, 78f, 420f, 22f);

			m_codeValue = MctierWidgets.CreateLabel("CodeValue", m_card, "", 28f, MctierTheme.Accent);
			m_codeValue.fontStyle = FontStyles.Bold;
			Place((RectTransform)m_codeValue.transform, 32f, 100f, 420f, 54f);

			UnityEngine.UI.Button copyCode = MctierWidgets.CreatePrimaryButton("CopyCode", m_card, "复制房间号", new Action(CopyCode), 17f);
			Place((RectTransform)copyCode.transform, 32f, 166f, 190f, 44f);

			UnityEngine.UI.Button copyLink = MctierWidgets.CreateButton("CopyLink", m_card, "复制邀请链接", MctierTheme.CardBg, MctierTheme.TextPrimary, new Action(CopyLink), 17f);
			Place((RectTransform)copyLink.transform, 236f, 166f, 210f, 44f);

			TMP_Text linkCaption = MctierWidgets.CreateCaption("LinkCaption", m_card, "邀请链接", 15f);
			Place((RectTransform)linkCaption.transform, 32f, 232f, 420f, 22f);

			m_linkValue = MctierWidgets.CreateLabel("LinkValue", m_card, "", 14f, MctierTheme.TextSecondary);
			m_linkValue.enableWordWrapping = true;
			Place((RectTransform)m_linkValue.transform, 32f, 254f, 396f, 96f);

			Image qrFrame = MctierWidgets.CreateCard("QrFrame", m_card, 12, false);
			Place((RectTransform)qrFrame.transform, 452f, 78f, 276f, 276f);
			m_qrImage = MctierWidgets.CreateImage("QrImage", qrFrame.transform, Color.white);
			m_qrImage.preserveAspect = true;
			MctierWidgets.Stretch((RectTransform)m_qrImage.transform, 12f);

			m_qrHint = MctierWidgets.CreateCaption("QrHint", m_card, "扫描二维码获取房间邀请。", 13f);
			m_qrHint.alignment = TextAlignmentOptions.Center;
			m_qrHint.enableWordWrapping = true;
			Place((RectTransform)m_qrHint.transform, 452f, 362f, 276f, 60f);

			TMP_Text connectionTitle = MctierWidgets.CreateCaption("ConnectionTitle", m_card, "连接信息", 15f);
			Place((RectTransform)connectionTitle.transform, 32f, 396f, 396f, 24f);

			m_connection = MctierWidgets.CreateCaption("ConnectionInfo", m_card, "", 14f);
			m_connection.enableWordWrapping = true;
			m_connection.color = MctierTheme.TextSecondary;
			Place((RectTransform)m_connection.transform, 32f, 428f, 696f, 92f);

			m_status = MctierWidgets.CreateCaption("InviteStatus", m_card, "", 14f);
			m_status.color = MctierTheme.Success;
			Place((RectTransform)m_status.transform, 32f, 532f, 700f, 24f);

			m_root.gameObject.SetActive(false);
		}

		/// <summary>
		/// Anchors a fixed-size element to the card's top-left corner, so the layout reads like a
		/// document (x grows right, y grows down) instead of fighting the anchor system.
		/// </summary>
		private static void Place(RectTransform rect, float left, float top, float width, float height)
		{
			if (rect == null)
			{
				return;
			}
			rect.anchorMin = new Vector2(0f, 1f);
			rect.anchorMax = new Vector2(0f, 1f);
			rect.pivot = new Vector2(0f, 1f);
			rect.offsetMin = new Vector2(left, -(top + height));
			rect.offsetMax = new Vector2(left + width, -top);
		}

		// ---------------------------------------------------------------- visibility

		internal void Show()
		{
			if (m_root == null)
			{
				return;
			}
			m_visible = true;
			m_root.gameObject.SetActive(true);
			m_root.SetAsLastSibling();
			SetStatus("", MctierTheme.Success);
			Refresh();
		}

		internal void Hide()
		{
			m_visible = false;
			if (m_root != null)
			{
				m_root.gameObject.SetActive(false);
			}
			ReleaseQr();
		}

		// ---------------------------------------------------------------- content

		internal void Refresh()
		{
			if (!m_visible)
			{
				return;
			}
			OnlineLobbyService service = OnlineLobbyService.Instance;
			if (service == null)
			{
				SetStatus("联机服务未启动。", MctierTheme.Warning);
				return;
			}
			string code = service.RoomCode ?? "";
			if (m_codeValue != null)
			{
				m_codeValue.text = string.IsNullOrEmpty(code) ? "——" : code;
			}

			string invite = service.BuildInviteLink();
			if (m_linkValue != null)
			{
				m_linkValue.text = string.IsNullOrEmpty(invite) ? "（还没有房间信息）" : invite;
			}
			if (m_connection != null)
			{
				m_connection.text = DescribeConnection(service);
			}
			if (invite != m_renderedSource)
			{
				RenderQr(service, invite, code);
			}
		}

		private void RenderQr(OnlineLobbyService service, string invite, string code)
		{
			ReleaseQr();
			m_renderedSource = invite;
			if (string.IsNullOrEmpty(invite))
			{
				if (m_qrHint != null)
				{
					m_qrHint.text = "创建房间后这里会显示二维码。";
				}
				return;
			}

			string note = "";
			string error;
			Texture2D texture = QrTexture.Create(invite, QrModulePixels, QrQuietZone, out error);
			if (texture == null && service.Room != null)
			{
				// The full link can carry a node address and names; drop those and retry, because
				// the code plus the host address is all a guest actually needs.
				OnlineRoomInfo compact = service.Room.Clone();
				compact.ExternalNode = "";
				compact.CustomPeer = "";
				compact.RoomName = "";
				compact.HostName = "";
				string shorter = OnlineInvite.Build(compact);
				if (!string.IsNullOrEmpty(shorter) && shorter != invite)
				{
					texture = QrTexture.Create(shorter, QrModulePixels, QrQuietZone, out error);
					if (texture != null)
					{
						note = "二维码里是精简版链接（房间号 + 房主地址）。";
					}
				}
			}
			if (texture == null && !string.IsNullOrEmpty(code))
			{
				texture = QrTexture.Create(code, QrModulePixels, QrQuietZone, out error);
				if (texture != null)
				{
					note = "二维码里只有房间号：请让朋友加入时手动填写房主地址。";
				}
			}

			if (texture == null)
			{
				if (m_qrHint != null)
				{
					m_qrHint.text = string.IsNullOrEmpty(error) ? "二维码生成失败。" : error;
					m_qrHint.color = MctierTheme.Danger;
				}
				return;
			}

			m_qrTexture = texture;
			m_qrSprite = UnityEngine.Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
			m_qrSprite.name = "QrCodeSprite";
			if (m_qrImage != null)
			{
				m_qrImage.sprite = m_qrSprite;
				m_qrImage.type = Image.Type.Simple;
				m_qrImage.color = Color.white;
			}
			if (m_qrHint != null)
			{
				m_qrHint.color = MctierTheme.TextMuted;
				m_qrHint.text = note.Length > 0 ? note : "手机扫码即可加入，链接会随房间号一起更新。";
			}
		}

		/// <summary>
		/// Spells out where this machine can be reached. This is the text a player reads out when
		/// the other side is on a different network and there is no server to introduce them.
		/// </summary>
		private static string DescribeConnection(OnlineLobbyService service)
		{
			OnlineRoomInfo room = service.Room;
			StringBuilder builder = new StringBuilder();
			string mode = room == null || room.Transport == OnlineTransportKind.Direct ? "直连（无需服务器）" : "虚拟局域网（EasyTier，无需服务器）";
			builder.Append("模式：").Append(mode);

			if (service.IsHost)
			{
				ushort port = room == null ? OnlineNetConstants.DefaultGamePort : room.GamePort;
				List<string> addresses = OnlineLanDiscovery.GetLanAddresses();
				builder.Append('\n').Append("本机监听端口：").Append(port);
				if (addresses.Count == 0)
				{
					builder.Append('\n').Append("没有检测到局域网地址，请确认已连接网络。");
				}
				else
				{
					builder.Append('\n').Append("同一局域网直接可用：").Append(addresses[0]).Append(':').Append(port);
					for (int i = 1; i < addresses.Count && i < 3; i++)
					{
						builder.Append(" / ").Append(addresses[i]).Append(':').Append(port);
					}
					builder.Append('\n').Append("不同网络时，让对方在「加入房间」里填写上面的地址。");
				}
			}
			else if (room != null)
			{
				string address = room.ResolveConnectAddress();
				builder.Append('\n').Append("房主地址：").Append(string.IsNullOrEmpty(address) ? "未知" : address).Append(':').Append(room.GamePort);
				if (!room.HasDirectAddress)
				{
					builder.Append('\n').Append("还没有房主的地址：把房间号发给朋友，或让对方把地址告诉你。");
				}
			}
			return builder.ToString();
		}

		// ---------------------------------------------------------------- actions

		private void CopyCode()
		{
			OnlineLobbyService service = OnlineLobbyService.Instance;
			string code = service == null ? "" : service.RoomCode;
			if (string.IsNullOrEmpty(code))
			{
				SetStatus("还没有房间号。", MctierTheme.Warning);
				return;
			}
			SetClipboard(code);
			SetStatus("房间号已复制：" + code, MctierTheme.Success);
		}

		private void CopyLink()
		{
			OnlineLobbyService service = OnlineLobbyService.Instance;
			string invite = service == null ? "" : service.BuildInviteLink();
			if (string.IsNullOrEmpty(invite))
			{
				SetStatus("还没有邀请链接。", MctierTheme.Warning);
				return;
			}
			SetClipboard(invite);
			SetStatus("邀请链接已复制，直接发给朋友即可。", MctierTheme.Success);
		}

		private static void SetClipboard(string text)
		{
			try
			{
				GUIUtility.systemCopyBuffer = text;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[Online] Clipboard unavailable: " + ex.Message);
			}
		}

		private void SetStatus(string text, Color color)
		{
			if (m_status == null)
			{
				return;
			}
			m_status.text = text ?? "";
			m_status.color = color;
		}

		private void ReleaseQr()
		{
			if (m_qrImage != null)
			{
				m_qrImage.sprite = null;
			}
			if (m_qrSprite != null)
			{
				MctierWidgets.DestroyObject(m_qrSprite);
				m_qrSprite = null;
			}
			if (m_qrTexture != null)
			{
				MctierWidgets.DestroyObject(m_qrTexture);
				m_qrTexture = null;
			}
			m_renderedSource = "";
		}
	}
}
