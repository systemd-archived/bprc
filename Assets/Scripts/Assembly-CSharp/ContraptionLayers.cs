using System.Collections.Generic;
using System;
using System.Globalization;
using UnityEngine;

/// <summary>BPIR structural layers: layer one is the shared chassis, layers two to six are independent planes.</summary>
public static class ContraptionLayers
{
    public static int ActiveLayer { get; private set; } = 1;
    public static int Revision { get; private set; }
    public static void InvalidateView() { unchecked { Revision++; } }
    public static int QueryLayer => Mathf.Max(1, ActiveLayer);
    public static bool Enabled(Contraption c)
    {
        return c != null && (c.LayerRulesForRun.HasValue ? c.LayerRulesForRun.Value :
            ConstructionEnabled(c));
    }
    internal static bool ConstructionEnabled(Contraption c)
    {
        var settings = INUserSettings.Instance?.GeneralSettings;
        return settings?.MultiLayerBuild == true || c != null && c.ImportedLayers;
    }
    public static bool Editable(BasePart part) => part != null && (!Enabled(part.contraption) || ActiveLayer > 0 && PartPhysicsParameters.Layer(part) == ActiveLayer);
    public static void Select(int layer)
    {
        ActiveLayer = Mathf.Clamp(layer, 0, 6);
        INMachineBuildEditor.Instance?.DeselectPart();
        INMachineBuildEditor.Instance?.DeselectMachine();
        INSelectionManager.Instance?.CancelPaste();
        INPartParameterPanel.Close();
        WPFMonoBehaviour.levelManager?.ConstructionUI?.CancelDrag();
        ContraptionLayerView.Refresh(WPFMonoBehaviour.levelManager?.ContraptionProto);
    }
    public static bool Compatible(BasePart a, BasePart b) => a != null && b != null &&
        (PartPhysicsParameters.SameLayer(a, b) || a.contraption == b.contraption && Enabled(a.contraption) &&
            (PartPhysicsParameters.Layer(a) == 1 || PartPhysicsParameters.Layer(b) == 1));
    public static int SavedLayer(string parameters)
    {
        if (string.IsNullOrEmpty(parameters)) return 1;
        const string prefix = PartPhysicsParameters.LayerKey + ":";
        int start = 0, layer = 1;
        while (start < parameters.Length)
        {
            int end = parameters.IndexOf(';', start); if (end < 0) end = parameters.Length;
            if (end - start > prefix.Length && string.CompareOrdinal(parameters, start, prefix, 0, prefix.Length) == 0 &&
                float.TryParse(parameters.AsSpan(start + prefix.Length, end - start - prefix.Length), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out float value) && BuildMachineTransform.Finite(value))
                layer = Mathf.Clamp(Mathf.RoundToInt(value), 1, 6);
            start = end + 1;
        }
        return layer;
    }
    public static int CellLayer(Contraption c, int layer) => Enabled(c) ? layer : 1;

    public static bool ShapeChanged(BasePart part) => GridlessBuild.GetOffset(part) != Vector2.zero ||
        !Mathf.Approximately(PartPhysicsParameters.Rotation(part), 0f) ||
        part.HasCustomParam(BasePart.PartScaleKey) || !Mathf.Approximately(part.PartScaleFactors.x, 1f) ||
        !Mathf.Approximately(part.PartScaleFactors.y, 1f);

    internal static void RegisterRunning(Contraption c) => CrossVehicleLayerConnections.Register(c);
    internal static void UnregisterRunning(Contraption c) => CrossVehicleLayerConnections.Unregister(c);
}

