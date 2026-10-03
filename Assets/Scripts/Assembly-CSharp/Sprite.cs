using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
[ExecuteInEditMode]
public class Sprite : MonoBehaviour, SpriteMeshGenerator
{
	private Renderer cachedRenderer;

	public string m_id = string.Empty;

	public float m_scaleX = 1f;

	public float m_scaleY = 1f;

	public int m_pivotX;

	public int m_pivotY;

	public bool m_updateCollider = true;

	/// <summary>
	/// Mirrors the sprite left to right in its mesh, for the art that used to be mirrored by giving the
	/// transform a negative x scale.
	/// </summary>
	/// <remarks>
	/// A BoxCollider cannot sit under a negative scale: PhysX forces the box positive and warns about it
	/// every time the collider is built, which is what the build menu's arrows did - "BoxCollider does not
	/// support negative scale or size", naming MoveLeftButton. Mirroring here instead leaves the transform,
	/// the generated mesh, and the collider's own geometry alone, so the picture is the same one and
	/// nothing else has to change. Button's hover animation reads the ratio of the scale it woke up with,
	/// so it wants a positive one too.
	/// </remarks>
	public bool m_flipX;

	public const float DefaultCameraSize = 10f;

	public const float DefaultCameraHeight = 20f;

	public const float DefaultScreenHeight = 768f;

	public const float DefaultAtlasSize = 1024f;

	[SerializeField] private int m_spriteWidth;

	[SerializeField] private int m_spriteHeight;

	private int m_spritePivotX;

	private int m_spritePivotY;

	[SerializeField] private Rect m_uvRect;

	protected void SetGeneratedSpriteMetadata(Vector2 size, Rect uv)
	{
		m_spriteWidth = Mathf.Max(1, Mathf.RoundToInt(size.x * 38.4f));
		m_spriteHeight = Mathf.Max(1, Mathf.RoundToInt(size.y * 38.4f));
		m_uvRect = uv;
	}

	private RuntimeSpriteDatabase m_spriteDatabase;

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

	public string Id => m_id;

	public Vector2 Size => new Vector2(base.transform.localScale.x * (float)m_spriteWidth / 768f * 20f, base.transform.localScale.y * (float)m_spriteHeight / 768f * 20f);

	public Mesh SpriteMesh => GetComponent<MeshFilter>().sharedMesh;

	public Vector2 PixelSize => new Vector2(m_spriteWidth, m_spriteHeight);

	public Rect UVRect => m_uvRect;

	public void GetPreviewImage(out float aspectRatio, out Rect uvRect)
	{
		if (GetComponent<MeshFilter>().sharedMesh != null)
		{
			aspectRatio = (float)m_spriteWidth / (float)m_spriteHeight;
			uvRect = m_uvRect;
			return;
		}
		SpriteData spriteData = Singleton<RuntimeSpriteDatabase>.Instance.Find(m_id);
		if (spriteData != null)
		{
			if ((float)spriteData.height > 0f)
			{
				aspectRatio = (float)spriteData.width / (float)spriteData.height;
			}
			else
			{
				aspectRatio = 1f;
			}
			uvRect = spriteData.uv;
		}
		else
		{
			aspectRatio = 1f;
			uvRect = new Rect(0f, 0f, 1f, 1f);
		}
	}

	protected virtual bool RenderingEnabled()
	{
		return true;
	}

	private void Awake()
	{
		if (Application.isPlaying && !string.IsNullOrEmpty(m_id))
		{
			SpriteData data = Singleton<RuntimeSpriteDatabase>.Instance.Find(m_id);
			SelectSprite(data);
		}
	}

	private void OnDestroy()
	{
	}

	public void RebuildMesh()
	{
		MeshFilter component = GetComponent<MeshFilter>();
		if (component.sharedMesh != null)
		{
			Object.DestroyImmediate(component.sharedMesh);
		}
		component.sharedMesh = null;
		SpriteData data = Singleton<RuntimeSpriteDatabase>.Instance.Find(m_id);
		SelectSprite(data);
	}

