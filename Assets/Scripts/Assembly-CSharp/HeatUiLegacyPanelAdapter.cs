using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class HeatUiLegacyPanelAdapter : MonoBehaviour
{
    private sealed class TogglePair
    {
        public ToggleSwitch Legacy;
        public SettingsToggleBinding Heat;
    }

    private sealed class InputPair
    {
        public InputField Legacy;
        public SettingsInputBinding Heat;
        public TMP_InputField Field;
    }

    private sealed class ButtonPair
    {
        public UnityEngine.UI.Button Legacy;
        public UnityEngine.UI.Button Target;
        public Michsky.UI.Heat.ButtonManager Heat;
        public Text Caption;
    }

    private readonly HashSet<int> m_groups = new HashSet<int>();
    private readonly HashSet<int> m_rows = new HashSet<int>();
    private readonly HashSet<int> m_buttons = new HashSet<int>();
    private readonly HashSet<int> m_switches = new HashSet<int>();
    private readonly HashSet<int> m_fields = new HashSet<int>();
    private readonly List<TogglePair> m_toggles = new List<TogglePair>();
    private readonly List<InputPair> m_inputs = new List<InputPair>();
    private readonly List<ButtonPair> m_buttonPairs = new List<ButtonPair>();
    private readonly Dictionary<Transform, GameObject> m_visuals = new Dictionary<Transform, GameObject>();
    private readonly List<Transform> m_expired = new List<Transform>();
    private readonly Dictionary<Text, TMP_Text> m_labels = new Dictionary<Text, TMP_Text>();
    private readonly List<Transform> m_pendingGroups = new List<Transform>();
    private static readonly System.Diagnostics.Stopwatch s_budget = new System.Diagnostics.Stopwatch();
    private Transform m_content;
    private float m_nextScan;
    private float m_nextSync;
    private bool m_structureDirty = true;
    private bool m_unfinished;
    private bool m_nativeHeatRows;

#if UNITY_EDITOR
    /// <summary>
    /// Editor-only breakdown of where a styling pass spends its time, in milliseconds, keyed by phase.
    /// </summary>
    /// <remarks>
    /// Replacing a page's rows with Heat UI ones is expensive enough to be felt as a hitch, and the cost
    /// is spread over several quite different things - instantiating the templates, filling the labels,
    /// swapping the value slot, building the bindings. This is what says which of them to attack; it
    /// compiles out of a player build.
    /// </remarks>
    public static readonly Dictionary<string, double> Profile = new Dictionary<string, double>();

    private static readonly System.Diagnostics.Stopwatch s_clock = System.Diagnostics.Stopwatch.StartNew();
    private static double s_mark;

    public static void ResetProfile()
    {
        Profile.Clear();
        s_mark = s_clock.Elapsed.TotalMilliseconds;
    }

    private static void Mark(string phase)
    {
        double now = s_clock.Elapsed.TotalMilliseconds;
        double previous;
        Profile[phase] = (Profile.TryGetValue(phase, out previous) ? previous : 0.0) + now - s_mark;
        s_mark = now;
    }
#else
    private static void Mark(string phase)
    {
    }

    public static void ResetProfile()
    {
    }
#endif

    public void Initialize(Transform content)
    {
        m_content = content;
        INSettingsInterface settings = GetComponent<INSettingsInterface>();
        m_nativeHeatRows = settings != null && settings.UsesHeatRows;
        Watch(content);
    }

    public void InvalidateStyles() => m_structureDirty = true;

    private void Watch(Transform target)
    {
        if (target == null) return;
        HeatUiHierarchyWatcher watcher = target.GetComponent<HeatUiHierarchyWatcher>();
        if (watcher == null) watcher = target.gameObject.AddComponent<HeatUiHierarchyWatcher>();
        watcher.Owner = this;
    }

    /// <summary>
    /// Styles everything the panel has built so far, whether or not this is the page on screen, and
    /// reports whether it found anything new to do.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The app interface calls this while the settings panel is up, so that clicking a category is not
    /// the thing that builds its Heat UI rows. A page's legacy rows are built by the panel shortly after
    /// it appears, but this component only sees <c>LateUpdate</c> while its own page is the open one, so
    /// without a warm-up the entire pass for a page - 更多设置 is 208 rows, each replaced by a Heat UI
    /// element carrying its own labels, switch or input field and bindings - landed on the frame its
    /// category was clicked. That is the hitch.
    /// </para>
    /// <para>
    /// Safe to call while the page is closed: the styling decisions read each control's own
    /// <c>activeSelf</c>, which is what the legacy panel sets, and never the page's visibility. The
    /// return value is what lets the caller keep calling until the panel has finished building.
    /// </para>
    /// </remarks>
    public bool WarmUp()
    {
        return WarmUp(float.PositiveInfinity);
    }

    /// <summary>
    /// A styling pass that stops adding rows once <paramref name="budgetMs"/> has been spent on them, so a
    /// caller that has to keep a frame short can spread a long page over several frames.
    /// </summary>
    /// <remarks>
    /// The pass reports what it did through its return value either way, so a caller that gets a budgeted
    /// partial pass back keeps calling until an empty one.
    /// </remarks>
    public bool WarmUp(float budgetMs)
    {
        return m_content != null && Scan(budgetMs);
    }

    private void LateUpdate()
    {
        if (m_content == null || !m_content.gameObject.activeInHierarchy)
        {
            return;
        }

        // Normally the warm-up has already done this; the repeat covers a panel that builds its rows over
        // several frames, and keeps the Heat UI rows mirroring rows the panel shows or hides later.
        if (Time.unscaledTime >= m_nextScan)
        {
            m_nextScan = Time.unscaledTime + 0.25f;
            // The fallback on the selected page has the same limit as background warm-up.
            Scan(3f);
        }

        if (Time.unscaledTime < m_nextSync) return;
        m_nextSync = Time.unscaledTime + 0.1f;

        foreach (KeyValuePair<Text, TMP_Text> pair in m_labels)
        {
            if (pair.Key != null && pair.Value != null && pair.Key.text != pair.Value.text)
            {
                pair.Value.text = pair.Key.text;
            }
        }
        foreach (TogglePair pair in m_toggles)
        {
            if (pair.Legacy != null && pair.Heat.IsOn != pair.Legacy.IsOn)
            {
                pair.Heat.IsOn = pair.Legacy.IsOn;
            }
        }
        foreach (InputPair pair in m_inputs)
        {
            if (pair.Legacy != null && pair.Field != null)
            {
                if (pair.Field.interactable != pair.Legacy.interactable) pair.Field.interactable = pair.Legacy.interactable;
                if (pair.Field.readOnly != pair.Legacy.readOnly) pair.Field.readOnly = pair.Legacy.readOnly;
            }
            if (pair.Legacy != null && pair.Field != null && !pair.Field.isFocused &&
                pair.Heat.Text != pair.Legacy.text)
            {
                pair.Heat.Text = pair.Legacy.text;
            }
        }
        foreach (ButtonPair pair in m_buttonPairs)
        {
            if (pair.Legacy == null || pair.Heat == null)
            {
                continue;
            }
            if (pair.Heat.gameObject.activeSelf != pair.Legacy.gameObject.activeSelf)
                pair.Heat.gameObject.SetActive(pair.Legacy.gameObject.activeSelf);
            if (pair.Target != null && pair.Target.interactable != pair.Legacy.interactable)
            {
                pair.Target.interactable = pair.Legacy.interactable;
                pair.Heat.Interactable(pair.Legacy.interactable);
            }
            if (pair.Caption != null && pair.Heat.buttonText != pair.Caption.text)
            {
                pair.Heat.SetText(pair.Caption.text);
            }
        }
    }

    /// <summary>
    /// One styling pass: drops what the panel has removed, then replaces whatever it has added since the
    /// last pass with Heat UI's own widgets. Runs from <see cref="WarmUp"/> and, for late arrivals, from
    /// <see cref="LateUpdate"/>. Returns whether the pass added or dropped anything, or was stopped part way
    /// by its budget - either way, whether there is a reason to call it again.
    /// </summary>
    private bool Scan()
    {
        return Scan(float.PositiveInfinity);
    }

    private bool Scan(float budgetMs)
    {
        // INSettingsInterface already creates Heat UI rows and buttons directly; no conversion is needed.
        if (m_nativeHeatRows) return false;
        Mark("scan: idle");
        int before = m_visuals.Count;
        m_expired.Clear();
        foreach (KeyValuePair<Transform, GameObject> pair in m_visuals)
        {
            if (pair.Key == null)
            {
                if (pair.Value != null) { Destroy(pair.Value); }
                m_expired.Add(pair.Key);
            }
            else if (pair.Value != null)
            {
                // The legacy row's own active state, not the page's: the replacement mirrors a row the
                // panel has hidden, and must not be hidden itself just because the page is closed.
                if (pair.Value.activeSelf != pair.Key.gameObject.activeSelf)
                    pair.Value.SetActive(pair.Key.gameObject.activeSelf);
            }
        }
        foreach (Transform expired in m_expired) { m_visuals.Remove(expired); }
        if (m_expired.Count > 0) m_structureDirty = true;
        // Visibility still follows the legacy controls, but discovery and typography only run on changes.
        if (!m_structureDirty && !m_unfinished) return false;
        m_toggles.RemoveAll(pair => pair.Legacy == null);
        m_inputs.RemoveAll(pair => pair.Legacy == null);
        m_buttonPairs.RemoveAll(pair => pair.Legacy == null);
        Mark("scan: prune");
        bool unfinished = StyleGroups(budgetMs);
        m_unfinished = unfinished;
        Mark("scan: groups + rows");
        if (!unfinished) StyleFreeControls();
        Mark("scan: free controls");
        if (!unfinished) StyleButtons();
        Mark("scan: buttons");
        m_structureDirty = false;
        return unfinished || before != m_visuals.Count;
    }

    /// <summary>
    /// Replaces the panel's own groups and rows with Heat UI's, stopping once <paramref name="budgetMs"/> has
    /// been spent on rows. Returns whether it stopped with rows still to reach.
    /// </summary>
    private bool StyleGroups(float budgetMs)
    {
        // Collect the groups before walking them: styling a row inserts its replacement into the content and
        // moves it to the row's place, which shifts the content's children under an index walk and leaves
        // the rest of the page for the next pass - five passes for 更多设置's 208 rows, every one of them a
        // frame long enough to see.
        m_pendingGroups.Clear();
        int count = m_content.childCount;
        for (int index = 0; index < count; index++)
        {
            Transform child = m_content.GetChild(index);
            if (child.name.StartsWith("SettingsGroup_") && child.Find("GroupName") != null)
            {
                m_pendingGroups.Add(child);
            }
        }

        s_budget.Restart();
        foreach (Transform group in m_pendingGroups)
        {
            if (!group.name.StartsWith("SettingsGroup_") || group.Find("GroupName") == null)
            {
                continue;
            }

            if (m_groups.Add(group.GetInstanceID()))
            {
                Watch(group);
                HeatUiSkin.ApplyTypography(group.gameObject);
                GameObject headerTemplate = HeatUiSkin.LoadTemplate(HeatUiSkin.HeaderTemplate);
                if (headerTemplate == null)
                {
                    return false;
                }
                GameObject header = Instantiate(headerTemplate, m_content, false);
                m_visuals[group] = header;
                header.transform.SetSiblingIndex(group.GetSiblingIndex());
                header.name = "HeatHeader_" + group.name;
                Text legacyTitle = group.Find("GroupName").GetComponent<Text>();
                TMP_Text heatTitle = header.GetComponent<TMP_Text>();
                if (legacyTitle != null && heatTitle != null)
                {
                    heatTitle.text = legacyTitle.text;
                    m_labels[legacyTitle] = heatTitle;
                }
                Michsky.UI.Heat.LocalizedObject locale = header.GetComponent<Michsky.UI.Heat.LocalizedObject>();
                if (locale != null)
                {
                    locale.enabled = false;
                }
                bool fullySupported = true;
                foreach (Transform child in group)
                {
                    if (child.name == "GroupName" || !child.gameObject.activeSelf) { continue; }
                    ToggleSwitch toggle = child.Find("ToggleSwitch")?.GetComponent<ToggleSwitch>();
                    InputField input = child.Find("InputField")?.GetComponent<InputField>();
                    fullySupported &= child.name.StartsWith("SettingsItem_") &&
                        ((toggle != null && toggle.gameObject.activeSelf) ||
                         (input != null && input.gameObject.activeSelf));
                }
                if (fullySupported) { Hide(group.gameObject); }
                else { Hide(group.Find("GroupName").gameObject); }
            }

            foreach (Transform row in group)
            {
                if (!row.name.StartsWith("SettingsItem_") || !row.gameObject.activeSelf ||
                    !m_rows.Add(row.GetInstanceID()))
                {
                    continue;
                }
                StyleRow(row);
                if (s_budget.Elapsed.TotalMilliseconds >= budgetMs)
                {
                    // Out of this frame's slice, with rows still to reach: the caller has to call again next
                    // frame, and is told so by the return value rather than by whether this pass happened to
                    // add anything - a pass cut short before it could add a row is still unfinished work.
                    // Rows it has not reached yet keep the panel's own look for now.
                    return true;
                }
            }
        }
        return false;
    }

    private void StyleRow(Transform row)
    {
        HeatUiSkin.ApplyTypography(row.gameObject);
        GameObject template = HeatUiSkin.LoadTemplate(HeatUiSkin.SwitchElementTemplate);
        if (template == null)
        {
            return;
        }

        ToggleSwitch legacyToggle = row.Find("ToggleSwitch")?.GetComponent<ToggleSwitch>();
        InputField legacyInput = row.Find("InputField")?.GetComponent<InputField>();
        bool hasToggle = legacyToggle != null && legacyToggle.gameObject.activeSelf;
        bool hasInput = legacyInput != null && legacyInput.gameObject.activeSelf;
        Slider legacySlider = row.GetComponentInChildren<Slider>(true);
        string key = row.name.Substring("SettingsItem_".Length);
        bool useSlider = legacySlider != null || key == "BackgroundVolume" || key == "BackgroundOpacity" ||
            key == "BuildVolume" || key == "RunVolume" || key == "SoundVolume" || key == "EntryMusicVolume";
        if (useSlider)
        {
            template = HeatUiSkin.LoadTemplate("Settings Element (Slider)") ?? template;
        }
        if (!hasToggle && !hasInput)
        {
            return;
        }

        GameObject heatRow = Instantiate(template, m_content, false);
        Mark("row: instantiate");
        string optionKey = row.name.Substring("SettingsItem_".Length);
        UITextLocale originalLocale = row.Find("Name")?.GetComponent<UITextLocale>();
        if (originalLocale != null && !string.IsNullOrEmpty(originalLocale.ID)) { optionKey = originalLocale.ID; }
        m_visuals[row] = heatRow;
        GameObject heading;
        if (m_visuals.TryGetValue(row.parent, out heading) && heading != null)
        {
            heatRow.transform.SetSiblingIndex(heading.transform.GetSiblingIndex() + row.GetSiblingIndex());
        }
        Hide(row.gameObject);
        heatRow.name = "HeatRow_" + row.name;
        Text caption = row.Find("Name")?.GetComponent<Text>();
        Transform label = HeatUiSkin.FindLabel(heatRow, HeatUiSkin.HeatLabelChild);
        if (caption != null && label != null)
        {
            TMP_Text text = label.GetComponent<TMP_Text>();
            if (text != null)
            {
                text.text = caption.text;
                m_labels[caption] = text;
            }
        }
        HeatUiOptionHelp.Bind(heatRow, optionKey, caption != null ? caption.text : optionKey);
        Mark("row: label + help");
        Michsky.UI.Heat.LocalizedObject locale = heatRow.GetComponent<Michsky.UI.Heat.LocalizedObject>();
        if (locale != null)
        {
            locale.enabled = false;
        }

        Slider heatSlider = heatRow.GetComponentInChildren<Slider>(true);
        if (useSlider && heatSlider != null)
        {
            if (legacyInput != null) { m_fields.Add(legacyInput.GetInstanceID()); }
            heatSlider.gameObject.AddComponent<HeatUiSliderBinding>().Initialize(legacyInput, legacySlider);
        }
        else if (hasToggle)
        {
            m_switches.Add(legacyToggle.GetInstanceID());
            SettingsToggleBinding binding = new SettingsToggleBinding(heatRow);
            binding.IsOn = legacyToggle.IsOn;
            binding.Changed += value => legacyToggle.IsOn = value;
            m_toggles.Add(new TogglePair { Legacy = legacyToggle, Heat = binding });
        }
        else
        {
            m_fields.Add(legacyInput.GetInstanceID());
            HeatUiSkin.ReplaceValueSlot(heatRow, HeatUiSkin.FindValueSlot(heatRow),
                HeatUiSkin.InputFieldTemplate, "HeatInputField");
            SettingsInputBinding binding = new SettingsInputBinding(heatRow);
            binding.Text = legacyInput.text;
            binding.EndEdit += value =>
            {
                legacyInput.text = value;
                legacyInput.onEndEdit.Invoke(value);
            };
            m_inputs.Add(new InputPair
            {
                Legacy = legacyInput,
                Heat = binding,
                Field = HeatUiSkin.FindInputField(heatRow)
            });
            ConfigureInput(legacyInput, HeatUiSkin.FindInputField(heatRow));
        }
        Mark("row: value control");
    }

    private void StyleFreeControls()
    {
        GameObject switchTemplate = HeatUiSkin.LoadTemplate(HeatUiSkin.SwitchElementTemplate);
        GameObject inputTemplate = HeatUiSkin.LoadTemplate(HeatUiSkin.InputFieldTemplate);
        if (switchTemplate != null)
        {
            Michsky.UI.Heat.SwitchManager source = HeatUiSkin.FindSwitch(switchTemplate);
            foreach (ToggleSwitch toggle in m_content.GetComponentsInChildren<ToggleSwitch>(true))
            {
                Watch(toggle.transform);
                if (!toggle.gameObject.activeSelf || IsHiddenGroupRow(toggle.transform) || source == null ||
                    !m_switches.Add(toggle.GetInstanceID()))
                {
                    continue;
                }

                GameObject visible = Instantiate(source.gameObject, toggle.transform.parent, false);
                m_visuals[toggle.transform] = visible;
                visible.transform.SetSiblingIndex(toggle.transform.GetSiblingIndex());
                HeatUiSkin.CopyGeometry(visible.transform, toggle.transform);
                SettingsToggleBinding binding = new SettingsToggleBinding(visible);
                binding.IsOn = toggle.IsOn;
                binding.Changed += value => toggle.IsOn = value;
                m_toggles.Add(new TogglePair { Legacy = toggle, Heat = binding });
                Hide(toggle.gameObject);
            }
        }

        if (inputTemplate != null)
        {
            foreach (InputField input in m_content.GetComponentsInChildren<InputField>(true))
            {
                Watch(input.transform);
                if (!input.gameObject.activeSelf || IsHiddenGroupRow(input.transform) ||
                    !m_fields.Add(input.GetInstanceID()))
                {
                    continue;
                }

                GameObject visible = Instantiate(inputTemplate, input.transform.parent, false);
                m_visuals[input.transform] = visible;
                visible.transform.SetSiblingIndex(input.transform.GetSiblingIndex());
                HeatUiSkin.CopyGeometry(visible.transform, input.transform);
                TMP_InputField field = HeatUiSkin.FindInputField(visible);
                if (field == null)
                {
                    Destroy(visible);
                    continue;
                }
                field.SetTextWithoutNotify(input.text);
                ConfigureInput(input, field);
                field.onValueChanged.AddListener(value => input.text = value);
                field.onEndEdit.AddListener(input.onEndEdit.Invoke);
                field.onSubmit.AddListener(input.onSubmit.Invoke);
                m_inputs.Add(new InputPair
                {
                    Legacy = input,
                    Heat = new SettingsInputBinding(visible),
                    Field = field
                });
                Hide(input.gameObject);
            }
        }
    }

    private bool IsHiddenGroupRow(Transform control)
    {
        // Preview hides the page above m_content; only a hidden legacy row inside the list is excluded.
        for (Transform parent = control.parent; parent != null && parent != m_content.parent; parent = parent.parent)
        {
            CanvasGroup visibility = parent.GetComponent<CanvasGroup>();
            if (visibility != null && visibility.alpha == 0)
            {
                return true;
            }
        }
        return false;
    }

    private void StyleButtons()
    {
        GameObject template = HeatUiSkin.LoadTemplate(HeatUiSkin.ButtonTemplate);
        if (template == null)
        {
            return;
        }
        foreach (UnityEngine.UI.Button button in m_content.GetComponentsInChildren<UnityEngine.UI.Button>(true))
        {
            // Inactive-inclusive: a Heat UI widget's own buttons sit under it whether or not its page is
            // the open one, and the warm-up styles pages while they are closed.
            if (button.GetComponentInParent<Michsky.UI.Heat.SettingsElement>(true) != null ||
                button.GetComponentInParent<Michsky.UI.Heat.SwitchManager>(true) != null ||
                button.GetComponentInParent<Michsky.UI.Heat.HorizontalSelector>(true) != null ||
                button.GetComponentInParent<Michsky.UI.Heat.SliderManager>(true) != null ||
                button.GetComponentInParent<Michsky.UI.Heat.Dropdown>(true) != null ||
                button.GetComponentInParent<Michsky.UI.Heat.ButtonManager>(true) != null ||
                button.GetComponentInParent<Michsky.UI.Heat.PanelButton>(true) != null)
            {
                continue;
            }
            Watch(button.transform);
            if (!button.gameObject.activeSelf || !m_buttons.Add(button.GetInstanceID()) ||
                button.GetComponent<Michsky.UI.Heat.ButtonManager>() != null ||
                button.GetComponent<Michsky.UI.Heat.PanelButton>() != null)
            {
                continue;
            }

            GameObject heatButton = Instantiate(template, button.transform.parent, false);
            heatButton.name = button.name + " (Heat UI)";
            HeatUiSkin.CopyGeometry(heatButton.transform, button.transform);
            heatButton.transform.SetSiblingIndex(button.transform.GetSiblingIndex());
            LayoutElement legacyLayout = button.GetComponent<LayoutElement>();
            if (legacyLayout != null)
            {
                LayoutElement heatLayout = heatButton.GetComponent<LayoutElement>();
                if (heatLayout == null) { heatLayout = heatButton.AddComponent<LayoutElement>(); }
                heatLayout.ignoreLayout = legacyLayout.ignoreLayout;
                heatLayout.minWidth = legacyLayout.minWidth;
                heatLayout.minHeight = legacyLayout.minHeight;
                heatLayout.preferredWidth = legacyLayout.preferredWidth;
                heatLayout.preferredHeight = legacyLayout.preferredHeight;
                heatLayout.flexibleWidth = legacyLayout.flexibleWidth;
                heatLayout.flexibleHeight = legacyLayout.flexibleHeight;
            }
            var manager = heatButton.GetComponent<Michsky.UI.Heat.ButtonManager>();
            if (manager == null)
            {
                Destroy(heatButton);
                continue;
            }
            Text caption = button.GetComponentInChildren<Text>(true);
            manager.useLocalization = false;
            manager.autoFitContent = false;
            manager.SetText(caption != null ? caption.text : button.name);
            HeatUiSkin.CopyGeometry(heatButton.transform, button.transform);
            UnityEngine.UI.Button target = heatButton.GetComponent<UnityEngine.UI.Button>();
            if (target == null)
            {
                target = heatButton.AddComponent<UnityEngine.UI.Button>();
            }
            manager.onClick.AddListener(button.onClick.Invoke);
            m_visuals[button.transform] = heatButton;
            target.interactable = button.interactable;
            manager.Interactable(button.interactable);
            HeatUiSkin.CompactActionButton(heatButton);
            Hide(button.gameObject);
            m_buttonPairs.Add(new ButtonPair { Legacy = button, Target = target, Heat = manager, Caption = caption });
        }
    }

    private static void ConfigureInput(InputField legacy, TMP_InputField field)
    {
        if (field == null) { return; }
        field.contentType = (TMP_InputField.ContentType)(int)legacy.contentType;
        field.inputType = (TMP_InputField.InputType)(int)legacy.inputType;
        field.keyboardType = legacy.keyboardType;
        TMP_InputField.CharacterValidation validation;
        if (System.Enum.TryParse(legacy.characterValidation.ToString(), out validation))
        {
            field.characterValidation = validation;
        }
        if (legacy.onValidateInput != null)
        {
            field.onValidateInput = (text, index, character) => legacy.onValidateInput(text, index, character);
        }
        field.lineType = (TMP_InputField.LineType)(int)legacy.lineType;
        field.characterLimit = legacy.characterLimit;
        field.interactable = legacy.interactable;
        field.readOnly = legacy.readOnly;
        Text placeholder = legacy.placeholder as Text;
        TMP_Text heatPlaceholder = field.placeholder as TMP_Text;
        if (placeholder != null && heatPlaceholder != null) { heatPlaceholder.text = placeholder.text; }
    }

    private static void Hide(GameObject source)
    {
        CanvasGroup group = source.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = source.AddComponent<CanvasGroup>();
        }
        group.alpha = 0;
        group.blocksRaycasts = false;
        LayoutElement element = source.GetComponent<LayoutElement>();
        if (element == null)
        {
            element = source.AddComponent<LayoutElement>();
        }
        element.ignoreLayout = true;
    }
}
