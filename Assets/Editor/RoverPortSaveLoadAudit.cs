using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using RCR2Port;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Verifies that a saved sandbox blueprint loads with all of its parts - including the game's own
/// variant parts whose type is shared with ported parts (RCR2_PORT.md).
///
/// The build list holds one button per part type and it shows the player's last used variant of that
/// type (PartSelector.CreatePartList -> CustomizationManager.GetLastUsedPartIndex), so
/// ConstructionUI.FindPartDesc had no descriptor for the other variants of the same type and
/// GameMode.BuildContraption skipped those parts silently: leaving the sandbox, re-entering it and
/// loading the save lost the native frames and the native motor wheel while the ported parts of the
/// same types survived.
///
/// The checks reproduce that state: a part list whose type buttons show the ported variants (what the
/// player's Settings.xml holds), the (part type, custom part index) pairs of a real save, and the part
/// data each pair has to resolve through.
///
/// Run with: Unity -batchmode -quit -executeMethod RoverPortSaveLoadAudit.Run
/// Report: Builds/QA/save-load-audit.txt
/// </summary>
public static class RoverPortSaveLoadAudit
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    /// <summary>
    /// The (SortedPartType, customPartIndex) pairs of the sandbox save that lost parts on load
    /// (Documents/新创Rover 预览版/contraptionsB/Level_Sandbox_06): the native variants 6 (frame) and
    /// 7 (motor wheel, supplied by the mod's off-road wheel part family) disappeared while the ported
    /// parts of the same types survived.
    /// </summary>
    private static readonly int[][] SavedParts =
    {
        new[] { 6, 6 },     // MetalFrame 6   (Part_MetalFrame_07_SET, game data)
        new[] { 6, 2019 },  // MetalFrame 2019 (ported)
        new[] { 6, 2044 },  // MetalFrame 2044 (ported)
        new[] { 10, 7 },    // MotorWheel 7   (Part_MotorWheel_08_SET, off-road wheel part family)
        new[] { 10, 2049 }, // MotorWheel 2049 (ported)
        new[] { 16, 4 },    // SodaBottle 4   (game data)
        new[] { 40, 0 },    // Pumpkin 0      (game data, regular)
        new[] { 1, 0 },     // Pig 0          (game data, regular)
        new[] { 48, 2002 }  // MechanicalPart 2002 (ported)
    };

    private static readonly List<string> failures = new List<string>();
    private static GameData s_gameData;
    private static PartListData s_partListData;

    [MenuItem("Tools/RCR2/Audit sandbox save load")]
    public static void Run()
    {
        PrepareEditorServices();
        var rows = new List<string> { "sortedType,partType,index,partsOfType,partData,listWithoutVariant,listWithVariant" };
        int checks = 0;
        var host = new GameObject("SaveLoadAuditHost");
        host.SetActive(false);
        try
        {
            foreach (int[] saved in SavedParts)
            {
                var sortedType = (SortedPartType)saved[0];
                BasePart.PartType partType = sortedType.ToPartType();
                int index = saved[1];
                string partData = "?";
                bool partsOfType = false;
                bool withoutVariant = false;
                bool withVariant = false;
                try
                {
                    partData = ResolveInData(partType, index);
                    partsOfType = IsTypeInBuild(partType);
                    // The state that broke the load: the type's button carries the variant the player
                    // used last (a ported frame / wheel), not the one this save holds.
                    withoutVariant = DescFound(host, partType, index, variantDescs: false);
                    withVariant = DescFound(host, partType, index, variantDescs: true);
                    Require(partsOfType, "part type " + partType + " is not part of this build");
                    Require(withoutVariant, "no descriptor for " + partType + " " + index + " while another variant of the type is the last used one");
                    Require(withVariant, "no descriptor for " + partType + " " + index + " while the saved variant is the last used one");
                    checks++;
                }
                catch (Exception exception)
                {
                    failures.Add("saved part " + sortedType + " " + index + ": " + exception.Message);
                }
                rows.Add(string.Join(",", new[] {
                    sortedType.ToString(), partType.ToString(), index.ToString(), partsOfType.ToString(),
                    partData, withoutVariant.ToString(), withVariant.ToString() }));
            }
            AuditMissingType(host);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }

        Directory.CreateDirectory("Builds/QA");
        File.WriteAllLines("Builds/QA/save-load-audit.txt", new List<string> {
            "Saved parts checked: " + checks,
            "Failures: " + failures.Count }.Concat(failures).Concat(rows));
        if (failures.Count > 0)
        {
            throw new Exception(string.Join("\n", failures));
        }
        Debug.Log("RCR2_SAVELOAD_PASS: " + checks + " saved parts keep their descriptor with either variant in the build list");
    }

    /// <summary>
    /// Loads the descriptor the way the game does when a sandbox level starts, in both states of the
    /// build list: the type's regular part (what the player's last used ported variant leaves behind)
    /// and the saved variant itself.
    /// </summary>
    private static bool DescFound(GameObject host, BasePart.PartType partType, int index, bool variantDescs)
    {
        ConstructionUI ui = BuildPartList(host, variantDescs);
        ConstructionUI.PartDesc found = ui.FindPartDesc(partType, index);
        if (found == null || found.part == null)
        {
            return false;
        }
        // A ported part is identified by its index (a save may carry a part type the port has since
        // corrected, the roller became a plain wheel for one); the game's own parts by their type.
        return index >= RoverPortCatalog.FirstPartIndex
            ? found.part.customPartIndex == index
            : found.part.m_partType == partType;
    }

    /// <summary>Mirrors ConstructionUI.Awake: the game's regular part per type plus every ported part.</summary>
    private static ConstructionUI BuildPartList(GameObject host, bool variantDescs)
    {
        var uiObject = new GameObject("SaveLoadAuditUI");
        uiObject.transform.SetParent(host.transform, false);
        var ui = uiObject.AddComponent<ConstructionUI>();
        var descs = new List<ConstructionUI.PartDesc>();
        foreach (BasePart.PartType type in TypesInFixture())
        {
            descs.Add(Desc(host, type, variantDescs ? SavedIndex(type) : 0));
        }
        foreach (GameObject prototype in RoverPortCatalog.Instance.Parts)
        {
            var ported = prototype.GetComponent<BasePart>();
            descs.Add(Desc(host, ported.m_partType, ported.customPartIndex, ported));
        }
        typeof(ConstructionUI).GetField("m_partDescs", PrivateInstance).SetValue(ui, descs);
        return ui;
    }

    private static IEnumerable<BasePart.PartType> TypesInFixture()
    {
        var types = new List<BasePart.PartType>();
        foreach (int[] saved in SavedParts)
        {
            BasePart.PartType type = ((SortedPartType)saved[0]).ToPartType();
            if (!types.Contains(type))
            {
                types.Add(type);
            }
        }
        return types;
    }

    /// <summary>The variant the player last used for that type (Settings.xml), i.e. what the button carries.</summary>
    private static int SavedIndex(BasePart.PartType type)
    {
        foreach (int[] saved in SavedParts)
        {
            if (((SortedPartType)saved[0]).ToPartType() == type && saved[1] < RoverPortCatalog.FirstPartIndex)
            {
                return saved[1];
            }
        }
        return 0;
    }

    private static ConstructionUI.PartDesc Desc(GameObject host, BasePart.PartType type, int index, BasePart part = null)
    {
        if (part == null)
        {
            var partObject = new GameObject("AuditPart_" + type + "_" + index);
            partObject.transform.SetParent(host.transform, false);
            part = partObject.AddComponent<BasePart>();
            part.m_partType = type;
            part.customPartIndex = index;
        }
        return new ConstructionUI.PartDesc { part = part, customPartIndex = index };
    }

    /// <summary>The type's button is a fallback, it must not invent a part list entry for a type the level does not offer.</summary>
    private static void AuditMissingType(GameObject host)
    {
        ConstructionUI ui = BuildPartList(host, variantDescs: false);
        Require(ui.FindPartDesc(BasePart.PartType.KingPig, 0) == null, "descriptor invented for an absent part type");
    }

    /// <summary>
    /// Where a saved (part type, custom part index) pair comes from in this build: the ported catalog,
    /// the level's own part data, or the mod's part list (which carries the part families the game data
    /// does not, e.g. the off-road wheel set). GameMode.BuildContraption builds the part through
    /// GameData.GetCustomPart(partType, index), so the part has to exist in one of these.
    /// </summary>
    private static string ResolveInData(BasePart.PartType partType, int index)
    {
        if (index >= RoverPortCatalog.FirstPartIndex)
        {
            GameObject ported = RoverPortCatalog.Instance.GetPart(index);
            Require(ported != null, "ported catalog has no part " + index);
            // The part type a save stored can be stale (a part type the port has since corrected), the
            // ported index decides which part is meant - see FindPartDesc.
            return "ported catalog (" + ported.GetComponent<BasePart>().Type + ")";
        }
        if (GameDataPart(partType, index) != null)
        {
            return "game data";
        }
        if (ModPartListPart(partType, index) != null)
        {
            return "mod part list";
        }
        throw new Exception("no part " + partType + " " + index + " in the build data");
    }

    private static BasePart GameDataPart(BasePart.PartType partType, int index)
    {
        foreach (GameObject part in LoadGameData().m_parts ?? new List<GameObject>())
        {
            BasePart component = Match(part, partType, index);
            if (component != null)
            {
                return component;
            }
        }
        foreach (CustomPartInfo info in LoadGameData().m_customParts ?? new List<CustomPartInfo>())
        {
            foreach (BasePart part in info.PartList)
            {
                if (part != null && part.m_partType == partType && part.customPartIndex == index)
                {
                    return part;
                }
            }
        }
        return null;
    }

    private static BasePart ModPartListPart(BasePart.PartType partType, int index)
    {
        foreach (GameObject part in LoadPartListData().Parts ?? new List<GameObject>())
        {
            BasePart component = Match(part, partType, index);
            if (component != null)
            {
                return component;
            }
        }
        return null;
    }

    private static PartListData LoadPartListData()
    {
        if (s_partListData == null)
        {
            s_partListData = AssetDatabase.LoadAssetAtPath<PartListData>("Assets/MonoBehaviour/PartListData.asset");
            Require(s_partListData != null, "part list asset not loadable");
        }
        return s_partListData;
    }

    private static GameData LoadGameData()
    {
        if (s_gameData == null)
        {
            s_gameData = AssetDatabase.LoadAssetAtPath<GameData>("Assets/MonoBehaviour/GameData.asset");
            Require(s_gameData != null, "game data asset not loadable");
        }
        return s_gameData;
    }

    private static BasePart Match(GameObject part, BasePart.PartType partType, int index)
    {
        var component = part == null ? null : part.GetComponent<BasePart>();
        return component != null && component.m_partType == partType && component.customPartIndex == index ? component : null;
    }

    /// <summary>The type has to be a real part type of this build: the game data, the mod's part list or the ported catalog.</summary>
    private static bool IsTypeInBuild(BasePart.PartType partType)
    {
        foreach (GameObject part in LoadGameData().m_parts ?? new List<GameObject>())
        {
            var component = part == null ? null : part.GetComponent<BasePart>();
            if (component != null && component.m_partType == partType)
            {
                return true;
            }
        }
        foreach (BasePart part in RoverPortCatalog.Instance.GetPartComponents())
        {
            if (part != null && part.m_partType == partType)
            {
                return true;
            }
        }
        if (LoadPartListData() != null)
        {
            foreach (GameObject part in s_partListData.Parts ?? new List<GameObject>())
            {
                var component = part == null ? null : part.GetComponent<BasePart>();
                if (component != null && component.m_partType == partType)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static void PrepareEditorServices()
    {
        typeof(Innovation.Json).GetProperty("Service").SetValue(null,
            Activator.CreateInstance(typeof(BasePart).Assembly.GetType("JsonService"), true));
        var resources = (Dictionary<(string, string), UnityEngine.Object>)typeof(INUnity)
            .GetField("s_resources", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        foreach (string name in new[] { "INDeclarationSettingsExp", "INAliasSettings" })
            resources[("TextAsset", name)] = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/TextAsset/" + name + ".json");
        foreach (string name in new[] { "UIPartButtonAtlas", "IngameAtlas" })
            resources[("Material", name)] = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/" + name + ".mat");
        typeof(INSettings).GetMethod("Load", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { 0 });
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
