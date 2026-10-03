using System;
using System.Collections.Generic;
using UnityEngine;

namespace RCR2Port
{
    [DefaultExecutionOrder(100)]
    public class RoverPortPoweredPart : RoverPortPart, IFuelConsumer
    {
        /// <summary>
        /// Native reference: one MotorWheel applies 50 N at its contact point (Part_MotorWheel_01_SET
        /// <c>m_force</c>) and reaches 15 m/s before the engine power factor. The audit compares both
        /// against the native prefab.
        /// </summary>
        public const float MotorWheelDriveForce = 50f;

        /// <summary>
        /// Drive force of one wheel that touches the ground, in MotorWheels. One ported wheel equals one
        /// native MotorWheel, so a part with two grounded tyres (the triple wheel rolls on two) pulls
        /// twice as hard and N grounded tyres pull N times as hard.
        /// </summary>
        public const float DriveForceInMotorWheels = 1f;

        /// <summary>Native MotorWheel top speed in m/s, before the engine power factor.</summary>
        public const float NativeMotorWheelSpeed = 15f;

        /// <summary>Fastest RCR2 rated wheel speed in rpm; slower wheels keep their lower top speed.</summary>
        public const float ReferenceMotorWheelRpm = 900f;

        /// <summary>
        /// Lowest friction a ported wheel may use. RCR2 wheels store friction on their own 0-50
        /// scale, where 35 maps to 0.7, and this floor is what keeps a low-rated RCR2 wheel from
        /// coming out frictionless.
        ///
        /// RC1 does not use this path any more: MAIN's imported Android wheels carry real
        /// PhysicMaterials on their colliders, and those are now reproduced per collider
        /// (see <see cref="RoverPortColliderMaterials"/> and Tools/RC1/BuildRC1Catalog.ps1).
        ///
        /// The old note here claimed BPLE's own parts run on Contraption_PhysMat at dynamic 0.7,
        /// which is not true of a wheel: Part_MotorWheel_*, Part_NormalWheel_* and
        /// Part_SmallWheel_* all put Contraption_WheelFriction_PhysMat (dynamic 0.025, static 0,
        /// Multiply) on the tyre and Contraption_PhysMat only on the SupportCollider above it.
        /// MAIN's imported Android wheels do the same. Applying the structural material's 0.7 to
        /// a tyre is 28x the grip either game gives it.
        /// </summary>
        public const float WheelMinimumFriction = 0.7f;

        /// <summary>
        /// BPLE power draw of one MotorWheel. Ported wheels report the same value so the contraption's
        /// engine power sharing treats them exactly like native motor wheels.
        /// </summary>
        public const float MotorWheelPowerConsumption = 100f;

        private readonly List<Rigidbody> m_driveBodies = new List<Rigidbody>();
        private readonly List<IFuelContainer> m_fallbackFuelContainers = new List<IFuelContainer>();
        private readonly List<Rigidbody> m_contactBodies = new List<Rigidbody>();
        /// <summary>The artwork pivots this part's drive step has to turn (see RoverPortWheelSpin).</summary>
        private readonly List<RoverPortWheelSpin> m_wheelSpins = new List<RoverPortWheelSpin>();
        private string m_motionKind;
        private float m_force;
        private float m_torque;
        private float m_maximumSpeed;
        private float m_reverseMaximumSpeed;
        private float m_acceleration;
        private float m_deceleration;
        private float m_inputMultiplier;
        private float m_consumption;
        private float m_suppliedThisStep;
        private float m_throttle;
        private float m_currentPropellerSpeed;
        private float m_enginePowerFactor = 1f;
        /// <summary>Energy scale computed in the last FixedUpdate (0 without fuel), for the turbine FX.</summary>
        private float m_lastEnergyScale;
        [SerializeField] private bool m_isJetTurbine;
        public bool IsJetTurbine => m_isJetTurbine;
        public const float JetPowerDemand = 10000f;
        public const float JetThrust = 1400f;

