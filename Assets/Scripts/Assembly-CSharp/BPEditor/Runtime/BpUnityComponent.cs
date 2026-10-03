using System.Collections.Generic;

// 1:1 移植 badpiggies-editor 的 unity_runtime/components/{mod,unknown}.rs（P0 脚手架）。
// 说明：Rust 用 `Box<dyn UnityComponent>` + Any 下转；C# 直接继承抽象类 + is/as。
// 类型化组件（Transform/BoxCollider/Behaviour/WindArea/…）属 P1，尚未移植；
// 未识别的 suffix 一律回退 BpUnknownComponent —— 数据仍按流顺序完整保留（可回写）。

// UnityComponent trait 的 C# 对应
public abstract class BpUnityComponent
{
	// component_suffix()
	public abstract string ComponentSuffix { get; }

	// get_field(&self, scene, name) -> Option<SceneValue>
	public virtual BpValue GetField(BpScene scene, string name) { return null; }

	// set_field(&mut self, scene, name, value) -> bool
	public virtual bool SetField(BpScene scene, string name, BpValue value) { return false; }

	// set_object_reference_index(&mut self, scene, name, index) -> bool
	public virtual bool SetObjectReferenceIndex(BpScene scene, string name, int index) { return false; }

	// MonoBehaviour.OnDataLoaded 钩子
	public virtual void OnDataLoaded(BpScene scene) { }

	// extra_mut() / extra()
	public abstract List<KeyValuePair<string, BpValue>> Extra { get; }

	// clone_box()
	public abstract BpUnityComponent Clone();
}

// unity_runtime::components::UnknownComponent —— 未类型化组件的兜底。
// 按流顺序保留每一次属性写入，供后续（P3）原样回写 override 块。
public class BpUnknownComponent : BpUnityComponent
{
	public string suffix;
	// (字段名, 值)，按流顺序
	public List<KeyValuePair<string, BpValue>> fields = new List<KeyValuePair<string, BpValue>>();

	public BpUnknownComponent(string suffix)
	{
		this.suffix = suffix ?? "";
	}

	public override string ComponentSuffix { get { return suffix; } }

	// lookup(name)：最近一次写入
	public BpValue Lookup(string name)
	{
		for (int i = fields.Count - 1; i >= 0; i--)
			if (fields[i].Key == name) return fields[i].Value;
		return null;
	}

	public override BpValue GetField(BpScene scene, string name)
	{
		return Lookup(name);
	}

	public override bool SetField(BpScene scene, string name, BpValue value)
	{
		// 全部接受并记录；返回 true 使宿主不再经 extra 重复记录。
		fields.Add(new KeyValuePair<string, BpValue>(name, value));
		return true;
	}

	public override List<KeyValuePair<string, BpValue>> Extra { get { return fields; } }

	public override BpUnityComponent Clone()
	{
		BpUnknownComponent c = new BpUnknownComponent(suffix);
		for (int i = 0; i < fields.Count; i++)
			c.fields.Add(new KeyValuePair<string, BpValue>(
				fields[i].Key, fields[i].Value != null ? fields[i].Value.Clone() : null));
		return c;
	}
}

// components/mod.rs 的 make_component_by_suffix()
public static class BpComponentRegistry
{
	// P1：按 override 流里的短 suffix 查类型化工厂；未识别返回 null
	// → 调用方回退 BpUnknownComponent（数据仍可完整 round-trip）。
	public static BpUnityComponent MakeComponentBySuffix(string suffix)
	{
		switch (suffix)
		{
			case "Transform": return new BpTransformComponent();
			case "BoxCollider":
			case "BoxCollider2D": return new BpBoxColliderComponent();
			case "Behaviour": return new BpBehaviourComponent();
			case "WindArea": return new BpWindAreaComponent();
			case "Bridge": return new BpBridgeComponent();
			case "Fan": return new BpFanComponent();
			case "Engine": return new BpEngineComponent();
			case "LevelManager": return new BpLevelManagerComponent();
			case "BezierCurve": return new BpBezierCurveComponent();
			case "BezierMesh": return new BpBezierMeshComponent();
			case "PositionSerializer": return new BpPositionSerializerComponent();
			case "PointLightSource": return new BpPointLightSourceComponent();
			case "Sprite": return new BpSpriteComponent();
			case "UnmanagedSprite": return new BpUnmanagedSpriteComponent();
			case "ParticleSystem": return new BpParticleSystemComponent();
			case "Camera": return new BpCameraComponent();
			case "CameraPreview": return new BpCameraPreviewComponent();
			case "Rigidbody": return new BpRigidbodyComponent();
		}
		return null;
	}
}
