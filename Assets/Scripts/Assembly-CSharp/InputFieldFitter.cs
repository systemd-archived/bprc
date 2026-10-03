using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InputFieldFitter : UIBehaviour, ILayoutSelfController, ILayoutController
{
	[SerializeField]
	private float m_fontSize;

	[SerializeField]
	private float m_padding;

	[SerializeField]
	private float m_maxSize;

	private RectTransform m_rectTransform;

	private InputField m_inputField;

	public float FontSize
	{
		get
		{
			return m_fontSize;
		}
		set
		{
			m_fontSize = value;
		}
	}

	public float MaxSize
	{
		get
		{
			return m_maxSize;
		}
		set
		{
			m_maxSize = value;
		}
	}

	protected override void Awake()
	{
		m_rectTransform = GetComponent<RectTransform>();
		m_inputField = GetComponent<InputField>();
		m_inputField.onValueChanged.AddListener(OnValueChanged);
	}

	protected override void OnEnable()
	{
		base.OnEnable();
		SetDirty();
	}

	protected override void OnDisable()
	{
		LayoutRebuilder.MarkLayoutForRebuild(m_rectTransform);
		base.OnDisable();
	}

	protected override void OnRectTransformDimensionsChange()
	{
		SetDirty();
	}

	protected void SetDirty()
	{
		if (IsActive())
		{
			LayoutRebuilder.MarkLayoutForRebuild(m_rectTransform);
		}
	}

	public void SetLayoutHorizontal()
	{
	}

	public void SetLayoutVertical()
	{
		if (!(m_rectTransform == null) && !(m_inputField == null))
		{
			int lineCount = GetLineCount(m_inputField.text);
			float num = ((m_fontSize != 0f) ? m_fontSize : ((float)m_inputField.textComponent.fontSize));
			float num2 = (float)lineCount * num + m_padding;
			if (m_maxSize != 0f && num2 > m_maxSize)
			{
				num2 = m_maxSize;
			}
			m_rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, num2);
		}
	}

	private void OnValueChanged(string text)
	{
		SetLayoutVertical();
	}

	private int GetLineCount(string text)
	{
		int num = 0;
		int num2 = 1;
		while (num < text.Length)
		{
			switch (text[num++])
			{
			case '\n':
				if (num < text.Length && text[num] == '\r')
				{
					num++;
				}
				num2++;
				break;
			case '\r':
				num2++;
				break;
			}
		}
		return num2;
	}
}
