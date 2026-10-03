using System;
using System.Collections.Generic;
using UnityEngine;

public class EntityLight : MonoBehaviour
{
	private static Dictionary<Collider, EntityLight> AntiTunnelColliders { get; } = new Dictionary<Collider, EntityLight>();

	public static bool ShouldBlockTunnel(Collider collider)
	{
		if (collider == null || !AntiTunnelColliders.TryGetValue(collider, out var light))
		{
			return false;
		}
		if (!INUserSettings.Instance.StarSeaSettings.LightAntiTunnel)
		{
			return false;
		}
		if (light.m_manager == null || light.m_index < 0 || light.m_index >= light.m_manager.m_capacities.Length)
		{
			return true;
		}
		float capacity = light.m_manager.m_capacities[light.m_index];
		if (capacity <= 0f)
		{
			return true;
		}
		float ratio = light.m_manager.m_electricities[light.m_index] / capacity;
		return ratio > 0f;
	}
	public struct LightData
	{
		public Vector2 Position0;

		public Vector2 Position1;

		public Vector2 Direction0;

		public Vector2 Direction1;

		public void Set(EntityLight light)
		{
			Vector3 position = light.Transform.position;
			Quaternion rotation = light.Transform.rotation;
			Position0 = Position1;
			Direction0 = Direction1;
			Position1.x = position.x;
			Position1.y = position.y;
			Direction1.x = 1f - (rotation.y * rotation.y * 2f + rotation.z * rotation.z * 2f);
			Direction1.y = rotation.x * rotation.y * 2f + rotation.w * rotation.z * 2f;
		}

		public void SetPosition(Vector2 position, Vector2 prePosition)
		{
			Position0 = prePosition;
			Position1 = position;
		}
	}

	[SerializeField]
	private int m_type;

	[SerializeField]
	private float m_angle;

	[SerializeField]
	private float m_halfWidth;

	[SerializeField]
	private float m_length;

	private bool m_enabled;

	private bool m_colored;

	private int m_index;

	private int m_sides;

	private float m_cos;

	private bool m_ignoreCollision;

	public float m_electricity;

	public int m_componentSize;

	private Color m_color;

	private INPhysicMaterial m_physicMaterial;

	private BasePart m_part;

	private Transform m_transform;

	private MeshRenderer m_meshRenderer;

	public MeshFilter m_meshFilter;

	private Collider m_collider;

	private EntityLightManager m_manager;

	private LightData m_data;

	public ref LightData Data => ref m_data;

	public int Sides => m_sides;

	public bool Enabled
	{
		get
		{
			return m_enabled;
		}
		set
		{
			m_enabled = value;
			if (IsLightPillar)
			{
				m_collider.enabled = value;
			}
		}
	}

	public int Type
	{
		get
		{
			return m_type;
		}
		set
		{
			m_type = value;
		}
	}

	public float Length
	{
		get
		{
			return m_length;
		}
		set
		{
			m_length = value;
		}
	}

	public float HalfWidth
	{
		get
		{
			return m_halfWidth;
		}
		set
		{
			m_halfWidth = value;
		}
	}

	public float Angle
	{
		get
		{
			return m_angle;
		}
		set
		{
			m_angle = value;
		}
	}

	public float Cos => m_cos;

	public bool IsLightPillar
	{
		get
		{
			if (m_type != 0)
			{
				return m_type == 1;
			}
			return true;
		}
	}

	public bool IsLightShield
	{
		get
		{
			if (m_type != 2)
			{
				return m_type == 4;
			}
			return true;
		}
	}

	public bool IsLightBox => m_type == 3;

	public int Index
	{
		get
		{
			return m_index;
		}
		set
		{
			m_index = value;
		}
	}

	public GameObject Light => m_transform.gameObject;

	public Transform Transform => m_transform;

	public BasePart Part => m_part;

	public int ComponentIndex => m_part.ConnectedComponent;

	public int ComponentSize => m_componentSize;

	private void Awake()
	{
		m_part = GetComponent<BasePart>();
		m_transform = base.transform.Find("INLight");
		if (m_transform == null)
		{
			GameObject gameObject = new GameObject("INLight");
			m_transform = gameObject.transform;
			m_transform.parent = base.transform;
			gameObject.AddComponent<MeshRenderer>();
			gameObject.AddComponent<MeshFilter>();
		}
		m_transform.localPosition = new Vector3(0f, 0.5f, -0.5f);
		m_transform.localRotation = new Quaternion(0f, 0f, 0.70710677f, 0.70710677f);
		m_meshRenderer = m_transform.GetComponent<MeshRenderer>();
		m_meshRenderer.sharedMaterial = new Material(INUnity.ColorTransparentShader);
		m_meshRenderer.material.color = Color.clear;
		m_meshFilter = m_transform.GetComponent<MeshFilter>();
	}

