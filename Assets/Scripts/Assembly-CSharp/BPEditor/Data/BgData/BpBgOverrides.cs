using System;
using System.Collections.Generic;
using UnityEngine;

// 1:1 移植 badpiggies-editor 的 data/bg_data/overrides.rs。
// BG override 文本解析、应用到主题精灵、以及 PositionSerializer 的
// childLocalPositions → 组位置覆盖映射。

public static class BpBgOverridesOps
{
	// scene_value_as_vec3
	private static float[] SceneValueAsVec3(BpValue value)
	{
		if (value == null) return null;

		if (value.kind == BpValueKind.Vector3)
			return new float[] { value.vector3.x, value.vector3.y, value.vector3.z };

		if (value.kind == BpValueKind.Generic && value.generic != null)
		{
			// 先找 "data" 包裹层并递归
			foreach (KeyValuePair<string, BpValue> kv in value.generic)
			{
				if (kv.Key == "data")
				{
					float[] nested = SceneValueAsVec3(kv.Value);
					if (nested != null) return nested;
				}
			}

			float? x = null, y = null, z = null;
			foreach (KeyValuePair<string, BpValue> kv in value.generic)
			{
				if (kv.Value == null) continue;
				if (kv.Value.kind == BpValueKind.Float || kv.Value.kind == BpValueKind.Integer)
				{
					float f = kv.Value.kind == BpValueKind.Float ? kv.Value.floatValue : kv.Value.intValue;
					if (kv.Key == "x") x = f;
					else if (kv.Key == "y") y = f;
					else if (kv.Key == "z") z = f;
				}
			}

			return new float[] { x ?? 0f, y ?? 0f, z ?? 0f };
		}

		return null;
	}

	// parse_bg_overrides —— 从 BackgroundObject 的 PrefabOverrideData 文本解析
	public static BpBgOverrides ParseBgOverrides(string raw)
	{
		BpBgOverrides result = new BpBgOverrides();
		string currentGroup = "";
		string currentSprite = "";
		string parsingFor = "";   // "group" 或 "sprite"

		if (raw == null) return result;

		foreach (string rawLine in raw.Split('\n'))
		{
			string stripped = rawLine.TrimEnd('\r');
			int depth = stripped.Length - stripped.TrimStart('\t').Length;
			string content = stripped.Trim();
			if (content.Length == 0) continue;

			if (depth == 1 && content.StartsWith("GameObject ", StringComparison.Ordinal))
			{
				currentGroup = content.Substring(11).Trim();
				currentSprite = "";
				parsingFor = "";
			}
			else if (depth == 2 && content.StartsWith("GameObject ", StringComparison.Ordinal))
			{
				currentSprite = content.Substring(11).Trim();
				parsingFor = "";
			}
			else if (depth == 2 && content == "Component UnityEngine.Transform")
			{
				parsingFor = "group";
			}
			else if (depth == 3 && content == "Component UnityEngine.Transform")
			{
				parsingFor = "sprite";
			}
			else if (content.StartsWith("Float ", StringComparison.Ordinal))
			{
				// 解析 "x = 1.23" / "y = -4.56"
				string rest = content.Substring("Float ".Length);
				int eq = rest.IndexOf('=');
				if (eq < 0) continue;
				string axis = rest.Substring(0, eq).Trim();
				float val;
				if (!float.TryParse(rest.Substring(eq + 1).Trim(), System.Globalization.NumberStyles.Float,
					System.Globalization.CultureInfo.InvariantCulture, out val)) continue;

				string targetName;
				Dictionary<string, float[]> targetMap;
				if (parsingFor == "group") { targetName = currentGroup; targetMap = result.groups; }
				else if (parsingFor == "sprite") { targetName = currentSprite; targetMap = result.sprites; }
				else continue;

				if (targetName.Length == 0) continue;

				float[] entry;
				if (!targetMap.TryGetValue(targetName, out entry))
				{
					entry = new float[] { float.NaN, float.NaN, float.NaN };
					targetMap[targetName] = entry;
				}
				if (axis == "x") entry[0] = val;
				else if (axis == "y") entry[1] = val;
				else if (axis == "z") entry[2] = val;
			}
		}
		return result;
	}

