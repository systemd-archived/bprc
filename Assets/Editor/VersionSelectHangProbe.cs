using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Drives the boot sequence headlessly from the version selector into a level, then dumps the numbers
/// that decide whether the build-mode view is sane (camera size and position, the level's construction
/// offset, the per-version camera/terrain settings, and the per-version contraption data folder).
///
/// It is deliberately verbose and never assumes it is on schedule: every tick logs, so if the main
/// thread wedges the last line of the log names the phase. Run it twice, once with -Version 2 (新创A)
/// and once with -Version 3 (新创B), to see what the version switch changes.
///
/// Run: Unity.exe -batchmode -nographics -projectPath . -executeMethod VersionSelectHangProbe.Run
/// Optional: -ProbeVersion &lt;0-3&gt;  -ProbeEpisode &lt;scene&gt;  -ProbeLevel &lt;index&gt;  -ProbeTag &lt;label&gt;
/// The names are prefixed because Unity reads "-version" itself and quits after printing its build.
/// </summary>
public static class VersionSelectHangProbe
{
    private const string BootScene = "Assets/Scene/Scenes/UI/SplashScreen.unity";
    private const string TuningPath = "Builds/QA/version-select-probe.txt";
    private const float BootPatience = 240f;      // the game's own boot can take minutes in batch
    private const float StepPatience = 120f;
    private const float Heartbeat = 1f;

    private enum Stage
    {
        WaitingForPlayMode,
        WaitingForSelector,
        WaitingForEnter,
        WaitingForMenu,
        WaitingForLevelScene,
        InLevel,
        Done
    }

    private static readonly List<string> s_log = new List<string>();
    private static Stage s_stage = Stage.WaitingForPlayMode;
    private static float s_next;
    private static float s_stageStart;
    private static float s_playStart;
    private static bool s_entered;
    private static int s_tick;
    private static int s_version = 3;
    private static string s_episode = "Episode1LevelSelection";
    private static int s_level = 0;
    private static string s_tag = "B";
    private static INVersionSelector s_selector;

    [MenuItem("Tools/Multiplayer/Probe Version Selection Hang")]
    public static void Run()
    {
        s_version = ArgInt("-ProbeVersion", 3);
        s_episode = Arg("-ProbeEpisode", "Episode1LevelSelection");
        s_level = ArgInt("-ProbeLevel", 0);
        s_tag = Arg("-ProbeTag", "v" + s_version);
        s_log.Clear();
        s_stage = Stage.WaitingForPlayMode;
        s_entered = false;
        s_tick = 0;
        EditorSceneManager.OpenScene(BootScene, OpenSceneMode.Single);
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        Log("probe start: version=" + s_version + " episode=" + s_episode + " level=" + s_level);
        EditorApplication.EnterPlaymode();
    }

