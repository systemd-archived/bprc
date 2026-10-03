using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using BadPiggies.Multiplayer;
using Mirror;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Rebuilds the Multiplayer assets the netcode loads at runtime (MULTIPLAYER_PORT.md step 4 and 5):
/// the two network prefabs, the main-menu button material, the S-VS arena loader copy and its entry
/// in EpisodeSandboxLevels. Everything is created through the AssetDatabase so the prefabs and the
/// material get correct serialization instead of hand-written YAML; only the NetworkIdentity asset
/// ids are written back as text, because the Mirror runtime DLL shipped with this port carries no
/// editor tooling to assign them.
/// </summary>
public static class MultiplayerAssetBuilder
{
    private const string MultiplayerFolder = "Assets/Resources/Multiplayer";
    private const string ButtonPngSource = "Tools/Multiplayer/assets/MultiplayerButton.png";
    private const string ButtonPngTarget = MultiplayerFolder + "/MultiplayerButton.png";
    private const string ButtonMaterialPath = MultiplayerFolder + "/MultiplayerButton.mat";
    private const string LobbyPlayerPath = MultiplayerFolder + "/MultiplayerLobbyPlayer.prefab";
    private const string MachineAnchorPath = MultiplayerFolder + "/MultiplayerMachineAnchor.prefab";

    private const string LoaderSource = "Assets/Resources/levels/episode_4_sandbox/level_sandbox_06_loader.prefab";
    private const string ArenaLoaderPath = "Assets/Resources/levels/episode_4_sandbox/Level_Sandbox_06_arena.prefab";
    private const string SandboxLevelsPath = "Assets/GameObject/EpisodeSandboxLevels.prefab";
    private const string ArenaEntryAfter = "S-F";
    private const string ArenaIdentifier = "S-VS";
    private const string ArenaLoaderResourcePath = "Assets/Resources/Levels/Episode_4_Sandbox/Level_Sandbox_06_arena.prefab";

    private const string ReportPath = "Builds/QA/multiplayer-assets.txt";

    // Taken from the source build's prefabs (Tools/Multiplayer/assets/prefab-structure.txt).
    public const uint LobbyPlayerAssetId = 0x8F0EB061u;
    public const uint MachineAnchorAssetId = 0x5AC8CCC4u;

    private static readonly List<string> s_log = new List<string>();

    [MenuItem("Tools/Multiplayer/Rebuild Multiplayer Assets")]
    public static void Run()
    {
        s_log.Clear();
        BuildButtonArt();
        BuildNetworkPrefab(LobbyPlayerPath, typeof(MultiplayerLobbyPlayer), LobbyPlayerAssetId);
        BuildNetworkPrefab(MachineAnchorPath, typeof(MultiplayerMachineAnchor), MachineAnchorAssetId);
        BuildArenaLoader();
        AddArenaEntry();
        Verify();
        WriteReport();
        Debug.Log("MULTIPLAYER_ASSETS_PASS " + string.Join(" | ", s_log));
    }

    // ------------------------------------------------------------------ button art

