using System.Diagnostics;
using UnityEngine;
using UnityEngine.Profiling;

public class INDataDetector : MonoBehaviour
{
	private static int s_currentFrameRate = -1;

	private static bool s_newAlgorithmActive;

	private float m_smoothFps;

	private float m_prevSmoothFps;

	private float m_frameRateCooldown;

	private int m_lowFpsStreak;

	private int m_recoverStreak;

	private int m_lastDirection;

	private int[] m_directionHistory = new int[12];

	private int m_directionIndex;

	private bool m_isOscillating;

	private bool m_applicationPaused;
	private bool m_applicationFocused = true;
	private bool m_interfaceOpen;
	private int m_lastSampleVersion = -1;
	private int m_configuredFrameRate = -1;
	private float m_memorySampleTimer;

	private class FPSCounter
	{
		private float m_updateInterval;

		private float m_frameCount;

		private Stopwatch m_stopwatch;

		private float m_result;

		public bool IsRunning => m_stopwatch.IsRunning;

		public float FPS => m_result;

		public int SampleVersion { get; private set; }

		public FPSCounter(float updateInterval)
		{
			m_updateInterval = updateInterval;
			m_frameCount = 0f;
			m_stopwatch = new Stopwatch();
		}

		public void Tick()
		{
			if (m_stopwatch.IsRunning)
			{
				m_frameCount += 1f;
				float num = (float)m_stopwatch.ElapsedMilliseconds / 1000f;
				if (num >= m_updateInterval)
				{
					m_result = m_frameCount / num;
					SampleVersion++;
					m_frameCount = 0f;
					m_stopwatch.Restart();
				}
			}
		}

		public void Start()
		{
			Reset();
			m_stopwatch.Start();
		}

		public void Stop()
		{
			m_stopwatch.Stop();
		}

		public void Reset()
		{
			m_frameCount = 0f;
			m_result = 0f;
			m_stopwatch.Reset();
		}
	}

	private FPSCounter m_counter;

	private FPSCounter m_fixedCounter;

	public float FPS => m_counter.FPS;

	public float FixedFPS => m_fixedCounter.FPS;

	public static int CurrentFrameRate => s_currentFrameRate;

	public float AllocatedManagedHeapSize { get; private set; }

	public float ReservedManagedHeapSize { get; private set; }

	public float TotalAllocatedMemorySize { get; private set; }

	public float TotalReservedMemorySize { get; private set; }

