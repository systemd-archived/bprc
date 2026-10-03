using System;
using System.Collections;
using System.Collections.Generic;
using BadPiggies.Multiplayer;
using UnityEngine;

public class Rocket : BasePropulsion
{
	public Vector3 m_direction = Vector3.up;

	public bool m_enabled;

	public bool m_explodes;

	public float m_boostForce = 10f;

	public float m_ignitionTime = 1f;

	public float m_boostDuration = 2f;

	public float m_boostEndDuration = 0.5f;

	public float m_maximumSpeed;

	public float m_explosionImpulse;

	public float m_explosionRadius;

	public float m_loopAudioTimeLimit;

	public Transform m_particlesIgnition;

	public Transform m_particlesFiring;

	public Transform m_particlesSmoke;

	public AudioSource m_loopAudio;

	public AudioSource m_launchAudio;

	public AudioSource m_shakeAudio;

	public GameObject m_smokeCloud;

	protected bool m_boostUsed;

	protected Vector3 m_origScale;

	protected float m_timeBoostStarted;

	protected float m_currentScale;

	protected bool m_firedRocket;

	public ParticleSystem m_particlesIgnitionInstance;

	public ParticleSystem m_particlesFiringInstance;

	private GameObject m_leftAttachment;

	private GameObject m_rightAttachment;

	private GameObject m_topAttachment;

	private GameObject m_bottomAttachment;

	private GameObject m_visualization;

	private GameObject m_loopAudioObject;

	private AudioSource m_launchAudioInstance;

	public Renderer m_content;

	public Renderer m_content2;

	private float m_currentAlpha;

	private float m_currentAlpha2;

	private PointLightSource pls;

	private BasePart m_targetPart;

	private float m_angle;

	private float m_explodeTime;

	// 军事版功能：AP 火箭（Rocket index 2）撞击后的起爆倒计时状态，见 INMilitaryFeatures。
	private bool m_apRocketTimering;

	private float m_apRocketBombTimer;

	public override bool IsEnabled()
	{
		if (INSettings.GetBool(INFeature.SwitchableCokeSodaRocket))
		{
			return m_enabled;
		}
		return Time.time - m_timeBoostStarted < m_ignitionTime + m_boostDuration + m_boostEndDuration;
	}

	public override Direction EffectDirection()
	{
		return BasePart.Rotate(Direction.Right, m_gridRotation);
	}

	public override void Awake()
	{
		base.Awake();
		m_leftAttachment = base.transform.Find("LeftAttachment").gameObject;
		m_rightAttachment = base.transform.Find("RightAttachment").gameObject;
		m_topAttachment = base.transform.Find("TopAttachment").gameObject;
		m_bottomAttachment = base.transform.Find("BottomAttachment").gameObject;
		m_leftAttachment.SetActive(value: false);
		m_rightAttachment.SetActive(value: false);
		m_topAttachment.SetActive(value: false);
		m_bottomAttachment.SetActive(value: false);
		Transform transform = base.transform.Find("BottleVisualization");
		if ((bool)transform)
		{
			m_visualization = transform.gameObject;
		}
		m_origScale = base.transform.localScale;
		m_timeBoostStarted = -1000f;
		m_boostUsed = false;
		m_currentAlpha = 1f;
		m_currentAlpha2 = 1f;
		m_enabled = false;
		if ((bool)m_content2)
		{
			m_content2.material.color = new Color(0f, 0f, 0f, 0f);
		}
		// 军事版功能：单件 AP 火箭的碰撞体被压扁，撞击时更容易“插”进目标（军事版 Rocket.cs:134）。
		// IsSinglePart 要看零件所属装置的连通分量，所以必须先确认零件真的在某个装置里：部件工厂给零件列表
		// 造模板时是没有装置的，那时 part.contraption 为 null，直接调用会空引用并中断工厂的建立。
		if (INMilitaryFeatures.Enabled && this.IsAPRocket() && contraption != null && this.IsSinglePart())
		{
			BoxCollider apRocketCollider = GetComponent<BoxCollider>();
			if (apRocketCollider != null)
			{
				apRocketCollider.size = new Vector3(0.5f, 0.01f, 0.5f);
			}
		}
	}

