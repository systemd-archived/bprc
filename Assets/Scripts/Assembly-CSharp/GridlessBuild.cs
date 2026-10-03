using System;
using System.Runtime.CompilerServices;
using UnityEngine;

/// <summary>Free placement outside the machine's one-cell boundary; shared lattice snapping inside.</summary>
public static class GridlessBuild
{
    private sealed class Box { public Vector2 offset; }
    private static readonly ConditionalWeakTable<BasePart, Box> offsets = new ConditionalWeakTable<BasePart, Box>();
    public sealed class BuildGroup
    {
        public int id;
        public Rect bounds;
        public BuildMachineTransform frame;
        public Vector2 origin;
        internal readonly System.Collections.Generic.List<BasePart> members = new System.Collections.Generic.List<BasePart>();
    }
    private sealed class BoundsCache
    {
        public int frame = -1, count, nextGroup = 1;
        public bool usedGroupZero;
        public readonly System.Collections.Generic.List<BuildGroup> groups = new System.Collections.Generic.List<BuildGroup>();
        public readonly System.Collections.Generic.Dictionary<int, BuildGroup> byId = new System.Collections.Generic.Dictionary<int, BuildGroup>();
        public readonly System.Collections.Generic.List<int> expired = new System.Collections.Generic.List<int>();
    }
    private static readonly ConditionalWeakTable<Contraption, BoundsCache> boundsCache = new ConditionalWeakTable<Contraption, BoundsCache>();
    public static void InvalidateBounds(Contraption contraption)
    {
        if (contraption != null && boundsCache.TryGetValue(contraption, out BoundsCache cache)) cache.frame = -1;
    }
    public const string OffsetXKey = "@gridlessX";
    public const string OffsetYKey = "@gridlessY";
    public const string OutsideKey = "@gridlessOutside"; // Legacy marker, migrated into a separate group.
    public const string GroupKey = "@machineGroup";
    public const string LatticeKey = "@machineLattice";
    public const float MaxVehicleOffset = 8f;
    public const float VehicleStep = 0.1f;
    public static bool Enabled => INUserSettings.Instance?.GeneralSettings?.NoGridMode ?? false;
    public static event Action ModeChanged;
    public static void NotifyModeChanged() => ModeChanged?.Invoke();

    // Mode controls placement, never the positions already saved in a blueprint.
    public static Vector2 GetOffset(BasePart part) => CachedOrRestored(part);
    public static void Invalidate(BasePart part) { if (part != null) { offsets.Remove(part); InvalidateBounds(part.contraption); } }
    public static Vector2 CachedOrRestored(BasePart part)
    {
        if (part == null) return Vector2.zero;
        if (offsets.TryGetValue(part, out Box box)) return box.offset;
        if (!part.TryGetStoredParam(OffsetXKey, out float x)) part.TryGetStoredParam(PartPhysicsParameters.OffsetXKey, out x);
        if (!part.TryGetStoredParam(OffsetYKey, out float y)) part.TryGetStoredParam(PartPhysicsParameters.OffsetYKey, out y);
        Vector2 restored = BuildMachineTransform.Finite(x) && BuildMachineTransform.Finite(y) ? new Vector2(x, y) : Vector2.zero;
        offsets.Add(part, new Box { offset = restored });
        return restored;
    }
    public static void SetOffset(BasePart part, Vector2 offset)
    {
        if (part == null || !BuildMachineTransform.Finite(offset.x) || !BuildMachineTransform.Finite(offset.y)) return;
        Invalidate(part);
        offsets.Add(part, new Box { offset = offset });
        part.SetStoredParam(OffsetXKey, offset.x);
        part.SetStoredParam(OffsetYKey, offset.y);
        if (part.HasCustomParam(PartPhysicsParameters.OffsetXKey)) part.SetStoredParam(PartPhysicsParameters.OffsetXKey, offset.x);
        if (part.HasCustomParam(PartPhysicsParameters.OffsetYKey)) part.SetStoredParam(PartPhysicsParameters.OffsetYKey, offset.y);
    }
    public static bool IsOutside(BasePart part) => GetGroupId(part) > 0 || (part != null && part.TryGetStoredParam(OutsideKey, out float value) && value > 0.5f);
    public static void SetOutside(BasePart part, bool outside)
    {
        if (part == null) return;
        if (outside) part.SetStoredParam(OutsideKey, 1f); else part.RemoveStoredParam(OutsideKey);
        InvalidateBounds(part.contraption);
    }
    public static Vector2 GetVehicleOffset(Contraption contraption) => BuildMachineTransform.Read(contraption).translation;
    public static Vector2 ClampVehicleOffset(Vector2 value) => new Vector2(Mathf.Clamp(value.x, -MaxVehicleOffset, MaxVehicleOffset), Mathf.Clamp(value.y, -MaxVehicleOffset, MaxVehicleOffset));
    public static void SetVehicleOffset(Contraption contraption, Vector2 value)
    {
        var frame = BuildMachineTransform.Read(contraption);
        frame.translation = value;
        BuildMachineTransform.Apply(contraption, frame);
    }
    public static bool NudgeVehicle(Contraption contraption, int dx, int dy)
    {
        if (contraption == null || (dx == 0 && dy == 0)) return false;
        SetVehicleOffset(contraption, GetVehicleOffset(contraption) + new Vector2(dx, dy) * VehicleStep);
        return true;
    }
    public static void ResetVehicleOffset(Contraption contraption) => SetVehicleOffset(contraption, Vector2.zero);

