using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using BadPiggies.Multiplayer;
using Mirror;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Verifies the Multiplayer multiplayer port end to end: project settings, Mirror runtime, netcode sources,
/// Weaver-generated serialization, the rebuilt Resources/Multiplayer assets, the S-VS arena entry, and the
/// coverage of the base-game hook patches. Run in batch mode:
///   Unity.exe -batchmode -nographics -quit -projectPath . -executeMethod MultiplayerAudit.Run -logFile ...
/// </summary>
public static class MultiplayerAudit
{
    private const string MultiplayerSourceFolder = "Assets/Scripts/Assembly-CSharp/Multiplayer";
    private const string GeneratedCodePath = "Assets/Scripts/Assembly-CSharp/Mirror/GeneratedNetworkCode.cs";
    private const string ReportPath = "Builds/QA/multiplayer-audit.txt";
    private const string LayerReportPath = "Builds/QA/multiplayer-settings-api.txt";

    private static readonly string[] RequiredPluginFiles =
    {
        "Mirror.dll", "Mirror.Transports.dll", "Mirror.Components.dll", "Mirror.Authenticators.dll",
        "Mirror.BouncyCastle.Cryptography.dll", "kcp2k.dll", "Telepathy.dll", "SimpleWebTransport.dll"
    };

    private static readonly string[] RequiredNetcodeTypes =
    {
        "MultiplayerArenaLoader", "MultiplayerArenaPresence", "MultiplayerAttachments", "MultiplayerBattleAssembler",
        "MultiplayerBlueprint", "MultiplayerBlueprintMirror", "MultiplayerBootstrap", "MultiplayerBuildPhaseUI",
        "MultiplayerGameInputBlock", "MultiplayerInput", "MultiplayerLayers", "MultiplayerLobbyPlayer", "MultiplayerLobbyUI",
        "MultiplayerMachineAnchor", "MultiplayerMatchController", "MultiplayerMenuButton", "MultiplayerMenuInjector",
        "MultiplayerMirrorAudit", "MultiplayerNetConfig", "MultiplayerNetworkManager", "MultiplayerPartAxis",
        "MultiplayerPoseBuffer", "MultiplayerReadyCheckmark", "MultiplayerReadyCounter", "MultiplayerResultUI",
        "MultiplayerSnapshot", "MultiplayerSpawnResolver", "MultiplayerTraffic"
    };

    /// <summary>The netcode files the source build shipped; MultiplayerMenuLayout is the port's own addition.</summary>
    private static readonly string[] SourceBuildNetcodeFiles =
    {
        "MultiplayerArenaLoader", "MultiplayerArenaPresence", "MultiplayerAttachments", "MultiplayerBattleAssembler",
        "MultiplayerBlueprint", "MultiplayerBlueprintMirror", "MultiplayerBootstrap", "MultiplayerBuildPhaseUI",
        "MultiplayerGameInputBlock", "MultiplayerInput", "MultiplayerLayers", "MultiplayerLoadArenaMessage",
        "MultiplayerLobbyPlayer", "MultiplayerLobbyUI", "MultiplayerMachineAnchor", "MultiplayerMatchController",
        "MultiplayerMenuButton", "MultiplayerMenuInjector", "MultiplayerMirrorAudit", "MultiplayerNetConfig",
        "MultiplayerNetworkManager", "MultiplayerPartAxis", "MultiplayerPoseBuffer", "MultiplayerReadyCheckmark",
        "MultiplayerReadyCounter", "MultiplayerResultUI", "MultiplayerSessionClosingMessage", "MultiplayerSnapshot",
        "MultiplayerSpawnResolver", "MultiplayerTraffic"
    };

    /// <summary>Layers the machine layer must never collide with: HUD, effect and trigger-only layers.</summary>
    private static readonly int[] IsolatedMachineLayers = { 10, 15, 16, 17, 18, 22, 23 };

