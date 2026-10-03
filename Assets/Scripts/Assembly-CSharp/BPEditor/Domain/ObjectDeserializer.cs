using System;
using System.Collections.Generic;
using UnityEngine;

// 1:1 移植 badpiggies-editor 的 domain/object_deserializer.rs（分段落落盘）。
// 本文件当前包含第 1 段：RuntimeHost 反射抽象 + Value 联合 + 基础结构 + PropertyData + ObjectReader。
// 后续段落将追加 walker（ReadObject/ReadComponent/ReadValueType/...）。

// ---- 基础结构 ----

public struct BpBounds
{
	public Vector3 center;
	public Vector3 extents;
}

public struct BpQuaternion
{
	public float x, y, z, w;
}

public struct BpRect
{
	public float x, y, width, height;
}

public struct BpKeyframe
{
	public float time;
	public float value;
	public float inTangent;
	public float outTangent;
}

// ---- Value 联合（Rust enum Value<H>）----

public enum BpValueKind
{
	Null,
	Integer,
	Float,
	String,
	Boolean,
	Enum,
	Bounds,
	Color,
	Vector2,
	Vector3,
	Quaternion,
	Rect,
	Keyframe,
	ObjectReference,
	Generic
}

public class BpValue
{
	public BpValueKind kind;
	public int intValue;
	public float floatValue;
	public string stringValue;
	public bool boolValue;
	public BpBounds bounds;
	public Color color;
	public Vector2 vector2;
	public Vector3 vector3;
	public BpQuaternion quaternion;
	public BpRect rect;
	public BpKeyframe keyframe;
	public int objectReference; // GameObject handle
	public List<KeyValuePair<string, BpValue>> generic; // Generic(name, value)

	public static BpValue Null() => new BpValue { kind = BpValueKind.Null };
	public static BpValue Integer(int v) => new BpValue { kind = BpValueKind.Integer, intValue = v };
	public static BpValue Float(float v) => new BpValue { kind = BpValueKind.Float, floatValue = v };
	public static BpValue Str(string v) => new BpValue { kind = BpValueKind.String, stringValue = v };
	public static BpValue Boolean(bool v) => new BpValue { kind = BpValueKind.Boolean, boolValue = v };
	public static BpValue Enum(int v) => new BpValue { kind = BpValueKind.Enum, intValue = v };
	public static BpValue OfBounds(BpBounds v) => new BpValue { kind = BpValueKind.Bounds, bounds = v };
	public static BpValue OfColor(Color v) => new BpValue { kind = BpValueKind.Color, color = v };
	public static BpValue OfVector2(Vector2 v) => new BpValue { kind = BpValueKind.Vector2, vector2 = v };
	public static BpValue OfVector3(Vector3 v) => new BpValue { kind = BpValueKind.Vector3, vector3 = v };
	public static BpValue OfQuaternion(BpQuaternion v) => new BpValue { kind = BpValueKind.Quaternion, quaternion = v };
	public static BpValue OfRect(BpRect v) => new BpValue { kind = BpValueKind.Rect, rect = v };
	public static BpValue OfKeyframe(BpKeyframe v) => new BpValue { kind = BpValueKind.Keyframe, keyframe = v };
	public static BpValue ObjectRef(int handle) => new BpValue { kind = BpValueKind.ObjectReference, objectReference = handle };
	public static BpValue Generic() => new BpValue { kind = BpValueKind.Generic, generic = new List<KeyValuePair<string, BpValue>>() };

	public BpValue Clone()
	{
		BpValue c = new BpValue
		{
			kind = kind,
			intValue = intValue,
			floatValue = floatValue,
			stringValue = stringValue,
			boolValue = boolValue,
			bounds = bounds,
			color = color,
			vector2 = vector2,
			vector3 = vector3,
			quaternion = quaternion,
			rect = rect,
			keyframe = keyframe,
			objectReference = objectReference
		};
		if (generic != null)
		{
			c.generic = new List<KeyValuePair<string, BpValue>>(generic.Count);
			foreach (KeyValuePair<string, BpValue> kv in generic)
				c.generic.Add(new KeyValuePair<string, BpValue>(kv.Key, kv.Value != null ? kv.Value.Clone() : null));
		}
		return c;
	}
}

public class BpArrayElement
{
	public int index;
	public BpValue value;
}

// ---- RuntimeHost 反射抽象（Unity 反射由宿主实现）----

public interface IBpRuntimeHost
{
	// LevelLoader 引用表
	BpValue ReferencedObject(int index);

	// GameObject 层级
	int FindChild(int parent, string name, out bool found);
	int GetComponent(int obj, string name, out bool found);
	int AddComponent(int obj, string name, out bool found);
	string GameObjectName(int obj);

