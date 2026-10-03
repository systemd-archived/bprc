using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Checks how the JoltC plugin behaves across repeated initialise/shutdown cycles, and across
/// repeated world creation within a single initialisation.
/// </summary>
/// <remarks>
/// <c>JoltC_Shutdown</c> tears down global plugin state, so whether it can be followed by another
/// <c>JoltC_Init</c> decides how the backend may be constructed and disposed. Run with:
/// <code>
/// Unity.exe -batchmode -nographics -quit -projectPath . -executeMethod JoltLifecycleProbe.Run -logFile Logs/lifecycle.log
/// </code>
/// </remarks>
public static class JoltLifecycleProbe
{
    [DllImport("libJoltC", CallingConvention = CallingConvention.Cdecl)]
    private static extern int JoltC_Init();

    [DllImport("libJoltC", CallingConvention = CallingConvention.Cdecl)]
    private static extern void JoltC_Shutdown();

    public static void Run()
    {
        var log = new System.Text.StringBuilder();
        void Say(string s) { log.AppendLine(s); Debug.Log("JoltLifecycle: " + s); }

        // Phase A: can the plugin be re-initialised after a shutdown?
        for (int cycle = 1; cycle <= 5; cycle++)
        {
            int rc = JoltC_Init();
            Say("A cycle " + cycle + " Init -> " + rc);
            if (rc == 0) { Say("A init failed; stopping phase A"); break; }
            JoltC_Shutdown();
            Say("A cycle " + cycle + " Shutdown ok");
        }

        // Phase B: two worlds inside one initialisation.
        Say("B init -> " + JoltC_Init());
        for (int i = 1; i <= 2; i++)
        {
            try
            {
                using (var probe = new JoltPhysicsBackend())
                {
                    probe.Synchronize();
                    Say("B world " + i + " ok");
                }
                Say("B world " + i + " disposed");
            }
            catch (Exception error)
            {
                Say("B world " + i + " EXCEPTION " + error.GetType().Name + ": " + error.Message);
            }
            try { JoltC_Init(); } catch (Exception error) { Say("B re-init threw " + error.Message); }
        }

        Directory.CreateDirectory("Builds/QA");
        File.WriteAllText("Builds/QA/jolt-lifecycle.txt", log.ToString());
    }
}