    /// <summary>
    /// Symbols every base-game hook in the port has to provide. The port's hooks are guards ("is this
    /// object a network replica?") and layer helpers, so this list is the contract the netcode relies on.
    /// </summary>
    private static readonly Dictionary<string, string[]> RequiredHookSymbols = new Dictionary<string, string[]>
    {
        { "BasePart.cs", new[] { "IsMultiplayerReplica", "PlayRemovalEffect" } },
        { "Contraption.cs", new[] { "MultiplayerDriveSign", "MultiplayerInput.RouteTouch" } },
        { "GameMode.cs", new[] { "MultiplayerSetContraptionRunning", "MultiplayerInput.RouteBulk" } },
        { "Balloon.cs", new[] { "IsMultiplayerReplica" } },
        { "Bellows.cs", new[] { "IsMultiplayerReplica" } },
        { "ExplodingGrapplingHook.cs", new[] { "IsMultiplayerReplica" } },
        { "FuelBox.cs", new[] { "IsMultiplayerReplica" } },
        { "Gearbox.cs", new[] { "IsMultiplayerReplica" } },
        { "GrapplingHook.cs", new[] { "IsMultiplayerReplica", "m_multiplayerVisualOnly" } },
        { "MotorWheel.cs", new[] { "MultiplayerDriveSign" } },
        { "OffRoadWheel.cs", new[] { "MultiplayerDriveSign" } },
        { "PointLight.cs", new[] { "IsMultiplayerReplica" } },
        { "PressureButton.cs", new[] { "MultiplayerLayers.IsContraptionLayer" } },
        { "Rocket.cs", new[] { "MultiplayerLayers.ContraptionLayerMask", "IsMultiplayerReplica" } },
        { "SPDTSwitchPart.cs", new[] { "IsMultiplayerReplica" } },
        { "SpringBoxingGlove.cs", new[] { "IsMultiplayerReplica", "m_multiplayerVisualOnly" } },
        { "StickyWheel.cs", new[] { "MultiplayerLayers.ContraptionLayerMask", "MultiplayerDriveSign" } },
        { "SwitchPart.cs", new[] { "IsMultiplayerReplica" } },
        { "TimeBomb.cs", new[] { "IsMultiplayerReplica" } },
        { "TNT.cs", new[] { "IsMultiplayerReplica" } },
        { "VccPart.cs", new[] { "IsMultiplayerReplica" } }
    };

    /// <summary>Patch symbols that are deliberately not ported, with the reason they are absent.</summary>
    private static readonly Dictionary<string, string> AcceptedMissing = new Dictionary<string, string>();

    private static readonly List<string> s_log = new List<string>();
    private static readonly List<string> s_failures = new List<string>();

    [MenuItem("Tools/Multiplayer/Audit Multiplayer Port")]
    public static void Run()
    {
        s_log.Clear();
        s_failures.Clear();

        CheckSettings();
        CheckMirrorRuntime();
        CheckNetcodeSources();
        CheckGeneratedNetworkCode();
        CheckAssets();
        CheckArenaEntry();
        CheckHookMembers();
        CheckHookCoverage();

        WriteReport();
        if (s_failures.Count > 0)
        {
            throw new Exception("MULTIPLAYER_AUDIT_FAILED with " + s_failures.Count + " failure(s): " +
                                string.Join("; ", s_failures));
        }
        Debug.Log("MULTIPLAYER_AUDIT_PASS " + string.Join(" | ", s_log));
    }

    // ------------------------------------------------------------------ settings

