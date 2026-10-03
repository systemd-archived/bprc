using UnityEngine;

namespace RCR2Port
{
    /// <summary>
    /// Turns one node of a ported wheel's artwork the way the *source* turns it.
    ///
    /// A wheel in this project is never rotated by physics alone. MAIN's <c>MotorWheel</c> and
    /// <c>OffRoadWheel</c> accumulate <c>m_angle += -360 * m_spinSpeed / m_circumference * dt</c>
    /// and write <c>WheelPivot.localRotation = AngleAxis(-partZ + m_angle, forward)</c>;
    /// <c>AndroidBigfootMotion</c> writes <c>wheelPivot.localRotation = Euler(0, 0, wheelAngle)</c>
    /// with <c>wheelAngle -= v.x / wheelRadius * Rad2Deg * dt</c>; <c>AndroidTrackMotion</c> writes
    /// <c>wheel.localRotation = initialLocalRotation * AngleAxis(visualAngle, forward)</c> for every
    /// roller. The artwork therefore moves with the vehicle instead of standing still.
    ///
    /// A ported part cannot do that with <c>Rigidbody.angularVelocity</c>: an RC1 wheel's catalog
    /// carries no <c>bodies</c>, so its artwork hangs off the part root rigidbody, which the
    /// contraption welds to the chassis with a FixedJoint and which is frozen on Z - spinning it
    /// rotates nothing. (RCR2 wheels do have separate wheel bodies, and there a
    /// <see cref="RoverPortFollower"/> copies the body's rotation onto the artwork.) This component
    /// is the RC1 equivalent: one instance per catalog <see cref="RoverPortWheelPivotData"/>,
    /// driven by <c>RoverPortPoweredPart.DriveWheels</c> with the very same surface speed MAIN's
    /// raycast produces.
    ///
    /// Two of the three rules move their node as well as turning it, because the source gave
    /// that wheel a suspension. <c>AndroidBigfootMotion</c> springs the wheel axle along the
    /// connection direction on top of the roll (<c>travel</c> 0.2601626 / 0.203252, the numbers
    /// it serialises), and the catalog carries the same three numbers for every other Android
    /// wheel too, re-based from Android's own <c>PartSPrWheel.suspensionProperties</c> (travel =
    /// (top - bottom) * 0.05, MAIN's scale factor; 12 Hz and 0.82 damping, MAIN's authored pair).
    /// The node both rules move is Android's <c>wheel</c> transform - <c>PartSPrWheel</c>'s
    /// wheelTransform, the only part of the artwork the source turns - so the base plate
    /// (<c>L1_wheel_bg</c>), the bracket (<c>L1_wheel_e</c>) and the anchor stay bolted to the
    /// frame while the tyre rolls and gives. The belt rule only ever writes a rotation, and its
    /// <c>travel</c> is 0, so this component leaves those rollers' position alone.
    /// </summary>
    [DefaultExecutionOrder(150)]
    public sealed class RoverPortWheelSpin : MonoBehaviour
    {
        /// <summary>MAIN's MotorWheel/OffRoadWheel rule: counter-rotate the part, then the roll angle.</summary>
        public const string ModeSurface = "surface";
        /// <summary>MAIN's AndroidBigfootMotion rule: a plain local roll angle.</summary>
        public const string ModeVelocityX = "velocity_x";
        /// <summary>MAIN's AndroidTrackMotion rule: roll angle on top of the authored roller rotation.</summary>
        public const string ModeBelt = "belt";

        /// <summary>Native airborne coast-down: MotorWheel does <c>m_spinSpeed *= 0.98f</c> per step.</summary>
        private const float SpinDecayPerStep = 0.98f;

