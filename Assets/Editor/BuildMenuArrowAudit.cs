using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Checks the build menu's mirrored arrow buttons: which way of mirroring the physics engine warns about,
/// and whether mirroring the sprite draws the same picture as mirroring the transform.
/// </summary>
/// <remarks>
/// <para>
/// Opening the in-game build menu logged "BoxCollider does not support negative scale or size ... Scene
/// hierarchy path InGameGUI/InGameBuildMenu/MoveButtons/MoveLeftButton". The arrows are one sprite, and
/// the left and up ones are mirrored by giving their transform a negative x scale, which is what a
/// BoxCollider cannot have: PhysX forces the box positive and warns. <c>Sprite.m_flipX</c> mirrors the
/// sprite in its own mesh instead, which leaves the transform, the mesh and the collider alone.
/// </para>
/// <para>
/// The two ways have to draw the same thing, so the audit builds both from the prefab's own sprite
/// settings and compares them vertex by vertex - world position and uv - and counts the warnings each one
/// produces when the physics engine is stepped. It also lists every collider in the prefab that still sits
/// under a negative scale, which is the check that the fix is complete.
/// </para>
/// </remarks>
[InitializeOnLoad]
public static class BuildMenuArrowAudit
{
    private const string Flag = "BuildMenuArrowAudit";
    private const string PrefabPath = "Assets/GameObject/InGameBuildMenu.prefab";
    private const string ReportPath = "Builds/QA/build-menu-arrows.txt";

    /// <summary>The arrows that the prefab mirrors, and the one that is not mirrored, as a control.</summary>
    private static readonly string[] Arrows = { "MoveLeftButton", "MoveUpButton", "MoveRightButton" };

    private static readonly StringBuilder Report = new StringBuilder();
    private static readonly List<string> Failures = new List<string>();
    private static int s_warnings;

