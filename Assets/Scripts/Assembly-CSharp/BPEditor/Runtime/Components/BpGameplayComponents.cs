using System.Collections.Generic;
using UnityEngine;

// 1:1 移植 badpiggies-editor 的 unity_runtime/components/{wind_area,bridge,fan,engine,sprite,
// unmanaged_sprite,particle_system,position_serializer,point_light_source}.rs。
// 约定同 BpCoreComponents.cs：Option<T> → T?；get_field 未设置返回 null；set_field 命中 true。

// ---- WindArea ----
public class BpWindAreaComponent : BpUnityComponent
{
	public Vector3? windDirectionHandle;
	public float? windPowerFactor;
	public bool? calculateParticleValues;
	public List<KeyValuePair<string, BpValue>> extra = new List<KeyValuePair<string, BpValue>>();

	public static readonly Vector3 DEFAULT_WIND_DIRECTION_HANDLE = new Vector3(0f, 1f, 0f);
	public const float DEFAULT_WIND_POWER_FACTOR = 1.0f;
	public const bool DEFAULT_CALCULATE_PARTICLE_VALUES = true;

	public override string ComponentSuffix { get { return "WindArea"; } }

	public override BpValue GetField(BpScene scene, string name)
	{
		switch (name)
		{
			case "windDirectionHandle":
				return windDirectionHandle.HasValue ? BpValue.OfVector3(windDirectionHandle.Value) : null;
			case "m_windPowerFactor":
				return windPowerFactor.HasValue ? BpValue.Float(windPowerFactor.Value) : null;
			case "m_calculateParticleValues":
				return calculateParticleValues.HasValue ? BpValue.Boolean(calculateParticleValues.Value) : null;
		}
		return null;
	}

	public override bool SetField(BpScene scene, string name, BpValue value)
	{
		switch (name)
		{
			case "windDirectionHandle":
				if (value.kind != BpValueKind.Vector3) return false;
				windDirectionHandle = value.vector3; return true;
			case "m_windPowerFactor":
				if (value.kind != BpValueKind.Float) return false;
				windPowerFactor = value.floatValue; return true;
			case "m_calculateParticleValues":
				if (value.kind != BpValueKind.Boolean) return false;
				calculateParticleValues = value.boolValue; return true;
		}
		return false;
	}

	public override List<KeyValuePair<string, BpValue>> Extra { get { return extra; } }

	public override BpUnityComponent Clone()
	{
		BpWindAreaComponent c = new BpWindAreaComponent();
		c.windDirectionHandle = windDirectionHandle;
		c.windPowerFactor = windPowerFactor;
		c.calculateParticleValues = calculateParticleValues;
		c.extra = BpComponentUtil.CloneExtra(extra);
		return c;
	}
}

// ---- Bridge ----
public class BpBridgeComponent : BpUnityComponent
{
	public float? stepLength;
	public float? stepGap;
	public List<KeyValuePair<string, BpValue>> extra = new List<KeyValuePair<string, BpValue>>();

	public const float DEFAULT_STEP_LENGTH = 1.0f;
	public const float DEFAULT_STEP_GAP = 0.2f;

	public override string ComponentSuffix { get { return "Bridge"; } }

	public override BpValue GetField(BpScene scene, string name)
	{
		switch (name)
		{
			case "stepLength":
				return stepLength.HasValue ? BpValue.Float(stepLength.Value) : null;
			case "stepGap":
				return stepGap.HasValue ? BpValue.Float(stepGap.Value) : null;
		}
		return null;
	}

	public override bool SetField(BpScene scene, string name, BpValue value)
	{
		switch (name)
		{
			case "stepLength":
				if (value.kind != BpValueKind.Float) return false;
				stepLength = value.floatValue; return true;
			case "stepGap":
				if (value.kind != BpValueKind.Float) return false;
				stepGap = value.floatValue; return true;
		}
		return false;
	}

	public override List<KeyValuePair<string, BpValue>> Extra { get { return extra; } }

	public override BpUnityComponent Clone()
	{
		BpBridgeComponent c = new BpBridgeComponent();
		c.stepLength = stepLength; c.stepGap = stepGap;
		c.extra = BpComponentUtil.CloneExtra(extra);
		return c;
	}
}

