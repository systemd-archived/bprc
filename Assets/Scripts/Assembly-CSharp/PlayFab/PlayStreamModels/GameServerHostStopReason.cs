namespace PlayFab.PlayStreamModels
{
	public enum GameServerHostStopReason
	{
		Other = 0,
		ExcessCapacity = 1,
		LimitExceeded = 2,
		BuildNotActiveInRegion = 3,
		Unresponsive = 4
	}
}
