using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;
using UnityEngine.SceneManagement;

/// <summary>
/// Front-end for the switchable rigid-body simulation backend.
/// </summary>
/// <remarks>
/// <para>
/// The game itself keeps talking to the ordinary Unity components. This facade mirrors that state into
/// whichever native engine is selected and exposes small <c>Engine*</c> extensions that route
/// force/velocity calls to the active backend.
/// </para>
/// <para>
/// <see cref="PhysicsEngine.PhysX"/> is not a backend object: it is the absence of one. When no
/// backend is installed <see cref="Simulate"/> simply calls <see cref="Physics.Simulate"/>.
/// </para>
/// </remarks>
public static class GamePhysics
{
    private struct Forces { public Vector3 linear, angular; }

    private static readonly Dictionary<Rigidbody, Forces> Pending = new Dictionary<Rigidbody, Forces>();

    /// <summary>
    /// Collider pairs that must not interact. Kept here rather than in a backend so the filter
    /// survives an engine switch.
    /// </summary>
    internal static readonly HashSet<ulong> IgnoredPairs = new HashSet<ulong>();

    private static IPhysicsBackend backend;
    private static PhysicsEngine current = PhysicsEngine.PhysX;
    private static PhysicsEngine[] cachedOrder;

    private const float SceneObjectCacheLifetime = 0.1f;
    private static int sceneObjectVersion = 1;

    private sealed class SceneObjectCache<T> where T : UnityEngine.Object
    {
        internal static readonly SceneObjectCache<T> Instance = new SceneObjectCache<T>();
        internal T[] Items = new T[0];
        internal float NextRefresh;
        internal int Version = -1;
    }

    static GamePhysics()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => MarkSceneObjectsDirty();

    private static void OnSceneUnloaded(Scene scene) => MarkSceneObjectsDirty();

    /// <summary>
    /// Invalidates the short-lived discovery snapshots used by native backends. Runtime systems that
    /// create or destroy a batch of physics components can call this after the batch completes.
    /// </summary>
    internal static void MarkSceneObjectsDirty()
    {
        unchecked { sceneObjectVersion++; }
    }

    /// <summary>The engine currently advancing the simulation.</summary>
    public static PhysicsEngine CurrentEngine => current;

    /// <summary>True when a native backend (anything other than PhysX) is installed.</summary>
    public static bool IsBox3D => backend != null;

    /// <summary>Name of the installed backend, or <c>"PhysX"</c>.</summary>
    public static string CurrentEngineName => backend != null ? backend.DisplayName : "PhysX";

    /// <summary>
    /// Worker threads a native backend may use for its solve, taken from the 优化设置 page's
    /// 物理线程数 row.
    /// </summary>
    /// <remarks>
    /// Shared here rather than duplicated per backend because the value is a property of the session, not
    /// of an engine. Falls back to the machine's own count when the settings are not loaded yet - batch
    /// audits and early boot both reach a backend constructor before <c>INUserSettings</c> exists.
    /// <see cref="BepuBackend"/> deliberately ignores this: its narrow-phase filter reads a main-thread
    /// collection, so it stays single-threaded.
    /// </remarks>
    public static int PhysicsWorkerCount()
    {
        var settings = INUserSettings.Instance?.StarSeaSettings;
        if (settings != null) return settings.ResolvedPhysicsWorkerCount;
        return Mathf.Max(1, SystemInfo.processorCount - 1);
    }

    /// <summary>Message describing the most recent backend failure, or <c>null</c>.</summary>
    public static string LastError { get; private set; }

    /// <summary><see cref="Time.realtimeSinceStartup"/> at the moment <see cref="LastError"/> was set.</summary>
    public static float LastErrorTime { get; private set; }

    /// <summary>
    /// Smoothed wall-clock cost of one <see cref="Simulate"/> call, in milliseconds.
    /// </summary>
    /// <remarks>
    /// Measured around the whole call, so it covers the native solve <i>and</i> the managed mirror on the
    /// backends that have one - which is the number that matters, because for Box3D/Jolt/Bepu the mirror
    /// is the part the main thread actually pays. This is what the 物理统计 overlay prints; it is the
    /// only runtime measurement the project had none of before.
    /// </remarks>
    public static float StepMilliseconds { get; private set; }

