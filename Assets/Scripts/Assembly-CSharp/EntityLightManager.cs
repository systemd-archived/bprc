using System;
using System.Collections.Generic;
using UnityEngine;

public class EntityLightManager : PartManager
{
	public struct RigidbodyData
	{
		public Rigidbody Rigidbody;

		public Vector2 Position;

		public Vector2 Direction;

		public float DeltaTime;

		public RigidbodyData(Rigidbody rigidbody, Vector2 position, float deltaTime)
		{
			Quaternion rotation = rigidbody.rotation;
			Rigidbody = rigidbody;
			Position = position;
			Direction.x = 1f - (rotation.y * rotation.y * 2f + rotation.z * rotation.z * 2f);
			Direction.y = rotation.x * rotation.y * 2f + rotation.w * rotation.z * 2f;
			DeltaTime = deltaTime;
		}
	}

	public readonly struct TOIResult
	{
		public readonly float TimeOfImpact;

		public readonly int ContactCount;

		public readonly Vector2 ContactPoint;

		public readonly Vector2 ContactNormal;

		public readonly float ContactSeparation;

		public readonly Vector2 RelativeVelocity;

		public static TOIResult Default => default(TOIResult);

		public TOIResult(float timeOfImpact)
			: this(timeOfImpact, 0, default(Vector2), default(Vector2), 0f, default(Vector2))
		{
		}

		public TOIResult(float timeOfImpact, int contactCount, Vector2 contactPoint, Vector2 contactNormal, float contactSeparation, Vector2 relativeVelocity)
		{
			TimeOfImpact = timeOfImpact;
			ContactCount = contactCount;
			ContactPoint = contactPoint;
			ContactNormal = contactNormal;
			ContactSeparation = contactSeparation;
			RelativeVelocity = relativeVelocity;
		}
	}

	public struct CCDData
	{
		public Rigidbody Rigidbody;

		public Vector2 Position0;

		public Vector2 Direction0;

		public Vector2 Position1;

		public Vector2 Direction1;

		public INBounds Bounds;

		public float Time;

		public float DeltaTime;

		public CCDData(RigidbodyData data)
		{
			this = default(CCDData);
			Set(data);
		}

		public void Set(RigidbodyData data)
		{
			Rigidbody rigidbody = data.Rigidbody;
			Vector3 position = rigidbody.position;
			Quaternion rotation = rigidbody.rotation;
			INBounds bounds = INContraption.GetBounds(rigidbody);
			Rigidbody = rigidbody;
			Direction0 = data.Direction;
			Position0.x = data.Position.x + Direction0.x * bounds.X - Direction0.y * bounds.Y;
			Position0.y = data.Position.y + Direction0.x * bounds.Y + Direction0.y * bounds.X;
			Direction1.x = 1f - (rotation.y * rotation.y * 2f + rotation.z * rotation.z * 2f);
			Direction1.y = rotation.x * rotation.y * 2f + rotation.w * rotation.z * 2f;
			Position1.x = position.x + Direction1.x * bounds.X - Direction1.y * bounds.Y;
			Position1.y = position.y + Direction1.x * bounds.Y + Direction1.y * bounds.X;
			Bounds = bounds;
			Time = 0f;
			DeltaTime = data.DeltaTime;
		}
	}

	public readonly struct ImpulseData
	{
		public readonly EntityLight Light;

		public readonly Rigidbody Rigidbody;

		public readonly Vector2 Position;

		public readonly Vector2 DeltaPosition;

		public readonly Vector2 DeltaVelocity;

		public readonly float Torque;

		public readonly float Electricity;

		public ImpulseData(EntityLight light, Rigidbody rigidbody, Vector2 position, Vector2 deltaPosition, Vector2 deltaVelocity, float torque, float electricity)
		{
			Light = light;
			Rigidbody = rigidbody;
			Position = position;
			DeltaPosition = deltaPosition;
			DeltaVelocity = deltaVelocity;
			Torque = torque;
			Electricity = electricity;
		}
	}

	private static EntityLightManager s_instance;

	public float[] m_electricities;

	public float[] m_capacities;

	private bool m_consumePower;

	private int[] m_lightCounts;

	private int m_rigidbodyCount;

	private int m_componentCount;

	private int m_alienComponentCount;

	private float m_maxDetectionDistance;

	private float m_maxTorque;

	private RigidbodyData[] m_rigidbodyData;

	private List<EntityLight> m_lights;

	private List<ImpulseData>[] m_lightImpulses;

	public bool ConsumePower => m_consumePower;

	public override StatusCode Status => StatusCode.Running;

	public static EntityLightManager Instance => s_instance;

	protected override void Initialize()
	{
		base.Initialize();
		s_instance = this;
	}

	public override void Start()
	{
		m_consumePower = !Contraption.Instance.HasTurboCharge;
		m_rigidbodyCount = 0;
		m_rigidbodyData = Array.Empty<RigidbodyData>();
		m_lights = new List<EntityLight>();
		m_lightImpulses = Array.Empty<List<ImpulseData>>();
		m_maxDetectionDistance = 40f;
		m_maxTorque = 1f;
	}

