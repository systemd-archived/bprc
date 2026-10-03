using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Innovation;
using ShellFileDialogs;
using UnityEngine;
using UnityEngine.UI;

public class INAddonInterface : MonoBehaviour
{
	public class AddonPackageItem
	{
		private class SettingsGroup
		{
			private AddonPackageSettings m_source;

			private GameObject m_gameObject;

			private GameObject m_itemTemplate;

			private List<SettingsItem> m_items;

			public AddonPackageSettings Source => m_source;

			public GameObject GameObject => m_gameObject;

			public List<SettingsItem> Items => m_items;

			public SettingsGroup(GameObject gameObject, GameObject itemTemplate)
			{
				m_gameObject = gameObject;
				m_itemTemplate = itemTemplate;
				m_items = new List<SettingsItem>();
			}

			public void ApplySettings(AddonPackageSettings source)
			{
				if (m_source == source)
				{
					return;
				}
				m_source = source;
				Clear();
				if (source == null)
				{
					return;
				}
				PropertyInfo[] properties = source.GetType().GetProperties();
				foreach (PropertyInfo propertyInfo in properties)
				{
					Type propertyType = propertyInfo.PropertyType;
					SettingsItemAttribute customAttribute = propertyInfo.GetCustomAttribute<SettingsItemAttribute>();
					string text = ((customAttribute == null) ? propertyInfo.Name : customAttribute.Name);
					if (propertyType == typeof(bool))
					{
						GenerateItemWithToggle(this, text, propertyInfo);
						continue;
					}
					if (propertyType == typeof(HexColor))
					{
						GenerateItemWithInputField(this, text, propertyInfo, delegate(string s, IFormatProvider provider, out HexColor result)
						{
							return HexColor.TryParse(s, out result);
						});
						continue;
					}
					GetType().GetMethod("GenerateItemWithInputField", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).MakeGenericMethod(propertyType).Invoke(this, new object[4] { this, text, propertyInfo, null });
				}
			}

			private GameObject GenerateItem(SettingsGroup group, string name)
			{
				GameObject gameObject = UnityEngine.Object.Instantiate(m_itemTemplate);
				gameObject.SetActive(value: true);
				gameObject.name = "SettingsItem_" + name;
				gameObject.transform.SetParent(group.GameObject.transform, worldPositionStays: false);
				return gameObject;
			}

			private SettingsItemWithToggle GenerateItemWithToggle(SettingsGroup group, string name, PropertyInfo property)
			{
				GameObject gameObject = GenerateItem(group, name);
				SettingsItemWithToggle settingsItemWithToggle = new SettingsItemWithToggle(group, gameObject);
				settingsItemWithToggle.Name.text = name;
				settingsItemWithToggle.Bind(property);
				group.Items.Add(settingsItemWithToggle);
				return settingsItemWithToggle;
			}

			private SettingsItemWithInputField<T> GenerateItemWithInputField<T>(SettingsGroup group, string name, PropertyInfo property, Parsable.TryParser<T> converter = null)
			{
				if (converter == null)
				{
					converter = Parsable.GetTryParser<T>();
				}
				if (converter == null)
				{
					return null;
				}
				GameObject gameObject = GenerateItem(group, name);
				SettingsItemWithInputField<T> settingsItemWithInputField = new SettingsItemWithInputField<T>(group, gameObject, converter);
				settingsItemWithInputField.Name.text = name;
				settingsItemWithInputField.Bind(property);
				group.Items.Add(settingsItemWithInputField);
				return settingsItemWithInputField;
			}

			private void Clear()
			{
				foreach (SettingsItem item in m_items)
				{
					UnityEngine.Object.Destroy(item.GameObject);
				}
				m_items.Clear();
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

			public SettingsItem(SettingsGroup group, GameObject gameObject)
			{
				m_group = group;
				m_gameObject = gameObject;
				m_name = gameObject.transform.Find("Name").GetComponent<Text>();
				m_nameLocale = m_name.GetComponent<UITextLocale>();
			}

			public abstract void Render();
		}

		private abstract class SettingsItem<T> : SettingsItem
		{
			protected DependencyProperty<T> m_property;

			public SettingsItem(SettingsGroup group, GameObject gameObject)
				: base(group, gameObject)
			{
				m_property = new DependencyProperty<T>();
			}

			public void Bind(string propertyName, Func<T> getter, Action<T> setter)
			{
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

			public virtual T GetValue()
			{
				return m_property.Value;
			}

			public virtual void SetValue(T value)
			{
				m_property.Value = value;
			}
		}

