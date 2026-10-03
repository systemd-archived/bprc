using System.Collections.Generic;
using UnityEngine;

// 1:1 移植 badpiggies-editor 的 domain/types.rs（关卡/对象数据模型）。
// 说明（平台适配，非逻辑偏差）：Unity 已有 Vector2/Vector3/Color，直接复用；
// Rust 侧 Color 的 packed 互转以静态方法保留。

// domain::types::DataType
public enum BpDataType
{
	None = 0,
	Terrain = 1,
	PrefabOverrides = 2
}

public static class BpDataTypeExt
{
	public static BpDataType FromByte(byte b)
	{
		switch (b)
		{
			case 1: return BpDataType.Terrain;
			case 2: return BpDataType.PrefabOverrides;
			default: return BpDataType.None;
		}
	}
}

// domain::types::Color packed helpers（R G B A，大端）
public static class BpColorPacked
{
	public static Color FromPacked(uint val)
	{
		return new Color(
			((val >> 24) & 0xff) / 255f,
			((val >> 16) & 0xff) / 255f,
			((val >> 8) & 0xff) / 255f,
			(val & 0xff) / 255f);
	}

	public static uint ToPacked(Color c)
	{
		uint r = (uint)Mathf.RoundToInt(c.r * 255f) & 0xff;
		uint g = (uint)Mathf.RoundToInt(c.g * 255f) & 0xff;
		uint b = (uint)Mathf.RoundToInt(c.b * 255f) & 0xff;
		uint a = (uint)Mathf.RoundToInt(c.a * 255f) & 0xff;
		return (r << 24) | (g << 16) | (b << 8) | a;
	}
}

// domain::types::CurveTexture（地形条纹贴图）
public class BpCurveTexture
{
	public int textureIndex;
	public Vector2 size;
	public bool fixedAngle;
	public float fadeThreshold;
}

// domain::types::PrefabOverrideData
public class BpPrefabOverrideData
{
	public string rawText = "";
	public byte[] rawBytes = new byte[0];
}

// domain::types::PrefabInstance（叶节点，childCount == 0）
public class BpPrefabInstance
{
	public string name = "";
	public Vector3 position = Vector3.zero;
	public short prefabIndex;
	public Vector3 rotation = Vector3.zero;
	public Vector3 scale = Vector3.one;
	public BpDataType dataType = BpDataType.None;
	public TTerrData terrainData;
	public BpPrefabOverrideData overrideData;
	public bool hasParent;
	public int parent;
}

// domain::types::ParentObject（容器，childCount > 0）
public class BpParentObject
{
	public string name = "";
	public Vector3 position = Vector3.zero;
	public List<int> children = new List<int>();
	public bool hasParent;
	public int parent;
}

// domain::types::LevelObject（enum Prefab/Parent）
public class BpLevelObject
{
	public bool isPrefab;
	public BpPrefabInstance prefab;
	public BpParentObject parentObj;

	public static BpLevelObject OfPrefab(BpPrefabInstance p)
	{
		return new BpLevelObject { isPrefab = true, prefab = p };
	}

	public static BpLevelObject OfParent(BpParentObject p)
	{
		return new BpLevelObject { isPrefab = false, parentObj = p };
	}

	public string Name()
	{
		if (isPrefab) return prefab != null ? prefab.name : "";
		return parentObj != null ? parentObj.name : "";
	}

	public Vector3 Position()
	{
		if (isPrefab) return prefab != null ? prefab.position : Vector3.zero;
		return parentObj != null ? parentObj.position : Vector3.zero;
	}

	public BpPrefabInstance AsPrefab() => isPrefab ? prefab : null;
	public BpParentObject AsParent() => isPrefab ? null : parentObj;
}

// domain::types::LevelData（扁平 arena + 顶层根索引）
public class BpLevelData
{
	public List<BpLevelObject> objects = new List<BpLevelObject>();
	public List<int> roots = new List<int>();

	public void Clear()
	{
		objects.Clear();
		roots.Clear();
	}
}

// domain::types::DropPosition
public enum BpDropKind { Before, After, IntoParent }

public struct BpDropPosition
{
	public BpDropKind kind;
	public int index;

	public static BpDropPosition Before(int target) => new BpDropPosition { kind = BpDropKind.Before, index = target };
	public static BpDropPosition After(int target) => new BpDropPosition { kind = BpDropKind.After, index = target };
	public static BpDropPosition IntoParent(int target) => new BpDropPosition { kind = BpDropKind.IntoParent, index = target };
}

// domain::types::PastePosition
public enum BpPasteKind { AppendTo, Exact }

public struct BpPastePosition
{
	public BpPasteKind kind;
	public bool hasParent;
	public int parent;
	public BpDropPosition exact;

	// AppendTo(None) = 作为新根
	public static BpPastePosition AppendToRoot() => new BpPastePosition { kind = BpPasteKind.AppendTo, hasParent = false };
	public static BpPastePosition AppendTo(int parent) => new BpPastePosition { kind = BpPasteKind.AppendTo, hasParent = true, parent = parent };
	public static BpPastePosition At(BpDropPosition pos) => new BpPastePosition { kind = BpPasteKind.Exact, exact = pos };
}
