using System;
using System.Collections.Generic;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.Constraints;
using UnityEngine;
using UJoint = UnityEngine.Joint;

public sealed partial class BepuBackend
{
    private sealed class JointEntry
    {
        public UJoint source;
        public ConstraintHandle primary;
        public bool hasPrimary;
        public readonly List<ConstraintHandle> extra = new List<ConstraintHandle>(2);
        public int signature;
        public int frameSignature;
        public BodyHandle a;
        public BodyHandle b;
    }

    private readonly Dictionary<int, JointEntry> joints = new Dictionary<int, JointEntry>();
    private readonly HashSet<UJoint> pendingJointDestruction = new HashSet<UJoint>();
    private BodyHandle anchor;
    private float lastDelta = 0.02f;

    /// <summary>
    /// A Bepu constraint needs two <see cref="BodyHandle"/>s, so a joint whose <c>connectedBody</c> is
    /// null - "anchored to the world" in Unity - is tied to one shared kinematic anchor body instead,
    /// which is the same device <c>Box3DBackend</c> uses.
    /// </summary>
    private void CreateAnchor()
    {
        anchor = simulation.Bodies.Add(BodyDescription.CreateKinematic(
            new RigidPose(System.Numerics.Vector3.Zero, System.Numerics.Quaternion.Identity),
            new CollidableDescription(default, 0f), new BodyActivityDescription(-1f, 32)));
    }

    /// <summary>
    /// Current hinge angle in degrees, read from the two body poses rather than from the solver - Bepu
    /// exposes no angle query, and the relative rotation about the hinge axis is exactly the quantity
    /// Unity's <see cref="HingeJoint.angle"/> reports.
    /// </summary>
    public float HingeAngle(HingeJoint source)
    {
        if (!joints.TryGetValue(source.GetInstanceID(), out var entry) || !entry.hasPrimary)
            return source.angle;
        var bodyA = simulation.Bodies.GetBodyReference(entry.a);
        var bodyB = simulation.Bodies.GetBodyReference(entry.b);
        if (!bodyA.Exists || !bodyB.Exists) return source.angle;
        Quaternion a = ToUnity(bodyA.Pose.Orientation);
        Quaternion b = ToUnity(bodyB.Pose.Orientation);
        Quaternion relative = Quaternion.Inverse(a) * b;
        Vector3 axis = source.axis.sqrMagnitude > 0f ? source.axis.normalized : Vector3.right;
        relative.ToAngleAxis(out float angle, out Vector3 rotationAxis);
        if (Vector3.Dot(rotationAxis, axis) < 0f) angle = -angle;
        return Mathf.DeltaAngle(0f, angle);
    }

    private void SynchronizeJoints()
    {
        pendingJointDestruction.RemoveWhere(joint => !joint);
        removed.Clear();
        foreach (var pair in joints)
            if (!pair.Value.source || !pair.Value.source.gameObject.activeInHierarchy)
                removed.Add(pair.Key);
        foreach (int id in removed) { DestroyJoint(joints[id]); joints.Remove(id); }

        foreach (var source in GamePhysics.FindSceneObjects<UJoint>())
        {
            if (!source.gameObject.activeInHierarchy || pendingJointDestruction.Contains(source)) continue;
            int id = source.GetInstanceID();
            int signature = JointSignature(source);
            int frameSignature = FrameSignature(source);
            if (joints.TryGetValue(id, out var existing))
            {
                if (existing.signature == signature) continue;
                // Reconfiguration: rebuild rather than retune, because the constraint *type* can change.
                DestroyJoint(existing);
                joints.Remove(id);
            }
            var created = TryCreateJoint(source, frameSignature, signature);
            if (created != null) joints.Add(id, created);
        }
    }

    private void DestroyJoint(JointEntry entry)
    {
        if (entry.hasPrimary && simulation.Solver.ConstraintExists(entry.primary))
            simulation.Solver.Remove(entry.primary);
        foreach (var handle in entry.extra)
            if (simulation.Solver.ConstraintExists(handle)) simulation.Solver.Remove(handle);
        entry.extra.Clear();
        entry.hasPrimary = false;
    }

