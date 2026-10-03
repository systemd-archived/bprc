using System;
using System.Collections.Generic;
using Box3D;
using Unity.Mathematics;
using UnityEngine;
using UJoint = UnityEngine.Joint;

// Rigidbody remains the game's state facade; only Box3D advances simulation in this mode.
public sealed partial class Box3DBackend : IPhysicsBackend
{
    public PhysicsEngine Engine => PhysicsEngine.Box3D;

    public string DisplayName => "Box3D";

    /// <summary>
    /// Box3D ships native libraries for every platform this project targets, so the switcher offers it
    /// everywhere. Whether the library actually loads is decided when the backend is constructed, which
    /// is where a genuine load failure is reported.
    /// </summary>
    public static bool IsSupported(out string reason)
    {
        reason = null;
        return true;
    }

    private sealed class BodyEntry
    {
        public Body native;
        public Rigidbody source;
        public Transform transform;
        public RigidbodyInterpolation interpolation;
        public Vector3 incomingVelocity;
        public RigidbodyConstraints constraints;
        public float mass = -1;
        public Vector3 center, inertia;
        public Quaternion inertiaRotation;
        public bool geometryDirty = true;
    }
    private sealed class ShapeEntry
    {
        public Collider source;
        public int instanceId;
        public BodyEntry owner;
        public Shape native;
        public TriangleMesh mesh;
        public HeightField terrain;
        public Vector3 position, scale, dimensions, center;
        public Quaternion rotation;
        public Matrix4x4 colliderToBody;
        public bool trigger;
        public Mesh unityMesh;
        public ulong mask;
        public int layer;
        public SurfaceMaterial material;
        public bool hasMaterial;
    }
    private World world;
    private Body anchor;
    private readonly Dictionary<int, BodyEntry> bodies = new Dictionary<int, BodyEntry>();
    private readonly Dictionary<int, ShapeEntry> shapes = new Dictionary<int, ShapeEntry>();
    private readonly Dictionary<ShapeId, ShapeEntry> shapeIds = new Dictionary<ShapeId, ShapeEntry>();
    private readonly List<int> removed = new List<int>();
    private readonly HashSet<int> seen = new HashSet<int>();
    private readonly ulong[] layerMasks = new ulong[32];
    private bool disposed;

    public Box3DBackend()
    {
        var def = WorldDef.Default;
        def.Gravity = Physics.gravity;
        def.WorkerCount = (uint)GamePhysics.PhysicsWorkerCount();
        def.RestitutionThreshold = Physics.bounceThreshold;
        world = World.Create(def);
        try
        {
            anchor = world.CreateBody(BodyDef.Default);
            world.SetCustomFilterCallback(Filter);
            world.SetFrictionCallback((a, idA, b, idB) => MixMaterial(a, b, (PhysicMaterialCombine)(idA & 255), (PhysicMaterialCombine)(idB & 255)));
            world.SetRestitutionCallback((a, idA, b, idB) => MixMaterial(a, b, (PhysicMaterialCombine)(idA >> 8), (PhysicMaterialCombine)(idB >> 8)));
        }
        catch { world.Destroy(); throw; }
    }

    private bool Filter(Shape a, Shape b)
    {
        // Worker-thread callback: only read immutable snapshots, never UnityEngine objects.
        return !shapeIds.TryGetValue(a.Id, out var x) || !shapeIds.TryGetValue(b.Id, out var y) ||
            !GamePhysics.IgnoredPairs.Contains(GamePhysics.PairKey(x.instanceId, y.instanceId));
    }

    public void Refilter(Collider a, Collider b)
    {
        if (shapes.TryGetValue(a.GetInstanceID(), out var x)) Refilter(x);
        if (shapes.TryGetValue(b.GetInstanceID(), out var y)) Refilter(y);
    }

    private static void Refilter(ShapeEntry entry)
    {
        // The native engine skips an identical filter; invalidate cached contacts explicitly.
        var filter = entry.native.GetFilter();
        entry.native.SetFilter(new CollisionFilter { CategoryBits = filter.CategoryBits, MaskBits = 0 });
        entry.native.SetFilter(filter);
        entry.owner.native.SetAwake(true);
    }

    public void Wake(Rigidbody body) { if (bodies.TryGetValue(body.GetInstanceID(), out var b)) b.native.SetAwake(true); }
    public void Sleep(Rigidbody body) { if (bodies.TryGetValue(body.GetInstanceID(), out var b)) b.native.SetAwake(false); }
    public bool IsSleeping(Rigidbody body) => bodies.TryGetValue(body.GetInstanceID(), out var b) && !b.native.IsAwake;

