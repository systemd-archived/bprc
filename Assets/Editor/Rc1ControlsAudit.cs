using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Verifies the 「RC1 操作」 feature: the 通用设置 row, and the pedal / throttle panel it turns on.
/// </summary>
/// <remarks>
/// <para>
/// What can be checked headlessly is checked here: the feature resolves through INSettings with the
/// right default, writing it through <c>GeneralSettings.Rc1Controls</c> reaches the same value the
/// runtime reads, the locale and alias rows exist, the panel builds all three controls from the art cut
/// out of RC1.apk, and nothing drives anything while the panel is not up.
/// </para>
/// <para>
/// What this cannot check is a contraption actually being driven by the pedals - that needs a running
/// level. <see cref="INRc1Controls.Active"/> is the single gate every consumer reads, so the check that
/// it is false with no contraption, and that the gyro therefore adds no torque, is what stands in for it.
/// </para>
/// <para>
/// Run with:  Tools\unity-batch.bat -executeMethod Rc1ControlsAudit.Run
/// or through the DSH bridge (Tools/StarSea12/run_audit_bridge.py --method Rc1ControlsAudit.RunFromBridge).
/// </para>
/// </remarks>
public static class Rc1ControlsAudit
{
    private const string ReportPath = "Builds/QA/rc1-controls-audit.txt";

