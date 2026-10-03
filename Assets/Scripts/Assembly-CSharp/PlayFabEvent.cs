public struct PlayFabEvent : EventManager.Event
{
	public enum Type
	{
		None = 0,
		UserDataUploadStarted = 1,
		UserDataUploadEnded = 2,
		UserDeltaChangeUploadStarted = 3,
		UserDeltaChangeUploadEnded = 4,
		LocalDataUpdated = 5
	}

	public Type type;

	public PlayFabEvent(Type type)
	{
		this.type = type;
	}
}
