using System;
using UnityEngine;

public class InterfaceTrigger : MonoBehaviour
{
	private InterfacePart m_part;

	private void Awake()
	{
		m_part = base.transform.parent.GetComponent<InterfacePart>();
	}

	private void OnTriggerEnter(Collider other)
	{
		HandleTrigger(other);
	}

	public void OnTriggerStay(Collider other)
	{
		HandleTrigger(other);
	}

	private void HandleTrigger(Collider other)
	{
		if (other.name != "InterfaceTrigger")
		{
			return;
		}
		InterfacePart part = m_part;
		InterfacePart component = other.transform.parent.GetComponent<InterfacePart>();
		Vector3 vector = part.rigidbody.position - part.rigidbody.velocity * Time.fixedDeltaTime;
		Vector3 vector2 = component.rigidbody.position - component.rigidbody.velocity * Time.fixedDeltaTime;
		float num = vector2.x - vector.x;
		float num2 = vector2.y - vector.y;
		if (num * num + num2 * num2 > 2f)
		{
			return;
		}
		Vector3 right = part.transform.right;
		Vector3 right2 = component.transform.right;
		Vector.InvTransform2(right.x, right.y, right2.x, right2.y, out var resultX, out var resultY);
		if (Math.Abs(resultX) > 0.1f && Math.Abs(resultY) > 0.1f)
		{
			return;
		}
		Vector.InvTransform2(num, num2, right.x, right.y, out var resultX2, out var resultY2);
		Vector.InvTransform2(0f - num, 0f - num2, right2.x, right2.y, out var resultX3, out var resultY3);
		float num3 = Math.Abs(resultX2);
		float num4 = Math.Abs(resultY2);
		float num5 = Math.Abs(resultX3);
		float num6 = Math.Abs(resultY3);
		if (((num3 < 1.1f && num4 < 0.9f) || (num3 < 0.9f && num4 < 1.1f)) && ((num5 < 1.1f && num6 < 0.9f) || (num5 < 0.9f && num6 < 1.1f)))
		{
			BitDirection direction = ((!(resultY2 > 0f - resultX2)) ? ((resultY2 > resultX2) ? BitDirection.Left : BitDirection.Down) : ((resultY2 < resultX2) ? BitDirection.Right : BitDirection.Up));
			BitDirection direction2 = ((!(resultY3 > 0f - resultX3)) ? ((resultY3 > resultX3) ? BitDirection.Left : BitDirection.Down) : ((resultY3 < resultX3) ? BitDirection.Right : BitDirection.Up));
			if (part.CanConnectTo(direction) && component.CanConnectTo(direction2))
			{
				part.Connect(component, direction);
				component.Connect(part, direction2);
			}
		}
	}
}
