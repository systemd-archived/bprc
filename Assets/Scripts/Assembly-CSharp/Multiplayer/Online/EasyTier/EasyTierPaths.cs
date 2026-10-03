using System;
using System.IO;
using UnityEngine;

namespace BadPiggies.Multiplayer.Online.EasyTier
{
	/// <summary>
	/// Where the bundled EasyTier runtime lives, and whether it can be used at all.
	/// <para>
	/// Every member is exception-free: on a non-Windows platform (or in a headless
	/// tool) the class simply reports <see cref="IsSupportedPlatform"/> = false and a
	/// human readable <see cref="MissingReason"/> instead of throwing.
	/// </para>
	/// <para>
	/// The staged binaries are produced by <c>Tools/Online/fetch-easytier.ps1</c>; see
	/// <c>Assets/StreamingAssets/easytier/README.md</c>.
	/// </para>
	/// </summary>
	public static class EasyTierPaths
	{
		/// <summary>Upstream EasyTier release the staged binaries come from.</summary>
		public const string EasyTierVersion = "2.6.4";

		public const string CoreExecutableName = "easytier-core.exe";
		public const string CliExecutableName = "easytier-cli.exe";

		/// <summary>Wintun driver DLL. Required only when a TUN device is created (i.e. not with <c>--no-tun</c>).</summary>
		public const string WintunDllName = "wintun.dll";

		/// <summary>Npcap capture driver DLL. Npcap is NOT redistributable — see the README.</summary>
		public const string PacketDllName = "Packet.dll";

		/// <summary>WinDivert kernel driver shipped by EasyTier for the UDP broadcast relay.</summary>
		public const string WinDivertDriverName = "WinDivert64.sys";

		/// <summary>Relative path under StreamingAssets.</summary>
		public const string RelativeDirectory = "easytier/win-x64";

		private static readonly object Sync = new object();
		private static string _overrideBaseDirectory;

		/// <summary>
		/// Directory holding <c>easytier-core.exe</c> and friends. Defaults to
		/// <c>&lt;StreamingAssets&gt;/easytier/win-x64</c>; can be redirected with
		/// <see cref="OverrideBaseDirectory"/> (tests, tools, custom builds).
		/// </summary>
		public static string BaseDirectory
		{
			get
			{
				string overridden;
				lock (Sync)
				{
					overridden = _overrideBaseDirectory;
				}
				if (!string.IsNullOrEmpty(overridden))
				{
					return overridden;
				}

				string root = null;
				try
				{
					root = Application.streamingAssetsPath;
				}
				catch (Exception)
				{
					root = null;
				}
				if (string.IsNullOrEmpty(root))
				{
					// Outside the player (unit tests, editor tooling) fall back to the
					// directory next to the executing assembly.
					try
					{
						root = AppDomain.CurrentDomain.BaseDirectory;
					}
					catch (Exception)
					{
						root = ".";
					}
				}
				return Path.Combine(Path.Combine(root, "easytier"), "win-x64");
			}
		}

		/// <summary>
		/// Point the whole EasyTier layer at another directory. Pass null or an empty
		/// string to go back to the StreamingAssets default.
		/// </summary>
		public static void OverrideBaseDirectory(string directory)
		{
			lock (Sync)
			{
				_overrideBaseDirectory = string.IsNullOrEmpty(directory) ? null : directory;
			}
		}

		public static string CoreExecutable
		{
			get { return Path.Combine(BaseDirectory, CoreExecutableName); }
		}

		public static string CliExecutable
		{
			get { return Path.Combine(BaseDirectory, CliExecutableName); }
		}

		public static string WintunDll
		{
			get { return Path.Combine(BaseDirectory, WintunDllName); }
		}

		public static string PacketDll
		{
			get { return Path.Combine(BaseDirectory, PacketDllName); }
		}

		public static string WinDivertDriver
		{
			get { return Path.Combine(BaseDirectory, WinDivertDriverName); }
		}