public partial class Contraption
{
    [SerializeField] private bool m_importedLayers;
    internal bool ImportedLayers => m_importedLayers;
    internal void AdoptImportedLayers(BasePart part)
    {
        if (m_importedLayers || part == null || !part.HasCustomParam(BpirSaveConverter.SourceKey) || PartPhysicsParameters.Layer(part) <= 1) return;
        m_importedLayers = true;
        RebuildLayerMap();
    }
    internal bool? LayerRulesForRun { get; private set; }
    private void CaptureLayerRules() => LayerRulesForRun = ContraptionLayers.ConstructionEnabled(this);
    private int MapLayer(int layer) => ContraptionLayers.Enabled(this) ? Mathf.Clamp(layer, 1, 6) : 1;
    internal void ConnectNativeGridPart(BasePart part, HashSet<(BasePart, BasePart)> pairs)
    {
        bool all = INSettings.GetBool(INFeature.AllDirectionsConnection);
        bool bpir = ContraptionLayers.Enabled(this);
        int layer = PartPhysicsParameters.Layer(part);
        var shared = bpir && layer > 1 ? LayerTarget(part, part.m_coordX, part.m_coordY, 1) : null;
        for (int d = 0; d < 4; d++)
        {
            var bit = (BitDirection)(1 << d);
            if (!all && (bit == BitDirection.Left || bit == BitDirection.Up)) continue;
            var delta = bit.ToVector();
            var other = FindPartAt(part.m_coordX + delta.Item1, part.m_coordY + delta.Item2, part);
            if (bpir) ConnectLayerPair(part, other, (BasePart.Direction)d);
            else ConnectNativeGridPair(part, other, (BasePart.Direction)d, pairs);
            if (bpir && layer > 1 && shared == null)
                ConnectLayerPair(part, LayerTarget(part, part.m_coordX + delta.Item1, part.m_coordY + delta.Item2, 1), (BasePart.Direction)d);
        }
        // BPIR creates each part's public-layer mounts immediately after its own-plane neighbors.
        // Deferring these until all native parts are connected changes Spring's final tracked body.
        if (shared != null)
            for (int d = 0; d < 4; d++)
                if (ConnectLayerPair(part, shared, (BasePart.Direction)d)) break;
    }
    private void ConnectNativeGridPair(BasePart part, BasePart other, BasePart.Direction direction, HashSet<(BasePart, BasePart)> pairs)
    {
        if (part is RCR2Port.RoverPortPart || other is RCR2Port.RoverPortPart || !CanConnectTo(part, other, direction)) return;
        if (pairs.Contains((other, part)) || !pairs.Add((part, other))) return;
        var bit = (BitDirection)(1 << (int)direction);
        var own = part.GetCustomJointConnectionDirection().ToBitDirection();
        var target = other.GetCustomJointConnectionDirection().ToBitDirection();
        // Preserve the native spring mount priority introduced for Android wheel connections.
        bool imported = part.HasCustomParam(BpirSaveConverter.SourceKey) && other.HasCustomParam(BpirSaveConverter.SourceKey);
        bool useOther = (target & bit.Reverse()) != 0 && ((own & bit) == 0 || !imported && other is Spring && !(part is Spring));
        if (useOther) AddJointToMap(other, part, other.CustomConnectToPart(part));
        else if ((own & bit) != 0) AddJointToMap(part, other, part.CustomConnectToPart(other));
        else AddFixedJoint(part, other);
    }
    public BasePart FindPartAtInLayer(int x, int y, int generation, int layer)
    {
        m_runtimePartMap.TryGet(x, y, generation, out var part, MapLayer(layer));
        return part;
    }
    internal void RebuildLayerMap()
    {
        if (m_runtimePartMap == null) return;
        m_runtimePartMap.Clear();
        foreach (var part in m_parts)
        {
            if (part == null) continue;
            if (part.enclosedInto != null && !PartPhysicsParameters.SameLayer(part, part.enclosedInto))
            {
                if (part.enclosedInto.enclosedPart == part) part.enclosedInto.enclosedPart = null;
                part.enclosedInto = null;
            }
            if (part.enclosedInto == null) SetPartMap(part.m_coordX, part.m_coordY, part.GenerationIndex, part);
        }
        m_isValidationRequired = true;
        ContraptionLayers.InvalidateView();
    }
    private BasePart LayerTarget(BasePart part, int x, int y, int layer)
    {
        return FindPartAtInGroup(x, y, part.GenerationIndex, layer, GridlessBuild.GetGroupId(part), GridlessBuild.GetLatticeId(part));
    }
    // Matches BPIR ConnectPart/TryConnectSameCell. Generic fuel/electrical lookup remains within its own layer.
    internal void ConnectLayeredParts()
    {
        if (!ContraptionLayers.Enabled(this)) return;
        bool allDirections = INSettings.GetBool(INFeature.AllDirectionsConnection);
        foreach (var part in m_parts)
        {
            if (part == null || part.m_jointConnectionType == BasePart.JointConnectionType.None) continue;
            int layer = PartPhysicsParameters.Layer(part);
            if (layer > 1)
            {
                var basePart = LayerTarget(part, part.m_coordX, part.m_coordY, 1);
                if (basePart != null)
                {
                    for (int d = 0; d < 4; d++)
                        if (ConnectLayerPair(part, basePart, (BasePart.Direction)d)) break;
                }
                else
                {
                    for (int d = 0; d < 4; d++)
                    {
                        var bit = (BitDirection)(1 << d);
                        if (!allDirections && (bit == BitDirection.Left || bit == BitDirection.Up)) continue;
                        var delta = bit.ToVector();
                        ConnectLayerPair(part, LayerTarget(part, part.m_coordX + delta.Item1, part.m_coordY + delta.Item2, 1), (BasePart.Direction)d);
                    }
                }
            }
            // BPIR OffsetPartConnection creates additional joints in the rounded, visually offset cell.
            Vector2 offset = GridlessBuild.GetOffset(part);
            int x = Mathf.RoundToInt(part.m_coordX + offset.x), y = Mathf.RoundToInt(part.m_coordY + offset.y);
            if (x == part.m_coordX && y == part.m_coordY) continue;
            for (int d = 0; d < 4; d++)
            {
                var delta = ((BitDirection)(1 << d)).ToVector();
                ConnectLayerPair(part, LayerTarget(part, x + delta.Item1, y + delta.Item2, layer), (BasePart.Direction)d);
            }
        }
        // Separate gridless machine groups still share the same world. BPIR bridges
        // their lattices when their frame shapes physically overlap.
        if (GridlessBuild.Enabled) ConnectOverlappingFrames(this);
    }

