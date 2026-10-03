using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Runs the RC1 controls for real: a contraption flagged as running, the panel pressed through its own
/// pointer handlers, and the gyro stepping its physics.
/// </summary>
/// <remarks>
/// <para>
/// The edit-mode audit next door checks the wiring; this one checks that the wiring does something once
/// the game is actually running, which is the only state the controls exist in. The level itself is
/// stood in for - a <see cref="LevelManager"/> holding a <see cref="GameMode"/> whose
/// <c>ContraptionRunning</c> is a contraption with <c>m_running</c> set - because
/// <c>INRc1Controls.IsDriving()</c> reads exactly that and nothing else, so everything downstream of the
/// gate is the real thing: the real panel, the real press/release handlers, the real gyro.
/// </para>
/// <para>
/// The checks are spread over frames on purpose: the panel shows itself from <c>Update</c>, and a step
/// that ran straight through would assert against a frame that has not happened yet.
/// </para>
/// <para>
/// Run with:  "&lt;editor&gt;\Unity.exe" -batchmode -projectPath &lt;project&gt; -executeMethod
/// Rc1RunModeAudit.Run   (no -quit and no -nographics: -quit ends the run before play mode, and the
/// repo's other play-mode audits run the same way.)
/// </para>
/// </remarks>
[InitializeOnLoad]
public static class Rc1RunModeAudit
{
    private const string Flag = "Rc1RunModeAudit";
    private const string ReportPath = "Builds/QA/rc1-controls-run-audit.txt";

    private static readonly List<string> Errors = new List<string>();
    private static readonly StringBuilder Report = new StringBuilder();

    private static INRc1Controls s_controls;
    private static Contraption s_contraption;
    private static Rc1AuditGameMode s_mode;
    private static RCR2Port.RoverPortGyro s_gyro;
    private static Rigidbody s_body;

    private static int s_step;
    private static double s_next;
    private static double s_deadline;

