using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class HeatUiLayoutBuilder
{
    /// <summary>
    /// Historical: rewrites com.unity.ugui / TextMeshPro package-source references into the
    /// precompiled DLL identities. The uGUI half is obsolete since 2026-09-28 — uGUI is provided by
    /// the com.unity.ugui package and the project's references are in source form again, so with the
    /// DLL gone this loop is a no-op. The TextMeshPro half is still valid.
    /// </summary>
    public static void RepairReferences()
    {
        var replacements = new Dictionary<string, string>();
        foreach (var entry in new[] {
            new[] { EditorApplication.applicationContentsPath + "/Resources/PackageManager/BuiltInPackages/com.unity.ugui", "Assets/Plugins/UnityEngine.UI.dll" },
            new[] { "Tools/HeatUI/TmpPackage", "Assets/Plugins/Unity.TextMeshPro.dll" } })
        {
            var scripts = AssetDatabase.LoadAllAssetsAtPath(entry[1]).OfType<MonoScript>().ToArray();
            foreach (var meta in Directory.GetFiles(entry[0], "*.cs.meta", SearchOption.AllDirectories))
            {
                var name = Path.GetFileName(meta).Replace(".cs.meta", "");
                var script = scripts.FirstOrDefault(candidate => candidate.name == name);
                if (script == null) continue;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out string guid, out long fileId);
                var sourceGuid = Regex.Match(File.ReadAllText(meta), @"guid: (\w+)").Groups[1].Value;
                replacements["fileID: 11500000, guid: " + sourceGuid + ", type: 3"] = "fileID: " + fileId + ", guid: " + guid + ", type: 3";
            }
        }
        int changed = 0;
        foreach (var folder in new[] { "Assets/Heat - Complete Modern UI", "Assets/Resources/HeatUI" })
        foreach (var path in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
        {
            if (!new[] { ".prefab", ".unity", ".asset" }.Contains(Path.GetExtension(path))) continue;
            var original = File.ReadAllText(path);
            var result = original;
            foreach (var replacement in replacements) result = result.Replace(replacement.Key, replacement.Value);
            if (result == original) continue;
            File.WriteAllText(path, result, new UTF8Encoding(false));
            changed++;
        }
        Debug.Log("HEAT reference repair: " + replacements.Count + " types, " + changed + " assets.");
        AssetDatabase.Refresh();
        Inspect();
    }

    public static void Inspect()
    {
        var report = new StringBuilder();
        var scene = EditorSceneManager.OpenScene("Assets/Heat - Complete Modern UI/Scenes/In-Game (Desktop).unity", OpenSceneMode.Single);
        foreach (var root in scene.GetRootGameObjects())
            Describe(root.transform, report, 0);
        foreach (var path in new[] { "INApplicationInterface", "INSettingsInterface", "INAddonInterface", "CustomBackgroundInterface", "INDataPanel" })
        {
            report.AppendLine("PREFAB " + path);
            Describe(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/" + path + ".prefab").transform, report, 0);
        }
        Directory.CreateDirectory("Builds/QA");
        File.WriteAllText("Builds/QA/heat-layout-source.txt", report.ToString());
    }

    private static void Describe(Transform target, StringBuilder report, int depth)
    {
        report.Append(new string(' ', depth * 2)).Append(target.name);
        foreach (var component in target.GetComponents<Component>())
            if (component != null && !(component is Transform)) report.Append(" [").Append(component.GetType().Name).Append(']');
        var rect = target as RectTransform;
        if (rect != null) report.Append(" rect=").Append(rect.sizeDelta).Append(" pos=").Append(rect.anchoredPosition);
        report.AppendLine();
        foreach (Transform child in target) Describe(child, report, depth + 1);
    }
}
