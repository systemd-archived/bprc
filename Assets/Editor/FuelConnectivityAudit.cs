using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Guards how fuel connectivity is allowed to form.
/// </summary>
/// <remarks>
/// <para>
/// The rule this pins down: <b>a fuel component is built only from the links the fuel parts themselves
/// declare</b> - <c>FuelTube.GetConnectedParts</c> for tubes and <c>IFuelContainer.GetConnectedParts</c>
/// for containers - and never from "these two parts share a structural connected component".
/// </para>
/// <para>
/// <c>FuelSystem.UpdateFuelParts</c> had grown exactly such a structural union, and this project was the
/// only one of six implementations carrying it (the other five - the Extra DLC reference under
/// <c>Bad Piggies Extra DLC Test/_upstream-extraction/reference</c>, <c>Tools/BPRebooted/upstream</c>,
/// <c>Tools/StarSea/upstream</c>, <c>工程文件901-12</c> and <c>bpxhgc-901-9</c> - have none). Its effects
/// were that a vanilla jet engine received fuel from a fuel box merely bolted to the same assembly, with no
/// tube between them, and that a tube's valve had no effect, because the union had already joined the two
/// sides and a disjoint set cannot un-join.
/// </para>
/// <para>
/// It is checked by reading the source rather than by running a contraption: the behaviour depends on the
/// part catalog, joint creation and the whole contraption lifecycle, so a synthetic rig would mostly be
/// testing the rig. <c>RoverPortInteractionAudit</c> already covers the one live consumer of this rule (the
/// RCR2 battery) and its fuel assertions pass.
/// </para>
/// </remarks>
public static class FuelConnectivityAudit
{
    private static readonly List<string> Report = new List<string>();
    private static int failures;

    private const string FuelSystemPath = "Assets/Scripts/Assembly-CSharp/FuelSystem.cs";
    private const string FuelBoxPath = "Assets/Scripts/Assembly-CSharp/FuelBox.cs";
    private const string BatteryPath = "Assets/Scripts/Assembly-CSharp/RCR2Port/RoverPortBatteryPart.cs";

    /// <summary>The reference implementations that must stay free of the structural union too.</summary>
    private static readonly string[] ReferencePaths =
    {
        "Bad Piggies Extra DLC Test/_upstream-extraction/reference/FuelSystem.cs",
        "Tools/BPRebooted/upstream/bp-rebooted/Assets/Scripts/Assembly-CSharp/FuelSystem.cs",
        "Tools/StarSea/upstream/Assets/Scripts/Assembly-CSharp/FuelSystem.cs",
        "工程文件901-12/Assets/Scripts/Assembly-CSharp/FuelSystem.cs",
        "bpxhgc-901-9/Assets/Scripts/Assembly-CSharp/FuelSystem.cs",
    };

    private static void Check(string name, bool ok, string detail)
    {
        Report.Add((ok ? "PASS  " : "FAIL  ") + name.PadRight(58) + detail);
        if (!ok) failures++;
    }

    public static void Run()
    {
        failures = 0;
        Report.Clear();
        Report.Add("fuel connectivity audit - Unity " + Application.unityVersion);

        CheckNoStructuralUnion();
        CheckLinkChannels();
        CheckReferencesAgree();

        Report.Add("");
        Report.Add(failures == 0 ? "FAILURES: 0" : "FAILURES: " + failures);
        Directory.CreateDirectory("Builds/QA");
        File.WriteAllLines("Builds/QA/fuel-connectivity.txt", Report);
        foreach (string line in Report) Debug.Log("[fuel] " + line);
        if (failures != 0 && Application.isBatchMode) EditorApplication.Exit(1);
    }

    private static void CheckNoStructuralUnion()
    {
        string source = Read(FuelSystemPath);
        Check("the fuel system source is readable", source != null, FuelSystemPath);
        if (source == null) return;

        bool unionsByComponent = source.Contains("ConnectedComponent == first.ConnectedComponent") ||
                                 source.Contains("list[k].ConnectedComponent == first.ConnectedComponent");
        Check("UpdateFuelParts does not union parts by structural component", !unionsByComponent,
            unionsByComponent ? "a structural union is present again" : "only link channels union");

        // The body that matters is UpdateFuelParts; if the check above ever moves, this says where it looked.
        Check("the file still has UpdateFuelParts", source.Contains("public void UpdateFuelParts()"),
            "UpdateFuelParts present");
    }

    /// <summary>
    /// The two link channels have to keep the shape that makes the rule mean something.
    /// </summary>
    /// <remarks>
    /// A vanilla fuel box must <b>not</b> reach out to everything in its component - that is the battery's
    /// job, and the reason the battery keeps working while the box does not. If the box ever grows a
    /// component-wide link, a jet engine would again be fuelled by a box with no tube between them, and
    /// this audit would be the only thing that noticed.
    /// </remarks>
    private static void CheckLinkChannels()
    {
        string box = Read(FuelBoxPath);
        Check("the fuel box source is readable", box != null, FuelBoxPath);
        if (box != null)
        {
            bool boxReachesWholeComponent = box.Contains("part.ConnectedComponent != ConnectedComponent");
            Check("a fuel box does not link to its whole structural component", !boxReachesWholeComponent,
                boxReachesWholeComponent ? "the box grew a component-wide link" : "enclosure-based, as upstream");
        }

        string battery = Read(BatteryPath);
        Check("the battery source is readable", battery != null, BatteryPath);
        if (battery != null)
        {
            // The battery is the deliberate exception: it links across its component, which is why the
            // RCR2 fuel audit still passes without the removed union.
            bool batteryReachesWholeComponent = battery.Contains("part.ConnectedComponent != ConnectedComponent");
            Check("the RCR2 battery still links across its component", batteryReachesWholeComponent,
                batteryReachesWholeComponent ? "component-wide, as before" : "the battery lost its link");
        }
    }

    private static void CheckReferencesAgree()
    {
        int checkedCount = 0, withUnion = 0;
        foreach (string path in ReferencePaths)
        {
            string source = Read(path);
            if (source == null) continue;
            checkedCount++;
            if (source.Contains("ConnectedComponent == first.ConnectedComponent") ||
                source.Contains("list[k].ConnectedComponent == first.ConnectedComponent"))
            {
                withUnion++;
                Report.Add("      unexpected structural union in " + path);
            }
        }
        Check("no reference implementation unions by structural component",
            checkedCount > 0 && withUnion == 0,
            checkedCount + " references checked, " + withUnion + " with a union");
    }

    private static string Read(string projectRelativePath)
    {
        string full = Path.Combine(Directory.GetCurrentDirectory(), projectRelativePath);
        return File.Exists(full) ? File.ReadAllText(full) : null;
    }
}
