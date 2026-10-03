using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static partial class BuildInterfaceAudit
{
    private static void RuntimeSettings(Action action)
    {
        var field = typeof(INSettings).GetField("s_runtimeSettings", Static);
        object old = field.GetValue(null);
        Type data = typeof(INSettings).GetNestedType("SettingData", BindingFlags.NonPublic);
        Type scope = typeof(INSettings).GetNestedType("SettingScope", BindingFlags.NonPublic);
        Type containerType = typeof(INSettings).GetNestedType("SettingDataContainer", BindingFlags.NonPublic);
        object container = Activator.CreateInstance(containerType, new object[] { Enum.GetNames(typeof(INFeature)).Length });
        foreach (INFeature feature in Enum.GetValues(typeof(INFeature)))
            containerType.GetMethod("SetData").Invoke(container, new[] { (object)feature,
                Activator.CreateInstance(data, new object[] { Enum.ToObject(scope, 3),
                    feature == INFeature.ConnectionStrength ? (Variant)new Variant<float>(1f) : new Variant<bool>(feature == INFeature.Rc1Controls) }) });
        var gameData = typeof(WPFMonoBehaviour).GetField("s_gameData", Static);
        object oldData = gameData.GetValue(null);
        var gd = ScriptableObject.CreateInstance<GameData>(); gd.m_jointConnectionStrengthWeak = 1000;
        field.SetValue(null, container); gameData.SetValue(null, gd);
        try { action(); }
        finally { field.SetValue(null, old); gameData.SetValue(null, oldData); Object.DestroyImmediate(gd); }
    }

    private static Transform ConnectionPoint(BasePart part, Vector3 position, string name = "Point-Left")
    {
        Transform root = New("ConnectionPoints").transform; root.SetParent(part.transform, false);
        Transform point = New(name).transform; point.SetParent(root, false); point.localPosition = position;
        return point;
    }

    private static RCR2Port.RoverPortPart PortPart(Contraption c, int x = 1)
    {
        var part = New("AuditPort").AddComponent<RCR2Port.RoverPortPart>();
        part.transform.SetParent(c.transform, false); part.contraption = c; c.Parts.Add(part);
        part.m_coordX = x; part.m_jointConnectionType = BasePart.JointConnectionType.Source;
        part.m_jointConnectionDirection = BasePart.JointConnectionDirection.Any;
        part.m_mass = 1; return part;
    }

    private static void PortMergeConnections() => RuntimeSettings(() => {
        foreach (float angle in new[] { 0f, 37f, 90f, 213f })
        {
            var c = MergeMachine("RotatedPortAssembly");
            BasePart chassis = MergePart(c, 0, 3, Vector2.zero, angle);
            var port = PortPart(c); GridlessBuild.SetGroupId(port, 3);
            new BuildMachineTransform { angle = angle }.Store(port);
            c.RefreshPartPose(port); PartRotationAnimator.Complete(port.transform);
            ConnectionPoint(port, Vector3.left * 0.5f);
            Check(PartConnections.TryGetConnection(port, chassis, out Vector3 anchor, out BasePart.Direction direction)
                && direction == BasePart.Direction.Left, "ported chassis connection survives " + angle + " degree machine rotation");
            Check(PartConnections.TryGetConnection(chassis, port, out _, out var reverse) && reverse == BasePart.Direction.Right,
                "rotated native/ported connection is symmetric at " + angle + " degrees");
            port.EnsureRigidbody(); chassis.EnsureRigidbody();
            port.CreateCustomJoints(); Check(c.JointMap.Count == 1, "runtime restores original port/chassis joint at " + angle + " degrees");
        }
        var scaled = MergeMachine("FineScaledConnection");
        BasePart native = MergePart(scaled, 0, 0, Vector2.zero, 37);
        native.SetCustomParam(BasePart.PartScaleXKey, 1.7f); native.SetCustomParam(BasePart.PartScaleYKey, 0.6f);
        native.transform.localRotation *= Quaternion.Euler(0, 0, 23);
        var circles = new List<GridlessGroupMerge.Circle>(); GridlessGroupMerge.GetCircles(native, circles);
        var attached = PortPart(scaled);
        var p = ConnectionPoint(attached, Vector3.zero); p.position = circles[0].position;
        Check(PartConnections.TryGetConnection(attached, native, out _, out _), "native faces match after independent axis scaling and fine rotation");
        p.position += Vector3.right * 0.2f;
        Check(!PartConnections.TryGetConnection(attached, native, out _, out _), "port/native tolerance does not widen to the group merge circle radius");

        var mountMachine = MergeMachine("MergedServoMount");
        var servo = PortPart(mountMachine, 0); servo.SetStoredParam(GridlessBuild.LatticeKey, 0);
        var mount = New("Platform").transform; mount.SetParent(servo.transform, false); mount.gameObject.AddComponent<Rigidbody>();
        Transform upper = ConnectionPoint(servo, Vector3.right * 0.5f, "Point-Top");
        var nodes = (Dictionary<string, Transform>)typeof(RCR2Port.RoverPortPart).GetField("m_nodes", Private).GetValue(servo);
        nodes["point"] = upper; nodes["platform"] = mount;
        servo.SetConnectionMounts(new[] { new RCR2Port.RoverPortMountData { point = "Point-Top", body = "Platform" } });
        BasePart neighbor = MergePart(mountMachine, 8, 0, Vector2.right); neighbor.SetStoredParam(GridlessBuild.LatticeKey, 7);
        servo.EnsureRigidbody(); neighbor.EnsureRigidbody();
        GridlessGroupMerge.ConnectRuntime(mountMachine);
        Check(mount.GetComponent<FixedJoint>() != null && servo.GetComponent<FixedJoint>() == null,
            "merged interface attaches to imported servo platform instead of the root chassis");

        var wheelMachine = MergeMachine("MergedSpringWheel");
        var wheel = PortPart(wheelMachine, 0); wheel.SetStoredParam(GridlessBuild.LatticeKey, 0);
        wheel.m_jointConnectionDirection = BasePart.JointConnectionDirection.Right;
        typeof(RCR2Port.RoverPortPart).GetField("m_portData", Private).SetValue(wheel,
            new RCR2Port.RoverPortPartData { android_class = "AndroidMotorWheel", base_component = "OffRoadWheel",
                motion = new RCR2Port.RoverPortMotionData { spring_stiffness = 150 } });
        ConnectionPoint(wheel, Vector3.right * 0.5f);
        BasePart frame = MergePart(wheelMachine, 8, 0, Vector2.right); frame.SetStoredParam(GridlessBuild.LatticeKey, 7);
        wheel.EnsureRigidbody(); frame.EnsureRigidbody();
        wheelMachine.transform.rotation = Quaternion.Euler(0, 0, 63);
        GridlessGroupMerge.ConnectRuntime(wheelMachine);
        var joint = wheel.GetComponent<ConfigurableJoint>();
        Check(joint != null && Mathf.Abs(joint.linearLimitSpring.spring - 150) < 0.001f && Vector3.Dot(joint.axis, Vector3.right) > 0.99f,
            "merged imported wheel retains its source suspension and local spring axis after rotation");
    });

    private static void Rc1Interactions() => RuntimeSettings(() => {
        var controls = New("AuditRc1").AddComponent<INRc1Controls>();
        typeof(INRc1Controls).GetProperty("Instance").SetValue(null, controls);
        Call(controls, "BuildPanel");
        Call(controls, "SetShown", true);
        RectTransform throttle = (RectTransform)controls.transform.Find("Panel/Throttle");
        RectTransform jet = (RectTransform)controls.transform.Find("Panel/Jet");
        Check(jet.anchorMin == throttle.anchorMin && jet.anchoredPosition.y < throttle.anchoredPosition.y,
            "Jet is below Throttle in the left column");
        foreach (string name in new[] { "Forward", "Back" })
        {
            var rect = (RectTransform)controls.transform.Find("Panel/" + name);
            var hold = rect.GetComponent<IPointerDownHandler>(); Vector3 before = rect.localScale;
            Call(hold, "Awake");
            hold.OnPointerDown(new PointerEventData(null) { pointerId = -1 });
            Check(rect.localScale.x > before.x && rect.localScale.y > before.y, name + " enlarges while pressed");
            ((IPointerUpHandler)hold).OnPointerUp(new PointerEventData(null) { pointerId = -1 });
            Check(rect.localScale == before, name + " restores its size on release");
        }
        var owner = MergeMachine("NativeDriveOwner"); typeof(Contraption).GetField("m_running", Private).SetValue(owner, true);
        var level = WPFMonoBehaviour.levelManager;
        var mode = (GameMode)typeof(LevelManager).GetField("m_gameMode", Private).GetValue(level);
        Contraption previous = mode.ContraptionRunning; mode.ContraptionRunning = owner;
        var native = New("NativeMotor").AddComponent<MotorWheel>(); native.contraption = owner; owner.Parts.Add(native);
        native.ConnectedComponent = -1; native.m_maximumSpeed = 15f;
        native.SetEnabled(true); Call(controls, "SyncNativeDrive");
        Check(native.IsEnabled(), "idle RC1 controls preserve a native button's enabled state");
        native.SetEnabled(false); typeof(INRc1Controls).GetField("m_forward", Private).SetValue(controls, true);
        Call(controls, "SyncNativeDrive"); Check(native.IsEnabled(), "RC1 pedal temporarily enables native wheels");
        INRc1Controls.PrepareNativeTrigger(native); native.SetEnabled(true); Call(controls, "SyncNativeDrive");
        typeof(INRc1Controls).GetField("m_forward", Private).SetValue(controls, false); Call(controls, "SyncNativeDrive");
        Check(native.IsEnabled(), "native toggle while a pedal is held survives pedal release");
        native.SetEnabled(false); typeof(INRc1Controls).GetField("m_forward", Private).SetValue(controls, true); Call(controls, "SyncNativeDrive");
        typeof(INRc1Controls).GetField("m_forward", Private).SetValue(controls, false); Call(controls, "SyncNativeDrive");
        Check(!native.IsEnabled(), "pedal release restores the original disabled state");

        object previousInstance = typeof(Contraption).GetProperty("Instance").GetValue(null);
        typeof(Contraption).GetProperty("Instance").SetValue(null, owner);
        var gameState = typeof(LevelManager).GetField("m_gameState", Private); object previousState = gameState.GetValue(level);
        gameState.SetValue(level, LevelManager.GameState.Running);
        var buttonGo = New("NativeTrigger", false);
        var texture = New("PartTexture"); texture.AddComponent<RectTransform>(); texture.transform.SetParent(buttonGo.transform, false); texture.AddComponent<RawImage>();
        var index = New("Index"); index.AddComponent<RectTransform>(); index.transform.SetParent(buttonGo.transform, false); index.AddComponent<Text>();
        var buttonImage = New("Button"); buttonImage.AddComponent<RectTransform>(); buttonImage.transform.SetParent(buttonGo.transform, false);
        buttonImage.AddComponent<Image>(); buttonImage.AddComponent<UIButton>();
        var trigger = buttonGo.AddComponent<UIPartTriggerButton>(); Call(trigger, "Awake");
        trigger.SetInfo(new UIPartButtonInfo(UIPartButtonType.Trigger, 0, BasePart.PartType.MotorWheel, 0, -1));
        trigger.SetMultiple(true); trigger.Parts.Add(native);
        try
        {
            typeof(INRc1Controls).GetField("m_forward", Private).SetValue(controls, true); Call(controls, "SyncNativeDrive");
            trigger.OnTriggered(); Call(controls, "SyncNativeDrive");
            typeof(INRc1Controls).GetField("m_forward", Private).SetValue(controls, false); Call(controls, "SyncNativeDrive");
            Check(native.IsEnabled(), "actual UIPartTriggerButton can enable a wheel while the RC1 pedal is held");
            trigger.OnTriggered(); Call(controls, "SyncNativeDrive");
            Check(!native.IsEnabled(), "actual UIPartTriggerButton can disable a wheel while RC1 is idle");
        }
        finally
        {
            typeof(Contraption).GetProperty("Instance").SetValue(null, previousInstance);
            gameState.SetValue(level, previousState);
        }

        var powered = New("PortedDrive").AddComponent<RCR2Port.RoverPortPoweredPart>();
        powered.SetEnabled(true); Call(powered, "SetControlInput"); Check(powered.IsEnabled() && powered.InputThrottle > 0,
            "ported power part remains usable from native controls with RC1 enabled");
        powered.SetEnabled(false); typeof(INRc1Controls).GetField("m_forward", Private).SetValue(controls, true); Call(powered, "SetControlInput");
        Check(powered.IsEnabled(), "ported power part responds to RC1 pedal");
        typeof(INRc1Controls).GetField("m_forward", Private).SetValue(controls, false); Call(powered, "SetControlInput");
        Check(!powered.IsEnabled(), "ported power part releases temporary RC1 drive");
        mode.ContraptionRunning = previous; Object.DestroyImmediate(controls.gameObject);
    });

    private static void UiScaling()
    {
        float previous = settings.GeneralSettings.UiScale;
        var go = New("ScaleCanvas", true); var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
        try
        {
            settings.GeneralSettings.UiScale = 1; INUiScale.Register(canvas); float baseline = canvas.scaleFactor;
            settings.GeneralSettings.UiScale = 1.25f; INUiScale.Register(canvas);
            Check(Mathf.Abs(canvas.scaleFactor / baseline - 1.25f) < 0.001f, "UI multiplier composes with reference-resolution adaptation");
            INUiScale.Register(canvas); Check(scaler.referenceResolution == new Vector2(1536, 864), "UI scaling does not compound on repeated registration");
            Check(new GeneralSettings(settings.GeneralSettings).UiScale == 1.25f, "UI scale survives settings copy/save migration");
            settings.GeneralSettings.UiScale = float.NaN; Check(settings.GeneralSettings.UiScale == 1.25f, "invalid UI scale is rejected");
            settings.GeneralSettings.UiScale = 9; Check(settings.GeneralSettings.UiScale == 1.5f, "UI scale stays within the usable range");
            settings.GeneralSettings.UiScale = 1; INUiScale.Register(canvas);
            Check(Mathf.Abs(canvas.scaleFactor - baseline) < 0.001f, "resetting UI size restores the original canvas scale");
            INUiScale.Refresh();
            Canvas overlay = editor.GetComponentInChildren<INBuildOverlayGraphic>().GetComponentInParent<Canvas>();
            Check(overlay.GetComponent<INUiScale>() == null && overlay.scaleFactor == 1,
                "screen-projected machine bounds avoid double UI scaling");
            var panel = INPartParameterPanel.Open(machine.Parts[1]); objects.Add(panel.gameObject);
            settings.GeneralSettings.UiScale = 1.5f; INUiScale.Refresh(); Call(panel, "FitPanelToScreen");
            var panelRect = (RectTransform)panel.transform.Find("Panel");
            Check(panelRect.rect.height + 120f <= ((RectTransform)panel.transform).rect.height &&
                (int)typeof(INPartParameterPanel).GetField("RowCount", Private).GetValue(panel) < 10,
                "large UI keeps parameter-panel footer visible by reducing the visible rows");
            INPartParameterPanel.Close();
        }
        finally { settings.GeneralSettings.UiScale = previous; INUiScale.Refresh(); }
    }

    private static void RuntimeAllocations() => RuntimeSettings(() => {
        var owner = MergeMachine("AllocationOwner");
        object previous = typeof(Contraption).GetProperty("Instance").GetValue(null);
        typeof(Contraption).GetProperty("Instance").SetValue(null, owner);
        typeof(Contraption).GetField("m_connectedComponentCount", Private).SetValue(owner, 32);
        var box = New("FuelBox").AddComponent<AuditFuelContainer>(); box.contraption = owner; box.ConnectedComponent = 0; owner.Parts.Add(box);
        var fuel = new FuelSystem(); fuel.UpdateFuelParts();
        try
        {
            for (int i = 0; i < 10; i++) fuel.FixedUpdate();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) fuel.FixedUpdate();
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            report.Add("MEASURE FuelSystem 1000 steady physics steps: " + bytes + " managed bytes");
            Check(bytes == 0, "fuel distribution/refuelling allocates zero managed bytes after warm-up");
            var wrapper = owner.gameObject.AddComponent<INContraption>();
            var body = New("Body").AddComponent<Rigidbody>(); body.transform.SetParent(owner.transform, false); body.gameObject.SetActive(true);
            var first = wrapper.GetComponents<Rigidbody>();
            typeof(INContraption).GetField("m_componentListMap", Private).GetValue(wrapper);
            var componentCache = (Dictionary<Type, INContraption.ComponentData>)typeof(INContraption).GetField("m_componentListMap", Private).GetValue(wrapper);
            componentCache[typeof(Rigidbody)].Time = -1;
            Check(ReferenceEquals(first, wrapper.GetComponents<Rigidbody>()), "unchanged component discovery reuses its array across physics steps");
            before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) { componentCache[typeof(Rigidbody)].Time = -1; wrapper.GetComponents<Rigidbody>(); }
            bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            report.Add("MEASURE INContraption 1000 component scans: " + bytes + " managed bytes");
            Check(bytes == 0, "steady runtime component discovery allocates zero managed bytes");
            body.gameObject.SetActive(false); componentCache[typeof(Rigidbody)].Time = -1;
            Check(wrapper.GetComponents<Rigidbody>().Length == 0, "component cache still discovers disabled/detached physics bodies");
            var points = new List<PartConnections.Point>(); ConnectionPoint(box, Vector3.right * 0.5f);
            PartConnections.GetPoints(box, points); before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) PartConnections.GetPoints(box, points);
            bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Check(bytes == 0, "connection-circle geometry allocates zero managed bytes after warm-up");
            var tick = new AuditRuntimeBehaviour();
            typeof(INContraption).GetField("m_behaviours", Private).SetValue(wrapper, new List<INBehaviour> { tick });
            typeof(INContraption).GetField("m_enabled", Private).SetValue(wrapper, true);
            typeof(INContraption).GetField("m_status", Private).SetValue(wrapper, INBehaviour.StatusCode.Running);
            var update = (Action)Delegate.CreateDelegate(typeof(Action), wrapper, typeof(INContraption).GetMethod("Update", Private));
            var fixedUpdate = (Action)Delegate.CreateDelegate(typeof(Action), wrapper, typeof(INContraption).GetMethod("FixedUpdate", Private));
            var lateUpdate = (Action)Delegate.CreateDelegate(typeof(Action), wrapper, typeof(INContraption).GetMethod("LateUpdate", Private));
            update(); fixedUpdate(); lateUpdate(); before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) { update(); fixedUpdate(); lateUpdate(); }
            bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Check(bytes == 0 && tick.Ticks == 3003, "runtime behaviour dispatch avoids per-frame iterator allocations");
            typeof(INContraption).GetField("m_enabled", Private).SetValue(wrapper, false);
        }
        finally { typeof(Contraption).GetProperty("Instance").SetValue(null, previous); }
    });

    private static void AndroidTouchPointers()
    {
        INPartParameterPanel.Close();
        var manager = New("AndroidTouchGui").AddComponent<GuiManager>();
        var focusType = typeof(GuiManager).GetNestedType("FocusData", BindingFlags.NonPublic);
        var input = typeof(GuiManager).GetMethod("PointerInput", Private);
        var pointers = new List<GuiManager.Pointer>();
        var touchIds = new List<int>();
        var focuses = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(focusType));
        for (int i = 0; i < 4; i++)
        {
            pointers.Add(new GuiManager.Pointer { touching = true, down = true, up = true, dragging = true,
                secondaryDown = true, secondaryUp = true, secondaryDragging = true,
                doubleClick = true, onWidget = true, touchUsed = true, fingerId = 20 + i, position = Vector3.one });
            touchIds.Add(-1); focuses.Add(Activator.CreateInstance(focusType, true));
        }
        typeof(GuiManager).GetField("m_pointers", Private).SetValue(manager, pointers);
        typeof(GuiManager).GetField("m_touchIds", Private).SetValue(manager, touchIds);
        typeof(GuiManager).GetField("m_focusData", Private).SetValue(manager, focuses);
        for (int frame = 0; frame < 120; frame++) Call(manager, "TouchInput");
        Check(pointers.TrueForAll(p => !p.touching && !p.down && !p.up && !p.dragging &&
            !p.secondaryDown && !p.secondaryUp && !p.secondaryDragging && !p.doubleClick &&
            !p.onWidget && !p.touchUsed && p.widget == null && p.fingerId == -1 && p.position == Vector3.zero),
            "Android TouchInput runs 120 no-touch frames and clears all four null-focus slots");
        Check((int)typeof(GuiManager).GetField("m_touchCount", Private).GetValue(manager) == 0,
            "idle Android touch frames report zero active touches");
        var panel = INPartParameterPanel.Open(machine.Parts[1]); objects.Add(panel.gameObject);
        Canvas.ForceUpdateCanvases();
        var rect = (RectTransform)panel.transform.Find("Panel");
        Vector2 inside = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        var primary = focuses[0]; var secondary = focuses[1];
        focusType.GetField("primary").SetValue(primary, true);
        focusType.GetField("fingerId").SetValue(primary, 42);
        focusType.GetField("fingerId").SetValue(secondary, 43);
        input.Invoke(manager, new object[] { 0, true, true, false, true, (Vector3)inside, 42, primary, pointers[0] });
        input.Invoke(manager, new object[] { 1, true, true, false, true, Vector3.zero, 43, secondary, pointers[1] });
        for (int i = 2; i < 4; i++)
            input.Invoke(manager, new object[] { i, false, false, false, false, Vector3.zero, -1, null, pointers[i] });
        Check(pointers[0].onWidget && pointers[0].touchUsed && !pointers[1].onWidget && pointers[1].touching &&
            pointers[1].fingerId == 43 && !pointers[2].touching && !pointers[3].touching,
            "two Android touches retain separate UI ownership while remaining slots are idle");
        input.Invoke(manager, new object[] { 0, true, false, true, false, (Vector3)inside, 42, primary, pointers[0] });
        Check(!(bool)focusType.GetField("uiCaptured").GetValue(primary) &&
            (int)focusType.GetField("fingerId").GetValue(primary) == -1 && pointers[0].up && !pointers[0].touchUsed,
            "ended Android touch releases focus and panel capture");
        input.Invoke(manager, new object[] { 0, false, false, false, false, Vector3.zero, -1, null, pointers[0] });
        Check(!pointers[0].up && !pointers[0].onWidget && !pointers[0].touching,
            "released touch becomes an idle slot on the next frame");
        input.Invoke(manager, new object[] { 3, true, true, false, true, (Vector3)inside, 44, null, pointers[3] });
        Check(pointers[3].onWidget && pointers[3].touching,
            "pointer UI detection is safe even when an active focus record is unavailable");
        INPartParameterPanel.Close();

        Call(manager, "CancelInput");
        var hud = New("AndroidTouchHud", true).AddComponent<Camera>(); hud.gameObject.layer = 31;
        hud.transform.position = new Vector3(5000, 5000, -10); hud.orthographic = true; hud.orthographicSize = 5;
        hud.enabled = false;
        typeof(GuiManager).GetField("m_hudCamera", Private).SetValue(manager, hud);
        typeof(GuiManager).GetField("m_layerBottomZ", Private).SetValue(manager, 100f);
        var target = New("AndroidTouchButton", true); target.layer = 31;
        Vector2 buttonPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        target.transform.position = hud.ScreenToWorldPoint(new Vector3(buttonPosition.x, buttonPosition.y, 10));
        target.AddComponent<BoxCollider>(); var button = target.AddComponent<AuditLegacyWidget>();
        button.Multitouch = true; Physics.SyncTransforms();
        var processTouch = typeof(GuiManager).GetMethod("ProcessTouch", Private);
        Action<int, int, TouchPhase> touch = (slot, finger, phase) => processTouch.Invoke(manager,
            new object[] { slot, new Touch { fingerId = finger, phase = phase, position = buttonPosition } });
        Func<bool> cleared = () => {
            foreach (object focus in focuses)
                if ((int)focusType.GetField("fingerId").GetValue(focus) != -1 ||
                    (bool)focusType.GetField("primary").GetValue(focus) ||
                    (bool)focusType.GetField("uiCaptured").GetValue(focus) ||
                    focusType.GetField("target").GetValue(focus) != null ||
                    focusType.GetField("mouseOver").GetValue(focus) != null) return false;
            return touchIds.TrueForAll(id => id == -1) && pointers.TrueForAll(p =>
                !p.touching && !p.down && !p.up && !p.dragging && !p.secondaryDown &&
                !p.secondaryUp && !p.secondaryDragging && !p.doubleClick && !p.onWidget &&
                !p.touchUsed && p.widget == null && p.fingerId == -1 && p.position == Vector3.zero);
        };
        for (int round = 0; round < 8; round++)
        {
            for (int slot = 0; slot < 4; slot++) touch(slot, 100 + round * 4 + slot, TouchPhase.Began);
            Check(pointers.TrueForAll(p => p.widget == button && p.touchUsed) &&
                (int)focusType.GetField("fingerId").GetValue(focuses[0]) == 100 + round * 4,
                "four simultaneous touches retain reusable focus records, abandonment round " + round);
            Call(manager, "TouchInput");
            Check(cleared() && button.Releases == 0 && button.Leaves == (round + 1) * 4,
                "missing Ended/Canceled reclaims every focus without clicking, abandonment round " + round);
        }
        touch(0, 200, TouchPhase.Began);
        typeof(GuiManager).GetField("m_doubleClickActive", Private).SetValue(manager, true);
        touch(0, 200, TouchPhase.Canceled);
        Check(cleared() && button.Releases == 0 && button.Leaves == 33 &&
            !(bool)typeof(GuiManager).GetField("m_doubleClickActive", Private).GetValue(manager),
            "actual TouchPhase.Canceled cancels the pressed widget without Release or double click");
        touch(0, 201, TouchPhase.Began); touch(0, 201, TouchPhase.Ended);
        Check(button.Releases == 1 && pointers[0].up,
            "normal touch end still releases a pressed widget after cancellation");
        typeof(GuiManager).GetField("m_doubleClickActive", Private).SetValue(manager, true);
        Call(manager, "TouchInput");
        Check((bool)typeof(GuiManager).GetField("m_doubleClickActive", Private).GetValue(manager),
            "retiring a normally ended touch preserves the double-click window");

        Action seedLifecycle = () => {
            for (int slot = 0; slot < 4; slot++)
            {
                touch(slot, 300 + slot, TouchPhase.Began);
                focusType.GetField("uiCaptured").SetValue(focuses[slot], true);
                pointers[slot].secondaryDown = pointers[slot].secondaryUp = pointers[slot].secondaryDragging = true;
                pointers[slot].doubleClick = true;
            }
            typeof(GuiManager).GetField("m_touchCount", Private).SetValue(manager, 4);
            typeof(GuiManager).GetField("m_doubleClickActive", Private).SetValue(manager, true);
        };
        Action<string> checkLifecycle = reason => Check(cleared() && button.Releases == 1 &&
            (int)typeof(GuiManager).GetField("m_touchCount", Private).GetValue(manager) == 0 &&
            !(bool)typeof(GuiManager).GetField("m_doubleClickActive", Private).GetValue(manager), reason);
        seedLifecycle(); Call(manager, "OnApplicationFocus", false);
        Call(manager, "Update"); checkLifecycle("focus loss clears all input and blocks updates during Android settings");
        Call(manager, "OnApplicationPause", true); Call(manager, "OnApplicationFocus", true);
        Call(manager, "Update"); checkLifecycle("regaining focus while still paused cannot restore a stale gesture");
        Call(manager, "OnApplicationPause", false);
        seedLifecycle(); Call(manager, "OnApplicationPause", true);
        int pauseLeaves = button.Leaves; Call(manager, "OnApplicationPause", true);
        checkLifecycle("repeated Android pause callbacks are safe and clear all capture");
        Check(button.Leaves == pauseLeaves, "repeated pause does not deliver duplicate cancellation callbacks");
        Call(manager, "OnApplicationPause", false);
        seedLifecycle(); manager.IsEnabled = false; Call(manager, "Update");
        checkLifecycle("disabling input clears pointers, pressed widgets, capture and double clicks");
        manager.IsEnabled = true;
        seedLifecycle(); Call(manager, "OnDisable");
        checkLifecycle("disabling the GuiManager component also cancels active gestures");

        button.InputCallback = type => { if (type == InputEvent.EventType.Press) manager.IsEnabled = false; };
        int drags = button.Drags; touch(0, 400, TouchPhase.Began);
        checkLifecycle("a press callback that disables input cannot recreate focus after cancellation");
        Check(button.Drags == drags, "reentrant input cancellation stops the old press before drag dispatch");
        button.InputCallback = null; manager.IsEnabled = true;
        touch(0, 401, TouchPhase.Began); touch(0, 401, TouchPhase.Ended);
        Check(button.Releases == 2, "a fresh touch works normally after returning from settings or re-enabling input");
        Call(manager, "TouchInput");
    }

    private static void PanelInputOwnership()
    {
        var rect = (RectTransform)INPartParameterPanel.Open(machine.Parts[1]).transform.Find("Panel");
        Canvas.ForceUpdateCanvases(); Vector2 position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        Check(INPartParameterPanel.ContainsPointer(position), "parameter panel owns pointer positions over its controls");
        var manager = New("LegacyGui").AddComponent<GuiManager>();
        var hud = New("LegacyHudCamera", true).AddComponent<Camera>(); hud.gameObject.layer = 31;
        hud.transform.position = new Vector3(0, 0, -10); hud.orthographic = true; hud.orthographicSize = 5;
        hud.enabled = false;
        typeof(GuiManager).GetField("m_hudCamera", Private).SetValue(manager, hud);
        typeof(GuiManager).GetField("m_layerBottomZ", Private).SetValue(manager, 100f);
        var behind = New("BehindPanel", true); behind.layer = 31;
        behind.transform.position = hud.ScreenToWorldPoint(new Vector3(position.x, position.y, 10));
        behind.AddComponent<BoxCollider>(); var widget = behind.AddComponent<AuditLegacyWidget>(); Physics.SyncTransforms();
        var focusType = typeof(GuiManager).GetNestedType("FocusData", BindingFlags.NonPublic);
        object focus = Activator.CreateInstance(focusType, true); var pointer = new GuiManager.Pointer();
        focusType.GetField("primary").SetValue(focus, true);
        var input = typeof(GuiManager).GetMethod("PointerInput", Private);
        input.Invoke(manager, new object[] { 0, true, true, false, false, (Vector3)position, 0, focus, pointer });
        Check(pointer.onWidget && pointer.widget == null && pointer.touchUsed && widget.Presses == 0,
            "panel input blocks legacy buttons even without an active uGUI event system");
        INPartParameterPanel.Close();
        input.Invoke(manager, new object[] { 0, true, false, true, false, (Vector3)position, 0, focus, pointer });
        Check(pointer.onWidget && pointer.widget == null && widget.Releases == 0, "closing panel during a click cannot release a button behind it");
        focusType.GetField("primary").SetValue(focus, true);
        input.Invoke(manager, new object[] { 0, true, true, false, false, (Vector3)position, 0, focus, pointer });
        Check(pointer.widget == widget && widget.Presses == 1, "native widget underneath works normally after the panel closes");
        input.Invoke(manager, new object[] { 0, true, false, true, false, (Vector3)position, 0, focus, pointer });
        Check(widget.Releases == 1, "legacy button still receives a complete normal click outside overlay ownership");
    }
}

