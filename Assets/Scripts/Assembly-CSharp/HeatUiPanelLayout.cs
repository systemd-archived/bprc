using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class HeatUiPanelLayout
{
    private const string LayoutPath = "HeatUI/Settings Layout";
    private const string CategoryButtonPath = "HeatUI/Button (Panel Alt)";
    private const string PanelContent = "Content/Panel Content";
    private const string DemoList = PanelContent + "/Panels/General/Content/List/Layout Group";

    public static TMP_Text Install(GameObject panel, GameObject main, GameObject sidebar,
        GameObject actionBar, UnityEngine.UI.Button backButton, UnityEngine.UI.Button menuButton,
        out GameObject layout)
    {
        layout = null;
        GameObject template = Resources.Load<GameObject>(LayoutPath);
        if (template == null)
        {
            return null;
        }

        layout = Object.Instantiate(template, panel.transform, false);
        foreach (Michsky.UI.Heat.PanelManager manager in layout.GetComponentsInChildren<Michsky.UI.Heat.PanelManager>(true))
        {
            manager.enabled = false;
            if (Application.isPlaying) { Object.Destroy(manager); }
            else { Object.DestroyImmediate(manager); }
        }
        foreach (Michsky.UI.Heat.SettingsDescriptionManager manager in layout.GetComponentsInChildren<Michsky.UI.Heat.SettingsDescriptionManager>(true))
        {
            manager.useLocalization = false;
            manager.enabled = false;
        }
        Transform content = layout.transform.Find(PanelContent);
        Transform categories = content != null ? content.Find("Categories") : null;
        Transform panels = content != null ? content.Find("Panels") : null;
        Transform hotkeys = layout.transform.Find("Content/Hotkeys");
        Transform description = content != null ? content.Find("Description Area") : null;
        if (categories == null || panels == null || hotkeys == null || description == null)
        {
            Remove(layout);
            layout = null;
            return null;
        }

        ClearChildren(categories);
        ClearChildren(hotkeys);
        for (int index = panels.childCount - 1; index >= 0; index--)
        {
            Transform child = panels.GetChild(index);
            if (child.name != "Background")
            {
                child.gameObject.SetActive(false);
                Remove(child.gameObject);
            }
        }

        Transform originalBackground = main.transform.Find("Background");
        if (originalBackground != null)
        {
            originalBackground.gameObject.SetActive(false);
        }

        Stretch(main.transform, panels);
        Stretch(sidebar.transform, categories);
        LayoutGroup categoryLayout = categories.GetComponent<LayoutGroup>();
        if (categoryLayout != null)
        {
            categoryLayout.enabled = false;
        }
        Image sidebarImage = sidebar.GetComponent<Image>();
        if (sidebarImage != null)
        {
            sidebarImage.enabled = false;
        }
        Mask sidebarMask = sidebar.GetComponent<Mask>();
        if (sidebarMask != null)
        {
            sidebarMask.enabled = false;
        }
        VerticalLayoutGroup vertical = sidebar.GetComponent<VerticalLayoutGroup>();
        if (vertical != null)
        {
            Object.DestroyImmediate(vertical);
        }
        HorizontalLayoutGroup horizontal = sidebar.GetComponent<HorizontalLayoutGroup>();
        if (horizontal == null) { horizontal = sidebar.AddComponent<HorizontalLayoutGroup>(); }
        horizontal.childAlignment = TextAnchor.MiddleLeft;
        horizontal.spacing = 0;
        horizontal.padding = new RectOffset();
        horizontal.childControlWidth = true;
        horizontal.childControlHeight = true;
        horizontal.childForceExpandWidth = false;
        horizontal.childForceExpandHeight = true;
        foreach (Transform item in sidebar.transform)
        {
            if (item.GetComponent<AppInterfaceSidebarItem>() == null)
            {
                LayoutElement ignored = item.GetComponent<LayoutElement>();
                if (ignored == null) { ignored = item.gameObject.AddComponent<LayoutElement>(); }
                ignored.ignoreLayout = true;
                continue;
            }
            if (item.gameObject.activeSelf)
            {
                PositionCategory(item);
            }
        }

        Transform oldBackground = actionBar.transform.Find("Background");
        if (oldBackground != null)
        {
            oldBackground.gameObject.SetActive(false);
        }
        Transform oldTitle = actionBar.transform.Find("Text");
        if (oldTitle != null)
        {
            oldTitle.gameObject.SetActive(false);
        }
        Stretch(actionBar.transform, hotkeys);
        LayoutGroup hotkeyLayout = hotkeys.GetComponent<LayoutGroup>();
        if (hotkeyLayout != null)
        {
            hotkeyLayout.enabled = false;
        }
        actionBar.SetActive(false);
        hotkeys.gameObject.SetActive(false);
        GameObject closeTemplate = Resources.Load<GameObject>("HeatUI/Close Button");
        if (closeTemplate != null)
        {
            GameObject close = Object.Instantiate(closeTemplate, panel.transform, false);
            close.name = "HeatCloseButton";
            var closeManager = close.GetComponent<Michsky.UI.Heat.ButtonManager>();
            closeManager.useSounds = false;
            closeManager.onClick = new UnityEngine.Events.UnityEvent();
            closeManager.onClick.AddListener(backButton.onClick.Invoke);
            close.SetActive(true);
        }

        CanvasGroup group = layout.GetComponent<CanvasGroup>();
        if (group != null)
        {
            group.alpha = 1;
            group.interactable = true;
            group.blocksRaycasts = true;
        }
        Animator animator = layout.GetComponent<Animator>();
        if (animator != null)
        {
            animator.enabled = false;
        }
        Transform title = description.Find("Title");
        TMP_Text titleText = title != null ? title.GetComponent<TMP_Text>() : null;
        if (titleText != null)
        {
            titleText.text = string.Empty;
        }
        Transform descriptionLabel = description.Find("Description");
        TMP_Text descriptionText = descriptionLabel != null ? descriptionLabel.GetComponent<TMP_Text>() : null;
        if (descriptionText != null)
        {
            descriptionText.text = string.Empty;
        }
        HeatUiOptionHelpView help = layout.AddComponent<HeatUiOptionHelpView>();
        help.Initialize(titleText, descriptionText);

        main.transform.SetAsLastSibling();
        sidebar.transform.SetAsLastSibling();
        actionBar.transform.SetAsLastSibling();
        return titleText;
    }

    public static void ConfigureList(GameObject content)
    {
        GameObject template = Resources.Load<GameObject>(LayoutPath);
        Transform source = template != null ? template.transform.Find(DemoList) : null;
        VerticalLayoutGroup sourceLayout = source != null ? source.GetComponent<VerticalLayoutGroup>() : null;
        VerticalLayoutGroup destination = content != null ? content.GetComponent<VerticalLayoutGroup>() : null;
        if (sourceLayout == null || destination == null)
        {
            return;
        }

        RectOffset padding = sourceLayout.padding;
        destination.padding = new RectOffset(padding.left, padding.right, padding.top, padding.bottom);
        destination.spacing = sourceLayout.spacing;
        destination.childAlignment = sourceLayout.childAlignment;
        destination.childControlWidth = sourceLayout.childControlWidth;
        destination.childControlHeight = sourceLayout.childControlHeight;
        destination.childForceExpandWidth = sourceLayout.childForceExpandWidth;
        destination.childForceExpandHeight = sourceLayout.childForceExpandHeight;
        destination.childScaleWidth = sourceLayout.childScaleWidth;
        destination.childScaleHeight = sourceLayout.childScaleHeight;
    }

    public static bool AttachElement(GameObject element)
    {
        GameObject template = Resources.Load<GameObject>(LayoutPath);
        Transform source = template != null ? template.transform.Find(PanelContent + "/Panels/General") : null;
        Transform oldPanel = element.transform.Find("Panel");
        Transform content = oldPanel != null ? oldPanel.Find("Scroll View/Viewport/Content") : null;
        Transform commandPage = element.transform.Find("MainPage");
        if (source == null || (content == null && commandPage == null))
        {
            return false;
        }

        GameObject page = Object.Instantiate(source.gameObject, element.transform, false);
        page.name = "HeatPage";
        page.SetActive(false);
        CanvasGroup pageGroup = page.GetComponent<CanvasGroup>();
        if (pageGroup != null)
        {
            pageGroup.alpha = 1;
            pageGroup.interactable = true;
            pageGroup.blocksRaycasts = true;
        }
        Animator pageAnimator = page.GetComponent<Animator>();
        if (pageAnimator != null) { pageAnimator.enabled = false; }
        if (content == null)
        {
            Transform exampleContent = page.transform.Find("Content");
            if (exampleContent != null)
            {
                exampleContent.gameObject.SetActive(false);
            }
            Stretch(commandPage, page.transform);
            Transform noticePage = element.transform.Find("NoticePage");
            if (noticePage != null)
            {
                Stretch(noticePage, page.transform);
            }
            Transform background = commandPage.Find("Background");
            if (background != null)
            {
                background.gameObject.SetActive(false);
            }
            HeatUiLegacyPanelAdapter commandAdapter = element.AddComponent<HeatUiLegacyPanelAdapter>();
            commandAdapter.Initialize(commandPage);
            return true;
        }
        Transform list = page.transform.Find("Content/List/Layout Group");
        if (list == null)
        {
            Remove(page);
            return false;
        }

        for (int index = list.childCount - 1; index >= 0; index--)
        {
            GameObject child = list.GetChild(index).gameObject;
            child.SetActive(false);
            Remove(child);
        }

        RectTransform listRect = list as RectTransform;
        RectTransform contentRect = content as RectTransform;
        content.SetParent(list.parent, false);
        if (listRect != null && contentRect != null)
        {
            HeatUiSkin.CopyGeometry(content, list);
        }
        content.SetSiblingIndex(list.GetSiblingIndex());
        list.gameObject.SetActive(false);
        Remove(list.gameObject);
        ConfigureList(content.gameObject);

        ScrollRect scroll = page.GetComponentInChildren<ScrollRect>(true);
        if (scroll != null && contentRect != null)
        {
            scroll.content = contentRect;
        }
        CanvasGroup group = page.GetComponent<CanvasGroup>();
        if (group != null)
        {
            group.alpha = 1;
            group.interactable = true;
            group.blocksRaycasts = true;
        }
        Animator animator = page.GetComponent<Animator>();
        if (animator != null)
        {
            animator.enabled = false;
        }
        oldPanel.gameObject.SetActive(false);
        HeatUiLegacyPanelAdapter adapter = element.AddComponent<HeatUiLegacyPanelAdapter>();
        adapter.Initialize(content);
        return true;
    }

    public static void SetElementActive(GameObject element, bool active)
    {
        Transform page = element.transform.Find("HeatPage");
        if (page != null)
        {
            page.gameObject.SetActive(active);
        }
        else
        {
            element.SetChildrenActive(active);
        }
    }

    public static Michsky.UI.Heat.PanelButton AddCategoryButton(GameObject item, Toggle toggle, Text caption)
    {
        GameObject template = Resources.Load<GameObject>(CategoryButtonPath);
        if (template == null)
        {
            return null;
        }

        PositionCategory(item.transform);
        foreach (Graphic original in item.GetComponentsInChildren<Graphic>(true)) { original.enabled = false; }
        toggle.transition = Selectable.Transition.None;
        toggle.graphic = null;

        GameObject buttonObject = Object.Instantiate(template, item.transform, false);
        HeatUiCategorySize categorySize = item.AddComponent<HeatUiCategorySize>();
        categorySize.Button = (RectTransform)buttonObject.transform;
        RectTransform buttonRect = buttonObject.transform as RectTransform;
        if (buttonRect != null)
        {
            buttonRect.anchorMin = new Vector2(0, 0);
            buttonRect.anchorMax = new Vector2(0, 1);
            buttonRect.pivot = new Vector2(0, 0.5f);
            buttonRect.anchoredPosition = Vector2.zero;
            buttonRect.sizeDelta = Vector2.zero;
        }

        Michsky.UI.Heat.PanelButton panelButton = buttonObject.GetComponent<Michsky.UI.Heat.PanelButton>();
        if (panelButton != null)
        {
            panelButton.useLocalization = false;
            panelButton.useSounds = false;
            panelButton.useSeperator = false;
            panelButton.buttonIcon = HeatUiOptionHelp.IconFor(caption.GetComponent<UITextLocale>()?.ID);
            panelButton.buttonText = caption.text;
            panelButton.UpdateUI();
            panelButton.onClick.AddListener(() => toggle.isOn = true);
            toggle.onValueChanged.AddListener(value =>
            {
                if (panelButton.gameObject.activeInHierarchy)
                {
                    panelButton.SetSelected(value);
                }
                else
                {
                    panelButton.isSelected = value;
                }
            });
            panelButton.isSelected = toggle.isOn;
            if (panelButton.gameObject.activeInHierarchy)
            {
                panelButton.SetSelected(toggle.isOn);
            }
        }
        Image image = item.GetComponent<Image>();
        if (image != null)
        {
            image.enabled = false;
        }
        caption.enabled = false;
        return panelButton;
    }

    private static void PlaceHotkey(Transform button, int index)
    {
        RectTransform rect = button as RectTransform;
        if (rect == null)
        {
            return;
        }
        rect.anchorMin = new Vector2(0, 0.5f);
        rect.anchorMax = new Vector2(0, 0.5f);
        rect.pivot = new Vector2(0, 0.5f);
        rect.sizeDelta = new Vector2(200, 50);
        rect.anchoredPosition = new Vector2(index * 200, 0);
    }

    private static void PositionCategory(Transform item)
    {
        RectTransform rect = item as RectTransform;
        if (rect == null)
        {
            return;
        }
        rect.anchorMin = new Vector2(0, 0.5f);
        rect.anchorMax = new Vector2(0, 0.5f);
        rect.pivot = new Vector2(0, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(190, 50);
        LayoutElement sizing = item.GetComponent<LayoutElement>();
        if (sizing == null) { sizing = item.gameObject.AddComponent<LayoutElement>(); }
        sizing.preferredWidth = 190;
        sizing.minWidth = 150;
        sizing.flexibleWidth = 0;
        sizing.preferredHeight = 50;
    }

    private static void Stretch(Transform child, Transform parent)
    {
        child.SetParent(parent, false);
        RectTransform rect = child as RectTransform;
        if (rect == null)
        {
            return;
        }
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void ClearChildren(Transform parent)
    {
        for (int index = parent.childCount - 1; index >= 0; index--)
        {
            GameObject child = parent.GetChild(index).gameObject;
            child.SetActive(false);
            Remove(child);
        }
    }

    private static void Remove(GameObject target)
    {
        if (Application.isPlaying)
        {
            Object.Destroy(target);
        }
        else
        {
            Object.DestroyImmediate(target);
        }
    }
}
