using System;
using UnityEngine;

public class RevoluteJoint : JointBase
{
	private Vector2 m_anchor;

	private float m_invMA;

	private float m_invMB;

	private float m_invIA;

	private float m_invIB;

	private Vector2 m_localAnchorA;

	private Vector2 m_localAnchorB;

	private Matrix2x2 m_matrix;

	public RevoluteJoint(Rigidbody bodyA, Rigidbody bodyB, Vector2 anchor)
	{
		m_bodyA = bodyA;
		m_bodyB = bodyB;
		m_anchor = anchor;
		m_localAnchorA = Vector.InvTransform2(anchor - (Vector2)bodyA.position, bodyA.rotation.ToDirection());
		m_localAnchorB = Vector.InvTransform2(anchor - (Vector2)bodyB.position, bodyB.rotation.ToDirection());
	}

	public void Initialize()
	{
	}

	public override void Prepare()
	{
		m_bodyA.interpolation = RigidbodyInterpolation.None;
		m_bodyB.interpolation = RigidbodyInterpolation.None;
		m_invMA = 1f / m_bodyA.mass;
		m_invMB = 1f / m_bodyB.mass;
		m_invIA = 0f;
		m_invIB = 0f;
		m_invIA = 1f / m_bodyA.inertiaTensor.z;
		m_invIB = 1f / m_bodyB.inertiaTensor.z;
	}

	public override void PreSolve()
	{
		float invMA = m_invMA;
		float invMB = m_invMB;
		float invIA = m_invIA;
		float invIB = m_invIB;
		Vector2 left = Vector.Transform2(m_localAnchorA, m_bodyA.rotation.ToDirection());
		Vector2 left2 = Vector.Transform2(m_localAnchorB, m_bodyB.rotation.ToDirection());
		if (invIA == 0f && invIB == 0f)
		{
			Vector2 vector = m_bodyA.velocity;
			Vector2 vector2 = m_bodyB.velocity;
			float z = m_bodyA.angularVelocity.z;
			float z2 = m_bodyB.angularVelocity.z;
			Vector2 vector3 = -(vector2 + new Vector2((0f - z2) * left2.y, z2 * left2.x) - (vector + new Vector2((0f - z) * left.y, z * left.x))) / (invMA + invMB);
			m_bodyA.EngineAddForce((0f - invMA) * vector3, ForceMode.VelocityChange);
			m_bodyB.EngineAddForce(invMB * vector3, ForceMode.VelocityChange);
			return;
		}
		m_matrix.M11 = invMA + invMB + invIA * left.y * left.y + invIB * left2.y * left2.y;
		m_matrix.M12 = (0f - invIA) * left.x * left.y - invIB * left2.x * left2.y;
		m_matrix.M21 = m_matrix.M12;
		m_matrix.M22 = invMA + invMB + invIA * left.x * left.x + invIB * left2.x * left2.x;
		Vector2 vector4 = m_bodyA.velocity;
		Vector2 vector5 = m_bodyB.velocity;
		float z3 = m_bodyA.angularVelocity.z;
		float z4 = m_bodyB.angularVelocity.z;
		Vector2 vector6 = vector5 + new Vector2((0f - z4) * left2.y, z4 * left2.x) - (vector4 + new Vector2((0f - z3) * left.y, z3 * left.x));
		Vector2 vector7 = Matrix2x2.Solve(m_matrix, -vector6);
		m_bodyA.EngineAddForce((0f - invMA) * vector7, ForceMode.VelocityChange);
		m_bodyA.EngineAddTorque(new Vector3(0f, 0f, (0f - invIA) * Vector.Cross2(left, vector7)), ForceMode.VelocityChange);
		m_bodyB.EngineAddForce(invMB * vector7, ForceMode.VelocityChange);
		m_bodyB.EngineAddTorque(new Vector3(0f, 0f, invIB * Vector.Cross2(left2, vector7)), ForceMode.VelocityChange);
	}

	public override void PostSolve()
	{
		float invMA = m_invMA;
		float invMB = m_invMB;
		float invIA = m_invIA;
		float invIB = m_invIB;
		Vector2 vector = Vector.Transform2(m_localAnchorA, m_bodyA.rotation.ToDirection());
		Vector2 vector2 = Vector.Transform2(m_localAnchorB, m_bodyB.rotation.ToDirection());
		if (invIA == 0f && invIB == 0f)
		{
			Vector2 vector3 = m_bodyA.position;
			Vector2 vector4 = m_bodyB.position;
			Vector2 vector5 = -(vector4 + vector2 - (vector3 + vector)) / (invMA + invMB);
			if (!m_bodyA.IsFixed())
			{
				m_bodyA.position = vector3 - invMA * vector5;
			}
			if (!m_bodyB.IsFixed())
			{
				m_bodyB.position = vector4 + invMB * vector5;
			}
			return;
		}
		m_matrix.M11 = invMA + invMB + invIA * vector.y * vector.y + invIB * vector2.y * vector2.y;
		m_matrix.M12 = (0f - invIA) * vector.x * vector.y - invIB * vector2.x * vector2.y;
		m_matrix.M21 = m_matrix.M12;
		m_matrix.M22 = invMA + invMB + invIA * vector.x * vector.x + invIB * vector2.x * vector2.x;
		Vector2 vector6 = m_bodyA.position;
		Vector2 vector7 = m_bodyB.position;
		Vector3 eulerAngles = m_bodyA.rotation.eulerAngles;
		Vector3 eulerAngles2 = m_bodyB.rotation.eulerAngles;
		Vector2 vector8 = vector7 + vector2 - (vector6 + vector);
		Vector2 vector9 = Matrix2x2.Solve(m_matrix, -vector8);
		if (!m_bodyA.IsFixed())
		{
			m_bodyA.position = vector6 - invMA * vector9;
			eulerAngles.z -= 180f / MathF.PI * invIA * Vector.Cross2(vector, vector9);
			m_bodyA.rotation = Quaternion.Euler(eulerAngles);
		}
		if (!m_bodyB.IsFixed())
		{
			m_bodyB.position = vector7 + invMB * vector9;
			eulerAngles2.z += 180f / MathF.PI * invIB * Vector.Cross2(vector2, vector9);
			m_bodyB.rotation = Quaternion.Euler(eulerAngles2);
		}
	}
}