    private static string Arg(string name, string fallback)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }
        return fallback;
    }

    private static int ArgInt(string name, int fallback)
    {
        int value;
        return int.TryParse(Arg(name, string.Empty), out value) ? value : fallback;
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        Log("play mode: " + change);
        if (change == PlayModeStateChange.EnteredPlayMode)
        {
            s_entered = true;
            s_playStart = Now();
            SetStage(Stage.WaitingForSelector);
        }
    }

    private static float Now()
    {
        return (float)EditorApplication.timeSinceStartup;
    }

    private static void SetStage(Stage stage)
    {
        s_stage = stage;
        s_stageStart = Now();
        s_next = Now();
        Log("stage -> " + stage);
    }

    private static void Tick()
    {
        if (s_stage == Stage.Done)
        {
            return;
        }
        float now = Now();
        if (!s_entered)
        {
            if (now >= s_next)
            {
                s_next = now + Heartbeat;
                Log(string.Format("tick {0} playing={1} entering={2}", ++s_tick,
                    EditorApplication.isPlaying, EditorApplication.isPlayingOrWillChangePlaymode));
            }
            if (EditorApplication.isPlaying && !s_entered)
            {
                s_entered = true;
                s_playStart = now;
                SetStage(Stage.WaitingForSelector);
            }
            return;
        }
        if (now >= s_next)
        {
            s_next = now + Heartbeat;
            Log(string.Format("tick {0} stage={1} scene={2} {3}", ++s_tick, s_stage,
                SceneManager.GetActiveScene().name, Snapshot()));
        }

        switch (s_stage)
        {
            case Stage.WaitingForSelector:
                if (TimedOut(BootPatience))
                {
                    Finish(1, "FAILED: the version selector never appeared");
                    return;
                }
                INVersionSelector selector = UnityEngine.Object.FindObjectOfType<INVersionSelector>();
                if (selector == null)
                {
                    return;
                }
                s_selector = selector;
                Log("selector up, buttons: " + Buttons(selector));
                Click(selector, s_version);
                SetStage(Stage.WaitingForEnter);
                return;

            case Stage.WaitingForEnter:
                if (s_selector == null)
                {
                    Finish(1, "FAILED: the selector vanished before Enter");
                    return;
                }
                ClickEnter(s_selector);
                SetStage(Stage.WaitingForMenu);
                return;

            case Stage.WaitingForMenu:
                if (SceneManager.GetActiveScene().name == "MainMenu")
                {
                    Log("main menu reached with version " + INSettings.VersionType + " (" + VersionLabel() + ")");
                    LogTuning();
                    GameManager manager = Singleton<GameManager>.Instance;
                    if (manager == null)
                    {
                        Finish(1, "FAILED: no GameManager at the main menu");
                        return;
                    }
                    Log("loading level selection scene " + s_episode);
                    manager.LoadLevelSelection(s_episode, showLoadingScreen: true);
                    SetStage(Stage.WaitingForLevelScene);
                    return;
                }
                if (TimedOut(StepPatience))
                {
                    Finish(1, "FAILED: the main menu never loaded");
                }
                return;

            case Stage.WaitingForLevelScene:
                if (SceneManager.GetActiveScene().name.Equals(s_episode, StringComparison.OrdinalIgnoreCase))
                {
                    Log("level selection scene is up (" + SceneManager.GetActiveScene().name + ")");
                    SetStage(Stage.InLevel);
                    s_next = Now() + 3f;
                    return;
                }
                if (TimedOut(StepPatience))
                {
                    Finish(1, "FAILED: " + s_episode + " never loaded");
                }
                return;

            case Stage.InLevel:
                if (s_next > now)
                {
                    return;
                }
                Log("DIAGNOSTICS " + Diagnostics());
                DumpHierarchy();
                Finish(0, "SUCCEEDED: reached the level selection under " + VersionLabel());
                return;
        }
    }

    private static bool TimedOut(float patience)
    {
        if (Now() - s_stageStart < patience)
        {
            return false;
        }
        Log(string.Format("stage {0} timed out after {1:0}s", s_stage, patience));
        return true;
    }

    private static string VersionLabel()
    {
        switch (INSettings.VersionType)
        {
            case 0: return "原版 (0)";
            case 1: return "新创O (1)";
            case 2: return "新创A (2)";
            default: return "新创B (3)";
        }
    }

    private static string Buttons(INVersionSelector selector)
    {
        List<string> parts = new List<string>();
        foreach (INVersionButton button in selector.GetComponentsInChildren<INVersionButton>(true))
        {
            parts.Add(button.name + "=type" + button.Type + (button.gameObject.activeSelf ? "" : "(inactive)"));
        }
        return string.Join(", ", parts.ToArray());
    }

    private static void Click(INVersionSelector selector, int version)
    {
        foreach (INVersionButton button in selector.GetComponentsInChildren<INVersionButton>(true))
        {
            if (button.Type == version)
            {
                Log("clicking " + button.name + " (type " + version + ")");
                button.onClick.Invoke();
                return;
            }
        }
        Log("no type " + version + " button; SelectVersion directly");
        selector.SelectVersion(version);
    }

    private static void ClickEnter(INVersionSelector selector)
    {
        foreach (INVersionButton button in selector.GetComponentsInChildren<INVersionButton>(true))
        {
            if (button.Type == -1 && button.gameObject.activeSelf)
            {
                Log("clicking enter button " + button.name);
                button.onClick.Invoke();
                return;
            }
        }
        Log("enter button not ready; EnterVersion directly");
        selector.EnterVersion();
    }

    private static string Snapshot()
    {
        StringBuilder builder = new StringBuilder();
        try
        {
            builder.Append("selected=").Append(INSettings.VersionSelected);
            builder.Append(" version=").Append(INSettings.VersionType);
            builder.Append(" bundle=").Append(Bundle.initialized).Append('/').Append(Bundle.checkingBundles);
            GameManager manager = Singleton<GameManager>.Instance;
            builder.Append(" state=").Append(manager == null ? "no-gm" : manager.GetGameState().ToString());
        }
        catch (Exception exception)
        {
            builder.Append(" snapshot threw ").Append(exception.GetType().Name);
        }
        return builder.ToString();
    }

    private static string F(INFeature feature)
    {
        try
        {
            return INSettings.GetFloat(feature).ToString("0.###");
        }
        catch (Exception exception)
        {
            return "!" + exception.GetType().Name;
        }
    }

    private static void LogTuning()
    {
        Log("TUNING version=" + VersionLabel()
            + " CameraInitialZoom=" + F(INFeature.CameraInitialZoom)
            + " CameraMaxZoom=" + F(INFeature.CameraMaxZoom)
            + " TerrainScale=" + F(INFeature.TerrainScale)
            + " NewCamera=" + SafeBool(INFeature.NewCamera));
    }

    private static string SafeBool(INFeature feature)
    {
        try
        {
            return INSettings.GetBool(feature).ToString();
        }
        catch (Exception exception)
        {
            return "!" + exception.GetType().Name;
        }
    }

    private static string Diagnostics()
    {
        StringBuilder builder = new StringBuilder();
        try
        {
            builder.Append("version=").Append(VersionLabel());
            builder.Append(" cameraInitialZoom=").Append(F(INFeature.CameraInitialZoom));
            builder.Append(" cameraMaxZoom=").Append(F(INFeature.CameraMaxZoom));
            builder.Append(" terrainScale=").Append(F(INFeature.TerrainScale));
            builder.Append(" newCamera=").Append(SafeBool(INFeature.NewCamera));
            string directory = "n/a";
            try
            {
                directory = INContraptionDataManager.Instance == null ? "null" : INContraptionDataManager.Instance.DataDirectory;
            }
            catch (Exception exception)
            {
                directory = "!" + exception.GetType().Name;
            }
            builder.Append(" contraptionDir=").Append(directory);
            LevelManager level = WPFMonoBehaviour.levelManager;
            if (level == null)
            {
                builder.Append(" level=null");
            }
            else
            {
                builder.Append(" levelState=").Append(level.gameState);
                builder.Append(" startPos=").Append(level.StartingPosition.ToString("0.###"));
                builder.Append(" constructionOffset=").Append(level.ConstructionOffset.ToString("0.###"));
                builder.Append(" protoParts=").Append(level.ContraptionProto == null ? -1 : level.ContraptionProto.Parts.Count);
            }
            Camera camera = null;
            IngameCamera ingame = UnityEngine.Object.FindObjectOfType<IngameCamera>();
            if (ingame != null)
            {
                camera = ingame.GetComponent<Camera>();
            }
            if (camera == null)
            {
                camera = Camera.main;
            }
            if (camera == null)
            {
                builder.Append(" camera=null");
            }
            else
            {
                builder.Append(" camera=").Append(camera.name);
                builder.Append(" ortho=").Append(camera.orthographicSize.ToString("0.###"));
                builder.Append(" pos=").Append(camera.transform.position.ToString("0.###"));
                builder.Append(" enabled=").Append(camera.enabled);
            }
        }
        catch (Exception exception)
        {
            builder.Append(" diagnostics threw ").Append(exception);
        }
        return builder.ToString();
    }

    private static void DumpHierarchy()
    {
        try
        {
            LevelManager level = WPFMonoBehaviour.levelManager;
            if (level == null)
            {
                return;
            }
            GameObject root = level.gameObject;
            int count = 0;
            StringBuilder builder = new StringBuilder();
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (count++ > 60)
                {
                    break;
                }
                builder.Append(child.name).Append('@').Append(child.position.ToString("0.#")).Append(" | ");
            }
            Log("HIERARCHY under " + root.name + ": " + builder);
        }
        catch (Exception exception)
        {
            Log("HIERARCHY dump failed: " + exception.Message);
        }
    }

    private static void Finish(int exitCode, string verdict)
    {
        s_stage = Stage.Done;
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        Write(verdict);
        Debug.Log("VERSION_SELECT_PROBE " + verdict);
        EditorApplication.Exit(exitCode);
    }

    private static void Write(string verdict)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Version selection probe " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        builder.AppendLine("tag: " + s_tag + "  verdict: " + verdict);
        builder.AppendLine();
        foreach (string line in s_log)
        {
            builder.AppendLine(line);
        }
        string path = TuningPath.Replace(".txt", "-" + s_tag + ".txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, builder.ToString());
    }

    private static void Log(string message)
    {
        string line = string.Format("[PROBE {0:0.0}] {1}", s_entered ? Now() - s_playStart : 0f, message);
        s_log.Add(line);
        Debug.Log(line);
    }
}
