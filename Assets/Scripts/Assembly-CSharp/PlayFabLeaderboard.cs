using System;
using System.Collections.Generic;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

public class PlayFabLeaderboard : MonoBehaviour
{
	public enum Leaderboard
	{
		CakeRaceWins = 0,
		CakeRaceCupF = 1,
		CakeRaceCupE = 2,
		CakeRaceCupD = 3,
		CakeRaceCupC = 4,
		CakeRaceCupB = 5,
		CakeRaceCupA = 6
	}

	public static Leaderboard LowestCup()
	{
		return Leaderboard.CakeRaceCupF;
	}

	public static Leaderboard HighestCup()
	{
		return Leaderboard.CakeRaceCupA;
	}

	public void AddScore(Leaderboard board, int score, Action<UpdatePlayerStatisticsResult> cb, Action<PlayFabError> errorCb)
	{
		// Offline (failed login) there is no session ticket, and PlayFabClientAPI throws
		// PlayFabException instead of reporting an error. Take the error path the callers already have.
		if (!PlayFabManager.IsLoggedInToPlayFab())
		{
			PlayFabManager.ReportNotLoggedIn(errorCb);
			return;
		}
		StatisticUpdate item = new StatisticUpdate
		{
			StatisticName = GetBoardName(board),
			Value = score
		};
		PlayFabClientAPI.UpdatePlayerStatistics(new UpdatePlayerStatisticsRequest
		{
			Statistics = new List<StatisticUpdate> { item }
		}, cb, errorCb);
	}

	public void AddScore(Leaderboard board, int score)
	{
		AddScore(board, score, OnAddScoreSuccess, OnAddScoreError);
	}

	private void OnAddScoreSuccess(UpdatePlayerStatisticsResult result)
	{
	}

	private void OnAddScoreError(PlayFabError error)
	{
	}

	public void GetScore(Leaderboard board, Action<GetPlayerStatisticsResult> cb, Action<PlayFabError> errorCb)
	{
		if (!PlayFabManager.IsLoggedInToPlayFab())
		{
			PlayFabManager.ReportNotLoggedIn(errorCb);
			return;
		}
		new StatisticUpdate().StatisticName = GetBoardName(board);
		PlayFabClientAPI.GetPlayerStatistics(new GetPlayerStatisticsRequest
		{
			StatisticNames = new List<string> { board.ToString() }
		}, cb, errorCb);
	}

	public void GetLeaderboard(Leaderboard board, Action<GetLeaderboardResult> cb, Action<PlayFabError> errorCb, bool previousSeason = false, int startPosition = 0, int maxCount = 50)
	{
		if (!PlayFabManager.IsLoggedInToPlayFab())
		{
			PlayFabManager.ReportNotLoggedIn(errorCb);
			return;
		}
		PlayFabClientAPI.GetLeaderboard(new GetLeaderboardRequest
		{
			StatisticName = GetBoardName(board, previousSeason),
			StartPosition = startPosition,
			MaxResultsCount = maxCount
		}, cb, errorCb);
	}

	public void GetLeaderboardAroundPlayer(Leaderboard board, Action<GetLeaderboardAroundPlayerResult> cb, Action<PlayFabError> errorCb, bool previousSeason = false, int maxCount = 1)
	{
		if (!PlayFabManager.IsLoggedInToPlayFab())
		{
			PlayFabManager.ReportNotLoggedIn(errorCb);
			return;
		}
		PlayFabClientAPI.GetLeaderboardAroundPlayer(new GetLeaderboardAroundPlayerRequest
		{
			StatisticName = GetBoardName(board, previousSeason),
			MaxResultsCount = maxCount
		}, cb, errorCb);
	}

	private static string GetBoardName(Leaderboard board, bool previousSeason = false)
	{
		if (((!previousSeason && CakeRaceMenu.CurrentCakeRaceWeek() % 2 != 0) || (previousSeason && CakeRaceMenu.CurrentCakeRaceWeek() % 2 == 0)) && (uint)(board - 1) <= 5u)
		{
			return $"{board.ToString()}2";
		}
		return board.ToString();
	}
}
