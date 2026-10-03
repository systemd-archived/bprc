using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Box3DPortAudit
{
    private static readonly List<string> Results = new List<string>();
    private static void Check(bool condition, string message)
    {
        Results.Add((condition ? "PASS " : "FAIL ") + message);
        if (!condition) throw new Exception(message);
    }
    private static Rigidbody Cube(string name, Vector3 position, bool kinematic = false)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name; go.transform.position = position;
        var body = go.AddComponent<Rigidbody>(); body.isKinematic = kinematic;
        return body;
    }
    private static void Steps(int count)
    {
        for (int i = 0; i < count; i++)
        {
            GamePhysics.Simulate(0.02f);
            if (!GamePhysics.IsBox3D) throw new Exception("Unexpected fallback: " + GamePhysics.LastError);
        }
    }

    public static void Run()
    {
        bool auto = PhysicsSimulation.Auto;
        int exit = 0;
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Box3DMeshCacheBaker.Bake();
            PhysicsSimulation.Auto = false;
            Time.fixedDeltaTime = 0.02f;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Box3D audit ground"; ground.transform.position = new Vector3(0, -0.5f, 0);
            ground.transform.localScale = new Vector3(40, 1, 40);
            var cube = Cube("falling", new Vector3(0, 3, 0));
            var receiver = cube.gameObject.AddComponent<Box3DAuditReceiver>();
            Check(GamePhysics.SelectBox3D(true), "native backend loads in Unity 2021.3");
            Steps(160);
            Check(Mathf.Abs(cube.position.y - 0.5f) < 0.12f, "dynamic cube rests on converted ground: " + cube.position);
            Check(receiver.enters > 0 && receiver.stays > 0 && receiver.points > 0, "contact enter/stay payload reaches scripts");
            Check(Physics.Raycast(cube.position + Vector3.up * 2, Vector3.down, out var hit, 4) && hit.rigidbody == cube, "Unity raycasts see Box3D positions");
            cube.EngineAddForce(Vector3.up * 5, ForceMode.VelocityChange);
            Steps(5);
            Check(cube.position.y > 0.8f, "legacy force calls drive Box3D");
            Check(receiver.exits > 0, "contact exit reaches scripts");

            var trigger = GameObject.CreatePrimitive(PrimitiveType.Cube);
            trigger.name = "sensor"; trigger.transform.position = new Vector3(5, 2, 0);
            trigger.GetComponent<Collider>().isTrigger = true;
            var visitor = Cube("visitor", new Vector3(5, 2, 0)); visitor.useGravity = false;
            var triggerReceiver = visitor.gameObject.AddComponent<Box3DAuditReceiver>();
            Steps(4);
            Check(triggerReceiver.triggerEnters > 0 && triggerReceiver.triggerStays > 0, "sensor enter/stay");
            visitor.position += Vector3.right * 3;
            Steps(3);
            Check(triggerReceiver.triggerExits > 0, "sensor exit after teleport");

            var ignored = Cube("ignored", new Vector3(-5, 2, 0));
            GamePhysics.IgnoreCollision(ignored.GetComponent<Collider>(), ground.GetComponent<Collider>());
            Steps(70);
            Check(ignored.position.y < -2, "per-collider IgnoreCollision is honored");
            UnityEngine.Object.DestroyImmediate(ignored.gameObject);
            Steps(1);

            var pivot = Cube("pivot", new Vector3(10, 5, 0), true);
            var pendulum = Cube("pendulum", new Vector3(10, 3, 0));
            var hinge = pendulum.gameObject.AddComponent<HingeJoint>();
            hinge.connectedBody = pivot; hinge.axis = Vector3.forward; hinge.anchor = Vector3.up;
            hinge.useMotor = true; hinge.motor = new JointMotor { force = 100, targetVelocity = 90 };
            Vector3 localAnchor = hinge.connectedAnchor;
            Steps(100);
            float separation = Vector3.Distance(pendulum.transform.TransformPoint(hinge.anchor), pivot.transform.TransformPoint(localAnchor));
            Check(separation < 0.15f, "hinge anchors remain connected: " + separation);
            Check(Mathf.Abs(pendulum.angularVelocity.z) > 0.2f, "hinge motor drives rotation");
            var fixedBody = Cube("welded", new Vector3(-10, 3, 0));
            var fixedPivot = Cube("fixed pivot", new Vector3(-10, 5, 0), true);
            var weld = fixedBody.gameObject.AddComponent<FixedJoint>(); weld.connectedBody = fixedPivot;
            Vector3 initial = fixedBody.position;
            Steps(100);
            Check(Vector3.Distance(initial, fixedBody.position) < 0.1f, "fixed joint remains stable");

            var dynamic = Cube("spawned", new Vector3(0, 8, 0));
            Steps(5);
            Check(dynamic.position.y < 7.99f, "runtime body discovery");
            dynamic.gameObject.SetActive(false); Steps(1);
            dynamic.gameObject.SetActive(true); Steps(5);
            Check(GamePhysics.IsBox3D, "disable/re-enable rebuilds native objects");

            var locked = Cube("locked axes", new Vector3(15, 2, 0)); locked.useGravity = false;
            locked.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
            locked.EngineAddForce(new Vector3(0, 0, 20), ForceMode.Impulse);
            locked.EngineAddTorque(new Vector3(20, 20, 0), ForceMode.Impulse);
            Steps(10);
            Check(Mathf.Abs(locked.position.z) < 0.001f && Quaternion.Angle(locked.rotation, Quaternion.identity) < 0.1f, "2.5D frozen axes stay locked");
            var noncolliding = Cube("detect collisions off", new Vector3(18, 2, 0)); noncolliding.detectCollisions = false;
            Steps(60);
            Check(noncolliding.position.y < -2, "detectCollisions=false is honored from first native shape creation");
            var resizable = Cube("resizable", new Vector3(-15, 3, 0));
            Steps(90);
            resizable.GetComponent<BoxCollider>().size = new Vector3(1, 2, 1);
            Steps(90);
            Check(Mathf.Abs(resizable.position.y - 1f) < 0.15f, "runtime collider resizing rebuilds native geometry");
            GamePhysics.IgnoreCollision(resizable.GetComponent<Collider>(), ground.GetComponent<Collider>(), true);
            Steps(60);
            Check(resizable.position.y < -2, "IgnoreCollision removes an existing contact");

            Vector3 before = cube.position;
            Check(GamePhysics.SelectBox3D(false) && !GamePhysics.IsBox3D, "switch back to PhysX");
            Check(Vector3.Distance(before, cube.position) < 0.001f, "switch preserves body position");
            cube.position = new Vector3(-3, 4, 0); cube.velocity = Vector3.zero;
            for (int i = 0; i < 100; i++) GamePhysics.Simulate(0.02f);
            Check(Mathf.Abs(cube.position.y - 0.5f) < 0.15f, "PhysX simulation still works after switch");
            var settings = (StarSeaSettings)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(StarSeaSettings));
            settings.UseBox3D = true;
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(settings);
            Check(Newtonsoft.Json.Linq.JObject.Parse(json).Value<bool>("UseBox3D"), "Box3D selection is included in settings JSON");
            var restored = (StarSeaSettings)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(StarSeaSettings));
            Newtonsoft.Json.JsonConvert.PopulateObject("{\"UseBox3D\":true}", restored);
            Check(restored.UseBox3D, "Box3D selection reloads from settings JSON");
            AuditSpring();
            GamePhysics.Shutdown();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AuditPortedWheel();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var unsupported = Cube("unsupported character joint", Vector3.zero);
            unsupported.gameObject.AddComponent<CharacterJoint>();
            Check(!GamePhysics.SelectBox3D(true) && !GamePhysics.IsBox3D && GamePhysics.LastError.Contains("CharacterJoint"),
                "unsupported constraints fail visibly and retain PhysX");
            UnityEngine.Object.DestroyImmediate(unsupported.gameObject);
            Check(GamePhysics.SelectBox3D(true), "backend can be enabled again after failed initialization");
        }
        catch (Exception error) { Results.Add(error.ToString()); Debug.LogException(error); exit = 1; }
        finally
        {
            GamePhysics.Shutdown(); PhysicsSimulation.Auto = auto;
            Directory.CreateDirectory("Builds/QA");
            File.WriteAllLines("Builds/QA/box3d-audit.txt", Results);
        }
        EditorApplication.Exit(exit);
    }

    private static void AuditPortedWheel()
    {
        RCR2Port.RoverPortExtraCatalog.Load();
        var prototype = RCR2Port.RoverPortCatalog.Instance.GetPart("part_mecanum_wheel");
        Check(prototype != null, "actual mecanum wheel prototype exists");
        var wheel = UnityEngine.Object.Instantiate(prototype);
        wheel.transform.SetParent(null, false); wheel.SetActive(true);
        var part = wheel.GetComponent<RCR2Port.RoverPortPoweredPart>();
        part.Awake(); part.EnsureRigidbody();
        wheel.GetComponent<Rigidbody>().isKinematic = true;
        foreach (var body in wheel.GetComponentsInChildren<Rigidbody>()) { body.useGravity = false; body.detectCollisions = false; }
        var springs = wheel.GetComponentsInChildren<SpringJoint>();
        var distances = new float[springs.Length];
        for (int i = 0; i < springs.Length; i++) distances[i] = Vector3.Distance(springs[i].transform.position, springs[i].connectedBody.position);
        Check(GamePhysics.SelectBox3D(true), "actual wheel constraints convert to Box3D");
        Steps(120);
        float drift = 0;
        for (int i = 0; i < springs.Length; i++)
            drift = Mathf.Max(drift, Mathf.Abs(Vector3.Distance(springs[i].transform.position, springs[i].connectedBody.position) - distances[i]));
        Check(drift < 0.15f, "actual wheel stays assembled at rest; radial drift=" + drift);
        var drive = typeof(RCR2Port.RoverPortPoweredPart).GetMethod("DriveWheels", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        drive.Invoke(part, new object[] { 0.1f, 1f });
        Steps(120);
        drift = 0;
        for (int i = 0; i < springs.Length; i++)
            drift = Mathf.Max(drift, Mathf.Abs(Vector3.Distance(springs[i].transform.position, springs[i].connectedBody.position) - distances[i]));
        Check(drift < 0.3f, "actual wheel stays assembled with motor; radial drift=" + drift);
        GamePhysics.Shutdown();
        UnityEngine.Object.DestroyImmediate(wheel);
    }

    private static void AuditSpring()
    {
        GamePhysics.Shutdown();
        var pivot = Cube("spring pivot", new Vector3(0, 30, 0), true);
        var bob = Cube("spring bob", new Vector3(0, 28, 0));
        bob.useGravity = false; bob.detectCollisions = false;
        var spring = bob.gameObject.AddComponent<SpringJoint>();
        spring.connectedBody = pivot;
        spring.autoConfigureConnectedAnchor = false;
        spring.anchor = Vector3.zero; spring.connectedAnchor = Vector3.zero;
        spring.minDistance = spring.maxDistance = 2;
        spring.spring = 100; spring.damper = 10;
        bob.position = new Vector3(0, 27, 0);
        for (int i = 0; i < 150; i++) Physics.Simulate(0.02f);
        float physxDistance = Vector3.Distance(bob.position, pivot.position);
        bob.position = new Vector3(0, 27, 0); bob.velocity = Vector3.zero;
        Check(GamePhysics.SelectBox3D(true), "spring comparison initializes");
        Steps(150);
        float boxDistance = Vector3.Distance(bob.position, pivot.position);
        Check(Mathf.Abs(physxDistance - boxDistance) < 0.1f, "spring rest distance matches PhysX: " + physxDistance + " / " + boxDistance);
        GamePhysics.Shutdown();
        UnityEngine.Object.DestroyImmediate(bob.gameObject); UnityEngine.Object.DestroyImmediate(pivot.gameObject);
    }
}

public sealed class Box3DAuditReceiver : MonoBehaviour
{
    public int enters, stays, exits, points, triggerEnters, triggerStays, triggerExits;
    public void OnPhysicsCollisionEnter(PhysicsCollision collision) { enters++; points += collision.contactCount; }
    public void OnPhysicsCollisionStay(PhysicsCollision collision) { stays++; }
    public void OnPhysicsCollisionExit(PhysicsCollision collision) { exits++; }
    public void OnCollisionEnter(Collision collision) { OnPhysicsCollisionEnter(new PhysicsCollision(collision)); }
    public void OnTriggerEnter(Collider other) { triggerEnters++; }
    public void OnTriggerStay(Collider other) { triggerStays++; }
    public void OnTriggerExit(Collider other) { triggerExits++; }
}
