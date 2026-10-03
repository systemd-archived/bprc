public struct UIEvent : EventManager.Event
{
	public enum Type
	{
		None = 0,
		Building = 1,
		Play = 2,
		LevelSelection = 3,
		NextLevel = 4,
		Home = 5,
		Preview = 6,
		Clear = 7,
		Pause = 8,
		Blueprint = 9,
		ReplayLevel = 10,
		ActivateRockets = 11,
		ActivateEngines = 12,
		BackFromPreview = 13,
		ContinueFromPause = 14,
		ReplayFlight = 15,
		IapPurchaseCurrency = 16,
		IapPurchaseRocket = 17,
		IapPurchaseEngine = 18,
		OpenIapMenu = 19,
		CloseIapMenu = 20,
		QuestModeBuild = 21,
		MoveContraptionLeft = 22,
		MoveContraptionRight = 23,
		MoveContraptionUp = 24,
		MoveContraptionDown = 25,
		OpenTutorial = 26,
		CloseTutorial = 27,
		Snapshot = 28,
		CloseMechanicInfo = 29,
		CloseMechanicInfoAndUseMechanic = 30,
		OpenUnlockFullVersionIapMenu = 31,
		CloseUnlockFullVersionIapMenu = 32,
		EpisodeSelection = 33,
		ApplySuperGlue = 34,
		ApplySuperMagnet = 35,
		ApplyTurboCharge = 36,
		SuperBlueprint = 37,
		RotateSuperBluePrints = 38,
		CloseMechanicInfoAndUseSuperMechanic = 39,
		LoadContraptionSlot1 = 40,
		LoadContraptionSlot2 = 41,
		LoadContraptionSlot3 = 42,
		UnlockNextLevelWithVideoAd = 43,
		ApplyNightVision = 44,
		OpenedLootWheel = 45,
		ClosedLootWheel = 46,
		OpenedSnoutCoinShop = 47,
		ClosedSnoutCoinShop = 48,
		OpenedLootCrateDialog = 49,
		ClosedLootCrateDialog = 50,
		CakeRaceTimerStarted = 51,
		OpenedMainMenuPromo = 52,
		ClosedMainMenuPromo = 53,
		OpenedPurchaseConfirmation = 54,
		ClosedPurchaseConfirmation = 55,
		OpenedNotificationPopup = 56,
		ClosedNotificationPopup = 57,
		ShopLockedScreen = 58,
		ShopUnlockedScreen = 59,
		OpenedCakeRaceUnlockedPopup = 60,
		ClosedCakeRaceUnlockedPopup = 61,
		OpenedDailyChallengeDialog = 62,
		ClosedDailyChallengeDialog = 63,
		OpenedWorkshopIntroduction = 64,
		ClosedWorkshopIntroduction = 65,
		OpenedPurchaseRoadHogsParts = 66,
		ClosedPurchaseRoadHogsParts = 67,
		LoadContraptionSlot4 = 68,
		LoadContraptionSlot5 = 69,
		LoadContraptionSlot6 = 70,
		LoadContraptionSlot7 = 71,
		LoadContraptionSlot8 = 72,
		SaveContraption = 73,
		ToolBookCopy = 74,
		ToolBookPaste = 75,
		ToolBookQuickPlace = 76,
		ToolBookOpenTutorial = 77,
		ToolBookSelect = 78,
		ToolBookUndo = 79,
		ToolBookPasteConfirm = 80,
		ToolBookPasteCancel = 81,
		ToolBookSaveClipboard = 82,
		ToolBookDeleteClipboard = 83
	}

	// 载具存档槽 4..N 的按钮是在运行时动态生成的（见 SchematicButton.Awake）。
	// 它们的 UIEvent.Type 从这个基址开始编号，避免与上面固定的枚举值撞车
	// （例如槽位9旧编号 73 = SaveContraption、槽位10旧编号 74 = ToolBookCopy，
	// 会让"存档/复制"按钮被当成"载入存档9/10"）。
	public const int ExtendedContraptionSlotTypeBase = 100;

	public Type type;

	// Keep the original eight serialized button values; reserve a separate range for extra slots.
	public static Type GetSlotEvent(int slotIndex)
	{
		if (slotIndex < 0) throw new System.ArgumentOutOfRangeException(nameof(slotIndex));
		return (Type)(slotIndex < 3 ? 40 + slotIndex : slotIndex < 8 ? 65 + slotIndex : 10000 + slotIndex);
	}

	public static int GetSlotIndex(Type eventType)
	{
		int value = (int)eventType;
		if (value >= 40 && value <= 42) return value - 40;
		if (value >= 68 && value <= 72) return value - 65;
		return value >= 10008 ? value - 10000 : -1;
	}

	public UIEvent(Type type)
	{
		this.type = type;
	}
}
