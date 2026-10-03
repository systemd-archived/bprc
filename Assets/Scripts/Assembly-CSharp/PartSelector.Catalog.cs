using UnityEngine;

public partial class PartSelector
{
    private const int CatalogBple = 0;
    private const int CatalogRcr2 = 1;
    private const int CatalogRc3 = 2;
    private const int CatalogRc1 = 3;

    private int m_catalog;
    private bool m_showCockpits;
    private Transform m_catalogTabs;
    private PartCatalogButton m_bpleTab, m_roverTab, m_rc3Tab, m_rc1Tab, m_catalogBack;

    /// <summary>选项卡底边和快捷皮肤面板顶边之间留的空隙（世界单位）。</summary>
    private const float CatalogTabsQuickGap = 0.15f;

    /// <summary>
    /// 快捷皮肤面板展开时选项卡临时抬高的量；面板没开时为 0（选项卡在原位）。
    /// 每帧重算，所以面板一关就会精确回到原来的高度。
    /// </summary>
    private float m_catalogTabsQuickLift;

    /// <summary>
    /// The catalogs share one part list, so the tab a part belongs to has to be
    /// read off the part itself. RC3 and RC1 parts are identified by the index
    /// their own catalog assigned (RC3PortCatalog / RC1PortCatalog keep that set)
    /// rather than by type: they are ordinary RCR2Port.RoverPortPart instances,
    /// deliberately, so that every "is this a ported part?" test in the game
    /// keeps working. Both range tests must come before the RoverPortPart
    /// fallback, otherwise the ported parts land in the RCR2 tab.
    /// </summary>
    private static int CatalogOf(BasePart part)
    {
        if (!part) return CatalogBple;
        // RC1 is asked first for continuity with how the tabs behaved before the predicates became
        // set-based: with IsRC1Index / IsRC3Index answering from each catalog's own assigned indices a
        // part belongs to at most one of them, so the order no longer changes the answer.
        if (RC3Port.RC1PortCatalog.IsRC1Index(part.Index)) return CatalogRc1;
        if (RC3Port.RC3PortCatalog.IsRC3Index(part.Index)) return CatalogRc3;
        return part is RCR2Port.RoverPortPart ? CatalogRcr2 : CatalogBple;
    }

    private bool IsRc3Catalog()
    {
        return m_catalog == CatalogRc3;
    }

    private bool IsInCurrentCatalog(BasePart part)
    {
        if (!part || CatalogOf(part) != m_catalog) return false;
        return !m_showCockpits || part is RCR2Port.RoverPortCockpitPart;
    }

    private bool CanChangeCatalog()
    {
        return m_constructionUI && !m_constructionUI.DisableFunctionality && !m_constructionUI.IsDragging() &&
            WPFMonoBehaviour.levelManager && WPFMonoBehaviour.levelManager.gameState == LevelManager.GameState.Building;
    }

    private void RevealCatalogPart(ConstructionUI.PartDesc descriptor)
    {
        if (descriptor == null || !descriptor.part) return;
        int catalog = CatalogOf(descriptor.part);
        bool cockpit = descriptor.part is RCR2Port.RoverPortCockpitPart;
        if (m_catalog == catalog && m_showCockpits == cockpit) return;
        m_catalog = catalog; m_showCockpits = cockpit;
        CreatePartList(false);
        UpdateCatalogTabs();
    }

    private void ChangeCatalog(int catalog, bool cockpits = false)
    {
        if (!CanChangeCatalog() || (m_catalog == catalog && m_showCockpits == cockpits)) return;
        m_catalog = catalog;
        m_showCockpits = cockpits;
        m_constructionUI.SelectPart(new ConstructionUI.PartDesc());
        CreatePartList(false);
        UpdateCatalogTabs();
    }

