using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Minimal isolation harness for the Jolt start-up path.
/// </summary>
/// <remarks>
/// Constructs the backend and runs empty synchronisation steps so a native crash can be attributed to
/// a specific phase. Run with:
/// <code>
/// Unity.exe -batchmode -nographics -quit -projectPath . -executeMethod JoltMinimalProbe.Run -logFile Logs/probe.log
/// </code>
/// </remarks>
public static class JoltMinimalProbe
{
    public static void Run()
    {
        var log = new System.Text.StringBuilder();
        void Say(string s) { log.AppendLine(s); Debug.Log("JoltProbe: " + s); }

        Say("supported = " + JoltPhysicsBackend.IsSupported(out string reason) + " (" + reason + ")");

        JoltPhysicsBackend probe = null;
        try
        {
            probe = new JoltPhysicsBackend();
            Say("constructed");
            probe.Synchronize();
            Say("empty Synchronize ok");
            probe.Step(0.02f);
            Say("empty Step ok");
        }
        catch (Exception error)
        {
            Say("EXCEPTION " + error);
        }
        finally
        {
            try { probe?.Dispose(); Say("disposed"); }
            catch (Exception error) { Say("dispose EXCEPTION " + error); }
        }

        // Now one body, no collider, to exercise CreateAndAddBody on its own.
        try
        {
            var go = new GameObject("probe body");
            go.transform.position = new Vector3(0f, 5f, 0f);
            go.AddComponent<Rigidbody>();
            probe = new JoltPhysicsBackend();
            Say("constructed (second)");
            probe.Synchronize();
            Say("Synchronize with 1 body ok, y=" + go.transform.position.y);
            probe.Dispose();
            Say("disposed (second)");
            UnityEngine.Object.DestroyImmediate(go);
        }
        catch (Exception error)
        {
            Say("BODY EXCEPTION " + error);
        }

        Directory.CreateDirectory("Builds/QA");
        File.WriteAllText("Builds/QA/jolt-probe.txt", log.ToString());
    }
}