    /// <summary>Entering play mode reloads the domain, which drops static subscriptions.</summary>
    static Rc1RunModeAudit()
    {
        EditorApplication.playModeStateChanged += StateChanged;
    }

    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Flag, true);
        double hardStop = EditorApplication.timeSinceStartup + 240;
        EditorApplication.update += () =>
        {
            if (!EditorApplication.isPlaying && EditorApplication.timeSinceStartup >= hardStop)
            {
                Debug.LogWarning("RC1 run audit never reached play mode; giving up.");
                SessionState.SetBool(Flag, false);
                EditorApplication.Exit(2);
            }
        };
        EditorApplication.EnterPlaymode();
    }

    private static void StateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Flag, false)) return;
        if (state != PlayModeStateChange.EnteredPlayMode) return;

        s_step = 0;
        s_next = EditorApplication.timeSinceStartup + 1;
        s_deadline = EditorApplication.timeSinceStartup + 180;
        EditorApplication.update += Tick;
        try
        {
            Boot();
        }
        catch (Exception exception)
        {
            Errors.Add("boot: " + exception);
        }
    }

    private static void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        if (now >= s_deadline)
        {
            Errors.Add("timed out at step " + s_step);
            Finish();
            return;
        }
        if (now < s_next) return;
        try
        {
            Step();
        }
        catch (Exception exception)
        {
            Errors.Add("step " + (s_step - 1) + ": " + exception);
            Finish();
        }
    }

    private static void Step()
    {
        switch (s_step++)
        {
            // Feature on, nothing running: the controls must stay away.
            case 0:
                Report.AppendLine();
                Report.AppendLine("=== feature on, nothing running ===");
                Report.AppendLine("  Enabled " + INRc1Controls.Enabled + ", Active " + INRc1Controls.Active);
                Check(INRc1Controls.Enabled, "the feature reads on");
                Check(!INRc1Controls.Active, "the controls stay hidden while nothing is running");
                Check(Mathf.Abs(INRc1Controls.DriveAxis) < 1e-4f && INRc1Controls.Throttle == 0f,
                    "no pedal and no throttle are reported while the controls are hidden");
                StartRunning();
                Wait(0.4);
                break;

            // Running: the panel appears from Update, so this is a frame later.
            case 1:
                Report.AppendLine();
                Report.AppendLine("=== feature on, contraption running ===");
                Report.AppendLine("  Active " + INRc1Controls.Active + ", panel visible " + PanelActive());
                Check(INRc1Controls.Active, "the controls come up once a contraption is running");
                Check(PanelActive(), "the panel is visible");
                Wait(0.05);
                break;

            case 2:
                CheckPedals();
                Wait(0.05);
                break;

            case 3:
                CheckGyro();
                Wait(0.05);
                break;

            // Switching the row off has to put everything away, even with a pedal still down.
            case 4:
                INUserSettings.Instance.GeneralSettings.Rc1Controls = false;
                Press("Forward", true);
                Wait(0.4);
                break;

            case 5:
                Report.AppendLine();
                Report.AppendLine("=== feature switched off while running ===");
                Report.AppendLine("  Enabled " + INRc1Controls.Enabled + ", Active " + INRc1Controls.Active +
                                  ", panel visible " + PanelActive() + ", DriveAxis " + INRc1Controls.DriveAxis);
                Check(!INRc1Controls.Enabled, "the feature reads off");
                Check(!INRc1Controls.Active, "the controls go away when the row is switched off");
                Check(!PanelActive(), "the panel is hidden again");
                Check(Mathf.Abs(INRc1Controls.DriveAxis) < 1e-4f,
                    "a pedal still held when they went away stops driving");
                Press("Forward", false);

                s_body.angularVelocity = Vector3.zero;
                StepGyro();
                Check(Mathf.Abs(s_body.angularVelocity.z) < 1e-4f, "the gyro stops with the row off");
                break;

            default:
                Finish();
                break;
        }
    }

    private static void CheckPedals()
    {
        Press("Forward", true);
        Check(Mathf.Abs(INRc1Controls.DriveAxis - 1f) < 1e-4f, "holding the gas pedal drives forwards");
        Press("Back", true);
        Check(Mathf.Abs(INRc1Controls.DriveAxis) < 1e-4f, "holding both pedals cancels out");
        Press("Forward", false);
        Check(Mathf.Abs(INRc1Controls.DriveAxis + 1f) < 1e-4f, "holding the brake pedal drives backwards");
        Press("Back", false);
        Check(Mathf.Abs(INRc1Controls.DriveAxis) < 1e-4f, "releasing the pedals stops the drive");

        Press("Throttle", true);
        bool held = INRc1Controls.ThrottleHeld;
        float throttle = INRc1Controls.Throttle;
        Press("Throttle", false);
        Check(held && Mathf.Abs(throttle - 1f) < 1e-4f, "holding the throttle reports 1");
        Check(!INRc1Controls.ThrottleHeld && INRc1Controls.Throttle == 0f, "releasing it reports 0");
    }

    private static void CheckGyro()
    {
        Report.AppendLine();
        Report.AppendLine("=== gyro while the pedals are held ===");
        s_gyro.MaxAngularVelocity = Rc1GyroMax;
        s_gyro.Torque = RCR2Port.RoverPortGyro.SourceTorque;
        float dt = Time.fixedDeltaTime;

        // A torque is queued for the next simulation, so every case has to run one before the body's
        // angular velocity says anything (Boot put physics in script mode for exactly this).
        Press("Forward", true);
        s_body.angularVelocity = Vector3.zero;
        Spin(dt);
        float forwards = s_body.angularVelocity.z;
        Report.AppendLine("  gas pedal, spin 0 -> " + F(forwards));
        Check(forwards > 0f, "the gas pedal spins the rover one way");

        s_body.angularVelocity = new Vector3(0f, 0f, s_gyro.MaxAngularVelocity + 1f);
        float atCeiling = s_body.angularVelocity.z;
        Spin(dt);
        float capped = s_body.angularVelocity.z;
        Report.AppendLine("  gas pedal at the ceiling (" + F(s_gyro.MaxAngularVelocity) + "): " +
                          F(atCeiling) + " -> " + F(capped));
        Check(capped <= atCeiling + 1e-3f, "the gas pedal adds nothing once the spin is at the ceiling");

        Press("Forward", false);
        Press("Back", true);
        s_body.angularVelocity = Vector3.zero;
        Spin(dt);
        float backwards = s_body.angularVelocity.z;
        Report.AppendLine("  brake pedal, spin 0 -> " + F(backwards));
        Check(backwards < 0f, "the brake pedal spins it the other way");

        s_body.angularVelocity = new Vector3(0f, 0f, -(s_gyro.MaxAngularVelocity + 1f));
        float atFarCeiling = s_body.angularVelocity.z;
        Spin(dt);
        Report.AppendLine("  brake pedal at the ceiling: " + F(atFarCeiling) + " -> " +
                          F(s_body.angularVelocity.z));
        Check(s_body.angularVelocity.z >= atFarCeiling - 1e-3f,
            "the brake pedal adds nothing once the spin is at the far ceiling");
        Press("Back", false);

        s_body.angularVelocity = Vector3.zero;
        Spin(dt);
        Check(Mathf.Abs(s_body.angularVelocity.z) < 1e-4f, "the gyro is inert with no pedal held");
    }

    // ---- scaffolding ------------------------------------------------------

    private const float Rc1GyroMax = 220f * Mathf.Deg2Rad;   // PartSPrGyro.properties.max, deg/s

    private static void Boot()
    {
        Innovation.Json.Service = (Innovation.IJsonService)Activator.CreateInstance(
            typeof(INSettings).Assembly.GetType("JsonService"));

        new GameObject("QA game services", typeof(INGameManager));
        var table = typeof(INUnity).GetField("s_resources", BindingFlags.Static | BindingFlags.NonPublic)
            .GetValue(null) as System.Collections.IDictionary;
        if (table != null) { table.Clear(); }
        INUnity.Initialize(AssetDatabase.LoadAssetAtPath<ResourceData>("Assets/MonoBehaviour/ResourceData.asset"));
        SetStatic(typeof(INUnity), "SettingsPath", Path.GetFullPath("Builds/QA/IsolatedSettings"));
        SetStatic(typeof(INUnity), "DataPath", Path.GetFullPath("Builds/QA/IsolatedSettings"));
        Directory.CreateDirectory(INUnity.SettingsPath);
        var resources = (Dictionary<(string, string), UnityEngine.Object>)typeof(INUnity)
            .GetField("s_resources", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        foreach (string guid in AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets/TextAsset" }))
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null) resources[("TextAsset", asset.name)] = asset;
        }
        typeof(UserSettings).GetField("m_data", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null,
            new SettingsData(Path.Combine(INUnity.SettingsPath, "Settings.xml"), false, string.Empty));
        string saved = Path.Combine(INUnity.SettingsPath, "INFeatureSettings.json");
        if (File.Exists(saved)) { File.Delete(saved); }
        typeof(INSettings).GetMethod("Load", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { 3 });
        INLocalization.Create();
        SetStatic(typeof(INUserSettings), "Default", new INUserSettings(INUnity.Version));
        SetStatic(typeof(INUserSettings), "Instance", new INUserSettings(INUnity.Version));
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        GameData data = AssetDatabase.LoadAssetAtPath<GameData>("Assets/MonoBehaviour/GameData.asset");
        SetStatic(typeof(WPFMonoBehaviour), "s_gameData", data);
        // WPFMonoBehaviour.levelManager only looks for a LevelManager while
        // Singleton<GameManager>.Instance.IsInGame() is true, and that is simply "the state is Level".
        GameManager manager = new GameObject("GameManager").AddComponent<GameManager>();
        SetField(manager, "m_gameData", data);
        SetField(manager, "m_gameState", GameManager.GameState.Level);

        // Built inactive so its game data can be assigned before Awake runs: a fresh LevelManager has a
        // null m_gameData and Awake dereferences it straight away.
        var levelObject = new GameObject("LevelManager (QA)");
        levelObject.SetActive(false);
        LevelManager level = levelObject.AddComponent<LevelManager>();
        SetField(level, "m_gameData", data);
        s_mode = new Rc1AuditGameMode();
        SetField(level, "m_gameMode", s_mode);
        levelObject.SetActive(true);

        s_contraption = new GameObject("Contraption (QA)").AddComponent<Contraption>();
        SetField(s_contraption, "m_running", true);

        s_controls = new GameObject("INRc1Controls (QA)").AddComponent<INRc1Controls>();
        if (INRc1Controls.Instance == null)
        {
            throw new InvalidOperationException("INRc1Controls did not register itself");
        }

        var gyroHost = new GameObject("gyro (QA)");
        s_body = gyroHost.AddComponent<Rigidbody>();
        s_body.useGravity = false;
        // The gyro answers the panel through the part it sits on, so the part has to be there too, and
        // the part has to know which contraption it belongs to - the game assigns that in
        // Contraption.StartContraption, and the gyro stays out of the way without it.
        RCR2Port.RoverPortGyroPart gyroPart = gyroHost.AddComponent<RCR2Port.RoverPortGyroPart>();
        s_gyro = gyroHost.AddComponent<RCR2Port.RoverPortGyro>();
        gyroHost.transform.SetParent(s_contraption.transform, false);
        SetMember(gyroPart, "contraption", s_contraption);
        Check(gyroPart.contraption == s_contraption, "the QA gyro part belongs to the running contraption");

        INUserSettings.Instance.GeneralSettings.Rc1Controls = true;

        LevelManager found = WPFMonoBehaviour.levelManager;
        Report.AppendLine("booted: levelManager " + (found != null));
        Check(found != null, "WPFMonoBehaviour.levelManager finds the QA level");
    }

    /// <summary>Flags the contraption as the running one, which is what the panel's gate reads.</summary>
    private static void StartRunning()
    {
        s_mode.ContraptionRunning = s_contraption;
        Report.AppendLine("  contraption running: " + s_contraption.IsRunning);
        Check(s_contraption.IsRunning, "the QA contraption reports itself running");
    }

    /// <summary>Steps the gyro once, the way Unity's FixedUpdate would.</summary>
    private static void StepGyro()
    {
        MethodInfo fixedUpdate = typeof(RCR2Port.RoverPortGyro)
            .GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        fixedUpdate.Invoke(s_gyro, null);
    }

    /// <summary>
    /// Runs the gyro and then one physics step, which is what turns the torque it queued into an actual
    /// change in the body's angular velocity. <c>GamePhysics.Simulate</c> is the step every other audit
    /// here uses: it flushes the queued forces into whichever backend is selected and steps that.
    /// </summary>
    private static void Spin(float dt)
    {
        StepGyro();
        GamePhysics.Simulate(dt);
    }

    /// <summary>Presses or releases one control through the handler the pointer would call.</summary>
    private static void Press(string control, bool down)
    {
        Transform child = s_controls.transform.Find("Panel/" + control);
        if (child == null) { throw new InvalidOperationException("no control " + control); }
        Component handler = child.GetComponents<MonoBehaviour>()
            .FirstOrDefault(c => c != null && c.GetType().Name == "HoldButton");
        if (handler == null) { throw new InvalidOperationException(control + " has no hold handler"); }
        MethodInfo method = handler.GetType().GetMethod(down ? "OnPointerDown" : "OnPointerUp");
        method.Invoke(handler, new object[] { null });
    }

    private static bool PanelActive()
    {
        Transform panel = s_controls != null ? s_controls.transform.Find("Panel") : null;
        return panel != null && panel.gameObject.activeSelf;
    }

    private static void Wait(double seconds)
    {
        s_next = EditorApplication.timeSinceStartup + seconds;
    }

    // ---- plumbing ---------------------------------------------------------

    private static void Check(bool condition, string message)
    {
        Report.AppendLine((condition ? "  PASS " : "  FAIL ") + message);
        if (!condition) { Errors.Add(message); }
    }

    private static string F(float value) { return value.ToString("0.####"); }

    /// <summary>
    /// A private/protected field, searching the base types too - <c>m_gameData</c> for instance lives on
    /// WPFMonoBehaviour, and GetField does not look at non-public members of a base class.
    /// </summary>
    private static void SetField(object target, string name, object value)
    {
        for (Type type = target.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic |
                                                  BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (field == null) continue;
            field.SetValue(target, value);
            return;
        }
        throw new InvalidOperationException("no field " + name + " on " + target.GetType().Name +
                                            " or its base types");
    }

    /// <summary>A property or field, by name, searching the base types too.</summary>
    private static void SetMember(object target, string name, object value)
    {
        for (Type type = target.GetType(); type != null; type = type.BaseType)
        {
            PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic |
                                                           BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (property != null && property.CanWrite)
            {
                property.SetValue(target, value);
                return;
            }
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic |
                                                  BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }
        }
        throw new InvalidOperationException("no member " + name + " on " + target.GetType().Name);
    }

    private static void SetStatic(Type type, string name, object value)
    {
        FieldInfo field = type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if (field != null) { field.SetValue(null, value); return; }
        PropertyInfo property = type.GetProperty(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if (property != null) { property.SetValue(null, value); return; }
        throw new InvalidOperationException("no static member " + name + " on " + type.Name);
    }

    private static void Finish()
    {
        if (Errors.Count == 0) { Report.AppendLine(); }
        EditorApplication.update -= Tick;
        Directory.CreateDirectory("Builds/QA");
        Report.AppendLine();
        Report.AppendLine(Errors.Count == 0 ? "RC1_CONTROLS_RUN_PASS" : "RC1_CONTROLS_RUN_FAIL");
        foreach (string failure in Errors) { Report.AppendLine("  ! " + failure); }
        File.WriteAllText(ReportPath, Report.ToString());
        SessionState.SetBool(Flag, false);
        if (Errors.Count == 0) { Debug.Log("RC1_CONTROLS_RUN_PASS"); }
        else { Debug.LogError("RC1_CONTROLS_RUN_FAIL: " + string.Join(" | ", Errors)); }
        EditorApplication.Exit(Errors.Count == 0 ? 0 : 1);
    }

    /// <summary>
    /// The smallest <see cref="GameMode"/> that can hold a running contraption: LevelManager reaches the
    /// contraption through its game mode, so one has to exist for the gate to have anything to read.
    /// </summary>
    private sealed class Rc1AuditGameMode : GameMode
    {
        public override void OnDataLoadedStart() { }
        public override void InitGameMode() { }
        public override void OnDataLoadedDone() { }
        public override LevelManager.GameState SetGameState(LevelManager.GameState currentState,
                                                            LevelManager.GameState newState)
        {
            return newState;
        }
        protected override bool HandleUIEvent(UIEvent data) { return false; }
        public override int GetPartCount(BasePart.PartType type) { return 0; }
    }
}
