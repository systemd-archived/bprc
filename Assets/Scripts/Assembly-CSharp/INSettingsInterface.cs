using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Innovation;
using UnityEngine;
using UnityEngine.UI;

public class INSettingsInterface : MonoBehaviour
{
	private class SettingsGroup
	{
		private SettingsBase m_source;

		private GameObject m_gameObject;

		private Text m_groupName;

		private UITextLocale m_groupNameLocale;

		private List<SettingsItem> m_items;

		public SettingsBase Source => m_source;

		public GameObject GameObject => m_gameObject;

		public Text GroupName => m_groupName;

		public UITextLocale GroupNameLocale => m_groupNameLocale;

		public List<SettingsItem> Items => m_items;

		public SettingsGroup(SettingsBase source, GameObject gameObject)
		{
			m_source = source;
			m_gameObject = gameObject;
			// The group template may be this project's own or Heat UI's settings header. Heat UI labels
			// it with TextMeshPro under "Text" and ships no locale component, which EnsureLocale adds.
			m_groupNameLocale = HeatUiSkin.EnsureLocale(gameObject, HeatUiSkin.LegacyGroupNameChild);
			m_groupName = m_groupNameLocale != null ? m_groupNameLocale.GetComponent<Text>() : null;
			m_items = new List<SettingsItem>();
		}

		public void Render()
		{
			foreach (SettingsItem item in m_items)
			{
				item.Render();
			}
		}
	}

	private abstract class SettingsItem
	{
		protected SettingsGroup m_group;

		protected GameObject m_gameObject;

		protected Text m_name;

		protected UITextLocale m_nameLocale;

		public SettingsGroup Group => m_group;

		public GameObject GameObject => m_gameObject;

		public Text Name => m_name;

		public UITextLocale NameLocale => m_nameLocale;

		public SettingsItem(SettingsGroup group, GameObject gameObject)
		{
			m_group = group;
			m_gameObject = gameObject;
			// Same as the group: a Heat UI settings element labels the row with TextMeshPro, so the
			// locale is attached there and the legacy Text handle is left null.
			m_nameLocale = HeatUiSkin.EnsureLocale(gameObject, HeatUiSkin.LegacyNameChild);
			m_name = m_nameLocale != null ? m_nameLocale.GetComponent<Text>() : null;
		}

		public abstract object GetBoxedValue();

		public abstract void SetBoxedValue(object value);

		public abstract void Render();

		/// <summary>
		/// Re-reads anything this row shows that is not one of its labels, after the language changed.
		/// </summary>
		public virtual void RefreshLocalization()
		{
		}

		// 从源重新读入到 UI 代理并回显（供重置后同步）
		public abstract void SyncFromSource();
	}

	private abstract class SettingsItem<T> : SettingsItem
	{
		protected DependencyProperty<T> m_property;

		private Func<T> m_getter;

		public SettingsItem(SettingsGroup group, GameObject gameObject)
			: base(group, gameObject)
		{
			m_property = new DependencyProperty<T>();
		}

		// 从源重新读入到 UI 代理并回显：用于重置后把"源=默认"写回 UI，保证再次编辑能正确生效。
		public override void SyncFromSource()
		{
			if (m_getter != null)
			{
				SetValue(m_getter());
			}
		}

		public void Bind(string propertyName, Func<T> getter, Action<T> setter)
		{
			m_getter = getter;
			Binding.Bind(m_group.Source, propertyName, delegate
			{
				SetValue(getter());
			});
			Binding.Bind(delegate
			{
				setter(GetValue());
				SetValue(getter());
			});
		}

		public void Bind(PropertyInfo property)
		{
			Func<T> getter = (Func<T>)Delegate.CreateDelegate(typeof(Func<T>), m_group.Source, property.GetGetMethod());
			Action<T> setter = (Action<T>)Delegate.CreateDelegate(typeof(Action<T>), m_group.Source, property.GetSetMethod());
			Bind(property.Name, getter, setter);
		}

		public override object GetBoxedValue()
		{
			return GetValue();
		}

		public override void SetBoxedValue(object value)
		{
			SetValue((T)value);
		}

		public virtual T GetValue()
		{
			return m_property.Value;
		}

		public virtual void SetValue(T value)
		{
			m_property.Value = value;
		}
	}

	private class SettingsItemWithSelector<T> : SettingsItem<T>
	{
		private readonly Michsky.UI.Heat.HorizontalSelector m_selector;
		private readonly T[] m_values;

		public SettingsItemWithSelector(SettingsGroup group, GameObject row, T[] values) : base(group, row)
		{
			m_values = values;
			m_selector = row.GetComponentInChildren<Michsky.UI.Heat.HorizontalSelector>(true);
			m_selector.useLocalization = false;
			m_selector.invokeOnAwake = false;
			m_selector.saveSelected = false;
			m_selector.items.Clear();
			foreach (T value in values)
			{
				m_selector.items.Add(new Michsky.UI.Heat.HorizontalSelector.Item { itemTitle = Title(value) });
			}
			m_selector.defaultIndex = 0;
			m_selector.InitializeSelector();
			m_selector.onValueChanged.AddListener(index => SetValue(m_values[index]));
		}