		private class SettingsItemWithToggle : SettingsItem<bool>
		{
			private ToggleSwitch m_toggle;

			public ToggleSwitch Toggle => m_toggle;

			public SettingsItemWithToggle(SettingsGroup group, GameObject gameObject)
				: base(group, gameObject)
			{
				m_toggle = gameObject.transform.Find("ToggleSwitch").GetComponent<ToggleSwitch>();
				m_toggle.gameObject.SetActive(value: true);
				m_toggle.OnValueChanged.AddListener(OnValueChanged);
			}

			public override void SetValue(bool value)
			{
				if (m_property.RawValue != value)
				{
					base.SetValue(value);
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
				m_toggle.IsOn = m_property.RawValue;
			}
		}

		private class SettingsItemWithInputField<T> : SettingsItem<T>
		{
			private InputField m_inputField;

			private Parsable.TryParser<T> m_parser;

			public InputField InputField => m_inputField;

			public SettingsItemWithInputField(SettingsGroup group, GameObject gameObject, Parsable.TryParser<T> parser)
				: base(group, gameObject)
			{
				m_inputField = gameObject.transform.Find("InputField").GetComponent<InputField>();
				m_inputField.gameObject.SetActive(value: true);
				m_inputField.onEndEdit.AddListener(OnEndEdit);
				m_parser = parser;
				Render();
			}

			public override void SetValue(T value)
			{
				if (!EqualityComparer<T>.Default.Equals(m_property.RawValue, value))
				{
					base.SetValue(value);
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
				InputField inputField = m_inputField;
				T rawValue = m_property.RawValue;
				inputField.text = ((rawValue != null) ? rawValue.ToString() : null);
			}
		}

		private INAddonInterface m_parent;

		private GameObject m_gameObject;

		private Text m_primaryText;

		private Text m_secondaryText;

		private Text m_statusText;

		private ToggleSwitch m_enabledToggle;

		private ToggleSwitch m_autoStartToggle;

		private UnityEngine.UI.Button m_reloadButton;

		private UnityEngine.UI.Button m_unloadButton;

		private SettingsGroup m_settingsGroup;

		private AddonPackageInfo m_info;

		public AddonPackageItem(INAddonInterface parent, GameObject gameObject)
		{
			m_parent = parent;
			m_gameObject = gameObject;
			Transform transform = gameObject.transform.Find("Main");
			m_primaryText = transform.Find("TextGroup").Find("Primary").GetComponent<Text>();
			m_secondaryText = transform.Find("TextGroup").Find("Secondary").GetComponent<Text>();
			m_statusText = transform.Find("Status").GetComponent<Text>();
			m_enabledToggle = transform.Find("EnabledToggle").Find("ToggleSwitch").GetComponent<ToggleSwitch>();
			m_autoStartToggle = transform.Find("AutoStartToggle").Find("ToggleSwitch").GetComponent<ToggleSwitch>();
			m_enabledToggle.OnValueChanged.AddListener(SetEnabled);
			m_autoStartToggle.OnValueChanged.AddListener(SetAutoStart);
			m_reloadButton = transform.Find("ReloadButton").GetComponent<UnityEngine.UI.Button>();
			m_unloadButton = transform.Find("UnloadButton").GetComponent<UnityEngine.UI.Button>();
			m_reloadButton.onClick.AddListener(Reload);
			m_unloadButton.onClick.AddListener(Unload);
			m_settingsGroup = new SettingsGroup(gameObject.transform.Find("SettingsGroup").gameObject, parent.m_settingsItemTemplate);
		}

		public void ApplyPackage(AddonPackageInfo info, Exception exception)
		{
			m_info = info;
			if (info.State == AddonPackageState.Loading)
			{
				m_primaryText.text = info.ID;
				m_secondaryText.text = string.Empty;
				m_statusText.text = INLocalization.Instance.GetText("AddonSystem_StateLoading");
				m_enabledToggle.interactable = false;
				return;
			}
			if (info.State == AddonPackageState.Failed)
			{
				m_primaryText.text = info.ID;
				m_secondaryText.text = string.Empty;
				string text = INLocalization.Instance.GetText("AddonSystem_StateFailed");
				m_statusText.text = "<color=#BF6060>" + text + " - " + exception.GetType().ToString() + ": " + exception.Message + "</color>";
				m_enabledToggle.interactable = false;
				return;
			}
			AddonPackage package = info.Package;
			m_primaryText.text = package.Name;
			m_secondaryText.text = "  |  " + package.ID + " " + package.Version?.ToString() + "  |  " + package.Developer;
			m_enabledToggle.interactable = true;
			m_enabledToggle.SetIsOnWithoutNotify(info.State == AddonPackageState.Enabled);
			m_autoStartToggle.SetIsOnWithoutNotify(info.AutoStart);
			if (info.State == AddonPackageState.Enabled)
			{
				string text2 = INLocalization.Instance.GetText("AddonSystem_StateEnabled");
				m_statusText.text = "MD5 - " + info.MD5 + "  |  <color=#60BF60>" + text2 + "</color>";
			}
			else
			{
				string text3 = INLocalization.Instance.GetText("AddonSystem_StateDisabled");
				m_statusText.text = "MD5 - " + info.MD5 + "  |  <color=#808080>" + text3 + "</color>";
			}
			m_settingsGroup.ApplySettings(package.Settings);
			LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)m_settingsGroup.GameObject.transform);
			LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)m_gameObject.transform);
		}