    public void Synchronize()
    {
        if (disposed) throw new ObjectDisposedException(nameof(Box3DBackend));
        GamePhysics.ReadLayerCollisionMasks(layerMasks);
        // Discovery also catches parts spawned by legacy code without changing prefab serialization.
        foreach (var rb in GamePhysics.FindSceneObjects<Rigidbody>()) EnsureBody(rb, rb.transform);
        seen.Clear();
        foreach (var collider in GamePhysics.FindSceneObjects<Collider>())
        {
            if (!collider.enabled || !collider.gameObject.activeInHierarchy) continue;
            int id = collider.GetInstanceID();
            seen.Add(id);
            var rb = collider.attachedRigidbody;
            var owner = EnsureBody(rb, rb ? rb.transform : collider.transform);
            ColliderPose(collider.transform, owner.transform, out var colliderToBody, out var rotation);
            Vector3 local = colliderToBody.MultiplyPoint3x4(Vector3.zero);
            Vector3 scale = collider.transform.lossyScale;
            GeometrySignature(collider, out var center, out var dimensions, out var mesh);
            // A sphere's orientation is irrelevant, including an off-center spinning sphere.
            if (collider is SphereCollider)
            {
                local = colliderToBody.MultiplyPoint3x4(center);
                rotation = Quaternion.identity;
            }
            shapes.TryGetValue(id, out var entry);
            if (entry != null && (entry.owner != owner || entry.trigger != collider.isTrigger || entry.unityMesh != mesh ||
                (entry.position - local).sqrMagnitude > 1e-10f || DifferentRotation(entry.rotation, rotation) ||
                (entry.scale - scale).sqrMagnitude > 1e-10f || entry.center != center || entry.dimensions != dimensions))
            {
                RemoveShape(entry);
                shapes.Remove(id);
                entry = null;
            }
            if (entry == null)
            {
                entry = new ShapeEntry { source = collider, instanceId = id, owner = owner, position = local, rotation = rotation,
                    scale = scale, colliderToBody = colliderToBody, trigger = collider.isTrigger, center = center, dimensions = dimensions, unityMesh = mesh, layer = -1 };
                CreateShape(entry);
                shapes.Add(id, entry);
                shapeIds.Add(entry.native.Id, entry);
            }
            int layer = collider.gameObject.layer;
            ulong mask = rb && !rb.detectCollisions ? 0 : layerMasks[layer];
            if (entry.layer != layer || entry.mask != mask)
            {
                entry.layer = layer; entry.mask = mask;
                entry.native.SetFilter(new CollisionFilter { CategoryBits = 1UL << layer, MaskBits = mask });
            }
            var material = collider.sharedMaterial;
            var surface = entry.material;
            surface.Friction = material ? material.dynamicFriction : 0.6f;
            surface.Restitution = material ? material.bounciness : 0f;
            surface.UserMaterialId = material ? (ulong)material.frictionCombine | ((ulong)material.bounceCombine << 8) : 0;
            if (!entry.hasMaterial || surface.Friction != entry.material.Friction || surface.Restitution != entry.material.Restitution || surface.UserMaterialId != entry.material.UserMaterialId)
            {
                entry.native.SetSurfaceMaterial(surface);
                entry.material = surface; entry.hasMaterial = true;
            }
        }
        removed.Clear();
        foreach (var pair in shapes) if (!seen.Contains(pair.Key)) removed.Add(pair.Key);
        foreach (int id in removed) { RemoveShape(shapes[id]); shapes.Remove(id); }

        removed.Clear();
        foreach (var pair in bodies)
        {
            var entry = pair.Value;
            if (!entry.transform || !entry.transform.gameObject.activeInHierarchy ||
                (!ReferenceEquals(entry.source, null) && !entry.source)) removed.Add(pair.Key);
        }
        foreach (int id in removed)
        {
            var entry = bodies[id];
            if (entry.source) entry.source.interpolation = entry.interpolation;
            entry.native.Destroy(); bodies.Remove(id);
        }
        SynchronizeJoints();
    }

