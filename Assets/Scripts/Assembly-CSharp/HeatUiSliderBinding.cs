using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class HeatUiSliderBinding : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private InputField m_input;
    private Slider m_original;
    private Slider m_slider;
    private Michsky.UI.Heat.SliderManager m_manager;
    private bool m_dragging;
    private float m_lastValue = float.NaN;

    public void Initialize(InputField input, Slider original)
    {
        m_input = input;
        m_original = original;
        m_slider = GetComponent<Slider>();
        m_manager = GetComponent<Michsky.UI.Heat.SliderManager>();
        m_slider.minValue = original != null ? original.minValue : 0;
        m_slider.maxValue = original != null ? original.maxValue : 1;
        m_slider.wholeNumbers = original != null && original.wholeNumbers;
        m_manager.useSounds = false;
        m_slider.onValueChanged.AddListener(value =>
        {
            if (m_original != null) { m_original.value = value; }
            else if (m_input != null)
            {
                string text = value.ToString("0.###", CultureInfo.InvariantCulture);
                m_input.text = text;
                m_input.onEndEdit.Invoke(text);
            }
        });
        LateUpdate();
    }

    private void LateUpdate()
    {
        if (m_slider == null || m_dragging) { return; }
        float value;
        if (m_original != null)
        {
            m_slider.interactable = m_original.interactable;
            m_slider.SetValueWithoutNotify(m_original.value);
        }
        else if (m_input != null && float.TryParse(m_input.text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            m_slider.interactable = m_input.interactable && !m_input.readOnly;
            m_slider.SetValueWithoutNotify(value);
        }
        if (!Mathf.Approximately(m_lastValue, m_slider.value))
        {
            m_lastValue = m_slider.value;
            m_manager.UpdateUI();
        }
    }

    public void OnPointerDown(PointerEventData data)
    {
        m_dragging = true;
        if (m_original != null)
        {
            EventTrigger trigger = m_original.GetComponent<EventTrigger>();
            if (trigger != null) { trigger.OnPointerDown(data); }
        }
    }

    public void OnPointerUp(PointerEventData data)
    {
        m_dragging = false;
        if (m_original != null)
        {
            EventTrigger trigger = m_original.GetComponent<EventTrigger>();
            if (trigger != null) { trigger.OnPointerUp(data); }
        }
    }
}
