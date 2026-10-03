using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>Same-step GPU force batches. Collision and joint solving stays with GamePhysics.</summary>
public sealed class GpuPhysicsForces : IDisposable
{
    public enum ForceKind { Gravity, PointCharge }
    public const int MinimumGpuBodies = 256;
    public const int Kinematic = 1;
    public const int SpecialCharge = 2;
    private const int Threads = 64;

    // float4 + int4 gives the same 32-byte layout on D3D, Vulkan and GLES 3.1.
    [StructLayout(LayoutKind.Sequential)]
    public struct Body
    {
        public Vector4 state; // world X, world Y, mass, ordinary charge
        public int component, flags, reserved0, reserved1;
    }

    private static readonly List<GpuPhysicsForces> live = new List<GpuPhysicsForces>();
    private static bool enabled;
    private static readonly int BodiesId = Shader.PropertyToID("_Bodies");
    private static readonly int ForcesId = Shader.PropertyToID("_Forces");
    private static readonly int CountId = Shader.PropertyToID("_Count");
    private static readonly int ModeId = Shader.PropertyToID("_Mode");
    private static readonly int GravityId = Shader.PropertyToID("_Gravity");
    private static readonly int AttenuationId = Shader.PropertyToID("_Attenuation");
    private readonly ForceKind kind;
    private ComputeShader shader;
    private ComputeBuffer bodiesBuffer, forcesBuffer;
    private int kernel, capacity, measuredCount, slowSteps;
    private bool calibrated, preferGpu, unavailable, disposed;
    private double baselineCpuMs;
    private Vector4[] cpuForces = Array.Empty<Vector4>(), gpuForces = Array.Empty<Vector4>();
    public Body[] Inputs { get; private set; } = Array.Empty<Body>();
    public string Status { get; private set; } = "CPU";
    public double LastMilliseconds { get; private set; }
    public long GpuDispatches { get; private set; }
    public bool HasGpuBuffers => bodiesBuffer != null;

    public GpuPhysicsForces(ForceKind kind) { this.kind = kind; live.Add(this); }

