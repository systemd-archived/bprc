using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class INInitializer : MonoBehaviour
{
	/// <summary>Version the game boots into when the version selector is switched off: 新创B.</summary>
	private const int FallbackVersion = 3;

	private const string BackgroundObjectName = "InnovationBackground";

	private const string VersionSelectorObjectName = "INVersionSelector";

	[SerializeField]
	private List<GameObject> m_splashes;

	[SerializeField]
	private List<GameObject> m_prefabs;

	[SerializeField]
	private ResourceData m_resourceData;

	private bool m_initialized;

	private bool m_useAlphaAnimation;

	private float m_time;

	public bool Initialized => m_initialized;

	private void Awake()
	{
		m_useAlphaAnimation = true;
		m_time = 3f;
		Initialize().Forget();
	}

	private async UniTask Initialize()
	{
		// 自动进入“新创B”：跳过开场动画与版本选择界面
		bool autoB = StarSeaSettings.GetAutoEnterVersionBFromPrefs();
		if (!autoB)
		{
		for (int i = 0; i < m_splashes.Count; i++)
		{
			GameObject splash = Object.Instantiate(m_splashes[i], Vector3.zero, Quaternion.identity);
			if (HideBackgroundArt(splash))
			{
				// The background was the whole of this splash's visible art, so waiting out its fade-in,
				// hold and fade-out would only make the player sit through three seconds of nothing.
				UnityEngine.Debug.Log("[INInitializer] 启动画面已无可见内容，跳过 " + m_time + " 秒的淡入淡出等待。");
				Object.Destroy(splash);
				continue;
			}
			await PlayAnimation(splash);
			Object.Destroy(splash);
		}
		}
		INUnity.Initialize(m_resourceData);
		// The version selector is created last and only when the player asked for it, so that skipping it
		// means the panel never exists - nothing to hide, and no fade animation left running against an
		// object that is about to go away. It has to come after the other prefabs either way: reading the
		// settings needs the declaration assets, and those are only loadable once the mod's own prefabs
		// have been created.
		List<GameObject> versionSelectors = new List<GameObject>();
		List<GameObject> instances = new List<GameObject>();
		foreach (GameObject prefab in m_prefabs)
		{
			if (prefab == null)
			{
				continue;
			}
			if (prefab.name.StartsWith(VersionSelectorObjectName))
			{
				versionSelectors.Add(prefab);
				continue;
			}
			instances.Add(Object.Instantiate(prefab));
		}
		if (!autoB && INSettings.PeekBool(INFeature.OpenVersionSelector, FallbackVersion))
		{
			foreach (GameObject prefab in versionSelectors)
			{
				instances.Add(Object.Instantiate(prefab));
			}
		}
		else
		{
			INSettings.Initialize(FallbackVersion);
			UnityEngine.Debug.Log("[INInitializer] " + (autoB ? "已启用自动进入新创B，" : "") + "已跳过版本选择器，直接使用版本 " + INSettings.VersionType + "。");
		}
		await UniTask.WaitUntil(() => INSettings.VersionSelected);
		m_initialized = true;
		await LoadMainMenu();
	}

	/// <summary>
	/// Takes the Innovation background's RawImage out of the picture. The splash is only a backdrop and
	/// the RawImage is the part of it that is not wanted, so the component is disabled and the object it
	/// sits on is left alone - anything else hanging off the splash keeps working.
	/// </summary>
	/// <returns>
	/// True when the splash's visible art was removed, which means there is nothing left worth displaying
	/// and the caller can skip its animation entirely.
	/// </returns>
	private static bool HideBackgroundArt(GameObject splash)
	{
		if (splash == null || !splash.name.StartsWith(BackgroundObjectName))
		{
			return false;
		}
		bool removed = false;
		RawImage[] images = splash.GetComponentsInChildren<RawImage>(includeInactive: true);
		for (int i = 0; i < images.Length; i++)
		{
			if (images[i].enabled)
			{
				images[i].enabled = false;
				removed = true;
				UnityEngine.Debug.Log("[INInitializer] " + splash.name + "/" + images[i].name + " 的显示已移除。");
			}
		}
		return removed;
	}

	private async UniTask LoadMainMenu()
	{
		await UniTask.WaitUntil(() => SingletonSpawner.SpawnDone);
		await UniTask.WaitUntil(() => Bundle.initialized && !Bundle.checkingBundles && Singleton<GameConfigurationManager>.Instance.HasData);
		PostInitialize();
		Singleton<GameManager>.Instance.LoadMainMenu(showLoadingScreen: true);
	}

	private void PostInitialize()
	{
		if (INSettings.GetBool(INFeature.RuntimeGameData))
		{
			Object.Instantiate(INUnity.LoadGameObject("INPartFactoryManager"));
		}
		if (INSettings.GetBool(INFeature.ApplicationInterface))
		{
			Object.Instantiate(INUnity.LoadGameObject("INApplicationInterface"));
		}
		if (INSettings.GetBool(INFeature.CommandSystem))
		{
			new GameObject("INAddonManager").AddComponent<INAddonManager>();
		}
		// The RC1 driving controls are created either way and decide for themselves whether to show:
		// they only appear while 「RC1 操作」 is on and a contraption is actually running, so switching
		// the row on in the settings screen takes effect immediately instead of at the next boot.
		new GameObject("INRc1Controls").AddComponent<INRc1Controls>();
	}

	private async UniTask PlayAnimation(GameObject gameObject)
	{
		if (!m_useAlphaAnimation)
		{
			await UniTask.Delay((int)(m_time * 1000f), ignoreTimeScale: true);
			return;
		}
		CanvasRenderer canvasRenderer = gameObject.GetComponentInChildren<CanvasRenderer>();
		if (canvasRenderer != null)
		{
			await canvasRenderer.PlayFadeInAnimation(m_time / 3f, ignoreTimeScale: true);
			await UniTask.Delay((int)(m_time / 3f * 1000f), ignoreTimeScale: true);
			await canvasRenderer.PlayFadeOutAnimation(m_time / 3f, ignoreTimeScale: true);
		}
	}
}