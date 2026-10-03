using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Verifies the connection-point mechanism on synthetic parts.
/// </summary>
/// <remarks>
/// <para>
/// An audit is the only way to check this: the mechanism is deliberately inert for everything already in
/// the project - a <see cref="BasePart"/> with no <c>ConnectionPoints</c> node is not matched by it - so
/// no existing prefab exercises it and gameplay would say nothing about whether it works. These parts are
/// built here, in memory, with the point nodes a prefab would carry.
/// </para>
/// <para>
/// Batch-runnable, and callable through the DSH bridge with <c>invoke_static PartConnectionsAudit.Run</c>:
/// <code>Tools\unity-batch.bat -executeMethod PartConnectionsAudit.Run</code>
/// </para>
/// </remarks>
public static class PartConnectionsAudit
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
        Report.Add("connection point audit - Unity " + Application.unityVersion);

        var created = new List<GameObject>();
        try
        {
            CheckInertForPlainParts(created);
            CheckAncestorRule(created);
            CheckCoincidentPointsConnect(created);
            CheckToleranceIsRespected(created);
            CheckRadiusWidensTheRange(created);
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
        File.WriteAllLines("Builds/QA/connection-points.txt", Report);
        foreach (string line in Report) Debug.Log("[conn-audit] " + line);
        if (failures != 0 && Application.isBatchMode) EditorApplication.Exit(1);
    }

    /// <summary>A part with no point nodes must not be matched at all - that is what keeps this additive.</summary>
    private static void CheckInertForPlainParts(List<GameObject> created)
    {
        BasePart plain = MakePart(created, "Plain", new Vector3(0f, 0f, 0f));
        BasePart other = MakePart(created, "PlainOther", new Vector3(1f, 0f, 0f));

        Check("a part with no ConnectionPoints declares none", !PartConnections.HasPoints(plain), "HasPoints=false");

        Vector3 anchor;
        BasePart.Direction direction;
        Check("two plain parts are not matched by the point rule",
            !PartConnections.TryGetConnection(plain, other, out anchor, out direction),
            "TryGetConnection=false");
    }

    /// <summary>
    /// A <c>Point-</c> node only counts under a <c>ConnectionPoints</c> ancestor. This is the rule the
    /// ported parts' reader has always had, and getting it wrong is silent - the points simply are never
    /// found, which is what <c>RC1PortDiagnostics</c> documents happening to a mis-parented prefab.
    /// </summary>
    private static void CheckAncestorRule(List<GameObject> created)
    {
        BasePart loose = MakePart(created, "LoosePoint", Vector3.zero);
        AddPoint(loose, "Point-Bottom-0-0", Vector3.zero, underPointsRoot: false);
        Check("a Point-* node without the ConnectionPoints parent is ignored",
            !PartConnections.HasPoints(loose), "HasPoints=false");

        BasePart proper = MakePart(created, "ProperPoint", Vector3.zero);
        AddPoint(proper, "Point-Bottom-0-0", Vector3.zero, underPointsRoot: true);
        Check("a Point-* node under ConnectionPoints is found",
            PartConnections.HasPoints(proper), "HasPoints=true");
    }

    private static void CheckCoincidentPointsConnect(List<GameObject> created)
    {
        BasePart left = MakePart(created, "AnchoredLeft", Vector3.zero);
        AddPoint(left, "Point-Right-0-0", new Vector3(0.5f, 0f, 0f), underPointsRoot: true);

        BasePart right = MakePart(created, "AnchoredRight", new Vector3(1f, 0f, 0f));
        AddPoint(right, "Point-Left-0-0", new Vector3(-0.5f, 0f, 0f), underPointsRoot: true);

        Vector3 anchor;
        BasePart.Direction direction;
        bool connected = PartConnections.TryGetConnection(left, right, out anchor, out direction);
        Check("points that meet connect", connected, connected ? "anchor=" + anchor : "not connected");
        Check("the anchor is the midpoint of the two points",
            connected && (anchor - new Vector3(0.5f, 0f, 0f)).magnitude < 1e-4f,
            "anchor=" + anchor.ToString("F4"));
        Check("the direction is taken from where the second part lies",
            connected && direction == BasePart.Direction.Right, "direction=" + direction);

        // The rule has to be symmetric, or a part would connect in one query order and not the other.
        Vector3 reverseAnchor;
        BasePart.Direction reverseDirection;
        bool reverse = PartConnections.TryGetConnection(right, left, out reverseAnchor, out reverseDirection);
        Check("the rule is symmetric", reverse && reverseDirection == BasePart.Direction.Left,
            reverse ? "direction=" + reverseDirection : "not connected in reverse");
    }

    private static void CheckToleranceIsRespected(List<GameObject> created)
    {
        BasePart left = MakePart(created, "FarLeft", Vector3.zero);
        AddPoint(left, "Point-Right-0-0", new Vector3(0.5f, 0f, 0f), underPointsRoot: true);

        // Gap deliberately several times AnchorTolerance, so this cannot pass by luck.
        float gap = PartConnections.AnchorTolerance * 10f;
        BasePart right = MakePart(created, "FarRight", new Vector3(1f + gap, 0f, 0f));
        AddPoint(right, "Point-Left-0-0", new Vector3(-0.5f, 0f, 0f), underPointsRoot: true);

        Vector3 anchor;
        BasePart.Direction direction;
        bool connected = PartConnections.TryGetConnection(left, right, out anchor, out direction);
        Check("points beyond AnchorTolerance do not connect", !connected,
            "gap=" + gap.ToString("F4") + " tolerance=" + PartConnections.AnchorTolerance.ToString("F4"));

        // And just inside it, so the boundary itself is exercised rather than only the far side.
        BasePart near = MakePart(created, "NearRight", new Vector3(1f + PartConnections.AnchorTolerance * 0.5f, 0f, 0f));
        AddPoint(near, "Point-Left-0-0", new Vector3(-0.5f, 0f, 0f), underPointsRoot: true);
        Vector3 nearAnchor;
        BasePart.Direction nearDirection;
        Check("points just inside AnchorTolerance do connect",
            PartConnections.TryGetConnection(left, near, out nearAnchor, out nearDirection),
            "gap=" + (PartConnections.AnchorTolerance * 0.5f).ToString("F4"));
    }

    /// <summary>
    /// The reach is a radius now, so scaling a point's node has to actually widen it - and two unscaled
    /// points have to still meet at exactly <c>AnchorTolerance</c>, or every ported prefab tuned against
    /// that number would move.
    /// </summary>
    private static void CheckRadiusWidensTheRange(List<GameObject> created)
    {
        Check("the default radius is half the tolerance, so two points meet at the tolerance",
            Mathf.Abs(PartConnections.DefaultPointRadius * 2f - PartConnections.AnchorTolerance) < 1e-6f,
            "default=" + PartConnections.DefaultPointRadius.ToString("F4") +
            " tolerance=" + PartConnections.AnchorTolerance.ToString("F4"));

        BasePart left = MakePart(created, "RadiusLeft", Vector3.zero);
        AddPoint(left, "Point-Right-0-0", new Vector3(0.5f, 0f, 0f), underPointsRoot: true, scale: 10f);

        // Four times the tolerance away: out of reach for unscaled points, inside it for these.
        float gap = PartConnections.AnchorTolerance * 4f;
        BasePart right = MakePart(created, "RadiusRight", new Vector3(1f + gap, 0f, 0f));
        AddPoint(right, "Point-Left-0-0", new Vector3(-0.5f, 0f, 0f), underPointsRoot: true);

        Vector3 anchor;
        BasePart.Direction direction;
        bool connected = PartConnections.TryGetConnection(left, right, out anchor, out direction);
        Check("a scaled-up point reaches further than the tolerance", connected,
            "gap=" + gap.ToString("F4") + " reach=" +
            (PartConnections.DefaultPointRadius * 10f + PartConnections.DefaultPointRadius).ToString("F4"));

        // And the same geometry with unscaled nodes on BOTH sides must still refuse, so the radius is what
        // made the difference rather than the gap being inside some other budget. The left part has to be
        // a fresh one: reusing the scaled part above would leave its widened point in the pair.
        BasePart plainLeft = MakePart(created, "RadiusPlainLeft", Vector3.zero);
        AddPoint(plainLeft, "Point-Right-0-0", new Vector3(0.5f, 0f, 0f), underPointsRoot: true);
        BasePart plain = MakePart(created, "RadiusPlain", new Vector3(1f + gap, 0f, 0f));
        AddPoint(plain, "Point-Left-0-0", new Vector3(-0.5f, 0f, 0f), underPointsRoot: true);
        Vector3 plainAnchor;
        BasePart.Direction plainDirection;
        Check("the same gap stays out of reach for unscaled points",
            !PartConnections.TryGetConnection(plainLeft, plain, out plainAnchor, out plainDirection),
            "gap=" + gap.ToString("F4") + " reach=" +
            (PartConnections.DefaultPointRadius * 2f).ToString("F4"));
    }

    private static BasePart MakePart(List<GameObject> created, string name, Vector3 position)
    {
        // Created inactive so BasePart's own Awake/Start do not run: these are geometry fixtures, not
        // gameplay parts, and the point walk includes inactive children anyway.
        var go = new GameObject(name);
        go.SetActive(false);
        go.transform.position = position;
        BasePart part = go.AddComponent<BasePart>();
        created.Add(go);
        return part;
    }

    private static void AddPoint(BasePart part, string name, Vector3 localPosition, bool underPointsRoot, float scale = 1f)
    {
        var node = new GameObject(name);
        if (underPointsRoot)
        {
            var root = new GameObject("ConnectionPoints");
            root.transform.SetParent(part.transform, worldPositionStays: false);
            node.transform.SetParent(root.transform, worldPositionStays: false);
        }
        else
        {
            node.transform.SetParent(part.transform, worldPositionStays: false);
        }
        node.transform.localPosition = localPosition;
        node.transform.localScale = new Vector3(scale, scale, scale);
    }
}
