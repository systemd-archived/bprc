using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using RCR2Port;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

public static partial class RC1ParityAudit
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly List<string> Rows = new List<string>();
    private static int failures;
    private static RoverPortCatalog catalog;
    private static RoverPortCatalogData data;
    private static Contraption owner;
    private static INRc1Controls controls;
    private static string reportPath;

    private sealed class Mode : GameMode
    {
        public override void OnDataLoadedStart() { }
        public override void InitGameMode() { }
        public override void OnDataLoadedDone() { }
        public override LevelManager.GameState SetGameState(LevelManager.GameState a, LevelManager.GameState b) => b;
        protected override bool HandleUIEvent(UIEvent value) => false;
        public override int GetPartCount(BasePart.PartType type) => 0;
    }

    public static void Run()
    {
        Run(false);
    }

    public static void RunWheelArtwork()
    {
        Run(true);
    }

    private static void Run(bool artworkOnly)
    {
        if (!Application.isBatchMode || !File.Exists(".bprc-rc1-audit"))
            throw new InvalidOperationException("Use Tools/run-rc1-parity-audit.ps1 in an isolated project.");
        Rows.Clear(); failures = 0;
        Directory.CreateDirectory("Builds/QA");
        reportPath = artworkOnly ? "Builds/QA/rc1-wheel-art.txt" : "Builds/QA/rc1-parity.txt";
        Physics.autoSimulation = false;
        try
        {
            Boot();
            if (!artworkOnly)
            {
                Connections();
                Inputs();
                foreach (var engine in new[] { PhysicsEngine.PhysX, PhysicsEngine.Box3D, PhysicsEngine.Jolt, PhysicsEngine.Bepu })
                {
                    Suspension(engine);
                    Vehicle(engine);
                }
                GamePhysics.SelectEngine(PhysicsEngine.PhysX);
                Aerodynamics();
                ArtAndFlame();
            }
            GamePhysics.SelectEngine(PhysicsEngine.PhysX);
            WheelArtwork();
        }
        catch (Exception exception) { Check(false, exception.ToString()); }
        File.WriteAllLines(reportPath, Rows);
        Debug.Log("RC1 " + (artworkOnly ? "wheel artwork" : "parity") + " audit: " + (Rows.Count - failures) + " passed, " + failures + " failed");
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    private static void Check(bool value, string label)
    {
        Rows.Add((value ? "PASS " : "FAIL ") + label);
        File.WriteAllLines(reportPath, Rows);
        if (!value) failures++;
    }

    private static GameObject Inactive(string name)
    {
        var go = new GameObject(name);
        go.SetActive(false);
        return go;
    }

    private static void Field(object target, string name, object value)
    {
        Type type = target.GetType();
        while (type != null)
        {
            var field = type.GetField(name, Private | BindingFlags.Public);
            if (field != null) { field.SetValue(target, value); return; }
            type = type.BaseType;
        }
        throw new MissingFieldException(name);
    }

    private static void Static(Type type, string name, object value)
    {
        var field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Static) ??
            type.GetField("<" + name + ">k__BackingField", BindingFlags.NonPublic | BindingFlags.Static);
        field.SetValue(null, value);
    }

    private static void Call(object target, string method)
    {
        target.GetType().GetMethod(method, Private).Invoke(target, null);
    }

    private static void Boot()
    {
        Innovation.Json.Service = (Innovation.IJsonService)Activator.CreateInstance(typeof(INSettings).Assembly.GetType("JsonService"));
        var resources = (Dictionary<(string, string), Object>)typeof(INUnity)
            .GetField("s_resources", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        foreach (string path in Directory.GetFiles("Assets/TextAsset", "IN*.json"))
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (asset != null) resources[("TextAsset", asset.name)] = asset;
        }
        Static(typeof(INUnity), "SettingsPath", Path.GetFullPath("Builds/QA/Settings"));
        Static(typeof(INUnity), "DataPath", INUnity.SettingsPath);
        Directory.CreateDirectory(INUnity.SettingsPath);
        typeof(INSettings).GetMethod("Load", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { 3 });
        Static(typeof(INUserSettings), "Default", new INUserSettings(INUnity.Version));
        Static(typeof(INUserSettings), "Instance", new INUserSettings(INUnity.Version));
        INUserSettings.Instance.GeneralSettings.Rc1Controls = true;
        owner = Inactive("RC1 audit owner").AddComponent<Contraption>();
        Field(owner, "m_running", true);
        var level = Inactive("RC1 audit level").AddComponent<LevelManager>();
        Field(level, "m_gameMode", new Mode { ContraptionRunning = owner });
        Static(typeof(WPFMonoBehaviour), "s_levelManager", level);
        controls = new GameObject("RC1 audit controls").AddComponent<INRc1Controls>();
        Static(typeof(INRc1Controls), "Instance", controls);
        typeof(INRc1Controls).GetMethod("BuildPanel", Private).Invoke(controls, null);
        Call(controls, "Update");
        data = JsonUtility.FromJson<RoverPortCatalogData>(Resources.Load<TextAsset>("RC1Ported/rc1_catalog").text);
        catalog = RoverPortCatalog.Instance;
        Field(catalog, "m_host", Inactive("RC1 audit catalog"));
        Field(catalog, "m_iconHost", Inactive("RC1 audit icons"));
        var textures = (Dictionary<string, Texture2D>)typeof(RoverPortCatalog).GetField("m_textures", Private).GetValue(catalog);
        foreach (var texture in data.textures)
            textures[texture.guid] = Resources.Load<Texture2D>("RC1Ported/Textures/" + Path.GetFileNameWithoutExtension(texture.canonical_file));
    }

    private static RoverPortPart Build(int index, Vector3 position, bool physical = false, int angle = 0)
    {
        var record = data.parts.Single(p => p.id == "part_rc1_" + index);
        var go = (GameObject)typeof(RoverPortCatalog).GetMethod("BuildPart", Private)
            .Invoke(catalog, new object[] { record, 4000 + index - 1001 });
        go.transform.SetParent(null);
        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(0, 0, angle);
        var part = go.GetComponent<RoverPortPart>();
        part.contraption = owner;
        owner.Parts.Add(part);
        if (physical) part.EnsureRigidbody();
        go.SetActive(true);
        Physics.SyncTransforms();
        var suspension = part.GetComponent<RC1Port.RC1WheelSuspension>();
        if (suspension != null) Call(suspension, "OnEnable");
        return part;
    }

    private static void Remove(RoverPortPart part)
    {
        owner.Parts.Remove(part);
        Object.DestroyImmediate(part.gameObject);
    }

    private static void Connections()
    {
        Check(data.parts.Length == 32, "all 32 RC1 parts retained");
        foreach (var record in data.parts)
        {
            int index = int.Parse(record.id.Substring(9));
            var part = Build(index, Vector3.zero);
            foreach (int angle in new[] { 0, 90, 180, 270 })
            {
                part.transform.rotation = Quaternion.Euler(0, 0, angle);
                foreach (Vector3 side in new[] { Vector3.up, Vector3.down, Vector3.left, Vector3.right })
                {
                    var neighbour = new GameObject("Native edge").AddComponent<BasePart>();
                    neighbour.m_partType = BasePart.PartType.MetalFrame;
                    neighbour.m_jointConnectionType = BasePart.JointConnectionType.Source;
                    neighbour.m_jointConnectionDirection = BasePart.JointConnectionDirection.Any;
                    neighbour.transform.position = part.transform.TransformDirection(side);
                    bool expected = record.grid.mounts.Any(m =>
                        Vector3.Dot(new Vector3(m.x, m.y, 0).normalized, side) > 0.99f);
                    bool found = RoverPortConnections.TryGetConnection(part, neighbour, out _, out _);
                    Check(found == expected, index + " connection " + side + " at " + angle);
                    Object.DestroyImmediate(neighbour.gameObject);
                }
            }
            Remove(part);
        }
    }

    private static void Press(string name, bool down)
    {
        var button = controls.transform.Find("Panel/" + name).gameObject;
        var pointer = new PointerEventData(EventSystem.current) { pointerId = -1 };
        if (down) ExecuteEvents.Execute(button, pointer, ExecuteEvents.pointerDownHandler);
        else ExecuteEvents.Execute(button, pointer, ExecuteEvents.pointerUpHandler);
    }

    private static void Inputs()
    {
        Check(INRc1Controls.Active, "controls active for a running vehicle");
        var wheel = (RoverPortPoweredPart)Build(1009, new Vector3(30, 0, 0), true);
        Press("Forward", true);
        Call(wheel, "Update");
        Check(wheel.IsEnabled() && wheel.InputThrottle == 1f, "pedal enables a previously disabled RC1 wheel");
        Press("Forward", false);
        Call(wheel, "Update");
        Check(!wheel.IsEnabled() && wheel.InputThrottle == 0f, "pedal release stops drive");
        Press("Back", true);
        Call(wheel, "Update");
        Check(wheel.InputThrottle == -1f, "reverse pedal reaches the wheel");
        Press("Back", false);
        var gyro = Build(1019, new Vector3(35, 0, 0), true);
        gyro.rigidbody.useGravity = false;
        Press("Forward", true);
        Call(gyro.GetComponent<RoverPortGyro>(), "FixedUpdate");
        GamePhysics.Simulate(0.02f);
        Check(gyro.rigidbody.angularVelocity.z > 0f, "pedal applies real gyro torque");
        Press("Forward", false);
        var source = Build(1017, new Vector3(40, 0, 0));
        Check(source.IsEngine() && source.m_enginePower > 0f && source.m_powerConsumption == 0f,
            "RC1 accumulator supplies the BPLE engine power pool");
        var nativeObject = Inactive("Native BPLE motor");
        nativeObject.transform.position = new Vector3(50, 5, 0);
        nativeObject.AddComponent<SphereCollider>().radius = 0.5f;
        var nativeBody = nativeObject.AddComponent<Rigidbody>();
        nativeBody.useGravity = false;
        nativeBody.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
        var native = nativeObject.AddComponent<MotorWheel>();
        native.contraption = owner;
        native.ConnectedComponent = 0;
        native.m_force = 50f;
        native.m_powerConsumption = 100f;
        var pivot = new GameObject("WheelPivot").transform;
        pivot.SetParent(nativeObject.transform, false);
        Field(native, "m_wheelPivot", pivot);
        Field(native, "m_radius", 0.5f);
        Field(native, "m_lastContactDirection", Vector3.down);
        owner.Parts.Add(native);
        source.ConnectedComponent = 0;
        var engine = Inactive("Native BPLE engine").AddComponent<Engine>();
        engine.contraption = owner;
        engine.ConnectedComponent = 0;
        engine.m_enginePower = 1000f;
        owner.Parts.Add(engine);
        Field(owner, "m_connectedComponents", new List<Contraption.ConnectedComponent> {
            new Contraption.ConnectedComponent { enginePower = source.m_enginePower + engine.m_enginePower,
                powerConsumption = 100f, hasEngine = true, motorWheels = new List<MotorWheel> { native } }
        });
        native.InitializeEngine();
        nativeObject.SetActive(true);
        Press("Forward", true);
        Call(controls, "Update");
        Check(native.IsEnabled() && engine.IsEnabled() && source.IsEnabled(), "pedal starts real BPLE motor and both power sources without recursion");
        Call(native, "FixedUpdate");
        Check((float)typeof(MotorWheel).GetField("m_spinSpeed", Private).GetValue(native) > 0f,
            "real native motor spins forward");
        var ground = new GameObject("Native motor test ground");
        ground.transform.position = new Vector3(50, 4, 0);
        ground.AddComponent<BoxCollider>().size = new Vector3(10, 1, 1);
        Physics.SyncTransforms();
        Field(native, "m_thrustTimer", 1f);
        Call(native, "FixedUpdate");
        GamePhysics.Simulate(0.02f);
        Check(nativeBody.velocity.x > 0.01f, "real native motor applies forward ground force");
        Object.DestroyImmediate(ground);
        Press("Forward", false);
        Call(controls, "Update");
        Check(!native.IsEnabled() && !engine.IsEnabled(), "release stops real BPLE motor and engine");
        Press("Back", true);
        Call(controls, "Update");
        Call(native, "FixedUpdate");
        Check((float)typeof(MotorWheel).GetField("m_spinSpeed", Private).GetValue(native) < 0f,
            "real native motor reverses without a gearbox");
        Press("Back", false);
        Call(controls, "Update");
        owner.Parts.Remove(native);
        Object.DestroyImmediate(native.gameObject);
        owner.Parts.Remove(engine);
        Object.DestroyImmediate(engine.gameObject);
        Field(owner, "m_connectedComponents", new List<Contraption.ConnectedComponent>());
        Remove(wheel); Remove(gyro); Remove(source);
        Press("Jet", true);
        Check(INRc1Controls.Jet == 1f && INRc1Controls.Throttle == 0f, "jet and turbo have independent input");
        Press("Jet", false);
    }

    private static void Suspension(PhysicsEngine engine)
    {
        Check(GamePhysics.SelectEngine(engine), engine + " starts");
        if (GamePhysics.CurrentEngine != engine) return;
        foreach (int index in new[] { 1009, 1010, 1011, 1012, 1013, 1014, 1015, 1016, 1028, 1029 })
        foreach (int angle in new[] { 0, 90, 180, 270 })
        {
            var part = Build(index, new Vector3(10, 10, 0), true, angle);
            part.rigidbody.isKinematic = true;
            var axle = part.GetComponent<RC1Port.RC1WheelSuspension>().Axle;
            axle.useGravity = false;
            Physics.SyncTransforms();
            Vector3 rest = axle.position;
            for (int step = 0; step < 40; step++)
            {
                axle.EngineAddForce(part.transform.TransformDirection(Vector3.right * 15f + Vector3.up * 3f));
                GamePhysics.Simulate(0.02f);
            }
            Vector3 delta = part.transform.InverseTransformDirection(axle.position - rest);
            Check(GamePhysics.CurrentEngine == engine, engine + " " + index + " at " + angle + " keeps selected backend: " + GamePhysics.LastError);
            Check(Mathf.Abs(delta.x) < 0.015f && delta.y > 0.005f && delta.magnitude < 2f,
                engine + " " + index + " at " + angle + " suspends only on mount axis, delta=" + delta);
            Remove(part);
        }
    }

    private static void Vehicle(PhysicsEngine engine)
    {
        foreach (int index in new[] { 1028, 1029 })
        {
            var wheel = Build(index, new Vector3(0, 10, 0), true);
            var top = Build(1001, new Vector3(0, 11, 0), true);
            var side = Build(1001, new Vector3(index == 1028 ? 1 : -1, 10, 0), true);
            var axle = wheel.GetComponent<RC1Port.RC1WheelSuspension>().Axle;
            Vector3 rest = wheel.transform.InverseTransformPoint(axle.position);
            foreach (var frame in new[] { top, side })
            {
                var joint = wheel.gameObject.AddComponent<FixedJoint>();
                joint.connectedBody = frame.rigidbody;
                joint.autoConfigureConnectedAnchor = false;
                joint.anchor = Vector3.zero;
                joint.connectedAnchor = frame.transform.InverseTransformPoint(wheel.transform.position);
            }
            foreach (var body in owner.Parts.SelectMany(p => p.GetComponentsInChildren<Rigidbody>())) body.useGravity = false;
            Call(wheel.GetComponent<RoverPortContraptionCollision>(), "OnEnable");
            Physics.SyncTransforms();
            for (int step = 0; step < 150; step++)
            {
                wheel.rigidbody.EngineAddForce(Vector3.right * 5f);
                axle.EngineAddForce(wheel.transform.up * 3f);
                GamePhysics.Simulate(0.02f);
            }
            Vector3 displacement = wheel.transform.InverseTransformPoint(axle.position) - rest;
            Check(GamePhysics.CurrentEngine == engine && displacement.magnitude < 3f && Mathf.Abs(displacement.x) < 0.08f &&
                wheel.rigidbody.velocity.magnitude < 50f && Mathf.Abs(Vector3.Distance(top.transform.position, wheel.transform.position) - 1f) < 0.1f &&
                Mathf.Abs(Vector3.Distance(side.transform.position, wheel.transform.position) - 1f) < 0.1f,
                engine + " bigfoot " + index + " stays stable with two dynamic frames, axle=" + displacement + " speed=" + wheel.rigidbody.velocity.magnitude);
            Remove(top); Remove(side); Remove(wheel);
        }
    }

    private static void Aerodynamics()
    {
        var part = Build(1032, new Vector3(0, 5, 0), true);
        part.rigidbody.useGravity = false;
        part.rigidbody.velocity = Vector3.right * 5f;
        var spoiler = part.GetComponent<RoverPortSpoiler>();
        Call(spoiler, "FixedUpdate");
        GamePhysics.Simulate(0.02f);
        Check(spoiler.LastDownforce > 29.9f && part.rigidbody.velocity.y < 0f, "APK spoiler applies downforce through physics backend");
        Remove(part);
    }

    private static void WheelArtwork()
    {
        bool autoSync = Physics.autoSyncTransforms;
        Physics.autoSyncTransforms = false;
        foreach (int index in new[] { 1009, 1010, 1011, 1012, 1013, 1014, 1015, 1016, 1028, 1029 })
        foreach (int angle in new[] { 0, 90, 180, 270 })
        {
            var part = Build(index, Vector3.zero, true, angle);
            var suspension = part.GetComponent<RC1Port.RC1WheelSuspension>();
            var axle = suspension.Axle;
            var spin = part.GetComponentsInChildren<RoverPortWheelSpin>(true).Single(s => s.Node.StartsWith("PE:"));
            Transform pivot = spin.Pivot;
            var tyre = pivot.GetComponentInChildren<MeshRenderer>();
            Vector3 meshOffset = tyre.bounds.center - pivot.position;
            Vector3 rest = pivot.localPosition;
            Vector3 axleRest = axle.transform.localPosition;
            // These are the APK's AnchorWheelTransform nodes, separate from the spinning tyre.
            string hubName = index == 1011 ? null : index == 1010 ? "L1_wheelE_a" :
                index == 1013 || index == 1014 ? "parts_wheel02_anchor" : "L1_wheel_a";
            Transform hub = part.GetComponentsInChildren<Transform>(true).FirstOrDefault(t =>
                t.name == hubName && !t.IsChildOf(part.ResolvePortNode("ME:src0")));
            Vector3 hubRest = hub != null ? hub.localPosition : Vector3.zero;
            Quaternion hubRotation = hub != null ? hub.localRotation : Quaternion.identity;
            Transform background = part.GetComponentsInChildren<Transform>(true).First(t =>
                (t.name.EndsWith("_bg")) && !t.IsChildOf(part.ResolvePortNode("ME:src0")));
            Vector3 backgroundRest = background.localPosition;
            bool tyreFollows = true, hubFollows = true, bracketFixed = true, physicsFixed = true, tyreCentered = true;
            float maxError = 0f;
            for (int frame = 0; frame < 128; frame++)
            {
                // Two revolutions forward and two backward, with compressed/extended suspension.
                spin.Advance((frame < 64 ? 1f : -1f) * 2f * Mathf.PI * spin.Radius, false, 1f / 32f);
                Vector3 displacement = part.transform.TransformVector(Vector3.up * (0.12f * Mathf.Sin(frame * 0.2f)));
                part.transform.position = new Vector3(frame * 0.05f, 0.01f * frame, 0f);
                axle.position = part.transform.TransformPoint(axleRest) + displacement;
                Physics.SyncTransforms();
                // Interpolation presents a pose between physics ticks, while Rigidbody.position
                // still describes the last simulation tick. Stage that difference without a sync.
                part.transform.position += Vector3.right * (0.4f + (frame % 5) * 0.1f);
                axle.transform.position = part.transform.TransformPoint(axleRest) + displacement;
                Vector3 physicsPosition = axle.position;
                Call(suspension, "LateUpdate");
                Vector3 expected = pivot.parent.TransformPoint(rest) + displacement;
                float error = Vector3.Distance(pivot.position, expected);
                maxError = Mathf.Max(maxError, error);
                tyreFollows &= error < 0.0001f;
                // The APK includes asymmetrically trimmed meshes (especially the carbon
                // wheel), so rotate the authored offset rather than recentering its quad.
                Vector3 centerError = tyre.bounds.center - expected - Quaternion.AngleAxis(spin.Angle, Vector3.forward) * meshOffset;
                tyreCentered &= centerError.magnitude < 0.0001f &&
                    Mathf.Abs(tyre.transform.TransformDirection(Vector3.forward).z) > 0.99f;
                if (hub != null)
                    hubFollows &= Vector3.Distance(hub.position, hub.parent.TransformPoint(hubRest) + displacement) < 0.0001f &&
                        Quaternion.Angle(hub.localRotation, hubRotation) < 0.001f;
                bracketFixed &= background.localPosition == backgroundRest;
                physicsFixed &= axle.position == physicsPosition;
            }
            Check(tyreFollows, index + " at " + angle + " tyre uses displayed axle through four revolutions; max error=" + maxError);
            Check(tyreCentered, index + " at " + angle + " outer mesh remains centered and in the sprite plane");
            if (hubName != null) Check(hub != null && hubFollows, index + " at " + angle + " APK hub follows suspension without spinning");
            Check(bracketFixed && physicsFixed, index + " at " + angle + " artwork leaves bracket and physics pose untouched");
            Remove(part);
        }
        Physics.autoSyncTransforms = autoSync;
    }

    private static void ArtAndFlame()
    {
        var part = Build(1009, Vector3.zero, true);
        foreach (var spin in part.GetComponentsInChildren<RoverPortWheelSpin>(true))
        {
            if (!spin.Node.StartsWith("PE:")) continue;
            spin.Advance(Mathf.PI * spin.Radius, false, 0.5f);
            var renderer = spin.Pivot.GetComponentInChildren<MeshRenderer>();
            Vector3 normal = renderer.transform.TransformDirection(Vector3.forward);
            Check(Mathf.Abs(normal.z) > 0.99f, "wheel spin preserves the sprite plane");
        }
        Capture(part, "Builds/QA/rc1-wheel.png");
        Remove(part);
        var jet = (RoverPortPoweredPart)Build(1020, Vector3.zero, true);
        var driver = jet.GetComponentInChildren<RoverPortParticleDriver>();
        var ps = driver.System;
        Check(!ps.emission.enabled && ps.textureSheetAnimation.cycleCount == 1,
            "APK plume uses per-step emission and one sheet cycle");
        Check(ps.GetComponent<ParticleSystemRenderer>().sharedMaterial.shader.name == "Mobile/Particles/Additive" &&
            Mathf.Abs(ps.textureSheetAnimation.frameOverTime.curve.Evaluate(0.25f) - 0.25f) < 0.001f,
            "APK plume uses additive blending and original linear sheet curve");
        ps.Play();
        for (int i = 0; i < 15; i++) { ps.Emit(1); ps.Simulate(0.02f, true, false); }
        Check(ps.particleCount > 0, "restored plume produces particles");
        Capture(jet, "Builds/QA/rc1-flame.png");
        Remove(jet);
        foreach (string name in new[] { "btn_gas", "btn_brake", "btn_turbo", "btn_start" })
            Check(Resources.Load<UnityEngine.Sprite>("RC1Controls/" + name) != null, "APK control sprite " + name + " loads");
        CaptureControls(1280, 720);
        CaptureControls(720, 1280);
    }

    private static void CaptureControls(int width, int height)
    {
        var cameraObject = new GameObject("Control audit camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.1f, 0.3f, 0.35f, 1f);
        var target = new RenderTexture(width, height, 24);
        camera.targetTexture = target;
        var canvas = controls.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1f;
        Canvas.ForceUpdateCanvases();
        camera.Render();
        RenderTexture.active = target;
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
        File.WriteAllBytes("Builds/QA/rc1-controls-" + width + "x" + height + ".png", texture.EncodeToPNG());
        RenderTexture.active = null;
        canvas.worldCamera = null;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        Object.DestroyImmediate(texture); Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(target);
    }

    private static void Capture(RoverPortPart part, string path)
    {
        var go = new GameObject("RC1 audit camera");
        var camera = go.AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 2.5f;
        camera.transform.position = new Vector3(-0.5f, 0, -10);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.1f, 0.3f, 0.35f, 1f);
        var target = new RenderTexture(384, 384, 24);
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        var image = new Texture2D(384, 384, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 384, 384), 0, 0); image.Apply();
        File.WriteAllBytes(path, image.EncodeToPNG());
        RenderTexture.active = null;
        Object.DestroyImmediate(image); Object.DestroyImmediate(go); Object.DestroyImmediate(target);
    }
}