	// apply_bg_overrides —— 应用覆盖，返回更新位置后的精灵副本
	public static List<BpBgSprite> ApplyBgOverrides(BpBgTheme theme, BpBgOverrides overrides)
	{
		List<BpBgSprite> outList = new List<BpBgSprite>();
		if (theme == null) return outList;

		if (overrides.groups.Count == 0 && overrides.sprites.Count == 0)
		{
			foreach (BpBgSprite s in theme.sprites) outList.Add(s.Clone());
			return outList;
		}

		foreach (BpBgSprite s in theme.sprites)
		{
			float[] defaults;
			if (!theme.groupDefaults.TryGetValue(s.parentGroup, out defaults))
			{
				outList.Add(s.Clone());
				continue;
			}

			float[] groupOvr;
			bool hasGroupOvr = overrides.groups.TryGetValue(s.parentGroup, out groupOvr);
			float[] spriteOvr;
			bool hasSpriteOvr = overrides.sprites.TryGetValue(s.name, out spriteOvr);
			if (!hasGroupOvr && !hasSpriteOvr)
			{
				outList.Add(s.Clone());
				continue;
			}

			float gx = hasGroupOvr && !float.IsNaN(groupOvr[0]) ? groupOvr[0] : defaults[0];
			float gy = hasGroupOvr && !float.IsNaN(groupOvr[1]) ? groupOvr[1] : defaults[1];
			float gz = hasGroupOvr && !float.IsNaN(groupOvr[2]) ? groupOvr[2] : defaults[2];

			// 当精灵自身就是其组父节点(name == parentGroup)时，defaults 与 local 表示同一平移，
			// 用 localX/Y 会重复计入 → 视为 0。
			bool isGroupRoot = s.name == s.parentGroup;
			float lx = hasSpriteOvr && !float.IsNaN(spriteOvr[0]) ? spriteOvr[0] : (isGroupRoot ? 0f : s.localX);
			float ly = hasSpriteOvr && !float.IsNaN(spriteOvr[1]) ? spriteOvr[1] : (isGroupRoot ? 0f : s.localY);

			float newX = gx + lx;
			float newY = gy + ly;
			float spriteLocalZ = s.worldZ - defaults[2];
			float newZ = gz + spriteLocalZ;

			BpBgSprite outSprite = s.Clone();
			outSprite.worldX = newX;
			outSprite.worldY = newY;
			outSprite.worldZ = newZ;
			outList.Add(outSprite);
		}
		return outList;
	}

	// parse_position_serializer_overrides —— EP6 背景 prefab 的 PositionSerializer.childLocalPositions
	public static BpBgOverrides ParsePositionSerializerOverrides(string raw, List<string> childOrder)
	{
		BpBgOverrides result = new BpBgOverrides();
		if (childOrder == null || childOrder.Count == 0) return result;

		int root;
		BpScene scene = BpScene.FromOverrideText(raw, out root);
		if (scene == null) return result;

		for (int i = 0; i < scene.ComponentCount; i++)
		{
			BpUnityComponent behavior = scene.Behavior(i);
			if (behavior == null || behavior.ComponentSuffix != "PositionSerializer") continue;

			List<KeyValuePair<string, BpValue>> extra = behavior.Extra;
			if (extra == null) continue;

			BpValue arrayValue = null;
			foreach (KeyValuePair<string, BpValue> kv in extra)
			{
				if (kv.Key == "childLocalPositions" && kv.Value != null && kv.Value.kind == BpValueKind.Generic)
				{
					arrayValue = kv.Value;
					break;
				}
			}
			if (arrayValue == null) continue;

			foreach (KeyValuePair<string, BpValue> kv in arrayValue.generic)
			{
				int idx;
				if (!int.TryParse(kv.Key, out idx)) continue;
				if (idx < 0 || idx >= childOrder.Count || string.IsNullOrEmpty(childOrder[idx])) continue;

				float[] value = SceneValueAsVec3(kv.Value);
				if (value == null) continue;

				result.groups[childOrder[idx]] = new float[] { value[0], value[1], value[2] };
			}
		}

		return result;
	}
}
