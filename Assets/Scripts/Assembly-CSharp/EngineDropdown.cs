using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The engine picker shown in the settings screen, matching bpsim_2023.0's engine switch.
/// </summary>
/// <remarks>
/// <para>
/// bpsim_2023.0 renders its engine choice (<c>SimulationSettings.SimulationType</c>) as a plain
/// <see cref="Dropdown"/> placed in the row's value slot; see
/// <c>bpsim_2023.0/Assets/Prefab/UserSettingsPanel.prefab</c>, object <c>DropdownTemplate</c>. The
/// same control is assembled here at runtime instead of shipping a prefab, because the list of
/// engines depends on what the running build can actually load.
/// </para>
/// <para>
/// The layout constants below are the values measured from that prefab, so both projects' engine
/// pickers look and behave the same. The single addition is the availability rule: an engine this
/// build cannot load is still listed, but its entry is dimmed and picking it is refused, which
/// leaves the previous engine selected and raises <see cref="Blocked"/> with the reason.
/// </para>
/// </remarks>
public sealed class EngineDropdown : MonoBehaviour
{
    // --- Style, measured from bpsim_2023.0/Assets/Prefab/UserSettingsPanel.prefab ---------------

    private static readonly Color s_background = new Color(0.25f, 0.25f, 0.25f, 1f);
    private static readonly Color s_normal = new Color(1f, 1f, 1f, 1f);
    private static readonly Color s_highlighted = new Color(0.9607843f, 0.9607843f, 0.9607843f, 1f);
    private static readonly Color s_pressed = new Color(0.7843137f, 0.7843137f, 0.7843137f, 1f);
    private static readonly Color s_selected = new Color(0.9607843f, 0.9607843f, 0.9607843f, 1f);
    private static readonly Color s_disabledTint = new Color(0.7843137f, 0.7843137f, 0.7843137f, 0.5019608f);
    private static readonly Color s_unavailable = new Color(0.55f, 0.55f, 0.55f, 1f);

    private const float ArrowSize = 30f;
    private const float ArrowInset = 15f;
    private const float LabelInset = 10f;
    private const float ItemHeight = 60f;
    private const float ListHeight = 240f;

    private static string s_unavailableHex;

    private Dropdown m_dropdown;
    private Michsky.UI.Heat.Dropdown m_heat;
    private readonly List<PhysicsEngineInfo> m_engines = new List<PhysicsEngineInfo>();
    private PhysicsEngine m_value;
    private bool m_refreshing;

    /// <summary>Raised when the user picks an engine this build can load.</summary>
    public event Action<PhysicsEngine> Selected;

    /// <summary>Raised when the user picks an engine this build cannot load; nothing changes.</summary>
    public event Action<PhysicsEngineInfo> Blocked;

    /// <summary>The engine the control currently displays.</summary>
    public PhysicsEngine Value
    {
        get { return m_value; }
        set
        {
            m_value = value;
            int index = IndexOf(value);
            if (index < 0) return;

            // Heat UI's dropdown keeps its own index and caption, so it is told the index instead of
            // being handed a list of options the way the built-in control is.
            if (m_heat != null)
            {
                m_refreshing = true;
                try { m_heat.SetDropdownIndex(index); }
                finally { m_refreshing = false; }
                return;
            }

            if (m_dropdown == null) return;
            m_refreshing = true;
            try { m_dropdown.SetValueWithoutNotify(index); }
            finally { m_refreshing = false; }
            m_dropdown.RefreshShownValue();
        }
    }

