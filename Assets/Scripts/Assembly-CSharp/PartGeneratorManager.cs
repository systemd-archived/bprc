using System;
using System.Collections.Generic;
using UnityEngine;

public class PartGeneratorManager : PartManager
{
	private class PartGenerationSystem
	{
		private bool m_resize;

		private DisjointSet m_disjointSet;

		private ComponentData[] m_components;

		private RuntimeContraption m_runtimeContraption;

		public bool IsEmpty => m_runtimeContraption.Parts.Count == 0;

		public RuntimeContraption RuntimeContraption => m_runtimeContraption;

		public PartGenerationSystem()
		{
			m_components = Array.Empty<ComponentData>();
			m_runtimeContraption = new RuntimeContraption();
			m_disjointSet = new DisjointSet(0);
		}

		public void InitializePart(BasePart template, GrapplingHook generator, int x, int y)
		{
			int enclosedPartIndex = -1;
			RuntimeContraption runtimeContraption = m_runtimeContraption;
			int key = generator.m_coordX + x + (generator.m_coordY + y << 16);
			int num = runtimeContraption.FindPartIndexAt(generator.m_coordX + x, generator.m_coordY + y);
			BasePart basePart;
			if (num != -1)
			{
				BasePart part = runtimeContraption.Parts[num].Part;
				if (part.CanBeEnclosed() && template.CanEncloseParts())
				{
					if (part.enclosedInto != null)
					{
						generator.OnPartGenerated();
						return;
					}
					basePart = UnityEngine.Object.Instantiate(WPFMonoBehaviour.gameData.GetCustomPart(template.m_partType, template.customPartIndex));
					runtimeContraption.PartIndexMap[key] = runtimeContraption.Parts.Count;
					basePart.enclosedPart = part;
					enclosedPartIndex = num;
				}
				else
				{
					if (!template.CanBeEnclosed() || !part.CanEncloseParts())
					{
						generator.OnPartGenerated();
						return;
					}
					if (part.enclosedPart != null)
					{
						generator.OnPartGenerated();
						return;
					}
					basePart = (part.enclosedPart = UnityEngine.Object.Instantiate(WPFMonoBehaviour.gameData.GetCustomPart(template.m_partType, template.customPartIndex)));
					PartData value = runtimeContraption.Parts[num];
					value.EnclosedPartIndex = runtimeContraption.Parts.Count;
					runtimeContraption.Parts[num] = value;
				}
			}
			else
			{
				basePart = UnityEngine.Object.Instantiate(WPFMonoBehaviour.gameData.GetCustomPart(template.m_partType, template.customPartIndex));
				runtimeContraption.PartIndexMap[key] = runtimeContraption.Parts.Count;
			}
			basePart.gameObject.SetActive(value: true);
			basePart.transform.position = template.transform.position;
			basePart.transform.rotation = template.transform.rotation;
			basePart.transform.parent = template.transform.parent;
			basePart.m_coordX = generator.m_coordX + x;
			basePart.m_coordY = generator.m_coordY + y;
			basePart.PrePlaced();
			// 生成体继承“模板部件”的自定义参数（复制器）
			basePart.CopyCustomParamsFrom(template);
			basePart.SetRotation(template.m_gridRotation);
			if (template.m_flipped)
			{
				basePart.SetFlipped(template.m_flipped);
			}
			basePart.GenerationLevel = template.GenerationLevel + 1;
			basePart.contraption = template.contraption;
			basePart.gameObject.SetActive(value: false);
			runtimeContraption.Parts.Add(new PartData(basePart, template, generator, Mathf.Sqrt(x * x + y * y), Time.time, enclosedPartIndex, generator.GetRendererArray()));
			m_resize = true;
		}

