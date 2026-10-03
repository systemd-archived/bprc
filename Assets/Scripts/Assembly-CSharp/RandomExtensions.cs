using System;

public static class RandomExtensions
{
	public static double NextDouble(this Random random, double max)
	{
		return random.NextDouble() * max;
	}

	public static double NextDouble(this Random random, double min, double max)
	{
		return random.NextDouble() * (max - min) + min;
	}

	public static float NextSingle(this Random random)
	{
		return (float)random.NextDouble();
	}

	public static float NextSingle(this Random random, float max)
	{
		return (float)random.NextDouble() * max;
	}

	public static float NextSingle(this Random random, float min, float max)
	{
		return (float)random.NextDouble() * (max - min) + min;
	}
}
