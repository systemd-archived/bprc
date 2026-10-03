using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Box3DDenseVehicleAudit
{
    private sealed class Part
    {
        public int type, variant, x, y;
        public Rigidbody body;
    }
    private static readonly List<string> results = new List<string>();
    private static int failures;
    private static readonly BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
    private static Dictionary<int, Box3D.JointId> JointIds()
    {
        var backend = typeof(GamePhysics).GetField("backend", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        var result = new Dictionary<int, Box3D.JointId>();
        if (backend == null) return result;
        foreach (DictionaryEntry item in (IDictionary)typeof(Box3DBackend).GetField("joints", flags).GetValue(backend))
            result[(int)item.Key] = ((Box3D.Joint)item.Value.GetType().GetField("native").GetValue(item.Value)).Id;
        return result;
    }
    private static void Clear()
    {
        GamePhysics.Shutdown();
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects()) UnityEngine.Object.DestroyImmediate(root);
    }
    private static void Connect(Part a, Part b, bool extra, List<Joint> joints)
    {
        Joint j;
        if (!extra && (a.type == 8 || a.type == 10 || b.type == 8 || b.type == 10))
        {
            var h = a.body.gameObject.AddComponent<HingeJoint>();
            h.autoConfigureConnectedAnchor = false;
            h.anchor = a.body.transform.InverseTransformPoint(b.body.position) * 0.5f;
            h.connectedAnchor = b.body.transform.InverseTransformPoint(a.body.position) * 0.5f;
            h.axis = Vector3.forward; h.useLimits = true;
            h.limits = new JointLimits { min = -0.1f, max = 0.1f }; j = h;
        }
        else j = a.body.gameObject.AddComponent<FixedJoint>();
        j.connectedBody = b.body; j.breakForce = extra ? 600 : Strength(a) + Strength(b);
        j.enablePreprocessing = extra; joints.Add(j);
    }
    private static float Strength(Part p) => p.type == 1 ? 125 : p.type == 8 || p.type == 10 ? 900 : 600;
    private static void RunCase(bool box, bool driven)
    {
        Clear();
        var parts = new List<Part>(); var joints = new List<Joint>();
        foreach (var line in File.ReadAllLines("Assets/Editor/Box3DDenseVehicle.csv"))
        {
            var v = Array.ConvertAll(line.Split(','), int.Parse);
            var p = new Part { type = v[0], variant = v[1], x = v[2], y = v[3] };
            string kind = p.type == 6 ? "MetalFrame" : p.type == 19 ? "EngineSmall" : p.type == 1 ? "Pig" : p.type == 8 ? "NormalWheel" : "MotorWheel";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObject/Part_" + kind + "_" + (p.variant + 1).ToString("00") + "_SET.prefab");
            if (!prefab) throw new Exception("Missing " + kind);
            var go = new GameObject(kind + " " + p.x + "," + p.y);
            go.transform.position = new Vector3(-837 + p.x, -10 + p.y, -(p.x + 2 * p.y) / 100000f);
            foreach (var c in prefab.GetComponentsInChildren<Collider>())
            {
                var child = new GameObject(c.name); child.transform.SetParent(go.transform, false);
                child.transform.localPosition = prefab.transform.InverseTransformPoint(c.transform.position);
                child.transform.localRotation = Quaternion.Inverse(prefab.transform.rotation) * c.transform.rotation;
                child.transform.localScale = c.transform.lossyScale;
                Collider clone;
                if (c is BoxCollider b) { var n = child.AddComponent<BoxCollider>(); n.size = b.size; n.center = b.center; clone = n; }
                else if (c is SphereCollider s) { var n = child.AddComponent<SphereCollider>(); n.radius = s.radius; n.center = s.center; clone = n; }
                else if (c is CapsuleCollider cap) { var n = child.AddComponent<CapsuleCollider>(); n.radius = cap.radius; n.height = cap.height; n.direction = cap.direction; n.center = cap.center; clone = n; }
                else throw new Exception("Unsupported fixture collider " + c.GetType());
                clone.sharedMaterial = c.sharedMaterial; clone.isTrigger = c.isTrigger;
            }
            p.body = go.AddComponent<Rigidbody>(); p.body.mass = p.type == 19 ? 0.25f : p.type == 8 ? 0.75f : 1f;
            p.body.constraints = (RigidbodyConstraints)56; p.body.drag = 0.2f; p.body.angularDrag = 0.05f;
            go.AddComponent<Box3DWheelAuditProbe>(); parts.Add(p);
        }
        foreach (var a in parts) foreach (var b in parts)
        {
            if (a == b) continue;
            if ((a.type == 19 || a.type == 1) && b.type == 6 && a.x == b.x && a.y == b.y)
            {
                Connect(a, b, false, joints);
                foreach (var ca in a.body.GetComponentsInChildren<Collider>()) foreach (var cb in b.body.GetComponentsInChildren<Collider>()) GamePhysics.IgnoreCollision(ca, cb);
            }
            if (a.type == 6 && b.type == 6 && (b.x == a.x + 1 && a.y == b.y || a.x == b.x && b.y == a.y + 1)) Connect(a, b, false, joints);
            if ((a.type == 8 || a.type == 10) && b.type == 6 && a.x == b.x && a.y + 1 == b.y) Connect(a, b, false, joints);
        }
        var ground = new GameObject("ground"); ground.transform.position = new Vector3(-821, -13.21f, 0);
        ground.AddComponent<BoxCollider>().size = new Vector3(1000, 1, 10);
        Physics.SyncTransforms();
        if (box && !GamePhysics.SelectBox3D(true)) throw new Exception(GamePhysics.LastError);
        float maxError = 0, lateSpeed = 0, lateError = 0; int rebuilds = 0, extraCount = 0;
        var ids = JointIds();
        for (int tick = 0; tick < 3000; tick++)
        {
            foreach (var p in parts) if (p.type == 8)
                foreach (var sphere in p.body.GetComponentsInChildren<SphereCollider>())
                    sphere.transform.localRotation = Quaternion.AngleAxis(-p.body.rotation.eulerAngles.z + (driven ? tick * 7 : 0), Vector3.forward);
            if (tick == 1) foreach (var a in parts) foreach (var b in parts)
                if (a != b && a.type == 6 && a.variant == 10 && b.type == 6 && b.variant == 10)
                { Connect(a, b, true, joints); extraCount++; }
            if (driven && tick > 100 && tick < 600) foreach (var p in parts)
                if (p.type == 10 && p.body.velocity.x < 15) p.body.EngineAddForceAtPosition(Vector3.right * 50, p.body.position + Vector3.down * 0.5f);
            GamePhysics.Simulate(0.02f);
            if (box && !GamePhysics.IsBox3D) throw new Exception(GamePhysics.LastError);
            foreach (var j in joints) if (j && j.connectedBody)
            {
                float error = Vector3.Distance(j.transform.TransformPoint(j.anchor), j.connectedBody.transform.TransformPoint(j.connectedAnchor));
                maxError = Mathf.Max(maxError, error); if (tick > 2000) lateError = Mathf.Max(lateError, error);
            }
            if (tick > 2000) foreach (var p in parts) lateSpeed = Mathf.Max(lateSpeed, p.body.velocity.magnitude);
            var next = JointIds(); foreach (var pair in next) if (ids.TryGetValue(pair.Key, out var id) && !id.Equals(pair.Value)) rebuilds++;
            foreach (var pair in ids) if (!next.ContainsKey(pair.Key))
                foreach (var j in joints) if (j && j.GetInstanceID() == pair.Key)
                    results.Add("Removed tick=" + tick + " joint=" + j.name + " to=" + j.connectedBody.name + " type=" + j.GetType().Name);
            ids = next;
        }
        int breaks = 0; float mass = 0; foreach (var p in parts) { breaks += p.body.GetComponent<Box3DWheelAuditProbe>().breaks; mass += p.body.mass; }
        bool pass = parts.Count == 34 && Mathf.Abs(mass - 24) < 0.001f && extraCount == 90 &&
            breaks == 0 && rebuilds == 0 && maxError < 0.05f && lateError < 0.02f && lateSpeed < 0.1f &&
            (!driven || parts[0].body.position.x > -800);
        if (!pass) failures++;
        results.Add((pass ? "PASS " : "FAIL ") + (box ? "Box3D" : "PhysX") + " driven=" + driven + " parts=" + parts.Count + " mass=" + mass + " joints=" + joints.Count + " extra=" + extraCount + " error=" + maxError + " lateError=" + lateError + " lateSpeed=" + lateSpeed + " breaks=" + breaks + " rebuilds=" + rebuilds + " x=" + parts[0].body.position.x);
        Directory.CreateDirectory("Builds/QA"); File.WriteAllLines("Builds/QA/box3d-dense.txt", results);
    }
    public static void Run()
    {
        int exit = 0;
        try { PhysicsSimulation.Auto = false; foreach (bool box in new[] { false, true }) foreach (bool drive in new[] { false, true }) RunCase(box, drive); }
        catch (Exception e) { results.Add(e.ToString()); exit = 1; }
        finally { Clear(); Directory.CreateDirectory("Builds/QA"); File.WriteAllLines("Builds/QA/box3d-dense.txt", results); }
        EditorApplication.Exit(exit != 0 || failures > 0 ? 1 : 0);
    }
    public static void RunInPlayMode()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool("Box3DDenseVehicleAudit.Play", true); EditorApplication.isPlaying = true;
    }
    [InitializeOnLoadMethod]
    private static void Resume()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("Box3DDenseVehicleAudit.Play", false)) return;
            SessionState.SetBool("Box3DDenseVehicleAudit.Play", false); EditorApplication.delayCall += Run;
        };
    }
}
