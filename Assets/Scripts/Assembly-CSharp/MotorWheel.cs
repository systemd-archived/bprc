using System;
using System.Collections.Generic;
using UnityEngine;

public class MotorWheel : BasePart
{
	public float m_force;

	public bool m_enabled;

	public float m_maximumSpeed;

	private float m_maximumForce;

	private Vector3 m_lastPosition = Vector3.zero;

	private Vector3 m_lastContactDirection = Vector3.zero;

	private float m_radius;

	private float m_circumference;

	private Transform m_wheelPivot;

	private Transform m_fakeWheelPivot;

	private float m_spinSpeed;

	private Vector3 m_lastForceDirection;

	private float m_angle;

	private Collider m_supportCollider;

	private bool m_hasContact = true;

	private float m_thrust;

	private float m_thrustTimer;

	private Rigidbody colliderRigidbody;

	private AudioSource loopingWheelSound;

	private AudioSource loopingWheelBrushSound;

	private AudioSource wheelBrushSoundStart;

	private bool m_grounded;

	private bool m_onIceSurface;

	private bool m_playedIceSurfaceStart;

	private bool lastGearboxState;

	private float loopingIceSurfaceSoundTime;

	public bool HasContact => m_hasContact;

	public override bool CanBeEnabled()
	{
		return m_maximumSpeed > 0f;
	}

	public override bool HasOnOffToggle()
	{
		return true;
	}

	public override bool IsEnabled()
	{
		return m_enabled;
	}

	private void Start()
	{
		m_wheelPivot = base.transform.Find("WheelPivot");
		m_fakeWheelPivot = base.transform.Find("FakeWheelPivot");
		m_radius = GetComponent<SphereCollider>().radius;
		m_circumference = MathF.PI * 2f * m_radius;
		if ((bool)base.transform.Find("SupportCollider"))
		{
			m_supportCollider = base.transform.Find("SupportCollider").GetComponent<Collider>();
		}
		// 应用“轮半径”覆盖（缩放视觉与物理）
		float overrideRadius = GetCustomParam("WheelRadius", -1f);
		Debug.Log("[WheelDiag] " + GetType().Name + " Start params='" + SerializeCustomParams() + "' override=" + overrideRadius);
		if (overrideRadius > 0f)
		{
			ApplyWheelRadius(overrideRadius);
		}
	}

	// 额外可调项：轮半径（缩放视觉 WheelSprite/SpokeSprite 与物理半径）
	public override void CollectExtraParams(List<string> keys)
	{
		keys.Add("WheelRadius");
	}

	public override bool ApplyExtraParam(string key, float value)
	{
		if (key == "WheelRadius")
		{
			ApplyWheelRadius(value);
			return true;
		}
		return base.ApplyExtraParam(key, value);
	}

	public override bool TryGetExtraParamValue(string key, out float value)
	{
		value = 0f;
		if (key == "WheelRadius")
		{
			SphereCollider rc = GetComponent<SphereCollider>();
			value = (m_radius > 0f) ? m_radius : ((rc != null) ? rc.radius : 0.45f);
			return true;
		}
		return base.TryGetExtraParamValue(key, out value);
	}

	[SerializeField, HideInInspector]
	private float m_baseWheelRadius;

	private float m_lastAppliedRadius;

	// 外部（如长按面板）直接改了 m_radius 字段时，按新半径重新缩放视觉与物理
	private void CheckRadiusChanged()
	{
		if (m_radius > 0f && !Mathf.Approximately(m_radius, m_lastAppliedRadius))
		{
			float r = m_radius;
			m_lastAppliedRadius = r;
			ApplyWheelRadius(r);
		}
	}

	[SerializeField, HideInInspector]
	private Vector3 m_baseRootScale = Vector3.zero;

	// 本部件自己按轮径写 localScale（ApplyWheelRadius），因此每部件缩放由它自己复合，见 BasePart.OwnsOwnScale。
	public override bool OwnsOwnScale => true;
	public override Vector3 BuildFootprintScale => m_baseRootScale == Vector3.zero ? PartScaleFactors : RelativeBuildScale(m_baseRootScale);

