using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Checks the language switch: the row in 通用设置 that carries it, and the quick menu it has to reach.
/// </summary>
/// <remarks>
/// <para>
/// Written for the request that the quick menu under InGameBuildMenu/ToolBookButton be localized and that
/// 通用设置 gain an English/Chinese row. Both halves are properties of the interface as it is built at
/// runtime, so they have to be read while playing: the settings rows are generated from the settings
/// classes, and the quick menu builds every one of its buttons and labels itself.
/// </para>
/// <para>
/// What is checked, in order: the row exists, is a Heat UI horizontal selector, and names its two
/// languages each in its own language; switching it moves the engine language and re-reads the panel's
/// labels - including that nothing is left showing its own locale id, which is what a missing entry looks
/// like; the quick menu's labels follow, in both languages, without the menu being rebuilt; the labels fit
/// the buttons they sit in; and the object names the handlers use do not move with the language.
/// </para>
/// </remarks>
[InitializeOnLoad]
public static class ToolBookLocalizationAudit
{
	private const string Flag = "ToolBookLocalizationAudit";

	private const string ReportPath = "Builds/QA/toolbook-localization.txt";

	private static readonly List<string> Errors = new List<string>();

	private static readonly StringBuilder Report = new StringBuilder();

	private static INAppInterface s_app;

	private static GameObject s_panel;

	private static GameObject s_menu;

	private static Michsky.UI.Heat.HorizontalSelector s_row;

	private static UITextLocale s_rowLabel;

	/// <summary>How wide the toolbar's buttons came out in each language.</summary>
	private static float s_englishWidth;

	private static float s_chineseWidth;

	private static int s_step;

	private static double s_next;

	private static double s_deadline;

	/// <summary>Every id the quick menu reads, including the ones built with a format argument.</summary>
	private static readonly string[] MenuIds =
	{
		"ToolBook_Copy", "ToolBook_Paste", "ToolBook_Select", "ToolBook_Undo", "ToolBook_Stash",
		"ToolBook_Cut", "ToolBook_Shape", "ToolBook_Fill", "ToolBook_Save",
		"ToolBook_Shape_Square", "ToolBook_Shape_Rectangle", "ToolBook_Shape_Circle",
		"ToolBook_Shape_Ellipse", "ToolBook_Shape_Line",
		"ToolBook_Delete", "ToolBook_DeleteSelection", "ToolBook_Cancel",
		"ToolBook_Overwrite", "ToolBook_Overwrite_None", "ToolBook_Overwrite_All",
		"ToolBook_RotateFormat", "ToolBook_Material", "ToolBook_Material_Wood",
		"ToolBook_Material_Metal", "ToolBook_Material_Custom2", "ToolBook_Material_Custom3",
		"ToolBook_Solid", "ToolBook_Hollow",
		"ToolBook_NameSlot", "ToolBook_NamePlaceholder", "ToolBook_SlotFormat"
	};

	/// <summary>The quick menu's buttons whose label says something about the current state.</summary>
	private static readonly string[] StateButtons = { "Delete", "Overwrite", "Rotate", "Material", "ShapeType", "ShapeSolid" };

	/// <summary>The Chinese labels, as the ids above read them. The English ones are the mirror image.</summary>
	private static readonly string[] ChineseLabels =
	{
		"复制", "粘贴", "选区", "撤销", "暂存", "剪切", "形状", "填充", "存档"
	};

	static ToolBookLocalizationAudit()
	{
		EditorApplication.playModeStateChanged += StateChanged;
	}

