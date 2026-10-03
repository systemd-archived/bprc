using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Batch-mode harness that verifies the Heat UI conversion of the console panels.
/// </summary>
/// <remarks>
/// <para>
/// The panels build their contents at runtime from templates, so "is the panel using Heat UI" is a
/// question about code paths and staged prefabs rather than about a scene that can be looked at. This
/// harness answers it without opening a scene:
/// </para>
/// <code>
/// Unity.exe -batchmode -nographics -quit -projectPath . -executeMethod HeatUiAudit.RunAll -logFile Logs/heatui-audit.log
/// </code>
/// <para>
/// Results go to <c>Builds/QA/heat-ui.txt</c>; a failing check throws, which makes the editor exit
/// non-zero.
/// </para>
/// </remarks>
public static class HeatUiAudit
{
    private const string ReportDirectory = "Builds/QA";
    private const string ReportPath = ReportDirectory + "/heat-ui.txt";

    private static readonly List<string> Results = new List<string>();
    private static int failures;

    private static void Check(bool condition, string message)
    {
        Results.Add((condition ? "PASS " : "FAIL ") + message);
        if (!condition)
        {
            failures++;
            Debug.LogError("HeatUiAudit FAIL: " + message);
        }
    }

    private static void Note(string message)
    {
        Results.Add("NOTE " + message);
    }

    /// <summary>Every staged template, and what the panels expect to find inside it.</summary>
    private static readonly string[] Templates =
    {
        HeatUiSkin.HeaderTemplate,
        HeatUiSkin.SwitchElementTemplate,
        HeatUiSkin.DropdownElementTemplate,
        HeatUiSkin.InputFieldTemplate,
        HeatUiSkin.DropdownTemplate,
        HeatUiSkin.ButtonTemplate,
    };

    /// <summary>Runs every check and writes the report.</summary>
    public static void RunAll()
    {
        Results.Clear();
        failures = 0;

        try
        {
            CheckAssemblyIsolation();
            CheckTemplatesAreStaged();
            CheckTemplateContents();
            CheckBridgeBindsToHeatUi();
            CheckRowValueSlotReplacement();
            CheckDataRowValueLabel();
            CheckDemoPanelLayout();
            CheckPanelSkin();
            CheckButtonSwap();
            CheckLocaleHandlesTextMeshPro();
        }
        finally
        {
            WriteReport();
        }

        if (failures > 0)
        {
            throw new Exception("HeatUiAudit: " + failures + " check(s) failed. See " + ReportPath + ".");
        }
    }

    /// <summary>
    /// Heat UI has to be its own assembly. The decompiled game declares Sprite, Button and friends in
    /// the global namespace, and in one assembly those shadow UnityEngine's types and the pack does not
    /// compile at all.
    /// </summary>
    private static void CheckAssemblyIsolation()
    {
        var heatAssembly = typeof(Michsky.UI.Heat.SwitchManager).Assembly;
        Check(heatAssembly.GetName().Name == "HeatUI", "Heat UI compiles into the HeatUI assembly (was " + heatAssembly.GetName().Name + ")");

        var gameAssembly = typeof(HeatUiSkin).Assembly;
        Check(gameAssembly != heatAssembly, "the bridge lives in a different assembly from Heat UI");

        Check(typeof(Michsky.UI.Heat.SwitchManager).FullName.StartsWith("Michsky.UI.Heat."), "Heat UI types stay inside their namespace");
    }

    /// <summary>
    /// The templates the panels instantiate are reached with Resources.Load, so they must be staged
    /// under Assets/Resources/HeatUI by Tools/HeatUI/Stage-Resources.ps1.
    /// </summary>
    private static void CheckTemplatesAreStaged()
    {
        foreach (string name in Templates)
        {
            var template = Resources.Load<GameObject>(HeatUiSkin.ResourceFolder + "/" + name);
            Check(template != null, "template staged: " + name);
            if (template != null)
            {
                Check(template.GetComponent<RectTransform>() != null, "template is a UI object: " + name);
            }
        }
    }

