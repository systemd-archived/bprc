using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HeatUiLayoutExporter
{
    public static string ExportOnlineHome()
    {
        Scene previous = SceneManager.GetActiveScene();
        Scene demo = EditorSceneManager.OpenScene(DemoScene, OpenSceneMode.Additive);
        GameObject copy = null;
        try
        {
            Transform home = null;
            foreach (GameObject root in demo.GetRootGameObjects())
            {
                home = FindChild(root.transform, "Home Panel");
                if (home != null) break;
            }
            if (home == null) throw new InvalidOperationException("Missing Heat desktop Home Panel");
            copy = UnityEngine.Object.Instantiate(home.gameObject);
            copy.name = "Online Home Layout";
            copy.SetActive(false);
            Animator rootAnimation = copy.GetComponent<Animator>();
            if (rootAnimation != null) UnityEngine.Object.DestroyImmediate(rootAnimation);
            foreach (Michsky.UI.Heat.LocalizedObject locale in copy.GetComponentsInChildren<Michsky.UI.Heat.LocalizedObject>(true))
                UnityEngine.Object.DestroyImmediate(locale);
            var report = new System.Text.StringBuilder();
            foreach (Transform t in copy.GetComponentsInChildren<Transform>(true))
            {
                report.AppendLine(t.name + " | " + string.Join(",", Array.ConvertAll(t.GetComponents<Component>(), c => c == null ? "missing" : c.GetType().Name)));
                var box = t.GetComponent<Michsky.UI.Heat.BoxButtonManager>();
                if (box != null)
                {
                    box.useLocalization = false;
                    box.onClick = new UnityEngine.Events.UnityEvent();
                    report.AppendLine("BOX: " + box.buttonTitle + " / " + (box.buttonBackground == null ? "none" : AssetDatabase.GetAssetPath(box.buttonBackground)));
                }
            }
            PrefabUtility.SaveAsPrefabAsset(copy, "Assets/Resources/HeatUI/Online Home Layout.prefab");
            GameObject backdrop = new GameObject("Online Home Backdrop", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            try
            {
                var image = backdrop.GetComponent<UnityEngine.UI.Image>();
                image.sprite = AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>("Assets/Heat - Complete Modern UI/Textures/Placeholders/Background/Placeholder Image 2.png");
                image.raycastTarget = false;
                PrefabUtility.SaveAsPrefabAsset(backdrop, "Assets/Resources/HeatUI/Online Home Backdrop.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(backdrop); }
            System.IO.File.WriteAllText("_codex_work/heat-home-layout.txt", report.ToString());
            return report.ToString();
        }
        finally
        {
            if (copy != null) UnityEngine.Object.DestroyImmediate(copy);
            EditorSceneManager.CloseScene(demo, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }
    private const string DemoScene = "Assets/Heat - Complete Modern UI/Scenes/Main Menu (Desktop).unity";
    private const string LayoutPrefab = "Assets/Resources/HeatUI/Settings Layout.prefab";
    private const string CategoryButton = "Assets/Heat - Complete Modern UI/Prefabs/UI Elements/Button/Button (Panel Alt).prefab";
    private const string StagedCategoryButton = "Assets/Resources/HeatUI/Button (Panel Alt).prefab";

    [MenuItem("Tools/Heat UI/Export desktop settings layout")]
    public static void Export()
    {
        Scene previousScene = SceneManager.GetActiveScene();
        Scene demoScene = EditorSceneManager.OpenScene(DemoScene, OpenSceneMode.Additive);
        try
        {
            Transform settings = null;
            foreach (GameObject root in demoScene.GetRootGameObjects())
            {
                settings = FindChild(root.transform, "Settings");
                if (settings != null)
                {
                    break;
                }
            }

            if (settings == null)
            {
                throw new InvalidOperationException("Heat UI desktop demo has no Settings panel.");
            }

            bool wasActive = settings.gameObject.activeSelf;
            settings.gameObject.SetActive(false);
            bool success;
            try
            {
                GameObject copy = UnityEngine.Object.Instantiate(settings.gameObject);
                try
                {
                    copy.name = "Settings Layout";
                    foreach (Michsky.UI.Heat.LocalizedObject localized in
                        copy.GetComponentsInChildren<Michsky.UI.Heat.LocalizedObject>(true))
                    {
                        UnityEngine.Object.DestroyImmediate(localized);
                    }
                    PrefabUtility.SaveAsPrefabAsset(copy, LayoutPrefab, out success);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(copy);
                }
            }
            finally
            {
                settings.gameObject.SetActive(wasActive);
            }
            if (!success)
            {
                throw new InvalidOperationException("Could not export the Heat UI Settings panel.");
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(StagedCategoryButton) == null &&
                !AssetDatabase.CopyAsset(CategoryButton, StagedCategoryButton))
            {
                throw new InvalidOperationException("Could not stage the Heat UI category button.");
            }
            GameObject category = PrefabUtility.LoadPrefabContents(StagedCategoryButton);
            try
            {
                Michsky.UI.Heat.PanelButton button = category.GetComponent<Michsky.UI.Heat.PanelButton>();
                if (button != null)
                {
                    button.useLocalization = false;
                }
                foreach (Michsky.UI.Heat.LocalizedObject localized in
                    category.GetComponentsInChildren<Michsky.UI.Heat.LocalizedObject>(true))
                {
                    UnityEngine.Object.DestroyImmediate(localized);
                }
                PrefabUtility.SaveAsPrefabAsset(category, StagedCategoryButton);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(category);
            }
        }
        finally
        {
            EditorSceneManager.CloseScene(demoScene, true);
            if (previousScene.IsValid() && previousScene.isLoaded)
            {
                SceneManager.SetActiveScene(previousScene);
            }
        }
    }

    private static Transform FindChild(Transform parent, string name)
    {
        if (parent.name == name)
        {
            return parent;
        }

        foreach (Transform child in parent)
        {
            Transform match = FindChild(child, name);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }
}
