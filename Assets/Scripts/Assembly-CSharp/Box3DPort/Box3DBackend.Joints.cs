using System;
using System.Collections.Generic;
using Box3D;
using Unity.Mathematics;
using UnityEngine;
using UJoint = UnityEngine.Joint;
using BJoint = Box3D.Joint;

public sealed partial class Box3DBackend
{
    private sealed class JointEntry
    {
        public UJoint source;
        public BJoint native;
        public int signature;
        public int frameSignature;
    }
    private readonly Dictionary<int, JointEntry> joints = new Dictionary<int, JointEntry>();
    private readonly HashSet<UJoint> pendingJointDestruction = new HashSet<UJoint>();
    private readonly List<int> brokenJointIds = new List<int>();

    public float HingeAngle(HingeJoint source)
    {
        return joints.TryGetValue(source.GetInstanceID(), out var entry) && entry.native.IsValid ?
            new Box3D.RevoluteJoint { Id = entry.native.Id }.GetAngle() * Mathf.Rad2Deg : source.angle;
    }

    private void SynchronizeJoints()
    {
        pendingJointDestruction.RemoveWhere(joint => !joint);
        removed.Clear();
        foreach (var pair in joints)
            if (!pair.Value.source || !pair.Value.source.gameObject.activeInHierarchy || !pair.Value.native.IsValid)
                removed.Add(pair.Key);
        foreach (int id in removed)
        {
            if (joints[id].native.IsValid) joints[id].native.Destroy();
            joints.Remove(id);
        }
        foreach (var source in GamePhysics.FindSceneObjects<UJoint>())
        {
            if (!source.gameObject.activeInHierarchy || pendingJointDestruction.Contains(source)) continue;
            int id = source.GetInstanceID();
            // Joint settings are plain data; detect reconfiguration without rebuilding stable joints.
            int signature = JointSignature(source);
            int frameSignature = FrameSignature(source);
            bool keepFrames = false;
            B3Transform frameA = default, frameB = default;
            if (joints.TryGetValue(id, out var old))
            {
                if (old.signature == signature) continue;
                keepFrames = old.frameSignature == frameSignature;
                if (keepFrames) { frameA = old.native.GetLocalFrameA(); frameB = old.native.GetLocalFrameB(); }
                if (old.native.IsValid) old.native.Destroy();
                joints.Remove(id);
            }
            var native = CreateJoint(source);
            if (native.IsValid)
            {
                if (keepFrames) { native.SetLocalFrameA(frameA); native.SetLocalFrameB(frameB); }
                joints.Add(id, new JointEntry { source = source, native = native, signature = signature, frameSignature = frameSignature });
            }
        }
    }

    private static int JointSignature(UJoint joint)
    {
        int h = FrameSignature(joint);
        h = Mix(h, joint.breakForce); h = Mix(h, joint.breakTorque); h = Mix(h, joint.enableCollision);
        if (joint is HingeJoint hinge)
        {
            h = Mix(h, hinge.useLimits); h = Mix(h, hinge.limits); h = Mix(h, hinge.useMotor);
            h = Mix(h, hinge.motor); h = Mix(h, hinge.useSpring); h = Mix(h, hinge.spring);
        }
        if (joint is SpringJoint spring)
        {
            h = Mix(h, spring.minDistance); h = Mix(h, spring.maxDistance); h = Mix(h, spring.spring); h = Mix(h, spring.damper);
        }
        if (joint is ConfigurableJoint c)
        {
            h = Mix(h, c.xMotion); h = Mix(h, c.yMotion); h = Mix(h, c.zMotion);
            h = Mix(h, c.angularXMotion); h = Mix(h, c.angularYMotion); h = Mix(h, c.angularZMotion);
            h = Mix(h, c.linearLimit); h = Mix(h, c.linearLimitSpring);
            h = Mix(h, c.lowAngularXLimit); h = Mix(h, c.highAngularXLimit); h = Mix(h, c.angularYLimit); h = Mix(h, c.angularZLimit);
            h = Mix(h, c.xDrive); h = Mix(h, c.yDrive); h = Mix(h, c.zDrive);
            h = Mix(h, c.targetPosition); h = Mix(h, c.targetVelocity); h = Mix(h, c.targetRotation);
        }
        return h;
    }

    private static int Mix<T>(int hash, T value) where T : struct => unchecked(hash * 31 + value.GetHashCode());
    private static int FrameSignature(UJoint j)
    {
        int h = j.connectedBody ? j.connectedBody.GetInstanceID() : 0;
        h = Mix(h, j.anchor); h = Mix(h, j.connectedAnchor); h = Mix(h, j.axis);
        if (j is ConfigurableJoint c) h = Mix(h, c.secondaryAxis);
        return h;
    }

