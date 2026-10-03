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

/// <summary>
/// Measures what opening each settings category costs, and what the value controls on it really are.
/// </summary>
/// <remarks>
/// <para>
/// Written for the two defects reported against the settings interface: 更多设置 (Heat UI's stand-in for
/// the legacy part-adjust panel, 208 rows) hitching when it is opened, and numeric rows showing an input
/// box that is not the box the row reserves for it. Both are properties of the panels as they are built,
/// not of any prefab, so they have to be read while playing.
/// </para>
/// <para>
/// Per category the audit reports, before the category is opened, how many Heat UI rows already exist in
/// it (styling a page on the frame it is first shown is what makes opening it hitch, so the count before
/// the click is the measurement that matters), then how many the click itself built and how long the
/// click's frame took, then the geometry of every value control on the page - with a screenshot at the
/// bottom of the list, where the numeric rows of a long page are.
/// </para>
/// </remarks>
[InitializeOnLoad]
public static class HeatUiValueAudit
{
    private const string Flag = "HeatUiValueAudit";
    private const string ReportPath = "Builds/QA/heat-ui-values.txt";

    /// <summary>Categories the sidebar holds, in the order the sidebar lists them.</summary>
    private const int Pages = 7;

    /// <summary>How long the app interface is given to warm every page up before the first measurement.</summary>
    private const double Settle = 25.0;

    /// <summary>
    /// True to leave the app interface's warm-up out of it, so that each category pays for its own styling
    /// on the click that opens it - the shape the defect had - and the profile below shows where that goes.
    /// </summary>
    private const bool ProfileWithoutWarmUp = false;

    /// <summary>
    /// True to ask the font for every character the panel shows before measuring the pages, which is what
    /// tells whether the pages were really paying for their own glyphs.
    /// </summary>
    private const bool WarmFontFirst = false;

    private static double s_deadline;
    private static double s_next;
    private static int s_step;
    private static int s_page;
    private static int s_waitFrames;
    private static int s_rowsBefore;
    private static double s_switchTime;
    private static string s_switchWhat = "first open";
    private static float s_worstFrame;
    private static float s_worstFrameOverall;
    private static double s_settleStart;
    private static double s_settleUntil;
    private static int s_ledgerTick;
    private static INAppInterface s_app;
    private static GameObject s_panel;
    private static Toggle[] s_categories;
    private static readonly List<string> Errors = new List<string>();
    private static readonly StringBuilder Report = new StringBuilder();

    static HeatUiValueAudit()
    {
        EditorApplication.playModeStateChanged += StateChanged;
    }

    public static void Run()
    {
        HeatUiPanelRepair.Run();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Flag, true);

