using System;
using System.Collections.Generic;
using UnityEngine;

public class CartWheel : BasePart
{
	private Vector3 m_lastPosition = Vector3.zero;

	private Vector3 m_lastContactDirection = Vector3.zero;

	private float m_radius;

	private float m_circumference;

	private Transform m_wheelPivot;

	private Transform m_fakeWheelPivot;

	private Transform m_axle;

	private float m_spinSpeed;

	private Vector3 m_lastForceDirection;

	private float m_angle;

	private AudioSource loopingWheelSound;

	private Collider m_supportCollider;

	private Rigidbody colliderRigidbody;

	private bool m_grounded;

	private GameObject m_rayObject;

	private bool m_rayActive;

	private float kd;

	private void Start()
	{
		m_wheelPivot = base.transform.Find("WheelPivot");
		m_fakeWheelPivot = base.transform.Find("FakeWheelPivot");
		if ((bool)m_wheelPivot)
		{
			m_axle = m_wheelPivot;
			m_radius = m_wheelPivot.GetComponent<SphereCollider>().radius;
			m_circumference = MathF.PI * 2f * m_radius;
		}
		else
		{
			m_axle = m_fakeWheelPivot;
			m_radius = m_fakeWheelPivot.GetComponent<SphereCollider>().radius;
			m_circumference = MathF.PI * 2f * m_radius;
		}
		if ((bool)base.transform.Find("SupportCollider"))
		{
			m_supportCollider = base.transform.Find("SupportCollider").GetComponent<Collider>();
		}
		// 应用“轮半径”覆盖（缩放视觉与物理）
		float overrideRadius = GetCustomParam("WheelRadius", -1f);
		Debug.Log("[WheelDiag] Start class=" + GetType().Name + " type=" + m_partType + " idx=" + customPartIndex
			+ " params='" + SerializeCustomParams() + "' override=" + overrideRadius);
		if (overrideRadius > 0f)
		{
			ApplyWheelRadius(overrideRadius);
		}
	}