		public void UpdateParts()
		{
			RuntimeContraption runtimeContraption = m_runtimeContraption;
			List<PartData> parts = runtimeContraption.Parts;
			int count = parts.Count;
			if (m_resize)
			{
				int count2 = m_disjointSet.Count;
				if (count2 < count)
				{
					DisjointSet disjointSet = new DisjointSet(count);
					for (int i = 0; i < count2; i++)
					{
						disjointSet.Union(i, m_disjointSet.FindSet(i));
					}
					m_disjointSet = disjointSet;
				}
				Connect(count2, count - 1);
				int num = 0;
				int[] array = new int[count];
				for (int j = 0; j < count; j++)
				{
					array[j] = -1;
				}
				for (int k = 0; k < count; k++)
				{
					int num2 = m_disjointSet.FindSet(k);
					int num3 = array[num2];
					PartData value = parts[k];
					if (num3 == -1)
					{
						num3 = num++;
					}
					array[num2] = num3;
					value.ComponentIndex = num3;
					parts[k] = value;
				}
				m_components = new ComponentData[num];
				for (int l = 0; l < count; l++)
				{
					PartData partData = parts[l];
					ref ComponentData reference = ref m_components[partData.ComponentIndex];
					if (partData.Time > reference.Time)
					{
						reference.Time = partData.Time;
					}
					m_disjointSet.FindSet(l, out var size);
					reference.GenerateTime = Mathf.Sqrt((size - 1) * 6 + 1) * 0.2f;
				}
				m_resize = false;
			}
			Contraption instance = Contraption.Instance;
			bool flag = false;
			foreach (PartData part4 in m_runtimeContraption.Parts)
			{
				ComponentData componentData = m_components[part4.ComponentIndex];
				if (componentData.Time + componentData.GenerateTime - Time.time < 0f)
				{
					flag = true;
					part4.Part.GenerationIndex = Instance.GenerationCount;
				}
			}
			if (flag)
			{
				Instance.GenerationCount++;
			}
			foreach (KeyValuePair<int, int> item in m_runtimeContraption.PartIndexMap)
			{
				PartData partData2 = m_runtimeContraption.Parts[item.Value];
				BasePart part = partData2.Part;
				ComponentData componentData2 = m_components[partData2.ComponentIndex];
				if (componentData2.Time + componentData2.GenerateTime - Time.time < 0f)
				{
					instance.SetPartMap(part.CoordX, part.CoordY, part.GenerationIndex, part);
				}
			}
			foreach (PartData part5 in m_runtimeContraption.Parts)
			{
				ComponentData componentData3 = m_components[part5.ComponentIndex];
				float num4 = componentData3.Time + componentData3.GenerateTime - Time.time;
				if (num4 < 0f)
				{
					if (part5.Original == null)
					{
						UnityEngine.Object.Destroy(part5.Part);
						continue;
					}
					part5.Generator.OnPartGenerated();
					BasePart part2 = part5.Part;
					Vector3 position = part5.Generator.rigidbody.position + part5.Generator.transform.right * part5.Distance;
					position.z = part2.transform.position.z;
					part2.gameObject.SetActive(value: true);
					part2.enabled = true;
					part2.gameObject.tag = "Contraption";
					for (int m = 0; m < part2.transform.childCount; m++)
					{
						part2.transform.GetChild(m).gameObject.tag = "Contraption";
					}
					part2.ChangeVisualConnections();
					part2.EnsureRigidbody();
					part2.transform.position = position;
					part2.transform.rotation = part5.Original.transform.rotation;
					part2.rigidbody.position = position;
					part2.rigidbody.velocity = part5.Generator.rigidbody.velocity;
					instance.AddRuntimePart(part2);
					continue;
				}
				(Renderer, Color)[] renderers = part5.Renderers;
				for (int n = 0; n < renderers.Length; n++)
				{
					(Renderer, Color) tuple = renderers[n];
					(MeshRenderer, Color) tuple2 = ((MeshRenderer)tuple.Item1, tuple.Item2);
					if (tuple2.Item1 != null)
					{
						Color color = Color.Lerp(new Color(0.5f, 0.75f, 1f, 0.1f), tuple2.Item2, 1f / (num4 + 1f));
						tuple2.Item1.material.color = color;
					}
				}
			}
			List<BasePart> list = new List<BasePart>();
			List<PartData> list2 = new List<PartData>();
			foreach (PartData item2 in parts)
			{
				ComponentData componentData4 = m_components[item2.ComponentIndex];
				if (componentData4.Time + componentData4.GenerateTime - Time.time < 0f)
				{
					list.Add(item2.Part);
					list2.Add(item2);
				}
			}
			CreateJoints();
			foreach (PartData item3 in list2)
			{
				BasePart part3 = item3.Part;
				part3.ConnectedComponent = item3.ComponentIndex;
				part3.StrictConnectedComponent = item3.ComponentIndex;
			}
			foreach (BasePart item4 in list)
			{
				item4.Initialize();
				item4.PostInitialize();
			}
			if (list.Count != 0)
			{
				if (INSettings.GetBool(INFeature.FrameJoint))
				{
					FrameJointManager.Instance.AddFrameParts(list);
				}
				if (INSettings.GetBool(INFeature.ElectricalSystem))
				{
					ElectricalSystem.Instance.InitializeElectricalParts(list);
				}
				foreach (BasePart item5 in list)
				{
					item5.ConnectedComponent = -1;
					item5.StrictConnectedComponent = -1;
				}
			}
			int count3 = parts.Count;
			int[] array2 = new int[count3];
			int num5 = 0;
			int num6 = 0;
			while (num5 < parts.Count && num6 < count3)
			{
				PartData partData3 = parts[num5];
				ComponentData componentData5 = m_components[partData3.ComponentIndex];
				if (componentData5.Time + componentData5.GenerateTime - Time.time < 0f)
				{
					array2[num6] = -1;
					parts.RemoveAt(num5);
				}
				else
				{
					array2[num6] = num5;
					num5++;
				}
				num6++;
			}
			bool flag2 = false;
			foreach (KeyValuePair<int, int> item6 in new List<KeyValuePair<int, int>>(runtimeContraption.PartIndexMap))
			{
				int num7 = array2[item6.Value];
				if (num7 == -1)
				{
					flag2 = true;
					runtimeContraption.PartIndexMap.Remove(item6.Key);
				}
				else
				{
					runtimeContraption.PartIndexMap[item6.Key] = num7;
				}
			}
			for (int num8 = 0; num8 < parts.Count; num8++)
			{
				PartData value2 = parts[num8];
				if (value2.EnclosedPartIndex != -1)
				{
					value2.EnclosedPartIndex = array2[value2.EnclosedPartIndex];
					parts[num8] = value2;
				}
			}
			if (flag2)
			{
				m_disjointSet = new DisjointSet(parts.Count);
				Connect(0, parts.Count - 1);
				Contraption.Instance.UpdateConnectedComponents();
			}
		}