	private void Start()
	{
		m_manager = EntityLightManager.Instance;
		m_transform.gameObject.layer = LayerMask.NameToLayer("Ground");
		RebuildMeshIfChanged();
		if (Contraption.Instance.IsRunning)
		{
			CreateCollider();
			InitializeColor();
			InitializePhysicMaterial();
			BasePart enclosedInto = m_part.m_enclosedInto;
			if (enclosedInto != null && enclosedInto.IsTransparentFrame())
			{
				m_ignoreCollision = true;
			}
		}
	}

	private void CreateCollider()
	{
		if (IsLightPillar)
		{
			m_collider = m_transform.GetComponent<BoxCollider>();
			if (m_collider == null)
			{
				m_collider = m_transform.gameObject.AddComponent<BoxCollider>();
			}
			BoxCollider obj = m_collider as BoxCollider;
			obj.size = new Vector3(m_length, m_halfWidth * 2f, 1f);
			obj.center = new Vector3(m_length * 0.5f, 0f, 0.5f);
			obj.isTrigger = true;
			AntiTunnelColliders[m_collider] = this;
		}
	}

	private void OnDestroy()
	{
		if (m_collider != null)
		{
			AntiTunnelColliders.Remove(m_collider);
		}
	}

	private void InitializePhysicMaterial()
	{
		float bounciness = 0f;
		PhysicMaterialCombine bounceMode = PhysicMaterialCombine.Average;
		float friction = 0.7f;
		PhysicMaterialCombine frictionMode = PhysicMaterialCombine.Average;
		BasePart enclosedInto = m_part.m_enclosedInto;
		if (enclosedInto != null && (enclosedInto.IsAlienMetalFrame() || enclosedInto.IsColoredrame()))
		{
			friction = 0f;
			frictionMode = PhysicMaterialCombine.Minimum;
		}
		m_physicMaterial = new INPhysicMaterial(bounciness, bounceMode, friction, frictionMode);
	}

	private void InitializeColor()
	{
		bool flag = !Contraption.Instance.HasTurboCharge;
		BasePart enclosedInto = m_part.m_enclosedInto;
		if (INSettings.GetBool(INFeature.ColoredFrame) && enclosedInto != null && enclosedInto is ColoredFrame coloredFrame)
		{
			m_colored = true;
			m_color = coloredFrame.Color.WithAlpha(flag ? 0.5f : 0.7f);
			return;
		}
		m_colored = false;
		if (flag)
		{
			Color color;
			switch (m_type)
			{
			case 0:
				color = new Color(0.5f, 0.75f, 1f, 0.5f);
				break;
			case 1:
				color = new Color(0.5f, 0.65f, 1f, 0.5f);
				break;
			case 2:
			case 4:
				color = new Color(0.5f, 0.7f, 1f, 0.5f);
				break;
			case 3:
				color = new Color(0.5f, 0.6f, 1f, 0.5f);
				break;
			default:
				color = default(Color);
				break;
			}
			m_color = color;
		}
		else
		{
			Color color;
			switch (m_type)
			{
			case 0:
				color = new Color(0.55f, 0.5f, 1f, 0.7f);
				break;
			case 1:
				color = new Color(0.65f, 0.5f, 1f, 0.7f);
				break;
			case 2:
			case 4:
				color = new Color(0.6f, 0.5f, 1f, 0.7f);
				break;
			case 3:
				color = new Color(0.7f, 0.5f, 1f, 0.7f);
				break;
			default:
				color = default(Color);
				break;
			}
			m_color = color;
		}
	}

	public void UpdateSelf()
	{
		if (m_type != 0 && m_type != 1)
		{
			return;
		}
		m_sides = 0;
		if (Contraption.Instance.ConnectedToGearbox(m_part))
		{
			Gearbox gearbox = Contraption.Instance.GetGearbox(m_part);
			if (gearbox.m_partTier != BasePart.PartTier.Regular)
			{
				BasePart.GridRotation gridRotation = m_part.m_gridRotation;
				bool flag = gearbox.IsEnabled() ^ (gridRotation == BasePart.GridRotation.Deg_0 || gridRotation == BasePart.GridRotation.Deg_45 || gridRotation == BasePart.GridRotation.Deg_90 || gridRotation == BasePart.GridRotation.Deg_135);
				m_sides = (flag ? 1 : 2);
			}
		}
	}

