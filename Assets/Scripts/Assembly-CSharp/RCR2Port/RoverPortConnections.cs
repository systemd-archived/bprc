using UnityEngine;

namespace RCR2Port
{
    public static class RoverPortConnections
    {
        /// <summary>
        /// Kept as the name the ported code already uses; the value lives in
        /// <see cref="PartConnections.AnchorTolerance"/>, which is now the single definition.
        /// </summary>
        public const float AnchorTolerance = PartConnections.AnchorTolerance;

        /// <summary>
        /// A cockpit hangs on the chassis at 70% of a metal frame's hold. The cockpit is multi-cell,
        /// but it gets one joint per neighbour (the first source mount that lines up), so that joint is
        /// a single cell's worth of hold and is deliberately weaker than a metal frame's.
        /// </summary>
        public const float CockpitFrameStrengthScale = 0.7f;

        /// <summary>Parts a cockpit is a frame for: the game's wooden / metal frames (the ported
        /// frames and cockpits are MetalFrame too), the frames the mod adds on top of them, and
        /// the imported RC1 Android frames (AndroidFrame/AndroidTriFrame), which used to be
        /// MetalFrame and now have part types of their own.</summary>
        public static bool IsFramePart(BasePart part)
        {
            return part != null && BasePart.IsFramePart(part.m_partType);
        }

        /// <summary>
        /// Break force of a joint between two placed parts. This is the value the game's
        /// Contraption.AddFixedJoint uses - the sum of both parts' connection strengths - except for a
        /// cockpit to frame connection, which is scaled by <see cref="CockpitFrameStrengthScale"/>.
        /// </summary>
        public static float GetJointBreakForce(Contraption contraption, BasePart first, BasePart second)
        {
            float strength = contraption.GetJointConnectionStrength(first.GetJointConnectionStrength())
                + contraption.GetJointConnectionStrength(second.GetJointConnectionStrength());
            if (!ContraptionLayers.Enabled(contraption) && ((first is RoverPortCockpitPart && IsFramePart(second)) ||
                (second is RoverPortCockpitPart && IsFramePart(first))))
            {
                strength *= CockpitFrameStrengthScale;
            }
            return strength;
        }

        /// <summary>
        /// Anchor and direction for a connection between two parts, or <c>false</c> when their points do
        /// not meet.
        /// </summary>
        /// <remarks>
        /// Delegates to <see cref="PartConnections"/>, which holds the one implementation of the rule.
        /// The matching was generalized to any <see cref="BasePart"/> because the ported parts' channel
        /// and the general one have to agree: two copies of "do these anchors meet" would drift, and the
        /// ported prefabs' spacing is tuned against the shared tolerance. A part that declares no
        /// <c>ConnectionPoints</c> is not matched here, so this returns <c>false</c> for it exactly as
        /// the ported-only version did.
        /// </remarks>
        public static bool TryGetConnection(BasePart first, BasePart second, out Vector3 anchor, out BasePart.Direction direction)
        {
            return PartConnections.TryGetConnection(first, second, out anchor, out direction);
        }
    }
}
