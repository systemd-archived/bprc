using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIPartSliderButton : UIPartButton
{
	public struct Range
	{
		public float Value;

		public float DefaultValue;

		public float Min;

		public float Max;

		public float Step;

		public float TimeInterval;

		public Range(float value, float defaultValue, float min, float max, float step, float timeInterval)
		{
			Value = value;
			DefaultValue = defaultValue;
			Min = min;
			Max = max;
			Step = step;
			TimeInterval = timeInterval;
		}

		public bool Contains(float value)
		{
			if (value >= Min)
			{
				return value <= Max;
			}
			return false;
		}

		public float Clamp(float value)
		{
			return Math.Clamp(value, Min, Max);
		}
	}

	public struct Timer
	{
		public int State;

		public float StartTime;

		public float LastTime;

		public bool Enabled => State > 0;

		public Timer(int state, float startTime, float lastTime)
		{
			State = state;
			StartTime = startTime;
			LastTime = lastTime;
		}
	}

	private enum SubButtonType
	{
		Up = 0,
		Down = 1,
		Reset = 2
	}

	private UIButton m_upButton;

	private UIButton m_downButton;

	private Image m_upButtonTexture;

	private Image m_downButtonTexture;

	private Image m_background;

	private Image m_verticalFill;

	private Image m_horizontalFill;

	private Text m_text;

	private UIPartTriggerButton m_upTriggerButton;

	private UIPartTriggerButton m_downTriggerButton;

	private UIPartTriggerButton m_resetTriggerButton;

	private bool m_expanded;

	private float m_expandProgress;

	private Vector2 m_fillSize;

	private Range m_range;

	private Timer m_upTimer;

	private Timer m_downTimer;

	public UIButton UpButton => m_upButton;

	public UIButton DownButton => m_downButton;

	public float Value => m_range.Clamp(m_range.Value);

	public Timer UpTimer => m_upTimer;

	public Timer DownTimer => m_downTimer;

	public override int SubButtonCount
	{
		get
		{
			if (!m_expanded)
			{
				return 0;
			}
			return 3;
		}
	}

	public override IEnumerable<UIPartButton> SubButtons
	{
		get
		{
			if (m_expanded)
			{
				yield return m_upTriggerButton;
				yield return m_downTriggerButton;
				yield return m_resetTriggerButton;
			}
		}
	}

	protected override void Awake()
	{
		base.Awake();
		m_upButton = base.transform.Find("UpButton").GetComponent<UIButton>();
		m_downButton = base.transform.Find("DownButton").GetComponent<UIButton>();
		m_upButtonTexture = m_upButton.transform.Find("Image").GetComponent<Image>();
		m_downButtonTexture = m_downButton.transform.Find("Image").GetComponent<Image>();
		AddEvents(m_upButton);
		AddEvents(m_downButton);
		m_background = base.transform.Find("Background").GetComponent<Image>();
		m_verticalFill = base.transform.Find("VerticalFill").GetComponent<Image>();
		m_horizontalFill = base.transform.Find("HorizontalFill").GetComponent<Image>();
		m_text = base.transform.Find("Text").GetComponent<Text>();
		m_fillSize = ((RectTransform)base.transform).sizeDelta;
		m_upTriggerButton = CreateSubButton(SubButtonType.Up, "UpTriggerButton", "SliderButton_UpTrigger_Sprite");
		m_downTriggerButton = CreateSubButton(SubButtonType.Down, "DownTriggerButton", "SliderButton_DownTrigger_Sprite");
		m_resetTriggerButton = CreateSubButton(SubButtonType.Reset, "ResetTriggerButton", "SliderButton_ResetTrigger_Sprite");
		m_resetTriggerButton.Button.PointerDown += delegate
		{
			RestoreDefaultValue();
		};
	}

	private void OnEnable()
	{
		m_upButtonTexture.canvasRenderer.SetColor(new Color(1f, 1f, 1f, 0.6f));
		m_downButtonTexture.canvasRenderer.SetColor(new Color(1f, 1f, 1f, 0.6f));
	}

	private UIPartTriggerButton CreateSubButton(SubButtonType buttonType, string name, string spriteName)
	{
		GameObject obj = UnityEngine.Object.Instantiate(UIPartButtonList.Instance.TriggerButtonPrefab);
		obj.name = name;
		obj.transform.SetParent(base.transform, worldPositionStays: false);
		obj.SetActive(value: true);
		obj.SetActive(value: false);
		UIPartTriggerButton component = obj.GetComponent<UIPartTriggerButton>();
		Texture texture = INUnity.LoadTexture("UIPartButtonAtlas");
		INSpriteData iNSpriteData = INSpriteManager.Instance.GetAtlasData(texture.name)[spriteName];
		component.SetSprite(enabled: true, texture, iNSpriteData.FlippedUVRect, iNSpriteData.PixelSize / 0.7f, Quaternion.identity);
		AddEvents(component.Button);
		return obj.GetComponent<UIPartTriggerButton>();
	}

	private void AddEvents(UIButton button)
	{
		button.PointerDown += delegate(PointerEventData eventData)
		{
			OnPointerDown(button, eventData);
		};
		button.Drag += delegate(PointerEventData eventData)
		{
			OnPointerDrag(button, eventData);
		};
		button.PointerUp += delegate(PointerEventData eventData)
		{
			OnPointerUp(button, eventData);
		};
		button.PointerExit += delegate(PointerEventData eventData)
		{
			OnPointerExit(button, eventData);
		};
	}

	public void SetRange(Range range)
	{
		m_range = range;
	}

	public void SetValue(float value)
	{
		m_range.Value = value;
		m_text.text = m_range.Value.ToString("0.##");
		float value2 = (m_range.Value - m_range.Min) / (m_range.Max - m_range.Min);
		((RectTransform)m_verticalFill.transform).sizeDelta = new Vector2(m_fillSize.x, m_fillSize.y * Math.Clamp(value2, 0f, 1f));
	}

	public void SetExpanded(bool expanded)
	{
		SetExpanded(expanded, render: true);
	}

	public void SetExpanded(bool expanded, bool render)
	{
		if (m_expanded ^ expanded)
		{
			m_expanded = expanded;
			m_upTriggerButton.gameObject.SetActive(expanded);
			m_downTriggerButton.gameObject.SetActive(expanded);
			m_resetTriggerButton.gameObject.SetActive(expanded);
			if (render)
			{
				UIPartButtonList.Instance.RenderButtons();
			}
		}
	}

	private void SetExpandProgress(float expandProgress)
	{
		if (m_expandProgress != expandProgress)
		{
			m_expandProgress = expandProgress;
			((RectTransform)m_horizontalFill.transform).sizeDelta = new Vector2(m_fillSize.x * expandProgress, m_fillSize.y);
		}
	}

	public override void Initialize()
	{
		base.Initialize();
		m_background.color = m_disabledColor;
		m_verticalFill.color = m_enabledColor;
		m_horizontalFill.color = new Color(1f, 1f, 1f, 0.5f);
		SetValue(m_range.Value);
		UIPartButtonInfo info = m_info;
		info.ButtonType = UIPartButtonType.Trigger;
		m_upTriggerButton.SetInfo(info);
		m_downTriggerButton.SetInfo(info);
		m_resetTriggerButton.SetInfo(info);
		m_upTriggerButton.Initialize();
		m_downTriggerButton.Initialize();
		m_resetTriggerButton.Initialize();
	}

	private void Increase(int times)
	{
		if (!(Math.Abs(m_range.Step) < 1E-05f))
		{
			int num = (int)MathF.Round(m_range.Value / m_range.Step);
			int min = (int)MathF.Ceiling(m_range.Min / m_range.Step);
			int max = (int)MathF.Floor(m_range.Max / m_range.Step);
			float num2 = (float)Math.Clamp(num + times, min, max) * m_range.Step;
			if (num2 != m_range.Value)
			{
				SetValue(num2);
				OnTriggered();
			}
		}
	}

	private void Decrease(int times)
	{
		Increase(-times);
	}

	private void OnTriggered()
	{
		foreach (BasePart part in m_parts)
		{
			if (part != null)
			{
				part.OnSliderButtonTriggered(this);
			}
		}
		Contraption.Instance.OnSliderButtonTriggered(this);
	}

	private void OnPointerDown(UIButton button, PointerEventData eventData)
	{
		if (eventData.button == PointerEventData.InputButton.Right)
		{
			SetExpanded(!m_expanded);
		}
	}

	private void OnPointerDrag(UIButton button, PointerEventData eventData)
	{
		if (eventData.button != PointerEventData.InputButton.Left)
		{
			return;
		}
		Vector2 vector = eventData.position - eventData.pressPosition;
		float x = vector.x;
		float y = vector.y;
		float num = 5f * base.transform.localScale.x;
		float num2 = 100f * base.transform.localScale.x;
		if (!m_expanded && y < x && y > 0f - x)
		{
			if (x >= num2)
			{
				ResetPointers();
				SetExpanded(expanded: true);
				SetExpandProgress(0f);
			}
			else if (x >= num)
			{
				SetExpandProgress((x - num) / (num2 - num));
			}
		}
		if (m_expanded && y > x && y < 0f - x)
		{
			if (0f - x >= num2)
			{
				ResetPointers();
				SetExpanded(expanded: false);
				SetExpandProgress(0f);
			}
			else if (0f - x >= num)
			{
				SetExpandProgress(1f - (0f - x - num) / (num2 - num));
			}
		}
	}

	private void OnPointerUp(UIButton button, PointerEventData eventData)
	{
		if (eventData.button == PointerEventData.InputButton.Left)
		{
			SetExpandProgress(0f);
		}
	}

	private void OnPointerExit(UIButton button, PointerEventData eventData)
	{
		if (eventData.button == PointerEventData.InputButton.Left)
		{
			SetExpandProgress(0f);
		}
	}

	private void ResetPointers()
	{
		m_upButton.ResetPointer();
		m_downButton.ResetPointer();
		m_upTriggerButton.Button.ResetPointer();
		m_downTriggerButton.Button.ResetPointer();
		m_resetTriggerButton.Button.ResetPointer();
	}

	private void RestoreDefaultValue()
	{
		SetValue(m_range.DefaultValue);
		OnTriggered();
	}

	private void UpdateTimer(ref Timer timer, bool pressed, KeyCode keyCode, out int result)
	{
		if (m_expandProgress != 0f)
		{
			result = 0;
			timer.State = 0;
		}
		else if (pressed || (m_buttonList.SelectedButton == this && Input.GetKey(keyCode)))
		{
			result = 0;
			float time = Time.time;
			switch (timer.State)
			{
			case 0:
				result = 1;
				timer.State = 1;
				timer.StartTime = time;
				break;
			case 1:
				if (time - timer.StartTime >= 0.2f)
				{
					result = 1;
					timer.State = 2;
					timer.LastTime = time;
				}
				break;
			case 2:
				if (time - timer.LastTime >= m_range.TimeInterval)
				{
					result = (int)((time - timer.LastTime) / m_range.TimeInterval);
					timer.LastTime += (float)result * m_range.TimeInterval;
				}
				break;
			}
		}
		else
		{
			result = 0;
			timer.State = 0;
		}
	}

	private void Update()
	{
		bool flag = m_upTimer.Enabled;
		bool flag2 = m_downTimer.Enabled;
		UpdateTimer(ref m_upTimer, IsPressed(m_upButton) || IsPressed(m_upTriggerButton.Button), KeyCode.UpArrow, out var result);
		UpdateTimer(ref m_downTimer, IsPressed(m_downButton) || IsPressed(m_downTriggerButton.Button), KeyCode.DownArrow, out var result2);
		if (result != 0)
		{
			Increase(result);
		}
		if (result2 != 0)
		{
			Decrease(result2);
		}
		if (flag && !m_upTimer.Enabled)
		{
			CrossFadeColor(m_upButtonTexture, new Color(1f, 1f, 1f, 0.6f));
		}
		else if (!flag && m_upTimer.Enabled)
		{
			CrossFadeColor(m_upButtonTexture, new Color(1f, 1f, 1f, 1f));
		}
		if (flag2 && !m_downTimer.Enabled)
		{
			CrossFadeColor(m_downButtonTexture, new Color(1f, 1f, 1f, 0.6f));
		}
		else if (!flag2 && m_downTimer.Enabled)
		{
			CrossFadeColor(m_downButtonTexture, new Color(1f, 1f, 1f, 1f));
		}
		static void CrossFadeColor(Image image, Color color)
		{
			image.CrossFadeColor(color, 0.1f, ignoreTimeScale: true, useAlpha: true);
		}
		static bool IsPressed(UIButton button)
		{
			if (button.IsPointerDown)
			{
				return button.IsPointerInside;
			}
			return false;
		}
	}

	public override void Reset()
	{
		base.Reset();
		m_text.text = string.Empty;
		m_range = default(Range);
		m_upTimer = default(Timer);
		m_downTimer = default(Timer);
		SetExpanded(expanded: false, render: false);
		SetExpandProgress(0f);
	}
}
