using UnityEngine;

// A page/group changing its children invalidates discovery; an idle page needs no hierarchy scan.
public sealed class HeatUiHierarchyWatcher : MonoBehaviour
{
    public HeatUiLegacyPanelAdapter Owner;

    private void OnTransformChildrenChanged()
    {
        if (Owner != null) Owner.InvalidateStyles();
    }

    private void OnEnable()
    {
        if (Owner != null) Owner.InvalidateStyles();
    }
}
