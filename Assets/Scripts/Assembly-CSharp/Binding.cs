using System;
using System.Collections.Generic;
using System.ComponentModel;

public static class Binding
{
	private static Action s_activeSubscriber;

	private static int s_recursionDepth;

	private static Dictionary<object, Dictionary<string, Dependency>> s_dependencyMap;

	static Binding()
	{
		s_dependencyMap = new Dictionary<object, Dictionary<string, Dependency>>();
	}

	public static void Bind(Action subscriber)
	{
		Call(subscriber);
	}

	public static T GetValue<T>(object target, string propertyName, T value)
	{
		Track(target, propertyName);
		return value;
	}

	public static void SetValue<T>(object target, string propertyName, ref T value, T newValue)
	{
		if (!EqualityComparer<T>.Default.Equals(value, newValue))
		{
			value = newValue;
			Trigger(target, propertyName);
		}
	}

	public static void Track(object target, string name)
	{
		if (s_activeSubscriber != null)
		{
			Subscribe(target, name, s_activeSubscriber);
		}
	}

	public static void Track(Dependency dependency)
	{
		if (s_activeSubscriber != null)
		{
			dependency.Subscribe(s_activeSubscriber);
		}
	}

	public static void Trigger(object target, string name)
	{
		if (s_dependencyMap.TryGetValue(target, out var value) && value.TryGetValue(name, out var value2))
		{
			value2.Invoke(Call);
		}
	}

	public static void Trigger(Dependency dependency)
	{
		dependency.Invoke(Call);
	}

	public static void Subscribe(object target, string name, Action subscriber)
	{
		if (!s_dependencyMap.TryGetValue(target, out var value))
		{
			value = new Dictionary<string, Dependency>();
			s_dependencyMap.Add(target, value);
		}
		if (!value.TryGetValue(name, out var value2))
		{
			value2 = new Dependency();
			value.Add(name, value2);
		}
		value2.Subscribe(subscriber);
	}

	public static void Unsubscribe(object target)
	{
		s_dependencyMap.Remove(target);
	}

	public static void Unsubscribe(object target, string name)
	{
		if (s_dependencyMap.TryGetValue(target, out var value))
		{
			value.Remove(name);
		}
	}

	private static void Call(Action subscriber)
	{
		if (s_recursionDepth >= 10)
		{
			throw new InvalidOperationException("Maximum recursion depth exceeded.");
		}
		Action action = s_activeSubscriber;
		s_activeSubscriber = subscriber;
		s_recursionDepth++;
		try
		{
			subscriber();
		}
		finally
		{
			s_activeSubscriber = action;
			s_recursionDepth--;
		}
	}

	public static void Bind(INotifyPropertyChanged source, string propertyName, Action subscriber)
	{
		Call(subscriber);
		source.PropertyChanged += delegate(object sender, PropertyChangedEventArgs args)
		{
			if (args.PropertyName == propertyName)
			{
				Call(subscriber);
			}
		};
	}

	public static void Bind(INotifyPropertyChanged source, Predicate<string> predicate, Action subscriber)
	{
		Call(subscriber);
		source.PropertyChanged += delegate(object sender, PropertyChangedEventArgs args)
		{
			if (predicate(args.PropertyName))
			{
				Call(subscriber);
			}
		};
	}
}
