using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AppInterfaceSidebar : MonoBehaviour
{
	[Serializable]
	public class SidebarElementData
	{
		public string Name;

		public GameObject RelatedObject;
	}

	[SerializeField]
	private List<SidebarElementData> m_sidebarElements;

	[SerializeField]
	private GameObject m_sidebarElementTemplate;

	// 开发用开关：这里列出的本地化 id 对应的侧栏入口会被隐藏（页面 prefab 与注册都保留）。
	// 想重新显示：把对应 id 从集合里删掉即可（无需改 prefab）。
	public static readonly HashSet<string> HiddenEntries = new HashSet<string>
	{
		"AppInterface_CodeEditor",
		"AppInterface_ClassBrowser"
	};

	private bool m_enabled;

	public bool Enabled => m_enabled;

	private void Awake()
	{
		m_enabled = true;
		GenerateSidebarElements();
	}

	private void GenerateSidebarElements()
	{
		GameObject sidebarElementTemplate = m_sidebarElementTemplate;
		int num = 0;
		foreach (SidebarElementData sidebarElement in m_sidebarElements)
		{
			if (HiddenEntries.Contains(sidebarElement.Name))
			{
				continue;
			}
			GameObject obj = UnityEngine.Object.Instantiate(sidebarElementTemplate);
			obj.SetActive(value: true);
			obj.name = "SidebarElement_" + (num + 1);
			obj.transform.SetParent(base.transform, worldPositionStays: false);
			obj.GetComponent<AppInterfaceSidebarItem>().Initialize(sidebarElement.Name, sidebarElement.RelatedObject);
			num++;
		}
	}

	public void SetEnabled(bool enabled)
	{
		if (m_enabled ^ enabled)
		{
			m_enabled = enabled;
			CanvasGroup component = GetComponent<CanvasGroup>();
			component.alpha = (enabled ? 1f : 0f);
			component.blocksRaycasts = enabled;
		}
	}

	public void SetElementEnabled(AppInterfaceSidebarItem element, bool enabled)
	{
		if (!GetComponent<ToggleGroup>().AnyTogglesOn())
		{
			string text = INLocalization.Instance.GetText("AppInterface_AppName");
			string text2 = INLocalization.Instance.GetText("AppInterface_Name");
			INAppInterface.Instance.SetTitle(text + " " + text2);
		}
		element.SetEnabled(enabled);
	}

	/// <summary>
	/// Puts the title back in the language that is now on.
	/// </summary>
	/// <remarks>
	/// The title is composed rather than resolved from an id - the app's name and the selected category's
	/// label - so refreshing the labels does not reach it; the category whose label was just re-read has to
	/// compose it again. With no category selected the panel shows its own name, which is how it opens.
	/// </remarks>
	public void RefreshTitle()
	{
		foreach (AppInterfaceSidebarItem item in GetComponentsInChildren<AppInterfaceSidebarItem>(includeInactive: true))
		{
			if (item.Enabled)
			{
				item.RefreshTitle();
				return;
			}
		}
		INAppInterface.Instance.SetTitle(INLocalization.Instance.GetText("AppInterface_AppName") + " " +
			INLocalization.Instance.GetText("AppInterface_Name"));
	}
}
