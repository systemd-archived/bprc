using UnityEngine;

public abstract class JointBase
{
	protected Rigidbody m_bodyA;

	protected Rigidbody m_bodyB;

	public Rigidbody BodyA => m_bodyA;

	public Rigidbody BodyB => m_bodyB;

	public virtual void Prepare()
	{
	}

	public virtual void PreSolve()
	{
	}

	public virtual void PostSolve()
	{
	}
}
