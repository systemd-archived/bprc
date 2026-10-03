using System;
using UnityEngine;

[Serializable]
public class StarSeaSettings : SettingsBase
{
	private float m_gunProjectileDrag;

	private float m_partDrag;

	private float m_partAngularDrag;

	private bool m_showFPS;

	private int m_targetFrameRate;

	private int m_fixedFrameRate;

	private int m_circuitFrameRate;

	private int m_frameFrameRate;

	private float m_antiTunnelSpeedThreshold;

	private float m_antiTunnelPullBackDistance;

	private bool m_useMultithreadFrameSolver;

	private bool m_enableMultithreadCircuit;

	// 电路求解优化(零分配求解器 + CSR + 缓冲复用)，默认关闭(实验性)；开启则使用优化求解器
	private bool m_optimizedCircuit = false;

	private int m_physicsSolverIterations;

	/// <summary>
	/// Worker threads the native physics backends may use. <c>0</c> means "decide from the machine".
	/// </summary>
	/// <remarks>
	/// Box3D and Jolt both hard-coded <c>Clamp(processorCount - 1, …, 8)</c> before this setting existed,
	/// which leaves half the cores idle on a 16-core machine. Bepu is deliberately single-threaded (its
	/// narrow-phase filter reads a main-thread collection), so this value does not reach it.
	/// </remarks>
	private int m_physicsWorkerCount;

	private bool m_showPhysicsStats;

	private bool m_gpuPhysicsCompute;

	public bool GpuPhysicsCompute
	{
		get { return m_gpuPhysicsCompute; }
		set
		{
			if (m_gpuPhysicsCompute == value) return;
			m_gpuPhysicsCompute = value;
			OnPropertyChanged("GpuPhysicsCompute");
		}
	}

	private bool m_useBox3D;

	/// <summary>
	/// Retired opt-in gate for the Jolt backend. Kept only so existing settings files still load.
	/// </summary>
	/// <remarks>
	/// This used to decide whether Jolt appeared as a selectable engine, while collision response was
	/// broken. That defect is fixed — a dynamic body now rests on static geometry, verified against the
	/// native plugin by <c>Tools/Jolt/test/run-shape-ownership.ps1</c> — so the value no longer gates
	/// anything and the toggle is hidden from the settings UI. Availability is probed from the native
	/// plugin itself instead. The field is deliberately not removed: dropping it would make every
	/// existing settings file fail to round-trip. See <c>docs/PhysicsBackends.md</c>.
	/// </remarks>
	[System.Obsolete("The Jolt gate is retired; availability is probed from the native plugin. This property is kept only for settings-file compatibility.")]
	public bool EnableExperimentalJolt
	{
		get { return m_enableExperimentalJolt; }
		set
		{
			if (m_enableExperimentalJolt == value) return;
			m_enableExperimentalJolt = value;
			OnPropertyChanged("EnableExperimentalJolt");
		}
	}

	private bool m_enableExperimentalJolt;

	/// <summary>
	/// Stored as <see cref="int"/> rather than the enum type so that the on-disk representation stays a
	/// plain integer: settings files written before this field existed stored a boolean under
	/// <c>m_useBox3D</c>, and 0/1 still load correctly here.
	/// </summary>
	private int m_physicsEngine;

	/// <summary>
	/// Which rigid-body engine drives the simulation. Replaces the old <c>UseBox3D</c> boolean; see
	/// <see cref="UseBox3D"/> for the compatibility shim.
	/// </summary>
	public PhysicsEngine PhysicsEngine
	{
		get { return (PhysicsEngine)m_physicsEngine; }
		set
		{
			int next = (int)value;
			if (m_physicsEngine == next) return;
			m_physicsEngine = next;
			OnPropertyChanged("PhysicsEngine");
		}
	}

	/// <summary>
	/// Legacy two-way toggle retained so existing settings files and any remaining callers keep
	/// working. Setting it selects Box3D or PhysX; reading it reports whether Box3D is selected.
	/// </summary>
	[Obsolete("Use PhysicsEngine instead.")]
	public bool UseBox3D
	{
		get { return PhysicsEngine == PhysicsEngine.Box3D; }
		set
		{
			m_useBox3D = value;
			PhysicsEngine = value ? PhysicsEngine.Box3D : PhysicsEngine.PhysX;
		}
	}

	private int m_adaptivePhysicsFrame;

	private int m_adaptivePhysicsFrameMin;

	private bool m_customPhysicsFrame;

	private bool m_ddNewConnectionLogic = false ;

	private float m_gateWidthCoefficient = 1f;

	private string m_gateLengthCoefficients = "1,1,1,1,1";

	private float m_gateOpacity = 0.8f;

	private string m_gateDefaultLengths = "1,2,4,8";

	private string m_gateAdjustableRange = "0.1,8,0.1";

	private float m_gateExtensionSpeed = 0.2f;

	private bool m_stableMultiblock = true;

	private bool m_specialPointChargeInvincible = true;

	private float m_specialChargeForce = 70f;

	private float m_specialChargeJointBreakForce = -1f;

	private float m_specialChargeForceRadius = 4f;

	private bool m_chargeJointSnapEnabled = true;

	private float m_chargeJointSize = 1f;

	private float m_specialChargeReinforceRange = 5f;

