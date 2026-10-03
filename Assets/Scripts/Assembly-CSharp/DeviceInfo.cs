using UnityEngine;

public class DeviceInfo
{
	public enum DeviceFamily
	{
		Ios = 0,
		Android = 1,
		Pc = 2,
		Osx = 3,
		BB10 = 4,
		WP8 = 5
	}

	public static bool UsesTouchInput
	{
		get
		{
			RuntimePlatform platform = Application.platform;
			if (platform != RuntimePlatform.IPhonePlayer)
			{
				return platform == RuntimePlatform.Android;
			}
			return true;
		}
	}

	public static DeviceFamily ActiveDeviceFamily => DeviceFamily.Android;

	public static bool IsDesktop
	{
		get
		{
			RuntimePlatform platform = Application.platform;
			if (platform != RuntimePlatform.OSXEditor && platform != RuntimePlatform.OSXPlayer && platform != RuntimePlatform.WindowsPlayer)
			{
				return platform == RuntimePlatform.WindowsEditor;
			}
			return true;
		}
	}
}
