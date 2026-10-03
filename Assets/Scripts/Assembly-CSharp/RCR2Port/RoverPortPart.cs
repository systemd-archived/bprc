using System;
using System.Collections.Generic;
using UnityEngine;

namespace RCR2Port
{
    public class RoverPortPart : BasePart
    {
        [Serializable]
        private sealed class RoverPortNodeRef
        {
            public string id;
            public Transform node;
        }

        [Serializable]
        private sealed class RoverPortMountRef
        {
            public string point;
            public string body;
        }

        protected readonly Dictionary<string, Transform> m_nodes = new Dictionary<string, Transform>();
        [SerializeField]
        protected RoverPortPartData m_portData;
        [SerializeField]
        private List<RoverPortNodeRef> m_nodeRefs = new List<RoverPortNodeRef>();
        [SerializeField]
        private List<RoverPortMountRef> m_mountRefs = new List<RoverPortMountRef>();
        private bool m_runtimePhysicsCreated;
        [SerializeField] private List<Renderer> m_constructionRenderers = new List<Renderer>();
        [SerializeField] private List<Renderer> m_physicsRenderers = new List<Renderer>();

        /// <summary>
        /// Damper of the wheel connection spring, from BPLE's own
        /// <c>OffRoadWheel.CustomConnectToPart</c> (it hard-codes 5 there; the stiffness is the
        /// part's serialized <c>m_springStiffness</c>).
        /// </summary>
        public const float OffRoadWheelSpringDamper = 5f;

        public void ConfigureVisuals(List<Renderer> construction, List<Renderer> physics)
        {
            m_constructionRenderers = construction;
            m_physicsRenderers = physics;
            if (construction.Count > 0)
                foreach (Renderer renderer in physics) renderer.enabled = false;
        }

        public string PortId { get; private set; }
        public string DisplayName { get; private set; }
        public string Description { get; private set; }
        public RoverPortIconSprite PortIcon { get; private set; }

        /// <summary>
        /// The catalog record this part was built from. Serialized, so it survives being
        /// instantiated; the runtime helpers (<see cref="RoverPortWheelSpin"/>,
        /// <see cref="RoverPortContraptionCollision"/>) read it to re-resolve their own wiring
        /// on a placed clone, the same way <see cref="AttachSpriteBindings"/> does.
        /// </summary>
        public RoverPortPartData PortData
        {
            get { return m_portData; }
        }

        /// <summary>Public node lookup for the runtime helpers that live on a child node.</summary>
        public Transform ResolvePortNode(string id)
        {
            return FindNode(id);
        }

        public virtual void Configure(RoverPortPartData data, int partIndex, Dictionary<string, Transform> nodes, RoverPortIconSprite icon)
        {
            m_portData = data;
            PortId = data.id;
            DisplayName = data.display_name;
            Description = data.description;

            CacheNodeReferences(nodes);

            gameObject.name = data.asset_name;
            m_partType = RoverPortTypeMap.Resolve(data);
            m_partTier = (PartTier)Mathf.Clamp(data.rarity, 0, 4);
            customPartIndex = partIndex;
            m_mass = Mathf.Max(0.01f, data.mass);
            m_powerConsumption = Mathf.Max(0f, data.consumption);
            m_enginePower = 0f;
            m_interactiveRadius = 0.5f;
            m_breakVelocity = Mathf.Clamp(2f + data.durability / 12f, 2f, 12f);
            m_jointType = JointType.FixedJoint;
            // Native rotatable parts (wheels, frames, cargo, ...) use AutoAlignType.Rotate; without it
            // Contraption.Flip() ignores the part and clicking it in the build UI does nothing.
            m_autoAlign = AutoAlignType.Rotate;
            m_jointConnectionStrength = JointConnectionStrength.High;
            m_jointConnectionType = IsStructuralPart(data)
                ? JointConnectionType.Source
                : JointConnectionType.Target;
            m_jointConnectionDirection = JointConnectionDirection.Any;
            m_static = false;
            craftable = true;
            lootCrateReward = false;
            tags = new List<string> { "RCR2", data.category };
            PortIcon = icon;
            m_constructionIconSprite = icon;

            // The imported Android parts (RC1) publish the contract MAIN's
            // AndroidPartImporter writes onto every prefab, so the port applies the
            // source's own values instead of these category-derived defaults. The two
            // enum families share their numbering (JointType.FixedJoint = 0,
            // JointConnectionType.Source = 1, JointConnectionStrength.High = 2,
            // JointConnectionDirection.Any = 0), which is why the catalog can ship them
            // as plain integers. m_jointPreprocessing has no public setter on BasePart,
            // so that one value cannot be applied from here.
            if (!string.IsNullOrEmpty(data.android_class))
            {
                if (data.break_velocity > 0f)
                {
                    m_breakVelocity = data.break_velocity;
                }
                if (data.interactive_radius > 0f)
                {
                    m_interactiveRadius = data.interactive_radius;
                }
                m_jointType = (JointType)data.joint_type;
                m_jointConnectionType = (JointConnectionType)data.joint_connection_type;
                m_jointConnectionStrength = (JointConnectionStrength)data.joint_connection_strength;
                m_jointConnectionDirection = (JointConnectionDirection)data.joint_connection_direction;
                m_autoAlign = (AutoAlignType)data.auto_align;
                m_eightWay = data.eight_way;
                m_ZOffset = data.z_offset;
            }

            int layer = LayerMask.NameToLayer("Contraption");
            if (layer >= 0)
            {
                gameObject.layer = layer;
                foreach (Transform child in GetComponentsInChildren<Transform>(true))
                {
                    child.gameObject.layer = layer;
                }
            }

            RecalculateGridFootprint();
            AttachSpriteBindings();
            RC1Port.RC1WheelSuspension.RefreshGeometry(this);
        }

        public override void ApplyPartScale()
        {
            base.ApplyPartScale();
            RC1Port.RC1WheelSuspension.RefreshGeometry(this);
        }