	public void SelectSprite(SpriteData data, bool forceResetMesh = false)
	{
		m_id = data.id;
		MeshFilter meshFilter = GetComponent(typeof(MeshFilter)) as MeshFilter;
		int num = (int)(m_scaleX * (float)data.width);
		int num2 = (int)(m_scaleY * (float)data.height);
		int num3 = data.selectionX + data.selectionWidth / 2;
		int num4 = data.selectionY + data.selectionHeight / 2;
		int num5 = data.UVx + data.width / 2;
		int num6 = data.UVy + data.height / 2;
		int num7 = num3 - num5;
		int num8 = num4 - num6;
		int num9 = (int)(m_scaleX * (float)(num7 + data.pivotX + m_pivotX));
		int num10 = (int)(m_scaleY * (float)(num8 + data.pivotY + m_pivotY));
		if (meshFilter.sharedMesh == null || forceResetMesh || num != m_spriteWidth || num2 != m_spriteHeight || num9 != m_spritePivotX || num10 != m_spritePivotY)
		{
			m_spritePivotX = num9;
			m_spritePivotY = num10;
			m_spriteWidth = num;
			m_spriteHeight = num2;
			CreateMesh(meshFilter, m_spriteWidth, m_spriteHeight, m_spritePivotX, m_spritePivotY);
			ResetUVs(data, meshFilter.sharedMesh);
			if (m_updateCollider)
			{
				UpdateCollider();
			}
		}
		else if (m_uvRect != data.uv)
		{
			ResetUVs(data, meshFilter.sharedMesh);
		}
	}

	private void ResetUVs(SpriteData data, Mesh mesh)
	{
		m_uvRect = data.uv;
		float num = 0f;
		float num2 = 0f;
		if (data.opaqueBorderPixels > 0)
		{
			num = (float)data.opaqueBorderPixels * data.uv.width / (float)data.width;
			num2 = (float)data.opaqueBorderPixels * data.uv.height / (float)data.height;
		}
		Vector2[] array = new Vector2[4];
		array[0].x = data.uv.x + num;
		array[0].y = data.uv.y + num2;
		array[1].x = data.uv.x + num;
		array[1].y = data.uv.y + data.uv.height - 1f * num2;
		array[2].x = data.uv.x + data.uv.width - 1f * num;
		array[2].y = data.uv.y + data.uv.height - 1f * num2;
		array[3].x = data.uv.x + data.uv.width - 1f * num;
		array[3].y = data.uv.y + num2;
		if (m_flipX)
		{
			// Mirror across the mesh rather than across the transform, so that the object can keep a
			// positive scale and its BoxCollider: the two swapped edges are the sprite's own left and
			// right, exactly the pair a negative x scale swaps.
			float left = array[0].x;
			float right = array[2].x;
			for (int index = 0; index < array.Length; index++)
			{
				array[index].x = left + right - array[index].x;
			}
		}
		m_uvRect = data.uv;
		if (RenderingEnabled())
		{
			mesh.uv = array;
		}
	}

	public void CreateMesh(MeshFilter mf, int width, int height, int pivotX, int pivotY)
	{
		if (RenderingEnabled())
		{
			float num = (float)width * 10f / 768f;
			float num2 = (float)height * 10f / 768f;
			float num3 = -2f * (float)pivotX * 10f / 768f;
			float num4 = -2f * (float)pivotY * 10f / 768f;
			Mesh mesh = new Mesh();
			mesh.name = "GeneratedMesh_" + width + "x" + height;
			if (!Application.isPlaying)
			{
				mesh.hideFlags = HideFlags.DontSave;
			}
			mesh.vertices = new Vector3[4]
			{
				new Vector3(num3 - num, num4 - num2, 0f),
				new Vector3(num3 - num, num4 + num2, 0f),
				new Vector3(num3 + num, num4 + num2, 0f),
				new Vector3(num3 + num, num4 - num2, 0f)
			};
			mesh.triangles = new int[6] { 0, 1, 2, 2, 3, 0 };
			mf.sharedMesh = mesh;
			mf.sharedMesh.RecalculateNormals();
			mf.sharedMesh.RecalculateBounds();
		}
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
		RefreshEditorView();
	}

	public void RefreshEditorView()
	{
		if (!Application.isPlaying && (bool)base.transform.GetComponent<MeshFilter>() && (bool)GetComponent<Renderer>().sharedMaterial && (bool)GetComponent<Renderer>().sharedMaterial.mainTexture)
		{
			if (m_spriteDatabase == null)
			{
				m_spriteDatabase = Singleton<RuntimeSpriteDatabase>.Instance;
			}
			SpriteData spriteData = m_spriteDatabase.Find(m_id);
			if (spriteData != null)
			{
				SelectSprite(spriteData);
			}
		}
	}
}
