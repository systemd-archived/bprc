using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

public class ToggleSwitch : Selectable, IPointerClickHandler, IEventSystemHandler, ISubmitHandler, ICanvasElement
{
	[Serializable]
	public class ToggleEvent : UnityEvent<bool>
	{
	}

	[SerializeField]
	private bool m_isOn;

	[SerializeField]
	private Image m_border;

	[SerializeField]
	private Image m_background;

	[SerializeField]
	private Image m_ellipse;

	[SerializeField]
	private Color m_enabledColor;

	[SerializeField]
	private Color m_disabledColor;

	private ToggleEvent m_onValueChanged = new ToggleEvent();

	public bool IsOn
	{
		get
		{
			return m_isOn;
		}
		set
		{
			Set(value);
		}
	}

	public ToggleEvent OnValueChanged => m_onValueChanged;

	Transform ICanvasElement.transform => base.transform;

	protected ToggleSwitch()
	{
	}

	protected override void Start()
	{
		PlayEffect(instant: true);
	}

	protected override void OnEnable()
	{
		base.OnEnable();
		PlayEffect(instant: true);
	}

	protected override void OnDisable()
	{
		base.OnDisable();
	}

	public void SetIsOnWithoutNotify(bool value)
	{
		Set(value, sendCallback: false);
	}

	private void Set(bool value, bool sendCallback = true)
	{
		if (m_isOn != value)
		{
			m_isOn = value;
			PlayEffect(instant: false);
			if (sendCallback)
			{
				m_onValueChanged.Invoke(m_isOn);
			}
		}
	}

	private void PlayEffect(bool instant)
	{
		float duration = (instant ? 0f : 0.2f);
		if (m_isOn)
		{
			UIExtensions.PlayLinearAnimation(0f, 1f, duration, ignoreTimeScale: true, PlayAnimation).Forget();
		}
		else
		{
			UIExtensions.PlayLinearAnimation(1f, 0f, duration, ignoreTimeScale: true, PlayAnimation).Forget();
		}
	}

	private void PlayAnimation(float t)
	{
		m_background.canvasRenderer.SetAlpha(t);
		m_ellipse.canvasRenderer.SetColor(Color.Lerp(m_disabledColor, m_enabledColor, t));
		m_ellipse.rectTransform.anchoredPosition = m_ellipse.rectTransform.anchoredPosition.WithX(-50f + 100f * t);
	}

	private void InternalToggle()
	{
		if (IsActive() && IsInteractable())
		{
			Set(!m_isOn);
		}
	}

	public virtual void OnPointerClick(PointerEventData eventData)
	{
		if (eventData.button == PointerEventData.InputButton.Left)
		{
			InternalToggle();
		}
	}

	public virtual void OnSubmit(BaseEventData eventData)
	{
		InternalToggle();
	}

	public void Rebuild(CanvasUpdate executing)
	{
	}

	public void LayoutComplete()
	{
	}

	public void GraphicUpdateComplete()
	{
	}
}