		private IEnumerable<PartData> GetGeneratedParts()
		{
			foreach (PartData part in m_runtimeContraption.Parts)
			{
				ComponentData componentData = m_components[part.ComponentIndex];
				if (Time.time > componentData.Time + componentData.GenerateTime)
				{
					yield return part;
				}
			}
		}

		private void Connect(int start, int end)
		{
			Contraption instance = Contraption.Instance;
			RuntimeContraption runtimeContraption = m_runtimeContraption;
			for (int i = start; i <= end; i++)
			{
				PartData partData = runtimeContraption.Parts[i];
				BasePart part = partData.Part;
				if (partData.EnclosedPartIndex != -1)
				{
					m_disjointSet.Union(i, partData.EnclosedPartIndex);
				}
				int coordX = part.m_coordX;
				int coordY = part.m_coordY;
				if (part is Balloon)
				{
					int j = 1;
					int x = 0;
					int y = 1;
					int num = INSettings.GetInt(INFeature.BalloonConnectionDistance);
					BasePart basePart = null;
					int num2 = -1;
					if (INSettings.GetBool(INFeature.RotatableBalloon))
					{
						BasePart.GetDirection((BasePart.GridRotation)((int)(part.m_gridRotation + 1) % 4), out x, out y);
					}
					for (; j < num + 1; j++)
					{
						if (!(basePart == null))
						{
							break;
						}
						num2 = runtimeContraption.FindPartIndexAt(part.m_coordX - j * x, part.m_coordY - j * y);
						basePart = runtimeContraption.GetPart(num2);
						if (basePart != null && !basePart.IsPartOfChassis() && basePart.m_partType != BasePart.PartType.Pig && basePart.m_partType != BasePart.PartType.Kicker)
						{
							basePart = null;
						}
					}
					if (num2 != -1)
					{
						m_disjointSet.Union(i, num2);
					}
					continue;
				}
				if (part is Rope)
				{
					int num3;
					int num4;
					if (part.GetCustomJointConnectionDirection() == BasePart.JointConnectionDirection.LeftAndRight)
					{
						num3 = runtimeContraption.FindPartIndexAt(coordX - 1, coordY);
						num4 = runtimeContraption.FindPartIndexAt(coordX + 1, coordY);
					}
					else
					{
						num3 = runtimeContraption.FindPartIndexAt(coordX, coordY + 1);
						num4 = runtimeContraption.FindPartIndexAt(coordX, coordY - 1);
					}
					BasePart basePart2 = runtimeContraption.GetPart(num3);
					BasePart basePart3 = runtimeContraption.GetPart(num4);
					if ((bool)basePart2 && basePart2 is Rope && basePart2.m_gridRotation != part.m_gridRotation)
					{
						basePart2 = null;
					}
					if ((bool)basePart2 && !(basePart2 is Rope) && !(basePart2 is Frame) && !(basePart2 is Kicker))
					{
						basePart2 = null;
					}
					if ((bool)basePart3 && basePart3 is Rope && basePart3.m_gridRotation != part.m_gridRotation)
					{
						basePart3 = null;
					}
					if ((bool)basePart3 && !(basePart3 is Rope) && !(basePart3 is Frame) && !(basePart3 is Kicker))
					{
						basePart3 = null;
					}
					if (basePart2 != null)
					{
						m_disjointSet.Union(i, num3);
					}
					if (basePart3 != null)
					{
						m_disjointSet.Union(i, num4);
					}
					continue;
				}
				if (part is HingePlate hingePlate)
				{
					(int, int)[] directions = HingePlate.Directions;
					for (int k = 0; k < directions.Length; k++)
					{
						(int, int) tuple = directions[k];
						int num5 = runtimeContraption.FindPartIndexAt(coordX + tuple.Item1, coordY + tuple.Item2);
						if (num5 != -1)
						{
							BasePart part2 = runtimeContraption.GetPart(num5);
							if (hingePlate.CanConnectTo(part2))
							{
								m_disjointSet.Union(i, num5);
							}
						}
					}
					continue;
				}
				if (part is ElectricalPart electricalPart)
				{
					int num6 = 1;
					int num7 = 0;
					for (int l = 0; l < 4; l++)
					{
						int num8 = runtimeContraption.FindPartIndexAt(coordX + num6, coordY + num7);
						if (num8 != -1)
						{
							BasePart basePart4 = runtimeContraption.Parts[num8].Part;
							if (basePart4.m_enclosedPart != null)
							{
								basePart4 = basePart4.m_enclosedPart;
							}
							if (electricalPart.CanConnectTo(basePart4, (BitDirection)(1 << l)))
							{
								m_disjointSet.Union(i, num8);
							}
						}
						int num9 = num6;
						num6 = -num7;
						num7 = num9;
					}
					continue;
				}
				int num10 = 1;
				int num11 = 0;
				for (int m = 0; m < 4; m++)
				{
					int num12 = runtimeContraption.FindPartIndexAt(coordX + num10, coordY + num11);
					if (num12 != -1)
					{
						BasePart part3 = runtimeContraption.Parts[num12].Part;
						if (instance.CanConnectTo(part, part3, (BasePart.Direction)m))
						{
							m_disjointSet.Union(i, num12);
						}
					}
					int num13 = num10;
					num10 = -num11;
					num11 = num13;
				}
			}
		}

