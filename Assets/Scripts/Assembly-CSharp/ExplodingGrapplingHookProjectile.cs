using System;
using System.Collections;
using UnityEngine;

public class ExplodingGrapplingHookProjectile : WPFMonoBehaviour
{
	public Action OnExplosion;

	[SerializeField]
	private float m_explosionImpulse;

	[SerializeField]
	private float m_explosionRadius;

	[SerializeField]
	private float m_force;

	[SerializeField]
	private float m_ttl;

	[SerializeField]
	private GameObject m_smokeCloud;

	private bool m_triggered;

	private Renderer m_renderer;

	private Vector3 m_forceDirection;

	private int m_explosionCount;

	private TextMesh m_projText;

	private void Start()
	{
		m_triggered = false;
		m_explosionCount = INSettings.GetInt(INFeature.GunProjectileExplosionCount);
		m_renderer = GetComponentInChildren<Renderer>();
		EventManager.Connect<UIEvent>(OnUIEvent);
		m_forceDirection = base.transform.parent.TransformDirection(Vector3.right);
		base.rigidbody.EngineAddForceAtPosition(m_forceDirection * m_force * INSettings.GetFloat(INFeature.GunProjectileSpeed), Vector3.zero, ForceMode.Impulse);
		StartCoroutine(TTL(m_ttl * INSettings.GetFloat(INFeature.GunProjectileExplosionTime)));
		Collider[] componentsInChildren = base.GetComponentsInChildren<Collider>(true);
		if (componentsInChildren != null && componentsInChildren.Length != 0)
		{
			foreach (TNT tnt in UnityEngine.Object.FindObjectsOfType<TNT>())
			{
				if (!(tnt == null) && tnt.m_partTier == BasePart.PartTier.Epic)
				{
					foreach (Collider collider in tnt.GetComponentsInChildren<Collider>(true))
					{
						foreach (Collider collider2 in componentsInChildren)
						{
							if (collider != null && collider2 != null)
							{
								Physics.IgnoreCollision(collider2, collider, true);
							}
						}
					}
				}
			}
		}
		GameObject projTextObject = new GameObject("ProjText");
		projTextObject.transform.parent = base.transform;
		projTextObject.transform.localPosition = new Vector3(0f, 0f, -0.5f);
		projTextObject.transform.localRotation = Quaternion.identity;
		m_projText = projTextObject.AddComponent<TextMesh>();
		m_projText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
		m_projText.text = ">";
		m_projText.fontSize = 100;
		m_projText.characterSize = 0f;
		m_projText.anchor = TextAnchor.MiddleCenter;
		m_projText.alignment = TextAlignment.Center;
		m_projText.color = new Color(0f, 1f, 1f, 0.66f);
	}

	// 视野过大(相机拉远)时，让机炮子弹的 ">" 标记随正交尺寸等比缩放，从而始终保持可见
	private void Update()
	{
		if (m_projText != null)
		{
			Camera main = Camera.main;
			float orthographicSize = ((main != null) ? main.orthographicSize : 10f);
			m_projText.characterSize = orthographicSize * 0.0028f;
		}
	}

	private void OnDestroy()
	{
		EventManager.Disconnect<UIEvent>(OnUIEvent);
	}

	public void OnCollisionEnter(UnityEngine.Collision collision) { OnPhysicsCollisionEnter(new PhysicsCollision(collision)); }

	private void OnPhysicsCollisionEnter(PhysicsCollision collision)
	{
		Explode();
	}

