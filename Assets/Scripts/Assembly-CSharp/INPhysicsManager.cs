using System;
using UnityEngine;

public class INPhysicsManager : MonoBehaviour
{
	private float m_timeStep;
	private float m_nextStatsSample;
	private float m_peakFrame;
	private long m_previousAllocated;
	private int m_previousCollections;
	private string m_statsText;

	private void Update()
	{
		var settings = INUserSettings.Instance?.StarSeaSettings;
		if (settings == null || !settings.ShowPhysicsStats)
		{
			m_statsText = null;
			m_peakFrame = 0f;
			m_previousAllocated = 0;
			return;
		}
		m_peakFrame = Mathf.Max(m_peakFrame, Time.unscaledDeltaTime * 1000f);
		if (Time.realtimeSinceStartup < m_nextStatsSample) return;
		m_nextStatsSample = Time.realtimeSinceStartup + 0.5f;
		GamePhysics.SamplePopulation();
		long allocated = GC.GetAllocatedBytesForCurrentThread();
		int collections = GC.CollectionCount(0);
		float kilobytes = m_previousAllocated == 0 ? 0f : (allocated - m_previousAllocated) / 1024f;
		int collected = m_previousAllocated == 0 ? 0 : collections - m_previousCollections;
		m_previousAllocated = allocated;
		m_previousCollections = collections;
		m_statsText = "物理 / GC 统计\n引擎: " + GamePhysics.CurrentEngineName +
			"   线程: " + settings.ResolvedPhysicsWorkerCount +
			"\n物理步: " + GamePhysics.StepMilliseconds.ToString("F3") + " ms" +
			"   峰值帧: " + m_peakFrame.ToString("F1") + " ms" +
			"\n刚体/碰撞体: " + GamePhysics.BodyCount + " / " + GamePhysics.ColliderCount +
			"\n主线程分配/0.5s: " + kilobytes.ToString("F1") + " KB   GC: " + collected +
			"\n托管堆: " + (GC.GetTotalMemory(false) / (1024f * 1024f)).ToString("F1") + " MB" +
			"\n" + GpuPhysicsForces.Describe();
		m_peakFrame = 0f;
	}

	public float TimeStep
	{
		get
		{
			return m_timeStep;
		}
		set
		{
			m_timeStep = value;
		}
	}

	public static INPhysicsManager Instance { get; private set; }

	public event Action BeforeSimulation;

	public event Action AfterSimulation;

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			enabled = false;
			Destroy(gameObject);
			return;
		}
		Instance = this;
		// This manager is a child inside the INGameManager prefab and DontDestroyOnLoad only accepts
		// a root object, so the call was refused: Unity logged "DontDestroyOnLoad only works for root
		// GameObjects or components on root GameObjects" and destroyed the manager on the next scene
		// load. That takes the simulation with it - automatic stepping is switched off below, so only
		// this component's FixedUpdate ever calls GamePhysics.Simulate - and JointManager subscribes
		// to the events here. Detaching first is what makes the intent stick; it is the same fix
		// INSpriteManager.Awake needed.
		transform.SetParent(null, worldPositionStays: true);
		UnityEngine.Object.DontDestroyOnLoad(this);
		// `Physics.autoSimulation` is obsolete on 2022.2+; PhysicsSimulation.Auto is the same switch
		// expressed with whichever API the editor has.
		PhysicsSimulation.Auto = false;
		m_timeStep = Time.fixedDeltaTime;
	}

	private void FixedUpdate()
	{
		GpuPhysicsForces.Configure(INUserSettings.Instance?.StarSeaSettings?.GpuPhysicsCompute ?? false);
		this.BeforeSimulation?.Invoke();
		if (!PhysicsSimulation.Auto)
		{
			m_timeStep = Time.fixedDeltaTime;
			GamePhysics.Simulate(m_timeStep);
		}
		this.AfterSimulation?.Invoke();
		// Switch only after the current backend has finished its step, so queued forces are applied
		// exactly once and never land in a half-torn-down world.
		var settings = INUserSettings.Instance?.StarSeaSettings;
		if (settings != null && settings.PhysicsEngine != GamePhysics.CurrentEngine)
		{
			PhysicsEngine requested = settings.PhysicsEngine;
			if (!GamePhysics.SelectEngine(requested))
			{
				// The engine could not start; fall back rather than retrying every frame.
				settings.PhysicsEngine = PhysicsEngine.PhysX;
				ShowEngineFallback(requested, GamePhysics.LastError);
			}
		}
	}

	private static void ShowEngineFallback(PhysicsEngine requested, string reason)
	{
		Debug.LogError("Physics engine '" + requested + "' could not start, reverting to PhysX. " + reason);
	}

	private void OnDestroy()
	{
		if (Instance != this) return;
		GamePhysics.Shutdown();
		Instance = null;
	}

	private void OnGUI()
	{
		DrawPhysicsStats();
		if (string.IsNullOrEmpty(GamePhysics.LastError) || Time.realtimeSinceStartup - GamePhysics.LastErrorTime > 12f) return;
		float width = Mathf.Min(560f, Screen.width - 24f);
		GUILayout.BeginArea(new Rect((Screen.width - width) * 0.5f, 16f, width, 120f), GUI.skin.box);
		GUILayout.Label(GamePhysics.CurrentEngineName + " stopped. PhysX is active.");
		GUILayout.Label(GamePhysics.LastError, new GUIStyle(GUI.skin.label) { wordWrap = true });
		GUILayout.EndArea();
	}

	/// <summary>
	/// The physics cost readout behind 优化设置 → 调试与度量 → 物理统计.
	/// </summary>
	/// <remarks>
	/// This exists because the project had no runtime measurement at all: every earlier claim about which
	/// engine or which setting is faster came from reading the source. The number that matters is
	/// <see cref="GamePhysics.StepMilliseconds"/>, which brackets the whole <c>Simulate</c> call - for the
	/// native backends that includes the managed mirror, which is the part the main thread actually pays
	/// and the reason a native engine is not automatically the faster choice.
	/// </remarks>
	private void DrawPhysicsStats()
	{
		var settings = INUserSettings.Instance?.StarSeaSettings;
		if (settings == null || !settings.ShowPhysicsStats) return;
		float width = Mathf.Min(440f, Screen.width - 24f);
		GUILayout.BeginArea(new Rect(8f, 8f, width, 150f), GUI.skin.box);
		GUILayout.Label(m_statsText ?? "正在采样物理 / GC…");
		GUILayout.EndArea();
	}
}