	// 反射
	void SetField(int target, string field, BpValue value);
	BpValue GetField(int target, string field, out bool found);
	bool AsStructTarget(ref BpValue value, out int target);
	void SendMessage(int obj, string message);

	// ReadComponent 兜底
	bool IsParticleSystem(int target);
	void SetParticleStartLifetime(int target, float value);
	void SetParticleStartSpeed(int target, float value);
	void SetParticleEmissionRate(int target, float value);
	void SetObjectReferenceIndex(int target, string field, int index);

	// AnimationCurve / Array
	void SetAnimationCurve(int target, string field, List<BpKeyframe> keys);
	void SetArray(int target, string field, int size, List<BpArrayElement> elements);
}

// ---- PropertyData（ObjectDeserializer.PropertyData 的镜像）----

public class BpPropertyData
{
	public string type = "";
	public string name = "";
	public string value = "";

	public static BpPropertyData Typed(string type, string name) => new BpPropertyData { type = type, name = name, value = "" };
	public static BpPropertyData Valued(string type, string name, string value) => new BpPropertyData { type = type, name = name, value = value };

	public int IntegerValue()
	{
		int v;
		return int.TryParse(value, out v) ? v : 0;
	}

	public float FloatValue()
	{
		float v;
		return float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v) ? v : 0f;
	}

	// value.Substring(1, value.Length-2)（按字符计数）
	public string StringValue()
	{
		int len = value == null ? 0 : value.Length;
		if (len >= 2) return value.Substring(1, len - 2);
		return "";
	}

	public bool BoolValue() => value == "True";
}

// ---- ObjectReader（ObjectDeserializer.ObjectReader 的镜像）----

public class BpObjectReader
{
	private readonly char[] m_chars;
	private int m_cursor;
	private int m_indentation;
	private bool m_indentationRead;

	public BpObjectReader(string text)
	{
		m_chars = (text ?? "").ToCharArray();
		m_cursor = 0;
		m_indentation = 0;
		m_indentationRead = false;
	}

	private char? Peek()
	{
		return m_cursor < m_chars.Length ? m_chars[m_cursor] : (char?)null;
	}

	private char? ReadChar()
	{
		char? c = Peek();
		if (c.HasValue) m_cursor++;
		return c;
	}

	// private string ReadLine()（改为 internal 供同程序集的 walker 调用；语义不变）
	internal string ReadLine()
	{
		m_indentationRead = false;
		if (m_cursor >= m_chars.Length) return null;
		System.Text.StringBuilder sb = new System.Text.StringBuilder();
		while (true)
		{
			char? c = Peek();
			if (!c.HasValue) break;
			if (c.Value == '\n') { m_cursor++; break; }
			if (c.Value == '\r')
			{
				m_cursor++;
				if (Peek() == '\n') m_cursor++;
				break;
			}
			sb.Append(c.Value);
			m_cursor++;
		}
		return sb.ToString();
	}

	// public PropertyData ReadProperty()
	public BpPropertyData ReadProperty()
	{
		string line = ReadLine();
		if (line == null) return null;
		string[] parts = line.Split(' ');
		if (parts.Length == 2)
			return BpPropertyData.Typed(parts[0], parts[1]);
		if (parts.Length == 4 && parts[2] == "=")
			return BpPropertyData.Valued(parts[0], parts[1], parts[3]);
		if (parts.Length > 4)
		{
			System.Text.StringBuilder combined = new System.Text.StringBuilder();
			for (int i = 1; i < parts.Length; i++)
			{
				if (parts[i] == "=") break;
				if (i > 1) combined.Append(' ');
				combined.Append(parts[i]);
			}
			return BpPropertyData.Valued(parts[0], combined.ToString(), parts[parts.Length - 1]);
		}
		return null;
	}

	// public PropertyData ReadTypeAndName()
	public BpPropertyData ReadTypeAndName()
	{
		string line = ReadLine();
		if (line == null) return null;
		int sp = line.IndexOf(' ');
		if (sp < 0) return null;
		return BpPropertyData.Typed(line.Substring(0, sp), line.Substring(sp + 1));
	}

	// public int GetIndentation()
	public int GetIndentation()
	{
		if (m_indentationRead) return m_indentation;
		int count = 0;
		while (Peek() == '\t')
		{
			count++;
			ReadChar();
		}
		m_indentation = count;
		m_indentationRead = true;
		return m_indentation;
	}

	// 供 walker 使用
	public bool AtEnd => m_cursor >= m_chars.Length;
	public int Cursor => m_cursor;
}

// ===========================================================================
// ObjectDeserializer — 1:1 移植 object_deserializer.rs 的静态方法（第 2+3 段）
// ===========================================================================

public static class BpObjectDeserializer
{
	private static int ParseIntOrZero(string s)
	{
		int v;
		return int.TryParse(s, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out v) ? v : 0;
	}

