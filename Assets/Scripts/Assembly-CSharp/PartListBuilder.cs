using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PartListBuilder
{
	public struct PartRangeValue
	{
		public BasePart Part;

		public int Count;

		public string PartName;

		public PartRangeValue(BasePart part, int count, string partName)
		{
			Part = part;
			Count = count;
			PartName = partName;
		}
	}

	private Dictionary<PartTypeInfo, BasePart> m_partMap;

	private Dictionary<PartTypeInfo, PartRangeValue> m_partRangeMap;

	private PartIconBuilder m_partIconBuilder;

	public PartListBuilder(PartListData data)
	{
		Initialize(data);
	}

	private void Initialize(PartListData data)
	{
		m_partIconBuilder = new PartIconBuilder(data);
		m_partMap = new Dictionary<PartTypeInfo, BasePart>();
		m_partRangeMap = new Dictionary<PartTypeInfo, PartRangeValue>();
		foreach (GameObject part in data.Parts)
		{
			BasePart component = part.GetComponent<BasePart>();
			m_partMap.Add(new PartTypeInfo(component), component);
		}
		foreach (PartListData.PartRangeData partRange in data.PartRanges)
		{
			BasePart component2 = partRange.Part.GetComponent<BasePart>();
			m_partRangeMap.Add(new PartTypeInfo(component2), new PartRangeValue(component2, partRange.Count, partRange.PartName));
		}
	}

	public BasePart GetPart(PartTypeInfo info)
	{
		return GetValueInternal(m_partMap, info);
	}

	public PartRangeValue GetPartRange(PartTypeInfo info)
	{
		return GetValueInternal(m_partRangeMap, info);
	}

	public IEnumerable<BasePart> GetParts(BasePart.PartType type)
	{
		return GetValuesInternal(m_partMap, type);
	}

	public IEnumerable<PartRangeValue> GetPartRanges(BasePart.PartType type)
	{
		return GetValuesInternal(m_partRangeMap, type);
	}

	private T GetValueInternal<T>(Dictionary<PartTypeInfo, T> dictionary, PartTypeInfo info)
	{
		return dictionary[info];
	}

	private IEnumerable<T> GetValuesInternal<T>(Dictionary<PartTypeInfo, T> dictionary, BasePart.PartType type)
	{
		return from pair in dictionary
			where pair.Key.PartType == type
			select pair.Value;
	}

	public BasePart CreatePart(PartTypeInfo partTypeInfo)
	{
		return CreatePart(GetPart(partTypeInfo));
	}

	public BasePart CreatePart(BasePart part)
	{
		return CreatePart(part, part.name, part.TypeInfo);
	}

	public BasePart CreatePart(BasePart part, string name, PartTypeInfo typeInfo)
	{
		BasePart basePart = Object.Instantiate(part);
		basePart.name = name;
		basePart.Type = typeInfo.PartType;
		basePart.Index = typeInfo.PartIndex;
		Sprite constructionIconSprite = part.m_constructionIconSprite;
		if (constructionIconSprite != null)
		{
			GameObject gameObject = Object.Instantiate(constructionIconSprite.gameObject);
			gameObject.name = constructionIconSprite.name;
			Vector3 position = gameObject.transform.position;
			gameObject.transform.position = new Vector3(position.x, position.y, -130f);
			basePart.m_constructionIconSprite = gameObject.GetComponent<Sprite>();
		}
		else if (m_partIconBuilder.Contains(basePart))
		{
			GameObject gameObject2 = m_partIconBuilder.CreatePartIcon(basePart);
			Vector3 position2 = gameObject2.transform.position;
			gameObject2.transform.position = new Vector3(position2.x, position2.y, -130f);
			basePart.m_constructionIconSprite = gameObject2.GetComponent<Sprite>();
		}
		return basePart;
	}

	public IEnumerable<BasePart> CreatePartRange(PartRangeValue partRange)
	{
		BasePart part = partRange.Part;
		for (int i = 0; i < partRange.Count; i++)
		{
			string name = string.Format(partRange.PartName, i + 1);
			PartTypeInfo typeInfo = new PartTypeInfo(part.Type, part.Index + i);
			yield return CreatePart(part, name, typeInfo);
		}
	}
}
