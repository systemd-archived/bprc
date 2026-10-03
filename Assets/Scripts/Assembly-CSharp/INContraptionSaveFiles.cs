using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Innovation;
using UnityEngine;

/// <summary>
/// Export, import and reveal the contraption ("载具存档") save file of the level the player is on.
/// </summary>
/// <remarks>
/// The three actions the 载具存档设置 group offers are built on this class. They all work on the single
/// save file the game itself would read for the current level, so what is exported is exactly what the
/// game would load, and an imported file lands where the game looks for it:
/// <list type="bullet">
/// <item><c>INFeature.NewContraptionData</c> on and <c>ContraptionDataSettings.Enabled</c> on: the new-format
/// file <c>&lt;DataPath&gt;/contraptions[|O|A|B]/&lt;levelName&gt;</c>, extensionless, CSV or JSON;</item>
/// <item>either switch off: the original encrypted
/// <c>&lt;DataPath&gt;/contraptions[|O|A|B]/&lt;hash of levelName&gt;.contraption</c>, which the game migrates on
/// load. Note that the feature being off means <c>&lt;DataPath&gt;/contraptions</c> without a version
/// letter - that is <c>WPFPrefs</c>, not the manager.</item>
/// </list>
/// An imported file always replaces the old save outright; no <c>.bak</c> copy is kept.
/// <para>
/// Windows exports through ShellFileDialogs. Android uses ACTION_CREATE_DOCUMENT to export through
/// the system file manager and ACTION_OPEN_DOCUMENT to import via a content-provider cache copy.
/// </para>
/// </remarks>
public static class INContraptionSaveFiles
{
    /// <summary>Extension the exported copy is given when the save file itself has none.</summary>
    private const string ExportExtension = ".contraption";

    /// <summary>
    /// The directory the game's loader reads the current level's contraption from.
    /// </summary>
    /// <remarks>
    /// <c>WPFPrefs.LoadContraptionDataset</c> is what a level calls. With <c>INFeature.NewContraptionData</c>
    /// off it reads the original format from <c>&lt;DataPath&gt;/contraptions</c> - a folder name that does not
    /// follow the settings version - and with it on it goes through <see cref="INContraptionDataManager"/>,
    /// which uses the version's folder. Exporting and importing have to land on the same one, or the game
    /// never sees what was written.
    /// </remarks>
    public static string LoaderDirectory
    {
        get
        {
            if (!INSettings.GetBool(INFeature.NewContraptionData))
            {
                return EnsureDirectory(INUnity.DataPath + "/contraptions");
            }
            INContraptionDataManager manager = INContraptionDataManager.Instance;
            return EnsureDirectory(manager != null ? manager.DataDirectory : SaveDirectory);
        }
    }

    /// <summary>
    /// True while the loader reads the new-format file written next to the level name, rather than the
    /// original encrypted <c>&lt;hash&gt;.contraption</c>.
    /// </summary>
    /// <remarks>
    /// Both switches have to be on: the feature decides whether the manager is in the way at all, and
    /// <c>ContraptionDataSettings.Enabled</c> decides whether the manager reads the new file or the original
    /// one in its own folder (<c>INContraptionDataManager.LoadContraptionData</c>).
    /// </remarks>
    public static bool LoaderUsesNewFormat
    {
        get
        {
            INUserSettings settings = INUserSettings.Instance;
            return INSettings.GetBool(INFeature.NewContraptionData)
                && settings != null
                && settings.ContraptionDataSettings.Enabled;
        }
    }

    /// <summary>
    /// The directory the current settings version keeps its contraptions in.
    /// </summary>
    /// <remarks>
    /// Recomputed rather than read from <see cref="INContraptionDataManager"/>, whose <c>Instance</c> is
    /// null whenever <c>INFeature.NewContraptionData</c> is off, and which is the same expression
    /// <c>INContraptionDataManager.Initialize</c> uses.
    /// </remarks>
    public static string SaveDirectory
    {
        get
        {
            string suffix = INSettings.VersionType switch
            {
                2 => "A",
                1 => "O",
                0 => string.Empty,
                _ => "B",
            };
            return EnsureDirectory(INUnity.DataPath + "/contraptions" + suffix);
        }
    }

