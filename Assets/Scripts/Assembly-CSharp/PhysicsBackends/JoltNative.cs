using System;
using System.Runtime.InteropServices;

/// <summary>
/// Raw P/Invoke surface for the <c>JoltC</c> native plugin (the flat C API over Jolt Physics).
/// </summary>
/// <remarks>
/// <para>
/// Keep this file free of Unity types: it mirrors <c>JoltC/include/JoltC/*.h</c> one for one so that
/// C# and C stay diffable. Marshalling and engine semantics live in <see cref="JoltPhysicsBackend"/>.
/// </para>
/// <para>
/// <c>JoltC_RVec3</c> is three <c>float</c>s here because the plugin is built without
/// <c>JOLTC_DOUBLE_PRECISION</c>. If a double-precision plugin is ever shipped, this struct and
/// <see cref="BodyCreationSettings"/> must change together or every position silently corrupts.
/// </para>
/// </remarks>
internal static class JoltNative
{    /// <summary>
    /// Plugin name as Unity expects it. Unity strips the <c>lib</c> prefix and extension; on Windows
    /// the file is <c>libJoltC.dll</c> and is looked up as <c>libJoltC</c>.
    /// </summary>
    public const string Library = "libJoltC";

    // ---------------------------------------------------------------------
    // Blittable math / settings structs
    // ---------------------------------------------------------------------