	private float GetPowerConsumption(float velocity, float mass)
	{
		float num = 2f * mass / (2f + mass);
		float num2 = Math.Abs(velocity);
		return 0.75f * (0.5f * num * num2 * num2 + 8f * num * num2);
	}

	public void HandleCollision(ref EntityLightManager.CCDData data, ref EntityLightManager.TOIResult result)
	{
		BasePart component = data.Rigidbody.GetComponent<BasePart>();
		if (!m_ignoreCollision || !(component != null) || component.ConnectedComponent != m_part.ConnectedComponent)
		{
			int type = Type;
			if (type == 0 || type == 1)
			{
				HandlePillarCollision(ref data, ref result);
			}
			else
			{
				HandleShieldAndBoxCollision(ref data, ref result);
			}
		}
	}

	private void HandlePillarCollision(ref EntityLightManager.CCDData data, ref EntityLightManager.TOIResult result)
	{
		BasePart component = data.Rigidbody.GetComponent<BasePart>();
		if (!m_ignoreCollision || !(component != null) || component.ConnectedComponent != m_part.ConnectedComponent)
		{
			INBounds bounds = data.Bounds;
			float timeOfImpact = result.TimeOfImpact;
			float x = result.ContactNormal.x;
			float y = result.ContactNormal.y;
			float x2 = result.RelativeVelocity.x;
			float y2 = result.RelativeVelocity.y;
			Vector2 value = Vector.InvTransform2(data.Direction1, m_data.Direction1);
			Vector2 vector = Vector.Transform2(value, result.ContactPoint);
			if (bounds.IsCircle)
			{
				vector = new Vector2(0f, 0f - bounds.R);
			}
			float x3 = vector.x;
			float y3 = vector.y;
			float num = 0.01f;
			bool flag = (Math.Abs(value.x) < num || Math.Abs(value.y) < num) && bounds.IsRect;
			float z = data.Rigidbody.angularVelocity.z;
			float num2 = x2 / data.DeltaTime;
			float num3 = y2 / data.DeltaTime;
			float num4 = num2 - z * y3;
			float num5 = num3 + z * x3;
			float mass = data.Rigidbody.mass;
			float electricity = 0f - GetPowerConsumption(num3, mass);
			INPhysicMaterial material = INContraption.GetMaterial(data.Rigidbody);
			float num6 = m_physicMaterial.CombineBounce(material);
			float num7 = m_physicMaterial.CombineFriction(material);
			float num8 = ((num4 > 0f) ? (-1f) : 1f) * num7 * ((num5 > 0f) ? num5 : (0f - num5));
			if ((num4 + num8 > 0f) ^ (num4 > 0f))
			{
				num8 = 0f - num4;
			}
			float num9 = (0f - (1f - timeOfImpact)) * y2 * (num6 + 1f) - result.ContactSeparation;
			Vector2 vector2 = new Vector2(x * num9, y * num9);
			float num10 = (0f - num3) * (num6 + 1f);
			Vector2 deltaVelocity = new Vector2(x * num10 + y * num8, y * num10 - x * num8);
			float num11 = (0f - x3) * ((!flag) ? num5 : (2f * z * x3)) * (num6 + 1f);
			float num12 = (0f - num8) * y3;
			data.Position0 = data.Position0 * (1f - timeOfImpact) + data.Position1 * timeOfImpact;
			data.Position1 += vector2;
			data.DeltaTime *= 1f - timeOfImpact;
			data.Time += (1f - data.Time) * timeOfImpact;
			m_manager.AddImpulse(this, new EntityLightManager.ImpulseData(this, data.Rigidbody, data.Position0, vector2, deltaVelocity, num11 + num12, electricity));
			if (component != null)
			{
				Vector2 contactPoint = data.Position1 + new Vector2(x * y3, y * y3);
				SendLightEvent(data.Rigidbody, component, new EntityLightCollision(contactPoint, vector2, deltaVelocity));
			}
		}
	}