        /// <summary>
        /// Thrust of one ported Android thruster, in newtons (BPLE's <c>EngineAddForce</c> scale).
        ///
        /// MAIN's thruster is <c>PartSPrTurbo</c> (SourceMechanics/1020.json: <c>properties.power
        /// = 220</c>, <c>consumption = 5</c>) and it pushes its vehicle for as long as it is
        /// switched on, the way BPLE's own <c>JetEngine</c> applies <c>m_realForce</c> along its
        /// force direction (JetEngine.cs:375-377). The port has no slider for it and the catalog's
        /// <c>motion.force</c> is 0, so the figure is a constant here, next to
        /// <see cref="MotorWheelDriveForce"/> and <see cref="JetThrust"/>.
        /// </summary>
        public const float ThrusterThrust = 1500f;

        /// <summary>Energy the part could actually draw last step (see CalculateEnergyScale).</summary>
        public float LastEnergyScale
        {
            get { return m_lastEnergyScale; }
        }

        /// <summary>Player input the part received last step.</summary>
        public float InputThrottle
        {
            get { return m_throttle; }
        }
        private float m_externalEnginePowerFactor;
        private bool m_enabled;

        public int FuelComponentIndex { get; set; }

        public float RequiredFuelAmount
        {
            get
            {
                if (IsJetTurbine || !m_enabled || m_externalEnginePowerFactor > 0f)
                    return 0f;
                return Mathf.Max(0f, m_consumption * Mathf.Abs(m_throttle) * Time.fixedDeltaTime);
            }
        }

        public override void Awake()
        {
            base.Awake();
            RestorePoweredData();
        }

        public override void Configure(RoverPortPartData data, int partIndex, Dictionary<string, Transform> nodes, RoverPortIconSprite icon)
        {
            base.Configure(data, partIndex, nodes, icon);
            RestorePoweredData();
        }

        private void RestorePoweredData()
        {
            RoverPortPartData data = m_portData;
            if (data == null || data.motion == null)
            {
                return;
            }
            m_isJetTurbine = data.id == "part_ice_gyroturbine" || data.id == "part_terra_turbine_basic";
            m_motionKind = data.motion.kind;
            m_force = data.motion.force;
            m_torque = data.motion.torque;
            m_maximumSpeed = data.motion.max_speed;
            m_reverseMaximumSpeed = data.motion.reverse_max_speed > 0f ? data.motion.reverse_max_speed : data.motion.max_speed;
            // MAIN's MotorWheel never reads a top speed out of the prefab: InitializeEngine
            // hard-codes m_maximumSpeed = 15 * enginePowerFactor and m_maximumForce = m_force *
            // enginePowerFactor. The imported RC1 wheels therefore carry m_maximumSpeed = 0, which
            // left RatedSurfaceSpeed - and with it WheelDriveForce's falloff - at 0: the wheel
            // applied no drive force at all and, being airborne, never spun either.
            // ReferenceMotorWheelRpm is by definition the rpm that maps to that same native 15 m/s
            // (see RatedSurfaceSpeed), so an imported wheel with no catalog speed gets the native
            // one. Only the RC1 imports are touched (android_class, the same test Configure uses to
            // recognise them), so every RCR2/RC3 wheel keeps the speed its own catalog states.
            if (m_motionKind == "wheel" && m_maximumSpeed <= 0f && !string.IsNullOrEmpty(data.android_class))
            {
                m_maximumSpeed = ReferenceMotorWheelRpm;
                m_reverseMaximumSpeed = ReferenceMotorWheelRpm;
            }
            m_acceleration = Mathf.Max(0f, data.motion.acceleration);
            m_deceleration = Mathf.Max(0f, data.motion.deceleration);
            m_inputMultiplier = data.motion.input_multiplier > 0f ? data.motion.input_multiplier : m_maximumSpeed;
            m_consumption = data.motion.consumption > 0f ? data.motion.consumption : data.consumption;
            // Wheels draw power in BPLE's scale, the same as a native MotorWheel; every other ported
            // consumer keeps its RCR2 draw.
            m_powerConsumption = m_motionKind == "wheel" ? MotorWheelPowerConsumption : m_consumption;
            if (IsJetTurbine) m_powerConsumption = JetPowerDemand;
            bool sourceEngine = data.android_class == "Accumulator" && m_motionKind == "engine";
            m_enginePower = sourceEngine ? Mathf.Max(0f, data.motion.power) : 0f;
            if (sourceEngine) m_powerConsumption = 0f;
            RefreshDriveBodies();
        }

