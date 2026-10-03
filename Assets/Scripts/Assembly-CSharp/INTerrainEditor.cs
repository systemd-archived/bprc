using System.Collections.Generic;
using UnityEngine;

public class INTerrainEditor
{
	private static List<e2dTerrain> s_cachedTerrains;
	private static float s_lastScanTime;
	private static readonly float RescanInterval = 1f;
	private static GameObject s_funcTerrainRoot;

	public static List<e2dTerrain> FindAllTerrains()
	{
		if (s_cachedTerrains != null && Time.unscaledTime - s_lastScanTime < RescanInterval)
		{
			s_cachedTerrains.RemoveAll((e2dTerrain t) => t == null);
			if (s_cachedTerrains.Count > 0) return s_cachedTerrains;
		}
		e2dTerrain[] found = Object.FindObjectsOfType<e2dTerrain>();
		s_cachedTerrains = new List<e2dTerrain>(found);
		s_lastScanTime = Time.unscaledTime;
		return s_cachedTerrains;
	}

	public static void InvalidateCache() { s_cachedTerrains = null; }

	// ===== e2d 地形操作 =====

	public static void UnloadTerrain(e2dTerrain terrain)
	{
		if (terrain == null) return;
		terrain.Reset();
	}

	public static void UnloadAllTerrains()
	{
		foreach (e2dTerrain t in FindAllTerrains()) UnloadTerrain(t);
	}

	public static void EnsureCollider(e2dTerrain terrain)
	{
		if (terrain == null || terrain.NoCollider) return;
		terrain.FixCurve();
		terrain.FixBoundary();
		terrain.RebuildAllMeshes();
		SetGroundLayer(terrain.transform, e2dConstants.COLLIDER_MESH_NAME);
	}

	public static void EnsureAllColliders()
	{
		foreach (e2dTerrain t in FindAllTerrains()) EnsureCollider(t);
	}

	public static void SetColliderEnabled(bool enabled)
	{
		foreach (e2dTerrain t in FindAllTerrains())
		{
			t.NoCollider = !enabled;
			Transform col = t.transform.Find(e2dConstants.COLLIDER_MESH_NAME);
			if (col != null)
			{
				MeshCollider mc = col.GetComponent<MeshCollider>();
				if (mc != null)
				{
					if (enabled && (mc.sharedMesh == null || mc.sharedMesh.vertexCount == 0))
						t.ColliderMesh.RebuildMesh();
					else if (!enabled)
						t.ColliderMesh.ResetMesh();
					if (enabled) col.gameObject.layer = LayerMask.NameToLayer("Ground");
				}
			}
			else if (enabled)
			{
				t.ColliderMesh.RebuildMesh();
				SetGroundLayer(t.transform, e2dConstants.COLLIDER_MESH_NAME);
			}
		}
	}

	public static void SetColliderDepth(float depth)
	{
		foreach (e2dTerrain t in FindAllTerrains())
		{
			Transform col = t.transform.Find(e2dConstants.COLLIDER_MESH_NAME);
			if (col == null) continue;
			MeshCollider mc = col.GetComponent<MeshCollider>();
			if (mc == null || mc.sharedMesh == null) continue;
			Vector3[] v = mc.sharedMesh.vertices;
			if (v.Length < 2) continue;
			for (int i = 0; i < v.Length; i += 2)
			{
				v[i].z = -0.5f * depth;
				v[i + 1].z = 0.5f * depth;
			}
			mc.sharedMesh.vertices = v;
		}
	}

	public static void ApplyHeightOffset(float offset)
	{
		foreach (e2dTerrain t in FindAllTerrains())
		{
			for (int i = 0; i < t.TerrainCurve.Count; i++)
			{
				Vector2 p = t.TerrainCurve[i].position;
				p.y += offset;
				t.TerrainCurve[i].position = p;
			}
			EnsureCollider(t);
		}
	}

	public static void SetHeightOffset(float targetOffset)
	{
		foreach (e2dTerrain t in FindAllTerrains())
		{
			if (t.TerrainCurve.Count == 0) continue;
			float min = float.MaxValue, max = float.MinValue;
			for (int i = 0; i < t.TerrainCurve.Count; i++)
			{
				float y = t.TerrainCurve[i].position.y;
				if (y < min) min = y;
				if (y > max) max = y;
			}
			float delta = targetOffset - 0.5f * (min + max);
			for (int j = 0; j < t.TerrainCurve.Count; j++)
			{
				Vector2 p = t.TerrainCurve[j].position;
				p.y += delta;
				t.TerrainCurve[j].position = p;
			}
			EnsureCollider(t);
		}
	}

	public static void ApplyScale(float sx, float sy)
	{
		foreach (e2dTerrain t in FindAllTerrains())
		{
			if (t.TerrainCurve.Count == 0) continue;
			Vector2 origin = t.TerrainCurve[0].position;
			for (int i = 0; i < t.TerrainCurve.Count; i++)
			{
				Vector2 p = t.TerrainCurve[i].position;
				p.x = origin.x + (p.x - origin.x) * sx;
				p.y = origin.y + (p.y - origin.y) * sy;
				t.TerrainCurve[i].position = p;
			}
			EnsureCollider(t);
		}
	}

	// ===== 函数绘图地形（独立碰撞，不依赖 e2d） =====