        // Serialized: a placed part is a clone of the catalog template
        // (ConstructionUI.SetPartAt -> Object.Instantiate), and only serialized fields come
        // over. A non-serialized reference would be null in the clone, which is exactly the
        // bug this component exists to avoid repeating.
        [SerializeField] private string m_node;
        [SerializeField] private float m_radius;
        [SerializeField] private string m_mode;
        [SerializeField] private string m_source;
        /// <summary>
        /// <c>AndroidBigfootMotion.travel</c>, or 0 for a pivot the source only ever rotates. A
        /// non-zero value is what makes this instance move its node as well as turn it.
        /// </summary>
        [SerializeField] private float m_travel;
        /// <summary><c>AndroidBigfootMotion.springFrequency</c>, in Hz.</summary>
        [SerializeField] private float m_springFrequency;
        /// <summary><c>AndroidBigfootMotion.damping</c>: a per-60Hz-frame factor, not a ratio.</summary>
        [SerializeField] private float m_damping = 1f;

        private RoverPortPart m_owner;
        private Transform m_pivot;
        private Quaternion m_baseRotation = Quaternion.identity;
        private Vector3 m_restLocalPosition;
        private Rigidbody m_body;
        private float m_springVelocity;
        private float m_angle;
        private float m_spinSpeed;

        /// <summary>Port node id this instance turns (catalog <c>wheel_pivots[].node</c>).</summary>
        public string Node { get { return m_node; } }

        /// <summary>Rolling radius the source divides the surface speed by.</summary>
        public float Radius { get { return m_radius; } }

        /// <summary>Which of MAIN's three rotation rules applies (catalog <c>wheel_pivots[].mode</c>).</summary>
        public string Mode { get { return m_mode; } }

        /// <summary>Component in MAIN that owns this rotation, for provenance/diagnostics.</summary>
        public string Source { get { return m_source; } }

        /// <summary>Accumulated roll angle in degrees; the diagnostics read it.</summary>
        public float Angle { get { return m_angle; } }

        /// <summary>Surface speed used by the last step, in m/s; the diagnostics read it.</summary>
        public float SpinSpeed { get { return m_spinSpeed; } }

        /// <summary>Sprung travel of this pivot, in metres; 0 when the source only rotates it.</summary>
        public float Travel { get { return m_travel; } }

        /// <summary>Spring frequency of the sprung axle, in Hz; 0 when there is none.</summary>
        public float SpringFrequency { get { return m_springFrequency; } }

        /// <summary>Current offset of the sprung axle from its resting Y, in metres.</summary>
        public float TravelOffset
        {
            get { return m_pivot == null ? 0f : m_pivot.localPosition.y - m_restLocalPosition.y; }
        }

        /// <summary>The artwork node this instance writes, or null if it could not be resolved.</summary>
        public Transform Pivot
        {
            get
            {
                Resolve();
                return m_pivot;
            }
        }

        public void Configure(string node, float radius, string mode, string source,
            float travel = 0f, float springFrequency = 0f, float damping = 1f)
        {
            m_node = node;
            m_radius = radius;
            m_mode = mode;
            m_source = source;
            m_travel = travel;
            m_springFrequency = springFrequency;
            m_damping = damping > 0f ? damping : 1f;
            m_pivot = null;
            m_owner = null;
            m_body = null;
            Resolve();
        }

        private void Awake()
        {
            Resolve();
        }

        private void FixedUpdate()
        {
            Resolve();
            if (m_owner != null && m_owner.PortData.motion.passive &&
                m_owner.contraption != null && m_owner.contraption.IsRunning)
            {
                if (m_body == null) m_body = m_owner.GetComponent<Rigidbody>();
                if (m_body != null) Advance(Vector3.Dot(m_body.velocity, m_owner.transform.right), false, Time.fixedDeltaTime);
            }
        }

        /// <summary>
        /// Re-resolves the node on every clone, the way <c>RoverPortParticleDriver</c> re-resolves
        /// its owner: the id is serialized, the transform is not. <c>RoverPortPart</c> keeps its
        /// node table in serialized <c>RoverPortNodeRef</c> entries, whose Transform references
        /// Unity remaps onto the clone, so the lookup works after instantiation.
        /// </summary>
        private void Resolve()
        {
            if (m_pivot != null || string.IsNullOrEmpty(m_node))
            {
                return;
            }
            if (m_owner == null)
            {
                m_owner = GetComponentInParent<RoverPortPart>();
            }
            if (m_owner == null)
            {
                return;
            }
            m_pivot = m_owner.ResolvePortNode(m_node);
            if (m_pivot != null)
            {
                m_baseRotation = m_pivot.localRotation;
                m_restLocalPosition = m_pivot.localPosition;
            }
            m_body = m_owner.GetComponent<Rigidbody>();
        }

