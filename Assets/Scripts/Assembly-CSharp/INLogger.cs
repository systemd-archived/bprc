using System.IO;
using System.Text;
using UnityEngine;

public static class INLogger
{
	private static string s_path = Path.Combine(Application.persistentDataPath, "INLog.txt");

	private static StringBuilder s_builder = new StringBuilder();

	public static void Log(object arg)
	{
		s_builder.Append(arg);
	}

	public static void Log(params object[] args)
	{
		foreach (object value in args)
		{
			s_builder.Append(value);
		}
	}

	public static void LogFormat(string format, params object[] args)
	{
		s_builder.AppendFormat(format, args);
	}

	public static void LogJoin(string separator, params object[] args)
	{
		s_builder.AppendJoin(separator, args);
	}

	public static void ILog(object arg)
	{
		LogInternal(arg.ToString());
	}

	public static void ILog(params object[] args)
	{
		LogInternal(string.Concat(args));
	}

	public static void ILogFormat(string format, params object[] args)
	{
		LogInternal(string.Format(format, args));
	}

	public static void ILogJoin(string separator, params object[] args)
	{
		LogInternal(string.Join(separator, args));
	}

	public static void Save()
	{
		LogInternal(s_builder.ToString());
		s_builder.Clear();
	}

	private static void LogInternal(string text)
	{
		using StreamWriter streamWriter = new StreamWriter(s_path, append: true);
		streamWriter.Write(text);
	}
}