	public override void ApplyPartScale()
	{
		GridlessBuild.InvalidateBounds(contraption);
		ApplyWheelRadius(GetCustomParam("WheelRadius", m_radius > 0f ? m_radius : 0.45f));
	}

	private void ApplyWheelRadius(float value)
	{
		GridlessBuild.InvalidateBounds(contraption);
		if (value <= 0f)
		{
			return;
		}
		if (m_baseWheelRadius <= 0f)
		{
			SphereCollider rc = GetComponent<SphereCollider>();
			m_baseWheelRadius = ((rc != null) ? rc.radius : 0.45f);
		}
		if (m_baseRootScale == Vector3.zero)
		{
			m_baseRootScale = base.transform.localScale;
		}
		float k = (m_baseWheelRadius > 0f) ? (value / m_baseWheelRadius) : 1f;
		Vector3 s = PartScaleFactors;
		base.transform.localScale = new Vector3(m_baseRootScale.x * k * s.x, m_baseRootScale.y * k * s.y, m_baseRootScale.z * s.z);
		m_radius = value;
		m_circumference = MathF.PI * 2f * value;
		m_lastAppliedRadius = value;
		Debug.Log("[WheelDiag] " + GetType().Name + " ApplyWheelRadius value=" + value + " base=" + m_baseWheelRadius + " k=" + k);
	}

	private void SpawnLoopingWheelSound()
	{
		loopingWheelSound = Singleton<AudioManager>.Instance.SpawnCombinedLoopingEffect(WPFMonoBehaviour.gameData.commonAudioCollection.motorWheelLoop, base.transform).GetComponent<AudioSource>();
		loopingWheelSound.volume = 0f;
	}

	private void SpawnLoopingWheelOnIceSound()
	{
		loopingWheelBrushSound = Singleton<AudioManager>.Instance.SpawnCombinedLoopingEffect(WPFMonoBehaviour.gameData.commonAudioCollection.motorWheelOnIceLoop, base.transform).GetComponent<AudioSource>();
		loopingWheelBrushSound.volume = 0f;
	}

	public override void InitializeEngine()
	{
		float enginePowerFactor = base.contraption.GetEnginePowerFactor(this);
		m_maximumForce = m_force * enginePowerFactor;
		m_maximumSpeed = 15f * enginePowerFactor;
	}

	private void ReinitializeEngine()
	{
		float enginePowerFactor = base.contraption.GetEnginePowerFactor(this);
		m_maximumForce = m_force * enginePowerFactor;
	}

	public override void OnPhysicsCollisionStay(PhysicsCollision collisionInfo)
	{
		base.OnPhysicsCollisionStay(collisionInfo);
		for (int i = 0; i < collisionInfo.contacts.Length; i++)
		{
			if (collisionInfo.contacts[i].otherCollider != m_supportCollider)
			{
				m_lastContactDirection = (collisionInfo.contacts[i].point - m_lastPosition).normalized;
				break;
			}
		}
	}

	public override void OnLightEnter(EntityLightCollision collision)
	{
		Vector2 vector = m_lastPosition;
		m_lastContactDirection = (collision.ContactPoint - vector).normalized;
	}

	private void Update()
	{
		CheckRadiusChanged();
		if (!base.contraption || !base.contraption.IsRunning)
		{
			return;
		}
		if (m_spinSpeed == 0f)
		{
			m_spinSpeed = SpeedInDirection(m_lastForceDirection);
		}
		float z = base.transform.rotation.eulerAngles.z;
		if (m_enabled && m_onIceSurface)
		{
			m_spinSpeed = 15f;
		}
		m_angle += -360f * m_spinSpeed / m_circumference * Time.deltaTime;
		if ((bool)m_wheelPivot)
		{
			m_wheelPivot.transform.localRotation = Quaternion.AngleAxis(0f - z + m_angle, Vector3.forward);
		}
		if ((bool)m_fakeWheelPivot)
		{
			float num = 0f - z + Mathf.Sin(2f * m_angle * (MathF.PI / 180f)) * 8f;
			if (m_onIceSurface)
			{
				num *= 2f;
			}
			m_fakeWheelPivot.transform.localRotation = Quaternion.AngleAxis(num, Vector3.forward);
		}
		UpdateSoundEffect();
	}

