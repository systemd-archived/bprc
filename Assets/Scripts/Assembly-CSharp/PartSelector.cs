using System;
using System.Collections.Generic;
using UnityEngine;

public partial class PartSelector : WPFMonoBehaviour, WidgetListener
{
	public class PartDescOrder : IComparer<ConstructionUI.PartDesc>
	{
		private GameData gameData;

		public PartDescOrder(GameData data)
		{
			gameData = data;
		}

		public int Compare(ConstructionUI.PartDesc obj1, ConstructionUI.PartDesc obj2)
		{
			int partStarLimit = GetPartStarLimit(obj1.part.m_partType);
			int partStarLimit2 = GetPartStarLimit(obj2.part.m_partType);
			if (partStarLimit < partStarLimit2)
			{
				return -1;
			}
			if (partStarLimit > partStarLimit2)
			{
				return 1;
			}
			if (obj1.sortKey < obj2.sortKey)
			{
				return -1;
			}
			if (obj1.sortKey > obj2.sortKey)
			{
				return 1;
			}
			return 0;
		}

		private int GetPartStarLimit(BasePart.PartType part)
		{
			string currentLevelIdentifier = Singleton<GameManager>.Instance.CurrentLevelIdentifier;
			RaceLevels.LevelUnlockablePartsData levelUnlockableData = gameData.m_raceLevels.GetLevelUnlockableData(currentLevelIdentifier);
			if (levelUnlockableData != null)
			{
				foreach (RaceLevels.UnlockableTier tier in levelUnlockableData.m_tiers)
				{
					if (tier.m_part == part)
					{
						return tier.m_starLimit;
					}
				}
				return -1;
			}
			return -1;
		}
	}

	public GameObject m_partButtonPrefab;

	public GameObject m_partUnavailableButtonPrefab;

	public GameObject m_partUnlockAnimatedLock;

	public GameData m_gameData;

	public Transform m_toolBox;

	public Transform m_partList;

	private ExtendedScrollList m_scrollList;

	private List<ConstructionUI.PartDesc> m_partDescs;

	private ConstructionUI m_constructionUI;

	private Dictionary<BasePart.PartType, int> m_partOrder = new Dictionary<BasePart.PartType, int>();

	private UnlockRoadHogsParts m_unlockPartTierDialog;

	// ===== 快捷切换部件（星海设置 QuickPartSwitch）=====
	private Transform m_quickRoot;
	private readonly List<GameObject> m_quickBtns = new List<GameObject>();
	private ConstructionUI.PartDesc m_quickTarget;
	private float m_quickPressTime;
	private bool m_quickPressOnPart;
	private bool m_quickToggleClose;
	private readonly List<int> m_quickOrder = new List<int>();
	private int m_quickCount;
	private float m_quickAnchorZ;

	// 滑动面板状态
	private int m_quickVisible = 16;      // 一次最多显示的个数（星海设置可调，默认 16）
	private float m_quickStep = 0.9f;    // 每个皮肤的世界间距（按可用宽度自适应）
	private float m_quickRightX;          // 可视窗口【右端】世界 x（部件栏右端）
	private float m_quickCenterX;         // 部件栏中心世界 x（数量少时居中用）
	private float m_quickBarWidth = 16f;  // 部件栏宽度（用于滑动限位换算）
	private bool m_quickCentered;         // 本帧是否“居中显示”
	private float m_quickSlotScale = 0.46f;   // 单个皮肤缩放（参考部件栏按钮大小）
	private float m_quickY;
	private float m_quickZ;
	private int m_quickCurrent = -1;      // 当前正在使用的皮肤下标（呼出时居中 + 醒目标记）
	private float m_quickLastClickTime = -10f;   // 双击判定：上一次点击时间
	private ConstructionUI.PartDesc m_quickLastClickTarget;
	private float m_quickOffset;          // 当前滑动偏移（单位：个）
	private float m_quickMinOffset;       // 滑动限位：左端（最左皮肤到部件栏最左端）
	private float m_quickMaxOffset;       // 滑动限位：右端（最右皮肤到部件栏最右端）
	private float m_quickPxPerItem = 1f;
	private bool m_quickSliding;
	private bool m_quickSlideMoved;
	private bool m_quickPlacementBlocked;   // 滑动一旦触发 → 禁用放置，直到松开手指
	private float m_quickSlideStartPx;
	private float m_quickSlideStartOffset;
	private float m_quickSlideVel;          // 滑动速度（项/秒），用于惯性
	private bool m_quickMomentum;           // 是否处于惯性滑动
	private float m_quickLastOffset;        // 上一帧偏移（用于计算速度）
	private GameObject m_quickBgObj;         // 覆盖可见窗口的背景控件（拦截空隙按下）
	private float m_quickSnapTarget;         // 平滑对齐目标
	private bool m_quickSnapping;            // 是否正在平滑对齐
	private float m_quickAnimTarget;         // 切换皮肤后平滑移动到“当前皮肤居中”的目标偏移
	private bool m_quickAnimating;           // 是否正在平滑移动（避免切换皮肤时瞬移）
	// 当前皮肤“放大”标记：跨重建保留缩放系数，使放大/缩小也是平滑的
	// ⚙ 两个平滑速度（系数越大越快；指数趋近 1-Exp(-k*dt)）：
	private const float QuickZoomMoveSmooth = 6f;      // ① 切换皮肤后「居中移动」的速度
	private const float QuickZoomScaleSmooth = 9f;     // ② 当前皮肤「放大/缩小」的速度（= 6f × 1.5，后续想再调就改这里）
	private const float QuickCurrentZoom = 1.28f;      // 当前皮肤的放大倍率（这是“大小”，不是速度）
	// ⚙ 切换【部件种类】时的过渡动画：新一排从下方滑入，旧一排向上滑出
	// 【已停用】如需恢复该动画，把 QuickIntroEnabled 改回 true 即可（动画代码全部保留）
	private static readonly bool QuickIntroEnabled = false;
	private const float QuickIntroDuration = 0.26f;       // 时长（秒）
	private const float QuickIntroSlideFrac = 0.18f;      // 纵向滑动距离 = 屏幕世界高度 × 该比例
	private const float QuickIntroScaleFrom = 0.7f;       // 新一排的起始缩放（越小入场越明显）
	private bool m_quickIntroActive;
	private float m_quickIntroTimer;
	private float m_quickWorldHeight = 9f;                // 屏幕世界高度（纵向动画距离基准，QuickAnchor 里计算）
	private Transform m_quickOutgoing;                    // 正在向上滑出的旧一排
	private float m_quickOutgoingStartY;
	private float m_quickOutgoingStartScale = 1f;
	private QuickFadeSet m_quickInFade;                   // 新一排的淡入基准（baseAlpha × factor）
	private QuickFadeSet m_quickOutFade;                  // 旧一排的淡出基准
	// 一组渲染器的“基准透明度”，用于在入场/出场时按比例淡入淡出而不破坏原有底色
	private sealed class QuickFadeSet
	{
		public readonly List<Renderer> renderers = new List<Renderer>();
		public readonly List<float> baseAlpha = new List<float>();
	}
	private readonly List<Transform> m_quickSlotTfs = new List<Transform>();
	private readonly List<int> m_quickSlotKeys = new List<int>();
	private readonly List<float> m_quickSlotFactors = new List<float>();
	private readonly List<float> m_quickSlotTargets = new List<float>();
	private readonly Dictionary<int, float> m_quickScaleFactor = new Dictionary<int, float>();

	private static bool QuickSwitchOn => INUserSettings.Instance?.StarSeaSettings?.QuickPartSwitch ?? true;

	private const string QuickBtnPrefix = "QuickVariant_";

	// 直接由按钮名字解析变体下标，避免任何列表错位导致“点到的皮肤不对”
	private static bool TryQuickIdx(GameObject go, out int idx)
	{
		idx = -1;
		if (go == null) return false;
		string n = go.name;
		if (string.IsNullOrEmpty(n) || !n.StartsWith(QuickBtnPrefix)) return false;
		return int.TryParse(n.Substring(QuickBtnPrefix.Length), out idx);
	}

