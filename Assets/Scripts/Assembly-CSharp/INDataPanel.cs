using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class INDataPanel : MonoBehaviour
{
	private class DataGroup
	{
		public GameObject GameObject { get; private set; }

		public Text GroupName { get; private set; }

		public UITextLocale GroupNameLocale { get; private set; }

		public List<DataItem> Items { get; private set; }

		public DataGroup(GameObject gameObject)
		{
			GameObject = gameObject;
			// The heading may come from Heat UI's settings header, which labels with TextMeshPro under
			// "Text" and carries no locale component until EnsureLocale adds one.
			GroupNameLocale = HeatUiSkin.EnsureLocale(gameObject, HeatUiSkin.LegacyGroupNameChild);
			GroupName = GroupNameLocale != null ? GroupNameLocale.GetComponent<Text>() : null;
			Items = new List<DataItem>();
		}
	}

	private class DataItem
	{
		public GameObject GameObject { get; private set; }

		public Text Name { get; private set; }

		public UITextLocale NameLocale { get; private set; }

		/// <summary>The object the value goes into, whichever kind of label it holds.</summary>
		public GameObject ValueObject { get; private set; }

		public Func<string> Getter { get; private set; }

		public DataItem(GameObject gameObject, Func<string> getter)
		{
			GameObject = gameObject;
			NameLocale = HeatUiSkin.EnsureLocale(gameObject, HeatUiSkin.LegacyNameChild);
			Name = NameLocale != null ? NameLocale.GetComponent<Text>() : null;

			// Heat UI has no read-only row, so one is made out of its settings element: the switch it
			// arrives with is replaced by a second copy of the row's own label, named Value, which is
			// what this panel writes into. The project's own template already has a Value child.
			Transform value = gameObject.transform.Find("Value");
			if (value == null)
			{
				Michsky.UI.Heat.SwitchManager heatSwitch = HeatUiSkin.FindSwitch(gameObject);
				Transform label = HeatUiSkin.FindLabel(gameObject, HeatUiSkin.HeatLabelChild);
				if (heatSwitch != null && label != null)
				{
					HeatUiSkin.CopyLabel(gameObject, heatSwitch.transform, label, "Value");
					value = gameObject.transform.Find("Value");
				}
			}

			ValueObject = value != null ? value.gameObject : null;
			Getter = getter;
		}

		public void UpdateValue()
		{
			if (Getter != null && ValueObject != null)
			{
				// SetLabelText reaches the label whether it is TextMeshPro, as Heat UI's is, or Text.
				HeatUiSkin.SetLabelText(ValueObject, "Value", Getter());
			}
		}
	}

	[SerializeField]
	private GameObject m_content;

	[SerializeField]
	private GameObject m_dataItemTemplate;

	[SerializeField]
	private GameObject m_dataGroupTemplate;

	// Heat UI replacement for the group heading. Left empty it is loaded from Assets/Resources/HeatUI;
	// when the pack has not been staged the original template is used instead.
	[SerializeField]
	private GameObject m_heatGroupTemplate;

	// Heat UI replacement for the row. Left empty it is loaded from Assets/Resources/HeatUI; when the pack
	// has not been staged the original template is used instead.
	[SerializeField]
	private GameObject m_heatItemTemplate;

	private List<DataGroup> m_dataGroups;

	private INDataDetector m_detector;

	public static INDataPanel Instance { get; private set; }

	private void Awake()
	{
		Instance = this;

		// The panel's own background takes Heat UI's palette; a no-op when the pack is not there.
		HeatUiSkin.ApplyPanelSkin(gameObject);
		HeatUiPanelLayout.ConfigureList(m_content);
		m_dataGroups = new List<DataGroup>();
		m_detector = UnityEngine.Object.Instantiate(INUnity.LoadGameObject("INDataDetector")).GetComponent<INDataDetector>();
	}

	private System.Collections.IEnumerator Start()
	{
		while (INLocalization.Instance == null) { yield return null; }
		DataGroup dataGroup = GenerateDataGroup(0, "DataPanel_TimeData");
		GenerateDataItem(dataGroup, 0, "DataPanel_Time", () => DateTime.Now.ToString("s"));
		GenerateDataItem(dataGroup, 1, "DataPanel_RunningTime", () => Time.realtimeSinceStartup.ToString("F2"));
		DataGroup dataGroup2 = GenerateDataGroup(1, "DataPanel_PerformanceData");
		GenerateDataItem(dataGroup2, 0, "DataPanel_FPS", () => m_detector.FPS.ToString("F2"));
		GenerateDataItem(dataGroup2, 1, "DataPanel_FixedFPS", () => m_detector.FixedFPS.ToString("F2"));
		GenerateDataItem(dataGroup2, 2, "DataPanel_AllocatedManagedHeapSize", () => m_detector.AllocatedManagedHeapSize.ToString("F2") + " MB");
		GenerateDataItem(dataGroup2, 3, "DataPanel_ReservedManagedHeapSize", () => m_detector.ReservedManagedHeapSize.ToString("F2") + " MB");
		GenerateDataItem(dataGroup2, 4, "DataPanel_TotalAllocatedMemorySize", () => m_detector.TotalAllocatedMemorySize.ToString("F2") + " MB");
		GenerateDataItem(dataGroup2, 5, "DataPanel_TotalReservedMemorySize", () => m_detector.TotalReservedMemorySize.ToString("F2") + " MB");
		DataGroup dataGroup3 = GenerateDataGroup(2, "DataPanel_DeviceData");
		GenerateDataItem(dataGroup3, 0, "DataPanel_DeviceModel", () => SystemInfo.deviceModel);
		GenerateDataItem(dataGroup3, 1, "DataPanel_DeviceName", () => SystemInfo.deviceName);
		GenerateDataItem(dataGroup3, 2, "DataPanel_DeviceType", () => SystemInfo.deviceType.ToString());
		GenerateDataItem(dataGroup3, 3, "DataPanel_OperatingSystem", () => SystemInfo.operatingSystem);
		GenerateDataItem(dataGroup3, 4, "DataPanel_ProcessorType", () => SystemInfo.processorType);
		GenerateDataItem(dataGroup3, 5, "DataPanel_ProcessorFrequency", () => SystemInfo.processorFrequency + " MHz");
		GenerateDataItem(dataGroup3, 6, "DataPanel_ProcessorCount", () => SystemInfo.processorCount.ToString());
		GenerateDataItem(dataGroup3, 7, "DataPanel_GraphicsDeviceName", () => SystemInfo.graphicsDeviceName);
		GenerateDataItem(dataGroup3, 8, "DataPanel_GraphicsDeviceType", () => SystemInfo.graphicsDeviceType.ToString());
		GenerateDataItem(dataGroup3, 9, "DataPanel_GraphicsMemorySize", () => SystemInfo.graphicsMemorySize + " MB");
		GenerateDataItem(dataGroup3, 10, "DataPanel_SystemMemorySize", () => SystemInfo.systemMemorySize + " MB");
		GenerateDataItem(dataGroup3, 11, "DataPanel_BatteryLevel", () => SystemInfo.batteryLevel.ToString("P"));
		GenerateDataItem(dataGroup3, 12, "DataPanel_BatteryStatus", () => SystemInfo.batteryStatus.ToString());
	}

	/// <summary>
	/// The template a data group's heading is built from: the assigned Heat UI header, the staged one, or
	/// this project's own template.
	/// </summary>
	private GameObject ResolveGroupTemplate()
	{
		if (m_heatGroupTemplate != null)
		{
			return m_heatGroupTemplate;
		}
		GameObject staged = HeatUiSkin.LoadTemplate(HeatUiSkin.HeaderTemplate);
		return staged != null ? staged : m_dataGroupTemplate;
	}

	/// <summary>
	/// The template a data row is built from: the assigned Heat UI element, the staged one, or this
	/// project's own template.
	/// </summary>
	private GameObject ResolveItemTemplate()
	{
		if (m_heatItemTemplate != null)
		{
			return m_heatItemTemplate;
		}
		GameObject staged = HeatUiSkin.LoadTemplate(HeatUiSkin.SwitchElementTemplate);
		return staged != null ? staged : m_dataItemTemplate;
	}

	private DataGroup GenerateDataGroup(int index, string groupName)
	{
		GameObject obj = UnityEngine.Object.Instantiate(ResolveGroupTemplate());
		obj.SetActive(value: true);
		obj.name = "DataGroup_" + index;
		obj.transform.SetParent(m_content.transform, worldPositionStays: false);
		DataGroup dataGroup = new DataGroup(obj);
		if (dataGroup.GroupNameLocale != null)
		{
			dataGroup.GroupNameLocale.ID = groupName;
			dataGroup.GroupNameLocale.UpdateText();
		}
		m_dataGroups.Add(dataGroup);
		return dataGroup;
	}

	private DataItem GenerateDataItem(DataGroup dataGroup, int index, string name, Func<string> getter)
	{
		GameObject obj = UnityEngine.Object.Instantiate(ResolveItemTemplate());
		obj.SetActive(value: true);
		obj.name = "DataItem_" + index;
		obj.transform.SetParent(m_content.transform, worldPositionStays: false);
		DataItem dataItem = new DataItem(obj, getter);
		if (dataItem.NameLocale != null)
		{
			dataItem.NameLocale.ID = name;
			dataItem.NameLocale.UpdateText();
		}
		if (dataItem.ValueObject != null)
		{
			HeatUiSkin.SetLabelText(dataItem.ValueObject, "Value", getter());
		}
		dataGroup.Items.Add(dataItem);
		return dataItem;
	}

	private void Update()
	{
		foreach (DataGroup dataGroup in m_dataGroups)
		{
			foreach (DataItem item in dataGroup.Items)
			{
				item.UpdateValue();
			}
		}
	}
}