    internal void ConnectOverlappingFrames(Contraption owner)
    {
        if (owner == null || !GridlessBuild.Enabled || !ContraptionLayers.Enabled(owner)) return;
        CrossVehicleLayerConnections.ConnectFrames(new[] { owner }, owner);
    }
    private bool ConnectLayerPair(BasePart part, BasePart other, BasePart.Direction direction)
    {
        if (other == null || other == part) return false;
        if (HasJointBetween(part, other)) return true;
        if (!CanConnectTo(part, other, direction)) return false;
        var bit = (BitDirection)(1 << (int)direction);
        var own = part.GetCustomJointConnectionDirection().ToBitDirection();
        var target = other.GetCustomJointConnectionDirection().ToBitDirection();
        if ((own & bit) != 0) AddJointToMap(part, other, part.CustomConnectToPart(other));
        else if ((target & bit.Reverse()) != 0) AddJointToMap(other, part, other.CustomConnectToPart(part));
        else AddFixedJoint(part, other);
        return true;
    }
}

/// <summary>Rebuilds only after connection changes, with zero per-frame scanning.</summary>
public sealed class ContraptionLayerCollisions : MonoBehaviour
{
    private Contraption m_owner;
    private readonly List<List<Collider>> m_shapes = new List<List<Collider>>();
    private readonly HashSet<(Collider, Collider)> m_owned = new HashSet<(Collider, Collider)>();
    private readonly HashSet<(Collider, Collider)> m_keep = new HashSet<(Collider, Collider)>();
    private readonly HashSet<(BasePart, BasePart)> m_links = new HashSet<(BasePart, BasePart)>();
    private readonly List<(Collider, Collider)> m_expired = new List<(Collider, Collider)>();
    private readonly List<Collider> m_buffer = new List<Collider>();

