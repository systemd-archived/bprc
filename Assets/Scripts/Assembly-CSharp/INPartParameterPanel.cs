using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UButton = UnityEngine.UI.Button;

// 建造界面长按部件后弹出的“部件参数”面板。
// 可调项来源（比只枚举 public 字段更全）：
//   1) 部件自身类型继承链上的数值字段（public 或 [SerializeField] 私有）
//   2) 部件自身类型继承链上的**可写数值属性**（如机炮的 CoolingTime 射速相关）
//   3) 部件上（本体 + 直接子物体）组件的数值成员（如 EntityLight 的 m_length/Length 光长度）
//   键格式：自身成员 = "成员名"；组件成员 = "组件类型名.成员名"。
// 编辑为暂存式：点“保存”才写入（真正生效），点“取消”放弃。
// 显示为 “原变量名[中文名]”，键仍是原变量名。
public class INPartParameterPanel : MonoBehaviour
{
	public static bool IsOpen { get; private set; }

	private static INPartParameterPanel s_current;

	private class Member
	{
		public string Key;
		public bool IsBool;
		// 该成员声明在“部件自身的具体类型”上（与这个部件最相关）
		public bool SelfType;
		// 默认隐藏（危险项 / 改了也没效果的项）
		public bool Hidden;
	}

	// 默认隐藏：改了就崩（载具自身属性）或改了没实际效果（图集/坐标/测量值等）
	private static readonly HashSet<string> s_hidden = new HashSet<string>
	{
		// 载具自身 / 危险
		"m_partType", "m_partTier", "m_gridRotation", "m_flipped", "m_eightWay", "m_static",
		"m_valid", "customPartIndex", "m_jointType", "m_autoAlign",
		"m_jointConnectionType", "m_jointConnectionDirection",
		"m_customJointConnectionDirection", "m_jointConnectionStrength",
		"m_coordX", "m_coordY", "m_gridXmin", "m_gridXmax", "m_gridYmin", "m_gridYmax",
		// 改了没效果（图集/贴图信息/层/测量值等）
		"m_UVx", "m_UVy", "m_pivotX", "m_pivotY", "m_textureWidth", "m_textureHeight",
		"m_spriteWidth", "m_spriteHeight", "m_subdivisionsX", "m_subdivisionsY",
		"m_border", "m_atlasGridSubdivisions", "m_zOrder", "m_nonCollideLayerMask",
		"m_contraptionLayer", "m_scaleX", "m_scaleY", "m_initialized", "m_cos",
		"m_index", "m_autoRotationIndex", "m_maxIX", "m_maxIY", "m_maxU", "m_U1", "m_U2",
		"m_I", "m_leftFrames", "m_lastFrameIndex",
		// 运行时状态 / 内部标志 / 测量输出 / 派生量（改了无意义或无关）
		"m_pressTime", "m_initialAngle", "m_angleOutput", "m_angleOutputUp", "m_angleSelf",
		"m_latchValue", "m_pwmTick", "m_cResistance", "m_level1", "m_level2",
		"m_partPlacementTime", "m_workingTime", "m_shotStartTime", "m_ShootStartTime",
		"m_ReWindingStartTime", "m_WindingStartTime", "m_SpringVisualizationInitZ",
		"m_stickingAllowed", "m_needsRescan", "m_bypassed", "m_isUlMode", "m_hasSuperMagnet",
		"m_lookAtDraggedPartDistance", "m_lookAtDraggedPartTimer",
		"m_rolledDistance", "m_traveledDistance", "m_previousSpeed",
		"m_currentScale", "m_currentAlpha", "m_currentAlpha2",
		"m_safetyPressCount", "m_previousStretchFactor", "m_cutIndex",
		"m_lightConeTargetScale", "m_animationTime", "m_viewHalfWidth", "m_viewHalfHeight",
		"m_pressure", "m_targetLength", "m_balloonCount", "m_fuelAmount",
		"m_rotationSpeed", "m_realForce", "m_suppliedFuelAmount", "m_circumference",
		"m_spinSpeed", "m_funLevel", "m_fearLevel", "m_type", "m_sides", "m_offset",
		"m_scale", "m_height", "m_width", "m_maxMove",
		"m_split", "m_speedBoost", "m_disturbance", "m_alarmTimer",
		// 更多状态/内部量/无关项
		"m_lookDirectionChangeTime", "m_shootTime", "m_hookDistance", "m_activatedAngle",
		"m_pressureID", "m_connectTime", "m_colliderRadius", "m_collisionIgnored",
		"m_invincibleApplied", "m_jointsDetached", "m_destroyCorpse", "m_hasExploded",
		"m_attached", "m_connected", "m_locked", "m_frozen", "m_gravityEnabled",
		"m_spawned", "m_generated", "m_isPlaced", "m_isRunning", "m_isSpecial",
		"m_pressCount", "m_useCount", "m_spawnCount", "m_generateCount", "m_cycleCount",
		"m_loopCount", "m_frameCount", "m_tickCount", "m_stepCount", "m_bufferSize",
		"m_cacheSize", "m_poolSize", "m_gridSize", "m_cellSize", "m_tileSize",
		"m_layerMask", "m_collisionMask", "m_ignoreMask", "m_rayCount", "m_raycastCount",
		"m_startAngle", "m_endAngle", "m_targetAngle", "m_targetScale", "m_targetPos",
		"m_dirX", "m_dirY", "m_normalX", "m_normalY", "m_screenWidth", "m_screenHeight",
		"m_fovMin", "m_fovMax", "m_zoomMin", "m_zoomMax", "m_panSpeed", "m_zoomSpeed",
		// 类型相关（改了会被当成别的部件）
		"Type", "PartType", "m_objectType", "m_resistorType", "CurrentResistorType",
		"m_variant", "m_kind", "m_category", "m_indexType", "m_spriteType"
	};

	// 常规（默认显示）的“相关”成员：其余一律默认隐藏（可用“隐藏项”开关查看）。
	// 另外，声明在**部件自身具体类型**上的成员一律视为相关（见 IsHiddenMember）。
	private static readonly HashSet<string> s_relevant = new HashSet<string>
	{
		"UseGravity", "LinearDrag", "PowerMultiplier", "SuctionMultiplier",
		"OffsetX", "OffsetY", "RotationOffset", "CollisionLayer", "DepthLayer",
		"m_stiffness", "m_damping",
		// BasePart 通用可调
		"m_mass", "m_interactiveRadius", "m_breakVelocity", "m_powerConsumption", "m_enginePower", "m_ZOffset",
		// 部件常见设计参数
		"m_force", "m_maximumSpeed", "m_numberOfBalloons", "m_boostForce", "m_ignitionTime",
		"m_boostDuration", "m_boostEndDuration", "m_loopAudioTimeLimit",
		"m_explosionImpulse", "m_explosionRadius", "m_triggerSpeed",
		"m_stickiness", "m_drag", "m_springStiffness", "m_maximumForce",
		"m_defaultSpeed", "m_maximumRotationSpeed", "m_radius", "m_speedMin", "m_speedMax",
		"m_rewindingTime", "m_ShootTime", "m_springStrength", "m_minimumDistance",
		"m_maxHookDistance", "m_maxWindingSpeed", "m_targetDistanceY", "m_targetDeviationX",
		"m_WindingTime", "m_SpringYDrive", "m_SpringYDriveDamper",
		"m_windPowerFactor", "m_electricity", "m_componentSize",
		"m_expressionSetTime", "m_pitch", "m_speedThreshold", "m_GlueDirectionMask",
		"m_maxFuelAmount", "m_requiredForce", "m_torque",
		"CoolingTime", "ForcedCoolingTime",
		// 额外可调项（部件自定义）
		"Resistance", "Capacitance", "MaxVoltage", "Charge",
		// 组件里明确相关的
		"EntityLight.m_length", "EntityLight.m_halfWidth"
	};

	// 不适合作为可调参数的成员名（字段/属性通用）。
	private static readonly HashSet<string> s_excluded = new HashSet<string>
	{
		"m_coordX", "m_coordY",
		"m_gridXmin", "m_gridXmax", "m_gridYmin", "m_gridYmax",
		"customPartIndex",
		"m_partTier", "m_jointType", "m_autoAlign",
		"m_jointConnectionType", "m_jointConnectionDirection",
		"m_customJointConnectionDirection", "m_jointConnectionStrength",
		// 属性噪声
		"Index", "CoordX", "CoordY",
		"ConnectedComponent", "StrictConnectedComponent", "GeneralConnectedComponent",
		"GeneratorRefCount", "GenerationLevel", "GenerationIndex",
		"PowerConsumption", "EnginePower",
		"ComponentIndex", "ComponentSize",
		"LastMovement", "QuickIconScale", "Data", "Part", "Light", "Transform"
	};

	// 不枚举其成员的内置/渲染类组件。
	private static readonly HashSet<string> s_componentDeny = new HashSet<string>
	{
		"Sprite", "INSerializedSprite", "MeshFilter", "MeshRenderer",
		"ParticleSystem", "ParticleSystemRenderer", "AudioSource",
		"BoxCollider", "BoxCollider2D", "CircleCollider2D", "SphereCollider",
		"CapsuleCollider", "MeshCollider", "Rigidbody", "Rigidbody2D",
		"Transform", "RectTransform", "CanvasRenderer", "SpriteRenderer",
		"LineRenderer", "TrailRenderer", "Animator",
		"INPartParameterPanel", "INScrollArea"
	};

