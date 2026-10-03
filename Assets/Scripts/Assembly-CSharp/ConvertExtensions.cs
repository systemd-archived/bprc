using System;
using UnityEngine;

public static class ConvertExtensions
{
	public class PartTypeConverter
	{
		private BasePart.PartType[] m_partTypeTable;

		private SortedPartType[] m_sortedPartTypeTable;

		public PartTypeConverter()
		{
			m_partTypeTable = new BasePart.PartType[(int)SortedPartType.MAX];
			m_sortedPartTypeTable = new SortedPartType[(int)BasePart.PartType.MAX];
			for (int i = 0; i < m_partTypeTable.Length; i++)
			{
				SortedPartType sortedPartType = (SortedPartType)i;
				if (!Enum.TryParse<BasePart.PartType>(sortedPartType.ToString(), out var result))
				{
					result = BasePart.PartType.Unknown;
				}
				m_partTypeTable[i] = result;
			}
			for (int j = 0; j < m_sortedPartTypeTable.Length; j++)
			{
				BasePart.PartType partType = (BasePart.PartType)j;
				if (!Enum.TryParse<SortedPartType>(partType.ToString(), out var result2))
				{
					result2 = SortedPartType.Unknown;
				}
				m_sortedPartTypeTable[j] = result2;
			}
		}

		public BasePart.PartType ToPartType(SortedPartType sortedPartType)
		{
			if (sortedPartType >= SortedPartType.Unknown && sortedPartType < SortedPartType.MAX)
			{
				return m_partTypeTable[(int)sortedPartType];
			}
			return (BasePart.PartType)sortedPartType;
		}

		public SortedPartType ToSortedPartType(BasePart.PartType partType)
		{
			if (partType >= BasePart.PartType.Unknown && partType < BasePart.PartType.MAX)
			{
				return m_sortedPartTypeTable[(int)partType];
			}
			return (SortedPartType)partType;
		}
	}

	private static PartTypeConverter s_partTypeConverter = new PartTypeConverter();

	public static string Vector2ToString(this Vector2 vector)
	{
		return vector.Vector2ToString(null);
	}

	public static string Vector2ToString(this Vector2 vector, string format)
	{
		return "(" + vector.x.ToString(format) + ", " + vector.y.ToString(format) + ")";
	}

	public static string Vector2ToString(this Vector3 vector)
	{
		return vector.Vector2ToString(null);
	}

	public static string Vector2ToString(this Vector3 vector, string format)
	{
		return "(" + vector.x.ToString(format) + ", " + vector.y.ToString(format) + ")";
	}

	public static string Vector3ToString(this Vector3 vector)
	{
		return vector.Vector3ToString(null);
	}

	public static string Vector3ToString(this Vector3 vector, string format)
	{
		return "(" + vector.x.ToString(format) + ", " + vector.y.ToString(format) + ", " + vector.z.ToString(format) + ")";
	}

	public static string ArrayToString<T>(this T[] array)
	{
		string text = "[";
		for (int i = 0; i < array.Length; i++)
		{
			text += array[i].ToString();
			if (i != array.Length - 1)
			{
				text += "; ";
			}
		}
		return text + "]";
	}

	public static T ToValue<T>(this string str) where T : struct
	{
		return str.ToValue<T>(ignoreCase: false);
	}

	public static T ToValue<T>(this string str, bool ignoreCase) where T : struct
	{
		Type typeFromHandle = typeof(T);
		if (typeFromHandle.IsPrimitive)
		{
			return str.ToPrimitive<T>();
		}
		if (typeFromHandle.IsEnum)
		{
			return str.ToEnum<T>(ignoreCase);
		}
		throw new FormatException();
	}

	public static bool TryToValue<T>(this string str, out T value) where T : struct
	{
		return str.TryToValue<T>(ignoreCase: false, out value);
	}

	public static bool TryToValue<T>(this string str, bool ignoreCase, out T value) where T : struct
	{
		Type typeFromHandle = typeof(T);
		if (typeFromHandle.IsPrimitive)
		{
			return str.TryToPrimitive<T>(out value);
		}
		if (typeFromHandle.IsEnum)
		{
			return str.TryToEnum<T>(ignoreCase, out value);
		}
		value = default(T);
		return false;
	}

	public static T ToPrimitive<T>(this string str) where T : struct
	{
		Type typeFromHandle = typeof(T);
		if (typeFromHandle == typeof(bool))
		{
			return (T)(object)bool.Parse(str);
		}
		if (typeFromHandle == typeof(char))
		{
			return (T)(object)char.Parse(str);
		}
		if (typeFromHandle == typeof(sbyte))
		{
			return (T)(object)sbyte.Parse(str);
		}
		if (typeFromHandle == typeof(byte))
		{
			return (T)(object)byte.Parse(str);
		}
		if (typeFromHandle == typeof(short))
		{
			return (T)(object)short.Parse(str);
		}
		if (typeFromHandle == typeof(ushort))
		{
			return (T)(object)ushort.Parse(str);
		}
		if (typeFromHandle == typeof(int))
		{
			return (T)(object)int.Parse(str);
		}
		if (typeFromHandle == typeof(uint))
		{
			return (T)(object)uint.Parse(str);
		}
		if (typeFromHandle == typeof(long))
		{
			return (T)(object)long.Parse(str);
		}
		if (typeFromHandle == typeof(ulong))
		{
			return (T)(object)ulong.Parse(str);
		}
		if (typeFromHandle == typeof(float))
		{
			return (T)(object)float.Parse(str);
		}
		if (typeFromHandle == typeof(double))
		{
			return (T)(object)double.Parse(str);
		}
		throw new FormatException();
	}

