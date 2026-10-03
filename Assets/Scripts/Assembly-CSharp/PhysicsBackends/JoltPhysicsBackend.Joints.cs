using System;
using System.Collections.Generic;
using UnityEngine;
using JN = JoltNative;
using UJoint = UnityEngine.Joint;

/// <summary>
/// Unity joint mirroring for the Jolt backend.
/// </summary>
/// <remarks>
/// Unity's <see cref="UJoint"/> components are plain data describing a constraint between two
/// rigidbodies. This partial rebuilds the matching Jolt constraint whenever that data changes, and
/// leaves stable joints alone so warm-starting keeps working.
/// </remarks>
public sealed partial class JoltPhysicsBackend
{
    private sealed class JointEntry
    {
        public UJoint source;
        public IntPtr constraint;
        public int signature;
        public bool isHinge;
        public BodyEntry other;
        public BodyEntry self;
    }

    private readonly Dictionary<int, JointEntry> joints = new Dictionary<int, JointEntry>();
    private readonly HashSet<UJoint> pendingJointDestruction = new HashSet<UJoint>();

    public float HingeAngle(HingeJoint source)
    {
        if (source && joints.TryGetValue(source.GetInstanceID(), out var entry) && entry.isHinge &&
            entry.constraint != IntPtr.Zero)
            return JN.JoltC_HingeConstraint_GetCurrentAngle(entry.constraint) * Mathf.Rad2Deg;
        return source ? source.angle : 0f;
    }

    private void SynchronizeJoints()
    {
        pendingJointDestruction.RemoveWhere(joint => !joint);

        removed.Clear();
        foreach (var pair in joints)
            if (!pair.Value.source || !pair.Value.source.gameObject.activeInHierarchy)
                removed.Add(pair.Key);
        foreach (int id in removed) DestroyJoint(joints[id], id);

        foreach (var source in GamePhysics.FindSceneObjects<UJoint>())
        {
            if (!source.gameObject.activeInHierarchy || pendingJointDestruction.Contains(source)) continue;
            int id = source.GetInstanceID();
            int signature = JointSignature(source);
            if (joints.TryGetValue(id, out var existing))
            {
                if (existing.signature == signature) continue;
                DestroyJoint(existing, id);
            }
            var constraint = CreateJoint(source);
            if (constraint != IntPtr.Zero)
            {
                JN.JoltC_PhysicsSystem_AddConstraint(system, constraint);
                joints.Add(id, new JointEntry
                {
                    source = source, constraint = constraint, signature = signature, isHinge = source is HingeJoint,
                    other = source.connectedBody ? bodies[source.connectedBody.GetInstanceID()] : null,
                    self = bodies[source.GetComponent<Rigidbody>().GetInstanceID()]
                });
            }
        }
    }

    private void DestroyJoint(JointEntry entry, int key)
    {
        if (entry.constraint != IntPtr.Zero)
        {
            JN.JoltC_PhysicsSystem_RemoveConstraint(system, entry.constraint);
            JN.JoltC_Constraint_Destroy(entry.constraint);
            entry.constraint = IntPtr.Zero;
        }
        joints.Remove(key);
    }

    private void DestroyJointsForBody(BodyEntry body)
    {
        // Native constraints retain body pointers. Detach them before freeing either endpoint.
        var attached = new List<int>();
        foreach (var pair in joints)
            if (ReferenceEquals(pair.Value.other, body) || ReferenceEquals(pair.Value.self, body)) attached.Add(pair.Key);
        foreach (int id in attached) DestroyJoint(joints[id], id);
    }

    /// <summary>
    /// Hashes everything that changes the meaning of a joint. When this is unchanged the existing
    /// native constraint is kept, which preserves Jolt's warm-start impulses across frames.
    /// </summary>
    private static int JointSignature(UJoint joint)
    {
        int h = joint.connectedBody ? joint.connectedBody.GetInstanceID() : 0;
        h = Mix(h, joint.anchor); h = Mix(h, joint.connectedAnchor); h = Mix(h, joint.axis);
        h = Mix(h, joint.autoConfigureConnectedAnchor);
        h = Mix(h, joint.enableCollision);
        if (joint is HingeJoint hinge)
        {
            h = Mix(h, hinge.useLimits); h = Mix(h, hinge.limits);
            h = Mix(h, hinge.useMotor); h = Mix(h, hinge.motor);
            h = Mix(h, hinge.useSpring); h = Mix(h, hinge.spring);
        }
        if (joint is SpringJoint spring)
        {
            h = Mix(h, spring.minDistance); h = Mix(h, spring.maxDistance);
            h = Mix(h, spring.spring); h = Mix(h, spring.damper);
        }
        if (joint is ConfigurableJoint c)
        {
            h = Mix(h, c.xMotion); h = Mix(h, c.yMotion); h = Mix(h, c.zMotion);
            h = Mix(h, c.angularXMotion); h = Mix(h, c.angularYMotion); h = Mix(h, c.angularZMotion);
            h = Mix(h, c.linearLimit); h = Mix(h, c.linearLimitSpring);
            h = Mix(h, c.lowAngularXLimit); h = Mix(h, c.highAngularXLimit);
            h = Mix(h, c.angularYLimit); h = Mix(h, c.angularZLimit);
            h = Mix(h, c.xDrive); h = Mix(h, c.yDrive); h = Mix(h, c.zDrive);
            h = Mix(h, c.secondaryAxis);
            h = Mix(h, c.targetPosition); h = Mix(h, c.targetVelocity);
        }
        return h;
    }

