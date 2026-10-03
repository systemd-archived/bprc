using System.Collections.Generic;
using UnityEngine;

// 1:1 移植 badpiggies-editor 的 domain/prefab_asset.rs（Unity prefab YAML 解析）
// 说明：HermiteKey 原属 data::unity_anim（阶段 3），此处先以 BpHermiteKey 承载，字段与语义一致。
public struct BpHermiteKey
{
	public float time;
	public float value;
	public float inSlope;
	public float outSlope;

	public BpHermiteKey(float t, float v, float inS, float outS)
	{
		time = t; value = v; inSlope = inS; outSlope = outS;
	}
}

public class BpPrefabGameObject
{
	public string name = "";
	public List<string> componentIds = new List<string>();
}

public class BpPrefabTransform
{
	public string gameObjectId = "";
	public float[] localPos = new float[3];
	public float[] localRotation = new float[] { 0f, 0f, 0f, 1f };
	public float[] localScale = new float[] { 1f, 1f, 1f };
	public string parentId; // null = 无父
	public List<string> children = new List<string>();
	public int rootOrder;

	public float[] TransformPoint(float[] p)
	{
		float[] scaled = { p[0] * localScale[0], p[1] * localScale[1], p[2] * localScale[2] };
		float[] rotated = BpPrefabAsset.RotateVec3(localRotation, scaled);
		return new[] { rotated[0] + localPos[0], rotated[1] + localPos[1], rotated[2] + localPos[2] };
	}
}

public enum BpPrefabComponentKind { Builtin, MonoBehaviour }

public class BpPrefabComponent
{
	public BpPrefabComponentKind kind;
	public string builtinName = "";
	public string scriptGuid = "";
	public string rawDoc = "";

	public string ScriptGuid() => kind == BpPrefabComponentKind.MonoBehaviour ? scriptGuid : null;

	public string ComponentSuffix()
	{
		if (kind == BpPrefabComponentKind.Builtin) return builtinName;
		return BpPrefabAsset.MonoBehaviourSuffix(scriptGuid);
	}

	public float? FieldF32(string fieldName)
	{
		string v = FieldValue(fieldName);
		if (v == null) return null;
		float f;
		return float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f) ? f : (float?)null;
	}

	public int? FieldI32(string fieldName)
	{
		string v = FieldValue(fieldName);
		if (v == null) return null;
		int i;
		return int.TryParse(v, out i) ? i : (int?)null;
	}

	public float[] FieldVec3(string fieldName)
	{
		string v = FieldValue(fieldName);
		return v == null ? null : BpPrefabAsset.ParseVec3(v);
	}

	public bool? FieldBool(string fieldName)
	{
		string v = FieldValue(fieldName);
		if (v == null) return null;
		if (v == "1" || v == "True" || v == "true") return true;
		if (v == "0" || v == "False" || v == "false") return false;
		return null;
	}

	public string FieldStr(string fieldName) => FieldValue(fieldName);

	public string FieldGuid(string fieldName)
	{
		string v = FieldValue(fieldName);
		if (v != null)
		{
			string g = BpPrefabAsset.ExtractGuid(v);
			if (g != null) return g;
		}
		string header = "\n  " + fieldName + ":\n";
		int start = rawDoc.IndexOf(header, System.StringComparison.Ordinal);
		if (start < 0) return null;
		start += header.Length;
		foreach (string rawLine in rawDoc.Substring(start).Split('\n'))
		{
			if (!rawLine.StartsWith("  ")) break;
			string g2 = BpPrefabAsset.ExtractGuid(rawLine.Trim());
			if (g2 != null) return g2;
		}
		return null;
	}

	public string FieldFileId(string fieldName)
	{
		string v = FieldValue(fieldName);
		if (v != null)
		{
			string f = BpPrefabAsset.ExtractFileId(v);
			if (f != null) return f;
		}
		string header = "\n  " + fieldName + ":\n";
		int start = rawDoc.IndexOf(header, System.StringComparison.Ordinal);
		if (start < 0) return null;
		start += header.Length;
		foreach (string rawLine in rawDoc.Substring(start).Split('\n'))
		{
			if (!rawLine.StartsWith("  ")) break;
			string f2 = BpPrefabAsset.ExtractFileId(rawLine.Trim());
			if (f2 != null) return f2;
		}
		return null;
	}

	public List<BpHermiteKey> FieldCurve(string fieldName)
	{
		string fieldHeader = "\n  " + fieldName + ":\n";
		int fieldStart = rawDoc.IndexOf(fieldHeader, System.StringComparison.Ordinal);
		if (fieldStart < 0) return null;
		fieldStart += fieldHeader.Length;
		const string curveMarker = "    m_Curve:\n";
		int rel = rawDoc.IndexOf(curveMarker, fieldStart, System.StringComparison.Ordinal);
		if (rel < 0) return null;
		int curveStart = rel + curveMarker.Length;

		List<BpHermiteKey> keys = new List<BpHermiteKey>();
		float? time = null, value = null, inSlope = null, outSlope = null;

		foreach (string line in rawDoc.Substring(curveStart).Split('\n'))
		{
			if (line.StartsWith("    - "))
			{
				BpHermiteKey? k = BuildKey(time, value, inSlope, outSlope);
				if (k.HasValue) keys.Add(k.Value);
				time = value = inSlope = outSlope = null;
				continue;
			}
			if (line.StartsWith("      time: ")) { time = ParseF(line.Substring(12)); continue; }
			if (line.StartsWith("      value: ")) { value = ParseF(line.Substring(13)); continue; }
			if (line.StartsWith("      inSlope: ")) { inSlope = ParseF(line.Substring(15)); continue; }
			if (line.StartsWith("      outSlope: ")) { outSlope = ParseF(line.Substring(16)); continue; }
			if (!line.StartsWith("      ")) break;
		}
		BpHermiteKey? last = BuildKey(time, value, inSlope, outSlope);
		if (last.HasValue) keys.Add(last.Value);
		return keys.Count == 0 ? null : keys;
	}

	private static float? ParseF(string s)
	{
		float f;
		return float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f) ? f : (float?)null;
	}

	private static BpHermiteKey? BuildKey(float? t, float? v, float? i, float? o)
	{
		if (!t.HasValue || !v.HasValue || !i.HasValue || !o.HasValue) return null;
		return new BpHermiteKey(t.Value, v.Value, i.Value, o.Value);
	}

	private string FieldValue(string fieldName)
	{
		return BpPrefabAsset.FieldValue(rawDoc, fieldName + ": ");
	}
}