// ---- Fan ----
// 注意：AnimationCurve 字段（verticalRamp/horizontalRamp/spinupRamp）由宿主的
// set_animation_curve 适配器落到 extra。
public class BpFanComponent : BpUnityComponent
{
	public float? targetForce;
	public bool? alwaysOn;
	public float? delayedStart;
	public float? startTime;
	public float? offTime;
	public float? onTime;
	public float? hearingDistance;
	public float? counter;
	public List<KeyValuePair<string, BpValue>> extra = new List<KeyValuePair<string, BpValue>>();

	public const float DEFAULT_HEARING_DISTANCE = 1000.0f;

	public override string ComponentSuffix { get { return "Fan"; } }

	public override BpValue GetField(BpScene scene, string name)
	{
		switch (name)
		{
			case "targetForce": return targetForce.HasValue ? BpValue.Float(targetForce.Value) : null;
			case "alwaysOn": return alwaysOn.HasValue ? BpValue.Boolean(alwaysOn.Value) : null;
			case "delayedStart": return delayedStart.HasValue ? BpValue.Float(delayedStart.Value) : null;
			case "startTime": return startTime.HasValue ? BpValue.Float(startTime.Value) : null;
			case "offTime": return offTime.HasValue ? BpValue.Float(offTime.Value) : null;
			case "onTime": return onTime.HasValue ? BpValue.Float(onTime.Value) : null;
			case "hearingDistance": return hearingDistance.HasValue ? BpValue.Float(hearingDistance.Value) : null;
			case "counter": return counter.HasValue ? BpValue.Float(counter.Value) : null;
		}
		return null;
	}

	public override bool SetField(BpScene scene, string name, BpValue value)
	{
		switch (name)
		{
			case "targetForce":
				if (value.kind != BpValueKind.Float) return false;
				targetForce = value.floatValue; return true;
			case "alwaysOn":
				if (value.kind != BpValueKind.Boolean) return false;
				alwaysOn = value.boolValue; return true;
			case "delayedStart":
				if (value.kind != BpValueKind.Float) return false;
				delayedStart = value.floatValue; return true;
			case "startTime":
				if (value.kind != BpValueKind.Float) return false;
				startTime = value.floatValue; return true;
			case "offTime":
				if (value.kind != BpValueKind.Float) return false;
				offTime = value.floatValue; return true;
			case "onTime":
				if (value.kind != BpValueKind.Float) return false;
				onTime = value.floatValue; return true;
			case "hearingDistance":
				if (value.kind != BpValueKind.Float) return false;
				hearingDistance = value.floatValue; return true;
			case "counter":
				if (value.kind != BpValueKind.Float) return false;
				counter = value.floatValue; return true;
		}
		return false;
	}

	public override List<KeyValuePair<string, BpValue>> Extra { get { return extra; } }

	public override BpUnityComponent Clone()
	{
		BpFanComponent c = new BpFanComponent();
		c.targetForce = targetForce; c.alwaysOn = alwaysOn; c.delayedStart = delayedStart;
		c.startTime = startTime; c.offTime = offTime; c.onTime = onTime;
		c.hearingDistance = hearingDistance; c.counter = counter;
		c.extra = BpComponentUtil.CloneExtra(extra);
		return c;
	}
}

// ---- Engine ----
public class BpEngineComponent : BpUnityComponent
{
	public bool running;
	public List<KeyValuePair<string, BpValue>> extra = new List<KeyValuePair<string, BpValue>>();

	public override string ComponentSuffix { get { return "Engine"; } }

	public override BpValue GetField(BpScene scene, string name)
	{
		if (name == "m_running") return BpValue.Boolean(running);
		return null;
	}

	public override bool SetField(BpScene scene, string name, BpValue value)
	{
		if (name == "m_running" && value.kind == BpValueKind.Boolean)
		{
			running = value.boolValue;
			return true;
		}
		return false;
	}

	public override List<KeyValuePair<string, BpValue>> Extra { get { return extra; } }

	public override BpUnityComponent Clone()
	{
		BpEngineComponent c = new BpEngineComponent();
		c.running = running;
		c.extra = BpComponentUtil.CloneExtra(extra);
		return c;
	}
}

// ---- Sprite（Bad Piggies 图集 sprite 渲染器，非 UnityEngine.Sprite）----
public class BpSpriteComponent : BpUnityComponent
{
	public string id = "";
	public float scaleX = 1.0f;
	public float scaleY = 1.0f;
	public int pivotX = 0;
	public int pivotY = 0;
	public bool updateCollider = true;
	public List<KeyValuePair<string, BpValue>> extra = new List<KeyValuePair<string, BpValue>>();