    private void EnsureCatalogTabs()
    {
        if (m_catalogTabs) return;
        m_catalogTabs = new GameObject("Part catalog tabs").transform;
        m_catalogTabs.SetParent(transform, false);
        var size = new Vector2(3.5f, 0.85f);
        m_bpleTab = PartCatalogButton.Create(m_catalogTabs, "BPLE 部件", size, () => ChangeCatalog(CatalogBple));
        m_roverTab = PartCatalogButton.Create(m_catalogTabs, "RCR2 部件", size, () => ChangeCatalog(CatalogRcr2));
        m_rc3Tab = PartCatalogButton.Create(m_catalogTabs, "RC3 部件", size, () => ChangeCatalog(CatalogRc3));
        m_rc1Tab = PartCatalogButton.Create(m_catalogTabs, "RC1 部件", size, () => ChangeCatalog(CatalogRc1));
        // The cockpit group is an RCR2 sub-view, so "返回部件" goes back to RCR2.
        m_catalogBack = PartCatalogButton.Create(m_catalogTabs, "返回部件", size, () => ChangeCatalog(CatalogRcr2));
        // Five buttons at most (BPLE, RCR2, RC3, RC1, 返回部件) are 3.5 wide, so
        // they are laid out on a 3.6 pitch: -3.6, 0, 3.6, 7.2, 10.8.
        m_bpleTab.transform.localPosition = new Vector3(-3.6f, 0, 0);
        m_roverTab.transform.localPosition = new Vector3(0f, 0, 0);
        m_rc3Tab.transform.localPosition = new Vector3(3.6f, 0, 0);
        m_rc1Tab.transform.localPosition = new Vector3(7.2f, 0, 0);
        // Clear of the RC1 tab: every button carries a BoxCollider, so overlapping
        // them would make each steal the other's clicks.
        m_catalogBack.transform.localPosition = new Vector3(10.8f, 0, 0);
        LayerHelper.SetLayer(m_catalogTabs.gameObject, m_scrollList.gameObject.layer, true);
    }

    private void UpdateCatalogTabs()
    {
        if (!m_catalogTabs || !m_scrollList || !m_partButtonPrefab) return;
        // The tray grows upwards when a second row is needed; keep the tabs above it.
        Vector3 position = m_scrollList.transform.position;
        float iconHeight = m_partButtonPrefab.GetComponent<Sprite>().Size.y;
        position.y += (m_scrollList.UsedRows - 1) * m_scrollList.offset.y +
            Mathf.Max(0f, (m_scrollList.offset.y - iconHeight) * 0.5f) + 0.65f;
        position.z -= 1f;
        // Buttons are 3.5 wide on a 3.6 pitch, so n visible buttons span
        // 3.6n - 0.1 units: 14.3 for the four tabs, 17.9 with "返回部件" too.
        // Their local centre sits at 1.8 * (n - 3), i.e. 1.8 and 3.6.
        bool back = m_showCockpits;
        float divisor = back ? 17.9f : 14.3f;
        float scale = Mathf.Min(1f, Mathf.Max(1f, 20f * Screen.width / Mathf.Max(1, Screen.height) - 2f) / divisor);
        m_catalogTabs.localScale = Vector3.one * scale;
        position.x -= (back ? 3.6f : 1.8f) * scale;

        // 快捷皮肤面板和选项卡占的是同一条横带，面板展开时选项卡要让到它上面去。
        // 让位距离是从“原位”算的：抬升只是整体平移，所以先在原位摆一次，CatalogTabsQuickLift
        // 读到的下沿才是没有抬升时的下沿（否则下一帧会把上一帧的抬升当成自己的高度，来回抖）。
        float naturalY = position.y;
        m_catalogTabs.position = position;
        m_catalogTabsQuickLift = CatalogTabsQuickLift();
        position.y = naturalY + m_catalogTabsQuickLift;
        m_catalogTabs.position = position;

        m_bpleTab.SetSelected(m_catalog == CatalogBple);
        m_roverTab.SetSelected(m_catalog == CatalogRcr2);
        m_rc3Tab.SetSelected(m_catalog == CatalogRc3);
        m_rc1Tab.SetSelected(m_catalog == CatalogRc1);
        m_catalogBack.gameObject.SetActive(back);
    }

