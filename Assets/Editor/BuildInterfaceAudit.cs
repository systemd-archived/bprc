using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Isolated regression tests for the actual build UI and pointer controller.</summary>
public static partial class BuildInterfaceAudit
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private const BindingFlags Static = BindingFlags.NonPublic | BindingFlags.Static;
    private static readonly List<GameObject> objects = new List<GameObject>();
    private static readonly List<string> report = new List<string>();
    private static readonly List<string> unityErrors = new List<string>();
    private static int failures;
    private static INUserSettings settings;
    private static Camera camera;
    private static Contraption machine;
    private static INMachineBuildEditor editor;
    private static void Check(bool ok, string name) { report.Add((ok ? "PASS " : "FAIL ") + name); if (!ok) failures++; }
    private static bool Near(Vector2 a, Vector2 b) => (a - b).sqrMagnitude < 0.00001f;
    private static GameObject New(string name, bool active = false)
    {
        var go = new GameObject(name); go.SetActive(active); objects.Add(go); return go;
    }
    private static BasePart Part(int x = 0, int y = 0, Contraption c = null)
    {
        var part = New("AuditPart").AddComponent<BasePart>();
        part.m_coordX = x; part.m_coordY = y; part.m_partType = BasePart.PartType.WoodenFrame;
        if (c != null) { part.transform.SetParent(c.transform, false); part.contraption = c; c.Parts.Add(part); }
        return part;
    }
    private static void Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
    private static void Case(string label, Action action)
    {
        try { action(); } catch (Exception e) { Check(false, label + ": " + (e.InnerException ?? e)); }
    }
    private static void OnUnityLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) unityErrors.Add(message);
    }
    public static void Run()
    {
        if (!Application.isBatchMode || (!File.Exists(".bprc-build-interface-audit") && !File.Exists(".bprc-physics-audit"))) throw new InvalidOperationException("Run in the isolated audit project.");
        report.Clear(); unityErrors.Clear(); failures = 0;
        Application.logMessageReceived += OnUnityLog;
        report.Add("Build interface audit / Unity " + Application.unityVersion);
        var previous = INUserSettings.Instance;
        report.Add("Run UTC " + DateTime.UtcNow.ToString("O"));
        try
        {
            settings = new INUserSettings { GeneralSettings = new GeneralSettings(), StarSeaSettings = (StarSeaSettings)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(StarSeaSettings)) };
            typeof(INUserSettings).GetField("<Instance>k__BackingField", Static).SetValue(null, settings);
            settings.StarSeaSettings.EnableParamPanel = true;
            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/AuditFont.otf");
            if (font != null) typeof(INUiUtil).GetField("s_font", Static).SetValue(null, font);
            var level = New("AuditLevel").AddComponent<LevelManager>();
            typeof(LevelManager).GetField("m_gameState", Private).SetValue(level, LevelManager.GameState.Building);
            typeof(WPFMonoBehaviour).GetField("s_levelManager", Static).SetValue(null, level);
            camera = New("AuditCamera", true).AddComponent<Camera>(); camera.tag = "MainCamera";
            camera.orthographic = true; camera.orthographicSize = 5.4f; camera.transform.position = new Vector3(3, 2, -10);
            camera.backgroundColor = new Color(0.27f, 0.34f, 0.30f); camera.clearFlags = CameraClearFlags.SolidColor;
            typeof(WPFMonoBehaviour).GetField("s_mainCamera", Static).SetValue(null, camera);
            machine = New("AuditMachine").AddComponent<Contraption>();
            Call(machine, "PostAwake");
            var mode = new BaseGameMode();
            typeof(GameMode).GetProperty("ContraptionProto").SetValue(mode, machine);
            typeof(LevelManager).GetField("m_gameMode", Private).SetValue(level, mode);
            Part(0, 1, machine); Part(1, 1, machine); Part(2, 1, machine); Part(1, 2, machine);
            machine.RefreshPartPositions();
            editor = New("AuditMachineEditor", true).AddComponent<INMachineBuildEditor>(); Call(editor, "Awake");
            editor.HandlePointer(machine, new GuiManager.Pointer(), 0, false, false);
            Case("axis scale", Scale);
            Case("wheel scale", Wheels);
            Case("persistent pose", Pose);
            Case("build-to-runtime pose", RuntimePose);
            Case("special part rotations", SpecialRotations);
            Case("magnet eight-way rotation", () => MagnetRotationAudit.Verify(Check));
            Case("proximity snapping", Snapping);
            Case("independent free-build groups", MultipleGroups);
            Case("connection-circle group merge", GroupMerge);
            Case("group merge pointer and undo", GroupMergeInput);
            Case("merged runtime connections", GroupMergeRuntime);
            Case("ported connections after merge and rotation", PortMergeConnections);
            Case("selection extraction isolation and remerge", SelectionIsolation);
            Case("selection extraction persisted identities", () => SelectionExtractAudit.Verify(Check));
            Case("RC1 and native controls", Rc1Interactions);
            Case("resolution UI scaling", UiScaling);
            Case("textureless unmanaged sprites", TexturelessSprites);
            Case("toolbar scene lifecycle", ToolbarLifecycle);
            Case("optional layer toolbar", LayerToolbar);
            Case("runtime allocation stability", RuntimeAllocations);
            Case("toolbar selection and right-button deletion", Pointer);
            Case("Windows deletion radius", Deletion);
            Case("same-cell deletion ownership", DeletionOwnership);
            Case("same-cell deletion inventory", DeletionInventory);
            Case("gizmo translate/rotate/undo", Gizmo);
            Case("toolbar toggle deselection", ToggleSelection);
            Case("single-part transform tools", PartTools);
            Case("single-part enclosure moves", EnclosedPartTools);
            Case("native fine-angle composition", NativeFineAngles);
            Case("state-encoded part rotation", StateEncodedRotation);
            Case("machine translate boundary", TranslateBoundary);
            Case("nonmodal panel", Panel);
            Case("panel click ownership", PanelInputOwnership);
            Case("Android idle and multitouch pointers", AndroidTouchPointers);
            Case("grid visibility", GridVisibility);
            Case("visual render", Render);
            Case("connection merge visual render", RenderMerge);
        }
        finally
        {
            INPartParameterPanel.Close();
            BuildToolPalette.Clear();
            foreach (var go in objects) if (go) Object.DestroyImmediate(go);
            objects.Clear();
            typeof(INUserSettings).GetField("<Instance>k__BackingField", Static).SetValue(null, previous);
            typeof(WPFMonoBehaviour).GetField("s_levelManager", Static).SetValue(null, null);
            typeof(WPFMonoBehaviour).GetField("s_mainCamera", Static).SetValue(null, null);
            Application.logMessageReceived -= OnUnityLog;
        }
        Check(unityErrors.Count == 0, "isolated UI audit emits no Unity errors" + (unityErrors.Count == 0 ? "" : ": " + string.Join(" | ", unityErrors)));
        report.Add("FAILURES: " + failures);
        Directory.CreateDirectory("Builds/QA"); File.WriteAllLines("Builds/QA/build-interface-audit.txt", report);
        foreach (string row in report) Debug.Log("[build-ui] " + row);
        if (failures > 0) EditorApplication.Exit(1);
    }
    private static void Scale()
    {
        BasePart p = Part(); p.transform.localScale = new Vector3(2, 3, 1);
        p.SetCustomParam(BasePart.PartScaleKey, 1.5f); p.SetCustomParam(BasePart.PartScaleXKey, 2); p.SetCustomParam(BasePart.PartScaleYKey, 0.5f);
        Check(p.transform.localScale == new Vector3(6, 2.25f, 1.5f), "independent X/Y compose with legacy uniform scale");
        p.ApplyPartScale(); Check(p.transform.localScale.x == 6f, "scale reapply does not compound");
        var clone = Object.Instantiate(p.gameObject); objects.Add(clone); clone.GetComponent<BasePart>().ApplyPartScale();
        Check(clone.transform.localScale == p.transform.localScale, "cloning a scaled part preserves its original baseline");
        var rebuilt = Part(); rebuilt.transform.localScale = new Vector3(2, 3, 1); rebuilt.DeserializeCustomParams(p.SerializeCustomParams());
        Check(rebuilt.transform.localScale == p.transform.localScale, "axis scale survives save/load on a different instance");
        p.RemoveCustomParam(BasePart.PartScaleXKey); p.ApplyPartScale(); Check(p.transform.localScale.x == 3f, "removing X override restores that axis only");
        p.SetCustomParam(BasePart.PartScaleYKey, float.NaN); Check(!float.IsNaN(p.transform.localScale.y), "invalid scale cannot poison the transform");
        var collider = p.gameObject.AddComponent<BoxCollider>(); collider.size = Vector3.one;
        p.m_coordX = 8; p.m_coordY = 4; p.contraption = machine; machine.Parts.Add(p);
        p.transform.position = new Vector3(8, 4, 0);
        p.gameObject.SetActive(true); Physics.SyncTransforms();
        Check(Mathf.Abs(collider.bounds.size.x - p.transform.lossyScale.x) < 0.001f, "box collision geometry follows the X stretch");
        var owner = New("PickAudit").AddComponent<ConstructionUI>();
        typeof(ConstructionUI).GetField("m_contraption", Private).SetValue(owner, machine);
        Vector3 screen = camera.WorldToScreenPoint(new Vector3(9.25f, 4, 0));
        var picked = (BasePart)typeof(ConstructionUI).GetMethod("PickBuildPart", Private).Invoke(owner, new object[] { screen });
        Check(picked == p, "stretched edge selects the actual part outside its original cell");
        machine.Parts.Remove(p); p.contraption = null;
        p.gameObject.SetActive(false);
    }

    private static void TexturelessSprites()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/ZWrite.mat");
        Check(material != null && material.shader.name == "_Custom/Utility_ZWrite" && !material.HasProperty("_MainTex"),
            "sprite regression uses the actual textureless ZWrite material");
        if (material == null) return;
        var go = New("DepthOnlySprite");
        var sprite = go.AddComponent<UnmanagedSprite>();
        sprite.GetComponent<Renderer>().sharedMaterial = material;
        sprite.m_spriteWidth = 128; sprite.m_spriteHeight = 64;
        Call(sprite, "Awake");
        var originalMesh = sprite.SpriteMesh;
        Check(originalMesh != null && originalMesh.vertexCount == 4 && originalMesh.uv.Length == 4,
            "depth-only sprite Awake creates its plane and UVs without reading a missing texture");
        sprite.ResetSize();
        Check(sprite.m_spriteWidth == 128 && sprite.m_spriteHeight == 64 && sprite.SpriteMesh.bounds.size == originalMesh.bounds.size,
            "depth-only ResetSize retains authored masking geometry");
        Call(sprite, "OnDrawGizmos");
        var clone = Object.Instantiate(go); objects.Add(clone);
        Call(clone.GetComponent<UnmanagedSprite>(), "Awake");
        Check(clone.GetComponent<UnmanagedSprite>().SpriteMesh != null, "cloned depth-only menu sprite initializes without material errors");
        var texture = new Texture2D(512, 256);
        var textured = new Material(Shader.Find("Sprites/Default")) { mainTexture = texture };
        try
        {
            sprite.GetComponent<Renderer>().sharedMaterial = textured;
            var uv = sprite.CalculateUVs(0, 5, 16, 3);
            Check(Mathf.Abs(uv[0].x - 0.5f / 512f) < 0.000001f && Mathf.Abs(uv[0].y - (5f / 16f + 0.5f / 256f)) < 0.000001f,
                "textured sprite UV inset still follows the actual non-square atlas dimensions");
            sprite.GetComponent<Renderer>().sharedMaterial = null;
            Check(sprite.CalculateUVs(0, 0, 1, 1).Length == 4, "missing sprite material uses safe fallback UVs");
        }
        finally { Object.DestroyImmediate(textured); Object.DestroyImmediate(texture); }
    }

    private static void ToolbarLifecycle()
    {
        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(activeScene.path))
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(activeScene, "Assets/ToolbarLifecycleAuditBase.unity");
        var host = New("ConstructionHudToolbarHost", true);
        var menu = host.AddComponent<InGameBuildMenu>();
        bool toolBookEnabled = settings.StarSeaSettings.ToolBookEnabled;
        settings.StarSeaSettings.ToolBookEnabled = false;
        try { RuntimeSettings(() => Call(menu, "Awake")); }
        finally { settings.StarSeaSettings.ToolBookEnabled = toolBookEnabled; }
        var toolbar = host.GetComponent<INBuildToolPalette>();
        Check(toolbar != null && host.GetComponent<ToolBookButton>() == null,
            "native construction HUD installs its toolbar even with the optional toolbook disabled");
        if (toolbar == null) return;
        Check(INBuildToolPalette.Install(host) == toolbar && host.GetComponents<INBuildToolPalette>().Length == 1,
            "repeated construction HUD binding does not duplicate the toolbar");
        Call(toolbar, "Awake"); Call(toolbar, "Update");
        var field = typeof(INBuildToolPalette).GetField("m_canvasRoot", Private);
        var firstRoot = (GameObject)field.GetValue(toolbar);
        int buttonCount = firstRoot.transform.Find("Tools").GetComponentsInChildren<UnityEngine.UI.Button>(true).Length;
        Check(firstRoot.GetComponent<Canvas>().isRootCanvas && buttonCount == 13,
            "toolbar initialization is idempotent and creates one complete independent canvas");
        Check(firstRoot.scene == host.scene, "toolbar canvas belongs to its construction HUD scene");
        var bar = (RectTransform)firstRoot.transform.Find("Tools");
        var safe = new Rect(80, 20, 1740, 990);
        Call(toolbar, "LayoutToolbar", new Vector2(1920, 1080), safe, 1f);
        Check(Mathf.Abs(bar.anchoredPosition.y + 82f) < 0.001f && Mathf.Abs(bar.anchoredPosition.x + 10f) < 0.001f,
            "Android cutout and edge-to-edge insets move the toolbar into the usable screen area");
        Call(toolbar, "LayoutToolbar", new Vector2(800, 600), new Rect(20, 0, 760, 560), 1f);
        var lastButton = (RectTransform)bar.Find("Tool_Undo");
        var layers = (RectTransform)firstRoot.transform.Find("StructuralLayers");
        Check(bar.sizeDelta.x <= 736f && lastButton.anchoredPosition.y < -6f &&
            -lastButton.anchoredPosition.y + lastButton.sizeDelta.y <= bar.sizeDelta.y &&
            layers.anchoredPosition.y < bar.anchoredPosition.y - bar.sizeDelta.y,
            "narrow viewports wrap all tools and place structural layers below the complete toolbar");
        Call(toolbar, "LayoutToolbar", new Vector2(1920, 1080), new Rect(0, 0, 1920, 1080), 1f);
        Check(lastButton.anchoredPosition.y == -6f && bar.sizeDelta.y == 78f,
            "returning to a wide viewport restores the single toolbar row");
        var unloadScene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
            UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Additive);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(firstRoot, unloadScene);
        UnityEditor.SceneManagement.EditorSceneManager.CloseScene(unloadScene, true);
        Check(firstRoot == null && toolbar != null, "scene unload reproduces a surviving host with a destroyed toolbar canvas");
        Call(toolbar, "Update");
        var rebuilt = (GameObject)field.GetValue(toolbar);
        Check(rebuilt != null && rebuilt.activeInHierarchy && rebuilt.transform.Find("Tools").gameObject.activeSelf,
            "returning to construction recreates and shows the toolbar after scene teardown");
        BuildToolPalette.Select(BuildTool.Mirror); Call(toolbar, "Update");
        Check(rebuilt.transform.Find("Tools/Tool_Mirror").GetComponent<Image>().color == INBuildStyle.Brass &&
            rebuilt.transform.Find("Tools").GetComponentsInChildren<UnityEngine.UI.Button>(true).Length == buttonCount,
            "rebuilt toolbar updates live controls without retaining destroyed buttons or duplicates");
        typeof(LevelManager).GetField("m_gameState", Private).SetValue(WPFMonoBehaviour.levelManager, LevelManager.GameState.Running);
        Call(toolbar, "Update");
        Check(!rebuilt.transform.Find("Tools").gameObject.activeSelf && BuildToolPalette.Current == BuildTool.None,
            "leaving construction hides the toolbar and clears its active tool");
        typeof(LevelManager).GetField("m_gameState", Private).SetValue(WPFMonoBehaviour.levelManager, LevelManager.GameState.Building);
        Call(toolbar, "Update");
        Check(rebuilt.transform.Find("Tools").gameObject.activeSelf, "entering construction again restores the toolbar");
        BuildToolPalette.Select(BuildTool.Translate);
        host.SetActive(false); Call(toolbar, "OnDisable");
        Check(!rebuilt.activeSelf && BuildToolPalette.Current == BuildTool.None,
            "hiding the construction HUD hides its canvas and cancels the active tool");
        Object.DestroyImmediate(rebuilt); host.SetActive(true); Call(toolbar, "OnEnable"); Call(toolbar, "Update");
        var enabledRoot = (GameObject)field.GetValue(toolbar);
        Check(enabledRoot != null && enabledRoot.activeInHierarchy, "re-enabling an owner also recovers a missing toolbar canvas");
        Call(menu, "OnDestroy"); Call(toolbar, "OnDestroy"); Object.DestroyImmediate(host);
        Check(enabledRoot == null, "destroying the toolbar owner cleans up its independent canvas");
    }
    private static void LayerToolbar()
    {
        var host = New("LayerToolbarHost", true); var toolbar = host.AddComponent<INBuildToolPalette>(); Call(toolbar, "Awake");
        var root = (GameObject)typeof(INBuildToolPalette).GetField("m_canvasRoot", Private).GetValue(toolbar);
        var selector = root.GetComponent<INBuildLayerSelector>(); var row = root.transform.Find("StructuralLayers");
        var part = Part(10, 4, machine); part.SetStoredParam(PartPhysicsParameters.LayerKey, 3);
        var material = new Material(Shader.Find("Sprites/Default")); var renderer = part.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        var selection = New("LayerClipboard").AddComponent<INSelectionManager>();
        var instanceField = typeof(INSelectionManager).GetField("s_instance", Static); var previousSelection = instanceField.GetValue(null);
        instanceField.SetValue(null, selection);
        foreach (string name in new[] { "m_minX", "m_minY" }) typeof(INSelectionManager).GetField(name, Private).SetValue(selection, -10000);
        foreach (string name in new[] { "m_maxX", "m_maxY" }) typeof(INSelectionManager).GetField(name, Private).SetValue(selection, 10000);
        try
        {
            settings.GeneralSettings.MultiLayerBuild = false; Call(selector, "Update");
            Check(!row.gameObject.activeSelf, "default-off layer setting hides the layer toolbar");
            settings.GeneralSettings.MultiLayerBuild = true; Call(selector, "Update");
            Check(row.gameObject.activeSelf && row.GetComponentsInChildren<UnityEngine.UI.Button>().Length == 7,
                "enabling layer building reveals six selectable planes and the all-layer view");
            row.Find("Layer_3").GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); Call(selector, "Update");
            Check(ContraptionLayers.ActiveLayer == 3 && ContraptionLayers.Editable(part), "the actual layer toolbar button changes the editable plane");
            Call(selection, "CopySelected");
            Check(selection.Clipboard?.Units.Count == 1 && ContraptionLayers.SavedLayer(selection.Clipboard.Units[0].CustomParams) == 3,
                "legacy selection clipboard copies only the editable layer");
            typeof(INSelectionManager).GetField("m_state", Private).SetValue(selection, INSelectionManager.State.Selected);
            row.Find("Layer_0").GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); Call(selector, "Update");
            Check(!ContraptionLayers.Editable(part) && selection.CurrentState == INSelectionManager.State.Idle,
                "the actual all-layer button enables a read-only construction view and cancels old toolbook selection");
            int count = machine.Parts.Count;
            typeof(INSelectionManager).GetField("m_state", Private).SetValue(selection, INSelectionManager.State.Selected);
            selection.DeleteSelection();
            Check(machine.Parts.Count == count, "legacy rectangle deletion cannot edit an all-layer read-only view");
            row.Find("Layer_2").GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); Call(selector, "Update");
            var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
            Check(Mathf.Approximately(block.GetColor("_Color").a, 0.3f), "layer toolbar applies BPIR focus dimming to another plane");
            Call(selector, "OnDisable"); renderer.GetPropertyBlock(block);
            Check(!block.HasColor("_Color"), "hiding the layer toolbar restores original renderer state");
            Call(selector, "Update"); renderer.GetPropertyBlock(block);
            Check(Mathf.Approximately(block.GetColor("_Color").a, 0.3f), "reopening the layer toolbar refreshes focus with unchanged owner and part count");
            settings.GeneralSettings.MultiLayerBuild = false; Call(selector, "Update"); renderer.GetPropertyBlock(block);
            Check(!row.gameObject.activeSelf && !block.HasColor("_Color"), "turning layer building off hides its controls and removes focus dimming");
        }
        finally
        {
            settings.GeneralSettings.MultiLayerBuild = false; ContraptionLayerView.Clear();
            machine.TryRemovePart(part); Object.DestroyImmediate(part.gameObject); Object.DestroyImmediate(material);
            instanceField.SetValue(null, previousSelection); Object.DestroyImmediate(selection.gameObject);
            Call(toolbar, "OnDestroy"); Object.DestroyImmediate(host);
        }
    }
    private static void Wheels()
    {
        foreach (Type type in new[] { typeof(MotorWheel), typeof(CartWheel), typeof(StickyWheel), typeof(OffRoadWheel) })
        {
            var go = New(type.Name); var collider = go.AddComponent<SphereCollider>(); collider.radius = type == typeof(OffRoadWheel) ? 0.9f : 0.45f;
            BasePart wheel = (BasePart)go.AddComponent(type);
            wheel.SetCustomParam("WheelRadius", collider.radius * 2);
            wheel.SetCustomParam(BasePart.PartScaleXKey, 1.5f); wheel.SetCustomParam(BasePart.PartScaleYKey, 0.75f);
            Check(Near(wheel.transform.localScale, new Vector2(3, 1.5f)), type.Name + " radius and axis scale compose immediately");
            var clone = Object.Instantiate(go); objects.Add(clone); var cp = clone.GetComponent<BasePart>(); cp.ApplyCustomParams();
            Check(Near(cp.transform.localScale, wheel.transform.localScale), type.Name + " scaled clone does not grow again");
            wheel.SetCustomParam(BasePart.PartScaleYKey, 1f); Check(Mathf.Abs(wheel.transform.localScale.y - 2f) < 0.001f, type.Name + " Y reset preserves radius/X");
        }
    }
    private static void Pose()
    {
        BasePart p = machine.Parts[0]; GridlessBuild.SetOffset(p, new Vector2(0.25f, 0.125f));
        var frame = new BuildMachineTransform { translation = new Vector2(2.4f, -0.3f), angle = 37f };
        BuildMachineTransform.Apply(machine, frame);
        Vector2 expected = frame.TransformPoint(new Vector2(p.m_coordX, p.m_coordY) + GridlessBuild.GetOffset(p));
        Check(Near(p.transform.localPosition, expected), "machine transform applies to the part's actual position");
        Check(Mathf.Abs(Mathf.DeltaAngle(p.transform.localEulerAngles.z, 37f)) < 0.001f, "machine rotation applies with fine-rotation setting at zero");
        var restoredMachine = New("RestoredMachine").AddComponent<Contraption>(); Call(restoredMachine, "PostAwake");
        var rebuilt = Part(p.m_coordX, p.m_coordY, restoredMachine); rebuilt.DeserializeCustomParams(p.SerializeCustomParams());
        Check(Near(rebuilt.transform.localPosition, p.transform.localPosition), "load-after-placement immediately restores saved pose");
        Check(Mathf.Abs(Mathf.DeltaAngle(rebuilt.transform.localEulerAngles.z, 37f)) < 0.001f, "saved machine angle survives loading");
        BasePart fine = Part(); fine.SetStoredParam(FineRotate.AngleKey, 23f);
        fine.SetRotation(BasePart.GridRotation.Deg_90);
        Check(Mathf.Abs(Mathf.DeltaAngle(fine.transform.localEulerAngles.z, 113f)) < 0.001f, "stock rotation still turns a saved fine angle when fine-step is disabled");
        var pivot = frame.TransformPoint(new Vector2(1, 1)); var rotated = BuildMachineTransform.AroundPivot(frame, pivot, 123f);
        Check(Near(rotated.TransformPoint(new Vector2(1, 1)), pivot), "rotation keeps the selected pivot fixed");
        Check(Near(frame.InversePoint(frame.TransformPoint(new Vector2(4.3f, 1.2f))), new Vector2(4.3f, 1.2f)), "placement inverse works after fractional translation and rotation");
        Check(machine.FindPartAt(0, 1) == p, "whole-machine transform preserves logical occupancy");
        p.TryGetStoredParam(BuildMachineTransform.AngleKey, out float saved);
        settings.GeneralSettings.NoGridMode = false;
        Check(Near(GridlessBuild.GetOffset(p), new Vector2(0.25f, 0.125f)) && saved == 37f, "placement toggles do not erase saved layout");
        string snapshot = p.SerializeCustomParams(); p.DeserializeCustomParams("@gridlessX:0.1;@gridlessY:0.2");
        Check(Near(GridlessBuild.GetOffset(p), new Vector2(0.1f, 0.2f)), "replacing parameters invalidates the old offset cache");
        p.DeserializeCustomParams(snapshot); BuildMachineTransform.Apply(machine, new BuildMachineTransform());
    }
    private static void Snapping()
    {
        settings.GeneralSettings.NoGridMode = true;
        GridlessBuild.TryGetBounds(machine, out Rect zone);
        Vector2 p = new Vector2(2.13f, 1.14f), snapped = GridlessBuild.SnapPointer(machine, p);
        Check(Near(snapped, new Vector2(2.25f, 1.125f)), "inside dashed boundary uses the first part's fractional grid origin");
        Vector2 outside = new Vector2(zone.xMax + 0.6f, zone.yMax + 0.33f);
        Check(Near(GridlessBuild.SnapPointer(machine, outside), outside), "outside dashed boundary remains continuous");
        GridlessBuild.TryGetBounds(machine, out Rect exact, 0);
        Check(Mathf.Abs(zone.xMin - exact.xMin + 1f) < 0.001f && Mathf.Abs(zone.yMax - exact.yMax - 1f) < 0.001f, "snap boundary extends exactly one cell from vehicle bounds");
        machine.Parts[2].SetCustomParam(BasePart.PartScaleXKey, 3f);
        GridlessBuild.TryGetBounds(machine, out Rect stretched, 0);
        Check(stretched.xMax > exact.xMax + 0.9f, "snap boundary updates immediately after axis stretching");
        machine.Parts[2].RemoveCustomParam(BasePart.PartScaleXKey); machine.Parts[2].ApplyPartScale();
        var frame = new BuildMachineTransform { angle = 90, translation = new Vector2(3.2f, 0.7f) }; BuildMachineTransform.Apply(machine, frame);
        Vector2 world = frame.TransformPoint(p);
        Check(Near(BuildMachineTransform.PointerLocal(machine, machine.transform.TransformPoint(world)), snapped), "rotated/translated machine still snaps to its own grid");
        GridlessBuild.Prepare(machine, 2, 1, p); BasePart placed = Part(); GridlessBuild.ConsumePending(placed);
        Check(Near(BuildMachineTransform.Read(placed).translation, frame.translation) && BuildMachineTransform.Read(placed).angle == 90, "new placement inherits the machine frame");
        Check(Near(GridlessBuild.GetOffset(placed), new Vector2(0.25f, 0.125f)), "placed part carries the snapped fractional origin");
        GridlessBuild.TryGetBounds(machine, out zone);
        outside = new Vector2(zone.xMax + 4.3f, zone.yMax + 0.33f);
        int ox = Mathf.RoundToInt(outside.x), oy = Mathf.RoundToInt(outside.y);
        GridlessBuild.Prepare(machine, ox, oy, outside);
        BasePart detached = Part(ox, oy, machine); GridlessBuild.ConsumePending(detached);
        GridlessBuild.TryGetBounds(machine, out Rect unchanged);
        Check(GridlessBuild.IsOutside(detached) && unchanged == zone, "placing outside the dashed boundary does not expand it");
        detached.SetCustomParam(BasePart.PartScaleXKey, 10f);
        GridlessBuild.TryGetBounds(machine, out unchanged);
        Check(unchanged == zone && Near(GridlessBuild.SnapPointer(machine, outside), outside), "stretching an outside part cannot enlarge the snapping zone");
        string detachedData = detached.SerializeCustomParams(); var restored = Part(); restored.DeserializeCustomParams(detachedData);
        Check(GridlessBuild.IsOutside(restored), "outside-boundary membership survives saving and loading");
        GridlessBuild.TryGetBounds(machine, out Rect beforeInside);
        Vector2 inside = new Vector2(beforeInside.xMax - 0.1f, 1.1f);
        GridlessBuild.Prepare(machine, Mathf.RoundToInt(inside.x), 1, inside);
        var joined = Part(Mathf.RoundToInt(inside.x), 1, machine); GridlessBuild.ConsumePending(joined);
        GridlessBuild.TryGetBounds(machine, out Rect extended);
        Check(!GridlessBuild.IsOutside(joined) && extended.xMax > beforeInside.xMax, "placement inside the zone still extends the vehicle boundary");
        machine.Parts.Remove(joined); joined.transform.SetParent(null); joined.contraption = null;
        BuildMachineTransform.Apply(machine, frame); GridlessBuild.TryGetBounds(machine, out unchanged);
        Check(unchanged == zone, "rotating the machine does not absorb an outside part into its boundary");
        machine.Parts.Remove(detached); detached.transform.SetParent(null); detached.contraption = null;
        BuildMachineTransform.Apply(machine, new BuildMachineTransform());
    }

    private static void RuntimePose()
    {
        foreach (float angle in new[] { 37f, 90f, 233f })
        {
            var frame = new BuildMachineTransform { angle = angle, translation = new Vector2(2.4f, -0.3f) };
            BuildMachineTransform.Apply(machine, frame);
            Contraption runtime = machine.Clone(); objects.Add(runtime.gameObject); Call(runtime, "PostAwake");
            bool samePose = true, sameCells = true, stable = true;
            foreach (BasePart part in runtime.GetComponentsInChildren<BasePart>(true))
            {
                Vector3 position = part.transform.localPosition; Quaternion rotation = part.transform.localRotation;
                int x = part.m_coordX, y = part.m_coordY;
                Call(runtime, "RestoreSimulationPartPose", part);
                samePose &= (part.transform.localPosition - position).sqrMagnitude < 0.00001f && Quaternion.Angle(part.transform.localRotation, rotation) < 0.001f;
                sameCells &= part.m_coordX == x && part.m_coordY == y && runtime.FindPartAt(x, y) == part;
                Call(runtime, "RestoreSimulationPartPose", part);
                stable &= (part.transform.localPosition - position).sqrMagnitude < 0.00001f;
            }
            Check(samePose, "runtime clone preserves displayed pose at " + angle + " degrees");
            Check(sameCells, "runtime initialization preserves logical cells and neighbors at " + angle + " degrees");
            Check(stable, "repeated runtime pose recovery does not compound transforms at " + angle + " degrees");
        }
        var legacy = New("LegacyRuntime").AddComponent<Contraption>(); Call(legacy, "PostAwake");
        BasePart authored = Part(0, 0, legacy); authored.transform.localPosition = new Vector3(4f, 2f, -0.1f);
        Call(legacy, "RestoreSimulationPartPose", authored);
        Check(authored.m_coordX == 4 && authored.m_coordY == 2, "legacy authored runtime parts still derive their cells from position");
        BuildMachineTransform.Apply(machine, new BuildMachineTransform());
    }

    private static BasePart PlaceWorld(Contraption c, Vector2 point)
    {
        Vector3 world = c.transform.TransformPoint(new Vector3(point.x, point.y, 0f));
        Vector3 local = BuildMachineTransform.PointerLocal(c, world);
        int x = Mathf.RoundToInt(local.x), y = Mathf.RoundToInt(local.y);
        GridlessBuild.PrepareWorld(c, x, y, world);
        BasePart part = Part(x, y, c); GridlessBuild.ConsumePending(part); c.RefreshPartPose(part);
        return part;
    }
    private static void MultipleGroups()
    {
        var c = New("IndependentGroups").AddComponent<Contraption>(); Call(c, "PostAwake");
        BasePart primary = Part(0, 1, c); Part(1, 1, c); GridlessBuild.SetOffset(primary, new Vector2(0.2f, 0.1f));
        var firstFrame = new BuildMachineTransform { angle = 47f, translation = new Vector2(0.4f, 0.3f) };
        BuildMachineTransform.Apply(c, firstFrame); GridlessBuild.TryGetBounds(c, out Rect firstBounds);
        BasePart second = PlaceWorld(c, new Vector2(5.3f, 2.2f)); int secondId = GridlessBuild.GetGroupId(second);
        GridlessBuild.TryGetBounds(c, out Rect stillFirst);
        Check(secondId > 0 && GridlessBuild.GetGroups(c).Count == 2 && stillFirst == firstBounds, "outside placement creates a second independent dashed frame without expanding the first");
        Check(BuildMachineTransform.Read(second).angle == 0f && Mathf.Abs(Mathf.DeltaAngle(second.transform.localEulerAngles.z, 0f)) < 0.001f,
            "a new outside group starts at zero angle instead of inheriting 47 degrees");
        Check(Near(second.transform.localPosition, new Vector2(5.3f, 2.2f)), "new outside group's first part stays at the requested world position");
        BasePart joined = PlaceWorld(c, new Vector2(6.35f, 2.29f));
        Check(GridlessBuild.GetGroupId(joined) == secondId && Near(joined.transform.localPosition, new Vector2(6.3f, 2.2f)), "a nearby placement joins the second group's fractional lattice");
        BasePart third = PlaceWorld(c, new Vector2(8.4f, 4.7f)); int thirdId = GridlessBuild.GetGroupId(third);
        Check(thirdId != secondId && thirdId > 0 && GridlessBuild.GetGroups(c).Count == 3 && BuildMachineTransform.Read(third).angle == 0f,
            "placement outside every frame creates a third independent zero-angle group");
        Vector2 primaryPosition = primary.transform.localPosition, thirdPosition = third.transform.localPosition;
        GridlessBuild.TryGetGroup(c, secondId, out var secondGroup);
        var rotated = BuildMachineTransform.AroundPivot(secondGroup.frame, secondGroup.frame.TransformPoint(secondGroup.bounds.center), 90f);
        BuildMachineTransform.Apply(c, rotated, secondId);
        Check(Near(primary.transform.localPosition, primaryPosition) && Near(third.transform.localPosition, thirdPosition) && BuildMachineTransform.Read(primary).angle == 47f,
            "rotating the second group leaves the first and third poses unchanged");
        Vector2 raw = rotated.TransformPoint(new Vector2(6.41f, 2.23f));
        Vector2 snap = BuildMachineTransform.PointerLocal(c, c.transform.TransformPoint(raw));
        Check(Near(snap, new Vector2(6.3f, 2.2f)), "pointer snapping follows the rotated second frame rather than the first frame");
        BasePart inherited = PlaceWorld(c, rotated.TransformPoint(new Vector2(6.42f, 3.25f)));
        Check(GridlessBuild.GetGroupId(inherited) == secondId && BuildMachineTransform.Read(inherited).angle == 90f,
            "new placement inside the rotated second frame inherits only that group's angle");
        var restored = New("RestoredGroups").AddComponent<Contraption>(); Call(restored, "PostAwake");
        bool saved = true;
        foreach (BasePart part in c.Parts)
        {
            BasePart copy = Part(part.m_coordX, part.m_coordY, restored); copy.DeserializeCustomParams(part.SerializeCustomParams());
            saved &= GridlessBuild.GetGroupId(copy) == GridlessBuild.GetGroupId(part) && Near(copy.transform.localPosition, part.transform.localPosition);
        }
        Check(saved && GridlessBuild.GetGroups(restored).Count == 3, "save/load restores all independent frames and their member poses");
        Contraption runtime = c.Clone(); objects.Add(runtime.gameObject); Call(runtime, "PostAwake"); bool runtimePose = true;
        foreach (BasePart part in runtime.GetComponentsInChildren<BasePart>(true))
        {
            Vector2 before = part.transform.localPosition; Call(runtime, "RestoreSimulationPartPose", part);
            runtimePose &= Near(before, part.transform.localPosition);
        }
        Check(runtimePose, "runtime cloning preserves mixed 47/90/0 degree groups without applying any frame twice");
        var neighbor = Part(1, 1, restored); GridlessBuild.SetGroupId(neighbor, thirdId); restored.RefreshPartPose(neighbor);
        BasePart logicalNeighbor = restored.FindPartAt(1, 1, restored.Parts[0]);
        Check(logicalNeighbor == null || GridlessBuild.GetGroupId(logicalNeighbor) == GridlessBuild.GetGroupId(restored.Parts[0]),
            "logical neighbor lookup never creates a joint to another vehicle group");
        editor.HandlePointer(c, new GuiManager.Pointer(), 0f, false, false);
        BuildToolPalette.Select(BuildTool.SelectMachine);
        Vector2 target = camera.WorldToScreenPoint(second.transform.position);
        editor.HandlePointer(c, new GuiManager.Pointer { down = true, position = target - Vector2.one * 15f }, 0.1f, false, true);
        editor.HandlePointer(c, new GuiManager.Pointer { up = true, position = target + Vector2.one * 15f }, 0.2f, false, false);
        Check(editor.Selected && editor.SelectedGroup == secondId, "toolbar marquee selects the group it intersects");
        Call(editor, "LateUpdate");
        Vector2 center = (Vector2)typeof(INMachineBuildEditor).GetField("m_pivotScreen", Private).GetValue(editor);
        Vector2 arrow = center + Vector2.right * (48f * Mathf.Max(0.6f, Screen.height / 1080f));
        editor.HandlePointer(c, new GuiManager.Pointer { down = true, position = arrow }, 0.3f, false, true);
        editor.HandlePointer(c, new GuiManager.Pointer { position = arrow + Vector2.right * 70f }, 0.4f, false, true);
        editor.HandlePointer(c, new GuiManager.Pointer { up = true, position = arrow + Vector2.right * 70f }, 0.5f, false, false);
        Check(!Near(BuildMachineTransform.Read(c, secondId).translation, rotated.translation) && Near(primary.transform.localPosition, primaryPosition) && Near(third.transform.localPosition, thirdPosition),
            "gizmo drag translates only the selected second vehicle");
        target = camera.WorldToScreenPoint(third.transform.position);
        editor.HandlePointer(c, new GuiManager.Pointer { down = true, position = target - Vector2.one * 15f }, 0.6f, false, true);
        editor.HandlePointer(c, new GuiManager.Pointer { up = true, position = target + Vector2.one * 15f }, 0.7f, false, false);
        editor.Undo();
        Check(editor.SelectedGroup == thirdId && Near(BuildMachineTransform.Read(c, secondId).translation, rotated.translation) && Near(third.transform.localPosition, thirdPosition),
            "undo restores the moved group even after selection switches to another group");
        BuildToolPalette.Select(BuildTool.Translate);
        Check((bool)typeof(INMachineBuildEditor).GetMethod("WantsMachinePointer", Private).Invoke(editor, new object[] { (Vector2)camera.WorldToScreenPoint(second.transform.position) }),
            "clicking another group captures camera input on the first frame");
        editor.HandlePointer(c, new GuiManager.Pointer { down = true, position = camera.WorldToScreenPoint(second.transform.position) }, 0.8f, false, true);
        Check(editor.SelectedGroup == secondId, "translate tool can switch directly to the clicked vehicle group");
        editor.DeselectMachine(); BuildToolPalette.Clear();
        BasePart legacy = Part(-3, 2, c); legacy.SetStoredParam(GridlessBuild.OutsideKey, 1f); firstFrame.Store(legacy); c.RefreshPartPose(legacy);
        Vector2 legacyPosition = legacy.transform.localPosition; GridlessBuild.InvalidateBounds(c);
        Check(GridlessBuild.GetGroups(c).Count == 4 && GridlessBuild.GetGroupId(legacy) > 0 && Near(legacy.transform.localPosition, legacyPosition),
            "legacy outside markers recover visible independent frames without moving the saved blueprint");
        int deletedId = thirdId; c.Parts.Remove(third); third.transform.SetParent(null); third.contraption = null;
        GridlessBuild.InvalidateBounds(c); BasePart replacement = PlaceWorld(c, new Vector2(10.3f, 5.2f));
        Check(GridlessBuild.GetGroupId(replacement) > deletedId, "deleted group IDs are not reused by new frames in the same session");
        var moved = New("MovedFrameSeed").AddComponent<Contraption>(); Call(moved, "PostAwake");
        BasePart occupied = Part(0, 1, moved);
        BuildMachineTransform.Apply(moved, new BuildMachineTransform { angle = 90f, translation = new Vector2(8f, 5f) });
        BasePart fresh = PlaceWorld(moved, new Vector2(0.12f, 1.08f));
        Check(GridlessBuild.GetGroupId(fresh) > 0 && moved.FindPartAt(0, 1) == occupied && fresh.m_coordX != occupied.m_coordX &&
            Near(fresh.transform.localPosition, new Vector2(0.12f, 1.08f)) && BuildMachineTransform.Read(fresh).angle == 0f,
            "a new frame can occupy empty world space above another moved group's logical cell");
        int previousGroup = GridlessBuild.GetGroupId(fresh); moved.Parts.Clear(); GridlessBuild.InvalidateBounds(moved);
        BasePart afterClear = PlaceWorld(moved, new Vector2(3.1f, 2.1f));
        Check(GridlessBuild.GetGroupId(afterClear) > previousGroup, "clearing every vehicle also preserves non-reusable group IDs for undo");
        var overlap = New("OverlappingMargins").AddComponent<Contraption>(); Call(overlap, "PostAwake");
        Part(0, 0, overlap); overlap.RefreshPartPositions(); BasePart nearby = PlaceWorld(overlap, new Vector2(1.7f, 0f));
        Vector2 resolved = GridlessBuild.ResolvePoint(overlap, new Vector2(1.4f, 0.8f), true, out int ownerId, out _);
        Check(ownerId == GridlessBuild.GetGroupId(nearby) && Near(resolved, new Vector2(1.7f, 1f)),
            "overlapping frame margins snap to the nearest group's own lattice");
        editor.HandlePointer(machine, new GuiManager.Pointer(), 1f, false, false);
    }

    private static void SpecialRotations()
    {
        foreach (Type type in new[] { typeof(Tail), typeof(Wings), typeof(IntegratedCircuitPart) })
        {
            BasePart part = (BasePart)New("Rotation_" + type.Name).AddComponent(type);
            var frame = new BuildMachineTransform { angle = 47f }; frame.Store(part);
            part.SetRotation(BasePart.GridRotation.Deg_90);
            Check(Mathf.Abs(Mathf.DeltaAngle(part.transform.localEulerAngles.z, 137f)) < 0.001f, type.Name + " custom rotation composes machine angle");
        }
    }
    private static BasePart MergePart(Contraption c, int x, int group, Vector2 position, float angle = 0f, int y = 0)
    {
        BasePart p = Part(x, y, c); p.m_jointConnectionDirection = BasePart.JointConnectionDirection.Any; p.m_mass = 1f;
        GridlessBuild.SetGroupId(p, group);
        new BuildMachineTransform { angle = angle, translation = position - BuildMachineTransform.Rotate(new Vector2(x, y), angle) }.Store(p);
        c.RefreshPartPose(p); return p;
    }
    private static Contraption MergeMachine(string name, bool active = false)
    {
        var c = New(name, active).AddComponent<Contraption>(); Call(c, "PostAwake"); return c;
    }
    private static void GroupMerge()
    {
        bool previous = settings.GeneralSettings.MergeGridlessGroups;
        bool previousGrid = settings.GeneralSettings.NoGridMode;
        try
        {
            Check(!new GeneralSettings().MergeGridlessGroups, "group merging is opt-in for existing settings files");
            var copy = new GeneralSettings(); copy.Update(new GeneralSettings { MergeGridlessGroups = true });
            var savedSettings = Newtonsoft.Json.JsonConvert.DeserializeObject<GeneralSettings>(Newtonsoft.Json.JsonConvert.SerializeObject(copy));
            Check(copy.MergeGridlessGroups && savedSettings.MergeGridlessGroups, "merge switch survives settings copying and JSON persistence");
            settings.GeneralSettings.NoGridMode = true;
            var c = MergeMachine("CircleMerge");
            BasePart a = MergePart(c, 0, 0, Vector2.zero), b = MergePart(c, 8, 7, new Vector2(1.12f, 0), 90);
            var circles = new List<GridlessGroupMerge.Circle>(); GridlessGroupMerge.GetCircles(a, circles);
            Check(circles.Count == 4 && Near(circles[0].position, new Vector2(0.5f, 0)), "native connection circles lie on all four authored grid faces");
            a.SetCustomParam(BasePart.PartScaleXKey, 2f); GridlessGroupMerge.GetCircles(a, circles);
            Check(Near(circles[0].position, new Vector2(1f, 0)), "native connection-circle positions follow independent axis stretching");
            a.RemoveCustomParam(BasePart.PartScaleXKey); a.ApplyPartScale();
            settings.GeneralSettings.MergeGridlessGroups = false;
            Check(GridlessGroupMerge.TryMerge(c, 7) == 0 && GridlessBuild.GetGroups(c).Count == 2, "disabled merge switch preserves touching independent groups");
            settings.GeneralSettings.MergeGridlessGroups = true; settings.GeneralSettings.NoGridMode = false;
            Check(GridlessGroupMerge.TryMerge(c, 7) == 0, "merge contact testing is inactive in grid construction mode");
            settings.GeneralSettings.NoGridMode = true;
            var far = MergeMachine("MarginsOnly"); MergePart(far, 0, 0, Vector2.zero); MergePart(far, 8, 8, new Vector2(2, 0));
            Check(GridlessGroupMerge.TryMerge(far, 8) == 0, "overlapping dashed margins alone do not merge groups");
            Vector2 oldA = a.transform.localPosition, oldB = b.transform.localPosition;
            float oldAngle = b.transform.localEulerAngles.z; int count = c.Parts.Count;
            Check(GridlessGroupMerge.TryMerge(c, 7) == 1 && GridlessBuild.GetGroups(c).Count == 1 &&
                GridlessBuild.GetGroupId(a) == 7 && GridlessBuild.GetGroupId(b) == 7, "partially overlapping connection circles absorb both frames into the manipulated group");
            Check(Near(oldA, a.transform.localPosition) && Near(oldB, b.transform.localPosition) &&
                Mathf.Abs(Mathf.DeltaAngle(oldAngle, b.transform.localEulerAngles.z)) < 0.001f && c.Parts.Count == count,
                "merging mixed 0/90-degree frames preserves part positions, orientations and inventory size");
            Check(a.m_coordX == 0 && b.m_coordX == 8 && c.FindPartAt(0, 0) == a && c.FindPartAt(8, 0) == b &&
                GridlessBuild.GetLatticeId(a) == 0 && GridlessBuild.GetLatticeId(b) == 7,
                "merged membership retains each lattice's logical cells and original map ownership");
            GridlessBuild.TryGetGroup(c, 7, out var merged);
            Check(merged.bounds.xMax < 2f && GridlessBuild.ContainsInclusive(merged.bounds, oldA) && GridlessBuild.ContainsInclusive(merged.bounds, oldB),
                "merged frame bounds project each member into the primary frame rather than including its old distant logical cell");
            var rogue = Part(1, 0); rogue.contraption = c; GridlessBuild.SetGroupId(rogue, 7); rogue.SetStoredParam(GridlessBuild.LatticeKey, 7);
            c.SetPartMap(1, 0, 0, rogue);
            Check(c.FindPartAt(1, 0, a) == null, "logical neighbor lookup cannot weld two different saved lattices accidentally");
            c.SetPartMap(1, 0, 0, null);
            GridlessBuild.PrepareWorld(c, 0, 1, c.transform.TransformPoint(new Vector3(0, 1)));
            var placed = Part(0, 1, c); GridlessBuild.ConsumePending(placed); c.RefreshPartPose(placed);
            Check(GridlessBuild.GetGroupId(placed) == 7 && GridlessBuild.GetLatticeId(placed) == 0,
                "new placement inside a merged frame inherits its primary snapping lattice");
            var saved = MergeMachine("SavedMerge");
            foreach (BasePart p in c.Parts)
            {
                BasePart load = Part(p.m_coordX, p.m_coordY, saved); load.DeserializeCustomParams(p.SerializeCustomParams());
                saved.RefreshPartPose(load);
            }
            Check(GridlessBuild.GetGroups(saved).Count == 1 && Near(saved.Parts[1].transform.localPosition, oldB) &&
                Mathf.Abs(Mathf.DeltaAngle(saved.Parts[1].transform.localEulerAngles.z, oldAngle)) < 0.001f,
                "save/load preserves merged membership and distinct member frames without rebasing or pose drift");
            var frame = BuildMachineTransform.AroundPivot(BuildMachineTransform.Read(c, 7), Vector2.zero, 37);
            frame.translation += new Vector2(2, 3); BuildMachineTransform.Apply(c, frame, 7);
            Check(Near(a.transform.localPosition, BuildMachineTransform.Rotate(oldA, 37) + new Vector2(2, 3)) &&
                Near(b.transform.localPosition, BuildMachineTransform.Rotate(oldB, 37) + new Vector2(2, 3)) &&
                Mathf.Abs(Mathf.DeltaAngle(b.transform.localEulerAngles.z, 127)) < 0.001f,
                "merged mixed-angle lattices translate and rotate together by one rigid delta");
            settings.GeneralSettings.MergeGridlessGroups = false;
            frame.translation += Vector2.right; BuildMachineTransform.Apply(c, frame, 7);
            Check(GridlessBuild.GetGroups(c).Count == 1 && GridlessBuild.GetGroupId(a) == GridlessBuild.GetGroupId(b),
                "switching merging off never splits a previously merged machine");
            var chain = MergeMachine("MergeChain");
            MergePart(chain, 0, 0, Vector2.zero); MergePart(chain, 8, 7, new Vector2(1.1f, 0));
            MergePart(chain, 16, 11, new Vector2(2.2f, 0)); BasePart unrelated = MergePart(chain, 25, 12, new Vector2(6, 0));
            settings.GeneralSettings.MergeGridlessGroups = true;
            Check(GridlessGroupMerge.TryMerge(chain, 7) == 2 && GridlessBuild.GetGroups(chain).Count == 2 && GridlessBuild.GetGroupId(unrelated) == 12,
                "transitive contact merges three touching groups while keeping unrelated groups independent");
            var authored = MergeMachine("AuthoredCircle"); BasePart p1 = Part(0, 0, authored), p2 = Part(8, 0, authored);
            GridlessBuild.SetGroupId(p2, 8); new BuildMachineTransform { translation = new Vector2(-7.95f, 0) }.Store(p2); authored.RefreshPartPose(p2);
            foreach (BasePart p in new[] { p1, p2 })
            {
                var root = New("ConnectionPoints"); root.transform.SetParent(p.transform, false);
                var node = New("Point-Centre"); node.transform.SetParent(root.transform, false); node.transform.localScale = Vector3.one * 2;
            }
            GridlessGroupMerge.GetCircles(p1, circles);
            Check(circles.Count == 1 && Mathf.Abs(circles[0].radius - PartConnections.DefaultPointRadius * 2f) < 0.0001f &&
                GridlessGroupMerge.TryMerge(authored, 8) == 1, "declared connection nodes retain their authored radii and drive group merging");
            var blocked = MergeMachine("NoConnectionCircle"); BasePart none = MergePart(blocked, 0, 0, Vector2.zero);
            none.m_jointConnectionType = BasePart.JointConnectionType.None;
            MergePart(blocked, 8, 8, new Vector2(1.1f, 0));
            Check(GridlessGroupMerge.TryMerge(blocked, 8) == 0, "parts without connection permission do not merge nearby frames");
        }
        finally { settings.GeneralSettings.MergeGridlessGroups = previous; settings.GeneralSettings.NoGridMode = previousGrid; }
    }
    private static void SelectMergeGroup(Contraption c, int id, BuildTool tool)
    {
        BuildToolPalette.Select(tool); editor.HandlePointer(c, new GuiManager.Pointer(), 30, false, false);
        typeof(INMachineBuildEditor).GetField("m_selectedGroup", Private).SetValue(editor, id);
        editor.SelectMachine(); Call(editor, "LateUpdate");
    }
    private static void GroupMergeInput()
    {
        bool previous = settings.GeneralSettings.MergeGridlessGroups;
        settings.GeneralSettings.NoGridMode = true; settings.GeneralSettings.MergeGridlessGroups = true;
        try
        {
            var c = MergeMachine("MergePointer");
            BasePart a = MergePart(c, 0, 0, Vector2.zero), b = MergePart(c, 4, 7, new Vector2(4, 0));
            string beforeA = a.SerializeCustomParams(), beforeB = b.SerializeCustomParams();
            SelectMergeGroup(c, 7, BuildTool.Translate);
            Vector2 start = GizmoCenter(); Vector2 end = camera.WorldToScreenPoint(WPFMonoBehaviour.ScreenToZ0(start) + Vector3.left * 2.88f);
            editor.HandlePointer(c, new GuiManager.Pointer { down = true, position = start }, 31, false, true);
            editor.HandlePointer(c, new GuiManager.Pointer { position = end }, 31.1f, false, true);
            Check(GridlessBuild.GetGroups(c).Count == 2 && Near(b.transform.localPosition, new Vector2(1.12f, 0)),
                "drag preview can overlap connection circles without merging before the action finishes");
            editor.HandlePointer(c, new GuiManager.Pointer { position = end }, 31.2f, false, true, escape: true);
            Check(GridlessBuild.GetGroups(c).Count == 2 && b.SerializeCustomParams() == beforeB,
                "cancelling an overlapping machine drag restores its pose and never merges groups");
            SelectMergeGroup(c, 7, BuildTool.Translate); start = GizmoCenter();
            end = camera.WorldToScreenPoint(WPFMonoBehaviour.ScreenToZ0(start) + Vector3.left * 2.88f);
            editor.HandlePointer(c, new GuiManager.Pointer { down = true, position = start }, 32, false, true);
            editor.HandlePointer(c, new GuiManager.Pointer { up = true, position = end }, 32.1f, false, false);
            Check(GridlessBuild.GetGroups(c).Count == 1 && editor.SelectedGroup == 7 && Near(b.transform.localPosition, new Vector2(1.12f, 0)),
                "machine release applies its final endpoint and commits one merged frame even without an intermediate drag event");
            editor.Undo(); Call(editor, "LateUpdate");
            Check(GridlessBuild.GetGroups(c).Count == 2 && a.SerializeCustomParams() == beforeA && b.SerializeCustomParams() == beforeB,
                "one undo restores both pre-drag poses and exact pre-merge membership/parameter keys");
            var rotated = MergeMachine("RotationMergePointer"); MergePart(rotated, 0, 0, Vector2.zero);
            BasePart rotating = MergePart(rotated, 4, 7, new Vector2(1.12f, 0), 37);
            SelectMergeGroup(rotated, 7, BuildTool.RotateMachine);
            Check(GridlessGroupMerge.TryMerge(rotated, 7) == 0, "a rotated face with no overlapping connection circle remains separate");
            PartTurn(rotated, -37);
            Check(GridlessBuild.GetGroups(rotated).Count == 1 && Mathf.Abs(Mathf.DeltaAngle(rotating.transform.localEulerAngles.z, 0)) < 0.01f,
                "rotating a face into circle overlap merges frames when the rotation is released");
            editor.Undo();
            Check(GridlessBuild.GetGroups(rotated).Count == 2 && Mathf.Abs(Mathf.DeltaAngle(rotating.transform.localEulerAngles.z, 37)) < 0.01f,
                "rotation undo restores the independent frame and its original arbitrary angle");
            var single = MergeMachine("SinglePartMergePointer", true);
            MergePart(single, 0, 0, Vector2.zero); BasePart moved = MergePart(single, 4, 7, new Vector2(4, 0));
            BasePart neighbor = MergePart(single, 5, 7, new Vector2(5, 0));
            foreach (BasePart p in single.Parts) { p.gameObject.AddComponent<BoxCollider>().size = Vector3.one * 0.8f; p.gameObject.SetActive(true); }
            BuildToolPalette.Select(BuildTool.TranslatePart); editor.HandlePointer(single, new GuiManager.Pointer(), 33, false, false); SelectPart(single, moved);
            PartDrag(single, GizmoCenter(), new Vector2(-2.88f, 0));
            Check(GridlessBuild.GetGroups(single).Count == 1 && editor.SelectedPart == moved && GridlessBuild.GetGroupId(neighbor) == 7,
                "single-part translation contact merges its whole original group while keeping the part selected");
            editor.Undo();
            Check(GridlessBuild.GetGroups(single).Count == 2 && moved.m_coordX == 4 && Near(moved.transform.localPosition, new Vector2(4, 0)),
                "single-part merge undo restores both part occupancy and independent-group membership");
            single.gameObject.SetActive(false);
        }
        finally
        {
            settings.GeneralSettings.MergeGridlessGroups = previous;
            editor.HandlePointer(machine, new GuiManager.Pointer(), 34, false, false); BuildToolPalette.Clear();
        }
    }
    private static void GroupMergeRuntime()
    {
        bool previous = settings.GeneralSettings.MergeGridlessGroups;
        FieldInfo runtimeSettings = typeof(INSettings).GetField("s_runtimeSettings", Static);
        object oldSettings = runtimeSettings.GetValue(null);
        Type dataType = typeof(INSettings).GetNestedType("SettingData", BindingFlags.NonPublic);
        Type scopeType = typeof(INSettings).GetNestedType("SettingScope", BindingFlags.NonPublic);
        Type containerType = typeof(INSettings).GetNestedType("SettingDataContainer", BindingFlags.NonPublic);
        object container = Activator.CreateInstance(containerType, new object[] { Enum.GetNames(typeof(INFeature)).Length });
        foreach (INFeature feature in Enum.GetValues(typeof(INFeature)))
            containerType.GetMethod("SetData").Invoke(container, new[] { (object)feature,
                Activator.CreateInstance(dataType, new object[] { Enum.ToObject(scopeType, 3),
                    feature == INFeature.ConnectionStrength ? (Variant)new Variant<float>(1f) : new Variant<bool>(false) }) });
        FieldInfo gameData = typeof(WPFMonoBehaviour).GetField("s_gameData", Static); object oldData = gameData.GetValue(null);
        var data = ScriptableObject.CreateInstance<GameData>(); data.m_jointConnectionStrengthWeak = 1000f;
        runtimeSettings.SetValue(null, container); gameData.SetValue(null, data);
        try
        {
            settings.GeneralSettings.NoGridMode = true; settings.GeneralSettings.MergeGridlessGroups = true;
            var c = MergeMachine("MergedRuntimeBlueprint");
            MergePart(c, 0, 0, Vector2.zero); MergePart(c, 8, 7, new Vector2(1.12f, 0), 90);
            GridlessGroupMerge.TryMerge(c, 7);
            BuildMachineTransform.Apply(c, new BuildMachineTransform { angle = 37, translation = new Vector2(2, 1) }, 7);
            var running = MergeMachine("MergedRuntimeClone");
            foreach (BasePart original in c.Parts)
            {
                BasePart clone = Object.Instantiate(original); objects.Add(clone.gameObject); clone.transform.SetParent(running.transform, false);
                clone.contraption = running; running.Parts.Add(clone); Call(running, "RestoreSimulationPartPose", clone); clone.EnsureRigidbody();
            }
            Check(Near(running.Parts[0].transform.localPosition, c.Parts[0].transform.localPosition) &&
                Near(running.Parts[1].transform.localPosition, c.Parts[1].transform.localPosition) &&
                Mathf.Abs(Mathf.DeltaAngle(running.Parts[1].transform.localEulerAngles.z, c.Parts[1].transform.localEulerAngles.z)) < 0.001f,
                "runtime pose recovery keeps both merged lattices at their displayed build positions and angles");
            settings.GeneralSettings.MergeGridlessGroups = false;
            GridlessGroupMerge.ConnectRuntime(running);
            var links = (List<Contraption.JointConnection>)typeof(Contraption).GetField("m_jointMap", Private).GetValue(running);
            Check(links.Count == 1 && links[0].joint.connectedBody != null && Mathf.Abs(links[0].joint.breakForce - 2000f) < 0.01f,
                "saved merge interfaces create a normal-strength runtime joint even when future merging is switched off");
            Vector3 anchorA = links[0].partA.transform.TransformPoint(links[0].joint.anchor);
            Vector3 anchorB = links[0].partB.transform.TransformPoint(links[0].joint.connectedAnchor);
            Check((anchorA - anchorB).sqrMagnitude < 0.00001f, "merged runtime joint anchors coincide at the actual circle-contact midpoint");
            GridlessGroupMerge.ConnectRuntime(running);
            Check(links.Count == 1, "multiple overlapping circles and repeated runtime setup do not create duplicate joints");
            var independent = MergeMachine("UnmergedRuntime");
            MergePart(independent, 0, 0, Vector2.zero); MergePart(independent, 8, 7, new Vector2(1.12f, 0));
            GridlessGroupMerge.ConnectRuntime(independent);
            Check(((List<Contraption.JointConnection>)typeof(Contraption).GetField("m_jointMap", Private).GetValue(independent)).Count == 0,
                "unmerged nearby machines receive no new runtime joints");
        }
        finally
        {
            runtimeSettings.SetValue(null, oldSettings); gameData.SetValue(null, oldData); Object.DestroyImmediate(data);
            settings.GeneralSettings.MergeGridlessGroups = previous;
        }
    }
    private static void Pointer()
    {
        Vector2 point = camera.WorldToScreenPoint(machine.Parts[1].transform.position);
        bool down = editor.HandlePointer(machine, new GuiManager.Pointer { secondaryDown = true, secondaryDragging = true, position = point }, 0f, true, false);
        Check(!down && !editor.Selected, "right-button down passes through to part deletion");
        bool release = editor.HandlePointer(machine, new GuiManager.Pointer { secondaryUp = true, position = point }, 0.1f, false, false);
        Check(!release && !editor.Selected, "right-button release creates no selection gesture");
        Vector2 a = point - Vector2.one * 100, b = point + Vector2.one * 100;
        editor.HandlePointer(machine, new GuiManager.Pointer { secondaryDown = true, secondaryDragging = true, position = a }, 1f, true, false);
        editor.HandlePointer(machine, new GuiManager.Pointer { secondaryDragging = true, position = b }, 1.4f, true, false);
        release = editor.HandlePointer(machine, new GuiManager.Pointer { secondaryUp = true, position = b }, 1.5f, false, false);
        Check(!release && !editor.Selected, "long right-drag never starts a machine marquee");
        int count = machine.Parts.Count;
        editor.HandlePointer(machine, new GuiManager.Pointer { secondaryDown = true, onWidget = true, position = point }, 2, true, false);
        editor.HandlePointer(machine, new GuiManager.Pointer { secondaryUp = true, onWidget = true, position = point }, 2.5f, false, false);
        Check(machine.Parts.Count == count && !editor.Selected, "right-clicking UI cannot select the machine");
        editor.HandlePointer(machine, new GuiManager.Pointer { secondaryDown = true, secondaryDragging = true, position = a }, 3, true, false);
        editor.HandlePointer(machine, new GuiManager.Pointer { secondaryUp = true, position = b }, 3.1f, false, false);
        Check(!editor.Selected, "a fast right drag also leaves machine selection idle");
        BuildToolPalette.Select(BuildTool.SelectMachine);
        editor.HandlePointer(machine, new GuiManager.Pointer { down = true, position = a }, 4, false, true);
        editor.HandlePointer(machine, new GuiManager.Pointer { position = b }, 4.1f, false, true);
        release = editor.HandlePointer(machine, new GuiManager.Pointer { up = true, position = b }, 4.2f, false, false);
        Check(release && editor.Selected, "toolbar selection still accepts a left-button marquee");
        BuildToolPalette.Clear();
    }

    private static void Deletion()
    {
        var c = New("DeletionMachine", true).AddComponent<Contraption>(); Call(c, "PostAwake");
        var owner = New("DeletionUI").AddComponent<ConstructionUI>(); typeof(ConstructionUI).GetField("m_contraption", Private).SetValue(owner, c);
        BasePart a = Part(6, 3, c), b = Part(8, 3, c), immovable = Part(10, 3, c);
        foreach (BasePart p in new[] { a, b, immovable })
        {
            var box = p.gameObject.AddComponent<BoxCollider>(); box.size = new Vector3(0.6f, 0.6f, 0.5f); p.gameObject.SetActive(true);
        }
        immovable.m_static = true; c.RefreshPartPositions(); Physics.SyncTransforms();
        Vector3 near = camera.WorldToScreenPoint(a.transform.position + Vector3.right * 0.45f);
        var exact = typeof(ConstructionUI).GetMethod("PickBuildPart", Private);
        var enlarged = typeof(ConstructionUI).GetMethod("PickDeletionPart", Private);
        Check(exact.Invoke(owner, new object[] { near }) == null && enlarged.Invoke(owner, new object[] { near }) as BasePart == a, "Windows right deletion reaches 0.15 cells beyond a collider edge");
        Vector3 far = camera.WorldToScreenPoint(a.transform.position + Vector3.up * 0.8f);
        Check(enlarged.Invoke(owner, new object[] { far }) == null, "enlarged deletion radius remains bounded");
        Call(owner, "HandlePartDeletion", new GuiManager.Pointer { secondaryDown = true, onWidget = true, position = near });
        Check(c.Parts.Count == 3, "right deletion never passes through a UI widget");
        Call(owner, "HandlePartDeletion", new GuiManager.Pointer { secondaryDown = true, position = near });
        Check(c.Parts.Count == 2 && !c.Parts.Contains(a), "right-button down immediately deletes the nearest part");
        Call(owner, "HandlePartDeletion", new GuiManager.Pointer { secondaryDragging = true, position = camera.WorldToScreenPoint(b.transform.position) });
        Check(c.Parts.Count == 1 && !c.Parts.Contains(b), "holding and sweeping the right button deletes the next part");
        Call(owner, "HandlePartDeletion", new GuiManager.Pointer { secondaryDragging = true, position = camera.WorldToScreenPoint(immovable.transform.position) });
        Check(c.Parts.Contains(immovable), "right sweep preserves static level parts");
        c.gameObject.SetActive(false);
    }
    private static (BasePart outer, BasePart inner) DeletionPair(Contraption c)
    {
        BasePart outer = Part(0, 0, c), inner = Part(0, 0, c);
        outer.enclosedPart = inner; c.SetPartMap(0, 0, 0, outer);
        return (outer, inner);
    }
    private static void DeletionOwnership()
    {
        var c = New("DeletionOwnership").AddComponent<Contraption>(); Call(c, "PostAwake");
        var pair = DeletionPair(c);
        Check(c.TryRemovePart(pair.inner) && c.FindPartAt(0, 0) == pair.outer && pair.outer.enclosedPart == null,
            "removing the inner instance preserves the outer cell and unlinks both enclosure references");
        Check(pair.inner != null && !c.TryRemovePart(pair.inner) && c.Parts.Count == 1,
            "a removed but not yet destroyed instance cannot be removed twice");
        Check(c.RemovePartAt(0, 0) == pair.outer && c.FindPartAt(0, 0) == null && c.Parts.Count == 0,
            "subsequent cell deletion removes the remaining outer instance exactly once");
        Check(pair.outer != null && c.RemovePartAt(0, 0) == null,
            "empty-cell deletion cannot return an old live reference before deferred destruction");
        pair = DeletionPair(c);
        Check(c.TryRemovePart(pair.outer) && c.FindPartAt(0, 0) == pair.inner && pair.inner.enclosedInto == null,
            "removing the outer instance lets its surviving inner instance own the cell");
        Check(!c.TryRemovePart(pair.outer) && c.FindPartAt(0, 0) == pair.inner,
            "replaying an outer deletion cannot delete its surviving inner instance");
        Check(c.RemovePartAt(0, 0) == pair.inner && c.Parts.Count == 0, "outer-first deletion removes both actual instances");
        BasePart fresh = Part(0, 0, c); c.SetPartMap(0, 0, 0, fresh);
        Check(!c.TryRemovePart(pair.outer) && !c.TryRemovePart(pair.inner) && c.FindPartAt(0, 0) == fresh && c.Parts.Count == 1,
            "old deletion requests cannot remove a newly placed part at the same cell");
        BasePart overlap = Part(0, 0, c); c.SetPartMap(0, 0, 0, overlap);
        Check(c.TryRemovePart(overlap) && c.FindPartAt(0, 0) == fresh,
            "overlapping unlinked instances retain a surviving cell owner after deletion");
        BasePart generated = Part(0, 0, c); generated.GenerationIndex = 3; c.SetPartMap(0, 0, 3, generated);
        Check(c.TryRemovePart(generated) && c.FindPartAt(0, 0, 3) == null && c.FindPartAt(0, 0) == fresh,
            "deletion clears only the matching generation's cell");
        Call(c, "ClearBuildParts");
        Check(c.Parts.Count == 0 && c.FindPartAt(0, 0) == null && c.FindPartAt(0, 0, 3) == null,
            "undo rebuild preparation clears all member and cell indexes together");
    }
    private static ConstructionUI.PartDesc DeletionDescriptor(ConstructionUI ui, BasePart template, int used, int max = 3)
    {
        var desc = new ConstructionUI.PartDesc { part = template, useCount = used, maxCount = max, customPartIndex = template.customPartIndex };
        ui.PartDescriptors.Add(desc); return desc;
    }
    private static bool DeleteBuildPart(ConstructionUI ui, BasePart part) =>
        (bool)typeof(ConstructionUI).GetMethod("RemoveBuildPart", Private).Invoke(ui, new object[] { part });
    private static void DeletionInventory()
    {
        var c = New("DeletionInventory").AddComponent<Contraption>(); Call(c, "PostAwake");
        var ui = New("DeletionInventoryUI").AddComponent<ConstructionUI>();
        typeof(ConstructionUI).GetField("m_contraption", Private).SetValue(ui, c);
        LevelManager level = WPFMonoBehaviour.levelManager;
        var mode = level.CurrentGameMode; Contraption oldProto = mode.ContraptionProto;
        FieldInfo uiField = typeof(LevelManager).GetField("m_constructionUI", Private);
        object oldUI = uiField.GetValue(level);
        FieldInfo runtimeSettings = typeof(INSettings).GetField("s_runtimeSettings", Static);
        object oldSettings = runtimeSettings.GetValue(null);
        Type dataType = typeof(INSettings).GetNestedType("SettingData", BindingFlags.NonPublic);
        Type scopeType = typeof(INSettings).GetNestedType("SettingScope", BindingFlags.NonPublic);
        Type containerType = typeof(INSettings).GetNestedType("SettingDataContainer", BindingFlags.NonPublic);
        object container = Activator.CreateInstance(containerType, new object[] { Enum.GetNames(typeof(INFeature)).Length });
        Action<INFeature, bool> set = (feature, value) => containerType.GetMethod("SetData").Invoke(container, new[] { (object)feature,
            Activator.CreateInstance(dataType, new object[] { Enum.ToObject(scopeType, 3), new Variant<bool>(value) }) });
        foreach (INFeature feature in Enum.GetValues(typeof(INFeature))) set(feature, false);
        var changes = new List<PartCountChanged>();
        EventManager.OnEvent<PartCountChanged> onCount = e => changes.Add(e);
        int removals = 0; EventManager.OnEvent<PartRemovedEvent> onRemoved = e => removals++;
        EventManager.Connect(onCount); EventManager.Connect(onRemoved);
        runtimeSettings.SetValue(null, container); uiField.SetValue(level, ui);
        typeof(GameMode).GetProperty("ContraptionProto").SetValue(mode, c);
        try
        {
            set(INFeature.PartCounter, true);
            var pair = DeletionPair(c); var desc = DeletionDescriptor(ui, Part(), 2);
            INContraption.RemoveParts(new[] { pair.outer, pair.outer, pair.inner, pair.inner }, out int removed);
            Check(removed == 2 && c.Parts.Count == 0 && c.FindPartAt(0, 0) == null && desc.useCount == 0,
                "same-cell batch removal counts two instances once despite duplicate input references");
            Check(changes.Count == 2 && changes[0].count == 1 && changes[1].count == 0 && removals == 2,
                "same-type overlapping deletion emits exactly one inventory and removal event per instance (2 to 1 to 0)");
            Check(!INContraption.RemovePart(0, 0) && !DeleteBuildPart(ui, pair.outer) && changes.Count == 2 && desc.useCount == 0,
                "repeated coordinate and instance deletions cannot return inventory again");

            changes.Clear(); pair = DeletionPair(c); desc.useCount = 2;
            INContraption.RemoveParts(c.Parts, out removed);
            Check(removed == 2 && c.Parts.Count == 0 && desc.useCount == 0 && changes.Count == 2,
                "batch removal snapshots the live Parts list before deleting its overlapping members");

            changes.Clear(); pair = DeletionPair(c); desc.useCount = 2;
            INContraption.RemoveParts(new[] { pair.inner, pair.outer }, out removed);
            Check(removed == 2 && desc.useCount == 0 && c.FindPartAt(0, 0) == null,
                "inner-first batch deletion has the same inventory result as outer-first deletion");

            changes.Clear(); pair = DeletionPair(c); desc.useCount = 2;
            var selection = New("DeletionSelection").AddComponent<INSelectionManager>();
            typeof(INSelectionManager).GetField("m_state", Private).SetValue(selection, INSelectionManager.State.Selected);
            selection.DeleteSelection();
            Check(c.Parts.Count == 0 && c.FindPartAt(0, 0) == null && desc.useCount == 0 && changes.Count == 2,
                "the real selection delete path clears overlapping instances and their cell without extra count events");

            changes.Clear(); ui.PartDescriptors.Clear(); pair = DeletionPair(c);
            pair.inner.m_partType = BasePart.PartType.Pig;
            var frameDesc = DeletionDescriptor(ui, Part(), 1);
            BasePart pigTemplate = Part(); pigTemplate.m_partType = BasePart.PartType.Pig;
            var pigDesc = DeletionDescriptor(ui, pigTemplate, 1);
            var duplicateDesc = DeletionDescriptor(ui, Part(), 1);
            INContraption.RemoveParts(new[] { pair.outer, pair.inner }, out removed);
            Check(removed == 2 && frameDesc.useCount == 0 && pigDesc.useCount == 0 && duplicateDesc.useCount == 1,
                "an outer frame and different-type inner part each return one item to only one matching inventory row");

            changes.Clear(); ui.PartDescriptors.Clear(); set(INFeature.PartCounter, false);
            pair = DeletionPair(c); desc = DeletionDescriptor(ui, Part(), 2);
            INContraption.RemoveParts(new[] { pair.outer, pair.inner }, out removed);
            Check(desc.CurrentCount == 3 && changes.Count == 2 && changes[0].count == 2 && changes[1].count == 3,
                "remaining-inventory display restores exactly two items after overlapping deletion");

            changes.Clear(); set(INFeature.PartCounter, true);
            BasePart stale = Part(0, 0, c); c.SetPartMap(0, 0, 0, stale); desc.useCount = 1;
            c.TryRemovePart(stale); desc.useCount = 0;
            BasePart fresh = Part(0, 0, c); c.SetPartMap(0, 0, 0, fresh); desc.useCount = 1;
            Check(stale != null && !DeleteBuildPart(ui, stale) && changes.Count == 0 && desc.useCount == 1 && c.FindPartAt(0, 0) == fresh,
                "inventory rejects a still-live stale reference without affecting its fresh replacement");
            fresh.m_static = true;
            Check(!DeleteBuildPart(ui, fresh) && desc.useCount == 1 && changes.Count == 0,
                "static-part deletion cannot consume inventory or emit a count event");
            fresh.m_static = false; desc.useCount = 0;
            Check(DeleteBuildPart(ui, fresh) && desc.useCount == 0 && changes.Count == 1 && changes[0].count == 0,
                "a legacy zero usage count does not underflow on a confirmed removal");

            changes.Clear(); ui.PartDescriptors.Clear();
            var customA = New("CustomDeleteA").AddComponent<RCR2Port.RoverPortPart>();
            var customB = New("CustomDeleteB").AddComponent<RCR2Port.RoverPortPart>();
            customA.m_partType = customB.m_partType = BasePart.PartType.RCR2Cargo;
            customA.customPartIndex = RCR2Port.RoverPortCatalog.FirstPartIndex;
            customB.customPartIndex = customA.customPartIndex + 1;
            var aDesc = DeletionDescriptor(ui, customA, 0); var bDesc = DeletionDescriptor(ui, customB, 0);
            BasePart a = Object.Instantiate(customA); BasePart b = Object.Instantiate(customB);
            objects.Add(a.gameObject); objects.Add(b.gameObject);
            foreach (BasePart part in new[] { a, b }) { part.contraption = c; part.transform.SetParent(c.transform, false); c.Parts.Add(part); }
            Call(selection, "RefreshPartCounters");
            Check(aDesc.useCount == 1 && bDesc.useCount == 1 && changes.Count == 2 && changes[0].customPartIndex != changes[1].customPartIndex,
                "selection count refresh keeps same-type custom catalog indexes independent");
            changes.Clear();
            Check(DeleteBuildPart(ui, a) && aDesc.useCount == 0 && bDesc.useCount == 1 && changes.Count == 1 &&
                changes[0].customPartIndex == customA.customPartIndex, "custom deletion returns inventory only to the exact catalog index");
            Check(DeleteBuildPart(ui, b) && bDesc.useCount == 0, "overlapping custom instances each return their own inventory item");
            Check(changes.TrueForAll(e => e.count >= 0), "custom inventory deletion never emits a negative count");
        }
        finally
        {
            EventManager.Disconnect(onCount); EventManager.Disconnect(onRemoved);
            runtimeSettings.SetValue(null, oldSettings); uiField.SetValue(level, oldUI);
            typeof(GameMode).GetProperty("ContraptionProto").SetValue(mode, oldProto);
        }
    }
    private static void Gizmo()
    {
        editor.SelectMachine(); Call(editor, "LateUpdate");
        var field = typeof(INMachineBuildEditor).GetField("m_pivotScreen", Private);
        Vector2 center = (Vector2)field.GetValue(editor);
        Vector2 start = center + Vector2.right * (48f * Mathf.Max(0.6f, Screen.height / 1080f));
        editor.HandlePointer(machine, new GuiManager.Pointer { down = true, position = start }, 3, false, true);
        editor.HandlePointer(machine, new GuiManager.Pointer { position = start + new Vector2(70, 40) }, 3.1f, false, true);
        editor.HandlePointer(machine, new GuiManager.Pointer { up = true, position = start + new Vector2(70, 40) }, 3.2f, false, false);
        var translated = BuildMachineTransform.Read(machine);
        Check(Mathf.Abs(translated.translation.x) > 0.01f && Mathf.Abs(translated.translation.y) < 0.001f, "red arrow drag moves only X");
        editor.Undo(); Check(Near(BuildMachineTransform.Read(machine).translation, Vector2.zero), "machine translation can be undone");
        Call(editor, "LateUpdate"); center = (Vector2)field.GetValue(editor);
        float radius = 78f * Mathf.Max(0.6f, Screen.height / 1080f);
        Vector2 ring = center + new Vector2(-radius, 0);
        editor.HandlePointer(machine, new GuiManager.Pointer { down = true, position = ring }, 4, false, true);
        editor.HandlePointer(machine, new GuiManager.Pointer { position = center + new Vector2(0, -radius) }, 4.1f, false, true);
        editor.HandlePointer(machine, new GuiManager.Pointer { up = true, position = center + new Vector2(0, -radius) }, 4.2f, false, false);
        Check(Mathf.Abs(Mathf.DeltaAngle(BuildMachineTransform.Read(machine).angle, 90)) < 0.01f, "rotation ring turns position and orientation by 90 degrees");
        editor.Undo(); Check(Mathf.Abs(BuildMachineTransform.Read(machine).angle) < 0.01f, "machine rotation can be undone");
    }
    private static void Panel()
    {
        BasePart part = machine.Parts[1]; var panel = INPartParameterPanel.Open(part); objects.Add(panel.gameObject);
        Check(panel.transform.Find("Backdrop") == null, "parameter panel creates no darkening backdrop");
        var rect = panel.transform.Find("Panel").GetComponent<RectTransform>();
        Check(rect.anchorMin == new Vector2(1, 1) && rect.pivot == new Vector2(1, 1), "parameter panel is anchored at screen right");
        Check(!INSelectionManager.PauseBoxDrawing, "opening panel does not globally pause construction/camera input");
        Check(panel.GetComponentsInChildren<Slider>().Length == 3, "panel exposes uniform, X and Y live sliders");
        Call(panel, "OnValueSubmitted", BasePart.PartScaleXKey, "1.8");
        Check(Mathf.Abs(part.transform.localScale.x - 1.8f) < 0.001f, "X slider/input immediately changes the selected part");
        Call(panel, "OnValueSubmitted", BasePart.PartScaleYKey, "NaN");
        Check(BuildMachineTransform.Finite(part.transform.localScale.y), "non-finite panel values are rejected");
        Call(panel, "OnSaveClicked"); Check(INPartParameterPanel.IsOpen, "Apply keeps the panel open for background editing");
    }
    private static void ToggleSelection()
    {
        var toolbar = New("ToggleToolbar").AddComponent<INBuildToolPalette>();
        foreach (BuildTool tool in new[] { BuildTool.SelectMachine, BuildTool.Translate, BuildTool.RotateMachine })
        {
            BuildToolPalette.Clear(); Call(toolbar, "OnPick", tool); editor.SelectMachine();
            Call(toolbar, "OnPick", tool);
            Check(!editor.Selected && BuildToolPalette.Current == BuildTool.None, "clicking active " + tool + " again cancels machine selection");
        }
        Call(toolbar, "OnPick", BuildTool.Translate); Call(editor, "LateUpdate");
        Vector2 center = (Vector2)typeof(INMachineBuildEditor).GetField("m_pivotScreen", Private).GetValue(editor);
        BuildMachineTransform before = BuildMachineTransform.Read(machine);
        editor.HandlePointer(machine, new GuiManager.Pointer { down = true, position = center }, 5, false, true);
        editor.HandlePointer(machine, new GuiManager.Pointer { position = center + Vector2.right * 70f }, 5.1f, false, true);
        Check(!Near(BuildMachineTransform.Read(machine).translation, before.translation), "toggle-cancel test starts an actual machine drag");
        Call(toolbar, "OnPick", BuildTool.Translate);
        Check(!editor.Selected && Near(BuildMachineTransform.Read(machine).translation, before.translation), "toolbar toggle cancels an unfinished drag and restores its starting pose");
        BuildToolPalette.Clear();
    }
    private static Vector2 GizmoCenter() => (Vector2)typeof(INMachineBuildEditor).GetField("m_pivotScreen", Private).GetValue(editor);
    private static float GizmoScale => Mathf.Max(0.6f, camera.pixelHeight / 1080f);
    private static void SelectPart(Contraption c, BasePart part)
    {
        Vector2 point = camera.WorldToScreenPoint(part.transform.position);
        editor.HandlePointer(c, new GuiManager.Pointer { down = true, position = point }, 10f, false, true);
        editor.HandlePointer(c, new GuiManager.Pointer { up = true, position = point }, 10.1f, false, false);
        Call(editor, "LateUpdate");
    }
    private static void PartDrag(Contraption c, Vector2 handle, Vector2 worldDelta, bool shift = false)
    {
        Vector2 startWorld = WPFMonoBehaviour.ScreenToZ0(handle);
        Vector2 screenEnd = camera.WorldToScreenPoint((Vector3)(startWorld + worldDelta));
        editor.HandlePointer(c, new GuiManager.Pointer { down = true, position = handle }, 11f, false, true);
        editor.HandlePointer(c, new GuiManager.Pointer { position = screenEnd }, 11.1f, false, true, shift: shift);
        editor.HandlePointer(c, new GuiManager.Pointer { up = true, position = screenEnd }, 11.2f, false, false, shift: shift);
        Call(editor, "LateUpdate");
    }
    private static void PartTurn(Contraption c, float delta, bool shift = false)
    {
        Vector2 center = GizmoCenter(), radial = Vector2.left * 78f * GizmoScale;
        Vector2 end = center + BuildMachineTransform.Rotate(radial, delta);
        editor.HandlePointer(c, new GuiManager.Pointer { down = true, position = center + radial }, 12, false, true);
        // The release endpoint must be applied even when there was no intervening drag event.
        editor.HandlePointer(c, new GuiManager.Pointer { up = true, position = end }, 12.2f, false, false, shift: shift);
        Call(editor, "LateUpdate");
    }
    private static void PartTools()
    {
        var c = New("SinglePartTools", true).AddComponent<Contraption>(); Call(c, "PostAwake");
        BasePart p = Part(-1, 2, c), neighbor = Part(3, 2, c), remote = Part(6, 4, c);
        GridlessBuild.SetGroupId(remote, 7);
        foreach (BasePart part in c.Parts)
        {
            part.gameObject.AddComponent<BoxCollider>().size = new Vector3(0.8f, 0.8f, 0.4f);
            part.gameObject.SetActive(true);
        }
        var frame = new BuildMachineTransform { angle = 37f, translation = new Vector2(1, 0.2f) };
        BuildMachineTransform.Apply(c, frame, 0); c.RefreshPartPose(remote);
        p.SetCustomParam(BasePart.PartScaleXKey, 1.2f); p.SetCustomParam(BasePart.PartScaleYKey, 0.8f);
        string originalData = p.SerializeCustomParams(); Vector2 original = p.transform.localPosition;
        Vector2 neighborPosition = neighbor.transform.localPosition, remotePosition = remote.transform.localPosition;
        editor.HandlePointer(c, new GuiManager.Pointer(), 9, false, false);
        BuildToolPalette.Select(BuildTool.TranslatePart);
        Vector2 click = camera.WorldToScreenPoint(p.transform.position);
        editor.HandlePointer(c, new GuiManager.Pointer { down = true, onWidget = true, position = click }, 9.1f, false, true);
        Check(editor.SelectedPart == null, "part tools never pick through a UI widget");
        SelectPart(c, p);
        Check(editor.SelectedPart == p && !editor.Selected && p.SerializeCustomParams() == originalData,
            "part translation selects the actual collider without rotating, replacing, or selecting the vehicle");
        Check((bool)typeof(INMachineBuildEditor).GetMethod("WantsMachinePointer", Private).Invoke(editor, new object[] { click }),
            "part tool captures the first press before camera panning can start");
        PartDrag(c, GizmoCenter() + Vector2.right * 48f * GizmoScale, new Vector2(1.6f, 0.7f));
        Check(Near(p.transform.localPosition, original + Vector2.right * 1.6f), "single-part red arrow moves only global X on a 37-degree vehicle");
        Check(Near(neighbor.transform.localPosition, neighborPosition) && Near(remote.transform.localPosition, remotePosition),
            "part translation leaves other members and independent vehicles fixed");
        Check(c.FindPartAt(-1, 2) == null && c.FindPartAt(p.m_coordX, p.m_coordY) == p && c.Parts.Count == 3,
            "part translation releases its old logical cell and preserves map ownership/count");
        editor.Undo(); Call(editor, "LateUpdate");
        Check(Near(p.transform.localPosition, original) && c.FindPartAt(-1, 2) == p && p.SerializeCustomParams() == originalData,
            "undo restores original cells, fractional offsets, and absence of internal keys");
        PartDrag(c, GizmoCenter() + Vector2.up * 48f * GizmoScale, new Vector2(0.7f, 1.4f));
        Check(Near(p.transform.localPosition, original + Vector2.up * 1.4f), "single-part green arrow moves only global Y");
        editor.Undo(); Call(editor, "LateUpdate");
        PartDrag(c, GizmoCenter(), new Vector2(1.4f, -0.9f));
        Check(Near(p.transform.localPosition, original + new Vector2(1.4f, -0.9f)), "single-part center handle preserves free two-axis translation");
        editor.Undo(); Call(editor, "LateUpdate");
        PartDrag(c, GizmoCenter(), new Vector2(0.76f, 0.23f), true);
        Check(Near(p.transform.localPosition, original + Vector2.right), "Shift snaps single-part displacement to half-cell increments");
        editor.Undo(); Call(editor, "LateUpdate");
        PartDrag(c, GizmoCenter(), neighborPosition - original);
        Check(Near(p.transform.localPosition, original) && c.FindPartAt(3, 2) == neighbor && c.FindPartAt(-1, 2) == p,
            "moving onto another logical cell cannot overwrite or delete its part");
        BuildToolPalette.Select(BuildTool.RotatePart); Call(editor, "LateUpdate");
        Check(editor.SelectedPart == p, "switching between part translate and rotate retains the same selected part");
        PartTurn(c, 73f);
        Check(Mathf.Abs(Mathf.DeltaAngle(FineRotate.GetAngle(p, 0), 73f)) < 0.01f &&
            Mathf.Abs(Mathf.DeltaAngle(p.transform.localEulerAngles.z, 110f)) < 0.01f,
            "part ring applies 73 degrees exactly once with fine-step disabled and composes the 37-degree machine angle");
        Check(Near(p.transform.localPosition, original) && Near(neighbor.transform.localPosition, neighborPosition) &&
            Mathf.Abs(Mathf.DeltaAngle(neighbor.transform.localEulerAngles.z, 37f)) < 0.001f,
            "single-part rotation keeps its center and all other part poses fixed");
        var restored = New("RestoredSinglePart").AddComponent<Contraption>(); Call(restored, "PostAwake");
        BasePart loaded = Part(p.m_coordX, p.m_coordY, restored); loaded.DeserializeCustomParams(p.SerializeCustomParams());
        Check(Near(loaded.transform.localPosition, p.transform.localPosition) && Quaternion.Angle(loaded.transform.localRotation, p.transform.localRotation) < 0.01f,
            "part angle and translated vehicle frame survive save/load");
        BuildToolPalette.Select(BuildTool.TranslatePart); Call(editor, "LateUpdate"); PartDrag(c, GizmoCenter(), new Vector2(1.2f, -0.7f));
        Contraption runtime = c.Clone(); objects.Add(runtime.gameObject); Call(runtime, "PostAwake"); bool stable = true;
        foreach (BasePart part in runtime.GetComponentsInChildren<BasePart>(true))
        {
            Vector2 before = part.transform.localPosition; Quaternion rotation = part.transform.localRotation;
            int x = part.m_coordX, y = part.m_coordY;
            Call(runtime, "RestoreSimulationPartPose", part);
            stable &= Near(part.transform.localPosition, before) && Quaternion.Angle(part.transform.localRotation, rotation) < 0.01f &&
                part.m_coordX == x && part.m_coordY == y;
        }
        Check(stable, "runtime pose recovery preserves both single-part movement and continuous rotation inside a rotated vehicle");
        Vector2 translatedPart = p.transform.localPosition;
        BuildToolPalette.Select(BuildTool.Translate); editor.SelectMachine(); Call(editor, "LateUpdate");
        PartDrag(c, GizmoCenter(), new Vector2(0.9f, 0.6f));
        editor.Undo();
        Check(Near(p.transform.localPosition, translatedPart) && Near(BuildMachineTransform.Read(c, 0).translation, frame.translation),
            "shared undo restores the latest vehicle movement before earlier part movement");
        editor.Undo();
        Check(Near(p.transform.localPosition, original) && Mathf.Abs(Mathf.DeltaAngle(FineRotate.GetAngle(p, 0), 73)) < 0.01f,
            "next shared undo restores part movement without erasing its preceding rotation");
        BuildToolPalette.Select(BuildTool.RotatePart); SelectPart(c, p);
        PartTurn(c, 28f, true);
        Check(Mathf.Abs(Mathf.DeltaAngle(FineRotate.GetAngle(p, 0), 118)) < 0.01f, "Shift defaults to Besiege's 45-degree part rotation increment");
        editor.Undo(); Call(editor, "LateUpdate"); settings.GeneralSettings.FineRotationStep = 10;
        PartTurn(c, 23f, true);
        Check(Mathf.Abs(Mathf.DeltaAngle(FineRotate.GetAngle(p, 0), 93)) < 0.01f, "part ring respects configured fine-rotation snap increments");
        editor.Undo(); settings.GeneralSettings.FineRotationStep = 0;
        BuildToolPalette.Select(BuildTool.TranslatePart); SelectPart(c, neighbor); editor.Undo();
        Check(editor.SelectedPart == neighbor && p.SerializeCustomParams() == originalData,
            "undo targets the recorded part after selection changes and restores native rotation state");
        var toolbar = New("PartToggleToolbar").AddComponent<INBuildToolPalette>();
        foreach (BuildTool tool in new[] { BuildTool.TranslatePart, BuildTool.RotatePart })
        {
            BuildToolPalette.Select(tool); SelectPart(c, p);
            Call(toolbar, "OnPick", tool);
            Check(editor.SelectedPart == null && BuildToolPalette.Current == BuildTool.None, "clicking active " + tool + " again deselects the part");
        }
        BuildToolPalette.Select(BuildTool.TranslatePart); SelectPart(c, p);
        Vector2 handle = GizmoCenter(); Vector2 endDrag = camera.WorldToScreenPoint((Vector3)(original + new Vector2(1.3f, -0.7f)));
        editor.HandlePointer(c, new GuiManager.Pointer { down = true, position = handle }, 13, false, true);
        editor.HandlePointer(c, new GuiManager.Pointer { position = endDrag }, 13.1f, false, true);
        Call(toolbar, "OnPick", BuildTool.TranslatePart);
        Check(Near(p.transform.localPosition, original) && p.SerializeCustomParams() == originalData && editor.SelectedPart == null,
            "toolbar toggle rolls an unfinished part translation back completely");
        BuildToolPalette.Select(BuildTool.RotatePart); SelectPart(c, p);
        Vector2 center = GizmoCenter(), radial = Vector2.left * 78f * GizmoScale;
        editor.HandlePointer(c, new GuiManager.Pointer { down = true, position = center + radial }, 14, false, true);
        editor.HandlePointer(c, new GuiManager.Pointer { position = center + BuildMachineTransform.Rotate(radial, 53f) }, 14.1f, false, true);
        editor.HandlePointer(c, new GuiManager.Pointer(), 14.2f, false, false, escape: true);
        Check(p.SerializeCustomParams() == originalData && editor.SelectedPart == null && BuildToolPalette.Current == BuildTool.None,
            "Escape cancels an unfinished part rotation and restores its original enum/angle");
        BuildToolPalette.Select(BuildTool.TranslatePart); SelectPart(c, p);
        bool right = editor.HandlePointer(c, new GuiManager.Pointer { secondaryDown = true, position = click }, 15, true, false);
        Check(!right && editor.SelectedPart == null, "right-button deletion still owns the gesture while a part tool is active");
        SelectPart(c, p); PartDrag(c, GizmoCenter(), new Vector2(1.3f, -0.7f));
        c.RemovePartAt(p.m_coordX, p.m_coordY); editor.HandlePointer(c, new GuiManager.Pointer(), 15.1f, false, false); editor.Undo();
        Check(editor.SelectedPart == null && c.Parts.Count == 2 && c.FindPartAt(-1, 2) == null,
            "deleted selections and undo records never resurrect a removed part");
        foreach (BasePart part in new[] { p, neighbor, remote }) part.gameObject.SetActive(false);
        c.gameObject.SetActive(false);
        editor.HandlePointer(machine, new GuiManager.Pointer(), 16, false, false); BuildToolPalette.Clear();
    }
    private static void NativeFineAngles()
    {
        var c = New("NativeFineAngleMachine").AddComponent<Contraption>(); Call(c, "PostAwake");
        FieldInfo runtimeSettings = typeof(INSettings).GetField("s_runtimeSettings", Static);
        object oldSettings = runtimeSettings.GetValue(null);
        Type dataType = typeof(INSettings).GetNestedType("SettingData", BindingFlags.NonPublic);
        Type scopeType = typeof(INSettings).GetNestedType("SettingScope", BindingFlags.NonPublic);
        Type containerType = typeof(INSettings).GetNestedType("SettingDataContainer", BindingFlags.NonPublic);
        object container = Activator.CreateInstance(containerType, new object[] { Enum.GetNames(typeof(INFeature)).Length });
        foreach (INFeature feature in Enum.GetValues(typeof(INFeature)))
            containerType.GetMethod("SetData").Invoke(container, new[] { (object)feature,
                Activator.CreateInstance(dataType, new object[] { Enum.ToObject(scopeType, 3), new Variant<bool>(false) }) });
        runtimeSettings.SetValue(null, container);
        try
        {
        foreach (Type type in new[] { typeof(BasePart), typeof(Tail), typeof(Wings), typeof(IntegratedCircuitPart) })
        {
            BasePart part = (BasePart)New("Fine_" + type.Name).AddComponent(type);
            part.contraption = c; part.transform.SetParent(c.transform, false); c.Parts.Add(part);
            if (part is Wings)
            {
                New("TopFrameSprite").transform.SetParent(part.transform, false);
                New("BottomFrameSprite").transform.SetParent(part.transform, false);
                part.gameObject.AddComponent<BoxCollider>();
            }
            if (part is IntegratedCircuitPart circuit) typeof(IntegratedCircuitPart).GetField("m_canBeFlipped", Private).SetValue(circuit, true);
            new BuildMachineTransform { angle = 47f }.Store(part);
            FineRotate.SetAngle(part, 123f);
            Check(Mathf.Abs(Mathf.DeltaAngle(part.transform.localEulerAngles.z, 170f)) < 0.001f,
                type.Name + " specialized rotation composes an arbitrary part angle and machine angle");
            string stored = part.SerializeCustomParams(); part.SetRotation(part.m_gridRotation);
            Check(stored == part.SerializeCustomParams() && Mathf.Abs(Mathf.DeltaAngle(part.transform.localEulerAngles.z, 170f)) < 0.001f,
                type.Name + " pose refresh does not apply a continuous angle twice");
            FineRotate.SetAngle(part, float.NaN);
            Check(part.SerializeCustomParams() == stored, type.Name + " rejects non-finite ring rotation");
            float expectedStep = 170f + Mathf.DeltaAngle(part.GetRotationAngle(part.m_gridRotation), 180f);
            part.SetRotation(BasePart.GridRotation.Deg_180);
            Check(Mathf.Abs(Mathf.DeltaAngle(part.transform.localEulerAngles.z, expectedStep)) < 0.001f,
                type.Name + " native step still advances a saved continuous angle");
        }
        }
        finally { runtimeSettings.SetValue(null, oldSettings); }
    }
    private static void EnclosedPartTools()
    {
        var c = New("EnclosedPartTools", true).AddComponent<Contraption>(); Call(c, "PostAwake");
        BasePart frame = Part(0, 1, c), inner = Part(0, 1, c); frame.enclosedPart = inner;
        var frameCollider = frame.gameObject.AddComponent<BoxCollider>();
        var innerCollider = inner.gameObject.AddComponent<BoxCollider>();
        frame.gameObject.SetActive(true); inner.gameObject.SetActive(true); c.RefreshPartPositions();
        editor.HandlePointer(c, new GuiManager.Pointer(), 20, false, false);
        BuildToolPalette.Select(BuildTool.TranslatePart); frameCollider.enabled = false; SelectPart(c, inner); frameCollider.enabled = true;
        PartDrag(c, GizmoCenter(), Vector2.right * 2f);
        Check(inner.enclosedInto == null && frame.enclosedPart == null && c.FindPartAt(0, 1) == frame && c.FindPartAt(2, 1) == inner,
            "moving an inner part releases its enclosure while preserving the original frame cell");
        editor.Undo();
        Check(inner.enclosedInto == frame && frame.enclosedPart == inner && c.FindPartAt(0, 1) == frame && c.FindPartAt(2, 1) == null,
            "undo restores an inner part's enclosure and cell ownership");
        BuildToolPalette.Clear(); BuildToolPalette.Select(BuildTool.TranslatePart);
        innerCollider.enabled = false; SelectPart(c, frame); innerCollider.enabled = true;
        PartDrag(c, GizmoCenter(), Vector2.right * 2f);
        Check(inner.enclosedInto == null && frame.enclosedPart == null && c.FindPartAt(0, 1) == inner && c.FindPartAt(2, 1) == frame &&
            Near(inner.transform.localPosition, new Vector2(0, 1)), "moving a containing frame alone leaves its inner part in place and promotes it in the map");
        editor.Undo();
        Check(inner.enclosedInto == frame && frame.enclosedPart == inner && c.FindPartAt(0, 1) == frame && c.FindPartAt(2, 1) == null,
            "undo restores a moved containing frame without losing or duplicating its inner part");
        c.gameObject.SetActive(false); editor.HandlePointer(machine, new GuiManager.Pointer(), 21, false, false); BuildToolPalette.Clear();
    }
    private static void TranslateBoundary()
    {
        editor.SelectMachine(); GridlessBuild.TryGetGroupBounds(machine, editor.SelectedGroup, out Rect bounds, 0f);
        Vector2 corner = camera.WorldToScreenPoint(machine.transform.TransformPoint(BuildMachineTransform.Read(machine).TransformPoint(bounds.min)));
        Func<bool> hasInnerCorner = () =>
        {
            object graphic = editor.GetComponentInChildren<INBuildOverlayGraphic>();
            var segments = (System.Collections.IEnumerable)typeof(INBuildOverlayGraphic).GetField("m_lines", Private).GetValue(graphic);
            foreach (object segment in segments)
            {
                Type type = segment.GetType(); Color color = (Color)type.GetField("color").GetValue(segment);
                if (color != INBuildStyle.Brass) continue;
                if (Near((Vector2)type.GetField("a").GetValue(segment), corner) || Near((Vector2)type.GetField("b").GetValue(segment), corner)) return true;
            }
            return false;
        };
        BuildToolPalette.Select(BuildTool.SelectMachine); editor.SelectMachine(); Call(editor, "LateUpdate");
        Check(hasInnerCorner(), "boundary audit detects the selected vehicle's solid frame in marquee mode");
        BuildToolPalette.Select(BuildTool.Translate); Call(editor, "LateUpdate");
        Check(!hasInnerCorner(), "translate-machine mode omits the inner solid frame while retaining gizmos and outer dashed bounds");
        editor.DeselectMachine(); BuildToolPalette.Clear();
    }
    private static void StateEncodedRotation()
    {
        foreach (BasePart.GridRotation native in new[] { BasePart.GridRotation.Deg_0, BasePart.GridRotation.Deg_90 })
        {
            var part = New("StateEncodedCharge").AddComponent<PointChargePart>();
            var sprite = part.gameObject.AddComponent<INSerializedSprite>();
            new BuildMachineTransform { angle = 37f }.Store(part); part.SetRotation(native);
            Quaternion original = part.transform.localRotation; string variant = sprite.SpriteName;
            FineRotate.SetAngle(part, part.GetRotationAngle(native) + 73f);
            Quaternion expected = Quaternion.AngleAxis(73f, Vector3.forward) * original;
            Check(part.m_gridRotation == native && sprite.SpriteName == variant && Quaternion.Angle(part.transform.localRotation, expected) < 0.001f,
                "single-part rotation preserves the native charge sprite/layout at " + native + " and turns its whole pose by 73 degrees");
            part.SetRotation(part.m_gridRotation);
            Check(Quaternion.Angle(part.transform.localRotation, expected) < 0.001f,
                "state-encoded part keeps its exact continuous rotation on pose refresh at " + native);
        }
    }
    private static void GridVisibility()
    {
        var owner = New("AuditConstruction").AddComponent<ConstructionUI>();
        var grid = New("GridCellGeometry"); grid.transform.SetParent(owner.transform, false);
        var cell = New("GridCell"); cell.transform.SetParent(grid.transform, false);
        typeof(ConstructionUI).GetField("m_grid", Private).SetValue(owner, grid.transform);
        typeof(ConstructionUI).GetField("m_cellMap", Private).SetValue(owner, new Dictionary<int, Transform> { { 0, cell.transform } });
        settings.GeneralSettings.NoGridMode = true; Call(owner, "ApplyGridlessGridVisibility");
        Check(!grid.activeSelf && !cell.activeSelf, "free-build hides both GridCell objects and the grid geometry root");
        settings.GeneralSettings.NoGridMode = false; Call(owner, "ApplyGridlessGridVisibility");
        Check(grid.activeSelf && cell.activeSelf, "grid restores when free-build is disabled");
        settings.GeneralSettings.NoGridMode = true;
    }
    private static void RenderMerge()
    {
        bool previous = settings.GeneralSettings.MergeGridlessGroups;
        var renderers = Object.FindObjectsOfType<Renderer>(); var visible = new bool[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) { visible[i] = renderers[i].enabled; renderers[i].enabled = false; }
        var cubes = new List<GameObject>(); var materials = new List<Material>();
        var rt = new RenderTexture(1920, 1080, 24); rt.Create(); camera.targetTexture = rt;
        var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false); RenderTexture old = RenderTexture.active;
        try
        {
            INPartParameterPanel.Close();
            settings.GeneralSettings.NoGridMode = true; settings.GeneralSettings.MergeGridlessGroups = true;
            var c = MergeMachine("MergeRender");
            MergePart(c, 1, 0, new Vector2(1, 1), y: 1); MergePart(c, 1, 0, new Vector2(1, 2), y: 2);
            MergePart(c, 8, 7, new Vector2(2.12f, 1), 90); MergePart(c, 9, 7, new Vector2(2.12f, 2), 90);
            foreach (BasePart part in c.Parts)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); objects.Add(cube); cubes.Add(cube);
                cube.transform.position = part.transform.position; cube.transform.rotation = part.transform.rotation;
                cube.transform.localScale = new Vector3(0.85f, 0.85f, 0.3f);
                var mat = new Material(Shader.Find("Unlit/Color")); materials.Add(mat);
                mat.color = GridlessBuild.GetGroupId(part) == 7 ? new Color(0.60f, 0.46f, 0.28f) : new Color(0.44f, 0.51f, 0.34f);
                cube.GetComponent<Renderer>().sharedMaterial = mat;
            }
            var toolbar = Object.FindObjectOfType<INBuildToolPalette>();
            SelectMergeGroup(c, 7, BuildTool.Translate); Call(toolbar, "Update"); Canvas.ForceUpdateCanvases();
            var toolbarRoot = (GameObject)typeof(INBuildToolPalette).GetField("m_canvasRoot", Private).GetValue(toolbar);
            Check(toolbarRoot.transform.Find("Tools/Tool_MergeGridless").GetComponent<Image>().color == INBuildStyle.Brass && BuildToolPalette.Current == BuildTool.Translate,
                "toolbar displays the merge switch independently of the selected transform tool");
            var graphic = editor.GetComponentInChildren<INBuildOverlayGraphic>();
            int withCircles = graphic.canvasRenderer.GetMesh().vertexCount;
            settings.GeneralSettings.MergeGridlessGroups = false; Call(editor, "LateUpdate"); Canvas.ForceUpdateCanvases();
            Check(graphic.canvasRenderer.GetMesh().vertexCount < withCircles,
                "switching merging off removes the connection-circle overlay without removing dashed frames or transform handles");
            settings.GeneralSettings.MergeGridlessGroups = true; Call(editor, "LateUpdate"); Canvas.ForceUpdateCanvases();
            Check(withCircles < 65000, "connection-circle overlay stays below the Unity UI vertex limit");
            foreach (string name in new[] { "build-merge-contact-preview.png", "build-merged-preview.png" })
            {
                if (name == "build-merged-preview.png") { GridlessGroupMerge.TryMerge(c, 7); Call(editor, "LateUpdate"); Canvas.ForceUpdateCanvases(); }
                camera.Render(); RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); texture.Apply();
                File.WriteAllBytes("Builds/QA/" + name, texture.EncodeToPNG());
            }
            Check(GridlessBuild.GetGroups(c).Count == 1, "visual preview renders a single merged boundary after overlapping contacts are committed");
        }
        finally
        {
            settings.GeneralSettings.MergeGridlessGroups = previous;
            for (int i = 0; i < renderers.Length; i++) if (renderers[i]) renderers[i].enabled = visible[i];
            foreach (GameObject cube in cubes) cube.SetActive(false);
            foreach (Material mat in materials) Object.DestroyImmediate(mat);
            RenderTexture.active = old; camera.targetTexture = null; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(texture);
            editor.HandlePointer(machine, new GuiManager.Pointer(), 35, false, false); BuildToolPalette.Clear();
        }
    }
    private static void Render()
    {
        // Actual runtime UI is rendered with a QA context, without loading/changing the user's scene.
        var toolbar = New("AuditToolbar", true).AddComponent<INBuildToolPalette>(); Call(toolbar, "Awake");
        BuildToolPalette.Select(BuildTool.Translate);
        Call(toolbar, "Update");
        BuildMachineTransform.Apply(machine, new BuildMachineTransform { angle = 25f, translation = new Vector2(-0.2f, 0.3f) });
        BasePart separate = PlaceWorld(machine, new Vector2(5.5f, 1.2f));
        PlaceWorld(machine, new Vector2(6.5f, 1.2f)); PlaceWorld(machine, new Vector2(5.5f, 2.2f));
        Check(GridlessBuild.GetGroups(machine).Count == 2 && BuildMachineTransform.Read(separate).angle == 0f,
            "preview includes a 25-degree vehicle and a new independent zero-degree vehicle");
        foreach (BasePart part in machine.Parts)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); objects.Add(cube);
            cube.transform.position = part.transform.position; cube.transform.rotation = part.transform.rotation;
            cube.transform.localScale = Vector3.Scale(new Vector3(0.85f, 0.85f, 0.3f), part.transform.lossyScale);
            var material = new Material(Shader.Find("Unlit/Color")); material.color = new Color(0.62f, 0.43f, 0.25f);
            cube.GetComponent<Renderer>().sharedMaterial = material;
        }
        var rt = new RenderTexture(1920, 1080, 24); rt.Create(); camera.targetTexture = rt;
        foreach (Canvas canvas in Object.FindObjectsOfType<Canvas>())
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>(); if (scaler != null) Call(scaler, "Handle");
        }
        Canvas.ForceUpdateCanvases(); editor.SelectMachine(); Call(editor, "LateUpdate"); Canvas.ForceUpdateCanvases();
        var toolbarRoot = (GameObject)typeof(INBuildToolPalette).GetField("m_canvasRoot", Private).GetValue(toolbar);
        Check(toolbarRoot.GetComponent<Canvas>().isRootCanvas, "toolbar anchors to the screen independently of the auxiliary menu canvas");
        Check(toolbarRoot.transform.Find("Tools/Tool_Translate").GetComponent<Image>().color == INBuildStyle.Brass,
            "the rendered toolbar highlights its actual active transform tool");
        var iconMesh = toolbarRoot.GetComponentInChildren<INBuildToolIcon>().canvasRenderer.GetMesh();
        Check(iconMesh != null && iconMesh.vertexCount > 0, "toolbar symbols produce visible geometry");
        var overlayMesh = editor.GetComponentInChildren<INBuildOverlayGraphic>().canvasRenderer.GetMesh();
        Check(overlayMesh != null && overlayMesh.vertexCount > 0, "dashed boundary and machine gizmo produce visible geometry");
        bool allCorners = true;
        foreach (var group in GridlessBuild.GetGroups(machine))
        {
            Vector2 corner = camera.WorldToScreenPoint(machine.transform.TransformPoint(group.frame.TransformPoint(group.bounds.min - Vector2.one)));
            bool found = false;
            foreach (Vector3 vertex in overlayMesh.vertices) if (((Vector2)vertex - corner).sqrMagnitude < 16f) { found = true; break; }
            allCorners &= found;
        }
        Check(allCorners, "every independent dashed frame renders at its own transformed corner");
        camera.Render(); var old = RenderTexture.active; RenderTexture.active = rt;
        var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false); texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); texture.Apply();
        Directory.CreateDirectory("Builds/QA"); File.WriteAllBytes("Builds/QA/build-interface-preview.png", texture.EncodeToPNG());
        Check(texture.width == 1920 && texture.height == 1080, "actual UI renders into a 1920x1080 preview");
        BasePart target = machine.Parts[0]; target.gameObject.AddComponent<BoxCollider>();
        target.gameObject.SetActive(true); machine.gameObject.SetActive(true);
        foreach (BuildTool tool in new[] { BuildTool.TranslatePart, BuildTool.RotatePart })
        {
            BuildToolPalette.Select(tool);
            if (editor.SelectedPart != target) SelectPart(machine, target);
            Call(toolbar, "Update"); Call(editor, "LateUpdate"); Canvas.ForceUpdateCanvases();
            camera.Render(); RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); texture.Apply();
            string name = tool == BuildTool.TranslatePart ? "build-part-translate-preview.png" : "build-part-rotate-preview.png";
            File.WriteAllBytes("Builds/QA/" + name, texture.EncodeToPNG());
            Check(editor.SelectedPart == target && toolbarRoot.transform.Find("Tools/Tool_" + tool).GetComponent<Image>().color == INBuildStyle.Brass,
                "actual collider selection renders the " + tool + " handles and active toolbar button");
        }
        RenderTexture.active = old; camera.targetTexture = null; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(texture);
    }
}
