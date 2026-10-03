using UnityEngine;

public readonly struct EntityLightCollision
{
	public readonly Vector2 ContactPoint;

	public readonly Vector2 DeltaPosition;

	public readonly Vector2 DeltaVelocity;

	public EntityLightCollision(Vector2 contactPoint, Vector2 deltaPosition, Vector2 deltaVelocity)
	{
		ContactPoint = contactPoint;
		DeltaPosition = deltaPosition;
		DeltaVelocity = deltaVelocity;
	}
}