    /// <summary>Each template carries the component the bridge looks for inside it.</summary>
    private static void CheckTemplateContents()
    {
        Check(Find<Michsky.UI.Heat.SwitchManager>(HeatUiSkin.SwitchElementTemplate) != null,
            HeatUiSkin.SwitchElementTemplate + " contains a SwitchManager");
        Check(Find<Michsky.UI.Heat.SettingsElement>(HeatUiSkin.SwitchElementTemplate) != null,
            HeatUiSkin.SwitchElementTemplate + " contains a SettingsElement");
        Check(Find<TMP_Text>(HeatUiSkin.SwitchElementTemplate) != null,
            HeatUiSkin.SwitchElementTemplate + " has a TextMeshPro label");
        Check(Find<TMP_Text>(HeatUiSkin.HeaderTemplate) != null,
            HeatUiSkin.HeaderTemplate + " has a TextMeshPro label");
        Check(Find<TMP_InputField>(HeatUiSkin.InputFieldTemplate) != null,
            HeatUiSkin.InputFieldTemplate + " contains a TMP_InputField");
        Check(Find<Michsky.UI.Heat.Dropdown>(HeatUiSkin.DropdownTemplate) != null,
            HeatUiSkin.DropdownTemplate + " contains a Heat UI Dropdown");
        Check(Find<Michsky.UI.Heat.ButtonManager>(HeatUiSkin.ButtonTemplate) != null,
            HeatUiSkin.ButtonTemplate + " contains a ButtonManager");
    }

    /// <summary>
    /// The bindings the settings panel uses have to pick Heat UI when it is there, because that is what
    /// decides whether a row is a Heat UI switch or the project's own toggle.
    /// </summary>
    private static void CheckBridgeBindsToHeatUi()
    {
        var row = Instantiate(HeatUiSkin.SwitchElementTemplate);
        if (row != null)
        {
            try
            {
                Check(HeatUiSkin.FindLabel(row, HeatUiSkin.LegacyNameChild) != null, "bridge finds the row label by falling back to Heat UI's \"Text\" child");

                var locale = HeatUiSkin.EnsureLocale(row, HeatUiSkin.LegacyNameChild);
                Check(locale != null, "bridge attaches a UITextLocale to a Heat UI row");

                var toggle = new SettingsToggleBinding(row);
                Check(toggle.IsAvailable, "toggle binding finds a widget on a Heat UI row");
                Check(toggle.IsHeatUi, "toggle binding prefers Heat UI's SwitchManager over the project's ToggleSwitch");

                bool raised = false;
                toggle.Changed += _ => raised = true;
                toggle.IsOn = !toggle.IsOn;
                Check(!raised, "writing IsOn does not look like a user change");

                toggle.IsOn = !toggle.IsOn;
                Check(!raised, "writing IsOn twice is still silent");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(row);
            }
        }
        else
        {
            Check(false, "could not instantiate " + HeatUiSkin.SwitchElementTemplate + " for the binding checks");
        }

        var field = Instantiate(HeatUiSkin.InputFieldTemplate);
        if (field != null)
        {
            try
            {
                var input = new SettingsInputBinding(field);
                Check(input.IsAvailable, "input binding finds a widget on a Heat UI field");
                Check(input.IsHeatUi, "input binding prefers Heat UI's TextMeshPro field");

                input.Text = "42";
                Check(input.Text == "42", "input binding round-trips text");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(field);
            }
        }
        else
        {
            Check(false, "could not instantiate " + HeatUiSkin.InputFieldTemplate + " for the binding checks");
        }

        var dropdown = Instantiate(HeatUiSkin.DropdownTemplate);
        if (dropdown != null)
        {
            try
            {
                Check(HeatUiSkin.FindDropdown(dropdown) != null, "bridge finds Heat UI's Dropdown");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(dropdown);
            }
        }
    }

    /// <summary>
    /// Rows that need a text field or a dropdown start from Heat UI's settings element and replace its
    /// switch, so what has to hold is that the replacement lands in the switch's place, that the switch
    /// stops being drawn, and that the row's binding then finds the replacement.
    /// </summary>
    private static void CheckRowValueSlotReplacement()
    {
        var row = Instantiate(HeatUiSkin.SwitchElementTemplate);
        if (row == null)
        {
            return;
        }

        try
        {
            Transform slot = HeatUiSkin.FindValueSlot(row);
            Check(slot != null, "a Heat UI row offers its switch as the value slot");
            if (slot == null)
            {
                return;
            }

            GameObject placed = HeatUiSkin.ReplaceValueSlot(row, slot, HeatUiSkin.InputFieldTemplate, "HeatInputField");
            Check(placed != null, "the value slot accepts the staged input field");
            Check(slot == null || !slot.gameObject.activeSelf, "the control the slot replaced stops being drawn");

            var input = new SettingsInputBinding(row);
            Check(input.IsAvailable && input.IsHeatUi, "the row then binds to the placed Heat UI input field");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(row);
        }
    }

