using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIPartButtonList : MonoBehaviour
{
	public class RuntimeWrapper : INBehaviour
	{
		private UIPartButtonList m_value;

		public override StatusCode Status => StatusCode.Running;

		public RuntimeWrapper(UIPartButtonList value)
		{
			m_value = value;
		}

		public static void Create()
		{
			RuntimeWrapper behaviour = new RuntimeWrapper(Instance);
			INContraption.Instance.AddBehaviour(behaviour);
		}

		public override void Start()
		{
			m_value.Initialize();
			Contraption.Instance.ConnectedComponentsChanged += OnConnectedComponentsChanged;
		}

		public void OnConnectedComponentsChanged()
		{
			m_value.NeedsUpdate = true;
		}

		public override void OnDestroy()
		{
			m_value.FreeButtons();
		}
	}

	private class ButtonComparer : IComparer<UIPartButton>
	{
		public int Compare(UIPartButton x, UIPartButton y)
		{
			int componentRank = x.Info.ComponentRank;
			int componentRank2 = y.Info.ComponentRank;
			if (componentRank < componentRank2)
			{
				return -1;
			}
			if (componentRank > componentRank2)
			{
				return 1;
			}
			if (!Settings.HighPartTypePriority)
			{
				Vector2 averagePosition = x.AveragePosition;
				Vector2 averagePosition2 = y.AveragePosition;
				if (averagePosition.x < averagePosition2.x)
				{
					return -1;
				}
				if (averagePosition.x > averagePosition2.x)
				{
					return 1;
				}
			}
			SortedPartType sortedPartType = x.SortedPartType;
			SortedPartType sortedPartType2 = y.SortedPartType;
			if (sortedPartType < sortedPartType2)
			{
				return -1;
			}
			if (sortedPartType > sortedPartType2)
			{
				return 1;
			}
			int partIndex = x.Info.PartIndex;
			int partIndex2 = y.Info.PartIndex;
			if (partIndex < partIndex2)
			{
				return -1;
			}
			if (partIndex > partIndex2)
			{
				return 1;
			}
			int buttonIndex = x.Info.ButtonIndex;
			int buttonIndex2 = y.Info.ButtonIndex;
			if (buttonIndex < buttonIndex2)
			{
				return -1;
			}
			if (buttonIndex > buttonIndex2)
			{
				return 1;
			}
			return 0;
		}
	}

	private struct ButtonSpriteInfo
	{
		public Texture Texture;

		public Rect UVRect;

		public Vector2 Scale;

		public Quaternion Rotation;

		public ButtonSpriteInfo(Texture texture, Rect uvRect, Vector2 scale, Quaternion rotation)
		{
			Texture = texture;
			UVRect = uvRect;
			Scale = scale;
			Rotation = rotation;
		}
	}

	private struct ButtonState
	{
		public int Index;

		public UIPartButton Button;

		public ButtonState(int index, UIPartButton button)
		{
			Index = index;
			Button = button;
		}
	}

	[SerializeField]
	private Canvas m_canvas;

	[SerializeField]
	private ScrollRect m_scrollView;

	[SerializeField]
	private GameObject m_triggerButtonPrefab;

	[SerializeField]
	private GameObject m_sliderButtonPrefab;

	private bool m_needsUpdate;

	private ButtonComparer m_comparer;

	private UIPartButton m_selectedButton;

	private List<UIPartButton> m_currentButtons;

	private List<UIPartTriggerButton> m_triggerButtonPool;

	private List<UIPartSliderButton> m_sliderButtonPool;

	private Heap<(int, int)> m_componentHeap;

	private Dictionary<UIPartButtonInfo, int> m_buttonInfoMap;

	private Dictionary<UIPartButtonInfo, ButtonSpriteInfo> m_spriteInfoMap;

	private Dictionary<(BasePart, int), ButtonState> m_buttonStateMap;

	public bool NeedsUpdate
	{
		get
		{
			return m_needsUpdate;
		}
		set
		{
			m_needsUpdate = value;
		}
	}

	public UIPartButton SelectedButton => m_selectedButton;

	public List<UIPartButton> Buttons => m_currentButtons;

	public GameObject TriggerButtonPrefab => m_triggerButtonPrefab;

	public GameObject SliderButtonPrefab => m_sliderButtonPrefab;

	public static bool Enabled
	{
		get
		{
			if (Instance != null)
			{
				return Instance.gameObject.activeSelf;
			}
			return false;
		}
	}

	public static UIPartButtonList Instance { get; private set; }

	public static ButtonSettings Settings => INUserSettings.Instance.ButtonSettings;

	private void Awake()
	{
		INUiScale.Register(m_canvas);
		Instance = this;
		m_comparer = new ButtonComparer();
		m_currentButtons = new List<UIPartButton>();
		m_triggerButtonPool = new List<UIPartTriggerButton>();
		m_sliderButtonPool = new List<UIPartSliderButton>();
		m_componentHeap = new Heap<(int, int)>();
		m_buttonInfoMap = new Dictionary<UIPartButtonInfo, int>();
		m_buttonStateMap = new Dictionary<(BasePart, int), ButtonState>();
	}

	private void Initialize()
	{
		CreateSpriteInfoMap();
		UpdateButtons();
	}

	private void CreateSpriteInfoMap()
	{
		m_spriteInfoMap = new Dictionary<UIPartButtonInfo, ButtonSpriteInfo>();
		Texture texture = INUnity.LoadTexture("UIPartButtonAtlas");
		foreach (GadgetButton button in WPFMonoBehaviour.levelManager.InGameGUI.flightMenuPrefab.GetComponent<InGameFlightMenu>().ButtonList.Buttons)
		{
			UIPartButtonInfo key = GetButtonInfo(button.m_partType, (int)button.m_direction);
			Transform transform = button.transform.Find("Gadget");
			MeshRenderer componentInChildren = transform.GetComponentInChildren<MeshRenderer>();
			Sprite component = transform.GetComponent<Sprite>();
			if (componentInChildren != null && component != null && !m_spriteInfoMap.ContainsKey(key))
			{
				Texture mainTexture = componentInChildren.sharedMaterial.mainTexture;
				SpriteData spriteData = Singleton<RuntimeSpriteDatabase>.Instance.Find(component.Id);
				m_spriteInfoMap.Add(key, new ButtonSpriteInfo(mainTexture, spriteData.uv, new Vector2((float)spriteData.width * component.m_scaleX, (float)spriteData.height * component.m_scaleY), transform.transform.rotation));
			}
		}
		for (int i = 0; i < 4; i++)
		{
			UIPartButtonInfo key2 = GetButtonInfo(BasePart.PartType.Kicker, (4 - i) % 4);
			ButtonSpriteInfo spriteInfo = GetSpriteInfo(texture, "KickerButton" + (i + 1) + "_Sprite");
			m_spriteInfoMap[key2] = spriteInfo;
		}
		if (INSettings.GetBool(INFeature.SpecialUmbrellas))
		{
			FindSpriteInfo(BasePart.PartType.Umbrella, out var buttonInfo, out var spriteInfo2);
			buttonInfo.ButtonType = UIPartButtonType.Slider;
			buttonInfo.ButtonIndex = 1;
			spriteInfo2.Scale *= 0.8f;
			for (int j = 0; j < 4; j++)
			{
				buttonInfo.PartIndex = j;
				spriteInfo2.Rotation = Quaternion.AngleAxis(90 * (j - 1), Vector3.forward);
				m_spriteInfoMap[buttonInfo] = spriteInfo2;
			}
		}
		if (INSettings.GetBool(INFeature.SpecialEggs))
		{
			UIPartButtonInfo key3 = GetButtonInfo(BasePart.PartType.Egg, 0);
			ButtonSpriteInfo spriteInfo3 = GetSpriteInfo(texture, "EggButton_Sprite");
			m_spriteInfoMap.Add(key3, spriteInfo3);
		}
		if (INSettings.GetBool(INFeature.FixedPumpkin) && INSettings.GetBool(INFeature.RotatablePumpkin))
		{
			UIPartButtonInfo key4 = GetButtonInfo(BasePart.PartType.Pumpkin, 0);
			ButtonSpriteInfo spriteInfo4 = GetSpriteInfo(texture, "PumpkinButton_Sprite");
			for (int k = 0; k < 4; k++)
			{
				key4.PartIndex = k;
				spriteInfo4.Rotation = Quaternion.AngleAxis(90 * k, Vector3.forward);
				m_spriteInfoMap.Add(key4, spriteInfo4);
			}
		}
		if (INSettings.GetBool(INFeature.SwitchableWing))
		{
			UIPartButtonInfo key5 = GetButtonInfo(BasePart.PartType.Wings, 0);
			UIPartButtonInfo key6 = GetButtonInfo(BasePart.PartType.MetalWing, 0);
			ButtonSpriteInfo spriteInfo5 = GetSpriteInfo(texture, "WoodenWingButton_Sprite");
			ButtonSpriteInfo spriteInfo6 = GetSpriteInfo(texture, "MetalWingButton_Sprite");
			m_spriteInfoMap.Add(key5, spriteInfo5);
			m_spriteInfoMap.Add(key6, spriteInfo6);
		}
		if (INSettings.GetBool(INFeature.SwitchableTail))
		{
			UIPartButtonInfo key7 = GetButtonInfo(BasePart.PartType.Tailplane, 0);
			UIPartButtonInfo key8 = GetButtonInfo(BasePart.PartType.MetalTail, 0);
			ButtonSpriteInfo spriteInfo7 = GetSpriteInfo(texture, "WoodenTailButton_Sprite");
			ButtonSpriteInfo spriteInfo8 = GetSpriteInfo(texture, "MetalTailButton_Sprite");
			m_spriteInfoMap.Add(key7, spriteInfo7);
			m_spriteInfoMap.Add(key8, spriteInfo8);
		}
		if (INSettings.GetBool(INFeature.RotatableTNT))
		{
			FindSpriteInfo(BasePart.PartType.TNT, out var buttonInfo2, out var spriteInfo9);
			for (int l = 1; l < 4; l++)
			{
				buttonInfo2.PartIndex = l;
				spriteInfo9.Rotation = Quaternion.AngleAxis(90 * l, Vector3.forward);
				m_spriteInfoMap.Add(buttonInfo2, spriteInfo9);
			}
		}
		if (INSettings.GetBool(INFeature.RotatableGearbox))
		{
			FindSpriteInfo(BasePart.PartType.Gearbox, out var buttonInfo3, out var spriteInfo10);
			for (int m = 1; m < 4; m++)
			{
				buttonInfo3.PartIndex = m;
				spriteInfo10.Rotation = Quaternion.AngleAxis(90 * m, Vector3.forward);
				m_spriteInfoMap.Add(buttonInfo3, spriteInfo10);
			}
		}
		if (INSettings.GetBool(INFeature.SeparatedPointLightButtons))
		{
			FindSpriteInfo(BasePart.PartType.PointLight, out var buttonInfo4, out var spriteInfo11);
			for (int n = 1; n < 4; n++)
			{
				buttonInfo4.PartIndex = n;
				spriteInfo11.Rotation = Quaternion.AngleAxis(90 * n, Vector3.forward);
				m_spriteInfoMap.Add(buttonInfo4, spriteInfo11);
			}
			buttonInfo4.PartIndex = 4;
			spriteInfo11 = GetSpriteInfo(texture, "AlienPointLightButton_Sprite");
			m_spriteInfoMap.Add(buttonInfo4, spriteInfo11);
		}
		if (INSettings.GetBool(INFeature.SeparatedSpotLightButtons))
		{
			FindSpriteInfo(BasePart.PartType.SpotLight, out var buttonInfo5, out var spriteInfo12);
			spriteInfo12.Rotation = Quaternion.AngleAxis(-35f, Vector3.forward);
			m_spriteInfoMap[buttonInfo5] = spriteInfo12;
			for (int num = 1; num < 8; num++)
			{
				buttonInfo5.PartIndex = num;
				spriteInfo12.Rotation = Quaternion.AngleAxis(-35 + ((num < 4) ? (90 * num) : (45 + 90 * (num - 4))), Vector3.forward);
				m_spriteInfoMap.Add(buttonInfo5, spriteInfo12);
			}
		}
		if (INSettings.GetBool(INFeature.FuelSystem))
		{
			UIPartButtonInfo key9 = GetButtonInfo(BasePart.PartType.JetEngine, -1);
			ButtonSpriteInfo spriteInfo13 = GetSpriteInfo(texture, "JetEngineButton_Sprite");
			ButtonSpriteInfo spriteInfo14 = GetSpriteInfo(texture, "JetEngineAllButton_Sprite");
			m_spriteInfoMap.Add(key9, spriteInfo14);
			for (int num2 = 1; num2 < 4; num2++)
			{
				key9.ButtonType = ((num2 >= 2) ? UIPartButtonType.Slider : UIPartButtonType.Trigger);
				key9.ButtonIndex = num2;
				m_spriteInfoMap.Add(key9, spriteInfo13);
			}
			key9 = GetButtonInfo(BasePart.PartType.JetEngine, -1);
			key9.ButtonIndex = 4;
			spriteInfo13 = GetSpriteInfo(texture, "ValveFuelTubeButton_Sprite");
			m_spriteInfoMap.Add(key9, spriteInfo13);
		}
		if (INSettings.GetBool(INFeature.ElectricalSystem))
		{
			UIPartButtonInfo key10 = GetButtonInfo(BasePart.PartType.ElectricalPart, -1);
			key10.ButtonType = UIPartButtonType.Trigger;
			key10.PartIndex = 0;
			ButtonSpriteInfo spriteInfo15 = GetSpriteInfo(texture, "SwitchButton_Sprite");
			m_spriteInfoMap.Add(key10, spriteInfo15);
			key10.ButtonType = UIPartButtonType.Trigger;
			key10.PartIndex = 1;
			spriteInfo15 = GetSpriteInfo(texture, "SPDTSwitchButton_Sprite");
			m_spriteInfoMap.Add(key10, spriteInfo15);
			key10.ButtonType = UIPartButtonType.Slider;
			key10.PartIndex = 2;
			spriteInfo15 = GetSpriteInfo(texture, "VariableResistorButton_Sprite");
			m_spriteInfoMap.Add(key10, spriteInfo15);
			key10.ButtonType = UIPartButtonType.Trigger;
			key10.PartIndex = 3;
			spriteInfo15 = GetSpriteInfo(texture, "VccButton_Sprite");
			m_spriteInfoMap.Add(key10, spriteInfo15);
			key10.ButtonType = UIPartButtonType.Trigger;
			key10.PartIndex = 4;
			spriteInfo15 = GetSpriteInfo(texture, "PointCharge_Sprite");
			m_spriteInfoMap.Add(key10, spriteInfo15);
			key10.PartIndex = 7;
			spriteInfo15 = GetSpriteInfo(texture, "PointCharge_Sprite", Quaternion.AngleAxis(45f, Vector3.forward));
			m_spriteInfoMap.Add(key10, spriteInfo15);
			key10.ButtonType = UIPartButtonType.Trigger;
			key10.PartIndex = 5;
			spriteInfo15 = GetSpriteInfo(texture, "ElectricFieldGenerator_Sprite");
			m_spriteInfoMap.Add(key10, spriteInfo15);
			key10.ButtonType = UIPartButtonType.Trigger;
			key10.PartIndex = 6;
			spriteInfo15 = GetSpriteInfo(texture, "MagneticFieldGenerator_Sprite");
			m_spriteInfoMap.Add(key10, spriteInfo15);
		}
		if (INSettings.GetBool(INFeature.MechanicalSystem))
		{
			UIPartButtonInfo key11 = GetButtonInfo(BasePart.PartType.MechanicalPart, -1);
			key11.ButtonType = UIPartButtonType.Trigger;
			key11.PartIndex = 0;
			ButtonSpriteInfo spriteInfo16 = GetSpriteInfo(texture, "MechanicalGate_Sprite");
			m_spriteInfoMap.Add(key11, spriteInfo16);
			key11.ButtonType = UIPartButtonType.Slider;
			key11.ButtonIndex = 1;
			m_spriteInfoMap.Add(key11, spriteInfo16);
		}
		static UIPartButtonInfo GetButtonInfo(BasePart.PartType partType, int partIndex)
		{
			return new UIPartButtonInfo(UIPartButtonType.Trigger, 0, partType, partIndex, -1);
		}
	}

	private ButtonSpriteInfo GetSpriteInfo(Texture texture, string name)
	{
		return GetSpriteInfo(texture, name, Quaternion.identity);
	}

	private ButtonSpriteInfo GetSpriteInfo(Texture texture, string name, Quaternion rotation)
	{
		INSpriteData iNSpriteData = INSpriteManager.Instance.GetAtlasData(texture.name)[name];
		return new ButtonSpriteInfo(texture, iNSpriteData.FlippedUVRect, iNSpriteData.PixelSize, rotation);
	}

	private bool FindSpriteInfo(BasePart.PartType partType, out UIPartButtonInfo buttonInfo, out ButtonSpriteInfo spriteInfo)
	{
		foreach (KeyValuePair<UIPartButtonInfo, ButtonSpriteInfo> item in m_spriteInfoMap)
		{
			if (item.Key.PartType == partType)
			{
				buttonInfo = item.Key;
				spriteInfo = item.Value;
				return true;
			}
		}
		buttonInfo = default(UIPartButtonInfo);
		spriteInfo = default(ButtonSpriteInfo);
		return false;
	}

	private bool FindSpriteInfo(UIPartButtonInfo buttonInfo, out ButtonSpriteInfo spriteInfo)
	{
		buttonInfo.PartType = GetBasePartType(buttonInfo.PartType);
		buttonInfo.ComponentRank = -1;
		if (buttonInfo.PartType == BasePart.PartType.JetEngine)
		{
			buttonInfo.PartIndex = -1;
		}
		return m_spriteInfoMap.TryGetValue(buttonInfo, out spriteInfo);
	}

	private void Update()
	{
		if (m_needsUpdate)
		{
			UpdateButtons();
			m_needsUpdate = false;
		}
		int num = -1;
		int num2 = 0;
		for (int i = 0; i < 26; i++)
		{
			if (Input.GetKeyDown((KeyCode)(97 + i)))
			{
				num = i;
				break;
			}
		}
		for (int j = 0; j < 9; j++)
		{
			if (Input.GetKey((KeyCode)(49 + j)))
			{
				num2 = j + 1;
				break;
			}
		}
		if (num == -1)
		{
			return;
		}
		if (Settings.ButtonIndexQwerty)
		{
			// 部件索引优化：按键字母按键盘顺序映射到显示序号，保证“显示什么键就按什么键”
			const string qwertyOrder = "QWERTYUIOPASDFGHJKLZXCVBNM";
			int displayIndex = qwertyOrder.IndexOf((char)(65 + num));
			if (displayIndex >= 0)
			{
				num = displayIndex;
			}
		}
		int num3 = num2 * 26 + num;
		if (num3 < m_currentButtons.Count)
		{
			m_selectedButton = m_currentButtons[num3];
			if (m_selectedButton is UIPartTriggerButton uIPartTriggerButton)
			{
				uIPartTriggerButton.OnTriggered();
			}
		}
	}

	private void UpdateButtons()
	{
		SaveButtonStates();
		CreateButtons();
		RenderButtons();
	}

	private void CreateButtons()
	{
		int connectedComponentCount = Contraption.Instance.ConnectedComponentCount;
		int num = Math.Min(Settings.MaxSeparationCount, connectedComponentCount);
		m_componentHeap.Clear();
		m_componentHeap.PushRange(GetComponentInfo());
		int[] array = new int[connectedComponentCount];
		Array.Fill(array, num);
		for (int i = 0; i < num; i++)
		{
			array[m_componentHeap.Pop().Item2] = i;
		}
		m_buttonInfoMap.Clear();
		bool[] array2 = new bool[m_currentButtons.Count];
		List<UIPartButton> list = new List<UIPartButton>(m_currentButtons.Count);
		foreach (BasePart part in Contraption.Instance.Parts)
		{
			if (!part.IsTriggerable())
			{
				continue;
			}
			foreach (UIPartButtonInfo item in GetAllButtonInfo(part))
			{
				UIPartButtonInfo current2 = item;
				int componentRank = array[current2.ComponentIndex];
				current2.ComponentRank = componentRank;
				if (!m_buttonInfoMap.TryGetValue(current2, out var value))
				{
					value = list.Count;
					m_buttonInfoMap.Add(current2, value);
					list.Add(null);
				}
				m_buttonStateMap.TryGetValue((part, current2.ButtonIndex), out var value2);
				if (list[value] == null && value2.Button != null && !array2[value2.Index])
				{
					list[value] = value2.Button;
					array2[value2.Index] = true;
				}
			}
		}
		for (int j = 0; j < m_currentButtons.Count; j++)
		{
			UIPartButton uIPartButton = m_currentButtons[j];
			if (!array2[j])
			{
				FreeButton(uIPartButton);
			}
			else
			{
				uIPartButton.Parts.Clear();
			}
		}
		array2 = new bool[list.Count];
		foreach (BasePart part2 in Contraption.Instance.Parts)
		{
			if (!part2.IsTriggerable())
			{
				continue;
			}
			foreach (UIPartTriggerButtonInfo item2 in part2.GetTriggerButtonInfo())
			{
				UIPartButtonInfo value3 = item2.Value;
				int componentRank2 = array[value3.ComponentIndex];
				value3.ComponentRank = componentRank2;
				int num2 = m_buttonInfoMap[value3];
				UIPartTriggerButton uIPartTriggerButton = list[num2] as UIPartTriggerButton;
				if (uIPartTriggerButton == null)
				{
					uIPartTriggerButton = (UIPartTriggerButton)(list[num2] = AllocateButton<UIPartTriggerButton>());
				}
				if (array2[num2])
				{
					uIPartTriggerButton.Parts.Add(part2);
					continue;
				}
				array2[num2] = true;
				uIPartTriggerButton.Parts.Add(part2);
				uIPartTriggerButton.SetInfo(value3);
				uIPartTriggerButton.SetConsistent(item2.Consistent);
				uIPartTriggerButton.SetMultiple(item2.Multiple);
				SetButtonSprite(value3, uIPartTriggerButton);
			}
			foreach (UIPartSliderButtonInfo item3 in part2.GetSliderButtonInfo())
			{
				UIPartButtonInfo value4 = item3.Value;
				int componentRank3 = array[value4.ComponentIndex];
				value4.ComponentRank = componentRank3;
				int num3 = m_buttonInfoMap[value4];
				UIPartSliderButton uIPartSliderButton = list[num3] as UIPartSliderButton;
				if (uIPartSliderButton == null)
				{
					uIPartSliderButton = (UIPartSliderButton)(list[num3] = AllocateButton<UIPartSliderButton>());
				}
				if (array2[num3])
				{
					uIPartSliderButton.Parts.Add(part2);
					continue;
				}
				array2[num3] = true;
				uIPartSliderButton.Parts.Add(part2);
				uIPartSliderButton.SetInfo(value4);
				uIPartSliderButton.SetRange(item3.Range);
				SetButtonSprite(value4, uIPartSliderButton);
			}
		}
		m_currentButtons = list;
		static IEnumerable<UIPartButtonInfo> GetAllButtonInfo(BasePart part)
		{
			foreach (UIPartTriggerButtonInfo item4 in part.GetTriggerButtonInfo())
			{
				yield return item4.Value;
			}
			foreach (UIPartSliderButtonInfo item5 in part.GetSliderButtonInfo())
			{
				yield return item5.Value;
			}
		}
		static IEnumerable<(int, int)> GetComponentInfo()
		{
			int componentCount = Contraption.Instance.ConnectedComponentCount;
			for (int k = 0; k < componentCount; k++)
			{
				yield return (-Contraption.Instance.ComponentPartCount(k), k);
			}
		}
	}

	private void SetButtonSprite(UIPartButtonInfo buttonInfo, UIPartButton button)
	{
		buttonInfo.ComponentRank = -1;
		buttonInfo.PartType = GetBasePartType(buttonInfo.PartType);
		if (FindSpriteInfo(buttonInfo, out var spriteInfo))
		{
			button.SetSprite(enabled: true, spriteInfo.Texture, spriteInfo.UVRect, spriteInfo.Scale / 0.7f, spriteInfo.Rotation);
		}
		else
		{
			button.SetSprite(enabled: false, null, Rect.zero, Vector2.zero, Quaternion.identity);
		}
	}

	public void RenderButtons()
	{
		if (m_currentButtons.Count == 0)
		{
			return;
		}
		foreach (UIPartButton currentButton in m_currentButtons)
		{
			currentButton.Initialize();
		}
		m_currentButtons.Sort(m_comparer);
		bool displayButtonIndex = Settings.DisplayButtonIndex;
		bool buttonIndexQwerty = Settings.ButtonIndexQwerty;
		const string qwertyOrder = "QWERTYUIOPASDFGHJKLZXCVBNM";
		for (int i = 0; i < m_currentButtons.Count; i++)
		{
			char c = (buttonIndexQwerty ? qwertyOrder[i % 26] : (char)(65 + i % 26));
			char c2 = (char)(48 + i / 26);
			string text = (displayButtonIndex ? ((i < 26) ? c.ToString() : (c.ToString() + c2)) : string.Empty);
			m_currentButtons[i].DisplayIndexText(text);
		}
		int num = 0;
		foreach (UIPartButton currentButton2 in m_currentButtons)
		{
			num += 1 + currentButton2.SubButtonCount;
		}
		float num2 = 1029.375f * ((float)Screen.width / (float)Screen.height);
		int j;
		for (j = 1; num > j * (int)(num2 / GetPadding(j + 1)); j++)
		{
		}
		float num3 = GetPadding(j);
		float min = GetPadding(j + 1);
		int num4 = num / j + ((num % j != 0) ? 1 : 0);
		if (Settings.LayoutMode == 1 && j > 1)
		{
			num4 = Math.Max(num4, (int)(num2 / num3));
		}
		float num5 = Math.Clamp(num2 / (float)num4, min, num3);
		float num6 = num5 / 240f;
		Vector3 localScale = new Vector3(num6, num6, 1f);
		int num7 = 0;
		float num8 = 10f;
		RectTransform content = m_scrollView.content;
		RectTransform rectTransform = (RectTransform)m_scrollView.transform;
		content.sizeDelta = new Vector2(content.sizeDelta.x, (float)j * num5 + num8);
		rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, Math.Min((float)j * num5 + num8, 360f * Settings.ScrollViewHeightScale));
		foreach (UIPartButton currentButton3 in m_currentButtons)
		{
			float num9 = (float)(-(num4 - 1)) / 2f + (float)(num7 % num4);
			float num10 = (float)(-(j - 1)) / 2f + (float)(num7 / num4);
			Vector2 vector = new Vector2(num5 * num9, (0f - num5) * num10 + num8);
			RectTransform obj = (RectTransform)currentButton3.transform;
			obj.localScale = localScale;
			obj.anchoredPosition = vector;
			num7++;
			if (currentButton3.SubButtonCount == 0)
			{
				continue;
			}
			foreach (UIPartButton subButton in currentButton3.SubButtons)
			{
				num9 = (float)(-(num4 - 1)) / 2f + (float)(num7 % num4);
				num10 = (float)(-(j - 1)) / 2f + (float)(num7 / num4);
				Vector2 vector2 = new Vector2(num5 * num9, (0f - num5) * num10 + num8);
				((RectTransform)subButton.transform).anchoredPosition = (vector2 - vector) / num6;
				num7++;
			}
		}
		static float GetPadding(int n)
		{
			return (float)Math.Max(200 - 20 * n, 140) * Settings.ButtonScale;
		}
	}

	private void SaveButtonStates()
	{
		m_buttonStateMap.Clear();
		for (int i = 0; i < m_currentButtons.Count; i++)
		{
			UIPartButton uIPartButton = m_currentButtons[i];
			foreach (BasePart part in uIPartButton.Parts)
			{
				m_buttonStateMap.Add((part, uIPartButton.Info.ButtonIndex), new ButtonState(i, uIPartButton));
			}
		}
	}

	private void FreeButtons()
	{
		foreach (UIPartButton currentButton in m_currentButtons)
		{
			if (currentButton != null)
			{
				FreeButton(currentButton);
			}
		}
		m_currentButtons.Clear();
	}

	private T AllocateButton<T>() where T : UIPartButton
	{
		if (typeof(T) == typeof(UIPartTriggerButton))
		{
			List<UIPartTriggerButton> triggerButtonPool = m_triggerButtonPool;
			if (triggerButtonPool.Count == 0)
			{
				return CreateButton<T>();
			}
			T val = (T)(UIPartButton)triggerButtonPool[triggerButtonPool.Count - 1];
			triggerButtonPool.RemoveAt(triggerButtonPool.Count - 1);
			val.gameObject.SetActive(value: true);
			return val;
		}
		if (typeof(T) == typeof(UIPartSliderButton))
		{
			List<UIPartSliderButton> sliderButtonPool = m_sliderButtonPool;
			if (sliderButtonPool.Count == 0)
			{
				return CreateButton<T>();
			}
			T val2 = (T)(UIPartButton)sliderButtonPool[sliderButtonPool.Count - 1];
			sliderButtonPool.RemoveAt(sliderButtonPool.Count - 1);
			val2.gameObject.SetActive(value: true);
			return val2;
		}
		return null;
	}

	private T CreateButton<T>() where T : UIPartButton
	{
		GameObject original = null;
		if (typeof(T) == typeof(UIPartTriggerButton))
		{
			original = m_triggerButtonPrefab;
		}
		else if (typeof(T) == typeof(UIPartSliderButton))
		{
			original = m_sliderButtonPrefab;
		}
		T component = UnityEngine.Object.Instantiate(original).GetComponent<T>();
		component.transform.SetParent(m_scrollView.content, worldPositionStays: false);
		component.gameObject.SetActive(value: true);
		return component;
	}

	private void FreeButton(UIPartButton button)
	{
		button.Reset();
		button.gameObject.SetActive(value: false);
		if (button is UIPartTriggerButton item)
		{
			m_triggerButtonPool.Add(item);
		}
		else if (button is UIPartSliderButton item2)
		{
			m_sliderButtonPool.Add(item2);
		}
	}

	private static BasePart.PartType GetBasePartType(BasePart.PartType partType)
	{
		// The RC1 (imported Android) parts got their own part types but kept the stock driver,
		// so the button they produce has to be the stock button they produced before the split:
		//   Accumulator/Gyro (L1_tank, L1_accumulator, L1_gyro - Engine driver) -> Engine
		//   AndroidThruster (L1_turbo, L1_jet - JetEngine driver)              -> JetEngine
		//   AndroidMotorWheel/AndroidGenerator/AndroidBigfootWheel/AndroidTrack
		//     (MotorWheel / OffRoadWheel driver)                               -> MotorWheel
		// All three are the same normalisation the stock EngineBig/EngineSmall already get;
		// without it FindSpriteInfo cannot resolve the type, SetButtonSprite is called with
		// enabled: false and the trigger button of the part is drawn blank (and, because the
		// part also had no joints, with its own connected-component colour).
		// The mapping itself is BasePart.DriverType, because Contraption's per-type bookkeeping
		// and activation have to normalise exactly the same way.
		return BasePart.DriverType(partType);
	}
}
