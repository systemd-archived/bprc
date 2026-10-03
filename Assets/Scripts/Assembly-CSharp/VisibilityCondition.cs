using System;
using UnityEngine;

public class VisibilityCondition : WPFMonoBehaviour
{
	[Serializable]
	public struct ConditionStruct
	{
		public Condition condition;

		public bool not;
	}

	public enum Condition
	{
		None = 0,
		HasValidContraption = 1,
		ShowEngineButton = 2,
		HasRockets = 3,
		IsPausedWhileRunning = 4,
		HasContraption = 5,
		QuestModeCanBuild = 6,
		IsPuzzleMode = 7,
		ShowPauseMenuReplayButton = 8,
		HasMotorWheels = 9,
		HasFans = 10,
		HasPropellers = 11,
		HasRotors = 12,
		ShowBuyBluePrintButton = 13,
		ShowAutoBuildButton = 14,
		ShowTutorialButton = 15,
		IsAutoBuilding = 16,
		CanClearContraption = 17,
		IsNotAutoBuilding = 18,
		ShowBuildModeButtons = 19,
		IAPEnabled = 20,
		ChiefPigExploded = 21,
		ShowSuperMechanicSwitch = 22,
		IsSandbox = 23,
		ShowSchematicsButton = 24,
		EveryPlayAvailable = 25,
		EveryPlayAvailableAndRecorded = 26,
		EveryPlayRecording = 27,
		GameCenterAvailable = 28,
		IsFreeVersion = 29,
		IsHDVersion = 30,
		IsOdyssey = 31,
		IsIOS = 32,
		CheatsEnabled = 33,
		IsDebugBuild = 34,
		CollectedFreeShopLootcrate = 35,
		HasNewParts = 36,
		LessCheats = 37,
		HasNetwork = 38,
		BoughtFieldOfDreams = 39,
		DailyChallengeComplete = 40,
		IsCakeRaceMode = 41,
		IsDecember = 42
	}

	public Condition condition;

	public bool not;

	[SerializeField]
	private bool disableGameObject;

	private Renderer m_renderer;

	private Collider m_collider;

	private Transform m_transform;

	private void Awake()
	{
		m_renderer = base.gameObject.GetComponent<Renderer>();
		m_collider = base.gameObject.GetComponent<Collider>();
		m_transform = base.gameObject.transform;
		if (Singleton<VisibilityConditionManager>.IsInstantiated())
		{
			Singleton<VisibilityConditionManager>.Instance.SubscribeToConditionChange(SetEnabled, condition);
		}
	}

	private void OnDestroy()
	{
		if (Singleton<VisibilityConditionManager>.IsInstantiated())
		{
			Singleton<VisibilityConditionManager>.Instance.UnsubscribeToConditionChange(SetEnabled, condition);
		}
	}

	public void UpdateState()
	{
		if (Singleton<VisibilityConditionManager>.IsInstantiated())
		{
			SetEnabled(condition, Singleton<VisibilityConditionManager>.Instance.GetState(condition));
		}
	}

	private void SetEnabled(Condition condition, bool enabled)
	{
		if (condition != this.condition)
		{
			return;
		}
		bool flag = false;
		if (not)
		{
			enabled = !enabled;
		}
		if ((bool)m_renderer && m_renderer.enabled != enabled)
		{
			flag = true;
			m_renderer.enabled = enabled;
		}
		if ((bool)m_collider && m_collider.enabled != enabled)
		{
			flag = true;
			m_collider.enabled = enabled;
		}
		if (flag || !m_renderer)
		{
			int childCount = m_transform.childCount;
			for (int i = 0; i < childCount; i++)
			{
				Renderer component = m_transform.GetChild(i).GetComponent<Renderer>();
				if ((bool)component && component.enabled != enabled)
				{
					component.enabled = enabled;
				}
			}
		}
		if (disableGameObject)
		{
			base.gameObject.SetActive(enabled);
		}
	}
}
