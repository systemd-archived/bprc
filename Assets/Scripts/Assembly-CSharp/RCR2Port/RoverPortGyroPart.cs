using System.Collections.Generic;
using UnityEngine;

namespace RCR2Port
{
    /// <summary>
    /// The RC1 gyroscope part: a plain <see cref="RoverPortPart"/> carrying a
    /// <see cref="RoverPortGyro"/>, plus the two numbers that gyro runs on as per-part parameters.
    /// </summary>
    /// <remarks>
    /// The gyro's ceiling and torque come from the source's <c>PartSPrGyro.properties</c> (220 and 90),
    /// but the source applies them to a <c>Rigidbody2D</c> and this port to the project's 3D bodies, so
    /// the authored numbers are a starting point rather than a guarantee. Exposing them through the same
    /// "extra parameter" hook the long-press panel uses (see <c>INPartParameterPanel</c> and
    /// <c>BasePart.CollectExtraParams</c>) is how they get tuned without a rebuild - exactly like the
    /// ported wheels' radius. Values are remembered on the part, so a saved contraption keeps them.
    /// </remarks>
    public class RoverPortGyroPart : RoverPortPart
    {
        public const string MaxAngularVelocityParam = "GyroMaxAngularVelocity";
        public const string TorqueParam = "GyroTorque";

        private RoverPortGyro m_gyro;

        /// <summary>The gyro component, found on first use (the catalog adds it after the part).</summary>
        public RoverPortGyro Gyro
        {
            get
            {
                if (m_gyro == null) m_gyro = GetComponent<RoverPortGyro>();
                return m_gyro;
            }
        }

        public override void CollectExtraParams(List<string> keys)
        {
            base.CollectExtraParams(keys);
            keys.Add(MaxAngularVelocityParam);
            keys.Add(TorqueParam);
        }

        public override bool ApplyExtraParam(string key, float value)
        {
            if (key == MaxAngularVelocityParam)
            {
                if (value > 0f && Gyro != null) Gyro.MaxAngularVelocity = value;
                return true;
            }
            if (key == TorqueParam)
            {
                if (Gyro != null) Gyro.Torque = value;
                return true;
            }
            return base.ApplyExtraParam(key, value);
        }

        public override bool TryGetExtraParamValue(string key, out float value)
        {
            value = 0f;
            if (key == MaxAngularVelocityParam)
            {
                // The panel shows the value in the degrees per second the source authored, so a number
                // typed there is the one printed in the APK rather than a radian conversion of it.
                value = (Gyro != null ? Gyro.MaxAngularVelocity : RoverPortGyro.SourceMaxAngularVelocity * Mathf.Deg2Rad)
                        * Mathf.Rad2Deg;
                return true;
            }
            if (key == TorqueParam)
            {
                value = Gyro != null ? Gyro.Torque : RoverPortGyro.SourceTorque;
                return true;
            }
            return base.TryGetExtraParamValue(key, out value);
        }
    }
}
