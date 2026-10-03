using System;
using System.Runtime.CompilerServices;

public readonly struct Maybe<T>
{
	private readonly bool m_hasValue;

	private readonly T m_value;

	public bool HasValue
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get
		{
			return m_hasValue;
		}
	}

	public T Value
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get
		{
			if (!m_hasValue)
			{
				throw new InvalidOperationException();
			}
			return m_value;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Maybe(T value)
	{
		this = default(Maybe<T>);
		if (value != null)
		{
			m_hasValue = true;
			m_value = value;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Maybe<T> Just(T value)
	{
		return new Maybe<T>(value);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Maybe<T> Nothing()
	{
		return default(Maybe<T>);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator Maybe<T>(T value)
	{
		return new Maybe<T>(value);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static explicit operator T(Maybe<T> value)
	{
		return value.Value;
	}
}
