using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

// 1:1 移植 badpiggies-editor 的 data/assets/unitypackage_loader.rs。
//
// 逻辑保持一致：以 Unity pathname（"Assets/..."）寻址；索引 pathname↔guid↔asset 字节；
// `list_pathnames(prefix, suffix)` 排序返回；guid 取自 asset.meta 的 `guid:` 行，
// 缺失时回退为 unitypackage 内的 <guid> 目录名。
//
// ★ 平台适配（唯一偏差，理由：安卓内存/体积）★
// Rust 把 72MB .unitypackage 解压后的**全部** asset 字节常驻内存（HashMap<String, Vec<u8>>），
// 在安卓上很容易 OOM。本移植改为：gzip 解压一次到 `BpPaths.AssetsDir/runtime/` 下的 tar 文件，
// 索引只记录每个 asset 的 (offset, length)，读取时按需 seek。
// 对外 API 语义与 Rust 完全一致。
//
// 与 Rust 的另一处差别（宿主健壮性）：Rust 缺包时 `panic!`，这里只记录错误并返回空/ null，
// 使编辑器在资源未就绪时仍可打开（避免安卓直接崩）。

public static class BpUnityPackageLoader
{
	// 原 DEFAULT_UNITYPACKAGE_RELATIVE_PATH = "assets/data/Bad-Piggies-2.3.6-Unity-Windows.unitypackage"
	public const string DefaultPackageName = "Bad-Piggies-2.3.6-Unity-Windows.unitypackage";

	// 解压后的 tar 文件名（平台适配产物）
	private const string DecompressedTarName = "unitypackage.tar";

	private class PackageEntry
	{
		public string pathname;
		public string guid;          // 最终 guid（meta 优先，缺失回退目录名）
		public long assetOffset;
		public int assetLength;
	}

	// 构建期的临时条目（按 <guid> 目录聚合）
	private class RawEntry
	{
		public string pathname;
		public long assetOffset = -1;
		public int assetLength;
		public long metaOffset = -1;
		public int metaLength;
	}

	private static bool s_tried;
	private static bool s_available;
	private static string s_tarPath;

	private static readonly Dictionary<string, string> s_byPathname = new Dictionary<string, string>();
	private static readonly Dictionary<string, string> s_byGuid = new Dictionary<string, string>();
	private static readonly Dictionary<string, PackageEntry> s_entryByPathname = new Dictionary<string, PackageEntry>();

	public static bool IsAvailable { get { EnsureIndex(); return s_available; } }
	public static string TarPath { get { EnsureIndex(); return s_tarPath; } }

	// ---------------- 路径解析 ----------------

	// 优先级：config.unityPackagePath → Root/assets/data/<默认名> → StreamingAssets/BPEditor/data/<默认名>
	public static string ResolvePackagePath()
	{
		try
		{
			BpPaths.BpConfig cfg = BpPaths.LoadConfig();
			if (cfg != null && !string.IsNullOrEmpty(cfg.unityPackagePath) && File.Exists(cfg.unityPackagePath))
				return cfg.unityPackagePath;
		}
		catch (Exception) { }

		string internalPath = Path.Combine(BpPaths.AssetsDir, "data", DefaultPackageName);
		if (File.Exists(internalPath)) return internalPath;

		string streamingPath = Path.Combine(BpPaths.StreamingRoot, "data", DefaultPackageName);
		if (File.Exists(streamingPath)) return streamingPath;

		return internalPath;
	}

	// ---------------- 索引构建 ----------------

	public static void Reset()
	{
		s_tried = false;
		s_available = false;
		s_tarPath = null;
		s_byPathname.Clear();
		s_byGuid.Clear();
		s_entryByPathname.Clear();
	}

	private static void EnsureIndex()
	{
		if (s_tried) return;
		s_tried = true;
		try { Build(); }
		catch (Exception ex)
		{
			Debug.LogError("[BPEditor] unitypackage index failed: " + ex.Message);
			s_available = false;
		}
	}