	private bool IsQuickButton(Widget widget)
	{
		int dummy;
		return widget != null && TryQuickIdx(widget.gameObject, out dummy);
	}

	// 常驻面板：开启=双击显示/再次双击关闭，切换部件/皮肤不关闭（放置照常）；关闭=原逻辑
	private static bool QuickPersistentOn => INUserSettings.Instance?.StarSeaSettings?.QuickPartSwitchPersistent ?? true;

	public int UsedRows => m_scrollList.UsedRows;

	public void SetMaxRows(int rows)
	{
		m_scrollList.SetMaxRows(rows);
	}

	public void SetConstructionUI(ConstructionUI constructionUI)
	{
		m_constructionUI = constructionUI;
	}

	public GameObject FindPartButton(ConstructionUI.PartDesc partDesc)
	{
		return m_scrollList.FindButton(partDesc);
	}

	public void SetSelection(ConstructionUI.PartDesc targetObject)
	{
		if (WPFMonoBehaviour.levelManager.gameState != LevelManager.GameState.AutoBuilding)
		{
			RevealCatalogPart(targetObject);
			m_scrollList.SetSelection(targetObject);
		}
	}

	public void Select(Widget widget, object targetObject)
	{
		// 变体按钮：开始滑动（松开时若几乎没动则视为单击切换）
		if (IsQuickButton(widget))
		{
			BeginQuickSlide();
			return;
		}

		ConstructionUI.PartDesc desc = targetObject as ConstructionUI.PartDesc;
		ConstructionUI.PartDesc prev = m_quickTarget;
		if (desc != null && QuickSwitchOn)
		{
			// 记录按下：双击时弹出/收起同类型皮肤变体
			m_quickPressOnPart = true;
			m_quickPressTime = Time.unscaledTime;
			// 只有“按下的正是面板当前显示的那个部件”时，再次双击才算关闭
			m_quickToggleClose = (prev == desc && m_quickRoot != null);
			m_quickTarget = desc;
		}
		else
		{
			m_quickPressOnPart = false;
		}
		if (!QuickPersistentOn)
		{
			CloseQuickVariants();
		}
		else if (m_quickRoot != null && desc != null && desc != prev)
		{
			OpenQuickVariants(desc);   // 常驻模式：面板跟随切换到的部件，显示其皮肤
		}

		if ((bool)m_constructionUI && WPFMonoBehaviour.levelManager.gameState != LevelManager.GameState.AutoBuilding && WPFMonoBehaviour.levelManager.gameState != LevelManager.GameState.SuperAutoBuilding)
		{
			m_constructionUI.SelectPart(desc);
		}
	}

	public void StartDrag(Widget widget, object targetObject)
	{
		if (IsQuickButton(widget))
		{
			return;   // 变体按钮不参与放置
		}
		if (m_quickPlacementBlocked)
		{
			return;   // 滑动已触发：松开手指前禁用放置
		}
		if (!QuickPersistentOn)
		{
			CloseQuickVariants();   // 开始拖出放置时收起变体面板，避免冲突
		}
		if ((bool)m_constructionUI && WPFMonoBehaviour.levelManager.gameState != LevelManager.GameState.AutoBuilding && WPFMonoBehaviour.levelManager.gameState != LevelManager.GameState.SuperAutoBuilding)
		{
			m_constructionUI.StartDrag((ConstructionUI.PartDesc)targetObject);
		}
	}

	public void CancelDrag(Widget widget, object targetObject)
	{
		if (IsQuickButton(widget))
		{
			return;
		}
		if ((bool)m_constructionUI)
		{
			m_constructionUI.CancelDrag((ConstructionUI.PartDesc)targetObject);
		}
	}

	public void Drop(Widget widget, Vector3 dropPosition, object targetObject)
	{
		if (IsQuickButton(widget))
		{
			int idx;
			TryQuickIdx(widget.gameObject, out idx);
			EndQuickSlide(idx);
			DraggableButton qdb = widget as DraggableButton;
			if (qdb != null) qdb.Deselect();   // 恢复按钮外观
			return;
		}
		if (m_quickPlacementBlocked)
		{
			return;   // 滑动中：禁用放置
		}
		// 单击快捷栏当前部件 → 弹出同类型皮肤变体（逻辑集中在 TryQuickOpenOnRelease）
		TryQuickOpenOnRelease(dropPosition, targetObject);
	}

	public void SetParts(List<ConstructionUI.PartDesc> partDescs)
	{
		m_partDescs = new List<ConstructionUI.PartDesc>(partDescs);
		foreach (ConstructionUI.PartDesc partDesc in m_partDescs)
		{
			int identityIndex = partDesc.part is RCR2Port.RoverPortPart ? partDesc.part.customPartIndex : 0;
			partDesc.customPartIndex = partDesc.part.customPartIndex;
			partDesc.sortKey = m_partOrder[partDesc.part.m_partType] * 100000 + identityIndex;
		}
		m_partDescs.Sort(new PartDescOrder(WPFMonoBehaviour.gameData));
		EnsureCatalogTabs();
		CreatePartList(handleDragIcons: false);
	}

	public void ResetSelection()
	{
		m_scrollList.ResetSelection();
	}

	private void Awake()
	{
		m_scrollList = base.transform.Find("ScrollList").GetComponent<ExtendedScrollList>();
		m_scrollList.SetListener(this);
		if (Singleton<BuildCustomizationLoader>.Instance.IsHDVersion)
		{
			m_scrollList.SetMaxRows(2);
			m_scrollList.SetButtonScale(INSettings.GetFloat(INFeature.PartSelectorScale));
		}
		else
		{
			m_scrollList.SetButtonScale(1.5f);
			base.transform.Translate(new Vector3(0f, 0.5f, 0f));
		}
		ReadPartOrder();
		if (!WPFMonoBehaviour.levelManager)
		{
			CreateTestPartList();
		}
		EventManager.Connect<CustomizePartUI.PartCustomizationEvent>(OnPartCustomized);
	}

	private void OnDestroy()
	{
		EventManager.Disconnect<CustomizePartUI.PartCustomizationEvent>(OnPartCustomized);
		CloseQuickVariants();
	}

	private void OnPartCustomized(CustomizePartUI.PartCustomizationEvent data)
	{
		if (m_partDescs == null)
		{
			return;
		}
		foreach (ConstructionUI.PartDesc partDesc in m_partDescs)
		{
			if (!ConstructionUI.MatchesPartIdentity(partDesc.part, data.customizedPart, data.customPartIndex))
			{
				continue;
			}
			BasePart selectedPart = WPFMonoBehaviour.gameData.GetCustomPart(partDesc.part.m_partType, data.customPartIndex);
			if (selectedPart == null)
			{
				continue;
			}
			partDesc.part = selectedPart;
			partDesc.customPartIndex = data.customPartIndex;
			GameObject gameObject = partDesc.part.m_constructionIconSprite == null ? null : partDesc.part.m_constructionIconSprite.gameObject;
			if (partDesc.currentPartIcon != null && gameObject != null)
			{
				GameObject gameObject2 = UnityEngine.Object.Instantiate(gameObject);
				gameObject2.transform.parent = partDesc.currentPartIcon.parent;
				gameObject2.transform.localScale = new Vector3(1.75f, 1.75f, 1f);
				gameObject2.transform.localPosition = new Vector3(0f, 0f, -0.5f);
				ConstructionUI.SetSortingOrder(gameObject2, 1, string.Empty);
				UnityEngine.Object.Destroy(partDesc.currentPartIcon.gameObject);
				partDesc.currentPartIcon = gameObject2.transform;
				if (partDesc.currentPartIcon.parent != null && partDesc.currentPartIcon.parent.GetComponent<DraggableButton>() != null)
				{
					partDesc.currentPartIcon.parent.GetComponent<DraggableButton>().Icon = gameObject2;
				}
			}
		}
	}

