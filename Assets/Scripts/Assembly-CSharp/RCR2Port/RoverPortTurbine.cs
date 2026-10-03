using System.Collections.Generic;
using UnityEngine;

namespace RCR2Port
{
    /// <summary>
    /// Rotating turbine part (part_ice_gyroturbine / 旋转涡轮, and the other turbines that ship the
    /// same rig).
    ///
    /// RCR2 animates this part with a small state machine of its own - RoverPartTurboSpecialPhysics
    /// plus the Animator "Part_Ice_GyroTurbine" with its Idle / Impulse / Shake / ShakeOff clips:
    ///
    ///  * while the engine thrusts, the rotor child spins and the nozzle is switched on with an
    ///    impulse: the SonicFX sprite shoots up to an overshoot scale and then keeps pulsing while it
    ///    runs (the "nozzle enable" animation);
    ///  * the rotation is one way only. When the thrust stops the nozzle is switched off again and the
    ///    rotor is driven back to the rotation it had when the part was built (the source keeps it as
    ///    _rotatedPartInitRotation), which is the rotation lock: it always settles back to its resting
    ///    orientation instead of coasting on.
    ///
    /// The port has no Animator, so the same timeline is reproduced here in code. The rotor is the
    /// chain that hosts the nozzle in the source prefab (RotateTurbine > Offset > Model > SonicFX).
    /// </summary>
    public sealed class RoverPortTurbine : MonoBehaviour
    {
        /// <summary>Rotor speed at full power.</summary>
        public const float MaxSpinDegreesPerSecond = 720f;

        /// <summary>How fast the rotor settles back to its resting rotation when the thrust stops.</summary>
        public const float UnwindDegreesPerSecond = 540f;

        /// <summary>Length of the enable impulse (the Animator's Impulse clip).</summary>
        public const float ImpulseDuration = 0.22f;

        /// <summary>Length of the shutdown (the Animator's ShakeOff clip).</summary>
        public const float ShutdownDuration = 0.28f;

        private const float ImpulseOvershoot = 1.18f;
        private const float ShakeAmplitude = 0.055f;
        private const float ShakeFrequency = 9f;
        private const float NozzleRestScale = 0.45f;

        // Serialized so Unity remaps them when the part is instantiated onto a contraption: the game
        // builds the prototype once and clones it, and non serialized references would be lost there.
        [SerializeField] private RoverPortPoweredPart m_powered;
        [SerializeField] private Transform m_rotor;
        [SerializeField] private Quaternion m_restRotation = Quaternion.identity;
        [SerializeField] private Transform m_nozzle;
        [SerializeField] private Renderer m_nozzleRenderer;
        [SerializeField] private Vector3 m_nozzleRestScale = Vector3.one;
        [SerializeField] private Color m_nozzleColor = Color.white;
        private MaterialPropertyBlock m_propertyBlock;
        private float m_angle;
        private float m_impulse;
        private float m_shutdown;
        private bool m_wasThrusting;

        /// <summary>Test hook: the audits drive the timetable directly instead of waiting for input.</summary>
        [System.NonSerialized] public bool PowerOverrideActive;
        [System.NonSerialized] public bool PowerOverride;

        public float RotorAngle
        {
            get { return m_angle; }
        }

        public bool NozzleVisible
        {
            get { return m_nozzle != null && m_nozzle.gameObject.activeSelf; }
        }

        public void Configure(RoverPortPartData data, Dictionary<string, Transform> nodes, RoverPortPoweredPart powered)
        {
            m_powered = powered;
            m_worldLocked = data.id == "part_ice_gyroturbine";
            m_rotor = FindRotor(nodes);
            if (m_rotor == null)
            {
                enabled = false;
                return;
            }
            m_restRotation = m_rotor.localRotation;
            m_directionCaptured = false;
            Transform nozzle = FindByName(nodes, "SonicFX");
            if (nozzle != null)
            {
                m_nozzle = nozzle;
                m_nozzleRenderer = nozzle.GetComponent<Renderer>();
                m_nozzleRestScale = nozzle.localScale;
                if (m_nozzleRenderer != null)
                {
                    m_propertyBlock = new MaterialPropertyBlock();
                    m_nozzleRenderer.GetPropertyBlock(m_propertyBlock);
                    m_nozzleColor = m_propertyBlock.GetColor("_Color");
                    if (m_nozzleColor.a <= 0f)
                    {
                        m_nozzleColor = Color.white;
                    }
                }
            }
        }

        [SerializeField] private bool m_worldLocked;
        private bool m_directionCaptured;
        private Vector3 m_lockedDirection;
        private Quaternion m_lockedRotation;
        public Vector3 ThrustDirection => m_worldLocked && m_directionCaptured
            ? m_lockedDirection : transform.right;

