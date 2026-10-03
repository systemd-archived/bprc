using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Verifies the 工程文件901-12 port: the assets it added, the settings rows it declares and the
/// locale ids every one of those needs. Also re-checks that the port introduced no missing script.
/// </summary>
/// <remarks>
/// Run with:
///   Tools\unity-batch.bat -nographics -executeMethod StarSea12Audit.Run
/// It writes Builds/QA/star-sea12-audit.txt and logs STAR_SEA12_AUDIT_PASS.
/// </remarks>
public static class StarSea12Audit
{
    private const string ReportPath = "Builds/QA/star-sea12-audit.txt";

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void Run()
    {
        Prepare();
        var checks = new List<string>();

        CheckPrefabs(checks);
        CheckSidebarWiring(checks);
        var loc = LoadLocalization(checks);
        CheckSettingsRows(loc, checks);
        CheckToolBookLabels(loc, checks);
        CheckNewApi(checks);

        Directory.CreateDirectory("Builds/QA");
        File.WriteAllLines(ReportPath, checks);
        Debug.Log("STAR_SEA12_AUDIT_PASS: " + checks.Count + " checks");
    }

    /// <summary>
    /// Makes INSettings readable without the game running: the declaration assets are registered in
    /// INUnity's resource table and the version's declarations are loaded. Same setup as
    /// <see cref="StarSeaMigrationAudit"/>, and needed before anything constructs a StarSeaSettings -
    /// its constructor reads INFeature variants.
    /// </summary>
    private static void Prepare()
    {
        Innovation.Json.Service = (Innovation.IJsonService)Activator.CreateInstance(
            typeof(INSettings).Assembly.GetType("JsonService"));
        var resources = (Dictionary<(string, string), UnityEngine.Object>)typeof(INUnity)
            .GetField("s_resources", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        foreach (string guid in AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets/TextAsset" }))
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null) resources[("TextAsset", asset.name)] = asset;
        }
        typeof(INSettings).GetMethod("Load", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { 3 });
    }

