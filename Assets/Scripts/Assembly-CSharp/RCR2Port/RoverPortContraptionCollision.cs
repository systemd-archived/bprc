using System.Collections.Generic;
using UnityEngine;

namespace RCR2Port
{
    /// <summary>
    /// Keeps a ported wheel from colliding with the rest of its own vehicle, which is what the
    /// source game does.
    ///
    /// MAIN's <c>AndroidTrackMotion.RefreshConnections</c> ignores every other part of the
    /// contraption, every physics step, with its own comment saying why: "the whole vehicle shares
    /// one physics space: the track and the other parts of the same vehicle (frame, ski, cargo
    /// box...) never collide; otherwise the track's top plate keeps squeezing the neighbouring
    /// parts and the solver depenetrates every step, which shows up as the whole track jittering".
    /// <c>OffRoadWheel.PostInitialize</c> does the same for an off-road wheel: with
    /// <c>INFeature.NonCollisionOffRoadWheel</c> on it ignores every part, and by default it
    /// ignores the same-connected-component neighbours inside a 3x2 cell window around itself.
    /// <c>OffRoadWheel.Update</c> then re-enables a part that left the connected component.
    ///
    /// None of that survived the port. <see cref="RoverPortPart.IgnoreInternalCollisions"/> only
    /// ignores the colliders *within one part*, <c>Contraption.AddFixedJoint</c> never calls
    /// <c>IgnoreCollision</c> (a FixedJoint has no <c>enableCollision</c>), and the catalog's
    /// <c>enable_collision = false</c> is only ever applied to the joints the RCR2 catalog ships -
    /// the RC1 records carry no joints at all. The RC1 wheels are wide (the bigfoot wheel's
    /// SphereCollider has radius 1.18 on a one-cell part, the track's top plate reaches 0.66
    /// against a 0.5 cell edge), so they bury themselves in whatever is mounted next to them.
    ///
    /// This component is the missing half of the source's behaviour, attached by
    /// <c>RoverPortPart.AttachWheelMotion</c> to OffRoadWheel and AndroidTrack parts.
    /// </summary>
    [DefaultExecutionOrder(120)]
    public sealed class RoverPortContraptionCollision : MonoBehaviour
    {
        private readonly List<BasePart> m_ignored = new List<BasePart>();
        private readonly List<Collider> m_own = new List<Collider>();
        private readonly List<Collider> m_contacts = new List<Collider>();
        private RoverPortPart m_part;
        private bool m_released;

        /// <summary>Parts this wheel currently has its collisions switched off against.</summary>
        public int IgnoredPartCount { get { return m_ignored.Count; } }

        /// <summary>MAIN's <c>AndroidTrackMotion.PhysicalColliders</c>: every collider of the part.</summary>
        public Collider[] PhysicalColliders
        {
            get { return GetComponentsInChildren<Collider>(true); }
        }

        private void Awake()
        {
            m_part = GetComponent<RoverPortPart>();
        }

        private void OnEnable()
        {
            // MAIN runs this once from OffRoadWheel.PostInitialize before the first step and then
            // re-checks every FixedUpdate (AndroidTrackMotion.RefreshConnections). Both only mean
            // anything while the vehicle is actually running, so skip the build view.
            if (IsRunning())
            {
                Collider[] own = PhysicalColliders;
                for (int i = 0; i < own.Length; i++)
                    for (int j = i + 1; j < own.Length; j++)
                        GamePhysics.IgnoreCollision(own[i], own[j], true);
                Refresh();
            }
        }

        private void FixedUpdate()
        {
            // Same gate as MAIN's AndroidTrackMotion.FixedUpdate.
            if (!IsRunning())
            {
                return;
            }
            Refresh();
        }

        private bool IsRunning()
        {
            if (m_part == null)
            {
                m_part = GetComponent<RoverPortPart>();
            }
            Contraption owner = m_part == null ? null : m_part.contraption;
            return owner != null && owner.IsRunning;
        }

        /// <summary>
        /// Mirrors <c>AndroidTrackMotion.RefreshConnections</c>: drop the parts that left this
        /// contraption, then ignore every part still on it.
        /// </summary>
        public void Refresh()
        {
            if (m_part == null)
            {
                m_part = GetComponent<RoverPortPart>();
            }
            Contraption owner = m_part == null ? null : m_part.contraption;
            if (owner == null)
            {
                ReleaseAll();
                return;
            }
            m_released = false;
            for (int i = m_ignored.Count - 1; i >= 0; i--)
            {
                BasePart other = m_ignored[i];
                if (other == null || other.contraption != owner)
                {
                    SetIgnore(other, false);
                    m_ignored.RemoveAt(i);
                }
            }
            foreach (BasePart other in owner.Parts)
            {
                if (other == null || other == m_part)
                {
                    continue;
                }
                SetIgnore(other, true);
                if (!m_ignored.Contains(other)) m_ignored.Add(other);
            }
        }

        private void ReleaseAll()
        {
            if (m_released || m_ignored.Count == 0)
            {
                return;
            }
            m_released = true;
            foreach (BasePart other in m_ignored)
            {
                SetIgnore(other, false);
            }
            m_ignored.Clear();
        }

        /// <summary>MAIN's <c>AndroidTrackMotion.SetIgnore</c>: the full collider cross product.</summary>
        private void SetIgnore(BasePart other, bool ignore)
        {
            if (m_part == null)
            {
                return;
            }
            m_own.Clear();
            m_part.GetComponentsInChildren(true, m_own);
            if (other == null)
            {
                // Releasing a destroyed part: the colliders are gone with it, nothing to undo.
                return;
            }
            m_contacts.Clear();
            other.GetComponentsInChildren(true, m_contacts);
            for (int a = 0; a < m_own.Count; a++)
            {
                Collider first = m_own[a];
                if (first == null || first.isTrigger)
                {
                    continue;
                }
                for (int b = 0; b < m_contacts.Count; b++)
                {
                    Collider second = m_contacts[b];
                    if (second == null || second.isTrigger || ReferenceEquals(first, second))
                    {
                        continue;
                    }
                    GamePhysics.IgnoreCollision(first, second, ignore);
                }
            }
        }
    }
}
