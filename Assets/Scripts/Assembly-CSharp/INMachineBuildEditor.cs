using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Machine marquee and persistent rigid manipulation, separate from clipboard/grid selection.</summary>
public class INMachineBuildEditor : MonoBehaviour
{
    private enum Drag { None, Free, X, Y, Rotate }
    public static INMachineBuildEditor Instance { get; private set; }
    public static bool IsCapturingPointer => Instance != null && INBuildToolPalette.IsBuildState() &&
        (Instance.m_marquee || Instance.m_drag != Drag.None ||
         Instance.m_consumedFrame == Time.frameCount ||
         (Input.GetMouseButton(0) && !GuiManager.GetPointer().onWidget && Instance.WantsMachinePointer(Input.mousePosition)) ||
         (Input.GetMouseButton(1) && !GuiManager.GetPointer().onWidget));

    private Contraption m_contraption;
    private bool m_selected, m_marquee, m_leftMarquee;
    private Vector2 m_boxStart, m_boxEnd, m_dragStart, m_pivot, m_pivotScreen;
    private float m_lastAngle, m_deltaAngle;
    private BuildMachineTransform m_startFrame;
    private int m_selectedGroup = -1;
    private readonly HashSet<BasePart> m_marqueeParts = new HashSet<BasePart>();
    private BasePart m_selectedPart;
    private PartPose m_startPart;
    private GridlessGroupMerge.Snapshot m_startGroups;
    private readonly List<GridlessGroupMerge.Circle> m_connectionCircles = new List<GridlessGroupMerge.Circle>();
    private RaycastHit[] m_pickHits = new RaycastHit[32];
    private struct PartPose
    {
        public int x, y;
        public Vector2 offset;
        public float angle;
        public bool hasX, hasY, hasAngle;
        public BasePart.GridRotation rotation;
        public BasePart enclosing, enclosed;
        public Vector2 position => new Vector2(x, y) + offset;
        public static PartPose Read(BasePart part)
        {
            var pose = new PartPose { x = part.m_coordX, y = part.m_coordY, offset = GridlessBuild.GetOffset(part),
                angle = FineRotate.GetAngle(part, part.GetRotationAngle(part.m_gridRotation)), rotation = part.m_gridRotation,
                enclosing = part.enclosedInto, enclosed = part.enclosedPart };
            pose.hasX = part.TryGetStoredParam(GridlessBuild.OffsetXKey, out _);
            pose.hasY = part.TryGetStoredParam(GridlessBuild.OffsetYKey, out _);
            pose.hasAngle = part.TryGetStoredParam(FineRotate.AngleKey, out _);
            return pose;
        }
        public bool Restore(Contraption contraption, BasePart part)
        {
            if (!contraption.TryMoveBuildPart(part, x, y, offset, true, enclosing, enclosed)) return false;
            if (!hasX) part.RemoveStoredParam(GridlessBuild.OffsetXKey);
            if (!hasY) part.RemoveStoredParam(GridlessBuild.OffsetYKey);
            FineRotate.Restore(part, rotation, hasAngle, angle);
            return true;
        }
    }
    private struct UndoFrame
    {
        public int group;
        public BuildMachineTransform frame;
        public bool singlePart;
        public BasePart part;
        public PartPose pose;
        public GridlessGroupMerge.Snapshot groups;
        public GridlessSelectionExtract.Snapshot extraction;
        public List<BasePart> selection;
    }
    private Drag m_drag;
    private int m_consumedFrame = -1;
    private readonly List<UndoFrame> m_undo = new List<UndoFrame>();
    private INBuildOverlayGraphic m_graphic;
    private Text m_translateLabel, m_rotateLabel, m_info;
    private Rect m_screenBounds;
    private float UiScale => Mathf.Max(0.6f, (Camera.main != null ? Camera.main.pixelHeight : Screen.height) / 1080f) * INUiScale.Multiplier;
    private float Radius => 78f * UiScale;
    private bool WantsMachinePointer(Vector2 mouse)
    {
        if (m_contraption == null || INPartParameterPanel.ContainsPointer(mouse)) return false;
        if (IsPartTool(BuildToolPalette.Current) || BuildToolPalette.Current == BuildTool.SelectMachine) return true;
        bool machineTool = BuildToolPalette.Current == BuildTool.Translate || BuildToolPalette.Current == BuildTool.RotateMachine;
        return (m_selected && (HitGizmo(mouse) != Drag.None || (machineTool && m_screenBounds.Contains(mouse)))) ||
            (machineTool && GridlessBuild.TryFindGroupAt(m_contraption, WorldPointer(mouse), out _));
    }
    public bool Selected => m_selected;
    public int SelectedGroup => m_selectedGroup;
    public BasePart SelectedPart => m_selectedPart;
    public IReadOnlyCollection<BasePart> MarqueeParts => m_marqueeParts;
    public bool CanExtractSelection => m_selected && m_drag == Drag.None && !m_marquee &&
        GridlessSelectionExtract.CanExtract(m_contraption, m_marqueeParts);
    private static bool IsPartTool(BuildTool tool) => tool == BuildTool.TranslatePart || tool == BuildTool.RotatePart;
    private static bool IsMachineTool(BuildTool tool) => tool == BuildTool.SelectMachine || tool == BuildTool.Translate || tool == BuildTool.RotateMachine;
    private BuildMachineTransform SelectedFrame => BuildMachineTransform.Read(m_contraption, m_selectedGroup);