		public static string Sha256Manifest
		{
			get { return Path.Combine(BaseDirectory, "SHA256.json"); }
		}

		/// <summary>True only on Windows x64 — the only platform EasyTier ships binaries for here.</summary>
		public static bool IsSupportedPlatform
		{
			get
			{
				try
				{
					if (IntPtr.Size != 8)
					{
						return false;
					}
					RuntimePlatform platform = Application.platform;
					return platform == RuntimePlatform.WindowsPlayer || platform == RuntimePlatform.WindowsEditor;
				}
				catch (Exception)
				{
					return false;
				}
			}
		}

		/// <summary>True when both executables are present on disk on a supported platform.</summary>
		public static bool IsInstalled
		{
			get
			{
				if (!IsSupportedPlatform)
				{
					return false;
				}
				return FileExists(CoreExecutable) && FileExists(CliExecutable);
			}
		}

		/// <summary>
		/// <c>wintun.dll</c> is present. Only matters when a TUN device is created, i.e.
		/// when <see cref="EasyTierOptions.NoTun"/> is false.
		/// </summary>
		public static bool HasWintun
		{
			get { return FileExists(WintunDll); }
		}

		/// <summary>
		/// <c>Packet.dll</c> (Npcap) is present. The stock upstream
		/// <c>easytier-core.exe</c> statically imports it, so it must be next to it or
		/// the process will not start.
		/// </summary>
		public static bool HasPacketDll
		{
			get { return FileExists(PacketDll); }
		}

		/// <summary>
		/// Chinese, actionable explanation of why the layer cannot run. Empty string
		/// when <see cref="IsInstalled"/> is true.
		/// </summary>
		public static string MissingReason
		{
			get
			{
				if (!IsSupportedPlatform)
				{
					if (IntPtr.Size != 8)
					{
						return "当前宿主机不是 64 位进程，EasyTier 只有 Windows x64 版本，虚拟局域网联机不可用。";
					}
					return "当前运行平台不是 Windows（或不是 Windows 编辑器/独立版），EasyTier 虚拟局域网联机不可用。";
				}

				bool hasCore = FileExists(CoreExecutable);
				bool hasCli = FileExists(CliExecutable);
				if (hasCore && hasCli)
				{
					return "";
				}

				string missing;
				if (!hasCore && !hasCli)
				{
					missing = "easytier-core.exe 和 easytier-cli.exe";
				}
				else if (!hasCore)
				{
					missing = "easytier-core.exe";
				}
				else
				{
					missing = "easytier-cli.exe";
				}

				return "在 " + BaseDirectory + " 中缺少 " + missing +
					"。请在仓库根目录运行 Tools/Online/fetch-easytier.ps1 重新暂存 EasyTier " +
					EasyTierVersion + " 运行时（可用 -FromDirectory 指向已解压的官方压缩包目录）。";
			}
		}

		/// <summary>
		/// Extra, non-blocking hints about a partially staged runtime. Returns an empty
		/// string when nothing is worth mentioning.
		/// </summary>
		public static string DescribeInstallation()
		{
			if (!IsInstalled)
			{
				return MissingReason;
			}

			System.Text.StringBuilder sb = new System.Text.StringBuilder();
			sb.Append("EasyTier ").Append(EasyTierVersion).Append(" @ ").Append(BaseDirectory);
			if (!HasWintun)
			{
				sb.Append("；缺少 wintun.dll，创建 TUN 网卡（需要管理员权限）时会失败，");
				sb.Append("只能使用 EasyTierOptions.NoTun = true。");
			}
			if (!HasPacketDll)
			{
				sb.Append("；未暂存 Packet.dll（Npcap）：只有目标机器已安装 Npcap，");
				sb.Append("或使用了不依赖 Npcap 的 easytier-core.exe 构建时才能启动。");
			}
			return sb.ToString();
		}

		private static bool FileExists(string path)
		{
			try
			{
				return !string.IsNullOrEmpty(path) && File.Exists(path);
			}
			catch (Exception)
			{
				return false;
			}
		}
	}
}
