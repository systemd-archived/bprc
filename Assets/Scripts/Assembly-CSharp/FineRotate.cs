using System;
using System.Runtime.CompilerServices;
using UnityEngine;

/// <summary>
/// 部件精细旋转: part rotation at a step the player chooses, instead of the build screen's fixed 45/90
/// degree increments.
/// </summary>
/// <remarks>
/// <para>
/// <b>What it changes and what it does not.</b> Only the part <i>root's</i> orientation becomes
/// continuous. Parts using the base eight-direction rotation keep their grid enum at the nearest
/// eighth. Specialized setters can encode a sprite variant, flip, or connector layout in that enum;
/// those retain their native state while an extra root angle turns the complete part.
/// </para>
/// <para>
/// The angle rides in the part's own stored parameters, the same channel and for the same reason as the
/// grid-less offset: it is the only per-part storage that survives a save, a clone and the multiplayer
/// blueprint. The keys carry the internal prefix so they never appear as editable rows or earn a
/// "modified" badge.
/// </para>
/// <para>
/// Specialized parts compose the extra angle in BasePart.ApplyBuildRotation, preserving their authored
/// child orientations and flips. The machine frame is applied after the part's own orientation.
/// </para>
/// <para>
/// Reference: Besiege's advanced building exposes an adjustable rotation snap
/// (<c>StatMaster.Mode.Transform.Snap.rotation</c>, 45 by default, applied as
/// <c>Round(value / snap) * snap</c>) with <c>Ctrl</c> to bypass it. See
/// <c>docs/BesiegeBuildSystemCompare.md</c> §1.2 and §4.2.
/// </para>
/// </remarks>
public static class FineRotate
{
    /// <summary>Stored parameter key the angle is persisted under.</summary>
    public const string AngleKey = BasePart.InternalParamPrefix + "fineAngle";

    private sealed class Box
    {
        public float angle;
    }

    // Weakly keyed, like the grid-less offsets: a destroyed part leaves nothing behind.
    private static readonly ConditionalWeakTable<BasePart, Box> angles =
        new ConditionalWeakTable<BasePart, Box>();
    private static readonly System.Collections.Generic.HashSet<BasePart> directRotations = new System.Collections.Generic.HashSet<BasePart>();
    private sealed class RotationState { public bool initialized; public BasePart.GridRotation rotation; }
    private static readonly ConditionalWeakTable<BasePart, RotationState> rotationStates = new ConditionalWeakTable<BasePart, RotationState>();
    private static readonly System.Collections.Generic.Dictionary<Type, bool> enumAngles = new System.Collections.Generic.Dictionary<Type, bool>();

    /// <summary>The step in degrees, or 0 when the feature is off and the stock increments apply.</summary>
    public static int StepDegrees
    {
        get
        {
            GeneralSettings settings = INUserSettings.Instance?.GeneralSettings;
            return settings != null ? settings.FineRotationStep : 0;
        }
    }

    public static bool Enabled => StepDegrees > 0;

    /// <summary>
    /// The angle to draw <paramref name="part"/> at: its fine angle when there is one, otherwise the grid
    /// angle it was given.
    /// </summary>
    /// <remarks>
    /// Called from <c>BasePart.SetRotation</c>, which is the single place the part root's rotation is
    /// applied - so every rotation path (placement, the rotate tap, a loaded save) goes through it and
    /// none of them has to know about this. Saved angles remain valid when the step setting is zero;
    /// an untouched part uses the supplied grid angle.
    /// </remarks>
    public static float GetAngle(BasePart part, float fallback)
    {
        if (part == null) return fallback;
        if (angles.TryGetValue(part, out Box box)) return box.angle;
        if (!part.TryGetStoredParam(AngleKey, out float stored) || !BuildMachineTransform.Finite(stored)) return fallback;
        angles.Remove(part);
        angles.Add(part, new Box { angle = stored });
        return stored;
    }