    public static int GetGroupId(BasePart part)
    {
        return part != null && part.TryGetStoredParam(GroupKey, out float id) && BuildMachineTransform.Finite(id) && id > 0f && id < 16777215f
            ? Mathf.RoundToInt(id) : 0;
    }
    public static void SetGroupId(BasePart part, int id)
    {
        if (part == null) return;
        if (id > 0) part.SetStoredParam(GroupKey, id); else part.RemoveStoredParam(GroupKey);
        part.RemoveStoredParam(OutsideKey);
        InvalidateBounds(part.contraption);
    }
    public static int GetLatticeId(BasePart part) => part != null && part.TryGetStoredParam(LatticeKey, out float id) &&
        BuildMachineTransform.Finite(id) && id >= 0f && id < 16777215f ? Mathf.RoundToInt(id) : GetGroupId(part);
    public static int AllocateGroupId(Contraption contraption)
    {
        if (contraption == null) return -1;
        BoundsCache cache = boundsCache.GetOrCreateValue(contraption);
        // Allocation must not migrate legacy markers or change the subset being split.
        foreach (BasePart part in contraption.Parts)
            if (part != null && !part.m_static) cache.nextGroup = Mathf.Max(cache.nextGroup,
                Mathf.Max(GetGroupId(part), GetLatticeId(part)) + 1);
        return cache.nextGroup < 16777215 ? cache.nextGroup++ : -1;
    }
    public static System.Collections.Generic.IReadOnlyList<BuildGroup> GetGroups(Contraption contraption)
    {
        if (contraption == null) return System.Array.Empty<BuildGroup>();
        BoundsCache cache = boundsCache.GetOrCreateValue(contraption);
        if (cache.frame == Time.frameCount && cache.count == contraption.Parts.Count) return cache.groups;
        foreach (BuildGroup group in cache.byId.Values) group.members.Clear();
        cache.groups.Clear();
        // Old outside placements were invisible to the boundary. Recover them as independent groups,
        // retaining their stored pose rather than moving an existing blueprint during migration.
        foreach (BasePart part in contraption.Parts)
            if (part != null && !part.m_static) cache.nextGroup = Mathf.Max(cache.nextGroup,
                Mathf.Max(GetGroupId(part), GetLatticeId(part)) + 1);
        foreach (BasePart part in contraption.Parts)
        {
            if (part == null || part.m_static) continue;
            if (!part.TryGetStoredParam(GroupKey, out _) && part.TryGetStoredParam(OutsideKey, out float outside) && outside > 0.5f)
                SetGroupId(part, cache.nextGroup++);
            int id = GetGroupId(part);
            if (id == 0) cache.usedGroupZero = true;
            if (!cache.byId.TryGetValue(id, out BuildGroup group))
            {
                group = new BuildGroup { id = id }; cache.byId.Add(id, group);
            }
            if (group.members.Count == 0)
            {
                cache.groups.Add(group); group.origin = GetOffset(part); group.frame = BuildMachineTransform.Read(part);
                group.bounds = PartBounds(part, group.frame);
            }
            else
            {
                Rect footprint = PartBounds(part, group.frame);
                group.bounds = Rect.MinMaxRect(Mathf.Min(group.bounds.xMin, footprint.xMin), Mathf.Min(group.bounds.yMin, footprint.yMin),
                    Mathf.Max(group.bounds.xMax, footprint.xMax), Mathf.Max(group.bounds.yMax, footprint.yMax));
            }
            group.members.Add(part);
        }
        cache.expired.Clear();
        foreach (var pair in cache.byId) if (pair.Value.members.Count == 0) cache.expired.Add(pair.Key);
        foreach (int id in cache.expired) cache.byId.Remove(id);
        cache.frame = Time.frameCount; cache.count = contraption.Parts.Count;
        return cache.groups;
    }
    private static Rect PartBounds(BasePart part, BuildMachineTransform groupFrame)
    {
        BuildMachineTransform ownFrame = BuildMachineTransform.Read(part);
        Vector2 pos = new Vector2(part.m_coordX, part.m_coordY) + GetOffset(part);
        bool measuredFootprint = part is RCR2Port.RoverPortPart;
        Vector3 scale = measuredFootprint ? Vector3.one : part.BuildFootprintScale;
        float angle = measuredFootprint ? 0f : FineRotate.GetAngle(part, part.GetRotationAngle(part.m_gridRotation)) + PartPhysicsParameters.Rotation(part);
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity), max = -min;
        for (int corner = 0; corner < 4; corner++)
        {
            Vector2 size = new Vector2((corner % 2 == 0 ? part.m_gridXmin - 0.5f : part.m_gridXmax + 0.5f) * scale.x,
                (corner < 2 ? part.m_gridYmin - 0.5f : part.m_gridYmax + 0.5f) * scale.y);
            Vector2 p = groupFrame.InversePoint(ownFrame.TransformPoint(pos + BuildMachineTransform.Rotate(size, angle)));
            min = Vector2.Min(min, p); max = Vector2.Max(max, p);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
    public static int PrimaryGroup(Contraption contraption)
    {
        var groups = GetGroups(contraption);
        foreach (BuildGroup group in groups) if (group.id == 0) return 0;
        return groups.Count == 0 ? 0 : groups[0].id;
    }
    public static bool TryGetGroup(Contraption contraption, int id, out BuildGroup group)
    {
        GetGroups(contraption);
        group = null;
        return contraption != null && boundsCache.TryGetValue(contraption, out BoundsCache cache) &&
            cache.byId.TryGetValue(id, out group) && group.members.Count > 0;
    }
    private static Rect Expand(Rect bounds, float margin) => Rect.MinMaxRect(bounds.xMin - margin, bounds.yMin - margin, bounds.xMax + margin, bounds.yMax + margin);
    public static bool TryGetGroupBounds(Contraption contraption, int id, out Rect bounds, float margin = 1f)
    {
        bool found = TryGetGroup(contraption, id, out BuildGroup group);
        bounds = found ? Expand(group.bounds, margin) : new Rect(); return found;
    }
    /// <summary>Compatibility API for the original/primary group only.</summary>
    public static bool TryGetBounds(Contraption contraption, out Rect bounds, float margin = 1f) => TryGetGroupBounds(contraption, PrimaryGroup(contraption), out bounds, margin);
    public static bool TryFindGroupAt(Contraption contraption, Vector2 machinePoint, out BuildGroup result)
    {
        result = null; float nearest = float.PositiveInfinity;
        foreach (BuildGroup group in GetGroups(contraption))
        {
            Vector2 point = group.frame.InversePoint(machinePoint);
            if (!ContainsInclusive(Expand(group.bounds, 1f), point)) continue;
            if (result == null) { result = group; continue; }
            // Only overlapping margins need the more expensive nearest-member comparison.
            if (float.IsPositiveInfinity(nearest)) nearest = NearestMemberDistance(result, machinePoint);
            float distance = NearestMemberDistance(group, machinePoint);
            if (distance < nearest) { nearest = distance; result = group; }
        }
        return result != null;
    }
    private static float NearestMemberDistance(BuildGroup group, Vector2 machinePoint)
    {
        float distance = float.PositiveInfinity;
        foreach (BasePart part in group.members)
            distance = Mathf.Min(distance, (machinePoint - BuildMachineTransform.Read(part).TransformPoint(
                new Vector2(part.m_coordX, part.m_coordY) + GetOffset(part))).sqrMagnitude);
        return distance;
    }
    public static Vector2 ResolvePoint(Contraption contraption, Vector2 machinePoint, bool snap, out int groupId, out BuildMachineTransform frame)
    {
        BuildGroup group;
        bool found = TryFindGroupAt(contraption, machinePoint, out group);
        if (!found && !Enabled) found = TryGetGroup(contraption, PrimaryGroup(contraption), out group);
        groupId = found ? group.id : -1; frame = found ? group.frame : (Enabled ? ProposeOutsideFrame(contraption, machinePoint) : new BuildMachineTransform());
        Vector2 point = frame.InversePoint(machinePoint);
        return found && snap && Enabled ? SnapToGroup(group, point) : point;
    }
    private static BuildMachineTransform ProposeOutsideFrame(Contraption contraption, Vector2 point)
    {
        // A moved/rotated group's logical cell can lie under empty world space. Give a new
        // frame a free seed cell while keeping its world position and zero angle unchanged.
        var frame = new BuildMachineTransform();
        if (contraption == null) return frame;
        int x = Mathf.RoundToInt(point.x), y = Mathf.RoundToInt(point.y);
        if (!LogicalCellOccupied(contraption, x, y)) return frame;
        for (int radius = 1; radius <= 64; radius++)
            for (int side = 0; side < 4; side++)
            {
                int cx = x + (side == 0 ? radius : side == 1 ? -radius : 0);
                int cy = y + (side == 2 ? radius : side == 3 ? -radius : 0);
                if (LogicalCellOccupied(contraption, cx, cy)) continue;
                frame.translation = new Vector2(x - cx, y - cy); return frame;
            }
        return frame;
    }
    private static bool LogicalCellOccupied(Contraption contraption, int x, int y)
    {
        foreach (BasePart part in contraption.Parts)
            if (part != null && x >= part.m_coordX + part.m_gridXmin && x <= part.m_coordX + part.m_gridXmax &&
                y >= part.m_coordY + part.m_gridYmin && y <= part.m_coordY + part.m_gridYmax) return true;
        return false;
    }
    private static Vector2 SnapToGroup(BuildGroup group, Vector2 point) => group.origin + new Vector2(Mathf.Round(point.x - group.origin.x), Mathf.Round(point.y - group.origin.y));
    public static Vector2 SnapPointer(Contraption contraption, Vector2 point)
    {
        if (!Enabled || !TryGetGroup(contraption, PrimaryGroup(contraption), out BuildGroup group) || !ContainsInclusive(Expand(group.bounds, 1f), point)) return point;
        return SnapToGroup(group, point);
    }
    public static bool ContainsInclusive(Rect bounds, Vector2 point) => point.x >= bounds.xMin && point.x <= bounds.xMax && point.y >= bounds.yMin && point.y <= bounds.yMax;
    public static Vector2 ComputeOffset(Contraption contraption, int coordX, int coordY, Vector2 pointerLocal)
    {
        return Enabled && contraption != null ? SnapPointer(contraption, pointerLocal) - new Vector2(coordX, coordY) : Vector2.zero;
    }
    private static Vector2 pending;
    private static BuildMachineTransform pendingFrame;
    private static int pendingGroup;
    private static int pendingLattice;
    private static bool pendingMerged;
    private static bool hasPending;
    // Retained for callers which pass a point in the primary group's build frame.
    public static void Prepare(Contraption contraption, int coordX, int coordY, Vector2 point) => PrepareMachinePoint(contraption, coordX, coordY, BuildMachineTransform.Read(contraption).TransformPoint(point));
    public static void PrepareWorld(Contraption contraption, int coordX, int coordY, Vector3 world) => PrepareMachinePoint(contraption, coordX, coordY, contraption != null ? (Vector2)contraption.transform.InverseTransformPoint(world) : (Vector2)world);
    private static void PrepareMachinePoint(Contraption contraption, int coordX, int coordY, Vector2 machinePoint)
    {
        Vector2 point = ResolvePoint(contraption, machinePoint, true, out pendingGroup, out pendingFrame);
        pendingMerged = TryGetGroup(contraption, pendingGroup, out BuildGroup target) &&
            target.members[0].TryGetStoredParam(LatticeKey, out _);
        pendingLattice = pendingMerged ? GetLatticeId(target.members[0]) : pendingGroup;
        if (pendingGroup < 0)
        {
            var cache = contraption != null ? boundsCache.GetOrCreateValue(contraption) : null;
            pendingGroup = cache == null || (cache.groups.Count == 0 && !cache.usedGroupZero) ? 0 : cache.nextGroup++;
        }
        pending = Enabled ? point - new Vector2(coordX, coordY) : Vector2.zero;
        hasPending = contraption != null;
    }
    public static void ConsumePending(BasePart part)
    {
        if (part != null && hasPending)
        {
            SetOffset(part, pending); SetGroupId(part, pendingGroup); pendingFrame.Store(part);
            if (pendingMerged) part.SetStoredParam(LatticeKey, pendingLattice);
            else part.RemoveStoredParam(LatticeKey);
        }
        hasPending = false;
    }
    private sealed class Original { public Vector3 position; }
    private static readonly ConditionalWeakTable<Transform, Original> originals = new ConditionalWeakTable<Transform, Original>();
    public static void MarkHover(Transform marker, Vector2 offset)
    {
        if (marker == null) return;
        if (!originals.TryGetValue(marker, out Original original))
        {
            original = new Original { position = marker.localPosition }; originals.Add(marker, original);
        }
        marker.localPosition = original.position + new Vector3(offset.x, offset.y);
    }
    public static void RestoreHover(Transform marker)
    {
        if (marker != null && originals.TryGetValue(marker, out Original original)) marker.localPosition = original.position;
    }
}