        public override void EnsureRigidbody()
        {
            RestorePoweredData();
            var jet = GetComponent<RoverPortTurbine>();
            if (jet != null) jet.CaptureDirection();
            base.EnsureRigidbody();
            RefreshDriveBodies();
        }

        private void RefreshDriveBodies()
        {
            m_driveBodies.Clear();
            m_contactBodies.Clear();
            if (m_portData == null || m_portData.motion == null)
            {
                return;
            }
            foreach (string bodyId in m_portData.motion.wheel_bodies ?? new string[0])
            {
                Rigidbody body = FindBody(bodyId);
                if (body != null && !m_driveBodies.Contains(body))
                {
                    m_driveBodies.Add(body);
                }
            }

            Rigidbody rootBody = gameObject.GetComponent<Rigidbody>();
            if (m_motionKind != "wheel" && rootBody != null)
            {
                m_driveBodies.Clear();
                m_driveBodies.Add(rootBody);
                return;
            }
            bool onlyRoot = m_driveBodies.Count == 1 && m_driveBodies[0] == rootBody;
            if (m_driveBodies.Count == 0 || onlyRoot)
            {
                m_driveBodies.Clear();
                foreach (SphereCollider wheelCollider in GetComponentsInChildren<SphereCollider>(true))
                {
                    Rigidbody body = wheelCollider.GetComponent<Rigidbody>();
                    if (body == null)
                    {
                        body = wheelCollider.GetComponentInParent<Rigidbody>();
                    }
                    if (body != null && body != rootBody && !m_driveBodies.Contains(body))
                    {
                        m_driveBodies.Add(body);
                    }
                }
                if (m_driveBodies.Count == 0)
                {
                    foreach (Rigidbody body in GetComponentsInChildren<Rigidbody>(true))
                    {
                        if (body != rootBody)
                        {
                            m_driveBodies.Add(body);
                        }
                    }
                }
            }
            if (m_driveBodies.Count == 0 && rootBody != null)
                m_driveBodies.Add(rootBody);
            RefreshContactBodies();
            // RoverPortPart.EnsureRigidbody (called just before this) attaches one
            // RoverPortWheelSpin per catalog wheel pivot, so the list is complete by now.
            m_wheelSpins.Clear();
            // AndroidTrackMotion advances each roller from the belt speed. The
            // generic wheel drive would otherwise advance the complete pivot
            // list once per roller and multiply the visual rotation.
            if (m_portData.motion == null || !m_portData.motion.external_physics_motion)
            {
                GetComponentsInChildren(true, m_wheelSpins);
            }
        }

        /// <summary>
        /// Bodies that provide this wheel's ground contact without being driven
        /// themselves. The mecanum wheel drives its hub, but the hub sits inside
        /// eight spring mounted rollers, so the hub's own collider never reaches
        /// the ground and the ring is what actually rolls.
        /// </summary>
        private void RefreshContactBodies()
        {
            m_contactBodies.Clear();
            if (m_portData == null || m_portData.motion == null || m_motionKind != "wheel")
            {
                return;
            }
            foreach (string bodyId in m_portData.motion.contact_bodies ?? new string[0])
            {
                Rigidbody body = FindBody(bodyId);
                if (body != null && !m_contactBodies.Contains(body) && !m_driveBodies.Contains(body))
                {
                    m_contactBodies.Add(body);
                }
            }
        }

        public override void InitializeEngine()
        {
            RefreshEnginePower();
        }

        /// <summary>
        /// Refreshes the engine power factor. Native MotorWheel calls ReinitializeEngine() every
        /// FixedUpdate; a ported wheel that only read the factor when <see cref="InitializeEngine"/> ran
        /// (before the contraption's connected components existed) would keep factor 1 and never see a
        /// native engine in its component, which shows up as native-wheel speed and acceleration being
        /// several times higher.
        /// </summary>
        private void RefreshEnginePower()
        {
            m_externalEnginePowerFactor = contraption == null ? 0f : contraption.GetEnginePowerFactor(this);
            m_enginePowerFactor = Mathf.Max(1f, m_externalEnginePowerFactor);
        }

        public override bool CanBeEnabled()
        {
            return true;
        }

        public override bool HasOnOffToggle()
        {
            return true;
        }

        public override bool IsEnabled()
        {
            return m_enabled;
        }

