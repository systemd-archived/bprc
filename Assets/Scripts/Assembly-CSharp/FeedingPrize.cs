using System;
using UnityEngine;

[Serializable]
public class FeedingPrize
{
	public enum PrizeType
	{
		None = 0,
		Junk = 1,
		SuperGlue = 2,
		SuperMagnet = 3,
		TurboCharge = 4,
		SuperMechanic = 5,
		PremiumPart = 6,
		NightVision = 7,
		SnoutCoins = 8,
		Scrap = 9
	}

	public string name;

	public PrizeType type;

	public float rangeWidth;

	public GameObject icon;

	public float iconScale = 2f;
}
