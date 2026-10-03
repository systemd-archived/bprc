using UnityEngine;

namespace RCR2Port
{
    public sealed class RoverPortGravityBody : MonoBehaviour
    {
        private Rigidbody m_body;
        private float m_gravityScale = 1f;

        public void Configure(Rigidbody body, float gravityScale)
        {
            m_body = body;
            m_gravityScale = gravityScale;
        }

        private void FixedUpdate()
        {
            if (m_body == null)
            {
                m_body = GetComponent<Rigidbody>();
            }
            if (m_body == null || m_body.isKinematic || !m_body.useGravity)
            {
                return;
            }
            Vector3 correction = Physics.gravity * ((m_gravityScale - 1f) * m_body.mass);
            m_body.EngineAddForce(correction, ForceMode.Force);
        }
    }
}
