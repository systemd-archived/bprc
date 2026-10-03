using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Runtime audit for the Bepu backend, in the same shape as <see cref="PhysicsBackendAudit"/>: it
/// builds a throwaway scene, drives the facade by hand and writes a report under
/// <c>Builds/QA/</c>. Batch-runnable without the editor being free for interactive work:
///
/// <code>
/// Tools\unity-batch.bat -executeMethod BepuBackendAudit.Run
/// </code>
///
/// Unlike the Jolt harness this needs no native plugin, so the editor is enough.
/// </summary>
public static class BepuBackendAudit
{
    private static int failures;
    private static readonly List<string> report = new List<string>();

    private static void Check(string name, bool ok, string detail)
    {
        report.Add((ok ? "PASS  " : "FAIL  ") + name.PadRight(52) + detail);
        if (!ok) failures++;
    }

    public static void Run()
    {
        failures = 0;
        report.Clear();
        report.Add("Bepu backend audit - Unity " + Application.unityVersion);

        bool available = BepuBackend.IsSupported(out string reason);
        Check("plugin loads / IsSupported", available, available ? "BepuPhysics resolved" : reason);

        var previous = GamePhysics.CurrentEngine;
        try
        {
            RunScenarios();
        }
        finally
        {
            GamePhysics.Shutdown();
            if (previous != PhysicsEngine.PhysX) GamePhysics.SelectEngine(previous);
        }

        report.Add("");
        report.Add(failures == 0 ? "FAILURES: 0" : "FAILURES: " + failures);
        Directory.CreateDirectory("Builds/QA");
        File.WriteAllLines("Builds/QA/bepu-backend.txt", report);
        foreach (string line in report) Debug.Log("[bepu-audit] " + line);
        // Only a batch run may terminate the process: this method is also reachable through the DSH
        // bridge, where an exit would close the editor the user is working in.
        if (failures != 0 && Application.isBatchMode) EditorApplication.Exit(1);
    }

    private static void RunScenarios()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        _ = scene;

        // A static ground plane, exactly as the game's levels provide one: a collider with no Rigidbody.
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.position = new Vector3(0f, -0.5f, 0f);
        ground.transform.localScale = new Vector3(60f, 1f, 60f);

        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = "FallingBox";
        box.transform.position = new Vector3(0f, 6f, 0f);
        var body = box.AddComponent<Rigidbody>();
        body.mass = 1f;
        body.useGravity = true;

        bool selected = GamePhysics.SelectEngine(PhysicsEngine.Bepu);
        Check("SelectEngine(Bepu) installs the backend", selected,
            selected ? GamePhysics.CurrentEngineName : GamePhysics.LastError);

        if (!selected) return;

        for (int i = 0; i < 400; i++) GamePhysics.Simulate(1f / 60f);

        Check("box falls and rests on the ground", Mathf.Abs(box.transform.position.y - 0.5f) < 0.2f,
            "y=" + box.transform.position.y.ToString("F4"));
        Check("box is at rest", body.velocity.magnitude < 0.2f,
            "|v|=" + body.velocity.magnitude.ToString("F4"));

        // Ignored pairs must suppress contacts: park a second box right on top of the first and tell the
        // pair to ignore each other, then confirm it sinks through instead of stacking.
        var ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ghost.name = "Ghost";
        ghost.transform.position = new Vector3(4f, 6f, 0f);
        var ghostBody = ghost.AddComponent<Rigidbody>();
        ghostBody.mass = 1f;

        var platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
        platform.name = "Platform";
        platform.transform.position = new Vector3(4f, 3f, 0f);
        platform.transform.localScale = new Vector3(4f, 0.4f, 4f);

        GamePhysics.IgnoreCollision(ghost.GetComponent<Collider>(), platform.GetComponent<Collider>(), true);
        for (int i = 0; i < 300; i++) GamePhysics.Simulate(1f / 60f);
        Check("IgnoredPairs let a body pass through", ghost.transform.position.y < 2.6f,
            "y=" + ghost.transform.position.y.ToString("F4"));

        // Waking and sleeping must reach the native body.
        GamePhysics.EngineSleep(body);
        bool sleeping = GamePhysics.EngineIsSleeping(body);
        GamePhysics.EngineWakeUp(body);
        Check("EngineSleep/EngineIsSleeping/EngineWakeUp round-trip", sleeping, "sleeping=" + sleeping);

        // Drive with an engine force and confirm it moves the body.
        float before = box.transform.position.z;
        for (int i = 0; i < 60; i++)
        {
            body.EngineAddForce(new Vector3(0f, 0f, 12f));
            GamePhysics.Simulate(1f / 60f);
        }
        Check("EngineAddForce moves the body", box.transform.position.z - before > 0.2f,
            "dz=" + (box.transform.position.z - before).ToString("F4"));

        // A jointed pair must hold together rather than drift apart.
        var anchorObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        anchorObject.name = "JointAnchor";
        anchorObject.transform.position = new Vector3(20f, 4f, 0f);
        var anchorBody = anchorObject.AddComponent<Rigidbody>();
        anchorBody.mass = 4f;

        var linked = GameObject.CreatePrimitive(PrimitiveType.Cube);
        linked.name = "JointLinked";
        linked.transform.position = new Vector3(21.5f, 4f, 0f);
        var linkedBody = linked.AddComponent<Rigidbody>();
        linkedBody.mass = 1f;

        var fixedJoint = linked.AddComponent<FixedJoint>();
        fixedJoint.connectedBody = anchorBody;
        fixedJoint.anchor = Vector3.zero;
        fixedJoint.connectedAnchor = new Vector3(1.5f, 0f, 0f);

        for (int i = 0; i < 200; i++) GamePhysics.Simulate(1f / 60f);
        float separation = Vector3.Distance(anchorObject.transform.position, linked.transform.position);
        Check("FixedJoint welds the pair (distance held)", Mathf.Abs(separation - 1.5f) < 0.6f,
            "separation=" + separation.ToString("F4"));

        // Switching away must tear the backend down cleanly and leave PhysX driving.
        GamePhysics.Shutdown();
        Check("Shutdown returns to PhysX", GamePhysics.CurrentEngine == PhysicsEngine.PhysX,
            GamePhysics.CurrentEngineName);
    }
}