public sealed class AuditRuntimeBehaviour : INBehaviour
{
    public int Ticks;
    public override StatusCode Status => StatusCode.Running;
    public override void Update() => Ticks++;
    public override void FixedUpdate() => Ticks++;
    public override void LateUpdate() => Ticks++;
}

public sealed class AuditLegacyWidget : Widget
{
    public int Presses, Releases, Leaves, Drags;
    public bool Multitouch;
    public Action<InputEvent.EventType> InputCallback;
    public override bool AllowMultitouch() => Multitouch;
    protected override void OnInput(InputEvent input)
    {
        if (input.type == InputEvent.EventType.Press) Presses++;
        if (input.type == InputEvent.EventType.Release) Releases++;
        if (input.type == InputEvent.EventType.MouseLeave) Leaves++;
        if (input.type == InputEvent.EventType.Drag) Drags++;
        InputCallback?.Invoke(input.type);
    }
}

public sealed class AuditFuelContainer : BasePart, IFuelContainer
{
    public int FuelComponentIndex { get; set; }
    public float SupplyFuelAmount => 1f;
    public float RefuelingAmount => 1f;
    public void SupplyFuel(float amount) { }
    public void Refuel(float amount) { }
    public IEnumerable<BasePart> GetConnectedParts() { yield break; }
}