		private void CreateJoints()
		{
			Contraption instance = Contraption.Instance;
			RuntimeContraption runtimeContraption = m_runtimeContraption;
			foreach (PartData generatedPart in GetGeneratedParts())
			{
				BasePart part = generatedPart.Part;
				int coordX = part.m_coordX;
				int coordY = part.m_coordY;
				BasePart.JointConnectionDirection customJointConnectionDirection = part.GetCustomJointConnectionDirection();
				if (part.m_jointConnectionType != BasePart.JointConnectionType.None)
				{
					BasePart basePart = runtimeContraption.FindPartAt(coordX + 1, coordY);
					BasePart basePart2 = runtimeContraption.FindPartAt(coordX, coordY - 1);
					if (instance.CanConnectTo(part, basePart, BasePart.Direction.Right))
					{
						BasePart.JointConnectionDirection customJointConnectionDirection2 = basePart.GetCustomJointConnectionDirection();
						if (customJointConnectionDirection == BasePart.JointConnectionDirection.Right || customJointConnectionDirection == BasePart.JointConnectionDirection.LeftAndRight)
						{
							instance.AddCustomConnectionBetweenParts(part, basePart);
						}
						else if (customJointConnectionDirection2 == BasePart.JointConnectionDirection.Left || customJointConnectionDirection2 == BasePart.JointConnectionDirection.LeftAndRight)
						{
							instance.AddCustomConnectionBetweenParts(basePart, part);
						}
						else
						{
							instance.AddFixedJoint(part, basePart);
						}
					}
					if (instance.CanConnectTo(part, basePart2, BasePart.Direction.Down))
					{
						BasePart.JointConnectionDirection customJointConnectionDirection3 = basePart2.GetCustomJointConnectionDirection();
						if (customJointConnectionDirection == BasePart.JointConnectionDirection.Down || customJointConnectionDirection == BasePart.JointConnectionDirection.UpAndDown)
						{
							instance.AddCustomConnectionBetweenParts(part, basePart2);
						}
						else if (customJointConnectionDirection3 == BasePart.JointConnectionDirection.Up || customJointConnectionDirection3 == BasePart.JointConnectionDirection.UpAndDown)
						{
							instance.AddCustomConnectionBetweenParts(basePart2, part);
						}
						else
						{
							instance.AddFixedJoint(part, basePart2);
						}
					}
					if (INSettings.GetBool(INFeature.AllDirectionsConnection))
					{
						BasePart basePart3 = runtimeContraption.FindPartAt(coordX - 1, coordY);
						BasePart basePart4 = runtimeContraption.FindPartAt(coordX, coordY + 1);
						if (instance.CanConnectTo(part, basePart3, BasePart.Direction.Left))
						{
							BasePart.JointConnectionDirection customJointConnectionDirection4 = basePart3.GetCustomJointConnectionDirection();
							if (customJointConnectionDirection == BasePart.JointConnectionDirection.Left || customJointConnectionDirection == BasePart.JointConnectionDirection.LeftAndRight)
							{
								instance.AddCustomConnectionBetweenParts(part, basePart3);
							}
							else if (customJointConnectionDirection4 == BasePart.JointConnectionDirection.Right || customJointConnectionDirection4 == BasePart.JointConnectionDirection.LeftAndRight)
							{
								instance.AddCustomConnectionBetweenParts(basePart3, part);
							}
							else
							{
								instance.AddFixedJoint(part, basePart3);
							}
						}
						if (instance.CanConnectTo(part, basePart4, BasePart.Direction.Up))
						{
							BasePart.JointConnectionDirection customJointConnectionDirection5 = basePart4.GetCustomJointConnectionDirection();
							if (customJointConnectionDirection == BasePart.JointConnectionDirection.Up || customJointConnectionDirection == BasePart.JointConnectionDirection.UpAndDown)
							{
								instance.AddCustomConnectionBetweenParts(part, basePart4);
							}
							else if (customJointConnectionDirection5 == BasePart.JointConnectionDirection.Down || customJointConnectionDirection5 == BasePart.JointConnectionDirection.UpAndDown)
							{
								instance.AddCustomConnectionBetweenParts(basePart4, part);
							}
							else
							{
								instance.AddFixedJoint(part, basePart4);
							}
						}
					}
					if (part.m_partType == BasePart.PartType.Rope && part is Rope)
					{
						Rope obj = (Rope)part;
						BasePart basePart5;
						BasePart basePart6;
						if (customJointConnectionDirection == BasePart.JointConnectionDirection.LeftAndRight)
						{
							basePart5 = runtimeContraption.FindPartAt(coordX - 1, coordY);
							basePart6 = runtimeContraption.FindPartAt(coordX + 1, coordY);
						}
						else
						{
							basePart5 = runtimeContraption.FindPartAt(coordX, coordY + 1);
							basePart6 = runtimeContraption.FindPartAt(coordX, coordY - 1);
						}
						if ((bool)basePart5 && basePart5 is Rope && basePart5.m_gridRotation != part.m_gridRotation)
						{
							basePart5 = null;
						}
						if ((bool)basePart5 && !(basePart5 is Rope) && !(basePart5 is Frame) && !(basePart5 is Kicker))
						{
							basePart5 = null;
						}
						if ((bool)basePart6 && basePart6 is Rope && basePart6.m_gridRotation != part.m_gridRotation)
						{
							basePart6 = null;
						}
						if ((bool)basePart6 && !(basePart6 is Rope) && !(basePart6 is Frame) && !(basePart6 is Kicker))
						{
							basePart6 = null;
						}
						obj.Create(basePart5, basePart6);
					}
				}
				if (part.m_partType == BasePart.PartType.Spring && part.m_enclosedInto == null)
				{
					BasePart.Direction direction = BasePart.ConvertDirection(part.GetCustomJointConnectionDirection());
					BasePart partAt = runtimeContraption.GetPartAt(part, direction);
					if (!partAt || !instance.CanConnectTo(part, partAt, direction))
					{
						(part as Spring).CreateSpringBody(direction);
					}
				}
				part.CreateCustomJoints();
				if (part.m_partType == BasePart.PartType.Pig && (bool)WPFMonoBehaviour.levelManager && WPFMonoBehaviour.levelManager.m_disablePigCollisions && part.m_enclosedInto != null)
				{
					part.gameObject.layer = LayerMask.NameToLayer("NonCollidingPart");
				}
				if (part.m_partType != BasePart.PartType.KingPig && part.m_partType != BasePart.PartType.GoldenPig)
				{
					continue;
				}
				for (int i = 0; i <= 1; i++)
				{
					for (int j = -2; j <= 2; j += 4)
					{
						BasePart basePart7 = runtimeContraption.FindPartAt(coordX + j, coordY + i);
						if (basePart7 != null && (basePart7.m_partType == BasePart.PartType.Wings || basePart7.m_partType == BasePart.PartType.MetalWing) && basePart7.collider != null)
						{
							GamePhysics.IgnoreCollision(part.collider, basePart7.collider);
						}
					}
					for (int k = -1; k <= 1; k++)
					{
						if (k == 0 && i == 0)
						{
							continue;
						}
						BasePart basePart8 = runtimeContraption.FindPartAt(coordX + k, coordY + i);
						if (!(basePart8 != null))
						{
							continue;
						}
						// Same pig-to-frame welding as Contraption: IsFramePart also accepts
						// the imported RC1 Android frames.
						if (BasePart.IsFramePart(basePart8.m_partType))
						{
							if (i == 0)
							{
								instance.AddFixedJoint(part, basePart8);
							}
							else
							{
								GamePhysics.IgnoreCollision(part.collider, basePart8.collider);
							}
						}
						else if (basePart8.m_partType == BasePart.PartType.Spring && basePart8.collider != null)
						{
							GamePhysics.IgnoreCollision(part.collider, basePart8.collider);
						}
					}
				}
			}
		}
	}

