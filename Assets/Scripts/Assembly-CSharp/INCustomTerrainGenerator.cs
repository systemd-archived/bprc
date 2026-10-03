using System.Collections.Generic;
using UnityEngine;

public class INCustomTerrainGenerator
{
	// 生成地形：先卸载旧地形，生成新曲线，确保碰撞
	public static void Generate(e2dTerrain terrain, int algorithm, float frequency, float roughness, int octaves, int seed, float width, float height)
	{
		if (terrain == null || width <= 0f || height <= 0f) return;

		// 1. 卸载旧地形
		INTerrainEditor.UnloadTerrain(terrain);

		// 2. 生成新曲线
		int nodeCount = Mathf.Max(2, 1 + Mathf.RoundToInt(width / 0.5f));
		float[] heightMap = new float[nodeCount];
		if (seed > 0) Random.InitState(seed);

		switch (algorithm)
		{
			case 0: GeneratePerlin(heightMap, frequency, octaves, roughness); break;
			case 1: GenerateVoronoi(heightMap, frequency); break;
			case 2: GenerateMidpoint(heightMap, frequency, roughness); break;
			case 3: GenerateWalk(heightMap, frequency, roughness); break;
			default: GeneratePerlin(heightMap, frequency, octaves, roughness); break;
		}
		NormalizeHeightmap(heightMap);

		// 3. 填充曲线节点
		terrain.TerrainCurve.Clear();
		for (int i = 0; i < nodeCount; i++)
		{
			float t = (float)i / (float)(nodeCount - 1);
			terrain.TerrainCurve.Add(new e2dCurveNode(new Vector2(t * width, heightMap[i] * height)));
		}
		terrain.CurveClosed = false;

		// 4. 重建网格 + 碰撞体（含 Ground 层设置）
		terrain.FixCurve();
		terrain.FixBoundary();
		terrain.RebuildAllMaterials();
		INTerrainEditor.EnsureCollider(terrain);
	}

	private static void GeneratePerlin(float[] heightMap, float frequency, int octaves, float persistence)
	{
		int freq = Mathf.Max(2, 1 + Mathf.RoundToInt(frequency * (float)heightMap.Length));
		e2dPerlinNoise noise = new e2dPerlinNoise(Mathf.Max(1, octaves), 1f, freq, Mathf.Clamp(persistence, 0.001f, 1f));
		noise.Regenerate();
		for (int i = 0; i < heightMap.Length; i++)
			heightMap[i] = noise.GetValue((float)i / (float)(heightMap.Length - 1));
	}

	private static void GenerateVoronoi(float[] heightMap, float frequency)
	{
		int peakCount = Mathf.Max(2, Mathf.RoundToInt(frequency * (float)heightMap.Length));
		List<Vector2> peaks = new List<Vector2>();
		for (int i = 0; i < peakCount; i++)
			peaks.Add(new Vector2((float)i / (float)(peakCount - 1), Random.value));
		e2dVoronoi voronoi = new e2dVoronoi(peaks, e2dVoronoiPeakType.SINE, 0.5f);
		for (int j = 0; j < heightMap.Length; j++)
			heightMap[j] = voronoi.GetValue((float)j / (float)(heightMap.Length - 1));
	}

	private static void GenerateMidpoint(float[] heightMap, float frequency, float roughness)
	{
		int step = Mathf.Max(2, Mathf.RoundToInt(frequency * (float)heightMap.Length));
		e2dMidpoint midpoint = new e2dMidpoint(heightMap.Length, Mathf.Max(1, heightMap.Length / step), Mathf.Clamp01(roughness), null);
		midpoint.Regenerate();
		for (int i = 0; i < heightMap.Length; i++)
			heightMap[i] = midpoint.GetValueAt(i);
	}

	private static void GenerateWalk(float[] heightMap, float frequency, float cohesion)
	{
		float dt = 1f / (float)(heightMap.Length - 1);
		float segLen = Mathf.Clamp01(1f / (frequency * (float)heightMap.Length));
		float val = 0.5f;
		float angle = 0f;
		float minVal = val, maxVal = val;
		heightMap[0] = val;
		float angleChange = Mathf.Lerp(20f, 80f, cohesion);
		for (int i = 1; i < heightMap.Length; i++)
		{
			float t = (float)i / (float)(heightMap.Length - 1);
			float edge = Mathf.Min(t % segLen, segLen - t % segLen);
			edge = 1f - edge * 2f / segLen;
			edge = Mathf.Pow(edge, 10f);
			float jitter = edge * angleChange * (2f * Random.value - 1f);
			angle = Mathf.Repeat(angle + jitter + 180f, 360f) - 180f;
			angle = Mathf.Clamp(angle, -80f, 80f);
			val += Mathf.Tan(angle * Mathf.Deg2Rad) * dt;
			float range = (2f - cohesion) * (float)i * dt * 0.5f;
			float clamped = Mathf.Clamp(val, maxVal - range, minVal + range);
			if (val != clamped) angle = 0f;
			val = clamped;
			minVal = Mathf.Min(minVal, val);
			maxVal = Mathf.Max(maxVal, val);
			heightMap[i] = val;
		}
	}

	private static void NormalizeHeightmap(float[] heightMap)
	{
		float min = float.MaxValue, max = float.MinValue;
		foreach (float v in heightMap)
		{
			if (v < min) min = v;
			if (v > max) max = v;
		}
		if (max - min > float.Epsilon)
		{
			for (int i = 0; i < heightMap.Length; i++)
				heightMap[i] = (heightMap[i] - min) / (max - min);
		}
	}
}
