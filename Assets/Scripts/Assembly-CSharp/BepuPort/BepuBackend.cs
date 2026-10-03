using System;
using System.Collections.Generic;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using BepuUtilities;
using BepuUtilities.Memory;
using UnityEngine;
using UJoint = UnityEngine.Joint;

/// <summary>
/// BepuPhysics v2 backend for the switchable rigid-body simulation.
/// </summary>
/// <remarks>
/// <para>
/// Ported from PigForge's <c>BepuPhysicsWorld</c> (the headless .NET 10 rewrite), adapted to this
/// project's <see cref="IPhysicsBackend"/> contract. PigForge drives Bepu through its own
/// <c>IPhysicsWorld</c> port with opaque ids and box/sphere shapes only; here Bepu has to mirror the
/// game's existing <see cref="Rigidbody"/>/<see cref="Collider"/> state every fixed step, exactly like
/// <see cref="Box3DBackend"/> does.
/// </para>
/// <para>
/// Pinned to <b>BepuPhysics 2.3.4</b>, not the 2.4.0 PigForge uses: 2.4.0 ships a <c>net6.0</c>-only
/// assembly, while 2.3.4 is the last build with a <c>netstandard2.0</c> target Unity's Mono can load.
/// The API differs accordingly - 2.3.4 has a scalar
/// <see cref="IPoseIntegratorCallbacks.IntegrateVelocity"/>, an <see cref="ITimestepper"/> instead of
/// <c>SolveDescription</c>, and solver iterations on <see cref="Solver.IterationCount"/>.
/// </para>
/// <para>
/// This backend is deliberately <b>single-threaded</b>: it calls the one-argument
/// <see cref="Simulation.Timestep(float)"/>. That keeps the narrow-phase callbacks on the main thread,
/// which matters because the filter callback reads <see cref="GamePhysics.IgnoredPairs"/> - a plain
/// <see cref="HashSet{T}"/> the game mutates from the main thread. Running Bepu multithreaded would
/// introduce exactly the data race documented for the Box3D backend.
/// </para>
/// </remarks>
public sealed partial class BepuBackend : IPhysicsBackend
{
    public PhysicsEngine Engine => PhysicsEngine.Bepu;

    public string DisplayName => "Bepu";

    /// <summary>
    /// Bepu is pure managed and ships every platform in one assembly, so availability is decided by
    /// whether the plugin actually loaded - which is what building a pool proves.
    /// </summary>
    public static bool IsSupported(out string reason)
    {
        reason = null;
        try
        {
            var probe = new BufferPool();
            probe.Clear();
            return true;
        }
        catch (Exception error)
        {
            reason = error.GetType().Name + ": " + error.Message;
            return false;
        }
    }

    private sealed class BodyEntry
    {
        public int instanceId;
        public Rigidbody source;
        public Transform transform;
        public bool isStatic;
        public int staticHandle = -1;
        public int bodyHandle = -1;
        public RigidbodyInterpolation interpolation;
        public Vector3 incomingVelocity;
        public float mass = -1;
        public Vector3 center, inertia;
        public Quaternion inertiaRotation;
        public bool geometryDirty = true;
        public bool useGravity;
        public float linearDamping, angularDamping;
        public bool hasMirrorState;
        public Vector3 mirroredPosition, mirroredVelocity, mirroredAngularVelocity;
        public Quaternion mirroredRotation;
        public int[] colliderIds = Array.Empty<int>();
        public int layer;
        public ulong mask;
        /// <summary>True when the body's shape is a <c>Compound</c> this backend owns and must return to the pool.</summary>
        public bool ownsShape;
        public Buffer<CompoundChild> compoundBuffer;
    }

    private sealed class ShapeEntry
    {
        public Collider source;
        public int instanceId;
        public int owner;
        public TypedIndex index;
        public bool trigger;
        public bool hasIndex;
        public float friction = 0.6f, restitution;
        public PhysicMaterialCombine frictionCombine = PhysicMaterialCombine.Average;
        public PhysicMaterialCombine bounceCombine = PhysicMaterialCombine.Average;
        /// <summary>Collider centre in body-local space; carried into the compound child pose.</summary>
        public Vector3 localOffset;
        /// <summary>Unity capsule direction (0=X, 1=Y, 2=Z); Bepu capsules are Y-aligned.</summary>
        public int capsuleAxis = 1;
        /// <summary>Pooled triangle buffer backing a non-convex <c>Mesh</c> shape.</summary>
        public Buffer<Triangle> meshBuffer;
        /// <summary>Unity layer and the resolved collision mask, kept for per-collider filtering.</summary>
        public int layer;
        public ulong mask;
    }

