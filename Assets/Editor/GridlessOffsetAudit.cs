using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Verifies that the grid-less placement offset survives a save and reload.
/// </summary>
/// <remarks>
/// <para>
/// This is the defect this audit exists for: the offset used to live only in a live table keyed by the
/// part instance, so the moment a contraption was rebuilt from its dataset - which is what running a level
/// does - the parts came back on the lattice. The fix rides the offset in the part's own stored
/// parameters, which is the only per-part channel that survives a save, a clone and the multiplayer
/// blueprint.
/// </para>
/// <para>
/// The round trip is simulated here rather than played: a part is given an offset, serialized to the
/// string the save format carries, and the string is read into a <b>different</b> part - which is exactly
/// what a rebuild does, and the only way to see the difference between "the cache still had it" and "it
/// was actually persisted".
/// </para>
/// <para>
/// Batch-runnable, and callable through the DSH bridge with <c>invoke_static GridlessOffsetAudit.Run</c>:
/// <code>Tools\unity-batch.bat -executeMethod GridlessOffsetAudit.Run</code>
/// </para>
/// </remarks>
public static class GridlessOffsetAudit
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
        Report.Add("grid-less offset persistence audit - Unity " + Application.unityVersion);

        var created = new List<GameObject>();
        try
        {
            CheckSurvivesARebuild(created);
            CheckInternalKeysStayOutOfTheUi(created);
            CheckKeepOnlyWhenInternal(created);
            CheckVehicleClamp();
        }
        finally
        {
            foreach (GameObject go in created)
            {
                if (go) Object.DestroyImmediate(go);
            }
        }

        Report.Add("");
        Report.Add(failures == 0 ? "FAILURES: 0" : "FAILURES: " + failures);
        Directory.CreateDirectory("Builds/QA");
        File.WriteAllLines("Builds/QA/gridless-offset.txt", Report);
        foreach (string line in Report) Debug.Log("[gl-offset] " + line);
        if (failures != 0 && Application.isBatchMode) EditorApplication.Exit(1);
    }

    /// <summary>The core assertion: offset in, save string, new part, offset out.</summary>
    private static void CheckSurvivesARebuild(List<GameObject> created)
    {
        BasePart original = MakePart(created, "Original");
        var offset = new Vector2(0.30f, -0.20f);
        original.SetStoredParam(GridlessBuild.OffsetXKey, offset.x);
        original.SetStoredParam(GridlessBuild.OffsetYKey, offset.y);

        string saved = original.SerializeCustomParams();
        Check("the offset is written into the part's stored parameters",
            !string.IsNullOrEmpty(saved) && saved.Contains(GridlessBuild.OffsetXKey),
            "'" + saved + "'");

        // A rebuild makes a new instance and hands it the saved string - the live table knows nothing
        // about this object, so anything it reports came from storage.
        BasePart rebuilt = MakePart(created, "Rebuilt");
        rebuilt.DeserializeCustomParams(saved);

        Vector2 restored = GridlessBuild.CachedOrRestored(rebuilt);
        Check("a rebuilt part recovers its offset from the saved string",
            (restored - offset).sqrMagnitude < 1e-8f,
            "expected=" + offset + " got=" + restored);

        // And the part that had no offset must not invent one.
        BasePart untouched = MakePart(created, "Untouched");
        Check("a part with no stored offset restores zero",
            GridlessBuild.CachedOrRestored(untouched) == Vector2.zero,
            "got=" + GridlessBuild.CachedOrRestored(untouched));
    }

    /// <summary>
    /// The storage is shared with the player's custom numbers, so the keys have to be recognisable as
    /// internal - otherwise every grid-less part would grow two editable rows in the long-press panel and
    /// a "modified" badge it did not earn.
    /// </summary>
    private static void CheckInternalKeysStayOutOfTheUi(List<GameObject> created)
    {
        Check("the offset keys are marked internal",
            BasePart.IsInternalParam(GridlessBuild.OffsetXKey) &&
            BasePart.IsInternalParam(GridlessBuild.OffsetYKey),
            GridlessBuild.OffsetXKey + " / " + GridlessBuild.OffsetYKey);

        Check("a player parameter is not treated as internal",
            !BasePart.IsInternalParam("WheelRadius") && !BasePart.IsInternalParam("JetEngineForce"),
            "WheelRadius, JetEngineForce");

        BasePart part = MakePart(created, "InternalOnly");
        part.SetStoredParam(GridlessBuild.OffsetXKey, 0.1f);
        int visible = 0;
        foreach (KeyValuePair<string, float> pair in part.CustomParams)
        {
            if (!BasePart.IsInternalParam(pair.Key)) visible++;
        }
        Check("an internal-only part exposes no player parameters", visible == 0,
            visible + " visible entries");
    }

    /// <summary>
    /// Reading must not depend on the custom-value system being switched on.
    /// </summary>
    /// <remarks>
    /// <c>GetCustomParam</c> returns its fallback when <c>INParamSystem</c> is off, because that toggle
    /// means "ignore the player's custom numbers". The offset is internal state that merely borrows the
    /// storage, so a player who turns that panel off must not lose their grid-less placements - which is
    /// what <c>TryGetStoredParam</c> exists for.
    /// </remarks>
    private static void CheckKeepOnlyWhenInternal(List<GameObject> created)
    {
        BasePart part = MakePart(created, "MixedParams");
        part.SetStoredParam(GridlessBuild.OffsetXKey, 0.25f);
        part.SetCustomParam("WheelRadius", 0.6f);

        Check("the ungated read sees the stored offset",
            part.TryGetStoredParam(GridlessBuild.OffsetXKey, out float stored) && Mathf.Abs(stored - 0.25f) < 1e-6f,
            "value=" + (part.TryGetStoredParam(GridlessBuild.OffsetXKey, out float v) ? v.ToString("F4") : "missing"));

        int player = 0, internalCount = 0;
        foreach (KeyValuePair<string, float> pair in part.CustomParams)
        {
            if (BasePart.IsInternalParam(pair.Key)) internalCount++; else player++;
        }
        Check("a part with both kinds counts one player parameter and one internal",
            player == 1 && internalCount == 1, "player=" + player + " internal=" + internalCount);
    }

    /// <summary>
    /// The whole-contraption shift is bounded, because the build screen has no other limit on it: without
    /// the clamp the move buttons could walk a vehicle out of the buildable area.
    /// </summary>
    private static void CheckVehicleClamp()
    {
        float max = GridlessBuild.MaxVehicleOffset;

        Check("a shift inside the bound is unchanged",
            (GridlessBuild.ClampVehicleOffset(new Vector2(1.5f, -2.5f)) - new Vector2(1.5f, -2.5f)).sqrMagnitude < 1e-8f,
            "max=" + max.ToString("F1"));

        Vector2 clamped = GridlessBuild.ClampVehicleOffset(new Vector2(max * 10f, -max * 10f));
        Check("a shift past the bound is clamped on both axes",
            Mathf.Abs(clamped.x - max) < 1e-4f && Mathf.Abs(clamped.y + max) < 1e-4f,
            "got=" + clamped);

        Check("the step is a fraction of a cell, so the buttons are usable",
            GridlessBuild.VehicleStep > 0f && GridlessBuild.VehicleStep < 1f,
            "step=" + GridlessBuild.VehicleStep.ToString("F2"));

        // With the mode off the whole feature has to be inert, which is what Contraption.SetPartPos and
        // ConstructionUI.MoveContraption rely on to keep the stock behaviour.
        if (!GridlessBuild.Enabled)
        {
            Check("no vehicle offset is reported while the mode is off",
                GridlessBuild.GetVehicleOffset(null) == Vector2.zero, "enabled=false");
        }
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
