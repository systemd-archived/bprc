using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Sticky build-group merging, using the same circles for preview and contact tests.</summary>
public static class GridlessGroupMerge
{
    public const float NativeCircleRadius = 0.08f;
    public static bool Enabled => GridlessBuild.Enabled && (INUserSettings.Instance?.GeneralSettings?.MergeGridlessGroups ?? false) &&
        !ContraptionLayers.ConstructionEnabled(WPFMonoBehaviour.levelManager?.ContraptionProto);

    public readonly struct Circle
    {
        public readonly BasePart part;
        public readonly Vector3 position;
        public readonly float radius;
        public readonly int side;
        public Circle(BasePart part, Vector3 position, float radius, int side = -1)
        { this.part = part; this.position = position; this.radius = radius; this.side = side; }
    }
    private static readonly List<PartConnections.Point> authored = new List<PartConnections.Point>();

    public static void GetCircles(BasePart part, List<Circle> into)
    {
        into.Clear();
        if (!PartConnections.Enabled(part) || part.m_static || part.m_jointConnectionType == BasePart.JointConnectionType.None) return;
        if (PartConnections.HasPoints(part))
        {
            PartConnections.GetPoints(part, authored);
            foreach (var point in authored)
                if (Finite(point.position) && BuildMachineTransform.Finite(point.radius) && point.radius > 0f)
                    into.Add(new Circle(part, point.position, point.radius));
            return;
        }
        BitDirection mask = part.m_jointConnectionDirection.ToBitDirection();
        Vector3 scale = part.BuildFootprintScale;
        float parentScale = part.transform.parent == null ? 1f : Mathf.Max(
            Mathf.Abs(part.transform.parent.lossyScale.x), Mathf.Abs(part.transform.parent.lossyScale.y));
        for (int side = 0; side < 4; side++)
        {
            if ((mask & (BitDirection)(1 << side)) == BitDirection.None) continue;
            bool horizontal = side == 0 || side == 2;
            float edge = side == 0 ? part.m_gridXmax + 0.5f : side == 1 ? part.m_gridYmax + 0.5f :
                side == 2 ? part.m_gridXmin - 0.5f : part.m_gridYmin - 0.5f;
            int min = horizontal ? part.m_gridYmin : part.m_gridXmin;
            int max = horizontal ? part.m_gridYmax : part.m_gridXmax;
            for (int row = min; row <= max; row++)
            {
                Vector3 local = horizontal ? new Vector3(edge * scale.x, row * scale.y) : new Vector3(row * scale.x, edge * scale.y);
                Vector3 displacement = part.transform.localRotation * local;
                if (part.transform.parent != null) displacement = part.transform.parent.TransformVector(displacement);
                Vector3 position = part.transform.position + displacement;
                if (Finite(position)) into.Add(new Circle(part, position, NativeCircleRadius * parentScale, side));
            }
        }
    }
    private static bool Finite(Vector3 value) => BuildMachineTransform.Finite(value.x) && BuildMachineTransform.Finite(value.y);

