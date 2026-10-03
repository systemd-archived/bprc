using UnityEngine;
using UnityEngine.UI;

// 运行时构建 UI 时的字体工具。
// 应用界面里已在使用的字体（侧栏文本等）支持中文；优先借用它，避免内置 Arial 缺字。
public static class INUiUtil
{
	private static Font s_font;

	public static Font UiFont
	{
		get
		{
			if (s_font != null)
			{
				return s_font;
			}
			Font f = ResolveFromAppInterface();
			if (f != null)
			{
				s_font = f;
				return f;
			}
			return INUnity.ArialFont != null ? INUnity.ArialFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
		}
	}

	private static Font ResolveFromAppInterface()
	{
		try
		{
			INAppInterface app = INAppInterface.Instance;
			if (app == null)
			{
				return null;
			}
			GameObject sidebar = app.Sidebar;
			if (sidebar != null)
			{
				Text t = sidebar.GetComponentInChildren<Text>(true);
				if (t != null && t.font != null)
				{
					return t.font;
				}
			}
			GameObject bar = app.ActionBar;
			if (bar != null)
			{
				Text t2 = bar.GetComponentInChildren<Text>(true);
				if (t2 != null && t2.font != null)
				{
					return t2.font;
				}
			}
		}
		catch
		{
		}
		return null;
	}
}