	private void OnDestroy()
	{
		if ((bool)m_loopAudioObject)
		{
			Singleton<AudioManager>.Instance.StopLoopingEffect(m_loopAudioObject.GetComponent<AudioSource>());
			UnityEngine.Object.Destroy(m_loopAudioObject);
		}
	}

	public override void ChangeVisualConnections()
	{
		if (INSettings.GetBool(INFeature.EnclosableParts) && m_enclosedInto != null)
		{
			m_leftAttachment.SetActive(value: false);
			m_rightAttachment.SetActive(value: false);
			m_topAttachment.SetActive(value: false);
			m_bottomAttachment.SetActive(value: false);
			return;
		}
		bool flag = base.contraption.CanConnectTo(this, BasePart.Rotate(Direction.Up, m_gridRotation));
		bool flag2 = base.contraption.CanConnectTo(this, BasePart.Rotate(Direction.Down, m_gridRotation));
		bool flag3 = base.contraption.CanConnectTo(this, BasePart.Rotate(Direction.Left, m_gridRotation));
		bool flag4 = base.contraption.CanConnectTo(this, BasePart.Rotate(Direction.Right, m_gridRotation));
		m_leftAttachment.SetActive(flag3);
		m_rightAttachment.SetActive(flag4);
		m_topAttachment.SetActive(flag);
		m_bottomAttachment.SetActive(flag2 || (!flag && !flag3 && !flag4));
		// 军事版功能：AP 火箭的连接口碰撞体在连接可用时反而是触发器，避免连接时被自己的连接口撞开
		// （军事版 Rocket.cs:167-173）。
		if (INMilitaryFeatures.Enabled && this.IsAPRocket())
		{
			m_leftAttachment.GetComponent<BoxCollider>().isTrigger = m_leftAttachment.activeInHierarchy;
			m_rightAttachment.GetComponent<BoxCollider>().isTrigger = m_rightAttachment.activeInHierarchy;
			m_topAttachment.GetComponent<BoxCollider>().isTrigger = m_topAttachment.activeInHierarchy;
			m_bottomAttachment.GetComponent<BoxCollider>().isTrigger = m_bottomAttachment.activeInHierarchy;
			return;
		}
		m_leftAttachment.GetComponent<BoxCollider>().isTrigger = !m_leftAttachment.activeInHierarchy;
		m_rightAttachment.GetComponent<BoxCollider>().isTrigger = !m_rightAttachment.activeInHierarchy;
		m_topAttachment.GetComponent<BoxCollider>().isTrigger = !m_topAttachment.activeInHierarchy;
		m_bottomAttachment.GetComponent<BoxCollider>().isTrigger = !m_bottomAttachment.activeInHierarchy;
	}

	public override GridRotation AutoAlignRotation(JointConnectionDirection target)
	{
		return target switch
		{
			JointConnectionDirection.Right => GridRotation.Deg_90, 
			JointConnectionDirection.Up => GridRotation.Deg_0, 
			JointConnectionDirection.Left => GridRotation.Deg_90, 
			JointConnectionDirection.Down => GridRotation.Deg_0, 
			_ => GridRotation.Deg_0, 
		};
	}

