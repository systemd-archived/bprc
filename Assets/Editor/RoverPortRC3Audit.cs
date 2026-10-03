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
/// Audit for the RC3 missile launcher (part_rc3_missile, see
/// Tools/RCR2/ExtractRC3Missile.py): checks the injection into the part
/// catalog (identity, one-cell footprint, cell-edge connection points,
/// icon), then fires the launcher inside a manually simulated physics scene
/// and verifies the port's behaviour contract:
///
///  * the first trigger press fires immediately, further presses are gated
///    by the 8.2 s cooldown;
///  * the projectile flies along the visible missile nose axis;
///  * on impact the BPLE TNT explosion pushes a nearby dynamic body;
///  * the projectile is gone afterwards.
///
/// Renders built/run/firing previews to Builds/QA/RC3Missile/.
/// Report: Builds/QA/rc3-missile-audit.txt
/// Run with: Unity -batchmode -quit -executeMethod RoverPortRC3Audit.Run
/// </summary>
public static class RoverPortRC3Audit
{
    private const int Layer = 30;
    private const int Size = 256;
    private const float Step = 1f / 60f;

    public static void Run()
    {
        string output = Path.GetFullPath("Builds/QA");
        Directory.CreateDirectory(output);
        string previewDirectory = Path.Combine(output, "RC3Missile");
        Directory.CreateDirectory(previewDirectory);

        var lines = new List<string> { "RC3 missile audit" };
        var failures = new List<string>();
        RoverPortMissilePart prototype = null;
        GameObject clone = null;
        GameObject dummy = null;
        GameObject wall = null;
        List<GameObject> spawned = new List<GameObject>();
        Camera camera = null;
        RenderTexture target = null;
        Texture2D image = null;
        bool previousAutoSimulation = PhysicsSimulation.Auto;

        try
        {
            RoverPortExtraCatalog.Load();
            RC3Port.RC3PortCatalog.Load();
            RoverPortCatalog catalog = RoverPortCatalog.Instance;
            GameObject go = catalog.GetPart("part_rc3_missile");
            if (go == null)
            {
                throw new Exception("part_rc3_missile was not injected into the catalog");
            }
            prototype = go.GetComponent<RoverPortMissilePart>();
            if (prototype == null)
            {
                throw new Exception("part_rc3_missile has no RoverPortMissilePart");
            }
            if (prototype.Type != BasePart.PartType.RC3Missile)
            {
                failures.Add("expected PartType.RC3Missile, got " + prototype.Type);
            }
            if (prototype.DisplayName != "导弹")
            {
                failures.Add("display name mismatch: " + prototype.DisplayName);
            }
            if (prototype.Index < RoverPortCatalog.FirstPartIndex)
            {
                failures.Add("index below FirstPartIndex: " + prototype.Index);
            }

            // ---- static geometry ----
            clone = UnityEngine.Object.Instantiate(go);
            clone.transform.position = Vector3.zero;
            clone.transform.rotation = Quaternion.identity;
            clone.SetActive(true);
            RoverPortPart part = clone.GetComponent<RoverPortPart>();
            part.Awake();

            if (part.m_gridXmin != 0 || part.m_gridXmax != 0 || part.m_gridYmin != 0 || part.m_gridYmax != 0)
            {
                failures.Add(string.Format(CultureInfo.InvariantCulture,
                    "expected 1x1 footprint at the root cell, got x[{0}..{1}] y[{2}..{3}]",
                    part.m_gridXmin, part.m_gridXmax, part.m_gridYmin, part.m_gridYmax));
            }
            // Source art extends beyond the single attachment cell (349 x 105 at 128 PPU).
            var missileData = (RoverPortPartData)typeof(RoverPortPart)
                .GetField("m_portData", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(part);
            var sourceSprite = missileData.construction_renderers.Single(r => r.node == "ME:4").sprite;
            if (sourceSprite.width != 349 || sourceSprite.height != 105)
                failures.Add("missile must retain the complete source sprite");

            List<Vector3> connectionPoints = part.GetConnectionPoints().ToList();
            if (connectionPoints.Count != 4)
            {
                failures.Add("expected 4 connection points, got " + connectionPoints.Count);
            }
            foreach (Vector3 point in connectionPoints)
            {
                Vector3 local = clone.transform.InverseTransformPoint(point);
                bool onEdge = (Mathf.Abs(Mathf.Abs(local.x) - 0.5f) < 0.02f && Mathf.Abs(local.y) < 0.02f)
                           || (Mathf.Abs(Mathf.Abs(local.y) - 0.5f) < 0.02f && Mathf.Abs(local.x) < 0.02f);
                if (!onEdge)
                {
                    failures.Add("connection point not on a cell-edge midpoint: " + local);
                }
            }

            // The build UI orders the toolbox through SortedPartType
            // (PartSelector.ReadPartOrder builds a per-type table from that
            // enum and PartSelector.SetParts indexes it directly); a part type
            // missing from the enum aborts the whole build menu with a
            // KeyNotFoundException. Every catalog part type must have a
            // SortedPartType entry.
            foreach (GameObject catalogPart in RoverPortCatalog.Instance.Parts)
            {
                BasePart catalogComponent = catalogPart.GetComponent<BasePart>();
                if (catalogComponent != null
                    && !Enum.TryParse(typeof(SortedPartType), catalogComponent.Type.ToString(), out _))
                {
                    failures.Add("part type missing from SortedPartType: " + catalogComponent.Type
                        + " (" + catalogPart.name + ")");
                }
            }

            RoverPortIconSprite icon = part.m_constructionIconSprite as RoverPortIconSprite;
            if (icon == null)
            {
                failures.Add("missing icon");
            }
            else
            {
                Bounds iconBounds = icon.SpriteMesh.bounds;
                if (iconBounds.center.sqrMagnitude >= 0.0001f)
                {
                    failures.Add("icon not centered");
                }
                if (Mathf.Max(iconBounds.size.x, iconBounds.size.y) > RoverPortIconSprite.MaximumSize + 0.001f)
                {
                    failures.Add("oversized icon: " + iconBounds.size);
                }
            }

            // The explosion path reads INSettings (TNT radius/force multipliers);
            // the editor has to bootstrap the JSON service and INUnity's resource
            // table, then load the settings the way the game does (same as
            // RoverPortInteractionAudit).
            typeof(Innovation.Json).GetProperty("Service").SetValue(null,
                Activator.CreateInstance(typeof(BasePart).Assembly.GetType("JsonService"), true));
            var resourcesField = typeof(INUnity).GetField("s_resources", BindingFlags.Static | BindingFlags.NonPublic);
            var resources = resourcesField.GetValue(null) as System.Collections.Generic.Dictionary<(string, string), UnityEngine.Object>;
            if (resources == null)
            {
                resources = new System.Collections.Generic.Dictionary<(string, string), UnityEngine.Object>();
                resourcesField.SetValue(null, resources);
            }
            resources[("TextAsset", "INDeclarationSettingsExp")] = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/TextAsset/INDeclarationSettingsExp.json");
            resources[("TextAsset", "INAliasSettings")] = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/TextAsset/INAliasSettings.json");
            resources[("Material", "UIPartButtonAtlas")] = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/UIPartButtonAtlas.mat");
            resources[("Material", "IngameAtlas")] = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/IngameAtlas.mat");
            try
            {
                typeof(INSettings).GetMethod("Load", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { 0 });
            }
            catch (System.Reflection.TargetInvocationException e)
            {
                throw new Exception("INSettings.Load failed: " + e.InnerException, e.InnerException);
            }

            part.EnsureRigidbody();

            // Visual contract: the construction (ME) and physics (PE) renderer
            // sets must be distinct objects - they live on separate node trees
            // exactly like the RCR2 ports. When both sets alias the same
            // renderers, ConfigureVisuals disables them all and the part turns
            // invisible in the build view.
            List<Renderer> constructionRenderers = GetPrivateRenderers(part, "m_constructionRenderers");
            List<Renderer> physicsRenderers = GetPrivateRenderers(part, "m_physicsRenderers");
            if (constructionRenderers.Count != 3 || physicsRenderers.Count != 5)
            {
                failures.Add(string.Format(CultureInfo.InvariantCulture,
                    "expected 3 construction + 5 physics renderers (frame/missile/ring + flash + projectile carriers), got {0}+{1}",
                    constructionRenderers.Count, physicsRenderers.Count));
            }
            if (constructionRenderers.Intersect(physicsRenderers).Any())
            {
                failures.Add("construction and physics renderer sets alias the same renderers");
            }
            foreach (Renderer renderer in constructionRenderers.Concat(physicsRenderers))
            {
                Texture mainTexture = renderer.sharedMaterial != null ? renderer.sharedMaterial.mainTexture : null;
                if (mainTexture == null || !mainTexture.name.StartsWith("texRC3_", StringComparison.Ordinal))
                {
                    failures.Add("renderer " + renderer.name + " does not use its RC3 texture ("
                        + (mainTexture != null ? mainTexture.name : "null") + ")");
                }
            }

            if (clone.GetComponentsInChildren<Rigidbody>(true).Length != 1)
            {
                failures.Add("expected exactly the root runtime rigidbody");
            }

            // ---- behaviour: fire, cooldown, impact explosion ----
            PhysicsSimulation.Auto = false;
            dummy = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dummy.name = "RC3AuditDummy";
            dummy.transform.position = new Vector3(0f, 5f, 0f);
            dummy.layer = LayerMask.NameToLayer("Contraption");
            Rigidbody dummyBody = dummy.AddComponent<Rigidbody>();
            dummyBody.mass = 1f;
            dummyBody.useGravity = false;
            dummyBody.constraints = (RigidbodyConstraints)56;
            foreach (Collider c in dummy.GetComponentsInChildren<Collider>())
            {
                c.gameObject.layer = LayerMask.NameToLayer("Contraption");
            }

            RoverPortMissilePart rig = clone.GetComponent<RoverPortMissilePart>();
            bool first = rig.TryFire();
            if (!first)
            {
                failures.Add("first trigger press did not fire");
            }
            GameObject projectile = GameObject.Find("RC3MissileProjectile");
            spawned.Add(projectile);
            if (projectile == null)
            {
                failures.Add("no projectile after fire");
            }
            else
            {
                Rigidbody projectileBody = projectile.GetComponent<Rigidbody>();
                if (projectileBody.velocity.y < 30f || Mathf.Abs(projectileBody.velocity.x) > 0.1f)
                {
                    failures.Add("projectile not flying along +Y: " + projectileBody.velocity);
                }
                Renderer projectileRenderer = projectile.GetComponentInChildren<Renderer>();
                Texture projectileTexture = projectileRenderer != null && projectileRenderer.sharedMaterial != null
                    ? projectileRenderer.sharedMaterial.mainTexture : null;
                if (projectileTexture == null || projectileTexture.name != "texRC3_missile_projectile")
                {
                    failures.Add("projectile texture wrong: "
                        + (projectileTexture != null ? projectileTexture.name : "null"));
                }
            }
            GameObject flash = GameObject.Find("RC3MissileFlash");
            if (flash != null)
            {
                Renderer flashRenderer = flash.GetComponentInChildren<Renderer>();
                Texture flashTexture = flashRenderer != null && flashRenderer.sharedMaterial != null
                    ? flashRenderer.sharedMaterial.mainTexture : null;
                if (flashTexture == null || flashTexture.name != "texRC3_missile_flash")
                {
                    failures.Add("muzzle flash texture wrong: "
                        + (flashTexture != null ? flashTexture.name : "null"));
                }
            }
            if (rig.TryFire())
            {
                failures.Add("second trigger press fired during cooldown");
            }

            dummyBody.velocity = Vector3.zero;
            dummyBody.angularVelocity = Vector3.zero;
            Vector3 dummyBefore = dummy.transform.position;
            float simulated = 0f;
            RoverPortRC3MissileProjectile projectileComponent = projectile.GetComponent<RoverPortRC3MissileProjectile>();
            while (simulated < 2f && projectile != null && projectile.activeSelf)
            {
                Physics.Simulate(Step);
                projectileComponent.Step(Step);
                simulated += Step;
            }
            if (projectile != null && projectile.activeSelf)
            {
                var leftoverComponent = projectile.GetComponent<RoverPortRC3MissileProjectile>();
                FieldInfo explodedField = typeof(RoverPortRC3MissileProjectile).GetField("m_exploded",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                failures.Add(string.Format(CultureInfo.InvariantCulture,
                    "projectile still alive after {0:0.00}s of simulation at {1} (exploded={2}, dummyY={3:0.00})",
                    simulated, projectile.transform.position, explodedField.GetValue(leftoverComponent), dummy.transform.position.y));
            }
            float dummySpeed = dummyBody.velocity.magnitude;
            float dummyMoved = Vector3.Distance(dummy.transform.position, dummyBefore);
            if (dummySpeed < 1f && dummyMoved < 0.01f)
            {
                failures.Add(string.Format(CultureInfo.InvariantCulture,
                    "dummy was not pushed by the explosion (speed {0:0.00}, moved {1:0.000})", dummySpeed, dummyMoved));
            }
            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "impact: simulated {0:0.00}s, dummy speed {1:0.00} m/s, moved {2:0.000} m",
                simulated, dummySpeed, dummyMoved));

            // cooldown expiry allows a second shot
            FieldInfo timeField = typeof(RoverPortMissilePart).GetField("m_currentTime",
                BindingFlags.Instance | BindingFlags.NonPublic);
            timeField.SetValue(rig, rig.Cooldown + 1f);
            if (!rig.TryFire())
            {
                failures.Add("trigger press after cooldown did not fire");
            }
            spawned.Add(GameObject.Find("RC3MissileProjectile"));

            // ---- previews ----
            var cameraObject = new GameObject("RC3AuditCamera");
            camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(30, 34, 44, 255);
            camera.cullingMask = 1 << Layer;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            image = new Texture2D(Size, Size, TextureFormat.ARGB32, false);

            SetView(clone, true);
            int builtVisible = VisibleRenderers(clone);
            if (builtVisible != 3)
            {
                failures.Add("build view expected 3 visible renderers, got " + builtVisible);
            }
            Frame(camera, clone);
            Save(camera, target, image, Path.Combine(previewDirectory, "built.png"));

            SetView(clone, false);
            int runVisible = VisibleRenderers(clone);
            if (runVisible != 3)
            {
                failures.Add("run view expected 3 visible renderers, got " + runVisible);
            }
            Frame(camera, clone);
            Save(camera, target, image, Path.Combine(previewDirectory, "run.png"));

            // firing view: render the launcher with the live projectile/flash of the second shot
            Frame(camera, clone);
            camera.orthographicSize = 3f;
            Save(camera, target, image, Path.Combine(previewDirectory, "firing.png"));
        }
        catch (Exception e)
        {
            failures.Add(e.ToString());
        }
        finally
        {
            PhysicsSimulation.Auto = previousAutoSimulation;
            if (clone != null) UnityEngine.Object.DestroyImmediate(clone);
            if (dummy != null) UnityEngine.Object.DestroyImmediate(dummy);
            if (wall != null) UnityEngine.Object.DestroyImmediate(wall);
            foreach (GameObject leftover in spawned)
            {
                if (leftover != null)
                {
                    UnityEngine.Object.DestroyImmediate(leftover);
                }
            }
            foreach (GameObject leftover in UnityEngine.Object.FindObjectsOfType<GameObject>(true))
            {
                if (leftover != null && (leftover.name == "RC3MissileProjectile" || leftover.name == "RC3MissileFlash"))
                {
                    UnityEngine.Object.DestroyImmediate(leftover);
                }
            }
            if (image != null) UnityEngine.Object.DestroyImmediate(image);
            if (target != null) target.Release();
            if (camera != null) UnityEngine.Object.DestroyImmediate(camera.gameObject);
        }

        lines.Add("failures: " + failures.Count);
        lines.AddRange(failures);
        File.WriteAllLines(Path.Combine(output, "rc3-missile-audit.txt"), lines);
        Debug.Log("RCR2_RC3_AUDIT: failures=" + failures.Count);
        if (failures.Count > 0)
        {
            throw new Exception(string.Join("\n", failures));
        }
        Debug.Log("RCR2_RC3_AUDIT_PASS: part_rc3_missile injected, geometry checked, fire/cooldown/impact verified.");
    }

