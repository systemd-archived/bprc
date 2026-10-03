using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

public class SelectableText : Selectable, ICanvasElement, IInitializePotentialDragHandler, IEventSystemHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IUpdateSelectedHandler
{
	public struct SelectionChangedEventArgs
	{
		public int StartIndex;

		public int EndIndex;

		public Vector2 ScreenPoint;

		public SelectionChangedEventArgs(int startIndex, int endIndex, Vector2 screenPoint)
		{
			StartIndex = startIndex;
			EndIndex = endIndex;
			ScreenPoint = screenPoint;
		}
	}

	public class SelectionChangedEvent : UnityEvent<SelectionChangedEventArgs>
	{
	}

	[SerializeField]
	private Text m_textComponent;

	[SerializeField]
	private Color m_selectionColor;

	private bool m_selecting;

	private int m_startPosition;

	private int m_endPosition;

	private CanvasRenderer m_renderer;

	private Mesh m_mesh;

	private StringBuilder m_builder;

	private SelectionChangedEvent m_selectionChanged = new SelectionChangedEvent();

	private List<(int, int)> m_unselectableTextRanges;

	public bool IsSelecting => m_selecting;

	public string Text
	{
		get
		{
			return m_textComponent.text;
		}
		set
		{
			m_textComponent.text = value;
		}
	}

	public Text TextComponent
	{
		get
		{
			return m_textComponent;
		}
		set
		{
			m_textComponent = value;
		}
	}

	public Color SelectionColor
	{
		get
		{
			return m_selectionColor;
		}
		set
		{
			m_selectionColor = value;
		}
	}

	public SelectionChangedEvent SelectionChanged => m_selectionChanged;

	public List<(int, int)> UnselectableTextRanges => m_unselectableTextRanges;

	Transform ICanvasElement.transform => base.transform;

	public void AddUnselectableTextRange(int start, int end)
	{
		int num = m_unselectableTextRanges.BinarySearch((start, end));
		if (num < 0)
		{
			m_unselectableTextRanges.Insert(~num, (start, end));
		}
	}

	protected override void Awake()
	{
		base.Awake();
		m_startPosition = -1;
		m_endPosition = -1;
		m_builder = new StringBuilder();
		m_unselectableTextRanges = new List<(int, int)>();
	}

	public void Rebuild(CanvasUpdate update)
	{
		if (update == CanvasUpdate.LatePreRender)
		{
			UpdateGeometrySelf();
		}
	}

	private void UpdateGeometrySelf()
	{
		if (m_renderer == null)
		{
			GameObject gameObject = new GameObject("Highlight", typeof(RectTransform), typeof(CanvasRenderer), typeof(LayoutElement));
			gameObject.hideFlags = HideFlags.DontSave;
			gameObject.transform.SetParent(m_textComponent.transform.parent);
			gameObject.transform.SetAsFirstSibling();
			gameObject.layer = gameObject.layer;
			m_renderer = gameObject.GetComponent<CanvasRenderer>();
			m_renderer.SetMaterial(m_textComponent.GetModifiedMaterial(Graphic.defaultGraphicMaterial), Texture2D.whiteTexture);
			m_mesh = new Mesh();
			gameObject.GetComponent<LayoutElement>().ignoreLayout = true;
			RectTransform rectTransform = m_textComponent.rectTransform;
			RectTransform component = gameObject.GetComponent<RectTransform>();
			component.localPosition = rectTransform.localPosition;
			component.localRotation = rectTransform.localRotation;
			component.localScale = rectTransform.localScale;
			component.anchorMin = rectTransform.anchorMin;
			component.anchorMax = rectTransform.anchorMax;
			component.anchoredPosition = rectTransform.anchoredPosition;
			component.sizeDelta = rectTransform.sizeDelta;
			component.pivot = rectTransform.pivot;
		}
		OnFillVBO(m_mesh);
		m_renderer.SetMesh(m_mesh);
	}

	private void OnFillVBO(Mesh vbo)
	{
		using VertexHelper vertexHelper = new VertexHelper();
		Vector2 roundingOffset = m_textComponent.PixelAdjustPoint(Vector2.zero);
		GenerateHighlight(vertexHelper, roundingOffset);
		vertexHelper.FillMesh(vbo);
	}

	private int DetermineCharacterLine(int charPos, TextGenerator generator)
	{
		for (int i = 0; i < generator.lineCount - 1; i++)
		{
			if (generator.lines[i + 1].startCharIdx > charPos)
			{
				return i;
			}
		}
		return generator.lineCount - 1;
	}

