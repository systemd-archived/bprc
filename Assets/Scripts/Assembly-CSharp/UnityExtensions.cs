using UnityEngine;

internal static class UnityExtensions
{
	public static Vector2 WithX(this Vector2 vector, float x)
	{
		return new Vector2(x, vector.y);
	}

	public static Vector2 WithY(this Vector2 vector, float y)
	{
		return new Vector2(vector.x, y);
	}

	public static Vector3 WithX(this Vector3 vector, float x)
	{
		return new Vector3(x, vector.y, vector.z);
	}

	public static Vector3 WithY(this Vector3 vector, float y)
	{
		return new Vector3(vector.x, y, vector.z);
	}

	public static Vector3 WithZ(this Vector3 vector, float z)
	{
		return new Vector3(vector.x, vector.y, z);
	}

	public static Color WithRGB(this Color color, float r, float g, float b)
	{
		return new Color(r, g, b, color.a);
	}

	public static Color WithAlpha(this Color color, float a)
	{
		return new Color(color.r, color.g, color.b, a);
	}

	public static Vector2 ToDirection(this Transform transform)
	{
		return transform.rotation.ToDirection();
	}

	public static Vector2 ToDirection(this Quaternion rotation)
	{
		return new Vector2(1f - (2f * rotation.y * rotation.y + 2f * rotation.z * rotation.z), 2f * rotation.x * rotation.y + 2f * rotation.w * rotation.z);
	}

	public static T AddOrGetComponent<T>(this GameObject gameObject) where T : Component
	{
		T val = gameObject.GetComponent<T>();
		if (val == null)
		{
			val = gameObject.AddComponent<T>();
		}
		return val;
	}

	public static void SetChildrenActive(this GameObject gameObject, bool active)
	{
		Transform transform = gameObject.transform;
		for (int i = 0; i < transform.childCount; i++)
		{
			transform.GetChild(i).gameObject.SetActive(active);
		}
	}

	public static bool IsFixed(this Rigidbody rigidbody)
	{
		if (!rigidbody.isKinematic)
		{
			return rigidbody.constraints == RigidbodyConstraints.FreezeAll;
		}
		return true;
	}

	public static Joint FindSpecifiedJoint(this Rigidbody self, Rigidbody other)
	{
		Joint[] components = self.GetComponents<Joint>();
		foreach (Joint joint in components)
		{
			if (joint.connectedBody == other)
			{
				return joint;
			}
		}
		return null;
	}
}
