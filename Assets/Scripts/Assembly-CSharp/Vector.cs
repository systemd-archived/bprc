using System;
using System.Runtime.CompilerServices;
using UnityEngine;

public static class Vector
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float Length2(float valueX, float valueY)
	{
		return (float)Math.Sqrt(valueX * valueX + valueY * valueY);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float Length2(Vector2 value)
	{
		return (float)Math.Sqrt(value.x * value.x + value.y * value.y);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float Length2(Vector3 value)
	{
		return (float)Math.Sqrt(value.x * value.x + value.y * value.y);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float Length3(float valueX, float valueY, float valueZ)
	{
		return (float)Math.Sqrt(valueX * valueX + valueY * valueY + valueZ * valueZ);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float Length3(Vector3 value)
	{
		return (float)Math.Sqrt(value.x * value.x + value.y * value.y + value.z * value.z);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float LengthSquared2(float valueX, float valueY)
	{
		return valueX * valueX + valueY * valueY;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float LengthSquared2(Vector2 value)
	{
		return value.x * value.x + value.y * value.y;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float LengthSquared2(Vector3 value)
	{
		return value.x * value.x + value.y * value.y;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float LengthSquared3(float valueX, float valueY, float valueZ)
	{
		return valueX * valueX + valueY * valueY + valueZ * valueZ;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float LengthSquared3(Vector3 value)
	{
		return value.x * value.x + value.y * value.y + value.z * value.z;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float Distance2(float leftX, float leftY, float rightX, float rightY)
	{
		float num = leftX - rightX;
		float num2 = leftY - rightY;
		return (float)Math.Sqrt(num * num + num2 * num2);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float Distance2(Vector2 left, Vector2 right)
	{
		float num = left.x - right.x;
		float num2 = left.y - right.y;
		return (float)Math.Sqrt(num * num + num2 * num2);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float Distance2(Vector3 left, Vector3 right)
	{
		float num = left.x - right.x;
		float num2 = left.y - right.y;
		return (float)Math.Sqrt(num * num + num2 * num2);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float Distance3(float leftX, float leftY, float leftZ, float rightX, float rightY, float rightZ)
	{
		float num = leftX - rightX;
		float num2 = leftY - rightY;
		float num3 = leftZ - rightZ;
		return (float)Math.Sqrt(num * num + num2 * num2 + num3 * num3);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float Distance3(Vector3 left, Vector3 right)
	{
		float num = left.x - right.x;
		float num2 = left.y - right.y;
		float num3 = left.z - right.z;
		return (float)Math.Sqrt(num * num + num2 * num2 + num3 * num3);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float DistanceSquared2(float leftX, float leftY, float rightX, float rightY)
	{
		float num = leftX - rightX;
		float num2 = leftY - rightY;
		return num * num + num2 * num2;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float DistanceSquared2(Vector2 left, Vector2 right)
	{
		float num = left.x - right.x;
		float num2 = left.y - right.y;
		return num * num + num2 * num2;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float DistanceSquared2(Vector3 left, Vector3 right)
	{
		float num = left.x - right.x;
		float num2 = left.y - right.y;
		return num * num + num2 * num2;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float DistanceSquared3(float leftX, float leftY, float leftZ, float rightX, float rightY, float rightZ)
	{
		float num = leftX - rightX;
		float num2 = leftY - rightY;
		float num3 = leftZ - rightZ;
		return num * num + num2 * num2 + num3 * num3;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float DistanceSquared3(Vector3 left, Vector3 right)
	{
		float num = left.x - right.x;
		float num2 = left.y - right.y;
		float num3 = left.z - right.z;
		return num * num + num2 * num2 + num3 * num3;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float Dot2(float leftX, float leftY, float rightX, float rightY)
	{
		return leftX * rightX + leftY * rightY;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float Dot2(Vector2 left, Vector2 right)
	{
		return left.x * right.x + left.y * right.y;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float Dot2(Vector3 left, Vector3 right)
	{
		return left.x * right.x + left.y * right.y;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float Dot3(float leftX, float leftY, float leftZ, float rightX, float rightY, float rightZ)
	{
		return leftX * rightX + leftY * rightY + leftZ * rightZ;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float Dot3(Vector3 left, Vector3 right)
	{
		return left.x * right.x + left.y * right.y + left.z * right.z;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float Cross2(float leftX, float leftY, float rightX, float rightY)
	{
		return leftX * rightY - leftY * rightX;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float Cross2(Vector2 left, Vector2 right)
	{
		return left.x * right.y - left.y * right.x;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float Cross2(Vector3 left, Vector3 right)
	{
		return left.x * right.y - left.y * right.x;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void Cross3(float leftX, float leftY, float leftZ, float rightX, float rightY, float rightZ, out float resultX, out float resultY, out float resultZ)
	{
		resultX = leftY * rightZ - leftZ * rightY;
		resultY = leftZ * rightX - leftX * rightZ;
		resultZ = leftX * rightY - leftY * rightX;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Vector3 Cross3(Vector3 left, Vector3 right)
	{
		return new Vector3(left.y * right.z - left.z * right.y, left.z * right.x - left.x * right.z, left.x * right.y - left.y * right.x);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void Transform2(float valueX, float valueY, float directionX, float directionY, out float resultX, out float resultY)
	{
		resultX = directionX * valueX - directionY * valueY;
		resultY = directionY * valueX + directionX * valueY;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void Transform2(Vector2 value, Vector2 direction, out float resultX, out float resultY)
	{
		resultX = direction.x * value.x - direction.y * value.y;
		resultY = direction.y * value.x + direction.x * value.y;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Vector2 Transform2(Vector2 value, Vector2 direction)
	{
		return new Vector2(direction.x * value.x - direction.y * value.y, direction.y * value.x + direction.x * value.y);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void Transform2(Vector3 value, Vector3 direction, out float resultX, out float resultY)
	{
		resultX = direction.x * value.x - direction.y * value.y;
		resultY = direction.y * value.x + direction.x * value.y;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Vector3 Transform2(Vector3 value, Vector3 direction)
	{
		return new Vector3(direction.x * value.x - direction.y * value.y, direction.y * value.x + direction.x * value.y);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void InvTransform2(float valueX, float valueY, float directionX, float directionY, out float resultX, out float resultY)
	{
		resultX = directionX * valueX + directionY * valueY;
		resultY = (0f - directionY) * valueX + directionX * valueY;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void InvTransform2(Vector2 value, Vector2 direction, out float resultX, out float resultY)
	{
		resultX = direction.x * value.x + direction.y * value.y;
		resultY = (0f - direction.y) * value.x + direction.x * value.y;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Vector2 InvTransform2(Vector2 value, Vector2 direction)
	{
		return new Vector2(direction.x * value.x + direction.y * value.y, (0f - direction.y) * value.x + direction.x * value.y);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void InvTransform2(Vector3 value, Vector3 direction, out float resultX, out float resultY)
	{
		resultX = direction.x * value.x + direction.y * value.y;
		resultY = (0f - direction.y) * value.x + direction.x * value.y;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Vector3 InvTransform2(Vector3 value, Vector3 direction)
	{
		return new Vector3(direction.x * value.x + direction.y * value.y, (0f - direction.y) * value.x + direction.x * value.y);
	}
}