        /// <summary>
        /// Whether the rebuilt Android particle system driven by <paramref name="driver"/>
        /// should be emitting right now. <see cref="RoverPortParticleDriver"/> polls this
        /// once a frame, the way MAIN's own drivers polled the vehicle.
        ///
        /// The exhaust pipe is the case this base class answers: MAIN's Exhaust.cs smokes
        /// while <c>owner.IsRunning &amp;&amp; owner.SomePoweredPartsEnabled()</c>, and
        /// deliberately not on <c>IsRunning</c> alone, because that is already true before
        /// the player switches a motor on. The thruster flame belongs to the part that
        /// fires it, so <see cref="RoverPortPoweredPart"/> overrides this for
        /// "PS_Fire_Blue".
        /// </summary>
        public virtual bool ShouldEmitParticles(string driver)
        {
            if (!string.Equals(driver, "FxSmoke", StringComparison.Ordinal))
            {
                return false;
            }
            Contraption owner = contraption;
            return owner != null && owner.IsRunning && owner.SomePoweredPartsEnabled();
        }

        /// <summary>
        /// Attaches the sprite/physics bindings the source prefab keeps in its
        /// own components (TransformFollower, SpriteRotatorBy2Points). Without
        /// them an animated wheel - the mecanum's roller ring and its rolling
        /// arms - is drawn standing still while the bodies move.
        /// </summary>
        private void AttachSpriteBindings()
        {
            if (m_portData == null)
            {
                return;
            }
            foreach (RoverPortFollowerData follower in m_portData.followers ?? new RoverPortFollowerData[0])
            {
                if (follower == null)
                {
                    continue;
                }
                Transform node = FindNode(follower.node);
                Transform target = FindNode(follower.target);
                if (node == null || target == null)
                {
                    continue;
                }
                RoverPortFollower component = node.GetComponent<RoverPortFollower>();
                if (component == null)
                {
                    component = node.gameObject.AddComponent<RoverPortFollower>();
                }
                component.Configure(target, follower.follow_position, follower.follow_rotation);
            }
            foreach (RoverPortStickRotatorData rotator in m_portData.stick_rotators ?? new RoverPortStickRotatorData[0])
            {
                if (rotator == null)
                {
                    continue;
                }
                Transform node = FindNode(rotator.node);
                Transform pointA = FindNode(rotator.point_a);
                Transform pointB = FindNode(rotator.point_b);
                if (node == null || pointA == null || pointB == null)
                {
                    continue;
                }
                RoverPortStickRotator component = node.GetComponent<RoverPortStickRotator>();
                if (component == null)
                {
                    component = node.gameObject.AddComponent<RoverPortStickRotator>();
                }
                component.Configure(pointA, pointB);
            }
        }

        public override void Awake()
        {
            base.Awake();
            EnsureRuntimeData();
            // Prefab instances do not carry the non-serialized wiring of the
            // binding components, so re-apply it from the port data.
            AttachSpriteBindings();
        }

        public IEnumerable<Vector3> GetConnectionPoints()
        {
            if (!PartConnections.Enabled(this)) yield break;
            EnsureRuntimeData();
            foreach (Transform point in m_nodes.Values)
            {
                if (point == null || !point.name.StartsWith("Point-", StringComparison.Ordinal))
                    continue;
                for (Transform parent = point.parent; parent != null && parent != transform; parent = parent.parent)
                {
                    if (parent.name != "ConnectionPoints")
                        continue;
                    yield return point.position;
                    break;
                }
            }
        }

        /// <summary>
        /// RCR2 keeps separate joint bodies per mount (RoverPhysicsElement's
        /// _separateJointBodies + separateJoints). The stabilizer's two upper
        /// mounts belong to its servo platform, so a frame stacked on top is
        /// welded to the platform and turns with it instead of hanging off the
        /// part root while the platform moves underneath.
        /// </summary>
        public void SetConnectionMounts(RoverPortMountData[] mounts)
        {
            if (m_mountRefs == null)
            {
                m_mountRefs = new List<RoverPortMountRef>();
            }
            m_mountRefs.Clear();
            foreach (RoverPortMountData mount in mounts ?? new RoverPortMountData[0])
            {
                if (mount == null || string.IsNullOrEmpty(mount.point) || string.IsNullOrEmpty(mount.body))
                {
                    continue;
                }
                m_mountRefs.Add(new RoverPortMountRef { point = mount.point, body = mount.body });
            }
        }

        /// <summary>Mount body a neighbour part has to be jointed to at this anchor.</summary>
        private Transform FindConnectionMountBody(Vector3 worldAnchor)
        {
            if (m_mountRefs == null || m_mountRefs.Count == 0)
            {
                return null;
            }
            EnsureRuntimeData();
            float tolerance = RoverPortConnections.AnchorTolerance * 2f;
            foreach (RoverPortMountRef mount in m_mountRefs)
            {
                Transform point = FindNodeByName(mount.point);
                if (point == null)
                {
                    continue;
                }
                Vector3 delta = point.position - worldAnchor;
                delta.z = 0f;
                if (delta.sqrMagnitude > tolerance * tolerance)
                {
                    continue;
                }
                Transform body = FindNodeByName(mount.body);
                if (body != null && body != transform)
                {
                    return body;
                }
            }
            return null;
        }

        private Transform FindNodeByName(string nodeName)
        {
            foreach (KeyValuePair<string, Transform> pair in m_nodes)
            {
                Transform node = pair.Value;
                if (node != null && node.name == nodeName)
                {
                    return node;
                }
            }
            return null;
        }

        public override void CreateCustomJoints()
        {
            if (contraption == null || ContraptionLayers.Enabled(contraption))
                return;
            foreach (BasePart other in contraption.Parts)
            {
                if (other == null || other == this)
                    continue;
                bool connected = false;
                foreach (Joint joint in contraption.FindPartJoints(this))
                {
                    if (joint != null && ((joint.GetComponent<Rigidbody>() == rigidbody && joint.connectedBody == other.rigidbody) ||
                        (joint.GetComponent<Rigidbody>() == other.rigidbody && joint.connectedBody == rigidbody)))
                    {
                        connected = true;
                        break;
                    }
                }
                if (connected)
                    continue;
                Vector3 anchor;
                Direction direction;
                if (!RoverPortConnections.TryGetConnection(this, other, out anchor, out direction))
                    continue;
                ConnectAtAnchor(other, anchor);
            }
        }

        internal void ConnectAtAnchor(BasePart other, Vector3 anchor)
        {
            if (contraption == null || ContraptionLayers.Enabled(contraption) || other == null || contraption.HasJointBetween(this, other)) return;
            Vector3 local = transform.InverseTransformPoint(anchor);
            Direction direction = Mathf.Abs(local.x) > Mathf.Abs(local.y)
                ? (local.x > 0f ? Direction.Right : Direction.Left)
                : (local.y > 0f ? Direction.Up : Direction.Down);
            if (UsesSourceWheelConnection && IsConnectionSource(direction))
                AddSourceWheelConnection(other, anchor, direction);
            else
            {
                Transform mountBody = FindConnectionMountBody(anchor);
                if (mountBody == null) contraption.AddFixedJoint(this, other, anchor);
                else AddFixedJointOnBody(mountBody, other, anchor);
            }
        }