	private void GenerateHighlight(VertexHelper vbo, Vector2 roundingOffset)
	{
		int num = Mathf.Max(0, m_startPosition);
		int num2 = Mathf.Max(0, m_endPosition);
		if (num > num2)
		{
			int num3 = num;
			num = num2;
			num2 = num3;
		}
		num2--;
		float pixelsPerUnit = m_textComponent.pixelsPerUnit;
		TextGenerator cachedTextGenerator = m_textComponent.cachedTextGenerator;
		if (cachedTextGenerator.lineCount <= 0)
		{
			return;
		}
		int num4 = DetermineCharacterLine(num, cachedTextGenerator);
		int lineEndPosition = GetLineEndPosition(cachedTextGenerator, num4);
		UIVertex simpleVert = UIVertex.simpleVert;
		simpleVert.uv0 = Vector2.zero;
		simpleVert.color = m_selectionColor;
		for (int i = num; i <= num2 && i < cachedTextGenerator.characterCount; i++)
		{
			if (i == lineEndPosition || i == num2)
			{
				UICharInfo uICharInfo = cachedTextGenerator.characters[num];
				UICharInfo uICharInfo2 = cachedTextGenerator.characters[i];
				Vector2 vector = new Vector2(uICharInfo.cursorPos.x / pixelsPerUnit, cachedTextGenerator.lines[num4].topY / pixelsPerUnit);
				Vector2 vector2 = new Vector2((uICharInfo2.cursorPos.x + uICharInfo2.charWidth) / pixelsPerUnit, vector.y - (float)cachedTextGenerator.lines[num4].height / pixelsPerUnit);
				if (vector2.x > m_textComponent.rectTransform.rect.xMax || vector2.x < m_textComponent.rectTransform.rect.xMin)
				{
					vector2.x = m_textComponent.rectTransform.rect.xMax;
				}
				int currentVertCount = vbo.currentVertCount;
				simpleVert.position = new Vector3(vector.x, vector2.y, 0f) + (Vector3)roundingOffset;
				vbo.AddVert(simpleVert);
				simpleVert.position = new Vector3(vector2.x, vector2.y, 0f) + (Vector3)roundingOffset;
				vbo.AddVert(simpleVert);
				simpleVert.position = new Vector3(vector2.x, vector.y, 0f) + (Vector3)roundingOffset;
				vbo.AddVert(simpleVert);
				simpleVert.position = new Vector3(vector.x, vector.y, 0f) + (Vector3)roundingOffset;
				vbo.AddVert(simpleVert);
				vbo.AddTriangle(currentVertCount, currentVertCount + 1, currentVertCount + 2);
				vbo.AddTriangle(currentVertCount + 2, currentVertCount + 3, currentVertCount);
				num = i + 1;
				num4++;
				lineEndPosition = GetLineEndPosition(cachedTextGenerator, num4);
			}
		}
	}

	public override void OnPointerDown(PointerEventData eventData)
	{
		base.OnPointerDown(eventData);
		if (eventData.button == PointerEventData.InputButton.Left)
		{
			m_startPosition = -1;
			m_endPosition = -1;
			m_selectionChanged.Invoke(new SelectionChangedEventArgs(m_startPosition, m_endPosition, eventData.position));
			CanvasUpdateRegistry.RegisterCanvasElementForGraphicRebuild(this);
		}
	}

	public override void OnPointerUp(PointerEventData eventData)
	{
		base.OnPointerUp(eventData);
		if (eventData.button == PointerEventData.InputButton.Left)
		{
			m_selecting = false;
		}
	}

	public void OnInitializePotentialDrag(PointerEventData eventData)
	{
		if (eventData.button == PointerEventData.InputButton.Left && eventData.clickCount >= 2)
		{
			m_selecting = true;
			RectTransformUtility.ScreenPointToLocalPointInRectangle(m_textComponent.rectTransform, eventData.position, eventData.pressEventCamera, out var localPoint);
			m_endPosition = (m_startPosition = GetCharacterIndexFromPosition(localPoint));
			m_selectionChanged.Invoke(new SelectionChangedEventArgs(m_startPosition, m_endPosition, eventData.position));
			CanvasUpdateRegistry.RegisterCanvasElementForGraphicRebuild(this);
		}
		if (!IsSelecting)
		{
			ExecuteEvents.ExecuteHierarchy(base.transform.parent.gameObject, eventData, ExecuteEvents.initializePotentialDrag);
		}
	}

	public void OnBeginDrag(PointerEventData eventData)
	{
		if (!IsSelecting)
		{
			ExecuteEvents.ExecuteHierarchy(base.transform.parent.gameObject, eventData, ExecuteEvents.beginDragHandler);
		}
	}

