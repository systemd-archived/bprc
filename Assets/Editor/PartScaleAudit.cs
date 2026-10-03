using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Verifies 每部件缩放 (EasyScale-style): the parameter applies, persists, and composes with the wheel
/// family instead of fighting it.
/// </summary>
/// <remarks>
/// <para>
/// The composition is the part that needs guarding. Four wheel classes write <c>transform.localScale</c>
/// themselves, deriving it from a wheel radius (<c>ApplyWheelRadius</c>), and each caches the root scale
/// on its first call. A general scale that simply wrote the root would either be overwritten by the next
/// wheel update or be baked into the wheel's own base and then multiplied again. So the wheels report
/// <c>OwnsOwnScale</c> and multiply the factor into their own expression - and this audit pins both halves:
/// the flag is set on the wheel types, and the general apply leaves their scale alone.
/// </para>
/// <para>
/// Batch-runnable, and callable through the DSH bridge with <c>invoke_static PartScaleAudit.Run</c>:
/// <code>Tools\unity-batch.bat -executeMethod PartScaleAudit.Run</code>
/// </para>
/// </remarks>
public static class PartScaleAudit
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
        Report.Add("per-part scale audit - Unity " + Application.unityVersion);

        var created = new List<GameObject>();
        try
        {
            CheckDefaultAndApply(created);
            CheckPersistence(created);
            CheckWheelsOwnTheirScale(created);
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
        File.WriteAllLines("Builds/QA/part-scale.txt", Report);
        foreach (string line in Report) Debug.Log("[scale] " + line);
        if (failures != 0 && Application.isBatchMode) EditorApplication.Exit(1);
    }

    private static void CheckDefaultAndApply(List<GameObject> created)
    {
        BasePart part = MakePart(created, "ScalePlain", new Vector3(1f, 1f, 1f));
        Check("an untouched part scales by 1", Mathf.Abs(part.ScaleMultiplier - 1f) < 1e-6f,
            "multiplier=" + part.ScaleMultiplier.ToString("F3"));

        // The panel's own path: a value is stored, then ApplyExtraParam is asked to make it take effect.
        part.SetCustomParam(BasePart.PartScaleKey, 2f);
        bool handled = part.ApplyExtraParam(BasePart.PartScaleKey, 2f);
        Check("setting PartScale is handled as an extra parameter", handled, "handled=" + handled);
        Check("the root scale follows the multiplier",
            Mathf.Abs(part.transform.localScale.x - 2f) < 1e-4f &&
            Mathf.Abs(part.transform.localScale.y - 2f) < 1e-4f,
            "localScale=" + part.transform.localScale);

        // Applying twice must not compound: the multiplier is taken against the original, not the last
        // result. Without that a repeated apply would square the scale.
        part.ApplyPartScale();
        part.ApplyPartScale();
        Check("re-applying does not compound", Mathf.Abs(part.transform.localScale.x - 2f) < 1e-4f,
            "localScale.x=" + part.transform.localScale.x.ToString("F4"));

        // And scale 1 puts it back, which is what a reset relies on.
        part.SetCustomParam(BasePart.PartScaleKey, 1f);
        part.ApplyPartScale();
        Check("multiplier 1 restores the original size", Mathf.Abs(part.transform.localScale.x - 1f) < 1e-4f,
            "localScale.x=" + part.transform.localScale.x.ToString("F4"));

        float reported;
        Check("the panel can read the current multiplier",
            part.TryGetExtraParamValue(BasePart.PartScaleKey, out reported) &&
            Mathf.Abs(reported - 1f) < 1e-4f, "reported=" + reported.ToString("F3"));

        Check("PartScale is a player parameter, not internal state",
            !BasePart.IsInternalParam(BasePart.PartScaleKey), BasePart.PartScaleKey);
    }

    private static void CheckPersistence(List<GameObject> created)
    {
        BasePart original = MakePart(created, "ScaleOriginal", Vector3.one);
        original.SetCustomParam(BasePart.PartScaleKey, 1.75f);
        string saved = original.SerializeCustomParams();

        BasePart rebuilt = MakePart(created, "ScaleRebuilt", Vector3.one);
        rebuilt.DeserializeCustomParams(saved);
        Check("the scale survives a rebuild",
            Mathf.Abs(rebuilt.ScaleMultiplier - 1.75f) < 1e-4f,
            "saved='" + saved + "' multiplier=" + rebuilt.ScaleMultiplier.ToString("F3"));
    }

    private static void CheckWheelsOwnTheirScale(List<GameObject> created)
    {
        var wheelTypes = new[] { typeof(MotorWheel), typeof(CartWheel), typeof(OffRoadWheel), typeof(StickyWheel) };
        int notOwning = 0;
        foreach (System.Type type in wheelTypes)
        {
            var go = new GameObject("Wheel_" + type.Name);
            go.SetActive(false);
            var wheel = (BasePart)go.AddComponent(type);
            created.Add(go);
            if (wheel.OwnsOwnScale) continue;
            notOwning++;
            Report.Add("      " + type.Name + " does not report OwnsOwnScale");
        }
        Check("every wheel type composes the scale itself", notOwning == 0,
            wheelTypes.Length + " checked, " + notOwning + " not owning");

        // And the general apply must leave such a part untouched, so it cannot overwrite the wheel math.
        var wheelGo = new GameObject("WheelScaleCheck");
        wheelGo.SetActive(false);
        var motor = (MotorWheel)wheelGo.AddComponent(typeof(MotorWheel));
        created.Add(wheelGo);
        motor.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
        motor.SetCustomParam(BasePart.PartScaleKey, 3f);
        motor.ApplyPartScale();
        Check("the general apply does not overwrite a wheel's own scale",
            Mathf.Abs(motor.transform.localScale.x - 0.5f) < 1e-4f,
            "localScale.x=" + motor.transform.localScale.x.ToString("F4"));
    }

    private static BasePart MakePart(List<GameObject> created, string name, Vector3 scale)
    {
        var go = new GameObject(name);
        go.SetActive(false);
        go.transform.localScale = scale;
        BasePart part = go.AddComponent<BasePart>();
        created.Add(go);
        return part;
    }
}
