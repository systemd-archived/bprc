using UnityEditor;
public static class BuildAndroidApk
{
    [MenuItem("Tools/Build/Build Android APK")]
    public static void Build()
    {
        BuildTools.OneClickBuild.BuildStarSeaTarget("Android");
    }
}