	public override string ComponentSuffix { get { return "Sprite"; } }

	public override BpValue GetField(BpScene scene, string name)
	{
		switch (name)
		{
			case "m_id": return BpValue.Str(id);
			case "m_scaleX": return BpValue.Float(scaleX);
			case "m_scaleY": return BpValue.Float(scaleY);
			case "m_pivotX": return BpValue.Integer(pivotX);
			case "m_pivotY": return BpValue.Integer(pivotY);
			case "m_updateCollider": return BpValue.Boolean(updateCollider);
		}
		return null;
	}

	public override bool SetField(BpScene scene, string name, BpValue value)
	{
		switch (name)
		{
			case "m_id":
				if (value.kind != BpValueKind.String) return false;
				id = value.stringValue; return true;
			case "m_scaleX":
				if (value.kind != BpValueKind.Float) return false;
				scaleX = value.floatValue; return true;
			case "m_scaleY":
				if (value.kind != BpValueKind.Float) return false;
				scaleY = value.floatValue; return true;
			case "m_pivotX":
				if (value.kind != BpValueKind.Integer) return false;
				pivotX = value.intValue; return true;
			case "m_pivotY":
				if (value.kind != BpValueKind.Integer) return false;
				pivotY = value.intValue; return true;
			case "m_updateCollider":
				if (value.kind != BpValueKind.Boolean) return false;
				updateCollider = value.boolValue; return true;
		}
		return false;
	}

	public override List<KeyValuePair<string, BpValue>> Extra { get { return extra; } }

	public override BpUnityComponent Clone()
	{
		BpSpriteComponent c = new BpSpriteComponent();
		c.id = id; c.scaleX = scaleX; c.scaleY = scaleY;
		c.pivotX = pivotX; c.pivotY = pivotY; c.updateCollider = updateCollider;
		c.extra = BpComponentUtil.CloneExtra(extra);
		return c;
	}
}

// ---- UnmanagedSprite ----
public class BpUnmanagedSpriteComponent : BpUnityComponent
{
	public int textureWidth = 0;
	public int textureHeight = 0;
	public float scale = 1.0f;
	public int spriteWidth = 0;
	public int spriteHeight = 0;
	public int uvX = 0;
	public int uvY = 0;
	public int width = 16;
	public int height = 16;
	public int atlasGridSubdivisions = 16;
	public int border = 0;
	public bool updateCollider = false;
	public List<KeyValuePair<string, BpValue>> extra = new List<KeyValuePair<string, BpValue>>();

	public override string ComponentSuffix { get { return "UnmanagedSprite"; } }

	public override BpValue GetField(BpScene scene, string name)
	{
		switch (name)
		{
			case "m_textureWidth": return BpValue.Integer(textureWidth);
			case "m_textureHeight": return BpValue.Integer(textureHeight);
			case "m_scale": return BpValue.Float(scale);
			case "m_spriteWidth": return BpValue.Integer(spriteWidth);
			case "m_spriteHeight": return BpValue.Integer(spriteHeight);
			case "m_UVx": return BpValue.Integer(uvX);
			case "m_UVy": return BpValue.Integer(uvY);
			case "m_width": return BpValue.Integer(width);
			case "m_height": return BpValue.Integer(height);
			case "m_atlasGridSubdivisions": return BpValue.Integer(atlasGridSubdivisions);
			case "m_border": return BpValue.Integer(border);
			case "m_updateCollider": return BpValue.Boolean(updateCollider);
		}
		return null;
	}

	public override bool SetField(BpScene scene, string name, BpValue value)
	{
		if (value.kind == BpValueKind.Integer)
		{
			switch (name)
			{
				case "m_textureWidth": textureWidth = value.intValue; return true;
				case "m_textureHeight": textureHeight = value.intValue; return true;
				case "m_spriteWidth": spriteWidth = value.intValue; return true;
				case "m_spriteHeight": spriteHeight = value.intValue; return true;
				case "m_UVx": uvX = value.intValue; return true;
				case "m_UVy": uvY = value.intValue; return true;
				case "m_width": width = value.intValue; return true;
				case "m_height": height = value.intValue; return true;
				case "m_atlasGridSubdivisions": atlasGridSubdivisions = value.intValue; return true;
				case "m_border": border = value.intValue; return true;
			}
			return false;
		}
		if (value.kind == BpValueKind.Float && name == "m_scale")
		{
			scale = value.floatValue; return true;
		}
		if (value.kind == BpValueKind.Boolean && name == "m_updateCollider")
		{
			updateCollider = value.boolValue; return true;
		}
		return false;
	}

