using UnityEngine;

[RequireComponent(typeof(LevelRigidbody))]
public class GoldenPigReaction : WPFMonoBehaviour
{
	private Rigidbody m_rigidbody;

	private LevelRigidbody levelRigidbody;

	private void Start()
	{
		levelRigidbody = GetComponent<LevelRigidbody>();
		m_rigidbody = base.rigidbody;
	}

	public void OnCollisionEnter(UnityEngine.Collision c) { OnPhysicsCollisionEnter(new PhysicsCollision(c)); }

	private void OnPhysicsCollisionEnter(PhysicsCollision c)
	{
		BasePart component = c.collider.GetComponent<BasePart>();
		if ((bool)component && component.m_partType == BasePart.PartType.GoldenPig)
		{
			if (levelRigidbody.breakEffect != null)
			{
				levelRigidbody.breakEffect.transform.parent = null;
				levelRigidbody.breakEffect.Play();
			}
			m_rigidbody.isKinematic = true;
			base.transform.position = -Vector3.up * 1000f;
		}
	}
}