    /// <summary>
    /// Builds the control on <paramref name="slot"/> and returns it. The caller owns the slot's
    /// geometry; this only fills it with the dropdown.
    /// </summary>
    public static EngineDropdown Create(RectTransform slot, Font font, IReadOnlyList<PhysicsEngineInfo> engines, GameObject heatTemplate = null)
    {
        if (slot == null) throw new ArgumentNullException("slot");

        // Heat UI first when a template was supplied; a failure inside its prefabs falls back to the
        // control this project draws itself rather than taking the settings screen down with it.
        if (heatTemplate != null && TryBuildHeat(slot, heatTemplate, engines, out EngineDropdown heatControl))
        {
            return heatControl;
        }

        var background = slot.GetComponent<Image>();
        if (background == null) background = slot.gameObject.AddComponent<Image>();
        background.sprite = null;
        background.type = Image.Type.Sliced;
        background.color = s_background;

        var dropdown = slot.GetComponent<Dropdown>();
        if (dropdown == null) dropdown = slot.gameObject.AddComponent<Dropdown>();
        dropdown.targetGraphic = background;
        dropdown.transition = Selectable.Transition.ColorTint;
        dropdown.colors = Tint();

        var control = slot.GetComponent<EngineDropdown>();
        if (control == null) control = slot.gameObject.AddComponent<EngineDropdown>();
        control.Build(dropdown, font, engines);
        return control;
    }

    /// <summary>
    /// Builds Heat UI's dropdown in the slot, returning false - having removed whatever it created - when
    /// the template has no Heat UI dropdown or building one threw.
    /// </summary>
    /// <remarks>
    /// Heat UI's control is a prefab with its own list, animator and item preset, not a skin over a
    /// UnityEngine.UI.Dropdown, so it is instantiated and driven through its own API. A failure here
    /// would otherwise take the whole settings screen with it, which is why it is contained.
    /// </remarks>
    private static bool TryBuildHeat(RectTransform slot, GameObject template, IReadOnlyList<PhysicsEngineInfo> engines, out EngineDropdown control)
    {
        control = null;
        GameObject instance = null;
        try
        {
            instance = UnityEngine.Object.Instantiate(template, slot, false);
            instance.name = "HeatDropdown";

            var rect = (RectTransform)instance.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var heat = instance.GetComponentInChildren<Michsky.UI.Heat.Dropdown>(true);
            if (heat == null)
            {
                UnityEngine.Object.Destroy(instance);
                return false;
            }

            control = slot.GetComponent<EngineDropdown>();
            if (control == null) control = slot.gameObject.AddComponent<EngineDropdown>();
            control.BuildHeat(heat, engines);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Heat UI engine dropdown could not be built (" + exception.Message + "); falling back to the built-in control.");
            if (instance != null) UnityEngine.Object.Destroy(instance);
            if (control != null) UnityEngine.Object.Destroy(control);
            control = null;
            return false;
        }
    }

    private void BuildHeat(Michsky.UI.Heat.Dropdown heat, IReadOnlyList<PhysicsEngineInfo> engines)
    {
        m_heat = heat;
        m_engines.Clear();
        if (engines != null) m_engines.AddRange(engines);

        FillHeatItems();
        m_heat.onValueChanged.AddListener(OnValueChanged);
    }

    /// <summary>
    /// Rebuilds Heat UI's entries. It instantiates its own item preset per entry, so entries go in
    /// through its API and its layout is refreshed afterwards; the labels stay rich text, which is what
    /// lets an unavailable engine be dimmed with the reason beside it.
    /// </summary>
    private void FillHeatItems()
    {
        if (m_heat == null) return;

        m_heat.items.Clear();
        if (m_heat.itemParent != null)
        {
            foreach (Transform child in m_heat.itemParent) UnityEngine.Object.Destroy(child.gameObject);
        }

        for (int i = 0; i < m_engines.Count; i++)
        {
            PhysicsEngineInfo info = m_engines[i];
            m_heat.CreateNewItem(info.Available ? info.Name : Dim(info), null, false);
        }

        m_heat.Initialize();
        m_heat.SetDropdownIndex(Mathf.Clamp(IndexOf(m_value), 0, m_engines.Count - 1));
    }

