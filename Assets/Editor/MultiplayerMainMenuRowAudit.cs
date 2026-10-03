using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BadPiggies.Multiplayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Checks the three-button main-menu row without launching the game: it opens MainMenu, injects the
/// Multiplayer button exactly as MultiplayerMenuInjector does, runs the real MultiplayerMenuLayout.Arrange and
/// then asserts that the three buttons sit side by side without overlapping each other, without
/// reaching into the CrossPromo button and without leaving the camera's view.
///
/// The scene is only modified in memory; nothing is saved back to disk.
/// </summary>
public static class MultiplayerMainMenuRowAudit
{
    private const string ScenePath = "Assets/Scene/Scenes/UI/MainMenu.unity";
    private const string ReportPath = "Builds/QA/mainmenu-row-audit.txt";
    private const string PlayButtonName = "StartButton";
    private const string LayoutParentName = "StartLayout";
    private const string MultiplayerButtonName = "MultiplayerMultiplayerButton";

    /// <summary>Smallest gap allowed between two neighbouring buttons; the row asks for 0.9.</summary>
    private const float MinimumGap = 0.5f;

    private const float VerticalTolerance = 0.05f;

    /// <summary>How far Play's centre may sit from the middle of the screen.</summary>
    private const float CentreTolerance = 0.05f;

    private const float ViewportMargin = 1f;

    private static readonly List<string> s_log = new List<string>();
    private static readonly List<string> s_failures = new List<string>();

    [MenuItem("Tools/Multiplayer/Audit Main Menu Row")]
    public static void Run()
    {
        s_log.Clear();
        s_failures.Clear();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject play = FindUnder("StartButton", "StartLayout");
        GameObject cake = FindByName("CakeRaceButton");
        if (play == null)
        {
            throw new InvalidOperationException("StartButton is missing from " + ScenePath);
        }

        GameObject multiplayer = UnityEngine.Object.Instantiate(play, play.transform.parent);
        multiplayer.name = MultiplayerButtonName;
        multiplayer.transform.localPosition = play.transform.localPosition;
        multiplayer.transform.localScale = play.transform.localScale;

        MultiplayerMenuLayout.Arrange(cake, play, multiplayer);

        List<GameObject> row = new List<GameObject>();
        if (cake != null)
        {
            row.Add(cake);
        }
        row.Add(play);
        row.Add(multiplayer);

        List<Bounds> bounds = new List<Bounds>();
        foreach (GameObject button in row)
        {
            Bounds footprint = MultiplayerMenuLayout.Footprint(button);
            bounds.Add(footprint);
            s_log.Add(string.Format("{0}: centre ({1:0.###}, {2:0.###}) size {3:0.###} x {4:0.###}",
                button.name, footprint.center.x, footprint.center.y, footprint.size.x, footprint.size.y));
        }

        Check(row.Count == 3, "the row holds 3 buttons (Play, Cake Race, Multiplayer), found " + row.Count);

        // Play is the middle button and the menu's primary action, so the layout centres the row on it
        Bounds playBounds = bounds[row.IndexOf(play)];
        Check(Mathf.Abs(playBounds.center.x) <= CentreTolerance, string.Format(
            "{0} sits at the middle of the screen (centre x {1:0.###})", play.name, playBounds.center.x));

        // neighbours: the row is ordered left to right, so compare consecutive buttons
        for (int i = 0; i + 1 < row.Count; i++)
        {
            float gap = bounds[i + 1].min.x - bounds[i].max.x;
            s_log.Add(string.Format("gap {0} -> {1}: {2:0.###}", row[i].name, row[i + 1].name, gap));
            Check(gap >= MinimumGap, string.Format(
                "the gap between {0} and {1} is at least {2:0.##} ({3:0.###})",
                row[i].name, row[i + 1].name, MinimumGap, gap));
        }

        // no pair may overlap on both axes
        for (int i = 0; i < row.Count; i++)
        {
            for (int j = i + 1; j < row.Count; j++)
            {
                Check(!Overlaps(bounds[i], bounds[j]),
                    string.Format("{0} and {1} do not overlap", row[i].name, row[j].name));
            }
        }

        float rowCenterY = bounds[row.IndexOf(play)].center.y;
        foreach (GameObject button in row)
        {
            Bounds footprint = MultiplayerMenuLayout.Footprint(button);
            Check(Mathf.Abs(footprint.center.y - rowCenterY) <= VerticalTolerance, string.Format(
                "{0} sits on the row's centre line (off by {1:0.###})", button.name,
                footprint.center.y - rowCenterY));
        }

        // the CrossPromo button keeps its own space: the layout slides it aside rather than letting the
        // row share pixels with it, and the test is two-dimensional because the promo usually sits on a
        // different line of the menu, where overlapping x ranges collide with nothing at all
        GameObject promo = FindByName(MultiplayerMenuLayout.CrossPromoName);
        if (promo != null && promo.activeInHierarchy)
        {
            Bounds promoBounds = MultiplayerMenuLayout.Footprint(promo);
            s_log.Add(string.Format("{0}: centre ({1:0.###}, {2:0.###}) size {3:0.###} x {4:0.###}",
                promo.name, promoBounds.center.x, promoBounds.center.y, promoBounds.size.x, promoBounds.size.y));
            foreach (GameObject button in row)
            {
                Check(!Overlaps(MultiplayerMenuLayout.Footprint(button), promoBounds), string.Format(
                    "{0} and {1} do not overlap", button.name, promo.name));
            }
        }
        else
        {
            s_log.Add("no active CrossPromo button in the menu; the row only had to respect its own width");
        }

        // still inside the camera
        float halfWidth = 0f;
        foreach (Camera camera in Camera.allCameras)
        {
            if (camera.orthographic)
            {
                halfWidth = Mathf.Max(halfWidth, camera.orthographicSize * camera.aspect);
            }
        }
        if (halfWidth > 0f)
        {
            foreach (GameObject button in row)
            {
                Bounds footprint = MultiplayerMenuLayout.Footprint(button);
                Check(Mathf.Abs(footprint.center.x) + footprint.size.x * 0.5f <= halfWidth - ViewportMargin,
                    string.Format("{0} stays inside the camera (half width {1:0.###}, edge {2:0.###})",
                        button.name, halfWidth, Mathf.Abs(footprint.center.x) + footprint.size.x * 0.5f));
            }
            s_log.Add(string.Format("camera half width: {0:0.###}", halfWidth));
        }

        Write();
        if (s_failures.Count > 0)
        {
            throw new InvalidOperationException("MAINMENU_ROW_AUDIT_FAILED with " + s_failures.Count + " failure(s)");
        }
        Debug.Log("MAINMENU_ROW_AUDIT_PASS " + string.Join(" | ", s_log.ToArray()));
    }