public class BpPrefabAssetDocument
{
	public string rootGameObjectId = "";
	public Dictionary<string, BpPrefabGameObject> gameObjects = new Dictionary<string, BpPrefabGameObject>();
	public Dictionary<string, BpPrefabTransform> transforms = new Dictionary<string, BpPrefabTransform>();
	public Dictionary<string, BpPrefabComponent> components = new Dictionary<string, BpPrefabComponent>();

	public static BpPrefabAssetDocument Parse(string text)
	{
		string rootGo = null;
		BpPrefabAssetDocument doc = new BpPrefabAssetDocument();

		string[] docs = text.Split(new[] { "--- " }, System.StringSplitOptions.None);
		for (int di = 1; di < docs.Length; di++)
		{
			string d = docs[di];
			string header = BpPrefabAsset.FirstLine(d);
			uint classId;
			string fileId;
			if (!BpPrefabAsset.ParseDocHeader(header, out classId, out fileId)) continue;

			if (classId == 1001)
			{
				string v = BpPrefabAsset.FieldValue(d, "m_RootGameObject: ");
				rootGo = v == null ? rootGo : BpPrefabAsset.ExtractFileId(v);
			}
			else if (classId == 1)
			{
				string v = BpPrefabAsset.FieldValue(d, "m_Name: ");
				string nm = v ?? fileId;
				doc.gameObjects[fileId] = new BpPrefabGameObject { name = nm, componentIds = BpPrefabAsset.ParseComponentIds(d) };
			}
			else if (classId == 4)
			{
				string v = BpPrefabAsset.FieldValue(d, "m_GameObject: ");
				string goId = v == null ? null : BpPrefabAsset.ExtractFileId(v);
				if (goId == null) continue;
				float[] pos = BpPrefabAsset.ParseVec3Or(BpPrefabAsset.FieldValue(d, "m_LocalPosition: "), new[] { 0f, 0f, 0f });
				float[] rot = BpPrefabAsset.ParseVec4Or(BpPrefabAsset.FieldValue(d, "m_LocalRotation: "), new[] { 0f, 0f, 0f, 1f });
				float[] scl = BpPrefabAsset.ParseVec3Or(BpPrefabAsset.FieldValue(d, "m_LocalScale: "), new[] { 1f, 1f, 1f });
				string fatherVal = BpPrefabAsset.FieldValue(d, "m_Father: ");
				string parentId = fatherVal == null ? null : BpPrefabAsset.ExtractFileId(fatherVal);
				if (parentId == "0") parentId = null;
				string roVal = BpPrefabAsset.FieldValue(d, "m_RootOrder: ");
				int rootOrder = 0;
				if (roVal != null) int.TryParse(roVal, out rootOrder);
				doc.transforms[fileId] = new BpPrefabTransform
				{
					gameObjectId = goId,
					localPos = pos,
					localRotation = rot,
					localScale = scl,
					parentId = parentId,
					children = BpPrefabAsset.ParseChildren(d),
					rootOrder = rootOrder
				};
				doc.components[fileId] = new BpPrefabComponent { kind = BpPrefabComponentKind.Builtin, builtinName = "Transform", rawDoc = d };
			}
			else if (classId == 114)
			{
				string v = BpPrefabAsset.FieldValue(d, "m_GameObject: ");
				if (v == null || BpPrefabAsset.ExtractFileId(v) == null) continue;
				string sv = BpPrefabAsset.FieldValue(d, "m_Script: ");
				string guid = sv == null ? null : BpPrefabAsset.ExtractGuid(sv);
				if (guid == null) continue;
				doc.components[fileId] = new BpPrefabComponent { kind = BpPrefabComponentKind.MonoBehaviour, scriptGuid = guid, rawDoc = d };
			}
			else
			{
				string v = BpPrefabAsset.FieldValue(d, "m_GameObject: ");
				if (v == null || BpPrefabAsset.ExtractFileId(v) == null) continue;
				string second = BpPrefabAsset.SecondLineTrimmed(d);
				if (second == null || !second.EndsWith(":")) continue;
				string compName = second.Substring(0, second.Length - 1);
				doc.components[fileId] = new BpPrefabComponent { kind = BpPrefabComponentKind.Builtin, builtinName = compName, rawDoc = d };
			}
		}

		if (rootGo == null)
		{
			foreach (KeyValuePair<string, BpPrefabTransform> kv in doc.transforms)
			{
				if (kv.Value.parentId == null) { rootGo = kv.Value.gameObjectId; break; }
			}
		}
		if (rootGo == null) return null;
		doc.rootGameObjectId = rootGo;
		return doc;
	}

