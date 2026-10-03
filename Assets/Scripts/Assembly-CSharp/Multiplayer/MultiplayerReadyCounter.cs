using UnityEngine;

namespace BadPiggies.Multiplayer
{
public class MultiplayerReadyCounter : MonoBehaviour
{
	private const float HorizontalGap = -1.45f;

	private TextMesh m_label;

	private Transform m_anchor;

	public void UpdateLabel(GameObject checkmark, string text)
	{
		if (checkmark == null)
		{
			Hide();
		}
		else if (!(m_label == null) || TryCreateLabel(checkmark))
		{
			m_anchor = checkmark.transform;
			m_label.gameObject.SetActive(value: true);
			m_label.text = text;
			Transform transform = m_label.transform;
			transform.position = m_anchor.position + new Vector3(-1.45f, 0f, 0f);
			transform.position = new Vector3(transform.position.x, transform.position.y, m_anchor.position.z - 0.1f);
		}
	}

	public void Hide()
	{
		if (m_label != null)
		{
			m_label.gameObject.SetActive(value: false);
		}
	}

	public void RemoveLabel()
	{
		if (m_label != null)
		{
			Object.Destroy(m_label.gameObject);
		}
		m_label = null;
		m_anchor = null;
	}

	private void OnDisable()
	{
		RemoveLabel();
	}

	private bool TryCreateLabel(GameObject checkmark)
	{
		TextMesh textMesh = FindFontSource(checkmark);
		if (textMesh == null)
		{
			return false;
		}
		GameObject gameObject = Object.Instantiate(textMesh.gameObject, checkmark.transform.parent);
		gameObject.name = "MultiplayerReadyCounter";
		gameObject.transform.localRotation = textMesh.transform.localRotation;
		gameObject.transform.localScale = textMesh.transform.localScale;
		m_label = gameObject.GetComponent<TextMesh>();
		m_label.anchor = TextAnchor.MiddleRight;
		m_label.alignment = TextAlignment.Right;
		Component[] componentsInChildren = gameObject.GetComponentsInChildren<Component>(includeInactive: true);
		foreach (Component component in componentsInChildren)
		{
			if (!(component == null) && !(component is Transform) && !(component is TextMesh) && !(component is MeshRenderer) && !(component is MeshFilter))
			{
				Object.Destroy(component);
			}
		}
		return true;
	}

	private static TextMesh FindFontSource(GameObject checkmark)
	{
		Transform parent = checkmark.transform.parent;
		if (parent != null)
		{
			TextMesh textMesh = FindUsableLabel(parent.GetComponentsInChildren<TextMesh>(includeInactive: true));
			if (textMesh != null)
			{
				return textMesh;
			}
		}
		GameObject gameObject = GameObject.Find("InGameGUI");
		if (gameObject != null)
		{
			return FindUsableLabel(gameObject.GetComponentsInChildren<TextMesh>(includeInactive: true));
		}
		return null;
	}

	private static TextMesh FindUsableLabel(TextMesh[] candidates)
	{
		foreach (TextMesh textMesh in candidates)
		{
			if (textMesh != null && textMesh.font != null && textMesh.name != "MultiplayerReadyCounter")
			{
				return textMesh;
			}
		}
		return null;
	}
}
}