        public override void SetEnabled(bool enabled)
        {
            m_manualEnabled = enabled;
            ApplyEnabled(enabled);
        }

        private bool m_manualEnabled;

        internal void PrepareManualTrigger() => ApplyEnabled(m_manualEnabled);

        private void ApplyEnabled(bool enabled)
        {
            if (m_enabled == enabled) return;
            m_enabled = enabled;
            m_throttle = enabled ? 1f : 0f;
            if (!enabled)
            {
                m_suppliedThisStep = 0f;
                m_lastEnergyScale = 0f;
            }
            if (!IsEngine() && contraption != null && ConnectedComponent >= 0)
                contraption.UpdateEngineStates(ConnectedComponent);
        }

        /// <summary>
        /// Emits the APK plume only while the owning thruster receives a command and has energy.
        /// </summary>
        public override bool ShouldEmitParticles(string driver)
        {
            if (string.Equals(driver, "PS_Fire_Blue", System.StringComparison.Ordinal))
            {
                Contraption owner = contraption;
                bool firing = m_enabled && m_throttle > 0f;
                return firing && owner != null && owner.IsRunning && m_lastEnergyScale > 0f;
            }
            return base.ShouldEmitParticles(driver);
        }

        protected override void OnTouch()
        {
            SetEnabled(!m_manualEnabled);
        }

        public void ConsumeFuel(float amount)
        {
            if (m_enabled && !float.IsNaN(amount) && !float.IsInfinity(amount))
                m_suppliedThisStep += Mathf.Max(0f, amount);
        }

        private void Update()
        {
            if (INRc1Controls.Active && contraption != null && contraption.IsRunning)
            {
                SetControlInput();
                return;
            }
            ApplyEnabled(m_manualEnabled);
            if (!m_enabled || contraption == null || !contraption.IsRunning)
            {
                m_throttle = 0f;
                return;
            }
            if (IsJetTurbine) { m_throttle = 1f; return; }
            float throttle = Input.GetAxisRaw("Horizontal");
            if (Mathf.Abs(throttle) < 0.05f)
            {
                Gearbox gearbox = contraption.GetGearbox(this);
                throttle = gearbox != null && gearbox.IsEnabled() ? -1f : 1f;
            }
            m_throttle = m_enabled ? Mathf.Clamp(throttle, -1f, 1f) : 0f;
        }

        /// <summary>
        /// Whether this part is switched by the throttle rather than by the pedals when 「RC1 操作」 is
        /// on. Jet parts select their separate input in <see cref="ControlInput"/>.
        /// </summary>
        private bool DrivesOnThrottle
        {
            get
            {
                return IsJetTurbine ||
                       string.Equals(m_motionKind, "thruster", StringComparison.Ordinal) ||
                       string.Equals(m_motionKind, "propeller", StringComparison.Ordinal);
            }
        }

        private float ControlInput => m_portData != null && m_portData.motion != null && m_portData.motion.jet_control
            ? INRc1Controls.Jet : DrivesOnThrottle ? INRc1Controls.Throttle : INRc1Controls.DriveAxis;

        private void SetControlInput()
        {
            float input = ControlInput;
            bool held = Mathf.Abs(input) > 0.001f;
            ApplyEnabled(held || m_manualEnabled);
            m_throttle = held ? input : m_manualEnabled ? 1f : 0f;
        }

        private void FixedUpdate()
        {
            m_lastEnergyScale = 0f;
            if (WPFMonoBehaviour.levelManager == null || contraption == null)
            {
                return;
            }
            // Drive while the contraption is started, exactly like the native parts do
            // (MotorWheel.cs:250-255 gates on contraption.IsRunning). The game has two pause
            // authorities — GameTime's Time.timeScale and LevelManager.gameState — and the app
            // interface restores the time scale without touching the state, so a stale
            // PausedWhileRunning used to stop every ported wheel, jet and propeller while the game was
            // visibly running again: the vehicle coasted, its speed dropped "at random" and only
            // Continue brought it back. The same desync also zeroed the propeller integrator, which is
            // why turbine vehicles then had to spool up from a standstill.
            if (!contraption.IsRunning)
            {
                m_suppliedThisStep = 0f;
                m_currentPropellerSpeed = 0f;
                return;
            }
            // Native parts refresh their engine power factor every step; do the same so the drive and
            // the rated speed follow the engines in this contraption.
            RefreshEnginePower();

            if (INRc1Controls.Active) SetControlInput();
            else ApplyEnabled(m_manualEnabled);

            float energyScale = CalculateEnergyScale();
            m_lastEnergyScale = energyScale;
            switch (m_motionKind)
            {
                case "wheel":
                    DriveWheels(m_throttle, energyScale);
                    break;
                case "engine":
                    DriveForward(m_throttle, energyScale);
                    break;
                case "thruster":
                    DriveThruster(energyScale);
                    break;
                case "propeller":
                    DrivePropeller(m_throttle, energyScale);
                    break;
            }

            m_suppliedThisStep = 0f;
        }