	private void HandleShieldAndBoxCollision(ref EntityLightManager.CCDData data, ref EntityLightManager.TOIResult result)
	{
		BasePart component = data.Rigidbody.GetComponent<BasePart>();
		if (!m_ignoreCollision || !(component != null) || component.ConnectedComponent != m_part.ConnectedComponent)
		{
			LightData data2 = m_data;
			Vector2 vector = data.Position0 - data2.Position0;
			Vector2 vector2 = data.Position1 - data2.Position1;
			Vector2 vector3 = Vector.Transform2(result.ContactNormal, data.Direction1);
			Vector.Transform2(result.ContactPoint, data.Direction1);
			float timeOfImpact = result.TimeOfImpact;
			float num = Vector.Dot2(vector2 - vector, vector3);
			float num2 = 0f - Vector.Cross2(vector2 - vector, vector3);
			float num3 = num * (1f - timeOfImpact);
			bool flag = result.ContactCount == 2;
			float z = data.Rigidbody.angularVelocity.z;
			Vector2 vector4 = Vector.Transform2(data.Direction1, result.ContactPoint);
			if (data.Bounds.IsCircle)
			{
				vector4 = new Vector2(0f, 0f - data.Bounds.R);
			}
			float x = vector4.x;
			float y = vector4.y;
			_ = num2 / data.DeltaTime;
			float num4 = num / data.DeltaTime;
			float num5 = num4 + z * x;
			float electricity = 0f - GetPowerConsumption(num4, data.Rigidbody.mass);
			INPhysicMaterial material = INContraption.GetMaterial(data.Rigidbody);
			float num6 = m_physicMaterial.CombineBounce(material);
			float num7 = 0f - result.ContactSeparation;
			Vector2 vector5 = num7 * vector3;
			float num8 = (0f - num3) * (num6 + 1f) + num7;
			float num9 = (0f - num4) * (num6 + 1f);
			if (m_type == 3)
			{
				float num10 = 0.5f * num2 * num2 / (m_length - m_halfWidth);
				num8 += (1f - timeOfImpact) * num10;
				num9 += num10 / data.DeltaTime;
			}
			Vector2 vector6 = vector3 * num8;
			Vector2 deltaVelocity = vector3 * num9;
			float torque = (0f - x) * ((!flag) ? num5 : (2f * z * x)) * (num6 + 1f);
			data.Position0 = data.Position0 * (1f - timeOfImpact) + data.Position1 * timeOfImpact + vector5;
			data.Position1 += vector6;
			data.DeltaTime *= 1f - timeOfImpact;
			data.Time += (1f - data.Time) * timeOfImpact;
			m_manager.AddImpulse(this, new EntityLightManager.ImpulseData(this, data.Rigidbody, data.Position0, vector6, deltaVelocity, torque, electricity));
			SendLightEvent(data.Rigidbody, new EntityLightCollision(data.Position1, vector6, deltaVelocity));
		}
	}

	private void SendLightEvent(Rigidbody rigidbody, EntityLightCollision collision)
	{
		SendLightEvent(rigidbody, rigidbody.GetComponent<BasePart>(), collision);
	}

	private void SendLightEvent(Rigidbody rigidbody, BasePart part, EntityLightCollision collision)
	{
		if (part != null)
		{
			part.OnLightEnter(collision);
		}
		if (INSettings.GetBool(INFeature.CanLightTriggerExplosion))
		{
			ExplodingGrapplingHookProjectile component = rigidbody.GetComponent<ExplodingGrapplingHookProjectile>();
			if (component != null)
			{
				component.Explode();
			}
		}
	}

	private float m_builtLength = float.NaN;

	private float m_builtHalfWidth = float.NaN;

	private float m_builtAngle = float.NaN;

	private int m_builtType = -1;

	// 外部（如长按参数面板）改了数值后重建网格/碰撞盒，保证建造界面的显示同步
	private void RebuildMeshIfChanged()
	{
		if (m_meshFilter == null)
		{
			return;
		}
		if (m_builtLength == m_length && m_builtHalfWidth == m_halfWidth && m_builtAngle == m_angle && m_builtType == m_type)
		{
			return;
		}
		m_builtLength = m_length;
		m_builtHalfWidth = m_halfWidth;
		m_builtAngle = m_angle;
		m_builtType = m_type;
		if (m_type == 0 || m_type == 1)
		{
			m_meshFilter.sharedMesh = MeshExtensions.CreateRectMesh(m_length, m_halfWidth);
		}
		else
		{
			m_cos = Mathf.Cos(m_angle * (MathF.PI / 360f));
			m_meshFilter.sharedMesh = MeshExtensions.CreateCircleMesh(m_length, m_halfWidth, m_angle, 150);
		}
		if (m_collider is BoxCollider boxCollider)
		{
			boxCollider.size = new Vector3(m_length, m_halfWidth * 2f, 1f);
			boxCollider.center = new Vector3(m_length * 0.5f, 0f, 0.5f);
		}
	}