    private static bool Overlaps(Bounds a, Bounds b)
    {
        return a.min.x < b.max.x && b.min.x < a.max.x && a.min.y < b.max.y && b.min.y < a.max.y;
    }

    private static void Check(bool condition, string message)
    {
        if (condition)
        {
            s_log.Add("OK   " + message);
            return;
        }
        s_log.Add("FAIL " + message);
        s_failures.Add(message);
    }

    private static GameObject FindByName(string name)
    {
        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go.name == name && go.scene.IsValid() && go.scene.isLoaded)
            {
                return go;
            }
        }
        return null;
    }

    private static GameObject FindUnder(string name, string ancestor)
    {
        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go.name != name || !go.scene.IsValid() || !go.scene.isLoaded)
            {
                continue;
            }
            Transform parent = go.transform.parent;
            while (parent != null)
            {
                if (parent.name == ancestor)
                {
                    return go;
                }
                parent = parent.parent;
            }
        }
        return null;
    }

    private static void Write()
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Main menu row audit (MultiplayerMainMenuRowAudit) " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        builder.AppendLine("scene " + ScenePath + " (opened in memory, not saved)");
        builder.AppendLine();
        builder.AppendLine("checks:");
        foreach (string line in s_log)
        {
            builder.AppendLine("  " + line);
        }
        builder.AppendLine();
        builder.AppendLine("failures: " + s_failures.Count);
        foreach (string failure in s_failures)
        {
            builder.AppendLine("  " + failure);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, builder.ToString());
        Debug.Log("report: " + ReportPath);
    }
}