    /// <summary>Fixed steps simulated since load, used by the overlay to prove it is ticking.</summary>
    public static long StepCount { get; private set; }

    private const float StepSmoothing = 0.1f;

    private static readonly ProfilerMarker SimulationMarker = new ProfilerMarker("BPRC.Physics.Simulate");
    private static readonly ProfilerMarker SynchronizeMarker = new ProfilerMarker("BPRC.Physics.Synchronize");
    private static readonly ProfilerMarker ForcesMarker = new ProfilerMarker("BPRC.Physics.FlushForces");
    private static readonly ProfilerMarker BackendStepMarker = new ProfilerMarker("BPRC.Physics.BackendStep");

    // Physics mirrors key entries by instance ID; they do not require the sorted arrays returned by
    // FindObjectsOfType. A short cache removes repeated native enumeration in steady state while still
    // picking up dynamic spawns quickly. Scene transitions invalidate all generic caches immediately.
    public static T[] FindSceneObjects<T>() where T : UnityEngine.Object
    {
        SceneObjectCache<T> cache = SceneObjectCache<T>.Instance;
        float now = Time.realtimeSinceStartup;
        // Editor audits and tooling often create objects immediately before querying. Keep those
        // edit-mode calls exact; the bounded cache is for the player physics loop.
        bool useCache = Application.isPlaying;
        if (useCache && cache.Version == sceneObjectVersion && now < cache.NextRefresh)
            return cache.Items;

#if UNITY_2022_2_OR_NEWER
        cache.Items = UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
        cache.Items = UnityEngine.Object.FindObjectsOfType<T>();
#endif
        if (cache.Items == null) cache.Items = new T[0];
        cache.Version = sceneObjectVersion;
        cache.NextRefresh = useCache ? now + SceneObjectCacheLifetime : now;
        return cache.Items;
    }

    internal static bool ReadLayerCollisionMasks(ulong[] masks)
    {
        // Legacy serialized matrices in this project contain asymmetric entries. Preserve both
        // directions rather than assuming symmetry; report changes for backends with cached filters.
        bool changed = false;
        for (int a = 0; a < 32; a++)
        {
            ulong mask = 0;
            for (int b = 0; b < 32; b++)
                if (!Physics.GetIgnoreLayerCollision(a, b)) mask |= 1UL << b;
            changed |= masks[a] != mask;
            masks[a] = mask;
        }
        return changed;
    }

    /// <summary>Physics step bodies/contacts actually carried, refreshed at a low rate by the overlay.</summary>
    public static int BodyCount { get; private set; }

    /// <summary>Number of enabled colliders in the loaded scenes; refreshed at a low rate.</summary>
    public static int ColliderCount { get; private set; }

    private static float nextPopulationSample;

    /// <summary>
    /// Re-counts the scene's rigid bodies and colliders. Deliberately not called per step: the whole
    /// point of the registry work in <c>docs/MulticoreOptimization.md</c> is that a per-step
    /// <c>FindObjectsOfType</c> is the expensive thing, so the readout samples instead.
    /// </summary>
    public static void SamplePopulation(float interval = 0.5f)
    {
        if (Time.realtimeSinceStartup < nextPopulationSample) return;
        nextPopulationSample = Time.realtimeSinceStartup + interval;
        BodyCount = FindSceneObjects<Rigidbody>().Length;
        ColliderCount = FindSceneObjects<Collider>().Length;
    }

