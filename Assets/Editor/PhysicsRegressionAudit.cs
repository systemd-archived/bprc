using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Debug = UnityEngine.Debug;

// Run through Tools/run-physics-audit.ps1 in a disposable project, never over an open level.
public static class PhysicsRegressionAudit
{
    private static readonly List<string> rows = new List<string>();
    private static readonly List<GameObject> objects = new List<GameObject>();
    private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
    private static int failures;

    private sealed class ProbeBackend : IPhysicsBackend
    {
        public int refilters;
        public PhysicsEngine Engine => PhysicsEngine.PhysX;
        public string DisplayName => "Audit probe";
        public void Synchronize() { }
        public void Step(float delta) { }
        public void Wake(Rigidbody body) { }
        public void Sleep(Rigidbody body) { }
        public bool IsSleeping(Rigidbody body) => false;
        public void Refilter(Collider a, Collider b) { refilters++; }
        public float HingeAngle(HingeJoint joint) => 0f;
        public void Dispose() { }
    }

    private static void Check(bool condition, string label)
    {
        rows.Add((condition ? "PASS " : "FAIL ") + label);
        if (!condition) failures++;
    }

    private static Rigidbody Cube(string name, Vector3 position)
    {
        var obj = new GameObject(name);
        objects.Add(obj);
        obj.transform.position = position;
        obj.AddComponent<BoxCollider>();
        return obj.AddComponent<Rigidbody>();
    }

    private static void Clear()
    {
        GamePhysics.Shutdown();
        foreach (var obj in objects) if (obj) Object.DestroyImmediate(obj);
        objects.Clear();
    }

    private static void RunCase(string name, Action action)
    {
        try { action(); }
        catch (Exception error) { Check(false, name + ": " + error); }
        finally { Clear(); }
    }

