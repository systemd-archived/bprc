using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using JN = JoltNative;

/// <summary>
/// Jolt Physics backend, driving the game's <see cref="Rigidbody"/> state through the JoltC C API.
/// </summary>
/// <remarks>
/// <para>
/// The structure mirrors what the game already expects from a backend: every live
/// <see cref="Rigidbody"/> becomes a Jolt body, every <see cref="Collider"/> under it becomes a
/// sub-shape of that body (so multi-collider parts and enclosed parts behave as one rigid body), and
/// the step writes positions, rotations and velocities back onto the Unity components.
/// </para>
/// <para>
/// Collision filtering is delegated to Jolt's <c>GroupFilterTable</c>: each body gets a unique
/// sub-group id, and ignored pairs disable that pair. That is what makes
/// <c>GamePhysics.IgnoreCollision</c> work for parts that must not collide with their own attached
/// colliders, which the game relies on heavily.
/// </para>
/// </remarks>
public sealed partial class JoltPhysicsBackend : IPhysicsBackend
{
    public PhysicsEngine Engine => PhysicsEngine.Jolt;

    public string DisplayName => "Jolt";

    /// <summary>Broad-phase capacity. Bodies beyond this are refused with a clear error.</summary>
    private const uint MaxBodies = 16384;

    private const int BroadPhaseLayers = 1;
    private const uint ErrorBodyLimit = 1;   // JoltC_PhysicsUpdateError bit for "too many bodies"

    // -----------------------------------------------------------------
    // Native handles
    // -----------------------------------------------------------------
    private IntPtr system;
    private IntPtr bodyInterface;
    private IntPtr tempAllocator;
    private IntPtr jobSystem;
    private IntPtr broadPhaseLayers;
    private IntPtr objectVsBroadPhase;
    private IntPtr objectPairs;
    private IntPtr groupFilter;
    /// <summary>
    /// The <c>GroupFilter*</c> inside <see cref="groupFilter"/>. Bodies must be given this pointer, not
    /// the wrapper; see <see cref="BuildFiltering"/>.
    /// </summary>
    private IntPtr groupFilterNative;
    private bool disposed;

    private readonly ulong[] layerMasks = new ulong[32];
    private uint nextSubGroup;

    // -----------------------------------------------------------------
    // Mirrored state
    // -----------------------------------------------------------------
    private sealed class BodyEntry
    {
        public Rigidbody source;
        public Transform transform;
        public uint id = JN.BodyIdInvalid;
        public uint subGroup;
        public RigidbodyInterpolation interpolation;
        public RigidbodyConstraints constraints;
        public Vector3 incomingVelocity;
        public float appliedMass = float.NaN;
        public bool appliedSensor;
        public bool shapeDirty = true;
        /// <summary>
        /// The shape handle the native body currently holds. This entry owns exactly one reference to it,
        /// separate from the shape table's reference to a per-collider handle, and releases it on the
        /// next rebuild or on <see cref="DestroyBody"/>.
        /// </summary>
        public IntPtr shape = IntPtr.Zero;
    }

    private sealed class ShapeEntry
    {
        public Collider source;
        public int instanceId;
        public BodyEntry owner;
        public IntPtr shape;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 scale;
        public Vector3 center;
        public Vector3 dimensions;
        public Mesh mesh;
        public bool trigger;
    }

    private readonly Dictionary<int, BodyEntry> bodies = new Dictionary<int, BodyEntry>();
    private readonly Dictionary<int, ShapeEntry> shapes = new Dictionary<int, ShapeEntry>();
    private readonly List<ShapeEntry> shapeBuffer = new List<ShapeEntry>();
    private readonly List<int> removed = new List<int>();
    private readonly HashSet<int> seen = new HashSet<int>();
    private readonly HashSet<ulong> appliedIgnores = new HashSet<ulong>();
    private readonly List<ulong> removedPairs = new List<ulong>();
    /// <summary>
    /// Handles for colliders that disappeared this frame. They are released only after every body has
    /// been rebuilt, because a compound built this frame may still reference them until then.
    /// </summary>
    private readonly List<IntPtr> pendingShapeRelease = new List<IntPtr>();
    /// <summary>Snapshot of the bodies needing a shape rebuild, so the loop cannot mutate what it walks.</summary>
    private readonly List<BodyEntry> rebuildScratch = new List<BodyEntry>();

    // -----------------------------------------------------------------
    // Availability
    // -----------------------------------------------------------------

    /// <summary>
    /// Reports whether the Jolt backend may be selected.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Availability depends only on the native <c>libJoltC</c> plugin loading, which is probed rather
    /// than assumed. The experimental gate that used to guard this was removed once collision response
    /// started working: the gate existed solely because a falling body passed through static geometry,
    /// and that defect is fixed (see <see cref="Synchronize"/> and <c>docs/PhysicsBackends.md</c>).
    /// </para>
    /// <para>
    /// <c>StarSeaSettings.EnableExperimentalJolt</c> is still persisted so an existing settings file
    /// keeps loading, but it no longer decides availability.
    /// </para>
    /// </remarks>
    public static bool IsSupported(out string reason)
    {
        return IsPluginUsable(out reason);
    }

    /// <summary>
    /// Loads and initialises the native plugin to decide whether it is usable at all, independent of
    /// the experimental gate.
    /// </summary>
    public static bool IsPluginUsable(out string reason)
    {
        try
        {
            if (JN.JoltC_Init() == 0)
            {
                reason = "JoltC_Init failed" + JN.Describe();
                return false;
            }
            JN.JoltC_Shutdown();
            reason = null;
            return true;
        }
        catch (DllNotFoundException)
        {
            reason = "libJoltC plugin not installed for " + Application.platform;
            return false;
        }
        catch (EntryPointNotFoundException error)
        {
            reason = "libJoltC is incompatible: " + error.Message;
            return false;
        }
        catch (Exception error)
        {
            reason = error.Message;
            return false;
        }
    }

    // -----------------------------------------------------------------
    // Construction / teardown
    // -----------------------------------------------------------------

    /// <summary>
    /// Diagnostic switch: when set, bodies are created without a collision group filter.
    /// </summary>
    /// <remarks>
    /// Used to isolate whether Jolt's <c>GroupFilterTable</c> is suppressing collisions. Not for
    /// gameplay; a group filter is what makes <c>GamePhysics.IgnoreCollision</c> work.
    /// </remarks>
    public static bool DisableGroupFilterForDiagnostics;

