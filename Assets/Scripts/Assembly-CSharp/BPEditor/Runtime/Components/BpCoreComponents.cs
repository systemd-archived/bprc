using System.Collections.Generic;
using UnityEngine;

// 1:1 移植 badpiggies-editor 的 unity_runtime/components/{transform,box_collider,behaviour,rigidbody,camera}.rs。
// 约定：Rust `Option<T>` → C# `T?`（未设置 = null）；get_field 未设置时返回 null（对应 `self.x?` 早退 None）；
// set_field 命中返回 true，名称/类型不匹配返回 false（由宿主溢出到 extra）。
// 命名加 Bp 前缀避免与工程全局类（Sprite/Camera 等）冲突。

// ---- Transform ----
public class BpTransformComponent : BpUnityComponent
{
	public Vector3? localPosition;
	public BpQuaternion? localRotation;
	public Vector3? localScale;
	public Vector3? localEulerAngles;
	public List<KeyValuePair<string, BpValue>> extra = new List<KeyValuePair<string, BpValue>>();

	public override string ComponentSuffix { get { return "Transform"; } }

	public override BpValue GetField(BpScene scene, string name)
	{
		switch (name)
		{
			case "m_LocalPosition":
				return localPosition.HasValue ? BpValue.OfVector3(localPosition.Value) : null;
			case "m_LocalRotation":
				return localRotation.HasValue ? BpValue.OfQuaternion(localRotation.Value) : null;
			case "m_LocalScale":
				return localScale.HasValue ? BpValue.OfVector3(localScale.Value) : null;
			case "m_LocalEulerAnglesHint":
				return localEulerAngles.HasValue ? BpValue.OfVector3(localEulerAngles.Value) : null;
		}
		return null;
	}

	public override bool SetField(BpScene scene, string name, BpValue value)
	{
		switch (name)
		{
			case "m_LocalPosition":
				if (value.kind != BpValueKind.Vector3) return false;
				localPosition = value.vector3; return true;
			case "m_LocalRotation":
				if (value.kind != BpValueKind.Quaternion) return false;
				localRotation = value.quaternion; return true;
			case "m_LocalScale":
				if (value.kind != BpValueKind.Vector3) return false;
				localScale = value.vector3; return true;
			case "m_LocalEulerAnglesHint":
				if (value.kind != BpValueKind.Vector3) return false;
				localEulerAngles = value.vector3; return true;
		}
		return false;
	}

	public override List<KeyValuePair<string, BpValue>> Extra { get { return extra; } }

	public override BpUnityComponent Clone()
	{
		BpTransformComponent c = new BpTransformComponent();
		c.localPosition = localPosition;
		c.localRotation = localRotation;
		c.localScale = localScale;
		c.localEulerAngles = localEulerAngles;
		c.extra = BpComponentUtil.CloneExtra(extra);
		return c;
	}
}

// ---- BoxCollider / BoxCollider2D ----
public class BpBoxColliderComponent : BpUnityComponent
{
	public Vector3? center;
	public Vector3? size;
	public bool? enabled;
	public bool? isTrigger;
	public List<KeyValuePair<string, BpValue>> extra = new List<KeyValuePair<string, BpValue>>();

	public override string ComponentSuffix { get { return "BoxCollider"; } }

	public override BpValue GetField(BpScene scene, string name)
	{
		switch (name)
		{
			case "m_Center":
				return center.HasValue ? BpValue.OfVector3(center.Value) : null;
			case "m_Size":
				return size.HasValue ? BpValue.OfVector3(size.Value) : null;
			case "m_Enabled":
			case "enabled":
				return enabled.HasValue ? BpValue.Boolean(enabled.Value) : null;
			case "m_IsTrigger":
			case "isTrigger":
				return isTrigger.HasValue ? BpValue.Boolean(isTrigger.Value) : null;
		}
		return null;
	}

