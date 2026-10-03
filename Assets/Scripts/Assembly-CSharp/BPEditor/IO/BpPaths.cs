using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

// 移植编辑器的平台路径适配层：桌面与 Android 统一入口。
// - Root = persistentDataPath/BPEditor（应用私有，安卓免权限）
// - 内置资源在 StreamingAssets/BPEditor，首启/版本变化时同步到 Root/assets
//   （安卓 StreamingAssets 在 APK 内，必须用 UnityWebRequest 读）
// - 原工具的两个环境变量改为 Root/config.json 的字段
public static class BpPaths
{
	public const string ConfigFileName = "config.json";
	public const string AssetsSubDir = "assets";
	public const string LevelsSubDir = "levels";
	public const string SavesSubDir = "saves";
	public const string ExportsSubDir = "exports";

	[Serializable]
	public class BpConfig
	{
		// 原 BP_EDITOR_EXTERNAL_UNITYPACKAGE_PATH
		public string unityPackagePath = "";
		// 原 BP_EDITOR_RUNTIME_ASSETS_DIR
		public string runtimeAssetsDir = "";
		public string locale = "";
		public string lastLevelFile = "";
	}

	private static string s_root;

	public static string Root
	{
		get
		{
			if (string.IsNullOrEmpty(s_root))
				s_root = Path.Combine(Application.persistentDataPath, "BPEditor");
			return s_root;
		}
	}

	public static string AssetsDir => Path.Combine(Root, AssetsSubDir);
	public static string LevelsDir => Path.Combine(Root, LevelsSubDir);
	public static string SavesDir => Path.Combine(Root, SavesSubDir);
	public static string ExportsDir => Path.Combine(Root, ExportsSubDir);
	public static string ConfigPath => Path.Combine(Root, ConfigFileName);
	public static string StreamingRoot => Path.Combine(Application.streamingAssetsPath, "BPEditor");

