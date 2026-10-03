public class Achievements : WPFMonoBehaviour
{
	public enum AchievementType
	{
		Time = 0,
		CoolPilot = 1,
		CrazyPilot = 2,
		SavedParts = 3,
		Eggs = 4
	}

	public static bool GetAchievementStatus(AchievementType achievement)
	{
		switch (achievement)
		{
		case AchievementType.Time:
			if (WPFMonoBehaviour.levelManager.TimeElapsed < WPFMonoBehaviour.levelManager.TimeLimit)
			{
				return true;
			}
			break;
		case AchievementType.CoolPilot:
			if ((float)WPFMonoBehaviour.levelManager.m_totalDestroyedParts / (float)WPFMonoBehaviour.levelManager.m_totalAvailableParts < 0.25f)
			{
				return true;
			}
			break;
		case AchievementType.CrazyPilot:
			if ((float)WPFMonoBehaviour.levelManager.m_totalDestroyedParts / (float)WPFMonoBehaviour.levelManager.m_totalAvailableParts > 0.5f)
			{
				return true;
			}
			break;
		}
		return false;
	}
}
