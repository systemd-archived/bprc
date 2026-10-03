using UnityEngine;
using UnityEngine.UI;

public class INSpriteBase : MonoBehaviour
{
	public enum GraphicType
	{
		None = 0,
		Mesh = 1,
		Image = 2,
		RawImage = 3
	}

	protected INSpriteData m_spriteData;

	protected GraphicType m_graphicType;

	public INSpriteData SpriteData => m_spriteData;

	public GraphicType GetGraphicType()
	{
		MeshRenderer component = GetComponent<MeshRenderer>();
		MeshFilter component2 = GetComponent<MeshFilter>();
		Graphic component3 = GetComponent<Graphic>();
		if (component != null && component2 != null)
		{
			return GraphicType.Mesh;
		}
		if (component3 is Image)
		{
			return GraphicType.Image;
		}
		if (component3 is RawImage)
		{
			return GraphicType.RawImage;
		}
		return GraphicType.None;
	}

	public virtual void InitializeSpriteData()
	{
	}

	public void CreateMesh()
	{
		MeshRenderer component = GetComponent<MeshRenderer>();
		MeshFilter component2 = GetComponent<MeshFilter>();
		if (component != null && component2 != null)
		{
			InitializeSpriteData();
			component2.sharedMesh = CreateMeshInternal(m_spriteData);
		}
	}

	public void UpdateMesh()
	{
		MeshRenderer component = GetComponent<MeshRenderer>();
		MeshFilter component2 = GetComponent<MeshFilter>();
		if (component != null && component2 != null)
		{
			InitializeSpriteData();
			UpdateMeshInternal(m_spriteData, component2.sharedMesh);
		}
	}

	private Mesh CreateMeshInternal(INSpriteData spriteData)
	{
		Mesh mesh = new Mesh();
		SetMeshInternal(spriteData, mesh);
		return mesh;
	}

	private void UpdateMeshInternal(INSpriteData spriteData, Mesh mesh)
	{
		if (mesh != null)
		{
			mesh.Clear();
			SetMeshInternal(spriteData, mesh);
		}
	}

	private void SetMeshInternal(INSpriteData spriteData, Mesh mesh)
	{
		Rect rect = spriteData.Rect;
		Vector2 size = spriteData.Size;
		Rect flippedUVRect = spriteData.FlippedUVRect;
		float x = size.x;
		float y = size.y;
		float x2 = flippedUVRect.x;
		float y2 = flippedUVRect.y;
		float width = flippedUVRect.width;
		float height = flippedUVRect.height;
		mesh.name = "Mesh_" + rect.width + "x" + rect.height;
		mesh.vertices = new Vector3[4]
		{
			new Vector3(0f - x, 0f - y, 0f),
			new Vector3(0f - x, y, 0f),
			new Vector3(x, y, 0f),
			new Vector3(x, 0f - y, 0f)
		};
		mesh.triangles = new int[6] { 0, 1, 2, 2, 3, 0 };
		mesh.uv = new Vector2[4]
		{
			new Vector2(x2, y2),
			new Vector2(x2, y2 + height),
			new Vector2(x2 + width, y2 + height),
			new Vector2(x2 + width, y2)
		};
		mesh.RecalculateNormals();
		mesh.RecalculateBounds();
	}
}
