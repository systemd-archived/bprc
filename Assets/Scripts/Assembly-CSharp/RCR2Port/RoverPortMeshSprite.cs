using UnityEngine;
using UnityEngine.Rendering;

namespace RCR2Port
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class RoverPortMeshSprite : MonoBehaviour
    {
        private Mesh m_generatedMesh;
        private MaterialPropertyBlock m_propertyBlock;

        public void Configure(
            Texture2D texture,
            RoverPortSpriteData spriteData,
            Material material,
            Color color,
            bool flipX,
            bool flipY,
            int sortingOrder,
            bool enabled)
        {
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            if (meshFilter == null || meshRenderer == null || texture == null || spriteData == null || material == null)
            {
                if (meshRenderer != null)
                {
                    meshRenderer.enabled = false;
                }
                return;
            }

            m_generatedMesh = BuildMesh(spriteData, flipX, flipY);
            meshFilter.sharedMesh = m_generatedMesh;
            meshRenderer.sharedMaterial = material;
            meshRenderer.sortingOrder = sortingOrder;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.enabled = enabled;

            m_propertyBlock = new MaterialPropertyBlock();
            m_propertyBlock.SetColor("_Color", color);
            meshRenderer.SetPropertyBlock(m_propertyBlock);
        }

        internal static Mesh BuildMesh(RoverPortSpriteData spriteData, bool flipX, bool flipY, float pixelsPerUnit = 0f)
        {
            if (spriteData.vertices != null && spriteData.vertices.Length > 0 &&
                spriteData.uvs != null && spriteData.uvs.Length == spriteData.vertices.Length)
            {
                Mesh sourceMesh = new Mesh { name = "RCR2_Mesh_" + spriteData.name, hideFlags = HideFlags.DontSave };
                Vector3[] vertices = new Vector3[spriteData.vertices.Length];
                Vector2[] uvs = new Vector2[vertices.Length];
                float scale = pixelsPerUnit > 0f ? spriteData.ppu / pixelsPerUnit : 1f;
                for (int i = 0; i < vertices.Length; i++)
                {
                    vertices[i] = new Vector3(spriteData.vertices[i].x * (flipX ? -scale : scale),
                        spriteData.vertices[i].y * (flipY ? -scale : scale), 0f);
                    uvs[i] = new Vector2(spriteData.uvs[i].x, spriteData.uvs[i].y);
                }
                sourceMesh.vertices = vertices;
                sourceMesh.uv = uvs;
                sourceMesh.triangles = spriteData.triangles;
                sourceMesh.RecalculateBounds();
                return sourceMesh;
            }
            float ppu = pixelsPerUnit > 0f ? pixelsPerUnit : Mathf.Max(1f, spriteData.ppu);
            float width = Mathf.Max(1, spriteData.width) / ppu;
            float height = Mathf.Max(1, spriteData.height) / ppu;
            float left = -spriteData.pivot_x * width;
            float bottom = -spriteData.pivot_y * height;
            float right = left + width;
            float top = bottom + height;

            Mesh mesh = new Mesh();
            mesh.name = "RCR2_Mesh_" + spriteData.name;
            mesh.hideFlags = HideFlags.DontSave;
            mesh.vertices = new[]
            {
                new Vector3(left, bottom, 0f),
                new Vector3(left, top, 0f),
                new Vector3(right, top, 0f),
                new Vector3(right, bottom, 0f)
            };

            float uMin = spriteData.x / (float)Mathf.Max(1, spriteData.texture_width);
            float uMax = (spriteData.x + spriteData.width) / (float)Mathf.Max(1, spriteData.texture_width);
            float uvY = spriteData.y;
            float vMin = uvY / (float)Mathf.Max(1, spriteData.texture_height);
            float vMax = (uvY + spriteData.height) / (float)Mathf.Max(1, spriteData.texture_height);
            float uLeft = flipX ? uMax : uMin;
            float uRight = flipX ? uMin : uMax;
            float vBottom = flipY ? vMax : vMin;
            float vTop = flipY ? vMin : vMax;

            mesh.uv = new[]
            {
                new Vector2(uLeft, vBottom),
                new Vector2(uLeft, vTop),
                new Vector2(uRight, vTop),
                new Vector2(uRight, vBottom)
            };
            mesh.triangles = new[] { 0, 1, 2, 2, 3, 0 };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
