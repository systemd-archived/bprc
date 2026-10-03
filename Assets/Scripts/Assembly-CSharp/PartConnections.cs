using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

/// <summary>
/// Explicit connection points: a part may declare named anchor nodes
/// (<c>ConnectionPoints/Point-*</c>) and then connects, and is anchored, through those instead of
/// through grid adjacency.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the migration of an existing mechanism, not a new one.</b> The ported RCR2 parts already
/// connect this way - <c>RCR2Port.RoverPortConnections</c> matches their <c>ConnectionPoints/Point-*</c>
/// nodes and <c>RoverPortPart</c> passes the resulting anchor to
/// <c>Contraption.AddFixedJoint(part, other, anchor)</c>. That channel was private to those parts and the
/// rest of the game fell through to the grid. What is added here is the same rule applied to any
/// <see cref="BasePart"/> that declares points, with <c>RoverPortConnections</c> now delegating to it so
/// there is one implementation rather than two.
/// </para>
/// <para>
/// The model is Besiege's <i>adding point</i>: the connection is decided from geometry the part itself
/// carries (a point, with a tolerance), not from a global lattice. That is why this is the prerequisite
/// for placing parts off the grid - grid adjacency is what stops a part at an arbitrary angle from
/// finding its neighbour at all. See <c>docs/BesiegeBuildSystemCompare.md</c> §4.1.
/// </para>
/// <para>
/// <b>Inert for existing content.</b> A part with no <c>ConnectionPoints</c> node is not matched by any
/// of this and keeps the grid path exactly as before, so nothing already in the project changes
/// behaviour in the native mode. BPIR multi-layer building disables this mechanism for the whole
/// contraption, including automatically recognized multi-layer imports, without changing prefab data.
/// </para>
/// <para>
/// Unlike Besiege this does <b>not</b> use physics queries (<c>Physics.OverlapSphere</c> against a
/// trigger layer): the points are matched directly, which is cheaper, needs no layer reservation, and
/// keeps the result deterministic for replay and for the multiplayer snapshot.
/// </para>
/// </remarks>
public static class PartConnections
{
    public static bool Enabled(BasePart part) => part != null && (part.contraption != null
        ? !ContraptionLayers.Enabled(part.contraption) : !ContraptionLayers.ConstructionEnabled(null));

    /// <summary>
    /// How close two points, or a point and a native part's edge, must be to count as connected.
    /// </summary>
    /// <remarks>
    /// Inherited from the ported parts' <c>RoverPortConnections.AnchorTolerance</c> rather than chosen
    /// again: the ported prefabs' point spacing was tuned against this value, and widening or narrowing
    /// it here would silently change which of them connect.
    /// </remarks>
    public const float AnchorTolerance = 0.035f;

    /// <summary>
    /// Radius a connection point has when its node is not scaled.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately **half** of <see cref="AnchorTolerance"/>, because the test is "do the two spheres
    /// overlap", i.e. <c>distance &lt;= radiusA + radiusB</c>. Two unscaled points therefore connect at
    /// exactly <see cref="AnchorTolerance"/> - the same distance the mechanism used before it had radii -
    /// so every part already relying on it keeps the behaviour it was tuned for, and the 766 mount points
    /// and 6552 frame nodes the ported regressions check are unaffected.
    /// </para>
    /// <para>
    /// Bigger ranges come from scaling the node: a node scaled to 10 has ten times the reach. That is the
    /// Besiege model - <c>BlockJointTrigger</c> carries a per-trigger <c>pos</c> and <c>radius</c>, and
    /// <c>Machine</c> queries <c>Physics.OverlapSphere(pos, radius, layerMask)</c> - with the radius read
    /// from authoring data instead of from a collider, so no physics query and no trigger layer is needed.
    /// </para>
    /// </remarks>
    public const float DefaultPointRadius = AnchorTolerance * 0.5f;

    /// <summary>One connection point: where it is in world space, and how far it reaches.</summary>
    public readonly struct Point
    {
        public readonly Vector3 position;
        public readonly float radius;

        public Point(Vector3 position, float radius)
        {
            this.position = position;
            this.radius = radius;
        }
    }

    private const string PointsRootName = "ConnectionPoints";
    private const string PointPrefix = "Point-";

    private sealed class Cache
    {
        public bool scanned;
        public bool hasPoints;
        public readonly List<Transform> nodes = new List<Transform>(8);
    }

    // Weakly keyed: a destroyed part leaves nothing behind, and the scan is paid once per part.
    private static readonly ConditionalWeakTable<BasePart, Cache> cache =
        new ConditionalWeakTable<BasePart, Cache>();

    private static readonly List<Point> scratchA = new List<Point>(8);
    private static readonly List<Point> scratchB = new List<Point>(8);
    private static readonly List<GridlessGroupMerge.Circle> nativePoints = new List<GridlessGroupMerge.Circle>(8);