	// 仅用于“显示”的成员名翻译表；未收录的只显示原名。
	private static readonly Dictionary<string, string> s_names = new Dictionary<string, string>
	{
		// BasePart 通用
		{ "m_mass", "质量" },
		{ "UseGravity", "受重力影响" },
		{ "LinearDrag", "刚体线性阻力" },
		{ "PowerMultiplier", "动力倍率" },
		{ "SuctionMultiplier", "吸附倍率" },
		{ "OffsetX", "部件X平移" },
		{ "OffsetY", "部件Y平移" },
		{ "RotationOffset", "部件附加旋转" },
		{ "CollisionLayer", "连接与碰撞层(1-6)" },
		{ "DepthLayer", "显示层偏移(-50至50)" },
		{ "m_interactiveRadius", "交互半径" },
		{ "m_breakVelocity", "断裂速度" },
		{ "m_powerConsumption", "耗电量" },
		{ "m_enginePower", "引擎功率" },
		{ "m_ZOffset", "Z偏移" },
		// 力 / 速度 / 气球
		{ "m_force", "作用力" },
		{ "m_maximumSpeed", "最大速度" },
		{ "m_numberOfBalloons", "气球数量" },
		// 鼓风 / 火箭
		{ "m_boostForce", "推力" },
		{ "m_ignitionTime", "点火延迟" },
		{ "m_boostDuration", "推进时长" },
		{ "m_boostEndDuration", "收尾时长" },
		{ "m_loopAudioTimeLimit", "循环音效时长" },
		// 爆炸
		{ "m_explosionImpulse", "爆炸冲量" },
		{ "m_explosionRadius", "爆炸半径" },
		{ "m_triggerSpeed", "触发速度" },
		// 轮子
		{ "m_stickiness", "粘性" },
		{ "m_drag", "空气阻力" },
		{ "m_springStiffness", "弹簧刚度" },
		// 风扇
		{ "m_defaultSpeed", "默认转速" },
		// 磁铁
		{ "m_radius", "作用半径" },
		{ "m_speedMin", "最小速度" },
		{ "m_speedMax", "最大速度" },
		// 抓钩 / 弹拳
		{ "m_rewindingTime", "回收时间" },
		{ "m_ShootTime", "发射时间" },
		{ "m_springStrength", "弹簧强度" },
		{ "m_minimumDistance", "最小距离" },
		{ "m_maxHookDistance", "最大钩距" },
		{ "m_maxWindingSpeed", "最大收线速度" },
		{ "m_targetDistanceY", "目标纵向距离" },
		{ "m_targetDeviationX", "横向偏差" },
		{ "m_WindingTime", "收回时间" },
		{ "m_SpringYDrive", "纵向弹簧力" },
		{ "m_SpringYDriveDamper", "纵向弹簧阻尼" },
		// 机炮（属性）
		{ "CoolingTime", "冷却时间(射速)" },
		{ "ForcedCoolingTime", "强制冷却" },
		// 风 / 光 / 猪 / 杂项
		{ "m_windPowerFactor", "风力系数" },
		{ "m_electricity", "电量" },
		{ "m_componentSize", "组件尺寸" },
		{ "m_expressionSetTime", "表情持续时间" },
		{ "m_pitch", "音调" },
		{ "m_speedThreshold", "速度阈值" },
		{ "m_GlueDirectionMask", "粘合方向掩码" },
		{ "m_scale", "缩放" },
		{ "m_offset", "偏移" },
		{ "m_range", "作用范围" },
		{ "m_strenght", "强度" },
		{ "m_angle", "角度" },
		{ "m_power", "功率" },
		// 光组件（EntityLight）
		{ "m_length", "长度" },
		{ "Length", "长度" },
		{ "HalfWidth", "半宽" },
		{ "Angle", "角度" },
		{ "Sides", "边数" },
		{ "Type", "类型" },
		// ===== 更多部件/组件成员 =====
		// 轮子类
		{ "m_maximumForce", "最大力" },
		{ "m_circumference", "周长" },
		{ "m_spinSpeed", "旋转速度" },
		{ "m_thrust", "推力" },
		{ "m_thrustTimer", "推力计时" },
		{ "m_spiderTimer", "吸附计时" },
		// 通用计时/状态
		{ "m_timer", "计时" },
		{ "m_value", "数值" },
		{ "m_delay", "延迟" },
		{ "m_maxDelay", "最大间隔" },
		{ "m_lastTime", "上次时间" },
		{ "m_lastTimeTouchedGround", "上次落地时间" },
		{ "m_moveTime", "移动时长" },
		{ "m_deltaAngle", "角度增量" },
		{ "m_maxMove", "最大移动" },
		// 风扇/螺旋桨
		{ "m_maximumRotationSpeed", "最大转速" },
		{ "m_rotationSpeed", "转速" },
		// 火箭/推进
		{ "m_timeBoostStarted", "推进开始时间" },
		{ "m_currentScale", "当前缩放" },
		{ "m_currentAlpha", "当前透明度" },
		{ "m_currentAlpha2", "当前透明度2" },
		{ "m_explodeTime", "爆炸延时" },
		// 猪 / 生物
		{ "m_blinkTimer", "眨眼计时" },
		{ "m_starsTimer", "星星计时" },
		{ "m_rolledDistance", "滚动距离" },
		{ "m_traveledDistance", "移动距离" },
		{ "m_lookDirectionChangeTime", "转向间隔" },
		{ "m_lookAtDraggedPartDistance", "注视距离" },
		{ "m_lookAtDraggedPartTimer", "注视计时" },
		{ "m_partPlacementTime", "放置时间" },
		{ "m_funLevel", "快乐值" },
		{ "m_fearLevel", "恐惧值" },
		{ "m_randomLaughTime", "随机大笑时间" },
		{ "m_stopTestTimer", "停止检测计时" },
		{ "m_workingTime", "工作计数" },
		{ "m_currentMagnitude", "当前速度" },
		{ "m_previousMagnitude", "上帧速度" },
		{ "m_minWrenchAnimationDelay", "最短扳手动画延迟" },
		{ "m_maxWrenchAnimationDelay", "最长扳手动画延迟" },
		{ "m_minBlinkAnimationDelay", "最短眨眼延迟" },
		{ "m_maxBlinkAnimationDelay", "最长眨眼延迟" },
		{ "m_wrenchAnimationTimer", "扳手动画计时" },
		{ "m_blinkAnimationTimer", "眨眼动画计时" },
		// 通用组件
		{ "m_Timeout", "超时" },
		{ "m_MinRelativeSpeed", "最小相对速度" },
		{ "m_LastPlayTime", "上次播放时间" },
		{ "m_lightConeTargetScale", "光锥目标缩放" },
		// 蛋 / 踢 / 门 / 机械
		{ "m_safetyPressCount", "安全按压次数" },
		{ "m_safetyTimer", "安全计时" },
		{ "m_connectTime", "连接时间" },
		{ "m_targetLength", "目标长度" },
		{ "m_activatedAngle", "激活角度" },
		{ "m_torque", "力矩" },
		// 框体颜色
		{ "m_alpha", "透明度" },
		{ "m_foregroundAlpha", "前景透明度" },
		{ "m_backgroundAlpha", "背景透明度" },
		// 光组件其他成员
		{ "m_type", "类型" },
		{ "m_halfWidth", "半宽" },
		{ "m_sides", "边数" },
		// 机炮 / 抓钩 / 子弹
		{ "m_shootTime", "射击时间" },
		{ "m_hookDistance", "钩索距离" },
		{ "m_plungerResetTimer", "回弹冷却" },
		{ "m_plungerTimer", "回弹计时" },
		{ "m_ttl", "存活时间" },
		{ "m_explosionCount", "爆炸次数" },
		// 喷气 / 燃料
		{ "m_requiredForce", "需求推力" },
		{ "m_realForce", "实际推力" },
		{ "m_suppliedFuelAmount", "供油量" },
		{ "m_fuelAmount", "燃料量" },
		{ "m_maxFuelAmount", "最大燃料量" },
		// 电路
		{ "m_autoRotationIndex", "自动旋转索引" },
		{ "m_maxCurrent", "最大电流" },
		{ "m_maxU", "最大电压" },
		{ "m_U1", "电压1" },
		{ "m_U2", "电压2" },
		{ "m_I", "电流" },
		{ "m_maxIX", "最大电流X" },
		{ "m_maxIY", "最大电流Y" },
		// 鸟 / 生物
		{ "m_relativeSlingForce", "弹弓相对力" },
		{ "m_speedBoost", "速度加成" },
		{ "m_split", "分裂数" },
		{ "m_disturbance", "扰动" },
		{ "m_speed", "速度" },
		{ "m_previousSpeed", "上帧速度" },
		{ "m_colliderRadius", "碰撞半径" },
		{ "m_distanceToSlingShot", "距弹弓距离" },
		{ "m_alarmTimer", "警报计时" },
		// 罗盘 / 指示器
		{ "m_viewHalfWidth", "视野半宽" },
		{ "m_viewHalfHeight", "视野半高" },
		{ "m_animationTime", "动画时长" },
		// 波动组件
		{ "m_rangeX", "范围X" },
		{ "m_rangeY", "范围Y" },
		{ "m_speedX", "速度X" },
		{ "m_speedY", "速度Y" },
		{ "m_startX", "起始X" },
		{ "m_startY", "起始Y" },
		{ "m_rangeScale", "范围缩放" },
		{ "m_speedScale", "速度缩放" },
		{ "m_startScale", "起始缩放" },
		{ "m_zOffset", "Z偏移" },
		{ "m_balloonCount", "气球数" },
		{ "m_scaleX", "缩放X" },
		{ "m_scaleY", "缩放Y" },
		// 属性名（可写数值属性）
		{ "Charge", "电荷" },
		// 额外可调项（部件自定义）
		{ "Resistance", "电阻" },
		{ "Capacitance", "电容" },
		{ "MaxVoltage", "耐压" },
		{ "ChargeAmount", "电荷量" },
		{ "SourceVoltage", "电源电压" },
		{ "WheelRadius", "轮半径" },
		{ "OffRoadWheelRadius", "越野轮半径" },
		{ "GateLength", "门长度" },
		{ "GateSpeed", "伸出速度" },
		{ "GateWidth", "门宽度" },
		{ "GateThickness", "门厚度" },
		{ "GateOpacity", "门不透明度" },
		{ "Radius", "作用半径" },
		{ "Height", "高度" },
		{ "Width", "宽度" },
		{ "Mass", "质量" },
		{ "Bounciness", "弹性" },
		{ "Layer", "图层" },
		{ "Speed", "速度" },
		{ "Force", "力" },
		{ "Delay", "延迟" },
		{ "Strength", "强度" },
		// 颜色 / 透明度 / 染色相关
		{ "m_color", "颜色" },
		{ "m_transparentColor", "透明色" },
		{ "Color", "颜色" },
		{ "TransparentColor", "透明色" },
		{ "Opacity", "透明度" },
		{ "m_opacity", "透明度" },
		{ "alpha", "透明度" },
		{ "Alpha", "透明度" },
		{ "Colorable", "可染色" },
		{ "m_colorable", "可染色" },
		{ "IsColorable", "可染色" },
		{ "CanBeColored", "可染色" },
		{ "m_canBeColored", "可染色" },
		// 其他部件/组件成员
		{ "m_pressure", "压力" },
		{ "m_pressureID", "压力通道" },
		{ "m_cutIndex", "切割点" },
		{ "m_previousStretchFactor", "上帧拉伸" },
		{ "m_targetTime", "目标时间" },
		{ "m_timeLimit", "时间限制" },
		{ "m_border", "边框" },
		{ "m_atlasGridSubdivisions", "图集细分" },
		{ "m_zOrder", "显示层级" },
		{ "m_UVx", "UV坐标X" },
		{ "m_UVy", "UV坐标Y" },
		{ "m_pivotX", "轴心X" },
		{ "m_pivotY", "轴心Y" },
		{ "m_textureWidth", "贴图宽" },
		{ "m_textureHeight", "贴图高" },
		{ "m_spriteWidth", "精灵宽" },
		{ "m_spriteHeight", "精灵高" },
		{ "m_subdivisionsX", "细分X" },
		{ "m_subdivisionsY", "细分Y" },
		{ "m_cloudVelocity", "云速度" },
		{ "m_cloudLimits", "云范围" },
		{ "m_farPlane", "远平面" },
		{ "m_maxClouds", "最大云数" },
		{ "m_height", "高度" },
		{ "m_width", "宽度" },
		{ "m_restLength", "静止长度" },
		{ "m_shotStartTime", "发射开始时间" },
		{ "m_ShootStartTime", "发射开始时间" },
		{ "m_ReWindingStartTime", "回收开始时间" },
		{ "m_WindingStartTime", "收回开始时间" },
		{ "m_SpringVisualizationInitZ", "弹簧可视化初始Z" },
		{ "m_nonCollideLayerMask", "非碰撞层掩码" },
		{ "m_contraptionLayer", "载具层" },
		// 其他常见可调名（部件/组件）
		{ "m_pressTime", "按压时间" },
		{ "m_initialAngle", "初始角度" },
		{ "m_angleOutput", "角度输出" },
		{ "m_latchValue", "锁存值" },
		{ "m_pwmTick", "PWM周期" },
		{ "m_dynamicAttachJointStrength", "挂钩强度" },
		{ "m_boostPower", "推进力" },
		{ "m_maxForce", "最大力" },
		{ "m_minForce", "最小力" },
		{ "m_charge", "电荷" },
		{ "m_chargeAmount", "电荷量" },
		{ "m_maxCharge", "最大电荷" },
		{ "m_capacitance", "电容" },
		{ "m_resistance", "电阻" },
		{ "m_maxVoltage", "最大电压" },
		{ "m_speedMultiplier", "速度倍率" },
		{ "m_forceMultiplier", "力倍率" },
		{ "m_torqueMultiplier", "力矩倍率" },
		{ "m_lifetime", "存活时间" },
		{ "m_duration", "持续时间" },
		{ "m_interval", "间隔" },
		{ "m_frequency", "频率" },
		{ "m_amplitude", "振幅" },
		{ "m_radiusX", "半径X" },
		{ "m_radiusY", "半径Y" },
		{ "m_angleOffset", "角度偏移" },
		{ "m_heightOffset", "高度偏移" },
		{ "m_offsetZ", "Z偏移" },
		{ "m_gravityScale", "重力系数" },
		{ "GravityEggMass", "重力蛋质量" },
		{ "GravityEggForce", "重力蛋引力" },
		{ "GravityEggInnerRadius", "重力蛋内半径" },
		{ "GravityEggOuterRadius", "重力蛋影响半径" },
		{ "m_buoyancy", "浮力" },
		{ "m_density", "密度" },
		{ "m_friction", "摩擦力" },
		{ "m_bounciness", "弹性" },
		// 更多常见参数名（覆盖面用）
		{ "m_maxSpeed", "最大速度" },
		{ "m_minSpeed", "最小速度" },
		{ "m_acceleration", "加速度" },
		{ "m_deceleration", "减速度" },
		{ "m_damping", "阻尼" },
		{ "m_stiffness", "刚度" },
		{ "m_springForce", "弹簧力" },
		{ "m_impulse", "冲量" },
		{ "m_restitution", "弹性" },
		{ "m_massScale", "质量系数" },
		{ "m_scaleFactor", "缩放系数" },
		{ "m_size", "尺寸" },
		{ "m_sizeX", "X尺寸" },
		{ "m_sizeY", "Y尺寸" },
		{ "m_sizeZ", "Z尺寸" },
		{ "m_speedLimit", "速度上限" },
		{ "m_torqueLimit", "力矩上限" },
		{ "m_forceLimit", "力上限" },
		{ "m_maxAngle", "最大角度" },
		{ "m_minAngle", "最小角度" },
		{ "m_angleRange", "角度范围" },
		{ "m_boostSpeed", "推进速度" },
		{ "m_launchForce", "发射力" },
		{ "m_pullForce", "拉力" },
		{ "m_pushForce", "推力" },
		{ "m_pushPower", "推力" },
		{ "m_lift", "升力" },
		{ "m_holdTime", "保持时间" },
		{ "m_waitTime", "等待时间" },
		{ "m_cooldown", "冷却时间" },
		{ "m_reloadTime", "装填时间" },
		{ "m_burstCount", "连发数" },
		{ "m_ammoCount", "弹药数" },
		{ "m_spread", "散布" },
		{ "m_recoil", "后坐力" },
		{ "m_damage", "伤害" },
		{ "m_health", "生命" },
		{ "m_capacity", "容量" },
		{ "m_efficiency", "效率" },
		{ "m_rangeMin", "最小范围" },
		{ "m_rangeMax", "最大范围" },
		{ "m_triggerDelay", "触发延时" },
		{ "m_detonationRadius", "引爆半径" },
		{ "m_dragCoefficient", "阻力系数" },
		{ "m_angularDrag", "角阻力" },
		{ "m_gravity", "重力" },
		{ "m_windSpeed", "风速" },
		{ "m_windDirection", "风向" },
		{ "m_lightRange", "光照范围" },
		{ "m_lightIntensity", "光照强度" },
		{ "m_emissionRate", "发射速率" },
		{ "m_particleSpeed", "粒子速度" },
		{ "m_particleSize", "粒子大小" },
		{ "m_particleLife", "粒子寿命" },
		{ "m_startSize", "起始大小" },
		{ "m_endSize", "结束大小" },
		// 更多常见参数名（二）
		{ "m_stretch", "拉伸" },
		{ "m_stretchX", "横向拉伸" },
		{ "m_stretchY", "纵向拉伸" },
		{ "m_rotZ", "Z旋转" },
		{ "m_eulerZ", "Z角" },
		{ "m_posX", "X位置" },
		{ "m_posY", "Y位置" },
		{ "m_offsetX", "X偏移" },
		{ "m_offsetY", "Y偏移" },
		{ "m_tint", "着色" },
		{ "m_glow", "辉光" },
		{ "m_flicker", "闪烁" },
		{ "m_pulse", "脉动" },
		{ "m_sway", "摇摆" },
		{ "m_rotateSpeed", "旋转速度" },
		{ "m_moveSpeed", "移动速度" },
		{ "m_climbSpeed", "攀爬速度" },
		{ "m_fallSpeed", "下落速度" },
		{ "m_jumpForce", "跳跃力" },
		{ "m_boostAmount", "推进量" },
		{ "m_chargeTime", "充能时间" },
		{ "m_dischargeTime", "放电时间" },
		{ "m_powerOutput", "输出功率" },
		{ "m_maxOutput", "最大输出" },
		{ "m_minOutput", "最小输出" },
		{ "m_voltage", "电压" },
		{ "m_inductance", "电感" },
		{ "m_positionX", "X位置" },
		{ "m_positionY", "Y位置" },
		{ "m_contactPoint", "接触点" },
		{ "m_groundCheck", "地面检测" },
		{ "m_grip", "抓地力" },
		{ "m_traction", "牵引力" },
		{ "Center", "中心" },
		{ "center", "中心" }
	};