    /// <summary>
    /// Every engine the switcher can offer, in display order. PhysX is always available; the others
    /// report availability so the UI can grey out what this build/platform cannot provide.
    /// </summary>
    public static IReadOnlyList<PhysicsEngineInfo> Engines
    {
        get
        {
            if (cachedOrder == null) cachedOrder = new[] { PhysicsEngine.PhysX, PhysicsEngine.Box3D, PhysicsEngine.Bepu, PhysicsEngine.Havok, PhysicsEngine.Jolt };
            var list = new List<PhysicsEngineInfo>(cachedOrder.Length);
            foreach (var engine in cachedOrder)
            {
                string name, detail, reason;
                bool available;
                switch (engine)
                {
                    case PhysicsEngine.PhysX:
                        name = "PhysX"; detail = "Unity built-in"; reason = null; available = true;
                        break;
                    case PhysicsEngine.Box3D:
                        name = "Box3D";
                        available = Box3DBackend.IsSupported(out reason);
                        detail = available ? "Box3D.Runtime" : reason;
                        break;
                    case PhysicsEngine.Bepu:
                        name = "Bepu";
                        available = BepuBackend.IsSupported(out reason);
                        detail = available ? "BepuPhysics v2 (managed)" : reason;
                        break;
                    case PhysicsEngine.Havok:
                        name = "Havok";
                        available = HavokPhysicsBackend.IsSupported(out reason);
                        detail = available ? "Havok Physics for Unity" : reason;
                        break;
                    case PhysicsEngine.Jolt:
                        name = "Jolt";
                        available = JoltPhysicsBackend.IsSupported(out reason);
                        detail = available ? "Jolt Physics (JoltC)" : reason;
                        break;
                    default:
                        name = engine.ToString(); detail = "unsupported"; reason = detail; available = false;
                        break;
                }
                list.Add(new PhysicsEngineInfo(engine, name, detail, reason, available));
            }
            return list;
        }
    }

    /// <summary>
    /// The active backend's own state dump, or <c>null</c> when PhysX is running.
    /// </summary>
    /// <remarks>
    /// Backend-specific diagnostics (body counts, native positions, motion types) live on the backend;
    /// this is the type-safe way for editor tooling to reach them without downcasting.
    /// </remarks>
    public static string DescribeActiveBackend()
    {
        if (backend is JoltPhysicsBackend jolt) return jolt.DescribeBodies();
        return backend == null ? null : backend.DisplayName + " has no state dump";
    }

    /// <summary>The active backend's layer-filter dump, or <c>null</c> when unsupported.</summary>
    public static string DescribeActiveBackendFiltering()
    {
        if (backend is JoltPhysicsBackend jolt) return jolt.DescribeFiltering();
        return backend == null ? null : backend.DisplayName + " has no filter dump";
    }

    /// <summary>The active backend's raycast probe dump, or <c>null</c> when unsupported.</summary>
    public static string DescribeActiveBackendRaycast()
    {
        if (backend is JoltPhysicsBackend jolt) return jolt.DescribeRaycast();
        return backend == null ? null : backend.DisplayName + " has no raycast probe";
    }

    /// <summary>
    /// Installs <paramref name="engine"/>, disposing whatever was running before. Returns true when the
    /// requested engine (or PhysX) is active afterwards. On failure the previous engine is dropped,
    /// PhysX takes over, and <see cref="LastError"/> explains why.
    /// </summary>
    public static bool SelectEngine(PhysicsEngine engine)
    {
        if (engine == current && (backend != null || engine == PhysicsEngine.PhysX)) return true;
        if (engine == PhysicsEngine.PhysX) { Shutdown(); current = PhysicsEngine.PhysX; return true; }

        // Tear the old backend down first so queued forces are flushed exactly once.
        Shutdown();
        current = engine;
        IPhysicsBackend candidate = null;
        try
        {
            candidate = Create(engine);
            try { candidate.Synchronize(); }
            catch { candidate.Dispose(); throw; }
            backend = candidate;
            LastError = null;
            Debug.Log("Physics backend: " + candidate.DisplayName);
            return true;
        }
        catch (Exception error)
        {
            LastError = error.Message;
            LastErrorTime = Time.realtimeSinceStartup;
            Debug.LogError(candidate != null ? candidate.DisplayName : engine.ToString() +
                " could not start; PhysX remains active. " + error);
            backend = null;
            current = PhysicsEngine.PhysX;
            Physics.SyncTransforms();
            return false;
        }
    }

    /// <summary>
    /// Compatibility shim for the original two-way toggle. Maps onto
    /// <see cref="PhysicsEngine.Box3D"/> / <see cref="PhysicsEngine.PhysX"/>.
    /// </summary>
    public static bool SelectBox3D(bool enabled) => SelectEngine(enabled ? PhysicsEngine.Box3D : PhysicsEngine.PhysX);

