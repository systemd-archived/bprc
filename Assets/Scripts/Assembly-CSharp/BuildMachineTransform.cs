using UnityEngine;

/// <summary>A persistent rigid build frame. Logical cells and their connection graph stay intact.</summary>
public struct BuildMachineTransform
{
    public const string XKey = "@machineX";
    public const string YKey = "@machineY";
    public const string AngleKey = "@machineAngle";
    public Vector2 translation;
    public float angle;

    public static Vector2 Rotate(Vector2 point, float degrees)
    {
        float a = degrees * Mathf.Deg2Rad;
        float c = Mathf.Cos(a), s = Mathf.Sin(a);
        return new Vector2(c * point.x - s * point.y, s * point.x + c * point.y);
    }
    public Vector2 TransformPoint(Vector2 point) => Rotate(point, angle) + translation;
    public Vector2 InversePoint(Vector2 point) => Rotate(point - translation, -angle);
    public static BuildMachineTransform Read(BasePart part)
    {
        var frame = new BuildMachineTransform();
        if (part == null) return frame;
        part.TryGetStoredParam(XKey, out frame.translation.x);
        part.TryGetStoredParam(YKey, out frame.translation.y);
        part.TryGetStoredParam(AngleKey, out frame.angle);
        if (!Finite(frame.translation.x) || !Finite(frame.translation.y) || !Finite(frame.angle)) return new BuildMachineTransform();
        return frame;
    }
    public static BuildMachineTransform Read(Contraption contraption) => Read(contraption, GridlessBuild.PrimaryGroup(contraption));
    public static BuildMachineTransform Read(Contraption contraption, int groupId)
    {
        return GridlessBuild.TryGetGroup(contraption, groupId, out var group) ? group.frame : new BuildMachineTransform();
    }
    public void Store(BasePart part)
    {
        if (part == null) return;
        part.StoreBuildFrame(translation, Mathf.Repeat(angle, 360f));
    }
    public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    public static BuildMachineTransform AroundPivot(BuildMachineTransform start, Vector2 pivot, float delta)
    {
        return new BuildMachineTransform {
            translation = pivot + Rotate(start.translation - pivot, delta),
            angle = Mathf.Repeat(start.angle + delta, 360f)
        };
    }
    public static void Apply(Contraption contraption, BuildMachineTransform frame) => Apply(contraption, frame, GridlessBuild.PrimaryGroup(contraption));
    public static void Apply(Contraption contraption, BuildMachineTransform frame, int groupId)
    {
        if (contraption == null || !Finite(frame.translation.x) || !Finite(frame.translation.y) || !Finite(frame.angle)) return;
        BuildMachineTransform previous = Read(contraption, groupId);
        float delta = Mathf.DeltaAngle(previous.angle, frame.angle);
        foreach (BasePart part in contraption.Parts)
            if (part != null && !part.m_static && GridlessBuild.GetGroupId(part) == groupId)
            {
                // Keep each merged lattice's own frame; apply the same rigid delta to all members.
                BuildMachineTransform own = Read(part);
                new BuildMachineTransform { translation = frame.TransformPoint(previous.InversePoint(own.translation)),
                    angle = own.angle + delta }.Store(part);
            }
        GridlessBuild.InvalidateBounds(contraption);
        foreach (BasePart part in contraption.Parts)
            if (part != null && !part.m_static && GridlessBuild.GetGroupId(part) == groupId)
            { contraption.RefreshPartPose(part); PartRotationAnimator.Complete(part.transform); }
    }
    public static Vector3 PointerLocal(Contraption contraption, Vector3 world, bool snap = true)
    {
        Vector3 local = contraption != null ? contraption.transform.InverseTransformPoint(world) : world;
        Vector2 point = GridlessBuild.ResolvePoint(contraption, local, snap, out _, out _);
        return new Vector3(point.x, point.y, local.z);
    }
}
