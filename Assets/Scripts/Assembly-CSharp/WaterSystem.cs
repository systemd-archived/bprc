using System;
using System.Collections.Generic;
using UnityEngine;

public class WaterSystem : INBehaviour
{
	private class BuoyancyResolver
	{
		private WaterSystem m_system;

		public BuoyancyResolver(WaterSystem system)
		{
			m_system = system;
		}

		public void Resolve()
		{
			List<BasePart> list = new List<BasePart>();
			foreach (BasePart part in Contraption.Instance.Parts)
			{
				if ((part.m_enclosedInto == null || part.m_partType == BasePart.PartType.KingPig || part.m_partType == BasePart.PartType.GoldenPig) && part.rigidbody != null && !part.rigidbody.IsFixed())
				{
					list.Add(part);
				}
			}
			int count = list.Count;
			if (count != 0)
			{
				int num = count * 4;
				Vector2[] array = new Vector2[num];
				Dictionary<BasePart, int> dictionary = new Dictionary<BasePart, int>(count);
				for (int i = 0; i < count; i++)
				{
					dictionary[list[i]] = i;
				}
				for (int j = 0; j < count; j++)
				{
					BasePart basePart = list[j];
					Vector3 position = basePart.transform.position;
					Vector3 right = basePart.transform.right;
					INBounds bounds = INContraption.GetBounds(basePart.rigidbody);
					Vector2 vector = new Vector2(position.x + bounds.X * right.x - bounds.Y * right.y, position.y + bounds.X * right.y + bounds.Y * right.x);
					float num2 = bounds.A - 0.05f * Math.Min(bounds.A, 0.5f);
					float num3 = bounds.B - 0.05f * Math.Min(bounds.B, 0.5f);
					Vector2 vector2 = new Vector2(num2 * right.x, num2 * right.y);
					Vector2 vector3 = new Vector2((0f - num3) * right.y, num3 * right.x);
					array[j * 4] = vector + vector2 + vector3;
					array[j * 4 + 1] = vector - vector2 + vector3;
					array[j * 4 + 2] = vector - vector2 - vector3;
					array[j * 4 + 3] = vector + vector2 - vector3;
				}
				float num4 = 0.05f * INSettings.GetFloat(INFeature.WatertightCoefficient);
				float num5 = 0.1f + 0.1f * INSettings.GetFloat(INFeature.WatertightCoefficient);
				int num6 = 0;
				List<Vector2> list2 = new List<Vector2>();
				HashSet<int> hashSet = new HashSet<int>();
				List<(float, int)>[] array2 = new List<(float, int)>[num];
				foreach (Contraption.JointConnection item8 in Contraption.Instance.JointMap)
				{
					if (!dictionary.TryGetValue(item8.partA, out var value) || !dictionary.TryGetValue(item8.partB, out var value2) || hashSet.Contains(value + value2 * count))
					{
						continue;
					}
					hashSet.Add(value + value2 * count);
					hashSet.Add(value2 + value * count);
					for (int k = 0; k < 4; k++)
					{
						for (int l = 0; l < 4; l++)
						{
							int num7 = value * 4 + k;
							int num8 = value * 4 + (k + 1) % 4;
							int num9 = value2 * 4 + l;
							int num10 = value2 * 4 + (l + 1) % 4;
							Vector2 vector4 = array[num7];
							Vector2 vector5 = array[num8];
							Vector2 vector6 = array[num9];
							Vector2 vector7 = array[num10];
							Vector2 vector8 = vector5 - vector4;
							Vector2 right2 = vector7 - vector6;
							float num11 = Vector.Cross2(vector8, right2);
							if (Math.Abs(num11) < 0.01f)
							{
								continue;
							}
							float num12 = Vector.Cross2(vector6 - vector4, right2) / num11;
							float num13 = Vector.Cross2(vector6 - vector4, vector8) / num11;
							if (num13 > 0f - num5 && num13 < 1f + num5 && num12 > 0f - num5 && num12 < 1f + num5)
							{
								if (array2[num7] == null)
								{
									array2[num7] = new List<(float, int)>();
								}
								if (array2[num9] == null)
								{
									array2[num9] = new List<(float, int)>();
								}
								array2[num7].Add((num12, num + num6));
								array2[num9].Add((num13, num + num6));
								float x = vector6.x + num13 * (vector7.x - vector6.x);
								float y = vector6.y + num13 * (vector7.y - vector6.y);
								list2.Add(new Vector2(x, y));
								num6++;
							}
						}
					}
				}
				DisjointSet disjointSet = new DisjointSet(num + num6);
				for (int m = 0; m < count; m++)
				{
					for (int n = 0; n < 4; n++)
					{
						int num14 = m * 4 + n;
						int item = m * 4 + (n + 1) % 4;
						List<(float, int)> list3 = array2[num14];
						if (list3 == null)
						{
							continue;
						}
						list3.Add((0f, num14));
						list3.Add((1f, item));
						list3.Sort();
						for (int num15 = 0; num15 < list3.Count - 1; num15++)
						{
							(float, int) tuple = list3[num15];
							(float, int) tuple2 = list3[num15 + 1];
							if (tuple2.Item1 - tuple.Item1 < num4)
							{
								disjointSet.Union(tuple.Item2, tuple2.Item2);
							}
						}
					}
				}
				int componentCount;
				int[] componentIndexes = disjointSet.GetComponentIndexes(out componentCount);
				Vector2[] array3 = new Vector2[componentCount];
				int[] array4 = new int[componentCount];
				for (int num16 = 0; num16 < num + num6; num16++)
				{
					int num17 = componentIndexes[num16];
					array3[num17] += ((num16 < num) ? array[num16] : list2[num16 - num]);
					array4[num17]++;
				}
				for (int num18 = 0; num18 < componentCount; num18++)
				{
					int num19 = array4[num18];
					if (num19 != 0)
					{
						array3[num18] /= (float)num19;
					}
				}
				DisjointSet disjointSet2 = new DisjointSet(componentCount);
				Graph<int> graph = new Graph<int>(componentCount);
				for (int num20 = 0; num20 < count; num20++)
				{
					for (int num21 = 0; num21 < 4; num21++)
					{
						int num22 = num20 * 4 + num21;
						int num23 = num20 * 4 + (num21 + 1) % 4;
						List<(float, int)> list4 = array2[num22];
						if (list4 == null)
						{
							int num24 = componentIndexes[num22];
							int num25 = componentIndexes[num23];
							if (num24 != num25)
							{
								graph.AddUndirectedEdge(num24, num25, num20);
								disjointSet2.Union(num24, num25);
							}
							continue;
						}
						for (int num26 = 0; num26 < list4.Count - 1; num26++)
						{
							(float, int) tuple3 = list4[num26];
							(float, int) tuple4 = list4[num26 + 1];
							int num27 = componentIndexes[tuple3.Item2];
							int num28 = componentIndexes[tuple4.Item2];
							if (num27 != num28)
							{
								graph.AddUndirectedEdge(num27, num28, num20);
								disjointSet2.Union(num27, num28);
							}
						}
					}
				}
				bool[] array5 = new bool[componentCount];
				for (int num29 = 0; num29 < componentCount; num29++)
				{
					List<Graph<int>.Edge> edges = graph.GetEdges(num29);
					bool flag = true;
					if (edges.Count > 0)
					{
						int to = edges[0].To;
						foreach (Graph<int>.Edge item9 in edges)
						{
							if (item9.To != to)
							{
								flag = false;
								break;
							}
						}
					}
					array5[num29] = flag;
				}
				int componentCount2;
				int[] componentIndexes2 = disjointSet2.GetComponentIndexes(out componentCount2);
				(int, int, float)[] array6 = new(int, int, float)[componentCount2];
				Array.Fill(array6, (-1, 0, 0f));
				List<(Vector2, int)>[] array7 = new List<(Vector2, int)>[componentCount2];
				for (int num30 = 0; num30 < componentCount; num30++)
				{
					if (!array5[num30])
					{
						int num31 = componentIndexes2[num30];
						float x2 = array3[num30].x;
						ref(int, int, float) reference = ref array6[num31];
						reference.Item2++;
						if (reference.Item1 == -1 || x2 < reference.Item3)
						{
							reference.Item1 = num30;
							reference.Item3 = x2;
						}
					}
				}
				for (int num32 = 0; num32 < componentCount2; num32++)
				{
					int item2 = array6[num32].Item1;
					int item3 = array6[num32].Item2;
					if (item2 == -1)
					{
						continue;
					}
					List<(Vector2, int)> list5 = new List<(Vector2, int)>();
					int num33 = item2;
					int num34 = -1;
					Vector2 left = new Vector2(1f, 0f);
					for (int num35 = 0; num35 < item3 * 2; num35++)
					{
						int item4 = -1;
						Vector2 vector9 = array3[num33];
						Vector2 item5 = default(Vector2);
						Vector2 vector10 = default(Vector2);
						float num36 = float.MaxValue;
						foreach (Graph<int>.Edge edge in graph.GetEdges(num33))
						{
							int to2 = edge.To;
							if (!array5[to2] && to2 != num33)
							{
								Vector2 vector11 = array3[to2];
								Vector2 vector12 = vector11 - vector9;
								float num37 = Vector.Dot2(left, vector12);
								float num38 = Vector.Cross2(left, vector12);
								float num39 = ((num38 == 0f && num37 < 0f) ? MathF.PI : MathF.Atan2(num38, num37));
								if (num39 < num36)
								{
									num34 = to2;
									item4 = edge.Value;
									item5 = vector11;
									vector10 = vector12;
									num36 = num39;
								}
							}
						}
						if (num34 == -1)
						{
							list5.Add((vector9, item4));
							break;
						}
						if (num34 == item2)
						{
							list5.Add((vector9, item4));
							list5.Add((item5, item4));
							break;
						}
						num33 = num34;
						num34 = -1;
						left = vector10;
						list5.Add((vector9, item4));
					}
					array7[num32] = list5;
				}
				bool renderDisplacedArea = INUserSettings.Instance.PartSettings.RenderDisplacedArea;
				if (m_system.m_borderGroup.activeSelf ^ renderDisplacedArea)
				{
					m_system.m_borderGroup.SetActive(renderDisplacedArea);
				}
				if (renderDisplacedArea)
				{
					m_system.RenderBorders(array7);
				}
				(Vector2, bool)[] array8 = new(Vector2, bool)[count];
				(Vector2, int)[] array9 = new(Vector2, int)[Contraption.Instance.ConnectedComponentCount];
				float height = m_system.m_height;
				List<(Vector2, int)>[] array10 = array7;
				foreach (List<(Vector2, int)> list6 in array10)
				{
					int num41 = 0;
					int num42 = 0;
					int num43 = list6.Count - 1;
					int[] array11 = new int[num43];
					List<(float, int)> list7 = new List<(float, int)>();
					for (int num44 = 0; num44 < num43; num44++)
					{
						array11[num44] = -1;
						(Vector2, int) tuple5 = list6[num44];
						(Vector2, int) tuple6 = list6[num44 + 1];
						var (vector13, _) = tuple5;
						var (vector14, _) = tuple6;
						if (vector13.y >= height && vector14.y >= height)
						{
							num41++;
							continue;
						}
						if (vector13.y < height && vector14.y < height)
						{
							num42++;
							continue;
						}
						float num45 = (height - vector13.y) / (vector14.y - vector13.y);
						list7.Add((vector13.x * (1f - num45) + vector14.x * num45, num44));
					}
					if (num42 == 0 && list7.Count == 0)
					{
						continue;
					}
					if (num42 == num43)
					{
						for (int num46 = 0; num46 < num43; num46++)
						{
							(Vector2, int) tuple9 = list6[num46];
							(Vector2, int) tuple10 = list6[num46 + 1];
							Vector2 item6 = tuple9.Item1;
							Vector2 item7 = tuple10.Item1;
							Vector2 vector15 = item7 - item6;
							float num47 = height - 0.5f * (item6.y + item7.y);
							ref(Vector2, bool) reference2 = ref array8[tuple9.Item2];
							reference2.Item1 += new Vector2((0f - num47) * vector15.y, num47 * vector15.x);
							reference2.Item2 = true;
						}
					}
					else
					{
						if (list7.Count <= 0)
						{
							continue;
						}
						list7.Sort();
						for (int num48 = 0; num48 < list7.Count; num48++)
						{
							array11[list7[num48].Item2] = num48;
						}
						int num49 = list7[0].Item2;
						for (int num50 = 0; num50 < num43; num50++)
						{
							(Vector2, int) tuple11 = list6[num49];
							(Vector2, int) tuple12 = list6[num49 + 1];
							Vector2 vector16 = tuple11.Item1;
							Vector2 vector17 = tuple12.Item1;
							bool flag2 = vector17.y >= height;
							if (array11[num49] != -1)
							{
								Vector2 vector18 = new Vector2(list7[array11[num49]].Item1, height);
								if (flag2)
								{
									vector17 = vector18;
								}
								else
								{
									vector16 = vector18;
								}
							}
							Vector2 vector19 = vector17 - vector16;
							float num51 = height - 0.5f * (vector16.y + vector17.y);
							ref(Vector2, bool) reference3 = ref array8[tuple11.Item2];
							reference3.Item1 += new Vector2((0f - num51) * vector19.y, num51 * vector19.x);
							reference3.Item2 = true;
							if (array11[num49] >= 0 && flag2)
							{
								if (array11[num49] == list7.Count - 1)
								{
									break;
								}
								num49 = list7[array11[num49] + 1].Item2;
							}
							else
							{
								num49 = (num49 + 1) % num43;
							}
						}
					}
				}
				float num52 = Math.Abs(Physics.gravity.y) * INSettings.GetFloat(INFeature.BuoyancyCoefficient) * INUserSettings.Instance.PartSettings.BuoyancyCoefficient;
				float num53 = INSettings.GetFloat(INFeature.HydraulicCoefficient);
				for (int num54 = 0; num54 < count; num54++)
				{
					(Vector2, bool) tuple13 = array8[num54];
					if (tuple13.Item2)
					{
						ref(Vector2, int) reference4 = ref array9[list[num54].ConnectedComponent];
						reference4.Item1 += tuple13.Item1;
						reference4.Item2++;
					}
				}
				for (int num55 = 0; num55 < count; num55++)
				{
					(Vector2, bool) tuple14 = array8[num55];
					BasePart basePart2 = list[num55];
					if (tuple14.Item2 && basePart2.rigidbody.useGravity)
					{
						(Vector2, int) tuple15 = array9[basePart2.ConnectedComponent];
						Vector2 vector20 = num52 * num53 * tuple14.Item1;
						Vector2 vector21 = num52 * (1f - num53) / (float)tuple15.Item2 * tuple15.Item1;
						basePart2.rigidbody.EngineAddForce(vector20 + vector21);
					}
				}
				foreach (BasePart part2 in Contraption.Instance.Parts)
				{
					if (part2.transform.position.y < height)
					{
						part2.rigidbody.EngineAddForce(-0.5f * part2.rigidbody.velocity);
					}
				}
			}
			float num56 = Math.Abs(Physics.gravity.y) * INSettings.GetFloat(INFeature.BuoyancyCoefficient) * INUserSettings.Instance.PartSettings.BuoyancyCoefficient;
			Rigidbody[] components = INContraption.Instance.Rigidbodies;
			foreach (Rigidbody rigidbody in components)
			{
				if (rigidbody.IsFixed() || !(rigidbody.GetComponent<BasePart>() == null))
				{
					continue;
				}
				Vector3 position2 = rigidbody.transform.position;
				Vector3 right3 = rigidbody.transform.right;
				INBounds bounds2 = INContraption.GetBounds(rigidbody);
				float num57 = position2.y + bounds2.X * right3.y + bounds2.Y * right3.x;
				float num58 = Math.Max(bounds2.A, bounds2.B) * 2f;
				float num59 = m_system.m_height - (num57 - num58 * 0.5f);
				if (num59 > 0f)
				{
					float y2 = num56 * num58 * Math.Min(num58, num59);
					Vector3 vector22 = new Vector3(0f, y2);
					if (rigidbody.useGravity)
					{
						rigidbody.EngineAddForce(vector22 - rigidbody.velocity * 0.5f);
					}
					else
					{
						rigidbody.EngineAddForce(-rigidbody.velocity * 0.5f);
					}
				}
			}
		}
	}