	private int m_specialChargeReinforceMaxCount = 50;

	private float m_specialChargeReinforceBreakForce = 1000f;

	private bool m_specialChargeCollisionIgnore = true;

	private float m_specialChargeJointDistance = 0.5f;

	private bool m_specialChargeAttractionCollisionIgnore = true;

	private bool m_showJointConnectionCount;

	private bool m_specialChargeDefaultEnabled;

	private bool m_specialChargeForceConnect;

	private bool m_toolBookEnabled = true;

	private float m_toolBookPosX = -60f;

	private float m_toolBookPosY = -60f;

	private float m_toolBookSize = 1f;

	private int m_toolBookPasteOverwriteMode = 1;

	private int m_toolBookClipboardMax = 3;

	private bool m_gameRunCollapse;

	private float m_gameRunQuickSpeed = 0.2f;

	private bool m_newTimeScale = true;

	// 高级显示：图形化可视化(速度/角速度/角度/质心/万有引力范围等)，默认开启
	private bool m_advancedDisplayGraphical = true;

	// 快捷切换部件：快捷栏点击当前部件弹出同类型皮肤变体，默认开启
	private bool m_quickPartSwitch = true;

	private bool m_quickInheritParams = true;

	private bool m_enableParamPanel = true;

	private bool m_showModifiedParams = true;

	// 快捷切换部件：面板一次最多显示的皮肤个数（默认 16，可滑动查看其余）
	private int m_quickPartSwitchMax = 16;

	// 快捷切换部件：常驻面板（关闭=原逻辑；开启=双击显示/再次双击关闭，切换皮肤不关闭）默认开启
	private bool m_quickPartSwitchPersistent = true;

	// 摄像机跟随模式：0=默认(原版，双击切自由相机) 1=始终跟随 2=始终不跟随(强制自由相机)
	private int m_cameraFollowMode;

	// 自动进入“新创B”：跳过开场动画与版本选择（默认关闭）
	private bool m_autoEnterVersionB;
	public const string AutoEnterVersionBKey = "INAutoEnterVersionB";

	private bool m_blasterTNTDestroy;


	private float m_anchorUmbrellaCoefficient = 10f;

	private bool m_lightAntiTunnel = true;

	// 万有引力：不同载具(不同连接组件)之间施加 F = G * m1 * m2 / r^(2*衰减倍率) 的相互引力，默认关闭
	private bool m_universalGravity;

	// 引力常数 G，默认 0.1 方便观察
	private float m_universalGravityConstant = 0.1f;

	// 衰减倍率(距离指数倍率)：F = G*m1*m2 / r^(2*衰减倍率)，默认 0.333(指数0.667，比 r² 衰减更慢)
	private float m_universalGravityAttenuation = 0.333f;

	// 载具部件碰撞弹性(bounciness)：默认 0 = 原版 Contraption_PhysMat 的值
	private float m_collisionBounciness;

	// 斜向(Sender 索引40, 45°/135°/225°/315°)无线电发送器的无线连接范围，默认 32
	private float m_diagonalSenderRange = 32f;

	public float GunProjectileDrag
	{
		get
		{
			try
			{
				return INSettings.GetFloat(INFeature.GunProjectileDrag);
			}
			catch
			{
				return m_gunProjectileDrag;
			}
		}
		set
		{
			if (float.IsFinite(value) && value >= 0f)
			{
				m_gunProjectileDrag = value;
				INSettings.SetValue(INFeature.GunProjectileDrag, new Variant<float>(value));
				OnPropertyChanged("GunProjectileDrag");
			}
		}
	}

	public float PartDrag
	{
		get
		{
			return m_partDrag;
		}
		set
		{
			if (m_partDrag != value && float.IsFinite(value) && value >= 0f)
			{
				m_partDrag = value;
				OnPropertyChanged("PartDrag");
			}
		}
	}

	public float PartAngularDrag
	{
		get
		{
			return m_partAngularDrag;
		}
		set
		{
			if (m_partAngularDrag != value && float.IsFinite(value) && value >= 0f)
			{
				m_partAngularDrag = value;
				OnPropertyChanged("PartAngularDrag");
			}
		}
	}

	public bool ShowFPS
	{
		get
		{
			return m_showFPS;
		}
		set
		{
			if (m_showFPS != value)
			{
				m_showFPS = value;
				OnPropertyChanged("ShowFPS");
			}
		}
	}

	public int TargetFrameRate
	{
		get
		{
			return m_targetFrameRate;
		}
		set
		{
			if (m_targetFrameRate != value && value >= 1 && value <= 240)
			{
				m_targetFrameRate = value;
				OnPropertyChanged("TargetFrameRate");
			}
		}
	}

	public int FixedFrameRate
	{
		get
		{
			return m_fixedFrameRate;
		}
		set
		{
			if (m_fixedFrameRate != value && value >= 1)
			{
				m_fixedFrameRate = value;
				OnPropertyChanged("FixedFrameRate");
			}
		}
	}

	public int CircuitFrameRate
	{
		get
		{
			return m_circuitFrameRate;
		}
		set
		{
			if (m_circuitFrameRate != value && value >= 1)
			{
				m_circuitFrameRate = value;
				OnPropertyChanged("CircuitFrameRate");
			}
		}
	}