    private BodyEntry EnsureBody(Rigidbody rb, Transform transform)
    {
        int id = rb ? rb.GetInstanceID() : transform.GetInstanceID();
        if (bodies.TryGetValue(id, out var entry)) return entry;
        var def = BodyDef.Default;
        def.Position = (float3)transform.position;
        def.Rotation = transform.rotation;
        def.Type = rb ? (rb.isKinematic ? BodyType.Kinematic : BodyType.Dynamic) : BodyType.Static;
        entry = new BodyEntry { native = world.CreateBody(def), source = rb, transform = transform };
        if (rb)
        {
            entry.interpolation = rb.interpolation;
            rb.interpolation = RigidbodyInterpolation.None;
        }
        bodies.Add(id, entry);
        return entry;
    }

    private static int CombineRank(PhysicMaterialCombine mode)
    {
        switch (mode) { case PhysicMaterialCombine.Minimum: return 1; case PhysicMaterialCombine.Multiply: return 2;
            case PhysicMaterialCombine.Maximum: return 3; default: return 0; }
    }
    private static float MixMaterial(float a, float b, PhysicMaterialCombine modeA, PhysicMaterialCombine modeB)
    {
        var mode = CombineRank(modeA) >= CombineRank(modeB) ? modeA : modeB;
        switch (mode) { case PhysicMaterialCombine.Minimum: return Math.Min(a, b); case PhysicMaterialCombine.Maximum: return Math.Max(a, b);
            case PhysicMaterialCombine.Multiply: return a * b; default: return (a + b) * 0.5f; }
    }

    private static void GeometrySignature(Collider c, out Vector3 center, out Vector3 dimensions, out Mesh mesh)
    {
        center = Vector3.zero; dimensions = Vector3.zero; mesh = null;
        if (c is BoxCollider box) { center = box.center; dimensions = box.size; }
        else if (c is SphereCollider sphere) { center = sphere.center; dimensions.x = sphere.radius; }
        else if (c is CapsuleCollider capsule) { center = capsule.center; dimensions = new Vector3(capsule.radius, capsule.height, capsule.direction); }
        else if (c is MeshCollider mc) { mesh = mc.sharedMesh; dimensions.x = mc.convex ? 1 : 0; }
    }