	public override List<KeyValuePair<string, BpValue>> Extra { get { return extra; } }

	public override BpUnityComponent Clone()
	{
		BpUnmanagedSpriteComponent c = new BpUnmanagedSpriteComponent();
		c.textureWidth = textureWidth; c.textureHeight = textureHeight; c.scale = scale;
		c.spriteWidth = spriteWidth; c.spriteHeight = spriteHeight;
		c.uvX = uvX; c.uvY = uvY; c.width = width; c.height = height;
		c.atlasGridSubdivisions = atlasGridSubdivisions; c.border = border;
		c.updateCollider = updateCollider;
		c.extra = BpComponentUtil.CloneExtra(extra);
		return c;
	}
}

// ---- ParticleSystem（ObjectDeserializer 暴露的子集）----
// 反序列化器通过专门的 RuntimeHost 钩子写 startLifetime/startSpeed/emissionRate；
// 其它模块字段（ShapeModule 等）经通用 set_field 溢出到 extra。
public class BpParticleSystemComponent : BpUnityComponent
{
	public float? startLifetime;
	public float? startSpeed;
	public float? emissionRate;
	public List<KeyValuePair<string, BpValue>> extra = new List<KeyValuePair<string, BpValue>>();

	public override string ComponentSuffix { get { return "ParticleSystem"; } }

	public override BpValue GetField(BpScene scene, string name)
	{
		switch (name)
		{
			case "startLifetime": return startLifetime.HasValue ? BpValue.Float(startLifetime.Value) : null;
			case "startSpeed": return startSpeed.HasValue ? BpValue.Float(startSpeed.Value) : null;
			case "emissionRate": return emissionRate.HasValue ? BpValue.Float(emissionRate.Value) : null;
		}
		return null;
	}

	public override bool SetField(BpScene scene, string name, BpValue value)
	{
		if (value.kind != BpValueKind.Float) return false;
		switch (name)
		{
			case "startLifetime": startLifetime = value.floatValue; return true;
			case "startSpeed": startSpeed = value.floatValue; return true;
			case "emissionRate": emissionRate = value.floatValue; return true;
		}
		return false;
	}

	public override List<KeyValuePair<string, BpValue>> Extra { get { return extra; } }

	public override BpUnityComponent Clone()
	{
		BpParticleSystemComponent c = new BpParticleSystemComponent();
		c.startLifetime = startLifetime; c.startSpeed = startSpeed; c.emissionRate = emissionRate;
		c.extra = BpComponentUtil.CloneExtra(extra);
		return c;
	}
}

// ---- PositionSerializer ----
// childLocalPositions（Vector3 数组）经 Scene.set_array 以 "childLocalPositions" 键
// 作为 Generic 落到 extra。
public class BpPositionSerializerComponent : BpUnityComponent
{
	// `ObjectReference prefab = N` —— index 指向关卡引用表；-1 = 未设置
	public int prefab = -1;
	public List<KeyValuePair<string, BpValue>> extra = new List<KeyValuePair<string, BpValue>>();

	public override string ComponentSuffix { get { return "PositionSerializer"; } }

	public override BpValue GetField(BpScene scene, string name)
	{
		if (name == "prefab" && prefab >= 0) return BpValue.ObjectRef(prefab);
		return null;
	}

	public override bool SetField(BpScene scene, string name, BpValue value)
	{
		if (name != "prefab") return false;
		if (value.kind == BpValueKind.ObjectReference) { prefab = value.objectReference; return true; }
		if (value.kind == BpValueKind.Null) { prefab = -1; return true; }
		return false;
	}

	public override List<KeyValuePair<string, BpValue>> Extra { get { return extra; } }

	public override BpUnityComponent Clone()
	{
		BpPositionSerializerComponent c = new BpPositionSerializerComponent();
		c.prefab = prefab;
		c.extra = BpComponentUtil.CloneExtra(extra);
		return c;
	}
}

