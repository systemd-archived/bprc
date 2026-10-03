using System.Collections.Generic;
using UnityEngine;

namespace RCR2Port
{
    /// <summary>
    /// An RC3 battery as a BPLE power source.
    ///
    /// **Why it is not <see cref="RoverPortBatteryPart"/>.** RC3 models a battery
    /// as *charge*: the source's `capacity` drains as the vehicle drives, and the
    /// port reproduced that by implementing <see cref="IFuelContainer"/> so
    /// <c>FuelSystem</c> could hand the charge to the driving parts. BPLE has no
    /// such model. It drives its wheels from the contraption's engine power pool:
    /// <c>Contraption.GetEnginePowerFactor</c> divides the component's summed
    /// <c>m_enginePower</c> by its summed <c>m_powerConsumption</c>, and the
    /// native <c>MotorWheel</c> reads that factor. So an RC3 battery supplies
    /// engine power, and the wheels it feeds are ordinary
    /// <see cref="RoverPortPoweredPart"/>s that already report BPLE-scale draw
    /// (<see cref="RoverPortPoweredPart.MotorWheelPowerConsumption"/>, the same
    /// 100 a native MotorWheel draws).
    ///
    /// RCR2's own batteries keep <see cref="RoverPortBatteryPart"/> and its charge
    /// model: that port was built and audited against the fuel path
    /// (<c>RoverPortInteractionAudit</c> drives a wheel off `part_battery_small`),
    /// so only the RC3 catalog moves to the engine-power model. The two are told
    /// apart by index range - see <c>RC3PortCatalog.IsRC3Index</c>.
    ///
    /// A part whose `m_enginePower` is positive is an engine as far as BPLE is
    /// concerned (<c>BasePart.IsEngine</c>, and the component's <c>hasEngine</c>
    /// flag), which is exactly what a power source should look like. Engine power
    /// is summed even while a part is disabled, so nothing else is needed to make
    /// the battery count.
    /// </summary>
    public class RoverPortPowerCellPart : RoverPortPart
    {
        /// <summary>
        /// Engine power one unit of source capacity is worth.
        ///
        /// The source game has no engine-power concept at all, so this is the
        /// only invented number in the port. It is anchored on BPLE's own scale:
        /// a native MotorWheel draws 100 (<c>m_powerConsumption</c>), so 100
        /// engine power drives one wheel at full factor. RC3 capacities run 30-90,
        /// which at this rate is roughly one to three and a half wheels -
        /// deliberately comparable to the small vehicles the source builds.
        /// Tune this single constant to rebalance the whole catalog.
        /// </summary>
        public const float EnginePowerPerCapacity = 4f;

        /// <summary>Floor so a degenerate capacity still reads as a power source.</summary>
        public const float MinimumEnginePower = 1f;

        /// <summary>The source capacity this part's output was derived from.</summary>
        public float Capacity { get; private set; }

        public override void Awake()
        {
            base.Awake();
            ApplyPowerOutput();
        }

        public override void Configure(RoverPortPartData data, int partIndex, Dictionary<string, Transform> nodes, RoverPortIconSprite icon)
        {
            base.Configure(data, partIndex, nodes, icon);
            ApplyPowerOutput();
        }

        public override void EnsureRigidbody()
        {
            ApplyPowerOutput();
            base.EnsureRigidbody();
        }

        /// <summary>Engine power a battery of this source capacity supplies.</summary>
        public static float EnginePowerForCapacity(float capacity)
        {
            return Mathf.Max(MinimumEnginePower, Mathf.Max(0f, capacity) * EnginePowerPerCapacity);
        }

        /// <summary>
        /// Publishes the battery's output. <see cref="RoverPortPart.Configure"/>
        /// zeroes <c>m_enginePower</c> for every ported part, so this has to run
        /// again afterwards - and on the clone a placement makes, which is why it
        /// is not folded into <c>Configure</c> alone.
        /// </summary>
        private void ApplyPowerOutput()
        {
            if (m_portData == null || m_portData.motion == null ||
                !string.Equals(m_portData.motion.kind, "battery", System.StringComparison.Ordinal))
            {
                return;
            }
            Capacity = Mathf.Max(0f, m_portData.motion.capacity);
            m_enginePower = EnginePowerForCapacity(Capacity);
            // A source does not draw: it must not be counted as a consumer.
            m_powerConsumption = 0f;
        }
    }
}
