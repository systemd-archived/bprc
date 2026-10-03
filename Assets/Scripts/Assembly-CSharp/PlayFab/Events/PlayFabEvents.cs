using System;
using PlayFab.ClientModels;
using PlayFab.Internal;
using PlayFab.SharedModels;

namespace PlayFab.Events
{
	public class PlayFabEvents
	{
		public delegate void PlayFabErrorEvent(PlayFabRequestCommon request, PlayFabError error);

		public delegate void PlayFabResultEvent<in TResult>(TResult result) where TResult : PlayFabResultCommon;

		public delegate void PlayFabRequestEvent<in TRequest>(TRequest request) where TRequest : PlayFabRequestCommon;

		private static PlayFabEvents _instance;

		public event PlayFabResultEvent<LoginResult> OnLoginResultEvent;

		public event PlayFabRequestEvent<AcceptTradeRequest> OnAcceptTradeRequestEvent;

		public event PlayFabResultEvent<AcceptTradeResponse> OnAcceptTradeResultEvent;

		public event PlayFabRequestEvent<AddFriendRequest> OnAddFriendRequestEvent;

		public event PlayFabResultEvent<AddFriendResult> OnAddFriendResultEvent;

		public event PlayFabRequestEvent<AddGenericIDRequest> OnAddGenericIDRequestEvent;

		public event PlayFabResultEvent<AddGenericIDResult> OnAddGenericIDResultEvent;

		public event PlayFabRequestEvent<AddOrUpdateContactEmailRequest> OnAddOrUpdateContactEmailRequestEvent;

		public event PlayFabResultEvent<AddOrUpdateContactEmailResult> OnAddOrUpdateContactEmailResultEvent;

		public event PlayFabRequestEvent<AddSharedGroupMembersRequest> OnAddSharedGroupMembersRequestEvent;

		public event PlayFabResultEvent<AddSharedGroupMembersResult> OnAddSharedGroupMembersResultEvent;

		public event PlayFabRequestEvent<AddUsernamePasswordRequest> OnAddUsernamePasswordRequestEvent;

		public event PlayFabResultEvent<AddUsernamePasswordResult> OnAddUsernamePasswordResultEvent;

		public event PlayFabRequestEvent<AddUserVirtualCurrencyRequest> OnAddUserVirtualCurrencyRequestEvent;

		public event PlayFabResultEvent<ModifyUserVirtualCurrencyResult> OnAddUserVirtualCurrencyResultEvent;

		public event PlayFabRequestEvent<AndroidDevicePushNotificationRegistrationRequest> OnAndroidDevicePushNotificationRegistrationRequestEvent;

		public event PlayFabResultEvent<AndroidDevicePushNotificationRegistrationResult> OnAndroidDevicePushNotificationRegistrationResultEvent;

		public event PlayFabRequestEvent<AttributeInstallRequest> OnAttributeInstallRequestEvent;

		public event PlayFabResultEvent<AttributeInstallResult> OnAttributeInstallResultEvent;

		public event PlayFabRequestEvent<CancelTradeRequest> OnCancelTradeRequestEvent;

		public event PlayFabResultEvent<CancelTradeResponse> OnCancelTradeResultEvent;

		public event PlayFabRequestEvent<ConfirmPurchaseRequest> OnConfirmPurchaseRequestEvent;

		public event PlayFabResultEvent<ConfirmPurchaseResult> OnConfirmPurchaseResultEvent;

		public event PlayFabRequestEvent<ConsumeItemRequest> OnConsumeItemRequestEvent;

		public event PlayFabResultEvent<ConsumeItemResult> OnConsumeItemResultEvent;

		public event PlayFabRequestEvent<CreateSharedGroupRequest> OnCreateSharedGroupRequestEvent;

		public event PlayFabResultEvent<CreateSharedGroupResult> OnCreateSharedGroupResultEvent;

		public event PlayFabRequestEvent<ExecuteCloudScriptRequest> OnExecuteCloudScriptRequestEvent;

		public event PlayFabResultEvent<ExecuteCloudScriptResult> OnExecuteCloudScriptResultEvent;

		public event PlayFabRequestEvent<GetAccountInfoRequest> OnGetAccountInfoRequestEvent;

		public event PlayFabResultEvent<GetAccountInfoResult> OnGetAccountInfoResultEvent;

		public event PlayFabRequestEvent<ListUsersCharactersRequest> OnGetAllUsersCharactersRequestEvent;

		public event PlayFabResultEvent<ListUsersCharactersResult> OnGetAllUsersCharactersResultEvent;

		public event PlayFabRequestEvent<GetCatalogItemsRequest> OnGetCatalogItemsRequestEvent;

		public event PlayFabResultEvent<GetCatalogItemsResult> OnGetCatalogItemsResultEvent;

		public event PlayFabRequestEvent<GetCharacterDataRequest> OnGetCharacterDataRequestEvent;

		public event PlayFabResultEvent<GetCharacterDataResult> OnGetCharacterDataResultEvent;

		public event PlayFabRequestEvent<GetCharacterInventoryRequest> OnGetCharacterInventoryRequestEvent;

		public event PlayFabResultEvent<GetCharacterInventoryResult> OnGetCharacterInventoryResultEvent;

		public event PlayFabRequestEvent<GetCharacterLeaderboardRequest> OnGetCharacterLeaderboardRequestEvent;

		public event PlayFabResultEvent<GetCharacterLeaderboardResult> OnGetCharacterLeaderboardResultEvent;

		public event PlayFabRequestEvent<GetCharacterDataRequest> OnGetCharacterReadOnlyDataRequestEvent;

		public event PlayFabResultEvent<GetCharacterDataResult> OnGetCharacterReadOnlyDataResultEvent;

		public event PlayFabRequestEvent<GetCharacterStatisticsRequest> OnGetCharacterStatisticsRequestEvent;

		public event PlayFabResultEvent<GetCharacterStatisticsResult> OnGetCharacterStatisticsResultEvent;

		public event PlayFabRequestEvent<GetContentDownloadUrlRequest> OnGetContentDownloadUrlRequestEvent;

		public event PlayFabResultEvent<GetContentDownloadUrlResult> OnGetContentDownloadUrlResultEvent;

		public event PlayFabRequestEvent<CurrentGamesRequest> OnGetCurrentGamesRequestEvent;

		public event PlayFabResultEvent<CurrentGamesResult> OnGetCurrentGamesResultEvent;

		public event PlayFabRequestEvent<GetFriendLeaderboardRequest> OnGetFriendLeaderboardRequestEvent;

		public event PlayFabResultEvent<GetLeaderboardResult> OnGetFriendLeaderboardResultEvent;

		public event PlayFabRequestEvent<GetFriendLeaderboardAroundPlayerRequest> OnGetFriendLeaderboardAroundPlayerRequestEvent;

		public event PlayFabResultEvent<GetFriendLeaderboardAroundPlayerResult> OnGetFriendLeaderboardAroundPlayerResultEvent;

		public event PlayFabRequestEvent<GetFriendsListRequest> OnGetFriendsListRequestEvent;

		public event PlayFabResultEvent<GetFriendsListResult> OnGetFriendsListResultEvent;

		public event PlayFabRequestEvent<GameServerRegionsRequest> OnGetGameServerRegionsRequestEvent;

		public event PlayFabResultEvent<GameServerRegionsResult> OnGetGameServerRegionsResultEvent;

		public event PlayFabRequestEvent<GetLeaderboardRequest> OnGetLeaderboardRequestEvent;

		public event PlayFabResultEvent<GetLeaderboardResult> OnGetLeaderboardResultEvent;

		public event PlayFabRequestEvent<GetLeaderboardAroundCharacterRequest> OnGetLeaderboardAroundCharacterRequestEvent;

		public event PlayFabResultEvent<GetLeaderboardAroundCharacterResult> OnGetLeaderboardAroundCharacterResultEvent;

		public event PlayFabRequestEvent<GetLeaderboardAroundPlayerRequest> OnGetLeaderboardAroundPlayerRequestEvent;

		public event PlayFabResultEvent<GetLeaderboardAroundPlayerResult> OnGetLeaderboardAroundPlayerResultEvent;

		public event PlayFabRequestEvent<GetLeaderboardForUsersCharactersRequest> OnGetLeaderboardForUserCharactersRequestEvent;

		public event PlayFabResultEvent<GetLeaderboardForUsersCharactersResult> OnGetLeaderboardForUserCharactersResultEvent;

		public event PlayFabRequestEvent<GetPaymentTokenRequest> OnGetPaymentTokenRequestEvent;

		public event PlayFabResultEvent<GetPaymentTokenResult> OnGetPaymentTokenResultEvent;

		public event PlayFabRequestEvent<GetPhotonAuthenticationTokenRequest> OnGetPhotonAuthenticationTokenRequestEvent;

		public event PlayFabResultEvent<GetPhotonAuthenticationTokenResult> OnGetPhotonAuthenticationTokenResultEvent;

		public event PlayFabRequestEvent<GetPlayerCombinedInfoRequest> OnGetPlayerCombinedInfoRequestEvent;

		public event PlayFabResultEvent<GetPlayerCombinedInfoResult> OnGetPlayerCombinedInfoResultEvent;

		public event PlayFabRequestEvent<GetPlayerProfileRequest> OnGetPlayerProfileRequestEvent;

		public event PlayFabResultEvent<GetPlayerProfileResult> OnGetPlayerProfileResultEvent;

		public event PlayFabRequestEvent<GetPlayerSegmentsRequest> OnGetPlayerSegmentsRequestEvent;

		public event PlayFabResultEvent<GetPlayerSegmentsResult> OnGetPlayerSegmentsResultEvent;

		public event PlayFabRequestEvent<GetPlayerStatisticsRequest> OnGetPlayerStatisticsRequestEvent;

		public event PlayFabResultEvent<GetPlayerStatisticsResult> OnGetPlayerStatisticsResultEvent;

		public event PlayFabRequestEvent<GetPlayerStatisticVersionsRequest> OnGetPlayerStatisticVersionsRequestEvent;

		public event PlayFabResultEvent<GetPlayerStatisticVersionsResult> OnGetPlayerStatisticVersionsResultEvent;

		public event PlayFabRequestEvent<GetPlayerTagsRequest> OnGetPlayerTagsRequestEvent;

		public event PlayFabResultEvent<GetPlayerTagsResult> OnGetPlayerTagsResultEvent;

		public event PlayFabRequestEvent<GetPlayerTradesRequest> OnGetPlayerTradesRequestEvent;

		public event PlayFabResultEvent<GetPlayerTradesResponse> OnGetPlayerTradesResultEvent;

		public event PlayFabRequestEvent<GetPlayFabIDsFromFacebookIDsRequest> OnGetPlayFabIDsFromFacebookIDsRequestEvent;

		public event PlayFabResultEvent<GetPlayFabIDsFromFacebookIDsResult> OnGetPlayFabIDsFromFacebookIDsResultEvent;

		public event PlayFabRequestEvent<GetPlayFabIDsFromGameCenterIDsRequest> OnGetPlayFabIDsFromGameCenterIDsRequestEvent;

		public event PlayFabResultEvent<GetPlayFabIDsFromGameCenterIDsResult> OnGetPlayFabIDsFromGameCenterIDsResultEvent;

		public event PlayFabRequestEvent<GetPlayFabIDsFromGenericIDsRequest> OnGetPlayFabIDsFromGenericIDsRequestEvent;

		public event PlayFabResultEvent<GetPlayFabIDsFromGenericIDsResult> OnGetPlayFabIDsFromGenericIDsResultEvent;

		public event PlayFabRequestEvent<GetPlayFabIDsFromGoogleIDsRequest> OnGetPlayFabIDsFromGoogleIDsRequestEvent;

		public event PlayFabResultEvent<GetPlayFabIDsFromGoogleIDsResult> OnGetPlayFabIDsFromGoogleIDsResultEvent;

		public event PlayFabRequestEvent<GetPlayFabIDsFromKongregateIDsRequest> OnGetPlayFabIDsFromKongregateIDsRequestEvent;

		public event PlayFabResultEvent<GetPlayFabIDsFromKongregateIDsResult> OnGetPlayFabIDsFromKongregateIDsResultEvent;

		public event PlayFabRequestEvent<GetPlayFabIDsFromSteamIDsRequest> OnGetPlayFabIDsFromSteamIDsRequestEvent;

		public event PlayFabResultEvent<GetPlayFabIDsFromSteamIDsResult> OnGetPlayFabIDsFromSteamIDsResultEvent;

		public event PlayFabRequestEvent<GetPlayFabIDsFromTwitchIDsRequest> OnGetPlayFabIDsFromTwitchIDsRequestEvent;

		public event PlayFabResultEvent<GetPlayFabIDsFromTwitchIDsResult> OnGetPlayFabIDsFromTwitchIDsResultEvent;

		public event PlayFabRequestEvent<GetPublisherDataRequest> OnGetPublisherDataRequestEvent;

		public event PlayFabResultEvent<GetPublisherDataResult> OnGetPublisherDataResultEvent;

		public event PlayFabRequestEvent<GetPurchaseRequest> OnGetPurchaseRequestEvent;

		public event PlayFabResultEvent<GetPurchaseResult> OnGetPurchaseResultEvent;

		public event PlayFabRequestEvent<GetSharedGroupDataRequest> OnGetSharedGroupDataRequestEvent;

		public event PlayFabResultEvent<GetSharedGroupDataResult> OnGetSharedGroupDataResultEvent;

		public event PlayFabRequestEvent<GetStoreItemsRequest> OnGetStoreItemsRequestEvent;

		public event PlayFabResultEvent<GetStoreItemsResult> OnGetStoreItemsResultEvent;

		public event PlayFabRequestEvent<GetTimeRequest> OnGetTimeRequestEvent;

		public event PlayFabResultEvent<GetTimeResult> OnGetTimeResultEvent;

		public event PlayFabRequestEvent<GetTitleDataRequest> OnGetTitleDataRequestEvent;

		public event PlayFabResultEvent<GetTitleDataResult> OnGetTitleDataResultEvent;

		public event PlayFabRequestEvent<GetTitleNewsRequest> OnGetTitleNewsRequestEvent;

		public event PlayFabResultEvent<GetTitleNewsResult> OnGetTitleNewsResultEvent;

		public event PlayFabRequestEvent<GetTitlePublicKeyRequest> OnGetTitlePublicKeyRequestEvent;

		public event PlayFabResultEvent<GetTitlePublicKeyResult> OnGetTitlePublicKeyResultEvent;

		public event PlayFabRequestEvent<GetTradeStatusRequest> OnGetTradeStatusRequestEvent;

		public event PlayFabResultEvent<GetTradeStatusResponse> OnGetTradeStatusResultEvent;

		public event PlayFabRequestEvent<GetUserDataRequest> OnGetUserDataRequestEvent;

		public event PlayFabResultEvent<GetUserDataResult> OnGetUserDataResultEvent;

		public event PlayFabRequestEvent<GetUserInventoryRequest> OnGetUserInventoryRequestEvent;

		public event PlayFabResultEvent<GetUserInventoryResult> OnGetUserInventoryResultEvent;

		public event PlayFabRequestEvent<GetUserDataRequest> OnGetUserPublisherDataRequestEvent;

		public event PlayFabResultEvent<GetUserDataResult> OnGetUserPublisherDataResultEvent;

		public event PlayFabRequestEvent<GetUserDataRequest> OnGetUserPublisherReadOnlyDataRequestEvent;

		public event PlayFabResultEvent<GetUserDataResult> OnGetUserPublisherReadOnlyDataResultEvent;

		public event PlayFabRequestEvent<GetUserDataRequest> OnGetUserReadOnlyDataRequestEvent;

		public event PlayFabResultEvent<GetUserDataResult> OnGetUserReadOnlyDataResultEvent;

		public event PlayFabRequestEvent<GetWindowsHelloChallengeRequest> OnGetWindowsHelloChallengeRequestEvent;

		public event PlayFabResultEvent<GetWindowsHelloChallengeResponse> OnGetWindowsHelloChallengeResultEvent;

		public event PlayFabRequestEvent<GrantCharacterToUserRequest> OnGrantCharacterToUserRequestEvent;

		public event PlayFabResultEvent<GrantCharacterToUserResult> OnGrantCharacterToUserResultEvent;

		public event PlayFabRequestEvent<LinkAndroidDeviceIDRequest> OnLinkAndroidDeviceIDRequestEvent;

		public event PlayFabResultEvent<LinkAndroidDeviceIDResult> OnLinkAndroidDeviceIDResultEvent;

		public event PlayFabRequestEvent<LinkCustomIDRequest> OnLinkCustomIDRequestEvent;

		public event PlayFabResultEvent<LinkCustomIDResult> OnLinkCustomIDResultEvent;

		public event PlayFabRequestEvent<LinkFacebookAccountRequest> OnLinkFacebookAccountRequestEvent;

		public event PlayFabResultEvent<LinkFacebookAccountResult> OnLinkFacebookAccountResultEvent;

		public event PlayFabRequestEvent<LinkGameCenterAccountRequest> OnLinkGameCenterAccountRequestEvent;

		public event PlayFabResultEvent<LinkGameCenterAccountResult> OnLinkGameCenterAccountResultEvent;

		public event PlayFabRequestEvent<LinkGoogleAccountRequest> OnLinkGoogleAccountRequestEvent;

		public event PlayFabResultEvent<LinkGoogleAccountResult> OnLinkGoogleAccountResultEvent;

		public event PlayFabRequestEvent<LinkIOSDeviceIDRequest> OnLinkIOSDeviceIDRequestEvent;

		public event PlayFabResultEvent<LinkIOSDeviceIDResult> OnLinkIOSDeviceIDResultEvent;

		public event PlayFabRequestEvent<LinkKongregateAccountRequest> OnLinkKongregateRequestEvent;

		public event PlayFabResultEvent<LinkKongregateAccountResult> OnLinkKongregateResultEvent;

		public event PlayFabRequestEvent<LinkSteamAccountRequest> OnLinkSteamAccountRequestEvent;

		public event PlayFabResultEvent<LinkSteamAccountResult> OnLinkSteamAccountResultEvent;

		public event PlayFabRequestEvent<LinkTwitchAccountRequest> OnLinkTwitchRequestEvent;

		public event PlayFabResultEvent<LinkTwitchAccountResult> OnLinkTwitchResultEvent;

		public event PlayFabRequestEvent<LinkWindowsHelloAccountRequest> OnLinkWindowsHelloRequestEvent;

		public event PlayFabResultEvent<LinkWindowsHelloAccountResponse> OnLinkWindowsHelloResultEvent;

		public event PlayFabRequestEvent<LoginWithAndroidDeviceIDRequest> OnLoginWithAndroidDeviceIDRequestEvent;

		public event PlayFabRequestEvent<LoginWithCustomIDRequest> OnLoginWithCustomIDRequestEvent;

		public event PlayFabRequestEvent<LoginWithEmailAddressRequest> OnLoginWithEmailAddressRequestEvent;

		public event PlayFabRequestEvent<LoginWithFacebookRequest> OnLoginWithFacebookRequestEvent;

		public event PlayFabRequestEvent<LoginWithGameCenterRequest> OnLoginWithGameCenterRequestEvent;

		public event PlayFabRequestEvent<LoginWithGoogleAccountRequest> OnLoginWithGoogleAccountRequestEvent;

		public event PlayFabRequestEvent<LoginWithIOSDeviceIDRequest> OnLoginWithIOSDeviceIDRequestEvent;

		public event PlayFabRequestEvent<LoginWithKongregateRequest> OnLoginWithKongregateRequestEvent;

		public event PlayFabRequestEvent<LoginWithPlayFabRequest> OnLoginWithPlayFabRequestEvent;

		public event PlayFabRequestEvent<LoginWithSteamRequest> OnLoginWithSteamRequestEvent;

		public event PlayFabRequestEvent<LoginWithTwitchRequest> OnLoginWithTwitchRequestEvent;

		public event PlayFabRequestEvent<LoginWithWindowsHelloRequest> OnLoginWithWindowsHelloRequestEvent;

		public event PlayFabRequestEvent<MatchmakeRequest> OnMatchmakeRequestEvent;

		public event PlayFabResultEvent<MatchmakeResult> OnMatchmakeResultEvent;

		public event PlayFabRequestEvent<OpenTradeRequest> OnOpenTradeRequestEvent;

		public event PlayFabResultEvent<OpenTradeResponse> OnOpenTradeResultEvent;

		public event PlayFabRequestEvent<PayForPurchaseRequest> OnPayForPurchaseRequestEvent;

		public event PlayFabResultEvent<PayForPurchaseResult> OnPayForPurchaseResultEvent;

		public event PlayFabRequestEvent<PurchaseItemRequest> OnPurchaseItemRequestEvent;

		public event PlayFabResultEvent<PurchaseItemResult> OnPurchaseItemResultEvent;

		public event PlayFabRequestEvent<RedeemCouponRequest> OnRedeemCouponRequestEvent;

		public event PlayFabResultEvent<RedeemCouponResult> OnRedeemCouponResultEvent;

		public event PlayFabRequestEvent<RegisterForIOSPushNotificationRequest> OnRegisterForIOSPushNotificationRequestEvent;

		public event PlayFabResultEvent<RegisterForIOSPushNotificationResult> OnRegisterForIOSPushNotificationResultEvent;

		public event PlayFabRequestEvent<RegisterPlayFabUserRequest> OnRegisterPlayFabUserRequestEvent;

		public event PlayFabResultEvent<RegisterPlayFabUserResult> OnRegisterPlayFabUserResultEvent;

		public event PlayFabRequestEvent<RegisterWithWindowsHelloRequest> OnRegisterWithWindowsHelloRequestEvent;

		public event PlayFabRequestEvent<RemoveContactEmailRequest> OnRemoveContactEmailRequestEvent;

		public event PlayFabResultEvent<RemoveContactEmailResult> OnRemoveContactEmailResultEvent;

		public event PlayFabRequestEvent<RemoveFriendRequest> OnRemoveFriendRequestEvent;

		public event PlayFabResultEvent<RemoveFriendResult> OnRemoveFriendResultEvent;

		public event PlayFabRequestEvent<RemoveGenericIDRequest> OnRemoveGenericIDRequestEvent;

		public event PlayFabResultEvent<RemoveGenericIDResult> OnRemoveGenericIDResultEvent;

		public event PlayFabRequestEvent<RemoveSharedGroupMembersRequest> OnRemoveSharedGroupMembersRequestEvent;

		public event PlayFabResultEvent<RemoveSharedGroupMembersResult> OnRemoveSharedGroupMembersResultEvent;

		public event PlayFabRequestEvent<DeviceInfoRequest> OnReportDeviceInfoRequestEvent;

		public event PlayFabResultEvent<EmptyResult> OnReportDeviceInfoResultEvent;

		public event PlayFabRequestEvent<ReportPlayerClientRequest> OnReportPlayerRequestEvent;

		public event PlayFabResultEvent<ReportPlayerClientResult> OnReportPlayerResultEvent;

		public event PlayFabRequestEvent<RestoreIOSPurchasesRequest> OnRestoreIOSPurchasesRequestEvent;

		public event PlayFabResultEvent<RestoreIOSPurchasesResult> OnRestoreIOSPurchasesResultEvent;

		public event PlayFabRequestEvent<SendAccountRecoveryEmailRequest> OnSendAccountRecoveryEmailRequestEvent;

		public event PlayFabResultEvent<SendAccountRecoveryEmailResult> OnSendAccountRecoveryEmailResultEvent;

		public event PlayFabRequestEvent<SetFriendTagsRequest> OnSetFriendTagsRequestEvent;

		public event PlayFabResultEvent<SetFriendTagsResult> OnSetFriendTagsResultEvent;

		public event PlayFabRequestEvent<SetPlayerSecretRequest> OnSetPlayerSecretRequestEvent;

		public event PlayFabResultEvent<SetPlayerSecretResult> OnSetPlayerSecretResultEvent;

		public event PlayFabRequestEvent<StartGameRequest> OnStartGameRequestEvent;

		public event PlayFabResultEvent<StartGameResult> OnStartGameResultEvent;

		public event PlayFabRequestEvent<StartPurchaseRequest> OnStartPurchaseRequestEvent;

		public event PlayFabResultEvent<StartPurchaseResult> OnStartPurchaseResultEvent;

		public event PlayFabRequestEvent<SubtractUserVirtualCurrencyRequest> OnSubtractUserVirtualCurrencyRequestEvent;

		public event PlayFabResultEvent<ModifyUserVirtualCurrencyResult> OnSubtractUserVirtualCurrencyResultEvent;

		public event PlayFabRequestEvent<UnlinkAndroidDeviceIDRequest> OnUnlinkAndroidDeviceIDRequestEvent;

		public event PlayFabResultEvent<UnlinkAndroidDeviceIDResult> OnUnlinkAndroidDeviceIDResultEvent;

		public event PlayFabRequestEvent<UnlinkCustomIDRequest> OnUnlinkCustomIDRequestEvent;

		public event PlayFabResultEvent<UnlinkCustomIDResult> OnUnlinkCustomIDResultEvent;

		public event PlayFabRequestEvent<UnlinkFacebookAccountRequest> OnUnlinkFacebookAccountRequestEvent;

