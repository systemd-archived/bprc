using System.Collections.Generic;
using UnityEngine;

public class PartIconBuilder
{
	private struct SpriteValue
	{
		public Material Material;

		public string SpriteName;

		public SpriteValue(Material material, string spriteName)
		{
			Material = material;
			SpriteName = spriteName;
		}
	}

	private GameObject m_partIconPrefab;

	private Dictionary<PartTypeInfo, SpriteValue> m_partIconSpriteMap;

	public PartIconBuilder(PartListData data)
	{
		Initialize(data);
	}

	private void Initialize(PartListData data)
	{
		int count = data.PartIcons.Count;
		m_partIconPrefab = data.PartIconPrefab;
		m_partIconSpriteMap = new Dictionary<PartTypeInfo, SpriteValue>(count);
		foreach (PartListData.PartIconData partIcon in data.PartIcons)
		{
			BasePart component = partIcon.Part.GetComponent<BasePart>();
			Material material = INUnity.LoadMaterial(partIcon.MaterialName);
			SpriteValue value = new SpriteValue(material, partIcon.SpriteName);
			m_partIconSpriteMap.Add(new PartTypeInfo(component.Type, component.Index), value);
		}
		foreach (PartListData.PartIconRangeData partIconRange in data.PartIconRanges)
		{
			BasePart component2 = partIconRange.Part.GetComponent<BasePart>();
			Material material2 = INUnity.LoadMaterial(partIconRange.MaterialName);
			for (int i = 0; i < partIconRange.Count; i++)
			{
				string spriteName = string.Format(partIconRange.SpriteName, i + 1);
				SpriteValue value2 = new SpriteValue(material2, spriteName);
				m_partIconSpriteMap.Add(new PartTypeInfo(component2.Type, component2.Index + i), value2);
			}
		}
	}

	public bool Contains(BasePart part)
	{
		return Contains(new PartTypeInfo(part));
	}

	public bool Contains(PartTypeInfo partTypeInfo)
	{
		return m_partIconSpriteMap.ContainsKey(partTypeInfo);
	}

	public GameObject CreatePartIcon(BasePart part)
	{
		return CreatePartIcon(new PartTypeInfo(part));
	}

	public GameObject CreatePartIcon(PartTypeInfo partTypeInfo)
	{
		SpriteValue spriteValue = m_partIconSpriteMap[partTypeInfo];
		GameObject gameObject = Object.Instantiate(m_partIconPrefab);
		gameObject.name = "Icon_" + partTypeInfo.PartType.ToString() + "_" + (partTypeInfo.PartIndex + 1);
		gameObject.layer = LayerMask.NameToLayer("HUD");
		MeshRenderer component = gameObject.GetComponent<MeshRenderer>();
		INSerializedSprite component2 = gameObject.GetComponent<INSerializedSprite>();
		component.material = spriteValue.Material;
		component2.SpriteName = spriteValue.SpriteName;
		component2.CreateMesh();
		return gameObject;
	}
}
