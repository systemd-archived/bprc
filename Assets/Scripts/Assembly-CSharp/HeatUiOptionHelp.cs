using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class HeatUiOptionHelp : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    private static readonly Dictionary<string, string[]> s_entries = new Dictionary<string, string[]>();
    private string m_key;
    private string m_title;
    private HeatUiOptionHelpView m_view;

    public static bool Chinese => INUnity.Language == SystemLanguage.Chinese || INUnity.Language == SystemLanguage.ChineseSimplified;

    public static void Bind(GameObject row, string key, string title)
    {
        if (row == null) { return; }
        HeatUiOptionHelp help = row.GetComponent<HeatUiOptionHelp>();
        if (help == null) { help = row.AddComponent<HeatUiOptionHelp>(); }
        help.m_key = key;
        help.m_title = title;
        var original = row.GetComponent<Michsky.UI.Heat.SettingsDescription>();
        if (original != null)
        {
            original.enabled = false;
            if (Application.isPlaying) { Destroy(original); }
            else { DestroyImmediate(original); }
        }
        var element = row.GetComponent<Michsky.UI.Heat.SettingsElement>();
        if (element != null) { element.useSounds = false; }
        AddIcon(row, key);
    }

    public static string Description(string key, string title)
    {
        if (s_entries.Count == 0)
        {
            foreach (string resource in new[] { "Feature Help", "Option Help" })
            {
                TextAsset asset = Resources.Load<TextAsset>("HeatUI/" + resource);
                if (asset == null) { continue; }
                foreach (string line in asset.text.Split('\n'))
                {
                    string[] fields = line.TrimEnd('\r').Split('\t');
                    if (fields.Length == 3) { s_entries[fields[0]] = fields; }
                }
            }
        }
        string[] entry;
        if (key != null && s_entries.TryGetValue(key, out entry)) { return entry[Chinese ? 1 : 2]; }
        return Chinese ? "调整“" + title + "”。修改后使用保存按钮保留设置，重置可恢复默认值。" :
            "Adjust " + title + ". Save to keep your changes; reset restores the default value.";
    }

    public static UnityEngine.Sprite IconFor(string key)
    {
        string icon = "Settings";
        if (!string.IsNullOrEmpty(key))
        {
            if (key.Contains("Data") || key.Contains("FPS") || key.Contains("Memory")) { icon = "Information"; }
            else if (key.Contains("Background") || key.Contains("Color") || key.Contains("Scale") || key.Contains("Custom")) { icon = "Visuals"; }
            else if (key.Contains("Volume") || key.Contains("Music")) { icon = "Audio"; }
            else if (key.Contains("Command") || key.Contains("Button") || key.Contains("ToolBook")) { icon = "Controls"; }
            else if (key.Contains("Addon")) { icon = "Extras"; }
        }
        return Resources.Load<UnityEngine.Sprite>("HeatUI/Icons/" + icon);
    }

    private static void AddIcon(GameObject row, string key)
    {
        if (row.GetComponent<Michsky.UI.Heat.SettingsElement>() == null || row.transform.Find("OptionIcon") != null) { return; }
        UnityEngine.Sprite sprite = IconFor(key);
        Transform label = HeatUiSkin.FindLabel(row, HeatUiSkin.HeatLabelChild);
        if (sprite == null || label == null) { return; }
        GameObject icon = new GameObject("OptionIcon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(row.transform, false);
        RectTransform rect = (RectTransform)icon.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0, 0.5f);
        rect.anchoredPosition = new Vector2(30, 0);
        rect.sizeDelta = new Vector2(22, 22);
        Image image = icon.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        RectTransform labelRect = label as RectTransform;
        if (labelRect != null) { labelRect.offsetMin = new Vector2(52, labelRect.offsetMin.y); }
    }

    private void Show()
    {
        if (m_view == null) { m_view = GetComponentInParent<HeatUiOptionHelpView>(); }
        if (m_view != null) { m_view.Show(m_title, Description(m_key, m_title)); }
    }

    public void OnPointerEnter(PointerEventData data) { Show(); }
    public void OnSelect(BaseEventData data) { Show(); }
    public void OnPointerExit(PointerEventData data) { if (m_view != null) { m_view.ResetHelp(); } }
    public void OnDeselect(BaseEventData data) { if (m_view != null) { m_view.ResetHelp(); } }
}
