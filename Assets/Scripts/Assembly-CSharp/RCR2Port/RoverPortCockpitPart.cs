using System.Collections.Generic;
using UnityEngine;

namespace RCR2Port
{
    /// <summary>
    /// A ported RCR2 cockpit behaves like the game's metal frame (RCR2_PORT.md):
    ///
    /// * it is part of the chassis like the game's frames, so wings, propulsion, lights and the build
    ///   UI treat it as structure instead of a loose part;
    /// * it carries one part inside it, welded to the cabin exactly like Frame.Initialize welds the
    ///   part stowed in a metal frame;
    /// * it holds its frame connections at 70% of a metal frame's
    ///   (<see cref="RoverPortConnections.GetJointBreakForce"/>).
    ///
    /// It derives from <see cref="RoverPortBatteryPart"/> because RCR2 puts the driver's onboard
    /// battery in the starter and terra cabins (capacity 100 in the catalog); the other cockpits carry
    /// no charge at all, which is why the battery class reads that from the part data.
    ///
    /// The driver RCR2 spawns in the cabin was removed from the catalog by
    /// Tools/RCR2/PatchCockpitPilot.py, which is what frees the interior.
    /// </summary>
    public class RoverPortCockpitPart : RoverPortBatteryPart
    {
        /// <summary>
        /// The cabin is the frame here, and the game's own frames are AutoAlignType.None
        /// (Part_MetalFrame_01_SET.prefab), so a tap on a cabin must not turn it. Leaving the blanket
        /// AutoAlignType.Rotate of RoverPortPart.Configure in place made the stow click do double duty:
        /// ConstructionUI finds the cabin at its anchor cell (the lower-left one) and calls
        /// Contraption.Flip before the enclosed part is stored, so the cabin turned 90 degrees in the
        /// very frame a pig was placed inside it — and from then on the same click targeted the stowed
        /// pig instead (ConstructionUI swaps to enclosedPart), so the cabin could no longer be turned by
        /// the mouse at all. SetRotation still works, so saved rotations keep loading the same way.
        /// </summary>
        public override void Configure(RoverPortPartData data, int partIndex, Dictionary<string, Transform> nodes, RoverPortIconSprite icon)
        {
            base.Configure(data, partIndex, nodes, icon);
            m_autoAlign = AutoAlignType.None;
        }

        public override bool CanEncloseParts()
        {
            return true;
        }

        /// <summary>The cabin is the frame here, so it never goes inside one.</summary>
        public override bool CanBeEnclosed()
        {
            return false;
        }

        public override bool IsPartOfChassis()
        {
            return true;
        }

        /// <summary>
        /// Welds the part stowed inside the cabin to it, the same way Frame.Initialize welds the part
        /// stowed in a metal frame: the source keeps that joint out of the part's own joint list and
        /// creates it once the contraption starts, when every part has its bodies.
        /// </summary>
        public override void Initialize()
        {
            base.Initialize();
            if (m_enclosedPart == null || m_enclosedPart is Rope || m_enclosedPart is HingePlate || contraption == null)
            {
                return;
            }
            Rigidbody body = GetComponent<Rigidbody>();
            if (body == null)
            {
                return;
            }
            FixedJoint joint = m_enclosedPart.gameObject.AddComponent<FixedJoint>();
            joint.connectedBody = body;
            joint.breakForce = RoverPortConnections.GetJointBreakForce(contraption, this, m_enclosedPart);
            joint.enablePreprocessing = false;
            contraption.AddJointToMap(this, m_enclosedPart, joint);
            // The shell must not push the part stowed inside it around: the cabin is solid geometry
            // around the part, not a wall between them.
            foreach (Collider collider in GetComponentsInChildren<Collider>(true))
            {
                IgnoreCollisionRecursive(collider, m_enclosedPart.gameObject);
            }
        }

        private static void IgnoreCollisionRecursive(Collider collider, GameObject part)
        {
            if (part.activeInHierarchy)
            {
                Collider other = part.GetComponent<Collider>();
                if (other != null)
                {
                    GamePhysics.IgnoreCollision(collider, other);
                }
            }
            for (int i = 0; i < part.transform.childCount; i++)
            {
                IgnoreCollisionRecursive(collider, part.transform.GetChild(i).gameObject);
            }
        }
    }
}