		/// <summary>
		/// What the selector shows for one of its choices.
		/// </summary>
		/// <remarks>
		/// The title is built from the value rather than resolved from a locale id, because these are not
		/// sentences: an enum member's name, a layout number, a file format. The ones that name something
		/// in words instead - a language, a layout - are built here so a language change can build them
		/// again, which is what <see cref="RefreshLocalization"/> does.
		/// </remarks>
		private static string Title(T value)
		{
			if (typeof(T) == typeof(InterfaceLanguage))
			{
				return InterfaceLanguages.Title((InterfaceLanguage)(object)value);
			}
			string title = value.ToString();
			if (typeof(T) == typeof(int)) { title = (HeatUiOptionHelp.Chinese ? "方案 " : "Layout ") + (Convert.ToInt32(value) + 1); }
			if (title == "ALL" && HeatUiOptionHelp.Chinese) { title = "全部格式"; }
			return title;
		}

		public override void SetValue(T value)
		{
			if (EqualityComparer<T>.Default.Equals(m_property.RawValue, value)) { return; }
			base.SetValue(value);
			m_group.Source.Apply();
			Render();
		}

		public override void Render()
		{
			m_selector.index = Mathf.Max(0, Array.IndexOf(m_values, m_property.RawValue));
			m_selector.defaultIndex = m_selector.index;
			m_selector.UpdateUI();
		}

		public override void RefreshLocalization()
		{
			for (int index = 0; index < m_values.Length && index < m_selector.items.Count; index++)
			{
				m_selector.items[index].itemTitle = Title(m_values[index]);
			}
			m_selector.UpdateUI();
		}
	}

	private class SettingsItemWithSlider : SettingsItem<float>
	{
		private readonly Slider m_slider;
		private readonly Michsky.UI.Heat.SliderManager m_manager;

		public SettingsItemWithSlider(SettingsGroup group, GameObject row) : base(group, row)
		{
			m_slider = row.GetComponentInChildren<Slider>(true);
			m_manager = m_slider.GetComponent<Michsky.UI.Heat.SliderManager>();
			m_slider.minValue = 0;
			m_slider.maxValue = 1;
			m_slider.wholeNumbers = false;
			m_slider.onValueChanged.AddListener(SetValue);
		}

		public override void SetValue(float value)
		{
			if (m_property.RawValue == value) { return; }
			base.SetValue(value);
			m_group.Source.Apply();
			Render();
		}

		public override void Render()
		{
			m_slider.SetValueWithoutNotify(m_property.RawValue);
			m_manager.UpdateUI();
		}
	}

	private class SettingsItemWithToggle : SettingsItem<bool>
	{
		private SettingsToggleBinding m_toggle;

		public SettingsToggleBinding Toggle => m_toggle;

		public SettingsItemWithToggle(SettingsGroup group, GameObject gameObject)
			: base(group, gameObject)
		{
			// Heat UI's switch drives the row when the template came from Heat UI; the project's own
			// ToggleSwitch is the fallback, so a row from either template keeps working.
			m_toggle = new SettingsToggleBinding(gameObject);
			if (!m_toggle.IsAvailable)
			{
				Debug.LogWarning("Settings row '" + gameObject.name + "' has no toggle: neither a Heat UI switch nor a ToggleSwitch child.");
				return;
			}
			m_toggle.Changed += OnValueChanged;
		}

		public override void SetBoxedValue(object value)
		{
			if (value is bool value2)
			{
				SetValue(value2);
				return;
			}
			if (value is string value3 && bool.TryParse(value3, out var result))
			{
				SetValue(result);
				return;
			}
			throw new InvalidCastException();
		}

		public override void SetValue(bool value)
		{
			if (m_property.RawValue != value)
			{
				base.SetValue(value);
				m_group.Source.Apply();
				Render();
			}
		}

		private void OnValueChanged(bool value)
		{
			SetValue(value);
			Render();
		}

		public override void Render()
		{
			if (m_toggle.IsAvailable)
			{
				m_toggle.IsOn = m_property.RawValue;
			}
		}
	}

	/// <summary>
	/// An action row: like every other row, except that its value slot is a button that runs code rather
	/// than a control that writes a setting.
	/// </summary>
	/// <remarks>
	/// The Heat UI button template takes over the slot the row reserved for its switch, which is what the
	/// input-field row does with a text field. Heat UI writes its caption into all three of its state
	/// labels from its own field and does that again whenever the button is enabled - and this panel
	/// enables and disables its pages - so both of those are switched off and the caption is applied from
	/// the locale id instead.
	/// </remarks>
	private class SettingsItemWithButton : SettingsItem
	{
		private readonly HeatUiButtonBinding m_button;

		private string m_labelLocale;

		public SettingsItemWithButton(SettingsGroup group, GameObject gameObject, UnityEngine.Events.UnityAction action)
			: base(group, gameObject)
		{
			GameObject buttonObject = HeatUiSkin.ReplaceValueSlot(gameObject, HeatUiSkin.FindValueSlot(gameObject), HeatUiSkin.ButtonTemplate, "HeatActionButton");
			if (buttonObject == null)
			{
				Debug.LogWarning("Settings row '" + gameObject.name + "' has no action button: Heat UI's button template is not staged.");
				return;
			}

			Michsky.UI.Heat.ButtonManager manager = buttonObject.GetComponent<Michsky.UI.Heat.ButtonManager>();
			if (manager != null)
			{
				manager.useLocalization = false;
				manager.bypassUpdateOnEnable = true;
				manager.autoFitContent = false;
			}

			UnityEngine.UI.Button button = buttonObject.GetComponent<UnityEngine.UI.Button>();
			if (button == null)
			{
				button = buttonObject.AddComponent<UnityEngine.UI.Button>();
			}
			button.onClick.AddListener(action);
			m_button = new HeatUiButtonBinding(button, manager);
		}

		public HeatUiButtonBinding Button => m_button;