    private static int Mix<T>(int hash, T value) where T : struct => unchecked(hash * 31 + value.GetHashCode());

    /// <summary>
    /// Resolves the two native bodies a Unity joint connects, plus the shared world anchor and axis.
    /// </summary>
    private bool ResolveJoint(UJoint source, out BodyEntry a, out BodyEntry b,
        out Vector3 worldAnchor, out Vector3 worldAxis)
    {
        a = null; b = null; worldAnchor = Vector3.zero; worldAxis = Vector3.forward;
        var rb = source.GetComponent<Rigidbody>();
        if (!rb) return false;

        b = EnsureBody(rb, rb.transform);
        // A joint with no connectedBody is anchored to a fixed point in the world; Jolt's convention
        // is body id 0xFFFFFFFF for the implicit static "fixed to world" body.
        if (source.connectedBody) a = EnsureBody(source.connectedBody, source.connectedBody.transform);
        worldAnchor = source.transform.TransformPoint(source.anchor);
        worldAxis = source.axis.sqrMagnitude > 0f ? source.transform.TransformDirection(source.axis).normalized : Vector3.forward;
        return true;
    }

    private static uint BodyIdOf(BodyEntry entry) => entry?.id ?? JN.BodyIdInvalid;

    /// <summary>
    /// Builds an orthonormal basis around <paramref name="axis"/> so a hinge has a well-defined
    /// reference direction; Jolt rejects a hinge whose normal axis is parallel to its hinge axis.
    /// </summary>
    private static void Basis(Vector3 axis, out Vector3 hinge, out Vector3 normal)
    {
        hinge = axis.sqrMagnitude > 0f ? axis.normalized : Vector3.forward;
        Vector3 reference = Mathf.Abs(Vector3.Dot(hinge, Vector3.up)) > 0.99f ? Vector3.right : Vector3.up;
        normal = Vector3.Cross(hinge, reference).normalized;
    }

