using UnityEngine;

public struct Matrix2x2
{
	public float M11;

	public float M12;

	public float M21;

	public float M22;

	public Matrix2x2(float m11, float m12, float m21, float m22)
	{
		M11 = m11;
		M12 = m12;
		M21 = m21;
		M22 = m22;
	}

	public static float Determinant(Matrix2x2 matrix)
	{
		float m = matrix.M11;
		float m2 = matrix.M12;
		float m3 = matrix.M21;
		float m4 = matrix.M22;
		return m * m4 - m2 * m3;
	}

	public static Matrix2x2 Invert(Matrix2x2 matrix)
	{
		float m = matrix.M11;
		float m2 = matrix.M12;
		float m3 = matrix.M21;
		float m4 = matrix.M22;
		float num = m * m4 - m2 * m3;
		if (num == 0f)
		{
			return new Matrix2x2(float.NaN, float.NaN, float.NaN, float.NaN);
		}
		float num2 = 1f / num;
		return new Matrix2x2(m4 * num2, (0f - m2) * num2, (0f - m3) * num2, m * num2);
	}

	public static Vector2 Solve(Matrix2x2 a, Vector2 b)
	{
		float m = a.M11;
		float m2 = a.M12;
		float m3 = a.M21;
		float m4 = a.M22;
		float num = m * m4 - m2 * m3;
		if (num == 0f)
		{
			return new Vector2(float.NaN, float.NaN);
		}
		float num2 = 1f / num;
		return new Vector2(num2 * (m4 * b.x - m2 * b.y), num2 * (m * b.y - m3 * b.x));
	}
}