	public override bool SetField(BpScene scene, string name, BpValue value)
	{
		switch (name)
		{
			case "m_Center":
				if (value.kind != BpValueKind.Vector3) return false;
				center = value.vector3; return true;
			case "m_Size":
				if (value.kind != BpValueKind.Vector3) return false;
				size = value.vector3; return true;
			case "m_Enabled":
			case "enabled":
				if (value.kind != BpValueKind.Boolean) return false;
				enabled = value.boolValue; return true;
			case "m_IsTrigger":
			case "isTrigger":
				if (value.kind != BpValueKind.Boolean) return false;
				isTrigger = value.boolValue; return true;
		}
		return false;
	}

	public override List<KeyValuePair<string, BpValue>> Extra { get { return extra; } }

	public override BpUnityComponent Clone()
	{
		BpBoxColliderComponent c = new BpBoxColliderComponent();
		c.center = center; c.size = size; c.enabled = enabled; c.isTrigger = isTrigger;
		c.extra = BpComponentUtil.CloneExtra(extra);
		return c;
	}
}

// ---- Behaviour（占位 MonoBehaviour 根）----
public class BpBehaviourComponent : BpUnityComponent
{
	public bool enabled;
	public List<KeyValuePair<string, BpValue>> extra = new List<KeyValuePair<string, BpValue>>();

	public override string ComponentSuffix { get { return "Behaviour"; } }

	public override BpValue GetField(BpScene scene, string name)
	{
		if (name == "m_Enabled" || name == "enabled") return BpValue.Boolean(enabled);
		return null;
	}

	public override bool SetField(BpScene scene, string name, BpValue value)
	{
		if ((name == "m_Enabled" || name == "enabled") && value.kind == BpValueKind.Boolean)
		{
			enabled = value.boolValue;
			return true;
		}
		return false;
	}

	public override List<KeyValuePair<string, BpValue>> Extra { get { return extra; } }

	public override BpUnityComponent Clone()
	{
		BpBehaviourComponent c = new BpBehaviourComponent();
		c.enabled = enabled;
		c.extra = BpComponentUtil.CloneExtra(extra);
		return c;
	}
}

// ---- Rigidbody ----
// 注意：ObjectDeserializer.SetProperty 会把 m_IsKinematic 重命名为 isKinematic，
// 且源以 Integer(0/1) 写出 → 这里见到的是 Boolean（walker 已转换）。
public class BpRigidbodyComponent : BpUnityComponent
{
	public float mass = 1.0f;
	public float drag = 0.0f;
	public float angularDrag = 0.05f;
	public bool useGravity = true;
	public bool isKinematic = false;
	public List<KeyValuePair<string, BpValue>> extra = new List<KeyValuePair<string, BpValue>>();

	public override string ComponentSuffix { get { return "Rigidbody"; } }

	public override BpValue GetField(BpScene scene, string name)
	{
		switch (name)
		{
			case "m_Mass": case "mass": return BpValue.Float(mass);
			case "m_Drag": case "drag": return BpValue.Float(drag);
			case "m_AngularDrag": case "angularDrag": return BpValue.Float(angularDrag);
			case "m_UseGravity": case "useGravity": return BpValue.Boolean(useGravity);
			case "m_IsKinematic": case "isKinematic": return BpValue.Boolean(isKinematic);
		}
		return null;
	}

	public override bool SetField(BpScene scene, string name, BpValue value)
	{
		switch (name)
		{
			case "m_Mass": case "mass":
				if (value.kind != BpValueKind.Float) return false;
				mass = value.floatValue; return true;
			case "m_Drag": case "drag":
				if (value.kind != BpValueKind.Float) return false;
				drag = value.floatValue; return true;
			case "m_AngularDrag": case "angularDrag":
				if (value.kind != BpValueKind.Float) return false;
				angularDrag = value.floatValue; return true;
			case "m_UseGravity": case "useGravity":
				if (value.kind != BpValueKind.Boolean) return false;
				useGravity = value.boolValue; return true;
			case "m_IsKinematic": case "isKinematic":
				if (value.kind != BpValueKind.Boolean) return false;
				isKinematic = value.boolValue; return true;
		}
		return false;
	}

	public override List<KeyValuePair<string, BpValue>> Extra { get { return extra; } }

	public override BpUnityComponent Clone()
	{
		BpRigidbodyComponent c = new BpRigidbodyComponent();
		c.mass = mass; c.drag = drag; c.angularDrag = angularDrag;
		c.useGravity = useGravity; c.isKinematic = isKinematic;
		c.extra = BpComponentUtil.CloneExtra(extra);
		return c;
	}
}