    private void Awake()
    {
        Instance = this;
        var canvasGo = new GameObject("MachineBuildOverlay", typeof(RectTransform), typeof(Canvas));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.overrideSorting = true; canvas.sortingOrder = 24000;
        var graphicGo = new GameObject("BoundaryAndGizmo", typeof(RectTransform), typeof(CanvasRenderer), typeof(INBuildOverlayGraphic));
        graphicGo.transform.SetParent(canvasGo.transform, false);
        var rect = graphicGo.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = Vector2.zero;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        m_graphic = graphicGo.GetComponent<INBuildOverlayGraphic>(); m_graphic.raycastTarget = false;
        m_translateLabel = CreateLabel(canvasGo.transform, "Translate Machine  /  平移载具");
        m_rotateLabel = CreateLabel(canvasGo.transform, "Click and drag to rotate machine  /  拖动旋转");
        m_info = CreateLabel(canvasGo.transform, "");
    }
    private Text CreateLabel(Transform parent, string value)
    {
        var go = new GameObject("GizmoLabel", typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
        Text label = go.GetComponent<Text>(); label.text = value; label.font = INUiUtil.UiFont; label.fontSize = 14;
        label.color = INBuildStyle.Ivory; label.raycastTarget = false; label.alignment = TextAnchor.MiddleCenter;
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = Vector2.zero;
        label.rectTransform.sizeDelta = new Vector2(410f, 26f);
        var shadow = go.AddComponent<Shadow>(); shadow.effectColor = new Color(0f, 0f, 0f, 0.8f); shadow.effectDistance = new Vector2(1f, -1f);
        return label;
    }
    private void OnDestroy() { if (Instance == this) Instance = null; }
    public void SelectMachine()
    {
        DeselectPart();
        if (!GridlessBuild.TryGetGroup(m_contraption, m_selectedGroup, out _)) m_selectedGroup = GridlessBuild.PrimaryGroup(m_contraption);
        m_selected = GridlessBuild.TryGetGroup(m_contraption, m_selectedGroup, out _);
    }
    public void DeselectMachine()
    {
        if (m_selected && m_drag != Drag.None) BuildMachineTransform.Apply(m_contraption, m_startFrame, m_selectedGroup);
        ResetInteraction(); m_selected = false; m_marqueeParts.Clear();
    }
    public void DeselectPart()
    {
        if (m_selectedPart != null && m_drag != Drag.None) m_startPart.Restore(m_contraption, m_selectedPart);
        if (m_selectedPart != null || !m_selected) ResetInteraction();
        m_selectedPart = null;
    }
    public void OnToolChanged(BuildTool previous, BuildTool next)
    {
        if (m_selected && m_drag != Drag.None)
        {
            BuildMachineTransform.Apply(m_contraption, m_startFrame, m_selectedGroup);
            ResetInteraction();
        }
        if (IsPartTool(previous) && IsPartTool(next))
        {
            if (m_drag != Drag.None && m_selectedPart != null) m_startPart.Restore(m_contraption, m_selectedPart);
            ResetInteraction();
        }
        else
        {
            DeselectPart();
            if (!IsMachineTool(next)) DeselectMachine();
        }
    }

    /// <summary>Handles toolbar marquee and gizmo input; right-button input belongs to part deletion.</summary>
    public bool HandleInput(Contraption contraption)
    {
        bool typing = EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
            EventSystem.current.currentSelectedGameObject.GetComponent<InputField>() != null;
        return HandlePointer(contraption, GuiManager.GetPointer(), Time.unscaledTime, Input.GetMouseButton(1), Input.GetMouseButton(0),
            !typing && Input.GetKeyDown(KeyCode.Escape), !typing && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.Z),
            Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
    }

    public bool HandlePointer(Contraption contraption, GuiManager.Pointer pointer, float now, bool rightHeld, bool leftHeld,
        bool escape = false, bool undo = false, bool shift = false)
    {
        if (m_contraption != contraption)
        {
            m_contraption = contraption; m_undo.Clear(); ResetInteraction(); m_selected = false; m_selectedPart = null; m_selectedGroup = -1;
            m_marqueeParts.Clear();
        }
        if (!INBuildToolPalette.IsBuildState() || contraption == null) { DeselectPart(); DeselectMachine(); return false; }
        if (ContraptionLayers.Enabled(contraption) && ContraptionLayers.ActiveLayer == 0) return false;
        Vector2 mouse = pointer.position;
        bool overUi = pointer.onWidget || INPartParameterPanel.ContainsPointer(mouse);
        if (escape)
        {
            DeselectPart();
            DeselectMachine();
            BuildToolPalette.Clear(); return Consume();
        }
        if (undo) { Undo(); return Consume(); }
        if (IsPartTool(BuildToolPalette.Current)) return HandlePartPointer(pointer, overUi, rightHeld, leftHeld, shift);
        if (pointer.secondaryDown || pointer.secondaryDragging || rightHeld) return false;
        if (BuildToolPalette.Current == BuildTool.SelectMachine && pointer.down && !overUi && (!m_selected || HitGizmo(mouse) == Drag.None))
        {
            m_marquee = m_leftMarquee = true; m_boxStart = m_boxEnd = mouse;
        }
        if (m_leftMarquee)
        {
            m_boxEnd = mouse;
            if (pointer.up) { FinishMarquee(); m_marquee = m_leftMarquee = false; }
            return Consume();
        }
        if (m_drag != Drag.None)
        {
            if (!overUi) UpdateMachineDrag(mouse, shift);
            if (pointer.up || !leftHeld)
            {
                if (!FrameEquals(m_startFrame, SelectedFrame))
                {
                    var beforeMerge = GridlessGroupMerge.Enabled ? m_startGroups ?? GridlessGroupMerge.Capture(contraption) : null;
                    bool merged = beforeMerge != null && GridlessGroupMerge.TryMerge(contraption, m_selectedGroup) > 0;
                    PushUndo(new UndoFrame { group = m_selectedGroup, frame = m_startFrame, groups = merged ? beforeMerge : null });
                    GetComponent<ConstructionUI>()?.NotifyMachineTransformChanged();
                }
                ResetInteraction(); return Consume();
            }
            return Consume();
        }
        if (pointer.down && !overUi)
        {
            Drag hit = m_selected ? HitGizmo(mouse) : Drag.None;
            bool machineTool = BuildToolPalette.Current == BuildTool.Translate || BuildToolPalette.Current == BuildTool.RotateMachine;
            if (hit == Drag.None && machineTool && (!m_selected || !m_screenBounds.Contains(mouse)) &&
                GridlessBuild.TryFindGroupAt(contraption, WorldPointer(mouse), out GridlessBuild.BuildGroup group))
            {
                m_selectedGroup = group.id; m_selected = true; m_marqueeParts.Clear(); LateUpdate();
                GetComponent<ConstructionUI>()?.ClearMachinePlacementSelection();
            }
            if (!m_selected) return machineTool ? Consume() : false;
            if (hit == Drag.None && BuildToolPalette.Current == BuildTool.Translate && m_screenBounds.Contains(mouse)) hit = Drag.Free;
            if (hit == Drag.None && BuildToolPalette.Current == BuildTool.RotateMachine && m_screenBounds.Contains(mouse)) hit = Drag.Rotate;
            if (hit != Drag.None)
            {
                m_drag = hit; m_startFrame = SelectedFrame; m_dragStart = WorldPointer(mouse);
                m_startGroups = GridlessGroupMerge.Enabled ? GridlessGroupMerge.Capture(contraption) : null;
                m_lastAngle = Mathf.Atan2(m_dragStart.y - m_pivot.y, m_dragStart.x - m_pivot.x) * Mathf.Rad2Deg; m_deltaAngle = 0;
                return Consume();
            }
        }
        return false;
    }
    private bool HandlePartPointer(GuiManager.Pointer pointer, bool overUi, bool rightHeld, bool leftHeld, bool shift)
    {
        if (m_selectedPart == null && m_drag != Drag.None) ResetInteraction();
        if (m_selectedPart != null && (!m_contraption.Parts.Contains(m_selectedPart) || m_selectedPart.m_static))
        { m_selectedPart = null; ResetInteraction(); }
        if (pointer.secondaryDown || pointer.secondaryDragging || rightHeld)
        {
            DeselectPart(); return false;
        }
        Vector2 mouse = pointer.position;
        if (m_drag != Drag.None && m_selectedPart != null)
        {
            if (!overUi) UpdatePartDrag(mouse, shift);
            if (pointer.up || !leftHeld)
            {
                PartPose current = PartPose.Read(m_selectedPart);
                if (!NearPartPose(m_startPart, current))
                {
                    var beforeMerge = GridlessGroupMerge.Enabled ? m_startGroups ?? GridlessGroupMerge.Capture(m_contraption) : null;
                    bool merged = beforeMerge != null && GridlessGroupMerge.TryMerge(m_contraption, GridlessBuild.GetGroupId(m_selectedPart)) > 0;
                    PushUndo(new UndoFrame { singlePart = true, part = m_selectedPart, pose = m_startPart, groups = merged ? beforeMerge : null });
                    GetComponent<ConstructionUI>()?.NotifyMachineTransformChanged();
                }
                ResetInteraction();
            }
            return Consume();
        }
        if (pointer.down && !overUi)
        {
            // Refresh on press so a part selected in the previous frame has immediately usable handles.
            LateUpdate();
            Drag hit = m_selectedPart != null ? HitGizmo(mouse) : Drag.None;
            if (hit != Drag.None)
            {
                m_drag = hit; m_startPart = PartPose.Read(m_selectedPart);
                m_startFrame = BuildMachineTransform.Read(m_selectedPart);
                m_startGroups = GridlessGroupMerge.Enabled ? GridlessGroupMerge.Capture(m_contraption) : null;
                m_dragStart = WorldPointer(mouse); m_pivot = m_selectedPart.transform.localPosition;
                m_lastAngle = Mathf.Atan2(m_dragStart.y - m_pivot.y, m_dragStart.x - m_pivot.x) * Mathf.Rad2Deg; m_deltaAngle = 0f;
            }
            else
            {
                m_selectedPart = ConstructionUI.PickBuildPart(m_contraption, mouse, ref m_pickHits);
                if (m_selectedPart != null) GetComponent<ConstructionUI>()?.ClearMachinePlacementSelection();
                LateUpdate();
            }
            return Consume();
        }
        // Empty clicks and releases belong to the selected tool too, not stock tap/long-press placement.
        return !overUi && (pointer.up || leftHeld) ? Consume() : false;
    }
    private static bool NearPartPose(PartPose a, PartPose b) => a.x == b.x && a.y == b.y &&
        (a.offset - b.offset).sqrMagnitude < 1e-10f && Mathf.Abs(Mathf.DeltaAngle(a.angle, b.angle)) < 0.0001f;
    private void UpdateMachineDrag(Vector2 mouse, bool shift)
    {
        Vector2 local = WorldPointer(mouse);
        var frame = m_startFrame;
        if (m_drag == Drag.Rotate)
        {
            float angle = Mathf.Atan2(local.y - m_pivot.y, local.x - m_pivot.x) * Mathf.Rad2Deg;
            m_deltaAngle += Mathf.DeltaAngle(m_lastAngle, angle); m_lastAngle = angle;
            float delta = shift ? Mathf.Round(m_deltaAngle / 15f) * 15f : m_deltaAngle;
            frame = BuildMachineTransform.AroundPivot(m_startFrame, m_pivot, delta);
        }
        else
        {
            Vector2 delta = local - m_dragStart;
            if (m_drag == Drag.X) delta.y = 0f;
            if (m_drag == Drag.Y) delta.x = 0f;
            if (shift) delta = new Vector2(Mathf.Round(delta.x), Mathf.Round(delta.y));
            frame.translation += delta;
        }
        if (!FrameEquals(frame, SelectedFrame)) BuildMachineTransform.Apply(m_contraption, frame, m_selectedGroup);
    }
    private void UpdatePartDrag(Vector2 mouse, bool shift)
    {
        Vector2 local = WorldPointer(mouse);
        if (m_drag == Drag.Rotate)
        {
            float angle = Mathf.Atan2(local.y - m_pivot.y, local.x - m_pivot.x) * Mathf.Rad2Deg;
            m_deltaAngle += Mathf.DeltaAngle(m_lastAngle, angle); m_lastAngle = angle;
            float step = FineRotate.StepDegrees > 0 ? FineRotate.StepDegrees : 45f;
            float delta = shift ? Mathf.Round(m_deltaAngle / step) * step : m_deltaAngle;
            float target = m_startPart.angle + delta;
            if (Mathf.Abs(Mathf.DeltaAngle(target, FineRotate.GetAngle(m_selectedPart, m_selectedPart.GetRotationAngle(m_selectedPart.m_gridRotation)))) > 0.0001f)
                FineRotate.SetAngle(m_selectedPart, target);
        }
        else
        {
            Vector2 delta = local - m_dragStart;
            if (m_drag == Drag.X) delta.y = 0f;
            if (m_drag == Drag.Y) delta.x = 0f;
            if (shift) delta = new Vector2(Mathf.Round(delta.x * 2f), Mathf.Round(delta.y * 2f)) * 0.5f;
            Vector2 logical = m_startPart.position + BuildMachineTransform.Rotate(delta, -m_startFrame.angle);
            if (!BuildMachineTransform.Finite(logical.x) || !BuildMachineTransform.Finite(logical.y) ||
                Mathf.Abs(logical.x) > 32760f || Mathf.Abs(logical.y) > 32760f) return;
            int x = Mathf.RoundToInt(logical.x), y = Mathf.RoundToInt(logical.y);
            Vector2 offset = logical - new Vector2(x, y);
            PartPose current = PartPose.Read(m_selectedPart);
            if (x != current.x || y != current.y || (offset - current.offset).sqrMagnitude > 1e-10f)
                m_contraption.TryMoveBuildPart(m_selectedPart, x, y, offset);
        }
    }
    private bool Consume() { m_consumedFrame = Time.frameCount; return true; }
    private static bool FrameEquals(BuildMachineTransform a, BuildMachineTransform b) => (a.translation - b.translation).sqrMagnitude < 1e-10f && Mathf.Abs(Mathf.DeltaAngle(a.angle, b.angle)) < 0.0001f;
    private void ResetInteraction() { m_drag = Drag.None; m_marquee = m_leftMarquee = false; m_startGroups = null; }
    private void PushUndo(UndoFrame frame) { m_undo.Add(frame); if (m_undo.Count > 24) m_undo.RemoveAt(0); }
    public void Undo()
    {
        if (m_contraption == null || m_drag != Drag.None) return;
        if (m_undo.Count == 0) { INSelectionManager.Instance?.Undo(); return; }
        int last = m_undo.Count - 1; UndoFrame previous = m_undo[last];
        previous.groups?.Restore(m_contraption);
        if (previous.extraction != null)
        {
            previous.extraction.Restore(m_contraption);
            m_selectedGroup = previous.extraction.OriginalGroup;
            m_selected = GridlessBuild.TryGetGroup(m_contraption, m_selectedGroup, out _);
            m_marqueeParts.Clear();
            foreach (BasePart part in previous.selection)
                if (part != null && m_contraption.Parts.Contains(part)) m_marqueeParts.Add(part);
        }
        else if (previous.singlePart)
        {
            // Deleted parts are skipped; a newly occupied original cell must never be overwritten.
            if (previous.part != null && m_contraption.Parts.Contains(previous.part) && !previous.pose.Restore(m_contraption, previous.part)) return;
        }
        else if (GridlessBuild.TryGetGroup(m_contraption, previous.group, out _)) BuildMachineTransform.Apply(m_contraption, previous.frame, previous.group);
        m_undo.RemoveAt(last);
        GetComponent<ConstructionUI>()?.NotifyMachineTransformChanged();
    }
    public bool ExtractSelection()
    {
        if (!CanExtractSelection) return false;
        var selection = new List<BasePart>(m_marqueeParts);
        if (!GridlessSelectionExtract.TryExtract(m_contraption, m_marqueeParts, out int group, out var snapshot)) return false;
        PushUndo(new UndoFrame { extraction = snapshot, selection = selection });
        m_selectedGroup = group;
        m_selected = true;
        m_marqueeParts.Clear();
        GetComponent<ConstructionUI>()?.NotifyMachineTransformChanged();
        LateUpdate();
        return true;
    }
    private Vector2 WorldPointer(Vector2 screen) => m_contraption.transform.InverseTransformPoint(WPFMonoBehaviour.ScreenToZ0(screen));
    private Vector2 ScreenPoint(Vector2 buildPoint) => ScreenPoint(buildPoint, SelectedFrame);
    private Vector2 ScreenPoint(Vector2 buildPoint, BuildMachineTransform frame)
    {
        Vector2 local = frame.TransformPoint(buildPoint);
        return Camera.main.WorldToScreenPoint(m_contraption.transform.TransformPoint(new Vector3(local.x, local.y, 0f)));
    }
    public static Rect SelectionRect(Vector2 a, Vector2 b) => Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
    private void FinishMarquee()
    {
        Rect rect = SelectionRect(m_boxStart, m_boxEnd);
        if (rect.width < 6f) { rect.xMin -= 6f; rect.xMax += 6f; }
        if (rect.height < 6f) { rect.yMin -= 6f; rect.yMax += 6f; }
        m_selected = false;
        m_marqueeParts.Clear();
        if (Camera.main == null) return;
        float nearest = float.PositiveInfinity;
        foreach (BasePart part in m_contraption.Parts)
        {
            if (part == null || part.m_static || !ContraptionLayers.Editable(part)) continue;
            Vector3 screenPoint = Camera.main.WorldToScreenPoint(part.transform.position);
            if (screenPoint.z <= 0f) continue;
            Vector2 point = screenPoint;
            if (!GridlessBuild.ContainsInclusive(rect, point)) continue;
            m_marqueeParts.Add(part);
            float distance = (point - rect.center).sqrMagnitude;
            if (distance < nearest) { nearest = distance; m_selected = true; m_selectedGroup = GridlessBuild.GetGroupId(part); }
        }
        if (m_selected) { GetComponent<ConstructionUI>()?.ClearMachinePlacementSelection(); LateUpdate(); }
    }
    private Drag HitGizmo(Vector2 mouse)
    {
        Vector2 d = (mouse - m_pivotScreen) / UiScale;
        if (BuildToolPalette.Current == BuildTool.RotatePart)
            return Mathf.Abs(d.magnitude - 78f) < 9f ? Drag.Rotate : Drag.None;
        if (Mathf.Abs(d.x) <= 11f && Mathf.Abs(d.y) <= 11f) return Drag.Free;
        if (d.x > 18f && d.x < 108f && Mathf.Abs(d.y) < 11f) return Drag.X;
        if (d.y > 18f && d.y < 108f && Mathf.Abs(d.x) < 11f) return Drag.Y;
        if (BuildToolPalette.Current != BuildTool.TranslatePart && Mathf.Abs(d.magnitude - 78f) < 9f) return Drag.Rotate;
        return Drag.None;
    }
    private void LateUpdate()
    {
        if (m_graphic == null) return;
        m_graphic.Begin();
        bool visible = INBuildToolPalette.IsBuildState() && m_contraption != null && Camera.main != null;
        m_translateLabel.enabled = m_rotateLabel.enabled = m_info.enabled = visible && (m_selected || m_selectedPart != null);
        if (!visible) { DeselectPart(); DeselectMachine(); m_graphic.End(); return; }
        if (INSelectionManager.Instance != null && INSelectionManager.Instance.CurrentState != INSelectionManager.State.Idle)
        {
            DeselectPart(); DeselectMachine();
            m_translateLabel.enabled = m_rotateLabel.enabled = m_info.enabled = false;
        }
        if (GridlessBuild.Enabled)
            foreach (GridlessBuild.BuildGroup group in GridlessBuild.GetGroups(m_contraption))
                DrawBounds(Rect.MinMaxRect(group.bounds.xMin - 1f, group.bounds.yMin - 1f, group.bounds.xMax + 1f, group.bounds.yMax + 1f), group.frame, true, INBuildStyle.Ivory);
        if (GridlessGroupMerge.Enabled && (IsPartTool(BuildToolPalette.Current) || IsMachineTool(BuildToolPalette.Current)))
            DrawConnectionCircles();
        if (m_marquee) m_graphic.Box(SelectionRect(m_boxStart, m_boxEnd), INBuildStyle.Brass, true);
        if (m_selected && !m_marquee)
        {
            int marked = 0;
            foreach (BasePart part in m_marqueeParts)
            {
                if (part == null || !ContraptionLayers.Editable(part)) continue;
                Vector3 point = Camera.main.WorldToScreenPoint(part.transform.position);
                if (point.z <= 0f) continue;
                m_graphic.Box(new Rect((Vector2)point - Vector2.one * 14f * UiScale,
                    Vector2.one * 28f * UiScale), INBuildStyle.Brass, false);
                if (++marked >= 256) break;
            }
        }
        if (IsPartTool(BuildToolPalette.Current) && m_selectedPart != null && m_contraption.Parts.Contains(m_selectedPart))
        {
            m_pivot = m_selectedPart.transform.localPosition;
            m_pivotScreen = Camera.main.WorldToScreenPoint(m_selectedPart.transform.position);
            bool translate = BuildToolPalette.Current == BuildTool.TranslatePart;
            m_translateLabel.enabled = translate; m_rotateLabel.enabled = !translate;
            m_translateLabel.text = "Translate Block  /  平移部件";
            m_rotateLabel.text = "Click and drag to rotate block  /  拖动旋转部件";
            if (translate)
            {
                m_graphic.Arrow(m_pivotScreen, m_pivotScreen + Vector2.right * 104f * UiScale, new Color(0.85f, 0.32f, 0.24f), UiScale);
                m_graphic.Arrow(m_pivotScreen, m_pivotScreen + Vector2.up * 104f * UiScale, new Color(0.47f, 0.72f, 0.34f), UiScale);
                m_graphic.Box(new Rect(m_pivotScreen - Vector2.one * 9f * UiScale, Vector2.one * 18f * UiScale), INBuildStyle.Brass, false);
            }
            else m_graphic.Circle(m_pivotScreen, Radius, INBuildStyle.Ivory);
            PositionLabel(m_translateLabel, m_pivotScreen + new Vector2(0, 130f * UiScale));
            PositionLabel(m_rotateLabel, m_pivotScreen - new Vector2(0, 110f * UiScale));
            PositionLabel(m_info, m_pivotScreen - new Vector2(0, 140f * UiScale));
            PartPose pose = PartPose.Read(m_selectedPart);
            m_info.text = string.Format("X {0:0.00}   Y {1:0.00}   {2:0.0}°  ·  再点工具取消选择", pose.position.x, pose.position.y, pose.angle);
            m_graphic.End(); return;
        }
        if (m_selected && GridlessBuild.TryGetGroupBounds(m_contraption, m_selectedGroup, out Rect bounds, 0f))
        {
            if (BuildToolPalette.Current != BuildTool.Translate && m_drag != Drag.Free && m_drag != Drag.X && m_drag != Drag.Y)
                DrawBounds(bounds, SelectedFrame, false, INBuildStyle.Brass);
            m_translateLabel.text = "Translate Machine  /  平移载具";
            m_rotateLabel.text = "Click and drag to rotate machine  /  拖动旋转";
            m_pivot = SelectedFrame.TransformPoint(bounds.center);
            m_pivotScreen = ScreenPoint(bounds.center);
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity), max = -min;
            for (int i = 0; i < 4; i++)
            {
                Vector2 p = ScreenPoint(new Vector2(i % 2 == 0 ? bounds.xMin : bounds.xMax, i < 2 ? bounds.yMin : bounds.yMax));
                min = Vector2.Min(min, p); max = Vector2.Max(max, p);
            }
            m_screenBounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            m_graphic.Circle(m_pivotScreen, Radius, INBuildStyle.Ivory);
            m_graphic.Arrow(m_pivotScreen, m_pivotScreen + Vector2.right * 104f * UiScale, new Color(0.85f, 0.32f, 0.24f), UiScale);
            m_graphic.Arrow(m_pivotScreen, m_pivotScreen + Vector2.up * 104f * UiScale, new Color(0.47f, 0.72f, 0.34f), UiScale);
            m_graphic.Box(new Rect(m_pivotScreen - Vector2.one * 9f * UiScale, Vector2.one * 18f * UiScale), INBuildStyle.Brass, false);
            PositionLabel(m_translateLabel, m_pivotScreen + new Vector2(0, 130f * UiScale));
            PositionLabel(m_rotateLabel, m_pivotScreen - new Vector2(0, 110f * UiScale));
            PositionLabel(m_info, m_pivotScreen - new Vector2(0, 140f * UiScale));
            var frame = SelectedFrame;
            m_info.text = string.Format("X {0:0.00}   Y {1:0.00}   {2:0.0}°  ·  再点工具取消选择", frame.translation.x, frame.translation.y, frame.angle);
        }
        else if (m_selected) m_selected = false;
        m_graphic.End();
    }
    private void DrawConnectionCircles()
    {
        int drawn = 0;
        foreach (BasePart part in m_contraption.Parts)
        {
            GridlessGroupMerge.GetCircles(part, m_connectionCircles);
            foreach (var point in m_connectionCircles)
            {
                Vector3 center = Camera.main.WorldToScreenPoint(point.position);
                if (center.z <= 0f || center.x < 0f || center.y < 0f || center.x > Camera.main.pixelWidth || center.y > Camera.main.pixelHeight) continue;
                Vector2 edge = Camera.main.WorldToScreenPoint(point.position + Camera.main.transform.right * point.radius);
                float radius = Vector2.Distance(center, edge);
                Color color = part == m_selectedPart || GridlessBuild.GetGroupId(part) == m_selectedGroup ? INBuildStyle.Brass : INBuildStyle.Ivory;
                m_graphic.Circle(center, radius, color, 16);
                // Bound overlay geometry independently of blueprint size; contact testing has no cap.
                if (++drawn >= 256) return;
            }
        }
    }
    private void PositionLabel(Text label, Vector2 position)
    {
        label.rectTransform.anchoredPosition = position; label.rectTransform.sizeDelta = new Vector2(440f, 28f) * UiScale;
        label.fontSize = Mathf.RoundToInt(14f * UiScale);
    }
    private void DrawBounds(Rect bounds, BuildMachineTransform frame, bool dashed, Color color)
    {
        Vector2 a = ScreenPoint(bounds.min, frame), b = ScreenPoint(new Vector2(bounds.xMax, bounds.yMin), frame), c = ScreenPoint(bounds.max, frame), d = ScreenPoint(new Vector2(bounds.xMin, bounds.yMax), frame);
        m_graphic.Line(a, b, color, dashed); m_graphic.Line(b, c, color, dashed); m_graphic.Line(c, d, color, dashed); m_graphic.Line(d, a, color, dashed);
    }
}