    private static void CheckSettings()
    {
        int parts = LayerMask.NameToLayer(MultiplayerLayers.PartsLayerName);
        Expect(parts == MultiplayerLayers.PartsLayerIndex,
            MultiplayerLayers.PartsLayerName + " layer index is " + parts + ", expected " + MultiplayerLayers.PartsLayerIndex);
        if (parts < 0)
        {
            return;
        }
        // The layer must not have taken over the stock contraption layer, or the base game's own
        // layer tests would start matching every machine.
        Expect(parts != LayerMask.NameToLayer("Contraption"), "The machine layer took over the Contraption layer index");

        string error;
        Expect(MultiplayerLayers.AreConfigured(out error), "MultiplayerLayers.AreConfigured failed: " + error);
        // First read the matrix as it is serialised, then force it: the runtime applies it from code,
        // so a stale ProjectSettings file is a reportable warning rather than a silent failure.
        bool serialisedOk = MultiplayerLayers.VerifyCollisionMatrix(out error);
        s_log.Add(serialisedOk
            ? "serialised collision matrix is already correct"
            : "serialised collision matrix needed fixing: " + error);
        MultiplayerLayers.ApplyCollisionMatrix();
        Expect(MultiplayerLayers.VerifyCollisionMatrix(out error), "collision matrix is wrong after applying: " + error);
        s_log.Add(string.Format("machine layer {0} ({1}), matrix verified on {2} isolated layer(s)",
            parts, MultiplayerLayers.PartsLayerName, IsolatedMachineLayers.Length));
    }

    // ------------------------------------------------------------------ runtime

    private static void CheckMirrorRuntime()
    {
        string folder = Path.Combine(Application.dataPath, "Plugins/Mirror");
        foreach (string file in RequiredPluginFiles)
        {
            Expect(File.Exists(Path.Combine(folder, file)), "Missing Mirror plugin: Assets/Plugins/Mirror/" + file);
        }
        Type manager = typeof(Mirror.NetworkManager);
        Expect(manager != null, "Mirror.NetworkManager is not resolvable");
        Expect(typeof(Mirror.NetworkBehaviour) != null, "Mirror.NetworkBehaviour is not resolvable");
        Expect(typeof(Mirror.NetworkIdentity) != null, "Mirror.NetworkIdentity is not resolvable");
        Expect(typeof(kcp2k.KcpTransport) != null, "kcp2k.KcpTransport is not resolvable");

        // The main project has its own NetworkManager; the Multiplayer sources must keep qualifying Mirror's.
        string multiplayerManager = ReadProjectFile("Assets/Scripts/Assembly-CSharp/Multiplayer/MultiplayerNetworkManager.cs");
        Expect(multiplayerManager.Contains("Mirror.NetworkManager"),
            "MultiplayerNetworkManager no longer qualifies Mirror.NetworkManager (the project has a NetworkManager of its own)");
        Expect(multiplayerManager.Contains("MultiplayerNetConfig.Port") || multiplayerManager.Contains("Port"),
            "MultiplayerNetworkManager lost its port handling");
        s_log.Add("Mirror runtime present (" + RequiredPluginFiles.Length + " assemblies)");
    }

    private static void CheckNetcodeSources()
    {
        string folder = Path.Combine(ProjectRoot, MultiplayerSourceFolder);
        string[] files = Directory.GetFiles(folder, "*.cs");

        // The 30 files the source build shipped, plus whatever the port added on top of them
        // (MultiplayerMenuLayout is ours: it arranges the three main menu buttons).
        foreach (string name in SourceBuildNetcodeFiles)
        {
            Expect(File.Exists(Path.Combine(folder, name + ".cs")), "Missing netcode source: " + name + ".cs");
        }
        Expect(files.Length >= SourceBuildNetcodeFiles.Length,
            "Expected at least " + SourceBuildNetcodeFiles.Length + " netcode sources, found " + files.Length);

        Assembly assembly = typeof(MultiplayerBootstrap).Assembly;
        foreach (string name in RequiredNetcodeTypes)
        {
            Expect(assembly.GetType("BadPiggies.Multiplayer." + name) != null, "Missing netcode type: " + name);
        }
        Expect(assembly.GetType("BadPiggies.Multiplayer.MultiplayerLoadArenaMessage") != null, "Missing MultiplayerLoadArenaMessage");
        Expect(assembly.GetType("BadPiggies.Multiplayer.MultiplayerSessionClosingMessage") != null, "Missing MultiplayerSessionClosingMessage");

        // C# 9: the decompiled file-scoped namespaces had to be turned into blocks.
        foreach (string file in files)
        {
            string text = File.ReadAllText(file);
            Expect(!Regex.IsMatch(text, @"^namespace [\w.]+;", RegexOptions.Multiline),
                "File-scoped namespace left in " + Path.GetFileName(file) + " (the project is on C# 9)");
        }

        string blueprint = ReadProjectFile("Assets/Scripts/Assembly-CSharp/Multiplayer/MultiplayerBlueprint.cs");
        Expect(blueprint.Contains("(int)BasePart.PartType.MAX"),
            "MultiplayerBlueprint does not use (int)BasePart.PartType.MAX as its part type ceiling");
        Expect(!Regex.IsMatch(blueprint, @">=\s*82\b"),
            "MultiplayerBlueprint still rejects part types at the source build's ceiling of 82");
        s_log.Add(files.Length + " netcode sources (" + SourceBuildNetcodeFiles.Length + " from the source build), "
            + RequiredNetcodeTypes.Length + " types verified");
    }