		public event PlayFabResultEvent<UnlinkFacebookAccountResult> OnUnlinkFacebookAccountResultEvent;

		public event PlayFabRequestEvent<UnlinkGameCenterAccountRequest> OnUnlinkGameCenterAccountRequestEvent;

		public event PlayFabResultEvent<UnlinkGameCenterAccountResult> OnUnlinkGameCenterAccountResultEvent;

		public event PlayFabRequestEvent<UnlinkGoogleAccountRequest> OnUnlinkGoogleAccountRequestEvent;

		public event PlayFabResultEvent<UnlinkGoogleAccountResult> OnUnlinkGoogleAccountResultEvent;

		public event PlayFabRequestEvent<UnlinkIOSDeviceIDRequest> OnUnlinkIOSDeviceIDRequestEvent;

		public event PlayFabResultEvent<UnlinkIOSDeviceIDResult> OnUnlinkIOSDeviceIDResultEvent;

		public event PlayFabRequestEvent<UnlinkKongregateAccountRequest> OnUnlinkKongregateRequestEvent;

		public event PlayFabResultEvent<UnlinkKongregateAccountResult> OnUnlinkKongregateResultEvent;

		public event PlayFabRequestEvent<UnlinkSteamAccountRequest> OnUnlinkSteamAccountRequestEvent;

		public event PlayFabResultEvent<UnlinkSteamAccountResult> OnUnlinkSteamAccountResultEvent;

		public event PlayFabRequestEvent<UnlinkTwitchAccountRequest> OnUnlinkTwitchRequestEvent;

		public event PlayFabResultEvent<UnlinkTwitchAccountResult> OnUnlinkTwitchResultEvent;

		public event PlayFabRequestEvent<UnlinkWindowsHelloAccountRequest> OnUnlinkWindowsHelloRequestEvent;

		public event PlayFabResultEvent<UnlinkWindowsHelloAccountResponse> OnUnlinkWindowsHelloResultEvent;

		public event PlayFabRequestEvent<UnlockContainerInstanceRequest> OnUnlockContainerInstanceRequestEvent;

		public event PlayFabResultEvent<UnlockContainerItemResult> OnUnlockContainerInstanceResultEvent;

		public event PlayFabRequestEvent<UnlockContainerItemRequest> OnUnlockContainerItemRequestEvent;

		public event PlayFabResultEvent<UnlockContainerItemResult> OnUnlockContainerItemResultEvent;

		public event PlayFabRequestEvent<UpdateAvatarUrlRequest> OnUpdateAvatarUrlRequestEvent;

		public event PlayFabResultEvent<EmptyResult> OnUpdateAvatarUrlResultEvent;

		public event PlayFabRequestEvent<UpdateCharacterDataRequest> OnUpdateCharacterDataRequestEvent;

		public event PlayFabResultEvent<UpdateCharacterDataResult> OnUpdateCharacterDataResultEvent;

		public event PlayFabRequestEvent<UpdateCharacterStatisticsRequest> OnUpdateCharacterStatisticsRequestEvent;

		public event PlayFabResultEvent<UpdateCharacterStatisticsResult> OnUpdateCharacterStatisticsResultEvent;

		public event PlayFabRequestEvent<UpdatePlayerStatisticsRequest> OnUpdatePlayerStatisticsRequestEvent;

		public event PlayFabResultEvent<UpdatePlayerStatisticsResult> OnUpdatePlayerStatisticsResultEvent;

		public event PlayFabRequestEvent<UpdateSharedGroupDataRequest> OnUpdateSharedGroupDataRequestEvent;

		public event PlayFabResultEvent<UpdateSharedGroupDataResult> OnUpdateSharedGroupDataResultEvent;

		public event PlayFabRequestEvent<UpdateUserDataRequest> OnUpdateUserDataRequestEvent;

		public event PlayFabResultEvent<UpdateUserDataResult> OnUpdateUserDataResultEvent;

		public event PlayFabRequestEvent<UpdateUserDataRequest> OnUpdateUserPublisherDataRequestEvent;

		public event PlayFabResultEvent<UpdateUserDataResult> OnUpdateUserPublisherDataResultEvent;

		public event PlayFabRequestEvent<UpdateUserTitleDisplayNameRequest> OnUpdateUserTitleDisplayNameRequestEvent;

		public event PlayFabResultEvent<UpdateUserTitleDisplayNameResult> OnUpdateUserTitleDisplayNameResultEvent;

		public event PlayFabRequestEvent<ValidateAmazonReceiptRequest> OnValidateAmazonIAPReceiptRequestEvent;

		public event PlayFabResultEvent<ValidateAmazonReceiptResult> OnValidateAmazonIAPReceiptResultEvent;

		public event PlayFabRequestEvent<ValidateGooglePlayPurchaseRequest> OnValidateGooglePlayPurchaseRequestEvent;

		public event PlayFabResultEvent<ValidateGooglePlayPurchaseResult> OnValidateGooglePlayPurchaseResultEvent;

		public event PlayFabRequestEvent<ValidateIOSReceiptRequest> OnValidateIOSReceiptRequestEvent;

		public event PlayFabResultEvent<ValidateIOSReceiptResult> OnValidateIOSReceiptResultEvent;

		public event PlayFabRequestEvent<ValidateWindowsReceiptRequest> OnValidateWindowsStoreReceiptRequestEvent;

		public event PlayFabResultEvent<ValidateWindowsReceiptResult> OnValidateWindowsStoreReceiptResultEvent;

		public event PlayFabRequestEvent<WriteClientCharacterEventRequest> OnWriteCharacterEventRequestEvent;

		public event PlayFabResultEvent<WriteEventResponse> OnWriteCharacterEventResultEvent;

		public event PlayFabRequestEvent<WriteClientPlayerEventRequest> OnWritePlayerEventRequestEvent;

		public event PlayFabResultEvent<WriteEventResponse> OnWritePlayerEventResultEvent;

		public event PlayFabRequestEvent<WriteTitleEventRequest> OnWriteTitleEventRequestEvent;

		public event PlayFabResultEvent<WriteEventResponse> OnWriteTitleEventResultEvent;

		public event PlayFabErrorEvent OnGlobalErrorEvent;

		private PlayFabEvents()
		{
		}

		public static PlayFabEvents Init()
		{
			if (_instance == null)
			{
				_instance = new PlayFabEvents();
			}
			PlayFabHttp.ApiProcessingEventHandler += _instance.OnProcessingEvent;
			PlayFabHttp.ApiProcessingErrorEventHandler += _instance.OnProcessingErrorEvent;
			return _instance;
		}

