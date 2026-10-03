using System.Collections.Generic;
using UnityEngine;

// 载具部件碰撞弹性：把星海设置里的 CollisionBounciness 应用到运行中载具部件的碰撞体材质上。
//
// 原始材质 bounciness：载具基础材质 Contraption_PhysMat = 0（本设置默认值即 0）；
// 但小猪 Pig_PhysMat = 0.5、垃圾奖 JunkPrize_PhysMat = 0.75 等其它材质并非 0。
// 规则（保证默认行为与原版完全一致）：
//   · 值 = 0（原版默认）时：完全不改动任何材质；
//   · 只处理“原始 bounciness 为 0 的基础材质”，把它设为本设置的值；
//   · 原始 bounciness 非 0 的材质（小猪/垃圾奖等）始终不处理，避免破坏原版物理。
// 为避免污染共享材质资产，改为为每个碰撞体创建独立材质实例（保留原摩擦与合并模式）。
public class CollisionBouncinessManager : PartManager
{
	// 已被本管理器替换为独立实例的碰撞体（这些可以自由更新 bounciness）
	private static readonly HashSet<Collider> s_touched = new HashSet<Collider>();

	public override StatusCode Status => StatusCode.Running;

	public override void FixedUpdate()
	{
		StarSeaSettings settings = INUserSettings.Instance?.StarSeaSettings;
		if (settings == null)
		{
			return;
		}
		float want = Mathf.Clamp01(settings.CollisionBounciness);

		Contraption contraption = Contraption.Instance;
		if (contraption == null)
		{
			return;
		}
		List<BasePart> parts = contraption.Parts;
		if (parts == null)
		{
			return;
		}

		for (int i = 0; i < parts.Count; i++)
		{
			BasePart part = parts[i];
			if (part == null)
			{
				continue;
			}
			Collider[] colliders = part.GetComponentsInChildren<Collider>();
			for (int j = 0; j < colliders.Length; j++)
			{
				ApplyBounciness(colliders[j], want);
			}
		}
	}

	private static void ApplyBounciness(Collider collider, float want)
	{
		if (collider == null)
		{
			return;
		}

		// 已由本管理器接管：直接更新实例上的 bounciness（值回到 0 时即恢复原版）
		if (s_touched.Contains(collider))
		{
			PhysicMaterial owned = collider.sharedMaterial;
			if (owned != null && !Mathf.Approximately(owned.bounciness, want))
			{
				owned.bounciness = want;
			}
			return;
		}

		// 默认值 0 = 原版：完全不触碰任何材质
		if (want <= 0f)
		{
			return;
		}

		PhysicMaterial src = collider.sharedMaterial;
		float orig = (src != null) ? src.bounciness : 0f;

		// 仅处理基础材质(原始 bounciness 为 0)；小猪/垃圾奖等非 0 材质保持不变
		if (!Mathf.Approximately(orig, 0f))
		{
			return;
		}

		PhysicMaterial mat = new PhysicMaterial();
		if (src != null)
		{
			mat.dynamicFriction = src.dynamicFriction;
			mat.staticFriction = src.staticFriction;
			mat.frictionCombine = src.frictionCombine;
			mat.bounceCombine = src.bounceCombine;
		}
		else
		{
			mat.dynamicFriction = 0.7f;
			mat.staticFriction = 0.5f;
			mat.frictionCombine = PhysicMaterialCombine.Average;
			mat.bounceCombine = PhysicMaterialCombine.Average;
		}
		mat.bounciness = want;
		collider.sharedMaterial = mat;
		s_touched.Add(collider);
	}
}