	public int FrameFrameRate
	{
		get
		{
			return m_frameFrameRate;
		}
		set
		{
			if (m_frameFrameRate != value && value >= 1)
			{
				m_frameFrameRate = value;
				OnPropertyChanged("FrameFrameRate");
			}
		}
	}

	public float AntiTunnelSpeedThreshold
	{
		get
		{
			return m_antiTunnelSpeedThreshold;
		}
		set
		{
			if (m_antiTunnelSpeedThreshold != value && float.IsFinite(value) && value >= 0f)
			{
				m_antiTunnelSpeedThreshold = value;
				OnPropertyChanged("AntiTunnelSpeedThreshold");
			}
		}
	}

	public float AntiTunnelPullBackDistance
	{
		get
		{
			return m_antiTunnelPullBackDistance;
		}
		set
		{
			if (m_antiTunnelPullBackDistance != value && float.IsFinite(value) && value >= 0f)
			{
				m_antiTunnelPullBackDistance = value;
				OnPropertyChanged("AntiTunnelPullBackDistance");
			}
		}
	}

	public bool UseMultithreadFrameSolver
	{
		get
		{
			return m_useMultithreadFrameSolver;
		}
		set
		{
			if (m_useMultithreadFrameSolver != value)
			{
				m_useMultithreadFrameSolver = value;
				OnPropertyChanged("UseMultithreadFrameSolver");
			}
		}
	}

	public bool EnableMultithreadCircuit
	{
		get
		{
			return m_enableMultithreadCircuit;
		}
		set
		{
			if (m_enableMultithreadCircuit != value)
			{
				m_enableMultithreadCircuit = value;
				OnPropertyChanged("EnableMultithreadCircuit");
			}
		}
	}

	public bool OptimizedCircuit
	{
		get
		{
			return m_optimizedCircuit;
		}
		set
		{
			if (m_optimizedCircuit != value)
			{
				m_optimizedCircuit = value;
				OnPropertyChanged("OptimizedCircuit");
			}
		}
	}

	public int PhysicsSolverIterations
	{
		get
		{
			return m_physicsSolverIterations;
		}
		set
		{
			if (m_physicsSolverIterations != value && value >= 1 && value <= 10)
			{
				m_physicsSolverIterations = value;
				OnPropertyChanged("PhysicsSolverIterations");
			}
		}
	}

	/// <summary>
	/// Draws the physics cost readout: the engine in use, the resolved worker count, the smoothed step
	/// time and the body/contact counts.
	/// </summary>
	/// <remarks>
	/// This is the instrumentation the performance analysis asked for and the project had none of: before
	/// it, every claim about which engine or which setting is faster was static inference, because nothing
	/// measured a step. It is off by default and costs a few microseconds per frame when on.
	/// </remarks>
	public bool ShowPhysicsStats
	{
		get
		{
			return m_showPhysicsStats;
		}
		set
		{
			if (m_showPhysicsStats != value)
			{
				m_showPhysicsStats = value;
				OnPropertyChanged("ShowPhysicsStats");
			}
		}
	}

	/// <summary>Worker threads for the native backends; <c>0</c> picks from the machine.</summary>
	public int PhysicsWorkerCount
	{
		get
		{
			return m_physicsWorkerCount;
		}
		set
		{
			if (m_physicsWorkerCount != value && value >= 0 && value <= 64)
			{
				m_physicsWorkerCount = value;
				OnPropertyChanged("PhysicsWorkerCount");
			}
		}
	}

	/// <summary>
	/// Resolves <see cref="PhysicsWorkerCount"/> to a concrete thread count.
	/// </summary>
	/// <remarks>
	/// <c>0</c> keeps the historical automatic behaviour but raises its ceiling: Box3D and Jolt used to
	/// cap themselves at eight workers, which wastes half the machine on a 16-core CPU.
	/// </para>
	/// <para>
	/// <b>Deliberately <c>internal</c>, not <c>public</c>.</b> A page built by reflecting a settings class -
	/// which is what <c>INSettingsInterface.GenerateGroup</c> does for the full settings window and for
	/// every page that lists a whole <c>SettingsBase</c> - turns every public property into a row, and
	/// <c>SettingsItem.Bind</c> hands <c>PropertyInfo.GetSetMethod()</c> straight to
	/// <c>Delegate.CreateDelegate</c>. For a read-only property that method is <c>null</c> and the page
	/// throws <c>ArgumentNullException</c> while building, taking the settings panel with it. Anything
	/// computed rather than stored must therefore stay off the public surface of a settings class.
	/// </para>
	/// </remarks>
	internal int ResolvedPhysicsWorkerCount
	{
		get
		{
			int automatic = Mathf.Max(1, SystemInfo.processorCount - 1);
			return m_physicsWorkerCount > 0 ? Mathf.Clamp(m_physicsWorkerCount, 1, 64) : automatic;
		}
	}

	public int AdaptivePhysicsFrame
	{
		get
		{
			return m_adaptivePhysicsFrame;
		}
		set
		{
			if (m_adaptivePhysicsFrame != value && value >= 0)
			{
				m_adaptivePhysicsFrame = value;
				OnPropertyChanged("AdaptivePhysicsFrame");
			}
		}
	}

