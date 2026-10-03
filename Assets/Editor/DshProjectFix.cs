// Temporary editor helper used by the DSH Unity bridge. Safe to delete.
using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Project-settings helper the bridge calls through <c>invoke_static</c>. It drives the asynchronous
/// shader compilation switch (Edit &gt; Project Settings &gt; Editor &gt; Shader Compilation), which is the
/// setting that puts Unity's cyan placeholder shader on screen for a frame the first time a variant is
/// needed after the shader cache goes cold.
/// </summary>
public static class DshProjectFix
{
    /// <summary>Current value of the setting as the editor holds it.</summary>
    public static string AsyncInfo()
    {
        return "EditorSettings.asyncShaderCompilation=" + EditorSettings.asyncShaderCompilation;
    }

    /// <summary>Turns asynchronous shader compilation off, so no cyan placeholder can be shown.</summary>
    public static string AsyncOff()
    {
        EditorSettings.asyncShaderCompilation = false;
        return "set. " + AsyncInfo();
    }

    /// <summary>Turns asynchronous shader compilation back on.</summary>
    public static string AsyncOn()
    {
        EditorSettings.asyncShaderCompilation = true;
        return "set. " + AsyncInfo();
    }

    /// <summary>
    /// Captures the real game view - every camera plus the overlay canvases - which the bridge's own
    /// screenshot command cannot do, because that one renders a fresh camera into a render texture.
    /// </summary>
    public static string Grab(string name)
    {
        string dir = System.IO.Path.Combine(Application.dataPath, "..", "Temp", "DshBridge");
        System.IO.Directory.CreateDirectory(dir);
        string rel = "Temp/DshBridge/" + name + ".png";
        ScreenCapture.CaptureScreenshot(rel, 1);
        return rel;
    }
}