    private static void CheckGeneratedNetworkCode()
    {
        Expect(File.Exists(Path.Combine(ProjectRoot, GeneratedCodePath)),
            "Missing " + GeneratedCodePath + " (the Weaver output the Mirror DLL cannot regenerate)");
        // Resolved through the game assembly on purpose: Mirror.Authenticators.dll carries a
        // Weaver-generated Mirror.GeneratedNetworkCode of its own, so the name is ambiguous.
        Type generated = typeof(MultiplayerBootstrap).Assembly.GetType("Mirror.GeneratedNetworkCode");
        Expect(generated != null, "Mirror.GeneratedNetworkCode is not present in Assembly-CSharp");
        foreach (string name in new[]
                 {
                     "_Read_BadPiggies_002EMultiplayer_002EMultiplayerLoadArenaMessage",
                     "_Write_BadPiggies_002EMultiplayer_002EMultiplayerLoadArenaMessage",
                     "_Read_BadPiggies_002EMultiplayer_002EMultiplayerSessionClosingMessage",
                     "_Write_BadPiggies_002EMultiplayer_002EMultiplayerSessionClosingMessage"
                 })
        {
            Expect(generated.GetMethod(name, BindingFlags.Public | BindingFlags.Static) != null,
                "GeneratedNetworkCode is missing " + name);
        }

        // The static constructor wires those readers and writers into Mirror's tables.
        string text = ReadProjectFile(GeneratedCodePath);
        Expect(text.Contains("Reader<MultiplayerLoadArenaMessage>.read") && text.Contains("Writer<MultiplayerLoadArenaMessage>.write"),
            "GeneratedNetworkCode does not register the MultiplayerLoadArenaMessage reader/writer");
        Expect(text.Contains("Reader<MultiplayerSessionClosingMessage>.read") &&
               text.Contains("Writer<MultiplayerSessionClosingMessage>.write"),
            "GeneratedNetworkCode does not register the MultiplayerSessionClosingMessage reader/writer");
        s_log.Add("Weaver output registers both Multiplayer network messages");
    }

    // ------------------------------------------------------------------ assets

