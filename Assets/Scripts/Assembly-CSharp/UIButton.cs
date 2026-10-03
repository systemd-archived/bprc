using System;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIButton : UnityEngine.UI.Button, IDragHandler, IEventSystemHandler
{
	public enum ButtonState
	{
		Normal = 0,
		Highlighted = 1,
		Pressed = 2,
		Selected = 3,
		Disabled = 4
	}

	private bool m_isPointerDown;

	private bool m_isPointerInside;

	public bool IsPointerDown => m_isPointerDown;

	public bool IsPointerInside => m_isPointerInside;

	public ButtonState CurrentState => ToButtonState(base.currentSelectionState);

	public event Action<PointerEventData> PointerDown;

	public event Action<PointerEventData> PointerUp;

	public event Action<PointerEventData> PointerEnter;

	public event Action<PointerEventData> PointerExit;

	public event Action<PointerEventData> PointerClick;

	public event Action<PointerEventData> Drag;

	public event Action<ButtonState> StateTransitioned;

	protected override void OnDisable()
	{
		base.OnDisable();
		m_isPointerDown = false;
		m_isPointerInside = false;
	}

	public void ResetPointer()
	{
		m_isPointerDown = false;
		m_isPointerInside = false;
		InstantClearState();
	}

	public void ResetEvents()
	{
		this.PointerClick = null;
		this.StateTransitioned = null;
	}

	protected override void DoStateTransition(SelectionState state, bool instant)
	{
		base.DoStateTransition(state, instant);
		this.StateTransitioned?.Invoke(ToButtonState(state));
	}

	public override void OnPointerDown(PointerEventData eventData)
	{
		base.OnPointerDown(eventData);
		if (eventData.button == PointerEventData.InputButton.Left)
		{
			m_isPointerDown = true;
		}
		this.PointerDown?.Invoke(eventData);
	}

	public override void OnPointerUp(PointerEventData eventData)
	{
		base.OnPointerUp(eventData);
		if (eventData.button == PointerEventData.InputButton.Left)
		{
			m_isPointerDown = false;
		}
		this.PointerUp?.Invoke(eventData);
	}

	public override void OnPointerEnter(PointerEventData eventData)
	{
		base.OnPointerEnter(eventData);
		m_isPointerInside = true;
		this.PointerEnter?.Invoke(eventData);
	}

	public override void OnPointerExit(PointerEventData eventData)
	{
		base.OnPointerExit(eventData);
		m_isPointerInside = false;
		this.PointerExit?.Invoke(eventData);
	}

	public override void OnPointerClick(PointerEventData eventData)
	{
		base.OnPointerClick(eventData);
		this.PointerClick?.Invoke(eventData);
	}

	public void OnDrag(PointerEventData eventData)
	{
		this.Drag?.Invoke(eventData);
	}

	private static ButtonState ToButtonState(SelectionState state)
	{
		return state switch
		{
			SelectionState.Normal => ButtonState.Normal, 
			SelectionState.Highlighted => ButtonState.Highlighted, 
			SelectionState.Pressed => ButtonState.Pressed, 
			SelectionState.Selected => ButtonState.Selected, 
			SelectionState.Disabled => ButtonState.Disabled, 
			_ => throw new InvalidOperationException(), 
		};
	}
}
