using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public static class HeatUiFontRepair
{
    private const string FontPath = "Assets/Resources/HeatUI/Chinese Fallback.asset";
    private const string Sample = "沙盒游戏 设置 数据 命令 插件 背景 返回 保存 取消 开启 关闭 音量 分辨率 自定义 零件 调整 中文输入，。！？：；（）【】0123456789 ABC xyz";

    public static void Run()
    {
        FontEngine.InitializeFontEngine();
        Font source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Font/SourceHanSansCN-Regular.otf");
        if (source == null) { throw new InvalidOperationException("Chinese source font is missing."); }
        TMP_FontAsset fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (fallback == null)
        {
            fallback = TMP_FontAsset.CreateFontAsset(source, 48, 5, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic);
            fallback.name = "Heat UI Chinese Fallback";
            fallback.isMultiAtlasTexturesEnabled = true;
            AssetDatabase.CreateAsset(fallback, FontPath);
            AssetDatabase.AddObjectToAsset(fallback.material, fallback);
            foreach (Texture2D atlas in fallback.atlasTextures)
            {
                AssetDatabase.AddObjectToAsset(atlas, fallback);
            }
        }
        fallback.isMultiAtlasTexturesEnabled = true;
        SerializedObject fontSettings = new SerializedObject(fallback);
        fontSettings.FindProperty("m_SourceFontFile").objectReferenceValue = source;
        fontSettings.FindProperty("m_SourceFontFileGUID").stringValue = AssetDatabase.AssetPathToGUID("Assets/Font/SourceHanSansCN-Regular.otf");
        fontSettings.ApplyModifiedPropertiesWithoutUndo();
        foreach (char character in Sample)
        {
            if (!fallback.HasCharacter(character, false, true))
            {
                throw new InvalidOperationException("Missing Chinese glyph: " + character);
            }
        }
        EditorUtility.SetDirty(fallback);
        foreach (Texture2D atlas in fallback.atlasTextures)
        {
            if (atlas != null) { EditorUtility.SetDirty(atlas); }
        }
        Shader shader = Shader.Find("TextMeshPro/Mobile/Distance Field");
        if (shader == null) { throw new InvalidOperationException("TMP SDF shader is missing."); }
        foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { "Assets/Heat - Complete Modern UI/Fonts" }))
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if (font.fallbackFontAssetTable == null) { font.fallbackFontAssetTable = new List<TMP_FontAsset>(); }
            if (!font.fallbackFontAssetTable.Contains(fallback)) { font.fallbackFontAssetTable.Add(fallback); }
            font.material.shader = shader;
            EditorUtility.SetDirty(font.material);
            EditorUtility.SetDirty(font);
        }
        SerializedObject settings = new SerializedObject(TMP_Settings.instance);
        SerializedProperty fallbacks = settings.FindProperty("m_fallbackFontAssets");
        bool present = false;
        for (int index = 0; index < fallbacks.arraySize; index++)
        {
            present |= fallbacks.GetArrayElementAtIndex(index).objectReferenceValue == fallback;
        }
        if (!present)
        {
            fallbacks.InsertArrayElementAtIndex(fallbacks.arraySize);
            fallbacks.GetArrayElementAtIndex(fallbacks.arraySize - 1).objectReferenceValue = fallback;
        }
        settings.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        Verify();
        HeatUiAudit.RunAll();
        HeatUiAdapterAudit.Run();
    }

    public static void Verify()
    {
        TMP_FontAsset fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (fallback == null || fallback.sourceFontFile == null || !fallback.isMultiAtlasTexturesEnabled)
        {
            throw new InvalidOperationException("Chinese fallback must include its source font and support multiple atlases.");
        }
        var manager = Resources.Load<Michsky.UI.Heat.UIManager>("Heat UI Manager");
        foreach (TMP_FontAsset font in new[] { manager.fontLight, manager.fontRegular, manager.fontMedium, manager.fontSemiBold, manager.fontBold })
        {
            if (font.material == null || font.material.shader == null ||
                !font.material.shader.name.StartsWith("TextMeshPro/"))
            {
                throw new InvalidOperationException(font.name + " has an invalid TMP shader.");
            }
            foreach (char character in Sample)
            {
                if (character != ' ' && !font.HasCharacter(character, true, true))
                {
                    throw new InvalidOperationException(font.name + " missing character: " + character);
                }
            }
        }
        Directory.CreateDirectory("Builds/QA");
        File.WriteAllText("Builds/QA/heat-ui-fonts.txt", "PASS: All five HEAT font weights resolve Chinese, punctuation, Latin and digits.\nPASS: All five HEAT font materials reference a TMP shader.\nPASS: Dynamic fallback source included; multi-atlas enabled.\n" + Sample);
    }

    public static void RenderPreview()
    {
        Run();
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
            UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
            UnityEditor.SceneManagement.NewSceneMode.Single);
        GameObject cameraObject = new GameObject("Font preview camera", typeof(Camera));
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.08f, 0.08f, 0.08f);
        GameObject canvasObject = new GameObject("Font preview", typeof(Canvas));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1;
        RenderTexture target = new RenderTexture(1600, 900, 24);
        camera.targetTexture = target;
        var manager = Resources.Load<Michsky.UI.Heat.UIManager>("Heat UI Manager");
        TMP_FontAsset[] fonts = { manager.fontLight, manager.fontRegular, manager.fontMedium, manager.fontSemiBold, manager.fontBold };
        for (int index = 0; index < fonts.Length; index++)
        {
            GameObject label = new GameObject("Font " + index, typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(canvasObject.transform, false);
            TMP_Text text = label.GetComponent<TMP_Text>();
            text.font = fonts[index];
            text.fontSize = 30;
            text.color = Color.white;
            text.text = fonts[index].name + "\n沙盒游戏 设置 数据 命令 插件 背景 返回 保存 取消\n中文输入，。！？：；（）【】0123456789 ABC xyz";
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(60, -25 - index * 170);
            rect.sizeDelta = new Vector2(1480, 150);
        }
        Canvas.ForceUpdateCanvases();
        foreach (TMP_Text text in canvasObject.GetComponentsInChildren<TMP_Text>()) { text.ForceMeshUpdate(); }
        camera.Render();
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        Texture2D screenshot = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        screenshot.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
        screenshot.Apply();
        File.WriteAllBytes("Builds/QA/heat-ui-font-preview.png", screenshot.EncodeToPNG());
        RenderTexture.active = previous;
        camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(screenshot);
        UnityEngine.Object.DestroyImmediate(target);
        UnityEngine.Object.DestroyImmediate(canvasObject);
        UnityEngine.Object.DestroyImmediate(cameraObject);
    }
}