	public List<string> RootComponentSuffixes()
	{
		List<string> suffixes = new List<string>();
		BpPrefabGameObject root;
		if (!gameObjects.TryGetValue(rootGameObjectId, out root)) return suffixes;
		foreach (string cid in root.componentIds)
		{
			BpPrefabComponent c;
			if (!components.TryGetValue(cid, out c)) continue;
			string suffix = c.ComponentSuffix();
			if (suffix == null) continue;
			if (!suffixes.Contains(suffix)) suffixes.Add(suffix);
		}
		return suffixes;
	}

	public List<string> RootScriptGuids()
	{
		List<string> guids = new List<string>();
		BpPrefabGameObject root;
		if (!gameObjects.TryGetValue(rootGameObjectId, out root)) return guids;
		foreach (string cid in root.componentIds)
		{
			BpPrefabComponent c;
			if (!components.TryGetValue(cid, out c)) continue;
			string g = c.ScriptGuid();
			if (g == null) continue;
			if (!guids.Contains(g)) guids.Add(g);
		}
		return guids;
	}

	public BpPrefabComponent RootComponent(string suffix)
	{
		BpPrefabGameObject root;
		if (!gameObjects.TryGetValue(rootGameObjectId, out root)) return null;
		foreach (string cid in root.componentIds)
		{
			BpPrefabComponent c;
			if (!components.TryGetValue(cid, out c)) continue;
			if (c.ComponentSuffix() == suffix) return c;
		}
		return null;
	}

	public BpPrefabComponent ComponentByGameObjectName(string gameObjectName, string suffix)
	{
		string goId = FindGameObjectIdByName(gameObjectName);
		if (goId == null) return null;
		BpPrefabGameObject go;
		if (!gameObjects.TryGetValue(goId, out go)) return null;
		foreach (string cid in go.componentIds)
		{
			BpPrefabComponent c;
			if (!components.TryGetValue(cid, out c)) continue;
			if (c.ComponentSuffix() == suffix) return c;
		}
		return null;
	}

	public BpPrefabTransform RootTransform()
	{
		foreach (KeyValuePair<string, BpPrefabTransform> kv in transforms)
			if (kv.Value.gameObjectId == rootGameObjectId) return kv.Value;
		return null;
	}

