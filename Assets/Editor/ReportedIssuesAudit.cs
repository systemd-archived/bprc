using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class RC1ParityAudit
{
    public static void RunReportedIssues()
    {
        if (!Application.isBatchMode || !File.Exists(".bprc-rc1-audit"))
            throw new InvalidOperationException("Use the isolated RC1 audit project.");
        Rows.Clear(); failures = 0;
        Directory.CreateDirectory("Builds/QA");
        reportPath = "Builds/QA/reported-issues.txt";
        Physics.autoSimulation = false;
        try
        {
            Boot();
            var gd = ScriptableObject.CreateInstance<GameData>();
            gd.m_jointConnectionStrengthWeak = 1000; gd.m_jointConnectionStrengthNormal = 2000;
            gd.m_jointConnectionStrengthHigh = 4000; gd.m_jointConnectionStrengthHighlyExtreme = 10000;
            Static(typeof(WPFMonoBehaviour), "s_gameData", gd);
            ReportCase("terrain", TerrainDefaults);
            ReportCase("native wheel", NativeWheel);
            ReportCase("bigfoot geometry", BigfootGeometry);
            ReportCase("gravity range", GravityRange);
            ReportCase("native attachments", NativeAttachments);
            ReportCase("runtime mounts", RuntimeMounts);
            ReportCase("night atlas", NightAtlas);
            foreach (int index in new[] { 1009, 1028, 1029 })
            {
                var part = Build(index, Vector3.zero, true);
                var backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
                backdrop.transform.position = new Vector3(0, 0, 2); backdrop.transform.localScale = Vector3.one * 20;
                var mat = new Material(Shader.Find("Sprites/Default")); mat.color = new Color(0.1f, 0.3f, 0.35f);
                backdrop.GetComponent<Renderer>().sharedMaterial = mat;
                Check(part.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled).All(r => r.sortingOrder > 0),
                    index + " artwork stays ahead of the background without blueprint shaders");
                Capture(part, "Builds/QA/wheel-" + index + ".png");
                Object.DestroyImmediate(backdrop); Object.DestroyImmediate(mat);
                Field(part, "m_throttle", 1f);
                typeof(RCR2Port.RoverPortPoweredPart).GetMethod("DriveWheels", Private).Invoke(part, new object[] { 1f, 0f });
                Check(part.GetComponentsInChildren<RCR2Port.RoverPortWheelSpin>().All(s => Mathf.Abs(s.SpinSpeed) < 0.001f),
                    index + " an unpowered airborne wheel does not actively spin");
                GroundedPower((RCR2Port.RoverPortPoweredPart)part, index);
                Remove(part);
            }
        }
        catch (Exception exception) { Check(false, exception.ToString()); }
        File.WriteAllLines(reportPath, Rows);
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    private static void ReportCase(string name, Action action)
    {
        try { action(); }
        catch (Exception error) { Check(false, name + ": " + (error.InnerException ?? error)); }
    }

    private static void GroundedPower(RCR2Port.RoverPortPoweredPart part, int index)
    {
        foreach (var body in part.GetComponentsInChildren<Rigidbody>()) body.useGravity = false;
        Field(part, "m_enabled", true);
        var energy = (Func<float>)typeof(RCR2Port.RoverPortPoweredPart).GetMethod("CalculateEnergyScale", Private)
            .CreateDelegate(typeof(Func<float>), part);
        Check(energy() == 0, index + " enabled wheel without an engine or fuel has no supplied energy");
        for (int i = 0; i < 16; i++) energy();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 256; i++) energy();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(allocated == 0, index + " repeated fallback power checks allocate zero managed bytes, measured=" + allocated);
        var tyre = part.GetComponentsInChildren<SphereCollider>().First();
        var ground = new GameObject("RC1 power test ground");
        ground.transform.position = new Vector3(tyre.bounds.center.x, tyre.bounds.min.y - 0.07f, 0);
        ground.AddComponent<BoxCollider>().size = new Vector3(8, 0.1f, 2);
        Physics.SyncTransforms();
        var drive = typeof(RCR2Port.RoverPortPoweredPart).GetMethod("DriveWheels", Private);
        drive.Invoke(part, new object[] { 1f, energy() });
        Check(part.rigidbody.GetAccumulatedForce().sqrMagnitude < 0.000001f,
            index + " unpowered grounded wheel applies no active force to its carrier");
        drive.Invoke(part, new object[] { 1f, 1f });
        Check(part.rigidbody.GetAccumulatedForce().x > 0.001f,
            index + " powered comparison applies real PhysX drive force at the same ground contact");
        Physics.Simulate(0.02f);
        Object.DestroyImmediate(ground);
    }

    private static void TerrainDefaults()
    {
        var record = typeof(INCustomBackgroundInterface).GetNestedType("Record", BindingFlags.NonPublic);
        object defaults = Activator.CreateInstance(record, true);
        Check(!(bool)record.GetField("FunctionTerrainPlotted").GetValue(defaults), "opening background settings cannot draw the default function terrain");
    }

    private static void SpriteManager()
    {
        if (INSpriteManager.Instance != null) return;
        var manager = Inactive("Native atlas audit").AddComponent<INSpriteManager>();
        Field(manager, "m_textAssets", new List<TextAsset> { AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/TextAsset/IngameAtlas4_TextAsset.txt") });
        Static(typeof(INSpriteManager), "Instance", manager);
        manager.Initialize();
    }

    private static BasePart Native(string path, Vector3 position)
    {
        SpriteManager();
        var holder = Inactive("Native staging");
        var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), holder.transform);
        go.SetActive(false); go.transform.SetParent(null); Object.DestroyImmediate(holder);
        go.transform.position = position;
        var part = go.GetComponent<BasePart>(); part.contraption = owner;
        part.Awake();
        foreach (var sprite in go.GetComponentsInChildren<INSerializedSprite>(true)) sprite.CreateMesh();
        part.EnsureRigidbody(); go.SetActive(true);
        part.rigidbody.useGravity = false;
        return part;
    }

    private static void NativeWheel()
    {
        var wheel = (OffRoadWheel)Native("Assets/GameObject/Part_MotorWheel_08_SET.prefab", Vector3.zero);
        Call(wheel, "Start");
        var tyre = wheel.GetComponent<SphereCollider>();
        var mesh = wheel.transform.Find("WheelPivot/WheelSprite").GetComponent<MeshFilter>();
        float artRadius = mesh.sharedMesh.bounds.extents.x * Mathf.Abs(mesh.transform.lossyScale.x);
        Check(Mathf.Abs(artRadius - tyre.radius) < 0.01f, "native off-road tyre image and collider have the same radius");
        Check(wheel.transform.Find("Support").GetComponent<Renderer>().sortingOrder > 0, "native wheel bracket renders ahead of the background");
        CaptureObject(wheel.gameObject, "Builds/QA/native-wheel.png", 2f, new Vector3(0, 0, -10));
        foreach (float scale in new[] { 0.5f, 1.3f, 2f })
        {
            wheel.SetCustomParam("PartScale", scale);
            wheel.ApplyPartScale();
            float radius = tyre.radius * Mathf.Abs(wheel.transform.lossyScale.x);
            float measured = (float)typeof(OffRoadWheel).GetField("m_radius", Private).GetValue(wheel);
            Check(Mathf.Abs(measured - radius) < 0.001f, "native rolling/contact radius follows uniform scale " + scale);
        }
        wheel.SetCustomParam("PartScale", 1f);
        wheel.SetCustomParam("PartScaleX", 0.5f); wheel.SetCustomParam("PartScaleY", 0.5f);
        Check(Mathf.Abs(wheel.transform.localScale.z - wheel.transform.localScale.x) < 0.001f, "wheel depth cannot inflate a reduced XY sphere");
        wheel.SetCustomParam("PartScaleX", 1f); wheel.SetCustomParam("PartScaleY", 1f);
        wheel.SetCustomParam("OffRoadWheelRadius", 1.8f); wheel.ApplyPartScale(); wheel.ApplyPartScale();
        Check(Mathf.Abs(tyre.radius - 1.8f) < 0.001f && Mathf.Abs(wheel.transform.localScale.x - 1f) < 0.001f,
            "off-road tyre scaling is not compounded into the part root");
        Object.DestroyImmediate(wheel.gameObject);
    }

    private static void BigfootGeometry()
    {
        foreach (int index in new[] { 1028, 1029 })
        foreach (float scale in new[] { 0.5f, 1f, 2f })
        foreach (int angle in new[] { 0, 37, 90 })
        {
            var part = Build(index, new Vector3(0, 5, 0), false, angle);
            part.SetCustomParam("PartScaleX", scale); part.SetCustomParam("PartScaleY", scale);
            part.ApplyPartScale(); part.ApplyPartScale();
            Physics.SyncTransforms();
            var source = part.GetComponentInChildren<SphereCollider>();
            float authoredRadius = part.PortData.motion.wheel_pivots.First(p => p.node.StartsWith("PE:")).radius;
            Check(Mathf.Abs(source.bounds.extents.x - authoredRadius * scale) < 0.002f,
                index + " construction collider follows XY scale=" + scale + " angle=" + angle);
            var clone = Object.Instantiate(part.gameObject).GetComponent<RCR2Port.RoverPortPart>();
            Remove(part); part = clone; part.contraption = owner; owner.Parts.Add(part);
            part.ApplyPartScale(); part.EnsureRigidbody();
            var suspension = part.GetComponent<RC1Port.RC1WheelSuspension>();
            var contact = suspension.Axle.GetComponent<SphereCollider>();
            var spin = part.GetComponentsInChildren<RCR2Port.RoverPortWheelSpin>(true).Single(s => s.Node.StartsWith("PE:"));
            float expectedRadius = spin.Radius * scale;
            Physics.SyncTransforms();
            float worldRadius = contact.bounds.extents.x;
            Check(Mathf.Abs(worldRadius - expectedRadius) < 0.002f,
                index + " collision follows the 2D wheel radius at scale=" + scale + " angle=" + angle + ": actual=" + worldRadius + " expected=" + expectedRadius);
            var mesh = spin.Pivot.GetComponentInChildren<MeshFilter>();
            var local = mesh.sharedMesh.bounds;
            Vector3 edgeX = mesh.transform.TransformVector(Vector3.right * local.extents.x);
            Vector3 edgeY = mesh.transform.TransformVector(Vector3.up * local.extents.y);
            float artRadius = Mathf.Max(edgeX.magnitude, edgeY.magnitude);
            Check(Mathf.Abs(worldRadius - artRadius) < 0.04f * scale,
                index + " collision agrees with the trimmed tyre artwork at scale=" + scale + " angle=" + angle + ": art=" + artRadius);
            float driveRadius = (float)typeof(RCR2Port.RoverPortPoweredPart).GetMethod("GetWheelRadius", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { suspension.Axle });
            Check(Mathf.Abs(driveRadius - worldRadius) < 0.002f,
                index + " ground detection uses the actual world contact radius at scale=" + scale + " angle=" + angle);
            if (scale == 1f && angle == 0)
            {
                var radiusGetter = (Func<Rigidbody, float>)typeof(RCR2Port.RoverPortPoweredPart)
                    .GetMethod("GetWheelRadius", BindingFlags.NonPublic | BindingFlags.Static)
                    .CreateDelegate(typeof(Func<Rigidbody, float>));
                for (int i = 0; i < 16; i++) radiusGetter(suspension.Axle);
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 256; i++) radiusGetter(suspension.Axle);
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Check(allocated == 0, index + " world contact radius checks allocate zero managed bytes, measured=" + allocated);
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.name = "Bigfoot contact ground";
                float groundY = contact.bounds.center.y - worldRadius;
                ground.transform.position = new Vector3(0, groundY - 0.1f, 0);
                ground.transform.localScale = new Vector3(10, 0.2f, 1);
                var material = new Material(Shader.Find("Sprites/Default")); material.color = new Color(0.3f, 0.6f, 0.2f);
                ground.GetComponent<Renderer>().sharedMaterial = material;
                part.rigidbody.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
                Physics.SyncTransforms();
                for (int step = 0; step < 150; step++) Physics.Simulate(0.02f);
                Call(suspension, "LateUpdate"); Physics.SyncTransforms();
                float gap = contact.bounds.min.y - groundY;
                Check(Mathf.Abs(gap) < 0.025f && Mathf.Abs(suspension.Axle.velocity.y) < 0.05f,
                    index + " actual PhysX tyre settles on the ground, gap=" + gap);
                Check(Mathf.Abs(mesh.GetComponent<Renderer>().bounds.min.y - groundY) < 0.04f,
                    index + " settled tyre artwork meets the ground");
                CaptureObject(part.gameObject, "Builds/QA/bigfoot-contact-" + index + ".png", 1.7f,
                    new Vector3(contact.bounds.center.x, groundY + 1.3f, -10));
                Object.DestroyImmediate(ground); Object.DestroyImmediate(material);
            }
            foreach (float runtimeScale in new[] { 0.5f, 2f, scale })
            {
                part.SetCustomParam("PartScaleX", runtimeScale); part.SetCustomParam("PartScaleY", runtimeScale);
                part.ApplyPartScale(); part.ApplyPartScale(); Physics.SyncTransforms();
                Check(Mathf.Abs(contact.bounds.extents.x - authoredRadius * runtimeScale) < 0.002f,
                    index + " runtime resize is repeatable at scale=" + runtimeScale + " angle=" + angle);
            }
            Remove(part);
        }
    }

    private static void GravityRange()
    {
        INSettings.SetSettingsValue("SpecialEggs", true);
        var c = new GameObject("Gravity range audit").AddComponent<Contraption>();
        Call(c, "Awake");
        Field(c, "m_running", true); Static(typeof(Contraption), "Instance", c);
        var eggObject = Inactive("Gravity egg"); eggObject.transform.SetParent(c.transform);
        var egg = eggObject.AddComponent<Egg>(); egg.customPartIndex = 1; egg.contraption = c;
        egg.EnsureRigidbody(); egg.rigidbody.useGravity = false; eggObject.SetActive(true); egg.Initialize();
        egg.SetCustomParam("GravityEggForce", 10); egg.SetCustomParam("GravityEggInnerRadius", 0);
        egg.SetCustomParam("GravityEggOuterRadius", 5); egg.SetEnabled(true);
        Check(INSettings.GetBool(INFeature.SpecialEggs) && egg.IsEnabled(), "gravity range fixture enables the special egg");
        var bodies = new List<Rigidbody>();
        foreach (float distance in new[] { 4.9f, 5f, 5.1f, 0f })
        {
            var target = new GameObject("Gravity target " + distance); target.transform.SetParent(c.transform);
            target.transform.position = new Vector3(distance, 0, 0);
            var body = target.AddComponent<Rigidbody>(); body.useGravity = false; bodies.Add(body);
        }
        Physics.SyncTransforms(); Call(egg, "FixedUpdate"); Physics.Simulate(0.02f);
        Rows.Add("INFO gravity bodies=" + INContraption.Instance.Rigidbodies.Length + " velocities=" + string.Join(",", bodies.Select(b => b.velocity.x)));
        Check(bodies[0].velocity.x < 0 && bodies.Skip(1).All(b => b.velocity == Vector3.zero),
            "gravity egg affects only bodies inside its actual 5-unit range, with a zero inner radius and coincident bodies");
        Check(BuildMachineTransform.Finite(egg.rigidbody.velocity.x), "gravity egg never applies non-finite force to itself");
        Object.DestroyImmediate(c.gameObject); Static(typeof(Contraption), "Instance", owner);
    }

    private static void NativeAttachments()
    {
        foreach (string path in new[] { "Assets/GameObject/Part_GrapplingHook_07_SET.prefab", "Assets/GameObject/Part_MotorWheel_08_SET.prefab" })
        {
            var part = Native(path, Vector3.zero);
            typeof(Contraption).GetMethod("PostAwake", Private).Invoke(owner, null);
            owner.Parts.Add(part); typeof(Contraption).GetMethod("SetPartPos", Private).Invoke(owner, new object[] { 2, 3, part });
            new BuildMachineTransform { angle = 37, translation = new Vector2(2000, -1000) }.Store(part);
            owner.RefreshPartPose(part);
            part.transform.localPosition += Vector3.right * 0.6f;
            typeof(Contraption).GetMethod("RestoreSimulationPartPose", Private).Invoke(owner, new object[] { part });
            Check(part.m_coordX == 2 && part.m_coordY == 3, part.GetType().Name + " runtime restores the saved cell instead of rounding displayed coordinates");
            Object.DestroyImmediate(part.gameObject); owner.Parts.Clear();
        }
        foreach (float step in new[] { 0.02f, 0.04f })
        {
            foreach (string path in new[] { "Assets/GameObject/Part_GrapplingHook_07_SET.prefab", "Assets/GameObject/Part_MotorWheel_08_SET.prefab" })
            {
                Call(owner, "PostAwake");
                var attachment = Native(path, Vector3.zero);
                var pipe = (FuelTube)Native("Assets/GameObject/Part_JetEngine_04_SET.prefab", Vector3.up);
                PlaceNative(attachment, 0, 0); PlaceNative(pipe, 0, 1);
                Physics.SyncTransforms(); pipe.CreateCustomJoints();
                var joints = attachment.GetComponents<Joint>().Concat(pipe.GetComponents<Joint>()).ToArray();
                Check(joints.Length == 1 && owner.FindPartJoints(pipe).Count == 1,
                    attachment.GetType().Name + " auto pipe creates one structural mount at step " + step);
                for (int i = 0; i < 30; i++) Physics.Simulate(step);
                Check(joints.All(j => j != null && j.connectedBody != null) &&
                    Vector3.Distance(attachment.transform.position, pipe.transform.position) < 1.1f,
                    attachment.GetType().Name + " pipe mount survives PhysX startup at step " + step);
                Check(!pipe.GetConnectedParts().Any(), "structural mount does not add a native wheel/cannon to the fuel network");
                Object.DestroyImmediate(attachment.gameObject); Object.DestroyImmediate(pipe.gameObject); owner.Parts.Clear();
            }
            Call(owner, "PostAwake");
            var wheel = Native("Assets/GameObject/Part_MotorWheel_08_SET.prefab", Vector3.zero);
            var spring = (Spring)Native("Assets/GameObject/Part_Spring_01_SET.prefab", Vector3.up);
            PlaceNative(wheel, 0, 0); PlaceNative(spring, 0, 1);
            Physics.SyncTransforms();
            Check(owner.CanConnectTo(spring, wheel, BasePart.Direction.Down), "native spring accepts wheel mount at step " + step);
            var mount = spring.CustomConnectToPart(wheel); owner.AddJointToMap(spring, wheel, mount);
            for (int i = 0; i < 30; i++) Physics.Simulate(step);
            Check(mount != null && mount.connectedBody == wheel.rigidbody &&
                Vector3.Distance(wheel.transform.position, spring.transform.position) < 1.1f,
                "spring wheel mount survives PhysX startup at step " + step);
            Object.DestroyImmediate(wheel.gameObject); Object.DestroyImmediate(spring.gameObject); owner.Parts.Clear();
        }
    }

    private static void PlaceNative(BasePart part, int x, int y)
    {
        owner.Parts.Add(part);
        typeof(Contraption).GetMethod("SetPartPos", Private).Invoke(owner, new object[] { x, y, part });
    }

    private static void RuntimeMounts()
    {
        var previous = owner;
        bool allDirections = INSettings.GetBool(INFeature.AllDirectionsConnection);
        try
        {
            foreach (bool all in new[] { false, true })
            foreach (bool wheelFirst in new[] { false, true })
            foreach (string mountType in new[] { "Spring", "AutoTube", "StraightTube", "CannonAutoTube" })
            {
                INSettings.SetSettingsValue("AllDirectionsConnection", all);
                owner = Inactive("Native runtime mount audit").AddComponent<Contraption>(); Call(owner, "Awake");
                INContraption.Instance.Enabled = false;
                owner.gameObject.SetActive(true);
                var wheel = Native(mountType == "CannonAutoTube" ? "Assets/GameObject/Part_GrapplingHook_07_SET.prefab" : "Assets/GameObject/Part_MotorWheel_08_SET.prefab", Vector3.zero);
                string mountingPrefab = mountType == "Spring" ? "Assets/GameObject/Part_Spring_01_SET.prefab" :
                    mountType == "StraightTube" ? "Assets/GameObject/Part_JetEngine_03_SET.prefab" : "Assets/GameObject/Part_JetEngine_04_SET.prefab";
                var spring = Native(mountingPrefab, Vector3.up);
                if (mountType == "StraightTube") spring.SetRotation(1);
                PlaceNative(wheel, 0, 0); PlaceNative(spring, 0, 1);
                if (wheelFirst) { wheel.transform.SetParent(owner.transform); spring.transform.SetParent(owner.transform); }
                else { spring.transform.SetParent(owner.transform); wheel.transform.SetParent(owner.transform); }
                BuildMachineTransform.Apply(owner, new BuildMachineTransform { angle = 37, translation = new Vector2(10, -8) });
                owner.StartContraption();
                var joints = owner.GetComponentsInChildren<Joint>();
                Check(owner.ConnectedComponentCount == 1 && joints.Length == 1 &&
                    (mountType != "Spring" || joints[0].GetComponent<Spring>() != null),
                    "real simulation startup uses one " + mountType + " mount: allDirections=" + all + " partFirst=" + wheelFirst);
                Physics.SyncTransforms();
                for (int i = 0; i < 30; i++) Physics.Simulate(0.04f);
                Check(joints.All(j => j != null && j.connectedBody != null) && Vector3.Distance(wheel.transform.position, spring.transform.position) < 1.1f,
                    "rotated " + mountType + " remains mounted after startup: allDirections=" + all + " partFirst=" + wheelFirst);
                Object.DestroyImmediate(owner.gameObject);
            }
        }
        finally
        {
            if (owner != null && owner != previous) Object.DestroyImmediate(owner.gameObject);
            owner = previous; Static(typeof(Contraption), "Instance", owner);
            INSettings.SetSettingsValue("AllDirectionsConnection", allDirections);
        }
    }

    private static void NightAtlas()
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Texture2D/Background_Night_Sheet_01.png");
        Check(texture.mipmapCount == 1, "night atlas cannot bleed neighboring rectangles through mip levels");
        var go = Object.Instantiate(Resources.Load<GameObject>("environment/background/background_night_01_set 1"));
        go.transform.localScale = new Vector3(8, 8, 1);
        foreach (var sprite in go.GetComponentsInChildren<UnmanagedSprite>()) sprite.RebuildMesh();
        var nearFill = go.transform.Find("BGLayerNear/Background_Near_fill").GetComponent<Renderer>();
        var grassFill = go.transform.Find("GrassLayer/Grass_fill").GetComponent<Renderer>();
        Check(nearFill.bounds.min.y < grassFill.bounds.max.y && nearFill.transform.position.z > grassFill.transform.position.z,
            "near background fill continues behind opaque foreground without exposing a rectangular bottom edge");
        var grassLayer = go.transform.Find("GrassLayer");
        Check(grassLayer.GetComponentsInChildren<Renderer>().All(r => !r.enabled) && grassLayer.GetComponentsInChildren<Collider>().Length > 0,
            "night's duplicate rectangular ground artwork is hidden while its physical ground remains intact");
        CaptureObject(go, "Builds/QA/night-background.png", 90f, new Vector3(0, 0, -100));
        Object.DestroyImmediate(go);
        var uvObject = new GameObject("Reduced atlas audit");
        var uv = uvObject.AddComponent<UnmanagedSprite>();
        var small = new Texture2D(512, 256); var mat = new Material(Shader.Find("Sprites/Default")); mat.mainTexture = small;
        uv.GetComponent<Renderer>().sharedMaterial = mat;
        var values = uv.CalculateUVs(0, 5, 16, 3);
        Check(Mathf.Abs(values[0].x - 0.5f / 512f) < 0.000001f && Mathf.Abs(values[0].y - (5f / 16f + 0.5f / 256f)) < 0.000001f,
            "background atlas UV inset follows actual texture dimensions on every platform");
        Object.DestroyImmediate(uvObject); Object.DestroyImmediate(small); Object.DestroyImmediate(mat);
    }

    private static void CaptureObject(GameObject go, string path, float size, Vector3 position)
    {
        var cameraObject = new GameObject("Reported issue camera"); var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = size; camera.transform.position = position;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.1f, 0.3f, 0.35f);
        var target = new RenderTexture(768, 512, 24); camera.targetTexture = target; camera.Render();
        var old = RenderTexture.active; RenderTexture.active = target;
        var image = new Texture2D(768, 512, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 768, 512), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG());
        RenderTexture.active = old; Object.DestroyImmediate(image); Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(target);
    }
}