	private static void Build()
	{
		string pkg = ResolvePackagePath();
		if (!File.Exists(pkg))
		{
			Debug.LogWarning("[BPEditor] unitypackage not found: " + pkg
				+ " (place " + DefaultPackageName + " under StreamingAssets/BPEditor/data/ or set config.unityPackagePath)");
			return;
		}

		string runtimeDir = Path.Combine(BpPaths.AssetsDir, "runtime");
		Directory.CreateDirectory(runtimeDir);
		string tarPath = Path.Combine(runtimeDir, DecompressedTarName);

		// 仅在 tar 缺失或比包旧时重新解压
		bool needExtract = true;
		if (File.Exists(tarPath))
		{
			try { needExtract = File.GetLastWriteTimeUtc(tarPath) < File.GetLastWriteTimeUtc(pkg); }
			catch (Exception) { needExtract = true; }
		}
		if (needExtract) ExtractGzipToFile(pkg, tarPath);

		s_tarPath = tarPath;
		ParseTarIndex(tarPath);
		s_available = s_entryByPathname.Count > 0;
		Debug.Log("[BPEditor] unitypackage index: " + s_entryByPathname.Count + " assets (" + Path.GetFileName(pkg) + ")");
	}

	private static void ExtractGzipToFile(string srcPath, string dstPath)
	{
		using (FileStream src = File.OpenRead(srcPath))
		using (GZipStream gz = new GZipStream(src, CompressionMode.Decompress))
		using (FileStream dst = File.Create(dstPath))
		{
			gz.CopyTo(dst);
		}
	}

	// tar：512 字节头（name 0..100 / size 124..136 八进制 / typeflag 156 / prefix 345..500），
	// 数据按 512 对齐。GNU 长名（typeflag 'L'）追加支持。
	private static void ParseTarIndex(string tarPath)
	{
		Dictionary<string, RawEntry> perGuidFolder = new Dictionary<string, RawEntry>();

		using (FileStream fs = File.OpenRead(tarPath))
		{
			byte[] header = new byte[512];
			string pendingLongName = null;

			while (true)
			{
				long headerPos = fs.Position;
				int read = ReadFully(fs, header, 512);
				if (read == 0) break;
				if (read < 512) break;
				if (IsZeroBlock(header)) break;   // 两个连续零块 = 归档结束

				string name = ReadCString(header, 0, 100);
				string prefix = ReadCString(header, 345, 155);
				if (!string.IsNullOrEmpty(prefix)) name = prefix + "/" + name;
				long size = ReadOctal(header, 124, 12);
				char typeflag = (char)header[156];
				long dataPos = headerPos + 512;

				if (typeflag == 'L')
				{
					// GNU long name：本条数据即下一条目的名字
					pendingLongName = ReadUTF8(fs, dataPos, size).TrimEnd('\0');
					fs.Position = dataPos + Pad512(size);
					continue;
				}
				if (typeflag == 'K' || typeflag == 'x' || typeflag == 'g')
				{
					// 长链接名 / pax 扩展头：跳过（unitypackage 通常不含）
					fs.Position = dataPos + Pad512(size);
					continue;
				}

				string entryName = pendingLongName != null ? pendingLongName : name;
				pendingLongName = null;

				if (typeflag == '0' || typeflag == '\0')
				{
					string folder, leaf;
					SplitEntry(entryName, out folder, out leaf);
					if (folder != null)
					{
						RawEntry slot;
						if (!perGuidFolder.TryGetValue(folder, out slot))
						{
							slot = new RawEntry();
							perGuidFolder[folder] = slot;
						}
						if (leaf == "pathname")
						{
							slot.pathname = ReadUTF8(fs, dataPos, size).Trim();
							if (slot.pathname.Length == 0) slot.pathname = null;
						}
						else if (leaf == "asset")
						{
							slot.assetOffset = dataPos;
							slot.assetLength = (int)size;
						}
						else if (leaf == "asset.meta")
						{
							slot.metaOffset = dataPos;
							slot.metaLength = (int)size;
						}
					}
				}

				fs.Position = dataPos + Pad512(size);
			}

			// 汇总（需要已定位到 tar 文件，故在此函数内完成读取）
			foreach (KeyValuePair<string, RawEntry> kv in perGuidFolder)
			{
				RawEntry e = kv.Value;
				if (e.pathname == null || e.assetOffset < 0) continue;

				string guid = kv.Key;
				if (e.metaOffset >= 0)
				{
					string metaText = ReadUTF8(fs, e.metaOffset, e.metaLength);
					string fromMeta = ReadGuidFromMetaText(metaText);
					if (!string.IsNullOrEmpty(fromMeta)) guid = fromMeta;
				}

				PackageEntry pe = new PackageEntry();
				pe.pathname = e.pathname;
				pe.guid = guid;
				pe.assetOffset = e.assetOffset;
				pe.assetLength = e.assetLength;

				s_byGuid[guid] = e.pathname;
				s_byPathname[e.pathname] = guid;
				s_entryByPathname[e.pathname] = pe;
			}
		}
	}

