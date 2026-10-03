using System.Collections.Generic;
using UnityEngine;

namespace RCR2Port
{
    /// <summary>
    /// Rebuilds the physical roller assembly owned by MAIN's AndroidTrackMotion.
    /// The catalog's track artwork remains in the normal node tree; each physics
    /// roller gets a small axle body so the belt can follow suspension instead of
    /// being a single rigid compound collider.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoverPortTrackMotion : MonoBehaviour
    {
        private sealed class Roller
        {
            public RoverPortTrackRollerData data;
            public Transform visual;
            public Rigidbody body;
        }

        private RoverPortPart m_owner;
        private readonly List<Roller> m_rollers = new List<Roller>();
        private bool m_initialized;

        public bool PhysicsInitialized { get { return m_initialized; } }
        public int RollerCount { get { return m_rollers.Count; } }

        private void FixedUpdate()
        {
            if (!m_initialized || m_owner == null) return;
            Rigidbody chassis = m_owner.GetComponent<Rigidbody>();
            if (chassis == null) return;
            float speed = Vector3.Dot(chassis.velocity, m_owner.transform.right);
            for (int i = 0; i < m_rollers.Count; i++)
            {
                RoverPortWheelSpin spin = m_rollers[i].visual.GetComponent<RoverPortWheelSpin>();
                if (spin != null)
                {
                    spin.Advance(speed, false, Time.fixedDeltaTime);
                }
            }
        }

        public static void Attach(RoverPortPart owner, RoverPortPartData data)
        {
            if (owner == null || data == null || data.motion == null ||
                !data.motion.external_physics_motion || data.motion.track_rollers == null ||
                data.motion.track_rollers.Length == 0)
            {
                return;
            }
            RoverPortTrackMotion motion = owner.GetComponent<RoverPortTrackMotion>();
            if (motion == null)
            {
                motion = owner.gameObject.AddComponent<RoverPortTrackMotion>();
            }
            motion.m_owner = owner;
            motion.Initialize(data.motion.track_rollers);
        }

        private void Initialize(RoverPortTrackRollerData[] records)
        {
            if (m_initialized || m_owner == null || records == null)
            {
                return;
            }
            Rigidbody chassis = m_owner.GetComponent<Rigidbody>();
            if (chassis == null)
            {
                return;
            }

            m_rollers.Clear();
            for (int i = 0; i < records.Length; i++)
            {
                RoverPortTrackRollerData record = records[i];
                if (record == null || string.IsNullOrEmpty(record.node) || record.radius <= 0f)
                {
                    continue;
                }
                Transform visual = m_owner.ResolvePortNode(record.node);
                if (visual == null)
                {
                    continue;
                }
                GameObject axle = new GameObject(visual.name + "_Axle");
                axle.layer = m_owner.gameObject.layer;
                axle.transform.SetPositionAndRotation(visual.position, m_owner.transform.rotation);
                axle.transform.SetParent(m_owner.transform, true);
                Rigidbody body = axle.AddComponent<Rigidbody>();
                body.mass = Mathf.Max(0.01f, record.mass);
                body.drag = Mathf.Max(0f, record.linear_drag);
                body.angularDrag = Mathf.Max(0f, record.angular_drag);
                body.useGravity = true;
                body.constraints = RigidbodyConstraints.FreezePositionZ |
                    RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.maxDepenetrationVelocity = 2.5f;
                body.velocity = chassis.GetPointVelocity(axle.transform.position);
                SphereCollider collider = axle.AddComponent<SphereCollider>();
                collider.radius = record.radius;

                // The source component carries the artwork on the roller body.
                visual.SetParent(axle.transform, true);
                Roller roller = new Roller { data = record, visual = visual, body = body };
                m_rollers.Add(roller);
                AddSuspensionJoint(chassis, roller);
            }

            IgnoreInternalContacts(chassis);
            m_initialized = m_rollers.Count > 0;
        }

        private void AddSuspensionJoint(Rigidbody chassis, Roller roller)
        {
            Vector3 mount = ToVector3(roller.data.mount);
            if (roller.data.frequency <= 0f)
            {
                HingeJoint hinge = gameObject.AddComponent<HingeJoint>();
                hinge.connectedBody = roller.body;
                hinge.autoConfigureConnectedAnchor = false;
                hinge.anchor = mount;
                hinge.connectedAnchor = Vector3.zero;
                hinge.axis = Vector3.forward;
                hinge.enableCollision = false;
                hinge.breakForce = float.PositiveInfinity;
                hinge.breakTorque = float.PositiveInfinity;
                return;
            }

            ConfigurableJoint joint = gameObject.AddComponent<ConfigurableJoint>();
            joint.connectedBody = roller.body;
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = mount;
            joint.connectedAnchor = Vector3.zero;
            joint.axis = Vector3.right;
            joint.secondaryAxis = Vector3.up;
            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Limited;
            joint.zMotion = ConfigurableJointMotion.Locked;
            joint.angularXMotion = ConfigurableJointMotion.Locked;
            joint.angularYMotion = ConfigurableJointMotion.Locked;
            joint.angularZMotion = ConfigurableJointMotion.Free;
            joint.linearLimit = new SoftJointLimit { limit = 0.35f, bounciness = 0f };
            float reducedMass = chassis.mass * roller.body.mass /
                Mathf.Max(0.01f, chassis.mass + roller.body.mass);
            float omega = 2f * Mathf.PI * roller.data.frequency;
            float spring = reducedMass * omega * omega;
            float damper = 2f * Mathf.Max(0f, roller.data.damping_ratio) * reducedMass * omega;
            joint.yDrive = new JointDrive {
                positionSpring = spring,
                positionDamper = damper,
                maximumForce = float.MaxValue
            };
            joint.linearLimitSpring = new SoftJointLimitSpring { spring = spring * 4f, damper = damper * 4f };
            joint.projectionMode = JointProjectionMode.PositionAndRotation;
            joint.projectionDistance = 0.08f;
            joint.projectionAngle = 10f;
            joint.enableCollision = false;
        }

        private void IgnoreInternalContacts(Rigidbody chassis)
        {
            Collider[] chassisColliders = m_owner.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < m_rollers.Count; i++)
            {
                Collider rollerCollider = m_rollers[i].body.GetComponent<Collider>();
                if (rollerCollider == null) continue;
                for (int j = 0; j < chassisColliders.Length; j++)
                {
                    Collider other = chassisColliders[j];
                    if (other != null && other != rollerCollider)
                    {
                        Physics.IgnoreCollision(rollerCollider, other, true);
                    }
                }
                for (int j = i + 1; j < m_rollers.Count; j++)
                {
                    Collider other = m_rollers[j].body.GetComponent<Collider>();
                    if (other != null) Physics.IgnoreCollision(rollerCollider, other, true);
                }
            }
        }

        private static Vector3 ToVector3(float[] value)
        {
            if (value == null || value.Length < 2) return Vector3.zero;
            return new Vector3(value[0], value[1], value.Length > 2 ? value[2] : 0f);
        }

        private void OnDestroy()
        {
            // Axles are children of the owner and are collected with the part.
            m_rollers.Clear();
        }
    }
}