	private static BpArrayElement El(int index, BpValue value)
	{
		return new BpArrayElement { index = index, value = value };
	}

	private static void AddGeneric(BpValue value, string name, BpValue v)
	{
		if (value.generic == null) value.generic = new List<KeyValuePair<string, BpValue>>();
		value.generic.Add(new KeyValuePair<string, BpValue>(name, v));
	}

	// ---- 入口 ----

	// read_prefab_overrides(host, obj, text)
	public static void ReadPrefabOverrides(IBpRuntimeHost host, int obj, string text)
	{
		BpObjectReader reader = new BpObjectReader(text);
		ReadFile(host, obj, reader);
		host.SendMessage(obj, "OnDataLoaded");
	}

	// read_file
	public static void ReadFile(IBpRuntimeHost host, int obj, BpObjectReader reader)
	{
		BpPropertyData property = reader.ReadTypeAndName();
		if (property == null) return;
		if (property.type == "GameObject" && property.name == host.GameObjectName(obj))
			ReadObject(host, obj, 1, reader);
	}

	// read_object
	private static void ReadObject(IBpRuntimeHost host, int obj, int depth, BpObjectReader reader)
	{
		while (reader.GetIndentation() == depth)
		{
			BpPropertyData property = reader.ReadTypeAndName();
			if (property == null) return;
			if (property.type == "Component")
			{
				int dot = property.name.LastIndexOf('.');
				string shortName = dot >= 0 ? property.name.Substring(dot + 1) : property.name;
				bool found;
				int component = host.GetComponent(obj, shortName, out found);
				if (!found)
					component = host.AddComponent(obj, shortName, out found);
				if (found)
				{
					if (host.IsParticleSystem(component))
						ReadParticleSystem(host, component, depth + 1, reader);
					else
						ReadComponent(host, component, depth + 1, reader);
				}
				else
				{
					// C# 对被孤立的子树不作处理，镜像为静默排空其子节点。
					DrainChildren(reader, depth + 1);
				}
			}
			else if (property.type == "GameObject")
			{
				bool found;
				int child = host.FindChild(obj, property.name, out found);
				if (found)
					ReadObject(host, child, depth + 1, reader);
				else
					DrainChildren(reader, depth + 1);
			}
		}
	}

	private static void DrainChildren(BpObjectReader reader, int depth)
	{
		while (reader.GetIndentation() >= depth)
		{
			if (reader.ReadLine() == null) break;
		}
	}

	// read_component
	private static void ReadComponent(IBpRuntimeHost host, int component, int depth, BpObjectReader reader)
	{
		while (reader.GetIndentation() == depth)
		{
			BpPropertyData property = reader.ReadProperty();
			if (property == null) return;
			switch (property.type)
			{
				case "Integer":
					SetProperty(host, component, property.name, BpValue.Integer(property.IntegerValue()));
					break;
				case "Float":
					SetProperty(host, component, property.name, BpValue.Float(property.FloatValue()));
					break;
				case "String":
					SetProperty(host, component, property.name, BpValue.Str(property.StringValue()));
					// Unity 在 String 后无条件多读一行。
					reader.ReadProperty();
					break;
				case "Boolean":
					SetProperty(host, component, property.name, BpValue.Boolean(property.BoolValue()));
					break;
				case "Enum":
					SetProperty(host, component, property.name, BpValue.Enum(property.IntegerValue()));
					break;
				case "Bounds":
				{
					BpBounds bounds = ReadBounds(reader, depth + 1);
					SetProperty(host, component, property.name, BpValue.OfBounds(bounds));
					break;
				}
				case "ObjectReference":
				{
					BpValue resolved = host.ReferencedObject(property.IntegerValue());
					if (resolved != null)
						SetProperty(host, component, property.name, resolved);
					else
						host.SetObjectReferenceIndex(component, property.name, property.IntegerValue());
					// depth+1 处最多再各读一行（两次）。
					if (reader.GetIndentation() == depth + 1) reader.ReadProperty();
					if (reader.GetIndentation() == depth + 1) reader.ReadProperty();
					break;
				}
				case "Array":
					ReadArray(host, component, property.name, depth + 1, reader);
					break;
				case "AnimationCurve":
					ReadAnimationCurve(host, component, property.name, depth + 1, reader);
					break;
				case "Generic":
				case "Color":
				case "Vector2":
				case "Vector3":
				case "Rect":
				case "16":
				case "Quaternion":
				{
					// 读-改-写：取当前值（缺失则默认），递归 patch，再写回。
					bool found;
					BpValue current = host.GetField(component, property.name, out found);
					if (!found) current = DefaultForType(property.type);
					ReadGeneric(host, current, depth + 1, reader);
					SetProperty(host, component, property.name, current);
					break;
				}
				default:
					DrainChildren(reader, depth + 1);
					break;
			}
		}
	}

