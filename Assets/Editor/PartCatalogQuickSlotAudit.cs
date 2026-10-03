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

/// <summary>
/// Checks that the part catalog tabs get out of the quick part-variant panel's way.
/// </summary>
/// <remarks>
/// <para>
/// The two are stacked UI: "Part catalog tabs" sits just above the part tray, and the quick variant row
/// (INQuickVariants/QuickSlot) is anchored a little higher up the screen, so when the panel opens they
/// land on the same horizontal band - which both hides the tabs and makes their BoxColliders fight over
/// clicks. PartSelector raises the tabs while the panel is open and drops them back when it closes.
/// </para>
/// <para>
/// This runs in edit mode, on the real PartSelector from InGameBuildMenu.prefab with its real catalog
/// tabs, and drives the real UpdateCatalogTabs. Only the quick panel is stood in for: it is the part of
/// the pair that needs a running game to build, and what matters here is its shape and where it sits, so
/// the stand-in is a root holding QuickSlot children scaled and positioned like the ones
/// BuildQuickRow/CreateQuickSlot make, with the same background strip collider.
/// </para>
/// <para>
/// Run with:  Tools\unity-batch.bat -executeMethod PartCatalogQuickSlotAudit.Run
/// or, with the editor already open, through the DSH bridge (see Tools/StarSea12/run_audit_bridge.py).
/// Reports to Builds/QA/part-catalog-quickslot-audit.txt.
/// </para>
/// </remarks>
public static class PartCatalogQuickSlotAudit
{
    private const string ReportPath = "Builds/QA/part-catalog-quickslot-audit.txt";
    private const string ShotPath = "Builds/QA/part-catalog-quickslot.png";
    private const float Gap = 0.15f;   // must match PartSelector.Catalog.cs CatalogTabsQuickGap
    private const float QuickSlotScale = 0.46f;   // PartSelector.QuickBtnScale
    private const float QuickCurrentZoom = 1.28f; // PartSelector.QuickCurrentZoom

    private static readonly List<string> Errors = new List<string>();
    private static readonly StringBuilder Report = new StringBuilder();

