using System;
using UnityEngine;

[Serializable]
public class ShopRibbon
{
	public enum Ribbon
	{
		BestValue = 0,
		MostPopular = 1
	}

	public Ribbon ribbonType;

	public GameObject ribbon;

	public RuntimePlatform platform;

	public string itemId;
}