    /// <summary>
    /// The data panel makes its read-only row out of Heat UI's settings element by copying the row's own
    /// label over the switch. What has to hold is that the copy exists, that nothing is left on it that
    /// would localize the value away, and that a value written the way the panel writes it lands.
    /// </summary>
    private static void CheckDataRowValueLabel()
    {
        var row = Instantiate(HeatUiSkin.SwitchElementTemplate);
        if (row == null)
        {
            return;
        }

        try
        {
            Transform heatSwitch = HeatUiSkin.FindSwitch(row) != null ? HeatUiSkin.FindSwitch(row).transform : null;
            Transform label = HeatUiSkin.FindLabel(row, HeatUiSkin.HeatLabelChild);
            Check(heatSwitch != null && label != null, "a Heat UI row offers both a switch and a label to copy");
            if (heatSwitch == null || label == null)
            {
                return;
            }

            GameObject value = HeatUiSkin.CopyLabel(row, heatSwitch, label, "Value");
            Check(value != null, "the data row's value label is built from the row's own label");
            if (value == null)
            {
                return;
            }

            Check(value.GetComponentInChildren<TMP_Text>(true) != null, "the value label is a TextMeshPro label");
            Check(heatSwitch == null || !heatSwitch.gameObject.activeSelf, "the switch the value label replaced stops being drawn");

            bool liveLocalizer = false;
            foreach (var localized in value.GetComponentsInChildren<Michsky.UI.Heat.LocalizedObject>(true))
            {
                if (localized != null && localized.enabled)
                {
                    liveLocalizer = true;
                }
            }

            Check(!liveLocalizer, "nothing is left on the value label that could localize the value away");

            HeatUiSkin.SetLabelText(value, "Value", "12345");
            TMP_Text text = value.GetComponentInChildren<TMP_Text>(true);
            Check(text != null && text.text == "12345", "a value written the way the panel writes it lands in the label");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(row);
        }
    }