        /// <summary>
        /// Whether BPLE would ask <b>this</b> part for the joint to its neighbour in
        /// <paramref name="direction"/>, i.e. whether this part's connection mask covers that side.
        ///
        /// BPLE's own walk (Contraption.AddCustomJoints) asks the part on the covered side:
        /// <c>if ((bitDirection(part) & bit(direction)) != None) joint = part.CustomConnectToPart(other)</c>,
        /// and only when the *neighbour's* mask covers the opposite side does it ask the neighbour
        /// instead - otherwise it falls back to <c>AddFixedJoint</c>. A bigfoot wheel is
        /// <c>JointConnectionDirection.Any</c> (15 bits), so it is always the source; the track is
        /// <c>Left</c>, so a part bolted to its right side gets that part's joint, exactly as in
        /// MAIN. Without this test the port would spring every connection a track makes.
        /// </summary>
        private bool IsConnectionSource(Direction direction)
        {
            BitDirection own = m_jointConnectionDirection.ToBitDirection();
            return (own & (BitDirection)(1 << (int)direction)) != BitDirection.None;
        }

        /// <summary>
        /// Whether the source component of this part overrides BPLE's <c>CustomConnectToPart</c>,
        /// so that the joint this part makes with its neighbour is not the plain
        /// <c>FixedJoint</c>/<c>HingeJoint</c> the joint type implies.
        ///
        /// Exactly one imported component does: <c>OffRoadWheel</c>, i.e. the two bigfoot wheels
        /// and the track. It returns a <c>ConfigurableJoint</c> whose Y axis is limited and
        /// carries a <c>linearLimitSpring</c> of <c>m_springStiffness</c> (150 on both bigfoot
        /// wheels, 110 on the track, 5 damping) while X and Z stay hard locked - the wheel gives
        /// along the connection direction like a spring and cannot cushion sideways. Every other
        /// imported part leaves the port's own rigid joint in place, which is what BPLE's
        /// <c>Contraption.AddFixedJoint</c> would have built for it.
        ///
        /// A ported part never reaches <c>CustomConnectToPart</c>: <c>Contraption</c> skips ported
        /// parts when it walks the neighbour grid (Contraption.cs, the
        /// <c>is RCR2Port.RoverPortPart</c> guard) and the port builds the connection itself here,
        /// so the override has to be reproduced rather than called. The value it needs was already
        /// in the catalog and simply unread.
        /// </summary>
        private bool UsesSourceWheelConnection
        {
            get
            {
                if (m_portData == null || string.IsNullOrEmpty(m_portData.android_class) || m_portData.motion == null)
                {
                    return false;
                }
                // RC1 tyres have their own axle joint; neighbour joints hold the bracket rigidly.
                if (GetComponent<RC1Port.RC1WheelSuspension>() != null)
                    return false;
                // AndroidTrack uses OffRoadWheel only as a propulsion shell. Its
                // external-physics flag makes the source connection rigid; the
                // individual roller joints own the track suspension.
                if (m_portData.motion.external_physics_motion)
                {
                    return true;
                }
                if (string.Equals(m_portData.base_component, "OffRoadWheel", StringComparison.Ordinal))
                {
                    return m_portData.motion.spring_stiffness > 0f;
                }
                return m_portData.motion.connection_frequency > 0f &&
                    (string.Equals(m_portData.android_class, "AndroidMotorWheel", StringComparison.Ordinal) ||
                     string.Equals(m_portData.android_class, "AndroidGenerator", StringComparison.Ordinal));
            }
        }

        /// <summary>
        /// Stiffness of this part's connection spring, or 0 when its neighbour joints are the
        /// ordinary rigid <c>FixedJoint</c>/<c>HingeJoint</c> (see
        /// <see cref="UsesSourceWheelConnection"/>). Read by the diagnostics probe.
        /// </summary>
        public float SourceWheelSpringStiffness
        {
            get { return UsesSourceWheelConnection && m_portData.motion.spring_stiffness > 0f ? m_portData.motion.spring_stiffness : 0f; }
        }