[ExecuteAlways]
[RequireComponent(typeof(CanvasRenderer))]
public class INBuildOverlayGraphic : MaskableGraphic
{
    private struct Segment { public Vector2 a, b; public Color color; public float width; }
    private readonly List<Segment> m_lines = new List<Segment>(512);
    public void Begin() => m_lines.Clear();
    public void End() => SetVerticesDirty();
    public void Line(Vector2 a, Vector2 b, Color color, bool dashed = false, float width = 1.8f)
    {
        if (!dashed) { m_lines.Add(new Segment { a = a, b = b, color = color, width = width }); return; }
        float length = (b - a).magnitude; if (length < 0.1f) return;
        float step = Mathf.Max(14f, length / 100f);
        for (float x = 0; x < length; x += step)
            Line(Vector2.Lerp(a, b, x / length), Vector2.Lerp(a, b, Mathf.Min(x + step * 0.55f, length) / length), color, false, width);
    }
    public void Box(Rect rect, Color color, bool dashed)
    {
        var a = rect.min; var b = new Vector2(rect.xMax, rect.yMin); var c = rect.max; var d = new Vector2(rect.xMin, rect.yMax);
        Line(a, b, color, dashed); Line(b, c, color, dashed); Line(c, d, color, dashed); Line(d, a, color, dashed);
    }
    public void Circle(Vector2 center, float radius, Color color, int segments = 72)
    {
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments, b = (i + 1) * Mathf.PI * 2f / segments;
            Line(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, color);
        }
    }
    public void Arrow(Vector2 a, Vector2 b, Color color, float scale)
    {
        Line(a, b, color, false, 3f * scale);
        Vector2 d = (b - a).normalized, n = new Vector2(-d.y, d.x);
        Line(b, b - d * 12f * scale + n * 7f * scale, color, false, 3f * scale);
        Line(b, b - d * 12f * scale - n * 7f * scale, color, false, 3f * scale);
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        foreach (Segment s in m_lines)
        {
            Vector2 n = new Vector2(-(s.b - s.a).y, (s.b - s.a).x).normalized * s.width * 0.5f;
            int i = vh.currentVertCount;
            vh.AddVert(s.a - n, s.color, Vector2.zero); vh.AddVert(s.a + n, s.color, Vector2.zero);
            vh.AddVert(s.b + n, s.color, Vector2.zero); vh.AddVert(s.b - n, s.color, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
