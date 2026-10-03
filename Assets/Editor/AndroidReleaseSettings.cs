using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Android settings required to produce an installable release APK. The ported project shipped
/// with an empty applicationIdentifier and no selected target architecture, which makes
/// BuildPipeline.BuildPlayer fail with "Target architecture not specified".
/// Run with: Unity -batchmode -quit -executeMethod AndroidReleaseSettings.Apply
/// </summary>
public static class AndroidReleaseSettings
{
    /// <summary>Package name of the mod build. Independent from the original game package so
    /// both can be installed side by side.</summary>
    public const string ApplicationIdentifier = "com.aedscol.rovpreview";

    /// <summary>Sideloading needs a non-zero version code; 0 cannot be upgraded in place.</summary>
    public const int BundleVersionCode = 1;

    /// <summary>
    /// There is no public API for the "Custom Gradle Properties Template" checkbox, so the
    /// serialized PlayerSettings field is toggled instead of editing ProjectSettings.asset by hand.
    /// </summary>
    private static void SetCustomGradlePropertiesTemplateEnabled()
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
        if (assets == null || assets.Length == 0)
        {
            Debug.LogWarning("RCR2_ANDROID_SETTINGS_WARN: ProjectSettings.asset not found");
            return;
        }
        SerializedObject settings = new SerializedObject(assets[0]);
        SerializedProperty property = settings.FindProperty("useCustomGradlePropertiesTemplate");
        if (property == null)
        {
            Debug.LogWarning("RCR2_ANDROID_SETTINGS_WARN: useCustomGradlePropertiesTemplate not found");
            return;
        }
        property.boolValue = true;
        settings.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        Debug.Log("RCR2_ANDROID_SETTINGS_GRADLE_TEMPLATE: useCustomGradlePropertiesTemplate=" + property.boolValue);
    }

    [MenuItem("Tools/Apply Android release settings")]
    public static void Apply()
    {
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, ApplicationIdentifier);
        PlayerSettings.Android.bundleVersionCode = BundleVersionCode;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel22;
        ApplyDisplaySettings();
        // The project lives in a path with non-ASCII characters, which makes the Android Gradle
        // Plugin abort the build unless the path check is overridden. The override is delivered
        // through Assets/Plugins/Android/gradleTemplate.properties, so the custom template has to
        // be enabled here as well.
        SetCustomGradlePropertiesTemplateEnabled();
        AssetDatabase.SaveAssets();
        Debug.Log(string.Format(
            "RCR2_ANDROID_SETTINGS_PASS: identifier={0} versionCode={1} architectures={2} minSdk={3}",
            PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android),
            PlayerSettings.Android.bundleVersionCode,
            PlayerSettings.Android.targetArchitectures,
            PlayerSettings.Android.minSdkVersion));
    }

    public static void ApplyDisplaySettings()
    {
        PlayerSettings.Android.maxAspectRatio = 3f;
        PlayerSettings.Android.resizableWindow = true;
        PlayerSettings.Android.renderOutsideSafeArea = true;
        PlayerSettings.Android.startInFullscreen = true;
        PlayerSettings.Android.optimizedFramePacing = true;
    }
}

// Covers both one-click builds and Unity's normal Build Settings window.
public sealed class AndroidDisplayBuildSettings : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform == BuildTarget.Android) AndroidReleaseSettings.ApplyDisplaySettings();
    }
}