        /// <summary>
        /// <c>OffRoadWheel.CustomConnectToPart</c>, reproduced field for field.
        ///
        /// Two deliberate notes. The anchors are left auto configured exactly as the source leaves
        /// them (it never touches <c>anchor</c> or <c>autoConfigureConnectedAnchor</c>), where the
        /// port's other connections pin an explicit world anchor. And the <c>breakForce</c> is
        /// left at Unity's default, so this joint cannot be pulled apart: that is what BPLE ships
        /// - the override does not set it, and <c>Contraption.AddJointToMap</c> only scales
        /// whatever is already there - so giving it the port's ordinary break force would be a
        /// change to the source behaviour, not a repair of it.
        ///
        /// <c>enablePreprocessing</c> and the projection triple are the other half of the source's
        /// joint (OffRoadWheel.cs:305-308) and the port used to omit them. Without them a
        /// ConfigurableJoint cannot correct accumulated error: once the two bodies overlap the
        /// constraint only grows until the solver throws them apart. That is exactly what a player
        /// saw when they mounted the big wheel - the vehicle launched at speed the moment the run
        /// started - so they are set here, to the same values. <c>enableCollision</c> stays at
        /// Unity's default (off) rather than the source's <c>!m_externalPhysicsMotion</c>: the
        /// port's own <see cref="RoverPortContraptionCollision"/> already makes a wheel ignore its
        /// own vehicle, which is the case that matters here.
        /// </summary>
        private void AddSourceWheelConnection(BasePart other, Vector3 worldAnchor, Direction direction)
        {
            other.EnsureRigidbody();
            Rigidbody body = FindRuntimeBody(transform);
            if (body == null || other.rigidbody == null)
            {
                return;
            }
            ConfigurableJoint joint = gameObject.AddComponent<ConfigurableJoint>();
            joint.connectedBody = other.rigidbody;
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = body.transform.InverseTransformPoint(worldAnchor);
            joint.connectedAnchor = other.transform.InverseTransformPoint(worldAnchor);
            joint.angularXMotion = ConfigurableJointMotion.Locked;
            joint.angularYMotion = ConfigurableJointMotion.Locked;
            joint.angularZMotion = ConfigurableJointMotion.Locked;
            Vector3 axis = body.transform.InverseTransformDirection(transform.TransformDirection(BasePart.GetDirectionVector(direction)));
            if (axis.sqrMagnitude < 0.0001f)
            {
                axis = Vector3.up;
            }
            axis.Normalize();
            joint.axis = axis;
            Vector3 secondary = Vector3.Cross(Vector3.forward, axis);
            if (secondary.sqrMagnitude < 0.0001f)
            {
                secondary = Vector3.Cross(Vector3.right, axis);
            }
            joint.secondaryAxis = secondary.normalized;

            bool externalPhysics = m_portData.motion.external_physics_motion;
            joint.xMotion = externalPhysics ? ConfigurableJointMotion.Locked : ConfigurableJointMotion.Limited;
            joint.yMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Locked;
            if (!externalPhysics)
            {
                float springStiffness = m_portData.motion.spring_stiffness;
                float springDamper = OffRoadWheelSpringDamper;
                if (springStiffness <= 0f)
                {
                    float frequency = Mathf.Max(0.01f, m_portData.motion.connection_frequency);
                    float dampingRatio = Mathf.Clamp(m_portData.motion.connection_damping_ratio, 0f, 2f);
                    float reducedMass = body.mass * other.rigidbody.mass / Mathf.Max(0.01f, body.mass + other.rigidbody.mass);
                    float omega = 2f * Mathf.PI * frequency;
                    springStiffness = reducedMass * omega * omega;
                    springDamper = 2f * dampingRatio * reducedMass * omega;
                }
                SoftJointLimitSpring spring = joint.linearLimitSpring;
                spring.spring = springStiffness;
                spring.damper = springDamper;
                joint.linearLimitSpring = spring;
                SoftJointLimit limit = joint.linearLimit;
                limit.limit = 0f;
                limit.bounciness = 0f;
                joint.linearLimit = limit;
            }
            // OffRoadWheel.cs:305-308. The source's own anti-explosion settings.
            joint.enablePreprocessing = true;
            joint.projectionMode = JointProjectionMode.PositionAndRotation;
            joint.projectionDistance = 0.02f;
            joint.projectionAngle = 2f;
            joint.enableCollision = false;
            if (externalPhysics)
            {
                joint.breakForce = float.PositiveInfinity;
                joint.breakTorque = float.PositiveInfinity;
            }
            contraption.AddJointToMap(this, other, joint);
            RoverPortContraptionCollision collision = GetComponent<RoverPortContraptionCollision>();
            if (collision != null)
            {
                collision.Refresh();
            }
        }

        /// <summary>
        /// The same joint Contraption.AddFixedJoint builds, but anchored on one of
        /// the part's own separate joint bodies instead of the part root, so the
        /// neighbour part rides that body (the stabilizer's servo platform).
        /// </summary>
        private void AddFixedJointOnBody(Transform body, BasePart other, Vector3 worldAnchor)
        {
            other.EnsureRigidbody();
            Joint joint;
            if (m_jointType == BasePart.JointType.HingeJoint || other.m_jointType == BasePart.JointType.HingeJoint)
            {
                HingeJoint hinge = body.gameObject.AddComponent<HingeJoint>();
                hinge.autoConfigureConnectedAnchor = false;
                hinge.anchor = body.InverseTransformPoint(other.transform.position) * 0.5f;
                hinge.connectedAnchor = other.transform.InverseTransformPoint(body.position) * 0.5f;
                JointLimits limits = hinge.limits;
                limits.min = -0.1f;
                limits.max = 0.1f;
                hinge.limits = limits;
                hinge.axis = Vector3.forward;
                hinge.useLimits = true;
                joint = hinge;
            }
            else
            {
                joint = body.gameObject.AddComponent<FixedJoint>();
            }
            joint.connectedBody = other.rigidbody;
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = body.InverseTransformPoint(worldAnchor);
            joint.connectedAnchor = other.transform.InverseTransformPoint(worldAnchor);
            joint.enablePreprocessing = JointPreprocessing && other.JointPreprocessing;
            joint.breakForce = RoverPortConnections.GetJointBreakForce(contraption, this, other);
            contraption.AddJointToMap(this, other, joint);
        }

        public override IEnumerable<Collider> GetColliders()
        {
            return GetComponentsInChildren<Collider>(true);
        }

        public override IEnumerable<Renderer> GetRenderers()
        {
            return GetComponentsInChildren<Renderer>(true);
        }

        public override IEnumerable<Rigidbody> GetRigidbodies()
        {
            return GetComponentsInChildren<Rigidbody>(true);
        }

        public override void EnsureRigidbody()
        {
            EnsureRuntimeData();
            if (m_runtimePhysicsCreated)
            {
                PartPhysicsParameters.Refresh(this);
                return;
            }
            m_runtimePhysicsCreated = true;
            foreach (Renderer renderer in m_constructionRenderers) renderer.enabled = false;
            foreach (Renderer renderer in m_physicsRenderers) renderer.enabled = true;
            EnsureRootRigidbody();
            CreateRuntimeBodies();
            NormalizeBodyMasses();
            CreateRuntimeJoints();
            RC1Port.RC1WheelSuspension.Attach(this);
            IgnoreInternalCollisions();
            AttachWheelMotion();
            RoverPortSpoiler.Attach(this, m_portData);
            PartPhysicsParameters.Refresh(this);
        }

