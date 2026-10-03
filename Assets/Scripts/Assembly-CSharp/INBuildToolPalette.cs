using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UButton = UnityEngine.UI.Button;
using UImage = UnityEngine.UI.Image;
using UText = UnityEngine.UI.Text;

/// <summary>
/// Besiege-style build tool set.
/// </summary>
/// <remarks>
/// <para>
/// The reference's advanced building exposes an exclusive tool per job - Translate, Rotate, Mirror,
/// Modify, Paint (<c>AdvancedBlockEditor</c>, with hotkeys out of <c>InputManager.AdvancedBuilding</c>) -
/// where the tool decides what a click on a block does. This project already had every one of those
/// <i>capabilities</i>, scattered: rotation on a tap (<c>Contraption.Flip</c>), flipping on
/// <c>BasePart.m_flipped</c>, the parameter panel on a long press (<c>INPartParameterPanel</c>),
/// translation on the four move buttons. What it did not have was the <b>tool</b> - a way to say "the next
/// click rotates" instead of "the next click does whatever a tap happens to mean". That is what this adds.
/// </para>
/// <para>
/// <b>Paint is deliberately absent.</b> In this project a part's colour is either a per-part-type field
/// (<c>m_tint</c>, reachable only through the parameter panel) or a selection-wide material override
/// (<c>INSelectionManager.CycleMaterialIndex</c>). Neither is an action on one clicked part, so a "paint
/// this part" button would have nothing to call - and a button that does nothing is worse than no button.
/// Colouring is still reachable: the Modify tool opens the panel that holds those fields.
/// </para>
/// <para>
/// The tool is <b>off by default</b> and every tool falls back to the stock behaviour when no tool is
/// selected, so a player who never opens the palette sees the build screen exactly as before.
/// </para>
/// </remarks>
public enum BuildTool
{
    /// <summary>No tool: a tap on a part does what it always did.</summary>
    None = 0,

    /// <summary>Click a part to turn it - the fine step when 部件精细旋转 is on, the stock step otherwise.</summary>
    Rotate = 1,

    /// <summary>Click a part to mirror it.</summary>
    Mirror = 2,

    /// <summary>Click a part to open its parameter panel.</summary>
    Modify = 3,

    /// <summary>Drag anywhere to shift the whole contraption.</summary>
    Translate = 4,
    RotateMachine = 5,
    SelectMachine = 6,
    Gridless = 7,
    Undo = 8,
    TranslatePart = 9,
    RotatePart = 10,
    MergeGridless = 11,
    ExtractSelection = 12,
}

/// <summary>
/// Which <see cref="BuildTool"/> is active. Static because the tool outlives any one build screen and both
/// the palette and the input code have to agree on it.
/// </summary>
public static class BuildToolPalette
{
    /// <summary>The active tool. <see cref="BuildTool.None"/> means the build screen behaves as stock.</summary>
    public static BuildTool Current { get; private set; } = BuildTool.None;

    public static bool TranslateActive => Current == BuildTool.Translate;

    public static void Select(BuildTool tool)
    {
        if (Current == tool) return;
        INMachineBuildEditor.Instance?.OnToolChanged(Current, tool);
        Current = tool;
    }

    /// <summary>Clears the tool, which is what leaving the build screen does.</summary>
    public static void Clear()
    {
        Select(BuildTool.None);
    }

    /// <summary>
    /// Applies the active tool to a part the player clicked. Returns true when the click was consumed, in
    /// which case the build screen must not also start its stock press / long-press / drag handling.
    /// </summary>
    /// <remarks>
    /// This is the whole point of a tool: without it a click means several things at once (tap rotates,
    /// long press opens the panel, drag moves), and the player cannot ask for one of them on purpose.
    /// </remarks>
    public static bool TryApplyToPart(Contraption contraption, BasePart part)
    {
        if (part == null || contraption == null) return false;
        switch (Current)
        {
            case BuildTool.Rotate:
                // The fine step when it is configured, otherwise the stock connectivity-driven turn. Both
                // are the game's own rotation, so a rotated part still ends up connected the same way.
                if (!FineRotate.Step(part, 1)) contraption.Flip(part);
                return true;
            case BuildTool.Mirror:
                part.SetFlipped(!part.m_flipped);
                part.OnChangeConnections();
                return true;
            case BuildTool.Modify:
                // Same precondition the long press has: with the custom-value system off there is no panel
                // to open, so the click falls through rather than being swallowed for nothing.
                if (!INParamSystem.Enabled) return false;
                INPartParameterPanel.Open(part);
                return true;
            default:
                return false;
        }
    }
}

