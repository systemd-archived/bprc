using Innovation;
using UnityEngine;

public class INGameManager : MonoBehaviour
{
	private Vector2Int m_resolution;

	public static INGameManager Instance { get; private set; }

	public void SetFullScreen(bool fullScreen)
	{
#if UNITY_ANDROID && !UNITY_EDITOR
		// Android owns the native surface size; desktop SetResolution can retain a restricted surface.
		RestoreAndroidDisplay();
		return;
#else
		if (Screen.fullScreen != fullScreen)
		{
			if (fullScreen)
			{
				Resolution currentResolution = Screen.currentResolution;
				m_resolution = new Vector2Int(Screen.width, Screen.height);
				Screen.SetResolution(currentResolution.width, currentResolution.height, fullscreen: true);
			}
			else
			{
				Screen.SetResolution(m_resolution.x, m_resolution.y, fullscreen: false);
			}
		}
#endif
	}

	private void Awake()
	{
		Instance = this;
		Object.DontDestroyOnLoad(this);
		Resolution currentResolution = Screen.currentResolution;
		m_resolution = new Vector2Int(currentResolution.width, currentResolution.height);
		BP.CoreService = new CoreService();
		BP.AddonService = new AddonService();
		BP.PartService = new PartService();
		Json.Service = new JsonService();
#if UNITY_ANDROID && !UNITY_EDITOR
		// The shared desktop quality profile requests 4x MSAA at the phone's native resolution.
		if (QualitySettings.antiAliasing > 2) QualitySettings.antiAliasing = 2;
		RestoreAndroidDisplay();
#endif
	}

#if UNITY_ANDROID && !UNITY_EDITOR
	private void OnApplicationPause(bool paused)
	{
		if (!paused) RestoreAndroidDisplay();
	}

	private void OnApplicationFocus(bool focused)
	{
		if (focused) RestoreAndroidDisplay();
	}

	private static void RestoreAndroidDisplay()
	{
		Screen.fullScreen = true;
		Application.targetFrameRate = INUserSettings.Instance?.StarSeaSettings?.TargetFrameRate ?? 60;
		try
		{
			using (AndroidJavaClass display = new AndroidJavaClass("com.innovation.filedialog.AndroidDisplayBridge"))
			{
				display.CallStatic("Apply");
			}
		}
		catch (System.Exception ex)
		{
			Debug.LogWarning("Android display restore: " + ex.Message);
		}
	}
#endif

	private void Update()
	{
		if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.F))
		{
			bool flag = !Screen.fullScreen;
			INSettingsInterface.Instance?.SetValue("GeneralSettings_FullScreen", flag);
		}
	}
}