	public BpPrefabTransform TransformByGameObjectName(string name)
	{
		string goId = FindGameObjectIdByName(name);
		if (goId == null) return null;
		foreach (KeyValuePair<string, BpPrefabTransform> kv in transforms)
			if (kv.Value.gameObjectId == goId) return kv.Value;
		return null;
	}

	private string FindGameObjectIdByName(string name)
	{
		foreach (KeyValuePair<string, BpPrefabGameObject> kv in gameObjects)
			if (kv.Value.name == name) return kv.Key;
		return null;
	}

	private string FindTransformIdByName(string name)
	{
		foreach (KeyValuePair<string, BpPrefabTransform> kv in transforms)
		{
			BpPrefabGameObject go;
			if (!gameObjects.TryGetValue(kv.Value.gameObjectId, out go)) continue;
			if (go.name == name) return kv.Key;
		}
		return null;
	}

	public float[] CumulativeScaleByGameObjectName(string name)
	{
		string tid = FindTransformIdByName(name);
		if (tid == null) return null;
		float[] scale = { 1f, 1f, 1f };
		string cur = tid;
		while (cur != null)
		{
			BpPrefabTransform t;
			if (!transforms.TryGetValue(cur, out t)) return null;
			scale[0] *= t.localScale[0];
			scale[1] *= t.localScale[1];
			scale[2] *= t.localScale[2];
			cur = t.parentId;
		}
		return scale;
	}

	public float[] CumulativeLocalPosByGameObjectName(string name)
	{
		string tid = FindTransformIdByName(name);
		if (tid == null) return null;
		float[] pos = { 0f, 0f, 0f };
		string cur = tid;
		while (cur != null)
		{
			BpPrefabTransform t;
			if (!transforms.TryGetValue(cur, out t)) return null;
			if (t.parentId == null) break; // 排除根自身
			pos[0] += t.localPos[0]; pos[1] += t.localPos[1]; pos[2] += t.localPos[2];
			cur = t.parentId;
		}
		return pos;
	}

	public BpPrefabTransform TransformByPath(string path)
	{
		string cur = RootTransformId();
		foreach (string seg in path.Split('/'))
		{
			if (seg.Length == 0) continue;
			if (cur == null) return null;
			BpPrefabTransform parent;
			if (!transforms.TryGetValue(cur, out parent)) return null;
			string found = null;
			foreach (string childId in parent.children)
			{
				BpPrefabTransform child;
				if (!transforms.TryGetValue(childId, out child)) return null;
				BpPrefabGameObject go;
				if (!gameObjects.TryGetValue(child.gameObjectId, out go)) return null;
				if (go.name == seg) { found = childId; break; }
			}
			cur = found;
		}
		if (cur == null) return null;
		BpPrefabTransform r;
		return transforms.TryGetValue(cur, out r) ? r : null;
	}

	public BpPrefabComponent ComponentByPath(string path, string suffix)
	{
		BpPrefabTransform t = TransformByPath(path);
		if (t == null) return null;
		BpPrefabGameObject go;
		if (!gameObjects.TryGetValue(t.gameObjectId, out go)) return null;
		foreach (string cid in go.componentIds)
		{
			BpPrefabComponent c;
			if (!components.TryGetValue(cid, out c)) continue;
			if (c.ComponentSuffix() == suffix) return c;
		}
		return null;
	}

	public float[] CumulativeLocalPosByPath(string path)
	{
		float[] pos = { 0f, 0f, 0f };
		string cur = RootTransformId();
		foreach (string seg in path.Split('/'))
		{
			if (seg.Length == 0) continue;
			if (cur == null) return null;
			BpPrefabTransform parent;
			if (!transforms.TryGetValue(cur, out parent)) return null;
			string found = null;
			foreach (string childId in parent.children)
			{
				BpPrefabTransform child;
				if (!transforms.TryGetValue(childId, out child)) return null;
				BpPrefabGameObject go;
				if (!gameObjects.TryGetValue(child.gameObjectId, out go)) return null;
				if (go.name == seg)
				{
					pos[0] += child.localPos[0]; pos[1] += child.localPos[1]; pos[2] += child.localPos[2];
					found = childId;
					break;
				}
			}
			cur = found;
		}
		return cur == null ? null : pos;
	}

