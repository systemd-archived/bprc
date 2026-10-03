using System;
using System.Collections;
using UnityEngine;

// 跨平台文件选择：Windows/Linux 用 ShellFileDialogs；Android 用系统文件选择器(ACTION_OPEN_DOCUMENT)，
// 由 Android 侧 FileDialogBridge 复制选中文件到缓存目录，Unity 协程轮询结果后回调。
public static class INFilePicker
{
	public static void PickFile(string filterDesc, string filterExt, Action<string> onPicked)
	{
		PickFile(filterDesc, filterExt, UnityEngine.Application.dataPath, onPicked);
	}

	/// <summary>
	/// Cross-platform file pick. <paramref name="initialDirectory"/> is where a desktop dialog starts; it
	/// is ignored on Android, whose system picker reopens where the user last was.
	/// </summary>
	public static void PickFile(string filterDesc, string filterExt, string initialDirectory, Action<string> onPicked)
	{
		if (onPicked == null)
			return;
#if UNITY_ANDROID && !UNITY_EDITOR
		PickAndroid(filterDesc, filterExt, onPicked);
#else
		PickDesktop(filterDesc, filterExt, initialDirectory, onPicked);
#endif
	}

#if UNITY_ANDROID && !UNITY_EDITOR
	private static bool s_androidDialogOpen;

	public static void SaveFileAndroid(string source, string name, Action<string, string> onSaved)
	{
		StartAndroidDialog(bridge => bridge.CallStatic("SaveFile", source, name), onSaved);
	}

	public static void OpenFolderAndroid(string path, Action<string, string> onClosed)
	{
		StartAndroidDialog(bridge => bridge.CallStatic("OpenFolder", path), onClosed);
	}

	private static void PickAndroid(string filterDesc, string filterExt, Action<string> onPicked)
	{
		try
		{
			string mime = "*/*";
			if (!string.IsNullOrEmpty(filterExt))
			{
				mime = filterExt.ToLowerInvariant() switch
				{
					"*.png" => "image/png",
					"*.jpg" or "*.jpeg" => "image/jpeg",
					"*.bmp" => "image/bmp",
					"*.tga" => "image/x-tga",
					"*.gif" => "image/gif",
					"*.mp4" => "video/mp4",
					"*.mov" => "video/quicktime",
					"*.webm" => "video/webm",
					"*.m4v" => "video/x-m4v",
					"*.avi" => "video/x-msvideo",
					"*" or "*.*" => "*/*",
					_ => "*/*",
				};
			}
			StartAndroidDialog(bridge =>
			{
				bridge.CallStatic("SetMimeType", mime);
				bridge.CallStatic("OpenFile");
			}, (result, error) =>
			{
				if (!string.IsNullOrEmpty(error))
				{
					Debug.LogWarning("INFilePicker: " + error);
					Innovation.BP.Feedback("BP> 文件选择失败：" + error);
				}
				onPicked(result);
			});
		}
		catch (Exception ex)
		{
			Debug.LogError("INFilePicker(PickFile) android: " + ex.Message);
			onPicked(null);
		}
	}

	private static void StartAndroidDialog(Action<AndroidJavaClass> launch, Action<string, string> onComplete)
	{
		if (s_androidDialogOpen)
		{
			onComplete?.Invoke(null, "已有文件选择窗口打开，请先完成或取消选择");
			return;
		}
		s_androidDialogOpen = true;
		try
		{
			using (AndroidJavaClass bridge = new AndroidJavaClass("com.innovation.filedialog.FileDialogBridge"))
			{
				launch(bridge);
			}
			CoroutineRunner.Instance.StartCoroutine(PollAndroidResult(onComplete));
		}
		catch (Exception ex)
		{
			s_androidDialogOpen = false;
			onComplete?.Invoke(null, ex.Message);
		}
	}

	private static IEnumerator PollAndroidResult(Action<string, string> onComplete)
	{
		bool done = false;
		string result = null;
		string error = null;
		AndroidJavaClass bridge = null;
		try
		{
			bridge = new AndroidJavaClass("com.innovation.filedialog.FileDialogBridge");
			// Do not time out while Android has paused Unity for the system picker or a cloud copy.
			while (!done)
			{
				yield return new WaitForSecondsRealtime(0.1f);
				try
				{
					if (bridge.CallStatic<bool>("IsReady"))
					{
						result = bridge.CallStatic<string>("GetResult");
						error = bridge.CallStatic<string>("GetError");
						done = true;
					}
				}
				catch (Exception ex)
				{
					error = ex.Message;
					done = true;
				}
			}
		}
		finally
		{
			s_androidDialogOpen = false;
			if (bridge != null)
				bridge.Dispose();
		}
		onComplete?.Invoke(string.IsNullOrEmpty(result) ? null : result, error);
	}
#else
	private static void PickDesktop(string filterDesc, string filterExt, string initialDirectory, Action<string> onPicked)
	{
		try
		{
			ShellFileDialogs.Filter[] filters = new ShellFileDialogs.Filter[1]
			{
				new ShellFileDialogs.Filter(filterDesc, filterExt)
			};
			string startIn = string.IsNullOrEmpty(initialDirectory) ? UnityEngine.Application.dataPath : initialDirectory;
			string path = ShellFileDialogs.FileOpenDialog.ShowSingleSelectDialog(IntPtr.Zero, string.Empty, startIn, string.Empty, filters, 0);
			onPicked(string.IsNullOrEmpty(path) ? null : path);
		}
		catch (Exception ex)
		{
			Debug.LogError("INFilePicker(PickFile) desktop: " + ex.Message);
			onPicked(null);
		}
	}
#endif
}
