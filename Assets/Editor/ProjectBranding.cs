using UnityEditor;
using UnityEngine;

/// <summary>
/// Product name for this build of the project: shown in the Editor title bar, the player window
/// title and the one-click build output folder name (Tools/one-click-build.ps1 reads
/// PlayerSettings.productName).
/// Run with: Unity -batchmode -quit -executeMethod ProjectBranding.Apply
/// </summary>
public static class ProjectBranding
{
    /// <summary>
    /// The released name. It was "新创Rover 预览版" while the port was in preview; dropping the suffix moved
    /// both <c>Application.persistentDataPath</c> and the Documents data root, which is what
    /// <see cref="LegacySaveMigration"/> now migrates from.
    /// </summary>
    public const string ProductName = "新创Rover";

    [MenuItem("Tools/Apply product name")]
    public static void Apply()
    {
        PlayerSettings.productName = ProductName;
        AssetDatabase.SaveAssets();
        Debug.Log("RCR2_BRANDING_PASS: productName = " + PlayerSettings.productName);
    }
}