        /// <summary>
        /// Legacy visual suspension for records without a separate physical axle.
        /// RC1WheelSuspension owns both the contact axle and its artwork displacement when present.
        /// </summary>
        private void LateUpdate()
        {
            if (m_owner != null && m_owner.GetComponent<RC1Port.RC1WheelSuspension>() != null)
                return;
            if (m_travel <= 0f || m_pivot == null)
            {
                return;
            }
            if (m_body == null)
            {
                // Resolve() short-circuits once the pivot is known, and on a placed clone the
                // rigidbody may not exist yet when Configure runs, so re-look-up the body here
                // rather than through Resolve().
                if (m_owner == null)
                {
                    m_owner = GetComponentInParent<RoverPortPart>();
                }
                if (m_owner == null)
                {
                    return;
                }
                m_body = m_owner.GetComponent<Rigidbody>();
                if (m_body == null)
                {
                    return;
                }
            }
            float step = Time.deltaTime;
            float target = Mathf.Clamp(-m_body.velocity.y * 0.018f, -m_travel, m_travel);
            float stiffness = m_springFrequency * m_springFrequency;
            float current = m_pivot.localPosition.y - m_restLocalPosition.y;
            m_springVelocity += (target - current) * stiffness * step;
            m_springVelocity *= Mathf.Pow(m_damping, step * 60f);
            float offset = Mathf.Clamp(current + m_springVelocity * step, -m_travel, m_travel);
            Vector3 position = m_restLocalPosition;
            position.y += offset;
            m_pivot.localPosition = position;
        }

        /// <summary>
        /// Advances the roll by one drive step with the surface speed MAIN's wheel components use.
        /// <paramref name="airborne"/> selects the native coast-down: a grounded wheel always takes
        /// the current contact speed (MAIN re-reads it every step), an airborne one keeps its rate
        /// while the motor runs and decays by 2% per step while it does not.
        /// </summary>
        public void Advance(float surfaceSpeed, bool airborne, float deltaTime)
        {
            Resolve();
            m_spinSpeed = airborne && Mathf.Abs(surfaceSpeed) <= 0.0001f
                ? m_spinSpeed * SpinDecayPerStep
                : surfaceSpeed;
            // MAIN's own expression: m_circumference = 2 * PI * m_radius.
            float circumference = 2f * Mathf.PI * Mathf.Max(0.01f, m_radius);
            m_angle += -360f * m_spinSpeed / circumference * deltaTime;
            ApplyRotation();
        }

        /// <summary>Writes the accumulated angle with the rule MAIN's owning component used.</summary>
        public void ApplyRotation()
        {
            if (m_pivot == null)
            {
                return;
            }
            if (m_owner != null && !string.IsNullOrEmpty(m_owner.PortData.android_class))
            {
                Vector3 axis = m_pivot.parent.InverseTransformDirection(Vector3.forward);
                m_pivot.localRotation = Quaternion.AngleAxis(m_angle, axis) * m_baseRotation;
                return;
            }
            switch (m_mode)
            {
                case ModeVelocityX:
                    m_pivot.localRotation = Quaternion.Euler(0f, 0f, m_angle);
                    break;
                case ModeBelt:
                    m_pivot.localRotation = m_baseRotation * Quaternion.AngleAxis(m_angle, Vector3.forward);
                    break;
                default:
                    float partZ = m_owner != null ? m_owner.transform.rotation.eulerAngles.z : 0f;
                    m_pivot.localRotation = Quaternion.AngleAxis(0f - partZ + m_angle, Vector3.forward);
                    break;
            }
        }
    }
}
