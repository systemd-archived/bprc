using UnityEngine;
using UnityEngine.UI;

public class AppInterfaceSidebarItem : MonoBehaviour
{
	private AppInterfaceSidebar m_sidebar;

	private Toggle m_toggle;

	private Text m_text;

	private bool m_enabled;

	private GameObject m_relatedObject;

	private string m_relatedName;

	private Michsky.UI.Heat.PanelButton m_heatButton;

	/// <summary>True while this category is the one the sidebar has selected.</summary>
	public bool Enabled => m_enabled;

	/// <summary>Name of the element this category shows, or null when it shows none.</summary>
	public string RelatedName => m_relatedName;

	public void Initialize(string id, GameObject relatedObject)
	{
		m_sidebar = base.transform.parent.GetComponent<AppInterfaceSidebar>();
		m_text = base.transform.Find("Text").GetComponent<Text>();
		m_toggle = GetComponent<Toggle>();
		m_toggle.group = m_sidebar.GetComponent<ToggleGroup>();
		m_toggle.onValueChanged.AddListener(OnValueChanged);
		UITextLocale component = m_text.GetComponent<UITextLocale>();
		component.ID = id;
		component.UpdateText();
		m_heatButton = HeatUiPanelLayout.AddCategoryButton(gameObject, m_toggle, m_text);
		if (relatedObject != null)
		{
			m_relatedName = relatedObject.name;
		}
	}

	private void Update()
	{
		if (m_heatButton != null && m_heatButton.buttonText != m_text.text)
		{
			m_heatButton.buttonText = m_text.text;
			m_heatButton.UpdateUI();
		}
	}

	private void OnValueChanged(bool enabled)
	{
		m_sidebar.SetElementEnabled(this, enabled);
	}

	public void SetEnabled(bool enabled)
	{
		if (m_relatedObject == null && m_relatedName != null)
		{
			m_relatedObject = INAppInterface.Instance.FindElement(m_relatedName);
		}
		if (m_enabled != enabled)
		{
			m_enabled = enabled;
			if (enabled)
			{
				INAppInterface.Instance.SetTitle(TitleText);
			}
			if (m_relatedObject != null)
			{
				HeatUiPanelLayout.SetElementActive(m_relatedObject, enabled);
			}
		}
	}

	/// <summary>
	/// Puts the title back after the language changed. Only the selected category owns the title.
	/// </summary>
	public void RefreshTitle()
	{
		if (m_enabled)
		{
			INAppInterface.Instance.SetTitle(TitleText);
		}
	}

	/// <summary>
	/// The panel's title for this category: the app's name and this category's own label, which is the
	/// text the label component resolves rather than the id.
	/// </summary>
	private string TitleText
	{
		get { return INLocalization.Instance.GetText("AppInterface_AppName") + " " + m_text.text; }
	}
}