        private float CalculateEnergyScale()
        {
            if (!m_enabled)
                return 0f;
            if (IsJetTurbine)
                return Mathf.Clamp01(m_externalEnginePowerFactor);
            if (m_externalEnginePowerFactor > 0f)
                return Mathf.Clamp01(m_externalEnginePowerFactor);
            if (m_consumption <= 0f)
            {
                return 1f;
            }
            if (m_suppliedThisStep > 0f && RequiredFuelAmount > 0f)
            {
                return Mathf.Clamp01(m_suppliedThisStep / RequiredFuelAmount);
            }
            if (FuelSystem.Instance != null && FuelSystem.Instance.Owner == contraption)
            {
                return 0f;
            }
            return ConsumeFallbackEnergy();
        }

        private float ConsumeFallbackEnergy()
        {
            if (contraption == null || ConnectedComponent < 0)
            {
                return 0f;
            }
            float available = 0f;
            List<IFuelContainer> containers = m_fallbackFuelContainers;
            containers.Clear();
            foreach (BasePart part in contraption.Parts)
            {
                IFuelContainer container = part as IFuelContainer;
                if (container != null && part.ConnectedComponent == ConnectedComponent && container.SupplyFuelAmount > 0f)
                {
                    containers.Add(container);
                    available += container.SupplyFuelAmount;
                }
            }
            float required = RequiredFuelAmount;
            if (available <= 0f || required <= 0f)
            {
                return 0f;
            }
            float scale = Mathf.Clamp01(available / required);
            float used = required * scale;
            foreach (IFuelContainer container in containers)
            {
                container.SupplyFuel(used * container.SupplyFuelAmount / available);
            }
            return scale;
        }

        /// <summary>
        /// Drives the wheels the way BPLE's own MotorWheel does: a tangential force applied straight at
        /// the ground contact of every wheel that touches the ground, ramped down as that wheel reaches
        /// its rated speed. Applying the force directly (instead of relying on tyre friction through a
        /// hinged wheel body) is what makes the acceleration match native wheels, which is also why the
        /// wheels are spun to match the rolling motion instead of being torque driven.
        /// </summary>
        private void DriveWheels(float throttle, float energyScale)
        {
            if (PortId == "part_mecanum_wheel")
            {
                DriveMecanumHub(throttle, energyScale);
                return;
            }
            if (m_driveBodies.Count == 0)
            {
                return;
            }

            bool hasCommand = Mathf.Abs(throttle) > 0.001f;
            float commandSign = hasCommand ? Mathf.Sign(throttle) : 0f;
            float rpm = throttle >= 0f ? m_maximumSpeed : m_reverseMaximumSpeed;
            float ratedSpeed = RatedGroundSpeed(rpm);
            float driveForce = DriveForcePerWheel;
            Rigidbody carrier = GetComponent<Rigidbody>();
            foreach (Rigidbody body in m_driveBodies)
            {
                if (body == null)
                {
                    continue;
                }
                Vector3 contact;
                Vector3 normal;
                Rigidbody ground;
                float rollingRadius;
                if (!TryFindWheelContact(body, out contact, out normal, out ground, out rollingRadius))
                {
                    // Airborne: nothing to push against, only spin the wheel for the visuals.
                    // MAIN does the same thing from MotorWheel.FixedUpdate: an airborne wheel with
                    // the motor on turns at its rated speed, one with the motor off coasts down.
                    SetWheelSpin(body, GetRollingRadius(body), commandSign * ratedSpeed * Mathf.Clamp01(energyScale), true);
                    continue;
                }
                Rigidbody host = carrier != null ? carrier : body;
                Vector3 tangent = Vector3.Cross(normal, Vector3.forward).normalized;
                float speedAlong = Vector3.Dot(host.velocity, tangent);
                Vector3 force = tangent * WheelDriveForce(driveForce, speedAlong, ratedSpeed, energyScale,
                    m_enginePowerFactor, commandSign);
                host.EngineAddForceAtPosition(force, contact, ForceMode.Force);
                if (ground != null && !ground.isKinematic)
                {
                    ground.EngineAddForceAtPosition(-force, contact, ForceMode.Force);
                }
                // Roll the wheel to match the motion so its own friction does not fight the drive.
                SetWheelSpin(body, rollingRadius, speedAlong, false);
            }
        }

