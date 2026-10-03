using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public class GameDataAdapter : GameData, IPartFactory
{
	private IPartFactory m_partFactory;

	public IPartFactory PartFactory
	{
		get
		{
			return m_partFactory;
		}
		set
		{
			m_partFactory = value;
		}
	}

	public IEnumerable<IPartFactoryUnit> Units => m_partFactory.Units;

	public override IEnumerable<GameObject> Parts
	{
		get
		{
			foreach (IPartFactoryUnit unit in m_partFactory.Units)
			{
				BasePart regularPart = unit.RegularPart;
				if (regularPart != null)
				{
					yield return regularPart.gameObject;
				}
			}
			bool cargoAdded = false;
			foreach (GameObject portedPart in RCR2Port.RoverPortCatalog.Instance.Parts)
			{
				if (portedPart.GetComponent<BasePart>().Type == BasePart.PartType.RCR2Cargo)
				{
					if (cargoAdded) continue;
					cargoAdded = true;
				}
				yield return portedPart;
			}
		}
	}

	public override IEnumerable<IReadOnlyList<BasePart>> CustomParts
	{
		get
		{
			foreach (IPartFactoryUnit unit in m_partFactory.Units)
			{
				yield return unit.CustomParts;
			}
			if (RCR2Port.RoverPortCatalog.HasParts)
			{
				yield return RCR2Port.RoverPortCatalog.Instance.GetPartComponents();
			}
		}
	}

	public static GameDataAdapter Create(GameData source)
	{
		return Create(source, null);
	}

	public static GameDataAdapter Create(GameData source, IPartFactory factory)
	{
		GameDataAdapter gameDataAdapter = ScriptableObject.CreateInstance<GameDataAdapter>();
		FieldInfo[] fields = source.GetType().GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		foreach (FieldInfo fieldInfo in fields)
		{
			fieldInfo.SetValue(gameDataAdapter, fieldInfo.GetValue(source));
		}
		gameDataAdapter.m_partFactory = factory;
		return gameDataAdapter;
	}

	public IPartFactoryUnit FindUnit(BasePart.PartType partType)
	{
		return m_partFactory.FindUnit(partType);
	}

	public BasePart FindPart(BasePart.PartType partType)
	{
		return m_partFactory.FindPart(partType);
	}

	public BasePart FindCustomPart(BasePart.PartType partType, int partIndex)
	{
		return GetCustomPart(partType, partIndex);
	}

	public override GameObject GetPart(BasePart.PartType type)
	{
		BasePart part = m_partFactory.FindPart(type);
		if (part != null)
		{
			return part.gameObject;
		}
		if (RCR2Port.RoverPortCatalog.HasParts)
		{
			GameObject first = RCR2Port.RoverPortCatalog.Instance.Parts.FirstOrDefault(p => p.GetComponent<BasePart>().Type == type);
			if (first != null)
			{
				return first;
			}
		}
		return null;
	}

	public override CustomPartInfo GetCustomPart(BasePart.PartType type)
	{
		List<BasePart> result = new List<BasePart>();
		IPartFactoryUnit partFactoryUnit = m_partFactory.FindUnit(type);
		if (partFactoryUnit != null)
		{
			result.AddRange(partFactoryUnit.CustomParts);
		}
		if (RCR2Port.RoverPortCatalog.HasParts)
		{
			foreach (BasePart ported in RCR2Port.RoverPortCatalog.Instance.GetPartComponents())
			{
				if (ported.Type == type && !result.Exists(existing => existing != null && existing.Index == ported.Index))
				{
					result.Add(ported);
				}
			}
		}
		if (result.Count == 0 && partFactoryUnit == null)
		{
			return null;
		}
		return new CustomPartInfo(type, result);
	}

	public override BasePart GetCustomPart(BasePart.PartType type, int customIndex)
	{
		if (RCR2Port.RoverPortCatalog.HasParts)
		{
			GameObject portedPart = RCR2Port.RoverPortCatalog.Instance.GetPart(customIndex);
			// The ported index range belongs to the port alone, so the index decides which part is
			// meant: a save written before the cockpit changed its part type still loads.
			if (portedPart != null)
			{
				return portedPart.GetComponent<BasePart>();
			}
		}
		return m_partFactory.FindCustomPart(type, customIndex);
	}

	public override int GetCustomPartIndex(BasePart.PartType type, string partName)
	{
		IPartFactoryUnit partFactoryUnit = m_partFactory.FindUnit(type);
		if (partFactoryUnit != null)
		{
			if (partFactoryUnit.RegularPart != null && partFactoryUnit.RegularPart.name == partName)
			{
				return 0;
			}
			foreach (BasePart customPart in partFactoryUnit.CustomParts)
			{
				if (customPart.Type == type && customPart.name == partName)
				{
					return customPart.Index;
				}
			}
		}
		if (RCR2Port.RoverPortCatalog.HasParts)
		{
			foreach (BasePart customPart in RCR2Port.RoverPortCatalog.Instance.GetPartComponents())
			{
				if (customPart.Type == type && customPart.name == partName)
				{
					return customPart.Index;
				}
			}
		}
		return -1;
	}
}
