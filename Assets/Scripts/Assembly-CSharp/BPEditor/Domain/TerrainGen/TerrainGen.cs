using System.Collections.Generic;
using UnityEngine;

// ============================================================
// 照搬 badpiggies-editor 的 domain/terrain_gen 逻辑（C# 直译）：
//   math.rs / curve.rs / png.rs / stripe_mesh.rs / fill_mesh.rs / mod.rs
// ============================================================

public struct TTerrMesh
{
	public List<Vector2> vertices;
	public List<int> indices;

	public static TTerrMesh Empty()
	{
		return new TTerrMesh { vertices = new List<Vector2>(), indices = new List<int>() };
	}
}

public class TCurveTexture
{
	// 对应 Rust CurveTexture.texture_index（关卡数据中的槽位索引）
	public int textureIndex;
	// 平台适配：由 texture_index 经 level refs 解析出的 Unity 贴图（阶段3 接入）
	public Texture texture;
	public Vector2 size;
	public bool fixedAngle;
	public float fadeThreshold;

	public TCurveTexture(Texture tex)
	{
		texture = tex;
		size = new Vector2(1f, 1f);
	}
}

public class TCurveNode
{
	public Vector2 position;
	public int texture;

	public TCurveNode(Vector2 p, int t)
	{
		position = p;
		texture = t;
	}

	public TCurveNode Clone() => new TCurveNode(position, texture);
}

// 对应 domain::types::TerrainData
public class TTerrData
{
	public float fillTextureTileOffsetX;
	public float fillTextureTileOffsetY;
	public TTerrMesh fillMesh = TTerrMesh.Empty();
	public Color fillColor = Color.white;
	public int fillTextureIndex;
	public Texture fillTexture;
	public TTerrMesh curveMesh = TTerrMesh.Empty();
	public List<TCurveTexture> curveTextures = new List<TCurveTexture>();
	public int controlTextureCount;
	public byte[] controlTextureData;
	public bool hasCollider;
	// 缓存填充边界 [minX,minY,maxX,maxY]，只扩不缩（照搬 mod.rs）
	public bool hasFillBoundary;
	public float[] fillBoundary = new float[4];
}

public static class TerrainGen
{
	// ---------------- math.rs ----------------

