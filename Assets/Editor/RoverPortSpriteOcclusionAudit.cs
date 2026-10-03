using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using RCR2Port;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Measures how much of every ported sprite survives the draw order of its renderer set.
///
/// The build view draws the construction (ME) renderer set, the run view draws the physics (PE)
/// set. When two sprites of a set overlap, the one drawn later wins, so a mount bracket whose
/// sorting order sits below the tyre disappears behind it. For every renderer the audit renders:
///   alone   - only that renderer enabled (its footprint, i.e. the pixels it owns when nothing
///             overlaps it)
///   natural - the whole set as configured (what the player sees in that view)
///   raised  - the whole set with that renderer's sorting order lifted above every other renderer
/// occludedPixels = |natural XOR raised| is exactly the area the renderer would add if it were
/// drawn last, so occludedPixels / footprint is 1 for a sprite that cannot be seen at all.
///
/// Sprites that are hidden in the construction set but visible in the physics set are the defect
/// this audit looks for: the part is fine while driving but loses detail while building.
///
/// Reports: Builds/QA/sprite-occlusion.csv and Builds/QA/sprite-occlusion.txt
/// Run with: Unity -executeMethod RoverPortSpriteOcclusionAudit.Run
/// </summary>
public static class RoverPortSpriteOcclusionAudit
{
    private const int Layer = 30;
    private const int Size = 192;
    private const int MinFootprintPixels = 24;
    private const float HiddenRatio = 0.98f;
    private const float VisibleRatio = 0.5f;

    private sealed class Entry
    {
        public string PartId;
        public string Set;
        public string Sprite;
        public int SortingOrder;
        public int Footprint;
        public int Occluded;

        public float Ratio
        {
            get { return Footprint > 0 ? Occluded / (float)Footprint : 0f; }
        }

        public bool IsMeasurable
        {
            get { return Footprint >= MinFootprintPixels; }
        }
    }