	// 销毁旧的函数绘图地形
	public static void DestroyFunctionTerrain()
	{
		if (s_funcTerrainRoot != null)
		{
			Object.Destroy(s_funcTerrainRoot);
			s_funcTerrainRoot = null;
		}
	}

	// 从多条曲线创建函数绘图地形（含碰撞体 + LineRenderer 线条可视化）
	public static void CreateFunctionTerrain(List<Vector2[]> curves, float depthZ, Color lineColor)
	{
		if (curves == null || curves.Count == 0) return;
		DestroyFunctionTerrain();
		s_funcTerrainRoot = new GameObject("_func_terrain");

		// 每条曲线一个 LineRenderer（不同颜色）
		int colorIdx = 0;
		Color[] palette = new Color[] { lineColor, Color.cyan, Color.yellow, Color.magenta, Color.green };
		foreach (Vector2[] curve in curves)
		{
			if (curve == null || curve.Length < 2) continue;
			CreateLineVisual(s_funcTerrainRoot, curve, palette[colorIdx % palette.Length]);
			colorIdx++;
		}

		// 每条曲线独立碰撞（线条本身，不填充底部）
		foreach (Vector2[] curve in curves)
		{
			if (curve == null || curve.Length < 2) continue;
			CreateLineCollision(s_funcTerrainRoot, curve, depthZ);
		}
	}

	// 为单条曲线创建沿曲线表面的碰撞墙（不封闭，不加底部矩形）
	// AddFunctionTerrain: 追加新地形到已有的根对象上（不销毁旧的）
	public static void AddFunctionTerrain(List<Vector2[]> curves, float depthZ, Color lineColor)
	{
		if (curves == null || curves.Count == 0) return;
		if (s_funcTerrainRoot == null)
			s_funcTerrainRoot = new GameObject("_func_terrain");

		int colorIdx = 0;
		Color[] palette = new Color[] { lineColor, Color.cyan, Color.yellow, Color.magenta, Color.green };
		foreach (Vector2[] curve in curves)
		{
			if (curve == null || curve.Length < 2) continue;
			CreateLineVisual(s_funcTerrainRoot, curve, palette[colorIdx % palette.Length]);
			colorIdx++;
		}
		foreach (Vector2[] curve in curves)
		{
			if (curve == null || curve.Length < 2) continue;
			CreateLineCollision(s_funcTerrainRoot, curve, depthZ);
		}
	}

	// 为单条曲线创建沿曲线表面的碰撞墙（不封闭，不加底部矩形）
	private static void CreateLineCollision(GameObject parent, Vector2[] curve, float depthZ)
	{
		int n = curve.Length;
		if (n < 2) return;

		int segCount = n - 1;
		Vector3[] verts = new Vector3[2 * n];
		for (int i = 0; i < n; i++)
		{
			verts[2 * i] = new Vector3(curve[i].x, curve[i].y, -0.5f * depthZ);
			verts[2 * i + 1] = new Vector3(curve[i].x, curve[i].y, 0.5f * depthZ);
		}

		// 双面碰撞：每个四边形正反两面各2个三角形
		int[] tris = new int[12 * segCount];
		for (int i = 0; i < segCount; i++)
		{
			int cur = 2 * i;
			int next = cur + 2;
			int t = 12 * i;
			// 正面
			tris[t]     = cur;
			tris[t + 1] = cur + 1;
			tris[t + 2] = next;
			tris[t + 3] = next;
			tris[t + 4] = cur + 1;
			tris[t + 5] = next + 1;
			// 反面
			tris[t + 6]  = next;
			tris[t + 7]  = cur + 1;
			tris[t + 8]  = cur;
			tris[t + 9]  = next + 1;
			tris[t + 10] = cur + 1;
			tris[t + 11] = next;
		}

		Mesh mesh = new Mesh();
		mesh.name = "curve_collider";
		mesh.vertices = verts;
		mesh.triangles = tris;
		mesh.RecalculateNormals();
		mesh.RecalculateBounds();

		GameObject go = new GameObject("_collider");
		go.transform.SetParent(parent.transform, false);
		go.layer = LayerMask.NameToLayer("Ground");
		MeshCollider mc = go.AddComponent<MeshCollider>();
		mc.sharedMesh = mesh;
	}

	// LineRenderer 线条可视化
	private static void CreateLineVisual(GameObject parent, Vector2[] points, Color color)
	{
		GameObject go = new GameObject("_line");
		go.transform.SetParent(parent.transform, false);
		LineRenderer lr = go.AddComponent<LineRenderer>();
		lr.positionCount = points.Length;
		lr.useWorldSpace = false;
		lr.startWidth = 0.08f;
		lr.endWidth = 0.08f;
		lr.numCornerVertices = 2;
		lr.numCapVertices = 2;
		Material lrMat = new Material(Shader.Find("Sprites/Default"));
		lrMat.color = color;
		lr.material = lrMat;
		lr.startColor = color;
		lr.endColor = color;
		for (int i = 0; i < points.Length; i++)
			lr.SetPosition(i, new Vector3(points[i].x, points[i].y, -0.1f));
	}

	// 显示/隐藏函数绘图地形
	public static void SetFunctionTerrainVisible(bool visible)
	{
		if (s_funcTerrainRoot != null)
			s_funcTerrainRoot.SetActive(visible);
	}

	private static void SetGroundLayer(Transform parent, string childName)
	{
		Transform child = parent.Find(childName);
		if (child != null)
			child.gameObject.layer = LayerMask.NameToLayer("Ground");
	}
}
