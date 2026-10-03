using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UButton = UnityEngine.UI.Button;

/// <summary>Construction-only layer focus. Original property blocks are restored before simulation.</summary>
public static class ContraptionLayerView
{
    private sealed class Saved { public Renderer renderer; public MaterialPropertyBlock original; }
    private static readonly List<Saved> saved = new List<Saved>();
    private static readonly List<Renderer> renderers = new List<Renderer>();
    private static readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
    public static void Clear()
    {
        foreach (var item in saved) if (item.renderer != null) item.renderer.SetPropertyBlock(item.original);
        saved.Clear();
    }
    public static void Refresh(Contraption owner)
    {
        Clear();
        if (owner == null || owner.IsRunning || !ContraptionLayers.Enabled(owner) || ContraptionLayers.ActiveLayer == 0) return;
        foreach (var part in owner.Parts)
        {
            if (part == null || PartPhysicsParameters.Layer(part) == ContraptionLayers.ActiveLayer) continue;
            renderers.Clear(); part.GetComponentsInChildren(true, renderers);
            foreach (var renderer in renderers)
            {
                var material = renderer.sharedMaterial;
                if (material == null || !material.HasProperty("_Color")) continue;
                var original = new MaterialPropertyBlock(); renderer.GetPropertyBlock(original);
                renderer.GetPropertyBlock(block);
                var color = original.HasColor("_Color") ? original.GetColor("_Color") : material.GetColor("_Color");
                color.r *= 0.85f; color.g *= 0.88f; color.b *= 0.95f; color.a *= 0.3f;
                block.SetColor("_Color", color); renderer.SetPropertyBlock(block);
                saved.Add(new Saved { renderer = renderer, original = original });
                block.Clear();
            }
        }
    }
}

public sealed class INBuildLayerSelector : MonoBehaviour
{
    private readonly List<Image> m_buttons = new List<Image>();
    private Contraption m_owner;
    private int m_count = -1, m_layer = -1, m_revision = -1;
    private GameObject m_row;
    public static void Install(GameObject toolbar) => toolbar.AddComponent<INBuildLayerSelector>().Build();
    private void Build()
    {
        m_row = new GameObject("StructuralLayers", typeof(RectTransform), typeof(Image));
        m_row.transform.SetParent(transform, false);
        var rect = m_row.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f); rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0, -122); rect.sizeDelta = new Vector2(564, 32);
        m_row.GetComponent<Image>().color = INBuildStyle.Panel;
        for (int layer = 0; layer <= 6; layer++)
        {
            int index = layer;
            var go = new GameObject("Layer_" + layer, typeof(RectTransform), typeof(Image), typeof(UButton));
            go.transform.SetParent(m_row.transform, false);
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f);
            rt.pivot = new Vector2(0, 0.5f); rt.anchoredPosition = new Vector2(4 + layer * 80, 0); rt.sizeDelta = new Vector2(76, 26);
            var bg = go.GetComponent<Image>(); m_buttons.Add(bg);
            var button = go.GetComponent<UButton>(); button.targetGraphic = bg; INBuildStyle.ButtonColors(button);
            button.onClick.AddListener(() => ContraptionLayers.Select(index));
            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text)); textGo.transform.SetParent(go.transform, false);
            var text = textGo.GetComponent<Text>(); text.font = INUiUtil.UiFont; text.fontSize = 13;
            text.text = layer == 0 ? "全部(查看)" : layer == 1 ? "1 公共层" : "图层 " + layer;
            text.color = INBuildStyle.Ivory; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            var tr = text.rectTransform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;
        }
        m_row.SetActive(false);
    }
    private void Update()
    {
        var owner = WPFMonoBehaviour.levelManager?.ContraptionProto;
        bool visible = INBuildToolPalette.IsBuildState() && ContraptionLayers.Enabled(owner);
        if (m_row.activeSelf != visible) m_row.SetActive(visible);
        if (!visible) { if (m_owner != null) ContraptionLayerView.Clear(); m_owner = null; return; }
        if (m_owner != owner || m_count != owner.Parts.Count || m_layer != ContraptionLayers.ActiveLayer || m_revision != ContraptionLayers.Revision)
        {
            m_owner = owner; m_count = owner.Parts.Count; m_layer = ContraptionLayers.ActiveLayer; m_revision = ContraptionLayers.Revision;
            ContraptionLayerView.Refresh(owner);
            for (int i = 0; i < m_buttons.Count; i++) m_buttons[i].color = i == m_layer ? INBuildStyle.Brass : INBuildStyle.Button;
        }
    }
    private void OnDisable()
    {
        ContraptionLayerView.Clear(); m_owner = null; m_revision = -1;
    }
}
