using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Verifies the Besiege-style tool palette: the tool is off by default, selecting one changes what a click
/// on a part means, and the click is consumed only when the tool actually did something.
/// </summary>
/// <remarks>
/// <para>
/// The consumption rule is the one worth guarding. The build screen's click already means three things -
/// a tap rotates, a long press opens the parameter panel, a drag moves the part - and a tool exists to let
/// the player ask for one of them on purpose. If a tool reported "consumed" without acting, the click would
/// silently do nothing at all, which is worse than either behaviour.
/// </para>
/// <para>
/// Batch-runnable, and callable through the DSH bridge with <c>invoke_static BuildToolPaletteAudit.Run</c>:
/// <code>Tools\unity-batch.bat -executeMethod BuildToolPaletteAudit.Run</code>
/// </para>
/// </remarks>
public static class BuildToolPaletteAudit
{
    private static readonly List<string> Report = new List<string>();
    private static int failures;

    private static void Check(string name, bool ok, string detail)
    {
        Report.Add((ok ? "PASS  " : "FAIL  ") + name.PadRight(58) + detail);
        if (!ok) failures++;
    }

    public static void Run()
    {
        failures = 0;
        Report.Clear();
        Report.Add("build tool palette audit - Unity " + Application.unityVersion);

        var created = new List<GameObject>();
        try
        {
            CheckDefaultAndSelection();
            CheckApply(created);
        }
        finally
        {
            BuildToolPalette.Clear();
            foreach (GameObject go in created)
            {
                if (go) Object.DestroyImmediate(go);
            }
        }

        Report.Add("");
        Report.Add(failures == 0 ? "FAILURES: 0" : "FAILURES: " + failures);
        Directory.CreateDirectory("Builds/QA");
        File.WriteAllLines("Builds/QA/build-tool-palette.txt", Report);
        foreach (string line in Report) Debug.Log("[tools] " + line);
        if (failures != 0 && Application.isBatchMode) EditorApplication.Exit(1);
    }

    private static void CheckDefaultAndSelection()
    {
        BuildToolPalette.Clear();
        Check("the tool starts as none, so the build screen is unchanged",
            BuildToolPalette.Current == BuildTool.None, BuildToolPalette.Current.ToString());

        BuildToolPalette.Select(BuildTool.Rotate);
        Check("selecting a tool takes effect and shows as active",
            BuildToolPalette.Current == BuildTool.Rotate, BuildToolPalette.Current.ToString());

        BuildToolPalette.Select(BuildTool.Translate);
        Check("Translate reports itself active", BuildToolPalette.TranslateActive, "translate");

        BuildToolPalette.Select(BuildTool.Rotate);
        Check("Translate reports itself inactive for another tool", !BuildToolPalette.TranslateActive, "rotate");

        BuildToolPalette.Clear();
        Check("clearing returns to none", BuildToolPalette.Current == BuildTool.None, "none");
    }

    private static void CheckApply(List<GameObject> created)
    {
        // With no tool the click must fall through to the stock handling, which is what keeps a player who
        // never opens the palette on exactly the old behaviour.
        BuildToolPalette.Clear();
        var contraptionGo = new GameObject("ToolAuditContraption");
        contraptionGo.SetActive(false);
        var contraption = contraptionGo.AddComponent<Contraption>();
        created.Add(contraptionGo);

        BasePart part = MakePart(created, "ToolAuditPart");
        Check("no tool does not consume the click",
            !BuildToolPalette.TryApplyToPart(contraption, part), "tool=none");

        // Mirror is the cleanest tool to assert on: it has one observable effect and needs no settings.
        bool mirrorOk;
        string mirrorDetail;
        try
        {
            BuildToolPalette.Select(BuildTool.Mirror);
            bool before = part.m_flipped;
            bool consumed = BuildToolPalette.TryApplyToPart(contraption, part);
            mirrorOk = consumed && part.m_flipped != before;
            mirrorDetail = "consumed=" + consumed + " flipped " + before + " -> " + part.m_flipped;
        }
        catch (System.Exception error)
        {
            mirrorOk = false;
            mirrorDetail = error.GetType().Name + ": " + error.Message;
        }
        Check("Mirror consumes the click and flips the part", mirrorOk, mirrorDetail);

        // Modify opens a panel that only exists when the custom-value system is on. With it off the tool
        // must decline rather than swallow the click - a consumed click that did nothing is the failure
        // mode this asserts against.
        bool modifyConsumed;
        string modifyDetail;
        try
        {
            BuildToolPalette.Select(BuildTool.Modify);
            modifyConsumed = BuildToolPalette.TryApplyToPart(contraption, part);
            modifyDetail = "paramSystem=" + INParamSystem.Enabled + " consumed=" + modifyConsumed;
        }
        catch (System.Exception error)
        {
            modifyConsumed = true;
            modifyDetail = error.GetType().Name + ": " + error.Message;
        }
        Check("Modify only consumes the click when it can open the panel",
            modifyConsumed == INParamSystem.Enabled, modifyDetail);

        // Null inputs must be refused rather than throw: the call sites reach here on every pointer down.
        BuildToolPalette.Select(BuildTool.Rotate);
        bool nullSafe;
        string nullDetail;
        try
        {
            nullSafe = !BuildToolPalette.TryApplyToPart(contraption, null) &&
                       !BuildToolPalette.TryApplyToPart(null, part);
            nullDetail = "both refused";
        }
        catch (System.Exception error)
        {
            nullSafe = false;
            nullDetail = error.GetType().Name + ": " + error.Message;
        }
        Check("a null part or contraption is refused, not thrown on", nullSafe, nullDetail);
    }

    private static BasePart MakePart(List<GameObject> created, string name)
    {
        var go = new GameObject(name);
        go.SetActive(false);
        BasePart part = go.AddComponent<BasePart>();
        created.Add(go);
        return part;
    }
}