    private void SetupJoint(ref JointDefBase def, UJoint source, Vector3 worldAxis, bool slide = false)
    {
        Rigidbody rb = source.GetComponent<Rigidbody>();
        if (!rb) throw new NotSupportedException("Joint has no Rigidbody: " + source.name);
        var self = EnsureBody(rb, rb.transform);
        Body other = source.connectedBody ? EnsureBody(source.connectedBody, source.connectedBody.transform).native : anchor;
        def.BodyIdA = other.Id;
        def.BodyIdB = self.native.Id;
        Vector3 worldAnchor = source.transform.TransformPoint(source.anchor);
        Vector3 connectedAnchor = source.connectedBody ? source.connectedBody.transform.TransformPoint(source.connectedAnchor) : source.connectedAnchor;
        Quaternion frame = Quaternion.FromToRotation(slide ? Vector3.right : Vector3.forward, worldAxis.normalized);
        def.LocalFrameA = new B3Transform { Position = other.GetLocalPoint((float3)connectedAnchor), Rotation = math.mul(math.inverse(other.Rotation), (quaternion)frame) };
        def.LocalFrameB = new B3Transform { Position = self.native.GetLocalPoint((float3)worldAnchor), Rotation = math.mul(math.inverse(self.native.Rotation), (quaternion)frame) };
        def.CollideConnected = source.enableCollision;
        def.ForceThreshold = float.IsInfinity(source.breakForce) ? float.MaxValue : Mathf.Max(0, source.breakForce);
        def.TorqueThreshold = float.IsInfinity(source.breakTorque) ? float.MaxValue : Mathf.Max(0, source.breakTorque);
    }

    private static void SpringValues(float stiffness, float damping, float mass, out float hertz, out float ratio)
    {
        hertz = stiffness > 0 ? Mathf.Sqrt(stiffness / Mathf.Max(mass, 0.0001f)) / (2 * Mathf.PI) : 0;
        ratio = stiffness > 0 ? damping / (2 * Mathf.Sqrt(stiffness * Mathf.Max(mass, 0.0001f))) : 0;
    }

    private BJoint CreateJoint(UJoint source)
    {
        var rb = source.GetComponent<Rigidbody>();
        if (!rb) return default;
        float mass = source.connectedBody && !source.connectedBody.isKinematic ?
            1f / (1f / rb.mass + 1f / source.connectedBody.mass) : rb.mass;
        if (source is FixedJoint)
        {
            var def = WeldJointDef.Default;
            SetupJoint(ref def.Base, source, source.transform.forward);
            return world.CreateWeldJoint(def);
        }
        if (source is HingeJoint hinge)
        {
            var def = RevoluteJointDef.Default;
            SetupJoint(ref def.Base, source, source.transform.TransformDirection(source.axis));
            def.EnableLimit = hinge.useLimits;
            def.LowerAngle = hinge.limits.min * Mathf.Deg2Rad; def.UpperAngle = hinge.limits.max * Mathf.Deg2Rad;
            def.EnableMotor = hinge.useMotor; def.MotorSpeed = hinge.motor.targetVelocity * Mathf.Deg2Rad;
            def.MaxMotorTorque = Mathf.Max(0, hinge.motor.force);
            def.EnableSpring = hinge.useSpring;
            SpringValues(hinge.spring.spring, hinge.spring.damper, mass, out def.Hertz, out def.DampingRatio);
            def.TargetAngle = hinge.spring.targetPosition * Mathf.Deg2Rad;
            return world.CreateRevoluteJoint(def);
        }
        if (source is SpringJoint spring)
        {
            var def = DistanceJointDef.Default;
            SetupJoint(ref def.Base, source, source.transform.forward);
            Vector3 otherAnchor = source.connectedBody ? source.connectedBody.transform.TransformPoint(source.connectedAnchor) : source.connectedAnchor;
            float rest = Vector3.Distance(source.transform.TransformPoint(source.anchor), otherAnchor);
            def.Length = Mathf.Max(0.001f, rest);
            def.EnableSpring = spring.spring > 0;
            SpringValues(spring.spring, spring.damper, mass, out def.Hertz, out def.DampingRatio);
            def.EnableLimit = true;
            def.MinLength = Mathf.Max(0.001f, rest + spring.minDistance);
            def.MaxLength = Mathf.Max(def.MinLength, rest + spring.maxDistance);
            return world.CreateDistanceJoint(def);
        }
        if (source is ConfigurableJoint c) return CreateConfigurable(c, mass);
        if (source is CharacterJoint)
            throw new NotSupportedException("CharacterJoint is not supported by the Box3D adapter: " + source.name);
        throw new NotSupportedException("Unsupported Box3D joint: " + source.GetType().Name);
    }

