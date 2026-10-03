using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

// 阶段 2 前置：关卡文件读取与定位（读写统一走 BpPaths，安卓可用）。
// level_key_from_filename 照搬 domain/level/refs.rs 的同名逻辑
// （后缀剥离+大小写不敏感）；其中"规范化键名"需要 data::assets 索引，留到阶段 3。
public static class BpLevelFile
{
	public static readonly string[] LevelSuffixes = { ".bytes", ".yaml", ".yml", ".toml" };

	public class Info
	{
		public string path = "";
		public bool exists;
		public long size;
		public string levelKey = "";
		public string extension = "";
		public string firstBytesHex = "";
		public string error = "";
		public string textPreview = "";
	}

	// 照搬 refs.rs::level_key_from_filename 的后缀剥离部分
	public static string LevelKeyFromFilename(string filename)
	{
		if (string.IsNullOrEmpty(filename)) return "";
		string basename = filename;
		int slash = basename.LastIndexOfAny(new[] { '/', '\\' });
		if (slash >= 0) basename = basename.Substring(slash + 1);

		string levelKey = basename;
		foreach (string suffix in LevelSuffixes)
		{
			if (levelKey.Length > suffix.Length &&
			    levelKey.Substring(levelKey.Length - suffix.Length).ToLowerInvariant() == suffix)
			{
				levelKey = levelKey.Substring(0, levelKey.Length - suffix.Length);
				break;
			}
		}
		return levelKey.Trim();
	}

	public static Info Inspect(string path)
	{
		Info info = new Info { path = path ?? "" };
		if (string.IsNullOrEmpty(path))
		{
			info.error = "路径为空";
			return info;
		}
		info.exists = File.Exists(path);
		if (!info.exists)
		{
			info.error = "文件不存在";
			return info;
		}
		try
		{
			FileInfo fi = new FileInfo(path);
			info.size = fi.Length;
			info.extension = fi.Extension.ToLowerInvariant();
			info.levelKey = LevelKeyFromFilename(fi.Name);

			byte[] head = ReadBytes(path, 4096);
			info.firstBytesHex = HexPreview(head, 32);

			if (IsTextExtension(info.extension) && head != null)
				info.textPreview = FirstLines(Encoding.UTF8.GetString(head), 8);
		}
		catch (Exception ex)
		{
			info.error = ex.Message;
		}
		return info;
	}

	public static bool IsTextExtension(string ext)
	{
		return ext == ".yaml" || ext == ".yml" || ext == ".toml" || ext == ".json" || ext == ".xml" || ext == ".txt";
	}

	// 读取最多 maxBytes 字节（maxBytes<=0 表示整文件）
	public static byte[] ReadBytes(string path, int maxBytes)
	{
		try
		{
			using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
			{
				if (maxBytes <= 0)
				{
					byte[] all = new byte[fs.Length];
					int read = 0;
					while (read < all.Length)
					{
						int n = fs.Read(all, read, all.Length - read);
						if (n <= 0) break;
						read += n;
					}
					if (read != all.Length) Array.Resize(ref all, read);
					return all;
				}
				byte[] buf = new byte[Math.Min(maxBytes, (int)Math.Min(int.MaxValue, fs.Length))];
				int got = fs.Read(buf, 0, buf.Length);
				if (got != buf.Length) Array.Resize(ref buf, got);
				return buf;
			}
		}
		catch (Exception ex)
		{
			Debug.LogError("[BPEditor] ReadBytes failed: " + path + " : " + ex.Message);
			return null;
		}
	}

	public static string HexPreview(byte[] data, int count)
	{
		if (data == null || data.Length == 0) return "";
		int n = Mathf.Min(count, data.Length);
		StringBuilder sb = new StringBuilder(n * 3);
		for (int i = 0; i < n; i++) sb.Append(data[i].ToString("x2")).Append(' ');
		if (data.Length > n) sb.Append("...");
		return sb.ToString();
	}

	private static string FirstLines(string text, int lines)
	{
		string[] all = text.Replace("\r\n", "\n").Split('\n');
		StringBuilder sb = new StringBuilder();
		for (int i = 0; i < Mathf.Min(lines, all.Length); i++)
		{
			if (i > 0) sb.Append('\n');
			sb.Append(all[i]);
		}
		return sb.ToString();
	}

	// 列出内部目录（安卓上用户可直接把文件放进去再选）
	public static List<string> ListInternalFiles(string dir)
	{
		List<string> list = new List<string>();
		try
		{
			if (!Directory.Exists(dir)) return list;
			foreach (string f in Directory.GetFiles(dir))
			{
				string ext = Path.GetExtension(f).ToLowerInvariant();
				foreach (string suffix in LevelSuffixes)
					if (ext == suffix) { list.Add(f); break; }
			}
			list.Sort();
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[BPEditor] ListInternalFiles failed: " + ex.Message);
		}
		return list;
	}
}