	// set_property（含静默忽略表与重命名表）
	private static void SetProperty(IBpRuntimeHost host, int target, string name, BpValue value)
	{
		switch (name)
		{
			case "m_ObjectHideFlags":
			case "m_EditorHideFlags":
			case "m_Name":
			case "m_PrefabParentObject":
			case "m_PrefabInternal":
			case "m_GameObject":
			case "m_EditorClassIdentifier":
			case "m_Script":
			case "m_Mesh":
			case "m_ConnectedBody":
			case "m_RootOrder":
			case "m_Mass":
				return;
		}

		string resolvedName;
		switch (name)
		{
			case "time": resolvedName = "m_Time"; break;
			case "inSlope": resolvedName = "m_InTangent"; break;
			case "outSlope": resolvedName = "m_OutTangent"; break;
			case "m_IsKinematic": resolvedName = "isKinematic"; break;
			case "orthographic size": resolvedName = "orthographicSize"; break;
			default: resolvedName = name; break;
		}

		host.SetField(target, resolvedName, value);
	}

	// read_bounds
	private static BpBounds ReadBounds(BpObjectReader reader, int depth)
	{
		BpBounds bounds = default(BpBounds);
		reader.GetIndentation();
		reader.ReadProperty();
		bounds.center = ReadVector3(reader, depth + 1);
		reader.GetIndentation();
		reader.ReadProperty();
		bounds.extents = ReadVector3(reader, depth + 1);
		return bounds;
	}

	// read_vector3
	private static Vector3 ReadVector3(BpObjectReader reader, int depth)
	{
		Vector3 v = default(Vector3);
		for (int i = 0; i < 3; i++)
		{
			if (reader.GetIndentation() == depth)
			{
				BpPropertyData prop = reader.ReadProperty();
				if (prop == null) continue;
				switch (prop.name)
				{
					case "x": v.x = prop.FloatValue(); break;
					case "y": v.y = prop.FloatValue(); break;
					case "z": v.z = prop.FloatValue(); break;
				}
			}
		}
		return v;
	}

	// default_for_type
	private static BpValue DefaultForType(string typeName)
	{
		switch (typeName)
		{
			case "Integer": return BpValue.Integer(0);
			case "Float": return BpValue.Float(0f);
			case "String": return BpValue.Str("");
			case "Boolean": return BpValue.Boolean(false);
			case "Enum": return BpValue.Enum(0);
			case "Bounds": return BpValue.OfBounds(default(BpBounds));
			case "Color": return BpValue.OfColor(default(Color));
			case "Vector2": return BpValue.OfVector2(default(Vector2));
			case "Vector3":
			case "16": return BpValue.OfVector3(default(Vector3));
			case "Quaternion": return BpValue.OfQuaternion(default(BpQuaternion));
			case "Rect": return BpValue.OfRect(default(BpRect));
			case "Keyframe": return BpValue.OfKeyframe(default(BpKeyframe));
			default: return BpValue.Generic();
		}
	}

	// read_generic
	private static void ReadGeneric(IBpRuntimeHost host, BpValue value, int depth, BpObjectReader reader)
	{
		while (reader.GetIndentation() == depth)
		{
			BpPropertyData property = reader.ReadProperty();
			if (property == null) return;
			switch (property.type)
			{
				case "Integer":
					AssignNamedInteger(value, property.name, property.IntegerValue());
					break;
				case "Float":
					AssignNamedScalar(value, property.name, property.FloatValue());
					break;
				case "Enum":
					AssignNamedEnum(value, property.name, property.IntegerValue());
					break;
				case "Boolean":
					AssignNamedBool(value, property.name, property.BoolValue());
					break;
				case "String":
					AssignNamedString(value, property.name, property.StringValue());
					reader.ReadProperty();
					break;
				case "Vector2":
				case "Vector3":
				case "Quaternion":
				case "Color":
				case "Rect":
				case "Bounds":
				case "Generic":
				case "Keyframe":
				case "16":
				{
					BpValue nested = DefaultForType(property.type);
					ReadGeneric(host, nested, depth + 1, reader);
					AssignNamedStruct(value, property.name, nested);
					break;
				}
				case "Array":
				{
					// 嵌套在 Generic 结构里的 Array：生成与 set_array 同形的 Generic。
					BpValue nested = BpValue.Generic();
					ReadArrayIntoValue(host, nested, depth + 1, reader);
					AssignNamedStruct(value, property.name, nested);
					break;
				}
				default:
					DrainChildren(reader, depth + 1);
					break;
			}
		}
	}

	// assign_named_integer
	private static void AssignNamedInteger(BpValue value, string name, int integer)
	{
		if (value.kind == BpValueKind.Integer && name == "scalar") value.intValue = integer;
		else if (value.kind == BpValueKind.Float && name == "scalar") value.floatValue = integer;
		else if (value.kind == BpValueKind.Generic) AddGeneric(value, name, BpValue.Integer(integer));
		else AssignNamedScalar(value, name, integer);
	}