	protected override void UpdateSoundEffect()
	{
		float num = 1f;
		if (Mathf.Abs(m_spinSpeed) > num && m_grounded)
		{
			if (m_onIceSurface && m_enabled)
			{
				loopingIceSurfaceSoundTime += Time.deltaTime;
				if (!m_playedIceSurfaceStart)
				{
					m_playedIceSurfaceStart = true;
					loopingIceSurfaceSoundTime = 0f;
					if ((bool)loopingWheelBrushSound)
					{
						loopingWheelBrushSound.volume = 0f;
					}
					if ((bool)wheelBrushSoundStart)
					{
						wheelBrushSoundStart.Stop();
						UnityEngine.Object.Destroy(wheelBrushSoundStart);
					}
					wheelBrushSoundStart = Singleton<AudioManager>.Instance.SpawnOneShotEffect(WPFMonoBehaviour.gameData.commonAudioCollection.motorWheelOnIceStart, base.transform);
					if (wheelBrushSoundStart != null)
					{
						if (!lastGearboxState)
						{
							wheelBrushSoundStart.volume = 0.4f * (1f - Mathf.Clamp01(base.rigidbody.velocity.x * 0.1f));
						}
						else
						{
							wheelBrushSoundStart.volume = 0.4f * (1f - Mathf.Clamp01((0f - base.rigidbody.velocity.x) * 0.1f));
						}
						wheelBrushSoundStart.Play();
					}
				}
				if (loopingIceSurfaceSoundTime > 1.4f)
				{
					if (!loopingWheelBrushSound)
					{
						SpawnLoopingWheelOnIceSound();
					}
					if (!lastGearboxState)
					{
						loopingWheelBrushSound.volume = 0.4f * (1f - Mathf.Clamp01(base.rigidbody.velocity.x * 0.1f));
					}
					else
					{
						loopingWheelBrushSound.volume = 0.4f * (1f - Mathf.Clamp01((0f - base.rigidbody.velocity.x) * 0.1f));
					}
				}
			}
			else
			{
				if ((bool)wheelBrushSoundStart)
				{
					wheelBrushSoundStart.Stop();
					UnityEngine.Object.Destroy(wheelBrushSoundStart);
				}
				if ((bool)loopingWheelBrushSound)
				{
					loopingWheelBrushSound.volume = 0f;
				}
			}
			if (!loopingWheelSound)
			{
				SpawnLoopingWheelSound();
			}
			loopingWheelSound.volume = 0.15f * (Mathf.Abs(m_spinSpeed) / num - 1f);
		}
		else
		{
			if ((bool)loopingWheelSound)
			{
				loopingWheelSound.volume = 0f;
			}
			if ((bool)loopingWheelBrushSound)
			{
				loopingWheelBrushSound.volume = 0f;
			}
			if ((bool)wheelBrushSoundStart)
			{
				wheelBrushSoundStart.Stop();
				UnityEngine.Object.Destroy(wheelBrushSoundStart);
			}
		}
	}

