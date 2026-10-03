using System;

namespace Box3D
{
    // Typed joint wrappers. All wrap the same JointId (box3d has no joint-type hierarchy in its
    // handles); each gets its b3{X}Joint_* surface via generated forwarders, plus implicit widening
    // to Joint for the common b3Joint_* surface.

    /// <summary>Distance joint: keeps two anchor points at a distance, optionally spring/limited.</summary>
    public partial struct DistanceJoint : IEquatable<DistanceJoint>
    {
        public JointId Id;

        public bool IsValid => UnsafeBindings.b3Joint_IsValid(Id);

        public static implicit operator Joint(DistanceJoint joint)
        {
            return new Joint { Id = joint.Id };
        }

        public bool Equals(DistanceJoint other)
        {
            return Id.Equals(other.Id);
        }

        public override bool Equals(object obj)
        {
            return obj is DistanceJoint other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public static bool operator ==(DistanceJoint left, DistanceJoint right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(DistanceJoint left, DistanceJoint right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>Motor joint: drives a body toward a target transform with force/torque limits.
    /// Box3D's replacement for a mouse/drag joint.</summary>
    public partial struct MotorJoint : IEquatable<MotorJoint>
    {
        public JointId Id;

        public bool IsValid => UnsafeBindings.b3Joint_IsValid(Id);

        public static implicit operator Joint(MotorJoint joint)
        {
            return new Joint { Id = joint.Id };
        }

        public bool Equals(MotorJoint other)
        {
            return Id.Equals(other.Id);
        }

        public override bool Equals(object obj)
        {
            return obj is MotorJoint other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public static bool operator ==(MotorJoint left, MotorJoint right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MotorJoint left, MotorJoint right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>Parallel joint: keeps two body frames rotationally aligned.</summary>
    public partial struct ParallelJoint : IEquatable<ParallelJoint>
    {
        public JointId Id;

        public bool IsValid => UnsafeBindings.b3Joint_IsValid(Id);

        public static implicit operator Joint(ParallelJoint joint)
        {
            return new Joint { Id = joint.Id };
        }

        public bool Equals(ParallelJoint other)
        {
            return Id.Equals(other.Id);
        }

        public override bool Equals(object obj)
        {
            return obj is ParallelJoint other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public static bool operator ==(ParallelJoint left, ParallelJoint right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ParallelJoint left, ParallelJoint right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>Prismatic joint: translation along one axis, rotation locked.</summary>
    public partial struct PrismaticJoint : IEquatable<PrismaticJoint>
    {
        public JointId Id;

        public bool IsValid => UnsafeBindings.b3Joint_IsValid(Id);

        public static implicit operator Joint(PrismaticJoint joint)
        {
            return new Joint { Id = joint.Id };
        }

        public bool Equals(PrismaticJoint other)
        {
            return Id.Equals(other.Id);
        }

        public override bool Equals(object obj)
        {
            return obj is PrismaticJoint other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public static bool operator ==(PrismaticJoint left, PrismaticJoint right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PrismaticJoint left, PrismaticJoint right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>Revolute joint: rotation around one axis (hinge).</summary>
    public partial struct RevoluteJoint : IEquatable<RevoluteJoint>
    {
        public JointId Id;

        public bool IsValid => UnsafeBindings.b3Joint_IsValid(Id);

        public static implicit operator Joint(RevoluteJoint joint)
        {
            return new Joint { Id = joint.Id };
        }

        public bool Equals(RevoluteJoint other)
        {
            return Id.Equals(other.Id);
        }

        public override bool Equals(object obj)
        {
            return obj is RevoluteJoint other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public static bool operator ==(RevoluteJoint left, RevoluteJoint right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RevoluteJoint left, RevoluteJoint right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>Spherical joint: ball-and-socket with cone/twist limits. The ragdoll joint.</summary>
    public partial struct SphericalJoint : IEquatable<SphericalJoint>
    {
        public JointId Id;

        public bool IsValid => UnsafeBindings.b3Joint_IsValid(Id);

        public static implicit operator Joint(SphericalJoint joint)
        {
            return new Joint { Id = joint.Id };
        }

        public bool Equals(SphericalJoint other)
        {
            return Id.Equals(other.Id);
        }

        public override bool Equals(object obj)
        {
            return obj is SphericalJoint other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public static bool operator ==(SphericalJoint left, SphericalJoint right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SphericalJoint left, SphericalJoint right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>Weld joint: rigidly connects two bodies, optionally springy.</summary>
    public partial struct WeldJoint : IEquatable<WeldJoint>
    {
        public JointId Id;

        public bool IsValid => UnsafeBindings.b3Joint_IsValid(Id);

        public static implicit operator Joint(WeldJoint joint)
        {
            return new Joint { Id = joint.Id };
        }

        public bool Equals(WeldJoint other)
        {
            return Id.Equals(other.Id);
        }

        public override bool Equals(object obj)
        {
            return obj is WeldJoint other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public static bool operator ==(WeldJoint left, WeldJoint right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(WeldJoint left, WeldJoint right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>Wheel joint: suspension spring along one axis plus drive motor around another.</summary>
    public partial struct WheelJoint : IEquatable<WheelJoint>
    {
        public JointId Id;

        public bool IsValid => UnsafeBindings.b3Joint_IsValid(Id);

        public static implicit operator Joint(WheelJoint joint)
        {
            return new Joint { Id = joint.Id };
        }

        public bool Equals(WheelJoint other)
        {
            return Id.Equals(other.Id);
        }

        public override bool Equals(object obj)
        {
            return obj is WheelJoint other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public static bool operator ==(WheelJoint left, WheelJoint right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(WheelJoint left, WheelJoint right)
        {
            return !left.Equals(right);
        }
    }
}