	// assign_named_enum
	private static void AssignNamedEnum(BpValue value, string name, int integer)
	{
		if (value.kind == BpValueKind.Enum) value.intValue = integer;
		else if (value.kind == BpValueKind.Generic) AddGeneric(value, name, BpValue.Enum(integer));
		else AssignNamedInteger(value, name, integer);
	}

	// assign_named_scalar
	private static void AssignNamedScalar(BpValue value, string name, float scalar)
	{
		switch (value.kind)
		{
			case BpValueKind.Vector2:
				switch (name)
				{
					case "x": value.vector2.x = scalar; break;
					case "y": value.vector2.y = scalar; break;
				}
				break;
			case BpValueKind.Vector3:
				switch (name)
				{
					case "x": value.vector3.x = scalar; break;
					case "y": value.vector3.y = scalar; break;
					case "z": value.vector3.z = scalar; break;
				}
				break;
			case BpValueKind.Quaternion:
				switch (name)
				{
					case "x": value.quaternion.x = scalar; break;
					case "y": value.quaternion.y = scalar; break;
					case "z": value.quaternion.z = scalar; break;
					case "w": value.quaternion.w = scalar; break;
				}
				break;
			case BpValueKind.Color:
				switch (name)
				{
					case "r": value.color.r = scalar; break;
					case "g": value.color.g = scalar; break;
					case "b": value.color.b = scalar; break;
					case "a": value.color.a = scalar; break;
				}
				break;
			case BpValueKind.Rect:
				switch (name)
				{
					case "x": value.rect.x = scalar; break;
					case "y": value.rect.y = scalar; break;
					case "width": value.rect.width = scalar; break;
					case "height": value.rect.height = scalar; break;
				}
				break;
			case BpValueKind.Keyframe:
				switch (name)
				{
					case "time":
					case "m_Time": value.keyframe.time = scalar; break;
					case "value":
					case "m_Value": value.keyframe.value = scalar; break;
					case "inSlope":
					case "m_InTangent": value.keyframe.inTangent = scalar; break;
					case "outSlope":
					case "m_OutTangent": value.keyframe.outTangent = scalar; break;
				}
				break;
			case BpValueKind.Float:
				if (name == "scalar") value.floatValue = scalar;
				break;
			case BpValueKind.Integer:
				if (name == "scalar") value.intValue = (int)scalar;
				break;
			case BpValueKind.Generic:
				AddGeneric(value, name, BpValue.Float(scalar));
				break;
		}
	}

	// assign_named_bool
	private static void AssignNamedBool(BpValue value, string name, bool b)
	{
		if (value.kind == BpValueKind.Generic) AddGeneric(value, name, BpValue.Boolean(b));
		else if (value.kind == BpValueKind.Boolean) value.boolValue = b;
	}

	// assign_named_string
	private static void AssignNamedString(BpValue value, string name, string s)
	{
		if (value.kind == BpValueKind.Generic) AddGeneric(value, name, BpValue.Str(s));
		else if (value.kind == BpValueKind.String) value.stringValue = s;
	}

	// assign_named_struct
	private static void AssignNamedStruct(BpValue value, string name, BpValue nested)
	{
		switch (value.kind)
		{
			case BpValueKind.Bounds:
				if (name == "m_Center" || name == "center")
				{
					if (nested.kind == BpValueKind.Vector3) value.bounds.center = nested.vector3;
				}
				else if (name == "m_Extent" || name == "extents")
				{
					if (nested.kind == BpValueKind.Vector3) value.bounds.extents = nested.vector3;
				}
				break;
			case BpValueKind.Generic:
				AddGeneric(value, name, nested);
				break;
		}
	}

	// read_animation_curve
	private static void ReadAnimationCurve(IBpRuntimeHost host, int component, string fieldName, int depth, BpObjectReader reader)
	{
		int d = depth;
		while (reader.GetIndentation() == d)
		{
			reader.ReadProperty(); // `Generic keys` wrapper
			d += 1;

			// 可选 `Integer length = N`
			int length = 0;
			if (reader.GetIndentation() == d)
			{
				BpPropertyData lenProp = reader.ReadProperty();
				if (lenProp != null) length = lenProp.IntegerValue();
			}

			List<BpKeyframe> keys = new List<BpKeyframe>(length > 0 ? length : 0);
			while (reader.GetIndentation() == d)
			{
				BpPropertyData prop = reader.ReadProperty();
				if (prop == null) break;
				if (prop.type == "Element")
				{
					int elementIndex = ParseIntOrZero(prop.name);
					while (keys.Count <= elementIndex) keys.Add(default(BpKeyframe));
					while (reader.GetIndentation() == d + 1)
					{
						BpPropertyData inner = reader.ReadProperty();
						if (inner == null) break;
						if (inner.type == "Generic" || inner.type == "Keyframe")
						{
							BpValue val = BpValue.OfKeyframe(keys[elementIndex]);
							ReadGeneric(host, val, d + 1, reader);
							if (val.kind == BpValueKind.Keyframe) keys[elementIndex] = val.keyframe;
						}
					}
				}
			}

			d -= 1;
			host.SetAnimationCurve(component, fieldName, keys);
		}
	}

