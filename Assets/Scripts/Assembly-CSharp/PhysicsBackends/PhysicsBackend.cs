using System;
using UnityEngine;

/// <summary>
/// Selectable rigid-body simulation backends.
/// </summary>
/// <remarks>
/// The numeric values of <see cref="PhysX"/> and <see cref="Box3D"/> are part of the on-disk
/// settings format: old <c>StarSeaSettings</c> files stored a plain boolean (<c>false</c>/<c>true</c>)
/// for <c>UseBox3D</c>, and a boolean still deserializes onto <c>int</c> as 0/1. Do not renumber
/// those two members.
/// </remarks>
public enum PhysicsEngine
{
    /// <summary>Unity's built-in NVIDIA PhysX backend. Always available.</summary>
    PhysX = 0,

    /// <summary>Box3D via the Box3D.Runtime bindings. Windows and Linux only.</summary>
    Box3D = 1,

    /// <summary>
    /// Havok Physics. Requires a Havok Physics for Unity plugin and Unity 2022.2 or newer;
    /// see <c>docs/HavokPhysics.md</c>. Selected at runtime, falls back to PhysX when unavailable.
    /// </summary>
    Havok = 2,

    /// <summary>Jolt Physics via the JoltC C API plugin.</summary>
    Jolt = 3,

    /// <summary>
    /// BepuPhysics v2 through the managed BepuPhysics plugin, ported from the PigForge headless
    /// project's Bepu backend. Pure managed - it loads no native library; see
    /// <c>BepuPort/BepuBackend.cs</c>.
    /// </summary>
    Bepu = 4,
}

/// <summary>
/// A rigid-body simulation backend that drives the game's <see cref="Rigidbody"/> state.
/// </summary>
/// <remarks>
/// Implementations treat the Unity components as the authoritative game state facade: the game keeps
/// reading and writing <see cref="Rigidbody.position"/>, <c>velocity</c>, <c>mass</c> and so on, and
/// the backend mirrors that state into its native world each step and writes results back afterwards.
/// </remarks>
public interface IPhysicsBackend : IDisposable
{
    /// <summary>Which engine this backend implements.</summary>
    PhysicsEngine Engine { get; }

    /// <summary>
    /// Human-readable name used by diagnostics and the on-screen overlay.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Creates native bodies, shapes and joints for every live <see cref="Rigidbody"/> and
    /// <see cref="Collider"/> in the loaded scenes, and retires the ones that have gone away.
    /// Safe to call every frame.
    /// </summary>
    void Synchronize();

    /// <summary>Advances the simulation by <paramref name="delta"/> seconds and writes results back.</summary>
    void Step(float delta);

    /// <summary>Marks a body as awake in the native world.</summary>
    void Wake(Rigidbody body);

    /// <summary>Marks a body as asleep in the native world.</summary>
    void Sleep(Rigidbody body);

    /// <summary>Reports whether the native world considers <paramref name="body"/> asleep.</summary>
    bool IsSleeping(Rigidbody body);

    /// <summary>Re-evaluates collision filtering for the pair containing the two colliders.</summary>
    void Refilter(Collider a, Collider b);

    /// <summary>Returns the current hinge angle in degrees for the native counterpart of <paramref name="joint"/>.</summary>
    float HingeAngle(HingeJoint joint);
}

/// <summary>
/// Describes one entry in the engine switcher list without instantiating the backend, so the UI can
/// show availability (and the reason for unavailability) up front.
/// </summary>
public sealed class PhysicsEngineInfo
{
    public PhysicsEngineInfo(PhysicsEngine engine, string name, string detail, string reason, bool available)
    {
        Engine = engine;
        Name = name;
        Detail = detail;
        Reason = reason;
        Available = available;
    }

    public PhysicsEngine Engine { get; }
    public string Name { get; }

    /// <summary>Short status line shown beside the engine in the switcher.</summary>
    public string Detail { get; }

    /// <summary>
    /// Full explanation of why the engine cannot be selected, or <c>null</c> when it is available.
    /// <see cref="Detail"/> is the abbreviated form of this, sized for the switcher list.
    /// </summary>
    public string Reason { get; }

    public bool Available { get; }

    public override string ToString() => Available ? Name : Name + " (" + Detail + ")";
}
