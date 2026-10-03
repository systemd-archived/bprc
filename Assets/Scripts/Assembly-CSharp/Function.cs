using System;

public static class Function
{
	public static Func<T, T> Compose<T>(Func<T, T> f, Func<T, T> g)
	{
		return (T x) => g(f(x));
	}

	public static Func<T1, TResult> Compose<T1, T2, TResult>(Func<T1, T2> f, Func<T2, TResult> g)
	{
		return (T1 x) => g(f(x));
	}

	public static Func<T, T> Compose<T>(Func<T, T> f, Func<T, T> g, Func<T, T> h)
	{
		return (T x) => h(g(f(x)));
	}

	public static Func<T1, TResult> Compose<T1, T2, T3, TResult>(Func<T1, T2> f, Func<T2, T3> g, Func<T3, TResult> h)
	{
		return (T1 x) => h(g(f(x)));
	}

	public static Func<T, T> Compose<T>(Func<T, T> f, Func<T, T> g, Func<T, T> h, Func<T, T> i)
	{
		return (T x) => i(h(g(f(x))));
	}

	public static Func<T1, TResult> Compose<T1, T2, T3, T4, TResult>(Func<T1, T2> f, Func<T2, T3> g, Func<T3, T4> h, Func<T4, TResult> i)
	{
		return (T1 x) => i(h(g(f(x))));
	}

	public static T Call<T>(this T x, Action<T> f)
	{
		f(x);
		return x;
	}

	public static TResult Call<T, TResult>(this T x, Func<T, TResult> f)
	{
		return f(x);
	}

	public static Func<T1, Func<T2, TResult>> Currying<T1, T2, TResult>(this Func<T1, T2, TResult> f)
	{
		return (T1 x) => (T2 y) => f(x, y);
	}

	public static Func<T1, Func<T2, Func<T3, TResult>>> Currying<T1, T2, T3, TResult>(this Func<T1, T2, T3, TResult> f)
	{
		return (T1 x) => (T2 y) => (T3 z) => f(x, y, z);
	}

	public static Func<T1, Func<T2, Func<T3, Func<T4, TResult>>>> Currying<T1, T2, T3, T4, TResult>(this Func<T1, T2, T3, T4, TResult> f)
	{
		return (T1 x) => (T2 y) => (T3 z) => (T4 w) => f(x, y, z, w);
	}
}
