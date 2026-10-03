using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Bridges this project's panels onto Heat UI widgets.
/// </summary>
/// <remarks>
/// <para>
/// Heat UI draws its widgets from prefabs, so a row built from one of its templates has a different
/// shape from the row this project ships. The label is TextMeshPro rather than
/// <see cref="UnityEngine.UI.Text"/>, the toggle is <c>Michsky.UI.Heat.SwitchManager</c> rather than
/// <see cref="ToggleSwitch"/>, and the choice control is <c>Michsky.UI.Heat.Dropdown</c> rather than
/// <see cref="UnityEngine.UI.Dropdown"/>.
/// </para>
/// <para>
/// Everything here is a lookup that falls back to the project's own layout, so a row keeps working
/// whether it was instantiated from a Heat UI template or from the original one. Nothing in this file
/// requires Heat UI to be present at a call site: when a widget is missing, the binding reports itself
/// unavailable and the caller keeps its previous behaviour.
/// </para>
/// </remarks>
public static class HeatUiSkin
{
    /// <summary>Child the project's own row template uses for the setting's name.</summary>
    public const string LegacyNameChild = "Name";

    /// <summary>Child the project's own group template uses for the group's name.</summary>
    public const string LegacyGroupNameChild = "GroupName";

    /// <summary>Child Heat UI puts the label on, in both its settings header and its settings elements.</summary>
    public const string HeatLabelChild = "Text";

    // --- Panel chrome ---------------------------------------------------------------------------

    private static Michsky.UI.Heat.UIManager s_manager;
    private static bool s_managerLookedUp;
    private static TMP_FontAsset s_chineseFont;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitializeFontEngine()
    {
        UnityEngine.TextCore.LowLevel.FontEngine.InitializeFontEngine();
        SettleLocalization();
    }

    /// <summary>
    /// Gives Heat UI the <c>LocalizationManager</c> its widgets look for, before any of them are built.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>LocalizedObject.Awake</c> takes a <c>LocalizationManager</c>, and when the project has none it
    /// looks for one with <c>Resources.FindObjectsOfTypeAll</c> - a walk over everything loaded, run once
    /// per component that carries a <c>LocalizedObject</c>. A settings row carries four of them: its label,
    /// and the two state labels inside its switch. Measured on this machine that is about 12ms a row, so
    /// building the 208 rows of 更多设置 cost over two seconds and the first open of the panel cost several.
    /// </para>
    /// <para>
    /// Localization is switched off in the UIManager asset and this project localizes through
    /// <c>INLocalization</c>, so a manager that simply exists is enough: it takes the pack's own early
    /// return, which destroys the component, instead of the search. The manager's own asset lookup is done
    /// here as well, while its host is still inactive, so it does not run that search either.
    /// </para>
    /// </remarks>
    private static void SettleLocalization()
    {
        if (Michsky.UI.Heat.LocalizationManager.instance != null) { return; }
        GameObject host = new GameObject("Heat UI Localization");
        host.SetActive(false);
        Michsky.UI.Heat.LocalizationManager manager = host.AddComponent<Michsky.UI.Heat.LocalizationManager>();
        manager.UIManagerAsset = Manager();
        UnityEngine.Object.DontDestroyOnLoad(host);
        host.hideFlags = HideFlags.HideAndDontSave;
        host.SetActive(true);
    }

    public static void ApplyTypography(GameObject root)
    {
        if (s_chineseFont == null)
        {
            s_chineseFont = Resources.Load<TMP_FontAsset>("HeatUI/Chinese Fallback");
        }
        if (root == null || s_chineseFont == null || s_chineseFont.sourceFontFile == null)
        {
            return;
        }
        foreach (Text label in root.GetComponentsInChildren<Text>(true))
        {
            if (label.font != s_chineseFont.sourceFontFile) label.font = s_chineseFont.sourceFontFile;
        }
    }

