using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

/// <summary>
/// Editor tooling for the version-gated Havok backend: it reports exactly what this editor is missing
/// and flips the <c>HAVOK_PHYSICS</c> scripting define that switches the DOTS half of the backend on.
/// </summary>
/// <remarks>
/// <para>
/// The define exists because the DOTS half of <c>HavokPhysicsBackend</c> cannot compile on an editor
/// without <c>com.unity.physics</c>. Keeping it behind a define is what lets this project build on
/// Unity 2021.3 today and switch to Havok by adding packages plus one define on a newer editor —
/// without a second branch or a pile of dead code.
/// </para>
/// <para>
/// Enabling the define on an editor that cannot host Havok is refused rather than allowed: it would
/// break the build with unresolvable <c>Unity.Physics</c> references, which is a worse failure than a
/// greyed-out engine entry. See <c>docs/HavokPhysics.md</c> for the full upgrade procedure.
/// </para>
/// </remarks>
public static class HavokPhysicsSetup
{
    private const string MenuRoot = "Tools/Physics/Havok/";

    [MenuItem(MenuRoot + "Report status", priority = 0)]
    public static void ReportStatus()
    {
        string status = DescribeStatus();
        Debug.Log(status);
        EditorUtility.DisplayDialog("Havok Physics", status, "OK");
    }

    [MenuItem(MenuRoot + "Enable HAVOK_PHYSICS define", priority = 20)]
    public static void EnableDefine()
    {
        if (!CanHostHavok(out string blocked))
        {
            EditorUtility.DisplayDialog("Havok Physics",
                "This editor cannot host Havok Physics for Unity yet.\n\n" + blocked +
                "\n\nSee docs/HavokPhysics.md for the upgrade steps.", "OK");
            return;
        }
        SetDefine(true);
    }

    [MenuItem(MenuRoot + "Disable HAVOK_PHYSICS define", priority = 21)]
    public static void DisableDefine()
    {
        SetDefine(false);
    }

    /// <summary>Human-readable status, shared by the menu item and by build tooling.</summary>
    public static string DescribeStatus()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Havok Physics for Unity");
        builder.AppendLine("  editor            " + Application.unityVersion +
            "  (needs " + HavokPhysicsBackend.MinimumUnityVersion + " or newer)");
        builder.AppendLine("  " + HavokPhysicsBackend.ScriptingDefine + " define    " +
            (HavokDots.Compiled ? "set for the selected build target" : "not set"));

        for (int i = 0; i < HavokDots.Assemblies.Length; i++)
        {
            string name = HavokDots.Assemblies[i];
            builder.AppendLine("  assembly          " + name + "  " +
                (HavokDots.AssemblyPresent(name) ? "loadable" : "missing"));
        }

        builder.AppendLine("  verdict           " + HavokPhysicsBackend.Describe());
        builder.Append("  note              com.havok.physics is only offered by the Package Manager on " +
            "Unity Pro, Enterprise or Industry licences.");
        return builder.ToString();
    }

    /// <summary>
    /// True when enabling the define would produce a build that compiles: the editor is new enough and
    /// every DOTS assembly the backend needs can be resolved.
    /// </summary>
    public static bool CanHostHavok(out string blocked)
    {
        if (!UnityVersionGate.AtLeast(Application.unityVersion, HavokPhysicsBackend.MinimumUnityVersion))
        {
            blocked = "This is Unity " + Application.unityVersion + "; com.unity.physics does not exist " +
                "for editors older than " + HavokPhysicsBackend.MinimumUnityVersion + ".";
            return false;
        }

        string missing;
        if (!HavokDots.Available(out missing))
        {
            blocked = "The " + missing + " assembly is not present. Add the com.unity.physics and " +
                "com.havok.physics packages through the Package Manager first.";
            return false;
        }

        blocked = null;
        return true;
    }

    private static void SetDefine(bool enable)
    {
        NamedBuildTarget target;
        try
        {
            target = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
        }
        catch (Exception error)
        {
            Debug.LogError("HavokPhysicsSetup: cannot resolve the active build target group. " + error.Message);
            return;
        }

        string defines = PlayerSettings.GetScriptingDefineSymbols(target);
        var symbols = new List<string>(defines.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
        bool present = symbols.Contains(HavokPhysicsBackend.ScriptingDefine);
        if (present == enable)
        {
            Debug.Log("HavokPhysicsSetup: " + HavokPhysicsBackend.ScriptingDefine + " is already " +
                (enable ? "set" : "clear") + " for " + target.TargetName + ".");
            return;
        }

        if (enable) symbols.Add(HavokPhysicsBackend.ScriptingDefine);
        else symbols.Remove(HavokPhysicsBackend.ScriptingDefine);

        PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", symbols));
        AssetDatabase.Refresh();
        Debug.Log("HavokPhysicsSetup: " + HavokPhysicsBackend.ScriptingDefine + " " +
            (enable ? "set" : "cleared") + " for " + target.TargetName + ". " + HavokPhysicsBackend.Describe());
    }
}
