using System;
using System.Collections.Generic;
using UnityEngine;

public class ConstructionUI : WPFMonoBehaviour
{
	public class PartDesc
	{
		public BasePart part;

		public Texture tex;

		public int coordX;

		public int coordY;

		public int useCount;

		public int maxCount;

		public int sortKey;

		public int customPartIndex;

		public Transform currentPartIcon;

		public bool IsUnlimited => part is RCR2Port.RoverPortPart;

		public bool HasAvailableParts => IsUnlimited || useCount < maxCount;

		public int CurrentCount
		{
			get
			{
				if (IsUnlimited || INSettings.GetBool(INFeature.PartCounter))
				{
					return useCount;
				}
				return maxCount - useCount;
			}
		}
	}

	public static bool MatchesPartIdentity(BasePart part, BasePart.PartType partType, int customPartIndex)
	{
		if (part != null && part.Type == BasePart.PartType.RCR2Cargo && partType == BasePart.PartType.RCR2Cargo) return true;
		if (part == null || part.m_partType != partType)
		{
			return false;
		}
		return (!(part is RCR2Port.RoverPortPart) && customPartIndex < RCR2Port.RoverPortCatalog.FirstPartIndex) || part.customPartIndex == customPartIndex;
	}

	public static bool SamePartIdentity(BasePart part, BasePart other)
	{
		if (part == null || other == null || part.m_partType != other.m_partType)
		{
			return false;
		}
		if (part is RCR2Port.RoverPortPart || other is RCR2Port.RoverPortPart)
		{
			return part.customPartIndex == other.customPartIndex;
		}
		return true;
	}

	public Action OnPartsUnlocked;

	public List<Transform> m_purchasableParts = new List<Transform>();

	public Transform m_gridPrefab;

	public Transform m_cellPrefab;

	public Material m_cellMaterial;

	public int m_itemsPerRow = 14;

	public float m_offsetX = 0.2f;

	public float m_offsetY = 0.2f;

	public float m_spacingX = 0.06f;

	public float m_scale = 0.1f;

	public Texture2D m_cellTextureValid;

	public Texture2D m_cellTextureInvalid;

	public Texture2D m_textureSelected;

	protected Transform m_grid;
	private bool m_gridRequested = true;

	protected Dictionary<int, Transform> m_cellMap = new Dictionary<int, Transform>();

	protected List<Transform> m_partInstances = new List<Transform>();

	protected List<PartDesc> m_partDescs = new List<PartDesc>();

	protected List<PartDesc> m_purchasablePartDescs = new List<PartDesc>();

	protected List<PartDesc> m_unlockedParts = new List<PartDesc>();

	protected Contraption m_contraption;

	protected int m_draggedElement = -1;

	protected int m_draggedElementCustomizationIndex;

	protected bool m_draggingFromContraption;

	private bool m_dragStartedFromContraption;

	protected int m_contraptionDragX;

	protected int m_contraptionDragY;

	private int m_dragStartedX;

	private int m_dragStartedY;

	protected int m_flipCount;

	protected int m_selectedElement = -1;

	protected BasePart.GridRotation draggedPartRotation;

	protected bool draggedPartFlipped;

	protected GUIStyle m_textStyle;

	private GameObject clearButton;

	private GameObject playButton;

	private List<Transform> m_parts = new List<Transform>();

	private PartSelector partSelector;

	private GameObject m_dragIcon;

	private bool m_useDragOffset = true;

	private Vector3 m_dragOffset;

	private Vector3 m_dragIconOffset = Vector3.zero;

	private Transform m_mouseOverCell;

	private bool m_dragStarted;

	private Vector3 m_dragStartPosition;

	private int m_moveCounter;

	private int m_rotationCounter;

	private float m_lastMoveTime;

	private bool m_tutorialPulseDone;

	private bool m_previewSwipeStarted;

	private Vector3 m_previewSwipeStartPosition;

	private bool m_allowDragPlacement;

	private bool m_disableFunctionality;

	private BasePart.PartType m_currentCustomizablePartType;

	private Vector3 m_pointerTime;

	// 长按部件调整参数：按住不动 PartLongPressDuration 秒后弹出参数面板。
	private const float PartLongPressDuration = 0.5f;

	private float m_partPressTime;

	private BasePart m_partPressTarget;

	private Vector3 m_partPressStartPosition;

	// 拖动部件时暂存其自定义参数，放置后恢复到新实例（拖动会移除并重建部件）。
	private string m_dragStoredCustomParams;
	private INMachineBuildEditor m_machineEditor;
	private RaycastHit[] m_buildPickHits = new RaycastHit[32];

	private BasePart PickBuildPart(Vector3 screen) => PickBuildPartInRadius(screen, 0f);

	private BasePart PickDeletionPart(Vector3 screen)
	{
		BasePart direct = PickBuildPart(screen);
		if (direct != null) return direct;
		bool windows = Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor;
		return windows ? PickBuildPartInRadius(screen, 0.25f) : null;
	}

	private BasePart PickBuildPartInRadius(Vector3 screen, float radius)
	{
		return PickBuildPart(m_contraption, screen, ref m_buildPickHits, radius);
	}

	internal static BasePart PickBuildPart(Contraption contraption, Vector3 screen, ref RaycastHit[] hits, float radius = 0f)
	{
		if (contraption == null || Camera.main == null) return null;
		Ray ray = Camera.main.ScreenPointToRay(screen);
		Physics.SyncTransforms();
		int count;
		while (true)
		{
			count = radius > 0f
				? Physics.SphereCastNonAlloc(ray, radius, hits, Camera.main.farClipPlane, ~0, QueryTriggerInteraction.Collide)
				: Physics.RaycastNonAlloc(ray, hits, Camera.main.farClipPlane, ~0, QueryTriggerInteraction.Collide);
			if (count < hits.Length) break;
			System.Array.Resize(ref hits, hits.Length * 2);
		}
		BasePart nearest = null;
		float distance = float.PositiveInfinity;
		for (int i = 0; i < count; i++)
		{
			BasePart part = hits[i].collider.GetComponentInParent<BasePart>();
			if (part != null && part.contraption == contraption && ContraptionLayers.Editable(part) && !part.m_static &&
				contraption.Parts.Contains(part) && hits[i].distance < distance)
			{
				nearest = part; distance = hits[i].distance;
			}
		}
		return nearest;
	}

	private void ResolvePointerCell(Vector3 screen, ref int x, ref int y)
	{
		BasePart part = PickBuildPart(screen);
		if (part != null) { x = part.m_coordX; y = part.m_coordY; }
		else ChangeCoordinatesToSelectBigPart(ref x, ref y);
	}

	public void NotifyMachineTransformChanged() { AddMove(); }
	public void ClearMachinePlacementSelection()
	{
		CancelDrag();
		partSelector?.ResetSelection();
		m_selectedElement = -1;
		m_dragStarted = false;
		m_partPressTarget = null;
	}

	public List<PartDesc> PartDescriptors => m_partDescs;

	public bool DisableFunctionality
	{
		get
		{
			return m_disableFunctionality;
		}
		set
		{
			m_disableFunctionality = value;
		}
	}

	public List<PartDesc> UnlockedParts => m_unlockedParts;

	public int RotationCount => m_rotationCounter;

	public int MoveCount => m_moveCounter;

	public int SelectedElement => m_selectedElement;

	public Vector3 PointerTime => m_pointerTime;

	private void AddMove()
	{
		m_lastMoveTime = Time.time;
		m_moveCounter++;
	}

	private void OnDestroy()
	{
		EventManager.Disconnect<UIEvent>(ReceiveUIEvent);
		EventManager.Disconnect<CustomizePartUI.PartCustomizationEvent>(OnPartCustomization);
	}

	public PartDesc FindPartDesc(BasePart.PartType partType)
	{
		foreach (PartDesc partDesc in m_partDescs)
		{
			if (partDesc.part.m_partType == partType)
			{
				return partDesc;
			}
		}
		return null;
	}

