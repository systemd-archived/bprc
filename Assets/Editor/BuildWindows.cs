using UnityEditor;
public static class BuildWindows
{
    [MenuItem("Tools/Build/Build Windows")]
    public static void Build()
    {
        BuildTools.OneClickBuild.BuildStarSeaTarget("Windows64");
    }
}
