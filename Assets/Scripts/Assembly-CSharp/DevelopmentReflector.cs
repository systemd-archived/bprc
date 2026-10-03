using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[DefaultExecutionOrder(-10000)]
public sealed class DevelopmentReflector : MonoBehaviour
{
    private struct Entry
    {
        public GameObject Target;
        public Rect Bounds;
        public float Depth;
    }

    private readonly List<Entry> entries = new List<Entry>();
    private Vector2 dragStart;
    private bool dragging;
    private float nextScan;
    private static string reportText = "";
    private static bool reportVisible;
    private static Vector2 reportScroll;
    private GUIStyle reportStyle;
    private readonly List<BaseInputModule> pausedModules = new List<BaseInputModule>();
    public static bool IsInspecting => (Debug.isDebugBuild || Application.isEditor) && Input.GetKey(KeyCode.F6);
    private static Rect ReportRect => new Rect(Mathf.Max(8f, Screen.width - 668f), 48f,
        Mathf.Min(660f, Screen.width - 16f), Mathf.Min(380f, Screen.height - 64f));
    public static bool BlocksPointer => IsInspecting || (reportVisible && ReportRect.Contains(
        new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y)));

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if ((!Debug.isDebugBuild && !Application.isEditor) || FindObjectOfType<DevelopmentReflector>() != null)
            return;
        DontDestroyOnLoad(new GameObject("Development Reflector", typeof(DevelopmentReflector)));
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) reportVisible = false;
        if (BlocksPointer)
        {
            foreach (BaseInputModule module in FindObjectsOfType<BaseInputModule>())
            {
                if (!module.enabled) continue;
                pausedModules.Add(module);
                module.enabled = false;
            }
        }
        else RestoreInput();
        if (!IsInspecting)
        {
            dragging = false;
            entries.Clear();
            return;
        }
        if (Time.unscaledTime >= nextScan)
        {
            Scan();
            nextScan = Time.unscaledTime + 0.1f;
        }
        Vector2 mouse = Input.mousePosition;
        if (Input.GetMouseButtonDown(0))
        {
            if (reportVisible && ReportRect.Contains(new Vector2(mouse.x, Screen.height - mouse.y)))
                return;
            dragStart = mouse;
            dragging = true;
        }
        if (dragging && Input.GetMouseButtonUp(0))
        {
            SelectAt(dragStart, mouse);
            dragging = false;
        }
    }

    private void SelectAt(Vector2 start, Vector2 end)
    {
        Scan();
        Rect selection = MakeRect(start, end);
        IEnumerable<Entry> selected = selection.width + selection.height > 8f
            ? entries.Where(entry => selection.Overlaps(entry.Bounds))
            : entries.Where(entry => entry.Bounds.Contains(end)).OrderByDescending(entry => entry.Depth)
                .ThenBy(entry => entry.Bounds.width * entry.Bounds.height).Take(1);
        reportText = "";
        reportScroll = Vector2.zero;
        foreach (Entry entry in selected.GroupBy(entry => entry.Target).Select(group => group.First()).Take(100))
            Report(entry.Target);
        if (reportText.Length == 0) Publish("[F6 Reflector] No visible element at this position.", null);
    }

    private void RestoreInput()
    {
        foreach (BaseInputModule module in pausedModules)
            if (module != null) module.enabled = true;
        pausedModules.Clear();
    }

    private void OnDisable()
    {
        RestoreInput();
    }

    private void Scan()
    {
        entries.Clear();
        foreach (Renderer renderer in FindObjectsOfType<Renderer>())
        {
            if (!renderer.enabled || renderer is ParticleSystemRenderer)
                continue;
            foreach (Camera camera in Camera.allCameras)
            {
                if (!camera.enabled || camera.targetTexture != null || (camera.cullingMask & (1 << renderer.gameObject.layer)) == 0)
                    continue;
                Bounds bounds = renderer.bounds;
                var points = new List<Vector3>();
                for (int corner = 0; corner < 8; corner++)
                    points.Add(bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                        (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
                Add(renderer.gameObject, points, camera, camera.depth);
            }
        }
        foreach (Graphic graphic in FindObjectsOfType<Graphic>())
        {
            if (!graphic.isActiveAndEnabled || graphic.canvas == null || graphic.canvasRenderer.cull)
                continue;
            var corners = new Vector3[4];
            graphic.rectTransform.GetWorldCorners(corners);
            Canvas canvas = graphic.canvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Add(graphic.gameObject, corners, camera, camera == null ? 10000f : camera.depth);
        }
    }

    private void Add(GameObject target, IEnumerable<Vector3> corners, Camera camera, float depth)
    {
        Vector2 minimum = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 maximum = new Vector2(float.MinValue, float.MinValue);
        foreach (Vector3 corner in corners)
        {
            Vector3 screen = camera == null ? corner : camera.WorldToScreenPoint(corner);
            if (camera != null && screen.z < camera.nearClipPlane)
                return;
            minimum = Vector2.Min(minimum, screen);
            maximum = Vector2.Max(maximum, screen);
        }
        Rect rect = MakeRect(minimum, maximum);
        if (rect.width > 0f && rect.height > 0f && rect.Overlaps(new Rect(0, 0, Screen.width, Screen.height)))
            entries.Add(new Entry { Target = target, Bounds = rect, Depth = depth });
    }

    private static Rect MakeRect(Vector2 first, Vector2 second)
    {
        return Rect.MinMaxRect(Mathf.Min(first.x, second.x), Mathf.Min(first.y, second.y), Mathf.Max(first.x, second.x), Mathf.Max(first.y, second.y));
    }

    private static void Report(GameObject target)
    {
        var report = new StringBuilder("[F6 Reflector] ");
        string path = target.name;
        for (Transform parent = target.transform.parent; parent != null; parent = parent.parent)
            path = parent.name + "/" + path;
        report.AppendLine(path);
        report.AppendLine("Components: " + string.Join(", ", target.GetComponents<Component>().Where(component => component != null).Select(component => component.GetType().Name)));
        Sprite sprite = target.GetComponent<Sprite>();
        SpriteRenderer spriteRenderer = target.GetComponent<SpriteRenderer>();
        Image image = target.GetComponent<Image>();
        if (sprite != null) report.AppendLine("BPLE Sprite: " + sprite.Id);
        if (spriteRenderer != null && spriteRenderer.sprite != null) report.AppendLine("Sprite: " + spriteRenderer.sprite.name);
        if (image != null && image.sprite != null) report.AppendLine("UI Sprite: " + image.sprite.name);
        MeshFilter mesh = target.GetComponent<MeshFilter>();
        if (mesh != null && mesh.sharedMesh != null) report.AppendLine("Mesh / source sprite: " + mesh.sharedMesh.name);
        Renderer renderer = target.GetComponent<Renderer>();
        Texture texture = renderer != null && renderer.sharedMaterial != null ? renderer.sharedMaterial.mainTexture : null;
        Graphic graphic = target.GetComponent<Graphic>();
        if (texture == null && graphic != null) texture = graphic.mainTexture;
        if (texture != null) report.AppendLine("Texture: " + texture.name + " " + texture.width + "x" + texture.height + " filter=" + texture.filterMode);
        BasePart part = target.GetComponentInParent<BasePart>();
        if (part == null && target.GetComponent<RCR2Port.RoverPortIconSprite>() != null && mesh != null)
            part = RCR2Port.RoverPortCatalog.Instance.GetPartComponents().FirstOrDefault(candidate =>
                candidate.m_constructionIconSprite != null && candidate.m_constructionIconSprite.SpriteMesh == mesh.sharedMesh);
        if (part != null) report.AppendLine("Part: " + part.name + " type=" + part.Type + " index=" + part.customPartIndex);
        var port = part as RCR2Port.RoverPortPart;
        if (port != null) report.AppendLine("RCR2: " + port.PortId + " / " + port.DisplayName);
        report.AppendLine("Scale: " + target.transform.lossyScale);
        Publish(report.ToString(), target);
    }

    private static void Publish(string text, GameObject target)
    {
        reportText += text + "\n";
        if (reportText.Length > 65536) reportText = reportText.Substring(reportText.Length - 65536);
        reportVisible = true;
        Debug.Log(text, target);
        INCommandManager commandManager = Singleton<INCommandManager>.Instance;
        if (commandManager != null && commandManager.Writer != null)
            commandManager.Writer.WriteLine(text);
    }

    private void OnGUI()
    {
        GUI.depth = -10000;
        if (reportVisible) DrawReport();
        if (!IsInspecting) return;
        GUI.Label(new Rect(12, 12, 650, 28), "F6 Reflector: click to inspect; drag to select (100 max); release F6 to exit.");
        Vector2 mouse = Input.mousePosition;
        Rect selection = MakeRect(dragStart, mouse);
        foreach (Entry entry in entries)
            if (dragging ? selection.Overlaps(entry.Bounds) : entry.Bounds.Contains(mouse))
                DrawBorder(entry.Bounds, Color.cyan);
        if (dragging) DrawBorder(selection, Color.yellow);
    }

    private void DrawReport()
    {
        if (reportStyle == null)
            reportStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = false, fontSize = 14 };
        Rect panel = ReportRect;
        GUI.Box(panel, GUIContent.none);
        GUILayout.BeginArea(new Rect(panel.x + 8f, panel.y + 8f, panel.width - 16f, panel.height - 16f));
        GUILayout.BeginHorizontal();
        GUILayout.Label("F6 Development Console");
        if (GUILayout.Button("Copy", GUILayout.Width(60f))) GUIUtility.systemCopyBuffer = reportText;
        if (GUILayout.Button("Close", GUILayout.Width(60f))) reportVisible = false;
        GUILayout.EndHorizontal();
        reportScroll = GUILayout.BeginScrollView(reportScroll);
        GUILayout.Label(reportText, reportStyle);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private static void DrawBorder(Rect screen, Color color)
    {
        Rect rect = new Rect(screen.x, Screen.height - screen.yMax, screen.width, screen.height);
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax, rect.width, 1), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.y, 1, rect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMax, rect.y, 1, rect.height), Texture2D.whiteTexture);
        GUI.color = previous;
    }
}