	public PartDesc FindPartDesc(BasePart.PartType partType, int customPartIndex)
	{
		if (customPartIndex >= RCR2Port.RoverPortCatalog.FirstPartIndex)
		{
			GameObject ported = RCR2Port.RoverPortCatalog.Instance.GetPart(customPartIndex);
			// A ported index identifies its part on its own, so an old save whose part was stored under
			// a part type the port has since changed (the cockpit used to be a Basket, older builds
			// stored them as MechanicalPart) still finds its button.
			if (ported != null) partType = ported.GetComponent<BasePart>().Type;
			if (partType == BasePart.PartType.RCR2Cargo) return FindPartDesc(partType);
		}
		foreach (PartDesc partDesc in m_partDescs)
		{
			if (partDesc.part.m_partType == partType && partDesc.part.customPartIndex == customPartIndex)
			{
				return partDesc;
			}
		}
		// The build list holds one button per part type and it shows the player's last used variant of
		// that type (PartSelector.CreatePartList -> CustomizationManager.GetLastUsedPartIndex), so a
		// blueprint saved with another variant of the same type - a native frame or motor wheel next to
		// the ported parts that share its type - used to lose those parts on load:
		// GameMode.BuildContraption skips a part whose descriptor it cannot find. The variant itself
		// comes from GameData.GetCustomPart(partType, customPartIndex), so the type's own button is
		// enough to load it.
		foreach (PartDesc partDesc in m_partDescs)
		{
			if (partDesc.part.m_partType == partType)
			{
				return partDesc;
			}
		}
		return null;
	}

	public int PartSelectorRowCount()
	{
		return partSelector.UsedRows;
	}

	public void SetPartSelectorMaxRowCount(int rows)
	{
		partSelector.SetMaxRows(rows);
	}

	public GameObject FindPartButton(BasePart.PartType partType)
	{
		return partSelector.FindPartButton(FindPartDesc(partType));
	}

	public void AddUnlockedPart(BasePart.PartType partType, int count)
	{
		foreach (PartDesc partDesc in m_partDescs)
		{
			if (partDesc.part.m_partType == partType)
			{
				partDesc.maxCount += count;
				EventManager.Send(new PartCountChanged(partType, partDesc.CurrentCount, partDesc.part.customPartIndex));
			}
		}
	}

	private void OnEnable()
	{
		SetButtonPositions();
		m_lastMoveTime = Time.time;
		EventManager.Connect<GameTimePaused>(ReceiveGameTimePaused);
		GridlessBuild.ModeChanged += ApplyGridlessGridVisibility;
		ApplyGridlessGridVisibility();
		if (!GridlessBuild.Enabled && INSettings.GetBool(INFeature.RenderInfiniteGrid))
		{
			m_grid.gameObject.SetActive(value: true);
		}
	}

	private void OnDisable()
	{
		EventManager.Disconnect<GameTimePaused>(ReceiveGameTimePaused);
		GridlessBuild.ModeChanged -= ApplyGridlessGridVisibility;
		if (INSettings.GetBool(INFeature.RenderInfiniteGrid))
		{
			m_grid.gameObject.SetActive(value: false);
		}
	}

	/// <summary>
	/// 无网格模式 hides the lattice completely - every cell, including the hovered one.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Showing a grid of cells while placing parts off it states the opposite of what the mode does, and
	/// keeping the hovered cell visible still reads as a grid: one square that jumps between lattice
	/// positions is the lattice. So nothing of it is drawn.
	/// </para>
	/// <para>
	/// The markers are hidden one by one rather than by switching <c>m_grid</c> off, because <c>m_grid</c>
	/// has existing owners - <see cref="SetEnabled"/> sets it from its <c>enableGrid</c> argument and also
	/// derives <c>base.gameObject.SetActive(enableUI || enableGrid)</c> from that same argument, and
	/// <c>OnEnable</c>/<c>OnDisable</c> set it for <c>RenderInfiniteGrid</c>. Overriding it here would
	/// change when the build screen itself is active, which is a different question.
	/// </para>
	/// </remarks>
	private void ApplyGridlessGridVisibility()
	{
		if (m_grid != null) m_grid.gameObject.SetActive(!GridlessBuild.Enabled && m_gridRequested);
		if (m_cellMap == null) return;
		foreach (KeyValuePair<int, Transform> cell in m_cellMap)
		{
			SetCellVisible(cell.Value);
		}
	}

	/// <summary>Shows or hides one lattice marker according to the current mode.</summary>
	private static void SetCellVisible(Transform cell)
	{
		if (!cell) return;
		bool visible = !GridlessBuild.Enabled;
		if (cell.gameObject.activeSelf != visible) cell.gameObject.SetActive(visible);
	}

	private void OnApplicationPause(bool paused)
	{
		if (paused && m_draggedElement != -1)
		{
			partSelector.ResetSelection();
			if (m_draggedElement != -1)
			{
				SetDraggedElement(-1);
			}
		}
	}

	private void ReceiveGameTimePaused(GameTimePaused data)
	{
		if (data.paused && m_draggedElement != -1)
		{
			partSelector.ResetSelection();
			if (m_draggedElement != -1)
			{
				SetDraggedElement(-1);
			}
		}
	}

	private void Awake()
	{
		m_machineEditor = gameObject.AddComponent<INMachineBuildEditor>();
		EventManager.Connect<UIEvent>(ReceiveUIEvent);
		EventManager.Connect<CustomizePartUI.PartCustomizationEvent>(OnPartCustomization);
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		m_useDragOffset = DeviceInfo.UsesTouchInput && !Singleton<BuildCustomizationLoader>.Instance.IsHDVersion;
		m_allowDragPlacement = !DeviceInfo.UsesTouchInput;
		foreach (GameObject part in WPFMonoBehaviour.gameData.Parts)
		{
			Transform transform = part.transform;
			m_parts.Add(transform);
			BasePart component = part.GetComponent<BasePart>();
			bool isPortedPart = component is RCR2Port.RoverPortPart;
			int num4 = isPortedPart ? int.MaxValue : WPFMonoBehaviour.levelManager.GetPartTypeCount(component.m_partType);
			if (!isPortedPart && INSettings.GetBool(INFeature.PartCounter) && component.m_constructionIconSprite != null)
			{
				num4 = 1061109567;
			}
			if (num4 == 0 && !m_purchasableParts.Contains(transform))
			{
				continue;
			}
			Transform obj = UnityEngine.Object.Instantiate(transform);
			component = obj.GetComponent<BasePart>();
			obj.gameObject.SetActive(value: false);
			obj.parent = base.transform;
			MeshRenderer component2 = obj.GetComponent<MeshRenderer>();
			if ((bool)component.m_constructionIconSprite)
			{
				component2 = component.m_constructionIconSprite.GetComponent<MeshRenderer>();
			}
			if (!component2 || !component2.sharedMaterial)
			{
				continue;
			}
			Texture mainTexture = component2.sharedMaterial.mainTexture;
			if (!mainTexture)
			{
				continue;
			}
			PartDesc partDesc = new PartDesc();
			partDesc.part = component;
			partDesc.tex = mainTexture;
			partDesc.coordX = num;
			partDesc.coordY = num2;
			partDesc.useCount = 0;
			partDesc.maxCount = num4;
			partDesc.customPartIndex = component.customPartIndex;
			if (WPFMonoBehaviour.levelManager.m_sandbox && !(WPFMonoBehaviour.levelManager.CurrentGameMode is CakeRaceMode))
			{
				int unlockedSandboxPartCount = GameProgress.GetUnlockedSandboxPartCount(component.m_partType);
				if (unlockedSandboxPartCount > 0)
				{
					GameProgress.SetUnlockedSandboxPartCount(component.m_partType, 0);
					PartDesc partDesc2 = new PartDesc();
					partDesc2.part = partDesc.part;
					partDesc2.maxCount = unlockedSandboxPartCount;
					partDesc2.customPartIndex = component.customPartIndex;
					m_unlockedParts.Add(partDesc2);
					partDesc.maxCount -= unlockedSandboxPartCount;
				}
				unlockedSandboxPartCount = GameProgress.GetUnlockedSandboxPartCount(Singleton<GameManager>.Instance.CurrentSceneName, component.m_partType);
				if (unlockedSandboxPartCount > 0)
				{
					GameProgress.SetUnlockedSandboxPartCount(Singleton<GameManager>.Instance.CurrentSceneName, component.m_partType, 0);
					PartDesc partDesc3 = new PartDesc();
					partDesc3.part = partDesc.part;
					partDesc3.maxCount = unlockedSandboxPartCount;
					partDesc3.customPartIndex = component.customPartIndex;
					m_unlockedParts.Add(partDesc3);
					partDesc.maxCount -= unlockedSandboxPartCount;
				}
			}
			m_partDescs.Add(partDesc);
			WPFMonoBehaviour.levelManager.m_totalAvailableParts += num4;
			num++;
			if (num >= m_itemsPerRow)
			{
				num = 0;
				num2++;
			}
			num3++;
		}
		if ((bool)WPFMonoBehaviour.levelManager)
		{
			m_contraption = WPFMonoBehaviour.levelManager.ContraptionProto;
		}
		if (!m_contraption)
		{
			GameObject obj2 = new GameObject("Contraption");
			obj2.transform.parent = base.transform;
			obj2.transform.localPosition = Vector3.zero;
			m_contraption = obj2.AddComponent<Contraption>();
		}
		m_cellPrefab = ((!WPFMonoBehaviour.levelManager.GridCellPrefab) ? m_cellPrefab : WPFMonoBehaviour.levelManager.GridCellPrefab.transform);
		if ((bool)m_cellPrefab)
		{
			GameObject gameObject = new GameObject();
			gameObject.transform.parent = base.transform;
			gameObject.transform.localPosition = Vector3.zero;
			for (int i = 0; i < WPFMonoBehaviour.levelManager.GridHeight; i++)
			{
				for (int j = WPFMonoBehaviour.levelManager.GridXMin; j <= WPFMonoBehaviour.levelManager.GridXMax; j++)
				{
					if (WPFMonoBehaviour.levelManager.CanPlacePartAtGridCell(j, i))
					{
						Transform transform2 = UnityEngine.Object.Instantiate(m_cellPrefab);
						transform2.transform.parent = gameObject.transform;
						transform2.localPosition = new Vector3(j, i, 1f);
						int key = i * 1000 + j;
						m_cellMap[key] = transform2;
					}
				}
			}
			m_grid = gameObject.transform;
			if (INSettings.GetBool(INFeature.RenderInfiniteGrid))
			{
				m_cellMaterial = new Material(m_cellPrefab.GetComponent<MeshRenderer>().sharedMaterial);
				m_cellMaterial.color = new Color(1f, 1f, 1f, 0.5f);
			}
			// The markers exist now, so 无网格模式 can be applied to them: the grid is built once, and a
			// mode that is already on has to take effect on this first pass rather than on the next change.
			ApplyGridlessGridVisibility();
		}
		GameObject gameObject2 = GameObject.Find("InGameGUI");
		if ((bool)gameObject2)
		{
			clearButton = gameObject2.transform.Find("InGameBuildMenu").Find("ClearButton").gameObject;
			playButton = gameObject2.transform.Find("InGameBuildMenu").Find("PlayButton").gameObject;
			partSelector = gameObject2.transform.Find("InGameBuildMenu").Find("PartSelector").GetComponent<PartSelector>();
			partSelector.SetParts(m_partDescs);
		}
	}