    private readonly BufferPool pool = new BufferPool();
    private Simulation simulation;
    private readonly Dictionary<int, BodyEntry> bodies = new Dictionary<int, BodyEntry>();
    private readonly Dictionary<int, ShapeEntry> shapes = new Dictionary<int, ShapeEntry>();
    private readonly Dictionary<int, BodyEntry> bodyByHandle = new Dictionary<int, BodyEntry>();
    private readonly Dictionary<int, BodyEntry> staticByHandle = new Dictionary<int, BodyEntry>();
    private readonly List<int> removed = new List<int>();
    private readonly HashSet<int> seen = new HashSet<int>();
    private readonly List<int> colliderScratch = new List<int>();
    private readonly ulong[] layerMasks = new ulong[32];
    private readonly BepuCallbacks callbacks;
    private bool disposed;

    public BepuBackend()
    {
        callbacks = new BepuCallbacks(this);
        simulation = Simulation.Create(pool, new BepuNarrowPhase { owner = callbacks },
            new BepuPoseIntegrator { owner = callbacks }, new PositionLastTimestepper());
        simulation.Solver.IterationCount = Mathf.Clamp(Physics.defaultSolverIterations, 1, 10);
        CreateAnchor();
    }

    // ------------------------------------------------------------------ filtering

    internal ulong[] LayerMasks => layerMasks;

    /// <summary>
    /// True when the pair may generate contacts. Mirrors what the other backends do: layer matrix,
    /// <c>detectCollisions</c>, and the <see cref="GamePhysics.IgnoredPairs"/> set.
    /// </summary>
    /// <remarks>
    /// Called from the narrow phase. Because <see cref="Step"/> uses the single-threaded
    /// <see cref="Simulation.Timestep(float)"/> overload this always runs on the main thread, so
    /// reading <see cref="GamePhysics.IgnoredPairs"/> here is safe.
    /// </remarks>
    internal bool AllowsPair(in CollidableReference a, in CollidableReference b)
    {
        if (!TryGetEntry(a, out var ea) || !TryGetEntry(b, out var eb)) return true;
        if (ea.isStatic && eb.isStatic) return false;
        if (ea.mask == 0UL || eb.mask == 0UL) return false;
        if ((ea.mask & (1UL << eb.layer)) == 0UL || (eb.mask & (1UL << ea.layer)) == 0UL) return false;
        var idsA = ea.colliderIds;
        var idsB = eb.colliderIds;
        for (int i = 0; i < idsA.Length; i++)
            for (int j = 0; j < idsB.Length; j++)
                if (GamePhysics.IgnoredPairs.Contains(GamePhysics.PairKey(idsA[i], idsB[j])))
                    return false;
        return true;
    }

    private bool TryGetEntry(in CollidableReference reference, out BodyEntry entry)
    {
        entry = null;
        return reference.Mobility == CollidableMobility.Static
            ? staticByHandle.TryGetValue(reference.StaticHandle.Value, out entry)
            : bodyByHandle.TryGetValue(reference.BodyHandle.Value, out entry);
    }

    private static ulong MobilityKey(in CollidableReference reference) =>
        reference.Mobility == CollidableMobility.Static
            ? ((ulong)reference.StaticHandle.Value << 1) | 1UL
            : (ulong)reference.BodyHandle.Value << 1;

    // ------------------------------------------------------------------ interface

    public void Wake(Rigidbody body)
    {
        if (!TryFind(body, out var entry) || entry.isStatic || entry.bodyHandle < 0) return;
        // BodyReference is a view over the body's memory, but C# still refuses a property write on a
        // method return value, so the reference has to land in a local first.
        var reference = simulation.Bodies.GetBodyReference(new BodyHandle(entry.bodyHandle));
        reference.Awake = true;
    }

    public void Sleep(Rigidbody body)
    {
        if (!TryFind(body, out var entry) || entry.isStatic || entry.bodyHandle < 0) return;
        var reference = simulation.Bodies.GetBodyReference(new BodyHandle(entry.bodyHandle));
        reference.Velocity = new BodyVelocity(ToNumerics(body.velocity), ToNumerics(body.angularVelocity));
        entry.mirroredVelocity = body.velocity;
        entry.mirroredAngularVelocity = body.angularVelocity;
        reference.Awake = false;
    }

