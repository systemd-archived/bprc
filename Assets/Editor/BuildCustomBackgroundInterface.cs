using System;
using UnityEditor;
using UnityEngine;
public static class BuildCustomBackgroundInterface
{
    [MenuItem("Tools/Build CustomBackgroundInterface")]
    public static void Build()
    {
        const string path = "Assets/GameObject/CustomBackgroundInterface.prefab";
        var template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/PartAdjustInterface.prefab");
        if (template == null) throw new InvalidOperationException("PartAdjustInterface template is missing.");
        var root = UnityEngine.Object.Instantiate(template);
        root.SetActive(false);
        try
        {
            root.name = "CustomBackgroundInterface";
            var previous = root.GetComponent<INPartAdjustInterface>();
            var source = new SerializedObject(previous);
            var controller = root.AddComponent<INCustomBackgroundInterface>();
            var destination = new SerializedObject(controller);
            foreach (string field in new[] { "m_content", "m_itemTemplate", "m_groupTemplate", "m_saveButton", "m_resetButton" })
                destination.FindProperty(field).objectReferenceValue = source.FindProperty(field).objectReferenceValue;
            destination.ApplyModifiedPropertiesWithoutUndo();
            UnityEngine.Object.DestroyImmediate(previous);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            AssetDatabase.SaveAssets();
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
