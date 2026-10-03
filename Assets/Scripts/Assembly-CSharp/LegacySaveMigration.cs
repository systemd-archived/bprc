using System;
using System.IO;
using UnityEngine;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using Microsoft.Win32;
#endif

/// <summary>
/// The product name has changed twice - "新创Unity 科技版" to "新创Rover 预览版", and then to "新创Rover" -
/// and each change moves Unity's data folder (Application.persistentDataPath =
/// .../LocalLow/&lt;company&gt;/&lt;product&gt;) and the PlayerPrefs registry key. This runs before the first scene
/// loads and copies the previous save data over, so existing progress, settings, achievements and
/// contraptions keep loading. It only copies what is missing, never overwrites or deletes anything, and
/// only touches Windows PlayerPrefs.
/// </summary>
public static class LegacySaveMigration
{
    /// <summary>
    /// Product names this project used before the current one, newest first.
    /// </summary>
    /// <remarks>
    /// The order matters: <see cref="CopyMissing"/> only fills what the current folder does not have, so
    /// the most recent name has to be copied before an older one can fill in the rest.
    /// </remarks>
    public static readonly string[] LegacyProductNames = { "新创Rover 预览版", "新创Unity 科技版" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Migrate()
    {
        try
        {
            if (Array.IndexOf(LegacyProductNames, Application.productName) >= 0)
            {
                return;
            }
            MigrateSaveFolder();
            MigrateDocumentsFolder();
            MigratePlayerPrefs();
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[LegacySaveMigration] " + exception.Message);
        }
    }

    /// <summary>Copies save files from the old product folder into the current one.</summary>
    private static void MigrateSaveFolder()
    {
        string current = Application.persistentDataPath;
        DirectoryInfo parent = Directory.GetParent(current);
        if (parent == null)
        {
            return;
        }
        Directory.CreateDirectory(current);
        foreach (string legacyName in LegacyProductNames)
        {
            string legacy = Path.Combine(parent.FullName, legacyName);
            if (!Directory.Exists(legacy))
            {
                continue;
            }
            int copied = CopyMissing(legacy, current);
            if (copied > 0)
            {
                Debug.Log("[LegacySaveMigration] restored " + copied + " save files from " + legacy);
            }
        }
    }

    /// <summary>
    /// Copies the game data folder built from INFileSystem.Root, which is
    /// Documents/&lt;product name&gt; on Windows, Android and in the Editor (contraptions, addons).
    /// On Android the folder is only readable once the storage permission of the launcher
    /// activity has been granted; failures are logged and otherwise ignored.
    /// </summary>
    private static void MigrateDocumentsFolder()
    {
        string documents = GetDocumentsFolder();
        if (string.IsNullOrEmpty(documents))
        {
            return;
        }
        string current = Path.Combine(documents, Application.productName);
        Directory.CreateDirectory(current);
        foreach (string legacyName in LegacyProductNames)
        {
            string legacy = Path.Combine(documents, legacyName);
            if (!Directory.Exists(legacy))
            {
                continue;
            }
            int copied = CopyMissing(legacy, current);
            if (copied > 0)
            {
                Debug.Log("[LegacySaveMigration] restored " + copied + " game data files from " + legacy);
            }
        }
    }

    private static string GetDocumentsFolder()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        using (AndroidJavaClass environment = new AndroidJavaClass("android.os.Environment"))
        {
            using (AndroidJavaObject directory = environment.CallStatic<AndroidJavaObject>(
                "getExternalStoragePublicDirectory",
                environment.GetStatic<string>("DIRECTORY_DOCUMENTS")))
            {
                return directory == null ? null : directory.Call<string>("getAbsolutePath");
            }
        }
#else
        return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
#endif
    }

    private static int CopyMissing(string source, string destination)
    {
        int copied = 0;
        foreach (string file in Directory.GetFiles(source))
        {
            string target = Path.Combine(destination, Path.GetFileName(file));
            if (File.Exists(target))
            {
                continue;
            }
            File.Copy(file, target);
            copied++;
        }
        foreach (string directory in Directory.GetDirectories(source))
        {
            string target = Path.Combine(destination, Path.GetFileName(directory));
            Directory.CreateDirectory(target);
            copied += CopyMissing(directory, target);
        }
        return copied;
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    /// <summary>
    /// Copies PlayerPrefs values from the old key. Unity keeps editor keys under
    /// Software\Unity\UnityEditor\&lt;company&gt; and player keys under Software\&lt;company&gt;.
    /// </summary>
    private static void MigratePlayerPrefs()
    {
        string company = Application.companyName;
        string current = Application.productName;
        foreach (string legacyName in LegacyProductNames)
        {
            CopyRegistryKey(@"Software\Unity\UnityEditor\" + company, legacyName, current);
            CopyRegistryKey(@"Software\" + company, legacyName, current);
        }
    }

    private static void CopyRegistryKey(string companyPath, string legacyProduct, string currentProduct)
    {
        using (RegistryKey parent = Registry.CurrentUser.OpenSubKey(companyPath))
        {
            if (parent == null)
            {
                return;
            }
            using (RegistryKey legacy = parent.OpenSubKey(legacyProduct))
            {
                if (legacy == null)
                {
                    return;
                }
                using (RegistryKey target = parent.CreateSubKey(currentProduct))
                {
                    if (target == null)
                    {
                        return;
                    }
                    int copied = 0;
                    foreach (string name in legacy.GetValueNames())
                    {
                        if (string.IsNullOrEmpty(name) || target.GetValue(name) != null)
                        {
                            continue;
                        }
                        target.SetValue(name, legacy.GetValue(name), legacy.GetValueKind(name));
                        copied++;
                    }
                    if (copied > 0)
                    {
                        Debug.Log("[LegacySaveMigration] restored " + copied + " PlayerPrefs entries under " + companyPath);
                    }
                }
            }
        }
    }
#else
    private static void MigratePlayerPrefs()
    {
    }
#endif
}