// ---- Camera（可序列化字段子集）----
public class BpCameraComponent : BpUnityComponent
{
	public Color backgroundColor = default(Color);
	public bool orthographic = false;
	public float orthographicSize = 5.0f;
	public float nearClipPlane = 0.3f;
	public float farClipPlane = 1000.0f;
	public float fieldOfView = 60.0f;
	public float depth = 0.0f;
	public int cullingMask = -1;
	public int clearFlags = 1;
	public List<KeyValuePair<string, BpValue>> extra = new List<KeyValuePair<string, BpValue>>();

	public override string ComponentSuffix { get { return "Camera"; } }

	public override BpValue GetField(BpScene scene, string name)
	{
		switch (name)
		{
			case "m_BackGroundColor": return BpValue.OfColor(backgroundColor);
			case "orthographic": case "m_Orthographic": return BpValue.Boolean(orthographic);
			case "orthographic size": case "orthographic_size": case "orthographicSize": case "m_OrthographicSize":
				return BpValue.Float(orthographicSize);
			case "near clip plane": case "nearClipPlane": return BpValue.Float(nearClipPlane);
			case "far clip plane": case "farClipPlane": return BpValue.Float(farClipPlane);
			case "field of view": case "fieldOfView": return BpValue.Float(fieldOfView);
			case "m_Depth": case "depth": return BpValue.Float(depth);
			case "m_CullingMask": return BpValue.Integer(cullingMask);
			case "m_ClearFlags": return BpValue.Integer(clearFlags);
		}
		return null;
	}

	public override bool SetField(BpScene scene, string name, BpValue value)
	{
		switch (name)
		{
			case "m_BackGroundColor":
				if (value.kind != BpValueKind.Color) return false;
				backgroundColor = value.color; return true;
			case "orthographic": case "m_Orthographic":
				if (value.kind != BpValueKind.Boolean) return false;
				orthographic = value.boolValue; return true;
			case "orthographic size": case "orthographic_size": case "orthographicSize": case "m_OrthographicSize":
				if (value.kind != BpValueKind.Float) return false;
				orthographicSize = value.floatValue; return true;
			case "near clip plane": case "nearClipPlane":
				if (value.kind != BpValueKind.Float) return false;
				nearClipPlane = value.floatValue; return true;
			case "far clip plane": case "farClipPlane":
				if (value.kind != BpValueKind.Float) return false;
				farClipPlane = value.floatValue; return true;
			case "field of view": case "fieldOfView":
				if (value.kind != BpValueKind.Float) return false;
				fieldOfView = value.floatValue; return true;
			case "m_Depth": case "depth":
				if (value.kind != BpValueKind.Float) return false;
				depth = value.floatValue; return true;
			case "m_CullingMask":
				if (value.kind != BpValueKind.Integer) return false;
				cullingMask = value.intValue; return true;
			case "m_ClearFlags":
				if (value.kind != BpValueKind.Integer) return false;
				clearFlags = value.intValue; return true;
		}
		return false;
	}

	public override List<KeyValuePair<string, BpValue>> Extra { get { return extra; } }

	public override BpUnityComponent Clone()
	{
		BpCameraComponent c = new BpCameraComponent();
		c.backgroundColor = backgroundColor; c.orthographic = orthographic;
		c.orthographicSize = orthographicSize; c.nearClipPlane = nearClipPlane;
		c.farClipPlane = farClipPlane; c.fieldOfView = fieldOfView;
		c.depth = depth; c.cullingMask = cullingMask; c.clearFlags = clearFlags;
		c.extra = BpComponentUtil.CloneExtra(extra);
		return c;
	}
}

