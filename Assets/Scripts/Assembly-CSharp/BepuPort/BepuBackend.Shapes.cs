using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using BepuUtilities;
using BepuUtilities.Memory;
using UnityEngine;

/// <summary>One narrow-phase contact sample, collected while <see cref="Simulation.Timestep(float)"/> runs.</summary>
internal struct BepuContactSample
{
    public ulong pairKey;
    public bool trigger;
    public CollidableReference a, b;
    public Vector3 offset, normal;
    public float depth;
}

/// <summary>
/// Shared state handed to the Bepu callbacks. The callbacks are structs - Bepu stores them by value -
/// so the mutable half lives behind this reference, which the backend reads after each step.
/// </summary>
internal sealed class BepuCallbacks
{
    public readonly BepuBackend backend;
    public readonly List<BepuContactSample> samples = new List<BepuContactSample>(256);
    public readonly HashSet<ulong> contactPairs = new HashSet<ulong>();
    public readonly HashSet<ulong> triggerPairs = new HashSet<ulong>();
    /// <summary>
    /// Pair key -> the two collidables. Kept because <see cref="CollidableReference"/> cannot be
    /// rebuilt from the key alone without guessing which id space a handle came from.
    /// </summary>
    public readonly Dictionary<ulong, (CollidableReference a, CollidableReference b)> pairRefs =
        new Dictionary<ulong, (CollidableReference, CollidableReference)>();

    public BepuCallbacks(BepuBackend backend) { this.backend = backend; }

    public void BeginStep()
    {
        samples.Clear();
        contactPairs.Clear();
        triggerPairs.Clear();
        pairRefs.Clear();
    }

    public void Register(ulong key, in CollidablePair pair)
    {
        if (!pairRefs.ContainsKey(key)) pairRefs[key] = (pair.A, pair.B);
    }
}

/// <summary>
/// Narrow-phase callbacks. Bepu 2.3.4's <see cref="INarrowPhaseCallbacks.AllowContactGeneration(int, CollidableReference, CollidableReference)"/>
/// takes no speculative-margin parameter (2.4 added one), and the contact callback reports the target
/// as <c>out</c> rather than <c>ref</c>.
/// </summary>
internal struct BepuNarrowPhase : INarrowPhaseCallbacks
{
    public BepuCallbacks owner;

