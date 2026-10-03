using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[InitializeOnLoad]
public static class HeatUiPanelPlayAudit
{
    private const string Flag = "HeatUiPanelPlayAudit";
    private static double s_deadline;
    private static double s_next;
    private static int s_page;
    private static int s_nextFrame;
    private static INAppInterface s_app;
    private static GameObject s_panel;
    private static readonly List<string> Errors = new List<string>();
    private static readonly StringBuilder Report = new StringBuilder();

    static HeatUiPanelPlayAudit()
    {
        EditorApplication.playModeStateChanged += StateChanged;
    }

    public static void Run()
    {
        HeatUiPanelRepair.Run();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Flag, true);
        EditorApplication.EnterPlaymode();
    }

    private static void StateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Flag, false)) { return; }
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            Application.logMessageReceived += OnLog;
            s_deadline = EditorApplication.timeSinceStartup + 75;
            s_next = EditorApplication.timeSinceStartup + 2;
            s_page = -1;
            s_nextFrame = 0;
            EditorApplication.update += Tick;
            try
            {
                new GameObject("QA game services", typeof(INGameManager));
                INUnity.Initialize(AssetDatabase.LoadAssetAtPath<ResourceData>("Assets/MonoBehaviour/ResourceData.asset"));
                SetStatic(typeof(INUnity), "SettingsPath", Path.GetFullPath("Builds/QA/IsolatedSettings"));
                SetStatic(typeof(INUnity), "DataPath", Path.GetFullPath("Builds/QA/IsolatedSettings"));
                Directory.CreateDirectory(INUnity.SettingsPath);
                typeof(UserSettings).GetField("m_data", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null,
                    new SettingsData(Path.Combine(INUnity.SettingsPath, "Settings.xml"), false, string.Empty));
                typeof(INSettings).GetMethod("Load", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { 2 });
                INLocalization.Create();
                SetStatic(typeof(INUserSettings), "Default", new INUserSettings(INUnity.Version));
                SetStatic(typeof(INUserSettings), "Instance", new INUserSettings(INUnity.Version));
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                s_app = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/INApplicationInterface.prefab")).GetComponent<INAppInterface>();
                s_panel = (GameObject)typeof(INAppInterface).GetField("m_panel", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(s_app);
            }
            catch (Exception exception) { Finish(exception.ToString()); }
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Flag, false);
            EditorApplication.Exit(SessionState.GetInt(Flag + "Exit", 1));
        }
    }

    private static void SetStatic(Type type, string property, object value)
    {
        type.GetProperty(property, BindingFlags.Static | BindingFlags.Public).SetValue(null, value);
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
        {
            if (Errors.Count < 30) { Errors.Add(message + "\n" + stack); }
        }
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup > s_deadline) { Finish("Timeout"); return; }
        if (EditorApplication.timeSinceStartup < s_next) { return; }
        if (Time.frameCount < s_nextFrame) { return; }
        s_next = EditorApplication.timeSinceStartup + 1.5;
        s_nextFrame = Time.frameCount + 60;
        try
        {
            if (s_app == null) { Finish("No application interface"); return; }
            s_panel.SetActive(true);
            ((UnityEngine.UI.Button)typeof(INAppInterface).GetField("m_enterButton", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(s_app)).gameObject.SetActive(false);
            Toggle[] categories = s_app.Sidebar.GetComponentsInChildren<Toggle>();
            if (s_page >= 0)
            {
                // The panel warms every page up in the background when it opens, showing each one for two
                // frames while it prepares it, so the page this audit asked for can be hidden again by the
                // time it is counted. Ask for it once more, and count the rows whether or not a page happens
                // to be showing - a count of zero here used to be the warm-up, not a missing page.
                categories[s_page].isOn = true;
                Canvas.ForceUpdateCanvases();
                int rows = s_app.Main.GetComponentsInChildren<Michsky.UI.Heat.SettingsElement>(includeInactive: true).Length;
                int showing = s_app.Main.GetComponentsInChildren<Michsky.UI.Heat.SettingsElement>().Length;
                Report.AppendLine("Page " + s_page + ": " + showing + " of the panel's " + rows + " HEAT rows showing; " + s_app.Main.GetComponentsInChildren<HeatUiOptionHelp>(includeInactive: true).Length + " help bindings");
                Capture("Builds/QA/heat-ui-page-" + s_page + ".png");
                CheckInteractions();
            }
            s_page++;
            if (s_page >= categories.Length)
            {
                s_panel.SetActive(false);
                s_panel.SetActive(true);
                Require(s_panel.GetComponentInChildren<Michsky.UI.Heat.PanelManager>(true) == null, "No stale demo PanelManager");
                Require(s_panel.transform.Find("HeatCloseButton") != null, "Circular close button present");
                CheckContraptionFileRows();
                Finish(null);
                return;
            }
            categories[s_page].isOn = true;
            Canvas.ForceUpdateCanvases();
        }
        catch (Exception exception) { Finish(exception.ToString()); }
    }

    private static void CheckInteractions()
    {
        foreach (var row in s_app.Main.GetComponentsInChildren<Michsky.UI.Heat.SettingsElement>(includeInactive: true))
        {
            Require(row.GetComponent<CanvasGroup>() == null || row.GetComponent<CanvasGroup>().alpha > 0, "HEAT row not hidden by legacy adapter");
        }
        // Active only: this is the check that a page of read-only data has no switches on it, so it has to
        // see the page that is showing rather than every switch the panel holds.
        var switches = s_app.Main.GetComponentsInChildren<Michsky.UI.Heat.SwitchManager>();
        if (s_page == 0)
        {
            var row = s_app.Main.GetComponentsInChildren<UITextLocale>(includeInactive: true).First(item => item.ID == "GeneralSettings_FullScreen").GetComponentInParent<Michsky.UI.Heat.SettingsElement>();
            bool before = INUserSettings.Instance.GeneralSettings.FullScreen;
            var toggle = row.GetComponentInChildren<Michsky.UI.Heat.SwitchManager>(includeInactive: true);
            CheckSwitchVisual(toggle);
            RectTransform previousTab = null;
            foreach (var tab in s_app.Sidebar.GetComponentsInChildren<HeatUiCategorySize>())
            {
                RectTransform wrapper = (RectTransform)tab.transform;
                Require(Mathf.Abs(wrapper.rect.width - tab.Button.rect.width) < 0.1f, "Category button matches wrapper width");
                if (previousTab != null)
                {
                    Require(Mathf.Abs(wrapper.anchoredPosition.x - previousTab.anchoredPosition.x - previousTab.rect.width) < 0.1f, "Adjacent categories have no gap");
                }
                previousTab = wrapper;
            }
            foreach (var action in s_app.Main.GetComponentsInChildren<Michsky.UI.Heat.ButtonManager>(includeInactive: true))
            {
                if (action.transform.parent.name != "ButtonGroup") { continue; }
                Rect rect = ((RectTransform)action.transform).rect;
                Require(Mathf.Abs(rect.width - 200) < 0.1f && Mathf.Abs(rect.height - 44) < 0.1f, "Save/reset dimensions are 200x44");
            }
            Report.AppendLine("Before click: source=" + before + ", switch=" + toggle.isOn + ", interactable=" + row.isInteractable);
            row.OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
            Report.AppendLine("After click: source=" + INUserSettings.Instance.GeneralSettings.FullScreen + ", switch=" + toggle.isOn);
            Require(INUserSettings.Instance.GeneralSettings.FullScreen != before, "Row pointer click updates setting");
            toggle.GetComponent<Animator>().Update(0.6f);
            CheckSwitchVisual(toggle);
            Capture("Builds/QA/heat-ui-switch-on.png");
            row.OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
            toggle.GetComponent<Animator>().Update(0.6f);
            CheckSwitchVisual(toggle);
            var selector = s_app.Main.GetComponentsInChildren<Michsky.UI.Heat.HorizontalSelector>(includeInactive: true).First();
            selector.NextItem();
            Require(selector.index >= 0 && selector.index < selector.items.Count, "Selector click stays within choices");
            var help = row.GetComponent<HeatUiOptionHelp>();
            help.OnPointerEnter(new PointerEventData(EventSystem.current));
            var layout = s_panel.GetComponentInChildren<HeatUiOptionHelpView>();
            Require(layout.GetComponentsInChildren<TMP_Text>().Any(label => label.text.Contains(HeatUiOptionHelp.Chinese ? "全屏与窗口" : "fullscreen and windowed")), "Localized description shown on hover");
        }
        if (s_page == 1)
        {
            Require(switches.Length == 0, "Data values are read-only");
            Require(s_app.Main.GetComponentsInChildren<TMP_Text>().Any(label => label.text == INLocalization.Instance.GetText("DataPanel_Time")), "Data row caption stays localized after adapter scan");
        }
    }

    /// <summary>
    /// The three file actions the 载具存档设置 group ends with - 导出（存档）, 导入（存档）and 打开存档位置
    /// (see <c>docs/SaveTransfer.md</c>).
    /// </summary>
    /// <remarks>
    /// Their rows are ordinary settings rows and their value slot holds a Heat UI button rather than a
    /// control, so this checks both that the rows are there and that the buttons carry a resolved caption
    /// rather than their locale id - which is what a caption that never got applied looks like.
    /// </remarks>
    private static void CheckContraptionFileRows()
    {
        List<TMP_Text> labels = s_app.Main.GetComponentsInChildren<TMP_Text>(includeInactive: true).ToList();
        string[] rows = { "ContraptionDataSettings_Export", "ContraptionDataSettings_Import", "ContraptionDataSettings_OpenFolder" };
        string[] buttons = { "ContraptionDataSettings_ExportButton", "ContraptionDataSettings_ImportButton", "ContraptionDataSettings_OpenFolderButton" };
        foreach (string id in rows)
        {
            Require(labels.Any(label => label.text == INLocalization.Instance.GetText(id)), "载具存档设置 row: " + INLocalization.Instance.GetText(id));
        }
        foreach (string id in buttons)
        {
            Require(labels.Any(label => label.text == INLocalization.Instance.GetText(id)), "载具存档设置 button: " + INLocalization.Instance.GetText(id));
        }
        Report.AppendLine("载具存档设置 buttons: " + string.Join(" / ", buttons.Select(id => INLocalization.Instance.GetText(id))));
    }

    private static void CheckSwitchVisual(Michsky.UI.Heat.SwitchManager toggle)
    {
        Require(Mathf.Abs(toggle.transform.Find("On").GetComponent<CanvasGroup>().alpha - (toggle.isOn ? 1 : 0)) < 0.01f, "Switch fill matches value");
        Require(Mathf.Abs(toggle.transform.Find("Off").GetComponent<CanvasGroup>().alpha - (toggle.isOn ? 0 : 1)) < 0.01f, "Switch outline matches value");
        Require(Mathf.Abs(toggle.transform.Find("Handler/On").GetComponent<Image>().color.a - (toggle.isOn ? 1 : 0)) < 0.01f, "Switch dark thumb matches value");
        Require(Mathf.Abs(toggle.transform.Find("Handler/Off").GetComponent<Image>().color.a - (toggle.isOn ? 0 : 1)) < 0.01f, "Switch light thumb matches value");
        Require(Mathf.Abs(toggle.transform.Find("Label On").GetComponent<TMP_Text>().color.a - (toggle.isOn ? 1 : 0)) < 0.01f, "Switch ON caption matches value");
        Require(Mathf.Abs(toggle.transform.Find("Label Off").GetComponent<TMP_Text>().color.a - (toggle.isOn ? 0 : 1)) < 0.01f, "Switch OFF caption matches value");
    }

    private static void Capture(string path)
    {
        Camera camera = new GameObject("QA camera", typeof(Camera)).GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.15f, 0.2f);
        RenderTexture target = new RenderTexture(1920, 1080, 24);
        camera.targetTexture = target;
        Canvas canvas = s_app.GetComponentInChildren<Canvas>(true);
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1;
        canvas.transform.localScale = Vector3.one;
        Canvas.ForceUpdateCanvases();
        camera.Render();
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        Texture2D image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
        image.Apply();
        File.WriteAllBytes(path, image.EncodeToPNG());
        RenderTexture.active = previous;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.worldCamera = null;
        camera.targetTexture = null;
        UnityEngine.Object.Destroy(image);
        UnityEngine.Object.Destroy(target);
        UnityEngine.Object.Destroy(camera.gameObject);
    }

    private static void Require(bool condition, string description)
    {
        // Collected rather than thrown: a check that fails early is no reason to lose the report of every
        // check after it, and the run that hit the warm-up used to give up before a single one was printed.
        if (!condition) { Errors.Add(description); }
        Report.AppendLine((condition ? "PASS " : "FAIL ") + description);
    }

    private static void Finish(string error)
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        if (error != null) { Errors.Add(error); }
        Report.AppendLine("Errors: " + Errors.Count);
        foreach (string message in Errors) { Report.AppendLine(message); }
        Directory.CreateDirectory("Builds/QA");
        File.WriteAllText("Builds/QA/heat-ui-panels-play.txt", Report.ToString());
        SessionState.SetInt(Flag + "Exit", Errors.Count == 0 ? 0 : 1);
        EditorApplication.ExitPlaymode();
    }
}
