using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace BadPiggies.Multiplayer
{
[RequireComponent(typeof(MultiplayerNetworkManager))]
public class MultiplayerBuildPhaseUI : MonoBehaviour
{
	private enum Stage
	{
		Inactive,
		Building,
		Submitted,
		Fighting
	}

	private Stage m_stage;

	private string m_message;

	private float m_messageClearTime;

	private const float MessageDuration = 4f;

	private MultiplayerReadyCheckmark m_checkmark;

	private MultiplayerReadyCounter m_counter;

	private readonly List<MultiplayerLobbyPlayer> m_players = new List<MultiplayerLobbyPlayer>();

	private bool m_blueprintRestored;

	private bool m_readyFlagsCleared;

	private void Awake()
	{
		m_checkmark = gameObject.AddComponent<MultiplayerReadyCheckmark>();
		m_checkmark.Pressed = OnReadyPressed;
		m_counter = gameObject.AddComponent<MultiplayerReadyCounter>();
	}

	private void Update()
	{
		if (!NetworkClient.active)
		{
			Deactivate();
			return;
		}
		LevelManager levelManager = WPFMonoBehaviour.levelManager;
		if (levelManager == null)
		{
			Deactivate();
			return;
		}
		if (m_stage == Stage.Inactive)
		{
			m_stage = Stage.Building;
			m_message = null;
			ClearStaleReadyFlags();
			m_blueprintRestored = false;
		}
		if (m_stage == Stage.Building && !m_blueprintRestored)
		{
			MultiplayerMatchController multiplayerMatchController = MultiplayerMatchController.Resolve();
			if (multiplayerMatchController == null || !multiplayerMatchController.HasStoredBlueprint)
			{
				m_blueprintRestored = true;
			}
			else if (multiplayerMatchController.RestoreLastBlueprint())
			{
				m_blueprintRestored = true;
			}
		}
		MultiplayerMatchController multiplayerMatchController2 = MultiplayerMatchController.Resolve();
		bool flag = (multiplayerMatchController2 != null && multiplayerMatchController2.BattleStarted) || MultiplayerMachineAnchor.AnyMachineSpawned();
		if ((m_stage != Stage.Fighting) & flag)
		{
			m_stage = Stage.Fighting;
			m_message = null;
		}
		if (m_stage == Stage.Building)
		{
			MultiplayerLobbyPlayer multiplayerLobbyPlayer = FindLocalPlayer();
			if (multiplayerLobbyPlayer != null && multiplayerLobbyPlayer.BlueprintSent)
			{
				m_stage = Stage.Submitted;
			}
		}
		UpdateReadyControl(levelManager);
	}

	private void OnDisable()
	{
		Deactivate();
	}

	private void Deactivate()
	{
		if (m_stage != Stage.Inactive)
		{
			ReleaseReadyControl();
			m_stage = Stage.Inactive;
			m_message = null;
			m_readyFlagsCleared = false;
			m_blueprintRestored = false;
		}
	}

	public void ResetToBuilding()
	{
		Deactivate();
	}

	private void UpdateReadyControl(LevelManager levelManager)
	{
		GameObject gameObject = FindCheckmark(levelManager);
		if (!(gameObject == null))
		{
			if (m_stage == Stage.Fighting)
			{
				ReleaseReadyControl();
				return;
			}
			m_checkmark.Bind(gameObject);
			bool flag = FindLocalPlayer() != null;
			m_checkmark.SetInteractable(flag && (m_stage == Stage.Building || m_stage == Stage.Submitted));
			m_checkmark.ShowCancelArt(m_stage == Stage.Submitted);
			m_counter.UpdateLabel(gameObject, BuildCounterText());
		}
	}

	private void ReleaseReadyControl()
	{
		m_checkmark.Restore();
		m_counter.RemoveLabel();
	}

	private static GameObject FindCheckmark(LevelManager levelManager)
	{
		InGameGUI inGameGUI = levelManager.InGameGUI;
		if (inGameGUI == null || inGameGUI.BuildMenu == null)
		{
			return null;
		}
		return inGameGUI.BuildMenu.PlayButton;
	}

	private string BuildCounterText()
	{
		if (!string.IsNullOrEmpty(m_message))
		{
			if (Time.unscaledTime < m_messageClearTime)
			{
				return m_message;
			}
			m_message = null;
		}
		MultiplayerLobbyPlayer.CollectInto(m_players);
		int num = 0;
		for (int i = 0; i < m_players.Count; i++)
		{
			if (m_players[i] != null && m_players[i].IsReady)
			{
				num++;
			}
		}
		// The denominator is the room as it is right now, so the label reads correctly for any
		// number of players.
		return $"{num}/{Mathf.Max(1, m_players.Count)} ready";
	}

	private void ClearStaleReadyFlags()
	{
		if (m_readyFlagsCleared)
		{
			return;
		}
		m_readyFlagsCleared = true;
		if (NetworkServer.active)
		{
			MultiplayerNetworkManager component = GetComponent<MultiplayerNetworkManager>();
			if (component != null)
			{
				component.ServerClearReadyFlags();
			}
		}
	}

	private void OnReadyPressed()
	{
		MultiplayerLobbyPlayer multiplayerLobbyPlayer = FindLocalPlayer();
		if (multiplayerLobbyPlayer == null)
		{
			ShowMessage("no player object");
		}
		else if (m_stage == Stage.Submitted)
		{
			if (multiplayerLobbyPlayer.WithdrawLocalBlueprint())
			{
				m_stage = Stage.Building;
				m_message = null;
			}
		}
		else
		{
			if (m_stage != Stage.Building)
			{
				return;
			}
			if (multiplayerLobbyPlayer.SubmitLocalBlueprint())
			{
				m_stage = Stage.Submitted;
				m_message = null;
				if (!multiplayerLobbyPlayer.IsReady)
				{
					multiplayerLobbyPlayer.ToggleReadyLocal();
				}
			}
			else
			{
				ShowMessage("build a machine first");
			}
		}
	}

	private void ShowMessage(string message)
	{
		m_message = message;
		m_messageClearTime = Time.unscaledTime + 4f;
	}

	private MultiplayerLobbyPlayer FindLocalPlayer()
	{
		MultiplayerLobbyPlayer.CollectInto(m_players);
		for (int i = 0; i < m_players.Count; i++)
		{
			if (m_players[i] != null && m_players[i].isLocalPlayer)
			{
				return m_players[i];
			}
		}
		return null;
	}
}
}
