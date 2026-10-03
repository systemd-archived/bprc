using System;
using UnityEngine;

/// <summary>
/// Havok Physics backend. Version gated: it runs wherever Havok Physics for Unity can run, and
/// reports precisely what is missing everywhere else.
/// </summary>
/// <remarks>
/// <para>
/// Havok Physics for Unity is not a standalone rigid-body API. It is a solver for the DOTS
/// <c>Unity.Physics</c> package: <c>Havok.Physics.dll</c> declares the ECS components, and selecting
/// Havok means adding the <c>PhysicsStep</c> singleton with <c>SimulationType.HavokPhysics</c>. That
/// is what the reference <c>bpsim_2023.0</c> project does, and what <see cref="HavokDotsWorld"/> does
/// here.
/// </para>
/// <para>
/// The prerequisites are checked in the order a reader can act on them:
/// </para>
/// <list type="number">
///   <item><description>the <c>HAVOK_PHYSICS</c> scripting define, which switches on the DOTS half of this backend and is set by <c>Assets/Editor/HavokPhysicsSetup.cs</c>;</description></item>
///   <item><description>Unity <see cref="MinimumUnityVersion"/> or newer — <c>com.unity.physics</c> does not exist for older editors, and Unity states that Havok Physics for Unity "cannot be accessed before the release of Unity 2022.2";</description></item>
///   <item><description>the <c>Unity.Entities</c>, <c>Unity.Physics</c>, <c>Havok.Physics</c> and <c>Havok.Physics.Hybrid</c> assemblies, which the <c>com.unity.physics</c> and <c>com.havok.physics</c> packages provide (the latter only on Unity Pro, Enterprise or Industry licences).</description></item>
/// </list>
/// <para>
/// The editor requirement is met by this project (Unity 2022.3.62f2), so the gate now stops at the
/// packages: without <c>com.unity.physics</c> the engine stays listed but not selectable, with
/// <c>Unity.Entities is not installed</c> beside it. Once the packages are added and the define is
/// set, <see cref="IsSupported"/> returns true and the backend drives a real DOTS world.
/// </para>
/// <para>
/// <b>Verification status.</b> The DOTS half is compiled and API-checked against the Havok and DOTS
/// assemblies shipped in <c>bpsim_2023.0/Assets/Plugins</c> — see <c>Tools/compile-check.ps1
/// -HavokProbe</c> — but has never been executed, because the packages and a Havok licence are not
/// present on this machine. Remaining gaps are listed in <c>docs/HavokPhysics.md</c>.
/// </para>
/// </remarks>
public sealed class HavokPhysicsBackend : IPhysicsBackend
{
    /// <summary>Minimum Unity that can host <c>com.unity.physics</c>, and therefore Havok.</summary>
    public const string MinimumUnityVersion = "2022.2.0b8";

    /// <summary>The scripting define that switches the DOTS half of this backend on.</summary>
    public const string ScriptingDefine = "HAVOK_PHYSICS";

    private const string Help =
        "Havok Physics for Unity needs Unity " + MinimumUnityVersion + " or newer, the com.unity.physics " +
        "and com.havok.physics packages, and a Unity Pro, Enterprise or Industry licence. " +
        "See docs/HavokPhysics.md.";

#if HAVOK_PHYSICS
    private readonly HavokDotsWorld m_world;
#endif

    public PhysicsEngine Engine => PhysicsEngine.Havok;

    public string DisplayName => "Havok";

    /// <summary>
    /// Reports whether this build can run Havok. <paramref name="reason"/> is the short form shown in
    /// the switcher list when it cannot; see <see cref="Describe"/> for the full explanation.
    /// </summary>
    public static bool IsSupported(out string reason)
    {
        reason = Probe();
        return reason == null;
    }

    /// <summary>The full explanation of the current availability, for logs and editor tooling.</summary>
    public static string Describe()
    {
        string reason = Probe();
        return reason == null
            ? "Havok Physics is available."
            : "Havok Physics is unavailable: " + reason + ". " + Help;
    }

    /// <summary>
    /// The first reason this build cannot run Havok, or <c>null</c> when it can. The order matters:
    /// the compile-time define is reported before the editor version so a project that has already
    /// been upgraded to 2022.2+ but not switched over gets an actionable message.
    /// </summary>
    private static string Probe()
    {
        if (!HavokDots.Compiled) return "not compiled in; define " + ScriptingDefine;

        if (!UnityVersionGate.AtLeast(Application.unityVersion, MinimumUnityVersion))
        {
            return "needs Unity " + MinimumUnityVersion + "+ (this is " + Application.unityVersion + ")";
        }

        // Deliberately checked before the packages. Havok's runtime is gated by Microsoft's licence, not
        // by Unity's package registry: the packages download normally on any subscription, but only Pro,
        // Enterprise and Industry may use their runtime features. Reporting "Unity.Entities is not
        // installed" first would send a Personal-licence user off to install two packages plus their
        // whole DOTS dependency chain, none of which could ever unlock the engine.
        if (!Application.HasProLicense())
        {
            return "needs a Unity Pro, Enterprise or Industry licence (this is a Personal licence, " +
                "and Havok locks its runtime features on Personal)";
        }

        string missing;
        if (!HavokDots.Available(out missing)) return missing + " is not installed";

        return null;
    }

    public HavokPhysicsBackend()
    {
        string reason = Probe();
        if (reason != null)
        {
            throw new NotSupportedException("Havok Physics is unavailable: " + reason + ". " + Help);
        }
#if HAVOK_PHYSICS
        m_world = new HavokDotsWorld();
#endif
    }

    // Every member below is a no-op without HAVOK_PHYSICS. That is not a silent failure: the
    // constructor above refuses to build the object at all unless the probe passed, so an instance
    // only ever exists in a build where the DOTS half is compiled in.

    public void Synchronize()
    {
#if HAVOK_PHYSICS
        m_world.Synchronize();
#endif
    }

    public void Step(float delta)
    {
#if HAVOK_PHYSICS
        m_world.Step(delta);
#endif
    }

    public void Wake(Rigidbody body)
    {
#if HAVOK_PHYSICS
        m_world.Wake(body);
#endif
    }

    public void Sleep(Rigidbody body)
    {
#if HAVOK_PHYSICS
        m_world.Sleep(body);
#endif
    }

    public bool IsSleeping(Rigidbody body)
    {
#if HAVOK_PHYSICS
        return m_world.IsSleeping(body);
#else
        return body != null && body.IsSleeping();
#endif
    }

    /// <summary>
    /// Joints are not mirrored into the DOTS world yet, so the Unity joint stays the source of truth
    /// for its own angle. See docs/HavokPhysics.md.
    /// </summary>
    public void Refilter(Collider a, Collider b)
    {
#if HAVOK_PHYSICS
        m_world.Refilter(a, b);
#endif
    }

    public float HingeAngle(HingeJoint joint)
    {
        return joint != null ? joint.angle : 0f;
    }

    public void Dispose()
    {
#if HAVOK_PHYSICS
        if (m_world != null) m_world.Dispose();
#endif
    }
}
