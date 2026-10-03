using UnityEngine;

namespace RCR2Port
{
    /// <summary>
    /// Binds one wheel sprite to the rigidbody node that drives it, the way the
    /// original TransformFollower does. The artwork keeps its authored place in
    /// the hierarchy (the mini stick carries it) but takes the roller body's
    /// rotation, so the mecanum's eight small rollers visibly spin with the
    /// physics instead of standing still. The rest relationship between sprite
    /// and body is captured once and kept as a delta, so the authored pose is
    /// reproduced exactly and only the motion is copied.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class RoverPortFollower : MonoBehaviour
    {
        private Transform m_target;
        private bool m_followPosition;
        private bool m_followRotation;
        private Quaternion m_restRotation = Quaternion.identity;
        private Quaternion m_restTargetRotation = Quaternion.identity;
        private Vector3 m_restOffset;
        private bool m_captured;

        public Transform Target
        {
            get { return m_target; }
        }

        public bool FollowsPosition
        {
            get { return m_followPosition; }
        }

        public bool FollowsRotation
        {
            get { return m_followRotation; }
        }

        public void Configure(Transform target, bool followPosition, bool followRotation)
        {
            bool changed = m_target != target;
            m_target = target;
            m_followPosition = followPosition;
            m_followRotation = followRotation;
            if (changed)
            {
                m_captured = false;
                Capture();
            }
        }

        private void Awake()
        {
            Capture();
        }

        private void Capture()
        {
            if (m_captured || m_target == null)
            {
                return;
            }
            m_captured = true;
            m_restRotation = transform.rotation;
            m_restTargetRotation = m_target.rotation;
            m_restOffset = Quaternion.Inverse(m_target.rotation) * (transform.position - m_target.position);
        }

        private void LateUpdate()
        {
            Refresh();
        }

        /// <summary>Copies the target motion; also used by the audits.</summary>
        public void Refresh()
        {
            if (m_target == null)
            {
                return;
            }
            if (!m_captured)
            {
                Capture();
            }
            if (m_followRotation)
            {
                transform.rotation = m_target.rotation * (Quaternion.Inverse(m_restTargetRotation) * m_restRotation);
            }
            if (m_followPosition)
            {
                transform.position = m_target.position + m_target.rotation * m_restOffset;
            }
        }
    }

    /// <summary>
    /// Rotates one sprite so it keeps pointing from its hub mount at its roller,
    /// the way the original SpriteRotatorBy2Points shows the mecanum's rolling
    /// arms. The authored orientation is captured with the mount-to-roller
    /// direction and only the rotation delta is applied, so any axis convention
    /// in the sprite survives.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class RoverPortStickRotator : MonoBehaviour
    {
        private Transform m_pointA;
        private Transform m_pointB;
        private Quaternion m_restRotation = Quaternion.identity;
        private Vector3 m_restDirection;
        private bool m_captured;

        public Transform PointA
        {
            get { return m_pointA; }
        }

        public Transform PointB
        {
            get { return m_pointB; }
        }

        public void Configure(Transform pointA, Transform pointB)
        {
            bool changed = m_pointA != pointA || m_pointB != pointB;
            m_pointA = pointA;
            m_pointB = pointB;
            if (changed)
            {
                m_captured = false;
                Capture();
            }
        }

        private void Awake()
        {
            Capture();
        }

        private void Capture()
        {
            if (m_captured || m_pointA == null || m_pointB == null)
            {
                return;
            }
            m_captured = true;
            m_restRotation = transform.rotation;
            m_restDirection = m_pointB.position - m_pointA.position;
        }

        private void LateUpdate()
        {
            Refresh();
        }

        /// <summary>Applies the current direction; also used by the audits.</summary>
        public void Refresh()
        {
            if (m_pointA == null || m_pointB == null)
            {
                return;
            }
            if (!m_captured)
            {
                Capture();
            }
            Vector3 direction = m_pointB.position - m_pointA.position;
            if (direction.sqrMagnitude < 0.00000001f || m_restDirection.sqrMagnitude < 0.00000001f)
            {
                return;
            }
            transform.rotation = Quaternion.FromToRotation(m_restDirection, direction) * m_restRotation;
        }
    }
}
