using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using Innovation;
using UnityEngine;

public class BasePart : WPFMonoBehaviour, IBasePart
{
	public enum JointType
	{
		FixedJoint = 0,
		HingeJoint = 1
	}

	public enum PartTier
	{
		Regular = 0,
		Common = 1,
		Rare = 2,
		Epic = 3,
		Legendary = 4
	}

	public enum PartType
	{
		Unknown = 0,
		Balloon = 1,
		Balloons2 = 2,
		Balloons3 = 3,
		Fan = 4,
		WoodenFrame = 5,
		Bellows = 6,
		CartWheel = 7,
		Basket = 8,
		Sandbag = 9,
		Pig = 10,
		Sandbag2 = 11,
		Sandbag3 = 12,
		Propeller = 13,
		Wings = 14,
		Tailplane = 15,
		Engine = 16,
		Rocket = 17,
		MetalFrame = 18,
		SmallWheel = 19,
		MetalWing = 20,
		MetalTail = 21,
		Rotor = 22,
		MotorWheel = 23,
		TNT = 24,
		EngineSmall = 25,
		EngineBig = 26,
		NormalWheel = 27,
		Spring = 28,
		Umbrella = 29,
		Rope = 30,
		CokeBottle = 31,
		KingPig = 32,
		RedRocket = 33,
		SodaBottle = 34,
		PoweredUmbrella = 35,
		Egg = 36,
		JetEngine = 37,
		ObsoleteWheel = 38,
		SpringBoxingGlove = 39,
		StickyWheel = 40,
		GrapplingHook = 41,
		Pumpkin = 42,
		Kicker = 43,
		Gearbox = 44,
		GoldenPig = 45,
		PointLight = 46,
		SpotLight = 47,
		TimeBomb = 48,
		ElectricalPart = 49,
		MechanicalPart = 50,
		RCR2Cargo = 51,
		RC3Missile = 52,
		Exhaust = 53,
		AndroidFrame = 54,
		AndroidTriFrame = 55,
		Accumulator = 56,
		Gyro = 57,
		AndroidThruster = 58,
		Bumper = 59,
		AndroidMotorWheel = 60,
		AndroidGenerator = 61,
		AndroidBigfootWheel = 62,
		AndroidTrack = 63,
		AndroidSki = 64,
		Spoiler = 65,
		MAX = 66
	}

	public enum AutoAlignType
	{
		None = 0,
		Rotate = 1,
		FlipVertically = 2,
		ul = -114514
	}

	public enum Direction
	{
		Right = 0,
		Up = 1,
		Left = 2,
		Down = 3,
		UpRight = 4,
		UpLeft = 5,
		DownLeft = 6,
		DownRight = 7
	}

	public enum GridRotation
	{
		Deg_0 = 0,
		Deg_90 = 1,
		Deg_180 = 2,
		Deg_270 = 3,
		Deg_45 = 4,
		Deg_135 = 5,
		Deg_225 = 6,
		Deg_315 = 7,
		Deg_Max = 8
	}

	public enum JointConnectionType
	{
		None = 0,
		Source = 1,
		Target = 2
	}

	public enum JointConnectionDirection
	{
		Any = 0,
		Right = 1,
		Up = 2,
		Left = 3,
		Down = 4,
		LeftAndRight = 5,
		UpAndDown = 6,
		None = 7
	}

	public enum JointConnectionStrength
	{
		Weak = 0,
		Normal = 1,
		High = 2,
		Extreme = 3,
		HighlyExtreme = 4
	}

	private static float m_lastTimeUsedCollisionParticles;

	protected static int m_groundLayer = -1;

	protected static int m_iceGroundLayer = -1;

	protected bool m_isOnGround;

	[SerializeField]
	private bool m_jointPreprocessing;

	public bool m_eightWay;

	public int m_coordX;

	public int m_coordY;

	[SerializeField, HideInInspector]
	internal bool m_hasBuildCell;

	public float m_mass = 1f;

	public float m_interactiveRadius = 0.5f;

	public float m_breakVelocity;

	public float m_powerConsumption;

	public float m_enginePower;

	public float m_ZOffset;

	public int customPartIndex;

	public bool craftable = true;

	public bool lootCrateReward = true;

	public List<string> tags;

	private float m_lastTimeTouchedGround;

	private bool m_isOnIce;

	private AudioSource m_slidingSound;

	[SerializeField]
	private AudioManager.AudioMaterial audioMaterial;

	public JointType m_jointType;

	public PartTier m_partTier;

	public PartType m_partType;

	public AutoAlignType m_autoAlign;

	public bool m_flipped;

	public GridRotation m_gridRotation;

	public int m_gridXmin;

	public int m_gridXmax;

	public int m_gridYmin;

	public int m_gridYmax;

	public bool m_static;

	public JointConnectionStrength m_jointConnectionStrength;

	public JointConnectionType m_jointConnectionType = JointConnectionType.Target;

	public JointConnectionDirection m_jointConnectionDirection;

	public JointConnectionDirection m_customJointConnectionDirection = JointConnectionDirection.None;

	private Contraption m_contraption;

	private bool m_broken;

	private int m_connectedComponent = -1;

	public BasePart m_enclosedPart;

	public BasePart m_enclosedInto;

	public Sprite m_constructionIconSprite;

	protected bool m_valid;

	protected SpriteManager m_spriteManager;

	private Vector3 m_windVelocity;

	// 每部件实例的自定义参数（仅该部件有效，不写入全局设置）。
	// 额外用**可序列化字符串**存一份：这样 Instantiate 克隆（例如游戏内的“复制器”）也会带上参数。
	// 运行时以 m_customParams 字典为缓存，二者保持同步。
	[SerializeField]
	[HideInInspector]
	private string m_customParamsData;

	[NonSerialized]
	private Dictionary<string, float> m_customParams;

	public IReadOnlyDictionary<string, float> CustomParams
	{
		get { return EnsureParams(); }
	}

	public bool HasCustomParams()
	{
		return EnsureParams().Count > 0;
	}

	public bool HasCustomParam(string key)
	{
		return !string.IsNullOrEmpty(key) && EnsureParams().ContainsKey(key);
	}

	public float GetCustomParam(string key, float fallback = 0f)
	{
		if (INParamSystem.Enabled && !string.IsNullOrEmpty(key) && EnsureParams().TryGetValue(key, out var value))
		{
			return value;
		}
		return fallback;
	}

	/// <summary>
	/// Prefix marking a stored parameter as internal state rather than one of the player's custom numbers.
	/// </summary>
	/// <remarks>
	/// The distinction has to exist somewhere: <see cref="CustomParams"/> is the only per-part storage that
	/// survives a save, a clone and the multiplayer blueprint, so anything that needs to persist per part
	/// has to live there - but the parameter panel turns every entry into an editable row and
	/// <c>INModifiedPartMarker</c> shows a "modified" badge for any part that has one. A key with this
	/// prefix is filtered out of both, so internal state persists without pretending to be a setting.
	/// </remarks>
	public const string InternalParamPrefix = "@";

	/// <summary>True when <paramref name="key"/> is internal state rather than a player-facing parameter.</summary>
	public static bool IsInternalParam(string key)
	{
		return !string.IsNullOrEmpty(key) && key[0] == InternalParamPrefix[0];
	}

	/// <summary>
	/// Reads a stored parameter regardless of whether the custom-value system is switched on.
	/// </summary>
	/// <remarks>
	/// <see cref="GetCustomParam"/> deliberately falls back when <see cref="INParamSystem.Enabled"/> is off,
	/// because that toggle means "ignore the player's custom numbers". Internal state that merely borrows
	/// the same storage must not vanish with that toggle, so it reads through here instead.
	/// </remarks>
	public bool TryGetStoredParam(string key, out float value)
	{
		value = 0f;
		return !string.IsNullOrEmpty(key) && EnsureParams().TryGetValue(key, out value);
	}

	/// <summary>
	/// Writes a stored parameter without treating it as a custom value.
	/// </summary>
	/// <remarks>
	/// <see cref="SetCustomParam"/> runs <c>ApplyCustomParam</c>, which walks the part's members looking for
	/// something to write the value into. Internal state has no member to write - it is read back by
	/// whoever put it there - so it is stored directly and the reflective pass is skipped.
	/// </remarks>
	public void SetStoredParam(string key, float value)
	{
		if (string.IsNullOrEmpty(key)) return;
		EnsureParams()[key] = value;
        if (key == GridlessBuild.OffsetXKey || key == GridlessBuild.OffsetYKey) GridlessBuild.Invalidate(this);
        if (key == GridlessBuild.OutsideKey || key == GridlessBuild.GroupKey || key == BuildMachineTransform.XKey || key == BuildMachineTransform.YKey || key == BuildMachineTransform.AngleKey) GridlessBuild.InvalidateBounds(contraption);
		if (key == FineRotate.AngleKey) FineRotate.Invalidate(this);
		SyncParamsData();
	}

