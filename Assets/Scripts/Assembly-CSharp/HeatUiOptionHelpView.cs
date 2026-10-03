using TMPro;
using UnityEngine;

public sealed class HeatUiOptionHelpView : MonoBehaviour
{
    private TMP_Text m_title;
    private TMP_Text m_description;
    private string m_pageTitle;

    public void Initialize(TMP_Text title, TMP_Text description)
    {
        m_title = title;
        m_description = description;
    }

    public void SetPage(string title)
    {
        m_pageTitle = title;
        ResetHelp();
    }

    public void Show(string title, string description)
    {
        if (m_title != null) { m_title.text = title; }
        if (m_description != null) { m_description.text = description; }
    }

    public void ResetHelp()
    {
        Show(m_pageTitle, HeatUiOptionHelp.Chinese ? "将指针移到选项上，查看功能介绍。" : "Point to an option to see what it does.");
    }
}