	public override void Initialize()
	{
		base.contraption.ChangeOneShotPartAmount(m_partType, EffectDirection(), 1);
		if (m_partType == PartType.CokeBottle && m_partTier == PartTier.Legendary)
		{
			m_boostForce = INSettings.GetFloat(INFeature.AlienCokeForceValue);
			m_maximumSpeed = INSettings.GetFloat(INFeature.AlienCokeSpeedValue);
		}
		else if (m_partType == PartType.SodaBottle && m_partTier == PartTier.Legendary)
		{
			m_boostForce = INSettings.GetFloat(INFeature.AlienSodaForceValue);
			m_maximumSpeed = INSettings.GetFloat(INFeature.AlienSodaSpeedValue);
		}
		else if (m_partType == PartType.CokeBottle || m_partType == PartType.SodaBottle)
		{
			m_boostForce *= INSettings.GetFloat(INFeature.CokeSodaForce);
			m_maximumSpeed *= INSettings.GetFloat(INFeature.CokeSodaSpeed);
		}
		else if (m_partType == PartType.Rocket || m_partType == PartType.RedRocket)
		{
			m_boostForce *= INSettings.GetFloat(INFeature.RocketForce);
			m_maximumSpeed *= INSettings.GetFloat(INFeature.RocketSpeed);
		}
	}

	public void Update()
	{
		// 军事版功能：AP 火箭撞击后 1 秒内起爆，倒计时过半时碰撞体涨回正常尺寸（军事版 Rocket.cs:222-234）。
		if (INMilitaryFeatures.Enabled && this.IsAPRocket() && m_apRocketTimering)
		{
			m_apRocketBombTimer += Time.deltaTime;
			if (m_apRocketBombTimer <= 1f)
			{
				if (m_apRocketBombTimer > 0.2f)
				{
					BoxCollider apRocketCollider = GetComponent<BoxCollider>();
					if (apRocketCollider != null)
					{
						apRocketCollider.size = new Vector3(0.5f, 0.2f, 0.5f);
					}
				}
				return;
			}
			Explode();
		}
		float num = Time.time - m_timeBoostStarted;
		if (m_boostUsed && (bool)m_content && m_currentAlpha > 0f)
		{
			m_currentAlpha -= Time.deltaTime;
			if (m_currentAlpha < 0f)
			{
				m_currentAlpha = 0f;
			}
			m_content.material.color = new Color(m_currentAlpha, m_currentAlpha, m_currentAlpha, m_currentAlpha);
			if ((bool)m_content2)
			{
				m_content2.material.color = new Color(1f - m_currentAlpha, 1f - m_currentAlpha, 1f - m_currentAlpha, 1f - m_currentAlpha);
			}
		}
		if (!(num < m_ignitionTime) && m_boostUsed && (bool)m_content2 && m_currentAlpha2 > 0f)
		{
			m_currentAlpha2 -= Time.deltaTime;
			if (m_currentAlpha2 < 0f)
			{
				m_currentAlpha2 = 0f;
			}
			Color color = new Color(m_currentAlpha2, m_currentAlpha2, m_currentAlpha2, m_currentAlpha2);
			m_content2.material.color = color;
		}
	}

