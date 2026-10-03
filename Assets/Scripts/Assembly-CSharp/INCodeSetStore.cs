using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

// “代码集”存储：应用数据目录下 Settings/codesets/<集名>/*.cs。
// 放在应用可读写目录，**打包成软件后依然可用**（不依赖工程源码）。
public static class INCodeSetStore
{
	public const string DefaultSet = "default";

	public static string Root
	{
		get { return Path.Combine(INUnity.SettingsPath, "codesets"); }
	}

	public static string SetDir(string set)
	{
		return Path.Combine(Root, string.IsNullOrEmpty(set) ? DefaultSet : set);
	}

	public static void EnsureRoot()
	{
		try
		{
			Directory.CreateDirectory(Root);
		}
		catch
		{
		}
	}

	public static void EnsureSet(string set)
	{
		try
		{
			Directory.CreateDirectory(SetDir(set));
		}
		catch
		{
		}
	}

	public static List<string> ListSets()
	{
		EnsureRoot();
		List<string> list = new List<string>();
		try
		{
			foreach (string d in Directory.GetDirectories(Root))
			{
				string n = Path.GetFileName(d);
				if (!string.IsNullOrEmpty(n))
				{
					list.Add(n);
				}
			}
		}
		catch
		{
		}
		list.Sort(StringComparer.Ordinal);
		return list;
	}

	public static List<string> ListFiles(string set)
	{
		EnsureSet(set);
		List<string> list = new List<string>();
		try
		{
			foreach (string f in Directory.GetFiles(SetDir(set), "*.cs"))
			{
				list.Add(Path.GetFileName(f));
			}
		}
		catch
		{
		}
		list.Sort(StringComparer.Ordinal);
		return list;
	}

	public static string FilePath(string set, string file)
	{
		return Path.Combine(SetDir(set), file);
	}

	public static string ReadFile(string set, string file)
	{
		try
		{
			string p = FilePath(set, file);
			if (File.Exists(p))
			{
				return File.ReadAllText(p);
			}
		}
		catch
		{
		}
		return null;
	}

	public static bool WriteFile(string set, string file, string text)
	{
		try
		{
			EnsureSet(set);
			File.WriteAllText(FilePath(set, file), text);
			return true;
		}
		catch
		{
			return false;
		}
	}

	public static bool DeleteFile(string set, string file)
	{
		try
		{
			string p = FilePath(set, file);
			if (File.Exists(p))
			{
				File.Delete(p);
				return true;
			}
		}
		catch
		{
		}
		return false;
	}

	// 新建一个空代码集，返回集名
	public static string CreateSet()
	{
		EnsureRoot();
		for (int i = 1; i < 1000; i++)
		{
			string name = "code" + i;
			if (!Directory.Exists(SetDir(name)))
			{
				EnsureSet(name);
				return name;
			}
		}
		return DefaultSet;
	}

	public static bool DeleteSet(string set)
	{
		try
		{
			string d = SetDir(set);
			if (Directory.Exists(d))
			{
				Directory.Delete(d, true);
				return true;
			}
		}
		catch
		{
		}
		return false;
	}

	// 导入：sourcePath 可为 文件夹 / .zip / 单个 .cs。返回导入的文件数。
	public static int Import(string set, string sourcePath, out string error)
	{
		error = null;
		EnsureSet(set);
		int n = 0;
		try
		{
			if (Directory.Exists(sourcePath))
			{
				foreach (string f in Directory.GetFiles(sourcePath, "*.cs", SearchOption.AllDirectories))
				{
					File.Copy(f, FilePath(set, Path.GetFileName(f)), true);
					n++;
				}
			}
			else if (File.Exists(sourcePath))
			{
				if (sourcePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
				{
					using (ZipArchive zip = ZipFile.OpenRead(sourcePath))
					{
						foreach (ZipArchiveEntry e in zip.Entries)
						{
							if (string.IsNullOrEmpty(e.Name) || !e.Name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
							{
								continue;
							}
							e.ExtractToFile(FilePath(set, e.Name), true);
							n++;
						}
					}
				}
				else if (sourcePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
				{
					File.Copy(sourcePath, FilePath(set, Path.GetFileName(sourcePath)), true);
					n = 1;
				}
				else
				{
					error = "仅支持 文件夹 / .zip / .cs";
				}
			}
			else
			{
				error = "路径不存在";
			}
		}
		catch (Exception e)
		{
			error = e.Message;
		}
		return n;
	}

	// 导出：destPath 以 .zip 结尾则打包为 zip，否则视为目标文件夹。返回导出的文件数。
	public static int Export(string set, string destPath, out string error)
	{
		error = null;
		int n = 0;
		try
		{
			string dir = SetDir(set);
			if (!Directory.Exists(dir))
			{
				error = "代码集不存在";
				return 0;
			}
			string[] files = Directory.GetFiles(dir, "*.cs");
			if (destPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
			{
				if (File.Exists(destPath))
				{
					File.Delete(destPath);
				}
				string parent = Path.GetDirectoryName(destPath);
				if (!string.IsNullOrEmpty(parent))
				{
					Directory.CreateDirectory(parent);
				}
				ZipFile.CreateFromDirectory(dir, destPath);
				n = files.Length;
			}
			else
			{
				Directory.CreateDirectory(destPath);
				foreach (string f in files)
				{
					File.Copy(f, Path.Combine(destPath, Path.GetFileName(f)), true);
					n++;
				}
			}
		}
		catch (Exception e)
		{
			error = e.Message;
		}
		return n;
	}

	// 在所有代码集中按“类名==文件名(不含扩展名)”查找，返回文件名与此文件所在集。
	public static string FindClassFile(string className, out string foundSet)
	{
		foundSet = null;
		if (string.IsNullOrEmpty(className))
		{
			return null;
		}
		int tick = className.IndexOf('`');
		if (tick > 0)
		{
			className = className.Substring(0, tick);
		}
		foreach (string set in ListSets())
		{
			foreach (string f in ListFiles(set))
			{
				if (string.Equals(Path.GetFileNameWithoutExtension(f), className, StringComparison.OrdinalIgnoreCase))
				{
					foundSet = set;
					return f;
				}
			}
		}
		return null;
	}
}
