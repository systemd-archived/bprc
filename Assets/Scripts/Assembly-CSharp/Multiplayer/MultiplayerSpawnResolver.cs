using UnityEngine;

namespace BadPiggies.Multiplayer
{
/// <summary>
/// Where each player's machine starts.
///
/// Slot 0 keeps the level's designed starting position. The remaining slots are spread across the
/// arena towards the position mirrored about the arena centre, so that two players are placed at the
/// two ends (exactly as the original duel layout did) and three or more players fan out evenly
/// between them. Every peer resolves the same slot to the same point, because the only inputs are
/// the level geometry and the slot index.
/// </summary>
public static class MultiplayerSpawnResolver
{
	private const float GroundProbeDistance = 100f;

	private const float GroundClearance = 1.1f;

	private const float CellClearanceRadius = 0.55f;

	private const int MaxSlideCells = 40;

	/// <summary>
	/// True when this lane's machine is built mirrored (and therefore drives with the opposite sign
	/// so its own controls keep pointing the way it faces). Lanes in the right half of the arena are
	/// mirrored, which for a two-player match is exactly lane 1 — the original behaviour.
	/// </summary>
	public static bool IsMirroredLane(int lane, int playerCount)
	{
		if (playerCount <= 1)
		{
			return false;
		}
		return lane * 2 > playerCount - 1;
	}

	/// <summary>
	/// <paramref name="lane"/> is the machine's position in the round's ordered player list (0 is the
	/// leftmost), not its network slot: slots stay stable while players come and go, lanes are
	/// re-derived from the players actually in the round.
	/// </summary>
	public static Vector3 Resolve(LevelManager levelManager, int lane, int playerCount)
	{
		if (levelManager == null)
		{
			Debug.LogError("[Multiplayer] No LevelManager; cannot resolve a spawn position.");
			return Vector3.zero;
		}
		Vector3 startingPosition = levelManager.StartingPosition;
		if (playerCount <= 1)
		{
			return startingPosition;
		}
		int index = Mathf.Clamp(lane, 0, playerCount - 1);
		if (index == 0)
		{
			return startingPosition;
		}
		float x = MirrorAcrossArena(levelManager, startingPosition.x);
		if (index < playerCount - 1)
		{
			float t = (float)index / (float)(playerCount - 1);
			x = Mathf.Lerp(startingPosition.x, x, t);
		}
		return SettleOnGround(levelManager, new Vector3(x, startingPosition.y, 0f), startingPosition.y, lane);
	}

	public static float MirrorAcrossArena(LevelManager levelManager, float x)
	{
		LevelManager.CameraLimits cameraLimits = levelManager.m_cameraLimits;
		if (cameraLimits == null || cameraLimits.size.x <= 0f)
		{
			Debug.LogWarning("[Multiplayer] Level declares no camera limits; mirroring the far slot about x = 0.");
			return 0f - x;
		}
		float x2 = cameraLimits.topLeft.x;
		float num = cameraLimits.topLeft.x + cameraLimits.size.x;
		return x2 + num - x;
	}

	private static Vector3 SettleOnGround(LevelManager levelManager, Vector3 candidate, float fallbackY, int lane)
	{
		int num = 1 << LayerMask.NameToLayer("Ground");
		int gridXMin = levelManager.GridXMin;
		int gridXMax = levelManager.GridXMax;
		int gridHeight = levelManager.GridHeight;
		int num2 = -1;
		Vector3 result = candidate;
		result.y = fallbackY;
		for (int i = 0; i <= 80; i++)
		{
			int num3 = (i + 1) / 2;
			if ((i & 1) == 1)
			{
				num3 = -num3;
			}
			Vector3 vector = new Vector3(candidate.x + (float)num3, candidate.y, 0f);
			Vector3 origin = new Vector3(vector.x, vector.y + 50f, 0f);
			if (Physics.Raycast(new Ray(origin, Vector3.down), out var hitInfo, 100f, num))
			{
				Vector3 vector2 = new Vector3(vector.x, origin.y - hitInfo.distance + 1.1f, 0f);
				int num4 = ScoreColumn(vector2, gridXMin, gridXMax, gridHeight, num);
				if (num4 > num2)
				{
					num2 = num4;
					result = vector2;
				}
				int num5 = (gridXMax - gridXMin + 1) * gridHeight;
				if (num4 >= num5)
				{
					break;
				}
			}
		}
		if (num2 < 0)
		{
			Debug.LogWarning($"[Multiplayer] Found no ground near x={candidate.x:F1} for lane {lane}; " +
				"using the computed point unmodified.");
			return new Vector3(candidate.x, fallbackY, 0f);
		}
		return result;
	}

	private static int ScoreColumn(Vector3 origin, int gridXMin, int gridXMax, int gridHeight, int groundMask)
	{
		int num = 0;
		for (int i = gridXMin; i <= gridXMax; i++)
		{
			for (int j = 0; j < gridHeight; j++)
			{
				if (!Physics.CheckSphere(origin + new Vector3(i, j, 0f), 0.55f, groundMask))
				{
					num++;
				}
			}
		}
		return num;
	}
}
}
