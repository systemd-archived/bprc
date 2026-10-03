using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Prints the managed size of every Jolt interop struct next to the size the native plugin expects.
/// </summary>
/// <remarks>
/// A layout mismatch here does not raise a managed exception – it corrupts the settings block and
/// crashes inside Jolt. Run this after touching anything in <c>JoltNative</c>:
/// <code>
/// Unity.exe -batchmode -nographics -quit -projectPath . -executeMethod JoltLayoutAudit.Run -logFile Logs/layout.log
/// </code>
/// </remarks>
public static class JoltLayoutAudit
{
    public static void Run()
    {
        Directory.CreateDirectory("Builds/QA");
        File.WriteAllText("Builds/QA/jolt-layout.txt", JoltPhysicsBackend.DescribeInteropLayout());
        Debug.Log("JoltLayoutAudit wrote Builds/QA/jolt-layout.txt");
    }
}