	public int AdaptivePhysicsFrameMin
	{
		get
		{
			return m_adaptivePhysicsFrameMin;
		}
		set
		{
			if (m_adaptivePhysicsFrameMin != value && value >= 0)
			{
				m_adaptivePhysicsFrameMin = value;
				OnPropertyChanged("AdaptivePhysicsFrameMin");
			}
		}
	}

	public bool CustomPhysicsFrame
	{
		get
		{
			return m_customPhysicsFrame;
		}
		set
		{
			if (m_customPhysicsFrame != value)
			{
				m_customPhysicsFrame = value;
				OnPropertyChanged("CustomPhysicsFrame");
			}
		}
	}

	public bool DDNewConnectionLogic
	{
		get
		{
			return m_ddNewConnectionLogic;
		}
		set
		{
			if (m_ddNewConnectionLogic != value)
			{
				m_ddNewConnectionLogic = value;
				OnPropertyChanged("DDNewConnectionLogic");
			}
		}
	}

	public float GateWidthCoefficient
	{
		get
		{
			return m_gateWidthCoefficient;
		}
		set
		{
			if (m_gateWidthCoefficient != value && float.IsFinite(value) && value > 0f)
			{
				m_gateWidthCoefficient = value;
				OnPropertyChanged("GateWidthCoefficient");
			}
		}
	}

	public string GateLengthCoefficients
	{
		get
		{
			return m_gateLengthCoefficients;
		}
		set
		{
			if (m_gateLengthCoefficients != value && !string.IsNullOrEmpty(value))
			{
				m_gateLengthCoefficients = value;
				OnPropertyChanged("GateLengthCoefficients");
			}
		}
	}

	public float GateOpacity
	{
		get
		{
			return m_gateOpacity;
		}
		set
		{
			if (m_gateOpacity != value && float.IsFinite(value) && value >= 0f && value <= 1f)
			{
				m_gateOpacity = value;
				OnPropertyChanged("GateOpacity");
			}
		}
	}

	public string GateDefaultLengths
	{
		get
		{
			return m_gateDefaultLengths;
		}
		set
		{
			if (m_gateDefaultLengths != value && !string.IsNullOrEmpty(value))
			{
				m_gateDefaultLengths = value;
				OnPropertyChanged("GateDefaultLengths");
			}
		}
	}

	public string GateAdjustableRange
	{
		get
		{
			return m_gateAdjustableRange;
		}
		set
		{
			if (m_gateAdjustableRange != value && !string.IsNullOrEmpty(value))
			{
				m_gateAdjustableRange = value;
				OnPropertyChanged("GateAdjustableRange");
			}
		}
	}

	public float GateExtensionSpeed
	{
		get
		{
			return m_gateExtensionSpeed;
		}
		set
		{
			if (m_gateExtensionSpeed != value && float.IsFinite(value) && value > 0f)
			{
				m_gateExtensionSpeed = value;
				OnPropertyChanged("GateExtensionSpeed");
			}
		}
	}

	public bool StableMultiblock
	{
		get
		{
			return m_stableMultiblock;
		}
		set
		{
			if (m_stableMultiblock != value)
			{
				m_stableMultiblock = value;
				OnPropertyChanged("StableMultiblock");
			}
		}
	}

	public bool SpecialPointChargeInvincible
	{
		get
		{
			return m_specialPointChargeInvincible;
		}
		set
		{
			if (m_specialPointChargeInvincible != value)
			{
				m_specialPointChargeInvincible = value;
				OnPropertyChanged("SpecialPointChargeInvincible");
			}
		}
	}

	public float SpecialChargeForce
	{
		get
		{
			return m_specialChargeForce;
		}
		set
		{
			if (m_specialChargeForce != value)
			{
				m_specialChargeForce = value;
				OnPropertyChanged("SpecialChargeForce");
			}
		}
	}

	public float SpecialChargeJointBreakForce
	{
		get
		{
			return m_specialChargeJointBreakForce;
		}
		set
		{
			if (m_specialChargeJointBreakForce != value)
			{
				m_specialChargeJointBreakForce = value;
				OnPropertyChanged("SpecialChargeJointBreakForce");
			}
		}
	}

	public float SpecialChargeForceRadius
	{
		get
		{
			return m_specialChargeForceRadius;
		}
		set
		{
			if (m_specialChargeForceRadius != value)
			{
				m_specialChargeForceRadius = value;
				OnPropertyChanged("SpecialChargeForceRadius");
			}
		}
	}

	public bool ChargeJointSnapEnabled
	{
		get
		{
			return m_chargeJointSnapEnabled;
		}
		set
		{
			if (m_chargeJointSnapEnabled != value)
			{
				m_chargeJointSnapEnabled = value;
				OnPropertyChanged("ChargeJointSnapEnabled");
			}
		}
	}

	public float ChargeJointSize
	{
		get
		{
			return m_chargeJointSize;
		}
		set
		{
			if (m_chargeJointSize != value)
			{
				m_chargeJointSize = value;
				OnPropertyChanged("ChargeJointSize");
			}
		}
	}

	public float SpecialChargeReinforceRange
	{
		get
		{
			return m_specialChargeReinforceRange;
		}
		set
		{
			if (m_specialChargeReinforceRange != value)
			{
				m_specialChargeReinforceRange = value;
				OnPropertyChanged("SpecialChargeReinforceRange");
			}
		}
	}