    // ------------------------------------------------------------------ signatures
    //
    // Deliberately hashes the individual float/int fields instead of calling GetHashCode on Unity's
    // joint structs. JointLimits/JointMotor/JointSpring/JointDrive/SoftJointLimit do not override
    // GetHashCode, so the generic path lands on ValueType.GetHashCode() - a reflection call per joint
    // per step. docs/PhysicsPerformanceAnalysis.md flags exactly that in the Box3D backend.

    private static int Mix(int hash, int value) => unchecked(hash * 31 + value);
    private static int Mix(int hash, float value) => unchecked(hash * 31 + value.GetHashCode());
    private static int Mix(int hash, Vector3 value) => Mix(Mix(Mix(hash, value.x), value.y), value.z);

    private static int FrameSignature(UJoint joint)
    {
        int h = joint.connectedBody ? joint.connectedBody.GetInstanceID() : 0;
        h = Mix(h, joint.anchor);
        h = Mix(h, joint.connectedAnchor);
        h = Mix(h, joint.axis);
        if (joint is ConfigurableJoint configurable) h = Mix(h, configurable.secondaryAxis);
        return h;
    }

    private static int JointSignature(UJoint joint)
    {
        int h = FrameSignature(joint);
        h = Mix(h, joint.breakForce);
        h = Mix(h, joint.breakTorque);
        h = Mix(h, joint.enableCollision ? 1 : 0);
        if (joint is HingeJoint hinge)
        {
            h = Mix(h, hinge.useLimits ? 1 : 0);
            h = Mix(h, hinge.limits.min); h = Mix(h, hinge.limits.max);
            h = Mix(h, hinge.limits.bounciness); h = Mix(h, hinge.limits.bounceMinVelocity);
            h = Mix(h, hinge.useMotor ? 1 : 0);
            h = Mix(h, hinge.motor.targetVelocity); h = Mix(h, hinge.motor.force); h = Mix(h, hinge.motor.freeSpin ? 1 : 0);
            h = Mix(h, hinge.useSpring ? 1 : 0);
            h = Mix(h, hinge.spring.spring); h = Mix(h, hinge.spring.damper);
            h = Mix(h, hinge.spring.targetPosition);
        }
        if (joint is SpringJoint spring)
        {
            h = Mix(h, spring.minDistance); h = Mix(h, spring.maxDistance);
            h = Mix(h, spring.spring); h = Mix(h, spring.damper);
        }
        if (joint is ConfigurableJoint configurable)
        {
            h = Mix(h, (int)configurable.xMotion); h = Mix(h, (int)configurable.yMotion); h = Mix(h, (int)configurable.zMotion);
            h = Mix(h, (int)configurable.angularXMotion); h = Mix(h, (int)configurable.angularYMotion);
            h = Mix(h, (int)configurable.angularZMotion);
            h = Mix(h, configurable.linearLimit.limit); h = Mix(h, configurable.linearLimit.bounciness);
            h = Mix(h, configurable.lowAngularXLimit.limit); h = Mix(h, configurable.highAngularXLimit.limit);
            h = Mix(h, configurable.angularYLimit.limit); h = Mix(h, configurable.angularZLimit.limit);
            h = Mix(h, configurable.targetPosition); h = Mix(h, configurable.targetVelocity);
            foreach (var drive in new[] { configurable.xDrive, configurable.yDrive, configurable.zDrive })
            {
                h = Mix(h, drive.positionSpring); h = Mix(h, drive.positionDamper); h = Mix(h, drive.maximumForce);
            }
        }
        return h;
    }

    // ------------------------------------------------------------------ construction