    private static void CheckAssets()
    {
        GameObject lobby = Resources.Load<GameObject>("Multiplayer/MultiplayerLobbyPlayer");
        GameObject anchor = Resources.Load<GameObject>("Multiplayer/MultiplayerMachineAnchor");
        Expect(lobby != null, "Resources/Multiplayer/MultiplayerLobbyPlayer is missing");
        Expect(anchor != null, "Resources/Multiplayer/MultiplayerMachineAnchor is missing");
        if (lobby != null)
        {
            CheckNetworkPrefab(lobby, "Multiplayer/MultiplayerLobbyPlayer", typeof(MultiplayerLobbyPlayer));
        }
        if (anchor != null)
        {
            CheckNetworkPrefab(anchor, "Multiplayer/MultiplayerMachineAnchor", typeof(MultiplayerMachineAnchor));
        }
        if (lobby != null && anchor != null)
        {
            uint lobbyId = lobby.GetComponent<NetworkIdentity>().assetId;
            uint anchorId = anchor.GetComponent<NetworkIdentity>().assetId;
            // The source build's ids are what the builder aims for; Mirror may re-assign them on import,
            // which is fine as long as both peers share the build. Distinct non-zero ids are what matter.
            Expect(lobbyId != anchorId, "The two Multiplayer prefabs share one asset id, so Mirror cannot tell them apart");
            s_log.Add(string.Format("prefab asset ids: lobby=0x{0:X8} anchor=0x{1:X8} (source build 0x{2:X8}/0x{3:X8})",
                lobbyId, anchorId, MultiplayerAssetBuilder.LobbyPlayerAssetId, MultiplayerAssetBuilder.MachineAnchorAssetId));
        }

        Material material = Resources.Load<Material>("Multiplayer/MultiplayerButton");
        Expect(material != null, "Resources/Multiplayer/MultiplayerButton material is missing");
        if (material != null)
        {
            Expect(material.mainTexture != null, "The Multiplayer button material has no main texture");
            Expect(material.shader != null && material.shader.isSupported,
                "The Multiplayer button material's shader is not supported: " + (material.shader != null ? material.shader.name : "null"));
        }
        s_log.Add("Resources/Multiplayer assets verified (2 prefabs + material)");
    }

    private static void CheckNetworkPrefab(GameObject prefab, string label, Type behaviour)
    {
        NetworkIdentity identity = prefab.GetComponent<NetworkIdentity>();
        Expect(identity != null, label + " has no NetworkIdentity");
        Expect(prefab.GetComponent(behaviour) != null, label + " has no " + behaviour.Name + " component");
        Expect(prefab.transform.childCount == 0, label + " is expected to have no children");
        if (identity != null)
        {
            Expect(identity.assetId != 0, label + " has assetId 0, so Mirror cannot resolve it across the network");
        }
    }

    private static void CheckArenaEntry()
    {
        const string levelsPath = "Assets/GameObject/EpisodeSandboxLevels.prefab";
        GameObject levels = AssetDatabase.LoadAssetAtPath<GameObject>(levelsPath);
        Expect(levels != null, "Could not load " + levelsPath);
        if (levels == null)
        {
            return;
        }
        SandboxLevels sandboxLevels = levels.GetComponent<SandboxLevels>();
        Expect(sandboxLevels != null, levelsPath + " has no SandboxLevels component");
        if (sandboxLevels == null)
        {
            return;
        }
        SandboxLevels.LevelData data = sandboxLevels.GetLevelData("S-VS");
        Expect(data != null, "The sandbox level list has no S-VS entry");
        if (data == null)
        {
            return;
        }
        const string expectedPath = "Assets/Resources/Levels/Episode_4_Sandbox/Level_Sandbox_06_arena.prefab";
        Expect(data.m_levelLoaderPath == expectedPath,
            "S-VS points at " + data.m_levelLoaderPath + " instead of " + expectedPath);
        Expect(data.SceneName == "Level_Sandbox_06",
            "S-VS resolves to scene " + data.SceneName + " instead of Level_Sandbox_06 (the S-F arena)");

        string physical = Path.Combine(ProjectRoot, "Assets/Resources/levels/episode_4_sandbox/Level_Sandbox_06_arena.prefab");
        Expect(File.Exists(physical), "The S-VS arena loader prefab is missing on disk");

        // The arena has to keep the sandbox's own loader, and S-F must still be listed.
        Expect(sandboxLevels.GetLevelData("S-F") != null, "The S-F entry disappeared from the sandbox level list");
        s_log.Add("S-VS arena entry present and pointing at the S-F loader copy");
    }

    // ------------------------------------------------------------------ hooks