	public void SetCurrentContraption()
	{
		m_contraption = WPFMonoBehaviour.levelManager.ContraptionProto;
	}

	private void Update()
	{
		if (m_disableFunctionality)
		{
			return;
		}
		if (INSelectionManager.Instance != null && INSelectionManager.Instance.CurrentState != INSelectionManager.State.Idle)
		{
			return;
		}
		if (m_machineEditor != null && m_machineEditor.HandleInput(m_contraption)) return;
		if (m_draggedElement != -1)
		{
			m_lastMoveTime = Time.time;
			if ((bool)m_dragIcon)
			{
				Vector3 position = WPFMonoBehaviour.hudCamera.GetComponent<Camera>().ScreenToWorldPoint(GuiManager.GetPointer().position);
				position.z = WPFMonoBehaviour.hudCamera.transform.position.z + 2f;
				position += m_dragOffset;
				position += m_dragIconOffset;
				m_dragIcon.transform.position = position;
			}
		}
		SetButtonPositions();
		if (Input.touchCount > 1)
		{
			CancelDrag();
		}
		HandleDragging();
	}

	private void LateUpdate()
	{
		if (GridlessBuild.Enabled || !INSettings.GetBool(INFeature.RenderInfiniteGrid))
		{
			return;
		}
		Vector3 vector = WPFMonoBehaviour.ingameCamera.transform.position - base.transform.position;
		float orthographicSize = WPFMonoBehaviour.ingameCamera.GetComponent<Camera>().orthographicSize;
		orthographicSize = ((orthographicSize > 20f) ? 20f : orthographicSize);
		float num = orthographicSize * (float)Screen.width / (float)Screen.height;
		bool flag = true;
		bool flag2 = true;
		int i = 0;
		int childCount = m_grid.childCount;
		if (WPFMonoBehaviour.levelManager.gameState == LevelManager.GameState.Building)
		{
			for (int j = (int)(vector.x - num) - 1; j <= (int)(vector.x + num) + 1 && flag2; j++)
			{
				for (int k = (int)(vector.y - orthographicSize) - 1; k <= (int)(vector.y + orthographicSize) + 1 && flag2; k++)
				{
					Transform transform;
					if (i < childCount)
					{
						transform = m_grid.GetChild(i);
						MeshRenderer component = transform.GetComponent<MeshRenderer>();
						if (!component.enabled)
						{
							flag = false;
						}
						if (!flag)
						{
							component.enabled = true;
						}
					}
					else
					{
						transform = UnityEngine.Object.Instantiate(m_cellPrefab);
						transform.GetComponent<MeshRenderer>().material = m_cellMaterial;
						transform.parent = m_grid;
					}
					i++;
					transform.localPosition = new Vector3(j, k, 1f);
					flag2 = i < childCount + 200;
				}
			}
		}
		for (; i < childCount && flag; i++)
		{
			MeshRenderer component2 = m_grid.GetChild(i).GetComponent<MeshRenderer>();
			if (component2.enabled)
			{
				component2.enabled = false;
				continue;
			}
			break;
		}
	}

	private void SetButtonPositions()
	{
		if (INSettings.GetBool(INFeature.SetBuildingButtonPosition))
		{
			float num = (float)Screen.width / (float)Screen.height * 10f;
			float num2 = -1.987f;
			float num3 = 3.815f;
			clearButton.transform.localPosition = new Vector3(num + num2, 8f - num3 * 3f, 0f);
			playButton.transform.localPosition = new Vector3(num + num2, 8f - num3 * 2f, 0f);
			if (INSettings.GetBool(INFeature.SaveButton))
			{
				clearButton.transform.parent.Find("SaveButton").transform.localPosition = new Vector3(num + num2, 8f - num3, 0f);
			}
		}
		else
		{
			SetHudPositionFromRelativeLevelPosition(clearButton, new Vector3((float)WPFMonoBehaviour.levelManager.GridXMin - 1f, 0.5f * (float)(WPFMonoBehaviour.levelManager.GridHeight - 1), 0f), new Vector3(-1.2f, 0f, 0f));
			SetHudPositionFromRelativeLevelPosition(playButton, new Vector3((float)WPFMonoBehaviour.levelManager.GridXMax + 1f, 0.5f * (float)(WPFMonoBehaviour.levelManager.GridHeight - 1), 0f), new Vector3(1.2f, 0f, 0f));
		}
	}