    public void Initialize(Simulation simulation) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b)
    {
        if (!owner.backend.AllowsPair(a, b)) return false;
        // Static-static is already refused by the filter; two kinematics cannot respond to each other.
        return a.Mobility != CollidableMobility.Kinematic || b.Mobility != CollidableMobility.Kinematic;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AllowContactGeneration(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB) => true;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold,
        out PairMaterialProperties pairMaterial) where TManifold : struct, IContactManifold<TManifold>
    {
        pairMaterial.FrictionCoefficient = MixFriction(pair);
        // Bepu v2 has no restitution: bounciness is a PhysX/Box3D-only concept here.
        pairMaterial.MaximumRecoveryVelocity = 2f;
        pairMaterial.SpringSettings = new SpringSettings(30f, 1f);
        if (manifold.Count > 0) Record(pair, manifold);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ConfigureContactManifold(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB,
        ref ConvexContactManifold manifold)
    {
        if (manifold.Count > 0) RecordConvex(pair, ref manifold);
        return true;
    }

    public void Dispose() { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float MixFriction(CollidablePair pair)
    {
        float a = owner.backend.FrictionOf(pair.A);
        float b = owner.backend.FrictionOf(pair.B);
        return (a + b) * 0.5f;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Record<TManifold>(CollidablePair pair, TManifold manifold)
        where TManifold : struct, IContactManifold<TManifold>
    {
        bool trigger = owner.backend.IsTrigger(pair.A) || owner.backend.IsTrigger(pair.B);
        ulong key = BepuBackend.PairKey(pair.A, pair.B);
        owner.Register(key, pair);
        if (trigger) owner.triggerPairs.Add(key); else owner.contactPairs.Add(key);
        for (int i = 0; i < manifold.Count; i++)
        {
            manifold.GetContact(i, out var offset, out var normal, out var depth, out _);
            owner.samples.Add(new BepuContactSample
            {
                pairKey = key, trigger = trigger, a = pair.A, b = pair.B,
                offset = new Vector3(offset.X, offset.Y, offset.Z),
                normal = new Vector3(normal.X, normal.Y, normal.Z), depth = depth
            });
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecordConvex(CollidablePair pair, ref ConvexContactManifold manifold)
    {
        bool trigger = owner.backend.IsTrigger(pair.A) || owner.backend.IsTrigger(pair.B);
        ulong key = BepuBackend.PairKey(pair.A, pair.B);
        owner.Register(key, pair);
        if (trigger) owner.triggerPairs.Add(key); else owner.contactPairs.Add(key);
        for (int i = 0; i < manifold.Count; i++)
        {
            manifold.GetContact(i, out var offset, out var normal, out var depth, out _);
            owner.samples.Add(new BepuContactSample
            {
                pairKey = key, trigger = trigger, a = pair.A, b = pair.B,
                offset = new Vector3(offset.X, offset.Y, offset.Z),
                normal = new Vector3(normal.X, normal.Y, normal.Z), depth = depth
            });
        }
    }
}

/// <summary>
/// Pose integrator. Bepu 2.3.4 uses the scalar callback and hands <c>dt</c> to
/// <see cref="PrepareForIntegration"/> rather than to <see cref="IntegrateVelocity"/>, so the gravity
/// increment is precomputed once per step.
/// </summary>
internal struct BepuPoseIntegrator : IPoseIntegratorCallbacks
{
    public BepuCallbacks owner;
    private System.Numerics.Vector3 gravityDt;
    private float delta;

    public AngularIntegrationMode AngularIntegrationMode => AngularIntegrationMode.Nonconserving;

    public void Initialize(Simulation simulation) { }

    public void PrepareForIntegration(float dt)
    {
        delta = dt;
        Vector3 gravity = Physics.gravity * dt;
        gravityDt = new System.Numerics.Vector3(gravity.x, gravity.y, gravity.z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IntegrateVelocity(int bodyIndex, in RigidPose position, in BodyInertia localInertia,
        int workerIndex, ref BodyVelocity velocity)
    {
        if (localInertia.InverseMass > 0f) owner.backend.IntegrateVelocity(bodyIndex, gravityDt, delta, ref velocity);
    }
}

public sealed partial class BepuBackend
{
    /// <summary>Stable key for a collidable pair. Body and static handles are separate id spaces.</summary>
    internal static ulong PairKey(in CollidableReference a, in CollidableReference b)
    {
        ulong x = MobilityKey(a), y = MobilityKey(b);
        return x < y ? (x << 32) | y : (y << 32) | x;
    }

    internal bool IsTrigger(in CollidableReference reference) =>
        TryGetEntry(reference, out var entry) && entry.colliderIds.Length > 0 &&
        shapes.TryGetValue(entry.colliderIds[0], out var shape) && shape.trigger;

    internal float FrictionOf(in CollidableReference reference)
    {
        if (!TryGetEntry(reference, out var entry) || entry.colliderIds.Length == 0) return 0.6f;
        return shapes.TryGetValue(entry.colliderIds[0], out var shape) ? shape.friction : 0.6f;
    }

    // ------------------------------------------------------------------ collider support

    /// <summary>
    /// Colliders this backend can mirror. Anything else is skipped rather than throwing, so a level that
    /// uses an unsupported collider type does not take the whole engine switch down with it - the
    /// collider simply has no native counterpart.
    /// </summary>
    private static bool IsSupportedCollider(Collider collider) =>
        collider is BoxCollider || collider is SphereCollider || collider is CapsuleCollider ||
        collider is MeshCollider;

    private bool CreateShape(ShapeEntry entry)
    {
        Vector3 lossy = entry.source.transform.lossyScale;
        var scale = new Vector3(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y), Mathf.Abs(lossy.z));
        Vector3 local = LocalOffset(entry.source);
        try
        {
            switch (entry.source)
            {
                case BoxCollider box:
                {
                    Vector3 size = Vector3.Scale(box.size, scale);
                    entry.index = simulation.Shapes.Add(new Box(
                        Mathf.Max(0.0001f, size.x), Mathf.Max(0.0001f, size.y), Mathf.Max(0.0001f, size.z)));
                    break;
                }
                case SphereCollider sphere:
                {
                    float radius = Mathf.Max(0.0001f, sphere.radius * Mathf.Max(scale.x, scale.y, scale.z));
                    entry.index = simulation.Shapes.Add(new Sphere(radius));
                    break;
                }
                case CapsuleCollider capsule:
                {
                    int axis = capsule.direction;
                    float radius = Mathf.Max(0.0001f,
                        capsule.radius * Mathf.Max(scale[(axis + 1) % 3], scale[(axis + 2) % 3]));
                    float length = Mathf.Max(0f, capsule.height * scale[axis] - 2f * radius);
                    // Bepu capsules point along local Y; rotate the local offset into that frame later.
                    entry.index = simulation.Shapes.Add(new Capsule(radius, length));
                    entry.capsuleAxis = axis;
                    break;
                }
                case MeshCollider meshCollider when meshCollider.sharedMesh:
                {
                    var mesh = meshCollider.sharedMesh;
                    var vertices = mesh.vertices;
                    var indices = mesh.triangles;
                    int triangleCount = indices.Length / 3;
                    pool.Take<Triangle>(triangleCount, out var triangles);
                    for (int i = 0; i < triangleCount; i++)
                    {
                        triangles[i] = new Triangle(
                            ToNumerics(vertices[indices[i * 3]]),
                            ToNumerics(vertices[indices[i * 3 + 1]]),
                            ToNumerics(vertices[indices[i * 3 + 2]]));
                    }
                    entry.meshBuffer = triangles;
                    entry.index = simulation.Shapes.Add(new BepuPhysics.Collidables.Mesh(triangles, ToNumerics(scale), pool));
                    break;
                }
                default:
                    return false;
            }
            entry.hasIndex = true;
            entry.localOffset = local;
            return true;
        }
        catch (NotSupportedException)
        {
            throw;
        }
        catch (Exception)
        {
            // A shape Bepu refuses (degenerate geometry, unsupported topology) is not worth failing the
            // whole engine switch over: report it as absent and let the collider have no counterpart.
            return false;
        }
    }

    private static Vector3 LocalOffset(Collider collider)
    {
        switch (collider)
        {
            case BoxCollider box: return box.center;
            case SphereCollider sphere: return sphere.center;
            case CapsuleCollider capsule: return capsule.center;
            default: return Vector3.zero;
        }
    }

    // ------------------------------------------------------------------ body shape assembly

    /// <summary>
    /// Composes the body's single Bepu shape from its colliders. One collider with no offset is used
    /// directly; everything else becomes a <see cref="Compound"/>. A non-convex <see cref="Mesh"/> can
    /// only be used directly, so a mesh collider is never compounded.
    /// </summary>
    private bool TryBuildShapeFor(BodyEntry entry, out TypedIndex index)
    {
        index = default;
        var ids = entry.colliderIds;
        if (ids.Length == 0) return false;
        if (ids.Length == 1 && shapes.TryGetValue(ids[0], out var single) && single.hasIndex)
        {
            index = single.index;
            entry.ownsShape = false;
            return true;
        }
        var children = new List<CompoundChild>(ids.Length);
        foreach (int id in ids)
        {
            if (!shapes.TryGetValue(id, out var shape) || !shape.hasIndex) continue;
            if (shape.source is MeshCollider) continue; // non-convex: cannot be a compound child
            var pose = new RigidPose(ToNumerics(shape.localOffset), ToNumerics(shape.source.transform.localRotation));
            children.Add(new CompoundChild { ShapeIndex = shape.index, LocalPose = pose });
        }
        if (children.Count == 0) return false;
        if (children.Count == 1)
        {
            index = children[0].ShapeIndex;
            entry.ownsShape = false;
            return true;
        }
        pool.Take<CompoundChild>(children.Count, out var buffer);
        for (int i = 0; i < children.Count; i++) buffer[i] = children[i];
        entry.compoundBuffer = buffer;
        index = simulation.Shapes.Add(new Compound(buffer));
        entry.ownsShape = true;
        return true;
    }

    private void ReleaseShape(ShapeEntry entry)
    {
        // The owning body references this shape, so drop the body first - Bepu would otherwise be left
        // holding a disposed shape index.
        var owner = FindByOwner(entry.owner);
        if (owner != null) { DestroyNative(owner); owner.geometryDirty = true; }
        if (entry.hasIndex)
        {
            simulation.Shapes.RemoveAndDispose(entry.index, pool);
            entry.hasIndex = false;
        }
        if (entry.meshBuffer.Allocated) { pool.Return(ref entry.meshBuffer); }
    }

    private void ReleaseOwnedShape(BodyEntry entry)
    {
        if (!entry.ownsShape) return;
        if (entry.compoundBuffer.Allocated) { pool.Return(ref entry.compoundBuffer); }
        entry.ownsShape = false;
    }

    // ------------------------------------------------------------------ inertia

    private static BodyInertia BuildInertia(BodyEntry entry)
    {
        var rb = entry.source;
        if (rb == null || rb.mass <= 0f || rb.isKinematic) return default;
        float inverseMass = 1f / rb.mass;
        Vector3 i = rb.inertiaTensor;
        // Unity reports zero principal inertia for a frozen rotation axis; that means infinite inertia,
        // so the inverse is zero there rather than a huge finite value.
        var inverse = new System.Numerics.Vector3(
            i.x > 0f ? 1f / i.x : 0f,
            i.y > 0f ? 1f / i.y : 0f,
            i.z > 0f ? 1f / i.z : 0f);
        var rotation = rb.inertiaTensorRotation;
        // Local inertia tensor = R * diag(inverse) * R^T
        var m = System.Numerics.Matrix4x4.CreateFromQuaternion(
            new System.Numerics.Quaternion(rotation.x, rotation.y, rotation.z, rotation.w));
        var diagonal = System.Numerics.Matrix4x4.CreateScale(inverse);
        var tensor = System.Numerics.Matrix4x4.Multiply(System.Numerics.Matrix4x4.Multiply(m, diagonal),
            System.Numerics.Matrix4x4.Transpose(m));
        return new BodyInertia
        {
            InverseMass = inverseMass,
            InverseInertiaTensor = new Symmetric3x3
            {
                XX = tensor.M11, YX = tensor.M21, YY = tensor.M22,
                ZX = tensor.M31, ZY = tensor.M32, ZZ = tensor.M33
            }
        };
    }

    // ------------------------------------------------------------------ conversions

    private static System.Numerics.Vector3 ToNumerics(Vector3 value) => new System.Numerics.Vector3(value.x, value.y, value.z);
    private static System.Numerics.Quaternion ToNumerics(Quaternion value) => new System.Numerics.Quaternion(value.x, value.y, value.z, value.w);
    private static Vector3 ToUnity(System.Numerics.Vector3 value) => new Vector3(value.X, value.Y, value.Z);
    private static Quaternion ToUnity(System.Numerics.Quaternion value) => new Quaternion(value.X, value.Y, value.Z, value.W);
}