	private struct DiscretePartData
	{
		public BasePart Part;

		public BasePart Generator;

		public int EnclosedPart;

		public float Distance;

		public float Time;

		public DiscretePartData(BasePart part, BasePart generator, float distance, float time, int enclosedPart)
		{
			Part = part;
			Generator = generator;
			Distance = distance;
			Time = time;
			EnclosedPart = enclosedPart;
		}
	}

	private struct PartInfo
	{
		public BasePart.PartType PartType;

		public int CustomPartIndex;

		public int CoordX;

		public int CoordY;

		public BasePart.GridRotation Rotation;

		public bool Flipped;
	}

	private struct PartData
	{
		public BasePart Part;

		public BasePart Original;

		public GrapplingHook Generator;

		public int EnclosedPartIndex;

		public float Distance;

		public float Time;

		public int ComponentIndex;

		public (Renderer, Color)[] Renderers;

		public PartData(BasePart part, BasePart original, GrapplingHook generator, float distance, float time, int enclosedPartIndex, Renderer[] renderers)
		{
			Part = part;
			Original = original;
			Generator = generator;
			Distance = distance;
			Time = time;
			ComponentIndex = 0;
			EnclosedPartIndex = enclosedPartIndex;
			Renderers = new(Renderer, Color)[renderers.Length];
			for (int i = 0; i < renderers.Length; i++)
			{
				Renderers[i] = (renderers[i], renderers[i].material.color);
			}
		}
	}

