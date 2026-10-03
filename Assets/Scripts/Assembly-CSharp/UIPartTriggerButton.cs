using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIPartTriggerButton : UIPartButton
{
	private enum TriggerButtonState
	{
		Disabled = 0,
		Highlighted = 1,
		Pressed = 2,
		Enabled = 3
	}

	private UIButton m_button;

	private Image m_texture;

	private TriggerButtonState m_state;

	private bool m_consistent;

	private bool m_multiple;

	private Color m_color;

	private Color m_highlightedColor;

	private Color m_pressedColor;

	public bool Enabled => m_state == TriggerButtonState.Enabled;

	public bool Consistent => m_consistent;

	public bool Multiple => m_multiple;

	public UIButton Button => m_button;

	protected override void Awake()
	{
		base.Awake();
		m_button = base.transform.Find("Button").GetComponent<UIButton>();
		m_button.PointerDown += OnPointerDown;
		m_texture = m_button.GetComponent<Image>();
		m_color = m_disabledColor;
	}

	private void OnEnable()
	{
		m_texture.canvasRenderer.SetColor(m_color);
	}

	public void SetConsistent(bool consistent)
	{
		m_consistent = consistent;
	}

	public void SetMultiple(bool multiple)
	{
		m_multiple = multiple;
	}

	public override void Initialize()
	{
		base.Initialize();
		m_highlightedColor = Color.Lerp(m_disabledColor, m_enabledColor, 0.5f);
		m_pressedColor = m_enabledColor;
		UpdateState(colorTint: false);
		m_texture.canvasRenderer.SetColor(m_color);
	}

	public void OnTriggered()
	{
		INRc1Controls.PrepareNativeTriggerButton();
		if (m_multiple)
		{
			TriggerMultipleParts();
		}
		else
		{
			TriggerSinglePart();
		}
		Contraption.Instance.OnButtonTriggered(this);
	}

	private void TriggerMultipleParts()
	{
		bool flag = false;
		foreach (BasePart part in m_parts)
		{
			if (part.IsEnabled())
			{
				flag = true;
				break;
			}
		}
		foreach (BasePart part2 in m_parts)
		{
			if (!m_consistent || (!flag ^ part2.IsEnabled()))
			{
				part2.OnButtonTriggered(this);
			}
		}
	}

	private void TriggerSinglePart()
	{
		bool flag = false;
		foreach (BasePart part in m_parts)
		{
			if (part.IsEnabled())
			{
				flag = true;
				break;
			}
		}
		BasePart basePart = null;
		float num = 0f;
		Vector3 position = Contraption.Instance.Pig.transform.position;
		foreach (BasePart part2 in m_parts)
		{
			if (!m_consistent || (!flag ^ part2.IsEnabled()))
			{
				Vector3 position2 = part2.transform.position;
				float num2 = position.x - position2.x;
				float num3 = position.y - position2.y;
				float num4 = num2 * num2 + num3 * num3;
				if (num4 > num)
				{
					num = num4;
					basePart = part2;
				}
			}
		}
		basePart?.OnButtonTriggered(this);
	}

	private void OnPointerDown(PointerEventData eventData)
	{
		OnTriggered();
	}

	private void Update()
	{
		UpdateState(colorTint: true);
	}

	private void UpdateState(bool colorTint)
	{
		bool flag = false;
		foreach (BasePart part in m_parts)
		{
			if (part.IsEnabled())
			{
				flag = true;
				break;
			}
		}
		TriggerButtonState state = m_state;
		if (flag)
		{
			m_state = TriggerButtonState.Enabled;
			m_color = m_enabledColor;
		}
		else if (m_button.IsPointerDown && m_button.IsPointerInside)
		{
			m_state = TriggerButtonState.Pressed;
			m_color = m_pressedColor;
		}
		else if (m_button.IsPointerInside)
		{
			m_state = TriggerButtonState.Highlighted;
			m_color = m_highlightedColor;
		}
		else
		{
			m_state = TriggerButtonState.Disabled;
			m_color = m_disabledColor;
		}
		if (colorTint && state != m_state)
		{
			m_texture.CrossFadeColor(m_color, 0.1f, ignoreTimeScale: true, useAlpha: true);
		}
	}

	public override void Reset()
	{
		base.Reset();
		m_state = TriggerButtonState.Disabled;
		m_color = m_disabledColor;
	}
}