	private void LateUpdate()
	{
		UpdateCatalogTabs();
		Vector3 position = m_toolBox.position;
		if (m_scrollList.leftButton.transform.position.y > position.y)
		{
			position.y = m_scrollList.leftButton.transform.position.y;
			m_toolBox.position = position;
		}
		position = m_partList.position;
		if (m_scrollList.rightButton.transform.position.y > position.y)
		{
			position.y = m_scrollList.rightButton.transform.position.y;
			m_partList.position = position;
		}

		// 变体面板滑动 + 惯性 + 平滑对齐 + 切换皮肤后的平滑居中 + 当前标记放大/缩小平滑 + 切换部件的进入动画
		UpdateQuickSlide();
		UpdateQuickMomentum();
		UpdateQuickSnap();
		UpdateQuickAnimate();
		UpdateQuickSlotScale();
		UpdateQuickIntro();

		// 松开手指 → 解除“禁用放置”
		if (!Input.GetMouseButton(0))
		{
			m_quickPlacementBlocked = false;
		}

		// 仅在“正在滑动”时清除选中/取消拖出（防止滑动误放置）；
		// 面板只是打开时不能拦截，否则会破坏正常拖出放置。
		if (m_quickPlacementBlocked)
		{
			if ((bool)m_constructionUI)
			{
				m_constructionUI.ClearSelectedPart();
				if (m_constructionUI.IsDragging())
				{
					m_constructionUI.CancelDrag((ConstructionUI.PartDesc)null);
				}
			}
		}
	}

	private void ReadPartOrder()
	{
		m_partOrder.Clear();
		for (int i = 0; i < (int)SortedPartType.MAX; i++)
		{
			SortedPartType sortedPartType = (SortedPartType)i;
			if (sortedPartType.IsValid())
			{
				m_partOrder.Add(sortedPartType.ToPartType(), i);
			}
		}
	}

	private void CreatePartList(bool handleDragIcons)
	{
		foreach (var descriptor in m_partDescs) descriptor.currentPartIcon = null;
		m_scrollList.Clear();
		bool cockpitGroupAdded = false;
		foreach (ConstructionUI.PartDesc partDesc in m_partDescs)
		{
			BasePart part = partDesc.part;
			if (!IsInCurrentCatalog(part)) continue;
			if (part is RCR2Port.RoverPortCockpitPart && !m_showCockpits && !IsRc3Catalog())
			{
				if (!cockpitGroupAdded) AddCockpitGroup(part);
				cockpitGroupAdded = true;
				continue;
			}
			GameObject gameObject = part.m_constructionIconSprite.gameObject;
			bool flag = true;
			int num = 0;
			int num2 = 0;
			int num3 = GameProgress.GetAllStars() + GameProgress.GetRaceLevelUnlockedStars();
			bool flag2 = false;
			if ((bool)WPFMonoBehaviour.levelManager && WPFMonoBehaviour.levelManager.m_raceLevel && WPFMonoBehaviour.levelManager.CurrentGameMode is BaseGameMode)
			{
				string currentLevelIdentifier = Singleton<GameManager>.Instance.CurrentLevelIdentifier;
				RaceLevels.LevelUnlockablePartsData levelUnlockableData = WPFMonoBehaviour.gameData.m_raceLevels.GetLevelUnlockableData(currentLevelIdentifier);
				if (levelUnlockableData != null)
				{
					foreach (RaceLevels.UnlockableTier tier in levelUnlockableData.m_tiers)
					{
						if (tier.m_part != part.m_partType)
						{
							continue;
						}
						if (num3 < tier.m_starLimit)
						{
							flag = false;
							num = Mathf.Max(tier.m_starLimit - num3, 0);
							num2 = tier.m_starLimit;
							if (m_unlockPartTierDialog == null)
							{
								m_unlockPartTierDialog = UnityEngine.Object.Instantiate(WPFMonoBehaviour.gameData.m_unlockPartTierDialog).GetComponent<UnlockRoadHogsParts>();
								m_unlockPartTierDialog.transform.position = WPFMonoBehaviour.hudCamera.transform.position + new Vector3(0f, 0f, 10f);
								m_unlockPartTierDialog.Close();
								m_unlockPartTierDialog.onOpen += delegate
								{
									m_constructionUI.DisableFunctionality = true;
								};
								m_unlockPartTierDialog.onClose += delegate
								{
									m_constructionUI.DisableFunctionality = false;
								};
							}
						}
						else if (!GameProgress.GetRaceLevelPartUnlocked(currentLevelIdentifier, part.m_partType.ToString()))
						{
							GameProgress.SetRaceLevelPartUnlocked(currentLevelIdentifier, part.m_partType.ToString(), unlocked: true);
							flag2 = true;
						}
						break;
					}
				}
			}
			GameObject gameObject2 = UnityEngine.Object.Instantiate((!flag) ? m_partUnavailableButtonPrefab : m_partButtonPrefab);
			gameObject2.transform.parent = m_scrollList.transform;
			ConstructionUI.SetSortingOrder(gameObject2, 1, string.Empty);
			if (flag)
			{
				gameObject2.GetComponent<DraggableButton>().DragObject = partDesc;
				if (handleDragIcons)
				{
					gameObject2.GetComponent<DraggableButton>().DragIconPrefab = gameObject;
				}
				gameObject2.GetComponent<DraggableButton>().DragIconScale = 1.75f;
				Transform obj = gameObject2.transform.Find("PartCount");
				obj.GetComponent<TextMesh>().text = partDesc.CurrentCount.ToString();
				PartCounter counter = obj.GetComponent<PartCounter>();
				counter.m_partType = partDesc.part.m_partType;
				counter.m_customPartIndex = partDesc.part.customPartIndex;
				obj.GetComponent<Renderer>().sortingOrder = 1;
			}
			else
			{
				Transform obj2 = gameObject2.transform.Find("Starlimit");
				obj2.GetComponent<TextMesh>().text = num.ToString();
				obj2.GetComponent<Renderer>().sortingOrder = 1;
				int productPrice = Singleton<VirtualCatalogManager>.Instance.GetProductPrice("road_hogs_star_unlock");
				if (productPrice > 0 && !Singleton<BuildCustomizationLoader>.Instance.IsOdyssey)
				{
					int totalCost = (num2 - num3) * productPrice;
					AddUnlockStarTierPopup(gameObject2.GetComponent<UnavailablePartButton>(), m_unlockPartTierDialog, num2, num3, totalCost, () => GameProgress.SnoutCoinCount() >= totalCost);
				}
			}
			GameObject gameObject3 = UnityEngine.Object.Instantiate(gameObject);
			gameObject3.transform.parent = gameObject2.transform;
			gameObject3.transform.localScale = new Vector3(1.75f, 1.75f, 1f);
			gameObject3.transform.localPosition = new Vector3(0f, 0f, -0.5f);
			ConstructionUI.SetSortingOrder(gameObject3, 1, string.Empty);
			partDesc.currentPartIcon = gameObject3.transform;
			if (flag)
			{
				gameObject2.GetComponent<DraggableButton>().Icon = gameObject3;
				if (flag2)
				{
					GameObject obj3 = UnityEngine.Object.Instantiate(m_partUnlockAnimatedLock);
					obj3.transform.parent = gameObject2.transform;
					obj3.transform.localPosition = new Vector3(0f, 0.6f, -1f);
				}
			}
			m_scrollList.AddButton(gameObject2.GetComponent<Widget>());
		}
		foreach (ConstructionUI.PartDesc partDesc2 in m_partDescs)
		{
			if (partDesc2 != null && !(partDesc2.part == null))
			{
				int lastUsedPartIndex = CustomizationManager.GetLastUsedPartIndex(partDesc2.part.m_partType);
				if (lastUsedPartIndex > 0)
				{
					OnPartCustomized(new CustomizePartUI.PartCustomizationEvent(partDesc2.part.m_partType, lastUsedPartIndex));
				}
			}
		}
	}