	public static void Run()
	{
		HeatUiPanelRepair.Run();
		EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
		SessionState.SetBool(Flag, true);

		// As with the other audits here: no -quit, because that ends the batch run before play mode, where
		// everything this checks is built, has started.
		double hardStop = EditorApplication.timeSinceStartup + 240;
		EditorApplication.update += () =>
		{
			if (!EditorApplication.isPlaying && EditorApplication.timeSinceStartup >= hardStop)
			{
				Debug.LogWarning("ToolBook localization audit never reached play mode; giving up.");
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

		s_deadline = EditorApplication.timeSinceStartup + 180;
		s_next = EditorApplication.timeSinceStartup + 2;
		s_step = 0;
		EditorApplication.update += Tick;
		try
		{
			new GameObject("QA game services", typeof(INGameManager));
			INUnity.Initialize(AssetDatabase.LoadAssetAtPath<ResourceData>("Assets/MonoBehaviour/ResourceData.asset"));
			SetStatic(typeof(INUnity), "SettingsPath", Path.GetFullPath("Builds/QA/IsolatedSettings"));
			SetStatic(typeof(INUnity), "DataPath", Path.GetFullPath("Builds/QA/IsolatedSettings"));
			Directory.CreateDirectory(INUnity.SettingsPath);
			typeof(UserSettings).GetField("m_data", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null,
				new SettingsData(Path.Combine(INUnity.SettingsPath, "Settings.xml"), false, string.Empty));
			typeof(INSettings).GetMethod("Load", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { 2 });
			INLocalization.Create();
			SetStatic(typeof(INUserSettings), "Default", new INUserSettings(INUnity.Version));
			SetStatic(typeof(INUserSettings), "Instance", new INUserSettings(INUnity.Version));
			new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
			s_app = UnityEngine.Object.Instantiate(
				AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/INApplicationInterface.prefab")).GetComponent<INAppInterface>();
			s_panel = (GameObject)typeof(INAppInterface).GetField("m_panel", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(s_app);
		}
		catch (Exception exception) { Finish(exception.ToString()); }
	}

	private static void Tick()
	{
		if (EditorApplication.timeSinceStartup >= s_deadline) { Finish("timed out at step " + s_step); return; }
		if (EditorApplication.timeSinceStartup < s_next) { return; }
		try { Step(); }
		catch (Exception exception) { Finish(exception.ToString()); }
	}

	private static void Step()
	{
		switch (s_step++)
		{
			case 0:
				Boot();
				break;
			case 1:
				CheckTheRow();
				BuildTheMenu();
				break;
			case 2:
				CheckTheMenu("English");
				break;
			case 3:
				Switch(s_row, InterfaceLanguage.Chinese);
				break;
			case 4:
				CheckTheMenu("Chinese");
				break;
			case 5:
				Switch(s_row, InterfaceLanguage.English);
				break;
			case 6:
				CheckTheSwitchBack();
				break;
			case 7:
				Switch(s_row, InterfaceLanguage.FollowSystem);
				break;
			case 8:
				CheckFollowingTheSystem();
				Finish(null);
				break;
		}
	}

	/// <summary>Shows the panel with its first category selected, which is where 通用设置 is.</summary>
	private static void Boot()
	{
		s_panel.SetActive(true);
		Toggle first = s_panel.GetComponentsInChildren<AppInterfaceSidebarItem>(true)
			.Select(item => item.GetComponent<Toggle>()).FirstOrDefault(toggle => toggle != null);
		if (first == null) { throw new Exception("the sidebar has no categories"); }
		first.isOn = true;
		Canvas.ForceUpdateCanvases();
		Report.AppendLine("panel shown with the first category selected: " + INLocalization.Instance.GetText("GeneralSettings_Name"));

		// The pages build their rows in their own Start, so give them a beat before reading anything.
		Wait(2.0);
	}

	/// <summary>The 通用设置 row itself: that it exists, what it offers, and what the panel leaks.</summary>
	private static void CheckTheRow()
	{
		INSettingsInterface page = s_panel.GetComponentInChildren<INSettingsInterface>(true);
		if (page == null) { throw new Exception("the app interface has no INSettingsInterface page"); }

		UITextLocale[] labels = page.GetComponentsInChildren<UITextLocale>(true);
		s_rowLabel = labels.FirstOrDefault(label => label.ID == "GeneralSettings_Language");
		if (s_rowLabel == null)
		{
			Errors.Add("通用设置 has no GeneralSettings_Language row; the rows it does have are: " +
				string.Join(", ", labels.Select(label => label.ID).Distinct().Take(30).ToArray()));
			return;
		}
		s_row = FindSelector(s_rowLabel.transform);
		if (s_row == null) { Errors.Add("the language row has no Heat UI horizontal selector"); return; }

		Report.AppendLine("row label under 通用设置: " + Shown(s_rowLabel.gameObject) +
			", control: Michsky.UI.Heat.HorizontalSelector with " + s_row.items.Count + " choices");
		Report.AppendLine("choices: " + string.Join(" / ", s_row.items.Select(item => item.itemTitle).ToArray()));
		Report.AppendLine("setting held by the row: " + INUserSettings.Instance.GeneralSettings.Language +
			" (index " + s_row.index + "), engine language: " + INUnity.Language);
		if (s_row.items.Count != 3) { Errors.Add("the language selector does not offer exactly three choices"); }
		if (INUserSettings.Instance.GeneralSettings.Language != InterfaceLanguage.FollowSystem)
		{
			Errors.Add("a settings file with no language in it did not come up following the system");
		}
		if (s_row.items[1].itemTitle != "English" || s_row.items[2].itemTitle != "中文")
		{
			Errors.Add("the two named languages are not each named in their own language: " +
				string.Join(" / ", s_row.items.Select(item => item.itemTitle).ToArray()));
		}

		// A label showing its own id is what a missing entry in the table looks like. Labels with no id at
		// all are the templates the page keeps for rows it has not built, and show nothing either way.
		Report.AppendLine("labels on the page: " + labels.Length + ", showing their own id (" +
			"which is what a missing entry looks like): " + Leaks(labels).Length);
		foreach (string id in Leaks(labels)) { Errors.Add("the table has no text for " + id); }

		string[] missing = MenuIds.Where(id => INLocalization.Instance.GetText(id) == id).ToArray();
		Report.AppendLine("quick menu ids with no text in the table: " + missing.Length);
		foreach (string id in missing) { Errors.Add("the table has no quick menu text for " + id); }

		// Everything the page shows now, so that the switch below can be measured against it rather than
		// only spot-checked: a row that kept the language it was built in is exactly the defect here.
		Dictionary<UITextLocale, string> before = labels.ToDictionary(label => label, label => Shown(label.gameObject));
		Component title = (Component)typeof(INAppInterface)
			.GetField("m_heatTitle", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(s_app);
		string titleBefore = title != null ? Shown(title.gameObject) : null;
		Report.AppendLine("title as the panel opened: " + (titleBefore ?? "(none)"));

		// Start from English, so that the switch under test is the one the row is driven to.
		Switch(s_row, InterfaceLanguage.English);

		int reRead = before.Count(pair => Shown(pair.Key.gameObject) != pair.Value);
		Report.AppendLine("page labels re-read for English: " + reRead + " of " + before.Count +
			", showing their own id now: " + Leaks(labels).Length);
		Report.AppendLine("title now: " + (title != null ? Shown(title.gameObject) : "(none)"));
		if (reRead < before.Count / 2)
		{
			Errors.Add("only " + reRead + " of the page's " + before.Count +
				" labels were re-read when the language changed");
		}
		foreach (string id in Leaks(labels)) { Errors.Add("after the switch the table has no text for " + id); }
		if (titleBefore != null && Shown(title.gameObject) == titleBefore)
		{
			Errors.Add("the title kept the language it was composed in: " + titleBefore);
		}
	}

	/// <summary>The labels that are showing their own id, which is what a missing entry looks like.</summary>
	private static string[] Leaks(IEnumerable<UITextLocale> labels)
	{
		return labels
			.Where(label => !string.IsNullOrEmpty(label.ID) && Shown(label.gameObject) == label.ID)
			.Select(label => label.ID + " (" + HierarchyPath(label.transform) + ")").Distinct().ToArray();
	}

	/// <summary>Builds the quick menu the way the build menu does, and opens it.</summary>
	private static void BuildTheMenu()
	{
		INUserSettings.Instance.StarSeaSettings.ToolBookEnabled = true;
		s_menu = new GameObject("ToolBookButton");
		s_menu.AddComponent<ToolBookButton>();
		ToolBookButton button = s_menu.GetComponent<ToolBookButton>();
		button.Toggle();
		Report.AppendLine();
		Report.AppendLine("quick menu built: " + s_menu.GetComponentsInChildren<Text>(true).Length +
			" labels, opened with 通用设置 on " + INUserSettings.Instance.GeneralSettings.Language);
		Wait(0.8);
	}

	/// <summary>
	/// Reads every label the quick menu shows, in the language that is on, and checks it against the row's
	/// own language.
	/// </summary>
	private static void CheckTheMenu(string expected)
	{
		Canvas.ForceUpdateCanvases();
		List<Text> labels = s_menu.GetComponentsInChildren<Text>(true)
			.Where(label => label.text.Length > 0 && label.text != "-").ToList();
		Report.AppendLine();
		Report.AppendLine("--- quick menu in " + expected + " (" + labels.Count + " labels)");
		foreach (Text label in labels.OrderBy(label => label.transform.position.y * -1f).ThenBy(label => label.transform.position.x))
		{
			RectTransform rect = label.rectTransform;
			GameObject owner = label.transform.parent != null ? label.transform.parent.gameObject : label.gameObject;
			Report.AppendLine(string.Format("  {0,-14} {1,-14} in a {2,5:F0}x{3,-3:F0} button, needs {4,5:F0}",
				owner.name, label.text, rect.rect.width, rect.rect.height, label.preferredWidth));
			if (label.preferredWidth > rect.rect.width + 1f)
			{
				Errors.Add("the label " + label.text + " needs " + label.preferredWidth.ToString("F0") +
					" but its " + owner.name + " button is " + rect.rect.width.ToString("F0") + " wide");
			}
			if (label.text.StartsWith("ToolBook_")) { Errors.Add("a quick menu label is showing its id: " + label.text); }

			// The six buttons that describe the current state are re-derived rather than fixed, so they are
			// the ones that could be left in the language the menu was in a frame ago.
			if (StateButtons.Contains(owner.name) && HasChinese(label.text) != (expected == "Chinese"))
			{
				Errors.Add("the " + owner.name + " button reads " + label.text + " in " + expected);
			}
		}

		string[] shown = labels.Select(label => label.text).ToArray();
		string[] wanted = expected == "Chinese" ? ChineseLabels : new[] { "Copy", "Paste", "Select", "Undo", "Stash", "Cut", "Shape", "Fill", "Save" };
		string[] missingLabels = wanted.Where(text => !shown.Contains(text)).ToArray();
		if (missingLabels.Length > 0)
		{
			Errors.Add("the toolbar in " + expected + " does not show: " + string.Join(", ", missingLabels));
		}

		// The toolbar's objects are named after the ids, so a language switch must not touch them.
		string[] names = s_menu.GetComponentsInChildren<Text>(true)
			.Select(label => label.transform.parent.gameObject.name).Distinct().OrderBy(name => name).ToArray();
		Report.AppendLine("  button names: " + string.Join(" ", names));
		if (names.Any(name => name.Contains("复制") || name.Contains("粘贴")))
		{
			Errors.Add("the quick menu names its objects in the interface language");
		}

		// The buttons have to follow the language under the open menu, not only when it is opened again:
		// an English label is longer, so the English buttons have to be the wider ones.
		Text copy = labels.FirstOrDefault(label => label.transform.parent.name == "Copy");
		if (copy != null)
		{
			float width = copy.rectTransform.rect.width;
			if (expected == "Chinese") { s_chineseWidth = width; }
			else { s_englishWidth = width; }
			if (s_chineseWidth > 0f && s_englishWidth > 0f && s_englishWidth <= s_chineseWidth)
			{
				Errors.Add("the buttons did not widen for English: " + s_englishWidth.ToString("F0") +
					" wide against " + s_chineseWidth.ToString("F0") + " for Chinese");
			}
		}

		Capture("Builds/QA/toolbook-localization-" + (expected == "Chinese" ? "zh" : "en") + ".png");
		Wait(0.4);
	}

	/// <summary>Switching back has to put the labels back, through the same live menu.</summary>
	private static void CheckTheSwitchBack()
	{
		CheckTheMenu("English");
		Report.AppendLine();
		Report.AppendLine("engine language at the end: " + INUnity.Language +
			", 通用设置 row: " + Shown(s_rowLabel.gameObject));
	}

	/// <summary>
	/// Handing the language back to the system: the choice has to be the default, name itself in the language
	/// that is on, and put the interface back in the language the system asks for.
	/// </summary>
	private static void CheckFollowingTheSystem()
	{
		InterfaceLanguage system = InterfaceLanguages.FromSystem();
		if (INUserSettings.Instance.GeneralSettings.Language != InterfaceLanguage.FollowSystem)
		{
			Errors.Add("choosing to follow the system did not leave the setting at FollowSystem");
		}
		Report.AppendLine();
		Report.AppendLine("following the system: system language " + Application.systemLanguage +
			" reads as " + system + ", engine language " + INUnity.Language +
			", first choice " + s_row.items[0].itemTitle);
		if (s_row.items[0].itemTitle != INLocalization.Instance.GetText("InterfaceLanguage_FollowSystem"))
		{
			Errors.Add("the choice that follows the system is not named in the language that is on: " +
				s_row.items[0].itemTitle);
		}
		CheckTheMenu(system == InterfaceLanguage.Chinese ? "Chinese" : "English");
	}

	/// <summary>Drives the row the way a player does: pick the choice, which applies the setting.</summary>
	private static void Switch(Michsky.UI.Heat.HorizontalSelector selector, InterfaceLanguage language)
	{
		int index = (int)language;
		selector.index = index;
		selector.defaultIndex = index;
		selector.UpdateUI();
		selector.onValueChanged.Invoke(index);
		Canvas.ForceUpdateCanvases();

		InterfaceLanguage held = INUserSettings.Instance.GeneralSettings.Language;
		Report.AppendLine();
		Report.AppendLine("chose " + InterfaceLanguages.Title(language) + ": setting " + held +
			", engine language " + INUnity.Language + ", row label " + Shown(s_rowLabel.gameObject));
		if (held != language) { Errors.Add("choosing " + language + " left the setting at " + held); }
		if (INUnity.Language != InterfaceLanguages.ToSystemLanguage(language))
		{
			Errors.Add("choosing " + language + " left the engine language at " + INUnity.Language);
		}
		string rowText = Shown(s_rowLabel.gameObject);
		string wanted = INLocalization.Instance.GetText("GeneralSettings_Language");
		if (rowText != wanted) { Errors.Add("the row label reads " + rowText + " but the table has " + wanted); }
		Wait(0.4);
	}

	private static Michsky.UI.Heat.HorizontalSelector FindSelector(Transform label)
	{
		Transform walk = label;
		while (walk != null)
		{
			Michsky.UI.Heat.HorizontalSelector selector = walk.GetComponentInChildren<Michsky.UI.Heat.HorizontalSelector>(true);
			if (selector != null) { return selector; }
			walk = walk.parent;
		}
		return null;
	}

	/// <summary>What a label is showing: Heat UI's labels are TextMeshPro, the quick menu's are not.</summary>
	private static string Shown(GameObject label)
	{
		TextMeshProUGUI tmp = label.GetComponent<TextMeshProUGUI>();
		if (tmp != null) { return tmp.text; }
		Text text = label.GetComponent<Text>();
		return text != null ? text.text : string.Empty;
	}

	/// <summary>Whether a label is written in Chinese, which is how one left in the other language reads.</summary>
	private static bool HasChinese(string text)
	{
		foreach (char character in text)
		{
			if (character >= '\u4e00' && character <= '\u9fff') { return true; }
		}
		return false;
	}

	/// <summary>Where a label sits, so that a report can name it rather than only its id.</summary>
	private static string HierarchyPath(Transform transform)
	{
		string path = transform.name;
		for (Transform walk = transform.parent; walk != null; walk = walk.parent)
		{
			path = walk.name + "/" + path;
		}
		return path;
	}

	private static void Wait(double seconds)
	{
		s_next = EditorApplication.timeSinceStartup + seconds;
	}

	private static void SetStatic(Type type, string property, object value)
	{
		type.GetProperty(property, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
			.SetValue(null, value);
	}

	/// <summary>Renders the panel and the quick menu's own canvas into one picture.</summary>
	private static void Capture(string path)
	{
		Camera camera = new GameObject("QA camera", typeof(Camera)).GetComponent<Camera>();
		camera.clearFlags = CameraClearFlags.SolidColor;
		camera.backgroundColor = new Color(0.12f, 0.15f, 0.2f);
		RenderTexture target = new RenderTexture(1920, 1080, 24);
		camera.targetTexture = target;

		Canvas[] canvases = UnityEngine.Object.FindObjectsOfType<Canvas>(true)
			.Where(canvas => canvas.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
		foreach (Canvas canvas in canvases)
		{
			canvas.renderMode = RenderMode.ScreenSpaceCamera;
			canvas.worldCamera = camera;
			canvas.planeDistance = 1;
		}
		Canvas.ForceUpdateCanvases();
		camera.Render();

		RenderTexture previous = RenderTexture.active;
		RenderTexture.active = target;
		Texture2D image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
		image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
		image.Apply();
		Directory.CreateDirectory("Builds/QA");
		File.WriteAllBytes(path, image.EncodeToPNG());
		Report.AppendLine("  captured " + path);
		RenderTexture.active = previous;
		foreach (Canvas canvas in canvases)
		{
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvas.worldCamera = null;
		}
		camera.targetTexture = null;
		UnityEngine.Object.Destroy(image);
		UnityEngine.Object.Destroy(target);
		UnityEngine.Object.Destroy(camera.gameObject);
	}

	private static void Finish(string error)
	{
		EditorApplication.update -= Tick;
		if (error != null) { Errors.Add(error); }
		Report.AppendLine();
		Report.AppendLine("Errors: " + Errors.Count);
		foreach (string message in Errors) { Report.AppendLine(message); }
		Directory.CreateDirectory("Builds/QA");
		File.WriteAllText(ReportPath, Report.ToString());
		SessionState.SetInt(Flag + "Exit", Errors.Count == 0 ? 0 : 1);
		EditorApplication.ExitPlaymode();
	}
}