    private JointEntry TryCreateJoint(UJoint source, int frameSignature, int signature)
    {
        var self = source.GetComponent<Rigidbody>();
        if (!self) return null;
        var owner = FindByOwner(self.GetInstanceID());
        if (owner == null || owner.bodyHandle < 0) return null;

        BodyHandle selfHandle = new BodyHandle(owner.bodyHandle);
        BodyHandle otherHandle;
        if (source.connectedBody)
        {
            var other = FindByOwner(source.connectedBody.GetInstanceID());
            if (other == null || other.bodyHandle < 0) return null;
            otherHandle = new BodyHandle(other.bodyHandle);
        }
        else
        {
            otherHandle = anchor;
        }

        Vector3 worldAnchorSelf = source.transform.TransformPoint(source.anchor);
        Vector3 worldAnchorOther = source.connectedBody
            ? source.connectedBody.transform.TransformPoint(source.connectedAnchor)
            : source.connectedAnchor;

        var bodySelf = simulation.Bodies.GetBodyReference(selfHandle);
        var bodyOther = simulation.Bodies.GetBodyReference(otherHandle);
        if (!bodySelf.Exists || !bodyOther.Exists) return null;

        var offsetSelf = ToNumerics(Quaternion.Inverse(ToUnity(bodySelf.Pose.Orientation)) * (worldAnchorSelf - ToUnity(bodySelf.Pose.Position)));
        var offsetOther = ToNumerics(Quaternion.Inverse(ToUnity(bodyOther.Pose.Orientation)) * (worldAnchorOther - ToUnity(bodyOther.Pose.Position)));

        var entry = new JointEntry
        {
            source = source, signature = signature, frameSignature = frameSignature,
            a = otherHandle, b = selfHandle
        };

        try
        {
            if (source is FixedJoint)
            {
                // A 3-axis angular weld is singular when both bodies have only Z rotation.
                // Keep the positional attachment and constrain the remaining twist alone.
                var planarLocks = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
                if ((self.constraints & planarLocks) == planarLocks && source.connectedBody &&
                    (source.connectedBody.constraints & planarLocks) == planarLocks)
                {
                    entry.primary = simulation.Solver.Add(otherHandle, selfHandle, new BallSocket
                    {
                        LocalOffsetA = offsetOther, LocalOffsetB = offsetSelf,
                        SpringSettings = new SpringSettings(30f, 1f)
                    });
                    entry.hasPrimary = true;
                    entry.extra.Add(simulation.Solver.Add(otherHandle, selfHandle, new TwistServo
                    {
                        LocalBasisA = ToNumerics(Quaternion.Inverse(ToUnity(bodyOther.Pose.Orientation))),
                        LocalBasisB = ToNumerics(Quaternion.Inverse(ToUnity(bodySelf.Pose.Orientation))),
                        TargetAngle = 0f, ServoSettings = ServoSettings.Default,
                        SpringSettings = new SpringSettings(30f, 1f)
                    }));
                    return entry;
                }
                var weld = new Weld
                {
                    LocalOffset = offsetOther,
                    LocalOrientation = System.Numerics.Quaternion.Identity,
                    SpringSettings = new SpringSettings(30f, 1f)
                };
                entry.primary = simulation.Solver.Add(otherHandle, selfHandle, weld);
                entry.hasPrimary = true;
                return entry;
            }
            if (source is HingeJoint hinge)
            {
                Vector3 worldAxis = source.transform.TransformDirection(
                    hinge.axis.sqrMagnitude > 0f ? hinge.axis.normalized : Vector3.right);
                var axisOther = ToNumerics(Quaternion.Inverse(ToUnity(bodyOther.Pose.Orientation)) * worldAxis);
                var axisSelf = ToNumerics(Quaternion.Inverse(ToUnity(bodySelf.Pose.Orientation)) * worldAxis);
                entry.primary = simulation.Solver.Add(otherHandle, selfHandle, new Hinge
                {
                    LocalOffsetA = offsetOther,
                    LocalOffsetB = offsetSelf,
                    LocalHingeAxisA = axisOther,
                    LocalHingeAxisB = axisSelf,
                    SpringSettings = new SpringSettings(30f, 1f)
                });
                entry.hasPrimary = true;

                Quaternion basisWorld = Quaternion.LookRotation(worldAxis, Vector3.up);
                var basisOther = ToNumerics(Quaternion.Inverse(ToUnity(bodyOther.Pose.Orientation)) * basisWorld);
                var basisSelf = ToNumerics(Quaternion.Inverse(ToUnity(bodySelf.Pose.Orientation)) * basisWorld);

                if (hinge.useLimits)
                {
                    entry.extra.Add(simulation.Solver.Add(otherHandle, selfHandle, new TwistLimit
                    {
                        LocalBasisA = basisOther,
                        LocalBasisB = basisSelf,
                        MinimumAngle = hinge.limits.min * Mathf.Deg2Rad,
                        MaximumAngle = hinge.limits.max * Mathf.Deg2Rad,
                        SpringSettings = new SpringSettings(Mathf.Max(1f, hinge.limits.bounciness * 30f), 1f)
                    }));
                }
                if (hinge.useMotor)
                {
                    entry.extra.Add(simulation.Solver.Add(otherHandle, selfHandle, new TwistMotor
                    {
                        LocalAxisA = axisOther,
                        LocalAxisB = axisSelf,
                        TargetVelocity = hinge.motor.targetVelocity * Mathf.Deg2Rad,
                        Settings = new MotorSettings(Mathf.Max(0.0001f, hinge.motor.force), 0.005f)
                    }));
                }
                if (hinge.useSpring && hinge.spring.spring > 0f)
                {
                    entry.extra.Add(simulation.Solver.Add(otherHandle, selfHandle, new TwistServo
                    {
                        LocalBasisA = basisOther,
                        LocalBasisB = basisSelf,
                        TargetAngle = hinge.spring.targetPosition * Mathf.Deg2Rad,
                        SpringSettings = new SpringSettings(
                            Mathf.Max(0.01f, Mathf.Sqrt(hinge.spring.spring) / (2f * Mathf.PI)), 1f),
                        ServoSettings = new ServoSettings(float.MaxValue, 0f, float.MaxValue)
                    }));
                }
                return entry;
            }
            if (source is SpringJoint springJoint)
            {
                float rest = Vector3.Distance(worldAnchorSelf, worldAnchorOther);
                entry.primary = simulation.Solver.Add(otherHandle, selfHandle, new DistanceServo
                {
                    LocalOffsetA = offsetOther,
                    LocalOffsetB = offsetSelf,
                    TargetDistance = Mathf.Max(0.001f, springJoint.maxDistance > 0f
                        ? rest + springJoint.maxDistance : rest),
                    SpringSettings = new SpringSettings(
                        Mathf.Max(0.01f, Mathf.Sqrt(Mathf.Max(0.0001f, springJoint.spring)) / (2f * Mathf.PI)), 1f),
                    ServoSettings = new ServoSettings(float.MaxValue, 0f, float.MaxValue)
                });
                entry.hasPrimary = true;
                entry.extra.Add(simulation.Solver.Add(otherHandle, selfHandle, new DistanceLimit
                {
                    LocalOffsetA = offsetOther,
                    LocalOffsetB = offsetSelf,
                    MinimumDistance = Mathf.Max(0.001f, rest + springJoint.minDistance),
                    MaximumDistance = Mathf.Max(0.002f, rest + springJoint.maxDistance),
                    SpringSettings = new SpringSettings(30f, 1f)
                }));
                return entry;
            }
            if (source is ConfigurableJoint configurable)
            {
                if (!TryCreateConfigurable(configurable, entry, bodyOther, bodySelf, offsetOther, offsetSelf))
                {
                    DestroyJoint(entry);
                    return null;
                }
                return entry;
            }
            // CharacterJoint and anything else this backend has no counterpart for. Returning null
            // leaves the joint unmirrored rather than throwing, so one exotic joint does not stop the
            // engine switch - the same "declare it, do not silently approximate it" rule the Jolt and
            // Havok backends document.
            if (jointUnsupportedLogged.Add(source.GetType().Name))
                Debug.LogWarning("Bepu backend: joint type " + source.GetType().Name +
                    " has no counterpart and is not simulated natively.");
            return null;
        }
        catch (Exception error)
        {
            if (jointFailureLogged.Add(error.GetType().Name))
                Debug.LogWarning("Bepu backend: joint creation failed: " + error.Message);
            DestroyJoint(entry);
            return null;
        }
    }

