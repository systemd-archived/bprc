using UnityEngine;

namespace BadPiggies.Multiplayer
{
public static class MultiplayerTraffic
{
	public const int MessageOverheadBytes = 15;

	private const float ReportInterval = 1f;

	private static long s_outBytes;

	private static long s_inBytes;

	private static int s_outMessages;

	private static int s_inMessages;

	private static long s_eventOutBytes;

	private static long s_eventInBytes;

	private static int s_eventOutMessages;

	private static float s_windowStart;

	private static bool s_running;

	public static float PeakOutBytesPerSecond { get; private set; }

	public static float PeakInBytesPerSecond { get; private set; }

	public static string LastReport { get; private set; } = string.Empty;

	public static string PeakSummary => "out " + Rate(PeakOutBytesPerSecond) + " / in " + Rate(PeakInBytesPerSecond);

	public static void Begin()
	{
		s_running = true;
		s_windowStart = Time.realtimeSinceStartup;
		PeakOutBytesPerSecond = 0f;
		PeakInBytesPerSecond = 0f;
		LastReport = string.Empty;
		ClearWindow();
	}

	public static void End()
	{
		if (s_running)
		{
			s_running = false;
			Debug.Log("[Multiplayer] Traffic peak: out " + Rate(PeakOutBytesPerSecond) + ", in " + Rate(PeakInBytesPerSecond) + ".");
		}
	}

	public static void CountSent(int payloadBytes)
	{
		s_outBytes += payloadBytes + 15;
		s_outMessages++;
	}

	public static void CountReceived(int payloadBytes)
	{
		s_inBytes += payloadBytes + 15;
		s_inMessages++;
	}

	public static void CountEventSent(int payloadBytes)
	{
		s_eventOutBytes += payloadBytes + 15;
		s_eventOutMessages++;
	}

	public static void CountEventReceived(int payloadBytes)
	{
		s_eventInBytes += payloadBytes + 15;
	}

	public static void Tick()
	{
		if (s_running)
		{
			float num = Time.realtimeSinceStartup - s_windowStart;
			if (!(num < 1f))
			{
				float num2 = (float)(s_outBytes + s_eventOutBytes) / num;
				float num3 = (float)(s_inBytes + s_eventInBytes) / num;
				PeakOutBytesPerSecond = Mathf.Max(PeakOutBytesPerSecond, num2);
				PeakInBytesPerSecond = Mathf.Max(PeakInBytesPerSecond, num3);
				LastReport = $"[Multiplayer] Traffic out {Rate(num2)} in {Rate(num3)} | snapshots {s_outMessages} sent / " + $"{s_inMessages} recv per s | events {s_eventOutMessages} | UDP+KCP headers not counted";
				Debug.Log(LastReport);
				s_windowStart = Time.realtimeSinceStartup;
				ClearWindow();
			}
		}
	}

	private static void ClearWindow()
	{
		s_outBytes = 0L;
		s_inBytes = 0L;
		s_outMessages = 0;
		s_inMessages = 0;
		s_eventOutBytes = 0L;
		s_eventInBytes = 0L;
		s_eventOutMessages = 0;
	}

	private static string Rate(float bytesPerSecond)
	{
		return $"{bytesPerSecond / 1024f:0.0} KB/s ({bytesPerSecond * 8f / 1000f:0} kbit/s)";
	}
}
}