	public void Explode()
	{
		if (m_triggered)
		{
			return;
		}
		m_explosionCount--;
		if (m_explosionCount <= 0)
		{
			m_triggered = true;
		}
		Collider[] array = Physics.OverlapSphere(base.transform.position, m_explosionRadius * INSettings.GetFloat(INFeature.GunProjectileExplosionRadius));
		foreach (Collider collider in array)
		{
			GameObject gameObject = FindParentWithRigidBody(collider.gameObject);
			if (gameObject != null)
			{
				int num = CountChildColliders(gameObject, 0);
				AddExplosionForce(gameObject, INSettings.GetFloat(INFeature.GunProjectileExplosionForce) / (float)num);
			}
			BasePart component = collider.GetComponent<BasePart>();
			if (component is TNT tNT && !component.HasGeneratorRef && tNT.m_partTier != BasePart.PartTier.Epic)
			{
				tNT.Explode();
			}
			if (INSettings.GetBool(INFeature.BlasterTNT) && component is BlasterTNT blasterTNT && !component.HasGeneratorRef && Vector.DistanceSquared2(base.transform.position, component.transform.position) < 4f)
			{
				blasterTNT.ExplodeSpecial();
			}
		}
		WPFMonoBehaviour.effectManager.CreateParticles(m_smokeCloud, base.transform.position - Vector3.forward * 12f, force: true);
		Singleton<AudioManager>.Instance.SpawnOneShotEffect(WPFMonoBehaviour.gameData.commonAudioCollection.tntExplosion, base.transform.position);
		StartCoroutine(ShineLight());
		if (OnExplosion != null)
		{
			OnExplosion();
			OnExplosion = null;
		}
	}

	private int CountChildColliders(GameObject obj, int count)
	{
		if ((bool)obj.GetComponent<Collider>())
		{
			count++;
		}
		for (int i = 0; i < obj.transform.childCount; i++)
		{
			count = CountChildColliders(obj.transform.GetChild(i).gameObject, count);
		}
		return count;
	}

	private GameObject FindParentWithRigidBody(GameObject obj)
	{
		if ((bool)obj.GetComponent<Rigidbody>())
		{
			return obj;
		}
		if ((bool)obj.transform.parent)
		{
			return FindParentWithRigidBody(obj.transform.parent.gameObject);
		}
		return null;
	}

	private void AddExplosionForce(GameObject target, float forceFactor)
	{
		Vector3 vector = target.transform.position - base.transform.position;
		float f = Mathf.Max(vector.magnitude, 1f);
		float num = forceFactor * m_explosionImpulse / Mathf.Pow(f, 1.5f);
		Rigidbody component = target.GetComponent<Rigidbody>();
		if (component.mass < 0.1f)
		{
			num *= component.mass;
		}
		else if (component.mass < 0.4f)
		{
			num *= component.mass / 0.4f;
		}
		component.EngineAddForce(num * vector.normalized, ForceMode.Impulse);
	}

	private IEnumerator ShineLight()
	{
		PointLightSource pls = GetComponentInChildren<PointLightSource>();
		if ((bool)pls)
		{
			if (m_renderer != null)
			{
				m_renderer.enabled = false;
			}
			pls.onLightTurnOff = (Action)Delegate.Combine(pls.onLightTurnOff, (Action)delegate
			{
				base.gameObject.SetActive(value: false);
			});
			pls.isEnabled = true;
			yield return new WaitForSeconds(pls.turnOnCurve[pls.turnOnCurve.length - 1].time);
			pls.isEnabled = false;
		}
		if (m_explosionCount <= 0)
		{
			UnityEngine.Object.Destroy(base.gameObject);
		}
	}

	private IEnumerator TTL(float ttl)
	{
		float current = 0f;
		float coolingTime = INSettings.GetFloat(INFeature.GunProjectileCoolingTime);
		while (current < ttl)
		{
			if (OnExplosion != null && current >= coolingTime)
			{
				OnExplosion();
				OnExplosion = null;
			}
			current += Time.deltaTime;
			yield return null;
		}
		while (!m_triggered)
		{
			Explode();
		}
	}

	private void OnUIEvent(UIEvent data)
	{
		if (data.type == UIEvent.Type.Building)
		{
			UnityEngine.Object.Destroy(base.gameObject);
		}
	}
}