    public bool IsSleeping(Rigidbody body)
    {
        if (!TryFind(body, out var entry) || entry.isStatic || entry.bodyHandle < 0) return false;
        var reference = simulation.Bodies.GetBodyReference(new BodyHandle(entry.bodyHandle));
        return reference.Exists && !reference.Awake;
    }

    /// <summary>
    /// Nothing to invalidate: Bepu re-runs the filter callback for every broad-phase pair each step, so
    /// a changed ignore-pair is honoured on the next <see cref="Step"/> with no cached state to flush.
    /// </summary>
    public void Refilter(Collider a, Collider b) { }

    private bool TryFind(Rigidbody body, out BodyEntry entry)
    {
        entry = null;
        return body && bodies.TryGetValue(body.GetInstanceID(), out entry);
    }

    private BodyEntry FindByOwner(int ownerInstanceId) =>
        bodies.TryGetValue(ownerInstanceId, out var entry) ? entry : null;

    // ------------------------------------------------------------------ synchronize

    public void Synchronize()
    {
        if (disposed) throw new ObjectDisposedException(nameof(BepuBackend));
        GamePhysics.ReadLayerCollisionMasks(layerMasks);
        foreach (var rb in GamePhysics.FindSceneObjects<Rigidbody>()) EnsureBody(rb, rb.transform);
        seen.Clear();
        foreach (var collider in GamePhysics.FindSceneObjects<Collider>())
        {
            if (!collider.enabled || !collider.gameObject.activeInHierarchy) continue;
            if (!IsSupportedCollider(collider)) continue;
            int id = collider.GetInstanceID();
            seen.Add(id);
            var rb = collider.attachedRigidbody;
            var owner = EnsureBody(rb, rb ? rb.transform : collider.transform);
            shapes.TryGetValue(id, out var entry);
            if (entry != null && (entry.owner != OwnerKey(owner) || entry.trigger != collider.isTrigger ||
                entry.source == null || entry.source.GetType() != collider.GetType()))
            {
                ReleaseShape(entry);
                shapes.Remove(id);
                entry = null;
            }
            if (entry == null)
            {
                entry = new ShapeEntry
                {
                    source = collider, instanceId = id, owner = OwnerKey(owner),
                    trigger = collider.isTrigger
                };
                if (CreateShape(entry)) shapes.Add(id, entry);
                else continue;
                owner.geometryDirty = true;
            }
            int layer = collider.gameObject.layer;
            ulong mask = rb && !rb.detectCollisions ? 0UL : layerMasks[layer];
            var material = collider.sharedMaterial;
            entry.layer = layer; entry.mask = mask;
            entry.friction = material ? material.dynamicFriction : 0.6f;
            entry.restitution = material ? material.bounciness : 0f;
            entry.frictionCombine = material ? material.frictionCombine : PhysicMaterialCombine.Average;
            entry.bounceCombine = material ? material.bounceCombine : PhysicMaterialCombine.Average;
        }
        removed.Clear();
        foreach (var pair in shapes) if (!seen.Contains(pair.Key)) removed.Add(pair.Key);
        foreach (int id in removed) { ReleaseShape(shapes[id]); shapes.Remove(id); }

        removed.Clear();
        foreach (var pair in bodies)
        {
            var entry = pair.Value;
            if (!entry.transform || !entry.transform.gameObject.activeInHierarchy ||
                (!ReferenceEquals(entry.source, null) && !entry.source)) removed.Add(pair.Key);
        }
        foreach (int id in removed) RemoveBody(bodies[id]);
        RebuildDirtyBodies();
        // Layer rules and detectCollisions can change without changing geometry. The narrow phase
        // reads BodyEntry masks, so refreshing only ShapeEntry masks leaves its filter stale.
        foreach (var entry in bodies.Values)
        {
            entry.layer = entry.transform.gameObject.layer;
            entry.mask = entry.source && !entry.source.detectCollisions ? 0UL : layerMasks[entry.layer];
        }
        SynchronizeJoints();
    }

    private static int OwnerKey(BodyEntry entry) =>
        entry.source ? entry.source.GetInstanceID() : entry.transform.GetInstanceID();

