using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The RC1 driving controls, ported from CarController's forward, back, turbo and jet actions.
/// </summary>
/// <remarks>
/// <para>
/// RoverCraft puts a gas pedal at the lower right and a brake pedal at the lower left, with the
/// throttle button above the pedals on the left; the art here is the game's own
/// <c>btn_gas</c> / <c>btn_brake</c> / <c>btn_turbo</c> sprites cut out of its UI atlas
/// (Tools/RC1/rasterise_tk2d.py), and the layout keeps those three corners.
/// </para>
/// <para>
/// The whole set only exists while <see cref="INFeature.Rc1Controls"/> is on - switching it off leaves
/// the running screen exactly as the stock game draws it - and it is hidden whenever a contraption is
/// not actually running, so it never covers the build screen.
/// </para>
/// <para>
/// Everything else reads the three hold states through the static properties, which are 0 whenever the
/// panel is not up: the gyro (<see cref="RCPort.RoverPortGyro"/>), the wheel and thruster drive
/// (<see cref="RCR2Port.RoverPortPoweredPart"/>) and the turbo/jet parts.
/// </para>
/// </remarks>
public class INRc1Controls : MonoBehaviour
{
    public static INRc1Controls Instance { get; private set; }

    /// <summary>Whether 通用设置 has 「RC1 操作」 switched on.</summary>
    public static bool Enabled
    {
        get
        {
            try
            {
                return INSettings.GetBool(INFeature.Rc1Controls);
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>The panel is up and a contraption is running, so the pedals mean something.</summary>
    public static bool Active => Instance != null && Instance.m_shown;

    /// <summary>+1 while the gas pedal is held, -1 while the brake pedal is held, 0 for neither.</summary>
    public static float DriveAxis
    {
        get
        {
            if (!Active) return 0f;
            return (Instance.m_forward || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)
                - (Instance.m_back || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
        }
    }

    /// <summary>1 while the throttle is held, 0 otherwise.</summary>
    public static float Throttle => Active && (Instance.m_turbo || Input.GetKey(KeyCode.LeftShift)) ? 1f : 0f;
    public static float Jet => Active && (Instance.m_jet || Input.GetKey(KeyCode.Space)) ? 1f : 0f;

    /// <summary>
    /// Whether the throttle button is held. Thrusters and turbines are switched by it the way
    /// RoverCraft's BtnTurbo switches its turbo and jet parts.
    /// </summary>
    public static bool ThrottleHeld => Throttle > 0f;

    public static float NativeDriveSign(BasePart part, float fallback)
    {
        if (!Active || Mathf.Abs(DriveAxis) < 0.001f) return fallback;
        Gearbox gearbox = part.contraption == null ? null : part.contraption.GetGearbox(part);
        // Native wheels apply the gearbox sign again in their drive step.
        return Mathf.Sign(DriveAxis) * fallback * (gearbox != null && gearbox.IsEnabled() ? -1f : 1f);
    }

    // RoverCraft's own button sizes, in reference-resolution pixels.
    private static readonly Vector2 GasSize = new Vector2(84f, 179f);
    private static readonly Vector2 BrakeSize = new Vector2(125f, 102f);
    private static readonly Vector2 TurboSize = new Vector2(194f, 198f);

    private Canvas m_canvas;
    private RectTransform m_panel;
    private bool m_shown;
    private bool m_forward;
    private bool m_back;
    private bool m_turbo;
    private bool m_jet;
    private bool m_nativeCommanded;
    private Contraption m_nativeOwner;
    private readonly Dictionary<BasePart, bool> m_manualStates = new Dictionary<BasePart, bool>();
    private int m_pressedCount;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        BuildPanel();
    }

    /// <summary>
    /// Builds the canvas and the driving controls.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="Awake"/> because <c>DontDestroyOnLoad</c> throws outside play mode, and
    /// the audit that checks this panel runs in the editor (see Rc1ControlsAudit).
    /// </remarks>
    internal void BuildPanel()
    {
        m_canvas = gameObject.AddComponent<Canvas>();
        m_canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Above the HUD but below the auxiliary menu, which sits at 100.
        m_canvas.sortingOrder = 90;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();
        INUiScale.Register(m_canvas);

        m_panel = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
        m_panel.SetParent(transform, false);
        m_panel.anchorMin = Vector2.zero;
        m_panel.anchorMax = Vector2.one;
        m_panel.offsetMin = Vector2.zero;
        m_panel.offsetMax = Vector2.zero;

        // Gas pedal, lower right - where RoverCraft's BtnForward (forwardMotion) sits.
        CreateHoldButton("Forward", "RC1Controls/btn_gas", GasSize,
            new Vector2(1f, 0f), new Vector2(-140f, 150f), value => m_forward = value);
        // Brake pedal, lower left - BtnBack ("back"), which the tutorial also labels the brake.
        CreateHoldButton("Back", "RC1Controls/btn_brake", BrakeSize,
            new Vector2(0f, 0f), new Vector2(140f, 150f), value => m_back = value);
        // Throttle above the pedals, on the left - BtnTurbo.
        CreateHoldButton("Throttle", "RC1Controls/btn_turbo", TurboSize,
            new Vector2(0f, 0.5f), new Vector2(150f, 40f), value => m_turbo = value);
        CreateHoldButton("Jet", "RC1Controls/btn_start", new Vector2(112f, 112f),
            new Vector2(0f, 0.5f), new Vector2(150f, -145f), value => m_jet = value);

        // Start hidden: m_shown is already false, so SetShown(false) would be a no-op and the panel
        // would sit over the build screen until the first frame decided otherwise.
        m_panel.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        RestoreNativeStates();
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        bool running = IsDriving();
        SetShown(Enabled && running);
        if (!m_shown)
        {
            // Release everything the moment the panel goes away, or a held pedal would keep driving.
            m_forward = m_back = m_turbo = m_jet = false;
        }
        if (m_shown || m_nativeCommanded) SyncNativeDrive();
    }

    private void SyncNativeDrive()
    {
        var owner = WPFMonoBehaviour.levelManager == null ? null : WPFMonoBehaviour.levelManager.ContraptionRunning;
        if (owner != m_nativeOwner) { RestoreNativeStates(); m_nativeOwner = owner; }
        if (owner == null || !owner.IsRunning) { RestoreNativeStates(); return; }
        bool drive = m_shown && Mathf.Abs(DriveAxis) > 0.001f;
        bool turbo = m_shown && ThrottleHeld;
        foreach (BasePart part in owner.Parts)
        {
            if (part == null || part is RCR2Port.RoverPortPart || !part.CanBeEnabled()) continue;
            bool command;
            if (part is Engine) command = drive || turbo || Jet > 0f;
            else if (part is MotorWheel || part is OffRoadWheel) command = drive;
            else if (part is JetEngine || part is Fan || part is FanPropeller) command = turbo || Jet > 0f;
            else continue;
            if (command)
            {
                if (!m_manualStates.ContainsKey(part)) m_manualStates.Add(part, part.IsEnabled());
                if (!part.IsEnabled()) part.SetEnabled(true);
            }
            else if (m_manualStates.TryGetValue(part, out bool manual))
            {
                m_manualStates.Remove(part);
                if (part.IsEnabled() != manual) part.SetEnabled(manual);
            }
        }
        m_nativeCommanded = m_manualStates.Count > 0;
    }

    private void RestoreNativeStates()
    {
        foreach (var entry in m_manualStates)
            if (entry.Key != null) entry.Key.SetEnabled(entry.Value);
        m_manualStates.Clear();
        m_nativeCommanded = false;
    }

    // A native toggle operates on the player's saved state, even while a pedal is held.
    public static void PrepareNativeTrigger(BasePart part)
    {
        if (part is RCR2Port.RoverPortPoweredPart powered) powered.PrepareManualTrigger();
        if (Instance == null || part == null || !Instance.m_manualStates.TryGetValue(part, out bool manual)) return;
        Instance.m_manualStates.Remove(part);
        part.SetEnabled(manual);
    }

    public static void PrepareNativeTriggerButton()
    {
        if (!Active) return;
        Instance.RestoreNativeStates();
        var owner = WPFMonoBehaviour.levelManager?.ContraptionRunning;
        if (owner == null) return;
        // Engine buttons also toggle powered neighbours outside their own button's Parts list.
        foreach (BasePart part in owner.Parts)
            if (part is RCR2Port.RoverPortPoweredPart powered) powered.PrepareManualTrigger();
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused && m_panel != null)
        {
            SetShown(false);
            m_forward = m_back = m_turbo = m_jet = false;
            SyncNativeDrive();
        }
    }

    /// <summary>A contraption is in flight, which is the only time the pedals do anything.</summary>
    private static bool IsDriving()
    {
        LevelManager manager = WPFMonoBehaviour.levelManager;
        if (manager == null) return false;
        Contraption running = manager.ContraptionRunning;
        return running != null && running.IsRunning && Time.timeScale > 0f;
    }

    private void SetShown(bool shown)
    {
        if (m_shown == shown) return;
        m_shown = shown;
        m_panel.gameObject.SetActive(shown);
    }

    /// <summary>
    /// One pedal or the throttle: an image with the game's own sprite, held down rather than clicked -
    /// the source buttons are tk2dUIItems whose Down/Up feeds the part actions, so the state has to
    /// track the press and the release, including a pointer that slides off the button.
    /// </summary>
    private void CreateHoldButton(string name, string spritePath, Vector2 size,
                                  Vector2 anchor, Vector2 offset, Action<bool> changed)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(m_panel, false);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.sizeDelta = size;
        rect.anchoredPosition = offset;

        Image image = go.GetComponent<Image>();
        // The decompiled game declares its own global Sprite, so UnityEngine's has to be spelled out.
        UnityEngine.Sprite sprite = Resources.Load<UnityEngine.Sprite>(spritePath);
        if (sprite != null)
        {
            image.sprite = sprite;
            image.preserveAspect = true;
        }
        else
        {
            // Never fall back to an invisible control: a flat sprite keeps the button usable while
            // showing that its art did not load.
            image.color = new Color(0.85f, 0.85f, 0.85f, 0.75f);
            Debug.LogWarning("[INRc1Controls] missing sprite " + spritePath + "; using a flat button");
        }
        image.raycastTarget = true;

        HoldButton hold = go.AddComponent<HoldButton>();
        hold.Changed = changed;
        // Pressing a pedal must not be read as a drag of the camera or the build grid.
        hold.Pressed = value =>
        {
            m_pressedCount = Mathf.Max(0, m_pressedCount + (value ? 1 : -1));
            if (INSelectionManager.Instance != null) INSelectionManager.PauseBoxDrawing = m_pressedCount > 0;
        };
    }

    /// <summary>Press/release tracking for one control.</summary>
    private sealed class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public Action<bool> Changed;
        public Action<bool> Pressed;

        private bool m_down;
        private int m_pointer = int.MinValue;
        private bool m_polled;
        private Vector3 m_restScale;
        private Color m_restColor;

        private void Awake()
        {
            m_restScale = transform.localScale;
            m_restColor = GetComponent<Image>().color;
        }

        // The game also uses legacy tk2d input; polling keeps pedals usable when no uGUI
        // input module is active. A press starts inside the control and owns one pointer.
        private void Update()
        {
            RectTransform rect = (RectTransform)transform;
            if (!m_down && Input.GetMouseButtonDown(0) &&
                RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition))
            {
                m_polled = true;
                Press(-1);
            }
            if (m_down && m_pointer == -1 && (Input.GetMouseButtonUp(0) ||
                (m_polled && !RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition)))) Release();
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (!m_down && touch.phase == TouchPhase.Began &&
                    RectTransformUtility.RectangleContainsScreenPoint(rect, touch.position)) Press(touch.fingerId);
                if (m_down && touch.fingerId == m_pointer && (touch.phase == TouchPhase.Ended ||
                    touch.phase == TouchPhase.Canceled ||
                    !RectTransformUtility.RectangleContainsScreenPoint(rect, touch.position))) Release();
            }
        }

        private void Press(int pointer)
        {
            if (m_down) return;
            m_pointer = pointer;
            m_down = true;
            transform.localScale = m_restScale * 1.12f;
            GetComponent<Image>().color = new Color(0.7f, 0.7f, 0.7f, 1f);
            Pressed?.Invoke(true);
            Changed?.Invoke(true);
        }

        private void Release()
        {
            if (!m_down) return;
            m_pointer = int.MinValue;
            m_polled = false;
            m_down = false;
            transform.localScale = m_restScale;
            GetComponent<Image>().color = m_restColor;
            Pressed?.Invoke(false);
            Changed?.Invoke(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            Press(eventData.pointerId);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == m_pointer) Release();
        }

        /// <summary>A finger that slides off the pedal lets go of it, as tk2dUIItem does.</summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            OnPointerUp(eventData);
        }

        private void OnDisable()
        {
            Release();
        }
    }
}