	public static bool UsesUnityWebRequest
	{
		get
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			return true;
#else
			return false;
#endif
		}
	}

	public static void EnsureDirs()
	{
		try
		{
			Directory.CreateDirectory(Root);
			Directory.CreateDirectory(AssetsDir);
			Directory.CreateDirectory(LevelsDir);
			Directory.CreateDirectory(SavesDir);
			Directory.CreateDirectory(ExportsDir);
		}
		catch (Exception ex) { Debug.LogError("[BPEditor] EnsureDirs failed: " + ex.Message); }
	}

	// ---------------- config.json ----------------

	public static BpConfig LoadConfig()
	{
		EnsureDirs();
		try
		{
			if (File.Exists(ConfigPath))
			{
				BpConfig c = JsonUtility.FromJson<BpConfig>(File.ReadAllText(ConfigPath));
				if (c != null) return c;
			}
		}
		catch (Exception ex) { Debug.LogWarning("[BPEditor] LoadConfig failed: " + ex.Message); }
		return new BpConfig();
	}

	public static bool SaveConfig(BpConfig cfg)
	{
		EnsureDirs();
		try
		{
			File.WriteAllText(ConfigPath, JsonUtility.ToJson(cfg, true));
			return true;
		}
		catch (Exception ex) { Debug.LogError("[BPEditor] SaveConfig failed: " + ex.Message); return false; }
	}

	// ---------------- StreamingAssets -> Root/assets ----------------

	[Serializable]
	private class AssetManifest
	{
		public string version = "";
		public List<string> files = new List<string>();
	}

	// 同步内置资源（版本不同才拷）。安卓走 UnityWebRequest，桌面走 File.Copy。
	public static IEnumerator SyncStreamingAssets()
	{
		EnsureDirs();
		string manifestRel = "manifest.json";
		string dstManifest = Path.Combine(AssetsDir, manifestRel);
		string dstVersionPath = Path.Combine(AssetsDir, ".version");

		string srcVersion = null;
		string manifestJson = null;
		yield return ReadStreamingText(manifestRel, t => manifestJson = t);
		if (!string.IsNullOrEmpty(manifestJson))
		{
			AssetManifest am = JsonUtility.FromJson<AssetManifest>(manifestJson);
			if (am != null) srcVersion = am.version;
			if (am != null && am.files != null && am.files.Count > 0)
			{
				string dstVersion = File.Exists(dstVersionPath) ? File.ReadAllText(dstVersionPath) : "";
				if (dstVersion == srcVersion && File.Exists(dstManifest))
				{
					Debug.Log("[BPEditor] assets up-to-date: " + srcVersion);
					yield break;
				}
				int ok = 0;
				for (int i = 0; i < am.files.Count; i++)
				{
					string rel = am.files[i];
					string dst = Path.Combine(AssetsDir, rel);
					string dir = Path.GetDirectoryName(dst);
					if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
					yield return CopyStreamingFile(rel, dst);
					if (File.Exists(dst)) ok++;
				}
				try { File.WriteAllText(dstVersionPath, srcVersion ?? ""); } catch { }
				Debug.Log("[BPEditor] synced assets: " + ok + "/" + am.files.Count + " version=" + srcVersion);
				yield break;
			}
		}
		Debug.LogWarning("[BPEditor] StreamingAssets/BPEditor/manifest.json missing; skipped asset sync");
	}

	private static IEnumerator ReadStreamingText(string rel, Action<string> onDone)
	{
		string path = Path.Combine(StreamingRoot, rel);
		if (UsesUnityWebRequest)
		{
			using (UnityWebRequest req = UnityWebRequest.Get(path))
			{
				yield return req.SendWebRequest();
				if (req.result == UnityWebRequest.Result.Success) onDone?.Invoke(req.downloadHandler.text);
				else Debug.LogWarning("[BPEditor] read failed: " + path + " (" + req.error + ")");
			}
		}
		else
		{
			if (File.Exists(path)) onDone?.Invoke(File.ReadAllText(path));
			else Debug.LogWarning("[BPEditor] missing: " + path);
			yield break;
		}
	}

	private static IEnumerator CopyStreamingFile(string rel, string dst)
	{
		string src = Path.Combine(StreamingRoot, rel);
		if (UsesUnityWebRequest)
		{
			using (UnityWebRequest req = UnityWebRequest.Get(src))
			{
				yield return req.SendWebRequest();
				if (req.result == UnityWebRequest.Result.Success)
				{
					try { File.WriteAllBytes(dst, req.downloadHandler.data); }
					catch (Exception ex) { Debug.LogError("[BPEditor] write failed: " + dst + " " + ex.Message); }
				}
				else Debug.LogWarning("[BPEditor] copy failed: " + src + " (" + req.error + ")");
			}
		}
		else
		{
			try
			{
				if (File.Exists(src)) File.Copy(src, dst, true);
				else Debug.LogWarning("[BPEditor] missing: " + src);
			}
			catch (Exception ex) { Debug.LogError("[BPEditor] copy failed: " + src + " " + ex.Message); }
		}
	}

	// 把外部文件（如用户粘贴的路径）拷进内部目录，返回内部路径
	public static string ImportIntoInternal(string externalPath, string subDir)
	{
		try
		{
			if (string.IsNullOrEmpty(externalPath) || !File.Exists(externalPath)) return null;
			string dir = Path.Combine(Root, subDir);
			Directory.CreateDirectory(dir);
			string dst = Path.Combine(dir, Path.GetFileName(externalPath));
			File.Copy(externalPath, dst, true);
			return dst;
		}
		catch (Exception ex)
		{
			Debug.LogError("[BPEditor] ImportIntoInternal failed: " + ex.Message);
			return null;
		}
	}

	public static string Locale()
	{
		BpConfig c = LoadConfig();
		if (!string.IsNullOrEmpty(c.locale)) return c.locale;
		switch (INUnity.Language)
		{
			case SystemLanguage.Chinese:
			case SystemLanguage.ChineseSimplified:
			case SystemLanguage.ChineseTraditional:
				return "zh-CN";
			default:
				return "en-US";
		}
	}
}
