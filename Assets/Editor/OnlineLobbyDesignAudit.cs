using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BadPiggies.Multiplayer.Online.Ui;
using Michsky.UI.Heat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class OnlineLobbyDesignAudit
{
    public static int CleanupPreviewOrphans()
    {
        if (Application.isPlaying || OnlineLobbyWindow.Instance != null) return 0;
        int count = 0;
        foreach (Canvas canvas in UnityEngine.Object.FindObjectsOfType<Canvas>(true))
            if (canvas.name == "Online Lobby Canvas" && canvas.transform.parent == null)
            { UnityEngine.Object.DestroyImmediate(canvas.gameObject); count++; }
        return count;
    }

    public static string Run()
    {
        if (Application.isPlaying || OnlineLobbyWindow.Instance != null)
            throw new InvalidOperationException("Run with no existing lobby and outside Play Mode.");
        OnlineLobbyWindow window = OnlineLobbyWindow.GetOrCreate();
        Canvas canvas = null;
        try
        {
            typeof(OnlineLobbyWindow).GetMethod("EnsureBuilt", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, null);
            Require(string.IsNullOrEmpty(window.LastBuildError), window.LastBuildError);
            canvas = UnityEngine.Object.FindObjectsOfType<Canvas>(true).First(c => c.name == "Online Lobby Canvas");
            canvas.gameObject.SetActive(true);
            Transform root = canvas.transform;
            Func<string, Transform> find = name => root.GetComponentsInChildren<Transform>(true).First(t => t.name == name);
            foreach (string name in new[] { "NavHome", "NavCreate", "NavJoin", "NavLan", "NavRecent", "NavSettings" })
                Require(find(name).GetComponent<PanelButton>() != null, name + " must use native Heat PanelButton");
            foreach (string name in new[] { "CreateButton", "JoinButton", "CopyCode" })
                Require(find(name).GetComponent<ButtonManager>() != null, name + " must use native Heat ButtonManager");
            window.ShowPage(OnlineLobbyWindow.Page.Home);
            find("HomeJoin").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Require(find("Page_Join").gameObject.activeInHierarchy, "Home join action did not navigate");
            TMP_InputField invite = find("JoinInput").GetComponent<TMP_InputField>();
            invite.text = "BP-TEST-1234";
            window.ShowPage(OnlineLobbyWindow.Page.Settings);
            window.ShowPage(OnlineLobbyWindow.Page.Join);
            Require(invite.text == "BP-TEST-1234", "Page switching lost input");
            foreach (string name in new[] { "Password", "JoinPassword" })
                Require(find(name).GetComponent<TMP_InputField>().contentType == TMP_InputField.ContentType.Password, "Password visible");
            Toggle toggle = find("ToggleBroadcast").GetComponent<Toggle>();
            toggle.isOn = false;
            Require(toggle.graphic == null, "Off-state must not hide the switch knob");
            toggle.isOn = true;
            object pages = typeof(OnlineLobbyWindow).GetField("m_pages", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
            pages.GetType().GetMethod("SetVisible").Invoke(pages, new object[] { false });
            foreach (string name in new[] { "Page_CreateScroll", "Page_JoinScroll", "Page_SettingsScroll" })
                Require(!find(name).gameObject.activeInHierarchy, "Room view leaves a page scrollbar active");
            string report = "PASS: native Heat buttons/tabs, home navigation, persistent forms, masked passwords, switch knob, room scroll isolation.";
            Directory.CreateDirectory("Builds/QA/online-redesign");
            File.WriteAllText("Builds/QA/online-redesign/design-audit.txt", report);
            return report;
        }
        finally { if (canvas != null) UnityEngine.Object.DestroyImmediate(canvas.gameObject); UnityEngine.Object.DestroyImmediate(window.gameObject); }
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