    private IntPtr CreateJoint(UJoint source)
    {
        if (!ResolveJoint(source, out var a, out var b, out var worldAnchor, out var worldAxis))
            return IntPtr.Zero;

        var self = b;
        var other = a;

        if (source is FixedJoint)
        {
            var settings = new JN.FixedConstraintSettings();
            JN.JoltC_FixedConstraintSettings_Init(ref settings);
            // Reusing the same anchor point for both bodies reproduces Unity's fixed-joint behaviour
            // even when the two transforms have drifted apart.
            settings.space = JN.ConstraintSpaceWorld;
            settings.autoDetectPoint = 0;
            settings.point1 = settings.point2 = new JN.RVec3(worldAnchor.x, worldAnchor.y, worldAnchor.z);
            Basis(GetAxisOrDefault(source), out var hinge, out var normal);
            settings.axisX1 = settings.axisX2 = new JN.Vec3(hinge.x, hinge.y, hinge.z);
            settings.axisY1 = settings.axisY2 = new JN.Vec3(normal.x, normal.y, normal.z);
            return JN.JoltC_FixedConstraint_Create(system, BodyIdOf(other), BodyIdOf(self), ref settings);
        }

        if (source is HingeJoint hingeJoint)
        {
            var settings = new JN.HingeConstraintSettings();
            JN.JoltC_HingeConstraintSettings_Init(ref settings);
            settings.space = JN.ConstraintSpaceWorld;
            settings.point1 = settings.point2 = new JN.RVec3(worldAnchor.x, worldAnchor.y, worldAnchor.z);
            Basis(worldAxis, out var hinge, out var normal);
            settings.hingeAxis1 = settings.hingeAxis2 = new JN.Vec3(hinge.x, hinge.y, hinge.z);
            settings.normalAxis1 = settings.normalAxis2 = new JN.Vec3(normal.x, normal.y, normal.z);
            settings.limitsMin = hingeJoint.useLimits ? hingeJoint.limits.min * Mathf.Deg2Rad : -Mathf.PI;
            settings.limitsMax = hingeJoint.useLimits ? hingeJoint.limits.max * Mathf.Deg2Rad : Mathf.PI;
            settings.maxFrictionTorque = 0f;
            settings.motorSettings = DefaultMotor();
            // The motor settings block is not filled by Init, so set the limits explicitly.
            settings.motorSettings.maxTorqueLimit = hingeJoint.useMotor ? Mathf.Max(0f, hingeJoint.motor.force) : 0f;
            settings.motorSettings.minTorqueLimit = -settings.motorSettings.maxTorqueLimit;

            IntPtr constraint = JN.JoltC_HingeConstraint_Create(system, BodyIdOf(other), BodyIdOf(self), ref settings);
            if (constraint != IntPtr.Zero)
            {
                if (hingeJoint.useMotor)
                {
                    JN.JoltC_HingeConstraint_SetMotorState(constraint, JN.MotorStateVelocity);
                    JN.JoltC_HingeConstraint_SetTargetAngularVelocity(constraint, hingeJoint.motor.targetVelocity * Mathf.Deg2Rad);
                }
                if (hingeJoint.useSpring)
                    JN.JoltC_HingeConstraint_SetTargetAngle(constraint, hingeJoint.spring.targetPosition * Mathf.Deg2Rad);
            }
            return constraint;
        }

        if (source is SpringJoint spring)
        {
            var settings = new JN.DistanceConstraintSettings();
            JN.JoltC_DistanceConstraintSettings_Init(ref settings);
            settings.space = JN.ConstraintSpaceWorld;
            Vector3 otherAnchor = source.connectedBody
                ? source.connectedBody.transform.TransformPoint(source.connectedAnchor)
                : source.connectedAnchor;
            settings.point1 = new JN.RVec3(otherAnchor.x, otherAnchor.y, otherAnchor.z);
            settings.point2 = new JN.RVec3(worldAnchor.x, worldAnchor.y, worldAnchor.z);
            float rest = Vector3.Distance(worldAnchor, otherAnchor);
            settings.minDistance = Mathf.Max(0.001f, rest + spring.minDistance);
            settings.maxDistance = Mathf.Max(settings.minDistance, rest + spring.maxDistance);
            // Jolt expresses a distance spring in the frequency/damping domain, not stiffness.
            settings.limitsSpringSettings = ToSpring(spring.spring, spring.damper, CombinedMass(spring));
            return JN.JoltC_DistanceConstraint_Create(system, BodyIdOf(other), BodyIdOf(self), ref settings);
        }

        if (source is ConfigurableJoint slider && slider.angularXMotion == ConfigurableJointMotion.Locked &&
            slider.angularYMotion == ConfigurableJointMotion.Locked && slider.angularZMotion == ConfigurableJointMotion.Locked)
        {
            var motions = new[] { slider.xMotion, slider.yMotion, slider.zMotion };
            int movingAxis = -1;
            for (int i = 0; i < 3; i++)
                if (motions[i] != ConfigurableJointMotion.Locked)
                {
                    if (movingAxis >= 0) throw new NotSupportedException("Jolt slider requires one linear axis: " + source.name);
                    movingAxis = i;
                }
            if (movingAxis >= 0)
            {
                Vector3 x = slider.axis.normalized;
                Vector3 z = Vector3.Cross(x, slider.secondaryAxis).normalized;
                Vector3 y = Vector3.Cross(z, x).normalized;
                worldAxis = slider.transform.TransformDirection(movingAxis == 0 ? x : movingAxis == 1 ? y : z).normalized;
                var settings = new JN.SliderConstraintSettings();
                JN.JoltC_SliderConstraintSettings_Init(ref settings);
                settings.space = JN.ConstraintSpaceWorld;
                settings.autoDetectPoint = 0;
                Vector3 otherAnchor = slider.connectedBody
                    ? slider.connectedBody.transform.TransformPoint(slider.connectedAnchor) : slider.connectedAnchor;
                settings.point1 = new JN.RVec3(otherAnchor.x, otherAnchor.y, otherAnchor.z);
                settings.point2 = new JN.RVec3(worldAnchor.x, worldAnchor.y, worldAnchor.z);
                Basis(worldAxis, out var axis, out var normal);
                settings.sliderAxis1 = settings.sliderAxis2 = new JN.Vec3(axis.x, axis.y, axis.z);
                settings.normalAxis1 = settings.normalAxis2 = new JN.Vec3(normal.x, normal.y, normal.z);
                float limit = Mathf.Max(0f, slider.linearLimit.limit);
                settings.limitsMin = motions[movingAxis] == ConfigurableJointMotion.Limited ? -limit : float.MinValue;
                settings.limitsMax = motions[movingAxis] == ConfigurableJointMotion.Limited ? limit : float.MaxValue;
                settings.limitsSpringSettings = ToSpring(slider.linearLimitSpring.spring, slider.linearLimitSpring.damper, CombinedMass(slider));
                JointDrive drive = movingAxis == 0 ? slider.xDrive : movingAxis == 1 ? slider.yDrive : slider.zDrive;
                settings.motorSettings = DefaultMotor();
                settings.motorSettings.springSettings = ToSpring(drive.positionSpring, drive.positionDamper, CombinedMass(slider));
                settings.motorSettings.maxForceLimit = Mathf.Max(0f, drive.maximumForce);
                settings.motorSettings.minForceLimit = -settings.motorSettings.maxForceLimit;
                IntPtr constraint = JN.JoltC_SliderConstraint_Create(system, BodyIdOf(other), BodyIdOf(self), ref settings);
                if (constraint != IntPtr.Zero && drive.maximumForce > 0f)
                {
                    JN.JoltC_SliderConstraint_SetMotorState(constraint,
                        drive.positionSpring > 0f ? JN.MotorStatePosition : JN.MotorStateVelocity);
                    JN.JoltC_SliderConstraint_SetTargetPosition(constraint, -slider.targetPosition[movingAxis]);
                    JN.JoltC_SliderConstraint_SetTargetVelocity(constraint, -slider.targetVelocity[movingAxis]);
                }
                return constraint;
            }
        }
        if (source is ConfigurableJoint)
            throw new NotSupportedException("Jolt does not yet support ConfigurableJoint: " + source.name);
        if (source is CharacterJoint)
            throw new NotSupportedException("Jolt does not yet support CharacterJoint: " + source.name);
        throw new NotSupportedException("Unsupported Jolt joint: " + source.GetType().Name);
    }

