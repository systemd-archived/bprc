using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Validates the 优化设置 category end to end without entering play mode: the page prefab, its
/// registration in the app-interface panel, every property name the page lists, and every locale id
/// those rows and headings ask for.
/// </summary>
/// <remarks>
/// <para>
/// The page is assembled from string names at runtime - <c>GenerateStarSeaGroup</c> silently
/// <c>continue</c>s past a property that does not exist, and <c>SetLocale</c> writes an id whether or not
/// the string table has it - so a typo produces a page that is merely missing a row, with nothing in the
/// console. This audit is what makes such a typo visible.
/// </para>
/// <para>
/// Batch-runnable, and callable through the DSH bridge with
/// <c>invoke_static OptimizationSettingsAudit.Run</c>:
/// <code>
/// Tools\unity-batch.bat -executeMethod OptimizationSettingsAudit.Run
/// </code>
/// </para>
/// </remarks>
public static class OptimizationSettingsAudit
{
    private const string PanelPath = "Assets/GameObject/INApplicationInterface.prefab";
    private const string PagePath = "Assets/GameObject/OptimizationSettingsInterface.prefab";
    private const string TemplatePath = "Assets/GameObject/StarSeaSettingsInterface.prefab";
    private const string LocalizationPath = "Assets/TextAsset/INLocalizationData.json";
    private const string PageKey = "OptimizationSettings";

    /// <summary>Must mirror the groups <c>INSettingsInterface.GenerateGroupList</c> builds for this page.</summary>
    private static readonly (string LocaleId, string[] Properties)[] Groups =
    {
        ("StarSeaSettings_OptimizationBackend", new[] { "PhysicsEngine", "PhysicsSolverIterations", "PhysicsWorkerCount", "GpuPhysicsCompute" }),
        ("StarSeaSettings_OptimizationTimestep", new[] { "TargetFrameRate", "CustomPhysicsFrame", "FixedFrameRate", "AdaptivePhysicsFrame", "AdaptivePhysicsFrameMin" }),
        ("StarSeaSettings_OptimizationThreading", new[] { "UseMultithreadFrameSolver", "EnableMultithreadCircuit", "OptimizedCircuit", "FrameFrameRate", "CircuitFrameRate" }),
        ("StarSeaSettings_OptimizationDiagnostics", new[] { "ShowFPS", "ShowPhysicsStats" }),
    };

    /// <summary>
    /// Rows that must no longer appear on the 星海设置 page, because 优化设置 is now their only home.
    /// </summary>
    /// <remarks>
    /// A property listed on two pages shows one value and can be edited from either; that reads as a bug,
    /// so this asserts the older listing was actually removed rather than merely supplemented.
    /// </remarks>
    private static readonly string[] RetiredFromStarSeaPage =
    {
        "PhysicsEngine", "PhysicsSolverIterations", "TargetFrameRate", "CustomPhysicsFrame",
        "AdaptivePhysicsFrame", "AdaptivePhysicsFrameMin", "FrameFrameRate",
        "UseMultithreadFrameSolver", "EnableMultithreadCircuit", "OptimizedCircuit",
        "ShowFPS", "FixedFrameRate", "CircuitFrameRate", "GpuPhysicsCompute",
    };

    private static readonly List<string> Report = new List<string>();
    private static int failures;

    private static void Check(string name, bool ok, string detail)
    {
        Report.Add((ok ? "PASS  " : "FAIL  ") + name.PadRight(56) + detail);
        if (!ok) failures++;
    }