	private struct ComponentData
	{
		public float GenerateTime;

		public float Time;
	}

	private class RuntimeContraption
	{
		private List<PartData> m_parts;

		private Dictionary<int, int> m_partIndexMap;

		private Dictionary<int, BasePart> m_partMap;

		public List<PartData> Parts => m_parts;

		public Dictionary<int, int> PartIndexMap => m_partIndexMap;

		public RuntimeContraption()
		{
			m_parts = new List<PartData>();
			m_partIndexMap = new Dictionary<int, int>();
		}

		public BasePart GetPart(int partIndex)
		{
			if (partIndex == -1)
			{
				return null;
			}
			return m_parts[partIndex].Part;
		}

		public BasePart FindPartAt(int x, int y)
		{
			int key = x + (y << 16);
			if (m_partIndexMap.TryGetValue(key, out var value))
			{
				return m_parts[value].Part;
			}
			return null;
		}

		public int FindPartIndexAt(int x, int y)
		{
			int key = x + (y << 16);
			if (m_partIndexMap.TryGetValue(key, out var value))
			{
				return value;
			}
			return -1;
		}

		public BasePart GetPartAt(BasePart part, BasePart.Direction direction)
		{
			int coordX = part.m_coordX;
			int coordY = part.m_coordY;
			return direction switch
			{
				BasePart.Direction.Right => FindPartAt(coordX + 1, coordY), 
				BasePart.Direction.Up => FindPartAt(coordX, coordY + 1), 
				BasePart.Direction.Left => FindPartAt(coordX - 1, coordY), 
				BasePart.Direction.Down => FindPartAt(coordX, coordY - 1), 
				_ => null, 
			};
		}

