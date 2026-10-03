using System;
using System.Collections.Generic;
using UnityEngine;

public class BlasterTNT : TNT
{
	private struct BlasterInfo
	{
		public Vector2 Center;

		public Vector2 CenterVelocity;

		public float CenterDrag;

		public float Radius;

		public float RadiusVelocity;

		public float RadiusDrag;

		public float Amplitude;

		public float Cycle;

		public float DeltaTime;

		public float GetValue(float time)
		{
			if (time <= 0f || time >= Cycle)
			{
				return 0f;
			}
			return Amplitude * (1f - time / Cycle) * MathF.Exp((0f - time) / Cycle);
		}

		public float GetValueWithRange(float time1, float time2)
		{
			return F(this, time2) - F(this, time1);
			static float F(BlasterInfo self, float x)
			{
				if (x <= 0f)
				{
					return 0f;
				}
				if (x >= self.Cycle)
				{
					return self.Amplitude * self.Cycle / MathF.E;
				}
				return self.Amplitude * x * MathF.Exp((0f - x) / self.Cycle);
			}
		}

		public void Update()
		{
			CenterVelocity *= Math.Max(1f - CenterDrag * DeltaTime, 0f);
			Center += CenterVelocity * DeltaTime;
			RadiusVelocity *= Math.Max(1f - RadiusDrag * DeltaTime, 0f);
			Radius += RadiusVelocity * DeltaTime;
		}
	}

	private GameObject m_sprite;

	private GameObject m_blasterSprite;

	private BlasterInfo m_blaster;

	private bool m_released;

	private bool m_exploded;

	private bool m_exploding;

	private float m_explodeTime;

	private bool m_destroyCorpse;

	private bool m_jointsDetached;

	public override void Awake()
	{
		base.Awake();
		m_sprite = base.transform.Find("Visualization").gameObject;
		m_blasterSprite = base.transform.Find("BlasterSprite").gameObject;
		if (no90 == 1)
		{
			OnTouch();
		}
	}

	public override void SetRotation(GridRotation rotation)
	{
		base.SetRotation(rotation);
		if (no90 == 1 && !m_released && m_sprite != null)
		{
			OnTouch();
		}
	}

	public override bool IsEnabled()
	{
		return m_released;
	}

	public override void OnPhysicsCollisionEnter(PhysicsCollision collision)
	{
		base.OnPhysicsCollisionEnter(collision);
		HandleCollision(collision);
	}

	public override void OnPhysicsCollisionStay(PhysicsCollision collision)
	{
		base.OnPhysicsCollisionStay(collision);
		HandleCollision(collision);
	}

	private void HandleCollision(PhysicsCollision collision)
	{
		if (Vector.LengthSquared2(collision.relativeVelocity) > m_triggerSpeed * m_triggerSpeed && !base.HasGeneratorRef)
		{
			ExplodeSpecial();
		}
	}

	public override void Explode()
	{
	}

	protected override void OnTouch()
	{
		if (!m_released)
		{
			m_released = true;
		}
		else
		{
			ExplodeSpecial();
		}
	}

	public void ExplodeSpecial()
	{
		if (!m_released || m_exploded)
		{
			return;
		}
		m_exploding = true;
		m_exploded = true;
		m_explodeTime = Time.fixedTime;
		m_blaster = new BlasterInfo
		{
			Center = base.transform.position,
			CenterVelocity = base.rigidbody.velocity,
			CenterDrag = 0.2f,
			Radius = 0.5f,
			RadiusVelocity = 160f,
			RadiusDrag = 0.2f,
			Amplitude = 80000f,
			Cycle = 0.02f,
			DeltaTime = 0.02f
		};
		m_blasterSprite.SetActive(value: true);
		UpdateBlasterSprite();
		m_destroyCorpse = INUserSettings.Instance?.StarSeaSettings?.BlasterTNTDestroy ?? false;
		if (m_destroyCorpse)
		{
			// 冲击波炸弹销毁：爆炸瞬间隐藏贴图、失去碰撞并销毁关节，特效结束后再整体销毁
			HideCorpse();
		}
		else
		{
			ToGray(base.gameObject, gray: true);
		}
		base.contraption.ChangeOneShotPartAmount(m_partType, EffectDirection(), -1);
		UIPartButtonList.Instance.NeedsUpdate = true;
		Vector3 position = base.transform.position;
		foreach (BasePart part in base.contraption.Parts)
		{
			if (part != this && part is BlasterTNT blasterTNT && Vector.DistanceSquared2(position, part.transform.position) < 64f)
			{
				blasterTNT.ExplodeSpecial();
			}
		}
	}

