using UnityEngine;

namespace RCR2Port
{
    /// <summary>
    /// The run-time body split of the imported Android snowmobile ski (RC1 part
    /// <c>AndroidSki</c> / <c>part_rc1_1031</c>), reproducing MAIN's
    /// <c>AndroidSkiMotion</c>.
    ///
    /// MAIN's source part is not one rigid body. <c>AndroidSkiMotion</c> says it in one line:
    /// "a spring-guided support carries a separate board which is hinged at
    /// <c>WheelAttachPoint</c> instead of being one rigid body". While the contraption runs it
    /// takes the part apart into
    ///
    ///  * the <b>support</b> (<c>L1_wheel_bg</c>, the visible suspension bracket, mass 0.55) with
    ///    its own rigidbody, slid onto the part root by a <c>ConfigurableJoint</c> whose axis is
    ///    tilted 72.8 degrees off horizontal and whose spring (110, damper 18) lets it travel
    ///    0.10 m along that axis and nowhere else;
    ///  * the <b>board</b> (<c>wheel</c>, the ski itself, mass 0.65) with its own rigidbody,
    ///    hinged to the support at <c>WheelAttachPoint</c> with +/-30 degrees of pitch;
    ///
    /// and reparents the bracket artwork (<c>L1_wheel_e</c>) onto the support so it follows the
    /// slider. The port used to build the whole part as one rigidbody welded to the frame, so the
    /// board was bound rigidly to its own base and the ski could neither travel nor pitch - the
    /// support and the board moved as a single block.
    ///
    /// The two colliders and their friction are <i>not</i> touched here: the catalog already
    /// builds MAIN's own volumes, support box included, with the source's
    /// <c>AndroidSkiSupportPhysicsMaterial</c> / <c>AndroidSkiBoardPhysicsMaterial</c> (see the
    /// collider contract in Tools/RC1/BuildRC1Catalog.ps1). MAIN assigns its board material at
    /// run time only because its board collider starts life as a converted 2D polygon.
    ///
    /// Two deliberate departures from MAIN, both forced by the port's architecture:
    ///
    /// 1. Only the physics (<c>PE:</c>) nodes are split. The port keeps a second, parallel tree of
    ///    construction artwork (<c>ME:</c>) for the build view and disables it while running
    ///    (<see cref="RoverPortPart.EnsureRigidbody"/>); splitting it too would leave an invisible
    ///    second ski inside the vehicle.
    /// 2. The nodes are found by the catalog's own node ids rather than by walking
    ///    <c>AndroidSource</c> for a name, because the port's tree is named by the catalog. The
    ///    generator asserts that all three lookups resolve, so a rename breaks the build instead
    ///    of silently leaving the ski rigid.
    ///
    /// One consequence worth naming, because it looks like a regression and is not: after the
    /// split, the support and the board are root objects, so they are no longer children of the
    /// part. <see cref="RoverPortContraptionCollision"/> (the port's blanket "a ported wheel
    /// ignores its own vehicle" rule, which <see cref="RoverPortPart.AttachWheelMotion"/> puts on
    /// every wheel) re-reads its colliders with <c>GetComponentsInChildren</c> every step, so from
    /// that moment it no longer covers them and the ski can touch its own vehicle again. That is
    /// what MAIN does: the ski is a <c>MotorWheel</c>, which has no ignore loop at all - only
    /// <c>OffRoadWheel</c> and <c>AndroidTrackMotion</c> have one - and MAIN detaches the same two
    /// bodies. The board never collides with its own support either way
    /// (<see cref="IgnoreBoardSupportContacts"/> here, and <c>RoverPortPart.IgnoreInternalCollisions</c>
    /// before that).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoverPortSkiMotion : MonoBehaviour
    {
        /// <summary>The imported Android class this component belongs to. Every other ported part is left alone.</summary>
        public const string AndroidSkiClass = "AndroidSki";

        /// <summary>MAIN's own node names, and the node the bracket artwork is reparented onto.</summary>
        public const string SupportNodeName = "L1_wheel_bg";
        public const string BoardNodeName = "wheel";
        public const string PivotNodeName = "WheelAttachPoint";
        public const string BracketNodeName = "L1_wheel_e";

        private const float SupportMass = 0.55f;
        private const float SupportDrag = 0.15f;
        private const float SupportAngularDrag = 0.8f;
        private const float BoardMass = 0.65f;
        private const float BoardDrag = 0.1f;
        private const float BoardAngularDrag = 0.65f;
        private const float MaxDepenetrationVelocity = 1.5f;
        /// <summary>MAIN tilts the slider axis 72.8 degrees about Z (AndroidSkiMotion.cs:110).</summary>
        private const float SliderAngle = -72.8f;
        private const float SliderLimit = 0.10f;
        private const float SliderSpring = 110f;
        private const float SliderDamper = 18f;
        /// <summary>MAIN's hinge limit, +/- this many degrees about the board's forward axis.</summary>
        private const float HingeLimit = 30f;

        [SerializeField] private string m_supportNode;
        [SerializeField] private string m_boardNode;
        [SerializeField] private string m_pivotNode;
        [SerializeField] private string m_bracketNode;

        private RoverPortPart m_owner;
        private Transform m_support;
        private Transform m_board;
        private Transform m_pivot;
        private Rigidbody m_rootBody;
        private Rigidbody m_supportBody;
        private Rigidbody m_boardBody;
        private bool m_initialized;

        /// <summary>True once the ski has been split into the support/board bodies.</summary>
        public bool IsSplit { get { return m_initialized; } }

        /// <summary>Support body, or null before the split; the diagnostics read it.</summary>
        public Rigidbody SupportBody { get { return m_supportBody; } }

        /// <summary>Board body, or null before the split; the diagnostics read it.</summary>
        public Rigidbody BoardBody { get { return m_boardBody; } }

        /// <summary>
        /// Adds the component to a ported part, but only when the part really is the imported
        /// ski and its catalog record carries the three nodes the split needs. Called from
        /// <see cref="RoverPortPart.EnsureRigidbody"/> on every ported part.
        /// </summary>
        public static void Attach(RoverPortPart part, RoverPortPartData data)
        {
            if (part == null || data == null ||
                !string.Equals(data.android_class, AndroidSkiClass, System.StringComparison.Ordinal))
            {
                return;
            }
            string support = FindPhysicsNode(data, SupportNodeName);
            string board = FindPhysicsNode(data, BoardNodeName);
            string pivot = FindPhysicsNode(data, PivotNodeName);
            if (support == null || board == null || pivot == null)
            {
                Debug.LogWarning("[RoverPort] " + data.id + " is an " + AndroidSkiClass +
                    " but its record is missing one of " + SupportNodeName + " / " + BoardNodeName +
                    " / " + PivotNodeName + "; the ski will stay a single rigid body.");
                return;
            }
            RoverPortSkiMotion motion = part.GetComponent<RoverPortSkiMotion>();
            if (motion == null)
            {
                motion = part.gameObject.AddComponent<RoverPortSkiMotion>();
            }
            motion.Configure(part, support, board, pivot, FindPhysicsNode(data, BracketNodeName));
        }

        private static string FindPhysicsNode(RoverPortPartData data, string name)
        {
            RoverPortNodeData[] nodes = data.nodes ?? new RoverPortNodeData[0];
            for (int i = 0; i < nodes.Length; i++)
            {
                RoverPortNodeData node = nodes[i];
                if (node == null || node.id == null || node.name == null)
                {
                    continue;
                }
                if (node.id.StartsWith("PE:", System.StringComparison.Ordinal) &&
                    string.Equals(node.name, name, System.StringComparison.Ordinal))
                {
                    return node.id;
                }
            }
            return null;
        }

        private void Configure(RoverPortPart owner, string supportNode, string boardNode, string pivotNode, string bracketNode)
        {
            m_owner = owner;
            m_supportNode = supportNode;
            m_boardNode = boardNode;
            m_pivotNode = pivotNode;
            m_bracketNode = bracketNode;
        }

        /// <summary>
        /// MAIN does the split from <c>Start</c> and keeps retrying from <c>Update</c> until the
        /// contraption is running, because the construction prototype is the very same object -
        /// "do not detach the support/board or add dynamic bodies while the construction prototype
        /// is active; that would leave a second physical ski beside the hidden preview when Run is
        /// pressed" (AndroidSkiMotion.cs:47-51). The port's parts are built the same way and
        /// <c>EnsureRigidbody</c> runs before the contraption is running, so the same retry is
        /// needed here.
        /// </summary>
        private void Update()
        {
            if (m_initialized)
            {
                return;
            }
            // A placed part is a clone of the catalog template, and a component reference is not
            // serialized: re-resolve the same way RoverPortWheelSpin does.
            if (m_owner == null)
            {
                m_owner = GetComponent<RoverPortPart>();
                m_support = null;
                m_board = null;
                m_pivot = null;
                m_rootBody = null;
                m_supportBody = null;
                m_boardBody = null;
                if (m_owner == null)
                {
                    return;
                }
            }
            Contraption owner = m_owner.contraption;
            if (owner == null || !owner.IsRunning)
            {
                return;
            }
            TryInitialize();
        }

        private void TryInitialize()
        {
            if (m_support == null) m_support = m_owner.ResolvePortNode(m_supportNode);
            if (m_board == null) m_board = m_owner.ResolvePortNode(m_boardNode);
            if (m_pivot == null) m_pivot = m_owner.ResolvePortNode(m_pivotNode);
            if (m_rootBody == null) m_rootBody = m_owner.GetComponent<Rigidbody>();
            if (m_rootBody == null || m_support == null || m_board == null || m_pivot == null)
            {
                return;
            }

            Vector3 pivotWorld = m_pivot.position;
            // "The visible suspension bracket is a sibling of the physics support in the
            // extracted hierarchy. Attach it to the support before the support is detached so
            // its artwork follows the slider exactly."
            Transform bracket = m_bracketNode == null ? null : m_owner.ResolvePortNode(m_bracketNode);
            if (bracket != null && bracket != m_support)
            {
                bracket.SetParent(m_support, true);
            }

            // "Suppress contacts during the reparent operation so the detached support cannot
            // inject an overlap impulse into the running contraption."
            SetCollidersEnabled(m_support, false);
            m_supportBody = EnsureBody(m_support, SupportMass, SupportDrag, SupportAngularDrag);
            m_support.SetParent(null, true);
            m_supportBody = m_support.GetComponent<Rigidbody>();
            ConfigurableJoint slider = m_support.GetComponent<ConfigurableJoint>();
            if (slider == null)
            {
                slider = m_support.gameObject.AddComponent<ConfigurableJoint>();
            }
            slider.connectedBody = m_rootBody;
            slider.enableCollision = false;
            slider.autoConfigureConnectedAnchor = false;
            slider.anchor = Vector3.zero;
            slider.connectedAnchor = m_rootBody.transform.InverseTransformPoint(m_support.position);
            slider.xMotion = ConfigurableJointMotion.Limited;
            slider.yMotion = ConfigurableJointMotion.Locked;
            slider.zMotion = ConfigurableJointMotion.Locked;
            slider.angularXMotion = ConfigurableJointMotion.Locked;
            slider.angularYMotion = ConfigurableJointMotion.Locked;
            slider.angularZMotion = ConfigurableJointMotion.Locked;
            slider.axis = Quaternion.Euler(0f, 0f, SliderAngle) * Vector3.right;
            SoftJointLimit limit = slider.linearLimit;
            limit.limit = SliderLimit;
            limit.bounciness = 0f;
            slider.linearLimit = limit;
            SoftJointLimitSpring spring = slider.linearLimitSpring;
            spring.spring = SliderSpring;
            spring.damper = SliderDamper;
            slider.linearLimitSpring = spring;
            slider.enablePreprocessing = false;
            SetCollidersEnabled(m_support, true);

            m_boardBody = EnsureBody(m_board, BoardMass, BoardDrag, BoardAngularDrag);
            SetCollidersEnabled(m_board, false);
            m_board.SetParent(null, true);
            m_boardBody = m_board.GetComponent<Rigidbody>();
            SetCollidersEnabled(m_board, true);

            HingeJoint hinge = m_board.GetComponent<HingeJoint>();
            if (hinge == null)
            {
                hinge = m_board.gameObject.AddComponent<HingeJoint>();
            }
            hinge.connectedBody = m_supportBody;
            hinge.enableCollision = false;
            hinge.autoConfigureConnectedAnchor = false;
            hinge.anchor = m_board.InverseTransformPoint(pivotWorld);
            hinge.connectedAnchor = m_support.InverseTransformPoint(pivotWorld);
            hinge.axis = Vector3.forward;
            hinge.useLimits = true;
            JointLimits angles = hinge.limits;
            angles.min = -HingeLimit;
            angles.max = HingeLimit;
            angles.bounciness = 0f;
            angles.bounceMinVelocity = 0f;
            angles.contactDistance = 1f;
            hinge.limits = angles;
            hinge.enablePreprocessing = false;

            IgnoreBoardSupportContacts();
            m_initialized = true;
        }

        private static Rigidbody EnsureBody(Transform node, float mass, float drag, float angularDrag)
        {
            Rigidbody body = node.GetComponent<Rigidbody>();
            if (body == null)
            {
                body = node.gameObject.AddComponent<Rigidbody>();
            }
            body.mass = mass;
            body.drag = drag;
            body.angularDrag = angularDrag;
            body.useGravity = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.maxDepenetrationVelocity = MaxDepenetrationVelocity;
            return body;
        }

        private void IgnoreBoardSupportContacts()
        {
            Collider[] supportColliders = m_support.GetComponentsInChildren<Collider>(true);
            Collider[] boardColliders = m_board.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < boardColliders.Length; i++)
            {
                for (int j = 0; j < supportColliders.Length; j++)
                {
                    if (boardColliders[i] != null && supportColliders[j] != null)
                    {
                        GamePhysics.IgnoreCollision(boardColliders[i], supportColliders[j], true);
                    }
                }
            }
        }

        /// <summary>
        /// MAIN destroys the two detached nodes when the part goes away, because after
        /// <c>SetParent(null, true)</c> they are root objects and nothing else would ever collect
        /// them - they would stay in the scene as a second, physical ski.
        /// </summary>
        private void OnDestroy()
        {
            if (!m_initialized)
            {
                return;
            }
            if (m_support != null && m_support.parent == null)
            {
                Destroy(m_support.gameObject);
            }
            if (m_board != null && m_board.parent == null)
            {
                Destroy(m_board.gameObject);
            }
        }

        private static void SetCollidersEnabled(Transform node, bool enabled)
        {
            if (node == null)
            {
                return;
            }
            Collider[] colliders = node.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = enabled;
                }
            }
        }
    }
}