    private static readonly List<string> Errors = new List<string>();
    private static readonly StringBuilder Report = new StringBuilder();

    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Check();
        Finish();
    }

    /// <summary>Entry point for the already-open editor: it leaves the scene alone.</summary>
    public static void RunFromBridge()
    {
        Check();
        Finish();
    }

    private static void Check()
    {
        Boot();
        CheckFeatureRow();
        CheckLocalization();
        CheckPanel();
        CheckGyro();
    }

    private static void Boot()
    {
        // INSettings reads its declaration files through this service; without it Load throws inside
        // Innovation.Json.Deserialize.
        Innovation.Json.Service = (Innovation.IJsonService)Activator.CreateInstance(
            typeof(INSettings).Assembly.GetType("JsonService"));

        new GameObject("QA game services", typeof(INGameManager));
        var table = typeof(INUnity).GetField("s_resources", BindingFlags.Static | BindingFlags.NonPublic)
            .GetValue(null) as System.Collections.IDictionary;
        if (table != null) { table.Clear(); }
        INUnity.Initialize(AssetDatabase.LoadAssetAtPath<ResourceData>("Assets/MonoBehaviour/ResourceData.asset"));
        SetStatic(typeof(INUnity), "SettingsPath", Path.GetFullPath("Builds/QA/IsolatedSettings"));
        SetStatic(typeof(INUnity), "DataPath", Path.GetFullPath("Builds/QA/IsolatedSettings"));
        Directory.CreateDirectory(INUnity.SettingsPath);

        // The declaration and settings files live in Assets/TextAsset, which ResourceData does not
        // necessarily list, so register every one of them the way the other audits here do.
        var resources = (Dictionary<(string, string), UnityEngine.Object>)typeof(INUnity)
            .GetField("s_resources", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        foreach (string guid in AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets/TextAsset" }))
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null) resources[("TextAsset", asset.name)] = asset;
        }

        typeof(UserSettings).GetField("m_data", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null,
            new SettingsData(Path.Combine(INUnity.SettingsPath, "Settings.xml"), false, string.Empty));
        string saved = Path.Combine(INUnity.SettingsPath, "INFeatureSettings.json");
        if (File.Exists(saved)) { File.Delete(saved); }
        typeof(INSettings).GetMethod("Load", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { 3 });
        INLocalization.Create();
        SetStatic(typeof(INUserSettings), "Default", new INUserSettings(INUnity.Version));
        SetStatic(typeof(INUserSettings), "Instance", new INUserSettings(INUnity.Version));
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    /// <summary>The row exists, defaults to off, and the settings proxy is the same value as the flag.</summary>
    private static void CheckFeatureRow()
    {
        Report.AppendLine("=== 通用设置 row");
        bool declared;
        try
        {
            declared = INSettings.GetBool(INFeature.Rc1Controls);
        }
        catch (Exception exception)
        {
            Errors.Add("INFeature.Rc1Controls is not declared in INDeclarationSettingsExp.json: " + exception.Message);
            return;
        }
        Report.AppendLine("  default: " + declared);
        Check(!declared, "RC1 操作 defaults to off");

        GeneralSettings settings = INUserSettings.Instance.GeneralSettings;
        Check(settings != null, "通用设置 is reachable through INUserSettings");
        if (settings == null) return;

        settings.Rc1Controls = true;
        bool on = settings.Rc1Controls;
        bool flag = INSettings.GetBool(INFeature.Rc1Controls);
        Report.AppendLine("  after switching it on: proxy " + on + ", INFeature " + flag);
        Check(on && flag, "switching the row on is what the runtime reads");

        settings.Rc1Controls = false;
        Check(!settings.Rc1Controls && !INSettings.GetBool(INFeature.Rc1Controls),
            "switching the row off is what the runtime reads");
        Report.AppendLine("  a version's default comes from INSettings{A,B,O}Exp.json and the value lives " +
                          "in INFeatureSettings.json, like the other rows");
    }

    private static void CheckLocalization()
    {
        var asset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/TextAsset/INLocalizationData.json");
        Check(asset != null, "INLocalizationData.json is loaded");
        if (asset == null) return;
        Check(asset.text.Contains("\"GeneralSettings_Rc1Controls\""),
            "the row has a locale entry (GeneralSettings_Rc1Controls)");
        Check(INLocalization.Instance != null, "INLocalization is up");

        var aliases = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/TextAsset/INAliasSettings.json");
        Check(aliases != null && aliases.text.Contains("Rc1Controls"),
            "the feature page has an alias for Rc1Controls");
    }

    /// <summary>The panel builds the three controls out of the sprites taken from RC1.apk.</summary>
    private static void CheckPanel()
    {
        Report.AppendLine();
        Report.AppendLine("=== run-screen controls");

        var host = new GameObject("INRc1Controls (QA)");
        INRc1Controls controls = host.AddComponent<INRc1Controls>();
        // Awake does not run for a component added in edit mode, and its DontDestroyOnLoad cannot run
        // outside play mode either, so the panel is built through the method Awake calls.
        Invoke(controls, "BuildPanel");

        foreach (string name in new[] { "Forward", "Back", "Throttle" })
        {
            Transform child = host.transform.Find("Panel/" + name);
            Check(child != null, "the panel has a " + name + " control");
            if (child == null) continue;
            Image image = child.GetComponent<Image>();
            Check(image != null && image.sprite != null,
                name + " loads its sprite from Resources/RC1Controls");
            if (image != null && image.sprite != null)
            {
                RectTransform rect = (RectTransform)child;
                float spriteAspect = (float)image.sprite.rect.width / image.sprite.rect.height;
                float rectAspect = rect.sizeDelta.x / rect.sizeDelta.y;
                Report.AppendLine($"  {name,-9} sprite {image.sprite.name} " +
                                  $"{image.sprite.rect.width}x{image.sprite.rect.height}, " +
                                  $"control {rect.sizeDelta.x}x{rect.sizeDelta.y}");
                Check(Mathf.Abs(spriteAspect - rectAspect) < 0.01f,
                    name + " keeps the source aspect ratio (non power-of-two art must not be rescaled)");
            }
        }

        // The sprites are non-POT; Unity's default nPOTScale would have rescaled them at import.
        foreach (string file in new[] { "btn_gas", "btn_brake", "btn_turbo" })
        {
            string path = "Assets/Resources/RC1Controls/" + file + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Check(importer != null, file + ".png is imported");
            if (importer == null) continue;
            Check(importer.textureType == TextureImporterType.Sprite, file + " is imported as a Sprite");
            Check(importer.npotScale == TextureImporterNPOTScale.None, file + " keeps its non-POT size");
        }

        Report.AppendLine();
        Report.AppendLine("=== gating");
        Report.AppendLine("  INRc1Controls.Enabled " + INRc1Controls.Enabled +
                          ", Active " + INRc1Controls.Active +
                          ", DriveAxis " + INRc1Controls.DriveAxis +
                          ", Throttle " + INRc1Controls.Throttle);
        Check(!INRc1Controls.Active, "the controls are not active without a running contraption");
        Check(INRc1Controls.DriveAxis == 0f && INRc1Controls.Throttle == 0f,
            "an inactive panel reports no pedal and no throttle");
        Transform panel = host.transform.Find("Panel");
        Check(panel != null && !panel.gameObject.activeSelf,
            "the panel is hidden while it is not driving");

        UnityEngine.Object.DestroyImmediate(host);
    }

    /// <summary>
    /// The gyro is inert while the panel is not up. Its source numbers are asserted against the APK's
    /// own PartSPrGyro.properties (max 220, power 90).
    /// </summary>
    private static void CheckGyro()
    {
        Report.AppendLine();
        Report.AppendLine("=== gyro");
        Check(Mathf.Abs(RCR2Port.RoverPortGyro.SourceMaxAngularVelocity - 220f) < 0.001f &&
              Mathf.Abs(RCR2Port.RoverPortGyro.SourceTorque - 90f) < 0.001f,
            "the gyro authors RC1.apk's own PartSPrGyro.properties (max 220, power 90)");

        var host = new GameObject("gyro (QA)");
        Rigidbody body = host.AddComponent<Rigidbody>();
        body.useGravity = false;
        RCR2Port.RoverPortGyro gyro = host.AddComponent<RCR2Port.RoverPortGyro>();
        float before = body.angularVelocity.z;
        Invoke(gyro, "FixedUpdate");
        Check(Mathf.Abs(body.angularVelocity.z - before) < 0.0001f,
            "the gyro adds no torque while the RC1 controls are not up");
        Report.AppendLine("  220 deg/s ceiling -> " + (220f * Mathf.Deg2Rad).ToString("0.####") +
                          " rad/s, which is the unit this project's bodies use");
        UnityEngine.Object.DestroyImmediate(host);

        // The gyro used to come out of the catalog as a powered part; the mapping still has to hold.
        var catalog = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/RC1Ported/rc1_catalog.json");
        Check(catalog != null, "the RC1 catalog is loadable");
        if (catalog != null)
        {
            var data = JsonUtility.FromJson<RCR2Port.RoverPortCatalogData>(catalog.text);
            int parts = data != null && data.parts != null ? data.parts.Length : 0;
            int gyros = 0;
            if (data != null && data.parts != null)
            {
                foreach (RCR2Port.RoverPortPartData entry in data.parts)
                {
                    if (string.Equals(entry.category, "gyroscope", StringComparison.Ordinal)) gyros++;
                }
            }
            Report.AppendLine("  RC1 catalog: " + parts + " parts, " + gyros + " gyroscope part(s)");
            Check(parts == 32, "the RC1 catalog still carries its 32 parts");
            Check(gyros == 1, "the RC1 catalog still carries its one gyroscope part");
        }
    }

    // ---- plumbing ---------------------------------------------------------

    private static void Check(bool condition, string message)
    {
        Report.AppendLine((condition ? "  PASS " : "  FAIL ") + message);
        if (!condition) { Errors.Add(message); }
    }

    private static void Invoke(object target, string name)
    {
        MethodInfo method = target.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .FirstOrDefault(m => m.Name == name && m.GetParameters().Length == 0);
        if (method == null) { throw new InvalidOperationException("no " + name + "() on " + target.GetType().Name); }
        method.Invoke(target, null);
    }

    private static void SetStatic(Type type, string name, object value)
    {
        FieldInfo field = type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if (field != null) { field.SetValue(null, value); return; }
        PropertyInfo property = type.GetProperty(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if (property != null) { property.SetValue(null, value); return; }
        throw new InvalidOperationException("no static member " + name + " on " + type.Name);
    }

    private static void Finish()
    {
        Directory.CreateDirectory("Builds/QA");
        Report.AppendLine();
        Report.AppendLine(Errors.Count == 0 ? "RC1_CONTROLS_PASS" : "RC1_CONTROLS_FAIL");
        foreach (string failure in Errors) { Report.AppendLine("  ! " + failure); }
        File.WriteAllText(ReportPath, Report.ToString());
        if (Errors.Count == 0) { Debug.Log("RC1_CONTROLS_PASS"); }
        else { Debug.LogError("RC1_CONTROLS_FAIL: " + string.Join(" | ", Errors)); }
        if (Application.isBatchMode) { EditorApplication.Exit(Errors.Count == 0 ? 0 : 1); }
    }
}