        /// <summary>
        /// Wires up the two runtime helpers a ported *wheel* needs and no other ported part
        /// does. Both are re-applied here rather than at configure time because a placed part
        /// is a clone (<c>ConstructionUI.SetPartAt</c> instantiates the catalog template), and
        /// this is the same moment the source's own wheel components start: MAIN calls
        /// <c>OffRoadWheel.PostInitialize</c> and <c>AndroidTrackMotion.InitializePhysics</c>
        /// from <c>BasePart.EnsureRigidbody</c>'s start-of-run pass.
        ///
        /// <see cref="RoverPortWheelSpin"/> turns the artwork node the source's component turns
        /// (see <see cref="RoverPortWheelPivotData"/>); <see cref="RoverPortContraptionCollision"/>
        /// reproduces the source's non-collision rule for OffRoadWheel and AndroidTrackMotion.
        /// </summary>
        private void AttachWheelMotion()
        {
            if (m_portData == null || m_portData.motion == null ||
                !string.Equals(m_portData.motion.kind, "wheel", StringComparison.Ordinal))
            {
                return;
            }
            foreach (RoverPortWheelPivotData pivot in m_portData.motion.wheel_pivots ?? new RoverPortWheelPivotData[0])
            {
                if (pivot == null || string.IsNullOrEmpty(pivot.node))
                {
                    continue;
                }
                Transform node = FindNode(pivot.node);
                if (node == null)
                {
                    continue;
                }
                RoverPortWheelSpin spin = node.GetComponent<RoverPortWheelSpin>();
                if (spin == null)
                {
                    spin = node.gameObject.AddComponent<RoverPortWheelSpin>();
                }
                spin.Configure(pivot.node, pivot.radius, pivot.mode, pivot.source,
                    pivot.travel, pivot.spring_frequency, pivot.damping);
            }
            bool needsWheelCollisionRule = !string.IsNullOrEmpty(m_portData.android_class) ||
                string.Equals(m_portData.base_component, "OffRoadWheel", StringComparison.Ordinal) ||
                m_portData.motion.external_physics_motion;
            if (needsWheelCollisionRule && GetComponent<RoverPortContraptionCollision>() == null)
            {
                gameObject.AddComponent<RoverPortContraptionCollision>();
            }
            var collision = GetComponent<RoverPortContraptionCollision>();
            if (collision != null) collision.Refresh();
            RoverPortTrackMotion.Attach(this, m_portData);
            RoverPortSkiMotion.Attach(this, m_portData);
        }

        private Rigidbody EnsureRootRigidbody()
        {
            Rigidbody body = GetComponent<Rigidbody>();
            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody>();
                body.mass = 1f;
            }
            body.constraints = (RigidbodyConstraints)56;
            body.drag = 0.2f;
            body.angularDrag = 0.05f;
            body.useGravity = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            // The imported spoiler's own EnsureRigidbody overwrites both drags (Spoiler.cs:107-108:
            // 1 and 0.2, the values Wings and Tail use), because a spoiler is an aerodynamic part
            // and MAIN does not want it damping like a girder. The rest of the constraints, the
            // mass and the interpolation stay the frame defaults, exactly as they do in MAIN.
            if (m_portData != null && m_portData.spoiler != null)
            {
                body.drag = 1f;
                body.angularDrag = 0.2f;
            }
            return body;
        }

        private void CreateRuntimeBodies()
        {
            if (m_portData == null || m_portData.bodies == null)
            {
                return;
            }
            foreach (RoverPortBodyData bodyData in m_portData.bodies)
            {
                if (bodyData == null)
                {
                    continue;
                }
                Transform node = FindNode(bodyData.node);
                if (node == null)
                {
                    continue;
                }
                Rigidbody body = node.GetComponent<Rigidbody>();
                if (body == null)
                {
                    body = node.gameObject.AddComponent<Rigidbody>();
                }
                body.mass = Mathf.Max(0.01f, bodyData.mass);
                body.drag = Mathf.Max(0f, bodyData.linear_drag);
                body.angularDrag = Mathf.Max(0f, bodyData.angular_drag);
                body.useGravity = bodyData.gravity_scale != 0f;
                body.isKinematic = bodyData.body_type != 0 || !bodyData.simulated;
                body.constraints = node == transform ? (RigidbodyConstraints)56 : ConvertConstraints(bodyData.constraints);
                RoverPortGravityBody gravityBody = node.GetComponent<RoverPortGravityBody>();
                if (gravityBody == null)
                {
                    gravityBody = node.gameObject.AddComponent<RoverPortGravityBody>();
                }
                gravityBody.Configure(body, bodyData.gravity_scale);
            }
        }

        private void CreateRuntimeJoints()
        {
            if (m_portData == null || m_portData.joints == null)
            {
                return;
            }
            Dictionary<Transform, float> rawMasses = BuildRawMassTable();
            foreach (RoverPortJointData jointData in m_portData.joints)
            {
                if (jointData == null || !jointData.enabled)
                {
                    continue;
                }
                Transform node = FindNode(jointData.node);
                Transform connectedNode = FindNode(jointData.connected_body);
                if (node == null || connectedNode == null)
                {
                    continue;
                }
                Rigidbody body = FindRuntimeBody(node);
                Rigidbody connectedBody = FindRuntimeBody(connectedNode);
                if (body == null || connectedBody == null || body == connectedBody)
                {
                    continue;
                }

                switch (jointData.type)
                {
                    case "FixedJoint":
                        CreateFixedJoint(body, connectedBody, jointData);
                        break;
                    case "HingeJoint":
                        CreateHingeJoint(body, connectedBody, jointData);
                        break;
                    case "SliderJoint":
                        CreateSliderJoint(body, connectedBody, jointData);
                        break;
                    case "WheelJoint":
                        CreateWheelJoint(body, connectedBody, jointData, rawMasses);
                        break;
                    case "SpringJoint":
                        CreateSpringJoint(body, connectedBody, jointData, rawMasses);
                        break;
                    case "DistanceJoint":
                        CreateDistanceJoint(body, connectedBody, jointData);
                        break;
                }
            }
        }

        /// <summary>
        /// Source joints whose connected anchor was auto configured (Box2D
        /// recomputes the anchor so both anchor points coincide) keep that
        /// behaviour instead of trusting the value frozen in the prefab, which
        /// guarantees the pin connects the two parts where they actually are.
        /// </summary>
        private static ConfigurableJoint CreateConfigurableJoint(Rigidbody body, Rigidbody connectedBody, RoverPortJointData data)
        {
            ConfigurableJoint joint = body.gameObject.AddComponent<ConfigurableJoint>();
            joint.connectedBody = connectedBody;
            joint.anchor = ToVector3(data.anchor);
            joint.autoConfigureConnectedAnchor = data.auto_configure_connected_anchor;
            if (!data.auto_configure_connected_anchor)
            {
                joint.connectedAnchor = ToVector3(data.connected_anchor);
            }
            joint.enableCollision = false;
            joint.enablePreprocessing = false;
            return joint;
        }

        private static void LockAllMotion(ConfigurableJoint joint)
        {
            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Locked;
            joint.angularXMotion = ConfigurableJointMotion.Locked;
            joint.angularYMotion = ConfigurableJointMotion.Locked;
            joint.angularZMotion = ConfigurableJointMotion.Locked;
        }

