using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class INPartFactoryManager : Singleton<INPartFactoryManager>, IPartFactory
{
	[SerializeField]
	private GameObject m_partContainer;

	[SerializeField]
	private GameObject m_partIconContainer;

	private PartFactory m_partFactory;

	private PartFactory m_extraPartFactory;

	private PartListData m_partListData;

	private PartListBuilder m_partListBuilder;

	public static bool IsInitialized => Singleton<INPartFactoryManager>.instance != null;

	public PartFactory PartFactory => m_partFactory;

	public PartFactory ExtraPartFactory => m_extraPartFactory;

	public GameObject PartContainer => m_partContainer;

	public GameObject PartIconContainer => m_partIconContainer;

	IEnumerable<IPartFactoryUnit> IPartFactory.Units
	{
		get
		{
			if (m_extraPartFactory == null)
			{
				return m_partFactory.Units;
			}
			return m_partFactory.Units.Concat(m_extraPartFactory.Units);
		}
	}

	public static GameDataAdapter Adapter { get; private set; }

	private void Awake()
	{
		SetAsPersistant();
		m_partListData = INUnity.LoadScriptableObject<PartListData>("PartListData");
		m_partListBuilder = new PartListBuilder(m_partListData);
		m_partFactory = CreatePartFactory();
		InitializeSettings();
		Adapter = GameDataAdapter.Create(Singleton<GameManager>.Instance.gameData, this);
	}

	private void InitializeSettings()
	{
		RegisterPart(INFeature.WoodenBox, delegate
		{
			SetPart(INFeature.WoodenBox, BasePart.WoodenBox);
		});
		RegisterPart(INFeature.MetalBox, delegate
		{
			SetPart(INFeature.MetalBox, BasePart.MetalBox);
		});
		RegisterPart(INFeature.BracketFrame, delegate
		{
			SetPart(INFeature.BracketFrame, BasePart.BracketFrame);
		});
		RegisterPart(INFeature.ColoredFrame, SetColoredFrames);
		RegisterPart(INFeature.OffRoadWheel, delegate
		{
			SetPart(INFeature.OffRoadWheel, BasePart.OffRoadWheel);
		});
		RegisterPart(INFeature.FuelSystem, SetFuelSystem);
		RegisterPart(INFeature.BlasterTNT, delegate
		{
			SetPart(INFeature.BlasterTNT, BasePart.BlasterTNT);
		});
		RegisterPart(INFeature.HingePlate, delegate
		{
			SetParts(INFeature.HingePlate, BasePart.HingePlates);
		});
		RegisterPart(INFeature.MultipartGenerator, SetMultiPartGenerators);
		RegisterPart(INFeature.AutoGun, delegate
		{
			SetPart(INFeature.AutoGun, BasePart.AutoGun);
		});
		RegisterPart(INFeature.DecelerationLight, delegate
		{
			SetPart(INFeature.DecelerationLight, BasePart.DecelerationLight);
		});
		RegisterPart(INFeature.AutoControlLight, delegate
		{
			SetPart(INFeature.AutoControlLight, BasePart.AutoControlLight);
		});
		RegisterPart(INFeature.ElectricalSystem, SetElectricalSystem);
		RegisterPart(INFeature.MechanicalSystem, SetMechanicalSystem);
	}

	private void RegisterPart(INFeature feature, Action action)
	{
		INSettings.AddListener(feature, action);
		if (INSettings.GetBool(feature))
		{
			action();
		}
	}

	private void SetColoredFrames()
	{
		if (INSettings.GetBool(INFeature.ColoredFrame))
		{
			List<BasePart> customParts = m_partFactory.FindUnit(BasePart.PartType.MetalFrame).CustomParts;
			BasePart part = m_partListBuilder.GetPart(new PartTypeInfo(BasePart.PartType.MetalFrame, 12));
			Shader shader = INUnity.LoadShader("Unlit_ColorTransparent_SolidColor");
			for (int i = 0; i < 120; i++)
			{
				int num = i - 118;
				int num2 = i / 36;
				string text = (i + 13).ToString();
				BasePart basePart = CreatePartAndSetParent(part);
				basePart.customPartIndex = i + 12;
				basePart.m_partTier = num2 switch
				{
					2 => BasePart.PartTier.Epic, 
					1 => BasePart.PartTier.Rare, 
					0 => BasePart.PartTier.Common, 
					_ => BasePart.PartTier.Legendary, 
				};
				if (num >= 0)
				{
					text = (num + 133).ToString();
					basePart.customPartIndex = num + 132;
					basePart.m_partTier = BasePart.PartTier.Regular;
				}
				basePart.gameObject.name = "Part_MetalFrame_" + text + "_SET";
				basePart.GetComponent<MeshRenderer>().material.shader = shader;
				Sprite constructionIconSprite = basePart.m_constructionIconSprite;
				constructionIconSprite.name = "Icon_MetalFrame_" + text;
				MeshRenderer component = constructionIconSprite.GetComponent<MeshRenderer>();
				MeshRenderer component2 = constructionIconSprite.transform.Find("Background").GetComponent<MeshRenderer>();
				component.material.name = "IngameAtlas3_MetalFrame_" + text;
				component.material.shader = shader;
				component2.material.name = "IngameAtlas3_MetalFrame_Background_" + text;
				(basePart as ColoredFrame).InitializeColor();
				customParts.Add(basePart);
			}
		}
		else
		{
			for (int j = 12; j <= 129; j++)
			{
				RemoveCustomPart(BasePart.PartType.MetalFrame, j);
			}
			RemoveCustomPart(BasePart.PartType.MetalFrame, 132);
			RemoveCustomPart(BasePart.PartType.MetalFrame, 133);
		}
	}

	private void SetMultiPartGenerators()
	{
		if (INSettings.GetBool(INFeature.MultipartGenerator))
		{
			for (int i = 0; i < 3; i++)
			{
				BasePart basePart = CreatePartAndSetParent(BasePart.PartType.GrapplingHook, 8);
				if (i != 0)
				{
					basePart.gameObject.name = $"Part_GrapplingHook_{i + 9}_SET";
					basePart.customPartIndex = i + 8;
					basePart.m_constructionIconSprite.gameObject.name = $"Icon_GrapplingHook_{i + 9}";
					INSerializedSprite component = basePart.GetComponent<INSerializedSprite>();
					component.SpriteName = $"MultiPartGenerator{i + 1}_Sprite";
					component.UpdateMesh();
					INSerializedSprite componentInChildren = basePart.m_constructionIconSprite.GetComponentInChildren<INSerializedSprite>();
					componentInChildren.SpriteName = $"MultiPartGenerator{i + 1}_IconSprite";
					componentInChildren.UpdateMesh();
				}
				basePart.m_partTier = BasePart.PartTier.Common;
				m_partFactory.AddCustomPart(basePart);
			}
		}
		else
		{
			for (int j = 0; j < 3; j++)
			{
				RemoveCustomPart(BasePart.PartType.GrapplingHook, j + 8);
			}
		}
	}

	private void SetFuelSystem()
	{
		if (!INSettings.GetBool(INFeature.FuelSystem))
		{
			return;
		}
		foreach (BasePart part in m_partListBuilder.GetParts(BasePart.PartType.JetEngine))
		{
			BasePart newPart = CreatePartAndSetParent(part);
			AddPart(newPart);
		}
	}

	private void SetElectricalSystem()
	{
		if (!INSettings.GetBool(INFeature.ElectricalSystem))
		{
			return;
		}
		foreach (BasePart part in m_partListBuilder.GetParts(BasePart.PartType.ElectricalPart))
		{
			BasePart newPart = CreatePartAndSetParent(part);
			AddPart(newPart);
		}
		foreach (PartListBuilder.PartRangeValue partRange in m_partListBuilder.GetPartRanges(BasePart.PartType.ElectricalPart))
		{
			foreach (BasePart item in m_partListBuilder.CreatePartRange(partRange))
			{
				SetParent(item);
				AddPart(item);
			}
		}
	}

	private void SetMechanicalSystem()
	{
		if (!INSettings.GetBool(INFeature.MechanicalSystem))
		{
			return;
		}
		foreach (BasePart part in m_partListBuilder.GetParts(BasePart.PartType.MechanicalPart))
		{
			BasePart newPart = CreatePartAndSetParent(part);
			AddPart(newPart);
		}
		foreach (PartListBuilder.PartRangeValue partRange in m_partListBuilder.GetPartRanges(BasePart.PartType.MechanicalPart))
		{
			foreach (BasePart item in m_partListBuilder.CreatePartRange(partRange))
			{
				item.Call((Func<BasePart, BasePart>)SetParent);
				AddPart(item);
			}
		}
	}

	private void SetPart(INFeature feature, PartTypeInfo info)
	{
		if (INSettings.GetBool(feature))
		{
			m_partFactory.AddCustomPart(CreatePartAndSetParent(info.PartType, info.PartIndex));
		}
		else
		{
			RemoveCustomPart(info.PartType, info.PartIndex);
		}
	}

	private void SetParts(INFeature feature, PartRangeInfo info)
	{
		if (INSettings.GetBool(feature))
		{
			for (int i = info.PartStartIndex; i <= info.PartEndIndex; i++)
			{
				m_partFactory.AddCustomPart(CreatePartAndSetParent(info.PartType, i));
			}
		}
		else
		{
			for (int j = info.PartStartIndex; j <= info.PartEndIndex; j++)
			{
				RemoveCustomPart(info.PartType, j);
			}
		}
	}

	private PartFactory CreatePartFactory()
	{
		GameData gameData = Singleton<GameManager>.Instance.gameData;
		PartFactory partFactory = new PartFactory(gameData.m_parts.Count);
		foreach (GameObject part2 in gameData.m_parts)
		{
			BasePart part = CreatePartAndSetParent(part2.GetComponent<BasePart>());
			partFactory.AddPart(part);
		}
		foreach (CustomPartInfo customPart in gameData.m_customParts)
		{
			partFactory.AddCustomParts(customPart.PartType, customPart.PartList.Select((BasePart part2) => CreatePartAndSetParent(part2)));
		}
		return partFactory;
	}

	private BasePart CreatePartAndSetParent(BasePart.PartType partType, int partIndex)
	{
		return CreatePartAndSetParent(new PartTypeInfo(partType, partIndex));
	}

	private BasePart CreatePartAndSetParent(PartTypeInfo info)
	{
		return m_partListBuilder.GetPart(info).Call((Func<BasePart, BasePart>)m_partListBuilder.CreatePart).Call((Func<BasePart, BasePart>)SetParent);
	}

	private BasePart CreatePartAndSetParent(BasePart part)
	{
		return part.Call((Func<BasePart, BasePart>)m_partListBuilder.CreatePart).Call((Func<BasePart, BasePart>)SetParent);
	}

	public BasePart SetParent(BasePart part)
	{
		part.transform.parent = m_partContainer.transform;
		if (part.m_constructionIconSprite != null)
		{
			part.m_constructionIconSprite.transform.parent = m_partIconContainer.transform;
		}
		return part;
	}

	public void AddPart(BasePart newPart)
	{
		if (newPart.customPartIndex == 0)
		{
			m_partFactory.AddPart(newPart);
		}
		else
		{
			m_partFactory.AddCustomPart(newPart);
		}
	}

	public void AddExtraPart(BasePart newPart)
	{
		// Addon parts must join the MAIN factory: the build menu / GetCustomPart(type) /
		// GetCustomPartIndex / FindCustomPart all read m_partFactory, and the game's own
		// custom parts (e.g. MetalFrame 12..129) live there too. m_extraPartFactory is
		// never created, which used to throw a NullReferenceException right here.
		if (newPart.customPartIndex == 0)
		{
			m_partFactory.AddPart(newPart);
		}
		else
		{
			m_partFactory.AddCustomPart(newPart);
		}
	}

	private void ReplaceCustomPart(BasePart newPart)
	{
		PartFactory.Unit unit = m_partFactory.FindUnit(newPart.Type);
		if (unit == null)
		{
			return;
		}
		int num = unit.FindPartIndex(newPart.Index);
		if (num != -1)
		{
			BasePart basePart = unit.CustomParts[num];
			unit.CustomParts[num] = newPart;
			if (basePart.m_constructionIconSprite != null)
			{
				UnityEngine.Object.Destroy(basePart.m_constructionIconSprite.gameObject);
			}
			UnityEngine.Object.Destroy(basePart.gameObject);
		}
	}

	private void RemoveCustomPart(BasePart.PartType partType, int partIndex)
	{
		PartFactory.Unit unit = m_partFactory.FindUnit(partType);
		if (unit == null)
		{
			return;
		}
		int num = unit.FindPartIndex(partIndex);
		if (num != -1)
		{
			BasePart basePart = unit.CustomParts[num];
			unit.CustomParts.RemoveAt(num);
			if (basePart.m_constructionIconSprite != null)
			{
				UnityEngine.Object.Destroy(basePart.m_constructionIconSprite.gameObject);
			}
			UnityEngine.Object.Destroy(basePart.gameObject);
		}
	}

	public BasePart FindPart(BasePart.PartType partType)
	{
		return m_extraPartFactory?.FindPart(partType) ?? m_partFactory.FindPart(partType);
	}

	public BasePart FindCustomPart(BasePart.PartType partType, int partIndex)
	{
		return m_extraPartFactory?.FindCustomPart(partType, partIndex) ?? m_partFactory.FindCustomPart(partType, partIndex);
	}

	IPartFactoryUnit IPartFactory.FindUnit(BasePart.PartType partType)
	{
		return m_partFactory.FindUnit(partType);
	}
}