	public void CheckUnlockedParts()
	{
		List<PartDesc> list = new List<PartDesc>();
		List<PartDesc> list2 = new List<PartDesc>();
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		int num4 = 0;
		foreach (Transform part in m_parts)
		{
			Transform transform = UnityEngine.Object.Instantiate(part);
			BasePart component = transform.GetComponent<BasePart>();
			bool isPortedPart = component is RCR2Port.RoverPortPart;
			transform.gameObject.SetActive(value: false);
			int num5 = isPortedPart ? int.MaxValue : WPFMonoBehaviour.levelManager.GetPartTypeCount(component.m_partType);
			if (!isPortedPart && INSettings.GetBool(INFeature.PartCounter) && component.m_constructionIconSprite != null)
			{
				num5 = 1061109567;
			}
			if (num5 == 0 && !m_purchasableParts.Contains(part))
			{
				UnityEngine.Object.Destroy(transform.gameObject);
				continue;
			}
			transform.parent = base.transform;
			MeshRenderer component2 = transform.GetComponent<MeshRenderer>();
			if ((bool)transform.GetComponent<BasePart>().m_constructionIconSprite)
			{
				component2 = transform.GetComponent<BasePart>().m_constructionIconSprite.GetComponent<MeshRenderer>();
			}
			if (!component2 || !component2.sharedMaterial)
			{
				continue;
			}
			Texture mainTexture = component2.sharedMaterial.mainTexture;
			if (!mainTexture)
			{
				continue;
			}
			PartDesc partDesc = new PartDesc();
			partDesc.part = component;
			partDesc.tex = mainTexture;
			partDesc.coordX = num;
			partDesc.coordY = num2;
			partDesc.useCount = WPFMonoBehaviour.levelManager.ContraptionProto.GetPartCount(component.m_partType);
			partDesc.maxCount = num5;
			partDesc.customPartIndex = component.customPartIndex;
			if (WPFMonoBehaviour.levelManager.m_sandbox)
			{
				int unlockedSandboxPartCount = GameProgress.GetUnlockedSandboxPartCount(component.m_partType);
				if (unlockedSandboxPartCount > 0)
				{
					GameProgress.SetUnlockedSandboxPartCount(component.m_partType, 0);
					list.Add(new PartDesc
					{
						part = partDesc.part,
						maxCount = unlockedSandboxPartCount,
						customPartIndex = component.customPartIndex
					});
					partDesc.maxCount -= unlockedSandboxPartCount;
				}
				if (unlockedSandboxPartCount > 0)
				{
					num4 += unlockedSandboxPartCount;
					WPFMonoBehaviour.levelManager.m_totalAvailableParts += unlockedSandboxPartCount;
				}
				unlockedSandboxPartCount = GameProgress.GetUnlockedSandboxPartCount(Singleton<GameManager>.Instance.CurrentSceneName, component.m_partType);
				if (unlockedSandboxPartCount > 0)
				{
					GameProgress.SetUnlockedSandboxPartCount(Singleton<GameManager>.Instance.CurrentSceneName, component.m_partType, 0);
					list.Add(new PartDesc
					{
						part = partDesc.part,
						maxCount = unlockedSandboxPartCount,
						customPartIndex = component.customPartIndex
					});
					partDesc.maxCount -= unlockedSandboxPartCount;
				}
				if (unlockedSandboxPartCount > 0)
				{
					num4 += unlockedSandboxPartCount;
					WPFMonoBehaviour.levelManager.m_totalAvailableParts += unlockedSandboxPartCount;
				}
				num++;
				if (num >= m_itemsPerRow)
				{
					num = 0;
					num2++;
				}
				num3++;
			}
			list2.Add(partDesc);
		}
		m_unlockedParts = list;
		if (num4 > 0)
		{
			m_partDescs = list2;
			RefreshButtons();
		}
		if (OnPartsUnlocked != null)
		{
			OnPartsUnlocked();
		}
	}

	public void RefreshButtons()
	{
		partSelector.SetParts(m_partDescs);
	}

	private void HandlePreviewSwipe()
	{
		if (m_draggedElement == -1)
		{
			GuiManager.Pointer pointer = GuiManager.GetPointer();
			if (pointer.down)
			{
				m_previewSwipeStarted = true;
				m_previewSwipeStartPosition = pointer.position;
			}
			if (pointer.up)
			{
				if (m_previewSwipeStarted && Vector3.Distance(m_previewSwipeStartPosition, pointer.position) / (float)Screen.width > 0.4f)
				{
					EventManager.Send(new UIEvent(UIEvent.Type.Preview));
				}
				m_previewSwipeStarted = false;
			}
		}
		else
		{
			m_previewSwipeStarted = false;
		}
	}

	private void HandleTutorialButton()
	{
		if (m_tutorialPulseDone)
		{
			return;
		}
		int tutorialBookOpenCount = GameProgress.GetTutorialBookOpenCount();
		if (tutorialBookOpenCount < 3)
		{
			if (Time.time - m_lastMoveTime > 7.5f * (float)tutorialBookOpenCount)
			{
				EventManager.Send(new PulseButtonEvent(UIEvent.Type.OpenTutorial));
				m_tutorialPulseDone = true;
			}
		}
		else
		{
			m_tutorialPulseDone = true;
		}
	}

	public Vector3 RelativeLevelPositionToHudPosition(Vector3 levelOffset)
	{
		Vector3 position = base.transform.position + levelOffset;
		Vector3 position2 = Camera.main.WorldToScreenPoint(position);
		return WPFMonoBehaviour.hudCamera.GetComponent<Camera>().ScreenToWorldPoint(position2);
	}

	private void SetHudPositionFromRelativeLevelPosition(GameObject obj, Vector3 levelOffset, Vector3 hudOffset)
	{
		Vector3 position = obj.transform.position;
		float z = position.z;
		Vector3 position2 = base.transform.position + levelOffset;
		Vector3 position3 = Camera.main.WorldToScreenPoint(position2);
		Vector3 vector = WPFMonoBehaviour.hudCamera.GetComponent<Camera>().ScreenToWorldPoint(position3);
		vector += hudOffset;
		vector.z = z;
		if (Vector3.SqrMagnitude(position - vector) > 1E-06f)
		{
			obj.transform.position = vector;
		}
	}

	private void ReceiveUIEvent(UIEvent data)
	{
		switch (data.type)
		{
		case UIEvent.Type.MoveContraptionLeft:
			MoveContraption(-1, 0);
			break;
		case UIEvent.Type.MoveContraptionRight:
			MoveContraption(1, 0);
			break;
		case UIEvent.Type.MoveContraptionUp:
			MoveContraption(0, 1);
			break;
		case UIEvent.Type.MoveContraptionDown:
			MoveContraption(0, -1);
			break;
		}
		if (INSettings.GetBool(INFeature.SaveButton) && data.type == UIEvent.Type.SaveContraption)
		{
			GameMode currentGameMode = WPFMonoBehaviour.levelManager.CurrentGameMode;
			m_contraption.CreateAndSaveContraption(currentGameMode.GetCurrentContraptionName());
		}
	}

	public void ContraptionPartChanged(int x, int y)
	{
		GridlessBuild.InvalidateBounds(m_contraption);
	}

	// Retained for existing GameMode/LevelManager callers; translation now uses the toolbar gizmo.
	public void SetMoveButtonStates()
	{
	}

	private void MoveContraption(int dx, int dy)
	{
		AddMove();
		// Legacy UI events remain usable by external callers.
		if (GridlessBuild.Enabled)
		{
			GridlessBuild.NudgeVehicle(m_contraption, dx, dy);
		}
		else
		{
			m_contraption.MoveOnGrid(dx, dy);
		}
	}

	public void SelectPart(PartDesc partDesc)
	{
		m_selectedElement = -1;
		for (int i = 0; i < m_partDescs.Count; i++)
		{
			if (m_partDescs[i].part == partDesc.part)
			{
				m_selectedElement = i;
				break;
			}
		}
	}

	// 供“快捷切换部件”使用：清除当前选中的部件，
	// 避免单击部件后指针移到网格上被自动放置（Update 里 m_selectedElement 的放置分支）。
	public void ClearSelectedPart()
	{
		m_selectedElement = -1;
		if ((bool)partSelector)
		{
			partSelector.ResetSelection();
		}
	}

	public void StartDrag(PartDesc partDesc)
	{
		if (WPFMonoBehaviour.levelManager.gameState != LevelManager.GameState.Building)
		{
			return;
		}
		int draggedElement = -1;
		for (int i = 0; i < m_partDescs.Count; i++)
		{
			if (m_partDescs[i] == partDesc)
			{
				if (m_partDescs[i].HasAvailableParts)
				{
					m_partDescs[i].useCount++;
					EventManager.Send(new PartCountChanged(partDesc.part.m_partType, m_partDescs[i].CurrentCount, partDesc.part.customPartIndex));
					draggedElement = i;
				}
				break;
			}
		}
		SetDraggedElement(draggedElement);
	}

	public bool IsDragging()
	{
		return m_draggedElement != -1;
	}

	public void CancelDrag(PartDesc partDesc)
	{
		SetDraggedElement(-1);
	}