        // Called before physics starts, after the construction rotation has been applied.
        public void CaptureDirection()
        {
            if (m_directionCaptured) return;
            m_lockedDirection = transform.right.normalized;
            m_lockedRotation = m_rotor != null ? m_rotor.rotation : transform.rotation;
            m_directionCaptured = true;
        }

        private void LateUpdate()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>Advances the turbine timeline. Public so the audit can drive it in edit mode.</summary>
        public void Tick(float deltaTime)
        {
            if (m_rotor == null)
            {
                return;
            }
            if (m_propertyBlock == null && m_nozzleRenderer != null)
            {
                m_propertyBlock = new MaterialPropertyBlock();
                m_nozzleRenderer.GetPropertyBlock(m_propertyBlock);
                m_nozzleColor = m_propertyBlock.GetColor("_Color");
                if (m_nozzleColor.a <= 0f)
                {
                    m_nozzleColor = Color.white;
                }
            }
            bool thrusting = PowerOverrideActive ? PowerOverride : ReadThrusting();

            if (thrusting != m_wasThrusting)
            {
                m_wasThrusting = thrusting;
                if (thrusting)
                {
                    m_impulse = 0f;
                    m_shutdown = 0f;
                    SetNozzleActive(true);
                }
            }

            if (thrusting)
            {
                CaptureDirection();
                m_impulse = Mathf.Min(1f, m_impulse + deltaTime / ImpulseDuration);
                m_shutdown = 0f;
            }
            else
            {
                // Rotation lock: unwind back to the resting orientation instead of coasting.
                m_angle = 0f;
                m_shutdown = Mathf.Min(1f, m_shutdown + deltaTime / ShutdownDuration);
                if (m_shutdown >= 1f)
                {
                    m_impulse = 0f;
                    SetNozzleActive(false);
                }
            }

            if (m_worldLocked && m_directionCaptured)
                m_rotor.rotation = m_lockedRotation;
            else
                m_rotor.localRotation = m_restRotation;
            UpdateNozzle(thrusting, deltaTime);
        }

        private bool ReadThrusting()
        {
            return m_powered != null && Mathf.Abs(m_powered.InputThrottle) > 0.01f && m_powered.LastEnergyScale > 0.0001f;
        }

        /// <summary>Enable impulse, then the running shake, then the shutdown fade.</summary>
        private void UpdateNozzle(bool thrusting, float deltaTime)
        {
            if (m_nozzle == null)
            {
                return;
            }
            float scale;
            float alpha;
            if (thrusting)
            {
                float t = m_impulse;
                if (t < 0.5f)
                {
                    // Rising part of the impulse: overshoot.
                    scale = Mathf.Lerp(NozzleRestScale, ImpulseOvershoot, t / 0.5f);
                }
                else if (t < 1f)
                {
                    scale = Mathf.Lerp(ImpulseOvershoot, 1f, (t - 0.5f) / 0.5f);
                }
                else
                {
                    scale = 1f + ShakeAmplitude * Mathf.Sin(Time.time * ShakeFrequency * Mathf.PI * 2f);
                }
                alpha = Mathf.Clamp01(t * 3f);
            }
            else
            {
                float t = m_shutdown;
                scale = Mathf.Lerp(1f, NozzleRestScale, t);
                alpha = 1f - t;
            }
            m_nozzle.localScale = m_nozzleRestScale * scale;
            if (m_nozzleRenderer != null && m_propertyBlock != null)
            {
                Color color = m_nozzleColor;
                color.a *= alpha;
                m_nozzleRenderer.GetPropertyBlock(m_propertyBlock);
                m_propertyBlock.SetColor("_Color", color);
                m_nozzleRenderer.SetPropertyBlock(m_propertyBlock);
            }
        }

        private void SetNozzleActive(bool active)
        {
            if (m_nozzle == null)
            {
                return;
            }
            if (active)
            {
                m_nozzle.localScale = m_nozzleRestScale * NozzleRestScale;
                m_nozzle.gameObject.SetActive(true);
            }
            else
            {
                m_nozzle.gameObject.SetActive(false);
            }
        }

        /// <summary>The rotor is the node that carries the nozzle in the source prefab.</summary>
        private static Transform FindRotor(Dictionary<string, Transform> nodes)
        {
            Transform rotor = FindByName(nodes, "RotateTurbine");
            if (rotor != null)
            {
                return rotor;
            }
            Transform nozzle = FindByName(nodes, "SonicFX");
            return nozzle != null ? nozzle.parent : null;
        }

        private static Transform FindByName(Dictionary<string, Transform> nodes, string name)
        {
            foreach (KeyValuePair<string, Transform> pair in nodes)
            {
                if (pair.Value != null && string.Equals(pair.Value.name, name, System.StringComparison.Ordinal))
                {
                    return pair.Value;
                }
            }
            return null;
        }
    }
}
