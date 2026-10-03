using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps the menu background - the jungle set behind the menus - alive between menu screens instead of
/// rebuilding it every time one opens.
/// </summary>
/// <remarks>
/// <c>GameManager.CreateMenuBackground</c> instantiates a 110-object prefab whose 102 sprite renderers all
/// arrive with an empty mesh, so every instance builds 102 meshes at load (<c>UnmanagedSprite.Awake</c>).
/// Four selectors call it from their <c>Awake</c> (the episode selector, both episode list styles and the
/// page selector), and each menu screen used to pay that cost again because the instance died with the
/// scene that created it.
/// <para>
/// The instance is therefore moved out of the scene and hidden on every scene change; the selector of the
/// next menu screen shows it again, and a level never does, so it stays invisible outside the menus. The
/// prefab it was built from is remembered, so changing the background setting builds the new one rather
/// than reusing the old.
/// </para>
/// </remarks>
public static class INMenuBackgroundCache
{
    private static GameObject s_instance;

    private static string s_source;

    /// <summary>True when an instance is alive, whether or not it is currently shown.</summary>
    public static bool HasInstance => s_instance != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private static void OnSceneUnloaded(Scene scene)
    {
        Hide();
    }

    /// <summary>
    /// Shows the background built from <paramref name="sourceName"/>, building it the first time.
    /// </summary>
    public static void Show(string sourceName, GameObject prefab)
    {
        if (prefab == null)
        {
            return;
        }
        if (s_instance != null && s_source == sourceName)
        {
            Position(s_instance);
            if (!s_instance.activeSelf)
            {
                s_instance.SetActive(value: true);
            }
            return;
        }
        if (s_instance != null)
        {
            Object.Destroy(s_instance);
            s_instance = null;
        }
        s_instance = Object.Instantiate(prefab, Vector3.forward * 10f, Quaternion.identity);
        s_instance.name = prefab.name;
        Object.DontDestroyOnLoad(s_instance);
        s_source = sourceName;
    }

    /// <summary>Hides the background without destroying it, so the next menu can show it again.</summary>
    public static void Hide()
    {
        if (s_instance != null && s_instance.activeSelf)
        {
            s_instance.SetActive(value: false);
        }
    }

    private static void Position(GameObject instance)
    {
        instance.transform.position = Vector3.forward * 10f;
        instance.transform.rotation = Quaternion.identity;
    }
}
