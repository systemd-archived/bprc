using System;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Reads and writes the Unity Console window's toolbar flags (Collapse, Clear on Play,
/// Log/Warning/Error filters, ...) from script.
///
/// Unity has no public API for them. The Console window keeps them in the native
/// <c>UnityEditor.LogEntries.consoleFlags</c> bit mask and persists that mask to the
/// project-scoped EditorPrefs key <c>kMonoEditorConsoleFlagsV2</c>, which on Windows is
/// <c>HKCU\Software\Unity Technologies\Unity Editor 5.x\kMonoEditorConsoleFlagsV2_h&lt;hash&gt;</c>.
/// The Console toolbar's Collapse toggle is literally
/// <c>SetFlag(ConsoleFlags.Collapse, GUILayout.Toggle(wasCollapsed, "Collapse", ...))</c>
/// (see UnityCsReference, <c>Editor/Mono/ConsoleWindow.cs</c>), so writing the flag here is
/// the same edit the button makes - Unity then saves it itself.
///
/// Bit values (UnityEditor.ConsoleWindow.ConsoleFlags):
///   1 Collapse, 2 ClearOnPlay, 4 ErrorPause, 8 Verbose, 16 StopForAssert,
///   32 StopForError, 64 Autoscroll, 128 LogLevelLog, 256 LogLevelWarning,
///   512 LogLevelError, 1024 ShowTimestamp, 2048 ClearOnBuild,
///   4096 ClearOnRecompile, 8192 UseMonospaceFont, 16384 StripLoggingCallstack
///
/// While Collapse is on, the Console merges every identical entry into one row carrying a
/// count badge, so a repeated message looks like it was logged once (and an entry logged
/// from several sources looks like it came from one).
///
/// Menu: Tools/Console/*
/// Batch: Unity -batchmode -quit -executeMethod ConsoleFlagsTool.DisableCollapse
/// </summary>
public static class ConsoleFlagsTool
{
    private const int CollapseBit = 1 << 0;

    /// <summary>Bit value to name, in mask order.</summary>
    private static readonly int[] Bits =
    {
        1 << 0, 1 << 1, 1 << 2, 1 << 3, 1 << 4, 1 << 5, 1 << 6, 1 << 7,
        1 << 8, 1 << 9, 1 << 10, 1 << 11, 1 << 12, 1 << 13, 1 << 14,
    };

    private static readonly string[] BitNames =
    {
        "Collapse", "ClearOnPlay", "ErrorPause", "Verbose", "StopForAssert",
        "StopForError", "Autoscroll", "LogLevelLog", "LogLevelWarning",
        "LogLevelError", "ShowTimestamp", "ClearOnBuild", "ClearOnRecompile",
        "UseMonospaceFont", "StripLoggingCallstack",
    };

    [MenuItem("Tools/Console/Report Console Flags")]
    private static void ReportMenu()
    {
        Debug.Log(Report());
    }

    [MenuItem("Tools/Console/Turn Collapse Off", true)]
    private static bool ValidateDisableCollapse()
    {
        int flags;
        return TryReadFlags(out flags) && (flags & CollapseBit) != 0;
    }

    [MenuItem("Tools/Console/Turn Collapse Off")]
    private static void DisableCollapseMenu()
    {
        Debug.Log(DisableCollapse());
    }

    /// <summary>Every known flag with its state, plus the raw persisted mask.</summary>
    public static string Report()
    {
        int flags;
        if (!TryReadFlags(out flags))
            return "[ConsoleFlagsTool] FAIL: UnityEditor.LogEntries.consoleFlags is not readable in this editor build.";

        StringBuilder sb = new StringBuilder();
        sb.Append("[ConsoleFlagsTool] consoleFlags = ").Append(flags)
          .Append(" (0x").Append(flags.ToString("X")).Append(")\n");
        for (int i = 0; i < Bits.Length; i++)
            sb.Append("  ").Append((flags & Bits[i]) != 0 ? "on  " : "off ").Append(BitNames[i]).Append('\n');
        sb.Append("  persisted EditorPrefs kMonoEditorConsoleFlagsV2 = ")
          .Append(EditorPrefs.GetInt("kMonoEditorConsoleFlagsV2", -1)).Append('\n');
        sb.Append("[ConsoleFlagsTool] Collapse is ").Append((flags & CollapseBit) != 0 ? "ON - identical entries are merged" : "off");
        return sb.ToString();
    }

    /// <summary>
    /// Clears the Collapse flag the way the toolbar button does, so the Console shows each
    /// entry separately again, and repaints any open Console window. Idempotent.
    /// </summary>
    public static string DisableCollapse()
    {
        return SetCollapse(false);
    }

