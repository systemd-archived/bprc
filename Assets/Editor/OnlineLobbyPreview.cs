using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BadPiggies.Multiplayer.Online.Ui;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders the MCTier-style online lobby to PNG files without entering Play Mode, so the window can
/// be reviewed (and screenshotted for a report) straight from the Editor. Each navigation page gets
/// its own image.
///
/// The window builds itself into a ScreenSpaceOverlay canvas, which cameras do not render, so this
/// switches the canvas to ScreenSpaceCamera for the length of the capture and restores it (and
/// destroys everything it created) afterwards.
///
/// Run from the menu (Tools/Multiplayer/Capture Online Lobby Preview), from the batch line
///   Unity.exe -batchmode -quit -projectPath . -executeMethod OnlineLobbyPreview.Capture -logFile -
/// or from the DSH bridge with {"method":"invoke_static",
///   "params":{"type":"OnlineLobbyPreview","method":"CaptureAll","args":["Builds/QA"]}}.
/// </summary>
public static class OnlineLobbyPreview
{
    private const string CanvasName = "Online Lobby Canvas";
    private const string DefaultOutputFolder = "Builds/QA";
    private static int Width = 1180;
    private static int Height = 680;

    private static readonly string[] Pages = { "Home", "Create", "Join", "Lan", "Recent", "Settings" };


    /// <summary>Moves a UI subtree onto its own layer so the capture camera never renders the scene.</summary>
    private const int CaptureLayer = 5;

    private static void SetLayer(Transform root)
    {
        if (root == null)
        {
            return;
        }
        root.gameObject.layer = CaptureLayer;
        for (int i = 0; i < root.childCount; i++)
        {
            SetLayer(root.GetChild(i));
        }
    }

    /// <summary>
    /// Renders Heat UI's whole settings panel on its own. Its prefab carries a full-screen demo
    /// background image, which used to hide the panel behind it, so images whose sprite comes from
    /// the placeholder/background set are disabled first.
    /// </summary>
    public static string CaptureHeatUiPanel(string outputFolder)
    {
        if (string.IsNullOrEmpty(outputFolder))
        {
            outputFolder = DefaultOutputFolder;
        }
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string absoluteFolder = Path.IsPathRooted(outputFolder) ? outputFolder : Path.Combine(projectRoot, outputFolder);
        Directory.CreateDirectory(absoluteFolder);

        const int width = 1600;
        const int height = 1000;
        GameObject canvasObject = null;
        GameObject cameraObject = null;
        RenderTexture target = null;
        try
        {
            canvasObject = new GameObject("HeatUI Panel Canvas", typeof(RectTransform));
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            cameraObject = new GameObject("HeatUI Panel Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.09f, 0.13f, 0.17f, 1f);
            camera.cullingMask = 1 << CaptureLayer;
            camera.orthographic = true;
            target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            target.Create();
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;

            GameObject prefab = Resources.Load<GameObject>("HeatUI/Settings Layout");
            if (prefab == null)
            {
                return "failed: HeatUI/Settings Layout not found";
            }
            GameObject instance = UnityEngine.Object.Instantiate(prefab, canvasObject.transform);
            instance.name = "Settings Layout";
            RectTransform rect = instance.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(width, height);
            }

            Image[] images = instance.GetComponentsInChildren<Image>(true);
            Rect rootRect = rect == null ? new Rect(0f, 0f, width, height) : rect.rect;
            int hidden = 0;
            foreach (Image image in images)
            {
                string spriteName = image.sprite == null ? "" : image.sprite.name;
                Rect imageRect = image.rectTransform.rect;
                // The demo wallpaper and the panel's own flat surface both cover the whole sheet;
                // hiding them leaves the rows, labels and controls the comparison is about.
                bool coversEverything = Mathf.Abs(imageRect.width) >= Mathf.Abs(rootRect.width) * 0.85f &&
                                        Mathf.Abs(imageRect.height) >= Mathf.Abs(rootRect.height) * 0.85f;
                if (spriteName.IndexOf("Placeholder", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    spriteName.IndexOf("Background", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    coversEverything)
                {
                    image.gameObject.SetActive(false);
                    hidden++;
                }
            }

            SetLayer(canvasObject.transform);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)canvasObject.transform);
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            Texture2D texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
            texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            texture.Apply();
            RenderTexture.active = null;
            string path = Path.Combine(absoluteFolder, "heatui-panel.png");
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            return "captured Heat UI settings panel (" + hidden + " background images hidden) to " + path;
        }
        catch (Exception ex)
        {
            return "failed: " + ex;
        }
        finally
        {
            RenderTexture.active = null;
            if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
            if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            if (canvasObject != null) UnityEngine.Object.DestroyImmediate(canvasObject);
        }
    }

