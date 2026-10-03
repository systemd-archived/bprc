using UnityEngine;

public class BoxingGlove : MonoBehaviour
{
	private static bool suckerPunchReported;

	public void OnCollisionEnter(UnityEngine.Collision coll) { OnPhysicsCollisionEnter(new PhysicsCollision(coll)); }

	private void OnPhysicsCollisionEnter(PhysicsCollision coll)
	{
		if (!suckerPunchReported && Singleton<SocialGameManager>.IsInstantiated() && (bool)coll.collider.gameObject.GetComponent<KingPig>())
		{
			suckerPunchReported = true;
			Singleton<SocialGameManager>.Instance.ReportAchievementProgress("grp.SUCKER_PUNCH", 100.0);
		}
	}
}