    /// <summary>
    /// Turns <paramref name="part"/> by one step. Returns false when the feature is off, so the caller can
    /// fall back to the stock rotation.
    /// </summary>
    /// <remarks>
    /// The stock rotation is not merely "45 degrees further": <c>Contraption.Flip</c> tries successive
    /// rotations until the part reports it can connect, which is how a grid part always ends up attached
    /// to something. That search is bypassed here on purpose - an angle chosen from a fixed step is the
    /// feature - and the nearest-eighth snap keeps the connectivity answer meaningful for the angle the
    /// player actually chose.
    /// </remarks>
    public static bool Step(BasePart part, int direction)
    {
        if (part == null || !Enabled || direction == 0) return false;

        float current = GetAngle(part, part.GetRotationAngle(part.m_gridRotation));
        float next = Mathf.Repeat(current + direction * StepDegrees, 360f);

        SetAngle(part, next);
        return true;
    }

    /// <summary>Sets an absolute local angle without adding the enum change a second time.</summary>
    public static void SetAngle(BasePart part, float degrees)
    {
        if (part == null || !BuildMachineTransform.Finite(degrees)) return;
        Type type = part.GetType();
        if (!enumAngles.TryGetValue(type, out bool enumIsAngle))
        {
            enumIsAngle = part is RCR2Port.RoverPortPart ||
                (type.GetMethod(nameof(BasePart.SetRotation), new[] { typeof(BasePart.GridRotation) }).DeclaringType == typeof(BasePart) &&
                 type.GetMethod(nameof(BasePart.SetRotation), new[] { typeof(int) }).DeclaringType == typeof(BasePart));
            enumAngles.Add(type, enumIsAngle);
        }
        Restore(part, enumIsAngle ? NearestEighth(degrees) : part.m_gridRotation, true, degrees);
    }

    internal static void Restore(BasePart part, BasePart.GridRotation rotation, bool hasAngle, float degrees)
    {
        if (part == null) return;
        directRotations.Add(part);
        try
        {
            if (hasAngle) part.SetStoredParam(AngleKey, Mathf.Repeat(degrees, 360f));
            else part.RemoveStoredParam(AngleKey);
            part.SetRotation(rotation);
            part.ChangeVisualConnections();
            PartRotationAnimator.Complete(part.transform);
        }
        finally { directRotations.Remove(part); }
    }

    public static void Invalidate(BasePart part) { if (part != null) angles.Remove(part); }

    public static void OnDiscreteRotation(BasePart part, BasePart.GridRotation previous, BasePart.GridRotation next)
    {
        if (part == null || !part.TryGetStoredParam(AngleKey, out float angle)) return;
        var state = rotationStates.GetOrCreateValue(part); state.initialized = true; state.rotation = next;
        if (Enabled || directRotations.Contains(part) || previous == next) return;
        float delta = Mathf.DeltaAngle(part.GetRotationAngle(previous), part.GetRotationAngle(next));
        part.SetStoredParam(AngleKey, Mathf.Repeat(angle + delta, 360f));
    }

    internal static void OnAppliedRotation(BasePart part)
    {
        if (!part.TryGetStoredParam(AngleKey, out _)) return;
        var state = rotationStates.GetOrCreateValue(part);
        // Specialized parts write their enum directly, so observe their native step here too.
        if (state.initialized) OnDiscreteRotation(part, state.rotation, part.m_gridRotation);
        else { state.initialized = true; state.rotation = part.m_gridRotation; }
    }

    /// <summary>The <see cref="BasePart.GridRotation"/> value closest to <paramref name="degrees"/>.</summary>
    /// <remarks>
    /// Public because it is the one piece of this that can be checked without the settings singleton
    /// loaded, and because getting it wrong is silent: the part would still turn, but the build model
    /// would answer connectivity questions about a different facing than the one drawn.
    /// </remarks>
    public static BasePart.GridRotation NearestEighth(float degrees)
    {
        int eighth = Mathf.RoundToInt(Mathf.Repeat(degrees, 360f) / 45f) % 8;
        return eighth switch
        {
            0 => BasePart.GridRotation.Deg_0,
            1 => BasePart.GridRotation.Deg_45,
            2 => BasePart.GridRotation.Deg_90,
            3 => BasePart.GridRotation.Deg_135,
            4 => BasePart.GridRotation.Deg_180,
            5 => BasePart.GridRotation.Deg_225,
            6 => BasePart.GridRotation.Deg_270,
            _ => BasePart.GridRotation.Deg_315,
        };
    }
}