    private static void CheckDemoPanelLayout()
    {
        GameObject layout = Resources.Load<GameObject>("HeatUI/Settings Layout");
        GameObject category = Resources.Load<GameObject>("HeatUI/Button (Panel Alt)");
        Check(layout != null && !layout.activeSelf, "the desktop example Settings panel is staged inactive for safe customization");
        Check(category != null && category.GetComponent<Michsky.UI.Heat.PanelButton>() != null,
            "the example category button is staged with its Heat UI panel controller");
        if (layout == null)
        {
            return;
        }
        Check(layout.GetComponentsInChildren<Michsky.UI.Heat.LocalizedObject>(true).Length == 0 &&
            category.GetComponentsInChildren<Michsky.UI.Heat.LocalizedObject>(true).Length == 0,
            "the exported visuals cannot overwrite the game's localized captions");
        CheckExampleGeometry(layout);

        Transform exampleList = layout.transform.Find("Content/Panel Content/Panels/General/Content/List/Layout Group");
        var exampleGroup = exampleList != null ? exampleList.GetComponent<UnityEngine.UI.VerticalLayoutGroup>() : null;
        Check(exampleGroup != null && exampleGroup.spacing == 15 && exampleGroup.childControlWidth,
            "the example settings list keeps its original layout group");

        GameObject appPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/INApplicationInterface.prefab");
        if (appPrefab != null)
        {
            GameObject app = UnityEngine.Object.Instantiate(appPrefab);
            try
            {
                Transform panel = app.transform.Find("Canvas/Panel");
                Transform main = panel.Find("Main");
                Transform sidebar = panel.Find("Sidebar");
                Transform actionBar = panel.Find("ActionBar");
                var back = actionBar.Find("BackButton").GetComponent<UnityEngine.UI.Button>();
                var menu = actionBar.Find("MenuButton").GetComponent<UnityEngine.UI.Button>();
                GameObject installed;
                TMP_Text title = HeatUiPanelLayout.Install(panel.gameObject, main.gameObject,
                    sidebar.gameObject, actionBar.gameObject, back, menu, out installed);
                Check(installed != null && title != null && main.IsChildOf(installed.transform) &&
                    sidebar.IsChildOf(installed.transform) && actionBar.IsChildOf(installed.transform),
                    "the application shell uses the example panel, categories, description and hotkeys");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(app);
            }
        }

        foreach (string name in new[] { "INSettingsInterface", "StarSeaSettingsInterface", "INDataPanel",
            "INAddonInterface", "PartAdjustInterface", "CustomBackgroundInterface" })
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/" + name + ".prefab");
            Check(source != null, name + " exists as an application panel");
            if (source == null)
            {
                continue;
            }

            GameObject instance = UnityEngine.Object.Instantiate(source);
            try
            {
                Transform original = instance.transform.Find("Panel");
                Transform content = original != null ? original.Find("Scroll View/Viewport/Content") : null;
                Check(HeatUiPanelLayout.AttachElement(instance), name + " attaches to the example scroll panel");
                Transform page = instance.transform.Find("HeatPage");
                Check(page != null && content != null && content.IsChildOf(page) && !original.gameObject.activeSelf,
                    name + " uses the example viewport instead of its former panel");
                if (page != null && content != null)
                {
                    var list = content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
                    var scroll = page.GetComponentInChildren<UnityEngine.UI.ScrollRect>(true);
                    Check(list != null && exampleGroup != null && list.spacing == exampleGroup.spacing &&
                        list.childControlWidth == exampleGroup.childControlWidth && scroll != null &&
                        scroll.content == content, name + " preserves the example list and scroll binding");
                    HeatUiPanelLayout.SetElementActive(instance, true);
                    Check(page.gameObject.activeSelf && !original.gameObject.activeSelf,
                        name + " only opens the Heat UI page");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        GameObject commandSource = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/INCommandSystem.prefab");
        if (commandSource != null)
        {
            GameObject command = UnityEngine.Object.Instantiate(commandSource);
            try
            {
                Transform mainPage = command.transform.Find("MainPage");
                Check(HeatUiPanelLayout.AttachElement(command) && mainPage.IsChildOf(command.transform.Find("HeatPage")),
                    "the command panel retains its functional pages inside the example panel");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(command);
            }
        }
    }

    private static void CheckExampleGeometry(GameObject layout)
    {
        Scene previous = SceneManager.GetActiveScene();
        Scene demo = EditorSceneManager.OpenScene(
            "Assets/Heat - Complete Modern UI/Scenes/Main Menu (Desktop).unity", OpenSceneMode.Additive);
        try
        {
            Transform source = null;
            foreach (GameObject root in demo.GetRootGameObjects())
            {
                source = root.transform.Find("Main Content/Settings");
                if (source != null)
                {
                    break;
                }
            }
            Check(source != null, "the desktop example Settings panel is present in the original scene");
            if (source == null)
            {
                return;
            }

            RectTransform[] original = source.GetComponentsInChildren<RectTransform>(true);
            RectTransform[] staged = layout.GetComponentsInChildren<RectTransform>(true);
            bool same = original.Length == staged.Length;
            for (int index = 0; same && index < original.Length; index++)
            {
                same = original[index].anchorMin == staged[index].anchorMin &&
                    original[index].anchorMax == staged[index].anchorMax &&
                    original[index].anchoredPosition == staged[index].anchoredPosition &&
                    original[index].sizeDelta == staged[index].sizeDelta &&
                    original[index].pivot == staged[index].pivot &&
                    original[index].localScale == staged[index].localScale;
            }
            Check(same, "the exported Settings panel retains every example RectTransform and its geometry");
        }
        finally
        {
            EditorSceneManager.CloseScene(demo, true);
            if (previous.IsValid() && previous.isLoaded)
            {
                SceneManager.SetActiveScene(previous);
            }
        }
    }

    /// <summary>
    /// The panels' own backgrounds take Heat UI's palette. Checked on a stand-in hierarchy shaped like
    /// the panels' - Panel/Background - because what has to hold is that the right object is found and
    /// that the colour source resolves, not that a particular prefab was edited.
    /// </summary>
    private static void CheckPanelSkin()
    {
        Check(HeatUiSkin.Manager() != null, "Heat UI's UIManager colour asset resolves from Resources");

        var root = new GameObject("HeatUiAuditPanel", typeof(RectTransform));
        var panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(root.transform, false);
        var background = new GameObject("Background", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        background.transform.SetParent(panel.transform, false);

        try
        {
            HeatUiSkin.ApplyPanelSkin(root);

            var tint = background.GetComponent<Michsky.UI.Heat.UIManagerImage>();
            Check(tint != null, "the panel background takes Heat UI's colour component");
            if (tint != null)
            {
                Check(tint.colorType == Michsky.UI.Heat.UIManagerImage.ColorType.Background,
                    "the panel background uses Heat UI's Background colour type");
            }

            // Applying again must not add a second one: the component is DisallowMultipleComponent.
            HeatUiSkin.ApplyPanelSkin(root);
            Check(background.GetComponents<Michsky.UI.Heat.UIManagerImage>().Length == 1,
                "re-applying the panel skin does not duplicate the colour component");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>
    /// Swaps a throwaway button for Heat UI's, the way the panels do. The panels subscribe to the result
    /// through onClick, so the thing that has to hold is that the swap yields a working click target even
    /// though ButtonManager only adds one itself while playing and enabled.
    /// </summary>
    private static void CheckButtonSwap()
    {
        var host = new GameObject("HeatUiAuditButtonHost", typeof(RectTransform));
        var old = new GameObject("OldButton", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
        old.transform.SetParent(host.transform, false);

        // The panels' own buttons carry a locale for their caption, and reading one needs the localization
        // system running - which it is not in this context. The swap has to survive that, so the fake old
        // button carries a locale id too.
        var oldLocale = old.AddComponent<UITextLocale>();
        oldLocale.ID = "AuditButton";

        bool clicked = false;

        try
        {
            var binding = HeatUiSkin.ReplaceButton(old.GetComponent<UnityEngine.UI.Button>(), HeatUiSkin.ButtonTemplate);
            Check(binding != null, "the button swap is taken for Heat UI's button template");
            if (binding == null)
            {
                return;
            }

            Check(binding.Button != null, "the swapped button has a click target for the panels to subscribe to");
            Check(binding.IsHeatUi, "the swapped button is driven by Heat UI's ButtonManager");
            Check(old == null, "the original button is removed once the swap is taken");

            binding.Label = "AUDIT";
            Check(true, "setting the caption through the binding does not throw");

            // A caller subscribes after the swap, which is what the panels do, so the listener goes on the
            // button the swap returned rather than on the one it replaced.
            if (binding.Button != null)
            {
                binding.Button.onClick.AddListener(() => clicked = true);
                binding.Button.onClick.Invoke();
                Check(clicked, "a listener subscribed to the swapped button still receives clicks");
            }

            binding.Interactable = false;
            Check(!binding.Interactable, "the swapped button can be made non-interactable");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    /// <summary>
    /// UITextLocale has to write into TextMeshPro as well as the legacy Text, since Heat UI labels are
    /// TextMeshPro. Skipped when localization is not running, which is the case in a bare batch run.
    /// </summary>
    private static void CheckLocaleHandlesTextMeshPro()
    {
        if (INLocalization.Instance == null)
        {
            Note("UITextLocale check skipped: INLocalization is not running in this context");
            return;
        }

        var go = new GameObject("HeatUiAuditLocale", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(UITextLocale));
        try
        {
            var locale = go.GetComponent<UITextLocale>();
            locale.ID = "DataPanel_FPS";
            locale.UpdateText();
            var label = go.GetComponent<TextMeshProUGUI>();
            Check(!string.IsNullOrEmpty(label.text), "UITextLocale writes into a TextMeshPro label");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    private static T Find<T>(string templateName) where T : Component
    {
        var template = Resources.Load<GameObject>(HeatUiSkin.ResourceFolder + "/" + templateName);
        return template == null ? null : template.GetComponentInChildren<T>(true);
    }

    private static GameObject Instantiate(string templateName)
    {
        var template = Resources.Load<GameObject>(HeatUiSkin.ResourceFolder + "/" + templateName);
        if (template == null)
        {
            Check(false, "template missing, cannot instantiate: " + templateName);
            return null;
        }

        return (GameObject)UnityEngine.Object.Instantiate(template);
    }

    private static void WriteReport()
    {
        var text = new StringBuilder();
        text.AppendLine("Heat UI conversion audit");
        text.AppendLine("========================");
        text.AppendLine();
        foreach (string line in Results)
        {
            text.AppendLine(line);
        }

        text.AppendLine();
        text.AppendLine(failures == 0 ? "RESULT: PASS" : "RESULT: FAIL (" + failures + ")");

        Directory.CreateDirectory(ReportDirectory);
        File.WriteAllText(ReportPath, text.ToString());
        Debug.Log("HeatUiAudit wrote " + ReportPath);
    }
}
