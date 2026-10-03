using System;
using UnityEngine;

public class AutoGun : ExplodingGrapplingHook
{
	private bool m_activated;

	private static System.Random s_random;

	static AutoGun()
	{
		s_random = new System.Random();
	}

	public override bool CanBeEnabled()
	{
		return true;
	}

	public override bool IsEnabled()
	{
		return m_activated;
	}

	protected override void OnTouch()
	{
		m_activated = !m_activated;
	}

	private void FixedUpdate()
	{
		if (!base.contraption || !base.contraption.IsRunning || !IsEnabled() || this.IsSinglePart())
		{
			return;
		}
		float num = INSettings.GetFloat(INFeature.AutoGunAngle);
		float num2 = INSettings.GetFloat(INFeature.AutoGunMaxDistance);
		float num3 = INSettings.GetFloat(INFeature.AutoGunRandomProbability);
		float num4 = Mathf.Cos(num * 0.5f * (MathF.PI / 180f));
		bool flag = false;
		Vector3 position = base.transform.position;
		Vector3 right = base.transform.right;
		Vector3 vector = base.rigidbody.velocity + right * num2;
		float magnitude = vector.magnitude;
		float num5 = float.PositiveInfinity;
		foreach (BasePart part in base.contraption.Parts)
		{
			if (MarkerManager.IsInSameTeamStatic(this, part))
			{
				continue;
			}
			Vector3 position2 = part.transform.position;
			if (part.HasMultipleRigidbodies())
			{
				position2 = part.Position;
			}
			Vector3 velocity = part.rigidbody.velocity;
			float num6 = position2.x - position.x;
			float num7 = position2.y - position.y;
			float num8 = Mathf.Sqrt(num6 * num6 + num7 * num7);
			if (!((right.x * num6 + right.y * num7) / num8 > num4) || !(num8 < num2))
			{
				continue;
			}
			float num9 = velocity.x - vector.x;
			float num10 = velocity.y - vector.y;
			float num11 = Mathf.Sqrt(num9 * num9 + num10 * num10);
			float num12 = (0f - (num9 * num6 + num10 * num7)) / num11;
			if (!(num11 > 1E-05f) || !(num12 > 0f))
			{
				continue;
			}
			float num13 = num12 / num11;
			float num14 = (0f - (num9 * num7 - num10 * num6)) / num11;
			float num15 = 2f + num13 * 8f;
			num15 = ((num15 > 8f) ? 8f : num15);
			if (0f - num15 < num14 && num14 < num15)
			{
				float num16 = num13 * magnitude;
				if (num16 < num5)
				{
					num5 = num16;
				}
				flag = true;
			}
		}
		int num17 = LayerMask.NameToLayer("Ground");
		int num18 = LayerMask.NameToLayer("IceGround");
		RaycastHit[] array = Physics.RaycastAll(position, right, num5);
		for (int i = 0; i < array.Length; i++)
		{
			RaycastHit raycastHit = array[i];
			Rigidbody attachedRigidbody = raycastHit.collider.attachedRigidbody;
			int layer = raycastHit.collider.gameObject.layer;
			if (layer == num17 || layer == num18)
			{
				flag = false;
				break;
			}
			if (attachedRigidbody != null)
			{
				BasePart component = attachedRigidbody.GetComponent<BasePart>();
				if (component != null && component.ConnectedComponent != base.ConnectedComponent && MarkerManager.IsInSameTeamStatic(this, component))
				{
					flag = false;
					break;
				}
			}
		}
		if (flag && s_random.NextSingle() < num3)
		{
			Shoot();
		}
	}
}
