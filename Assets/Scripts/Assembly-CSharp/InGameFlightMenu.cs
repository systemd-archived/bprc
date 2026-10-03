using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InGameFlightMenu : WPFMonoBehaviour
{
	public class PartButtonOrder : IComparer<GadgetButton>
	{
		private float middle;

		public PartButtonOrder(float middle)
		{
			this.middle = middle;
		}

		public int Compare(GadgetButton obj1, GadgetButton obj2)
		{
			float placementOrder = middle;
			float placementOrder2 = middle;
			if ((bool)obj1)
			{
				placementOrder = obj1.PlacementOrder;
			}
			if ((bool)obj1)
			{
				placementOrder2 = obj2.PlacementOrder;
			}
			if (placementOrder < placementOrder2)
			{
				return -1;
			}
			if (placementOrder > placementOrder2)
			{
				return 1;
			}
			return 0;
		}
	}

	public Button superContraptionIndexButton;

	[SerializeField]
	private GameObject cheatButton1Star;

	[SerializeField]
	private GameObject cheatButton3Stars;

	[SerializeField]
	private GameObject editorButtons;

	[SerializeField]
	private GameObject snapshotButton;

	[SerializeField]
	private GameObject[] leftButtons;

	[SerializeField]
	private GadgetButtonList buttonList;

	public GadgetButtonList ButtonList => buttonList;

	private void Awake()
	{
		GameObject original = buttonList.Buttons[0].gameObject;
		Material material = INUnity.LoadMaterial("UIPartButtonAtlas");
		if (INSettings.GetBool(INFeature.SpecialEggs))
		{
			CreateButtonWithSprite(original, BasePart.PartType.Egg, BasePart.Direction.Right, material, "EggButton_Sprite");
		}
		if (INSettings.GetBool(INFeature.SwitchableWing))
		{
			CreateButtonWithSprite(original, BasePart.PartType.Wings, BasePart.Direction.Right, material, "WoodenWingButton_Sprite");
			CreateButtonWithSprite(original, BasePart.PartType.MetalWing, BasePart.Direction.Right, material, "MetalWingButton_Sprite");
		}
		if (INSettings.GetBool(INFeature.SwitchableTail))
		{
			CreateButtonWithSprite(original, BasePart.PartType.Tailplane, BasePart.Direction.Right, material, "WoodenTailButton_Sprite");
			CreateButtonWithSprite(original, BasePart.PartType.MetalTail, BasePart.Direction.Right, material, "MetalTailButton_Sprite");
		}
		if (INSettings.GetBool(INFeature.RotatablePumpkin))
		{
			for (int i = 0; i < 4; i++)
			{
				CreateButtonWithSprite(original, BasePart.PartType.Pumpkin, (BasePart.Direction)i, material, "PumpkinButton_Sprite").transform.Find("Gadget").localRotation = Quaternion.AngleAxis(90 * i, Vector3.forward);
			}
		}
		if (INSettings.GetBool(INFeature.RotatableTNT))
		{
			BasePart.PartType partType = BasePart.PartType.TNT;
			Button button = FindButton(partType);
			for (int j = 1; j < 4; j++)
			{
				CreateButton(button.gameObject, partType, (BasePart.Direction)j).transform.Find("Gadget").localRotation = Quaternion.AngleAxis(90 * j, Vector3.forward);
			}
		}
		if (INSettings.GetBool(INFeature.RotatableGearbox))
		{
			BasePart.PartType partType2 = BasePart.PartType.Gearbox;
			Button button2 = FindButton(partType2);
			for (int k = 1; k < 4; k++)
			{
				CreateButton(button2.gameObject, partType2, (BasePart.Direction)k).transform.Find("Gadget").localRotation = Quaternion.AngleAxis(90 * k, Vector3.forward);
			}
		}
		if (INSettings.GetBool(INFeature.SeparatedPointLightButtons))
		{
			BasePart.PartType partType3 = BasePart.PartType.PointLight;
			Button button3 = FindButton(partType3);
			for (int l = 1; l < 5; l++)
			{
				if (l == 4)
				{
					CreateButtonWithSprite(button3.gameObject, partType3, BasePart.Direction.UpRight, material, "AlienPointLightButton_Sprite");
				}
				else
				{
					CreateButton(button3.gameObject, partType3, (BasePart.Direction)l).transform.Find("Gadget").localRotation = Quaternion.AngleAxis(90 * l, Vector3.forward);
				}
			}
		}
		if (INSettings.GetBool(INFeature.SeparatedSpotLightButtons))
		{
			BasePart.PartType partType4 = BasePart.PartType.SpotLight;
			Button button4 = FindButton(partType4);
			button4.transform.Find("Gadget").localRotation = Quaternion.AngleAxis(-35f, Vector3.forward);
			for (int m = 1; m < 8; m++)
			{
				GadgetButton gadgetButton = CreateButton(button4.gameObject, partType4, (BasePart.Direction)m);
				int num = -35 + ((m < 4) ? (90 * m) : (45 + 90 * (m - 4)));
				gadgetButton.transform.Find("Gadget").localRotation = Quaternion.AngleAxis(num, Vector3.forward);
			}
		}
		if (INSettings.GetBool(INFeature.FuelSystem))
		{
			CreateButtonWithSprite(original, BasePart.PartType.JetEngine, BasePart.Direction.Right, material, "JetEngineButton_Sprite");
		}
	}

	private GadgetButton FindButton(BasePart.PartType partType)
	{
		foreach (GadgetButton button in buttonList.Buttons)
		{
			if (button.m_partType == partType)
			{
				return button;
			}
		}
		return null;
	}

	private GadgetButton CreateButton(GameObject original, BasePart.PartType partType, BasePart.Direction direction)
	{
		GameObject obj = Object.Instantiate(original);
		obj.transform.parent = original.transform.parent;
		obj.transform.localScale = original.transform.localScale;
		GadgetButton component = obj.GetComponent<GadgetButton>();
		component.m_partType = partType;
		component.m_direction = direction;
		buttonList.Buttons.Add(component);
		return component;
	}

	private GadgetButton CreateButtonWithSprite(GameObject original, BasePart.PartType partType, BasePart.Direction direction, Material material, string spriteName)
	{
		GadgetButton gadgetButton = CreateButton(original, partType, direction);
		GameObject obj = gadgetButton.transform.Find("Gadget").gameObject;
		obj.GetComponent<MeshRenderer>().material = material;
		Object.Destroy(obj.GetComponent<Sprite>());
		INSerializedSprite iNSerializedSprite = obj.AddComponent<INSerializedSprite>();
		iNSerializedSprite.SpriteName = spriteName;
		iNSerializedSprite.CreateMesh();
		return gadgetButton;
	}

	private void OnEnable()
	{
		if (INSettings.GetBool(INFeature.UIPartButtonSystem) && UIPartButtonList.Settings.Enabled)
		{
			UIPartButtonList uIPartButtonList = UIPartButtonList.Instance;
			if (uIPartButtonList == null)
			{
				uIPartButtonList = Object.Instantiate(INUnity.LoadGameObject("UIPartButtonList")).GetComponent<UIPartButtonList>();
				uIPartButtonList.transform.parent = base.transform;
			}
			uIPartButtonList.gameObject.SetActive(value: true);
			buttonList.gameObject.SetActive(value: false);
		}
		else
		{
			UIPartButtonList instance = UIPartButtonList.Instance;
			if (instance != null)
			{
				instance.gameObject.SetActive(value: false);
			}
			buttonList.gameObject.SetActive(value: true);
		}
		if ((bool)WPFMonoBehaviour.levelManager && (bool)WPFMonoBehaviour.levelManager.ContraptionRunning)
		{
			SetGadgetButtonOrder(WPFMonoBehaviour.levelManager.ContraptionRunning.PartPlacements);
		}
		bool flag = WPFMonoBehaviour.levelManager.CurrentGameMode is CakeRaceMode;
		if (!Singleton<BuildCustomizationLoader>.Instance.CheatsEnabled || WPFMonoBehaviour.levelManager.m_sandbox || flag)
		{
			cheatButton1Star.SetActive(value: false);
			cheatButton3Stars.SetActive(value: false);
		}
		editorButtons.SetActive(value: false);
		leftButtons[0].SetActive(!flag);
		leftButtons[1].SetActive(flag);
		leftButtons[2].SetActive(!flag);
		StartCoroutine(WaitEndOfAwake());
		snapshotButton.SetActive(value: false);
		if (INUserSettings.Instance.LevelSceneSettings.HideRuntimeButtons)
		{
			for (int i = 0; i < 3; i++)
			{
				leftButtons[i].gameObject.GetComponent<Renderer>().enabled = false;
			}
		}
	}

	private IEnumerator WaitEndOfAwake()
	{
		yield return new WaitForEndOfFrame();
		GridLayout component = leftButtons[0].transform.parent.GetComponent<GridLayout>();
		if (component != null)
		{
			component.UpdateLayout();
		}
	}

	private void SetGadgetButtonOrder(List<Contraption.PartPlacementInfo> parts)
	{
		int num = 0;
		List<GadgetButton> buttons = buttonList.Buttons;
		for (int i = 0; i < buttons.Count; i++)
		{
			buttons[i].UpdateState();
			if (buttons[i].VisibilityCondition != null)
			{
				buttons[i].VisibilityCondition.UpdateState();
			}
			bool flag = false;
			for (int j = 0; j < parts.Count; j++)
			{
				if (CombinedTypeForGadgetButtonOrdering(parts[j].partType) == buttons[i].m_partType && parts[j].direction == buttons[i].m_direction)
				{
					buttons[i].PlacementOrder = j;
					if (parts[j].count > 0)
					{
						flag = true;
						num++;
					}
					break;
				}
			}
			buttons[i].Enabled = flag;
		}
		buttonList.Sort(new PartButtonOrder((float)num / 2f + ((num % 2 != 0) ? 0.5f : 0f)));
	}

	public static BasePart.PartType CombinedTypeForGadgetButtonOrdering(BasePart.PartType originalType)
	{
		// The gadget button a part belongs to is the stock button of the driver it was grafted
		// onto: the imported RC1 engine-family parts (Accumulator/Gyro), thrusters
		// (AndroidThruster) and motor wheels are ordered with the stock button they replace,
		// exactly like EngineBig/EngineSmall. BasePart.DriverType is the single mapping, shared
		// with UIPartButtonList.GetBasePartType (the sprite) and with Contraption's per-type
		// bookkeeping and activation, which have to agree with it or a part's own gadget button
		// cannot see the part at all.
		return BasePart.DriverType(originalType);
	}

	public void CompleteLevelWithThreeStars()
	{
	}

	public void CompleteLevelWithOneStar()
	{
	}
}
