using System;
using System.Collections.Generic;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

public class PlayFabUserHandling : MonoBehaviour
{
	private List<string> replayKeyCache;

	private void Awake()
	{
		replayKeyCache = new List<string>();
		for (int i = 0; i < 7; i++)
		{
			replayKeyCache.Add($"replay_track_{i}");
		}
	}

	public void GetUserReplays(string playfabID, Action<GetUserDataResult> cb, Action<PlayFabError> errorCb)
	{
		// Same as the leaderboard wrappers: without a session PlayFabClientAPI throws instead of
		// reporting an error, so hand the caller the error it would otherwise never receive.
		if (!PlayFabManager.IsLoggedInToPlayFab())
		{
			PlayFabManager.ReportNotLoggedIn(errorCb);
			return;
		}
		PlayFabClientAPI.GetUserData(new GetUserDataRequest
		{
			PlayFabId = playfabID,
			Keys = replayKeyCache
		}, cb, errorCb);
	}
}
