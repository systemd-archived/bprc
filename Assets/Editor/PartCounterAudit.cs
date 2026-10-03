using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Checks that a setting changed by hand on the feature page actually takes effect where it is read.
/// </summary>
/// <remarks>
/// <para>
/// Written for the report that 部件计数器, switched on under 设置 - 更多设置 - 建造格, still did not count
/// parts. Every feature carries a scope - one mode, or both - and <c>INSettings.GetVariant</c> answers with
/// the version's default value outside that scope. The feature page is usually opened from the main menu, and
/// nothing is a sandbox there, so a feature the version declares as sandbox-only could be switched on and read
/// back as off: the control wrote a value that nothing ever read. The parts kept their per-level allowance and
/// the numbers on the buttons kept counting down as parts were used.
/// </para>
/// <para>
/// This runs for each of the three settings versions in turn, because each ships its own defaults and the
/// scope that broke this came from one of them, and each version gets its own settings directory so that a
/// file saved by one cannot decide the next one's scopes.
/// </para>
/// </remarks>
[InitializeOnLoad]
public static class PartCounterAudit
{
	private const string Flag = "PartCounterAudit";

	private const string ReportPath = "Builds/QA/part-counter.txt";

	/// <summary>The versions a player can pick at first run, as <c>INSettings.Initialize</c> numbers them.</summary>
	private static readonly int[] Versions = { 0, 1, 2 };

	private static readonly List<string> Errors = new List<string>();

	private static readonly StringBuilder Report = new StringBuilder();

	static PartCounterAudit()
	{
		EditorApplication.playModeStateChanged += StateChanged;
	}

	public static void Run()
	{
		EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
		SessionState.SetBool(Flag, true);

		// As with the other audits here: no -quit, because that ends the batch run before play mode, where the
		// settings boot - and the resource data they read - exist, has started.
		double hardStop = EditorApplication.timeSinceStartup + 240;
		EditorApplication.update += () =>
		{
			if (!EditorApplication.isPlaying && EditorApplication.timeSinceStartup >= hardStop)
			{
				Debug.LogWarning("Part counter audit never reached play mode; giving up.");
				SessionState.SetBool(Flag, false);
				EditorApplication.Exit(2);
			}
		};
		EditorApplication.EnterPlaymode();
	}

	private static void StateChanged(PlayModeStateChange state)
	{
		if (!SessionState.GetBool(Flag, false)) { return; }
		if (state != PlayModeStateChange.EnteredPlayMode) { return; }

		try
		{
			new GameObject("QA game services", typeof(INGameManager));
			INUnity.Initialize(AssetDatabase.LoadAssetAtPath<ResourceData>("Assets/MonoBehaviour/ResourceData.asset"));
			SetStatic(typeof(INUnity), "DataPath", Path.GetFullPath("Builds/QA/IsolatedSettings"));
			foreach (int version in Versions)
			{
				CheckVersion(version);
			}
			CheckThePartButtonNumber();
		}
		catch (Exception exception)
		{
			Errors.Add(exception.ToString());
		}

		Finish();
	}

	/// <summary>Switches the counter on and off the way the feature page does, in one settings version.</summary>
	private static void CheckVersion(int version)
	{
		string directory = Path.GetFullPath("Builds/QA/IsolatedSettings/v" + version);
		Directory.CreateDirectory(directory);
		SetStatic(typeof(INUnity), "SettingsPath", directory);

		// Start from the version's own defaults every run: a file left behind by an earlier run would carry the
		// widened scopes this audit itself wrote, and the check would pass without proving anything.
		string saved = Path.Combine(directory, "INFeatureSettings.json");
		if (File.Exists(saved)) { File.Delete(saved); }

		INSettings.Initialize(version);

		Report.AppendLine("=== settings version " + version + " ===");
		Report.AppendLine("  部件计数器: scope " + ScopeOf(INFeature.PartCounter) +
			", applies where it is read " + INSettings.IsEnabled(INFeature.PartCounter) +
			", reads " + INSettings.GetBool(INFeature.PartCounter));

		// This is what the switch on the feature page calls when it is flipped.
		INSettings.SetSettingsValue("PartCounter", true);
		bool on = INSettings.GetBool(INFeature.PartCounter);
		Report.AppendLine("  after switching it on: scope " + ScopeOf(INFeature.PartCounter) + ", reads " + on);
		if (!on)
		{
			Errors.Add("version " + version + ": 部件计数器 switched on still reads off" +
				" (scope " + ScopeOf(INFeature.PartCounter) + ")");
		}

		INSettings.SetSettingsValue("PartCounter", false);
		bool off = INSettings.GetBool(INFeature.PartCounter);
		if (off)
		{
			Errors.Add("version " + version + ": 部件计数器 switched off still reads on");
		}

		// 建造格大小 sits in the same group and is scoped the same way in the same version.
		INSettings.SetSettingsValue("GridSize", 5);
		int size = INSettings.GetInt(INFeature.GridSize);
		Report.AppendLine("  建造格大小: set to 5, reads " + size + ", scope " + ScopeOf(INFeature.GridSize));
		if (size != 5)
		{
			Errors.Add("version " + version + ": 建造格大小 set to 5 reads " + size);
		}

		// Back on, so that the number checked below is the one a player who switched it on would see.
		INSettings.SetSettingsValue("PartCounter", true);
	}

	/// <summary>
	/// The number the part buttons show, which is the whole point of the feature: with it on, how many of that
	/// part the contraption uses; with it off, how many the level still allows.
	/// </summary>
	private static void CheckThePartButtonNumber()
	{
		Report.AppendLine();
		Report.AppendLine("=== what a part button shows ===");
		ConstructionUI.PartDesc desc = new ConstructionUI.PartDesc();
		desc.useCount = 3;
		desc.maxCount = 10;

		INSettings.SetSettingsValue("PartCounter", true);
		int used = desc.CurrentCount;
		INSettings.SetSettingsValue("PartCounter", false);
		int left = desc.CurrentCount;
		INSettings.SetSettingsValue("PartCounter", true);

		Report.AppendLine("  three of ten placed, counter on: " + used + ", counter off: " + left);
		if (used != 3)
		{
			Errors.Add("with the counter on, the button shows " + used + " instead of the 3 used");
		}
		if (left != 7)
		{
			Errors.Add("with the counter off, the button shows " + left + " instead of the 7 left");
		}
	}

	/// <summary>The scope a feature is filed under, which decides whether its value is read at all.</summary>
	private static string ScopeOf(INFeature feature)
	{
		MethodInfo method = typeof(INSettings).GetMethod("GetScope", BindingFlags.NonPublic | BindingFlags.Static);
		object scope = method.Invoke(null, new object[] { feature });
		return scope == null ? "<none>" : scope.ToString();
	}

	private static void SetStatic(Type type, string property, object value)
	{
		type.GetProperty(property, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
			.SetValue(null, value);
	}

	private static void Finish()
	{
		Report.AppendLine();
		Report.AppendLine("Errors: " + Errors.Count);
		foreach (string message in Errors) { Report.AppendLine(message); }
		Directory.CreateDirectory("Builds/QA");
		File.WriteAllText(ReportPath, Report.ToString());
		Debug.Log("[PartCounterAudit] " + (Errors.Count == 0 ? "PASS" : "FAIL") + " - see " + ReportPath);
		SessionState.SetBool(Flag, false);
		EditorApplication.ExitPlaymode();
		EditorApplication.Exit(Errors.Count == 0 ? 0 : 1);
	}
}