    public static void Run()
    {
        failures = 0;
        Report.Clear();
        Report.Add("优化设置 category audit - Unity " + Application.unityVersion);

        CheckPagePrefab();
        CheckPanelRegistration();
        CheckProperties();
        CheckLocales();
        CheckNoDuplicatesOnStarSeaPage();
        CheckSettingsClassesAreReflectable();

        Report.Add("");
        Report.Add(failures == 0 ? "FAILURES: 0" : "FAILURES: " + failures);
        Directory.CreateDirectory("Builds/QA");
        File.WriteAllLines("Builds/QA/optimization-settings.txt", Report);
        foreach (string line in Report) Debug.Log("[opt-audit] " + line);
        // Only a batch run may terminate the process: this method is also reachable through the DSH
        // bridge, where an exit would close the editor the user is working in.
        if (failures != 0 && Application.isBatchMode) EditorApplication.Exit(1);
    }

    private static void CheckPagePrefab()
    {
        var page = AssetDatabase.LoadAssetAtPath<GameObject>(PagePath);
        Check("page prefab exists", page != null, PagePath);
        if (page == null) return;

        Check("page root keeps the expected name", page.name == "OptimizationSettingsInterface", page.name);

        var settings = page.GetComponent<INSettingsInterface>();
        Check("page carries INSettingsInterface", settings != null, settings == null ? "missing" : "found");
        if (settings == null) return;

        var so = new SerializedObject(settings);
        var key = so.FindProperty("m_onlyGroupName");
        Check("m_onlyGroupName is " + PageKey, key != null && key.stringValue == PageKey,
            key == null ? "property missing" : "'" + key.stringValue + "'");

        Check("m_content wired", so.FindProperty("m_content")?.objectReferenceValue != null,
            Describe(so, "m_content"));
        Check("m_groupTemplate wired", so.FindProperty("m_groupTemplate")?.objectReferenceValue != null,
            Describe(so, "m_groupTemplate"));
        Check("m_itemTemplate wired", so.FindProperty("m_itemTemplate")?.objectReferenceValue != null,
            Describe(so, "m_itemTemplate"));
        Check("save/reset buttons wired",
            so.FindProperty("m_saveButton")?.objectReferenceValue != null &&
            so.FindProperty("m_resetButton")?.objectReferenceValue != null,
            "save=" + (so.FindProperty("m_saveButton")?.objectReferenceValue != null) +
            " reset=" + (so.FindProperty("m_resetButton")?.objectReferenceValue != null));

        var template = AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePath);
        Check("page differs from its template", template != null && template != page, TemplatePath);
    }

    private static string Describe(SerializedObject so, string path)
    {
        var property = so.FindProperty(path);
        return property == null ? "property missing" : (property.objectReferenceValue == null ? "null" : "set");
    }

    private static void CheckPanelRegistration()
    {
        var panel = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
        Check("panel prefab exists", panel != null, PanelPath);
        if (panel == null) return;

        var app = panel.GetComponent<INAppInterface>();
        var sidebar = panel.GetComponentInChildren<AppInterfaceSidebar>(true);
        Check("panel has INAppInterface", app != null, app == null ? "missing" : "found");
        Check("panel has AppInterfaceSidebar", sidebar != null, sidebar == null ? "missing" : "found");
        if (app == null || sidebar == null) return;

        var page = AssetDatabase.LoadAssetAtPath<GameObject>(PagePath);
        var appSo = new SerializedObject(app);
        var elements = appSo.FindProperty("m_elements");
        var sidebarSo = new SerializedObject(sidebar);
        var entries = sidebarSo.FindProperty("m_sidebarElements");

        Check("m_elements and m_sidebarElements have equal length",
            elements != null && entries != null && elements.arraySize == entries.arraySize,
            "elements=" + (elements?.arraySize ?? -1) + " sidebar=" + (entries?.arraySize ?? -1));

        if (elements == null || entries == null) return;

        int elementIndex = -1, sidebarIndex = -1;
        for (int i = 0; i < elements.arraySize; i++)
        {
            if (elements.GetArrayElementAtIndex(i).objectReferenceValue == page) { elementIndex = i; break; }
        }
        for (int i = 0; i < entries.arraySize; i++)
        {
            var related = entries.GetArrayElementAtIndex(i).FindPropertyRelative("RelatedObject");
            if (related != null && related.objectReferenceValue == page) { sidebarIndex = i; break; }
        }
        Check("page is registered in m_elements", elementIndex >= 0, "index=" + elementIndex);
        Check("page is registered in m_sidebarElements", sidebarIndex >= 0, "index=" + sidebarIndex);
        Check("the two lists agree on the position", elementIndex >= 0 && elementIndex == sidebarIndex,
            "elements=" + elementIndex + " sidebar=" + sidebarIndex);

        if (sidebarIndex >= 0)
        {
            var name = entries.GetArrayElementAtIndex(sidebarIndex).FindPropertyRelative("Name");
            Check("sidebar entry names the locale id", name != null && name.stringValue == "AppInterface_Optimization",
                name == null ? "missing" : "'" + name.stringValue + "'");
        }

        for (int i = 0; i < elements.arraySize; i++)
        {
            if (elements.GetArrayElementAtIndex(i).objectReferenceValue != null) continue;
            Check("no null page at index " + i, false, "m_elements[" + i + "] is null");
            return;
        }
        Check("no null page references", true, elements.arraySize + " pages");
    }

    private static void CheckProperties()
    {
        var settings = typeof(StarSeaSettings);
        SetLocaleProbe();
        int total = 0, missing = 0;
        foreach (var group in Groups)
        {
            foreach (string name in group.Properties)
            {
                total++;
                PropertyInfo property = settings.GetProperty(name);
                if (property == null)
                {
                    missing++;
                    Report.Add("      missing property: StarSeaSettings." + name + " (group " + group.LocaleId + ")");
                }
            }
        }
        Check("every listed property exists on StarSeaSettings", missing == 0,
            total + " checked, " + missing + " missing");

        // The row label the panel derives from a property is type.Name + "_" + propertyName.
        var expected = new List<string>();
        foreach (var group in Groups)
            foreach (string name in group.Properties)
                expected.Add("StarSeaSettings_" + name);
        _ = expected;
    }

    private static void SetLocaleProbe() { }

    /// <summary>
    /// Every public property of every <see cref="SettingsBase"/> subclass must be readable <i>and</i>
    /// writable.
    /// </summary>
    /// <remarks>
    /// This is the invariant the reflected settings pages depend on, and it is not obvious: a page that
    /// lists a whole settings class turns each public property into a row, and
    /// <c>SettingsItem.Bind</c> passes <c>PropertyInfo.GetSetMethod()</c> to
    /// <c>Delegate.CreateDelegate</c>. A read-only property makes that method <c>null</c> and the page
    /// throws <c>ArgumentNullException</c> mid-build - which is exactly what a get-only
    /// <c>ResolvedPhysicsWorkerCount</c> did to the full settings window before this check existed.
    /// </remarks>
    private static void CheckSettingsClassesAreReflectable()
    {
        var types = new List<Type>();
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] candidates;
            try { candidates = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException partial) { candidates = partial.Types; }
            catch (Exception) { continue; }
            foreach (Type type in candidates)
            {
                if (type == null || type.IsAbstract || !typeof(SettingsBase).IsAssignableFrom(type)) continue;
                types.Add(type);
            }
        }

        Check("found the settings classes", types.Count > 0, types.Count + " classes");

        int offenders = 0, scanned = 0;
        foreach (Type type in types)
        {
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                scanned++;
                if (property.GetGetMethod() != null && property.GetSetMethod() != null) continue;
                offenders++;
                Report.Add("      not read/write: " + type.Name + "." + property.Name +
                    " (get=" + (property.GetGetMethod() != null) + " set=" + (property.GetSetMethod() != null) + ")");
            }
        }
        Check("every public settings property is read/write", offenders == 0,
            scanned + " scanned, " + offenders + " unreflectable");
    }

    /// <summary>
    /// Reads the star-sea branch of <c>GenerateGroupList</c> straight out of the source file and asserts
    /// the retired rows are gone from it.
    /// </summary>
    /// <remarks>
    /// Source inspection rather than reflection because the group definitions are local variables inside a
    /// method - there is nothing at runtime to query. This is the same technique the repo's other static
    /// audits use, and it fails loudly if a future edit re-adds a row to both pages.
    /// </remarks>
    private static void CheckNoDuplicatesOnStarSeaPage()
    {
        const string sourcePath = "Assets/Scripts/Assembly-CSharp/INSettingsInterface.cs";
        var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(sourcePath);
        Check("settings interface source loads", asset != null, sourcePath);
        if (asset == null) return;

        string source = asset.text;
        // The star-sea branch is the region between its marker and the optimization branch, which is
        // searched for *after* the marker: the constant that also carries that name is declared far
        // earlier in the file, and matching it would invert the slice.
        int start = source.IndexOf("m_onlyGroupName == \"StarSeaSettings\"", StringComparison.Ordinal);
        int end = start < 0 ? -1 : source.IndexOf("OptimizationGroupName", start, StringComparison.Ordinal);
        Check("found the star-sea branch", start >= 0 && end > start,
            "start=" + start + " end=" + end);
        if (start < 0 || end <= start) return;
        string branch = source.Substring(start, end - start);
        // Only the group listings matter; strip the explanatory comment block so a property *named* in a
        // comment does not read as a live row.
        int comment = branch.IndexOf("// The former Performance group", StringComparison.Ordinal);
        string listings = comment > 0 ? branch.Substring(0, comment) : branch;

        int stillListed = 0;
        foreach (string name in RetiredFromStarSeaPage)
        {
            if (!listings.Contains("\"" + name + "\"")) continue;
            stillListed++;
            Report.Add("      still on the star-sea page: " + name);
        }
        Check("retired rows are gone from the star-sea page", stillListed == 0,
            RetiredFromStarSeaPage.Length + " checked, " + stillListed + " still listed");
    }

    private static void CheckLocales()
    {
        var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(LocalizationPath);
        Check("localization asset loads", asset != null, LocalizationPath);
        if (asset == null) return;
        string json = asset.text;

        var required = new List<string> { "AppInterface_Optimization" };
        foreach (var group in Groups) required.Add(group.LocaleId);
        foreach (var group in Groups)
            foreach (string name in group.Properties)
                required.Add("StarSeaSettings_" + name);

        int missing = 0;
        foreach (string id in required)
        {
            if (json.Contains("\"" + id + "\"")) continue;
            missing++;
            Report.Add("      missing locale id: " + id);
        }
        Check("every id the page asks for is in the string table", missing == 0,
            required.Count + " checked, " + missing + " missing");

        // Both languages must be present for the ids this change introduced.
        foreach (string id in new[] { "AppInterface_Optimization", "StarSeaSettings_PhysicsWorkerCount", "StarSeaSettings_ShowPhysicsStats", "GeneralSettings_NoGridMode", "StarSeaSettings_GpuPhysicsCompute" })
        {
            int at = json.IndexOf("\"" + id + "\"", StringComparison.Ordinal);
            if (at < 0) { Check("locale " + id + " has both languages", false, "id missing"); continue; }
            string slice = json.Substring(at, Math.Min(320, json.Length - at));
            Check("locale " + id + " has both languages",
                slice.Contains("\"en-US\"") && slice.Contains("\"zh-Hans\""), "en-US + zh-Hans");
        }

        // 通用设置 is built by reflecting every public property of GeneralSettings, so each one becomes a
        // row whose label is GeneralSettings_<name>. A property added without its locale id renders as a
        // blank row rather than failing, which is easy to miss.
        foreach (var property in typeof(GeneralSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.IsDefined(typeof(ObsoleteAttribute), inherit: true)) continue;
            string id = "GeneralSettings_" + property.Name;
            bool present = json.Contains("\"" + id + "\"");
            Check("通用设置 row has a label: " + property.Name, present, present ? id : "MISSING " + id);
        }
    }
}