	private void CreateTestPartList()
	{
		m_partDescs = new List<ConstructionUI.PartDesc>();
		foreach (GameObject part in WPFMonoBehaviour.gameData.Parts)
		{
			ConstructionUI.PartDesc partDesc = new ConstructionUI.PartDesc();
			partDesc.part = part.GetComponent<BasePart>();
			partDesc.sortKey = m_partOrder[part.GetComponent<BasePart>().m_partType];
			partDesc.maxCount = UnityEngine.Random.Range(0, 20);
			m_partDescs.Add(partDesc);
		}
		m_partDescs.Sort(new PartDescOrder(WPFMonoBehaviour.gameData));
		CreatePartList(handleDragIcons: true);
	}

	// ===== 快捷切换部件：固定位置的皮肤变体面板（按品质排序 + 分页）=====

	private const float QuickBtnScale = 0.46f;    // 按钮整体缩放
	private const float QuickStepMin = 0.55f;     // 间距下限（与之前“合适”的版本一致）
	private const float QuickStepMax = 1.15f;     // 间距上限（与之前“合适”的版本一致）
	private const float QuickBgAlpha = 0.5f;    // 按钮背景透明度（更透明）
	private static readonly Color QuickCurrentBg = new Color(0.20f, 0.85f, 0.35f, 0.95f);   // 当前皮肤：醒目绿色底

	// 部件列表(PartListing)的引用：用于复用相同的“底框 + 部件皮肤”渲染
	private PartListing QuickListing()
	{
		GameObject go = (WPFMonoBehaviour.gameData != null) ? WPFMonoBehaviour.gameData.m_partListing : null;
		return (go != null) ? go.GetComponent<PartListing>() : null;
	}

	private static int QuickTierOf(BasePart.PartType type, int idx)
	{
		BasePart bp = WPFMonoBehaviour.gameData.GetCustomPart(type, idx);
		return (bp != null) ? (int)bp.m_partTier : 99;
	}

	// 排序分组：铁框(MetalFrame) 先常规皮肤(0~11)，再染色框(12~129)
	private static int QuickGroupOf(BasePart.PartType type, int idx)
	{
		return (type == BasePart.PartType.MetalFrame && idx >= 12) ? 1 : 0;
	}

	private void OpenQuickVariants(ConstructionUI.PartDesc desc)
	{
		if (desc == null || desc.part == null)
		{
			CloseQuickVariants();
			return;
		}
		BasePart.PartType type = desc.part.m_partType;

		// 收集所有皮肤的【真实下标】：基础皮肤=0；其它用部件自身的 Index。
		// 插件/扩展新增的部件下标不一定等于列表位置（工厂按 part.Index 匹配），
		// 因此这里用 part.Index 而不是位置，缺失时回退为位置。
		m_quickOrder.Clear();
		m_quickOrder.Add(0);
		CustomPartInfo info = WPFMonoBehaviour.gameData.GetCustomPart(type);
		if (info != null && info.PartList != null)
		{
			for (int j = 0; j < info.PartList.Count; j++)
			{
				BasePart p = info.PartList[j];
				int id = (p != null && p.Index > 0) ? p.Index : (j + 1);
				if (!m_quickOrder.Contains(id))
				{
					m_quickOrder.Add(id);
				}
			}
		}
		int count = m_quickOrder.Count;
		if (count <= 1)
		{
			CloseQuickVariants();   // 没有其它皮肤，不弹
			return;
		}

		m_quickTarget = desc;
		m_quickCount = count;
		// 先按分组(铁框：常规皮肤→染色框)，再按品质 PartTier Regular<Common<Rare<Epic<Legendary
		m_quickOrder.Sort(delegate (int a, int b)
		{
			int ga = QuickGroupOf(type, a);
			int gb = QuickGroupOf(type, b);
			if (ga != gb) return ga.CompareTo(gb);
			int ta = QuickTierOf(type, a);
			int tb = QuickTierOf(type, b);
			if (ta != tb) return ta.CompareTo(tb);
			return a.CompareTo(b);
		});
		m_quickCurrent = CustomizationManager.GetLastUsedPartIndex(type);
		// 记录锚定按钮的 z（与部件栏按钮同一层，保证能被 HUD 相机渲染与点击）
		GameObject anchorBtn = FindPartButton(desc);
		m_quickAnchorZ = (anchorBtn != null) ? anchorBtn.transform.position.z
			: ((m_toolBox != null) ? m_toolBox.position.z : 0f);

		// 面板已经打开时切换【部件种类】→ 旧一排上滑出场、新一排从下方滑入（内容不瞬变）
		// 已停用：QuickIntroEnabled=false 时直接瞬时切换
		bool playIntro = QuickIntroEnabled && (m_quickRoot != null && m_quickBtns.Count > 0);

		m_quickOffset = 0f;
		m_quickScaleFactor.Clear();   // 重新呼出：缩放标记直接就位，不做过渡
		BuildQuickRow(false, playIntro);
	}

