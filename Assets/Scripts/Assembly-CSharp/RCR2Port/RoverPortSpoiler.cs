using UnityEngine;

namespace RCR2Port
{
    /// <summary>
    /// Reproduces RC1.apk's PartSPhSpoiler.FixedUpdate (RVA 0xAE7604).
    /// Horizontal motion gives the full lift coefficient; vertical motion gives no downforce.
    /// The original transforms its force through AddRelativeForce, including the part's rotation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoverPortSpoiler : MonoBehaviour
    {
        /// <summary>MAIN's own <c>AndroidLiftCoefficient</c>, the divisor that keeps the Android upgrade scaling.</summary>
        public const float AndroidLiftCoefficient = 30f;

        [SerializeField] private RoverPortSpoilerData m_data;

        private RoverPortPart m_owner;
        private Rigidbody m_body;

        /// <summary>Downforce applied by the last fixed step, in newtons; 0 while the vehicle is idle. The diagnostics read it.</summary>
        public float LastDownforce { get; private set; }

        /// <summary>
        /// Adds the component to a ported part, but only when the part really is the imported
        /// spoiler (its record carries a <c>spoiler</c> block). Called from
        /// <see cref="RoverPortPart.EnsureRigidbody"/> on every ported part.
        /// </summary>
        public static void Attach(RoverPortPart part, RoverPortPartData data)
        {
            if (part == null || data == null || data.spoiler == null)
            {
                return;
            }
            RoverPortSpoiler spoiler = part.GetComponent<RoverPortSpoiler>();
            if (spoiler == null)
            {
                spoiler = part.gameObject.AddComponent<RoverPortSpoiler>();
            }
            spoiler.Configure(part, data.spoiler);
        }

        private void Configure(RoverPortPart owner, RoverPortSpoilerData data)
        {
            m_owner = owner;
            m_data = data;
        }

        private void FixedUpdate()
        {
            if (m_data == null)
            {
                return;
            }
            // A placed part is a clone of the catalog template, and a component reference is not
            // serialized: re-resolve the same way RoverPortWheelSpin does.
            if (m_owner == null)
            {
                m_owner = GetComponent<RoverPortPart>();
                m_body = null;
                if (m_owner == null)
                {
                    return;
                }
            }
            Contraption owner = m_owner.contraption;
            if (owner == null || !owner.IsRunning)
            {
                LastDownforce = 0f;
                return;
            }
            if (m_body == null)
            {
                m_body = m_owner.GetComponent<Rigidbody>();
                if (m_body == null)
                {
                    return;
                }
            }
            // "Same wind bookkeeping as Wings/Tail: the aerodynamic force acts on the airspeed
            // relative to the wind, and the accumulated wind velocity is consumed here."
            Vector3 velocity = m_body.velocity - m_owner.WindVelocity;
            m_owner.WindVelocity = Vector3.zero;
            // PartSPhSpoiler.FixedUpdate in RC1.apk (RVA 0xAE7604).
            float angle = Vector3.Angle(Vector3.up, velocity);
            float alignment = 1f - 2f * Mathf.Abs(angle / 180f - 0.5f);
            Vector3 localForce = -m_body.transform.up * (alignment * m_data.lift_coefficient);
            Vector3 force = m_body.transform.TransformDirection(localForce);
            m_body.EngineAddForce(force, ForceMode.Force);
            LastDownforce = force.magnitude;
        }
    }
}