	private bool m_enabled;

	private float m_height;

	private GameObject m_water;

	private GameObject m_borderGroup;

	private BuoyancyResolver m_resolver;

	private Dictionary<MeshRenderer, (Color, Color)> m_rendererTable;

	public override StatusCode Status => StatusCode.Running;

	public static void Create()
	{
		new WaterSystem().Initialize();
	}

	private void Initialize()
	{
		INContraption.Instance.AddBehaviour(this);
		m_enabled = false;
		m_resolver = new BuoyancyResolver(this);
		m_rendererTable = new Dictionary<MeshRenderer, (Color, Color)>();
	}

	private void CreateWater()
	{
		GameObject gameObject = new GameObject("INWater");
		gameObject.transform.parent = Contraption.Instance.transform;
		GameObject gameObject2 = new GameObject("BorderGroup");
		gameObject2.transform.parent = gameObject.transform;
		LineRenderer lineRenderer = gameObject.AddComponent<LineRenderer>();
		Color color = new Color(0.32f, 0.56f, 0.8f, 0.5f);
		lineRenderer.material = new Material(Shader.Find("GUI/Text Shader"));
		lineRenderer.material.color = color;
		lineRenderer.startWidth = 0.5f;
		lineRenderer.endWidth = 0.5f;
		lineRenderer.startColor = color;
		lineRenderer.endColor = color;
		lineRenderer.positionCount = 2;
		gameObject.AddComponent<MeshFilter>().sharedMesh = INUnity.QuadMesh;
		Material material = new Material(INUnity.ColorTransparentShader);
		material.color = new Color(0.28f, 0.49f, 0.7f, 0.3f);
		gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
		m_water = gameObject;
		m_borderGroup = gameObject2;
	}

