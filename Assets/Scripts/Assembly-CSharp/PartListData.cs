using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PartListData", menuName = "ScriptableObjects/PartListData", order = 1)]
public class PartListData : ScriptableObject
{
	[Serializable]
	public class PartIconData
	{
		public GameObject Part;

		public string MaterialName;

		public string SpriteName;
	}

	[Serializable]
	public class PartRangeData
	{
		public GameObject Part;

		public int Count;

		public string PartName;
	}

	[Serializable]
	public class PartIconRangeData
	{
		public GameObject Part;

		public int Count;

		public string MaterialName;

		public string SpriteName;
	}

	public GameObject CustomPartPrefab;

	public GameObject PartIconPrefab;

	public List<GameObject> Parts;

	public List<PartIconData> PartIcons;

	public List<PartRangeData> PartRanges;

	public List<PartIconRangeData> PartIconRanges;
}
