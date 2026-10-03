namespace PlayFab.PlayStreamModels
{
	public enum TransactionStatus
	{
		CreateCart = 0,
		Init = 1,
		Approved = 2,
		Succeeded = 3,
		FailedByProvider = 4,
		DisputePending = 5,
		RefundPending = 6,
		Refunded = 7,
		RefundFailed = 8,
		ChargedBack = 9,
		FailedByUber = 10,
		FailedByPlayFab = 11,
		Revoked = 12,
		TradePending = 13,
		Traded = 14,
		Upgraded = 15,
		StackPending = 16,
		Stacked = 17,
		Other = 18,
		Failed = 19
	}
}