    private BodyEntry EnsureBody(Rigidbody rb, Transform transform)
    {
        int id = rb ? rb.GetInstanceID() : transform.GetInstanceID();
        if (bodies.TryGetValue(id, out var entry)) return entry;
        entry = new BodyEntry
        {
            instanceId = id,
            source = rb,
            transform = transform,
            isStatic = rb == null,
            interpolation = rb ? rb.interpolation : RigidbodyInterpolation.None
        };
        bodies.Add(id, entry);
        return entry;
    }

    /// <summary>
    /// Builds the native body once its colliders are known. A Bepu body owns exactly one shape, so a
    /// part with several colliders is mirrored as a <see cref="Compound"/>; a single collider - the
    /// common case - stays a direct shape.
    /// </summary>
    private void RebuildDirtyBodies()
    {
        foreach (var pair in bodies)
        {
            var entry = pair.Value;
            if (!entry.geometryDirty) continue;
            entry.geometryDirty = false;
            colliderScratch.Clear();
            int owner = OwnerKey(entry);
            foreach (var shape in shapes) if (shape.Value.owner == owner) colliderScratch.Add(shape.Key);
            entry.colliderIds = colliderScratch.Count > 0 ? colliderScratch.ToArray() : Array.Empty<int>();
            entry.layer = entry.transform ? entry.transform.gameObject.layer : 0;
            entry.mask = entry.source && !entry.source.detectCollisions ? 0UL : layerMasks[entry.layer];
            DestroyNative(entry);
            CreateNative(entry);
        }
    }

    private void RemoveBody(BodyEntry entry)
    {
        if (entry.source) entry.source.interpolation = entry.interpolation;
        DestroyNative(entry);
        // A destroyed Rigidbody compares equal to null, but its entry was keyed by the Rigidbody ID,
        // not its Transform ID. Keep the original key so destroyed parts actually leave the registry.
        bodies.Remove(entry.instanceId);
    }

    private void DestroyNative(BodyEntry entry)
    {
        if (entry.bodyHandle >= 0)
        {
            if (simulation.Bodies.BodyExists(new BodyHandle(entry.bodyHandle)))
                simulation.Bodies.Remove(new BodyHandle(entry.bodyHandle));
            bodyByHandle.Remove(entry.bodyHandle);
            entry.bodyHandle = -1;
        }
        if (entry.staticHandle >= 0)
        {
            if (simulation.Statics.StaticExists(new StaticHandle(entry.staticHandle)))
                simulation.Statics.Remove(new StaticHandle(entry.staticHandle));
            staticByHandle.Remove(entry.staticHandle);
            entry.staticHandle = -1;
        }
        ReleaseOwnedShape(entry);
    }

    private void CreateNative(BodyEntry entry)
    {
        entry.hasMirrorState = false;
        if (!TryBuildShapeFor(entry, out var shapeIndex)) return;
        var pose = new BepuPhysics.RigidPose(ToNumerics(entry.transform.position), ToNumerics(entry.transform.rotation));
        var collidable = new CollidableDescription(shapeIndex, 0.1f);
        // Only a collider with no Rigidbody at all becomes a Bepu static. A frozen kinematic body must
        // stay a body: RigidbodyFreezer unfreezes it later, and a static has no pose to write back.
        if (entry.isStatic)
        {
            entry.staticHandle = simulation.Statics.Add(new StaticDescription(pose.Position, pose.Orientation, collidable)).Value;
            staticByHandle[entry.staticHandle] = entry;
            return;
        }
        // Sleeping is disabled: the game drives sleep explicitly through RigidbodyFreezer/EngineSleep,
        // and a native sleep threshold would disagree with the PhysX mirror of rb.sleepThreshold.
        var activity = new BodyActivityDescription(-1f, 32);
        BodyDescription description = entry.source.isKinematic
            ? BodyDescription.CreateKinematic(pose, collidable, activity)
            : BodyDescription.CreateDynamic(pose, BuildInertia(entry), collidable, activity);
        entry.bodyHandle = simulation.Bodies.Add(description).Value;
        bodyByHandle[entry.bodyHandle] = entry;
        if (entry.source) entry.source.interpolation = RigidbodyInterpolation.None;
    }

    // ------------------------------------------------------------------ step

