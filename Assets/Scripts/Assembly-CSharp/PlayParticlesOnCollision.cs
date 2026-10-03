using System.Collections.Generic;
using UnityEngine;

public class PlayParticlesOnCollision : WPFMonoBehaviour
{
	public enum CollisionEvent
	{
		Enter = 0,
		Stay = 1,
		Exit = 2
	}

	public enum Action
	{
		PlayByEffectManager = 0,
		ShowEffectByEffectManager = 1,
		PlayParticleSystem = 2,
		Instantinate = 3,
		SpawnAudioOneShotEffect = 4
	}

	private bool m_enabled = true;

	private float m_LastPlayTime;

	public List<Action> m_ActionList = new List<Action>();

	public GameObject m_ParticlesPrefab;

	public AudioSource m_AudioOneShotEffect;

	public LayerMask m_CollisionLayerMask;

	public float m_Timeout;

	public bool m_PlayOnEachContact;

	public CollisionEvent m_CollisionEvent;

	public float m_MinRelativeSpeed;

	public float m_ZOffset;

	public bool Enabled
	{
		get
		{
			return m_enabled;
		}
		set
		{
			m_LastPlayTime = 0f;
			m_enabled = value;
		}
	}

	private bool HandleContactPoint(PhysicsContactPoint cp, PhysicsCollision coll)
	{
		bool result = false;
		if ((m_CollisionLayerMask.value & (1 << cp.otherCollider.gameObject.layer)) != 0)
		{
			if (Time.time - m_LastPlayTime >= m_Timeout && coll.relativeVelocity.magnitude > m_MinRelativeSpeed)
			{
				Vector3 point = cp.point;
				point.z += m_ZOffset;
				foreach (Action action in m_ActionList)
				{
					switch (action)
					{
					case Action.PlayByEffectManager:
						WPFMonoBehaviour.effectManager.CreateParticles(m_ParticlesPrefab, point);
						break;
					case Action.ShowEffectByEffectManager:
						WPFMonoBehaviour.effectManager.ShowBreakEffect(m_ParticlesPrefab, point, Quaternion.identity);
						break;
					case Action.PlayParticleSystem:
						m_ParticlesPrefab.GetComponent<ParticleSystem>().Play();
						break;
					case Action.Instantinate:
						Object.Instantiate(m_ParticlesPrefab, point, Quaternion.identity);
						break;
					case Action.SpawnAudioOneShotEffect:
						if ((bool)m_AudioOneShotEffect)
						{
							Singleton<AudioManager>.Instance.SpawnOneShotEffect(m_AudioOneShotEffect, point);
						}
						break;
					}
				}
				m_LastPlayTime = Time.time;
			}
			result = !m_PlayOnEachContact;
		}
		return result;
	}

	private void HandleCollisionEvent(CollisionEvent collEvent, PhysicsCollision coll)
	{
		if (!m_enabled || m_CollisionEvent != collEvent)
		{
			return;
		}
		PhysicsContactPoint[] contacts = coll.contacts;
		foreach (PhysicsContactPoint cp in contacts)
		{
			if (HandleContactPoint(cp, coll))
			{
				break;
			}
		}
	}

	public void OnCollisionEnter(UnityEngine.Collision coll) { OnPhysicsCollisionEnter(new PhysicsCollision(coll)); }

	public void OnPhysicsCollisionEnter(PhysicsCollision coll)
	{
		HandleCollisionEvent(CollisionEvent.Enter, coll);
	}

	public void OnCollisionStay(UnityEngine.Collision coll) { OnPhysicsCollisionStay(new PhysicsCollision(coll)); }

	public void OnPhysicsCollisionStay(PhysicsCollision coll)
	{
		HandleCollisionEvent(CollisionEvent.Stay, coll);
	}

	public void OnCollisionExit(UnityEngine.Collision coll) { OnPhysicsCollisionExit(new PhysicsCollision(coll)); }

	public void OnPhysicsCollisionExit(PhysicsCollision coll)
	{
		HandleCollisionEvent(CollisionEvent.Exit, coll);
	}
}