    private static int VisibleRenderers(GameObject root)
    {
        int count = 0;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer.enabled || !IsLocallyActive(renderer.transform))
            {
                continue;
            }
            count++;
        }
        return count;
    }

    private static bool IsLocallyActive(Transform node)
    {
        for (Transform current = node; current != null; current = current.parent)
        {
            if (!current.gameObject.activeSelf)
            {
                return false;
            }
        }
        return true;
    }

    private static List<Renderer> GetPrivateRenderers(RoverPortPart part, string field)
    {
        return (List<Renderer>)typeof(RoverPortPart)
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(part);
    }

    private static void SetView(GameObject part, bool construction)
    {
        var component = part.GetComponent<RoverPortPart>();
        foreach (Renderer renderer in GetPrivateRenderers(component, "m_constructionRenderers"))
        {
            renderer.enabled = construction;
        }
        foreach (Renderer renderer in GetPrivateRenderers(component, "m_physicsRenderers"))
        {
            renderer.enabled = !construction;
        }
    }

    private static void Frame(Camera camera, GameObject part)
    {
        var bounds = new Bounds();
        bool started = false;
        foreach (Renderer renderer in part.GetComponentsInChildren<Renderer>(true))
        {
            if (!IsLocallyActive(renderer.transform))
            {
                continue;
            }
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
        camera.orthographicSize = Mathf.Max(0.05f, Mathf.Max(bounds.extents.y, bounds.extents.x) * 1.15f);
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
