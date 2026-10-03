using UnityEngine;

public struct INPhysicMaterial
{
	public float Bounciness;

	public PhysicMaterialCombine BounceMode;

	public float Friction;

	public PhysicMaterialCombine FrictionMode;

	public INPhysicMaterial(PhysicMaterial material)
	{
		Bounciness = material.bounciness;
		BounceMode = material.bounceCombine;
		Friction = material.dynamicFriction;
		FrictionMode = material.frictionCombine;
	}

	public INPhysicMaterial(float bounciness, PhysicMaterialCombine bounceMode, float friction, PhysicMaterialCombine frictionMode)
	{
		Bounciness = bounciness;
		BounceMode = bounceMode;
		Friction = friction;
		FrictionMode = frictionMode;
	}

	public void AddMaterial(INPhysicMaterial other)
	{
		if (other.BounceMode > BounceMode || (other.BounceMode == BounceMode && other.Bounciness > Bounciness))
		{
			BounceMode = other.BounceMode;
			Bounciness = other.Bounciness;
		}
		if (other.FrictionMode > FrictionMode || (other.FrictionMode == FrictionMode && other.Friction > Friction))
		{
			FrictionMode = other.FrictionMode;
			Friction = other.Friction;
		}
	}

	public float CombineBounce(INPhysicMaterial other)
	{
		PhysicMaterialCombine mode = ((BounceMode > other.BounceMode) ? BounceMode : other.BounceMode);
		return CombineValue(mode, Bounciness, other.Bounciness);
	}

	public float CombineFriction(INPhysicMaterial other)
	{
		PhysicMaterialCombine mode = ((FrictionMode > other.FrictionMode) ? FrictionMode : other.FrictionMode);
		return CombineValue(mode, Friction, other.Friction);
	}

	private float CombineValue(PhysicMaterialCombine mode, float value1, float value2)
	{
		switch (mode)
		{
		case PhysicMaterialCombine.Average:
			return (value1 + value2) * 0.5f;
		case PhysicMaterialCombine.Minimum:
			if (!(value1 < value2))
			{
				return value2;
			}
			return value1;
		case PhysicMaterialCombine.Multiply:
			return value1 * value2;
		case PhysicMaterialCombine.Maximum:
			if (!(value1 > value2))
			{
				return value2;
			}
			return value1;
		default:
			return 0f;
		}
	}
}
