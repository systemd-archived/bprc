using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class SchematicButton : SliderButton
{
	public float hideSpeed;

	public List<GameObject> toggleList = new List<GameObject>();

	public GameObject button;

	private bool isButtonOut;

	private bool lastIsPlaying;

	private bool coroutineRunning;

	private const string AnimName = "SchematicsButtonSlide";

	private const string ToolBoxAnimName = "ToolBoxButton";

	private List<Vector3> origPositions;

	private List<Vector3> targetPositions;

	private List<Transform> selectedSlotSprites = new List<Transform>();

	private static LevelManager levelManager;

	private readonly int SLOT_COUNT = 3;

	private bool m_openList;

	private static LevelManager LevelManager
	{
		get
		{
			if (levelManager != null)
			{
				return levelManager;
			}
			return levelManager = Object.FindObjectOfType<LevelManager>();
		}
	}

	public static string LastLoadedSlotKey
	{
		get
		{
			if (LevelManager.CurrentGameMode is CakeRaceMode)
			{
				return $"cr_{Singleton<GameManager>.Instance.CurrentSceneName}_LastLoadedContraptionSlotIndex";
			}
			return $"{Singleton<GameManager>.Instance.CurrentSceneName}_LastLoadedContraptionSlotIndex";
		}
	}

	public bool ToolboxOpen => isButtonOut;

	private void Awake()
	{
		ContraptionDataSettings contraptionDataSettings = INUserSettings.Instance.ContraptionDataSettings;
		if (contraptionDataSettings.Enabled)
		{
			float spacing = contraptionDataSettings.SlotSpacing;
			float scale = contraptionDataSettings.SlotScale;
			int perRow = contraptionDataSettings.SlotPerRow;
			float rowHeight = contraptionDataSettings.SlotRowHeight;
			if (perRow < 1)
			{
				perRow = 1;
			}
			GameObject original = base.transform.GetChild(0).gameObject;
			AnimationClip clip = GetComponent<Animation>().clip;
			for (int i = SLOT_COUNT + 1; i <= contraptionDataSettings.SlotCount; i++)
			{
				GameObject obj = Object.Instantiate(original, base.transform);
				Button component = obj.GetComponent<Button>();
				TextMesh component2 = obj.transform.Find("Number").GetComponent<TextMesh>();
				string relativePath = (obj.name = "Slot" + i.ToString("00"));
				component.EventToSend.GetParameters()[0].stringValue = UIEvent.GetSlotEvent(i - 1).ToString();
				FieldInfo origScaleField = typeof(Button).GetField("originalScale", BindingFlags.Instance | BindingFlags.NonPublic);
				if (origScaleField != null)
				{
					origScaleField.SetValue(component, Vector3.one * scale);
				}
				component2.text = i.ToString();
				int col = (i - 1) % perRow;
				int row = (i - 1) / perRow;
				float x = 10.5f + (col - 3) * spacing;
				float y = -row * rowHeight;
				clip.SetCurve(relativePath, typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 0.25f, x));
				clip.SetCurve(relativePath, typeof(Transform), "localPosition.y", AnimationCurve.Linear(0f, 0f, 0.25f, y));
				clip.SetCurve(relativePath, typeof(Transform), "localEulerAngles.z", AnimationCurve.Linear(0f, -180f, 0.25f, 0f));
				clip.SetCurve(relativePath, typeof(Transform), "localScale.x", AnimationCurve.Linear(0f, 0.1f, 0.25f, scale));
				clip.SetCurve(relativePath, typeof(Transform), "localScale.y", AnimationCurve.Linear(0f, 0.1f, 0.25f, scale));
				clip.SetCurve(relativePath, typeof(Transform), "localScale.z", AnimationCurve.Linear(0f, 0.1f, 0.25f, scale));
			}
		}
	}

	protected override void Start()
	{
		base.Start();
		if (LevelManager != null && !LevelManager.m_sandbox)
		{
			return;
		}
		origPositions = new List<Vector3>();
		targetPositions = new List<Vector3>();
		foreach (GameObject toggle in toggleList)
		{
			origPositions.Add(toggle.transform.position);
			targetPositions.Add(toggle.transform.position + new Vector3(0f, 5f));
		}
		for (int i = 0; i < SLOT_COUNT; i++)
		{
			Transform transform = base.transform.FindChildRecursively($"Slot{i + 1:00}");
			if (!(transform == null))
			{
				Transform transform2 = transform.Find("SlotSelected");
				if (!(transform2 == null))
				{
					selectedSlotSprites.Add(transform2);
					transform2.GetComponent<Renderer>().enabled = false;
				}
			}
		}
		ContraptionDataSettings contraptionDataSettings = INUserSettings.Instance.ContraptionDataSettings;
		if (contraptionDataSettings.Enabled)
		{
			for (int j = SLOT_COUNT + 1; j <= contraptionDataSettings.SlotCount; j++)
			{
				string n = "Slot" + j.ToString("00");
				Transform item = base.transform.Find(n).Find("SlotSelected");
				selectedSlotSprites.Add(item);
			}
		}
		if (!GameProgress.HasKey(LastLoadedSlotKey))
		{
			GameProgress.SetInt(LastLoadedSlotKey, 0);
		}
	}

	private void OnEnable()
	{
		button.transform.Find("Gear").transform.rotation = Quaternion.identity;
		isButtonOut = (lastIsPlaying = (coroutineRunning = false));
		EnableRendererRecursively(base.gameObject, enable: false);
		ActivateToggleList(state: true);
		if (toggleList != null && origPositions != null)
		{
			for (int i = 0; i < toggleList.Count; i++)
			{
				toggleList[i].transform.position = origPositions[i];
			}
		}
		EventManager.Connect<UIEvent>(OnReceivedEvent);
	}

	private void OnDisable()
	{
		for (int i = 0; i < base.transform.childCount; i++)
		{
			Transform child = base.transform.GetChild(i);
			Vector3 localPosition = child.localPosition;
			localPosition.x = (localPosition.y = 0f);
			child.localPosition = localPosition;
			child.localRotation = Quaternion.identity;
			child.localScale = Vector3.one * 0.2f;
		}
		EventManager.Disconnect<UIEvent>(OnReceivedEvent);
	}

	private void OnReceivedEvent(UIEvent data)
	{
		if (LevelManager != null && !LevelManager.m_sandbox)
		{
			return;
		}
		int num = UIEvent.GetSlotIndex(data.type);
		int slotCount = INUserSettings.Instance.ContraptionDataSettings.SlotCount;
		if (num >= 0 && num < slotCount)
		{
			GameProgress.SetInt(LastLoadedSlotKey, num);
			for (int i = 0; i < selectedSlotSprites.Count; i++)
			{
				selectedSlotSprites[i].GetComponent<Renderer>().enabled = num == i;
			}
		}
	}

	public void OnPressed()
	{
		if (GetComponent<Animation>().isPlaying || coroutineRunning)
		{
			return;
		}
		EnableRendererRecursively(base.gameObject, enable: true);
		int num = GameProgress.GetInt(LastLoadedSlotKey);
		for (int i = 0; i < selectedSlotSprites.Count; i++)
		{
			selectedSlotSprites[i].GetComponent<Renderer>().enabled = num == i;
		}
		bool flag = isButtonOut;
		InitAnimationStates(flag, GetComponent<Animation>()["SchematicsButtonSlide"], button.GetComponent<Animation>()["ToolBoxButton"]);
		button.GetComponent<Animation>().Play();
		GetComponent<Animation>().Play();
		isButtonOut = !isButtonOut;
		_ = isButtonOut;
		for (int j = 0; j < toggleList.Count; j++)
		{
			if (flag)
			{
				StartCoroutine(MoveObject(toggleList[j].transform, origPositions[j]));
			}
			else
			{
				StartCoroutine(MoveObject(toggleList[j].transform, targetPositions[j]));
			}
		}
	}

	private void Update()
	{
		if (!GetComponent<Animation>().isPlaying && lastIsPlaying && !isButtonOut)
		{
			ActivateToggleList(state: true);
		}
		if (m_openList)
		{
			OnPressed();
			m_openList = false;
		}
		lastIsPlaying = GetComponent<Animation>().isPlaying;
	}

	private void ActivateToggleList(bool state)
	{
		foreach (GameObject toggle in toggleList)
		{
			toggle.SetActive(state);
		}
	}

	private void InitAnimationStates(bool reverse, params AnimationState[] states)
	{
		foreach (AnimationState animationState in states)
		{
			animationState.speed = ((!reverse) ? 1 : (-1));
			animationState.time = ((!reverse) ? 0f : animationState.length);
		}
	}

	private void EnableRendererRecursively(GameObject obj, bool enable)
	{
		if ((bool)obj.GetComponent<Renderer>())
		{
			obj.GetComponent<Renderer>().enabled = enable;
		}
		if ((bool)obj.GetComponent<Collider>())
		{
			obj.GetComponent<Collider>().enabled = enable;
		}
		for (int i = 0; i < obj.transform.childCount; i++)
		{
			EnableRendererRecursively(obj.transform.GetChild(i).gameObject, enable);
		}
	}

	public void OpenMenu()
	{
		ActivateToggleList(state: true);
	}

	private IEnumerator MoveObject(Transform tf, Vector3 targetPos)
	{
		coroutineRunning = true;
		Vector3 startPos = tf.position;
		float step = 0f;
		while (step < 1f)
		{
			tf.position = Vector3.Lerp(startPos, targetPos, step);
			step += hideSpeed * Time.deltaTime;
			yield return null;
		}
		tf.position = targetPos;
		coroutineRunning = false;
	}
}
