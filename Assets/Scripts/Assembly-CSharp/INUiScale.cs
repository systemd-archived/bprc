using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Resolution-aware UI size, captured once per root canvas without per-frame discovery.</summary>
[DisallowMultipleComponent]
public sealed class INUiScale : MonoBehaviour
{
    private Canvas m_canvas;
    private CanvasScaler m_scaler;
    private Vector2 m_reference;
    private float m_pixelScale;
    private float m_applied = -1f;

    public static float Multiplier => INUserSettings.Instance?.GeneralSettings?.UiScale ?? 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Refresh();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Refresh();

    // Used on settings changes and scene loads only. Dynamic UI factories register directly.
    public static void Refresh()
    {
        foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Register(canvas);
    }

    public static void Register(Canvas canvas)
    {
        if (canvas == null || !canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace) return;
        // Blueprint bounds are projected in screen pixels; the editor scales only its handle sizes.
        if (canvas.GetComponentInChildren<INBuildOverlayGraphic>(true) != null) return;
        INUiScale scale = canvas.GetComponent<INUiScale>();
        if (scale == null) scale = canvas.gameObject.AddComponent<INUiScale>();
        scale.Capture();
        scale.Apply();
    }

    private void Capture()
    {
        if (m_canvas != null) return;
        m_canvas = GetComponent<Canvas>();
        m_scaler = GetComponent<CanvasScaler>();
        if (m_scaler == null)
        {
            m_scaler = gameObject.AddComponent<CanvasScaler>();
            m_scaler.scaleFactor = m_canvas.scaleFactor;
        }
        m_reference = m_scaler.referenceResolution;
        m_pixelScale = m_scaler.scaleFactor;
    }

    private void LateUpdate() => Apply();

    private void OnEnable() => Canvas.preWillRenderCanvases += ApplyPhysicalSize;
    private void OnDisable() => Canvas.preWillRenderCanvases -= ApplyPhysicalSize;

    private void ApplyPhysicalSize()
    {
        if (m_canvas == null || m_scaler == null || m_canvas.renderMode == RenderMode.WorldSpace ||
            m_scaler.uiScaleMode != CanvasScaler.ScaleMode.ConstantPhysicalSize) return;
        float dpi = Screen.dpi > 0f ? Screen.dpi : m_scaler.fallbackScreenDPI;
        float units = m_scaler.physicalUnit == CanvasScaler.Unit.Centimeters ? 2.54f :
            m_scaler.physicalUnit == CanvasScaler.Unit.Millimeters ? 25.4f :
            m_scaler.physicalUnit == CanvasScaler.Unit.Points ? 72f :
            m_scaler.physicalUnit == CanvasScaler.Unit.Picas ? 6f : 1f;
        m_canvas.scaleFactor = dpi / units * Multiplier;
    }

    private void Apply()
    {
        if (m_canvas == null || m_scaler == null || m_canvas.renderMode == RenderMode.WorldSpace) return;
        float scale = Multiplier;
        if (Mathf.Approximately(scale, m_applied)) return;
        m_applied = scale;
        if (m_scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
            m_scaler.referenceResolution = m_reference / scale;
        else if (m_scaler.uiScaleMode == CanvasScaler.ScaleMode.ConstantPixelSize)
            m_scaler.scaleFactor = m_pixelScale * scale;
        // The installed uGUI scaler handles preWillRenderCanvases, including resolution changes.
        // Flush it for live settings changes; inactive canvases pick up their settings on enable.
        if (m_canvas.isActiveAndEnabled) Canvas.ForceUpdateCanvases();
        ApplyPhysicalSize();
    }
}
