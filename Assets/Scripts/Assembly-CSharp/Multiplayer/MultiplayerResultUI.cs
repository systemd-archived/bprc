using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BadPiggies.Multiplayer
{
/// <summary>
/// Result screen for an elimination round: shows whether the local player is the last machine
/// standing, where they finished, and collects the rematch votes of the whole room.
/// </summary>
public class MultiplayerResultUI : MonoBehaviour
{
	private readonly List<MultiplayerLobbyPlayer> m_players = new List<MultiplayerLobbyPlayer>();

	private bool m_requested;

	private void Update()
	{
		MultiplayerGameInputBlock.Apply(this, ShouldDraw());
		if (!ShouldDraw())
		{
			m_requested = false;
		}
	}

	private void OnDisable()
	{
		MultiplayerGameInputBlock.Release(this);
	}

	private bool ShouldDraw()
	{
		MultiplayerMatchController multiplayerMatchController = MultiplayerMatchController.Resolve();
		if (NetworkClient.active && multiplayerMatchController != null && multiplayerMatchController.HasResult)
		{
			return SceneManager.GetActiveScene().name == "LevelStub";
		}
		return false;
	}

	private void OnGUI()
	{
		if (!ShouldDraw())
		{
			return;
		}
		MultiplayerMatchController multiplayerMatchController = MultiplayerMatchController.Resolve();
		GUILayout.BeginArea(new Rect(((float)Screen.width - 460f) * 0.5f, ((float)Screen.height - 320f) * 0.5f, 460f, 320f), GUI.skin.box);
		GUILayout.Label(Headline(multiplayerMatchController), RichLabel(24));
		GUILayout.Label(DescribeResult(multiplayerMatchController), RichLabel(15));
		GUILayout.Space(6f);
		GUILayout.Label(DescribeField(multiplayerMatchController), RichLabel(12));
		GUILayout.Label("Traffic peak: " + MultiplayerTraffic.PeakSummary, RichLabel(12));
		GUILayout.FlexibleSpace();
		MultiplayerLobbyPlayer multiplayerLobbyPlayer = FindLocalPlayer();
		int num = CountRematchVotes();
		int num2 = Mathf.Max(m_players.Count, multiplayerMatchController.RoundPlayers);
		GUILayout.Label($"Rematch {num}/{num2}", RichLabel(14));
		bool flag = m_requested || (multiplayerLobbyPlayer != null && multiplayerLobbyPlayer.WantsRematch);
		GUI.enabled = multiplayerLobbyPlayer != null && !flag;
		if (GUILayout.Button(flag ? "Waiting for the other players…" : "Rematch", GUILayout.Height(36f)))
		{
			multiplayerLobbyPlayer.RequestRematchLocal();
			m_requested = true;
		}
		GUI.enabled = true;
		if (GUILayout.Button("Leave match", GUILayout.Height(28f)))
		{
			LeaveMatch();
		}
		GUILayout.EndArea();
	}

	private void LeaveMatch()
	{
		m_requested = false;
		MultiplayerGameInputBlock.Release(this);
		Debug.Log("[Multiplayer] Leaving the match from the result screen.");
		EventManager.Send(new UIEvent(UIEvent.Type.LevelSelection));
	}

	private static string Headline(MultiplayerMatchController controller)
	{
		if (controller.IsDraw)
		{
			return "<b>DRAW</b>";
		}
		return controller.LocalPlayerWon ? "<b>VICTORY</b>" : "<b>ELIMINATED</b>";
	}

	private static string DescribeResult(MultiplayerMatchController controller)
	{
		int roundPlayers = controller.RoundPlayers;
		if (controller.IsDraw)
		{
			return "Nobody was left standing — the round is a draw.";
		}
		if (controller.LocalPlayerWon)
		{
			return $"You are the last machine standing — 1st of {roundPlayers}.";
		}
		if (!controller.LocalPlayerEliminated)
		{
			return "The round ended before your machine was judged.";
		}
		return $"You finished #{controller.LocalPlacement} of {roundPlayers} — {DescribeReason(controller.LocalLossReason)}";
	}

	private static string DescribeReason(MultiplayerMatchController.LossReason reason)
	{
		switch (reason)
		{
		case MultiplayerMatchController.LossReason.PilotEjected:
			return "your pilot was thrown out of the machine.";
		case MultiplayerMatchController.LossReason.LeftTheArena:
			return "your machine left the arena.";
		default:
			return "you left the session.";
		}
	}

	private string DescribeField(MultiplayerMatchController controller)
	{
		if (controller.IsDraw)
		{
			return $"{controller.EliminatedCount} machine(s) went out this round.";
		}
		MultiplayerLobbyPlayer multiplayerLobbyPlayer = FindAtSlot(controller.WinnerSlot);
		string arg = ((multiplayerLobbyPlayer != null) ? multiplayerLobbyPlayer.DisplayName : $"slot {controller.WinnerSlot + 1}");
		return $"Winner: {arg}   Eliminated: {controller.EliminatedCount}/{controller.RoundPlayers}";
	}

	private MultiplayerLobbyPlayer FindAtSlot(int slot)
	{
		MultiplayerLobbyPlayer.CollectInto(m_players);
		for (int i = 0; i < m_players.Count; i++)
		{
			if (m_players[i] != null && m_players[i].Slot == slot)
			{
				return m_players[i];
			}
		}
		return null;
	}

	private int CountRematchVotes()
	{
		MultiplayerLobbyPlayer.CollectInto(m_players);
		int num = 0;
		for (int i = 0; i < m_players.Count; i++)
		{
			if (m_players[i] != null && m_players[i].WantsRematch)
			{
				num++;
			}
		}
		return num;
	}

	private MultiplayerLobbyPlayer FindLocalPlayer()
	{
		NetworkIdentity localPlayer = NetworkClient.localPlayer;
		if (!(localPlayer != null))
		{
			return null;
		}
		return localPlayer.GetComponent<MultiplayerLobbyPlayer>();
	}

	private static GUIStyle RichLabel(int fontSize)
	{
		return new GUIStyle(GUI.skin.label)
		{
			richText = true,
			alignment = TextAnchor.MiddleCenter,
			fontSize = fontSize,
			wordWrap = true
		};
	}
}
}