	public void FixedUpdate()
	{
		bool flag = INSettings.GetBool(INFeature.SwitchableCokeSodaRocket);
		if (!m_enabled)
		{
			m_targetPart = null;
			m_angle = float.NaN;
			return;
		}
		float num = Time.time - m_timeBoostStarted;
		if (num < m_ignitionTime && m_visualization != null)
		{
			m_visualization.transform.localPosition = UnityEngine.Random.insideUnitCircle * 0.1f;
			if (!flag)
			{
				return;
			}
		}
		float num2 = 1f;
		if (num > m_ignitionTime)
		{
			if (m_boostUsed)
			{
				if (m_particlesIgnitionInstance != null && m_particlesIgnitionInstance.isPlaying)
				{
					m_particlesIgnitionInstance.Stop();
				}
				if (m_visualization != null)
				{
					m_visualization.transform.localPosition = Vector3.zero;
					Transform transform = m_visualization.transform.Find("Cork");
					if (transform != null)
					{
						transform.parent = base.transform.parent;
						transform.GetComponent<Cork>().Fly(-20f * base.transform.right, 200f, 0.75f);
					}
				}
			}
			if (flag || num < m_ignitionTime + m_boostDuration + m_boostEndDuration)
			{
				if (!m_particlesFiringInstance.isPlaying)
				{
					m_particlesFiringInstance.Play();
				}
			}
			else if (m_particlesFiringInstance != null)
			{
				m_particlesFiringInstance.Stop();
			}
		}
		if (!flag && num > m_ignitionTime + m_boostDuration + m_boostEndDuration)
		{
			if (m_explodes)
			{
				Explode();
			}
			UnityEngine.Object.Destroy(m_particlesIgnitionInstance.gameObject);
			UnityEngine.Object.Destroy(m_particlesFiringInstance.gameObject);
			m_enabled = false;
		}
		if (!flag && num > m_ignitionTime + m_boostDuration)
		{
			num2 = 1f - (num - m_boostDuration - m_ignitionTime) / m_boostEndDuration;
		}
		bool num3 = INSettings.GetBool(INFeature.AvoidanceRocket) && this.IsAvoidanceRocket() && !this.IsSinglePart();
		bool flag2 = INSettings.GetBool(INFeature.TrackingRocket) && this.IsTrackingRocket() && !this.IsSinglePart();
		if (!num3 && !flag2)
		{
			Vector3 zero = Vector3.zero;
			Vector3 position = base.transform.position + zero * 0.5f;
			Vector3 vector = base.transform.TransformDirection(m_direction);
			float num4 = LimitForceForSpeed(num2 * m_boostForce * Mathf.Max(0f, GetCustomParam(PartPhysicsParameters.PowerKey, 1f)), vector);
			base.rigidbody.EngineAddForceAtPosition(num4 * vector, position, ForceMode.Force);
		}
		else
		{
			INFixedUpdate();
		}
	}