    private void Build(Dropdown dropdown, Font font, IReadOnlyList<PhysicsEngineInfo> engines)
    {
        m_dropdown = dropdown;
        m_engines.Clear();
        if (engines != null) m_engines.AddRange(engines);

        // captionText and template both refresh the shown value, so the caption is wired first.
        var caption = CreateText("Label", transform, font, TextAnchor.MiddleLeft);
        Stretch(caption.rectTransform, LabelInset, ArrowInset + ArrowSize + 5f);
        m_dropdown.captionText = caption;

        GameObject arrowObject = NewChild("Arrow", transform);
        var arrowRect = (RectTransform)arrowObject.transform;
        arrowRect.anchorMin = new Vector2(1f, 0.5f);
        arrowRect.anchorMax = new Vector2(1f, 0.5f);
        arrowRect.pivot = new Vector2(0.5f, 0.5f);
        arrowRect.anchoredPosition = new Vector2(-ArrowInset, 0f);
        arrowRect.sizeDelta = new Vector2(ArrowSize, ArrowSize);
        var arrow = arrowObject.AddComponent<Image>();
        arrow.sprite = ArrowSprite();
        arrow.color = Color.white;
        arrow.raycastTarget = false;

        RectTransform template = null;
        Text itemText = null;
        BuildList(font, out template, out itemText);
        m_dropdown.template = template;
        m_dropdown.itemText = itemText;

        m_dropdown.options = BuildOptions();
        m_dropdown.onValueChanged.AddListener(OnValueChanged);
        m_dropdown.RefreshShownValue();
    }

    /// <summary>
    /// Rebuilds the option list so availability changes (for example toggling the experimental Jolt
    /// gate) show up without reopening the screen. The selected engine is preserved.
    /// </summary>
    public void RefreshEngines(IReadOnlyList<PhysicsEngineInfo> engines)
    {
        if (engines == null) return;
        if (m_heat == null && m_dropdown == null) return;
        if (SameAvailability(engines)) return;

        m_engines.Clear();
        m_engines.AddRange(engines);

        if (m_heat != null)
        {
            FillHeatItems();
            Value = m_value;
            return;
        }

        m_dropdown.options = BuildOptions();
        Value = m_value;
    }

    private bool SameAvailability(IReadOnlyList<PhysicsEngineInfo> engines)
    {
        if (engines.Count != m_engines.Count) return false;
        for (int i = 0; i < engines.Count; i++)
        {
            if (engines[i].Engine != m_engines[i].Engine) return false;
            if (engines[i].Available != m_engines[i].Available) return false;
            if (engines[i].Detail != m_engines[i].Detail) return false;
        }
        return true;
    }

    private List<Dropdown.OptionData> BuildOptions()
    {
        var options = new List<Dropdown.OptionData>(m_engines.Count);
        for (int i = 0; i < m_engines.Count; i++)
        {
            PhysicsEngineInfo info = m_engines[i];
            options.Add(new Dropdown.OptionData(info.Available ? info.Name : Dim(info)));
        }
        return options;
    }

    /// <summary>Dimmed caption for an engine that cannot be selected, with the reason beside it.</summary>
    private static string Dim(PhysicsEngineInfo info)
    {
        if (s_unavailableHex == null) s_unavailableHex = ColorUtility.ToHtmlStringRGB(s_unavailable);
        return "<color=#" + s_unavailableHex + ">" + info.Name + " \u2014 " + info.Detail + "</color>";
    }

    private void OnValueChanged(int index)
    {
        if (m_refreshing) return;
        if (m_heat == null && m_dropdown == null) return;
        if (index < 0 || index >= m_engines.Count) return;

        PhysicsEngineInfo info = m_engines[index];
        if (!info.Available)
        {
            // Put the caption back where it was: the entry is listed, not selectable.
            Value = m_value;
            Action<PhysicsEngineInfo> blocked = Blocked;
            if (blocked != null) blocked(info);
            return;
        }

        m_value = info.Engine;
        Action<PhysicsEngine> selected = Selected;
        if (selected != null) selected(m_value);
    }

    private int IndexOf(PhysicsEngine engine)
    {
        for (int i = 0; i < m_engines.Count; i++)
        {
            if (m_engines[i].Engine == engine) return i;
        }
        return -1;
    }

    // --- Widget construction --------------------------------------------------------------------

