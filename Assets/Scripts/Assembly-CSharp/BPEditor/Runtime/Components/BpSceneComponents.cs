using System.Collections.Generic;
using UnityEngine;

// 1:1 移植 badpiggies-editor 的 unity_runtime/components/{camera_preview,level_manager}.rs。

// CameraPreview 的 Catmull-Rom 控制点
public struct BpCameraControlPoint
{
	public Vector2 position;
	public float zoom;
}

// ---- CameraPreview ----
public class BpCameraPreviewComponent : BpUnityComponent
{
	// 序列化控制点（m_controlPoints）；运行时动态生成路线的关卡为空。
	public List<BpCameraControlPoint> controlPoints = new List<BpCameraControlPoint>();
	// 预览动画总时长（秒）（m_animationTime）
	public float animationTime;
	public List<KeyValuePair<string, BpValue>> extra = new List<KeyValuePair<string, BpValue>>();

	public override string ComponentSuffix { get { return "CameraPreview"; } }

	// Rust 未实现 get_field → 默认 None
	public override bool SetField(BpScene scene, string name, BpValue value)
	{
		if (name == "m_animationTime" && value.kind == BpValueKind.Float)
		{
			animationTime = value.floatValue; return true;
		}
		if (name == "m_controlPoints" && value.kind == BpValueKind.Generic)
		{
			controlPoints = DecodeControlPoints(value.generic); return true;
		}
		return false;
	}

	public override List<KeyValuePair<string, BpValue>> Extra { get { return extra; } }

	public override BpUnityComponent Clone()
	{
		BpCameraPreviewComponent c = new BpCameraPreviewComponent();
		c.controlPoints = new List<BpCameraControlPoint>(controlPoints);
		c.animationTime = animationTime;
		c.extra = BpComponentUtil.CloneExtra(extra);
		return c;
	}

	// 解码 m_controlPoints 的 array-of-generic 形状：
	//   Array m_controlPoints
	//     ArraySize size = N
	//     Element 0
	//       Generic data
	//         Vector2 position / Float zoom
	// 宿主包为：Generic([("size", Integer(N)), ("0", Generic([("position", Vector2), ("zoom", Float)])), …])
	private static List<BpCameraControlPoint> DecodeControlPoints(List<KeyValuePair<string, BpValue>> entries)
	{
		int size = 0;
		List<KeyValuePair<int, BpCameraControlPoint>> indexed = new List<KeyValuePair<int, BpCameraControlPoint>>();

		foreach (KeyValuePair<string, BpValue> kv in entries)
		{
			if (kv.Key == "size")
			{
				if (kv.Value != null && kv.Value.kind == BpValueKind.Integer)
					size = kv.Value.intValue < 0 ? 0 : kv.Value.intValue;
				continue;
			}
			int idx;
			if (!BpComponentUtil.TryParseIndex(kv.Key, out idx)) continue;
			if (kv.Value == null || kv.Value.kind != BpValueKind.Generic) continue;

			BpCameraControlPoint pt = default(BpCameraControlPoint);
			foreach (KeyValuePair<string, BpValue> fv in kv.Value.generic)
			{
				if (fv.Key == "position" && fv.Value != null && fv.Value.kind == BpValueKind.Vector2)
					pt.position = fv.Value.vector2;
				else if (fv.Key == "zoom" && fv.Value != null && fv.Value.kind == BpValueKind.Float)
					pt.zoom = fv.Value.floatValue;
			}
			indexed.Add(new KeyValuePair<int, BpCameraControlPoint>(idx, pt));
		}

		indexed.Sort((a, b) => a.Key.CompareTo(b.Key));
		List<BpCameraControlPoint> outList = new List<BpCameraControlPoint>(size);
		foreach (KeyValuePair<int, BpCameraControlPoint> kv in indexed) outList.Add(kv.Value);
		return outList;
	}
}

// ---- LevelManager ----
public class BpLevelManagerComponent : BpUnityComponent
{
	public bool? darkLevel;
	public bool? sandbox;
	// m_gridCellPrefab 的原始对象引用 index（通常指向 GridCellLight prefab）。
	// Some(_) = override 设置了它；编辑器没有 prefab 表可解析。
	public int? gridCellPrefab;
	// 每行 construction-grid 的位掩码（bit 置位 = 该格可用）
	public List<int> constructionGridRows;
	// 相机矩形：top-left + size
	public float[] cameraLimits;
	// 建造模式相机相对 LevelStart 的偏移（x/y 中心偏移，z 半尺寸符号）
	public Vector3? constructionOffset;
	// 初始预览相机中心（编辑器用 x/y；z 保留）
	public Vector3? previewOffset;
	// 初始预览相机世界单位半尺寸
	public float? previewZoomOut;
	public List<KeyValuePair<string, BpValue>> extra = new List<KeyValuePair<string, BpValue>>();

	public override string ComponentSuffix { get { return "LevelManager"; } }

	// Rust 未实现 get_field → 默认 None
	public override bool SetField(BpScene scene, string name, BpValue value)
	{
		if (name == "m_darkLevel" && value.kind == BpValueKind.Boolean) { darkLevel = value.boolValue; return true; }
		if (name == "m_sandbox" && value.kind == BpValueKind.Boolean) { sandbox = value.boolValue; return true; }
		if (name == "m_cameraLimits" && value.kind == BpValueKind.Generic)
		{
			Vector2 tl = Vector2.zero, sz = Vector2.zero;
			foreach (KeyValuePair<string, BpValue> kv in value.generic)
			{
				if (kv.Key == "topLeft" && kv.Value != null && kv.Value.kind == BpValueKind.Vector2)
					tl = kv.Value.vector2;
				else if (kv.Key == "size" && kv.Value != null && kv.Value.kind == BpValueKind.Vector2)
					sz = kv.Value.vector2;
			}
			cameraLimits = new float[] { tl.x, tl.y, sz.x, sz.y };
			return true;
		}
		if (name == "m_constructionOffset" && value.kind == BpValueKind.Vector3) { constructionOffset = value.vector3; return true; }
		if (name == "m_previewOffset" && value.kind == BpValueKind.Vector3) { previewOffset = value.vector3; return true; }
		if (name == "m_previewZoomOut" && value.kind == BpValueKind.Float) { previewZoomOut = value.floatValue; return true; }
		if (name == "m_constructionGridRows" && value.kind == BpValueKind.Generic)
		{
			constructionGridRows = BpComponentUtil.DecodeIntArray(value.generic); return true;
		}
		return false;
	}

	public override bool SetObjectReferenceIndex(BpScene scene, string name, int index)
	{
		if (name == "m_gridCellPrefab") { gridCellPrefab = index; return true; }
		return false;
	}

	public override List<KeyValuePair<string, BpValue>> Extra { get { return extra; } }

	public override BpUnityComponent Clone()
	{
		BpLevelManagerComponent c = new BpLevelManagerComponent();
		c.darkLevel = darkLevel; c.sandbox = sandbox; c.gridCellPrefab = gridCellPrefab;
		if (constructionGridRows != null) c.constructionGridRows = new List<int>(constructionGridRows);
		if (cameraLimits != null) c.cameraLimits = (float[])cameraLimits.Clone();
		c.constructionOffset = constructionOffset; c.previewOffset = previewOffset;
		c.previewZoomOut = previewZoomOut;
		c.extra = BpComponentUtil.CloneExtra(extra);
		return c;
	}
}
