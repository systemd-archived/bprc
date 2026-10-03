using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Batch-mode harness that exercises the switchable physics backends.
/// </summary>
/// <remarks>
/// This exists so the backends can be verified from CI or a shell:
/// <code>
/// Unity.exe -batchmode -nographics -quit -projectPath . -executeMethod PhysicsBackendAudit.RunAll -logFile Logs/audit.log
/// </code>
/// Individual entries are reachable the same way, e.g. <c>-executeMethod PhysicsBackendAudit.RunJolt</c>.
/// Results are written to <c>Builds/QA/physics-backends.txt</c>; a failing check throws, which makes the
/// editor exit non-zero.
/// </remarks>
public static class PhysicsBackendAudit
{
    private const string ReportDirectory = "Builds/QA";
    private const string ReportPath = ReportDirectory + "/physics-backends.txt";

    private static readonly List<string> Results = new List<string>();
    private static int failures;

    private static void Check(bool condition, string message)
    {
        Results.Add((condition ? "PASS " : "FAIL ") + message);
        if (!condition)
        {
            failures++;
            Debug.LogError("PhysicsBackendAudit FAIL: " + message);
        }
    }

    private static Rigidbody Cube(string name, Vector3 position, bool kinematic = false)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = position;
        var body = go.AddComponent<Rigidbody>();
        body.isKinematic = kinematic;
        return body;
    }

    /// <summary>Runs every backend that reports itself available, plus the shared facade behaviour.</summary>
    public static void RunAll()
    {
        bool auto = PhysicsSimulation.Auto;
        try
        {
            PhysicsSimulation.Auto = false;
            Time.fixedDeltaTime = 0.02f;

            ReportEngines();
            AuditFacadeSwitching();
            AuditPhysX();
            if (Box3DBackend.IsSupported(out _)) AuditBackend(PhysicsEngine.Box3D);
            else Results.Add("SKIP Box3D unavailable");
            // Jolt is no longer gated: collision response works, so the audit runs it whenever the
            // native plugin loads.
            if (JoltPhysicsBackend.IsPluginUsable(out string joltReason)) AuditBackend(PhysicsEngine.Jolt);
            else Results.Add("SKIP Jolt plugin unusable: " + joltReason);
            AuditHavok();
        }
        catch (Exception error)
        {
            failures++;
            Results.Add("FAIL harness exception: " + error);
            Debug.LogError("PhysicsBackendAudit harness exception: " + error);
        }
        finally
        {
            GamePhysics.Shutdown();
            PhysicsSimulation.Auto = auto;
            Write();
        }
        if (failures > 0) throw new Exception(failures + " physics backend check(s) failed; see " + ReportPath);
    }

    public static void RunJolt()
    {
        bool auto = PhysicsSimulation.Auto;
        try
        {
            PhysicsSimulation.Auto = false;
            Time.fixedDeltaTime = 0.02f;
            // Runs Jolt directly through the backend, independent of how the engine list advertises it,
            // so this in isolation answers "is the Jolt path healthy" without the switcher involved.
            if (!JoltPhysicsBackend.IsPluginUsable(out string reason))
            {
                Results.Add("SKIP Jolt plugin unusable: " + reason);
            }
            else
            {
                AuditBackend(PhysicsEngine.Jolt);
            }
        }
        finally
        {
            GamePhysics.Shutdown();
            PhysicsSimulation.Auto = auto;
            Write();
        }
        if (failures > 0) throw new Exception(failures + " Jolt check(s) failed; see " + ReportPath);
    }

    public static void RunBox3D()
    {
        bool auto = PhysicsSimulation.Auto;
        try
        {
            PhysicsSimulation.Auto = false;
            Time.fixedDeltaTime = 0.02f;
            AuditBackend(PhysicsEngine.Box3D);
        }
        finally
        {
            GamePhysics.Shutdown();
            PhysicsSimulation.Auto = auto;
            Write();
        }
        if (failures > 0) throw new Exception(failures + " Box3D check(s) failed; see " + ReportPath);
    }

    /// <summary>
    /// Audits Havok on its own. On an editor that cannot host it this still succeeds: it asserts the
    /// refusal path, which is the behaviour that matters while the packages are absent.
    /// </summary>
    public static void RunHavok()
    {
        bool auto = PhysicsSimulation.Auto;
        try
        {
            PhysicsSimulation.Auto = false;
            Time.fixedDeltaTime = 0.02f;
            AuditHavok();
        }
        finally
        {
            GamePhysics.Shutdown();
            PhysicsSimulation.Auto = auto;
            Write();
        }
        if (failures > 0) throw new Exception(failures + " Havok check(s) failed; see " + ReportPath);
    }

    /// <summary>Confirms the engine list advertises what each engine's own probe reports.</summary>
    private static void ReportEngines()
    {
        foreach (var info in GamePhysics.Engines)
        {
            Results.Add("INFO engine " + info.Engine + " available=" + info.Available + " detail=" + info.Detail);
            if (info.Available) Check(info.Detail != null, "available engine " + info.Engine + " reports a description");
            else Check(!string.IsNullOrEmpty(info.Detail), "unavailable engine " + info.Engine + " explains why");
        }
    }

    /// <summary>
    /// The facade must be able to move between engines repeatedly without leaking or wedging state,
    /// and a request for an unavailable engine must fail cleanly back to PhysX.
    /// </summary>
    private static void AuditFacadeSwitching()
    {
        PhysicsEngine original = GamePhysics.CurrentEngine;
        Check(GamePhysics.SelectEngine(PhysicsEngine.PhysX), "select PhysX");
        Check(GamePhysics.CurrentEngine == PhysicsEngine.PhysX, "PhysX active after select");
        Check(!GamePhysics.IsBox3D, "no native backend installed under PhysX");

        // PhysX must still simulate through the facade.
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "facade ground";
        ground.transform.position = new Vector3(0f, -0.5f, 0f);
        ground.transform.localScale = new Vector3(20f, 1f, 20f);
        var falling = Cube("facade faller", new Vector3(0f, 4f, 0f));
        for (int i = 0; i < 200; i++) GamePhysics.Simulate(0.02f);
        Check(falling.position.y < 1.2f && falling.position.y > 0.3f,
            "PhysX through the facade settles a cube, y=" + falling.position.y);

        // An unavailable engine must be refused, leave PhysX running, and record a reason.
        if (!HavokPhysicsBackend.IsSupported(out _))
        {
            Check(!GamePhysics.SelectEngine(PhysicsEngine.Havok), "selecting Havok is refused");
            Check(GamePhysics.CurrentEngine == PhysicsEngine.PhysX, "PhysX stays active after a refused switch");
            Check(!string.IsNullOrEmpty(GamePhysics.LastError), "a refused switch records LastError");
        }

        UnityEngine.Object.DestroyImmediate(falling.gameObject);
        UnityEngine.Object.DestroyImmediate(ground.gameObject);
        GamePhysics.Shutdown();
        Results.Add("INFO facade audit done, was " + original);
    }

    private static void AuditPhysX()
    {
        Results.Add("INFO auditing PhysX");
        // Covered by AuditFacadeSwitching; kept as a named step for the report.
    }

    /// <summary>
    /// Drives one native backend through the behaviours the game depends on: gravity, rest on a
    /// static body, force application, ignored pairs, mass, sleeping, and repeated enable/disable.
    /// </summary>
    private static void AuditBackend(PhysicsEngine engine)
    {
        Results.Add("INFO auditing " + engine);
        if (!GamePhysics.SelectEngine(engine))
        {
            Check(false, engine + " could not start: " + GamePhysics.LastError);
            return;
        }
        Check(GamePhysics.CurrentEngine == engine, engine + " reports itself active");

        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = engine + " ground";
        ground.transform.position = new Vector3(0f, -0.5f, 0f);
        ground.transform.localScale = new Vector3(40f, 1f, 40f);

        var cube = Cube(engine + " cube", new Vector3(0f, 3f, 0f));
        for (int i = 0; i < 200; i++) GamePhysics.Simulate(0.02f);
        Check(GamePhysics.CurrentEngine == engine, engine + " survived the settle loop, error=" + GamePhysics.LastError);
        Check(Mathf.Abs(cube.position.y - 0.5f) < 0.2f, engine + " cube rests on the ground, y=" + cube.position.y);
        Check(Mathf.Abs(cube.velocity.y) < 0.5f, engine + " cube is at rest, vy=" + cube.velocity.y);

        // Forces routed through the facade must move the body.
        Vector3 before = cube.position;
        cube.EngineAddForce(Vector3.up * 8f, ForceMode.VelocityChange);
        for (int i = 0; i < 4; i++) GamePhysics.Simulate(0.02f);
        Check(cube.position.y > before.y + 0.2f, engine + " honours EngineAddForce, y=" + cube.position.y);

        // An ignored pair must pass through another body.
        var blocker = Cube(engine + " blocker", new Vector3(0f, 4f, 0f));
        blocker.constraints = RigidbodyConstraints.FreezeAll;
        GamePhysics.IgnoreCollision(cube.GetComponent<Collider>(), blocker.GetComponent<Collider>());
        cube.position = new Vector3(0f, 8f, 0f);
        cube.velocity = Vector3.zero;
        for (int i = 0; i < 120; i++) GamePhysics.Simulate(0.02f);
        Check(cube.position.y < 4f, engine + " passes through an ignored pair, y=" + cube.position.y);
        GamePhysics.IgnoreCollision(cube.GetComponent<Collider>(), blocker.GetComponent<Collider>(), false);

        // Sleeping round-trip.
        cube.EngineSleep();
        Check(cube.EngineIsSleeping() || !GamePhysics.IsBox3D, engine + " reports a slept body (best effort)");
        cube.EngineWakeUp();

        UnityEngine.Object.DestroyImmediate(blocker.gameObject);
        UnityEngine.Object.DestroyImmediate(cube.gameObject);
        UnityEngine.Object.DestroyImmediate(ground.gameObject);
        for (int i = 0; i < 3; i++) GamePhysics.Simulate(0.02f);

        // Repeated switching must not wedge the backend.
        GamePhysics.Shutdown();
        Check(GamePhysics.CurrentEngine == PhysicsEngine.PhysX, engine + " shutdown returns to PhysX");
        Check(GamePhysics.SelectEngine(engine), engine + " can be re-selected after shutdown");
        Check(GamePhysics.SelectEngine(engine), engine + " re-selecting the active engine is a no-op success");
        GamePhysics.Shutdown();
    }

    /// <summary>
    /// Audits Havok, which is version gated rather than absent. On an editor that can host Havok
    /// Physics for Unity the backend has to run like any other; everywhere else it has to refuse
    /// loudly and say exactly what is missing.
    /// </summary>
    private static void AuditHavok()
    {
        Results.Add("INFO auditing Havok: " + HavokPhysicsBackend.Describe());

        if (HavokPhysicsBackend.IsSupported(out string reason))
        {
            AuditBackend(PhysicsEngine.Havok);
            return;
        }

        Check(!string.IsNullOrEmpty(reason), "Havok explains why it is unavailable");
        Check(reason != HavokPhysicsBackend.MinimumUnityVersion, "Havok's reason is specific, not a placeholder");
        Check(!GamePhysics.SelectEngine(PhysicsEngine.Havok), "selecting Havok is refused");
        Check(GamePhysics.CurrentEngine == PhysicsEngine.PhysX, "PhysX stays active after a refused Havok switch");
        Check(!string.IsNullOrEmpty(GamePhysics.LastError), "a refused Havok switch records LastError");

        bool threw = false;
        try { new HavokPhysicsBackend(); }
        catch (NotSupportedException) { threw = true; }
        catch (Exception error) { Results.Add("INFO Havok ctor threw " + error.GetType().Name); threw = true; }
        Check(threw, "constructing the Havok backend fails loudly rather than silently");
    }

    private static void Write()
    {
        Directory.CreateDirectory(ReportDirectory);
        var builder = new StringBuilder();
        foreach (string line in Results) builder.AppendLine(line);
        builder.AppendLine("FAILURES: " + failures);
        File.WriteAllText(ReportPath, builder.ToString());
        Debug.Log("PhysicsBackendAudit wrote " + ReportPath + " (" + failures + " failure(s))");
    }
}