	public bool RemoveStoredParam(string key)
	{
		if (string.IsNullOrEmpty(key) || !EnsureParams().Remove(key)) return false;
        if (key == GridlessBuild.OffsetXKey || key == GridlessBuild.OffsetYKey) GridlessBuild.Invalidate(this);
        if (key == GridlessBuild.OutsideKey || key == GridlessBuild.GroupKey || key == BuildMachineTransform.XKey || key == BuildMachineTransform.YKey || key == BuildMachineTransform.AngleKey) GridlessBuild.InvalidateBounds(contraption);
		if (key == FineRotate.AngleKey) FineRotate.Invalidate(this);
		SyncParamsData();
		return true;
	}

	internal void StoreBuildFrame(Vector2 translation, float angle)
	{
		var map = EnsureParams();
		map[BuildMachineTransform.XKey] = translation.x;
		map[BuildMachineTransform.YKey] = translation.y;
		map[BuildMachineTransform.AngleKey] = angle;
		GridlessBuild.InvalidateBounds(contraption);
		SyncParamsData();
	}

	public void SetCustomParam(string key, float value)
	{
		if (string.IsNullOrEmpty(key))
		{
			return;
		}
		EnsureParams()[key] = value;
		SyncParamsData();
		// 立即写入部件字段，使参数真正生效。
		ApplyCustomParam(key);
	}

	public bool RemoveCustomParam(string key)
	{
		if (string.IsNullOrEmpty(key) || !EnsureParams().Remove(key))
		{
			return false;
		}
		SyncParamsData();
		PartPhysicsParameters.Removed(this, key);
		return true;
	}

	public void ClearCustomParams()
	{
		EnsureParams().Clear();
		SyncParamsData();
		GridlessBuild.Invalidate(this);
		FineRotate.Invalidate(this);
		PartPhysicsParameters.Refresh(this);
		contraption?.RebuildLayerMap();
		if (contraption != null && !contraption.IsRunning) contraption.RefreshPartPose(this);
	}

	// 序列化为 "key:value;key:value"（InvariantCulture；键不含分隔符），空则返回空串。
	public string SerializeCustomParams()
	{
		if (m_customParams == null)
		{
			return string.IsNullOrEmpty(m_customParamsData) ? string.Empty : m_customParamsData;
		}
		return BuildParamsString(m_customParams);
	}

	public void DeserializeCustomParams(string text)
	{
		m_customParamsData = string.IsNullOrEmpty(text) ? null : text;
		m_customParams = null;
		GridlessBuild.Invalidate(this);
		FineRotate.Invalidate(this);
		// 解析 + 应用（可能含组件成员/属性，需在部件/组件就绪后再调用一次 ApplyCustomParams）
		ApplyCustomParams();
		contraption?.RebuildLayerMap();
		if (contraption != null && !contraption.IsRunning) contraption.RefreshPartPose(this);
	}

	private Dictionary<string, float> EnsureParams()
	{
		if (m_customParams == null)
		{
			m_customParams = new Dictionary<string, float>();
			if (!string.IsNullOrEmpty(m_customParamsData))
			{
				ParseParams(m_customParamsData, m_customParams);
			}
		}
		return m_customParams;
	}

	private void SyncParamsData()
	{
		string s = BuildParamsString(m_customParams);
		m_customParamsData = string.IsNullOrEmpty(s) ? null : s;
	}

	private static string BuildParamsString(Dictionary<string, float> map)
	{
		if (map == null || map.Count == 0)
		{
			return string.Empty;
		}
		StringBuilder builder = new StringBuilder();
		foreach (KeyValuePair<string, float> pair in map)
		{
			if (string.IsNullOrEmpty(pair.Key))
			{
				continue;
			}
			if (builder.Length > 0)
			{
				builder.Append(';');
			}
			builder.Append(pair.Key);
			builder.Append(':');
			builder.Append(pair.Value.ToString(CultureInfo.InvariantCulture));
		}
		return builder.ToString();
	}

	private static void ParseParams(string text, Dictionary<string, float> into)
	{
		if (string.IsNullOrEmpty(text) || into == null)
		{
			return;
		}
		string[] entries = text.Split(';');
		foreach (string entry in entries)
		{
			if (string.IsNullOrEmpty(entry))
			{
				continue;
			}
			int sep = entry.IndexOf(':');
			if (sep <= 0 || sep >= entry.Length - 1)
			{
				continue;
			}
			string key = entry.Substring(0, sep);
			string raw = entry.Substring(sep + 1);
			if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
			{
				into[key] = value;
			}
		}
	}

	// 把某个自定义参数写入目标（部件自身字段/属性，或部件上的组件成员 “组件类型.成员”）——真正生效。
	public void ApplyCustomParam(string key)
	{
		if (!INParamSystem.Enabled)
		{
			return;
		}
		if (string.IsNullOrEmpty(key))
		{
			return;
		}
		float value;
		if (!EnsureParams().TryGetValue(key, out value))
		{
			return;
		}
		int dot = key.IndexOf('.');
		bool applied;
		if (dot > 0 && dot < key.Length - 1)
		{
			// 组件成员：<组件类型名>.<成员名>
			string compType = key.Substring(0, dot);
			string member = key.Substring(dot + 1);
			MonoBehaviour comp = FindComponentByTypeName(compType);
			applied = comp != null && SetNumericMember(comp, member, value, null);
		}
		else
		{
			applied = SetNumericMember(this, key, value, typeof(BasePart));
		}
		if (!applied)
		{
			// 无法用字段/属性表达的值（如电阻/电容），交给子类的“额外可调项”
			ApplyExtraParam(key, value);
		}
		SyncCachedValues(key, value);
		if (GetComponent<PartPhysicsParameters>() != null) PartPhysicsParameters.Refresh(this);
	}

	// 额外可调项：无法用字段/属性表达的值（如电阻、电容）。子类重写以声明键（键会出现在长按面板里）。
	// 这些键通常由部件自身在创建时用 GetCustomParam(key, 默认值) 读取，因此 ApplyExtraParam 可不做处理。
	public virtual void CollectExtraParams(List<string> keys)
	{
		// 每部件缩放（EasyScale 式）对所有部件可用，所以在基类声明；值由 PartScaleKey 存储，
		// 由 ApplyExtraParam 立即生效。
		keys.Add(PartScaleKey);
		keys.Add(PartScaleXKey);
		keys.Add(PartScaleYKey);
		PartPhysicsParameters.Collect(this, keys);
	}

	/// <summary>每部件缩放（EasyScale 式）的参数键。</summary>
	public const string PartScaleKey = "PartScale";
	public const string PartScaleXKey = "PartScaleX";
	public const string PartScaleYKey = "PartScaleY";

	public static float ValidScale(float value)
	{
		return float.IsNaN(value) || float.IsInfinity(value) || value <= 0f ? 1f : Mathf.Clamp(value, 0.05f, 20f);
	}

	public Vector3 PartScaleFactors => new Vector3(
		ScaleMultiplier * ValidScale(GetCustomParam(PartScaleXKey, 1f)),
		ScaleMultiplier * ValidScale(GetCustomParam(PartScaleYKey, 1f)), ScaleMultiplier);

	public static bool IsScaleParam(string key) => key == PartScaleKey || key == PartScaleXKey || key == PartScaleYKey;

	/// <summary>缩放的原始尺寸缓存，用于把倍数乘在原始值上而不是上一次的结果上。</summary>
	[SerializeField, HideInInspector]
	private Vector3 m_originalScale = Vector3.zero;

	/// <summary>
	/// 缩放的真相在 <c>transform.localScale</c>，这里只是读取它对应的倍数（与 EasyScale 的做法一致：
	/// 面板上的数字是镜像读数，未装本功能的客户端仍按真实尺寸渲染）。
	/// </summary>
	/// <remarks>
	/// 只读属性刻意不叫 <see cref="PartScaleKey"/>：长按面板会先按键名反射找成员，再回落到
	/// <c>ApplyExtraParam</c>；同名属性会让它找到这个只读属性而写入失败。命名分离开，反射就
	/// 直接落到 <c>ApplyExtraParam</c>。
	/// </remarks>
	public float ScaleMultiplier
	{
		get
		{
			float value = GetCustomParam(PartScaleKey, 1f);
			return ValidScale(value);
		}
	}

