using RCR2Port;
using UnityEngine;

namespace RC1Port
{
    [DefaultExecutionOrder(150)]
    public sealed class RC1WheelSuspension : MonoBehaviour
    {
        private Rigidbody m_axle;
        private Transform m_visual;
        private Vector3 m_visualRest;
        private Vector3 m_axleRest;
        private Transform[] m_followers;
        private Vector3[] m_followerRest;
        private SphereCollider m_contact;
        private float m_radius;

        public Rigidbody Axle => m_axle;

        public static void RefreshGeometry(RoverPortPart part)
        {
            var data = part.PortData;
            if (data == null || data.motion == null || data.motion.kind != "wheel" ||
                string.IsNullOrEmpty(data.android_class) || data.motion.external_physics_motion ||
                data.android_class == "AndroidSki" || data.motion.wheel_pivots == null)
                return;
            foreach (var pivot in data.motion.wheel_pivots)
            {
                if (pivot == null || string.IsNullOrEmpty(pivot.node) || !pivot.node.StartsWith("PE:") || pivot.radius <= 0f)
                    continue;
                Transform node = part.ResolvePortNode(pivot.node);
                SphereCollider tyre = node != null ? node.GetComponent<SphereCollider>() : null;
                if (tyre != null) SetContactRadius(part.transform, tyre, pivot.radius);
            }
            var suspension = part.GetComponent<RC1WheelSuspension>();
            if (suspension != null && suspension.m_contact != null)
                SetContactRadius(part.transform, suspension.m_contact, suspension.m_radius);
        }

