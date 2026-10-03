using UnityEngine;

namespace RCR2Port
{
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshFilter))]
    public sealed class RoverPortIconSprite : Sprite
    {
        // PartSelector applies 1.75x; keep the result within its 2.4 unit button.
        public const float MaximumSize = 1.15f;

        private Mesh m_generatedMesh;

        public void Configure(Texture2D texture, RoverPortSpriteData spriteData, Material material)
        {
            if (texture == null || spriteData == null || material == null)
            {
                return;
            }

            m_generatedMesh = RoverPortMeshSprite.BuildMesh(spriteData, false, false);
            Bounds bounds = m_generatedMesh.bounds;
            float scale = MaximumSize / Mathf.Max(0.001f, bounds.size.x, bounds.size.y);
            Vector3[] vertices = m_generatedMesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = (vertices[i] - bounds.center) * scale;
            }
            m_generatedMesh.vertices = vertices;
            m_generatedMesh.RecalculateBounds();
            SetGeneratedSpriteMetadata(m_generatedMesh.bounds.size,
                new Rect(spriteData.x / (float)spriteData.texture_width, spriteData.y / (float)spriteData.texture_height,
                    spriteData.width / (float)spriteData.texture_width, spriteData.height / (float)spriteData.texture_height));
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            if (meshFilter == null || meshRenderer == null)
            {
                return;
            }

            meshFilter.sharedMesh = m_generatedMesh;
            meshRenderer.sharedMaterial = material;
            meshRenderer.sortingOrder = 1;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.enabled = true;
        }
    }
}