    /// <summary>
    /// Renders Heat UI's own prefabs into one sheet so the lobby can be compared against the real
    /// thing instead of against guesses about it. Nothing is instantiated into the open scene: the
    /// sheet lives on its own canvas and is destroyed before returning.
    /// </summary>
    public static string CaptureHeatUiReference(string outputFolder)
    {
        if (string.IsNullOrEmpty(outputFolder))
        {
            outputFolder = DefaultOutputFolder;
        }
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string absoluteFolder = Path.IsPathRooted(outputFolder) ? outputFolder : Path.Combine(projectRoot, outputFolder);
        Directory.CreateDirectory(absoluteFolder);

        GameObject canvasObject = null;
        GameObject cameraObject = null;
        RenderTexture target = null;
        try
        {
            canvasObject = new GameObject("HeatUI Reference Canvas", typeof(RectTransform));
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            cameraObject = new GameObject("HeatUI Reference Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.09f, 0.13f, 0.17f, 1f);
            camera.cullingMask = 1 << CaptureLayer;
            camera.orthographic = true;
            target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            target.Create();
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;

            string[,] sheet =
            {
                { "Button", "10", "-10", "200", "44" },
                { "Button (Panel Alt)", "10", "-70", "300", "50" },
                { "Close Button", "330", "-70", "60", "50" },
                { "Input Field", "10", "-140", "420", "70" },
                { "Dropdown", "450", "-140", "300", "70" },
                { "Settings Header", "10", "-230", "300", "40" },
                { "Settings Element (Switch)", "10", "-280", "560", "60" },
                { "Settings Element (Slider)", "10", "-350", "560", "60" },
                { "Settings Element (Dropdown)", "10", "-420", "560", "60" },
                { "Settings Element (Horizontal Selector)", "10", "-490", "560", "60" },
                { "Settings Layout", "600", "-140", "560", "520" }
            };

            int placed = 0;
            for (int i = 0; i < sheet.GetLength(0); i++)
            {
                string name = sheet[i, 0];
                GameObject prefab = Resources.Load<GameObject>("HeatUI/" + name);
                if (prefab == null)
                {
                    Debug.LogWarning("[OnlineLobbyPreview] missing prefab HeatUI/" + name);
                    continue;
                }
                GameObject instance = UnityEngine.Object.Instantiate(prefab, canvasObject.transform);
                instance.name = name;
                RectTransform rect = instance.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.anchorMin = new Vector2(0f, 1f);
                    rect.anchorMax = new Vector2(0f, 1f);
                    rect.pivot = new Vector2(0f, 1f);
                    rect.anchoredPosition = new Vector2(float.Parse(sheet[i, 1]), float.Parse(sheet[i, 2]));
                    rect.sizeDelta = new Vector2(float.Parse(sheet[i, 3]), float.Parse(sheet[i, 4]));
                }
                placed++;
            }

            SetLayer(canvasObject.transform);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)canvasObject.transform);
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            Texture2D texture = new Texture2D(Width, Height, TextureFormat.ARGB32, false);
            texture.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
            texture.Apply();
            RenderTexture.active = null;
            string path = Path.Combine(absoluteFolder, "heatui-reference.png");
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            return "captured " + placed + " Heat UI prefabs to " + path;
        }
        catch (Exception ex)
        {
            return "failed: " + ex;
        }
        finally
        {
            RenderTexture.active = null;
            if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
            if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            if (canvasObject != null) UnityEngine.Object.DestroyImmediate(canvasObject);
        }
    }

    /// <summary>
    /// Captures the in-room view (member list, chat, invite, host controls) by hiding the nav pages
    /// and showing OnlineRoomView directly. Everything is reached by reflection because the room
    /// view is internal to the runtime assembly.
    /// </summary>
    private static bool CaptureRoomView(OnlineLobbyWindow window, Canvas canvas, Camera camera,
        RenderTexture target, string absoluteFolder)
    {
        try
        {
            PropertyInfo property = typeof(OnlineLobbyWindow).GetProperty("RoomView",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            object roomView = property == null ? null : property.GetValue(window);
            if (roomView == null)
            {
                return false;
            }
            MethodInfo setVisible = roomView.GetType().GetMethod("SetVisible",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (setVisible == null)
            {
                return false;
            }
            setVisible.Invoke(roomView, new object[] { true });
            TMP_Text title = FindDescendant(canvas.transform, "PageTitle").GetComponent<TMP_Text>();
            title.text = "房间 / 集结队伍";

            Transform content = FindDescendant(canvas.transform, "Content");
            if (content != null)
            {
                for (int i = 0; i < content.childCount; i++)
                {
                    Transform child = content.GetChild(i);
                    if (child.name.StartsWith("Page_", StringComparison.Ordinal))
                    {
                        child.gameObject.SetActive(false);
                    }
                }
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)canvas.transform);
            Canvas.ForceUpdateCanvases();
            ApplyScrollbarVisibility(canvas.transform);
            Canvas.ForceUpdateCanvases();
            Canvas.ForceUpdateCanvases();

            camera.Render();
            RenderTexture.active = target;
            Texture2D texture = new Texture2D(Width, Height, TextureFormat.ARGB32, false);
            texture.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
            texture.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(absoluteFolder, "online-lobby-room.png"), texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[OnlineLobbyPreview] room view capture skipped: " + ex.Message);
            return false;
        }
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        if (root == null)
        {
            return null;
        }
        if (root.name == name)
        {
            return root;
        }
        for (int i = 0; i < root.childCount; i++)
        {
            Transform hit = FindDescendant(root.GetChild(i), name);
            if (hit != null)
            {
                return hit;
            }
        }
        return null;
    }

    /// <summary>
    /// The scrollbars use ScrollRect's AutoHide, which only ticks in Play Mode; in edit mode the
    /// capture would otherwise show a scrollbar on pages whose content already fits.
    /// </summary>
    private static void ApplyScrollbarVisibility(Transform root)
    {
        ScrollRect[] scrolls = root.GetComponentsInChildren<ScrollRect>(true);
        foreach (ScrollRect scroll in scrolls)
        {
            if (scroll.verticalScrollbar == null || scroll.viewport == null || scroll.content == null)
            {
                continue;
            }
            bool needed = scroll.content.rect.height > scroll.viewport.rect.height + 0.5f;
            scroll.verticalScrollbar.gameObject.SetActive(needed);
        }
    }

    /// <summary>Dumps every frame/outline image: a filled outline sprite shows up as a white box.</summary>
    private static void DumpOutlines(Transform root, string pageName, System.Text.StringBuilder builder)
    {
        Image[] images = root.GetComponentsInChildren<Image>(true);
        foreach (Image image in images)
        {
            if (!image.gameObject.activeInHierarchy)
            {
                continue;
            }
            string sprite = image.sprite == null ? "(none)" : image.sprite.name;
            bool interesting = image.name.IndexOf("Outline", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               image.name.IndexOf("Row", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               image.name.IndexOf("Border", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!interesting)
            {
                continue;
            }
            Vector4 border = image.sprite == null ? Vector4.zero : image.sprite.border;
            builder.AppendLine(string.Format(
                "[{0}] {1,-18} type={2,-7} size={3,5:0}x{4,-4:0} sprite={5} border=({6:0},{7:0},{8:0},{9:0}) color=#{10}",
                pageName, image.name, image.type, image.rectTransform.rect.width, image.rectTransform.rect.height,
                sprite, border.x, border.y, border.z, border.w,
                ColorUtility.ToHtmlStringRGBA(image.color)));
        }
    }

    /// <summary>Writes every label's rect, so a missing/zero-sized caption is visible in the report.</summary>
    private static void DumpLabels(Transform root, string pageName, System.Text.StringBuilder builder)
    {
        TMP_Text[] labels = root.GetComponentsInChildren<TMP_Text>(true);
        builder.AppendLine("=== " + pageName + ": " + labels.Length + " labels ===");
        foreach (TMP_Text label in labels)
        {
            if (!label.gameObject.activeInHierarchy)
            {
                continue;
            }
            RectTransform rect = label.rectTransform;
            Vector3 local = root.InverseTransformPoint(rect.position);
            string text = label.text ?? "";
            if (text.Length > 24)
            {
                text = text.Substring(0, 24) + "…";
            }
            builder.AppendLine(string.Format(
                "{0,-22} size={1,6:0}x{2,-5:0} at=({3,7:0},{4,-6:0}) color=#{5} text=\"{6}\"",
                label.name, rect.rect.width, rect.rect.height, local.x, local.y,
                ColorUtility.ToHtmlStringRGB(label.color), text));
        }
    }

    [MenuItem("Tools/Multiplayer/Capture Online Lobby Preview")]
    public static void Capture()
    {
        try
        {
            Debug.Log("[OnlineLobbyPreview] " + CaptureAll(DefaultOutputFolder));
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    /// <summary>Captures every lobby page and returns a one-line summary.</summary>
    public static string CaptureAtSize(string outputFolder, int width, int height)
    {
        int previousWidth = Width, previousHeight = Height;
        try { Width = width; Height = height; return CaptureAll(outputFolder); }
        finally { Width = previousWidth; Height = previousHeight; }
    }

    public static string CaptureAll(string outputFolder)
    {
        if (string.IsNullOrEmpty(outputFolder))
        {
            outputFolder = DefaultOutputFolder;
        }

        OnlineLobbyWindow window = OnlineLobbyWindow.GetOrCreate();
        if (window == null)
        {
            return "failed: could not create OnlineLobbyWindow";
        }

        Canvas canvas = null;
        GameObject cameraObject = null;
        RenderTexture target = null;
        try
        {
            MethodInfo ensureBuilt = typeof(OnlineLobbyWindow).GetMethod("EnsureBuilt",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (ensureBuilt == null)
            {
                return "failed: OnlineLobbyWindow.EnsureBuilt not found";
            }
            ensureBuilt.Invoke(window, null);
            if (!string.IsNullOrEmpty(window.LastBuildError))
            {
                return "failed: " + window.LastBuildError;
            }

            canvas = UnityEngine.Object.FindObjectsOfType<Canvas>(true)
                .FirstOrDefault(candidate => candidate.gameObject.name == CanvasName);
            if (canvas == null)
            {
                return "failed: no canvas named " + CanvasName;
            }

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string absoluteFolder = Path.IsPathRooted(outputFolder)
                ? outputFolder
                : Path.Combine(projectRoot, outputFolder);
            Directory.CreateDirectory(absoluteFolder);
            SetLayer(canvas.transform);

            cameraObject = new GameObject("Online Lobby Preview Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.06f, 0.09f, 1f);
            // A ScreenSpaceCamera canvas is still culled per layer, and the widgets are spread over
            // the default layer, so this camera sees everything. The window's full-frame backdrop
            // covers whatever else the open scene contains.
            camera.cullingMask = 1 << CaptureLayer;
            camera.orthographic = true;

            target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            target.Create();
            camera.targetTexture = target;

            RenderMode previousMode = canvas.renderMode;
            Camera previousCamera = canvas.worldCamera;
            float previousPlane = canvas.planeDistance;
            bool previousActive = canvas.gameObject.activeSelf;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            canvas.gameObject.SetActive(true);

            int captured = 0;
            System.Text.StringBuilder dump = new System.Text.StringBuilder();
            dump.AppendLine("Online lobby label layout - " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            foreach (string pageName in Pages)
            {
                OnlineLobbyWindow.Page page = (OnlineLobbyWindow.Page)Enum.Parse(
                    typeof(OnlineLobbyWindow.Page), pageName);
                window.ShowPage(page);
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)canvas.transform);
                Canvas.ForceUpdateCanvases();
                ApplyScrollbarVisibility(canvas.transform);
                Canvas.ForceUpdateCanvases();
                Canvas.ForceUpdateCanvases();
                DumpLabels(canvas.transform, pageName, dump);
                DumpOutlines(canvas.transform, pageName, dump);

                camera.Render();
                RenderTexture.active = target;
                Texture2D texture = new Texture2D(Width, Height, TextureFormat.ARGB32, false);
                texture.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
                texture.Apply();
                RenderTexture.active = null;

                string fileName = "online-lobby-" + pageName.ToLowerInvariant() + ".png";
                File.WriteAllBytes(Path.Combine(absoluteFolder, fileName), texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                captured++;
            }
            File.WriteAllText(Path.Combine(absoluteFolder, "online-lobby-text.txt"), dump.ToString(),
                new UTF8Encoding(false));

            // The in-room view is not a nav page, so it gets its own pass.
            if (CaptureRoomView(window, canvas, camera, target, absoluteFolder))
            {
                captured++;
            }

            // Preview-only fixture: no hosting, network connections or saved preferences.
            Transform invite = FindDescendant(canvas.transform, "InviteOverlay");
            invite.gameObject.SetActive(true);
            FindDescendant(invite, "CodeValue").GetComponent<TMP_Text>().text = "BP-7K4M-2XQ9";
            const string sampleLink = "bprc://join?c=BP-7K4M-2XQ9&a=192.0.2.1&p=7777";
            FindDescendant(invite, "LinkValue").GetComponent<TMP_Text>().text = sampleLink;
            FindDescendant(invite, "ConnectionInfo").GetComponent<TMP_Text>().text = "预览房间 · 直连模式\n房主地址：192.0.2.1:7777\n同一局域网可通过房间号加入；跨网络请分享可达的房主地址。";
            string qrError;
            Texture2D qr = BadPiggies.Multiplayer.Online.Qr.QrTexture.Create(sampleLink, 8, 4, null, null, out qrError);
            UnityEngine.Sprite qrSprite = UnityEngine.Sprite.Create(qr, new Rect(0,0,qr.width,qr.height), new Vector2(.5f,.5f));
            FindDescendant(invite, "QrImage").GetComponent<Image>().sprite = qrSprite;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            Texture2D inviteImage = new Texture2D(Width, Height, TextureFormat.ARGB32, false);
            inviteImage.ReadPixels(new Rect(0,0,Width,Height),0,0);
            inviteImage.Apply();
            File.WriteAllBytes(Path.Combine(absoluteFolder, "online-lobby-invite.png"), inviteImage.EncodeToPNG());
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(inviteImage);
            UnityEngine.Object.DestroyImmediate(qrSprite);
            UnityEngine.Object.DestroyImmediate(qr);
            captured++;

            canvas.renderMode = previousMode;
            canvas.worldCamera = previousCamera;
            canvas.planeDistance = previousPlane;
            canvas.gameObject.SetActive(previousActive);
            AssetDatabase.Refresh();

            return "captured " + captured + " page(s) to " + absoluteFolder;
        }
        catch (TargetInvocationException ex)
        {
            return "failed: " + (ex.InnerException ?? ex);
        }
        catch (Exception ex)
        {
            return "failed: " + ex;
        }
        finally
        {
            RenderTexture.active = null;
            if (cameraObject != null)
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
            if (target != null)
            {
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
            if (canvas != null)
            {
                UnityEngine.Object.DestroyImmediate(canvas.gameObject);
            }
            if (window != null)
            {
                UnityEngine.Object.DestroyImmediate(window.gameObject);
            }
        }
    }
}
