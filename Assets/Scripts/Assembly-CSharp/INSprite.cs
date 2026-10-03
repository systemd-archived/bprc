using UnityEngine;

public class INSprite : INSpriteBase
{
	[SerializeField]
	private Rect m_rect;

	[SerializeField]
	private Vector2 m_scale = new Vector2(1f, 1f);

	[SerializeField]
	private int m_screenHeight = 1080;

	public Rect Rect => m_rect;

	public Vector2 Scale => m_scale;

	public int ScreenHeight => m_screenHeight;

	private void Awake()
	{
		m_graphicType = GetGraphicType();
		if (m_graphicType == GraphicType.Mesh)
		{
			CreateMesh();
		}
	}

	public override void InitializeSpriteData()
	{
		Texture mainTexture = GetComponent<MeshRenderer>().sharedMaterial.mainTexture;
		m_spriteData = new INSpriteData(textureSize: new Vector2Int(mainTexture.width, mainTexture.height), rect: m_rect, scale: m_scale, screenHeight: m_screenHeight);
	}
}
