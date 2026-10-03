using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using RCR2Port;
using UnityEditor;
using UnityEngine;

public static class RoverPortInteractionAudit
{
    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly BindingFlags PublicInstance = BindingFlags.Instance | BindingFlags.Public;

    private static int rotationChecksTotal;
    private static int driveChecksTotal;
    private static int driveTableRowsTotal;
    private static readonly List<string> driveRows = new List<string> { "id, drive force, MotorWheels per wheel, rated speed" };

    public static void Run()
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
        var gameData = ScriptableObject.CreateInstance<GameData>();
        gameData.m_jointConnectionStrengthWeak = 100f;
        gameData.m_jointConnectionStrengthNormal = 1000f;
        gameData.m_jointConnectionStrengthHigh = 10000f;
        typeof(WPFMonoBehaviour).GetField("s_gameData", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, gameData);
        RoverPortAudit.Run();
        var failures = new List<string>();
        int anchorChecks = 0;
        int poweredChecks = 0;
        var nativeObject = new GameObject("AuditNativeFrame");
        var frame = nativeObject.AddComponent<BasePart>();
        frame.m_jointConnectionType = BasePart.JointConnectionType.Source;
        frame.m_jointConnectionDirection = BasePart.JointConnectionDirection.Any;
        try
        {
            foreach (GameObject prototype in RoverPortCatalog.Instance.Parts)
            {
                var clone = UnityEngine.Object.Instantiate(prototype);
                var part = clone.GetComponent<RoverPortPart>();
                try
                {
                    clone.transform.SetParent(null, false);
                    clone.transform.position = Vector3.zero;
                    part.Awake();
                    if (part.tags.Contains("wheel"))
                        anchorChecks += AuditWheelMounts(part, frame);
                    var powered = part as RoverPortPoweredPart;
                    if (powered != null)
                    {
                        powered.EnsureRigidbody();
                        var driveBodies = (List<Rigidbody>)typeof(RoverPortPoweredPart).GetField("m_driveBodies", PrivateInstance).GetValue(powered);
                        Require(driveBodies.Count > 0, "powered part has no driven rigidbody");
                        Require(!powered.IsEngine(), "consumer incorrectly registered as engine");
                        Require(powered.IsPowered(), "missing BPLE powered registration");
                        powered.SetEnabled(true);
                        if (powered.IsJetTurbine)
                        {
                            // Jet turbines draw engine power instead of fuel (RequiredFuelAmount is
                            // always 0 for them); their thrust path is covered by the turbine audit.
                            poweredChecks++;
                        }
                        else
                        {
                            Require(powered.IsEnabled() && powered.RequiredFuelAmount > 0f, "BPLE toggle needs keyboard input");
                            powered.ConsumeFuel(powered.RequiredFuelAmount);
                            Require(Mathf.Abs(EnergyScale(powered) - 1f) < 0.001f, "fuel supply not driving part");
                            powered.SetEnabled(false);
                            Require(powered.RequiredFuelAmount == 0f && EnergyScale(powered) == 0f, "disabled part consumes energy");
                            powered.SetEnabled(true);
                            Require(EnergyScale(powered) == 0f, "part produces power without a source");
                        }
                        poweredChecks++;
                    }
                    var battery = part as RoverPortBatteryPart;
                    if (battery != null)
                    {
                        battery.SupplyFuel(battery.SupplyFuelAmount);
                        battery.EnsureRigidbody();
                        Require(battery.SupplyFuelAmount == 0f, "EnsureRigidbody refills empty battery");
                        battery.SupplyFuel(float.NaN);
                        Require(!float.IsNaN(battery.SupplyFuelAmount), "invalid supply corrupts charge");
                    }
                }
                catch (Exception exception) { failures.Add(part.PortId + ": " + exception); }
                finally { UnityEngine.Object.DestroyImmediate(clone); }
            }
            AuditExternalMount(failures);
            int rotationChecks = AuditClickRotation(failures);
            int driveChecks = AuditMotorWheelDrive(failures);
            int driveTableRows = AuditWheelDriveTable(failures);
            rotationChecksTotal = rotationChecks;
            driveChecksTotal = driveChecks;
            driveTableRowsTotal = driveTableRows;
        }
        finally { UnityEngine.Object.DestroyImmediate(nativeObject); }
        Directory.CreateDirectory("Builds/QA");
        File.WriteAllLines("Builds/QA/wheel-drive.csv", driveRows);
        File.WriteAllLines("Builds/QA/interaction-audit.txt", new[] {
            "Wheel mount/orientation checks: " + anchorChecks,
            "Powered parts checked: " + poweredChecks,
            "Build UI rotation checks: " + rotationChecksTotal,
            "MotorWheel equivalence checks: " + driveChecksTotal,
            "Wheel drive rows: " + driveTableRowsTotal,
            "Failures: " + failures.Count }
            .Concat(driveRows).Concat(failures));
        if (failures.Count > 0)
            throw new Exception(string.Join("\n", failures));
        Debug.Log("RCR2_INTERACTION_PASS: " + anchorChecks + " wheel mounts; " + poweredChecks + " consumers; " +
                  rotationChecksTotal + " build UI rotations; " + driveChecksTotal + " MotorWheel equivalences; " +
                  driveTableRowsTotal + " wheels in the drive table");
    }

    private static int AuditWheelMounts(RoverPortPart part, BasePart frame)
    {
        int count = 0;
        for (int rotation = 0; rotation < 4; rotation++)
        {
            for (int flip = 0; flip < 2; flip++)
            {
                part.SetRotation(rotation);
                part.SetFlipped(flip != 0);
                foreach (Vector3 point in part.GetConnectionPoints())
                {
                    bool matched = false;
                    foreach (Vector3 offset in new[] { Vector3.right, Vector3.up, Vector3.left, Vector3.down })
                    {
                        Vector3 center = point + offset * 0.5f;
                        if (Mathf.Abs(center.x - Mathf.Round(center.x)) > 0.035f ||
                            Mathf.Abs(center.y - Mathf.Round(center.y)) > 0.035f)
                            continue;
                        frame.transform.position = new Vector3(Mathf.Round(center.x), Mathf.Round(center.y), 0f);
                        frame.m_jointConnectionDirection = offset.x > 0f ? BasePart.JointConnectionDirection.Left :
                            offset.x < 0f ? BasePart.JointConnectionDirection.Right :
                            offset.y > 0f ? BasePart.JointConnectionDirection.Down : BasePart.JointConnectionDirection.Up;
                        Vector3 anchor;
                        BasePart.Direction direction;
                        Require(RoverPortConnections.TryGetConnection(part, frame, out anchor, out direction), "native mount not found");
                        Require((anchor - point).sqrMagnitude < 0.002f, "wrong mount anchor");
                        matched = true;
                    }
                    Require(matched, "source mount not aligned to grid edge at rotation " + rotation);
                    count++;
                }
                frame.transform.position = new Vector3(50f, 50f, 0f);
                Vector3 unusedAnchor;
                BasePart.Direction unusedDirection;
                Require(!RoverPortConnections.TryGetConnection(part, frame, out unusedAnchor, out unusedDirection), "distant frame connects");
            }
        }
        return count;
    }

    private static void AuditExternalMount(List<string> failures)
    {
        var root = new GameObject("AuditContraption");
        var contraption = root.AddComponent<Contraption>();
        typeof(Contraption).GetMethod("PostAwake", PrivateInstance).Invoke(contraption, null);
        GameObject wheelObject = UnityEngine.Object.Instantiate(RoverPortCatalog.Instance.GetPart("part_starter_wheel"));
        var frameObject = new GameObject("RemoteNativeMount");
        try
        {
            wheelObject.transform.SetParent(root.transform, false);
            wheelObject.transform.localPosition = Vector3.zero;
            var wheel = wheelObject.GetComponent<RoverPortPart>();
            wheel.Awake();
            wheel.contraption = contraption;
            AuditGearboxLifecycle(contraption, (RoverPortPoweredPart)wheel);
            wheel.EnsureRigidbody();
            frameObject.transform.SetParent(root.transform, false);
            frameObject.transform.localPosition = new Vector3(1f, 1f, 0f);
            var frame = frameObject.AddComponent<BasePart>();
            frame.m_jointConnectionDirection = BasePart.JointConnectionDirection.Any;
            frame.m_jointConnectionType = BasePart.JointConnectionType.Source;
            frameObject.AddComponent<Rigidbody>();
            contraption.Parts.Add(wheel);
            contraption.Parts.Add(frame);
            wheel.CreateCustomJoints();
            Joint joint = contraption.FindPartJoints(wheel).Single();
            wheel.CreateCustomJoints();
            Require(contraption.FindPartJoints(wheel).Count == 1, "repeated initialization duplicates mount joints");
            Require(joint.connectedBody == frame.GetComponent<Rigidbody>(), "remote mount missing from joint graph");
            Require((wheel.transform.TransformPoint(joint.anchor) - new Vector3(1f, 0.5f, 0f)).sqrMagnitude < 0.001f,
                "multi-cell mount uses center midpoint instead of source anchor");
            Require((wheel.transform.TransformPoint(joint.anchor) - frame.transform.TransformPoint(joint.connectedAnchor)).sqrMagnitude < 0.001f,
                "joint anchors disagree");
            Require(contraption.CanConnectTo(wheel, BasePart.Direction.Up), "construction preview misses remote mount");
            frame.m_enginePower = 100f;
            typeof(Contraption).GetMethod("FindConnectedComponents", PrivateInstance).Invoke(contraption, null);
            var powered = (RoverPortPoweredPart)wheel;
            powered.InitializeEngine();
            powered.SetEnabled(true);
            Require(powered.RequiredFuelAmount == 0f && EnergyScale(powered) > 0f, "native engine power does not reach RCR2 wheel");
            frame.m_enginePower = 0f;
            AuditFuelDistribution(contraption, powered);
        }
        catch (Exception exception) { failures.Add("Remote mount: " + exception); }
        finally
        {
            UnityEngine.Object.DestroyImmediate(wheelObject);
            UnityEngine.Object.DestroyImmediate(frameObject);
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void AuditGearboxLifecycle(Contraption contraption, RoverPortPoweredPart powered)
    {
        var gearboxObject = new GameObject("AuditGearbox");
        gearboxObject.SetActive(false);
        var gearbox = gearboxObject.AddComponent<Gearbox>();
        int originalComponent = powered.ConnectedComponent;
        MethodInfo update = typeof(RoverPortPoweredPart).GetMethod("Update", PrivateInstance);
        FieldInfo throttle = typeof(RoverPortPoweredPart).GetField("m_throttle", PrivateInstance);
        FieldInfo running = typeof(Contraption).GetField("m_running", PrivateInstance);
        try
        {
            Require(!contraption.ConnectedToGearbox(null) && contraption.GetGearbox(null) == null,
                "null gearbox query must be empty");
            foreach (int index in new[] { -1, 0, 999 })
            {
                powered.ConnectedComponent = index;
                Require(!contraption.ConnectedToGearbox(powered) && contraption.GetGearbox(powered) == null,
                    "uninitialized or stale component query must be empty");
                Require(contraption.GetEnginePowerFactor(powered) == 0f, "invalid component supplies engine power");
                powered.SetEnabled(true);
                update.Invoke(powered, null);
                Require((float)throttle.GetValue(powered) == 0f, "construction part reads drive input");
                running.SetValue(contraption, true);
                update.Invoke(powered, null);
                running.SetValue(contraption, false);
            }
            contraption.ConnectedComponents.Add(new Contraption.ConnectedComponent { hasGearbox = true, gearbox = gearbox });
            powered.ConnectedComponent = 0;
            Require(contraption.ConnectedToGearbox(powered) && contraption.GetGearbox(powered) == gearbox,
                "valid gearbox connection was lost");
            running.SetValue(contraption, true);
            typeof(Gearbox).GetField("activated", PrivateInstance).SetValue(gearbox, true);
            update.Invoke(powered, null);
            Require((float)throttle.GetValue(powered) == -1f, "enabled gearbox does not reverse drive");
            typeof(Gearbox).GetField("activated", PrivateInstance).SetValue(gearbox, false);
            update.Invoke(powered, null);
            Require((float)throttle.GetValue(powered) == 1f, "disabled gearbox does not restore forward drive");
            contraption.ConnectedComponents.Clear();
            update.Invoke(powered, null);
            Require((float)throttle.GetValue(powered) == 1f, "cleared graph leaves stale gearbox state");
            powered.SetEnabled(false);
            update.Invoke(powered, null);
            Require((float)throttle.GetValue(powered) == 0f, "disabled drive retains throttle");
            Debug.Log("RCR2_GEARBOX_LIFECYCLE_PASS");
        }
        finally
        {
            running.SetValue(contraption, false);
            contraption.ConnectedComponents.Clear();
            powered.ConnectedComponent = originalComponent;
            UnityEngine.Object.DestroyImmediate(gearboxObject);
        }
    }

    private static float EnergyScale(RoverPortPoweredPart part)
    {
        return (float)typeof(RoverPortPoweredPart).GetMethod("CalculateEnergyScale", PrivateInstance).Invoke(part, null);
    }

    private static void AuditFuelDistribution(Contraption contraption, RoverPortPoweredPart powered)
    {
        GameObject batteryObject = UnityEngine.Object.Instantiate(RoverPortCatalog.Instance.GetPart("part_battery_small"));
        try
        {
            var battery = batteryObject.GetComponent<RoverPortBatteryPart>();
            battery.Awake();
            battery.contraption = contraption;
            battery.EnsureRigidbody();
            contraption.Parts.Add(battery);
            contraption.AddFixedJoint(powered, battery);
            typeof(Contraption).GetMethod("FindConnectedComponents", PrivateInstance).Invoke(contraption, null);
            typeof(Contraption).GetProperty("Instance").GetSetMethod(true).Invoke(null, new object[] { contraption });
            powered.InitializeEngine();
            powered.SetEnabled(true);
            var system = new FuelSystem();
            system.UpdateFuelParts();
            float charge = battery.SupplyFuelAmount;
            float demand = powered.RequiredFuelAmount;
            system.FixedUpdate();
            Require(EnergyScale(powered) > 0f, "FuelSystem does not supply RCR2 wheel");
            Require(Mathf.Abs(charge - battery.SupplyFuelAmount - demand) < 0.0001f, "battery draw does not match active demand");
            powered.SetEnabled(false);
            charge = battery.SupplyFuelAmount;
            system.FixedUpdate();
            Require(battery.SupplyFuelAmount == charge, "disabled consumer drains battery");
            battery.SupplyFuel(charge);
            powered.SetEnabled(true);
            system.FixedUpdate();
            Require(battery.SupplyFuelAmount == 0f && EnergyScale(powered) == 0f, "empty battery produces power or NaN");
        }
        finally { UnityEngine.Object.DestroyImmediate(batteryObject); }
    }

    /// <summary>
    /// The build UI rotates a placed part when it is clicked without dragging: ConstructionUI calls
    /// Contraption.Flip(), which only acts on parts flagged as rotatable. Ported RCR2 parts must take
    /// the same path as native wheels (AutoAlignType.Rotate), rotate 90 degrees per click and cycle
    /// back after four clicks, with the footprint following the rotation.
    /// </summary>
    private static int AuditClickRotation(List<string> failures)
    {
        int checks = 0;
        GameObject root = null;
        GameObject partObject = null;
        try
        {
            root = new GameObject("AuditRotationContraption");
            var contraption = root.AddComponent<Contraption>();
            typeof(Contraption).GetMethod("PostAwake", PrivateInstance).Invoke(contraption, null);
            partObject = UnityEngine.Object.Instantiate(RoverPortCatalog.Instance.GetPart("part_sand_wheel_track"));
            partObject.transform.SetParent(root.transform, false);
            var part = partObject.GetComponent<RoverPortPart>();
            part.Awake();
            part.contraption = contraption;
            contraption.Parts.Add(part);
            Require(part.m_autoAlign == BasePart.AutoAlignType.Rotate, "ported part is not marked as rotatable");
            checks++;
            int start = part.GetRotation();
            Require(contraption.Flip(part), "build UI click does not rotate a ported part");
            checks++;
            Require(part.GetRotation() != start, "clicked part keeps its rotation");
            Require(Footprint(part) == "x[0,0] y[-1,1]", "first click footprint " + Footprint(part) + " (expected the 3x1 source block turned 90 degrees)");
            checks++;
            Require(contraption.Flip(part), "second click ignored");
            Require(Footprint(part) == "x[-1,1] y[0,0]", "180 degree footprint " + Footprint(part) + " (expected the mirrored source block)");
            checks++;
            contraption.Flip(part);
            contraption.Flip(part);
            Require(part.GetRotation() == start, "four clicks do not restore the original rotation");
            Require(Footprint(part) == "x[-1,1] y[0,0]", "footprint after a full turn " + Footprint(part));
            checks++;
            Require(!part.IsFlipped(), "rotation must not toggle the flip state");
            checks++;
            // Rotation animation contract: only the build phase animates, and the easing is normalised.
            Require(!PartRotationAnimator.ShouldAnimate(part),
                "rotation must snap outside play mode so saved data and audits see the final angle");
            Require(PartRotationAnimator.Duration > 0f, "rotation animation duration is not set");
            Require(Mathf.Abs(PartRotationAnimator.Ease(0f)) < 0.0001f &&
                    Mathf.Abs(PartRotationAnimator.Ease(1f) - 1f) < 0.0001f &&
                    PartRotationAnimator.Ease(0.5f) > 0.5f,
                "rotation animation easing is not a normalised ease-out");
            checks++;

            // The super magnet lifts the rotation restriction (ported from the sister project
            // bpxhgc-901-9): a part the build UI will not turn turns once the magnet is on, in 45 degree
            // steps, and a part that is rotatable keeps its own marking so it still turns with the
            // magnet off again.
            GameObject blockedObject = null;
            try
            {
                blockedObject = UnityEngine.Object.Instantiate(RoverPortCatalog.Instance.GetPart("part_terra_frame_basic"));
                blockedObject.transform.SetParent(root.transform, false);
                var blocked = blockedObject.GetComponent<RoverPortPart>();
                blocked.Awake();
                blocked.contraption = contraption;
                blocked.m_autoAlign = BasePart.AutoAlignType.None;
                blocked.m_jointConnectionDirection = BasePart.JointConnectionDirection.Any;
                contraption.Parts.Add(blocked);
                Require(!contraption.Flip(blocked), "a part the build UI cannot turn was turned without the magnet");
                Require(blocked.GetRotation() == (int)BasePart.GridRotation.Deg_0, "blocked part turned without the magnet");
                checks++;
                SetMagnet(contraption, true);
                Require(contraption.Flip(blocked), "the magnet does not let a restricted part turn");
                Require(blocked.GetRotation() == (int)BasePart.GridRotation.Deg_315,
                    "magnet turn is not a 45 degree step: " + blocked.GetRotation());
                checks++;
                Require(part.m_autoAlign == BasePart.AutoAlignType.Rotate,
                    "the magnet changed the part's own rotation marking");
                int beforeMagnet = part.GetRotation();
                Require(contraption.Flip(part), "the magnet ignores a ported part");
                Require(part.GetRotation() != beforeMagnet, "magnet click on a ported part did nothing");
                checks++;
                SetMagnet(contraption, false);
                Require(part.m_autoAlign == BasePart.AutoAlignType.Rotate && contraption.Flip(part),
                    "ported part no longer turns once the magnet is off");
                checks++;
            }
            finally
            {
                if (blockedObject != null) UnityEngine.Object.DestroyImmediate(blockedObject);
            }
        }
        catch (Exception exception) { failures.Add("Build UI rotation: " + exception.Message); }
        finally
        {
            if (partObject != null) UnityEngine.Object.DestroyImmediate(partObject);
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }
        return checks;
    }

    private static void SetMagnet(Contraption contraption, bool value)
    {
        // The public property plays the magnet audio effect, which the audit has no AudioManager for.
        typeof(Contraption).GetField("m_hasSuperMagnet", PrivateInstance).SetValue(contraption, value);
        Require(contraption.HasSuperMagnet == value, "magnet flag did not take");
    }

    /// <summary>
    /// One ported wheel at full power must drive exactly like one native MotorWheel: the applied wheel
    /// torque has to match a reference torque of MotorWheel.m_force * wheel radius, and N wheels then
    /// scale to N times that force.
    /// </summary>
    private static int AuditMotorWheelDrive(List<string> failures)
    {
        int checks = 0;
        GameObject wheelObject = null;
        try
        {
            // Native reference: the base motor wheel part (all upgrades except 08 share these values).
            var nativeTemplate = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/Part_MotorWheel_01_SET.prefab");
            var nativeWheel = nativeTemplate == null ? null : nativeTemplate.GetComponent<MotorWheel>();
            Require(nativeWheel != null, "native MotorWheel template not found");
            float nativeForce = nativeWheel.m_force;
            float nativeConsumption = nativeWheel.m_powerConsumption;
            Require(nativeForce > 0f, "native MotorWheel force is not set");
            Require(Mathf.Abs(nativeForce - RoverPortPoweredPart.MotorWheelDriveForce) < 0.001f,
                "ported drive force " + RoverPortPoweredPart.MotorWheelDriveForce + " differs from native MotorWheel force " + nativeForce);
            checks++;
            Require(Mathf.Abs(nativeConsumption - RoverPortPoweredPart.MotorWheelPowerConsumption) < 0.001f,
                "ported power draw " + RoverPortPoweredPart.MotorWheelPowerConsumption + " differs from native MotorWheel draw " + nativeConsumption);
            checks++;

            wheelObject = UnityEngine.Object.Instantiate(RoverPortCatalog.Instance.GetPart("part_terra_wheel_basic"));
            var part = wheelObject.GetComponent<RoverPortPoweredPart>();
            part.Awake();
            Require(Mathf.Abs(part.PowerConsumption - RoverPortPoweredPart.MotorWheelPowerConsumption) < 0.001f,
                "ported wheel power draw is " + part.PowerConsumption);
            checks++;
            // Drive arithmetic: one grounded wheel delivers one native MotorWheel's force, and N grounded
            // wheels deliver N times that force.
            float perWheel = RoverPortPoweredPart.MotorWheelDriveForce * RoverPortPoweredPart.DriveForceInMotorWheels;
            Require(Mathf.Abs(perWheel - nativeForce) < 0.001f,
                "one grounded ported wheel delivers " + perWheel + " N instead of one MotorWheel (" + nativeForce + " N)");
            checks++;
            Require(Mathf.Abs(RoverPortPoweredPart.WheelDriveForce(perWheel, 0f, 15f, 1f, 1f, 1f) - perWheel) < 0.001f,
                "full power force at rest is not the rated wheel force");
            Require(Mathf.Abs(RoverPortPoweredPart.WheelDriveForce(perWheel * 0.6f, 0f, 15f, 1f, 1f, 1f) - perWheel * 0.6f) < 0.001f,
                "part strength is not applied to the drive");
            Require(Mathf.Abs(RoverPortPoweredPart.WheelDriveForce(perWheel, 0f, 15f, 0f, 1f, 1f)) < 0.0001f,
                "wheel drives without supplied power");
            Require(Mathf.Abs(RoverPortPoweredPart.WheelDriveForce(perWheel, 0f, 15f, 1f, 1f, 0f)) < 0.0001f,
                "wheel drives without a command");
            Require(Mathf.Abs(RoverPortPoweredPart.WheelDriveForce(perWheel, 15f, 15f, 1f, 1f, 1f)) < 0.0001f,
                "wheel keeps driving past its rated speed");
            Require(RoverPortPoweredPart.WheelDriveForce(perWheel, 0f, 15f, 1f, 1f, -1f) < 0f,
                "reverse command does not reverse the wheel force");
            Require(Mathf.Abs(RoverPortPoweredPart.WheelDriveForce(perWheel, 0f, 15f, 1f, 2f, 1f) - 2f * perWheel) < 0.001f,
                "engine power factor does not scale the wheel drive");
            checks++;
            Require(Mathf.Abs(part.RatedSurfaceSpeed(900f) - RoverPortPoweredPart.NativeMotorWheelSpeed) < 0.001f,
                "fastest RCR2 wheel does not reach the native top speed");
            Require(Mathf.Abs(part.RatedSurfaceSpeed(450f) - RoverPortPoweredPart.NativeMotorWheelSpeed * 0.5f) < 0.001f,
                "slower RCR2 wheels do not keep their lower top speed");
            Require(Mathf.Abs(part.RatedSurfaceSpeed(8000f) - RoverPortPoweredPart.NativeMotorWheelSpeed) < 0.001f,
                "outlier RCR2 motor speeds are not capped at the native top speed");
            // The engine power factor lifts the top speed as well, like MotorWheel's 15 * factor.
            FieldInfo factorField = typeof(RoverPortPoweredPart).GetField("m_enginePowerFactor", PrivateInstance);
            float savedFactor = (float)factorField.GetValue(part);
            factorField.SetValue(part, 6f);
            Require(Mathf.Abs(part.RatedGroundSpeed(900f) - RoverPortPoweredPart.NativeMotorWheelSpeed * 6f) < 0.001f,
                "engine power factor does not scale the wheel top speed");
            factorField.SetValue(part, savedFactor);
            checks++;
            Require(Mathf.Abs(part.DriveForcePerWheel - nativeForce) < 0.001f,
                "base torque wheel force is " + part.DriveForcePerWheel);
            foreach (Collider collider in wheelObject.GetComponentsInChildren<Collider>(true))
            {
                if (collider.sharedMaterial != null)
                    Require(collider.sharedMaterial.dynamicFriction >= RoverPortPoweredPart.WheelMinimumFriction - 0.001f,
                        "tyre friction " + collider.sharedMaterial.dynamicFriction + " is below the native material");
            }
            checks++;
            part.EnsureRigidbody();
            var bodies = (List<Rigidbody>)typeof(RoverPortPoweredPart).GetField("m_driveBodies", PrivateInstance).GetValue(part);
            Require(bodies.Count == 1 && bodies[0] != null, "wheel has no single driven body");
        }
        catch (Exception exception) { failures.Add("MotorWheel drive: " + exception.Message); }
        finally
        {
            if (wheelObject != null) UnityEngine.Object.DestroyImmediate(wheelObject);
        }
        return checks;
    }

    /// <summary>
    /// Reports what one wheel of every ported wheel or track delivers at full power, expressed in
    /// native MotorWheels (MotorWheel: 50 N, 15 m/s before the engine factor) together with the rated
    /// surface speed, so the values can be compared with BPLE's own wheels.
    /// </summary>
    private static int AuditWheelDriveTable(List<string> failures)
    {
        int checks = 0;
        foreach (GameObject prototype in RoverPortCatalog.Instance.Parts)
        {
            var part = prototype.GetComponent<RoverPortPoweredPart>();
            if (part == null || !part.tags.Contains("wheel"))
                continue;
            try
            {
                var data = (RoverPortPartData)typeof(RoverPortPart).GetField("m_portData", PrivateInstance).GetValue(part);
                float force = part.DriveForcePerWheel;
                Require(force >= RoverPortPoweredPart.MotorWheelDriveForce,
                    "drive force " + force + " N is weaker than one MotorWheel");
                float radius = 0f;
                foreach (SphereCollider collider in prototype.GetComponentsInChildren<SphereCollider>(true))
                    radius = Mathf.Max(radius, collider.radius);
                float rpm = data != null && data.motion != null
                    ? Mathf.Max(data.motion.max_speed, data.motion.reverse_max_speed)
                    : 0f;
                float speed = part.RatedSurfaceSpeed(rpm);
                foreach (Collider collider in prototype.GetComponentsInChildren<Collider>(true))
                {
                    if (collider.sharedMaterial != null)
                        Require(collider.sharedMaterial.dynamicFriction >= RoverPortPoweredPart.WheelMinimumFriction - 0.001f,
                            "tyre friction " + collider.sharedMaterial.dynamicFriction + " is below the native material");
                }
                driveRows.Add(string.Format("{0}, {1:0} N, {2:0.0}x MotorWheel, rated {3:0.#} m/s base",
                    part.PortId, force, force / RoverPortPoweredPart.MotorWheelDriveForce, speed));
                checks++;
            }
            catch (Exception exception) { failures.Add(part.PortId + " drive table: " + exception.Message); }
        }
        return checks;
    }

    private static string Footprint(RoverPortPart part)
    {
        return string.Format("x[{0},{1}] y[{2},{3}]",
            typeof(BasePart).GetField("m_gridXmin", PublicInstance).GetValue(part),
            typeof(BasePart).GetField("m_gridXmax", PublicInstance).GetValue(part),
            typeof(BasePart).GetField("m_gridYmin", PublicInstance).GetValue(part),
            typeof(BasePart).GetField("m_gridYmax", PublicInstance).GetValue(part));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
