using System;
using UnityEngine;

public class VisibilityToggle : MonoBehaviour
{
	[SerializeField]
	private string gameConfigTitle = string.Empty;

	[SerializeField]
	private string gameConfigKey = string.Empty;

	[SerializeField]
	private bool isEnabledAtStart = true;

	private void Awake()
	{
		Enable(isEnabledAtStart);
		if (!string.IsNullOrEmpty(gameConfigTitle) && !string.IsNullOrEmpty(gameConfigKey) && !(Singleton<GameConfigurationManager>.Instance == null))
		{
			if (Singleton<GameConfigurationManager>.Instance.HasData)
			{
				OnDataFetched();
				return;
			}
			GameConfigurationManager instance = Singleton<GameConfigurationManager>.Instance;
			instance.OnHasData = (Action)Delegate.Combine(instance.OnHasData, new Action(OnDataFetched));
		}
	}

	private void OnDestroy()
	{
		if (Singleton<GameConfigurationManager>.Instance != null)
		{
			GameConfigurationManager instance = Singleton<GameConfigurationManager>.Instance;
			instance.OnHasData = (Action)Delegate.Remove(instance.OnHasData, new Action(OnDataFetched));
		}
	}

	private void OnDataFetched()
	{
		Enable(Singleton<GameConfigurationManager>.Instance.GetValue<bool>(gameConfigTitle, gameConfigKey));
	}

	private void Enable(bool enable)
	{
		// The original build logged "VisibilityToggle: <state>" here, on every call - which is once per
		// component in Awake and again whenever the remote config arrives. It said nothing a log reader can
		// act on and the objects it belongs to are instantiated in bulk by the menus, so it is gone.
		base.gameObject.SetActive(enable);
	}
}