	private void INFixedUpdate()
	{
		bool num = INSettings.GetBool(INFeature.AvoidanceRocket) && this.IsAvoidanceRocket() && !this.IsSinglePart();
		bool flag = INSettings.GetBool(INFeature.TrackingRocket) && this.IsTrackingRocket() && !this.IsSinglePart();
		if (num)
		{
			float num2 = INSettings.GetFloat(INFeature.AvoidanceRocketAngle);
			float maxDistance = INSettings.GetFloat(INFeature.AvoidanceRocketMaxDistance);
			float num3 = 400f;
			float num4 = 2f;
			Vector3 direction = base.transform.TransformDirection(m_direction);
			if (customPartIndex == 1)
			{
				if (RaycastWithParts(base.transform.position, direction, out var hitInfo, maxDistance))
				{
					Vector3 velocity = base.rigidbody.velocity;
					if (hitInfo.rigidbody != null)
					{
						velocity -= hitInfo.rigidbody.velocity;
					}
					float num5 = velocity.x * direction.x + velocity.y * direction.y + 8f;
					if (num5 > 0f)
					{
						float num6 = ((hitInfo.distance > 4f) ? (hitInfo.distance - 3f) : 1f);
						float num7 = num6 / num5;
						if (num7 < num4)
						{
							float num8 = ((num7 < num4 * 0.5f) ? (num5 * num5 / (2f * num6)) : (-2f * num6 / (num4 * num4) + 2f * num5 / num4));
							num8 *= 8f;
							num8 = 0f - ((num8 < num3) ? num8 : num3);
							base.rigidbody.EngineAddForce(new Vector3(num8 * direction.x, num8 * direction.y), ForceMode.Force);
						}
					}
				}
			}
			else
			{
				int num9 = 6;
				bool[] array = new bool[num9];
				float[] array2 = new float[num9];
				Vector2[] array3 = new Vector2[num9];
				float num10 = -0.5f * num2;
				for (int i = 0; i < num9; i++)
				{
					float f = num10 * (MathF.PI / 180f);
					float num11 = Mathf.Cos(f);
					float num12 = Mathf.Sin(f);
					Vector2 vector = new Vector2(direction.x * num11 - direction.y * num12, direction.x * num12 + direction.y * num11);
					RaycastHit hitInfo2;
					bool flag2 = (array[i] = RaycastWithParts(base.transform.position, vector, out hitInfo2, maxDistance));
					array3[i] = vector;
					array2[i] = (flag2 ? hitInfo2.distance : float.PositiveInfinity);
					num10 += num2 / (float)(num9 - 1);
				}
				int num13 = -1;
				float num14 = float.PositiveInfinity;
				for (int j = 0; j < num9; j++)
				{
					if (array[j] && array2[j] < num14)
					{
						num13 = j;
						num14 = array2[j];
					}
				}
				if (num13 != -1)
				{
					Vector3 velocity2 = base.rigidbody.velocity;
					Vector2 vector2 = array3[num13];
					float num15 = velocity2.x * vector2.x + velocity2.y * vector2.y + 8f;
					if (num15 > 0f)
					{
						float num16 = ((num13 < num9 / 2) ? 1f : (-1f));
						float num17 = ((num14 > 4f) ? (num14 - 3f) : 1f) / num15;
						if (num17 < num4)
						{
							float num18 = ((num17 < num4 * 0.5f) ? (1f / num17) : (-4f * num17 / (num4 * num4) + 4f / num4));
							num18 *= 150f;
							num18 = ((num18 < 400f) ? num18 : 400f) * num16;
							base.rigidbody.EngineAddForce(new Vector3(num18 * (0f - direction.y), num18 * direction.x), ForceMode.Force);
						}
					}
				}
			}
		}
		if (!flag)
		{
			return;
		}
		float f2 = INSettings.GetFloat(INFeature.TrackingRocketAngle) * 0.5f * (MathF.PI / 180f);
		float num19 = INSettings.GetFloat(INFeature.TrackingRocketMaxForce);
		float num20 = INSettings.GetFloat(INFeature.TrackingRocketMaxDistance);
		Vector3 position = base.transform.position;
		Vector3 velocity3 = base.rigidbody.velocity;
		Vector3 vector3 = base.transform.TransformDirection(m_direction);
		float num21 = ((m_targetPart == null) ? 0f : (CalculatePartPriority(m_targetPart) * 1.5f));
		float num22 = 0f;
		float num23 = Mathf.Cos(f2);
		BasePart basePart = null;
		int num24 = LayerMask.NameToLayer("Ground");
		foreach (BasePart part in base.contraption.Parts)
		{
			bool flag3 = false;
			if (!MarkerManager.IsInSameTeamStatic(this, part) && part.m_partType != PartType.Rope && part.gameObject.layer != num24)
			{
				Vector3 vector4 = part.transform.position - position;
				if (part.HasMultipleRigidbodies())
				{
					vector4 = part.Position - position;
				}
				vector4.z = 0f;
				float magnitude = vector4.magnitude;
				if (magnitude < num20 && vector4.x * vector3.x + vector4.y * vector3.y > num23 * magnitude)
				{
					float num25 = CalculatePartPriority(part);
					if (num25 > num22 && num25 > num21)
					{
						num22 = num25;
						basePart = part;
					}
					flag3 = true;
				}
			}
			if (!flag3 && part == m_targetPart)
			{
				m_targetPart = null;
			}
		}
		if (basePart != null && basePart != m_targetPart)
		{
			m_targetPart = basePart;
			m_angle = float.NaN;
		}
		if (!(m_targetPart != null))
		{
			return;
		}
		if (customPartIndex == 1)
		{
			Vector3 vector5 = m_targetPart.rigidbody.velocity - velocity3;
			vector5.z = 0f;
			Vector3 vector6 = m_targetPart.transform.position - position;
			vector6.z = 0f;
			float magnitude2 = vector6.magnitude;
			magnitude2 = ((magnitude2 > 1f) ? magnitude2 : 1f);
			Vector3 force = 8f * vector5 + 16f * ((magnitude2 > 16f) ? 1f : (16f / magnitude2)) * vector6;
			float magnitude3 = force.magnitude;
			if (magnitude3 > num19)
			{
				force *= num19 / magnitude3;
			}
			base.rigidbody.EngineAddForce(force, ForceMode.Force);
			return;
		}
		float num26 = MathF.PI;
		Vector3 vector7 = m_targetPart.rigidbody.velocity - velocity3;
		vector7.z = 0f;
		Vector3 vector8 = m_targetPart.transform.position - position;
		vector8.z = 0f;
		Vector3 vector9 = vector7 + 2f * vector8;
		float x = vector3.x * vector9.x + vector3.y * vector9.y;
		float num27 = Mathf.Atan2(vector3.x * vector9.y - vector3.y * vector9.x, x);
		if (!float.IsNaN(m_angle))
		{
			float num28 = num27 - m_angle;
			num28 = ((num28 < 0f - num26) ? (num28 + 2f * num26) : ((num28 > num26) ? (num28 - 2f * num26) : num28));
			float num29 = num28 * 1600f + num27 * 400f;
			num29 = ((num29 < 0f - num19) ? (0f - num19) : ((num29 > num19) ? num19 : num29));
			base.rigidbody.EngineAddForce(new Vector3(num29 * (0f - vector3.y), num29 * vector3.x), ForceMode.Force);
		}
		m_angle = num27;
	}

