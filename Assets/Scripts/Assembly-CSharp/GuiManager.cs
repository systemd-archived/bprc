using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class GuiManager : Singleton<GuiManager>
{
	public class Pointer
	{
		public bool touching;

		public bool down;

		public bool up;

		public bool dragging;

		public bool secondaryDown;

		public bool secondaryUp;

		public bool secondaryDragging;

		public bool doubleClick;

		public bool onWidget;

		public bool touchUsed;

		public int fingerId;

		public Widget widget;

		public Vector3 position;
	}

	private class FocusData
	{
		public int fingerId = -1;

		public bool primary;

		public Widget target;

		public Widget mouseOver;
		public bool uiCaptured;
	}

	[SerializeField]
	private Texture m_hackTex;

	[SerializeField]
	private AudioSource m_defaultButtonAudio;

	private int guiLayerMask = 1;

	private bool m_enabled = true;
	private bool m_applicationFocused = true;
	private bool m_applicationPaused;
	private int m_inputGeneration;

	private List<int> m_touchIds;

	private List<FocusData> m_focusData;

	private List<Pointer> m_pointers;

	private int m_touchCount;

	private List<object> m_pointerGrabList = new List<object>();

	private static int pointerGrabCount;

	private bool m_doubleClickActive;

	private float m_doubleClickStartTime;

	private Vector3 m_doubleClickPosition;

	private List<HUDLayer> m_layers = new List<HUDLayer>();

	private float m_layerBottomZ;

	private int m_originalResolutionWidth;

	private int m_originalResolutionHeight;

	private int m_originalResolutionWidthDescktop;

	private int m_originalResolutionHeightDescktop;

	private bool m_startedInFullScreen;
	private Camera m_hudCamera;

	public static int PointerCount => Singleton<GuiManager>.instance.m_pointers.Count;

	public static int TouchCount => Singleton<GuiManager>.instance.m_touchCount;

	public AudioSource DefaultButtonAudio
	{
		get
		{
			return m_defaultButtonAudio;
		}
		set
		{
			m_defaultButtonAudio = value;
		}
	}

	public bool IsEnabled
	{
		get
		{
			return m_enabled;
		}
		set
		{
			if (m_enabled == value) return;
			m_enabled = value;
			if (!value) CancelInput();
		}
	}

	public static Pointer GetPointer()
	{
		if (pointerGrabCount > 0 || DevelopmentReflector.BlocksPointer)
		{
			return new Pointer();
		}
		return Singleton<GuiManager>.instance.m_pointers[0];
	}

	public static Pointer GetPointer(int index)
	{
		if (pointerGrabCount > 0 || DevelopmentReflector.BlocksPointer)
		{
			return new Pointer();
		}
		return Singleton<GuiManager>.instance.m_pointers[index];
	}

	public Camera FindCamera()
	{
		if (m_hudCamera != null) return m_hudCamera;
		GameObject gameObject = GameObject.FindGameObjectWithTag("HUDCamera");
		if ((bool)gameObject)
		{
			m_hudCamera = gameObject.GetComponent<Camera>();
			return m_hudCamera;
		}
		return null;
	}

	public void GrabPointer(object obj)
	{
		pointerGrabCount++;
		m_pointerGrabList.Add(obj);
	}

	public void ReleasePointer(object obj)
	{
		pointerGrabCount--;
		m_pointerGrabList.Remove(obj);
	}

	public void ResetFocus()
	{
		m_inputGeneration++;
		if (m_focusData == null) return;
		foreach (FocusData focusDatum in m_focusData)
		{
			CancelFocus(focusDatum, Input.mousePosition);
		}
	}

	private static void CancelFocus(FocusData focus, Vector3 position)
	{
		if (focus == null) return;
		Widget target = focus.target;
		Widget mouseOver = focus.mouseOver;
		// Clear ownership before callbacks, which can disable input or change scenes.
		focus.target = null;
		focus.mouseOver = null;
		focus.uiCaptured = false;
		focus.fingerId = -1;
		focus.primary = false;
		if (target != null) target.SendInput(new InputEvent(InputEvent.EventType.MouseLeave, position));
		if (mouseOver != null && mouseOver != target)
			mouseOver.SendInput(new InputEvent(InputEvent.EventType.MouseLeave, position));
	}

	private static void ClearPointer(Pointer pointer)
	{
		pointer.touching = pointer.down = pointer.up = pointer.dragging = false;
		pointer.secondaryDown = pointer.secondaryUp = pointer.secondaryDragging = false;
		pointer.doubleClick = pointer.onWidget = pointer.touchUsed = false;
		pointer.position = Vector3.zero;
		pointer.fingerId = -1;
		pointer.widget = null;
	}

	private void CancelInput()
	{
		m_touchCount = 0;
		m_doubleClickActive = false;
		if (m_pointers != null)
			foreach (Pointer pointer in m_pointers) ClearPointer(pointer);
		if (m_touchIds != null)
			for (int i = 0; i < m_touchIds.Count; i++) m_touchIds[i] = -1;
		ResetFocus();
	}

	private void CancelTouch(int pointerIndex)
	{
		FocusData focus = FindFocusData(m_touchIds[pointerIndex]);
		Vector3 position = m_pointers[pointerIndex].position;
		bool ended = m_pointers[pointerIndex].up;
		m_touchIds[pointerIndex] = -1;
		ClearPointer(m_pointers[pointerIndex]);
		if (!ended) m_doubleClickActive = false;
		CancelFocus(focus, position);
	}

	private void Awake()
	{
		Object.DontDestroyOnLoad(this);
		Singleton<GuiManager>.instance = this;
		guiLayerMask = 1 << base.gameObject.layer;
		m_touchIds = new List<int>();
		m_focusData = new List<FocusData>();
		m_pointers = new List<Pointer>();
		for (int i = 0; i < 4; i++)
		{
			m_touchIds.Add(-1);
			m_focusData.Add(new FocusData());
			m_pointers.Add(new Pointer());
		}
		m_originalResolutionWidth = Screen.width;
		m_originalResolutionHeight = Screen.height;
		m_startedInFullScreen = Screen.fullScreen;
		if (DeviceInfo.ActiveDeviceFamily == DeviceInfo.DeviceFamily.Pc)
		{
			m_originalResolutionHeightDescktop = Screen.resolutions[Screen.resolutions.Length - 1].height;
			m_originalResolutionWidthDescktop = Screen.resolutions[Screen.resolutions.Length - 1].width;
		}
		else
		{
			m_originalResolutionHeightDescktop = Screen.currentResolution.height;
			m_originalResolutionWidthDescktop = Screen.currentResolution.width;
		}
	}

	private Widget RayCast(Vector2 screenPosition)
	{
		Widget widget = null;
		Camera camera = FindCamera();
		if (camera == null) return null;
		guiLayerMask = 1 << camera.gameObject.layer;
		if (Physics.Raycast(camera.ScreenPointToRay(screenPosition), out var hitInfo, 100f, guiLayerMask))
		{
			widget = hitInfo.collider.gameObject.GetComponent<Widget>();
		}
		if (widget != null && widget.transform.position.z > m_layerBottomZ)
		{
			widget = null;
		}
		return widget;
	}

	private void MouseInput()
	{
		int generation = m_inputGeneration;
		FocusData focusData = m_focusData[0];
		focusData.primary = true;
		PointerInput(0, touching: true, Input.GetMouseButtonDown(0), Input.GetMouseButtonUp(0), Input.GetMouseButton(0), Input.mousePosition, 0, focusData, m_pointers[0]);
		if (generation != m_inputGeneration) return;
		m_pointers[0].secondaryDown = Input.GetMouseButtonDown(1);
		m_pointers[0].secondaryUp = Input.GetMouseButtonUp(1);
		m_pointers[0].secondaryDragging = Input.GetMouseButton(1);
		if (Input.GetMouseButton(0) || Input.GetMouseButtonUp(0))
		{
			m_touchCount = 1;
		}
		else
		{
			m_touchCount = 0;
		}
	}

	private void TouchInput()
	{
		int generation = m_inputGeneration;
		m_touchCount = 0;
		// Android can drop a finger without Ended/Canceled when opening settings or pausing.
		for (int i = 0; i < m_touchIds.Count; i++)
		{
			if (m_touchIds[i] != -1 && FindTouch(i) == -1)
			{
				CancelTouch(i);
				if (generation != m_inputGeneration) return;
			}
		}
		int nextSlot = 0;
		for (int i = 0; i < m_touchIds.Count; i++)
		{
			if (m_touchIds[i] == -1) continue;
			if (i != nextSlot)
			{
				m_touchIds[nextSlot] = m_touchIds[i];
				m_touchIds[i] = -1;
				Pointer spare = m_pointers[nextSlot];
				m_pointers[nextSlot] = m_pointers[i];
				m_pointers[i] = spare;
			}
			nextSlot++;
		}
		for (int i = 0; i < m_touchIds.Count; i++)
		{
			int num = FindTouch(i);
			if (num != -1)
			{
				ProcessTouch(i, Input.GetTouch(num));
				if (generation != m_inputGeneration) return;
				if (m_pointers[i].touching) m_touchCount++;
			}
			else
			{
				ClearPointer(m_pointers[i]);
			}
		}
	}

	private void ProcessTouch(int pointerIndex, Touch touch)
	{
		m_touchIds[pointerIndex] = touch.fingerId;
		if (touch.phase == TouchPhase.Canceled)
		{
			m_doubleClickActive = false;
			CancelTouch(pointerIndex);
			return;
		}
		bool ended = touch.phase == TouchPhase.Ended;
		FocusData focus = GetFocusData(touch.fingerId);
		if (focus != null) focus.primary = pointerIndex == 0;
		PointerInput(pointerIndex, touching: true, touch.phase == TouchPhase.Began, ended, !ended,
			touch.position, touch.fingerId, focus, m_pointers[pointerIndex]);
	}

	private FocusData FindFocusData(int fingerId)
	{
		if (fingerId == -1) return null;
		for (int i = 0; i < m_focusData.Count; i++)
			if (m_focusData[i].fingerId == fingerId) return m_focusData[i];
		return null;
	}

	private FocusData GetFocusData(int fingerId)
	{
		FocusData existing = FindFocusData(fingerId);
		if (existing != null) return existing;
		for (int j = 0; j < m_focusData.Count; j++)
		{
			if (m_focusData[j].fingerId == -1)
			{
				m_focusData[j].fingerId = fingerId;
				return m_focusData[j];
			}
		}
		return null;
	}

	private int FindTouch(int touchIdIndex)
	{
		int result = -1;
		for (int i = 0; i < Input.touchCount; i++)
		{
			Touch touch = Input.GetTouch(i);
			if (m_touchIds[touchIdIndex] != -1 && m_touchIds[touchIdIndex] != touch.fingerId)
			{
				continue;
			}
			bool flag = false;
			for (int j = 0; j < m_touchIds.Count; j++)
			{
				if (j != touchIdIndex && touch.fingerId == m_touchIds[j])
				{
					flag = true;
					break;
				}
			}
			if (!flag && (m_touchIds[touchIdIndex] != -1 || touch.phase == TouchPhase.Began))
			{
				result = i;
				break;
			}
		}
		return result;
	}

	private void Update()
	{
		if (!m_enabled || !m_applicationFocused || m_applicationPaused)
		{
			return;
		}
		int generation = m_inputGeneration;
		if (DeviceInfo.UsesTouchInput)
		{
			TouchInput();
		}
		else
		{
			MouseInput();
		}
		if (generation != m_inputGeneration) return;
		HandleDoubleClick();
		if (DeviceInfo.ActiveDeviceFamily == DeviceInfo.DeviceFamily.Pc)
		{
			if (!Screen.fullScreen && (Screen.height != m_originalResolutionHeight || Screen.width != m_originalResolutionWidth))
			{
				Screen.SetResolution(m_originalResolutionWidth, m_originalResolutionHeight, fullscreen: false);
			}
			if (Screen.fullScreen && (Screen.height != m_originalResolutionHeightDescktop || Screen.width != m_originalResolutionWidthDescktop))
			{
				Screen.SetResolution(m_originalResolutionWidthDescktop, m_originalResolutionHeightDescktop, fullscreen: true);
			}
		}
	}

	private void HandleDoubleClick()
	{
		if (m_touchCount == 1)
		{
			Pointer pointer = m_pointers[0];
			if (pointer.secondaryDragging)
			{
				m_doubleClickActive = false;
			}
			else if (!m_doubleClickActive)
			{
				if (pointer.up)
				{
					m_doubleClickActive = true;
					m_doubleClickStartTime = Time.time;
					m_doubleClickPosition = pointer.position;
				}
			}
			else if (pointer.up)
			{
				if (Time.time - m_doubleClickStartTime < 0.4f && (pointer.position - m_doubleClickPosition).sqrMagnitude < 10000f)
				{
					m_doubleClickActive = false;
					pointer.doubleClick = true;
				}
				else
				{
					m_doubleClickActive = true;
					m_doubleClickStartTime = Time.time;
					m_doubleClickPosition = pointer.position;
				}
			}
		}
		else if (m_touchCount > 1)
		{
			m_doubleClickActive = false;
		}
	}

	private void PointerInput(int pointerIndex, bool touching, bool pointerDown, bool pointerUp, bool dragging, Vector3 position, int fingerId, FocusData focus, Pointer pointer)
	{
		int generation = m_inputGeneration;
		// TouchInput passes no focus for unused touch slots, including every slot on idle frames.
		if (!touching)
		{
			ClearPointer(pointer);
			pointer.position = position;
			pointer.fingerId = fingerId;
			return;
		}
		EventSystem current = EventSystem.current;
		bool overUi = INPartParameterPanel.ContainsPointer(position) ||
			(current != null && current.IsPointerOverUIObject(position));
		if (pointerDown && focus != null) focus.uiCaptured = overUi;
		bool blocked = overUi || (focus != null && focus.uiCaptured);
		Widget widget = blocked ? null : RayCast(position);
		if (blocked && focus != null && focus.target != null)
		{
			focus.target.SendInput(new InputEvent(InputEvent.EventType.MouseLeave, position));
			if (generation != m_inputGeneration) return;
			focus.target = null;
		}
		pointer.touching = touching;
		pointer.down = pointerDown;
		pointer.up = pointerUp;
		pointer.dragging = dragging;
		pointer.position = position;
		pointer.fingerId = fingerId;
		pointer.onWidget = blocked || widget != null;
		pointer.widget = widget;
		pointer.doubleClick = false;
		if ((widget != null || blocked) && pointerDown)
		{
			pointer.touchUsed = true;
		}
		else if (pointerUp || !touching)
		{
			pointer.touchUsed = false;
		}
		if (focus == null) return;
		if (widget != null && focus != null && !focus.primary && !widget.AllowMultitouch())
		{
			widget = null;
		}
		if (pointerDown)
		{
			if ((bool)widget)
			{
				focus.target = widget;
				focus.target.SendInput(new InputEvent(InputEvent.EventType.Press, position));
				if (generation != m_inputGeneration) return;
			}
			else
			{
				focus.target = null;
			}
		}
		if (pointerUp)
		{
			if ((bool)widget && widget == focus.target)
			{
				widget.SendInput(new InputEvent(InputEvent.EventType.Release, position));
				if (generation != m_inputGeneration) return;
			}
			focus.target = null;
		}
		if (touching)
		{
			if (widget != null && focus.mouseOver != widget && (focus.target == null || focus.target == widget))
			{
				if (focus.mouseOver != null)
				{
					focus.mouseOver.SendInput(new InputEvent(InputEvent.EventType.MouseLeave, position));
					if (generation != m_inputGeneration) return;
				}
				focus.mouseOver = widget;
				widget.SendInput(new InputEvent(InputEvent.EventType.MouseEnter, position));
				if (generation != m_inputGeneration) return;
				if (widget == focus.target && !pointerDown)
				{
					widget.SendInput(new InputEvent(InputEvent.EventType.MouseReturn, position));
					if (generation != m_inputGeneration) return;
				}
			}
			if (focus.mouseOver != null && widget != focus.mouseOver)
			{
				focus.mouseOver.SendInput(new InputEvent(InputEvent.EventType.MouseLeave, position));
				if (generation != m_inputGeneration) return;
				focus.mouseOver = null;
			}
		}
		if (pointerUp)
		{
			if (focus.mouseOver != null)
			{
				focus.mouseOver.SendInput(new InputEvent(InputEvent.EventType.MouseLeave, position));
				if (generation != m_inputGeneration) return;
			}
			focus.mouseOver = null;
			focus.target = null;
			focus.fingerId = -1;
			focus.primary = false;
			focus.uiCaptured = false;
		}
		if (focus != null && focus.target != null)
		{
			focus.target.SendInput(new InputEvent(InputEvent.EventType.Drag, position));
		}
	}

	private void OnEnable()
	{
		KeyListener.keyPressed += HandleKeyListenerkeyPressed;
		Object.FindObjectsOfType<HUDLayer>();
	}

	private void OnDisable()
	{
		KeyListener.keyPressed -= HandleKeyListenerkeyPressed;
		CancelInput();
	}

	public void AddLayer(HUDLayer layer)
	{
		float topLayerZ = GetTopLayerZ();
		Vector3 position = layer.transform.position;
		position.z = topLayerZ;
		layer.transform.position = position;
		m_layerBottomZ = position.z + 1f;
		m_layers.Add(layer);
		UpdateCameraZ();
	}

	public void RemoveLayer(HUDLayer layer)
	{
		m_layers.Remove(layer);
		if (m_layers.Count > 0)
		{
			m_layerBottomZ = m_layers[m_layers.Count - 1].transform.position.z + 1f;
		}
		UpdateCameraZ();
	}

	private float GetTopLayerZ()
	{
		if (m_layers.Count > 0)
		{
			HUDLayer hUDLayer = m_layers[m_layers.Count - 1];
			return hUDLayer.transform.position.z - hUDLayer.GetDepth() - 2f;
		}
		return 0f;
	}

	private void UpdateCameraZ()
	{
		float topLayerZ = GetTopLayerZ();
		Camera camera = FindCamera();
		if ((bool)camera)
		{
			Vector3 position = camera.transform.position;
			position.z = topLayerZ;
			camera.transform.position = position;
		}
	}

	private void HandleKeyListenerkeyPressed(KeyCode obj)
	{
		if (!INSettings.GetBool(INFeature.InputSettings) && DeviceInfo.IsDesktop && obj == KeyCode.F)
		{
			SetFullscreen();
		}
	}

	public void SetFullscreen()
	{
		if (INSettings.GetBool(INFeature.InputSettings))
		{
			return;
		}
		if (!Screen.fullScreen)
		{
			if (m_startedInFullScreen)
			{
				Screen.SetResolution(m_originalResolutionWidth, m_originalResolutionHeight, fullscreen: true);
			}
			else
			{
				Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, fullscreen: true);
			}
		}
		else
		{
			Screen.SetResolution(m_originalResolutionWidth, m_originalResolutionHeight, fullscreen: false);
		}
	}

	private void OnApplicationFocus(bool focus)
	{
		m_applicationFocused = focus;
		if (!focus) CancelInput();
		if (Application.platform == RuntimePlatform.OSXPlayer && focus && Screen.fullScreen && (Screen.currentResolution.width != Screen.width || Screen.currentResolution.height != Screen.height))
		{
			Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, fullscreen: true);
		}
	}

	private void OnApplicationPause(bool paused)
	{
		m_applicationPaused = paused;
		if (paused) CancelInput();
	}
}