	// read_array
	private static void ReadArray(IBpRuntimeHost host, int component, string fieldName, int depth, BpObjectReader reader)
	{
		// `ArraySize size = N`
		int size = 0;
		if (reader.GetIndentation() == depth)
		{
			BpPropertyData prop = reader.ReadProperty();
			if (prop != null) size = prop.IntegerValue();
		}

		List<BpArrayElement> elements = new List<BpArrayElement>();
		while (reader.GetIndentation() == depth)
		{
			BpPropertyData prop = reader.ReadProperty();
			if (prop == null) break;
			if (prop.type != "Element") continue;
			int elementIndex = ParseIntOrZero(prop.name);

			if (reader.GetIndentation() != depth + 1) continue;
			BpPropertyData inner = reader.ReadProperty();
			if (inner == null) break;
			switch (inner.type)
			{
				case "Integer":
					elements.Add(El(elementIndex, BpValue.Integer(inner.IntegerValue())));
					break;
				case "Float":
					elements.Add(El(elementIndex, BpValue.Float(inner.FloatValue())));
					break;
				case "Boolean":
					elements.Add(El(elementIndex, BpValue.Boolean(inner.BoolValue())));
					break;
				case "Enum":
					elements.Add(El(elementIndex, BpValue.Enum(inner.IntegerValue())));
					break;
				case "String":
				{
					BpValue v = BpValue.Str(inner.StringValue());
					reader.ReadProperty();
					elements.Add(El(elementIndex, v));
					break;
				}
				case "ObjectReference":
				{
					BpValue resolved = host.ReferencedObject(inner.IntegerValue());
					elements.Add(El(elementIndex, resolved != null ? resolved : BpValue.Null()));
					break;
				}
				case "Vector2":
				case "Vector3":
				case "Quaternion":
				case "Color":
				case "Rect":
				case "Bounds":
				case "Keyframe":
				case "16":
				{
					BpValue v = DefaultForType(inner.type);
					ReadGeneric(host, v, depth + 2, reader);
					elements.Add(El(elementIndex, v));
					break;
				}
				case "Generic":
				{
					BpValue v = BpValue.Generic();
					int childDepth = reader.GetIndentation();
					if (childDepth > depth) ReadGeneric(host, v, childDepth, reader);
					elements.Add(El(elementIndex, v));
					break;
				}
				default:
					DrainChildren(reader, depth + 2);
					break;
			}

			// 同元素内残留的兄弟 Float 行并入上一元素。
			while (reader.GetIndentation() == depth + 1)
			{
				BpPropertyData extra = reader.ReadProperty();
				if (extra == null) break;
				if (extra.type == "Float")
				{
					if (elements.Count > 0)
						AssignNamedScalar(elements[elements.Count - 1].value, extra.name, extra.FloatValue());
				}
				else
				{
					DrainChildren(reader, depth + 2);
					break;
				}
			}
		}

		host.SetArray(component, fieldName, size, elements);
	}