    public static void Configure(bool value)
    {
        if (enabled == value) return;
        enabled = value;
        foreach (var solver in live)
        {
            solver.ReleaseGpu();
            solver.calibrated = solver.preferGpu = solver.unavailable = false;
            solver.slowSteps = 0;
            solver.Status = value ? "CPU（等待计算）" : "CPU（关闭）";
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        foreach (var solver in live) solver.ReleaseGpu();
        live.Clear(); enabled = false;
    }

    public static string Describe()
    {
        if (!enabled) return "GPU 计算：关闭";
        string gravity = "未运行", charge = "未运行";
        foreach (var solver in live)
            if (solver.kind == ForceKind.Gravity) gravity = solver.Status; else charge = solver.Status;
        return "GPU 力计算：引力 " + gravity + " / 点电荷 " + charge;
    }

    public void Reserve(int count)
    {
        if (disposed) throw new ObjectDisposedException(nameof(GpuPhysicsForces));
        if (count <= capacity) return;
        ReleaseGpu();
        capacity = Mathf.NextPowerOfTwo(Mathf.Max(Threads, count));
        Inputs = new Body[capacity]; cpuForces = new Vector4[capacity]; gpuForces = new Vector4[capacity];
        calibrated = preferGpu = false;
        slowSteps = 0;
    }

    // Called during vehicle initialization, rather than compiling the shader in a physics step.
    public void Prepare(int count)
    {
        Reserve(count);
        // Warm the managed/JIT path too, so its first-call cost cannot bias device selection.
        Cpu(Mathf.Min(count, 2), 0f, 1f);
        if (enabled && count >= MinimumGpuBodies && EnsureGpu())
            TryGpu(1, 0f, 1f, out _); // Warm shader/driver with a zero force batch.
    }

    public Vector4[] Evaluate(int count, float gravity = 0f, float attenuation = 1f)
    {
        if (count < 0 || count > capacity) throw new ArgumentOutOfRangeException(nameof(count));
        if (!enabled || count < MinimumGpuBodies || unavailable)
        {
            if (!unavailable) Status = !enabled ? "CPU（关闭）" : "CPU（规模较小）";
            return Cpu(count, gravity, attenuation);
        }
        if (!calibrated)
        {
            Cpu(count, gravity, attenuation);
            baselineCpuMs = LastMilliseconds; measuredCount = count;
            bool ok = TryGpu(count, gravity, attenuation, out _);
            bool parity = ok && Equivalent(cpuForces, gpuForces, count);
            if (ok && !parity) Fail("GPU 结果与 CPU 不一致");
            preferGpu = parity && LastMilliseconds < baselineCpuMs * 0.9;
            calibrated = true;
            Status = unavailable ? Status : preferGpu ? "GPU" : "CPU（GPU 开销较高）";
            if (!preferGpu) ReleaseGpu();
            return preferGpu ? gpuForces : cpuForces;
        }
        if (preferGpu && TryGpu(count, gravity, attenuation, out var result))
        {
            double scaledCpuMs = baselineCpuMs * count * count / ((double)measuredCount * measuredCount);
            slowSteps = LastMilliseconds > scaledCpuMs * 1.25 ? slowSteps + 1 : 0;
            if (slowSteps >= 4)
            {
                preferGpu = false; Status = "CPU（GPU 等待较长）";
                ReleaseGpu(); // Keep this decision until a larger batch or an explicit toggle.
            }
            return result;
        }
        return Cpu(count, gravity, attenuation);
    }

    private Vector4[] Cpu(int count, float gravity, float attenuation)
    {
        long started = Stopwatch.GetTimestamp();
        CalculateCpu(kind, Inputs, cpuForces, count, gravity, attenuation);
        LastMilliseconds = Elapsed(started);
        return cpuForces;
    }

    public static void CalculateCpu(ForceKind kind, Body[] bodies, Vector4[] forces, int count, float gravity, float attenuation)
    {
        Array.Clear(forces, 0, count);
        for (int i = 0; i < count; i++)
        {
            Body a = bodies[i];
            for (int j = i + 1; j < count; j++)
            {
                Body b = bodies[j];
                if ((a.flags & Kinematic) != 0 && (b.flags & Kinematic) != 0) continue;
                float dx = b.state.x - a.state.x, dy = b.state.y - a.state.y;
                float sqr = dx * dx + dy * dy, scale;
                if (kind == ForceKind.Gravity)
                {
                    if (a.component == b.component) continue;
                    sqr = Mathf.Max(1f, sqr);
                    float magnitude = gravity * a.state.z * b.state.z / Mathf.Pow(sqr, attenuation);
                    if (magnitude < 0.01f) continue;
                    scale = magnitude / Mathf.Sqrt(sqr);
                }
                else
                {
                    if (((a.flags | b.flags) & SpecialCharge) != 0 || sqr >= 4096f) continue;
                    float distance = Mathf.Max(1f, Mathf.Sqrt(sqr));
                    scale = -1.5f * a.state.w * b.state.w / (distance * distance * distance);
                }
                Vector4 force = new Vector4(scale * dx, scale * dy, 0f, 0f);
                if ((a.flags & Kinematic) == 0) forces[i] += force;
                if ((b.flags & Kinematic) == 0) forces[j] -= force;
            }
        }
    }

    // Public for isolated device audits and reusable by other force calculators. Never applies forces.
    public bool TryGpu(int count, float gravity, float attenuation, out Vector4[] result)
    {
        result = gpuForces;
        if (count < 1 || count > capacity || !EnsureGpu()) return false;
        try
        {
            long started = Stopwatch.GetTimestamp();
            bodiesBuffer.SetData(Inputs, 0, 0, count);
            shader.SetInt(CountId, count); shader.SetInt(ModeId, (int)kind);
            shader.SetFloat(GravityId, gravity); shader.SetFloat(AttenuationId, attenuation);
            shader.Dispatch(kernel, (count + Threads - 1) / Threads, 1, 1);
            // Same-step readback preserves physics timing. Its entire cost participates in selection.
            forcesBuffer.GetData(gpuForces, 0, 0, count);
            LastMilliseconds = Elapsed(started); GpuDispatches++;
            for (int i = 0; i < count; i++)
                if (!BuildMachineTransform.Finite(gpuForces[i].x) || !BuildMachineTransform.Finite(gpuForces[i].y))
                    throw new InvalidOperationException("GPU 返回非有限力");
            return true;
        }
        catch (Exception error) { Fail(error.Message); return false; }
    }

    private bool EnsureGpu()
    {
        if (unavailable || disposed) return false;
        if (shader != null) return true;
        if (!SystemInfo.supportsComputeShaders || SystemInfo.graphicsShaderLevel < 45)
        { unavailable = true; Status = "CPU（设备不支持）"; return false; }
        try
        {
            var asset = Resources.Load<ComputeShader>("Physics/PairForces");
            if (asset == null || !asset.HasKernel("PairForces")) throw new InvalidOperationException("GPU 计算资源不可用");
            shader = UnityEngine.Object.Instantiate(asset); shader.hideFlags = HideFlags.HideAndDontSave;
            kernel = shader.FindKernel("PairForces");
            if (!shader.IsSupported(kernel)) throw new InvalidOperationException("当前图形接口不支持力计算内核");
            bodiesBuffer = new ComputeBuffer(capacity, 32); forcesBuffer = new ComputeBuffer(capacity, 16);
            shader.SetBuffer(kernel, BodiesId, bodiesBuffer); shader.SetBuffer(kernel, ForcesId, forcesBuffer);
            return true;
        }
        catch (Exception error) { Fail(error.Message); return false; }
    }

    private void Fail(string message)
    {
        unavailable = true; preferGpu = false;
        Status = "CPU（GPU 不可用）";
        ReleaseGpu();
        UnityEngine.Debug.LogWarning("GPU 力计算已回退 CPU：" + message);
    }

    public static bool Equivalent(Vector4[] a, Vector4[] b, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (!BuildMachineTransform.Finite(a[i].x) || !BuildMachineTransform.Finite(a[i].y) ||
                !BuildMachineTransform.Finite(b[i].x) || !BuildMachineTransform.Finite(b[i].y)) return false;
            float tolerance = 0.0005f * Mathf.Max(1f, Mathf.Max(Mathf.Abs(a[i].x), Mathf.Abs(a[i].y)));
            if (Mathf.Abs(a[i].x - b[i].x) > tolerance || Mathf.Abs(a[i].y - b[i].y) > tolerance) return false;
        }
        return true;
    }

    private static double Elapsed(long start) => (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
    private void ReleaseGpu()
    {
        bodiesBuffer?.Release(); forcesBuffer?.Release(); bodiesBuffer = forcesBuffer = null;
        if (shader != null)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(shader); else UnityEngine.Object.DestroyImmediate(shader);
            shader = null;
        }
    }
    public void Dispose()
    {
        if (disposed) return;
        ReleaseGpu(); live.Remove(this); disposed = true;
        Inputs = Array.Empty<Body>(); cpuForces = gpuForces = Array.Empty<Vector4>();
    }
}