        private static void SetContactRadius(Transform root, SphereCollider contact, float radius)
        {
            // Android's radius is already in part XY units. PhysX also scales spheres
            // by Z, which otherwise inflates tyres whose source artwork only scales XY.
            float planarScale = Mathf.Max(root.TransformVector(Vector3.right).magnitude,
                root.TransformVector(Vector3.up).magnitude);
            Vector3 scale = contact.transform.lossyScale;
            float sphereScale = Mathf.Max(0.001f, Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            contact.radius = radius * planarScale / sphereScale;
        }

        public static void Attach(RoverPortPart part)
        {
            var data = part.PortData;
            if (data == null || data.motion == null || data.motion.kind != "wheel" ||
                string.IsNullOrEmpty(data.android_class) || data.motion.external_physics_motion ||
                data.android_class == "AndroidSki" || part.GetComponent<RC1WheelSuspension>() != null)
                return;
            SphereCollider tyre = part.GetComponentInChildren<SphereCollider>(true);
            if (tyre == null) return;
            RoverPortWheelPivotData pivot = null;
            foreach (var candidate in data.motion.wheel_pivots ?? new RoverPortWheelPivotData[0])
                if (candidate != null && !string.IsNullOrEmpty(candidate.node) && candidate.node.StartsWith("PE:")) { pivot = candidate; break; }
            if (pivot == null) return;
            Transform visual = part.ResolvePortNode(pivot.node);
            if (visual == null) return;

            var suspension = part.gameObject.AddComponent<RC1WheelSuspension>();
            suspension.m_visual = visual;
            suspension.m_visualRest = suspension.m_visual.localPosition;
            var followers = new System.Collections.Generic.List<Transform>();
            foreach (string node in data.motion.suspension_followers ?? new string[0])
            {
                Transform follower = part.ResolvePortNode(node);
                if (follower != null && !follower.IsChildOf(visual)) followers.Add(follower);
            }
            suspension.m_followers = followers.ToArray();
            suspension.m_followerRest = new Vector3[followers.Count];
            for (int i = 0; i < followers.Count; i++)
                suspension.m_followerRest[i] = followers[i].localPosition;
            var axleObject = new GameObject("RC1 Axle");
            axleObject.layer = part.gameObject.layer;
            axleObject.transform.SetParent(part.transform, false);
            axleObject.transform.position = tyre.transform.TransformPoint(tyre.center);
            suspension.m_axleRest = axleObject.transform.localPosition;
            var contact = axleObject.AddComponent<SphereCollider>();
            suspension.m_contact = contact;
            suspension.m_radius = pivot.radius;
            SetContactRadius(part.transform, contact, pivot.radius);
            contact.sharedMaterial = tyre.sharedMaterial;
            tyre.enabled = false;

            Rigidbody carrier = part.GetComponent<Rigidbody>();
            Rigidbody axle = axleObject.AddComponent<Rigidbody>();
            suspension.m_axle = axle;
            float totalMass = carrier.mass;
            axle.mass = totalMass * 0.25f;
            carrier.mass = totalMass * 0.75f;
            if (part.gameObject.activeInHierarchy) suspension.AlignCarrierInertia();
            // The joint locks rotation; duplicating those locks on the body makes PhysX singular.
            axle.constraints = RigidbodyConstraints.FreezePositionZ;
            axle.interpolation = RigidbodyInterpolation.Interpolate;
            axle.angularDrag = 0.05f;

            var joint = axleObject.AddComponent<ConfigurableJoint>();
            joint.connectedBody = carrier;
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = Vector3.zero;
            joint.connectedAnchor = carrier.transform.InverseTransformPoint(axle.transform.position);
            // Axis belongs to the bracket, so part rotation rotates the suspension with its mount.
            joint.axis = Vector3.up;
            joint.secondaryAxis = Vector3.right;
            joint.xMotion = ConfigurableJointMotion.Limited;
            joint.yMotion = joint.zMotion = ConfigurableJointMotion.Locked;
            joint.angularXMotion = joint.angularYMotion = joint.angularZMotion = ConfigurableJointMotion.Locked;
            float travel = data.motion.suspension_travel > 0f ? data.motion.suspension_travel : Mathf.Max(0.08f, pivot.travel);
            joint.linearLimit = new SoftJointLimit { limit = travel };
            float frequency = data.motion.connection_frequency > 0f ? data.motion.connection_frequency : 4.4f;
            float omega = 2f * Mathf.PI * frequency;
            float reducedMass = axle.mass * carrier.mass / totalMass;
            joint.xDrive = new JointDrive {
                positionSpring = reducedMass * omega * omega,
                positionDamper = 2f * Mathf.Max(0.6f, data.motion.connection_damping_ratio) * reducedMass * omega,
                maximumForce = float.MaxValue
            };
            joint.enableCollision = false;
            joint.projectionMode = JointProjectionMode.PositionAndRotation;
            joint.projectionDistance = travel + 0.05f;
            joint.projectionAngle = 5f;
        }

        private void OnEnable()
        {
            if (m_axle != null) AlignCarrierInertia();
        }

        private void AlignCarrierInertia()
        {
            // Asymmetric bracket colliders tilt the principal inertia axes. Unity freezes
            // rotation in that frame, so align its free axis with the original 2D axle.
            // Recompute after activation, when PhysX has the actual bracket geometry.
            Rigidbody carrier = GetComponent<Rigidbody>();
            var constraints = carrier.constraints;
            carrier.constraints = RigidbodyConstraints.None;
            carrier.ResetInertiaTensor();
            Vector3 moments = carrier.inertiaTensor;
            Vector3 zInFrame = Quaternion.Inverse(carrier.inertiaTensorRotation) * Vector3.forward;
            float planarInertia = Vector3.Dot(Vector3.Scale(zInFrame, zInFrame), moments);
            carrier.inertiaTensorRotation = Quaternion.identity;
            carrier.inertiaTensor = new Vector3(moments.x, moments.y, Mathf.Max(0.0001f, planarInertia));
            carrier.constraints = constraints;
        }

        private void LateUpdate()
        {
            if (m_axle == null || m_visual == null) return;
            // Both poses must be from the rendered frame. Rigidbody.position is the last
            // physics tick; mixing it with an interpolated carrier makes the tyre drift/snap.
            Vector3 displacement = m_axle.transform.position - transform.TransformPoint(m_axleRest);
            m_visual.localPosition = m_visualRest + m_visual.parent.InverseTransformVector(displacement);
            // The APK hub is a sibling of the spinning tyre. Copy suspension translation
            // only, retaining its authored offset, depth and non-spinning orientation.
            for (int i = 0; i < m_followers.Length; i++)
                if (m_followers[i] != null)
                    m_followers[i].localPosition = m_followerRest[i] + m_followers[i].parent.InverseTransformVector(displacement);
        }
    }
}
