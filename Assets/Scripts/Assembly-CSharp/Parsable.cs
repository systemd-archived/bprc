using System;
using System.Globalization;

public static class Parsable
{
	public delegate T Parser<T>(string s, IFormatProvider provider);

	public delegate bool TryParser<T>(string s, IFormatProvider provider, out T result);

	public static bool ParseBoolean(string s, IFormatProvider provider)
	{
		return bool.Parse(s);
	}

	public static bool TryParseBoolean(string s, IFormatProvider provider, out bool result)
	{
		return bool.TryParse(s, out result);
	}

	public static char ParseChar(string s, IFormatProvider provider)
	{
		return char.Parse(s);
	}

	public static bool TryParseChar(string s, IFormatProvider provider, out char result)
	{
		return char.TryParse(s, out result);
	}

	public static sbyte ParseSByte(string s, IFormatProvider provider)
	{
		return sbyte.Parse(s, NumberStyles.Integer, provider);
	}

	public static bool TryParseSByte(string s, IFormatProvider provider, out sbyte result)
	{
		return sbyte.TryParse(s, NumberStyles.Integer, provider, out result);
	}

	public static byte ParseByte(string s, IFormatProvider provider)
	{
		return byte.Parse(s, NumberStyles.Integer, provider);
	}

	public static bool TryParseByte(string s, IFormatProvider provider, out byte result)
	{
		return byte.TryParse(s, NumberStyles.Integer, provider, out result);
	}

	public static short ParseInt16(string s, IFormatProvider provider)
	{
		return short.Parse(s, NumberStyles.Integer, provider);
	}

	public static bool TryParseInt16(string s, IFormatProvider provider, out short result)
	{
		return short.TryParse(s, NumberStyles.Integer, provider, out result);
	}

	public static ushort ParseUInt16(string s, IFormatProvider provider)
	{
		return ushort.Parse(s, NumberStyles.Integer, provider);
	}

	public static bool TryParseUInt16(string s, IFormatProvider provider, out ushort result)
	{
		return ushort.TryParse(s, NumberStyles.Integer, provider, out result);
	}

	public static int ParseInt32(string s, IFormatProvider provider)
	{
		return int.Parse(s, NumberStyles.Integer, provider);
	}

	public static bool TryParseInt32(string s, IFormatProvider provider, out int result)
	{
		return int.TryParse(s, NumberStyles.Integer, provider, out result);
	}

	public static uint ParseUInt32(string s, IFormatProvider provider)
	{
		return uint.Parse(s, NumberStyles.Integer, provider);
	}

	public static bool TryParseUInt32(string s, IFormatProvider provider, out uint result)
	{
		return uint.TryParse(s, NumberStyles.Integer, provider, out result);
	}

	public static long ParseInt64(string s, IFormatProvider provider)
	{
		return long.Parse(s, NumberStyles.Integer, provider);
	}

	public static bool TryParseInt64(string s, IFormatProvider provider, out long result)
	{
		return long.TryParse(s, NumberStyles.Integer, provider, out result);
	}

	public static ulong ParseUInt64(string s, IFormatProvider provider)
	{
		return ulong.Parse(s, NumberStyles.Integer, provider);
	}

	public static bool TryParseUInt64(string s, IFormatProvider provider, out ulong result)
	{
		return ulong.TryParse(s, NumberStyles.Integer, provider, out result);
	}

	public static float ParseSingle(string s, IFormatProvider provider)
	{
		return float.Parse(s, NumberStyles.Float | NumberStyles.AllowThousands, provider);
	}

