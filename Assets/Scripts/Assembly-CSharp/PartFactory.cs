using System.Collections.Generic;
using UnityEngine;

public class PartFactory : IPartFactory
{
	public class Unit : IPartFactoryUnit
	{
		private BasePart.PartType m_partType;

		private BasePart m_regularPart;

		private List<BasePart> m_customParts;

		public BasePart.PartType PartType => m_partType;

		public BasePart RegularPart => m_regularPart;

		public List<BasePart> CustomParts => m_customParts;

		IReadOnlyList<BasePart> IPartFactoryUnit.CustomParts => m_customParts;

		public Unit(BasePart.PartType partType)
		{
			m_partType = partType;
			m_regularPart = null;
			m_customParts = new List<BasePart>();
		}

		public BasePart FindPart(int partIndex)
		{
			if (partIndex == 0)
			{
				return m_regularPart;
			}
			return m_customParts.Find((BasePart part) => part.Index == partIndex);
		}

		public int FindPartIndex(int partIndex)
		{
			return m_customParts.FindIndex((BasePart part) => part.Index == partIndex);
		}

		public void SetPart(BasePart part)
		{
			m_regularPart = part;
		}

		public void AddCustomPart(BasePart part)
		{
			if (!SetCustomPart(part))
			{
				m_customParts.Add(part);
			}
		}

		public bool SetCustomPart(BasePart part)
		{
			int num = FindPartIndex(part.Index);
			if (num == -1)
			{
				return false;
			}
			CustomParts[num] = part;
			return true;
		}

		public void AddCustomParts(IEnumerable<BasePart> part)
		{
			m_customParts.AddRange(part);
		}
	}

	private List<Unit> m_units;

	public List<Unit> Units => m_units;

	IEnumerable<IPartFactoryUnit> IPartFactory.Units => m_units;

	public PartFactory()
		: this(0)
	{
	}

	public PartFactory(int capacity)
	{
		m_units = new List<Unit>(capacity);
	}

	public Unit FindUnit(BasePart.PartType partType)
	{
		return m_units.Find((Unit unit) => unit.PartType == partType);
	}

	IPartFactoryUnit IPartFactory.FindUnit(BasePart.PartType partType)
	{
		return FindUnit(partType);
	}

	public BasePart FindPart(BasePart.PartType partType)
	{
		return FindUnit(partType)?.RegularPart;
	}

	public BasePart FindCustomPart(BasePart.PartType partType, int partIndex)
	{
		return FindUnit(partType)?.FindPart(partIndex);
	}

	public BasePart CreatePart(BasePart.PartType partType)
	{
		BasePart basePart = FindPart(partType);
		if (!(basePart != null))
		{
			return null;
		}
		return Object.Instantiate(basePart);
	}

	public BasePart CreateCustomPart(BasePart.PartType partType, int partIndex)
	{
		BasePart basePart = FindCustomPart(partType, partIndex);
		if (!(basePart != null))
		{
			return null;
		}
		return Object.Instantiate(basePart);
	}

	public void AddPart(BasePart part)
	{
		if (!SetPart(part))
		{
			Unit unit = new Unit(part.Type);
			unit.SetPart(part);
			m_units.Add(unit);
		}
	}

	public bool SetPart(BasePart part)
	{
		Unit unit = FindUnit(part.Type);
		if (unit == null)
		{
			return false;
		}
		unit.SetPart(part);
		return true;
	}

	public void AddCustomPart(BasePart part)
	{
		Unit unit = FindUnit(part.Type);
		if (unit == null)
		{
			unit = new Unit(part.Type);
			m_units.Add(unit);
		}
		unit.AddCustomPart(part);
	}

	public bool SetCustomPart(BasePart part)
	{
		return FindUnit(part.Type)?.SetCustomPart(part) ?? false;
	}

	public void AddCustomParts(BasePart.PartType partType, IEnumerable<BasePart> part)
	{
		FindUnit(partType)?.AddCustomParts(part);
	}
}
