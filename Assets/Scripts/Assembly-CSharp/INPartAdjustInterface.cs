using System;
using System.Collections.Generic;
using Innovation;
using UnityEngine;
using UnityEngine.UI;

public class INPartAdjustInterface : MonoBehaviour
{
	private class DeclType
	{
		public string MainType { get; set; }

		public string[] GenericArguments { get; set; }
	}

	private class DeclItem
	{
		public string Name { get; set; }

		public DeclType Type { get; set; }

		public object Value { get; set; }
	}

	private class DeclRoot
	{
		public DeclItem[] Items { get; set; }
	}

	private class Group
	{
		public int Start;

		public int End;

		public string Title;

		public Group(int start, int end, string title)
		{
			Start = start;
			End = end;
			Title = title;
		}
	}

	private abstract class PartItem
	{
		public INFeature Feature;

		public string Name;

		public GameObject GameObject;

		public abstract void Refresh();
	}

	private class BoolPartItem : PartItem
	{
		public ToggleSwitch Toggle;

		public override void Refresh()
		{
			try
			{
				Toggle.IsOn = INSettings.GetBool(Feature);
			}
			catch
			{
			}
		}
	}

	private class NumberPartItem : PartItem
	{
		public InputField InputField;

		public bool IsInt;

		public override void Refresh()
		{
			try
			{
				InputField.text = (IsInt ? INSettings.GetInt(Feature).ToString() : INSettings.GetFloat(Feature).ToString());
			}
			catch
			{
			}
		}
	}

	private class StringPartItem : PartItem
	{
		public InputField InputField;

		public override void Refresh()
		{
			try
			{
				InputField.text = INSettings.GetString(Feature);
			}
			catch
			{
			}
		}
	}

	private class ArrayPartItem : PartItem
	{
		public InputField InputField;

		public override void Refresh()
		{
			try
			{
				int[] array = INSettings.GetValue<int[]>(Feature);
				InputField.text = (array != null ? string.Join(",", array) : string.Empty);
			}
			catch
			{
			}
		}
	}

	[SerializeField]
	private GameObject m_content;

	[SerializeField]
	private GameObject m_itemTemplate;

	[SerializeField]
	private GameObject m_groupTemplate;

	[SerializeField]
	private UnityEngine.UI.Button m_saveButton;

	[SerializeField]
	private UnityEngine.UI.Button m_resetButton;

	private Dictionary<string, string> m_aliases;

	private List<PartItem> m_items;

	private Dictionary<string, object> m_defaults;
	private readonly Queue<Action> m_pendingRows = new Queue<Action>();
	private Dictionary<string, INFeature> m_declaredFeatures;
	public bool IsBuilding { get; private set; } = true;

	public static INPartAdjustInterface Instance { get; private set; }

	private void Awake()
	{
		Instance = this;
		m_saveButton.onClick.AddListener(Save);
		m_resetButton.onClick.AddListener(Reset);
	}

	private void Start()
	{
		CoroutineRunner.Instance.StartCoroutine(BuildRows());
	}

	private System.Collections.IEnumerator BuildRows()
	{
		m_resetButton.interactable = false;
		m_aliases = Json.Deserialize<Dictionary<string, string>>(INUnity.LoadTextAsset("INAliasSettings").text);
		DeclRoot decl = Json.Deserialize<DeclRoot>(INUnity.LoadTextAsset("INDeclarationSettingsExp").text);
		m_items = new List<PartItem>();
		m_defaults = new Dictionary<string, object>();
		if (decl?.Items == null)
		{
			IsBuilding = false;
			m_resetButton.interactable = true;
			yield break;
		}
		m_declaredFeatures = new Dictionary<string, INFeature>();
		foreach (DeclItem item in decl.Items)
		{
			if (Enum.TryParse(item.Name, out INFeature feature)) m_declaredFeatures[item.Name] = feature;
		}
		Group[] groups = new Group[]
		{
			new Group(0, 5, "基础系统"),
			new Group(6, 11, "解锁"),
			new Group(12, 17, "属性界面"),
			new Group(18, 21, "建造格"),
			new Group(22, 26, "视界"),
			new Group(27, 33, "部件操作"),
			new Group(34, 39, "系统开关"),
			new Group(40, 43, "保存/绳"),
			new Group(44, 47, "视界/隐藏"),
			new Group(48, 54, "载具物理"),
			new Group(55, 60, "水面/阻力"),
			new Group(61, 74, "框体"),
			new Group(75, 87, "箱/框/蛋"),
			new Group(88, 90, "轮子"),
			new Group(91, 98, "风扇/桨"),
			new Group(99, 110, "火箭"),
			new Group(111, 120, "追踪/伞/弹簧"),
			new Group(121, 131, "TNT/绳"),
			new Group(132, 134, "铰链板"),
			new Group(135, 140, "气球/沙袋"),
			new Group(141, 146, "翼/尾/拳"),
			new Group(147, 154, "部件生成器"),
			new Group(155, 167, "机炮"),
			new Group(168, 169, "南瓜"),
			new Group(170, 177, "分离/连接器"),
			new Group(178, 178, "变速箱"),
			new Group(179, 180, "光源按钮"),
			new Group(181, 201, "光源"),
			new Group(202, 205, "燃料/引擎"),
			new Group(206, 207, "电力/机械")
		};
		foreach (Group group in groups)
		{
			m_pendingRows.Enqueue(() => GenerateGroup(group, decl.Items));
		}
		while (m_pendingRows.Count > 0 && this != null)
		{
			if (!gameObject.activeInHierarchy) { yield return null; continue; }
			while (m_pendingRows.Count > 0 && HeatUiBuildBudget.TryRun(() =>
			{
				try { m_pendingRows.Dequeue()(); }
				catch (Exception ex) { Debug.LogWarning("Settings row build: " + ex.Message); }
			})) { }
			if (m_pendingRows.Count > 0) yield return null;
		}
		if (this == null) yield break;
		IsBuilding = false;
		m_resetButton.interactable = true;
	}