    /// <summary>
    /// World-space connection points declared by <paramref name="part"/>, each with the radius its node
    /// is scaled to.
    /// </summary>
    /// <remarks>
    /// The radius comes from the node's <b>local</b> scale on x, so a parent's scale cannot quietly widen
    /// every point under it; an unscaled node gets <see cref="DefaultPointRadius"/>.
    /// </remarks>
    public static void GetPoints(BasePart part, List<Point> into)
    {
        into.Clear();
        if (!Enabled(part)) return;
        HasPoints(part);
        foreach (Transform candidate in cache.GetOrCreateValue(part).nodes)
        {
            if (candidate == null) continue;
            float scale = Mathf.Abs(candidate.localScale.x);
            into.Add(new Point(candidate.position,
                DefaultPointRadius * (scale > 0.0001f ? scale : 1f)));
        }
    }

    private static bool HasPointsRootAncestor(Transform node, Transform stopAt)
    {
        for (Transform parent = node.parent; parent != null && parent != stopAt; parent = parent.parent)
        {
            if (parent.name == PointsRootName) return true;
        }
        return false;
    }

    /// <summary>
    /// Whether <paramref name="part"/> declares any connection point. Cached, because the connection
    /// walk asks this for every candidate pair and the answer cannot change while the part lives.
    /// </summary>
    public static bool HasPoints(BasePart part)
    {
        if (!Enabled(part)) return false;
        Cache entry = cache.GetOrCreateValue(part);
        if (entry.scanned) return entry.hasPoints;
        entry.scanned = true;
        entry.hasPoints = false;
        foreach (Transform candidate in part.GetComponentsInChildren<Transform>(true))
        {
            if (candidate == part.transform) continue;
            if (!candidate.name.StartsWith(PointPrefix, System.StringComparison.Ordinal)) continue;
            if (!HasPointsRootAncestor(candidate, part.transform)) continue;
            entry.nodes.Add(candidate);
        }
        entry.hasPoints = entry.nodes.Count > 0;
        return entry.hasPoints;
    }

    /// <summary>
    /// Decides whether two parts connect through their connection points, and where the joint belongs.
    /// </summary>
    /// <remarks>
    /// Two cases, both taken from the ported parts' rule:
    /// <list type="bullet">
    /// <item>both sides declare points - the closest pair within <see cref="AnchorTolerance"/>, anchored
    /// at their midpoint, with the direction taken from which way the second part lies;</item>
    /// <item>one side declares points and the other is a grid part - the point must land on one of the
    /// grid part's four edges (within tolerance, and lined up with its rows), which is what lets an
    /// anchored part meet an ordinary one.</item>
    /// </list>
    /// </remarks>
    public static bool TryGetConnection(BasePart first, BasePart second, out Vector3 anchor,
        out BasePart.Direction direction)
    {
        anchor = Vector3.zero;
        direction = BasePart.Direction.Right;
        if (!Enabled(first) || !Enabled(second) || first == second || !ContraptionLayers.Compatible(first, second) ||
            GridlessBuild.GetGroupId(first) != GridlessBuild.GetGroupId(second)) return false;
        if (!HasPoints(first))
        {
            if (!HasPoints(second) || !TryGetConnection(second, first, out anchor, out direction)) return false;
            direction = DirectionBetween(first, second);
            return true;
        }

        GetPoints(first, scratchA);
        bool otherHasPoints = HasPoints(second);
        if (otherHasPoints) GetPoints(second, scratchB);
        else GridlessGroupMerge.GetCircles(second, nativePoints);

        foreach (Point point in scratchA)
        {
            if (otherHasPoints)
            {
                foreach (Point otherPoint in scratchB)
                {
                    Vector3 difference = point.position - otherPoint.position;
                    difference.z = 0f;
                    // Spheres overlap, not points coincide: this is the Besiege rule expressed without a
                    // physics query. Two unscaled points meet at AnchorTolerance, exactly as before radii
                    // existed, so nothing already tuned against it moves.
                    float reach = point.radius + otherPoint.radius;
                    if (difference.sqrMagnitude > reach * reach) continue;
                    anchor = (point.position + otherPoint.position) * 0.5f;
                    direction = DirectionBetween(first, second);
                    return true;
                }
                continue;
            }

            if (second.m_jointConnectionType == BasePart.JointConnectionType.None ||
                (first.m_jointConnectionType != BasePart.JointConnectionType.Source &&
                 second.m_jointConnectionType != BasePart.JointConnectionType.Source))
            {
                continue;
            }

            // Match the actual faces after machine rotation, fine rotation and axis stretching.
            // Parent-space grid coordinates cease to describe those faces after a group transform.
            foreach (var nativePoint in nativePoints)
            {
                Vector2 delta = point.position - nativePoint.position;
                float budget = point.radius + DefaultPointRadius;
                if (delta.sqrMagnitude > budget * budget) continue;
                anchor = point.position;
                direction = DirectionBetween(first, second);
                return true;
            }
        }
        return false;
    }

    private static BasePart.Direction DirectionBetween(BasePart first, BasePart second)
    {
        Vector3 delta = second.transform.position - first.transform.position;
        if (first.transform.parent != null) delta = first.transform.parent.InverseTransformVector(delta);
        delta = BuildMachineTransform.Rotate(delta, -BuildMachineTransform.Read(first).angle);
        return Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
            ? (delta.x > 0f ? BasePart.Direction.Right : BasePart.Direction.Left)
            : (delta.y > 0f ? BasePart.Direction.Up : BasePart.Direction.Down);
    }
}