        private static void CreateFixedJoint(Rigidbody body, Rigidbody connectedBody, RoverPortJointData data)
        {
            ConfigurableJoint joint = CreateConfigurableJoint(body, connectedBody, data);
            LockAllMotion(joint);
        }

        private static void CreateHingeJoint(Rigidbody body, Rigidbody connectedBody, RoverPortJointData data)
        {
            HingeJoint joint = body.gameObject.AddComponent<HingeJoint>();
            joint.connectedBody = connectedBody;
            joint.anchor = ToVector3(data.anchor);
            joint.axis = Vector3.forward;
            joint.autoConfigureConnectedAnchor = data.auto_configure_connected_anchor;
            if (!data.auto_configure_connected_anchor)
            {
                joint.connectedAnchor = ToVector3(data.connected_anchor);
            }
            joint.enableCollision = false;
            joint.enablePreprocessing = false;

            float lower = Mathf.Min(data.lower_limit, data.upper_limit);
            float upper = Mathf.Max(data.lower_limit, data.upper_limit);
            if (data.use_limits)
            {
                JointLimits limits = joint.limits;
                limits.min = lower;
                limits.max = upper;
                joint.limits = limits;
                joint.useLimits = true;
            }

            if (data.max_motor_torque > 0f && data.use_motor)
            {
                JointMotor motor = joint.motor;
                motor.targetVelocity = data.motor_speed;
                motor.force = data.max_motor_torque;
                motor.freeSpin = false;
                joint.motor = motor;
                joint.useMotor = true;
            }
        }

        private static void CreateSliderJoint(Rigidbody body, Rigidbody connectedBody, RoverPortJointData data)
        {
            ConfigurableJoint joint = CreateConfigurableJoint(body, connectedBody, data);
            LockAllMotion(joint);
            joint.axis = Quaternion.AngleAxis(data.angle, Vector3.forward) * Vector3.right;
            joint.secondaryAxis = Vector3.Cross(Vector3.forward, joint.axis);
            joint.xMotion = data.use_limits ? ConfigurableJointMotion.Limited : ConfigurableJointMotion.Free;
            if (data.use_limits)
            {
                // The travel is centred on the anchor the port places itself, so
                // this joint owns its connected anchor even when the source had
                // it auto configured.
                joint.autoConfigureConnectedAnchor = false;
                float lower = Mathf.Min(data.lower_limit, data.upper_limit);
                float upper = Mathf.Max(data.lower_limit, data.upper_limit);
                float center = (lower + upper) * 0.5f;
                Vector3 offset = joint.axis * center;
                joint.connectedAnchor = ToVector3(data.connected_anchor) + connectedBody.transform.InverseTransformVector(body.transform.TransformVector(offset));
                SoftJointLimit limit = joint.linearLimit;
                limit.limit = Mathf.Max(0.001f, (upper - lower) * 0.5f);
                limit.bounciness = 0f;
                joint.linearLimit = limit;
            }
        }

        private static void CreateWheelJoint(Rigidbody body, Rigidbody connectedBody, RoverPortJointData data, Dictionary<Transform, float> rawMasses)
        {
            ConfigurableJoint joint = CreateConfigurableJoint(body, connectedBody, data);
            LockAllMotion(joint);
            joint.axis = Quaternion.AngleAxis(data.angle, Vector3.forward) * Vector3.right;
            joint.secondaryAxis = Vector3.Cross(Vector3.forward, joint.axis);
            joint.xMotion = data.frequency > 0f ? ConfigurableJointMotion.Free : ConfigurableJointMotion.Locked;
            joint.angularZMotion = ConfigurableJointMotion.Free;
            float stiffness;
            float damping;
            GetSpringParameters(body, connectedBody, data, rawMasses, out stiffness, out damping);
            joint.xDrive = new JointDrive { positionSpring = stiffness, positionDamper = damping, maximumForce = float.MaxValue };
        }

        /// <summary>
        /// Source masses of every body, by transform. Box2D stores a joint's suspension as absolute
        /// spring constants derived from the prefab masses; the port normalizes body masses to the
        /// catalog stat, so reading the live (normalized) masses here would soften every spring by
        /// the normalization factor - the roller suspension collapsed to about 1/16 of the original
        /// stiffness under load. Joints must use the original masses to keep the original rates.
        /// </summary>
        private Dictionary<Transform, float> BuildRawMassTable()
        {
            Dictionary<Transform, float> masses = new Dictionary<Transform, float>();
            if (m_portData == null || m_portData.bodies == null)
            {
                return masses;
            }
            foreach (RoverPortBodyData bodyData in m_portData.bodies)
            {
                if (bodyData == null)
                {
                    continue;
                }
                Transform node = FindNode(bodyData.node);
                if (node != null)
                {
                    masses[node] = Mathf.Max(0.01f, bodyData.mass);
                }
            }
            return masses;
        }

        private static float RawMass(Rigidbody body, Dictionary<Transform, float> rawMasses)
        {
            float mass;
            if (body != null && rawMasses.TryGetValue(body.transform, out mass))
            {
                return mass;
            }
            return body != null ? body.mass : 0f;
        }

        private static void GetSpringParameters(Rigidbody body, Rigidbody connectedBody, RoverPortJointData data,
            Dictionary<Transform, float> rawMasses, out float stiffness, out float damping)
        {
            float inverseMass = (body == null || body.isKinematic ? 0f : 1f / RawMass(body, rawMasses)) +
                (connectedBody == null || connectedBody.isKinematic ? 0f : 1f / RawMass(connectedBody, rawMasses));
            float mass = inverseMass > 0f ? 1f / inverseMass : 0f;
            float omega = 2f * Mathf.PI * Mathf.Max(0f, data.frequency);
            stiffness = mass * omega * omega;
            damping = 2f * mass * Mathf.Max(0f, data.damping_ratio) * omega;
        }

        private static void CreateSpringJoint(Rigidbody body, Rigidbody connectedBody, RoverPortJointData data, Dictionary<Transform, float> rawMasses)
        {
            SpringJoint joint = body.gameObject.AddComponent<SpringJoint>();
            joint.connectedBody = connectedBody;
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = ToVector3(data.anchor);
            joint.connectedAnchor = ToVector3(data.connected_anchor);
            joint.enableCollision = false;
            joint.enablePreprocessing = false;
            float distance = data.auto_configure_distance
                ? Vector3.Distance(body.transform.TransformPoint(joint.anchor), connectedBody.transform.TransformPoint(joint.connectedAnchor))
                : Mathf.Max(0f, data.distance);
            joint.minDistance = distance;
            joint.maxDistance = distance;
            joint.tolerance = 0.001f;
            float stiffness;
            float damping;
            GetSpringParameters(body, connectedBody, data, rawMasses, out stiffness, out damping);
            joint.spring = stiffness;
            joint.damper = damping;
        }