	private void RenderBorders(List<(Vector2, int)>[] borders)
	{
		for (int i = 0; i < m_borderGroup.transform.childCount; i++)
		{
			Transform child = m_borderGroup.transform.GetChild(i);
			if (child.name.StartsWith("Border"))
			{
				int num = int.Parse(child.name.Substring(7));
				if (num >= borders.Length || borders[num].Count == 0)
				{
					child.gameObject.SetActive(value: false);
				}
			}
		}
		for (int j = 0; j < borders.Length; j++)
		{
			if (borders[j].Count != 0)
			{
				List<(Vector2, int)> border = borders[j];
				RenderBorder(j, border);
			}
		}
	}

	private void RenderBorder(int index, List<(Vector2, int)> border)
	{
		string text = "Border_" + index;
		GameObject gameObject = m_borderGroup.transform.Find(text)?.gameObject;
		LineRenderer component;
		if (gameObject == null)
		{
			gameObject = new GameObject(text, typeof(LineRenderer));
			gameObject.transform.parent = m_borderGroup.transform;
			component = gameObject.GetComponent<LineRenderer>();
			component.material = new Material(Shader.Find("GUI/Text Shader"));
			Color white = Color.white;
			component.material.color = white;
			component.startWidth = 0.05f;
			component.endWidth = 0.05f;
			component.startColor = white;
			component.endColor = white;
		}
		else
		{
			component = gameObject.GetComponent<LineRenderer>();
			gameObject.SetActive(value: true);
		}
		int num = (component.positionCount = border.Count);
		for (int i = 0; i < num; i++)
		{
			Vector2 item = border[i].Item1;
			component.SetPosition(i, new Vector3(item.x, item.y, -1f));
		}
	}