	private static void SplitEntry(string entryName, out string folder, out string leaf)
	{
		folder = null;
		leaf = null;
		if (entryName == null) return;
		string[] parts = entryName.Split('/');
		if (parts.Length != 2) return;   // Rust: parts.len() != 2 → skip
		folder = parts[0];
		leaf = parts[1];
	}

	private static string ReadGuidFromMetaText(string text)
	{
		if (text == null) return null;
		string[] lines = text.Split('\n');
		foreach (string raw in lines)
		{
			string line = raw.Trim();
			if (line.StartsWith("guid:", StringComparison.Ordinal))
			{
				string value = line.Substring(5).Trim();
				if (value.Length > 0) return value;
			}
		}
		return null;
	}

	// ---------------- tar 读取工具 ----------------

	private static int ReadFully(FileStream fs, byte[] buf, int count)
	{
		int total = 0;
		while (total < count)
		{
			int n = fs.Read(buf, total, count - total);
			if (n <= 0) break;
			total += n;
		}
		return total;
	}

	private static bool IsZeroBlock(byte[] block)
	{
		for (int i = 0; i < block.Length; i++) if (block[i] != 0) return false;
		return true;
	}

	private static long Pad512(long size)
	{
		return ((size + 511) / 512) * 512;
	}

	private static string ReadCString(byte[] buf, int offset, int max)
	{
		int end = offset;
		int limit = offset + max;
		while (end < limit && buf[end] != 0) end++;
		return Encoding.UTF8.GetString(buf, offset, end - offset);
	}

	private static long ReadOctal(byte[] buf, int offset, int length)
	{
		long value = 0;
		int i = offset;
		int end = offset + length;
		while (i < end && (buf[i] == ' ' || buf[i] == 0)) i++;
		for (; i < end; i++)
		{
			byte b = buf[i];
			if (b < '0' || b > '7') break;
			value = value * 8 + (b - '0');
		}
		return value;
	}

	private static string ReadUTF8(FileStream fs, long offset, long length)
	{
		if (length <= 0) return "";
		byte[] buf = new byte[length];
		fs.Position = offset;
		ReadFully(fs, buf, (int)length);
		return Encoding.UTF8.GetString(buf);
	}

	private static byte[] ReadBytesAt(FileStream fs, long offset, int length)
	{
		if (offset < 0 || length <= 0) return new byte[0];
		byte[] buf = new byte[length];
		fs.Position = offset;
		int got = ReadFully(fs, buf, length);
		if (got == length) return buf;
		byte[] trimmed = new byte[got];
		Array.Copy(buf, trimmed, got);
		return trimmed;
	}

	// ---------------- 对外 API（与 Rust 同名语义）----------------

	public static string GuidForPathname(string pathname)
	{
		EnsureIndex();
		string guid;
		if (pathname != null && s_byPathname.TryGetValue(pathname, out guid)) return guid;
		return null;
	}

	public static string PathnameForGuid(string guid)
	{
		EnsureIndex();
		string pathname;
		if (guid != null && s_byGuid.TryGetValue(guid, out pathname)) return pathname;
		return null;
	}

	public static byte[] ReadPathname(string pathname)
	{
		EnsureIndex();
		if (pathname == null) return null;
		PackageEntry e;
		if (!s_entryByPathname.TryGetValue(pathname, out e)) return null;
		if (e.assetOffset < 0) return null;
		try
		{
			using (FileStream fs = File.OpenRead(s_tarPath))
				return ReadBytesAt(fs, e.assetOffset, e.assetLength);
		}
		catch (Exception ex)
		{
			Debug.LogError("[BPEditor] read asset failed: " + pathname + " " + ex.Message);
			return null;
		}
	}

	public static string ReadPathnameText(string pathname)
	{
		byte[] bytes = ReadPathname(pathname);
		return bytes == null ? null : Encoding.UTF8.GetString(bytes);
	}

	public static string ReadGuidText(string guid)
	{
		string pathname = PathnameForGuid(guid);
		return pathname == null ? null : ReadPathnameText(pathname);
	}

	// 遍历匹配 prefix/suffix 的完整 pathname，排序返回
	public static List<string> ListPathnames(string prefix, string suffix)
	{
		EnsureIndex();
		List<string> outList = new List<string>();
		foreach (string p in s_byPathname.Keys)
		{
			if (p.StartsWith(prefix, StringComparison.Ordinal) && p.EndsWith(suffix, StringComparison.Ordinal))
				outList.Add(p);
		}
		outList.Sort(StringComparer.Ordinal);
		return outList;
	}

	public static int Count { get { EnsureIndex(); return s_entryByPathname.Count; } }
}
