using UnityEngine;

public static class JointExtensions
{
	public static SpringJoint ConfigureSpringJoint(this SpringJoint joint, float minDistance, float maxDistance, float spring, float damper)
	{
		joint.minDistance = minDistance;
		joint.maxDistance = maxDistance;
		joint.spring = spring;
		joint.damper = damper;
		return joint;
	}

	public static ConfigurableJoint ConfigurePrismaticJoint(this ConfigurableJoint joint, int axis, float limit, float limitSpring, float limitDamper)
	{
		joint.angularXMotion = ConfigurableJointMotion.Locked;
		joint.angularYMotion = ConfigurableJointMotion.Locked;
		joint.angularZMotion = ConfigurableJointMotion.Locked;
		joint.xMotion = ((axis == 0) ? ConfigurableJointMotion.Limited : ConfigurableJointMotion.Locked);
		joint.yMotion = ((axis == 1) ? ConfigurableJointMotion.Limited : ConfigurableJointMotion.Locked);
		joint.zMotion = ConfigurableJointMotion.Locked;
		joint.linearLimit = new SoftJointLimit
		{
			limit = limit
		};
		joint.linearLimitSpring = new SoftJointLimitSpring
		{
			spring = limitSpring,
			damper = limitDamper
		};
		return joint;
	}

	public static ConfigurableJoint ConfigureRevoluteJoint(this ConfigurableJoint joint, bool useLimits, float limit, float limitSpring, float limitDamper)
	{
		joint.angularXMotion = ConfigurableJointMotion.Locked;
		joint.angularYMotion = ConfigurableJointMotion.Locked;
		joint.angularZMotion = (useLimits ? ConfigurableJointMotion.Limited : ConfigurableJointMotion.Free);
		joint.xMotion = ConfigurableJointMotion.Locked;
		joint.yMotion = ConfigurableJointMotion.Locked;
		joint.zMotion = ConfigurableJointMotion.Locked;
		if (useLimits)
		{
			joint.angularZLimit = new SoftJointLimit
			{
				limit = limit
			};
			joint.angularYZLimitSpring = new SoftJointLimitSpring
			{
				spring = limitSpring,
				damper = limitDamper
			};
		}
		return joint;
	}
}