	public float[] CumulativeLocalScaleByPath(string path)
	{
		float[] scale = { 1f, 1f, 1f };
		string cur = RootTransformId();
		if (cur == null) return null;
		foreach (string seg in path.Split('/'))
		{
			if (seg.Length == 0) continue;
			if (cur == null) return null;
			BpPrefabTransform parent;
			if (!transforms.TryGetValue(cur, out parent)) return null;
			string found = null;
			foreach (string childId in parent.children)
			{
				BpPrefabTransform child;
				if (!transforms.TryGetValue(childId, out child)) return null;
				BpPrefabGameObject go;
				if (!gameObjects.TryGetValue(child.gameObjectId, out go)) return null;
				if (go.name == seg)
				{
					scale[0] *= child.localScale[0]; scale[1] *= child.localScale[1]; scale[2] *= child.localScale[2];
					found = childId;
					break;
				}
			}
			cur = found;
		}
		return cur == null ? null : scale;
	}

	public float[] TransformPointByPath(string path, float[] point)
	{
		BpPrefabTransform t = TransformByPath(path);
		if (t == null) return null;
		foreach (KeyValuePair<string, BpPrefabTransform> kv in transforms)
		{
			if (kv.Value.gameObjectId == t.gameObjectId) return TransformPointByFileId(kv.Key, point);
		}
		return null;
	}

	public float[] TransformPointByFileId(string fileId, float[] point)
	{
		float[] transformed = point;
		string cur = fileId;
		while (cur != null)
		{
			BpPrefabTransform t;
			if (!transforms.TryGetValue(cur, out t)) return null;
			if (t.parentId == null) break;
			transformed = t.TransformPoint(transformed);
			cur = t.parentId;
		}
		return transformed;
	}

	private string RootTransformId()
	{
		foreach (KeyValuePair<string, BpPrefabTransform> kv in transforms)
			if (kv.Value.parentId == null) return kv.Key;
		return null;
	}
}

public static class BpPrefabAsset
{
	private static readonly Dictionary<string, string> KnownMonoBehaviourGuidSuffixes = new Dictionary<string, string>
	{
		{ "ae5f82fde6e6559b4e6280a34047fbb4", "PositionSerializer" },
		{ "b0e2701ec6dd05b749c7483cb8824587", "Bridge" },
		{ "5e1217a38f28f58b406d6bfa1d443506", "BezierCurve" },
		{ "b0912da955fc22ac49cacf3bf45b15ff", "BezierMesh" },
		{ "f800f9004829d2bf463a4d04b1b26c2c", "Engine" },
		{ "136cea14b9e8b6964b418f8d2caa309d", "Fan" },
		{ "1c0b17aff10189b24f7a3dc26453b419", "PointLightSource" },
		{ "e8df771c7097e6a8496aaf8b005b6e19", "PartSecret" },
		{ "5eaf56a364255ba8499cf075501b0d29", "Rocket" },
		{ "d023fcf410bcc6ab493b676524aeacc2", "RopeVisualization" },
		{ "eaa85264a31f76994888187c4d3a9fb9", "Sprite" },
		{ "b011dfa16a4475b746a1372ea41fdf05", "UnmanagedSprite" },
		{ "3d46d566866fd29148f73f2aa9b6b572", "WindArea" }
	};

	public static string MonoBehaviourSuffix(string guid)
	{
		string s;
		return KnownMonoBehaviourGuidSuffixes.TryGetValue(guid, out s) ? s : null;
	}

	public static string FieldValue(string doc, string prefix)
	{
		foreach (string line in doc.Split('\n'))
		{
			string t = line.Trim();
			if (t.StartsWith(prefix, System.StringComparison.Ordinal))
				return t.Substring(prefix.Length).Trim();
		}
		return null;
	}

	public static bool ParseDocHeader(string header, out uint classId, out string fileId)
	{
		classId = 0; fileId = null;
		if (header == null) return false;
		string rest = header.Trim();
		const string tag = "!u!";
		if (!rest.StartsWith(tag, System.StringComparison.Ordinal)) return false;
		rest = rest.Substring(tag.Length);
		int amp = rest.IndexOf(" &", System.StringComparison.Ordinal);
		if (amp < 0) return false;
		uint id;
		if (!uint.TryParse(rest.Substring(0, amp), out id)) return false;
		classId = id;
		fileId = rest.Substring(amp + 2).Trim();
		return true;
	}

	public static string ExtractFileId(string value)
	{
		int start = value.IndexOf("fileID: ", System.StringComparison.Ordinal);
		if (start < 0) return null;
		start += "fileID: ".Length;
		string tail = value.Substring(start);
		int end = tail.Length;
		for (int i = 0; i < tail.Length; i++)
			if (tail[i] == ',' || tail[i] == '}') { end = i; break; }
		string id = tail.Substring(0, end).Trim();
		return id.Length == 0 ? null : id;
	}

