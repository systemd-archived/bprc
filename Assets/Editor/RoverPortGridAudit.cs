using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using RCR2Port;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Verifies that the ported RoverCraft 2 parts sit on BPLE's build grid the way RCR2 authored them:
/// the construction artwork must fill whole 1x1 cells and the runtime footprint must match the
/// footprint recovered from the source by Tools/RCR2/GridAlignment.py.
/// Run with: Unity -executeMethod RoverPortGridAudit.Run
/// </summary>
public static class RoverPortGridAudit
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const float LatticeTolerance = 0.02f;

    public static void Run()
    {
        var failures = new List<string>();
        var warnings = new List<string>();
        var rows = new List<string> { "id,name,category,phase,shift,cells,runtimeCells,overhang,gap,mounts" };
        var catalog = RoverPortCatalog.Instance;
        int aligned = 0;
        int mountsChecked = 0;
        float worstOverhang = 0f;
        foreach (GameObject prototype in catalog.Parts)
        {
            var part = prototype.GetComponent<RoverPortPart>();
            var data = typeof(RoverPortPart).GetField("m_portData", PrivateInstance).GetValue(part) as RoverPortPartData;
            if (data == null)
            {
                failures.Add(part.PortId + ": missing port data");
                continue;
            }
            string id = part.PortId;
            if (data.grid == null)
            {
                failures.Add(id + ": missing grid alignment block");
                continue;
            }
            bool shifted = data.grid.shift != null && Mathf.Abs(data.grid.shift[0]) + Mathf.Abs(data.grid.shift[1]) > 0.001f;
            if (shifted) aligned++;

            Bounds artwork;
            if (!TryArtworkBounds(prototype, out artwork))
            {
                failures.Add(id + ": no construction artwork");
                continue;
            }

            int xmin = (int)typeof(BasePart).GetField("m_gridXmin", AnyInstance).GetValue(part);
            int xmax = (int)typeof(BasePart).GetField("m_gridXmax", AnyInstance).GetValue(part);
            int ymin = (int)typeof(BasePart).GetField("m_gridYmin", AnyInstance).GetValue(part);
            int ymax = (int)typeof(BasePart).GetField("m_gridYmax", AnyInstance).GetValue(part);
            var cells = data.grid.cells;
            if (cells == null)
            {
                failures.Add(id + ": missing grid cells");
                continue;
            }
            if (xmin != cells.xmin || xmax != cells.xmax || ymin != cells.ymin || ymax != cells.ymax)
                failures.Add(string.Format("{0}: footprint x[{1},{2}] y[{3},{4}] does not match source cells x[{5},{6}] y[{7},{8}]",
                    id, xmin, xmax, ymin, ymax, cells.xmin, cells.xmax, cells.ymin, cells.ymax));
            if (!(cells.xmin <= 0 && 0 <= cells.xmax && cells.ymin <= 0 && 0 <= cells.ymax))
                warnings.Add(string.Format("{0}: part root (its grid anchor) is outside its own footprint x[{1},{2}] y[{3},{4}]",
                    id, cells.xmin, cells.xmax, cells.ymin, cells.ymax));

            // The artwork may be smaller than its cells, but it must not spill out of them.
            float overhang = Mathf.Max(
                Mathf.Max(0f, (cells.xmin - 0.5f) - artwork.min.x), Mathf.Max(0f, artwork.max.x - (cells.xmax + 0.5f)),
                Mathf.Max(0f, (cells.ymin - 0.5f) - artwork.min.y), Mathf.Max(0f, artwork.max.y - (cells.ymax + 0.5f)));
            float gap = Mathf.Max(
                Mathf.Max(0f, artwork.min.x - (cells.xmin - 0.5f)), Mathf.Max(0f, (cells.xmax + 0.5f) - artwork.max.x),
                Mathf.Max(0f, artwork.min.y - (cells.ymin - 0.5f)), Mathf.Max(0f, (cells.ymax + 0.5f) - artwork.max.y));
            if (overhang > worstOverhang)
                worstOverhang = overhang;

            // Every mount must stay on the lattice (cell centre / cell border), otherwise the part
            // could not connect to RCR2 or native BPLE parts any more.
            int mounts = 0;
            foreach (Vector3 point in part.GetConnectionPoints())
            {
                mounts++;
                mountsChecked++;
                if (OffLattice(point.x) || OffLattice(point.y))
                    failures.Add(string.Format("{0}: mount {1} is off the build lattice", id, point));
            }

            // The footprint must follow the artwork through all four grid rotations.
            foreach (int rotation in new[] { 1, 2, 3 })
            {
                part.SetRotation(rotation);
                int rx = (int)typeof(BasePart).GetField("m_gridXmin", AnyInstance).GetValue(part);
                int rx2 = (int)typeof(BasePart).GetField("m_gridXmax", AnyInstance).GetValue(part);
                int ry = (int)typeof(BasePart).GetField("m_gridYmin", AnyInstance).GetValue(part);
                int ry2 = (int)typeof(BasePart).GetField("m_gridYmax", AnyInstance).GetValue(part);
                int[] expected = RotateCells(cells.xmin, cells.xmax, cells.ymin, cells.ymax, rotation);
                if (rx != expected[0] || rx2 != expected[1] || ry != expected[2] || ry2 != expected[3])
                    failures.Add(string.Format("{0}: rotation {1} footprint x[{2},{3}] y[{4},{5}] != rotated source cells x[{6},{7}] y[{8},{9}]",
                        id, rotation * 90, rx, rx2, ry, ry2, expected[0], expected[1], expected[2], expected[3]));
            }
            part.SetRotation(0);
            part.SetFlipped(false);

            rows.Add(Quote(id) + "," + Quote(data.display_name ?? string.Empty) + "," + Quote(data.category) + "," +
                Quote(string.Format("{0}|{1}", data.grid.phase[0], data.grid.phase[1])) + "," +
                Quote(string.Format("{0}|{1}", data.grid.shift[0], data.grid.shift[1])) + "," +
                Quote(string.Format("x[{0},{1}] y[{2},{3}]", cells.xmin, cells.xmax, cells.ymin, cells.ymax)) + "," +
                Quote(string.Format("x[{0},{1}] y[{2},{3}]", xmin, xmax, ymin, ymax)) + "," +
                overhang.ToString("0.###", CultureInfo.InvariantCulture) + "," +
                gap.ToString("0.###", CultureInfo.InvariantCulture) + "," + mounts);
        }

        Directory.CreateDirectory("Builds/QA");
        File.WriteAllLines("Builds/QA/grid-alignment.csv", rows);
        File.WriteAllLines("Builds/QA/grid-alignment.txt", new[]
        {
            "Parts: " + catalog.Parts.Count,
            "Parts with a half-cell grid shift: " + aligned,
            "Mount points checked: " + mountsChecked,
            "Worst artwork overhang: " + worstOverhang.ToString("0.###"),
            "Warnings: " + warnings.Count,
            "Failures: " + failures.Count
        }.Concat(warnings.Select(w => "warning: " + w)).Concat(failures.Select(f => "fail: " + f)));
        if (failures.Count > 0)
            throw new Exception(string.Join("\n", failures));
        Debug.Log("RCR2_GRID_PASS: " + catalog.Parts.Count + " parts, " + aligned +
                  " aligned to BPLE cells, " + mountsChecked + " mounts on the lattice.");
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    /// <summary>Cell block after a grid rotation, matching Contraption's rotation of the part root.</summary>
    private static int[] RotateCells(int xmin, int xmax, int ymin, int ymax, int rotation)
    {
        switch (rotation & 3)
        {
            case 1:
                return new[] { -ymax, -ymin, xmin, xmax };
            case 2:
                return new[] { -xmax, -xmin, -ymax, -ymin };
            case 3:
                return new[] { ymin, ymax, -xmax, -xmin };
            default:
                return new[] { xmin, xmax, ymin, ymax };
        }
    }

    private static bool OffLattice(float value)
    {
        return Mathf.Abs(value - Mathf.Round(value)) > LatticeTolerance &&
               Mathf.Abs(Mathf.Abs(value - Mathf.Round(value)) - 0.5f) > LatticeTolerance;
    }

    /// <summary>Same rule the runtime footprint uses: a node counts only when it and its parents are active.</summary>
    private static bool IsLocallyActive(Transform node, Transform root)
    {
        for (Transform current = node; current != null && current != root; current = current.parent)
        {
            if (!current.gameObject.activeSelf)
                return false;
        }
        return true;
    }

    /// <summary>Exact local-space bounds of the enabled construction artwork (mesh vertices, not mesh AABBs).</summary>
    private static bool TryArtworkBounds(GameObject part, out Bounds bounds)
    {
        bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool any = false;
        Transform root = part.transform;
        foreach (MeshRenderer renderer in part.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (renderer == null || !renderer.enabled)
                continue;
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null || !IsLocallyActive(renderer.transform, root))
                continue;
            foreach (Vector3 vertex in filter.sharedMesh.vertices)
            {
                Vector3 local = root.InverseTransformPoint(renderer.transform.TransformPoint(vertex));
                if (!any)
                {
                    bounds = new Bounds(local, Vector3.zero);
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(local);
                }
            }
        }
        return any;
    }
}
