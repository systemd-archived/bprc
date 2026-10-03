using System;
using UnityEngine;

public class WeldJoint : JointBase
{
	private Vector2 m_anchor;

	private float m_invMA;

	private float m_invMB;

	private float m_invIA;

	private float m_invIB;

	private float m_angle;

	private Vector2 m_localAnchorA;

	private Vector2 m_localAnchorB;

	private Matrix3x3 m_matrix;

	public WeldJoint(Rigidbody bodyA, Rigidbody bodyB, Vector2 anchor)
	{
		m_bodyA = bodyA;
		m_bodyB = bodyB;
		m_anchor = anchor;
		m_localAnchorA = Vector.InvTransform2(anchor - (Vector2)bodyA.position, bodyA.rotation.ToDirection());
		m_localAnchorB = Vector.InvTransform2(anchor - (Vector2)bodyB.position, bodyB.rotation.ToDirection());
		m_angle = MathF.PI / 180f * (bodyB.rotation.eulerAngles.z - bodyA.rotation.eulerAngles.z);
	}

	public void Initialize()
	{
	}

	public override void Prepare()
	{
		m_invMA = 1f / m_bodyA.mass;
		m_invMB = 1f / m_bodyB.mass;
		m_invIA = 1f / m_bodyA.inertiaTensor.z;
		m_invIB = 1f / m_bodyB.inertiaTensor.z;
	}

	public override void PreSolve()
	{
		float invMA = m_invMA;
		float invMB = m_invMB;
		float invIA = m_invIA;
		float invIB = m_invIB;
		Vector2 left = Vector.Transform2(m_localAnchorA - (Vector2)m_bodyA.centerOfMass, m_bodyA.rotation.ToDirection());
		Vector2 left2 = Vector.Transform2(m_localAnchorB - (Vector2)m_bodyB.centerOfMass, m_bodyB.rotation.ToDirection());
		if (invIA == 0f && invIB == 0f)
		{
			Vector2 vector = m_bodyA.velocity;
			Vector2 vector2 = m_bodyB.velocity;
			float z = m_bodyA.angularVelocity.z;
			float z2 = m_bodyB.angularVelocity.z;
			Vector2 vector3 = -(vector2 + new Vector2((0f - z2) * left2.y, z2 * left2.x) - (vector + new Vector2((0f - z) * left.y, z * left.x))) / (invMA + invMB);
			vector -= invMA * vector3;
			m_bodyA.velocity = vector;
			vector2 += invMB * vector3;
			m_bodyB.velocity = vector2;
			return;
		}
		m_matrix.M11 = invMA + invMB + invIA * left.y * left.y + invIB * left2.y * left2.y;
		m_matrix.M12 = (0f - invIA) * left.x * left.y - invIB * left2.x * left2.y;
		m_matrix.M13 = (0f - invIA) * left.y - invIB * left2.y;
		m_matrix.M21 = m_matrix.M12;
		m_matrix.M22 = invMA + invMB + invIA * left.x * left.x + invIB * left2.x * left2.x;
		m_matrix.M23 = invIA * left.x + invIB * left2.x;
		m_matrix.M31 = m_matrix.M13;
		m_matrix.M32 = m_matrix.M23;
		m_matrix.M33 = invIA + invIB;
		Vector2 vector4 = m_bodyA.velocity;
		Vector2 vector5 = m_bodyB.velocity;
		Vector3 angularVelocity = m_bodyA.angularVelocity;
		Vector3 angularVelocity2 = m_bodyB.angularVelocity;
		Vector3 vector6 = vector5 + new Vector2((0f - angularVelocity2.z) * left2.y, angularVelocity2.z * left2.x) - (vector4 + new Vector2((0f - angularVelocity.z) * left.y, angularVelocity.z * left.x));
		vector6.z = angularVelocity2.z - angularVelocity.z;
		Vector3 vector7 = Matrix3x3.Solve(m_matrix, -vector6);
		Vector2 vector8 = vector7;
		vector4 -= invMA * vector8;
		angularVelocity.z -= invIA * (Vector.Cross2(left, vector8) + vector7.z);
		m_bodyA.velocity = vector4;
		m_bodyA.angularVelocity = angularVelocity;
		vector5 += invMB * vector8;
		angularVelocity2.z += invIB * (Vector.Cross2(left2, vector8) + vector7.z);
		m_bodyB.velocity = vector5;
		m_bodyB.angularVelocity = angularVelocity2;
	}

	public override void PostSolve()
	{
		float invMA = m_invMA;
		float invMB = m_invMB;
		float invIA = m_invIA;
		float invIB = m_invIB;
		Vector2 vector = Vector.Transform2(m_localAnchorA - (Vector2)m_bodyA.centerOfMass, m_bodyA.rotation.ToDirection());
		Vector2 vector2 = Vector.Transform2(m_localAnchorB - (Vector2)m_bodyB.centerOfMass, m_bodyB.rotation.ToDirection());
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
		m_matrix.M13 = (0f - invIA) * vector.y - invIB * vector2.y;
		m_matrix.M21 = m_matrix.M12;
		m_matrix.M22 = invMA + invMB + invIA * vector.x * vector.x + invIB * vector2.x * vector2.x;
		m_matrix.M23 = invIA * vector.x + invIB * vector2.x;
		m_matrix.M31 = m_matrix.M13;
		m_matrix.M32 = m_matrix.M23;
		m_matrix.M33 = invIA + invIB;
		Vector2 vector6 = m_bodyA.position;
		Vector2 vector7 = m_bodyB.position;
		Vector3 vector8 = MathF.PI / 180f * m_bodyA.rotation.eulerAngles;
		Vector3 vector9 = MathF.PI / 180f * m_bodyB.rotation.eulerAngles;
		Vector3 vector10 = vector7 + vector2 - (vector6 + vector);
		vector10.z = vector9.z - vector8.z - m_angle;
		vector10.z = ((vector10.z >= MathF.PI) ? (vector10.z - MathF.PI * 2f) : ((vector10.z <= -MathF.PI) ? (vector10.z + MathF.PI * 2f) : vector10.z));
		Vector3 vector11 = Matrix3x3.Solve(m_matrix, -vector10);
		Vector2 vector12 = vector11;
		if (!m_bodyA.IsFixed())
		{
			m_bodyA.position = vector6 - invMA * vector12;
			vector8.z -= invIA * (Vector.Cross2(vector, vector12) + vector11.z);
			m_bodyA.rotation = Quaternion.Euler(57.29578f * vector8);
		}
		if (!m_bodyB.IsFixed())
		{
			m_bodyB.position = vector7 + invMB * vector12;
			vector9.z += invIB * (Vector.Cross2(vector2, vector12) + vector11.z);
			m_bodyB.rotation = Quaternion.Euler(57.29578f * vector9);
		}
	}
}
