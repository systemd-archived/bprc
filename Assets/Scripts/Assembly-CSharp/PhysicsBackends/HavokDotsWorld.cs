#if HAVOK_PHYSICS
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UBoxCollider = UnityEngine.BoxCollider;
using UCapsuleCollider = UnityEngine.CapsuleCollider;
using UCollider = UnityEngine.Collider;
using UDebug = UnityEngine.Debug;
using UFixedJoint = UnityEngine.FixedJoint;
using UHingeJoint = UnityEngine.HingeJoint;
using UJoint = UnityEngine.Joint;
using UMaterial = UnityEngine.Material;
using UMathf = UnityEngine.Mathf;
using UMesh = UnityEngine.Mesh;
using UMeshCollider = UnityEngine.MeshCollider;
using UObject = UnityEngine.Object;
using UPhysics = UnityEngine.Physics;
using UQuaternion = UnityEngine.Quaternion;
using URigidbody = UnityEngine.Rigidbody;
using URigidbodyConstraints = UnityEngine.RigidbodyConstraints;
using USphereCollider = UnityEngine.SphereCollider;
using USpringJoint = UnityEngine.SpringJoint;
using UTerrainCollider = UnityEngine.TerrainCollider;
using UTerrainData = UnityEngine.TerrainData;
using UTime = UnityEngine.Time;
using UTransform = UnityEngine.Transform;
using UVector3 = UnityEngine.Vector3;

/// <summary>
/// The DOTS half of the Havok backend: a private ECS world whose <c>PhysicsStep</c> selects the Havok
/// solver, with the game's Unity rigidbodies mirrored in at the start of each step and read back at
/// the end.
/// </summary>
/// <remarks>
/// <para>
/// Havok Physics for Unity is not a standalone rigid-body API — it is a solver for the DOTS
/// <c>Unity.Physics</c> package. Selecting it means adding the <c>PhysicsStep</c> singleton and
/// setting <c>SimulationType.HavokPhysics</c>, which is exactly what the reference
/// <c>bpsim_2023.0</c> project does in <c>PartSimulator.CreatePhysicsStep</c>. Everything here mirrors
/// that approach; the collider, mass and transform calls follow <c>PartPhysics</c> from the same
/// project.
/// </para>
/// <para>
/// This file is compiled only when <c>HAVOK_PHYSICS</c> is defined, which is what keeps the project
/// building on Unity 2021.3 where none of these packages exist. The define is managed by
/// <c>Assets/Editor/HavokPhysicsSetup.cs</c>.
/// </para>
/// <para>
/// <b>Verification status.</b> This code has been compiled against the Havok and DOTS assemblies that
/// ship inside <c>bpsim_2023.0/Assets/Plugins</c> (see <c>Tools/compile-check.ps1 -HavokProbe</c>), so
/// the API usage is checked. It has never been executed: that needs Unity 2022.2+ with
/// <c>com.unity.physics</c> and <c>com.havok.physics</c> installed and a Pro/Enterprise/Industry
/// licence, none of which exist on the machine this port was written on. The gaps that remain are
/// listed in <c>docs/HavokPhysics.md</c>.
/// </para>
/// </remarks>
public sealed class HavokDotsWorld : IDisposable
{
    private const int SolverIterations = 8;

    private readonly World m_world;
    private readonly EntityManager m_entities;
    private readonly Entity m_step;
    private readonly Dictionary<int, Body> m_bodies = new Dictionary<int, Body>();
    private readonly List<int> m_stale = new List<int>();
    private readonly List<int> m_staleChildren = new List<int>();
    private readonly HashSet<int> m_seenColliders = new HashSet<int>();
    private readonly List<BlobAssetReference<Collider>> m_owned = new List<BlobAssetReference<Collider>>();
    private readonly HashSet<string> m_warned = new HashSet<string>();
    private readonly ulong[] m_layerMasks = new ulong[32];
    private readonly Dictionary<int, JointEntry> m_joints = new Dictionary<int, JointEntry>();
    private readonly List<int> m_staleJoints = new List<int>();
    private readonly HashSet<int> m_seenJoints = new HashSet<int>();
    private readonly HashSet<ulong> m_appliedIgnores = new HashSet<ulong>();
    private readonly List<ulong> m_removedIgnores = new List<ulong>();
    /// <summary>Collider instance id to shared collision-group id, derived from the ignored pairs.</summary>
    private readonly Dictionary<int, int> m_groupByCollider = new Dictionary<int, int>();
    private float m_elapsed;
    private bool m_disposed;

    /// <summary>
    /// One mirrored joint: a constraint entity plus the two bodies it joins.
    /// </summary>
    private sealed class JointEntry
    {
        public Entity Entity;
        /// <summary>
        /// The second constraint entity carrying a motorised hinge's angular-velocity motor, or
        /// <see cref="Entity.Null"/> when the joint has no motor. One <c>PhysicsJoint</c> cannot express
        /// both the hinge and its motor, so the two live on separate entities over the same body pair.
        /// </summary>
        public Entity MotorEntity;
        public UJoint Source;
        /// <summary>Hash of every joint property that affects the constraint; see <see cref="JointSignature"/>.</summary>
        public int Signature;
        public Body A;
        public Body B;
        public URigidbody Ra;
        public URigidbody Rb;
    }

    private sealed class Body
    {
        public Entity Entity;
        public URigidbody Source;
        public UTransform Transform;
        /// <summary>Every collider currently mirrored on this body, by collider instance id.</summary>
        public readonly Dictionary<int, Child> Children = new Dictionary<int, Child>();
        public bool Kinematic;
        public bool Sleeping;
    }

    /// <summary>
    /// One collider of a body. Unity.Physics carries a single collider per entity, so a body with
    /// several colliders needs one child entity per collider, parented to the body's root entity.
    /// </summary>
    private sealed class Child
    {
        public Entity Entity;
        public UCollider Source;
        public BlobAssetReference<Collider> Blob;
        public int Layer;
        public bool Trigger;
    }

    public HavokDotsWorld()
    {
        m_world = new World("BPLE Havok Physics");
        m_entities = m_world.EntityManager;

        // Populate the world with the same systems the default world gets, so BuildPhysicsWorld /
        // StepPhysicsWorld / ExportPhysicsWorld run when the world is updated.
        DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(
            m_world, DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default));

