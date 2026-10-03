using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RCR2Port;
using UnityEditor;
using UnityEngine;

public static class RoverPortAudit
{
    [MenuItem("Tools/RCR2/Audit all parts")]
    public static void Run()
    {
        var failures = new List<string>();
        var rows = new List<string> { "id,index,type,iconWidth,iconHeight,constructionRenderers,physicsRenderers" };
        var catalog = RoverPortCatalog.Instance;
        var ids = new HashSet<string>();
        var indices = new HashSet<int>();
        int cargo = 0;
        foreach (var go in catalog.Parts)
        {
            var source = go.GetComponent<RoverPortPart>();
            GameObject clone = null;
            GameObject iconClone = null;
            try
            {
                Require(ids.Add(source.PortId), "duplicate ID");
                Require(indices.Add(source.Index), "duplicate index");
                Require(catalog.GetPart(source.Index) == go, "index lookup");
                Require(source.Type.ToSortedPartType().ToPartType() == source.Type, "type conversion");
                if (source.Type == BasePart.PartType.RCR2Cargo) cargo++;
                var icon = source.m_constructionIconSprite;
                Require(icon != null, "missing icon");
                var bounds = icon.SpriteMesh.bounds;
                Require(bounds.center.sqrMagnitude < 0.00001f, "icon not centered");
                Require(Mathf.Max(bounds.size.x, bounds.size.y) <= RoverPortIconSprite.MaximumSize + 0.001f, "oversized icon");
                Require(Mathf.Min(bounds.size.x, bounds.size.y) > 0f, "empty icon");
                iconClone = UnityEngine.Object.Instantiate(icon.gameObject);
                var clonedIcon = iconClone.GetComponent<Sprite>();
                clonedIcon.GetPreviewImage(out float aspect, out Rect uv);
                Require(!float.IsNaN(aspect) && aspect > 0f && uv.width > 0, "clone preview metadata");
                clone = UnityEngine.Object.Instantiate(go);
                var part = clone.GetComponent<RoverPortPart>();
                part.Awake();
                Require(part.PortId == source.PortId && part.Index == source.Index, "clone identity");
                int construction = ActiveRenderers(clone);
                Require(construction > 0, "no construction visual");
                foreach (var mesh in clone.GetComponentsInChildren<MeshFilter>(true))
                {
                    Require(mesh.sharedMesh != null, "missing mesh");
                    Require(mesh.sharedMesh.vertexCount == mesh.sharedMesh.uv.Length, "UV count");
                    Require(mesh.GetComponent<Renderer>().sharedMaterial.mainTexture != null, "missing texture");
                }
                part.EnsureRigidbody();
                int physics = ActiveRenderers(clone);
                Require(physics > 0, "no physics visual");
                foreach (var joint in clone.GetComponentsInChildren<Joint>(true))
                    Require(joint.connectedBody != null, "joint attached to world instead of body");
                foreach (var body in clone.GetComponentsInChildren<Rigidbody>(true))
                    Require((body.constraints & RigidbodyConstraints.FreezePositionZ) != 0, "body can leave 2D plane");
                rows.Add($"{part.PortId},{part.Index},{part.Type},{bounds.size.x},{bounds.size.y},{construction},{physics}");
            }
            catch (Exception e) { failures.Add(source.PortId + ": " + e.Message); }
            finally
            {
                if (clone != null) UnityEngine.Object.DestroyImmediate(clone);
                if (iconClone != null) UnityEngine.Object.DestroyImmediate(iconClone);
            }
        }
        if (catalog.Parts.Count != 150) failures.Add("Expected 150 parts, got " + catalog.Parts.Count);
        if (cargo != 95) failures.Add("Expected 95 cargo, got " + cargo);
        Directory.CreateDirectory("Builds/QA");
        File.WriteAllLines("Builds/QA/parts-audit.csv", rows);
        File.WriteAllLines("Builds/QA/parts-audit-result.txt", new[] { "Parts: " + catalog.Parts.Count, "Cargo: " + cargo, "Failures: " + failures.Count }.Concat(failures));
        if (failures.Count > 0) throw new Exception(string.Join("\n", failures));
        Debug.Log("RCR2_AUDIT_PASS: 150 parts, 95 cargo; meshes, icons, clones and runtime joints checked.");
    }

    private static int ActiveRenderers(GameObject root)
    {
        return root.GetComponentsInChildren<Renderer>(true).Count(r => {
            if (!r.enabled) return false;
            for (Transform t = r.transform; t != root.transform; t = t.parent)
                if (!t.gameObject.activeSelf) return false;
            return true;
        });
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
