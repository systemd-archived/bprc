using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Runs <see cref="JoltPhysicsBackend.ProbeBodyCreation"/> and writes the matrix to disk.
/// </summary>
/// <remarks>
/// <code>
/// Unity.exe -batchmode -nographics -quit -projectPath . -executeMethod JoltBodyProbe.Run -logFile Logs/bodyprobe.log
/// </code>
/// </remarks>
public static class JoltBodyProbe
{
    public static void Run()
    {
        string report = JoltPhysicsBackend.ProbeBodyCreation();
        Directory.CreateDirectory("Builds/QA");
        File.WriteAllText("Builds/QA/jolt-bodyprobe.txt", report);
        Debug.Log("JoltBodyProbe wrote Builds/QA/jolt-bodyprobe.txt");
    }
}