    [StructLayout(LayoutKind.Sequential)]
    public struct Vec3
    {
        public float x, y, z;
        public Vec3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Quat
    {
        public float x, y, z, w;
        public Quat(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RVec3
    {
        public float x, y, z;
        public RVec3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Triangle
    {
        public Vec3 v1, v2, v3;
        public uint materialIndex;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CompoundShapeSubShape
    {
        public Vec3 position;
        public Quat rotation;
        public IntPtr shape;
        public uint userData;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SpringSettings
    {
        public int mode;              // JoltC_SpringMode
        public float frequencyOrStiffness;
        public float damping;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MotorSettings
    {
        public SpringSettings springSettings;
        public float minForceLimit, maxForceLimit;
        public float minTorqueLimit, maxTorqueLimit;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Mat44
    {
        // Column-major, 16 floats - mirrors JoltC_Mat44.
        public float m00, m10, m20, m30;
        public float m01, m11, m21, m31;
        public float m02, m12, m22, m32;
        public float m03, m13, m23, m33;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MassProperties
    {
        public float mass;
        public Mat44 inertia;
    }

    /// <summary>
    /// Mirrors <c>JoltC_CollisionGroup</c>, which is 16 bytes: a pointer, then two 32-bit ids.
    /// </summary>
    /// <remarks>
    /// The filter is stored as a <see cref="long"/> rather than an <see cref="IntPtr"/> on purpose.
    /// A struct containing a pointer field is <i>not</i> blittable, so the marshaller would insert a
    /// reference-to-native conversion: <c>CollisionGroup</c> would be reported as 24 bytes instead of
    /// 16, shifting every field after it inside <see cref="BodyCreationSettings"/> and turning the
    /// whole settings block into garbage. That exact mistake crashes Jolt inside
    /// <c>CreateAndAddBody</c> with an access violation. Keep this field a long.
    ///
    /// The value must be the <c>GroupFilter*</c> itself, not the <c>JoltC_GroupFilter</c> wrapper that
    /// <c>JoltC_GroupFilterTable_Create</c> returns and that the <c>JoltC_GroupFilterTable_*</c> calls
    /// take: <c>body.cpp</c> casts this field straight to <c>const GroupFilter*</c> without unwrapping the
    /// wrapper, so the wrapper's first pointer is what has to be sent. See
    /// <c>JoltPhysicsBackend.BuildFiltering</c> for the full story.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    public struct CollisionGroup
    {
        public long groupFilter;
        public uint groupID;
        public uint subGroupID;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BodyCreationSettings
    {
        public RVec3 position;
        public Quat rotation;
        public Vec3 linearVelocity;
        public Vec3 angularVelocity;
        public ulong userData;
        public ushort objectLayer;
        public CollisionGroup collisionGroup;
        public int motionType;
        public int allowedDOFs;
        public int allowDynamicOrKinematic;
        public int isSensor;
        public int collideKinematicVsNonDynamic;
        public int useManifoldReduction;
        public int applyGyroscopicForce;
        public int motionQuality;
        public int enhancedInternalEdgeRemoval;
        public int allowSleeping;
        public float friction;
        public float restitution;
        public float linearDamping;
        public float angularDamping;
        public float maxLinearVelocity;
        public float maxAngularVelocity;
        public float gravityFactor;
        public uint numVelocityStepsOverride;
        public uint numPositionStepsOverride;
        public int overrideMassProperties;
        public float inertiaMultiplier;
        /// <summary>
        /// Inline, not a pointer: <c>JoltC_BodyCreationSettings</c> embeds the 68-byte
        /// <c>JoltC_MassProperties</c> by value, which is what puts <see cref="shape"/> at offset 240.
        /// Unused unless <see cref="overrideMassProperties"/> selects the "provided" mode, but it must
        /// stay in the layout or every field after it shifts.
        /// </summary>
        public MassProperties massPropertiesOverride;
        public IntPtr shape;
    }

    /// <summary>Size of <see cref="BodyCreationSettings"/> as the native plugin sees it.</summary>
    public const int BodyCreationSettingsSize = 248;

    [StructLayout(LayoutKind.Sequential)]
    public struct PointConstraintSettings
    {
        public int space;
        public RVec3 point1, point2;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FixedConstraintSettings
    {
        public int space;
        public int autoDetectPoint;
        public RVec3 point1;
        public Vec3 axisX1, axisY1;
        public RVec3 point2;
        public Vec3 axisX2, axisY2;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DistanceConstraintSettings
    {
        public int space;
        public RVec3 point1, point2;
        public float minDistance, maxDistance;
        public SpringSettings limitsSpringSettings;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HingeConstraintSettings
    {
        public int space;
        public RVec3 point1;
        public Vec3 hingeAxis1, normalAxis1;
        public RVec3 point2;
        public Vec3 hingeAxis2, normalAxis2;
        public float limitsMin, limitsMax;
        public SpringSettings limitsSpringSettings;
        public float maxFrictionTorque;
        public MotorSettings motorSettings;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SliderConstraintSettings
    {
        public int space;
        public int autoDetectPoint;
        public RVec3 point1;
        public Vec3 sliderAxis1, normalAxis1;
        public RVec3 point2;
        public Vec3 sliderAxis2, normalAxis2;
        public float limitsMin, limitsMax;
        public SpringSettings limitsSpringSettings;
        public float maxFrictionForce;
        public MotorSettings motorSettings;
    }

    // ---------------------------------------------------------------------
    // Enums - values must match JoltC/common.h exactly
    // ---------------------------------------------------------------------

    public const int MotionTypeStatic = 0;
    public const int MotionTypeKinematic = 1;
    public const int MotionTypeDynamic = 2;

    public const int MotionQualityDiscrete = 0;
    public const int MotionQualityLinearCast = 1;

    public const int ActivationActivate = 0;
    public const int ActivationDontActivate = 1;

    public const int ConstraintSpaceLocal = 0;
    public const int ConstraintSpaceWorld = 1;

    public const int MotorStateOff = 0;
    public const int MotorStateVelocity = 1;
    public const int MotorStatePosition = 2;

    public const int SpringModeFrequency = 0;
    public const int SpringModeStiffness = 1;

    public const uint BodyIdInvalid = 0xFFFFFFFFu;

    /// <summary>All six degrees of freedom free - <c>JOLTC_ALLOWED_DOFS_ALL</c>.</summary>
    public const int AllowedDofsAll = 0b111111;

    /// <summary>
    /// Bit per axis: 1 = X translation, 2 = Y, 4 = Z, 8 = X rotation, 16 = Y, 32 = Z.
    /// Unity's <see cref="UnityEngine.RigidbodyConstraints"/> uses the same layout for its freeze bits,
    /// but Unity's semantics are inverted: a set Unity bit means <i>locked</i>, while a set JoltC bit
    /// means <i>allowed</i>. See <c>JoltPhysicsBackend.ToAllowedDofs</c>.
    /// </summary>
    public const int AllowedDofsTranslationX = 0b000001;
    public const int AllowedDofsTranslationY = 0b000010;
    public const int AllowedDofsTranslationZ = 0b000100;
    public const int AllowedDofsRotationX = 0b001000;
    public const int AllowedDofsRotationY = 0b010000;
    public const int AllowedDofsRotationZ = 0b100000;

    // ---------------------------------------------------------------------
    // common.h - lifecycle and error reporting
    // ---------------------------------------------------------------------

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern int JoltC_Init();

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_Shutdown();

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JoltC_GetLastError();

    // ---------------------------------------------------------------------
    // physics_system.h
    // ---------------------------------------------------------------------

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JoltC_TempAllocatorMalloc_Create();

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_TempAllocator_Destroy(IntPtr allocator);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JoltC_JobSystemThreadPool_Create(uint maxJobs, uint maxBarriers, int numThreads);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_JobSystem_Destroy(IntPtr jobSystem);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JoltC_BroadPhaseLayerInterfaceTable_Create(uint numObjectLayers, uint numBroadPhaseLayers);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_BroadPhaseLayerInterfaceTable_MapObjectToBroadPhaseLayer(
        IntPtr iface, ushort objectLayer, byte broadPhaseLayer);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_BroadPhaseLayerInterface_Destroy(IntPtr iface);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JoltC_ObjectVsBroadPhaseLayerFilterTable_Create(
        IntPtr broadPhaseLayerInterface, uint numBroadPhaseLayers,
        IntPtr objectLayerPairFilter, uint numObjectLayers);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_ObjectVsBroadPhaseLayerFilter_Destroy(IntPtr filter);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JoltC_ObjectLayerPairFilterTable_Create(uint numObjectLayers);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JoltC_GroupFilterTable_Create(uint numSubGroups);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_GroupFilterTable_DisableCollision(IntPtr filter, uint subGroup1, uint subGroup2);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_GroupFilterTable_EnableCollision(IntPtr filter, uint subGroup1, uint subGroup2);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_GroupFilter_Destroy(IntPtr filter);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_ObjectLayerPairFilterTable_EnableCollision(IntPtr filter, ushort layer1, ushort layer2);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_ObjectLayerPairFilterTable_DisableCollision(IntPtr filter, ushort layer1, ushort layer2);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern int JoltC_ObjectLayerPairFilterTable_ShouldCollide(IntPtr filter, ushort layer1, ushort layer2);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_ObjectLayerPairFilter_Destroy(IntPtr filter);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JoltC_PhysicsSystem_Create();

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_PhysicsSystem_Destroy(IntPtr system);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_PhysicsSystem_Init(
        IntPtr system, uint maxBodies, uint numBodyMutexes, uint maxBodyPairs, uint maxContactConstraints,
        IntPtr broadPhaseLayerInterface, IntPtr objectVsBroadPhaseLayerFilter, IntPtr objectLayerPairFilter);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_PhysicsSystem_OptimizeBroadPhase(IntPtr system);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint JoltC_PhysicsSystem_Update(
        IntPtr system, float deltaTime, int collisionSteps, IntPtr tempAllocator, IntPtr jobSystem);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_PhysicsSystem_SetGravity(IntPtr system, Vec3 gravity);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint JoltC_PhysicsSystem_GetNumBodies(IntPtr system);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_PhysicsSystem_GetBounds(IntPtr system, out AABox bounds);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint JoltC_PhysicsSystem_GetNumConstraints(IntPtr system);

    [StructLayout(LayoutKind.Sequential)]
    public struct AABox
    {
        public Vec3 min;
        public Vec3 max;
    }

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JoltC_PhysicsSystem_GetBodyInterface(IntPtr system);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JoltC_PhysicsSystem_GetNarrowPhaseQuery(IntPtr system);

    /// <summary>
    /// Casts a ray and reports the closest hit; the return value is the hit flag.
    /// </summary>
    /// <remarks>
    /// Four parameters, matching <c>JoltC_NarrowPhaseQuery_CastRay</c> in query.h, which takes no
    /// filters. This used to declare three extra <see cref="IntPtr"/> filter arguments: x64 cdecl
    /// tolerates the surplus because the callee never reads them, but it is a lie about the ABI and would
    /// break under any convention that pops arguments. Filtered ray casts have their own entry point,
    /// <c>JoltC_NarrowPhaseQuery_CastRay2</c>.
    /// </remarks>
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern int JoltC_NarrowPhaseQuery_CastRay(
        IntPtr query, RVec3 origin, Vec3 direction, out RayCastResult result);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JoltC_PhysicsSystem_GetDefaultBroadPhaseLayerFilter(IntPtr system, ushort layer);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JoltC_PhysicsSystem_GetDefaultLayerFilter(IntPtr system, ushort layer);

    [StructLayout(LayoutKind.Sequential)]
    public struct RayCastResult
    {
        public uint bodyID;
        public float fraction;
        public uint subShapeID2;
    }

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_PhysicsSystem_AddConstraint(IntPtr system, IntPtr constraint);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_PhysicsSystem_RemoveConstraint(IntPtr system, IntPtr constraint);

    // ---------------------------------------------------------------------
    // shape.h
    // ---------------------------------------------------------------------

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JoltC_BoxShapeSettings_Create(Vec3 halfExtent, float convexRadius);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JoltC_BoxShapeSettings_CreateShape(Vec3 halfExtent, float convexRadius);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JoltC_SphereShapeSettings_CreateShape(float radius);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JoltC_CapsuleShapeSettings_CreateShape(float halfHeight, float radius);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JoltC_RotatedTranslatedShape_Create(Vec3 position, Quat rotation, IntPtr innerShape);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JoltC_ConvexHullShapeSettings_Create([In] Vec3[] points, int pointCount, float maxConvexRadius);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JoltC_ConvexHullShapeSettings_CreateShape(IntPtr settings);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JoltC_MeshShapeSettings_Create([In] Triangle[] triangles, uint triangleCount);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JoltC_MeshShapeSettings_CreateShape(IntPtr settings);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JoltC_StaticCompoundShapeSettings_Create();

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JoltC_StaticCompoundShape_Create([In] CompoundShapeSubShape[] subShapes, int numSubShapes);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JoltC_EmptyShape_Create(Vec3 centerOfMass);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_CompoundShapeSettings_AddShape2(
        IntPtr settings, Vec3 position, Quat rotation, IntPtr shape, uint userData);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_MeshShapeSettings_Destroy(IntPtr settings);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JoltC_HeightFieldShapeSettings_Create(
        [In] float[] samples, Vec3 offset, Vec3 scale, uint sampleCount);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JoltC_HeightFieldShapeSettings_CreateShape(IntPtr settings);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_HeightFieldShapeSettings_Destroy(IntPtr settings);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_ShapeSettings_Destroy(IntPtr settings);

    /// <summary>
    /// Decrements a shape's reference count. <c>JoltC_Shape_Destroy</c> is an alias for this.
    /// </summary>
    /// <remarks>
    /// Every <c>..._CreateShape</c> factory and <see cref="JoltC_BodyInterface_GetShape"/> return one
    /// owned reference, so a handle that is stored in a second owner must be <c>AddRef</c>ed. Skipping
    /// that is what makes a double release possible, and a double release is a heap corruption that
    /// Windows only reports later — as a <c>0xC0000409</c> fail-fast at the next validation.
    /// </remarks>
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_Shape_Release(IntPtr shape);

    /// <summary>
    /// Increments a shape's reference count, for when two owners hold the same handle.
    /// </summary>
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_Shape_AddRef(IntPtr shape);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_Shape_Destroy(IntPtr shape);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern float JoltC_Shape_GetVolume(IntPtr shape);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern int JoltC_Shape_GetType(IntPtr shape);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern int JoltC_Shape_GetSubType(IntPtr shape);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JoltC_BodyInterface_GetShape(IntPtr iface, uint bodyId);

    // ---------------------------------------------------------------------
    // body.h
    // ---------------------------------------------------------------------

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JoltC_BodyCreationSettings_Create();

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_BodyCreationSettings_Destroy(IntPtr settings);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_BodyCreationSettings_SetDefault(ref BodyCreationSettings settings);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint JoltC_BodyInterface_CreateAndAddBody(
        IntPtr iface, ref BodyCreationSettings settings, int activation);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_BodyInterface_RemoveAndDestroyBody(IntPtr iface, uint bodyId);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_BodyInterface_AddBody(IntPtr iface, uint bodyId, int activation);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_BodyInterface_RemoveBody(IntPtr iface, uint bodyId);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern int JoltC_BodyInterface_IsAdded(IntPtr iface, uint bodyId);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_BodyInterface_ActivateBody(IntPtr iface, uint bodyId);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_BodyInterface_DeactivateBody(IntPtr iface, uint bodyId);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern int JoltC_BodyInterface_IsActive(IntPtr iface, uint bodyId);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_BodyInterface_SetPosition(IntPtr iface, uint bodyId, RVec3 position, int activation);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern RVec3 JoltC_BodyInterface_GetPosition(IntPtr iface, uint bodyId);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern RVec3 JoltC_BodyInterface_GetCenterOfMassPosition(IntPtr iface, uint bodyId);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_BodyInterface_SetRotation(IntPtr iface, uint bodyId, Quat rotation, int activation);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern Quat JoltC_BodyInterface_GetRotation(IntPtr iface, uint bodyId);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_BodyInterface_SetPositionAndRotation(
        IntPtr iface, uint bodyId, RVec3 position, Quat rotation, int activation);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_BodyInterface_GetPositionAndRotation(
        IntPtr iface, uint bodyId, out RVec3 position, out Quat rotation);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_BodyInterface_SetLinearVelocity(IntPtr iface, uint bodyId, Vec3 velocity);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern Vec3 JoltC_BodyInterface_GetLinearVelocity(IntPtr iface, uint bodyId);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_BodyInterface_SetAngularVelocity(IntPtr iface, uint bodyId, Vec3 velocity);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern Vec3 JoltC_BodyInterface_GetAngularVelocity(IntPtr iface, uint bodyId);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_BodyInterface_GetLinearAndAngularVelocity(
        IntPtr iface, uint bodyId, out Vec3 linear, out Vec3 angular);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_BodyInterface_MoveKinematic(
        IntPtr iface, uint bodyId, RVec3 targetPosition, Quat targetRotation, float deltaTime);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_BodyInterface_SetMotionType(IntPtr iface, uint bodyId, int motionType, int activation);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern int JoltC_BodyInterface_GetMotionType(IntPtr iface, uint bodyId);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_BodyInterface_SetFriction(IntPtr iface, uint bodyId, float friction);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_BodyInterface_SetRestitution(IntPtr iface, uint bodyId, float restitution);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_BodyInterface_SetGravityFactor(IntPtr iface, uint bodyId, float gravityFactor);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_BodyInterface_SetLinearDamping(IntPtr iface, uint bodyId, float damping);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_BodyInterface_SetAngularDamping(IntPtr iface, uint bodyId, float damping);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_BodyInterface_SetMaxLinearVelocity(IntPtr iface, uint bodyId, float maxVelocity);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_BodyInterface_SetMaxAngularVelocity(IntPtr iface, uint bodyId, float maxVelocity);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_BodyInterface_SetMotionQuality(IntPtr iface, uint bodyId, int quality);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_BodyInterface_SetIsSensor(IntPtr iface, uint bodyId, int value);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_BodyInterface_SetShape(
        IntPtr iface, uint bodyId, IntPtr shape, int updateMassProperties, int activation);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_BodyInterface_SetCollisionGroup(IntPtr iface, uint bodyId, ref CollisionGroup group);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_BodyInterface_InvalidateContactCache(IntPtr iface, uint bodyId);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern float JoltC_BodyInterface_GetInverseMass(IntPtr iface, uint bodyId);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_BodyInterface_SetInverseMass(IntPtr iface, uint bodyId, float inverseMass);

    // ---------------------------------------------------------------------
    // constraint.h
    // ---------------------------------------------------------------------

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_Constraint_Destroy(IntPtr constraint);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_FixedConstraintSettings_Init(ref FixedConstraintSettings settings);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JoltC_FixedConstraint_Create(
        IntPtr system, uint body1, uint body2, ref FixedConstraintSettings settings);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_PointConstraintSettings_Init(ref PointConstraintSettings settings);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JoltC_PointConstraint_Create(
        IntPtr system, uint body1, uint body2, ref PointConstraintSettings settings);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_DistanceConstraintSettings_Init(ref DistanceConstraintSettings settings);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JoltC_DistanceConstraint_Create(
        IntPtr system, uint body1, uint body2, ref DistanceConstraintSettings settings);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_HingeConstraintSettings_Init(ref HingeConstraintSettings settings);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JoltC_HingeConstraint_Create(
        IntPtr system, uint body1, uint body2, ref HingeConstraintSettings settings);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern float JoltC_HingeConstraint_GetCurrentAngle(IntPtr constraint);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_HingeConstraint_SetMotorState(IntPtr constraint, int state);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_HingeConstraint_SetTargetAngularVelocity(IntPtr constraint, float velocity);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_HingeConstraint_SetTargetAngle(IntPtr constraint, float angle);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_HingeConstraint_SetLimits(IntPtr constraint, float min, float max);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_SliderConstraintSettings_Init(ref SliderConstraintSettings settings);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr JoltC_SliderConstraint_Create(
        IntPtr system, uint body1, uint body2, ref SliderConstraintSettings settings);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_SliderConstraint_SetMotorState(IntPtr constraint, int state);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_SliderConstraint_SetTargetVelocity(IntPtr constraint, float velocity);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_SliderConstraint_SetTargetPosition(IntPtr constraint, float position);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void JoltC_SliderConstraint_SetLimits(IntPtr constraint, float min, float max);

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    /// <summary>
    /// Reads the plugin's last error string. Returns <c>null</c> when there is nothing pending.
    /// </summary>
    public static string LastError()
    {
        IntPtr pointer = JoltC_GetLastError();
        return pointer == IntPtr.Zero ? null : Marshal.PtrToStringAnsi(pointer);
    }

    /// <summary>Throws with the plugin's own message when <paramref name="value"/> is a null handle.</summary>
    public static IntPtr Require(IntPtr value, string what)
    {
        if (value == IntPtr.Zero) throw new InvalidOperationException("Jolt: " + what + " failed" + Describe());
        return value;
    }

    /// <summary>Every interop struct, for the layout diagnostic.</summary>
    public static readonly Type[] InteropTypes =
    {
        typeof(Vec3), typeof(Quat), typeof(RVec3), typeof(Triangle), typeof(CompoundShapeSubShape),
        typeof(SpringSettings), typeof(MotorSettings), typeof(CollisionGroup),
        typeof(BodyCreationSettings), typeof(PointConstraintSettings), typeof(FixedConstraintSettings),
        typeof(DistanceConstraintSettings), typeof(HingeConstraintSettings), typeof(SliderConstraintSettings),
    };

    /// <summary>
    /// Fails fast if a marshalled struct no longer matches the native layout.
    /// </summary>
    /// <remarks>
    /// A layout drift here does not produce a managed exception; it produces a native access
    /// violation deep inside Jolt. Catching it at start-up turns a crash into a clear message. The
    /// expected sizes come from compiling the same definitions as C with the same compiler the plugin
    /// was built with.
    /// </remarks>
    public static void ValidateLayouts()
    {
        CheckSize<Vec3>(12);
        CheckSize<Quat>(16);
        CheckSize<RVec3>(12);
        CheckSize<Triangle>(40);
        CheckSize<CompoundShapeSubShape>(IntPtr.Size == 8 ? 48 : 36);
        CheckSize<SpringSettings>(12);
        CheckSize<MotorSettings>(28);
        CheckSize<CollisionGroup>(16);
        CheckSize<MassProperties>(68);
        CheckSize<BodyCreationSettings>(BodyCreationSettingsSize);
        CheckSize<PointConstraintSettings>(28);
        CheckSize<FixedConstraintSettings>(80);
        CheckSize<DistanceConstraintSettings>(48);
        CheckSize<HingeConstraintSettings>(128);
        CheckSize<SliderConstraintSettings>(132);
    }

    private static void CheckSize<T>(int expected) where T : struct
    {
        int actual = Marshal.SizeOf(typeof(T));
        if (actual != expected)
            throw new InvalidOperationException("Jolt: " + typeof(T).Name + " marshals as " + actual +
                " bytes but the native plugin expects " + expected + "; the interop layout has drifted.");
    }

    public static string Describe()
    {
        string error = LastError();
        return string.IsNullOrEmpty(error) ? string.Empty : ": " + error;
    }
}