    public static void CompactActionButton(GameObject button)
    {
        if (button.transform.parent == null || button.transform.parent.name != "ButtonGroup") { return; }
        var manager = button.GetComponent<Michsky.UI.Heat.ButtonManager>();
        if (manager != null)
        {
            manager.autoFitContent = false;
            manager.textSize = 22;
            manager.iconScale = 0.8f;
            manager.buttonIcon = Resources.Load<UnityEngine.Sprite>("HeatUI/Icons/" +
                (button.name.Contains("Reset") ? "Restart" : "Load"));
            manager.UpdateUI();
        }
        // The caption is Chinese on this project's panels, so the pack's caption cannot wrap: Heat UI's
        // button gives its label 18px of padding a side, 12px of spacing and 20px to the icon, which on
        // the old 150px button left 82px - less than the ~95px four Chinese glyphs need, so the caption
        // broke onto a second line that then overflowed the 44px button. The button is as wide as the
        // legacy one it replaces (200), and wrapping is switched off outright so no caption can split.
        if (manager != null)
        {
            foreach (TMPro.TMP_Text label in button.GetComponentsInChildren<TMPro.TMP_Text>(true))
            {
                label.enableWordWrapping = false;
            }
        }
        RectTransform rect = (RectTransform)button.transform;
        rect.sizeDelta = new Vector2(200, 44);
        LayoutElement element = button.GetComponent<LayoutElement>();
        if (element == null) { element = button.AddComponent<LayoutElement>(); }
        element.minWidth = element.preferredWidth = 200;
        element.minHeight = element.preferredHeight = 44;
        element.flexibleWidth = element.flexibleHeight = 0;
        RectTransform group = (RectTransform)button.transform.parent;
        group.sizeDelta = new Vector2(group.sizeDelta.x, 44);
        var layout = group.GetComponent<HorizontalLayoutGroup>();
        if (layout == null) { layout = group.gameObject.AddComponent<HorizontalLayoutGroup>(); }
        if (layout != null)
        {
            layout.spacing = 12;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleRight;
        }
    }

    /// <summary>
    /// Heat UI's colour asset, or null when the pack is not there. Cached, and the miss is remembered so
    /// a project without the pack does not repeat the search once per panel.
    /// </summary>
    /// <remarks>
    /// Worth checking before adding any <c>UIManagerImage</c>: that component reads
    /// <c>UIManagerAsset.enableDynamicUpdate</c> in its own Awake without a null check, so attaching one
    /// while the asset is missing would throw and take the panel with it.
    /// </remarks>
    public static Michsky.UI.Heat.UIManager Manager()
    {
        if (!s_managerLookedUp)
        {
            s_managerLookedUp = true;
            s_manager = Resources.Load<Michsky.UI.Heat.UIManager>("Heat UI Manager");
            if (s_manager == null)
            {
                Debug.LogWarning("Heat UI's UIManager asset ('Heat UI Manager') is not in Resources; panels keep their own colours.");
            }
        }

        return s_manager;
    }

    /// <summary>
    /// Brings a panel's own chrome into Heat UI's palette.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Heat UI keeps its colours on the UIManager asset and its images read them through
    /// <c>UIManagerImage</c>, so the panel is recoloured the same way rather than with copied constants.
    /// The pack's own panels use the <c>Background</c> type for their body areas, which is what the
    /// panels' <c>Panel/Background</c> is; the app interface's bars are tinted the same way.
    /// </para>
    /// <para>
    /// Only objects named <c>Background</c> directly under <c>Panel</c>, <c>ActionBar</c> or
    /// <c>Sidebar</c> are touched, and each is skipped when it has no image or already carries the
    /// component. Nothing in this project writes those images' colours, so Heat UI's per-frame update
    /// does not fight any fade or tint of its own.
    /// </para>
    /// </remarks>
    public static void ApplyPanelSkin(GameObject panel)
    {
        ApplyTypography(panel);
        if (panel == null || Manager() == null)
        {
            return;
        }

        TintPanelBackground(panel, "Panel");
        TintPanelBackground(panel, "ActionBar");
        TintPanelBackground(panel, "Sidebar");
    }

    private static void TintPanelBackground(GameObject root, string ownerName)
    {
        Transform owner = FindDeep(root.transform, ownerName);
        if (owner == null)
        {
            return;
        }

        Transform background = owner.Find("Background");
        if (background == null)
        {
            return;
        }

        var image = background.GetComponent<UnityEngine.UI.Image>();
        if (image == null)
        {
            return;
        }

        var tint = background.GetComponent<Michsky.UI.Heat.UIManagerImage>();
        if (tint == null)
        {
            tint = background.gameObject.AddComponent<Michsky.UI.Heat.UIManagerImage>();
        }

        if (tint != null)
        {
            tint.colorType = Michsky.UI.Heat.UIManagerImage.ColorType.Background;
        }
    }

    /// <summary>Depth-first search for a descendant by name, the object itself excluded.</summary>
    private static Transform FindDeep(Transform root, string name)
    {
        if (root == null)
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == name)
            {
                return child;
            }

