using IngameDebugConsole;
using UnityEngine;

/// <summary>
/// Owns the bundled In-game Debug Console (yasirkula's UnityIngameDebugConsole, v1.9.0).
/// </summary>
/// <remarks>
/// The asset ships as a prefab carrying <c>DebugLogManager</c>; the usual integration is to drop that
/// prefab into the scene, which is exactly what this does at boot instead - one
/// <see cref="RuntimeInitializeOnLoadMethod"/> and no scene edit - so the console is already
/// subscribed to <c>Application.logMessageReceived</c> when the first scripts wake up and the log
/// really is "everything since launch".
///
/// 通用设置 carries both ways in: the 显示debug悬浮窗 switch decides whether the console's floating
/// popup window is on screen (<see cref="PopupEnabled"/>, off by default -
/// <c>GeneralSettings.ShowDebugPopup</c>), and the 打开debug工具 row below it opens the log window there
/// and then (<c>INSettingsInterface.GenerateGeneralDebugRows</c>). The console's own close button puts
/// the window away again.
///
/// The prefab staged under Assets/Resources is not a byte copy of the asset's own: this project used to
/// ship uGUI and TextMeshPro as DLLs (Assets/Plugins/UnityEngine.UI.dll, Unity.TextMeshPro.dll), so
/// every component the original prefabs reference in UPM source form had to be relinked to the
/// DLL-backed MonoScripts (uGUI has since moved back to com.unity.ugui source references - see
/// CLAUDE.md), and the console's inactive EventSystem fallback was dropped. Re-copying the asset's
/// prefab over the staged one silently breaks the console again - see
/// Tools/IngameDebugConsole/DEBUG_CONSOLE_NOTES.md.
/// </remarks>
public static class INDebugConsole
{
	/// <summary>The prefab staged under Assets/Resources so a build can load it, not just the editor.</summary>
	private const string ResourcePath = "INDebugConsole";

	private static GameObject s_root;

	/// <summary>The live state behind <see cref="PopupEnabled"/>.</summary>
	private static bool s_popupEnabled;

	/// <summary>Whether the console has been created (and therefore has been logging since boot).</summary>
	public static bool Exists
	{
		get { return DebugLogManager.Instance != null; }
	}

	/// <summary>Whether the log window is on screen right now.</summary>
	public static bool IsOpen
	{
		get
		{
			DebugLogManager manager = DebugLogManager.Instance;
			return manager != null && manager.IsLogWindowVisible;
		}
	}

	/// <summary>
	/// Whether the console's floating popup window is allowed on screen.
	/// </summary>
	/// <remarks>
	/// Off unless 通用设置's 显示debug悬浮窗 switch turns it on - the switch owns the value and this is
	/// where it lands (<c>INDebugConsole</c> cannot be referenced from the settings class it is a row
	/// of, so the flag lives here and the row writes it). The window itself is the asset's own floating
	/// button, which counts new log entries and opens the log window when clicked.
	/// </remarks>
	public static bool PopupEnabled
	{
		get { return s_popupEnabled; }
		set
		{
			s_popupEnabled = value;
			ApplyPopup();
		}
	}

	/// <summary>Puts <see cref="PopupEnabled"/> onto the live console, if there is one yet.</summary>
	private static void ApplyPopup()
	{
		DebugLogManager manager = DebugLogManager.Instance;
		if (manager != null && manager.PopupEnabled != s_popupEnabled)
		{
			manager.PopupEnabled = s_popupEnabled;
		}
	}

	/// <summary>
	/// Creates the console if it does not exist yet. Safe to call any number of times, and never
	/// throws: it runs from <see cref="Bootstrap"/>, where an exception would take the boot sequence
	/// down with it.
	/// </summary>
	public static void Ensure()
	{
		if (DebugLogManager.Instance != null)
		{
			if (s_root != null)
			{
				s_root.SetActive(true);
			}
			return;
		}
		// Nothing live to talk to. A console can be left in that state without its GameObject being
		// gone: Unity does not re-run RuntimeInitializeOnLoadMethod for a script reload during play, so
		// the console from before the reload keeps existing while DebugLogManager.Instance (a static)
		// is reset, and its manager has lost every non-serialized field to that reload. Such a
		// leftover cannot be revived, so it is cleared out and a fresh console is built in its place -
		// otherwise the 通用设置 rows would quietly stop working after a hot reload.
		foreach (DebugLogManager stale in Object.FindObjectsByType<DebugLogManager>(FindObjectsSortMode.None))
		{
			Object.Destroy(stale.gameObject);
		}
		s_root = null;
		GameObject prefab = Resources.Load<GameObject>(ResourcePath);
		if (prefab == null)
		{
			Debug.LogWarning("[INDebugConsole] Resources/" + ResourcePath + " is missing; the debug console cannot start.");
			return;
		}
		s_root = Object.Instantiate(prefab);
		s_root.name = "INDebugConsole";
		// The manager asserts its own idea of the floating window's visibility in its Start, which runs
		// after this method has already prepared the console, so the flag is applied here and kept
		// applied from then on (see INDebugConsolePopupKeeper).
		if (s_root.AddComponent<INDebugConsolePopupKeeper>() == null)
		{
			Debug.LogWarning("[INDebugConsole] the popup keeper could not be attached; the floating window will then follow the asset's own preference.");
		}
		ApplyPopup();
		// PopupEnabled is a live property and Awake has already run, so this hides the asset's own
		// log window: the settings screen is this build's way in.
		DebugLogManager manager = DebugLogManager.Instance;
		if (manager != null)
		{
			try
			{
				manager.HideLogWindow();
			}
			catch (System.Exception exception)
			{
				// Should not happen with the staged prefab, but this runs on the boot path, where a
				// broken console must not be allowed to take the rest of the game down with it.
				Debug.LogWarning("[INDebugConsole] HideLogWindow failed: " + exception.Message);
			}
		}
	}

	/// <summary>Brings the log window up, creating the console first if this is the first time.</summary>
	public static void Show()
	{
		Ensure();
		DebugLogManager manager = DebugLogManager.Instance;
		if (manager != null)
		{
			manager.ShowLogWindow();
		}
	}

	/// <summary>Puts the log window away again.</summary>
	public static void Hide()
	{
		DebugLogManager manager = DebugLogManager.Instance;
		if (manager != null)
		{
			manager.HideLogWindow();
		}
	}

	/// <summary>Shows the window if it is hidden and hides it if it is open.</summary>
	public static void Toggle()
	{
		if (IsOpen)
		{
			Hide();
		}
		else
		{
			Show();
		}
	}

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Bootstrap()
	{
		Ensure();
	}
}
