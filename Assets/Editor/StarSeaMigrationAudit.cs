using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

public static class StarSeaMigrationAudit
{
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void Run()
    {
        Innovation.Json.Service = (Innovation.IJsonService)Activator.CreateInstance(typeof(INSettings).Assembly.GetType("JsonService"));
        var resources = (Dictionary<(string, string), UnityEngine.Object>)typeof(INUnity)
            .GetField("s_resources", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
        foreach (string guid in AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets/TextAsset" }))
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null) resources[("TextAsset", asset.name)] = asset;
        }
        // Load declarations only: do not touch the user's saved settings during an editor audit.
        typeof(INSettings).GetMethod("Load", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, new object[] { 3 });
        var checks = new List<string>();
        foreach (string name in new[] { "StarSeaSettingsInterface", "PartAdjustInterface", "CustomBackgroundInterface", "INApplicationInterface" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/" + name + ".prefab");
            Require(prefab != null, "Missing prefab: " + name);
            foreach (Transform node in prefab.GetComponentsInChildren<Transform>(true))
                Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject) == 0, name + ": missing script on " + node.name);
            checks.Add("PASS prefab scripts " + name);
        }
        foreach (string name in new[] { "PartAdjustInterface", "CustomBackgroundInterface" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/" + name + ".prefab");
            Component component = name == "PartAdjustInterface" ? (Component)prefab.GetComponent<INPartAdjustInterface>() : prefab.GetComponent<INCustomBackgroundInterface>();
            Require(component != null, "Missing controller " + name);
            var serialized = new SerializedObject(component);
            foreach (string field in new[] { "m_content", "m_itemTemplate", "m_groupTemplate", "m_saveButton", "m_resetButton" })
                Require(serialized.FindProperty(field).objectReferenceValue != null, name + ": missing " + field);
            checks.Add("PASS UI bindings " + name);
        }
        var settings = new StarSeaSettings { PartDrag = 0.7f, SpecialChargeForce = 123f, GateOpacity = 0.6f };
        var restored = JsonConvert.DeserializeObject<StarSeaSettings>(JsonConvert.SerializeObject(settings));
        Require(Mathf.Abs(restored.PartDrag - 0.7f) < 0.001f && restored.SpecialChargeForce == 123f && Mathf.Abs(restored.GateOpacity - 0.6f) < 0.001f, "Settings round trip");
        var slots = new ContraptionDataSettings { SlotCount = 4096 };
        Require(slots.SlotCount == 4096, "4096 slots");
        checks.Add("PASS settings round trip and 4096 save slots");
        for (int i = 0; i < 4096; i++)
            Require(UIEvent.GetSlotIndex(UIEvent.GetSlotEvent(i)) == i, "Save slot event round trip: " + i);
        foreach (UIEvent.Type command in new[] { UIEvent.Type.SaveContraption, UIEvent.Type.ToolBookCopy, UIEvent.Type.ToolBookPaste, UIEvent.Type.ToolBookDeleteClipboard })
            Require(UIEvent.GetSlotIndex(command) == -1, "Menu command aliases a save slot: " + command);
        checks.Add("PASS 4096 slot events do not overlap tool menu commands");
        INMathExpressionEvaluator.SetAngleMode(true);
        var curves = INMathExpressionEvaluator.ParseAndGenerateCurves("y=2x+1", 0, 0, 5, 1, 100);
        Require(curves.Count > 0 && curves.SelectMany(c => c).Any(), "Function terrain empty");
        Require(curves.SelectMany(c => c).All(p => Mathf.Abs(p.y - (2*p.x+1)) < 0.001f), "Implicit multiplication");
        curves = INMathExpressionEvaluator.ParseAndGenerateCurves("sin(x);cos(x)", 0, 0, 90, 1, 100);
        Require(curves.Count >= 2, "Multiple terrain expressions");
        checks.Add("PASS function terrain and multiple expressions");
        Require(BasePart.PartType.RCR2Cargo.ToSortedPartType().ToPartType() == BasePart.PartType.RCR2Cargo, "RCR2 type lost");
        Require(BasePart.PartType.RC3Missile.ToSortedPartType().ToPartType() == BasePart.PartType.RC3Missile, "RC3 type lost");
        RoverPortAudit.Run();
        checks.Add("PASS RCR2 150-part regression audit and RC3 type mapping");
        Directory.CreateDirectory("Builds/QA");
        File.WriteAllLines("Builds/QA/star-sea-audit.txt", checks);
        Debug.Log("STAR_SEA_AUDIT_PASS: " + checks.Count + " checks");
    }
}