    private void BuildList(Font font, out RectTransform template, out Text itemText)
    {
        GameObject templateObject = NewChild("Template", transform);
        RectTransform templateRect = (RectTransform)templateObject.transform;
        // Opens downwards from the bottom edge of the dropdown, as in the reference prefab.
        templateRect.anchorMin = new Vector2(0f, 0f);
        templateRect.anchorMax = new Vector2(1f, 0f);
        templateRect.pivot = new Vector2(0.5f, 1f);
        templateRect.anchoredPosition = new Vector2(0f, 2f);
        templateRect.sizeDelta = new Vector2(0f, ListHeight);

        var templateBackground = templateObject.AddComponent<Image>();
        templateBackground.color = s_background;

        GameObject viewportObject = NewChild("Viewport", templateObject.transform);
        RectTransform viewportRect = (RectTransform)viewportObject.transform;
        Stretch(viewportRect, 0f, 0f);
        viewportObject.AddComponent<RectMask2D>();

        GameObject contentObject = NewChild("Content", viewportObject.transform);
        RectTransform contentRect = (RectTransform)contentObject.transform;
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, ItemHeight);

        GameObject itemObject = NewChild("Item", contentObject.transform);
        RectTransform itemRect = (RectTransform)itemObject.transform;
        itemRect.anchorMin = new Vector2(0f, 0.5f);
        itemRect.anchorMax = new Vector2(1f, 0.5f);
        itemRect.pivot = new Vector2(0.5f, 0.5f);
        itemRect.anchoredPosition = Vector2.zero;
        itemRect.sizeDelta = new Vector2(0f, ItemHeight);

        var itemBackground = itemObject.AddComponent<Image>();
        itemBackground.color = s_background;

        var toggle = itemObject.AddComponent<Toggle>();
        toggle.targetGraphic = itemBackground;
        toggle.transition = Selectable.Transition.ColorTint;
        toggle.colors = Tint();

        Text label = CreateText("Item Label", itemObject.transform, font, TextAnchor.MiddleLeft);
        Stretch(label.rectTransform, LabelInset, LabelInset);

        // Dropdown lays the items out itself; the scroll rect only adds wheel scrolling for long lists.
        var scroll = templateObject.AddComponent<ScrollRect>();
        scroll.viewport = viewportRect;
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 20f;

        templateObject.SetActive(false);
        template = templateRect;
        itemText = label;
    }

    private Text CreateText(string name, Transform parent, Font font, TextAnchor alignment)
    {
        GameObject textObject = NewChild(name, parent);
        var text = textObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = 28;
        text.fontStyle = FontStyle.Bold;
        text.alignment = alignment;
        text.color = Color.white;
        text.supportRichText = true;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect, float left, float right)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, 0f);
        rect.offsetMax = new Vector2(-right, 0f);
    }

    private GameObject NewChild(string name, Transform parent)
    {
        var child = new GameObject(name, typeof(RectTransform));
        child.layer = parent.gameObject.layer;
        child.transform.SetParent(parent, false);
        return child;
    }

    private static ColorBlock Tint()
    {
        var block = ColorBlock.defaultColorBlock;
        block.normalColor = s_normal;
        block.highlightedColor = s_highlighted;
        block.pressedColor = s_pressed;
        block.selectedColor = s_selected;
        block.disabledColor = s_disabledTint;
        block.colorMultiplier = 1f;
        block.fadeDuration = 0.1f;
        return block;
    }

    // --- Arrow ----------------------------------------------------------------------------------

    private static UnityEngine.Sprite s_arrow;

    /// <summary>
    /// The arrow is drawn rather than shipped as an asset: the reference project keeps its own
    /// <c>DropdownArrow.png</c>, and a generated triangle keeps this project free of a dependency on
    /// it while looking the same.
    /// </summary>
    /// <remarks>
    /// <c>UnityEngine.Sprite</c> is spelled out because this project declares its own global
    /// <see cref="global::Sprite"/> component, which would otherwise win name resolution.
    /// </remarks>
    private static UnityEngine.Sprite ArrowSprite()
    {
        if (s_arrow != null) return s_arrow;

        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "EngineDropdownArrow";
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            // Texture rows run bottom-up, so the wide edge is the top one: a downward triangle.
            float half = 0.5f * ((float)y / (size - 1));
            for (int x = 0; x < size; x++)
            {
                float offset = Mathf.Abs(((float)x / (size - 1)) - 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, offset <= half ? (byte)255 : (byte)0);
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();

        s_arrow = UnityEngine.Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        s_arrow.name = "EngineDropdownArrow";
        s_arrow.hideFlags = HideFlags.HideAndDontSave;
        return s_arrow;
    }
}