	private void SpawnSound()
	{
		switch (m_partType)
		{
		case PartType.ObsoleteWheel:
			loopingWheelSound = Singleton<AudioManager>.Instance.SpawnCombinedLoopingEffect(WPFMonoBehaviour.gameData.commonAudioCollection.woodenWheelLoop, base.transform).GetComponent<AudioSource>();
			break;
		case PartType.NormalWheel:
			loopingWheelSound = Singleton<AudioManager>.Instance.SpawnCombinedLoopingEffect(WPFMonoBehaviour.gameData.commonAudioCollection.normalWheelLoop, base.transform).GetComponent<AudioSource>();
			break;
		case PartType.SmallWheel:
			loopingWheelSound = Singleton<AudioManager>.Instance.SpawnCombinedLoopingEffect(WPFMonoBehaviour.gameData.commonAudioCollection.smallWheelLoop, base.transform).GetComponent<AudioSource>();
			break;
		case PartType.CartWheel:
			loopingWheelSound = Singleton<AudioManager>.Instance.SpawnCombinedLoopingEffect(WPFMonoBehaviour.gameData.commonAudioCollection.woodenWheelLoop, base.transform).GetComponent<AudioSource>();
			break;
		}
		loopingWheelSound.volume = 0f;
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

	private void Update()
	{
		CheckRadiusChanged();
		Camera main = Camera.main;
		float num = (main != null) ? main.orthographicSize : 10f;
		kd = num * 0.007f;
		if ((bool)base.contraption && base.contraption.IsRunning)
		{
			if (m_spinSpeed == 0f)
			{
				m_spinSpeed = SpeedInDirection(m_lastForceDirection);
			}
			float z = base.transform.rotation.eulerAngles.z;
			m_angle += -360f * m_spinSpeed / m_circumference * Time.deltaTime;
			if ((bool)m_wheelPivot)
			{
				m_wheelPivot.localRotation = Quaternion.AngleAxis(0f - z + m_angle, Vector3.forward);
			}
			if ((bool)m_fakeWheelPivot)
			{
				float angle = 0f - z + Mathf.Sin(2f * m_angle * (MathF.PI / 180f)) * 8f;
				m_fakeWheelPivot.localRotation = Quaternion.AngleAxis(angle, Vector3.forward);
			}
			UpdateSoundEffect();
			if (m_rayActive && m_rayObject != null)
			{
				LineRenderer component = m_rayObject.GetComponent<LineRenderer>();
				if (component != null)
				{
					component.startWidth = kd;
					component.endWidth = kd;
					Color color = new Color(1f, 0f, 0f, 0.33f);
					if (m_enclosedInto is ColoredFrame)
					{
						Color color2 = ((ColoredFrame)m_enclosedInto).Color;
						color = new Color(color2.r, color2.g, color2.b, 0.33f);
					}
					component.startColor = color;
					component.endColor = color;
					component.SetPosition(0, base.transform.position);
					component.SetPosition(1, base.transform.position + base.transform.up * -1E+14f);
				}
			}
		}
	}

	protected override void UpdateSoundEffect()
	{
		float num = 1f;
		if (Mathf.Abs(m_spinSpeed) > num && m_grounded)
		{
			if (!loopingWheelSound)
			{
				SpawnSound();
			}
			loopingWheelSound.volume = 0.2f * (Mathf.Abs(m_spinSpeed) / num - 1f);
		}
		else if ((bool)loopingWheelSound)
		{
			loopingWheelSound.volume = 0f;
		}
	}

	private void FixedUpdate()
	{
		if ((bool)base.contraption && base.contraption.IsRunning)
		{
			m_lastPosition = m_axle.position;
			if (Physics.Raycast(m_axle.position, m_lastContactDirection, out var hitInfo, m_radius + 0.1f) && hitInfo.collider != m_supportCollider)
			{
				colliderRigidbody = ((hitInfo.collider.gameObject.layer != BasePart.m_groundLayer) ? hitInfo.rigidbody : null);
				Vector3 lastForceDirection = Vector3.Cross(hitInfo.normal, Vector3.forward);
				m_lastForceDirection = lastForceDirection;
				m_spinSpeed = 0f;
				m_grounded = true;
			}
			else
			{
				m_spinSpeed *= 0.98f;
				colliderRigidbody = null;
				m_grounded = false;
			}
		}
	}

	private float SpeedInDirection(Vector3 direction)
	{
		Vector3 velocity = base.rigidbody.velocity;
		if ((bool)colliderRigidbody)
		{
			velocity -= colliderRigidbody.velocity;
		}
		return Vector3.Dot(velocity, direction);
	}

	protected override void OnTouch()
	{
		if (m_partTier != PartTier.Epic)
		{
			return;
		}
		m_rayActive = !m_rayActive;
		if (m_rayActive)
		{
			if (m_rayObject == null)
			{
				GameObject wheelRay = new GameObject("WheelRay");
				wheelRay.transform.parent = base.transform;
				wheelRay.transform.position = base.transform.position;
				LineRenderer lineRenderer = wheelRay.AddComponent<LineRenderer>();
				Material material = new Material(Shader.Find("Sprites/Default"));
				if (material == null)
				{
					material = new Material(Shader.Find("Unlit/Color"));
				}
				lineRenderer.material = material;
				Color color = new Color(1f, 0f, 0f, 0.33f);
				lineRenderer.startColor = color;
				lineRenderer.endColor = color;
				lineRenderer.positionCount = 2;
				m_rayObject = wheelRay;
			}
			m_rayObject.SetActive(true);
		}
		else if (m_rayObject != null)
		{
			m_rayObject.SetActive(false);
		}
	}

	// 额外可调项：轮半径（原本由子物体 WheelPivot/FakeWheelPivot 的 SphereCollider 半径决定）
	public override void CollectExtraParams(List<string> keys)
	{
		keys.Add("WheelRadius");
	}

	public override bool ApplyExtraParam(string key, float value)
	{
		if (key == "WheelRadius")
		{
			Debug.Log("[WheelDiag] ApplyExtraParam WheelRadius=" + value);
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
			value = m_radius;
			if (value <= 0f)
			{
				SphereCollider sc = FindWheelCollider();
				if (sc != null)
				{
					value = sc.radius;
				}
			}
			if (value <= 0f)
			{
				value = 0.5f;
			}
			return true;
		}
		return base.TryGetExtraParamValue(key, out value);
	}

	private SphereCollider FindWheelCollider()
	{
		Transform t = base.transform.Find("WheelPivot");
		if (t == null)
		{
			t = base.transform.Find("FakeWheelPivot");
		}
		return (t != null) ? t.GetComponent<SphereCollider>() : null;
	}

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
	private float m_baseWheelRadius;

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

	// 轮半径 = 视觉(WheelSprite)与物理(WheelPivot 的 SphereCollider)一起缩放
	private void ApplyWheelRadius(float value)
	{
		GridlessBuild.InvalidateBounds(contraption);
		if (value <= 0f)
		{
			return;
		}
		if (m_baseWheelRadius <= 0f)
		{
			SphereCollider rc = FindWheelCollider();
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
}
