using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RCR2Port
{
    /// <summary>
    /// Rubber band of a tracked part.
    ///
    /// RCR2 builds it at runtime: the belt link sprite (part_sand_wheel_track_lvla_track, a 22x8 px
    /// tread block) is referenced by no prefab at all, and the "TrackRenderer" node is only a host for
    /// the generated mesh. The belt loop is the chain of hinge linked bodies of the part
    /// (motion.wheel_bodies), so the port draws one link quad per link length along that loop and
    /// scrolls the links with the part's speed, which is what makes a track look like a track.
    ///
    /// The band is independent of the construction/physics renderer switching, so it is visible in the
    /// build view and while driving.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class RoverPortTrackBand : MonoBehaviour
    {
        private const int MaxLinks = 512;

        // Serialized so the links survive Instantiate when the part is placed on a contraption.
        [SerializeField] private List<Transform> m_links = new List<Transform>();
        [SerializeField] private RoverPortSpriteData m_sprite;
        [SerializeField] private float[] m_radii;
        private Rigidbody m_rootBody;
        private Mesh m_mesh;
        private Material m_material;
        private readonly List<Vector3> m_points = new List<Vector3>();
        private readonly List<Vector3> m_vertices = new List<Vector3>();
        private readonly List<Vector2> m_uvs = new List<Vector2>();
        private readonly List<int> m_triangles = new List<int>();
        private float m_thickness = 0.08f;
        private float m_linkLength = 0.22f;
        private float m_loopLength;
        private float m_scroll;

        public void Configure(
            RoverPortBandData data,
            Dictionary<string, Transform> nodes,
            Texture2D texture,
            Material template)
        {
            if (data == null || data.sprite == null || texture == null || template == null)
            {
                enabled = false;
                return;
            }
            m_sprite = data.sprite;
            m_linkLength = Mathf.Max(0.01f, data.link_length);
            m_thickness = Mathf.Max(0.01f, data.thickness);
            m_radii = data.radii;
            m_links.Clear();
            foreach (string id in data.nodes ?? new string[0])
            {
                Transform node;
                if (nodes.TryGetValue(id, out node) && node != null)
                {
                    m_links.Add(node);
                }
            }
            if (m_links.Count < 3)
            {
                enabled = false;
                return;
            }

            var filter = GetComponent<MeshFilter>();
            var meshRenderer = GetComponent<MeshRenderer>();
            m_mesh = new Mesh { name = "RCR2_TrackBand", hideFlags = HideFlags.DontSave };
            m_mesh.MarkDynamic();
            filter.sharedMesh = m_mesh;
            m_material = new Material(template) { hideFlags = HideFlags.DontSave };
            m_material.mainTexture = texture;
            meshRenderer.sharedMaterial = m_material;
            meshRenderer.sortingOrder = data.sorting_order;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            m_rootBody = GetComponentInParent<Rigidbody>();
            m_loopLength = MeasureLoop();
            // Edit mode never runs LateUpdate, so the first band is built here; the build view and the
            // editor then show the track as well.
            Build();
        }

        private void LateUpdate()
        {
            if (m_mesh == null || m_links.Count < 3)
            {
                return;
            }
            m_scroll += TravelSpeed() * Time.deltaTime;
            Build();
        }

        private void OnDestroy()
        {
            if (m_mesh != null)
            {
                DestroyImmediate(m_mesh);
                m_mesh = null;
            }
            if (m_material != null)
            {
                DestroyImmediate(m_material);
                m_material = null;
            }
        }

        /// <summary>How fast the belt travels: the part moving along its own right axis.</summary>
        private float TravelSpeed()
        {
            if (m_rootBody == null)
            {
                m_rootBody = GetComponentInParent<Rigidbody>();
            }
            if (m_rootBody == null)
            {
                return 0f;
            }
            Vector3 velocity = m_rootBody.velocity;
            velocity.z = 0f;
            return Vector3.Dot(velocity, transform.right);
        }

        /// <summary>
        /// Belt centre line: the chain nodes sit on the roller centres, so every point is pushed out
        /// to the roller surface (its collider radius) plus half the belt thickness. That is where a
        /// real belt runs, and it keeps the belt outside the rollers instead of across their hubs.
        /// </summary>
        private void CollectPoints()
        {
            m_points.Clear();
            int count = m_links.Count;
            var raw = new List<Vector3>(count);
            float centreX = 0f;
            float centreY = 0f;
            foreach (Transform link in m_links)
            {
                Vector3 local = transform.InverseTransformPoint(link.position);
                raw.Add(local);
                centreX += local.x;
                centreY += local.y;
            }
            centreX /= count;
            centreY /= count;
            for (int i = 0; i < count; i++)
            {
                Vector3 point = raw[i];
                float radius = m_radii != null && i < m_radii.Length ? m_radii[i] : 0.15f;
                var outward = new Vector3(point.x - centreX, point.y - centreY, 0f);
                if (outward.sqrMagnitude < 0.000001f)
                {
                    outward = Vector3.down;
                }
                outward = outward.normalized * (radius + m_thickness * 0.5f);
                m_points.Add(point + outward);
            }
        }

        private float MeasureLoop()
        {
            CollectPoints();
            float length = 0f;
            for (int i = 0; i < m_points.Count; i++)
            {
                length += Vector3.Distance(m_points[i], m_points[(i + 1) % m_points.Count]);
            }
            return length;
        }

        private Vector3 Sample(float distance, out Vector3 tangent)
        {
            int count = m_points.Count;
            float remaining = Mathf.Repeat(distance, Mathf.Max(0.01f, m_loopLength));
            for (int i = 0; i < count; i++)
            {
                Vector3 start = m_points[i];
                Vector3 end = m_points[(i + 1) % count];
                Vector3 segment = end - start;
                float length = segment.magnitude;
                if (length <= 0.0001f)
                {
                    continue;
                }
                if (remaining <= length)
                {
                    tangent = segment / length;
                    return start + tangent * remaining;
                }
                remaining -= length;
            }
            tangent = Vector3.right;
            return m_points[0];
        }

        private void Build()
        {
            if (m_mesh == null)
            {
                return;
            }
            CollectPoints();
            if (Mathf.Approximately(m_loopLength, 0f))
            {
                m_loopLength = MeasureLoop();
            }
            int links = Mathf.Clamp(Mathf.RoundToInt(m_loopLength / Mathf.Max(0.01f, m_linkLength)), 3, MaxLinks);
            float spacing = m_loopLength / links;
            float half = spacing * 0.5f;
            float halfThickness = m_thickness * 0.5f;

            float textureWidth = Mathf.Max(1, m_sprite.texture_width);
            float textureHeight = Mathf.Max(1, m_sprite.texture_height);
            float uMin = m_sprite.x / textureWidth;
            float uMax = (m_sprite.x + m_sprite.width) / textureWidth;
            float vMin = m_sprite.y / textureHeight;
            float vMax = (m_sprite.y + m_sprite.height) / textureHeight;

            m_vertices.Clear();
            m_uvs.Clear();
            m_triangles.Clear();
            for (int i = 0; i < links; i++)
            {
                Vector3 tangent;
                Vector3 center = Sample(m_scroll + i * spacing, out tangent);
                Vector3 normal = new Vector3(-tangent.y, tangent.x, 0f);
                Vector3 along = tangent * half;
                Vector3 across = normal * halfThickness;
                int baseIndex = m_vertices.Count;
                m_vertices.Add(center - along - across);
                m_vertices.Add(center - along + across);
                m_vertices.Add(center + along + across);
                m_vertices.Add(center + along - across);
                m_uvs.Add(new Vector2(uMin, vMin));
                m_uvs.Add(new Vector2(uMin, vMax));
                m_uvs.Add(new Vector2(uMax, vMax));
                m_uvs.Add(new Vector2(uMax, vMin));
                m_triangles.Add(baseIndex);
                m_triangles.Add(baseIndex + 1);
                m_triangles.Add(baseIndex + 2);
                m_triangles.Add(baseIndex + 2);
                m_triangles.Add(baseIndex + 3);
                m_triangles.Add(baseIndex);
            }

            m_mesh.Clear();
            m_mesh.SetVertices(m_vertices);
            m_mesh.SetUVs(0, m_uvs);
            m_mesh.SetTriangles(m_triangles, 0);
            m_mesh.RecalculateBounds();
        }
    }
}