	public void OnDrag(PointerEventData eventData)
	{
		if (!IsSelecting)
		{
			ExecuteEvents.ExecuteHierarchy(base.transform.parent.gameObject, eventData, ExecuteEvents.dragHandler);
		}
		if (eventData.button == PointerEventData.InputButton.Left && IsSelecting)
		{
			RectTransformUtility.ScreenPointToLocalPointInRectangle(m_textComponent.rectTransform, eventData.position, eventData.pressEventCamera, out var localPoint);
			int characterIndexFromPosition = GetCharacterIndexFromPosition(localPoint);
			m_endPosition = characterIndexFromPosition;
			m_selectionChanged.Invoke(new SelectionChangedEventArgs(m_startPosition, m_endPosition, eventData.position));
			CanvasUpdateRegistry.RegisterCanvasElementForGraphicRebuild(this);
		}
	}

	public void OnEndDrag(PointerEventData eventData)
	{
		if (!IsSelecting)
		{
			ExecuteEvents.ExecuteHierarchy(base.transform.parent.gameObject, eventData, ExecuteEvents.endDragHandler);
		}
		if (eventData.button == PointerEventData.InputButton.Left)
		{
			m_selecting = false;
		}
	}

	public void OnUpdateSelected(BaseEventData eventData)
	{
		Event obj = new Event();
		while (Event.PopEvent(obj))
		{
			if (obj.rawType == EventType.KeyDown)
			{
				OnKeyPressed(obj);
			}
		}
	}

	private static int GetLineEndPosition(TextGenerator gen, int line)
	{
		line = Mathf.Max(line, 0);
		if (line + 1 < gen.lines.Count)
		{
			return gen.lines[line + 1].startCharIdx - 1;
		}
		return gen.characterCountVisible;
	}

	private int GetUnclampedCharacterLineFromPosition(Vector2 pos, TextGenerator generator)
	{
		float num = pos.y * m_textComponent.pixelsPerUnit;
		float num2 = 0f;
		for (int i = 0; i < generator.lineCount; i++)
		{
			float topY = generator.lines[i].topY;
			float num3 = topY - (float)generator.lines[i].height;
			if (num > topY)
			{
				float num4 = topY - num2;
				if (num > topY - 0.5f * num4)
				{
					return i - 1;
				}
				return i;
			}
			if (num > num3)
			{
				return i;
			}
			num2 = num3;
		}
		return generator.lineCount;
	}

	protected int GetCharacterIndexFromPosition(Vector2 pos)
	{
		TextGenerator cachedTextGenerator = m_textComponent.cachedTextGenerator;
		if (cachedTextGenerator.lineCount == 0)
		{
			return 0;
		}
		int unclampedCharacterLineFromPosition = GetUnclampedCharacterLineFromPosition(pos, cachedTextGenerator);
		if (unclampedCharacterLineFromPosition < 0)
		{
			return 0;
		}
		if (unclampedCharacterLineFromPosition >= cachedTextGenerator.lineCount)
		{
			return cachedTextGenerator.characterCountVisible;
		}
		int startCharIdx = cachedTextGenerator.lines[unclampedCharacterLineFromPosition].startCharIdx;
		int lineEndPosition = GetLineEndPosition(cachedTextGenerator, unclampedCharacterLineFromPosition);
		for (int i = startCharIdx; i < lineEndPosition && i < cachedTextGenerator.characterCountVisible; i++)
		{
			UICharInfo uICharInfo = cachedTextGenerator.characters[i];
			Vector2 vector = uICharInfo.cursorPos / m_textComponent.pixelsPerUnit;
			float num = pos.x - vector.x;
			float num2 = vector.x + uICharInfo.charWidth / m_textComponent.pixelsPerUnit - pos.x;
			if (num < num2)
			{
				return i;
			}
		}
		return lineEndPosition;
	}

	private void OnKeyPressed(Event processingEvent)
	{
		bool flag = processingEvent.modifiers == EventModifiers.Control;
		if (processingEvent.keyCode == KeyCode.C && flag)
		{
			CopySelectedText();
		}
	}

	public void CopySelectedText()
	{
		if (m_startPosition == -1 || m_endPosition == -1)
		{
			return;
		}
		int num = m_startPosition;
		int num2 = m_endPosition;
		if (num > num2)
		{
			int num3 = num;
			num = num2;
			num2 = num3;
		}
		List<(int, int)> unselectableTextRanges = m_unselectableTextRanges;
		int i = 0;
		StringBuilder builder = m_builder;
		for (int j = num; j < num2; j++)
		{
			for (; i < unselectableTextRanges.Count && j > unselectableTextRanges[i].Item2; i++)
			{
			}
			if (i >= unselectableTextRanges.Count || j < unselectableTextRanges[i].Item1)
			{
				builder.Append(m_textComponent.text[j]);
			}
		}
		GUIUtility.systemCopyBuffer = builder.ToString();
		builder.Clear();
	}

	public void LayoutComplete()
	{
	}

	public void GraphicUpdateComplete()
	{
	}
}