	public int SpecialChargeReinforceMaxCount
	{
		get
		{
			return m_specialChargeReinforceMaxCount;
		}
		set
		{
			if (m_specialChargeReinforceMaxCount != value)
			{
				m_specialChargeReinforceMaxCount = value;
				OnPropertyChanged("SpecialChargeReinforceMaxCount");
			}
		}
	}

	public float SpecialChargeReinforceBreakForce
	{
		get
		{
			return m_specialChargeReinforceBreakForce;
		}
		set
		{
			if (m_specialChargeReinforceBreakForce != value)
			{
				m_specialChargeReinforceBreakForce = value;
				OnPropertyChanged("SpecialChargeReinforceBreakForce");
			}
		}
	}

	public bool SpecialChargeCollisionIgnore
	{
		get
		{
			return m_specialChargeCollisionIgnore;
		}
		set
		{
			if (m_specialChargeCollisionIgnore != value)
			{
				m_specialChargeCollisionIgnore = value;
				OnPropertyChanged("SpecialChargeCollisionIgnore");
			}
		}
	}

	public float SpecialChargeJointDistance
	{
		get
		{
			return m_specialChargeJointDistance;
		}
		set
		{
			if (m_specialChargeJointDistance != value && float.IsFinite(value) && value >= 0f)
			{
				m_specialChargeJointDistance = value;
				OnPropertyChanged("SpecialChargeJointDistance");
			}
		}
	}

	public bool SpecialChargeAttractionCollisionIgnore
	{
		get
		{
			return m_specialChargeAttractionCollisionIgnore;
		}
		set
		{
			if (m_specialChargeAttractionCollisionIgnore != value)
			{
				m_specialChargeAttractionCollisionIgnore = value;
				OnPropertyChanged("SpecialChargeAttractionCollisionIgnore");
			}
		}
	}

	public bool ShowJointConnectionCount
	{
		get
		{
			return m_showJointConnectionCount;
		}
		set
		{
			if (m_showJointConnectionCount != value)
			{
				m_showJointConnectionCount = value;
				OnPropertyChanged("ShowJointConnectionCount");
			}
		}
	}

	public bool SpecialChargeDefaultEnabled
	{
		get
		{
			return m_specialChargeDefaultEnabled;
		}
		set
		{
			if (m_specialChargeDefaultEnabled != value)
			{
				m_specialChargeDefaultEnabled = value;
				OnPropertyChanged("SpecialChargeDefaultEnabled");
			}
		}
	}

	public bool SpecialChargeForceConnect
	{
		get
		{
			return m_specialChargeForceConnect;
		}
		set
		{
			if (m_specialChargeForceConnect != value)
			{
				m_specialChargeForceConnect = value;
				OnPropertyChanged("SpecialChargeForceConnect");
			}
		}
	}

	public bool ToolBookEnabled
	{
		get
		{
			return m_toolBookEnabled;
		}
		set
		{
			if (m_toolBookEnabled != value)
			{
				m_toolBookEnabled = value;
				OnPropertyChanged("ToolBookEnabled");
			}
		}
	}

	public bool EnableParamPanel
	{
		get { return m_enableParamPanel; }
		set
		{
			if (m_enableParamPanel != value)
			{
				m_enableParamPanel = value;
				OnPropertyChanged("EnableParamPanel");
			}
		}
	}
	public bool QuickInheritParams
	{
		get { return m_quickInheritParams; }
		set
		{
			if (m_quickInheritParams != value)
			{
				m_quickInheritParams = value;
				OnPropertyChanged("QuickInheritParams");
			}
		}
	}

	public bool ShowModifiedParams
	{
		get { return m_showModifiedParams; }
		set
		{
			if (m_showModifiedParams != value)
			{
				m_showModifiedParams = value;
				OnPropertyChanged("ShowModifiedParams");
			}
		}
	}
	public float ToolBookPosX
	{
		get { return m_toolBookPosX; }
		set
		{
			if (m_toolBookPosX != value)
			{
				m_toolBookPosX = value;
				OnPropertyChanged("ToolBookPosX");
			}
		}
	}

	public float ToolBookPosY
	{
		get { return m_toolBookPosY; }
		set
		{
			if (m_toolBookPosY != value)
			{
				m_toolBookPosY = value;
				OnPropertyChanged("ToolBookPosY");
			}
		}
	}

	public float ToolBookSize
	{
		get { return m_toolBookSize; }
		set
		{
			if (m_toolBookSize != value)
			{
				m_toolBookSize = value;
				OnPropertyChanged("ToolBookSize");
			}
		}
	}

	public int ToolBookPasteOverwriteMode
	{
		get { return m_toolBookPasteOverwriteMode; }
		set
		{
			if (m_toolBookPasteOverwriteMode != value)
			{
				m_toolBookPasteOverwriteMode = value;
				OnPropertyChanged("ToolBookPasteOverwriteMode");
			}
		}
	}

	public int ToolBookClipboardMax
	{
		get { return m_toolBookClipboardMax; }
		set
		{
			if (m_toolBookClipboardMax != value)
			{
				m_toolBookClipboardMax = value;
				OnPropertyChanged("ToolBookClipboardMax");
			}
		}
	}