// 组件共用工具
public static class BpComponentUtil
{
	// 深拷贝 extra 列表（对应 Rust `#[derive(Clone)]` 对 Vec<(String, Value)> 的克隆，
	// Value 内部为 Rc-free 结构 → 深拷贝）
	public static List<KeyValuePair<string, BpValue>> CloneExtra(List<KeyValuePair<string, BpValue>> src)
	{
		List<KeyValuePair<string, BpValue>> dst = new List<KeyValuePair<string, BpValue>>(src.Count);
		for (int i = 0; i < src.Count; i++)
			dst.Add(new KeyValuePair<string, BpValue>(
				src[i].Key, src[i].Value != null ? src[i].Value.Clone() : null));
		return dst;
	}

	// 等价 Rust `name.parse::<usize>()`（仅十进制数字，无符号/前导 +-）
	public static bool TryParseIndex(string s, out int index)
	{
		index = 0;
		if (string.IsNullOrEmpty(s)) return false;
		int v = 0;
		for (int i = 0; i < s.Length; i++)
		{
			char c = s[i];
			if (c < '0' || c > '9') return false;
			v = v * 10 + (c - '0');
		}
		index = v;
		return true;
	}

	// 解码 BezierCurve 的单个节点：容忍 `Generic data { … }` 包裹层（元素读取器通常已解开，
	// 故未包裹时直接读 entries）。对应 Rust `decode_node`（恒返回 Some）。
	public static bool TryDecodeBezierNode(List<KeyValuePair<string, BpValue>> entries, out BpBezierNode node)
	{
		List<KeyValuePair<string, BpValue>> data = entries;
		foreach (KeyValuePair<string, BpValue> kv in entries)
		{
			if (kv.Key == "data" && kv.Value != null && kv.Value.kind == BpValueKind.Generic)
			{
				data = kv.Value.generic;
				break;
			}
		}

		node = default(BpBezierNode);
		foreach (KeyValuePair<string, BpValue> kv in data)
		{
			if (kv.Value == null || kv.Value.kind != BpValueKind.Vector3) continue;
			switch (kv.Key)
			{
				case "position": node.position = kv.Value.vector3; break;
				case "tangent0": node.tangent0 = kv.Value.vector3; break;
				case "tangent1": node.tangent1 = kv.Value.vector3; break;
			}
		}
		return true;
	}

	// 解码 Scene.set_array 产出的 int 数组形状：
	// [("size", Integer(N)), ("0", value), ("1", value), …]；缺失下标的元素补 0
	// （对齐 Unity IList<T> 的稀疏语义）。元素可能是整数本身，也可能是
	// `Generic([("data", Integer(v))])`（源流把每个元素包在 data Generic 里）。
	public static List<int> DecodeIntArray(List<KeyValuePair<string, BpValue>> entries)
	{
		int size = 0;
		List<KeyValuePair<int, int>> indexed = new List<KeyValuePair<int, int>>();
		foreach (KeyValuePair<string, BpValue> kv in entries)
		{
			if (kv.Key == "size")
			{
				if (kv.Value != null && kv.Value.kind == BpValueKind.Integer)
					size = kv.Value.intValue < 0 ? 0 : kv.Value.intValue;
				continue;
			}
			int idx;
			if (!TryParseIndex(kv.Key, out idx)) continue;
			bool has = false;
			int scalar = 0;
			if (kv.Value != null)
			{
				if (kv.Value.kind == BpValueKind.Integer) { scalar = kv.Value.intValue; has = true; }
				else if (kv.Value.kind == BpValueKind.Float) { scalar = (int)kv.Value.floatValue; has = true; }
				else if (kv.Value.kind == BpValueKind.Generic)
				{
					foreach (KeyValuePair<string, BpValue> inner in kv.Value.generic)
					{
						if (inner.Key != "data" || inner.Value == null) continue;
						if (inner.Value.kind == BpValueKind.Integer) { scalar = inner.Value.intValue; has = true; break; }
						if (inner.Value.kind == BpValueKind.Float) { scalar = (int)inner.Value.floatValue; has = true; break; }
					}
				}
			}
			if (has) indexed.Add(new KeyValuePair<int, int>(idx, scalar));
		}

		List<int> outList = new List<int>(size);
		for (int i = 0; i < size; i++) outList.Add(0);
		foreach (KeyValuePair<int, int> kv in indexed)
			if (kv.Key >= 0 && kv.Key < outList.Count) outList[kv.Key] = kv.Value;
		return outList;
	}
}