	public override void FixedUpdate()
	{
		m_lights.Clear();
		foreach (BasePart part in Contraption.Instance.Parts)
		{
			if (part is PointLight pointLight && pointLight.EntityLight != null)
			{
				m_lights.Add(pointLight.EntityLight);
			}
			else if (part is SpotLight spotLight && spotLight.EntityLight != null)
			{
				m_lights.Add(spotLight.EntityLight);
			}
		}
		_ = m_lights.Count;
		List<EntityLight> lights = m_lights;
		UpdateLights(lights);
		UpdateAlienLights(lights);
		foreach (EntityLight item in lights)
		{
			if (item.Type != 4)
			{
				item.Data.Set(item);
			}
			item.UpdateSelf();
		}
		CCDData data = default(CCDData);
		bool[] array = new bool[m_componentCount];
		List<EntityLight> list = new List<EntityLight>();
		for (int i = 0; i < m_rigidbodyCount; i++)
		{
			RigidbodyData data2 = m_rigidbodyData[i];
			if (data2.Rigidbody == null || data2.Rigidbody.IsFixed())
			{
				continue;
			}
			data.Set(data2);
			list.Clear();
			Array.Clear(array, 0, array.Length);
			foreach (EntityLight item2 in lights)
			{
				if (!item2.Enabled)
				{
					continue;
				}
				if (item2.Type == 4)
				{
					if (array[item2.Index / 5])
					{
						continue;
					}
					array[item2.Index / 5] = true;
				}
				if (BroadPhaseDetect(item2, ref data))
				{
					list.Add(item2);
				}
			}
			int num = 4;
			int count = list.Count;
			int num2 = -1;
			int num3 = -1;
			for (int j = 0; j < num; j++)
			{
				num3 = -1;
				TOIResult result = TOIResult.Default;
				for (int k = 0; k < count; k++)
				{
					if (k != num2 && ComputeTimeOfImpact(list[k], ref data, out var result2) && (num3 == -1 || result2.TimeOfImpact < result.TimeOfImpact))
					{
						num3 = k;
						result = result2;
					}
				}
				if (num3 == -1)
				{
					break;
				}
				num2 = num3;
				list[num3].HandleCollision(ref data, ref result);
			}
		}
		for (int l = 0; l < m_lightImpulses.Length; l++)
		{
			float num4 = 0f;
			List<ImpulseData> list2 = m_lightImpulses[l];
			foreach (ImpulseData item3 in list2)
			{
				num4 -= item3.Electricity;
			}
			float val = Math.Min(num4, m_electricities[l]);
			float num5 = ((num4 > 0f && m_consumePower) ? (Math.Max(val, 0f) / num4) : 1f);
			foreach (ImpulseData item4 in list2)
			{
				EntityLight light = item4.Light;
				Rigidbody rigidbody = item4.Rigidbody;
				float num6 = item4.Electricity * num5 * num5;
				float num7 = num5 * Defend(item4.Light.Data.Position1, item4.Position, num6, m_electricities[light.Index]);
				if (light.Type == 4)
				{
					foreach (EntityLight item5 in lights)
					{
						if (item5.Index == light.Index)
						{
							item5.m_electricity += (m_consumePower ? (num6 / (float)light.m_componentSize) : 0f);
						}
					}
				}
				else
				{
					light.m_electricity += (m_consumePower ? num6 : 0f);
				}
				Vector3 position = rigidbody.position + (Vector3)(item4.DeltaPosition * num7);
				Vector3 vector = item4.DeltaVelocity * num7;
				rigidbody.position = position;
				if (INSettings.GetBool(INFeature.ReactionLight))
				{
					Rigidbody rigidbody2 = light.Part.rigidbody;
					float num8 = rigidbody.mass * rigidbody2.mass / (rigidbody.mass + rigidbody2.mass);
					rigidbody.EngineAddForce(num8 * vector, ForceMode.Impulse);
					rigidbody2.EngineAddForce((0f - num8) * vector, ForceMode.Impulse);
				}
				else
				{
					rigidbody.EngineAddForce(vector, ForceMode.VelocityChange);
				}
				float torque = item4.Torque;
				torque = Math.Clamp(torque, 0f - m_maxTorque, m_maxTorque);
				rigidbody.EngineAddTorque(new Vector3(0f, 0f, torque), ForceMode.VelocityChange);
			}
		}
		Rigidbody[] components = INContraption.Instance.Rigidbodies;
		m_rigidbodyCount = components.Length;
		if (m_rigidbodyData.Length < m_rigidbodyCount)
		{
			m_rigidbodyData = new RigidbodyData[m_rigidbodyCount];
		}
		for (int m = 0; m < m_rigidbodyCount; m++)
		{
			Rigidbody rigidbody3 = components[m];
			m_rigidbodyData[m] = new RigidbodyData(rigidbody3, rigidbody3.position, Time.fixedDeltaTime);
		}
	}