		/// <summary>Captions the button, in the language that is on now and in any later one.</summary>
		public void SetLabelLocale(string localeName)
		{
			m_labelLocale = localeName;
			RefreshLocalization();
		}

		public override void RefreshLocalization()
		{
			if (m_button == null || string.IsNullOrEmpty(m_labelLocale))
			{
				return;
			}
			m_button.Label = INLocalization.Instance != null ? INLocalization.Instance.GetText(m_labelLocale) : m_labelLocale;
		}

		public override object GetBoxedValue()
		{
			return null;
		}

		public override void SetBoxedValue(object value)
		{
		}

		public override void Render()
		{
		}

		public override void SyncFromSource()
		{
		}
	}

	private class SettingsItemWithInputField<T> : SettingsItem<T>
	{
		private SettingsInputBinding m_inputField;

		private Parsable.TryParser<T> m_parser;

		public SettingsInputBinding InputField => m_inputField;

		public SettingsItemWithInputField(SettingsGroup group, GameObject gameObject, Parsable.TryParser<T> parser)
			: base(group, gameObject)
		{
			// Heat UI ships no settings element with a text field, so its input field takes over the
			// slot the row reserved - the switch on a Heat UI row, the InputField child on this
			// project's own template - and whatever was there is removed.
			HeatUiSkin.ReplaceValueSlot(gameObject, HeatUiSkin.FindValueSlot(gameObject), HeatUiSkin.InputFieldTemplate, "HeatInputField");

			// Heat UI's TextMeshPro field drives the row when the template came from Heat UI; the
			// project's own InputField child is the fallback.
			m_inputField = new SettingsInputBinding(gameObject);
			m_parser = parser;
			if (!m_inputField.IsAvailable)
			{
				Debug.LogWarning("Settings row '" + gameObject.name + "' has no input field: neither a TextMeshPro field nor an InputField child.");
				return;
			}
			m_inputField.EndEdit += OnEndEdit;
			Render();
		}

		public override void SetBoxedValue(object value)
		{
			if (value is T value2)
			{
				SetValue(value2);
				return;
			}
			if (value is string s && m_parser(s, CultureInfo.CurrentCulture, out var result))
			{
				SetValue(result);
				return;
			}
			throw new InvalidCastException();
		}

		public override void SetValue(T value)
		{
			if (!EqualityComparer<T>.Default.Equals(m_property.RawValue, value))
			{
				base.SetValue(value);
				m_group.Source.Apply();
				Render();
			}
		}

		private void OnEndEdit(string text)
		{
			if (m_parser(text, CultureInfo.CurrentCulture, out var result))
			{
				SetValue(result);
			}
			Render();
		}

		public override void Render()
		{
			if (!m_inputField.IsAvailable)
			{
				return;
			}
			T rawValue = m_property.RawValue;
			m_inputField.Text = ((rawValue != null) ? rawValue.ToString() : null);
		}
	}

	/// <summary>
	/// Engine selector row.
	/// </summary>
	/// <remarks>
	/// bpsim_2023.0 renders its engine enum as a dropdown in the row's value slot — see
	/// <see cref="EngineDropdown"/> — and this row now does the same. The previous implementation
	/// built a strip of one button per engine, which had no room for the availability reason and did
	/// not read as a single choice.
	/// </remarks>
	private class SettingsItemWithEngineList : SettingsItem<PhysicsEngine>
	{
		private EngineDropdown m_dropdown;

		public SettingsItemWithEngineList(SettingsGroup group, GameObject gameObject)
			: base(group, gameObject)
		{
			// The row reserves a slot for its own control: Heat UI's switch on a Heat UI row, this
			// project's ToggleSwitch and InputField on its own template. The engine choice replaces all
			// of them with a single dropdown placed where the slot was.
			HeatUiSkin.DestroyChild(gameObject, "ToggleSwitch");
			Transform slot = HeatUiSkin.FindValueSlot(gameObject);
			Build(slot);
			if (slot != null)
			{
				slot.gameObject.SetActive(false);
				UnityEngine.Object.Destroy(slot.gameObject);
			}
		}

		/// <summary>
		/// Fills the row's value slot with the engine dropdown, matching where every other row puts
		/// its control.
		/// </summary>
		private void Build(Transform source)
		{
			var element = m_gameObject.GetComponent<Michsky.UI.Heat.SettingsElement>();
			if (element != null) { element.onClick = new UnityEngine.Events.UnityEvent(); }
			var slotObject = new GameObject("EngineDropdown", typeof(RectTransform));
			slotObject.layer = m_gameObject.layer;
			var slot = (RectTransform)slotObject.transform;
			slot.SetParent(m_gameObject.transform, false);
			if (source != null)
			{
				var sourceRect = (RectTransform)source;
				slot.anchorMin = sourceRect.anchorMin;
				slot.anchorMax = sourceRect.anchorMax;
				slot.pivot = sourceRect.pivot;
				slot.anchoredPosition = sourceRect.anchoredPosition;
				slot.sizeDelta = sourceRect.sizeDelta;
			}
			else
			{
				slot.anchorMin = new Vector2(1f, 0.5f);
				slot.anchorMax = new Vector2(1f, 0.5f);
				slot.pivot = new Vector2(0.5f, 0.5f);
				slot.anchoredPosition = new Vector2(-259f, 0f);
				slot.sizeDelta = new Vector2(480f, 60f);
			}

			m_dropdown = EngineDropdown.Create(slot, m_name != null ? m_name.font : null, GamePhysics.Engines, ResolveEngineDropdownTemplate());
			slot.anchorMin = slot.anchorMax = new Vector2(1, 0.5f);
			slot.pivot = new Vector2(1, 0.5f);
			slot.anchoredPosition = new Vector2(-15, 0);
			slot.sizeDelta = new Vector2(260, 50);
			m_dropdown.Selected += OnEngineSelected;
			m_dropdown.Blocked += OnEngineBlocked;
		}