	private void Update()
	{
		RebuildMeshIfChanged();
		if (!Contraption.Instance.IsRunning)
		{
			Color color;
			if (m_type == 4)
			{
				color = default(Color);
			}
			else if (m_part.m_enclosedInto != null)
			{
				color = ((!INSettings.GetBool(INFeature.ColoredFrame) || !(m_part.m_enclosedInto is ColoredFrame coloredFrame)) ? new Color(1f, 1f, 1f, 0.15f) : coloredFrame.Color.WithAlpha(0.15f));
			}
			else
			{
				color = default(Color);
				Contraption instance = Contraption.Instance;
				int num = 1;
				int num2 = 0;
				for (int i = 0; i < 4; i++)
				{
					BasePart part = instance.FindPartAt(m_part.m_coordX + num, m_part.m_coordY + num2);
					if (instance.CanConnectTo(m_part, part, (BasePart.Direction)i))
					{
						color = new Color(1f, 1f, 1f, 0.15f);
						break;
					}
					int num3 = num;
					num = -num2;
					num2 = num3;
				}
			}
			m_meshRenderer.material.color = color;
		}
		else if (!m_enabled)
		{
			m_meshRenderer.material.color = Color.clear;
		}
		else
		{
			Color color2 = m_color;
			Color a = (m_colored ? m_color.WithAlpha(0.1f) : new Color(1f, 0.25f, 0.25f, 0.1f));
			if (m_type == 4)
			{
				color2.a /= m_componentSize;
				a.a /= m_componentSize;
			}
			if (!m_manager.ConsumePower)
			{
				m_meshRenderer.material.color = color2;
				return;
			}
			float num4 = m_manager.m_electricities[m_index] / m_manager.m_capacities[m_index];
			m_meshRenderer.material.color = Color.Lerp(a, color2, (num4 > 0f) ? Mathf.Sqrt(num4) : 0f);
		}
	}

	public static bool RaycastWithLights(Vector3 origin, Vector3 direction, out RaycastHit hitInfo, float maxDistance, int layerMask)
	{
		RaycastHit[] array = Physics.RaycastAll(origin, direction, maxDistance, layerMask);
		int num = -1;
		float num2 = float.PositiveInfinity;
		for (int i = 0; i < array.Length; i++)
		{
			RaycastHit raycastHit = array[i];
			if (raycastHit.distance < num2 && CheckRaycast(in raycastHit))
			{
				num2 = raycastHit.distance;
				num = i;
			}
		}
		if (num == -1)
		{
			hitInfo = default(RaycastHit);
			return false;
		}
		hitInfo = array[num];
		return true;
	}

	public static bool CheckRaycast(in RaycastHit raycastHit)
	{
		Rigidbody rigidbody = raycastHit.rigidbody;
		if (rigidbody == null)
		{
			return true;
		}
		EntityLight component = rigidbody.GetComponent<EntityLight>();
		if (component == null || !component.IsLightPillar || !Contraption.Instance.ConnectedToGearbox(component.m_part))
		{
			return true;
		}
		Gearbox gearbox = Contraption.Instance.GetGearbox(component.m_part);
		if (gearbox.m_partTier == BasePart.PartTier.Regular)
		{
			return true;
		}
		Vector2 direction = component.m_data.Direction1;
		Vector2 position = component.m_data.Position1;
		Vector3 point = raycastHit.point;
		bool num = direction.x * (point.y - position.y) - direction.y * (point.x - position.x) > 0f;
		BasePart.GridRotation gridRotation = component.m_part.m_gridRotation;
		return num ^ gearbox.IsEnabled() ^ (gridRotation == BasePart.GridRotation.Deg_0 || gridRotation == BasePart.GridRotation.Deg_45 || gridRotation == BasePart.GridRotation.Deg_90 || gridRotation == BasePart.GridRotation.Deg_135);
	}
}
