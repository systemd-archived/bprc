using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Verifies 部件精细旋转: the nearest-eighth snap the build model reads, the angle's persistence, and that
/// the whole feature is inert while the step is 0.
/// </summary>
/// <remarks>
/// <para>
/// The snap is the part worth testing. A fine angle only changes what the part is <i>drawn</i> at; the
/// value placement, occupancy, connectivity and the save format read is still
/// <c>BasePart.m_gridRotation</c>, an eight-value enum. If the snap were wrong the part would visibly
/// turn while the build model answered about a different facing - a bug that looks like "the joint is on
/// the wrong side" and would be chased in the wrong place.
/// </para>
/// <para>
/// Batch-runnable, and callable through the DSH bridge with <c>invoke_static FineRotateAudit.Run</c>:
/// <code>Tools\unity-batch.bat -executeMethod FineRotateAudit.Run</code>
/// </para>
/// </remarks>
public static class FineRotateAudit
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
        Report.Add("fine rotation audit - Unity " + Application.unityVersion);

        var created = new List<GameObject>();
        try
        {
            CheckSnap();
            CheckPersistence(created);
            CheckInertWhenOff(created);
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
        File.WriteAllLines("Builds/QA/fine-rotate.txt", Report);
        foreach (string line in Report) Debug.Log("[fine-rot] " + line);
        if (failures != 0 && Application.isBatchMode) EditorApplication.Exit(1);
    }

    /// <summary>Every eighth, plus the midpoints that decide which way a tie falls, plus wraparound.</summary>
    private static void CheckSnap()
    {
        var cases = new (float degrees, BasePart.GridRotation expected)[]
        {
            (0f, BasePart.GridRotation.Deg_0),
            (22f, BasePart.GridRotation.Deg_0),
            (23f, BasePart.GridRotation.Deg_45),
            (45f, BasePart.GridRotation.Deg_45),
            (90f, BasePart.GridRotation.Deg_90),
            (135f, BasePart.GridRotation.Deg_135),
            (180f, BasePart.GridRotation.Deg_180),
            (225f, BasePart.GridRotation.Deg_225),
            (270f, BasePart.GridRotation.Deg_270),
            (315f, BasePart.GridRotation.Deg_315),
            // Past the end it has to wrap rather than fall off the table. 338 is closer to 360 than to
            // 315 (22 against 23), so it belongs on Deg_0 - which is the wrap this checks.
            (359f, BasePart.GridRotation.Deg_0),
            (350f, BasePart.GridRotation.Deg_0),
            (338f, BasePart.GridRotation.Deg_0),
            (337f, BasePart.GridRotation.Deg_315),
            (-45f, BasePart.GridRotation.Deg_315),
            (360f, BasePart.GridRotation.Deg_0),
        };

        int wrong = 0;
        foreach (var test in cases)
        {
            BasePart.GridRotation got = FineRotate.NearestEighth(test.degrees);
            if (got == test.expected) continue;
            wrong++;
            Report.Add("      " + test.degrees + " deg -> " + got + ", expected " + test.expected);
        }
        Check("every angle snaps to the expected eighth", wrong == 0,
            cases.Length + " checked, " + wrong + " wrong");
    }

    /// <summary>
    /// The angle has to survive a rebuild for the same reason the grid-less offset does, and through the
    /// same channel.
    /// </summary>
    private static void CheckPersistence(List<GameObject> created)
    {
        BasePart original = MakePart(created, "FineOriginal");
        original.SetStoredParam(FineRotate.AngleKey, 123.5f);
        original.SetStoredParam("WheelRadius", 0.6f);

        string saved = original.SerializeCustomParams();
        Check("the angle is written into the part's stored parameters",
            saved.Contains(FineRotate.AngleKey), "'" + saved + "'");

        BasePart rebuilt = MakePart(created, "FineRebuilt");
        rebuilt.DeserializeCustomParams(saved);
        Check("a rebuilt part still carries the angle",
            rebuilt.TryGetStoredParam(FineRotate.AngleKey, out float angle) && Mathf.Abs(angle - 123.5f) < 1e-4f,
            "angle=" + (rebuilt.TryGetStoredParam(FineRotate.AngleKey, out float a) ? a.ToString("F2") : "missing"));

        Check("the angle key is internal, so it is not an editable row",
            BasePart.IsInternalParam(FineRotate.AngleKey), FineRotate.AngleKey);

        // A part that also has a real parameter must still count as modified; only internal-only parts do
        // not, which is the rule INModifiedPartMarker now applies.
        int player = 0;
        foreach (KeyValuePair<string, float> pair in original.CustomParams)
        {
            if (!BasePart.IsInternalParam(pair.Key)) player++;
        }
        Check("a part with a real parameter still counts one", player == 1, "player=" + player);
    }

    /// <summary>With the step at 0 nothing may differ from stock.</summary>
    private static void CheckInertWhenOff(List<GameObject> created)
    {
        // The settings singleton decides this, and an editor session may not have loaded it; either way
        // the feature must report itself off rather than assume on.
        bool enabled = FineRotate.Enabled;
        Check("the feature is off unless a step is configured",
            !enabled || FineRotate.StepDegrees > 0,
            "enabled=" + enabled + " step=" + FineRotate.StepDegrees);

        BasePart part = MakePart(created, "FineInert");
        part.SetStoredParam(FineRotate.AngleKey, 123.5f);
        // Step returns false when the feature is off, which is the contract Contraption.Flip relies on to
        // fall back to the stock rotation search.
        Check("Step declines to act while the feature is off or the direction is zero",
            !FineRotate.Step(part, 1) || enabled,
            "enabled=" + enabled);
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