	public override void FixedUpdate()
	{
		if (m_enabled)
		{
			m_resolver.Resolve();
		}
	}

	public override void Update()
	{
		bool enableWaterSystem = INUserSettings.Instance.PartSettings.EnableWaterSystem;
		float waterLevel = INUserSettings.Instance.PartSettings.WaterLevel;
		m_height = waterLevel;
		if (enableWaterSystem != m_enabled)
		{
			SetEnabled(enableWaterSystem);
		}
		if (enableWaterSystem)
		{
			UpdateRenderers();
		}
	}

	public override void LateUpdate()
	{
		if (m_enabled)
		{
			UpdateWaterPosition();
		}
	}

	private void UpdateWaterPosition()
	{
		Camera component = WPFMonoBehaviour.ingameCamera.GetComponent<Camera>();
		Vector3 position = component.transform.position;
		float x = position.x;
		float y = position.y;
		float num = component.orthographicSize * 1.1f;
		float num2 = num * (float)Screen.width / (float)Screen.height;
		float height = m_height;
		LineRenderer component2 = m_water.GetComponent<LineRenderer>();
		component2.SetPosition(0, new Vector3(x - num2, height - 0.25f));
		component2.SetPosition(1, new Vector3(x + num2, height - 0.25f));
		float num3 = y - num;
		float num4 = Math.Clamp(height, y - num, y + num);
		m_water.transform.position = new Vector3(x, (num3 + num4) * 0.5f);
		m_water.transform.localScale = new Vector3(num2 * 2f, num4 - num3, 1f);
	}

