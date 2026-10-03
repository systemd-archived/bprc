using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-side tools for the bundled In-game Debug Console port, kept because this project's UI
/// assemblies are the reason the console needed repairing at all.
/// </summary>
/// <remarks>
/// This decompiled project historically resolved uGUI and TextMeshPro from precompiled DLLs
/// (Assets/Plugins/UnityEngine.UI.dll, Assets/Plugins/Unity.TextMeshPro.dll) rather than from UPM
/// packages, so their MonoScripts lived inside the DLL assets and carried an identity of
/// "fileID != 11500000, guid = dll guid". The asset-store prefabs reference the same components in
/// source form ("fileID: 11500000, guid = uGUI/TMP source guid"), which at the time resolved to
/// nothing here: every Image, Button, ScrollRect, TMP text and layout group in them deserialized as a
/// missing script, and <c>DebugLogManager.Awake</c> aborted before it could initialize the console, so
/// the popup and the log list threw NullReferenceException. Both prefabs were relinked by rewriting
/// those <c>m_Script</c> references; <see cref="VerifyPrefab"/> is how that is checked, and
/// <see cref="DumpDllScripts"/> is where the replacement identities come from.
/// See Tools/IngameDebugConsole/DEBUG_CONSOLE_NOTES.md.
/// <para>
/// Since 2026-09-28 uGUI is provided by the built-in <c>com.unity.ugui</c> package and every uGUI
/// reference in the project was migrated back to source form (the <c>UnityEngine.UI.dll</c> entry in
/// <see cref="Dlls"/> is therefore a no-op); only the TextMeshPro half is still DLL-backed.
/// </para>
/// </remarks>
public static class DebugConsoleRelink
{
	private static readonly string[] Dlls =
	{
		// Legacy no-op since 2026-09-28: uGUI is provided by the com.unity.ugui package and the
		// project's uGUI references are in source form again (LoadAllAssetsAtPath returns nothing
		// for a path that no longer exists).
		"Assets/Plugins/UnityEngine.UI.dll",
		"Assets/Plugins/Unity.TextMeshPro.dll",
	};

	/// <summary>Lists "typeName guid fileID" for every MonoScript inside the UI/TMP dlls.</summary>
	public static string DumpDllScripts()
	{
		StringBuilder report = new StringBuilder();
		foreach (string dll in Dlls)
		{
			Object[] assets = AssetDatabase.LoadAllAssetsAtPath(dll);
			report.AppendLine("=== " + dll + " (" + assets.Length + " assets) ===");
			foreach (Object asset in assets)
			{
				MonoScript script = asset as MonoScript;
				if (script == null)
				{
					continue;
				}
				string guid;
				long localId;
				AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out guid, out localId);
				System.Type type = script.GetClass();
				report.AppendLine((type == null ? "<no class>" : type.Name) + " " + guid + " " + localId);
			}
		}
		return report.ToString();
	}

	/// <summary>
	/// Lists every component slot in a prefab that failed to resolve, by hierarchy path. A console
	/// prefab that reports anything but zero here will throw at runtime.
	/// </summary>
	public static string VerifyPrefab(string path)
	{
		GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
		if (root == null)
		{
			return "prefab not found: " + path;
		}
		List<string> missing = new List<string>();
		int total = 0;
		Walk(root.transform, "", missing, ref total);
		StringBuilder report = new StringBuilder();
		report.AppendLine(path + ": " + total + " component slots, " + missing.Count + " missing");
		foreach (string line in missing)
		{
			report.AppendLine("  MISSING " + line);
		}
		return report.ToString();
	}

	/// <summary>
	/// Queues a game-view capture. The console lives on a screen-space-overlay canvas, which no camera
	/// renders, so <c>Camera.Render</c>-based capture tools cannot see it - only the game view can.
	/// </summary>
	public static string CaptureGameView(string path)
	{
		ScreenCapture.CaptureScreenshot(path);
		return "scheduled " + path;
	}

	private static void Walk(Transform node, string path, List<string> missing, ref int total)
	{
		string here = path + "/" + node.name;
		Component[] components = node.GetComponents<Component>();
		for (int index = 0; index < components.Length; index++)
		{
			total++;
			if (components[index] == null)
			{
				missing.Add(here + " slot #" + index);
			}
		}
		for (int index = 0; index < node.childCount; index++)
		{
			Walk(node.GetChild(index), here, missing, ref total);
		}
	}
}