		private void OnEngineSelected(PhysicsEngine engine)
		{
			SetValue(engine);
		}

		/// <summary>
		/// An engine this build cannot load stays in the list but cannot be picked. The entry itself
		/// carries the short reason; the full one goes to the log.
		/// </summary>
		private void OnEngineBlocked(PhysicsEngineInfo info)
		{
			Debug.LogWarning("Physics engine '" + info.Name + "' is not available in this build: " + info.Reason);
		}

		public override void SetValue(PhysicsEngine value)
		{
			if (EqualityComparer<PhysicsEngine>.Default.Equals(m_property.RawValue, value))
			{
				return;
			}
			base.SetValue(value);
			m_group.Source.Apply();
			Render();
		}

		public override void Render()
		{
			// Availability can change while the screen is open — the Jolt gate is a toggle in this
			// same group — so the list is re-checked before the current selection is shown.
			m_dropdown.RefreshEngines(GamePhysics.Engines);
			m_dropdown.Value = m_property.RawValue;
		}

		public override void SetBoxedValue(object value)
		{
			if (value is PhysicsEngine engine)
			{
				SetValue(engine);
				return;
			}
			if (value is string text && Enum.TryParse(text, out PhysicsEngine parsed))
			{
				SetValue(parsed);
				return;
			}
			throw new InvalidCastException();
		}
	}

	[SerializeField]
	private GameObject m_content;

	[SerializeField]
	private GameObject m_itemTemplate;

	[SerializeField]
	private GameObject m_groupTemplate;

	// Heat UI replacements for the two templates above. Left empty they are loaded from
	// Assets/Resources/HeatUI, where Tools/HeatUI/Stage-Resources.ps1 puts them; when the pack has not
	// been staged the original templates are used instead, so the panel keeps working either way.
	[SerializeField]
	private GameObject m_heatGroupTemplate;

	[SerializeField]
	private GameObject m_heatToggleTemplate;

	[SerializeField]
	private UnityEngine.UI.Button m_saveButton;

	[SerializeField]
	private UnityEngine.UI.Button m_resetButton;

	// Set when the pack is staged: the save button is then Heat UI's, and its caption has to go through
	// the manager rather than a Text component.
	private HeatUiButtonBinding m_saveBinding;

	[SerializeField]
	private string m_onlyGroupName;

	private bool m_dirty;

	private List<SettingsGroup> m_settingsGroups;

	private Dictionary<string, SettingsItem> m_settingsMap;
	private readonly Queue<Action> m_pendingRows = new Queue<Action>();
	private bool m_deferRows;
	private UITextLocale m_saveLocale;
	private string m_saveCaption;
	public bool IsBuilding { get; private set; } = true;
	public bool UsesHeatRows => ResolveRowTemplate()?.GetComponent<Michsky.UI.Heat.SettingsElement>() != null;

	public static INSettingsInterface Instance { get; private set; }

	/// <summary>
	/// <c>m_onlyGroupName</c> of the 优化设置 page. It is not a property of
	/// <see cref="INUserSettings"/> - the page is a gathering of optimization rows that already live in
	/// <see cref="StarSeaSettings"/> - so both the page builder and <see cref="Reset"/> map it back to
	/// that source by name. Setting the prefab's value to anything else silently produces an empty page.
	/// </summary>
	public const string OptimizationGroupName = "OptimizationSettings";

	/// <summary>Maps a page key onto the settings object that actually owns its properties.</summary>
	private static string ResolveSourceName(string onlyGroupName) =>
		onlyGroupName == OptimizationGroupName ? "StarSeaSettings" : onlyGroupName;

	public object GetValue(string name)
	{
		SettingsItem item;
		return m_settingsMap != null && m_settingsMap.TryGetValue(name, out item) && item != null
			? item.GetBoxedValue() : null;
	}

	public void SetValue(string name, object value)
	{
		SettingsItem item;
		if (m_settingsMap != null && m_settingsMap.TryGetValue(name, out item) && item != null)
		{
			item.SetBoxedValue(value);
		}
	}

	public void SetDirty()
	{
		m_dirty = true;
	}

	public void Save()
	{
		INUserSettings.Save();
		// Some rows are two-way proxies onto an INFeature value - 通用设置's 打开版本选择器, which
		// INInitializer reads at boot before any version is chosen, and 部件设置's shared feature rows -
		// and those live in INFeatureSettings.json. The pages' Save is therefore what has to write that
		// file, or the boot read would keep seeing the value from before the player's edit. The file the
		// feature list owns is written on its own Save as well.
		INSettings.SaveToFile();
		m_dirty = false;
	}

