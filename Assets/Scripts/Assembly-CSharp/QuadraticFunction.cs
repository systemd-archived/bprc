using System;

public struct QuadraticFunction
{
	public readonly struct IntersectResult
	{
		public readonly int Count;

		public readonly float X1;

		public readonly float X2;

		public static IntersectResult None => default(IntersectResult);

		public float this[int index]
		{
			get
			{
				if (index < 0 || index >= Count)
				{
					throw new ArgumentOutOfRangeException("index");
				}
				if (index != 0)
				{
					return X2;
				}
				return X1;
			}
		}

		public IntersectResult(float x)
			: this(1, x, 0f)
		{
		}

		public IntersectResult(float x1, float x2)
			: this(2, x1, x2)
		{
		}

		private IntersectResult(int count, float x1, float x2)
		{
			Count = count;
			X1 = x1;
			X2 = x2;
		}
	}

	public float A;

	public float B;

	public float C;

	public QuadraticFunction(float a, float b, float c)
	{
		A = a;
		B = b;
		C = c;
	}

	public float GetValue(float x)
	{
		return A * x * x + B * x + C;
	}

	public IntersectResult GetIntersections(float y)
	{
		return SolveEquation(A, B, C - y);
	}

	public float GetMinValue(float left, float right)
	{
		if (A > 0f)
		{
			float num = (0f - B) / (2f * A);
			if (left <= num && num < right)
			{
				return GetValue(num);
			}
		}
		return Math.Min(GetValue(left), GetValue(right));
	}

	public float GetMaxValue(float left, float right)
	{
		if (A < 0f)
		{
			float num = (0f - B) / (2f * A);
			if (left <= num && num < right)
			{
				return GetValue(num);
			}
		}
		return Math.Max(GetValue(left), GetValue(right));
	}

	public static IntersectResult SolveEquation(float a, float b, float c)
	{
		if (a == 0f)
		{
			if (b == 0f)
			{
				return IntersectResult.None;
			}
			return new IntersectResult((0f - c) / b);
		}
		float num = b * b - 4f * a * c;
		if (num > 0f)
		{
			float num2 = (float)Math.Sqrt(num);
			return new IntersectResult((0f - b - num2) / (2f * a), (0f - b + num2) / (2f * a));
		}
		if (num == 0f)
		{
			return new IntersectResult((0f - b) / (2f * a));
		}
		return IntersectResult.None;
	}
}
