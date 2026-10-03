using System;
using System.IO;
using System.Text;
using Michsky.UI.Heat;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Reports, once the game is actually running, whether the console panels came up on Heat UI.
/// </summary>
/// <remarks>
/// <para>
/// The batch audit cannot answer this. It never builds a panel, and the panels build their rows from
/// prefab templates while playing, so nothing short of a running game proves the conversion took. This
/// probe closes that gap from inside the editor: a few seconds after play starts it counts the Heat UI
/// widgets that are really in the built panels, logs a summary, and writes it to
/// <c>Builds/QA/heat-ui-runtime.txt</c>.
/// </para>
/// <para>
/// The console is an in-game panel, so a run that stops at the main menu finds none. In that case the
/// probe builds the panels itself from their own prefabs - which is what the game does when it opens them -
/// and counts inside those instead. Either way it is read-only apart from that: a run where the panels
/// cannot be built reports that rather than failing.
/// </para>
/// </remarks>
public static class HeatUiRuntimeProbe
{
    private const string ReportPath = "Builds/QA/heat-ui-runtime.txt";
    private const string AuditReportPath = "Builds/QA/heat-ui.txt";

    /// <summary>Long enough for the game to load a save and open the panels its own way.</summary>
    private const float DelaySeconds = 4f;

    /// <summary>How long the batch play-mode run is given before it exits on its own.</summary>
    private const float PlaySeconds = 15f;

    /// <summary>Panels the console shows, which a run stopping at the main menu will not have loaded.</summary>
    private static readonly string[] PanelPrefabs =
    {
        "Assets/GameObject/StarSeaSettingsInterface.prefab",
        "Assets/GameObject/INDataPanel.prefab"
    };

    private static double s_deadline;
    private static bool s_waiting;
    private static bool s_spawnTried;
    private static bool s_spawned;
    private static double s_spawnDeadline;

    [InitializeOnLoadMethod]
    private static void Hook()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    /// <summary>Runs the checks that do not need a running game, from the editor's menu.</summary>
    [MenuItem("Tools/Heat UI/Audit (no play mode needed)")]
    private static void RunAuditFromMenu()
    {
        try
        {
            HeatUiAudit.RunAll();
            EditorUtility.DisplayDialog("Heat UI audit", "Passed.\n\nReport: " + AuditReportPath, "OK");
        }
        catch (Exception exception)
        {
            Debug.LogError("Heat UI audit failed: " + exception);
            EditorUtility.DisplayDialog("Heat UI audit", "FAILED: " + exception.Message + "\n\nReport: " + AuditReportPath, "OK");
        }
    }

