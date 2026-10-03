using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

public static class MagnetRotationAudit
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;

    public static void Run()
    {
        var report = new List<string>();
        int failures = 0;
        Verify((ok, name) => { report.Add((ok ? "PASS " : "FAIL ") + name); if (!ok) failures++; });
        report.Add("FAILURES: " + failures);
        Directory.CreateDirectory("Builds/QA");
        File.WriteAllLines("Builds/QA/magnet-rotation-audit.txt", report);
        foreach (string line in report) Debug.Log("[magnet-rotation] " + line);
        if (failures != 0) throw new InvalidOperationException("Magnet rotation audit failed: " + failures);
    }

    public static void Verify(Action<bool, string> check)
    {
        var objects = new List<GameObject>();
        FieldInfo runtime = typeof(INSettings).GetField("s_runtimeSettings", Static);
        FieldInfo user = typeof(INUserSettings).GetField("<Instance>k__BackingField", Static);
        FieldInfo dataField = typeof(WPFMonoBehaviour).GetField("s_gameData", Static);
        object oldRuntime = runtime.GetValue(null), oldUser = user.GetValue(null), oldData = dataField.GetValue(null);
        var data = ScriptableObject.CreateInstance<GameData>();
        data.m_jointConnectionStrengthWeak = 1000f;
        var settings = new INUserSettings { GeneralSettings = new GeneralSettings() };
        Type itemType = typeof(INSettings).GetNestedType("SettingData", BindingFlags.NonPublic);
        Type scopeType = typeof(INSettings).GetNestedType("SettingScope", BindingFlags.NonPublic);
        Type containerType = typeof(INSettings).GetNestedType("SettingDataContainer", BindingFlags.NonPublic);
        object container = Activator.CreateInstance(containerType, new object[] { Enum.GetNames(typeof(INFeature)).Length });
        foreach (INFeature feature in Enum.GetValues(typeof(INFeature)))
            containerType.GetMethod("SetData").Invoke(container, new[] { (object)feature,
                Activator.CreateInstance(itemType, new object[] { Enum.ToObject(scopeType, 3),
                    feature == INFeature.ConnectionStrength ? (Variant)new Variant<float>(1f) : new Variant<bool>(false) }) });
        runtime.SetValue(null, container); user.SetValue(null, settings); dataField.SetValue(null, data);
        try
        {
            foreach (Type type in new[] { typeof(BasePart), typeof(CartWheel), typeof(Rope), typeof(Balloon),
                typeof(Tail), typeof(Wings), typeof(IntegratedCircuitPart), typeof(TNT), typeof(BlasterTNT),
                typeof(MechanicalGate), typeof(IndicatorPart), typeof(PowerTransmitterPart), typeof(SpringBoxingGlove),
                typeof(PartTrigger), typeof(GrapplingHook), typeof(Kicker), typeof(FuelTube), typeof(MagneticFieldGenerator) })
            {
                BasePart part = Part(type, objects);
                Contraption machine = part.contraption;
                bool specialized = type.GetMethod("SetRotation", new[] { typeof(BasePart.GridRotation) }).DeclaringType != typeof(BasePart) ||
                    type.GetMethod("SetRotation", new[] { typeof(int) }).DeclaringType != typeof(BasePart);
                BasePart.GridRotation native = type == typeof(MagneticFieldGenerator) ? BasePart.GridRotation.Deg_180 :
                    type == typeof(SpringBoxingGlove) || type == typeof(Tail) ||
                    type == typeof(Wings) || type == typeof(IntegratedCircuitPart) ? BasePart.GridRotation.Deg_90 :
                    specialized ? BasePart.GridRotation.Deg_45 : BasePart.GridRotation.Deg_0;
                if (type == typeof(Tail) || type == typeof(Wings) || type == typeof(IntegratedCircuitPart)) part.SetFlipped(true);
                part.SetRotation(native); PartRotationAnimator.Complete(part.transform);
                float initial = part.GetRotationAngle(part.m_gridRotation);
                Quaternion original = part.transform.localRotation;
                BasePart.GridRotation originalEnum = part.m_gridRotation;
                bool originalFlip = part.m_flipped, originalEightWay = part.m_eightWay;
                BasePart.AutoAlignType originalAlign = part.m_autoAlign;
                for (int step = 1; step <= 8; step++)
                {
                    bool turned = machine.Flip(part);
                    Quaternion expected = Quaternion.AngleAxis(-45f * step, Vector3.forward) * original;
                    check(turned && Near(part.transform.localRotation, expected) &&
                        Mathf.Abs(Mathf.DeltaAngle(FineRotate.GetAngle(part, -999f), initial - 45f * step)) < 0.001f,
                        type.Name + " clockwise magnet step " + step + " is exactly 45 degrees");
                    check(part.m_flipped == originalFlip && part.m_eightWay == originalEightWay && part.m_autoAlign == originalAlign &&
                        (!specialized || part.m_gridRotation == originalEnum), type.Name + " step " + step + " preserves native variant and rotation rules");
                    if (part is MagneticFieldGenerator field)
                        check(Mathf.Abs(field.MagneticFluxDensity - 0.02f) < 0.000001f,
                            "MagneticFieldGenerator step " + step + " preserves its selected magnetic polarity");
                    machine.AutoAlign(part); part.OnChangeConnections(); machine.RefreshPartPose(part);
                    PartRotationAnimator.Complete(part.transform);
                    check(Near(part.transform.localRotation, expected), type.Name + " step " + step + " survives connection and placement refresh");
                }
                check(Near(part.transform.localRotation, original), type.Name + " returns to its original pose after eight steps");
                machine.Flip(part);
                Quaternion afterTurn = part.transform.localRotation;
                var move = typeof(Contraption).GetMethod("TryMoveBuildPart", Private);
                bool moved = (bool)move.Invoke(machine, new object[] { part, 3, 2, new Vector2(0.2f, -0.1f), false, null, null });
                machine.RefreshPartPositions(); PartRotationAnimator.Complete(part.transform);
                check(moved && part.m_coordX == 3 && part.m_coordY == 2 && Near(part.transform.localRotation, afterTurn),
                    type.Name + " build drag preserves the selected angle");

                string saved = part.SerializeCustomParams();
                BasePart restored = Part(type, objects);
                restored.SetFlipped(part.m_flipped); restored.SetRotation(part.m_gridRotation);
                restored.DeserializeCustomParams(saved); restored.SetRotation(part.m_gridRotation);
                PartRotationAnimator.Complete(restored.transform);
                check(Near(restored.transform.localRotation, afterTurn) &&
                    Mathf.Abs(Mathf.DeltaAngle(FineRotate.GetAngle(restored, -999f), initial - 45f)) < 0.001f &&
                    restored.m_gridRotation == part.m_gridRotation && restored.m_flipped == part.m_flipped,
                    type.Name + " rebuild restores angle and native state from the saved parameters");
                if (part is SpringBoxingGlove)
                {
                    Transform visual = part.transform.Find("Visualization");
                    Quaternion buildVisual = visual.rotation;
                    part.EnsureRigidbody(); PartRotationAnimator.Complete(part.transform);
                    check(Near(visual.rotation, buildVisual), "SpringBoxingGlove transfers its complete build orientation to the runtime body");
                }
                if (part is GrapplingHook)
                {
                    Quaternion buildRoot = part.transform.localRotation;
                    part.EnsureRigidbody(); PartRotationAnimator.Complete(part.transform);
                    check(Near(part.transform.localRotation, buildRoot), "GrapplingHook keeps its complete build orientation on simulation entry");
                }

                BasePart nativeOnly = Part(type, objects);
                nativeOnly.SetFlipped(part.m_flipped); nativeOnly.SetRotation(part.m_gridRotation);
                PartRotationAnimator.Complete(nativeOnly.transform);
                float extra = FineRotate.GetAngle(part, initial) - part.GetRotationAngle(part.m_gridRotation);
                SetMagnet(machine, false); SetMagnet(nativeOnly.contraption, false);
                if (part.IsCustomRotated())
                {
                    part.SetRotation(part.GetRotation() + 1); nativeOnly.SetRotation(nativeOnly.GetRotation() + 1);
                }
                else { part.RotateClockwise(); nativeOnly.RotateClockwise(); }
                PartRotationAnimator.Complete(part.transform); PartRotationAnimator.Complete(nativeOnly.transform);
                check(Near(part.transform.localRotation, Quaternion.AngleAxis(extra, Vector3.forward) * nativeOnly.transform.localRotation) &&
                    part.m_gridRotation == nativeOnly.m_gridRotation && part.m_flipped == nativeOnly.m_flipped &&
                    part.GetJointConnectionDirection() == nativeOnly.GetJointConnectionDirection() &&
                    part.GetCustomJointConnectionDirection() == nativeOnly.GetCustomJointConnectionDirection(),
                    type.Name + " magnet off preserves the saved offset and native rotation and joint directions");
            }
            BasePart autoTube = Part(typeof(FuelTube), objects, 3);
            autoTube.SetRotation(BasePart.GridRotation.Deg_0); autoTube.ChangeVisualConnections();
            Quaternion autoOriginal = autoTube.transform.localRotation;
            for (int step = 1; step <= 8; step++)
            {
                autoTube.contraption.Flip(autoTube); autoTube.OnChangeConnections(); autoTube.contraption.RefreshPartPose(autoTube);
                check(Near(autoTube.transform.localRotation, Quaternion.AngleAxis(-45f * step, Vector3.forward) * autoOriginal) &&
                    (int)typeof(FuelTube).GetField("m_autoRotationIndex", Private).GetValue(autoTube) == 10,
                    "Auto FuelTube step " + step + " applies the angle with an unchanged connector layout");
            }
            new BuildMachineTransform { angle = 77f }.Store(autoTube);
            autoTube.contraption.RefreshPartPose(autoTube);
            check(Near(autoTube.transform.localRotation, Quaternion.AngleAxis(40f, Vector3.forward) * autoOriginal),
                "Auto FuelTube frame refresh applies its rotation with an unchanged connector layout");
            autoTube.contraption.Flip(autoTube);
            string autoSaved = autoTube.SerializeCustomParams();
            BasePart autoRestored = Part(typeof(FuelTube), objects, 3);
            autoRestored.DeserializeCustomParams(autoSaved); autoRestored.contraption.RefreshPartPose(autoRestored);
            SetMagnet(autoRestored.contraption, false); autoRestored.OnChangeConnections();
            check(Near(autoRestored.transform.localRotation, autoTube.transform.localRotation),
                "Auto FuelTube rebuild and magnet off retain the saved angle");
            BasePart disabled = Part(typeof(BasePart), objects);
            SetMagnet(disabled.contraption, false);
            check(!disabled.contraption.Flip(disabled) && !disabled.TryGetStoredParam(FineRotate.AngleKey, out _),
                "magnet off keeps nonrotatable parts unchanged");
            disabled.m_autoAlign = BasePart.AutoAlignType.Rotate;
            disabled.m_jointConnectionDirection = BasePart.JointConnectionDirection.Any;
            check(disabled.contraption.Flip(disabled) && disabled.m_gridRotation == BasePart.GridRotation.Deg_270 &&
                !disabled.TryGetStoredParam(FineRotate.AngleKey, out _), "magnet off retains the normal clockwise 90-degree step");
            BasePart fine = Part(typeof(BasePart), objects);
            typeof(GeneralSettings).GetField("m_fineRotationStep", Private).SetValue(settings.GeneralSettings, 23);
            check(fine.contraption.Flip(fine) && Mathf.Abs(Mathf.DeltaAngle(FineRotate.GetAngle(fine, -999f), 23f)) < 0.001f,
                "explicit fine rotation step retains priority over the magnet");
            check(!fine.contraption.Flip(null), "a missing rotation target is ignored");
        }
        finally
        {
            for (int i = objects.Count - 1; i >= 0; i--) if (objects[i]) Object.DestroyImmediate(objects[i]);
            runtime.SetValue(null, oldRuntime); user.SetValue(null, oldUser); dataField.SetValue(null, oldData);
            Object.DestroyImmediate(data);
        }
    }

    private static BasePart Part(Type type, List<GameObject> objects, int fuelTubeIndex = 2)
    {
        var owner = new GameObject("MagnetAudit_" + type.Name); owner.SetActive(false); objects.Add(owner);
        var machine = owner.AddComponent<Contraption>();
        typeof(Contraption).GetMethod("PostAwake", Private).Invoke(machine, null); SetMagnet(machine, true);
        var go = new GameObject(type.Name); go.SetActive(false); go.transform.SetParent(owner.transform, false);
        BasePart part = (BasePart)go.AddComponent(type); part.contraption = machine; part.m_mass = 1f;
        part.m_jointConnectionDirection = BasePart.JointConnectionDirection.Up;
        part.m_jointConnectionType = BasePart.JointConnectionType.Source;
        part.m_autoAlign = type == typeof(BasePart) ? BasePart.AutoAlignType.None : BasePart.AutoAlignType.Rotate;
        machine.Parts.Add(part); machine.SetPartMap(0, 0, part.GenerationIndex, part);
        new BuildMachineTransform { angle = 37f }.Store(part);
        if (part is Wings)
        {
            Child(go, "TopFrameSprite"); Child(go, "BottomFrameSprite"); go.AddComponent<BoxCollider>();
        }
        if (part is IntegratedCircuitPart) typeof(IntegratedCircuitPart).GetField("m_canBeFlipped", Private).SetValue(part, true);
        if (part is MagneticFieldGenerator) part.m_autoAlign = (BasePart.AutoAlignType)(-1);
        if (part is TNT) Child(go, "Visualization");
        if (part is FuelTube)
        {
            part.customPartIndex = fuelTubeIndex;
            part.m_autoAlign = fuelTubeIndex == 3 ? BasePart.AutoAlignType.None : (BasePart.AutoAlignType)(-1);
            typeof(FuelTube).GetField("m_autoRotationIndex", Private).SetValue(part, 10);
        }
        if (part is GrapplingHook)
        {
            part.m_eightWay = true;
            typeof(GrapplingHook).GetField("m_Visualization", Private).SetValue(part, Child(go, "Visualization"));
            typeof(GrapplingHook).GetField("m_SpringVisualization", Private).SetValue(part, Child(go, "SpringVisualization"));
            foreach (string name in new[] { "left", "right", "top", "bottom", "bottomLeft", "bottomRight", "topLeft", "topRight" })
                typeof(GrapplingHook).GetField("m_" + name + "Attachment", Private).SetValue(part, Child(go, name + "Attachment"));
        }
        if (part is MechanicalGate)
        {
            Child(go, "GateCollider").AddComponent<BoxCollider>(); Child(go, "GateRenderer").AddComponent<MeshRenderer>();
        }
        if (part is IndicatorPart)
        {
            typeof(IndicatorPart).GetField("m_symbol", Private).SetValue(part, Child(go, "Symbol"));
            typeof(IndicatorPart).GetField("m_text", Private).SetValue(part, Child(go, "Text").AddComponent<TextMesh>());
        }
        if (part is SpringBoxingGlove)
        {
            typeof(SpringBoxingGlove).GetField("m_Visualization", Private).SetValue(part, Child(go, "Visualization"));
            typeof(SpringBoxingGlove).GetField("m_SpringVisualization", Private).SetValue(part, Child(go, "SpringVisualization"));
        }
        return part;
    }

    private static GameObject Child(GameObject parent, string name)
    {
        var go = new GameObject(name); go.SetActive(false); go.transform.SetParent(parent.transform, false); return go;
    }

    private static void SetMagnet(Contraption machine, bool value) =>
        typeof(Contraption).GetField("m_hasSuperMagnet", Private).SetValue(machine, value);

    private static bool Near(Quaternion actual, Quaternion expected) => Quaternion.Angle(actual, expected) < 0.05f;
}
