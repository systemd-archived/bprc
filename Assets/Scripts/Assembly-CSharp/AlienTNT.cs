using System.Collections;
using UnityEngine;

public class AlienTNT : TNT
{
	private float m_explodeTime;

	public override void Explode()
	{
		if (m_triggered || Time.time < m_explodeTime + INSettings.GetFloat(INFeature.AlienTNTExplosionCoolingTime))
		{
			return;
		}
		m_explodeTime = Time.time;
		m_triggered = true;
		Collider[] array = Physics.OverlapSphere(base.transform.position, m_explosionRadius * INSettings.GetFloat(INFeature.AlienTNTExplosionRadius));
		foreach (Collider collider in array)
		{
			GameObject gameObject = FindParentWithRigidBody(collider.gameObject);
			if (gameObject != null)
			{
				int num = CountChildColliders(gameObject, 0);
				AddExplosionForce(gameObject, INSettings.GetFloat(INFeature.AlienTNTExplosionForce) / (float)num);
			}
			BasePart component = collider.GetComponent<BasePart>();
			if (component is TNT tNT && !(component is AlienTNT) && !component.HasGeneratorRef && m_partTier == PartTier.Epic)
			{
				tNT.Explode();
			}
			if (INSettings.GetBool(INFeature.BlasterTNT) && component is BlasterTNT blasterTNT && !component.HasGeneratorRef && Vector.DistanceSquared2(base.transform.position, component.transform.position) < 4f)
			{
				blasterTNT.ExplodeSpecial();
			}
			if (INSettings.GetBool(INFeature.AlienTNTTriggerGun) && component is ExplodingGrapplingHook part && !part.IsAutoGun() && Vector.DistanceSquared2(collider.transform.position, base.transform.position + collider.transform.right) < 0.78f)
			{
				StartCoroutine(TouchPart(part, 0.2f));
			}
		}
		Singleton<AudioManager>.Instance.SpawnOneShotEffect(WPFMonoBehaviour.gameData.commonAudioCollection.tntExplosion, base.transform.position);
		WPFMonoBehaviour.effectManager.CreateParticles(smokeCloud, base.transform.position - Vector3.forward * 5f, force: true);
		if ((bool)extraEffect)
		{
			WPFMonoBehaviour.effectManager.CreateParticles(extraEffect, base.transform.position - Vector3.forward * 4f, force: true);
		}
		CheckForTNTAchievement();
		StartCoroutine(ShineLight());
	}

	public override void OnPhysicsCollisionEnter(PhysicsCollision c)
	{
	}

	protected new virtual void LateUpdate()
	{
		m_triggered = false;
	}

	protected override IEnumerator ShineLight()
	{
		PointLightSource pls = GetComponentInChildren<PointLightSource>();
		if ((bool)pls)
		{
			pls.isEnabled = true;
			yield return new WaitForSeconds(pls.turnOnCurve[pls.turnOnCurve.length - 1].time);
			pls.isEnabled = false;
		}
	}

	private IEnumerator TouchPart(BasePart part, float time)
	{
		yield return new WaitForSeconds(time);
		part.ProcessTouch();
	}
}