	/// <summary>
	/// True when the part writes its own <c>transform.localScale</c>.
	/// </summary>
	/// <remarks>
	/// The wheel family does: it derives the root scale from a wheel radius so the rolling circle matches
	/// (<c>ApplyWheelRadius</c>). A general scale must not fight that write, so those parts compose
	/// <see cref="ScaleMultiplier"/> into their own math instead of being scaled here - which is also why
	/// this is a virtual rather than a type test.
	/// </remarks>
	public virtual bool OwnsOwnScale => false;
	public virtual Vector3 BuildFootprintScale => PartScaleFactors;
	protected Vector3 RelativeBuildScale(Vector3 original)
	{
		return new Vector3(original.x != 0f ? transform.localScale.x / original.x : 1f,
			original.y != 0f ? transform.localScale.y / original.y : 1f, 1f);
	}

	/// <summary>Applies the per-part scale to the root transform. No-op for parts that own their scale.</summary>
	public virtual void ApplyPartScale()
	{
		GridlessBuild.InvalidateBounds(contraption);
		if (OwnsOwnScale) return;
		if (m_originalScale == Vector3.zero)
		{
			m_originalScale = base.transform.localScale;
		}
		Vector3 scale = PartScaleFactors;
		// Colliders and the 3D chain under the part follow automatically - Unity propagates a parent's
		// localScale. That is the same mechanism Besiege relies on for its blocks (BlockBehaviour.SetScale
		// only writes the root and refreshes the body). The grid footprint
		// (m_gridXmin..m_gridYmax) is deliberately left alone: it is baked per prefab and every consumer
		// of it (clear-cell, hit-test, modified marker) assumes the authored size.
		base.transform.localScale = Vector3.Scale(m_originalScale, scale);
	}

	// 应用额外可调项；返回是否已处理（默认不处理）。
	public virtual bool ApplyExtraParam(string key, float value)
	{
		if (PartPhysicsParameters.Apply(this, key, value)) return true;
		if (IsScaleParam(key))
		{
			ApplyPartScale();
			return true;
		}
		return false;
	}

	// 读取额外可调项的“当前值/默认值”（长按面板用它显示，避免显示 0）；返回是否已处理。
	public virtual bool TryGetExtraParamValue(string key, out float value)
	{
		if (PartPhysicsParameters.TryRead(this, key, out value)) return true;
		if (IsScaleParam(key))
		{
			value = key == PartScaleKey ? ScaleMultiplier : ValidScale(GetCustomParam(key, 1f));
			return true;
		}
		value = 0f;
		return false;
	}

	// 将所有自定义参数写入目标（字段/属性/组件成员）。
	public void ApplyCustomParams()
	{
		PartPhysicsParameters.Prepare(this);
		Dictionary<string, float> map = EnsureParams();
		if (map.Count == 0)
		{
			PartPhysicsParameters.Refresh(this);
			return;
		}
		List<string> keys = new List<string>(map.Keys);
		foreach (string key in keys)
		{
			ApplyCustomParam(key);
		}
		PartPhysicsParameters.Refresh(this);
	}

	// 复制另一部件实例的自定义参数（用于克隆 / 拖动 / 粘贴保留参数）。
	public void CopyCustomParamsFrom(BasePart other)
	{
		if (other == null)
		{
			return;
		}
		Dictionary<string, float> source = other.EnsureParams();
		if (source.Count == 0)
		{
			return;
		}
		foreach (KeyValuePair<string, float> pair in source)
		{
			SetCustomParam(pair.Key, pair.Value);
		}
	}

	private MonoBehaviour FindComponentByTypeName(string typeName)
	{
		MonoBehaviour[] comps = GetComponentsInChildren<MonoBehaviour>(true);
		foreach (MonoBehaviour c in comps)
		{
			if (c == null) continue;
			for (Type type = c.GetType(); type != null && typeof(MonoBehaviour).IsAssignableFrom(type); type = type.BaseType)
				if (type.Name == typeName) return c;
		}
		return null;
	}

