using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Dumps the public surface of the bundled ShellFileDialogs plugin, because the save/export feature of
/// the contraption save page needs to know whether the plugin offers a save dialog (and a folder
/// picker) next to the open dialog that INFilePicker already uses.
///
/// Run with: Unity -batchmode -executeMethod ShellDialogApiAudit.Run
/// Report: Builds/QA/shell-dialogs-api.txt
/// </summary>
public static class ShellDialogApiAudit
{
    [MenuItem("Tools/QA/Dump ShellFileDialogs API")]
    public static void Run()
    {
        List<string> lines = new List<string>();
        try
        {
            Assembly assembly = typeof(ShellFileDialogs.FileOpenDialog).Assembly;
            lines.Add("Assembly: " + assembly.FullName);
            lines.Add("Location: " + assembly.Location);
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                types = exception.Types.Where(type => type != null).ToArray();
                lines.Add("(partial type list: " + exception.LoaderExceptions.Length + " loader exceptions)");
            }
            foreach (Type type in types.OrderBy(type => type.FullName))
            {
                if (!type.IsPublic) continue;
                lines.Add("TYPE " + type.FullName + (type.IsEnum ? " (enum)" : string.Empty));
                if (type.IsEnum)
                {
                    lines.Add("   values: " + string.Join(", ", Enum.GetNames(type)));
                    continue;
                }
                foreach (ConstructorInfo constructor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
                {
                    lines.Add("   ctor(" + Describe(constructor.GetParameters()) + ")");
                }
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    lines.Add("   " + (method.IsStatic ? "static " : string.Empty) + method.ReturnType.Name + " " + method.Name + "(" + Describe(method.GetParameters()) + ")");
                }
            }
        }
        catch (Exception exception)
        {
            lines.Add("FAILED: " + exception);
        }
        Directory.CreateDirectory("Builds/QA");
        File.WriteAllLines("Builds/QA/shell-dialogs-api.txt", lines);
        Debug.Log("SHELL_DIALOGS_API_DUMPED: " + lines.Count + " lines");
    }

    private static string Describe(ParameterInfo[] parameters)
    {
        return string.Join(", ", parameters.Select(parameter => parameter.ParameterType.Name + " " + parameter.Name));
    }
}
