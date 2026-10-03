using System.Collections.Generic;
using UnityEngine;

/// <summary>Splits a selected subset without rebuilding parts or changing their saved poses.</summary>
public static class GridlessSelectionExtract
{
    private struct Member
    {
        public BasePart part;
        public float group, lattice, outside;
        public bool hasGroup, hasLattice, hasOutside;
    }
    private struct Enclosure { public BasePart outer, inner; }

    public sealed class Snapshot
    {
        private readonly List<Member> members = new List<Member>();
        private readonly List<Enclosure> enclosures = new List<Enclosure>();
        public int OriginalGroup { get; private set; }
        public int ExtractedGroup { get; private set; }

        internal Snapshot(Contraption contraption, HashSet<BasePart> selected, int originalGroup, int extractedGroup)
        {
            OriginalGroup = originalGroup;
            ExtractedGroup = extractedGroup;
            foreach (BasePart part in selected)
            {
                var member = new Member { part = part };
                member.hasGroup = part.TryGetStoredParam(GridlessBuild.GroupKey, out member.group);
                member.hasLattice = part.TryGetStoredParam(GridlessBuild.LatticeKey, out member.lattice);
                member.hasOutside = part.TryGetStoredParam(GridlessBuild.OutsideKey, out member.outside);
                members.Add(member);
            }
            foreach (BasePart part in contraption.Parts)
                if (part != null && part.enclosedPart != null &&
                    selected.Contains(part) != selected.Contains(part.enclosedPart))
                    enclosures.Add(new Enclosure { outer = part, inner = part.enclosedPart });
        }

        internal void DisconnectEnclosures()
        {
            foreach (Enclosure link in enclosures)
            {
                if (link.outer.enclosedPart == link.inner) link.outer.enclosedPart = null;
                if (link.inner.enclosedInto == link.outer) link.inner.enclosedInto = null;
            }
        }

        public void Restore(Contraption contraption)
        {
            if (contraption == null || contraption.IsRunning) return;
            var live = new HashSet<BasePart>(contraption.Parts);
            foreach (Member member in members)
            {
                if (member.part == null || !live.Contains(member.part)) continue;
                RestoreParam(member.part, GridlessBuild.GroupKey, member.hasGroup, member.group);
                RestoreParam(member.part, GridlessBuild.LatticeKey, member.hasLattice, member.lattice);
                RestoreParam(member.part, GridlessBuild.OutsideKey, member.hasOutside, member.outside);
            }
            foreach (Enclosure link in enclosures)
            {
                if (link.outer == null || link.inner == null || !live.Contains(link.outer) || !live.Contains(link.inner) ||
                    link.outer.enclosedPart != null || link.inner.enclosedInto != null ||
                    link.outer.m_coordX != link.inner.m_coordX || link.outer.m_coordY != link.inner.m_coordY ||
                    GridlessBuild.GetGroupId(link.outer) != GridlessBuild.GetGroupId(link.inner) ||
                    !PartPhysicsParameters.SameLayer(link.outer, link.inner)) continue;
                link.outer.enclosedPart = link.inner;
            }
            // Group/lattice identities changed even when no enclosure crossed the marquee; rebuild
            // the occupancy map so undo immediately restores the original logical owner.
            Refresh(contraption, OriginalGroup, ExtractedGroup, true);
        }

        private static void RestoreParam(BasePart part, string key, bool exists, float value)
        {
            if (exists) part.SetStoredParam(key, value); else part.RemoveStoredParam(key);
        }
    }

    public static bool CanExtract(Contraption contraption, HashSet<BasePart> selected)
    {
        if (contraption == null || contraption.IsRunning || selected == null || selected.Count == 0) return false;
        int group = -1;
        foreach (BasePart part in selected)
        {
            if (part == null || part.m_static || part.contraption != contraption || !ContraptionLayers.Editable(part)) return false;
            int id = GridlessBuild.GetGroupId(part);
            if (group >= 0 && group != id) return false;
            group = id;
        }
        int members = 0, liveSelected = 0;
        foreach (BasePart part in contraption.Parts)
        {
            if (part == null || part.m_static || GridlessBuild.GetGroupId(part) != group) continue;
            members++;
            if (selected.Contains(part)) liveSelected++;
        }
        return liveSelected == selected.Count && liveSelected < members;
    }

    public static bool TryExtract(Contraption contraption, IEnumerable<BasePart> parts, out int extractedGroup, out Snapshot snapshot)
    {
        extractedGroup = -1;
        snapshot = null;
        if (parts == null) return false;
        var selected = new HashSet<BasePart>(parts);
        if (!CanExtract(contraption, selected)) return false;
        int originalGroup = -1;
        foreach (BasePart part in selected) { originalGroup = GridlessBuild.GetGroupId(part); break; }
        extractedGroup = GridlessBuild.AllocateGroupId(contraption);
        if (extractedGroup < 0) return false;
        var lattices = new Dictionary<int, int>();
        var targets = new Dictionary<BasePart, int>();
        foreach (BasePart part in selected)
        {
            int originalLattice = GridlessBuild.GetLatticeId(part);
            if (lattices.ContainsKey(originalLattice)) continue;
            int lattice = lattices.Count == 0 ? extractedGroup : GridlessBuild.AllocateGroupId(contraption);
            if (lattice < 0) { extractedGroup = -1; return false; }
            lattices.Add(originalLattice, lattice);
        }
        foreach (BasePart part in selected) targets[part] = lattices[GridlessBuild.GetLatticeId(part)];
        snapshot = new Snapshot(contraption, selected, originalGroup, extractedGroup);
        foreach (BasePart part in selected)
        {
            // Fresh lattice identities preserve internal topology when the moved subset is merged again.
            part.SetStoredParam(GridlessBuild.LatticeKey, targets[part]);
            GridlessBuild.SetGroupId(part, extractedGroup);
        }
        snapshot.DisconnectEnclosures();
        Refresh(contraption, originalGroup, extractedGroup, true);
        return true;
    }

    private static void Refresh(Contraption contraption, int originalGroup, int extractedGroup, bool rebuildMap)
    {
        if (rebuildMap) contraption.RebuildLayerMap();
        GridlessBuild.InvalidateBounds(contraption);
        ContraptionLayers.InvalidateView();
        foreach (BasePart part in contraption.Parts)
            if (part != null && (GridlessBuild.GetGroupId(part) == originalGroup || GridlessBuild.GetGroupId(part) == extractedGroup))
                part.ChangeVisualConnections();
    }
}
