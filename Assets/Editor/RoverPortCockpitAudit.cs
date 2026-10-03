using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using RCR2Port;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Verifies the cockpit / frame behaviour of the ported RoverCraft 2 parts (RCR2_PORT.md):
///
/// * the driver RCR2 spawns in the cabin is gone from the catalog - no Pilot node, no hero sprite,
///   no dangling node reference - while the cabin keeps its own artwork, colliders and body;
/// * the cockpit acts as the game's metal frame: MetalFrame part type, chassis part, stows one part
///   inside it (welded like Frame.Initialize welds a metal frame's interior);
/// * every other ported part can be stowed in a frame;
/// * a cockpit to frame connection holds at 70% of a metal frame to metal frame connection, while
///   its other connections keep the game's rule.
///
/// Run with: Unity -batchmode -quit -executeMethod RoverPortCockpitAudit.Run
/// Report: Builds/QA/cockpit-audit.txt
/// </summary>
public static class RoverPortCockpitAudit
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const float StrengthTolerance = 0.01f;

    private static readonly List<string> failures = new List<string>();

    [MenuItem("Tools/RCR2/Audit cockpit and frames")]
    public static void Run()
    {
        PrepareEditorServices();
        var catalog = RoverPortCatalog.Instance;
        var rows = new List<string> { "id,category,type,encloses,enclosed,chassis,nodes,renderers,activeColliders,activeBodies" };
        int cockpits = 0;
        int stowable = 0;
        foreach (GameObject prototype in catalog.Parts)
        {
            var part = prototype.GetComponent<RoverPortPart>();
            RoverPortPartData data = PortData(part);
            if (data == null)
            {
                failures.Add(part.PortId + ": missing port data");
                continue;
            }
            bool cockpit = string.Equals(data.category, "cockpit", StringComparison.Ordinal);
            try
            {
                if (cockpit)
                {
                    cockpits++;
                    Require(part is RoverPortCockpitPart, "cockpit is not built as a cockpit part");
                    Require(part.Type == BasePart.PartType.MetalFrame, "cockpit part type is " + part.Type);
                    Require(part.CanEncloseParts(), "cockpit cannot stow a part");
                    Require(!part.CanBeEnclosed(), "cockpit can be stowed in a frame");
                    Require(part.IsPartOfChassis(), "cockpit is not part of the chassis");
                    // The cabin is the frame, and the game's own frames are not rotatable. If the cabin
                    // stays AutoAlignType.Rotate, the click that stows a part also turns the cabin, because
                    // ConstructionUI finds the cabin at its anchor cell and calls Contraption.Flip before
                    // the stowed part exists - and afterwards that click targets the stowed part instead, so
                    // the cabin can no longer be turned at all.
                    Require(part.m_autoAlign == BasePart.AutoAlignType.None,
                        "cockpit is still marked rotatable (" + part.m_autoAlign + ")");
                    AuditPilotRemoved(part, data);
                }
                else
                {
                    Require(!(part is RoverPortCockpitPart), "non-cockpit built as a cockpit part");
                    Require(part.CanBeEnclosed(), "part cannot be stowed in a frame");
                    stowable++;
                }
                rows.Add(string.Join(",", new[] {
                    part.PortId, data.category, part.Type.ToString(), part.CanEncloseParts().ToString(),
                    part.CanBeEnclosed().ToString(), part.IsPartOfChassis().ToString(),
                    Count(data.nodes).ToString(), Count(data.renderers).ToString(),
                    CountActive(data, data.colliders.Select(c => c.node)).ToString(),
                    CountActive(data, data.bodies.Select(b => b.node)).ToString() }));
            }
            catch (Exception exception)
            {
                failures.Add(part.PortId + ": " + exception.Message);
            }
        }
        Require(cockpits == 9, "expected 9 cockpits, found " + cockpits);
        Require(stowable == catalog.Parts.Count - cockpits, "every non-cockpit part must be stowable");

        int strengthChecks = AuditConnectionStrength();
        AuditEnclosure();
        int poweredCabins = AuditCabinPower();
        int rotationChecks = AuditCockpitRotation();

        Directory.CreateDirectory("Builds/QA");
        File.WriteAllLines("Builds/QA/cockpit-audit.txt", new List<string> {
            "Cockpits: " + cockpits,
            "Stowable parts: " + stowable,
            "Cabins carrying the source battery: " + poweredCabins,
            "Connection strength checks: " + strengthChecks,
            "Rotation checks: " + rotationChecks,
            "Failures: " + failures.Count }
            .Concat(failures).Concat(rows));
        if (failures.Count > 0)
        {
            throw new Exception(string.Join("\n", failures));
        }
        Debug.Log("RCR2_COCKPIT_PASS: " + cockpits + " cockpits without a driver, " + stowable +
                  " stowable parts, " + poweredCabins + " cabins with the source battery, " +
                  strengthChecks + " connection strength checks, " + rotationChecks + " rotation checks");
    }

    /// <summary>
    /// The stow click must not turn the cabin. Reproduces exactly what the build UI does: it finds the
    /// cabin at its anchor cell and calls Contraption.Flip on it, and only afterwards stores the part
    /// that was being placed. A cabin that reports itself rotatable turns 90 degrees in that frame and
    /// then stops responding to the mouse, because the next click on that cell is aimed at the stowed
    /// part instead. Ported parts that are meant to rotate (wheels, frames, cargo) must keep rotating.
    /// </summary>
    private static int AuditCockpitRotation()
    {
        var host = new GameObject("CockpitRotationContraption");
        var contraption = host.AddComponent<Contraption>();
        typeof(Contraption).GetMethod("PostAwake", PrivateInstance).Invoke(contraption, null);
        int checks = 0;
        try
        {
            foreach (string cockpitId in new[] { "part_starter_cockpit", "part_ice_cockpit" })
            {
                var cockpit = ClonePart(host, cockpitId);
                cockpit.contraption = contraption;
                cockpit.EnsureRigidbody();
                int before = cockpit.GetRotation();
                Require(!contraption.Flip(cockpit), "the stow click still turns the cabin " + cockpitId);
                Require(cockpit.GetRotation() == before, "cabin " + cockpitId + " rotated to " + cockpit.GetRotation());
                checks += 2;
                UnityEngine.Object.DestroyImmediate(cockpit.gameObject);
            }
            // The blanket marking must stay for everything else that is meant to be turned in the UI.
            foreach (string id in new[] { "part_terra_wheel_basic", "part_terra_frame_basic" })
            {
                var part = ClonePart(host, id);
                Require(part.m_autoAlign == BasePart.AutoAlignType.Rotate, id + " is no longer rotatable in the build UI");
                checks++;
                UnityEngine.Object.DestroyImmediate(part.gameObject);
            }
        }
        catch (Exception exception)
        {
            failures.Add("cockpit rotation: " + exception.Message);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
        return checks;
    }

    /// <summary>
    /// The cabin must be free of the driver: no Pilot node left, no hero sprite in either renderer
    /// set, no reference to a node that was dropped - and its own rig (artwork, colliders, body) in
    /// one piece after the removal.
    /// </summary>
    private static void AuditPilotRemoved(RoverPortPart part, RoverPortPartData data)
    {
        string[] pilots = data.nodes.Where(n => n.name.StartsWith("Pilot", StringComparison.OrdinalIgnoreCase))
            .Select(n => n.name).ToArray();
        Require(pilots.Length == 0, "pilot nodes left: " + string.Join(", ", pilots));
        foreach (RoverPortRendererData renderer in data.renderers.Concat(data.construction_renderers))
        {
            string sprite = renderer.sprite == null ? "" : renderer.sprite.name ?? "";
            Require(sprite.IndexOf("hero", StringComparison.OrdinalIgnoreCase) < 0, "hero sprite left: " + sprite);
        }
        var nodeIds = new HashSet<string>(data.nodes.Select(n => n.id));
        foreach (RoverPortRendererData renderer in data.renderers.Concat(data.construction_renderers))
            Require(nodeIds.Contains(renderer.node), "renderer points at a dropped node");
        foreach (RoverPortColliderData collider in data.colliders)
            Require(nodeIds.Contains(collider.node), "collider points at a dropped node");
        foreach (RoverPortBodyData body in data.bodies)
            Require(nodeIds.Contains(body.node), "body points at a dropped node");
        foreach (RoverPortJointData joint in data.joints)
        {
            Require(nodeIds.Contains(joint.node), "joint points at a dropped node");
            Require(string.IsNullOrEmpty(joint.connected_body) || nodeIds.Contains(joint.connected_body),
                "joint body points at a dropped node");
        }
        // The cabin itself has to survive with its physics: the ported cockpit is a chassis part.
        Require(CountActive(data, data.colliders.Select(c => c.node)) > 0, "cabin has no live collider");
        Require(CountActive(data, data.bodies.Select(b => b.node)) > 0, "cabin has no live body");
        Require(Count(data.construction_renderers) > 0, "cabin has no build artwork");
        Require(Count(data.renderers) > 0, "cabin has no run artwork");
        Require(data.grid != null && data.grid.shift != null, "cabin grid alignment missing");

        GameObject clone = null;
        try
        {
            clone = UnityEngine.Object.Instantiate(part.gameObject);
            clone.transform.SetParent(null, false);
            var clonePart = clone.GetComponent<RoverPortPart>();
            clonePart.Awake();
            string[] runtimePilots = clone.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.StartsWith("Pilot", StringComparison.OrdinalIgnoreCase))
                .Select(t => t.name).ToArray();
            Require(runtimePilots.Length == 0, "pilot transform in the built part: " + string.Join(", ", runtimePilots));
            Require(clone.GetComponentsInChildren<MeshRenderer>(true).Any(r => r.enabled), "built cabin has no artwork");
        }
        finally
        {
            if (clone != null) UnityEngine.Object.DestroyImmediate(clone);
        }
    }

    /// <summary>
    /// The cockpit holds its frame connections at 70% of a metal frame's; every other connection
    /// keeps the game's rule (the sum of both parts' connection strengths).
    /// </summary>
    private static int AuditConnectionStrength()
    {
        var host = new GameObject("CockpitStrengthContraption");
        var contraption = host.AddComponent<Contraption>();
        typeof(Contraption).GetMethod("PostAwake", PrivateInstance).Invoke(contraption, null);
        host.transform.position = Vector3.zero;
        int checks = 0;
        try
        {
            var metalA = NativeFrame(host, "MetalFrameA", BasePart.PartType.MetalFrame, BasePart.JointConnectionStrength.High);
            var metalB = NativeFrame(host, "MetalFrameB", BasePart.PartType.MetalFrame, BasePart.JointConnectionStrength.High);
            var wooden = NativeFrame(host, "WoodenFrame", BasePart.PartType.WoodenFrame, BasePart.JointConnectionStrength.Normal);
            var cockpitA = ClonePart(host, "part_starter_cockpit");
            var cockpitB = ClonePart(host, "part_ice_cockpit");
            var portedFrameA = ClonePart(host, "part_terra_frame_basic");
            var portedFrameB = ClonePart(host, "part_terra_frame_heavy");
            var wheel = ClonePart(host, "part_starter_wheel");

            // The game's own value for two metal frames, read from the same game data the cockpit
            // scales from: 600 + 600.
            float metalPair = RoverPortConnections.GetJointBreakForce(contraption, metalA, metalB);
            Require(Mathf.Abs(metalPair - 1200f) < StrengthTolerance, "metal frame pair is " + metalPair + ", expected 1200");
            // 0.7 x the metal frame pair, whatever the neighbour frame is made of.
            ExpectStrength(contraption, cockpitA, metalA, metalPair * RoverPortConnections.CockpitFrameStrengthScale, ref checks);
            ExpectStrength(contraption, cockpitA, wooden, (600f + 125f) * RoverPortConnections.CockpitFrameStrengthScale, ref checks);
            ExpectStrength(contraption, cockpitA, portedFrameA, metalPair * RoverPortConnections.CockpitFrameStrengthScale, ref checks);
            ExpectStrength(contraption, cockpitA, cockpitB, metalPair * RoverPortConnections.CockpitFrameStrengthScale, ref checks);
            // Everything that is not a frame keeps the plain sum.
            ExpectStrength(contraption, cockpitA, wheel, 1200f, ref checks);
            ExpectStrength(contraption, metalA, wheel, 1200f, ref checks);
            ExpectStrength(contraption, portedFrameA, portedFrameB, 1200f, ref checks);
            // The rule must not depend on which part of the pair is asked first.
            ExpectStrength(contraption, metalB, cockpitA, metalPair * RoverPortConnections.CockpitFrameStrengthScale, ref checks);
            ExpectStrength(contraption, wheel, cockpitB, 1200f, ref checks);

            // And the joint the game actually builds has to carry that number.
            cockpitA.EnsureRigidbody();
            metalA.EnsureRigidbody();
            contraption.AddFixedJoint(cockpitA, metalA);
            Joint cockpitJoint = contraption.FindPartJoints(cockpitA).Single();
            Require(Mathf.Abs(cockpitJoint.breakForce - 840f * ConnectionStrengthFactor()) < StrengthTolerance,
                "cockpit joint break force is " + cockpitJoint.breakForce);
            contraption.AddFixedJoint(metalB, metalA);
            Joint frameJoint = contraption.FindPartJoints(metalB).Single();
            Require(Mathf.Abs(frameJoint.breakForce - 1200f * ConnectionStrengthFactor()) < StrengthTolerance,
                "metal frame joint break force is " + frameJoint.breakForce);
            checks += 2;
        }
        catch (Exception exception)
        {
            failures.Add("connection strength: " + exception.Message);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
        return checks;
    }

    /// <summary>
    /// RCR2 puts the driver's onboard battery in the starter and terra cabins (capacity 100, part data
    /// kind "battery"); removing the driver must not take the vehicle's energy supply with it, and the
    /// other cockpits must not turn into empty containers either.
    /// </summary>
    private static int AuditCabinPower()
    {
        int batteries = 0;
        foreach (GameObject prototype in RoverPortCatalog.Instance.Parts)
        {
            var part = prototype.GetComponent<RoverPortPart>();
            RoverPortPartData data = PortData(part);
            if (data == null || !string.Equals(data.category, "cockpit", StringComparison.Ordinal))
            {
                continue;
            }
            bool sourceBattery = data.motion != null &&
                string.Equals(data.motion.kind, "battery", StringComparison.Ordinal);
            GameObject clone = null;
            try
            {
                clone = UnityEngine.Object.Instantiate(prototype);
                clone.transform.SetParent(null, false);
                var clonePart = clone.GetComponent<RoverPortPart>();
                clonePart.Awake();
                var container = clonePart as IFuelContainer;
                Require(container != null, "cabin is not a fuel container");
                float charge = container.SupplyFuelAmount;
                Require(sourceBattery ? charge > 0f : charge == 0f,
                    "cabin charge is " + charge + " for source kind " + (data.motion == null ? "none" : data.motion.kind));
                if (sourceBattery)
                {
                    batteries++;
                }
            }
            finally
            {
                if (clone != null) UnityEngine.Object.DestroyImmediate(clone);
            }
        }
        Require(batteries == 2, "expected 2 cockpits with the source battery, found " + batteries);
        return batteries;
    }

    /// <summary>
    /// The cockpit stows a part the way the game's frames do, and the game's frames stow ported
    /// parts - the weld Frame.Initialize / RoverPortCockpitPart.Initialize make.
    /// </summary>
    private static void AuditEnclosure()
    {
        var host = new GameObject("CockpitEnclosureContraption");
        var contraption = host.AddComponent<Contraption>();
        typeof(Contraption).GetMethod("PostAwake", PrivateInstance).Invoke(contraption, null);
        try
        {
            foreach (string stowedId in new[] { "part_starter_wheel", "part_terra_frame_basic" })
            {
                var cockpit = ClonePart(host, "part_starter_cockpit");
                cockpit.contraption = contraption;
                cockpit.EnsureRigidbody();
                var stowed = ClonePart(host, stowedId);
                stowed.contraption = contraption;
                stowed.EnsureRigidbody();
                cockpit.enclosedPart = stowed;
                cockpit.Initialize();
                FixedJoint weld = stowed.GetComponent<FixedJoint>();
                Require(weld != null, "no weld for the part stowed in the cabin (" + stowedId + ")");
                Require(weld.connectedBody == cockpit.GetComponent<Rigidbody>(), "cabin weld hangs on the wrong body");
                bool frameInside = RoverPortConnections.IsFramePart(stowed);
                float expected = (frameInside ? 840f : 1200f) * ConnectionStrengthFactor();
                Require(Mathf.Abs(weld.breakForce - expected) < StrengthTolerance,
                    "cabin weld with " + stowedId + " holds " + weld.breakForce + ", expected " + expected);
                UnityEngine.Object.DestroyImmediate(cockpit.gameObject);
            }

            // Requirement: any ported part except the cockpit goes inside the game's frames.
            foreach (BasePart.PartType frameType in new[] { BasePart.PartType.MetalFrame, BasePart.PartType.WoodenFrame })
            {
                var frame = NativeFrame(host, frameType.ToString(), frameType, BasePart.JointConnectionStrength.High);
                frame.gameObject.AddComponent<BoxCollider>();
                frame.EnsureRigidbody();
                frame.contraption = contraption;
                Require(frame.CanEncloseParts(), "native " + frameType + " cannot stow a part");
                var guest = ClonePart(host, "part_terra_wheel_basic");
                guest.contraption = contraption;
                guest.EnsureRigidbody();
                Require(guest.CanBeEnclosed(), "ported wheel cannot be stowed in " + frameType);
                frame.enclosedPart = guest;
                frame.Initialize();
                FixedJoint weld = guest.GetComponent<FixedJoint>();
                Require(weld != null, "no weld for the ported part inside " + frameType);
                Require(weld.connectedBody == frame.GetComponent<Rigidbody>(), frameType + " weld hangs on the wrong body");
                Require(Mathf.Abs(weld.breakForce - 1200f * ConnectionStrengthFactor()) < StrengthTolerance,
                    frameType + " weld with the ported wheel holds " + weld.breakForce);
                UnityEngine.Object.DestroyImmediate(frame.gameObject);
            }
        }
        catch (Exception exception)
        {
            failures.Add("enclosure: " + exception.Message);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    private static void ExpectStrength(Contraption contraption, BasePart first, BasePart second, float expected, ref int checks)
    {
        float value = RoverPortConnections.GetJointBreakForce(contraption, first, second);
        Require(Mathf.Abs(value - expected) < StrengthTolerance,
            "connection strength is " + value + ", expected " + expected + " (" + first.name + " / " + second.name + ")");
        checks++;
    }

    private static float ConnectionStrengthFactor()
    {
        return Mathf.Approximately(INSettings.GetFloat(INFeature.ConnectionStrength), 0f)
            ? 1f
            : INSettings.GetFloat(INFeature.ConnectionStrength);
    }

    private static BasePart NativeFrame(GameObject host, string name, BasePart.PartType type, BasePart.JointConnectionStrength strength)
    {
        var gameObject = new GameObject(name);
        gameObject.transform.SetParent(host.transform, false);
        var frame = gameObject.AddComponent<Frame>();
        frame.m_partType = type;
        frame.m_jointConnectionStrength = strength;
        frame.m_jointType = BasePart.JointType.FixedJoint;
        frame.m_jointConnectionType = BasePart.JointConnectionType.Source;
        frame.m_jointConnectionDirection = BasePart.JointConnectionDirection.Any;
        return frame;
    }

    private static RoverPortPart ClonePart(GameObject host, string id)
    {
        GameObject prototype = RoverPortCatalog.Instance.GetPart(id);
        Require(prototype != null, "missing ported part " + id);
        GameObject clone = UnityEngine.Object.Instantiate(prototype);
        clone.transform.SetParent(host.transform, false);
        clone.transform.position = Vector3.zero;
        var part = clone.GetComponent<RoverPortPart>();
        part.Awake();
        return part;
    }

    private static int CountActive(RoverPortPartData data, IEnumerable<string> nodeIds)
    {
        var byId = data.nodes.ToDictionary(n => n.id, n => n);
        int active = 0;
        foreach (string id in nodeIds)
        {
            RoverPortNodeData current;
            if (!byId.TryGetValue(id, out current)) continue;
            bool live = true;
            while (current != null)
            {
                if (!current.active) { live = false; break; }
                RoverPortNodeData parent;
                current = current.parent != null && byId.TryGetValue(current.parent, out parent) ? parent : null;
            }
            if (live) active++;
        }
        return active;
    }

    private static int Count<T>(T[] values)
    {
        return values == null ? 0 : values.Length;
    }

    private static RoverPortPartData PortData(RoverPortPart part)
    {
        return (RoverPortPartData)typeof(RoverPortPart).GetField("m_portData", PrivateInstance).GetValue(part);
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
        var gameData = ScriptableObject.CreateInstance<GameData>();
        // The values the game ships with (Assets/MonoBehaviour/GameData.asset).
        gameData.m_jointConnectionStrengthWeak = 125f;
        gameData.m_jointConnectionStrengthNormal = 125f;
        gameData.m_jointConnectionStrengthHigh = 600f;
        gameData.m_jointConnectionStrengthExtreme = 900f;
        gameData.m_jointConnectionStrengthHighlyExtreme = 1200f;
        typeof(WPFMonoBehaviour).GetField("s_gameData", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, gameData);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