    /// <summary>
    /// 选项卡要为快捷皮肤面板让出的高度，0 表示不用让（面板没开，或本来就不重合）。
    /// </summary>
    /// <remarks>
    /// 两边的尺寸都是算出来的，不去遍历子物体：这个方法每帧都会被 LateUpdate 调到，而
    /// GetComponentsInChildren 每次都要分配一个数组。用同一个 <see cref="CatalogTabHeight"/>
    /// 和面板自己的缩放算，结果和量出来的包围盒一致（按钮的碰撞体就是 0.85 高），
    /// 而且面板里当前皮肤放大 / 缩小的动画期间也不会让选项卡跟着抖。
    /// </remarks>
    private float CatalogTabsQuickLift()
    {
        if (m_quickRoot == null || m_quickSlotTfs.Count == 0) return 0f;
        float tabsBottom = TabsBottomY();
        float panelTop = QuickPanelTopY();
        return Mathf.Max(0f, panelTop + CatalogTabsQuickGap - tabsBottom);
    }

    /// <summary>选项卡按钮的高度（<see cref="EnsureCatalogTabs"/> 建出来时的尺寸）。</summary>
    private const float CatalogTabHeight = 0.85f;

    /// <summary>快捷皮肤面板背景条的高度（<see cref="CreateQuickBackgroundStrip"/> 里的 0.62）。</summary>
    private const float QuickStripHeight = 0.62f;

    /// <summary>选项卡在世界空间的下沿。</summary>
    private float TabsBottomY()
    {
        float scale = m_catalogTabs.localScale.y;
        return m_catalogTabs.position.y - CatalogTabHeight * scale * 0.5f;
    }

    /// <summary>
    /// 快捷皮肤面板在世界空间的上沿：整排皮肤里最高的那一个（当前使用中的皮肤会被放大，
    /// QuickCurrentZoom）和面板背景条，取更高的那个。
    /// </summary>
    private float QuickPanelTopY()
    {
        float half = 0f;
        if (m_partButtonPrefab != null)
        {
            Sprite icon = m_partButtonPrefab.GetComponent<Sprite>();
            if (icon != null) half = icon.Size.y * 0.5f;
        }
        // 皮肤个数少时面板会居中，最高点仍以行为中心对称，所以只比高度：最坏情况是当前皮肤
        // 被放大到 QuickCurrentZoom，其余按 1。
        float slots = half * m_quickSlotScale * QuickCurrentZoom;
        float strip = QuickStripHeight * 0.5f;
        return m_quickY + Mathf.Max(slots, strip);
    }

    private void AddCockpitGroup(BasePart representative)
    {
        // Only the RCR2 cockpits are grouped behind the button; the RC3 seats
        // are ordinary entries of the RC3 tab.
        int count = 0;
        foreach (var part in m_partDescs)
        {
            if (part.part is RCR2Port.RoverPortCockpitPart && CatalogOf(part.part) == CatalogRcr2) count++;
        }
        Vector3 size = m_partButtonPrefab.GetComponent<Sprite>().Size;
        var button = PartCatalogButton.Create(m_scrollList.transform, "驾驶舱 " + count + "款", new Vector2(size.x, size.y), () => ChangeCatalog(CatalogRcr2, true));
        button.SetLabelAtBottom();
        if (representative.m_constructionIconSprite)
        {
            var icon = Instantiate(representative.m_constructionIconSprite.gameObject, button.transform);
            icon.transform.localPosition = new Vector3(0, 0.15f, -0.25f);
            icon.transform.localScale = Vector3.one * 1.35f;
            ConstructionUI.SetSortingOrder(icon, 2, string.Empty);
        }
        LayerHelper.SetLayer(button.gameObject, m_scrollList.gameObject.layer, true);
        m_scrollList.AddButton(button);
    }
}
