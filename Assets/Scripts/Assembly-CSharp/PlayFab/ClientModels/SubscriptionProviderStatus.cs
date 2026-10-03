namespace PlayFab.ClientModels
{
	public enum SubscriptionProviderStatus
	{
		NoError = 0,
		Cancelled = 1,
		UnknownError = 2,
		BillingError = 3,
		ProductUnavailable = 4,
		CustomerDidNotAcceptPriceChange = 5,
		FreeTrial = 6,
		PaymentPending = 7
	}
}
