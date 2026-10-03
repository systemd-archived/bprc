using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BadPiggies.Multiplayer;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Applies the multiplayer machine layer's collision matrix through the physics API rather than by
/// editing the serialised bit blob in ProjectSettings/DynamicsManager.asset, which has a non-obvious
/// layer/bit layout.
///
/// Target state, taken from the reference build: the machine layer collides with the world and with
/// itself, but never with HUD, NonCollidingPart, Bird, Food, Mouth, IcePart or Light. The runtime
/// applies the same matrix from code (MultiplayerLayers.ApplyCollisionMatrix), so this command is
/// about persisting the state in ProjectSettings as well.
/// </summary>
public static class MultiplayerSettings
{
    /// <summary>Layers the machine layer must not collide with, exactly as in the reference build.</summary>
    private static readonly int[] IsolatedLayers = { 10, 15, 16, 17, 18, 22, 23 };

    private const string ReportPath = "Builds/QA/multiplayer-settings-api.txt";
    private static readonly List<string> s_log = new List<string>();

    [MenuItem("Tools/Multiplayer/Apply Multiplayer Layers")]
    public static void Run()
    {
        s_log.Clear();
        int parts = LayerMask.NameToLayer(MultiplayerLayers.PartsLayerName);
        if (parts != MultiplayerLayers.PartsLayerIndex)
        {
            throw new InvalidOperationException(string.Format(
                "Layer {0} is '{1}', expected '{2}' - fill it in ProjectSettings/TagManager.asset first",
                MultiplayerLayers.PartsLayerIndex, LayerMask.LayerToName(MultiplayerLayers.PartsLayerIndex),
                MultiplayerLayers.PartsLayerName));
        }
        s_log.Add("layer " + parts + " = " + MultiplayerLayers.PartsLayerName);

        MultiplayerLayers.ApplyCollisionMatrix();
        AssetDatabase.SaveAssets();
        EditorApplication.ExecuteMenuItem("File/Save Project");

        Verify(parts);
        WriteReport();
        Debug.Log("MULTIPLAYER_SETTINGS_PASS " + string.Join(" | ", s_log));
    }

    private static void Verify(int parts)
    {
        if (Physics.GetIgnoreLayerCollision(parts, parts))
        {
            throw new InvalidOperationException("The machine layer ignores itself, so a machine would fall apart");
        }
        foreach (int other in IsolatedLayers)
        {
            if (!Physics.GetIgnoreLayerCollision(parts, other))
            {
                throw new InvalidOperationException(string.Format(
                    "{0} still collides with layer {1} ({2})",
                    MultiplayerLayers.PartsLayerName, other, LayerMask.LayerToName(other)));
            }
        }
        // The machines must still be solid against the world: spot check a few world layers.
        foreach (int other in new[] { 0, 8, 9, 11, 19, 20 })
        {
            if (Physics.GetIgnoreLayerCollision(parts, other))
            {
                throw new InvalidOperationException(string.Format(
                    "The machine layer no longer collides with layer {0} ({1})",
                    other, LayerMask.LayerToName(other)));
            }
        }
        string error;
        if (!MultiplayerLayers.VerifyCollisionMatrix(out error))
        {
            throw new InvalidOperationException(error);
        }
        s_log.Add("matrix verified through the physics API (" + IsolatedLayers.Length +
                  " isolated layers, world collision kept)");
    }

    private static void WriteReport()
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Multiplayer collision matrix (MultiplayerSettings) " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        foreach (string line in s_log)
        {
            builder.AppendLine("  " + line);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, builder.ToString());
        s_log.Add("report " + ReportPath);
    }
}
