using System;
using System.IO;
using UnityEngine;

public static class INFileSystem
{
	private static string m_root;

	// The launcher owns the optional permission request. Resolve a writable root
	// here without opening settings while Unity is initializing the first scene.
	public static void EnsureAndroidStoragePermission()
	{
#if UNITY_ANDROID && !UNITY_EDITOR
		_ = Root;
#endif
	}

	public static string Root
	{
		get
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			if (!string.IsNullOrEmpty(m_root)) return m_root;
			m_root = GetAndroidRoot();
#else
			m_root = GetDefaultRoot();
			if (!string.IsNullOrEmpty(m_root) && !Directory.Exists(m_root))
			{
				Directory.CreateDirectory(m_root);
			}
#endif
			return m_root;
		}
	}

#if UNITY_ANDROID && !UNITY_EDITOR
	private static string GetAndroidRoot()
	{
		string publicRoot = null;
		try
		{
			publicRoot = GetDefaultRoot();
		}
		catch (Exception exception)
		{
			Debug.LogWarning("[INStorage] Public storage is unavailable: " + exception.Message);
		}

		string appRoot = string.IsNullOrEmpty(Application.persistentDataPath) ? null :
			Path.Combine(Application.persistentDataPath, "GameData");
		string internalRoot = null;
		try
		{
			using (AndroidJavaClass player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
			using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
			using (AndroidJavaObject directory = activity.Call<AndroidJavaObject>("getFilesDir"))
			{
				internalRoot = Path.Combine(directory.Call<string>("getAbsolutePath"), "GameData");
			}
		}
		catch (Exception exception)
		{
			Debug.LogWarning("[INStorage] Internal storage path is unavailable: " + exception.Message);
		}

		return ResolveAndroidRoot(publicRoot, appRoot, internalRoot);
	}

	private static string ResolveAndroidRoot(string publicRoot, params string[] appRoots)
	{
		// Once app storage has been selected, keep using it after permissions change.
		// Switching back automatically would hide newer saves behind older public files.
		foreach (string root in appRoots)
		{
			if (!Directory.Exists(root) || !TryEnsureWritable(root)) continue;
			CopyExistingPublicSaves(publicRoot, root);
			return root;
		}
		if (TryEnsureWritable(publicRoot)) return publicRoot;
		foreach (string root in appRoots)
		{
			if (!TryEnsureWritable(root)) continue;
			CopyExistingPublicSaves(publicRoot, root);
			Debug.LogWarning("[INStorage] Public save folder is not writable. Using app storage: " + root);
			return root;
		}
		throw new IOException("No writable game data directory is available.");
	}

	private static void CopyExistingPublicSaves(string publicRoot, string appRoot)
	{
		try
		{
			if (Directory.Exists(publicRoot)) CopyMissing(publicRoot, appRoot);
		}
		catch (Exception exception)
		{
			Debug.LogWarning("[INStorage] Existing public saves could not be copied: " + exception.Message);
		}
	}

	private static bool TryEnsureWritable(string root)
	{
		if (string.IsNullOrEmpty(root)) return false;
		string probe = null;
		try
		{
			Directory.CreateDirectory(root);
			probe = Path.Combine(root, ".storage-check-" + Guid.NewGuid().ToString("N"));
			using (FileStream stream = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None))
			{
				stream.WriteByte(0);
			}
			File.Delete(probe);
			return true;
		}
		catch (Exception)
		{
			return false;
		}
		finally
		{
			if (probe != null)
			{
				try { File.Delete(probe); } catch (Exception) { }
			}
		}
	}

	private static void CopyMissing(string source, string destination)
	{
		foreach (string file in Directory.GetFiles(source))
		{
			string target = Path.Combine(destination, Path.GetFileName(file));
			if (!File.Exists(target)) File.Copy(file, target, false);
		}
		foreach (string directory in Directory.GetDirectories(source))
		{
			string target = Path.Combine(destination, Path.GetFileName(directory));
			Directory.CreateDirectory(target);
			CopyMissing(directory, target);
		}
	}
#endif

	private static string GetDefaultRoot()
	{
#if UNITY_ANDROID && !UNITY_EDITOR
		using (AndroidJavaClass androidJavaClass = new AndroidJavaClass("android.os.Environment"))
		{
			using (AndroidJavaObject androidJavaObject = androidJavaClass.CallStatic<AndroidJavaObject>("getExternalStoragePublicDirectory", androidJavaClass.GetStatic<string>("DIRECTORY_DOCUMENTS")))
			{
				if (androidJavaObject == null)
				{
					return string.Empty;
				}
				string text = androidJavaObject.Call<string>("getAbsolutePath");
				if (string.IsNullOrEmpty(text))
				{
					return string.Empty;
				}
				return Path.Combine(text, Application.productName);
			}
		}
#else
		return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), Application.productName);
#endif
	}
}