	// read_array_into_value
	private static void ReadArrayIntoValue(IBpRuntimeHost host, BpValue outValue, int depth, BpObjectReader reader)
	{
		int size = 0;
		if (reader.GetIndentation() == depth)
		{
			BpPropertyData prop = reader.ReadProperty();
			if (prop != null) size = prop.IntegerValue();
		}

		if (outValue.kind != BpValueKind.Generic) return;
		List<KeyValuePair<string, BpValue>> entries = outValue.generic;
		if (entries == null)
		{
			entries = new List<KeyValuePair<string, BpValue>>();
			outValue.generic = entries;
		}
		entries.Add(new KeyValuePair<string, BpValue>("size", BpValue.Integer(size)));

		while (reader.GetIndentation() == depth)
		{
			BpPropertyData prop = reader.ReadProperty();
			if (prop == null) break;
			if (prop.type != "Element") continue;
			int elementIndex = ParseIntOrZero(prop.name);

			if (reader.GetIndentation() != depth + 1) continue;
			BpPropertyData inner = reader.ReadProperty();
			if (inner == null) break;
			BpValue elementValue;
			switch (inner.type)
			{
				case "Integer":
					elementValue = BpValue.Integer(inner.IntegerValue());
					break;
				case "Float":
					elementValue = BpValue.Float(inner.FloatValue());
					break;
				case "Boolean":
					elementValue = BpValue.Boolean(inner.BoolValue());
					break;
				case "Enum":
					elementValue = BpValue.Enum(inner.IntegerValue());
					break;
				case "String":
				{
					elementValue = BpValue.Str(inner.StringValue());
					reader.ReadProperty();
					break;
				}
				case "ObjectReference":
				{
					BpValue resolved = host.ReferencedObject(inner.IntegerValue());
					elementValue = resolved != null ? resolved : BpValue.Null();
					break;
				}
				case "Vector2":
				case "Vector3":
				case "Quaternion":
				case "Color":
				case "Rect":
				case "Bounds":
				case "Keyframe":
				case "16":
				{
					elementValue = DefaultForType(inner.type);
					ReadGeneric(host, elementValue, depth + 2, reader);
					break;
				}
				case "Generic":
				{
					elementValue = BpValue.Generic();
					int childDepth = reader.GetIndentation();
					if (childDepth > depth) ReadGeneric(host, elementValue, childDepth, reader);
					break;
				}
				default:
					DrainChildren(reader, depth + 2);
					continue;
			}
			entries.Add(new KeyValuePair<string, BpValue>(elementIndex.ToString(System.Globalization.CultureInfo.InvariantCulture), elementValue));

			while (reader.GetIndentation() == depth + 1)
			{
				BpPropertyData extra = reader.ReadProperty();
				if (extra == null) break;
				if (extra.type == "Float")
				{
					if (entries.Count > 0)
						AssignNamedScalar(entries[entries.Count - 1].Value, extra.name, extra.FloatValue());
				}
				else
				{
					DrainChildren(reader, depth + 2);
					break;
				}
			}
		}
	}

	// read_particle_system_module
	private static void ReadParticleSystemModule(IBpRuntimeHost host, int particleSystem, string module, int depth, BpObjectReader reader)
	{
		while (reader.GetIndentation() == depth)
		{
			BpPropertyData property = reader.ReadProperty();
			if (property == null) return;
			if (module == "InitialModule" && property.type == "Generic" && property.name == "startLifetime")
			{
				if (reader.GetIndentation() == depth + 1)
				{
					BpPropertyData inner = reader.ReadProperty();
					if (inner != null) host.SetParticleStartLifetime(particleSystem, inner.FloatValue());
				}
			}
			else if (module == "InitialModule" && property.type == "Generic" && property.name == "startSpeed")
			{
				if (reader.GetIndentation() == depth + 1)
				{
					BpPropertyData inner = reader.ReadProperty();
					if (inner != null) host.SetParticleStartSpeed(particleSystem, inner.FloatValue());
				}
			}
			else if (module == "EmissionModule" && property.type == "Generic" && property.name == "rate")
			{
				if (reader.GetIndentation() == depth + 1)
				{
					BpPropertyData inner = reader.ReadProperty();
					if (inner != null) host.SetParticleEmissionRate(particleSystem, inner.FloatValue());
				}
			}
			else if (module == "ShapeModule")
			{
				// C# 体为空（`if (!(... enabled == true)) {}`）——什么都不做。
			}
		}
	}

	// read_particle_system
	private static void ReadParticleSystem(IBpRuntimeHost host, int particleSystem, int depth, BpObjectReader reader)
	{
		while (reader.GetIndentation() == depth)
		{
			BpPropertyData property = reader.ReadProperty();
			if (property == null) return;
			if (property.type == "Generic" &&
				(property.name == "InitialModule" || property.name == "EmissionModule" || property.name == "ShapeModule"))
			{
				ReadParticleSystemModule(host, particleSystem, property.name, depth + 1, reader);
			}
		}
	}

	// =======================================================================
	// PrefabOverrides Transform 同步辅助（第 3 段）
	// =======================================================================

	// 等价 Rust `str::lines()`：按 '\n' 切分并去掉行尾 '\r'，且不返回末尾空行。
	// （public：供同程序集的 BpScene.FromOverrideText 复用，避免两份实现分叉）
	public static string[] SplitLinesLikeRust(string text)
	{
		List<string> result = new List<string>();
		if (text == null || text.Length == 0) return result.ToArray();
		int start = 0;
		int len = text.Length;
		while (start < len)
		{
			int nl = text.IndexOf('\n', start);
			string line;
			if (nl < 0)
			{
				line = text.Substring(start);
				start = len;
			}
			else
			{
				line = text.Substring(start, nl - start);
				start = nl + 1;
			}
			if (line.Length > 0 && line[line.Length - 1] == '\r')
				line = line.Substring(0, line.Length - 1);
			result.Add(line);
		}
		return result.ToArray();
	}

