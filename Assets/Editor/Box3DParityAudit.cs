using System;
using System.Collections.Generic;
using System.IO;
using Box3D;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Box3DParityAudit
{
    private static readonly List<string> rows = new List<string>();
    private static readonly List<GameObject> objects = new List<GameObject>();
    private static bool useBox;
    private static readonly List<Mesh> meshes = new List<Mesh>();
    private static int failures;

    private static void Require(bool condition, string message)
    {
        rows.Add((condition ? "PASS " : "FAIL ") + (useBox ? "Box3D " : "PhysX ") + message);
        if (!condition) failures++;
    }

    private static Rigidbody Body(string name, Vector3 position, Vector3 size, float mass = 1)
    {
        var go = new GameObject(name); objects.Add(go);
        go.transform.position = position;
        var collider = go.AddComponent<BoxCollider>(); collider.size = size;
        var body = go.AddComponent<Rigidbody>(); body.mass = mass; body.maxAngularVelocity = 100;
        body.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
        return body;
    }
    private static void Clear()
    {
        GamePhysics.Shutdown();
        foreach (var go in objects) if (go) UnityEngine.Object.DestroyImmediate(go);
        objects.Clear();
        foreach (var mesh in meshes) if (mesh) UnityEngine.Object.DestroyImmediate(mesh);
        meshes.Clear();
    }
    private static void Ground(bool mesh, bool mirrored)
    {
        var go = new GameObject("ground"); objects.Add(go);
        if (!mesh)
        {
            go.transform.position = new Vector3(0, -0.1f, 0);
            go.AddComponent<BoxCollider>().size = new Vector3(100, 0.2f, 10);
        }
        else
        {
            var geometry = new Mesh(); meshes.Add(geometry); geometry.name = "parity ground";
            geometry.vertices = new[] { new Vector3(-50, 0, -5), new Vector3(-50, 0, 5), new Vector3(50, 0, 5), new Vector3(50, 0, -5) };
            geometry.triangles = new[] { 0, 1, 2, 0, 2, 3 }; geometry.RecalculateBounds();
            go.AddComponent<MeshCollider>().sharedMesh = geometry;
            if (mirrored) go.transform.localScale = new Vector3(-1, 1, 1);
        }
    }
    private static void Start()
    {
        Physics.SyncTransforms();
        if (useBox && !GamePhysics.SelectBox3D(true)) throw new Exception(GamePhysics.LastError);
    }
    private static void Step()
    {
        GamePhysics.Simulate(0.02f);
        if (useBox && !GamePhysics.IsBox3D) throw new Exception("Unexpected fallback: " + GamePhysics.LastError);
    }
    private static void WeldChain()
    {
        Ground(false, false);
        var bodies = new List<Rigidbody>(); var joints = new List<FixedJoint>();
        for (int i = 0; i < 12; i++)
        {
            var rb = Body("frame " + i, new Vector3(i - 6, 6, -0.1f - i * 0.00001f), new Vector3(0.95f, 0.95f, 0.3f), 1);
            bodies.Add(rb);
            if (i > 0)
            {
                var j = rb.gameObject.AddComponent<FixedJoint>(); j.connectedBody = bodies[i - 1];
                j.enablePreprocessing = false; joints.Add(j);
            }
        }
        var payload = Body("enclosed payload", bodies[6].position, new Vector3(0.6f, 0.6f, 0.2f), 20);
        var inside = payload.gameObject.AddComponent<FixedJoint>(); inside.connectedBody = bodies[6]; joints.Add(inside);
        GamePhysics.IgnoreCollision(payload.GetComponent<Collider>(), bodies[6].GetComponent<Collider>());
        Start();
        float maxError = 0, maxPenetration = 0, maxPayload = 0;
        for (int tick = 0; tick < 250; tick++)
        {
            if (tick == 90) payload.EngineAddForce(new Vector3(100, 0, 0), ForceMode.Impulse);
            Step();
            foreach (var j in joints) maxError = Mathf.Max(maxError, Vector3.Distance(j.transform.TransformPoint(j.anchor), j.connectedBody.transform.TransformPoint(j.connectedAnchor)));
            foreach (var rb in bodies) maxPenetration = Mathf.Max(maxPenetration, -rb.GetComponent<Collider>().bounds.min.y);
            maxPayload = Mathf.Max(maxPayload, Vector3.Distance(payload.position, bodies[6].position));
        }
        rows.Add((useBox ? "Box3D" : "PhysX") + " chain: anchorError=" + maxError + " groundPenetration=" + maxPenetration + " payloadOffset=" + maxPayload);
        Require(maxError < 0.25f && maxPayload < 0.25f && maxPenetration < 0.15f, "loaded frame chain stays connected and above ground");
    }
    private static void Drop(bool mesh, bool mirrored, float speed, bool locks = true, bool freezeAllRotation = false)
    {
        Ground(mesh, mirrored);
        var rb = Body("drop", new Vector3(0, 4, 0), new Vector3(0.4f, 0.4f, 0.2f));
        rb.velocity = new Vector3(0, -speed, 0); rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        if (!locks) rb.constraints = RigidbodyConstraints.None;
        if (freezeAllRotation) rb.constraints |= RigidbodyConstraints.FreezeRotation;
        Start();
        float minimum = 100;
        for (int i = 0; i < 100; i++) { Step(); minimum = Mathf.Min(minimum, rb.GetComponent<Collider>().bounds.min.y); }
        rows.Add((useBox ? "Box3D" : "PhysX") + " drop locks=" + rb.constraints + " mesh=" + mesh + " mirrored=" + mirrored + " speed=" + speed + " minBottom=" + minimum + " final=" + rb.position);
        Require(minimum > -0.01f && rb.position.y < 0.4f && Mathf.Abs(rb.velocity.y) < 0.1f, "drop reaches and rests on ground");
    }
    private static void ChangingLocks()
    {
        var rb = Body("changing locks", Vector3.zero, Vector3.one * 0.4f, 2);
        rb.useGravity = false;
        Start(); Step();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        Step();
        rb.EngineAddForce(Vector3.right * 2, ForceMode.Impulse);
        Step();
        Require(Mathf.Abs(rb.velocity.x - 1) < 0.001f, "changing rotation locks preserves authored mass");
        rb.constraints = RigidbodyConstraints.None;
        rb.EngineAddTorque(Vector3.forward * 0.1f, ForceMode.Impulse);
        Step();
        Require(rb.angularVelocity.z > 1 && rb.angularVelocity.z < 3, "unlocking rotation restores finite inertia");
    }

    private static void FramePayload()
    {
        Ground(false, false);
        var frame = Body("frame", new Vector3(0, 5, 0), Vector3.one, 1);
        var payload = Body("payload", frame.position, Vector3.one * 0.6f, 20);
        var joint = payload.gameObject.AddComponent<FixedJoint>(); joint.connectedBody = frame;
        joint.enablePreprocessing = false; joint.breakForce = 100000;
        GamePhysics.IgnoreCollision(frame.GetComponent<Collider>(), payload.GetComponent<Collider>());
        Start();
        float drift = 0;
        for (int i = 0; i < 160; i++)
        {
            if (i == 65) payload.EngineAddForce(Vector3.right * 40, ForceMode.Impulse);
            if (i == 66) joint.breakForce *= 2;
            Step(); drift = Mathf.Max(drift, Vector3.Distance(frame.position, payload.position));
        }
        rows.Add((useBox ? "Box3D" : "PhysX") + " enclosed payload max offset=" + drift);
        // PhysX itself has a transient ~0.203 offset for this 20:1 impact in Play Mode.
        Require(joint && drift < 0.22f && frame.position.y > 0.4f, "enclosed payload stays at PhysX containment tolerance through impact and retuning");
    }

    private static Vector3 ReadPosition(BinaryReader reader) => new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
    private static Vector3[] ReadTerrainMesh(BinaryReader reader)
    {
        var vertices = new Vector3[reader.ReadInt32()];
        for (int i = 0; i < vertices.Length; i++) vertices[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), 0);
        int indices = reader.ReadInt32();
        reader.BaseStream.Seek(indices * 2L, SeekOrigin.Current);
        return vertices;
    }

    // Read only collider geometry using LevelLoader's binary layout; gameplay singletons are unnecessary.
    private static void ReadDreamObject(BinaryReader reader, LevelLoader loader, List<Collider> ground)
    {
        int children = reader.ReadInt16(); string name = reader.ReadString();
        if (children != 0)
        {
            ReadPosition(reader);
            for (int i = 0; i < children; i++) ReadDreamObject(reader, loader, ground);
            return;
        }
        reader.ReadInt16();
        Vector3 position = ReadPosition(reader), euler = ReadPosition(reader), scale = ReadPosition(reader);
        byte type = reader.ReadByte();
        if (type == 0) return;
        if (type == 2) { int count = reader.ReadInt32(); reader.BaseStream.Seek(count, SeekOrigin.Current); return; }
        if (type != 1) throw new InvalidDataException("Unknown level data type " + type);
        reader.ReadSingle(); reader.ReadSingle();
        var polygon = ReadTerrainMesh(reader);
        reader.ReadUInt32(); reader.ReadInt32(); ReadTerrainMesh(reader);
        int textures = reader.ReadInt32();
        reader.BaseStream.Seek(textures * 17L, SeekOrigin.Current);
        if (reader.ReadInt32() > 0) { int count = reader.ReadInt32(); reader.BaseStream.Seek(count, SeekOrigin.Current); }
        if (!reader.ReadBoolean()) return;
        var go = new GameObject(name); objects.Add(go);
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(euler)); go.transform.localScale = scale;
        var child = new GameObject(e2dConstants.COLLIDER_MESH_NAME); child.transform.SetParent(go.transform, false);
        var collider = child.AddComponent<MeshCollider>();
        loader.CreateCollider(go, polygon);
        meshes.Add(collider.sharedMesh); ground.Add(collider);
    }

    private static void DreamTerrain()
    {
        var loader = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/levels/episode_4_sandbox/level_sandbox_06_loader.prefab").GetComponent<LevelLoader>();
        var ground = new List<Collider>();
        using (var reader = new BinaryReader(File.OpenRead("Assets/assetbundles/episode_sandbox_levels_2.unity3d/Level_Sandbox_06_data.bytes")))
        {
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++) ReadDreamObject(reader, loader, ground);
            Require(reader.BaseStream.Position == reader.BaseStream.Length, "Field of Dreams data fully parsed");
        }
        Physics.SyncTransforms();
        var bounds = ground[0].bounds;
        foreach (var c in ground) bounds.Encapsulate(c.bounds);
        var drops = new List<Rigidbody>(); var heights = new List<float>();
        for (int i = 1; i < 100 && drops.Count < 12; i++)
        {
            float x = Mathf.Lerp(bounds.min.x, bounds.max.x, i / 100f);
            if (!Physics.Raycast(new Vector3(x, bounds.max.y + 10, 0), Vector3.down, out var hit, bounds.size.y + 20) || hit.normal.y < 0.97f) continue;
            var rb = Body("dream drop " + drops.Count, hit.point + Vector3.up * 4, Vector3.one * 0.4f);
            rb.constraints |= RigidbodyConstraints.FreezePositionX;
            rb.velocity = Vector3.down * 30; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            drops.Add(rb); heights.Add(hit.point.y);
        }
        Require(ground.Count > 0 && drops.Count >= 8, "Field of Dreams terrain and distributed drop samples loaded");
        Start();
        float clearance = float.MaxValue;
        for (int tick = 0; tick < 100; tick++)
        {
            Step();
            for (int i = 0; i < drops.Count; i++) clearance = Mathf.Min(clearance, drops[i].position.y - heights[i]);
        }
        rows.Add((useBox ? "Box3D" : "PhysX") + " Field of Dreams terrain meshes=" + ground.Count + " drops=" + drops.Count + " min center clearance=" + clearance);
        Require(clearance > 0.15f && clearance < 0.35f, "Field of Dreams fast drops stop above real terrain");
    }
    private static void Hierarchy()
    {
        var child = Body("created first child", new Vector3(0, 3, 0), Vector3.one * 0.5f);
        var parent = Body("created second parent", new Vector3(0, 3, 0), Vector3.one);
        child.transform.SetParent(parent.transform, true);
        var weld = child.gameObject.AddComponent<FixedJoint>(); weld.connectedBody = parent;
        GamePhysics.IgnoreCollision(child.GetComponent<Collider>(), parent.GetComponent<Collider>());
        Start();
        float error = 0;
        for (int i = 0; i < 80; i++)
        {
            parent.EngineAddForce(Vector3.right * 10);
            Step(); error = Mathf.Max(error, Vector3.Distance(child.transform.position, parent.transform.position));
        }
        rows.Add((useBox ? "Box3D" : "PhysX") + " nested body offset=" + error);
    }
    public static void Run()
    {
        int code = 0;
        try
        {
            if (!EditorApplication.isPlaying) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PhysicsSimulation.Auto = false; Time.fixedDeltaTime = 0.02f;
            rows.Add("Execution: " + (EditorApplication.isPlaying ? "Play Mode" : "Edit Mode"));
            var wd = WeldJointDef.Default; var world = WorldDef.Default; var distance = DistanceJointDef.Default;
            rows.Add("Native defaults weld=" + wd.LinearHertz + "/" + wd.AngularHertz + " constraint=" + wd.Base.ConstraintHertz + " contact=" + world.ContactHertz + " contactSpeed=" + world.ContactSpeed + " distanceForces=" + distance.LowerSpringForce + "/" + distance.UpperSpringForce);
            foreach (bool mode in new[] { false, true })
            {
                useBox = mode;
                WeldChain(); Clear();
                FramePayload(); Clear();
                ChangingLocks(); Clear();
                Drop(false, false, 30); Clear();
                Drop(false, false, 30, true, true); Clear();
                Drop(false, false, 0, false); Clear();
                Drop(false, false, 30, false); Clear();
                Drop(true, false, 30); Clear();
                Drop(true, true, 30); Clear();
                Hierarchy(); Clear();
                DreamTerrain(); Clear();
            }
        }
        catch (Exception e) { code = 1; rows.Add(e.ToString()); Debug.LogException(e); }
        finally { Clear(); Directory.CreateDirectory("Builds/QA"); File.WriteAllLines("Builds/QA/box3d-parity" + (EditorApplication.isPlaying ? "-play" : "") + ".txt", rows); }
        EditorApplication.Exit(code != 0 || failures > 0 ? 1 : 0);
    }

    public static void RunInPlayMode()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool("Box3DParityAudit.RunInPlayMode", true);
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    private static void ResumePlayModeAudit()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("Box3DParityAudit.RunInPlayMode", false)) return;
            SessionState.SetBool("Box3DParityAudit.RunInPlayMode", false);
            EditorApplication.delayCall += Run;
        };
    }
}
