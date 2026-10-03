using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using RCR2Port;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Checks what the port actually builds for a driven part once the contraption starts: the rigidbody
/// chain, the joints between it, the colliders, the friction they run on and the bodies the drive
/// reaches. Tracked parts are the interesting case - RCR2 drives them through a chain of hinge linked
/// bodies, one per belt link, so their traction comes from several contact patches instead of a
/// single tyre.
///
/// Reports: Builds/QA/physics-setup.csv
/// Run with: Unity -executeMethod RoverPortPhysicsAudit.Run
/// </summary>
public static class RoverPortPhysicsAudit
{
    public static void Run()
    {
        string output = Path.GetFullPath("Builds/QA");
        Directory.CreateDirectory(output);
        var rows = new List<string>
        {
            "part_id,motion,passive,part_type,catalog_bodies,catalog_joints,catalog_colliders,built_rigidbodies,built_joints," +
            "built_colliders,friction,drive_bodies,drive_force_per_contact,motor_wheels_per_contact"
        };
        var spotlight = new List<string>();
        var failures = new List<string>();
        try
        {
            foreach (GameObject prototype in RoverPortCatalog.Instance.Parts)
            {
                var part = prototype.GetComponent<RoverPortPart>();
                if (part == null)
                {
                    continue;
                }
                RoverPortPartData data = GetField<RoverPortPartData>(part, "m_portData");
                if (data == null || data.motion == null || data.motion.kind != "wheel")
                {
                    continue;
                }
                var clone = UnityEngine.Object.Instantiate(prototype);
                try
                {
                    clone.transform.SetParent(null, false);
                    clone.transform.position = Vector3.zero;
                    clone.SetActive(true);
                    var instance = clone.GetComponent<RoverPortPart>();
                    instance.EnsureRigidbody();

                    Rigidbody[] bodies = clone.GetComponentsInChildren<Rigidbody>(true);
                    Joint[] joints = clone.GetComponentsInChildren<Joint>(true);
                    Collider[] colliders = clone.GetComponentsInChildren<Collider>(true);
                    float friction = 0f;
                    foreach (Collider collider in colliders)
                    {
                        if (collider.sharedMaterial != null)
                        {
                            friction = Mathf.Max(friction, collider.sharedMaterial.dynamicFriction);
                        }
                    }
                    var drive = GetField<List<Rigidbody>>(instance, "m_driveBodies");
                    float forcePerContact = 0f;
                    float motorWheels = 0f;
                    var powered = instance as RoverPortPoweredPart;
                    if (powered != null)
                    {
                        forcePerContact = powered.DriveForcePerWheel;
                        motorWheels = RoverPortPoweredPart.DriveForceInMotorWheels;
                    }
                    rows.Add(string.Join(",", data.id, data.motion.kind, data.motion.passive.ToString(), instance.Type.ToString(),
                        Count(data.bodies), Count(data.joints), Count(data.colliders),
                        bodies.Length, joints.Length, colliders.Length,
                        friction.ToString("0.000", CultureInfo.InvariantCulture),
                        drive == null ? 0 : drive.Count,
                        forcePerContact.ToString("0.0", CultureInfo.InvariantCulture),
                        motorWheels.ToString("0.00", CultureInfo.InvariantCulture)));
                    // A wheel the source builds without a motor must not drive: no powered component, no
                    // engine power draw, no fuel demand, and the plain wheel family in the part list.
                    // Its source bodies and joints stay, so the ground rolls it (see
                    // Tools/RCR2/PatchPassiveWheels.py).
                    if (data.motion.passive)
                    {
                        if (powered != null) failures.Add(data.id + ": passive wheel is a RoverPortPoweredPart");
                        if (instance.m_powerConsumption != 0f) failures.Add(data.id + ": passive wheel draws " + instance.m_powerConsumption + " engine power");
                        if (instance is IFuelConsumer) failures.Add(data.id + ": passive wheel is a fuel consumer");
                        if (instance.Type != BasePart.PartType.NormalWheel) failures.Add(data.id + ": passive wheel is typed " + instance.Type);
                        if (bodies.Length == 0 || joints.Length == 0) failures.Add(data.id + ": passive wheel lost its rig");
                        spotlight.Add(string.Format(CultureInfo.InvariantCulture,
                            "passive wheel {0}: bodies {1}/{2} joints {3}/{4} friction {5:0.000} driveBodies {6} force {7:0.0} N",
                            data.id, bodies.Length, Count(data.bodies), joints.Length, Count(data.joints),
                            friction, drive == null ? 0 : drive.Count, forcePerContact));
                    }
                    else if (powered == null)
                    {
                        failures.Add(data.id + ": driven wheel has no powered component");
                    }
                    if (data.id == "part_sand_wheel_track")
                    {
                        spotlight.Add(string.Format(CultureInfo.InvariantCulture,
                            "track: bodies {0}/{1} joints {2}/{3} colliders {4}/{5} friction {6:0.000} driveBodies {7} force {8:0.0} N per contact ({9:0.00} MotorWheels)",
                            bodies.Length, Count(data.bodies), joints.Length, Count(data.joints),
                            colliders.Length, Count(data.colliders), friction,
                            drive == null ? 0 : drive.Count, forcePerContact, motorWheels));
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(clone);
                }
            }
        }
        finally
        {
            File.WriteAllLines(Path.Combine(output, "physics-setup.csv"), rows);
        }
        foreach (string line in spotlight)
        {
            Debug.Log("RCR2_PHYSICS_SETUP: " + line);
        }
        if (failures.Count > 0)
        {
            throw new Exception(string.Join("\n", failures));
        }
        Debug.Log("RCR2_PHYSICS_SETUP: " + (rows.Count - 1) + " driven parts measured");
    }

    private static int Count(Array array)
    {
        return array == null ? 0 : array.Length;
    }

    private static T GetField<T>(object target, string field) where T : class
    {
        FieldInfo info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
        return info == null ? null : info.GetValue(target) as T;
    }
}
