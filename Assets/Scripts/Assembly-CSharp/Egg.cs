using System.Collections.Generic;
using UnityEngine;

public class Egg : BasePart
{
	private bool m_enabled;

	private bool m_isSpecialEgg;

	private List<Collider> m_colliders;

	private Dictionary<Rigidbody, bool> m_rigidBodyData;

	private int m_safetyPressCount;

	private float m_safetyTimer;

	public override void CollectExtraParams(List<string> keys)
	{
		base.CollectExtraParams(keys);
		if (customPartIndex != 1) return;
		keys.Add("GravityEggMass");
		keys.Add("GravityEggForce");
		keys.Add("GravityEggInnerRadius");
		keys.Add("GravityEggOuterRadius");
	}

	public override bool TryGetExtraParamValue(string key, out float value)
	{
		INFeature feature;
		if (customPartIndex == 1 && System.Enum.TryParse(key, out feature) &&
			feature >= INFeature.GravityEggMass && feature <= INFeature.GravityEggOuterRadius)
		{
			value = GetCustomParam(key, INSettings.GetFloat(feature));
			return true;
		}
		return base.TryGetExtraParamValue(key, out value);
	}

	public override bool CanBeEnclosed()
	{
		return true;
	}

	public override void Awake()
	{
		base.Awake();
		if (customPartIndex == 1)
		{
			m_jointConnectionStrength = JointConnectionStrength.HighlyExtreme;
		}
	}

	public override void Initialize()
	{
		base.Initialize();
		int num = customPartIndex;
		m_isSpecialEgg = num >= 1 && num <= 4;
	}

	protected override void OnTouch()
	{
		if (!INSettings.GetBool(INFeature.SpecialEggs) || !m_isSpecialEgg)
		{
			return;
		}
		if (customPartIndex != 1 || (m_gridRotation != BasePart.GridRotation.Deg_45 && m_gridRotation != BasePart.GridRotation.Deg_135 && m_gridRotation != BasePart.GridRotation.Deg_225 && m_gridRotation != BasePart.GridRotation.Deg_315))
		{
			SetEnabled(!m_enabled);
			return;
		}
		if (!m_enabled)
		{
			if (Time.time - m_safetyTimer > 1f)
			{
				m_safetyPressCount = 0;
			}
			m_safetyTimer = Time.time;
			m_safetyPressCount++;
			Singleton<AudioManager>.Instance.SpawnOneShotEffect(WPFMonoBehaviour.gameData.commonAudioCollection.tntExplosion, base.transform.position);
			WPFMonoBehaviour.effectManager.CreateParticles(WPFMonoBehaviour.gameData.m_dustParticles, base.transform.position - Vector3.forward * 5f, force: true);
			if (m_safetyPressCount >= 3)
			{
				m_safetyPressCount = 0;
				SetEnabled(true);
			}
			return;
		}
		SetEnabled(false);
	}

	public override void SetEnabled(bool enabled)
	{
		if (!INSettings.GetBool(INFeature.SpecialEggs) || !m_isSpecialEgg || m_enabled == enabled)
		{
			return;
		}
		m_enabled = enabled;
		switch (customPartIndex)
		{
		case 2:
			if (m_enabled)
			{
				m_rigidBodyData = new Dictionary<Rigidbody, bool>();
				{
					foreach (BasePart part in base.contraption.Parts)
					{
						if (part.ConnectedComponent != base.ConnectedComponent)
						{
							continue;
						}
						foreach (Rigidbody rigidbody in part.GetRigidbodies())
						{
							m_rigidBodyData.Add(rigidbody, rigidbody.useGravity);
							rigidbody.useGravity = false;
						}
					}
					break;
				}
			}
			foreach (KeyValuePair<Rigidbody, bool> rigidBodyDatum in m_rigidBodyData)
			{
				Rigidbody key = rigidBodyDatum.Key;
				if (key != null)
				{
					key.useGravity = rigidBodyDatum.Value;
				}
			}
			m_rigidBodyData = null;
			break;
		case 3:
		{
			if (!m_enabled)
			{
				foreach (Collider collider2 in m_colliders)
				{
					if (collider2 != null)
					{
						collider2.enabled = true;
					}
				}
				break;
			}
			m_colliders = new List<Collider>();
			Collider[] components = INContraption.Instance.GetComponents<Collider>();
			foreach (Collider collider in components)
			{
				if (collider.attachedRigidbody != null)
				{
					BasePart component = collider.attachedRigidbody.GetComponent<BasePart>();
					if (component != null && component.ConnectedComponent == base.ConnectedComponent)
					{
						collider.enabled = false;
						m_colliders.Add(collider);
					}
				}
			}
			break;
		}
		case 4:
			if (!m_enabled)
			{
				foreach (Collider collider3 in m_colliders)
				{
					if (collider3 != null)
					{
						collider3.enabled = true;
					}
				}
				break;
			}
			m_colliders = new List<Collider>();
			break;
		}
	}

