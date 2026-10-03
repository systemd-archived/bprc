using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class INBlueprintPartEffect : MonoBehaviour
{
    private struct Draw
    {
        public Renderer renderer;
        public Material source;
        public int submesh;
        public float distance;
    }

    private static readonly HashSet<INBlueprintPartEffect> s_cameras = new HashSet<INBlueprintPartEffect>();
    private static bool s_requested;
    private static readonly int MainTex = Shader.PropertyToID("_MainTex");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private const CameraEvent MaskEvent = CameraEvent.BeforeImageEffects;

    private readonly List<Renderer> m_renderers = new List<Renderer>();
    private readonly List<Material> m_materials = new List<Material>();
    private readonly List<Draw> m_draws = new List<Draw>();
    private readonly Dictionary<Material, Material> m_masks = new Dictionary<Material, Material>();
    private readonly HashSet<Material> m_usedMaterials = new HashSet<Material>();
    private readonly List<Material> m_staleMaterials = new List<Material>();
    private readonly Plane[] m_frustum = new Plane[6];
    private Camera m_camera;
    private CommandBuffer m_commands;
    private RenderTexture m_mask;
    private RenderTexture m_scene;
    private Material m_composite;
    private Shader m_maskShader;
    private Texture2D m_palette;
    private Texture2D m_dither;
    private bool m_failed;

    public static INBlueprintPartEffect Attach(Camera camera)
    {
        if (camera == null) return null;
        var effect = camera.GetComponent<INBlueprintPartEffect>();
        if (effect == null) effect = camera.gameObject.AddComponent<INBlueprintPartEffect>();
        effect.m_camera = camera;
        s_cameras.Add(effect);
        effect.enabled = s_requested;
        return effect;
    }

    public static void SetEnabled(bool value)
    {
        s_requested = value;
        foreach (var effect in s_cameras)
            if (effect != null) effect.enabled = value;
        if (IngameCamera.Instance != null) Attach(IngameCamera.Instance.GetComponent<Camera>());
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_requested = false;
        s_cameras.Clear();
    }

    private void Awake()
    {
        m_camera = GetComponent<Camera>();
        s_cameras.Add(this);
    }

    private void OnEnable() { m_failed = false; }

    private bool PrepareResources()
    {
        if (m_composite != null) return true;
        if (m_failed) return false;
        m_maskShader = Resources.Load<Shader>("AeronauticsBlueprint/PartMask");
        Shader composite = Resources.Load<Shader>("AeronauticsBlueprint/BlueprintComposite");
        if (m_maskShader == null || composite == null || !m_maskShader.isSupported || !composite.isSupported)
        {
            m_failed = true;
            Debug.LogWarning("[Blueprint] Blueprint shaders are unavailable on this graphics device.", this);
            return false;
        }
        m_composite = new Material(composite) { hideFlags = HideFlags.HideAndDontSave };
        // Palette samples are stored as gamma-space data, just like Minecraft's RGBA8 lookup.
        uint[] colors = {
            0x303133, 0x393A3C, 0x595A57, 0x7A7973, 0x9A988E, 0xBBB7AA, 0xD4CFBF, 0xE1DCCB,
            0x282218, 0x3F3828, 0x564F36, 0x736B46, 0x928855, 0xAFA366, 0xC1B57C, 0xD3C791
        };
        m_palette = new Texture2D(8, 2, TextureFormat.RGBA32, false, true)
        {
            name = "Blueprint palette", hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
        };
        for (int i = 0; i < colors.Length; i++)
            m_palette.SetPixel(i % 8, i / 8, new Color32((byte)(colors[i] >> 16), (byte)(colors[i] >> 8), (byte)colors[i], 255));
        m_palette.Apply(false, true);
        m_dither = new Texture2D(32, 32, TextureFormat.RGBA32, false, true)
        {
            name = "Blueprint Bayer dither", hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Repeat
        };
        for (int y = 0; y < 32; y++)
        for (int x = 0; x < 32; x++)
        {
            int rank = 0;
            for (int bit = 0; bit < 4; bit++)
            {
                int xb = (x >> bit) & 1, yb = (y >> bit) & 1;
                rank += ((xb ^ yb) * 2 + yb) * (64 >> (bit * 2));
            }
            byte threshold = (byte)rank;
            m_dither.SetPixel(x, y, new Color32(threshold, threshold, threshold, 255));
        }
        m_dither.Apply(false, true);
        m_composite.SetTexture("_Palette", m_palette);
        m_composite.SetTexture("_Dither", m_dither);
        m_commands = new CommandBuffer { name = "Aeronautics blueprint part mask" };
        m_camera.AddCommandBuffer(MaskEvent, m_commands);
        return true;
    }

    private void Collect(Contraption machine)
    {
        if (machine == null || !machine.gameObject.activeInHierarchy) return;
        m_renderers.Clear();
        machine.GetComponentsInChildren(false, m_renderers);
        foreach (Renderer renderer in m_renderers)
        {
            if (!(renderer is MeshRenderer || renderer is SkinnedMeshRenderer || renderer is SpriteRenderer)
                || !renderer.enabled || renderer.forceRenderingOff
                || (m_camera.cullingMask & (1 << renderer.gameObject.layer)) == 0
                || renderer.GetComponentInParent<BasePart>() == null
                || renderer.GetComponentInParent<Canvas>() != null
                || !GeometryUtility.TestPlanesAABB(m_frustum, renderer.bounds)) continue;
            var filter = renderer.GetComponent<MeshFilter>();
            var skinned = renderer as SkinnedMeshRenderer;
            int submeshes = filter != null && filter.sharedMesh != null ? filter.sharedMesh.subMeshCount
                : skinned != null && skinned.sharedMesh != null ? skinned.sharedMesh.subMeshCount : 1;
            m_materials.Clear();
            renderer.GetSharedMaterials(m_materials);
            Vector3 delta = renderer.bounds.center - m_camera.transform.position;
            float distance = m_camera.orthographic ? Vector3.Dot(delta, m_camera.transform.forward) : delta.sqrMagnitude;
            for (int i = 0; i < m_materials.Count; i++)
            {
                if (m_materials[i] == null) continue;
                m_draws.Add(new Draw { renderer = renderer, source = m_materials[i], submesh = Mathf.Min(i, submeshes - 1), distance = distance });
            }
        }
    }

    private static int CompareDraws(Draw a, Draw b)
    {
        int result = a.source.renderQueue.CompareTo(b.source.renderQueue);
        if (result == 0) result = SortingLayer.GetLayerValueFromID(a.renderer.sortingLayerID).CompareTo(SortingLayer.GetLayerValueFromID(b.renderer.sortingLayerID));
        if (result == 0) result = a.renderer.sortingOrder.CompareTo(b.renderer.sortingOrder);
        if (result == 0) result = b.distance.CompareTo(a.distance);
        if (result == 0) result = a.renderer.GetInstanceID().CompareTo(b.renderer.GetInstanceID());
        return result == 0 ? a.submesh.CompareTo(b.submesh) : result;
    }

    private void OnPreCull()
    {
        if (m_camera == null) m_camera = GetComponent<Camera>();
        if (!PrepareResources()) return;
        m_commands.Clear();
        int width = Mathf.Max(1, m_camera.scaledPixelWidth), height = Mathf.Max(1, m_camera.scaledPixelHeight);
        int samples = m_camera.targetTexture != null ? m_camera.targetTexture.antiAliasing
            : m_camera.allowMSAA && m_camera.actualRenderingPath == RenderingPath.Forward ? Mathf.Max(1, QualitySettings.antiAliasing) : 1;
        if (m_mask == null || m_mask.width != width || m_mask.height != height || m_mask.antiAliasing != samples)
        {
            DisposeObject(m_mask);
            DisposeObject(m_scene);
            var format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf) ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32;
            m_mask = new RenderTexture(width, height, 0, format, RenderTextureReadWrite.Linear)
            {
                name = "Blueprint part depth and coverage", hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point, antiAliasing = samples
            };
            m_mask.Create();
            m_scene = new RenderTexture(width, height, 0, m_camera.allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.ARGB32)
            {
                name = "Blueprint scene color", hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear
            };
            m_scene.Create();
        }
        GeometryUtility.CalculateFrustumPlanes(m_camera, m_frustum);
        m_draws.Clear();
        LevelManager level = WPFMonoBehaviour.levelManager;
        if (level != null && level.CurrentGameMode != null)
        {
            Collect(level.ContraptionProto);
            if (level.ContraptionRunning != level.ContraptionProto) Collect(level.ContraptionRunning);
        }
        m_draws.Sort(CompareDraws);
        if (m_draws.Count == 0) return;
        // Save and restore camera color around the mask's shared scene-depth attachment.
        m_commands.Blit(BuiltinRenderTextureType.CurrentActive, m_scene);
        m_commands.SetRenderTarget(m_mask, BuiltinRenderTextureType.CameraTarget);
        m_commands.ClearRenderTarget(false, true, new Color(1, 0, 0, 0));
        m_usedMaterials.Clear();
        foreach (Draw draw in m_draws)
        {
            if (m_usedMaterials.Add(draw.source))
            {
                if (!m_masks.TryGetValue(draw.source, out Material mask))
                {
                    mask = new Material(m_maskShader) { hideFlags = HideFlags.HideAndDontSave };
                    m_masks.Add(draw.source, mask);
                }
                mask.SetTexture(MainTex, draw.source.HasProperty(MainTex) ? draw.source.GetTexture(MainTex) : Texture2D.whiteTexture);
                mask.SetColor(ColorId, draw.source.HasProperty(ColorId) ? draw.source.GetColor(ColorId) : Color.white);
                mask.SetTexture("_AlphaTex", draw.source.HasProperty("_AlphaTex") ? draw.source.GetTexture("_AlphaTex") : Texture2D.whiteTexture);
                mask.SetFloat("_EnableExternalAlpha", draw.source.HasProperty("_EnableExternalAlpha") ? draw.source.GetFloat("_EnableExternalAlpha") : 0);
                if (draw.source.IsKeywordEnabled("ETC1_EXTERNAL_ALPHA")) mask.EnableKeyword("ETC1_EXTERNAL_ALPHA");
                else mask.DisableKeyword("ETC1_EXTERNAL_ALPHA");
                if (draw.source.HasProperty(MainTex))
                {
                    mask.SetTextureScale("_MainTex", draw.source.GetTextureScale("_MainTex"));
                    mask.SetTextureOffset("_MainTex", draw.source.GetTextureOffset("_MainTex"));
                }
            }
            // DrawRenderer preserves the renderer's property block and animated/skinned geometry.
            m_commands.DrawRenderer(draw.renderer, m_masks[draw.source], draw.submesh, 0);
        }
        m_staleMaterials.Clear();
        foreach (var pair in m_masks)
            if (!m_usedMaterials.Contains(pair.Key)) m_staleMaterials.Add(pair.Key);
        foreach (Material source in m_staleMaterials)
        {
            DisposeObject(m_masks[source]);
            m_masks.Remove(source);
        }
        m_composite.SetTexture("_BlueprintMask", m_mask);
        m_commands.Blit(m_scene, BuiltinRenderTextureType.CameraTarget, m_composite);
    }

    private static void DisposeObject(Object value)
    {
        if (value == null) return;
        var texture = value as RenderTexture;
        if (texture != null) texture.Release();
        if (Application.isPlaying) Destroy(value);
        else DestroyImmediate(value);
    }

    private void OnDisable()
    {
        if (m_commands != null)
        {
            if (m_camera != null) m_camera.RemoveCommandBuffer(MaskEvent, m_commands);
            m_commands.Release();
            m_commands = null;
        }
        foreach (Material mask in m_masks.Values) DisposeObject(mask);
        m_masks.Clear();
        DisposeObject(m_composite); m_composite = null;
        DisposeObject(m_mask); m_mask = null;
        DisposeObject(m_scene); m_scene = null;
        DisposeObject(m_palette); m_palette = null;
        DisposeObject(m_dither); m_dither = null;
        m_draws.Clear();
        m_renderers.Clear();
        m_materials.Clear();
        m_usedMaterials.Clear();
        m_staleMaterials.Clear();
    }

    private void OnDestroy()
    {
        OnDisable();
        s_cameras.Remove(this);
    }
}