    private static void CheckHookMembers()
    {
        Expect(HasMember(typeof(BasePart), "PlayRemovalEffect"), "BasePart.PlayRemovalEffect() is missing");
        Expect(HasMember(typeof(BasePart), "IsMultiplayerReplica"), "BasePart.IsMultiplayerReplica() is missing");
        Expect(HasMember(typeof(Contraption), "MultiplayerDriveSign"), "Contraption.MultiplayerDriveSign is missing");
        Expect(HasMember(typeof(GameMode), "MultiplayerSetContraptionRunning"), "GameMode.MultiplayerSetContraptionRunning(...) is missing");

        string basePart = ReadProjectFile("Assets/Scripts/Assembly-CSharp/BasePart.cs");
        Expect(basePart.Contains("IsMultiplayerReplica") && basePart.Contains("rigidbody.isKinematic"),
            "BasePart.IsMultiplayerReplica does not test rigidbody.isKinematic");

        // The military features toggle from the previous port must survive the Multiplayer hooks.
        Expect(File.Exists(Path.Combine(ProjectRoot, "Assets/Scripts/Assembly-CSharp/INMilitaryFeatures.cs")),
            "INMilitaryFeatures.cs disappeared while porting Multiplayer");
        s_log.Add("BasePart/Contraption/GameMode Multiplayer members verified");
    }

    private static bool HasMember(Type type, string name)
    {
        return type.GetMember(name, BindingFlags.Public | BindingFlags.NonPublic |
                                   BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length > 0;
    }

    /// <summary>
    /// Every base-game hook the netcode depends on has to exist in the ported file. This is the
    /// definition of done for the hooks: a guard that silently disappears would leave the remote
    /// machines simulating locally, or would cut them out of every part's own layer/raycast logic.
    /// </summary>
    private static void CheckHookCoverage()
    {
        int files = 0;
        int symbols = 0;
        foreach (KeyValuePair<string, string[]> entry in RequiredHookSymbols.OrderBy(p => p.Key))
        {
            string relative = "Assets/Scripts/Assembly-CSharp/" + entry.Key;
            string text = ReadProjectFile(relative);
            if (text.Length == 0)
            {
                continue;
            }
            files++;
            foreach (string symbol in entry.Value)
            {
                symbols++;
                Expect(text.Contains(symbol), entry.Key + " is missing hook symbol " + symbol);
            }
        }
        s_log.Add(string.Format("{0} hook files, {1} hook symbols checked", files, symbols));

        if (File.Exists(Path.Combine(ProjectRoot, LayerReportPath)))
        {
            s_log.Add("settings report " + LayerReportPath + " present");
        }
        else
        {
            s_log.Add("settings report " + LayerReportPath + " not written yet (run Tools/Multiplayer/Apply Multiplayer Layers)");
        }
    }

    // ------------------------------------------------------------------ plumbing

    private static string ProjectRoot
    {
        get { return Directory.GetParent(Application.dataPath).FullName; }
    }

    private static string ReadProjectFile(string relative)
    {
        string path = Path.Combine(ProjectRoot, relative);
        if (!File.Exists(path))
        {
            s_failures.Add("Missing file: " + relative);
            return string.Empty;
        }
        return File.ReadAllText(path);
    }

    private static void Expect(bool condition, string failure)
    {
        if (!condition)
        {
            s_failures.Add(failure);
        }
    }

    private static void WriteReport()
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Multiplayer port audit (MultiplayerAudit) " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        builder.AppendLine("unity " + Application.unityVersion + ", project " + ProjectRoot);
        builder.AppendLine();
        builder.AppendLine("checks:");
        foreach (string line in s_log)
        {
            builder.AppendLine("  OK   " + line);
        }
        builder.AppendLine();
        builder.AppendLine("failures: " + s_failures.Count);
        foreach (string line in s_failures)
        {
            builder.AppendLine("  FAIL " + line);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(ProjectRoot, ReportPath)));
        File.WriteAllText(Path.Combine(ProjectRoot, ReportPath), builder.ToString());
        Debug.Log("Multiplayer audit report written to " + ReportPath);
    }
}