	public void Reset()
	{
		if (IsBuilding) return;
		// 先强制把与部件调整界面共享的 INFeature 项重置为版本默认，
		// 避免双向代理属性(get 实时读 INFeature)导致 Update(Default) 拿到"当前值"而重置失效。
		INSettings.ResetUiSharedDefaults();
		if (string.IsNullOrEmpty(m_onlyGroupName))
		{
			INUserSettings.Reset();
		}
		else
		{
			SettingsBase source = GetGroupSource(INUserSettings.Instance, ResolveSourceName(m_onlyGroupName));
			SettingsBase defaultValue = GetGroupSource(INUserSettings.Default, ResolveSourceName(m_onlyGroupName));
			if (source != null && defaultValue != null)
			{
				MethodInfo updateMethod = source.GetType().GetMethod("Update", new[] { source.GetType() });
				if (updateMethod != null)
				{
					updateMethod.Invoke(source, new object[] { defaultValue });
				}
				source.Apply();
			}
		}
		m_dirty = true;
		// 重置后把每个设置项从源重新读入 UI 代理并回显（原版写回通道），
		// 保证重置后再次编辑能正确比较、写入源并生效（否则需退出重进设置界面才行）。
		if (m_settingsGroups == null)
		{
			return;
		}
		foreach (SettingsGroup settingsGroup in m_settingsGroups)
		{
			foreach (SettingsItem item in settingsGroup.Items)
			{
				item.SyncFromSource();
			}
			settingsGroup.Render();
		}
	}

	private void Awake()
	{
		Instance = this;

		// The panel's own background takes Heat UI's palette; a no-op when the pack is not there.
		HeatUiSkin.ApplyPanelSkin(gameObject);
		HeatUiPanelLayout.ConfigureList(m_content);

		// The buttons become Heat UI's when the pack is staged; a swap that cannot produce a working
		// button returns null and the original is kept, so Save and Reset always stay reachable.
		m_saveBinding = HeatUiSkin.ReplaceButton(m_saveButton, HeatUiSkin.ButtonTemplate);
		if (m_saveBinding != null) m_saveButton = m_saveBinding.Button;
		HeatUiButtonBinding reset = HeatUiSkin.ReplaceButton(m_resetButton, HeatUiSkin.ButtonTemplate);
		if (reset != null) m_resetButton = reset.Button;

		m_saveButton.onClick.AddListener(Save);
		m_resetButton.onClick.AddListener(Reset);
		m_saveLocale = m_saveButton.GetComponentInChildren<UITextLocale>(includeInactive: true);
	}

	private void Start()
	{
		// Closing the panel pauses work; a persistent runner lets reopening continue the same build.
		CoroutineRunner.Instance.StartCoroutine(BuildSettings());
	}

	private System.Collections.IEnumerator BuildSettings()
	{
		while (INUserSettings.Instance == null || INLocalization.Instance == null)
		{
			yield return null;
		}
		m_resetButton.interactable = false;
		m_deferRows = true;
		m_settingsGroups = GenerateGroupList(INUserSettings.Instance);
		m_deferRows = false;
		m_settingsMap = new Dictionary<string, SettingsItem>(StringComparer.OrdinalIgnoreCase);
		while (m_pendingRows.Count > 0 && this != null)
		{
			if (!gameObject.activeInHierarchy) { yield return null; continue; }
			while (m_pendingRows.Count > 0 && HeatUiBuildBudget.TryRun(() => m_pendingRows.Dequeue()())) { }
			if (m_pendingRows.Count > 0) yield return null;
		}
		if (this == null) yield break;
		foreach (SettingsGroup settingsGroup in m_settingsGroups)
		{
			foreach (SettingsItem item in settingsGroup.Items)
			{
				if (item.NameLocale != null)
				{
					m_settingsMap.Add(item.NameLocale.ID, item);
				}
			}
		}
		IsBuilding = false;
		m_resetButton.interactable = true;
	}

	private void ScheduleRow(Action build)
	{
		if (m_deferRows) m_pendingRows.Enqueue(build);
		else build();
	}