	private void FixedUpdate()
	{
		if (!INSettings.GetBool(INFeature.SpecialEggs) || !m_isSpecialEgg || !base.contraption || !base.contraption.IsRunning || !m_enabled)
		{
			return;
		}
		switch (customPartIndex)
		{
		case 1:
		{
			Rigidbody rigidbody = base.rigidbody;
			if (rigidbody == null || INContraption.Instance == null) break;
			float mass = rigidbody.mass;
			Vector3 position4 = rigidbody.position;
			float num5 = GetCustomParam("GravityEggForce", INSettings.GetFloat(INFeature.GravityEggForce));
			float num6 = GetCustomParam("GravityEggInnerRadius", INSettings.GetFloat(INFeature.GravityEggInnerRadius));
			float num7 = GetCustomParam("GravityEggOuterRadius", INSettings.GetFloat(INFeature.GravityEggOuterRadius));
			if (!BuildMachineTransform.Finite(num5) || float.IsNaN(num7) || num7 <= 0f) break;
			num6 = BuildMachineTransform.Finite(num6) ? Mathf.Max(0.001f, num6) : 0.001f;
			float outerSquared = num7 * num7;
			Rigidbody[] components2 = INContraption.Instance.Rigidbodies;
			foreach (Rigidbody rigidbody2 in components2)
			{
				if (rigidbody2 == null || rigidbody2 == rigidbody) continue;
				Vector3 position5 = rigidbody2.position;
				float num8 = position4.x - position5.x;
				float num9 = position4.y - position5.y;
				float squaredDistance = num8 * num8 + num9 * num9;
				if (squaredDistance > 0.00000001f && squaredDistance < outerSquared)
				{
					float num10 = Mathf.Max(Mathf.Sqrt(squaredDistance), num6);
					float num11 = num5 / (num10 * num10 * num10) * mass * rigidbody2.mass;
					if (!BuildMachineTransform.Finite(num11)) continue;
					rigidbody2.EngineAddForce(new Vector3(num11 * num8, num11 * num9), ForceMode.Force);
					Egg component = rigidbody2.GetComponent<Egg>();
					if (component == null || component.customPartIndex != 1 || !component.IsEnabled())
					{
						rigidbody.EngineAddForce(new Vector3((0f - num11) * num8, (0f - num11) * num9), ForceMode.Force);
					}
				}
			}
			break;
		}
		case 4:
		{
			Vector3 position = base.transform.position;
			foreach (Collider collider2 in m_colliders)
			{
				if (collider2 != null)
				{
					Vector3 position2 = collider2.transform.position;
					float num = position2.x - position.x;
					float num2 = position2.y - position.y;
					if (num * num + num2 * num2 >= 64f)
					{
						collider2.enabled = true;
					}
				}
			}
			m_colliders.Clear();
			Collider[] components = INContraption.Instance.GetComponents<Collider>();
			foreach (Collider collider in components)
			{
				Vector3 position3 = collider.transform.position;
				float num3 = position3.x - position.x;
				float num4 = position3.y - position.y;
				if (num3 * num3 + num4 * num4 < 64f)
				{
					collider.enabled = false;
					m_colliders.Add(collider);
				}
			}
			break;
		}
		}
	}

	public override void PostInitialize()
	{
		if (INSettings.GetBool(INFeature.SpecialEggs) && m_isSpecialEgg)
		{
			int num = customPartIndex;
			if (num == 1)
			{
				base.contraption.ChangeOneShotPartAmount(m_partType, EffectDirection(), 1);
				float mass = GetCustomParam("GravityEggMass", INSettings.GetFloat(INFeature.GravityEggMass));
				if (BuildMachineTransform.Finite(mass) && mass > 0f) base.rigidbody.mass = mass;
			}
			if (num == 2 || num == 3 || num == 4)
			{
				base.contraption.ChangeOneShotPartAmount(m_partType, EffectDirection(), 1);
				OnTouch();
			}
		}
	}

	public override bool IsEnabled()
	{
		if (INSettings.GetBool(INFeature.SpecialEggs))
		{
			return m_enabled;
		}
		return false;
	}
}
