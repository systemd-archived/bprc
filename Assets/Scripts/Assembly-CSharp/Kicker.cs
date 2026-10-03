using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Kicker : BasePart
{
	public bool m_enabled;

	private List<BasePart> m_connectedParts;

	private List<bool> m_isOverDistance;

	private List<(Rigidbody, Joint)> m_connectedJoints;

	private List<(Rigidbody, Joint)> m_originalConnectedJoints;

	private bool m_isTriggered;

	private List<Joint> m_JointsToBreak;

	private GameObject[] m_colorMarks = new GameObject[8];

	private float m_connectTime;

	public override Direction EffectDirection()
	{
		return BasePart.Rotate(Direction.Right, m_gridRotation);
	}

	public override void SetRotation(GridRotation rotation)
	{
		m_gridRotation = rotation;
		SetColorMark();
		ApplyBuildRotation(Quaternion.identity, false);
	}

	private void SetColorMark()
	{
		int num = m_colorMarks.Length;
		while (--num >= 0)
		{
			if ((bool)m_colorMarks[num])
			{
				m_colorMarks[num].GetComponent<Renderer>().enabled = false;
			}
		}
		if ((bool)m_colorMarks[(int)m_gridRotation])
		{
			m_colorMarks[(int)m_gridRotation].GetComponent<Renderer>().enabled = true;
		}
	}

	public override void Awake()
	{
		base.Awake();
		m_colorMarks[0] = base.transform.Find("Deg0").gameObject;
		m_colorMarks[1] = base.transform.Find("Deg90").gameObject;
		m_colorMarks[2] = base.transform.Find("Deg180").gameObject;
		m_colorMarks[3] = base.transform.Find("Deg270").gameObject;
		SetColorMark();
	}

	public override void Initialize()
	{
		if (WPFMonoBehaviour.levelManager.ContraptionRunning == null || (INSettings.GetBool(INFeature.SeparatorConnection) && m_enclosedInto != null))
		{
			return;
		}
		m_JointsToBreak = WPFMonoBehaviour.levelManager.ContraptionRunning.FindPartFixedJoints(this);
		if (INSettings.GetBool(INFeature.AutoConnector) && this.IsAutoConnector())
		{
			m_connectedJoints = new List<(Rigidbody, Joint)>();
			m_originalConnectedJoints = new List<(Rigidbody, Joint)>();
			foreach (Joint item in m_JointsToBreak)
			{
				if (item != null && item.GetComponent<Rigidbody>() == base.rigidbody)
				{
					m_originalConnectedJoints.Add((item.connectedBody, item));
				}
			}
		}
		if (!INSettings.GetBool(INFeature.MarkerSeparator) || !this.IsMarker())
		{
			base.contraption.ChangeOneShotPartAmount(m_partType, EffectDirection(), 1);
		}
	}

	public override bool IsTriggerable()
	{
		if (INSettings.GetBool(INFeature.MarkerSeparator) && this.IsMarker())
		{
			return false;
		}
		if (INSettings.GetBool(INFeature.SeparatorConnection) && m_partTier != PartTier.Regular)
		{
			return !base.HasGeneratorRef;
		}
		if (!m_isTriggered)
		{
			return !base.HasGeneratorRef;
		}
		return false;
	}

	protected override void OnTouch()
	{
		if (INSettings.GetBool(INFeature.MarkerSeparator) && this.IsMarker())
		{
			return;
		}
		base.rigidbody.EngineWakeUp();
		if (INSettings.GetBool(INFeature.SeparatorConnection))
		{
			if (m_enclosedInto != null)
			{
				return;
			}
			if (this.IsAutoConnector() && m_isTriggered)
			{
				m_enabled = !m_enabled;
				if (m_enabled)
				{
					return;
				}
				foreach (var connectedJoint in m_connectedJoints)
				{
					Joint item = connectedJoint.Item2;
					bool flag = false;
					if (item != null)
					{
						UnityEngine.Object.Destroy(item);
						flag = true;
					}
					if (flag)
					{
						Contraption.Instance.UpdateConnectedComponents();
					}
				}
				m_connectedJoints.Clear();
				return;
			}
			if (m_partTier != PartTier.Regular && m_isTriggered)
			{
				m_enabled = !m_enabled;
				base.contraption.UpdateConnectedComponents();
				return;
			}
			if (m_partTier == PartTier.Regular)
			{
				base.contraption.ChangeOneShotPartAmount(m_partType, EffectDirection(), -1);
				base.contraption.UpdateConnectedComponents();
			}
			m_isTriggered = true;
			Singleton<AudioManager>.Instance.SpawnOneShotEffect(WPFMonoBehaviour.gameData.commonAudioCollection.kickerDetach, base.transform.position);
			m_connectedParts = new List<BasePart>();
			m_isOverDistance = new List<bool>();
			for (int i = 0; i < m_JointsToBreak.Count; i++)
			{
				if ((bool)m_JointsToBreak[i])
				{
					BasePart component = m_JointsToBreak[i].GetComponent<BasePart>();
					BasePart component2 = m_JointsToBreak[i].connectedBody.GetComponent<BasePart>();
					if ((component == null || ((component.m_enclosedInto == null || component.m_partType != PartType.Kicker) && (component.m_enclosedPart == null || component.m_enclosedPart.m_partType != PartType.Kicker))) && (component2 == null || ((component2.m_enclosedInto == null || component2.m_partType != PartType.Kicker) && (component2.m_enclosedPart == null || component2.m_enclosedPart.m_partType != PartType.Kicker))))
					{
						UnityEngine.Object.Destroy(m_JointsToBreak[i]);
						if (component != null)
						{
							component.HandleJointBreak(playEffects: false);
						}
						if (component != null && component.m_enclosedInto == null && component != this)
						{
							m_connectedParts.Add(component);
							m_isOverDistance.Add(IsPartOverDistance(component));
						}
						if (component2 != null && component2.m_enclosedInto == null && component2 != this)
						{
							m_connectedParts.Add(component2);
							m_isOverDistance.Add(IsPartOverDistance(component2));
						}
					}
				}
				m_JointsToBreak[i] = null;
			}
		}
		else
		{
			if (m_isTriggered)
			{
				return;
			}
			m_isTriggered = true;
			Singleton<AudioManager>.Instance.SpawnOneShotEffect(WPFMonoBehaviour.gameData.commonAudioCollection.kickerDetach, base.transform.position);
			base.contraption.ChangeOneShotPartAmount(m_partType, EffectDirection(), -1);
			for (int j = 0; j < m_JointsToBreak.Count; j++)
			{
				if ((bool)m_JointsToBreak[j])
				{
					UnityEngine.Object.Destroy(m_JointsToBreak[j]);
					BasePart component3 = m_JointsToBreak[j].gameObject.GetComponent<BasePart>();
					if ((bool)component3)
					{
						component3.HandleJointBreak(playEffects: false);
					}
				}
				m_JointsToBreak[j] = null;
			}
			m_JointsToBreak.Clear();
		}
	}

	public override bool IsEnabled()
	{
		return m_enabled;
	}

	private void FixedUpdate()
	{
		if (INSettings.GetBool(INFeature.AutoConnector) && this.IsAutoConnector())
		{
			float num = INSettings.GetFloat(INFeature.AutoConnectorCoolingTime);
			if (m_enabled && Time.time > m_connectTime + num)
			{
				bool flag = false;
				for (int num2 = m_connectedJoints.Count - 1; num2 >= 0; num2--)
				{
					(Rigidbody, Joint) tuple = m_connectedJoints[num2];
					if (tuple.Item1 == null || tuple.Item2 == null)
					{
						if (tuple.Item2 != null)
						{
							UnityEngine.Object.Destroy(tuple.Item2);
							flag = true;
						}
						m_connectedJoints.RemoveAt(num2);
					}
				}
				Vector3 position = base.transform.position;
				float num3 = 1.44f;
				float jointConnectionStrength = Contraption.Instance.GetJointConnectionStrength(GetJointConnectionStrength());
				Rigidbody rigidbody = base.rigidbody;
				int num4 = INSettings.GetInt(INFeature.AutoConnectorMaxConnectionCount);
				Rigidbody[] components = INContraption.Instance.Rigidbodies;
				foreach (Rigidbody rigidbody2 in components)
				{
					if (m_connectedJoints.Count >= num4)
					{
						break;
					}
					Vector3 position2 = rigidbody2.position;
					if ((position2.x - position.x) * (position2.x - position.x) + (position2.y - position.y) * (position2.y - position.y) > num3 || rigidbody2 == rigidbody)
					{
						continue;
					}
					// A marker separator lets same-faction vehicles overlap, and this connector used to weld
					// the first body in range whoever owned it: the two vehicles then became one connected
					// component and shared engines, starter and keys. Never weld to another marked vehicle —
					// loose parts (no marker of their own) stay connectable, so self-repair still works.
					BasePart candidate = rigidbody2.GetComponentInParent<BasePart>();
					if (candidate != null && candidate != this && candidate.ConnectedComponent != ConnectedComponent && MarkerManager.IsInSameTeamStatic(this, candidate))
					{
						continue;
					}
					Predicate<(Rigidbody, Joint)> match = ((Rigidbody, Joint) tuple2) => tuple2.Item1 == rigidbody2 && tuple2.Item2 != null;
					if (m_originalConnectedJoints.Exists(match) || m_connectedJoints.Exists(match))
					{
						continue;
					}
					BasePart component = rigidbody2.GetComponent<BasePart>();
					if (component == null || component.m_partType != PartType.Rope || !rigidbody2.isKinematic)
					{
						flag = true;
						float num5 = ((component != null) ? Contraption.Instance.GetJointConnectionStrength(component.GetJointConnectionStrength()) : 0f);
						Joint joint = base.gameObject.AddComponent<FixedJoint>();
						joint.connectedBody = rigidbody2;
						if (Contraption.Instance.HasSuperGlue)
						{
							joint.breakForce = float.PositiveInfinity;
						}
						else
						{
							joint.breakForce = (jointConnectionStrength + num5) * INSettings.GetFloat(INFeature.ConnectionStrength);
						}
						joint.enablePreprocessing = false;
						m_connectedJoints.Add((rigidbody2, joint));
						m_connectTime = Time.time;
						break;
					}
				}
				if (flag)
				{
					Contraption.Instance.UpdateConnectedComponents();
				}
			}
		}
		if (!INSettings.GetBool(INFeature.SeparatorConnection) || !this.IsElasticConnector() || !m_enabled)
		{
			return;
		}
		for (int num6 = 0; num6 < m_connectedParts.Count; num6++)
		{
			BasePart part = m_connectedParts[num6];
			bool flag2 = IsPartOverDistance(part);
			if (m_isOverDistance[num6] != flag2)
			{
				base.contraption.UpdateConnectedComponents();
			}
			m_isOverDistance[num6] = flag2;
		}
		float num7 = INSettings.GetFloat(INFeature.SeparatorConnectionInnerDistance);
		float num8 = INSettings.GetFloat(INFeature.SeparatorConnectionOuterDistance);
		if (customPartIndex == 2)
		{
			float fixedDeltaTime = Time.fixedDeltaTime;
			float mass = base.rigidbody.mass;
			Vector3 position3 = base.rigidbody.position;
			Vector3 velocity = base.rigidbody.velocity;
			for (int num9 = 0; num9 < m_connectedParts.Count; num9++)
			{
				BasePart basePart = m_connectedParts[num9];
				if (basePart != null && m_isOverDistance[num9])
				{
					Vector3 position4 = basePart.rigidbody.position;
					float num10 = position3.x - position4.x;
					float num11 = position3.y - position4.y;
					float num12 = Vector.Distance2(position3, position4);
					if (num12 > 1E-05f)
					{
						Vector3 velocity2 = basePart.rigidbody.velocity;
						float rightX = velocity.x - velocity2.x;
						float rightY = velocity.y - velocity2.y;
						float mass2 = basePart.rigidbody.mass;
						float num13 = mass2 / (mass + mass2);
						float num14 = mass / (mass + mass2);
						float num15 = ((num12 > num7) ? ((num8 - num12) / (num8 - num7)) : 1f);
						float num16 = Vector.Dot2(num10, num11, rightX, rightY) / num12;
						num16 *= num15;
						num16 = ((num16 > 20f) ? 20f : ((num16 < -20f) ? (-20f) : num16)) / num12;
						velocity -= new Vector3(num13 * num16 * num10, num13 * num16 * num11);
						basePart.rigidbody.velocity += new Vector3(num14 * num16 * num10, num14 * num16 * num11);
						float num17 = num12 - 1f;
						num17 *= num15;
						num17 = ((num17 > 0.4f) ? 0.4f : ((num17 < -0.4f) ? (-0.4f) : num17)) / num12;
						velocity -= new Vector3(num13 * num17 * num10 / fixedDeltaTime, num13 * num17 * num11 / fixedDeltaTime);
						basePart.rigidbody.velocity += new Vector3(num14 * num17 * num10 / fixedDeltaTime, num14 * num17 * num11 / fixedDeltaTime);
					}
				}
			}
			base.rigidbody.velocity = velocity;
		}
		else
		{
			if (customPartIndex != 4)
			{
				return;
			}
			float deltaTime = Time.deltaTime;
			float mass3 = base.rigidbody.mass;
			Vector3 position5 = base.rigidbody.position;
			Vector3 velocity3 = base.rigidbody.velocity;
			for (int num18 = 0; num18 < m_connectedParts.Count; num18++)
			{
				BasePart basePart2 = m_connectedParts[num18];
				if (basePart2 != null && m_isOverDistance[num18])
				{
					Vector3 position6 = basePart2.rigidbody.position;
					Vector3 velocity4 = basePart2.rigidbody.velocity;
					float num19 = Vector.Distance2(position5, position6);
					float num20 = ((num19 > num7) ? ((num8 - num19) / (num8 - num7)) : 1f);
					float num21 = 32f * (velocity3.x - velocity4.x) + 128f * (position5.x - position6.x);
					float num22 = 32f * (velocity3.y - velocity4.y) + 128f * (position5.y - position6.y);
					num21 *= num20;
					num22 *= num20;
					float num23 = Mathf.Sqrt(num21 * num21 + num22 * num22);
					float mass4 = basePart2.rigidbody.mass;
					float num24 = ((num23 * mass4 > 250f) ? (deltaTime * 250f / (num23 * mass4)) : deltaTime);
					float num25 = num24 * mass4 / mass3;
					velocity3 -= new Vector3(num25 * num21, num25 * num22);
					basePart2.rigidbody.velocity += new Vector3(num24 * num21, num24 * num22);
				}
			}
			base.rigidbody.velocity = velocity3;
		}
	}

	public IEnumerable<BasePart> GetConnectedParts()
	{
		if (customPartIndex == 2 || customPartIndex == 4 || customPartIndex == 3)
		{
			if (m_connectedParts == null)
			{
				return Enumerable.Empty<BasePart>();
			}
			if (INSettings.GetBool(INFeature.PersistentSeparatorConnection))
			{
				return m_connectedParts;
			}
			if (!m_enabled)
			{
				return Enumerable.Empty<BasePart>();
			}
			return GetConnectedPartsYield();
		}
		if (customPartIndex == 1)
		{
			if (!m_enabled)
			{
				return Enumerable.Empty<BasePart>();
			}
			return GetConnectedPartsYield();
		}
		return Enumerable.Empty<BasePart>();
	}

	private IEnumerable<BasePart> GetConnectedPartsYield()
	{
		if (this.IsElasticConnector())
		{
			for (int i = 0; i < m_connectedParts.Count; i++)
			{
				if (m_isOverDistance[i])
				{
					yield return m_connectedParts[i];
				}
			}
		}
		else
		{
			if (!this.IsAutoConnector())
			{
				yield break;
			}
			foreach (var connectedJoint in m_connectedJoints)
			{
				if (connectedJoint.Item1 != null)
				{
					BasePart component = connectedJoint.Item1.GetComponent<BasePart>();
					if (component != null)
					{
						yield return component;
					}
				}
			}
		}
	}

	private bool IsPartOverDistance(BasePart part)
	{
		float num = INSettings.GetFloat(INFeature.SeparatorConnectionOuterDistance);
		Vector3 position = base.transform.position;
		Vector3 position2 = part.transform.position;
		return (position.x - position2.x) * (position.x - position2.x) + (position.y - position2.y) * (position.y - position2.y) < num * num;
	}
}
