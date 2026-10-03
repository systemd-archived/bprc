using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Editor-side scene diagnostics for this decompiled project: finds the objects Unity draws magenta.
/// </summary>
/// <remarks>
/// A magenta surface means the material's shader did not load - Unity substitutes
/// <c>Hidden/InternalErrorShader</c> - which in a decompilation usually means a shader this project has
/// not restored yet, or a material whose shader reference no longer resolves. Both renderers and UI
/// graphics are checked, because either kind can be the magenta thing on screen; run it while in play
/// mode on the scene in question and it names the objects instead of leaving it to guesswork.
/// </remarks>
public static class SceneDiagnostics
{
	/// <summary>Lists every renderer and UI graphic whose material has no working shader.</summary>
	public static string ReportMissingShaders()
	{
		List<string> broken = new List<string>();

		foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
		{
			string detail = Broken(renderer.sharedMaterials);
			if (detail != null)
			{
				broken.Add("Renderer " + Path(renderer.transform) + " (" + renderer.GetType().Name + ") -> " + detail);
			}
		}

		foreach (Graphic graphic in Object.FindObjectsByType<Graphic>(FindObjectsSortMode.None))
		{
			Material material = graphic.material;
			string detail = material == null ? "<no material>" : ShaderProblem(material);
			if (detail != null)
			{
				broken.Add("Graphic " + Path(graphic.transform) + " (" + graphic.GetType().Name + ") -> " + detail);
			}
		}

		StringBuilder report = new StringBuilder();
		report.AppendLine("objects drawn magenta: " + broken.Count);
		foreach (string line in broken)
		{
			report.AppendLine("  " + line);
		}
		return report.ToString();
	}

	/// <summary>
	/// Lists the Game view's own boolean toggles. The gizmo overlay is one of them, and while it is on
	/// Unity draws every AudioSource/Camera/Collider icon of the scene straight into the Game view, on
	/// top of the game - which looks like the game is drawing them, and is not.
	/// </summary>
	public static string ListGameViewToggles()
	{
		StringBuilder report = new StringBuilder();
		System.Type gameView = GameViewType();
		report.AppendLine("GameView type: " + (gameView == null ? "NOT FOUND" : gameView.FullName));
		if (gameView == null)
		{
			return report.ToString();
		}
		foreach (EditorWindow window in Resources.FindObjectsOfTypeAll(gameView))
		{
			report.AppendLine("window '" + window.titleContent.text + "'");
			foreach (System.Reflection.FieldInfo field in gameView.GetFields(
				System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic))
			{
				if (field.FieldType == typeof(bool))
				{
					report.AppendLine("  " + field.Name + " = " + field.GetValue(window));
				}
			}
		}
		return report.ToString();
	}

	/// <summary>Sets one of those toggles on every Game view, and repaints them.</summary>
	public static string SetGameViewToggle(string fieldName, bool value)
	{
		System.Type gameView = GameViewType();
		if (gameView == null)
		{
			return "GameView type not found";
		}
		System.Reflection.FieldInfo field = gameView.GetField(fieldName,
			System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
		if (field == null)
		{
			return "no such field: " + fieldName;
		}
		StringBuilder report = new StringBuilder();
		foreach (EditorWindow window in Resources.FindObjectsOfTypeAll(gameView))
		{
			field.SetValue(window, value);
			window.Repaint();
			report.AppendLine(window.titleContent.text + "." + fieldName + " = " + field.GetValue(window));
		}
		return report.ToString();
	}

	private static System.Type GameViewType()
	{
		foreach (System.Reflection.Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
		{
			System.Type type = assembly.GetType("UnityEditor.GameView");
			if (type != null)
			{
				return type;
			}
		}
		return null;
	}

	private static string Broken(Material[] materials)
	{
		StringBuilder detail = new StringBuilder();
		foreach (Material material in materials)
		{
			string problem = material == null ? "<no material>" : ShaderProblem(material);
			if (problem != null)
			{
				detail.Append(problem + " ");
			}
		}
		return detail.Length == 0 ? null : detail.ToString().Trim();
	}

	private static string ShaderProblem(Material material)
	{
		Shader shader = material.shader;
		if (shader == null)
		{
			return material.name + " (no shader)";
		}
		if (shader.name == "Hidden/InternalErrorShader")
		{
			return material.name + " (internal error shader)";
		}
		return null;
	}

	private static string Path(Transform node)
	{
		string path = node.name;
		Transform walk = node.parent;
		while (walk != null)
		{
			path = walk.name + "/" + path;
			walk = walk.parent;
		}
		return path;
	}
}
