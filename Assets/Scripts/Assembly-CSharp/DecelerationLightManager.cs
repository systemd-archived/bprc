using System.Collections.Generic;
using UnityEngine;

public class DecelerationLightManager : PartManager
{
	private static float s_force;

	private static float s_maxForce;

	private static float s_innerRadius;

	private static float s_outerRadius;
	private bool[] m_hasLight = System.Array.Empty<bool>();
	private Vector3[] m_positions = System.Array.Empty<Vector3>();
	private bool[] m_movable = System.Array.Empty<bool>();
	private readonly List<(int, int, float)> m_pairs = new List<(int, int, float)>();
	private Vector2[] m_velocities = System.Array.Empty<Vector2>();
	private float[] m_masses = System.Array.Empty<float>();
	private Vector2[] m_forces = System.Array.Empty<Vector2>();

	public override StatusCode Status => StatusCode.Running;

	protected override void Initialize()
	{
		base.Initialize();
	}

	public override void FixedUpdate()
	{
		s_force = INSettings.GetFloat(INFeature.DecelerationLightForce);
		s_maxForce = INSettings.GetFloat(INFeature.DecelerationLightMaxForce);
		s_innerRadius = INSettings.GetFloat(INFeature.DecelerationLightInnerRadius);
		s_outerRadius = INSettings.GetFloat(INFeature.DecelerationLightOuterRadius);
		Rigidbody[] components = INContraption.Instance.Rigidbodies;
		int num = components.Length;
		if (m_hasLight.Length < num)
		{
			m_hasLight = new bool[num];
			m_positions = new Vector3[num];
			m_movable = new bool[num];
			m_velocities = new Vector2[num];
			m_masses = new float[num];
			m_forces = new Vector2[num];
		}
		System.Array.Clear(m_hasLight, 0, num);
		bool[] array = m_hasLight;
		Vector3[] array2 = m_positions;
		for (int i = 0; i < num; i++)
		{
			Rigidbody rigidbody = components[i];
			BasePart component = rigidbody.GetComponent<BasePart>();
			array2[i] = rigidbody.position;
			if (component != null && component.IsDecelerationLight())
			{
				if (component.IsEnabled() && component.IsSinglePart())
				{
					component.SetEnabled(enabled: false);
				}
				array[i] = component.IsEnabled() && !component.IsSinglePart();
			}
		}
		float num2 = s_outerRadius * s_outerRadius;
		float num3 = 1f / (1f / s_innerRadius - 1f / s_outerRadius);
		float num4 = num3 / s_outerRadius;
		System.Array.Clear(m_movable, 0, num);
		bool[] array3 = m_movable;
		List<(int, int, float)> list = m_pairs;
		list.Clear();
		for (int j = 0; j < num; j++)
		{
			if (!array[j])
			{
				continue;
			}
			Rigidbody rigidbody2 = components[j];
			Vector3 vector = array2[j];
			float mass = rigidbody2.mass;
			bool flag = !rigidbody2.IsFixed();
			for (int k = 0; k < components.Length; k++)
			{
				if (j == k)
				{
					continue;
				}
				Rigidbody rigidbody3 = components[k];
				Vector3 vector2 = array2[k];
				float num5 = (vector.x - vector2.x) * (vector.x - vector2.x) + (vector.y - vector2.y) * (vector.y - vector2.y);
				if (num5 < num2)
				{
					float mass2 = rigidbody2.mass;
					bool flag2 = !rigidbody3.IsFixed();
					if (flag || flag2)
					{
						float num6 = Mathf.Sqrt(num5);
						float item = s_force * (mass * mass2 / (mass + mass2)) * ((num6 > s_innerRadius) ? (num3 / num6 - num4) : 1f);
						list.Add((j, k, item));
						array3[j] = flag;
						array3[k] = flag2;
					}
				}
			}
		}
		System.Array.Clear(m_velocities, 0, num);
		System.Array.Clear(m_masses, 0, num);
		Vector2[] array4 = m_velocities;
		float[] array5 = m_masses;
		for (int l = 0; l < num; l++)
		{
			if (array3[l])
			{
				Rigidbody rigidbody4 = components[l];
				Vector3 velocity = rigidbody4.velocity;
				array4[l] = new Vector2(velocity.x, velocity.y);
				array5[l] = rigidbody4.mass;
			}
		}
		System.Array.Clear(m_forces, 0, num);
		Vector2[] array6 = m_forces;
		foreach (var item2 in list)
		{
			Vector2 vector3 = ComputeForce(array4[item2.Item1], array4[item2.Item2], item2.Item3);
			array6[item2.Item1] += vector3 / array5[item2.Item1];
			array6[item2.Item2] -= vector3 / array5[item2.Item2];
		}
		for (int m = 0; m < num; m++)
		{
			if (array3[m])
			{
				components[m].EngineAddForce(array6[m], ForceMode.Acceleration);
			}
		}
	}

	private static Vector2 ComputeForce(Vector2 v1, Vector2 v2, float c)
	{
		float num = v2.x - v1.x;
		float num2 = v2.y - v1.y;
		float num3 = c * Mathf.Sqrt(num * num + num2 * num2);
		c *= ((num3 > s_maxForce) ? (s_maxForce / num3) : 1f);
		return new Vector2(c * num, c * num2);
	}
}