	private List<SettingsGroup> GenerateGroupList(INUserSettings userSettings)
	{
		List<SettingsGroup> list = new List<SettingsGroup>();
		if (userSettings == null)
		{
			return list;
		}
		if (string.IsNullOrEmpty(m_onlyGroupName))
		{
			foreach (SettingsBase settings in userSettings.SettingsList)
			{
				list.Add(GenerateGroup(settings.GetType().Name, settings.GetType().Name + "_Name", settings));
			}
			return list;
		}
		if (m_onlyGroupName == "StarSeaSettings")
		{
			SettingsBase starSea = GetGroupSource(userSettings, "StarSeaSettings");
			if (starSea == null)
			{
				return list;
			}
			list.Add(GenerateStarSeaGroup(starSea, "StarSeaSettings_Behavior", "Behavior", "AntiTunnelSpeedThreshold", "AntiTunnelPullBackDistance", "PartDrag", "PartAngularDrag", "GunProjectileDrag", "StableMultiblock", "AnchorUmbrellaCoefficient", "LightAntiTunnel", "DDNewConnectionLogic", "BlasterTNTDestroy", "CollisionBounciness", "DiagonalSenderRange"));
			list.Add(GenerateStarSeaGroup(starSea, "StarSeaSettings_ToolBook", "辅助菜单", "ToolBookEnabled", "ToolBookPosX", "ToolBookPosY", "ToolBookSize", "ToolBookPasteOverwriteMode", "ToolBookClipboardMax", "GameRunCollapse", "GameRunQuickSpeed", "NewTimeScale", "AutoEnterVersionB", "AdvancedDisplayGraphical", "QuickPartSwitch", "QuickPartSwitchMax", "QuickPartSwitchPersistent", "QuickInheritParams", "ShowModifiedParams", "EnableParamPanel"));
			// The former Performance group and the two rate rows that used to sit in Behavior
			// (FixedFrameRate, CircuitFrameRate) now live on the 优化设置 page, which gathers every
			// optimization row in one place. They are not repeated here: the same property appearing in
			// two pages would show one value and let it be edited from either, which reads as a bug.
			// EnableExperimentalJolt stays absent for its own reason - see the note in GenerateGroupList.
			list.Add(GenerateStarSeaGroup(starSea, "StarSeaSettings_MechanicalGate", "MechanicalGate", "GateWidthCoefficient", "GateLengthCoefficients", "GateOpacity", "GateDefaultLengths", "GateAdjustableRange", "GateExtensionSpeed"));
			list.Add(GenerateStarSeaGroup(starSea, "StarSeaSettings_PointCharge", "PointCharge", "SpecialPointChargeInvincible", "SpecialChargeDefaultEnabled", "SpecialChargeForce", "SpecialChargeForceRadius", "SpecialChargeJointBreakForce", "SpecialChargeJointDistance", "ChargeJointSnapEnabled", "ChargeJointSize", "SpecialChargeReinforceRange", "SpecialChargeReinforceMaxCount", "SpecialChargeReinforceBreakForce", "SpecialChargeCollisionIgnore", "SpecialChargeAttractionCollisionIgnore", "SpecialChargeForceConnect", "ShowJointConnectionCount"));
			list.Add(GenerateStarSeaGroup(starSea, "StarSeaSettings_GravityGroup", "万有引力", "UniversalGravity", "UniversalGravityConstant", "UniversalGravityAttenuation"));
			return list;
		}
		// 优化设置 is its own top-level category (the 优化设置接口 prefab sets m_onlyGroupName to this),
		// but it deliberately keeps reading StarSeaSettings: those properties are already wired into
		// runtime code - StarSeaSettings.Apply writes Physics.defaultSolverIterations,
		// Time.fixedDeltaTime, ElectricalSystem.EnableMultithreadCircuit and FrameJointManager - so
		// moving them into a new settings class would mean touching every reader for no gain. The page
		// is a view over the same source, gathered in one place.
		if (m_onlyGroupName == OptimizationGroupName)
		{
			SettingsBase optimization = GetGroupSource(userSettings, "StarSeaSettings");
			if (optimization == null)
			{
				return list;
			}
			list.Add(GenerateStarSeaGroup(optimization, "StarSeaSettings_OptimizationBackend", "物理后端", "PhysicsEngine", "PhysicsSolverIterations", "PhysicsWorkerCount", "GpuPhysicsCompute"));
			list.Add(GenerateStarSeaGroup(optimization, "StarSeaSettings_OptimizationTimestep", "时间步与帧率", "TargetFrameRate", "CustomPhysicsFrame", "FixedFrameRate", "AdaptivePhysicsFrame", "AdaptivePhysicsFrameMin"));
			list.Add(GenerateStarSeaGroup(optimization, "StarSeaSettings_OptimizationThreading", "多线程与求解频率", "UseMultithreadFrameSolver", "EnableMultithreadCircuit", "OptimizedCircuit", "FrameFrameRate", "CircuitFrameRate"));
			list.Add(GenerateStarSeaGroup(optimization, "StarSeaSettings_OptimizationDiagnostics", "调试与度量", "ShowFPS", "ShowPhysicsStats"));
			return list;
		}
		SettingsGroup settingsGroup2 = GenerateGroup(m_onlyGroupName, m_onlyGroupName + "_Name", GetGroupSource(userSettings, m_onlyGroupName));
		if (settingsGroup2 != null)
		{
			list.Add(settingsGroup2);
		}
		return list;
	}

	private SettingsGroup GenerateStarSeaGroup(SettingsBase source, string groupLocaleName, string groupName, params string[] propertyNames)
	{
		GameObject gameObject = UnityEngine.Object.Instantiate(ResolveGroupTemplate());
		gameObject.SetActive(value: true);
		gameObject.name = "SettingsGroup_" + groupName;
		gameObject.transform.SetParent(m_content.transform, worldPositionStays: false);
		SettingsGroup settingsGroup = new SettingsGroup(source, gameObject);
		SetLocale(settingsGroup.GroupNameLocale, groupLocaleName);
		Type type = source.GetType();
		foreach (string propName in propertyNames)
		{
			PropertyInfo propertyInfo = type.GetProperty(propName);
			if (propertyInfo == null)
			{
				continue;
			}
			ScheduleRow(() => BuildProperty(settingsGroup, propertyInfo));
		}
		return settingsGroup;
	}

	private readonly Dictionary<Type, MethodInfo> m_inputFactories = new Dictionary<Type, MethodInfo>();

	private void BuildProperty(SettingsGroup group, PropertyInfo property)
	{
		Type propertyType = property.PropertyType;
		string name = property.Name;
		string locale = group.Source.GetType().Name + "_" + name;
		if (propertyType == typeof(bool)) GenerateItemWithToggle(group, name, locale, property);
		else if (propertyType == typeof(PhysicsEngine)) GenerateItemWithEngineList(group, name, locale, property);
		else if (propertyType == typeof(HexColor))
		{
			GenerateItemWithInputField(group, name, locale, property,
				delegate(string s, IFormatProvider provider, out HexColor result) { return HexColor.TryParse(s, out result); });
		}
		else
		{
			if (!m_inputFactories.TryGetValue(propertyType, out MethodInfo factory))
			{
				factory = GetType().GetMethod("GenerateItemWithInputField", BindingFlags.Instance | BindingFlags.NonPublic)
					.MakeGenericMethod(propertyType);
				m_inputFactories[propertyType] = factory;
			}
			factory.Invoke(this, new object[] { group, name, locale, property, null });
		}
	}

	private SettingsItemWithEngineList GenerateItemWithEngineList(SettingsGroup group, string name, string localeName, PropertyInfo property)
	{
		GameObject gameObject = GenerateItem(group, name, ResolveRowTemplate());
		SettingsItemWithEngineList item = new SettingsItemWithEngineList(group, gameObject);
		SetLocale(item.NameLocale, localeName);
		item.Bind(property);
		item.Render();
		group.Items.Add(item);
		return item;
	}