        private static void CreateDistanceJoint(Rigidbody body, Rigidbody connectedBody, RoverPortJointData data)
        {
            SpringJoint joint = body.gameObject.AddComponent<SpringJoint>();
            joint.connectedBody = connectedBody;
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = ToVector3(data.anchor);
            joint.connectedAnchor = ToVector3(data.connected_anchor);
            joint.enableCollision = false;
            joint.enablePreprocessing = false;
            float distance = Mathf.Max(0f, data.distance);
            joint.minDistance = data.max_distance_only ? 0f : distance;
            joint.maxDistance = distance;
            joint.spring = 10000f;
            joint.damper = 100f;
        }

        private Rigidbody FindRuntimeBody(Transform node)
        {
            Rigidbody body = node.GetComponent<Rigidbody>();
            return body != null ? body : node.GetComponentInParent<Rigidbody>();
        }

        private static RigidbodyConstraints ConvertConstraints(int constraints)
        {
            RigidbodyConstraints result = RigidbodyConstraints.FreezePositionZ |
                RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
            if ((constraints & 1) != 0)
            {
                result |= RigidbodyConstraints.FreezePositionX;
            }
            if ((constraints & 2) != 0)
            {
                result |= RigidbodyConstraints.FreezePositionY;
            }
            if ((constraints & 4) != 0)
            {
                result |= RigidbodyConstraints.FreezeRotation;
            }
            return result;
        }

        private static Vector3 ToVector3(float[] value)
        {
            if (value == null || value.Length < 2)
            {
                return Vector3.zero;
            }
            return new Vector3(value[0], value[1], value.Length > 2 ? value[2] : 0f);
        }

        private static bool IsStructuralPart(RoverPortPartData data)
        {
            return string.Equals(data.category, "frame", StringComparison.Ordinal) ||
                   string.Equals(data.category, "cockpit", StringComparison.Ordinal) ||
                   string.Equals(data.category, "package", StringComparison.Ordinal) ||
                   string.Equals(data.category, "gyroscope", StringComparison.Ordinal);
        }

