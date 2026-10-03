using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class INSelectionManager : MonoBehaviour
{
	public enum State
	{
		Idle,
		Selecting,
		Selected,
		PastePreview,
		ShapeDrawing
	}

	public enum ShapeType
	{
		Square,
		Rectangle,
		Circle,
		Ellipse,
		Line
	}

	public const int OVERWRITE_NONE = 0;
	public const int OVERWRITE_NORMAL = 1;
	public const int OVERWRITE_FULL = 2;

	private State m_state = State.Idle;
	private Vector3 m_selectStart;
	private Vector3 m_selectEnd;
	private int m_minX, m_minY, m_maxX, m_maxY;
	private bool m_isDragging;
	private bool m_ready;
	private int m_waitFrames;

	// Selected drag-to-move
	private Vector3 m_pasteDragStart;
	private bool m_pasteDragStarted;
	private bool m_pasteDragInBounds;
	private bool m_dragMoveFromSelected;
	private int m_dragStartFingerX, m_dragStartFingerY;
	// How many times the current selection has been rotated in place (0..3)
	private int m_selectionRotation;

	private List<GameObject> m_selectionPreviews = new List<GameObject>();
	private List<GameObject> m_pastePreviews = new List<GameObject>();
	private List<INContraptionData> m_undoStack = new List<INContraptionData>();

	private INContraptionData m_clipboard;
	private List<INContraptionData> m_clipboardSlots = new List<INContraptionData>();
	private List<int> m_clipboardSlotIds = new List<int>();
	private List<string> m_clipboardSlotNames = new List<string>();
	private int m_nextSlotId = 1;

	private Material m_selectionMat;
	private Material m_pasteMat;
	private Material m_pasteBlockedMat;
	private Material m_shapeMat;

	private Camera m_mainCamera;

	// Paste rotation
	private int m_pasteRotation;

	// Shape drawing
	private ShapeType m_currentShape = ShapeType.Rectangle;
	private bool m_shapeSolid = false;
	private Vector3 m_shapeStart;
	private Vector3 m_shapeEnd;
	private bool m_shapeDragging;

	// Material selection
	private bool m_materialOverride = false;
	private int m_materialIndex = 0;

	// Stash
	private INContraptionData m_stash;

	// Shape persistence: track confirmed shape previews for undo
	private List<List<GameObject>> m_shapePreviewLayers = new List<List<GameObject>>();
	private List<INContraptionData> m_shapeUndoData = new List<INContraptionData>();

	// Cached preview for selection only (fill quad + grid lines)
	private GameObject m_cachedFillObj;
	private Renderer m_cachedFillRenderer;
	private Material m_lineMat;
	private List<GameObject> m_cachedLineObjs = new List<GameObject>();
	private int m_lastMinX = int.MinValue, m_lastMinY = int.MinValue;
	private int m_lastMaxX = int.MinValue, m_lastMaxY = int.MinValue;

	private static INSelectionManager s_instance;
	private bool m_suppressBuild;
	private bool m_dragMoveKeepPreview;
	public static INSelectionManager Instance => s_instance;
	public static bool IsInteracting => PauseBoxDrawing || INMachineBuildEditor.IsCapturingPointer || INPartParameterPanel.ContainsPointer(Input.mousePosition) || (s_instance != null && (s_instance.m_state != State.Idle || s_instance.m_suppressBuild));
	// 暂停长按“只画框”选区预览进行中：用于禁用视角拖动等交互
	public static bool PauseBoxDrawing;

	public State CurrentState => m_state;
	public INContraptionData Clipboard => m_clipboard;
	public List<INContraptionData> ClipboardSlots => m_clipboardSlots;
	public int SelectionMinX => m_minX;
	public int SelectionMinY => m_minY;
	public int SelectionMaxX => m_maxX;
	public int SelectionMaxY => m_maxY;
	public bool IsDragMovingFromSelected => m_dragMoveFromSelected;
	public List<int> ClipboardSlotIds => m_clipboardSlotIds;
	public List<string> ClipboardSlotNames => m_clipboardSlotNames;
	public int PasteRotation => m_pasteRotation;
	public int SelectionRotation => m_selectionRotation;
	public ShapeType CurrentShape => m_currentShape;
	public bool ShapeSolid => m_shapeSolid;
	public int PasteOverwriteMode => INUserSettings.Instance?.StarSeaSettings?.ToolBookPasteOverwriteMode ?? 1;
	public bool MaterialOverride => m_materialOverride;
	public int MaterialIndex => m_materialIndex;
	public INContraptionData Stash => m_stash;
	public int ShapePreviewCount => m_shapePreviewLayers.Count;

	private void Awake()
	{
		s_instance = this;
		m_mainCamera = Camera.main;
		CreateMaterials();
		LoadClipboards();
		EventManager.Connect<PartPlacedEvent>(OnPartPlacedForCounters);
	}

	private void OnDestroy()
	{
		if (s_instance == this) s_instance = null;
		EventManager.Disconnect<PartPlacedEvent>(OnPartPlacedForCounters);
		ClearPreviews(m_selectionPreviews);
		ClearPreviews(m_pastePreviews);
		ClearAllShapePreviews();
	}

	// 部件计数器是画在部件按钮上的，凡是把部件放到载具上的路径都必须让它保持正确。会发
	// PartCountChanged 的路径自己维护了计数，但不是所有路径都发，而且被取消的拖拽已经把部件
	// 算进去了 —— 所以真正放下一个部件之后，从载具本身重新数一遍。
	private void OnPartPlacedForCounters(PartPlacedEvent data)
	{
		RefreshPartCounters();
	}

	private void CreateMaterials()
	{
		Shader shader = Shader.Find("Sprites/Default");
		if (shader == null) shader = Shader.Find("UI/Default");

		m_selectionMat = new Material(shader);
		m_selectionMat.color = new Color(0.3f, 0.5f, 1f, 0.35f);

		m_pasteMat = new Material(shader);
		m_pasteMat.color = new Color(0.3f, 1f, 0.3f, 0.4f);

		m_pasteBlockedMat = new Material(shader);
		m_pasteBlockedMat.color = new Color(1f, 0.2f, 0.2f, 0.4f);

		m_shapeMat = new Material(shader);
		m_shapeMat.color = new Color(1f, 0.8f, 0.2f, 0.4f);

		m_lineMat = new Material(shader);
		m_lineMat.color = new Color(0.3f, 0.5f, 1f, 0.5f);
	}

	private void Update()
	{
		var layerOwner = WPFMonoBehaviour.levelManager?.ContraptionProto;
		if (ContraptionLayers.Enabled(layerOwner) && ContraptionLayers.ActiveLayer == 0)
		{
			if (m_state != State.Idle) CancelPaste();
			return;
		}
		// Clear suppressBuild flag after one frame
		if (m_suppressBuild)
		{
			m_suppressBuild = false;
			return;
		}

		switch (m_state)
		{
			case State.Selecting:
				if (!m_ready)
				{
					m_waitFrames++;
					if (m_waitFrames > 3) m_ready = true;
					return;
				}
				UpdateSelecting();
				break;
			case State.PastePreview:
				UpdatePastePreview();
				// Auto-confirm on mouse-up when drag-moving from selection
				if (m_dragMoveFromSelected && Input.GetMouseButtonUp(0))
				{
					// ConfirmPaste reads m_dragMoveFromSelected to apply the drag offset,
					// so do NOT clear it before calling (it clears itself at the end).
					ConfirmPaste(true);
				}
				break;
			case State.ShapeDrawing:
				UpdateShapeDrawing();
				break;
			case State.Selected:
				UpdateSelectedDrag();
				break;
		}
	}

	// --- Frame detection ---

	private static bool IsFrameType(int sortedType)
	{
		return sortedType == 5 || sortedType == 6;
	}

	// --- Serializable wrappers for JSON ---

	[System.Serializable]
	private class SerializableUnit
	{
		public int Type;
		public int Index;
		public int X;
		public int Y;
		public int Rotation;
		public bool Flipped;
		public string CustomParams;

		public SerializableUnit() { }
		public SerializableUnit(INContraptionData.Unit u)
		{
			Type = u.Type;
			Index = u.Index;
			X = u.X;
			Y = u.Y;
			Rotation = u.Rotation;
			Flipped = u.Flipped;
			CustomParams = u.CustomParams;
		}

		public INContraptionData.Unit ToUnit()
		{
			var unit = new INContraptionData.Unit(Type, Index, X, Y, Rotation, Flipped);
			unit.CustomParams = CustomParams;
			return unit;
		}
	}

	[System.Serializable]
	private class SerializableContraptionData
	{
		public List<SerializableUnit> Units = new List<SerializableUnit>();

		public SerializableContraptionData() { }
		public SerializableContraptionData(INContraptionData data)
		{
			if (data == null || data.Units == null) return;
			foreach (var u in data.Units)
				Units.Add(new SerializableUnit(u));
		}

		public INContraptionData ToContraptionData()
		{
			var data = new INContraptionData(Units.Count);
			foreach (var su in Units)
				data.Units.Add(su.ToUnit());
			return data;
		}
	}

	[System.Serializable]
	private class ClipboardWrapper
	{
		public List<SerializableContraptionData> Slots = new List<SerializableContraptionData>();
		public List<int> SlotIds = new List<int>();
		public List<string> SlotNames = new List<string>();
		public int NextId;
	}

	private static INContraptionData DeepCopy(INContraptionData src)
	{
		if (src == null) return null;
		var copy = new INContraptionData(src.Units.Count);
		foreach (var u in src.Units)
		{
			var unit = new INContraptionData.Unit(u.Type, u.Index, u.X, u.Y, u.Rotation, u.Flipped);
			unit.CustomParams = u.CustomParams;
			copy.Units.Add(unit);
		}
		return copy;
	}

	// --- Clipboard Slots ---

	// Dedup feedback for UI
	public static bool LastSaveDuplicate;
	public static float LastSaveDuplicateTime;
	public static bool NewSavePending;

	public void SaveToClipboard()
	{
		// Bake the current paste rotation into the saved data so the archive
		// stores the real orientation.
		INContraptionData clip = GetRotatedClipboard();
		if (clip == null || clip.Units.Count == 0)
		{
			Debug.Log("Nothing to save - copy first");
			return;
		}
		// Dedup: compare unit count + positions with existing slots
		for (int i = 0; i < m_clipboardSlots.Count; i++)
		{
			if (IsContraptionDuplicate(clip, m_clipboardSlots[i]))
			{
				Debug.Log($"[Clipboard] Duplicate of slot #{m_clipboardSlotIds[i]}, skipped");
				LastSaveDuplicate = true;
				LastSaveDuplicateTime = Time.unscaledTime;
				return;
			}
		}
		// No eviction — allow unlimited storage
		LastSaveDuplicate = false;
		int newId = m_nextSlotId++;
		m_clipboardSlots.Add(DeepCopy(clip));
		m_clipboardSlotIds.Add(newId);
		m_clipboardSlotNames.Add(newId.ToString());
		m_pasteRotation = 0;
		NewSavePending = true;
		SaveClipboards();
		Debug.Log($"[Clipboard] Saved slot #{newId} ({clip.Units.Count} units). Total: {m_clipboardSlots.Count}");
	}

	private static bool IsContraptionDuplicate(INContraptionData a, INContraptionData b)
	{
		if (a == null || b == null) return false;
		if (a.Units.Count != b.Units.Count) return false;
		for (int i = 0; i < a.Units.Count; i++)
		{
			var ua = a.Units[i];
			var ub = b.Units[i];
			if (ua.X != ub.X || ua.Y != ub.Y || ua.Type != ub.Type) return false;
		}
		return true;
	}

	public void DeleteClipboardSlot(int index)
	{
		if (index < 0 || index >= m_clipboardSlots.Count) return;
		int removedId = m_clipboardSlotIds[index];
		m_clipboardSlots.RemoveAt(index);
		m_clipboardSlotIds.RemoveAt(index);
		if (index < m_clipboardSlotNames.Count) m_clipboardSlotNames.RemoveAt(index);
		SaveClipboards();
		Debug.Log("Deleted clipboard slot " + removedId);
	}

	public void RenameClipboardSlot(int index, string name)
	{
		if (index < 0 || index >= m_clipboardSlotNames.Count) return;
		m_clipboardSlotNames[index] = name ?? "";
		SaveClipboards();
		Debug.Log($"[Clipboard] Renamed slot {index} to \"{name}\"");
	}

	public void LoadClipboardSlot(int index)
	{
		if (index < 0 || index >= m_clipboardSlots.Count) return;
		m_clipboard = DeepCopy(m_clipboardSlots[index]);
		// Keep the current rotation so "转" also applies when placing from the archive.
		Debug.Log($"[Clipboard] Loaded slot #{m_clipboardSlotIds[index]} (idx {index}, {m_clipboard.Units.Count} units). All IDs: [{string.Join(",", m_clipboardSlotIds)}]");
	}

	private void SaveClipboards()
	{
		try
		{
			string path = INUnity.SettingsPath + "/ToolBookClipboard.json";
			var wrapper = new ClipboardWrapper();
			for (int i = 0; i < m_clipboardSlots.Count; i++)
			{
				wrapper.Slots.Add(new SerializableContraptionData(m_clipboardSlots[i]));
			}
			wrapper.SlotIds = new List<int>(m_clipboardSlotIds);
			wrapper.SlotNames = new List<string>(m_clipboardSlotNames);
			wrapper.NextId = m_nextSlotId;
			string json = UnityEngine.JsonUtility.ToJson(wrapper, true);
			System.IO.File.WriteAllText(path, json);
			Debug.Log("Clipboards saved to " + path + " (" + wrapper.Slots.Count + " slots)");
		}
		catch (System.Exception e)
		{
			Debug.LogWarning("Failed to save clipboard: " + e.Message);
		}
	}

	private void LoadClipboards()
	{
		try
		{
			string path = INUnity.SettingsPath + "/ToolBookClipboard.json";
			if (!System.IO.File.Exists(path)) return;
			string json = System.IO.File.ReadAllText(path);
			var wrapper = UnityEngine.JsonUtility.FromJson<ClipboardWrapper>(json);
			if (wrapper != null && wrapper.Slots != null && wrapper.Slots.Count > 0)
			{
				m_clipboardSlots.Clear();
				m_clipboardSlotIds.Clear();
				m_clipboardSlotNames.Clear();
				foreach (var s in wrapper.Slots)
					m_clipboardSlots.Add(s.ToContraptionData());

				if (wrapper.SlotIds != null && wrapper.SlotIds.Count == m_clipboardSlots.Count)
					m_clipboardSlotIds = wrapper.SlotIds;
				else
				{
					m_clipboardSlotIds.Clear();
					for (int i = 0; i < m_clipboardSlots.Count; i++)
						m_clipboardSlotIds.Add(i + 1);
				}

				if (wrapper.SlotNames != null && wrapper.SlotNames.Count == m_clipboardSlots.Count)
					m_clipboardSlotNames = wrapper.SlotNames;
				else
				{
					m_clipboardSlotNames.Clear();
					for (int i = 0; i < m_clipboardSlots.Count; i++)
						m_clipboardSlotNames.Add(m_clipboardSlotIds[i].ToString());
				}

				m_nextSlotId = wrapper.NextId > 0 ? wrapper.NextId : m_clipboardSlotIds[m_clipboardSlotIds.Count - 1] + 1;
				Debug.Log("Loaded " + m_clipboardSlots.Count + " clipboard slots from " + path);
			}
		}
		catch (System.Exception e)
		{
			Debug.LogWarning("Failed to load clipboard: " + e.Message);
		}
	}

	// --- Stash ---

	public void StashContraption()
	{
		m_stash = INContraption.CopyContraption();
		Debug.Log("Stashed contraption (" + m_stash.Units.Count + " parts)");
	}

	public void PasteStash()
	{
		if (m_stash == null || m_stash.Units.Count == 0)
		{
			Debug.Log("Nothing in stash");
			return;
		}
		m_clipboard = DeepCopy(m_stash);
		m_pasteRotation = 0;
		StartPastePreview();
	}

	public bool HasStash => m_stash != null && m_stash.Units.Count > 0;

	// --- Material Override ---

	public void ToggleMaterialOverride()
	{
		m_materialOverride = !m_materialOverride;
		Debug.Log("Material override: " + (m_materialOverride ? "ON" : "OFF"));
	}

	public void CycleMaterialIndex()
	{
		m_materialIndex = (m_materialIndex + 1) % 4;
		Debug.Log("Material index: " + m_materialIndex);
	}

	// --- Rotation ---

	public void CyclePasteRotation()
	{
		m_pasteRotation = (m_pasteRotation + 1) % 4;
		Debug.Log("Paste rotation: " + (m_pasteRotation * 90) + "\u00b0");
	}

	// Rotation button entry point.
	// - Selected: rotate the actual boxed parts in place (and refresh clipboard).
	// - PastePreview / Idle: cycle the pending paste rotation.
	public void RotateSelection()
	{
		if (m_state == State.Selected)
		{
			RotateSelectedInPlace();
			return;
		}
		if (m_state == State.Selecting) return; // ignore mid-drag
		CyclePasteRotation();
	}

	// Rotate the currently selected parts 90° clockwise around the selection and
	// put them back, staying in the Selected state.
	private void RotateSelectedInPlace()
	{
		var proto = WPFMonoBehaviour.levelManager.CurrentGameMode?.ContraptionProto;
		var ui = WPFMonoBehaviour.levelManager.ConstructionUI;
		if (proto == null || ui == null) return;
		if (m_minX > m_maxX || m_minY > m_maxY) return;

		List<BasePart> selected = new List<BasePart>();
		foreach (BasePart part in proto.Parts)
		{
			if (ContraptionLayers.Editable(part) && part.CoordX >= m_minX && part.CoordX <= m_maxX &&
				part.CoordY >= m_minY && part.CoordY <= m_maxY)
				selected.Add(part);
		}
		if (selected.Count == 0) return;

		SaveUndoState();

		// Snapshot the selection
		INContraptionData data = new INContraptionData(selected.Count);
		foreach (var p in selected)
		{
			var unit = new INContraptionData.Unit(
				(int)p.Type.ToSortedPartType(), p.Index,
				p.CoordX, p.CoordY, (int)p.Rotation, p.Flipped);
			unit.CustomParams = p.SerializeCustomParams();
			data.Units.Add(unit);
		}

		// Remove originals
		foreach (var p in selected)
		{
			var pd = ui.FindPartDesc(p.Type);
			if (pd != null) pd.useCount--;
			proto.RemovePart(p);
			Object.Destroy(p.gameObject);
		}

		// Rotate around the centroid, then re-centre on the original box centre so
		// repeated rotations stay in the same place.
		float origCx = (m_minX + m_maxX) * 0.5f;
		float origCy = (m_minY + m_maxY) * 0.5f;
		INContraptionData rotated = RotateData90CW(data);
		RecenterData(rotated, origCx, origCy);

		// New selection bounds
		int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
		foreach (var u in rotated.Units)
		{
			if (u.X < minX) minX = u.X;
			if (u.X > maxX) maxX = u.X;
			if (u.Y < minY) minY = u.Y;
			if (u.Y > maxY) maxY = u.Y;
		}
		m_minX = minX; m_maxX = maxX; m_minY = minY; m_maxY = maxY;

		// Place rotated parts
		foreach (var u in rotated.Units)
		{
			SortedPartType type = (SortedPartType)u.Type;
			ConstructionUI.PartDesc partDesc = ui.FindPartDesc(type.ToPartType());
			if (partDesc == null) continue;
			BasePart customPart = WPFMonoBehaviour.gameData.GetCustomPart(partDesc.part.m_partType, u.Index);
			if (customPart == null) continue;
			BasePart builtPart = WPFMonoBehaviour.levelManager.BuildPart(u.X, u.Y, u.Rotation, u.Flipped, customPart, u.CustomParams);
			partDesc.useCount++;
		}

		CopySelected();
		m_pasteRotation = 0;
		m_selectionRotation = (m_selectionRotation + 1) % 4;
		m_suppressBuild = true;
		RefreshPartCounters();
		UpdateCachedSelectionPreview(m_minX, m_minY, m_maxX, m_maxY);
		m_lastMinX = m_minX; m_lastMinY = m_minY;
		m_lastMaxX = m_maxX; m_lastMaxY = m_maxY;
		m_state = State.Selected;

		Debug.Log("Rotated selection 90\u00b0 clockwise (" + rotated.Units.Count + " parts)");
	}

	// 90° clockwise rotation around the unit centroid (matches GetRotatedClipboard case 1).
	private static INContraptionData RotateData90CW(INContraptionData src)
	{
		if (src == null || src.Units.Count == 0) return src;
		int cx = 0, cy = 0;
		foreach (var u in src.Units) { cx += u.X; cy += u.Y; }
		cx /= src.Units.Count;
		cy /= src.Units.Count;

		INContraptionData rotated = new INContraptionData(src.Units.Count);
		foreach (var u in src.Units)
		{
			int dx = u.X - cx;
			int dy = u.Y - cy;
			// Same 90° step as GetRotatedClipboard case 1; Flip is preserved.
			var unit = new INContraptionData.Unit(
				u.Type, u.Index, cx - dy, cy + dx, (u.Rotation + 1) % 4, u.Flipped);
			unit.CustomParams = u.CustomParams;
			rotated.Units.Add(unit);
		}
		return rotated;
	}

	// Shift the data so its bounding-box centre matches the target centre.
	private static void RecenterData(INContraptionData data, float targetCx, float targetCy)
	{
		if (data == null || data.Units.Count == 0) return;
		int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
		foreach (var u in data.Units)
		{
			if (u.X < minX) minX = u.X;
			if (u.X > maxX) maxX = u.X;
			if (u.Y < minY) minY = u.Y;
			if (u.Y > maxY) maxY = u.Y;
		}
		int shiftX = Mathf.RoundToInt(targetCx - (minX + maxX) * 0.5f);
		int shiftY = Mathf.RoundToInt(targetCy - (minY + maxY) * 0.5f);
		if (shiftX == 0 && shiftY == 0) return;
		foreach (var u in data.Units) { u.X += shiftX; u.Y += shiftY; }
	}

	private INContraptionData GetRotatedClipboard()
	{
		if (m_clipboard == null || m_clipboard.Units.Count == 0) return m_clipboard;
		if (m_pasteRotation == 0) return m_clipboard;

		int cx = 0, cy = 0;
		foreach (var u in m_clipboard.Units) { cx += u.X; cy += u.Y; }
		cx /= m_clipboard.Units.Count;
		cy /= m_clipboard.Units.Count;

		INContraptionData rotated = new INContraptionData();
		foreach (var u in m_clipboard.Units)
		{
			int newX = u.X, newY = u.Y;
			int newRot = (u.Rotation + m_pasteRotation) % 4;
			bool newFlipped = u.Flipped; // rotation must never change Flip

			int dx = u.X - cx;
			int dy = u.Y - cy;

			switch (m_pasteRotation)
			{
				case 1: newX = cx - dy; newY = cy + dx; break;
				case 2: newX = cx - dx; newY = cy - dy; break;
				case 3: newX = cx + dy; newY = cy - dx; break;
			}

			var unit = new INContraptionData.Unit(u.Type, u.Index, newX, newY, newRot, newFlipped);
			unit.CustomParams = u.CustomParams;
			rotated.Units.Add(unit);
		}
		return rotated;
	}

	// --- State Transitions ---

	public void EnterSelectionMode()
	{
		if (m_state == State.PastePreview) CancelPaste();
		if (m_state == State.ShapeDrawing) CancelShape();
		ClearPreviews(m_selectionPreviews);
		m_state = State.Selecting;
		m_clipboard = null;
		m_isDragging = false;
		m_ready = false;
		m_waitFrames = 0;
		m_selectionRotation = 0;
	}

	public void ExitSelectionMode()
	{
		ClearPreviews(m_selectionPreviews);
		ReleaseCachedPreview();
		m_state = State.Idle;
		m_isDragging = false;
	}

	// --- Shape Drawing ---

	public void SetShapeType(ShapeType type)
	{
		m_currentShape = type;
		Debug.Log("Shape: " + type);
	}

	public void CycleShapeType()
	{
		m_currentShape = (ShapeType)(((int)m_currentShape + 1) % 5);
		Debug.Log("Shape: " + m_currentShape);
	}

	public void ToggleShapeSolid()
	{
		m_shapeSolid = !m_shapeSolid;
		Debug.Log("Shape solid: " + m_shapeSolid);
	}

	public void EnterShapeDrawing()
	{
		if (m_state == State.PastePreview) CancelPaste();
		if (m_state == State.Selecting) ExitSelectionMode();
		m_state = State.ShapeDrawing;
		m_shapeDragging = false;
		m_ready = false;
		m_waitFrames = 0;
	}

	private void UpdateShapeDrawing()
	{
		if (!m_ready)
		{
			m_waitFrames++;
			if (m_waitFrames > 3) m_ready = true;
			return;
		}

		if (Input.GetMouseButtonDown(0))
		{
			m_shapeStart = GetWorldPos(Input.mousePosition);
			m_shapeDragging = true;
		}

		if (m_shapeDragging && Input.GetMouseButton(0))
		{
			m_shapeEnd = GetWorldPos(Input.mousePosition);
			UpdateShapePreview();
		}

		if (Input.GetMouseButtonUp(0) && m_shapeDragging)
		{
			m_shapeDragging = false;
			m_shapeEnd = GetWorldPos(Input.mousePosition);
			UpdateShapePreview();
			ConfirmShape();
		}
	}

	private void UpdateShapePreview()
	{
		ClearPreviews(m_selectionPreviews);

		Vector3 origin = WPFMonoBehaviour.levelManager.StartingPosition;
		int x0 = Mathf.FloorToInt(m_shapeStart.x - origin.x + 0.5f);
		int y0 = Mathf.FloorToInt(m_shapeStart.y - origin.y + 0.5f);
		int x1 = Mathf.FloorToInt(m_shapeEnd.x - origin.x + 0.5f);
		int y1 = Mathf.FloorToInt(m_shapeEnd.y - origin.y + 0.5f);

		HashSet<Vector2Int> cells = GetShapeCells(x0, y0, x1, y1, m_currentShape, m_shapeSolid);

		foreach (var cell in cells)
		{
			CreateQuad(m_selectionPreviews, m_shapeMat, cell.x, cell.y);
		}
	}

	public void ConfirmShape()
	{
		if (m_state != State.ShapeDrawing) return;

		// Save the preview quads as a persistent visual layer (no auto-fill)
		List<GameObject> layer = new List<GameObject>(m_selectionPreviews);
		m_selectionPreviews.Clear(); // Don't destroy - they stay on screen

		if (layer.Count == 0)
		{
			m_state = State.Idle;
			return;
		}

		m_shapePreviewLayers.Add(layer);

		Debug.Log("Shape confirmed (" + m_currentShape + (m_shapeSolid ? " solid" : " hollow") + "), " + m_shapePreviewLayers.Count + " layers on screen");
		m_state = State.Idle;
	}

	public void CancelShape()
	{
		ClearPreviews(m_selectionPreviews);
		m_shapeDragging = false;
		m_state = State.Idle;
	}

	private HashSet<Vector2Int> GetShapeCells(int x0, int y0, int x1, int y1, ShapeType shape, bool solid)
	{
		HashSet<Vector2Int> cells = new HashSet<Vector2Int>();

		int minX = Mathf.Min(x0, x1);
		int maxX = Mathf.Max(x0, x1);
		int minY = Mathf.Min(y0, y1);
		int maxY = Mathf.Max(y0, y1);

		switch (shape)
		{
			case ShapeType.Rectangle:
				for (int x = minX; x <= maxX; x++)
					for (int y = minY; y <= maxY; y++)
						if (solid || x == minX || x == maxX || y == minY || y == maxY)
							cells.Add(new Vector2Int(x, y));
				break;

			case ShapeType.Square:
				int side = Mathf.Max(maxX - minX, maxY - minY);
				maxX = minX + side;
				maxY = minY + side;
				for (int x = minX; x <= maxX; x++)
					for (int y = minY; y <= maxY; y++)
						if (solid || x == minX || x == maxX || y == minY || y == maxY)
							cells.Add(new Vector2Int(x, y));
				break;

			case ShapeType.Circle:
				int radius = Mathf.Max(maxX - minX, maxY - minY) / 2;
				int cx = (minX + maxX) / 2;
				int cy = (minY + maxY) / 2;
				for (int x = cx - radius; x <= cx + radius; x++)
					for (int y = cy - radius; y <= cy + radius; y++)
					{
						float dist = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
						if (solid)
						{ if (dist <= radius + 0.5f) cells.Add(new Vector2Int(x, y)); }
						else
						{ if (dist >= radius - 0.5f && dist <= radius + 0.5f) cells.Add(new Vector2Int(x, y)); }
					}
				break;

			case ShapeType.Ellipse:
				float a = (maxX - minX) / 2f;
				float b = (maxY - minY) / 2f;
				float ecx = (minX + maxX) / 2f;
				float ecy = (minY + maxY) / 2f;
				if (a < 0.5f) a = 0.5f;
				if (b < 0.5f) b = 0.5f;
				for (int x = minX; x <= maxX; x++)
					for (int y = minY; y <= maxY; y++)
					{
						float norm = ((x - ecx) * (x - ecx)) / (a * a) + ((y - ecy) * (y - ecy)) / (b * b);
						if (solid)
						{ if (norm <= 1.0f) cells.Add(new Vector2Int(x, y)); }
						else
						{ if (norm >= 0.6f && norm <= 1.4f) cells.Add(new Vector2Int(x, y)); }
					}
				break;

			case ShapeType.Line:
				int lx0 = x0, ly0 = y0, lx1 = x1, ly1 = y1;
				int dx = Mathf.Abs(lx1 - lx0), dy = Mathf.Abs(ly1 - ly0);
				int sx = lx0 < lx1 ? 1 : -1, sy = ly0 < ly1 ? 1 : -1;
				int err = dx - dy;
				while (true)
				{
					cells.Add(new Vector2Int(lx0, ly0));
					if (lx0 == lx1 && ly0 == ly1) break;
					int e2 = 2 * err;
					if (e2 > -dy) { err -= dy; lx0 += sx; }
					if (e2 < dx) { err += dx; ly0 += sy; }
				}
				break;
		}

		return cells;
	}

	// --- Fill: auto-place the currently selected build part ---

	public void FillWithSelectedPart()
	{
		// Find the currently selected part from ConstructionUI
		ConstructionUI ui = WPFMonoBehaviour.levelManager.ConstructionUI;
		if (ui == null)
		{
			Debug.Log("No ConstructionUI found");
			return;
		}

		// Get the selected part descriptor
		var partDescs = ui.PartDescriptors;
		ConstructionUI.PartDesc selectedDesc = null;

		// Try to find the part that the user currently has selected (the one they'd place by clicking)
		// ConstructionUI tracks m_selectedElement internally; we check unlocked parts
		foreach (var pd in partDescs)
		{
			if (pd != null && pd.part != null && pd.CurrentCount > 0)
			{
				// Check if this part is currently selected by looking at the drag state
				// The first available part with remaining count is a reasonable default
				if (selectedDesc == null) selectedDesc = pd;
			}
		}

		// Try to get the part at the mouse position (what the user would place next)
		Vector3 worldPos = GetWorldPos(Input.mousePosition);
		Vector3 origin = WPFMonoBehaviour.levelManager.StartingPosition;
		int targetX = Mathf.FloorToInt(worldPos.x - origin.x + 0.5f);
		int targetY = Mathf.FloorToInt(worldPos.y - origin.y + 0.5f);

		// Check if there's already a part at the mouse position to identify what the user is working with
		var proto = WPFMonoBehaviour.levelManager.CurrentGameMode?.ContraptionProto;
		BasePart partAtCursor = proto?.FindPartAt(targetX, targetY);

		if (partAtCursor != null)
		{
			// Use the same type as the part under cursor
			SortedPartType cursorType = partAtCursor.Type.ToSortedPartType();
			foreach (var pd in partDescs)
			{
				if (pd != null && pd.part != null &&
					pd.part.Type.ToSortedPartType() == cursorType && pd.CurrentCount > 0)
				{
					selectedDesc = pd;
					break;
				}
			}
		}

		if (selectedDesc == null || selectedDesc.part == null)
		{
			Debug.Log("No part available for fill");
			return;
		}

		SaveUndoState();

		// Place one instance of the selected part at the cursor position
		BasePart customPart = WPFMonoBehaviour.gameData.GetCustomPart(selectedDesc.part.m_partType, selectedDesc.customPartIndex);
		if (customPart != null)
		{
			int overwriteMode = PasteOverwriteMode;
			BasePart existing = proto?.FindPartAt(targetX, targetY);

			if (overwriteMode == OVERWRITE_NONE && existing != null)
			{
				Debug.Log("Fill: cell occupied, skipping (overwrite=none)");
				return;
			}
			if (overwriteMode == OVERWRITE_NORMAL && existing != null && IsFrameType((int)existing.Type.ToSortedPartType()))
			{
				Debug.Log("Fill: cell has frame, skipping (overwrite=normal)");
				return;
			}
			if (existing != null && (overwriteMode == OVERWRITE_FULL || overwriteMode == OVERWRITE_NORMAL))
			{
				proto.RemovePart(existing);
				Object.Destroy(existing.gameObject);
			}

			WPFMonoBehaviour.levelManager.BuildPart(targetX, targetY,
				m_materialOverride ? 0 : (int)partAtCursor?.Rotation,
				false, customPart);
			selectedDesc.useCount++;
			Debug.Log("Fill: placed " + selectedDesc.part.m_partType + " at (" + targetX + "," + targetY + ")");
		}
	}

	// --- Delete Selection ---

	public void DeleteSelection()
	{
		if (m_state != State.Selected && m_state != State.Selecting) return;
		var proto = WPFMonoBehaviour.levelManager.CurrentGameMode?.ContraptionProto;
		if (proto == null) return;

		SaveUndoState();

		var ui = WPFMonoBehaviour.levelManager.ConstructionUI;
		List<BasePart> toRemove = new List<BasePart>();
		foreach (BasePart part in proto.Parts)
		{
			if (ContraptionLayers.Editable(part) && part.CoordX >= m_minX && part.CoordX <= m_maxX &&
				part.CoordY >= m_minY && part.CoordY <= m_maxY)
				toRemove.Add(part);
		}

		int removed = 0;
		foreach (BasePart part in toRemove)
		{
			if (part != null)
			{
				if (ui != null) { if (ui.RemoveBuildPart(part)) removed++; }
				else if (!part.m_static && proto.TryRemovePart(part)) { Object.Destroy(part.gameObject); removed++; }
			}
		}

		Debug.Log("Deleted " + removed + " parts from selection");
		if (!m_dragMoveKeepPreview)
		{
			ClearPreviews(m_selectionPreviews);
			ReleaseCachedPreview();
		}
		m_suppressBuild = true;
		RefreshPartCounters();
		m_state = State.Idle;
		m_isDragging = false;
	}

	// --- Selection Input ---

	private void UpdateSelecting()
	{
		if (Input.GetMouseButtonDown(0))
		{
			m_selectStart = GetWorldPos(Input.mousePosition);
			m_isDragging = true;
		}

		if (m_isDragging && Input.GetMouseButton(0))
		{
			m_selectEnd = GetWorldPos(Input.mousePosition);
			UpdateSelectionRect();
		}

		if (Input.GetMouseButtonUp(0) && m_isDragging)
		{
			m_isDragging = false;
			m_selectEnd = GetWorldPos(Input.mousePosition);
			UpdateSelectionRect();
			if (m_minX <= m_maxX && m_minY <= m_maxY)
			{
				m_selectionRotation = 0;
				m_state = State.Selected;
				CopySelected();
			}
		}
	}

	private void UpdateSelectionRect()
	{
		Vector3 origin = WPFMonoBehaviour.levelManager.StartingPosition;
		int sx = Mathf.FloorToInt(m_selectStart.x - origin.x + 0.5f);
		int sy = Mathf.FloorToInt(m_selectStart.y - origin.y + 0.5f);
		int ex = Mathf.FloorToInt(m_selectEnd.x - origin.x + 0.5f);
		int ey = Mathf.FloorToInt(m_selectEnd.y - origin.y + 0.5f);

		m_minX = Mathf.Min(sx, ex);
		m_maxX = Mathf.Max(sx, ex);
		m_minY = Mathf.Min(sy, ey);
		m_maxY = Mathf.Max(sy, ey);

		if (m_minX == m_lastMinX && m_minY == m_lastMinY &&
			m_maxX == m_lastMaxX && m_maxY == m_lastMaxY)
			return;

		UpdateCachedSelectionPreview(m_minX, m_minY, m_maxX, m_maxY);
		m_lastMinX = m_minX; m_lastMinY = m_minY;
		m_lastMaxX = m_maxX; m_lastMaxY = m_maxY;
	}

	// --- Selected Drag (move selected parts) ---

	private void UpdateSelectedDrag()
	{
		if (Input.GetMouseButtonDown(0))
		{
			m_pasteDragStart = GetWorldPos(Input.mousePosition);
			m_pasteDragStarted = false;
			// Only allow drag-to-move when the press starts inside the selection box
			m_pasteDragInBounds = IsInsideSelection(m_pasteDragStart);
		}

		if (Input.GetMouseButton(0))
		{
			Vector3 cur = GetWorldPos(Input.mousePosition);
			if (m_pasteDragInBounds && !m_pasteDragStarted && Vector3.Distance(cur, m_pasteDragStart) > 0.3f)
			{
				m_pasteDragStarted = true;
				m_dragMoveFromSelected = true;
				m_dragMoveKeepPreview = true;

				// Record the finger's grid cell at drag start; paste offset is
				// (current cell - start cell), so the box starts exactly in place.
				Vector3 origin = WPFMonoBehaviour.levelManager.StartingPosition;
				m_dragStartFingerX = Mathf.FloorToInt(m_pasteDragStart.x - origin.x + 0.5f);
				m_dragStartFingerY = Mathf.FloorToInt(m_pasteDragStart.y - origin.y + 0.5f);

				DeleteSelection();
				m_dragMoveKeepPreview = false;
				m_suppressBuild = false;
				StartPastePreview();
			}
		}

		if (Input.GetMouseButtonUp(0) && !m_pasteDragStarted)
		{
			// Simple click in Selected state — do nothing (keep selection)
			m_pasteDragStarted = false;
			m_pasteDragInBounds = false;
		}
	}

	// True when the world position falls inside the visual selection box
	private bool IsInsideSelection(Vector3 worldPos)
	{
		if (m_minX > m_maxX || m_minY > m_maxY) return false;
		Vector3 origin = WPFMonoBehaviour.levelManager.StartingPosition;
		float gx = worldPos.x - origin.x;
		float gy = worldPos.y - origin.y;
		return gx >= m_minX - 0.5f && gx <= m_maxX + 0.5f &&
			   gy >= m_minY - 0.5f && gy <= m_maxY + 0.5f;
	}

	// --- Copy ---

	private void CopySelected()
	{
		var proto = WPFMonoBehaviour.levelManager.CurrentGameMode?.ContraptionProto;
		if (proto == null || proto.Parts.Count == 0) return;

		INContraptionData data = new INContraptionData();
		foreach (BasePart part in proto.Parts)
		{
			if (ContraptionLayers.Editable(part) && part.CoordX >= m_minX && part.CoordX <= m_maxX &&
				part.CoordY >= m_minY && part.CoordY <= m_maxY)
			{
				var unit = new INContraptionData.Unit(
					(int)part.Type.ToSortedPartType(), part.Index,
					part.CoordX, part.CoordY, (int)part.Rotation, part.Flipped);
				unit.CustomParams = part.SerializeCustomParams();
				data.Units.Add(unit);
			}
		}

		if (data.Units.Count > 0)
		{
			m_clipboard = data;
			m_pasteRotation = 0;
			Debug.Log("Copied " + data.Units.Count + " parts");
		}
	}

	public void CopyFullContraption()
	{
		m_clipboard = INContraption.CopyContraption();
		m_pasteRotation = 0;
	}

	// Cut: copy selection then delete originals.
	// 没有框选时不做任何事（旧行为会把整辆载具删掉并破坏粘贴，已移除），仅给出提示。
	public void CutSelected()
	{
		if (m_state != State.Selected && m_state != State.Selecting)
		{
			Innovation.BP.Feedback("BP> Nothing selected - cut cancelled");
			Debug.Log("[ToolBook] Cut ignored: nothing selected");
			return;
		}
		// Cut selected area
		CopySelected();
		DeleteSelection();
		Debug.Log("Cut selection to clipboard");
	}

	// --- Paste Preview ---

	public void StartPastePreview()
	{
		if (m_clipboard == null || m_clipboard.Units.Count == 0) return;
		// Keep the current m_pasteRotation so a rotation set before pasting is honoured.
		m_state = State.PastePreview;
		ClearPreviews(m_pastePreviews);
	}

	private void UpdatePastePreview()
	{
		INContraptionData clip = GetRotatedClipboard();
		if (clip == null) return;

		Vector3 origin = WPFMonoBehaviour.levelManager.StartingPosition;
		Vector3 worldPos = GetWorldPos(Input.mousePosition);
		int targetX = Mathf.FloorToInt(worldPos.x - origin.x + 0.5f);
		int targetY = Mathf.FloorToInt(worldPos.y - origin.y + 0.5f);

		int cx = 0, cy = 0;
		foreach (var u in clip.Units) { cx += u.X; cy += u.Y; }
		cx /= clip.Units.Count;
		cy /= clip.Units.Count;

		int ox, oy;
		if (m_dragMoveFromSelected)
		{
			ox = targetX - m_dragStartFingerX;
			oy = targetY - m_dragStartFingerY;
		}
		else
		{
			ox = targetX - cx;
			oy = targetY - cy;
		}

		ClearPreviews(m_pastePreviews);

		var proto = WPFMonoBehaviour.levelManager.CurrentGameMode?.ContraptionProto;
		int overwriteMode = PasteOverwriteMode;

		foreach (var u in clip.Units)
		{
			int px = u.X + ox;
			int py = u.Y + oy;
			BasePart existing = proto != null ? proto.FindPartAtInLayer(px, py, 0, ContraptionLayers.SavedLayer(u.CustomParams)) : null;
			bool occupied = existing != null;

			if (!occupied)
				CreateQuad(m_pastePreviews, m_pasteMat, px, py);
			else if (overwriteMode == OVERWRITE_NONE)
				CreateQuad(m_pastePreviews, m_pasteBlockedMat, px, py);
			else if (overwriteMode == OVERWRITE_NORMAL && IsFrameType((int)existing.Type.ToSortedPartType()))
				CreateQuad(m_pastePreviews, m_pasteBlockedMat, px, py);
			else
				CreateQuad(m_pastePreviews, m_pasteMat, px, py);
		}

		if (m_dragMoveFromSelected)
			UpdateCachedSelectionPreview(m_minX + ox, m_minY + oy, m_maxX + ox, m_maxY + oy);
	}

	// --- Confirm / Cancel ---

	public void ConfirmPaste(bool returnToSelected = false)
	{
		if (ContraptionLayers.Enabled(WPFMonoBehaviour.levelManager?.ContraptionProto) && ContraptionLayers.ActiveLayer == 0) return;
		INContraptionData clip = GetRotatedClipboard();
		if (clip == null || clip.Units.Count == 0) return;

		Vector3 origin = WPFMonoBehaviour.levelManager.StartingPosition;
		Vector3 worldPos = GetWorldPos(Input.mousePosition);
		int targetX = Mathf.FloorToInt(worldPos.x - origin.x + 0.5f);
		int targetY = Mathf.FloorToInt(worldPos.y - origin.y + 0.5f);

		int cx = 0, cy = 0;
		foreach (var u in clip.Units) { cx += u.X; cy += u.Y; }
		cx /= clip.Units.Count;
		cy /= clip.Units.Count;

		int ox, oy;
		if (m_dragMoveFromSelected)
		{
			ox = targetX - m_dragStartFingerX;
			oy = targetY - m_dragStartFingerY;
		}
		else
		{
			ox = targetX - cx;
			oy = targetY - cy;
		}

		SaveUndoState();

		var proto = WPFMonoBehaviour.levelManager.CurrentGameMode?.ContraptionProto;
		int overwriteMode = PasteOverwriteMode;
		int placed = 0;

		// Snapshot occupied cells BEFORE any placement (for NONE-mode skip check)
		HashSet<(int, int, int)> occupiedBefore = new HashSet<(int, int, int)>();
		if (overwriteMode == OVERWRITE_NONE && proto != null)
		{
			foreach (BasePart part in proto.Parts)
				occupiedBefore.Add((part.CoordX, part.CoordY, ContraptionLayers.CellLayer(proto, PartPhysicsParameters.Layer(part))));
		}

		// Pre-clear all parts in target cells (handles frame+inner at same cell)
		if (overwriteMode != OVERWRITE_NONE && proto != null)
		{
			HashSet<(int, int, int)> targetCells = new HashSet<(int, int, int)>();
			foreach (var u in clip.Units)
				targetCells.Add((u.X + ox, u.Y + oy, ContraptionLayers.CellLayer(proto, ContraptionLayers.SavedLayer(u.CustomParams))));

			List<BasePart> toRemove = new List<BasePart>();
			foreach (BasePart part in proto.Parts)
			{
				if (targetCells.Contains((part.CoordX, part.CoordY, ContraptionLayers.CellLayer(proto, PartPhysicsParameters.Layer(part)))))
				{
					if (overwriteMode == OVERWRITE_NORMAL && IsFrameType((int)part.Type.ToSortedPartType()))
						continue;
					toRemove.Add(part);
				}
			}
			foreach (BasePart part in toRemove)
			{
				proto.RemovePart(part);
				Object.Destroy(part.gameObject);
			}
		}

		foreach (var u in clip.Units)
		{
			int px = u.X + ox;
			int py = u.Y + oy;

			// Use pre-paste snapshot for NONE check (avoids skipping inner part at same cell as just-placed frame)
			if (overwriteMode == OVERWRITE_NONE && occupiedBefore.Contains((px, py, ContraptionLayers.CellLayer(proto, ContraptionLayers.SavedLayer(u.CustomParams)))))
				continue;

			SortedPartType type = (SortedPartType)u.Type;

			if (m_materialOverride && (type == SortedPartType.WoodenFrame || type == SortedPartType.MetalFrame))
			{
				type = m_materialIndex > 0 ? SortedPartType.MetalFrame : SortedPartType.WoodenFrame;
			}

			ConstructionUI.PartDesc partDesc = WPFMonoBehaviour.levelManager.ConstructionUI.FindPartDesc(type.ToPartType());
			if (partDesc != null)
			{
				BasePart customPart = WPFMonoBehaviour.gameData.GetCustomPart(partDesc.part.m_partType, u.Index);
				if (customPart != null)
				{
					BasePart builtPart = WPFMonoBehaviour.levelManager.BuildPart(px, py, u.Rotation, u.Flipped, customPart, u.CustomParams);
					partDesc.useCount++;
					placed++;
				}
			}
		}

		Debug.Log("Pasted " + placed + " parts (rot " + (m_pasteRotation * 90) + "\u00b0)");
		ClearPreviews(m_pastePreviews);
		m_pasteRotation = 0;
		m_dragMoveFromSelected = false;
		m_suppressBuild = true;
		RefreshPartCounters();

		if (returnToSelected && placed > 0)
		{
			// Update selection bounds to new position and stay in Selected state
			m_minX += ox;
			m_maxX += ox;
			m_minY += oy;
			m_maxY += oy;
			CopySelected();
			UpdateCachedSelectionPreview(m_minX, m_minY, m_maxX, m_maxY);
			m_selectionRotation = 0;
			m_state = State.Selected;
		}
		else
		{
			ClearPreviews(m_selectionPreviews);
			ReleaseCachedPreview();
			m_state = State.Idle;
		}
	}

	public void CancelPaste()
	{
		ClearPreviews(m_pastePreviews);
		ClearPreviews(m_selectionPreviews);
		ReleaseCachedPreview();
		m_pasteRotation = 0;
		m_dragMoveFromSelected = false;
		m_suppressBuild = true;
		m_state = State.Idle;
	}

	// --- Undo ---

	private void SaveUndoState()
	{
		var proto = WPFMonoBehaviour.levelManager.CurrentGameMode?.ContraptionProto;
		if (proto == null) return;

		INContraptionData snap = new INContraptionData(proto.Parts.Count);
		foreach (BasePart p in proto.Parts)
		{
			var unit = new INContraptionData.Unit(
				(int)p.Type.ToSortedPartType(), p.Index,
				p.CoordX, p.CoordY, (int)p.Rotation, p.Flipped);
			unit.CustomParams = p.SerializeCustomParams();
			snap.Units.Add(unit);
		}
		m_undoStack.Add(snap);
		if (m_undoStack.Count > 20) m_undoStack.RemoveAt(0);
	}

	public void Undo()
	{
		if (ContraptionLayers.Enabled(WPFMonoBehaviour.levelManager?.ContraptionProto) && ContraptionLayers.ActiveLayer == 0) return;
		if (m_undoStack.Count == 0) return;
		if (m_state == State.PastePreview) CancelPaste();
		if (m_state == State.ShapeDrawing) CancelShape();

		INContraptionData snap = m_undoStack[m_undoStack.Count - 1];
		m_undoStack.RemoveAt(m_undoStack.Count - 1);

		var proto = WPFMonoBehaviour.levelManager.CurrentGameMode?.ContraptionProto;
		if (proto == null) return;

		List<BasePart> toRemove = new List<BasePart>(proto.Parts);
		foreach (BasePart p in toRemove)
		{
			if (p != null) Object.Destroy(p.gameObject);
		}
		proto.ClearBuildParts();

		foreach (var u in snap.Units)
		{
			SortedPartType type = (SortedPartType)u.Type;
			ConstructionUI.PartDesc pd = WPFMonoBehaviour.levelManager.ConstructionUI.FindPartDesc(type.ToPartType());
			if (pd != null)
			{
				BasePart cp = WPFMonoBehaviour.gameData.GetCustomPart(pd.part.m_partType, u.Index);
				if (cp != null)
				{
					BasePart builtPart = WPFMonoBehaviour.levelManager.BuildPart(u.X, u.Y, u.Rotation, u.Flipped, cp, u.CustomParams);
				}
			}
		}

		// Also remove the last shape preview layer if any exist
		if (m_shapePreviewLayers.Count > 0)
		{
			int lastIdx = m_shapePreviewLayers.Count - 1;
			ClearPreviews(m_shapePreviewLayers[lastIdx]);
			m_shapePreviewLayers.RemoveAt(lastIdx);
			if (m_shapeUndoData.Count > lastIdx)
				m_shapeUndoData.RemoveAt(lastIdx);
			Debug.Log("Undo: removed shape preview layer (" + m_shapePreviewLayers.Count + " remaining)");
		}

		Debug.Log("Undo: restored " + snap.Units.Count + " parts");
	}

	private void ClearAllShapePreviews()
	{
		foreach (var layer in m_shapePreviewLayers)
			ClearPreviews(layer);
		m_shapePreviewLayers.Clear();
		m_shapeUndoData.Clear();
	}

	// --- Helpers ---

	private void CreateQuad(List<GameObject> list, Material mat, int x, int y)
	{
		Vector3 origin = WPFMonoBehaviour.levelManager.StartingPosition;
		GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
		go.name = "Preview";
		go.transform.position = new Vector3(origin.x + x, origin.y + y, -2f);
		go.transform.localScale = Vector3.one * 0.95f;
		var r = go.GetComponent<Renderer>();
		r.material = mat;
		var c = go.GetComponent<Collider>();
		if (c != null) Destroy(c);
		list.Add(go);
	}

	// Sync useCount with actual parts on contraption and fire UI events
	private void RefreshPartCounters()
	{
		var ui = WPFMonoBehaviour.levelManager.ConstructionUI;
		if (ui == null) return;
		var proto = WPFMonoBehaviour.levelManager.CurrentGameMode?.ContraptionProto;
		if (proto == null) return;
		var counts = new Dictionary<(BasePart.PartType type, int index), int>();
		foreach (BasePart p in proto.Parts)
		{
			if (p == null || p.m_static) continue;
			var identity = (p.Type, p is RCR2Port.RoverPortPart ? p.customPartIndex : -1);
			counts.TryGetValue(identity, out int count); counts[identity] = count + 1;
		}
		foreach (var pd in ui.PartDescriptors)
		{
			if (pd == null || pd.part == null) continue;
			var identity = (pd.part.Type, pd.part is RCR2Port.RoverPortPart ? pd.part.customPartIndex : -1);
			counts.TryGetValue(identity, out int actual);
			if (pd.useCount != actual)
			{
				pd.useCount = actual;
				EventManager.Send(new PartCountChanged(pd.part.m_partType, pd.CurrentCount, pd.part.customPartIndex));
			}
		}
	}

	private void ClearPreviews(List<GameObject> list)
	{
		foreach (var go in list) { if (go != null) Destroy(go); }
		list.Clear();
	}

	// Cached selection preview: fill quad + grid lines
	private void UpdateCachedSelectionPreview(int minX, int minY, int maxX, int maxY)
	{
		Vector3 origin = WPFMonoBehaviour.levelManager.StartingPosition;
		int w = maxX - minX + 1;
		int h = maxY - minY + 1;
		float cx = origin.x + (minX + maxX) * 0.5f;
		float cy = origin.y + (minY + maxY) * 0.5f;

		if (m_cachedFillObj == null)
		{
			m_cachedFillObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
			m_cachedFillObj.name = "SelectionFill";
			var col = m_cachedFillObj.GetComponent<Collider>();
			if (col != null) Destroy(col);
			m_cachedFillRenderer = m_cachedFillObj.GetComponent<Renderer>();
		}
		m_cachedFillObj.transform.position = new Vector3(cx, cy, -2f);
		m_cachedFillObj.transform.localScale = new Vector3(w, h, 1f);
		if (m_cachedFillRenderer.sharedMaterial != m_selectionMat) m_cachedFillRenderer.sharedMaterial = m_selectionMat;
		m_cachedFillObj.SetActive(true);

		int hLineCount = h + 1;
		int vLineCount = w + 1;
		int totalLines = hLineCount + vLineCount;
		float lineThickness = 0.12f;

		while (m_cachedLineObjs.Count < totalLines)
		{
			GameObject line = GameObject.CreatePrimitive(PrimitiveType.Quad);
			line.name = "GridLine";
			var lc = line.GetComponent<Collider>();
			if (lc != null) Destroy(lc);
			line.SetActive(false);
			m_cachedLineObjs.Add(line);
		}

		for (int i = totalLines; i < m_cachedLineObjs.Count; i++)
			m_cachedLineObjs[i].SetActive(false);

		float left = origin.x + minX - 0.5f;
		float bottom = origin.y + minY - 0.5f;

		for (int i = 0; i < hLineCount; i++)
		{
			float y = bottom + i;
			GameObject line = m_cachedLineObjs[i];
			line.SetActive(true);
			line.transform.position = new Vector3(cx, y, -2.01f);
			line.transform.localScale = new Vector3(w, lineThickness, 1f);
			line.GetComponent<Renderer>().sharedMaterial = m_lineMat;
		}

		for (int i = 0; i < vLineCount; i++)
		{
			float x = left + i;
			GameObject line = m_cachedLineObjs[hLineCount + i];
			line.SetActive(true);
			line.transform.position = new Vector3(x, cy, -2.01f);
			line.transform.localScale = new Vector3(lineThickness, h, 1f);
			line.GetComponent<Renderer>().sharedMaterial = m_lineMat;
		}
	}

	private void ReleaseCachedPreview()
	{
		if (m_cachedFillObj != null) { Destroy(m_cachedFillObj); m_cachedFillObj = null; m_cachedFillRenderer = null; }
		foreach (var line in m_cachedLineObjs) { if (line != null) Destroy(line); }
		m_cachedLineObjs.Clear();
		m_lastMinX = m_lastMinY = m_lastMaxX = m_lastMaxY = int.MinValue;
	}

	private Vector3 GetWorldPos(Vector3 screenPos)
	{
		if (m_mainCamera == null) m_mainCamera = Camera.main;
		if (m_mainCamera == null) return Vector3.zero;
		Vector3 wp = m_mainCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 10f));
		wp.z = 0f;
		return wp;
	}
}