	// 在 target 上按成员名写入数值；stopAt 非空时只在该类型（含）以内的继承链查找。
	// 成员名可带通道后缀 “@r/@g/@b/@a”（用于 Color）；bool→(value!=0)；枚举→四舍五入取整。
	private static bool SetNumericMember(object target, string member, float value, Type stopAt)
	{
		if (target == null || string.IsNullOrEmpty(member))
		{
			return false;
		}
		string channel = null;
		int at = member.IndexOf('@');
		if (at > 0 && at < member.Length - 1)
		{
			channel = member.Substring(at + 1);
			member = member.Substring(0, at);
		}
		Type type = target.GetType();
		while (type != null && (stopAt == null || stopAt.IsAssignableFrom(type)))
		{
			FieldInfo field = type.GetField(member, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
			if (field != null && !field.IsInitOnly && !field.IsLiteral)
			{
				try
				{
					field.SetValue(target, ConvertValue(value, field.FieldType, field.GetValue(target), channel));
					return true;
				}
				catch
				{
					return false;
				}
			}
			PropertyInfo prop = type.GetProperty(member, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
			if (prop != null && prop.CanWrite && prop.GetIndexParameters().Length == 0)
			{
				try
				{
					prop.SetValue(target, ConvertValue(value, prop.PropertyType, prop.GetValue(target, null), channel), null);
					return true;
				}
				catch
				{
					return false;
				}
			}
			type = type.BaseType;
		}
		return false;
	}

	private static object ConvertValue(float value, Type type, object current, string channel)
	{
		if (type == typeof(bool))
		{
			return value != 0f;
		}
		if (type == typeof(Color))
		{
			Color color = (current is Color) ? (Color)current : Color.white;
			switch (channel)
			{
				case "r":
					color.r = value;
					break;
				case "g":
					color.g = value;
					break;
				case "b":
					color.b = value;
					break;
				case "a":
					color.a = value;
					break;
				default:
					color.r = value;
					color.g = value;
					color.b = value;
					break;
			}
			return color;
		}
		if (type.IsEnum)
		{
			try
			{
				return Enum.ToObject(type, Mathf.RoundToInt(value));
			}
			catch
			{
				return current;
			}
		}
		if (type == typeof(int))
		{
			return Mathf.RoundToInt(value);
		}
		if (type == typeof(double))
		{
			return (double)value;
		}
		if (type == typeof(long))
		{
			return (long)Mathf.RoundToInt(value);
		}
		return value;
	}

	// 某些字段被缓存到别处，写入字段后需要同步。
	private void SyncCachedValues(string key, float value)
	{
		if (key == "m_mass" && base.rigidbody != null)
		{
			base.rigidbody.mass = value;
		}
	}

	public bool VisibleOnPartListBeforeUnlocking
	{
		get
		{
			if (!CustomizationManager.IsPartUnlocked(this))
			{
				if (craftable)
				{
					return lootCrateReward;
				}
				return false;
			}
			return true;
		}
	}

	public bool JointPreprocessing => m_jointPreprocessing;

	public virtual Vector3 Position => base.transform.position;

	public AudioManager.AudioMaterial AudioMaterial => audioMaterial;

	public Contraption contraption
	{
		get
		{
			return m_contraption;
		}
		set
		{
			m_contraption = value;
		}
	}

	public int ConnectedComponent
	{
		get
		{
			return m_connectedComponent;
		}
		set
		{
			m_connectedComponent = value;
		}
	}

	public BasePart enclosedPart
	{
		get
		{
			return m_enclosedPart;
		}
		set
		{
			m_enclosedPart = value;
			if ((bool)value)
			{
				value.enclosedInto = this;
			}
		}
	}

	public BasePart enclosedInto
	{
		get
		{
			return m_enclosedInto;
		}
		set
		{
			m_enclosedInto = value;
		}
	}

	public Vector3 WindVelocity
	{
		get
		{
			return m_windVelocity;
		}
		set
		{
			m_windVelocity = value;
		}
	}

	public bool valid
	{
		get
		{
			return m_valid;
		}
		set
		{
			m_valid = value;
		}
	}

	public PartType Type
	{
		get
		{
			return m_partType;
		}
		set
		{
			m_partType = value;
		}
	}

	public PartTier Tier
	{
		get
		{
			return m_partTier;
		}
		set
		{
			m_partTier = value;
		}
	}

	public int Index
	{
		get
		{
			return customPartIndex;
		}
		set
		{
			customPartIndex = value;
		}
	}

	public PartTypeInfo TypeInfo
	{
		get
		{
			return new PartTypeInfo(m_partType, customPartIndex);
		}
		set
		{
			(m_partType, customPartIndex) = value;
		}
	}

	public int CoordX
	{
		get
		{
			return m_coordX;
		}
		set
		{
			m_coordX = value;
		}
	}

	public int CoordY
	{
		get
		{
			return m_coordY;
		}
		set
		{
			m_coordY = value;
		}
	}

	public GridRotation Rotation
	{
		get
		{
			return m_gridRotation;
		}
		set
		{
			m_gridRotation = value;
		}
	}

	public bool Flipped
	{
		get
		{
			return m_flipped;
		}
		set
		{
			m_flipped = value;
		}
	}

	public float PowerConsumption
	{
		get
		{
			return m_powerConsumption;
		}
		set
		{
			m_powerConsumption = value;
		}
	}

	public float EnginePower
	{
		get
		{
			return m_enginePower;
		}
		set
		{
			m_enginePower = value;
		}
	}

	MonoBehaviour IBasePart.UnityObject => this;

	PartTypeCode IBasePart.PartType => (PartTypeCode)m_partType.ToSortedPartType();

	PartTierCode IBasePart.PartTier => (PartTierCode)m_partTier;

	int IBasePart.PartIndex => customPartIndex;

	int IBasePart.GridRotation => (int)m_gridRotation;

	IBasePart IBasePart.EnclosedPart => m_enclosedPart;

	IBasePart IBasePart.EnclosedInto => m_enclosedInto;

	public int StrictConnectedComponent { get; set; }

	public int GeneralConnectedComponent { get; set; }

	public int GeneratorRefCount { get; set; }

	public int GenerationLevel { get; set; }

	public int GenerationIndex { get; set; }

	public float Temperature { get; set; }

	public BasePart EnclosedPart => m_enclosedPart;

	public BasePart EnclosedInto => m_enclosedInto;

	public bool HasGeneratorRef
	{
		get
		{
			if (INSettings.GetBool(INFeature.PartGenerator))
			{
				return GeneratorRefCount > 0;
			}
			return false;
		}
	}

	public static PartTypeInfo WoodenBox => new PartTypeInfo(PartType.WoodenFrame, 10);

	public static PartTypeInfo SeparatedFrame => new PartTypeInfo(PartType.MetalFrame, 8);

	public static PartTypeInfo LightFrame => new PartTypeInfo(PartType.MetalFrame, 10);

	public static PartTypeInfo AlienMetalFrame => new PartTypeInfo(PartType.MetalFrame, 11);

	public static PartRangeInfo ColoredFrames => new PartRangeInfo(PartType.MetalFrame, 12, 129);

	public static PartTypeInfo MetalBox => new PartTypeInfo(PartType.MetalFrame, 130);

	public static PartTypeInfo BracketFrame => new PartTypeInfo(PartType.MetalFrame, 131);

	public static PartRangeInfo TransparentFrames => new PartRangeInfo(PartType.MetalFrame, 132, 133);

	public static PartTypeInfo OffRoadWheel => new PartTypeInfo(PartType.MotorWheel, 7);

	public static PartTypeInfo AvoidanceRocketA => new PartTypeInfo(PartType.Rocket, 1);

	public static PartTypeInfo AvoidanceRocketB => new PartTypeInfo(PartType.Rocket, 3);

	public static PartTypeInfo TrackingRocketA => new PartTypeInfo(PartType.RedRocket, 1);

	public static PartTypeInfo TrackingRocketB => new PartTypeInfo(PartType.RedRocket, 3);

	public static PartTypeInfo BlasterTNT => new PartTypeInfo(PartType.TNT, 6);

	public static PartRangeInfo HingePlates => new PartRangeInfo(PartType.Rope, 4, 7);

	public static PartTypeInfo AutoGun => new PartTypeInfo(PartType.GrapplingHook, 6);

	public static PartRangeInfo MultipartGenerators => new PartRangeInfo(PartType.GrapplingHook, 8, 10);

	public static PartTypeInfo AutoConnector => new PartTypeInfo(PartType.Kicker, 1);

	public static PartTypeInfo ElasticConnectorA => new PartTypeInfo(PartType.Kicker, 2);

	public static PartTypeInfo Marker => new PartTypeInfo(PartType.Kicker, 3);

	public static PartTypeInfo ElasticConnectorB => new PartTypeInfo(PartType.Kicker, 4);

	public static PartRangeInfo EntityLightsA => new PartRangeInfo(PartType.PointLight, 0, 4);

	public static PartTypeInfo DecelerationLight => new PartTypeInfo(PartType.PointLight, 5);

	public static PartTypeInfo AutoControlLight => new PartTypeInfo(PartType.PointLight, 6);

	public static PartRangeInfo EntityLightsB => new PartRangeInfo(PartType.SpotLight, 0, 3);

	// 军事版 24.7.12 用到、主工程原先只在内联的 customPartIndex 判断里出现的编号。
	// 编号与主工程是同一套（见 docs/MilitaryPartsDiff.md 第 2.3 节），行为由“军事版功能”开关控制。
	public static PartTypeInfo HeavyBomb => new PartTypeInfo(PartType.TNT, 2);

	public static PartTypeInfo NuclearBomb => new PartTypeInfo(PartType.TNT, 3);

	public static PartTypeInfo AcidBomb => new PartTypeInfo(PartType.TNT, 4);

	public static PartTypeInfo AlienBomb => new PartTypeInfo(PartType.TNT, 5);

	public static PartTypeInfo APRocket => new PartTypeInfo(PartType.Rocket, 2);

	public virtual JointConnectionStrength GetJointConnectionStrength()
	{
		return m_jointConnectionStrength;
	}

	/// <summary>
	/// True for the parts BPLE drives with its engine gadget button (the in-flight
	/// start/stop-motor button): the imported RC1 accumulator/gyro keep the Engine driver but
	/// got their own part types in the RC1 class split.
	/// UIPartButtonList.GetBasePartType normalises a part's button to PartType.Engine for all
	/// of these, so a button built from an imported accumulator still carries the engine
	/// button type (and its sprite) and still reaches Contraption.ActivateAllPoweredParts.
	/// MAIN does the same (BasePart.IsEngineButtonType).
	/// </summary>
	public static bool IsEngineButtonType(PartType type)
	{
		return type == PartType.Accumulator || type == PartType.Gyro;
	}

	/// <summary>
	/// True for the imported RC1 wheel classes that keep the stock motor-wheel behaviour. They
	/// are built on MAIN's MotorWheel / OffRoadWheel base (see Tools/RC1/RC1_CATALOG_NOTES.md)
	/// and, like every ported powered part, they are switchable
	/// (RoverPortPoweredPart.CanBeEnabled/HasOnOffToggle), so they produce a trigger button.
	/// UIPartButtonList.GetBasePartType normalises that button to PartType.MotorWheel, which is
	/// the button they would have had while they shared the stock type; without it the button
	/// is built with no sprite at all (SetButtonSprite(enabled: false, ...)).
	///
	/// This is the one family MAIN's own BasePart.IsEngineButtonType does not cover - MAIN's
	/// Android wheels are still MotorWheel *components*, and MAIN simply never normalised the
	/// type it split them into.
	/// </summary>
	public static bool IsMotorWheelButtonType(PartType type)
	{
		return type == PartType.AndroidMotorWheel || type == PartType.AndroidGenerator
			|| type == PartType.AndroidBigfootWheel || type == PartType.AndroidTrack;
	}

	/// <summary>
	/// True for every part that behaves like a frame the way the stock frames do: the wooden
	/// frame, the metal frame and the two imported RC1 frame classes
	/// (AndroidFrame/AndroidTriFrame - the 13 RC1 classes are listed in
	/// Tools/RC1/RC1_CATALOG_NOTES.md).
	///
	/// The RC1 frames are frames in every sense the game cares about (the same base prefab,
	/// the same joint contract), so every system that asks "is this a frame?" has to answer
	/// yes for them - otherwise the alien super glue overlay, the auto-control lights, the
	/// hinge plate and the pig-to-frame welding would silently stop seeing them.
	/// Cases that are about a *stock* frame index or a feature of the stock frames (colored
	/// frames, metal boxes, the colored frame tint, snap sprites, customPartIndex ranges)
	/// deliberately stay keyed on MetalFrame/WoodenFrame alone.
	/// </summary>
	public static bool IsFramePart(PartType type)
	{
		return type == PartType.WoodenFrame || type == PartType.MetalFrame
			|| type == PartType.AndroidFrame || type == PartType.AndroidTriFrame;
	}

	/// <summary>
	/// The same set minus the wooden frame: the frames treated as metal-framed. The RC1
	/// Android frames are built from the metal frame base prefab and share its material and
	/// joint setup.
	/// </summary>
	public static bool IsMetalFramePart(PartType type)
	{
		return type == PartType.MetalFrame || type == PartType.AndroidFrame
			|| type == PartType.AndroidTriFrame;
	}

	public static PartType BaseType(PartType type)
	{
		switch (type)
		{
		case PartType.Balloons2:
		case PartType.Balloons3:
			return PartType.Balloon;
		case PartType.Sandbag2:
		case PartType.Sandbag3:
			return PartType.Sandbag;
		default:
			return type;
		}
	}

	/// <summary>
	/// The stock part type whose *driver* a part of this type actually is - the type whose gadget
	/// button, flight-menu row and per-type part bookkeeping the part belongs to.
	///
	/// The 13 imported RC1 (Android) parts were given their own <see cref="PartType"/> and
	/// SortedPartType entries but kept the stock driver they were grafted onto: an
	/// <c>AndroidThruster</c> is a <c>JetEngine</c>, <c>Accumulator</c>/<c>Gyro</c> are
	/// <c>Engine</c>s, and the four RC1 wheel classes are <c>MotorWheel</c>s (see
	/// Tools/RC1/RC1_CATALOG_NOTES.md). The UI already normalises them this way -
	/// <c>UIPartButtonList.GetBasePartType</c> for the button sprite and
	/// <c>InGameFlightMenu.CombinedTypeForGadgetButtonOrdering</c> for the row order - and both of
	/// those now call this method, so there is exactly one mapping.
	///
	/// Every path that *matches a part against a button* has to use it too. The in-flight gadget
	/// button for a jet is built for <c>JetEngine</c> (InGameFlightMenu.
	/// CreateButtonWithSprite), so without this normalisation the button asks
	/// <c>HasActiveParts(JetEngine, Right)</c>, the RC1 thruster answered to
	/// <c>AndroidThruster</c>, and the button stayed permanently disabled *and* dark: the part
	/// could not be switched on at all, which also kept its flame off
	/// (RoverPortPoweredPart.ShouldEmitParticles polls m_enabled).
	/// </summary>
	public static PartType DriverType(PartType type)
	{
		PartType partType = BaseType(type);
		if (IsEngineButtonType(partType) || partType == PartType.EngineSmall || partType == PartType.EngineBig)
		{
			return PartType.Engine;
		}
		if (partType == PartType.AndroidThruster)
		{
			return PartType.JetEngine;
		}
		if (IsMotorWheelButtonType(partType))
		{
			return PartType.MotorWheel;
		}
		return partType;
	}

	public virtual void Awake()
	{
		ConnectedComponent = -1;
		StrictConnectedComponent = -1;
		GeneralConnectedComponent = -1;
		m_valid = true;
		m_spriteManager = GetComponent<SpriteManager>();
		m_groundLayer = LayerMask.NameToLayer("Ground");
		m_iceGroundLayer = LayerMask.NameToLayer("IceGround");
		// 若该实例自带自定义参数（例如被“复制器”克隆而来），这里先应用一次（字段/属性）；
		// 组件成员（如 EntityLight）在放置后会再补一次。
		ApplyCustomParams();
		if (!string.IsNullOrEmpty(m_customParamsData))
		{
			Debug.Log("[PARAMDIAG] Awake data='" + m_customParamsData + "' on " + base.name);
		}
	}

	public virtual void Initialize()
	{
	}

	public virtual void InitializeEngine()
	{
	}

	public virtual bool CanBeEnabled()
	{
		return false;
	}

	public virtual bool HasOnOffToggle()
	{
		return false;
	}

	public virtual bool IsEnabled()
	{
		return false;
	}

	public virtual void SetEnabled(bool enabled)
	{
	}

	public virtual void PlayRemovalEffect()
	{
	}

	protected bool IsMultiplayerReplica()
	{
		if (rigidbody != null)
		{
			return rigidbody.isKinematic;
		}
		return false;
	}

	public virtual bool IsPowered()
	{
		return m_powerConsumption > 0f;
	}

	public bool IsEngine()
	{
		return m_enginePower > 0f;
	}

	public virtual Direction EffectDirection()
	{
		return Direction.Right;
	}

	public virtual GridRotation AutoAlignRotation(JointConnectionDirection target)
	{
		if (m_jointConnectionDirection == JointConnectionDirection.None)
		{
			return RotationTo(m_customJointConnectionDirection, target);
		}
		return RotationTo(m_jointConnectionDirection, target);
	}

	public bool HasTag(string tag)
	{
		if (tags != null)
		{
			return tags.Contains(tag);
		}
		return false;
	}

	public bool IsFlipped()
	{
		return m_flipped;
	}

	public virtual void SetFlipped(bool flipped)
	{
		m_flipped = flipped;
		if (m_flipped)
		{
			base.transform.localRotation = Quaternion.AngleAxis(180f, Vector3.up);
		}
		else
		{
			base.transform.localRotation = Quaternion.identity;
		}
		OnFlipped();
	}

	public static Direction ConvertDirection(JointConnectionDirection direction)
	{
		return (Direction)(direction - 1);
	}

	public static bool IsDirection(JointConnectionDirection direction)
	{
		if (direction != JointConnectionDirection.Left && direction != JointConnectionDirection.Right && direction != JointConnectionDirection.Up)
		{
			return direction == JointConnectionDirection.Down;
		}
		return true;
	}

	public JointConnectionDirection GetJointConnectionDirection()
	{
		if (INSettings.GetBool(INFeature.NewTimeBombConnection) && m_partType == PartType.TimeBomb)
		{
			return GlobalJointConnectionDirection((!(m_enclosedInto == null)) ? JointConnectionDirection.UpAndDown : JointConnectionDirection.Any);
		}
		return GlobalJointConnectionDirection(m_jointConnectionDirection);
	}

	public JointConnectionDirection GetCustomJointConnectionDirection()
	{
		return GlobalJointConnectionDirection(m_customJointConnectionDirection);
	}

	public JointConnectionDirection GlobalJointConnectionDirection(JointConnectionDirection localDirection)
	{
		if (localDirection == JointConnectionDirection.Any || localDirection == JointConnectionDirection.None)
		{
			return localDirection;
		}
		JointConnectionDirection jointConnectionDirection = localDirection;
		switch (localDirection)
		{
		case JointConnectionDirection.LeftAndRight:
			if (m_gridRotation == GridRotation.Deg_90 || m_gridRotation == GridRotation.Deg_270)
			{
				jointConnectionDirection = JointConnectionDirection.UpAndDown;
			}
			break;
		case JointConnectionDirection.UpAndDown:
			if (m_gridRotation == GridRotation.Deg_90 || m_gridRotation == GridRotation.Deg_270)
			{
				jointConnectionDirection = JointConnectionDirection.LeftAndRight;
			}
			break;
		default:
			jointConnectionDirection = (JointConnectionDirection)(((int)(localDirection - 1) + (int)m_gridRotation) % 4 + 1);
			break;
		}
		if (m_flipped)
		{
			switch (jointConnectionDirection)
			{
			case JointConnectionDirection.Left:
				return JointConnectionDirection.Right;
			case JointConnectionDirection.Right:
				return JointConnectionDirection.Left;
			case JointConnectionDirection.Up:
				return JointConnectionDirection.Down;
			case JointConnectionDirection.Down:
				return JointConnectionDirection.Up;
			}
		}
		return jointConnectionDirection;
	}

	public static Direction InverseDirection(Direction direction)
	{
		return direction switch
		{
			Direction.Right => Direction.Left, 
			Direction.Up => Direction.Down, 
			Direction.Left => Direction.Right, 
			Direction.Down => Direction.Up, 
			_ => Direction.Right, 
		};
	}

	public bool CanConnectTo(Direction direction)
	{
		switch (GetJointConnectionDirection())
		{
		case JointConnectionDirection.Any:
			return true;
		case JointConnectionDirection.Right:
			return direction == Direction.Right;
		case JointConnectionDirection.Up:
			return direction == Direction.Up;
		case JointConnectionDirection.Left:
			return direction == Direction.Left;
		case JointConnectionDirection.Down:
			return direction == Direction.Down;
		case JointConnectionDirection.LeftAndRight:
			if (direction != Direction.Left)
			{
				return direction == Direction.Right;
			}
			return true;
		case JointConnectionDirection.UpAndDown:
			if (direction != Direction.Up)
			{
				return direction == Direction.Down;
			}
			return true;
		case JointConnectionDirection.None:
			return false;
		default:
			return false;
		}
	}

	public bool CanCustomConnectTo(Direction direction)
	{
		switch (GetCustomJointConnectionDirection())
		{
		case JointConnectionDirection.Any:
			return true;
		case JointConnectionDirection.Right:
			return direction == Direction.Right;
		case JointConnectionDirection.Up:
			return direction == Direction.Up;
		case JointConnectionDirection.Left:
			return direction == Direction.Left;
		case JointConnectionDirection.Down:
			return direction == Direction.Down;
		case JointConnectionDirection.LeftAndRight:
			if (direction != Direction.Left)
			{
				return direction == Direction.Right;
			}
			return true;
		case JointConnectionDirection.UpAndDown:
			if (direction != Direction.Up)
			{
				return direction == Direction.Down;
			}
			return true;
		case JointConnectionDirection.None:
			return false;
		default:
			return false;
		}
	}

	public static Direction Rotate(Direction direction, GridRotation rotation)
	{
		return (Direction)(((int)direction + (int)rotation) % 4);
	}

	public static Direction RotateWithEightDirections(Direction direction, GridRotation rotation)
	{
		return (Direction)(((int)direction + (int)rotation) % 8);
	}

	public static GridRotation RotationTo(JointConnectionDirection source, JointConnectionDirection target)
	{
		if (source == JointConnectionDirection.Any || target == JointConnectionDirection.Any || source == JointConnectionDirection.None || target == JointConnectionDirection.None)
		{
			return GridRotation.Deg_0;
		}
		switch (source)
		{
		case JointConnectionDirection.LeftAndRight:
			if (target == JointConnectionDirection.UpAndDown || target == JointConnectionDirection.Up || target == JointConnectionDirection.Down)
			{
				return GridRotation.Deg_90;
			}
			return GridRotation.Deg_0;
		default:
			return (GridRotation)((target - source + 4) % 4);
		case JointConnectionDirection.UpAndDown:
			if (target == JointConnectionDirection.LeftAndRight || target == JointConnectionDirection.Left || target == JointConnectionDirection.Right)
			{
				return GridRotation.Deg_90;
			}
			return GridRotation.Deg_0;
		}
	}

	public float GetRotationAngle(GridRotation rotation)
	{
		return rotation switch
		{
			GridRotation.Deg_0 => 0f, 
			GridRotation.Deg_90 => 90f, 
			GridRotation.Deg_180 => 180f, 
			GridRotation.Deg_270 => 270f, 
			GridRotation.Deg_45 => 45f, 
			GridRotation.Deg_135 => 135f, 
			GridRotation.Deg_225 => 225f, 
			GridRotation.Deg_315 => 315f, 
			_ => 0f, 
		};
	}

	public virtual void SetRotation(GridRotation rotation)
	{
		FineRotate.OnDiscreteRotation(this, m_gridRotation, rotation);
		m_gridRotation = rotation;
		// The one place the part root's rotation is applied, so 部件精细旋转 hooks in here rather than in
		// every caller: when the part carries a fine angle it is drawn at that instead of the grid angle,
		// and otherwise this is the stock line unchanged.
		ApplyBuildRotation(Quaternion.AngleAxis(GetRotationAngle(rotation), Vector3.forward));
	}

	protected void ApplyBuildRotation(Quaternion localRotation, bool animate = true)
	{
		GridlessBuild.InvalidateBounds(contraption);
		FineRotate.OnAppliedRotation(this);
		float nativeAngle = GetRotationAngle(m_gridRotation);
		float extraAngle = FineRotate.GetAngle(this, nativeAngle) - nativeAngle;
		Quaternion target = Quaternion.AngleAxis(BuildMachineTransform.Read(this).angle + extraAngle + PartPhysicsParameters.Rotation(this), Vector3.forward) * localRotation;
		if (animate) PartRotationAnimator.Apply(this, target);
		else PartRotationAnimator.Snap(transform, target);
	}

	public void RotateClockwise()
	{
		if (m_autoAlign == AutoAlignType.ul)
		{
			m_eightWay = true;
		}
		switch (m_gridRotation)
		{
		case GridRotation.Deg_0:
			if (!m_eightWay)
			{
				SetRotation(GridRotation.Deg_270);
			}
			else
			{
				SetRotation(GridRotation.Deg_315);
			}
			break;
		case GridRotation.Deg_90:
			if (!m_eightWay)
			{
				SetRotation(GridRotation.Deg_0);
			}
			else
			{
				SetRotation(GridRotation.Deg_45);
			}
			break;
		case GridRotation.Deg_180:
			if (!m_eightWay)
			{
				SetRotation(GridRotation.Deg_90);
			}
			else
			{
				SetRotation(GridRotation.Deg_135);
			}
			break;
		case GridRotation.Deg_270:
			if (!m_eightWay)
			{
				SetRotation(GridRotation.Deg_180);
			}
			else
			{
				SetRotation(GridRotation.Deg_225);
			}
			break;
		case GridRotation.Deg_45:
			SetRotation(GridRotation.Deg_0);
			break;
		case GridRotation.Deg_135:
			SetRotation(GridRotation.Deg_90);
			break;
		case GridRotation.Deg_225:
			SetRotation(GridRotation.Deg_180);
			break;
		case GridRotation.Deg_315:
			SetRotation(GridRotation.Deg_270);
			break;
		}
	}

	public virtual bool IsInInteractiveRadius(Vector3 position)
	{
		Vector3 a = base.transform.position - base.rigidbody.velocity * Time.deltaTime;
		a.z = 0f;
		return Vector3.Distance(a, position) <= m_interactiveRadius;
	}

	public void ProcessTouch()
	{
		if ((!WPFMonoBehaviour.levelManager || WPFMonoBehaviour.levelManager.gameState == LevelManager.GameState.Running) && !HasGeneratorRef)
		{
			OnTouch();
			OnTouch(hasPosition: false, Vector3.zero);
		}
	}

	public void ProcessTouch(Vector3 touchPosition)
	{
		if (!WPFMonoBehaviour.levelManager || WPFMonoBehaviour.levelManager.gameState == LevelManager.GameState.Running)
		{
			OnTouch();
			OnTouch(hasPosition: true, touchPosition);
		}
	}

	protected virtual void OnTouch()
	{
	}

	protected virtual void OnTouch(bool hasPosition, Vector3 touchPosition)
	{
	}

	public void OnCollisionEnter(UnityEngine.Collision c) { OnPhysicsCollisionEnter(new PhysicsCollision(c)); }

	public virtual void OnPhysicsCollisionEnter(PhysicsCollision c)
	{
		Collider collider = c.collider;
		GameObject gameObject = collider.gameObject;
		int layer = gameObject.layer;
		BasePart component = collider.GetComponent<BasePart>();
		if (layer == m_groundLayer || gameObject.CompareTag("Ground"))
		{
			if (contraption.IsConnectedToPig(this, base.collider))
			{
				m_lastTimeTouchedGround = Time.time;
				contraption.SetTouchingGround(touching: true);
			}
			m_isOnGround = true;
		}
		if ((bool)component && component.ConnectedComponent == ConnectedComponent)
		{
			return;
		}
		m_isOnIce = gameObject.CompareTag("IceSurface");
		PlayCollisionAudio(this, c);
		if (!(Time.time - m_lastTimeUsedCollisionParticles > 0.25f))
		{
			return;
		}
		int num = c.contacts.Length;
		for (int i = 0; i < num; i++)
		{
			PhysicsContactPoint contactPoint = c.contacts[i];
			if (contactPoint.otherCollider.CompareTag("Untagged") && (bool)base.rigidbody)
			{
				m_lastTimeUsedCollisionParticles = Time.time;
				WPFMonoBehaviour.effectManager.CreateParticles(WPFMonoBehaviour.gameData.m_dustParticles, base.transform.position - Vector3.forward);
				float num2 = Vector3.Dot(base.rigidbody.GetPointVelocity(contactPoint.point), contactPoint.normal);
				// 军事版功能：TimeBomb 的碰撞破坏阈值取 4 倍（INMilitaryFeatures.Enabled 关闭时仍是 m_breakVelocity）
				if (m_breakVelocity > 0f && num2 > ((INMilitaryFeatures.Enabled && m_partType == PartType.TimeBomb) ? (m_breakVelocity * 4f) : m_breakVelocity) && !m_broken)
				{
					OnBreak();
					m_broken = true;
				}
				break;
			}
		}
	}

	public void OnCollisionStay(UnityEngine.Collision c) { OnPhysicsCollisionStay(new PhysicsCollision(c)); }

	public virtual void OnPhysicsCollisionStay(PhysicsCollision c)
	{
		int layer = c.collider.gameObject.layer;
		if (layer == m_groundLayer || layer == m_iceGroundLayer)
		{
			m_lastTimeTouchedGround = Time.time;
			m_contraption.SetGroundTouchTime(this);
		}
	}

	public void OnCollisionExit(UnityEngine.Collision c) { OnPhysicsCollisionExit(new PhysicsCollision(c)); }

	public virtual void OnPhysicsCollisionExit(PhysicsCollision c)
	{
		GameObject gameObject = c.gameObject;
		int layer = gameObject.layer;
		if (layer == m_iceGroundLayer)
		{
			m_isOnIce = false;
		}
		if (layer == m_groundLayer || gameObject.CompareTag("Ground"))
		{
			m_isOnGround = false;
		}
	}

	protected void LateUpdate()
	{
		UpdateSoundEffect();
	}

	public void PlayCollisionAudio(BasePart collisionPart, PhysicsCollision collisionData)
	{
		if (collisionData.relativeVelocity.magnitude < 2.5f)
		{
			return;
		}
		AudioSource[] array = null;
		AudioSource[] array2 = null;
		switch (collisionPart.AudioMaterial)
		{
		default:
			array = null;
			array2 = null;
			break;
		case AudioManager.AudioMaterial.Wood:
			array = WPFMonoBehaviour.gameData.commonAudioCollection.collisionWoodHit;
			array2 = WPFMonoBehaviour.gameData.commonAudioCollection.collisionWoodDamage;
			break;
		case AudioManager.AudioMaterial.Metal:
			array = WPFMonoBehaviour.gameData.commonAudioCollection.collisionMetalHit;
			array2 = WPFMonoBehaviour.gameData.commonAudioCollection.collisionMetalDamage;
			break;
		}
		float num = 1f;
		if (this is GoldenPig)
		{
			array = WPFMonoBehaviour.gameData.commonAudioCollection.goldenPigHit;
			array2 = array;
			num = 1.5f;
		}
		if (array == null || array2 == null)
		{
			return;
		}
		float num2 = 0f;
		Vector3 soundPosition = Vector3.zero;
		foreach (PhysicsContactPoint collisionDatum in collisionData)
		{
			float num3 = Vector3.Dot(collisionData.relativeVelocity, collisionDatum.normal);
			if (num3 > num2)
			{
				num2 = num3 * num;
				soundPosition = collisionDatum.point;
			}
		}
		if (num2 > 8f)
		{
			Singleton<AudioManager>.Instance.SpawnOneShotEffect(array2, soundPosition);
		}
		else if (num2 > 2.5f)
		{
			AudioSource audioSource = Singleton<AudioManager>.Instance.SpawnOneShotEffect(array, soundPosition);
			if ((bool)audioSource)
			{
				audioSource.volume = (num2 - 2f) / 8f;
			}
		}
	}

	public void PlayBreakAudio(BasePart breakingPart)
	{
		AudioSource[] array = breakingPart.AudioMaterial switch
		{
			AudioManager.AudioMaterial.Wood => WPFMonoBehaviour.gameData.commonAudioCollection.collisionWoodDestroy, 
			AudioManager.AudioMaterial.Metal => WPFMonoBehaviour.gameData.commonAudioCollection.collisionMetalBreak, 
			_ => null, 
		};
		if (array != null)
		{
			Singleton<AudioManager>.Instance.SpawnOneShotEffect(array, breakingPart.transform.position);
		}
	}

	public virtual void PostInitialize()
	{
		AddShineEffect();
	}

	public virtual void PrePlaced()
	{
		if (!INSettings.GetBool(INFeature.DisableAlienPartParticles))
		{
			for (int i = 0; i < tags.Count; i++)
			{
				if (tags[i] == "Alien_part")
				{
					GameObject obj = UnityEngine.Object.Instantiate(WPFMonoBehaviour.gameData.m_alienPartParticles);
					obj.transform.parent = base.transform;
					obj.transform.localPosition = Vector3.back * 0.1f;
					obj.transform.localRotation = Quaternion.identity;
					obj.GetComponent<ParticleSystem>().startDelay = UnityEngine.Random.Range(0f, 2f);
				}
			}
		}
		if (WPFMonoBehaviour.levelManager != null && WPFMonoBehaviour.levelManager.CurrentGameMode is CakeRaceMode && Singleton<CakeRaceKingsFavorite>.Instance.CurrentFavorite.m_partType == m_partType && Singleton<CakeRaceKingsFavorite>.Instance.CurrentFavorite.m_partTier == m_partTier)
		{
			GameObject obj2 = UnityEngine.Object.Instantiate(WPFMonoBehaviour.gameData.m_heartParticles);
			obj2.transform.parent = base.transform;
			obj2.transform.localPosition = Vector3.back;
			obj2.transform.localRotation = Quaternion.identity;
		}
	}

	private void AddShineEffect()
	{
		if (WPFMonoBehaviour.levelManager != null && HasTag("Gold"))
		{
			SpriteShineEffect.AddOneTimeShine(base.gameObject);
		}
	}

	public static Vector3 GetDirectionVector(Direction direction)
	{
		return direction switch
		{
			Direction.Right => Vector3.right, 
			Direction.Up => Vector3.up, 
			Direction.Left => -Vector3.right, 
			Direction.Down => -Vector3.up, 
			_ => Vector3.up, 
		};
	}

	public virtual bool IsIntegralPart()
	{
		return true;
	}

	public virtual bool CanEncloseParts()
	{
		return false;
	}

	public virtual bool CanBeEnclosed()
	{
		if (INSettings.GetBool(INFeature.EnclosableParts))
		{
			if (INSettings.GetBool(INFeature.WoodenBox) && m_partType == PartType.WoodenFrame && customPartIndex == 10)
			{
				return true;
			}
			if (INSettings.GetBool(INFeature.MetalBox) && m_partType == PartType.MetalFrame && customPartIndex == 130)
			{
				return true;
			}
			if (m_partType != PartType.WoodenFrame)
			{
				return m_partType != PartType.MetalFrame;
			}
			return false;
		}
		return false;
	}

	public virtual bool IsPartOfChassis()
	{
		return false;
	}

	public virtual JointConnectionType GetJointConnectionType()
	{
		return m_jointConnectionType;
	}

	public virtual bool ValidatePart()
	{
		return true;
	}

	public virtual void EnsureRigidbody()
	{
		if (base.rigidbody == null)
		{
			base.rigidbody = base.gameObject.AddComponent<Rigidbody>();
		}
		base.rigidbody.constraints = (RigidbodyConstraints)56;
		base.rigidbody.mass = m_mass;
		StarSeaSettings starSeaSettings = INUserSettings.Instance?.StarSeaSettings;
		base.rigidbody.drag = ((starSeaSettings != null) ? starSeaSettings.PartDrag : 0.2f);
		base.rigidbody.angularDrag = ((starSeaSettings != null) ? starSeaSettings.PartAngularDrag : 0.05f);
		base.rigidbody.useGravity = true;
		base.rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
		PartPhysicsParameters.Refresh(this);
		if (base.gameObject.layer == LayerMask.NameToLayer("Default") || base.gameObject.layer == LayerMask.NameToLayer("Contraption"))
		{
			base.gameObject.layer = LayerMask.NameToLayer("Contraption");
			for (int i = 0; i < base.transform.childCount; i++)
			{
				base.transform.GetChild(i).gameObject.layer = LayerMask.NameToLayer("Contraption");
			}
		}
	}

	public virtual Joint CustomConnectToPart(BasePart part)
	{
		return null;
	}

	public virtual void OnChangeConnections()
	{
		if (m_jointConnectionDirection != JointConnectionDirection.Any && m_jointConnectionDirection != JointConnectionDirection.None && !contraption.CanConnectTo(this, GetJointConnectionDirection()))
		{
			contraption.AutoAlign(this);
		}
		ChangeVisualConnections();
	}

	public virtual void ChangeVisualConnections()
	{
	}

	public void OnEnclosedPartDetached()
	{
	}

	public virtual void OnDetach()
	{
	}

	protected virtual void OnJointBreak(float breakForce)
	{
		HandleJointBreak();
	}

	protected virtual void OnFlipped()
	{
	}

	public virtual void OnPartPlaced()
	{
	}

	public void HandleJointBreak(bool playEffects = true)
	{
		CheckForBrokenPartsAchievement();
		contraption.UpdateConnectedComponents();
		if (playEffects)
		{
			PlayBreakAudio(this);
			Vector3 position = base.transform.position;
			Vector3 normalized = base.rigidbody.velocity.normalized;
			GameObject sprite = ((m_partType != PartType.WoodenFrame && m_partType != PartType.Pig) ? WPFMonoBehaviour.gameData.m_snapSprite : WPFMonoBehaviour.gameData.m_krakSprite);
			WPFMonoBehaviour.effectManager.ShowBreakEffect(sprite, position - 2f * normalized + new Vector3(0f, 0f, -10f), Quaternion.AngleAxis(UnityEngine.Random.Range(-30f, 30f), Vector3.forward));
		}
	}

	public virtual void OnBreak()
	{
	}

	protected virtual void UpdateSoundEffect()
	{
		if (!contraption || !contraption.IsRunning)
		{
			return;
		}
		float num = Mathf.Abs(base.rigidbody.velocity.magnitude);
		float num2 = 1f;
		float num3 = 10f;
		if (m_isOnIce && num > num2)
		{
			if (!m_slidingSound)
			{
				SpawnSlidingSound();
			}
			if ((bool)m_slidingSound)
			{
				m_slidingSound.volume = 0.25f * (Mathf.Clamp(num, num2, num3) / num3);
			}
		}
		else if ((bool)m_slidingSound)
		{
			if (Time.time - m_lastTimeTouchedGround < 0.4f)
			{
				m_slidingSound.volume = 0.25f * Mathf.Clamp01(1f - (Time.time - m_lastTimeTouchedGround) / 0.3f);
			}
			else
			{
				m_slidingSound.volume = 0f;
			}
		}
	}

	private void SpawnSlidingSound()
	{
		if ((bool)m_slidingSound)
		{
			m_slidingSound.Stop();
		}
		m_slidingSound = Singleton<AudioManager>.Instance.SpawnOneShotEffect(WPFMonoBehaviour.gameData.commonAudioCollection.partSlideOnIceLoop, base.transform);
		if ((bool)m_slidingSound)
		{
			m_slidingSound.volume = 0f;
		}
	}

	public void CheckForBrokenPartsAchievement()
	{
		if (!Singleton<SocialGameManager>.IsInstantiated() || !Singleton<GameManager>.Instance.IsInGame())
		{
			return;
		}
		int brokenParts = GameProgress.GetInt("Broken_Parts") + 1;
		GameProgress.SetInt("Broken_Parts", brokenParts);
		((Action<List<string>>)delegate(List<string> achievements)
		{
			foreach (string achievement in achievements)
			{
				if (Singleton<SocialGameManager>.Instance.TryReportAchievementProgress(achievement, 100.0, (int limit) => brokenParts >= limit))
				{
					break;
				}
			}
		})(new List<string> { "grp.VETERAN_WRECKER", "grp.QUALIFIED_WRECKER", "grp.JUNIOR_WRECKER" });
	}

	public string GetAnalyticsName()
	{
		return base.name;
	}

	public bool BelongsTo(PartTypeInfo info)
	{
		return TypeInfo.BelongsTo(info);
	}

	public bool BelongsTo(PartTypeInfo infoA, PartTypeInfo infoB)
	{
		if (!TypeInfo.BelongsTo(infoA))
		{
			return TypeInfo.BelongsTo(infoB);
		}
		return true;
	}

	public bool BelongsTo(PartRangeInfo info)
	{
		return TypeInfo.BelongsTo(info);
	}

	public bool BelongsTo(PartRangeInfo infoA, PartRangeInfo infoB)
	{
		if (!TypeInfo.BelongsTo(infoA))
		{
			return TypeInfo.BelongsTo(infoB);
		}
		return true;
	}

	public void MoveTo(int x, int y)
	{
		CoordX = x;
		CoordY = y;
		float num = (float)(-(x + 2 * y)) / 100000f;
		base.transform.localPosition = new Vector3(x, y, -0.1f + m_ZOffset + num);
	}

	public void RotateTo(int rotation, bool flipped)
	{
		if (flipped)
		{
			SetFlipped(flipped);
		}
		SetRotation(rotation);
	}

	public virtual void Inject(IInjectionPart part)
	{
		throw new NotImplementedException();
	}

	public virtual IEnumerable<Rigidbody> GetRigidbodies()
	{
		yield return base.rigidbody;
	}

	public virtual IEnumerable<Collider> GetColliders()
	{
		yield return base.collider;
	}

	public virtual IEnumerable<Renderer> GetRenderers()
	{
		yield return base.renderer;
	}

	public virtual bool IsCustomRotated()
	{
		return m_autoAlign == (AutoAlignType)(-1);
	}

	public virtual int GetRotation()
	{
		return (int)m_gridRotation;
	}

	public virtual void SetRotation(int rotation)
	{
	}

	public virtual void CreateCustomJoints()
	{
	}

	public virtual void OnLightEnter(EntityLightCollision collision)
	{
	}

	public virtual bool IsTriggerable()
	{
		if (!HasGeneratorRef)
		{
			if (!CanBeEnabled())
			{
				return contraption.HasOneShotParts(m_partType, EffectDirection());
			}
			return true;
		}
		return false;
	}

	public virtual IEnumerable<UIPartTriggerButtonInfo> GetTriggerButtonInfo()
	{
		yield return new UIPartTriggerButtonInfo(UIPartButtonType.Trigger, 0, m_partType, (int)EffectDirection(), m_connectedComponent, HasOnOffToggle());
	}

	public virtual IEnumerable<UIPartSliderButtonInfo> GetSliderButtonInfo()
	{
		yield break;
	}

	public virtual void OnButtonTriggered(UIPartTriggerButton button)
	{
		ProcessTouch();
	}

	public virtual void OnSliderButtonTriggered(UIPartSliderButton button)
	{
	}

	public virtual void ApplyTemplate(CustomPartTemplate template, IResourceResolver resolver)
	{
		base.gameObject.layer = LayerMask.NameToLayer("Contraption");
		PartListData partListData = INUnity.LoadScriptableObject<PartListData>("PartListData");
		m_partType = ((SortedPartType)template.PartType).ToPartType();
		m_partTier = (PartTier)template.PartTier;
		customPartIndex = template.PartIndex;
		m_mass = template.Mass;
		m_powerConsumption = template.PowerConsumption;
		m_enginePower = template.EnginePower;
		if (tags == null)
		{
			tags = new List<string>();
		}
		if (template.IconRendererTemplate != null)
		{
			GameObject gameObject = UnityEngine.Object.Instantiate(partListData.PartIconPrefab);
			gameObject.name = "Icon_" + Type.ToString() + "_" + (Index + 1);
			gameObject.layer = LayerMask.NameToLayer("HUD");
			TemplateFactory.ApplyTemplate(gameObject, template.IconRendererTemplate, resolver);
			m_constructionIconSprite = gameObject.GetComponent<Sprite>();
		}
	}

	public static Direction Rotate4(Direction direction, int times)
	{
		return (Direction)((int)(direction + times) % 4);
	}

	public static Direction Rotate8(Direction direction, int times)
	{
		int num = (int)direction;
		num = ((num < 4) ? (num * 2) : ((num - 4) * 2 + 1));
		num = (num + times) % 8;
		return (Direction)((num % 2 == 0) ? (num / 2) : (num / 2 + 4));
	}

	public static int GetIntegralAngle(GridRotation rotation)
	{
		return rotation switch
		{
			GridRotation.Deg_0 => 0, 
			GridRotation.Deg_45 => 45, 
			GridRotation.Deg_90 => 90, 
			GridRotation.Deg_135 => 135, 
			GridRotation.Deg_180 => 180, 
			GridRotation.Deg_225 => 225, 
			GridRotation.Deg_270 => 270, 
			GridRotation.Deg_315 => 315, 
			GridRotation.Deg_Max => 360, 
			_ => throw new ArgumentException("rotation"), 
		};
	}

	public static (int, int) GetDirection(GridRotation rotation)
	{
		return rotation switch
		{
			GridRotation.Deg_0 => (1, 0), 
			GridRotation.Deg_45 => (1, 1), 
			GridRotation.Deg_90 => (0, 1), 
			GridRotation.Deg_135 => (-1, 1), 
			GridRotation.Deg_180 => (-1, 0), 
			GridRotation.Deg_225 => (-1, -1), 
			GridRotation.Deg_270 => (0, -1), 
			GridRotation.Deg_315 => (1, -1), 
			GridRotation.Deg_Max => (1, 0), 
			_ => throw new ArgumentException("rotation"), 
		};
	}

	public static void GetDirection(GridRotation rotation, out int x, out int y)
	{
		(x, y) = GetDirection(rotation);
	}
}