	private static SettingsBase GetGroupSource(INUserSettings userSettings, string groupName)
	{
		if (userSettings == null || string.IsNullOrEmpty(groupName))
		{
			return null;
		}
		System.Reflection.PropertyInfo property = userSettings.GetType().GetProperty(groupName);
		if (property != null && property.GetValue(userSettings) is SettingsBase settingsBase)
		{
			return settingsBase;
		}
		return null;
	}

	private SettingsGroup GenerateGroup(string name, string groupLocaleName, SettingsBase source)
	{
		if (source == null)
		{
			return null;
		}
		GameObject gameObject = UnityEngine.Object.Instantiate(ResolveGroupTemplate());
		gameObject.SetActive(value: true);
		gameObject.name = "SettingsGroup_" + name;
		gameObject.transform.SetParent(m_content.transform, worldPositionStays: false);
		SettingsGroup settingsGroup = new SettingsGroup(source, gameObject);
		SetLocale(settingsGroup.GroupNameLocale, groupLocaleName);
		Type type = source.GetType();
		PropertyInfo[] properties = type.GetProperties();
		foreach (PropertyInfo propertyInfo in properties)
		{
			// Retired settings keep their property for settings-file compatibility but must not become
			// controls. An obsolete toggle either does nothing - EnableExperimentalJolt, whose gate is
			// retired - or duplicates the control that replaced it: UseBox3D is superseded by the
			// PhysicsEngine dropdown. The StarSea-only panel already leaves them out by naming its
			// properties explicitly (see GenerateGroupList); this is the same rule for the reflected
			// panel, and it also stops them asking for locale ids that were never written.
			if (propertyInfo.IsDefined(typeof(System.ObsoleteAttribute), inherit: true))
			{
				continue;
			}
			ScheduleRow(() => BuildProperty(settingsGroup, propertyInfo));
		}
		// 载具存档设置 is the one group whose subject is a file rather than a value, so it ends with the
		// three file actions. They are ordinary rows built here, which keeps them in the group's own
		// section of the list.
		if (source is ContraptionDataSettings)
		{
			ScheduleRow(() => GenerateContraptionFileRows(settingsGroup));
		}
		return settingsGroup;
	}

	/// <summary>
	/// The template a group heading is built from: the assigned Heat UI header, the staged one, or this
	/// project's own template.
	/// </summary>
	/// <remarks>
	/// Every step is an explicit comparison rather than <c>??</c>, because a destroyed or missing Unity
	/// object compares equal to null while not being null, which makes the null-coalescing operator
	/// silently pick the wrong branch.
	/// </remarks>
	private GameObject ResolveGroupTemplate()
	{
		if (m_heatGroupTemplate != null)
		{
			return m_heatGroupTemplate;
		}
		GameObject staged = HeatUiSkin.LoadTemplate(HeatUiSkin.HeaderTemplate);
		return staged != null ? staged : m_groupTemplate;
	}

	/// <summary>
	/// The Heat UI dropdown the engine picker is built from, or null when the pack has not been staged -
	/// in which case <see cref="EngineDropdown"/> draws its own control.
	/// </summary>
	private static GameObject ResolveEngineDropdownTemplate()
	{
		return HeatUiSkin.LoadTemplate(HeatUiSkin.DropdownTemplate);
	}

	/// <summary>
	/// The template every settings row is built from: the assigned Heat UI element, the staged one, or
	/// this project's own template.
	/// </summary>
	/// <remarks>
	/// One Heat UI element serves all three row kinds so they share a look. It arrives with a switch,
	/// which is what a boolean row keeps; the rows that need a text field or a dropdown put their own
	/// widget into the switch's slot and remove it - Heat UI ships no settings element with a text field.
	/// </remarks>
	private GameObject ResolveRowTemplate()
	{
		if (m_heatToggleTemplate != null)
		{
			return m_heatToggleTemplate;
		}
		GameObject staged = HeatUiSkin.LoadTemplate(HeatUiSkin.SwitchElementTemplate);
		return staged != null ? staged : m_itemTemplate;
	}

	private GameObject GenerateItem(SettingsGroup group, string name, GameObject template = null)
	{
		GameObject used = template != null ? template : m_itemTemplate;
		GameObject obj = UnityEngine.Object.Instantiate(used);
		obj.SetActive(value: true);
		obj.name = "SettingsItem_" + name;
		obj.transform.SetParent(m_content.transform, worldPositionStays: false);
		// Headers are planned first; deferred rows still belong directly after their own header.
		obj.transform.SetSiblingIndex(group.GameObject.transform.GetSiblingIndex() + group.Items.Count + 1);
		return obj;
	}

	private SettingsItemWithToggle GenerateItemWithToggle(SettingsGroup group, string name, string localeName, PropertyInfo property)
	{
		GameObject gameObject = GenerateItem(group, name, ResolveRowTemplate());
		SettingsItemWithToggle settingsItemWithToggle = new SettingsItemWithToggle(group, gameObject);
		SetLocale(settingsItemWithToggle.NameLocale, localeName);
		settingsItemWithToggle.Bind(property);
		settingsItemWithToggle.Render();
		group.Items.Add(settingsItemWithToggle);
		return settingsItemWithToggle;
	}