    /// <summary>Sets the Collapse flag. Returns a before/after report, or a failure reason.</summary>
    public static string SetCollapse(bool collapsed)
    {
        return SetBit(CollapseBit, "Collapse", collapsed);
    }

    /// <summary>
    /// Sets any known flag by name, e.g. <c>SetFlag("ClearOnPlay", false)</c>. Useful for the
    /// other flags that also make entries disappear (ClearOnPlay / ClearOnBuild / ClearOnRecompile).
    /// </summary>
    public static string SetFlag(string name, bool value)
    {
        int bit = BitFor(name);
        if (bit == 0)
            return "[ConsoleFlagsTool] FAIL: unknown flag '" + name + "'. Known: " + string.Join(", ", BitNames) + ".";
        return SetBit(bit, name, value);
    }

    private static string SetBit(int bit, string label, bool value)
    {
        int before;
        if (!TryReadFlags(out before))
            return "[ConsoleFlagsTool] FAIL: UnityEditor.LogEntries.consoleFlags is not readable in this editor build.";

        bool was = (before & bit) != 0;
        if (was == value)
            return "[ConsoleFlagsTool] " + label + " was already " + (value ? "on" : "off") + "; consoleFlags = " + before + ". Nothing to do.";

        string error;
        if (!TryWriteFlag(bit, value, out error))
            return "[ConsoleFlagsTool] FAIL: " + error;

        int after;
        TryReadFlags(out after);
        RepaintConsoleWindows();

        bool now = (after & bit) != 0;
        if (now != value)
            return "[ConsoleFlagsTool] FAIL: asked for " + label + "=" + value + " but consoleFlags is " + after + ".";

        return "[ConsoleFlagsTool] " + label + " " + (was ? "on -> off" : "off -> on")
             + "; consoleFlags " + before + " -> " + after
             + "; EditorPrefs kMonoEditorConsoleFlagsV2 = " + EditorPrefs.GetInt("kMonoEditorConsoleFlagsV2", -1)
             + "; Console window repainted.";
    }

    private static int BitFor(string name)
    {
        if (string.IsNullOrEmpty(name)) return 0;
        for (int i = 0; i < BitNames.Length; i++)
            if (string.Equals(BitNames[i], name, StringComparison.OrdinalIgnoreCase)) return Bits[i];
        return 0;
    }

    // ---------- reflection over the internal editor API ----------

    private static Type FindLogEntriesType()
    {
        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try { types = asm.GetTypes(); }
            catch { continue; }
            foreach (Type t in types)
                if (t.FullName == "UnityEditor.LogEntries") return t;
        }
        return null;
    }

    private static bool TryReadFlags(out int flags)
    {
        flags = 0;
        Type t = FindLogEntriesType();
        if (t == null) return false;

        const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        PropertyInfo prop = t.GetProperty("consoleFlags", Static);
        if (prop != null && prop.PropertyType == typeof(int))
        {
            flags = (int)prop.GetValue(null, null);
            return true;
        }

        FieldInfo field = t.GetField("consoleFlags", Static);
        if (field != null && field.FieldType == typeof(int))
        {
            flags = (int)field.GetValue(null);
            return true;
        }
        return false;
    }

    private static bool TryWriteFlag(int bit, bool value, out string error)
    {
        error = null;
        Type t = FindLogEntriesType();
        if (t == null) { error = "UnityEditor.LogEntries not found."; return false; }

        const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        MethodInfo set = t.GetMethod("SetConsoleFlag", Static, null, new[] { typeof(int), typeof(bool) }, null);
        if (set == null) { error = "UnityEditor.LogEntries.SetConsoleFlag(int, bool) not found."; return false; }

        try
        {
            set.Invoke(null, new object[] { bit, value });
            return true;
        }
        catch (Exception e)
        {
            error = "SetConsoleFlag threw " + e.GetType().Name + ": " + (e.InnerException != null ? e.InnerException.Message : e.Message);
            return false;
        }
    }

    private static void RepaintConsoleWindows()
    {
        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type console = null;
            try
            {
                Type[] types = asm.GetTypes();
                foreach (Type t in types)
                    if (t.FullName == "UnityEditor.ConsoleWindow") { console = t; break; }
            }
            catch { continue; }
            if (console == null) continue;

            UnityEngine.Object[] windows = Resources.FindObjectsOfTypeAll(console);
            for (int i = 0; i < windows.Length; i++)
            {
                EditorWindow w = windows[i] as EditorWindow;
                if (w != null) w.Repaint();
            }
            return;
        }
    }
}