	public bool RaycastWithParts(Vector3 origin, Vector3 direction, out RaycastHit hitInfo, float maxDistance)
	{
		int num = (1 << LayerMask.NameToLayer("Ground")) | (1 << LayerMask.NameToLayer("IceGround"));
		if (!(m_enclosedInto != null) || m_enclosedInto.IsTransparentFrame())
		{
			return Physics.Raycast(origin, direction, out hitInfo, maxDistance, num);
		}
		int num2 = MultiplayerLayers.ContraptionLayerMask;
		num |= num2;
		RaycastHit[] array = Physics.RaycastAll(origin, direction, maxDistance, num);
		int num3 = -1;
		float num4 = float.PositiveInfinity;
		for (int i = 0; i < array.Length; i++)
		{
			RaycastHit raycastHit = array[i];
			if (!(raycastHit.distance < num4))
			{
				continue;
			}
			Rigidbody rigidbody = raycastHit.rigidbody;
			if (rigidbody != null && (num2 & (1 << rigidbody.gameObject.layer)) != 0)
			{
				BasePart component = rigidbody.GetComponent<BasePart>();
				if (component == null || component.ConnectedComponent == base.ConnectedComponent || !MarkerManager.IsInSameTeamStatic(component, this))
				{
					continue;
				}
			}
			num4 = raycastHit.distance;
			num3 = i;
		}
		if (num3 == -1)
		{
			hitInfo = default(RaycastHit);
			return false;
		}
		hitInfo = array[num3];
		return true;
	}

	private float CalculatePartPriority(BasePart part)
	{
		Vector3 position = base.transform.position;
		Vector3 position2 = part.transform.position;
		float magnitude = new Vector2(position2.x - position.x, position2.y - position.y).magnitude;
		magnitude = ((magnitude > 1f) ? magnitude : 1f);
		return (float)base.contraption.ComponentPartCount(part.ConnectedComponent) / magnitude;
	}

	private float LimitForceForSpeed(float forceMagnitude, Vector3 forceDir)
	{
		Vector3 velocity = base.rigidbody.velocity;
		float num = Vector3.Dot(velocity.normalized, forceDir);
		if (num > 0f)
		{
			Vector3 vector = velocity * num;
			float maximumSpeed = m_maximumSpeed * Mathf.Max(0f, GetCustomParam(PartPhysicsParameters.PowerKey, 1f));
			if (vector.magnitude > maximumSpeed)
			{
				return forceMagnitude / (1f + vector.magnitude - maximumSpeed);
			}
		}
		return forceMagnitude;
	}

