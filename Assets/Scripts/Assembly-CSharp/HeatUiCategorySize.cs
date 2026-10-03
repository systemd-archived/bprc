using UnityEngine;
using UnityEngine.UI;

public sealed class HeatUiCategorySize : MonoBehaviour
{
    public RectTransform Button;
    private LayoutElement m_size;

    private void LateUpdate()
    {
        if (Button == null) { return; }
        if (m_size == null) { m_size = GetComponent<LayoutElement>(); }
        float width = LayoutUtility.GetPreferredWidth(Button);
        if (width <= 0) { return; }
        if (!Mathf.Approximately(m_size.preferredWidth, width))
        {
            m_size.minWidth = width;
            m_size.preferredWidth = width;
            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform.parent);
        }
    }
}