        // This wheel transmits motor torque through its spring-mounted roller ring.
        // Pushing the chassis and assigning hub angularVelocity bypasses that suspension.
        private void DriveMecanumHub(float throttle, float energyScale)
        {
            foreach (Rigidbody hub in m_driveBodies)
            {
                if (hub == null) continue;
                HingeJoint hinge = hub.GetComponent<HingeJoint>();
                if (hinge == null) continue;
                float rpm = throttle >= 0f ? m_maximumSpeed : m_reverseMaximumSpeed;
                hub.maxAngularVelocity = Mathf.Max(hub.maxAngularVelocity, rpm * Mathf.Deg2Rad * 6f);
                JointMotor motor = hinge.motor;
                motor.targetVelocity = -throttle * rpm * 6f; // rpm -> degrees/sec
                motor.force = Mathf.Max(0f, m_torque) * Mathf.Clamp01(energyScale);
                motor.freeSpin = false;
                hinge.motor = motor;
                hinge.useMotor = energyScale > 0f && Mathf.Abs(throttle) > 0.001f;
            }
        }

        /// <summary>
        /// Ground contact of one driven wheel, plus the radius it rolls on. The
        /// wheel's own collider is tried first; when the driven body is a hub
        /// that sits inside its own ring of spring mounted rollers, the ring is
        /// asked instead because the hub alone can never reach the ground.
        /// </summary>
        private bool TryFindWheelContact(Rigidbody body, out Vector3 contact, out Vector3 normal,
            out Rigidbody ground, out float rollingRadius)
        {
            contact = Vector3.zero;
            normal = Vector3.up;
            ground = null;
            float ownRadius = GetWheelRadius(body);
            rollingRadius = ownRadius;
            if (TryFindGroundContact(body, ownRadius, out contact, out normal, out ground))
            {
                return true;
            }
            if (m_contactBodies.Count == 0)
            {
                return false;
            }
            Vector3 origin = GetColliderCentre(body);
            bool found = false;
            float lowest = 0f;
            foreach (Rigidbody patch in m_contactBodies)
            {
                if (patch == null)
                {
                    continue;
                }
                Vector3 patchContact;
                Vector3 patchNormal;
                Rigidbody patchGround;
                if (!TryFindGroundContact(patch, GetWheelRadius(patch), out patchContact, out patchNormal,
                        out patchGround))
                {
                    continue;
                }
                if (found && patchContact.y >= lowest)
                {
                    continue;
                }
                found = true;
                lowest = patchContact.y;
                contact = patchContact;
                normal = patchNormal;
                ground = patchGround;
                rollingRadius = Mathf.Max(ownRadius, Vector3.Distance(origin, patchContact));
            }
            return found;
        }

        /// <summary>
        /// Radius the wheel rolls on: its own collider, or - for a hub carried by
        /// a ring of spring mounted rollers - the ring that reaches the ground,
        /// so the spin matches the travel even while airborne.
        /// </summary>
        private float GetRollingRadius(Rigidbody body)
        {
            float radius = GetWheelRadius(body);
            if (m_contactBodies.Count == 0)
            {
                return radius;
            }
            Vector3 origin = GetColliderCentre(body);
            foreach (Rigidbody patch in m_contactBodies)
            {
                if (patch == null)
                {
                    continue;
                }
                radius = Mathf.Max(radius, Vector3.Distance(origin, GetColliderCentre(patch)) + GetWheelRadius(patch));
            }
            return radius;
        }