	// 结合代码上下文的翻译：键 = "<部件类名或组件类型名>.<成员名>"，优先于 s_names。
	// 用于同一成员名在不同部件上含义不同的情况（如 m_force）。
	private static readonly Dictionary<string, string> s_ctxNames = new Dictionary<string, string>
	{
		// m_force 在不同部件上的含义
		{ "FanPropeller.m_force", "风力" },
		{ "MotorWheel.m_force", "驱动力" },
		{ "OffRoadWheel.m_force", "驱动力" },
		{ "StickyWheel.m_force", "驱动力" },
		{ "Umbrella.m_force", "伞阻力" },
		{ "PoweredUmbrella.m_force", "伞推力" },
		{ "Balloon.m_force", "浮力" },
		{ "Bellows.m_force", "鼓风力" },
		{ "Rocket.m_force", "推进力" },
		{ "SpringBoxingGlove.m_force", "出拳力" },
		{ "GrapplingHook.m_force", "钩索力" },
		// 其他按上下文
		{ "Balloon.m_numberOfBalloons", "气球数量" },
		{ "Sandbag.m_numberOfBalloons", "沙袋数量" },
		{ "FanPropeller.m_angle", "桨叶角度" },
		{ "EntityLight.m_angle", "扇形角度" },
		{ "SuperMagnet.m_radius", "磁力半径" },
		{ "CartWheel.m_radius", "轮半径" },
		{ "OffRoadWheel.m_radius", "轮半径" },
		{ "StickyWheel.m_radius", "轮半径" },
		{ "MotorWheel.m_radius", "轮半径" },
		{ "PoweredUmbrella.m_maximumForce", "最大伞力" },
		{ "Umbrella.m_maximumForce", "最大伞力" },
		{ "PigHat.m_speedThreshold", "触发速度阈值" },
		{ "PigMask.m_speedThreshold", "触发速度阈值" },
		{ "EntityLight.m_length", "光长度" },
		{ "EntityLight.m_halfWidth", "光半宽" },
		{ "EntityLight.m_electricity", "光电量" }
	};