		public void UnregisterInstance(object instance)
		{
			Delegate[] invocationList;
			if (this.OnLoginResultEvent != null)
			{
				invocationList = this.OnLoginResultEvent.GetInvocationList();
				foreach (Delegate obj in invocationList)
				{
					if (obj.Target == instance)
					{
						OnLoginResultEvent -= (PlayFabResultEvent<LoginResult>)obj;
					}
				}
			}
			if (this.OnAcceptTradeRequestEvent != null)
			{
				invocationList = this.OnAcceptTradeRequestEvent.GetInvocationList();
				foreach (Delegate obj2 in invocationList)
				{
					if (obj2.Target == instance)
					{
						OnAcceptTradeRequestEvent -= (PlayFabRequestEvent<AcceptTradeRequest>)obj2;
					}
				}
			}
			if (this.OnAcceptTradeResultEvent != null)
			{
				invocationList = this.OnAcceptTradeResultEvent.GetInvocationList();
				foreach (Delegate obj3 in invocationList)
				{
					if (obj3.Target == instance)
					{
						OnAcceptTradeResultEvent -= (PlayFabResultEvent<AcceptTradeResponse>)obj3;
					}
				}
			}
			if (this.OnAddFriendRequestEvent != null)
			{
				invocationList = this.OnAddFriendRequestEvent.GetInvocationList();
				foreach (Delegate obj4 in invocationList)
				{
					if (obj4.Target == instance)
					{
						OnAddFriendRequestEvent -= (PlayFabRequestEvent<AddFriendRequest>)obj4;
					}
				}
			}
			if (this.OnAddFriendResultEvent != null)
			{
				invocationList = this.OnAddFriendResultEvent.GetInvocationList();
				foreach (Delegate obj5 in invocationList)
				{
					if (obj5.Target == instance)
					{
						OnAddFriendResultEvent -= (PlayFabResultEvent<AddFriendResult>)obj5;
					}
				}
			}
			if (this.OnAddGenericIDRequestEvent != null)
			{
				invocationList = this.OnAddGenericIDRequestEvent.GetInvocationList();
				foreach (Delegate obj6 in invocationList)
				{
					if (obj6.Target == instance)
					{
						OnAddGenericIDRequestEvent -= (PlayFabRequestEvent<AddGenericIDRequest>)obj6;
					}
				}
			}
			if (this.OnAddGenericIDResultEvent != null)
			{
				invocationList = this.OnAddGenericIDResultEvent.GetInvocationList();
				foreach (Delegate obj7 in invocationList)
				{
					if (obj7.Target == instance)
					{
						OnAddGenericIDResultEvent -= (PlayFabResultEvent<AddGenericIDResult>)obj7;
					}
				}
			}
			if (this.OnAddOrUpdateContactEmailRequestEvent != null)
			{
				invocationList = this.OnAddOrUpdateContactEmailRequestEvent.GetInvocationList();
				foreach (Delegate obj8 in invocationList)
				{
					if (obj8.Target == instance)
					{
						OnAddOrUpdateContactEmailRequestEvent -= (PlayFabRequestEvent<AddOrUpdateContactEmailRequest>)obj8;
					}
				}
			}
			if (this.OnAddOrUpdateContactEmailResultEvent != null)
			{
				invocationList = this.OnAddOrUpdateContactEmailResultEvent.GetInvocationList();
				foreach (Delegate obj9 in invocationList)
				{
					if (obj9.Target == instance)
					{
						OnAddOrUpdateContactEmailResultEvent -= (PlayFabResultEvent<AddOrUpdateContactEmailResult>)obj9;
					}
				}
			}
			if (this.OnAddSharedGroupMembersRequestEvent != null)
			{
				invocationList = this.OnAddSharedGroupMembersRequestEvent.GetInvocationList();
				foreach (Delegate obj10 in invocationList)
				{
					if (obj10.Target == instance)
					{
						OnAddSharedGroupMembersRequestEvent -= (PlayFabRequestEvent<AddSharedGroupMembersRequest>)obj10;
					}
				}
			}
			if (this.OnAddSharedGroupMembersResultEvent != null)
			{
				invocationList = this.OnAddSharedGroupMembersResultEvent.GetInvocationList();
				foreach (Delegate obj11 in invocationList)
				{
					if (obj11.Target == instance)
					{
						OnAddSharedGroupMembersResultEvent -= (PlayFabResultEvent<AddSharedGroupMembersResult>)obj11;
					}
				}
			}
			if (this.OnAddUsernamePasswordRequestEvent != null)
			{
				invocationList = this.OnAddUsernamePasswordRequestEvent.GetInvocationList();
				foreach (Delegate obj12 in invocationList)
				{
					if (obj12.Target == instance)
					{
						OnAddUsernamePasswordRequestEvent -= (PlayFabRequestEvent<AddUsernamePasswordRequest>)obj12;
					}
				}
			}
			if (this.OnAddUsernamePasswordResultEvent != null)
			{
				invocationList = this.OnAddUsernamePasswordResultEvent.GetInvocationList();
				foreach (Delegate obj13 in invocationList)
				{
					if (obj13.Target == instance)
					{
						OnAddUsernamePasswordResultEvent -= (PlayFabResultEvent<AddUsernamePasswordResult>)obj13;
					}
				}
			}
			if (this.OnAddUserVirtualCurrencyRequestEvent != null)
			{
				invocationList = this.OnAddUserVirtualCurrencyRequestEvent.GetInvocationList();
				foreach (Delegate obj14 in invocationList)
				{
					if (obj14.Target == instance)
					{
						OnAddUserVirtualCurrencyRequestEvent -= (PlayFabRequestEvent<AddUserVirtualCurrencyRequest>)obj14;
					}
				}
			}
			if (this.OnAddUserVirtualCurrencyResultEvent != null)
			{
				invocationList = this.OnAddUserVirtualCurrencyResultEvent.GetInvocationList();
				foreach (Delegate obj15 in invocationList)
				{
					if (obj15.Target == instance)
					{
						OnAddUserVirtualCurrencyResultEvent -= (PlayFabResultEvent<ModifyUserVirtualCurrencyResult>)obj15;
					}
				}
			}
			if (this.OnAndroidDevicePushNotificationRegistrationRequestEvent != null)
			{
				invocationList = this.OnAndroidDevicePushNotificationRegistrationRequestEvent.GetInvocationList();
				foreach (Delegate obj16 in invocationList)
				{
					if (obj16.Target == instance)
					{
						OnAndroidDevicePushNotificationRegistrationRequestEvent -= (PlayFabRequestEvent<AndroidDevicePushNotificationRegistrationRequest>)obj16;
					}
				}
			}
			if (this.OnAndroidDevicePushNotificationRegistrationResultEvent != null)
			{
				invocationList = this.OnAndroidDevicePushNotificationRegistrationResultEvent.GetInvocationList();
				foreach (Delegate obj17 in invocationList)
				{
					if (obj17.Target == instance)
					{
						OnAndroidDevicePushNotificationRegistrationResultEvent -= (PlayFabResultEvent<AndroidDevicePushNotificationRegistrationResult>)obj17;
					}
				}
			}
			if (this.OnAttributeInstallRequestEvent != null)
			{
				invocationList = this.OnAttributeInstallRequestEvent.GetInvocationList();
				foreach (Delegate obj18 in invocationList)
				{
					if (obj18.Target == instance)
					{
						OnAttributeInstallRequestEvent -= (PlayFabRequestEvent<AttributeInstallRequest>)obj18;
					}
				}
			}
			if (this.OnAttributeInstallResultEvent != null)
			{
				invocationList = this.OnAttributeInstallResultEvent.GetInvocationList();
				foreach (Delegate obj19 in invocationList)
				{
					if (obj19.Target == instance)
					{
						OnAttributeInstallResultEvent -= (PlayFabResultEvent<AttributeInstallResult>)obj19;
					}
				}
			}
			if (this.OnCancelTradeRequestEvent != null)
			{
				invocationList = this.OnCancelTradeRequestEvent.GetInvocationList();
				foreach (Delegate obj20 in invocationList)
				{
					if (obj20.Target == instance)
					{
						OnCancelTradeRequestEvent -= (PlayFabRequestEvent<CancelTradeRequest>)obj20;
					}
				}
			}
			if (this.OnCancelTradeResultEvent != null)
			{
				invocationList = this.OnCancelTradeResultEvent.GetInvocationList();
				foreach (Delegate obj21 in invocationList)
				{
					if (obj21.Target == instance)
					{
						OnCancelTradeResultEvent -= (PlayFabResultEvent<CancelTradeResponse>)obj21;
					}
				}
			}
			if (this.OnConfirmPurchaseRequestEvent != null)
			{
				invocationList = this.OnConfirmPurchaseRequestEvent.GetInvocationList();
				foreach (Delegate obj22 in invocationList)
				{
					if (obj22.Target == instance)
					{
						OnConfirmPurchaseRequestEvent -= (PlayFabRequestEvent<ConfirmPurchaseRequest>)obj22;
					}
				}
			}
			if (this.OnConfirmPurchaseResultEvent != null)
			{
				invocationList = this.OnConfirmPurchaseResultEvent.GetInvocationList();
				foreach (Delegate obj23 in invocationList)
				{
					if (obj23.Target == instance)
					{
						OnConfirmPurchaseResultEvent -= (PlayFabResultEvent<ConfirmPurchaseResult>)obj23;
					}
				}
			}
			if (this.OnConsumeItemRequestEvent != null)
			{
				invocationList = this.OnConsumeItemRequestEvent.GetInvocationList();
				foreach (Delegate obj24 in invocationList)
				{
					if (obj24.Target == instance)
					{
						OnConsumeItemRequestEvent -= (PlayFabRequestEvent<ConsumeItemRequest>)obj24;
					}
				}
			}
			if (this.OnConsumeItemResultEvent != null)
			{
				invocationList = this.OnConsumeItemResultEvent.GetInvocationList();
				foreach (Delegate obj25 in invocationList)
				{
					if (obj25.Target == instance)
					{
						OnConsumeItemResultEvent -= (PlayFabResultEvent<ConsumeItemResult>)obj25;
					}
				}
			}
			if (this.OnCreateSharedGroupRequestEvent != null)
			{
				invocationList = this.OnCreateSharedGroupRequestEvent.GetInvocationList();
				foreach (Delegate obj26 in invocationList)
				{
					if (obj26.Target == instance)
					{
						OnCreateSharedGroupRequestEvent -= (PlayFabRequestEvent<CreateSharedGroupRequest>)obj26;
					}
				}
			}
			if (this.OnCreateSharedGroupResultEvent != null)
			{
				invocationList = this.OnCreateSharedGroupResultEvent.GetInvocationList();
				foreach (Delegate obj27 in invocationList)
				{
					if (obj27.Target == instance)
					{
						OnCreateSharedGroupResultEvent -= (PlayFabResultEvent<CreateSharedGroupResult>)obj27;
					}
				}
			}
			if (this.OnExecuteCloudScriptRequestEvent != null)
			{
				invocationList = this.OnExecuteCloudScriptRequestEvent.GetInvocationList();
				foreach (Delegate obj28 in invocationList)
				{
					if (obj28.Target == instance)
					{
						OnExecuteCloudScriptRequestEvent -= (PlayFabRequestEvent<ExecuteCloudScriptRequest>)obj28;
					}
				}
			}
			if (this.OnExecuteCloudScriptResultEvent != null)
			{
				invocationList = this.OnExecuteCloudScriptResultEvent.GetInvocationList();
				foreach (Delegate obj29 in invocationList)
				{
					if (obj29.Target == instance)
					{
						OnExecuteCloudScriptResultEvent -= (PlayFabResultEvent<ExecuteCloudScriptResult>)obj29;
					}
				}
			}
			if (this.OnGetAccountInfoRequestEvent != null)
			{
				invocationList = this.OnGetAccountInfoRequestEvent.GetInvocationList();
				foreach (Delegate obj30 in invocationList)
				{
					if (obj30.Target == instance)
					{
						OnGetAccountInfoRequestEvent -= (PlayFabRequestEvent<GetAccountInfoRequest>)obj30;
					}
				}
			}
			if (this.OnGetAccountInfoResultEvent != null)
			{
				invocationList = this.OnGetAccountInfoResultEvent.GetInvocationList();
				foreach (Delegate obj31 in invocationList)
				{
					if (obj31.Target == instance)
					{
						OnGetAccountInfoResultEvent -= (PlayFabResultEvent<GetAccountInfoResult>)obj31;
					}
				}
			}
			if (this.OnGetAllUsersCharactersRequestEvent != null)
			{
				invocationList = this.OnGetAllUsersCharactersRequestEvent.GetInvocationList();
				foreach (Delegate obj32 in invocationList)
				{
					if (obj32.Target == instance)
					{
						OnGetAllUsersCharactersRequestEvent -= (PlayFabRequestEvent<ListUsersCharactersRequest>)obj32;
					}
				}
			}
			if (this.OnGetAllUsersCharactersResultEvent != null)
			{
				invocationList = this.OnGetAllUsersCharactersResultEvent.GetInvocationList();
				foreach (Delegate obj33 in invocationList)
				{
					if (obj33.Target == instance)
					{
						OnGetAllUsersCharactersResultEvent -= (PlayFabResultEvent<ListUsersCharactersResult>)obj33;
					}
				}
			}
			if (this.OnGetCatalogItemsRequestEvent != null)
			{
				invocationList = this.OnGetCatalogItemsRequestEvent.GetInvocationList();
				foreach (Delegate obj34 in invocationList)
				{
					if (obj34.Target == instance)
					{
						OnGetCatalogItemsRequestEvent -= (PlayFabRequestEvent<GetCatalogItemsRequest>)obj34;
					}
				}
			}
			if (this.OnGetCatalogItemsResultEvent != null)
			{
				invocationList = this.OnGetCatalogItemsResultEvent.GetInvocationList();
				foreach (Delegate obj35 in invocationList)
				{
					if (obj35.Target == instance)
					{
						OnGetCatalogItemsResultEvent -= (PlayFabResultEvent<GetCatalogItemsResult>)obj35;
					}
				}
			}
			if (this.OnGetCharacterDataRequestEvent != null)
			{
				invocationList = this.OnGetCharacterDataRequestEvent.GetInvocationList();
				foreach (Delegate obj36 in invocationList)
				{
					if (obj36.Target == instance)
					{
						OnGetCharacterDataRequestEvent -= (PlayFabRequestEvent<GetCharacterDataRequest>)obj36;
					}
				}
			}
			if (this.OnGetCharacterDataResultEvent != null)
			{
				invocationList = this.OnGetCharacterDataResultEvent.GetInvocationList();
				foreach (Delegate obj37 in invocationList)
				{
					if (obj37.Target == instance)
					{
						OnGetCharacterDataResultEvent -= (PlayFabResultEvent<GetCharacterDataResult>)obj37;
					}
				}
			}
			if (this.OnGetCharacterInventoryRequestEvent != null)
			{
				invocationList = this.OnGetCharacterInventoryRequestEvent.GetInvocationList();
				foreach (Delegate obj38 in invocationList)
				{
					if (obj38.Target == instance)
					{
						OnGetCharacterInventoryRequestEvent -= (PlayFabRequestEvent<GetCharacterInventoryRequest>)obj38;
					}
				}
			}
			if (this.OnGetCharacterInventoryResultEvent != null)
			{
				invocationList = this.OnGetCharacterInventoryResultEvent.GetInvocationList();
				foreach (Delegate obj39 in invocationList)
				{
					if (obj39.Target == instance)
					{
						OnGetCharacterInventoryResultEvent -= (PlayFabResultEvent<GetCharacterInventoryResult>)obj39;
					}
				}
			}
			if (this.OnGetCharacterLeaderboardRequestEvent != null)
			{
				invocationList = this.OnGetCharacterLeaderboardRequestEvent.GetInvocationList();
				foreach (Delegate obj40 in invocationList)
				{
					if (obj40.Target == instance)
					{
						OnGetCharacterLeaderboardRequestEvent -= (PlayFabRequestEvent<GetCharacterLeaderboardRequest>)obj40;
					}
				}
			}
			if (this.OnGetCharacterLeaderboardResultEvent != null)
			{
				invocationList = this.OnGetCharacterLeaderboardResultEvent.GetInvocationList();
				foreach (Delegate obj41 in invocationList)
				{
					if (obj41.Target == instance)
					{
						OnGetCharacterLeaderboardResultEvent -= (PlayFabResultEvent<GetCharacterLeaderboardResult>)obj41;
					}
				}
			}
			if (this.OnGetCharacterReadOnlyDataRequestEvent != null)
			{
				invocationList = this.OnGetCharacterReadOnlyDataRequestEvent.GetInvocationList();
				foreach (Delegate obj42 in invocationList)
				{
					if (obj42.Target == instance)
					{
						OnGetCharacterReadOnlyDataRequestEvent -= (PlayFabRequestEvent<GetCharacterDataRequest>)obj42;
					}
				}
			}
			if (this.OnGetCharacterReadOnlyDataResultEvent != null)
			{
				invocationList = this.OnGetCharacterReadOnlyDataResultEvent.GetInvocationList();
				foreach (Delegate obj43 in invocationList)
				{
					if (obj43.Target == instance)
					{
						OnGetCharacterReadOnlyDataResultEvent -= (PlayFabResultEvent<GetCharacterDataResult>)obj43;
					}
				}
			}
			if (this.OnGetCharacterStatisticsRequestEvent != null)
			{
				invocationList = this.OnGetCharacterStatisticsRequestEvent.GetInvocationList();
				foreach (Delegate obj44 in invocationList)
				{
					if (obj44.Target == instance)
					{
						OnGetCharacterStatisticsRequestEvent -= (PlayFabRequestEvent<GetCharacterStatisticsRequest>)obj44;
					}
				}
			}
			if (this.OnGetCharacterStatisticsResultEvent != null)
			{
				invocationList = this.OnGetCharacterStatisticsResultEvent.GetInvocationList();
				foreach (Delegate obj45 in invocationList)
				{
					if (obj45.Target == instance)
					{
						OnGetCharacterStatisticsResultEvent -= (PlayFabResultEvent<GetCharacterStatisticsResult>)obj45;
					}
				}
			}
			if (this.OnGetContentDownloadUrlRequestEvent != null)
			{
				invocationList = this.OnGetContentDownloadUrlRequestEvent.GetInvocationList();
				foreach (Delegate obj46 in invocationList)
				{
					if (obj46.Target == instance)
					{
						OnGetContentDownloadUrlRequestEvent -= (PlayFabRequestEvent<GetContentDownloadUrlRequest>)obj46;
					}
				}
			}
			if (this.OnGetContentDownloadUrlResultEvent != null)
			{
				invocationList = this.OnGetContentDownloadUrlResultEvent.GetInvocationList();
				foreach (Delegate obj47 in invocationList)
				{
					if (obj47.Target == instance)
					{
						OnGetContentDownloadUrlResultEvent -= (PlayFabResultEvent<GetContentDownloadUrlResult>)obj47;
					}
				}
			}
			if (this.OnGetCurrentGamesRequestEvent != null)
			{
				invocationList = this.OnGetCurrentGamesRequestEvent.GetInvocationList();
				foreach (Delegate obj48 in invocationList)
				{
					if (obj48.Target == instance)
					{
						OnGetCurrentGamesRequestEvent -= (PlayFabRequestEvent<CurrentGamesRequest>)obj48;
					}
				}
			}
			if (this.OnGetCurrentGamesResultEvent != null)
			{
				invocationList = this.OnGetCurrentGamesResultEvent.GetInvocationList();
				foreach (Delegate obj49 in invocationList)
				{
					if (obj49.Target == instance)
					{
						OnGetCurrentGamesResultEvent -= (PlayFabResultEvent<CurrentGamesResult>)obj49;
					}
				}
			}
			if (this.OnGetFriendLeaderboardRequestEvent != null)
			{
				invocationList = this.OnGetFriendLeaderboardRequestEvent.GetInvocationList();
				foreach (Delegate obj50 in invocationList)
				{
					if (obj50.Target == instance)
					{
						OnGetFriendLeaderboardRequestEvent -= (PlayFabRequestEvent<GetFriendLeaderboardRequest>)obj50;
					}
				}
			}
			if (this.OnGetFriendLeaderboardResultEvent != null)
			{
				invocationList = this.OnGetFriendLeaderboardResultEvent.GetInvocationList();
				foreach (Delegate obj51 in invocationList)
				{
					if (obj51.Target == instance)
					{
						OnGetFriendLeaderboardResultEvent -= (PlayFabResultEvent<GetLeaderboardResult>)obj51;
					}
				}
			}
			if (this.OnGetFriendLeaderboardAroundPlayerRequestEvent != null)
			{
				invocationList = this.OnGetFriendLeaderboardAroundPlayerRequestEvent.GetInvocationList();
				foreach (Delegate obj52 in invocationList)
				{
					if (obj52.Target == instance)
					{
						OnGetFriendLeaderboardAroundPlayerRequestEvent -= (PlayFabRequestEvent<GetFriendLeaderboardAroundPlayerRequest>)obj52;
					}
				}
			}
			if (this.OnGetFriendLeaderboardAroundPlayerResultEvent != null)
			{
				invocationList = this.OnGetFriendLeaderboardAroundPlayerResultEvent.GetInvocationList();
				foreach (Delegate obj53 in invocationList)
				{
					if (obj53.Target == instance)
					{
						OnGetFriendLeaderboardAroundPlayerResultEvent -= (PlayFabResultEvent<GetFriendLeaderboardAroundPlayerResult>)obj53;
					}
				}
			}
			if (this.OnGetFriendsListRequestEvent != null)
			{
				invocationList = this.OnGetFriendsListRequestEvent.GetInvocationList();
				foreach (Delegate obj54 in invocationList)
				{
					if (obj54.Target == instance)
					{
						OnGetFriendsListRequestEvent -= (PlayFabRequestEvent<GetFriendsListRequest>)obj54;
					}
				}
			}
			if (this.OnGetFriendsListResultEvent != null)
			{
				invocationList = this.OnGetFriendsListResultEvent.GetInvocationList();
				foreach (Delegate obj55 in invocationList)
				{
					if (obj55.Target == instance)
					{
						OnGetFriendsListResultEvent -= (PlayFabResultEvent<GetFriendsListResult>)obj55;
					}
				}
			}
			if (this.OnGetGameServerRegionsRequestEvent != null)
			{
				invocationList = this.OnGetGameServerRegionsRequestEvent.GetInvocationList();
				foreach (Delegate obj56 in invocationList)
				{
					if (obj56.Target == instance)
					{
						OnGetGameServerRegionsRequestEvent -= (PlayFabRequestEvent<GameServerRegionsRequest>)obj56;
					}
				}
			}
			if (this.OnGetGameServerRegionsResultEvent != null)
			{
				invocationList = this.OnGetGameServerRegionsResultEvent.GetInvocationList();
				foreach (Delegate obj57 in invocationList)
				{
					if (obj57.Target == instance)
					{
						OnGetGameServerRegionsResultEvent -= (PlayFabResultEvent<GameServerRegionsResult>)obj57;
					}
				}
			}
			if (this.OnGetLeaderboardRequestEvent != null)
			{
				invocationList = this.OnGetLeaderboardRequestEvent.GetInvocationList();
				foreach (Delegate obj58 in invocationList)
				{
					if (obj58.Target == instance)
					{
						OnGetLeaderboardRequestEvent -= (PlayFabRequestEvent<GetLeaderboardRequest>)obj58;
					}
				}
			}
			if (this.OnGetLeaderboardResultEvent != null)
			{
				invocationList = this.OnGetLeaderboardResultEvent.GetInvocationList();
				foreach (Delegate obj59 in invocationList)
				{
					if (obj59.Target == instance)
					{
						OnGetLeaderboardResultEvent -= (PlayFabResultEvent<GetLeaderboardResult>)obj59;
					}
				}
			}
			if (this.OnGetLeaderboardAroundCharacterRequestEvent != null)
			{
				invocationList = this.OnGetLeaderboardAroundCharacterRequestEvent.GetInvocationList();
				foreach (Delegate obj60 in invocationList)
				{
					if (obj60.Target == instance)
					{
						OnGetLeaderboardAroundCharacterRequestEvent -= (PlayFabRequestEvent<GetLeaderboardAroundCharacterRequest>)obj60;
					}
				}
			}
			if (this.OnGetLeaderboardAroundCharacterResultEvent != null)
			{
				invocationList = this.OnGetLeaderboardAroundCharacterResultEvent.GetInvocationList();
				foreach (Delegate obj61 in invocationList)
				{
					if (obj61.Target == instance)
					{
						OnGetLeaderboardAroundCharacterResultEvent -= (PlayFabResultEvent<GetLeaderboardAroundCharacterResult>)obj61;
					}
				}
			}
			if (this.OnGetLeaderboardAroundPlayerRequestEvent != null)
			{
				invocationList = this.OnGetLeaderboardAroundPlayerRequestEvent.GetInvocationList();
				foreach (Delegate obj62 in invocationList)
				{
					if (obj62.Target == instance)
					{
						OnGetLeaderboardAroundPlayerRequestEvent -= (PlayFabRequestEvent<GetLeaderboardAroundPlayerRequest>)obj62;
					}
				}
			}
			if (this.OnGetLeaderboardAroundPlayerResultEvent != null)
			{
				invocationList = this.OnGetLeaderboardAroundPlayerResultEvent.GetInvocationList();
				foreach (Delegate obj63 in invocationList)
				{
					if (obj63.Target == instance)
					{
						OnGetLeaderboardAroundPlayerResultEvent -= (PlayFabResultEvent<GetLeaderboardAroundPlayerResult>)obj63;
					}
				}
			}
			if (this.OnGetLeaderboardForUserCharactersRequestEvent != null)
			{
				invocationList = this.OnGetLeaderboardForUserCharactersRequestEvent.GetInvocationList();
				foreach (Delegate obj64 in invocationList)
				{
					if (obj64.Target == instance)
					{
						OnGetLeaderboardForUserCharactersRequestEvent -= (PlayFabRequestEvent<GetLeaderboardForUsersCharactersRequest>)obj64;
					}
				}
			}
			if (this.OnGetLeaderboardForUserCharactersResultEvent != null)
			{
				invocationList = this.OnGetLeaderboardForUserCharactersResultEvent.GetInvocationList();
				foreach (Delegate obj65 in invocationList)
				{
					if (obj65.Target == instance)
					{
						OnGetLeaderboardForUserCharactersResultEvent -= (PlayFabResultEvent<GetLeaderboardForUsersCharactersResult>)obj65;
					}
				}
			}
			if (this.OnGetPaymentTokenRequestEvent != null)
			{
				invocationList = this.OnGetPaymentTokenRequestEvent.GetInvocationList();
				foreach (Delegate obj66 in invocationList)
				{
					if (obj66.Target == instance)
					{
						OnGetPaymentTokenRequestEvent -= (PlayFabRequestEvent<GetPaymentTokenRequest>)obj66;
					}
				}
			}
			if (this.OnGetPaymentTokenResultEvent != null)
			{
				invocationList = this.OnGetPaymentTokenResultEvent.GetInvocationList();
				foreach (Delegate obj67 in invocationList)
				{
					if (obj67.Target == instance)
					{
						OnGetPaymentTokenResultEvent -= (PlayFabResultEvent<GetPaymentTokenResult>)obj67;
					}
				}
			}
			if (this.OnGetPhotonAuthenticationTokenRequestEvent != null)
			{
				invocationList = this.OnGetPhotonAuthenticationTokenRequestEvent.GetInvocationList();
				foreach (Delegate obj68 in invocationList)
				{
					if (obj68.Target == instance)
					{
						OnGetPhotonAuthenticationTokenRequestEvent -= (PlayFabRequestEvent<GetPhotonAuthenticationTokenRequest>)obj68;
					}
				}
			}
			if (this.OnGetPhotonAuthenticationTokenResultEvent != null)
			{
				invocationList = this.OnGetPhotonAuthenticationTokenResultEvent.GetInvocationList();
				foreach (Delegate obj69 in invocationList)
				{
					if (obj69.Target == instance)
					{
						OnGetPhotonAuthenticationTokenResultEvent -= (PlayFabResultEvent<GetPhotonAuthenticationTokenResult>)obj69;
					}
				}
			}
			if (this.OnGetPlayerCombinedInfoRequestEvent != null)
			{
				invocationList = this.OnGetPlayerCombinedInfoRequestEvent.GetInvocationList();
				foreach (Delegate obj70 in invocationList)
				{
					if (obj70.Target == instance)
					{
						OnGetPlayerCombinedInfoRequestEvent -= (PlayFabRequestEvent<GetPlayerCombinedInfoRequest>)obj70;
					}
				}
			}
			if (this.OnGetPlayerCombinedInfoResultEvent != null)
			{
				invocationList = this.OnGetPlayerCombinedInfoResultEvent.GetInvocationList();
				foreach (Delegate obj71 in invocationList)
				{
					if (obj71.Target == instance)
					{
						OnGetPlayerCombinedInfoResultEvent -= (PlayFabResultEvent<GetPlayerCombinedInfoResult>)obj71;
					}
				}
			}
			if (this.OnGetPlayerProfileRequestEvent != null)
			{
				invocationList = this.OnGetPlayerProfileRequestEvent.GetInvocationList();
				foreach (Delegate obj72 in invocationList)
				{
					if (obj72.Target == instance)
					{
						OnGetPlayerProfileRequestEvent -= (PlayFabRequestEvent<GetPlayerProfileRequest>)obj72;
					}
				}
			}
			if (this.OnGetPlayerProfileResultEvent != null)
			{
				invocationList = this.OnGetPlayerProfileResultEvent.GetInvocationList();
				foreach (Delegate obj73 in invocationList)
				{
					if (obj73.Target == instance)
					{
						OnGetPlayerProfileResultEvent -= (PlayFabResultEvent<GetPlayerProfileResult>)obj73;
					}
				}
			}
			if (this.OnGetPlayerSegmentsRequestEvent != null)
			{
				invocationList = this.OnGetPlayerSegmentsRequestEvent.GetInvocationList();
				foreach (Delegate obj74 in invocationList)
				{
					if (obj74.Target == instance)
					{
						OnGetPlayerSegmentsRequestEvent -= (PlayFabRequestEvent<GetPlayerSegmentsRequest>)obj74;
					}
				}
			}
			if (this.OnGetPlayerSegmentsResultEvent != null)
			{
				invocationList = this.OnGetPlayerSegmentsResultEvent.GetInvocationList();
				foreach (Delegate obj75 in invocationList)
				{
					if (obj75.Target == instance)
					{
						OnGetPlayerSegmentsResultEvent -= (PlayFabResultEvent<GetPlayerSegmentsResult>)obj75;
					}
				}
			}
			if (this.OnGetPlayerStatisticsRequestEvent != null)
			{
				invocationList = this.OnGetPlayerStatisticsRequestEvent.GetInvocationList();
				foreach (Delegate obj76 in invocationList)
				{
					if (obj76.Target == instance)
					{
						OnGetPlayerStatisticsRequestEvent -= (PlayFabRequestEvent<GetPlayerStatisticsRequest>)obj76;
					}
				}
			}
			if (this.OnGetPlayerStatisticsResultEvent != null)
			{
				invocationList = this.OnGetPlayerStatisticsResultEvent.GetInvocationList();
				foreach (Delegate obj77 in invocationList)
				{
					if (obj77.Target == instance)
					{
						OnGetPlayerStatisticsResultEvent -= (PlayFabResultEvent<GetPlayerStatisticsResult>)obj77;
					}
				}
			}
			if (this.OnGetPlayerStatisticVersionsRequestEvent != null)
			{
				invocationList = this.OnGetPlayerStatisticVersionsRequestEvent.GetInvocationList();
				foreach (Delegate obj78 in invocationList)
				{
					if (obj78.Target == instance)
					{
						OnGetPlayerStatisticVersionsRequestEvent -= (PlayFabRequestEvent<GetPlayerStatisticVersionsRequest>)obj78;
					}
				}
			}
			if (this.OnGetPlayerStatisticVersionsResultEvent != null)
			{
				invocationList = this.OnGetPlayerStatisticVersionsResultEvent.GetInvocationList();
				foreach (Delegate obj79 in invocationList)
				{
					if (obj79.Target == instance)
					{
						OnGetPlayerStatisticVersionsResultEvent -= (PlayFabResultEvent<GetPlayerStatisticVersionsResult>)obj79;
					}
				}
			}
			if (this.OnGetPlayerTagsRequestEvent != null)
			{
				invocationList = this.OnGetPlayerTagsRequestEvent.GetInvocationList();
				foreach (Delegate obj80 in invocationList)
				{
					if (obj80.Target == instance)
					{
						OnGetPlayerTagsRequestEvent -= (PlayFabRequestEvent<GetPlayerTagsRequest>)obj80;
					}
				}
			}
			if (this.OnGetPlayerTagsResultEvent != null)
			{
				invocationList = this.OnGetPlayerTagsResultEvent.GetInvocationList();
				foreach (Delegate obj81 in invocationList)
				{
					if (obj81.Target == instance)
					{
						OnGetPlayerTagsResultEvent -= (PlayFabResultEvent<GetPlayerTagsResult>)obj81;
					}
				}
			}
			if (this.OnGetPlayerTradesRequestEvent != null)
			{
				invocationList = this.OnGetPlayerTradesRequestEvent.GetInvocationList();
				foreach (Delegate obj82 in invocationList)
				{
					if (obj82.Target == instance)
					{
						OnGetPlayerTradesRequestEvent -= (PlayFabRequestEvent<GetPlayerTradesRequest>)obj82;
					}
				}
			}
			if (this.OnGetPlayerTradesResultEvent != null)
			{
				invocationList = this.OnGetPlayerTradesResultEvent.GetInvocationList();
				foreach (Delegate obj83 in invocationList)
				{
					if (obj83.Target == instance)
					{
						OnGetPlayerTradesResultEvent -= (PlayFabResultEvent<GetPlayerTradesResponse>)obj83;
					}
				}
			}
			if (this.OnGetPlayFabIDsFromFacebookIDsRequestEvent != null)
			{
				invocationList = this.OnGetPlayFabIDsFromFacebookIDsRequestEvent.GetInvocationList();
				foreach (Delegate obj84 in invocationList)
				{
					if (obj84.Target == instance)
					{
						OnGetPlayFabIDsFromFacebookIDsRequestEvent -= (PlayFabRequestEvent<GetPlayFabIDsFromFacebookIDsRequest>)obj84;
					}
				}
			}
			if (this.OnGetPlayFabIDsFromFacebookIDsResultEvent != null)
			{
				invocationList = this.OnGetPlayFabIDsFromFacebookIDsResultEvent.GetInvocationList();
				foreach (Delegate obj85 in invocationList)
				{
					if (obj85.Target == instance)
					{
						OnGetPlayFabIDsFromFacebookIDsResultEvent -= (PlayFabResultEvent<GetPlayFabIDsFromFacebookIDsResult>)obj85;
					}
				}
			}
			if (this.OnGetPlayFabIDsFromGameCenterIDsRequestEvent != null)
			{
				invocationList = this.OnGetPlayFabIDsFromGameCenterIDsRequestEvent.GetInvocationList();
				foreach (Delegate obj86 in invocationList)
				{
					if (obj86.Target == instance)
					{
						OnGetPlayFabIDsFromGameCenterIDsRequestEvent -= (PlayFabRequestEvent<GetPlayFabIDsFromGameCenterIDsRequest>)obj86;
					}
				}
			}
			if (this.OnGetPlayFabIDsFromGameCenterIDsResultEvent != null)
			{
				invocationList = this.OnGetPlayFabIDsFromGameCenterIDsResultEvent.GetInvocationList();
				foreach (Delegate obj87 in invocationList)
				{
					if (obj87.Target == instance)
					{
						OnGetPlayFabIDsFromGameCenterIDsResultEvent -= (PlayFabResultEvent<GetPlayFabIDsFromGameCenterIDsResult>)obj87;
					}
				}
			}
			if (this.OnGetPlayFabIDsFromGenericIDsRequestEvent != null)
			{
				invocationList = this.OnGetPlayFabIDsFromGenericIDsRequestEvent.GetInvocationList();
				foreach (Delegate obj88 in invocationList)
				{
					if (obj88.Target == instance)
					{
						OnGetPlayFabIDsFromGenericIDsRequestEvent -= (PlayFabRequestEvent<GetPlayFabIDsFromGenericIDsRequest>)obj88;
					}
				}
			}
			if (this.OnGetPlayFabIDsFromGenericIDsResultEvent != null)
			{
				invocationList = this.OnGetPlayFabIDsFromGenericIDsResultEvent.GetInvocationList();
				foreach (Delegate obj89 in invocationList)
				{
					if (obj89.Target == instance)
					{
						OnGetPlayFabIDsFromGenericIDsResultEvent -= (PlayFabResultEvent<GetPlayFabIDsFromGenericIDsResult>)obj89;
					}
				}
			}
			if (this.OnGetPlayFabIDsFromGoogleIDsRequestEvent != null)
			{
				invocationList = this.OnGetPlayFabIDsFromGoogleIDsRequestEvent.GetInvocationList();
				foreach (Delegate obj90 in invocationList)
				{
					if (obj90.Target == instance)
					{
						OnGetPlayFabIDsFromGoogleIDsRequestEvent -= (PlayFabRequestEvent<GetPlayFabIDsFromGoogleIDsRequest>)obj90;
					}
				}
			}
			if (this.OnGetPlayFabIDsFromGoogleIDsResultEvent != null)
			{
				invocationList = this.OnGetPlayFabIDsFromGoogleIDsResultEvent.GetInvocationList();
				foreach (Delegate obj91 in invocationList)
				{
					if (obj91.Target == instance)
					{
						OnGetPlayFabIDsFromGoogleIDsResultEvent -= (PlayFabResultEvent<GetPlayFabIDsFromGoogleIDsResult>)obj91;
					}
				}
			}
			if (this.OnGetPlayFabIDsFromKongregateIDsRequestEvent != null)
			{
				invocationList = this.OnGetPlayFabIDsFromKongregateIDsRequestEvent.GetInvocationList();
				foreach (Delegate obj92 in invocationList)
				{
					if (obj92.Target == instance)
					{
						OnGetPlayFabIDsFromKongregateIDsRequestEvent -= (PlayFabRequestEvent<GetPlayFabIDsFromKongregateIDsRequest>)obj92;
					}
				}
			}
			if (this.OnGetPlayFabIDsFromKongregateIDsResultEvent != null)
			{
				invocationList = this.OnGetPlayFabIDsFromKongregateIDsResultEvent.GetInvocationList();
				foreach (Delegate obj93 in invocationList)
				{
					if (obj93.Target == instance)
					{
						OnGetPlayFabIDsFromKongregateIDsResultEvent -= (PlayFabResultEvent<GetPlayFabIDsFromKongregateIDsResult>)obj93;
					}
				}
			}
			if (this.OnGetPlayFabIDsFromSteamIDsRequestEvent != null)
			{
				invocationList = this.OnGetPlayFabIDsFromSteamIDsRequestEvent.GetInvocationList();
				foreach (Delegate obj94 in invocationList)
				{
					if (obj94.Target == instance)
					{
						OnGetPlayFabIDsFromSteamIDsRequestEvent -= (PlayFabRequestEvent<GetPlayFabIDsFromSteamIDsRequest>)obj94;
					}
				}
			}
			if (this.OnGetPlayFabIDsFromSteamIDsResultEvent != null)
			{
				invocationList = this.OnGetPlayFabIDsFromSteamIDsResultEvent.GetInvocationList();
				foreach (Delegate obj95 in invocationList)
				{
					if (obj95.Target == instance)
					{
						OnGetPlayFabIDsFromSteamIDsResultEvent -= (PlayFabResultEvent<GetPlayFabIDsFromSteamIDsResult>)obj95;
					}
				}
			}
			if (this.OnGetPlayFabIDsFromTwitchIDsRequestEvent != null)
			{
				invocationList = this.OnGetPlayFabIDsFromTwitchIDsRequestEvent.GetInvocationList();
				foreach (Delegate obj96 in invocationList)
				{
					if (obj96.Target == instance)
					{
						OnGetPlayFabIDsFromTwitchIDsRequestEvent -= (PlayFabRequestEvent<GetPlayFabIDsFromTwitchIDsRequest>)obj96;
					}
				}
			}
			if (this.OnGetPlayFabIDsFromTwitchIDsResultEvent != null)
			{
				invocationList = this.OnGetPlayFabIDsFromTwitchIDsResultEvent.GetInvocationList();
				foreach (Delegate obj97 in invocationList)
				{
					if (obj97.Target == instance)
					{
						OnGetPlayFabIDsFromTwitchIDsResultEvent -= (PlayFabResultEvent<GetPlayFabIDsFromTwitchIDsResult>)obj97;
					}
				}
			}
			if (this.OnGetPublisherDataRequestEvent != null)
			{
				invocationList = this.OnGetPublisherDataRequestEvent.GetInvocationList();
				foreach (Delegate obj98 in invocationList)
				{
					if (obj98.Target == instance)
					{
						OnGetPublisherDataRequestEvent -= (PlayFabRequestEvent<GetPublisherDataRequest>)obj98;
					}
				}
			}
			if (this.OnGetPublisherDataResultEvent != null)
			{
				invocationList = this.OnGetPublisherDataResultEvent.GetInvocationList();
				foreach (Delegate obj99 in invocationList)
				{
					if (obj99.Target == instance)
					{
						OnGetPublisherDataResultEvent -= (PlayFabResultEvent<GetPublisherDataResult>)obj99;
					}
				}
			}
			if (this.OnGetPurchaseRequestEvent != null)
			{
				invocationList = this.OnGetPurchaseRequestEvent.GetInvocationList();
				foreach (Delegate obj100 in invocationList)
				{
					if (obj100.Target == instance)
					{
						OnGetPurchaseRequestEvent -= (PlayFabRequestEvent<GetPurchaseRequest>)obj100;
					}
				}
			}
			if (this.OnGetPurchaseResultEvent != null)
			{
				invocationList = this.OnGetPurchaseResultEvent.GetInvocationList();
				foreach (Delegate obj101 in invocationList)
				{
					if (obj101.Target == instance)
					{
						OnGetPurchaseResultEvent -= (PlayFabResultEvent<GetPurchaseResult>)obj101;
					}
				}
			}
			if (this.OnGetSharedGroupDataRequestEvent != null)
			{
				invocationList = this.OnGetSharedGroupDataRequestEvent.GetInvocationList();
				foreach (Delegate obj102 in invocationList)
				{
					if (obj102.Target == instance)
					{
						OnGetSharedGroupDataRequestEvent -= (PlayFabRequestEvent<GetSharedGroupDataRequest>)obj102;
					}
				}
			}
			if (this.OnGetSharedGroupDataResultEvent != null)
			{
				invocationList = this.OnGetSharedGroupDataResultEvent.GetInvocationList();
				foreach (Delegate obj103 in invocationList)
				{
					if (obj103.Target == instance)
					{
						OnGetSharedGroupDataResultEvent -= (PlayFabResultEvent<GetSharedGroupDataResult>)obj103;
					}
				}
			}
			if (this.OnGetStoreItemsRequestEvent != null)
			{
				invocationList = this.OnGetStoreItemsRequestEvent.GetInvocationList();
				foreach (Delegate obj104 in invocationList)
				{
					if (obj104.Target == instance)
					{
						OnGetStoreItemsRequestEvent -= (PlayFabRequestEvent<GetStoreItemsRequest>)obj104;
					}
				}
			}
			if (this.OnGetStoreItemsResultEvent != null)
			{
				invocationList = this.OnGetStoreItemsResultEvent.GetInvocationList();
				foreach (Delegate obj105 in invocationList)
				{
					if (obj105.Target == instance)
					{
						OnGetStoreItemsResultEvent -= (PlayFabResultEvent<GetStoreItemsResult>)obj105;
					}
				}
			}
			if (this.OnGetTimeRequestEvent != null)
			{
				invocationList = this.OnGetTimeRequestEvent.GetInvocationList();
				foreach (Delegate obj106 in invocationList)
				{
					if (obj106.Target == instance)
					{
						OnGetTimeRequestEvent -= (PlayFabRequestEvent<GetTimeRequest>)obj106;
					}
				}
			}
			if (this.OnGetTimeResultEvent != null)
			{
				invocationList = this.OnGetTimeResultEvent.GetInvocationList();
				foreach (Delegate obj107 in invocationList)
				{
					if (obj107.Target == instance)
					{
						OnGetTimeResultEvent -= (PlayFabResultEvent<GetTimeResult>)obj107;
					}
				}
			}
			if (this.OnGetTitleDataRequestEvent != null)
			{
				invocationList = this.OnGetTitleDataRequestEvent.GetInvocationList();
				foreach (Delegate obj108 in invocationList)
				{
					if (obj108.Target == instance)
					{
						OnGetTitleDataRequestEvent -= (PlayFabRequestEvent<GetTitleDataRequest>)obj108;
					}
				}
			}
			if (this.OnGetTitleDataResultEvent != null)
			{
				invocationList = this.OnGetTitleDataResultEvent.GetInvocationList();
				foreach (Delegate obj109 in invocationList)
				{
					if (obj109.Target == instance)
					{
						OnGetTitleDataResultEvent -= (PlayFabResultEvent<GetTitleDataResult>)obj109;
					}
				}
			}
			if (this.OnGetTitleNewsRequestEvent != null)
			{
				invocationList = this.OnGetTitleNewsRequestEvent.GetInvocationList();
				foreach (Delegate obj110 in invocationList)
				{
					if (obj110.Target == instance)
					{
						OnGetTitleNewsRequestEvent -= (PlayFabRequestEvent<GetTitleNewsRequest>)obj110;
					}
				}
			}
			if (this.OnGetTitleNewsResultEvent != null)
			{
				invocationList = this.OnGetTitleNewsResultEvent.GetInvocationList();
				foreach (Delegate obj111 in invocationList)
				{
					if (obj111.Target == instance)
					{
						OnGetTitleNewsResultEvent -= (PlayFabResultEvent<GetTitleNewsResult>)obj111;
					}
				}
			}
			if (this.OnGetTitlePublicKeyRequestEvent != null)
			{
				invocationList = this.OnGetTitlePublicKeyRequestEvent.GetInvocationList();
				foreach (Delegate obj112 in invocationList)
				{
					if (obj112.Target == instance)
					{
						OnGetTitlePublicKeyRequestEvent -= (PlayFabRequestEvent<GetTitlePublicKeyRequest>)obj112;
					}
				}
			}
			if (this.OnGetTitlePublicKeyResultEvent != null)
			{
				invocationList = this.OnGetTitlePublicKeyResultEvent.GetInvocationList();
				foreach (Delegate obj113 in invocationList)
				{
					if (obj113.Target == instance)
					{
						OnGetTitlePublicKeyResultEvent -= (PlayFabResultEvent<GetTitlePublicKeyResult>)obj113;
					}
				}
			}
			if (this.OnGetTradeStatusRequestEvent != null)
			{
				invocationList = this.OnGetTradeStatusRequestEvent.GetInvocationList();
				foreach (Delegate obj114 in invocationList)
				{
					if (obj114.Target == instance)
					{
						OnGetTradeStatusRequestEvent -= (PlayFabRequestEvent<GetTradeStatusRequest>)obj114;
					}
				}
			}
			if (this.OnGetTradeStatusResultEvent != null)
			{
				invocationList = this.OnGetTradeStatusResultEvent.GetInvocationList();
				foreach (Delegate obj115 in invocationList)
				{
					if (obj115.Target == instance)
					{
						OnGetTradeStatusResultEvent -= (PlayFabResultEvent<GetTradeStatusResponse>)obj115;
					}
				}
			}
			if (this.OnGetUserDataRequestEvent != null)
			{
				invocationList = this.OnGetUserDataRequestEvent.GetInvocationList();
				foreach (Delegate obj116 in invocationList)
				{
					if (obj116.Target == instance)
					{
						OnGetUserDataRequestEvent -= (PlayFabRequestEvent<GetUserDataRequest>)obj116;
					}
				}
			}
			if (this.OnGetUserDataResultEvent != null)
			{
				invocationList = this.OnGetUserDataResultEvent.GetInvocationList();
				foreach (Delegate obj117 in invocationList)
				{
					if (obj117.Target == instance)
					{
						OnGetUserDataResultEvent -= (PlayFabResultEvent<GetUserDataResult>)obj117;
					}
				}
			}
			if (this.OnGetUserInventoryRequestEvent != null)
			{
				invocationList = this.OnGetUserInventoryRequestEvent.GetInvocationList();
				foreach (Delegate obj118 in invocationList)
				{
					if (obj118.Target == instance)
					{
						OnGetUserInventoryRequestEvent -= (PlayFabRequestEvent<GetUserInventoryRequest>)obj118;
					}
				}
			}
			if (this.OnGetUserInventoryResultEvent != null)
			{
				invocationList = this.OnGetUserInventoryResultEvent.GetInvocationList();
				foreach (Delegate obj119 in invocationList)
				{
					if (obj119.Target == instance)
					{
						OnGetUserInventoryResultEvent -= (PlayFabResultEvent<GetUserInventoryResult>)obj119;
					}
				}
			}
			if (this.OnGetUserPublisherDataRequestEvent != null)
			{
				invocationList = this.OnGetUserPublisherDataRequestEvent.GetInvocationList();
				foreach (Delegate obj120 in invocationList)
				{
					if (obj120.Target == instance)
					{
						OnGetUserPublisherDataRequestEvent -= (PlayFabRequestEvent<GetUserDataRequest>)obj120;
					}
				}
			}
			if (this.OnGetUserPublisherDataResultEvent != null)
			{
				invocationList = this.OnGetUserPublisherDataResultEvent.GetInvocationList();
				foreach (Delegate obj121 in invocationList)
				{
					if (obj121.Target == instance)
					{
						OnGetUserPublisherDataResultEvent -= (PlayFabResultEvent<GetUserDataResult>)obj121;
					}
				}
			}
			if (this.OnGetUserPublisherReadOnlyDataRequestEvent != null)
			{
				invocationList = this.OnGetUserPublisherReadOnlyDataRequestEvent.GetInvocationList();
				foreach (Delegate obj122 in invocationList)
				{
					if (obj122.Target == instance)
					{
						OnGetUserPublisherReadOnlyDataRequestEvent -= (PlayFabRequestEvent<GetUserDataRequest>)obj122;
					}
				}
			}
			if (this.OnGetUserPublisherReadOnlyDataResultEvent != null)
			{
				invocationList = this.OnGetUserPublisherReadOnlyDataResultEvent.GetInvocationList();
				foreach (Delegate obj123 in invocationList)
				{
					if (obj123.Target == instance)
					{
						OnGetUserPublisherReadOnlyDataResultEvent -= (PlayFabResultEvent<GetUserDataResult>)obj123;
					}
				}
			}
			if (this.OnGetUserReadOnlyDataRequestEvent != null)
			{
				invocationList = this.OnGetUserReadOnlyDataRequestEvent.GetInvocationList();
				foreach (Delegate obj124 in invocationList)
				{
					if (obj124.Target == instance)
					{
						OnGetUserReadOnlyDataRequestEvent -= (PlayFabRequestEvent<GetUserDataRequest>)obj124;
					}
				}
			}
			if (this.OnGetUserReadOnlyDataResultEvent != null)
			{
				invocationList = this.OnGetUserReadOnlyDataResultEvent.GetInvocationList();
				foreach (Delegate obj125 in invocationList)
				{
					if (obj125.Target == instance)
					{
						OnGetUserReadOnlyDataResultEvent -= (PlayFabResultEvent<GetUserDataResult>)obj125;
					}
				}
			}
			if (this.OnGetWindowsHelloChallengeRequestEvent != null)
			{
				invocationList = this.OnGetWindowsHelloChallengeRequestEvent.GetInvocationList();
				foreach (Delegate obj126 in invocationList)
				{
					if (obj126.Target == instance)
					{
						OnGetWindowsHelloChallengeRequestEvent -= (PlayFabRequestEvent<GetWindowsHelloChallengeRequest>)obj126;
					}
				}
			}
			if (this.OnGetWindowsHelloChallengeResultEvent != null)
			{
				invocationList = this.OnGetWindowsHelloChallengeResultEvent.GetInvocationList();
				foreach (Delegate obj127 in invocationList)
				{
					if (obj127.Target == instance)
					{
						OnGetWindowsHelloChallengeResultEvent -= (PlayFabResultEvent<GetWindowsHelloChallengeResponse>)obj127;
					}
				}
			}
			if (this.OnGrantCharacterToUserRequestEvent != null)
			{
				invocationList = this.OnGrantCharacterToUserRequestEvent.GetInvocationList();
				foreach (Delegate obj128 in invocationList)
				{
					if (obj128.Target == instance)
					{
						OnGrantCharacterToUserRequestEvent -= (PlayFabRequestEvent<GrantCharacterToUserRequest>)obj128;
					}
				}
			}
			if (this.OnGrantCharacterToUserResultEvent != null)
			{
				invocationList = this.OnGrantCharacterToUserResultEvent.GetInvocationList();
				foreach (Delegate obj129 in invocationList)
				{
					if (obj129.Target == instance)
					{
						OnGrantCharacterToUserResultEvent -= (PlayFabResultEvent<GrantCharacterToUserResult>)obj129;
					}
				}
			}
			if (this.OnLinkAndroidDeviceIDRequestEvent != null)
			{
				invocationList = this.OnLinkAndroidDeviceIDRequestEvent.GetInvocationList();
				foreach (Delegate obj130 in invocationList)
				{
					if (obj130.Target == instance)
					{
						OnLinkAndroidDeviceIDRequestEvent -= (PlayFabRequestEvent<LinkAndroidDeviceIDRequest>)obj130;
					}
				}
			}
			if (this.OnLinkAndroidDeviceIDResultEvent != null)
			{
				invocationList = this.OnLinkAndroidDeviceIDResultEvent.GetInvocationList();
				foreach (Delegate obj131 in invocationList)
				{
					if (obj131.Target == instance)
					{
						OnLinkAndroidDeviceIDResultEvent -= (PlayFabResultEvent<LinkAndroidDeviceIDResult>)obj131;
					}
				}
			}
			if (this.OnLinkCustomIDRequestEvent != null)
			{
				invocationList = this.OnLinkCustomIDRequestEvent.GetInvocationList();
				foreach (Delegate obj132 in invocationList)
				{
					if (obj132.Target == instance)
					{
						OnLinkCustomIDRequestEvent -= (PlayFabRequestEvent<LinkCustomIDRequest>)obj132;
					}
				}
			}
			if (this.OnLinkCustomIDResultEvent != null)
			{
				invocationList = this.OnLinkCustomIDResultEvent.GetInvocationList();
				foreach (Delegate obj133 in invocationList)
				{
					if (obj133.Target == instance)
					{
						OnLinkCustomIDResultEvent -= (PlayFabResultEvent<LinkCustomIDResult>)obj133;
					}
				}
			}
			if (this.OnLinkFacebookAccountRequestEvent != null)
			{
				invocationList = this.OnLinkFacebookAccountRequestEvent.GetInvocationList();
				foreach (Delegate obj134 in invocationList)
				{
					if (obj134.Target == instance)
					{
						OnLinkFacebookAccountRequestEvent -= (PlayFabRequestEvent<LinkFacebookAccountRequest>)obj134;
					}
				}
			}
			if (this.OnLinkFacebookAccountResultEvent != null)
			{
				invocationList = this.OnLinkFacebookAccountResultEvent.GetInvocationList();
				foreach (Delegate obj135 in invocationList)
				{
					if (obj135.Target == instance)
					{
						OnLinkFacebookAccountResultEvent -= (PlayFabResultEvent<LinkFacebookAccountResult>)obj135;
					}
				}
			}
			if (this.OnLinkGameCenterAccountRequestEvent != null)
			{
				invocationList = this.OnLinkGameCenterAccountRequestEvent.GetInvocationList();
				foreach (Delegate obj136 in invocationList)
				{
					if (obj136.Target == instance)
					{
						OnLinkGameCenterAccountRequestEvent -= (PlayFabRequestEvent<LinkGameCenterAccountRequest>)obj136;
					}
				}
			}
			if (this.OnLinkGameCenterAccountResultEvent != null)
			{
				invocationList = this.OnLinkGameCenterAccountResultEvent.GetInvocationList();
				foreach (Delegate obj137 in invocationList)
				{
					if (obj137.Target == instance)
					{
						OnLinkGameCenterAccountResultEvent -= (PlayFabResultEvent<LinkGameCenterAccountResult>)obj137;
					}
				}
			}
			if (this.OnLinkGoogleAccountRequestEvent != null)
			{
				invocationList = this.OnLinkGoogleAccountRequestEvent.GetInvocationList();
				foreach (Delegate obj138 in invocationList)
				{
					if (obj138.Target == instance)
					{
						OnLinkGoogleAccountRequestEvent -= (PlayFabRequestEvent<LinkGoogleAccountRequest>)obj138;
					}
				}
			}
			if (this.OnLinkGoogleAccountResultEvent != null)
			{
				invocationList = this.OnLinkGoogleAccountResultEvent.GetInvocationList();
				foreach (Delegate obj139 in invocationList)
				{
					if (obj139.Target == instance)
					{
						OnLinkGoogleAccountResultEvent -= (PlayFabResultEvent<LinkGoogleAccountResult>)obj139;
					}
				}
			}
			if (this.OnLinkIOSDeviceIDRequestEvent != null)
			{
				invocationList = this.OnLinkIOSDeviceIDRequestEvent.GetInvocationList();
				foreach (Delegate obj140 in invocationList)
				{
					if (obj140.Target == instance)
					{
						OnLinkIOSDeviceIDRequestEvent -= (PlayFabRequestEvent<LinkIOSDeviceIDRequest>)obj140;
					}
				}
			}
			if (this.OnLinkIOSDeviceIDResultEvent != null)
			{
				invocationList = this.OnLinkIOSDeviceIDResultEvent.GetInvocationList();
				foreach (Delegate obj141 in invocationList)
				{
					if (obj141.Target == instance)
					{
						OnLinkIOSDeviceIDResultEvent -= (PlayFabResultEvent<LinkIOSDeviceIDResult>)obj141;
					}
				}
			}
			if (this.OnLinkKongregateRequestEvent != null)
			{
				invocationList = this.OnLinkKongregateRequestEvent.GetInvocationList();
				foreach (Delegate obj142 in invocationList)
				{
					if (obj142.Target == instance)
					{
						OnLinkKongregateRequestEvent -= (PlayFabRequestEvent<LinkKongregateAccountRequest>)obj142;
					}
				}
			}
			if (this.OnLinkKongregateResultEvent != null)
			{
				invocationList = this.OnLinkKongregateResultEvent.GetInvocationList();
				foreach (Delegate obj143 in invocationList)
				{
					if (obj143.Target == instance)
					{
						OnLinkKongregateResultEvent -= (PlayFabResultEvent<LinkKongregateAccountResult>)obj143;
					}
				}
			}
			if (this.OnLinkSteamAccountRequestEvent != null)
			{
				invocationList = this.OnLinkSteamAccountRequestEvent.GetInvocationList();
				foreach (Delegate obj144 in invocationList)
				{
					if (obj144.Target == instance)
					{
						OnLinkSteamAccountRequestEvent -= (PlayFabRequestEvent<LinkSteamAccountRequest>)obj144;
					}
				}
			}
			if (this.OnLinkSteamAccountResultEvent != null)
			{
				invocationList = this.OnLinkSteamAccountResultEvent.GetInvocationList();
				foreach (Delegate obj145 in invocationList)
				{
					if (obj145.Target == instance)
					{
						OnLinkSteamAccountResultEvent -= (PlayFabResultEvent<LinkSteamAccountResult>)obj145;
					}
				}
			}
			if (this.OnLinkTwitchRequestEvent != null)
			{
				invocationList = this.OnLinkTwitchRequestEvent.GetInvocationList();
				foreach (Delegate obj146 in invocationList)
				{
					if (obj146.Target == instance)
					{
						OnLinkTwitchRequestEvent -= (PlayFabRequestEvent<LinkTwitchAccountRequest>)obj146;
					}
				}
			}
			if (this.OnLinkTwitchResultEvent != null)
			{
				invocationList = this.OnLinkTwitchResultEvent.GetInvocationList();
				foreach (Delegate obj147 in invocationList)
				{
					if (obj147.Target == instance)
					{
						OnLinkTwitchResultEvent -= (PlayFabResultEvent<LinkTwitchAccountResult>)obj147;
					}
				}
			}
			if (this.OnLinkWindowsHelloRequestEvent != null)
			{
				invocationList = this.OnLinkWindowsHelloRequestEvent.GetInvocationList();
				foreach (Delegate obj148 in invocationList)
				{
					if (obj148.Target == instance)
					{
						OnLinkWindowsHelloRequestEvent -= (PlayFabRequestEvent<LinkWindowsHelloAccountRequest>)obj148;
					}
				}
			}
			if (this.OnLinkWindowsHelloResultEvent != null)
			{
				invocationList = this.OnLinkWindowsHelloResultEvent.GetInvocationList();
				foreach (Delegate obj149 in invocationList)
				{
					if (obj149.Target == instance)
					{
						OnLinkWindowsHelloResultEvent -= (PlayFabResultEvent<LinkWindowsHelloAccountResponse>)obj149;
					}
				}
			}
			if (this.OnLoginWithAndroidDeviceIDRequestEvent != null)
			{
				invocationList = this.OnLoginWithAndroidDeviceIDRequestEvent.GetInvocationList();
				foreach (Delegate obj150 in invocationList)
				{
					if (obj150.Target == instance)
					{
						OnLoginWithAndroidDeviceIDRequestEvent -= (PlayFabRequestEvent<LoginWithAndroidDeviceIDRequest>)obj150;
					}
				}
			}
			if (this.OnLoginWithCustomIDRequestEvent != null)
			{
				invocationList = this.OnLoginWithCustomIDRequestEvent.GetInvocationList();
				foreach (Delegate obj151 in invocationList)
				{
					if (obj151.Target == instance)
					{
						OnLoginWithCustomIDRequestEvent -= (PlayFabRequestEvent<LoginWithCustomIDRequest>)obj151;
					}
				}
			}
			if (this.OnLoginWithEmailAddressRequestEvent != null)
			{
				invocationList = this.OnLoginWithEmailAddressRequestEvent.GetInvocationList();
				foreach (Delegate obj152 in invocationList)
				{
					if (obj152.Target == instance)
					{
						OnLoginWithEmailAddressRequestEvent -= (PlayFabRequestEvent<LoginWithEmailAddressRequest>)obj152;
					}
				}
			}
			if (this.OnLoginWithFacebookRequestEvent != null)
			{
				invocationList = this.OnLoginWithFacebookRequestEvent.GetInvocationList();
				foreach (Delegate obj153 in invocationList)
				{
					if (obj153.Target == instance)
					{
						OnLoginWithFacebookRequestEvent -= (PlayFabRequestEvent<LoginWithFacebookRequest>)obj153;
					}
				}
			}
			if (this.OnLoginWithGameCenterRequestEvent != null)
			{
				invocationList = this.OnLoginWithGameCenterRequestEvent.GetInvocationList();
				foreach (Delegate obj154 in invocationList)
				{
					if (obj154.Target == instance)
					{
						OnLoginWithGameCenterRequestEvent -= (PlayFabRequestEvent<LoginWithGameCenterRequest>)obj154;
					}
				}
			}
			if (this.OnLoginWithGoogleAccountRequestEvent != null)
			{
				invocationList = this.OnLoginWithGoogleAccountRequestEvent.GetInvocationList();
				foreach (Delegate obj155 in invocationList)
				{
					if (obj155.Target == instance)
					{
						OnLoginWithGoogleAccountRequestEvent -= (PlayFabRequestEvent<LoginWithGoogleAccountRequest>)obj155;
					}
				}
			}
			if (this.OnLoginWithIOSDeviceIDRequestEvent != null)
			{
				invocationList = this.OnLoginWithIOSDeviceIDRequestEvent.GetInvocationList();
				foreach (Delegate obj156 in invocationList)
				{
					if (obj156.Target == instance)
					{
						OnLoginWithIOSDeviceIDRequestEvent -= (PlayFabRequestEvent<LoginWithIOSDeviceIDRequest>)obj156;
					}
				}
			}
			if (this.OnLoginWithKongregateRequestEvent != null)
			{
				invocationList = this.OnLoginWithKongregateRequestEvent.GetInvocationList();
				foreach (Delegate obj157 in invocationList)
				{
					if (obj157.Target == instance)
					{
						OnLoginWithKongregateRequestEvent -= (PlayFabRequestEvent<LoginWithKongregateRequest>)obj157;
					}
				}
			}
			if (this.OnLoginWithPlayFabRequestEvent != null)
			{
				invocationList = this.OnLoginWithPlayFabRequestEvent.GetInvocationList();
				foreach (Delegate obj158 in invocationList)
				{
					if (obj158.Target == instance)
					{
						OnLoginWithPlayFabRequestEvent -= (PlayFabRequestEvent<LoginWithPlayFabRequest>)obj158;
					}
				}
			}
			if (this.OnLoginWithSteamRequestEvent != null)
			{
				invocationList = this.OnLoginWithSteamRequestEvent.GetInvocationList();
				foreach (Delegate obj159 in invocationList)
				{
					if (obj159.Target == instance)
					{
						OnLoginWithSteamRequestEvent -= (PlayFabRequestEvent<LoginWithSteamRequest>)obj159;
					}
				}
			}
			if (this.OnLoginWithTwitchRequestEvent != null)
			{
				invocationList = this.OnLoginWithTwitchRequestEvent.GetInvocationList();
				foreach (Delegate obj160 in invocationList)
				{
					if (obj160.Target == instance)
					{
						OnLoginWithTwitchRequestEvent -= (PlayFabRequestEvent<LoginWithTwitchRequest>)obj160;
					}
				}
			}
			if (this.OnLoginWithWindowsHelloRequestEvent != null)
			{
				invocationList = this.OnLoginWithWindowsHelloRequestEvent.GetInvocationList();
				foreach (Delegate obj161 in invocationList)
				{
					if (obj161.Target == instance)
					{
						OnLoginWithWindowsHelloRequestEvent -= (PlayFabRequestEvent<LoginWithWindowsHelloRequest>)obj161;
					}
				}
			}
			if (this.OnMatchmakeRequestEvent != null)
			{
				invocationList = this.OnMatchmakeRequestEvent.GetInvocationList();
				foreach (Delegate obj162 in invocationList)
				{
					if (obj162.Target == instance)
					{
						OnMatchmakeRequestEvent -= (PlayFabRequestEvent<MatchmakeRequest>)obj162;
					}
				}
			}
			if (this.OnMatchmakeResultEvent != null)
			{
				invocationList = this.OnMatchmakeResultEvent.GetInvocationList();
				foreach (Delegate obj163 in invocationList)
				{
					if (obj163.Target == instance)
					{
						OnMatchmakeResultEvent -= (PlayFabResultEvent<MatchmakeResult>)obj163;
					}
				}
			}
			if (this.OnOpenTradeRequestEvent != null)
			{
				invocationList = this.OnOpenTradeRequestEvent.GetInvocationList();
				foreach (Delegate obj164 in invocationList)
				{
					if (obj164.Target == instance)
					{
						OnOpenTradeRequestEvent -= (PlayFabRequestEvent<OpenTradeRequest>)obj164;
					}
				}
			}
			if (this.OnOpenTradeResultEvent != null)
			{
				invocationList = this.OnOpenTradeResultEvent.GetInvocationList();
				foreach (Delegate obj165 in invocationList)
				{
					if (obj165.Target == instance)
					{
						OnOpenTradeResultEvent -= (PlayFabResultEvent<OpenTradeResponse>)obj165;
					}
				}
			}
			if (this.OnPayForPurchaseRequestEvent != null)
			{
				invocationList = this.OnPayForPurchaseRequestEvent.GetInvocationList();
				foreach (Delegate obj166 in invocationList)
				{
					if (obj166.Target == instance)
					{
						OnPayForPurchaseRequestEvent -= (PlayFabRequestEvent<PayForPurchaseRequest>)obj166;
					}
				}
			}
			if (this.OnPayForPurchaseResultEvent != null)
			{
				invocationList = this.OnPayForPurchaseResultEvent.GetInvocationList();
				foreach (Delegate obj167 in invocationList)
				{
					if (obj167.Target == instance)
					{
						OnPayForPurchaseResultEvent -= (PlayFabResultEvent<PayForPurchaseResult>)obj167;
					}
				}
			}
			if (this.OnPurchaseItemRequestEvent != null)
			{
				invocationList = this.OnPurchaseItemRequestEvent.GetInvocationList();
				foreach (Delegate obj168 in invocationList)
				{
					if (obj168.Target == instance)
					{
						OnPurchaseItemRequestEvent -= (PlayFabRequestEvent<PurchaseItemRequest>)obj168;
					}
				}
			}
			if (this.OnPurchaseItemResultEvent != null)
			{
				invocationList = this.OnPurchaseItemResultEvent.GetInvocationList();
				foreach (Delegate obj169 in invocationList)
				{
					if (obj169.Target == instance)
					{
						OnPurchaseItemResultEvent -= (PlayFabResultEvent<PurchaseItemResult>)obj169;
					}
				}
			}
			if (this.OnRedeemCouponRequestEvent != null)
			{
				invocationList = this.OnRedeemCouponRequestEvent.GetInvocationList();
				foreach (Delegate obj170 in invocationList)
				{
					if (obj170.Target == instance)
					{
						OnRedeemCouponRequestEvent -= (PlayFabRequestEvent<RedeemCouponRequest>)obj170;
					}
				}
			}
			if (this.OnRedeemCouponResultEvent != null)
			{
				invocationList = this.OnRedeemCouponResultEvent.GetInvocationList();
				foreach (Delegate obj171 in invocationList)
				{
					if (obj171.Target == instance)
					{
						OnRedeemCouponResultEvent -= (PlayFabResultEvent<RedeemCouponResult>)obj171;
					}
				}
			}
			if (this.OnRegisterForIOSPushNotificationRequestEvent != null)
			{
				invocationList = this.OnRegisterForIOSPushNotificationRequestEvent.GetInvocationList();
				foreach (Delegate obj172 in invocationList)
				{
					if (obj172.Target == instance)
					{
						OnRegisterForIOSPushNotificationRequestEvent -= (PlayFabRequestEvent<RegisterForIOSPushNotificationRequest>)obj172;
					}
				}
			}
			if (this.OnRegisterForIOSPushNotificationResultEvent != null)
			{
				invocationList = this.OnRegisterForIOSPushNotificationResultEvent.GetInvocationList();
				foreach (Delegate obj173 in invocationList)
				{
					if (obj173.Target == instance)
					{
						OnRegisterForIOSPushNotificationResultEvent -= (PlayFabResultEvent<RegisterForIOSPushNotificationResult>)obj173;
					}
				}
			}
			if (this.OnRegisterPlayFabUserRequestEvent != null)
			{
				invocationList = this.OnRegisterPlayFabUserRequestEvent.GetInvocationList();
				foreach (Delegate obj174 in invocationList)
				{
					if (obj174.Target == instance)
					{
						OnRegisterPlayFabUserRequestEvent -= (PlayFabRequestEvent<RegisterPlayFabUserRequest>)obj174;
					}
				}
			}
			if (this.OnRegisterPlayFabUserResultEvent != null)
			{
				invocationList = this.OnRegisterPlayFabUserResultEvent.GetInvocationList();
				foreach (Delegate obj175 in invocationList)
				{
					if (obj175.Target == instance)
					{
						OnRegisterPlayFabUserResultEvent -= (PlayFabResultEvent<RegisterPlayFabUserResult>)obj175;
					}
				}
			}
			if (this.OnRegisterWithWindowsHelloRequestEvent != null)
			{
				invocationList = this.OnRegisterWithWindowsHelloRequestEvent.GetInvocationList();
				foreach (Delegate obj176 in invocationList)
				{
					if (obj176.Target == instance)
					{
						OnRegisterWithWindowsHelloRequestEvent -= (PlayFabRequestEvent<RegisterWithWindowsHelloRequest>)obj176;
					}
				}
			}
			if (this.OnRemoveContactEmailRequestEvent != null)
			{
				invocationList = this.OnRemoveContactEmailRequestEvent.GetInvocationList();
				foreach (Delegate obj177 in invocationList)
				{
					if (obj177.Target == instance)
					{
						OnRemoveContactEmailRequestEvent -= (PlayFabRequestEvent<RemoveContactEmailRequest>)obj177;
					}
				}
			}
			if (this.OnRemoveContactEmailResultEvent != null)
			{
				invocationList = this.OnRemoveContactEmailResultEvent.GetInvocationList();
				foreach (Delegate obj178 in invocationList)
				{
					if (obj178.Target == instance)
					{
						OnRemoveContactEmailResultEvent -= (PlayFabResultEvent<RemoveContactEmailResult>)obj178;
					}
				}
			}
			if (this.OnRemoveFriendRequestEvent != null)
			{
				invocationList = this.OnRemoveFriendRequestEvent.GetInvocationList();
				foreach (Delegate obj179 in invocationList)
				{
					if (obj179.Target == instance)
					{
						OnRemoveFriendRequestEvent -= (PlayFabRequestEvent<RemoveFriendRequest>)obj179;
					}
				}
			}
			if (this.OnRemoveFriendResultEvent != null)
			{
				invocationList = this.OnRemoveFriendResultEvent.GetInvocationList();
				foreach (Delegate obj180 in invocationList)
				{
					if (obj180.Target == instance)
					{
						OnRemoveFriendResultEvent -= (PlayFabResultEvent<RemoveFriendResult>)obj180;
					}
				}
			}
			if (this.OnRemoveGenericIDRequestEvent != null)
			{
				invocationList = this.OnRemoveGenericIDRequestEvent.GetInvocationList();
				foreach (Delegate obj181 in invocationList)
				{
					if (obj181.Target == instance)
					{
						OnRemoveGenericIDRequestEvent -= (PlayFabRequestEvent<RemoveGenericIDRequest>)obj181;
					}
				}
			}
			if (this.OnRemoveGenericIDResultEvent != null)
			{
				invocationList = this.OnRemoveGenericIDResultEvent.GetInvocationList();
				foreach (Delegate obj182 in invocationList)
				{
					if (obj182.Target == instance)
					{
						OnRemoveGenericIDResultEvent -= (PlayFabResultEvent<RemoveGenericIDResult>)obj182;
					}
				}
			}
			if (this.OnRemoveSharedGroupMembersRequestEvent != null)
			{
				invocationList = this.OnRemoveSharedGroupMembersRequestEvent.GetInvocationList();
				foreach (Delegate obj183 in invocationList)
				{
					if (obj183.Target == instance)
					{
						OnRemoveSharedGroupMembersRequestEvent -= (PlayFabRequestEvent<RemoveSharedGroupMembersRequest>)obj183;
					}
				}
			}
			if (this.OnRemoveSharedGroupMembersResultEvent != null)
			{
				invocationList = this.OnRemoveSharedGroupMembersResultEvent.GetInvocationList();
				foreach (Delegate obj184 in invocationList)
				{
					if (obj184.Target == instance)
					{
						OnRemoveSharedGroupMembersResultEvent -= (PlayFabResultEvent<RemoveSharedGroupMembersResult>)obj184;
					}
				}
			}
			if (this.OnReportDeviceInfoRequestEvent != null)
			{
				invocationList = this.OnReportDeviceInfoRequestEvent.GetInvocationList();
				foreach (Delegate obj185 in invocationList)
				{
					if (obj185.Target == instance)
					{
						OnReportDeviceInfoRequestEvent -= (PlayFabRequestEvent<DeviceInfoRequest>)obj185;
					}
				}
			}
			if (this.OnReportDeviceInfoResultEvent != null)
			{
				invocationList = this.OnReportDeviceInfoResultEvent.GetInvocationList();
				foreach (Delegate obj186 in invocationList)
				{
					if (obj186.Target == instance)
					{
						OnReportDeviceInfoResultEvent -= (PlayFabResultEvent<EmptyResult>)obj186;
					}
				}
			}
			if (this.OnReportPlayerRequestEvent != null)
			{
				invocationList = this.OnReportPlayerRequestEvent.GetInvocationList();
				foreach (Delegate obj187 in invocationList)
				{
					if (obj187.Target == instance)
					{
						OnReportPlayerRequestEvent -= (PlayFabRequestEvent<ReportPlayerClientRequest>)obj187;
					}
				}
			}
			if (this.OnReportPlayerResultEvent != null)
			{
				invocationList = this.OnReportPlayerResultEvent.GetInvocationList();
				foreach (Delegate obj188 in invocationList)
				{
					if (obj188.Target == instance)
					{
						OnReportPlayerResultEvent -= (PlayFabResultEvent<ReportPlayerClientResult>)obj188;
					}
				}
			}
			if (this.OnRestoreIOSPurchasesRequestEvent != null)
			{
				invocationList = this.OnRestoreIOSPurchasesRequestEvent.GetInvocationList();
				foreach (Delegate obj189 in invocationList)
				{
					if (obj189.Target == instance)
					{
						OnRestoreIOSPurchasesRequestEvent -= (PlayFabRequestEvent<RestoreIOSPurchasesRequest>)obj189;
					}
				}
			}
			if (this.OnRestoreIOSPurchasesResultEvent != null)
			{
				invocationList = this.OnRestoreIOSPurchasesResultEvent.GetInvocationList();
				foreach (Delegate obj190 in invocationList)
				{
					if (obj190.Target == instance)
					{
						OnRestoreIOSPurchasesResultEvent -= (PlayFabResultEvent<RestoreIOSPurchasesResult>)obj190;
					}
				}
			}
			if (this.OnSendAccountRecoveryEmailRequestEvent != null)
			{
				invocationList = this.OnSendAccountRecoveryEmailRequestEvent.GetInvocationList();
				foreach (Delegate obj191 in invocationList)
				{
					if (obj191.Target == instance)
					{
						OnSendAccountRecoveryEmailRequestEvent -= (PlayFabRequestEvent<SendAccountRecoveryEmailRequest>)obj191;
					}
				}
			}
			if (this.OnSendAccountRecoveryEmailResultEvent != null)
			{
				invocationList = this.OnSendAccountRecoveryEmailResultEvent.GetInvocationList();
				foreach (Delegate obj192 in invocationList)
				{
					if (obj192.Target == instance)
					{
						OnSendAccountRecoveryEmailResultEvent -= (PlayFabResultEvent<SendAccountRecoveryEmailResult>)obj192;
					}
				}
			}
			if (this.OnSetFriendTagsRequestEvent != null)
			{
				invocationList = this.OnSetFriendTagsRequestEvent.GetInvocationList();
				foreach (Delegate obj193 in invocationList)
				{
					if (obj193.Target == instance)
					{
						OnSetFriendTagsRequestEvent -= (PlayFabRequestEvent<SetFriendTagsRequest>)obj193;
					}
				}
			}
			if (this.OnSetFriendTagsResultEvent != null)
			{
				invocationList = this.OnSetFriendTagsResultEvent.GetInvocationList();
				foreach (Delegate obj194 in invocationList)
				{
					if (obj194.Target == instance)
					{
						OnSetFriendTagsResultEvent -= (PlayFabResultEvent<SetFriendTagsResult>)obj194;
					}
				}
			}
			if (this.OnSetPlayerSecretRequestEvent != null)
			{
				invocationList = this.OnSetPlayerSecretRequestEvent.GetInvocationList();
				foreach (Delegate obj195 in invocationList)
				{
					if (obj195.Target == instance)
					{
						OnSetPlayerSecretRequestEvent -= (PlayFabRequestEvent<SetPlayerSecretRequest>)obj195;
					}
				}
			}
			if (this.OnSetPlayerSecretResultEvent != null)
			{
				invocationList = this.OnSetPlayerSecretResultEvent.GetInvocationList();
				foreach (Delegate obj196 in invocationList)
				{
					if (obj196.Target == instance)
					{
						OnSetPlayerSecretResultEvent -= (PlayFabResultEvent<SetPlayerSecretResult>)obj196;
					}
				}
			}
			if (this.OnStartGameRequestEvent != null)
			{
				invocationList = this.OnStartGameRequestEvent.GetInvocationList();
				foreach (Delegate obj197 in invocationList)
				{
					if (obj197.Target == instance)
					{
						OnStartGameRequestEvent -= (PlayFabRequestEvent<StartGameRequest>)obj197;
					}
				}
			}
			if (this.OnStartGameResultEvent != null)
			{
				invocationList = this.OnStartGameResultEvent.GetInvocationList();
				foreach (Delegate obj198 in invocationList)
				{
					if (obj198.Target == instance)
					{
						OnStartGameResultEvent -= (PlayFabResultEvent<StartGameResult>)obj198;
					}
				}
			}
			if (this.OnStartPurchaseRequestEvent != null)
			{
				invocationList = this.OnStartPurchaseRequestEvent.GetInvocationList();
				foreach (Delegate obj199 in invocationList)
				{
					if (obj199.Target == instance)
					{
						OnStartPurchaseRequestEvent -= (PlayFabRequestEvent<StartPurchaseRequest>)obj199;
					}
				}
			}
			if (this.OnStartPurchaseResultEvent != null)
			{
				invocationList = this.OnStartPurchaseResultEvent.GetInvocationList();
				foreach (Delegate obj200 in invocationList)
				{
					if (obj200.Target == instance)
					{
						OnStartPurchaseResultEvent -= (PlayFabResultEvent<StartPurchaseResult>)obj200;
					}
				}
			}
			if (this.OnSubtractUserVirtualCurrencyRequestEvent != null)
			{
				invocationList = this.OnSubtractUserVirtualCurrencyRequestEvent.GetInvocationList();
				foreach (Delegate obj201 in invocationList)
				{
					if (obj201.Target == instance)
					{
						OnSubtractUserVirtualCurrencyRequestEvent -= (PlayFabRequestEvent<SubtractUserVirtualCurrencyRequest>)obj201;
					}
				}
			}
			if (this.OnSubtractUserVirtualCurrencyResultEvent != null)
			{
				invocationList = this.OnSubtractUserVirtualCurrencyResultEvent.GetInvocationList();
				foreach (Delegate obj202 in invocationList)
				{
					if (obj202.Target == instance)
					{
						OnSubtractUserVirtualCurrencyResultEvent -= (PlayFabResultEvent<ModifyUserVirtualCurrencyResult>)obj202;
					}
				}
			}
			if (this.OnUnlinkAndroidDeviceIDRequestEvent != null)
			{
				invocationList = this.OnUnlinkAndroidDeviceIDRequestEvent.GetInvocationList();
				foreach (Delegate obj203 in invocationList)
				{
					if (obj203.Target == instance)
					{
						OnUnlinkAndroidDeviceIDRequestEvent -= (PlayFabRequestEvent<UnlinkAndroidDeviceIDRequest>)obj203;
					}
				}
			}
			if (this.OnUnlinkAndroidDeviceIDResultEvent != null)
			{
				invocationList = this.OnUnlinkAndroidDeviceIDResultEvent.GetInvocationList();
				foreach (Delegate obj204 in invocationList)
				{
					if (obj204.Target == instance)
					{
						OnUnlinkAndroidDeviceIDResultEvent -= (PlayFabResultEvent<UnlinkAndroidDeviceIDResult>)obj204;
					}
				}
			}
			if (this.OnUnlinkCustomIDRequestEvent != null)
			{
				invocationList = this.OnUnlinkCustomIDRequestEvent.GetInvocationList();
				foreach (Delegate obj205 in invocationList)
				{
					if (obj205.Target == instance)
					{
						OnUnlinkCustomIDRequestEvent -= (PlayFabRequestEvent<UnlinkCustomIDRequest>)obj205;
					}
				}
			}
			if (this.OnUnlinkCustomIDResultEvent != null)
			{
				invocationList = this.OnUnlinkCustomIDResultEvent.GetInvocationList();
				foreach (Delegate obj206 in invocationList)
				{
					if (obj206.Target == instance)
					{
						OnUnlinkCustomIDResultEvent -= (PlayFabResultEvent<UnlinkCustomIDResult>)obj206;
					}
				}
			}
			if (this.OnUnlinkFacebookAccountRequestEvent != null)
			{
				invocationList = this.OnUnlinkFacebookAccountRequestEvent.GetInvocationList();
				foreach (Delegate obj207 in invocationList)
				{
					if (obj207.Target == instance)
					{
						OnUnlinkFacebookAccountRequestEvent -= (PlayFabRequestEvent<UnlinkFacebookAccountRequest>)obj207;
					}
				}
			}
			if (this.OnUnlinkFacebookAccountResultEvent != null)
			{
				invocationList = this.OnUnlinkFacebookAccountResultEvent.GetInvocationList();
				foreach (Delegate obj208 in invocationList)
				{
					if (obj208.Target == instance)
					{
						OnUnlinkFacebookAccountResultEvent -= (PlayFabResultEvent<UnlinkFacebookAccountResult>)obj208;
					}
				}
			}
			if (this.OnUnlinkGameCenterAccountRequestEvent != null)
			{
				invocationList = this.OnUnlinkGameCenterAccountRequestEvent.GetInvocationList();
				foreach (Delegate obj209 in invocationList)
				{
					if (obj209.Target == instance)
					{
						OnUnlinkGameCenterAccountRequestEvent -= (PlayFabRequestEvent<UnlinkGameCenterAccountRequest>)obj209;
					}
				}
			}
			if (this.OnUnlinkGameCenterAccountResultEvent != null)
			{
				invocationList = this.OnUnlinkGameCenterAccountResultEvent.GetInvocationList();
				foreach (Delegate obj210 in invocationList)
				{
					if (obj210.Target == instance)
					{
						OnUnlinkGameCenterAccountResultEvent -= (PlayFabResultEvent<UnlinkGameCenterAccountResult>)obj210;
					}
				}
			}
			if (this.OnUnlinkGoogleAccountRequestEvent != null)
			{
				invocationList = this.OnUnlinkGoogleAccountRequestEvent.GetInvocationList();
				foreach (Delegate obj211 in invocationList)
				{
					if (obj211.Target == instance)
					{
						OnUnlinkGoogleAccountRequestEvent -= (PlayFabRequestEvent<UnlinkGoogleAccountRequest>)obj211;
					}
				}
			}
			if (this.OnUnlinkGoogleAccountResultEvent != null)
			{
				invocationList = this.OnUnlinkGoogleAccountResultEvent.GetInvocationList();
				foreach (Delegate obj212 in invocationList)
				{
					if (obj212.Target == instance)
					{
						OnUnlinkGoogleAccountResultEvent -= (PlayFabResultEvent<UnlinkGoogleAccountResult>)obj212;
					}
				}
			}
			if (this.OnUnlinkIOSDeviceIDRequestEvent != null)
			{
				invocationList = this.OnUnlinkIOSDeviceIDRequestEvent.GetInvocationList();
				foreach (Delegate obj213 in invocationList)
				{
					if (obj213.Target == instance)
					{
						OnUnlinkIOSDeviceIDRequestEvent -= (PlayFabRequestEvent<UnlinkIOSDeviceIDRequest>)obj213;
					}
				}
			}
			if (this.OnUnlinkIOSDeviceIDResultEvent != null)
			{
				invocationList = this.OnUnlinkIOSDeviceIDResultEvent.GetInvocationList();
				foreach (Delegate obj214 in invocationList)
				{
					if (obj214.Target == instance)
					{
						OnUnlinkIOSDeviceIDResultEvent -= (PlayFabResultEvent<UnlinkIOSDeviceIDResult>)obj214;
					}
				}
			}
			if (this.OnUnlinkKongregateRequestEvent != null)
			{
				invocationList = this.OnUnlinkKongregateRequestEvent.GetInvocationList();
				foreach (Delegate obj215 in invocationList)
				{
					if (obj215.Target == instance)
					{
						OnUnlinkKongregateRequestEvent -= (PlayFabRequestEvent<UnlinkKongregateAccountRequest>)obj215;
					}
				}
			}
			if (this.OnUnlinkKongregateResultEvent != null)
			{
				invocationList = this.OnUnlinkKongregateResultEvent.GetInvocationList();
				foreach (Delegate obj216 in invocationList)
				{
					if (obj216.Target == instance)
					{
						OnUnlinkKongregateResultEvent -= (PlayFabResultEvent<UnlinkKongregateAccountResult>)obj216;
					}
				}
			}
			if (this.OnUnlinkSteamAccountRequestEvent != null)
			{
				invocationList = this.OnUnlinkSteamAccountRequestEvent.GetInvocationList();
				foreach (Delegate obj217 in invocationList)
				{
					if (obj217.Target == instance)
					{
						OnUnlinkSteamAccountRequestEvent -= (PlayFabRequestEvent<UnlinkSteamAccountRequest>)obj217;
					}
				}
			}
			if (this.OnUnlinkSteamAccountResultEvent != null)
			{
				invocationList = this.OnUnlinkSteamAccountResultEvent.GetInvocationList();
				foreach (Delegate obj218 in invocationList)
				{
					if (obj218.Target == instance)
					{
						OnUnlinkSteamAccountResultEvent -= (PlayFabResultEvent<UnlinkSteamAccountResult>)obj218;
					}
				}
			}
			if (this.OnUnlinkTwitchRequestEvent != null)
			{
				invocationList = this.OnUnlinkTwitchRequestEvent.GetInvocationList();
				foreach (Delegate obj219 in invocationList)
				{
					if (obj219.Target == instance)
					{
						OnUnlinkTwitchRequestEvent -= (PlayFabRequestEvent<UnlinkTwitchAccountRequest>)obj219;
					}
				}
			}
			if (this.OnUnlinkTwitchResultEvent != null)
			{
				invocationList = this.OnUnlinkTwitchResultEvent.GetInvocationList();
				foreach (Delegate obj220 in invocationList)
				{
					if (obj220.Target == instance)
					{
						OnUnlinkTwitchResultEvent -= (PlayFabResultEvent<UnlinkTwitchAccountResult>)obj220;
					}
				}
			}
			if (this.OnUnlinkWindowsHelloRequestEvent != null)
			{
				invocationList = this.OnUnlinkWindowsHelloRequestEvent.GetInvocationList();
				foreach (Delegate obj221 in invocationList)
				{
					if (obj221.Target == instance)
					{
						OnUnlinkWindowsHelloRequestEvent -= (PlayFabRequestEvent<UnlinkWindowsHelloAccountRequest>)obj221;
					}
				}
			}
			if (this.OnUnlinkWindowsHelloResultEvent != null)
			{
				invocationList = this.OnUnlinkWindowsHelloResultEvent.GetInvocationList();
				foreach (Delegate obj222 in invocationList)
				{
					if (obj222.Target == instance)
					{
						OnUnlinkWindowsHelloResultEvent -= (PlayFabResultEvent<UnlinkWindowsHelloAccountResponse>)obj222;
					}
				}
			}
			if (this.OnUnlockContainerInstanceRequestEvent != null)
			{
				invocationList = this.OnUnlockContainerInstanceRequestEvent.GetInvocationList();
				foreach (Delegate obj223 in invocationList)
				{
					if (obj223.Target == instance)
					{
						OnUnlockContainerInstanceRequestEvent -= (PlayFabRequestEvent<UnlockContainerInstanceRequest>)obj223;
					}
				}
			}
			if (this.OnUnlockContainerInstanceResultEvent != null)
			{
				invocationList = this.OnUnlockContainerInstanceResultEvent.GetInvocationList();
				foreach (Delegate obj224 in invocationList)
				{
					if (obj224.Target == instance)
					{
						OnUnlockContainerInstanceResultEvent -= (PlayFabResultEvent<UnlockContainerItemResult>)obj224;
					}
				}
			}
			if (this.OnUnlockContainerItemRequestEvent != null)
			{
				invocationList = this.OnUnlockContainerItemRequestEvent.GetInvocationList();
				foreach (Delegate obj225 in invocationList)
				{
					if (obj225.Target == instance)
					{
						OnUnlockContainerItemRequestEvent -= (PlayFabRequestEvent<UnlockContainerItemRequest>)obj225;
					}
				}
			}
			if (this.OnUnlockContainerItemResultEvent != null)
			{
				invocationList = this.OnUnlockContainerItemResultEvent.GetInvocationList();
				foreach (Delegate obj226 in invocationList)
				{
					if (obj226.Target == instance)
					{
						OnUnlockContainerItemResultEvent -= (PlayFabResultEvent<UnlockContainerItemResult>)obj226;
					}
				}
			}
			if (this.OnUpdateAvatarUrlRequestEvent != null)
			{
				invocationList = this.OnUpdateAvatarUrlRequestEvent.GetInvocationList();
				foreach (Delegate obj227 in invocationList)
				{
					if (obj227.Target == instance)
					{
						OnUpdateAvatarUrlRequestEvent -= (PlayFabRequestEvent<UpdateAvatarUrlRequest>)obj227;
					}
				}
			}
			if (this.OnUpdateAvatarUrlResultEvent != null)
			{
				invocationList = this.OnUpdateAvatarUrlResultEvent.GetInvocationList();
				foreach (Delegate obj228 in invocationList)
				{
					if (obj228.Target == instance)
					{
						OnUpdateAvatarUrlResultEvent -= (PlayFabResultEvent<EmptyResult>)obj228;
					}
				}
			}
			if (this.OnUpdateCharacterDataRequestEvent != null)
			{
				invocationList = this.OnUpdateCharacterDataRequestEvent.GetInvocationList();
				foreach (Delegate obj229 in invocationList)
				{
					if (obj229.Target == instance)
					{
						OnUpdateCharacterDataRequestEvent -= (PlayFabRequestEvent<UpdateCharacterDataRequest>)obj229;
					}
				}
			}
			if (this.OnUpdateCharacterDataResultEvent != null)
			{
				invocationList = this.OnUpdateCharacterDataResultEvent.GetInvocationList();
				foreach (Delegate obj230 in invocationList)
				{
					if (obj230.Target == instance)
					{
						OnUpdateCharacterDataResultEvent -= (PlayFabResultEvent<UpdateCharacterDataResult>)obj230;
					}
				}
			}
			if (this.OnUpdateCharacterStatisticsRequestEvent != null)
			{
				invocationList = this.OnUpdateCharacterStatisticsRequestEvent.GetInvocationList();
				foreach (Delegate obj231 in invocationList)
				{
					if (obj231.Target == instance)
					{
						OnUpdateCharacterStatisticsRequestEvent -= (PlayFabRequestEvent<UpdateCharacterStatisticsRequest>)obj231;
					}
				}
			}
			if (this.OnUpdateCharacterStatisticsResultEvent != null)
			{
				invocationList = this.OnUpdateCharacterStatisticsResultEvent.GetInvocationList();
				foreach (Delegate obj232 in invocationList)
				{
					if (obj232.Target == instance)
					{
						OnUpdateCharacterStatisticsResultEvent -= (PlayFabResultEvent<UpdateCharacterStatisticsResult>)obj232;
					}
				}
			}
			if (this.OnUpdatePlayerStatisticsRequestEvent != null)
			{
				invocationList = this.OnUpdatePlayerStatisticsRequestEvent.GetInvocationList();
				foreach (Delegate obj233 in invocationList)
				{
					if (obj233.Target == instance)
					{
						OnUpdatePlayerStatisticsRequestEvent -= (PlayFabRequestEvent<UpdatePlayerStatisticsRequest>)obj233;
					}
				}
			}
			if (this.OnUpdatePlayerStatisticsResultEvent != null)
			{
				invocationList = this.OnUpdatePlayerStatisticsResultEvent.GetInvocationList();
				foreach (Delegate obj234 in invocationList)
				{
					if (obj234.Target == instance)
					{
						OnUpdatePlayerStatisticsResultEvent -= (PlayFabResultEvent<UpdatePlayerStatisticsResult>)obj234;
					}
				}
			}
			if (this.OnUpdateSharedGroupDataRequestEvent != null)
			{
				invocationList = this.OnUpdateSharedGroupDataRequestEvent.GetInvocationList();
				foreach (Delegate obj235 in invocationList)
				{
					if (obj235.Target == instance)
					{
						OnUpdateSharedGroupDataRequestEvent -= (PlayFabRequestEvent<UpdateSharedGroupDataRequest>)obj235;
					}
				}
			}
			if (this.OnUpdateSharedGroupDataResultEvent != null)
			{
				invocationList = this.OnUpdateSharedGroupDataResultEvent.GetInvocationList();
				foreach (Delegate obj236 in invocationList)
				{
					if (obj236.Target == instance)
					{
						OnUpdateSharedGroupDataResultEvent -= (PlayFabResultEvent<UpdateSharedGroupDataResult>)obj236;
					}
				}
			}
			if (this.OnUpdateUserDataRequestEvent != null)
			{
				invocationList = this.OnUpdateUserDataRequestEvent.GetInvocationList();
				foreach (Delegate obj237 in invocationList)
				{
					if (obj237.Target == instance)
					{
						OnUpdateUserDataRequestEvent -= (PlayFabRequestEvent<UpdateUserDataRequest>)obj237;
					}
				}
			}
			if (this.OnUpdateUserDataResultEvent != null)
			{
				invocationList = this.OnUpdateUserDataResultEvent.GetInvocationList();
				foreach (Delegate obj238 in invocationList)
				{
					if (obj238.Target == instance)
					{
						OnUpdateUserDataResultEvent -= (PlayFabResultEvent<UpdateUserDataResult>)obj238;
					}
				}
			}
			if (this.OnUpdateUserPublisherDataRequestEvent != null)
			{
				invocationList = this.OnUpdateUserPublisherDataRequestEvent.GetInvocationList();
				foreach (Delegate obj239 in invocationList)
				{
					if (obj239.Target == instance)
					{
						OnUpdateUserPublisherDataRequestEvent -= (PlayFabRequestEvent<UpdateUserDataRequest>)obj239;
					}
				}
			}
			if (this.OnUpdateUserPublisherDataResultEvent != null)
			{
				invocationList = this.OnUpdateUserPublisherDataResultEvent.GetInvocationList();
				foreach (Delegate obj240 in invocationList)
				{
					if (obj240.Target == instance)
					{
						OnUpdateUserPublisherDataResultEvent -= (PlayFabResultEvent<UpdateUserDataResult>)obj240;
					}
				}
			}
			if (this.OnUpdateUserTitleDisplayNameRequestEvent != null)
			{
				invocationList = this.OnUpdateUserTitleDisplayNameRequestEvent.GetInvocationList();
				foreach (Delegate obj241 in invocationList)
				{
					if (obj241.Target == instance)
					{
						OnUpdateUserTitleDisplayNameRequestEvent -= (PlayFabRequestEvent<UpdateUserTitleDisplayNameRequest>)obj241;
					}
				}
			}
			if (this.OnUpdateUserTitleDisplayNameResultEvent != null)
			{
				invocationList = this.OnUpdateUserTitleDisplayNameResultEvent.GetInvocationList();
				foreach (Delegate obj242 in invocationList)
				{
					if (obj242.Target == instance)
					{
						OnUpdateUserTitleDisplayNameResultEvent -= (PlayFabResultEvent<UpdateUserTitleDisplayNameResult>)obj242;
					}
				}
			}
			if (this.OnValidateAmazonIAPReceiptRequestEvent != null)
			{
				invocationList = this.OnValidateAmazonIAPReceiptRequestEvent.GetInvocationList();
				foreach (Delegate obj243 in invocationList)
				{
					if (obj243.Target == instance)
					{
						OnValidateAmazonIAPReceiptRequestEvent -= (PlayFabRequestEvent<ValidateAmazonReceiptRequest>)obj243;
					}
				}
			}
			if (this.OnValidateAmazonIAPReceiptResultEvent != null)
			{
				invocationList = this.OnValidateAmazonIAPReceiptResultEvent.GetInvocationList();
				foreach (Delegate obj244 in invocationList)
				{
					if (obj244.Target == instance)
					{
						OnValidateAmazonIAPReceiptResultEvent -= (PlayFabResultEvent<ValidateAmazonReceiptResult>)obj244;
					}
				}
			}
			if (this.OnValidateGooglePlayPurchaseRequestEvent != null)
			{
				invocationList = this.OnValidateGooglePlayPurchaseRequestEvent.GetInvocationList();
				foreach (Delegate obj245 in invocationList)
				{
					if (obj245.Target == instance)
					{
						OnValidateGooglePlayPurchaseRequestEvent -= (PlayFabRequestEvent<ValidateGooglePlayPurchaseRequest>)obj245;
					}
				}
			}
			if (this.OnValidateGooglePlayPurchaseResultEvent != null)
			{
				invocationList = this.OnValidateGooglePlayPurchaseResultEvent.GetInvocationList();
				foreach (Delegate obj246 in invocationList)
				{
					if (obj246.Target == instance)
					{
						OnValidateGooglePlayPurchaseResultEvent -= (PlayFabResultEvent<ValidateGooglePlayPurchaseResult>)obj246;
					}
				}
			}
			if (this.OnValidateIOSReceiptRequestEvent != null)
			{
				invocationList = this.OnValidateIOSReceiptRequestEvent.GetInvocationList();
				foreach (Delegate obj247 in invocationList)
				{
					if (obj247.Target == instance)
					{
						OnValidateIOSReceiptRequestEvent -= (PlayFabRequestEvent<ValidateIOSReceiptRequest>)obj247;
					}
				}
			}
			if (this.OnValidateIOSReceiptResultEvent != null)
			{
				invocationList = this.OnValidateIOSReceiptResultEvent.GetInvocationList();
				foreach (Delegate obj248 in invocationList)
				{
					if (obj248.Target == instance)
					{
						OnValidateIOSReceiptResultEvent -= (PlayFabResultEvent<ValidateIOSReceiptResult>)obj248;
					}
				}
			}
			if (this.OnValidateWindowsStoreReceiptRequestEvent != null)
			{
				invocationList = this.OnValidateWindowsStoreReceiptRequestEvent.GetInvocationList();
				foreach (Delegate obj249 in invocationList)
				{
					if (obj249.Target == instance)
					{
						OnValidateWindowsStoreReceiptRequestEvent -= (PlayFabRequestEvent<ValidateWindowsReceiptRequest>)obj249;
					}
				}
			}
			if (this.OnValidateWindowsStoreReceiptResultEvent != null)
			{
				invocationList = this.OnValidateWindowsStoreReceiptResultEvent.GetInvocationList();
				foreach (Delegate obj250 in invocationList)
				{
					if (obj250.Target == instance)
					{
						OnValidateWindowsStoreReceiptResultEvent -= (PlayFabResultEvent<ValidateWindowsReceiptResult>)obj250;
					}
				}
			}
			if (this.OnWriteCharacterEventRequestEvent != null)
			{
				invocationList = this.OnWriteCharacterEventRequestEvent.GetInvocationList();
				foreach (Delegate obj251 in invocationList)
				{
					if (obj251.Target == instance)
					{
						OnWriteCharacterEventRequestEvent -= (PlayFabRequestEvent<WriteClientCharacterEventRequest>)obj251;
					}
				}
			}
			if (this.OnWriteCharacterEventResultEvent != null)
			{
				invocationList = this.OnWriteCharacterEventResultEvent.GetInvocationList();
				foreach (Delegate obj252 in invocationList)
				{
					if (obj252.Target == instance)
					{
						OnWriteCharacterEventResultEvent -= (PlayFabResultEvent<WriteEventResponse>)obj252;
					}
				}
			}
			if (this.OnWritePlayerEventRequestEvent != null)
			{
				invocationList = this.OnWritePlayerEventRequestEvent.GetInvocationList();
				foreach (Delegate obj253 in invocationList)
				{
					if (obj253.Target == instance)
					{
						OnWritePlayerEventRequestEvent -= (PlayFabRequestEvent<WriteClientPlayerEventRequest>)obj253;
					}
				}
			}
			if (this.OnWritePlayerEventResultEvent != null)
			{
				invocationList = this.OnWritePlayerEventResultEvent.GetInvocationList();
				foreach (Delegate obj254 in invocationList)
				{
					if (obj254.Target == instance)
					{
						OnWritePlayerEventResultEvent -= (PlayFabResultEvent<WriteEventResponse>)obj254;
					}
				}
			}
			if (this.OnWriteTitleEventRequestEvent != null)
			{
				invocationList = this.OnWriteTitleEventRequestEvent.GetInvocationList();
				foreach (Delegate obj255 in invocationList)
				{
					if (obj255.Target == instance)
					{
						OnWriteTitleEventRequestEvent -= (PlayFabRequestEvent<WriteTitleEventRequest>)obj255;
					}
				}
			}
			if (this.OnWriteTitleEventResultEvent == null)
			{
				return;
			}
			invocationList = this.OnWriteTitleEventResultEvent.GetInvocationList();
			foreach (Delegate obj256 in invocationList)
			{
				if (obj256.Target == instance)
				{
					OnWriteTitleEventResultEvent -= (PlayFabResultEvent<WriteEventResponse>)obj256;
				}
			}
		}