	public bool GameRunCollapse
	{
		get { return m_gameRunCollapse; }
		set
		{
			if (m_gameRunCollapse != value)
			{
				m_gameRunCollapse = value;
				OnPropertyChanged("GameRunCollapse");
			}
		}
	}

	public float GameRunQuickSpeed
	{
		get { return m_gameRunQuickSpeed; }
		set
		{
			if (m_gameRunQuickSpeed != value && float.IsFinite(value) && value >= 0f && value <= 10f)
			{
				m_gameRunQuickSpeed = value;
				OnPropertyChanged("GameRunQuickSpeed");
			}
		}
	}

	public bool BlasterTNTDestroy
	{
		get { return m_blasterTNTDestroy; }
		set
		{
			if (m_blasterTNTDestroy != value)
			{
				m_blasterTNTDestroy = value;
				OnPropertyChanged("BlasterTNTDestroy");
			}
		}
	}

	public bool NewTimeScale
	{
		get { return m_newTimeScale; }
		set
		{
			if (m_newTimeScale != value)
			{
				m_newTimeScale = value;
				OnPropertyChanged("NewTimeScale");
			}
		}
	}

	public int CameraFollowMode
	{
		get { return m_cameraFollowMode; }
		set
		{
			if (value < 0) value = 0;
			else if (value > 2) value = 2;
			if (m_cameraFollowMode != value)
			{
				m_cameraFollowMode = value;
				OnPropertyChanged("CameraFollowMode");
			}
		}
	}

	// 自动进入“新创B”(跳过开场动画与版本选择)
	// 版本选择发生在 INUserSettings 载入之前，故镜像到 PlayerPrefs 供启动早期读取
	public bool AutoEnterVersionB
	{
		get { return m_autoEnterVersionB; }
		set
		{
			if (m_autoEnterVersionB != value)
			{
				m_autoEnterVersionB = value;
				PlayerPrefs.SetInt(AutoEnterVersionBKey, value ? 1 : 0);
				PlayerPrefs.Save();
				OnPropertyChanged("AutoEnterVersionB");
			}
		}
	}

	// 启动早期(版本未选择、INUserSettings 未载入)读取该开关
	public static bool GetAutoEnterVersionBFromPrefs()
	{
		return PlayerPrefs.GetInt(AutoEnterVersionBKey, 0) == 1;
	}

	public float AnchorUmbrellaCoefficient
	{
		get
		{
			return m_anchorUmbrellaCoefficient;
		}
		set
		{
			if (m_anchorUmbrellaCoefficient != value && float.IsFinite(value) && value >= 0f)
			{
				m_anchorUmbrellaCoefficient = value;
				OnPropertyChanged("AnchorUmbrellaCoefficient");
			}
		}
	}

	public bool LightAntiTunnel
	{
		get
		{
			return m_lightAntiTunnel;
		}
		set
		{
			if (m_lightAntiTunnel != value)
			{
				m_lightAntiTunnel = value;
				OnPropertyChanged("LightAntiTunnel");
			}
		}
	}

	public bool UniversalGravity
	{
		get
		{
			return m_universalGravity;
		}
		set
		{
			if (m_universalGravity != value)
			{
				m_universalGravity = value;
				OnPropertyChanged("UniversalGravity");
			}
		}
	}

	public float UniversalGravityConstant
	{
		get
		{
			return m_universalGravityConstant;
		}
		set
		{
			if (m_universalGravityConstant != value && float.IsFinite(value) && value >= 0f)
			{
				m_universalGravityConstant = value;
				OnPropertyChanged("UniversalGravityConstant");
			}
		}
	}

	public float UniversalGravityAttenuation
	{
		get
		{
			return m_universalGravityAttenuation;
		}
		set
		{
			if (m_universalGravityAttenuation != value && float.IsFinite(value) && value >= 0f)
			{
				m_universalGravityAttenuation = value;
				OnPropertyChanged("UniversalGravityAttenuation");
			}
		}
	}

	// 高级显示：图形化(箭头/标记/引力范围圈)；关闭则只显示文字读数
	public bool AdvancedDisplayGraphical
	{
		get
		{
			return m_advancedDisplayGraphical;
		}
		set
		{
			if (m_advancedDisplayGraphical != value)
			{
				m_advancedDisplayGraphical = value;
				OnPropertyChanged("AdvancedDisplayGraphical");
			}
		}
	}

	// 快捷切换部件：在快捷栏点击当前部件即弹出同类型皮肤变体，点击即可切换（默认开启）
	public bool QuickPartSwitch
	{
		get { return m_quickPartSwitch; }
		set
		{
			if (m_quickPartSwitch != value)
			{
				m_quickPartSwitch = value;
				OnPropertyChanged("QuickPartSwitch");
			}
		}
	}

	// 快捷切换部件：面板一次最多显示的皮肤个数（默认 16）
	public int QuickPartSwitchMax
	{
		get { return m_quickPartSwitchMax; }
		set
		{
			int v = (value < 1) ? 1 : ((value > 60) ? 60 : value);
			if (m_quickPartSwitchMax != v)
			{
				m_quickPartSwitchMax = v;
				OnPropertyChanged("QuickPartSwitchMax");
			}
		}
	}

