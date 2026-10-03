using System.Collections.Generic;
using UnityEngine;

namespace BadPiggies.Multiplayer
{
public static class MultiplayerAttachments
{
	public static void CollectInto(List<BasePart> parts, List<Transform> results)
	{
		if (parts == null || results == null)
		{
			return;
		}
		for (int i = 0; i < parts.Count; i++)
		{
			BasePart basePart = parts[i];
			if (!(basePart == null))
			{
				Append<BoxingGlove>(basePart, results);
				Append<Hook>(basePart, results);
			}
		}
	}

	private static void Append<T>(BasePart part, List<Transform> results) where T : Component
	{
		T[] componentsInChildren = part.GetComponentsInChildren<T>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			if (!(componentsInChildren[i] == null) && !(componentsInChildren[i].transform == part.transform))
			{
				results.Add(componentsInChildren[i].transform);
			}
		}
	}
}
}
