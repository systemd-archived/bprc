using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>Persistent per-part physics controls, including BPIR's adjustment multipliers.</summary>
public sealed class PartPhysicsParameters : MonoBehaviour
{
    public const string GravityKey = "UseGravity";
    public const string DragKey = "LinearDrag";
    public const string PowerKey = "PowerMultiplier";
    public const string SuctionKey = "SuctionMultiplier";
    public const string OffsetXKey = "OffsetX";
    public const string OffsetYKey = "OffsetY";
    public const string RotationKey = "RotationOffset";
    public const string LayerKey = "CollisionLayer";
    public const string DepthKey = "DepthLayer";

    [Serializable]
    private sealed class Binding
    {
        public MonoBehaviour target;
        public string field, key;
        public float original;
        public bool suction;
    }

    [Serializable]
    private sealed class BodyDefault
    {
        public Rigidbody body;
        public bool gravity;
        public float drag;
    }

    [SerializeField] private List<Binding> m_bindings = new List<Binding>();
    [SerializeField] private bool m_captured;
    [SerializeField] private List<BodyDefault> m_defaults = new List<BodyDefault>();
    private readonly List<Rigidbody> m_bodies = new List<Rigidbody>();

    public static int Layer(BasePart part) => part != null && part.TryGetStoredParam(LayerKey, out float value) && BuildMachineTransform.Finite(value)
        ? Mathf.Clamp(Mathf.RoundToInt(value), 1, 6) : 1;
    public static bool SameLayer(BasePart a, BasePart b) => a != null && b != null && Layer(a) == Layer(b);
    public static float Rotation(BasePart part) => part.GetCustomParam(RotationKey, 0f);
    public static float Depth(BasePart part) => (Mathf.Clamp(Mathf.RoundToInt(part.GetCustomParam(DepthKey, 0f)), -50, 50) +
        (Layer(part) - 1) * 2) * 0.01f;

    public static void Collect(BasePart part, List<string> keys)
    {
        keys.Add(GravityKey); keys.Add(DragKey);
        keys.Add(OffsetXKey); keys.Add(OffsetYKey); keys.Add(RotationKey);
        keys.Add(LayerKey); keys.Add(DepthKey);
        if (HasPower(part)) keys.Add(PowerKey);
        if (part is StickyWheel || part.GetComponentInChildren<SuperMagnet>(true) != null) keys.Add(SuctionKey);
    }

    private static bool HasPower(BasePart part) => part is MotorWheel || part is OffRoadWheel || part is StickyWheel || part is Rocket ||
        part is RCR2Port.RoverPortPoweredPart || part.GetComponentInChildren<FanPropeller>(true) != null ||
        part.GetComponentInChildren<Spring>(true) != null;

    public static bool TryRead(BasePart part, string key, out float value)
    {
        switch (key)
        {
            case GravityKey: value = part.GetCustomParam(key, part.rigidbody == null || part.rigidbody.useGravity ? 1f : 0f); return true;
            case DragKey: value = part.GetCustomParam(key, part.rigidbody != null ? part.rigidbody.drag : INUserSettings.Instance?.StarSeaSettings?.PartDrag ?? 0.2f); return true;
            case PowerKey: case SuctionKey: value = part.GetCustomParam(key, 1f); return true;
            case OffsetXKey: value = GridlessBuild.GetOffset(part).x; return true;
            case OffsetYKey: value = GridlessBuild.GetOffset(part).y; return true;
            case RotationKey: value = Rotation(part); return true;
            case LayerKey: value = Layer(part); return true;
            case DepthKey: value = part.GetCustomParam(key, 0f); return true;
            default: value = 0f; return false;
        }
    }

    public static bool Apply(BasePart part, string key, float value)
    {
        if (!BuildMachineTransform.Finite(value)) return false;
        switch (key)
        {
            case GravityKey: case DragKey: case PowerKey: case SuctionKey:
                Refresh(part); return true;
            case OffsetXKey: case OffsetYKey:
                Vector2 offset = GridlessBuild.GetOffset(part);
                if (key == OffsetXKey) offset.x = value; else offset.y = value;
                GridlessBuild.SetOffset(part, offset);
                RefreshPose(part); return true;
            case LayerKey:
                part.contraption?.RebuildLayerMap();
                goto case DepthKey;
            case RotationKey: case DepthKey:
                GridlessBuild.InvalidateBounds(part.contraption);
                RefreshPose(part); return true;
            default: return false;
        }
    }