	private static bool FindLocalLine(string[] lines, string target, out int lineIndex, out string indent)
	{
		lineIndex = -1;
		indent = null;
		for (int i = 0; i < lines.Length; i++)
		{
			string line = lines[i];
			string trimmed = line.TrimStart('\t');
			if (trimmed == target)
			{
				indent = line.Substring(0, line.Length - trimmed.Length);
				lineIndex = i;
				return true;
			}
		}
		return false;
	}

	private static bool UpdateFloatLine(List<string> lines, int startLine, string fieldName, float newValue, string fieldIndent)
	{
		string prefix = fieldIndent + "Float " + fieldName + " = ";
		for (int i = startLine + 1; i < lines.Count; i++)
		{
			string line = lines[i];
			string trimmed = line.TrimStart('\t');
			if (trimmed.StartsWith("Float "))
			{
				if (line.StartsWith(prefix))
				{
					string rest = line.Substring(prefix.Length);
					bool numeric = rest.Length > 0;
					foreach (char c in rest)
					{
						if (!((c >= '0' && c <= '9') || c == '-' || c == '.')) { numeric = false; break; }
					}
					if (numeric)
					{
						lines[i] = prefix + newValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
						return true;
					}
				}
			}
			else if (trimmed.Length != 0)
			{
				// 已越过 Float 块——停止搜索。
				break;
			}
		}
		return false;
	}

	// sync_override_local_position
	public static string SyncOverrideLocalPosition(string text, float newWorldX, float newWorldY, float newWorldZ,
		float parentWorldX, float parentWorldY, float parentWorldZ)
	{
		float localX = newWorldX - parentWorldX;
		float localY = newWorldY - parentWorldY;
		float localZ = newWorldZ - parentWorldZ;

		string[] rawLines = SplitLinesLikeRust(text);
		int posLine;
		string indent;
		if (!FindLocalLine(rawLines, "Vector3 m_LocalPosition", out posLine, out indent))
			return text;
		string fieldIndent = indent + "\t";

		List<string> lines = new List<string>(rawLines);
		UpdateFloatLine(lines, posLine, "x", localX, fieldIndent);
		UpdateFloatLine(lines, posLine, "y", localY, fieldIndent);
		UpdateFloatLine(lines, posLine, "z", localZ, fieldIndent);

		return string.Join("\n", lines);
	}

	// sync_override_local_rotation
	public static string SyncOverrideLocalRotation(string text, float newWorldZDeg, float parentWorldZDeg)
	{
		float localZDeg = newWorldZDeg - parentWorldZDeg;
		double half = localZDeg * Mathf.Deg2Rad * 0.5;
		float qz = (float)Math.Sin(half);
		float qw = (float)Math.Cos(half);

		string[] rawLines = SplitLinesLikeRust(text);
		int rotLine;
		string indent;
		if (!FindLocalLine(rawLines, "Quaternion m_LocalRotation", out rotLine, out indent))
			return text;
		string fieldIndent = indent + "\t";

		List<string> lines = new List<string>(rawLines);
		UpdateFloatLine(lines, rotLine, "x", 0f, fieldIndent);
		UpdateFloatLine(lines, rotLine, "y", 0f, fieldIndent);
		UpdateFloatLine(lines, rotLine, "z", qz, fieldIndent);
		UpdateFloatLine(lines, rotLine, "w", qw, fieldIndent);

		return string.Join("\n", lines);
	}

	// sync_override_local_scale
	public static string SyncOverrideLocalScale(string text, float newScaleX, float newScaleY, float newScaleZ)
	{
		string[] rawLines = SplitLinesLikeRust(text);
		int scaleLine;
		string indent;
		if (!FindLocalLine(rawLines, "Vector3 m_LocalScale", out scaleLine, out indent))
			return text;
		string fieldIndent = indent + "\t";

		List<string> lines = new List<string>(rawLines);
		UpdateFloatLine(lines, scaleLine, "x", newScaleX, fieldIndent);
		UpdateFloatLine(lines, scaleLine, "y", newScaleY, fieldIndent);
		UpdateFloatLine(lines, scaleLine, "z", newScaleZ, fieldIndent);

		return string.Join("\n", lines);
	}

	// sync_override_transform
	public static string SyncOverrideTransform(string text, Vector3 newWorldPos, float newWorldRotZDeg,
		Vector3 newScale, Vector3 parentWorldPos, float parentWorldRotZDeg)
	{
		string updated = SyncOverrideLocalPosition(text, newWorldPos.x, newWorldPos.y, newWorldPos.z,
			parentWorldPos.x, parentWorldPos.y, parentWorldPos.z);
		updated = SyncOverrideLocalRotation(updated, newWorldRotZDeg, parentWorldRotZDeg);
		updated = SyncOverrideLocalScale(updated, newScale.x, newScale.y, newScale.z);
		return updated;
	}
}
