using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace BadPiggies.Multiplayer
{
public class MultiplayerReadyCheckmark : MonoBehaviour
{
	public Action Pressed;

	private GameObject m_button;

	private MethodCaller m_methodCaller;

	private GameObject m_savedTargetObject;

	private string m_savedTargetComponent;

	private string m_savedMethod;

	private EventSender m_eventSender;

	private string m_savedEventName;

	private PullButton m_pullButton;

	private FieldInfo m_pullCallObjectField;

	private FieldInfo m_pullMethodField;

	private GameObject m_savedPullCallObject;

	private string m_savedPullMethod;

	private bool m_bound;

	private GameObject m_cancelArt;

	private readonly List<Renderer> m_hiddenArt = new List<Renderer>();

	private bool m_cancelArtSearched;

	private GameObject m_cancelDonor;

	public bool IsBound => m_bound;

	public bool IsShowingCancel => m_cancelArt != null;

	public bool Bind(GameObject button)
	{
		if (m_bound)
		{
			return true;
		}
		if (button == null)
		{
			return false;
		}
		m_button = button;
		if (!button.activeSelf)
		{
			button.SetActive(value: true);
		}
		if (TryBindButton(button) || TryBindAnimatedButton(button) || TryBindPullButton(button))
		{
			m_bound = true;
			return true;
		}
		Debug.LogWarning("[Multiplayer] The build-menu checkmark carries no Button/AnimatedButton/PullButton; cannot use it as the Ready control.");
		m_button = null;
		return false;
	}

	private bool TryBindButton(GameObject button)
	{
		Button component = button.GetComponent<Button>();
		if (component == null)
		{
			return false;
		}
		CaptureMethodCaller(component.MethodToCall);
		CaptureEventSender(component.EventToSend);
		Debug.Log("[Multiplayer] Ready bound to the build-menu checkmark (Button).");
		return true;
	}

	private bool TryBindAnimatedButton(GameObject button)
	{
		AnimatedButton component = button.GetComponent<AnimatedButton>();
		if (component == null)
		{
			return false;
		}
		CaptureMethodCaller(component.MethodToCall);
		CaptureEventSender(component.EventSender);
		Debug.Log("[Multiplayer] Ready bound to the build-menu checkmark (AnimatedButton).");
		return true;
	}

	private bool TryBindPullButton(GameObject button)
	{
		PullButton component = button.GetComponent<PullButton>();
		if (component == null)
		{
			return false;
		}
		m_pullButton = component;
		m_pullCallObjectField = typeof(PullButton).GetField("callObject", BindingFlags.Instance | BindingFlags.NonPublic);
		m_pullMethodField = typeof(PullButton).GetField("methodToCall", BindingFlags.Instance | BindingFlags.NonPublic);
		if (m_pullCallObjectField == null || m_pullMethodField == null || m_pullCallObjectField.FieldType != typeof(GameObject) || m_pullMethodField.FieldType != typeof(string))
		{
			component.LockDragging(lockDragging: true);
			Debug.LogWarning("[Multiplayer] PullButton's call fields could not be reflected; the lever has been locked so it cannot start a single-player match.");
			return true;
		}
		m_savedPullCallObject = m_pullCallObjectField.GetValue(component) as GameObject;
		m_savedPullMethod = m_pullMethodField.GetValue(component) as string;
		m_pullCallObjectField.SetValue(component, gameObject);
		m_pullMethodField.SetValue(component, "OnMultiplayerReadyPressed");
		Debug.Log("[Multiplayer] Ready bound to the build-menu checkmark (PullButton).");
		return true;
	}

	private void CaptureMethodCaller(MethodCaller caller)
	{
		if (caller != null)
		{
			m_methodCaller = caller;
			m_savedTargetObject = caller.TargetObject;
			m_savedTargetComponent = caller.TargetComponent;
			m_savedMethod = caller.MethodToInvoke;
			caller.SetMethod(this, "OnMultiplayerReadyPressed");
		}
	}

	private void CaptureEventSender(EventSender sender)
	{
		if (sender != null && sender.HasEvent())
		{
			m_eventSender = sender;
			m_savedEventName = sender.EventName;
			sender.SetEvent("None", null);
		}
	}

	public void Restore()
	{
		ShowCancelArt(cancel: false);
		m_cancelArtSearched = false;
		m_cancelDonor = null;
		if (m_methodCaller != null)
		{
			if (m_savedTargetObject != null && !string.IsNullOrEmpty(m_savedTargetComponent) && !string.IsNullOrEmpty(m_savedMethod))
			{
				Component component = m_savedTargetObject.GetComponent(m_savedTargetComponent);
				if (component != null)
				{
					m_methodCaller.SetMethod(component, m_savedMethod);
				}
				else
				{
					m_methodCaller.Reset();
				}
			}
			else
			{
				m_methodCaller.Reset();
			}
		}
		if (m_eventSender != null && !string.IsNullOrEmpty(m_savedEventName))
		{
			m_eventSender.SetEvent(m_savedEventName, null);
			m_eventSender.PrepareSend();
		}
		if (m_pullButton != null)
		{
			if (m_pullCallObjectField != null && m_pullMethodField != null)
			{
				m_pullCallObjectField.SetValue(m_pullButton, m_savedPullCallObject);
				m_pullMethodField.SetValue(m_pullButton, m_savedPullMethod);
			}
			m_pullButton.LockDragging(lockDragging: false);
		}
		m_methodCaller = null;
		m_savedTargetObject = null;
		m_savedTargetComponent = null;
		m_savedMethod = null;
		m_eventSender = null;
		m_savedEventName = null;
		m_pullButton = null;
		m_pullCallObjectField = null;
		m_pullMethodField = null;
		m_savedPullCallObject = null;
		m_savedPullMethod = null;
		m_button = null;
		m_bound = false;
	}

	private void OnDisable()
	{
		Restore();
	}

	public void OnMultiplayerReadyPressed()
	{
		Pressed?.Invoke();
	}

	public void SetInteractable(bool interactable)
	{
		if (!(m_button == null))
		{
			Button component = m_button.GetComponent<Button>();
			if (component != null)
			{
				component.Lock(!interactable);
			}
			if (m_pullButton != null)
			{
				m_pullButton.LockDragging(!interactable);
			}
		}
	}

	public void ShowCancelArt(bool cancel)
	{
		if (m_button == null || cancel == (m_cancelArt != null))
		{
			return;
		}
		if (!cancel)
		{
			if (m_cancelArt != null)
			{
				UnityEngine.Object.Destroy(m_cancelArt);
				m_cancelArt = null;
			}
			for (int i = 0; i < m_hiddenArt.Count; i++)
			{
				if (m_hiddenArt[i] != null)
				{
					m_hiddenArt[i].enabled = true;
				}
			}
			m_hiddenArt.Clear();
			return;
		}
		GameObject gameObject = FindCancelDonor();
		if (gameObject == null)
		{
			return;
		}
		m_cancelArt = UnityEngine.Object.Instantiate(gameObject, m_button.transform);
		m_cancelArt.name = "MultiplayerCancelArt";
		m_cancelArt.transform.localPosition = Vector3.zero;
		m_cancelArt.transform.localRotation = Quaternion.identity;
		m_cancelArt.transform.localPosition = new Vector3(0f, 0f, -0.05f);
		Component[] componentsInChildren = m_cancelArt.GetComponentsInChildren<Component>(includeInactive: true);
		foreach (Component component in componentsInChildren)
		{
			if (!(component == null) && !(component is Transform) && !(component is Renderer) && !(component is MeshFilter))
			{
				UnityEngine.Object.Destroy(component);
			}
		}
		m_hiddenArt.Clear();
		Renderer[] componentsInChildren2 = m_button.GetComponentsInChildren<Renderer>(includeInactive: false);
		foreach (Renderer renderer in componentsInChildren2)
		{
			if (!(renderer == null) && !renderer.transform.IsChildOf(m_cancelArt.transform))
			{
				renderer.enabled = false;
				m_hiddenArt.Add(renderer);
			}
		}
	}

	private GameObject FindCancelDonor()
	{
		if (m_cancelArtSearched)
		{
			return m_cancelDonor;
		}
		m_cancelArtSearched = true;
		LevelManager levelManager = WPFMonoBehaviour.levelManager;
		InGameGUI inGameGUI = ((levelManager != null) ? levelManager.InGameGUI : null);
		PausePage pausePage = ((inGameGUI != null) ? inGameGUI.PauseMenu : null);
		if (pausePage == null)
		{
			return null;
		}
		string[] array = new string[4] { "close", "cancel", "back", "resume" };
		Button[] componentsInChildren = pausePage.GetComponentsInChildren<Button>(includeInactive: true);
		for (int i = 0; i < array.Length; i++)
		{
			for (int j = 0; j < componentsInChildren.Length; j++)
			{
				if (!(componentsInChildren[j] == null) && componentsInChildren[j].name.ToLowerInvariant().Contains(array[i]) && componentsInChildren[j].GetComponentInChildren<Renderer>() != null)
				{
					m_cancelDonor = componentsInChildren[j].gameObject;
					return m_cancelDonor;
				}
			}
		}
		Debug.Log("[Multiplayer] No close button found in the pause menu to borrow a cancel icon from; the Ready control keeps the checkmark art while submitted.");
		return null;
	}
}
}