    public static void Install(Contraption owner)
    {
        if (!ContraptionLayers.Enabled(owner)) return;
        var control = owner.GetComponent<ContraptionLayerCollisions>() ?? owner.gameObject.AddComponent<ContraptionLayerCollisions>();
        if (control.m_owner != owner)
        {
            if (control.m_owner != null) control.m_owner.ConnectedComponentsChanged -= control.Refresh;
            control.m_owner = owner;
            owner.ConnectedComponentsChanged += control.Refresh;
        }
        control.Refresh();
    }
    public void Refresh()
    {
        if (m_owner == null) return;
        m_keep.Clear();
        m_links.Clear();
        foreach (var link in m_owner.JointMap)
            if (link.joint != null && link.partA != null && link.partB != null)
                m_links.Add((link.partA, link.partB));
        var parts = m_owner.Parts;
        while (m_shapes.Count < parts.Count) m_shapes.Add(new List<Collider>());
        for (int i = 0; i < parts.Count; i++)
        {
            var shapes = m_shapes[i]; shapes.Clear(); m_buffer.Clear();
            if (parts[i] == null) continue;
            parts[i].GetComponentsInChildren(true, m_buffer);
            foreach (var c in m_buffer)
                if (c.enabled && !c.isTrigger && c.gameObject.activeInHierarchy && c.GetComponentInParent<BasePart>() == parts[i]) shapes.Add(c);
        }
        bool selfCollision = INUserSettings.Instance?.GeneralSettings?.LayerSelfCollision ?? false;
        for (int i = 0; i < parts.Count; i++)
        for (int j = i + 1; j < parts.Count; j++)
        {
            var a = parts[i]; var b = parts[j];
            if (a == null || b == null) continue;
            bool sameLayer = PartPhysicsParameters.SameLayer(a, b);
            bool sameComponent = a.ConnectedComponent >= 0 && a.ConnectedComponent == b.ConnectedComponent;
            bool structural = m_links.Contains((a, b)) || m_links.Contains((b, a)) || a is Frame && b is Frame && sameLayer &&
                a.GenerationIndex == b.GenerationIndex && GridlessBuild.GetGroupId(a) == GridlessBuild.GetGroupId(b) &&
                Mathf.Abs(a.m_coordX - b.m_coordX) + Mathf.Abs(a.m_coordY - b.m_coordY) == 1 &&
                GridlessBuild.GetLatticeId(a) == GridlessBuild.GetLatticeId(b);
            bool sameCell = a.m_coordX == b.m_coordX && a.m_coordY == b.m_coordY;
            if (sameLayer && !structural && (!sameComponent || selfCollision && !sameCell &&
                !ContraptionLayers.ShapeChanged(a) && !ContraptionLayers.ShapeChanged(b))) continue;
            foreach (var ac in m_shapes[i])
            foreach (var bc in m_shapes[j])
            {
                var pair = ac.GetInstanceID() < bc.GetInstanceID() ? (ac, bc) : (bc, ac);
                m_keep.Add(pair);
                // Preserve ignores owned by native joints, overlap filtering and special parts.
                if (!m_owned.Contains(pair) && !GamePhysics.IsIgnoredPair(ac, bc) && !Physics.GetIgnoreCollision(ac, bc)) m_owned.Add(pair);
                GamePhysics.IgnoreCollision(ac, bc);
            }
        }
        m_expired.Clear();
        foreach (var pair in m_owned) if (!m_keep.Contains(pair)) m_expired.Add(pair);
        foreach (var pair in m_expired)
        {
            GamePhysics.IgnoreCollision(pair.Item1, pair.Item2, false);
            m_owned.Remove(pair);
        }
    }
    private void OnDestroy() { if (m_owner != null) m_owner.ConnectedComponentsChanged -= Refresh; }
}

/// <summary>
/// BPIR-style physical bridges between gridless layered vehicles. A vehicle can be a
/// separate gridless group in one Contraption or a separate running Contraption object.
/// The registry is rebuilt only when a vehicle starts or stops, so the physics loop does
/// not pay for scene-wide overlap searches.
/// </summary>
internal static class CrossVehicleLayerConnections
{
    private sealed class FrameEntry
    {
        public BasePart part;
        public readonly List<Collider> colliders = new List<Collider>();
        public Bounds bounds;
    }

    private sealed class Link
    {
        public Contraption first;
        public Contraption second;
        public Joint joint;
    }

    private static readonly HashSet<Contraption> running = new HashSet<Contraption>();
    private static readonly List<Link> links = new List<Link>();
    private static readonly List<Collider> colliderBuffer = new List<Collider>();

    internal static void Register(Contraption contraption)
    {
        if (contraption == null || !GridlessBuild.Enabled || !ContraptionLayers.Enabled(contraption)) return;
        running.Add(contraption);
        Rebuild();
    }

    internal static void Unregister(Contraption contraption)
    {
        if (contraption == null) return;
        running.Remove(contraption);
        for (int i = links.Count - 1; i >= 0; i--)
        {
            Link link = links[i];
            if (link.first != contraption && link.second != contraption) continue;
            link.first?.RemoveSharedJointFromMap(link.joint);
            link.second?.RemoveSharedJointFromMap(link.joint);
            if (link.joint != null) UnityEngine.Object.Destroy(link.joint);
            if (link.first != null && link.first.IsRunning) link.first.UpdateConnectedComponents();
            if (link.second != null && link.second.IsRunning) link.second.UpdateConnectedComponents();
            links.RemoveAt(i);
        }
    }