    public static void Run()
    {
        // This harness advances the entire physics world. The runner supplies an explicit marker so
        // accidentally invoking it in the main project cannot change the user's level or simulation.
        if (!Application.isBatchMode || !File.Exists(".bprc-physics-audit"))
            throw new InvalidOperationException("Use Tools/run-physics-audit.ps1 to run in an isolated project.");
        rows.Clear(); failures = 0;
        rows.Add("Unity " + Application.unityVersion + "; " + SystemInfo.processorType);
        bool auto = PhysicsSimulation.Auto;
        float timestep = Time.fixedDeltaTime;
        Vector3 gravity = Physics.gravity;
        try
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects()) Object.DestroyImmediate(root);
            PhysicsSimulation.Auto = false;
            Time.fixedDeltaTime = 0.02f;
            Physics.gravity = new Vector3(0f, -9.81f, 0f);
            RunCase("collision filtering", Filtering);
            RunCase("force queue", ForceQueue);
            RunCase("layer collision masks", LayerMasks);
            RunCase("anti-tunnel", AntiTunnel);
            RunCase("destroyed freezer bodies", Freezer);
            RunCase("joint manager teardown", () => { new JointManager().OnDestroy(); Check(true, "joint teardown without a physics manager"); });
            RunCase("PhysX", () => Backend(PhysicsEngine.PhysX));
            RunCase("Box3D", () => Backend(PhysicsEngine.Box3D));
            RunCase("Bepu", () => Backend(PhysicsEngine.Bepu));
            if (JoltPhysicsBackend.IsPluginUsable(out string reason))
                RunCase("Jolt", () => Backend(PhysicsEngine.Jolt));
            else rows.Add("SKIP Jolt: " + reason);
            RunCase("scene search", Search);
        }
        finally
        {
            Clear();
            PhysicsSimulation.Auto = auto;
            Time.fixedDeltaTime = timestep;
            Physics.gravity = gravity;
            rows.Add("Failures: " + failures);
            Directory.CreateDirectory("Builds/QA");
            File.WriteAllLines("Builds/QA/physics-regression-audit.txt", rows);
        }
        if (failures != 0) throw new Exception("Physics regression failures: " + failures);
        Debug.Log("PHYSICS_REGRESSION_AUDIT_PASS");
    }

    private static void Filtering()
    {
        var a = Cube("filter A", Vector3.zero).GetComponent<Collider>();
        var b = Cube("filter B", Vector3.right * 3f).GetComponent<Collider>();
        var probe = new ProbeBackend();
        typeof(GamePhysics).GetField("backend", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, probe);
        for (int i = 0; i < 20; i++) GamePhysics.IgnoreCollision(a, b);
        Check(probe.refilters == 1, "20 repeated ignore requests invalidate native contacts once (actual " + probe.refilters + ")");
        Check(GamePhysics.IsIgnoredPair(b, a) && Physics.GetIgnoreCollision(a, b), "ignore state agrees with Unity and is symmetric");
        Physics.IgnoreCollision(a, b, false);
        GamePhysics.IgnoreCollision(a, b);
        Check(Physics.GetIgnoreCollision(a, b) && probe.refilters == 1, "restores Unity's lost flag without waking native bodies");
        for (int i = 0; i < 20; i++) GamePhysics.IgnoreCollision(a, b, false);
        Check(probe.refilters == 2 && !GamePhysics.IsIgnoredPair(a, b), "restoring collisions invalidates once");
        GamePhysics.IgnoreCollision(null, b);
        Check(probe.refilters == 2, "null colliders do not reach the backend");
    }

    private static void ForceQueue()
    {
        var body = Cube("force queue", Vector3.zero);
        body.useGravity = false; body.mass = 2f;
        var probe = new ProbeBackend();
        typeof(GamePhysics).GetField("backend", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, probe);
        body.EngineAddForce(Vector3.right * 2f, ForceMode.Impulse);
        GamePhysics.Simulate(0.02f);
        Check(Mathf.Abs(body.velocity.x - 1f) < 0.001f, "native force queue converts impulse by mass");
        GamePhysics.Simulate(0.02f);
        Check(Mathf.Abs(body.velocity.x - 1f) < 0.001f, "queued impulse is consumed exactly once");
    }

    private static void AntiTunnel()
    {
        var body = Cube("tunnelling body", new Vector3(100f, 500f, 0f));
        body.useGravity = false; body.velocity = Vector3.right * 100f;
        // Create far-to-near: accepting the first query result must not decide which obstacle wins.
        for (int i = 97; i >= 1; i--)
        {
            var obstacle = Cube("obstacle " + i, new Vector3(i, 500f, 0f));
            obstacle.isKinematic = true;
            obstacle.GetComponent<BoxCollider>().size = Vector3.one * 0.2f;
        }
        var helper = body.gameObject.AddComponent<INContraption>();
        typeof(INContraption).GetField("m_componentListMap", PrivateInstance).SetValue(helper,
            new Dictionary<Type, INContraption.ComponentData> {
                { typeof(Rigidbody), new INContraption.ComponentData(new[] { body }, float.PositiveInfinity) }
            });
        typeof(INContraption).GetField("m_previousPositionMap", PrivateInstance).SetValue(helper,
            new Dictionary<Rigidbody, Vector3> { { body, new Vector3(0f, 500f, 0f) } });
        Physics.SyncTransforms();
        var method = typeof(INContraption).GetMethod("GetTunnelHits", PrivateInstance);
        int count = (int)method.Invoke(helper, new object[] { new Vector3(0f, 500f, 0f), Vector3.right, 100f });
        // The moving body's own collider is also on this ray; query results must include it and
        // AntiTunnelCheck must reject it separately.
        Check(count == 98, "non-alloc query retains 97 obstacles and self after buffer overflow (actual " + count + ")");
        var hits = (RaycastHit[])typeof(INContraption).GetField("m_tunnelHits", PrivateInstance).GetValue(helper);
        method.Invoke(helper, new object[] { new Vector3(0f, 500f, 0f), Vector3.right, 100f });
        Check(ReferenceEquals(hits, typeof(INContraption).GetField("m_tunnelHits", PrivateInstance).GetValue(helper)), "raycast buffer is reused after growth");
        typeof(INContraption).GetMethod("AntiTunnelCheck", PrivateInstance).Invoke(helper, null);
        Check(Mathf.Abs(body.position.x - 0.89f) < 0.005f, "anti-tunnel chooses nearest valid obstacle, x=" + body.position.x);
    }

    private static void LayerMasks()
    {
        var masks = new ulong[32];
        var read = typeof(GamePhysics).GetMethod("ReadLayerCollisionMasks", BindingFlags.NonPublic | BindingFlags.Static);
        bool original = Physics.GetIgnoreLayerCollision(30, 31);
        try
        {
            read.Invoke(null, new object[] { masks });
            bool same = true;
            for (int a = 0; a < 32; a++)
            for (int b = 0; b < 32; b++)
                same &= ((masks[a] & (1UL << b)) != 0) == !Physics.GetIgnoreLayerCollision(a, b);
            Check(same, "layer snapshot matches all 1024 legacy matrix entries");
            Physics.IgnoreLayerCollision(30, 31, !original);
            read.Invoke(null, new object[] { masks });
            Check(((masks[30] & (1UL << 31)) != 0) == original &&
                ((masks[31] & (1UL << 30)) != 0) == original, "live layer changes appear in both symmetric mask entries");
        }
        finally { Physics.IgnoreLayerCollision(30, 31, original); }
    }

    private static void Freezer()
    {
        var parent = new GameObject("freezer"); objects.Add(parent);
        var body = Cube("freezer survivor", Vector3.zero);
        var destroyed = Cube("freezer destroyed", Vector3.right * 3f);
        var freezer = parent.AddComponent<RigidbodyFreezer>();
        typeof(RigidbodyFreezer).GetField("rigidbodies", PrivateInstance).SetValue(freezer, new[] { destroyed, body });
        typeof(RigidbodyFreezer).GetField("origConstraints", PrivateInstance).SetValue(freezer,
            new[] { RigidbodyConstraints.None, RigidbodyConstraints.FreezePositionZ });
        Object.DestroyImmediate(destroyed.gameObject);
        typeof(RigidbodyFreezer).GetMethod("Unfreeze", PrivateInstance).Invoke(freezer, null);
        Check(body.constraints == RigidbodyConstraints.FreezePositionZ, "unfreeze survives a destroyed child and restores remaining constraints");
    }

    private static void Backend(PhysicsEngine engine)
    {
        var ground = Cube("ground", new Vector3(0f, -0.5f, 0f));
        ground.isKinematic = true; ground.transform.localScale = new Vector3(20f, 1f, 20f);
        var body = Cube("faller", new Vector3(0f, 3f, 0f));
        Physics.SyncTransforms();
        Check(GamePhysics.SelectEngine(engine), engine + " starts");
        if (GamePhysics.CurrentEngine != engine) return;
        for (int i = 0; i < 180; i++) Step(engine);
        Check(GamePhysics.CurrentEngine == engine && Mathf.Abs(body.position.y - 0.5f) < 0.15f,
            engine + " falls and settles without fallback, y=" + body.position.y);

        // Test an isolated zero-gravity body so contacts cannot legitimately wake it.
        var sleeper = Cube("sleeper", new Vector3(30f, 3f, 0f));
        sleeper.useGravity = false;
        sleeper.rotation = Quaternion.Euler(17f, 29f, 31f);
        Step(engine);
        Check(Mathf.Abs(sleeper.velocity.y) < 0.001f, engine + " respects useGravity=false");
        sleeper.EngineSleep();
        for (int i = 0; i < 8; i++) Step(engine);
        Check(sleeper.EngineIsSleeping(), engine + " explicit sleep survives 8 steps");
        sleeper.EngineAddForce(Vector3.right * 2f, ForceMode.Impulse);
        Step(engine);
        Check(!sleeper.EngineIsSleeping() && sleeper.velocity.x > 1.9f, engine + " impulse wakes sleeping body");

        var extraCollider = sleeper.gameObject.AddComponent<SphereCollider>();
        extraCollider.radius = 0.2f;
        Step(engine);
        Check(sleeper.velocity.x > 1.9f, engine + " preserves velocity while rebuilding a compound collider");
        Object.DestroyImmediate(extraCollider);
        Step(engine);
        Check(sleeper.velocity.x > 1.9f, engine + " preserves velocity when removing a compound child");

        Vector3 originalGravity = Physics.gravity;
        try
        {
            Physics.gravity = new Vector3(0f, -4f, 0f);
            sleeper.useGravity = true; sleeper.velocity = Vector3.zero;
            Step(engine);
            Check(Mathf.Abs(sleeper.velocity.y + 0.08f) < 0.005f, engine + " reads live global gravity and per-body gravity toggle");
            sleeper.useGravity = false; sleeper.drag = 2f; sleeper.velocity = Vector3.right * 2f;
            Step(engine);
            Check(sleeper.velocity.x < 1.99f && sleeper.velocity.x > 1.7f, engine + " applies linear drag");
        }
        finally { Physics.gravity = originalGravity; }

        var ghost = Cube("ghost", new Vector3(6f, 4f, 0f));
        GamePhysics.IgnoreCollision(ghost.GetComponent<Collider>(), ground.GetComponent<Collider>());
        for (int i = 0; i < 80; i++) Step(engine);
        Check(ghost.position.y < -1f, engine + " ignored body passes through ground");
        GamePhysics.IgnoreCollision(ghost.GetComponent<Collider>(), ground.GetComponent<Collider>(), false);
        var added = Cube("late spawned", new Vector3(-6f, 3f, 0f));
        for (int i = 0; i < 180; i++) Step(engine);
        Check(Mathf.Abs(added.position.y - 0.5f) < 0.15f, engine + " discovers runtime-spawned bodies immediately");
        Object.DestroyImmediate(added.gameObject);
        Step(engine);
        Check(GamePhysics.CurrentEngine == engine, engine + " retires destroyed bodies without fallback");

        bool originalLayerRule = Physics.GetIgnoreLayerCollision(29, 30);
        try
        {
            Physics.IgnoreLayerCollision(29, 30, true);
            var layerGround = Cube("layer ground", new Vector3(60f, -0.5f, 0f));
            layerGround.isKinematic = true; layerGround.gameObject.layer = 29;
            layerGround.transform.localScale = new Vector3(10f, 1f, 10f);
            var layerBody = Cube("layer body", new Vector3(60f, 3f, 0f));
            layerBody.gameObject.layer = 30;
            for (int i = 0; i < 80; i++) Step(engine);
            Check(layerBody.position.y < -1f, engine + " accepts runtime layer collision suppression");
            Physics.IgnoreLayerCollision(29, 30, false);
            layerBody.position = new Vector3(60f, 3f, 0f); layerBody.velocity = Vector3.zero;
            layerBody.EngineWakeUp();
            for (int i = 0; i < 180; i++) Step(engine);
            Check(Mathf.Abs(layerBody.position.y - 0.5f) < 0.15f, engine + " accepts runtime layer collision restoration");
        }
        finally { Physics.IgnoreLayerCollision(29, 30, originalLayerRule); }
    }

    private static void Search()
    {
        for (int i = 0; i < 2000; i++)
        {
            var obj = new GameObject("search " + i); objects.Add(obj);
            obj.AddComponent<Rigidbody>();
        }
        var inactive = new GameObject("inactive"); objects.Add(inactive);
        inactive.AddComponent<Rigidbody>(); inactive.SetActive(false);
        var sorted = Object.FindObjectsOfType<Rigidbody>();
        var unsorted = GamePhysics.FindSceneObjects<Rigidbody>();
        var ids = new HashSet<int>(); foreach (var rb in sorted) ids.Add(rb.GetInstanceID());
        bool same = sorted.Length == unsorted.Length;
        foreach (var rb in unsorted) same &= ids.Remove(rb.GetInstanceID());
        Check(same && ids.Count == 0 && unsorted.Length == 2000, "unsorted scene discovery preserves population and inactive exclusion");
        for (int i = 0; i < 5; i++) { Object.FindObjectsOfType<Rigidbody>(); GamePhysics.FindSceneObjects<Rigidbody>(); }
        // Alternate to reduce drift; report measured search cost rather than claiming an FPS gain.
        long sortedTicks = 0, unsortedTicks = 0;
        for (int i = 0; i < 80; i++)
        {
            long start = Stopwatch.GetTimestamp(); Object.FindObjectsOfType<Rigidbody>();
            sortedTicks += Stopwatch.GetTimestamp() - start;
            start = Stopwatch.GetTimestamp(); GamePhysics.FindSceneObjects<Rigidbody>();
            unsortedTicks += Stopwatch.GetTimestamp() - start;
        }
        double scale = 1000.0 / Stopwatch.Frequency / 80;
        rows.Add("MEASURE search 2000 bodies, 80 samples: sorted=" + (sortedTicks * scale).ToString("F4") +
            " ms/query; unsorted=" + (unsortedTicks * scale).ToString("F4") + " ms/query");
    }

    private static void Step(PhysicsEngine engine)
    {
        GamePhysics.Simulate(0.02f);
        if (GamePhysics.CurrentEngine != engine)
            throw new InvalidOperationException(engine + " fell back during audit: " + GamePhysics.LastError);
    }
}