	private void FixedUpdate()
	{
		if (!base.contraption || !base.contraption.IsRunning)
		{
			return;
		}
		float multiplayerDriveSign = INRc1Controls.NativeDriveSign(this, base.contraption.MultiplayerDriveSign);
		ReinitializeEngine();
		if (m_enabled)
		{
			m_thrustTimer += Time.deltaTime * 1f;
			m_thrustTimer = Mathf.Min(m_thrustTimer, 1f);
			m_thrust = Mathf.Pow(m_thrustTimer, 0.4f);
			if (base.contraption.ConnectedToGearbox(this))
			{
				bool flag = base.contraption.GetGearbox(this).IsEnabled();
				if (flag != lastGearboxState)
				{
					OnGearboxStateChanged();
				}
				if (flag)
				{
					m_thrust = 0f - m_thrust;
				}
				lastGearboxState = flag;
			}
		}
		else
		{
			m_thrustTimer = 0f;
			m_thrust = 0f;
		}
		m_lastPosition = m_wheelPivot.transform.position;
		RaycastHit hitInfo;
		bool num = Physics.Raycast(m_wheelPivot.transform.position, m_lastContactDirection, out hitInfo, m_radius + 0.1f);
		m_onIceSurface = false;
		// The marker separator lets same-faction vehicles overlap, but Physics.IgnoreCollision only
		// removes contacts: this probe still hits the hull being driven through. Treating that hull as
		// ground drives the wheel along its normal and pushes the other vehicle at the hit point, which
		// is what tips the trailing vehicle over. No contact is the honest answer here.
		if (num && hitInfo.collider != m_supportCollider && !MarkerManager.IsPhasedVehicle(this, hitInfo.collider))
		{
			float num2 = SpeedInDirection(base.transform.right) * multiplayerDriveSign;
			Vector3 vector = (m_lastForceDirection = Vector3.Cross(hitInfo.normal, Vector3.forward) * multiplayerDriveSign);
			m_spinSpeed = 0f;
			m_hasContact = true;
			colliderRigidbody = hitInfo.collider.gameObject.GetComponent<Rigidbody>();
			if (m_enabled && m_maximumSpeed > 0f && num2 < m_maximumSpeed && num2 > 0f - m_maximumSpeed)
			{
				float f = 1f - Mathf.Abs(num2) / m_maximumSpeed;
				f = Mathf.Pow(f, 0.5f);
				float num3 = m_thrust * m_maximumForce * f;
				if (m_onIceSurface = hitInfo.transform.CompareTag("IceSurface"))
				{
					num3 *= 0.25f + Mathf.Clamp(num2, 0f, m_maximumSpeed) / m_maximumSpeed * 0.5f;
				}
				base.rigidbody.EngineAddForceAtPosition(num3 * vector, hitInfo.point, ForceMode.Force);
				if (!colliderRigidbody && hitInfo.collider.transform.parent != null)
				{
					colliderRigidbody = hitInfo.collider.transform.parent.GetComponent<Rigidbody>();
				}
				if (colliderRigidbody != null && !colliderRigidbody.isKinematic)
				{
					colliderRigidbody.EngineAddForceAtPosition((0f - num3) * vector, hitInfo.point, ForceMode.Force);
				}
			}
			m_grounded = true;
			return;
		}
		m_hasContact = false;
		colliderRigidbody = null;
		if (m_enabled)
		{
			m_spinSpeed = m_maximumSpeed * multiplayerDriveSign;
			if (base.contraption.ConnectedToGearbox(this) && base.contraption.GetGearbox(this).IsEnabled())
			{
				m_spinSpeed = (0f - m_maximumSpeed) * multiplayerDriveSign;
			}
		}
		else
		{
			m_spinSpeed *= 0.98f;
		}
	}

	private float SpeedInDirection(Vector3 direction)
	{
		Vector3 velocity = GetComponent<Rigidbody>().velocity;
		if ((bool)colliderRigidbody)
		{
			velocity -= colliderRigidbody.velocity;
		}
		return Vector3.Dot(velocity, direction);
	}

	protected override void OnTouch()
	{
		SetEnabled(!m_enabled);
	}

	public override void SetEnabled(bool enabled)
	{
		m_enabled = enabled;
		if (!m_enabled)
		{
			m_playedIceSurfaceStart = false;
		}
		base.contraption.UpdateEngineStates(base.ConnectedComponent);
	}

	private void OnGearboxStateChanged()
	{
		m_playedIceSurfaceStart = false;
	}
}