	private void BuildQuickRow(bool animateOffset = false, bool playIntro = false)
	{
		// 只清理 root 的子节点，保留 root（固定位置复用）
		// 切换部件种类(playIntro)：旧的一排不立即销毁，而是搬到“出场组”整体向上滑出
		RemoveQuickOutgoing();
		m_quickInFade = null;
		m_quickIntroActive = false;   // 重建即结束上一次过渡（playIntro 时会在末尾重新开始）
		if (playIntro && m_quickRoot != null && m_quickRoot.childCount > 0)
		{
			GameObject outGo = new GameObject("QuickOutgoing");
			outGo.transform.SetParent(transform, false);
			outGo.transform.position = m_quickRoot.position;
			outGo.transform.rotation = m_quickRoot.rotation;
			outGo.transform.localScale = m_quickRoot.localScale;
			for (int c = m_quickRoot.childCount - 1; c >= 0; c--)
			{
				m_quickRoot.GetChild(c).SetParent(outGo.transform, true);   // 保留世界位置
			}
			// 出场期间不可点击（否则可能误触旧按钮）
			Collider[] cols = outGo.GetComponentsInChildren<Collider>(true);
			for (int i = 0; i < cols.Length; i++) cols[i].enabled = false;
			m_quickOutgoing = outGo.transform;
			m_quickOutgoingStartY = outGo.transform.position.y;
			m_quickOutgoingStartScale = outGo.transform.localScale.x;
			m_quickOutFade = CaptureQuickFade(outGo.transform);   // 记录旧一排的基准透明度（淡出用）
		}
		else if (m_quickRoot != null)
		{
			for (int c = m_quickRoot.childCount - 1; c >= 0; c--)
			{
				UnityEngine.Object.Destroy(m_quickRoot.GetChild(c).gameObject);
			}
		}
		m_quickBtns.Clear();
		m_quickSlotTfs.Clear();
		m_quickSlotKeys.Clear();
		m_quickSlotFactors.Clear();
		m_quickSlotTargets.Clear();
		if (m_quickBgObj != null)
		{
			UnityEngine.Object.Destroy(m_quickBgObj);
			m_quickBgObj = null;
		}
		m_quickSnapping = false;

		if (m_quickTarget == null || m_quickTarget.part == null || m_quickCount <= 1)
		{
			CloseQuickVariants();
			return;
		}

		BasePart.PartType type = m_quickTarget.part.m_partType;
		int count = m_quickCount;
		PartListing listing = QuickListing();

		// 一次最多显示个数（星海设置可调，默认 16）
		int want = 16;
		StarSeaSettings ss = INUserSettings.Instance?.StarSeaSettings;
		if (ss != null) want = ss.QuickPartSwitchMax;
		m_quickVisible = Mathf.Clamp(want, 1, 60);
		if (m_quickVisible > count) m_quickVisible = count;

		// 固定锚点：屏幕下方（正常部件列表上方一点）
		QuickAnchor();

		// 间距：按可用宽度自适应并钳制在 [QuickStepMin, QuickStepMax]（与之前“合适”的版本一致）；
		// 面板始终居中，左右限位只作用于“滑动范围”（不改变布局）。
		float usable = Mathf.Max(1f, m_quickBarWidth * 0.6f);
		m_quickStep = (m_quickVisible > 1)
			? Mathf.Clamp(usable / (m_quickVisible - 1), QuickStepMin, QuickStepMax)
			: QuickStepMax;
		m_quickSlotScale = QuickBtnScale;   // 0.46
		m_quickCentered = true;

		float pxPerWorld = (m_quickBarWidth > 0.01f) ? (Screen.width / m_quickBarWidth) : 100f;
		m_quickPxPerItem = Mathf.Max(1f, m_quickStep * pxPerWorld);

		// 滑动限位 = 部件栏范围：最左皮肤最多滑到部件栏最左端、最右皮肤最多滑到部件栏最右端。
		// 由 ApplyQuickRootPos 的公式反解：rootX = centerX - (offset + slots) * step，item i 在 rootX + i*step。
		float halfBar = m_quickBarWidth * 0.5f;
		float slotsNow = (m_quickVisible - 1) * 0.5f;
		float offLeftSkinAtBarLeft = halfBar / m_quickStep - slotsNow;       // item0 落在部件栏最左端
		float offRightSkinAtBarRight = (count - 1) - slotsNow - halfBar / m_quickStep;   // 末件落在部件栏最右端
		m_quickMinOffset = Mathf.Min(offLeftSkinAtBarLeft, offRightSkinAtBarRight);
		m_quickMaxOffset = Mathf.Max(offLeftSkinAtBarLeft, offRightSkinAtBarRight);
		// 呼出时把“正在使用的皮肤”放到窗口正中
		float curPos = 0f;
		for (int i = 0; i < m_quickOrder.Count; i++)
		{
			if (m_quickOrder[i] == m_quickCurrent) { curPos = i; break; }
		}
		float targetOffset = Mathf.Clamp(curPos - (m_quickVisible - 1) * 0.5f, m_quickMinOffset, m_quickMaxOffset);
		m_quickOffset = Mathf.Clamp(m_quickOffset, m_quickMinOffset, m_quickMaxOffset);
		m_quickSliding = false;
		if (animateOffset)
		{
			// 切换皮肤：平滑移动到“当前皮肤居中”，不瞬移
			ScrollQuickOffsetTo(targetOffset, true);
		}
		else
		{
			m_quickAnimating = false;
			m_quickOffset = targetOffset;
		}

		if (m_quickRoot == null)
		{
			GameObject rootGo = new GameObject("INQuickVariants");
			rootGo.transform.SetParent(transform, false);
			m_quickRoot = rootGo.transform;
		}
		m_quickRoot.localScale = Vector3.one;

		for (int i = 0; i < count; i++)
		{
			CreateQuickVariantButton(type, m_quickOrder[i], listing, i * m_quickStep);
		}
		CreateQuickBackgroundStrip(count);
		ApplyQuickRootPos();
		// 面板落地后立刻让选项卡让位，不要等下一帧的 LateUpdate（否则会闪一帧重合）
		UpdateCatalogTabs();

		if (playIntro)
		{
			m_quickInFade = CaptureQuickFade(m_quickRoot);   // 记录新一排基准透明度
			BeginQuickIntro();
			ApplyQuickFade(m_quickInFade, 0f);               // 立即透明，避免首帧闪一下
		}
	}

	// “背景”控件：只覆盖【可见窗口】范围（含按钮之间的空隙），使空隙处按下也能滑动、且不拖动视野。
	// 注意：不能覆盖整条（皮肤很多时碰撞体会巨大并挡住整个 UI），且它固定在窗口处、不随滑动移动。
	private void CreateQuickBackgroundStrip(int count)
	{
		if (count <= 0)
		{
			return;
		}
		float width = Mathf.Max(0.5f, m_quickVisible * m_quickStep);
		float baseX0 = m_quickCentered ? m_quickCenterX : m_quickRightX;
		float centerX = baseX0 - (m_quickVisible - 1) * m_quickStep * 0.5f;

		GameObject bg = new GameObject(QuickBtnPrefix + "-1");   // 名字带前缀 → IsQuickButton 为真，idx=-1
		bg.transform.SetParent(transform, false);
		bg.transform.position = new Vector3(centerX, m_quickY, m_quickZ + 0.5f);   // 稍靠后：按钮优先被点中
		BoxCollider bc = bg.AddComponent<BoxCollider>();
		bc.size = new Vector3(width, 0.62f, 0.05f);
		if (m_quickBtns.Count > 0)
		{
			bg.layer = m_quickBtns[0].layer;   // 与按钮同层（GuiManager 只对 HUD 层做拾取）
		}
		m_quickBgObj = bg;
		Button btn = bg.AddComponent<Button>();
		btn.SetInputDelegate(delegate (InputEvent inp)
		{
			if (inp.type == InputEvent.EventType.Press) BeginQuickSlide();
			else if (inp.type == InputEvent.EventType.Release) EndQuickSlide(-1);
		});
	}

	// ===== 滑动选择 =====

	private void BeginQuickSlide()
	{
		m_quickSliding = true;
		m_quickSlideMoved = false;
		m_quickPlacementBlocked = true;   // 滑动触发即禁用放置，直到松开
		m_quickMomentum = false;
		m_quickAnimating = false;         // 手指按下 → 停止“切换皮肤后的平滑居中”
		CancelQuickIntro();               // 手指按下 → 立刻结束进入动画
		m_quickSlideVel = 0f;
		m_quickSlideStartPx = Input.mousePosition.x;
		m_quickSlideStartOffset = m_quickOffset;
		m_quickLastOffset = m_quickOffset;
	}

	private void UpdateQuickSlide()
	{
		if (!m_quickSliding || !Input.GetMouseButton(0))
		{
			return;
		}
		float dx = Input.mousePosition.x - m_quickSlideStartPx;
		if (Mathf.Abs(dx) > 8f)
		{
			m_quickSlideMoved = true;
		}
		// 滑动期间强制取消任何挂起的放置，避免“滑动=放置”
		// 注意：必须用接收 PartDesc 的重载（无参重载在“从载具拖出”时会把部件放下！）
		if ((bool)m_constructionUI && m_constructionUI.IsDragging())
		{
			m_constructionUI.CancelDrag((ConstructionUI.PartDesc)null);
		}
		m_quickOffset = Mathf.Clamp(m_quickSlideStartOffset - dx / Mathf.Max(1f, m_quickPxPerItem), m_quickMinOffset, m_quickMaxOffset);

		// 记录速度（项/秒），松开后用于惯性
		float dt = Time.deltaTime;
		if (dt > 0.0001f)
		{
			m_quickSlideVel = (m_quickOffset - m_quickLastOffset) / dt;
		}
		m_quickLastOffset = m_quickOffset;

		ApplyQuickRootPos();
	}

	private void EndQuickSlide(int variantIdx)
	{
		if (!m_quickSliding)
		{
			return;
		}
		m_quickSliding = false;
		if (!m_quickSlideMoved)
		{
			if (variantIdx >= 0)
			{
				ApplyQuickVariant(variantIdx);   // 没怎么动 = 单击切换（间隙背景为 -1，忽略）
			}
			return;
		}
		// 惯性：按松开时的速度继续滑动并逐渐衰减
		m_quickMomentum = Mathf.Abs(m_quickSlideVel) > 0.25f;
		if (!m_quickMomentum)
		{
			m_quickSlideVel = 0f;
			BeginQuickSnap();   // 平滑对齐，避免瞬移
		}
	}

	// 平滑对齐到最近的整数位置（避免“突然瞬移”）
	private void BeginQuickSnap()
	{
		m_quickAnimating = false;
		m_quickSnapTarget = Mathf.Round(Mathf.Clamp(m_quickOffset, m_quickMinOffset, m_quickMaxOffset));
		m_quickSnapping = true;
	}