    /// <summary>
    /// Batch entry point: plays the game briefly so the panels build, then exits.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The batch audit proves the conversion works on Heat UI's own prefabs, but not that this game's
    /// panels come up on them - those are built while playing. This runs the game inside a batch editor
    /// for a few seconds, long enough for the probe above to take its reading and write the report, and
    /// then asks the editor to exit.
    /// </para>
    /// <para>
    /// It is deliberately not paired with <c>-quit</c>, which would end the run the moment this method
    /// returned, and it carries a hard stop so a game that never reaches play mode terminates with a
    /// distinguishable exit code instead of hanging. A game that reaches play mode but does not shut down
    /// cleanly is still bounded from outside, because the report is written before the exit is asked for.
    /// </para>
    /// </remarks>
    public static void RunPlayModeProbe()
    {
        double start = EditorApplication.timeSinceStartup;
        double deadline = start + PlaySeconds;
        double hardStop = start + (PlaySeconds * 4d);

        EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.EnterPlaymode();
            }
        };

        EditorApplication.update += () =>
        {
            if (!EditorApplication.isPlaying)
            {
                if (EditorApplication.timeSinceStartup >= hardStop)
                {
                    Debug.LogWarning("Heat UI play-mode probe never reached play mode; giving up.");
                    EditorApplication.Exit(2);
                }

                return;
            }

            if (EditorApplication.timeSinceStartup >= deadline)
            {
                Debug.Log("Heat UI play-mode probe done; report at " + ReportPath);
                EditorApplication.Exit(0);
            }
        };
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode)
        {
            return;
        }

        EditorApplication.delayCall += Schedule;
    }

    private static void Schedule()
    {
        if (!EditorApplication.isPlaying)
        {
            return;
        }

        s_spawnTried = false;
        s_spawned = false;
        s_spawnDeadline = 0d;
        s_deadline = EditorApplication.timeSinceStartup + DelaySeconds;
        if (s_waiting)
        {
            return;
        }

        s_waiting = true;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying)
        {
            s_waiting = false;
            EditorApplication.update -= Tick;
            return;
        }

        if (EditorApplication.timeSinceStartup < s_deadline)
        {
            return;
        }

        // Nothing in the scene: build the panels from their own prefabs, then give their Start a frame to
        // lay the rows out before counting.
        if (!s_spawnTried)
        {
            s_spawnTried = true;
            s_spawned = SpawnPanels();
            if (s_spawned)
            {
                s_spawnDeadline = EditorApplication.timeSinceStartup + 1d;
                return;
            }
        }

        if (s_spawnDeadline > 0d && EditorApplication.timeSinceStartup < s_spawnDeadline)
        {
            return;
        }

        s_waiting = false;
        EditorApplication.update -= Tick;

        try
        {
            Report();
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Heat UI runtime probe could not finish: " + exception.Message);
        }
    }

    private static void Report()
    {
        bool spawnedHere = s_spawned;
        var app = FindInScene<INAppInterface>();
        var settings = FindInScene<INSettingsInterface>();
        var data = FindInScene<INDataPanel>();

        int switches = Count<SwitchManager>(settings);
        int inputs = Count<TMP_InputField>(settings);
        int dropdowns = Count<Michsky.UI.Heat.Dropdown>(settings);
        int buttons = Count<ButtonManager>(app) + Count<ButtonManager>(settings) + Count<ButtonManager>(data);
        int headers = Count<LocalizedObject>(settings) + Count<LocalizedObject>(data);

        // What the panels do in Awake, which does not need the game's caches the way building the rows in
        // Start does - so this is the part that still reports when the panels were built here rather than
        // by the game.
        int panelButtons = Count<ButtonManager>(settings);
        int panelTints = Count<UIManagerImage>(settings) + Count<UIManagerImage>(data);

        bool rowsBuilt = switches > 0 || inputs > 0 || dropdowns > 0 || headers > 0;
        bool converted = rowsBuilt || buttons > 0 || panelButtons > 0 || panelTints > 0;

        var report = new StringBuilder();
        report.AppendLine("HEAT UI RUNTIME PROBE");
        report.AppendLine("=====================");
        report.AppendLine("Taken " + DelaySeconds + "s into play mode, from the panels as they were actually built.");
        report.AppendLine(spawnedHere
            ? "The scene had no console panels, so they were built here from their own prefabs. Their Awake runs"
              + " that way, but Start cannot build rows without the game's own caches."
            : "The console panels were already in the loaded scene.");
        report.AppendLine();
        report.AppendLine("panels:");
        report.AppendLine("  INAppInterface        " + (app != null));
        report.AppendLine("  INSettingsInterface   " + (settings != null));
        report.AppendLine("  INDataPanel           " + (data != null));
        report.AppendLine();
        report.AppendLine("Heat UI widgets inside them:");
        report.AppendLine("  SwitchManager         " + switches + "   (one per boolean settings row)");
        report.AppendLine("  TMP_InputField        " + inputs + "   (one per text settings row; the project's own fields are legacy InputField)");
        report.AppendLine("  Heat Dropdown         " + dropdowns + "   (one per engine settings row)");
        report.AppendLine("  ButtonManager         " + buttons + "   (panel buttons)");
        report.AppendLine("  LocalizedObject       " + headers + "   (group headings)");
        report.AppendLine();
        report.AppendLine("what the panels do in Awake, which does not need the game's caches:");
        report.AppendLine("  ButtonManager         " + panelButtons + "   (save and reset, swapped for Heat UI's)");
        report.AppendLine("  UIManagerImage        " + panelTints + "   (the panel background on Heat UI's palette)");
        report.AppendLine();
        report.AppendLine("geometry of generated rows in the Heat UI example list:");
        report.AppendLine("  row      " + Describe(FindByPrefix(settings != null ? settings.transform : null, "SettingsItem_"))
            + "   (Heat UI element template is 1000x60 before list width control)");
        report.AppendLine("  heading  " + Describe(FindByPrefix(settings != null ? settings.transform : null, "SettingsGroup_"))
            + "   (Heat UI header template is 200x30 before list width control)");
        report.AppendLine();

        if (rowsBuilt)
        {
            report.AppendLine("RESULT: PASS - the console panels built their rows on Heat UI.");
        }
        else if (converted)
        {
            report.AppendLine("RESULT: PASS (partial) - the panels' Awake converted their chrome to Heat UI, but no rows"
                + " were built. Rows are built in Start, which needs the game's own bootstrap, so a run that only"
                + " reaches the main menu cannot show them. Play the game into the console to see the rows.");
        }
        else
        {
            report.AppendLine("RESULT: INCONCLUSIVE - nothing Heat UI was found. Either the panels could not be built here,"
                + " or the pack's templates are not staged under Assets/Resources/HeatUI.");
        }

        string text = report.ToString();
        Debug.Log(text);

        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, text);
    }

    /// <summary>
    /// Builds the console's panels from their own prefabs, the way the game does when it opens them.
    /// Returns false when none of them could be built, which is reported rather than treated as failure.
    /// </summary>
    private static bool SpawnPanels()
    {
        bool any = false;
        foreach (string path in PanelPrefabs)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning("Heat UI runtime probe could not load " + path);
                continue;
            }

            try
            {
                // The probe may run before the game's settings/localization bootstrap. Instantiate the
                // staged prefab while its source is inactive so Awake/Start cannot dereference those
                // runtime-only services and flood the editor console with false failures.
                bool sourceActive = prefab.activeSelf;
                prefab.SetActive(false);
                try
                {
                    GameObject instance = UnityEngine.Object.Instantiate(prefab);
                    instance.name = prefab.name + " (probe)";
                    instance.SetActive(false);
                    any = true;
                }
                finally
                {
                    prefab.SetActive(sourceActive);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Heat UI runtime probe could not build " + path + ": " + exception.Message);
            }
        }

        return any;
    }

    /// <summary>
    /// Finds a component in the loaded scenes, ignoring the prefab assets that
    /// <see cref="Resources.FindObjectsOfTypeAll{T}"/> also returns.
    /// </summary>
    private static T FindInScene<T>() where T : Component
    {
        foreach (T candidate in Resources.FindObjectsOfTypeAll<T>())
        {
            if (candidate != null && candidate.gameObject.scene.IsValid())
            {
                return candidate;
            }
        }

        return null;
    }

    private static int Count<T>(Component root) where T : Component
    {
        return root == null ? 0 : root.GetComponentsInChildren<T>(includeInactive: true).Length;
    }

    /// <summary>First descendant whose name starts with a prefix, which is how the panels name their rows.</summary>
    private static Transform FindByPrefix(Transform root, string prefix)
    {
        if (root == null)
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name.StartsWith(prefix, StringComparison.Ordinal))
            {
                return child;
            }

            Transform found = FindByPrefix(child, prefix);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    /// A generated row's own size, which is the thing the geometry adoption is there to control: the
    /// panels' content does not size its children, so whatever the template says is what shows.
    /// </summary>
    private static string Describe(Transform target)
    {
        if (target == null)
        {
            return "not found";
        }

        var rect = target as RectTransform;
        return rect == null ? "no rect" : rect.sizeDelta.x + "x" + rect.sizeDelta.y;
    }
}
