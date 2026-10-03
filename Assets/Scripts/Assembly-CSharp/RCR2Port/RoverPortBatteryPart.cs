using System.Collections.Generic;
using UnityEngine;

namespace RCR2Port
{
    public class RoverPortBatteryPart : RoverPortPart, IFuelContainer
    {
        private float m_capacity;
        private float m_charge;
        private bool m_chargeInitialized;

        public int FuelComponentIndex { get; set; }

        public float SupplyFuelAmount
        {
            get { return Mathf.Max(0f, m_charge); }
        }

        public float RefuelingAmount
        {
            get { return 0f; }
        }

        public override void Awake()
        {
            base.Awake();
            RestoreBatteryData();
        }

        public override void Configure(RoverPortPartData data, int partIndex, Dictionary<string, Transform> nodes, RoverPortIconSprite icon)
        {
            base.Configure(data, partIndex, nodes, icon);
            RestoreBatteryData();
        }

        public override void EnsureRigidbody()
        {
            RestoreBatteryData();
            base.EnsureRigidbody();
        }

        private void RestoreBatteryData()
        {
            if (m_portData == null || m_portData.motion == null)
            {
                return;
            }
            // Only parts the source marks as a battery carry a charge: the starter and terra cockpits
            // hold the driver's onboard battery (capacity 100), the other cockpits hold none, and a
            // part with no capacity must not turn into an empty container.
            bool isBattery = string.Equals(m_portData.motion.kind, "battery", System.StringComparison.Ordinal);
            m_capacity = isBattery ? Mathf.Max(1f, m_portData.motion.capacity) : 0f;
            if (!m_chargeInitialized)
            {
                m_charge = m_capacity;
                m_chargeInitialized = true;
            }
            if (isBattery)
            {
                m_powerConsumption = 0f;
                m_enginePower = 0f;
            }
        }

        public void SupplyFuel(float amount)
        {
            if (float.IsNaN(amount) || float.IsInfinity(amount))
                return;
            m_charge = Mathf.Max(0f, m_charge - Mathf.Max(0f, amount));
        }

        public void Refuel(float amount)
        {
            if (float.IsNaN(amount) || float.IsInfinity(amount))
                return;
            m_charge = Mathf.Min(m_capacity, m_charge + Mathf.Max(0f, amount));
        }

        public IEnumerable<BasePart> GetConnectedParts()
        {
            if (contraption == null || ConnectedComponent < 0)
            {
                yield break;
            }
            foreach (BasePart part in contraption.Parts)
            {
                if (part == null || part == this || part.ConnectedComponent != ConnectedComponent)
                {
                    continue;
                }
                if (part is IFuelConsumer || part is IFuelContainer)
                {
                    yield return part;
                }
            }
        }

        public void DrainFallback(float amount)
        {
            m_charge = Mathf.Max(0f, m_charge - Mathf.Max(0f, amount));
        }
    }
}

