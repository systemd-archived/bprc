using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class INCustomTerrainFileLoader
{
	[Serializable]
	private class TerrainData
	{
		public int version = 1;
		public List<CurveNodeData> curveNodes = new List<CurveNodeData>();
		public bool curveClosed;
		public float fillTextureTileWidth = 1f;
		public float fillTextureTileHeight = 1f;
		public float fillTextureTileOffsetX;
		public float fillTextureTileOffsetY;
	}

	[Serializable]
	private class CurveNodeData
	{
		public float x;
		public float y;
		public int texture;
	}

	public static bool ExportTerrain(e2dTerrain terrain, string path)
	{
		if (terrain == null || string.IsNullOrEmpty(path)) return false;
		try
		{
			TerrainData data = new TerrainData();
			data.curveClosed = terrain.CurveClosed;
			data.fillTextureTileWidth = terrain.FillTextureTileWidth;
			data.fillTextureTileHeight = terrain.FillTextureTileHeight;
			data.fillTextureTileOffsetX = terrain.FillTextureTileOffsetX;
			data.fillTextureTileOffsetY = terrain.FillTextureTileOffsetY;
			foreach (e2dCurveNode node in terrain.TerrainCurve)
			{
				data.curveNodes.Add(new CurveNodeData { x = node.position.x, y = node.position.y, texture = node.texture });
			}
			string dir = Path.GetDirectoryName(path);
			if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
				Directory.CreateDirectory(dir);
			File.WriteAllText(path, JsonUtility.ToJson(data, true));
			return true;
		}
		catch (Exception ex)
		{
			Debug.LogError("[INTerrainFileLoader] Export failed: " + ex.Message);
			return false;
		}
	}

	// 导入地形：先卸载旧地形，再加载新数据，确保碰撞
	public static bool ImportTerrain(e2dTerrain terrain, string path)
	{
		if (terrain == null || string.IsNullOrEmpty(path)) return false;
		if (!File.Exists(path))
		{
			Debug.LogError("[INTerrainFileLoader] File not found: " + path);
			return false;
		}
		try
		{
			TerrainData data = JsonUtility.FromJson<TerrainData>(File.ReadAllText(path));
			if (data == null || data.curveNodes == null || data.curveNodes.Count < 2)
			{
				Debug.LogError("[INTerrainFileLoader] Invalid data: need >= 2 curve nodes");
				return false;
			}

			// 1. 卸载旧地形
			INTerrainEditor.UnloadTerrain(terrain);

			// 2. 加载新曲线
			terrain.TerrainCurve.Clear();
			foreach (CurveNodeData n in data.curveNodes)
			{
				e2dCurveNode node = new e2dCurveNode(new Vector2(n.x, n.y));
				node.texture = n.texture;
				terrain.TerrainCurve.Add(node);
			}
			terrain.CurveClosed = data.curveClosed;
			terrain.FillTextureTileWidth = data.fillTextureTileWidth;
			terrain.FillTextureTileHeight = data.fillTextureTileHeight;
			terrain.FillTextureTileOffsetX = data.fillTextureTileOffsetX;
			terrain.FillTextureTileOffsetY = data.fillTextureTileOffsetY;

			// 3. 重建网格 + 碰撞体（含 Ground 层设置）
			terrain.FixCurve();
			terrain.FixBoundary();
			terrain.RebuildAllMaterials();
			INTerrainEditor.EnsureCollider(terrain);
			return true;
		}
		catch (Exception ex)
		{
			Debug.LogError("[INTerrainFileLoader] Import failed: " + ex.Message);
			return false;
		}
	}
}