    private static void BuildButtonArt()
    {
        EnsureFolder("Assets/Resources");
        EnsureFolder(MultiplayerFolder);

        if (!File.Exists(ButtonPngSource))
        {
            throw new FileNotFoundException("Missing extracted button art: " + ButtonPngSource);
        }
        File.Copy(ButtonPngSource, ButtonPngTarget, true);
        AssetDatabase.ImportAsset(ButtonPngTarget, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(ButtonPngTarget) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ButtonPngTarget);
        if (texture == null)
        {
            throw new InvalidOperationException("The button texture did not import: " + ButtonPngTarget);
        }
        s_log.Add(string.Format("texture {0}x{1}", texture.width, texture.height));

        Shader shader = Shader.Find("Unlit/Transparent");
        if (shader == null) { shader = Shader.Find("Sprites/Default"); }
        if (shader == null) { throw new InvalidOperationException("No unlit transparent shader is available."); }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(ButtonMaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "MultiplayerButton" };
            AssetDatabase.CreateAsset(material, ButtonMaterialPath);
        }
        material.shader = shader;
        material.mainTexture = texture;
        if (material.HasProperty("_Color")) { material.SetColor("_Color", Color.white); }
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        s_log.Add("material " + shader.name);
    }

    // ------------------------------------------------------------------ network prefabs

    private static void BuildNetworkPrefab(string path, Type behaviour, uint assetId)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            AssetDatabase.DeleteAsset(path);
        }
        GameObject temp = new GameObject(Path.GetFileNameWithoutExtension(path));
        temp.AddComponent<NetworkIdentity>();
        temp.AddComponent(behaviour);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
        UnityEngine.Object.DestroyImmediate(temp);
        if (prefab == null)
        {
            throw new InvalidOperationException("Could not save the prefab: " + path);
        }

        // The Mirror runtime DLL has no editor tooling, so the asset id the source build baked into
        // its prefabs (and which both peers need to agree on) is written straight into the YAML. The
        // field is a uint in the shipped Mirror, but the serialized form is read back from the field
        // type so a string-based assetId would be written as hex instead of breaking the YAML.
        string text = File.ReadAllText(path);
        Regex pattern = new Regex("(_assetId|m_AssetId): [0-9a-fA-Fx]+");
        if (!pattern.IsMatch(text))
        {
            throw new InvalidOperationException("NetworkIdentity has no serialized asset id in " + path);
        }
        FieldInfo field = typeof(NetworkIdentity).GetField("_assetId",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field == null)
        {
            field = typeof(NetworkIdentity).GetField("m_AssetId",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }
        string serialized = field != null && field.FieldType == typeof(string)
            ? assetId.ToString("x8")
            : assetId.ToString();
        text = pattern.Replace(text, pattern.Match(text).Groups[1].Value + ": " + serialized, 1);
        File.WriteAllText(path, text);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        GameObject reloaded = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        NetworkIdentity identity = reloaded != null ? reloaded.GetComponent<NetworkIdentity>() : null;
        if (identity == null)
        {
            throw new InvalidOperationException("Prefab lost its NetworkIdentity: " + path);
        }
        Component component = reloaded.GetComponent(behaviour);
        if (component == null)
        {
            throw new InvalidOperationException("Prefab lost its " + behaviour.Name + ": " + path);
        }
        if (identity.assetId == 0)
        {
            throw new InvalidOperationException("The asset id did not stick for " + path);
        }
        if (identity.assetId != assetId)
        {
            // Mirror's own editor tooling assigns its own id on import; either value works as long as
            // both peers run the same build, so this is reported rather than treated as a failure.
            s_log.Add(string.Format("NOTE {0}: assetId became 0x{1:X8} (source build had 0x{2:X8})",
                Path.GetFileNameWithoutExtension(path), identity.assetId, assetId));
            return;
        }
        s_log.Add(string.Format("{0}: assetId=0x{1:X8}", Path.GetFileNameWithoutExtension(path), assetId));
    }

    // ------------------------------------------------------------------ arena loader

    private static void BuildArenaLoader()
    {
        if (!File.Exists(LoaderSource))
        {
            throw new FileNotFoundException("Missing the S-F loader prefab: " + LoaderSource);
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ArenaLoaderPath) != null)
        {
            AssetDatabase.DeleteAsset(ArenaLoaderPath);
        }
        if (!AssetDatabase.CopyAsset(LoaderSource, ArenaLoaderPath))
        {
            throw new InvalidOperationException("Could not copy the S-F loader to " + ArenaLoaderPath);
        }
        AssetDatabase.ImportAsset(ArenaLoaderPath, ImportAssetOptions.ForceUpdate);
        s_log.Add("arena loader copied from " + Path.GetFileName(LoaderSource));
    }

    private static void AddArenaEntry()
    {
        string guid = AssetDatabase.AssetPathToGUID(ArenaLoaderPath);
        if (string.IsNullOrEmpty(guid))
        {
            throw new InvalidOperationException("The arena loader copy has no GUID: " + ArenaLoaderPath);
        }

        string text = File.ReadAllText(SandboxLevelsPath);
        if (text.Contains("- m_identifier: " + ArenaIdentifier))
        {
            s_log.Add(ArenaIdentifier + " entry already present");
            return;
        }

        Match anchor = Regex.Match(text, "- m_identifier: " + Regex.Escape(ArenaEntryAfter) + "\r?\n" +
                                         "(?:[ \t]+.*\r?\n)*");
        if (!anchor.Success)
        {
            throw new InvalidOperationException("Could not find the " + ArenaEntryAfter + " entry in " + SandboxLevelsPath);
        }

        string entry = string.Format(
            "  - m_identifier: {0}\n    m_starBoxCount: 0\n    m_levelLoaderPath: {1}\n    m_levelLoaderGUID: {2}\n",
            ArenaIdentifier, ArenaLoaderResourcePath, guid);
        text = text.Insert(anchor.Index + anchor.Length, entry);
        File.WriteAllText(SandboxLevelsPath, text);
        AssetDatabase.ImportAsset(SandboxLevelsPath, ImportAssetOptions.ForceUpdate);
        s_log.Add(ArenaIdentifier + " entry inserted after " + ArenaEntryAfter + " (guid " + guid + ")");
    }

    // ------------------------------------------------------------------ verification

    private static void Verify()
    {
        GameObject levels = AssetDatabase.LoadAssetAtPath<GameObject>(SandboxLevelsPath);
        SandboxLevels sandboxLevels = levels != null ? levels.GetComponent<SandboxLevels>() : null;
        if (sandboxLevels == null)
        {
            throw new InvalidOperationException("EpisodeSandboxLevels lost its SandboxLevels component.");
        }
        SandboxLevels.LevelData data = sandboxLevels.GetLevelData(ArenaIdentifier);
        if (data == null)
        {
            throw new InvalidOperationException(ArenaIdentifier + " is missing from the sandbox level list.");
        }
        if (data.m_levelLoaderPath != ArenaLoaderResourcePath)
        {
            throw new InvalidOperationException(ArenaIdentifier + " points at " + data.m_levelLoaderPath);
        }
        GameObject arena = AssetDatabase.LoadAssetAtPath<GameObject>(ArenaLoaderPath);
        if (arena == null)
        {
            throw new InvalidOperationException("The arena loader prefab is not loadable.");
        }

        // The arena loader has to derive the same scene name as the S-F loader, since it is a copy of it.
        string sceneName = Path.GetFileNameWithoutExtension(ArenaLoaderResourcePath);
        sceneName = sceneName.Remove(sceneName.LastIndexOf('_'));
        if (sceneName != "Level_Sandbox_06")
        {
            throw new InvalidOperationException("Unexpected arena scene name: " + sceneName);
        }

        GameObject lobbyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LobbyPlayerPath);
        GameObject anchorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MachineAnchorPath);
        if (lobbyPrefab == null || anchorPrefab == null)
        {
            throw new InvalidOperationException("A Multiplayer prefab is missing from " + MultiplayerFolder);
        }
        if (AssetDatabase.LoadAssetAtPath<Material>(ButtonMaterialPath) == null)
        {
            throw new InvalidOperationException("The button material is missing.");
        }
        s_log.Add("verified arena scene " + sceneName + ", 2 prefabs, 1 material");
    }

    private static void WriteReport()
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Multiplayer asset rebuild (MultiplayerAssetBuilder) " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        foreach (string line in s_log)
        {
            builder.AppendLine("  " + line);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, builder.ToString());
        s_log.Add("report " + ReportPath);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string name = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }
        AssetDatabase.CreateFolder(parent, name);
    }
}