		private void OnProcessingErrorEvent(PlayFabRequestCommon request, PlayFabError error)
		{
			if (_instance.OnGlobalErrorEvent != null)
			{
				_instance.OnGlobalErrorEvent(request, error);
			}
		}

		private void OnProcessingEvent(ApiProcessingEventArgs e)
		{
			if (e.EventType == ApiProcessingEventType.Pre)
			{
				Type type = e.Request.GetType();
				if (type == typeof(AcceptTradeRequest) && _instance.OnAcceptTradeRequestEvent != null)
				{
					_instance.OnAcceptTradeRequestEvent((AcceptTradeRequest)e.Request);
				}
				else if (type == typeof(AddFriendRequest) && _instance.OnAddFriendRequestEvent != null)
				{
					_instance.OnAddFriendRequestEvent((AddFriendRequest)e.Request);
				}
				else if (type == typeof(AddGenericIDRequest) && _instance.OnAddGenericIDRequestEvent != null)
				{
					_instance.OnAddGenericIDRequestEvent((AddGenericIDRequest)e.Request);
				}
				else if (type == typeof(AddOrUpdateContactEmailRequest) && _instance.OnAddOrUpdateContactEmailRequestEvent != null)
				{
					_instance.OnAddOrUpdateContactEmailRequestEvent((AddOrUpdateContactEmailRequest)e.Request);
				}
				else if (type == typeof(AddSharedGroupMembersRequest) && _instance.OnAddSharedGroupMembersRequestEvent != null)
				{
					_instance.OnAddSharedGroupMembersRequestEvent((AddSharedGroupMembersRequest)e.Request);
				}
				else if (type == typeof(AddUsernamePasswordRequest) && _instance.OnAddUsernamePasswordRequestEvent != null)
				{
					_instance.OnAddUsernamePasswordRequestEvent((AddUsernamePasswordRequest)e.Request);
				}
				else if (type == typeof(AddUserVirtualCurrencyRequest) && _instance.OnAddUserVirtualCurrencyRequestEvent != null)
				{
					_instance.OnAddUserVirtualCurrencyRequestEvent((AddUserVirtualCurrencyRequest)e.Request);
				}
				else if (type == typeof(AndroidDevicePushNotificationRegistrationRequest) && _instance.OnAndroidDevicePushNotificationRegistrationRequestEvent != null)
				{
					_instance.OnAndroidDevicePushNotificationRegistrationRequestEvent((AndroidDevicePushNotificationRegistrationRequest)e.Request);
				}
				else if (type == typeof(AttributeInstallRequest) && _instance.OnAttributeInstallRequestEvent != null)
				{
					_instance.OnAttributeInstallRequestEvent((AttributeInstallRequest)e.Request);
				}
				else if (type == typeof(CancelTradeRequest) && _instance.OnCancelTradeRequestEvent != null)
				{
					_instance.OnCancelTradeRequestEvent((CancelTradeRequest)e.Request);
				}
				else if (type == typeof(ConfirmPurchaseRequest) && _instance.OnConfirmPurchaseRequestEvent != null)
				{
					_instance.OnConfirmPurchaseRequestEvent((ConfirmPurchaseRequest)e.Request);
				}
				else if (type == typeof(ConsumeItemRequest) && _instance.OnConsumeItemRequestEvent != null)
				{
					_instance.OnConsumeItemRequestEvent((ConsumeItemRequest)e.Request);
				}
				else if (type == typeof(CreateSharedGroupRequest) && _instance.OnCreateSharedGroupRequestEvent != null)
				{
					_instance.OnCreateSharedGroupRequestEvent((CreateSharedGroupRequest)e.Request);
				}
				else if (type == typeof(ExecuteCloudScriptRequest) && _instance.OnExecuteCloudScriptRequestEvent != null)
				{
					_instance.OnExecuteCloudScriptRequestEvent((ExecuteCloudScriptRequest)e.Request);
				}
				else if (type == typeof(GetAccountInfoRequest) && _instance.OnGetAccountInfoRequestEvent != null)
				{
					_instance.OnGetAccountInfoRequestEvent((GetAccountInfoRequest)e.Request);
				}
				else if (type == typeof(ListUsersCharactersRequest) && _instance.OnGetAllUsersCharactersRequestEvent != null)
				{
					_instance.OnGetAllUsersCharactersRequestEvent((ListUsersCharactersRequest)e.Request);
				}
				else if (type == typeof(GetCatalogItemsRequest) && _instance.OnGetCatalogItemsRequestEvent != null)
				{
					_instance.OnGetCatalogItemsRequestEvent((GetCatalogItemsRequest)e.Request);
				}
				else if (type == typeof(GetCharacterDataRequest) && _instance.OnGetCharacterDataRequestEvent != null)
				{
					_instance.OnGetCharacterDataRequestEvent((GetCharacterDataRequest)e.Request);
				}
				else if (type == typeof(GetCharacterInventoryRequest) && _instance.OnGetCharacterInventoryRequestEvent != null)
				{
					_instance.OnGetCharacterInventoryRequestEvent((GetCharacterInventoryRequest)e.Request);
				}
				else if (type == typeof(GetCharacterLeaderboardRequest) && _instance.OnGetCharacterLeaderboardRequestEvent != null)
				{
					_instance.OnGetCharacterLeaderboardRequestEvent((GetCharacterLeaderboardRequest)e.Request);
				}
				else if (type == typeof(GetCharacterDataRequest) && _instance.OnGetCharacterReadOnlyDataRequestEvent != null)
				{
					_instance.OnGetCharacterReadOnlyDataRequestEvent((GetCharacterDataRequest)e.Request);
				}
				else if (type == typeof(GetCharacterStatisticsRequest) && _instance.OnGetCharacterStatisticsRequestEvent != null)
				{
					_instance.OnGetCharacterStatisticsRequestEvent((GetCharacterStatisticsRequest)e.Request);
				}
				else if (type == typeof(GetContentDownloadUrlRequest) && _instance.OnGetContentDownloadUrlRequestEvent != null)
				{
					_instance.OnGetContentDownloadUrlRequestEvent((GetContentDownloadUrlRequest)e.Request);
				}
				else if (type == typeof(CurrentGamesRequest) && _instance.OnGetCurrentGamesRequestEvent != null)
				{
					_instance.OnGetCurrentGamesRequestEvent((CurrentGamesRequest)e.Request);
				}
				else if (type == typeof(GetFriendLeaderboardRequest) && _instance.OnGetFriendLeaderboardRequestEvent != null)
				{
					_instance.OnGetFriendLeaderboardRequestEvent((GetFriendLeaderboardRequest)e.Request);
				}
				else if (type == typeof(GetFriendLeaderboardAroundPlayerRequest) && _instance.OnGetFriendLeaderboardAroundPlayerRequestEvent != null)
				{
					_instance.OnGetFriendLeaderboardAroundPlayerRequestEvent((GetFriendLeaderboardAroundPlayerRequest)e.Request);
				}
				else if (type == typeof(GetFriendsListRequest) && _instance.OnGetFriendsListRequestEvent != null)
				{
					_instance.OnGetFriendsListRequestEvent((GetFriendsListRequest)e.Request);
				}
				else if (type == typeof(GameServerRegionsRequest) && _instance.OnGetGameServerRegionsRequestEvent != null)
				{
					_instance.OnGetGameServerRegionsRequestEvent((GameServerRegionsRequest)e.Request);
				}
				else if (type == typeof(GetLeaderboardRequest) && _instance.OnGetLeaderboardRequestEvent != null)
				{
					_instance.OnGetLeaderboardRequestEvent((GetLeaderboardRequest)e.Request);
				}
				else if (type == typeof(GetLeaderboardAroundCharacterRequest) && _instance.OnGetLeaderboardAroundCharacterRequestEvent != null)
				{
					_instance.OnGetLeaderboardAroundCharacterRequestEvent((GetLeaderboardAroundCharacterRequest)e.Request);
				}
				else if (type == typeof(GetLeaderboardAroundPlayerRequest) && _instance.OnGetLeaderboardAroundPlayerRequestEvent != null)
				{
					_instance.OnGetLeaderboardAroundPlayerRequestEvent((GetLeaderboardAroundPlayerRequest)e.Request);
				}
				else if (type == typeof(GetLeaderboardForUsersCharactersRequest) && _instance.OnGetLeaderboardForUserCharactersRequestEvent != null)
				{
					_instance.OnGetLeaderboardForUserCharactersRequestEvent((GetLeaderboardForUsersCharactersRequest)e.Request);
				}
				else if (type == typeof(GetPaymentTokenRequest) && _instance.OnGetPaymentTokenRequestEvent != null)
				{
					_instance.OnGetPaymentTokenRequestEvent((GetPaymentTokenRequest)e.Request);
				}
				else if (type == typeof(GetPhotonAuthenticationTokenRequest) && _instance.OnGetPhotonAuthenticationTokenRequestEvent != null)
				{
					_instance.OnGetPhotonAuthenticationTokenRequestEvent((GetPhotonAuthenticationTokenRequest)e.Request);
				}
				else if (type == typeof(GetPlayerCombinedInfoRequest) && _instance.OnGetPlayerCombinedInfoRequestEvent != null)
				{
					_instance.OnGetPlayerCombinedInfoRequestEvent((GetPlayerCombinedInfoRequest)e.Request);
				}
				else if (type == typeof(GetPlayerProfileRequest) && _instance.OnGetPlayerProfileRequestEvent != null)
				{
					_instance.OnGetPlayerProfileRequestEvent((GetPlayerProfileRequest)e.Request);
				}
				else if (type == typeof(GetPlayerSegmentsRequest) && _instance.OnGetPlayerSegmentsRequestEvent != null)
				{
					_instance.OnGetPlayerSegmentsRequestEvent((GetPlayerSegmentsRequest)e.Request);
				}
				else if (type == typeof(GetPlayerStatisticsRequest) && _instance.OnGetPlayerStatisticsRequestEvent != null)
				{
					_instance.OnGetPlayerStatisticsRequestEvent((GetPlayerStatisticsRequest)e.Request);
				}
				else if (type == typeof(GetPlayerStatisticVersionsRequest) && _instance.OnGetPlayerStatisticVersionsRequestEvent != null)
				{
					_instance.OnGetPlayerStatisticVersionsRequestEvent((GetPlayerStatisticVersionsRequest)e.Request);
				}
				else if (type == typeof(GetPlayerTagsRequest) && _instance.OnGetPlayerTagsRequestEvent != null)
				{
					_instance.OnGetPlayerTagsRequestEvent((GetPlayerTagsRequest)e.Request);
				}
				else if (type == typeof(GetPlayerTradesRequest) && _instance.OnGetPlayerTradesRequestEvent != null)
				{
					_instance.OnGetPlayerTradesRequestEvent((GetPlayerTradesRequest)e.Request);
				}
				else if (type == typeof(GetPlayFabIDsFromFacebookIDsRequest) && _instance.OnGetPlayFabIDsFromFacebookIDsRequestEvent != null)
				{
					_instance.OnGetPlayFabIDsFromFacebookIDsRequestEvent((GetPlayFabIDsFromFacebookIDsRequest)e.Request);
				}
				else if (type == typeof(GetPlayFabIDsFromGameCenterIDsRequest) && _instance.OnGetPlayFabIDsFromGameCenterIDsRequestEvent != null)
				{
					_instance.OnGetPlayFabIDsFromGameCenterIDsRequestEvent((GetPlayFabIDsFromGameCenterIDsRequest)e.Request);
				}
				else if (type == typeof(GetPlayFabIDsFromGenericIDsRequest) && _instance.OnGetPlayFabIDsFromGenericIDsRequestEvent != null)
				{
					_instance.OnGetPlayFabIDsFromGenericIDsRequestEvent((GetPlayFabIDsFromGenericIDsRequest)e.Request);
				}
				else if (type == typeof(GetPlayFabIDsFromGoogleIDsRequest) && _instance.OnGetPlayFabIDsFromGoogleIDsRequestEvent != null)
				{
					_instance.OnGetPlayFabIDsFromGoogleIDsRequestEvent((GetPlayFabIDsFromGoogleIDsRequest)e.Request);
				}
				else if (type == typeof(GetPlayFabIDsFromKongregateIDsRequest) && _instance.OnGetPlayFabIDsFromKongregateIDsRequestEvent != null)
				{
					_instance.OnGetPlayFabIDsFromKongregateIDsRequestEvent((GetPlayFabIDsFromKongregateIDsRequest)e.Request);
				}
				else if (type == typeof(GetPlayFabIDsFromSteamIDsRequest) && _instance.OnGetPlayFabIDsFromSteamIDsRequestEvent != null)
				{
					_instance.OnGetPlayFabIDsFromSteamIDsRequestEvent((GetPlayFabIDsFromSteamIDsRequest)e.Request);
				}
				else if (type == typeof(GetPlayFabIDsFromTwitchIDsRequest) && _instance.OnGetPlayFabIDsFromTwitchIDsRequestEvent != null)
				{
					_instance.OnGetPlayFabIDsFromTwitchIDsRequestEvent((GetPlayFabIDsFromTwitchIDsRequest)e.Request);
				}
				else if (type == typeof(GetPublisherDataRequest) && _instance.OnGetPublisherDataRequestEvent != null)
				{
					_instance.OnGetPublisherDataRequestEvent((GetPublisherDataRequest)e.Request);
				}
				else if (type == typeof(GetPurchaseRequest) && _instance.OnGetPurchaseRequestEvent != null)
				{
					_instance.OnGetPurchaseRequestEvent((GetPurchaseRequest)e.Request);
				}
				else if (type == typeof(GetSharedGroupDataRequest) && _instance.OnGetSharedGroupDataRequestEvent != null)
				{
					_instance.OnGetSharedGroupDataRequestEvent((GetSharedGroupDataRequest)e.Request);
				}
				else if (type == typeof(GetStoreItemsRequest) && _instance.OnGetStoreItemsRequestEvent != null)
				{
					_instance.OnGetStoreItemsRequestEvent((GetStoreItemsRequest)e.Request);
				}
				else if (type == typeof(GetTimeRequest) && _instance.OnGetTimeRequestEvent != null)
				{
					_instance.OnGetTimeRequestEvent((GetTimeRequest)e.Request);
				}
				else if (type == typeof(GetTitleDataRequest) && _instance.OnGetTitleDataRequestEvent != null)
				{
					_instance.OnGetTitleDataRequestEvent((GetTitleDataRequest)e.Request);
				}
				else if (type == typeof(GetTitleNewsRequest) && _instance.OnGetTitleNewsRequestEvent != null)
				{
					_instance.OnGetTitleNewsRequestEvent((GetTitleNewsRequest)e.Request);
				}
				else if (type == typeof(GetTitlePublicKeyRequest) && _instance.OnGetTitlePublicKeyRequestEvent != null)
				{
					_instance.OnGetTitlePublicKeyRequestEvent((GetTitlePublicKeyRequest)e.Request);
				}
				else if (type == typeof(GetTradeStatusRequest) && _instance.OnGetTradeStatusRequestEvent != null)
				{
					_instance.OnGetTradeStatusRequestEvent((GetTradeStatusRequest)e.Request);
				}
				else if (type == typeof(GetUserDataRequest) && _instance.OnGetUserDataRequestEvent != null)
				{
					_instance.OnGetUserDataRequestEvent((GetUserDataRequest)e.Request);
				}
				else if (type == typeof(GetUserInventoryRequest) && _instance.OnGetUserInventoryRequestEvent != null)
				{
					_instance.OnGetUserInventoryRequestEvent((GetUserInventoryRequest)e.Request);
				}
				else if (type == typeof(GetUserDataRequest) && _instance.OnGetUserPublisherDataRequestEvent != null)
				{
					_instance.OnGetUserPublisherDataRequestEvent((GetUserDataRequest)e.Request);
				}
				else if (type == typeof(GetUserDataRequest) && _instance.OnGetUserPublisherReadOnlyDataRequestEvent != null)
				{
					_instance.OnGetUserPublisherReadOnlyDataRequestEvent((GetUserDataRequest)e.Request);
				}
				else if (type == typeof(GetUserDataRequest) && _instance.OnGetUserReadOnlyDataRequestEvent != null)
				{
					_instance.OnGetUserReadOnlyDataRequestEvent((GetUserDataRequest)e.Request);
				}
				else if (type == typeof(GetWindowsHelloChallengeRequest) && _instance.OnGetWindowsHelloChallengeRequestEvent != null)
				{
					_instance.OnGetWindowsHelloChallengeRequestEvent((GetWindowsHelloChallengeRequest)e.Request);
				}
				else if (type == typeof(GrantCharacterToUserRequest) && _instance.OnGrantCharacterToUserRequestEvent != null)
				{
					_instance.OnGrantCharacterToUserRequestEvent((GrantCharacterToUserRequest)e.Request);
				}
				else if (type == typeof(LinkAndroidDeviceIDRequest) && _instance.OnLinkAndroidDeviceIDRequestEvent != null)
				{
					_instance.OnLinkAndroidDeviceIDRequestEvent((LinkAndroidDeviceIDRequest)e.Request);
				}
				else if (type == typeof(LinkCustomIDRequest) && _instance.OnLinkCustomIDRequestEvent != null)
				{
					_instance.OnLinkCustomIDRequestEvent((LinkCustomIDRequest)e.Request);
				}
				else if (type == typeof(LinkFacebookAccountRequest) && _instance.OnLinkFacebookAccountRequestEvent != null)
				{
					_instance.OnLinkFacebookAccountRequestEvent((LinkFacebookAccountRequest)e.Request);
				}
				else if (type == typeof(LinkGameCenterAccountRequest) && _instance.OnLinkGameCenterAccountRequestEvent != null)
				{
					_instance.OnLinkGameCenterAccountRequestEvent((LinkGameCenterAccountRequest)e.Request);
				}
				else if (type == typeof(LinkGoogleAccountRequest) && _instance.OnLinkGoogleAccountRequestEvent != null)
				{
					_instance.OnLinkGoogleAccountRequestEvent((LinkGoogleAccountRequest)e.Request);
				}
				else if (type == typeof(LinkIOSDeviceIDRequest) && _instance.OnLinkIOSDeviceIDRequestEvent != null)
				{
					_instance.OnLinkIOSDeviceIDRequestEvent((LinkIOSDeviceIDRequest)e.Request);
				}
				else if (type == typeof(LinkKongregateAccountRequest) && _instance.OnLinkKongregateRequestEvent != null)
				{
					_instance.OnLinkKongregateRequestEvent((LinkKongregateAccountRequest)e.Request);
				}
				else if (type == typeof(LinkSteamAccountRequest) && _instance.OnLinkSteamAccountRequestEvent != null)
				{
					_instance.OnLinkSteamAccountRequestEvent((LinkSteamAccountRequest)e.Request);
				}
				else if (type == typeof(LinkTwitchAccountRequest) && _instance.OnLinkTwitchRequestEvent != null)
				{
					_instance.OnLinkTwitchRequestEvent((LinkTwitchAccountRequest)e.Request);
				}
				else if (type == typeof(LinkWindowsHelloAccountRequest) && _instance.OnLinkWindowsHelloRequestEvent != null)
				{
					_instance.OnLinkWindowsHelloRequestEvent((LinkWindowsHelloAccountRequest)e.Request);
				}
				else if (type == typeof(LoginWithAndroidDeviceIDRequest) && _instance.OnLoginWithAndroidDeviceIDRequestEvent != null)
				{
					_instance.OnLoginWithAndroidDeviceIDRequestEvent((LoginWithAndroidDeviceIDRequest)e.Request);
				}
				else if (type == typeof(LoginWithCustomIDRequest) && _instance.OnLoginWithCustomIDRequestEvent != null)
				{
					_instance.OnLoginWithCustomIDRequestEvent((LoginWithCustomIDRequest)e.Request);
				}
				else if (type == typeof(LoginWithEmailAddressRequest) && _instance.OnLoginWithEmailAddressRequestEvent != null)
				{
					_instance.OnLoginWithEmailAddressRequestEvent((LoginWithEmailAddressRequest)e.Request);
				}
				else if (type == typeof(LoginWithFacebookRequest) && _instance.OnLoginWithFacebookRequestEvent != null)
				{
					_instance.OnLoginWithFacebookRequestEvent((LoginWithFacebookRequest)e.Request);
				}
				else if (type == typeof(LoginWithGameCenterRequest) && _instance.OnLoginWithGameCenterRequestEvent != null)
				{
					_instance.OnLoginWithGameCenterRequestEvent((LoginWithGameCenterRequest)e.Request);
				}
				else if (type == typeof(LoginWithGoogleAccountRequest) && _instance.OnLoginWithGoogleAccountRequestEvent != null)
				{
					_instance.OnLoginWithGoogleAccountRequestEvent((LoginWithGoogleAccountRequest)e.Request);
				}
				else if (type == typeof(LoginWithIOSDeviceIDRequest) && _instance.OnLoginWithIOSDeviceIDRequestEvent != null)
				{
					_instance.OnLoginWithIOSDeviceIDRequestEvent((LoginWithIOSDeviceIDRequest)e.Request);
				}
				else if (type == typeof(LoginWithKongregateRequest) && _instance.OnLoginWithKongregateRequestEvent != null)
				{
					_instance.OnLoginWithKongregateRequestEvent((LoginWithKongregateRequest)e.Request);
				}
				else if (type == typeof(LoginWithPlayFabRequest) && _instance.OnLoginWithPlayFabRequestEvent != null)
				{
					_instance.OnLoginWithPlayFabRequestEvent((LoginWithPlayFabRequest)e.Request);
				}
				else if (type == typeof(LoginWithSteamRequest) && _instance.OnLoginWithSteamRequestEvent != null)
				{
					_instance.OnLoginWithSteamRequestEvent((LoginWithSteamRequest)e.Request);
				}
				else if (type == typeof(LoginWithTwitchRequest) && _instance.OnLoginWithTwitchRequestEvent != null)
				{
					_instance.OnLoginWithTwitchRequestEvent((LoginWithTwitchRequest)e.Request);
				}
				else if (type == typeof(LoginWithWindowsHelloRequest) && _instance.OnLoginWithWindowsHelloRequestEvent != null)
				{
					_instance.OnLoginWithWindowsHelloRequestEvent((LoginWithWindowsHelloRequest)e.Request);
				}
				else if (type == typeof(MatchmakeRequest) && _instance.OnMatchmakeRequestEvent != null)
				{
					_instance.OnMatchmakeRequestEvent((MatchmakeRequest)e.Request);
				}
				else if (type == typeof(OpenTradeRequest) && _instance.OnOpenTradeRequestEvent != null)
				{
					_instance.OnOpenTradeRequestEvent((OpenTradeRequest)e.Request);
				}
				else if (type == typeof(PayForPurchaseRequest) && _instance.OnPayForPurchaseRequestEvent != null)
				{
					_instance.OnPayForPurchaseRequestEvent((PayForPurchaseRequest)e.Request);
				}
				else if (type == typeof(PurchaseItemRequest) && _instance.OnPurchaseItemRequestEvent != null)
				{
					_instance.OnPurchaseItemRequestEvent((PurchaseItemRequest)e.Request);
				}
				else if (type == typeof(RedeemCouponRequest) && _instance.OnRedeemCouponRequestEvent != null)
				{
					_instance.OnRedeemCouponRequestEvent((RedeemCouponRequest)e.Request);
				}
				else if (type == typeof(RegisterForIOSPushNotificationRequest) && _instance.OnRegisterForIOSPushNotificationRequestEvent != null)
				{
					_instance.OnRegisterForIOSPushNotificationRequestEvent((RegisterForIOSPushNotificationRequest)e.Request);
				}
				else if (type == typeof(RegisterPlayFabUserRequest) && _instance.OnRegisterPlayFabUserRequestEvent != null)
				{
					_instance.OnRegisterPlayFabUserRequestEvent((RegisterPlayFabUserRequest)e.Request);
				}
				else if (type == typeof(RegisterWithWindowsHelloRequest) && _instance.OnRegisterWithWindowsHelloRequestEvent != null)
				{
					_instance.OnRegisterWithWindowsHelloRequestEvent((RegisterWithWindowsHelloRequest)e.Request);
				}
				else if (type == typeof(RemoveContactEmailRequest) && _instance.OnRemoveContactEmailRequestEvent != null)
				{
					_instance.OnRemoveContactEmailRequestEvent((RemoveContactEmailRequest)e.Request);
				}
				else if (type == typeof(RemoveFriendRequest) && _instance.OnRemoveFriendRequestEvent != null)
				{
					_instance.OnRemoveFriendRequestEvent((RemoveFriendRequest)e.Request);
				}
				else if (type == typeof(RemoveGenericIDRequest) && _instance.OnRemoveGenericIDRequestEvent != null)
				{
					_instance.OnRemoveGenericIDRequestEvent((RemoveGenericIDRequest)e.Request);
				}
				else if (type == typeof(RemoveSharedGroupMembersRequest) && _instance.OnRemoveSharedGroupMembersRequestEvent != null)
				{
					_instance.OnRemoveSharedGroupMembersRequestEvent((RemoveSharedGroupMembersRequest)e.Request);
				}
				else if (type == typeof(DeviceInfoRequest) && _instance.OnReportDeviceInfoRequestEvent != null)
				{
					_instance.OnReportDeviceInfoRequestEvent((DeviceInfoRequest)e.Request);
				}
				else if (type == typeof(ReportPlayerClientRequest) && _instance.OnReportPlayerRequestEvent != null)
				{
					_instance.OnReportPlayerRequestEvent((ReportPlayerClientRequest)e.Request);
				}
				else if (type == typeof(RestoreIOSPurchasesRequest) && _instance.OnRestoreIOSPurchasesRequestEvent != null)
				{
					_instance.OnRestoreIOSPurchasesRequestEvent((RestoreIOSPurchasesRequest)e.Request);
				}
				else if (type == typeof(SendAccountRecoveryEmailRequest) && _instance.OnSendAccountRecoveryEmailRequestEvent != null)
				{
					_instance.OnSendAccountRecoveryEmailRequestEvent((SendAccountRecoveryEmailRequest)e.Request);
				}
				else if (type == typeof(SetFriendTagsRequest) && _instance.OnSetFriendTagsRequestEvent != null)
				{
					_instance.OnSetFriendTagsRequestEvent((SetFriendTagsRequest)e.Request);
				}
				else if (type == typeof(SetPlayerSecretRequest) && _instance.OnSetPlayerSecretRequestEvent != null)
				{
					_instance.OnSetPlayerSecretRequestEvent((SetPlayerSecretRequest)e.Request);
				}
				else if (type == typeof(StartGameRequest) && _instance.OnStartGameRequestEvent != null)
				{
					_instance.OnStartGameRequestEvent((StartGameRequest)e.Request);
				}
				else if (type == typeof(StartPurchaseRequest) && _instance.OnStartPurchaseRequestEvent != null)
				{
					_instance.OnStartPurchaseRequestEvent((StartPurchaseRequest)e.Request);
				}
				else if (type == typeof(SubtractUserVirtualCurrencyRequest) && _instance.OnSubtractUserVirtualCurrencyRequestEvent != null)
				{
					_instance.OnSubtractUserVirtualCurrencyRequestEvent((SubtractUserVirtualCurrencyRequest)e.Request);
				}
				else if (type == typeof(UnlinkAndroidDeviceIDRequest) && _instance.OnUnlinkAndroidDeviceIDRequestEvent != null)
				{
					_instance.OnUnlinkAndroidDeviceIDRequestEvent((UnlinkAndroidDeviceIDRequest)e.Request);
				}
				else if (type == typeof(UnlinkCustomIDRequest) && _instance.OnUnlinkCustomIDRequestEvent != null)
				{
					_instance.OnUnlinkCustomIDRequestEvent((UnlinkCustomIDRequest)e.Request);
				}
				else if (type == typeof(UnlinkFacebookAccountRequest) && _instance.OnUnlinkFacebookAccountRequestEvent != null)
				{
					_instance.OnUnlinkFacebookAccountRequestEvent((UnlinkFacebookAccountRequest)e.Request);
				}
				else if (type == typeof(UnlinkGameCenterAccountRequest) && _instance.OnUnlinkGameCenterAccountRequestEvent != null)
				{
					_instance.OnUnlinkGameCenterAccountRequestEvent((UnlinkGameCenterAccountRequest)e.Request);
				}
				else if (type == typeof(UnlinkGoogleAccountRequest) && _instance.OnUnlinkGoogleAccountRequestEvent != null)
				{
					_instance.OnUnlinkGoogleAccountRequestEvent((UnlinkGoogleAccountRequest)e.Request);
				}
				else if (type == typeof(UnlinkIOSDeviceIDRequest) && _instance.OnUnlinkIOSDeviceIDRequestEvent != null)
				{
					_instance.OnUnlinkIOSDeviceIDRequestEvent((UnlinkIOSDeviceIDRequest)e.Request);
				}
				else if (type == typeof(UnlinkKongregateAccountRequest) && _instance.OnUnlinkKongregateRequestEvent != null)
				{
					_instance.OnUnlinkKongregateRequestEvent((UnlinkKongregateAccountRequest)e.Request);
				}
				else if (type == typeof(UnlinkSteamAccountRequest) && _instance.OnUnlinkSteamAccountRequestEvent != null)
				{
					_instance.OnUnlinkSteamAccountRequestEvent((UnlinkSteamAccountRequest)e.Request);
				}
				else if (type == typeof(UnlinkTwitchAccountRequest) && _instance.OnUnlinkTwitchRequestEvent != null)
				{
					_instance.OnUnlinkTwitchRequestEvent((UnlinkTwitchAccountRequest)e.Request);
				}
				else if (type == typeof(UnlinkWindowsHelloAccountRequest) && _instance.OnUnlinkWindowsHelloRequestEvent != null)
				{
					_instance.OnUnlinkWindowsHelloRequestEvent((UnlinkWindowsHelloAccountRequest)e.Request);
				}
				else if (type == typeof(UnlockContainerInstanceRequest) && _instance.OnUnlockContainerInstanceRequestEvent != null)
				{
					_instance.OnUnlockContainerInstanceRequestEvent((UnlockContainerInstanceRequest)e.Request);
				}
				else if (type == typeof(UnlockContainerItemRequest) && _instance.OnUnlockContainerItemRequestEvent != null)
				{
					_instance.OnUnlockContainerItemRequestEvent((UnlockContainerItemRequest)e.Request);
				}
				else if (type == typeof(UpdateAvatarUrlRequest) && _instance.OnUpdateAvatarUrlRequestEvent != null)
				{
					_instance.OnUpdateAvatarUrlRequestEvent((UpdateAvatarUrlRequest)e.Request);
				}
				else if (type == typeof(UpdateCharacterDataRequest) && _instance.OnUpdateCharacterDataRequestEvent != null)
				{
					_instance.OnUpdateCharacterDataRequestEvent((UpdateCharacterDataRequest)e.Request);
				}
				else if (type == typeof(UpdateCharacterStatisticsRequest) && _instance.OnUpdateCharacterStatisticsRequestEvent != null)
				{
					_instance.OnUpdateCharacterStatisticsRequestEvent((UpdateCharacterStatisticsRequest)e.Request);
				}
				else if (type == typeof(UpdatePlayerStatisticsRequest) && _instance.OnUpdatePlayerStatisticsRequestEvent != null)
				{
					_instance.OnUpdatePlayerStatisticsRequestEvent((UpdatePlayerStatisticsRequest)e.Request);
				}
				else if (type == typeof(UpdateSharedGroupDataRequest) && _instance.OnUpdateSharedGroupDataRequestEvent != null)
				{
					_instance.OnUpdateSharedGroupDataRequestEvent((UpdateSharedGroupDataRequest)e.Request);
				}
				else if (type == typeof(UpdateUserDataRequest) && _instance.OnUpdateUserDataRequestEvent != null)
				{
					_instance.OnUpdateUserDataRequestEvent((UpdateUserDataRequest)e.Request);
				}
				else if (type == typeof(UpdateUserDataRequest) && _instance.OnUpdateUserPublisherDataRequestEvent != null)
				{
					_instance.OnUpdateUserPublisherDataRequestEvent((UpdateUserDataRequest)e.Request);
				}
				else if (type == typeof(UpdateUserTitleDisplayNameRequest) && _instance.OnUpdateUserTitleDisplayNameRequestEvent != null)
				{
					_instance.OnUpdateUserTitleDisplayNameRequestEvent((UpdateUserTitleDisplayNameRequest)e.Request);
				}
				else if (type == typeof(ValidateAmazonReceiptRequest) && _instance.OnValidateAmazonIAPReceiptRequestEvent != null)
				{
					_instance.OnValidateAmazonIAPReceiptRequestEvent((ValidateAmazonReceiptRequest)e.Request);
				}
				else if (type == typeof(ValidateGooglePlayPurchaseRequest) && _instance.OnValidateGooglePlayPurchaseRequestEvent != null)
				{
					_instance.OnValidateGooglePlayPurchaseRequestEvent((ValidateGooglePlayPurchaseRequest)e.Request);
				}
				else if (type == typeof(ValidateIOSReceiptRequest) && _instance.OnValidateIOSReceiptRequestEvent != null)
				{
					_instance.OnValidateIOSReceiptRequestEvent((ValidateIOSReceiptRequest)e.Request);
				}
				else if (type == typeof(ValidateWindowsReceiptRequest) && _instance.OnValidateWindowsStoreReceiptRequestEvent != null)
				{
					_instance.OnValidateWindowsStoreReceiptRequestEvent((ValidateWindowsReceiptRequest)e.Request);
				}
				else if (type == typeof(WriteClientCharacterEventRequest) && _instance.OnWriteCharacterEventRequestEvent != null)
				{
					_instance.OnWriteCharacterEventRequestEvent((WriteClientCharacterEventRequest)e.Request);
				}
				else if (type == typeof(WriteClientPlayerEventRequest) && _instance.OnWritePlayerEventRequestEvent != null)
				{
					_instance.OnWritePlayerEventRequestEvent((WriteClientPlayerEventRequest)e.Request);
				}
				else if (type == typeof(WriteTitleEventRequest) && _instance.OnWriteTitleEventRequestEvent != null)
				{
					_instance.OnWriteTitleEventRequestEvent((WriteTitleEventRequest)e.Request);
				}
			}
			else
			{
				Type type2 = e.Result.GetType();
				if (type2 == typeof(LoginResult) && _instance.OnLoginResultEvent != null)
				{
					_instance.OnLoginResultEvent((LoginResult)e.Result);
				}
				else if (type2 == typeof(AcceptTradeResponse) && _instance.OnAcceptTradeResultEvent != null)
				{
					_instance.OnAcceptTradeResultEvent((AcceptTradeResponse)e.Result);
				}
				else if (type2 == typeof(AddFriendResult) && _instance.OnAddFriendResultEvent != null)
				{
					_instance.OnAddFriendResultEvent((AddFriendResult)e.Result);
				}
				else if (type2 == typeof(AddGenericIDResult) && _instance.OnAddGenericIDResultEvent != null)
				{
					_instance.OnAddGenericIDResultEvent((AddGenericIDResult)e.Result);
				}
				else if (type2 == typeof(AddOrUpdateContactEmailResult) && _instance.OnAddOrUpdateContactEmailResultEvent != null)
				{
					_instance.OnAddOrUpdateContactEmailResultEvent((AddOrUpdateContactEmailResult)e.Result);
				}
				else if (type2 == typeof(AddSharedGroupMembersResult) && _instance.OnAddSharedGroupMembersResultEvent != null)
				{
					_instance.OnAddSharedGroupMembersResultEvent((AddSharedGroupMembersResult)e.Result);
				}
				else if (type2 == typeof(AddUsernamePasswordResult) && _instance.OnAddUsernamePasswordResultEvent != null)
				{
					_instance.OnAddUsernamePasswordResultEvent((AddUsernamePasswordResult)e.Result);
				}
				else if (type2 == typeof(ModifyUserVirtualCurrencyResult) && _instance.OnAddUserVirtualCurrencyResultEvent != null)
				{
					_instance.OnAddUserVirtualCurrencyResultEvent((ModifyUserVirtualCurrencyResult)e.Result);
				}
				else if (type2 == typeof(AndroidDevicePushNotificationRegistrationResult) && _instance.OnAndroidDevicePushNotificationRegistrationResultEvent != null)
				{
					_instance.OnAndroidDevicePushNotificationRegistrationResultEvent((AndroidDevicePushNotificationRegistrationResult)e.Result);
				}
				else if (type2 == typeof(AttributeInstallResult) && _instance.OnAttributeInstallResultEvent != null)
				{
					_instance.OnAttributeInstallResultEvent((AttributeInstallResult)e.Result);
				}
				else if (type2 == typeof(CancelTradeResponse) && _instance.OnCancelTradeResultEvent != null)
				{
					_instance.OnCancelTradeResultEvent((CancelTradeResponse)e.Result);
				}
				else if (type2 == typeof(ConfirmPurchaseResult) && _instance.OnConfirmPurchaseResultEvent != null)
				{
					_instance.OnConfirmPurchaseResultEvent((ConfirmPurchaseResult)e.Result);
				}
				else if (type2 == typeof(ConsumeItemResult) && _instance.OnConsumeItemResultEvent != null)
				{
					_instance.OnConsumeItemResultEvent((ConsumeItemResult)e.Result);
				}
				else if (type2 == typeof(CreateSharedGroupResult) && _instance.OnCreateSharedGroupResultEvent != null)
				{
					_instance.OnCreateSharedGroupResultEvent((CreateSharedGroupResult)e.Result);
				}
				else if (type2 == typeof(ExecuteCloudScriptResult) && _instance.OnExecuteCloudScriptResultEvent != null)
				{
					_instance.OnExecuteCloudScriptResultEvent((ExecuteCloudScriptResult)e.Result);
				}
				else if (type2 == typeof(GetAccountInfoResult) && _instance.OnGetAccountInfoResultEvent != null)
				{
					_instance.OnGetAccountInfoResultEvent((GetAccountInfoResult)e.Result);
				}
				else if (type2 == typeof(ListUsersCharactersResult) && _instance.OnGetAllUsersCharactersResultEvent != null)
				{
					_instance.OnGetAllUsersCharactersResultEvent((ListUsersCharactersResult)e.Result);
				}
				else if (type2 == typeof(GetCatalogItemsResult) && _instance.OnGetCatalogItemsResultEvent != null)
				{
					_instance.OnGetCatalogItemsResultEvent((GetCatalogItemsResult)e.Result);
				}
				else if (type2 == typeof(GetCharacterDataResult) && _instance.OnGetCharacterDataResultEvent != null)
				{
					_instance.OnGetCharacterDataResultEvent((GetCharacterDataResult)e.Result);
				}
				else if (type2 == typeof(GetCharacterInventoryResult) && _instance.OnGetCharacterInventoryResultEvent != null)
				{
					_instance.OnGetCharacterInventoryResultEvent((GetCharacterInventoryResult)e.Result);
				}
				else if (type2 == typeof(GetCharacterLeaderboardResult) && _instance.OnGetCharacterLeaderboardResultEvent != null)
				{
					_instance.OnGetCharacterLeaderboardResultEvent((GetCharacterLeaderboardResult)e.Result);
				}
				else if (type2 == typeof(GetCharacterDataResult) && _instance.OnGetCharacterReadOnlyDataResultEvent != null)
				{
					_instance.OnGetCharacterReadOnlyDataResultEvent((GetCharacterDataResult)e.Result);
				}
				else if (type2 == typeof(GetCharacterStatisticsResult) && _instance.OnGetCharacterStatisticsResultEvent != null)
				{
					_instance.OnGetCharacterStatisticsResultEvent((GetCharacterStatisticsResult)e.Result);
				}
				else if (type2 == typeof(GetContentDownloadUrlResult) && _instance.OnGetContentDownloadUrlResultEvent != null)
				{
					_instance.OnGetContentDownloadUrlResultEvent((GetContentDownloadUrlResult)e.Result);
				}
				else if (type2 == typeof(CurrentGamesResult) && _instance.OnGetCurrentGamesResultEvent != null)
				{
					_instance.OnGetCurrentGamesResultEvent((CurrentGamesResult)e.Result);
				}
				else if (type2 == typeof(GetLeaderboardResult) && _instance.OnGetFriendLeaderboardResultEvent != null)
				{
					_instance.OnGetFriendLeaderboardResultEvent((GetLeaderboardResult)e.Result);
				}
				else if (type2 == typeof(GetFriendLeaderboardAroundPlayerResult) && _instance.OnGetFriendLeaderboardAroundPlayerResultEvent != null)
				{
					_instance.OnGetFriendLeaderboardAroundPlayerResultEvent((GetFriendLeaderboardAroundPlayerResult)e.Result);
				}
				else if (type2 == typeof(GetFriendsListResult) && _instance.OnGetFriendsListResultEvent != null)
				{
					_instance.OnGetFriendsListResultEvent((GetFriendsListResult)e.Result);
				}
				else if (type2 == typeof(GameServerRegionsResult) && _instance.OnGetGameServerRegionsResultEvent != null)
				{
					_instance.OnGetGameServerRegionsResultEvent((GameServerRegionsResult)e.Result);
				}
				else if (type2 == typeof(GetLeaderboardResult) && _instance.OnGetLeaderboardResultEvent != null)
				{
					_instance.OnGetLeaderboardResultEvent((GetLeaderboardResult)e.Result);
				}
				else if (type2 == typeof(GetLeaderboardAroundCharacterResult) && _instance.OnGetLeaderboardAroundCharacterResultEvent != null)
				{
					_instance.OnGetLeaderboardAroundCharacterResultEvent((GetLeaderboardAroundCharacterResult)e.Result);
				}
				else if (type2 == typeof(GetLeaderboardAroundPlayerResult) && _instance.OnGetLeaderboardAroundPlayerResultEvent != null)
				{
					_instance.OnGetLeaderboardAroundPlayerResultEvent((GetLeaderboardAroundPlayerResult)e.Result);
				}
				else if (type2 == typeof(GetLeaderboardForUsersCharactersResult) && _instance.OnGetLeaderboardForUserCharactersResultEvent != null)
				{
					_instance.OnGetLeaderboardForUserCharactersResultEvent((GetLeaderboardForUsersCharactersResult)e.Result);
				}
				else if (type2 == typeof(GetPaymentTokenResult) && _instance.OnGetPaymentTokenResultEvent != null)
				{
					_instance.OnGetPaymentTokenResultEvent((GetPaymentTokenResult)e.Result);
				}
				else if (type2 == typeof(GetPhotonAuthenticationTokenResult) && _instance.OnGetPhotonAuthenticationTokenResultEvent != null)
				{
					_instance.OnGetPhotonAuthenticationTokenResultEvent((GetPhotonAuthenticationTokenResult)e.Result);
				}
				else if (type2 == typeof(GetPlayerCombinedInfoResult) && _instance.OnGetPlayerCombinedInfoResultEvent != null)
				{
					_instance.OnGetPlayerCombinedInfoResultEvent((GetPlayerCombinedInfoResult)e.Result);
				}
				else if (type2 == typeof(GetPlayerProfileResult) && _instance.OnGetPlayerProfileResultEvent != null)
				{
					_instance.OnGetPlayerProfileResultEvent((GetPlayerProfileResult)e.Result);
				}
				else if (type2 == typeof(GetPlayerSegmentsResult) && _instance.OnGetPlayerSegmentsResultEvent != null)
				{
					_instance.OnGetPlayerSegmentsResultEvent((GetPlayerSegmentsResult)e.Result);
				}
				else if (type2 == typeof(GetPlayerStatisticsResult) && _instance.OnGetPlayerStatisticsResultEvent != null)
				{
					_instance.OnGetPlayerStatisticsResultEvent((GetPlayerStatisticsResult)e.Result);
				}
				else if (type2 == typeof(GetPlayerStatisticVersionsResult) && _instance.OnGetPlayerStatisticVersionsResultEvent != null)
				{
					_instance.OnGetPlayerStatisticVersionsResultEvent((GetPlayerStatisticVersionsResult)e.Result);
				}
				else if (type2 == typeof(GetPlayerTagsResult) && _instance.OnGetPlayerTagsResultEvent != null)
				{
					_instance.OnGetPlayerTagsResultEvent((GetPlayerTagsResult)e.Result);
				}
				else if (type2 == typeof(GetPlayerTradesResponse) && _instance.OnGetPlayerTradesResultEvent != null)
				{
					_instance.OnGetPlayerTradesResultEvent((GetPlayerTradesResponse)e.Result);
				}
				else if (type2 == typeof(GetPlayFabIDsFromFacebookIDsResult) && _instance.OnGetPlayFabIDsFromFacebookIDsResultEvent != null)
				{
					_instance.OnGetPlayFabIDsFromFacebookIDsResultEvent((GetPlayFabIDsFromFacebookIDsResult)e.Result);
				}
				else if (type2 == typeof(GetPlayFabIDsFromGameCenterIDsResult) && _instance.OnGetPlayFabIDsFromGameCenterIDsResultEvent != null)
				{
					_instance.OnGetPlayFabIDsFromGameCenterIDsResultEvent((GetPlayFabIDsFromGameCenterIDsResult)e.Result);
				}
				else if (type2 == typeof(GetPlayFabIDsFromGenericIDsResult) && _instance.OnGetPlayFabIDsFromGenericIDsResultEvent != null)
				{
					_instance.OnGetPlayFabIDsFromGenericIDsResultEvent((GetPlayFabIDsFromGenericIDsResult)e.Result);
				}
				else if (type2 == typeof(GetPlayFabIDsFromGoogleIDsResult) && _instance.OnGetPlayFabIDsFromGoogleIDsResultEvent != null)
				{
					_instance.OnGetPlayFabIDsFromGoogleIDsResultEvent((GetPlayFabIDsFromGoogleIDsResult)e.Result);
				}
				else if (type2 == typeof(GetPlayFabIDsFromKongregateIDsResult) && _instance.OnGetPlayFabIDsFromKongregateIDsResultEvent != null)
				{
					_instance.OnGetPlayFabIDsFromKongregateIDsResultEvent((GetPlayFabIDsFromKongregateIDsResult)e.Result);
				}
				else if (type2 == typeof(GetPlayFabIDsFromSteamIDsResult) && _instance.OnGetPlayFabIDsFromSteamIDsResultEvent != null)
				{
					_instance.OnGetPlayFabIDsFromSteamIDsResultEvent((GetPlayFabIDsFromSteamIDsResult)e.Result);
				}
				else if (type2 == typeof(GetPlayFabIDsFromTwitchIDsResult) && _instance.OnGetPlayFabIDsFromTwitchIDsResultEvent != null)
				{
					_instance.OnGetPlayFabIDsFromTwitchIDsResultEvent((GetPlayFabIDsFromTwitchIDsResult)e.Result);
				}
				else if (type2 == typeof(GetPublisherDataResult) && _instance.OnGetPublisherDataResultEvent != null)
				{
					_instance.OnGetPublisherDataResultEvent((GetPublisherDataResult)e.Result);
				}
				else if (type2 == typeof(GetPurchaseResult) && _instance.OnGetPurchaseResultEvent != null)
				{
					_instance.OnGetPurchaseResultEvent((GetPurchaseResult)e.Result);
				}
				else if (type2 == typeof(GetSharedGroupDataResult) && _instance.OnGetSharedGroupDataResultEvent != null)
				{
					_instance.OnGetSharedGroupDataResultEvent((GetSharedGroupDataResult)e.Result);
				}
				else if (type2 == typeof(GetStoreItemsResult) && _instance.OnGetStoreItemsResultEvent != null)
				{
					_instance.OnGetStoreItemsResultEvent((GetStoreItemsResult)e.Result);
				}
				else if (type2 == typeof(GetTimeResult) && _instance.OnGetTimeResultEvent != null)
				{
					_instance.OnGetTimeResultEvent((GetTimeResult)e.Result);
				}
				else if (type2 == typeof(GetTitleDataResult) && _instance.OnGetTitleDataResultEvent != null)
				{
					_instance.OnGetTitleDataResultEvent((GetTitleDataResult)e.Result);
				}
				else if (type2 == typeof(GetTitleNewsResult) && _instance.OnGetTitleNewsResultEvent != null)
				{
					_instance.OnGetTitleNewsResultEvent((GetTitleNewsResult)e.Result);
				}
				else if (type2 == typeof(GetTitlePublicKeyResult) && _instance.OnGetTitlePublicKeyResultEvent != null)
				{
					_instance.OnGetTitlePublicKeyResultEvent((GetTitlePublicKeyResult)e.Result);
				}
				else if (type2 == typeof(GetTradeStatusResponse) && _instance.OnGetTradeStatusResultEvent != null)
				{
					_instance.OnGetTradeStatusResultEvent((GetTradeStatusResponse)e.Result);
				}
				else if (type2 == typeof(GetUserDataResult) && _instance.OnGetUserDataResultEvent != null)
				{
					_instance.OnGetUserDataResultEvent((GetUserDataResult)e.Result);
				}
				else if (type2 == typeof(GetUserInventoryResult) && _instance.OnGetUserInventoryResultEvent != null)
				{
					_instance.OnGetUserInventoryResultEvent((GetUserInventoryResult)e.Result);
				}
				else if (type2 == typeof(GetUserDataResult) && _instance.OnGetUserPublisherDataResultEvent != null)
				{
					_instance.OnGetUserPublisherDataResultEvent((GetUserDataResult)e.Result);
				}
				else if (type2 == typeof(GetUserDataResult) && _instance.OnGetUserPublisherReadOnlyDataResultEvent != null)
				{
					_instance.OnGetUserPublisherReadOnlyDataResultEvent((GetUserDataResult)e.Result);
				}
				else if (type2 == typeof(GetUserDataResult) && _instance.OnGetUserReadOnlyDataResultEvent != null)
				{
					_instance.OnGetUserReadOnlyDataResultEvent((GetUserDataResult)e.Result);
				}
				else if (type2 == typeof(GetWindowsHelloChallengeResponse) && _instance.OnGetWindowsHelloChallengeResultEvent != null)
				{
					_instance.OnGetWindowsHelloChallengeResultEvent((GetWindowsHelloChallengeResponse)e.Result);
				}
				else if (type2 == typeof(GrantCharacterToUserResult) && _instance.OnGrantCharacterToUserResultEvent != null)
				{
					_instance.OnGrantCharacterToUserResultEvent((GrantCharacterToUserResult)e.Result);
				}
				else if (type2 == typeof(LinkAndroidDeviceIDResult) && _instance.OnLinkAndroidDeviceIDResultEvent != null)
				{
					_instance.OnLinkAndroidDeviceIDResultEvent((LinkAndroidDeviceIDResult)e.Result);
				}
				else if (type2 == typeof(LinkCustomIDResult) && _instance.OnLinkCustomIDResultEvent != null)
				{
					_instance.OnLinkCustomIDResultEvent((LinkCustomIDResult)e.Result);
				}
				else if (type2 == typeof(LinkFacebookAccountResult) && _instance.OnLinkFacebookAccountResultEvent != null)
				{
					_instance.OnLinkFacebookAccountResultEvent((LinkFacebookAccountResult)e.Result);
				}
				else if (type2 == typeof(LinkGameCenterAccountResult) && _instance.OnLinkGameCenterAccountResultEvent != null)
				{
					_instance.OnLinkGameCenterAccountResultEvent((LinkGameCenterAccountResult)e.Result);
				}
				else if (type2 == typeof(LinkGoogleAccountResult) && _instance.OnLinkGoogleAccountResultEvent != null)
				{
					_instance.OnLinkGoogleAccountResultEvent((LinkGoogleAccountResult)e.Result);
				}
				else if (type2 == typeof(LinkIOSDeviceIDResult) && _instance.OnLinkIOSDeviceIDResultEvent != null)
				{
					_instance.OnLinkIOSDeviceIDResultEvent((LinkIOSDeviceIDResult)e.Result);
				}
				else if (type2 == typeof(LinkKongregateAccountResult) && _instance.OnLinkKongregateResultEvent != null)
				{
					_instance.OnLinkKongregateResultEvent((LinkKongregateAccountResult)e.Result);
				}
				else if (type2 == typeof(LinkSteamAccountResult) && _instance.OnLinkSteamAccountResultEvent != null)
				{
					_instance.OnLinkSteamAccountResultEvent((LinkSteamAccountResult)e.Result);
				}
				else if (type2 == typeof(LinkTwitchAccountResult) && _instance.OnLinkTwitchResultEvent != null)
				{
					_instance.OnLinkTwitchResultEvent((LinkTwitchAccountResult)e.Result);
				}
				else if (type2 == typeof(LinkWindowsHelloAccountResponse) && _instance.OnLinkWindowsHelloResultEvent != null)
				{
					_instance.OnLinkWindowsHelloResultEvent((LinkWindowsHelloAccountResponse)e.Result);
				}
				else if (type2 == typeof(MatchmakeResult) && _instance.OnMatchmakeResultEvent != null)
				{
					_instance.OnMatchmakeResultEvent((MatchmakeResult)e.Result);
				}
				else if (type2 == typeof(OpenTradeResponse) && _instance.OnOpenTradeResultEvent != null)
				{
					_instance.OnOpenTradeResultEvent((OpenTradeResponse)e.Result);
				}
				else if (type2 == typeof(PayForPurchaseResult) && _instance.OnPayForPurchaseResultEvent != null)
				{
					_instance.OnPayForPurchaseResultEvent((PayForPurchaseResult)e.Result);
				}
				else if (type2 == typeof(PurchaseItemResult) && _instance.OnPurchaseItemResultEvent != null)
				{
					_instance.OnPurchaseItemResultEvent((PurchaseItemResult)e.Result);
				}
				else if (type2 == typeof(RedeemCouponResult) && _instance.OnRedeemCouponResultEvent != null)
				{
					_instance.OnRedeemCouponResultEvent((RedeemCouponResult)e.Result);
				}
				else if (type2 == typeof(RegisterForIOSPushNotificationResult) && _instance.OnRegisterForIOSPushNotificationResultEvent != null)
				{
					_instance.OnRegisterForIOSPushNotificationResultEvent((RegisterForIOSPushNotificationResult)e.Result);
				}
				else if (type2 == typeof(RegisterPlayFabUserResult) && _instance.OnRegisterPlayFabUserResultEvent != null)
				{
					_instance.OnRegisterPlayFabUserResultEvent((RegisterPlayFabUserResult)e.Result);
				}
				else if (type2 == typeof(RemoveContactEmailResult) && _instance.OnRemoveContactEmailResultEvent != null)
				{
					_instance.OnRemoveContactEmailResultEvent((RemoveContactEmailResult)e.Result);
				}
				else if (type2 == typeof(RemoveFriendResult) && _instance.OnRemoveFriendResultEvent != null)
				{
					_instance.OnRemoveFriendResultEvent((RemoveFriendResult)e.Result);
				}
				else if (type2 == typeof(RemoveGenericIDResult) && _instance.OnRemoveGenericIDResultEvent != null)
				{
					_instance.OnRemoveGenericIDResultEvent((RemoveGenericIDResult)e.Result);
				}
				else if (type2 == typeof(RemoveSharedGroupMembersResult) && _instance.OnRemoveSharedGroupMembersResultEvent != null)
				{
					_instance.OnRemoveSharedGroupMembersResultEvent((RemoveSharedGroupMembersResult)e.Result);
				}
				else if (type2 == typeof(EmptyResult) && _instance.OnReportDeviceInfoResultEvent != null)
				{
					_instance.OnReportDeviceInfoResultEvent((EmptyResult)e.Result);
				}
				else if (type2 == typeof(ReportPlayerClientResult) && _instance.OnReportPlayerResultEvent != null)
				{
					_instance.OnReportPlayerResultEvent((ReportPlayerClientResult)e.Result);
				}
				else if (type2 == typeof(RestoreIOSPurchasesResult) && _instance.OnRestoreIOSPurchasesResultEvent != null)
				{
					_instance.OnRestoreIOSPurchasesResultEvent((RestoreIOSPurchasesResult)e.Result);
				}
				else if (type2 == typeof(SendAccountRecoveryEmailResult) && _instance.OnSendAccountRecoveryEmailResultEvent != null)
				{
					_instance.OnSendAccountRecoveryEmailResultEvent((SendAccountRecoveryEmailResult)e.Result);
				}
				else if (type2 == typeof(SetFriendTagsResult) && _instance.OnSetFriendTagsResultEvent != null)
				{
					_instance.OnSetFriendTagsResultEvent((SetFriendTagsResult)e.Result);
				}
				else if (type2 == typeof(SetPlayerSecretResult) && _instance.OnSetPlayerSecretResultEvent != null)
				{
					_instance.OnSetPlayerSecretResultEvent((SetPlayerSecretResult)e.Result);
				}
				else if (type2 == typeof(StartGameResult) && _instance.OnStartGameResultEvent != null)
				{
					_instance.OnStartGameResultEvent((StartGameResult)e.Result);
				}
				else if (type2 == typeof(StartPurchaseResult) && _instance.OnStartPurchaseResultEvent != null)
				{
					_instance.OnStartPurchaseResultEvent((StartPurchaseResult)e.Result);
				}
				else if (type2 == typeof(ModifyUserVirtualCurrencyResult) && _instance.OnSubtractUserVirtualCurrencyResultEvent != null)
				{
					_instance.OnSubtractUserVirtualCurrencyResultEvent((ModifyUserVirtualCurrencyResult)e.Result);
				}
				else if (type2 == typeof(UnlinkAndroidDeviceIDResult) && _instance.OnUnlinkAndroidDeviceIDResultEvent != null)
				{
					_instance.OnUnlinkAndroidDeviceIDResultEvent((UnlinkAndroidDeviceIDResult)e.Result);
				}
				else if (type2 == typeof(UnlinkCustomIDResult) && _instance.OnUnlinkCustomIDResultEvent != null)
				{
					_instance.OnUnlinkCustomIDResultEvent((UnlinkCustomIDResult)e.Result);
				}
				else if (type2 == typeof(UnlinkFacebookAccountResult) && _instance.OnUnlinkFacebookAccountResultEvent != null)
				{
					_instance.OnUnlinkFacebookAccountResultEvent((UnlinkFacebookAccountResult)e.Result);
				}
				else if (type2 == typeof(UnlinkGameCenterAccountResult) && _instance.OnUnlinkGameCenterAccountResultEvent != null)
				{
					_instance.OnUnlinkGameCenterAccountResultEvent((UnlinkGameCenterAccountResult)e.Result);
				}
				else if (type2 == typeof(UnlinkGoogleAccountResult) && _instance.OnUnlinkGoogleAccountResultEvent != null)
				{
					_instance.OnUnlinkGoogleAccountResultEvent((UnlinkGoogleAccountResult)e.Result);
				}
				else if (type2 == typeof(UnlinkIOSDeviceIDResult) && _instance.OnUnlinkIOSDeviceIDResultEvent != null)
				{
					_instance.OnUnlinkIOSDeviceIDResultEvent((UnlinkIOSDeviceIDResult)e.Result);
				}
				else if (type2 == typeof(UnlinkKongregateAccountResult) && _instance.OnUnlinkKongregateResultEvent != null)
				{
					_instance.OnUnlinkKongregateResultEvent((UnlinkKongregateAccountResult)e.Result);
				}
				else if (type2 == typeof(UnlinkSteamAccountResult) && _instance.OnUnlinkSteamAccountResultEvent != null)
				{
					_instance.OnUnlinkSteamAccountResultEvent((UnlinkSteamAccountResult)e.Result);
				}
				else if (type2 == typeof(UnlinkTwitchAccountResult) && _instance.OnUnlinkTwitchResultEvent != null)
				{
					_instance.OnUnlinkTwitchResultEvent((UnlinkTwitchAccountResult)e.Result);
				}
				else if (type2 == typeof(UnlinkWindowsHelloAccountResponse) && _instance.OnUnlinkWindowsHelloResultEvent != null)
				{
					_instance.OnUnlinkWindowsHelloResultEvent((UnlinkWindowsHelloAccountResponse)e.Result);
				}
				else if (type2 == typeof(UnlockContainerItemResult) && _instance.OnUnlockContainerInstanceResultEvent != null)
				{
					_instance.OnUnlockContainerInstanceResultEvent((UnlockContainerItemResult)e.Result);
				}
				else if (type2 == typeof(UnlockContainerItemResult) && _instance.OnUnlockContainerItemResultEvent != null)
				{
					_instance.OnUnlockContainerItemResultEvent((UnlockContainerItemResult)e.Result);
				}
				else if (type2 == typeof(EmptyResult) && _instance.OnUpdateAvatarUrlResultEvent != null)
				{
					_instance.OnUpdateAvatarUrlResultEvent((EmptyResult)e.Result);
				}
				else if (type2 == typeof(UpdateCharacterDataResult) && _instance.OnUpdateCharacterDataResultEvent != null)
				{
					_instance.OnUpdateCharacterDataResultEvent((UpdateCharacterDataResult)e.Result);
				}
				else if (type2 == typeof(UpdateCharacterStatisticsResult) && _instance.OnUpdateCharacterStatisticsResultEvent != null)
				{
					_instance.OnUpdateCharacterStatisticsResultEvent((UpdateCharacterStatisticsResult)e.Result);
				}
				else if (type2 == typeof(UpdatePlayerStatisticsResult) && _instance.OnUpdatePlayerStatisticsResultEvent != null)
				{
					_instance.OnUpdatePlayerStatisticsResultEvent((UpdatePlayerStatisticsResult)e.Result);
				}
				else if (type2 == typeof(UpdateSharedGroupDataResult) && _instance.OnUpdateSharedGroupDataResultEvent != null)
				{
					_instance.OnUpdateSharedGroupDataResultEvent((UpdateSharedGroupDataResult)e.Result);
				}
				else if (type2 == typeof(UpdateUserDataResult) && _instance.OnUpdateUserDataResultEvent != null)
				{
					_instance.OnUpdateUserDataResultEvent((UpdateUserDataResult)e.Result);
				}
				else if (type2 == typeof(UpdateUserDataResult) && _instance.OnUpdateUserPublisherDataResultEvent != null)
				{
					_instance.OnUpdateUserPublisherDataResultEvent((UpdateUserDataResult)e.Result);
				}
				else if (type2 == typeof(UpdateUserTitleDisplayNameResult) && _instance.OnUpdateUserTitleDisplayNameResultEvent != null)
				{
					_instance.OnUpdateUserTitleDisplayNameResultEvent((UpdateUserTitleDisplayNameResult)e.Result);
				}
				else if (type2 == typeof(ValidateAmazonReceiptResult) && _instance.OnValidateAmazonIAPReceiptResultEvent != null)
				{
					_instance.OnValidateAmazonIAPReceiptResultEvent((ValidateAmazonReceiptResult)e.Result);
				}
				else if (type2 == typeof(ValidateGooglePlayPurchaseResult) && _instance.OnValidateGooglePlayPurchaseResultEvent != null)
				{
					_instance.OnValidateGooglePlayPurchaseResultEvent((ValidateGooglePlayPurchaseResult)e.Result);
				}
				else if (type2 == typeof(ValidateIOSReceiptResult) && _instance.OnValidateIOSReceiptResultEvent != null)
				{
					_instance.OnValidateIOSReceiptResultEvent((ValidateIOSReceiptResult)e.Result);
				}
				else if (type2 == typeof(ValidateWindowsReceiptResult) && _instance.OnValidateWindowsStoreReceiptResultEvent != null)
				{
					_instance.OnValidateWindowsStoreReceiptResultEvent((ValidateWindowsReceiptResult)e.Result);
				}
				else if (type2 == typeof(WriteEventResponse) && _instance.OnWriteCharacterEventResultEvent != null)
				{
					_instance.OnWriteCharacterEventResultEvent((WriteEventResponse)e.Result);
				}
				else if (type2 == typeof(WriteEventResponse) && _instance.OnWritePlayerEventResultEvent != null)
				{
					_instance.OnWritePlayerEventResultEvent((WriteEventResponse)e.Result);
				}
				else if (type2 == typeof(WriteEventResponse) && _instance.OnWriteTitleEventResultEvent != null)
				{
					_instance.OnWriteTitleEventResultEvent((WriteEventResponse)e.Result);
				}
			}
		}
	}
}