    // Hash circle centres once per completed action. With cell >= maximum diameter, only the
    // adjacent nine cells can contain an overlapping circle, including negative coordinates.
    private static void VisitContacts(Contraption c, bool acrossGroups, Action<Circle, Circle, Vector3> visit)
    {
        var circles = new List<Circle>(); var buffer = new List<Circle>();
        float cellSize = 0.25f;
        foreach (BasePart part in c.Parts)
        {
            GetCircles(part, buffer);
            foreach (Circle point in buffer) { circles.Add(point); cellSize = Mathf.Max(cellSize, point.radius * 2f); }
        }
        var buckets = new Dictionary<(int x, int y), List<int>>();
        for (int i = 0; i < circles.Count; i++)
        {
            Circle point = circles[i];
            int x = Mathf.FloorToInt(point.position.x / cellSize), y = Mathf.FloorToInt(point.position.y / cellSize);
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (!buckets.TryGetValue((x + dx, y + dy), out List<int> nearby)) continue;
                    foreach (int j in nearby)
                    {
                        Circle other = circles[j];
                        if (point.part == other.part || !PartPhysicsParameters.SameLayer(point.part, other.part)) continue;
                        bool different = GridlessBuild.GetGroupId(point.part) != GridlessBuild.GetGroupId(other.part);
                        if (acrossGroups != different || (!acrossGroups &&
                            GridlessBuild.GetLatticeId(point.part) == GridlessBuild.GetLatticeId(other.part))) continue;
                        Vector2 delta = point.position - other.position;
                        float reach = point.radius + other.radius;
                        if (delta.sqrMagnitude <= reach * reach + 1e-8f)
                            visit(point, other, (point.position + other.position) * 0.5f);
                    }
                }
            if (!buckets.TryGetValue((x, y), out List<int> bucket)) buckets.Add((x, y), bucket = new List<int>());
            bucket.Add(i);
        }
    }
    private static int Root(int[] parents, int index)
    {
        while (parents[index] != index) { parents[index] = parents[parents[index]]; index = parents[index]; }
        return index;
    }
    /// <returns>The number of other groups absorbed by the manipulated group.</returns>
    public static int TryMerge(Contraption c, int manipulatedGroup)
    {
        if (!Enabled || c == null || c.IsRunning || ContraptionLayers.Enabled(c)) return 0;
        var groups = GridlessBuild.GetGroups(c);
        var indices = new Dictionary<int, int>(); var parents = new int[groups.Count];
        for (int i = 0; i < groups.Count; i++) { indices.Add(groups[i].id, i); parents[i] = i; }
        if (!indices.TryGetValue(manipulatedGroup, out int moved)) return 0;
        VisitContacts(c, true, (a, b, anchor) => {
            int first = Root(parents, indices[GridlessBuild.GetGroupId(a.part)]);
            int second = Root(parents, indices[GridlessBuild.GetGroupId(b.part)]);
            parents[second] = first;
        });
        int connected = Root(parents, moved);
        var absorb = new HashSet<int>();
        foreach (var entry in indices) if (Root(parents, entry.Value) == connected) absorb.Add(entry.Key);
        if (absorb.Count < 2) return 0;
        foreach (BasePart part in c.Parts)
        {
            if (part == null || part.m_static || !absorb.Contains(GridlessBuild.GetGroupId(part))) continue;
            int lattice = GridlessBuild.GetLatticeId(part);
            part.SetStoredParam(GridlessBuild.LatticeKey, lattice);
            GridlessBuild.SetGroupId(part, manipulatedGroup);
        }
        GridlessBuild.InvalidateBounds(c);
        return absorb.Count - 1;
    }

    private struct Member
    {
        public BasePart part;
        public int group, lattice;
        public BuildMachineTransform frame;
        public bool hasGroup, hasLattice, hasX, hasY, hasAngle;
    }
    public sealed class Snapshot
    {
        private readonly List<Member> members = new List<Member>();
        internal Snapshot(Contraption c)
        {
            foreach (BasePart part in c.Parts)
                if (part != null && !part.m_static) members.Add(new Member { part = part, group = GridlessBuild.GetGroupId(part),
                    lattice = GridlessBuild.GetLatticeId(part), frame = BuildMachineTransform.Read(part),
                    hasGroup = part.TryGetStoredParam(GridlessBuild.GroupKey, out _), hasLattice = part.TryGetStoredParam(GridlessBuild.LatticeKey, out _),
                    hasX = part.TryGetStoredParam(BuildMachineTransform.XKey, out _), hasY = part.TryGetStoredParam(BuildMachineTransform.YKey, out _),
                    hasAngle = part.TryGetStoredParam(BuildMachineTransform.AngleKey, out _) });
        }
        public void Restore(Contraption c)
        {
            var live = new HashSet<BasePart>(c.Parts);
            foreach (Member m in members)
            {
                if (m.part == null || !live.Contains(m.part)) continue;
                Set(m.part, GridlessBuild.GroupKey, m.hasGroup, m.group);
                Set(m.part, GridlessBuild.LatticeKey, m.hasLattice, m.lattice);
                Set(m.part, BuildMachineTransform.XKey, m.hasX, m.frame.translation.x);
                Set(m.part, BuildMachineTransform.YKey, m.hasY, m.frame.translation.y);
                Set(m.part, BuildMachineTransform.AngleKey, m.hasAngle, m.frame.angle);
                c.RefreshPartPose(m.part); PartRotationAnimator.Complete(m.part.transform);
            }
            GridlessBuild.InvalidateBounds(c);
        }
        private static void Set(BasePart part, string key, bool exists, float value)
        { if (exists) part.SetStoredParam(key, value); else part.RemoveStoredParam(key); }
    }
    public static Snapshot Capture(Contraption c) => new Snapshot(c);

    /// <summary>Connect surviving interfaces between the saved lattices of a merged machine.</summary>
    public static void ConnectRuntime(Contraption c)
    {
        if (c == null || ContraptionLayers.Enabled(c) || !c.Parts.Exists(p => p != null && p.TryGetStoredParam(GridlessBuild.LatticeKey, out _))) return;
        var pairs = new HashSet<(int a, int b)>();
        VisitContacts(c, false, (a, b, anchor) => {
            int first = a.part.GetInstanceID(), second = b.part.GetInstanceID();
            if (!pairs.Add(first < second ? (first, second) : (second, first)) || c.HasJointBetween(a.part, b.part)) return;
            // Imported axles and servo platforms own their attachment body and joint type.
            if (a.part is RCR2Port.RoverPortPart portA) { portA.ConnectAtAnchor(b.part, anchor); return; }
            if (b.part is RCR2Port.RoverPortPart portB) { portB.ConnectAtAnchor(a.part, anchor); return; }
            Joint joint = null; BasePart source = a.part, target = b.part;
            if (a.side >= 0 && (a.part.m_customJointConnectionDirection.ToBitDirection() & (BitDirection)(1 << a.side)) != BitDirection.None)
                joint = source.CustomConnectToPart(target);
            else if (b.side >= 0 && (b.part.m_customJointConnectionDirection.ToBitDirection() & (BitDirection)(1 << b.side)) != BitDirection.None)
            { source = b.part; target = a.part; joint = source.CustomConnectToPart(target); }
            if (joint != null) c.AddJointToMap(source, target, joint);
            else c.AddFixedJoint(a.part, b.part, anchor);
        });
    }
}
