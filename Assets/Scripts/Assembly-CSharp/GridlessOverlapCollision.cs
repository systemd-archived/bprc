using System.Collections.Generic;
using UnityEngine;

/// <summary>Ignore initial intersecting part shapes, without disabling ground or later contacts.</summary>
public static class GridlessOverlapCollision
{
    private sealed class Entry
    {
        public BasePart part;
        public readonly List<Collider> colliders = new List<Collider>();
        public Bounds bounds;
    }

    public static void Apply(Contraption contraption)
    {
        if (contraption == null) return;
        bool overlaps = GridlessBuild.Enabled && (INUserSettings.Instance?.GeneralSettings?.IgnoreGridlessOverlap ?? true);
        var entries = new List<Entry>();
        var colliders = new List<Collider>();
        bool layered = false;
        foreach (var part in contraption.Parts)
        {
            if (part == null) continue;
            // The opt-in controller owns cross-layer pairs so it can restore them after layer/component changes.
            layered |= !ContraptionLayers.Enabled(contraption) && PartPhysicsParameters.Layer(part) > 1;
            colliders.Clear(); part.GetComponentsInChildren(true, colliders);
            var entry = new Entry { part = part };
            foreach (var collider in colliders)
                if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy && !collider.isTrigger &&
                    collider.GetComponentInParent<BasePart>() == part)
                    entry.colliders.Add(collider);
            if (entry.colliders.Count > 0) entries.Add(entry);
        }
        if (!overlaps && !layered) return;
        Physics.SyncTransforms();
        foreach (var entry in entries)
        {
            entry.bounds = entry.colliders[0].bounds;
            for (int i = 1; i < entry.colliders.Count; i++) entry.bounds.Encapsulate(entry.colliders[i].bounds);
        }
        entries.Sort((a, b) => a.bounds.min.x.CompareTo(b.bounds.min.x));
        for (int i = 0; i < entries.Count; i++)
        for (int j = i + 1; j < entries.Count; j++)
        {
            Entry a = entries[i], b = entries[j];
            bool differentLayers = layered && !PartPhysicsParameters.SameLayer(a.part, b.part);
            if (!layered && b.bounds.min.x >= a.bounds.max.x) break;
            if (!differentLayers && (!overlaps || !a.bounds.Intersects(b.bounds) || !Intersects(a, b))) continue;
            foreach (var ac in a.colliders)
            foreach (var bc in b.colliders)
                if (!GamePhysics.IsIgnoredPair(ac, bc)) GamePhysics.IgnoreCollision(ac, bc);
        }
    }

    private static bool Intersects(Entry a, Entry b)
    {
        foreach (var ac in a.colliders)
        foreach (var bc in b.colliders)
            if (ac.bounds.Intersects(bc.bounds) && Physics.ComputePenetration(
                ac, ac.transform.position, ac.transform.rotation,
                bc, bc.transform.position, bc.transform.rotation, out _, out float distance) && distance > 0.001f) return true;
        return false;
    }
}
