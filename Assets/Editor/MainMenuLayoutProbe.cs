using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Reports the main menu's start-area layout: where StartButton, CakeRaceButton and the injected
/// Multiplayer button sit, and how big their visible artwork and their click boxes actually are.
/// Opens Assets/Scene/Scenes/UI/MainMenu.unity, writes Builds/QA/mainmenu-layout.txt and logs
/// MAINMENU_LAYOUT_REPORT. Read-only: nothing is saved back into the scene.
/// </summary>
public static class MainMenuLayoutProbe
{
    private const string ScenePath = "Assets/Scene/Scenes/UI/MainMenu.unity";
    private const string ReportPath = "Builds/QA/mainmenu-layout.txt";
    private static readonly List<string> s_log = new List<string>();

    [MenuItem("Tools/Multiplayer/Probe Main Menu Layout")]
    public static void Run()
    {
        s_log.Clear();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject start = Find("StartButton");
        GameObject cake = Find("CakeRaceButton");
        GameObject layout = Find("StartLayout");
        if (start == null)
        {
            throw new InvalidOperationException("StartButton is missing from " + ScenePath);
        }
        if (layout == null)
        {
            throw new InvalidOperationException("StartLayout is missing from " + ScenePath);
        }

        Describe("StartLayout", layout);
        Describe("StartButton", start);
        if (cake != null)
        {
            Describe("CakeRaceButton", cake);
        }
        DescribeParentChain(start);
        DescribeCamera();
        DescribeNeighbours(start, cake);

        // What a row would look like if the three visuals were centred side by side.
        Bounds startVisual = VisualBounds(start);
        Bounds cakeVisual = cake != null ? VisualBounds(cake) : new Bounds();
        s_log.Add(string.Format("play visual  centre=({0:0.###}, {1:0.###}) size=({2:0.###} x {3:0.###})",
            startVisual.center.x, startVisual.center.y, startVisual.size.x, startVisual.size.y));
        if (cake != null)
        {
            s_log.Add(string.Format("cake visual  centre=({0:0.###}, {1:0.###}) size=({2:0.###} x {3:0.###})",
                cakeVisual.center.x, cakeVisual.center.y, cakeVisual.size.x, cakeVisual.size.y));
        }
        s_log.Add(string.Format("horizontal gap between play and cake visuals: {0:0.###}",
            Mathf.Abs(startVisual.center.x - cakeVisual.center.x) - (startVisual.size.x + cakeVisual.size.x) * 0.5f));
        s_log.Add("fileID of StartButton: " + ObjectId(start));

        Write();
        Debug.Log("MAINMENU_LAYOUT_REPORT " + string.Join(" | ", s_log));
    }

    private static string ObjectId(GameObject go)
    {
        string path = AssetDatabase.GetAssetPath(go);
        return string.IsNullOrEmpty(path) ? go.name : path;
    }