	private void GenerateGroup(Group group, DeclItem[] items)
	{
		GameObject obj = UnityEngine.Object.Instantiate(m_groupTemplate);
		obj.SetActive(value: true);
		obj.name = "SettingsGroup_" + group.Title;
		obj.transform.SetParent(m_content.transform, worldPositionStays: false);
		obj.transform.Find("GroupName").GetComponent<Text>().text = group.Title;
		foreach (DeclItem item in items)
		{
			INFeature feature;
			if (!m_declaredFeatures.TryGetValue(item.Name, out feature)) continue;
			if ((int)feature < group.Start || (int)feature > group.End)
			{
				continue;
			}
			try
			{
				Variant defaultValue = INSettings.GetDefaultValue(feature);
				m_defaults[item.Name] = (defaultValue != null ? defaultValue.BoxedValue : item.Value);
				m_pendingRows.Enqueue(() => GenerateItem(obj.transform, item, feature));
			}
			catch
			{
			}
		}
	}

	private void GenerateItem(Transform groupTransform, DeclItem item, INFeature feature)
	{
		GameObject obj = UnityEngine.Object.Instantiate(m_itemTemplate);
		obj.SetActive(value: true);
		obj.name = "SettingsItem_" + item.Name;
		obj.transform.SetParent(groupTransform, worldPositionStays: false);
		string label = item.Name;
		if (HeatUiOptionHelp.Chinese && m_aliases != null && m_aliases.TryGetValue(item.Name, out var chinese))
		{
			label = chinese;
		}
		obj.transform.Find("Name").GetComponent<Text>().text = label;
		string mainType = item.Type?.MainType;
		if (mainType == "Boolean")
		{
			ToggleSwitch toggle = obj.transform.Find("ToggleSwitch").GetComponent<ToggleSwitch>();
			toggle.gameObject.SetActive(value: true);
			BoolPartItem boolItem = new BoolPartItem();
			boolItem.Feature = feature;
			boolItem.Name = item.Name;
			boolItem.GameObject = obj;
			boolItem.Toggle = toggle;
			toggle.OnValueChanged.AddListener(delegate(bool v)
			{
				INSettings.SetSettingsValue(item.Name, v);
			});
			boolItem.Refresh();
			m_items.Add(boolItem);
		}
		else if (mainType == "Int32" || mainType == "Single")
		{
			InputField inputField = obj.transform.Find("InputField").GetComponent<InputField>();
			inputField.gameObject.SetActive(value: true);
			NumberPartItem numberItem = new NumberPartItem();
			numberItem.Feature = feature;
			numberItem.Name = item.Name;
			numberItem.GameObject = obj;
			numberItem.InputField = inputField;
			numberItem.IsInt = mainType == "Int32";
			inputField.onValueChanged.AddListener(delegate(string text)
			{
				WriteNumberSetting(item.Name, numberItem.IsInt, text);
			});
			inputField.onEndEdit.AddListener(delegate(string text)
			{
				WriteNumberSetting(item.Name, numberItem.IsInt, text);
			});
			numberItem.Refresh();
			m_items.Add(numberItem);
		}
		else if (mainType == "String")
		{
			InputField inputField = obj.transform.Find("InputField").GetComponent<InputField>();
			inputField.gameObject.SetActive(value: true);
			StringPartItem stringItem = new StringPartItem();
			stringItem.Feature = feature;
			stringItem.Name = item.Name;
			stringItem.GameObject = obj;
			stringItem.InputField = inputField;
			inputField.onEndEdit.AddListener(delegate(string text)
			{
				try
				{
					INSettings.SetSettingsValue(item.Name, text);
				}
				catch
				{
				}
			});
			stringItem.Refresh();
			m_items.Add(stringItem);
		}
		else if (mainType == "Array")
		{
			InputField inputField = obj.transform.Find("InputField").GetComponent<InputField>();
			inputField.gameObject.SetActive(value: true);
			ArrayPartItem arrayItem = new ArrayPartItem();
			arrayItem.Feature = feature;
			arrayItem.Name = item.Name;
			arrayItem.GameObject = obj;
			arrayItem.InputField = inputField;
			inputField.onEndEdit.AddListener(delegate(string text)
			{
				try
				{
					string[] parts = text.Split(',');
					int[] array = new int[parts.Length];
					for (int i = 0; i < parts.Length; i++)
					{
						if (int.TryParse(parts[i].Trim(), out var v))
						{
							array[i] = v;
						}
					}
					INSettings.SetSettingsValue(item.Name, array);
				}
				catch
				{
				}
			});
			arrayItem.Refresh();
			m_items.Add(arrayItem);
		}
	}

	private void WriteNumberSetting(string name, bool isInt, string text)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				return;
			}
			if (isInt && int.TryParse(text, out var intValue))
			{
				INSettings.SetSettingsValue(name, intValue);
			}
			else if (!isInt && float.TryParse(text, out var floatValue))
			{
				INSettings.SetSettingsValue(name, floatValue);
			}
		}
		catch
		{
		}
	}

	public void Save()
	{
		INSettings.SaveToFile();
	}

	public void Reset()
	{
		if (IsBuilding) return;
		if (m_defaults != null)
		{
			foreach (KeyValuePair<string, object> pair in m_defaults)
			{
				try
				{
					INSettings.SetSettingsValue(pair.Key, pair.Value);
				}
				catch
				{
				}
			}
		}
		if (m_items != null)
		{
			foreach (PartItem item in m_items)
			{
				item.Refresh();
			}
		}
	}
}