        // Deliberately not run with -quit: that ends the batch run the moment this method returns, which
        // is before play mode - where the panels are built - has started. This carries its own hard stop
        // instead, so a run that never reaches play mode still terminates.
        double hardStop = EditorApplication.timeSinceStartup + 240;
        EditorApplication.update += () =>
        {
            if (!EditorApplication.isPlaying && EditorApplication.timeSinceStartup >= hardStop)
            {
                Debug.LogWarning("Heat UI value audit never reached play mode; giving up.");
                SessionState.SetBool(Flag, false);
                EditorApplication.Exit(2);
            }
        };
        EditorApplication.EnterPlaymode();
    }

    private static void StateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Flag, false)) { return; }
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            Application.logMessageReceived += OnLog;
            s_deadline = EditorApplication.timeSinceStartup + 180;
            s_next = EditorApplication.timeSinceStartup + 2;
            s_step = -1;
            s_page = 0;
            s_waitFrames = 0;
            EditorApplication.update += Sample;
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
                if (ProfileWithoutWarmUp)
                {
                    // Claim the warm-up has already run, so nothing is styled until a category is opened.
                    typeof(INAppInterface).GetField("m_warmedUp", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(s_app, true);
                }
            }
            catch (Exception exception) { Finish(exception.ToString()); }
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Flag, false);
            EditorApplication.Exit(SessionState.GetInt(Flag + "Exit", 1));
        }
    }

    /// <summary>
    /// Times what one Heat UI settings row costs, split into the parts that can be avoided.
    /// </summary>
    /// <remarks>
    /// Building a row is the whole cost of opening a category, and a row is only six game objects, so the
    /// time has to be in the components waking up rather than in the prefab. This builds rows into a parent
    /// that is switched off - where nothing wakes up - and then switches individual component types off
    /// before moving the rows under a live parent, which is what tells them apart.
    /// </remarks>
    private static void Benchmark()
    {
        GameObject template = Resources.Load<GameObject>("HeatUI/" + HeatUiSkin.SwitchElementTemplate);
        GameObject field = Resources.Load<GameObject>("HeatUI/" + HeatUiSkin.InputFieldTemplate);
        GameObject header = Resources.Load<GameObject>("HeatUI/" + HeatUiSkin.HeaderTemplate);
        if (template == null || field == null)
        {
            Report.AppendLine("=== row benchmark: templates missing");
            return;
        }
        const int Count = 50;
        GameObject idle = new GameObject("QA bench idle", typeof(RectTransform));
        GameObject live = new GameObject("QA bench live", typeof(RectTransform));
        idle.SetActive(false);
        WarmRows(idle, live, template, null, 5);

        Report.AppendLine("=== row benchmark, " + Count + " rows each, milliseconds per row");
        Report.AppendLine("   prefab only (idle parent)            " + InstantiateCost(idle, template, Count).ToString("F2"));
        Report.AppendLine("   + components waking (as shipped)     " + WakeCost(idle, live, template, null, Count, null, false).ToString("F2"));
        Report.AppendLine("   + wake, nested Switch hidden         " + WakeCost(idle, live, template, null, Count, "Switch", false).ToString("F2"));
        Report.AppendLine("   + wake, LocalizedObject objects hid  " + WakeCost(idle, live, template, null, Count, null, true).ToString("F2"));
        Report.AppendLine("   + wake, Animator object hidden       " + WakeCost(idle, live, template, null, Count, "Switch", true).ToString("F2"));
        Report.AppendLine("   + wake, without Animator             " + WakeCost(idle, live, template, typeof(Animator), Count, null, false).ToString("F2"));
        Report.AppendLine("   + wake, without every Behaviour      " + WakeCost(idle, live, template, typeof(Behaviour), Count, null, false).ToString("F2"));
        Report.AppendLine("   + wake, without LocalizedObject      " + WakeCost(idle, live, template, typeof(Michsky.UI.Heat.LocalizedObject), Count, null, false).ToString("F2"));
        Report.AppendLine("   + wake, without SettingsElement      " + WakeCost(idle, live, template, typeof(Michsky.UI.Heat.SettingsElement), Count, null, false).ToString("F2"));
        Report.AppendLine("   + wake, without SwitchManager        " + WakeCost(idle, live, template, typeof(Michsky.UI.Heat.SwitchManager), Count, null, false).ToString("F2"));
        Report.AppendLine("   + wake, without UIManagerImage       " + WakeCost(idle, live, template, typeof(Michsky.UI.Heat.UIManagerImage), Count, null, false).ToString("F2"));
        Report.AppendLine("   + wake, without UIManagerText        " + WakeCost(idle, live, template, typeof(Michsky.UI.Heat.UIManagerText), Count, null, false).ToString("F2"));
        Report.AppendLine("   input field prefab only              " + InstantiateCost(idle, field, Count).ToString("F2"));
        Report.AppendLine("   input field, components waking       " + WakeCost(idle, live, field, null, Count, null, false).ToString("F2"));
        Report.AppendLine("   header prefab, components waking     " + WakeCost(idle, live, header, null, Count, null, false).ToString("F2"));
        Report.AppendLine("   header prefab, LocalizedObject hid   " + WakeCost(idle, live, header, null, Count, null, true).ToString("F2"));
        ReportRow(template);
        ReportRow(header);
        Clear(idle);
        Clear(live);
        UnityEngine.Object.DestroyImmediate(idle);
        UnityEngine.Object.DestroyImmediate(live);
        FontBenchmark(WarmFontFirst);
    }

    /// <summary>
    /// Measures what the labels' Chinese characters cost to get into the font atlas.
    /// </summary>
    /// <remarks>
    /// Building a row measures far cheaper than styling a page does, and the label assignment itself is
    /// quick, which points at the text meshes: a dynamic font asset rasterizes a glyph the first time it is
    /// asked for one, and that happens when the text is regenerated - at the end of the frame, outside any
    /// of the phases the adapter can time. This asks the font for every character the panel shows, in one
    /// go, and reports what that cost and what it did to the atlas. With <paramref name="addCharacters"/>
    /// the glyphs are left in the atlas, so the pages measured afterwards are measured warm.
    /// </remarks>
    private static void FontBenchmark(bool addCharacters)
    {
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("HeatUI/Chinese Fallback");
        Report.AppendLine("=== font atlas");
        if (font == null)
        {
            Report.AppendLine("   Chinese Fallback missing");
            return;
        }
        StringBuilder all = new StringBuilder();
        foreach (Text label in s_panel.GetComponentsInChildren<Text>(true)) { all.Append(label.text); }
        HashSet<char> distinct = new HashSet<char>();
        foreach (char character in all.ToString()) { distinct.Add(character); }
        Report.AppendLine("   " + font.name + " populationMode=" + font.atlasPopulationMode +
            " atlas=" + font.atlasTexture.width + "x" + font.atlasTexture.height +
            " glyphs=" + font.characterTable.Count);
        Report.AppendLine("   the panel shows " + all.Length + " characters, " + distinct.Count + " of them distinct");
        if (!addCharacters)
        {
            Report.AppendLine("   atlas left cold: the pages below pay for their own glyphs");
            return;
        }
        string missing;
        double start = EditorApplication.timeSinceStartup;
        bool added = font.TryAddCharacters(all.ToString(), out missing);
        double spent = EditorApplication.timeSinceStartup - start;
        Report.AppendLine("   TryAddCharacters over all of them: " + (spent * 1000).ToString("F1") + "ms, ok=" + added +
            ", glyphs now " + font.characterTable.Count + ", still missing " + (missing == null ? 0 : missing.Length));
    }

    private static void WarmRows(GameObject idle, GameObject live, GameObject template, Type skip, int count)
    {
        WakeCost(idle, live, template, skip, count, null, false);
    }

    private static double InstantiateCost(GameObject parent, GameObject template, int count)
    {
        double start = EditorApplication.timeSinceStartup;
        for (int index = 0; index < count; index++)
        {
            UnityEngine.Object.Instantiate(template, parent.transform, false);
        }
        double spent = EditorApplication.timeSinceStartup - start;
        Clear(parent);
        return spent * 1000.0 / count;
    }

    /// <summary>Milliseconds per row for everything that runs when a row's components first wake up.</summary>
    /// <remarks>
    /// <paramref name="skip"/> turns components off, which does NOT keep their Awake and OnEnable from
    /// running - only switching the game object off does that, which is what <paramref name="hideChild"/>
    /// and <paramref name="hideLocalized"/> are for.
    /// </remarks>
    private static double WakeCost(GameObject idle, GameObject live, GameObject template, Type skip, int count,
        string hideChild, bool hideLocalized)
    {
        List<GameObject> rows = new List<GameObject>();
        for (int index = 0; index < count; index++)
        {
            rows.Add(UnityEngine.Object.Instantiate(template, idle.transform, false));
        }
        foreach (GameObject row in rows)
        {
            if (hideChild != null)
            {
                Transform child = row.transform.Find(hideChild);
                if (child != null) { child.gameObject.SetActive(false); }
            }
            if (hideLocalized)
            {
                foreach (Michsky.UI.Heat.LocalizedObject locale in row.GetComponentsInChildren<Michsky.UI.Heat.LocalizedObject>(true))
                {
                    locale.gameObject.SetActive(false);
                }
            }
            if (skip == null) { continue; }
            foreach (Component component in row.GetComponentsInChildren(skip, true))
            {
                Behaviour behaviour = component as Behaviour;
                if (behaviour != null) { behaviour.enabled = false; }
            }
        }
        double start = EditorApplication.timeSinceStartup;
        foreach (GameObject row in rows)
        {
            row.transform.SetParent(live.transform, false);
        }
        double spent = EditorApplication.timeSinceStartup - start;
        Clear(live);
        Clear(idle);
        return spent * 1000.0 / count;
    }

    private static void Clear(GameObject parent)
    {
        for (int index = parent.transform.childCount - 1; index >= 0; index--)
        {
            UnityEngine.Object.DestroyImmediate(parent.transform.GetChild(index).gameObject);
        }
    }

    /// <summary>The objects and components a row is made of, which is what the cost has to be sitting in.</summary>
    private static void ReportRow(GameObject template)
    {
        GameObject holder = new GameObject("QA bench probe", typeof(RectTransform));
        holder.SetActive(false);
        GameObject row = UnityEngine.Object.Instantiate(template, holder.transform, false);
        Report.AppendLine("=== contents of " + template.name);
        foreach (Transform child in row.GetComponentsInChildren<Transform>(true))
        {
            StringBuilder parts = new StringBuilder();
            foreach (Component component in child.GetComponents<Component>())
            {
                parts.Append(component == null ? "MISSING " : component.GetType().Name + " ");
            }
            Report.AppendLine("   " + child.name + ": " + parts);
        }
        UnityEngine.Object.DestroyImmediate(holder);
    }

    private static void SetStatic(Type type, string property, object value)
    {
        type.GetProperty(property, BindingFlags.Static | BindingFlags.Public).SetValue(null, value);
    }

    /// <summary>Watches the length of the frames the game is getting, so a hitch shows up as one number.</summary>
    private static void Sample()
    {
        if (!EditorApplication.isPlaying) { return; }
        float delta = Time.unscaledDeltaTime;
        if (delta > s_worstFrame) { s_worstFrame = delta; }
        if (delta > s_worstFrameOverall) { s_worstFrameOverall = delta; }
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
        s_next = EditorApplication.timeSinceStartup + 0.75;
        try
        {
            if (s_app == null) { Finish("No application interface"); return; }
            s_panel.SetActive(true);
            ((UnityEngine.UI.Button)typeof(INAppInterface).GetField("m_enterButton", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(s_app)).gameObject.SetActive(false);
            if (s_categories == null) { s_categories = s_app.Sidebar.GetComponentsInChildren<Toggle>(); }

            if (s_step == -1)
            {
                if (s_settleUntil <= 0)
                {
                    s_settleStart = EditorApplication.timeSinceStartup;
                    s_settleUntil = s_settleStart + Settle;
                    s_ledgerTick = 0;
                    Report.AppendLine("=== warm-up window (" + Settle + "s)");
                }
                // The panel has just come up, which is when the panels build their rows and the app
                // interface warms the pages up. Nothing is measured until that has had time to run.
                if (s_ledgerTick++ % 4 == 1) { ReportLedger(); }
                if (EditorApplication.timeSinceStartup >= s_settleUntil)
                {
                    Benchmark();
                    s_step = 0;
                }
                return;            }

            if (s_step == 0)
            {
                if (s_page >= Pages)
                {
                    Finish(null);
                    return;
                }

                ReportBefore(s_page);
                Activate(s_page, "first open");
                s_step = 1;
                s_waitFrames = Time.frameCount + 12;
                return;
            }

            if (Time.frameCount < s_waitFrames) { return; }

            if (s_step == 1)
            {
                ReportSwitch(s_page, "first open");
                Capture("Builds/QA/heat-ui-value-" + s_page + "-top.png");
                ReportGeometry(s_page, "top");
                ScrollToBottom();
                s_step = 2;
                s_waitFrames = Time.frameCount + 6;
                return;
            }

            if (s_step == 2)
            {
                Capture("Builds/QA/heat-ui-value-" + s_page + "-bottom.png");
                ReportGeometry(s_page, "bottom");

                // Opening the page a second time is the number that matters once the rows are built and
                // the warm-up has run: it is what the player's click costs when nothing is left to style.
                if (s_page == 0) { s_step = 0; s_page++; return; }
                s_categories[0].isOn = true;
                s_step = 3;
                s_waitFrames = Time.frameCount + 12;
                return;
            }

            if (s_step == 3 && Time.frameCount >= s_waitFrames)
            {
                Activate(s_page, "reopen");
                s_step = 4;
                s_waitFrames = Time.frameCount + 12;
                return;
            }

            if (s_step == 4 && Time.frameCount >= s_waitFrames)
            {
                ReportSwitch(s_page, "reopen");
                s_step = 0;
                s_page++;
            }
        }
        catch (Exception exception) { Finish(exception.ToString()); }
    }

    /// <summary>Switches to a category the way the sidebar does, and starts the clock on its frame.</summary>
    private static void Activate(int page, string what)
    {
        s_rowsBefore = Rows(page);
        s_switchTime = EditorApplication.timeSinceStartup;
        s_switchWhat = what;
        s_worstFrame = 0f;
        HeatUiLegacyPanelAdapter.ResetProfile();
        s_categories[page].isOn = true;
        Canvas.ForceUpdateCanvases();
    }

    /// <summary>How many Heat UI rows a page holds, open or not.</summary>
    private static int Rows(int page)
    {
        return Elements()[page].GetComponentsInChildren<Michsky.UI.Heat.SettingsElement>(includeInactive: true).Length;
    }

    /// <summary>
    /// Samples what each page has, once a beat, while the app interface warms the pages up: the panels
    /// build their rows in their own Start, and the warm-up can only style what is there when it runs.
    /// </summary>
    private static void ReportLedger()
    {
        Report.AppendLine();
        Report.AppendLine("--- ledger at +" + (EditorApplication.timeSinceStartup - s_settleStart).ToString("F2") + "s");
        foreach (GameObject element in Elements())
        {
            int groups = 0;
            int legacyRows = 0;
            foreach (Transform child in element.transform)
            {
                CountLegacy(child, ref groups, ref legacyRows);
            }
            Report.AppendLine("   " + element.name + ": legacyGroups=" + groups + " legacyRows=" + legacyRows +
                " heatRows=" + element.GetComponentsInChildren<Michsky.UI.Heat.SettingsElement>(includeInactive: true).Length +
                " inputFields=" + element.GetComponentsInChildren<TMP_InputField>(includeInactive: true).Length);
        }
    }

    private static void CountLegacy(Transform parent, ref int groups, ref int rows)
    {
        foreach (Transform child in parent)
        {
            if (child.name.StartsWith("SettingsGroup_")) { groups++; }
            if (child.name.StartsWith("SettingsItem_")) { rows++; }
            CountLegacy(child, ref groups, ref rows);
        }
    }

    /// <summary>A page's own rows, before it has ever been opened.</summary>
    private static void ReportBefore(int page)
    {
        Report.AppendLine();
        Report.AppendLine("=== category " + page + " (" + CategoryName(page) + ") before it is opened");
        foreach (GameObject element in Elements())
        {
            Transform heatPage = element.transform.Find("HeatPage");
            int rows = element.GetComponentsInChildren<Michsky.UI.Heat.SettingsElement>(includeInactive: true).Length;
            int fields = element.GetComponentsInChildren<TMP_InputField>(includeInactive: true).Length;
            Report.AppendLine("   " + element.name + ": pageOpen=" + (heatPage != null && heatPage.gameObject.activeSelf) +
                " heatRows=" + rows + " inputFields=" + fields);
        }
    }

    /// <summary>What the click that opened this page actually built, and how long its frame took.</summary>
    private static void ReportSwitch(int page, string what)
    {
        int rowsAfter = Rows(page);
        double work = EditorApplication.timeSinceStartup - s_switchTime - 0.75;
        Report.AppendLine("   OPEN (" + what + ") " + CategoryName(page) + ": heatRows " + s_rowsBefore + " -> " + rowsAfter +
            " (built by the click: " + (rowsAfter - s_rowsBefore) + "), work on top of the 0.75s beat: " +
            (work * 1000).ToString("F0") + "ms, worst frame seen: " + (s_worstFrame * 1000).ToString("F0") + "ms");
        ReportProfile(what);
    }

    /// <summary>Where the styling pass spent its time, if it ran at all during the last step.</summary>
    private static void ReportProfile(string what)
    {
        if (HeatUiLegacyPanelAdapter.Profile.Count == 0) { return; }
        double total = 0;
        foreach (KeyValuePair<string, double> entry in HeatUiLegacyPanelAdapter.Profile) { total += entry.Value; }
        List<KeyValuePair<string, double>> sorted = HeatUiLegacyPanelAdapter.Profile.ToList();
        sorted.Sort((left, right) => right.Value.CompareTo(left.Value));
        Report.AppendLine("      styling profile (" + what + "): total " + total.ToString("F1") + "ms");
        foreach (KeyValuePair<string, double> entry in sorted)
        {
            Report.AppendLine("         " + entry.Key.PadRight(22) + entry.Value.ToString("F1") + "ms");
        }
    }

    /// <summary>Every value control on the page, with the row and viewport it has to fit in.</summary>
    private static void ReportGeometry(int page, string where)
    {
        GameObject element = Elements()[page];
        Transform heatPage = element.transform.Find("HeatPage");
        if (heatPage == null) { Report.AppendLine("   no HeatPage"); return; }

        ScrollRect scroll = heatPage.GetComponentInChildren<ScrollRect>(true);
        Rect viewportRect = scroll != null && scroll.viewport != null ? scroll.viewport.rect : new Rect();
        int paletteComponents = heatPage.GetComponentsInChildren<Michsky.UI.Heat.UIManagerImage>(includeInactive: true).Length +
            heatPage.GetComponentsInChildren<Michsky.UI.Heat.UIManagerText>(includeInactive: true).Length;

        Report.AppendLine("   geometry (" + where + "): viewport=" + Size(viewportRect.size) +
            " valueBoxes=" + heatPage.GetComponentsInChildren<TMP_InputField>(includeInactive: true).Length +
            " paletteDriven=" + paletteComponents);

        foreach (Michsky.UI.Heat.SettingsElement row in heatPage.GetComponentsInChildren<Michsky.UI.Heat.SettingsElement>(includeInactive: true))
        {
            TMP_InputField field = row.GetComponentInChildren<TMP_InputField>(includeInactive: true);
            Rect rowRect = ((RectTransform)row.transform).rect;
            if (field == null)
            {
                Michsky.UI.Heat.SwitchManager toggle = row.GetComponentInChildren<Michsky.UI.Heat.SwitchManager>(includeInactive: true);
                Report.AppendLine("      row " + row.name + " " + Size(rowRect) + " -> " +
                    (toggle != null ? "switch " + Size(((RectTransform)toggle.transform).rect.size) + " scale " + toggle.transform.localScale.x.ToString("F2") : "no control"));
                continue;
            }

            RectTransform fieldTransform = (RectTransform)field.transform;
            Rect fieldRect = fieldTransform.rect;
            string fit = viewportRect.width > 0 && fieldRect.width > viewportRect.width - 10 ? " WIDER-THAN-VIEWPORT" : string.Empty;
            Report.AppendLine("      row " + row.name + " " + Size(rowRect) + " -> input " + Size(fieldRect) +
                " scale " + field.transform.localScale.x.ToString("F2") +
                " local " + fieldTransform.anchoredPosition.ToString("F0") +
                " world " + fieldTransform.position.ToString("F0") + fit);
        }
    }

    private static string Size(Rect rect)
    {
        return rect.width.ToString("F0") + "x" + rect.height.ToString("F0");
    }

    private static string Size(Vector2 size)
    {
        return size.x.ToString("F0") + "x" + size.y.ToString("F0");
    }

    private static void ScrollToBottom()
    {
        ScrollRect scroll = Elements()[s_page].GetComponentInChildren<ScrollRect>(true);
        if (scroll != null) { scroll.verticalNormalizedPosition = 0f; }
        Canvas.ForceUpdateCanvases();
    }

    private static string CategoryName(int page)
    {
        GameObject[] elements = Elements();
        return page < elements.Length ? elements[page].name : "?";
    }

    private static GameObject[] Elements()
    {
        Transform main = s_app.Main.transform;
        List<GameObject> list = new List<GameObject>();
        foreach (Transform child in main)
        {
            if (child.name == "HeatHeader" || child.name.StartsWith("HeatRow_")) { continue; }
            if (child.GetComponent<HeatUiLegacyPanelAdapter>() != null ||
                child.GetComponentInChildren<Michsky.UI.Heat.SettingsElement>(true) != null ||
                child.Find("HeatPage") != null ||
                child.name.StartsWith("IN") || child.name.StartsWith("StarSea") || child.name.StartsWith("PartAdjust") || child.name.StartsWith("Custom"))
            {
                list.Add(child.gameObject);
            }
        }
        return list.ToArray();
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
        Canvas.ForceUpdateCanvases();
        camera.Render();
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        Texture2D image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
        image.Apply();
        Directory.CreateDirectory("Builds/QA");
        File.WriteAllBytes(path, image.EncodeToPNG());
        RenderTexture.active = previous;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.worldCamera = null;
        camera.targetTexture = null;
        UnityEngine.Object.Destroy(image);
        UnityEngine.Object.Destroy(target);
        UnityEngine.Object.Destroy(camera.gameObject);
    }

    private static void Finish(string error)
    {
        EditorApplication.update -= Sample;
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        if (error != null) { Errors.Add(error); }
        Report.Insert(0, "HEAT UI VALUE AUDIT\n===================\nwarm-up: " +
            (ProfileWithoutWarmUp ? "off (each page styles on its own click)" : "on") +
            ", worst frame in the whole run: " + (s_worstFrameOverall * 1000).ToString("F0") + "ms\n\n");
        Report.AppendLine();
        Report.AppendLine("Errors: " + Errors.Count);
        foreach (string message in Errors) { Report.AppendLine(message); }
        Directory.CreateDirectory("Builds/QA");
        File.WriteAllText(ReportPath, Report.ToString());
        SessionState.SetInt(Flag + "Exit", Errors.Count == 0 ? 0 : 1);
        EditorApplication.ExitPlaymode();
    }
}