		public bool CanConnectTo(BasePart part, BasePart.Direction direction)
		{
			int coordX = part.m_coordX;
			int coordY = part.m_coordY;
			switch (direction)
			{
			case BasePart.Direction.Right:
			{
				BasePart part5 = FindPartAt(coordX + 1, coordY);
				return Contraption.Instance.CanConnectTo(part, part5, direction);
			}
			case BasePart.Direction.Up:
			{
				BasePart part4 = FindPartAt(coordX, coordY + 1);
				return Contraption.Instance.CanConnectTo(part, part4, direction);
			}
			case BasePart.Direction.Left:
			{
				BasePart part3 = FindPartAt(coordX - 1, coordY);
				return Contraption.Instance.CanConnectTo(part, part3, direction);
			}
			case BasePart.Direction.Down:
			{
				BasePart part2 = FindPartAt(coordX, coordY - 1);
				return Contraption.Instance.CanConnectTo(part, part2, direction);
			}
			default:
				return false;
			}
		}
	}

	private List<(BasePart, BasePart, int, int)> m_cachedParts;

	private List<PartGenerationSystem> m_systems;

	private PartGenerationSystem[] m_systemMap;

	public int GenerationCount { get; set; }

	public override StatusCode Status => StatusCode.Running;

	public static PartGeneratorManager Instance { get; private set; }

