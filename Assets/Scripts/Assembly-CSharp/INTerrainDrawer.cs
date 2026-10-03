using System.Collections.Generic;
using UnityEngine;

public class INTerrainDrawer : MonoBehaviour
{
	private static INTerrainDrawer s_instance;
	private bool m_isDrawing;
	private List<Vector2> m_drawPoints = new List<Vector2>();
	private LineRenderer m_preview;
	private Camera m_cam;
	private Color m_drawColor = Color.white;
	private float m_lineWidth = 1f;

	public static void BeginDraw(Color color, float lineWidth)
	{
		if (s_instance == null)
		{
			GameObject go = new GameObject("_terrain_drawer");
			s_instance = go.AddComponent<INTerrainDrawer>();
			DontDestroyOnLoad(go);
		}
		s_instance.m_drawColor = color;
		s_instance.m_lineWidth = lineWidth;
		s_instance.m_isDrawing = false;
		s_instance.m_drawPoints.Clear();
		s_instance.EnsurePreview();
		s_instance.enabled = true;
	}

	private void EnsurePreview()
	{
		if (m_preview == null)
		{
			GameObject go = new GameObject("_draw_preview");
			go.transform.SetParent(transform, false);
			m_preview = go.AddComponent<LineRenderer>();
			m_preview.useWorldSpace = true;
			m_preview.startWidth = 0.08f;
			m_preview.endWidth = 0.08f;
			Material mat = new Material(Shader.Find("Sprites/Default"));
			mat.color = m_drawColor;
			m_preview.material = mat;
			m_preview.startColor = m_drawColor;
			m_preview.endColor = m_drawColor;
		}
		m_preview.positionCount = 0;
	}

	private void Update()
	{
		if (m_cam == null) m_cam = Camera.main;
		if (m_cam == null) return;

		bool mouseDown = Input.GetMouseButtonDown(0);
		bool mouseHeld = Input.GetMouseButton(0);
		bool mouseUp = Input.GetMouseButtonUp(0);

		if (UnityEngine.EventSystems.EventSystem.current != null &&
		    UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
			return;

		Vector3 wp = m_cam.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, -m_cam.transform.position.z));
		Vector2 pt = new Vector2(wp.x, wp.y);

		if (mouseDown)
		{
			m_isDrawing = true;
			m_drawPoints.Clear();
			m_drawPoints.Add(pt);
		}
		else if (mouseHeld && m_isDrawing)
		{
			if (m_drawPoints.Count == 0 || Vector2.Distance(m_drawPoints[m_drawPoints.Count - 1], pt) > 0.005f)
			{
				m_drawPoints.Add(pt);
				UpdatePreview();
			}
		}
		else if (mouseUp && m_isDrawing)
		{
			m_isDrawing = false;
			if (m_drawPoints.Count >= 2)
			{
				// 保持原始绘制顺序（不按X排序，保留手绘精度）
				// 仅去除完全重合的点
				List<Vector2> cleaned = new List<Vector2> { m_drawPoints[0] };
				for (int i = 1; i < m_drawPoints.Count; i++)
				{
					if (Vector2.Distance(m_drawPoints[i], cleaned[cleaned.Count - 1]) > 0.001f)
						cleaned.Add(m_drawPoints[i]);
				}
				if (cleaned.Count >= 2)
				{
					// 手绘不卸载旧地形，每次追加新地形
					List<Vector2[]> curves = new List<Vector2[]> { cleaned.ToArray() };
					INTerrainEditor.AddFunctionTerrain(curves, 10f, m_drawColor);
					Debug.Log("[TerrainDrawer] Hand-drawn terrain: " + cleaned.Count + " points");
				}
			}
			m_preview.positionCount = 0;
			enabled = false;
		}
	}

	private void UpdatePreview()
	{
		if (m_preview == null || m_drawPoints.Count < 2) return;
		m_preview.positionCount = m_drawPoints.Count;
		for (int i = 0; i < m_drawPoints.Count; i++)
			m_preview.SetPosition(i, new Vector3(m_drawPoints[i].x, m_drawPoints[i].y, -0.1f));
	}
}