    private static void RefreshPose(BasePart part)
    {
        if (part.contraption == null || part.contraption.IsRunning) return;
        part.contraption.RefreshPartPose(part);
        PartRotationAnimator.Complete(part.transform);
    }

    public static void Refresh(BasePart part)
    {
        var control = part.GetComponent<PartPhysicsParameters>();
        if (control == null)
        {
            if (!part.HasCustomParam(GravityKey) && !part.HasCustomParam(DragKey) &&
                !part.HasCustomParam(PowerKey) && !part.HasCustomParam(SuctionKey)) return;
            control = part.gameObject.AddComponent<PartPhysicsParameters>();
        }
        control.Capture(part);
        float power = Mathf.Max(0f, part.GetCustomParam(PowerKey, 1f));
        float suction = Mathf.Max(0f, part.GetCustomParam(SuctionKey, 1f));
        foreach (var binding in control.m_bindings)
        {
            if (binding.target == null) continue;
            var field = FindField(binding.target.GetType(), binding.field);
            if (field != null) field.SetValue(binding.target, ReadBinding(part, binding, binding.suction ? suction : power));
        }
        control.m_bodies.Clear();
        part.GetComponentsInChildren(true, control.m_bodies);
        foreach (var body in control.m_bodies)
        {
            BodyDefault original = null;
            foreach (var item in control.m_defaults) if (item.body == body) { original = item; break; }
            if (original == null)
            {
                original = new BodyDefault { body = body, gravity = body.useGravity, drag = body.drag };
                control.m_defaults.Add(original);
            }
            body.useGravity = part.GetCustomParam(GravityKey, original.gravity ? 1f : 0f) != 0f;
            body.drag = Mathf.Max(0f, part.GetCustomParam(DragKey, original.drag));
        }
    }

    public static void Prepare(BasePart part)
    {
        if (!part.HasCustomParam(PowerKey) && !part.HasCustomParam(SuctionKey)) return;
        var control = part.GetComponent<PartPhysicsParameters>() ?? part.gameObject.AddComponent<PartPhysicsParameters>();
        control.Capture(part);
    }

    public static void Removed(BasePart part, string key)
    {
        if (key == OffsetXKey || key == OffsetYKey)
        {
            Vector2 offset = GridlessBuild.GetOffset(part);
            if (key == OffsetXKey) offset.x = 0f; else offset.y = 0f;
            GridlessBuild.SetOffset(part, offset);
        }
        if (key == OffsetXKey || key == OffsetYKey || key == RotationKey || key == LayerKey || key == DepthKey)
        {
            if (key == LayerKey) part.contraption?.RebuildLayerMap();
            GridlessBuild.InvalidateBounds(part.contraption);
            RefreshPose(part);
        }
        if (key == GravityKey || key == DragKey || key == PowerKey || key == SuctionKey) Refresh(part);
    }

    private static float ReadBinding(BasePart part, Binding binding, float multiplier)
    {
        if (!INParamSystem.Enabled) return binding.original;
        if (part.HasCustomParam(binding.key)) return part.GetCustomParam(binding.key);
        for (Type type = binding.target.GetType(); type != null && type != typeof(MonoBehaviour); type = type.BaseType)
        {
            string key = type.Name + "." + binding.field;
            if (part.HasCustomParam(key)) return part.GetCustomParam(key);
        }
        return binding.original * multiplier;
    }

    private static FieldInfo FindField(Type type, string name)
    {
        while (type != null)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field != null) return field;
            type = type.BaseType;
        }
        return null;
    }

    private void Add(BasePart part, MonoBehaviour target, string name, bool suction = false)
    {
        if (target == null) return;
        var field = FindField(target.GetType(), name);
        if (field == null || field.FieldType != typeof(float)) return;
        m_bindings.Add(new Binding { target = target, field = name, original = (float)field.GetValue(target),
            key = target == part ? name : target.GetType().Name + "." + name, suction = suction });
    }

    private void Capture(BasePart part)
    {
        if (m_captured) return;
        m_captured = true;
        if (part is MotorWheel || part is OffRoadWheel || part is StickyWheel || part is RCR2Port.RoverPortPoweredPart)
            Add(part, part, "m_force");
        if (part is RCR2Port.RoverPortPoweredPart) Add(part, part, "m_torque");
        Add(part, part.GetComponentInChildren<FanPropeller>(true), "m_force");
        if (part is StickyWheel) Add(part, part, "m_stickiness", true);
        var magnet = part.GetComponentInChildren<SuperMagnet>(true);
        Add(part, magnet, "m_radius", true); Add(part, magnet, "m_speedMin", true); Add(part, magnet, "m_speedMax", true);
    }
}