	private bool BroadPhaseDetect(EntityLight light, ref CCDData data)
	{
		int type = light.Type;
		EntityLight.LightData data2 = light.Data;
		if (type == 0 || type == 1)
		{
			float valueX = data.Position0.x - data2.Position0.x;
			float valueY = data.Position0.y - data2.Position0.y;
			float valueX2 = data.Position1.x - data2.Position1.x;
			float valueY2 = data.Position1.y - data2.Position1.y;
			float x = data2.Direction0.x;
			float y = data2.Direction0.y;
			float x2 = data2.Direction1.x;
			float y2 = data2.Direction1.y;
			Vector.InvTransform2(valueX, valueY, x, y, out var resultX, out var resultY);
			Vector.InvTransform2(valueX2, valueY2, x2, y2, out var resultX2, out var resultY2);
			float maxDetectionDistance = m_maxDetectionDistance;
			float length = light.Length;
			float halfWidth = light.HalfWidth;
			if ((resultX < 0f - maxDetectionDistance || resultX > length + maxDetectionDistance) && (resultX2 < 0f - maxDetectionDistance || resultX2 > length + maxDetectionDistance))
			{
				return false;
			}
			if (Math.Abs(resultY) > halfWidth + maxDetectionDistance && Math.Abs(resultY2) > halfWidth + maxDetectionDistance)
			{
				return false;
			}
			return true;
		}
		float num = data.Position0.x - data2.Position0.x;
		float num2 = data.Position1.x - data2.Position1.x;
		float num3 = data.Position1.y - data2.Position1.y;
		float num4 = num * num + num * num;
		float num5 = num2 * num2 + num3 * num3;
		float num6 = light.Length + m_maxDetectionDistance;
		float num7 = num6 * num6;
		if (num4 > num7 && num5 > num7)
		{
			return false;
		}
		return true;
	}

	private bool ComputeTimeOfImpact(EntityLight light, ref CCDData data, out TOIResult result)
	{
		switch (light.Type)
		{
		case 0:
		case 1:
			return ComputeTimeOfImpactRect(light, ref data, out result);
		case 2:
		case 4:
			return ComputeTimeOfImpactOuterCircle(light, ref data, out result);
		default:
			return ComputeTimeOfImpactInnerCircle(light, ref data, out result);
		}
	}

	private bool ComputeTimeOfImpactRect(EntityLight light, ref CCDData data, out TOIResult result)
	{
		result = TOIResult.Default;
		EntityLight.LightData data2 = light.Data;
		float length = light.Length;
		float valueX = data.Position0.x - data2.Position0.x;
		float valueY = data.Position0.y - data2.Position0.y;
		float valueX2 = data.Position1.x - data2.Position1.x;
		float valueY2 = data.Position1.y - data2.Position1.y;
		float x = data2.Direction0.x;
		float y = data2.Direction0.y;
		float x2 = data2.Direction1.x;
		float y2 = data2.Direction1.y;
		Vector.InvTransform2(valueX, valueY, x, y, out var resultX, out var resultY);
		Vector.InvTransform2(valueX2, valueY2, x2, y2, out var resultX2, out var resultY2);
		if ((!(0f < resultX) || !(resultX < length)) && (!(0f < resultX2) || !(resultX2 < length)))
		{
			return false;
		}
		int sides = light.Sides;
		float halfWidth = light.HalfWidth;
		INBounds bounds = data.Bounds;
		Vector.InvTransform2(data.Direction1, data2.Direction1, out var resultX3, out var resultY3);
		float halfProjection = bounds.GetHalfProjection(0f - resultY3, resultX3);
		float num = resultY - halfProjection - halfWidth;
		float num2 = resultY2 - halfProjection - halfWidth;
		float num3 = resultY + halfProjection + halfWidth;
		float num4 = resultY2 + halfProjection + halfWidth;
		bool flag = num > -0.25f && num2 < 0f && num2 < num;
		bool flag2 = num3 < 0.25f && num4 > 0f && num4 > num3;
		if (sides != 0)
		{
			flag = flag && (sides & 1) == 0;
			flag2 = flag2 && (sides & 2) == 0;
		}
		if (!flag && !flag2)
		{
			return false;
		}
		int num5 = (flag ? 1 : (-1));
		float num6 = (flag ? num : (0f - num3));
		float num7 = (flag ? num2 : (0f - num4));
		float num8 = (0f - num6) / (num7 - num6);
		float contactSeparation = 0f;
		if (Math.Abs(num7 - num6) < 1E-05f || num8 < 0f)
		{
			num8 = 0f;
			contactSeparation = num7;
		}
		bounds.GetLocalContactPoint(resultX3, resultY3, out var pointX, out var pointY);
		Vector2 contactPoint = new Vector2(pointX, pointY);
		Vector2 contactNormal = new Vector2((float)(-num5) * y2, (float)num5 * x2);
		Vector2 relativeVelocity = new Vector2((float)num5 * (resultX2 - resultX), (float)num5 * (resultY2 - resultY));
		result = new TOIResult(num8, 1, contactPoint, contactNormal, contactSeparation, relativeVelocity);
		return true;
	}

