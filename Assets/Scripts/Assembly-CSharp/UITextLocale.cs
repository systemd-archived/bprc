using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UITextLocale : MonoBehaviour
{
	[SerializeField]
	private string m_id;

	public string ID
	{
		get
		{
			return m_id;
		}
		set
		{
			m_id = value;
		}
	}

	public string Text => INLocalization.Instance != null ? INLocalization.Instance.GetText(m_id) : string.Empty;

	private void Awake()
	{
		UpdateText();
	}

	/// <summary>
	/// Writes the localized string into whichever text component this object carries.
	/// </summary>
	/// <remarks>
	/// Heat UI labels are TextMeshPro, while this project's own panels use
	/// <see cref="UnityEngine.UI.Text"/>. Both are handled so a label keeps working after its panel is
	/// rebuilt from Heat UI prefabs, and neither branch is required to be present.
	/// </remarks>
	public void UpdateText()
	{
		if (string.IsNullOrEmpty(m_id) || INLocalization.Instance == null)
		{
			return;
		}

		TMP_Text tmp = GetComponent<TMP_Text>();
		var row = GetComponentInParent<Michsky.UI.Heat.SettingsElement>();
		if (row != null) { HeatUiOptionHelp.Bind(row.gameObject, m_id, Text); }
		if (tmp != null)
		{
			tmp.text = Text;
			return;
		}

		UnityEngine.UI.Text component = GetComponent<UnityEngine.UI.Text>();
		if (component != null)
		{
			component.text = Text;
		}
	}
}
