using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using RCR2Port;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Drives the turbine rig of the ported rotating turbines through its timeline in edit mode and
/// checks the two behaviours the part had in RCR2:
///
///  * the nozzle enable animation - while the engine thrusts the SonicFX nozzle is switched on with an
///    impulse and keeps pulsing, and it is switched off again when the thrust stops;
///  * the rotation lock - the rotor only ever turns one way, and once the thrust stops it is driven
///    back to the rotation the part was built with instead of coasting on.
///
/// Also renders an idle and a powered frame for part_ice_gyroturbine so both states can be checked by
/// eye. Reports: Builds/QA/turbine-audit.txt, Builds/QA/Turbine/*.png
/// Run with: Unity -executeMethod RoverPortTurbineAudit.Run
/// </summary>
public static class RoverPortTurbineAudit
{
    private const int Layer = 30;
    private const int Size = 256;
    private const float Step = 1f / 60f;

    public static void Run()
    {
        string output = Path.GetFullPath("Builds/QA");
        Directory.CreateDirectory(output);
        string previewDirectory = Path.Combine(output, "Turbine");
        Directory.CreateDirectory(previewDirectory);

        var cameraObject = new GameObject("RCR2TurbineAuditCamera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color32(30, 34, 44, 255);
        camera.cullingMask = 1 << Layer;
        camera.allowHDR = false;
        camera.allowMSAA = false;
        var target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture = target;
        var image = new Texture2D(Size, Size, TextureFormat.ARGB32, false);
        RenderTexture previous = RenderTexture.active;

        var lines = new List<string> { "turbine audit" };
        int failures = 0;
        try
        {
            foreach (GameObject prototype in RoverPortCatalog.Instance.Parts)
            {
                var turbine = prototype.GetComponent<RoverPortTurbine>();
                if (turbine == null)
                {
                    continue;
                }
                var part = prototype.GetComponent<RoverPortPart>();
                string id = part != null ? part.PortId : prototype.name;
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
                    var instance = clone.GetComponent<RoverPortTurbine>();

                    // idle
                    for (int i = 0; i < 10; i++)
                    {
                        instance.Tick(Step);
                    }
                    float idleAngle = Mathf.Abs(instance.RotorAngle);
                    bool idleNozzle = instance.NozzleVisible;

                    // powered
                    instance.PowerOverrideActive = true;
                    instance.PowerOverride = true;
                    float afterImpulse = 0f;
                    float angleBefore = instance.RotorAngle;
                    for (int i = 0; i < 60; i++)
                    {
                        instance.Tick(Step);
                        if (i == 5)
                        {
                            afterImpulse = instance.RotorAngle;
                        }
                    }
                    float poweredAngle = instance.RotorAngle;
                    bool poweredNozzle = instance.NozzleVisible;

                    // thrust stops: the nozzle shuts down and the rotor unwinds to its rest rotation
                    instance.PowerOverride = false;
                    for (int i = 0; i < 120; i++)
                    {
                        instance.Tick(Step);
                    }
                    float settledAngle = Mathf.Abs(instance.RotorAngle);
                    bool settledNozzle = instance.NozzleVisible;

                    clone.transform.rotation = Quaternion.Euler(0, 0, 90);
                    instance.Tick(Step);
                    Vector3 expected = id == "part_ice_gyroturbine" ? Vector3.right : Vector3.up;
                    bool directionOk = Vector3.Dot(instance.ThrustDirection, expected) > 0.999f;
                    clone.transform.rotation = Quaternion.identity;
                    bool ok = idleAngle < 0.001f && !idleNozzle
                        && poweredAngle == 0f && poweredNozzle && directionOk
                        && settledAngle < 0.001f && !settledNozzle
                        && clone.GetComponent<RoverPortPoweredPart>().m_powerConsumption == 10000f;
                    lines.Add(id + ": world direction after 90 degree body rotation=" + instance.ThrustDirection + " directionCheck=" + directionOk);
                    if (!ok)
                    {
                        failures++;
                    }
                    lines.Add(string.Format(CultureInfo.InvariantCulture,
                        "{0}: idle(angle={1:0.00} nozzle={2}) impulse(angle@5f={3:0.00}) powered(angle={4:0.00} nozzle={5}) settled(angle={6:0.00} nozzle={7}) => {8}",
                        id, idleAngle, idleNozzle, afterImpulse, poweredAngle, poweredNozzle, settledAngle, settledNozzle, ok ? "PASS" : "FAIL"));

                    if (id == "part_ice_gyroturbine")
                    {
                        Frame(camera, clone);
                        SetRendered(clone, true);
                        Save(camera, target, image, Path.Combine(previewDirectory, id + "_powered.png"));
                        // idle frame: reset the rig and render again
                        var idleClone = UnityEngine.Object.Instantiate(prototype);
                        try
                        {
                            idleClone.transform.SetParent(null, false);
                            idleClone.transform.position = Vector3.zero;
                            idleClone.transform.rotation = Quaternion.identity;
                            idleClone.SetActive(true);
                            foreach (Transform child in idleClone.GetComponentsInChildren<Transform>(true))
                            {
                                child.gameObject.layer = Layer;
                            }
                            var idleRig = idleClone.GetComponent<RoverPortTurbine>();
                            idleRig.Tick(Step);
                            Frame(camera, idleClone);
                            SetRendered(idleClone, true);
                            Save(camera, target, image, Path.Combine(previewDirectory, id + "_idle.png"));
                        }
                        finally
                        {
                            UnityEngine.Object.DestroyImmediate(idleClone);
                        }
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(clone);
                }
            }
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

        lines.Add("failures: " + failures.ToString(CultureInfo.InvariantCulture));
        File.WriteAllLines(Path.Combine(output, "turbine-audit.txt"), lines);
        Debug.Log("RCR2_TURBINE_AUDIT: failures=" + failures);
        if (failures > 0) throw new Exception("Turbine audit failed");
    }

    /// <summary>Run view: the physics renderers, which is where the nozzle sprite lives.</summary>
    private static void SetRendered(GameObject part, bool runView)
    {
        var component = part.GetComponent<RoverPortPart>();
        List<Renderer> construction = GetRenderers(component, "m_constructionRenderers");
        List<Renderer> physics = GetRenderers(component, "m_physicsRenderers");
        foreach (Renderer renderer in construction)
        {
            renderer.enabled = !runView;
        }
        foreach (Renderer renderer in physics)
        {
            renderer.enabled = runView;
        }
    }

    private static List<Renderer> GetRenderers(RoverPortPart part, string field)
    {
        return (List<Renderer>)typeof(RoverPortPart)
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(part);
    }

    private static void Frame(Camera camera, GameObject part)
    {
        var bounds = new Bounds();
        bool started = false;
        foreach (Renderer renderer in part.GetComponentsInChildren<Renderer>(true))
        {
            if (!started)
            {
                bounds = renderer.bounds;
                started = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }
        if (!started)
        {
            bounds = new Bounds(Vector3.zero, Vector3.one);
        }
        camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, bounds.center.z - 50f);
        camera.transform.rotation = Quaternion.identity;
        camera.orthographicSize = Mathf.Max(0.05f, Mathf.Max(bounds.extents.y, bounds.extents.x) * 1.1f);
    }

    private static void Save(Camera camera, RenderTexture target, Texture2D image, string path)
    {
        camera.Render();
        RenderTexture.active = target;
        image.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
        image.Apply();
        File.WriteAllBytes(path, image.EncodeToPNG());
    }
}
