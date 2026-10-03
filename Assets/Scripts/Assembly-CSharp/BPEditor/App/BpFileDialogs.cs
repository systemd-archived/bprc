using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 打开文件的对话框（安卓友好：路径文本框 + 内部目录列表，不依赖系统文件选择器）
public class BpFileDialogs
{
	private readonly RectTransform m_canvasRoot;
	private GameObject m_panel;

	public BpFileDialogs(RectTransform canvasRoot)
	{
		m_canvasRoot = canvasRoot;
	}

	public void Close()
	{
		if (m_panel != null) UnityEngine.Object.Destroy(m_panel);
		m_panel = null;
	}

	// 打开关卡：从"内部目录列表"或"手动输入路径"二选一
	public void ShowOpenLevel(string title, string initialPath, Action<string> onOpen, Action onCancel)
	{
		Close();

		m_panel = BpUi.Node("BpOpenDialog", m_canvasRoot);
		RectTransform pr = m_panel.AddComponent<RectTransform>();
		pr.anchorMin = new Vector2(0.5f, 0.5f);
		pr.anchorMax = new Vector2(0.5f, 0.5f);
		pr.pivot = new Vector2(0.5f, 0.5f);
		pr.sizeDelta = new Vector2(760f, 520f);
		pr.anchoredPosition = Vector2.zero;
		m_panel.AddComponent<UnityEngine.UI.Image>().color = new Color(0.12f, 0.14f, 0.18f, 0.99f);

		RectTransform col = BpUi.VerticalStretch(m_panel.transform, 6f, new RectOffset(12, 12, 12, 12), "Col");

		BpUi.Label(col, title, 22, TextAnchor.MiddleLeft, 30f);

		BpUi.Label(col, "① 手动输入/粘贴完整路径（安卓推荐）", 17, TextAnchor.MiddleLeft, 24f);
		string picked = initialPath;
		UnityEngine.UI.InputField input = BpUi.TextField(col, initialPath, v => picked = v, 32f, "例如 " + BpPaths.LevelsDir + "\\Level_14_data.bytes");

		BpUi.Label(col, "② 或从内部目录直接选（" + BpPaths.LevelsDir + "）", 17, TextAnchor.MiddleLeft, 24f);
		List<string> files = BpLevelFile.ListInternalFiles(BpPaths.LevelsDir);
		if (files.Count == 0)
		{
			BpUi.Label(col, "（内部目录暂无 .bytes/.yaml/.yml/.toml 文件）", 16, TextAnchor.MiddleLeft, 22f);
		}
		else
		{
			int shown = Mathf.Min(files.Count, 6);
			for (int i = 0; i < shown; i++)
			{
				string f = files[i];
				BpUi.Button(col, System.IO.Path.GetFileName(f), () => { picked = f; if (input != null) input.text = f; }, 30f,
					new Color(0.18f, 0.20f, 0.26f, 0.96f));
			}
			if (files.Count > shown)
				BpUi.Label(col, "…共 " + files.Count + " 个（其余可用上面文本框输入完整路径）", 16, TextAnchor.MiddleLeft, 22f);
		}

		Transform row = BpUi.Horizontal(col, 8f, new RectOffset(0, 0, 4, 0), "Buttons", 40f);
		BpUi.Button(row, "打开", () => { Close(); onOpen?.Invoke(picked); }, 38f, new Color(0.16f, 0.52f, 0.82f, 0.98f));
		BpUi.Button(row, "取消", () => { Close(); onCancel?.Invoke(); }, 38f, new Color(0.55f, 0.24f, 0.24f, 0.96f));
	}

	// 打开后展示文件信息 / 解析结果
	public void ShowInfo(string title, string body, Action onClose)
	{
		Close();
		m_panel = BpUi.Node("BpInfoDialog", m_canvasRoot);
		RectTransform pr = m_panel.AddComponent<RectTransform>();
		pr.anchorMin = new Vector2(0.5f, 0.5f);
		pr.anchorMax = new Vector2(0.5f, 0.5f);
		pr.pivot = new Vector2(0.5f, 0.5f);
		pr.sizeDelta = new Vector2(820f, 560f);
		pr.anchoredPosition = Vector2.zero;
		m_panel.AddComponent<UnityEngine.UI.Image>().color = new Color(0.12f, 0.14f, 0.18f, 0.99f);

		RectTransform col = BpUi.VerticalStretch(m_panel.transform, 6f, new RectOffset(12, 12, 12, 12), "Col");
		BpUi.Label(col, title, 22, TextAnchor.MiddleLeft, 30f);

		UnityEngine.UI.ScrollRect scroll = BpUi.ScrollView(col, out RectTransform content, 400f);
		LayoutElement le = scroll.gameObject.AddComponent<LayoutElement>();
		le.flexibleHeight = 1f;
		le.minHeight = 220f;
		Text bodyText = BpUi.Label(content, body, 17, TextAnchor.UpperLeft, 420f);
		bodyText.verticalOverflow = VerticalWrapMode.Overflow;

		Transform row = BpUi.Horizontal(col, 8f, new RectOffset(0, 0, 4, 0), "Buttons", 40f);
		BpUi.Button(row, "关闭", () => { Close(); onClose?.Invoke(); }, 38f, new Color(0.16f, 0.52f, 0.82f, 0.98f));
	}
}
