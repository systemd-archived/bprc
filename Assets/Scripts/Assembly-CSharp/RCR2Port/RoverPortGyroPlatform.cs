using System;
using System.Collections.Generic;
using UnityEngine;

namespace RCR2Port
{
    /// <summary>
    /// Port of RoverCraft 2's RoverPartGyroplatformSpecialPhysics plus its two
    /// HingeJointMuscle servos: the stick and platform of the gyro stabilizer are
    /// actively driven toward their serialized target angle (straight), so parts
    /// connected above rest on a level plate instead of being knocked around by a
    /// freely swinging one - the original reason the port flopped around and dragged
    /// neighbours into rotating. Torque follows the muscle's angle-error curve and
    /// scales up with the mass connected above the part, mirroring the original's
    /// UpdatePlatformPower / massMultiplier behaviour.
    /// </summary>
    public class RoverPortGyroPlatform : MonoBehaviour
    {
        /// <summary>PD damping factor relative to critical damping.</summary>
        private const float DampingRatio = 0.9f;

        /// <summary>Extra holding torque per connected mass unit (see forceScale).</summary>
        private const float MassTorqueScale = 0.005f;

        private sealed class Muscle
        {
            public string nodeName;
            public float targetAngle;
            public float torquePerRadian;
            public Rigidbody body;
            public Rigidbody reference;
        }

        private readonly List<Muscle> m_muscles = new List<Muscle>();
        private RoverPortPart m_part;
        private float m_massMultiplier = 20f;
        private float m_connectedMass;
        private float m_nextMassRefresh;
        private bool m_bodiesResolved;

        public void Configure(RoverPortPart part, string stickNode, string platformNode,
            RoverPortMuscleData stick, RoverPortMuscleData platform, float massMultiplier)
        {
            m_part = part;
            m_massMultiplier = Mathf.Max(0f, massMultiplier);
            m_muscles.Clear();
            m_bodiesResolved = false;
            AddMuscle(stickNode, stick);
            AddMuscle(platformNode, platform);
        }

        private void AddMuscle(string nodeName, RoverPortMuscleData data)
        {
            if (string.IsNullOrEmpty(nodeName) || data == null || !data.enabled)
            {
                return;
            }
            float torquePerDegree = CurvePerDegree(data.torque_curve);
            m_muscles.Add(new Muscle
            {
                nodeName = nodeName,
                targetAngle = data.target_angle,
                // force 50, multiplier 500, curve 2.78 per degree -> ~70 N·m per radian
                torquePerRadian = data.force * data.torque_multiplier * torquePerDegree / 1000f
                    * Mathf.Deg2Rad,
            });
        }

        private static float CurvePerDegree(RoverPortCurveKeyData[] keys)
        {
            if (keys == null || keys.Length == 0)
            {
                return 1f;
            }
            if (keys.Length == 1)
            {
                return keys[0].value / Mathf.Max(0.001f, keys[0].time);
            }
            RoverPortCurveKeyData last = keys[keys.Length - 1];
            return last.value / Mathf.Max(0.001f, last.time);
        }

        private void FixedUpdate()
        {
            if (m_part == null || m_muscles.Count == 0)
            {
                return;
            }
            Contraption contraption = m_part.contraption;
            if (contraption == null || !contraption.IsRunning)
            {
                return;
            }
            if (!m_bodiesResolved)
            {
                m_bodiesResolved = ResolveBodies();
                if (!m_bodiesResolved)
                {
                    return;
                }
            }
            if (Time.time >= m_nextMassRefresh)
            {
                RefreshConnectedMass();
                m_nextMassRefresh = Time.time + 0.5f;
            }
            float forceScale = Mathf.Clamp(1f + m_connectedMass * m_massMultiplier * MassTorqueScale, 1f, 4f);
            foreach (Muscle muscle in m_muscles)
            {
                if (muscle.body == null || muscle.reference == null)
                {
                    m_bodiesResolved = false;
                    continue;
                }
                float error = Mathf.Deg2Rad * Wrap180(
                    muscle.targetAngle - Vector3.SignedAngle(muscle.reference.transform.up, muscle.body.transform.up, Vector3.forward));
                float kp = muscle.torquePerRadian * forceScale;
                float inertia = PlanarInertia(muscle.body);
                float kd = DampingRatio * 2f * Mathf.Sqrt(Mathf.Max(0f, kp * inertia));
                float limit = kp * Mathf.PI;
                float command = Mathf.Clamp(kp * error - kd * muscle.body.angularVelocity.z, -limit, limit);
                muscle.body.EngineAddRelativeTorque(0f, 0f, command);
            }
        }

        private bool ResolveBodies()
        {
            Rigidbody root = m_part.GetComponent<Rigidbody>();
            bool complete = m_muscles.Count > 0;
            foreach (Muscle muscle in m_muscles)
            {
                if (muscle.body == null)
                {
                    Transform node = FindChildByName(m_part.transform, muscle.nodeName);
                    if (node == null)
                    {
                        complete = false;
                        continue;
                    }
                    muscle.body = node.GetComponent<Rigidbody>();
                    if (muscle.body == null)
                    {
                        complete = false;
                        continue;
                    }
                }
                if (muscle.reference == null)
                {
                    HingeJoint hinge = muscle.body.GetComponent<HingeJoint>();
                    muscle.reference = hinge != null && hinge.connectedBody != null
                        ? hinge.connectedBody
                        : root;
                    if (muscle.reference == null)
                    {
                        complete = false;
                    }
                }
            }
            return complete;
        }

        private void RefreshConnectedMass()
        {
            m_connectedMass = 0f;
            Contraption contraption = m_part.contraption;
            if (contraption == null || m_part.ConnectedComponent < 0)
            {
                return;
            }
            float platformY = m_part.transform.position.y + 0.9f;
            foreach (BasePart other in contraption.Parts)
            {
                if (other == null || other == m_part || other.ConnectedComponent != m_part.ConnectedComponent)
                {
                    continue;
                }
                if (other.transform.position.y < platformY)
                {
                    continue;
                }
                Rigidbody body = other.rigidbody;
                if (body != null)
                {
                    m_connectedMass += body.mass;
                }
            }
        }

        private static float PlanarInertia(Rigidbody body)
        {
            Vector3 tensor = body.inertiaTensor;
            return Mathf.Max(0.01f, Mathf.Max(tensor.x, tensor.y));
        }

        private static float Wrap180(float angle)
        {
            angle %= 360f;
            if (angle > 180f)
            {
                angle -= 360f;
            }
            if (angle < -180f)
            {
                angle += 360f;
            }
            return angle;
        }

        private static Transform FindChildByName(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildByName(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }
    }
}