	private const float RowHeight = 44f;
	private const float PanelWidth = 420f;
	private const float ScaleHeight = 156f;
	private const float TitleHeight = 52f;
	private const float HintHeight = 30f;
	private const float FooterHeight = 72f;
	private const float Pad = 20f;
	private const int MaximumRows = 10;
	private int RowCount = MaximumRows;

	private BasePart m_part;
	private Font m_font;

	// 面板内的工作副本：键 → 待保存值（点“保存”才写入部件）
	private readonly Dictionary<string, float> m_work = new Dictionary<string, float>();
	private readonly HashSet<string> m_originalKeys = new HashSet<string>();
	private readonly Dictionary<string, InputField> m_inputs = new Dictionary<string, InputField>();
	private readonly Dictionary<string, Text> m_labels = new Dictionary<string, Text>();
	private readonly List<Member> m_members = new List<Member>();
	// 当前可见（过滤掉隐藏项，除非开关打开或该项已被改过）
	private readonly List<Member> m_view = new List<Member>();
	// 布尔成员（显示 true/false）
	private readonly HashSet<string> m_boolKeys = new HashSet<string>();
	// 是否显示隐藏项（默认关；隐藏项被改过时也会显示）
	private bool m_showHidden;
	private Text m_toggleLabel;

	// 面板打开时的“当前值”（用于判断是否真的被改过）与“默认值”（用于恢复默认）
	private readonly Dictionary<string, float> m_baseline = new Dictionary<string, float>();
	private readonly Dictionary<string, float> m_defaults = new Dictionary<string, float>();

	private Transform m_rowsRoot;
	private Text m_indicator;
	private int m_offset;

	// 打开期间屏蔽其它输入
	private RectTransform m_panelRect;
	private readonly Dictionary<string, Slider> m_scaleSliders = new Dictionary<string, Slider>();
	private readonly Dictionary<string, InputField> m_scaleInputs = new Dictionary<string, InputField>();
	private bool m_refreshingScales;
	public static bool ContainsPointer(Vector2 position)
	{
		if (s_current == null || s_current.m_panelRect == null || !s_current.m_panelRect.gameObject.activeInHierarchy) return false;
		Canvas canvas = s_current.GetComponent<Canvas>();
		return RectTransformUtility.RectangleContainsScreenPoint(s_current.m_panelRect, position,
			canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null);
	}

	public static INPartParameterPanel Open(BasePart part)
	{
		if (part == null)
		{
			return null;
		}
		Close();
		GameObject go = new GameObject("INPartParameterPanel");
		INPartParameterPanel panel = go.AddComponent<INPartParameterPanel>();
		s_current = panel;
		IsOpen = true;
		panel.Build(part);
		return panel;
	}

	public static void Close()
	{
		if (s_current != null)
		{
			if (Application.isPlaying) UnityEngine.Object.Destroy(s_current.gameObject);
			else UnityEngine.Object.DestroyImmediate(s_current.gameObject);
		}
		s_current = null;
		IsOpen = false;
	}

	private void OnDestroy()
	{
		if (s_current == this)
		{
			s_current = null;
			IsOpen = false;
		}
	}

	// The panel owns only its rectangle. Background construction remains interactive.

	private void Update()
	{
		if (m_part == null || (WPFMonoBehaviour.levelManager != null && WPFMonoBehaviour.levelManager.gameState != LevelManager.GameState.Building))
		{
			Close();
			return;
		}
		FitPanelToScreen();
	}

	private int FittedRowCount()
	{
		RectTransform root = transform as RectTransform;
		float height = root != null && root.rect.height > 0f ? root.rect.height : Screen.height;
		float fixedHeight = Pad * 2f + TitleHeight + HintHeight + ScaleHeight + FooterHeight;
		return Mathf.Clamp(Mathf.FloorToInt((height - 104f - 16f - fixedHeight) / RowHeight), 1, MaximumRows);
	}

	private void FitPanelToScreen()
	{
		if (m_panelRect == null || m_rowsRoot == null) return;
		int rows = FittedRowCount();
		if (rows == RowCount) return;
		float delta = (rows - RowCount) * RowHeight;
		var rowRect = (RectTransform)m_rowsRoot;
		RowCount = rows;
		m_panelRect.sizeDelta += Vector2.up * delta;
		for (int i = 0; i < m_panelRect.childCount; i++)
		{
			var child = m_panelRect.GetChild(i) as RectTransform;
			if (child == null || child == m_rowsRoot) continue;
			child.anchoredPosition += Vector2.up * (child.anchoredPosition.y > rowRect.anchoredPosition.y ? delta * 0.5f : -delta * 0.5f);
		}
		rowRect.sizeDelta = new Vector2(rowRect.sizeDelta.x, RowCount * RowHeight);
		RefreshRows();
	}

	private void Build(BasePart part)
	{
		m_part = part;
		m_font = INUiUtil.UiFont;

		// 用部件现有覆盖初始化工作副本
		m_work.Clear();
		m_originalKeys.Clear();
		if (part.CustomParams != null)
		{
			foreach (KeyValuePair<string, float> pair in part.CustomParams)
			{
				// Internal state (see BasePart.InternalParamPrefix) shares this storage because it is the
				// only per-part channel that persists, but it is not something the player tunes, so it
				// does not become a row.
				if (BasePart.IsInternalParam(pair.Key)) continue;
				m_work[pair.Key] = pair.Value;
				m_originalKeys.Add(pair.Key);
			}
		}

		// 收集可调项
		m_members.Clear();
		m_boolKeys.Clear();
		CollectMembers(part, m_members);
		foreach (Member mm in m_members)
		{
			if (mm.IsBool)
			{
				m_boolKeys.Add(mm.Key);
			}
		}
		// 与载具相关性高的排前面；其余按首字母
		m_members.Sort(CompareMembers);
		m_showHidden = GetShowHiddenPref();
		RebuildView();
		m_offset = 0;

		// 记录打开时的“显示值”（baseline）与默认值（defaults，取自预制模板）
		// baseline 用 ShownValue，保证“面板里显示的初始值”与 baseline 一致（不改动就不会被判为已改）
		m_baseline.Clear();
		m_defaults.Clear();
		foreach (Member mm in m_members)
		{
			float cur = ShownValue(mm.Key);
			m_baseline[mm.Key] = cur;
			float def;
			// 默认值：优先模板默认；但若模板给出 0 而当前值非 0（如由代码派生的值），
			// 则用当前值当默认，避免“恢复默认变成 0”。
			bool tplOk = TryReadTemplateValue(mm.Key, out def);
			m_defaults[mm.Key] = (tplOk && (def != 0f || cur == 0f)) ? def : cur;
		}

		Canvas canvas = gameObject.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 30000;
		CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920f, 1080f);
		scaler.matchWidthOrHeight = 0.5f;
		gameObject.AddComponent<GraphicRaycaster>();
		INUiScale.Register(canvas);

		RowCount = FittedRowCount();
		float panelHeight = Pad * 2f + TitleHeight + HintHeight + ScaleHeight + FooterHeight + RowCount * RowHeight;

		Image panel = CreateImage(transform, "Panel");
		RectTransform prt = panel.rectTransform;
		m_panelRect = prt;
		prt.anchorMin = prt.anchorMax = new Vector2(1f, 1f);
		prt.pivot = new Vector2(1f, 1f);
		prt.anchoredPosition = new Vector2(-16f, -104f);
		prt.sizeDelta = new Vector2(PanelWidth, panelHeight);
		panel.color = INBuildStyle.Panel;
		INBuildStyle.Bevel(panel.gameObject);

		float top = panelHeight * 0.5f;

		// 标题
		Text title = CreateText(panel.transform, "Title", 22, TextAnchor.MiddleLeft, INBuildStyle.Ivory);
		Place(title.rectTransform, 0f, top - Pad - TitleHeight * 0.5f, PanelWidth - 32f, TitleHeight);
		title.text = "部件调整  /  " + part.m_partType;

		// 提示
		Text hint = CreateText(panel.transform, "Hint", 13, TextAnchor.MiddleRight, INBuildStyle.Muted);
		Place(hint.rectTransform, 62f, top - Pad - TitleHeight - HintHeight * 0.5f, 244f, HintHeight);
		hint.text = "独立轴缩放 · 建造区仍可操作";