	protected override void OnTouch()
	{
		if (INSettings.GetBool(INFeature.SwitchableCokeSodaRocket))
		{
			m_enabled = !m_enabled;
			m_boostUsed = !m_boostUsed;
			if (!m_enabled)
			{
				m_particlesIgnitionInstance.Stop();
				m_particlesFiringInstance.Stop();
				m_currentAlpha = 1f;
				m_currentAlpha2 = 1f;
				m_targetPart = null;
				if (m_launchAudioInstance != null)
				{
					UnityEngine.Object.Destroy(m_launchAudioInstance.gameObject);
				}
				return;
			}
			m_particlesIgnitionInstance.Play();
			m_timeBoostStarted = Time.time;
			if (m_explodes)
			{
				Explode();
			}
		}
		else
		{
			if (m_boostUsed)
			{
				return;
			}
			m_enabled = !m_enabled;
			m_particlesIgnitionInstance.Play();
			m_timeBoostStarted = Time.time;
			m_boostUsed = true;
			base.contraption.ChangeOneShotPartAmount(m_partType, EffectDirection(), -1);
		}
		if (m_loopAudio != null)
		{
			float length;
			if (m_shakeAudio != null)
			{
				length = m_shakeAudio.clip.length;
				Singleton<AudioManager>.Instance.SpawnOneShotEffect(m_shakeAudio, base.transform);
			}
			else
			{
				length = m_launchAudio.clip.length;
			}
			StartCoroutine(CoroutineRunner.DelayActionSequence(delegate
			{
				m_loopAudioObject = Singleton<AudioManager>.Instance.SpawnLoopingEffect(m_loopAudio, base.transform);
				AudioSource loopAudioSource = m_loopAudioObject.GetComponent<AudioSource>();
				float originalVolume = loopAudioSource.volume;
				if (m_loopAudioTimeLimit > 0f)
				{
					StartCoroutine(CoroutineRunner.DeltaAction(m_loopAudioTimeLimit, realTime: false, delegate(float t)
					{
						loopAudioSource.volume = originalVolume * t;
					}));
				}
			}, length, realTime: false));
		}
		else
		{
			m_launchAudioInstance = Singleton<AudioManager>.Instance.SpawnOneShotEffect(m_launchAudio, base.transform);
		}
		StartCoroutine(ShineRocketLight());
	}

	private IEnumerator ShineRocketLight()
	{
		pls = GetComponentInChildren<PointLightSource>();
		if ((bool)pls)
		{
			yield return new WaitForSeconds(m_ignitionTime);
			pls.isEnabled = true;
			yield return new WaitForSeconds(m_boostDuration + m_boostEndDuration);
			pls.isEnabled = false;
		}
	}

	public override void SetEnabled(bool enabled)
	{
		if (!IsMultiplayerReplica() || m_enabled == enabled)
		{
			return;
		}
		if (!enabled)
		{
			m_enabled = false;
			if (m_particlesIgnitionInstance != null)
			{
				m_particlesIgnitionInstance.Stop();
			}
			if (m_particlesFiringInstance != null)
			{
				m_particlesFiringInstance.Stop();
			}
			return;
		}
		m_enabled = true;
		m_boostUsed = true;
		m_timeBoostStarted = Time.time;
		if (m_particlesIgnitionInstance != null)
		{
			m_particlesIgnitionInstance.Play();
		}
		if (Singleton<AudioManager>.IsInstantiated() && m_launchAudio != null)
		{
			m_launchAudioInstance = Singleton<AudioManager>.Instance.SpawnOneShotEffect(m_launchAudio, base.transform);
		}
		StartCoroutine(ShineRocketLight());
	}