	public void CancelDrag()
	{
		if (m_draggedElement != -1)
		{
			if (m_draggingFromContraption)
			{
				PartDesc partDesc = m_partDescs[m_draggedElement];
				partDesc.useCount++;
				EventManager.Send(new PartCountChanged(partDesc.part.m_partType, partDesc.CurrentCount, partDesc.part.customPartIndex));
				BasePart basePart = SetPartAt(m_contraptionDragX, m_contraptionDragY, partDesc.part);
				ContraptionPartChanged(m_contraptionDragX, m_contraptionDragY);
				basePart.OnPartPlaced();
				EventManager.Send(new PartPlacedEvent(partDesc.part.m_partType, partDesc.part.m_partTier, basePart.transform.position));
			}
			SetDraggedElement(-1);
		}
	}

	private void SetDraggedElement(int element)
	{
		SetDraggedElement(element, fromContraption: false, 0, 0);
	}

	private void SetDraggedElement(int element, bool fromContraption, int contraptionX, int contraptionY)
	{
		m_draggingFromContraption = fromContraption;
		m_contraptionDragX = contraptionX;
		m_contraptionDragY = contraptionY;
		m_dragStarted = false;
		if (m_useDragOffset)
		{
			if (fromContraption)
			{
				m_dragOffset = new Vector3(0f, 0.75f, 0f);
			}
			else
			{
				m_dragOffset = new Vector3(0f, 3f, 0f);
			}
		}
		if (m_draggedElement != -1)
		{
			PartDesc partDesc = m_partDescs[m_draggedElement];
			partDesc.useCount--;
			EventManager.Send(new PartCountChanged(partDesc.part.m_partType, partDesc.CurrentCount, partDesc.part.customPartIndex));
		}
		m_draggedElement = element;
		if (element == -1)
		{
			if ((bool)m_dragIcon)
			{
				UnityEngine.Object.Destroy(m_dragIcon);
				m_dragIcon = null;
			}
			return;
		}
		if ((bool)m_dragIcon)
		{
			UnityEngine.Object.Destroy(m_dragIcon);
		}
		GameObject gameObject = null;
		BasePart basePart = null;
		if (m_draggingFromContraption)
		{
			BasePart selectedPart = m_partDescs[element].part;
			basePart = selectedPart is RCR2Port.RoverPortPart ? selectedPart : WPFMonoBehaviour.gameData.GetCustomPart(selectedPart.m_partType, m_draggedElementCustomizationIndex);
		}
		if (basePart != null)
		{
			gameObject = basePart.m_constructionIconSprite.gameObject;
		}
		if (gameObject == null)
		{
			gameObject = m_partDescs[element].part.m_constructionIconSprite.gameObject;
		}
		m_dragIcon = UnityEngine.Object.Instantiate(gameObject);
		float num = Vector3.Distance(GridPositionToGuiPosition(0, 0), GridPositionToGuiPosition(1, 0));
		SetSortingOrder(m_dragIcon, 0, "Popup");
		m_dragIcon.transform.localScale = new Vector3(num, num, num);
		EventManager.Send(new DragStartedEvent(m_partDescs[element].part.m_partType));
	}

