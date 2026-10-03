using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static class HeatUiPanelRepair
{
    public static void Run()
    {
        BuildFeatureHelp();
        foreach (string template in new[] { "Settings Element (Slider)", "Settings Element (Horizontal Selector)" })
        {
            string path = "Assets/Resources/HeatUI/" + template + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                AssetDatabase.CopyAsset("Assets/Heat - Complete Modern UI/Prefabs/UI Elements/Settings/" + template + ".prefab", path);
            }
        }
        if (!AssetDatabase.IsValidFolder("Assets/Resources/HeatUI/Icons"))
        {
            AssetDatabase.CreateFolder("Assets/Resources/HeatUI", "Icons");
        }
        foreach (string icon in new[] { "Settings", "Information", "Visuals", "Audio", "Controls", "Extras", "Load", "Restart" })
        {
            string destination = "Assets/Resources/HeatUI/Icons/" + icon + ".png";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(destination) == null)
            {
                AssetDatabase.CopyAsset("Assets/Heat - Complete Modern UI/Textures/Icons/Misc/" + icon + " (64x).png", destination);
            }
        }
        GameObject close = PrefabUtility.LoadPrefabContents("Assets/Heat - Complete Modern UI/Prefabs/UI Elements/Button/Button (Radial Icon Only).prefab");
        try
        {
            var manager = close.GetComponent<Michsky.UI.Heat.ButtonManager>();
            manager.buttonIcon = AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>("Assets/Heat - Complete Modern UI/Textures/Icons/Navigation/Close (64x).png");
            manager.useLocalization = false;
            manager.useSounds = false;
            manager.autoFitContent = false;
            manager.onClick = new UnityEngine.Events.UnityEvent();
            manager.UpdateUI();
            RectTransform rect = (RectTransform)close.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-40, -25);
            rect.sizeDelta = new Vector2(50, 50);
            PrefabUtility.SaveAsPrefabAsset(close, "Assets/Resources/HeatUI/Close Button.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(close); }

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources/HeatUI" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var animator in prefab.GetComponentsInChildren<Animator>(true))
                {
                    animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                }
                foreach (var manager in prefab.GetComponentsInChildren<Michsky.UI.Heat.PanelManager>(true))
                {
                    UnityEngine.Object.DestroyImmediate(manager);
                }
                foreach (var manager in prefab.GetComponentsInChildren<Michsky.UI.Heat.SettingsDescriptionManager>(true))
                {
                    manager.useLocalization = false;
                    manager.enabled = false;
                }
                foreach (var description in prefab.GetComponentsInChildren<Michsky.UI.Heat.SettingsDescription>(true))
                {
                    UnityEngine.Object.DestroyImmediate(description);
                }
                foreach (var selector in prefab.GetComponentsInChildren<Michsky.UI.Heat.HorizontalSelector>(true))
                {
                    selector.useLocalization = false;
                    selector.saveSelected = false;
                    selector.invokeOnAwake = false;
                }
                foreach (var slider in prefab.GetComponentsInChildren<Michsky.UI.Heat.SliderManager>(true))
                {
                    slider.useSounds = false;
                    slider.saveValue = false;
                    slider.invokeOnAwake = false;
                }
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }
        AssetDatabase.SaveAssets();
    }

    private static void BuildFeatureHelp()
    {
        JObject aliases = JObject.Parse(File.ReadAllText("Assets/TextAsset/INAliasSettings.json"));
        JObject declarations = JObject.Parse(File.ReadAllText("Assets/TextAsset/INDeclarationSettingsExp.json"));
        StringBuilder output = new StringBuilder();
        foreach (JToken item in declarations["items"])
        {
            string key = (string)item["name"];
            string chinese = (string)aliases[key] ?? key;
            string english = Regex.Replace(key, "(?<=[a-z])([A-Z])", " $1");
            string type = (string)item["type"]?["mainType"];
            string zh;
            string en;
            if (type == "Boolean")
            {
                zh = "切换“" + chinese + "”功能。开启后使用对应的扩展行为，关闭后停用此功能。";
                en = "Toggle " + english + ". Enable its extended behavior or turn this feature off.";
            }
            else if (type == "Array")
            {
                zh = "设置“" + chinese + "”的数值列表，使用逗号分隔各项并按原顺序填写。";
                en = "Set the values for " + english + ". Enter a comma-separated list in the existing order.";
            }
            else if (type == "String")
            {
                zh = "编辑“" + chinese + "”使用的文本参数，提交输入后应用。";
                en = "Edit the text parameter used by " + english + ". Submit the field to apply it.";
            }
            else
            {
                zh = "调整“" + chinese + "”的" + (type == "Int32" ? "整数" : "数值") + "参数。提交输入后应用，重置可恢复当前游戏版本的默认值。";
                en = "Adjust the " + (type == "Int32" ? "integer" : "numeric") + " parameter for " + english + ". Submit to apply; reset restores the current game version's default.";
            }
            output.AppendLine(key + "\t" + zh + "\t" + en);
        }
        File.WriteAllText("Assets/Resources/HeatUI/Feature Help.txt", output.ToString(), new UTF8Encoding(false));
        AssetDatabase.ImportAsset("Assets/Resources/HeatUI/Feature Help.txt");
    }
}