        // The singleton that makes this world solve with Havok instead of Unity Physics.
        m_step = m_entities.CreateEntity();
        PhysicsStep step = PhysicsStep.Default;
        step.SimulationType = SimulationType.HavokPhysics;
        step.Gravity = new float3(UPhysics.gravity.x, UPhysics.gravity.y, UPhysics.gravity.z);
        step.SolverIterationCount = SolverIterations;
        m_entities.AddComponentData(m_step, step);

        RefreshLayerMasks();
    }

    // ---------------------------------------------------------------------------------------------
    // Mirror in
    // ---------------------------------------------------------------------------------------------

    public void Synchronize()
    {
        if (m_disposed) throw new ObjectDisposedException("HavokDotsWorld");

        RefreshLayerMasks();

        // Discovery also catches parts spawned by legacy code without changing prefab serialization.
        foreach (URigidbody source in GamePhysics.FindSceneObjects<URigidbody>()) EnsureBody(source);

        m_stale.Clear();
        foreach (var pair in m_bodies)
        {
            Body body = pair.Value;
            if (body.Source == null || body.Transform == null || !body.Transform.gameObject.activeInHierarchy)
                m_stale.Add(pair.Key);
        }
        for (int i = 0; i < m_stale.Count; i++) RemoveBody(m_stale[i]);

        // Colliders are collected per body first, because a body's mass depends on the whole set:
        // Unity disables a compound shape on a kinematic body, so the check has to see every collider
        // before any of them is mirrored.
        m_seenColliders.Clear();
        foreach (UCollider source in GamePhysics.FindSceneObjects<UCollider>())
        {
            if (!source.enabled || !source.gameObject.activeInHierarchy) continue;
            URigidbody owner = source.attachedRigidbody;
            if (owner == null) continue;
            Body body = EnsureBody(owner);
            if (body != null) AttachCollider(body, source);
        }

        // Anything not seen this pass is gone: drop the child entity and its cooked blob.
        foreach (var pair in m_bodies)
        {
            Body body = pair.Value;
            if (body.Children.Count == 0) continue;
            m_staleChildren.Clear();
            foreach (var child in body.Children)
                if (!m_seenColliders.Contains(child.Key)) m_staleChildren.Add(child.Key);
            for (int i = 0; i < m_staleChildren.Count; i++) RemoveChild(body, m_staleChildren[i]);
        }

        SynchronizeJoints();
        SynchronizeIgnores();
    }

    /// <summary>
    /// Mirrors one collider as a child entity of the body, adding or refreshing it as needed.
    /// </summary>
    /// <remarks>
    /// One collider per entity is a Unity.Physics rule, not a limitation of this backend: the child is
    /// given a <c>Parent</c> and a local <c>LocalTransform</c> carrying the collider's offset, which is
    /// what makes a multi-collider part behave as the single rigid body Unity describes.
    /// </remarks>
    private void AttachCollider(Body body, UCollider source)
    {
        int id = source.GetInstanceID();
        m_seenColliders.Add(id);

        BlobAssetReference<Collider> blob = CreateCollider(source);
        if (!blob.IsCreated)
        {
            WarnOnce("shape" + source.GetType().Name, "Havok: collider type " + source.GetType().Name +
                " is not mirrored yet; '" + source.name + "' has no collision shape.");
            return;
        }

        if (!m_entities.Exists(body.Entity))
        {
            blob.Dispose();
            return;
        }

        // The collider's pose in body space. Cooked geometry is already scaled into the collider's own
        // space, so only the offset from the body is carried here.
        ColliderPose(source.transform, body.Transform, out float3 offset, out quaternion rotation);

        Child existing;
        if (body.Children.TryGetValue(id, out existing))
        {
            // Replace the cooked blob. The old handle must be untracked before it is disposed so the
            // teardown loop cannot dispose it a second time, and the new one must be tracked so it is
            // not leaked.
            if (existing.Blob.IsCreated)
            {
                m_owned.Remove(existing.Blob);
                existing.Blob.Dispose();
            }
            existing.Blob = blob;
            m_owned.Add(blob);

            m_entities.SetComponentData(existing.Entity, new PhysicsCollider { Value = blob });
            m_entities.SetComponentData(existing.Entity, new LocalTransform
            {
                Position = offset,
                Rotation = rotation,
                Scale = 1f,
            });
            existing.Trigger = source.isTrigger;
            ApplyChildFilter(existing, body);
            ApplyMass(body);
            return;
        }

        Entity child = m_entities.CreateEntity();
        var entry = new Child
        {
            Entity = child,
            Source = source,
            Blob = blob,
            Layer = source.gameObject.layer,
            Trigger = source.isTrigger,
        };

        m_entities.AddSharedComponent(child, new PhysicsWorldIndex(0u));
        m_entities.AddComponentData(child, new LocalTransform
        {
            Position = offset,
            Rotation = rotation,
            Scale = 1f,
        });
        m_entities.AddComponentData(child, new LocalToWorld { Value = float4x4.identity });
        m_entities.AddComponentData(child, new PhysicsCollider { Value = blob });
        m_entities.AddComponentData(child, PhysicsVelocity.Zero);
        m_entities.AddComponentData(child, new PhysicsDamping { Linear = 0f, Angular = 0.05f });
        m_entities.AddComponentData(child, new PhysicsMass { InverseMass = 1f });

        // Parent links the collider to the body so the solver treats them as one rigid body. The child
        // keeps its own LocalTransform, which is the offset from the body.
        Entity parent = body.Entity;
        m_entities.AddComponentData(child, new Parent { Value = parent });

        body.Children.Add(id, entry);
        ApplyChildFilter(entry, body);

        // Unity disables a compound collider on a kinematic body (PhysX behaviour), so a multi-collider
        // part that is kinematic must keep its individual shapes rather than merging them.
        ApplyMass(body);
    }

    private void ApplyChildFilter(Child child, Body body)
    {
        if (!m_entities.Exists(child.Entity)) return;

        if (child.Trigger)
        {
            // A trigger is a collider that resolves no contact. Unity.Physics expresses that by
            // zeroing the filter's response bits rather than by a component, so the collider stays
            // cooked and queryable while never pushing anything.
            CollisionFilter inert = default(CollisionFilter);
            inert.BelongsTo = 0u;
            inert.CollidesWith = 0u;
            PhysicsCollider collider = m_entities.GetComponentData<PhysicsCollider>(child.Entity);
            collider.Value.Value.SetCollisionFilter(inert);
            m_entities.SetComponentData(child.Entity, collider);
            child.Trigger = true;
            return;
        }

        CollisionFilter filter = BuildFilter(child.Source, body.Source);
        PhysicsCollider shape = m_entities.GetComponentData<PhysicsCollider>(child.Entity);
        shape.Value.Value.SetCollisionFilter(filter);
        m_entities.SetComponentData(child.Entity, shape);
    }

    private void RemoveChild(Body body, int id)
    {
        Child child;
        if (!body.Children.TryGetValue(id, out child)) return;
        if (m_entities.Exists(child.Entity)) m_entities.DestroyEntity(child.Entity);
        if (child.Blob.IsCreated)
        {
            m_owned.Remove(child.Blob);
            child.Blob.Dispose();
        }
        body.Children.Remove(id);
        // Only re-derive mass while the body still exists; during teardown the entity is already gone
        // and ApplyMass would be writing to a destroyed entity.
        if (m_entities.Exists(body.Entity)) ApplyMass(body);
    }

    /// <summary>Composes a collider's pose relative to the body it belongs to.</summary>
    private static void ColliderPose(UTransform collider, UTransform body, out float3 position, out quaternion rotation)
    {
        if (collider == body || body == null)
        {
            position = float3.zero;
            rotation = quaternion.identity;
            return;
        }

        UVector3 worldPosition = collider.position;
        UQuaternion worldRotation = collider.rotation;
        position = ToFloat3(body.InverseTransformPoint(worldPosition));
        rotation = ToQuaternion(UQuaternion.Inverse(body.rotation) * worldRotation);
    }

    private Body EnsureBody(URigidbody source)
    {
        if (source == null) return null;

        int id = source.GetInstanceID();
        Body body;
        if (m_bodies.TryGetValue(id, out body) && m_entities.Exists(body.Entity)) return body;

        Entity entity = m_entities.CreateEntity();
        body = new Body { Entity = entity, Source = source, Transform = source.transform };
        m_bodies[id] = body;

        // PhysicsWorldIndex is a shared component; it decides which physics world the body belongs to.
        m_entities.AddSharedComponent(entity, new PhysicsWorldIndex(0u));
        m_entities.AddComponentData(entity, LocalTransform.Identity);
        m_entities.AddComponentData(entity, new LocalToWorld { Value = float4x4.identity });
        m_entities.AddComponentData(entity, PhysicsVelocity.Zero);
        m_entities.AddComponentData(entity, new PhysicsDamping { Linear = 0f, Angular = 0.05f });
        m_entities.AddComponentData(entity, new PhysicsGravityFactor { Value = source.useGravity ? 1f : 0f });
        // A collider replaces this once one is found; until then the body is an inert kinematic point.
        m_entities.AddComponentData(entity, PhysicsMass.CreateKinematic(MassProperties.UnitSphere));
        return body;
    }

    private static BlobAssetReference<Collider> CreateCollider(UCollider source)
    {
        var box = source as UBoxCollider;
        if (box != null)
        {
            float3 size = ToFloat3(Scaled(box.size, box.transform.lossyScale));
            var geometry = new BoxGeometry
            {
                Center = ToFloat3(box.center),
                Orientation = quaternion.identity,
                Size = size,
                BevelRadius = math.min(ConvexHullGenerationParameters.Default.BevelRadius, 0.5f * math.cmin(size)),
            };
            return BoxCollider.Create(geometry);
        }

        var sphere = source as USphereCollider;
        if (sphere != null)
        {
            float uniform = UniformScale(sphere.transform.lossyScale);
            return SphereCollider.Create(new SphereGeometry
            {
                Center = ToFloat3(sphere.center),
                Radius = sphere.radius * uniform,
            });
        }

        var capsule = source as UCapsuleCollider;
        if (capsule != null)
        {
            float axis = AxisScale(capsule.transform.lossyScale, capsule.direction);
            float height = UMathf.Max(capsule.height, capsule.radius * 2f);
            float half = UMathf.Max(0f, height * 0.5f - capsule.radius) * axis;
            float3 offset = capsule.direction == 0 ? new float3(half, 0f, 0f)
                : capsule.direction == 2 ? new float3(0f, 0f, half)
                : new float3(0f, half, 0f);
            float3 center = ToFloat3(capsule.center);
            return CapsuleCollider.Create(new CapsuleGeometry
            {
                Vertex0 = center - offset,
                Vertex1 = center + offset,
                Radius = capsule.radius * axis,
            });
        }

        var mesh = source as UMeshCollider;
        if (mesh != null && mesh.sharedMesh != null) return CreateMeshCollider(mesh);

        var terrain = source as UTerrainCollider;
        if (terrain != null && terrain.terrainData != null) return CreateTerrainCollider(terrain);

        return default(BlobAssetReference<Collider>);
    }

    /// <summary>
    /// Cooks a Unity terrain into a Unity.Physics height field.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The height samples are already in metres, and the collider's own space is the terrain's local
    /// space, so the <c>scale</c> passed here is the terrain's size rather than the transform's lossy
    /// scale — those are the same thing for an unrotated terrain, and the child entity's
    /// <c>LocalTransform</c> carries the offset from the body.
    /// </para>
    /// <para>
    /// Terrain colliders are static by nature; when one is found on a moving body the heights are still
    /// cooked against the pose at mirror time, which is documented as a limitation rather than silently
    /// producing a height field that does not follow its body.
    /// </para>
    /// </remarks>
    private static BlobAssetReference<Collider> CreateTerrainCollider(UTerrainCollider source)
    {
        UTerrainData data = source.terrainData;
        int resolution = data.heightmapResolution;
        if (resolution < 2) return default(BlobAssetReference<Collider>);

        // Unity's heightmap is [y, x] and normalised 0..1; Unity.Physics wants row-major samples in
        // metres, which is heights * size.y.
        float[,] heights = data.GetHeights(0, 0, resolution, resolution);
        UVector3 size = data.size;

        var samples = new NativeArray<float>(resolution * resolution, Allocator.Temp);
        try
        {
            for (int y = 0; y < resolution; y++)
                for (int x = 0; x < resolution; x++)
                    samples[y * resolution + x] = heights[y, x] * size.y;

            var scale = new float3(size.x, 1f, size.z);
            BlobAssetReference<Collider> blob = TerrainCollider.Create(
                samples,
                new int2(resolution, resolution),
                scale,
                Unity.Physics.TerrainCollider.CollisionMethod.Triangles,
                CollisionFilter.Default,
                Material.Default);
            return blob;
        }
        finally
        {
            samples.Dispose();
        }
    }

    /// <summary>
    /// Static level geometry is usually a mesh collider, so the triangle mesh is cooked directly.
    /// Vertices are scaled into body space because the mirrored <c>LocalTransform</c> carries no scale.
    /// </summary>
    private static BlobAssetReference<Collider> CreateMeshCollider(UMeshCollider source)
    {
        UMesh mesh = source.sharedMesh;
        UVector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;

        var points = new NativeArray<float3>(vertices.Length, Allocator.Temp);
        var indices = new NativeArray<int3>(triangles.Length / 3, Allocator.Temp);
        try
        {
            UVector3 scale = source.transform.lossyScale;
            for (int i = 0; i < vertices.Length; i++) points[i] = ToFloat3(Scaled(vertices[i], scale));
            for (int i = 0; i < indices.Length; i++)
                indices[i] = new int3(triangles[i * 3], triangles[i * 3 + 1], triangles[i * 3 + 2]);
            return MeshCollider.Create(points, indices, CollisionFilter.Default, Material.Default);
        }
        finally
        {
            points.Dispose();
            indices.Dispose();
        }
    }

    /// <summary>
    /// Rebuilds the body's mass from the colliders currently mirrored on it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Mass properties come from the cooked colliders rather than being assembled here: each blob
    /// already carries the volume, centre of mass and inertia tensor of its shape, which is the same
    /// source <c>bpsim_2023.0</c> feeds to <c>PhysicsMass.CreateDynamic</c>. With several colliders the
    /// sum of the individual mass properties is used, which is what Unity does for a compound body.
    /// </para>
    /// <para>
    /// The colliders live on child entities, so the body entity deliberately carries no
    /// <c>PhysicsCollider</c> of its own — the collider components would otherwise fight with the
    /// children for the same shape.
    /// </para>
    /// </remarks>
    private void ApplyMass(Body body)
    {
        URigidbody source = body.Source;
        if (source == null || !m_entities.Exists(body.Entity)) return;
        body.Kinematic = source.isKinematic;

        float mass = UMathf.Max(source.mass, 0.0001f);

        // Mass properties come from the cooked colliders. With several colliders they are combined
        // rather than taken from the first one, so a multi-collider part is not under-weighted: volumes
        // add, the centre of mass is the volume-weighted average, and the inertia tensors combine with
        // the parallel axis theorem about that shared centre.
        MassProperties properties = default(MassProperties);
        bool any = false;
        float totalVolume = 0f;
        float3 weightedCentre = float3.zero;
        foreach (var pair in body.Children)
        {
            Child child = pair.Value;
            if (!child.Blob.IsCreated) continue;
            MassProperties p = child.Blob.Value.MassProperties;
            float volume = UMathf.Max(0f, p.Volume);

            if (!any)
            {
                properties = p;
                totalVolume = volume;
                weightedCentre = p.MassDistribution.Transform.pos * volume;
                any = true;
                continue;
            }

            totalVolume += volume;
            weightedCentre += p.MassDistribution.Transform.pos * volume;
        }

        if (!any)
        {
            // No usable collider: keep the body inert rather than giving it a solid shape.
            m_entities.SetComponentData(body.Entity, PhysicsMass.CreateKinematic(MassProperties.UnitSphere));
            return;
        }

        if (body.Children.Count > 1 && totalVolume > 0f)
        {
            // Combine as unit-density solids: each collider's mass is proportional to its volume, so
            // the parallel-axis term uses the volume share rather than an absolute mass. The resulting
            // tensor is scaled to the real mass internally by PhysicsMass.CreateDynamic.
            float3 centre = weightedCentre / totalVolume;
            float3 inertia = float3.zero;
            foreach (var pair in body.Children)
            {
                Child child = pair.Value;
                if (!child.Blob.IsCreated) continue;
                MassProperties p = child.Blob.Value.MassProperties;
                float3 r = p.MassDistribution.Transform.pos - centre;
                float share = p.Volume / totalVolume;
                // Parallel axis theorem, per axis: I = I_local + m*(|r|^2 - r_i^2).
                inertia += p.MassDistribution.InertiaTensor +
                    share * (math.dot(r, r) - r * r);
            }

            properties = new MassProperties
            {
                MassDistribution = new MassDistribution
                {
                    Transform = new RigidTransform(quaternion.identity, centre),
                    InertiaTensor = inertia,
                },
                Volume = totalVolume,
                AngularExpansionFactor = properties.AngularExpansionFactor,
            };
        }

        PhysicsMass builtMass = source.isKinematic
            ? PhysicsMass.CreateKinematic(properties)
            : PhysicsMass.CreateDynamic(properties, mass);

        ApplyConstraints(source, ref builtMass);
        m_entities.SetComponentData(body.Entity, builtMass);
    }

    /// <summary>
    /// Applies Unity's <c>RigidbodyConstraints</c> freeze flags to a mass, which is how Unity.Physics
    /// expresses a locked linear or angular axis.
    /// </summary>
    /// <remarks>
    /// A locked axis is represented by zeroing the corresponding inverse inertia or by setting the
    /// inverse mass to zero for a fully frozen translation, because the solver integrates by inverse
    /// mass and an inverse of zero cannot be moved. Unity's flags are inverted here: a frozen axis
    /// becomes a zero inverse.
    /// </remarks>
    private static void ApplyConstraints(URigidbody source, ref PhysicsMass mass)
    {
        URigidbodyConstraints constraints = source.constraints;
        if (constraints == URigidbodyConstraints.None) return;

        bool freezeX = (constraints & URigidbodyConstraints.FreezePositionX) != 0;
        bool freezeY = (constraints & URigidbodyConstraints.FreezePositionY) != 0;
        bool freezeZ = (constraints & URigidbodyConstraints.FreezePositionZ) != 0;
        bool freezeAllPosition = freezeX && freezeY && freezeZ;

        bool freezeRotX = (constraints & URigidbodyConstraints.FreezeRotationX) != 0;
        bool freezeRotY = (constraints & URigidbodyConstraints.FreezeRotationY) != 0;
        bool freezeRotZ = (constraints & URigidbodyConstraints.FreezeRotationZ) != 0;

        // PhysicsMass has one inverse mass plus a per-axis inverse inertia. Partial translation locks are
        // expressed on the velocity each step (see ApplyInputs) because the mass component cannot hold a
        // per-axis linear lock; a fully frozen translation can simply be made immovable here.
        if (freezeAllPosition) mass.InverseMass = 0f;

        float3 inverseInertia = mass.InverseInertia;
        if (freezeRotX) inverseInertia.x = 0f;
        if (freezeRotY) inverseInertia.y = 0f;
        if (freezeRotZ) inverseInertia.z = 0f;
        mass.InverseInertia = inverseInertia;
    }

    private void UpdateFilter(Body body, UCollider source)
    {
        Child child;
        if (body.Children.TryGetValue(source.GetInstanceID(), out child)) ApplyChildFilter(child, body);
    }

    /// <summary>
    /// Builds the collision filter for a collider: the layer mask from Unity's collision matrix, plus a
    /// group index when the collider is part of an explicitly ignored pair.
    /// </summary>
    private CollisionFilter BuildFilter(UCollider source, URigidbody owner)
    {
        int layer = source.gameObject.layer;
        ulong mask = owner != null && !owner.detectCollisions ? 0UL : m_layerMasks[layer];

        int group = 0;
        m_groupByCollider.TryGetValue(source.GetInstanceID(), out group);

        return new CollisionFilter
        {
            BelongsTo = (uint)(1UL << layer),
            CollidesWith = (uint)mask,
            // A negative group index means "collide with nobody", which is exactly a Unity ignore pair.
            GroupIndex = group == 0 ? 0 : -group,
        };
    }

    private void RefreshLayerMasks()
    {
        GamePhysics.ReadLayerCollisionMasks(m_layerMasks);
    }

    // ---------------------------------------------------------------------------------------------
    // Step
    // ---------------------------------------------------------------------------------------------

    public void Step(float delta)
    {
        if (m_disposed) throw new ObjectDisposedException("HavokDotsWorld");
        if (delta <= 0f) return;

        ApplyInputs();

        // One fixed step per call: the fixed-step group accumulates DeltaTime and divides it by
        // Timestep, so feeding it the frame delta keeps the game's fixed timestep authoritative.
        m_elapsed += delta;
        m_world.SetTime(new TimeData(m_elapsed, delta));
        m_world.GetExistingSystemManaged<FixedStepSimulationSystemGroup>().Timestep = delta;
        m_world.GetExistingSystemManaged<SimulationSystemGroup>().Update();

        ReadBack();
    }

    private void ApplyInputs()
    {
        foreach (var pair in m_bodies)
        {
            Body body = pair.Value;
            URigidbody source = body.Source;
            if (source == null || !m_entities.Exists(body.Entity)) continue;

            m_entities.SetComponentData(body.Entity, new LocalTransform
            {
                Position = ToFloat3(source.position),
                Rotation = ToQuaternion(source.rotation),
                Scale = 1f,
            });
            m_entities.SetComponentData(body.Entity, new PhysicsGravityFactor { Value = source.useGravity ? 1f : 0f });
            m_entities.SetComponentData(body.Entity, new PhysicsDamping
            {
                Linear = UMathf.Max(0f, source.drag),
                Angular = UMathf.Max(0f, source.angularDrag),
            });

            if (source.isKinematic != body.Kinematic)
            {
                ApplyMass(body);
                if (source.isKinematic) body.Sleeping = false;
            }

            if (source.isKinematic || body.Sleeping)
            {
                m_entities.SetComponentData(body.Entity, PhysicsVelocity.Zero);
                continue;
            }

            PhysicsVelocity velocity = new PhysicsVelocity
            {
                Linear = ToFloat3(source.velocity),
                Angular = ToFloat3(UVector3.ClampMagnitude(source.angularVelocity, source.maxAngularVelocity)),
            };
            ApplyConstraintLocks(source, ref velocity);
            m_entities.SetComponentData(body.Entity, velocity);
        }
    }

    /// <summary>
    /// Zeroes the velocity components Unity's freeze flags lock.
    /// </summary>
    /// <remarks>
    /// <c>PhysicsMass</c> cannot express a per-axis linear lock, so a partially frozen translation is
    /// enforced by never handing the solver a velocity on the frozen axes. A fully frozen body is left
    /// to the zero inverse mass set in <see cref="ApplyConstraints"/>.
    /// </remarks>
    private static void ApplyConstraintLocks(URigidbody source, ref PhysicsVelocity velocity)
    {
        URigidbodyConstraints constraints = source.constraints;
        if (constraints == URigidbodyConstraints.None) return;

        if ((constraints & URigidbodyConstraints.FreezePositionX) != 0) velocity.Linear.x = 0f;
        if ((constraints & URigidbodyConstraints.FreezePositionY) != 0) velocity.Linear.y = 0f;
        if ((constraints & URigidbodyConstraints.FreezePositionZ) != 0) velocity.Linear.z = 0f;

        if ((constraints & URigidbodyConstraints.FreezeRotationX) != 0) velocity.Angular.x = 0f;
        if ((constraints & URigidbodyConstraints.FreezeRotationY) != 0) velocity.Angular.y = 0f;
        if ((constraints & URigidbodyConstraints.FreezeRotationZ) != 0) velocity.Angular.z = 0f;
    }

    private void ReadBack()
    {
        foreach (var pair in m_bodies)
        {
            Body body = pair.Value;
            URigidbody source = body.Source;
            if (source == null || source.isKinematic || body.Sleeping) continue;
            if (!m_entities.Exists(body.Entity) || !m_entities.HasComponent<LocalTransform>(body.Entity)) continue;

            LocalTransform local = m_entities.GetComponentData<LocalTransform>(body.Entity);
            PhysicsVelocity velocity = m_entities.GetComponentData<PhysicsVelocity>(body.Entity);

            // A partially frozen translation is enforced on the way out as well as on the way in: the
            // solver can still integrate a frozen axis during the step, so the read-back restores the
            // component Unity says must not move.
            UVector3 position = new UVector3(local.Position.x, local.Position.y, local.Position.z);
            UQuaternion rotation = new UQuaternion(local.Rotation.value.x, local.Rotation.value.y, local.Rotation.value.z, local.Rotation.value.w);
            URigidbodyConstraints constraints = source.constraints;
            if (constraints != URigidbodyConstraints.None)
            {
                UVector3 previous = body.Transform.position;
                if ((constraints & URigidbodyConstraints.FreezePositionX) != 0) position.x = previous.x;
                if ((constraints & URigidbodyConstraints.FreezePositionY) != 0) position.y = previous.y;
                if ((constraints & URigidbodyConstraints.FreezePositionZ) != 0) position.z = previous.z;
                UQuaternion previousRotation = body.Transform.rotation;
                if ((constraints & URigidbodyConstraints.FreezeRotationX) != 0) rotation.x = previousRotation.x;
                if ((constraints & URigidbodyConstraints.FreezeRotationY) != 0) rotation.y = previousRotation.y;
                if ((constraints & URigidbodyConstraints.FreezeRotationZ) != 0) rotation.z = previousRotation.z;
            }

            body.Transform.SetPositionAndRotation(position, rotation);
            source.velocity = new UVector3(velocity.Linear.x, velocity.Linear.y, velocity.Linear.z);
            source.angularVelocity = new UVector3(velocity.Angular.x, velocity.Angular.y, velocity.Angular.z);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Per-body operations
    // ---------------------------------------------------------------------------------------------

    public void Wake(URigidbody source)
    {
        Body body = Find(source);
        if (body != null) body.Sleeping = false;
    }

    /// <summary>
    /// Unity.Physics has no per-body sleep flag to set; a slept body is held still by zeroing its
    /// velocity each step until <see cref="Wake"/> clears the flag.
    /// </summary>
    public void Sleep(URigidbody source)
    {
        Body body = Find(source);
        if (body == null) return;
        body.Sleeping = true;
        if (m_entities.Exists(body.Entity)) m_entities.SetComponentData(body.Entity, PhysicsVelocity.Zero);
    }

    public bool IsSleeping(URigidbody source)
    {
        Body body = Find(source);
        return body != null && body.Sleeping;
    }

    // ---------------------------------------------------------------------------------------------
    // Joints
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Mirrors Unity joints as Unity.Physics constraints.
    /// </summary>
    /// <remarks>
    /// A constraint is its own entity carrying two <c>PhysicsConstrainedBodyPair</c> and two
    /// <c>PhysicsJoint</c> components (one body pair, and the joint data oriented each way, which is
    /// what makes the impulse solver symmetric). The set is diffed by signature so a joint whose limits
    /// or motor change is rebuilt rather than left stale.
    /// </remarks>
    private void SynchronizeJoints()
    {
        m_seenJoints.Clear();
        foreach (UJoint source in GamePhysics.FindSceneObjects<UJoint>())
        {
            // UnityEngine.Joint derives from Component, not Behaviour, so it has no enabled flag; a
            // joint is switched off through its GameObject.
            if (source == null || !source.gameObject.activeInHierarchy) continue;
            if (source.connectedBody == null) continue;

            Body a = Find(source.GetComponent<URigidbody>());
            Body b = Find(source.connectedBody);
            if (a == null || b == null) continue;
            if (!m_entities.Exists(a.Entity) || !m_entities.Exists(b.Entity)) continue;

            int id = source.GetInstanceID();
            m_seenJoints.Add(id);

            int signature = JointSignature(source);
            JointEntry existing;
            if (m_joints.TryGetValue(id, out existing))
            {
                // The signature has to be compared, not just the body pair: this game edits joints
                // after creating them — ActivatedHingeDoor assigns HingeJoint.limits at runtime, and
                // BridgeStep rewrites useMotor/motor — and a mirror that only noticed a changed body
                // pair would keep the stale constraint forever.
                if (existing.A == a && existing.B == b && existing.Signature == signature) continue;
                DestroyJoint(existing);
                m_joints.Remove(id);
            }

            BuildJoint(id, source, a, b, signature);
        }

        m_staleJoints.Clear();
        foreach (var pair in m_joints) if (!m_seenJoints.Contains(pair.Key)) m_staleJoints.Add(pair.Key);
        for (int i = 0; i < m_staleJoints.Count; i++)
        {
            DestroyJoint(m_joints[m_staleJoints[i]]);
            m_joints.Remove(m_staleJoints[i]);
        }
    }

    /// <summary>
    /// Hashes everything that changes the meaning of a mirrored joint.
    /// </summary>
    /// <remarks>
    /// Deliberately not just the two bodies. Unity lets a joint be reconfigured in place, and this game
    /// does so: <c>ActivatedHingeDoor</c> assigns <c>HingeJoint.limits</c> after the joint exists, and
    /// <c>BridgeStep</c> rewrites <c>useMotor</c>/<c>motor</c>. Those edits are invisible in the body
    /// pair, so without this the rebuilt constraint would never appear.
    /// </remarks>
    private static int JointSignature(UJoint joint)
    {
        unchecked
        {
            int h = 17;
            h = h * 31 + (joint.connectedBody != null ? joint.connectedBody.GetInstanceID() : 0);
            h = h * 31 + joint.anchor.GetHashCode();
            h = h * 31 + joint.connectedAnchor.GetHashCode();
            h = h * 31 + joint.axis.GetHashCode();
            h = h * 31 + (joint.autoConfigureConnectedAnchor ? 1 : 0);
            h = h * 31 + (joint.enableCollision ? 1 : 0);

            var hinge = joint as UHingeJoint;
            if (hinge != null)
            {
                var limits = hinge.limits;
                var motor = hinge.motor;
                h = h * 31 + (hinge.useLimits ? 1 : 0);
                h = h * 31 + limits.min.GetHashCode();
                h = h * 31 + limits.max.GetHashCode();
                h = h * 31 + (hinge.useMotor ? 1 : 0);
                h = h * 31 + motor.targetVelocity.GetHashCode();
                h = h * 31 + motor.force.GetHashCode();
                h = h * 31 + (motor.freeSpin ? 1 : 0);
            }

            var spring = joint as USpringJoint;
            if (spring != null)
            {
                h = h * 31 + spring.minDistance.GetHashCode();
                h = h * 31 + spring.maxDistance.GetHashCode();
            }

            return h;
        }
    }

    private void BuildJoint(int id, UJoint source, Body a, Body b, int signature)
    {
        Entity entity = m_entities.CreateEntity();
        var joint = new JointEntry
        {
            Entity = entity,
            Source = source,
            A = a,
            B = b,
            Ra = a.Source,
            Rb = b.Source,
            Signature = signature,
        };

        UTransform jointTransform = source.transform;
        float3 anchorA = ToFloat3(a.Transform.InverseTransformPoint(jointTransform.TransformPoint(source.anchor)));
        float3 anchorB = ToFloat3(b.Transform.InverseTransformPoint(jointTransform.TransformPoint(source.connectedAnchor)));

        PhysicsJoint data;
        if (!CreateJointData(source, a, b, out data))
        {
            m_entities.DestroyEntity(entity);
            WarnOnce("joint" + source.GetType().Name, "Havok: joint type " + source.GetType().Name +
                " is not mirrored yet; '" + source.name + "' is ignored.");
            return;
        }

        m_entities.AddSharedComponent(entity, new PhysicsWorldIndex(0u));
        m_entities.AddComponentData(entity, new PhysicsConstrainedBodyPair(a.Entity, b.Entity, source.enableCollision));
        m_entities.AddComponentData(entity, data);

        // A motorised hinge needs a second constraint block: CreateHinge and CreateAngularVelocityMotor
        // are separate joints, and one PhysicsJoint cannot express both. Unity.Physics solves multiple
        // joint entities over the same body pair independently, so the motor is simply its own entity
        // sharing the body pair — no PhysicsJointCompanion is needed (that type is an
        // IBufferElementData used to keep authoring-time joints atomic, not a runtime requirement).
        var hinge = source as UHingeJoint;
        if (hinge != null && hinge.useMotor)
        {
            PhysicsJoint motor;
            if (CreateHingeMotorData(hinge, a, b, anchorA, anchorB, out motor))
            {
                joint.MotorEntity = m_entities.CreateEntity();
                m_entities.AddSharedComponent(joint.MotorEntity, new PhysicsWorldIndex(0u));
                m_entities.AddComponentData(joint.MotorEntity,
                    new PhysicsConstrainedBodyPair(a.Entity, b.Entity, source.enableCollision));
                m_entities.AddComponentData(joint.MotorEntity, motor);
            }
        }

        m_joints[id] = joint;
    }

    /// <summary>
    /// Translates a Unity joint into Unity.Physics joint data, expressed as body frames (an anchor plus
    /// an axis) in each body's local space.
    /// </summary>
    /// <remarks>
    /// The exact factories used here were read off the shipped <c>Unity.Physics.dll</c> rather than
    /// guessed: <c>CreateFixed</c>, <c>CreateHinge</c>, <c>CreateLimitedHinge</c> and
    /// <c>CreateLimitedDistance</c>. Note that the hinge factories take only the two frames — the hinge
    /// axis rides in <c>BodyFrame.Axis</c>, not as a separate argument — and that <c>BodyFrame</c> is
    /// constructed from a single <c>RigidTransform</c>.
    /// </remarks>
    private bool CreateJointData(UJoint source, Body a, Body b, out PhysicsJoint data)
    {
        data = default(PhysicsJoint);

        // Anchors are authored in the joint's own space; convert them into each body's local space.
        UTransform jointTransform = source.transform;
        float3 anchorA = ToFloat3(a.Transform.InverseTransformPoint(jointTransform.TransformPoint(source.anchor)));
        float3 anchorB = ToFloat3(b.Transform.InverseTransformPoint(jointTransform.TransformPoint(source.connectedAnchor)));

        var fixedJoint = source as UFixedJoint;
        if (fixedJoint != null)
        {
            data = PhysicsJoint.CreateFixed(
                new BodyFrame(new RigidTransform(quaternion.identity, anchorA)),
                new BodyFrame(new RigidTransform(quaternion.identity, anchorB)));
            return true;
        }

        var hinge = source as UHingeJoint;
        if (hinge != null)
        {
            BodyFrame frameA, frameB;
            HingeFrames(hinge, a, b, anchorA, anchorB, out frameA, out frameB);

            if (hinge.useLimits)
            {
                // Unity authors hinge limits in degrees and its own inspector will accept a reversed
                // pair, so the range is normalised before it reaches the solver.
                var limits = hinge.limits;
                float min = math.radians(limits.min);
                float max = math.radians(limits.max);
                if (max < min) { float swap = min; min = max; max = swap; }
                data = PhysicsJoint.CreateLimitedHinge(frameA, frameB, new Unity.Physics.Math.FloatRange(min, max));
            }
            else
            {
                data = PhysicsJoint.CreateHinge(frameA, frameB);
            }
            return true;
        }

        var spring = source as USpringJoint;
        if (spring != null)
        {
            float min = UMathf.Max(0f, spring.minDistance);
            float max = UMathf.Max(min, spring.maxDistance);
            data = PhysicsJoint.CreateLimitedDistance(anchorA, anchorB, new Unity.Physics.Math.FloatRange(min, max));
            return true;
        }

        // ConfigurableJoint and CharacterJoint have no direct Unity.Physics counterpart — the closest is
        // CreateLimitedDOF, which locks whole axes rather than expressing Unity's per-axis limits and
        // drives. Reporting the gap is deliberate: silently approximating them would mis-simulate a
        // vehicle or a ragdoll, and both are used in this game (BalloonBalancer, GrapplingHook).
        return false;
    }

    /// <summary>
    /// Builds the two hinge body frames shared by the hinge constraint and its motor.
    /// </summary>
    /// <remarks>
    /// The hinge axis is expressed in each body's local frame, because that is the frame the solver
    /// constrains against. <c>BodyFrame</c> derives its perpendicular axis from this one.
    /// </remarks>
    private static void HingeFrames(
        UHingeJoint hinge, Body a, Body b, float3 anchorA, float3 anchorB,
        out BodyFrame frameA, out BodyFrame frameB)
    {
        float3 worldAxis = hinge.transform.TransformDirection(hinge.axis.normalized);
        float3 axisA = math.normalizesafe(ToFloat3(a.Transform.InverseTransformDirection(worldAxis)));
        float3 axisB = math.normalizesafe(ToFloat3(b.Transform.InverseTransformDirection(worldAxis)));

        frameA = new BodyFrame(new RigidTransform(quaternion.LookRotationSafe(axisA, math.up()), anchorA));
        frameB = new BodyFrame(new RigidTransform(quaternion.LookRotationSafe(axisB, math.up()), anchorB));
    }

    /// <summary>
    /// Builds the angular-velocity motor for a motorised <see cref="UHingeJoint"/>, or returns false
    /// when the motor cannot drive anything.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unity and Unity.Physics disagree on two units here, and both conversions are approximations
    /// rather than exact equivalences:
    /// </para>
    /// <list type="bullet">
    ///   <item><description><c>JointMotor.targetVelocity</c> is degrees per second; the solver wants
    ///   radians per second.</description></item>
    ///   <item><description><c>JointMotor.force</c> is a force (N), while
    ///   <c>CreateAngularVelocityMotor</c> takes a maximum <i>impulse</i> (N·s). It is scaled by the
    ///   fixed timestep, which is the correct dimensional conversion for a per-step impulse but still
    ///   ignores how the solver spreads a constraint impulse across its iterations.</description></item>
    /// </list>
    /// <para>
    /// <c>JointMotor.freeSpin</c> has no counterpart at all: Unity uses it to stop the motor resisting
    /// motion in the driven direction, while an angular-velocity motor always resists. A free-spinning
    /// hinge will therefore behave slightly more stiffly than under PhysX.
    /// </para>
    /// </remarks>
    private static bool CreateHingeMotorData(UHingeJoint hinge, Body a, Body b, float3 anchorA, float3 anchorB, out PhysicsJoint data)
    {
        data = default(PhysicsJoint);

        var motor = hinge.motor;
        // force == 0 means the motor has no authority, so there is nothing to add. This also keeps a
        // useMotor toggle that the game saved but never configured from creating a dead constraint.
        if (motor.force <= 0f) return false;

        BodyFrame frameA, frameB;
        HingeFrames(hinge, a, b, anchorA, anchorB, out frameA, out frameB);

        float target = math.radians(motor.targetVelocity);
        float maxImpulse = motor.force * UTime.fixedDeltaTime;
        data = PhysicsJoint.CreateAngularVelocityMotor(frameA, frameB, target, maxImpulse);
        return true;
    }

    private void DestroyJoint(JointEntry joint)
    {
        if (joint.MotorEntity != Entity.Null && m_entities.Exists(joint.MotorEntity))
            m_entities.DestroyEntity(joint.MotorEntity);
        if (joint.Entity != Entity.Null && m_entities.Exists(joint.Entity))
            m_entities.DestroyEntity(joint.Entity);
    }

    // ---------------------------------------------------------------------------------------------
    // Explicit ignore pairs
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Mirrors <see cref="UPhysics.IgnoreCollision"/> pairs onto the collision filters.
    /// </summary>
    /// <remarks>
    /// Unity.Physics has no per-pair switch, but the filter is evaluated per entity pair, so the pair
    /// can be expressed with the group index: setting both colliders to the same non-zero
    /// <c>GroupIndex</c> makes them collide only with each other, and a negative group makes them never
    /// collide. Ignored pairs therefore take a shared negative group; a collider with no ignored pairs
    /// keeps group 0, which means "collide with everything the layer mask allows".
    /// </remarks>
    private void SynchronizeIgnores()
    {
        var desired = GamePhysics.IgnoredPairs;
        bool changed = false;

        foreach (ulong key in desired) if (m_appliedIgnores.Add(key)) changed = true;
        m_removedIgnores.Clear();
        foreach (ulong key in m_appliedIgnores) if (!desired.Contains(key)) m_removedIgnores.Add(key);
        for (int i = 0; i < m_removedIgnores.Count; i++)
        {
            m_appliedIgnores.Remove(m_removedIgnores[i]);
            changed = true;
        }
        if (!changed && m_appliedIgnores.Count == 0) return;

        // Rebuild the group assignment from scratch and re-apply to every child collider.
        m_groupByCollider.Clear();
        int nextGroup = 1;
        foreach (ulong key in m_appliedIgnores)
        {
            int high = (int)(key >> 32);
            int low = (int)(key & 0xFFFFFFFF);
            int group;
            if (!m_groupByCollider.TryGetValue(high, out group))
            {
                group = nextGroup++;
                m_groupByCollider[high] = group;
            }
            m_groupByCollider[low] = group;
        }

        foreach (var pair in m_bodies)
        {
            foreach (var child in pair.Value.Children) ApplyChildFilter(child.Value, pair.Value);
        }
    }

    /// <summary>
    /// Re-applies filtering after the game registers or clears an ignore pair.
    /// </summary>
    public void Refilter(UCollider a, UCollider b)
    {
        // The authoritative work happens in SynchronizeIgnores on the next step; nothing native is left
        // stale by deferring, because the filter is rebuilt from GamePhysics.IgnoredPairs every time it
        // changes.
    }

    public void Dispose()
    {
        if (m_disposed) return;
        m_disposed = true;

        for (int i = 0; i < m_owned.Count; i++)
        {
            if (m_owned[i].IsCreated) m_owned[i].Dispose();
        }
        m_owned.Clear();
        m_bodies.Clear();
        m_world.Dispose();
    }

    // ---------------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------------

    private Body Find(URigidbody source)
    {
        if (source == null) return null;
        Body body;
        return m_bodies.TryGetValue(source.GetInstanceID(), out body) ? body : null;
    }

    private void RemoveBody(int id)
    {
        Body body;
        if (!m_bodies.TryGetValue(id, out body)) return;

        // Joints that reference this body have to go first: a constraint left pointing at a destroyed
        // entity is a dangling reference the solver would follow.
        if (m_joints.Count > 0)
        {
            m_staleJoints.Clear();
            foreach (var pair in m_joints)
                if (pair.Value.A == body || pair.Value.B == body) m_staleJoints.Add(pair.Key);
            for (int i = 0; i < m_staleJoints.Count; i++)
            {
                DestroyJoint(m_joints[m_staleJoints[i]]);
                m_joints.Remove(m_staleJoints[i]);
            }
        }

        m_staleChildren.Clear();
        foreach (var child in body.Children) m_staleChildren.Add(child.Key);
        for (int i = 0; i < m_staleChildren.Count; i++) RemoveChild(body, m_staleChildren[i]);

        if (m_entities.Exists(body.Entity)) m_entities.DestroyEntity(body.Entity);
        m_bodies.Remove(id);
    }

    private void WarnOnce(string key, string message)
    {
        if (!m_warned.Add(key)) return;
        UDebug.LogWarning(message);
    }

    private static float3 ToFloat3(UVector3 value)
    {
        return new float3(value.x, value.y, value.z);
    }

    private static quaternion ToQuaternion(UQuaternion value)
    {
        return new quaternion(value.x, value.y, value.z, value.w);
    }

    private static UVector3 Scaled(UVector3 size, UVector3 scale)
    {
        return new UVector3(size.x * UMathf.Abs(scale.x), size.y * UMathf.Abs(scale.y), size.z * UMathf.Abs(scale.z));
    }

    private static float UniformScale(UVector3 scale)
    {
        return UMathf.Max(UMathf.Abs(scale.x), UMathf.Max(UMathf.Abs(scale.y), UMathf.Abs(scale.z)));
    }

    private static float AxisScale(UVector3 scale, int direction)
    {
        return UMathf.Abs(direction == 0 ? scale.x : direction == 2 ? scale.z : scale.y);
    }
}
#endif