    private static Vector3 GetAxisOrDefault(UJoint joint)
    {
        Vector3 axis = joint.axis;
        return axis.sqrMagnitude > 0f ? joint.transform.TransformDirection(axis).normalized : joint.transform.forward;
    }

    private static JN.MotorSettings DefaultMotor() => new JN.MotorSettings
    {
        springSettings = new JN.SpringSettings { mode = JN.SpringModeFrequency, frequencyOrStiffness = 0f, damping = 1f },
        minForceLimit = float.MinValue,
        maxForceLimit = float.MaxValue,
        minTorqueLimit = float.MinValue,
        maxTorqueLimit = float.MaxValue,
    };

    /// <summary>Effective mass of the jointed pair, used to turn stiffness into a spring frequency.</summary>
    private static float CombinedMass(UJoint joint)
    {
        var rb = joint.GetComponent<Rigidbody>();
        float mass = rb ? rb.mass : 1f;
        if (joint.connectedBody && !joint.connectedBody.isKinematic && joint.connectedBody.mass > 0f)
            mass = mass * joint.connectedBody.mass / (mass + joint.connectedBody.mass);
        return Mathf.Max(mass, 0.0001f);
    }

    /// <summary>
    /// Converts Unity's stiffness/damper spring into Jolt's frequency/damping ratio pair.
    /// </summary>
    /// <remarks>
    /// Unity's <c>spring</c> is a Hooke constant in N/m and <c>damper</c> a damping coefficient in
    /// Ns/m, whereas Jolt takes a natural frequency in Hz plus a dimensionless damping ratio
    /// (1 = critical). The conversion below is the standard second-order system identity; running
    /// outside the ratio's useful range (roughly 0..2) makes the spring feel wrong, which is why the
    /// ratio is clamped.
    /// </remarks>
    private static JN.SpringSettings ToSpring(float stiffness, float damping, float mass)
    {
        if (stiffness <= 0f)
            return new JN.SpringSettings { mode = JN.SpringModeFrequency, frequencyOrStiffness = 0f, damping = 0f };
        float frequency = Mathf.Sqrt(stiffness / mass) / (2f * Mathf.PI);
        float ratio = damping / (2f * Mathf.Sqrt(stiffness * mass));
        return new JN.SpringSettings
        {
            mode = JN.SpringModeFrequency,
            frequencyOrStiffness = Mathf.Clamp(frequency, 0f, 200f),
            damping = Mathf.Clamp(ratio, 0f, 2f),
        };
    }

    /// <summary>
    /// Placeholder for broken-constraint handling.
    /// </summary>
    /// <remarks>
    /// The Box3D backend breaks joints itself by polling the native constraint force against Unity's
    /// <c>breakForce</c>/<c>breakTorque</c>. JoltC exposes no equivalent force query on its constraint
    /// API, so the Jolt backend cannot reproduce threshold breaking on its own. Joints still tear
    /// because the game drives breaks through gameplay logic and
    /// <c>BasePart.HandleJointBreak</c>; the per-joint numeric thresholds are the part that does not
    /// carry over. See <c>docs/PhysicsBackends.md</c>.
    /// </remarks>
    private void BreakJoints()
    {
    }
}
