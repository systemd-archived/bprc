using System.Collections.Generic;
using UnityEngine;

public class AutoControlLightManager : PartManager
{
	private List<AutoControlLight> m_lights;

	private HashSet<Collider> m_colliders;

	private Dictionary<Renderer, float> m_renderers;

	public override StatusCode Status => StatusCode.Running;

	protected override void Initialize()
	{
		base.Initialize();
		m_lights = new List<AutoControlLight>();
		m_colliders = new HashSet<Collider>();
		m_renderers = new Dictionary<Renderer, float>();
	}

	public override void FixedUpdate()
	{
		m_lights.Clear();
		foreach (BasePart part in Contraption.Instance.Parts)
		{
			if (part.IsAutoControlLight())
			{
				m_lights.Add((AutoControlLight)part);
			}
		}
		if (m_lights.Count == 0)
		{
			return;
		}
		Rigidbody[] components = INContraption.Instance.Rigidbodies;
		HashSet<Collider> hashSet = new HashSet<Collider>();
		HashSet<Renderer> hashSet2 = new HashSet<Renderer>();
		Dictionary<Renderer, float> dictionary = new Dictionary<Renderer, float>();
		foreach (AutoControlLight light in m_lights)
		{
			if (light.IsEnabled() && light.enclosedInto != null)
			{
				bool flag = light.CurrentType == 1;
				bool flag2 = false;
				Vector3 position = light.transform.position;
				Vector3 vector = light.transform.TransformDirection(Vector3.down);
				Rigidbody[] array = components;
				foreach (Rigidbody rigidbody in array)
				{
					Vector3 position2 = rigidbody.position;
					float x = vector.x * (position2.x - position.x) + vector.y * (position2.y - position.y);
					float y = vector.y * (position2.x - position.x) - vector.x * (position2.y - position.y);
					bool flag3 = light.IsInDetectArea(x, y);
					bool flag4 = light.IsInCollideArea(x, y);
					if (flag3 || flag4)
					{
						BasePart component = rigidbody.GetComponent<BasePart>();
						flag |= flag3 && (component == null || component.ConnectedComponent != light.ConnectedComponent);
						flag2 |= flag4 && component != null && component.ConnectedComponent != light.ConnectedComponent;
						if (flag && flag2)
						{
							break;
						}
					}
				}
				light.SetDetected(flag, force: false);
				light.SetCollided(flag2);
			}
			else
			{
				light.SetDetected(detected: false, force: true);
			}
		}
		foreach (BasePart part2 in Contraption.Instance.Parts)
		{
			// The auto-control light is mounted on a frame; BasePart.IsFramePart also accepts
			// the imported RC1 Android frames.
			if (!BasePart.IsFramePart(part2.m_partType))
			{
				continue;
			}
			bool flag5 = false;
			Vector3 position3 = part2.rigidbody.position;
			foreach (AutoControlLight light2 in m_lights)
			{
				if (!light2.IsEnabled() || !light2.IsDetected())
				{
					continue;
				}
				Vector3 position4 = light2.transform.position;
				Vector3 vector2 = light2.transform.TransformDirection(Vector3.down);
				float x2 = vector2.x * (position3.x - position4.x) + vector2.y * (position3.y - position4.y);
				float y2 = vector2.y * (position3.x - position4.x) - vector2.x * (position3.y - position4.y);
				if (light2.IsInCollideArea(x2, y2))
				{
					flag5 = part2 != null && part2.ConnectedComponent == light2.ConnectedComponent;
					if (flag5)
					{
						break;
					}
				}
			}
			if (flag5)
			{
				hashSet.Add(part2.GetComponent<Collider>());
				hashSet2.Add(part2.GetComponent<Renderer>());
			}
		}
		foreach (Collider collider in m_colliders)
		{
			if (collider != null)
			{
				collider.enabled = true;
			}
		}
		foreach (KeyValuePair<Renderer, float> renderer in m_renderers)
		{
			Renderer key = renderer.Key;
			if (key != null)
			{
				Color color = key.material.color;
				color.a = renderer.Value;
				key.material.color = color;
			}
		}
		foreach (Collider item in hashSet)
		{
			item.enabled = false;
		}
		foreach (Renderer item2 in hashSet2)
		{
			Color color2 = item2.material.color;
			dictionary[item2] = color2.a;
			color2.a = 0.25f;
			item2.material.color = color2;
		}
		m_colliders = hashSet;
		m_renderers = dictionary;
	}
}