    internal static void ConnectFrames(IEnumerable<Contraption> vehicles, Contraption ownerFilter = null)
    {
        if (vehicles == null) return;
        var entries = new List<FrameEntry>();
        var seen = new HashSet<Contraption>();
        foreach (Contraption vehicle in vehicles)
        {
            if (vehicle == null || !seen.Add(vehicle) || !GridlessBuild.Enabled || !ContraptionLayers.Enabled(vehicle)) continue;
            if (ownerFilter == null && !vehicle.IsRunning) continue;
            foreach (BasePart part in vehicle.Parts)
            {
                if (part == null || !(part is Frame) && !BasePart.IsFramePart(part.m_partType) ||
                    part.m_jointConnectionType == BasePart.JointConnectionType.None) continue;
                colliderBuffer.Clear();
                part.GetComponentsInChildren(true, colliderBuffer);
                var entry = new FrameEntry { part = part };
                foreach (Collider collider in colliderBuffer)
                    if (collider != null && collider.enabled && !collider.isTrigger && collider.gameObject.activeInHierarchy &&
                        collider.GetComponentInParent<BasePart>() == part) entry.colliders.Add(collider);
                if (entry.colliders.Count == 0) continue;
                entry.bounds = entry.colliders[0].bounds;
                for (int i = 1; i < entry.colliders.Count; i++) entry.bounds.Encapsulate(entry.colliders[i].bounds);
                entries.Add(entry);
            }
        }
        if (entries.Count < 2) return;
        Physics.SyncTransforms();
        const float cellSize = 1f;
        var buckets = new Dictionary<(int x, int y), List<int>>();
        for (int i = 0; i < entries.Count; i++)
        {
            FrameEntry current = entries[i];
            int minX = Mathf.FloorToInt(current.bounds.min.x / cellSize);
            int maxX = Mathf.FloorToInt(current.bounds.max.x / cellSize);
            int minY = Mathf.FloorToInt(current.bounds.min.y / cellSize);
            int maxY = Mathf.FloorToInt(current.bounds.max.y / cellSize);
            var candidates = new HashSet<int>();
            for (int x = minX - 1; x <= maxX + 1; x++)
                for (int y = minY - 1; y <= maxY + 1; y++)
                    if (buckets.TryGetValue((x, y), out List<int> bucket))
                        foreach (int index in bucket) candidates.Add(index);
            foreach (int index in candidates)
            {
                FrameEntry other = entries[index];
                if (!CanBridge(current.part, other.part) || !current.bounds.Intersects(other.bounds) || !Intersects(current, other)) continue;
                ConnectPair(current.part, other.part);
            }
            for (int x = minX; x <= maxX; x++)
                for (int y = minY; y <= maxY; y++)
                {
                    if (!buckets.TryGetValue((x, y), out List<int> bucket)) buckets.Add((x, y), bucket = new List<int>());
                    bucket.Add(i);
                }
        }
    }

    private static bool CanBridge(BasePart first, BasePart second)
    {
        if (first == null || second == null || first == second) return false;
        if (first.contraption == second.contraption && GridlessBuild.GetGroupId(first) == GridlessBuild.GetGroupId(second)) return false;
        int a = PartPhysicsParameters.Layer(first), b = PartPhysicsParameters.Layer(second);
        return a == b || a == 1 || b == 1;
    }

    private static bool Intersects(FrameEntry first, FrameEntry second)
    {
        foreach (Collider a in first.colliders)
            foreach (Collider b in second.colliders)
                if (a.bounds.Intersects(b.bounds) && Physics.ComputePenetration(a, a.transform.position, a.transform.rotation,
                    b, b.transform.position, b.transform.rotation, out _, out float distance) && distance > 0.0001f) return true;
        return false;
    }

    private static void ConnectPair(BasePart first, BasePart second)
    {
        Contraption a = first.contraption, b = second.contraption;
        if (a == null || b == null) return;
        if (a == b)
        {
            if (a.HasJointBetween(first, second)) return;
        }
        else if (a.HasJointBetween(first, second) || b.HasJointBetween(first, second))
        {
            return;
        }
        Contraption owner = a.GetInstanceID() <= b.GetInstanceID() ? a : b;
        Contraption other = owner == a ? b : a;
        BasePart ownerPart = owner == a ? first : second;
        BasePart otherPart = owner == a ? second : first;
        Joint joint = owner.CreateLayerOverlapJoint(ownerPart, otherPart);
        if (joint == null) return;
        if (other != owner) other.AddSharedJointToMap(ownerPart, otherPart, joint);
        if (other != owner) links.Add(new Link { first = owner, second = other, joint = joint });
        if (owner.IsRunning) owner.UpdateConnectedComponents();
        if (other.IsRunning) other.UpdateConnectedComponents();
    }

    private static void Rebuild()
    {
        running.RemoveWhere(c => c == null || !c.IsRunning);
        ConnectFrames(running);
    }
}