    private static string EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
        return path;
    }

    /// <summary>
    /// The level name the current level saves its contraption under, or null when no level is running.
    /// </summary>
    public static string CurrentLevelName()
    {
        try
        {
            LevelManager levelManager = WPFMonoBehaviour.levelManager;
            if (levelManager == null)
            {
                return null;
            }
            GameMode gameMode = levelManager.CurrentGameMode;
            if (gameMode == null)
            {
                return null;
            }
            string name = gameMode.GetCurrentContraptionName();
            return string.IsNullOrEmpty(name) ? null : name;
        }
        catch (Exception)
        {
            // WPFMonoBehaviour.levelManager throws rather than returning null when no level is loaded.
            return null;
        }
    }

    /// <summary>The file the game would read for <paramref name="levelName"/>, or null when it has none.</summary>
    /// <remarks>
    /// Mirrors <c>WPFPrefs.LoadContraptionDataset</c> and the manager behind it: the new-format file answers
    /// only while the loader is in new-format mode, and the original one answers otherwise - and also answers
    /// in new-format mode when it is the only file there, because the game migrates it on the next load.
    /// </remarks>
    public static string ExistingSavePath(string levelName)
    {
        if (string.IsNullOrEmpty(levelName))
        {
            return null;
        }
        string directory = LoaderDirectory;
        if (LoaderUsesNewFormat)
        {
            string current = Path.Combine(directory, levelName);
            if (File.Exists(current))
            {
                return current;
            }
        }
        string original = Path.Combine(directory, WPFPrefs.ContraptionFileName(levelName));
        return File.Exists(original) ? original : null;
    }

    /// <summary>
    /// 导出（存档）: writes a copy of the current level's save file where the player asks for it.
    /// </summary>
    public static void ExportCurrentSave()
    {
        string levelName = CurrentLevelName();
        if (levelName == null)
        {
            Feedback("当前没有正在进行的关卡，无法导出载具存档");
            return;
        }
        string source = ExistingSavePath(levelName);
        if (source == null)
        {
            Feedback("当前关卡还没有载具存档：" + levelName);
            return;
        }
        // The new-format file is already named after the level, but the original one is named after a hash of
        // it, so the exported copy is always named after the level rather than after that hash.
        string suggested = levelName + ExportExtension;
        ExportThroughSaveDialog(source, suggested);
    }

    /// <summary>
    /// 导入（存档）: replaces the current level's save file with one the player picks, and puts the running
    /// level onto it.
    /// </summary>
    public static void ImportCurrentSave()
    {
        string levelName = CurrentLevelName();
        if (levelName == null)
        {
            Feedback("当前没有正在进行的关卡，无法导入载具存档");
            return;
        }
        INFilePicker.PickFile("载具存档", "*", LoaderDirectory, delegate(string picked)
        {
            if (string.IsNullOrEmpty(picked) || !File.Exists(picked))
            {
                return;
            }
            string message;
            if (Install(picked, levelName, out message))
            {
                Feedback(message + (ApplyToRunningLevel() ? "，已替换当前载具" : "，重新进入关卡后生效"));
            }
            else
            {
                Feedback(message);
            }
        });
    }

    /// <summary>
    /// 打开存档位置: reveals the folder the saves live in (Explorer on Windows, the file manager on Android).
    /// </summary>
    public static void OpenSaveFolder()
    {
        OpenFolder(LoaderDirectory);
    }

    /// <summary>Opens <paramref name="path"/> in the platform's file manager.</summary>
    public static void OpenFolder(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }
        try
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            OpenFolderAndroid(path);
#else
            OpenFolderDesktop(path);