    private static IPhysicsBackend Create(PhysicsEngine engine)
    {
        switch (engine)
        {
            case PhysicsEngine.Box3D: return new Box3DBackend();
            case PhysicsEngine.Bepu: return new BepuBackend();
            case PhysicsEngine.Havok: return new HavokPhysicsBackend();
            case PhysicsEngine.Jolt: return new JoltPhysicsBackend();
            default: throw new NotSupportedException("No backend for " + engine);
        }
    }

    /// <summary>
    /// Advances the simulation by <paramref name="timeStep"/>. With no backend installed this is
    /// exactly <see cref="Physics.Simulate"/>.
    /// </summary>
    public static void Simulate(float timeStep)
    {
        // The stopwatch brackets the whole call, so the reading includes whatever the active path costs -
        // native solve, managed mirror, and the error handling below when a backend dies mid-step.
        long start = profilerSw.ElapsedTicks;
        try
        {
            using (SimulationMarker.Auto()) SimulateCore(timeStep);
        }
        finally
        {
            long elapsed = profilerSw.ElapsedTicks - start;
            float milliseconds = (float)(elapsed * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
            StepMilliseconds = StepCount == 0 ? milliseconds
                : StepMilliseconds + (milliseconds - StepMilliseconds) * StepSmoothing;
            StepCount++;
        }
    }

    private static readonly System.Diagnostics.Stopwatch profilerSw = System.Diagnostics.Stopwatch.StartNew();

    private static void SimulateCore(float timeStep)
    {
        if (backend == null) { Physics.Simulate(timeStep); return; }
        try
        {
            using (SynchronizeMarker.Auto()) backend.Synchronize();
            using (ForcesMarker.Auto()) FlushForces();
            using (BackendStepMarker.Auto()) backend.Step(timeStep);
        }
        catch (Exception error)
        {
            // Do not simulate the same timestep twice after an incomplete native step.
            LastError = error.Message;
            LastErrorTime = Time.realtimeSinceStartup;
            Debug.LogError(backend.DisplayName + " stopped; switching to PhysX. " + error);
            Shutdown();
            current = PhysicsEngine.PhysX;
            if (INUserSettings.Instance != null) INUserSettings.Instance.StarSeaSettings.PhysicsEngine = PhysicsEngine.PhysX;
        }
    }

    /// <summary>Disposes the active backend and hands control back to PhysX.</summary>
    public static void Shutdown()
    {
        FlushForces();
        try { backend?.Dispose(); }
        catch (Exception error) { Debug.LogError("Physics backend shutdown failed: " + error); }
        backend = null;
        current = PhysicsEngine.PhysX;
        Physics.SyncTransforms();
    }

    private static void FlushForces()
    {
        foreach (var pair in Pending)
            if (pair.Key && !pair.Key.isKinematic)
            {
                pair.Key.velocity += pair.Value.linear;
                pair.Key.angularVelocity += pair.Value.angular;
            }
        Pending.Clear();
    }

    internal static ulong PairKey(int a, int b)
    {
        uint x = unchecked((uint)a), y = unchecked((uint)b);
        return x < y ? ((ulong)x << 32) | y : ((ulong)y << 32) | x;
    }

    public static void IgnoreCollision(Collider collider1, Collider collider2, bool ignore = true)
    {
        if (!collider1 || !collider2) return;
        ulong key = PairKey(collider1.GetInstanceID(), collider2.GetInstanceID());
        // Unity may lose its ignore flag when a collider is disabled. Reapply that flag when needed,
        // but only invalidate native contacts when the backend's pair membership actually changes.
        bool changed = ignore ? IgnoredPairs.Add(key) : IgnoredPairs.Remove(key);
        if (Physics.GetIgnoreCollision(collider1, collider2) != ignore)
            Physics.IgnoreCollision(collider1, collider2, ignore);
        if (changed) backend?.Refilter(collider1, collider2);
    }

    /// <summary>
    /// True while this exact collider pair is told to ignore collisions — the marker separator's
    /// same-faction phasing, special charges, off-road wheels and stowed parts all use
    /// <see cref="IgnoreCollision"/>. Physics.IgnoreCollision suppresses CONTACTS only: raycasts and
    /// other queries still hit the collider, so anything that turns a query hit into a force
    /// (wheel ground probes, anti-tunnel checks, lights) has to ask this first.
    /// </summary>
    public static bool IsIgnoredPair(Collider collider1, Collider collider2)
    {
        if (!collider1 || !collider2) return false;
        return IgnoredPairs.Contains(PairKey(collider1.GetInstanceID(), collider2.GetInstanceID()));
    }

    private static Vector3 AngularDelta(Rigidbody body, Vector3 torque)
    {
        Quaternion frame = body.rotation * body.inertiaTensorRotation;
        Vector3 local = Quaternion.Inverse(frame) * torque;
        Vector3 inertia = body.inertiaTensor;
        local = new Vector3(inertia.x > 0f ? local.x / inertia.x : 0f,
            inertia.y > 0f ? local.y / inertia.y : 0f, inertia.z > 0f ? local.z / inertia.z : 0f);
        return frame * local;
    }

    private static void Accumulate(Rigidbody body, Vector3 value, ForceMode mode, bool angular)
    {
        if (!body || body.isKinematic || !body.gameObject.activeInHierarchy) return;
        if (mode == ForceMode.Force || mode == ForceMode.Acceleration) value *= Time.fixedDeltaTime;
        if (mode == ForceMode.Force || mode == ForceMode.Impulse)
            value = angular ? AngularDelta(body, value) : value / body.mass;
        Pending.TryGetValue(body, out var force);
        if (angular) force.angular += value; else force.linear += value;
        Pending[body] = force;
        backend?.Wake(body);
    }

    public static void EngineAddForce(this Rigidbody body, Vector3 force, ForceMode mode = ForceMode.Force)
    {
        if (!IsBox3D) body.AddForce(force, mode); else Accumulate(body, force, mode, false);
    }
    public static void EngineAddForce(this Rigidbody body, float x, float y, float z, ForceMode mode = ForceMode.Force)
        => body.EngineAddForce(new Vector3(x, y, z), mode);
    public static void EngineAddTorque(this Rigidbody body, Vector3 torque, ForceMode mode = ForceMode.Force)
    {
        if (!IsBox3D) body.AddTorque(torque, mode); else Accumulate(body, torque, mode, true);
    }
    public static void EngineAddTorque(this Rigidbody body, float x, float y, float z, ForceMode mode = ForceMode.Force)
        => body.EngineAddTorque(new Vector3(x, y, z), mode);
    public static void EngineAddForceAtPosition(this Rigidbody body, Vector3 force, Vector3 position, ForceMode mode = ForceMode.Force)
    {
        if (!IsBox3D) { body.AddForceAtPosition(force, position, mode); return; }
        Accumulate(body, force, mode, false);
        Accumulate(body, Vector3.Cross(position - body.worldCenterOfMass, force), mode, true);
    }
    public static void EngineAddRelativeForce(this Rigidbody body, Vector3 force, ForceMode mode = ForceMode.Force)
        => body.EngineAddForce(body.transform.TransformDirection(force), mode);
    public static void EngineAddRelativeTorque(this Rigidbody body, Vector3 torque, ForceMode mode = ForceMode.Force)
        => body.EngineAddTorque(body.transform.TransformDirection(torque), mode);
    public static void EngineAddRelativeTorque(this Rigidbody body, float x, float y, float z, ForceMode mode = ForceMode.Force)
        => body.EngineAddRelativeTorque(new Vector3(x, y, z), mode);
    public static void EngineMovePosition(this Rigidbody body, Vector3 position)
    {
        if (!IsBox3D) body.MovePosition(position); else body.position = position;
    }
    public static void EngineMoveRotation(this Rigidbody body, Quaternion rotation)
    {
        if (!IsBox3D) body.MoveRotation(rotation); else body.rotation = rotation;
    }
    public static void EngineWakeUp(this Rigidbody body)
    {
        body.WakeUp();
        backend?.Wake(body);
    }
    public static void EngineSleep(this Rigidbody body)
    {
        body.Sleep();
        backend?.Sleep(body);
    }
    public static bool EngineIsSleeping(this Rigidbody body) => backend != null ? backend.IsSleeping(body) : body.IsSleeping();
    public static float EngineAngle(this HingeJoint joint) => backend != null ? backend.HingeAngle(joint) : joint.angle;
}