	public static bool TryToPrimitive<T>(this string str, out T value) where T : struct
	{
		Type typeFromHandle = typeof(T);
		if (typeFromHandle == typeof(bool))
		{
			bool result2;
			bool result = bool.TryParse(str, out result2);
			value = (T)(object)result2;
			return result;
		}
		if (typeFromHandle == typeof(char))
		{
			char result4;
			bool result3 = char.TryParse(str, out result4);
			value = (T)(object)result4;
			return result3;
		}
		if (typeFromHandle == typeof(sbyte))
		{
			sbyte result6;
			bool result5 = sbyte.TryParse(str, out result6);
			value = (T)(object)result6;
			return result5;
		}
		if (typeFromHandle == typeof(byte))
		{
			byte result8;
			bool result7 = byte.TryParse(str, out result8);
			value = (T)(object)result8;
			return result7;
		}
		if (typeFromHandle == typeof(short))
		{
			short result10;
			bool result9 = short.TryParse(str, out result10);
			value = (T)(object)result10;
			return result9;
		}
		if (typeFromHandle == typeof(ushort))
		{
			ushort result12;
			bool result11 = ushort.TryParse(str, out result12);
			value = (T)(object)result12;
			return result11;
		}
		if (typeFromHandle == typeof(int))
		{
			int result14;
			bool result13 = int.TryParse(str, out result14);
			value = (T)(object)result14;
			return result13;
		}
		if (typeFromHandle == typeof(uint))
		{
			uint result16;
			bool result15 = uint.TryParse(str, out result16);
			value = (T)(object)result16;
			return result15;
		}
		if (typeFromHandle == typeof(long))
		{
			long result18;
			bool result17 = long.TryParse(str, out result18);
			value = (T)(object)result18;
			return result17;
		}
		if (typeFromHandle == typeof(ulong))
		{
			ulong result20;
			bool result19 = ulong.TryParse(str, out result20);
			value = (T)(object)result20;
			return result19;
		}
		if (typeFromHandle == typeof(float))
		{
			float result22;
			bool result21 = float.TryParse(str, out result22);
			value = (T)(object)result22;
			return result21;
		}
		if (typeFromHandle == typeof(double))
		{
			double result24;
			bool result23 = double.TryParse(str, out result24);
			value = (T)(object)result24;
			return result23;
		}
		value = default(T);
		return false;
	}

	public static T ToEnum<T>(this string str) where T : struct
	{
		return (T)Enum.Parse(typeof(T), str);
	}

	public static bool TryToEnum<T>(this string str, out T value) where T : struct
	{
		T result2;
		bool result = Enum.TryParse<T>(str, out result2);
		value = result2;
		return result;
	}

	public static T ToEnum<T>(this string str, bool ignoreCase) where T : struct
	{
		return (T)Enum.Parse(typeof(T), str, ignoreCase);
	}

	public static bool TryToEnum<T>(this string str, bool ignoreCase, out T value) where T : struct
	{
		T result2;
		bool result = Enum.TryParse<T>(str, ignoreCase, out result2);
		value = result2;
		return result;
	}

	public static Vector2 ToVector2(this string str)
	{
		string[] array = str.Substring(1, str.Length - 2).Split(',');
		float x = float.Parse(array[0]);
		float y = float.Parse(array[1]);
		return new Vector2(x, y);
	}

	public static bool TryToVector2(this string str, out Vector2 vector)
	{
		if (str.Length >= 2)
		{
			string[] array = str.Substring(1, str.Length - 2).Split(',');
			if (array.Length >= 2 && float.TryParse(array[0], out var result) && float.TryParse(array[1], out var result2))
			{
				vector = new Vector2(result, result2);
				return true;
			}
		}
		vector = default(Vector2);
		return false;
	}

	public static Vector3 ToVector3(this string str)
	{
		string[] array = str.Substring(1, str.Length - 2).Split(',');
		float x = float.Parse(array[0]);
		float y = float.Parse(array[1]);
		float z = float.Parse(array[2]);
		return new Vector3(x, y, z);
	}

	public static BasePart.PartType ToPartType(this SortedPartType sortedPartType)
	{
		return s_partTypeConverter.ToPartType(sortedPartType);
	}

	public static SortedPartType ToSortedPartType(this BasePart.PartType partType)
	{
		return s_partTypeConverter.ToSortedPartType(partType);
	}
}
