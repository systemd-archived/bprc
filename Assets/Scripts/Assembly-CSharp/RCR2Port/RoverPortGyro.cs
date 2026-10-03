using UnityEngine;

namespace RCR2Port
{
    /// <summary>
    /// The RC1 gyroscope: RoverCraft's <c>PartSPhGyro</c>, which spins the whole rover while one of the
    /// two pedals is held.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The source is driven by <c>CarController</c>'s forward and back commands and does one thing to
    /// each (<c>PartSPhGyro.SpecialForwardAction</c> / <c>SpecialBackAction</c>, read out of
    /// libil2cpp.so with Tools/RC1/disasm.py):
    /// </para>
    /// <code>
    /// if (rigidbody.angularVelocity &lt;  max) rigidbody.AddTorque( power);   // forward pedal
    /// if (rigidbody.angularVelocity &gt; -max) rigidbody.AddTorque(-power);   // back pedal
    /// </code>
    /// <para>
    /// so the pedals are a spin control with an angular-velocity ceiling, not a force. The ceiling and
    /// the torque are the part's own serialized <c>PartSPrGyro.properties</c>
    /// (<c>max = 220</c>, <c>power = 90</c>) - read from the APK's prefab - and they are the same
    /// numbers this component authors, with one conversion: the source is a <c>Rigidbody2D</c>, whose
    /// <c>angularVelocity</c> is degrees per second, while the port drives the project's 3D bodies,
    /// whose <c>angularVelocity</c> is radians per second. Both numbers are also exposed as per-part
    /// parameters, so the long-press panel can retune them without a rebuild.
    /// </para>
    /// <para>
    /// The gyro used to come out of the catalog as a <c>RoverPortPoweredPart</c> on the stock Engine
    /// driver, which made it a second engine that pushed the rover along instead of turning it. It is a
    /// plain part now and only reacts to the pedals.
    /// </para>
    /// </remarks>
    public class RoverPortGyro : MonoBehaviour
    {
        /// <summary>The source's <c>PartSPrGyro.properties.max</c>, in degrees per second.</summary>
        public const float SourceMaxAngularVelocity = 220f;

        /// <summary>The source's <c>PartSPrGyro.properties.power</c>, the torque it adds per step.</summary>
        public const float SourceTorque = 90f;

        /// <summary>The ceiling, in the radians per second the port's bodies speak.</summary>
        public float MaxAngularVelocity { get; set; } = SourceMaxAngularVelocity * Mathf.Deg2Rad;

        /// <summary>The torque added each physics step while a pedal is held.</summary>
        public float Torque { get; set; } = SourceTorque;

        private RoverPortPart m_part;
        private Rigidbody m_body;

        private void FixedUpdate()
        {
            // RoverCraft only feeds these parts while its controls are live; the port's panel is the
            // same gate, so with 「RC1 操作」 off the gyro is completely inert.
            if (!INRc1Controls.Active)
            {
                m_spin = 0f;
                return;
            }
            float axis = INRc1Controls.DriveAxis;
            if (axis == 0f)
            {
                m_spin = 0f;
                return;
            }
            if (m_part == null) m_part = GetComponent<RoverPortPart>();
            if (m_part == null || m_part.contraption == null || !m_part.contraption.IsRunning)
            {
                return;
            }
            if (m_body == null) m_body = GetComponent<Rigidbody>();
            if (m_body == null) return;

            float spin = m_body.angularVelocity.z;
            if (axis > 0f)
            {
                if (spin < MaxAngularVelocity)
                {
                    m_body.EngineAddTorque(0f, 0f, Torque);
                }
            }
            else if (spin > -MaxAngularVelocity)
            {
                m_body.EngineAddTorque(0f, 0f, -Torque);
            }
            // Remembered only so the part parameter panel can show which way it is being driven.
            m_spin = axis;
        }

        private float m_spin;

        /// <summary>+1 while the gyro is being driven forwards, -1 backwards, 0 otherwise.</summary>
        public float SpinDirection => m_spin;
    }
}
