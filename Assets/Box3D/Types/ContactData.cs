using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Box3D
{
    /// <summary>One point of a contact manifold. Mirrors native b3ManifoldPoint. Positions are given
    /// relative to each body's center of mass (a world-space offset), so the absolute world contact
    /// point is <c>ContactData.ShapeA.GetBody()</c>'s world center of mass + <see cref="AnchorA"/>
    /// (box3d — not the caller — decides which shape is A, so it may be the other body). Equivalently
    /// use <see cref="AnchorB"/> with shape B's body.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ManifoldPoint
    {
        /// <summary>Contact point relative to body A's center of mass, in world space.</summary>
        public float3 AnchorA;

        /// <summary>Contact point relative to body B's center of mass, in world space.</summary>
        public float3 AnchorB;

        /// <summary>Gap between the shapes at this point; negative means penetrating.</summary>
        public float Separation;

        /// <summary>Cached separation used internally for contact recycling.</summary>
        public float BaseSeparation;

        /// <summary>Normal impulse from the final solver sub-step.</summary>
        public float NormalImpulse;

        /// <summary>Normal impulse summed across all sub-steps — the best measure of how hard the
        /// point was pushed this step (use this for impact strength).</summary>
        public float TotalNormalImpulse;

        /// <summary>Relative normal velocity before solving. Negative means the shapes were
        /// approaching; the more negative, the harder the incoming hit.</summary>
        public float NormalVelocity;

        /// <summary>Stable id uniquely identifying this contact point between the two shapes — match
        /// it across steps to track a specific point.</summary>
        public uint FeatureId;

        /// <summary>Triangle index when one shape is a mesh or height field (else undefined) — use it
        /// for per-triangle surface material/audio.</summary>
        public int TriangleIndex;

        /// <summary>Whether this point also existed in the previous step (false = a fresh impact).</summary>
        public NativeBool Persisted;
    }

    /// <summary>A contact manifold — the set of contact points (1 to 4) and shared data for a pair of
    /// touching shapes. Mirrors native b3Manifold; layout-compatible so it is copied by value.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct Manifold
    {
        // Four inline points matching native b3Manifold's fixed b3ManifoldPoint[4]. Private so callers
        // reach them through the indexer bounded by PointCount — the copy fills all four slots, but
        // only the first PointCount hold live data.
        private ManifoldPoint _point0;
        private ManifoldPoint _point1;
        private ManifoldPoint _point2;
        private ManifoldPoint _point3;

        /// <summary>World-space unit normal, pointing from shape A toward shape B.</summary>
        public float3 Normal;

        /// <summary>Central friction angular impulse, applied about the normal.</summary>
        public float TwistImpulse;

        /// <summary>Central friction linear impulse (world space).</summary>
        public float3 FrictionImpulse;

        /// <summary>Rolling resistance angular impulse.</summary>
        public float3 RollingImpulse;

        /// <summary>Number of valid points (1 to 4). Only indices below this are meaningful.</summary>
        public int PointCount;

        /// <summary>The valid points; index in [0, <see cref="PointCount"/>).</summary>
        public ManifoldPoint this[int index]
        {
            get
            {
                if ((uint)index >= (uint)PointCount)
                {
                    throw new IndexOutOfRangeException($"index {index} out of range for PointCount {PointCount}");
                }
                switch (index)
                {
                    case 0: return _point0;
                    case 1: return _point1;
                    case 2: return _point2;
                    default: return _point3;
                }
            }
        }
    }

    /// <summary>A snapshot of one live contact between two shapes: the shapes and their contact
    /// manifold(s). Returned by <see cref="Contact.GetData"/>, <see cref="Body.GetContacts"/> and
    /// <see cref="Shape.GetContacts"/>.
    ///
    /// <para>In the raw C API, b3ContactData exposes the manifolds through a pointer into internal
    /// engine memory that <b>may become invalid — you must not store it</b>. This wrapper copies the
    /// manifold data out into managed memory at the moment you call the accessor, so the ContactData
    /// you receive is a safe, self-contained snapshot you can keep. The values still reflect the state
    /// at the last <c>World.Step</c>; call again after stepping for fresh data.</para></summary>
    public struct ContactData
    {
        /// <summary>The first shape in the contact.</summary>
        public Shape ShapeA;

        /// <summary>The second shape in the contact.</summary>
        public Shape ShapeB;

        /// <summary>The contact manifolds (copied). Usually one; mesh and height-field collisions can
        /// produce several.</summary>
        public Manifold[] Manifolds;

        // Copies the transient native manifold pointer into managed memory immediately (the pointer
        // must not outlive this call). Manifold/ManifoldPoint are layout-identical to their native
        // mirrors, so each manifold is a plain value copy.
        internal static unsafe ContactData FromNative(in b3ContactData native)
        {
            int count = native.manifoldCount;
            Manifold[] manifolds = count > 0 ? new Manifold[count] : Array.Empty<Manifold>();
            for (int i = 0; i < count; i++)
            {
                manifolds[i] = *(Manifold*)(native.manifolds + i);
            }

            return new ContactData
            {
                ShapeA = new Shape { Id = native.shapeIdA },
                ShapeB = new Shape { Id = native.shapeIdB },
                Manifolds = manifolds,
            };
        }
    }
}