	private void UpdateRenderers()
	{
		MeshRenderer[] components = INContraption.Instance.GetComponents<MeshRenderer>();
		Dictionary<MeshRenderer, (Color, Color)> dictionary = new Dictionary<MeshRenderer, (Color, Color)>();
		MeshRenderer[] array = components;
		foreach (MeshRenderer meshRenderer in array)
		{
			if (meshRenderer != null && meshRenderer.name != "INWater")
			{
				float num = meshRenderer.transform.position.y - m_height;
				if (num < 0f)
				{
					Color color = meshRenderer.material.color;
					(Color, Color) value;
					Color item = ((m_rendererTable.TryGetValue(meshRenderer, out value) && ColorEquals(color, value.Item2)) ? value.Item1 : color);
					float num2 = 0.7f / (1f - num / 64f) + 0.3f;
					Color color2 = new Color((num2 - 0.1f) * 0.8f * item.r, (num2 - 0.05f) * 0.9f * item.g, num2 * item.b, num2 * item.a);
					dictionary.Add(meshRenderer, (item, color2));
					meshRenderer.material.color = color2;
				}
			}
		}
		foreach (KeyValuePair<MeshRenderer, (Color, Color)> item2 in m_rendererTable)
		{
			MeshRenderer key = item2.Key;
			if (key != null && !dictionary.ContainsKey(key))
			{
				key.material.color = item2.Value.Item1;
			}
		}
		m_rendererTable = dictionary;
		static bool ColorEquals(Color x, Color y)
		{
			if (x.r == y.r && x.g == y.g)
			{
				return x.b == y.b;
			}
			return false;
		}
	}

	private void SetEnabled(bool enabled)
	{
		m_enabled = enabled;
		if (enabled)
		{
			if (m_water == null)
			{
				CreateWater();
			}
			m_water.SetActive(value: true);
			UpdateWaterPosition();
			return;
		}
		m_water.SetActive(value: false);
		foreach (KeyValuePair<MeshRenderer, (Color, Color)> item in m_rendererTable)
		{
			MeshRenderer key = item.Key;
			if (key != null)
			{
				key.material.color = item.Value.Item1;
			}
		}
		m_rendererTable.Clear();
	}
}
