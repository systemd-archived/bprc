using System.IO;
using UnityEditor;
using UnityEngine;

public static class RebuildAssetBundles
{
    private static readonly string SourceRoot =
        Path.Combine("Assets", "assetbundles");

    private static readonly string OutputRoot =
        Path.Combine("Assets", "StreamingAssets", "AssetBundles");

    [MenuItem("Tools/AssetBundles/Rebuild All Targets")]
    public static void RebuildAllTargets()
    {
        Rebuild(
            (BuildTarget.Android, "Android"),
            (BuildTarget.StandaloneWindows64, "Windows"),
            (BuildTarget.StandaloneLinux64, "Linux")
        );
    }

    [MenuItem("Tools/AssetBundles/Rebuild Android")]
    public static void RebuildAndroid()
    {
        Rebuild((BuildTarget.Android, "Android"));
    }

    [MenuItem("Tools/AssetBundles/Rebuild Windows")]
    public static void RebuildWindows()
    {
        Rebuild((BuildTarget.StandaloneWindows64, "Windows"));
    }

    [MenuItem("Tools/AssetBundles/Rebuild Linux")]
    public static void RebuildLinux()
    {
        Rebuild((BuildTarget.StandaloneLinux64, "Linux"));
    }

    private static void Rebuild(params (BuildTarget target, string folderName)[] builds)
    {
        if (!ValidateSourceRoot())
            return;

        Debug.Log($"AssetBundle Source Root: {SourceRoot}");
        Debug.Log($"AssetBundle Output Root: {OutputRoot}");

        AssignBundleNames();

        AssetDatabase.RemoveUnusedAssetBundleNames();
        AssetDatabase.SaveAssets();

        foreach (var (target, folderName) in builds)
        {
            BuildForTarget(target, folderName);
        }

        AssetDatabase.Refresh();

        Debug.Log("AssetBundle rebuild complete.");
    }

    private static bool ValidateSourceRoot()
    {
        if (AssetDatabase.IsValidFolder(SourceRoot))
            return true;

        Debug.LogError($"Missing folder: {SourceRoot}");
        return false;
    }

    private static void AssignBundleNames()
    {
        string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { SourceRoot });

        if (guids.Length == 0)
        {
            Debug.LogWarning($"No assets found in source root: {SourceRoot}");
            return;
        }

        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);

            if (AssetDatabase.IsValidFolder(assetPath))
                continue;

            string bundleName = GetBundleName(assetPath);

            if (string.IsNullOrEmpty(bundleName))
            {
                Debug.LogWarning($"Null or empty bundle: {assetPath}");
                continue;
            }

            AssetImporter importer = AssetImporter.GetAtPath(assetPath);

            if (importer == null)
                continue;

            importer.assetBundleName = bundleName;

            Debug.Log($"Assigned Bundle [{bundleName}] -> {assetPath}");
        }
    }

    private static void BuildForTarget(BuildTarget target, string folderName)
    {
        string platformOutput = Path.Combine(OutputRoot, folderName);

        Directory.CreateDirectory(platformOutput);

        BuildPipeline.BuildAssetBundles(
            platformOutput,
            BuildAssetBundleOptions.ChunkBasedCompression,
            target
        );

        Debug.Log($"Built AssetBundles for {target} at {platformOutput}");
    }

    private static string GetBundleName(string assetPath)
    {
        string directoryPath = Path.GetDirectoryName(assetPath);

        if (string.IsNullOrEmpty(directoryPath))
            return string.Empty;

        return Path.GetFileName(directoryPath);
    }
}