		// “显示隐藏选项”开关（默认关；隐藏项被改过时也会显示）
		UButton toggleHidden = CreateButton(panel.transform, "隐藏项:关", 16, new Color(0.3f, 0.32f, 0.42f, 1f));
		Place(toggleHidden.GetComponent<RectTransform>(), -140f, top - Pad - TitleHeight - HintHeight * 0.5f, 104f, HintHeight - 4f);
		toggleHidden.onClick.AddListener(OnToggleHiddenClicked);
		m_toggleLabel = toggleHidden.GetComponentInChildren<Text>();
		UpdateToggleLabel();

		// 行区域
		float scaleTop = top - Pad - TitleHeight - HintHeight;
		BuildScaleControls(panel.transform, scaleTop);
		float rowsTop = scaleTop - ScaleHeight;
		GameObject rowsGo = new GameObject("Rows", typeof(RectTransform));
		RectTransform rrt = (RectTransform)rowsGo.transform;
		rrt.SetParent(panel.transform, false);
		rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 0.5f);
		rrt.pivot = new Vector2(0.5f, 0.5f);
		rrt.sizeDelta = new Vector2(PanelWidth - 24f, RowCount * RowHeight);
		rrt.anchoredPosition = new Vector2(0f, rowsTop - RowCount * RowHeight * 0.5f);
		m_rowsRoot = rrt;

		// 行区域支持 拖动 / 滚轮（win 端）逐行滚动（手机端拖动同样走这里）
		// 容器自身加一个几乎透明的可命中底板：这样在行间空白处也能拖动/滚动
		Image rowsBg = rowsGo.AddComponent<Image>();
		rowsBg.color = new Color(1f, 1f, 1f, 0.001f);
		rowsBg.raycastTarget = true;

		INScrollArea rowsScroll = rowsGo.AddComponent<INScrollArea>();
		rowsScroll.PixelsPerStep = RowHeight;
		rowsScroll.WheelStepsPerNotch = 3f;
		rowsScroll.OnStep = OnRowsScrollStep;

		float footerY = -top + Pad + FooterHeight * 0.5f;

		// 分页
		UButton up = CreateButton(panel.transform, "▲", 20, new Color(0.35f, 0.35f, 0.4f, 1f));
		Place(up.GetComponent<RectTransform>(), -176f, footerY, 32f, 36f);
		up.onClick.AddListener(OnPageUp);

		UButton down = CreateButton(panel.transform, "▼", 20, new Color(0.35f, 0.35f, 0.4f, 1f));
		Place(down.GetComponent<RectTransform>(), -138f, footerY, 32f, 36f);
		down.onClick.AddListener(OnPageDown);

		m_indicator = CreateText(panel.transform, "Indicator", 17, TextAnchor.MiddleLeft, new Color(0.8f, 0.85f, 0.9f, 1f));
		Place(m_indicator.rectTransform, -76f, footerY, 84f, 36f);
		m_indicator.fontSize = 12;

		// 全部恢复默认 / 取消 / 保存
		UButton restoreAll = CreateButton(panel.transform, "全部恢复", 19, new Color(0.45f, 0.40f, 0.28f, 1f));
		Place(restoreAll.GetComponent<RectTransform>(), 12f, footerY, 82f, 36f);
		restoreAll.GetComponentInChildren<Text>().fontSize = 14;
		restoreAll.onClick.AddListener(OnRestoreAllClicked);

		UButton cancel = CreateButton(panel.transform, "关闭", 16, INBuildStyle.Button);
		Place(cancel.GetComponent<RectTransform>(), 95f, footerY, 70f, 36f);
		cancel.onClick.AddListener(OnCancelClicked);

		UButton save = CreateButton(panel.transform, "应用", 16, INBuildStyle.Brass);
		Place(save.GetComponent<RectTransform>(), 174f, footerY, 70f, 36f);
		save.onClick.AddListener(OnSaveClicked);

		RefreshRows();
	}

	private void Place(RectTransform rt, float x, float y, float w, float h)
	{
		rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
		rt.pivot = new Vector2(0.5f, 0.5f);
		rt.sizeDelta = new Vector2(w, h);
		rt.anchoredPosition = new Vector2(x, y);
	}

	private void OnPageUp()

	{
		m_offset -= RowCount;
		RefreshRows();
	}

	private void BuildScaleControls(Transform parent, float top)
	{
		string[] keys = { BasePart.PartScaleKey, BasePart.PartScaleXKey, BasePart.PartScaleYKey };
		string[] labels = { "整体缩放", "X 轴拉伸", "Y 轴拉伸" };
		for (int i = 0; i < keys.Length; i++)
		{
			string key = keys[i];
			float y = top - 28f - i * 44f;
			Text label = CreateText(parent, "ScaleLabel", 15, TextAnchor.MiddleLeft, INBuildStyle.Ivory);
			Place(label.rectTransform, -142f, y, 98f, 32f);
			Image track = CreateImage(parent, "Scale_" + key);
			Place(track.rectTransform, -6f, y, 160f, 8f);
			track.color = new Color(0.09f, 0.10f, 0.09f);
			label.text = labels[i];
			Slider slider = track.gameObject.AddComponent<Slider>();
			slider.minValue = 0.1f; slider.maxValue = 3f;
			Image handle = CreateImage(track.transform, "Handle");
			handle.color = INBuildStyle.Brass;
			handle.rectTransform.sizeDelta = new Vector2(12f, 24f);
			slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
			m_scaleSliders[key] = slider;
			InputField input = CreateInputField(parent, new Vector2(135f, y), new Vector2(102f, 32f));
			m_scaleInputs[key] = input;
			input.onEndEdit.AddListener(value => OnValueSubmitted(key, value));
			slider.onValueChanged.AddListener(value => {
				if (!m_refreshingScales) OnValueSubmitted(key, value.ToString(CultureInfo.InvariantCulture));
			});
		}
		RefreshScaleControls();
	}

	private void RefreshScaleControls()
	{
		m_refreshingScales = true;
		foreach (var entry in m_scaleSliders)
		{
			float value = BasePart.ValidScale(GetDisplayValue(entry.Key));
			entry.Value.SetValueWithoutNotify(value);
			m_scaleInputs[entry.Key].SetTextWithoutNotify(value.ToString("0.###", CultureInfo.InvariantCulture));
		}
		m_refreshingScales = false;
	}

	private void OnPageDown()
	{
		int max = Mathf.Max(0, m_view.Count - RowCount);
		m_offset = Mathf.Min(max, m_offset + RowCount);
		RefreshRows();
	}

	// 拖动 / 滚轮：逐行滚动
	private void OnRowsScrollStep(int steps)
	{
		if (steps == 0)
		{
			return;
		}
		int max = Mathf.Max(0, m_view.Count - RowCount);
		m_offset = Mathf.Clamp(m_offset + steps, 0, max);
		RefreshRows();
	}

	// 是否已改过（用于“隐藏项被修改后也显示”）
	private bool HasOverride(string key)
	{
		return m_work.ContainsKey(key) || m_originalKeys.Contains(key);
	}

	private bool IsHiddenKey(string key)
	{
		for (int i = 0; i < m_members.Count; i++)
		{
			if (m_members[i].Key == key)
			{
				return m_members[i].Hidden;
			}
		}
		return false;
	}

	// 按“显示隐藏选项”开关过滤出当前可见列表
	private void RebuildView()
	{
		m_view.Clear();
		for (int i = 0; i < m_members.Count; i++)
		{
			Member m = m_members[i];
			if (BasePart.IsScaleParam(m.Key)) continue;
			if (m.Hidden && !m_showHidden && !HasOverride(m.Key))
			{
				continue;
			}
			m_view.Add(m);
		}
	}

	private void OnToggleHiddenClicked()
	{
		m_showHidden = !m_showHidden;
		SetShowHiddenPref(m_showHidden);
		UpdateToggleLabel();
		m_offset = 0;
		RebuildView();
		RefreshRows();
	}

	private void UpdateToggleLabel()
	{
		if (m_toggleLabel != null)
		{
			m_toggleLabel.text = m_showHidden ? "隐藏项:开" : "隐藏项:关";
			m_toggleLabel.color = m_showHidden ? new Color(1f, 0.85f, 0.3f, 1f) : new Color(0.7f, 0.75f, 0.85f, 1f);
		}
	}

	private const string ShowHiddenPrefKey = "INParamPanelShowHidden";

	// 会话内静态缓存：即使 PlayerPrefs 写入失败，本次游戏内也不会每次都自动关闭
	private static bool s_showHiddenStatic;
	private static bool s_showHiddenStaticValid;

	private static bool GetShowHiddenPref()
	{
		if (s_showHiddenStaticValid)
		{
			return s_showHiddenStatic;
		}
		try
		{
			return PlayerPrefs.GetInt(ShowHiddenPrefKey, 0) != 0;
		}
		catch
		{
			return false;
		}
	}

	private static void SetShowHiddenPref(bool value)
	{
		s_showHiddenStatic = value;
		s_showHiddenStaticValid = true;
		try
		{
			PlayerPrefs.SetInt(ShowHiddenPrefKey, value ? 1 : 0);
			PlayerPrefs.Save();
		}
		catch
		{
		}
	}

	private void RefreshRows()
	{
		if (m_rowsRoot != null)
		{
			for (int i = m_rowsRoot.childCount - 1; i >= 0; i--)
			{
				GameObject row = m_rowsRoot.GetChild(i).gameObject;
				if (Application.isPlaying) UnityEngine.Object.Destroy(row);
				else UnityEngine.Object.DestroyImmediate(row);
			}
		}
		m_inputs.Clear();
		m_labels.Clear();

		int total = m_view.Count;
		if (total == 0)
		{
			if (m_rowsRoot != null)
			{
				Text none = CreateText(m_rowsRoot, "None", 19, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.6f));
				Place(none.rectTransform, 0f, RowHeight * 0.5f, PanelWidth - 60f, RowHeight);
				none.text = "没有找到可调数值项";
			}
			if (m_indicator != null)
			{
				m_indicator.text = "0 / 0";
			}
			return;
		}

		int maxOffset = Mathf.Max(0, total - RowCount);
		if (m_offset > maxOffset)
		{
			m_offset = maxOffset;
		}
		if (m_offset < 0)
		{
			m_offset = 0;
		}

		float topY = RowCount * RowHeight * 0.5f;
		for (int i = 0; i < RowCount; i++)
		{
			int idx = m_offset + i;
			if (idx >= total)
			{
				break;
			}
			CreateRow(m_rowsRoot, m_view[idx].Key, topY - (i + 0.5f) * RowHeight);
		}

		if (m_indicator != null)
		{
			m_indicator.text = (m_offset + 1) + "-" + Mathf.Min(m_offset + RowCount, total) + " / " + total;
		}
	}

	private void CreateRow(Transform parent, string key, float centerY)
	{
		Text label = CreateText(parent, "L_" + key, 13, TextAnchor.MiddleLeft, INBuildStyle.Ivory);
		label.horizontalOverflow = HorizontalWrapMode.Wrap;
		Place(label.rectTransform, -96f, centerY, 196f, RowHeight - 6f);
		label.text = key;

		float initial = GetDisplayValue(key);

		InputField input = CreateInputField(parent, new Vector2(60f, centerY), new Vector2(100f, RowHeight - 10f));
		input.text = FormatValue(key, initial);
		string captured = key;
		input.onEndEdit.AddListener(delegate(string value)
		{
			OnValueSubmitted(captured, value);
		});

		// “恢复”= 该行恢复默认值
		UButton restore = CreateButton(parent, "恢复", 17, new Color(0.45f, 0.40f, 0.28f, 1f));
		Place(restore.GetComponent<RectTransform>(), 155f, centerY, 70f, RowHeight - 14f);
		restore.onClick.AddListener(delegate
		{
			OnRestoreClicked(captured);
		});

		m_inputs[key] = input;
		m_labels[key] = label;
		RefreshRowAppearance(key);
	}

	// ===== 暂存 =====

	private void OnValueSubmitted(string key, string text)
	{
		if (m_part == null || string.IsNullOrEmpty(key))
		{
			return;
		}
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}
		float value;
		bool parsed;
		if (m_boolKeys.Contains(key))
		{
			parsed = TryParseBool(text, out value);
		}
		else
		{
			parsed = float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
				|| float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out value);
		}
		if (!parsed || !BuildMachineTransform.Finite(value))
		{
			if (m_inputs.TryGetValue(key, out var field))
			{
				field.text = FormatValue(key, GetDisplayValue(key));
			}
			return;
		}
		if (BasePart.IsScaleParam(key)) value = BasePart.ValidScale(value);
		m_work[key] = value;
		if (m_inputs.TryGetValue(key, out var input))
		{
			input.text = FormatValue(key, value);
		}
		// 与打开面板时的当前值相同 → 视为“没改过”
		if (!m_originalKeys.Contains(key) && m_baseline.TryGetValue(key, out var baseline) && Mathf.Approximately(value, baseline))
		{
			m_work.Remove(key);
		}
		RefreshRowAppearance(key);
		if (BasePart.IsScaleParam(key))
		{
			if (m_work.ContainsKey(key)) m_part.SetCustomParam(key, value);
			else { m_part.RemoveCustomParam(key); m_part.ApplyPartScale(); }
			RefreshScaleControls();
		}
	}

	// 值→显示文本（布尔显示 true/false，其余显示数值）
	private string FormatValue(string key, float value)
	{
		if (m_boolKeys.Contains(key))
		{
			return (value != 0f) ? "true" : "false";
		}
		return value.ToString(CultureInfo.InvariantCulture);
	}

	// 布尔解析：接受 true/false、1/0、on/off、yes/no、是/否、真/假、开/关
	private static bool TryParseBool(string text, out float value)
	{
		value = 0f;
		if (string.IsNullOrEmpty(text))
		{
			return false;
		}
		string t = text.Trim().ToLowerInvariant();
		if (t == "1" || t == "true" || t == "yes" || t == "on" || t == "是" || t == "真" || t == "开")
		{
			value = 1f;
			return true;
		}
		if (t == "0" || t == "false" || t == "no" || t == "off" || t == "否" || t == "假" || t == "关")
		{
			value = 0f;
			return true;
		}
		return false;
	}

	// 该行“恢复默认”：设为默认值（模板值），若与打开时的当前值相同则视为没改
	private void OnRestoreClicked(string key)
	{
		if (m_part == null)
		{
			return;
		}
		float def;
		if (!m_defaults.TryGetValue(key, out def))
		{
			def = ReadMemberValue(key);
		}
		// 已有覆盖时必须**显式写回默认值**，否则字段不会真的恢复
		if (!HasOverride(key) && m_baseline.TryGetValue(key, out var baseline) && Mathf.Approximately(def, baseline))
		{
			m_work.Remove(key);
		}
		else
		{
			m_work[key] = def;
		}
		if (m_inputs.TryGetValue(key, out var input))
		{
			input.text = FormatValue(key, def);
		}
		// 恢复后隐藏项可能重新变隐藏 → 重建视图
		if (BasePart.IsScaleParam(key)) { m_part.SetCustomParam(key, def); RefreshScaleControls(); }
		RebuildView();
		RefreshRows();
	}

	// 全部恢复默认（本页所有已改项）
	private void OnRestoreAllClicked()
	{
		if (m_part == null)
		{
			return;
		}
		List<string> keys = new List<string>(m_work.Keys);
		foreach (string key in keys)
		{
			float def;
			if (!m_defaults.TryGetValue(key, out def))
			{
				def = ReadMemberValue(key);
			}
			if (!HasOverride(key) && m_baseline.TryGetValue(key, out var baseline) && Mathf.Approximately(def, baseline))
			{
				m_work.Remove(key);
			}
			else
			{
				m_work[key] = def;
			}
			if (m_inputs.TryGetValue(key, out var input))
			{
				input.text = FormatValue(key, def);
			}
			RefreshRowAppearance(key);
			if (BasePart.IsScaleParam(key)) m_part.SetCustomParam(key, def);
		}
		RebuildView();
		RefreshRows();
		RefreshScaleControls();
	}

	private void OnSaveClicked()
	{
		if (m_part == null)
		{
			Close();
			return;
		}
		foreach (KeyValuePair<string, float> pair in m_work)
		{
			m_part.SetCustomParam(pair.Key, pair.Value);
		}
		foreach (string key in m_originalKeys)
		{
			if (!m_work.ContainsKey(key))
			{
				m_part.RemoveCustomParam(key);
			}
		}
		INQuickInherit.Remember(m_part);
		m_part.ApplyPartScale();
		PartPhysicsParameters.Refresh(m_part);
		m_part.contraption?.RefreshPartPose(m_part);
		m_originalKeys.Clear();
		foreach (string key in m_work.Keys) m_originalKeys.Add(key);
		RefreshRows();
		RefreshScaleControls();
	}

	private void OnCancelClicked()
	{
		Close();
	}

	// 显示名：原名[中文]（未收录翻译则只显示原名）；颜色通道加“·红/绿/蓝/透明度”；
	// 布尔项不加额外标记（玩家看 true/false 自会判断，且不要顶掉翻译）；有覆盖时追加 “ *”。
	private string DisplayName(string key, bool overridden, bool isBool)
	{
		SplitKey(key, out string comp, out string member, out string channel);
		string text = key;
		string zh = ResolveZh(comp, member);
		string suffix = null;
		if (!string.IsNullOrEmpty(zh))
		{
			suffix = zh + (channel != null ? ("·" + ChannelLabel(channel)) : "");
		}
		else if (channel != null)
		{
			suffix = ChannelLabel(channel);
		}
		if (!string.IsNullOrEmpty(suffix))
		{
			text = key + "[" + suffix + "]";
		}
		return overridden ? (text + " *") : text;
	}

	// 结合上下文的译名：组件成员用组件类型名，自身成员用部件类名（沿继承链向上找）
	private string ResolveZh(string comp, string member)
	{
		string zh;
		if (comp != null)
		{
			if (s_ctxNames.TryGetValue(comp + "." + member, out zh) && !string.IsNullOrEmpty(zh))
			{
				return zh;
			}
		}
		else if (m_part != null)
		{
			Type t = m_part.GetType();
			while (t != null && typeof(BasePart).IsAssignableFrom(t))
			{
				if (s_ctxNames.TryGetValue(t.Name + "." + member, out zh) && !string.IsNullOrEmpty(zh))
				{
					return zh;
				}
				t = t.BaseType;
			}
		}
		if (s_names.TryGetValue(member, out zh) && !string.IsNullOrEmpty(zh))
		{
			return zh;
		}
		return null;
	}

	private static string ChannelLabel(string channel)
	{
		switch (channel)
		{
			case "r": return "红";
			case "g": return "绿";
			case "b": return "蓝";
			case "a": return "透明度";
			default: return channel;
		}
	}

	// 拆键： [组件类型.]成员[@通道]
	private static void SplitKey(string key, out string comp, out string member, out string channel)
	{
		comp = null;
		channel = null;
		member = key;
		if (string.IsNullOrEmpty(key))
		{
			return;
		}
		int dot = key.IndexOf('.');
		if (dot > 0 && dot < key.Length - 1)
		{
			comp = key.Substring(0, dot);
			member = key.Substring(dot + 1);
		}
		int at = member.IndexOf('@');
		if (at > 0 && at < member.Length - 1)
		{
			channel = member.Substring(at + 1);
			member = member.Substring(0, at);
		}
	}

	// 把成员值折算成单个 float（bool→0/1；枚举→int；Color→按通道取值）
	private static float ToFloat(object value, string channel)
	{
		if (value == null)
		{
			return 0f;
		}
		if (channel != null && value is Color)
		{
			Color c = (Color)value;
			switch (channel)
			{
				case "r": return c.r;
				case "g": return c.g;
				case "b": return c.b;
				case "a": return c.a;
			}
			return 0f;
		}
		if (value is bool)
		{
			return ((bool)value) ? 1f : 0f;
		}
		try
		{
			return Convert.ToSingle(value, CultureInfo.InvariantCulture);
		}
		catch
		{
			return 0f;
		}
	}

	private void RefreshRowAppearance(string key)
	{
		bool overridden = m_work.ContainsKey(key);
		if (m_labels.TryGetValue(key, out var label) && label != null)
		{
			label.text = DisplayName(key, overridden, m_boolKeys.Contains(key));
			if (overridden)
			{
				// 已改：黄色
				label.color = new Color(1f, 0.85f, 0.3f, 1f);
			}
			else if (IsHiddenKey(key))
			{
				// 隐藏项：灰蓝色（与常规项区分）
				label.color = new Color(0.55f, 0.6f, 0.72f, 1f);
			}
			else
			{
				label.color = Color.white;
			}
		}
	}

	private float GetDisplayValue(string key)
	{
		if (m_work.TryGetValue(key, out var staged))
		{
			return staged;
		}
		return ShownValue(key);
	}

	// 未改过时的显示值：优先“当前值”；当前为 0（或读不到）时回退到**模板默认值**，避免误显示 0。
	private float ShownValue(string key)
	{
		bool liveOk = TryReadMemberValue(key, out float live);
		bool tplOk = TryReadTemplateValue(key, out float tpl);
		if (liveOk && live != 0f)
		{
			return live;
		}
		if (tplOk)
		{
			return tpl;
		}
		return liveOk ? live : 0f;
	}

	private float ReadMemberValue(string key)
	{
		TryReadMemberValue(key, out float value);
		return value;
	}

	private bool TryReadMemberValue(string key, out float value)
	{
		value = 0f;
		if (m_part == null || string.IsNullOrEmpty(key))
		{
			return false;
		}
		try
		{
			SplitKey(key, out string comp, out string member, out string channel);
			object target = m_part;
			if (comp != null)
			{
				MonoBehaviour c = FindComponent(comp);
				if (c == null)
				{
					return false;
				}
				target = c;
			}
			Type stopAt = (target is BasePart) ? typeof(BasePart) : null;
			Type type = target.GetType();
			while (type != null && (stopAt == null || stopAt.IsAssignableFrom(type)))
			{
				FieldInfo f = type.GetField(member, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
				if (f != null)
				{
					value = ToFloat(f.GetValue(target), channel);
					return true;
				}
				PropertyInfo p = type.GetProperty(member, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
				if (p != null && p.CanRead && p.GetIndexParameters().Length == 0)
				{
					value = ToFloat(p.GetValue(target, null), channel);
					return true;
				}
				type = type.BaseType;
			}
			// 额外可调项（如电阻/电容）：向部件询问当前值
			if (target == m_part && channel == null && m_part.TryGetExtraParamValue(member, out float extra))
			{
				value = extra;
				return true;
			}
		}
		catch
		{
		}
		return false;
	}

	// 从预制模板读取该成员的“默认值”（拿不到则返回 false）
	private bool TryReadTemplateValue(string key, out float value)
	{
		value = 0f;
		if (m_part == null || string.IsNullOrEmpty(key))
		{
			return false;
		}
		try
		{
			BasePart template = (WPFMonoBehaviour.gameData != null)
				? WPFMonoBehaviour.gameData.GetCustomPart(m_part.m_partType, m_part.customPartIndex)
				: null;
			if (template == null)
			{
				return false;
			}
			object target = template;
			SplitKey(key, out string compName, out string member, out string channel);
			if (compName != null)
			{
				MonoBehaviour comp = null;
				MonoBehaviour[] comps = template.GetComponentsInChildren<MonoBehaviour>(true);
				foreach (MonoBehaviour c in comps)
				{
                        if (MatchesComponentType(c, compName))
					{
						comp = c;
						break;
					}
				}
				if (comp == null)
				{
					return false;
				}
				target = comp;
			}
			Type stopAt = (target is BasePart) ? typeof(BasePart) : null;
			Type type = target.GetType();
			while (type != null && (stopAt == null || stopAt.IsAssignableFrom(type)))
			{
				FieldInfo f = type.GetField(member, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
				if (f != null)
				{
					value = ToFloat(f.GetValue(target), channel);
					return true;
				}
				PropertyInfo p = type.GetProperty(member, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
				if (p != null && p.CanRead && p.GetIndexParameters().Length == 0)
				{
					value = ToFloat(p.GetValue(target, null), channel);
					return true;
				}
				type = type.BaseType;
			}
			// 额外可调项：向模板部件询问默认值
			if (target == template && channel == null && template.TryGetExtraParamValue(member, out float extra))
			{
				value = extra;
				return true;
			}
		}
		catch
		{
		}
		return false;
	}

	private MonoBehaviour FindComponent(string typeName)
	{
		if (m_part == null)
		{
			return null;
		}
		MonoBehaviour[] comps = m_part.GetComponentsInChildren<MonoBehaviour>(true);
		foreach (MonoBehaviour c in comps)
		{
			if (MatchesComponentType(c, typeName))
			{
				return c;
			}
		}
		return null;
	}

	private static bool MatchesComponentType(MonoBehaviour component, string name)
	{
		if (component == null) return false;
		for (Type type = component.GetType(); type != null && typeof(MonoBehaviour).IsAssignableFrom(type); type = type.BaseType)
			if (type.Name == name) return true;
		return false;
	}

	// ===== 枚举可调项 =====

	private static void CollectMembers(BasePart part, List<Member> list)
	{
		HashSet<string> seen = new HashSet<string>();
		var physicsKeys = new List<string>();
		PartPhysicsParameters.Collect(part, physicsKeys);
		foreach (string key in physicsKeys) AddOne(list, seen, key, key == PartPhysicsParameters.GravityKey);
		string selfClass = part.GetType().Name;
		// 1) 部件自身
		AddTypeMembers(part.GetType(), typeof(BasePart), null, list, seen, true, selfClass);
		// 2) 本体组件
		AddComponentMembers(part.gameObject, list, seen);
		// 3) 直接子物体组件
		Transform t = part.transform;
		for (int i = 0; i < t.childCount; i++)
		{
			AddComponentMembers(t.GetChild(i).gameObject, list, seen);
		}
		// 4) 部件自定义的“额外可调项”（无法用字段表达的值，如电阻/电容）
		try
		{
			List<string> extras = new List<string>();
			part.CollectExtraParams(extras);
			PartPhysicsParameters.Collect(part, extras);
			foreach (string k in extras)
			{
				AddOne(list, seen, k, k == PartPhysicsParameters.GravityKey);
			}
		}
		catch
		{
		}
	}

	private static void AddComponentMembers(GameObject go, List<Member> list, HashSet<string> seen)
	{
		MonoBehaviour[] comps = go.GetComponents<MonoBehaviour>();
		foreach (MonoBehaviour c in comps)
		{
			if (c == null)
			{
				continue;
			}
			Type ct = c.GetType();
			if (typeof(BasePart).IsAssignableFrom(ct))
			{
				continue;
			}
			string name = ct.Name;
			if (s_componentDeny.Contains(name))
			{
				continue;
			}
			AddTypeMembers(ct, null, name + ".", list, seen, false);
		}
	}

	private static void AddTypeMembers(Type start, Type stopAt, string prefix, List<Member> list, HashSet<string> seen, bool selfTypeRoot, string selfClass = null)
	{
		Type type = start;
		while (type != null && (stopAt == null || stopAt.IsAssignableFrom(type)))
		{
			bool declaredOnConcreteType = selfTypeRoot && type == start;
			FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
			foreach (FieldInfo f in fields)
			{
				if (f.IsInitOnly || f.IsLiteral)
				{
					continue;
				}
				if (!IsSupported(f.FieldType) || s_excluded.Contains(f.Name) || IsNoisy(f.Name))
				{
					continue;
				}
				Add(list, seen, prefix + f.Name, f.FieldType, declaredOnConcreteType, IsHiddenMember(prefix, f.Name, declaredOnConcreteType, selfClass));
			}
			PropertyInfo[] props = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
			foreach (PropertyInfo p in props)
			{
				if (!p.CanWrite || p.GetIndexParameters().Length != 0)
				{
					continue;
				}
				if (!IsSupported(p.PropertyType) || s_excluded.Contains(p.Name) || IsNoisy(p.Name))
				{
					continue;
				}
				Add(list, seen, prefix + p.Name, p.PropertyType, declaredOnConcreteType, IsHiddenMember(prefix, p.Name, declaredOnConcreteType, selfClass));
			}
			type = type.BaseType;
		}
	}

	// 轮子：m_radius/m_circumference 是由子物体碰撞体“派生”的值，直接改无效 → 隐藏，
	// 改用额外可调项 WheelRadius（会同时缩放视觉与物理）。
	private static readonly HashSet<string> s_wheelClasses = new HashSet<string>
	{
		"CartWheel", "MotorWheel", "OffRoadWheel", "StickyWheel"
	};

	// 默认是否隐藏：类型相关/黑名单 → 隐藏；轮子的派生量 → 隐藏；部件自身类成员 → 相关；
	// 其余只有在“相关白名单”里才默认显示（否则一并隐藏，可用“隐藏项”开关查看）。
	private static bool IsHiddenMember(string prefix, string name, bool selfType, string selfClass)
	{
		if (s_hidden.Contains(name))
		{
			return true;
		}
		if (prefix == null && selfClass != null && s_wheelClasses.Contains(selfClass) && (name == "m_radius" || name == "m_circumference"))
		{
			return true;
		}
		if (selfType)
		{
			return false;
		}
		string full = (prefix ?? string.Empty) + name;
		if (s_relevant.Contains(full))
		{
			return false;
		}
		if (prefix == null && s_relevant.Contains(name))
		{
			return false;
		}
		return true;
	}

	// 颜色展开成 4 个通道；其余一个条目
	private static void Add(List<Member> list, HashSet<string> seen, string key, Type valueType, bool selfType, bool hidden)
	{
		if (string.IsNullOrEmpty(key) || key[0] == '<')
		{
			return;
		}
		if (valueType == typeof(Color))
		{
			string[] channels = { "r", "g", "b", "a" };
			foreach (string ch in channels)
			{
				AddOne(list, seen, key + "@" + ch, false, selfType, hidden);
			}
			return;
		}
		AddOne(list, seen, key, valueType == typeof(bool), selfType, hidden);
	}

	private static void AddOne(List<Member> list, HashSet<string> seen, string key, bool isBool = false, bool selfType = false, bool hidden = false)
	{
		// 按“语义名”去重：m_length 与 Length、m_alpha 与 Alpha 视为同一个
		if (seen.Add(NormalizeKey(key)))
		{
			Member m = new Member();
			m.Key = key;
			m.IsBool = isBool;
			m.SelfType = selfType;
			m.Hidden = hidden;
			list.Add(m);
		}
	}

	// 去重用的规范化键：组件前缀与颜色通道保持不变，仅把成员名去 m_ 前缀并转小写
	private static string NormalizeKey(string key)
	{
		SplitKey(key, out string comp, out string member, out string channel);
		string norm = member;
		if (norm.StartsWith("m_", StringComparison.Ordinal))
		{
			norm = norm.Substring(2);
		}
		norm = norm.ToLowerInvariant();
		return (comp != null ? (comp + ".") : string.Empty) + norm + (channel != null ? ("@" + channel) : string.Empty);
	}

	// 可调类型：数值 / 布尔 / 颜色 / 枚举
	private static bool IsSupported(Type type)
	{
		if (type == typeof(float) || type == typeof(int) || type == typeof(double) || type == typeof(long))
		{
			return true;
		}
		if (type == typeof(bool) || type == typeof(Color))
		{
			return true;
		}
		return type.IsEnum;
	}

	// 噪声名：运行时状态/缓存/索引等，不适合作为可调项
	private static readonly string[] s_noisyParts =
	{
		"timer", "lasttime", "m_time", "prev", "last", "cache", "temp", "index",
		"delta", "progress", "streak", "fps", "accum", "random", "seed", "hash",
		"handle", "ptr", "initialized", "enabled", "hideflags", "isactiveandenabled",
		"useguilayout", "runineditmode", "solveriter", "connectedcomponent",
		"componentcount", "componentindex", "m_cos", "current", "normalized", "ratio",
		"magnitude", "distance", "alpha2"
	};

	private static bool IsNoisy(string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return true;
		}
		string lower = name.ToLowerInvariant();
		foreach (string n in s_noisyParts)
		{
			if (lower.Contains(n))
			{
				return true;
			}
		}
		return false;
	}

	// 与载具相关性从高到低的优先顺序（列在前面的排最前）；其余按“有译名的自身成员 → 自身成员 → 组件成员”再按首字母。
	private static readonly string[] s_priority =
	{
		"m_mass", "m_force", "m_enginePower", "m_powerConsumption", "m_maximumSpeed",
		"CoolingTime", "ForcedCoolingTime", "m_boostForce", "m_ignitionTime",
		"m_boostDuration", "m_boostEndDuration", "m_explosionImpulse", "m_explosionRadius",
		"m_triggerSpeed", "m_stickiness", "m_drag", "m_springStiffness", "m_maximumForce",
		"m_defaultSpeed", "m_maximumRotationSpeed", "m_rotationSpeed", "m_radius",
		"m_speedMin", "m_speedMax", "m_length", "Length", "HalfWidth", "m_halfWidth",
		"m_angle", "Angle", "Sides", "m_sides",
		"m_rewindingTime", "m_ShootTime", "m_shootTime", "m_springStrength", "m_minimumDistance",
		"m_maxHookDistance", "m_maxWindingSpeed", "m_targetDistanceY", "m_targetDeviationX",
		"m_WindingTime", "m_SpringYDrive", "m_SpringYDriveDamper", "m_numberOfBalloons",
		"m_windPowerFactor", "m_electricity", "m_componentSize", "m_interactiveRadius",
		"m_breakVelocity", "m_ZOffset", "m_expressionSetTime", "m_pitch", "m_speedThreshold",
		"Resistance", "Capacitance", "MaxVoltage", "Charge"
	};

	private static int CompareMembers(Member a, Member b)
	{
		int ra = RankOf(a);
		int rb = RankOf(b);
		if (ra != rb)
		{
			return ra - rb;
		}
		return string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase);
	}

	private static int RankOf(Member m)
	{
		if (m.Hidden)
		{
			return 9000; // 隐藏项排最后
		}
		SplitKey(m.Key, out string comp, out string member, out string channel);
		if (m.SelfType)
		{
			return 0; // 该部件自己声明的成员 → 最相关
		}
		for (int i = 0; i < s_priority.Length; i++)
		{
			if (s_priority[i] == member)
			{
				return 1000 + i;
			}
		}
		bool isSelf = comp == null;
		bool hasZh = s_names.ContainsKey(member);
		if (isSelf && hasZh)
		{
			return 5000;
		}
		if (isSelf)
		{
			return 6000;
		}
		if (hasZh)
		{
			return 7000;
		}
		return 8000;
	}

	// ---------- UI 构建辅助 ----------

	private Image CreateImage(Transform parent, string name)
	{
		GameObject go = new GameObject(name, typeof(RectTransform));
		RectTransform rt = go.GetComponent<RectTransform>();
		rt.SetParent(parent, false);
		Image image = go.AddComponent<Image>();
		image.raycastTarget = true;
		return image;
	}

	private Text CreateText(Transform parent, string name, int fontSize, TextAnchor anchor, Color color)
	{
		GameObject go = new GameObject(name, typeof(RectTransform));
		RectTransform rt = go.GetComponent<RectTransform>();
		rt.SetParent(parent, false);
		Text text = go.AddComponent<Text>();
		text.font = m_font;
		text.fontSize = fontSize;
		text.alignment = anchor;
		text.color = color;
		text.horizontalOverflow = HorizontalWrapMode.Overflow;
		text.verticalOverflow = VerticalWrapMode.Overflow;
		text.raycastTarget = false;
		return text;
	}

	private InputField CreateInputField(Transform parent, Vector2 center, Vector2 size)
	{
		GameObject go = new GameObject("Input", typeof(RectTransform));
		RectTransform rt = go.GetComponent<RectTransform>();
		rt.SetParent(parent, false);
		rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
		rt.pivot = new Vector2(0.5f, 0.5f);
		rt.sizeDelta = size;
		rt.anchoredPosition = center;

		Image bg = go.AddComponent<Image>();
		bg.color = new Color(0.09f, 0.10f, 0.09f, 1f);
		INBuildStyle.Bevel(go);
		InputField input = go.AddComponent<InputField>();
		input.targetGraphic = bg;
		input.lineType = InputField.LineType.SingleLine;
		input.contentType = InputField.ContentType.Standard;

		Text text = CreateText(go.transform, "Text", 16, TextAnchor.MiddleLeft, INBuildStyle.Ivory);
		RectTransform trt = text.rectTransform;
		trt.anchorMin = Vector2.zero;
		trt.anchorMax = Vector2.one;
		trt.offsetMin = new Vector2(10f, 4f);
		trt.offsetMax = new Vector2(-10f, -4f);
		text.supportRichText = false;
		input.textComponent = text;

		Text placeholder = CreateText(go.transform, "Placeholder", 20, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.3f));
		RectTransform phrt = placeholder.rectTransform;
		phrt.anchorMin = Vector2.zero;
		phrt.anchorMax = Vector2.one;
		phrt.offsetMin = new Vector2(10f, 4f);
		phrt.offsetMax = new Vector2(-10f, -4f);
		placeholder.text = "0";
		input.placeholder = placeholder;

		return input;
	}

	private UButton CreateButton(Transform parent, string label, int fontSize, Color color)
	{
		GameObject go = new GameObject("Btn_" + label, typeof(RectTransform));
		RectTransform rt = go.GetComponent<RectTransform>();
		rt.SetParent(parent, false);
		Image bg = go.AddComponent<Image>();
		bg.color = label == "应用" ? INBuildStyle.Brass : INBuildStyle.Button;
		UButton button = go.AddComponent<UButton>();
		button.targetGraphic = bg;
		INBuildStyle.ButtonColors(button);
		INBuildStyle.Bevel(go);

		Text text = CreateText(go.transform, "Text", fontSize, TextAnchor.MiddleCenter, Color.white);
		RectTransform trt = text.rectTransform;
		trt.anchorMin = Vector2.zero;
		trt.anchorMax = Vector2.one;
		trt.offsetMin = Vector2.zero;
		trt.offsetMax = Vector2.zero;
		text.text = label;
		return button;
	}
}
