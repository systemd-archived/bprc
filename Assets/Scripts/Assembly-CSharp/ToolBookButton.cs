using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UButton = UnityEngine.UI.Button;
using UImage = UnityEngine.UI.Image;
using UText = UnityEngine.UI.Text;

public partial class ToolBookButton : MonoBehaviour
{
	private static ToolBookButton s_instance;
	private RectTransform m_mainRect;
	private List<RectTransform> m_childRects = new List<RectTransform>();
	private List<UImage> m_childBgs = new List<UImage>();
	private List<UText> m_childTexts = new List<UText>();
	private bool m_isOpen;
	private bool m_coroutineRunning;
	private float m_slideSpeed = 20000f;
	private float m_size = 1f;
	private bool m_isDragging;
	private Canvas m_canvas;
	private bool m_hiddenBySetting;
	private bool m_runCollapsed;
	private bool m_runAdjustMenuShown;
	// 运行态菜单：m_runStateActive=当前处于游戏运行状态；m_runOptionsShown=显示全新运行选项(true)还是原建造选项(false)
	private bool m_runStateActive;
	private bool m_runOptionsShown;
	private bool m_quickSpeedOn;
	private bool m_runPaused;
	private int m_activeChildIndex = -1;

	// 收起且长时间未使用时的半透明淡出
	private CanvasGroup m_canvasGroup;
	private float m_lastUseTime;
	private const float kIdleFadeDelay = 5f;    // 无操作多少秒后开始变半透明
	private const float kIdleAlpha = 0.25f;    // 目标透明度（15%）
	private const float kIdleFadeSpeed = 4f;   // 淡出速度（每秒 alpha 变化量）

	// 暂停时长按选区（仅画框，不启用实际功能）
	private UnityEngine.UI.Image m_pauseProgressFill;
	private bool m_pauseLongPressActive;
	private float m_pauseLongPressStartTime;
	private Vector2 m_pauseLongPressStartScreen;
	private bool m_pauseBoxArmed;
	private Vector3 m_pauseBoxStartWorld;
	private Vector3 m_pauseBoxEndWorld;
	// 暂停画框后捕获的物体（画框结束时固定，拖动只移动这些）
	private readonly List<BasePart> m_pausePickedParts = new List<BasePart>();
	private bool m_pausePartDragging;
	private Vector3 m_pausePartDragStartWorld;
	private GameObject m_pauseBoxRoot;
	private Transform m_pauseBoxFill;
	private readonly Transform[] m_pauseBoxEdges = new Transform[4];
	private Material m_pauseBoxFillMat;
	private Material m_pauseBoxEdgeMat;

	// 暂停框选旋转（以选框中心为轴心；双指或第二列“角度”旋钮；角度对齐 5°）
	private float m_pauseBoxAngle;
	private bool m_pauseAngleSnap;
	private readonly List<Vector3> m_pauseRotStartPos = new List<Vector3>();
	private readonly List<float> m_pauseRotStartZ = new List<float>();
	private Vector3 m_pauseRotPivot;
	private float m_pauseRotGestureBase;
	private float m_pauseRotTarget;
	private bool m_pauseKnobDragging;
	private Vector2 m_pauseKnobDragStartScreen;
	private float m_pauseKnobDragStartAngle;
	private Vector2 m_pauseKnobCenter;
	private float m_pauseKnobStickAngle0;
	private bool m_pauseKnobMoved;
	private bool m_twoFingerRotating;
	private float m_twoFingerLastAngle;
	private GameObject m_angleKnobObj;
	private UImage m_angleKnobBg;
	private UText m_angleKnobText;
	private RectTransform m_angleKnobNeedle;

	// 运行框选复制/粘贴（第二列，仅暂停显示）：直接记录被框选部件本体，粘贴时克隆
	private readonly List<BasePart> m_runCopyParts = new List<BasePart>();
	private readonly List<Vector3> m_runCopyOffsets = new List<Vector3>();
	private readonly List<Quaternion> m_runCopyRots = new List<Quaternion>();
	private readonly List<Vector3> m_runCopyVel = new List<Vector3>();
	private readonly List<Vector3> m_runCopyAngVel = new List<Vector3>();
	// 每个被复制部件所属的“载具连通分组”（区分同一个载具与另一个载具）
	private readonly List<int> m_runCopyGroup = new List<int>();
	private Vector3 m_runCopyAnchor;
	// 常规(橙色)模式下对“粘贴等继承类”操作的旋转角
	private float m_runClipAngle;
	// 框选框淡出（无物体时先变红再渐变消失）
	private float m_pauseBoxFade;
	private const float kPauseBoxFadeTime = 0.6f;
	private static readonly Color s_boxFillDefault = new Color(0.3f, 0.5f, 1f, 0.3f);
	private static readonly Color s_boxEdgeDefault = new Color(0.4f, 0.6f, 1f, 0.9f);
	private static readonly Color s_boxFillBad = new Color(1f, 0.2f, 0.2f, 0.35f);
	private static readonly Color s_boxEdgeBad = new Color(1f, 0.3f, 0.3f, 0.95f);
	// 惯性继承模式：0=关 1=相对角度(默认，速度随粘贴旋转一起转) 2=绝对角度
	private int m_runVelocityMode = 1;
	private bool m_runPasteDragging;
	private Vector2 m_runPasteDragStartScreen;
	private GameObject m_runCopyObj;
	private GameObject m_runPasteObj;
	private GameObject m_runInertiaObj;
	private UImage m_runInertiaBg;
	private UText m_runInertiaText;
	// 单帧步进（暂停时可用：真实跑一个固定帧后仍保持暂停）
	private GameObject m_runStepObj;
	private bool m_stepRunning;
	// 粘贴预览：复制时对被复制部件做一次屏幕截图，拖动“粘贴”时用该截图跟随（最简方案）
	private GameObject m_runPreviewRoot;
	private readonly List<GameObject> m_runPreviewObjs = new List<GameObject>();
	private readonly List<Material> m_runPreviewMats = new List<Material>();
	private Texture2D m_runPreviewTex;
	private Vector2 m_runPreviewPixel = Vector2.one;       // 截图区域的像素尺寸
	private Vector2 m_runPreviewScreenOffset;              // 预览中心相对锚点的屏幕像素偏移（按当前相机换算）
	private Vector2 m_runPreviewScreenCenter;              // 预览在屏幕上的中心（IMGUI 绘制用）
	// 预览的世界尺寸/世界偏移：拖动时按**当前**相机换算像素尺寸与屏幕偏移，
	// 这样视野缩放后幽灵预览的大小会跟着变（而不是固定为复制时的像素尺寸）。
	private Vector3 m_runPreviewWorldOffset;
	private Vector2 m_runPreviewWorldSize = Vector2.one;
	// 复制时“每世界单位占多少屏幕像素”，用于按当前相机做**等比缩放**（只缩放、不拉伸）
	private float m_runPreviewCapturePpu;
	private bool m_runPreviewLogged;
	// 幽灵贴图当前使用的粘贴角度；以及是否有一次生成正在进行
	private float m_runPreviewAngleUsed;
	private bool m_runCapturePending;

	// Column 2 buttons
	private GameObject m_deleteBtnObj;
	private RectTransform m_deleteRect;
	private UImage m_deleteBg;
	private UText m_deleteText;
	private GameObject m_overwriteObj;
	private UImage m_overwriteBg;
	private UText m_overwriteText;
	private GameObject m_rotateObj;
	private UText m_rotateText;
	private GameObject m_materialObj;
	private UImage m_materialBg;
	private UText m_materialText;
	private GameObject m_shapeTypeObj;
	private UImage m_shapeTypeBg;
	private UText m_shapeTypeText;
	private GameObject m_shapeSolidObj;
	private UImage m_shapeSolidBg;
	private UText m_shapeSolidText;

	// Column 3: clipboard slots (display slots = page size)
	private List<GameObject> m_clipSlotObjs = new List<GameObject>();
	private List<RectTransform> m_clipSlotRects = new List<RectTransform>();
	private List<UImage> m_clipSlotBgs = new List<UImage>();
	private List<UText> m_clipSlotTexts = new List<UText>();

	// Clipboard scroll pagination
	private int m_clipScrollOffset = 0;
	private GameObject m_clipPageUpObj;
	private GameObject m_clipPageDownObj;
	private GameObject m_clipPageTextObj;
	private UText m_clipPageText;
	private static readonly Color s_pageBtnBg = new Color(0.3f, 0.3f, 0.3f, 0.85f);

	// Confirm/Cancel for shape drawing
	private GameObject m_confirmObj;
	private GameObject m_cancelObj;

	// Clip drag state
	private bool m_clipDragging;
	private Vector2 m_clipDragStartScreenPos;
	private int m_clipDragIndex = -1;
	private bool m_clipDragStarted;

	// Rename UI
	private GameObject m_renameObj;
	private RectTransform m_renameRect;
	private UnityEngine.UI.InputField m_renameInput;
	private UText m_renameTitle;
	private UText m_renamePlaceholder;
	private int m_renameSlotIndex = -1;

	// Paste drag state
	private bool m_pasteDragging;
	private Vector2 m_pasteDragStartScreenPos;

	// Undo button reference
	private RectTransform m_undoRect;
	private UImage m_undoBg;

	// Selection dimension label
	private GameObject m_dimLabelObj;
	private UText m_dimLabel;

	// Child button labels, by the locale id each one reads its text from.
	private static readonly string[] s_toolIds = { "ToolBook_Copy", "ToolBook_Paste", "ToolBook_Select", "ToolBook_Undo", "ToolBook_Stash", "ToolBook_Cut", "ToolBook_Shape", "ToolBook_Fill", "ToolBook_Save" };

	/// <summary>
	/// Where each of the nine toolbar buttons sits in <see cref="s_toolIds"/>.
	/// </summary>
	/// <remarks>
	/// Named places rather than the text: the handlers and the highlight used to find their button by
	/// comparing its label, which stops working the moment the label is translated.
	/// </remarks>
	private const int ToolCopy = 0;

	private const int ToolPaste = 1;

	private const int ToolSelect = 2;

	private const int ToolUndo = 3;

	private const int ToolStash = 4;

	private const int ToolCut = 5;

	private const int ToolShape = 6;

	private const int ToolFill = 7;

	private const int ToolSave = 8;

	// Shape type names, indexed by the selection manager's shape type.
	private static readonly string[] s_shapeIds = { "ToolBook_Shape_Square", "ToolBook_Shape_Rectangle", "ToolBook_Shape_Circle", "ToolBook_Shape_Ellipse", "ToolBook_Shape_Line" };

	/// <summary>The text for a locale id, or the id itself while there is no localization to read.</summary>
	private static string L(string id)
	{
		return INLocalization.Instance != null ? INLocalization.Instance.GetText(id) : id;
	}

	/// <summary>
	/// How much wider a button has to be than the Chinese it was sized for.
	/// </summary>
	/// <remarks>
	/// The menu's buttons are 48 wide and hold two Chinese characters, which is what the layout was built
	/// around; an English label is four to six characters in the same font and would wrap into a second
	/// line that the 36-high button clips. The widths and the column maths both use this, so the layout
	/// stays consistent whichever language is on.
	/// </remarks>
	private static float LabelWidth
	{
		get { return InterfaceLanguages.ToInterface(INUnity.Language) == InterfaceLanguage.English ? 1.4f : 1f; }
	}

	// 游戏运行时的全新选项（骨架，功能后续添加）；第 6 个（RunSwitchIndex）为“切换”按钮，用于切回原建造选项
	private static readonly string[] s_runIds = { "ToolBook_RunPause", "ToolBook_RunSpeed", "ToolBook_RunRestart", "ToolBook_RunCycleFollow", "ToolBook_RunFollowNone", "ToolBook_RunSwitch" };
	private const int RunSwitchIndex = 5;
	private const int RunCycleFollowIndex = 3;
	private const int RunFollowModeIndex = 4;

	private static readonly Color s_normalBg = new Color(0.2f, 0.2f, 0.2f, 0.9f);
	private static readonly Color s_normalText = Color.white;
	private static readonly Color s_activeBg = new Color(0.9f, 0.9f, 0.9f, 0.95f);
	private static readonly Color s_activeText = new Color(0.1f, 0.1f, 0.1f, 1f);
	private static readonly Color s_redBg = new Color(0.9f, 0.15f, 0.15f, 0.95f);
	private static readonly Color s_clipFilledBg = new Color(0.15f, 0.45f, 0.75f, 0.9f);
	private static readonly Color s_clipEmptyBg = new Color(0.25f, 0.25f, 0.25f, 0.7f);
	// 变速启用时的按钮配色
	private static readonly Color s_speedOnBg = new Color(0.15f, 0.5f, 0.15f, 0.95f);
	// 暂停模式下的按钮配色（变白 + 深色文字）
	private static readonly Color s_pausedBg = new Color(0.95f, 0.95f, 0.95f, 0.95f);
	private static readonly Color s_pausedText = new Color(0.1f, 0.1f, 0.1f, 1f);
	// 暂停时长按 0.15 秒进入“只画框”选区预览（需几乎不移动）
	private const float kPauseLongPressDuration = 0.15f;
	// 长按期间允许的最大位移（屏幕像素），超过则视为滑动/拖动，取消长按
	private const float kPauseLongPressMaxMove = 12f;
	// 角度旋钮：极坐标（摇杆）式拖动；角度对齐档位
	private const float kPauseAngleSnapStep = 5f;

	private void Awake()
	{
		if (s_instance != null)
		{
			Destroy(gameObject);
			return;
		}
		s_instance = this;
		transform.SetParent(null);
		DontDestroyOnLoad(gameObject);

		m_canvas = gameObject.AddComponent<Canvas>();
		m_canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		m_canvas.sortingOrder = 100;
		var scaler = gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
		scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920f, 1080f);
		scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
		scaler.matchWidthOrHeight = 0.5f;
		gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

		m_canvasGroup = gameObject.AddComponent<CanvasGroup>();
		m_lastUseTime = Time.unscaledTime;

		ApplySettings();
		CreateMainButton();
		CreateChildButtons();
		CreateColumn2Buttons();
		CreateRunExtraButtons();
		CreateAdvancedDisplayUI();
		CreateClipboardSlots();
		CreateDimLabel();
		ApplyPosition();

		gameObject.AddComponent<INSelectionManager>();
		gameObject.AddComponent<INBuildQuickOptions>();

