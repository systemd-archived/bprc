using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Focused probe for Jolt ground/body collision: builds a static ground and a falling cube, steps the
/// simulation and reports positions, plus what the layer filters think of the relevant pair.
/// </summary>
/// <remarks>
/// <code>
/// Unity.exe -batchmode -nographics -quit -projectPath . -executeMethod JoltFallProbe.Run -logFile Logs/fall.log
/// </code>
/// </remarks>
public static class JoltFallProbe
{
    public static void Run()
    {
        bool auto = PhysicsSimulation.Auto;
        var combined = new StringBuilder();
        try
        {
            PhysicsSimulation.Auto = false;
            Time.fixedDeltaTime = 0.02f;
            combined.AppendLine(Fall());
        }
        catch (Exception error)
        {
            combined.AppendLine("EXCEPTION " + error);
        }
        finally
        {
            GamePhysics.Shutdown();
            PhysicsSimulation.Auto = auto;
            Directory.CreateDirectory("Builds/QA");
            File.WriteAllText("Builds/QA/jolt-fall.txt", combined.ToString());
        }
    }

    private static string Fall()
    {
        var log = new StringBuilder();
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "probe ground";
        ground.transform.position = new Vector3(0f, -0.5f, 0f);
        ground.transform.localScale = new Vector3(40f, 1f, 40f);

        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "probe cube";
        cube.transform.position = new Vector3(0f, 3f, 0f);
        var body = cube.AddComponent<Rigidbody>();

        log.AppendLine("cube layer=" + cube.layer + " ground layer=" + ground.layer);
        log.AppendLine("select Jolt: " + GamePhysics.SelectEngine(PhysicsEngine.Jolt) + " err=" + GamePhysics.LastError);

        try
        {
            for (int i = 0; i < 80; i++)
            {
                GamePhysics.Simulate(0.02f);
                if (i % 20 == 0 || i == 79)
                    log.AppendLine("  step " + i + " cubeY=" + cube.transform.position.y.ToString("F3") +
                        " vy=" + body.velocity.y.ToString("F3") +
                        " groundY=" + ground.transform.position.y.ToString("F3"));
            }
            log.AppendLine(GamePhysics.DescribeActiveBackend());
            log.AppendLine("--- raycast ---");
            log.AppendLine(GamePhysics.DescribeActiveBackendRaycast());
            log.AppendLine("--- filtering ---");
            log.AppendLine(GamePhysics.DescribeActiveBackendFiltering());
        }
        finally
        {
            GamePhysics.Shutdown();
            UnityEngine.Object.DestroyImmediate(cube);
            UnityEngine.Object.DestroyImmediate(ground);
        }
        return log.ToString();
    }
}