    public JoltPhysicsBackend()
    {
        if (JN.JoltC_Init() == 0) throw new InvalidOperationException("JoltC_Init failed" + JN.Describe());
        try
        {
            // A drifted interop struct crashes natively inside Jolt; catch it here instead.
            JN.ValidateLayouts();
            BuildFiltering();
            system = JN.Require(JN.JoltC_PhysicsSystem_Create(), "PhysicsSystem_Create");
            JN.JoltC_PhysicsSystem_Init(system, MaxBodies, 0, MaxBodies * 2, MaxBodies,
                broadPhaseLayers, objectVsBroadPhase, objectPairs);
            bodyInterface = JN.Require(JN.JoltC_PhysicsSystem_GetBodyInterface(system), "GetBodyInterface");
            tempAllocator = JN.Require(JN.JoltC_TempAllocatorMalloc_Create(), "TempAllocatorMalloc_Create");
            // Jolt's own default is hardware_concurrency() - 1; the ceiling used to be pinned at eight,
            // which left cores idle on a larger machine. 优化设置 now owns the number.
            int threads = Mathf.Clamp(GamePhysics.PhysicsWorkerCount(), 0, 64);
            jobSystem = JN.Require(JN.JoltC_JobSystemThreadPool_Create(2048, 8, threads), "JobSystemThreadPool_Create");
            JN.JoltC_PhysicsSystem_SetGravity(system, ToJolt(Physics.gravity));
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>
    /// Builds the object-layer and group-filter tables.
    /// </summary>
    /// <remarks>
    /// Every Unity layer maps to its own Jolt object layer (Jolt allows 16 bits, Unity uses 5) with a
    /// single broad-phase layer, and the layer-pair filter is seeded from
    /// <see cref="Physics.GetIgnoreLayerCollision"/> so the project's collision matrix is honoured.
    /// </remarks>
    private void BuildFiltering()
    {
        broadPhaseLayers = JN.Require(JN.JoltC_BroadPhaseLayerInterfaceTable_Create(32, BroadPhaseLayers),
            "BroadPhaseLayerInterfaceTable_Create");
        for (ushort layer = 0; layer < 32; layer++)
            JN.JoltC_BroadPhaseLayerInterfaceTable_MapObjectToBroadPhaseLayer(broadPhaseLayers, layer, 0);

        objectPairs = JN.Require(JN.JoltC_ObjectLayerPairFilterTable_Create(32), "ObjectLayerPairFilterTable_Create");
        // The broad-phase table snapshots the pair table at construction. Seed a permissive coarse
        // filter first, so later layer changes can enable contacts without a stale broad-phase veto.
        // The mutable objectPairs table below remains the authoritative narrow-phase layer filter.
        for (ushort a = 0; a < 32; a++)
            for (ushort b = a; b < 32; b++)
                JN.JoltC_ObjectLayerPairFilterTable_EnableCollision(objectPairs, a, b);
        objectVsBroadPhase = JN.Require(
            JN.JoltC_ObjectVsBroadPhaseLayerFilterTable_Create(broadPhaseLayers, BroadPhaseLayers, objectPairs, 32),
            "ObjectVsBroadPhaseLayerFilterTable_Create");

        GamePhysics.ReadLayerCollisionMasks(layerMasks);
        UpdateLayerFiltering();

        groupFilter = JN.Require(JN.JoltC_GroupFilterTable_Create(MaxBodies), "GroupFilterTable_Create");
        // BodyCreationSettings takes the unwrapped GroupFilter*, while table APIs take its wrapper.
        groupFilterNative = Marshal.ReadIntPtr(groupFilter);
        nextSubGroup = 0;
    }

    private void UpdateLayerFiltering()
    {
        for (ushort a = 0; a < 32; a++)
            for (ushort b = 0; b < 32; b++)
            {
                if ((layerMasks[a] & (1UL << b)) == 0)
                    JN.JoltC_ObjectLayerPairFilterTable_DisableCollision(objectPairs, a, b);
                else
                    JN.JoltC_ObjectLayerPairFilterTable_EnableCollision(objectPairs, a, b);
            }

    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;

        foreach (int id in new List<int>(joints.Keys)) DestroyJoint(joints[id], id);

        foreach (var entry in bodies.Values)
        {
            if (entry.source) entry.source.interpolation = entry.interpolation;
            // Each body holds one reference to its own shape handle.
            if (entry.shape != IntPtr.Zero) { JN.JoltC_Shape_Release(entry.shape); entry.shape = IntPtr.Zero; }
        }
        bodies.Clear();
        // The shape table owns the per-collider handles; releasing them here would double-release any
        // handle a compound was built over.
        foreach (var entry in shapes.Values)
            if (entry.shape != IntPtr.Zero) JN.JoltC_Shape_Release(entry.shape);
        shapes.Clear();
        foreach (IntPtr handle in pendingShapeRelease)
            if (handle != IntPtr.Zero) JN.JoltC_Shape_Release(handle);
        pendingShapeRelease.Clear();
        appliedIgnores.Clear();
        joints.Clear();
        pendingJointDestruction.Clear();

        if (system != IntPtr.Zero) { JN.JoltC_PhysicsSystem_Destroy(system); system = IntPtr.Zero; bodyInterface = IntPtr.Zero; }
        if (jobSystem != IntPtr.Zero) { JN.JoltC_JobSystem_Destroy(jobSystem); jobSystem = IntPtr.Zero; }
        if (tempAllocator != IntPtr.Zero) { JN.JoltC_TempAllocator_Destroy(tempAllocator); tempAllocator = IntPtr.Zero; }
        if (objectVsBroadPhase != IntPtr.Zero) { JN.JoltC_ObjectVsBroadPhaseLayerFilter_Destroy(objectVsBroadPhase); objectVsBroadPhase = IntPtr.Zero; }
        if (objectPairs != IntPtr.Zero) { JN.JoltC_ObjectLayerPairFilter_Destroy(objectPairs); objectPairs = IntPtr.Zero; }
        if (broadPhaseLayers != IntPtr.Zero) { JN.JoltC_BroadPhaseLayerInterface_Destroy(broadPhaseLayers); broadPhaseLayers = IntPtr.Zero; }
        if (groupFilter != IntPtr.Zero) { JN.JoltC_GroupFilter_Destroy(groupFilter); groupFilter = IntPtr.Zero; groupFilterNative = IntPtr.Zero; }

        try { JN.JoltC_Shutdown(); } catch (Exception) { /* the plugin may already be gone */ }
    }

    // -----------------------------------------------------------------
    // Synchronization
    // -----------------------------------------------------------------

    public void Synchronize()
    {
        if (disposed) throw new ObjectDisposedException(nameof(JoltPhysicsBackend));
        if (GamePhysics.ReadLayerCollisionMasks(layerMasks)) UpdateLayerFiltering();

        // Discovery picks up parts spawned by legacy code without touching prefab serialization.
        //
        // Order matters and is the whole point of this pass: every collider is resolved into the shape
        // table BEFORE the first native body exists. Building the collider set first is what lets
        // CreateBody receive a real shape, which is what removed the empty stand-in this backend used to
        // create bodies with.
        foreach (var rb in GamePhysics.FindSceneObjects<Rigidbody>()) EnsureBody(rb, rb.transform);

        seen.Clear();
        foreach (var collider in GamePhysics.FindSceneObjects<Collider>())
        {
            if (!collider.enabled || !collider.gameObject.activeInHierarchy) continue;
            int id = collider.GetInstanceID();
            seen.Add(id);
            var rb = collider.attachedRigidbody;
            var owner = EnsureBody(rb, rb ? rb.transform : collider.transform);
            ColliderPose(collider.transform, owner.transform, out var toBody, out var rotation);
            Vector3 scale = collider.transform.lossyScale;
            GeometrySignature(collider, out var center, out var dimensions, out var mesh);
            Vector3 local = toBody.MultiplyPoint3x4(center);
            if (collider is SphereCollider) rotation = Quaternion.identity;

            shapes.TryGetValue(id, out var entry);
            if (entry != null && (entry.owner != owner || entry.trigger != collider.isTrigger || entry.mesh != mesh ||
                (entry.localPosition - local).sqrMagnitude > 1e-10f || Quaternion.Angle(entry.localRotation, rotation) > 0.01f ||
                (entry.scale - scale).sqrMagnitude > 1e-10f || entry.center != center || entry.dimensions != dimensions))
            {
                entry.owner.shapeDirty = true;
                // The old handle is replaced by the rebuilt entry below, so it has to be released or it
                // leaks. It is queued rather than freed now because a compound built from it may still
                // be alive until the rebuild loop below has run.
                if (entry.shape != IntPtr.Zero) pendingShapeRelease.Add(entry.shape);
                shapes.Remove(id);
                entry = null;
            }
            if (entry == null)
            {
                entry = new ShapeEntry
                {
                    source = collider, instanceId = id, owner = owner, localPosition = local, localRotation = rotation,
                    scale = scale, center = center, dimensions = dimensions, mesh = mesh, trigger = collider.isTrigger
                };
                entry.shape = CreateShape(entry);
                if (entry.shape == IntPtr.Zero)
                {
                    // Unsupported collider: skip it rather than losing the entire backend.
                    Debug.LogWarning("Jolt: skipping unsupported collider " + collider.GetType().Name + " on " + collider.name);
                    continue;
                }
                shapes.Add(id, entry);
                owner.shapeDirty = true;
            }
        }

        removed.Clear();
        foreach (var pair in shapes) if (!seen.Contains(pair.Key)) removed.Add(pair.Key);
        foreach (int id in removed)
        {
            var entry = shapes[id];
            if (entry.owner != null) entry.owner.shapeDirty = true;
            // Queue the handle rather than releasing it now: a body rebuilt this frame after removing
            // the collider may still have a compound built over this handle.
            if (entry.shape != IntPtr.Zero) pendingShapeRelease.Add(entry.shape);
            shapes.Remove(id);
        }

        removed.Clear();
        foreach (var pair in bodies)
        {
            var entry = pair.Value;
            if (!entry.transform || !entry.transform.gameObject.activeInHierarchy ||
                (!ReferenceEquals(entry.source, null) && !entry.source)) removed.Add(pair.Key);
        }
        foreach (int id in removed) DestroyBody(bodies[id], id);

        ApplyPendingIgnores();

        // Rebuild any body whose collider set changed. This runs after the whole collider sweep so a
        // body with several colliders is rebuilt once, not once per collider.
        //
        // The dirty set is snapshotted first: RebuildShape detaches a body that lost its last collider
        // (id goes invalid) but never removes it from `bodies`, so a snapshot keeps this loop correct
        // without enumerating a collection that could change underneath it.
        rebuildScratch.Clear();
        foreach (var entry in bodies.Values)
            if (entry.shapeDirty) rebuildScratch.Add(entry);
        for (int i = 0; i < rebuildScratch.Count; i++) RebuildShape(rebuildScratch[i]);
        rebuildScratch.Clear();

        // Joints last: both endpoints exist by now, and a body rebuilt above has a stable id.
        SynchronizeJoints();

        // Only now is it safe to release the shapes whose colliders vanished this frame.
        foreach (IntPtr handle in pendingShapeRelease)
            if (handle != IntPtr.Zero) JN.JoltC_Shape_Release(handle);
        pendingShapeRelease.Clear();
    }

    /// <summary>
    /// Reconciles <see cref="GamePhysics.IgnoredPairs"/> with Jolt's group filter.
    /// </summary>
    /// <remarks>
    /// The ignored-pair set is authoritative and deliberately lives in <c>GamePhysics</c> rather than
    /// in a backend, so it survives an engine switch. Reconciling the whole set each frame (rather
    /// than reacting to each <c>IgnoreCollision</c> call) means a switch into Jolt immediately honours
    /// every pair the game has already registered.
    /// </remarks>
    private void ApplyPendingIgnores()
    {
        var desired = GamePhysics.IgnoredPairs;
        foreach (ulong key in desired)
        {
            if (appliedIgnores.Contains(key)) continue;
            if (!TryPair(key, out var a, out var b)) continue;
            JN.JoltC_GroupFilterTable_DisableCollision(groupFilter, a.subGroup, b.subGroup);
            appliedIgnores.Add(key);
            Invalidate(a); Invalidate(b);
        }

        removedPairs.Clear();
        foreach (ulong key in appliedIgnores) if (!desired.Contains(key)) removedPairs.Add(key);
        foreach (ulong key in removedPairs)
        {
            appliedIgnores.Remove(key);
            if (!TryPair(key, out var a, out var b)) continue;
            JN.JoltC_GroupFilterTable_EnableCollision(groupFilter, a.subGroup, b.subGroup);
            Invalidate(a); Invalidate(b);
        }
    }

    /// <summary>Resolves a collider-pair key to the two bodies that own those colliders.</summary>
    private bool TryPair(ulong key, out BodyEntry a, out BodyEntry b)
    {
        a = null; b = null;
        int high = (int)(key >> 32), low = (int)(key & 0xFFFFFFFF);
        if (!shapes.TryGetValue(high, out var sa) || !shapes.TryGetValue(low, out var sb)) return false;
        a = sa.owner; b = sb.owner;
        return a != null && b != null && a != b;
    }

    private void Invalidate(BodyEntry entry)
    {
        if (entry != null && entry.id != JN.BodyIdInvalid)
            JN.JoltC_BodyInterface_InvalidateContactCache(bodyInterface, entry.id);
    }

    public void Refilter(Collider a, Collider b)
    {
        // The authoritative work happens in ApplyPendingIgnores on the next Synchronize. Here it is
        // enough to drop the cached contacts so stale filtering cannot keep the old pairs alive.
        if (a && shapes.TryGetValue(a.GetInstanceID(), out var sa)) Invalidate(sa.owner);
        if (b && shapes.TryGetValue(b.GetInstanceID(), out var sb)) Invalidate(sb.owner);
    }

    // -----------------------------------------------------------------
    // Bodies
    // -----------------------------------------------------------------

    /// <summary>
    /// Returns the entry for a rigidbody's owner, registering it if this is the first time it is seen.
    /// </summary>
    /// <remarks>
    /// Registration only. The native body is created later, by <see cref="RebuildShape"/>, once the
    /// frame's colliders have been resolved into the shape table — a body cannot be created without a
    /// shape, and the shape is whatever the colliders turn out to be.
    /// </remarks>
    private BodyEntry EnsureBody(Rigidbody rb, Transform transform)
    {
        int id = rb ? rb.GetInstanceID() : transform.GetInstanceID();
        if (bodies.TryGetValue(id, out var entry)) return entry;

        if (bodies.Count >= MaxBodies)
            throw new InvalidOperationException("Jolt body limit reached (" + MaxBodies + ")");

        entry = new BodyEntry { source = rb, transform = transform, subGroup = nextSubGroup++, shapeDirty = true };
        if (rb)
        {
            entry.interpolation = rb.interpolation;
            rb.interpolation = RigidbodyInterpolation.None;
        }
        bodies.Add(id, entry);
        return entry;
    }

    /// <summary>
    /// Creates the native body with the shape its colliders resolve to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A shape handle is required, and the caller owns exactly one reference to it. There is no empty
    /// stand-in: a body with no colliders is never created at all (see <see cref="RebuildShape"/>),
    /// because a placeholder would have to be swapped out later and swapping is where this backend used
    /// to corrupt the heap.
    /// </para>
    /// <para>
    /// The body takes its own reference to the shape, so the handle stays valid for the body's lifetime
    /// regardless of what the shape table does with it.
    /// </para>
    /// </remarks>
    private void CreateBody(BodyEntry entry, IntPtr shape)
    {
        if (shape == IntPtr.Zero)
            throw new InvalidOperationException("Jolt: refusing to create a body with a null shape");

        var rb = entry.source;
        Vector3 position = rb ? rb.position : entry.transform.position;
        Quaternion rotation = rb ? rb.rotation : entry.transform.rotation;

        var settings = new JN.BodyCreationSettings();
        JN.JoltC_BodyCreationSettings_SetDefault(ref settings);
        settings.position = ToJoltPosition(position);
        settings.rotation = ToJolt(rotation);
        settings.motionType = rb ? (rb.isKinematic ? JN.MotionTypeKinematic : JN.MotionTypeDynamic) : JN.MotionTypeStatic;
        settings.objectLayer = (ushort)(rb ? rb.gameObject.layer : entry.transform.gameObject.layer);
        settings.allowDynamicOrKinematic = 1;
        settings.enhancedInternalEdgeRemoval = 1;
        settings.allowedDOFs = rb ? ToAllowedDofs(rb.constraints) : JN.AllowedDofsAll;
        settings.collisionGroup = new JN.CollisionGroup
        {
            // CollisionGroup.groupFilter is a long precisely so the struct stays blittable; see the
            // remarks on JoltNative.CollisionGroup. It must carry the unwrapped GroupFilter* rather than
            // the JoltC_GroupFilter wrapper - see BuildFiltering for why.
            groupFilter = DisableGroupFilterForDiagnostics ? 0L : groupFilterNative.ToInt64(),
            groupID = 0,
            subGroupID = entry.subGroup,
        };
        if (rb)
        {
            entry.constraints = rb.constraints;
            settings.friction = 0.6f;
            settings.restitution = 0f;
            settings.linearDamping = Mathf.Max(0f, rb.drag);
            settings.angularDamping = Mathf.Max(0f, rb.angularDrag);
            settings.maxAngularVelocity = Mathf.Max(0.01f, rb.maxAngularVelocity);
            settings.gravityFactor = rb.useGravity ? 1f : 0f;
            settings.motionQuality = rb.collisionDetectionMode != CollisionDetectionMode.Discrete
                ? JN.MotionQualityLinearCast : JN.MotionQualityDiscrete;
        }
        settings.shape = shape;
        // A body whose only colliders are triggers becomes a sensor, so it reports overlap instead of
        // resolving a contact. Without this a trigger was a solid obstacle: the shape was correct but
        // its trigger flag never reached the native body.
        settings.isSensor = IsSensorOnly(entry) ? 1 : 0;

        uint id = JN.JoltC_BodyInterface_CreateAndAddBody(bodyInterface, ref settings, JN.ActivationActivate);
        if (id == JN.BodyIdInvalid)
            throw new InvalidOperationException("Jolt: CreateAndAddBody failed" + JN.Describe());

        entry.id = id;
        entry.appliedSensor = settings.isSensor != 0;
        entry.shapeDirty = false;
    }

    /// <summary>
    /// True when every collider on this body is a trigger, which is when Unity treats the body as a
    /// sensor rather than a solid shape.
    /// </summary>
    private bool IsSensorOnly(BodyEntry entry)
    {
        bool any = false;
        foreach (var pair in shapes)
        {
            if (pair.Value.owner != entry) continue;
            if (!pair.Value.trigger) return false;
            any = true;
        }
        return any;
    }

    /// <summary>
    /// Builds the shape handle a body should use, transferring one owned reference to the caller.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Returns <see cref="IntPtr.Zero"/> when the body has no usable colliders. There is deliberately no
    /// empty-shape fallback: a zero-volume shape produces no contacts and is invisible to a raycast, so
    /// a body holding one looks exactly like a body that does not collide at all.
    /// </para>
    /// <para>
    /// Single collider: the table's handle is <c>AddRef</c>ed and returned, so the caller may release it
    /// without disturbing the shape table's own reference. Several colliders: a fresh compound is built
    /// and returned. Either way the caller must release the result exactly once.
    /// </para>
    /// </remarks>
    private IntPtr BuildShapeFor(BodyEntry entry)
    {
        shapeBuffer.Clear();
        foreach (var pair in shapes) if (pair.Value.owner == entry) shapeBuffer.Add(pair.Value);
        // Stable order keeps compound sub-shape user data reproducible across frames.
        shapeBuffer.Sort((x, y) => x.instanceId.CompareTo(y.instanceId));

        if (shapeBuffer.Count == 0) return IntPtr.Zero;
        if (shapeBuffer.Count == 1)
        {
            // The shape table keeps owning this handle; the body gets its own reference.
            JN.JoltC_Shape_AddRef(shapeBuffer[0].shape);
            return shapeBuffer[0].shape;
        }

        // JoltC exports the direct array-based constructor, not
        // StaticCompoundShapeSettings_CreateShape. Use the ABI in JoltC/include/JoltC/shape.h.
        var children = new JN.CompoundShapeSubShape[shapeBuffer.Count];
        for (int i = 0; i < shapeBuffer.Count; i++)
        {
            var shape = shapeBuffer[i];
            children[i] = new JN.CompoundShapeSubShape
            {
                position = ToJolt(shape.localPosition), rotation = ToJolt(shape.localRotation),
                shape = shape.shape, userData = (uint)shape.instanceId
            };
        }
        return JN.Require(JN.JoltC_StaticCompoundShape_Create(children, children.Length), "StaticCompoundShape_Create");
    }

    /// <summary>
    /// Brings a body's native shape in line with its current colliders.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>JoltC_BodyInterface_SetShape</c> is the whole mechanism here. It swaps the shape, invalidates
    /// the contact cache and notifies the broad phase (see <c>BodyInterface::SetShape</c> in the vendored
    /// Jolt source), so adding a collider at runtime is a shape swap and the body id stays stable. An
    /// earlier version of this method believed otherwise and destroyed and recreated the body instead;
    /// that path released the very handle it then handed to <c>CreateAndAddBody</c>, which is a
    /// use-after-free and the reason this backend used to pass through geometry.
    /// </para>
    /// <para>
    /// The previous handle is released <i>after</i> the swap, so the body is never briefly pointing at a
    /// freed shape.
    /// </para>
    /// </remarks>
    private void RebuildShape(BodyEntry entry)
    {
        entry.shapeDirty = false;

        IntPtr built = BuildShapeFor(entry);

        // A body with no colliders has nothing to simulate. Drop it rather than give it a placeholder,
        // and let the next Synchronize bring it back if a collider appears.
        if (built == IntPtr.Zero)
        {
            if (entry.id != JN.BodyIdInvalid)
            {
                DestroyJointsForBody(entry);
                JN.JoltC_BodyInterface_RemoveAndDestroyBody(bodyInterface, entry.id);
                entry.id = JN.BodyIdInvalid;
            }
            if (entry.shape != IntPtr.Zero)
            {
                JN.JoltC_Shape_Release(entry.shape);
                entry.shape = IntPtr.Zero;
            }
            return;
        }

        if (entry.id == JN.BodyIdInvalid)
        {
            // The body was dropped earlier (or is new); create it now that a real shape exists.
            CreateBody(entry, built);
            if (entry.shape != IntPtr.Zero) JN.JoltC_Shape_Release(entry.shape);
            entry.shape = built;
            entry.appliedMass = float.NaN;
            return;
        }

        JN.JoltC_BodyInterface_SetShape(bodyInterface, entry.id, built, 1, JN.ActivationActivate);
        JN.JoltC_BodyInterface_InvalidateContactCache(bodyInterface, entry.id);
        if (entry.shape != IntPtr.Zero) JN.JoltC_Shape_Release(entry.shape);
        entry.shape = built;
        entry.appliedMass = float.NaN;

        // Toggling isTrigger at runtime changes the shape entry, which lands here. Re-apply the sensor
        // flag so a trigger turned solid (or the reverse) takes effect on the swap rather than on the
        // next body recreation.
        bool sensor = IsSensorOnly(entry);
        if (sensor != entry.appliedSensor)
        {
            JN.JoltC_BodyInterface_SetIsSensor(bodyInterface, entry.id, sensor ? 1 : 0);
            entry.appliedSensor = sensor;
        }
    }

    private void DestroyBody(BodyEntry entry, int key)
    {
        if (entry.source) entry.source.interpolation = entry.interpolation;
        if (entry.id != JN.BodyIdInvalid)
        {
            DestroyJointsForBody(entry);
            JN.JoltC_BodyInterface_RemoveAndDestroyBody(bodyInterface, entry.id);
            entry.id = JN.BodyIdInvalid;
        }
        if (entry.shape != IntPtr.Zero)
        {
            JN.JoltC_Shape_Release(entry.shape);
            entry.shape = IntPtr.Zero;
        }
        bodies.Remove(key);
    }

    // -----------------------------------------------------------------
    // Shape construction
    // -----------------------------------------------------------------

    private static void GeometrySignature(Collider c, out Vector3 center, out Vector3 dimensions, out Mesh mesh)
    {
        center = Vector3.zero; dimensions = Vector3.zero; mesh = null;
        if (c is BoxCollider box) { center = box.center; dimensions = box.size; }
        else if (c is SphereCollider sphere) { center = sphere.center; dimensions.x = sphere.radius; }
        else if (c is CapsuleCollider capsule) { center = capsule.center; dimensions = new Vector3(capsule.radius, capsule.height, capsule.direction); }
        else if (c is MeshCollider mc) { mesh = mc.sharedMesh; dimensions.x = mc.convex ? 1 : 0; }
        else if (c is TerrainCollider) { dimensions.x = 1; }
    }

    private static void ColliderPose(Transform collider, Transform owner, out Matrix4x4 matrix, out Quaternion rotation)
    {
        matrix = Matrix4x4.identity;
        rotation = Quaternion.identity;
        for (var t = collider; t != null && t != owner; t = t.parent)
        {
            matrix = Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale) * matrix;
            rotation = t.localRotation * rotation;
        }
        matrix = Matrix4x4.Scale(owner.lossyScale) * matrix;
        rotation = rotation.normalized;
    }

    private static IntPtr CreateShape(ShapeEntry e)
    {
        Vector3 scale = new Vector3(Mathf.Abs(e.scale.x), Mathf.Abs(e.scale.y), Mathf.Abs(e.scale.z));
        if (e.source is BoxCollider box)
        {
            Vector3 half = Vector3.Scale(box.size, scale) * 0.5f;
            half = Vector3.Max(half, Vector3.one * 0.0001f);
            return JN.Require(JN.JoltC_BoxShapeSettings_CreateShape(ToJolt(half), 0.05f), "BoxShapeSettings_CreateShape");
        }
        if (e.source is SphereCollider sphere)
        {
            float radius = Mathf.Max(0.0001f, sphere.radius * Mathf.Max(scale.x, scale.y, scale.z));
            return JN.Require(JN.JoltC_SphereShapeSettings_CreateShape(radius), "SphereShapeSettings_CreateShape");
        }
        if (e.source is CapsuleCollider capsule)
        {
            int axis = capsule.direction;
            float radius = Mathf.Max(0.0001f, capsule.radius * Mathf.Max(scale[(axis + 1) % 3], scale[(axis + 2) % 3]));
            float halfHeight = Mathf.Max(0f, capsule.height * scale[axis] * 0.5f - radius);
            // Jolt's capsule is Y-aligned, so a non-Y collider axis needs the shape rotated in place.
            IntPtr shape = JN.Require(JN.JoltC_CapsuleShapeSettings_CreateShape(halfHeight, radius), "CapsuleShapeSettings_CreateShape");
            if (axis == 0)
                return JN.Require(JN.JoltC_RotatedTranslatedShape_Create(
                    new JN.Vec3(0, 0, 0), ToJolt(Quaternion.Euler(0f, 0f, -90f)), shape), "RotatedTranslatedShape_Create");
            if (axis == 2)
                return JN.Require(JN.JoltC_RotatedTranslatedShape_Create(
                    new JN.Vec3(0, 0, 0), ToJolt(Quaternion.Euler(90f, 0f, 0f)), shape), "RotatedTranslatedShape_Create");
            return shape;
        }
        if (e.source is MeshCollider mc && mc.sharedMesh)
        {
            JoltMeshCache.Read(mc.sharedMesh, out var source, out var triangles);
            var vertices = new Vector3[source.Length];
            for (int i = 0; i < source.Length; i++)
                vertices[i] = new Vector3(source[i].x * scale.x, source[i].y * scale.y, source[i].z * scale.z);
            if (mc.convex)
            {
                var points = new JN.Vec3[vertices.Length];
                for (int i = 0; i < vertices.Length; i++) points[i] = ToJolt(vertices[i]);
                IntPtr settings = JN.Require(JN.JoltC_ConvexHullShapeSettings_Create(points, points.Length, 0.05f),
                    "ConvexHullShapeSettings_Create");
                try { return JN.Require(JN.JoltC_ConvexHullShapeSettings_CreateShape(settings), "ConvexHullShapeSettings_CreateShape"); }
                finally { JN.JoltC_ShapeSettings_Destroy(settings); }
            }
            if (e.owner != null && e.owner.source)
                throw new NotSupportedException("Jolt requires a convex mesh for a Rigidbody: " + mc.name);
            var joltTriangles = new JN.Triangle[triangles.Length / 3];
            for (int i = 0, t = 0; i < triangles.Length; i += 3, t++)
                joltTriangles[t] = new JN.Triangle
                {
                    v1 = ToJolt(vertices[triangles[i]]),
                    v2 = ToJolt(vertices[triangles[i + 1]]),
                    v3 = ToJolt(vertices[triangles[i + 2]]),
                    materialIndex = 0,
                };
            IntPtr meshSettings = JN.Require(JN.JoltC_MeshShapeSettings_Create(joltTriangles, (uint)joltTriangles.Length),
                "MeshShapeSettings_Create");
            try { return JN.Require(JN.JoltC_MeshShapeSettings_CreateShape(meshSettings), "MeshShapeSettings_CreateShape"); }
            finally { JN.JoltC_MeshShapeSettings_Destroy(meshSettings); }
        }
        if (e.source is TerrainCollider tc && tc.terrainData)
        {
            if (e.owner != null && e.owner.source)
                throw new NotSupportedException("Jolt terrain must be static: " + tc.name);
            var data = tc.terrainData;
            int size = data.heightmapResolution;
            var samples = data.GetHeights(0, 0, size, size);
            // Unity's height map is [z, x] and Jolt's is [x + z * sampleCount], i.e. x fastest.
            var heights = new float[size * size];
            for (int z = 0; z < size; z++) for (int x = 0; x < size; x++) heights[z * size + x] = samples[z, x];
            var spacing = Vector3.Scale(data.heightmapScale, scale);
            IntPtr settings = JN.Require(
                JN.JoltC_HeightFieldShapeSettings_Create(heights, new JN.Vec3(0, 0, 0), ToJolt(spacing), (uint)size),
                "HeightFieldShapeSettings_Create");
            try { return JN.Require(JN.JoltC_HeightFieldShapeSettings_CreateShape(settings), "HeightFieldShapeSettings_CreateShape"); }
            finally { JN.JoltC_HeightFieldShapeSettings_Destroy(settings); }
        }
        return IntPtr.Zero;
    }

    // -----------------------------------------------------------------
    // Stepping
    // -----------------------------------------------------------------

    public void Step(float delta)
    {
        JN.JoltC_PhysicsSystem_SetGravity(system, ToJolt(Physics.gravity));

        foreach (var entry in bodies.Values)
        {
            var rb = entry.source;
            if (!rb || entry.id == JN.BodyIdInvalid) continue;

            int desired = rb.isKinematic ? JN.MotionTypeKinematic : JN.MotionTypeDynamic;
            if (JN.JoltC_BodyInterface_GetMotionType(bodyInterface, entry.id) != desired)
            {
                JN.JoltC_BodyInterface_SetMotionType(bodyInterface, entry.id, desired, JN.ActivationActivate);
                entry.appliedMass = float.NaN;
            }

            if (rb.isKinematic)
            {
                // Kinematic bodies follow the transform the game already moved.
                JN.JoltC_BodyInterface_MoveKinematic(bodyInterface, entry.id,
                    ToJoltPosition(rb.position), ToJolt(rb.rotation), delta);
                continue;
            }

            Vector3 position = rb.position;
            Quaternion rotation = rb.rotation;
            var nativePosition = JN.JoltC_BodyInterface_GetPosition(bodyInterface, entry.id);
            var nativeRotation = JN.JoltC_BodyInterface_GetRotation(bodyInterface, entry.id);
            bool moved = (position - ToVector(nativePosition)).sqrMagnitude > 1e-10f ||
                Quaternion.Angle(rotation, ToQuaternion(nativeRotation)) > 0.01f;
            if (moved)
                JN.JoltC_BodyInterface_SetPositionAndRotation(bodyInterface, entry.id,
                    ToJoltPosition(position), ToJolt(rotation), JN.ActivationActivate);

            // Jolt derives mass from shape volume and density, so Unity's authored mass is imposed as
            // inverse mass. Inertia stays shape-derived: Unity's per-axis inertia tensor has no Jolt
            // equivalent on the body interface.
            if (!(entry.appliedMass == rb.mass))
            {
                entry.appliedMass = rb.mass;
                JN.JoltC_BodyInterface_SetInverseMass(bodyInterface, entry.id, rb.mass > 0f ? 1f / rb.mass : 0f);
            }

            JN.JoltC_BodyInterface_SetGravityFactor(bodyInterface, entry.id, rb.useGravity ? 1f : 0f);
            JN.JoltC_BodyInterface_SetLinearDamping(bodyInterface, entry.id, Mathf.Max(0f, rb.drag));
            JN.JoltC_BodyInterface_SetAngularDamping(bodyInterface, entry.id, Mathf.Max(0f, rb.angularDrag));
            JN.JoltC_BodyInterface_SetMaxAngularVelocity(bodyInterface, entry.id, Mathf.Max(0.01f, rb.maxAngularVelocity));

            entry.incomingVelocity = rb.velocity;
            var linear = JN.JoltC_BodyInterface_GetLinearVelocity(bodyInterface, entry.id);
            if ((rb.velocity - ToVector(linear)).sqrMagnitude > 1e-12f)
                JN.JoltC_BodyInterface_SetLinearVelocity(bodyInterface, entry.id, ToJolt(rb.velocity));
            var angular = JN.JoltC_BodyInterface_GetAngularVelocity(bodyInterface, entry.id);
            Vector3 wanted = Vector3.ClampMagnitude(rb.angularVelocity, rb.maxAngularVelocity);
            if ((wanted - ToVector(angular)).sqrMagnitude > 1e-12f)
                JN.JoltC_BodyInterface_SetAngularVelocity(bodyInterface, entry.id, ToJolt(wanted));
        }

        uint error = JN.JoltC_PhysicsSystem_Update(system, delta, 1, tempAllocator, jobSystem);
        if ((error & ErrorBodyLimit) != 0)
            throw new InvalidOperationException("Jolt: too many bodies for the configured capacity (" + MaxBodies + ")");

        // A parent's Transform write moves its child rigidbodies. Write parents first
        // so a sprung axle retains the world pose computed by the native solver.
        mirrorOrder.Clear();
        foreach (var entry in bodies.Values)
            if (entry.source && !entry.source.isKinematic && entry.id != JN.BodyIdInvalid) mirrorOrder.Add(entry);
        mirrorOrder.Sort((a, b) => TransformDepth(a.transform).CompareTo(TransformDepth(b.transform)));
        foreach (var entry in mirrorOrder)
        {
            var rb = entry.source;
            JN.JoltC_BodyInterface_GetPositionAndRotation(bodyInterface, entry.id, out var position, out var rotation);
            JN.JoltC_BodyInterface_GetLinearAndAngularVelocity(bodyInterface, entry.id, out var linear, out var angular);
            rb.position = ToVector(position);
            rb.rotation = ToQuaternion(rotation);
            rb.velocity = ToVector(linear);
            rb.angularVelocity = ToVector(angular);
            entry.transform.SetPositionAndRotation(rb.position, rb.rotation);
        }
        Physics.SyncTransforms();
        BreakJoints();
    }

    private readonly List<BodyEntry> mirrorOrder = new List<BodyEntry>();

    private static int TransformDepth(Transform transform)
    {
        int depth = 0;
        for (var parent = transform.parent; parent != null; parent = parent.parent) depth++;
        return depth;
    }

    public void Wake(Rigidbody body)
    {
        if (body && bodies.TryGetValue(body.GetInstanceID(), out var entry) && entry.id != JN.BodyIdInvalid)
            JN.JoltC_BodyInterface_ActivateBody(bodyInterface, entry.id);
    }

    public void Sleep(Rigidbody body)
    {
        if (body && bodies.TryGetValue(body.GetInstanceID(), out var entry) && entry.id != JN.BodyIdInvalid)
            JN.JoltC_BodyInterface_DeactivateBody(bodyInterface, entry.id);
    }

    public bool IsSleeping(Rigidbody body)
    {
        if (!body || !bodies.TryGetValue(body.GetInstanceID(), out var entry) || entry.id == JN.BodyIdInvalid) return false;
        return JN.JoltC_BodyInterface_IsActive(bodyInterface, entry.id) == 0;
    }

    /// <summary>
    /// Reports what the layer filters say about each pair, and what Unity's own matrix says.
    /// </summary>
    /// <remarks>
    /// Contact suppression is invisible from the managed side: Jolt simply finds no pair. This prints
    /// both filters' verdicts so a disagreement is visible.
    /// </remarks>
    public string DescribeFiltering()
    {
        var report = new System.Text.StringBuilder();
        if (objectPairs == IntPtr.Zero) return "no filter\n";
        for (ushort a = 0; a < 32; a++)
        {
            for (ushort b = 0; b < 32; b++)
            {
                bool jolt = JN.JoltC_ObjectLayerPairFilterTable_ShouldCollide(objectPairs, a, b) != 0;
                bool unityAllows = !Physics.GetIgnoreLayerCollision(a, b);
                if (jolt != unityAllows)
                    report.AppendLine("  MISMATCH layer " + a + "x" + b + " jolt=" + jolt + " unityAllows=" + unityAllows);
            }
        }
        report.AppendLine("  unity layer0 self-ignores = " + Physics.GetIgnoreLayerCollision(0, 0));
        report.AppendLine("  jolt layer0x0 shouldCollide = " +
            (JN.JoltC_ObjectLayerPairFilterTable_ShouldCollide(objectPairs, 0, 0) != 0));
        // The ground and cube both sit on layer 0 in the probes, so this is the pair that matters.
        report.AppendLine("  subGroups in use = " + nextSubGroup);
        return report.ToString();
    }

    /// <summary>
    /// Casts a ray straight down through the world and reports what it hits.
    /// </summary>
    /// <remarks>
    /// Separates "the geometry is not in the world" from "the geometry is there but contacts are
    /// suppressed", which look identical from the outside because both end with a body falling through
    /// the floor.
    /// </remarks>
    public string DescribeRaycast()
    {
        var report = new System.Text.StringBuilder();
        IntPtr query = JN.JoltC_PhysicsSystem_GetNarrowPhaseQuery(system);
        if (query == IntPtr.Zero) return "no narrow phase query\n";
        var origin = new JN.RVec3(0f, 5f, 0f);
        var direction = new JN.Vec3(0f, -1f, 0f);
        bool hit = JN.JoltC_NarrowPhaseQuery_CastRay(query, origin, direction, out var result) != 0;
        report.AppendLine("  ray (0,5,0)->down hit=" + hit +
            (hit ? " bodyId=" + result.bodyID + " fraction=" + result.fraction.ToString("F3") : string.Empty));
        return report.ToString();
    }

    /// <summary>
    /// One line per mirrored body: name, native motion type, native position and shape presence.
    /// </summary>
    /// <remarks>
    /// For diagnosing "the body fell through the floor" style failures, where the interesting question
    /// is whether the static body was actually created static and whether a shape reached the plugin.
    /// </remarks>
    public string DescribeBodies()
    {
        var report = new System.Text.StringBuilder();
        if (bodyInterface == IntPtr.Zero) return "no body interface\n";
        report.AppendLine("bodies=" + bodies.Count + " shapes=" + shapes.Count +
            " nativeBodies=" + JN.JoltC_PhysicsSystem_GetNumBodies(system) +
            " constraints=" + JN.JoltC_PhysicsSystem_GetNumConstraints(system));
        JN.JoltC_PhysicsSystem_GetBounds(system, out var bounds);
        report.AppendLine("  broadphase bounds min=(" + bounds.min.x.ToString("F1") + "," + bounds.min.y.ToString("F1") + "," + bounds.min.z.ToString("F1") +
            ") max=(" + bounds.max.x.ToString("F1") + "," + bounds.max.y.ToString("F1") + "," + bounds.max.z.ToString("F1") + ")");
        foreach (var entry in bodies.Values)
        {
            string name = entry.source ? entry.source.name : entry.transform ? entry.transform.name : "?";
            string position = "?";
            int motion = -1;
            int active = -1;
            if (entry.id != JN.BodyIdInvalid)
            {
                var p = JN.JoltC_BodyInterface_GetPosition(bodyInterface, entry.id);
                position = p.x.ToString("F2") + "," + p.y.ToString("F2") + "," + p.z.ToString("F2");
                motion = JN.JoltC_BodyInterface_GetMotionType(bodyInterface, entry.id);
                // Jolt only generates contacts for active bodies, so this is the first thing to check
                // when a body exists but refuses to collide.
                active = JN.JoltC_BodyInterface_IsActive(bodyInterface, entry.id);
            }
            report.AppendLine("  " + name + " id=" + entry.id + " motion=" + motion + " active=" + active +
                " pos=" + position + " shape=0x" + entry.shape.ToInt64().ToString("X") +
                " subGroup=" + entry.subGroup + " hasRb=" + (entry.source != null));
        }
        foreach (var shape in shapes.Values)
        {
            string owner = shape.owner != null && shape.owner.source ? shape.owner.source.name : "(static)";
            report.AppendLine("  shape " + shape.source.GetType().Name + " on " + shape.source.name +
                " owner=" + owner + " dims=" + shape.dimensions + " scale=" + shape.scale +
                " local=" + shape.localPosition);
        }
        return report.ToString();
    }

    /// <summary>
    /// Creates bodies under a matrix of settings to isolate which field crashes Jolt.
    /// </summary>
    /// <remarks>
    /// A wrong body-creation setting does not raise a managed exception - it segfaults inside the
    /// plugin, and the stack trace only ever names <c>CreateAndAddBody</c>. This walks the settings one
    /// at a time so the culprit is identifiable. Each case runs in its own freshly initialised world.
    /// </remarks>
    public static string ProbeBodyCreation()
    {
        var report = new System.Text.StringBuilder();
        foreach (bool useShape in new[] { true, false })
        foreach (bool useEmptyShape in new[] { true, false })
        foreach (bool useGroupFilter in new[] { false, true })
        foreach (int motion in new[] { JN.MotionTypeStatic, JN.MotionTypeDynamic })
        {
            if (useShape && useEmptyShape) continue;
            // The null-shape case segfaults the plugin, so it runs last: everything after it would
            // otherwise be unreachable.
            if (!useShape && !useEmptyShape) continue;
            string label = "shape=" + (useShape ? "box" : "empty") +
                " groupFilter=" + useGroupFilter + " motion=" + motion;
            // Logged before the native call so that when the plugin segfaults, the log still names the
            // case that killed it.
            UnityEngine.Debug.Log("JoltProbe case " + label);
            try
            {
                using (var probe = new JoltPhysicsBackend())
                {
                    IntPtr shape = useShape
                        ? JN.Require(JN.JoltC_BoxShapeSettings_CreateShape(new JN.Vec3(0.5f, 0.5f, 0.5f), 0.05f), "box")
                        : JN.Require(JN.JoltC_EmptyShape_Create(new JN.Vec3(0f, 0f, 0f)), "empty");
                    var settings = new JN.BodyCreationSettings();
                    JN.JoltC_BodyCreationSettings_SetDefault(ref settings);
                    settings.motionType = motion;
                    settings.allowedDOFs = JN.AllowedDofsAll;
                    settings.shape = shape;
                    if (useGroupFilter)
                        settings.collisionGroup = new JN.CollisionGroup
                        { groupFilter = probe.groupFilterNative.ToInt64(), groupID = 0, subGroupID = 0 };

                    uint id = JN.JoltC_BodyInterface_CreateAndAddBody(probe.bodyInterface, ref settings, JN.ActivationActivate);
                    report.AppendLine("OK   " + label + " -> bodyId=" + id);
                    if (id != JN.BodyIdInvalid)
                        JN.JoltC_BodyInterface_RemoveAndDestroyBody(probe.bodyInterface, id);
                    if (shape != IntPtr.Zero) JN.JoltC_Shape_Destroy(shape);
                }
            }
            catch (Exception error)
            {
                report.AppendLine("FAIL " + label + ": " + error.GetType().Name + " " + error.Message);
            }
        }
        return report.ToString();
    }

    /// <summary>
    /// Dumps every interop struct's managed size and field offsets next to the native expectation.
    /// </summary>
    /// <remarks>
    /// Exposed publicly because the editor-side audit lives in a different assembly. Keep the type
    /// exposed and the offsets printed side by side: when these drift the failure mode is a native
    /// access violation inside Jolt, not an exception, so this is the fastest way to find it.
    /// </remarks>
    public static string DescribeInteropLayout()
    {
        var builder = new System.Text.StringBuilder();
        foreach (var type in JN.InteropTypes)
        {
            builder.AppendLine("== " + type.Name + " size=" + System.Runtime.InteropServices.Marshal.SizeOf(type));
            foreach (var field in type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                int offset = (int)System.Runtime.InteropServices.Marshal.OffsetOf(type, field.Name);
                string detail = field.FieldType.IsValueType && !field.FieldType.IsEnum
                    ? " (size " + System.Runtime.InteropServices.Marshal.SizeOf(field.FieldType) + ")"
                    : string.Empty;
                builder.AppendLine("   " + offset.ToString().PadLeft(4) + "  " + field.FieldType.Name + " " + field.Name + detail);
            }
        }
        return builder.ToString();
    }

    // -----------------------------------------------------------------
    // Conversions
    // -----------------------------------------------------------------

    private static JN.Vec3 ToJolt(Vector3 v) => new JN.Vec3(v.x, v.y, v.z);

    private static JN.RVec3 ToJoltPosition(Vector3 v) => new JN.RVec3(v.x, v.y, v.z);

    private static JN.Quat ToJolt(Quaternion q) => new JN.Quat(q.x, q.y, q.z, q.w);

    private static Vector3 ToVector(JN.Vec3 v) => new Vector3(v.x, v.y, v.z);

    private static Vector3 ToVector(JN.RVec3 v) => new Vector3(v.x, v.y, v.z);

    private static Quaternion ToQuaternion(JN.Quat q) => new Quaternion(q.x, q.y, q.z, q.w);

    /// <summary>
    /// Converts Unity's freeze bitmask into Jolt's allowed-degrees-of-freedom bitmask.
    /// </summary>
    /// <remarks>
    /// The bit positions happen to agree (X/Y/Z translation then X/Y/Z rotation) but the sense is
    /// inverted: Unity sets a bit to <i>lock</i> an axis, JoltC sets a bit to <i>allow</i> it. Unity's
    /// <see cref="RigidbodyConstraints.FreezeRotation"/> (56) sets all three rotation bits, so passing
    /// the value straight through would lock translation and free rotation - the exact opposite.
    /// </remarks>
    private static int ToAllowedDofs(RigidbodyConstraints constraints)
    {
        int unity = (int)constraints;
        int allowed = 0;
        if ((unity & (int)RigidbodyConstraints.FreezePositionX) == 0) allowed |= JN.AllowedDofsTranslationX;
        if ((unity & (int)RigidbodyConstraints.FreezePositionY) == 0) allowed |= JN.AllowedDofsTranslationY;
        if ((unity & (int)RigidbodyConstraints.FreezePositionZ) == 0) allowed |= JN.AllowedDofsTranslationZ;
        if ((unity & (int)RigidbodyConstraints.FreezeRotationX) == 0) allowed |= JN.AllowedDofsRotationX;
        if ((unity & (int)RigidbodyConstraints.FreezeRotationY) == 0) allowed |= JN.AllowedDofsRotationY;
        if ((unity & (int)RigidbodyConstraints.FreezeRotationZ) == 0) allowed |= JN.AllowedDofsRotationZ;
        return allowed;
    }
}
