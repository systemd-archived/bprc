using UnityEditor;
using UnityEngine;

public sealed class RoverPortTextureSettings : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/RCR2Ported/Textures/"))
            return;
        Configure((TextureImporter)assetImporter);
    }

    private static void Configure(TextureImporter importer)
    {
        importer.maxTextureSize = 4096;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.crunchedCompression = false;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        foreach (string platform in new[] { "Standalone", "Android" })
        {
            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platform);
            settings.overridden = true;
            settings.maxTextureSize = 4096;
            settings.format = TextureImporterFormat.RGBA32;
            settings.textureCompression = TextureImporterCompression.Uncompressed;
            settings.crunchedCompression = false;
            importer.SetPlatformTextureSettings(settings);
        }
    }

    [MenuItem("Tools/RCR2/Restore source texture quality")]
    public static void Apply()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/RCR2Ported/Textures" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Configure(importer);
            importer.SaveAndReimport();
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Debug.Log("RCR2_TEXTURE_QUALITY: " + path + " " + texture.width + "x" + texture.height + " " + texture.format);
        }
    }
}