	public static INDataDetector Instance { get; private set; }

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			enabled = false;
			Destroy(gameObject);
			return;
		}
		Instance = this;
		transform.SetParent(null, worldPositionStays: true);
		Object.DontDestroyOnLoad(this);
		m_counter = new FPSCounter(0.5f);
		m_counter.Start();
		m_fixedCounter = new FPSCounter(0.5f);
		m_fixedCounter.Start();
	}

	private INAppInterface m_interface;

	private System.Collections.IEnumerator Start()
	{
		while (INAppInterface.Instance == null) { yield return null; }
		m_interface = INAppInterface.Instance;
		m_interface.AppInterfaceEnabled += OnAppInterfaceEnabled;
		m_interface.AppInterfaceDisabled += OnAppInterfaceDisabled;
	}

	private void OnDestroy()
	{
		if (Instance == this) Instance = null;
		if (m_interface == null) { return; }
		m_interface.AppInterfaceEnabled -= OnAppInterfaceEnabled;
		m_interface.AppInterfaceDisabled -= OnAppInterfaceDisabled;
	}

	private void OnAppInterfaceEnabled()
	{
		m_interfaceOpen = true;
		RefreshSamplingState();
	}

	private void OnAppInterfaceDisabled()
	{
		m_interfaceOpen = false;
		RefreshSamplingState();
	}

	private void OnApplicationPause(bool paused)
	{
		m_applicationPaused = paused;
		RefreshSamplingState();
	}

	private void OnApplicationFocus(bool focused)
	{
		m_applicationFocused = focused;
		RefreshSamplingState();
	}

	private void RefreshSamplingState()
	{
		if (m_counter == null || m_fixedCounter == null) return;
		bool sampling = !m_applicationPaused && m_applicationFocused && !m_interfaceOpen;
		if (sampling == m_counter.IsRunning) return;
		if (sampling)
		{
			m_counter.Start();
			m_fixedCounter.Start();
			ResetAdaptiveFrame();
		}
		else
		{
			m_counter.Reset();
			m_fixedCounter.Reset();
		}
	}

	private void ResetAdaptiveFrame()
	{
		m_smoothFps = m_prevSmoothFps = 0f;
		m_lowFpsStreak = m_recoverStreak = m_lastDirection = m_directionIndex = 0;
		m_isOscillating = false;
		System.Array.Clear(m_directionHistory, 0, m_directionHistory.Length);
		m_lastSampleVersion = -1;
		m_frameRateCooldown = 1f;
		StarSeaSettings settings = INUserSettings.Instance?.StarSeaSettings;
		m_configuredFrameRate = settings != null ? settings.FixedFrameRate : -1;
		s_currentFrameRate = settings != null && settings.CustomPhysicsFrame ? settings.FixedFrameRate : -1;
		s_newAlgorithmActive = s_currentFrameRate > 0;
		if (settings != null) Time.fixedDeltaTime = 1f / settings.FixedFrameRate;
	}

	private void FixedUpdate()
	{
		m_fixedCounter.Tick();
	}

	private void Update()
	{
		m_counter.Tick();
		// These are display statistics, not physics inputs. Sampling at every physics step was costly.
		m_memorySampleTimer -= Time.unscaledDeltaTime;
		if (m_counter.IsRunning && m_memorySampleTimer <= 0f)
		{
			m_memorySampleTimer = 1f;
			AllocatedManagedHeapSize = (float)Profiler.GetMonoUsedSizeLong() / 1048576f;
			ReservedManagedHeapSize = (float)Profiler.GetMonoHeapSizeLong() / 1048576f;
			TotalAllocatedMemorySize = (float)Profiler.GetTotalAllocatedMemoryLong() / 1048576f;
			TotalReservedMemorySize = (float)Profiler.GetTotalReservedMemoryLong() / 1048576f;
		}
		ApplyAdaptivePhysicsFrame();
	}

	private void ApplyAdaptivePhysicsFrame()
	{
		StarSeaSettings settings = INUserSettings.Instance?.StarSeaSettings;
		if (settings == null)
		{
			s_currentFrameRate = -1;
			return;
		}
		if (!settings.CustomPhysicsFrame)
		{
			if (s_newAlgorithmActive)
			{
				Time.fixedDeltaTime = 1f / settings.FixedFrameRate;
				s_newAlgorithmActive = false;
			}
			s_currentFrameRate = -1;
			return;
		}
		int targetFrameRate = settings.FixedFrameRate;
		if (s_currentFrameRate < 0 || m_configuredFrameRate != targetFrameRate)
		{
			ResetAdaptiveFrame();
		}
		int threshold = settings.AdaptivePhysicsFrame;
		int minFrameRate = settings.AdaptivePhysicsFrameMin;
		int lowerBound = Mathf.Clamp(minFrameRate, 1, targetFrameRate);
		if (threshold <= 0)
		{
			if (s_currentFrameRate != targetFrameRate)
			{
				s_currentFrameRate = targetFrameRate;
				Time.fixedDeltaTime = 1f / s_currentFrameRate;
			}
			return;
		}
		if (m_frameRateCooldown > 0f)
		{
			m_frameRateCooldown -= Time.unscaledDeltaTime;
			return;
		}
		// Decide once per completed measurement. An empty/stale sample must never reduce the step rate.
		if (!m_counter.IsRunning || m_counter.FPS <= 0f || m_counter.SampleVersion == m_lastSampleVersion) return;
		m_lastSampleVersion = m_counter.SampleVersion;
		m_prevSmoothFps = m_smoothFps;
		m_smoothFps = m_smoothFps <= 0f ? m_counter.FPS : m_smoothFps * 0.85f + m_counter.FPS * 0.15f;
		// Recovery must be reachable under the render cap, including Android's refresh-rate divisors.
		float renderCeiling = settings.TargetFrameRate;
		if (Application.platform == RuntimePlatform.Android)
		{
			float refreshRate = (float)Screen.currentResolution.refreshRateRatio.value;
			if (refreshRate > 0f) renderCeiling = refreshRate / Mathf.Ceil(refreshRate / renderCeiling);
		}
		float lowerThreshold = Mathf.Min(threshold, renderCeiling * 0.85f);
		float upperThreshold = Mathf.Min(lowerThreshold + Mathf.Max(2f, lowerThreshold * 0.1f), renderCeiling * 0.95f);
		if (m_prevSmoothFps > 0f && m_smoothFps < m_prevSmoothFps - 20f && s_currentFrameRate > lowerBound)
		{
			int nextFrameRate = Mathf.Max(lowerBound, s_currentFrameRate / 2);
			if (nextFrameRate != s_currentFrameRate)
			{
				RecordDirection(nextFrameRate < s_currentFrameRate ? -1 : 1);
				s_currentFrameRate = nextFrameRate;
				Time.fixedDeltaTime = 1f / s_currentFrameRate;
			}
			m_lowFpsStreak = 0;
			m_recoverStreak = 0;
			m_frameRateCooldown = 0.3f;
			return;
		}
		if (m_smoothFps < lowerThreshold)
		{
			m_lowFpsStreak++;
			m_recoverStreak = 0;
			if (m_lowFpsStreak >= 6 && s_currentFrameRate > lowerBound)
			{
				int step = Mathf.Max(1, Mathf.RoundToInt(s_currentFrameRate * 0.15f));
				int nextFrameRate = s_currentFrameRate - step;
				if (nextFrameRate < lowerBound)
				{
					nextFrameRate = lowerBound;
				}
				if (nextFrameRate != s_currentFrameRate)
				{
					if (m_isOscillating)
					{
						int dampened = Mathf.RoundToInt(Mathf.Lerp(s_currentFrameRate, nextFrameRate, 0.4f));
						if (dampened < lowerBound)
						{
							dampened = lowerBound;
						}
						nextFrameRate = dampened;
					}
					RecordDirection(-1);
					s_currentFrameRate = nextFrameRate;
					Time.fixedDeltaTime = 1f / s_currentFrameRate;
				}
				m_lowFpsStreak = 0;
				m_frameRateCooldown = m_isOscillating ? 0.5f : 0.35f;
			}
		}
		else if (m_smoothFps > upperThreshold && s_currentFrameRate < targetFrameRate)
		{
			m_recoverStreak++;
			m_lowFpsStreak = 0;
			if (m_recoverStreak >= 8)
			{
				if (targetFrameRate - s_currentFrameRate <= 3)
				{
					RecordDirection(1);
					s_currentFrameRate = targetFrameRate;
					Time.fixedDeltaTime = 1f / s_currentFrameRate;
				}
				else
				{
					int step = Mathf.Max(1, Mathf.RoundToInt(s_currentFrameRate * 0.15f));
					int nextFrameRate = s_currentFrameRate + step;
					if (nextFrameRate > targetFrameRate)
					{
						nextFrameRate = targetFrameRate;
					}
					if (nextFrameRate != s_currentFrameRate)
					{
						if (m_isOscillating)
						{
							int dampened = Mathf.RoundToInt(Mathf.Lerp(s_currentFrameRate, nextFrameRate, 0.3f));
							if (dampened > targetFrameRate)
							{
								dampened = targetFrameRate;
							}
							nextFrameRate = dampened;
						}
						RecordDirection(1);
						s_currentFrameRate = nextFrameRate;
						Time.fixedDeltaTime = 1f / s_currentFrameRate;
					}
				}
				m_recoverStreak = 0;
				m_frameRateCooldown = m_isOscillating ? 0.5f : 0.35f;
			}
		}
		else
		{
			m_lowFpsStreak = 0;
			m_recoverStreak = 0;
		}
	}

	private void RecordDirection(int dir)
	{
		if (m_lastDirection != 0 && dir != m_lastDirection)
		{
			m_directionHistory[m_directionIndex] = 1;
		}
		else
		{
			m_directionHistory[m_directionIndex] = 0;
		}
		m_lastDirection = dir;
		m_directionIndex = (m_directionIndex + 1) % m_directionHistory.Length;
		int reversals = 0;
		for (int i = 0; i < m_directionHistory.Length; i++)
		{
			reversals += m_directionHistory[i];
		}
		m_isOscillating = reversals >= 4;
	}
}