    private BJoint CreateConfigurable(ConfigurableJoint c, float mass)
    {
        var linear = new[] { c.xMotion, c.yMotion, c.zMotion };
        var angular = new[] { c.angularXMotion, c.angularYMotion, c.angularZMotion };
        Vector3 x = c.axis.normalized;
        Vector3 z = Vector3.Cross(x, c.secondaryAxis).normalized;
        Vector3 y = Vector3.Cross(z, x).normalized;
        var axes = new[] { x, y, z };
        int translation = -1, rotation = -1, linearCount = 0, angularCount = 0;
        for (int i = 0; i < 3; i++)
        {
            if (linear[i] != ConfigurableJointMotion.Locked) { translation = i; linearCount++; }
            if (angular[i] != ConfigurableJointMotion.Locked) { rotation = i; angularCount++; }
        }
        if (linearCount == 0 && angularCount == 0)
        {
            var def = WeldJointDef.Default;
            SetupJoint(ref def.Base, c, c.transform.TransformDirection(z));
            return world.CreateWeldJoint(def);
        }
        if (linearCount == 0 && angularCount == 1)
        {
            var def = RevoluteJointDef.Default;
            SetupJoint(ref def.Base, c, c.transform.TransformDirection(axes[rotation]));
            def.EnableLimit = angular[rotation] == ConfigurableJointMotion.Limited;
            float limit = rotation == 0 ? c.highAngularXLimit.limit : rotation == 1 ? c.angularYLimit.limit : c.angularZLimit.limit;
            def.LowerAngle = (rotation == 0 ? c.lowAngularXLimit.limit : -limit) * Mathf.Deg2Rad;
            def.UpperAngle = limit * Mathf.Deg2Rad;
            return world.CreateRevoluteJoint(def);
        }
        if (linearCount == 0 && angularCount == 3 && c.angularXMotion == ConfigurableJointMotion.Free &&
            c.angularYMotion == ConfigurableJointMotion.Free && c.angularZMotion == ConfigurableJointMotion.Free)
        {
            var def = SphericalJointDef.Default;
            SetupJoint(ref def.Base, c, c.transform.forward);
            return world.CreateSphericalJoint(def);
        }
        if (linearCount == 1 && angularCount <= 1)
        {
            JointDrive drive = translation == 0 ? c.xDrive : translation == 1 ? c.yDrive : c.zDrive;
            if (angularCount == 1)
            {
                // Box3D wheel joint uses local frame X for suspension and Z for its axle.
                if (Mathf.Abs(Vector3.Dot(axes[translation], axes[rotation])) > 0.001f)
                    throw new NotSupportedException("Box3D wheel requires perpendicular slide and rotation axes: " + c.name);
                var def = WheelJointDef.Default;
                SetupJoint(ref def.Base, c, c.transform.TransformDirection(axes[rotation]));
                Quaternion frame = Quaternion.LookRotation(c.transform.TransformDirection(axes[rotation]),
                    c.transform.TransformDirection(Vector3.Cross(axes[rotation], axes[translation])));
                var a = new Body(def.Base.BodyIdA); var b = new Body(def.Base.BodyIdB);
                def.Base.LocalFrameA.Rotation = math.mul(math.inverse(a.Rotation), (quaternion)frame);
                def.Base.LocalFrameB.Rotation = math.mul(math.inverse(b.Rotation), (quaternion)frame);
                def.EnableSuspensionSpring = drive.positionSpring > 0;
                SpringValues(drive.positionSpring, drive.positionDamper, mass, out def.SuspensionHertz, out def.SuspensionDampingRatio);
                def.EnableSuspensionLimit = linear[translation] == ConfigurableJointMotion.Limited;
                def.LowerSuspensionLimit = -c.linearLimit.limit; def.UpperSuspensionLimit = c.linearLimit.limit;
                return world.CreateWheelJoint(def);
            }
            var slider = PrismaticJointDef.Default;
            SetupJoint(ref slider.Base, c, c.transform.TransformDirection(axes[translation]), true);
            slider.EnableLimit = linear[translation] == ConfigurableJointMotion.Limited;
            slider.LowerTranslation = -c.linearLimit.limit; slider.UpperTranslation = c.linearLimit.limit;
            slider.EnableSpring = drive.positionSpring > 0;
            SpringValues(drive.positionSpring, drive.positionDamper, mass, out slider.Hertz, out slider.DampingRatio);
            slider.TargetTranslation = c.targetPosition[translation];
            return world.CreatePrismaticJoint(slider);
        }
        throw new NotSupportedException("Unsupported ConfigurableJoint motion combination: " + c.name);
    }

    private void BreakJoints()
    {
        var broken = brokenJointIds;
        broken.Clear();
        foreach (var pair in joints)
        {
            var j = pair.Value;
            if (!j.source || !j.native.IsValid) continue;
            float force = math.length(j.native.GetConstraintForce());
            float torque = math.length(j.native.GetConstraintTorque());
            if (force > j.source.breakForce || torque > j.source.breakTorque)
            {
                // Several physics steps may run before Unity processes deferred Destroy.
                pendingJointDestruction.Add(j.source);
                j.native.Destroy();
                j.source.gameObject.SendMessage("OnJointBreak", force, SendMessageOptions.DontRequireReceiver);
                UnityEngine.Object.Destroy(j.source);
                broken.Add(pair.Key);
            }
        }
        foreach (int id in broken) joints.Remove(id);
    }
}