	public static bool TryParseSingle(string s, IFormatProvider provider, out float result)
	{
		return float.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, provider, out result);
	}

	public static double ParseDouble(string s, IFormatProvider provider)
	{
		return double.Parse(s, NumberStyles.Float | NumberStyles.AllowThousands, provider);
	}

	public static bool TryParseDouble(string s, IFormatProvider provider, out double result)
	{
		return double.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, provider, out result);
	}

	public static decimal ParseDecimal(string s, IFormatProvider provider)
	{
		return decimal.Parse(s, NumberStyles.Float | NumberStyles.AllowThousands, provider);
	}

	public static bool TryParseDecimal(string s, IFormatProvider provider, out decimal result)
	{
		return decimal.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, provider, out result);
	}

	public static T ParseEnum<T>(string s, IFormatProvider provider)
	{
		return (T)Enum.Parse(typeof(T), s);
	}

	public static T ParseEnum<T>(string s, bool ignoreCase, IFormatProvider provider)
	{
		return (T)Enum.Parse(typeof(T), s, ignoreCase);
	}

	public static bool TryParseEnum<T>(string s, IFormatProvider provider, out T result)
	{
		object result2;
		bool flag = Enum.TryParse(typeof(T), s, out result2);
		result = (flag ? ((T)result2) : default(T));
		return flag;
	}

	public static bool TryParseEnum<T>(string s, bool ignoreCase, IFormatProvider provider, out T result)
	{
		object result2;
		bool flag = Enum.TryParse(typeof(T), s, ignoreCase, out result2);
		result = (flag ? ((T)result2) : default(T));
		return flag;
	}

	public static string ParseString(string s, IFormatProvider provider)
	{
		return s;
	}

	public static bool TryParseString(string s, IFormatProvider provider, out string result)
	{
		result = s;
		return true;
	}

	public static Parser<T> GetParser<T>()
	{
		if (typeof(T) == typeof(bool))
		{
			return (Parser<T>)(object)new Parser<bool>(ParseBoolean);
		}
		if (typeof(T) == typeof(char))
		{
			return (Parser<T>)(object)new Parser<char>(ParseChar);
		}
		if (typeof(T) == typeof(sbyte))
		{
			return (Parser<T>)(object)new Parser<sbyte>(ParseSByte);
		}
		if (typeof(T) == typeof(byte))
		{
			return (Parser<T>)(object)new Parser<byte>(ParseByte);
		}
		if (typeof(T) == typeof(short))
		{
			return (Parser<T>)(object)new Parser<short>(ParseInt16);
		}
		if (typeof(T) == typeof(ushort))
		{
			return (Parser<T>)(object)new Parser<ushort>(ParseUInt16);
		}
		if (typeof(T) == typeof(int))
		{
			return (Parser<T>)(object)new Parser<int>(ParseInt32);
		}
		if (typeof(T) == typeof(uint))
		{
			return (Parser<T>)(object)new Parser<uint>(ParseUInt32);
		}
		if (typeof(T) == typeof(long))
		{
			return (Parser<T>)(object)new Parser<long>(ParseInt64);
		}
		if (typeof(T) == typeof(ulong))
		{
			return (Parser<T>)(object)new Parser<ulong>(ParseUInt64);
		}
		if (typeof(T) == typeof(float))
		{
			return (Parser<T>)(object)new Parser<float>(ParseSingle);
		}
		if (typeof(T) == typeof(double))
		{
			return (Parser<T>)(object)new Parser<double>(ParseDouble);
		}
		if (typeof(T) == typeof(decimal))
		{
			return (Parser<T>)(object)new Parser<decimal>(ParseDecimal);
		}
		if (typeof(T) == typeof(string))
		{
			return (Parser<T>)(object)new Parser<string>(ParseString);
		}
		if (typeof(T).IsEnum)
		{
			return ParseEnum<T>;
		}
		throw new InvalidOperationException();
	}

	public static TryParser<T> GetTryParser<T>()
	{
		if (typeof(T) == typeof(bool))
		{
			return (TryParser<T>)(object)new TryParser<bool>(TryParseBoolean);
		}
		if (typeof(T) == typeof(char))
		{
			return (TryParser<T>)(object)new TryParser<char>(TryParseChar);
		}
		if (typeof(T) == typeof(sbyte))
		{
			return (TryParser<T>)(object)new TryParser<sbyte>(TryParseSByte);
		}
		if (typeof(T) == typeof(byte))
		{
			return (TryParser<T>)(object)new TryParser<byte>(TryParseByte);
		}
		if (typeof(T) == typeof(short))
		{
			return (TryParser<T>)(object)new TryParser<short>(TryParseInt16);
		}
		if (typeof(T) == typeof(ushort))
		{
			return (TryParser<T>)(object)new TryParser<ushort>(TryParseUInt16);
		}
		if (typeof(T) == typeof(int))
		{
			return (TryParser<T>)(object)new TryParser<int>(TryParseInt32);
		}
		if (typeof(T) == typeof(uint))
		{
			return (TryParser<T>)(object)new TryParser<uint>(TryParseUInt32);
		}
		if (typeof(T) == typeof(long))
		{
			return (TryParser<T>)(object)new TryParser<long>(TryParseInt64);
		}
		if (typeof(T) == typeof(ulong))
		{
			return (TryParser<T>)(object)new TryParser<ulong>(TryParseUInt64);
		}
		if (typeof(T) == typeof(float))
		{
			return (TryParser<T>)(object)new TryParser<float>(TryParseSingle);
		}
		if (typeof(T) == typeof(double))
		{
			return (TryParser<T>)(object)new TryParser<double>(TryParseDouble);
		}
		if (typeof(T) == typeof(decimal))
		{
			return (TryParser<T>)(object)new TryParser<decimal>(TryParseDecimal);
		}
		if (typeof(T) == typeof(string))
		{
			return (TryParser<T>)(object)new TryParser<string>(TryParseString);
		}
		if (typeof(T).IsEnum)
		{
			return TryParseEnum;
		}
		throw new InvalidOperationException();
	}
}