    private static GameObject s_menu;
    private static PartSelector s_selector;
    private static Transform s_tabs;
    private static GameObject s_quickRoot;
    private static GameObject s_quickStrip;

    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        try
        {
            Check();
        }
        catch (Exception exception)
        {
            Errors.Add(exception.ToString());
        }
        Finish(null);
    }

    /// <summary>
    /// Entry point for a live editor driven over the DSH bridge: it leaves the scene the user has open
    /// alone, and the report is written without exiting the editor.
    /// </summary>
    public static void RunFromBridge()
    {
        try
        {
            Check();
        }
        catch (Exception exception)
        {
            Errors.Add(exception.ToString());
        }
        Finish(null);
    }

    private static void Check()
    {
        Build();
        Canvas.ForceUpdateCanvases();

        Report.AppendLine("screen " + Screen.width + "x" + Screen.height);
        Report.AppendLine("tray at " + TrayPosition() + ", quick row y " + F(QuickY()));

        // ---- closed: the tabs sit where they have always sat -----------------
        Refresh();
        float closedY = s_tabs.position.y;
        Report.AppendLine();
        Report.AppendLine("=== quick panel closed ===");
        Report.AppendLine("  tabs y " + F(closedY) + ", lift " + F(Lift()) + ", " + DescribeTabs());
        Check(Mathf.Abs(Lift()) < 1e-4f, "the tabs are at their home height while the quick panel is closed");

        // ---- open: the panel lands on the tabs' band -------------------------
        OpenQuickPanel();
        Refresh();
        float openY = s_tabs.position.y;
        Report.AppendLine();
        Report.AppendLine("=== quick panel open ===");
        Report.AppendLine("  tabs y " + F(openY) + " (was " + F(closedY) + "), lift " + F(Lift()));
        Report.AppendLine("  " + DescribeQuickRow() + ", " + DescribeTabs());
        Check(Clears(Gap), "the tabs clear the quick panel while it is open");
        Check(Lift() > 1e-4f && openY > closedY,
            "the tabs moved up for a quick panel that reaches their band");

        // ---- forced onto the tabs: they must still give way ------------------
        // The panel's height comes from m_quickY, so that is what has to move - the same field
        // ApplyQuickRootPos places the root from.
        float home = QuickY();
        Bounds tabs = TabsBounds();
        float rowHeight = Mathf.Max(0.2f, QuickBounds().size.y);
        float pushed = tabs.center.y - tabs.extents.y + rowHeight * 0.25f - rowHeight * 0.31f / 0.62f;
        Field("m_quickY").SetValue(s_selector, pushed);
        MoveQuickPanel(pushed);
        Refresh();
        float forcedY = s_tabs.position.y;
        Report.AppendLine();
        Report.AppendLine("=== quick panel pushed up onto the tabs ===");
        Report.AppendLine("  tabs y " + F(forcedY) + ", lift " + F(Lift()));
        Report.AppendLine("  " + DescribeQuickRow() + ", " + DescribeTabs());
        Check(Lift() > 1e-4f, "the tabs gave way to a quick panel sitting on them");
        Check(Clears(Gap), "the tabs clear a quick panel sitting on them");

        Capture(ShotPath);

        // ---- closed again: back to exactly where they were -------------------
        Field("m_quickY").SetValue(s_selector, home);
        CloseQuickPanel();
        Refresh();
        float restoredY = s_tabs.position.y;
        Report.AppendLine();
        Report.AppendLine("=== quick panel closed again ===");
        Report.AppendLine("  tabs y " + F(restoredY) + ", lift " + F(Lift()) + ", " + DescribeTabs());
        Check(Mathf.Abs(Lift()) < 1e-4f, "the tabs are back home after the quick panel closed");
        Check(Mathf.Abs(restoredY - closedY) < 1e-4f,
            "the tabs returned to exactly their original height");
    }

    // ---- building the pieces ---------------------------------------------

    private static void Build()
    {
        // PartSelector.Awake needs these two singletons, and it is not [ExecuteAlways], so Awake only
        // runs in play mode - everything here is driven explicitly instead.
        new GameObject("BuildCustomizationLoader").AddComponent<BuildCustomizationLoader>();
        GameData data = AssetDatabase.LoadAssetAtPath<GameData>("Assets/MonoBehaviour/GameData.asset");
        SetStatic(typeof(WPFMonoBehaviour), "s_gameData", data);

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        var cameraObject = new GameObject("HUDCamera");
        cameraObject.tag = "HUDCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 10f;
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 1000f;
        cameraObject.transform.position = new Vector3(-15.6272f, -0.1554938f, -100f);
        SetStatic(typeof(WPFMonoBehaviour), "s_hudCamera", camera);

        s_menu = UnityEngine.Object.Instantiate(
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/InGameBuildMenu.prefab"));
        s_selector = s_menu.GetComponentInChildren<PartSelector>(true);
        if (s_selector == null) { throw new InvalidOperationException("no PartSelector in InGameBuildMenu.prefab"); }

        // Awake does not run in edit mode, so the two things it wires up that EnsureCatalogTabs and
        // UpdateCatalogTabs need are wired here: the tray it measures from, and the button prefab.
        Transform trayObject = s_selector.transform.Find("ScrollList");
        if (trayObject == null) { throw new InvalidOperationException("no ScrollList under PartSelector"); }
        var list = trayObject.GetComponent<ExtendedScrollList>();
        if (list == null) { throw new InvalidOperationException("ScrollList has no ExtendedScrollList"); }
        list.SetListener(s_selector);
        Field("m_scrollList").SetValue(s_selector, list);
        if (s_selector.m_partButtonPrefab == null) { throw new InvalidOperationException("no m_partButtonPrefab"); }

        // EnsureCatalogTabs is what SetParts reaches on the way in. The part list itself is not needed to
        // measure the tabs - and it is not reachable here anyway: ReadPartOrder asks INSettings which
        // parts a version allows, and INSettings is only booted by the game.
        Invoke("EnsureCatalogTabs");
        s_tabs = (Transform)Field("m_catalogTabs").GetValue(s_selector);
        if (s_tabs == null) { throw new InvalidOperationException("PartSelector never built its catalog tabs"); }
        Report.AppendLine("tabs " + s_tabs.name + ", " + s_tabs.GetComponentsInChildren<Renderer>(true).Length + " renderers");
    }

    /// <summary>
    /// Stands in for BuildQuickRow/CreateQuickSlot: a root at the row's own y carrying the same number of
    /// QuickSlot shells, scaled like the real ones (the current skin is the big one, QuickCurrentZoom),
    /// plus the background strip that makes up the rest of the panel's footprint.
    /// </summary>
    private static void OpenQuickPanel()
    {
        float spriteHeight = s_selector.m_partButtonPrefab.GetComponent<Sprite>().Size.y;
        float step = 0.9f;
        int count = 6;

        s_quickRoot = new GameObject("INQuickVariants");
        s_quickRoot.transform.SetParent(s_selector.transform, false);
        var slots = new List<Transform>();
        for (int i = 0; i < count; i++)
        {
            var slot = new GameObject("QuickSlot");
            slot.transform.SetParent(s_quickRoot.transform, false);
            slot.transform.localPosition = new Vector3(i * step, 0f, 0f);
            // The slot that is showing the skin in use is the largest one, so the panel has to clear that.
            float factor = (i == 2) ? QuickCurrentZoom : 1f;
            slot.transform.localScale = Vector3.one * (QuickSlotScale * factor);

            var frame = new GameObject("Frame");
            frame.transform.SetParent(slot.transform, false);
            SpriteRenderer renderer = frame.AddComponent<SpriteRenderer>();
            renderer.sprite = WhiteSprite();
            frame.transform.localScale = new Vector3(spriteHeight, spriteHeight, 1f);
            // The real slot's footprint is its button collider, which is what the production code
            // measures and what would steal the tabs' clicks, so the stand-in carries one too.
            frame.AddComponent<BoxCollider>().size = new Vector3(1f, 1f, 0.1f);
            slots.Add(slot.transform);
        }
        s_quickRoot.transform.position = new Vector3(0f, QuickY(), 0f);

        s_quickStrip = new GameObject("QuickVariantsStrip");
        s_quickStrip.transform.SetParent(s_selector.transform, false);
        s_quickStrip.transform.position = new Vector3(0f, QuickY(), 0f);
        s_quickStrip.AddComponent<BoxCollider>().size = new Vector3(8f, 0.62f, 0.05f);
        Field("m_quickRoot").SetValue(s_selector, s_quickRoot.transform);
        Field("m_quickY").SetValue(s_selector, QuickY());
        Field("m_quickBgObj").SetValue(s_selector, s_quickStrip);
        var list = (List<Transform>)Field("m_quickSlotTfs").GetValue(s_selector);
        list.Clear();
        list.AddRange(slots);
    }

    /// <summary>Moves the stand-in panel, the way ApplyQuickRootPos does when m_quickY changes.</summary>
    private static void MoveQuickPanel(float y)
    {
        if (s_quickRoot != null) { s_quickRoot.transform.position = new Vector3(0f, y, 0f); }
        if (s_quickStrip != null) { s_quickStrip.transform.position = new Vector3(0f, y, 0f); }
    }

    private static void CloseQuickPanel()    {
        Field("m_quickRoot").SetValue(s_selector, null);
        Field("m_quickBgObj").SetValue(s_selector, null);
        ((List<Transform>)Field("m_quickSlotTfs").GetValue(s_selector)).Clear();
        if (s_quickRoot != null) { UnityEngine.Object.DestroyImmediate(s_quickRoot); s_quickRoot = null; }
        if (s_quickStrip != null) { UnityEngine.Object.DestroyImmediate(s_quickStrip); s_quickStrip = null; }
    }

    private static UnityEngine.Sprite WhiteSprite()
    {
        var texture = new Texture2D(2, 2);
        texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        texture.Apply();
        return UnityEngine.Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f, 2f);
    }

    /// <summary>The lift the production code applied, read straight off the field it stores it in.</summary>
    private static float Lift()
    {
        return (float)Field("m_catalogTabsQuickLift").GetValue(s_selector);
    }

    // ---- measurement (independent of the production helpers) --------------

    private static void Refresh()
    {
        Invoke("UpdateCatalogTabs");
    }

    private static bool Clears(float gap)
    {
        return TabsBounds().min.y >= QuickBounds().max.y + gap - 1e-4f;
    }

    private static Bounds TabsBounds() { return Footprint(s_tabs); }

    private static Bounds QuickBounds()
    {
        var bounds = new Bounds(new Vector3(0f, QuickY(), 0f), new Vector3(0.02f, 0.02f, 0.02f));
        if (s_quickRoot != null) { bounds.Encapsulate(Footprint(s_quickRoot.transform)); }
        if (s_quickStrip != null) { bounds.Encapsulate(s_quickStrip.GetComponent<Collider>().bounds); }
        return bounds;
    }

    /// <summary>
    /// The click footprint of a group: its enabled colliders, which is what the production code measures
    /// and what decides whether the two rows steal each other's clicks. Labels are deliberately left out -
    /// a TextMesh's bounds are far larger than the button it sits on.
    /// </summary>
    private static Bounds Footprint(Transform root)
    {
        var colliders = root.GetComponentsInChildren<Collider>(true).Where(c => c.enabled).ToArray();
        if (colliders.Length == 0) { return new Bounds(root.position, Vector3.zero); }
        Bounds bounds = colliders[0].bounds;
        for (int i = 1; i < colliders.Length; i++) { bounds.Encapsulate(colliders[i].bounds); }
        return bounds;
    }

    private static string DescribeTabs()
    {
        Bounds b = TabsBounds();
        return "tabs [" + F(b.min.y) + " .. " + F(b.max.y) + "]";
    }

    private static string DescribeQuickRow()
    {
        Bounds b = QuickBounds();
        return "quick [" + F(b.min.y) + " .. " + F(b.max.y) + "]";
    }

    private static float QuickY()
    {
        // What QuickAnchor settles on with the real HUD camera: viewport y = 0.265 of an 18-unit view.
        Camera camera = WPFMonoBehaviour.hudCamera;
        if (camera == null || !camera.orthographic) { return -4.85f; }
        return camera.transform.position.y + (0.265f - 0.5f) * 2f * camera.orthographicSize;
    }

    private static Vector3 TrayPosition()
    {
        var list = (MonoBehaviour)Field("m_scrollList").GetValue(s_selector);
        return list != null ? list.transform.position : Vector3.zero;
    }

    // ---- plumbing ---------------------------------------------------------

    private static void Check(bool condition, string message)
    {
        Report.AppendLine((condition ? "  PASS " : "  FAIL ") + message);
        if (!condition) { Errors.Add(message); }
    }

    private static FieldInfo Field(string name)
    {
        FieldInfo field = typeof(PartSelector).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null) { throw new InvalidOperationException("no field " + name + " on PartSelector"); }
        return field;
    }

    private static void Invoke(string name)
    {
        MethodInfo method = typeof(PartSelector).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .FirstOrDefault(m => m.Name == name && m.GetParameters().Length == 0);
        if (method == null) { throw new InvalidOperationException("no method " + name + "() on PartSelector"); }
        method.Invoke(s_selector, null);
    }

    private static void SetStatic(Type type, string name, object value)
    {
        FieldInfo field = type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if (field != null) { field.SetValue(null, value); return; }
        PropertyInfo property = type.GetProperty(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if (property != null) { property.SetValue(null, value); return; }
        throw new InvalidOperationException("no static member " + name + " on " + type.Name);
    }

    /// <summary>
    /// Renders the band the two share, so the report has a picture next to its numbers. The camera object
    /// is hidden and the scene is never saved, so this exists only for the length of the capture.
    /// </summary>
    private static void Capture(string path)
    {
        Bounds bounds = TabsBounds();
        bounds.Encapsulate(QuickBounds());
        // A small margin, so the gap the tabs leave reads as a gap in the picture rather than a few pixels.
        bounds.Expand(0.35f);

        var cameraObject = new GameObject("QA camera", typeof(Camera));
        cameraObject.hideFlags = HideFlags.HideAndDontSave;
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.15f, 0.2f);
        camera.orthographic = true;
        camera.orthographicSize = Mathf.Max(0.3f, bounds.extents.y);
        camera.aspect = 1600f / 900f * 0.5f;   // half as wide, so the rows fill the frame
        camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, -50f);
        Report.AppendLine("  capture frames y " + F(bounds.min.y) + " .. " + F(bounds.max.y) +
            " (ortho size " + F(camera.orthographicSize) + ")");

        RenderTexture target = new RenderTexture(1600, 900, 24);
        camera.targetTexture = target;
        camera.Render();
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
        image.Apply();
        Directory.CreateDirectory("Builds/QA");
        File.WriteAllBytes(path, image.EncodeToPNG());
        Report.AppendLine("  captured " + path);
        RenderTexture.active = previous;
        camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(image);
        UnityEngine.Object.DestroyImmediate(target);
        UnityEngine.Object.DestroyImmediate(cameraObject);
    }

    private static string F(float value) { return value.ToString("0.####"); }

    private static void Finish(string error)
    {
        if (!string.IsNullOrEmpty(error)) { Errors.Add(error); }
        Directory.CreateDirectory("Builds/QA");
        Report.AppendLine();
        Report.AppendLine(Errors.Count == 0 ? "PART_CATALOG_QUICKSLOT_PASS" : "PART_CATALOG_QUICKSLOT_FAIL");
        foreach (string failure in Errors) { Report.AppendLine("  ! " + failure); }
        File.WriteAllText(ReportPath, Report.ToString());
        if (Errors.Count == 0) { Debug.Log("PART_CATALOG_QUICKSLOT_PASS"); }
        else { Debug.LogError("PART_CATALOG_QUICKSLOT_FAIL: " + string.Join(" | ", Errors)); }
        if (Application.isBatchMode) { EditorApplication.Exit(Errors.Count == 0 ? 0 : 1); }
    }
}
