using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using RCR2Port;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Renders selected ported parts over a BPLE build grid: the anchor cell is drawn in one colour,
/// the neighbouring cells in another, so the artwork's position on the grid can be checked by eye.
/// Each part is written twice: "<id>_after.png" with the recovered grid alignment applied and
/// "<id>_before.png" with the alignment undone (how the part looked before the fix).
/// Run with: Unity -executeMethod RoverPortGridPreview.Run
/// </summary>
public static class RoverPortGridPreview
{
    private static readonly string[] Parts =
    {
        "part_acid_wheel_trio", "part_ice_wheel_bigwheel", "part_acid_wheel_multi",
        "part_terra_wheel_basic", "part_sand_wheel_soft", "part_acid_wheel_double",
        "part_ice_wheel_ski", "part_acid_cockpit", "part_acid_gyroplatform"
    };

    private const int Layer = 30;

    public static void Run()
    {
        // A part restyled by the extra catalog has to be rendered with its override applied,
        // otherwise the preview shows a look the game never displays (the stabilizer's 1.1.0 art).
        RoverPortExtraCatalog.Load();
        string output = Path.GetFullPath("Builds/QA/GridPreview");
        Directory.CreateDirectory(output);
        var cameraObject = new GameObject("RCR2GridPreviewCamera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 2.4f;
        camera.transform.position = new Vector3(0f, 0f, -10f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color32(30, 34, 44, 255);
        camera.cullingMask = 1 << Layer;
        camera.allowHDR = false;
        camera.allowMSAA = false;
        var target = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture = target;
        var image = new Texture2D(256, 256, TextureFormat.RGB24, false);
        var lines = BuildGridLines();
        RenderTexture previous = RenderTexture.active;
        try
        {
            foreach (string id in Parts)
            {
                GameObject prototype = RoverPortCatalog.Instance.GetPart(id);
                if (prototype == null)
                {
                    Debug.LogWarning("[RCR2Port] grid preview: unknown part " + id);
                    continue;
                }
                lines.transform.SetParent(null, false);
                lines.SetActive(true);
                var clone = UnityEngine.Object.Instantiate(prototype);
                try
                {
                    clone.SetActive(true);
                    clone.transform.SetParent(null, false);
                    clone.transform.position = Vector3.zero;
                    foreach (Transform child in clone.GetComponentsInChildren<Transform>(true))
                        child.gameObject.layer = Layer;
                    var data = typeof(RoverPortPart).GetField("m_portData",
                        BindingFlags.Instance | BindingFlags.NonPublic).GetValue(clone.GetComponent<RoverPortPart>()) as RoverPortPartData;
                    Vector3 gridShift = data != null && data.grid != null && data.grid.shift != null
                        ? new Vector3(data.grid.shift[0], data.grid.shift[1], 0f)
                        : Vector3.zero;
                    Save(camera, target, image, Path.Combine(output, id + "_after.png"));
                    // Undo the alignment to reproduce the previous placement.
                    for (int i = 0; i < clone.transform.childCount; i++)
                        clone.transform.GetChild(i).localPosition -= gridShift;
                    Save(camera, target, image, Path.Combine(output, id + "_before.png"));
                    // "Run" look: the physics renderer set, which StartContraption switches to.
                    foreach (var renderer in ConstructionRenderers(clone))
                        renderer.enabled = false;
                    foreach (var renderer in PhysicsRenderers(clone))
                        renderer.enabled = true;
                    Save(camera, target, image, Path.Combine(output, id + "_run.png"));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(clone);
                    lines.SetActive(false);
                }
            }
            Debug.Log("RCR2_GRID_PREVIEW: " + Parts.Length + " parts rendered with build grid overlay");
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(image);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(lines);
        }
    }

    private static System.Collections.Generic.List<Renderer> ConstructionRenderers(GameObject part)
    {
        return (System.Collections.Generic.List<Renderer>)typeof(RCR2Port.RoverPortPart)
            .GetField("m_constructionRenderers", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(part.GetComponent<RCR2Port.RoverPortPart>());
    }

    private static System.Collections.Generic.List<Renderer> PhysicsRenderers(GameObject part)
    {
        return (System.Collections.Generic.List<Renderer>)typeof(RCR2Port.RoverPortPart)
            .GetField("m_physicsRenderers", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(part.GetComponent<RCR2Port.RoverPortPart>());
    }

    private static void Save(Camera camera, RenderTexture target, Texture2D image, string path)
    {
        camera.Render();
        RenderTexture.active = target;
        image.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
        image.Apply();
        File.WriteAllBytes(path, image.EncodeToPNG());
    }

    /// <summary>Quads marking the BPLE cell borders (half integers) and the part's anchor cell.</summary>
    private static GameObject BuildGridLines()
    {
        var root = new GameObject("RCR2GridLines");
        var lineMaterial = new Material(Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default"));
        lineMaterial.hideFlags = HideFlags.DontSave;
        var anchorMaterial = new Material(lineMaterial);
        for (int offset = -3; offset <= 3; offset++)
        {
            float edge = offset + 0.5f;
            if (offset == 0)
                continue;
            AddQuad(root, lineMaterial, new Vector4(edge - 0.015f, -3f, 0.03f, 6f), new Color32(120, 132, 150, 255));
            AddQuad(root, lineMaterial, new Vector4(-3f, edge - 0.015f, 6f, 0.03f), new Color32(120, 132, 150, 255));
        }
        AddQuad(root, anchorMaterial, new Vector4(-0.02f, -3f, 0.04f, 6f), new Color32(232, 182, 80, 255));
        AddQuad(root, anchorMaterial, new Vector4(-3f, -0.02f, 6f, 0.04f), new Color32(232, 182, 80, 255));
        root.SetActive(false);
        return root;
    }

    private static void AddQuad(GameObject parent, Material template, Vector4 rect, Color color)
    {
        var quad = new GameObject("line");
        quad.layer = Layer;
        quad.transform.SetParent(parent.transform, false);
        quad.transform.position = new Vector3(0f, 0f, 0.5f);
        var filter = quad.AddComponent<MeshFilter>();
        filter.sharedMesh = QuadMesh(rect.x, rect.y, rect.z, rect.w);
        var renderer = quad.AddComponent<MeshRenderer>();
        var material = new Material(template);
        material.hideFlags = HideFlags.DontSave;
        material.color = color;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static Mesh QuadMesh(float x, float y, float width, float height)
    {
        var mesh = new Mesh { hideFlags = HideFlags.DontSave };
        mesh.vertices = new[]
        {
            new Vector3(x, y, 0f), new Vector3(x, y + height, 0f),
            new Vector3(x + width, y + height, 0f), new Vector3(x + width, y, 0f)
        };
        mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
        mesh.triangles = new[] { 0, 1, 2, 2, 3, 0 };
        mesh.RecalculateBounds();
        return mesh;
    }
}