        private void IgnoreInternalCollisions()
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                for (int j = i + 1; j < colliders.Length; j++)
                {
                    if (colliders[i] != null && colliders[j] != null)
                    {
                        GamePhysics.IgnoreCollision(colliders[i], colliders[j], true);
                    }
                }
            }
        }

        private void NormalizeBodyMasses()
        {
            Rigidbody rootBody = GetComponent<Rigidbody>();
            Rigidbody[] bodies = GetComponentsInChildren<Rigidbody>(true);
            // The source keeps its broken-off debris (the inactive Broken* containers) dormant until
            // the part breaks, so those bodies must not dilute the scale: normalizing over all of
            // them left the live assembly at a fraction of the catalog mass (the track's dormant
            // roller mounts alone are 8 of its 14.4 mass units). Dormant bodies keep their source
            // mass, exactly like the original.
            float liveTotal = 0f;
            foreach (Rigidbody body in bodies)
            {
                if (body != null && IsLocallyActive(body.transform))
                {
                    liveTotal += Mathf.Max(0.01f, body.mass);
                }
            }
            if (liveTotal <= 0f)
            {
                rootBody.mass = Mathf.Max(0.01f, m_mass);
                return;
            }
            float scale = Mathf.Max(0.01f, m_mass) / liveTotal;
            foreach (Rigidbody body in bodies)
            {
                if (body != null && IsLocallyActive(body.transform))
                {
                    body.mass = Mathf.Max(0.01f, body.mass * scale);
                }
            }
        }

        public override int GetRotation()
        {
            return (int)m_gridRotation;
        }

        public override void SetRotation(GridRotation rotation)
        {
            SetRotation((int)rotation);
        }

        public override void SetRotation(int rotation)
        {
            int normalized = rotation % 8;
            if (normalized < 0)
            {
                normalized += 8;
            }
            FineRotate.OnDiscreteRotation(this, m_gridRotation, (GridRotation)normalized);
            m_gridRotation = (GridRotation)normalized;
            ApplyRotation();
        }

        public override void SetFlipped(bool flipped)
        {
            bool changed = m_flipped != flipped;
            m_flipped = flipped;
            ApplyRotation();
            if (changed)
            {
                OnFlipped();
            }
        }

        private void ApplyRotation()
        {
            float angle = GetIntegralAngle(m_gridRotation);
            bool quarterTurn = m_gridRotation == GridRotation.Deg_90 || m_gridRotation == GridRotation.Deg_270;
            float xRotation = m_flipped && quarterTurn ? 180f : 0f;
            float yRotation = m_flipped && !quarterTurn ? 180f : 0f;
            Quaternion target = Quaternion.Euler(xRotation, yRotation, angle);
            // The footprint has to be measured at the final angle, then the part animates towards it.
            Quaternion previous = transform.localRotation;
            float buildAngle = FineRotate.GetAngle(this, angle) - angle + PartPhysicsParameters.Rotation(this);
            transform.localRotation = Quaternion.AngleAxis(buildAngle, Vector3.forward) * target;
            RecalculateGridFootprint();
            transform.localRotation = previous;
            ApplyBuildRotation(target);
        }

        /// <summary>
        /// Every ported part can be carried inside a frame - the game's wooden and metal frames as
        /// well as the ported cockpit (the one part that is itself a frame here, see
        /// <see cref="RoverPortCockpitPart"/>). The game's INFeature.EnclosableParts switch only gates
        /// the game's own parts, so the ported parts do not depend on it.
        /// </summary>
        public override bool CanBeEnclosed()
        {
            return true;
        }

        public override bool ValidatePart()
        {
            return true;
        }

        public override Direction EffectDirection()
        {
            return Direction.Right;
        }

        private void CacheNodeReferences(Dictionary<string, Transform> nodes)
        {
            m_nodes.Clear();
            m_nodeRefs.Clear();
            foreach (KeyValuePair<string, Transform> pair in nodes)
            {
                m_nodes[pair.Key] = pair.Value;
                m_nodeRefs.Add(new RoverPortNodeRef
                {
                    id = pair.Key,
                    node = pair.Value
                });
            }
        }

        private void EnsureRuntimeData()
        {
            if (string.IsNullOrEmpty(PortId) && m_portData != null)
            {
                PortId = m_portData.id;
                DisplayName = m_portData.display_name;
                Description = m_portData.description;
            }
            if (PortIcon == null && m_constructionIconSprite != null)
            {
                PortIcon = m_constructionIconSprite as RoverPortIconSprite;
            }
            if (m_nodes.Count > 0)
            {
                return;
            }
            if (m_nodeRefs == null)
            {
                m_nodeRefs = new List<RoverPortNodeRef>();
            }
            for (int i = 0; i < m_nodeRefs.Count; i++)
            {
                RoverPortNodeRef reference = m_nodeRefs[i];
                if (reference != null && !string.IsNullOrEmpty(reference.id) && reference.node != null)
                {
                    m_nodes[reference.id] = reference.node;
                }
            }
        }

        protected Transform FindNode(string id)
        {
            EnsureRuntimeData();
            Transform result;
            return !string.IsNullOrEmpty(id) && m_nodes.TryGetValue(id, out result) ? result : null;
        }

        protected Rigidbody FindBody(string id)
        {
            Transform node = FindNode(id);
            if (node == null)
            {
                return null;
            }
            Rigidbody body = node.GetComponent<Rigidbody>();
            return body != null ? body : node.GetComponentInParent<Rigidbody>();
        }

        private void RecalculateGridFootprint()
        {
            // The catalog's build grid is authoritative when it is present: it is
            // the source game's own layout (`Ci`), so a part can never claim more
            // cells than it was authored for. The art is only a fallback for the
            // parts the source publishes no layout for (the eighteen RC3 drivers
            // and the RCR2 parts whose grid the extractor could not recover).
            if (TryApplyCatalogGrid())
            {
                return;
            }
            Bounds? localBounds = null;
            foreach (MeshRenderer renderer in GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer == null || !renderer.enabled || !IsLocallyActive(renderer.transform))
                {
                    continue;
                }
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    EncapsulateLocalBounds(filter.sharedMesh.bounds, renderer.transform, ref localBounds);
                }
            }
            if (!localBounds.HasValue)
            {
                m_gridXmin = 0;
                m_gridXmax = 0;
                m_gridYmin = 0;
                m_gridYmax = 0;
                return;
            }

            Bounds bounds = localBounds.Value;
            const float edgeTolerance = 0.001f;
            m_gridXmin = Mathf.CeilToInt(bounds.min.x + edgeTolerance);
            m_gridXmax = Mathf.FloorToInt(bounds.max.x - edgeTolerance);
            m_gridYmin = Mathf.CeilToInt(bounds.min.y + edgeTolerance);
            m_gridYmax = Mathf.FloorToInt(bounds.max.y - edgeTolerance);
            if (m_gridXmin > m_gridXmax)
            {
                m_gridXmin = m_gridXmax = 0;
            }
            if (m_gridYmin > m_gridYmax)
            {
                m_gridYmin = m_gridYmax = 0;
            }
        }

        /// <summary>
        /// Writes the catalog's cell rectangle onto the part, rotated with it.
        /// Returns false when the part carries no *authoritative* grid, in which
        /// case the caller falls back to measuring the artwork.
        ///
        /// This replaces the art-bounds measurement for every ported part whose
        /// source layout is known. Measuring the art is what let ported parts
        /// overhang their cells: RC3 art is drawn larger than the cell it belongs
        /// to, and a quarter turn made the mismatch worse (the bounds were
        /// measured at the new angle but against a rectangle that no longer
        /// matched the cells the part occupies).
        ///
        /// The RCR2 catalog predates the flag and keeps measuring its artwork,
        /// which is what its own alignment data was recovered from - so only the
        /// parts that opt in change behaviour.
        /// </summary>
        private bool TryApplyCatalogGrid()
        {
            RoverPortGridData grid = m_portData == null ? null : m_portData.grid;
            if (grid == null || !grid.authoritative || grid.cells == null)
            {
                return false;
            }
            int xmin = grid.cells.xmin;
            int xmax = grid.cells.xmax;
            int ymin = grid.cells.ymin;
            int ymax = grid.cells.ymax;
            if (xmax < xmin || ymax < ymin)
            {
                return false;
            }
            // A quarter turn swaps the footprint's axes about the part origin.
            // This mirrors `RoverPortGridAudit.RotateCells` exactly - the audit
            // asserts the runtime footprint equals the rotated source cells, so
            // the two must stay in lockstep.
            switch ((int)m_gridRotation & 3)
            {
                case 1:
                    int x0 = -ymax, x1 = -ymin, y0 = xmin, y1 = xmax;
                    xmin = x0; xmax = x1; ymin = y0; ymax = y1;
                    break;
                case 2:
                    int x2 = -xmax, x3 = -xmin, y2 = -ymax, y3 = -ymin;
                    xmin = x2; xmax = x3; ymin = y2; ymax = y3;
                    break;
                case 3:
                    int x4 = ymin, x5 = ymax, y4 = -xmax, y5 = -xmin;
                    xmin = x4; xmax = x5; ymin = y4; ymax = y5;
                    break;
            }
            m_gridXmin = xmin;
            m_gridXmax = xmax;
            m_gridYmin = ymin;
            m_gridYmax = ymax;
            return true;
        }

        private bool IsLocallyActive(Transform node)
        {
            for (Transform current = node; current != null && current != transform; current = current.parent)
            {
                if (!current.gameObject.activeSelf)
                {
                    return false;
                }
            }
            return true;
        }

        private void EncapsulateLocalBounds(Bounds source, Transform sourceTransform, ref Bounds? accumulated)
        {
            Vector3 center = source.center;
            Vector3 extents = source.extents;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 sourcePoint = center + Vector3.Scale(extents, new Vector3(x, y, z));
                        Vector3 localPoint = sourceTransform.TransformPoint(sourcePoint) - transform.position;
                        if (transform.parent != null)
                            localPoint = transform.parent.InverseTransformVector(localPoint);
                        if (!accumulated.HasValue)
                        {
                            accumulated = new Bounds(localPoint, Vector3.zero);
                        }
                        else
                        {
                            Bounds result = accumulated.Value;
                            result.Encapsulate(localPoint);
                            accumulated = result;
                        }
                    }
                }
            }
        }
    }
}