		// The menu builds its own labels rather than using locale components, so it has to re-read them
		// itself when the language changes.
		EventManager.Connect<LocalizationReloaded>(OnLocalizationReloaded);
	}

	private void OnDestroy()
	{
		EventManager.Disconnect<LocalizationReloaded>(OnLocalizationReloaded);
		if (s_instance == this) s_instance = null;
		INSelectionManager.PauseBoxDrawing = false;
		ClearRunPreview();
		if (m_runPreviewTex != null) { Destroy(m_runPreviewTex); m_runPreviewTex = null; }
		m_pausePickedParts.Clear();
		if (m_pauseBoxRoot != null) Destroy(m_pauseBoxRoot);
		if (m_pauseBoxFillMat != null) Destroy(m_pauseBoxFillMat);
		if (m_pauseBoxEdgeMat != null) Destroy(m_pauseBoxEdgeMat);
	}

	private void OnLocalizationReloaded(LocalizationReloaded reloaded)
	{
		RefreshLocalization();
	}

	/// <summary>
	/// Re-reads the labels the menu shows, in the language that is now on.
	/// </summary>
	/// <remarks>
	/// The toolbar and the rename panel hold fixed text; the rest say something about the current state -
	/// what the delete button will delete, the paste mode, the rotation angle, the material and the shape -
	/// and the frame loop re-derives those anyway. They are re-derived here as well, because the frame that
	/// carries the change would still be drawn with the old language otherwise.
	/// </remarks>
	private void RefreshLocalization()
	{
		for (int i = 0; i < m_childTexts.Count && i < s_toolIds.Length; i++)
		{
			if (m_childTexts[i] != null) { m_childTexts[i].text = L(s_toolIds[i]); }
		}
		if (m_renameTitle != null) { m_renameTitle.text = L("ToolBook_NameSlot"); }
		if (m_renamePlaceholder != null) { m_renamePlaceholder.text = L("ToolBook_NamePlaceholder"); }
		UpdateDeleteButtonLabel();
		UpdateOverwriteToggle();
		UpdateRotateButton();
		UpdateMaterialButton();
		UpdateShapeTypeButton();
		UpdateShapeSolidButton();
		// The English labels are longer, so the buttons have to grow with them.
		ApplySizes();
	}

	private void Update()
	{
		if (m_childBgs.Count == 0) return;
		UpdateAdvancedDisplayUI();
		bool shouldBeOn = INUserSettings.Instance?.StarSeaSettings?.ToolBookEnabled ?? false;
		bool runCollapse = INUserSettings.Instance?.StarSeaSettings?.GameRunCollapse ?? false;
		bool running = IsGameRunning();
		// 菜单内容判定：运行中或运行中暂停都算“游戏中”
		bool runMenu = running || IsGamePausedRunning();

		// 游戏运行的收起：开启=整体隐藏；关闭=不收起，改为唤出调节菜单（预留空方法）
		// 注意：暂停中（m_runPaused）不收起，否则暂停后菜单无法再打开
		if (running && runCollapse && !m_runPaused)
		{
			if (!m_runCollapsed)
			{
				m_runCollapsed = true;
				m_runAdjustMenuShown = false;
				ResetRunTimeScale();
				CollapseForRunning();
			}
			return;
		}
		m_runCollapsed = false;

		if (!shouldBeOn)
		{
			if (!m_hiddenBySetting) { m_mainRect.gameObject.SetActive(false); m_hiddenBySetting = true; }
			return;
		}
		if (m_hiddenBySetting) { m_mainRect.gameObject.SetActive(true); m_hiddenBySetting = false; m_lastUseTime = Time.unscaledTime; }

		// 收起状态下，按在主按钮上即算作“使用”，重新计时
		if (Input.GetMouseButtonDown(0) && IsOverRect(m_mainRect))
			m_lastUseTime = Time.unscaledTime;

		// 运行态：菜单内容切换为全新运行选项（可点“切换”临时回到原建造选项）
		if (runMenu != m_runStateActive)
		{
			m_runStateActive = runMenu;
			m_runOptionsShown = runMenu;
			ApplyMenuLabels();
			if (runMenu)
			{
				m_runAdjustMenuShown = true;
				ShowRunAdjustMenu();
			}
			else
			{
				m_runAdjustMenuShown = false;
				ResetRunTimeScale();
				RestoreBuildOnlyWidgets();
			}
		}
		if (m_runStateActive && m_runOptionsShown)
		{
			HideBuildOnlyWidgets();
			UpdateRunLabels();
		}

		// Active button highlight (选区)；运行选项态不做选区高亮，避免覆盖运行项配色
		if (!(m_runStateActive && m_runOptionsShown))
		{
			bool selecting = INSelectionManager.Instance != null &&
				INSelectionManager.Instance.CurrentState != INSelectionManager.State.Idle;
			int selectIdx = ToolSelect;
			int newActive = selecting ? selectIdx : -1;
			if (newActive != m_activeChildIndex)
			{
				if (m_activeChildIndex >= 0 && m_activeChildIndex < m_childBgs.Count && m_childBgs[m_activeChildIndex] != null)
				{
					m_childBgs[m_activeChildIndex].color = s_normalBg;
					if (m_childTexts[m_activeChildIndex] != null) m_childTexts[m_activeChildIndex].color = s_normalText;
				}
				if (newActive >= 0 && newActive < m_childBgs.Count && m_childBgs[newActive] != null)
				{
					m_childBgs[newActive].color = s_activeBg;
					if (m_childTexts[newActive] != null) m_childTexts[newActive].color = s_activeText;
				}
				m_activeChildIndex = newActive;
			}
		}

		// Undo red highlight during drag
		if (m_pasteDragging || m_clipDragging)
		{
			if (m_undoBg != null && IsOverUndoButton())
				m_undoBg.color = s_redBg;
			else
				ResetUndoColor();
		}
		else
		{
			ResetUndoColor();
		}

		// Delete red during clip drag
		if (m_clipDragging && m_deleteBg != null)
			m_deleteBg.color = IsOverDeleteButton() ? s_redBg : new Color(0.8f, 0.1f, 0.1f, 0.9f);

		UpdateDeleteButtonLabel();
		SyncClipboardSlotCount();
		RefreshClipboardSlots();
		UpdateSaveDedupFlash();
		UpdateOverwriteToggle();
		UpdateRotateButton();
		UpdateMaterialButton();
		UpdateShapeTypeButton();
		UpdateShapeSolidButton();
		UpdateConfirmCancelVisibility();
		UpdateDimLabel();
		UpdateIdleTransparency();
		UpdatePauseSelection();
		UpdatePauseBoxFade();
		UpdateAngleKnob();
		UpdateRunClipButtons();
		UpdateRunExtraButtons();
	}

	// 暂停模式下：屏幕任意位置静止长按 0.15 秒（暂停按钮上有填充进度条提示）
	// 进入“只画框”的选区预览；松手后捕获框内物体（此后固定）。
	// 之后：框内按住拖动=移动这批物体（框跟随）；双指/角度旋钮=绕选框中心旋转（无极）。
	private void UpdatePauseSelection()
	{
		if (!m_runPaused)
		{
			m_pauseLongPressActive = false;
			m_pauseBoxArmed = false;
			m_pausePartDragging = false;
			// 注意：不要清 m_pauseKnobDragging —— 旋钮在非暂停(运行中)也要能拖动，
			// 每帧清零会让 OnAngleKnobDrag 立即 return。
			m_twoFingerRotating = false;
			m_pausePickedParts.Clear();
			m_pauseBoxAngle = 0f;
			SetPauseProgress(0f);
			ClearPauseBox();
			INSelectionManager.PauseBoxDrawing = false;
			return;
		}

		// 高级显示：正在拖动速度指针时，不进入长按画框（拖动由 UpdateAdvancedDisplayDrag 处理）
		if (m_advVelocityDragging)
		{
			m_pauseLongPressActive = false;
			m_pauseBoxArmed = false;
			m_pausePartDragging = false;
			SetPauseProgress(0f);
			INSelectionManager.PauseBoxDrawing = true;
			return;
		}

		bool haveBox = m_pauseBoxRoot != null && m_pauseBoxRoot.activeSelf;

		// 双指旋转（触摸）：以选框中心为轴心，无极
		if (haveBox && m_pausePickedParts.Count > 0 && Input.touchCount == 2)
		{
			Touch t0 = Input.GetTouch(0);
			Touch t1 = Input.GetTouch(1);
			Vector2 d = t1.position - t0.position;
			float cur = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
			if (!m_twoFingerRotating)
			{
				m_twoFingerRotating = true;
				m_twoFingerLastAngle = cur;
				BeginPauseRotation();
				m_pauseRotTarget = m_pauseBoxAngle;
			}
			else
			{
				m_pauseRotTarget += Mathf.DeltaAngle(m_twoFingerLastAngle, cur);
				m_twoFingerLastAngle = cur;
				ApplyPauseRotation(m_pauseRotTarget);
			}
			INSelectionManager.PauseBoxDrawing = true;
			return;
		}
		m_twoFingerRotating = false;

		// UI 拖动进行中（从按钮拖出粘贴 / 拖旋钮）时，绝不处理世界框选交互，
		// 否则会被误判成“框内拖动”，把框内部件按鼠标位移搬走。
		if (m_runPasteDragging || m_pauseKnobDragging || m_clipDragging)
		{
			INSelectionManager.PauseBoxDrawing = true;
			return;
		}

		// 按在工具栏任意 UI 上时不触发长按画框/框内拖动
		// （EventSystem.current 在本项目不可靠——存在多个 EventSystem，故显式做矩形判定）
		bool overUI = IsPointerOverToolBook() ||
			(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject());

		if (Input.GetMouseButtonDown(0) && !overUI && Input.touchCount <= 1)
		{
			Vector3 w = GetBoxWorldPos(Input.mousePosition);
			if (haveBox && IsInsidePauseBox(w))
			{
				// 框内直接拖：移动已捕获的物体
				m_pausePartDragging = true;
				m_pausePartDragStartWorld = w;
			}
			else
			{
				m_pauseLongPressActive = true;
				m_pauseLongPressStartTime = Time.unscaledTime;
				m_pauseLongPressStartScreen = Input.mousePosition;
				SetPauseProgress(0f);
			}
		}

		// 拖动被框住的物体（框跟随移动；新进入框的物体忽略，集合固定）
		if (m_pausePartDragging && Input.GetMouseButton(0))
		{
			Vector3 w = GetBoxWorldPos(Input.mousePosition);
			Vector3 delta = w - m_pausePartDragStartWorld;
			if (delta != Vector3.zero)
			{
				for (int i = 0; i < m_pausePickedParts.Count; i++)
				{
					BasePart p = m_pausePickedParts[i];
					if (p != null) p.transform.position += delta;
				}
				m_pauseBoxStartWorld += delta;
				m_pauseBoxEndWorld += delta;
				m_pausePartDragStartWorld = w;
				UpdatePauseBoxVisual();
			}
		}

		if (m_pauseLongPressActive && !m_pauseBoxArmed && Input.GetMouseButton(0))
		{
			// 位移过大 → 视为滑动/拖动视野，取消长按
			if (Vector2.Distance(Input.mousePosition, m_pauseLongPressStartScreen) > kPauseLongPressMaxMove)
			{
				m_pauseLongPressActive = false;
				SetPauseProgress(0f);
			}
			else
			{
				float p = Mathf.Clamp01((Time.unscaledTime - m_pauseLongPressStartTime) / kPauseLongPressDuration);
				SetPauseProgress(p);
				if (p >= 1f)
				{
					// 长按完成：从当前点开始拉框（替换旧框与旧选择）
					m_pauseBoxArmed = true;
					SetPauseProgress(0f);
					m_pausePickedParts.Clear();
					m_pauseBoxAngle = 0f;
					m_pauseBoxStartWorld = GetBoxWorldPos(Input.mousePosition);
					m_pauseBoxEndWorld = m_pauseBoxStartWorld;
					EnsurePauseBox();
					m_pauseBoxFade = 0f;
					if (m_pauseBoxFillMat != null) m_pauseBoxFillMat.color = s_boxFillDefault;
					if (m_pauseBoxEdgeMat != null) m_pauseBoxEdgeMat.color = s_boxEdgeDefault;
					UpdatePauseBoxVisual();
				}
			}
		}

		if (m_pauseBoxArmed && Input.GetMouseButton(0))
		{
			m_pauseBoxEndWorld = GetBoxWorldPos(Input.mousePosition);
			UpdatePauseBoxVisual();
		}

		if (Input.GetMouseButtonUp(0))
		{
			if (m_pauseBoxArmed)
			{
				// 画框结束：捕获框内物体（之后固定不变）
				CapturePauseBoxParts();
				float bw = Mathf.Abs(m_pauseBoxEndWorld.x - m_pauseBoxStartWorld.x);
				float bh = Mathf.Abs(m_pauseBoxEndWorld.y - m_pauseBoxStartWorld.y);
				if (m_pausePickedParts.Count == 0)
				{
					// 框内无物体：先变红，再渐变消失
					m_pauseBoxAngle = 0f;
					StartPauseBoxBadFade();
				}
				else if (bw < 0.25f && bh < 0.25f)
				{
					// 范围过小：直接取消
					m_pausePickedParts.Clear();
					m_pauseBoxAngle = 0f;
					ClearPauseBox();
				}
			}
			// 把框拖到“删除”按钮上松手 → 删除框内部件
			if (m_pausePartDragging && IsOverRunDeleteButton())
			{
				DeleteRunParts(m_pausePickedParts);
				m_pauseBoxAngle = 0f;
				m_pauseBoxFade = 0f;
				ClearPauseBox();
				if (m_pauseBoxFillMat != null) m_pauseBoxFillMat.color = s_boxFillDefault;
				if (m_pauseBoxEdgeMat != null) m_pauseBoxEdgeMat.color = s_boxEdgeDefault;
			}
			m_pauseLongPressActive = false;
			m_pauseBoxArmed = false;      // 框保留在屏幕上
			m_pausePartDragging = false;
			SetPauseProgress(0f);
		}
		else if (!Input.GetMouseButton(0))
		{
			SetPauseProgress(0f);
		}

		// 画框/拖动/旋钮旋转期间禁用视角拖动
		INSelectionManager.PauseBoxDrawing = m_pauseBoxArmed || m_pausePartDragging || m_pauseKnobDragging;
	}

	// --- 暂停旋转（以选框中心为轴心，无极；角度对齐可选 3°） ---

	private void BeginPauseRotation()
	{
		if (m_pausePickedParts.Count == 0) return;
		float minX = Mathf.Min(m_pauseBoxStartWorld.x, m_pauseBoxEndWorld.x);
		float maxX = Mathf.Max(m_pauseBoxStartWorld.x, m_pauseBoxEndWorld.x);
		float minY = Mathf.Min(m_pauseBoxStartWorld.y, m_pauseBoxEndWorld.y);
		float maxY = Mathf.Max(m_pauseBoxStartWorld.y, m_pauseBoxEndWorld.y);
		m_pauseRotPivot = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0f);
		m_pauseRotGestureBase = m_pauseBoxAngle;
		m_pauseRotStartPos.Clear();
		m_pauseRotStartZ.Clear();
		for (int i = 0; i < m_pausePickedParts.Count; i++)
		{
			BasePart p = m_pausePickedParts[i];
			if (p == null) { m_pauseRotStartPos.Add(Vector3.zero); m_pauseRotStartZ.Add(0f); continue; }
			m_pauseRotStartPos.Add(p.transform.position);
			m_pauseRotStartZ.Add(p.transform.eulerAngles.z);
		}
	}

	private void ApplyPauseRotation(float targetAngle)
	{
		float snapped = m_pauseAngleSnap
			? Mathf.Round(targetAngle / kPauseAngleSnapStep) * kPauseAngleSnapStep
			: targetAngle;
		m_pauseBoxAngle = snapped;
		float delta = snapped - m_pauseRotGestureBase;
		float rad = delta * Mathf.Deg2Rad;
		float cs = Mathf.Cos(rad);
		float sn = Mathf.Sin(rad);
		int n = Mathf.Min(m_pausePickedParts.Count, m_pauseRotStartPos.Count);
		for (int i = 0; i < n; i++)
		{
			BasePart p = m_pausePickedParts[i];
			if (p == null) continue;
			Vector3 off = m_pauseRotStartPos[i] - m_pauseRotPivot;
			Vector3 rot = new Vector3(off.x * cs - off.y * sn, off.x * sn + off.y * cs, 0f);
			p.transform.position = m_pauseRotPivot + rot;
			p.transform.rotation = Quaternion.Euler(0f, 0f, m_pauseRotStartZ[i] + delta);
		}
		// 若已复制过，旋转后同步刷新剪贴板，使“复制→旋转→粘贴”也能带上旋转
		if (m_runCopyParts.Count > 0 && m_pausePickedParts.Count > 0) CopyRunSelection();
		UpdatePauseBoxVisual();
	}

	private void OnAngleKnobPointerDown(BaseEventData data)
	{
		PointerEventData ped = (PointerEventData)data;
		m_pauseKnobDragging = true;
		m_pauseKnobMoved = false;
		m_pauseKnobDragStartScreen = ped.position;

		// 摇杆中心 = 旋钮按钮的屏幕中心
		RectTransform rt = m_angleKnobObj.GetComponent<RectTransform>();
		Vector3[] corners = new Vector3[4];
		rt.GetWorldCorners(corners);
		m_pauseKnobCenter = new Vector2((corners[0].x + corners[2].x) * 0.5f, (corners[0].y + corners[2].y) * 0.5f);
		Vector2 v0 = ped.position - m_pauseKnobCenter;
		m_pauseKnobStickAngle0 = Mathf.Atan2(v0.y, v0.x) * Mathf.Rad2Deg;

		// 框选状态(蓝) → 旋转框选部件；常规状态(橙) → 旋转“粘贴等继承类”操作
		if (m_pausePickedParts.Count > 0)
		{
			m_pauseKnobDragStartAngle = m_pauseBoxAngle;
			BeginPauseRotation();
			m_pauseRotTarget = m_pauseBoxAngle;
		}
		else
		{
			m_pauseKnobDragStartAngle = m_runClipAngle;
		}
	}

	private void OnAngleKnobDrag(BaseEventData data)
	{
		if (!m_pauseKnobDragging) return;
		PointerEventData ped = (PointerEventData)data;
		Vector2 d = ped.position - m_pauseKnobDragStartScreen;
		if (d.magnitude > 4f) m_pauseKnobMoved = true;

		// 极坐标（摇杆）：由指针相对旋钮中心的方位角变化决定旋转量
		Vector2 v = ped.position - m_pauseKnobCenter;
		if (v.sqrMagnitude < 25f) return;
		float stick = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
		float delta = Mathf.DeltaAngle(m_pauseKnobStickAngle0, stick);
		float target = m_pauseKnobDragStartAngle + delta;

		if (m_pausePickedParts.Count > 0)
		{
			m_pauseRotTarget = target;
			ApplyPauseRotation(m_pauseRotTarget);
		}
		else
		{
			m_runClipAngle = m_pauseAngleSnap
				? Mathf.Round(target / kPauseAngleSnapStep) * kPauseAngleSnapStep
				: target;
			// 粘贴角度变了：请求按新角度重新生成幽灵贴图
			RequestRunPreviewCapture();
		}
	}

	private void OnAngleKnobPointerUp(BaseEventData data)
	{
		if (!m_pauseKnobDragging) return;
		m_pauseKnobDragging = false;
		if (!m_pauseKnobMoved)
		{
			// 单击：开关角度对齐（5°）
			m_pauseAngleSnap = !m_pauseAngleSnap;
		}
	}

	// --- 运行框选 复制/粘贴（第二列，仅暂停显示） ---

	private void UpdateRunClipButtons()
	{
		if (m_runCopyObj == null) return;
		// 不再限制“仅暂停”：运行中（运行选项菜单可见）也可用复制/粘贴/惯性
		bool show = m_isOpen && m_runStateActive && m_runOptionsShown;
		if (m_runCopyObj.activeSelf != show) m_runCopyObj.SetActive(show);
		if (m_runPasteObj.activeSelf != show) m_runPasteObj.SetActive(show);
		if (m_runInertiaObj.activeSelf != show) m_runInertiaObj.SetActive(show);
		if (!show) return;

		float mainW = m_mainRect.sizeDelta.x;
		float spacing = 6f * m_size;
		float btnW = 48f * m_size;
		float btnH = 36f * m_size;
		float col2X = -(mainW + spacing + btnW + spacing);
		// 角度旋钮在 y=0，往下依次是 复制/粘贴/惯性
		m_runCopyObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(col2X, -(btnH + spacing));
		m_runPasteObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(col2X, -(2 * (btnH + spacing)));
		m_runInertiaObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(col2X, -(3 * (btnH + spacing)));
		// 单帧按钮：仅暂停时显示
		bool stepShow = show && m_runPaused;
		if (m_runStepObj != null)
		{
			if (m_runStepObj.activeSelf != stepShow) m_runStepObj.SetActive(stepShow);
			if (stepShow) m_runStepObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(col2X, -(4 * (btnH + spacing)));
		}

		// 惯性按钮：关 / 相对角度 / 绝对角度
		if (m_runInertiaText != null)
		{
			string vt = m_runVelocityMode == 0 ? "惯:关" : (m_runVelocityMode == 1 ? "惯:相对" : "惯:绝对");
			if (m_runInertiaText.text != vt) m_runInertiaText.text = vt;
		}
		if (m_runInertiaBg != null)
		{
			Color c = m_runVelocityMode == 0 ? s_normalBg
				: (m_runVelocityMode == 1 ? s_speedOnBg : new Color(0.7f, 0.45f, 0.1f, 0.95f));
			if (m_runInertiaBg.color != c) m_runInertiaBg.color = c;
		}
	}

	private void OnRunCopyClicked()
	{
		CopyRunSelection();
		// 复制后按当前角度请求生成幽灵贴图（在帧末安全时机生成）
		RequestRunPreviewCapture();
	}

	// 请求（重新）生成幽灵贴图：按当前粘贴角度；同一时刻只挂一个协程
	private void RequestRunPreviewCapture()
	{
		if (m_runPreviewTex != null && Mathf.Abs(Mathf.DeltaAngle(m_runPreviewAngleUsed, m_runClipAngle)) < 0.01f)
			return;   // 角度没变且已有贴图，不必重做
		if (m_runCapturePending) return;
		if (m_runCopyParts.Count == 0) return;
		StartCoroutine(CaptureRunPreviewAfterRender(m_runClipAngle));
	}

	private IEnumerator CaptureRunPreviewAfterRender(float angleDeg)
	{
		m_runCapturePending = true;
		// 生成前先隐藏选框（隔离渲染已排除其它物件，属双保险）
		bool boxWasActive = m_pauseBoxRoot != null && m_pauseBoxRoot.activeSelf;
		if (boxWasActive) m_pauseBoxRoot.SetActive(false);
		yield return new WaitForEndOfFrame();
		CaptureRunPreviewTexture(angleDeg);
		m_runPreviewAngleUsed = angleDeg;
		if (boxWasActive && m_pauseBoxRoot != null) m_pauseBoxRoot.SetActive(true);
		m_runCapturePending = false;
	}

	// 生成粘贴预览贴图：**只渲染被复制的部件**并把粘贴旋转角**烘焙进贴图**（临时正交相机
	// 绕 Z 轴转 -angle 渲染到透明背景 RenderTexture，期间隐藏其它所有渲染器）。
	// 旋转完全由世界数学决定，不依赖 GUI 空间旋转方向，方向/位置都与实际落点一致。
	private void CaptureRunPreviewTexture(float angleDeg)
	{
		if (m_runCopyParts.Count == 0) return;
		Camera cam = Camera.main;
		if (cam == null) return;

		Bounds b;
		if (!TryGetCopiedPartsBounds(out b)) { Debug.LogWarning("[RunPreview] no mesh/sprite renderers on copied parts"); return; }

		float ppu = GetPixelsPerWorldUnit(cam, b);
		if (ppu <= 0.01f) ppu = 1f;

		// 旋转后的包围盒半宽/半高（贴图必须覆盖旋转后的全部内容）
		float rad = angleDeg * Mathf.Deg2Rad;
		float acs = Mathf.Abs(Mathf.Cos(rad));
		float asn = Mathf.Abs(Mathf.Sin(rad));
		float halfX = Mathf.Max(b.extents.x, 0.0001f);
		float halfY = Mathf.Max(b.extents.y, 0.0001f);
		float halfW = halfX * acs + halfY * asn;
		float halfH = halfX * asn + halfY * acs;

		float w0 = 2f * halfW * ppu;
		float h0 = 2f * halfH * ppu;
		float fit = Mathf.Min(1f, 2048f / Mathf.Max(w0, h0));
		int w = Mathf.Clamp(Mathf.RoundToInt(w0 * fit), 4, 2048);
		int h = Mathf.Clamp(Mathf.RoundToInt(h0 * fit), 4, 2048);

		Texture2D tex = TryRenderIsolatedParts(cam, b, w, h, angleDeg, halfH);
		if (tex == null) tex = TryCaptureFromScreen(cam, b);   // 兜底（会拍到背景，且不含旋转）
		if (tex == null) { Debug.LogWarning("[RunPreview] capture failed (keep old texture)"); return; }

		// 只有成功才替换旧贴图（失败时保留旧贴图，避免幽灵整块消失）
		if (m_runPreviewTex != null) UnityEngine.Object.Destroy(m_runPreviewTex);
		m_runPreviewTex = tex;

		// 预览中心相对“复制锚点”的**世界**偏移与**世界**尺寸：
		// 拖动时用当前相机换算成屏幕偏移/像素尺寸，保证缩放后大小正确（只等比缩放）。
		m_runPreviewWorldOffset = new Vector3(b.center.x, b.center.y, 0f) - new Vector3(m_runCopyAnchor.x, m_runCopyAnchor.y, 0f);
		m_runPreviewWorldSize = new Vector2(2f * halfW, 2f * halfH);
		m_runPreviewCapturePpu = ppu;
		m_runPreviewPixel = new Vector2(tex.width, tex.height);
		// 兜底屏幕偏移（UpdateRunPreview 会用当前相机重算）
		Vector2 bCenterScreen = cam.WorldToScreenPoint(new Vector3(b.center.x, b.center.y, b.center.z));
		Vector2 anchorScreen = cam.WorldToScreenPoint(new Vector3(m_runCopyAnchor.x, m_runCopyAnchor.y, b.center.z));
		m_runPreviewScreenOffset = bCenterScreen - anchorScreen;
		if (m_runPreviewScreenOffset.magnitude > 2000f) m_runPreviewScreenOffset = Vector2.zero;
		Debug.Log($"[RunPreview] captured {tex.width}x{tex.height} angle={angleDeg} b.size={b.size}");
	}

	// 被复制部件（含子物体）的网格/精灵渲染器世界包围盒
	private bool TryGetCopiedPartsBounds(out Bounds b)
	{
		b = new Bounds();
		bool has = false;
		for (int i = 0; i < m_runCopyParts.Count; i++)
		{
			BasePart p = m_runCopyParts[i];
			if (p == null) continue;
			Renderer[] rs = p.GetComponentsInChildren<Renderer>();
			for (int j = 0; j < rs.Length; j++)
			{
				if (rs[j] == null || !rs[j].enabled) continue;
				// 只用网格/精灵渲染器：粒子/线段/拖尾等 bounds 可能极大，会把包围盒撑爆
				if (!(rs[j] is MeshRenderer) && !(rs[j] is SpriteRenderer)) continue;
				if (!has) { b = rs[j].bounds; has = true; }
				else b.Encapsulate(rs[j].bounds);
			}
		}
		return has;
	}

	private static float GetPixelsPerWorldUnit(Camera cam, Bounds b)
	{
		if (cam.orthographic)
			return Screen.height / Mathf.Max(2f * cam.orthographicSize, 0.0001f);
		Vector3 s0 = cam.WorldToScreenPoint(new Vector3(b.center.x, b.center.y, b.center.z));
		Vector3 s1 = cam.WorldToScreenPoint(new Vector3(b.center.x + 1f, b.center.y, b.center.z));
		return Mathf.Abs(s1.x - s0.x);
	}

	// 只渲染被复制部件：临时隐藏其它所有渲染器 + 临时正交相机渲到透明背景 RenderTexture。
	// 同步执行（finally 立即还原），不会造成主画面闪烁。
	private Texture2D TryRenderIsolatedParts(Camera mainCam, Bounds b, int w, int h, float angleDeg, float halfHeightWorld)
	{
		List<Renderer> hidden = new List<Renderer>();
		RenderTexture rt = null;
		GameObject camObj = null;
		Texture2D tex = null;
		try
		{
			// 保留集合：被复制部件及其子物体
			HashSet<Transform> keep = new HashSet<Transform>();
			for (int i = 0; i < m_runCopyParts.Count; i++)
			{
				BasePart p = m_runCopyParts[i];
				if (p == null) continue;
				Transform[] ts = p.GetComponentsInChildren<Transform>(true);
				for (int j = 0; j < ts.Length; j++) if (ts[j] != null) keep.Add(ts[j]);
			}

			Renderer[] all = UnityEngine.Object.FindObjectsOfType<Renderer>();
			for (int i = 0; i < all.Length; i++)
			{
				Renderer r = all[i];
				if (r == null || !r.enabled) continue;
				bool under = false;
				for (Transform t = r.transform; t != null; t = t.parent)
					if (keep.Contains(t)) { under = true; break; }
				if (under) continue;
				r.enabled = false;
				hidden.Add(r);
			}

			rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
			rt.Create();

			camObj = new GameObject("RunPreviewCam");
			Camera tmp = camObj.AddComponent<Camera>();
			tmp.orthographic = true;
			tmp.orthographicSize = Mathf.Max(halfHeightWorld, 0.05f);
			tmp.aspect = (float)w / h;
			tmp.clearFlags = CameraClearFlags.SolidColor;
			tmp.backgroundColor = new Color(0f, 0f, 0f, 0f);
			tmp.cullingMask = ~0;
			tmp.nearClipPlane = 0.05f;
			tmp.farClipPlane = 1000f;
			tmp.allowHDR = false;
			tmp.allowMSAA = false;
			tmp.depth = -100;
			tmp.transform.position = new Vector3(b.center.x, b.center.y, mainCam.transform.position.z - 10f);
			// 绕 Z 轴转 -angle：渲染出的图像即“已按粘贴角旋转”的部件（烘焙旋转）
			tmp.transform.rotation = Quaternion.Euler(0f, 0f, -angleDeg);
			tmp.targetTexture = rt;
			tmp.enabled = false;         // 手动渲染
			tmp.Render();

			RenderTexture prevActive = RenderTexture.active;
			RenderTexture.active = rt;
			tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
			tex.ReadPixels(new Rect(0f, 0f, w, h), 0, 0);
			tex.Apply();
			RenderTexture.active = prevActive;

			// 半透明幽灵：只把“有内容”的像素变半透明，背景(alpha=0)保持全透明
			Color32[] px = tex.GetPixels32();
			for (int i = 0; i < px.Length; i++)
				if (px[i].a > 0) px[i].a = (byte)Mathf.Min(px[i].a, 150);
			tex.SetPixels32(px);
			tex.Apply();
		}
		catch (System.Exception e)
		{
			Debug.LogWarning("[RunPreview] isolated render failed: " + e.Message);
			if (tex != null) { UnityEngine.Object.Destroy(tex); tex = null; }
		}
		finally
		{
			for (int i = 0; i < hidden.Count; i++) if (hidden[i] != null) hidden[i].enabled = true;
			if (camObj != null) UnityEngine.Object.Destroy(camObj);
			if (rt != null) { rt.Release(); UnityEngine.Object.Destroy(rt); }
		}
		return tex;
	}

	// 兜底：从屏幕缓冲截取部件包围盒区域（会拍到背景/其它部件）
	private Texture2D TryCaptureFromScreen(Camera cam, Bounds b)
	{
		try
		{
			Vector3 a = cam.WorldToScreenPoint(new Vector3(b.min.x, b.min.y, 0f));
			Vector3 c2 = cam.WorldToScreenPoint(new Vector3(b.max.x, b.max.y, 0f));
			int x0 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x, c2.x)), 0, Mathf.Max(0, Screen.width - 1));
			int y0 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y, c2.y)), 0, Mathf.Max(0, Screen.height - 1));
			int x1 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x, c2.x)), x0 + 1, Screen.width);
			int y1 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y, c2.y)), y0 + 1, Screen.height);
			int w = x1 - x0;
			int h = y1 - y0;
			Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
			tex.ReadPixels(new Rect(x0, y0, w, h), 0, 0);
			tex.Apply();
			Color32[] px = tex.GetPixels32();
			for (int i = 0; i < px.Length; i++) px[i].a = 128;
			tex.SetPixels32(px);
			tex.Apply();
			return tex;
		}
		catch (System.Exception e)
		{
			Debug.LogWarning("[RunPreview] screen capture fallback failed: " + e.Message);
			return null;
		}
	}

	private void BuildRunPreview()
	{
		ClearRunPreview();
		if (m_runPreviewTex == null) return;
		m_runPreviewLogged = false;
		Debug.Log($"[RunPreview] ready: tex={m_runPreviewTex.width}x{m_runPreviewTex.height} pixel={m_runPreviewPixel} screenOffset={m_runPreviewScreenOffset}");
	}

	// 位置用“指针屏幕坐标 + 按当前相机换算的世界偏移”；大小用**等比缩放**
	// （缩放系数 = 当前每世界单位像素 / 复制时每世界单位像素），只缩放不拉伸。
	private void UpdateRunPreview(Vector2 screenPos)
	{
		if (m_runPreviewTex == null) return;

		Camera cam = Camera.main;
		if (cam != null && m_runPreviewWorldSize.x > 0f && m_runPreviewCapturePpu > 0f)
		{
			Vector3 s0 = cam.WorldToScreenPoint(Vector3.zero);
			Vector3 sOff = cam.WorldToScreenPoint(new Vector3(m_runPreviewWorldOffset.x, m_runPreviewWorldOffset.y, 0f)) - s0;
			Vector3 sW = cam.WorldToScreenPoint(new Vector3(m_runPreviewWorldSize.x, 0f, 0f)) - s0;
			float ppu = Mathf.Abs(sW.x) / m_runPreviewWorldSize.x;   // 当前每世界单位像素
			float scale = ppu / m_runPreviewCapturePpu;              // 等比缩放
			if (scale <= 0f) scale = 1f;
			m_runPreviewScreenOffset = new Vector2(sOff.x, sOff.y);
			// 用贴图自身的像素尺寸等比缩放（保持贴图长宽比，绝不拉伸）
			m_runPreviewPixel = new Vector2(m_runPreviewTex.width, m_runPreviewTex.height) * scale;
		}

		float rad = m_runClipAngle * Mathf.Deg2Rad;
		float cs = Mathf.Cos(rad);
		float sn = Mathf.Sin(rad);
		Vector2 off = m_runPreviewScreenOffset;
		Vector2 roff = new Vector2(off.x * cs - off.y * sn, off.x * sn + off.y * cs);
		m_runPreviewScreenCenter = screenPos + roff;

		if (!m_runPreviewLogged)
		{
			m_runPreviewLogged = true;
			Debug.Log($"[RunPreview] upd screen={screenPos} center={m_runPreviewScreenCenter} offset={off} size={m_runPreviewPixel}");
		}
	}

	// IMGUI 直接画在屏幕上（GUI 层最后绘制，与相机/世界层/深度/自定义背景无关）
	// 注意 GUI 的 Y 轴向下，需要翻转。
	private void OnGUI()
	{
		DrawAdvancedDisplay();
		if (!m_runPasteDragging || m_runPreviewTex == null) return;
		float w = m_runPreviewPixel.x;
		float h = m_runPreviewPixel.y;
		if (w < 1f || h < 1f) return;
		Vector2 center = new Vector2(m_runPreviewScreenCenter.x, Screen.height - m_runPreviewScreenCenter.y);

		Matrix4x4 bak = GUI.matrix;
		// 旋转已**烘焙进贴图**（见 CaptureRunPreviewTexture），这里只做平移绘制，不再旋转，
		// 避免 GUI 空间(y 向下)与屏幕空间(y 向上)的旋转方向歧义。
		GUI.color = new Color(1f, 1f, 1f, 1f);      // 透明度由贴图 alpha 决定
		GUI.DrawTexture(new Rect(center.x - w * 0.5f, center.y - h * 0.5f, w, h), m_runPreviewTex, ScaleMode.StretchToFill, true);
		GUI.color = Color.white;
		GUI.matrix = bak;
	}

	private void ClearRunPreview()
	{
		for (int i = 0; i < m_runPreviewObjs.Count; i++)
			if (m_runPreviewObjs[i] != null) UnityEngine.Object.Destroy(m_runPreviewObjs[i]);
		m_runPreviewObjs.Clear();
		for (int i = 0; i < m_runPreviewMats.Count; i++)
			if (m_runPreviewMats[i] != null) UnityEngine.Object.Destroy(m_runPreviewMats[i]);
		m_runPreviewMats.Clear();
		if (m_runPreviewRoot != null) { UnityEngine.Object.Destroy(m_runPreviewRoot); m_runPreviewRoot = null; }
	}

	private void OnRunInertiaClicked()
	{
		m_runVelocityMode = (m_runVelocityMode + 1) % 3;
	}

	// 运行一个物理帧后仍保持暂停：让"恰好一个固定帧"真实跑过一遍，
	// 这样 MonoBehaviour.FixedUpdate（马达/引擎等）与 ElectricalSystem（电路累加器：
	// m_accumulator += Time.fixedDeltaTime，step = 1/CircuitFrameRate）都会按正常运行推进。
	// 用 Time.captureDeltaTime 把每帧累积的“缩放后时间”钉死为一个 fixedDeltaTime，
	// 避免恢复 timeScale 后同帧"追赶"补跑多个固定帧（之前"时对时多"的原因）。
	private void OnRunStepFrameClicked()
	{
		if (!m_runPaused || m_stepRunning) return;
		StartCoroutine(StepOneFixedFrame());
	}

	private IEnumerator StepOneFixedFrame()
	{
		m_stepRunning = true;
		float resume = m_quickSpeedOn ? GetQuickSpeed() : GetBaseTimeScale();
		if (resume <= 0f) resume = 1f;

		float prevMax = Time.maximumDeltaTime;
		float prevCapture = Time.captureDeltaTime;
		Time.captureDeltaTime = Time.fixedDeltaTime / resume;   // 每帧只累积一个物理步
		Time.maximumDeltaTime = Time.fixedDeltaTime;
		Time.timeScale = resume;

		yield return new WaitForFixedUpdate();   // 真实跑过一个固定帧（含物理与电路累加）

		Time.timeScale = 0f;
		Time.captureDeltaTime = prevCapture;
		Time.maximumDeltaTime = prevMax;
		m_stepRunning = false;
	}

	private void OnRunPasteBeginDrag(BaseEventData data)
	{
		PointerEventData ped = (PointerEventData)data;
		m_runPasteDragging = true;
		m_runPasteDragStartScreen = ped.position;
		// 若角度变化导致贴图过期，这里再请求一次（安全时机生成）；否则直接用现有贴图
		RequestRunPreviewCapture();
		BuildRunPreview();
		UpdateRunPreview(ped.position);
	}

	private void OnRunPasteDrag(BaseEventData data)
	{
		if (!m_runPasteDragging) return;
		PointerEventData ped = (PointerEventData)data;
		UpdateRunPreview(ped.position);
	}

	private void OnRunPasteEndDrag(BaseEventData data)
	{
		if (!m_runPasteDragging) return;
		m_runPasteDragging = false;
		PointerEventData ped = (PointerEventData)data;
		ClearRunPreview();
		if (Vector2.Distance(ped.position, m_runPasteDragStartScreen) <= 30f) return;
		Vector3 w = GetBoxWorldPos(ped.position);
		PasteRunClipboard(w);
	}

	// 复制运行框选中的部件：直接记录部件本体（保留全部信息）+ 相对锚点偏移与旋转
	private void CopyRunSelection()
	{
		m_runCopyParts.Clear();
		m_runCopyOffsets.Clear();
		m_runCopyRots.Clear();
		m_runCopyVel.Clear();
		m_runCopyAngVel.Clear();
		m_runCopyGroup.Clear();
		m_runCopyEnabled.Clear();
		m_runCopyFlipped.Clear();
		m_runCopyParams.Clear();

		Vector3 sum = Vector3.zero;
		int n = 0;
		for (int i = 0; i < m_pausePickedParts.Count; i++)
		{
			BasePart p = m_pausePickedParts[i];
			if (p == null) continue;
			sum += p.transform.position;
			n++;
		}
		if (n == 0) { Debug.Log("[RunClip] copy: no parts"); return; }
		m_runCopyAnchor = sum / n;

		for (int i = 0; i < m_pausePickedParts.Count; i++)
		{
			BasePart p = m_pausePickedParts[i];
			if (p == null) continue;
			m_runCopyParts.Add(p);
			m_runCopyOffsets.Add(p.transform.position - m_runCopyAnchor);
			m_runCopyRots.Add(p.transform.rotation);
			// 记录所属载具分组（区分同一个载具/另一个载具）
			m_runCopyGroup.Add(p.ConnectedComponent);
			// 记录“开关状态”与朝向，粘贴时继承
			m_runCopyEnabled.Add(SafeIsEnabled(p));
			m_runCopyFlipped.Add(p.Flipped);
			m_runCopyParams.Add(p.SerializeCustomParams());
			Rigidbody srcRb = p.GetComponent<Rigidbody>();
			if (srcRb != null && !srcRb.isKinematic)
			{
				m_runCopyVel.Add(srcRb.velocity);
				m_runCopyAngVel.Add(srcRb.angularVelocity);
			}
			else
			{
				m_runCopyVel.Add(Vector3.zero);
				m_runCopyAngVel.Add(Vector3.zero);
			}
		}
		Debug.Log("[RunClip] copied " + m_runCopyParts.Count + " parts");
	}

	// 在指针处粘贴：克隆被框选部件的本体（连同全部组件状态），按生成器方式挂载并连接
	private void PasteRunClipboard(Vector3 worldPos)
	{
		if (m_runCopyParts.Count == 0)
		{
			Debug.Log("[RunClip] nothing to paste");
			return;
		}
		var lm = WPFMonoBehaviour.levelManager;
		var cr = lm != null ? lm.ContraptionRunning : null;
		if (cr == null) return;

		List<BasePart> placedParts = new List<BasePart>();
		Dictionary<BasePart, BasePart> cloneMap = new Dictionary<BasePart, BasePart>();
		Dictionary<Vector2Int, BasePart> localMap = new Dictionary<Vector2Int, BasePart>();
		Dictionary<BasePart, int> placedGroup = new Dictionary<BasePart, int>();
		Dictionary<BasePart, bool> placedEnabled = new Dictionary<BasePart, bool>();

		int placed = PlaceRunObjectsAt(cr, worldPos, Vector3.zero, placedParts, cloneMap, localMap, placedGroup, placedEnabled);

		// 重建同一批内的 enclosure（框/被框）关系
		for (int i = 0; i < placedParts.Count; i++)
		{
			BasePart inst = placedParts[i];
			if (inst == null) continue;
			if (inst.enclosedPart != null && cloneMap.TryGetValue(inst.enclosedPart, out BasePart c1)) inst.enclosedPart = c1;
			if (inst.enclosedInto != null && cloneMap.TryGetValue(inst.enclosedInto, out BasePart c2)) inst.enclosedInto = c2;
		}

		// 脱离网格：按“距离邻近”焊接；但只在**同一原始载具分组**内互焊，
		// 不同分组的部件（不同载具）保持独立，不会焊成一坨。
		WeldPastedParts(cr, placedParts, placedGroup);
		for (int i = 0; i < placedParts.Count; i++)
		{
			CreatePastedRope(cr, localMap, placedParts[i]);
			placedParts[i].CreateCustomJoints();
		}
		// 关节建立后再刷新一次连接贴图（电线等要显示“已连接”状态的外观，而非默认贴图）
		for (int i = 0; i < placedParts.Count; i++)
		{
			BasePart bp = placedParts[i];
			if (bp == null) continue;
			bp.ChangeVisualConnections();
			cr.RefreshNeighboursVisual(bp.m_coordX, bp.m_coordY);
		}
		for (int i = 0; i < placedParts.Count; i++)
		{
			placedParts[i].Initialize();
			placedParts[i].PostInitialize();
		}
		if (placedParts.Count != 0)
		{
			if (INSettings.GetBool(INFeature.FrameJoint) && FrameJointManager.Instance != null)
				FrameJointManager.Instance.AddFrameParts(placedParts);
			if (INSettings.GetBool(INFeature.ElectricalSystem) && ElectricalSystem.Instance != null)
				ElectricalSystem.Instance.InitializeElectricalParts(placedParts);
			for (int i = 0; i < placedParts.Count; i++)
			{
				placedParts[i].ConnectedComponent = -1;
				placedParts[i].StrictConnectedComponent = -1;
			}
		}
		// 继承“开关状态”：粘贴出的部件（开关/引擎/风扇/变速箱/灯等）保持与源部件一致的开/关
		for (int i = 0; i < placedParts.Count; i++)
		{
			BasePart inst = placedParts[i];
			if (inst == null) continue;
			bool want = placedEnabled.TryGetValue(inst, out bool w) && w;
			ApplyInheritedEnabled(inst, want);
		}
		cr.UpdateConnectedComponents();
		Debug.Log("[RunClip] pasted " + placed + "/" + m_runCopyParts.Count + " parts");
	}

	// 以 (worldPos+shift) 作为锚点落点克隆整套部件（脱离网格：按精确世界坐标放置，不吸附格子）
	private int PlaceRunObjectsAt(Contraption cr, Vector3 worldPos, Vector3 shift, List<BasePart> placedParts,
		Dictionary<BasePart, BasePart> cloneMap, Dictionary<Vector2Int, BasePart> localMap, Dictionary<BasePart, int> placedGroup,
		Dictionary<BasePart, bool> placedEnabled)
	{
		int n = m_runCopyParts.Count;
		Vector3 anchor = worldPos + shift;
		// 旋转角不量化：关节用 FixedJoint 锁当前相对姿态，本身不依赖网格
		float clipRad = m_runClipAngle * Mathf.Deg2Rad;
		float clipCs = Mathf.Cos(clipRad);
		float clipSn = Mathf.Sin(clipRad);
		Quaternion clipRot = Quaternion.Euler(0f, 0f, m_runClipAngle);

		int placed = 0;
		for (int i = 0; i < n; i++)
		{
			BasePart src = m_runCopyParts[i];
			if (src == null) continue;

			GameObject obj = UnityEngine.Object.Instantiate(src.gameObject);
			obj.name = src.gameObject.name;
			obj.SetActive(true);
			BasePart inst = obj.GetComponent<BasePart>();
			if (inst == null) { UnityEngine.Object.Destroy(obj); continue; }

			// 克隆体会连带复制原部件的关节（指向原邻居），必须清除
			Joint[] joints = obj.GetComponentsInChildren<Joint>(true);
			for (int j = 0; j < joints.Length; j++)
				if (joints[j] != null) UnityEngine.Object.Destroy(joints[j]);

			Vector3 off = m_runCopyOffsets[i];
			Vector3 roff = new Vector3(off.x * clipCs - off.y * clipSn, off.x * clipSn + off.y * clipCs, off.z);
			Vector3 target = anchor + roff;

			inst.transform.SetParent(cr.transform, true);
			inst.transform.position = new Vector3(target.x, target.y, 0f);
			inst.transform.rotation = clipRot * m_runCopyRots[i];
			// 网格坐标仅作记录（保存/其它系统用），不再用于放置
			Vector3 lpos = cr.transform.InverseTransformPoint(target);
			inst.m_coordX = Mathf.RoundToInt(lpos.x);
			inst.m_coordY = Mathf.RoundToInt(lpos.y);
			inst.contraption = cr;
			if (i < m_runCopyFlipped.Count) inst.m_flipped = m_runCopyFlipped[i];
			inst.PrePlaced();
			if (i < m_runCopyParams.Count && !string.IsNullOrEmpty(m_runCopyParams[i]))
			{
				inst.DeserializeCustomParams(m_runCopyParams[i]);
			}
			inst.gameObject.tag = "Contraption";
			for (int c = 0; c < inst.transform.childCount; c++)
				inst.transform.GetChild(c).gameObject.tag = "Contraption";
			inst.ChangeVisualConnections();
			inst.EnsureRigidbody();
			if (inst.rigidbody != null)
			{
				inst.rigidbody.position = inst.transform.position;
				inst.rigidbody.rotation = inst.transform.rotation;
				if (m_runVelocityMode == 0)
				{
					// 惯性关闭
					inst.rigidbody.velocity = Vector3.zero;
					inst.rigidbody.angularVelocity = Vector3.zero;
				}
				else
				{
					Vector3 v = (i < m_runCopyVel.Count) ? m_runCopyVel[i] : Vector3.zero;
					Vector3 av = (i < m_runCopyAngVel.Count) ? m_runCopyAngVel[i] : Vector3.zero;
					if (m_runVelocityMode == 1)
					{
						// 相对角度：速度随粘贴旋转一起旋转
						v = clipRot * v;
						av = clipRot * av;
					}
					// 绝对角度(mode 2)：原样沿用
					inst.rigidbody.velocity = v;
					inst.rigidbody.angularVelocity = av;
				}
			}
			cr.AddRuntimePart(inst);
			cloneMap[src] = inst;
			localMap[new Vector2Int(inst.m_coordX, inst.m_coordY)] = inst;
			if (i < m_runCopyGroup.Count) placedGroup[inst] = m_runCopyGroup[i];
			if (i < m_runCopyEnabled.Count && placedEnabled != null) placedEnabled[inst] = m_runCopyEnabled[i];
			placedParts.Add(inst);
			placed++;
		}
		return placed;
	}

	// 网格是否已被载具内部件占用（直接扫描，避免依赖可能过期的网格表）
	private static bool CellOccupied(Contraption cr, int x, int y)
	{
		List<BasePart> parts = cr.Parts;
		for (int i = 0; i < parts.Count; i++)
		{
			BasePart p = parts[i];
			if (p != null && p.m_coordX == x && p.m_coordY == y) return true;
		}
		return false;
	}

	// 复用 PartGeneratorManager.CreateJoints 的判定：把 part 与四方向邻居连成一体
	private static void ConnectPastedPart(Contraption cr, Dictionary<Vector2Int, BasePart> localMap, BasePart part)
	{
		if (cr == null || part == null) return;
		int x = part.m_coordX;
		int y = part.m_coordY;

		ConnectOne(cr, part, FindRunNb(cr, localMap, x + 1, y), BasePart.Direction.Right,
			BasePart.JointConnectionDirection.Right, BasePart.JointConnectionDirection.LeftAndRight,
			BasePart.JointConnectionDirection.Left, BasePart.JointConnectionDirection.LeftAndRight);

		ConnectOne(cr, part, FindRunNb(cr, localMap, x, y - 1), BasePart.Direction.Down,
			BasePart.JointConnectionDirection.Down, BasePart.JointConnectionDirection.UpAndDown,
			BasePart.JointConnectionDirection.Up, BasePart.JointConnectionDirection.UpAndDown);

		if (INSettings.GetBool(INFeature.AllDirectionsConnection))
		{
			ConnectOne(cr, part, FindRunNb(cr, localMap, x - 1, y), BasePart.Direction.Left,
				BasePart.JointConnectionDirection.Left, BasePart.JointConnectionDirection.LeftAndRight,
				BasePart.JointConnectionDirection.Right, BasePart.JointConnectionDirection.LeftAndRight);

			ConnectOne(cr, part, FindRunNb(cr, localMap, x, y + 1), BasePart.Direction.Up,
				BasePart.JointConnectionDirection.Up, BasePart.JointConnectionDirection.UpAndDown,
				BasePart.JointConnectionDirection.Down, BasePart.JointConnectionDirection.UpAndDown);
		}
	}

	// 电线/绳子：必须调用 Rope.Create(左, 右) 才会生成“已连接”外观（否则显示默认网格贴图）
	private static void CreatePastedRope(Contraption cr, Dictionary<Vector2Int, BasePart> localMap, BasePart part)
	{
		if (!(part is Rope rope)) return;
		int x = part.m_coordX;
		int y = part.m_coordY;
		BitDirection bd = part.GetCustomJointConnectionDirection().ToBitDirection();
		int dx, dy;
		if (bd != BitDirection.LeftAndRight) { dx = 0; dy = 1; }
		else { dx = -1; dy = 0; }

		BasePart a = FindRunNb(cr, localMap, x + dx, y + dy);
		BasePart b = FindRunNb(cr, localMap, x - dx, y - dy);
		if (a is Rope && a.m_gridRotation != part.m_gridRotation) a = null;
		if (a != null && !(a is Rope) && !(a is Frame) && !(a is Kicker)) a = null;
		if (b is Rope && b.m_gridRotation != part.m_gridRotation) b = null;
		if (b != null && !(b is Rope) && !(b is Frame) && !(b is Kicker)) b = null;
		rope.Create(a, b);
	}

	// 脱离网格制：按距离邻近焊接。规则：
	// ① 新部件二者之间：仅当属于**同一原始载具分组**才焊（不同载具保持独立）；
	// ② 新部件与载具已有部件之间：按距离焊接（便于贴到现有结构上连接成一体）。
	// FixedJoint 锁定“当前相对姿态”，因此任意角度都能连上。
	private void WeldPastedParts(Contraption cr, List<BasePart> placedParts, Dictionary<BasePart, int> placedGroup)
	{
		if (cr == null || placedParts.Count == 0) return;
		List<BasePart> all = cr.Parts;
		const float maxDist = 1.05f;   // 一格间距≈1.0

		// 源部件所属的整个载具（同一连通分量的所有部件）都不参与焊接：
		// 否则焊上去会把仍在运动的原件/其邻居拽回粘贴位置。
		HashSet<int> srcGroups = new HashSet<int>();
		for (int i = 0; i < m_runCopyGroup.Count; i++)
			if (m_runCopyGroup[i] >= 0) srcGroups.Add(m_runCopyGroup[i]);

		for (int i = 0; i < placedParts.Count; i++)
		{
			BasePart a = placedParts[i];
			if (a == null) continue;
			int ia = all.IndexOf(a);
			if (ia < 0) continue;
			Vector3 pa = a.transform.position;
			for (int j = 0; j < ia; j++)
			{
				BasePart b = all[j];
				if (b == null) continue;
				if (m_runCopyParts.Contains(b)) continue;              // 不与源部件本身焊接
				if (b.ConnectedComponent >= 0 && srcGroups.Contains(b.ConnectedComponent)) continue;  // 不与源载具的任何部件焊接
				if (Vector3.Distance(pa, b.transform.position) > maxDist) continue;

				// b 也是本次粘贴的部件 → 必须同组才焊
				if (placedGroup != null && placedGroup.TryGetValue(b, out int gb))
				{
					int ga;
					if (!placedGroup.TryGetValue(a, out ga) || ga != gb) continue;
				}
				cr.AddFixedJoint(a, b);
			}
		}
	}

	// 邻居查找：先查本次粘贴的本地表，再查载具
	private static BasePart FindRunNb(Contraption cr, Dictionary<Vector2Int, BasePart> localMap, int x, int y)
	{
		if (localMap != null && localMap.TryGetValue(new Vector2Int(x, y), out BasePart lp) && lp != null)
			return lp;
		return cr.FindPartAt(x, y);
	}

	private static void ConnectOne(Contraption cr, BasePart part, BasePart nb, BasePart.Direction dir,
		BasePart.JointConnectionDirection selfA, BasePart.JointConnectionDirection selfB,
		BasePart.JointConnectionDirection nbA, BasePart.JointConnectionDirection nbB)
	{
		if (nb == null) return;
		if (!cr.CanConnectTo(part, nb, dir)) return;
		BasePart.JointConnectionDirection sd = part.GetCustomJointConnectionDirection();
		BasePart.JointConnectionDirection nd = nb.GetCustomJointConnectionDirection();
		if (sd == selfA || sd == selfB) cr.AddCustomConnectionBetweenParts(part, nb);
		else if (nd == nbA || nd == nbB) cr.AddCustomConnectionBetweenParts(nb, part);
		else cr.AddFixedJoint(part, nb);
	}

	// 角度旋钮显隐/位置/文案（运行选项菜单可见时显示在第二列顶部）
	private void UpdateAngleKnob()
	{
		if (m_angleKnobObj == null) return;
		bool show = m_isOpen && m_runStateActive && m_runOptionsShown;
		if (m_angleKnobObj.activeSelf != show) m_angleKnobObj.SetActive(show);
		if (!show) return;

		float mainW = m_mainRect.sizeDelta.x;
		float spacing = 6f * m_size;
		float btnW = 48f * m_size;
		float col2X = -(mainW + spacing + btnW + spacing);
		m_angleKnobObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(col2X, 0f);

		// 框选状态(蓝)显示框选部件的角度；常规状态(橙)显示“粘贴等继承类”操作的角度
		bool boxMode = m_pausePickedParts.Count > 0;
		float shown = boxMode ? m_pauseBoxAngle : m_runClipAngle;
		string t = (m_pauseAngleSnap ? Mathf.RoundToInt(shown).ToString() : shown.ToString("F2")) + "\u00b0";
		if (m_angleKnobText != null && m_angleKnobText.text != t) m_angleKnobText.text = t;
		if (m_angleKnobNeedle != null)
		{
			m_angleKnobNeedle.localRotation = Quaternion.Euler(0f, 0f, shown);
			// 框选状态=蓝色（影响框选部件）；常规状态=橙色（影响粘贴等继承类操作）
			Color nc = boxMode ? new Color(0.25f, 0.55f, 1f, 0.95f) : new Color(1f, 0.6f, 0.1f, 0.95f);
			UImage needleImg = m_angleKnobNeedle.GetComponent<UImage>();
			if (needleImg != null && needleImg.color != nc) needleImg.color = nc;
		}
		Color c = m_pauseAngleSnap ? s_speedOnBg : s_normalBg;
		if (m_angleKnobBg != null && m_angleKnobBg.color != c) m_angleKnobBg.color = c;
	}

	private void CapturePauseBoxParts()
	{
		m_pausePickedParts.Clear();
		var lm = WPFMonoBehaviour.levelManager;
		var cr = lm != null ? lm.ContraptionRunning : null;
		if (cr == null || cr.Parts == null) return;

		float minX = Mathf.Min(m_pauseBoxStartWorld.x, m_pauseBoxEndWorld.x);
		float maxX = Mathf.Max(m_pauseBoxStartWorld.x, m_pauseBoxEndWorld.x);
		float minY = Mathf.Min(m_pauseBoxStartWorld.y, m_pauseBoxEndWorld.y);
		float maxY = Mathf.Max(m_pauseBoxStartWorld.y, m_pauseBoxEndWorld.y);

		foreach (BasePart p in cr.Parts)
		{
			if (p == null) continue;
			Vector3 pos = p.transform.position;
			if (pos.x >= minX && pos.x <= maxX && pos.y >= minY && pos.y <= maxY)
				m_pausePickedParts.Add(p);
		}
	}

	private bool IsInsidePauseBox(Vector3 w)
	{
		float minX = Mathf.Min(m_pauseBoxStartWorld.x, m_pauseBoxEndWorld.x);
		float maxX = Mathf.Max(m_pauseBoxStartWorld.x, m_pauseBoxEndWorld.x);
		float minY = Mathf.Min(m_pauseBoxStartWorld.y, m_pauseBoxEndWorld.y);
		float maxY = Mathf.Max(m_pauseBoxStartWorld.y, m_pauseBoxEndWorld.y);
		float cx = (minX + maxX) * 0.5f;
		float cy = (minY + maxY) * 0.5f;
		// 逆旋转回未旋转坐标系再判断
		float rad = -m_pauseBoxAngle * Mathf.Deg2Rad;
		float cs = Mathf.Cos(rad);
		float sn = Mathf.Sin(rad);
		float dx = w.x - cx;
		float dy = w.y - cy;
		float lx = cx + dx * cs - dy * sn;
		float ly = cy + dx * sn + dy * cs;
		return lx >= minX && lx <= maxX && ly >= minY && ly <= maxY;
	}

	private void SetPauseProgress(float p)
	{
		if (m_pauseProgressFill != null) m_pauseProgressFill.fillAmount = p;
	}

	// 供 Filled 类型进度条使用的 1x1 白色 Sprite（无 sprite 时 Filled 不会按比例绘制）
	private static UnityEngine.Sprite s_whiteSprite;
	private static UnityEngine.Sprite WhiteSprite
	{
		get
		{
			if (s_whiteSprite == null)
			{
				Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
				tex.SetPixel(0, 0, Color.white);
				tex.Apply();
				s_whiteSprite = UnityEngine.Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f);
			}
			return s_whiteSprite;
		}
	}

	private void EnsurePauseBox()
	{
		if (m_pauseBoxRoot != null) return;
		Shader sh = Shader.Find("Sprites/Default");
		if (sh == null) sh = Shader.Find("UI/Default");

		m_pauseBoxFillMat = new Material(sh);
		m_pauseBoxFillMat.color = s_boxFillDefault;
		m_pauseBoxEdgeMat = new Material(sh);
		m_pauseBoxEdgeMat.color = s_boxEdgeDefault;

		m_pauseBoxRoot = new GameObject("PauseSelectBox");
		m_pauseBoxFill = CreateBoxQuad("Fill", m_pauseBoxFillMat).transform;
		m_pauseBoxFill.SetParent(m_pauseBoxRoot.transform, false);
		for (int i = 0; i < 4; i++)
		{
			m_pauseBoxEdges[i] = CreateBoxQuad("Edge" + i, m_pauseBoxEdgeMat).transform;
			m_pauseBoxEdges[i].SetParent(m_pauseBoxRoot.transform, false);
		}
		// 轴线：随框体一起旋转，用于显示当前角度方向
		m_pauseBoxRoot.SetActive(false);
	}

	private GameObject CreateBoxQuad(string name, Material mat)
	{
		GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
		go.name = name;
		var col = go.GetComponent<Collider>();
		if (col != null) Destroy(col);
		go.GetComponent<Renderer>().sharedMaterial = mat;
		return go;
	}

	private void UpdatePauseBoxVisual()
	{
		if (m_pauseBoxRoot == null) return;

		// 平滑（不按网格吸附）：世界坐标矩形；整体绕选框中心旋转
		float minX = Mathf.Min(m_pauseBoxStartWorld.x, m_pauseBoxEndWorld.x);
		float maxX = Mathf.Max(m_pauseBoxStartWorld.x, m_pauseBoxEndWorld.x);
		float minY = Mathf.Min(m_pauseBoxStartWorld.y, m_pauseBoxEndWorld.y);
		float maxY = Mathf.Max(m_pauseBoxStartWorld.y, m_pauseBoxEndWorld.y);
		float cx = (minX + maxX) * 0.5f;
		float cy = (minY + maxY) * 0.5f;
		float w = Mathf.Max(maxX - minX, 0.001f);
		float h = Mathf.Max(maxY - minY, 0.001f);

		m_pauseBoxRoot.transform.position = new Vector3(cx, cy, -2f);
		m_pauseBoxRoot.transform.rotation = Quaternion.Euler(0f, 0f, m_pauseBoxAngle);

		m_pauseBoxFill.localPosition = Vector3.zero;
		m_pauseBoxFill.localScale = new Vector3(w, h, 1f);

		float t = 0.12f;
		float hw = w * 0.5f;
		float hh = h * 0.5f;
		m_pauseBoxEdges[0].localPosition = new Vector3(0f, -hh, -0.01f);
		m_pauseBoxEdges[0].localScale = new Vector3(w + t, t, 1f);
		m_pauseBoxEdges[1].localPosition = new Vector3(0f, hh, -0.01f);
		m_pauseBoxEdges[1].localScale = new Vector3(w + t, t, 1f);
		m_pauseBoxEdges[2].localPosition = new Vector3(-hw, 0f, -0.01f);
		m_pauseBoxEdges[2].localScale = new Vector3(t, h + t, 1f);
		m_pauseBoxEdges[3].localPosition = new Vector3(hw, 0f, -0.01f);
		m_pauseBoxEdges[3].localScale = new Vector3(t, h + t, 1f);

		m_pauseBoxRoot.SetActive(true);
	}

	private void ClearPauseBox()
	{
		if (m_pauseBoxRoot != null && m_pauseBoxRoot.activeSelf) m_pauseBoxRoot.SetActive(false);
	}

	// 框内无物体：先变红，再渐变消失
	private void StartPauseBoxBadFade()
	{
		if (m_pauseBoxFillMat != null) m_pauseBoxFillMat.color = s_boxFillBad;
		if (m_pauseBoxEdgeMat != null) m_pauseBoxEdgeMat.color = s_boxEdgeBad;
		m_pauseBoxFade = kPauseBoxFadeTime;
	}

	private void UpdatePauseBoxFade()
	{
		if (m_pauseBoxFade <= 0f) return;
		m_pauseBoxFade -= Time.unscaledDeltaTime;
		float a = Mathf.Clamp01(m_pauseBoxFade / kPauseBoxFadeTime);
		if (m_pauseBoxFillMat != null) m_pauseBoxFillMat.color = new Color(s_boxFillBad.r, s_boxFillBad.g, s_boxFillBad.b, s_boxFillBad.a * a);
		if (m_pauseBoxEdgeMat != null) m_pauseBoxEdgeMat.color = new Color(s_boxEdgeBad.r, s_boxEdgeBad.g, s_boxEdgeBad.b, s_boxEdgeBad.a * a);
		if (m_pauseBoxFade <= 0f)
		{
			ClearPauseBox();
			m_pausePickedParts.Clear();
			m_pauseBoxAngle = 0f;
			if (m_pauseBoxFillMat != null) m_pauseBoxFillMat.color = s_boxFillDefault;
			if (m_pauseBoxEdgeMat != null) m_pauseBoxEdgeMat.color = s_boxEdgeDefault;
		}
	}

	private Vector3 GetBoxWorldPos(Vector3 screenPos)
	{
		Camera cam = Camera.main;
		if (cam == null) return Vector3.zero;
		Vector3 wp = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 10f));
		wp.z = 0f;
		return wp;
	}

	// 菜单收起且长时间无操作时淡出到半透明（15%），任何使用都会立即恢复
	private void UpdateIdleTransparency()
	{
		if (m_canvasGroup == null) return;

		if (m_isOpen)
		{
			m_lastUseTime = Time.unscaledTime;
			if (m_canvasGroup.alpha != 1f) m_canvasGroup.alpha = 1f;
			return;
		}

		float target = (Time.unscaledTime - m_lastUseTime >= kIdleFadeDelay) ? kIdleAlpha : 1f;
		if (!Mathf.Approximately(m_canvasGroup.alpha, target))
			m_canvasGroup.alpha = Mathf.MoveTowards(m_canvasGroup.alpha, target, Time.unscaledDeltaTime * kIdleFadeSpeed);
	}

	private bool IsGameRunning()
	{
		var lm = WPFMonoBehaviour.levelManager;
		if (lm == null) return false;
		return lm.gameState == LevelManager.GameState.Running || lm.gameState == LevelManager.GameState.Continue;
	}

	private bool IsGamePausedRunning()
	{
		var lm = WPFMonoBehaviour.levelManager;
		if (lm == null) return false;
		return lm.gameState == LevelManager.GameState.PausedWhileRunning;
	}

	// 游戏运行时“收起”：整体隐藏辅助菜单
	private void CollapseForRunning()
	{
		m_hiddenBySetting = true;
		m_mainRect.gameObject.SetActive(false);
		if (m_dimLabelObj != null) m_dimLabelObj.SetActive(false);
	}

	// 预留：进入运行态时唤出的调节菜单（后续在此添加内容）
	public void ShowRunAdjustMenu()
	{
	}

	// 按当前模式刷新第一列按钮的文字：运行选项 / 原建造选项
	private void ApplyMenuLabels()
	{
		if (m_childTexts == null) return;
		for (int i = 0; i < m_childTexts.Count; i++)
		{
			if (m_childTexts[i] == null) continue;
			if (m_runStateActive && m_runOptionsShown)
				m_childTexts[i].text = (i < s_runIds.Length) ? L(s_runIds[i]) : "";
			else if (i < s_toolIds.Length)
				m_childTexts[i].text = L(s_toolIds[i]);
		}
		// 离开运行选项时清除暂停/跟随按钮的配色残留
		if (!(m_runStateActive && m_runOptionsShown))
		{
			if (m_childBgs.Count > 0 && m_childBgs[0] != null) m_childBgs[0].color = s_normalBg;
			if (m_childTexts.Count > 0 && m_childTexts[0] != null) m_childTexts[0].color = s_normalText;
			if (m_childBgs.Count > RunFollowModeIndex && m_childBgs[RunFollowModeIndex] != null)
				m_childBgs[RunFollowModeIndex].color = s_normalBg;
			if (m_childTexts.Count > RunFollowModeIndex && m_childTexts[RunFollowModeIndex] != null)
				m_childTexts[RunFollowModeIndex].color = s_normalText;
		}
	}

	// 运行选项：0=暂停/继续, 1=变速, 2=sr重开
	private void OnRunOptionClicked(int index)
	{
		switch (index)
		{
			case 0:
				m_runPaused = !m_runPaused;
				ApplyRunTimeScale();
				break;
			case 1:
				m_quickSpeedOn = !m_quickSpeedOn;
				ApplyRunTimeScale();
				break;
			case 2:
				RestartRun();
				break;
			case RunCycleFollowIndex:
				// 切换到下一个同类型跟随目标
				if (IngameCamera.Instance != null) IngameCamera.Instance.CycleFollowTarget();
				break;
			case RunFollowModeIndex:
				// 默认 → 始终跟随 → 始终不跟随 → 默认
				var ss = INUserSettings.Instance?.StarSeaSettings;
				if (ss != null)
				{
					ss.CameraFollowMode = (ss.CameraFollowMode + 1) % 3;
					INUserSettings.Save();
				}
				break;
		}
	}

	private void RestartRun()
	{
		ResetRunTimeScale();
		// 与 /sr 指令一致的软重启
		INCommandManager.SoftRestart();
	}

	private float GetQuickSpeed()
	{
		float s = INUserSettings.Instance?.StarSeaSettings?.GameRunQuickSpeed ?? 0.2f;
		if (s < 0f) s = 0f;
		else if (s > 10f) s = 10f;
		return s;
	}

	private float GetBaseTimeScale()
	{
		float b = 1f;
		var gs = INUserSettings.Instance?.GeneralSettings;
		if (gs != null) b = gs.TimeScale;
		if (b < 0.01f) b = 1f;
		return b;
	}

	// 暂停/变速都只改时间倍数：暂停=0；变速=星海设置倍率；都关=常规速度
	// 相机是否改用真实时间由 IngameCamera 自行根据倍速与「新型时间倍速」判定
	private void ApplyRunTimeScale()
	{
		float t;
		if (m_runPaused) t = 0f;
		else if (m_quickSpeedOn) t = GetQuickSpeed();
		else t = GetBaseTimeScale();
		Time.timeScale = t;
	}

	private void ResetRunTimeScale()
	{
		m_runPaused = false;
		m_quickSpeedOn = false;
		Time.timeScale = GetBaseTimeScale();
	}

	// 运行选项反馈：暂停/继续文字 + 变速启用时的背景色标记
	private void UpdateRunLabels()
	{
		if (m_childTexts.Count > 0 && m_childTexts[0] != null)
		{
			string t = m_runPaused ? "继续" : "暂停";
			if (m_childTexts[0].text != t) m_childTexts[0].text = t;
		}
		if (m_childTexts.Count > 1 && m_childTexts[1] != null)
		{
			if (m_childTexts[1].text != "变速") m_childTexts[1].text = "变速";
		}
		// 变速启用：绿色背景标记
		if (m_childBgs.Count > 1 && m_childBgs[1] != null)
		{
			Color c = m_quickSpeedOn ? s_speedOnBg : s_normalBg;
			if (m_childBgs[1].color != c) m_childBgs[1].color = c;
		}
		// 暂停中：暂停按钮变白、文字变深
		if (m_childBgs.Count > 0 && m_childBgs[0] != null)
		{
			Color c = m_runPaused ? s_pausedBg : s_normalBg;
			if (m_childBgs[0].color != c) m_childBgs[0].color = c;
			if (m_childTexts.Count > 0 && m_childTexts[0] != null)
			{
				Color tc = m_runPaused ? s_pausedText : s_normalText;
				if (m_childTexts[0].color != tc) m_childTexts[0].color = tc;
			}
		}
		// 跟随设置：默认/始终跟随/始终不跟随；默认时若当前不跟随也显示（按钮变白）
		if (m_childBgs.Count > RunFollowModeIndex && m_childBgs[RunFollowModeIndex] != null)
		{
			int mode = INUserSettings.Instance?.StarSeaSettings?.CameraFollowMode ?? 0;
			bool free = IngameCamera.Instance != null && IngameCamera.Instance.IsFreeCamera;

			string t;
			Color c;
			Color tc = s_normalText;
			if (mode == 1) { t = "跟随"; c = s_speedOnBg; }
			else if (mode == 2) { t = "自由"; c = s_pausedBg; tc = s_pausedText; }
			else { t = "默认"; c = free ? s_pausedBg : s_normalBg; if (free) tc = s_pausedText; }

			if (m_childTexts[RunFollowModeIndex] != null && m_childTexts[RunFollowModeIndex].text != t)
				m_childTexts[RunFollowModeIndex].text = t;
			if (m_childBgs[RunFollowModeIndex].color != c) m_childBgs[RunFollowModeIndex].color = c;
			if (m_childTexts[RunFollowModeIndex] != null && m_childTexts[RunFollowModeIndex].color != tc)
				m_childTexts[RunFollowModeIndex].color = tc;
		}
	}

	// 隐藏仅属于建造模式的内容：多余列1按钮 + 第二列（删除/覆盖/旋转/材质/形状）
	private void HideBuildOnlyWidgets()
	{
		for (int i = s_runIds.Length; i < m_childRects.Count; i++)
			if (m_childRects[i] != null && m_childRects[i].gameObject.activeSelf)
				m_childRects[i].gameObject.SetActive(false);

		GameObject[] col2 = { m_deleteBtnObj, m_overwriteObj, m_rotateObj, m_materialObj, m_shapeTypeObj, m_shapeSolidObj };
		for (int i = 0; i < col2.Length; i++)
			if (col2[i] != null && col2[i].activeSelf) col2[i].SetActive(false);
	}

	// 由运行选项切回建造选项时，恢复被隐藏的建造内容（剪切板由 RefreshClipboardSlots 恢复）
	private void RestoreBuildOnlyWidgets()
	{
		// 清除运行项残留配色（变速启用绿），与菜单开关状态无关
		if (m_childBgs.Count > 1 && m_childBgs[1] != null) m_childBgs[1].color = s_normalBg;
		if (!m_isOpen) return;
		for (int i = s_runIds.Length; i < m_childRects.Count; i++)
			if (m_childRects[i] != null) m_childRects[i].gameObject.SetActive(true);

		GameObject[] col2 = { m_deleteBtnObj, m_overwriteObj, m_rotateObj, m_materialObj, m_shapeTypeObj, m_shapeSolidObj };
		for (int i = 0; i < col2.Length; i++)
			if (col2[i] != null) col2[i].SetActive(true);
	}

	private void ResetUndoColor()
	{
		if (m_undoBg == null) return;
		int undoIdx = ToolUndo;
		if (undoIdx >= 0 && undoIdx < m_childBgs.Count)
		{
			Color expected = (undoIdx == m_activeChildIndex) ? s_activeBg : s_normalBg;
			if (m_undoBg.color != expected) m_undoBg.color = expected;
		}
	}

	private bool IsOverUndoButton()
	{
		if (m_undoRect == null) return false;
		return IsOverRect(m_undoRect);
	}

	private bool IsOverDeleteButton()
	{
		if (m_deleteRect == null || m_deleteBtnObj == null || !m_deleteBtnObj.activeSelf) return false;
		return IsOverRect(m_deleteRect);
	}

	private bool IsOverRect(RectTransform rt)
	{
		Vector2 mousePos = Input.mousePosition;
		Vector3[] corners = new Vector3[4];
		rt.GetWorldCorners(corners);
		return mousePos.x >= corners[0].x && mousePos.x <= corners[2].x &&
			   mousePos.y >= corners[0].y && mousePos.y <= corners[2].y;
	}

	// 指针是否落在辅助菜单的任意可见 UI 上（不依赖 EventSystem，本项目存在多个 EventSystem）
	private bool IsPointerOverToolBook()
	{
		if (m_mainRect != null && m_mainRect.gameObject.activeInHierarchy && IsOverRect(m_mainRect)) return true;
		for (int i = 0; i < m_childRects.Count; i++)
		{
			RectTransform rt = m_childRects[i];
			if (rt != null && rt.gameObject.activeInHierarchy && IsOverRect(rt)) return true;
		}
		for (int i = 0; i < m_clipSlotRects.Count; i++)
		{
			RectTransform rt = m_clipSlotRects[i];
			if (rt != null && rt.gameObject.activeInHierarchy && IsOverRect(rt)) return true;
		}
		GameObject[] extras = { m_deleteBtnObj, m_overwriteObj, m_rotateObj, m_materialObj, m_shapeTypeObj, m_shapeSolidObj, m_angleKnobObj, m_runCopyObj, m_runPasteObj, m_runInertiaObj, m_runStepObj, m_runArchiveSaveObj, m_runDeleteObj, m_confirmObj, m_cancelObj };
		for (int i = 0; i < extras.Length; i++)
		{
			GameObject o = extras[i];
			if (o == null || !o.activeInHierarchy) continue;
			RectTransform rt = o.GetComponent<RectTransform>();
			if (rt != null && IsOverRect(rt)) return true;
		}
		if (IsPointerOverAdvUi()) return true;
		return false;
	}

	private void ApplySettings()
	{
		var settings = INUserSettings.Instance?.StarSeaSettings;
		if (settings == null) return;
		m_size = settings.ToolBookSize;
		if (m_size < 0.1f) m_size = 1f;
	}

	// --- Create UI ---

	private void CreateMainButton()
	{
		GameObject mainBtn = new GameObject("MainToggle");
		mainBtn.transform.SetParent(transform, false);

		float sz = 48f * m_size;
		m_mainRect = mainBtn.AddComponent<RectTransform>();
		m_mainRect.anchorMin = new Vector2(1f, 1f);
		m_mainRect.anchorMax = new Vector2(1f, 1f);
		m_mainRect.pivot = new Vector2(1f, 1f);
		m_mainRect.sizeDelta = new Vector2(sz, sz);

		UImage bg = mainBtn.AddComponent<UImage>();
		bg.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

		UButton btn = mainBtn.AddComponent<UButton>();
		btn.targetGraphic = bg;
		btn.onClick.AddListener(OnToggleClicked);

		EventTrigger trigger = mainBtn.AddComponent<EventTrigger>();
		AddEventTrigger(trigger, EventTriggerType.BeginDrag, OnBeginDrag);
		AddEventTrigger(trigger, EventTriggerType.Drag, OnDrag);
		AddEventTrigger(trigger, EventTriggerType.EndDrag, OnEndDrag);

		CreateLabel(mainBtn, "\u2261", 32, Color.white);
	}

	private void CreateChildButtons()
	{
		m_childRects.Clear();
		m_childBgs.Clear();
		m_childTexts.Clear();

		for (int i = 0; i < s_toolIds.Length; i++)
		{
			// The object is named after the id rather than after the label, so switching language does not
			// rename objects and the hierarchy reads the same either way.
			GameObject btn = CreateButtonObj(s_toolIds[i].Substring("ToolBook_".Length), m_mainRect.transform);
			RectTransform rect = btn.GetComponent<RectTransform>();
			UImage bg = btn.GetComponent<UImage>();

			m_childBgs.Add(bg);

			// 第 0 个按钮（运行态的“暂停”）叠加一个 0.25 秒填充进度条
			if (i == 0)
			{
				GameObject fillObj = new GameObject("PauseProgress");
				fillObj.transform.SetParent(btn.transform, false);
				RectTransform fillRect = fillObj.AddComponent<RectTransform>();
				fillRect.anchorMin = Vector2.zero;
				fillRect.anchorMax = Vector2.one;
				fillRect.sizeDelta = Vector2.zero;
				m_pauseProgressFill = fillObj.AddComponent<UImage>();
				m_pauseProgressFill.sprite = WhiteSprite;
				m_pauseProgressFill.color = new Color(1f, 0.85f, 0.2f, 0.9f);
				m_pauseProgressFill.type = UImage.Type.Filled;
				m_pauseProgressFill.fillMethod = UImage.FillMethod.Horizontal;
				m_pauseProgressFill.fillOrigin = 0;
				m_pauseProgressFill.fillAmount = 0f;
				m_pauseProgressFill.raycastTarget = false;
			}

			UButton b = btn.GetComponent<UButton>();
			int idx = i;

			// 所有子按钮统一挂点击（运行态“变速”等需要 onClick；建造态“粘贴”该项无 case，点击无副作用）
			b.onClick.AddListener(() => OnChildClicked(idx));

			// “粘贴”额外支持拖拽（拖拽事件在运行选项态会被忽略）
			if (i == ToolPaste)
			{
				EventTrigger trigger = btn.AddComponent<EventTrigger>();
				AddEventTrigger(trigger, EventTriggerType.BeginDrag, OnPasteBeginDrag);
				AddEventTrigger(trigger, EventTriggerType.Drag, OnPasteDrag);
				AddEventTrigger(trigger, EventTriggerType.EndDrag, OnPasteEndDrag);
			}

			UText t = CreateLabel(btn, L(s_toolIds[i]), 16, Color.white);
			m_childTexts.Add(t);

			btn.SetActive(false);
			m_childRects.Add(rect);

			if (i == ToolUndo)
			{
				m_undoRect = rect;
				m_undoBg = bg;
			}
		}
	}

	private void CreateColumn2Buttons()
	{
		// Delete
		m_deleteBtnObj = CreateButtonObj("Delete", m_mainRect.transform);
		m_deleteRect = m_deleteBtnObj.GetComponent<RectTransform>();
		m_deleteBg = m_deleteBtnObj.GetComponent<UImage>();
		m_deleteBg.color = new Color(0.8f, 0.1f, 0.1f, 0.9f);
		m_deleteBtnObj.GetComponent<UButton>().onClick.AddListener(OnDeleteClicked);
		m_deleteText = CreateLabel(m_deleteBtnObj, L("ToolBook_Delete"), 16, Color.white);
		m_deleteBtnObj.SetActive(false);

		// Overwrite
		m_overwriteObj = CreateButtonObj("Overwrite", m_mainRect.transform);
		m_overwriteBg = m_overwriteObj.GetComponent<UImage>();
		m_overwriteBg.color = s_normalBg;
		m_overwriteObj.GetComponent<UButton>().onClick.AddListener(OnOverwriteToggle);
		m_overwriteText = CreateLabel(m_overwriteObj, L("ToolBook_Overwrite"), 12, Color.white);
		m_overwriteObj.SetActive(false);

		// Rotate
		m_rotateObj = CreateButtonObj("Rotate", m_mainRect.transform);
		m_rotateObj.GetComponent<UImage>().color = s_normalBg;
		m_rotateObj.GetComponent<UButton>().onClick.AddListener(OnRotateClicked);
		m_rotateText = CreateLabel(m_rotateObj, RotateTitle(0) + s_rotateArrows[0], 12, Color.white);
		m_rotateObj.SetActive(false);

		// Material
		m_materialObj = CreateButtonObj("Material", m_mainRect.transform);
		m_materialBg = m_materialObj.GetComponent<UImage>();
		m_materialBg.color = s_normalBg;
		m_materialObj.GetComponent<UButton>().onClick.AddListener(OnMaterialToggle);
		m_materialText = CreateLabel(m_materialObj, L("ToolBook_Material"), 12, Color.white);
		m_materialObj.SetActive(false);

		// Shape type selector
		m_shapeTypeObj = CreateButtonObj("ShapeType", m_mainRect.transform);
		m_shapeTypeBg = m_shapeTypeObj.GetComponent<UImage>();
		m_shapeTypeBg.color = s_normalBg;
		m_shapeTypeObj.GetComponent<UButton>().onClick.AddListener(OnShapeTypeCycle);
		m_shapeTypeText = CreateLabel(m_shapeTypeObj, L(s_shapeIds[0]), 12, Color.white);
		m_shapeTypeObj.SetActive(false);

		// Shape solid/hollow toggle
		m_shapeSolidObj = CreateButtonObj("ShapeSolid", m_mainRect.transform);
		m_shapeSolidBg = m_shapeSolidObj.GetComponent<UImage>();
		m_shapeSolidBg.color = s_normalBg;
		m_shapeSolidObj.GetComponent<UButton>().onClick.AddListener(OnShapeSolidToggle);
		m_shapeSolidText = CreateLabel(m_shapeSolidObj, L("ToolBook_Hollow"), 12, Color.white);
		m_shapeSolidObj.SetActive(false);

		// 角度旋钮（第二列，仅暂停时显示）：按住拖动=旋转；单击=开关角度对齐
		m_angleKnobObj = CreateButtonObj("AngleKnob", m_mainRect.transform);
		m_angleKnobBg = m_angleKnobObj.GetComponent<UImage>();
		m_angleKnobBg.color = s_normalBg;
		EventTrigger knobTrigger = m_angleKnobObj.AddComponent<EventTrigger>();
		AddEventTrigger(knobTrigger, EventTriggerType.PointerDown, OnAngleKnobPointerDown);
		AddEventTrigger(knobTrigger, EventTriggerType.Drag, OnAngleKnobDrag);
		AddEventTrigger(knobTrigger, EventTriggerType.PointerUp, OnAngleKnobPointerUp);

		// 旋钮上的方向指针（随机旋角度旋转，显示轴向）
		GameObject needle = new GameObject("Needle");
		needle.transform.SetParent(m_angleKnobObj.transform, false);
		m_angleKnobNeedle = needle.AddComponent<RectTransform>();
		m_angleKnobNeedle.anchorMin = new Vector2(0.5f, 0.5f);
		m_angleKnobNeedle.anchorMax = new Vector2(0.5f, 0.5f);
		m_angleKnobNeedle.pivot = new Vector2(0.5f, 0.5f);
		m_angleKnobNeedle.sizeDelta = new Vector2(44f * m_size, 3f * m_size);
		m_angleKnobNeedle.anchoredPosition = Vector2.zero;
		UImage needleImg = needle.AddComponent<UImage>();
		needleImg.sprite = WhiteSprite;
		needleImg.color = new Color(1f, 0.6f, 0.1f, 0.95f);
		needleImg.raycastTarget = false;

		m_angleKnobText = CreateLabel(m_angleKnobObj, "0\u00b0", 13, Color.white);
		m_angleKnobObj.SetActive(false);

		// 运行框选：复制 / 粘贴 / 惯性（仅暂停时显示）
		m_runCopyObj = CreateButtonObj("RunCopy", m_mainRect.transform);
		m_runCopyObj.GetComponent<UImage>().color = s_normalBg;
		m_runCopyObj.GetComponent<UButton>().onClick.AddListener(OnRunCopyClicked);
		CreateLabel(m_runCopyObj, "复制", 12, Color.white);
		m_runCopyObj.SetActive(false);

		m_runPasteObj = CreateButtonObj("RunPaste", m_mainRect.transform);
		m_runPasteObj.GetComponent<UImage>().color = s_normalBg;
		// 拖出到世界里松手 → 在指针处粘贴
		EventTrigger pasteTrigger = m_runPasteObj.AddComponent<EventTrigger>();
		AddEventTrigger(pasteTrigger, EventTriggerType.BeginDrag, OnRunPasteBeginDrag);
		AddEventTrigger(pasteTrigger, EventTriggerType.Drag, OnRunPasteDrag);
		AddEventTrigger(pasteTrigger, EventTriggerType.EndDrag, OnRunPasteEndDrag);
		CreateLabel(m_runPasteObj, "粘贴", 12, Color.white);
		m_runPasteObj.SetActive(false);

		m_runInertiaObj = CreateButtonObj("RunInertia", m_mainRect.transform);
		m_runInertiaBg = m_runInertiaObj.GetComponent<UImage>();
		m_runInertiaBg.color = s_normalBg;
		m_runInertiaObj.GetComponent<UButton>().onClick.AddListener(OnRunInertiaClicked);
		m_runInertiaText = CreateLabel(m_runInertiaObj, "惯性", 12, Color.white);
		m_runInertiaObj.SetActive(false);

		// 单帧：暂停时跑一个物理帧后重新暂停
		m_runStepObj = CreateButtonObj("RunStep", m_mainRect.transform);
		m_runStepObj.GetComponent<UImage>().color = s_normalBg;
		m_runStepObj.GetComponent<UButton>().onClick.AddListener(OnRunStepFrameClicked);
		CreateLabel(m_runStepObj, "单帧", 12, Color.white);
		m_runStepObj.SetActive(false);
	}

	private void CreateClipboardSlots()
	{
		int max = GetClipboardMax();
		for (int i = 0; i < max; i++)
			CreateClipSlotObj(i);
		CreatePaginationUI();
		CreateRenameUI();
	}

	private int GetClipboardMax()
	{
		int max = 3;
		if (INUserSettings.Instance?.StarSeaSettings != null)
			max = INUserSettings.Instance.StarSeaSettings.ToolBookClipboardMax;
		return max < 1 ? 3 : max;
	}

	private int m_lastClipPageSize = -1;

	private void SyncClipboardSlotCount()
	{
		int desired = GetClipboardMax();
		if (desired == m_lastClipPageSize) return;
		m_lastClipPageSize = desired;

		// Rebuild display slots to match new page size
		foreach (var obj in m_clipSlotObjs)
			if (obj != null) Destroy(obj);
		m_clipSlotObjs.Clear();
		m_clipSlotRects.Clear();
		m_clipSlotBgs.Clear();
		m_clipSlotTexts.Clear();

		for (int i = 0; i < desired; i++)
			CreateClipSlotObj(i);
	}

	private void CreateClipSlotObj(int idx)
	{
		float w = 48f * m_size * LabelWidth;
		float h = 36f * m_size;

		GameObject slotBtn = new GameObject("Clip" + (idx + 1));
		slotBtn.transform.SetParent(m_mainRect.transform, false);
		RectTransform rect = slotBtn.AddComponent<RectTransform>();
		rect.anchorMin = new Vector2(1f, 1f);
		rect.anchorMax = new Vector2(1f, 1f);
		rect.pivot = new Vector2(1f, 1f);
		rect.sizeDelta = new Vector2(w, h);

		UImage bg = slotBtn.AddComponent<UImage>();
		bg.color = s_clipEmptyBg;
		m_clipSlotBgs.Add(bg);

		// No UButton - use EventTrigger only to avoid click/drag conflict
		EventTrigger trigger = slotBtn.AddComponent<EventTrigger>();
		int slotIdx = idx;
		AddEventTrigger(trigger, EventTriggerType.PointerClick, (data) => OnClipSlotClicked(data, slotIdx));
		AddEventTrigger(trigger, EventTriggerType.BeginDrag, (data) => OnClipSlotBeginDrag(data, slotIdx));
		AddEventTrigger(trigger, EventTriggerType.Drag, OnClipSlotDrag);
		AddEventTrigger(trigger, EventTriggerType.EndDrag, OnClipSlotEndDrag);

		UText t = CreateLabel(slotBtn, "-", 16, Color.white);
		m_clipSlotTexts.Add(t);

		slotBtn.SetActive(false);
		m_clipSlotObjs.Add(slotBtn);
		m_clipSlotRects.Add(rect);
	}

	private void CreateDimLabel()
	{
		m_dimLabelObj = new GameObject("DimLabel");
		m_dimLabelObj.transform.SetParent(transform, false);
		RectTransform rt = m_dimLabelObj.AddComponent<RectTransform>();
		rt.anchorMin = new Vector2(0.25f, 1f);
		rt.anchorMax = new Vector2(0.25f, 1f);
		rt.pivot = new Vector2(0.5f, 1f);
		rt.sizeDelta = new Vector2(400f, 50f);
		rt.anchoredPosition = new Vector2(0f, -10f);
		m_dimLabel = CreateLabel(m_dimLabelObj, "", 28, new Color(1f, 0.9f, 0.4f, 1f));
		m_dimLabel.alignment = TextAnchor.MiddleCenter;
		m_dimLabelObj.SetActive(false);
	}

	private void CreatePaginationUI()
	{
		float halfW = 24f * m_size;
		float btnH = 36f * m_size;

		// Page Up button
		m_clipPageUpObj = new GameObject("ClipPageUp");
		m_clipPageUpObj.transform.SetParent(m_mainRect.transform, false);
		RectTransform upRect = m_clipPageUpObj.AddComponent<RectTransform>();
		upRect.anchorMin = new Vector2(1f, 1f);
		upRect.anchorMax = new Vector2(1f, 1f);
		upRect.pivot = new Vector2(1f, 1f);
		upRect.sizeDelta = new Vector2(halfW, btnH);
		UImage upBg = m_clipPageUpObj.AddComponent<UImage>();
		upBg.color = s_pageBtnBg;
		UButton upBtn = m_clipPageUpObj.AddComponent<UButton>();
		upBtn.targetGraphic = upBg;
		upBtn.onClick.AddListener(OnClipPageUp);
		CreateLabel(m_clipPageUpObj, "\u25b2", 18, Color.white);
		m_clipPageUpObj.SetActive(false);

		// Page Down button
		m_clipPageDownObj = new GameObject("ClipPageDown");
		m_clipPageDownObj.transform.SetParent(m_mainRect.transform, false);
		RectTransform downRect = m_clipPageDownObj.AddComponent<RectTransform>();
		downRect.anchorMin = new Vector2(1f, 1f);
		downRect.anchorMax = new Vector2(1f, 1f);
		downRect.pivot = new Vector2(1f, 1f);
		downRect.sizeDelta = new Vector2(halfW, btnH);
		UImage downBg = m_clipPageDownObj.AddComponent<UImage>();
		downBg.color = s_pageBtnBg;
		UButton downBtn = m_clipPageDownObj.AddComponent<UButton>();
		downBtn.targetGraphic = downBg;
		downBtn.onClick.AddListener(OnClipPageDown);
		CreateLabel(m_clipPageDownObj, "\u25bc", 18, Color.white);
		m_clipPageDownObj.SetActive(false);

		// Page indicator text
		m_clipPageTextObj = new GameObject("ClipPageText");
		m_clipPageTextObj.transform.SetParent(m_mainRect.transform, false);
		RectTransform pageTxtRect = m_clipPageTextObj.AddComponent<RectTransform>();
		pageTxtRect.anchorMin = new Vector2(1f, 1f);
		pageTxtRect.anchorMax = new Vector2(1f, 1f);
		pageTxtRect.pivot = new Vector2(1f, 1f);
		pageTxtRect.sizeDelta = new Vector2(72f * m_size, 28f * m_size);
		m_clipPageText = CreateLabel(m_clipPageTextObj, "0/0", 18, new Color(0.8f, 0.8f, 0.8f, 1f));
		m_clipPageTextObj.SetActive(false);
	}

	// --- Helpers ---

	private GameObject CreateButtonObj(string name, Transform parent)
	{
		float w = 48f * m_size * LabelWidth;
		float h = 36f * m_size;
		GameObject btn = new GameObject(name);
		btn.transform.SetParent(parent, false);
		RectTransform rect = btn.AddComponent<RectTransform>();
		rect.anchorMin = new Vector2(1f, 1f);
		rect.anchorMax = new Vector2(1f, 1f);
		rect.pivot = new Vector2(1f, 1f);
		rect.sizeDelta = new Vector2(w, h);
		rect.anchoredPosition = Vector2.zero;
		UImage bg = btn.AddComponent<UImage>();
		bg.color = s_normalBg;
		UButton b = btn.AddComponent<UButton>();
		b.targetGraphic = bg;
		return btn;
	}

	private UText CreateLabel(GameObject parent, string text, int fontSize, Color color)
	{
		GameObject txt = new GameObject("Text");
		txt.transform.SetParent(parent.transform, false);
		RectTransform txtRect = txt.AddComponent<RectTransform>();
		txtRect.anchorMin = Vector2.zero;
		txtRect.anchorMax = Vector2.one;
		txtRect.sizeDelta = Vector2.zero;
		UText t = txt.AddComponent<UText>();
		t.text = text;
		t.font = INUnity.ArialFont;
		t.fontSize = Mathf.RoundToInt(fontSize * m_size);
		t.alignment = TextAnchor.MiddleCenter;
		t.color = color;
		return t;
	}

	private void AddEventTrigger(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> callback)
	{
		var entry = new EventTrigger.Entry { eventID = type };
		entry.callback.AddListener(callback);
		trigger.triggers.Add(entry);
	}

	private void RefreshClipboardSlots()
	{
		if (!m_isOpen) return;

		// 运行态显示全新选项时：第三列改为显示“运行存档”槽位
		if (m_runStateActive && m_runOptionsShown)
		{
			RefreshRunArchiveSlots();
			return;
		}

		var sm = INSelectionManager.Instance;
		if (sm == null) return;

		var slots = sm.ClipboardSlots;
		var slotIds = sm.ClipboardSlotIds;
		var slotNames = sm.ClipboardSlotNames;
		int totalData = slots.Count;
		int pageSize = m_clipSlotObjs.Count;

		// Clamp scroll offset
		int maxOffset = Mathf.Max(0, totalData - pageSize);
		if (m_clipScrollOffset > maxOffset) m_clipScrollOffset = maxOffset;
		if (m_clipScrollOffset < 0) m_clipScrollOffset = 0;

		// Auto-jump to last page after a new save
		if (INSelectionManager.NewSavePending)
		{
			INSelectionManager.NewSavePending = false;
			m_clipScrollOffset = maxOffset;
		}

		// Map display slots to data via offset
		for (int i = 0; i < pageSize; i++)
		{
			int dataIdx = m_clipScrollOffset + i;
			if (dataIdx < totalData)
			{
				m_clipSlotObjs[i].SetActive(true);
				m_clipSlotBgs[i].color = s_clipFilledBg;
				if (dataIdx < slotNames.Count && !string.IsNullOrEmpty(slotNames[dataIdx]))
					m_clipSlotTexts[i].text = slotNames[dataIdx];
				else if (dataIdx < slotIds.Count)
					m_clipSlotTexts[i].text = slotIds[dataIdx].ToString();
				else
					m_clipSlotTexts[i].text = "-";
			}
			else
			{
				m_clipSlotBgs[i].color = s_clipEmptyBg;
				m_clipSlotTexts[i].text = "-";
			}
		}

		// Column 3 layout: page controls at TOP, slots below
		float spacing = 6f * m_size;
		float btnH = 36f * m_size;
		float mainW = m_mainRect.sizeDelta.x;
		float btnW = 48f * m_size * LabelWidth;
		float col3X = -(mainW + spacing + btnW + spacing + btnW + spacing);
		float halfW = 24f * m_size;
		float scrollX = col3X - btnW;

		bool showPage = totalData > pageSize;
		float pageOffset = showPage ? (btnH + spacing) : 0f;

		// Page controls at top
		if (m_clipPageUpObj != null)
		{
			m_clipPageUpObj.SetActive(showPage);
			m_clipPageUpObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(scrollX, 0f);
		}
		if (m_clipPageDownObj != null)
		{
			m_clipPageDownObj.SetActive(showPage);
			m_clipPageDownObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(scrollX, -(btnH + spacing));
		}
		if (m_clipPageTextObj != null && m_clipPageText != null)
		{
			m_clipPageTextObj.SetActive(showPage);
			if (showPage)
			{
				int first = m_clipScrollOffset + 1;
				int last = Mathf.Min(m_clipScrollOffset + pageSize, totalData);
				m_clipPageText.text = $"{first}-{last}/{totalData}";
				m_clipPageTextObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(scrollX, -(2 * btnH + 2 * spacing));
			}
		}

		// Slots below page controls
		for (int i = 0; i < pageSize; i++)
		{
			if (m_clipSlotObjs[i].activeSelf)
				m_clipSlotRects[i].anchoredPosition = new Vector2(col3X, -(pageOffset + i * (btnH + spacing)));
		}
	}

	private void ApplyPosition()
	{
		var settings = INUserSettings.Instance?.StarSeaSettings;
		if (settings == null) return;
		// Fix stale default (-60,-60) to correct position
		if (settings.ToolBookPosX == -60f && settings.ToolBookPosY == -60f)
		{
			settings.ToolBookPosX = -360f;
			settings.ToolBookPosY = -60f;
		}
		m_mainRect.anchoredPosition = new Vector2(settings.ToolBookPosX, settings.ToolBookPosY);
	}

	private void SavePosition()
	{
		var settings = INUserSettings.Instance?.StarSeaSettings;
		if (settings == null) return;
		settings.ToolBookPosX = m_mainRect.anchoredPosition.x;
		settings.ToolBookPosY = m_mainRect.anchoredPosition.y;
		INUserSettings.Save();
	}

	// --- Button handlers ---

	private void OnToggleClicked()
	{
		if (m_isDragging || m_coroutineRunning) return;
		Toggle();
	}

	private void OnChildClicked(int index)
	{
		// 运行态且显示全新选项时，按运行选项处理
		if (m_runStateActive && m_runOptionsShown)
		{
			if (index == RunSwitchIndex)
			{
				// “切换”：回到原建造选项
				m_runOptionsShown = false;
				ResetRunTimeScale();
				ApplyMenuLabels();
				RestoreBuildOnlyWidgets();
			}
			else
			{
				OnRunOptionClicked(index);
			}
			return;
		}
		var sm = INSelectionManager.Instance;
		if (sm == null) return;

		switch (index)
		{
			case ToolCopy: EventManager.Send(new UIEvent(UIEvent.Type.ToolBookCopy)); break;
			case ToolSelect:
				if (sm.CurrentState == INSelectionManager.State.Idle) sm.EnterSelectionMode();
				else sm.ExitSelectionMode();
				break;
			case ToolUndo: sm.Undo(); break;
			case ToolStash: sm.StashContraption(); break;
			case ToolCut: sm.CutSelected(); break;
			case ToolSave: EventManager.Send(new UIEvent(UIEvent.Type.ToolBookSaveClipboard)); break;
			case ToolShape: sm.EnterShapeDrawing(); break;
			case ToolFill: sm.FillWithSelectedPart(); break;
		}
	}

	// --- Scroll handlers ---

	private void OnClipPageUp()
	{
		if (m_runStateActive && m_runOptionsShown) { OnRunArchivePageUp(); return; }
		int pageSize = m_clipSlotObjs.Count;
		m_clipScrollOffset = Mathf.Max(0, m_clipScrollOffset - pageSize);
	}

	private void OnClipPageDown()
	{
		if (m_runStateActive && m_runOptionsShown) { OnRunArchivePageDown(); return; }
		var sm = INSelectionManager.Instance;
		if (sm == null) return;
		int pageSize = m_clipSlotObjs.Count;
		int maxOffset = Mathf.Max(0, sm.ClipboardSlots.Count - pageSize);
		m_clipScrollOffset = Mathf.Min(maxOffset, m_clipScrollOffset + pageSize);
	}

	private void OnOverwriteToggle()
	{
		var settings = INUserSettings.Instance?.StarSeaSettings;
		if (settings == null) return;
		settings.ToolBookPasteOverwriteMode = (settings.ToolBookPasteOverwriteMode + 1) % 3;
		INUserSettings.Save();
	}

	private void OnRotateClicked()
	{
		if (INSelectionManager.Instance != null)
			INSelectionManager.Instance.RotateSelection();
	}

	private void OnMaterialToggle()
	{
		var sm = INSelectionManager.Instance;
		if (sm == null) return;
		sm.CycleMaterialIndex();
	}

	private void OnShapeTypeCycle()
	{
		var sm = INSelectionManager.Instance;
		if (sm == null) return;
		sm.CycleShapeType();
	}

	private void OnShapeSolidToggle()
	{
		var sm = INSelectionManager.Instance;
		if (sm == null) return;
		sm.ToggleShapeSolid();
	}

	private void OnDeleteClicked()
	{
		var sm = INSelectionManager.Instance;
		if (sm == null) return;
		if (sm.CurrentState == INSelectionManager.State.Selected)
			sm.DeleteSelection();
		else if (sm.CurrentState == INSelectionManager.State.ShapeDrawing)
			sm.CancelShape();
	}

	private void OnConfirmShape() { INSelectionManager.Instance?.ConfirmShape(); }
	private void OnCancelShape() { INSelectionManager.Instance?.CancelShape(); }

	private void UpdateDeleteButtonLabel()
	{
		if (m_deleteText == null) return;
		var sm = INSelectionManager.Instance;
		if (sm == null) return;
		if (sm.CurrentState == INSelectionManager.State.Selected)
			m_deleteText.text = L("ToolBook_DeleteSelection");
		else if (sm.CurrentState == INSelectionManager.State.ShapeDrawing)
			m_deleteText.text = L("ToolBook_Cancel");
		else
			m_deleteText.text = L("ToolBook_Delete");
	}

	private void UpdateOverwriteToggle()
	{
		if (m_overwriteText == null) return;
		int mode = INUserSettings.Instance?.StarSeaSettings?.ToolBookPasteOverwriteMode ?? 1;
		switch (mode)
		{
			case 0: m_overwriteText.text = L("ToolBook_Overwrite_None"); m_overwriteBg.color = s_normalBg; break;
			case 1: m_overwriteText.text = L("ToolBook_Overwrite"); m_overwriteBg.color = new Color(0.15f, 0.5f, 0.15f, 0.9f); break;
			case 2: m_overwriteText.text = L("ToolBook_Overwrite_All"); m_overwriteBg.color = new Color(0.7f, 0.4f, 0.1f, 0.9f); break;
		}
	}

	/// <summary>What the rotate button says for a quarter turn, in the language that is on.</summary>
	private static string RotateTitle(int quarterTurns)
	{
		return string.Format(L("ToolBook_RotateFormat"), quarterTurns * 90);
	}

	// Direction hint arrows for rotation steps: 0°→ 90°↓ 180°← 270°↑
	private static readonly string[] s_rotateArrows = { "\u2192", "\u2193", "\u2190", "\u2191" };
	private void UpdateRotateButton()
	{
		if (m_rotateText == null) return;
		var sm = INSelectionManager.Instance;
		int rot = 0;
		if (sm != null)
			rot = (sm.CurrentState == INSelectionManager.State.Selected)
				? sm.SelectionRotation
				: sm.PasteRotation;
		rot = ((rot % 4) + 4) % 4;
		m_rotateText.text = RotateTitle(rot) + s_rotateArrows[rot];
	}

	private void UpdateMaterialButton()
	{
		if (m_materialText == null || m_materialBg == null) return;
		var sm = INSelectionManager.Instance;
		if (sm == null) return;
		bool on = sm.MaterialOverride;
		int idx = sm.MaterialIndex;
		string[] ids = { "ToolBook_Material_Wood", "ToolBook_Material_Metal", "ToolBook_Material_Custom2", "ToolBook_Material_Custom3" };
		m_materialText.text = on ? L(ids[Mathf.Clamp(idx, 0, ids.Length - 1)]) : L("ToolBook_Material");
		m_materialBg.color = on ? new Color(0.5f, 0.3f, 0.1f, 0.9f) : s_normalBg;
	}

	private void UpdateShapeTypeButton()
	{
		if (m_shapeTypeText == null) return;
		var sm = INSelectionManager.Instance;
		if (sm == null) return;
		int shapeIdx = (int)sm.CurrentShape;
		if (shapeIdx >= 0 && shapeIdx < s_shapeIds.Length)
			m_shapeTypeText.text = L(s_shapeIds[shapeIdx]);
	}

	private void UpdateShapeSolidButton()
	{
		if (m_shapeSolidText == null) return;
		var sm = INSelectionManager.Instance;
		if (sm == null) return;
		bool solid = sm.ShapeSolid;
		m_shapeSolidText.text = solid ? L("ToolBook_Solid") : L("ToolBook_Hollow");
		m_shapeSolidBg.color = solid ? new Color(0.5f, 0.3f, 0.1f, 0.9f) : s_normalBg;
	}

	private static readonly Color s_dedupFlashBg = new Color(0.9f, 0.7f, 0.1f, 0.9f);
	private int m_saveBtnFlashIdx = -1;

	private void UpdateSaveDedupFlash()
	{
		int saveIdx = ToolSave;
		if (saveIdx < 0 || saveIdx >= m_childBgs.Count) return;

		if (INSelectionManager.LastSaveDuplicate &&
			Time.unscaledTime - INSelectionManager.LastSaveDuplicateTime < 1.5f)
		{
			// Flash yellow when duplicate detected
			if (m_saveBtnFlashIdx != saveIdx)
			{
				m_childBgs[saveIdx].color = s_dedupFlashBg;
				m_saveBtnFlashIdx = saveIdx;
			}
		}
		else if (m_saveBtnFlashIdx == saveIdx)
		{
			// Restore normal color
			m_childBgs[saveIdx].color = (saveIdx == m_activeChildIndex) ? s_activeBg : s_normalBg;
			m_saveBtnFlashIdx = -1;
		}
	}

	private void UpdateConfirmCancelVisibility()
	{
		bool show = INSelectionManager.Instance != null &&
			INSelectionManager.Instance.CurrentState == INSelectionManager.State.ShapeDrawing;
		if (m_confirmObj != null) m_confirmObj.SetActive(m_isOpen && show);
		if (m_cancelObj != null) m_cancelObj.SetActive(m_isOpen && show);
	}

	private void UpdateDimLabel()
	{
		if (m_dimLabelObj == null) return;
		var sm = INSelectionManager.Instance;
		if (sm == null) { m_dimLabelObj.SetActive(false); return; }

		bool selecting = sm.CurrentState == INSelectionManager.State.Selecting ||
			sm.CurrentState == INSelectionManager.State.Selected;
		bool pasting = sm.CurrentState == INSelectionManager.State.PastePreview;

		// Also show when drag-moving (state may lag one frame)
		if (!selecting && !pasting && sm.IsDragMovingFromSelected)
			pasting = true;

		if (!selecting && !pasting)
		{
			m_dimLabelObj.SetActive(false);
			return;
		}

		int w, h;
		if (selecting)
		{
			w = sm.SelectionMaxX - sm.SelectionMinX + 1;
			h = sm.SelectionMaxY - sm.SelectionMinY + 1;
		}
		else
		{
			var clip = sm.Clipboard;
			if (clip == null || clip.Units.Count == 0) { m_dimLabelObj.SetActive(false); return; }
			int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
			foreach (var u in clip.Units)
			{
				if (u.X < minX) minX = u.X;
				if (u.X > maxX) maxX = u.X;
				if (u.Y < minY) minY = u.Y;
				if (u.Y > maxY) maxY = u.Y;
			}
			w = maxX - minX + 1;
			h = maxY - minY + 1;
		}

		m_dimLabel.text = w + " x " + h;
		m_dimLabelObj.SetActive(true);
	}

	// --- Clipboard Slot Drag (handles both click and drag) ---

	private void OnClipSlotClicked(BaseEventData data, int displayIndex)
	{
		if (m_clipDragStarted) return;
		if (m_runStateActive && m_runOptionsShown)
		{
			int runIdx = m_runArchiveScrollOffset + displayIndex;
			if (runIdx < m_runArchiveSlots.Count) ShowRunRenameSlot(displayIndex, runIdx);
			return;
		}
		int actualIdx = m_clipScrollOffset + displayIndex;
		var sm = INSelectionManager.Instance;
		if (sm != null && actualIdx < sm.ClipboardSlots.Count)
			ShowRenameSlot(displayIndex, actualIdx);
	}

	private void OnClipSlotBeginDrag(BaseEventData data, int displayIndex)
	{
		PointerEventData ped = (PointerEventData)data;
		if (m_runStateActive && m_runOptionsShown)
		{
			int runIdx = m_runArchiveScrollOffset + displayIndex;
			if (runIdx >= m_runArchiveSlots.Count) return;
			m_clipDragStartScreenPos = ped.position;
			m_clipDragging = true;
			m_clipDragIndex = runIdx;
			m_clipDragStarted = true;
			ResetRunSlotDeleteHover();
			return;
		}
		int actualIdx = m_clipScrollOffset + displayIndex;
		var sm = INSelectionManager.Instance;
		if (sm == null || actualIdx >= sm.ClipboardSlots.Count) return;
		m_clipDragStartScreenPos = ped.position;
		m_clipDragging = true;
		m_clipDragIndex = actualIdx;
		m_clipDragStarted = true;
		sm.LoadClipboardSlot(actualIdx);
		sm.StartPastePreview();
	}

	private void OnClipSlotDrag(BaseEventData data)
	{
		if (m_runStateActive && m_runOptionsShown)
			RunSlotDragTick(IsOverRunDeleteButton());
	}

	private void OnClipSlotEndDrag(BaseEventData data)
	{
		if (!m_clipDragging) return;
		m_clipDragging = false;
		PointerEventData ped = (PointerEventData)data;

		if (m_runStateActive && m_runOptionsShown)
		{
			// 需在“删除”按钮上停留 >0.3s 才删除，避免拖槽位时误删
			if (IsOverRunDeleteButton() && RunSlotDeleteArmed())
			{
				DeleteRunArchiveSlot(m_clipDragIndex);
			}
			else if (Vector2.Distance(ped.position, m_clipDragStartScreenPos) > 20f)
			{
				Vector3 w = GetBoxWorldPos(ped.position);
				PasteRunFromArchive(m_clipDragIndex, w);
			}
			ResetRunSlotDeleteHover();
			m_clipDragIndex = -1;
			m_clipDragStarted = false;
			return;
		}

		if (IsOverDeleteButton())
		{
			INSelectionManager.Instance?.DeleteClipboardSlot(m_clipDragIndex);
			INSelectionManager.Instance?.CancelPaste();
		}
		else if (IsOverUndoButton())
		{
			INSelectionManager.Instance?.CancelPaste();
		}
		else if (Vector2.Distance(ped.position, m_clipDragStartScreenPos) > 20f)
		{
			INSelectionManager.Instance?.ConfirmPaste();
		}
		m_clipDragIndex = -1;
		m_clipDragStarted = false;
	}

	// --- Paste Drag ---

	private void OnPasteBeginDrag(BaseEventData data)
	{
		// 运行选项态下该位是“变速”等运行项，不触发粘贴拖拽
		if (m_runStateActive && m_runOptionsShown) return;
		PointerEventData ped = (PointerEventData)data;
		m_pasteDragStartScreenPos = ped.position;
		m_pasteDragging = true;
		EventManager.Send(new UIEvent(UIEvent.Type.ToolBookPaste));
	}

	private void OnPasteDrag(BaseEventData data) { }

	private void OnPasteEndDrag(BaseEventData data)
	{
		if (m_runStateActive && m_runOptionsShown) { m_pasteDragging = false; return; }
		if (!m_pasteDragging) return;
		m_pasteDragging = false;
		PointerEventData ped = (PointerEventData)data;

		if (IsOverUndoButton())
			EventManager.Send(new UIEvent(UIEvent.Type.ToolBookPasteCancel));
		else if (Vector2.Distance(ped.position, m_pasteDragStartScreenPos) > 30f)
			EventManager.Send(new UIEvent(UIEvent.Type.ToolBookPasteConfirm));
		else
			EventManager.Send(new UIEvent(UIEvent.Type.ToolBookPasteCancel));
	}

	// --- Main Button Drag ---

	private void OnBeginDrag(BaseEventData data) { m_isDragging = true; }

	private void OnDrag(BaseEventData data)
	{
		PointerEventData ped = (PointerEventData)data;
		m_mainRect.anchoredPosition += ped.delta / m_canvas.scaleFactor;
	}

	private void OnEndDrag(BaseEventData data)
	{
		SavePosition();
		StartCoroutine(ResetDragging());
	}

	private IEnumerator ResetDragging()
	{
		yield return null;
		m_isDragging = false;
	}

	// --- Rename UI ---

	private void CreateRenameUI()
	{
		m_renameObj = new GameObject("RenamePanel");
		m_renameObj.transform.SetParent(m_mainRect.transform, false);
		m_renameRect = m_renameObj.AddComponent<RectTransform>();
		m_renameRect.anchorMin = new Vector2(0.5f, 0.5f);
		m_renameRect.anchorMax = new Vector2(0.5f, 0.5f);
		m_renameRect.pivot = new Vector2(0.5f, 0.5f);
		m_renameRect.sizeDelta = new Vector2(240f * m_size, 80f * m_size);

		UImage panelBg = m_renameObj.AddComponent<UImage>();
		panelBg.color = new Color(0.12f, 0.12f, 0.12f, 0.97f);

		// Title
		GameObject titleObj = new GameObject("Title");
		titleObj.transform.SetParent(m_renameObj.transform, false);
		RectTransform titleRect = titleObj.AddComponent<RectTransform>();
		titleRect.anchorMin = new Vector2(0f, 0.6f);
		titleRect.anchorMax = new Vector2(1f, 1f);
		titleRect.offsetMin = new Vector2(8f, 0f);
		titleRect.offsetMax = new Vector2(-8f, -2f);
		UText titleText = titleObj.AddComponent<UText>();
		titleText.text = L("ToolBook_NameSlot");
		titleText.font = INUnity.ArialFont;
		titleText.fontSize = Mathf.RoundToInt(13 * m_size);
		titleText.alignment = TextAnchor.MiddleCenter;
		titleText.color = new Color(0.7f, 0.7f, 0.7f, 1f);
		m_renameTitle = titleText;

		// Input field
		GameObject inputObj = new GameObject("Input");
		inputObj.transform.SetParent(m_renameObj.transform, false);
		RectTransform inputRect = inputObj.AddComponent<RectTransform>();
		inputRect.anchorMin = new Vector2(0.05f, 0.15f);
		inputRect.anchorMax = new Vector2(0.95f, 0.58f);
		inputRect.offsetMin = Vector2.zero;
		inputRect.offsetMax = Vector2.zero;

		UImage inputBg = inputObj.AddComponent<UImage>();
		inputBg.color = new Color(0.15f, 0.15f, 0.15f, 1f);

		m_renameInput = inputObj.AddComponent<UnityEngine.UI.InputField>();
		m_renameInput.targetGraphic = inputBg;

		// Input text child
		GameObject inputTextObj = new GameObject("Text");
		inputTextObj.transform.SetParent(inputObj.transform, false);
		RectTransform inputTextRect = inputTextObj.AddComponent<RectTransform>();
		inputTextRect.anchorMin = Vector2.zero;
		inputTextRect.anchorMax = Vector2.one;
		inputTextRect.offsetMin = new Vector2(6f, 0f);
		inputTextRect.offsetMax = new Vector2(-6f, 0f);
		UText inputText = inputTextObj.AddComponent<UText>();
		inputText.font = INUnity.ArialFont;
		inputText.fontSize = Mathf.RoundToInt(14 * m_size);
		inputText.alignment = TextAnchor.MiddleLeft;
		inputText.color = Color.white;
		inputText.supportRichText = false;
		m_renameInput.textComponent = inputText;

		// Placeholder
		GameObject placeholderObj = new GameObject("Placeholder");
		placeholderObj.transform.SetParent(inputObj.transform, false);
		RectTransform placeholderRect = placeholderObj.AddComponent<RectTransform>();
		placeholderRect.anchorMin = Vector2.zero;
		placeholderRect.anchorMax = Vector2.one;
		placeholderRect.offsetMin = new Vector2(6f, 0f);
		placeholderRect.offsetMax = new Vector2(-6f, 0f);
		UText placeholder = placeholderObj.AddComponent<UText>();
		placeholder.font = INUnity.ArialFont;
		placeholder.fontSize = Mathf.RoundToInt(14 * m_size);
		placeholder.fontStyle = FontStyle.Italic;
		placeholder.alignment = TextAnchor.MiddleLeft;
		placeholder.color = new Color(0.5f, 0.5f, 0.5f, 0.7f);
		placeholder.text = L("ToolBook_NamePlaceholder");
		m_renameInput.placeholder = placeholder;
		m_renamePlaceholder = placeholder;

		m_renameInput.onEndEdit.AddListener(OnRenameInputEndEdit);

		m_renameObj.SetActive(false);
	}

	private void ShowRenameSlot(int displayIndex, int actualIndex)
	{
		if (m_renameObj == null) return;
		var sm = INSelectionManager.Instance;
		if (sm == null) return;
		if (actualIndex < 0 || actualIndex >= sm.ClipboardSlots.Count) return;

		m_renameSlotIndex = actualIndex;
		m_renameIsRun = false;

		// Position near the display slot
		float spacing = 6f * m_size;
		float btnH = 36f * m_size;
		float slotY = -(displayIndex * (btnH + spacing));
		float panelW = 240f * m_size;
		float panelH = 80f * m_size;
		float slotX = m_clipSlotRects[displayIndex].anchoredPosition.x;
		m_renameRect.anchoredPosition = new Vector2(slotX - panelW * 0.5f - 10f, slotY - panelH * 0.5f);

		// Set current name
		var names = sm.ClipboardSlotNames;
		string currentName = (actualIndex < names.Count) ? names[actualIndex] : "";
		m_renameInput.text = currentName;
		m_renameObj.SetActive(true);
		m_renameInput.ActivateInputField();
		m_renameInput.Select();
	}

	private void OnRenameInputEndEdit(string value)
	{
		if (m_renameSlotIndex < 0) return;
		if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
		{
			ConfirmRename(value);
			return;
		}
		// Android: soft keyboard "Done" doesn't fire KeyCode.Return — confirm instead of dismiss
		if (Application.platform == RuntimePlatform.Android)
		{
			ConfirmRename(value);
			return;
		}
		DismissRename();
	}

	private void ConfirmRename(string value)
	{
		if (m_renameSlotIndex >= 0)
		{
			string name = string.IsNullOrEmpty(value) ? string.Format(L("ToolBook_SlotFormat"), m_renameSlotIndex + 1) : value.Trim();
			if (m_renameIsRun) RenameRunArchiveSlot(m_renameSlotIndex, name);
			else INSelectionManager.Instance?.RenameClipboardSlot(m_renameSlotIndex, name);
		}
		m_renameSlotIndex = -1;
		m_renameIsRun = false;
		if (m_renameObj != null) m_renameObj.SetActive(false);
	}

	private void DismissRename()
	{
		m_renameSlotIndex = -1;
		if (m_renameObj != null) m_renameObj.SetActive(false);
	}

	// --- Open/Close ---

	public void Toggle()
	{
		if (m_coroutineRunning) return;
		ApplySettings();
		ApplySizes();

		if (m_isOpen) { m_isOpen = false; StartCoroutine(SlideClose()); }
		else { m_isOpen = true; StartCoroutine(SlideOpen()); }
	}

	/// <summary>
	/// Sizes every button the menu shows, for the size setting and the language that are on.
	/// </summary>
	/// <remarks>
	/// The widths follow <see cref="LabelWidth"/>, so this runs when the menu is opened and when the
	/// language changes under it: a label that grows from two Chinese characters to six English ones would
	/// otherwise be drawn wider than the button holding it until the menu was closed and opened again.
	/// </remarks>
	private void ApplySizes()
	{
		if (m_mainRect == null) return;
		float sz = 48f * m_size;
		m_mainRect.sizeDelta = new Vector2(sz, sz);

		float btnW = 48f * m_size * LabelWidth;
		float btnH = 36f * m_size;
		for (int i = 0; i < m_childRects.Count; i++)
		{
			if (m_childRects[i] != null)
				m_childRects[i].sizeDelta = new Vector2(btnW, btnH);
			if (i < m_childTexts.Count && m_childTexts[i] != null)
				m_childTexts[i].fontSize = Mathf.RoundToInt(16 * m_size);
		}
		for (int i = 0; i < m_clipSlotRects.Count; i++)
		{
			if (m_clipSlotRects[i] != null)
				m_clipSlotRects[i].sizeDelta = new Vector2(btnW, btnH);
			if (i < m_clipSlotTexts.Count && m_clipSlotTexts[i] != null)
				m_clipSlotTexts[i].fontSize = Mathf.RoundToInt(16 * m_size);
		}

		// Column 2 buttons, whose labels are a size smaller than the toolbar's
		GameObject[] col2 = { m_deleteBtnObj, m_overwriteObj, m_rotateObj, m_materialObj, m_shapeTypeObj, m_shapeSolidObj };
		UText[] col2Texts = { m_deleteText, m_overwriteText, m_rotateText, m_materialText, m_shapeTypeText, m_shapeSolidText };
		int[] col2FontSizes = { 16, 12, 12, 12, 12, 12 };
		for (int i = 0; i < col2.Length; i++)
		{
			if (col2[i] != null)
			{
				RectTransform rt = col2[i].GetComponent<RectTransform>();
				if (rt != null) rt.sizeDelta = new Vector2(btnW, btnH);
			}
			if (col2Texts[i] != null)
				col2Texts[i].fontSize = Mathf.RoundToInt(col2FontSizes[i] * m_size);
		}

		float halfW = 24f * m_size;
		if (m_clipPageUpObj != null) m_clipPageUpObj.GetComponent<RectTransform>().sizeDelta = new Vector2(halfW, btnH);
		if (m_clipPageDownObj != null) m_clipPageDownObj.GetComponent<RectTransform>().sizeDelta = new Vector2(halfW, btnH);
		if (m_clipPageTextObj != null) m_clipPageTextObj.GetComponent<RectTransform>().sizeDelta = new Vector2(72f * m_size, 28f * m_size);

		if (InterfaceLanguages.ToInterface(INUnity.Language) == InterfaceLanguage.English)
		{
			m_mainRect.sizeDelta = new Vector2(sz * 1.3f, sz);
		}
	}

	// Layout: Col1=child buttons | Col2=delete/overwrite/rotate/material/shapeType/shapeSolid | Col3=clipboard
	private IEnumerator SlideOpen()
	{
		m_coroutineRunning = true;
		float mainW = m_mainRect.sizeDelta.x;
		float spacing = 6f * m_size;
		float btnW = 48f * m_size * LabelWidth;
		float btnH = 36f * m_size;

		float col1X = -(mainW + spacing);
		float col2X = -(mainW + spacing + btnW + spacing);
		float col3X = -(mainW + spacing + btnW + spacing + btnW + spacing);

		// Column 1: child buttons
		for (int i = 0; i < m_childRects.Count; i++)
		{
			float targetY = -(i * (btnH + spacing));
			m_childRects[i].anchoredPosition = new Vector2(0f, targetY);
			m_childRects[i].gameObject.SetActive(true);
			StartCoroutine(SlideTo(m_childRects[i], new Vector2(col1X, targetY)));
		}

		// Column 2: delete(0) → overwrite(1) → rotate(2) → material(3) → shapeType(4) → shapeSolid(5)
		float col2Y = 0f;
		System.Action<GameObject, float> slideCol2 = (obj, y) =>
		{
			if (obj == null) return;
			RectTransform rt = obj.GetComponent<RectTransform>();
			rt.anchoredPosition = new Vector2(0f, 0f);
			obj.SetActive(true);
			StartCoroutine(SlideTo(rt, new Vector2(col2X, y)));
		};
		slideCol2(m_deleteBtnObj, col2Y);
		col2Y -= (btnH + spacing);
		slideCol2(m_overwriteObj, col2Y);
		col2Y -= (btnH + spacing);
		slideCol2(m_rotateObj, col2Y);
		col2Y -= (btnH + spacing);
		slideCol2(m_materialObj, col2Y);
		col2Y -= (btnH + spacing);
		slideCol2(m_shapeTypeObj, col2Y);
		col2Y -= (btnH + spacing);
		slideCol2(m_shapeSolidObj, col2Y);

		// Column 3: page controls at top, clipboard slots below
		float halfW = 24f * m_size;
		float scrollX = col3X - btnW;
		float pageOffset = btnH + spacing; // space for page controls

		if (m_clipPageUpObj != null)
		{
			m_clipPageUpObj.SetActive(true);
			m_clipPageUpObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 0f);
			StartCoroutine(SlideTo(m_clipPageUpObj.GetComponent<RectTransform>(), new Vector2(scrollX, 0f)));
		}
		if (m_clipPageDownObj != null)
		{
			m_clipPageDownObj.SetActive(true);
			m_clipPageDownObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 0f);
			StartCoroutine(SlideTo(m_clipPageDownObj.GetComponent<RectTransform>(), new Vector2(scrollX, -(btnH + spacing))));
		}
		if (m_clipPageTextObj != null)
		{
			m_clipPageTextObj.SetActive(true);
			m_clipPageTextObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 0f);
		}

		for (int i = 0; i < m_clipSlotObjs.Count; i++)
		{
			float slotTargetY = -(pageOffset + i * (btnH + spacing));
			m_clipSlotObjs[i].SetActive(true);
			m_clipSlotRects[i].anchoredPosition = new Vector2(0f, 0f);
			StartCoroutine(SlideTo(m_clipSlotRects[i], new Vector2(col3X, slotTargetY)));
		}

		// 用真实时间等待：暂停时 timeScale=0，WaitForSeconds 永不结束会导致菜单无法再次打开
		yield return new WaitForSecondsRealtime(0.3f);
		m_coroutineRunning = false;
	}

	private IEnumerator SlideClose()
	{
		m_coroutineRunning = true;
		for (int i = 0; i < m_childRects.Count; i++)
			StartCoroutine(SlideTo(m_childRects[i], new Vector2(0f, m_childRects[i].anchoredPosition.y), deactivate: true));
		for (int i = 0; i < m_clipSlotObjs.Count; i++)
			StartCoroutine(SlideTo(m_clipSlotRects[i], new Vector2(0f, m_clipSlotRects[i].anchoredPosition.y), deactivate: true));

		GameObject[] col2Objs = { m_deleteBtnObj, m_overwriteObj, m_rotateObj, m_materialObj, m_shapeTypeObj, m_shapeSolidObj };
		foreach (var obj in col2Objs)
		{
			if (obj != null)
			{
				RectTransform rt = obj.GetComponent<RectTransform>();
				StartCoroutine(SlideTo(rt, new Vector2(0f, rt.anchoredPosition.y), deactivate: true));
			}
		}

		// Slide out pagination controls
		if (m_clipPageUpObj != null)
		{
			RectTransform rt = m_clipPageUpObj.GetComponent<RectTransform>();
			StartCoroutine(SlideTo(rt, new Vector2(0f, rt.anchoredPosition.y), deactivate: true));
		}
		if (m_clipPageDownObj != null)
		{
			RectTransform rt = m_clipPageDownObj.GetComponent<RectTransform>();
			StartCoroutine(SlideTo(rt, new Vector2(0f, rt.anchoredPosition.y), deactivate: true));
		}
		if (m_clipPageTextObj != null)
		{
			RectTransform rt = m_clipPageTextObj.GetComponent<RectTransform>();
			StartCoroutine(SlideTo(rt, new Vector2(0f, rt.anchoredPosition.y), deactivate: true));
		}

		if (m_confirmObj != null) m_confirmObj.SetActive(false);
		if (m_cancelObj != null) m_cancelObj.SetActive(false);
		DismissRename();

		// 用真实时间等待：暂停时 timeScale=0，WaitForSeconds 永不结束会导致菜单无法再次打开
		yield return new WaitForSecondsRealtime(0.3f);
		m_coroutineRunning = false;
	}

	private IEnumerator SlideTo(RectTransform rt, Vector2 target, bool deactivate = false)
	{
		Vector2 start = rt.anchoredPosition;
		float step = 0f;
		float dist = Vector2.Distance(start, target);
		if (dist < 1f) dist = 1f;
		float duration = dist / m_slideSpeed;

		while (step < duration)
		{
			step += Time.unscaledDeltaTime;
			rt.anchoredPosition = Vector2.Lerp(start, target, step / duration);
			yield return null;
		}
		rt.anchoredPosition = target;

		if (deactivate) rt.gameObject.SetActive(false);
	}

}
