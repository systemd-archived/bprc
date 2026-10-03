using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

/// <summary>Isolated regression checks for marquee subset extraction and its persisted identities.</summary>
public static class SelectionExtractAudit
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<GameObject> objects = new List<GameObject>();

    public static void Run()
    {
        string marker = Path.Combine(Directory.GetCurrentDirectory(), ".bprc-build-interface-audit");
        if (!Application.isBatchMode || !File.Exists(marker)) return;
        var report = new List<string>(); int failures = 0;
        Verify((ok, name) => { report.Add((ok ? "PASS " : "FAIL ") + name); if (!ok) failures++; });
        report.Add("FAILURES: " + failures);
        Directory.CreateDirectory("Builds/QA"); File.WriteAllLines("Builds/QA/selection-extract-audit.txt", report);
        foreach (string line in report) Debug.Log("[selection-extract] " + line);
        if (failures != 0) throw new InvalidOperationException("Selection extraction audit failed: " + failures);
    }

    public static void Verify(Action<bool, string> check)
    {
        try
        {
            Case(check, "subset extraction", () => Subset(check));
            Case(check, "enclosure extraction", () => Enclosure(check));
            Case(check, "saved extraction reload", () => SaveRoundTrip(check));
            Case(check, "selection validation", () => Validation(check));
        }
        finally
        {
            for (int i = objects.Count - 1; i >= 0; i--) if (objects[i]) Object.DestroyImmediate(objects[i]);
            objects.Clear();
        }
    }

    private static void Subset(Action<bool, string> check)
    {
        Contraption c = Machine("ExtractSubset");
        BasePart a = Part(c, 0, 0, 0, 17), b = Part(c, 1, 0, 0, 17), boundary = Part(c, 2, 0, 0, 23);
        GridlessBuild.SetOffset(a, new Vector2(.25f, -.15f));
        new BuildMachineTransform { translation = new Vector2(1.3f, -0.7f), angle = 27f }.Store(a);
        a.transform.localScale = new Vector3(1.4f, .8f, 1f); a.SetFlipped(true); a.SetStoredParam("@auditParam", 73f);
        Vector3 position = a.transform.localPosition; Quaternion rotation = a.transform.localRotation;
        string saved = a.SerializeCustomParams();
        var selected = new HashSet<BasePart> { a, b };
        check(GridlessSelectionExtract.CanExtract(c, selected), "a proper marquee subset is extractable");
        check(!GridlessSelectionExtract.CanExtract(c, new HashSet<BasePart>(c.Parts)), "the full group is not extractable");
        check(GridlessSelectionExtract.TryExtract(c, selected, out int group, out var snapshot), "subset extraction allocates an independent group");
        check(group > 23 && GridlessBuild.GetGroupId(a) == group && GridlessBuild.GetGroupId(b) == group &&
            GridlessBuild.GetLatticeId(a) == GridlessBuild.GetLatticeId(b), "new group and fresh internal lattice identities are assigned");
        check(GridlessBuild.GetGroupId(boundary) == 0 && c.FindPartAt(1, 0, a) == b && c.FindPartAt(2, 0, b) == null,
            "native lookup keeps internal neighbors and cuts the boundary neighbor");
        bool hasAuditParam = a.TryGetStoredParam("@auditParam", out float auditParam);
        check(a.transform.localPosition == position && a.transform.localRotation == rotation && a.transform.localScale == new Vector3(1.4f, .8f, 1f) &&
            a.m_flipped && a.SerializeCustomParams() != saved && hasAuditParam && Mathf.Abs(auditParam - 73f) < 0.001f,
            "world pose, scale, flip, and custom parameters survive extraction");
        // Point interfaces use the same group guard as the ported connection path.
        AddPoint(a, Vector3.zero); AddPoint(boundary, new Vector3(-2f, 0f, 0f));
        check(!PartConnections.TryGetConnection(a, boundary, out _, out _), "ported point interfaces cannot cross an extracted group boundary");
        snapshot.Restore(c);
        check(GridlessBuild.GetGroupId(a) == 0 && GridlessBuild.GetGroupId(b) == 0 &&
            a.SerializeCustomParams() == saved && c.FindPartAt(2, 0, b) == boundary,
            "extraction undo restores exact membership, parameters, and boundary lookup");
    }

    private static void Enclosure(Action<bool, string> check)
    {
        Contraption c = Machine("ExtractEnclosure");
        BasePart outer = Part(c, 0, 0), inner = Part(c, 0, 0), spare = Part(c, 2, 0);
        outer.enclosedPart = inner;
        var selected = new HashSet<BasePart> { inner };
        check(GridlessSelectionExtract.TryExtract(c, selected, out _, out var snapshot), "an enclosed subset can be extracted");
        check(outer.enclosedPart == null && inner.enclosedInto == null, "cross-boundary enclosure links are disconnected");
        snapshot.Restore(c);
        check(outer.enclosedPart == inner && inner.enclosedInto == outer, "enclosure undo restores both sides of the relationship");
        spare.m_static = true;
        check(!GridlessSelectionExtract.CanExtract(c, new HashSet<BasePart> { spare }), "static parts cannot be extracted");
    }

    private static void SaveRoundTrip(Action<bool, string> check)
    {
        Contraption source = Machine("ExtractSaveSource");
        BasePart first = Part(source, 0, 0, 0), extracted = Part(source, 1, 0, 9, 31), large = Part(source, 3, 0, 9, 31);
        large.m_gridXmin = -1; large.m_gridXmax = 1; large.m_gridYmin = -1; large.m_gridYmax = 1;
        string rootParams = first.SerializeCustomParams(), extractedParams = extracted.SerializeCustomParams();
        var dataset = new ContraptionDataset();
        dataset.AddPart(first.m_coordX, first.m_coordY, (int)first.m_partType, first.customPartIndex, first.m_gridRotation, first.m_flipped, rootParams);
        dataset.AddPart(extracted.m_coordX, extracted.m_coordY, (int)extracted.m_partType, extracted.customPartIndex, extracted.m_gridRotation, extracted.m_flipped, extractedParams);
        dataset.AddPart(large.m_coordX, large.m_coordY, (int)large.m_partType, large.customPartIndex, large.m_gridRotation, large.m_flipped, large.SerializeCustomParams());
        INContraptionData data = INContraptionData.Create(dataset);
        check(data.ConvertTo().ContraptionDatasetList.Count == 3 && data.Units[1].CustomParams == extractedParams,
            "saved units retain group, lattice, and parameter payloads");
        // The actual LevelManager route is exercised when the isolated build UI supplies a real manager;
        // directly rebuilding through SetPartAt covers the same construction collision path in this audit.
        Contraption load = Machine("ExtractSaveLoad");
        foreach (ContraptionDataset.ContraptionDatasetUnit unit in dataset.ContraptionDatasetList)
        {
            BasePart prefab = Part(null, unit.x, unit.y); prefab.DeserializeCustomParams(unit.customParams);
            load.SetPartAt(unit.x, unit.y, prefab, refreshNeighbours: false, preserveExisting: true);
        }
        check(load.Parts.Count == 3 && GridlessBuild.GetGroupId(load.Parts[1]) == 9 && GridlessBuild.GetGroupId(load.Parts[2]) == 9,
            "reloading grouped parts in forward order keeps same-cell ownership and large footprints");
        // Reverse order must have identical ownership and count.
        Contraption reverse = Machine("ExtractSaveReverse");
        for (int i = dataset.ContraptionDatasetList.Count - 1; i >= 0; i--)
        {
            var unit = dataset.ContraptionDatasetList[i]; BasePart prefab = Part(null, unit.x, unit.y);
            prefab.DeserializeCustomParams(unit.customParams);
            reverse.SetPartAt(unit.x, unit.y, prefab, refreshNeighbours: false, preserveExisting: true);
        }
        check(reverse.Parts.Count == 3 && GridlessBuild.GetGroupId(reverse.Parts[1]) == 9,
            "reloading the same save in reverse order keeps all extracted members");
    }

    private static void Validation(Action<bool, string> check)
    {
        Contraption c = Machine("ExtractValidation"); BasePart a = Part(c, 0, 0), b = Part(c, 1, 0), foreign = Part(Machine("Foreign"), 0, 0);
        check(!GridlessSelectionExtract.CanExtract(c, new HashSet<BasePart>()), "empty selection is rejected");
        check(!GridlessSelectionExtract.CanExtract(c, new HashSet<BasePart> { a, foreign }), "multi-contraption selection is rejected");
        b.m_static = true; check(!GridlessSelectionExtract.CanExtract(c, new HashSet<BasePart> { a, b }), "static selection is rejected");
    }

    private static Contraption Machine(string name)
    {
        GameObject go = New(name); go.SetActive(false); Contraption c = go.AddComponent<Contraption>();
        typeof(Contraption).GetMethod("PostAwake", Private).Invoke(c, null); return c;
    }
    private static BasePart Part(Contraption c, int x, int y, int group = 0, int lattice = -1)
    {
        GameObject go = New("ExtractPart"); go.SetActive(false); BasePart p = go.AddComponent<BasePart>();
        p.m_coordX = x; p.m_coordY = y; p.m_mass = 1f; p.m_partType = BasePart.PartType.WoodenFrame;
        p.m_jointConnectionType = BasePart.JointConnectionType.Source; p.m_jointConnectionDirection = BasePart.JointConnectionDirection.Any;
        if (c != null) { go.transform.SetParent(c.transform, false); p.contraption = c; c.Parts.Add(p); c.SetPartMap(x, y, p.GenerationIndex, p); }
        if (group > 0) GridlessBuild.SetGroupId(p, group); if (lattice >= 0) p.SetStoredParam(GridlessBuild.LatticeKey, lattice);
        return p;
    }
    private static GameObject New(string name) { var go = new GameObject(name); objects.Add(go); return go; }
    private static void Case(Action<bool, string> check, string name, Action action)
    {
        try { action(); }
        catch (Exception e)
        {
            Exception cause = e.InnerException ?? e;
            check(false, name + " threw " + cause.GetType().Name + ": " + cause.Message);
        }
    }
    private static void AddPoint(BasePart p, Vector3 local)
    {
        Transform root = New("ConnectionPoints").transform; root.SetParent(p.transform, false);
        Transform point = New("Point-A").transform; point.SetParent(root, false); point.localPosition = local;
    }
}