	private static Vector2 Sub(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
	private static float Dot(Vector2 a, Vector2 b) => a.x * b.x + a.y * b.y;
	private static float DistSq(Vector2 a, Vector2 b) { Vector2 d = Sub(a, b); return Dot(d, d); }
	private static float Cross2D(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

	private static Vector2 Normalize(Vector2 v)
	{
		float len = Mathf.Sqrt(v.x * v.x + v.y * v.y);
		if (len < 1e-10f) return new Vector2(0f, 0f);
		return new Vector2(v.x / len, v.y / len);
	}

	private static double SignedArea(List<Vector2> polygon)
	{
		int n = polygon.Count;
		double area = 0.0;
		for (int i = 0; i < n; i++)
		{
			int j = (i + 1) % n;
			area += (double)polygon[i].x * polygon[j].y;
			area -= (double)polygon[j].x * polygon[i].y;
		}
		return area * 0.5;
	}

	private static float Cross3(Vector2 o, Vector2 v1, Vector2 v2)
	{
		return (v1.x - o.x) * (v2.y - o.y) - (v1.y - o.y) * (v2.x - o.x);
	}

	private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
	{
		float d1 = Cross3(p, a, b);
		float d2 = Cross3(p, b, c);
		float d3 = Cross3(p, c, a);
		bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
		bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
		return !(hasNeg && hasPos);
	}

	private static bool PointInTriangleStrict(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
	{
		float d1 = Cross2D(Sub(b, a), Sub(p, a));
		float d2 = Cross2D(Sub(c, b), Sub(p, b));
		float d3 = Cross2D(Sub(a, c), Sub(p, c));
		return (d1 > 0f && d2 > 0f && d3 > 0f) || (d1 < 0f && d2 < 0f && d3 < 0f);
	}

	private static Vector2 ProjectOnSegment(Vector2 p, Vector2 a, Vector2 b)
	{
		Vector2 ab = Sub(b, a);
		Vector2 ap = Sub(p, a);
		float t = Dot(ap, ab) / Mathf.Max(Dot(ab, ab), 1e-10f);
		t = Mathf.Clamp01(t);
		return new Vector2(a.x + t * ab.x, a.y + t * ab.y);
	}

	private static float PointToSegmentDistSq(Vector2 p, Vector2 a, Vector2 b)
	{
		Vector2 proj = ProjectOnSegment(p, a, b);
		return DistSq(p, proj);
	}

	// 照搬 e2dUtils.SegmentsIntersect（Cramer 法则 + 近轴段 0.01 容差）
	private static bool SegmentsIntersectPoint(Vector2 a, Vector2 b, Vector2 c, Vector2 d, out Vector2 pt)
	{
		pt = Vector2.zero;
		float denom = (a.x - b.x) * (c.y - d.y) - (a.y - b.y) * (c.x - d.x);
		if (Mathf.Abs(denom) <= float.Epsilon) return false;
		float abCross = a.x * b.y - a.y * b.x;
		float cdCross = c.x * d.y - c.y * d.x;
		float ix = ((c.x - d.x) * abCross - (a.x - b.x) * cdCross) / denom;
		float iy = ((c.y - d.y) * abCross - (a.y - b.y) * cdCross) / denom;

		float epsAB = (Mathf.Abs(a.x - b.x) <= float.Epsilon || Mathf.Abs(a.y - b.y) <= float.Epsilon) ? 0.01f : 0f;
		if (ix < Mathf.Min(a.x, b.x) - epsAB || ix > Mathf.Max(a.x, b.x) + epsAB) return false;
		if (iy < Mathf.Min(a.y, b.y) - epsAB || iy > Mathf.Max(a.y, b.y) + epsAB) return false;

		float epsCD = (Mathf.Abs(c.x - d.x) <= float.Epsilon || Mathf.Abs(c.y - d.y) <= float.Epsilon) ? 0.01f : 0f;
		if (ix < Mathf.Min(c.x, d.x) - epsCD || ix > Mathf.Max(c.x, d.x) + epsCD) return false;
		if (iy < Mathf.Min(c.y, d.y) - epsCD || iy > Mathf.Max(c.y, d.y) + epsCD) return false;

		pt = new Vector2(ix, iy);
		return true;
	}

	private static bool EdgesCross(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
	{
		float d1 = Cross2D(Sub(b2, b1), Sub(a1, b1));
		float d2 = Cross2D(Sub(b2, b1), Sub(a2, b1));
		float d3 = Cross2D(Sub(a2, a1), Sub(b1, a1));
		float d4 = Cross2D(Sub(a2, a1), Sub(b2, a1));
		return d1 * d2 < 0f && d3 * d4 < 0f;
	}

	private static int CountSelfIntersections(List<Vector2> polygon)
	{
		int n = polygon.Count;
		int count = 0;
		for (int i = 0; i < n; i++)
		{
			Vector2 a1 = polygon[i];
			Vector2 a2 = polygon[(i + 1) % n];
			for (int j = i + 2; j < n; j++)
			{
				if (i == 0 && j == n - 1) continue;
				Vector2 b1 = polygon[j];
				Vector2 b2 = polygon[(j + 1) % n];
				if (EdgesCross(a1, a2, b1, b2)) count++;
			}
		}
		return count;
	}

	// ---------------- curve.rs ----------------

	public static bool IsClosedLoop(List<TCurveNode> nodes)
	{
		return nodes.Count >= 2 && DistSq(nodes[0].position, nodes[nodes.Count - 1].position) < 0.5f * 0.5f;
	}

	// 外圈顶点(偶数索引)为节点位置；贴图索引来自控制贴图 PNG
	public static List<TCurveNode> ExtractCurveNodes(TTerrData td)
	{
		List<TCurveNode> nodes = new List<TCurveNode>();
		List<Vector2> verts = td.curveMesh.vertices;
		int nodeCount = verts.Count / 2;
		Color32[] px = DecodeControlPngPixels(td.controlTextureData);
		for (int i = 0; i < nodeCount; i++)
		{
			Vector2 pos = verts[i * 2];
			int texture = px != null ? DecodeControlTextureIndex(px, i) : 0;
			nodes.Add(new TCurveNode(pos, texture));
		}
		return nodes;
	}

	// ---------------- png.rs ----------------

	public static Color32[] DecodeControlPngPixels(byte[] data)
	{
		if (data == null || data.Length == 0) return null;
		Texture2D tex = new Texture2D(1, 1, TextureFormat.ARGB32, false);
		if (!tex.LoadImage(data)) { Object.Destroy(tex); return null; }
		Color32[] px = tex.GetPixels32();
		Object.Destroy(tex);
		return px;
	}

	// Unity 存 0..3 在 R/G/B/A 通道
	public static int DecodeControlTextureIndex(Color32[] pixels, int nodeIndex)
	{
		int b = nodeIndex * 4;
		if (pixels == null || b + 3 >= pixels.Length) return 0;
		// GetPixels32 是 row-major；控制贴图为 1×N，需按像素索引取
		int idx = nodeIndex;
		if (idx >= pixels.Length) return 0;
		Color32 c = pixels[idx];
		if (c.r == 255) return 0;
		if (c.g == 255) return 1;
		if (c.b == 255) return 2;
		if (c.a == 255) return 3;
		return 0;
	}

	// 1×N RGBA PNG，宽度为节点数向上取 2 的幂
	public static byte[] EncodeControlPng(List<TCurveNode> nodes)
	{
		int n = Mathf.Max(nodes.Count, 1);
		int width = Mathf.NextPowerOfTwo(n);
		Color32[] px = new Color32[width];
		Color32 zero = new Color32(0, 0, 0, 0);
		for (int i = 0; i < px.Length; i++) px[i] = zero;
		for (int i = 0; i < nodes.Count; i++)
		{
			switch (((nodes[i].texture % 4) + 4) % 4)
			{
				case 0: px[i] = new Color32(255, 0, 0, 0); break;
				case 1: px[i] = new Color32(0, 255, 0, 0); break;
				case 2: px[i] = new Color32(0, 0, 255, 0); break;
				case 3: px[i] = new Color32(0, 0, 0, 255); break;
			}
		}
		Texture2D tex = new Texture2D(width, 1, TextureFormat.ARGB32, false);
		tex.SetPixels32(px);
		tex.Apply();
		byte[] png = tex.EncodeToPNG();
		Object.Destroy(tex);
		return png;
	}

	// ---------------- stripe_mesh.rs ----------------

	public static TTerrMesh RebuildCurveMesh(List<TCurveNode> nodes, List<float> stripWidths)
	{
		if (nodes.Count < 2) return TTerrMesh.Empty();
		int n = nodes.Count;
		bool closed = IsClosedLoop(nodes);

		List<Vector2> stripe = new List<Vector2>(n);
		for (int i = 0; i < n; i++)
		{
			float w = (nodes[i].texture >= 0 && nodes[i].texture < stripWidths.Count) ? stripWidths[nodes[i].texture] : 0.5f;
			Vector2 inner;
			if (closed && (i == 0 || i == n - 1)) inner = nodes[i].position;
			else if (i == 0) inner = FirstStripe(nodes, w);
			else if (i == n - 1) inner = LastStripe(nodes, w);
			else inner = InteriorStripe(nodes, i, w);
			stripe.Add(inner);
		}

		CollapseInnerIntersections(stripe);

		List<Vector2> vertices = new List<Vector2>(n * 2);
		for (int i = 0; i < n; i++)
		{
			vertices.Add(nodes[i].position);
			vertices.Add(stripe[i]);
		}

		int segmentCount = (closed && n >= 3) ? n - 2 : n - 1;
		List<int> indices = TriangulateStripN(vertices, segmentCount);
		return new TTerrMesh { vertices = vertices, indices = indices };
	}

	private static Vector2 Perp(Vector2 tangent) => Normalize(new Vector2(tangent.y, -tangent.x));

	private static Vector2 FirstStripe(List<TCurveNode> nodes, float w)
	{
		Vector2 perp = Perp(Sub(nodes[1].position, nodes[0].position));
		return new Vector2(nodes[0].position.x + perp.x * w, nodes[0].position.y + perp.y * w);
	}

	private static Vector2 LastStripe(List<TCurveNode> nodes, float w)
	{
		int n = nodes.Count;
		Vector2 perp = Perp(Sub(nodes[n - 1].position, nodes[n - 2].position));
		return new Vector2(nodes[n - 1].position.x + perp.x * w, nodes[n - 1].position.y + perp.y * w);
	}

	private static Vector2 InteriorStripe(List<TCurveNode> nodes, int i, float w)
	{
		Vector2 incoming = Sub(nodes[i].position, nodes[i - 1].position);
		Vector2 outgoing = Sub(nodes[i + 1].position, nodes[i].position);
		Vector2 pi = Perp(incoming);
		Vector2 po = Perp(outgoing);
		Vector2 bisector = Normalize(new Vector2(pi.x + po.x, pi.y + po.y));
		return new Vector2(nodes[i].position.x + bisector.x * w, nodes[i].position.y + bisector.y * w);
	}

	private static void CollapseInnerIntersections(List<Vector2> stripe)
	{
		int n = stripe.Count;
		if (n < 3) return;
		for (int j = 0; j < n - 1; j++)
		{
			for (int k = j + 2; k < n - 1; k++)
			{
				if (SegmentsIntersectPoint(stripe[j], stripe[j + 1], stripe[k], stripe[k + 1], out Vector2 pt))
				{
					for (int m = j + 1; m <= k; m++) stripe[m] = pt;
					break;
				}
			}
		}
	}

	private static List<int> TriangulateStripN(List<Vector2> verts, int segmentCount)
	{
		List<int> indices = new List<int>();
		int pairCount = verts.Count / 2;
		if (pairCount < 2 || segmentCount == 0) return indices;
		int segments = Mathf.Min(segmentCount, pairCount - 1);
		for (int i = 1; i <= segments; i++)
		{
			int idx = i * 2;
			int oPrev = idx - 2;
			int iPrev = idx - 1;
			int oCurr = idx;
			int iCurr = idx + 1;
			if (PointInTriangle(verts[iCurr], verts[oPrev], verts[oCurr], verts[iPrev]))
			{
				indices.Add(oPrev); indices.Add(iCurr); indices.Add(iPrev);
				indices.Add(oPrev); indices.Add(oCurr); indices.Add(iCurr);
			}
			else
			{
				indices.Add(oPrev); indices.Add(oCurr); indices.Add(iPrev);
				indices.Add(iPrev); indices.Add(oCurr); indices.Add(iCurr);
			}
		}
		return indices;
	}

	// ---------------- fill_mesh.rs ----------------

	public static TTerrMesh RebuildFillMesh(List<TCurveNode> nodes, float[] boundary)
	{
		if (nodes.Count < 2) return TTerrMesh.Empty();
		bool isClosed = IsClosedLoop(nodes);

		List<Vector2> polygon;
		if (isClosed)
		{
			polygon = new List<Vector2>();
			foreach (TCurveNode nd in nodes) polygon.Add(nd.position);
			if (polygon.Count >= 2 && DistSq(polygon[polygon.Count - 1], polygon[0]) < 0.5f * 0.5f)
				polygon.RemoveAt(polygon.Count - 1);
		}
		else
		{
			polygon = BuildFillPolygon(nodes, boundary);
		}
		if (polygon.Count < 3) return TTerrMesh.Empty();

		List<int> indices = EarClipTriangulate(polygon);
		return new TTerrMesh { vertices = polygon, indices = indices };
	}

	private static List<Vector2> BuildFillPolygon(List<TCurveNode> nodes, float[] boundary)
	{
		float minX = boundary[0], minY = boundary[1], maxX = boundary[2], maxY = boundary[3];
		Vector2[] corners =
		{
			new Vector2(maxX, minY),
			new Vector2(minX, minY),
			new Vector2(minX, maxY),
			new Vector2(maxX, maxY)
		};

		Vector2 start = nodes[0].position;
		Vector2 end = nodes[nodes.Count - 1].position;
		int startEdge = ProjectToBoundaryEdge(start, corners);
		Vector2 startProj = ProjectToBoundary(start, corners);
		int endEdge = ProjectToBoundaryEdge(end, corners);
		Vector2 endProj = ProjectToBoundary(end, corners);

		List<Vector2> polygon = new List<Vector2>();
		if (DistSq(endProj, end) > 1e-6f) polygon.Add(endProj);

		bool skipCorners;
		if (endEdge == startEdge)
		{
			Vector2 a = corners[endEdge];
			Vector2 b = corners[(endEdge + 1) % 4];
			Vector2 ab = Sub(b, a);
			float len2 = Mathf.Max(Dot(ab, ab), 1e-10f);
			float tEnd = Dot(Sub(endProj, a), ab) / len2;
			float tStart = Dot(Sub(startProj, a), ab) / len2;
			skipCorners = tEnd <= tStart;
		}
		else skipCorners = false;

		if (!skipCorners)
		{
			int edge = endEdge;
			while (true)
			{
				polygon.Add(corners[(edge + 1) % 4]);
				edge = (edge + 1) % 4;
				if (edge == startEdge) break;
			}
		}

		if (DistSq(startProj, start) > 1e-6f) polygon.Add(startProj);
		foreach (TCurveNode nd in nodes) polygon.Add(nd.position);
		if (polygon.Count >= 2 && DistSq(polygon[polygon.Count - 1], polygon[0]) < 1e-6f)
			polygon.RemoveAt(polygon.Count - 1);
		return polygon;
	}

	private static int ProjectToBoundaryEdge(Vector2 p, Vector2[] corners)
	{
		int best = 0;
		float bestDist = float.MaxValue;
		for (int i = 0; i < 4; i++)
		{
			float d = PointToSegmentDistSq(p, corners[i], corners[(i + 1) % 4]);
			if (d < bestDist)
			{
				bestDist = d;
				best = i;
			}
		}
		return best;
	}

	private static Vector2 ProjectToBoundary(Vector2 p, Vector2[] corners)
	{
		int edge = ProjectToBoundaryEdge(p, corners);
		return ProjectOnSegment(p, corners[edge], corners[(edge + 1) % 4]);
	}

	private static List<int> EarClipTriangulate(List<Vector2> polygon)
	{
		int n = polygon.Count;
		List<int> indices = new List<int>();
		if (n < 3) return indices;
		if (n == 3)
		{
			indices.Add(0); indices.Add(1); indices.Add(2);
			return indices;
		}

		int si = CountSelfIntersections(polygon);
		if (si > 0)
			Debug.LogWarning("[INTerrainTool] ear_clip: polygon has " + n + " verts and " + si + " self-intersection(s)");

		double area = SignedArea(polygon);
		List<Vector2> ccw = new List<Vector2>(polygon);
		if (area < 0.0) ccw.Reverse();

		List<int> active = new List<int>();
		for (int i = 0; i < n; i++) active.Add(i);

		int failCount = 0;
		while (active.Count > 3)
		{
			int m = active.Count;
			bool clipped = false;
			for (int ai = 0; ai < m; ai++)
			{
				int prev = active[(ai + m - 1) % m];
				int curr = active[ai];
				int next = active[(ai + 1) % m];

				float crossVal = Cross2D(Sub(ccw[curr], ccw[prev]), Sub(ccw[next], ccw[curr]));
				if (crossVal < 1e-10f) continue;

				bool ear = true;
				for (int k = 0; k < m; k++)
				{
					int vi = active[k];
					if (vi == prev || vi == curr || vi == next) continue;
					if (PointInTriangleStrict(ccw[vi], ccw[prev], ccw[curr], ccw[next])) { ear = false; break; }
				}
				if (!ear) continue;

				indices.Add(prev); indices.Add(curr); indices.Add(next);
				active.RemoveAt(ai);
				clipped = true;
				failCount = 0;
				break;
			}
			if (!clipped)
			{
				failCount++;
				if (failCount > active.Count)
				{
					for (int i = 2; i < active.Count; i++)
					{
						indices.Add(active[0]); indices.Add(active[i - 1]); indices.Add(active[i]);
					}
					break;
				}
			}
		}
		if (active.Count == 3)
		{
			indices.Add(active[0]); indices.Add(active[1]); indices.Add(active[2]);
		}

		if (area < 0.0)
		{
			for (int i = 0; i < indices.Count; i++) indices[i] = n - 1 - indices[i];
		}
		return indices;
	}

	// ---------------- mod.rs ----------------

	private static float[] InferBoundaryFromFillMesh(List<Vector2> fillVerts)
	{
		float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
		foreach (Vector2 v in fillVerts)
		{
			minX = Mathf.Min(minX, v.x); minY = Mathf.Min(minY, v.y);
			maxX = Mathf.Max(maxX, v.x); maxY = Mathf.Max(maxY, v.y);
		}
		if (minX > maxX) return new[] { 0f, 0f, 1f, 1f };
		return new[] { minX, minY, maxX, maxY };
	}

	private static float[] ExpandBoundaryForNodes(float[] boundary, List<TCurveNode> nodes)
	{
		float minX = boundary[0], minY = boundary[1], maxX = boundary[2], maxY = boundary[3];
		foreach (TCurveNode n in nodes)
		{
			minX = Mathf.Min(minX, n.position.x); minY = Mathf.Min(minY, n.position.y);
			maxX = Mathf.Max(maxX, n.position.x); maxY = Mathf.Max(maxY, n.position.y);
		}
		return new[] { minX, minY, maxX, maxY };
	}

	// 从节点重算全部地形网格与控制贴图（就地更新 td）
	public static void RegenerateTerrain(TTerrData td, List<TCurveNode> nodes)
	{
		List<float> stripWidths = new List<float>();
		foreach (TCurveTexture ct in td.curveTextures) stripWidths.Add(ct.size.y);
		if (stripWidths.Count == 0) stripWidths.Add(1f);

		td.curveMesh = RebuildCurveMesh(nodes, stripWidths);

		float[] boundary = td.hasFillBoundary
			? ExpandBoundaryForNodes(td.fillBoundary, nodes)
			: ExpandBoundaryForNodes(InferBoundaryFromFillMesh(td.fillMesh.vertices), nodes);
		td.fillBoundary = boundary;
		td.hasFillBoundary = true;

		td.fillMesh = RebuildFillMesh(nodes, boundary);

		byte[] png = EncodeControlPng(nodes);
		if (png != null)
		{
			td.controlTextureData = png;
			td.controlTextureCount = 1;
		}
		else
		{
			td.controlTextureData = null;
			td.controlTextureCount = 0;
		}
	}
}
