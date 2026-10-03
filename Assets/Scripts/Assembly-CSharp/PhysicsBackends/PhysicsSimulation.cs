using UnityEngine;

/// <summary>
/// The one place that knows how to turn Unity's automatic physics stepping on and off.
/// </summary>
/// <remarks>
/// <para>
/// <c>Physics.autoSimulation</c> became obsolete in 2022.2 in favour of
/// <c>Physics.simulationMode</c>, and this project moved from 2021.3.45f2 to 2022.3.62f2 (see
/// <c>docs/EngineSwitcherHandover.md</c>). Routing every call through here keeps the intent readable
/// and compiles without an obsolete-API warning on either editor.
/// </para>
/// <para>
/// The two APIs are exact mirrors: Unity implements <c>autoSimulation</c> as
/// <c>simulationMode != SimulationMode.Script</c>, and its setter selects
/// <c>SimulationMode.FixedUpdate</c> for true and <c>SimulationMode.Script</c> for false. The bool
/// round-trip therefore collapses <c>SimulationMode.Update</c> onto <c>FixedUpdate</c> - both are
/// "automatic", and nothing in this project ever selects <c>Update</c>.
/// </para>
/// <para>
/// This is a load-bearing switch rather than a convenience: <c>INPhysicsManager.Awake</c> turns
/// automatic stepping off and only its own <c>FixedUpdate</c> calls <c>GamePhysics.Simulate</c>, so
/// the selected physics backend owns the step. The audits flip the same switch while they drive a
/// scene by hand.
/// </para>
/// </remarks>
public static class PhysicsSimulation
{
    /// <summary>
    /// True while Unity steps the world itself, which is the default
    /// (<c>SimulationMode.FixedUpdate</c>); false means only an explicit
    /// <c>Physics.Simulate</c> call advances it.
    /// </summary>
    public static bool Auto
    {
#if UNITY_2022_2_OR_NEWER
        get { return Physics.simulationMode != SimulationMode.Script; }
        set { Physics.simulationMode = value ? SimulationMode.FixedUpdate : SimulationMode.Script; }
#else
        get { return Physics.autoSimulation; }
        set { Physics.autoSimulation = value; }
#endif
    }
}