/// <summary>
/// A fixed top toolbar with an independent overlay canvas and shared construction styling.
/// </summary>
/// <remarks>
/// <para>
/// Owned by the construction HUD, like the original vehicle movement buttons. Its independent canvas
/// avoids inheriting the legacy HUD's world-space geometry. It shows only while the player is building.
/// </para>
/// <para>
/// Labels are written here rather than resolved from a locale id, matching the pair next to it
/// (快捷继承 / 显示修改 do the same), so there is nothing to add to the string table.
/// </para>
/// </remarks>
[DisallowMultipleComponent]
public class INBuildToolPalette : MonoBehaviour
{
    private const float SlotW = 78f;
    private const float Tall = 66f;
    private RectTransform m_rect;
    private GameObject m_canvasRoot;
    private Canvas m_canvas;
    private RectTransform m_layers;
    private UText m_hint;
    private Vector2 m_layoutScreen;
    private Rect m_layoutSafe;
    private float m_layoutScale = -1f;
    private readonly List<RectTransform> m_slotRects = new List<RectTransform>();
    private readonly List<(BuildTool tool, UImage bg, UText text, UButton button)> m_buttons = new List<(BuildTool, UImage, UText, UButton)>();
    private static readonly (BuildTool tool, string label, string hint)[] slots = {
        (BuildTool.None, "建造", "放置与移动部件 · 右键按住扫除部件"),
        (BuildTool.Rotate, "点按旋转", "单击部件按设定步长旋转"),
        (BuildTool.TranslatePart, "平移部件", "单击选择部件 · 拖动红 / 绿箭头或中心 · Shift 吸附 0.5 格"),
        (BuildTool.RotatePart, "旋转部件", "单击选择部件 · 拖动圆环 · Shift 按设定步长吸附（默认 45°）"),
        (BuildTool.Mirror, "镜像", "单击部件翻转"),
        (BuildTool.Modify, "部件调整", "单击部件 · 右侧调整菜单"),
        (BuildTool.SelectMachine, "框选", "左键拖动框选载具 · 再点取消选择"),
        (BuildTool.ExtractSelection, "拆出载具", "将框内选中的部分部件拆出为独立载具"),
        (BuildTool.Translate, "平移载具", "拖动红 / 绿箭头或中心方块 · 再点取消选择"),
        (BuildTool.RotateMachine, "旋转载具", "拖动旋转圆环 · Shift 吸附 15° · 再点取消选择"),
        (BuildTool.Gridless, "无网格", "隐藏网格 · 虚线范围内吸附到载具网格"),
        (BuildTool.MergeGridless, "连接合并", "开关 · 松开拖动后，重叠的连接点圆圈合并载具框 · 关闭不拆分已合并框"),
        (BuildTool.Undo, "撤销", "撤销上一次部件或载具变换")
    };
    public static INBuildToolPalette Install(GameObject owner)
    {
        var palette = owner.GetComponent<INBuildToolPalette>();
        return palette != null ? palette : owner.AddComponent<INBuildToolPalette>();
    }
    private void Awake() { BuildUI(); }
    private void BuildUI()
    {
        if (m_canvasRoot != null) return;
        m_buttons.Clear();
        m_slotRects.Clear();
        m_layoutScale = -1f;
        var root = new GameObject("BesiegeBuildToolbar", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        m_canvasRoot = root;
        if (gameObject.scene.IsValid() && root.scene != gameObject.scene)
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, gameObject.scene);
        Canvas canvas = root.GetComponent<Canvas>();
        m_canvas = canvas;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.overrideSorting = true; canvas.sortingOrder = 25000;
        var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
        INUiScale.Register(root.GetComponent<Canvas>());
        INBuildLayerSelector.Install(root);
        m_layers = root.transform.Find("StructuralLayers") as RectTransform;
        GameObject bar = new GameObject("Tools", typeof(RectTransform), typeof(UImage));
        bar.transform.SetParent(root.transform, false);
        m_rect = bar.GetComponent<RectTransform>();
        m_rect.anchorMin = m_rect.anchorMax = new Vector2(0.5f, 1f);
        m_rect.pivot = new Vector2(0.5f, 1f);
        m_rect.anchoredPosition = new Vector2(0f, -12f);
        m_rect.sizeDelta = new Vector2(slots.Length * (SlotW + 4f) + 12f, Tall + 12f);
        bar.GetComponent<UImage>().color = INBuildStyle.Panel;
        INBuildStyle.Bevel(bar);
        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            GameObject go = new GameObject("Tool_" + slot.tool, typeof(RectTransform), typeof(UImage), typeof(UButton));
            go.transform.SetParent(bar.transform, false);
            var rt = go.GetComponent<RectTransform>();
            m_slotRects.Add(rt);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(8f + i * (SlotW + 4f), -6f); rt.sizeDelta = new Vector2(SlotW, Tall);
            var bg = go.GetComponent<UImage>(); bg.color = INBuildStyle.Button;
            var button = go.GetComponent<UButton>(); button.targetGraphic = bg;
            INBuildStyle.Bevel(go); INBuildStyle.ButtonColors(button);
            button.onClick.AddListener(() => OnPick(slot.tool));
            var symbol = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(INBuildToolIcon));
            symbol.transform.SetParent(go.transform, false);
            var iconRect = symbol.GetComponent<RectTransform>(); iconRect.anchoredPosition = new Vector2(0f, 9f); iconRect.sizeDelta = new Vector2(32f, 32f);
            var icon = symbol.GetComponent<INBuildToolIcon>(); icon.raycastTarget = false; icon.color = INBuildStyle.Ivory;
            icon.symbol = (int)slot.tool;
            var text = Label(go.transform, slot.label, 13);
            text.rectTransform.anchoredPosition = new Vector2(0f, -21f); text.rectTransform.sizeDelta = new Vector2(SlotW, 24f);
            m_buttons.Add((slot.tool, bg, text, button));
            EventTrigger trigger = go.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            entry.callback.AddListener(_ => { if (m_hint != null) m_hint.text = slot.hint; }); trigger.triggers.Add(entry);
        }
        m_hint = Label(bar.transform, "框选用左键  ·  再点工具取消选择  ·  右键扫除部件", 13);
        m_hint.color = INBuildStyle.Muted; m_hint.rectTransform.anchorMin = m_hint.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        m_hint.rectTransform.anchoredPosition = new Vector2(0f, -18f);
        m_hint.rectTransform.sizeDelta = new Vector2(m_rect.sizeDelta.x, 22f);
        m_rect.gameObject.SetActive(IsBuildState());
        RefreshLayout();
        RefreshVisual();
    }
    private void RefreshLayout()
    {
        if (m_canvas == null || m_rect == null) return;
        LayoutToolbar(new Vector2(Screen.width, Screen.height), Screen.safeArea, Mathf.Max(0.01f, m_canvas.scaleFactor));
    }
    private void LayoutToolbar(Vector2 screen, Rect safeArea, float scale)
    {
        if (safeArea.width <= 0f || safeArea.height <= 0f) safeArea = new Rect(Vector2.zero, screen);
        if (m_layoutScreen == screen && m_layoutSafe == safeArea && Mathf.Approximately(m_layoutScale, scale)) return;
        m_layoutScreen = screen; m_layoutSafe = safeArea; m_layoutScale = scale;
        float available = Mathf.Max(SlotW + 16f, safeArea.width / scale - 24f);
        int columns = Mathf.Clamp(Mathf.FloorToInt((available - 12f) / (SlotW + 4f)), 1, slots.Length);
        int rows = (slots.Length + columns - 1) / columns;
        float top = 12f + (screen.y - safeArea.yMax) / scale;
        float center = (safeArea.center.x - screen.x * 0.5f) / scale;
        m_rect.anchoredPosition = new Vector2(center, -top);
        m_rect.sizeDelta = new Vector2(columns * (SlotW + 4f) + 12f, rows * (Tall + 4f) + 8f);
        for (int i = 0; i < m_slotRects.Count; i++)
            m_slotRects[i].anchoredPosition = new Vector2(8f + i % columns * (SlotW + 4f), -6f - i / columns * (Tall + 4f));
        if (m_hint != null) m_hint.rectTransform.sizeDelta = new Vector2(m_rect.sizeDelta.x, 22f);
        if (m_layers != null)
        {
            m_layers.anchoredPosition = new Vector2(center, -(top + m_rect.sizeDelta.y + 32f));
            float layerScale = Mathf.Min(1f, available / 564f);
            m_layers.localScale = new Vector3(layerScale, layerScale, 1f);
        }
    }
    private static UText Label(Transform parent, string value, int size)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(UText)); go.transform.SetParent(parent, false);
        var text = go.GetComponent<UText>(); text.font = INUiUtil.UiFont; text.text = value; text.fontSize = size;
        text.color = INBuildStyle.Ivory; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
        return text;
    }
    private void OnEnable()
    {
        BuildUI();
        m_canvasRoot.SetActive(true);
        Canvas.preWillRenderCanvases -= RefreshLayout;
        Canvas.preWillRenderCanvases += RefreshLayout;
        Update();
    }
    private void OnDisable()
    {
        Canvas.preWillRenderCanvases -= RefreshLayout;
        if (m_canvasRoot != null) m_canvasRoot.SetActive(false);
        BuildToolPalette.Clear();
    }
    private void OnDestroy()
    {
        Canvas.preWillRenderCanvases -= RefreshLayout;
        if (m_canvasRoot == null) return;
        if (Application.isPlaying) Destroy(m_canvasRoot); else DestroyImmediate(m_canvasRoot);
    }
    private void OnPick(BuildTool tool)
    {
        if (tool == BuildTool.Gridless)
        {
            var settings = INUserSettings.Instance?.GeneralSettings;
            if (settings != null) { settings.NoGridMode = !settings.NoGridMode; INUserSettings.Save(); }
        }
        else if (tool == BuildTool.MergeGridless)
        {
            var settings = INUserSettings.Instance?.GeneralSettings;
            if (settings != null) { settings.MergeGridlessGroups = !settings.MergeGridlessGroups; INUserSettings.Save(); }
        }
        else if (tool == BuildTool.Undo) INMachineBuildEditor.Instance?.Undo();
        else if (tool == BuildTool.ExtractSelection) INMachineBuildEditor.Instance?.ExtractSelection();
        else
        {
            bool toggledOff = BuildToolPalette.Current == tool;
            BuildToolPalette.Select(toggledOff ? BuildTool.None : tool);
            bool machineTool = tool == BuildTool.SelectMachine || tool == BuildTool.Translate || tool == BuildTool.RotateMachine;
            if (machineTool && toggledOff) INMachineBuildEditor.Instance?.DeselectMachine();
            else if (tool == BuildTool.Translate || tool == BuildTool.RotateMachine) INMachineBuildEditor.Instance?.SelectMachine();
        }
        RefreshVisual();
    }
    private void RefreshVisual()
    {
        foreach (var entry in m_buttons)
        {
            bool available = entry.tool != BuildTool.ExtractSelection || INMachineBuildEditor.Instance?.CanExtractSelection == true;
            entry.button.interactable = available;
            bool active = entry.tool == BuildTool.Gridless ? GridlessBuild.Enabled :
                entry.tool == BuildTool.MergeGridless ? (INUserSettings.Instance?.GeneralSettings?.MergeGridlessGroups ?? false) :
                entry.tool == BuildToolPalette.Current;
            entry.bg.color = active ? INBuildStyle.Brass : INBuildStyle.Button;
            entry.text.color = !available ? INBuildStyle.Muted : active ? new Color(0.12f, 0.12f, 0.10f) : INBuildStyle.Ivory;
        }
    }
    private void Update()
    {
        if (m_canvasRoot == null) BuildUI();
        RefreshLayout();
        bool building = IsBuildState();
        if (!building && BuildToolPalette.Current != BuildTool.None) BuildToolPalette.Clear();
        if (m_rect != null && m_rect.gameObject.activeSelf != building) m_rect.gameObject.SetActive(building);
        if (building) RefreshVisual();
    }
    public static bool IsBuildState()
    {
        LevelManager level = WPFMonoBehaviour.levelManager;
        return level != null && level.gameState == LevelManager.GameState.Building;
    }
}