	// 快捷切换部件：常驻面板（默认开启）
	public bool QuickPartSwitchPersistent
	{
		get { return m_quickPartSwitchPersistent; }
		set
		{
			if (m_quickPartSwitchPersistent != value)
			{
				m_quickPartSwitchPersistent = value;
				OnPropertyChanged("QuickPartSwitchPersistent");
			}
		}
	}

	// 载具部件碰撞弹性(bounciness)：0=原版；越大碰撞越弹
	public float CollisionBounciness
	{
		get
		{
			return m_collisionBounciness;
		}
		set
		{
			if (m_collisionBounciness != value && float.IsFinite(value) && value >= 0f)
			{
				m_collisionBounciness = value;
				OnPropertyChanged("CollisionBounciness");
			}
		}
	}

	// 斜向无线电发送器的无线连接范围(世界单位)，默认 32
	public float DiagonalSenderRange
	{
		get
		{
			return m_diagonalSenderRange;
		}
		set
		{
			if (m_diagonalSenderRange != value && float.IsFinite(value) && value > 0f)
			{
				m_diagonalSenderRange = value;
				OnPropertyChanged("DiagonalSenderRange");
			}
		}
	}

	public StarSeaSettings()
	{
		GunProjectileDrag = INSettings.GetFloat(INFeature.GunProjectileDrag);
		PartDrag = 0.2f;
		PartAngularDrag = 0.05f;
		ShowFPS = true;
		TargetFrameRate = 60;
		FixedFrameRate = 50;
		CircuitFrameRate = 50;
		FrameFrameRate = 50;
		AntiTunnelSpeedThreshold = 50f;
		AntiTunnelPullBackDistance = 0f;
		UseMultithreadFrameSolver = false;
		EnableMultithreadCircuit = false;
		OptimizedCircuit = false;
		PhysicsSolverIterations = 6;
		PhysicsWorkerCount = 0;
		ShowPhysicsStats = false;
		GpuPhysicsCompute = false;
		PhysicsEngine = PhysicsEngine.PhysX;
		AdaptivePhysicsFrame = 0;
		AdaptivePhysicsFrameMin = 20;
		CustomPhysicsFrame = true;
		GateWidthCoefficient = 1f;
		GateLengthCoefficients = "1,1,1,1,1";
		GateOpacity = 0.8f;
		GateDefaultLengths = "1,2,4,8";
		GateAdjustableRange = "0.1,8,0.1";
		GateExtensionSpeed = 0.2f;
		StableMultiblock = true;
		SpecialPointChargeInvincible = true;
		SpecialChargeForce = 70f;
		SpecialChargeJointBreakForce = -1f;
		SpecialChargeForceRadius = 4f;
		ChargeJointSnapEnabled = true;
		ChargeJointSize = 1f;
		SpecialChargeReinforceRange = 5f;
		SpecialChargeReinforceMaxCount = 50;
		SpecialChargeReinforceBreakForce = 1000f;
		SpecialChargeCollisionIgnore = true;
		SpecialChargeJointDistance = 0.5f;
		SpecialChargeAttractionCollisionIgnore = true;
		ShowJointConnectionCount = false;
		SpecialChargeDefaultEnabled = false;
		SpecialChargeForceConnect = false;
		AnchorUmbrellaCoefficient = 10f;
		LightAntiTunnel = true;
		UniversalGravity = false;
		UniversalGravityConstant = 0.1f;
		UniversalGravityAttenuation = 0.333f;
		ToolBookEnabled = true;
		QuickInheritParams = true;
		EnableParamPanel = true;
		ShowModifiedParams = true;
		ToolBookPosX = -360f;
		ToolBookPosY = -60f;
		ToolBookSize = 1f;
		ToolBookPasteOverwriteMode = 1;
		ToolBookClipboardMax = 3;
		GameRunCollapse = false;
		GameRunQuickSpeed = 0.2f;
		NewTimeScale = true;
		AdvancedDisplayGraphical = true;
		QuickPartSwitch = true;
		QuickPartSwitchMax = 16;
		QuickPartSwitchPersistent = true;
		CollisionBounciness = 0f;
		DiagonalSenderRange = 32f;
		CameraFollowMode = 0;
		AutoEnterVersionB = false;
		BlasterTNTDestroy = false;
	}

	public StarSeaSettings(StarSeaSettings settings)
		: this()
	{
		Update(settings);
	}

	public override void Apply()
	{
		INSettings.SetValue(INFeature.GunProjectileDrag, new Variant<float>(GunProjectileDrag));
		Application.targetFrameRate = TargetFrameRate;
		if (!CustomPhysicsFrame)
		{
			Time.fixedDeltaTime = 1f / FixedFrameRate;
		}
		ElectricalSystem.CircuitFrameRate = CircuitFrameRate;
		ElectricalSystem.EnableMultithreadCircuit = EnableMultithreadCircuit;
		ElectricalSystem.UseOptimizedCircuit = OptimizedCircuit;
		FrameJointManager.FrameFrameRate = FrameFrameRate;
		INContraption.AntiTunnelSpeedThreshold = AntiTunnelSpeedThreshold;
		INContraption.AntiTunnelPullBackDistance = AntiTunnelPullBackDistance;
		Physics.defaultSolverIterations = PhysicsSolverIterations;
		GpuPhysicsForces.Configure(GpuPhysicsCompute);
	}

