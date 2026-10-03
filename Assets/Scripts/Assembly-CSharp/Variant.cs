using System;
using System.Collections.Generic;

public abstract class Variant : IEquatable<Variant>
{
	public abstract object BoxedValue { get; }

	public T Unbox<T>()
	{
		if (this is Variant<T> variant)
		{
			return variant.Value;
		}
		return (T)BoxedValue;
	}

	public override string ToString()
	{
		return BoxedValue.ToString();
	}

	public override bool Equals(object other)
	{
		if (other is Variant other2)
		{
			return Equals(other2);
		}
		return false;
	}

	public bool Equals(Variant other)
	{
		if (!ReferenceEquals(other, null))
		{
			return EqualityComparer<object>.Default.Equals(BoxedValue, other.BoxedValue);
		}
		return false;
	}

	public override int GetHashCode()
	{
		return EqualityComparer<object>.Default.GetHashCode(BoxedValue);
	}

	public static bool operator ==(Variant left, Variant right)
	{
		if (ReferenceEquals(left, right))
		{
			return true;
		}
		if (ReferenceEquals(left, null))
		{
			return false;
		}
		return left.Equals(right);
	}

	public static bool operator !=(Variant left, Variant right)
	{
		if (ReferenceEquals(left, right))
		{
			return false;
		}
		if (ReferenceEquals(left, null))
		{
			return true;
		}
		return !left.Equals(right);
	}

	public static Variant<T> Create<T>(IConvertible convertible, IFormatProvider provider)
	{
		return (Variant<T>)Create(convertible, typeof(T), provider);
	}

	public static Variant Create(IConvertible convertible, Type type, IFormatProvider provider)
	{
		if (type == typeof(bool))
		{
			return new Variant<bool>(convertible.ToBoolean(provider));
		}
		if (type == typeof(char))
		{
			return new Variant<char>(convertible.ToChar(provider));
		}
		if (type == typeof(sbyte))
		{
			return new Variant<sbyte>(convertible.ToSByte(provider));
		}
		if (type == typeof(byte))
		{
			return new Variant<byte>(convertible.ToByte(provider));
		}
		if (type == typeof(short))
		{
			return new Variant<short>(convertible.ToInt16(provider));
		}
		if (type == typeof(ushort))
		{
			return new Variant<ushort>(convertible.ToUInt16(provider));
		}
		if (type == typeof(int))
		{
			return new Variant<int>(convertible.ToInt32(provider));
		}
		if (type == typeof(uint))
		{
			return new Variant<uint>(convertible.ToUInt32(provider));
		}
		if (type == typeof(long))
		{
			return new Variant<long>(convertible.ToInt64(provider));
		}
		if (type == typeof(ulong))
		{
			return new Variant<ulong>(convertible.ToUInt64(provider));
		}
		if (type == typeof(float))
		{
			return new Variant<float>(convertible.ToSingle(provider));
		}
		if (type == typeof(double))
		{
			return new Variant<double>(convertible.ToDouble(provider));
		}
		if (type == typeof(decimal))
		{
			return new Variant<decimal>(convertible.ToDecimal(provider));
		}
		if (type == typeof(DateTime))
		{
			return new Variant<DateTime>(convertible.ToDateTime(provider));
		}
		if (type == typeof(string))
		{
			return new Variant<string>(convertible.ToString(provider));
		}
		if (type == typeof(object))
		{
			return new Variant<object>(convertible);
		}
		throw new InvalidCastException();
	}

	public static Variant Create(IConvertible convertible, TypeCode type, IFormatProvider provider)
	{
		return type switch
		{
			TypeCode.Boolean => new Variant<bool>(convertible.ToBoolean(provider)), 
			TypeCode.Char => new Variant<char>(convertible.ToChar(provider)), 
			TypeCode.SByte => new Variant<sbyte>(convertible.ToSByte(provider)), 
			TypeCode.Byte => new Variant<byte>(convertible.ToByte(provider)), 
			TypeCode.Int16 => new Variant<short>(convertible.ToInt16(provider)), 
			TypeCode.UInt16 => new Variant<ushort>(convertible.ToUInt16(provider)), 
			TypeCode.Int32 => new Variant<int>(convertible.ToInt32(provider)), 
			TypeCode.UInt32 => new Variant<uint>(convertible.ToUInt32(provider)), 
			TypeCode.Int64 => new Variant<long>(convertible.ToInt64(provider)), 
			TypeCode.UInt64 => new Variant<ulong>(convertible.ToUInt64(provider)), 
			TypeCode.Single => new Variant<float>(convertible.ToSingle(provider)), 
			TypeCode.Double => new Variant<double>(convertible.ToDouble(provider)), 
			TypeCode.Decimal => new Variant<decimal>(convertible.ToDecimal(provider)), 
			TypeCode.DateTime => new Variant<DateTime>(convertible.ToDateTime(provider)), 
			TypeCode.String => new Variant<string>(convertible.ToString(provider)), 
			TypeCode.Object => new Variant<object>(convertible), 
			_ => throw new InvalidCastException(), 
		};
	}
}
public class Variant<T> : Variant, IEquatable<Variant<T>>
{
	private T m_value;

	public T Value => m_value;

	public override object BoxedValue => m_value;

	public Variant(T value)
	{
		m_value = value;
	}

	public object Box()
	{
		return m_value;
	}

	public override string ToString()
	{
		return m_value.ToString();
	}

	public override bool Equals(object other)
	{
		if (other is Variant<T> other2)
		{
			return Equals(other2);
		}
		return false;
	}

	public bool Equals(Variant<T> other)
	{
		if (!ReferenceEquals(other, null))
		{
			return EqualityComparer<T>.Default.Equals(m_value, other.m_value);
		}
		return false;
	}

	public override int GetHashCode()
	{
		return EqualityComparer<T>.Default.GetHashCode(m_value);
	}

	public static bool operator ==(Variant<T> left, Variant<T> right)
	{
		if (!ReferenceEquals(left, right))
		{
			if (!ReferenceEquals(left, null))
			{
				return left.Equals(right);
			}
			return false;
		}
		return true;
	}

	public static bool operator !=(Variant<T> left, Variant<T> right)
	{
		return !(ReferenceEquals(left, right) || (!ReferenceEquals(left, null) && left.Equals(right)));
	}
}
