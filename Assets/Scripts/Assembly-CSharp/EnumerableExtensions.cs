using System.Collections.Generic;

public static class EnumerableExtensions
{
	public static IEnumerable<T> ToEnumerable<T>(this T item)
	{
		yield return item;
	}

	public static IEnumerable<T> ToEnumerable<T>(this (T, T) tuple)
	{
		yield return tuple.Item1;
		yield return tuple.Item2;
	}

	public static IEnumerable<T> ToEnumerable<T>(this (T, T, T) tuple)
	{
		yield return tuple.Item1;
		yield return tuple.Item2;
		yield return tuple.Item3;
	}

	public static IEnumerable<T> ToEnumerable<T>(this (T, T, T, T) tuple)
	{
		yield return tuple.Item1;
		yield return tuple.Item2;
		yield return tuple.Item3;
		yield return tuple.Item4;
	}
}