// ---- PointLightSource ----
public class BpPointLightSourceComponent : BpUnityComponent
{
	public float? size;
	public float? borderWidth;
	public float? beamAngle;
	public float? beamCut;
	public int? vertexCount;
	public float? colliderSize;
	public Vector3? beamArcCenter;
	public float? baseLightSize;
	public bool? canLitObjects;
	public bool? canCollide;
	public bool? canBeLit;
	public bool? usesCurves;
	public List<KeyValuePair<string, BpValue>> extra = new List<KeyValuePair<string, BpValue>>();

	public const float DEFAULT_SIZE = 1.0f;
	public const float DEFAULT_BORDER_WIDTH = 0.3f;
	public const float DEFAULT_BEAM_ANGLE = 45.0f;
	public const float DEFAULT_BEAM_CUT = 0.5f;
	public const int DEFAULT_VERTEX_COUNT = 100;
	public const float DEFAULT_COLLIDER_SIZE = 0.0f;
	public const float DEFAULT_BASE_LIGHT_SIZE = 0.5f;
	public const bool DEFAULT_USES_CURVES = true;

	public override string ComponentSuffix { get { return "PointLightSource"; } }

	public override BpValue GetField(BpScene scene, string name)
	{
		switch (name)
		{
			case "size": return size.HasValue ? BpValue.Float(size.Value) : null;
			case "borderWidth": return borderWidth.HasValue ? BpValue.Float(borderWidth.Value) : null;
			case "beamAngle": return beamAngle.HasValue ? BpValue.Float(beamAngle.Value) : null;
			case "beamCut": return beamCut.HasValue ? BpValue.Float(beamCut.Value) : null;
			case "vertexCount": return vertexCount.HasValue ? BpValue.Integer(vertexCount.Value) : null;
			case "colliderSize": return colliderSize.HasValue ? BpValue.Float(colliderSize.Value) : null;
			case "beamArcCenter": return beamArcCenter.HasValue ? BpValue.OfVector3(beamArcCenter.Value) : null;
			case "baseLightSize": return baseLightSize.HasValue ? BpValue.Float(baseLightSize.Value) : null;
			case "canLitObjects": return canLitObjects.HasValue ? BpValue.Boolean(canLitObjects.Value) : null;
			case "canCollide": return canCollide.HasValue ? BpValue.Boolean(canCollide.Value) : null;
			case "canBeLit": return canBeLit.HasValue ? BpValue.Boolean(canBeLit.Value) : null;
			case "usesCurves": return usesCurves.HasValue ? BpValue.Boolean(usesCurves.Value) : null;
		}
		return null;
	}

	public override bool SetField(BpScene scene, string name, BpValue value)
	{
		if (value.kind == BpValueKind.Float)
		{
			switch (name)
			{
				case "size": size = value.floatValue; return true;
				case "borderWidth": borderWidth = value.floatValue; return true;
				case "beamAngle": beamAngle = value.floatValue; return true;
				case "beamCut": beamCut = value.floatValue; return true;
				case "colliderSize": colliderSize = value.floatValue; return true;
				case "baseLightSize": baseLightSize = value.floatValue; return true;
			}
			return false;
		}
		if (value.kind == BpValueKind.Integer && name == "vertexCount") { vertexCount = value.intValue; return true; }
		if (value.kind == BpValueKind.Vector3 && name == "beamArcCenter") { beamArcCenter = value.vector3; return true; }
		if (value.kind == BpValueKind.Boolean)
		{
			switch (name)
			{
				case "canLitObjects": canLitObjects = value.boolValue; return true;
				case "canCollide": canCollide = value.boolValue; return true;
				case "canBeLit": canBeLit = value.boolValue; return true;
				case "usesCurves": usesCurves = value.boolValue; return true;
			}
			return false;
		}
		return false;
	}

	public override List<KeyValuePair<string, BpValue>> Extra { get { return extra; } }

	public override BpUnityComponent Clone()
	{
		BpPointLightSourceComponent c = new BpPointLightSourceComponent();
		c.size = size; c.borderWidth = borderWidth; c.beamAngle = beamAngle; c.beamCut = beamCut;
		c.vertexCount = vertexCount; c.colliderSize = colliderSize; c.beamArcCenter = beamArcCenter;
		c.baseLightSize = baseLightSize; c.canLitObjects = canLitObjects; c.canCollide = canCollide;
		c.canBeLit = canBeLit; c.usesCurves = usesCurves;
		c.extra = BpComponentUtil.CloneExtra(extra);
		return c;
	}
}
