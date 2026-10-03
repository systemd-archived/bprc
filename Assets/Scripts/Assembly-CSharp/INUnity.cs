using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class INUnity
{
	public enum VersionType
	{
		Original = 0,
		ModeO = 1,
		ModeA = 2,
		ModeB = 3
	}

	private static Dictionary<(string, string), UnityEngine.Object> s_resources;

	public static bool Enabled => true;

	public static Version Version { get; private set; }

	public static string VersionText { get; private set; }

	public static int BuildVersion { get; private set; }

	public static string DataPath { get; private set; }

	public static string SettingsPath { get; private set; }

	public static SystemLanguage Language { get; private set; }

	/// <summary>
	/// Changes the language the interface is read in.
	/// </summary>
	/// <remarks>
	/// Only the language setting should call this, and only through <see cref="INLocalization.SetLanguage"/>:
	/// on its own it changes the engine language, which is what every localized label consults, but it
	/// reloads neither string table. The static constructor above decides the starting language from the
	/// system, which is what a settings file with no language in it falls back to.
	/// </remarks>
	public static void SetLanguage(SystemLanguage language)
	{
		Language = language;
	}

	public static Font ArialFont { get; private set; }

	public static Mesh QuadMesh { get; private set; }

	public static Shader ColorShader { get; private set; }

	public static Shader ColorTransparentShader { get; private set; }

	public static Shader CustomTransparentShader { get; private set; }

	static INUnity()
	{
		Version = Version.Parse(Application.version);
		VersionText = Application.version;
		string[] array = VersionText.Split('-', 2);
		Version = Version.Parse(array[0]);
		BuildVersion = -1;
		if (array.Length > 1)
		{
			BuildVersion = int.Parse(array[1]);
		}
		SystemLanguage systemLanguage = Application.systemLanguage;
		if (systemLanguage == SystemLanguage.Chinese || systemLanguage == SystemLanguage.ChineseSimplified || systemLanguage == SystemLanguage.ChineseTraditional)
		{
			Language = SystemLanguage.Chinese;
		}
		else
		{
			Language = SystemLanguage.English;
		}
		ArialFont = LoadBuiltinFont();
		QuadMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
		s_resources = new Dictionary<(string, string), UnityEngine.Object>();
	}

	/// <summary>
	/// The engine's built-in font.
	/// </summary>
	/// <remarks>
	/// Unity 2022.3 removed the built-in <c>Arial.ttf</c>: asking for it throws
	/// <see cref="ArgumentException"/> ("Arial.ttf is no longer a valid built in font. Please use
	/// LegacyRuntime.ttf"), and because this runs from a static constructor it surfaced as a
	/// <c>TypeInitializationException</c> that took the whole game down on the splash screen. The new
	/// name is tried first, the old one second, and a project font is the last resort so a future
	/// rename cannot break the boot path again.
	/// </remarks>
	private static Font LoadBuiltinFont()
	{
		Font font = TryBuiltinFont("LegacyRuntime.ttf");
		if (font != null)
		{
			return font;
		}
		font = TryBuiltinFont("Arial.ttf");
		if (font != null)
		{
			return font;
		}
		Debug.LogWarning("No built-in UI font is available; falling back to the bundled Liberation Sans.");
		return Resources.Load<Font>("Font/LiberationSans");
	}

	private static Font TryBuiltinFont(string name)
	{
		try
		{
			return Resources.GetBuiltinResource<Font>(name);
		}
		catch (Exception)
		{
			return null;
		}
	}

	public static void InitializeRoot()
	{
		DataPath = INFileSystem.Root;
		SettingsPath = Path.Combine(DataPath, "Settings");
	}

	public static void Initialize(ResourceData data)
	{
		InitializeResources("GameObject", data.Prefabs);
		InitializeResources("Font", data.Fonts);
		InitializeResources("Texture", data.Textures);
		InitializeResources("Shader", data.Shaders);
		InitializeResources("Material", data.Materials);
		InitializeResources("TextAsset", data.TextAssets);
		InitializeResources("ScriptableObject", data.ScriptableObjects);
		s_resources.Add(("Font", "DefaultFont-Regular"), LoadFont("FrutigerNeueLTW1G-Regular"));
		ColorShader = LoadShader("Unlit_Color");
		ColorTransparentShader = LoadShader("Unlit_ColorTransparent");
		CustomTransparentShader = LoadShader("PreAlpha_Unlit_ColorTransparent_Geometry");
	}

	private static void InitializeResources<T>(string typeName, List<T> data) where T : UnityEngine.Object
	{
		foreach (T datum in data)
		{
			s_resources.Add((typeName, GetName(datum)), datum);
		}
	}

	private static string GetName(UnityEngine.Object obj)
	{
		string name = obj.name;
		if (obj is Shader)
		{
			int num = name.LastIndexOf('/');
			if (num != -1)
			{
				return name.Substring(num + 1);
			}
			return name;
		}
		return name;
	}

	public static Font LoadFont(string name)
	{
		return (Font)LoadObject("Font", name);
	}

	public static GameObject LoadGameObject(string name)
	{
		return (GameObject)LoadObject("GameObject", name);
	}

	public static Texture LoadTexture(string name)
	{
		return (Texture)LoadObject("Texture", name);
	}

	public static Material LoadMaterial(string name)
	{
		return (Material)LoadObject("Material", name);
	}

	public static Shader LoadShader(string name)
	{
		return (Shader)LoadObject("Shader", name);
	}

	public static TextAsset LoadTextAsset(string name)
	{
		return (TextAsset)LoadObject("TextAsset", name);
	}

	public static T LoadScriptableObject<T>(string name) where T : ScriptableObject
	{
		return (T)LoadObject("ScriptableObject", name);
	}

	public static UnityEngine.Object LoadObject(string typeName, string name)
	{
		return s_resources[(typeName, name)];
	}
}