	private bool ComputeTimeOfImpactOuterCircle(EntityLight light, ref CCDData data, out TOIResult result)
	{
		result = TOIResult.Default;
		EntityLight.LightData data2 = light.Data;
		float time = data.Time;
		Vector2 vector = (1f - time) * data2.Position0 + time * data2.Position1 - data.Position0;
		Vector2 vector2 = data2.Position1 - data.Position1;
		if (0f - Vector.Dot2(vector, data2.Direction0) <= light.Cos * Vector.Length2(vector) && 0f - Vector.Dot2(vector2, data2.Direction1) <= light.Cos * Vector.Length2(vector2))
		{
			return false;
		}
		float x = data.Direction1.x;
		float y = data.Direction1.y;
		float num = x * vector.x + y * vector.y;
		float num2 = x * vector.y - y * vector.x;
		float num3 = x * vector2.x + y * vector2.y;
		float num4 = x * vector2.y - y * vector2.x;
		float num5 = num3 - num;
		float num6 = num4 - num2;
		if (Vector.LengthSquared2(num5, num6) < 1E-10f)
		{
			return false;
		}
		float num7 = light.Length + light.HalfWidth;
		float num8 = num7 - 0.5f;
		float num9 = num7 * num7;
		float num10 = num8 * num8;
		INBounds bounds = data.Bounds;
		int count = 0;
		float x2 = 0f;
		float x3 = 0f;
		int count2 = 0;
		float x4 = 0f;
		float x5 = 0f;
		if (bounds.IsCircle)
		{
			LineAndCircleIntersection(num, num2, num5, num6, 0f, 0f, num7 + bounds.R, out count, out x2, out x3);
			LineAndCircleIntersection(num, num2, num5, num6, 0f, 0f, num8 + bounds.R, out count2, out x4, out x5);
		}
		else if (Math.Abs(num5) < 1E-05f)
		{
			float num11 = Math.Max(Math.Abs(num) - bounds.A, 0f);
			int num12 = ((num6 > 0f) ? 1 : (-1));
			if (num7 > num11)
			{
				float num13 = (float)num12 * ((float)Math.Sqrt(num9 - num11 * num11) + bounds.B);
				count = 2;
				x2 = (0f - num13 - num2) / num6;
				x3 = (num13 - num2) / num6;
			}
			if (num8 > num11)
			{
				float num14 = (float)num12 * ((float)Math.Sqrt(num10 - num11 * num11) + bounds.B);
				count2 = 2;
				x4 = (0f - num14 - num2) / num6;
				x5 = (num14 - num2) / num6;
			}
		}
		else if (Math.Abs(num6) < 1E-05f)
		{
			float num15 = Math.Max(Math.Abs(num2) - bounds.B, 0f);
			int num16 = ((num5 > 0f) ? 1 : (-1));
			if (num7 > num15)
			{
				float num17 = (float)num16 * ((float)Math.Sqrt(num9 - num15 * num15) + bounds.A);
				count = 2;
				x2 = (0f - num17 - num) / num5;
				x3 = (num17 - num) / num5;
			}
			if (num8 > num15)
			{
				float num18 = (float)num16 * ((float)Math.Sqrt(num10 - num15 * num15) + bounds.A);
				count2 = 2;
				x4 = (0f - num18 - num) / num5;
				x5 = (num18 - num) / num5;
			}
		}
		else
		{
			float item = (0f - bounds.A - num) / num5;
			float item2 = (bounds.A - num) / num5;
			float item3 = (0f - bounds.B - num2) / num6;
			float item4 = (bounds.B - num2) / num6;
			SortFour((item, 0), (item2, 1), (item3, 2), (item4, 3), out var b, out var b2, out var b3, out var b4);
			int num19 = -1;
			int num20 = -1;
			float num21 = float.NegativeInfinity;
			for (int i = 0; i < 5; i++)
			{
				(float, int) tuple = ((i >= 2) ? ((i == 2) ? b3 : b4) : ((i == 0) ? b : b2));
				float num22;
				if (i < 4)
				{
					(num22, _) = tuple;
				}
				else
				{
					num22 = float.PositiveInfinity;
				}
				float num23 = num22;
				int num24 = ((num5 > 0f) ? num19 : (-num19));
				int num25 = ((num6 > 0f) ? num20 : (-num20));
				float num26 = (float)num24 * num5;
				float num27 = (float)num24 * num - bounds.A;
				float num28 = (float)num25 * num6;
				float num29 = (float)num25 * num2 - bounds.B;
				if (num24 != 0 && num25 != 0)
				{
					QuadraticFunction quadraticFunction = new QuadraticFunction(num26 * num26 + num28 * num28, 2f * (num26 * num27 + num28 * num29), num27 * num27 + num29 * num29);
					QuadraticFunction.IntersectResult intersections = quadraticFunction.GetIntersections(num9);
					QuadraticFunction.IntersectResult intersections2 = quadraticFunction.GetIntersections(num10);
					for (int j = 0; j < intersections.Count; j++)
					{
						float num30 = intersections[j];
						if (num21 <= num30 && num30 < num23)
						{
							SetResult(num30, ref count, ref x2, ref x3);
						}
					}
					for (int k = 0; k < intersections2.Count; k++)
					{
						float num31 = intersections2[k];
						if (num21 <= num31 && num31 < num23)
						{
							SetResult(num31, ref count2, ref x4, ref x5);
						}
					}
				}
				else if (num24 != 0 && num25 == 0)
				{
					float num32 = (num7 - num27) / num26;
					float num33 = (num8 - num27) / num26;
					if (num21 <= num32 && num32 < num23)
					{
						SetResult(num32, ref count, ref x2, ref x3);
					}
					if (num21 <= num33 && num33 < num23)
					{
						SetResult(num33, ref count2, ref x4, ref x5);
					}
				}
				else if (num24 == 0 && num25 != 0)
				{
					float num34 = (num7 - num29) / num28;
					float num35 = (num8 - num29) / num28;
					if (num21 <= num34 && num34 < num23)
					{
						SetResult(num34, ref count, ref x2, ref x3);
					}
					if (num21 <= num35 && num35 < num23)
					{
						SetResult(num35, ref count2, ref x4, ref x5);
					}
				}
				num21 = num23;
				if (tuple.Item2 < 2)
				{
					num19++;
				}
				else
				{
					num20++;
				}
			}
		}
		if (count == 2 && ((count2 == 2) ? x4 : x3) >= 0f && x2 <= 1f)
		{
			float num36 = ((x2 > 0f) ? x2 : 0f);
			float num37 = num + num5 * num36;
			float num38 = num2 + num6 * num36;
			Vector2 vector3 = new Vector2(num37, num38);
			Vector2 vector4 = default(Vector2);
			Vector2 vector5 = default(Vector2);
			float num39 = 0f;
			if (bounds.IsCircle)
			{
				num39 = Vector.Length2(vector3);
				vector4 = bounds.R / num39 * vector3;
				vector5 = -1f / num39 * vector3;
				num39 -= bounds.R;
			}
			else
			{
				int num40 = ((num37 < 0f - bounds.A) ? (-1) : ((!(num37 < bounds.A)) ? 1 : 0));
				int num41 = ((num38 < 0f - bounds.B) ? (-1) : ((!(num38 < bounds.B)) ? 1 : 0));
				if (num40 == 0 && num41 == 0)
				{
					return false;
				}
				if (num40 != 0 && num41 != 0)
				{
					vector4 = new Vector2((float)num40 * bounds.A, (float)num41 * bounds.B);
					vector5 = vector4 - vector3;
					num39 = Vector.Length2(vector5);
					vector5 /= num39;
				}
				else if (num40 == 0 && num41 != 0)
				{
					vector4 = new Vector2(num37, (float)num41 * bounds.B);
					vector5 = new Vector2(0f, -num41);
					num39 = Math.Abs(num38) - bounds.B;
				}
				else if (num40 != 0 && num41 == 0)
				{
					vector4 = new Vector2((float)num40 * bounds.A, num38);
					vector5 = new Vector2(-num40, 0f);
					num39 = Math.Abs(num37) - bounds.A;
				}
			}
			result = new TOIResult(num36, 1, vector4, vector5, num39 - num7, default(Vector2));
			return true;
		}
		return false;
		static void SetResult(float value, ref int reference, ref float reference2, ref float reference3)
		{
			if (reference == 0)
			{
				reference = 1;
				reference2 = value;
			}
			else if (reference == 1 && Math.Abs(value - reference2) > 1E-05f)
			{
				reference = 2;
				if (value >= reference2)
				{
					reference3 = value;
				}
				else
				{
					reference3 = reference2;
					reference2 = value;
				}
			}
		}
	}

