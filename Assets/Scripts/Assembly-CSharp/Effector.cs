using System;
using System.Collections.Generic;
using UnityEngine;

public class Effector : WPFMonoBehaviour
{
	public enum Type
	{
		Wind = 0,
		Magnet = 1,
		Formula = 2,
		Bumper = 3
	}

	public enum Shape
	{
		Box = 0,
		Circle = 1,
		Capsule = 2
	}

	public enum InteractionType
	{
		Include = 0,
		Exclude = 1
	}

	public enum FormulaType
	{
		Sin = 0,
		Cos = 1,
		AbsSin = 2,
		AbsCos = 3
	}

	public Shape m_shape;

	public bool m_isTrigger = true;

	[HideInInspector]
	public float m_range = 5f;

	[HideInInspector]
	public float m_strenght = 5f;

	[HideInInspector]
	public float m_angle;

	[HideInInspector]
	public FormulaType m_formulaType;

	[HideInInspector]
	public bool m_useVectorUpForDirection;

	[HideInInspector]
	public bool m_hasLinearFadeout;

	public InteractionType m_interactionType;

	public List<string> m_interactionTypeList = new List<string>();

	protected List<GameObject> m_influenceList = new List<GameObject>();

	[HideInInspector]
	public Vector3 m_effectorCenterPoint;

	public Type m_type;

	private void Start()
	{
	}

	private void FixedUpdate()
	{
		foreach (GameObject influence in m_influenceList)
		{
			if (!influence || !influence.GetComponent<Rigidbody>())
			{
				continue;
			}
			switch (m_type)
			{
			case Type.Wind:
				if (m_hasLinearFadeout)
				{
					influence.GetComponent<Rigidbody>().EngineAddForce((Vector3.right * Mathf.Cos(m_angle * (MathF.PI / 180f)) + Vector3.up * Mathf.Sin(m_angle * (MathF.PI / 180f))) * m_strenght / Mathf.Clamp((influence.transform.position - base.transform.position).magnitude, 1f, 100f), ForceMode.Force);
				}
				else
				{
					influence.GetComponent<Rigidbody>().EngineAddForce((Vector3.right * Mathf.Cos(m_angle * (MathF.PI / 180f)) + Vector3.up * Mathf.Sin(m_angle * (MathF.PI / 180f))) * m_strenght, ForceMode.Force);
				}
				break;
			case Type.Magnet:
				influence.GetComponent<Rigidbody>().EngineAddForce((base.transform.position + m_effectorCenterPoint - influence.transform.position).normalized * m_strenght, ForceMode.Acceleration);
				break;
			case Type.Formula:
				ApplyFormulaEffector(m_formulaType, influence.GetComponent<Rigidbody>());
				break;
			}
		}
	}

	private void OnTriggerEnter(Collider c)
	{
		if (!c.GetComponent<Rigidbody>())
		{
			return;
		}
		if (m_interactionTypeList.Count == 0)
		{
			m_influenceList.Add(c.gameObject);
			return;
		}
		foreach (string interactionType in m_interactionTypeList)
		{
			Component component = c.GetComponent(interactionType);
			if (((bool)component && m_interactionType == InteractionType.Include) || (!component && m_interactionType == InteractionType.Exclude))
			{
				m_influenceList.Add(c.gameObject);
			}
		}
	}

	private void OnTriggerExit(Collider c)
	{
		m_influenceList.Remove(c.gameObject);
	}

	public void OnCollisionEnter(UnityEngine.Collision c) { OnPhysicsCollisionEnter(new PhysicsCollision(c)); }

	private void OnPhysicsCollisionEnter(PhysicsCollision c)
	{
		if (!c.rigidbody || m_type != Type.Bumper)
		{
			return;
		}
		PhysicsContactPoint[] contacts = c.contacts;
		for (int i = 0; i < contacts.Length; i++)
		{
			PhysicsContactPoint contactPoint = contacts[i];
			if (Vector3.Dot(contactPoint.normal, base.transform.up) <= 0.707f)
			{
				Bump(contactPoint.otherCollider.gameObject);
			}
		}
	}

	private void ApplyFormulaEffector(FormulaType type, Rigidbody rb)
	{
		switch (type)
		{
		case FormulaType.Sin:
			rb.EngineAddForce((Vector3.right * Mathf.Cos(m_angle * (MathF.PI / 180f)) + Vector3.up * Mathf.Sin(m_angle * (MathF.PI / 180f))) * m_strenght * Mathf.Sin(Time.time), ForceMode.Force);
			break;
		case FormulaType.Cos:
			rb.EngineAddForce((Vector3.right * Mathf.Cos(m_angle * (MathF.PI / 180f)) + Vector3.up * Mathf.Sin(m_angle * (MathF.PI / 180f))) * m_strenght * Mathf.Cos(Time.time), ForceMode.Force);
			break;
		case FormulaType.AbsSin:
			rb.EngineAddForce((Vector3.right * Mathf.Cos(m_angle * (MathF.PI / 180f)) + Vector3.up * Mathf.Sin(m_angle * (MathF.PI / 180f))) * m_strenght * Mathf.Abs(Mathf.Cos(Time.time)), ForceMode.Force);
			break;
		case FormulaType.AbsCos:
			rb.EngineAddForce((Vector3.right * Mathf.Cos(m_angle * (MathF.PI / 180f)) + Vector3.up * Mathf.Sin(m_angle * (MathF.PI / 180f))) * m_strenght * Mathf.Abs(Mathf.Cos(Time.time)), ForceMode.Force);
			break;
		}
	}

	private void Bump(GameObject go)
	{
		Vector3 up = base.transform.up;
		up = (go.transform.position - base.transform.position).normalized;
		Vector3 velocity = go.GetComponent<Rigidbody>().velocity;
		float num = Vector3.Dot(up, velocity);
		Vector3 vector = up * num;
		Vector3 vector2 = velocity - vector;
		num = Mathf.Max(m_strenght, Mathf.Abs(num));
		Vector3 velocity2 = vector2 + up * num;
		go.GetComponent<Rigidbody>().velocity = velocity2;
		if ((bool)base.animation)
		{
			base.animation.Play();
		}
	}
}
