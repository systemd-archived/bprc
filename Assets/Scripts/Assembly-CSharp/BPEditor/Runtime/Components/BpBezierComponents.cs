using System.Collections.Generic;
using UnityEngine;

// 1:1 移植 badpiggies-editor 的 unity_runtime/components/{bezier_curve,bezier_mesh}.rs。

// BezierCurve 的节点
public struct BpBezierNode
{
	public Vector3 position;
	public Vector3 tangent0;
	public Vector3 tangent1;

	public static BpBezierNode Zero { get { return new BpBezierNode(); } }
}

// ---- MentalTools.BezierCurve ----
public class BpBezierCurveComponent : BpUnityComponent
{
	public int? bezierPointCount;
	public bool? looped;
	public List<BpBezierNode> nodes;   // null = 未设置
	public List<KeyValuePair<string, BpValue>> extra = new List<KeyValuePair<string, BpValue>>();

	public const int DEFAULT_BEZIER_POINT_COUNT = 10;
	public const bool DEFAULT_LOOPED = false;

	public override string ComponentSuffix { get { return "BezierCurve"; } }

	public override BpValue GetField(BpScene scene, string name)
	{
		switch (name)
		{
			case "bezierPointCount":
				return bezierPointCount.HasValue ? BpValue.Integer(bezierPointCount.Value) : null;
			case "loop":
				return looped.HasValue ? BpValue.Boolean(looped.Value) : null;
		}
		return null;
	}

	public override bool SetField(BpScene scene, string name, BpValue value)
	{
		if (name == "bezierPointCount" && value.kind == BpValueKind.Integer)
		{
			bezierPointCount = value.intValue; return true;
		}
		if (name == "loop" && value.kind == BpValueKind.Boolean)
		{
			looped = value.boolValue; return true;
		}
		if (name == "bezierCurve" && value.kind == BpValueKind.Generic)
		{
			nodes = DecodeNodes(value.generic); return true;
		}
		return false;
	}

	public override List<KeyValuePair<string, BpValue>> Extra { get { return extra; } }

	public override BpUnityComponent Clone()
	{
		BpBezierCurveComponent c = new BpBezierCurveComponent();
		c.bezierPointCount = bezierPointCount; c.looped = looped;
		if (nodes != null) c.nodes = new List<BpBezierNode>(nodes);
		c.extra = BpComponentUtil.CloneExtra(extra);
		return c;
	}

	// 解码 `Generic bezierCurve { Array nodes { ... } }` 形状 —— 宿主把数组存为
	// Generic([("size", Integer), ("0", elem), ...])，每个元素是
	// Generic([("data", Generic([("position", Vec3), ...]))])。
	private static List<BpBezierNode> DecodeNodes(List<KeyValuePair<string, BpValue>> entries)
	{
		List<KeyValuePair<string, BpValue>> nodesEntries = new List<KeyValuePair<string, BpValue>>();
		foreach (KeyValuePair<string, BpValue> kv in entries)
		{
			if (kv.Key == "nodes" && kv.Value != null && kv.Value.kind == BpValueKind.Generic)
				nodesEntries = kv.Value.generic;
		}

		int size = 0;
		List<KeyValuePair<int, BpBezierNode>> indexed = new List<KeyValuePair<int, BpBezierNode>>();
		foreach (KeyValuePair<string, BpValue> kv in nodesEntries)
		{
			if (kv.Key == "size")
			{
				if (kv.Value != null && kv.Value.kind == BpValueKind.Integer)
					size = kv.Value.intValue < 0 ? 0 : kv.Value.intValue;
				continue;
			}
			int idx;
			if (!BpComponentUtil.TryParseIndex(kv.Key, out idx)) continue;
			if (kv.Value != null && kv.Value.kind == BpValueKind.Generic)
			{
				BpBezierNode n;
				if (BpComponentUtil.TryDecodeBezierNode(kv.Value.generic, out n))
					indexed.Add(new KeyValuePair<int, BpBezierNode>(idx, n));
			}
		}

		List<BpBezierNode> outNodes = new List<BpBezierNode>(size);
		for (int i = 0; i < size; i++) outNodes.Add(BpBezierNode.Zero);
		foreach (KeyValuePair<int, BpBezierNode> kv in indexed)
			if (kv.Key >= 0 && kv.Key < outNodes.Count) outNodes[kv.Key] = kv.Value;
		return outNodes;
	}
}

// ---- MentalTools.BezierMesh ----
public class BpBezierMeshComponent : BpUnityComponent
{
	public float? borderWidth;
	public List<KeyValuePair<string, BpValue>> extra = new List<KeyValuePair<string, BpValue>>();

	public const float DEFAULT_BORDER_WIDTH = 0.0f;

	public override string ComponentSuffix { get { return "BezierMesh"; } }

	public override BpValue GetField(BpScene scene, string name)
	{
		if (name == "borderWidth")
			return borderWidth.HasValue ? BpValue.Float(borderWidth.Value) : null;
		return null;
	}

	public override bool SetField(BpScene scene, string name, BpValue value)
	{
		if (name == "borderWidth" && value.kind == BpValueKind.Float)
		{
			borderWidth = value.floatValue; return true;
		}
		return false;
	}

	public override List<KeyValuePair<string, BpValue>> Extra { get { return extra; } }

	public override BpUnityComponent Clone()
	{
		BpBezierMeshComponent c = new BpBezierMeshComponent();
		c.borderWidth = borderWidth;
		c.extra = BpComponentUtil.CloneExtra(extra);
		return c;
	}
}