	private bool ComputeTimeOfImpactInnerCircle(EntityLight light, ref CCDData data, out TOIResult result)
	{
		result = TOIResult.Default;
		EntityLight.LightData data2 = light.Data;
		float time = data.Time;
		Vector2 vector = (1f - time) * data2.Position0 + time * data2.Position1 - data.Position0;
		Vector2 vector2 = data2.Position1 - data.Position1;
		if (0f - Vector.Dot2(vector, data2.Direction0) <= light.Cos * Vector.Length2(vector) && 0f - Vector.Dot2(vector2, data2.Direction1) <= light.Cos * Vector.Length2(vector2))
		{
			return false;
		}
		float x = data.Direction1.x;
		float y = data.Direction1.y;
		float num = x * vector.x + y * vector.y;
		float num2 = x * vector.y - y * vector.x;
		float num3 = x * vector2.x + y * vector2.y;
		float num4 = x * vector2.y - y * vector2.x;
		float num5 = num3 - num;
		float num6 = num4 - num2;
		if (Vector.LengthSquared2(num5, num6) < 1E-10f)
		{
			return false;
		}
		float num7 = light.Length - light.HalfWidth;
		float num8 = num7 + 0.5f;
		float num9 = num7 * num7;
		float num10 = num8 * num8;
		INBounds bounds = data.Bounds;
		int count = 0;
		float x2 = 0f;
		float x3 = 0f;
		int count2 = 0;
		float x4 = 0f;
		float x5 = 0f;
		if (bounds.IsCircle)
		{
			LineAndCircleIntersection(num, num2, num5, num6, 0f, 0f, num7 - bounds.R, out count, out x2, out x3);
			LineAndCircleIntersection(num, num2, num5, num6, 0f, 0f, num8 - bounds.R, out count2, out x4, out x5);
		}
		else if (Math.Abs(num5) < 1E-05f)
		{
			float num11 = Math.Abs(num) + bounds.A;
			int num12 = ((num6 > 0f) ? 1 : (-1));
			if (num7 > num11)
			{
				float num13 = (float)num12 * ((float)Math.Sqrt(num9 - num11 * num11) - bounds.B);
				count = 2;
				x2 = (0f - num13 - num2) / num6;
				x3 = (num13 - num2) / num6;
			}
			if (num8 > num11)
			{
				float num14 = (float)num12 * ((float)Math.Sqrt(num10 - num11 * num11) - bounds.B);
				count2 = 2;
				x4 = (0f - num14 - num2) / num6;
				x5 = (num14 - num2) / num6;
			}
		}
		else if (Math.Abs(num6) < 1E-05f)
		{
			float num15 = Math.Abs(num2) + bounds.B;
			int num16 = ((num5 > 0f) ? 1 : (-1));
			if (num7 > num15)
			{
				float num17 = (float)num16 * ((float)Math.Sqrt(num9 - num15 * num15) - bounds.A);
				count = 2;
				x2 = (0f - num17 - num) / num5;
				x3 = (num17 - num) / num5;
			}
			if (num8 > num15)
			{
				float num18 = (float)num16 * ((float)Math.Sqrt(num10 - num15 * num15) - bounds.A);
				count2 = 2;
				x4 = (0f - num18 - num) / num5;
				x5 = (num18 - num) / num5;
			}
		}
		else
		{
			float item = (0f - num) / num5;
			float item2 = (0f - num2) / num6;
			(float, int) tuple = (item, 0);
			(float, int) tuple2 = (item2, 1);
			if (tuple.CompareTo(tuple2) > 0)
			{
				(float, int) tuple3 = tuple;
				tuple = tuple2;
				tuple2 = tuple3;
			}
			int num19 = -1;
			int num20 = -1;
			float num21 = float.NegativeInfinity;
			for (int i = 0; i < 3; i++)
			{
				(float, int) tuple4 = ((i == 0) ? tuple : tuple2);
				float num22;
				if (i < 2)
				{
					(num22, _) = tuple4;
				}
				else
				{
					num22 = float.PositiveInfinity;
				}
				float num23 = num22;
				int num24 = ((num5 > 0f) ? num19 : (-num19));
				int num25 = ((num6 > 0f) ? num20 : (-num20));
				float num26 = (float)num24 * num5;
				float num27 = (float)num24 * num + bounds.A;
				float num28 = (float)num25 * num6;
				float num29 = (float)num25 * num2 + bounds.B;
				QuadraticFunction quadraticFunction = new QuadraticFunction(num26 * num26 + num28 * num28, 2f * (num26 * num27 + num28 * num29), num27 * num27 + num29 * num29);
				QuadraticFunction.IntersectResult intersections = quadraticFunction.GetIntersections(num9);
				QuadraticFunction.IntersectResult intersections2 = quadraticFunction.GetIntersections(num10);
				for (int j = 0; j < intersections.Count; j++)
				{
					float num30 = intersections[j];
					if (num21 <= num30 && num30 < num23)
					{
						SetResult(num30, ref count, ref x2, ref x3);
					}
				}
				for (int k = 0; k < intersections2.Count; k++)
				{
					float num31 = intersections2[k];
					if (num21 <= num31 && num31 < num23)
					{
						SetResult(num31, ref count2, ref x4, ref x5);
					}
				}
				num21 = num23;
				if (tuple4.Item2 == 0)
				{
					num19 += 2;
				}
				else
				{
					num20 += 2;
				}
			}
		}
		if (count2 == 2 && x5 >= 0f && ((count == 2) ? x3 : x4) <= 1f)
		{
			float num32 = ((count == 2 && x3 > 0f) ? x3 : 0f);
			float num33 = num + num5 * num32;
			float num34 = num2 + num6 * num32;
			int contactCount = 1;
			Vector2 vector3 = new Vector2(num33, num34);
			Vector2 vector4;
			Vector2 vector5;
			float num35;
			if (bounds.IsCircle)
			{
				num35 = Vector.Length2(vector3);
				vector4 = (0f - bounds.R) / num35 * vector3;
				vector5 = 1f / num35 * vector3;
				num35 += bounds.R;
			}
			else
			{
				int num36 = ((!(num33 < 0f)) ? 1 : (-1));
				int num37 = ((!(num34 < 0f)) ? 1 : (-1));
				num35 = Vector.Length2(num33 + (float)num36 * bounds.A, num34 + (float)num37 * bounds.B);
				if (Math.Abs(num33) / num7 < 0.02f)
				{
					contactCount = 2;
					num36 = 0;
				}
				else if (Math.Abs(num34) / num7 < 0.02f)
				{
					contactCount = 2;
					num37 = 0;
				}
				vector4 = new Vector2((float)(-num36) * bounds.A, (float)(-num37) * bounds.B);
				vector5 = vector3 - vector4;
				vector5 /= Vector.Length2(vector5);
			}
			result = new TOIResult(num32, contactCount, vector4, vector5, num7 - num35, default(Vector2));
			return true;
		}
		return false;
		static void SetResult(float value, ref int reference, ref float reference2, ref float reference3)
		{
			if (reference == 0)
			{
				reference = 1;
				reference2 = value;
			}
			else if (reference == 1 && Math.Abs(value - reference2) > 1E-05f)
			{
				reference = 2;
				if (value >= reference2)
				{
					reference3 = value;
				}
				else
				{
					reference3 = reference2;
					reference2 = value;
				}
			}
		}
	}

