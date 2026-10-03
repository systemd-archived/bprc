using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using RCR2Port;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Verifies that a native part next to a ported part cannot lock the game up in the connection
/// alignment, and that a gone part stops the build UI from throwing (RCR2_PORT.md).
///
/// Connecting a flag to a fan froze the game with a StackOverflowException: Contraption.AutoAlign asks
/// the part to re-check its connections (BasePart.OnChangeConnections), which asks to align the very
/// same part again when it still cannot connect, and a ported neighbour can never satisfy the game's
/// connection test for the direction it asks about (the port matches its own connection points, see
/// RoverPortConnections) - the flag's connection points sit on half cells, which the native check reads
/// as "not aligned". Re-entering the build UI then spammed NullReferenceException from AutoAlign's
/// GetComponent because the destroyed parts of the stopped contraption were still listed.
///
/// The checks below build that fixture - the ported flag with the native fan beside it - and exercise
/// both paths from the crash reports.
///
/// Run with: Unity -batchmode -quit -executeMethod RoverPortAlignAudit.Run
/// Report: Builds/QA/align-audit.txt
/// </summary>
public static class RoverPortAlignAudit
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    private static readonly List<string> failures = new List<string>();

    [MenuItem("Tools/RCR2/Audit connection alignment")]
    public static void Run()
    {
        PrepareEditorServices();
        var lines = new List<string>();
        var host = new GameObject("AlignAuditHost");
        var contraptionObject = new GameObject("AlignAuditContraption");
        contraptionObject.transform.SetParent(host.transform, false);
        var contraption = contraptionObject.AddComponent<Contraption>();
        typeof(Contraption).GetMethod("PostAwake", PrivateInstance).Invoke(contraption, null);
        try
        {
            RoverPortPart flag = PortedPart(contraption, "part_terra_flag", 0, 0);
            AuditFan fan = NativeFan(contraption, 1, 0);
            Require(flag != null, "ported flag not available");

            // The placing path: the fan is asked to align itself onto the connection source beside it,
            // which it already faces. The port makes the connection, but the game's own check for that
            // direction stays false, so this used to recurse until the stack ran out.
            contraption.AutoAlign(fan);
            int aligned = fan.changeCount;
            Require(aligned <= 3, "AutoAlign re-entered the fan " + aligned + " times");
            lines.Add("fan OnChangeConnections after one AutoAlign: " + aligned);

            // The build UI path: entering the build state refreshes every part's connections.
            contraption.RefreshConnections();
            int refreshed = fan.changeCount - aligned;
            Require(refreshed <= 3, "RefreshConnections re-entered the fan " + refreshed + " times");
            lines.Add("fan OnChangeConnections after RefreshConnections: " + refreshed);

            // A native part aligned onto a native connection source still converges (one alignment).
            var nativeSource = NativeSource(contraption, 2, 0);
            AuditFan nativeFan = NativeFan(contraption, 3, 0);
            contraption.AutoAlign(nativeFan);
            Require(nativeFan.changeCount <= 1, "native fan was re-aligned " + nativeFan.changeCount + " times");
            lines.Add("native fan OnChangeConnections with a native source beside it: " + nativeFan.changeCount);
            UnityEngine.Object.DestroyImmediate(nativeSource.gameObject);
            UnityEngine.Object.DestroyImmediate(nativeFan.gameObject);

            // The ported flag has one connection point on the bottom edge of its cells, so a native part
            // below it is the placement that really connects: the alignment turns that part onto the
            // flag and the connection check then accepts the port's own match.
            AuditFan connectedFan = NativeFan(contraption, 0, -1);
            contraption.AutoAlign(connectedFan);
            Require(contraption.CanConnectTo(connectedFan, connectedFan.GetJointConnectionDirection()),
                "the flag's own connection point did not join the native part below it");
            Require(connectedFan.changeCount <= 3, "connected fan was re-aligned " + connectedFan.changeCount + " times");
            lines.Add("native fan under the flag: aligned " + connectedFan.changeCount + " time(s), connected: " +
                contraption.CanConnectTo(connectedFan, connectedFan.GetJointConnectionDirection()));
            UnityEngine.Object.DestroyImmediate(connectedFan.gameObject);

            // A part that is gone must be dropped instead of throwing from AutoAlign's GetComponent.
            UnityEngine.Object.DestroyImmediate(fan.gameObject);
            int before = contraption.Parts.Count;
            contraption.RefreshConnections();
            Require(!contraption.Parts.Any(part => part == null), "a destroyed part stayed in the contraption");
            lines.Add("parts after refreshing with a destroyed part: " + before + " -> " + contraption.Parts.Count);
        }
        catch (Exception exception)
        {
            failures.Add(exception.ToString());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }

        Directory.CreateDirectory("Builds/QA");
        File.WriteAllLines("Builds/QA/align-audit.txt", new List<string> {
            "Failures: " + failures.Count }.Concat(failures).Concat(lines));
        if (failures.Count > 0)
        {
            throw new Exception(string.Join("\n", failures));
        }
        Debug.Log("RCR2_ALIGN_PASS: flag/fan alignment terminates and a destroyed part is dropped");
    }

    private static RoverPortPart PortedPart(Contraption contraption, string id, int x, int y)
    {
        GameObject prototype = RoverPortCatalog.Instance.GetPart(id);
        if (prototype == null)
        {
            return null;
        }
        GameObject clone = UnityEngine.Object.Instantiate(prototype);
        clone.transform.SetParent(contraption.transform, false);
        clone.transform.position = new Vector3(x, y, 0f);
        var part = clone.GetComponent<RoverPortPart>();
        part.Awake();
        Place(contraption, part, x, y);
        return part;
    }

    private static AuditFan NativeFan(Contraption contraption, int x, int y)
    {
        var partObject = new GameObject("AuditNativeFan");
        partObject.transform.SetParent(contraption.transform, false);
        partObject.transform.position = new Vector3(x, y, 0f);
        var fan = partObject.AddComponent<AuditFan>();
        fan.m_partType = BasePart.PartType.Fan;
        fan.m_autoAlign = BasePart.AutoAlignType.Rotate;
        fan.m_jointType = BasePart.JointType.FixedJoint;
        fan.m_jointConnectionType = BasePart.JointConnectionType.Target;
        fan.m_jointConnectionDirection = BasePart.JointConnectionDirection.Left;
        fan.m_gridXmin = fan.m_gridXmax = fan.m_gridYmin = fan.m_gridYmax = 0;
        Place(contraption, fan, x, y);
        return fan;
    }

    private static BasePart NativeSource(Contraption contraption, int x, int y)
    {
        var partObject = new GameObject("AuditNativeFrame");
        partObject.transform.SetParent(contraption.transform, false);
        partObject.transform.position = new Vector3(x, y, 0f);
        var part = partObject.AddComponent<BasePart>();
        part.m_partType = BasePart.PartType.MetalFrame;
        part.m_jointType = BasePart.JointType.FixedJoint;
        part.m_jointConnectionType = BasePart.JointConnectionType.Source;
        part.m_jointConnectionDirection = BasePart.JointConnectionDirection.Any;
        part.m_gridXmin = part.m_gridXmax = part.m_gridYmin = part.m_gridYmax = 0;
        Place(contraption, part, x, y);
        return part;
    }

    private static void Place(Contraption contraption, BasePart part, int x, int y)
    {
        part.contraption = contraption;
        part.m_coordX = x;
        part.m_coordY = y;
        contraption.Parts.Add(part);
        contraption.SetPartMap(x, y, part.GenerationIndex, part);
    }

    /// <summary>The native fan, counting how often the alignment code asks it to re-check its connections.</summary>
    private class AuditFan : BasePart
    {
        public int changeCount;

        public override void OnChangeConnections()
        {
            changeCount++;
            base.OnChangeConnections();
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    /// <summary>The connection checks read INSettings (the time bomb connection feature, for one), so the editor needs them loaded the way the game loads them.</summary>
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
}