        /// <summary>Centre of the collider a body rolls on, in world space.</summary>
        private static Vector3 GetColliderCentre(Rigidbody body)
        {
            SphereCollider sphere = body.GetComponent<SphereCollider>();
            if (sphere == null)
            {
                sphere = body.GetComponentInChildren<SphereCollider>();
            }
            return sphere != null ? body.transform.TransformPoint(sphere.center) : body.worldCenterOfMass;
        }

        /// <summary>
        /// Signed drive force for one grounded wheel: a native MotorWheel's force times
        /// <see cref="DriveForceInMotorWheels"/>, scaled by the part's RCR2 motor torque, the engine power
        /// factor, the supplied energy and the speed falloff.
        /// </summary>
        public static float WheelDriveForce(float driveForce, float speedAlong, float ratedSpeed,
            float energyScale, float enginePowerFactor, float commandSign)
        {
            float falloff = ratedSpeed > 0.01f
                ? Mathf.Sqrt(Mathf.Clamp01(1f - Mathf.Abs(speedAlong) / ratedSpeed))
                : 0f;
            return commandSign * Mathf.Max(0f, driveForce) * Mathf.Max(0f, enginePowerFactor) *
                   Mathf.Clamp01(energyScale) * falloff;
        }

        /// <summary>
        /// Rated ground speed of this part in m/s at the given RCR2 motor speed: BPLE's native wheel top
        /// speed, scaled by the part's own RCR2 speed, so a 900 rpm wheel reaches the native 15 m/s while
        /// slower wheels keep their lower top speed.
        /// </summary>
        public float RatedSurfaceSpeed(float rpm)
        {
            return NativeMotorWheelSpeed * Mathf.Clamp01(rpm / ReferenceMotorWheelRpm);
        }

        /// <summary>
        /// Rated ground speed the drive actually uses: the part's own RCR2 speed scaled by the engine
        /// power factor, exactly like MotorWheel sets m_maximumSpeed = 15 * enginePowerFactor.
        /// </summary>
        public float RatedGroundSpeed(float rpm)
        {
            return RatedSurfaceSpeed(rpm) * m_enginePowerFactor;
        }

        /// <summary>
        /// Turns the wheel: the rigidbody (what an RCR2 wheel with its own body needs, and what
        /// its RoverPortFollower copies onto the artwork) *and* every artwork pivot the catalog
        /// lists. MAIN's own wheels do both halves in one place - MotorWheel applies the drive
        /// force at the contact point and rolls <c>WheelPivot</c> from the same contact raycast -
        /// so passing the same <paramref name="linearSpeed"/> to both keeps the port identical.
        /// </summary>
        private void SetWheelSpin(Rigidbody body, float radius, float linearSpeed, bool airborne)
        {
            if (body != null && GetComponent<RC1Port.RC1WheelSuspension>() == null &&
                (body != GetComponent<Rigidbody>() || string.IsNullOrEmpty(m_portData.android_class)))
            {
                // Rolling forward (positive X) is a clockwise spin, i.e. a negative Z angular velocity.
                body.angularVelocity = new Vector3(0f, 0f, -linearSpeed / Mathf.Max(0.01f, radius));
            }
            for (int i = 0; i < m_wheelSpins.Count; i++)
            {
                RoverPortWheelSpin spin = m_wheelSpins[i];
                if (spin != null)
                {
                    spin.Advance(linearSpeed, airborne, Time.fixedDeltaTime);
                }
            }
        }