	// 让滑动偏移平滑移动到 target（animate=false 则立即设置）。用于切换皮肤后回到居中位置。
	private void ScrollQuickOffsetTo(float target, bool animate)
	{
		target = Mathf.Clamp(target, m_quickMinOffset, m_quickMaxOffset);
		if (!animate)
		{
			m_quickAnimating = false;
			m_quickOffset = target;
			ApplyQuickRootPos();
			return;
		}
		m_quickSnapping = false;
		m_quickMomentum = false;
		m_quickSlideVel = 0f;
		m_quickAnimTarget = target;
		m_quickAnimating = true;
	}

	// 切换【部件种类】的过渡：新一排从下方滑入 + 轻微放大；旧一排(出场组)向上滑出后销毁
	private void BeginQuickIntro()
	{
		m_quickAnimating = false;
		m_quickSnapping = false;
		m_quickMomentum = false;
		m_quickSlideVel = 0f;
		m_quickIntroActive = true;
		m_quickIntroTimer = 0f;
	}

	// 销毁出场组（旧一排）
	private void RemoveQuickOutgoing()
	{
		if (m_quickOutgoing != null)
		{
			UnityEngine.Object.Destroy(m_quickOutgoing.gameObject);
			m_quickOutgoing = null;
		}
		m_quickOutFade = null;
	}

	private void CancelQuickIntro()
	{
		if (!m_quickIntroActive && m_quickOutgoing == null)
		{
			return;
		}
		m_quickIntroActive = false;
		RemoveQuickOutgoing();
		ApplyQuickFade(m_quickInFade, 1f);   // 恢复不透明
		if (m_quickRoot != null)
		{
			m_quickRoot.localScale = Vector3.one;
			ApplyQuickRootPos();
		}
	}

	private void UpdateQuickIntro()
	{
		bool hasOut = (m_quickOutgoing != null);
		if (!m_quickIntroActive && !hasOut)
		{
			return;
		}
		if (m_quickRoot == null)
		{
			m_quickIntroActive = false;
			RemoveQuickOutgoing();
			return;
		}
		m_quickIntroTimer += Time.unscaledDeltaTime;
		float t = Mathf.Clamp01(m_quickIntroTimer / QuickIntroDuration);
		float ease = 1f - Mathf.Pow(1f - t, 3f);   // easeOutCubic：快进慢收
		float slideY = Mathf.Max(0.5f, m_quickWorldHeight * QuickIntroSlideFrac);

		// 新一排：从下方滑入 + 由透明淡入
		ApplyQuickRootPos();
		if (m_quickIntroActive)
		{
			Vector3 p = m_quickRoot.position;
			p.y -= slideY * (1f - ease);   // 起始在下方，逐渐升到目标位置
			m_quickRoot.position = p;
			m_quickRoot.localScale = Vector3.one * Mathf.Lerp(QuickIntroScaleFrom, 1f, ease);
			ApplyQuickFade(m_quickInFade, ease);
		}

		// 旧一排：向上滑出 + 收缩 + 淡出
		if (hasOut)
		{
			Vector3 op = m_quickOutgoing.position;
			op.y = m_quickOutgoingStartY + slideY * ease;
			m_quickOutgoing.position = op;
			m_quickOutgoing.localScale = Vector3.one
				* Mathf.Lerp(m_quickOutgoingStartScale, m_quickOutgoingStartScale * QuickIntroScaleFrom, ease);
			ApplyQuickFade(m_quickOutFade, 1f - ease);
		}

		if (t >= 1f)
		{
			m_quickIntroActive = false;
			RemoveQuickOutgoing();
			m_quickRoot.localScale = Vector3.one;
			ApplyQuickFade(m_quickInFade, 1f);
			ApplyQuickRootPos();
		}
	}

	// 记录一组渲染器的当前透明度作为“基准”（跳过 TextMesh：其材质色会与文字顶点色相乘）
	private static QuickFadeSet CaptureQuickFade(Transform root)
	{
		QuickFadeSet set = new QuickFadeSet();
		if (root == null)
		{
			return set;
		}
		Renderer[] rs = root.GetComponentsInChildren<Renderer>(true);
		for (int i = 0; i < rs.Length; i++)
		{
			Renderer r = rs[i];
			if (r == null || r.GetComponent<TextMesh>() != null)
			{
				continue;
			}
			Material m = r.material;
			if (m == null || !m.HasProperty("_Color"))
			{
				continue;
			}
			set.renderers.Add(r);
			set.baseAlpha.Add(m.color.a);
		}
		return set;
	}

	// 按比例应用透明度：alpha = 基准 alpha × factor（保留原有颜色，只改 a）
	private static void ApplyQuickFade(QuickFadeSet set, float factor)
	{
		if (set == null)
		{
			return;
		}
		factor = Mathf.Clamp01(factor);
		for (int i = 0; i < set.renderers.Count; i++)
		{
			Renderer r = set.renderers[i];
			if (r == null)
			{
				continue;
			}
			Material m = r.material;
			if (m == null || !m.HasProperty("_Color"))
			{
				continue;
			}
			Color c = m.color;
			c.a = Mathf.Clamp01(set.baseAlpha[i] * factor);
			m.color = c;
		}
	}

	// 当前皮肤“放大”标记的平滑过渡（切换皮肤时旧皮肤平滑缩小、新皮肤平滑放大）
	private void UpdateQuickSlotScale()
	{
		if (m_quickSlotTfs.Count == 0)
		{
			return;
		}
		float dt = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
		float k = 1f - Mathf.Exp(-QuickZoomScaleSmooth * dt);
		for (int i = 0; i < m_quickSlotTfs.Count; i++)
		{
			Transform t = m_quickSlotTfs[i];
			if (t == null)
			{
				continue;
			}
			float target = m_quickSlotTargets[i];
			float cur = m_quickSlotFactors[i];
			if (Mathf.Abs(cur - target) < 0.002f)
			{
				cur = target;
			}
			else
			{
				cur = Mathf.Lerp(cur, target, k);
			}
			m_quickSlotFactors[i] = cur;
			m_quickScaleFactor[m_quickSlotKeys[i]] = cur;   // 跨重建保留，避免重建时跳变
			t.localScale = Vector3.one * (m_quickSlotScale * cur);
		}
	}

	// 切换皮肤后的平滑移动（指数趋近，不瞬移、不过冲）
	private void UpdateQuickAnimate()
	{
		if (!m_quickAnimating)
		{
			return;
		}
		float dt = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
		float k = 1f - Mathf.Exp(-QuickZoomMoveSmooth * dt);
		m_quickOffset = Mathf.Lerp(m_quickOffset, m_quickAnimTarget, k);
		if (Mathf.Abs(m_quickOffset - m_quickAnimTarget) < 0.002f)
		{
			m_quickOffset = m_quickAnimTarget;
			m_quickAnimating = false;
		}
		ApplyQuickRootPos();
	}

	private void UpdateQuickSnap()
	{
		if (!m_quickSnapping)
		{
			return;
		}
		m_quickOffset = Mathf.MoveTowards(m_quickOffset, m_quickSnapTarget, 6f * Time.unscaledDeltaTime);
		ApplyQuickRootPos();
		if (Mathf.Abs(m_quickOffset - m_quickSnapTarget) < 0.001f)
		{
			m_quickOffset = m_quickSnapTarget;
			m_quickSnapping = false;
			ApplyQuickRootPos();
		}
	}

	// 惯性滑动推进
	private void UpdateQuickMomentum()
	{
		if (!m_quickMomentum)
		{
			return;
		}
		float dt = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
		float next = m_quickOffset + m_quickSlideVel * dt;
		float clamped = Mathf.Clamp(next, m_quickMinOffset, m_quickMaxOffset);
		if (clamped != next)
		{
			m_quickSlideVel = 0f;   // 到达两端：停止
		}
		m_quickOffset = clamped;
		m_quickSlideVel *= Mathf.Exp(-6f * dt);
		ApplyQuickRootPos();
		if (Mathf.Abs(m_quickSlideVel) < 0.08f)
		{
			m_quickMomentum = false;
			m_quickSlideVel = 0f;
			BeginQuickSnap();   // 平滑对齐，避免瞬移
		}
	}