	public void Update(StarSeaSettings settings)
	{
		if (settings != null)
		{
			GunProjectileDrag = settings.GunProjectileDrag;
			PartDrag = settings.PartDrag;
			PartAngularDrag = settings.PartAngularDrag;
			ShowFPS = settings.ShowFPS;
			TargetFrameRate = settings.TargetFrameRate;
			FixedFrameRate = settings.FixedFrameRate;
			CircuitFrameRate = settings.CircuitFrameRate;
			FrameFrameRate = settings.FrameFrameRate;
			AntiTunnelSpeedThreshold = settings.AntiTunnelSpeedThreshold;
			AntiTunnelPullBackDistance = settings.AntiTunnelPullBackDistance;
			UseMultithreadFrameSolver = settings.UseMultithreadFrameSolver;
			EnableMultithreadCircuit = settings.EnableMultithreadCircuit;
			OptimizedCircuit = settings.OptimizedCircuit;
			PhysicsSolverIterations = settings.PhysicsSolverIterations;
			PhysicsWorkerCount = settings.PhysicsWorkerCount;
			ShowPhysicsStats = settings.ShowPhysicsStats;
			GpuPhysicsCompute = settings.GpuPhysicsCompute;
			PhysicsEngine = settings.PhysicsEngine;
			// Copied through the backing field on purpose: the property is [Obsolete], and its setter
			// would raise a spurious change notification part-way through constructing the copy.
			m_enableExperimentalJolt = settings.m_enableExperimentalJolt;
			AdaptivePhysicsFrame = settings.AdaptivePhysicsFrame;
			AdaptivePhysicsFrameMin = settings.AdaptivePhysicsFrameMin;
			CustomPhysicsFrame = settings.CustomPhysicsFrame;
			GateWidthCoefficient = settings.GateWidthCoefficient;
			GateLengthCoefficients = settings.GateLengthCoefficients;
			GateOpacity = settings.GateOpacity;
			GateDefaultLengths = settings.GateDefaultLengths;
			GateAdjustableRange = settings.GateAdjustableRange;
			GateExtensionSpeed = settings.GateExtensionSpeed;
			StableMultiblock = settings.StableMultiblock;
			SpecialPointChargeInvincible = settings.SpecialPointChargeInvincible;
			SpecialChargeForce = settings.SpecialChargeForce;
			SpecialChargeJointBreakForce = settings.SpecialChargeJointBreakForce;
			SpecialChargeForceRadius = settings.SpecialChargeForceRadius;
			ChargeJointSnapEnabled = settings.ChargeJointSnapEnabled;
			ChargeJointSize = settings.ChargeJointSize;
			SpecialChargeReinforceRange = settings.SpecialChargeReinforceRange;
			SpecialChargeReinforceMaxCount = settings.SpecialChargeReinforceMaxCount;
			SpecialChargeReinforceBreakForce = settings.SpecialChargeReinforceBreakForce;
			SpecialChargeCollisionIgnore = settings.SpecialChargeCollisionIgnore;
			SpecialChargeJointDistance = settings.SpecialChargeJointDistance;
			SpecialChargeAttractionCollisionIgnore = settings.SpecialChargeAttractionCollisionIgnore;
			ShowJointConnectionCount = settings.ShowJointConnectionCount;
			SpecialChargeDefaultEnabled = settings.SpecialChargeDefaultEnabled;
			SpecialChargeForceConnect = settings.SpecialChargeForceConnect;
			AnchorUmbrellaCoefficient = settings.AnchorUmbrellaCoefficient;
			LightAntiTunnel = settings.LightAntiTunnel;
			UniversalGravity = settings.UniversalGravity;
			UniversalGravityConstant = settings.UniversalGravityConstant;
			UniversalGravityAttenuation = settings.UniversalGravityAttenuation;
			ToolBookEnabled = settings.ToolBookEnabled;
			QuickInheritParams = settings.QuickInheritParams;
			EnableParamPanel = settings.EnableParamPanel;
			ShowModifiedParams = settings.ShowModifiedParams;
			ToolBookPosX = settings.ToolBookPosX;
			ToolBookPosY = settings.ToolBookPosY;
			ToolBookSize = settings.ToolBookSize;
			ToolBookPasteOverwriteMode = settings.ToolBookPasteOverwriteMode;
			ToolBookClipboardMax = settings.ToolBookClipboardMax;
			GameRunCollapse = settings.GameRunCollapse;
			GameRunQuickSpeed = settings.GameRunQuickSpeed;
			NewTimeScale = settings.NewTimeScale;
			AdvancedDisplayGraphical = settings.AdvancedDisplayGraphical;
			QuickPartSwitch = settings.QuickPartSwitch;
			QuickPartSwitchMax = settings.QuickPartSwitchMax;
			QuickPartSwitchPersistent = settings.QuickPartSwitchPersistent;
			CollisionBounciness = settings.CollisionBounciness;
			DiagonalSenderRange = settings.DiagonalSenderRange;
			CameraFollowMode = settings.CameraFollowMode;
			AutoEnterVersionB = settings.AutoEnterVersionB;
			BlasterTNTDestroy = settings.BlasterTNTDestroy;
		}
	}
}
