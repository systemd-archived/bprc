using System;
using System.Collections.Generic;
using System.IO;
using RCR2Port;
using UnityEngine;

public static class RoverPortVisualAudit
{
    public static void Run()
    {
        string output = Path.GetFullPath("Builds/QA/Visual");
        Directory.CreateDirectory(output);
        var cameraObject = new GameObject("RCR2VisualAuditCamera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 1.2f;
        camera.transform.position = new Vector3(0f, 0f, -10f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color32(36, 42, 52, 255);
        camera.cullingMask = 1 << 30;
        camera.allowHDR = false;
        camera.allowMSAA = false;
        var target = new RenderTexture(192, 192, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture = target;
        var image = new Texture2D(192, 192, TextureFormat.RGB24, false);
        var rows = new List<string> { "id,shader,scale,width,height,centerX,centerY" };
        RenderTexture previous = RenderTexture.active;
        try
        {
            foreach (GameObject prototype in RoverPortCatalog.Instance.Parts)
            {
                var part = prototype.GetComponent<RoverPortPart>();
                var icon = UnityEngine.Object.Instantiate(part.m_constructionIconSprite.gameObject);
                try
                {
                    icon.transform.SetParent(null, false);
                    icon.transform.position = Vector3.zero;
                    icon.transform.rotation = Quaternion.identity;
                    icon.layer = 30;
                    icon.SetActive(true);
                    var renderer = icon.GetComponent<MeshRenderer>();
                    foreach (float scale in new[] { 1.75f, 1.6f })
                    {
                        icon.transform.localScale = new Vector3(scale, scale, 1f);
                        camera.Render();
                        RenderTexture.active = target;
                        image.ReadPixels(new Rect(0, 0, 192, 192), 0, 0);
                        image.Apply();
                        string suffix = scale == 1.75f ? "toolbar" : "expanded";
                        File.WriteAllBytes(Path.Combine(output, part.PortId + "_" + suffix + ".png"), image.EncodeToPNG());
                        Bounds bounds = renderer.bounds;
                        rows.Add(string.Join(",", part.PortId, renderer.sharedMaterial.shader.name, scale,
                            bounds.size.x, bounds.size.y, bounds.center.x, bounds.center.y));
                        if (Mathf.Max(bounds.size.x, bounds.size.y) > 2.4f || bounds.center.sqrMagnitude > 0.00001f)
                            throw new Exception(part.PortId + ": icon outside centered button bounds");
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(icon); }
            }
            File.WriteAllLines(Path.Combine(output, "render-audit.csv"), rows);
            Debug.Log("RCR2_VISUAL_RENDER_PASS: 150 icons at toolbar and expanded-list scales");
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(image);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }
}