	private void ApplyQuickRootPos()
	{
		if (m_quickRoot == null)
		{
			return;
		}
		// 居中模式：整排以部件栏中心居中；否则右端对齐部件栏右端
		float baseX = m_quickCentered ? m_quickCenterX : m_quickRightX;
		float slots = m_quickCentered ? ((m_quickVisible - 1) * 0.5f) : (m_quickVisible - 1);
		m_quickRoot.position = new Vector3(
			baseX - (m_quickOffset + slots) * m_quickStep,
			m_quickY, m_quickZ);
	}

	// 松开时触发：单击(短按且几乎没移动)快捷栏部件 → 取消放置（不放下部件）+ 双击呼出面板。
	// 只有真正的“拖出放置”(位移大)才交给原放置流程。
	private void TryQuickOpenOnRelease(Vector3 dropPosition, object targetObject)
	{
		if (!m_quickPressOnPart)
		{
			return;
		}
		m_quickPressOnPart = false;
		if (!QuickSwitchOn || m_quickTarget == null)
		{
			return;
		}
		if (Time.unscaledTime - m_quickPressTime > 0.5f)
		{
			return;   // 按太久，视为其它操作
		}

		ConstructionUI.PartDesc pressed = targetObject as ConstructionUI.PartDesc;
		if (pressed != null && pressed != m_quickTarget)
		{
			return;
		}
		// 松开时若不在任何控件上（说明被拖到网格上）→ 视为真实“拖出放置”，不干预。
		// 用 GuiManager 的 onWidget 判断：部件栏本身可横向滚动会带动按钮位移，
		// 之前用“松开位置↔按钮”的距离会因此误判为拖出，导致双击偶发失效。
		if (!GuiManager.GetPointer().onWidget)
		{
			return;
		}

		// 单击：取消本次拖出（不放下部件）
		if ((bool)m_constructionUI)
		{
			m_constructionUI.CancelDrag(m_quickTarget);
		}

		// 双击判定：与上一次点击为同一部件且在 0.5s 内
		bool dbl = (m_quickLastClickTarget == m_quickTarget)
			&& (Time.unscaledTime - m_quickLastClickTime <= 0.5f);
		m_quickLastClickTime = Time.unscaledTime;
		m_quickLastClickTarget = m_quickTarget;
		if (!dbl)
		{
			return;   // 第一次点击不弹，等第二次
		}

		if (m_quickToggleClose)
		{
			CloseQuickVariants();
			return;
		}
		OpenQuickVariants(m_quickTarget);
	}