	private void LineAndCircleIntersection(float pX, float pY, float dX, float dY, float cX, float cY, float r, out int resultCount, out float result1, out float result2)
	{
		float num = pX - cX;
		float num2 = pY - cY;
		float num3 = dX * num + dY * num2;
		float num4 = dX * num2 - dY * num;
		float num5 = dX * dX + dY * dY;
		float num6 = r * r * num5 - num4 * num4;
		float num7 = 1E-10f;
		if (num6 < 0f - num7)
		{
			resultCount = 0;
			result1 = float.NaN;
			result2 = float.NaN;
		}
		else if (num6 < num7)
		{
			resultCount = 1;
			result1 = (0f - num3) / num5;
			result2 = (0f - num3) / num5;
		}
		else
		{
			float num8 = (float)Math.Sqrt(num6);
			resultCount = 2;
			result1 = (0f - num3 - num8) / num5;
			result2 = (0f - num3 + num8) / num5;
		}
	}

	private void UpdateLights(List<EntityLight> lights)
	{
		int num = 0;
		int num2 = 5;
		float num3 = 22500f * INSettings.GetFloat(INFeature.LightElectricityFactor);
		float num4 = INSettings.GetFloat(INFeature.LightChargingSpeedFactor);
		float num5 = 6000f;
		List<BasePart> parts = Contraption.Instance.Parts;
		int[] array = new int[Contraption.Instance.ConnectedComponentCount];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = -1;
		}
		foreach (EntityLight light in lights)
		{
			int componentIndex = light.ComponentIndex;
			light.Enabled = light.Part.IsEnabled() && !light.Part.IsSinglePart();
			if (componentIndex != -1)
			{
				array[componentIndex] = num++;
			}
		}
		m_componentCount = num;
		foreach (EntityLight light2 in lights)
		{
			int componentIndex2 = light2.ComponentIndex;
			if (componentIndex2 != -1)
			{
				light2.Index = array[componentIndex2] * num2 + light2.Type;
			}
		}
		m_electricities = new float[num * num2];
		m_capacities = new float[num * num2];
		m_lightCounts = new int[num * num2];
		m_lightImpulses = new List<ImpulseData>[num * num2];
		float[] array2 = new float[num];
		float[] array3 = new float[num * num2];
		float[] array4 = new float[num * num2];
		for (int j = 0; j < num * num2; j++)
		{
			m_lightImpulses[j] = new List<ImpulseData>();
		}
		foreach (BasePart item in parts)
		{
			if (item is Engine)
			{
				int connectedComponent = item.ConnectedComponent;
				if (connectedComponent != -1 && array[connectedComponent] != -1)
				{
					array2[array[connectedComponent]] += num3;
				}
			}
		}
		foreach (EntityLight light3 in lights)
		{
			m_electricities[light3.Index] += light3.m_electricity;
			m_capacities[light3.Index] += num5;
			m_lightCounts[light3.Index]++;
		}
		for (int k = 0; k < num; k++)
		{
			int num6 = 0;
			for (int l = 0; l < num2; l++)
			{
				num6 += m_lightCounts[k * num2 + l];
			}
			if (num6 != 0)
			{
				for (int m = 0; m < num2; m++)
				{
					m_capacities[k * num2 + m] += array2[k] * (float)m_lightCounts[k * num2 + m] / (float)num6;
				}
			}
		}
		for (int n = 0; n < num * num2; n++)
		{
			float num7 = m_capacities[n];
			float num8 = m_electricities[n] + num7;
			float num9 = Math.Min(num8 + Mathf.Sqrt(num7) * num4, num7);
			m_electricities[n] = num9;
			array3[n] = (num9 - num8) / (float)m_lightCounts[n];
		}
		for (int num10 = 0; num10 < num; num10++)
		{
			int num11 = 0;
			float num12 = 0f;
			for (int num13 = 0; num13 < num2; num13++)
			{
				num12 += m_electricities[num10 * num2 + num13];
			}
			for (int num14 = num2; num14 > 0; num14--)
			{
				int num15 = 0;
				float num16 = float.MaxValue;
				for (int num17 = 0; num17 < num2; num17++)
				{
					if ((num11 & (1 << num17)) == 0)
					{
						float num18 = m_capacities[num10 * num2 + num17];
						if (num18 < num16)
						{
							num15 = num17;
							num16 = num18;
						}
					}
				}
				num11 |= 1 << num15;
				float num19 = ((num16 < num12 / (float)num14) ? num16 : (num12 / (float)num14));
				num12 -= num19;
				int num20 = m_lightCounts[num10 * num2 + num15];
				if (num20 != 0)
				{
					array4[num10 * num2 + num15] = (num19 - num16) / (float)num20;
				}
			}
		}
		foreach (EntityLight light4 in lights)
		{
			light4.m_electricity += array3[light4.Index];
			light4.m_electricity += (array4[light4.Index] - light4.m_electricity) * 0.04f;
			_ = m_electricities[light4.Index] / m_capacities[light4.Index];
		}
	}

	private void SortFour<T>(ref T a1, ref T a2, ref T a3, ref T a4) where T : IComparable<T>
	{
		SortFour(a1, a2, a3, a4, out var b, out var b2, out var b3, out var b4);
		a1 = b;
		a2 = b2;
		a3 = b3;
		a4 = b4;
	}

	private void SortFour<T>(T a1, T a2, T a3, T a4, out T b1, out T b2, out T b3, out T b4) where T : IComparable<T>
	{
		T val;
		T val2;
		if (a1.CompareTo(a2) <= 0)
		{
			val = a1;
			val2 = a2;
		}
		else
		{
			val = a2;
			val2 = a1;
		}
		T val3;
		T val4;
		if (a3.CompareTo(a4) <= 0)
		{
			val3 = a3;
			val4 = a4;
		}
		else
		{
			val3 = a4;
			val4 = a3;
		}
		if (val.CompareTo(val3) <= 0)
		{
			b1 = val;
			b2 = val3;
		}
		else
		{
			b1 = val3;
			b2 = val;
		}
		if (val2.CompareTo(val4) <= 0)
		{
			b3 = val2;
			b4 = val4;
		}
		else
		{
			b3 = val4;
			b4 = val2;
		}
		T other = b3;
		if (b2.CompareTo(other) > 0)
		{
			T val5 = b2;
			b2 = b3;
			b3 = val5;
		}
	}

	private void UpdateAlienLights(List<EntityLight> lights)
	{
		int num = 0;
		int[] array = new int[Contraption.Instance.ConnectedComponentCount];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = -1;
		}
		foreach (EntityLight light in lights)
		{
			int connectedComponent = light.Part.ConnectedComponent;
			if (light.Type == 4 && connectedComponent != -1)
			{
				array[connectedComponent] = num++;
			}
		}
		m_alienComponentCount = num;
		List<BasePart> parts = Contraption.Instance.Parts;
		List<BasePart>[] array2 = new List<BasePart>[num];
		List<EntityLight>[] array3 = new List<EntityLight>[num];
		for (int j = 0; j < num; j++)
		{
			array2[j] = new List<BasePart>();
			array3[j] = new List<EntityLight>();
		}
		foreach (BasePart item in parts)
		{
			if (item.ConnectedComponent != -1)
			{
				int num2 = array[item.ConnectedComponent];
				if (num2 != -1)
				{
					array2[num2].Add(item);
				}
			}
		}
		foreach (EntityLight light2 in lights)
		{
			int connectedComponent2 = light2.Part.ConnectedComponent;
			if (light2.Type == 4 && connectedComponent2 != -1)
			{
				int num3 = array[connectedComponent2];
				array3[num3].Add(light2);
			}
		}
		float fixedDeltaTime = Time.fixedDeltaTime;
		float num4 = INSettings.GetFloat(INFeature.AlienLightRadiusLimitFactor);
		for (int k = 0; k < num; k++)
		{
			int num5 = 0;
			float num6 = 0f;
			Vector2 position = default(Vector2);
			Vector2 prePosition = default(Vector2);
			List<BasePart> list = array2[k];
			foreach (BasePart item2 in list)
			{
				if (!item2.HasMultipleRigidbodies())
				{
					num5++;
					Vector3 position2 = item2.transform.position;
					Vector3 velocity = item2.rigidbody.velocity;
					position = new Vector2(position.x + position2.x, position.y + position2.y);
					prePosition = new Vector2(prePosition.x + position2.x - velocity.x * fixedDeltaTime, prePosition.y + position2.y - velocity.y * fixedDeltaTime);
				}
			}
			position /= (float)num5;
			prePosition /= (float)num5;
			foreach (BasePart item3 in list)
			{
				if (!item3.HasMultipleRigidbodies())
				{
					Vector3 position3 = item3.transform.position;
					float num7 = (position3.x - position.x) * (position3.x - position.x) + (position3.y - position.y) * (position3.y - position.y);
					num6 = ((num7 > num6) ? num7 : num6);
				}
			}
			int num8 = 0;
			float num9 = 2f * Mathf.Sqrt(list.Count) * num4;
			float num10 = Mathf.Sqrt(num6);
			num10 = ((num10 < num9) ? num10 : num9) + 4f;
			foreach (EntityLight item4 in array3[k])
			{
				if (item4.Enabled)
				{
					num8++;
				}
				Vector3 position4 = item4.Transform.position;
				item4.Transform.position = new Vector3(position.x, position.y, position4.z);
				item4.Length = num10;
				item4.HalfWidth = (num10 + 8f) / 64f;
				item4.m_meshFilter.sharedMesh.UpdateCircleMesh(item4.Length, item4.HalfWidth, item4.Angle, 150);
				item4.Data.SetPosition(position, prePosition);
			}
			int componentSize = ((num8 <= 1) ? 1 : num8);
			foreach (EntityLight item5 in array3[k])
			{
				item5.m_componentSize = componentSize;
			}
		}
	}

	public void AddImpulse(EntityLight light, ImpulseData impulseData)
	{
		m_lightImpulses[light.Index].Add(impulseData);
	}

	public float Defend(Vector2 from, Vector2 to, float work, float electricity)
	{
		EntityLight[] components = INContraption.Instance.GetComponents<EntityLight>();
		List<(int, float)> list = null;
		int[] array = null;
		float num = 0f;
		for (int i = 0; i < components.Length; i++)
		{
			EntityLight entityLight = components[i];
			if (!entityLight.Enabled || entityLight.Type != 4 || !(m_electricities[entityLight.Index] / m_capacities[entityLight.Index] > 0f))
			{
				continue;
			}
			Vector2 position = entityLight.Data.Position1;
			float num2 = position.x - from.x;
			float num3 = position.y - from.y;
			float num4 = position.x - to.x;
			float num5 = position.y - to.y;
			float num6 = entityLight.Length - entityLight.HalfWidth;
			float num7 = num6 * num6;
			if (num4 * num4 + num5 * num5 < num7 && num2 * num2 + num3 * num3 > num7)
			{
				float num8 = MathF.Sqrt(m_electricities[entityLight.Index]);
				if (list == null)
				{
					list = new List<(int, float)>();
					array = new int[m_componentCount * 5];
				}
				if (array[entityLight.Index] == 0)
				{
					num += num8;
				}
				array[entityLight.Index]++;
				list.Add((i, num8));
			}
		}
		float num9 = 0.7f;
		if (electricity > 0f)
		{
			num9 = Math.Clamp(0.4f * num / MathF.Sqrt(electricity), 0f, 0.7f);
		}
		if (m_consumePower && list != null && work < 0f)
		{
			float num10 = work * num9 / num;
			for (int j = 0; j < list.Count; j++)
			{
				(int, float) tuple = list[j];
				components[tuple.Item1].m_electricity += num10 * tuple.Item2;
			}
		}
		return 1f - num9;
	}
}