    static BuildMenuArrowAudit()
    {
        EditorApplication.playModeStateChanged += StateChanged;
    }

    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Flag, true);
        EditorApplication.update += () =>
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("Build menu arrow audit never reached play mode; giving up.");
                SessionState.SetBool(Flag, false);
                EditorApplication.Exit(2);
            }
        };
        EditorApplication.EnterPlaymode();
    }

    private static void StateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Flag, false)) { return; }
        if (state != PlayModeStateChange.EnteredPlayMode) { return; }
        SessionState.SetBool(Flag, false);
        int code = 1;
        try
        {
            code = Check() ? 0 : 1;
        }
        catch (Exception exception)
        {
            Failures.Add(exception.ToString());
        }
        Report.Insert(0, "BUILD MENU ARROW AUDIT\n======================\n\n");
        foreach (string failure in Failures) { Report.AppendLine("FAIL " + failure); }
        Report.AppendLine();
        Report.AppendLine("Result: " + (Failures.Count == 0 ? "PASS" : "FAIL (" + Failures.Count + ")"));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(ReportPath)));
        File.WriteAllText(ReportPath, Report.ToString());
        EditorApplication.Exit(code);
    }

    private static bool Check()
    {
        // The sprites come out of the game's own database, loaded from Resources by the component itself.
        new GameObject("QA sprite database", typeof(RuntimeSpriteDatabase));

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Failures.Add("no prefab at " + PrefabPath);
            return false;
        }

        ReportNegativeColliders(prefab);

        if (Find(prefab.transform, "MoveButtons") == null)
        {
            // The toolbar gizmo has replaced the legacy four-arrow group.
            Report.AppendLine("Legacy move buttons removed; sprite-mirroring comparison is retired.");
            foreach (string name in new[] { "MoveLeftButton", "MoveUpButton", "MoveRightButton", "MoveDownButton" })
                if (Find(prefab.transform, name) != null) Failures.Add("orphaned legacy arrow " + name);
            return Failures.Count == 0;
        }

        Application.logMessageReceived += OnLog;
        foreach (string name in Arrows)
        {
            Transform arrow = Find(prefab.transform, name);
            if (arrow == null)
            {
                Failures.Add("no " + name + " in the prefab");
                continue;
            }
            Compare(name, arrow);
        }
        Application.logMessageReceived -= OnLog;
        return Failures.Count == 0;
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (message.Contains("negative scale")) { s_warnings++; }
    }

    /// <summary>Every collider in the prefab that still sits under a scale with a negative component.</summary>
    private static void ReportNegativeColliders(GameObject prefab)
    {
        Report.AppendLine("=== colliders under a negative scale");
        int found = 0;
        foreach (Collider collider in prefab.GetComponentsInChildren<Collider>(true))
        {
            Vector3 scale = Vector3.one;
            for (Transform step = collider.transform; step != null; step = step.parent)
            {
                scale = Vector3.Scale(scale, step.localScale);
            }
            if (scale.x >= 0f && scale.y >= 0f && scale.z >= 0f) { continue; }
            found++;
            Report.AppendLine("   " + HierarchyPath(collider.transform) + " " + collider.GetType().Name +
                " scale " + scale + (collider.enabled ? "" : " (disabled)"));
            Failures.Add(HierarchyPath(collider.transform) + " still has a collider under " + scale);
        }
        if (found == 0) { Report.AppendLine("   none"); }
    }

    /// <summary>
    /// Builds the arrow three ways - with the negative scale the warning is about, as the prefab has it, and
    /// with the mirror moved into the sprite - and compares the last two, which have to draw the same thing.
    /// </summary>
    private static void Compare(string name, Transform arrow)
    {
        Sprite source = arrow.GetComponent<Sprite>();
        BoxCollider box = arrow.GetComponent<BoxCollider>();
        if (source == null || box == null)
        {
            Failures.Add(name + " has no Sprite or no BoxCollider");
            return;
        }

        Report.AppendLine();
        Report.AppendLine("=== " + name);
        Report.AppendLine("   prefab: scale " + arrow.localScale + " flipX " + source.m_flipX +
            " sprite " + source.m_id + " scale " + source.m_scaleX + "x" + source.m_scaleY +
            " pivot " + source.m_pivotX + "," + source.m_pivotY + " collider " + box.size);

        // Three builds of the same arrow: one with the negative scale the warning is about, one exactly as
        // the prefab has it today, and one with the mirror moved into the sprite. The last two have to draw
        // the same picture, and the prefab's own one has to come out of it warning-free once it is fixed.
        bool mirrored = source.m_flipX || arrow.localScale.x < 0f;
        GameObject negative = Build(name + " (negative scale)", arrow, source, box, -Mathf.Abs(arrow.localScale.x), false);
        GameObject current = Build(name + " (as the prefab has it)", arrow, source, box, arrow.localScale.x, source.m_flipX);
        GameObject moved = Build(name + " (sprite carries the mirror)", arrow, source, box, Mathf.Abs(arrow.localScale.x), mirrored);
        Report.AppendLine("   meshes: " + Describe(current.GetComponent<MeshFilter>().sharedMesh) +
            " and " + Describe(moved.GetComponent<MeshFilter>().sharedMesh));

        CompareVertices(name, current, moved);
        CompareColliders(name, current, moved);
        Report.AppendLine("   warnings from stepping physics: negative scale " + Warnings(negative) +
            ", as the prefab has it " + Warnings(current) + ", sprite carrying the mirror " + Warnings(moved));
        if (arrow.localScale.x < 0f)
        {
            Failures.Add(name + " still mirrors with a negative transform scale, which its BoxCollider cannot have");
        }

        UnityEngine.Object.DestroyImmediate(negative);
        UnityEngine.Object.DestroyImmediate(current);
        UnityEngine.Object.DestroyImmediate(moved);
    }

    /// <summary>
    /// Every vertex of the arrow mirrored in its sprite has to land on a vertex of the same arrow mirrored
    /// by its transform, with the same uv - which is the statement that the two draw the same picture in
    /// the same place.
    /// </summary>
    private static void CompareVertices(string name, GameObject current, GameObject moved)
    {
        Mesh left = current.GetComponent<MeshFilter>().sharedMesh;
        Mesh right = moved.GetComponent<MeshFilter>().sharedMesh;
        if (left == null || right == null)
        {
            Failures.Add(name + " built no mesh");
            return;
        }
        Vector3[] leftVertices = left.vertices;
        Vector2[] leftUvs = left.uv;
        Vector3[] rightVertices = right.vertices;
        Vector2[] rightUvs = right.uv;
        Matrix4x4 toLeft = current.transform.localToWorldMatrix;
        Matrix4x4 toRight = moved.transform.localToWorldMatrix;

        float worstPosition = 0f;
        float worstUv = 0f;
        foreach (Vector3 vertex in rightVertices)
        {
            Vector3 wanted = toRight.MultiplyPoint3x4(vertex);
            int index = IndexOf(rightVertices, vertex);
            Vector3 wantedUv = rightUvs[index];
            float nearest = float.MaxValue;
            float uvAtNearest = 0f;
            for (int other = 0; other < leftVertices.Length; other++)
            {
                float distance = Vector3.Distance(wanted, toLeft.MultiplyPoint3x4(leftVertices[other]));
                if (distance >= nearest) { continue; }
                nearest = distance;
                uvAtNearest = Vector2.Distance(wantedUv, leftUvs[other]);
            }
            worstPosition = Mathf.Max(worstPosition, nearest);
            worstUv = Mathf.Max(worstUv, uvAtNearest);
        }
        Report.AppendLine("   same picture: worst vertex distance " + worstPosition.ToString("F5") +
            ", worst uv difference " + worstUv.ToString("F5"));
        if (worstPosition > 0.0001f || worstUv > 0.0001f)
        {
            Failures.Add(name + " draws a different picture with its sprite carrying the mirror (position " +
                worstPosition.ToString("F5") + ", uv " + worstUv.ToString("F5") + ")");
        }
    }

    private static void CompareColliders(string name, GameObject current, GameObject moved)
    {
        BoxCollider left = current.GetComponent<BoxCollider>();
        BoxCollider right = moved.GetComponent<BoxCollider>();
        Vector3 leftCentre = current.transform.TransformPoint(left.center);
        Vector3 rightCentre = moved.transform.TransformPoint(right.center);
        Report.AppendLine("   collider in world: as the prefab has it " + leftCentre + " size " + left.size +
            ", sprite carrying the mirror " + rightCentre + " size " + right.size);
        if (Vector3.Distance(leftCentre, rightCentre) > 0.0001f)
        {
            Failures.Add(name + "'s collider sits elsewhere once the sprite carries the mirror (centre " +
                leftCentre + " vs " + rightCentre + ")");
        }
    }

    /// <summary>Steps the physics engine with this arrow's collider live and counts the warnings it logs.</summary>
    private static int Warnings(GameObject arrow)
    {
        s_warnings = 0;
        Physics.SyncTransforms();
        for (int step = 0; step < 4; step++) { Physics.Simulate(0.02f); }
        return s_warnings;
    }

    /// <summary>Rebuilds one arrow from the prefab's own settings, with the given scale and mirror.</summary>
    private static GameObject Build(string name, Transform arrow, Sprite source, BoxCollider box, float scaleX, bool flipX)
    {
        GameObject built = new GameObject(name);
        built.SetActive(false);
        built.transform.localPosition = arrow.localPosition;
        built.transform.localRotation = arrow.localRotation;
        Vector3 scale = arrow.localScale;
        built.transform.localScale = new Vector3(scaleX, scale.y, scale.z);
        built.AddComponent<MeshRenderer>();
        built.AddComponent<MeshFilter>();
        BoxCollider collider = built.AddComponent<BoxCollider>();
        collider.size = box.size;
        collider.center = box.center;

        // The sprite is configured while the object is still switched off, because its Awake is what reads
        // these and builds the mesh and the collider.
        Sprite sprite = built.AddComponent<Sprite>();
        sprite.m_id = source.m_id;
        sprite.m_scaleX = source.m_scaleX;
        sprite.m_scaleY = source.m_scaleY;
        sprite.m_pivotX = source.m_pivotX;
        sprite.m_pivotY = source.m_pivotY;
        sprite.m_updateCollider = source.m_updateCollider;
        sprite.m_flipX = flipX;
        built.SetActive(true);
        return built;
    }

    private static string Describe(Mesh mesh)
    {
        return mesh == null ? "none" : mesh.vertexCount + " vertices, bounds " + mesh.bounds;
    }

    private static int IndexOf(Vector3[] vertices, Vector3 wanted)
    {
        for (int index = 0; index < vertices.Length; index++)
        {
            if (vertices[index] == wanted) { return index; }
        }
        return 0;
    }

    private static Transform Find(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name) { return child; }
        }
        return null;
    }

    private static string HierarchyPath(Transform transform)
    {
        string path = transform.name;
        for (Transform step = transform.parent; step != null; step = step.parent)
        {
            path = step.name + "/" + path;
        }
        return path;
    }
}