    // ---- A. the ported prefabs load and hold no missing script -----------------
    private static void CheckPrefabs(List<string> checks)
    {
        foreach (string name in new[]
                 {
                     "ClassBrowserInterface", "CodeEditorInterface", "INApplicationInterface",
                     "InGameBuildMenu", "StarSeaSettingsInterface", "PartAdjustInterface",
                     "CustomBackgroundInterface", "PartParameterPanel"
                 })
        {
            string path = "Assets/GameObject/" + name + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;   // PartParameterPanel is built in code, not a prefab
            foreach (Transform node in prefab.GetComponentsInChildren<Transform>(true))
            {
                Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject) == 0,
                    name + ": missing script on " + node.name);
            }
            checks.Add("PASS prefab scripts " + name);
        }
    }

    // ---- B. the two new sidebar entries point at the two new prefabs -----------
    private static void CheckSidebarWiring(List<string> checks)
    {
        const string sidebar = "Assets/GameObject/INApplicationInterface.prefab";
        Require(File.Exists(sidebar), "Missing " + sidebar);
        string text = File.ReadAllText(sidebar);
        foreach (string name in new[] { "ClassBrowserInterface", "CodeEditorInterface" })
        {
            string prefabPath = "Assets/GameObject/" + name + ".prefab";
            Require(File.Exists(prefabPath), "Missing prefab " + prefabPath);
            string guid = AssetDatabase.AssetPathToGUID(prefabPath);
            Require(!string.IsNullOrEmpty(guid), "No GUID for " + prefabPath);
            Require(text.Contains(guid), sidebar + " does not reference " + name + " (" + guid + ")");
            checks.Add("PASS sidebar entry for " + name);
        }

        // The whole project's missing-script count is the canary for a bad prefab merge; the two
        // documented survivors are CoinSalePopup and CrateCrazePopup.
        var withMissing = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith("Packages/")) continue;
            string body = File.ReadAllText(path);
            if (body.Contains("m_Script: {fileID: 0}")) withMissing.Add(Path.GetFileNameWithoutExtension(path));
        }
        withMissing.Sort();
        Require(withMissing.SequenceEqual(new[] { "CoinSalePopup", "CrateCrazePopup" }),
            "Unexpected prefabs with missing scripts: " + string.Join(", ", withMissing));
        checks.Add("PASS only the 2 pre-existing prefabs have missing scripts");
    }

    // ---- C. every StarSea settings row resolves to a property and a locale id --
    private static Dictionary<string, string> LoadLocalization(List<string> checks)
    {
        var asset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/TextAsset/INLocalizationData.json");
        Require(asset != null, "Missing INLocalizationData.json");
        var root = JObject.Parse(asset.text);
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var duplicates = new List<string>();
        foreach (var entry in root["data"])
        {
            string id = (string)entry["id"];
            if (map.ContainsKey(id)) duplicates.Add(id);
            else map[id] = (string)entry["text"]["zh-Hans"];
        }
        Require(duplicates.Count == 0, "Duplicate localization ids: " + string.Join(", ", duplicates));
        checks.Add("PASS localization parses, " + map.Count + " ids, no duplicates");
        return map;
    }

    private static void CheckSettingsRows(Dictionary<string, string> loc, List<string> checks)
    {
        const string source = "Assets/Scripts/Assembly-CSharp/INSettingsInterface.cs";
        Require(File.Exists(source), "Missing " + source);
        var type = typeof(StarSeaSettings);
        var row = new Regex(@"GenerateStarSeaGroup\(starSea,\s*""([^""]+)"",\s*""([^""]+)""((?:\s*,\s*""[^""]*"")*)\)");
        var propertyName = new Regex(@"""([^""]+)""");
        int groups = 0, props = 0;
        var missingLoc = new List<string>();
        foreach (Match m in row.Matches(File.ReadAllText(source)))
        {
            groups++;
            string groupId = m.Groups[1].Value;
            if (!loc.ContainsKey(groupId)) missingLoc.Add(groupId);
            foreach (Match p in propertyName.Matches(m.Groups[3].Value))
            {
                string name = p.Groups[1].Value;
                props++;
                Require(type.GetProperty(name) != null,
                    "StarSeaSettings has no property '" + name + "' (row " + groupId + ")");
                string id = type.Name + "_" + name;
                if (!loc.ContainsKey(id)) missingLoc.Add(id);
            }
        }
        Require(groups > 0, "No StarSea settings groups found in INSettingsInterface.cs");
        Require(missingLoc.Count == 0, "Settings rows without a locale id: " + string.Join(", ", missingLoc));
        checks.Add("PASS " + props + " StarSea settings rows over " + groups + " groups resolve, all localized");
    }

    // ---- D. the tool menu's label arrays are all localized ---------------------
    private static void CheckToolBookLabels(Dictionary<string, string> loc, List<string> checks)
    {
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        foreach (string field in new[] { "s_toolIds", "s_shapeIds", "s_runIds" })
        {
            var info = typeof(ToolBookButton).GetField(field, flags);
            Require(info != null, "ToolBookButton." + field + " not found");
            var ids = (string[])info.GetValue(null);
            Require(ids != null && ids.Length > 0, "ToolBookButton." + field + " is empty");
            foreach (string id in ids)
            {
                Require(loc.ContainsKey(id), "No localization for " + field + " entry '" + id + "'");
            }
            checks.Add("PASS " + field + " localized (" + ids.Length + " entries)");
        }
    }

    // ---- E. the API the ported code calls is actually there -------------------
    private static void CheckNewApi(List<string> checks)
    {
        var starSea = typeof(StarSeaSettings);
        foreach (string member in new[]
                 {
                     "GetAutoEnterVersionBFromPrefs", "UniversalGravity", "UniversalGravityConstant",
                     "UniversalGravityAttenuation", "OptimizedCircuit", "AutoEnterVersionB",
                     "AdvancedDisplayGraphical", "QuickPartSwitch", "QuickPartSwitchMax",
                     "QuickPartSwitchPersistent", "QuickInheritParams", "ShowModifiedParams",
                     "EnableParamPanel", "GameRunCollapse", "GameRunQuickSpeed", "NewTimeScale",
                     "BlasterTNTDestroy", "CollisionBounciness", "DiagonalSenderRange"
                 })
        {
            bool present = starSea.GetProperty(member, BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance) != null
                           || starSea.GetMethod(member, BindingFlags.Public | BindingFlags.Static) != null;
            Require(present, "StarSeaSettings is missing " + member);
        }

        // A settings round trip has to carry the 901-12 fields as well.
        var settings = new StarSeaSettings
        {
            UniversalGravity = true, UniversalGravityConstant = 1.25f, OptimizedCircuit = true,
            AutoEnterVersionB = true, QuickPartSwitchMax = 7, ShowModifiedParams = false,
            CollisionBounciness = 0.4f, DiagonalSenderRange = 48f
        };
        var restored = Newtonsoft.Json.JsonConvert.DeserializeObject<StarSeaSettings>(
            Newtonsoft.Json.JsonConvert.SerializeObject(settings));
        Require(restored.UniversalGravity && Mathf.Abs(restored.UniversalGravityConstant - 1.25f) < 0.001f
                && restored.OptimizedCircuit && restored.AutoEnterVersionB
                && restored.QuickPartSwitchMax == 7 && !restored.ShowModifiedParams
                && Mathf.Abs(restored.CollisionBounciness - 0.4f) < 0.001f
                && Mathf.Abs(restored.DiagonalSenderRange - 48f) < 0.001f,
            "901-12 settings round trip");

        for (int i = 0; i < 4096; i++)
        {
            Require(UIEvent.GetSlotIndex(UIEvent.GetSlotEvent(i)) == i, "Save slot event round trip: " + i);
        }

        foreach (string typeName in new[]
                 {
                     "INParamSystem", "INPartParameterPanel", "INQuickInherit", "INModifiedPartMarker",
                     "UniversalGravityManager", "CollisionBouncinessManager", "OptimizedCircuitSimulator",
                     "INBuildQuickOptions", "INCodeEditorPage", "INClassBrowserPage", "INCodeSetStore",
                     "INUiUtil", "INScrollArea"
                 })
        {
            Require(AppDomain.CurrentDomain.GetAssemblies()
                    .Any(a => a.GetType(typeName, false) != null), "Type missing: " + typeName);
        }
        checks.Add("PASS 901-12 settings round trip, 4096 slots and 13 ported types");
    }
}
