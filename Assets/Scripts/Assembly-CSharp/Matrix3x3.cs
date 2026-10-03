using UnityEngine;

public struct Matrix3x3
{
	public float M11;

	public float M12;

	public float M13;

	public float M21;

	public float M22;

	public float M23;

	public float M31;

	public float M32;

	public float M33;

	public Matrix3x3(float m11, float m12, float m13, float m21, float m22, float m23, float m31, float m32, float m33)
	{
		M11 = m11;
		M12 = m12;
		M13 = m13;
		M21 = m21;
		M22 = m22;
		M23 = m23;
		M31 = m31;
		M32 = m32;
		M33 = m33;
	}

	public static float Determinant(Matrix3x3 matrix)
	{
		float m = matrix.M11;
		float m2 = matrix.M12;
		float m3 = matrix.M13;
		float m4 = matrix.M21;
		float m5 = matrix.M22;
		float m6 = matrix.M23;
		float m7 = matrix.M31;
		float m8 = matrix.M32;
		float m9 = matrix.M33;
		return m * (m5 * m9 - m6 * m8) + m2 * (m6 * m7 - m4 * m9) + m3 * (m4 * m8 - m5 * m7);
	}

	public static Matrix3x3 Invert(Matrix3x3 matrix)
	{
		float num = Determinant(matrix);
		if (num == 0f)
		{
			return new Matrix3x3(float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN);
		}
		_ = 1f / num;
		return default(Matrix3x3);
	}

	public static Vector3 Solve(Matrix3x3 a, Vector3 b)
	{
		float m = a.M11;
		float m2 = a.M12;
		float m3 = a.M13;
		float m4 = a.M21;
		float m5 = a.M22;
		float m6 = a.M23;
		float m7 = a.M31;
		float m8 = a.M32;
		float m9 = a.M33;
		float num = Determinant(a);
		if (num == 0f)
		{
			return new Vector2(float.NaN, float.NaN);
		}
		float num2 = 1f / num;
		float num3 = Determinant(new Matrix3x3(b.x, m2, m3, b.y, m5, m6, b.z, m8, m9));
		float num4 = Determinant(new Matrix3x3(m, b.x, m3, m4, b.y, m6, m7, b.z, m9));
		float num5 = Determinant(new Matrix3x3(m, m2, b.x, m4, m5, b.y, m7, m8, b.z));
		return new Vector3(num2 * num3, num2 * num4, num2 * num5);
	}
}
