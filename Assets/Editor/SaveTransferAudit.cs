using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Checks the three file actions added to 载具存档设置 - 导出（存档）, 导入（存档）, 打开存档位置 - and the
/// stutter fixes that came with the same report: the menu background is built once instead of on every menu
/// screen, the episode list's per-frame collider lookups are cached, and CrossPromoButton no longer logs a
/// warning every time the level list is built.
/// </summary>
/// <remarks>
/// The native save/open dialogs cannot be answered in a batch run, so what is checked here is everything
/// around them: that an import lands in the file and format the loader actually reads - proved by loading it
/// back through <c>WPFPrefs.LoadContraptionDataset</c>, which is the call a level makes - that the save it
/// replaced is gone rather than kept as a <c>.bak</c>, that a file which is not a save is refused, and that
/// the menu background is reused rather than rebuilt.
/// </remarks>
[InitializeOnLoad]
public static class SaveTransferAudit
{
    private const string Flag = "SaveTransferAudit";

    private const string ReportPath = "Builds/QA/save-transfer.txt";

    private static readonly List<string> Errors = new List<string>();

    private static readonly StringBuilder Report = new StringBuilder();

    private static string s_root;

    static SaveTransferAudit()
    {
        EditorApplication.playModeStateChanged += StateChanged;
    }

    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Flag, true);

        // No -quit: the settings, the resource data and the singletons only exist in play mode.
        double hardStop = EditorApplication.timeSinceStartup + 240;
        EditorApplication.update += () =>
        {
            if (!EditorApplication.isPlaying && EditorApplication.timeSinceStartup >= hardStop)
            {
                Debug.LogWarning("Save transfer audit never reached play mode; giving up.");
                SessionState.SetBool(Flag, false);
                EditorApplication.Exit(2);
            }
        };
        EditorApplication.EnterPlaymode();
    }

    private static void StateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Flag, false)) { return; }
        if (state != PlayModeStateChange.EnteredPlayMode) { return; }

        try
        {
            new GameObject("QA game services", typeof(INGameManager));
            INUnity.Initialize(AssetDatabase.LoadAssetAtPath<ResourceData>("Assets/MonoBehaviour/ResourceData.asset"));
            s_root = Path.GetFullPath("Builds/QA/SaveTransfer");
            if (Directory.Exists(s_root)) { Directory.Delete(s_root, recursive: true); }
            Directory.CreateDirectory(s_root);
            SetStatic(typeof(INUnity), "DataPath", s_root);
            SetStatic(typeof(INUnity), "SettingsPath", Path.Combine(s_root, "Settings"));

            INSettings.Initialize(0);
            // The isolated root starts from the version's defaults, and the new-format feature is what decides
            // which file and folder the loader uses; switch it on explicitly so the audit checks one known
            // arrangement instead of whatever the default happens to be.
            INSettings.SetSettingsValue("NewContraptionData", true);
            INContraptionDataManager.SetContraptionData();
            if (INUserSettings.Instance != null)
            {
                INUserSettings.Instance.ContraptionDataSettings.Enabled = true;
            }
            CheckSaveDirectory();
            CheckExportSource();
            CheckImportOfNewFormat();
            CheckImportRefusesForeignFile();
            CheckImportOfOriginalFormat();
            CheckImportWhileTheLoaderWantsOriginalFormat();
            CheckMenuBackgroundCache();
            CheckSourceContracts();
        }
        catch (Exception exception)
        {
            Errors.Add(exception.ToString());
        }

        Finish();
    }

    private static void CheckSaveDirectory()
    {
        string directory = INContraptionSaveFiles.SaveDirectory;
        Report.AppendLine("=== save directory ===");
        Report.AppendLine("  version folder: " + directory);
        Report.AppendLine("  the loader's:   " + INContraptionSaveFiles.LoaderDirectory);
        Report.AppendLine("  loader reads the new format: " + INContraptionSaveFiles.LoaderUsesNewFormat);
        Require(directory.StartsWith(s_root, StringComparison.OrdinalIgnoreCase), "the saves live under the data root");
        Require(Directory.Exists(directory), "the directory is created on demand");
        Require(!directory.EndsWith("contraptions", StringComparison.Ordinal) || INSettings.VersionType == 0, "version 0 keeps them in 'contraptions'");
        Require(INContraptionSaveFiles.LoaderDirectory == directory, "with the feature on, the loader reads the version folder");
        Require(INContraptionSaveFiles.LoaderUsesNewFormat, "and it reads the new format while the data setting is on");
    }

    /// <summary>Writes a real save and checks the export side can find it.</summary>
    private static void CheckExportSource()
    {
        Report.AppendLine();
        Report.AppendLine("=== what 导出 would copy ===");
        const string level = "QA_Level_Export";
        string path = WriteSave(level, 3, 3);
        Report.AppendLine("  saved " + path);

        string found = INContraptionSaveFiles.ExistingSavePath(level);
        Report.AppendLine("  ExistingSavePath: " + found);
        Require(found == path, "the save just written is the file 导出 copies");
        Require(File.Exists(found), "that file exists");
        Require(INContraptionSaveFiles.ExistingSavePath("QA_Level_Missing") == null, "a level with no save has nothing to export");
    }

    private static void CheckImportOfNewFormat()
    {
        Report.AppendLine();
        Report.AppendLine("=== 导入 of a new-format save ===");
        const string level = "QA_Level_New";
        string target = WriteSave(level, 1, 1);
        string original = File.ReadAllText(target);
        string imported = SaveText(7, 7);
        string picked = Path.Combine(s_root, "picked_new.csv");
        File.WriteAllText(picked, imported);
        string message;
        bool installed = INContraptionSaveFiles.Install(picked, level, out message);
        Report.AppendLine("  Install -> " + installed + " (" + message + ")");
        Require(installed, "a new-format save is accepted");
        Require(File.ReadAllText(target) == imported, "the imported parts are what the game will read");
        Require(!File.Exists(target + ".bak"), "the save it replaced is gone rather than kept as .bak");

        // The check that matters: the game's own loader has to hand back the imported parts. Reading the file
        // directly would only prove the copy happened, not that it landed where and how the loader looks.
        ContraptionDataset loaded = WPFPrefs.LoadContraptionDataset(level);
        Report.AppendLine("  LoadContraptionDataset -> " + Describe(loaded));
        Require(loaded != null && loaded.ContraptionDatasetList != null && loaded.ContraptionDatasetList.Count == 7,
            "the loader reads back the imported 7 parts");
        Require(original != imported, "and that is not what was there before");
    }

    private static void CheckImportRefusesForeignFile()
    {
        Report.AppendLine();
        Report.AppendLine("=== 导入 of a file that is not a save ===");
        const string level = "QA_Level_Foreign";
        string target = WriteSave(level, 2, 2);
        string original = File.ReadAllText(target);
        string picked = Path.Combine(s_root, "notes.txt");
        File.WriteAllText(picked, "this is not a contraption save");

        string message;
        bool installed = INContraptionSaveFiles.Install(picked, level, out message);
        Report.AppendLine("  Install -> " + installed + " (" + message + ")");
        Require(!installed, "the file is refused");
        Require(File.ReadAllText(target) == original, "the save that was there is untouched");
    }

    private static void CheckImportOfOriginalFormat()
    {
        Report.AppendLine();
        Report.AppendLine("=== 导入 of an original-format save ===");
        const string level = "QA_Level_Original";
        string newFormat = WriteSave(level, 5, 1);
        string picked = Path.Combine(s_root, "picked_original.contraption");
        // The encrypted original format is binary, which is how this one is told apart from text that is not
        // a save at all; the game decrypts and migrates it on the next load.
        File.WriteAllBytes(picked, new byte[] { 0x00, 0x1f, 0x8b, 0x41, 0x00, 0x52 });

        string message;
        bool installed = INContraptionSaveFiles.Install(picked, level, out message);
        string legacy = Path.Combine(INContraptionSaveFiles.SaveDirectory, WPFPrefs.ContraptionFileName(level));
        Report.AppendLine("  Install -> " + installed + " (" + message + ")");
        Report.AppendLine("  legacy path: " + legacy);
        Require(installed, "a binary save is accepted");
        Require(File.Exists(legacy), "it lands under the hashed original name");
        Require(!File.Exists(newFormat), "the new-format file is moved aside so the imported one is the one read");
        Require(!File.Exists(newFormat + ".bak") && !File.Exists(legacy + ".bak"), "nothing is kept as .bak");
    }

    /// <summary>
    /// The case that used to do nothing at all: the loader is on the original format, so a new-format pick has
    /// to be converted instead of being copied where nothing would ever read it.
    /// </summary>
    private static void CheckImportWhileTheLoaderWantsOriginalFormat()
    {
        Report.AppendLine();
        Report.AppendLine("=== 导入 while the loader wants the original format ===");
        const string level = "QA_Level_Converted";
        // Switching the feature off is what puts WPFPrefs - and therefore the original format, in a folder
        // without a version letter - in charge of the level's save.
        INSettings.SetSettingsValue("NewContraptionData", false);
        INContraptionDataManager.SetContraptionData();
        Report.AppendLine("  loader directory: " + INContraptionSaveFiles.LoaderDirectory);
        Report.AppendLine("  loader reads the new format: " + INContraptionSaveFiles.LoaderUsesNewFormat);
        Require(!INContraptionSaveFiles.LoaderUsesNewFormat, "with the feature off the loader reads the original format");
        Require(SamePath(INContraptionSaveFiles.LoaderDirectory, Path.Combine(s_root, "contraptions")), "and reads <DataPath>/contraptions, without a version letter");

        string picked = Path.Combine(s_root, "picked_converted.csv");
        File.WriteAllText(picked, SaveText(3, 4));
        string message;
        bool installed = INContraptionSaveFiles.Install(picked, level, out message);
        string legacy = Path.Combine(INContraptionSaveFiles.LoaderDirectory, WPFPrefs.ContraptionFileName(level));
        Report.AppendLine("  Install -> " + installed + " (" + message + ")");
        Report.AppendLine("  converted save: " + legacy);
        Require(installed, "a new-format save is accepted by an original-format loader");
        Require(File.Exists(legacy), "it is converted into the encrypted original file");

        ContraptionDataset loaded = WPFPrefs.LoadContraptionDataset(level);
        Report.AppendLine("  LoadContraptionDataset -> " + Describe(loaded));
        Require(loaded != null && loaded.ContraptionDatasetList != null && loaded.ContraptionDatasetList.Count == 4,
            "the loader reads back the imported 4 parts");

        INSettings.SetSettingsValue("NewContraptionData", true);
        INContraptionDataManager.SetContraptionData();
    }

    /// <summary>A one-line summary of a dataset, for the report.</summary>
    private static string Describe(ContraptionDataset dataset)
    {
        int count = dataset != null && dataset.ContraptionDatasetList != null ? dataset.ContraptionDatasetList.Count : -1;
        return count < 0 ? "null" : count + " parts";
    }

    private static void CheckMenuBackgroundCache()
    {
        Report.AppendLine();
        Report.AppendLine("=== menu background ===");
        GameObject prefab = Resources.Load<GameObject>("Environment/Background/Background_Jungle_01_SET");
        if (prefab == null)
        {
            Report.AppendLine("  background prefab not found in Resources; skipped");
            return;
        }
        INMenuBackgroundCache.Show(string.Empty, prefab);
        GameObject first = GameObject.Find(prefab.name);
        Require(first != null, "the first call builds it");
        INMenuBackgroundCache.Show(string.Empty, prefab);
        GameObject second = GameObject.Find(prefab.name);
        Require(second != null && first != null && second.GetInstanceID() == first.GetInstanceID(), "the second call reuses the same instance");
        INMenuBackgroundCache.Hide();
        Require(!second.activeSelf, "it is hidden when a scene unloads");
        INMenuBackgroundCache.Show(string.Empty, prefab);
        Require(GameObject.Find(prefab.name) != null && GameObject.Find(prefab.name).activeSelf, "and shown again by the next menu");
        Report.AppendLine("  reused instance " + first.GetInstanceID() + " across three menu entries");
    }

    /// <summary>
    /// The parts of this change that only exist as text: the panel rows, the button caption fix and the
    /// product rename.
    /// </summary>
    private static void CheckSourceContracts()
    {
        Report.AppendLine();
        Report.AppendLine("=== source contracts ===");
        string panel = ReadSource("Assets/Scripts/Assembly-CSharp/INSettingsInterface.cs");
        Require(panel.Contains("GenerateContraptionFileRows"), "the settings panel builds the file action rows");
        Require(panel.Contains("ContraptionDataSettings_ExportButton"), "the export row is captioned");
        Require(panel.Contains("ContraptionDataSettings_ImportButton"), "the import row is captioned");
        Require(panel.Contains("ContraptionDataSettings_OpenFolderButton"), "the open-folder row is captioned");
        Require(typeof(INSettingsInterface).GetNestedType("SettingsItemWithButton", BindingFlags.NonPublic) != null, "the action row type exists");

        string localization = ReadSource("Assets/TextAsset/INLocalizationData.json");
        foreach (string id in new[] { "ContraptionDataSettings_Export", "ContraptionDataSettings_Import", "ContraptionDataSettings_OpenFolder" })
        {
            Require(localization.Contains("\"" + id + "\""), id + " is localised");
        }
        Require(localization.Contains("\"zh-Hans\": \"导出（存档）\""), "导出（存档） is the Chinese label");
        Require(localization.Contains("\"zh-Hans\": \"导入（存档）\""), "导入（存档） is the Chinese label");
        Require(localization.Contains("\"zh-Hans\": \"打开存档位置\""), "打开存档位置 is the Chinese label");

        string crossPromo = ReadSource("Assets/Scripts/Assembly-CSharp/CrossPromoButton.cs");
        // The file's own comment explains what was removed, so look for calls, not for the words.
        Require(!crossPromo.Contains("Debug.LogWarning(") && !crossPromo.Contains("Debug.Log("), "CrossPromoButton no longer logs a warning per instantiation");
        Require(crossPromo.Contains("OnRenderableReady"), "its cross-promo handling is unchanged");

        string skin = ReadSource("Assets/Scripts/Assembly-CSharp/HeatUiSkin.cs");
        Require(skin.Contains("new Vector2(200, 44)"), "the action buttons are 200 wide so Chinese captions fit");
        Require(skin.Contains("enableWordWrapping = false"), "and their captions cannot wrap");

        Require(ProjectBranding.ProductName == "新创Rover", "the product name constant is 新创Rover");
        Require(PlayerSettings.productName == "新创Rover", "ProjectSettings.productName is 新创Rover");
        Require(localization.Contains("\"zh-Hans\": \"新创Rover\""), "the in-game title is 新创Rover");
        Require(Array.IndexOf(LegacySaveMigration.LegacyProductNames, "新创Rover 预览版") >= 0, "saves written under the preview name are migrated");
        Require(Array.IndexOf(LegacySaveMigration.LegacyProductNames, "新创Unity 科技版") >= 0, "and so are the older ones");
        Require(LegacySaveMigration.LegacyProductNames[0] == "新创Rover 预览版", "the newest legacy name is copied first");

        string selector = ReadSource("Assets/Scripts/Assembly-CSharp/CompactEpisodeSelector.cs");
        Require(selector.Contains("m_episodeColliders"), "the episode list caches its colliders");
        Require(!selector.Contains("gameObject.GetComponent<Collider>().enabled"), "the per-frame scaling pass no longer looks them up");

        // 导入 has to put the level onto the imported file, not just write it: the game saves its in-memory
        // contraption back over that file when the level is left, so a write-only import is undone unread.
        string files = ReadSource("Assets/Scripts/Assembly-CSharp/INContraptionSaveFiles.cs");
        Require(files.Contains("ApplyToRunningLevel"), "an import is applied to the running level");
        Require(files.Contains("LoadContraptionSlot"), "by replaying the game's own slot load");
        Require(files.Contains("ClearContraption"), "which needs the build area cleared first");
        Require(files.Contains("SetContraptionProto"), "and the prototype dropped, or that load saves over the import");
        Require(files.Contains("SetContraptionProto(gameMode, null)") && files.Contains("property.SetValue(gameMode, contraption)"),
            "the prototype is dropped through its property, which is the only writable way in");
        Require(files.Contains("LoaderUsesNewFormat") && files.Contains("LoaderDirectory"), "which file the loader reads decides where an import lands");
        Require(files.Contains("parsed.ConvertTo()"), "a new-format save imported into an original-format loader is converted");
        Require(!files.Contains("Backup("), "an import replaces the old save outright, with no .bak copy");

        // The apply above leans on two things in the game's own code: a level loads through
        // WPFPrefs.LoadContraptionDataset, and GameMode.LoadContraptionFromSlot only saves the contraption it
        // is replacing while a prototype exists - which is why the prototype is dropped before it is called.
        string baseMode = ReadSource("Assets/Scripts/Assembly-CSharp/BaseGameMode.cs");
        Require(baseMode.Contains("WPFPrefs.LoadContraptionDataset(GetCurrentContraptionName())"),
            "a level loads its contraption through WPFPrefs.LoadContraptionDataset");
        string mode = ReadSource("Assets/Scripts/Assembly-CSharp/GameMode.cs");
        Require(mode.Contains("public Contraption ContraptionProto { get; protected set; }"),
            "the prototype's setter is protected, so it is released through its property");
        int guard = mode.IndexOf("if ((bool)ContraptionProto)", StringComparison.Ordinal);
        int save = mode.IndexOf("ContraptionProto.SaveContraption(GetCurrentContraptionName())", StringComparison.Ordinal);
        Require(guard >= 0 && save > guard, "the slot load saves the contraption it replaces only while a prototype exists");
    }

    private static string WriteSave(string level, int partType, int count)
    {
        Directory.CreateDirectory(INContraptionSaveFiles.SaveDirectory);
        string path = Path.Combine(INContraptionSaveFiles.SaveDirectory, level);
        File.WriteAllText(path, SaveText(partType, count));
        return path;
    }

    /// <summary>
    /// One CSV row per part - <c>Type,Index,X,Y,Rotation,Flipped</c>, all integers - which is the layout
    /// <c>INContraptionDataManager.TryLoadDataFromCsv</c> parses and the one <c>SaveAsCsv</c> writes.
    /// </summary>
    private static string SaveText(int partType, int count)
    {
        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < count; i++)
        {
            builder.Append(partType).Append(',').Append(i).Append(",0,0,0,0").AppendLine();
        }
        return builder.ToString();
    }

    private static string ReadSource(string relativePath)
    {
        string path = Path.GetFullPath(relativePath);
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }

    /// <summary>Compares two paths as paths, not as strings: one may use '/' where the other uses '\'.</summary>
    private static bool SamePath(string left, string right)
    {
        if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
        {
            return false;
        }
        return string.Equals(Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
    }

    private static void Require(bool condition, string what)
    {
        Checks++;
        Report.AppendLine("  [" + (condition ? "ok" : "FAIL") + "] " + what);
        if (!condition)
        {
            Errors.Add(what);
        }
    }

    private static void SetStatic(Type type, string property, object value)
    {
        type.GetProperty(property, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .SetValue(null, value);
    }

    private static void Finish()
    {
        Report.AppendLine();
        Report.AppendLine("Checks: " + Checks + ", Failures: " + Errors.Count);
        foreach (string message in Errors) { Report.AppendLine("FAILED: " + message); }
        Directory.CreateDirectory("Builds/QA");
        File.WriteAllText(ReportPath, Report.ToString());
        Debug.Log("[SaveTransferAudit] " + (Errors.Count == 0 ? "PASS" : "FAIL") + " - see " + ReportPath);
        SessionState.SetBool(Flag, false);
        EditorApplication.ExitPlaymode();
        EditorApplication.Exit(Errors.Count == 0 ? 0 : 1);
    }

    private static int Checks;
}
