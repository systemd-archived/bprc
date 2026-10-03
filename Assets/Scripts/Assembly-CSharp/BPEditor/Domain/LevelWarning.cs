using System.Collections.Generic;

// 1:1 移植 badpiggies-editor 的 domain/level_warning.rs（关卡风险检查）
// 说明：原实现用 unity_runtime::Scene 读 LevelManager.sandbox；此处以等价的
// override 文本扫描（m_sandbox = True/False）实现，其余逻辑逐条一致。
public enum BpLevelWarningSeverity { High, Low }

public enum BpLevelWarningKind
{
	MultipleLevelManagers,
	MissingLevelManager,
	MultipleLevelStarts,
	MissingLevelStart,
	MultipleCameraSystems,
	MissingCameraSystem,
	MultipleGameCameras,
	MultipleHudCameras,
	MultipleWorldObjects,
	MissingWorldObject,
	MissingGoalArea,
	MultipleGoalAreas,
	MultipleDessertPlaces,
	MissingDessertPlaces,
	MultipleEffectManagers,
	MultipleIngameCameras
}

public struct BpLevelWarning
{
	public BpLevelWarningKind kind;
	public string objectName;
	public int count;

	public string MessageKey()
	{
		switch (kind)
		{
			case BpLevelWarningKind.MissingLevelManager: return "level_warning_missing_level_manager";
			case BpLevelWarningKind.MissingLevelStart: return "level_warning_missing_level_start";
			case BpLevelWarningKind.MultipleCameraSystems: return "level_warning_multiple_camera_system";
			case BpLevelWarningKind.MissingCameraSystem: return "level_warning_missing_camera_system";
			case BpLevelWarningKind.MultipleGameCameras: return "level_warning_multiple_game_camera";
			case BpLevelWarningKind.MultipleHudCameras: return "level_warning_multiple_hud_camera";
			case BpLevelWarningKind.MultipleWorldObjects: return "level_warning_multiple_world_object";
			case BpLevelWarningKind.MissingWorldObject: return "level_warning_missing_world_object";
			case BpLevelWarningKind.MissingGoalArea: return "level_warning_missing_goal_area";
			case BpLevelWarningKind.MultipleGoalAreas: return "level_warning_multiple_goal_area";
			case BpLevelWarningKind.MultipleDessertPlaces: return "level_warning_multiple_dessert_places";
			case BpLevelWarningKind.MissingDessertPlaces: return "level_warning_missing_dessert_places";
			default: return "level_warning_multiple_singleton";
		}
	}

	public BpLevelWarningSeverity Severity()
	{
		switch (kind)
		{
			case BpLevelWarningKind.MultipleDessertPlaces:
			case BpLevelWarningKind.MissingDessertPlaces:
				return BpLevelWarningSeverity.Low;
			default:
				return BpLevelWarningSeverity.High;
		}
	}
}

public static class BpLevelWarnings
{
	private static readonly string[] WorldTaggedBackgroundObjects =
	{
		"Background_Jungle_01_SET",
		"Background_Plateau_01_SET",
		"Background_Cave_01_SET 1",
		"Background_Night_01_SET 1",
		"Background_Forest_01_SET 1",
		"Background_Halloween",
		"Background_MM_01_SET",
		"Background_MM_Temple_01_SET_01",
		"Background_MM_Cave_01_SET",
		"Background_MM_Cave_01_SET_DARK",
		"Background_MM_Cave_02_SET_DARK",
		"Background_MM_High_01_SET",
		"BackgroundObject"
	};

	private struct KnownRiskObject
	{
		public string name;
		public BpLevelWarningKind kind;
		public KnownRiskObject(string n, BpLevelWarningKind k) { name = n; kind = k; }
	}

	private static readonly KnownRiskObject[] KnownRiskObjects =
	{
		new KnownRiskObject("LevelManager", BpLevelWarningKind.MultipleLevelManagers),
		new KnownRiskObject("EffectManager", BpLevelWarningKind.MultipleEffectManagers),
		new KnownRiskObject("IngameCamera", BpLevelWarningKind.MultipleIngameCameras)
	};

	private static bool OverrideHasGoalTag(string rawText)
	{
		if (rawText == null) return false;
		foreach (string raw in rawText.Split('\n'))
		{
			string t = raw.Trim();
			if (t == "String m_TagString = Goal" || t == "m_TagString = Goal" || t.EndsWith("m_TagString = Goal")) return true;
		}
		return false;
	}

	private static bool IsGoalObject(BpLevelObject obj)
	{
		string name = obj.Name();
		if (name.StartsWith("GoalArea") || name == "Goal") return true;
		BpPrefabInstance p = obj.AsPrefab();
		if (p == null || p.overrideData == null) return false;
		return OverrideHasGoalTag(p.overrideData.rawText);
	}

