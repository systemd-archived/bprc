using System;
using UnityEngine;

/// <summary>
/// Compares Unity version strings such as <c>"2021.3.45f2"</c> and <c>"2022.2.0b8"</c>.
/// </summary>
/// <remarks>
/// <see cref="Version"/> cannot parse Unity's version strings because of the <c>a</c>/<c>b</c>/<c>f</c>
/// suffix, so the leading digits of each dotted component are compared instead. A component that is
/// absent counts as zero, which makes <c>"2022.2"</c> and <c>"2022.2.0"</c> compare equal.
/// </remarks>
public static class UnityVersionGate
{
    /// <summary>True when <paramref name="version"/> is the same as or newer than <paramref name="minimum"/>.</summary>
    public static bool AtLeast(string version, string minimum)
    {
        int[] left = Parse(version);
        int[] right = Parse(minimum);
        return left != null && right != null && Compare(left, right) >= 0;
    }

    /// <summary>Splits a Unity version string into its numeric components, or <c>null</c> if malformed.</summary>
    public static int[] Parse(string version)
    {
        if (string.IsNullOrEmpty(version)) return null;
        string[] parts = version.Split('.');
        var numbers = new int[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            int value = 0;
            int digits = 0;
            for (int c = 0; c < parts[i].Length; c++)
            {
                char character = parts[i][c];
                if (character < '0' || character > '9') break;
                value = value * 10 + (character - '0');
                digits++;
            }
            if (digits == 0) return null;
            numbers[i] = value;
        }
        return numbers;
    }

    /// <summary>Component-wise comparison, treating absent components as zero.</summary>
    public static int Compare(int[] left, int[] right)
    {
        int length = Math.Max(left.Length, right.Length);
        for (int i = 0; i < length; i++)
        {
            int a = i < left.Length ? left[i] : 0;
            int b = i < right.Length ? right[i] : 0;
            if (a != b) return a < b ? -1 : 1;
        }
        return 0;
    }
}

/// <summary>
/// Probes whether the DOTS/Havok assemblies the Havok backend needs are loadable at run time.
/// </summary>
/// <remarks>
/// <para>
/// The probe is deliberately kept out of <c>HavokDotsWorld</c> so that it exists in builds compiled
/// without <c>HAVOK_PHYSICS</c> as well: the switcher must be able to explain what is missing on a
/// Unity 2021.3 build, where none of these packages can be installed at all.
/// </para>
/// <para>
/// Presence is tested by name rather than by type because the assemblies are loaded on demand; asking
/// for an assembly by simple name is what makes Unity resolve it out of the project's plugin folders.
/// </para>
/// </remarks>
public static class HavokDots
{
    /// <summary>Assemblies Havok Physics for Unity needs, in the order they are reported as missing.</summary>
    public static readonly string[] Assemblies =
    {
        "Unity.Entities",
        "Unity.Physics",
        "Havok.Physics",
        "Havok.Physics.Hybrid",
    };

    private const string PhysicsStepType = "Unity.Physics.PhysicsStep, Unity.Physics";

    /// <summary>True when this assembly was compiled with the DOTS half of the backend included.</summary>
    public const bool Compiled =
#if HAVOK_PHYSICS
        true;
#else
        false;
#endif

    /// <summary>
    /// Reports whether every required assembly can be loaded. <paramref name="missing"/> names the
    /// first one that cannot, or is <c>null</c> when nothing is missing.
    /// </summary>
    public static bool Available(out string missing)
    {
        for (int i = 0; i < Assemblies.Length; i++)
        {
            if (!AssemblyPresent(Assemblies[i]))
            {
                missing = Assemblies[i];
                return false;
            }
        }
        if (!TypeLoadable(PhysicsStepType))
        {
            missing = "Unity.Physics.PhysicsStep";
            return false;
        }
        missing = null;
        return true;
    }

    /// <summary>True when an assembly with this simple name is already loaded or can be loaded.</summary>
    public static bool AssemblyPresent(string simpleName)
    {
        var loaded = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < loaded.Length; i++)
        {
            if (string.Equals(loaded[i].GetName().Name, simpleName, StringComparison.Ordinal)) return true;
        }
        try
        {
            System.Reflection.Assembly.Load(simpleName);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>True when an assembly-qualified type name resolves.</summary>
    public static bool TypeLoadable(string qualifiedName)
    {
        try
        {
            return Type.GetType(qualifiedName, false) != null;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
