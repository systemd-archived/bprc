using System;

public static class MaybeExtensions
{
	public static Maybe<U> Fmap<T, U>(this Maybe<T> self, Func<T, U> f)
	{
		if (!self.HasValue)
		{
			return Maybe<U>.Nothing();
		}
		return Maybe<U>.Just(f(self.Value));
	}

	public static Maybe<U> Apply<T, U>(this Maybe<T> self, Maybe<Func<T, U>> f)
	{
		if (!self.HasValue || !f.HasValue)
		{
			return Maybe<U>.Nothing();
		}
		return Maybe<U>.Just(f.Value(self.Value));
	}

	public static Maybe<T> Returns<T>(this T value)
	{
		return Maybe<T>.Just(value);
	}

	public static Maybe<U> Bind<T, U>(this Maybe<T> self, Func<T, Maybe<U>> f)
	{
		if (!self.HasValue)
		{
			return Maybe<U>.Nothing();
		}
		return f(self.Value);
	}

	public static Maybe<U> Then<T, U>(this Maybe<T> self, Maybe<U> other)
	{
		return other;
	}
}