    public void Step(float delta)
    {
        lastDelta = delta;
        callbacks.BeginStep();
        foreach (var pair in bodies)
        {
            var entry = pair.Value;
            if (entry.isStatic || entry.bodyHandle < 0) continue;
            var rb = entry.source;
            if (!rb) continue;
            entry.useGravity = rb.useGravity;
            entry.linearDamping = Mathf.Max(0f, rb.drag);
            entry.angularDamping = Mathf.Max(0f, rb.angularDrag);
            var body = simulation.Bodies.GetBodyReference(new BodyHandle(entry.bodyHandle));
            if (!body.Exists) continue;
            if (rb.isKinematic)
            {
                body.Pose = new RigidPose(ToNumerics(rb.position), ToNumerics(rb.rotation));
                body.Velocity = new BodyVelocity(ToNumerics(rb.velocity), ToNumerics(rb.angularVelocity));
            }
            else
            {
                if (entry.mass != rb.mass || entry.center != rb.centerOfMass ||
                    entry.inertia != rb.inertiaTensor || entry.inertiaRotation != rb.inertiaTensorRotation)
                {
                    body.Awake = true;
                    body.SetLocalInertia(BuildInertia(entry));
                    entry.mass = rb.mass; entry.center = rb.centerOfMass;
                    entry.inertia = rb.inertiaTensor; entry.inertiaRotation = rb.inertiaTensorRotation;
                }
                var pose = new RigidPose(ToNumerics(rb.position), ToNumerics(rb.rotation));
                var velocity = new BodyVelocity(ToNumerics(rb.velocity), ToNumerics(rb.angularVelocity));
                // A sleeping body stays asleep until the game changes its state or explicitly wakes it.
                // Waking every mirror step defeated EngineSleep and forced the solver to process it.
                // Compare with the last facade values, not the native quaternion: Unity normalizes
                // quaternion writes, and that round-trip alone must not wake a rotated sleeping body.
                bool changed = !entry.hasMirrorState || !rb.position.Equals(entry.mirroredPosition) ||
                    !rb.rotation.Equals(entry.mirroredRotation) || !rb.velocity.Equals(entry.mirroredVelocity) ||
                    !rb.angularVelocity.Equals(entry.mirroredAngularVelocity);
                if (changed)
                {
                    body.Awake = true;
                    body.Pose = pose;
                    body.Velocity = velocity;
                }
            }
            entry.incomingVelocity = rb.velocity;
        }

        simulation.Timestep(delta);

        foreach (var pair in bodies)
        {
            var entry = pair.Value;
            if (entry.isStatic || entry.bodyHandle < 0) continue;
            var rb = entry.source;
            if (!rb || rb.isKinematic) continue;
            var body = simulation.Bodies.GetBodyReference(new BodyHandle(entry.bodyHandle));
            if (!body.Exists) continue;
            rb.position = ToUnity(body.Pose.Position);
            rb.rotation = ToUnity(body.Pose.Orientation);
            rb.velocity = ToUnity(body.Velocity.Linear);
            rb.angularVelocity = ToUnity(body.Velocity.Angular);
            entry.transform.SetPositionAndRotation(rb.position, rb.rotation);
            entry.mirroredPosition = rb.position;
            entry.mirroredRotation = rb.rotation;
            entry.mirroredVelocity = rb.velocity;
            entry.mirroredAngularVelocity = rb.angularVelocity;
            entry.hasMirrorState = true;
        }
        Physics.SyncTransforms();
        DispatchEvents();
        BreakJoints();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        foreach (var e in bodies.Values) if (e.source) e.source.interpolation = e.interpolation;
        simulation.Dispose();
        pool.Clear();
        bodies.Clear(); shapes.Clear(); bodyByHandle.Clear(); staticByHandle.Clear();
        joints.Clear(); pendingJointDestruction.Clear();
    }

    internal void IntegrateVelocity(int bodyIndex, System.Numerics.Vector3 gravityDt, float delta, ref BodyVelocity velocity)
    {
        // The scalar callback receives an index in Bepu's active set, not a persistent body handle.
        int handle = simulation.Bodies.ActiveSet.IndexToHandle[bodyIndex].Value;
        if (!bodyByHandle.TryGetValue(handle, out var entry)) return;
        if (entry.useGravity) velocity.Linear += gravityDt;
        velocity.Linear *= Mathf.Max(0f, 1f - entry.linearDamping * delta);
        velocity.Angular *= Mathf.Max(0f, 1f - entry.angularDamping * delta);
    }
}