		public void SetEnabled(bool enabled)
		{
			if (m_info.State != AddonPackageState.Failed)
			{
				INAddonManager.Instance.PackageManager.SetPackageEnabled(m_info.ID, enabled);
				ApplyPackage(m_info, null);
			}
		}

		public void SetAutoStart(bool autoStart)
		{
			if (m_info.State != AddonPackageState.Failed)
			{
				INAddonManager.Instance.PackageManager.SetPackageAutoStart(m_info.ID, autoStart);
				ApplyPackage(m_info, null);
			}
		}

		public void Reload()
		{
			UnityEngine.Object.Destroy(m_gameObject);
			m_parent.m_packageItemMap.Remove(m_info.ID);
			INAddonManager.Instance.PackageManager.ReloadPackage(m_info.ID);
		}

		public void Unload()
		{
			INAddonManager.Instance.PackageManager.UnloadPackage(m_info.ID);
			UnityEngine.Object.Destroy(m_gameObject);
			m_parent.m_packageItemMap.Remove(m_info.ID);
		}
	}

	public class FileReceiver : AndroidJavaProxy
	{
		public event Action<bool, string> Received;

		public FileReceiver()
			: base("com.innovation.filedialog.FileReceiver")
		{
		}

		public void onActivityResult(bool success, string result)
		{
			try
			{
				this.Received?.Invoke(success, result);
			}
			catch
			{
			}
		}
	}

	[SerializeField]
	private UnityEngine.UI.Button m_importButton;

	[SerializeField]
	private GameObject m_content;

	[SerializeField]
	private GameObject m_packageItemTemplate;

	[SerializeField]
	private GameObject m_settingsItemTemplate;

	private Dictionary<string, AddonPackageItem> m_packageItemMap;

	private ConcurrentQueue<string> m_pathQueue;

	public static INAddonInterface Instance { get; private set; }

	private void Awake()
	{
		Instance = this;
		m_importButton.onClick.AddListener(ImportPackage);
		m_packageItemMap = new Dictionary<string, AddonPackageItem>();
		m_pathQueue = new ConcurrentQueue<string>();
	}

	private void Start()
	{
	}

	private void ImportPackage()
	{
		OpenFileDialog(delegate(bool success, string path)
		{
			if (success)
			{
				m_pathQueue.Enqueue(path);
			}
		});
	}

	private void OpenFileDialog(Action<bool, string> handler)
	{
		// 跨平台文件选择：Android 用系统文件选择器，桌面用 ShellFileDialogs
		INFilePicker.PickFile("All files", "*", delegate(string path)
		{
			handler(arg1: !string.IsNullOrEmpty(path), path ?? string.Empty);
		});
	}

	public void ApplyPackage(AddonPackageInfo info, Exception exception)
	{
		if (m_packageItemMap.TryGetValue(info.ID, out var value))
		{
			value.ApplyPackage(info, exception);
			return;
		}
		GameObject gameObject = UnityEngine.Object.Instantiate(m_packageItemTemplate);
		gameObject.SetActive(value: true);
		gameObject.name = "PackageItem_" + info.ID;
		gameObject.transform.SetParent(m_content.transform, worldPositionStays: false);
		AddonPackageItem addonPackageItem = new AddonPackageItem(this, gameObject);
		addonPackageItem.ApplyPackage(info, exception);
		m_packageItemMap[info.ID] = addonPackageItem;
	}

	private void Update()
	{
		string result;
		while (m_pathQueue.TryDequeue(out result))
		{
			try
			{
				INAddonManager.Instance.PackageManager.ImportExternalPackage(result).Forget();
			}
			catch
			{
			}
		}
	}
}