    private readonly HashSet<string> jointUnsupportedLogged = new HashSet<string>();
    private readonly HashSet<string> jointFailureLogged = new HashSet<string>();

    /// <summary>
    /// Maps the ConfigurableJoint motion combinations this project actually authors. Anything else is
    /// refused instead of approximated - the project deliberately reports unsupported content rather
    /// than mis-simulating it.
    /// </summary>
    private bool TryCreateConfigurable(ConfigurableJoint joint, JointEntry entry,
        BodyReference bodyOther, BodyReference bodySelf,
        System.Numerics.Vector3 offsetOther, System.Numerics.Vector3 offsetSelf)
    {
        int linearFree = 0, angularFree = 0;
        var linear = new[] { joint.xMotion, joint.yMotion, joint.zMotion };
        var angular = new[] { joint.angularXMotion, joint.angularYMotion, joint.angularZMotion };
        for (int i = 0; i < 3; i++)
        {
            if (linear[i] != ConfigurableJointMotion.Locked) linearFree++;
            if (angular[i] != ConfigurableJointMotion.Locked) angularFree++;
        }
        if (linearFree == 0 && angularFree == 0)
        {
            entry.primary = simulation.Solver.Add(entry.a, entry.b, new Weld
            {
                LocalOffset = offsetOther,
                LocalOrientation = System.Numerics.Quaternion.Identity,
                SpringSettings = new SpringSettings(30f, 1f)
            });
            entry.hasPrimary = true;
            return true;
        }
        if (linearFree == 1 && angularFree == 0)
        {
            int axisIndex = linear[0] != ConfigurableJointMotion.Locked ? 0
                : linear[1] != ConfigurableJointMotion.Locked ? 1 : 2;
            Vector3 x = joint.axis.normalized;
            Vector3 z = Vector3.Cross(x, joint.secondaryAxis).normalized;
            Vector3 y = Vector3.Cross(z, x).normalized;
            Vector3 worldAxis = joint.transform.TransformDirection(axisIndex == 0 ? x : axisIndex == 1 ? y : z).normalized;
            var axisOther = ToNumerics(Quaternion.Inverse(ToUnity(bodyOther.Pose.Orientation)) * worldAxis);
            var rigidSpring = new SpringSettings(30f, 1f);
            entry.primary = simulation.Solver.Add(entry.a, entry.b, new PointOnLineServo
            {
                LocalOffsetA = offsetOther, LocalOffsetB = offsetSelf, LocalDirection = axisOther,
                SpringSettings = rigidSpring, ServoSettings = ServoSettings.Default
            });
            entry.hasPrimary = true;
            entry.extra.Add(simulation.Solver.Add(entry.a, entry.b, new AngularServo
            {
                TargetRelativeRotationLocalA = ToNumerics(Quaternion.Inverse(ToUnity(bodyOther.Pose.Orientation)) * ToUnity(bodySelf.Pose.Orientation)),
                SpringSettings = rigidSpring, ServoSettings = ServoSettings.Default
            }));
            if (linear[axisIndex] == ConfigurableJointMotion.Limited)
            {
                float limit = Mathf.Max(0f, joint.linearLimit.limit);
                entry.extra.Add(simulation.Solver.Add(entry.a, entry.b, new LinearAxisLimit
                {
                    LocalOffsetA = offsetOther, LocalOffsetB = offsetSelf, LocalAxis = axisOther,
                    MinimumOffset = -limit, MaximumOffset = limit, SpringSettings = rigidSpring
                }));
            }
            JointDrive drive = axisIndex == 0 ? joint.xDrive : axisIndex == 1 ? joint.yDrive : joint.zDrive;
            if (drive.positionSpring > 0f && drive.maximumForce > 0f)
            {
                float mass = joint.GetComponent<Rigidbody>().mass;
                if (joint.connectedBody && !joint.connectedBody.isKinematic)
                    mass = mass * joint.connectedBody.mass / (mass + joint.connectedBody.mass);
                float frequency = Mathf.Sqrt(drive.positionSpring / Mathf.Max(0.0001f, mass)) / (2f * Mathf.PI);
                float damping = drive.positionDamper / (2f * Mathf.Sqrt(drive.positionSpring * Mathf.Max(0.0001f, mass)));
                var servo = ServoSettings.Default;
                servo.MaximumForce = drive.maximumForce;
                entry.extra.Add(simulation.Solver.Add(entry.a, entry.b, new LinearAxisServo
                {
                    LocalOffsetA = offsetOther, LocalOffsetB = offsetSelf, LocalPlaneNormal = axisOther,
                    TargetOffset = -joint.targetPosition[axisIndex], ServoSettings = servo,
                    SpringSettings = new SpringSettings(frequency, damping)
                }));
            }
            return true;
        }
        if (linearFree == 0 && angularFree == 3)
        {
            entry.primary = simulation.Solver.Add(entry.a, entry.b, new BallSocket
            {
                LocalOffsetA = offsetOther,
                LocalOffsetB = offsetSelf,
                SpringSettings = new SpringSettings(30f, 1f)
            });
            entry.hasPrimary = true;
            return true;
        }
        if (linearFree == 0 && angularFree == 1)
        {
            int axisIndex = angular[0] != ConfigurableJointMotion.Locked ? 0
                : angular[1] != ConfigurableJointMotion.Locked ? 1 : 2;
            Vector3 localAxis = axisIndex == 0 ? Vector3.right : axisIndex == 1 ? Vector3.up : Vector3.forward;
            Vector3 worldAxis = joint.transform.TransformDirection(localAxis);
            var axisOther = ToNumerics(Quaternion.Inverse(ToUnity(bodyOther.Pose.Orientation)) * worldAxis);
            var axisSelf = ToNumerics(Quaternion.Inverse(ToUnity(bodySelf.Pose.Orientation)) * worldAxis);
            entry.primary = simulation.Solver.Add(entry.a, entry.b, new Hinge
            {
                LocalOffsetA = offsetOther,
                LocalOffsetB = offsetSelf,
                LocalHingeAxisA = axisOther,
                LocalHingeAxisB = axisSelf,
                SpringSettings = new SpringSettings(30f, 1f)
            });
            entry.hasPrimary = true;
            if (angular[axisIndex] == ConfigurableJointMotion.Limited)
            {
                float limit = axisIndex == 0 ? joint.highAngularXLimit.limit
                    : axisIndex == 1 ? joint.angularYLimit.limit : joint.angularZLimit.limit;
                float low = axisIndex == 0 ? joint.lowAngularXLimit.limit : -limit;
                entry.extra.Add(simulation.Solver.Add(entry.a, entry.b, new TwistLimit
                {
                    LocalBasisA = ToNumerics(Quaternion.Inverse(ToUnity(bodyOther.Pose.Orientation))
                        * Quaternion.LookRotation(worldAxis, Vector3.up)),
                    LocalBasisB = ToNumerics(Quaternion.Inverse(ToUnity(bodySelf.Pose.Orientation))
                        * Quaternion.LookRotation(worldAxis, Vector3.up)),
                    MinimumAngle = low * Mathf.Deg2Rad,
                    MaximumAngle = limit * Mathf.Deg2Rad,
                    SpringSettings = new SpringSettings(30f, 1f)
                }));
            }
            return true;
        }
        return false;
    }