    private static GameObject Find(string name)
    {
        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go.name == name && go.scene.IsValid() && go.scene.isLoaded)
            {
                return go;
            }
        }
        return null;
    }

    private static void Describe(string label, GameObject root)
    {
        Transform transform = root.transform;
        s_log.Add(string.Format("{0}: active={1} pos={2} localScale={3} worldScale={4}",
            label, root.activeSelf, transform.localPosition, transform.localScale, transform.lossyScale));
        DescribeChildren(transform, 1);
    }

    private static void DescribeChildren(Transform parent, int depth)
    {
        for (int i = 0; i < parent.childCount && depth <= 3; i++)
        {
            Transform child = parent.GetChild(i);
            GameObject go = child.gameObject;
            List<string> bits = new List<string>();
            foreach (Component component in go.GetComponents<Component>())
            {
                if (component == null || component is Transform)
                {
                    continue;
                }
                if (component is MeshRenderer || component is SkinnedMeshRenderer || component is SpriteRenderer)
                {
                    Renderer renderer = (Renderer)component;
                    bits.Add(string.Format("{0}(enabled={1}, size=({2:0.###}x{3:0.###}) centre=({4:0.###},{5:0.###}) mat={6})",
                        component.GetType().Name, renderer.enabled, renderer.bounds.size.x, renderer.bounds.size.y,
                        renderer.bounds.center.x, renderer.bounds.center.y,
                        renderer.sharedMaterial != null ? renderer.sharedMaterial.name : "null"));
                }
                else if (component is MeshFilter)
                {
                    Mesh mesh = ((MeshFilter)component).sharedMesh;
                    bits.Add("MeshFilter(" + (mesh != null ? mesh.name + " bounds=" + mesh.bounds.size : "null") + ")");
                }
                else if (component is Collider)
                {
                    Collider collider = (Collider)component;
                    bits.Add(string.Format("{0}(enabled={1}, size=({2:0.###}x{3:0.###}) centre=({4:0.###},{5:0.###}))",
                        component.GetType().Name, collider.enabled, collider.bounds.size.x, collider.bounds.size.y,
                        collider.bounds.center.x, collider.bounds.center.y));
                }
                else
                {
                    bits.Add(component.GetType().Name);
                }
            }
            s_log.Add(string.Format("{0}{1} pos={2} scale={3} :: {4}",
                new string(' ', depth * 2), go.name, child.localPosition, child.localScale, string.Join(" ", bits)));
            DescribeChildren(child, depth + 1);
        }
    }

    private static void DescribeParentChain(GameObject go)
    {
        Transform transform = go.transform;
        while (transform != null)
        {
            s_log.Add(string.Format("chain: {0} local={1} world={2} scale={3}",
                transform.name, transform.localPosition, transform.position, transform.lossyScale));
            transform = transform.parent;
        }
    }

    private static void DescribeCamera()
    {
        foreach (Camera camera in Camera.allCameras)
        {
            s_log.Add(string.Format("camera {0}: ortho={1} size={2:0.###} pos={3} aspect={4:0.###}",
                camera.name, camera.orthographic, camera.orthographicSize, camera.transform.position,
                camera.aspect));
        }
    }

    /// <summary>
    /// Everything else in the loaded scene whose click box reaches into the band the three start
    /// buttons would occupy, so a wider row does not land on top of another control.
    /// </summary>
    private static void DescribeNeighbours(GameObject start, GameObject cake)
    {
        const float bandLeft = -13f;
        const float bandRight = 13f;
        const float bandBottom = -4.6f;
        const float bandTop = 2.4f;
        Bounds band = new Bounds(new Vector3((bandLeft + bandRight) * 0.5f, (bandBottom + bandTop) * 0.5f, 0f),
            new Vector3(bandRight - bandLeft, bandTop - bandBottom, 0f));
        band.center = new Vector3(band.center.x, band.center.y, start.transform.position.z);

        List<string> found = new List<string>();
        foreach (Collider collider in Resources.FindObjectsOfTypeAll<Collider>())
        {
            GameObject go = collider.gameObject;
            if (!go.scene.IsValid() || !go.scene.isLoaded || !collider.enabled || !go.activeInHierarchy)
            {
                continue;
            }
            if (go.transform.IsChildOf(start.transform) || (cake != null && go.transform.IsChildOf(cake.transform)))
            {
                continue;
            }
            if (!collider.bounds.Intersects(band))
            {
                continue;
            }
            found.Add(string.Format("{0} (size {1:0.##}x{2:0.##} centre {3:0.##},{4:0.##})",
                HierarchyPath(go), collider.bounds.size.x, collider.bounds.size.y,
                collider.bounds.center.x, collider.bounds.center.y));
        }
        found.Sort(StringComparer.Ordinal);
        s_log.Add("neighbours in the row band (" + found.Count + "):");
        foreach (string line in found)
        {
            s_log.Add("  " + line);
        }
    }

    private static string HierarchyPath(GameObject go)
    {
        string path = go.name;
        Transform parent = go.transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }
    /// <summary>Union of every enabled renderer under the object; falls back to collider bounds.</summary>
    private static Bounds VisualBounds(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
        bool has = false;
        Bounds bounds = new Bounds(go.transform.position, Vector3.zero);
        foreach (Renderer renderer in renderers)
        {
            if (!renderer.enabled || renderer.GetComponent<MeshFilter>() != null &&
                renderer.GetComponent<MeshFilter>().sharedMesh == null)
            {
                continue;
            }
            if (!has)
            {
                bounds = renderer.bounds;
                has = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }
        if (!has)
        {
            Collider[] colliders = go.GetComponentsInChildren<Collider>(true);
            foreach (Collider collider in colliders)
            {
                if (!has)
                {
                    bounds = collider.bounds;
                    has = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }
        }
        return bounds;
    }

    private static void Write()
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Main menu layout probe " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        foreach (string line in s_log)
        {
            builder.AppendLine("  " + line);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, builder.ToString());
        Debug.Log("report: " + ReportPath);
    }
}
