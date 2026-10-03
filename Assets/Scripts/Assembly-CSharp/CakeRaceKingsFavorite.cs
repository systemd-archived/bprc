using System;
using System.Collections.Generic;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

public class CakeRaceKingsFavorite : Singleton<CakeRaceKingsFavorite>
{
	private const string RANDOM_SEED_KEY = "kings_favorite_random_seed";

	public Action OnPartAcquired;

	private bool isFetchingSeed;

	private int randomSeed;

	public BasePart CurrentFavorite { get; private set; }

	private void Start()
	{
		if (Singleton<PlayFabManager>.Instance.Initialized)
		{
			FetchRandomSeed();
		}
		else
		{
			PlayFabManager playFabManager = Singleton<PlayFabManager>.Instance;
			playFabManager.OnLogin = (Action<string, string>)Delegate.Combine(playFabManager.OnLogin, new Action<string, string>(OnPlayFabLogin));
		}
		SetAsPersistant();
	}

	private void OnPlayFabLogin(string playfabId, string facebookbId)
	{
		// PlayFabManager raises OnLogin with empty ids when the PlayFab login failed. Title data needs a
		// session, so skip the fetch and stay subscribed: if a later login succeeds, OnLogin is raised
		// again with a real id and the favourite is fetched then. (Fetching on failure threw
		// PlayFabException and aborted the remaining OnLogin subscribers, which is what kept HatchManager
		// from falling back to offline play.)
		if (string.IsNullOrEmpty(playfabId))
		{
			return;
		}
		FetchRandomSeed();
	}

	private void UpdateRandomPart(int newSeed)
	{
		UnityEngine.Random.State state = UnityEngine.Random.state;
		randomSeed = newSeed;
		UnityEngine.Random.InitState(randomSeed);
		int num = UnityEngine.Random.Range(0, 4);
		List<BasePart> allTierParts = CustomizationManager.GetAllTierParts((BasePart.PartTier)num, (num != 0) ? CustomizationManager.PartFlags.Craftable : CustomizationManager.PartFlags.None);
		if (allTierParts.Count > 0)
		{
			int index = UnityEngine.Random.Range(0, allTierParts.Count);
			CurrentFavorite = allTierParts[index];
		}
		if (OnPartAcquired != null)
		{
			OnPartAcquired();
		}
		UnityEngine.Random.state = state;
	}

	private void FetchRandomSeed()
	{
		// Start() and ClearCurrentFavorite() can reach this before (or after a failed) login; requesting
		// title data then would throw, so wait for a session instead.
		if (isFetchingSeed || !PlayFabManager.IsLoggedInToPlayFab())
		{
			return;
		}
		isFetchingSeed = true;
		Singleton<PlayFabManager>.Instance.GetTitleData(new List<string> { "kings_favorite_random_seed" }, OnSeedFetched, OnSeedFetchError);
	}

	private void OnSeedFetched(GetTitleDataResult result)
	{
		if (result != null && result.Data != null && result.Data.ContainsKey("kings_favorite_random_seed") && int.TryParse(result.Data["kings_favorite_random_seed"], out var result2))
		{
			UpdateRandomPart(result2);
		}
		isFetchingSeed = false;
	}

	private void OnSeedFetchError(PlayFabError error)
	{
		isFetchingSeed = false;
	}

	public void ClearCurrentFavorite()
	{
		CurrentFavorite = null;
		FetchRandomSeed();
	}
}