	/// <summary>
	/// The three file actions the 载具存档设置 group ends with.
	/// </summary>
	/// <remarks>
	/// The group's own properties only decide how a save is written; these act on the save file of the
	/// level the player is on - export a copy of it, import one over it, or open the folder it lives in.
	/// </remarks>
	private void GenerateContraptionFileRows(SettingsGroup group)
	{
		GenerateActionRow(group, "ExportContraptionSave", "ContraptionDataSettings_Export", "ContraptionDataSettings_ExportButton", INContraptionSaveFiles.ExportCurrentSave);
		GenerateActionRow(group, "ImportContraptionSave", "ContraptionDataSettings_Import", "ContraptionDataSettings_ImportButton", INContraptionSaveFiles.ImportCurrentSave);
		GenerateActionRow(group, "OpenContraptionSaveFolder", "ContraptionDataSettings_OpenFolder", "ContraptionDataSettings_OpenFolderButton", INContraptionSaveFiles.OpenSaveFolder);
	}

	/// <summary>
	/// Builds a row whose value slot holds a button that runs <paramref name="action"/> instead of writing
	/// a setting.
	/// </summary>
	private SettingsItemWithButton GenerateActionRow(SettingsGroup group, string name, string localeName, string buttonLocaleName, UnityEngine.Events.UnityAction action)
	{
		GameObject gameObject = GenerateItem(group, name, ResolveRowTemplate());
		SettingsItemWithButton settingsItemWithButton = new SettingsItemWithButton(group, gameObject, action);
		SetLocale(settingsItemWithButton.NameLocale, localeName);
		settingsItemWithButton.SetLabelLocale(buttonLocaleName);
		group.Items.Add(settingsItemWithButton);
		return settingsItemWithButton;
	}

	private SettingsItem GenerateItemWithInputField<T>(SettingsGroup group, string name, string localeName, PropertyInfo property, Parsable.TryParser<T> converter = null)
	{
		if (typeof(T) == typeof(PhysicsEngine)) { return GenerateItemWithEngineList(group, name, localeName, property); }
		if (typeof(T).IsEnum || localeName == "ButtonSettings_LayoutMode")
		{
			Array values = typeof(T).IsEnum ? Enum.GetValues(typeof(T)) : new[] { 0, 1 };
			T[] choices = new T[values.Length];
			for (int index = 0; index < values.Length; index++) { choices[index] = (T)values.GetValue(index); }
			GameObject row = GenerateItem(group, name, HeatUiSkin.LoadTemplate("Settings Element (Horizontal Selector)"));
			var selectorItem = new SettingsItemWithSelector<T>(group, row, choices);
			SetLocale(selectorItem.NameLocale, localeName);
			selectorItem.Bind(property);
			selectorItem.Render();
			group.Items.Add(selectorItem);
			return selectorItem;
		}
		if (typeof(T) == typeof(float) && (localeName == "AppInterfaceSettings_EnterButtonAlpha" || localeName == "StarSeaSettings_GateOpacity"))
		{
			GameObject row = GenerateItem(group, name, HeatUiSkin.LoadTemplate("Settings Element (Slider)"));
			var sliderItem = new SettingsItemWithSlider(group, row);
			SetLocale(sliderItem.NameLocale, localeName);
			sliderItem.Bind(property);
			sliderItem.Render();
			group.Items.Add(sliderItem);
			return sliderItem;
		}
		if (converter == null)
		{
			converter = Parsable.GetTryParser<T>();
		}
		if (converter == null)
		{
			return null;
		}
		GameObject gameObject = GenerateItem(group, name, ResolveRowTemplate());
		SettingsItemWithInputField<T> settingsItemWithInputField = new SettingsItemWithInputField<T>(group, gameObject, converter);
		SetLocale(settingsItemWithInputField.NameLocale, localeName);
		settingsItemWithInputField.Bind(property);
		group.Items.Add(settingsItemWithInputField);
		return settingsItemWithInputField;
	}

	/// <summary>
	/// Re-reads the generated rows in the language that is now on.
	/// </summary>
	/// <remarks>
	/// The labels take care of themselves - each one is a <see cref="UITextLocale"/>, which the panel
	/// refreshes - but a row's own strings are not labels: the selector's item titles are built when the
	/// row is created and would otherwise keep the language they were built in.
	/// </remarks>
	public void RefreshLocalization()
	{
		if (m_settingsGroups == null) { return; }
		foreach (SettingsGroup group in m_settingsGroups)
		{
			foreach (SettingsItem item in group.Items)
			{
				item.RefreshLocalization();
			}
		}
	}

	private void Update()
	{
		if (INLocalization.Instance == null || m_saveButton == null)
		{
			return;
		}
		// The locale sits on the button root for a Heat UI button - carried over when it replaced the
		// original - and on the Text child for this project's own button.
		UITextLocale locale = m_saveLocale;
		if (locale == null)
		{
			return;
		}
		string text = m_dirty ? (locale.Text + "*") : locale.Text;
		if (text == m_saveCaption) return;
		m_saveCaption = text;
		if (m_saveBinding != null && m_saveBinding.IsHeatUi)
		{
			m_saveBinding.Label = text;
			return;
		}
		HeatUiSkin.SetLabelText(m_saveButton.gameObject, "Text", text);
	}

	/// <summary>
	/// Applies a locale id to a label when the template has one.
	/// </summary>
	/// <remarks>
	/// Rows built from Heat UI prefabs get their locale component attached at runtime, and a row with
	/// no label at all is left unlocalized rather than throwing.
	/// </remarks>
	private static void SetLocale(UITextLocale locale, string id)
	{
		if (locale == null || string.IsNullOrEmpty(id))
		{
			return;
		}
		locale.ID = id;
		locale.UpdateText();
	}
}