	public void Explode()
	{
		if (IsMultiplayerReplica() || Time.time < m_explodeTime + INSettings.GetFloat(INFeature.RocketExplosionCoolingTime))
		{
			return;
		}
		m_explodeTime = Time.time;
		Collider[] array = Physics.OverlapSphere(base.transform.position, m_explosionRadius * INSettings.GetFloat(INFeature.RocketExplosionRadius));
		foreach (Collider collider in array)
		{
			GameObject gameObject = FindParentWithRigidBody(collider.gameObject);
			if (gameObject != null)
			{
				int num = CountChildColliders(gameObject, 0);
				AddExplosionForce(gameObject, INSettings.GetFloat(INFeature.RocketExplosionForce) / (float)num);
			}
			TNT component = collider.GetComponent<TNT>();
			// 军事版功能：核弹不会被火箭爆炸连锁引爆（军事版 Rocket.cs:677）。
			if ((bool)component && !component.HasGeneratorRef && !(INMilitaryFeatures.Enabled && component.IsNuclearBomb()))
			{
				component.Explode();
			}
		}
		WPFMonoBehaviour.effectManager.CreateParticles(m_smokeCloud, base.transform.position - Vector3.forward * 12f, force: true);
		Singleton<AudioManager>.Instance.SpawnOneShotEffect(WPFMonoBehaviour.gameData.commonAudioCollection.tntExplosion, base.transform.position);
		CheckForTNTAchievement();
		StartCoroutine(ShineExplosionLight());
	}

	/// <summary>
	/// 军事版功能：高速撞击起爆。AP 火箭（Rocket index 2）先进入 1 秒倒计时（在 Update 里走完），
	/// 其他带爆炸的火箭直接引爆（军事版 Rocket.cs:771-785）。关闭开关时本方法等同于空实现。
	/// </summary>
	public override void OnPhysicsCollisionEnter(PhysicsCollision collision)
	{
		base.OnPhysicsCollisionEnter(collision);
		if (!INMilitaryFeatures.Enabled || !m_explodes || base.HasGeneratorRef)
		{
			return;
		}
		if (collision.relativeVelocity.magnitude <= 5f)
		{
			return;
		}
		if (this.IsAPRocket())
		{
			m_apRocketTimering = true;
			m_apRocketBombTimer = 0f;
		}
		else
		{
			Explode();
		}
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
		if (m_partType == PartType.RedRocket)
		{
			Vector3 vector2 = target.transform.position - base.transform.position;
			float num3 = Mathf.Max(vector2.magnitude, 1f);
			float num4 = forceFactor * 1.5f * m_explosionImpulse / Mathf.Pow(num3, 2f);
			Rigidbody component2 = target.GetComponent<Rigidbody>();
			if (component2.mass < 0.1f)
			{
				num4 *= component2.mass;
			}
			else if (component2.mass < 0.4f)
			{
				num4 *= component2.mass / 0.4f;
			}
			component2.EngineAddForce(num4 * vector2.normalized, ForceMode.Impulse);
		}
	}

	public void CheckForTNTAchievement()
	{
		if (!Singleton<SocialGameManager>.IsInstantiated())
		{
			return;
		}
		int brokenTNTs = GameProgress.GetInt("Broken_TNTs") + 1;
		GameProgress.SetInt("Broken_TNTs", brokenTNTs);
		foreach (string item in new List<string> { "grp.BOOM_BOOM_III", "grp.BOOM_BOOM_II", "grp.BOOM_BOOM_I" })
		{
			Singleton<SocialGameManager>.Instance.TryReportAchievementProgress(item, 100.0, (int limit) => brokenTNTs >= limit);
		}
	}

	private IEnumerator ShineExplosionLight()
	{
		PointLightSource pls = GetComponentInChildren<PointLightSource>();
		if ((bool)pls)
		{
			pls.size = 4f;
			if ((bool)base.renderer)
			{
				base.renderer.enabled = false;
			}
			pls.onLightTurnOff = (Action)Delegate.Combine(pls.onLightTurnOff, (Action)delegate
			{
				UnityEngine.Object.Destroy(base.gameObject);
			});
			pls.isEnabled = true;
			yield return new WaitForSeconds(pls.turnOnCurve[pls.turnOnCurve.length - 1].time);
			pls.isEnabled = false;
		}
		else if (!INSettings.GetBool(INFeature.SwitchableCokeSodaRocket))
		{
			UnityEngine.Object.Destroy(base.gameObject);
		}
	}
}
