using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Guards the five reported bugs fixed in this pass:
///
/// 1. 部件计数器 never counted - settings written in the app interface were scoped to a sandbox that
///    does not exist outside a sandbox level, so the value was never read back (INSettings).
/// 2. closing the pause menu dropped the vehicle's speed - the ported drive parts gated on
///    LevelManager.gameState, which the app interface did not restore (RoverPortPoweredPart,
///    INAppInterface).
/// 3. overlapping same-faction vehicles tilted - Physics.IgnoreCollision removes contacts, not
///    raycasts, so wheel ground probes and the anti-tunnel check still acted on the phased hull
///    (MarkerManager.IsPhasedVehicle, MotorWheel / OffRoadWheel / StickyWheel / INContraption).
/// 4. overlapping same-faction vehicles shared engines and keys - joints and buttons welded the two
///    vehicles together (Kicker, FrameJointManager, Contraption.OnButtonTriggered).
/// 5. the RCR2 cockpit turned itself when a pig was stowed in it and then ignored the mouse - the
///    cabin stayed marked rotatable (RoverPortCockpitPart; also covered by RoverPortCockpitAudit).
///
/// The behavioural checks here run in edit mode; the cockpit audit is run first so that its own
/// assertions (including the new rotation contract) fail this run too.
///
/// Run with: Unity -batchmode -executeMethod BugFixAudit.Run
/// Report: Builds/QA/bugfix-audit.txt
/// </summary>
public static class BugFixAudit
{
    private static readonly List<string> failures = new List<string>();
    private static int checks;

    [MenuItem("Tools/QA/Audit reported bug fixes")]
    public static void Run()
    {
        // Compile+behaviour gate for bug 5 (it throws when it fails, which fails this run as well).
        RoverPortCockpitAudit.Run();

        AuditIgnoredPairPredicate();
        AuditSourceContracts();

        Directory.CreateDirectory("Builds/QA");
        File.WriteAllLines("Builds/QA/bugfix-audit.txt", new List<string> {
            "Checks: " + checks,
            "Failures: " + failures.Count }.Concat(failures));
        if (failures.Count > 0)
        {
            throw new Exception(string.Join("\n", failures));
        }
        Debug.Log("BUGFIX_AUDIT_PASS: " + checks + " checks over the five reported fixes");
    }

    /// <summary>
    /// The whole bug 3 fix rests on this predicate: a pair that is told to ignore collisions has to be
    /// recognisable from a query hit, because Unity drops the contacts but keeps the raycast hits.
    /// </summary>
    private static void AuditIgnoredPairPredicate()
    {
        var first = new GameObject("BugFixAuditA");
        var second = new GameObject("BugFixAuditB");
        try
        {
            Collider a = first.AddComponent<BoxCollider>();
            Collider b = second.AddComponent<BoxCollider>();
            Require(!GamePhysics.IsIgnoredPair(a, b), "a fresh collider pair already reports as ignored");
            Require(!GamePhysics.IsIgnoredPair(null, b), "a null collider must not report as ignored");
            GamePhysics.IgnoreCollision(a, b, true);
            Require(GamePhysics.IsIgnoredPair(a, b), "an ignored pair is not reported as ignored");
            Require(GamePhysics.IsIgnoredPair(b, a), "the ignored pair is not symmetric");
            GamePhysics.IgnoreCollision(a, b, false);
            Require(!GamePhysics.IsIgnoredPair(a, b), "a restored pair still reports as ignored");
            Require(!MarkerManager.IsPhasedVehicle(null, b), "IsPhasedVehicle must accept a null part");
            checks += 6;
        }
        catch (Exception exception)
        {
            failures.Add("ignored pair predicate: " + exception.Message);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(first);
            UnityEngine.Object.DestroyImmediate(second);
        }
    }

    /// <summary>
    /// Source contracts for the fixes that cannot be exercised without a level and a running
    /// contraption (the drive gate, the pause handover, the weld/button identity rules). Each entry is
    /// a file that must and must not contain a marker, so undoing a fix fails this audit.
    /// </summary>
    private static void AuditSourceContracts()
    {
        Expect("Assets/Scripts/Assembly-CSharp/RCR2Port/RoverPortPoweredPart.cs", "contraption.IsRunning", true);
        Expect("Assets/Scripts/Assembly-CSharp/RCR2Port/RoverPortPoweredPart.cs", "LevelManager.GameState.Running", false);
        Expect("Assets/Scripts/Assembly-CSharp/INAppInterface.cs", "PausedWhileRunning", true);
        Expect("Assets/Scripts/Assembly-CSharp/INSettings.cs", "SettingScope.Global", true);
        Expect("Assets/Scripts/Assembly-CSharp/INSelectionManager.cs", "OnPartPlacedForCounters", true);
        Expect("Assets/Scripts/Assembly-CSharp/INContraption.cs", "MarkerManager.IsPhasedVehicle", true);
        Expect("Assets/Scripts/Assembly-CSharp/MotorWheel.cs", "MarkerManager.IsPhasedVehicle", true);
        Expect("Assets/Scripts/Assembly-CSharp/OffRoadWheel.cs", "MarkerManager.IsPhasedVehicle", true);
        Expect("Assets/Scripts/Assembly-CSharp/StickyWheel.cs", "MarkerManager.IsPhasedVehicle", true);
        Expect("Assets/Scripts/Assembly-CSharp/Kicker.cs", "MarkerManager.IsInSameTeamStatic", true);
        Expect("Assets/Scripts/Assembly-CSharp/FrameJointManager.cs", "MarkerManager.IsInSameTeamStatic", true);
        Expect("Assets/Scripts/Assembly-CSharp/Contraption.cs", "components.Contains(part.ConnectedComponent)", true);
        Expect("Assets/Scripts/Assembly-CSharp/RCR2Port/RoverPortCockpitPart.cs", "AutoAlignType.None", true);
    }

    private static void Expect(string path, string marker, bool shouldContain)
    {
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception exception)
        {
            failures.Add(path + ": " + exception.Message);
            return;
        }
        bool contains = text.Contains(marker);
        if (contains != shouldContain)
        {
            failures.Add(path + (shouldContain ? " no longer contains " : " still contains ") + "'" + marker + "'");
            return;
        }
        checks++;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