    private static void ColliderPose(Transform collider, Transform owner, out Matrix4x4 matrix, out Quaternion rotation)
    {
        // Compose below the Rigidbody, avoiding subtraction of moving world coordinates.
        matrix = Matrix4x4.identity;
        rotation = Quaternion.identity;
        for (var t = collider; t != owner; t = t.parent)
        {
            matrix = Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale) * matrix;
            rotation = t.localRotation * rotation;
        }
        matrix = Matrix4x4.Scale(owner.lossyScale) * matrix;
        rotation = rotation.normalized;
    }

    private static bool DifferentRotation(Quaternion a, Quaternion b)
    {
        float4 x = ((quaternion)a.normalized).value, y = ((quaternion)b.normalized).value;
        if (math.dot(x, y) < 0) y = -y;
        return math.lengthsq(x - y) > 1e-10f;
    }

    private Vector3 LocalPoint(ShapeEntry e, Vector3 p) => e.colliderToBody.MultiplyPoint3x4(p);

    private void CreateShape(ShapeEntry e)
    {
        var def = ShapeDef.Default;
        def.IsSensor = e.trigger;
        def.EnableSensorEvents = true;
        def.EnableContactEvents = true;
        def.EnableCustomFiltering = true;
        def.UpdateBodyMass = false;
        def.Filter = new CollisionFilter { CategoryBits = 1UL << e.source.gameObject.layer, MaskBits = layerMasks[e.source.gameObject.layer] };
        Vector3 scale = new Vector3(Mathf.Abs(e.scale.x), Mathf.Abs(e.scale.y), Mathf.Abs(e.scale.z));
        if (e.source is BoxCollider box)
        {
            Vector3 half = Vector3.Scale(box.size, scale) * 0.5f;
            half = Vector3.Max(half, Vector3.one * 0.0001f);
            var hull = BoxHull.CreateTransformed(half.x, half.y, half.z,
                new B3Transform { Position = LocalPoint(e, box.center), Rotation = e.rotation });
            e.native = e.owner.native.CreateHullShape(def, in hull);
        }
        else if (e.source is SphereCollider sphere)
            e.native = e.owner.native.CreateSphereShape(def, new Sphere { Center = LocalPoint(e, sphere.center),
                Radius = Mathf.Max(0.0001f, sphere.radius * Mathf.Max(scale.x, scale.y, scale.z)) });
        else if (e.source is CapsuleCollider capsule)
        {
            int axis = capsule.direction;
            float radius = Mathf.Max(0.0001f, capsule.radius * Mathf.Max(scale[(axis + 1) % 3], scale[(axis + 2) % 3]));
            Vector3 offset = Vector3.zero;
            offset[axis] = Mathf.Max(0, capsule.height * scale[axis] * 0.5f - radius);
            offset = e.rotation * offset;
            Vector3 center = LocalPoint(e, capsule.center);
            e.native = e.owner.native.CreateCapsuleShape(def, new Capsule { Center1 = center - offset, Center2 = center + offset, Radius = radius });
        }
        else if (e.source is MeshCollider mc && mc.sharedMesh)
        {
            Box3DMeshCache.Read(mc.sharedMesh, out var source, out var triangles);
            var vertices = new float3[source.Length];
            for (int i = 0; i < source.Length; i++) vertices[i] = LocalPoint(e, source[i]);
            if (mc.convex)
            {
                var hull = Hull.Create(vertices);
                try { e.native = e.owner.native.CreateHullShape(def, hull); }
                finally { hull.Destroy(); }
            }
            else
            {
                if (e.owner.source)
                    throw new NotSupportedException("Box3D requires a convex mesh for a Rigidbody: " + mc.name);
                if (e.scale.x * e.scale.y * e.scale.z < 0)
                {
                    // PhysX preserves the front face under a reflected transform.
                    triangles = (int[])triangles.Clone();
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        int swap = triangles[i + 1]; triangles[i + 1] = triangles[i + 2]; triangles[i + 2] = swap;
                    }
                }
                e.mesh = TriangleMesh.Create(vertices, triangles);
                try { e.native = e.owner.native.CreateMeshShape(def, e.mesh); }
                catch { e.mesh.Destroy(); throw; }
            }
        }
        else if (e.source is TerrainCollider tc && tc.terrainData)
        {
            if (e.owner.source || Quaternion.Angle(e.rotation, Quaternion.identity) > 0.01f)
                throw new NotSupportedException("Box3D terrain must be static and unrotated: " + tc.name);
            var data = tc.terrainData;
            int size = data.heightmapResolution;
            var samples = data.GetHeights(0, 0, size, size);
            var heights = new float[size * size];
            for (int z = 0; z < size; z++) for (int x = 0; x < size; x++) heights[z * size + x] = samples[z, x];
            var holes = data.GetHoles(0, 0, size - 1, size - 1);
            var materials = new byte[(size - 1) * (size - 1)];
            for (int z = 0; z < size - 1; z++) for (int x = 0; x < size - 1; x++)
                if (!holes[z, x]) materials[z * (size - 1) + x] = HeightField.HoleMaterial;
            Vector3 spacing = Vector3.Scale(data.heightmapScale, scale);
            e.terrain = HeightField.Create(heights, size, size, spacing, 0, 1, false, materials);
            try { e.native = e.owner.native.CreateHeightFieldShape(def, e.terrain); }
            catch { e.terrain.Destroy(); throw; }
        }
        else throw new NotSupportedException("Unsupported Box3D collider: " + e.source.GetType().Name + " on " + e.source.name);
        if (!e.native.IsValid) throw new InvalidOperationException("Box3D could not create collider " + e.source.name);
        e.owner.mass = -1;
        e.owner.geometryDirty = true;
    }

    private void RemoveShape(ShapeEntry entry)
    {
        entry.owner.geometryDirty = true;
        entry.owner.mass = -1;
        shapeIds.Remove(entry.native.Id);
        if (entry.native.IsValid) entry.native.Destroy(false);
        if (entry.mesh.IsCreated) entry.mesh.Destroy();
        if (entry.terrain.IsCreated) entry.terrain.Destroy();
    }

    public void Step(float delta)
    {
        world.SetGravity(Physics.gravity);
        foreach (var entry in bodies.Values)
        {
            var rb = entry.source;
            Body body = entry.native;
            if (rb)
            {
                BodyType desired = rb.isKinematic ? BodyType.Kinematic : BodyType.Dynamic;
                if (body.Type != desired) { body.SetType(desired); entry.mass = -1; }
            }
            Vector3 position = rb ? rb.position : entry.transform.position;
            Quaternion rotation = rb ? rb.rotation : entry.transform.rotation;
            if (rb && rb.isKinematic)
                body.SetTargetTransform(new B3WorldTransform { Position = (float3)position, Rotation = rotation }, delta, true);
            else if ((position - (Vector3)(float3)body.Position).sqrMagnitude > 1e-10f || DifferentRotation(rotation, body.Rotation))
                body.SetTransform((float3)position, rotation);
            if (!rb) continue;
            if (entry.geometryDirty)
            {
                // Native mass calculation also initializes the shape extents used by CCD.
                // SetMassData alone leaves those extents uninitialized in Box3D 0.1.0.
                body.ApplyMassFromShapes();
                entry.geometryDirty = false;
                entry.mass = -1;
            }
            body.SetGravityScale(rb.useGravity ? 1 : 0);
            body.SetLinearDamping(Mathf.Max(0, rb.drag));
            body.SetAngularDamping(Mathf.Max(0, rb.angularDrag));
            body.SetBullet(rb.collisionDetectionMode != CollisionDetectionMode.Discrete);
            if (entry.constraints != rb.constraints || entry.mass < 0)
            {
                var c = rb.constraints;
                body.SetMotionLocks(new MotionLocks { LinearX = (c & RigidbodyConstraints.FreezePositionX) != 0,
                    LinearY = (c & RigidbodyConstraints.FreezePositionY) != 0, LinearZ = (c & RigidbodyConstraints.FreezePositionZ) != 0,
                    AngularX = (c & RigidbodyConstraints.FreezeRotationX) != 0, AngularY = (c & RigidbodyConstraints.FreezeRotationY) != 0,
                    AngularZ = (c & RigidbodyConstraints.FreezeRotationZ) != 0 });
                entry.constraints = c;
                entry.mass = -1;
            }
            if (!rb.isKinematic && (entry.mass != rb.mass || entry.center != rb.centerOfMass || entry.inertia != rb.inertiaTensor || entry.inertiaRotation != rb.inertiaTensorRotation))
            {
                float3x3 r = new float3x3((quaternion)rb.inertiaTensorRotation);
                // Unity's zero principal inertia means infinite inertia, not a small mass.
                // Native SetMassData requires an invertible finite matrix for partial locks.
                float3 inertia = rb.inertiaTensor;
                float lockedInertia = math.max(math.cmax(inertia), 0.00001f) * 1000000f;
                if (math.any(inertia != 0f)) inertia = math.select(inertia, new float3(lockedInertia), inertia == 0f);
                float3x3 matrix = math.mul(math.mul(r, new float3x3(inertia.x, 0, 0, 0, inertia.y, 0, 0, 0, inertia.z)), math.transpose(r));
                body.SetMassData(new MassData { Mass = rb.mass, Center = rb.centerOfMass,
                    Inertia = new B3Matrix3 { Cx = matrix.c0, Cy = matrix.c1, Cz = matrix.c2 } });
                // Box3D 0.1.0 SetMassData updates only the local inverse inertia.
                // ApplyMassFromShapes can leave a density-based world tensor that
                // disagrees with our authored mass and frozen-axis inertia.
                // Refresh the world tensor before constraints use it in this step.
                body.SetTransform(body.Position, body.Rotation);
                entry.mass = rb.mass; entry.center = rb.centerOfMass; entry.inertia = rb.inertiaTensor; entry.inertiaRotation = rb.inertiaTensorRotation;
            }
            entry.incomingVelocity = rb.velocity;
            if (!rb.isKinematic)
            {
                if ((rb.velocity - (Vector3)body.LinearVelocity).sqrMagnitude > 1e-12f) body.LinearVelocity = rb.velocity;
                Vector3 angular = Vector3.ClampMagnitude(rb.angularVelocity, rb.maxAngularVelocity);
                if ((angular - (Vector3)body.AngularVelocity).sqrMagnitude > 1e-12f) body.AngularVelocity = angular;
            }
        }
        world.Step(delta, 4);
        foreach (var entry in bodies.Values)
        {
            var rb = entry.source;
            if (!rb || rb.isKinematic) continue;
            rb.position = (float3)entry.native.Position;
            rb.rotation = entry.native.Rotation;
            rb.velocity = entry.native.LinearVelocity;
            rb.angularVelocity = entry.native.AngularVelocity;
            entry.transform.SetPositionAndRotation(rb.position, rb.rotation);
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
        world.Destroy();
        foreach (var e in shapes.Values)
        {
            if (e.mesh.IsCreated) e.mesh.Destroy();
            if (e.terrain.IsCreated) e.terrain.Destroy();
        }
        bodies.Clear(); shapes.Clear(); shapeIds.Clear(); joints.Clear();
        pendingJointDestruction.Clear();
    }
}
