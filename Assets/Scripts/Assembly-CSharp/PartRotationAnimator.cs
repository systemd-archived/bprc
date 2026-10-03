using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Smooth rotation for a part that is rotated in the build UI. RCR2 animates a part to its new angle
/// over RoverModelElement.ROTATE_ANIMATION_TIME (0.2 s, DOTween ease-out); BPLE snapped instantly.
///
/// This is shared by native parts (<see cref="BasePart.SetRotation(BasePart.GridRotation)"/>) and by the
/// ported RCR2 parts (<see cref="RCR2Port.RoverPortPart"/>). Outside play mode, or while the contraption
/// is running, the rotation is applied immediately, so saved data, footprints, physics and the editor
/// audits always see the final orientation.
/// </summary>
public static class PartRotationAnimator
{
    /// <summary>Rotation animation length in seconds; RCR2 used 0.2 s, sped up here on request.</summary>
    public const float Duration = 0.1f;

    private sealed class Tween
    {
        public Transform target;
        public Quaternion from;
        public Quaternion to;
        public float elapsed;
    }

    private static readonly List<Tween> s_active = new List<Tween>();
    private static RotationTweenDriver s_driver;

    /// <summary>
    /// Rotates the part so that its local rotation becomes <paramref name="localRotation"/>, animated
    /// while the player is building the contraption.
    /// </summary>
    public static void Apply(BasePart part, Quaternion localRotation)
    {
        if (part == null)
        {
            return;
        }
        Transform target = part.transform;
        if (!ShouldAnimate(part))
        {
            Cancel(target);
            target.localRotation = localRotation;
            return;
        }

        Tween tween = Find(target);
        if (tween == null)
        {
            tween = new Tween { target = target };
            s_active.Add(tween);
        }
        // Continue from wherever the previous animation has reached.
        tween.from = target.localRotation;
        tween.to = localRotation;
        tween.elapsed = 0f;
        EnsureDriver();
    }

    /// <summary>Applies the rotation immediately, cancelling any animation on the same part.</summary>
    public static void Snap(Transform target, Quaternion localRotation)
    {
        if (target == null)
        {
            return;
        }
        Cancel(target);
        target.localRotation = localRotation;
    }

    public static void Complete(Transform target)
    {
        Tween tween = Find(target);
        if (tween != null) Snap(target, tween.to);
    }

    /// <summary>True when the rotation should be animated instead of applied instantly.</summary>
    public static bool ShouldAnimate(BasePart part)
    {
        if (part == null || !Application.isPlaying || !part.isActiveAndEnabled)
        {
            return false;
        }
        Contraption contraption = part.contraption;
        return contraption == null || !contraption.IsRunning;
    }

    /// <summary>Ease-out curve of the rotation animation; 0 and 1 map to 0 and 1.</summary>
    public static float Ease(float ratio)
    {
        ratio = Mathf.Clamp01(ratio);
        return 1f - (1f - ratio) * (1f - ratio);
    }

    private static Tween Find(Transform target)
    {
        for (int i = 0; i < s_active.Count; i++)
        {
            if (s_active[i].target == target)
            {
                return s_active[i];
            }
        }
        return null;
    }

    private static void Cancel(Transform target)
    {
        for (int i = s_active.Count - 1; i >= 0; i--)
        {
            if (s_active[i].target == target)
            {
                s_active.RemoveAt(i);
            }
        }
    }

    private static void EnsureDriver()
    {
        if (s_driver != null)
        {
            return;
        }
        var host = new GameObject("PartRotationTweenDriver") { hideFlags = HideFlags.HideAndDontSave };
        Object.DontDestroyOnLoad(host);
        s_driver = host.AddComponent<RotationTweenDriver>();
    }

    private sealed class RotationTweenDriver : MonoBehaviour
    {
        private void Update()
        {
            // Unscaled time: the build UI can be paused with timeScale 0.
            float step = Time.unscaledDeltaTime;
            for (int i = s_active.Count - 1; i >= 0; i--)
            {
                Tween tween = s_active[i];
                if (tween.target == null)
                {
                    s_active.RemoveAt(i);
                    continue;
                }
                tween.elapsed += step;
                float ratio = Duration <= 0f ? 1f : Mathf.Clamp01(tween.elapsed / Duration);
                if (ratio >= 1f)
                {
                    tween.target.localRotation = tween.to;
                    s_active.RemoveAt(i);
                    continue;
                }
                // Ease out, matching the DOTween rotation RCR2 used.
                float eased = Ease(ratio);
                tween.target.localRotation = Quaternion.Slerp(tween.from, tween.to, eased);
            }
            if (s_active.Count == 0)
            {
                s_driver = null;
                Destroy(gameObject);
            }
        }
    }
}