#endif
        }
        catch (Exception exception)
        {
            Debug.LogWarning("INContraptionSaveFiles.OpenFolder: " + exception.Message);
            Feedback("无法打开目录：" + path);
        }
    }

    /// <summary>
    /// Replaces the save the game reads for <paramref name="levelName"/> with <paramref name="pickedPath"/>.
    /// </summary>
    /// <remarks>
    /// The old save is replaced outright - nothing is kept as a <c>.bak</c> - and the format is decided from
    /// the file's content, not its name, because the save dialog suggests <c>.contraption</c> for the
    /// extensionless new-format files. Which file is written, and in which format, follows the loader:
    /// <list type="bullet">
    /// <item>new-format loader: a new-format pick is copied to the level's own name; an original pick is put
    /// at the hashed name with the new-format file removed, because the loader only reaches the original one
    /// when that file is absent, and migrates it there.</item>
    /// <item>original-format loader: an original pick is copied to the hashed name, and a new-format pick is
    /// converted through the game's own serializer - the loader would never look at a new-format file, which
    /// is why importing one used to do nothing at all.</item>
    /// </list>
    /// A file that is neither parseable new-format data nor the encrypted original format is refused, so a
    /// wrong pick cannot destroy a working save.
    /// </remarks>
    public static bool Install(string pickedPath, string levelName, out string message)
    {
        message = string.Empty;
        byte[] bytes = null;
        string text = null;
        try
        {
            bytes = File.ReadAllBytes(pickedPath);
            text = Encoding.UTF8.GetString(bytes);
        }
        catch (Exception)
        {
            bytes = null;
            text = null;
        }

        BpirSaveConverter.Result converted = null;
        bool bpirText = BpirSaveConverter.LooksLikeExtendedText(text);
        bool bpir = BpirSaveConverter.TryConvert(bytes, true, out converted, out string conversionError);
        if (!bpir && LooksBinary(bytes))
        {
            try { bpirText |= BpirSaveConverter.LooksLikeExtendedText(Encoding.UTF8.GetString(new CryptoUtility("3b91A049Ca7HvSjhxT35").Decrypt(bytes, 0))); }
            catch { }
        }
        if (bpirText && !bpir)
        {
            message = conversionError ?? "无法识别 BPIR 存档";
            return false;
        }
        INContraptionData parsed = bpir ? converted.data : ParseNewFormat(text);
        bool isNewFormat = parsed != null;
        bool isOriginalFormat = !isNewFormat && IsOriginalSave(bytes);
        if (!isNewFormat && !isOriginalFormat)
        {
            message = "这个文件不是载具存档，已取消导入";
            return false;
        }

        string directory = LoaderDirectory;
        Directory.CreateDirectory(directory);
        string currentPath = Path.Combine(directory, levelName);
        string originalPath = Path.Combine(directory, WPFPrefs.ContraptionFileName(levelName));
        try
        {
            if (LoaderUsesNewFormat)
            {
                if (isNewFormat)
                {
                    if (INContraptionDataManager.Settings.SaveFormat == ContraptionDataSettings.SerializationFormat.JSON)
                    {
                        using var writer = new StreamWriter(currentPath, false, new UTF8Encoding(false));
                        Json.Serialize(writer, parsed);
                    }
                    else File.WriteAllText(currentPath, BpirSaveConverter.SerializeCsv(parsed), new UTF8Encoding(false));
                }
                else
                {
                    // LoadContraptionData prefers the new-format file, so an imported original save only takes
                    // effect once that file is out of the way; the game then migrates the original on load.
                    if (File.Exists(currentPath))
                    {
                        File.Delete(currentPath);
                    }
                    File.Copy(pickedPath, originalPath, overwrite: true);
                }
            }
            else if (isOriginalFormat)
            {
                File.Copy(pickedPath, originalPath, overwrite: true);
            }
            else
            {
                WPFPrefs.SaveOriginalContraptionDataset(directory, levelName, parsed.ConvertTo());
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning("INContraptionSaveFiles.Install: " + exception.Message);
            message = "导入失败：" + exception.Message;
            return false;
        }

        message = bpir ? converted.Summary + "，已导入 " + levelName : "已导入载具存档 " + levelName;
        return true;
    }

    /// <summary>
    /// Rebuilds the running level's contraption from the save file that was just written.
    /// </summary>
    /// <remarks>
    /// Writing the file is not enough while a level is up: the game keeps the contraption in memory and saves
    /// it back to that same file when the level is left, so an import that only writes the file is overwritten
    /// before it is ever read. This replays the game's own slot load - back to the build state, clear the build
    /// area, drop the prototype, then <c>LoadContraptionSlot</c>, which rebuilds from the file.
    /// <para>
    /// The prototype is dropped through its property because <c>LoadContraptionFromSlot</c> saves the current
    /// contraption before loading; with no prototype that save is skipped, which is exactly what stops the
    /// contraption being replaced from writing itself back over the import. The state change comes first,
    /// because the building state reaches for the prototype and because leaving a running state is what stops
    /// the contraption being replaced.
    /// </para>
    /// Returns false when there is no build area to rebuild into (no level, or a level without build UI), in
    /// which case the file is still in place and the next level load picks it up.
    /// </remarks>
    private static bool ApplyToRunningLevel()
    {
        try
        {
            LevelManager levelManager = WPFMonoBehaviour.levelManager;
            GameMode gameMode = levelManager != null ? levelManager.CurrentGameMode : null;
            ConstructionUI constructionUI = levelManager != null ? levelManager.ConstructionUI : null;
            if (gameMode == null || constructionUI == null)
            {
                return false;
            }
            switch (levelManager.gameState)
            {
            case LevelManager.GameState.Building:
            case LevelManager.GameState.PreviewWhileBuilding:
            case LevelManager.GameState.PausedWhileBuilding:
                break;
            default:
                levelManager.SetGameState(LevelManager.GameState.Building);
                break;
            }
            Contraption running = gameMode.ContraptionRunning;
            if (running != null)
            {
                gameMode.ContraptionRunning = null;
                UnityEngine.Object.Destroy(running.gameObject);
            }
            constructionUI.ClearContraption();
            Contraption proto = gameMode.ContraptionProto;
            SetContraptionProto(gameMode, null);
            if (proto != null)
            {
                UnityEngine.Object.Destroy(proto.gameObject);
            }
            gameMode.LoadContraptionSlot(gameMode.CurrentContraptionIndex);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("INContraptionSaveFiles.ApplyToRunningLevel: " + exception.Message);
            return false;
        }
    }

    /// <summary>
    /// Sets <c>GameMode.ContraptionProto</c>, whose setter is protected.
    /// </summary>
    private static void SetContraptionProto(GameMode gameMode, Contraption contraption)
    {
        PropertyInfo property = typeof(GameMode).GetProperty("ContraptionProto", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null && property.CanWrite)
        {
            property.SetValue(gameMode, contraption);
        }
    }

    /// <summary>Reads new-format data the way the loader would, or null when the text is not new-format data.</summary>
    private static INContraptionData ParseNewFormat(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }
        if (INContraptionDataManager.TryLoadDataFromCsv(text, out INContraptionData csv))
        {
            return csv;
        }
        try
        {
            return INContraptionDataManager.LoadDataFromJson(text);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// True for data that is not text.
    /// </summary>
    /// <remarks>
    /// The original format is the XML dataset encrypted by <c>WPFPrefs</c>, so its bytes look random -
    /// around a third of them are control bytes - while a new-format save is CSV or JSON and is printable
    /// throughout. A few control bytes are required rather than one, so that a lone stray byte in a text
    /// file cannot pass for an encrypted save.
    /// </remarks>
    private static bool LooksBinary(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
        {
            return false;
        }
        int sample = Math.Min(bytes.Length, 4096);
        int control = 0;
        for (int i = 0; i < sample; i++)
        {
            byte value = bytes[i];
            if (value != 9 && value != 10 && value != 13 && value < 32)
            {
                control++;
            }
        }
        return control * 20 >= sample;
    }

    private static bool IsOriginalSave(byte[] bytes)
    {
        if (!LooksBinary(bytes)) return false;
        try
        {
            using var stream = new MemoryStream(new CryptoUtility("3b91A049Ca7HvSjhxT35").Decrypt(bytes, 0));
            using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            return new XmlSerializer(typeof(ContraptionDataset)).Deserialize(reader) is ContraptionDataset dataset && dataset.ContraptionDatasetList != null;
        }
        catch { return false; }
    }

    private static void ExportThroughSaveDialog(string source, string suggestedName)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        ExportThroughAndroidDialog(source, suggestedName);
#else
        try
        {
            ShellFileDialogs.Filter[] filters = new ShellFileDialogs.Filter[1]
            {
                new ShellFileDialogs.Filter("载具存档", new string[1] { "*" })
            };
            string destination = ShellFileDialogs.FileSaveDialog.ShowDialog(IntPtr.Zero, "导出载具存档", LoaderDirectory, suggestedName, filters, 0);
            if (string.IsNullOrEmpty(destination))
            {
                return;
            }
            File.Copy(source, destination, overwrite: true);
            Feedback("载具存档已导出：" + destination);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("INContraptionSaveFiles.ExportThroughSaveDialog: " + exception.Message);
            Feedback("导出失败：" + exception.Message);
        }
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static void ExportThroughAndroidDialog(string source, string suggestedName)
    {
        string snapshot = null;
        try
        {
            // Freeze the selected save while the user browses destinations in another activity.
            snapshot = Path.Combine(Application.temporaryCachePath, "save_export_" + Guid.NewGuid().ToString("N"));
            File.Copy(source, snapshot);
            string pending = snapshot;
            INFilePicker.SaveFileAndroid(pending, suggestedName, (destination, error) =>
            {
                try
                {
                    if (!string.IsNullOrEmpty(error)) Feedback("导出失败：" + error);
                    else if (!string.IsNullOrEmpty(destination)) Feedback("载具存档已导出到所选位置");
                }
                finally
                {
                    try { File.Delete(pending); }
                    catch (Exception ex) { Debug.LogWarning("Export cache cleanup: " + ex.Message); }
                }
            });
        }
        catch (Exception ex)
        {
            try { if (snapshot != null) File.Delete(snapshot); }
            catch (Exception cleanup) { Debug.LogWarning("Export cache cleanup: " + cleanup.Message); }
            Feedback("导出失败：" + ex.Message);
        }
    }
#endif

    private static void OpenFolderDesktop(string path)
    {
#if !UNITY_ANDROID || UNITY_EDITOR
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = "\"" + path.Replace('/', '\\') + "\"",
                UseShellExecute = true,
            });
            return;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("INContraptionSaveFiles.OpenFolderDesktop: " + exception.Message);
        }
        // Nothing else on this platform knows how to reveal a directory, so at least hand it to the shell.
        Application.OpenURL("file://" + path);
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static void OpenFolderAndroid(string path)
    {
        INFilePicker.OpenFolderAndroid(path, (result, error) =>
        {
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogWarning("INContraptionSaveFiles.OpenFolderAndroid: " + error);
                Feedback("打开文件管理器失败：" + error + "\n存档目录：" + path);
            }
        });
    }
#endif

    private static void Feedback(string message)
    {
        Debug.Log("INContraptionSaveFiles: " + message);
        BP.Feedback("BP> " + message);
    }
}