        private static bool TryFindGroundContact(Rigidbody body, float radius, out Vector3 contact,
            out Vector3 normal, out Rigidbody ground)
        {
            contact = Vector3.zero;
            normal = Vector3.up;
            ground = null;
            Vector3 centre = GetColliderCentre(body);
            RaycastHit hit;
            if (!Physics.Raycast(centre, Vector3.down, out hit, radius + 0.1f, GroundMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }
            if (hit.rigidbody == body)
            {
                return false;
            }
            contact = hit.point;
            normal = hit.normal;
            ground = hit.rigidbody;
            return true;
        }

        private static int s_groundMask;

        /// <summary>Everything except the contraption itself, so wheels only drive against the world.</summary>
        private static int GroundMask
        {
            get
            {
                if (s_groundMask == 0)
                {
                    int contraptionLayer = LayerMask.NameToLayer("Contraption");
                    s_groundMask = contraptionLayer >= 0 ? ~(1 << contraptionLayer) : ~0;
                }
                return s_groundMask;
            }
        }

        /// <summary>
        /// Contact force one grounded wheel of this part delivers at full power without engine boost: one
        /// native MotorWheel per grounded tyre, so a part rolling on two tyres pulls twice as hard.
        /// </summary>
        public float DriveForcePerWheel
        {
            get { return MotorWheelDriveForce * DriveForceInMotorWheels; }
        }

        /// <summary>
        /// BPLE's own jet engine pushes with <c>rigidbody.EngineAddForce(force * forceDirection)</c>
        /// while it is switched on and fuelled (JetEngine.cs:369-377), so a ported Android thruster
        /// does the same: the part's own forward (<c>transform.right</c>, the axis
        /// <see cref="DriveForward"/> and every native thruster part use) at
        /// <see cref="ThrusterThrust"/>. MAIN's thruster is a plain on/off part
        /// (<c>PartSPrTurbo</c>, switched by the player the way this port's <c>OnTouch</c> does), so
        /// there is no throttle term; <paramref name="energyScale"/> is already 0 while the part is
        /// off, which is what gates the thrust.
        /// </summary>
        private void DriveThruster(float energyScale)
        {
            if (energyScale <= 0f)
            {
                return;
            }
            // RoverCraft's turbo and jet parts only fire while the turbo button is held; with
            // 「RC1 操作」 on that button is what the player has, so an unheld throttle means no thrust.
            if (m_throttle <= 0f)
            {
                return;
            }
            Rigidbody body = GetComponent<Rigidbody>();
            if (body == null)
            {
                return;
            }
            body.EngineAddForce(transform.right * (ThrusterThrust * Mathf.Clamp01(energyScale)), ForceMode.Force);
        }

        private void DriveForward(float throttle, float energyScale)
        {
            if (energyScale <= 0f)
            {
                return;
            }
            if (IsJetTurbine)
            {
                // The throttle button switches this family while 「RC1 操作」 is on, so an unheld
                // throttle means no thrust (m_throttle is INRc1Controls.Throttle for these parts).
                if (INRc1Controls.Active && m_throttle <= 0f)
                {
                    return;
                }
                var jet = GetComponent<RoverPortTurbine>();
                Rigidbody root = GetComponent<Rigidbody>();
                if (root != null && jet != null)
                {
                    jet.CaptureDirection();
                    root.EngineAddForce(jet.ThrustDirection * (JetThrust * Mathf.Clamp01(energyScale)), ForceMode.Force);
                }
                return;
            }
            float force = (m_force > 0f ? m_force : m_torque) * throttle * energyScale * m_enginePowerFactor;
            foreach (Rigidbody body in m_driveBodies)
            {
                if (body != null)
                {
                    body.EngineAddForce(transform.right * force, ForceMode.Force);
                }
            }
        }

        private void DrivePropeller(float throttle, float energyScale)
        {
            float targetSpeed = throttle * m_inputMultiplier * m_enginePowerFactor;
            float rate = Mathf.Abs(targetSpeed) > Mathf.Abs(m_currentPropellerSpeed) ? m_acceleration : m_deceleration;
            if (rate <= 0f)
            {
                rate = 1f;
            }
            m_currentPropellerSpeed = Mathf.MoveTowards(m_currentPropellerSpeed, targetSpeed, rate * Time.fixedDeltaTime);
            if (energyScale <= 0f)
            {
                return;
            }
            float denominator = Mathf.Max(2f, Mathf.Abs(m_currentPropellerSpeed));
            float force = m_force * m_currentPropellerSpeed / denominator * energyScale;
            foreach (Rigidbody body in m_driveBodies)
            {
                if (body != null)
                {
                    body.EngineAddForce(transform.right * force, ForceMode.Force);
                }
            }
        }

        private static float GetWheelRadius(Rigidbody body)
        {
            SphereCollider collider = body.GetComponent<SphereCollider>();
            if (collider == null)
            {
                collider = body.GetComponentInChildren<SphereCollider>();
            }
            if (collider == null) return 0.5f;
            Vector3 scale = collider.transform.lossyScale;
            float sphereScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            return Mathf.Max(0.01f, collider.radius * sphereScale);
        }

    }
}