	private static bool? LevelSandboxFlag(BpLevelData level)
	{
		bool sawTrue = false, sawFalse = false;
		foreach (BpLevelObject obj in level.objects)
		{
			BpPrefabInstance p = obj.AsPrefab();
			if (p == null || p.name != "LevelManager" || p.overrideData == null) continue;
			string raw = p.overrideData.rawText;
			if (raw == null) continue;
			foreach (string line in raw.Split('\n'))
			{
				string t = line.Trim();
				if (t.EndsWith("m_sandbox = True")) sawTrue = true;
				else if (t.EndsWith("m_sandbox = False")) sawFalse = true;
			}
		}
		if (sawTrue && !sawFalse) return true;
		if (!sawTrue && sawFalse) return false;
		return null;
	}

	public static List<BpLevelWarning> CollectLevelWarnings(BpLevelData level)
	{
		Dictionary<string, int> counts = new Dictionary<string, int>();
		foreach (BpLevelObject obj in level.objects)
		{
			string n = obj.Name();
			int c;
			counts[n] = counts.TryGetValue(n, out c) ? c + 1 : 1;
		}

		int Count(string key)
		{
			int c;
			return counts.TryGetValue(key, out c) ? c : 0;
		}

		int levelManagerCount = Count("LevelManager");
		int levelStartCount = Count("LevelStart");
		int cameraSystemCount = Count("CameraSystem");
		int gameCameraCount = Count("GameCamera");
		int hudCameraCount = Count("HUDCamera");

		HashSet<string> worldNames = new HashSet<string>();
		foreach (BpLevelObject obj in level.objects)
		{
			string n = obj.Name();
			foreach (string w in WorldTaggedBackgroundObjects)
				if (w == n) { worldNames.Add(n); break; }
		}
		int worldObjectCount = worldNames.Count;

		int goalAreaCount = 0;
		foreach (BpLevelObject obj in level.objects) if (IsGoalObject(obj)) goalAreaCount++;

		int dessertPlacesCount = Count("DessertPlaces");
		bool? sandboxFlag = LevelSandboxFlag(level);

		List<BpLevelWarning> warnings = new List<BpLevelWarning>();

		if (levelManagerCount == 0) warnings.Add(new BpLevelWarning { kind = BpLevelWarningKind.MissingLevelManager, objectName = "LevelManager", count = 0 });
		else if (levelManagerCount > 1) warnings.Add(new BpLevelWarning { kind = BpLevelWarningKind.MultipleLevelManagers, objectName = "LevelManager", count = levelManagerCount });

		if (levelStartCount == 0) warnings.Add(new BpLevelWarning { kind = BpLevelWarningKind.MissingLevelStart, objectName = "LevelStart", count = 0 });
		else if (levelStartCount > 1) warnings.Add(new BpLevelWarning { kind = BpLevelWarningKind.MultipleLevelStarts, objectName = "LevelStart", count = levelStartCount });

		if (cameraSystemCount == 0) warnings.Add(new BpLevelWarning { kind = BpLevelWarningKind.MissingCameraSystem, objectName = "CameraSystem", count = 0 });
		else if (cameraSystemCount > 1) warnings.Add(new BpLevelWarning { kind = BpLevelWarningKind.MultipleCameraSystems, objectName = "CameraSystem", count = cameraSystemCount });

		if (cameraSystemCount == 1)
		{
			if (gameCameraCount > 1) warnings.Add(new BpLevelWarning { kind = BpLevelWarningKind.MultipleGameCameras, objectName = "GameCamera", count = gameCameraCount });
			if (hudCameraCount > 1) warnings.Add(new BpLevelWarning { kind = BpLevelWarningKind.MultipleHudCameras, objectName = "HUDCamera", count = hudCameraCount });
		}

		if (worldObjectCount == 0) warnings.Add(new BpLevelWarning { kind = BpLevelWarningKind.MissingWorldObject, objectName = "World-tagged background", count = 0 });
		else if (worldObjectCount > 1) warnings.Add(new BpLevelWarning { kind = BpLevelWarningKind.MultipleWorldObjects, objectName = "World-tagged background", count = worldObjectCount });

		if (goalAreaCount == 0 && sandboxFlag == false) warnings.Add(new BpLevelWarning { kind = BpLevelWarningKind.MissingGoalArea, objectName = "GoalArea*", count = 0 });
		if (goalAreaCount > 1) warnings.Add(new BpLevelWarning { kind = BpLevelWarningKind.MultipleGoalAreas, objectName = "GoalArea*", count = goalAreaCount });

		foreach (KnownRiskObject rule in KnownRiskObjects)
		{
			int c = Count(rule.name);
			if (c > 1 && rule.name != "LevelManager")
				warnings.Add(new BpLevelWarning { kind = rule.kind, objectName = rule.name, count = c });
		}

		if (dessertPlacesCount == 0) warnings.Add(new BpLevelWarning { kind = BpLevelWarningKind.MissingDessertPlaces, objectName = "DessertPlaces", count = 0 });
		else if (dessertPlacesCount > 1) warnings.Add(new BpLevelWarning { kind = BpLevelWarningKind.MultipleDessertPlaces, objectName = "DessertPlaces", count = dessertPlacesCount });

		return warnings;
	}
}