	public static void SetSortingOrder(GameObject target, int sortingOrder, string sortingLayer = "")
	{
		if (target == null)
		{
			return;
		}
		Renderer[] componentsInChildren = target.GetComponentsInChildren<Renderer>();
		if (componentsInChildren == null || componentsInChildren.Length == 0)
		{
			return;
		}
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			if (!string.IsNullOrEmpty(sortingLayer))
			{
				componentsInChildren[i].sortingLayerName = sortingLayer;
			}
			componentsInChildren[i].sortingOrder = sortingOrder;
		}
	}

	private int FindPartIndex(BasePart part)
	{
		for (int i = 0; i < m_partDescs.Count; i++)
		{
			if (SamePartIdentity(m_partDescs[i].part, part))
			{
				return i;
			}
		}
		return -1;
	}

	private void OnPartCustomization(CustomizePartUI.PartCustomizationEvent data)
	{
		PartDesc partDesc = FindPartDesc(data.customizedPart, data.customPartIndex);
		if (partDesc != null)
		{
			EventManager.Send(new PartCountChanged(partDesc.part.m_partType, partDesc.CurrentCount, partDesc.part.customPartIndex));
		}
	}

	private void HandleDragging()
	{
		if (ContraptionLayers.Enabled(m_contraption) && ContraptionLayers.ActiveLayer == 0) return;
		if (INPartParameterPanel.ContainsPointer(GuiManager.GetPointer().position))
		{
			return;
		}
		if (WPFMonoBehaviour.levelManager.gameState != LevelManager.GameState.Building)
		{
			return;
		}
		GuiManager.Pointer pointer = GuiManager.GetPointer();
		if (DeviceInfo.UsesTouchInput)
		{
			if (Input.touchCount != 0)
			{
				TouchPhase phase = Input.GetTouch(0).phase;
				if (INSettings.GetBool(INFeature.PartPlacementOperation))
				{
					if (phase == TouchPhase.Moved)
					{
						m_pointerTime.x += Time.deltaTime;
					}
					if (phase == TouchPhase.Ended)
					{
						m_pointerTime.x = 0f;
					}
					if (m_pointerTime.x >= 0.4f || m_dragStarted)
					{
						m_pointerTime.y = 0f;
					}
				}
				if (INSettings.GetBool(INFeature.PartDeletionOperation))
				{
					if (phase == TouchPhase.Stationary)
					{
						m_pointerTime.y += Time.deltaTime;
					}
					if (phase == TouchPhase.Ended)
					{
						m_pointerTime.y = 0f;
					}
					if (m_pointerTime.y >= 0.4f)
					{
						m_pointerTime.x = 0f;
					}
				}
				if (INSettings.GetBool(INFeature.PartDeselectionOperation))
				{
					if (phase == TouchPhase.Stationary && pointer.onWidget)
					{
						m_pointerTime.z += Time.deltaTime;
					}
					if (phase == TouchPhase.Ended)
					{
						m_pointerTime.z = 0f;
					}
					if (m_pointerTime.z >= 0.4f)
					{
						partSelector.ResetSelection();
						m_selectedElement = -1;
					}
				}
			}
		}
		else
		{
			if (INSettings.GetBool(INFeature.PartPlacementOperation))
			{
				if (pointer.dragging)
				{
					m_pointerTime.x += Time.deltaTime;
				}
				else
				{
					m_pointerTime.x = 0f;
				}
			}
			if (INSettings.GetBool(INFeature.PartDeselectionOperation) && pointer.secondaryDown && pointer.onWidget)
			{
				partSelector.ResetSelection();
				m_selectedElement = -1;
			}
		}
		int constructionUiRows = m_partDescs.Count / m_itemsPerRow + ((m_partDescs.Count % m_itemsPerRow != 0) ? 1 : 0);
		WPFMonoBehaviour.levelManager.m_constructionUiRows = constructionUiRows;
		if (pointer.down && !pointer.onWidget && m_draggedElement == -1 && !m_dragStarted)
		{
			Vector3 vector = BuildMachineTransform.PointerLocal(m_contraption, WPFMonoBehaviour.ScreenToZ0(pointer.position));
			int coordX = Mathf.RoundToInt(vector.x);
			int coordY = Mathf.RoundToInt(vector.y);
			ResolvePointerCell(pointer.position, ref coordX, ref coordY);
			BasePart basePart = m_contraption.FindPartAt(coordX, coordY);
			if ((bool)basePart)
			{
				if ((bool)basePart.enclosedPart)
				{
					basePart = basePart.enclosedPart;
				}
				if (!basePart.m_static)
				{
					// Besiege-style tool: with one selected, the click applies that tool and is consumed,
					// so the stock meaning of a click (tap rotates, long press opens the panel, drag moves)
					// does not also fire. Without a tool this returns false and nothing below changes.
					if (BuildToolPalette.TryApplyToPart(m_contraption, basePart))
					{
						AddMove();
						return;
					}
					m_dragStartPosition = pointer.position;
					m_dragStarted = true;
					m_partPressTime = 0f;
					m_partPressTarget = basePart;
					m_partPressStartPosition = pointer.position;
				}
			}
		}
		if (pointer.up)
		{
			if (m_dragStarted && m_draggedElement == -1 && m_partPressTime < PartLongPressDuration)
			{
			Vector3 vector2 = BuildMachineTransform.PointerLocal(m_contraption, WPFMonoBehaviour.ScreenToZ0(m_dragStartPosition));
				int x = Mathf.RoundToInt(vector2.x);
				int y = Mathf.RoundToInt(vector2.y);
				ResolvePointerCell(m_dragStartPosition, ref x, ref y);
				BasePart basePart2 = m_contraption.FindPartAt(x, y);
				if ((bool)basePart2)
				{
					bool willEncloseSelectedPart = false;
					if (m_selectedElement != -1 && !pointer.onWidget && basePart2.CanEncloseParts())
					{
						PartDesc selectedPartDesc = m_partDescs[m_selectedElement];
						if (selectedPartDesc != null && selectedPartDesc.part.CanBeEnclosed() && (!basePart2.enclosedPart || selectedPartDesc.part.m_partType != basePart2.enclosedPart.m_partType) && selectedPartDesc.useCount < selectedPartDesc.maxCount)
						{
							willEncloseSelectedPart = true;
						}
					}
					if ((bool)basePart2.enclosedPart)
					{
						basePart2 = basePart2.enclosedPart;
					}
					if (!willEncloseSelectedPart && m_contraption.Flip(basePart2))
					{
						AddMove();
						m_rotationCounter++;
						Singleton<AudioManager>.Instance.Play2dEffect(WPFMonoBehaviour.gameData.commonAudioCollection.rotatePart);
					}
				}
			}
			m_dragStarted = false;
			m_partPressTarget = null;
			m_partPressTime = 0f;
		}
		float num = 1f;
		if (DeviceInfo.UsesTouchInput)
		{
			num = ((!m_useDragOffset) ? 10f : 20f);
		}
		if (m_dragStarted && m_draggedElement == -1 && m_partPressTarget != null)
		{
			if (pointer.touching && Vector3.Distance(pointer.position, m_partPressStartPosition) < num)
			{
				m_partPressTime += Time.unscaledDeltaTime;
				if (m_partPressTime >= PartLongPressDuration)
				{
					BasePart partTarget = m_partPressTarget;
					m_partPressTarget = null;
					m_partPressTime = 0f;
					m_dragStarted = false;
					if (INParamSystem.Enabled) INPartParameterPanel.Open(partTarget);
				}
			}
			else
			{
				m_partPressTarget = null;
				m_partPressTime = 0f;
			}
		}
		if (m_dragStarted && m_draggedElement == -1 && Vector3.Distance(pointer.position, m_dragStartPosition) >= num)
		{
			// Besiege-style Translate: the drag moves the whole contraption instead of picking the part up.
			// The shift follows the pointer from where the drag began, in the same build-local space the
			// cells live in, so the machine tracks the hand exactly like the reference's translate tool.
			Vector3 vector3 = BuildMachineTransform.PointerLocal(m_contraption, WPFMonoBehaviour.ScreenToZ0(m_dragStartPosition));
			int coordX2 = Mathf.RoundToInt(vector3.x);
			int coordY2 = Mathf.RoundToInt(vector3.y);
			ResolvePointerCell(m_dragStartPosition, ref coordX2, ref coordY2);
			BasePart basePart3 = m_contraption.FindPartAt(coordX2, coordY2);
			if (!basePart3 || (basePart3.m_partType != BasePart.PartType.Rope && basePart3.m_partType != BasePart.PartType.Spring))
			{
				ResolvePointerCell(m_dragStartPosition, ref coordX2, ref coordY2);
			}
			BasePart basePart4 = m_contraption.RemovePartAt(coordX2, coordY2);
			if ((bool)basePart4)
			{
				m_draggedElementCustomizationIndex = basePart4.customPartIndex;
				m_dragStoredCustomParams = basePart4.SerializeCustomParams();
				EventManager.Send(new PartRemovedEvent(basePart4.m_partType, basePart4.transform.position));
				draggedPartRotation = basePart4.m_gridRotation;
				draggedPartFlipped = basePart4.m_flipped;
				for (int i = 0; i < m_partDescs.Count; i++)
				{
					if (SamePartIdentity(m_partDescs[i].part, basePart4))
					{
						m_dragStartedFromContraption = true;
						SetDraggedElement(i, fromContraption: true, coordX2, coordY2);
						m_selectedElement = i;
						partSelector.SetSelection(m_partDescs[m_selectedElement]);
						break;
					}
				}
				if (INSettings.GetBool(INFeature.AutoSetPartBuildingIndex))
				{
					CustomizationManager.SetLastUsedPartIndex(basePart4.m_partType, basePart4.customPartIndex);
					EventManager.Send(new CustomizePartUI.PartCustomizationEvent(basePart4.m_partType, basePart4.customPartIndex));
				}
				if (INSettings.GetBool(INFeature.AutoSetPartBuildingRotation) && m_contraption.PartRotationMap != null)
				{
					PartTypeInfo key = new PartTypeInfo(basePart4);
					m_contraption.PartRotationMap[key] = ((int)basePart4.m_gridRotation, basePart4.m_flipped);
				}
				UnityEngine.Object.Destroy(basePart4.gameObject);
				ContraptionPartChanged(coordX2, coordY2);
				PlayDragSound();
			}
		}
		if (m_draggedElement != -1)
		{
			PartDesc partDesc = m_partDescs[m_draggedElement];
			Vector3 position = WPFMonoBehaviour.hudCamera.GetComponent<Camera>().ScreenToWorldPoint(pointer.position) + m_dragOffset;
			Vector3 position2 = WPFMonoBehaviour.hudCamera.GetComponent<Camera>().WorldToScreenPoint(position);
			Vector3 vector4 = Camera.main.ScreenToWorldPoint(position2);
			EventManager.Send(new DraggingPartEvent(partDesc.part.m_partType, vector4));
			Vector3 vector5 = BuildMachineTransform.PointerLocal(m_contraption, vector4);
			int num2 = Mathf.RoundToInt(vector5.x);
			int num3 = Mathf.RoundToInt(vector5.y);
			if (m_useDragOffset)
			{
				int key2 = num3 * 1000 + num2;
				if (m_cellMap.TryGetValue(key2, out var value))
				{
					if ((bool)m_mouseOverCell && m_mouseOverCell != value)
					{
						m_mouseOverCell.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
						// 无网格模式 moved this marker off its cell; put it back before it stops being hovered.
						GridlessBuild.RestoreHover(m_mouseOverCell);
						// The grid is hidden in this mode except for the hovered marker, so the one being
						// left has to be hidden and the one being entered shown.
						SetCellVisible(m_mouseOverCell);
					}
					m_mouseOverCell = value;
					value.transform.localScale = new Vector3(0.33f, 0.33f, 0.33f);
					SetCellVisible(value);
					// 无网格模式: the marker is the build screen's only preview, so it is moved onto the
					// position the part will actually take instead of staying on the cell centre.
					GridlessBuild.MarkHover(value,
						GridlessBuild.Enabled ? new Vector2(vector5.x - num2, vector5.y - num3) : Vector2.zero);
				}
				else if ((bool)m_mouseOverCell)
				{
					m_mouseOverCell.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
					GridlessBuild.RestoreHover(m_mouseOverCell);
					SetCellVisible(m_mouseOverCell);
				}
			}
			if (pointer.up)
			{
				partDesc.customPartIndex = m_draggedElementCustomizationIndex;
				AddMove();
				if (WPFMonoBehaviour.levelManager.CanPlacePartAtGridCell(num2, num3) && m_contraption.CanPlaceSpecificPartAt(num2, num3, partDesc.part) && !pointer.onWidget)
				{
					partDesc.useCount++;
					EventManager.Send(new PartCountChanged(partDesc.part.m_partType, partDesc.CurrentCount, partDesc.part.customPartIndex));
					BasePart basePart5 = partDesc.part;
					if (m_draggingFromContraption)
					{
						basePart5 = WPFMonoBehaviour.gameData.GetCustomPart(basePart5.m_partType, m_draggedElementCustomizationIndex);
					}
					GridlessBuild.PrepareWorld(m_contraption, num2, num3, vector4);
					BasePart basePart6 = SetPartAt(num2, num3, basePart5);
					if (basePart6 != null && !string.IsNullOrEmpty(m_dragStoredCustomParams))
					{
						Vector2 placedOffset = GridlessBuild.GetOffset(basePart6);
						int placedGroup = GridlessBuild.GetGroupId(basePart6);
						BuildMachineTransform placedFrame = BuildMachineTransform.Read(basePart6);
						basePart6.DeserializeCustomParams(m_dragStoredCustomParams);
						GridlessBuild.SetOffset(basePart6, placedOffset);
						GridlessBuild.SetGroupId(basePart6, placedGroup);
						placedFrame.Store(basePart6);
						m_contraption.RefreshPartPositions();
					}
					m_dragStoredCustomParams = null;
					ContraptionPartChanged(num2, num3);
					PlayPartPlacedSound();
					basePart6.OnPartPlaced();
					EventManager.Send(new PartPlacedEvent(partDesc.part.m_partType, partDesc.part.m_partTier, basePart6.transform.position));
				}
				else
				{
					PlayRemoveSound();
				}
				m_dragStoredCustomParams = null;
				SetDraggedElement(-1);
			}
		}
		else if (m_selectedElement != -1 && m_draggedElement == -1 && !pointer.onWidget)
		{
			PartDesc partDesc2 = m_partDescs[m_selectedElement];
			Vector3 vector6 = BuildMachineTransform.PointerLocal(m_contraption, WPFMonoBehaviour.ScreenToZ0(pointer.position));
			int num4 = Mathf.RoundToInt(vector6.x);
			int num5 = Mathf.RoundToInt(vector6.y);
			BasePart basePart7 = m_contraption.FindPartAt(num4, num5);
			if (!basePart7 && (m_pointerTime.x >= 0.4f || pointer.down || (m_allowDragPlacement && pointer.dragging)) && partDesc2.HasAvailableParts)
			{
				if (TryPlacePartAtGridCell(num4, num5, partDesc2))
				{
					AddMove();
				}
			}
			else if ((bool)basePart7 && basePart7.CanEncloseParts() && partDesc2.part.CanBeEnclosed() && (m_pointerTime.x >= 0.4f || pointer.up) && (!basePart7.enclosedPart || partDesc2.part.m_partType != basePart7.enclosedPart.m_partType) && partDesc2.HasAvailableParts && TryPlacePartAtGridCell(num4, num5, partDesc2))
			{
				AddMove();
			}
		}
		HandlePartDeletion(pointer);
		if (m_draggedElement == -1 && (bool)m_mouseOverCell)
		{
			m_mouseOverCell.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
			GridlessBuild.RestoreHover(m_mouseOverCell);
			SetCellVisible(m_mouseOverCell);
			m_mouseOverCell = null;
		}
	}

	private void HandlePartDeletion(GuiManager.Pointer pointer)
	{
		if (!INBuildToolPalette.IsBuildState() || INPartParameterPanel.ContainsPointer(pointer.position)) return;
		if (m_draggedElement == -1 && !pointer.onWidget && (m_pointerTime.y >= 0.4f ||
			(!DeviceInfo.UsesTouchInput && (pointer.secondaryDown || pointer.secondaryDragging))))
		{
			Vector3 vector7 = BuildMachineTransform.PointerLocal(m_contraption, WPFMonoBehaviour.ScreenToZ0(pointer.position));
			int x2 = Mathf.RoundToInt(vector7.x);
			int y2 = Mathf.RoundToInt(vector7.y);
			BasePart basePart8 = PickDeletionPart(pointer.position);
			if (basePart8 != null) { x2 = basePart8.m_coordX; y2 = basePart8.m_coordY; }
			else
			{
				ChangeCoordinatesToSelectBigPart(ref x2, ref y2);
				basePart8 = m_contraption.FindPartAt(x2, y2);
			}
			if ((bool)basePart8 && (bool)basePart8.enclosedPart)
			{
				basePart8 = basePart8.enclosedPart;
			}
			if ((bool)basePart8 && !basePart8.m_static)
			{
				if (RemoveBuildPart(basePart8)) AddMove();
			}
		}
	}

	internal bool RemoveBuildPart(BasePart part)
	{
		if (part == null || part.m_static || m_contraption == null || !m_contraption.TryRemovePart(part)) return false;
		int x = part.m_coordX, y = part.m_coordY;
		EventManager.Send(new PartRemovedEvent(part.m_partType, part.transform.position));
		ReturnPartInventory(part);
		m_contraption.RefreshNeighbours(x, y);
		ContraptionPartChanged(x, y);
		if (Application.isPlaying) UnityEngine.Object.Destroy(part.gameObject);
		else UnityEngine.Object.DestroyImmediate(part.gameObject);
		return true;
	}

	private void ReturnPartInventory(BasePart part)
	{
		foreach (PartDesc desc in m_partDescs)
			if (desc != null && SamePartIdentity(desc.part, part))
			{
				// A confirmed removal returns one part to one inventory row. Repair legacy underflow as well.
				desc.useCount = Mathf.Max(0, desc.useCount - 1);
				EventManager.Send(new PartCountChanged(desc.part.m_partType, desc.CurrentCount, desc.part.customPartIndex));
				break;
			}
	}

	private void ChangeCoordinatesToSelectBigPart(ref int coordX, ref int coordY)
	{
		BasePart directPart = m_contraption.FindPartAt(coordX, coordY);
		foreach (BasePart candidate in m_contraption.Parts)
		{
			if (directPart == null && candidate is RCR2Port.RoverPortPart &&
				coordX >= candidate.m_coordX + candidate.m_gridXmin && coordX <= candidate.m_coordX + candidate.m_gridXmax &&
				coordY >= candidate.m_coordY + candidate.m_gridYmin && coordY <= candidate.m_coordY + candidate.m_gridYmax)
			{
				coordX = candidate.m_coordX;
				coordY = candidate.m_coordY;
				return;
			}
		}
		for (int i = -1; i <= 0; i++)
		{
			for (int j = -1; j <= 1; j++)
			{
				if (j == 0 && i == 0)
				{
					continue;
				}
				int num = coordX + j;
				int num2 = coordY + i;
				BasePart basePart = m_contraption.FindPartAt(num, num2);
				if ((bool)basePart)
				{
					if ((bool)basePart.enclosedPart)
					{
						basePart = basePart.enclosedPart;
					}
					if (num + basePart.m_gridXmin <= coordX && num + basePart.m_gridXmax >= coordX && num2 + basePart.m_gridYmin <= coordY && num2 + basePart.m_gridYmax >= coordY)
					{
						coordX = num;
						coordY = num2;
						return;
					}
				}
			}
		}
	}

	/// <summary>
	/// 无网格模式: tells <see cref="GridlessBuild"/> where inside the target cell the pointer is, so the
	/// part about to be placed lands there rather than on the cell centre.
	/// </summary>
	/// <remarks>
	/// The pointer is read live instead of taking the caller's value because the two placement paths
	/// derive it differently - the drag path is offset by <c>m_dragOffset</c> and projected through the HUD
	/// camera - while all that is wanted here is the fraction inside a cell the caller has already
	/// validated. Reading the cell from the pointer cannot change which cell is occupied, so validity,
	/// part counts and connectivity are exactly what they were without the mode.
	/// </remarks>
	private void PrepareGridlessOffset(int coordX, int coordY)
	{
		GridlessBuild.PrepareWorld(m_contraption, coordX, coordY, WPFMonoBehaviour.ScreenToZ0(GuiManager.GetPointer().position));
	}

	private bool TryPlacePartAtGridCell(int coordX, int coordY, PartDesc partDesc)
	{
		if (WPFMonoBehaviour.levelManager.CanPlacePartAtGridCell(coordX, coordY) && m_contraption.CanPlaceSpecificPartAt(coordX, coordY, partDesc.part))
		{
			partDesc.useCount++;
			EventManager.Send(new PartCountChanged(partDesc.part.m_partType, partDesc.CurrentCount, partDesc.part.customPartIndex));
			PrepareGridlessOffset(coordX, coordY);
			BasePart basePart = SetPartAt(coordX, coordY, partDesc.part);
			ContraptionPartChanged(coordX, coordY);
			PlayPartPlacedSound();
			basePart.OnPartPlaced();
			EventManager.Send(new PartPlacedEvent(basePart.m_partType, basePart.m_partTier, basePart.transform.position));
			return true;
		}
		return false;
	}

	private void ClearNonChassisPart(int coordX, int coordY, BasePart self = null)
	{
		BasePart basePart = self != null ? m_contraption.FindPartAt(coordX, coordY, self) : m_contraption.FindPartAt(coordX, coordY);
		if (!basePart || (basePart.IsPartOfChassis() && !basePart.enclosedPart))
		{
			return;
		}
		if (basePart.enclosedPart != null) basePart = basePart.enclosedPart;
		RemoveBuildPart(basePart);
	}

	public Vector3 GridPositionToGuiPosition(int x, int y)
	{
		Vector3 position = m_contraption.transform.position + Vector3.right * x + Vector3.up * y;
		Vector3 position2 = Camera.main.WorldToScreenPoint(position);
		return WPFMonoBehaviour.hudCamera.GetComponent<Camera>().ScreenToWorldPoint(position2);
	}

	public Vector3 GridPositionToWorldPosition(int x, int y)
	{
		return m_contraption.transform.position + Vector3.right * x + Vector3.up * y;
	}

	private void ClearCollidingParts(int coordX, int coordY, BasePart part)
	{
		for (int i = part.m_gridYmin; i <= part.m_gridYmax; i++)
		{
			for (int j = part.m_gridXmin; j <= part.m_gridXmax; j++)
			{
				if (j == 0 && i == 0)
				{
					continue;
				}
				if (part.m_partType == BasePart.PartType.GoldenPig)
				{
					BasePart basePart = m_contraption.FindPartAt(coordX + j, coordY + i, part);
					if ((bool)basePart && (basePart.m_partType == BasePart.PartType.Rope || basePart.m_partType == BasePart.PartType.Spring))
					{
						continue;
					}
				}
				ClearNonChassisPart(coordX + j, coordY + i, part);
			}
		}
		if (part.IsPartOfChassis())
		{
			return;
		}
		for (int k = -1; k <= 0; k++)
		{
			for (int l = -1; l <= 1; l++)
			{
				BasePart basePart2 = m_contraption.FindPartAt(coordX + l, coordY + k, part);
				if ((bool)basePart2)
				{
					if ((bool)basePart2.enclosedPart)
					{
						basePart2 = basePart2.enclosedPart;
					}
					if ((basePart2.m_partType == BasePart.PartType.KingPig || basePart2.m_partType == BasePart.PartType.GoldenPig) && ((part.m_partType != BasePart.PartType.Rope && part.m_partType != BasePart.PartType.Spring) || basePart2.m_partType != BasePart.PartType.GoldenPig))
					{
						ClearNonChassisPart(coordX + l, coordY + k, part);
					}
				}
			}
		}
	}

	public BasePart SetPartAt(int coordX, int coordY, BasePart part, bool autoalign = true, bool preserveExisting = false, string customParams = null)
	{
		GameObject obj = UnityEngine.Object.Instantiate(part.gameObject);
		obj.SetActive(value: true);
		BasePart component = obj.GetComponent<BasePart>();
		// Structural layer must be known before enclosure and map insertion.
		if (customParams != null) component.DeserializeCustomParams(customParams);
		else if (ContraptionLayers.Enabled(m_contraption)) component.SetStoredParam(PartPhysicsParameters.LayerKey, ContraptionLayers.QueryLayer);
		m_contraption.AdoptImportedLayers(component);
		BasePart basePart = m_contraption.FindPartAt(coordX, coordY);
		if (component.m_partType == BasePart.PartType.Pig && (bool)basePart && basePart.m_partType == BasePart.PartType.WoodenFrame && Singleton<SocialGameManager>.IsInstantiated())
		{
			Singleton<SocialGameManager>.Instance.ReportAchievementProgress("grp.THINK_INSIDE_THE_BOX", 100.0);
		}
		if (component is RCR2Port.RoverPortPart && INSettings.GetBool(INFeature.AutoSetPartBuildingRotation))
		{
			PartTypeInfo identity = new PartTypeInfo(component);
			if (m_contraption.PartRotationMap != null && m_contraption.PartRotationMap.TryGetValue(identity, out var savedRotation))
			{
				component.SetFlipped(savedRotation.Item2);
				component.SetRotation((BasePart.GridRotation)savedRotation.Item1);
			}
		}
		if (!preserveExisting) ClearCollidingParts(coordX, coordY, component);
		component.PrePlaced();
		// 无网格模式: the offset has to be recorded before the Contraption places the part, because that
		// is the call which turns the cell into a position.
		GridlessBuild.ConsumePending(component);
		BasePart basePart2 = m_contraption.SetPartAt(coordX, coordY, component, autoalign, preserveExisting);
		if (autoalign)
		{
			m_contraption.AutoAlign(component);
		}
		if ((bool)basePart2)
		{
			EventManager.Send(new PartRemovedEvent(basePart2.m_partType, basePart2.transform.position));
			CollectPart(basePart2);
		}
		if (INSettings.GetBool(INFeature.AutoSetPartBuildingRotation))
		{
			BasePart basePart3 = component;
			PartTypeInfo key = new PartTypeInfo(basePart3);
			if (m_contraption.PartRotationMap != null && m_contraption.PartRotationMap.TryGetValue(key, out var value))
			{
				if (value.Item2)
				{
					basePart3.SetFlipped(value.Item2);
				}
				basePart3.SetRotation((BasePart.GridRotation)value.Item1);
				basePart3.OnChangeConnections();
				m_contraption.RefreshNeighboursVisual(basePart3.m_coordX, basePart3.m_coordY);
			}
		}
		// 若该实例自带自定义参数（例如被“复制器”克隆而来），在此应用，保证克隆体状态一致
		string diagParams = component.SerializeCustomParams();
		if (!string.IsNullOrEmpty(diagParams))
		{
			Debug.Log("[PARAMDIAG] SetPartAt instance params='" + diagParams + "'");
		}
		component.ApplyCustomParams();
		Debug.Log("[QuickInherit] SetPartAt hook notify=" + (!m_draggingFromContraption) + " draggingFromContraption=" + m_draggingFromContraption);
		if (!m_draggingFromContraption && customParams == null)
		{
			INQuickInherit.OnPlaced(component);
		}
		return component;
	}

	protected void CollectPart(BasePart part)
	{
		if (!part)
		{
			return;
		}
		ReturnPartInventory(part);
		if ((bool)part.enclosedPart)
		{
			CollectPart(part.enclosedPart);
		}
		UnityEngine.Object.Destroy(part.gameObject);
	}

	public void SetEnabled(bool enableUI, bool enableGrid)
	{
		m_gridRequested = enableGrid;
		if ((bool)m_grid)
		{
			m_grid.gameObject.SetActive(enableGrid && !GridlessBuild.Enabled);
		}
		partSelector.SetConstructionUI(this);
		if (enableUI)
		{
			m_contraption.RefreshConnections();
		}
		base.gameObject.SetActive(enableUI || enableGrid);
		base.enabled = enableUI;
	}

	public void ClearContraption()
	{
		m_contraption.RemoveAllDynamicParts();
		foreach (PartDesc partDesc in m_partDescs)
		{
			partDesc.useCount = 0;
			EventManager.Send(new PartCountChanged(partDesc.part.m_partType, partDesc.CurrentCount, partDesc.part.customPartIndex));
		}
		SetMoveButtonStates();
		m_moveCounter = 0;
	}

	public void ApplySuperGlue(bool apply)
	{
		m_contraption.ApplySuperGlue(Glue.Type.Regular);
	}

	public void ApplySuperMagnet(bool apply)
	{
		m_contraption.HasSuperMagnet = apply;
	}

	public void ApplyTurboCharge(bool apply)
	{
		m_contraption.HasTurboCharge = apply;
	}

	public void ApplyNightVision(bool apply)
	{
		m_contraption.HasNightVision = apply;
	}

	public void PlayPartPlacedSound()
	{
		if (Singleton<AudioManager>.IsInstantiated())
		{
			Singleton<AudioManager>.Instance.Play2dEffect(WPFMonoBehaviour.gameData.commonAudioCollection.placePart);
		}
	}

	public void PlayDragSound()
	{
		if (WPFMonoBehaviour.gameData.commonAudioCollection.dragPart != null && Singleton<AudioManager>.IsInstantiated())
		{
			Singleton<AudioManager>.Instance.Play2dEffect(WPFMonoBehaviour.gameData.commonAudioCollection.dragPart);
		}
	}

	public void PlayRemoveSound()
	{
		if (Singleton<AudioManager>.IsInstantiated())
		{
			Singleton<AudioManager>.Instance.Play2dEffect(WPFMonoBehaviour.gameData.commonAudioCollection.removePart);
		}
	}
}