    public static void Run()
    {
        string output = Path.GetFullPath("Builds/QA");
        Directory.CreateDirectory(output);
        var entries = new List<Entry>();

        var cameraObject = new GameObject("RCR2OcclusionAuditCamera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        camera.cullingMask = 1 << Layer;
        camera.allowHDR = false;
        camera.allowMSAA = false;
        camera.enabled = false;
        var target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture = target;
        var image = new Texture2D(Size, Size, TextureFormat.ARGB32, false);
        RenderTexture previous = RenderTexture.active;

        int parts = 0;
        try
        {
            foreach (GameObject prototype in RoverPortCatalog.Instance.Parts)
            {
                var part = prototype.GetComponent<RoverPortPart>();
                if (part == null)
                {
                    continue;
                }
                var clone = UnityEngine.Object.Instantiate(prototype);
                try
                {
                    clone.transform.SetParent(null, false);
                    clone.transform.position = Vector3.zero;
                    clone.transform.rotation = Quaternion.identity;
                    clone.SetActive(true);
                    foreach (Transform child in clone.GetComponentsInChildren<Transform>(true))
                    {
                        child.gameObject.layer = Layer;
                    }
                    // The renderer lists must come from the clone: they reference the clone's own
                    // children, and the prototype has to stay untouched for the next part.
                    var instance = clone.GetComponent<RoverPortPart>();
                    List<Renderer> construction = GetRenderers(instance, "m_constructionRenderers");
                    List<Renderer> physics = GetRenderers(instance, "m_physicsRenderers");
                    if (construction.Count == 0 && physics.Count == 0)
                    {
                        continue;
                    }
                    Frame(camera, construction, physics);
                    Measure(part.PortId, "construction", construction, physics, camera, target, image, entries);
                    Measure(part.PortId, "physics", physics, construction, camera, target, image, entries);
                    parts++;
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(clone);
                }
            }
        }
        finally
        {
            // Reports and previews need the camera, so they run before the cleanup below.
            Write(output, entries, parts);
            RenderPreviews(output, entries, camera, target, image);
            DumpIsolatedSprites(output, camera, target, image);
            RenderTexture.active = previous;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(image);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }

    /// <summary>Saves a build-view and a run-view image for every part with a hidden sprite.</summary>
    private static void RenderPreviews(
        string output, List<Entry> entries, Camera camera, RenderTexture target, Texture2D image)
    {
        var flagged = new HashSet<string>();
        foreach (Entry entry in entries)
        {
            if (entry.Set == "construction" && entry.IsMeasurable && entry.Ratio >= HiddenRatio)
            {
                flagged.Add(entry.PartId);
            }
        }
        foreach (GameObject prototype in RoverPortCatalog.Instance.Parts)
        {
            var wheelPart = prototype.GetComponent<RoverPortPart>();
            if (wheelPart == null)
            {
                continue;
            }
            RoverPortPartData wheelData = GetData(wheelPart);
            if (wheelData != null && wheelData.category == "wheel")
            {
                flagged.Add(wheelPart.PortId);
            }
        }
        if (flagged.Count == 0)
        {
            return;
        }
        string directory = Path.Combine(output, "BracketPreview");
        Directory.CreateDirectory(directory);
        camera.backgroundColor = new Color32(30, 34, 44, 255);
        foreach (GameObject prototype in RoverPortCatalog.Instance.Parts)
        {
            var part = prototype.GetComponent<RoverPortPart>();
            if (part == null || !flagged.Contains(part.PortId))
            {
                continue;
            }
            List<Renderer> construction;
            List<Renderer> physics;
            var clone = UnityEngine.Object.Instantiate(prototype);
            try
            {
                clone.transform.SetParent(null, false);
                clone.transform.position = Vector3.zero;
                clone.transform.rotation = Quaternion.identity;
                clone.SetActive(true);
                foreach (Transform child in clone.GetComponentsInChildren<Transform>(true))
                {
                    child.gameObject.layer = Layer;
                }
                var instance = clone.GetComponent<RoverPortPart>();
                construction = GetRenderers(instance, "m_constructionRenderers");
                physics = GetRenderers(instance, "m_physicsRenderers");
                Frame(camera, construction, physics);
                foreach (Renderer renderer in physics)
                {
                    renderer.enabled = false;
                }
                foreach (Renderer renderer in construction)
                {
                    renderer.enabled = true;
                }
                Save(camera, target, image, Path.Combine(directory, part.PortId + "_build.png"));
                foreach (Renderer renderer in construction)
                {
                    renderer.enabled = false;
                }
                foreach (Renderer renderer in physics)
                {
                    renderer.enabled = true;
                }
                Save(camera, target, image, Path.Combine(directory, part.PortId + "_run.png"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(clone);
            }
        }
        Debug.Log("RCR2_OCCLUSION_AUDIT: previews for " + flagged.Count + " parts in " + directory);
    }

    /// <summary>
    /// Writes every construction sprite of every wheel part on its own, so the role of each sprite
    /// (tyre ring, mount bracket, wheel face / hub) can be checked by eye when ordering them.
    /// </summary>
    private static void DumpIsolatedSprites(
        string output, Camera camera, RenderTexture target, Texture2D image)
    {
        string directory = Path.Combine(output, "SpriteParts");
        Directory.CreateDirectory(directory);
        camera.backgroundColor = new Color32(30, 34, 44, 255);
        foreach (GameObject prototype in RoverPortCatalog.Instance.Parts)
        {
            var part = prototype.GetComponent<RoverPortPart>();
            if (part == null)
            {
                continue;
            }
            RoverPortPartData data = GetData(part);
            if (data == null || data.category != "wheel")
            {
                continue;
            }
            var clone = UnityEngine.Object.Instantiate(prototype);
            try
            {
                clone.transform.SetParent(null, false);
                clone.transform.position = Vector3.zero;
                clone.transform.rotation = Quaternion.identity;
                clone.SetActive(true);
                foreach (Transform child in clone.GetComponentsInChildren<Transform>(true))
                {
                    child.gameObject.layer = Layer;
                }
                var instance = clone.GetComponent<RoverPortPart>();
                List<Renderer> construction = GetRenderers(instance, "m_constructionRenderers");
                List<Renderer> physics = GetRenderers(instance, "m_physicsRenderers");
                Frame(camera, construction, physics);
                for (int index = 0; index < construction.Count; index++)
                {
                    for (int other = 0; other < construction.Count; other++)
                    {
                        construction[other].enabled = other == index;
                    }
                    string name = string.Format(CultureInfo.InvariantCulture, "{0}_{1:00}_{2}.png",
                        part.PortId, index, SpriteName(construction[index]));
                    Save(camera, target, image, Path.Combine(directory, name));
                }
                // Does the wheel sprite itself carry the visible wheel face? If its centre is opaque
                // the wheel art covers the hub, so the mount bracket has to stay behind it.
                int wheelIndex = LargestSprite(construction);
                if (wheelIndex >= 0)
                {
                    for (int other = 0; other < construction.Count; other++)
                    {
                        construction[other].enabled = other == wheelIndex;
                    }
                    camera.Render();
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                    image.Apply();
                    Color32[] pixels = image.GetPixels32();
                    Vector3 screen = camera.WorldToScreenPoint(construction[wheelIndex].bounds.center);
                    int px = Mathf.Clamp(Mathf.RoundToInt(screen.x), 0, Size - 1);
                    int py = Mathf.Clamp(Mathf.RoundToInt(screen.y), 0, Size - 1);
                    byte alpha = pixels[py * Size + px].a;
                    Debug.Log(string.Format(CultureInfo.InvariantCulture,
                        "RCR2_WHEEL_FACE: {0} wheel={1} centreAlpha={2} carriesFace={3}",
                        part.PortId, SpriteName(construction[wheelIndex]), alpha, alpha > 32 ? "yes" : "no"));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(clone);
            }
        }
        Debug.Log("RCR2_OCCLUSION_AUDIT: isolated wheel sprites in " + directory);
    }

    private static int LargestSprite(List<Renderer> renderers)
    {
        int best = -1;
        float bestArea = 0f;
        for (int i = 0; i < renderers.Count; i++)
        {
            var filter = renderers[i].GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
            {
                continue;
            }
            Bounds bounds = filter.sharedMesh.bounds;
            float area = bounds.size.x * bounds.size.y;
            if (area > bestArea)
            {
                bestArea = area;
                best = i;
            }
        }
        return best;
    }

    private static void Save(Camera camera, RenderTexture target, Texture2D image, string path)
    {
        camera.Render();
        RenderTexture.active = target;
        image.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
        image.Apply();
        File.WriteAllBytes(path, image.EncodeToPNG());
    }

    /// <summary>Enables one set, disables the other, then measures every renderer of the set.</summary>
    private static void Measure(
        string partId, string setName, List<Renderer> active, List<Renderer> inactive,
        Camera camera, RenderTexture target, Texture2D image, List<Entry> entries)
    {
        if (active.Count == 0)
        {
            return;
        }
        foreach (Renderer renderer in inactive)
        {
            renderer.enabled = false;
        }
        foreach (Renderer renderer in active)
        {
            renderer.enabled = true;
        }

        int highestOrder = int.MinValue;
        foreach (Renderer renderer in active)
        {
            highestOrder = Mathf.Max(highestOrder, renderer.sortingOrder);
        }

        Color32[] natural = Capture(camera, target, image);
        int naturalPixels = Coverage(natural);

        foreach (Renderer renderer in active)
        {
            foreach (Renderer other in active)
            {
                other.enabled = other == renderer;
            }
            Color32[] alone = Capture(camera, target, image);

            foreach (Renderer other in active)
            {
                other.enabled = true;
            }
            int order = renderer.sortingOrder;
            renderer.sortingOrder = highestOrder + 1;
            Color32[] raised = Capture(camera, target, image);
            renderer.sortingOrder = order;

            entries.Add(new Entry
            {
                PartId = partId,
                Set = setName,
                Sprite = SpriteName(renderer),
                SortingOrder = order,
                Footprint = Coverage(alone),
                Occluded = Changed(natural, raised)
            });
        }

        if (naturalPixels == 0)
        {
            Debug.LogWarning("RCR2_OCCLUSION_AUDIT: " + partId + " rendered no pixels in set " + setName);
        }
    }

    private static void Frame(Camera camera, List<Renderer> construction, List<Renderer> physics)
    {
        var bounds = new Bounds();
        bool started = false;
        foreach (List<Renderer> set in new[] { construction, physics })
        {
            foreach (Renderer renderer in set)
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                {
                    continue;
                }
                Bounds local = filter.sharedMesh.bounds;
                Vector3 center = renderer.transform.TransformPoint(local.center);
                Vector3 extents = renderer.transform.TransformVector(local.extents);
                extents = new Vector3(Mathf.Abs(extents.x), Mathf.Abs(extents.y), Mathf.Abs(extents.z));
                var world = new Bounds(center, extents * 2f);
                if (!started)
                {
                    bounds = world;
                    started = true;
                }
                else
                {
                    bounds.Encapsulate(world);
                }
            }
        }
        if (!started)
        {
            bounds = new Bounds(Vector3.zero, Vector3.one);
        }
        camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, bounds.center.z - 50f);
        camera.transform.rotation = Quaternion.identity;
        camera.orthographicSize = Mathf.Max(0.05f, Mathf.Max(bounds.extents.y, bounds.extents.x) * 1.08f);
    }

    private static Color32[] Capture(Camera camera, RenderTexture target, Texture2D image)
    {
        camera.Render();
        RenderTexture.active = target;
        image.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
        image.Apply();
        return image.GetPixels32();
    }

    private static int Coverage(Color32[] pixels)
    {
        int count = 0;
        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i].a > 32)
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Pixels whose colour changes when the renderer is drawn last: exactly the surface it loses to
    /// the sprites above it. Raising a sprite never removes it, so the union of drawn pixels stays
    /// the same and only the winning colour can differ.
    /// </summary>
    private static int Changed(Color32[] before, Color32[] after)
    {
        int count = 0;
        for (int i = 0; i < before.Length && i < after.Length; i++)
        {
            if (before[i].a <= 32 && after[i].a <= 32)
            {
                continue;
            }
            if (before[i].r != after[i].r || before[i].g != after[i].g || before[i].b != after[i].b)
            {
                count++;
            }
        }
        return count;
    }

    private static string SpriteName(Renderer renderer)
    {
        var filter = renderer.GetComponent<MeshFilter>();
        string name = filter != null && filter.sharedMesh != null ? filter.sharedMesh.name : renderer.name;
        const string prefix = "RCR2_Mesh_";
        return name.StartsWith(prefix, StringComparison.Ordinal) ? name.Substring(prefix.Length) : name;
    }

    private static List<Renderer> GetRenderers(RoverPortPart part, string field)
    {
        return (List<Renderer>)typeof(RoverPortPart)
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(part);
    }

    private static RoverPortPartData GetData(RoverPortPart part)
    {
        return (RoverPortPartData)typeof(RoverPortPart)
            .GetField("m_portData", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(part);
    }

    private static void Write(string output, List<Entry> entries, int parts)
    {
        var rows = new List<string> { "part_id,set,sprite,sorting_order,footprint_px,occluded_px,occluded_ratio" };
        foreach (Entry entry in entries)
        {
            rows.Add(string.Join(",", entry.PartId, entry.Set, entry.Sprite,
                entry.SortingOrder.ToString(CultureInfo.InvariantCulture),
                entry.Footprint.ToString(CultureInfo.InvariantCulture),
                entry.Occluded.ToString(CultureInfo.InvariantCulture),
                entry.Ratio.ToString("0.0000", CultureInfo.InvariantCulture)));
        }
        File.WriteAllLines(Path.Combine(output, "sprite-occlusion.csv"), rows);

        // Cross reference: a sprite that the run view shows but the build view hides is a defect.
        var physics = new Dictionary<string, Entry>();
        foreach (Entry entry in entries)
        {
            if (entry.Set == "physics")
            {
                physics[entry.PartId + "|" + entry.Sprite] = entry;
            }
        }

        var hidden = new List<Entry>();
        var bothHidden = new List<Entry>();
        var affected = new SortedDictionary<string, int>();
        foreach (Entry entry in entries)
        {
            if (entry.Set != "construction" || !entry.IsMeasurable || entry.Ratio < HiddenRatio)
            {
                continue;
            }
            hidden.Add(entry);
            affected[entry.PartId] = affected.TryGetValue(entry.PartId, out int count) ? count + 1 : 1;
            if (physics.TryGetValue(entry.PartId + "|" + entry.Sprite, out Entry run)
                && run.IsMeasurable && run.Ratio >= VisibleRatio)
            {
                bothHidden.Add(entry);
            }
        }

        var lines = new List<string>
        {
            "精灵遮挡审计（构建视图构造集 vs 运行视图物理集）",
            "part_id,set,sprite,sorting_order,footprint_px,occluded_px,occluded_ratio",
            string.Format(CultureInfo.InvariantCulture, "parts={0} sprite_measurements={1}", parts, entries.Count),
            string.Format(CultureInfo.InvariantCulture, "construction sprites hidden (ratio >= {0:0.00}, footprint >= {1}px): {2}",
                HiddenRatio, MinFootprintPixels, hidden.Count),
            string.Format(CultureInfo.InvariantCulture, "of those, visible while running (ratio < {0:0.00}): {1}", VisibleRatio, bothHidden.Count),
            string.Format(CultureInfo.InvariantCulture, "affected parts: {0}", affected.Count),
            "",
            "== 构建视图隐藏、运行视图可见（即“支架消失”）=="
        };
        lines.Add("part_id,set,sprite,sorting_order,footprint_px,occluded_px,occluded_ratio");
        foreach (Entry entry in bothHidden)
        {
            Entry run = physics[entry.PartId + "|" + entry.Sprite];
            lines.Add(string.Join(",", entry.PartId, entry.Sprite,
                entry.SortingOrder.ToString(CultureInfo.InvariantCulture),
                entry.Footprint.ToString(CultureInfo.InvariantCulture),
                entry.Occluded.ToString(CultureInfo.InvariantCulture),
                entry.Ratio.ToString("0.0000", CultureInfo.InvariantCulture),
                "run_ratio=" + run.Ratio.ToString("0.0000", CultureInfo.InvariantCulture)));
        }
        lines.Add("");
        lines.Add("== 两个视图都隐藏（可能是刻意的，先不动）==");
        foreach (Entry entry in hidden)
        {
            if (bothHidden.Contains(entry))
            {
                continue;
            }
            lines.Add(string.Join(",", entry.PartId, entry.Sprite,
                entry.Footprint.ToString(CultureInfo.InvariantCulture),
                entry.Occluded.ToString(CultureInfo.InvariantCulture),
                entry.Ratio.ToString("0.0000", CultureInfo.InvariantCulture)));
        }
        File.WriteAllLines(Path.Combine(output, "sprite-occlusion.txt"), lines);
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "RCR2_OCCLUSION_AUDIT: parts={0} measurements={1} hiddenConstruction={2} hiddenButVisibleRunning={3} affectedParts={4}",
            parts, entries.Count, hidden.Count, bothHidden.Count, affected.Count));
    }
}