	// 面板固定锚点：屏幕下方（部件列表上方一点）；返回可视窗口中心 x、y、z 与世界宽度
	private void QuickAnchor()
	{
		Camera cam = null;
		if (Singleton<GuiManager>.IsInstantiated())
		{
			cam = Singleton<GuiManager>.Instance.FindCamera();
		}
		if (cam == null) cam = WPFMonoBehaviour.hudCamera;
		if (cam == null) cam = WPFMonoBehaviour.mainCamera;

		Vector3 refPos = (m_toolBox != null) ? m_toolBox.position : Vector3.zero;
		float refZ = (m_quickAnchorZ != 0f) ? m_quickAnchorZ : refPos.z;

		float centerX = refPos.x;
		float rightX = refPos.x;
		float y = refPos.y + 1.5f;
		float barWidth = 16f;

		if (cam != null)
		{
			float depth = Mathf.Max(1f, Mathf.Abs(cam.transform.position.z - refZ));
			centerX = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, depth)).x;
			rightX = centerX;
			y = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.265f, depth)).y;
			barWidth = Mathf.Abs(cam.ViewportToWorldPoint(new Vector3(1f, 0.5f, depth)).x
				- cam.ViewportToWorldPoint(new Vector3(0f, 0.5f, depth)).x) * 0.5f;   // 回退：屏幕一半
			// 屏幕世界高度（纵向动画距离基准）
			m_quickWorldHeight = Mathf.Abs(cam.ViewportToWorldPoint(new Vector3(0.5f, 1f, depth)).y
				- cam.ViewportToWorldPoint(new Vector3(0.5f, 0f, depth)).y);
		}

		// 优先用【部件栏】定位：右端=右箭头按钮，中心/宽度由左右箭头得出
		if (m_scrollList != null)
		{
			bool hasRight = m_scrollList.rightButton != null && m_scrollList.rightButton.activeInHierarchy;
			bool hasLeft = m_scrollList.leftButton != null && m_scrollList.leftButton.activeInHierarchy;
			float r = hasRight ? m_scrollList.rightButton.transform.position.x : centerX;
			float l = hasLeft ? m_scrollList.leftButton.transform.position.x : centerX;
			if (hasRight || hasLeft)
			{
				rightX = r;
				centerX = (r + l) * 0.5f;
				float w = Mathf.Abs(r - l);
				if (w > 0.5f) barWidth = w;
			}
		}

		m_quickRightX = rightX;
		m_quickCenterX = centerX;
		m_quickBarWidth = barWidth;
		m_quickY = y;
		// 用与部件栏按钮完全相同的 z（GuiManager 会拒绝 z 高于层底部的 widget，故不能往前挪）
		m_quickZ = refZ;
	}

	// 按钮外壳：用父级承担统一的缩放，避免 Button/DraggableButton 的悬停动画覆盖缩放
	private Transform CreateQuickSlot(float localX, bool highlight, int key)
	{
		GameObject slotGo = new GameObject("QuickSlot");
		slotGo.transform.SetParent(m_quickRoot, false);
		slotGo.transform.localPosition = new Vector3(localX, 0f, 0f);
		// 大小参考部件栏；当前使用的皮肤整体略大，作为醒目标记（缩放平滑过渡，不瞬变）
		float target = highlight ? QuickCurrentZoom : 1f;
		float factor = target;
		if (m_quickScaleFactor.TryGetValue(key, out float prev)) factor = prev;
		slotGo.transform.localScale = Vector3.one * (m_quickSlotScale * factor);
		m_quickScaleFactor[key] = factor;
		m_quickSlotTfs.Add(slotGo.transform);
		m_quickSlotKeys.Add(key);
		m_quickSlotFactors.Add(factor);
		m_quickSlotTargets.Add(target);
		return slotGo.transform;
	}

	private void CreateQuickVariantButton(BasePart.PartType type, int variantIdx, PartListing listing, float localX)
	{
		if (m_partButtonPrefab == null)
		{
			return;
		}
		BasePart bp = WPFMonoBehaviour.gameData.GetCustomPart(type, variantIdx);
		if (bp == null || bp.m_constructionIconSprite == null)
		{
			return;
		}
		bool isCurrent = (variantIdx == m_quickCurrent);
		Transform slotTf = CreateQuickSlot(localX, isCurrent, variantIdx);

		// 用与部件栏相同的按钮（可交互性已确认）：底框 + 部件皮肤
		GameObject btn = UnityEngine.Object.Instantiate(m_partButtonPrefab);
		btn.name = "QuickVariant_" + variantIdx;
		btn.transform.SetParent(slotTf, false);
		btn.transform.localPosition = Vector3.zero;
		btn.transform.localScale = Vector3.one;
		ConstructionUI.SetSortingOrder(btn, 1, string.Empty);

		// 先给底框上色（此时按钮下还没有图标子物体），当前皮肤=醒目底色
		FadeQuickBackground(btn, isCurrent);

		GameObject icon = UnityEngine.Object.Instantiate(bp.m_constructionIconSprite.gameObject);
		icon.transform.SetParent(btn.transform, false);
		icon.transform.localPosition = new Vector3(0f, 0f, -0.5f);
		icon.transform.localScale = new Vector3(1.4f, 1.4f, 1f);   // 内部图标大小
		ConstructionUI.SetSortingOrder(icon, 1, string.Empty);

		string[] hideNames = { "PartCount", "Index", "Starlimit" };
		for (int h = 0; h < hideNames.Length; h++)
		{
			Transform t = btn.transform.Find(hideNames[h]);
			if (t != null) t.gameObject.SetActive(false);
		}

		// 品质标记：全部品质都显示（含零星），用颜色区分
		int tier = Mathf.Clamp((int)bp.m_partTier, 0, 4);
		{
			Transform mark = btn.transform.Find("PartCount");
			if (mark != null)
			{
				mark.gameObject.SetActive(true);
				mark.localPosition = new Vector3(-1f, -0f, -1f);   // 方格正下方（水平居中）
				TextMesh tm = mark.GetComponent<TextMesh>();
				if (tm != null)
				{
					// 品质标记：固定 1 个短横“-”，颜色区分品质（含零星）
					Color col = QuickStarColor(tier);
					tm.text = "---";
					tm.color = col;
					// 使用 Unity 引擎默认字体（内置 Arial / LegacyRuntime）
					Font f = QuickBuiltinFont();
					if (f != null)
					{
						tm.font = f;
						tm.fontSize = 100;
					}
					tm.characterSize = 0.5f;
					Renderer mr = mark.GetComponent<Renderer>();
					if (mr != null)
					{
						if (f != null) mr.material = f.material;   // 同步默认字体的材质
						// 颜色只用“顶点色(tm.color)”。若同时把材质 _Color 也设为同色，会相乘 → 明显变暗。
						Material mm = mr.material;
						if (mm != null && mm.HasProperty("_Color")) mm.color = Color.white;
					}
					ConstructionUI.SetSortingOrder(mark.gameObject, 5, string.Empty);   // 画在最前，避免被图标盖住
				}
			}
		}

		HookQuickButton(btn, variantIdx);
		m_quickBtns.Add(btn);
	}

	// Unity 引擎默认字体（内置）
	private static Font s_quickFont;
	private static Font QuickBuiltinFont()
	{
		if (s_quickFont != null)
		{
			return s_quickFont;
		}
		try { s_quickFont = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
		if (s_quickFont == null)
		{
			try { s_quickFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
		}
		return s_quickFont;
	}

	// 品质颜色（按需求给定的色号）
	// 零星 #9b9c8d / 一星 #6dc702 / 二星 #07b9b6 / 三星 #cfa615 / 四星 #921d88
	private static Color QuickStarColor(int tier)
	{
		switch (tier)
		{
		case 0: return new Color32(0x9b, 0x9c, 0x8d, 0xff);
		case 1: return new Color32(0x6d, 0xc7, 0x02, 0xff);
		case 2: return new Color32(0x07, 0xb9, 0xb6, 0xff);
		case 3: return new Color32(0xcf, 0xa6, 0x15, 0xff);
		default: return new Color32(0x92, 0x1d, 0x88, 0xff);
		}
	}

	// 按钮底框：普通更透明；当前使用的皮肤用醒目底色（对按钮本身及其子渲染器都处理）
	private static void FadeQuickBackground(GameObject btn, bool highlight)
	{
		Renderer[] rs = btn.GetComponentsInChildren<Renderer>(true);
		for (int i = 0; i < rs.Length; i++)
		{
			Renderer r = rs[i];
			if (r == null) continue;
			// 跳过文本(下划线标记)：其材质颜色会与文字颜色相乘 → 导致颜色不正常
			if (r.GetComponent<TextMesh>() != null) continue;
			Material m = r.material;
			if (m == null) continue;
			if (m.HasProperty("_Color"))
			{
				if (highlight)
				{
					m.color = QuickCurrentBg;
				}
				else
				{
					Color c = m.color;
					c.a = QuickBgAlpha;
					m.color = c;
				}
			}
		}
	}

	// 绑定：按下开始滑动，松开结束（几乎没动=单击切换）
	private void HookQuickButton(GameObject btn, int variantIdx)
	{
		Button cb = btn.GetComponentInChildren<Button>();
		DraggableButton db = btn.GetComponentInChildren<DraggableButton>();
		if (cb != null)
		{
			cb.Lock(true);   // 阻止底框自带行为，只用输入回调
			cb.SetInputDelegate(delegate (InputEvent inp)
			{
				if (inp.type == InputEvent.EventType.Press) BeginQuickSlide();
				else if (inp.type == InputEvent.EventType.Release) EndQuickSlide(variantIdx);
			});
		}
		else if (db != null)
		{
			// 没有 Button 的底框（DraggableButton）：走 WidgetListener 的 Select/Drop
			db.DragObject = null;
			db.SetListener(this);
		}
	}

	private void ApplyQuickVariant(int variantIdx)
	{
		ConstructionUI.PartDesc desc = m_quickTarget;
		if (desc != null && desc.part != null)
		{
			BasePart.PartType type = desc.part.m_partType;
			if (WPFMonoBehaviour.gameData.GetCustomPart(type, variantIdx) != null)
			{
				CustomizationManager.SetLastUsedPartIndex(type, variantIdx);
				// 复用原版回调：更新所有同类型 PartDesc 的 part 与图标
				OnPartCustomized(new CustomizePartUI.PartCustomizationEvent(type, variantIdx));
				if ((bool)m_constructionUI)
				{
					m_constructionUI.SelectPart(desc);
				}
			}
		}
		// 常驻模式：切换皮肤后不关闭面板，仅刷新（让“当前使用”标记移动）
		if (QuickPersistentOn && m_quickRoot != null)
		{
			m_quickCurrent = CustomizationManager.GetLastUsedPartIndex(desc != null && desc.part != null ? desc.part.m_partType : BasePart.PartType.Unknown);
			BuildQuickRow(true);   // 平滑移动让“当前使用”的皮肤回到中间，不瞬移
			return;
		}
		CloseQuickVariants();
	}

	// 仅销毁弹窗(保留 m_quickTarget)，供 Select/关闭时使用
	private void CloseQuickVariants()
	{
		m_quickBtns.Clear();
		m_quickSlotTfs.Clear();
		m_quickSlotKeys.Clear();
		m_quickSlotFactors.Clear();
		m_quickSlotTargets.Clear();
		m_quickIntroActive = false;
		m_quickInFade = null;
		RemoveQuickOutgoing();
		if (m_quickRoot != null)
		{
			UnityEngine.Object.Destroy(m_quickRoot.gameObject);
			m_quickRoot = null;
		}
		if (m_quickBgObj != null)
		{
			UnityEngine.Object.Destroy(m_quickBgObj);
			m_quickBgObj = null;
		}
		m_quickSnapping = false;
		m_quickMomentum = false;
		m_quickAnimating = false;
		m_quickSlideVel = 0f;
		// 面板一关就把选项卡放回原位，和展开时一样是当帧生效
		UpdateCatalogTabs();
	}

	public void AddUnlockStarTierPopup(UnavailablePartButton button, UnlockRoadHogsParts dialog, int tier, int currentStars, int cost, Func<bool> requirements)
	{
		if (button == null || dialog == null)
		{
			return;
		}
		button.OnPress = (Action)Delegate.Combine(button.OnPress, (Action)delegate
		{
			dialog.Cost = $"[snout] {cost}";
			dialog.ShowConfirmEnabled = () => true;
			dialog.SetOnConfirm(delegate
			{
				if (requirements() && GameProgress.UseSnoutCoins(cost))
				{
					GameProgress.AddRaceLevelUnlockedStars(tier - currentStars);
					Singleton<AudioManager>.Instance.Spawn2dOneShotEffect(WPFMonoBehaviour.gameData.commonAudioCollection.snoutCoinUse);
					CreatePartList(handleDragIcons: false);
					dialog.Close();
				}
				else if (!requirements() && Singleton<IapManager>.IsInstantiated())
				{
					dialog.Close();
					Singleton<IapManager>.Instance.OpenShopPage(dialog.Open, "SnoutCoinShop");
				}
				else
				{
					dialog.Close();
				}
			});
			dialog.Open();
		});
	}
}
