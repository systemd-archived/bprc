using System;
using System.Reflection;

public static class SettingsExtensions
{
	public static Func<TTarget, TValue> GenerateGetter<TTarget, TValue>(this FieldInfo fieldInfo)
	{
		return null;
	}
}