            Transform found = FindDeep(child, name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    // --- Templates ------------------------------------------------------------------------------

    /// <summary>Folder under a Resources folder holding the staged Heat UI templates.</summary>
    public const string ResourceFolder = "HeatUI";

    /// <summary>Group heading, instantiated once per settings group.</summary>
    public const string HeaderTemplate = "Settings Header";

    /// <summary>Row with a Heat UI switch, used for boolean settings.</summary>
    public const string SwitchElementTemplate = "Settings Element (Switch)";

    /// <summary>Row with a Heat UI dropdown, used for choices.</summary>
    public const string DropdownElementTemplate = "Settings Element (Dropdown)";

    /// <summary>Standalone TextMeshPro input field, for rows that need a value slot.</summary>
    public const string InputFieldTemplate = "Input Field";

    /// <summary>Standalone dropdown, for the engine picker.</summary>
    public const string DropdownTemplate = "Dropdown";

    /// <summary>Standard Heat UI button.</summary>
    public const string ButtonTemplate = "Button";

    private static readonly Dictionary<string, GameObject> s_templates = new Dictionary<string, GameObject>();

    /// <summary>
    /// Loads a Heat UI template staged under <c>Assets/Resources/HeatUI</c>, or null when it is not there.
    /// </summary>
    /// <remarks>
    /// Null is a normal answer, not a failure: it means the pack has not been staged
    /// (<c>Tools/HeatUI/Stage-Resources.ps1</c>) and the caller keeps its own template. The lookup is
    /// cached because a panel builds one row per setting, and a missing template warns once per name.
    /// </remarks>
    public static GameObject LoadTemplate(string name)
    {
        GameObject cached;
        if (s_templates.TryGetValue(name, out cached))
        {
            return cached;
        }

        GameObject loaded = Resources.Load<GameObject>(ResourceFolder + "/" + name);
        s_templates[name] = loaded;
        if (loaded == null)
        {
            Debug.LogWarning("Heat UI template '" + name + "' is not staged under Assets/Resources/" + ResourceFolder + "; run Tools/HeatUI/Stage-Resources.ps1.");
        }

        return loaded;
    }

    // --- Labels ---------------------------------------------------------------------------------

    /// <summary>
    /// Finds the object carrying a row's or group's label: the project's child first, then Heat UI's,
    /// then any text in the subtree. Prefers TextMeshPro when it has to guess.
    /// </summary>
    public static Transform FindLabel(GameObject root, string legacyChildName)
    {
        if (root == null)
        {
            return null;
        }

        Transform legacy = root.transform.Find(legacyChildName);
        if (legacy != null)
        {
            return legacy;
        }

        Transform heat = root.transform.Find(HeatLabelChild);
        if (heat != null)
        {
            return heat;
        }

        TMP_Text tmp = root.GetComponentInChildren<TMP_Text>(includeInactive: true);
        if (tmp != null)
        {
            return tmp.transform;
        }

        UnityEngine.UI.Text text = root.GetComponentInChildren<UnityEngine.UI.Text>(includeInactive: true);
        return text != null ? text.transform : null;
    }

    /// <summary>
    /// Returns the <see cref="UITextLocale"/> for a row or group, attaching one to the label when the
    /// template came from Heat UI and therefore has none. Returns null when there is no label at all,
    /// which callers treat as "this row cannot be localized" rather than as an error.
    /// </summary>
    public static UITextLocale EnsureLocale(GameObject root, string legacyChildName)
    {
        if (root == null)
        {
            return null;
        }

        Transform label = FindLabel(root, legacyChildName);
        if (label != null)
        {
            UITextLocale onLabel = label.GetComponent<UITextLocale>();
            if (onLabel != null)
            {
                return onLabel;
            }
        }

        UITextLocale anywhere = root.GetComponentInChildren<UITextLocale>(includeInactive: true);
        if (anywhere != null)
        {
            return anywhere;
        }

        return label != null ? label.gameObject.AddComponent<UITextLocale>() : null;
    }

    /// <summary>
    /// Writes a string into a label, whichever text component it carries. Used by the dirty marker on
    /// the save button, which appends an asterisk to the localized caption.
    /// </summary>
    public static void SetLabelText(GameObject root, string legacyChildName, string value)
    {
        Transform label = FindLabel(root, legacyChildName);
        if (label == null)
        {
            return;
        }

        TMP_Text tmp = label.GetComponent<TMP_Text>();
        if (tmp != null)
        {
            tmp.text = value;
            return;
        }

        UnityEngine.UI.Text text = label.GetComponent<UnityEngine.UI.Text>();
        if (text != null)
        {
            text.text = value;
        }
    }

    // --- Widget lookup --------------------------------------------------------------------------

    /// <summary>The Heat UI switch in a row, or null when the row came from the project's template.</summary>
    public static Michsky.UI.Heat.SwitchManager FindSwitch(GameObject root)
    {
        return root == null ? null : root.GetComponentInChildren<Michsky.UI.Heat.SwitchManager>(includeInactive: true);
    }

    /// <summary>The TextMeshPro input field in a row, or null when the row uses the legacy one.</summary>
    public static TMP_InputField FindInputField(GameObject root)
    {
        return root == null ? null : root.GetComponentInChildren<TMP_InputField>(includeInactive: true);
    }

    /// <summary>
    /// The Heat UI dropdown in a row. Qualified in full: this project also has a global
    /// <c>EngineDropdown</c>, and Heat UI's type shares its name with <see cref="UnityEngine.UI.Dropdown"/>.
    /// </summary>
    public static Michsky.UI.Heat.Dropdown FindDropdown(GameObject root)
    {
        return root == null ? null : root.GetComponentInChildren<Michsky.UI.Heat.Dropdown>(includeInactive: true);
    }

    // --- Buttons --------------------------------------------------------------------------------

    /// <summary>
    /// Swaps a panel button for Heat UI's, returning the binding for it, or null when the swap could not
    /// produce a working button - in which case the original is left exactly as it was.
    /// </summary>
    /// <remarks>
    /// Heat UI's button is a prefab whose <c>ButtonManager</c> adds its own
    /// <see cref="UnityEngine.UI.Button"/> while its Awake runs, so the replacement is only accepted once
    /// that component is really there: the panels drive their buttons through <c>onClick</c>, and losing
    /// it would leave the screen unusable. The caption's locale id is carried over, so the panels'
    /// localization keeps driving the label.
    /// </remarks>
    public static HeatUiButtonBinding ReplaceButton(UnityEngine.UI.Button existing, string templateName)
    {
        if (existing == null || existing.transform.parent == null)
        {
            return null;
        }

        GameObject template = LoadTemplate(templateName);
        if (template == null)
        {
            return null;
        }

        GameObject instance = null;
        try
        {
            instance = UnityEngine.Object.Instantiate(template, existing.transform.parent, false);
            instance.name = existing.gameObject.name + " (Heat UI)";

            CopyGeometry(instance.transform, existing.transform);
            instance.transform.SetSiblingIndex(existing.transform.GetSiblingIndex());

            var manager = instance.GetComponentInChildren<Michsky.UI.Heat.ButtonManager>(includeInactive: true);
            if (manager == null)
            {
                Debug.LogWarning("Heat UI button template '" + templateName + "' carries no ButtonManager; keeping '" + existing.name + "'.");
                Discard(instance);
                return null;
            }

            // Heat UI has its own localization table; this project's INLocalization drives the caption.
            manager.useLocalization = false;
            manager.autoFitContent = false;
            manager.UpdateUI();
            CopyGeometry(instance.transform, existing.transform);

            // ButtonManager adds this itself, but only while playing and only once the object is enabled.
            // The panels build their buttons from Awake, which happens while the panel may still be
            // closed, so the click target is put in place here rather than waited for. ButtonManager
            // finds it again when it enables rather than adding a second one.
            var button = instance.GetComponent<UnityEngine.UI.Button>();
            if (button == null)
            {
                button = instance.AddComponent<UnityEngine.UI.Button>();
            }

            var binding = new HeatUiButtonBinding(button, manager);
            button.interactable = existing.interactable;
            manager.Interactable(existing.interactable);

            UITextLocale locale = existing.GetComponentInChildren<UITextLocale>(includeInactive: true);
            if (locale != null && !string.IsNullOrEmpty(locale.ID))
            {
                // Carrying the caption's locale id over is a separate concern from the swap itself, so it is
                // guarded on its own: reading a locale needs the localization system to be running, and a
                // context without it must not cost the button its conversion. The panels write the caption
                // themselves anyway, so the button still ends up labelled.
                try
                {
                    UITextLocale carried = instance.GetComponent<UITextLocale>();
                    if (carried == null)
                    {
                        carried = instance.AddComponent<UITextLocale>();
                    }

                    carried.ID = locale.ID;
                    binding.Label = carried.Text;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("Heat UI button '" + existing.name + "' was converted without carrying its caption locale (" + exception.Message + ").");
                }
            }

            Discard(existing.gameObject);
            CompactActionButton(instance);
            return binding;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Heat UI button could not replace '" + existing.name + "' (" + exception.Message + "); keeping the original.");
            Discard(instance);
            return null;
        }
    }

    /// <summary>Activates a template's widget, which ships inactive so it does not show before use.</summary>
    public static void Activate(GameObject gameObject)
    {
        if (gameObject != null && !gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Gives one object another's rect: anchors, pivot, position, size, and the local rotation and scale.
    /// </summary>
    /// <remarks>
    /// Used two ways. To place a replacement exactly where the control it replaces was. And to put a Heat
    /// UI row or heading onto the geometry the panel around it was laid out for: the panels position
    /// their children with a layout group that controls neither width nor height, so a child keeps its
    /// own rect, and Heat UI's templates are sized for Heat UI's own panels - its settings element is
    /// 1000x60 and its header 200x30, against this project's 1200x80 and 1200x60.
    /// </remarks>
    public static void CopyGeometry(Transform target, Transform source)
    {
        if (target == null || source == null)
        {
            return;
        }

        var targetRect = target as RectTransform;
        var sourceRect = source as RectTransform;
        if (targetRect == null || sourceRect == null)
        {
            return;
        }

        targetRect.anchorMin = sourceRect.anchorMin;
        targetRect.anchorMax = sourceRect.anchorMax;
        targetRect.pivot = sourceRect.pivot;
        targetRect.anchoredPosition = sourceRect.anchoredPosition;
        targetRect.sizeDelta = sourceRect.sizeDelta;
        targetRect.localRotation = sourceRect.localRotation;
        targetRect.localScale = sourceRect.localScale;
    }

    /// <summary>
    /// Removes an object this bridge created or replaced.
    /// </summary>
    /// <remarks>
    /// Switched off first, because destruction is deferred to the end of the frame and until it runs the
    /// old object would still be drawn beside whatever replaced it. <c>Destroy</c> is the right call while
    /// playing, but it is not allowed in edit mode - Unity warns and does nothing - so the immediate form
    /// is used there. That is also what lets the audit exercise these helpers outside play mode.
    /// </remarks>
    private static void Discard(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        target.SetActive(false);
        Discard((UnityEngine.Object)target);
    }

    /// <summary>Same, for a component: nothing to switch off, since callers disable it first.</summary>
    private static void Discard(UnityEngine.Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(target);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    /// <summary>
    /// True when a value slot is Heat UI's switch, whose own rect lives in a nested prefab and so cannot
    /// be read from the element's file.
    /// </summary>
    /// <remarks>
    /// The lookup has to include inactive objects. A row is built as soon as the panel that owns it is
    /// built, which is while its own page is still closed, and an inactive search would miss the
    /// <c>SwitchManager</c> there - which silently sent the replacement down the "copy the slot's
    /// geometry" path and gave a numeric row an input box the size of the switch (70x40 at scale 0.8,
    /// the small half-drawn box on the 星海设置 page) instead of the 260x44 area the row reserves.
    /// </remarks>
    private static bool IsHeatSwitch(Transform slot)
    {
        return slot != null && slot.GetComponentInParent<Michsky.UI.Heat.SwitchManager>(includeInactive: true) != null;
    }

    /// <summary>
    /// Gives an instance built from a Heat UI template the geometry of the template it replaced, when the
    /// two really are different templates.
    /// </summary>
    /// <remarks>
    /// Both panels lay their content out with a layout group that controls neither width nor height, so
    /// every child keeps its own rect. Heat UI's settings element is 1000x60 and its header 200x30, while
    /// this project's row and group templates are 1200x80 and 1200x60 - so without this a converted row
    /// would come out narrower, shorter and mis-stacked. Heat UI's controls inside the row are anchored,
    /// so they follow the corrected size. A no-op when the instance already came from the original.
    /// </remarks>
    public static void AdoptGeometry(GameObject instance, GameObject originalTemplate, GameObject usedTemplate)
    {
        if (instance == null || originalTemplate == null || usedTemplate == null || originalTemplate == usedTemplate)
        {
            return;
        }

        CopyGeometry(instance.transform, originalTemplate.transform);
    }

    /// <summary>
    /// The object a row reserves for its control: Heat UI's switch when the row is one of its settings
    /// elements, otherwise this project's own <c>InputField</c> child.
    /// </summary>
    /// <remarks>
    /// A Heat UI settings element has no named value slot - its control is the switch itself - so a row
    /// built from one hands over the switch's own position for whatever replaces it.
    /// </remarks>
    public static Transform FindValueSlot(GameObject row)
    {
        if (row == null)
        {
            return null;
        }

        Michsky.UI.Heat.SwitchManager heatSwitch = FindSwitch(row);
        if (heatSwitch != null)
        {
            return heatSwitch.transform;
        }

        return row.transform.Find("InputField");
    }

    /// <summary>Removes a child by name, doing nothing when the row does not have one.</summary>
    public static void DestroyChild(GameObject root, string childName)
    {
        if (root == null)
        {
            return;
        }

        Transform child = root.transform.Find(childName);
        if (child != null)
        {
            UnityEngine.Object.Destroy(child.gameObject);
        }
    }

    /// <summary>
    /// Puts a staged Heat UI widget into a row's value slot, taking the slot's geometry so the row keeps
    /// its layout, and removes the control that was there.
    /// </summary>
    /// <remarks>
    /// This is how a row gets Heat UI's control. Heat UI ships no settings element with a text field, and
    /// its dropdown is a prefab with its own list rather than a drop-in replacement for
    /// <see cref="UnityEngine.UI.Dropdown"/>, so the widget goes in the slot the row already reserved -
    /// the same thing <c>EngineDropdown</c> does. Returns null, changing nothing, when the row has no
    /// slot or the template is not staged.
    /// </remarks>
    public static GameObject ReplaceValueSlot(GameObject row, Transform slot, string templateName, string name = null)
    {
        if (row == null || slot == null)
        {
            return null;
        }

        GameObject template = LoadTemplate(templateName);
        if (template == null)
        {
            return null;
        }

        GameObject instance = UnityEngine.Object.Instantiate(template, row.transform, false);
        instance.name = string.IsNullOrEmpty(name) ? template.name : name;
        var element = row.GetComponent<Michsky.UI.Heat.SettingsElement>();
        if (element != null) { element.onClick = new UnityEngine.Events.UnityEvent(); }

        if (IsHeatSwitch(slot))
        {
            // Heat UI's switch is a nested prefab, so its rect is not in the element's own file, and the
            // element's label reserves exactly 270px on its right for it. The replacement is therefore
            // placed from that measured inset - right-anchored and 260 wide, clear of the label - rather
            // than from a rect that cannot be read here.
            var rect = (RectTransform)instance.transform;
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-10f, 0f);
            rect.sizeDelta = new Vector2(260f, 44f);
        }
        else
        {
            CopyGeometry(instance.transform, slot);
        }

        // Switched off before being removed: destruction is deferred while playing, and until it runs the
        // old control would still be drawn beside the one that replaced it.
        Discard(slot.gameObject);
        return instance;
    }

    /// <summary>Same as <see cref="ReplaceValueSlot(GameObject, Transform, string, string)"/>, by child name.</summary>
    public static GameObject ReplaceValueSlot(GameObject row, string slotChildName, string templateName, string name = null)
    {
        return row == null ? null : ReplaceValueSlot(row, row.transform.Find(slotChildName), templateName, name);
    }

    /// <summary>
    /// Copies one of a row's own labels into its value slot under a new name.
    /// </summary>
    /// <remarks>
    /// Heat UI ships no read-only settings row, so the data panel makes one: the switch a Heat UI row
    /// arrives with is replaced by a second copy of that row's label, which is where the panel writes its
    /// values. Cloning the row's own label keeps the pack's font, size and colour without staging another
    /// template.
    /// <para>
    /// The copy has to lose the <c>LocalizedObject</c> and <c>UITextLocale</c> it was cloned with: the
    /// first would overwrite the value from the localization table, and it runs on enable, so both are
    /// switched off and destroyed before the copy is used. Neither has an Update of its own, so the value
    /// written in the same frame is the one that stays.
    /// </para>
    /// </remarks>
    public static GameObject CopyLabel(GameObject row, Transform slot, Transform source, string name)
    {
        if (row == null || slot == null || source == null)
        {
            return null;
        }

        GameObject copy = UnityEngine.Object.Instantiate(source.gameObject, row.transform, false);
        var element = row.GetComponent<Michsky.UI.Heat.SettingsElement>();
        if (element != null) { element.onClick = new UnityEngine.Events.UnityEvent(); }
        copy.name = name;

        if (IsHeatSwitch(slot))
        {
            // The value takes the right half of the row, mirroring where the row's own label stops: that
            // label is inset 270px on its right, so the value spans the right half less a small margin
            // and can hold values as long as an operating system string.
            var rect = (RectTransform)copy.transform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(10f, 0f);
            rect.offsetMax = new Vector2(-10f, 0f);
        }
        else
        {
            CopyGeometry(copy.transform, slot);
        }

        // This label holds data, not a locale id, so nothing may localize it or re-localize it later.
        foreach (Michsky.UI.Heat.LocalizedObject localized in copy.GetComponentsInChildren<Michsky.UI.Heat.LocalizedObject>(includeInactive: true))
        {
            localized.enabled = false;
            Discard(localized);
        }

        foreach (UITextLocale locale in copy.GetComponentsInChildren<UITextLocale>(includeInactive: true))
        {
            locale.enabled = false;
            Discard(locale);
        }
        RectTransform valueRect = (RectTransform)copy.transform;
        valueRect.anchorMin = new Vector2(0.5f, 0);
        valueRect.anchorMax = Vector2.one;
        valueRect.offsetMin = new Vector2(10, 0);
        valueRect.offsetMax = new Vector2(-20, 0);
        TMP_Text valueText = copy.GetComponent<TMP_Text>();
        if (valueText != null)
        {
            valueText.margin = Vector4.zero;
            valueText.alignment = TextAlignmentOptions.MidlineRight;
            valueText.enableWordWrapping = false;
            valueText.enableAutoSizing = true;
            valueText.fontSizeMin = 16;
            valueText.fontSizeMax = 24;
        }

        Discard(slot.gameObject);
        return copy;
    }
}

/// <summary>
/// Drives whichever toggle a settings row was built from: Heat UI's <c>SwitchManager</c> when the row
/// came from a Heat UI template, otherwise this project's <see cref="ToggleSwitch"/>.
/// </summary>
/// <remarks>
/// Both widgets raise their change event for programmatic updates as well as for clicks, so writes
/// through <see cref="IsOn"/> are wrapped in a silence flag. Without it, rendering a row would look
/// like the user had just flipped it and would write the same value back to the settings source.
/// </remarks>
public sealed class SettingsToggleBinding
{
    private readonly ToggleSwitch m_legacy;
    private readonly Michsky.UI.Heat.SwitchManager m_heat;
    private bool m_silent;

    public SettingsToggleBinding(GameObject row)
    {
        if (row == null)
        {
            return;
        }

        m_heat = HeatUiSkin.FindSwitch(row);
        if (m_heat != null)
        {
            HeatUiSkin.Activate(m_heat.gameObject);
            var element = row.GetComponent<Michsky.UI.Heat.SettingsElement>();
            if (element != null)
            {
                element.onClick = new UnityEngine.Events.UnityEvent();
                element.onClick.AddListener(m_heat.AnimateSwitch);
            }
            m_heat.onValueChanged.AddListener(OnChanged);
            return;
        }

        Transform legacy = row.transform.Find("ToggleSwitch");
        if (legacy != null)
        {
            m_legacy = legacy.GetComponent<ToggleSwitch>();
            if (m_legacy != null)
            {
                HeatUiSkin.Activate(m_legacy.gameObject);
                m_legacy.OnValueChanged.AddListener(OnChanged);
            }
        }
    }

    /// <summary>False when the row has no toggle at all, which means the template is not usable.</summary>
    public bool IsAvailable
    {
        get { return m_legacy != null || m_heat != null; }
    }

    /// <summary>True when Heat UI is the widget in use, as opposed to the project's own toggle.</summary>
    public bool IsHeatUi
    {
        get { return m_heat != null; }
    }

    public bool IsOn
    {
        get { return m_heat != null ? m_heat.isOn : (m_legacy != null && m_legacy.IsOn); }
        set
        {
            m_silent = true;
            try
            {
                if (m_heat != null)
                {
                    if (m_heat.isOn == value)
                    {
                        return;
                    }

                    // SetOn/SetOff play the animator and raise onValueChanged; UpdateUI only restores
                    // the state the animator already holds, so it is used when nothing changed.
                    if (value)
                    {
                        m_heat.SetOn();
                    }
                    else
                    {
                        m_heat.SetOff();
                    }
                }
                else if (m_legacy != null)
                {
                    m_legacy.SetIsOnWithoutNotify(value);
                }
            }
            finally
            {
                m_silent = false;
            }
        }
    }

    private void OnChanged(bool value)
    {
        if (!m_silent && Changed != null)
        {
            Changed(value);
        }
    }

    /// <summary>Raised when the user changes the value, never for a write through <see cref="IsOn"/>.</summary>
    public event Action<bool> Changed;
}

/// <summary>
/// Drives whichever text input a settings row was built from: Heat UI's TextMeshPro field when the row
/// came from a Heat UI template, otherwise this project's legacy <see cref="UnityEngine.UI.InputField"/>.
/// </summary>
public sealed class SettingsInputBinding
{
    private readonly UnityEngine.UI.InputField m_legacy;
    private readonly TMP_InputField m_tmp;

    public SettingsInputBinding(GameObject row)
    {
        if (row == null)
        {
            return;
        }

        m_tmp = HeatUiSkin.FindInputField(row);
        if (m_tmp != null)
        {
            HeatUiSkin.Activate(m_tmp.gameObject);
            m_tmp.onEndEdit.AddListener(OnEndEdit);
            return;
        }

        Transform legacy = row.transform.Find("InputField");
        if (legacy != null)
        {
            m_legacy = legacy.GetComponent<UnityEngine.UI.InputField>();
            if (m_legacy != null)
            {
                HeatUiSkin.Activate(m_legacy.gameObject);
                m_legacy.onEndEdit.AddListener(OnEndEdit);
            }
        }
    }

    /// <summary>False when the row has no input field at all, which means the template is not usable.</summary>
    public bool IsAvailable
    {
        get { return m_legacy != null || m_tmp != null; }
    }

    /// <summary>True when Heat UI is the widget in use, as opposed to the project's own input field.</summary>
    public bool IsHeatUi
    {
        get { return m_tmp != null; }
    }

    public string Text
    {
        get
        {
            if (m_tmp != null)
            {
                return m_tmp.text;
            }

            return m_legacy != null ? m_legacy.text : null;
        }
        set
        {
            if (m_tmp != null)
            {
                m_tmp.SetTextWithoutNotify(value);
            }
            else if (m_legacy != null)
            {
                m_legacy.SetTextWithoutNotify(value);
            }
        }
    }

    private void OnEndEdit(string text)
    {
        if (EndEdit != null)
        {
            EndEdit(text);
        }
    }

    /// <summary>Raised when the user finishes editing.</summary>
    public event Action<string> EndEdit;
}

/// <summary>
/// Drives a panel button that may be Heat UI's or this project's own.
/// </summary>
/// <remarks>
/// Both expose a click event, but only Heat UI's manager knows how to update the three copies of the
/// caption it cross-fades between, so the label goes through it when it is there.
/// </remarks>
public sealed class HeatUiButtonBinding
{
    private readonly UnityEngine.UI.Button m_button;
    private readonly Michsky.UI.Heat.ButtonManager m_manager;
    private string m_lastLabel;

    public HeatUiButtonBinding(UnityEngine.UI.Button button, Michsky.UI.Heat.ButtonManager manager)
    {
        m_button = button;
        m_manager = manager;
    }

    /// <summary>The click target, which is what the panels subscribe to.</summary>
    public UnityEngine.UI.Button Button
    {
        get { return m_button; }
    }

    /// <summary>True when Heat UI is the widget in use, as opposed to the project's own button.</summary>
    public bool IsHeatUi
    {
        get { return m_manager != null; }
    }

    public bool Interactable
    {
        get { return m_button != null && m_button.interactable; }
        set
        {
            if (m_button != null)
            {
                m_button.interactable = value;
            }

            if (m_manager != null)
            {
                m_manager.Interactable(value);
            }
        }
    }

    /// <summary>Sets the caption, through Heat UI's manager when the button is one of its own.</summary>
    public string Label
    {
        set
        {
            // ButtonManager.SetText forces multiple layout rebuilds even for identical text.
            if (m_lastLabel == value) return;
            m_lastLabel = value;
            if (m_manager != null)
            {
                m_manager.SetText(value);
                return;
            }

            HeatUiSkin.SetLabelText(m_button.gameObject, HeatUiSkin.HeatLabelChild, value);
        }
    }
}