	protected override void Initialize()
	{
		base.Initialize();
		Instance = this;
	}

	public override void Start()
	{
		GenerationCount = 1;
		m_cachedParts = new List<(BasePart, BasePart, int, int)>();
		m_systems = new List<PartGenerationSystem>();
		m_systemMap = Array.Empty<PartGenerationSystem>();
	}

	public override void FixedUpdate()
	{
		int generalConnectedComponentCount = Contraption.Instance.GeneralConnectedComponentCount;
		if (m_systemMap.Length >= generalConnectedComponentCount)
		{
			Array.Clear(m_systemMap, 0, m_systemMap.Length);
		}
		else
		{
			m_systemMap = new PartGenerationSystem[generalConnectedComponentCount];
		}
		foreach (PartGenerationSystem system in m_systems)
		{
			int count = system.RuntimeContraption.Parts.Count;
			foreach (PartData part in system.RuntimeContraption.Parts)
			{
				int generalConnectedComponent = part.Generator.GeneralConnectedComponent;
				if (m_systemMap[generalConnectedComponent] == null || count > m_systemMap[generalConnectedComponent].RuntimeContraption.Parts.Count)
				{
					m_systemMap[generalConnectedComponent] = system;
				}
			}
		}
		foreach (var cachedPart in m_cachedParts)
		{
			(BasePart, GrapplingHook, int, int) tuple = (cachedPart.Item1, (GrapplingHook)cachedPart.Item2, cachedPart.Item3, cachedPart.Item4);
			BasePart item = tuple.Item1;
			int generalConnectedComponent2 = item.GeneralConnectedComponent;
			PartGenerationSystem partGenerationSystem = m_systemMap[generalConnectedComponent2];
			if (partGenerationSystem == null)
			{
				partGenerationSystem = new PartGenerationSystem();
				m_systems.Add(partGenerationSystem);
				m_systemMap[generalConnectedComponent2] = partGenerationSystem;
			}
			partGenerationSystem.InitializePart(item, tuple.Item2, tuple.Item3, tuple.Item4);
		}
		m_cachedParts.Clear();
		foreach (PartGenerationSystem system2 in m_systems)
		{
			system2.UpdateParts();
		}
		m_systems.RemoveAll((PartGenerationSystem system) => system.IsEmpty);
	}

	public override void OnDestroy()
	{
		if (Instance == this)
		{
			Instance = null;
		}
	}

	public void GeneratePart(BasePart template, BasePart generator, int x, int y, bool discrete)
	{
		if (!(WPFMonoBehaviour.levelManager == null))
		{
			m_cachedParts.Add((template, generator, x, y));
		}
	}
}
