using UnityEngine;

public class INSerializedSprite : INSpriteBase
{
	[SerializeField]
	private string m_name;

	public string SpriteName
	{
		get
		{
			return m_name;
		}
		set
		{
			m_name = value;
		}
	}

	private void Awake()
	{
		if (!string.IsNullOrEmpty(m_name))
		{
			m_graphicType = GetGraphicType();
			if (m_graphicType == GraphicType.Mesh)
			{
				CreateMesh();
			}
		}
	}

	public override void InitializeSpriteData()
	{
		Texture mainTexture = GetComponent<MeshRenderer>().sharedMaterial.mainTexture;
		m_spriteData = INSpriteManager.Instance.GetAtlasData(mainTexture.name)[m_name];
	}
}