	private void FixedUpdate()
	{
		if (!m_exploding)
		{
			return;
		}
		if (Time.time - m_explodeTime > 2f)
		{
			m_exploding = false;
			m_blasterSprite.SetActive(value: false);
			// 冲击波炸弹销毁：特效结束后整体销毁本体，不留下尸体
			if (m_destroyCorpse)
			{
				RemoveCorpse();
			}
			return;
		}
		Vector2 center = m_blaster.Center;
		float radius = m_blaster.Radius;
		float radiusVelocity = m_blaster.RadiusVelocity;
		m_blaster.Update();
		Vector2 center2 = m_blaster.Center;
		float radius2 = m_blaster.Radius;
		float radiusVelocity2 = m_blaster.RadiusVelocity;
		float cycle = m_blaster.Cycle;
		Rigidbody[] components = INContraption.Instance.Rigidbodies;
		foreach (Rigidbody rigidbody in components)
		{
			Vector3 position = rigidbody.position;
			float num = Vector.Distance2(position.x, position.y, center.x, center.y);
			float num2 = Vector.Distance2(position.x, position.y, center2.x, center2.y);
			float num3 = (radius - num) / radiusVelocity;
			float num4 = (radius2 - num2) / radiusVelocity2;
			if (!(num < 0.5f) && !(num2 < 0.5f) && (!(num3 <= 0f) || !(num4 <= 0f)) && (!(num3 >= cycle) || !(num4 >= cycle)))
			{
				float num5 = (position.x - center2.x) / num2;
				float num6 = (position.y - center2.y) / num2;
				float num7 = Math.Abs(m_blaster.GetValueWithRange(num3, num4)) / Math.Max(num2 * num2, 16f);
				rigidbody.EngineAddForce(new Vector3(num7 * num5, num7 * num6), ForceMode.Impulse);
			}
		}
		foreach (BasePart part in base.contraption.Parts)
		{
			if (part is PointLight { EntityLight: var entityLight } && entityLight != null && entityLight.Type == 4)
			{
				Vector3 position2 = part.transform.position;
				float num8 = radius2;
				float length = entityLight.Length;
				float num9 = Vector.DistanceSquared2(position2.x, position2.y, center2.x, center2.y);
				if ((num8 - length) * (num8 - length) < num9 && num9 < (num8 + length) * (num8 + length))
				{
					float num10 = MathF.Sqrt(num9);
					float value = (num8 * num8 + num9 - length * length) / (2f * num8 * num10);
					float num11 = 2f * MathF.Acos(Math.Abs(value));
					entityLight.m_electricity -= 0.5f * m_blaster.Amplitude * num11 / Math.Max(num8, 4f) / (float)entityLight.m_componentSize;
				}
			}
		}
	}

	private void Update()
	{
		if (m_exploding)
		{
			UpdateBlasterSprite();
		}
	}

	private void UpdateBlasterSprite()
	{
		float radius = m_blaster.Radius;
		m_blasterSprite.transform.position = m_blaster.Center;
		m_blasterSprite.transform.localScale = new Vector3(2f * radius, 2f * radius, 1f);
		m_blasterSprite.GetComponent<MeshRenderer>().material.color = new Color(1f, 1f, 1f, Math.Min(64f / (radius * radius), 0.25f));
	}

	private void ToGray(GameObject gameObject, bool gray)
	{
		Shader shader = INUnity.LoadShader(gray ? "PreAlpha_Unlit_ColorTransparent_Geometry_Gray" : "PreAlpha_Unlit_ColorTransparent_Geometry");
		MeshRenderer[] componentsInChildren = gameObject.GetComponentsInChildren<MeshRenderer>();
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			componentsInChildren[i].material.shader = shader;
		}
	}

	/// <summary>
	/// 冲击波炸弹销毁：爆炸瞬间隐藏本体贴图、关闭碰撞并立即销毁关节（保留 BlasterSprite 冲击波特效）。
	/// </summary>
	private void HideCorpse()
	{
		if (m_sprite != null)
		{
			m_sprite.SetActive(value: false);
		}
		// 连接点贴图（左/右/上/下 Attachment）也要一并隐藏
		HideChildByName("LeftAttachment");
		HideChildByName("RightAttachment");
		HideChildByName("TopAttachment");
		HideChildByName("BottomAttachment");
		if (base.rigidbody != null)
		{
			base.rigidbody.detectCollisions = false;
		}
		Collider[] colliders = GetComponentsInChildren<Collider>(includeInactive: true);
		for (int i = 0; i < colliders.Length; i++)
		{
			colliders[i].enabled = false;
		}
		// 关节立即销毁：避免隐形尸体在这段特效时间里继续拖拽/推动载具
		DetachJoints();
	}

	/// <summary>
	/// 按名字隐藏本体下的直接子物体（不存在则忽略）。
	/// </summary>
	private void HideChildByName(string childName)
	{
		Transform child = base.transform.Find(childName);
		if (child != null)
		{
			child.gameObject.SetActive(value: false);
		}
	}

	/// <summary>
	/// 立即销毁本部件与载具之间的所有关节（只执行一次）。
	/// </summary>
	private void DetachJoints()
	{
		if (m_jointsDetached || base.contraption == null)
		{
			return;
		}
		m_jointsDetached = true;
		List<Joint> joints = base.contraption.FindPartJoints(this);
		if (joints.Count > 0)
		{
			foreach (Joint joint in joints)
			{
				if (joint != null)
				{
					UnityEngine.Object.Destroy(joint);
				}
			}
			HandleJointBreak();
		}
		else
		{
			HandleJointBreak(playEffects: false);
		}
	}

	/// <summary>
	/// 冲击波炸弹销毁：从载具移除本部件并销毁自身（不留尸体）。关节已在爆炸瞬间断开。
	/// </summary>
	private void RemoveCorpse()
	{
		DetachJoints();
		if (base.contraption != null)
		{
			base.contraption.RemovePart(this);
		}
		UnityEngine.Object.Destroy(base.gameObject);
	}
}
