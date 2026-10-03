using UnityEngine;

public struct INBounds
{
	public int Type;

	public float X;

	public float Y;

	public float A;

	public float B;

	public float R;

	public bool IsRect => Type == 0;

	public bool IsCircle => Type == 1;

	public float GetHalfProjection(float directionX, float directionY)
	{
		if (Type == 0)
		{
			return ((directionX > 0f) ? directionX : (0f - directionX)) * A + ((directionY > 0f) ? directionY : (0f - directionY)) * B;
		}
		return R;
	}

	public float GetHalfProjection(Vector2 direction)
	{
		return GetHalfProjection(direction.x, direction.y);
	}

	public void GetLocalContactPoint(float directionX, float directionY, out float pointX, out float pointY)
	{
		if (Type == 0)
		{
			pointX = ((directionY > 0f) ? (0f - A) : A);
			pointY = ((directionX > 0f) ? (0f - B) : B);
		}
		else
		{
			pointX = 0f;
			pointY = 0f - R;
		}
	}

	public void GetContactPoint(float directionX, float directionY, out float pointX, out float pointY)
	{
		if (Type == 0)
		{
			float num = ((directionY > 0f) ? (0f - A) : A);
			float num2 = ((directionX > 0f) ? (0f - B) : B);
			pointX = directionX * num - directionY * num2;
			pointY = directionY * num + directionX * num2;
		}
		else
		{
			pointX = 0f;
			pointY = 0f - R;
		}
	}

	public Vector2 GetContactPoint(Vector2 direction)
	{
		GetContactPoint(direction.x, direction.y, out var pointX, out var pointY);
		return new Vector2(pointX, pointY);
	}
}