    // ------------------------------------------------------------------ breaking

    /// <summary>
    /// Breaks joints whose accumulated solver impulse, expressed as a force over the step, exceeds the
    /// Unity <c>breakForce</c>/<c>breakTorque</c> threshold.
    /// </summary>
    /// <remarks>
    /// This is an <b>approximation</b>, and the honest comparison is the Jolt backend's documented
    /// limitation: PhysX compares solver constraint forces, while Bepu 2.3.4 only exposes
    /// <see cref="Solver.GetAccumulatedImpulseMagnitude"/> - an impulse, not a force. Dividing by the
    /// step length recovers a comparable quantity but the two solvers will not agree numerically, so a
    /// contraption may break slightly earlier or later than under PhysX.
    /// </remarks>
    private void BreakJoints()
    {
        pairScratchJoints.Clear();
        foreach (var pair in joints)
        {
            var entry = pair.Value;
            if (!entry.source || !entry.hasPrimary) continue;
            if (!simulation.Solver.ConstraintExists(entry.primary)) { pairScratchJoints.Add(pair.Key); continue; }
            float force = simulation.Solver.GetAccumulatedImpulseMagnitude(entry.primary) / Mathf.Max(0.0001f, lastDelta);
            float limit = float.IsInfinity(entry.source.breakForce) ? float.MaxValue
                : Mathf.Max(0f, entry.source.breakForce);
            if (force <= limit) continue;
            pendingJointDestruction.Add(entry.source);
            DestroyJoint(entry);
            entry.source.gameObject.SendMessage("OnJointBreak", force, SendMessageOptions.DontRequireReceiver);
            UnityEngine.Object.Destroy(entry.source);
            pairScratchJoints.Add(pair.Key);
        }
        foreach (int id in pairScratchJoints) joints.Remove(id);
    }

    private readonly List<int> pairScratchJoints = new List<int>(32);
}