	public static string ExtractGuid(string value)
	{
		int start = value.IndexOf("guid: ", System.StringComparison.Ordinal);
		if (start < 0) return null;
		start += "guid: ".Length;
		string tail = value.Substring(start);
		int end = tail.Length;
		for (int i = 0; i < tail.Length; i++)
			if (tail[i] == ',' || tail[i] == '}') { end = i; break; }
		string g = tail.Substring(0, end).Trim();
		return g.Length == 0 ? null : g;
	}

	public static float[] ParseVec3(string value)
	{
		return ParseVecN(value, 3);
	}

	public static float[] ParseVec4(string value)
	{
		return ParseVecN(value, 4);
	}

	public static float[] ParseVec3Or(string value, float[] fallback)
	{
		if (value == null) return fallback;
		float[] v = ParseVec3(value);
		return v ?? fallback;
	}

	public static float[] ParseVec4Or(string value, float[] fallback)
	{
		if (value == null) return fallback;
		float[] v = ParseVec4(value);
		return v ?? fallback;
	}

	private static float[] ParseVecN(string value, int n)
	{
		float[] outv = new float[n];
		bool[] seen = new bool[n];
		string t = value.Trim().TrimStart('{').TrimEnd('}');
		foreach (string part in t.Split(','))
		{
			string[] kv = part.Trim().Split(new[] { ':' }, 2);
			if (kv.Length != 2) return null;
			string axis = kv[0].Trim();
			int idx;
			switch (axis)
			{
				case "x": idx = 0; break;
				case "y": idx = 1; break;
				case "z": idx = 2; break;
				case "w": idx = 3; break;
				default: continue;
			}
			if (idx >= n) continue;
			float f;
			if (!float.TryParse(kv[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f)) return null;
			outv[idx] = f;
			seen[idx] = true;
		}
		foreach (bool s in seen) if (!s) return null;
		return outv;
	}

	public static float[] RotateVec3(float[] quat, float[] point)
	{
		float qx = quat[0], qy = quat[1], qz = quat[2], qw = quat[3];
		float[] uv =
		{
			qy * point[2] - qz * point[1],
			qz * point[0] - qx * point[2],
			qx * point[1] - qy * point[0]
		};
		float[] uuv =
		{
			qy * uv[2] - qz * uv[1],
			qz * uv[0] - qx * uv[2],
			qx * uv[1] - qy * uv[0]
		};
		return new[]
		{
			point[0] + 2f * (qw * uv[0] + uuv[0]),
			point[1] + 2f * (qw * uv[1] + uuv[1]),
			point[2] + 2f * (qw * uv[2] + uuv[2])
		};
	}

	public static List<string> ParseComponentIds(string doc)
	{
		List<string> ids = new List<string>();
		bool inComponents = false;
		foreach (string raw in doc.Split('\n'))
		{
			string t = raw.Trim();
			if (t.StartsWith("m_Component:", System.StringComparison.Ordinal)) { inComponents = true; continue; }
			if (!inComponents) continue;
			if (t.StartsWith("- ", System.StringComparison.Ordinal))
			{
				string cid = ExtractFileId(t.Substring(2));
				if (cid != null) ids.Add(cid);
				continue;
			}
			if (t.Length != 0) break;
		}
		return ids;
	}

	public static List<string> ParseChildren(string doc)
	{
		List<string> children = new List<string>();
		bool inChildren = false;
		foreach (string raw in doc.Split('\n'))
		{
			string t = raw.Trim();
			if (t.StartsWith("m_Children:", System.StringComparison.Ordinal)) { inChildren = true; continue; }
			if (!inChildren) continue;
			if (t.StartsWith("- ", System.StringComparison.Ordinal))
			{
				string cid = ExtractFileId(t.Substring(2));
				if (cid != null) children.Add(cid);
				continue;
			}
			if (t.StartsWith("m_Father:", System.StringComparison.Ordinal)) break;
			if (t.Length != 0) break;
		}
		return children;
	}

	public static string FirstLine(string s)
	{
		int i = s.IndexOf('\n');
		return i < 0 ? s : s.Substring(0, i);
	}

	public static string SecondLineTrimmed(string s)
	{
		string[] lines = s.Split('\n');
		return lines.Length > 1 ? lines[1].Trim() : null;
	}
}
