using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class RC1ParityAudit
{
    public static void RunGpuAndroidShaders()
    {
        if (!Application.isBatchMode || !File.Exists(".bprc-rc1-audit"))
            throw new InvalidOperationException("Use the isolated GPU audit project.");
        Rows.Clear(); failures = 0;
        Directory.CreateDirectory("Builds/QA"); reportPath = "Builds/QA/gpu-physics.txt";
        try
        {
            Check(BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android), "Unity Android build module is installed");
            // Only the marked isolated project is configured; the user's project keeps its API policy.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.Vulkan, UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
            string output = "Builds/QA/android-gpu-shaders"; Directory.CreateDirectory(output);
            var map = new[] { new AssetBundleBuild { assetBundleName = "gpu-pair-forces", assetNames = new[] { "Assets/Resources/Physics/PairForces.compute" } } };
            var manifest = BuildPipeline.BuildAssetBundles(output, map, BuildAssetBundleOptions.ForceRebuildAssetBundle, BuildTarget.Android);
            Check(manifest != null && manifest.GetAllAssetBundles().Contains("gpu-pair-forces"), "actual Android asset bundle build compiles the compute resource for Vulkan and GLES3");
            var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(map[0].assetNames[0]);
            var messages = ShaderUtil.GetComputeShaderMessages(shader);
            foreach (var message in messages) Rows.Add("SHADER " + message.severity + " " + message.message);
            Check(!messages.Any(m => m.severity.ToString() == "Error"), "Android compute shader import/build has no compiler errors");
            Rows.Add("No Android device was deployed or benchmarked by this shader packaging test.");
        }
        catch (Exception error) { Check(false, error.ToString()); }
        Rows.Add("FAILURES: " + failures); File.WriteAllLines(reportPath, Rows);
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    public static void RunGpuPhysics()
    {
        if (!Application.isBatchMode || !File.Exists(".bprc-rc1-audit"))
            throw new InvalidOperationException("Use Tools/run-gpu-physics-audit.ps1 in the isolated project.");
        Rows.Clear(); failures = 0;
        Directory.CreateDirectory("Builds/QA"); reportPath = "Builds/QA/gpu-physics.txt";
        Physics.autoSimulation = false;
        try
        {
            Boot(); GamePhysics.SelectEngine(PhysicsEngine.PhysX);
            Rows.Add("DEVICE " + SystemInfo.graphicsDeviceName + " / " + SystemInfo.graphicsDeviceType +
                " / compute=" + SystemInfo.supportsComputeShaders + " / Unity " + Application.unityVersion);
            Check(Marshal.SizeOf<GpuPhysicsForces.Body>() == 32, "cross-platform body buffer stride is 32 bytes");
            GpuSettings();
            GpuEdgeCases();
            foreach (var kind in new[] { GpuPhysicsForces.ForceKind.Gravity, GpuPhysicsForces.ForceKind.PointCharge })
            {
                foreach (int count in new[] { 2, 65, 257, 512, 1024 }) GpuKernel(kind, count);
                GpuSelection(kind);
            }
            GpuAppliedGravity(); GpuAppliedCharges(); GpuAppliedLargeBatch(); GpuOwnerDestruction();
            var registered = (System.Collections.ICollection)typeof(GpuPhysicsForces).GetField("live", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            Check(registered.Count == 0, "destroying all managers leaves no retained solver references");
        }
        catch (Exception error) { Check(false, error.ToString()); }
        finally { GpuPhysicsForces.Configure(false); }
        Rows.Add("FAILURES: " + failures); File.WriteAllLines(reportPath, Rows);
        UnityEngine.Debug.Log("GPU force audit: " + failures + " failures");
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    private static void GpuSettings()
    {
        var defaults = new StarSeaSettings();
        Check(!defaults.GpuPhysicsCompute, "GPU setting defaults off for existing saves");
        var enabledSettings = new StarSeaSettings { GpuPhysicsCompute = true };
        defaults.Update(enabledSettings);
        Check(defaults.GpuPhysicsCompute, "settings copy preserves GPU option");
        var restored = Innovation.Json.Deserialize<StarSeaSettings>(Innovation.Json.Serialize(defaults));
        Check(restored.GpuPhysicsCompute && !Innovation.Json.Deserialize<StarSeaSettings>("{}").GpuPhysicsCompute,
            "new setting round-trips and old settings without the field remain off");
        enabledSettings.Apply();
        Check(!GpuPhysicsForces.Describe().Contains("关闭"), "applying enabled settings actually activates GPU calculation policy");
        defaults.Update(new StarSeaSettings());
        defaults.Apply();
        Check(!defaults.GpuPhysicsCompute, "settings reset restores GPU default");
        Check(GpuPhysicsForces.Describe().Contains("关闭"), "applying reset settings deactivates GPU calculation policy");
        var property = typeof(StarSeaSettings).GetProperty("GpuPhysicsCompute");
        Check(property.CanRead && property.CanWrite && property.PropertyType == typeof(bool), "GPU toggle supports settings UI reflection binding");
        var locales = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText("Assets/TextAsset/INLocalizationData.json"));
        var text = locales["data"].First(t => (string)t["id"] == "StarSeaSettings_GpuPhysicsCompute")["text"];
        Check(!string.IsNullOrEmpty((string)text["zh-Hans"]) && !string.IsNullOrEmpty((string)text["en-US"]), "GPU option has Chinese and English labels");
        string source = File.ReadAllText("Assets/Scripts/INSettingsInterface.cs");
        int at = source.IndexOf("m_onlyGroupName == OptimizationGroupName", StringComparison.Ordinal);
        Check(at >= 0 && source.Substring(at, Math.Min(1400, source.Length - at)).Contains("\"GpuPhysicsCompute\""),
            "optimization settings page lists the actual GPU property in its physics group");
        Check(HeatUiOptionHelp.Description("StarSeaSettings_GpuPhysicsCompute", "GPU").Contains("CPU"),
            "settings row help explains the supported forces and CPU fallback");
        GpuPhysicsForces.Configure(true);
    }

    private static void GpuFill(GpuPhysicsForces solver, int count)
    {
        solver.Reserve(count);
        var random = new System.Random(314159);
        for (int i = 0; i < count; i++)
            solver.Inputs[i] = new GpuPhysicsForces.Body {
                state = new Vector4((float)(random.NextDouble() * 80 - 40), (float)(random.NextDouble() * 80 - 40),
                    1f + i % 7, i % 2 == 0 ? 3f : -5f),
                component = i % 3, flags = i % 17 == 0 ? GpuPhysicsForces.Kinematic : i % 23 == 0 ? GpuPhysicsForces.SpecialCharge : 0
            };
        if (count > 64)
        {
            // Near-distance clamp, strict 64-unit cutoff, zero charges, coincident positions and tail threads.
            solver.Inputs[1].state = new Vector4(0, 0, 2, 0);
            solver.Inputs[2].state = new Vector4(0, 0, 3, -5);
            solver.Inputs[3].state = new Vector4(0.1f, 0.1f, 4, 3);
            solver.Inputs[64].state = new Vector4(64, 0, 5, 3);
        }
    }

    private static void GpuEdgeCases()
    {
        foreach (var kind in new[] { GpuPhysicsForces.ForceKind.Gravity, GpuPhysicsForces.ForceKind.PointCharge })
            using (var solver = new GpuPhysicsForces(kind))
            {
                solver.Reserve(2);
                foreach (float distance in new[] { 0f, 0.1f, 1f, 63.999f, 64f, 64.001f })
                    foreach (int flags in new[] { 0, GpuPhysicsForces.Kinematic, GpuPhysicsForces.SpecialCharge })
                    {
                        solver.Inputs[0] = new GpuPhysicsForces.Body { state = new Vector4(0, 0, 2, 3), component = 1 };
                        solver.Inputs[1] = new GpuPhysicsForces.Body { state = new Vector4(distance, 0, 3, -5), component = 2, flags = flags };
                        var expected = GpuOracle(kind, solver.Inputs, 2, 0.7f, 1f);
                        Check(solver.TryGpu(2, 0.7f, 1f, out var result) && GpuPhysicsForces.Equivalent(expected, result, 2),
                            kind + " distance/clamp/cutoff/source edge d=" + distance + " flags=" + flags);
                    }
                if (kind == GpuPhysicsForces.ForceKind.Gravity)
                {
                    solver.Inputs[0] = new GpuPhysicsForces.Body { state = new Vector4(0, 0, 1, 0), component = 1 };
                    solver.Inputs[1] = new GpuPhysicsForces.Body { state = new Vector4(1, 0, 1, 0), component = 2 };
                    foreach (float strength in new[] { 0.009f, 0.011f })
                    {
                        solver.TryGpu(2, strength, 0, out var result);
                        Check(Mathf.Abs(result[0].x - (strength < 0.01f ? 0f : strength)) < 0.000001f,
                            "gravity pair threshold magnitude=" + strength);
                    }
                    solver.Inputs[1].component = 1; solver.TryGpu(2, 1, 0, out var isolated);
                    Check(isolated[0] == Vector4.zero && isolated[1] == Vector4.zero, "same-component gravity produces exactly zero force");
                }
            }
    }

    // Independent double-precision, per-destination oracle. No calls into the CPU/GPU implementation.
    private static Vector4[] GpuOracle(GpuPhysicsForces.ForceKind kind, GpuPhysicsForces.Body[] data, int count, float gravity, float attenuation)
    {
        var result = new Vector4[count];
        for (int i = 0; i < count; i++)
        {
            var a = data[i]; if ((a.flags & GpuPhysicsForces.Kinematic) != 0) continue;
            double x = 0, y = 0;
            for (int j = 0; j < count; j++)
            {
                var b = data[j]; if (i == j) continue;
                double dx = (double)b.state.x - a.state.x, dy = (double)b.state.y - a.state.y;
                double square = dx * dx + dy * dy, factor;
                if (kind == GpuPhysicsForces.ForceKind.Gravity)
                {
                    if (a.component == b.component) continue;
                    square = Math.Max(1, square);
                    double strength = gravity * (double)a.state.z * b.state.z / Math.Pow(square, attenuation);
                    if (strength < 0.01) continue;
                    factor = strength / Math.Sqrt(square);
                }
                else
                {
                    if (((a.flags | b.flags) & GpuPhysicsForces.SpecialCharge) != 0 || square >= 4096) continue;
                    double distance = Math.Max(1, Math.Sqrt(square));
                    factor = -1.5 * a.state.w * b.state.w / (distance * distance * distance);
                }
                x += dx * factor; y += dy * factor;
            }
            result[i] = new Vector4((float)x, (float)y, 0, 0);
        }
        return result;
    }

    private static void GpuKernel(GpuPhysicsForces.ForceKind kind, int count)
    {
        using (var solver = new GpuPhysicsForces(kind))
        {
            GpuFill(solver, count); solver.Prepare(count);
            var cpu = new Vector4[count];
            foreach (float attenuation in kind == GpuPhysicsForces.ForceKind.Gravity ? new[] { 0f, 0.333f, 1f } : new[] { 1f })
            {
                var oracle = GpuOracle(kind, solver.Inputs, count, 0.7f, attenuation);
                GpuPhysicsForces.CalculateCpu(kind, solver.Inputs, cpu, count, 0.7f, attenuation);
                Check(GpuPhysicsForces.Equivalent(oracle, cpu, count), kind + " cached CPU / independent oracle N=" + count + " decay=" + attenuation);
                bool ok = solver.TryGpu(count, 0.7f, attenuation, out var gpu);
                Check(ok && GpuPhysicsForces.Equivalent(oracle, gpu, count), kind + " actual GPU dispatch / oracle N=" + count + " decay=" + attenuation);
            }
            if (count >= 257)
            {
                const int samples = 8;
                var cpuTimes = new double[samples]; var gpuTimes = new double[samples];
                for (int run = 0; run < samples; run++)
                {
                    long start = Stopwatch.GetTimestamp();
                    GpuPhysicsForces.CalculateCpu(kind, solver.Inputs, cpu, count, 0.7f, 0.333f);
                    cpuTimes[run] = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
                    solver.TryGpu(count, 0.7f, 0.333f, out _); gpuTimes[run] = solver.LastMilliseconds;
                }
                Array.Sort(cpuTimes); Array.Sort(gpuTimes);
                Rows.Add("TIMING " + kind + " N=" + count + " CPU median=" + cpuTimes[4].ToString("F3") +
                    "ms GPU upload+dispatch+readback median=" + gpuTimes[4].ToString("F3") + "ms");
                // Warmed wrappers, buffers and arrays; no caller allocations inside the measured loops.
                solver.TryGpu(count, 0.7f, 0.333f, out _);
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int run = 0; run < 8; run++) solver.TryGpu(count, 0.7f, 0.333f, out _);
                long gpuBytes = GC.GetAllocatedBytesForCurrentThread() - before;
                before = GC.GetAllocatedBytesForCurrentThread();
                for (int run = 0; run < 8; run++) GpuPhysicsForces.CalculateCpu(kind, solver.Inputs, cpu, count, 0.7f, 0.333f);
                long cpuBytes = GC.GetAllocatedBytesForCurrentThread() - before;
                Check(cpuBytes == 0 && gpuBytes == 0, kind + " steady-state 8 steps managed allocations CPU=" + cpuBytes + " GPU=" + gpuBytes);
            }
        }
    }

    private static void GpuSelection(GpuPhysicsForces.ForceKind kind)
    {
        using (var solver = new GpuPhysicsForces(kind))
        {
            GpuFill(solver, 65); solver.Prepare(65);
            solver.Evaluate(65, 0.7f, 0.333f);
            Check(solver.GpuDispatches == 0 && !solver.HasGpuBuffers, kind + " small vehicles avoid GPU initialization");
            GpuFill(solver, 257); solver.Prepare(257);
            var inputs = solver.Inputs;
            var expected = GpuOracle(kind, inputs, 257, 0.7f, 0.333f);
            Check(GpuPhysicsForces.Equivalent(expected, solver.Evaluate(257, 0.7f, 0.333f), 257), kind + " automatic selection applies one valid same-step result");
            Rows.Add("SELECTION " + kind + " " + solver.Status);
            bool noSlowerBuffer = solver.Status == "GPU" || !solver.HasGpuBuffers;
            Check(noSlowerBuffer, kind + " losing GPU path releases buffers");
            for (int i = 0; i < 4; i++) solver.Evaluate(257, 0.7f, 0.333f);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 8; i++) solver.Evaluate(257, 0.7f, 0.333f);
            Check(GC.GetAllocatedBytesForCurrentThread() == before && ReferenceEquals(inputs, solver.Inputs), kind + " selected path reuses buffers without managed garbage");
            GpuPhysicsForces.Configure(false);
            long dispatches = solver.GpuDispatches;
            solver.Evaluate(257, 0.7f, 0.333f);
            Check(!solver.HasGpuBuffers && dispatches == solver.GpuDispatches, kind + " disabling immediately releases GPU and uses CPU");
            GpuPhysicsForces.Configure(true);
            solver.Evaluate(257, 0.7f, 0.333f);
            Check(solver.GpuDispatches > dispatches, kind + " reenabling retries device selection");
            // Deterministic driver-stall simulation: the measured CPU baseline is below any dispatch cost.
            Field(solver, "calibrated", true); Field(solver, "preferGpu", true);
            Field(solver, "baselineCpuMs", 0d); Field(solver, "measuredCount", 257); Field(solver, "slowSteps", 0);
            for (int i = 0; i < 4; i++) solver.Evaluate(257, 0.7f, 0.333f);
            dispatches = solver.GpuDispatches;
            var fallback = solver.Evaluate(257, 0.7f, 0.333f);
            Check(!solver.HasGpuBuffers && dispatches == solver.GpuDispatches && GpuPhysicsForces.Equivalent(expected, fallback, 257),
                kind + " sustained GPU wait falls back to CPU without applying a stale or duplicate force");
            GpuPhysicsForces.Configure(false); GpuPhysicsForces.Configure(true);
            Field(solver, "unavailable", true);
            fallback = solver.Evaluate(257, 0.7f, 0.333f);
            Check(!solver.HasGpuBuffers && dispatches == solver.GpuDispatches && GpuPhysicsForces.Equivalent(expected, fallback, 257),
                kind + " unavailable device uses correct CPU forces without retrying every step");
            GpuPhysicsForces.Configure(false); GpuPhysicsForces.Configure(true);
            GpuFill(solver, 1025);
            expected = GpuOracle(kind, solver.Inputs, 1025, 0.7f, 0.333f);
            Check(GpuPhysicsForces.Equivalent(expected, solver.Evaluate(1025, 0.7f, 0.333f), 1025), kind + " growth recreates buffers and validates partial groups");
            solver.Dispose(); solver.Dispose();
            Check(!solver.HasGpuBuffers, kind + " disposal is idempotent and releases GPU resources");
        }
    }

    private static BasePart GpuPart(bool charge, Vector3 position, int component, bool fixedBody = false)
    {
        var go = Inactive("GPU force application fixture");
        go.transform.position = position;
        var part = charge ? (BasePart)go.AddComponent<PointChargePart>() : go.AddComponent<BasePart>();
        part.ConnectedComponent = component; part.contraption = owner;
        part.EnsureRigidbody(); part.rigidbody.useGravity = false; part.rigidbody.drag = 0;
        part.rigidbody.mass = 2; part.rigidbody.isKinematic = fixedBody;
        if (charge) Field(part, "m_enabled", true);
        owner.Parts.Add(part);
        // Keep script callbacks dormant; activate just the rigidbody on a separate sibling object.
        // BasePart's cached rigidbody lets this fixture exercise the real manager without artwork.
        var physical = new GameObject("GPU fixture rigidbody"); physical.transform.position = position;
        var body = physical.AddComponent<Rigidbody>(); body.mass = 2; body.useGravity = false;
        body.drag = 0; body.isKinematic = fixedBody; body.constraints = RigidbodyConstraints.FreezePositionZ;
        part.rigidbody = body;
        return part;
    }

    private static void GpuRemove(BasePart part)
    {
        Object.DestroyImmediate(part.rigidbody.gameObject); owner.Parts.Remove(part); Object.DestroyImmediate(part.gameObject);
    }

    private static void GpuAppliedGravity()
    {
        Static(typeof(Contraption), "Instance", owner);
        var settings = INUserSettings.Instance.StarSeaSettings;
        settings.UniversalGravity = true; settings.UniversalGravityConstant = 1; settings.UniversalGravityAttenuation = 0;
        var a = GpuPart(false, new Vector3(100, 100, 0), 1);
        var b = GpuPart(false, new Vector3(102, 100, 0), 2, true);
        var c = GpuPart(false, new Vector3(101, 100, 0), 1);
        foreach (bool enabled in new[] { false, true })
        {
            settings.GpuPhysicsCompute = enabled;
            var manager = new UniversalGravityManager(); manager.Start();
            a.rigidbody.velocity = c.rigidbody.velocity = Vector3.zero;
            manager.FixedUpdate(); Physics.Simulate(0.02f);
            Check(Mathf.Abs(a.rigidbody.velocity.x - 0.04f) < 0.00001f && Mathf.Abs(c.rigidbody.velocity.x - 0.04f) < 0.00001f &&
                b.rigidbody.velocity == Vector3.zero, "gravity manager same-component isolation / kinematic source / single force application enabled=" + enabled);
            manager.OnDestroy();
        }
        GpuRemove(a); GpuRemove(b); GpuRemove(c); settings.UniversalGravity = false;
    }

    private static void GpuAppliedCharges()
    {
        var a = (PointChargePart)GpuPart(true, new Vector3(200, 100, 0), 1);
        var b = (PointChargePart)GpuPart(true, new Vector3(202, 100, 0), 2);
        b.m_gridRotation = BasePart.GridRotation.Deg_180;
        var off = (PointChargePart)GpuPart(true, new Vector3(201, 100, 0), 3); Field(off, "m_enabled", false);
        var parts = new List<ElectricalPart> { a, b, off };
        foreach (string method in new[] { "UpdatePointCharges", "UpdatePointChargesOptimized" })
            foreach (bool enabled in new[] { false, true })
            {
                INUserSettings.Instance.StarSeaSettings.GpuPhysicsCompute = enabled; GpuPhysicsForces.Configure(enabled);
                var system = new ElectricalSystem(); Field(system, "m_electricalParts", parts);
                a.rigidbody.velocity = b.rigidbody.velocity = off.rigidbody.velocity = Vector3.zero;
                Call(system, method); Physics.Simulate(0.02f);
                Check(Mathf.Abs(a.rigidbody.velocity.x - 1.5f) < 0.0001f && Mathf.Abs(b.rigidbody.velocity.x + 1.5f) < 0.0001f &&
                    off.rigidbody.velocity == Vector3.zero, method + " enabled=" + enabled + " opposite charges attract once and disabled charges exert no force");
                system.OnDestroy();
            }
        GpuRemove(a); GpuRemove(b); GpuRemove(off);
    }

    private static void GpuAppliedLargeBatch()
    {
        const int count = 257;
        var settings = INUserSettings.Instance.StarSeaSettings;
        settings.GpuPhysicsCompute = true; GpuPhysicsForces.Configure(true);
        settings.UniversalGravity = true; settings.UniversalGravityConstant = 0.7f; settings.UniversalGravityAttenuation = 0.333f;
        foreach (bool charges in new[] { false, true })
        {
            var kind = charges ? GpuPhysicsForces.ForceKind.PointCharge : GpuPhysicsForces.ForceKind.Gravity;
            var parts = new List<BasePart>(); var inputs = new GpuPhysicsForces.Body[count];
            for (int i = 0; i < count; i++)
            {
                var part = GpuPart(charges, new Vector3(300 + i % 17 * 0.8f, 100 + i / 17 * 0.8f, 0), i % 3, i == 0);
                parts.Add(part);
                if (charges) part.m_gridRotation = i % 2 == 0 ? BasePart.GridRotation.Deg_0 : BasePart.GridRotation.Deg_180;
                inputs[i] = new GpuPhysicsForces.Body { state = new Vector4(part.transform.position.x, part.transform.position.y, 2, charges ? ((PointChargePart)part).Charge : 0),
                    component = i % 3, flags = i == 0 ? GpuPhysicsForces.Kinematic : 0 };
            }
            var expected = GpuOracle(kind, inputs, count, 0.7f, 0.333f);
            PartManager manager;
            GpuPhysicsForces solver;
            if (charges)
            {
                var system = new ElectricalSystem(); manager = system;
                Field(system, "m_electricalParts", parts.Cast<ElectricalPart>().ToList());
                Call(system, "UpdatePointChargesOptimized");
                solver = (GpuPhysicsForces)typeof(ElectricalSystem).GetField("m_chargeForces", Private).GetValue(system);
            }
            else
            {
                var gravity = new UniversalGravityManager(); manager = gravity; gravity.Start(); gravity.FixedUpdate();
                solver = (GpuPhysicsForces)typeof(UniversalGravityManager).GetField("forces", Private).GetValue(gravity);
            }
            // Flush calibration forces, then test a guaranteed real GPU step independently of device selection.
            Physics.Simulate(0.02f);
            foreach (var part in parts) if (!part.rigidbody.isKinematic) part.rigidbody.velocity = Vector3.zero;
            Field(solver, "calibrated", true); Field(solver, "preferGpu", true);
            Field(solver, "baselineCpuMs", 1000000d); Field(solver, "measuredCount", count);
            long dispatches = solver.GpuDispatches;
            if (charges) Call(manager, "UpdatePointChargesOptimized"); else manager.FixedUpdate();
            Physics.Simulate(0.02f);
            var velocities = parts.Select(p => new Vector4(p.rigidbody.velocity.x * 100, p.rigidbody.velocity.y * 100, 0, 0)).ToArray();
            Check(solver.GpuDispatches == dispatches + 1 && GpuPhysicsForces.Equivalent(expected, velocities, count),
                kind + " 257 actual rigidbodies receive current GPU batch once before the same PhysX step");
            manager.OnDestroy(); foreach (var part in parts) GpuRemove(part);
        }
        settings.UniversalGravity = false;
    }

    private static void GpuOwnerDestruction()
    {
        foreach (bool enabled in new[] { false, true })
        {
            var go = Inactive("GPU owner lifecycle fixture"); var controller = go.AddComponent<INContraption>();
            var gravity = new UniversalGravityManager(); var electrical = new ElectricalSystem();
            var solvers = new[] {
                (GpuPhysicsForces)typeof(UniversalGravityManager).GetField("forces", Private).GetValue(gravity),
                (GpuPhysicsForces)typeof(ElectricalSystem).GetField("m_chargeForces", Private).GetValue(electrical)
            };
            GpuPhysicsForces.Configure(true);
            foreach (var solver in solvers) solver.Prepare(257);
            Check(solvers.All(s => s.HasGpuBuffers), "lifecycle fixture has real GPU buffers before owner destruction enabled=" + enabled);
            Field(controller, "m_enabled", enabled); Field(controller, "m_status", INBehaviour.StatusCode.Building);
            Field(controller, "m_behaviours", new List<INBehaviour> { gravity, electrical });
            Call(controller, "OnDestroy"); Call(controller, "OnDestroy");
            Check(solvers.All(s => !s.HasGpuBuffers), "owner destruction releases running GPU managers while status is building, independent of enabled state=" + enabled);
            Object.DestroyImmediate(go);
        }
    }
}
