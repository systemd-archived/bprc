using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
public class UnmanagedSprite : MonoBehaviour
{
	private Renderer cachedRenderer;

	[HideInInspector]
	public int m_textureWidth;

	[HideInInspector]
	public int m_textureHeight;

	public float m_scale = 1f;

	public int m_spriteWidth;

	public int m_spriteHeight;

	public int m_UVx;

	public int m_UVy;

	public int m_width = 16;

	public int m_height = 16;

	public int m_atlasGridSubdivisions = 16;

	public int m_border;

	public bool m_updateCollider;

	[HideInInspector]
	public Rect m_uvRect;

	public const float DefaultCameraSize = 10f;

	public const float DefaultCameraHeight = 20f;

	public const float DefaultScreenHeight = 768f;

	public const float DefaultAtlasSize = 1024f;

	protected Mesh m_spriteMesh;

	protected MeshFilter m_meshFilter;

	public Renderer renderer
	{
		get
		{
			if (cachedRenderer == null)
			{
				cachedRenderer = GetComponent<Renderer>();
			}
			return cachedRenderer;
		}
	}

	public Vector2 Size => new Vector2(base.transform.localScale.x * (float)m_spriteWidth / 768f * 20f, base.transform.localScale.y * (float)m_spriteHeight / 768f * 20f);

	public Mesh SpriteMesh => m_spriteMesh;

	private void Awake()
	{
		MeshFilter meshFilter = GetComponent(typeof(MeshFilter)) as MeshFilter;
		if (!meshFilter.sharedMesh)
		{
			CreatePlane(meshFilter, m_spriteWidth, m_spriteHeight);
			MapUVToTexture(m_UVx, m_UVy, m_width, m_height);
		}
		else
		{
			m_spriteMesh = meshFilter.sharedMesh;
		}
		m_meshFilter = meshFilter;
	}

	public void RebuildMesh()
	{
		MeshFilter mf = GetComponent(typeof(MeshFilter)) as MeshFilter;
		CreatePlane(mf, m_spriteWidth, m_spriteHeight);
		MapUVToTexture(m_UVx, m_UVy, m_width, m_height);
		if (m_updateCollider)
		{
			UpdateCollider();
		}
	}

	public void ResetSize()
	{
		Texture texture = GetAtlasTexture();
		if (texture == null)
		{
			RebuildMesh();
			return;
		}
		int subdivisions = Mathf.Max(1, m_atlasGridSubdivisions);
		m_textureWidth = m_width * texture.width / subdivisions;
		m_textureHeight = m_height * texture.height / subdivisions;
		m_spriteWidth = (int)(m_scale * (float)(m_textureWidth - 2 * m_border));
		m_spriteHeight = (int)(m_scale * (float)(m_textureHeight - 2 * m_border));
		RebuildMesh();
	}

	public void MapUVToTexture(int UVx, int UVy, int width, int height)
	{
		if (!(m_spriteMesh == null))
		{
			m_UVx = UVx;
			m_UVy = UVy;
			m_width = width;
			m_height = height;
			Vector2[] array = CalculateUVs(m_UVx, m_UVy, m_width, m_height);
			m_uvRect = new Rect(array[0].x, array[0].y, array[2].x - array[0].x, array[2].y - array[0].y);
			m_spriteMesh.uv = array;
		}
	}

	public Vector2[] CalculateUVs(int UVx, int UVy, int width, int height)
	{
		float num = 1f / Mathf.Max(1, m_atlasGridSubdivisions);
		Texture texture = GetAtlasTexture();
		float insetX = (m_border + 0.5f) / (texture != null ? texture.width : 1024f);
		float insetY = (m_border + 0.5f) / (texture != null ? texture.height : 1024f);
		float num4 = (float)UVx * num + insetX;
		float num5 = (float)UVy * num + insetY;
		float x = (UVx + width) * num - insetX;
		float y = (UVy + height) * num - insetY;
		return new Vector2[4]
		{
			new Vector2(num4, num5),
			new Vector2(num4, y),
			new Vector2(x, y),
			new Vector2(x, num5)
		};
	}

	private Texture GetAtlasTexture()
	{
		Material material = renderer.sharedMaterial;
		// Depth-only geometry also uses UnmanagedSprite, without any texture property.
		return material != null && material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
	}

	public void ChangeUVs(Vector2[] uv)
	{
		m_meshFilter.mesh.uv = uv;
	}

	public void Instantiate()
	{
	}

	public void CreatePlane(MeshFilter mf, int width, int height)
	{
		float num = (float)width * 10f / 768f;
		float num2 = (float)height * 10f / 768f;
		mf.sharedMesh = new Mesh
		{
			name = "GeneratedMesh_" + width + "x" + height,
			vertices = new Vector3[4]
			{
				new Vector3(0f - num, 0f - num2, 0f),
				new Vector3(0f - num, num2, 0f),
				new Vector3(num, num2, 0f),
				new Vector3(num, 0f - num2, 0f)
			},
			triangles = new int[6] { 0, 1, 2, 2, 3, 0 }
		};
		m_spriteMesh = mf.sharedMesh;
		mf.sharedMesh.RecalculateNormals();
		mf.sharedMesh.RecalculateBounds();
	}

	private void UpdateCollider()
	{
		BoxCollider component = GetComponent<BoxCollider>();
		if ((bool)component)
		{
			float z = component.size.z;
			component.center = SpriteMesh.bounds.center;
			Vector3 size = 2f * SpriteMesh.bounds.extents;
			size.z = z;
			component.size = size;
		}
	}

	private void OnDrawGizmos()
	{
		if (Application.isPlaying)
		{
			return;
		}
		MeshFilter component = base.transform.GetComponent<MeshFilter>();
		Texture texture = GetAtlasTexture();
		if (component != null && texture != null)
		{
			int subdivisions = Mathf.Max(1, m_atlasGridSubdivisions);
			m_textureWidth = m_width * texture.width / subdivisions;
			m_textureHeight = m_height * texture.height / subdivisions;
			if (m_spriteWidth == 0)
			{
				m_spriteWidth = (int)(m_scale * (float)m_textureWidth);
			}
			if (m_spriteHeight == 0)
			{
				m_spriteHeight = (int)(m_scale * (float)m_textureHeight);
			}
			if (!component.sharedMesh)
			{
				CreatePlane(component, m_spriteWidth, m_spriteHeight);
				MapUVToTexture(m_UVx, m_UVy, m_width, m_height);
			}
		}
	}
}
