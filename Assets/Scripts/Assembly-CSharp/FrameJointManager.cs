using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class FrameJointManager : PartManager
{
	private bool m_needsUpdate;

	private List<BasePart> m_cacheParts;

	private Dictionary<BasePart, int> m_partIndexMap;

	private float m_accumulator;

	private static int s_frameFrameRate = 50;

	public static int FrameFrameRate
	{
		get
		{
			return s_frameFrameRate;
		}
		set
		{
			if (value >= 1)
			{
				s_frameFrameRate = value;
			}
		}
	}

	public override StatusCode Status => StatusCode.Running;

	public static FrameJointManager Instance { get; private set; }

	protected override void Initialize()
	{
		base.Initialize();
		Instance = this;
	}

	public override void Start()
	{
		m_partIndexMap = new Dictionary<BasePart, int>();
		AddFrameParts(Contraption.Instance.Parts);
	}

	public override void FixedUpdate()
	{
		if (m_needsUpdate)
		{
			m_accumulator += Time.fixedDeltaTime;
			if (m_accumulator >= 1f / FrameFrameRate)
			{
				m_accumulator = 0f;
				AddFrameJoints(m_cacheParts);
				m_needsUpdate = false;
				m_cacheParts = null;
			}
		}
	}

	public override void OnDestroy()
	{
		if (Instance == this)
		{
			Instance = null;
		}
	}

	public void AddFrameParts(List<BasePart> parts)
	{
		m_needsUpdate = true;
		if (m_cacheParts == null)
		{
			m_cacheParts = parts;
		}
		else
		{
			m_cacheParts.AddRange(parts);
		}
	}

	private void AddFrameJoints(List<BasePart> parts)
	{
		List<(BasePart, byte)> list = new List<(BasePart, byte)>();
		foreach (BasePart part in parts)
		{
			byte b = 0;
			BasePart enclosedPart = part.EnclosedPart;
			bool flag = enclosedPart != null && enclosedPart.Type == BasePart.PartType.SpringBoxingGlove && enclosedPart.Index == 4;
			// Metal-framed parts (including the imported RC1 Android frames, which used to be
			// typed MetalFrame) take bit 1, wooden frames bit 2.
			if (BasePart.IsMetalFramePart(part.Type) && flag)
			{
				b |= 1;
			}
			if (part.Type == BasePart.PartType.WoodenFrame && flag)
			{
				b |= 2;
			}
			if (part.IsLightFrame())
			{
				b |= 4;
			}
			if (part.IsBracketFrame())
			{
				b |= 8;
			}
			if (b > 0)
			{
				list.Add((part, b));
			}
		}
		int count = list.Count;
		for (int i = 0; i < count; i++)
		{
			m_partIndexMap[list[i].Item1] = i;
		}
		List<BasePart> list2 = new List<BasePart>();
		foreach (BasePart part2 in parts)
		{
			if (part2.IsBracketFrame())
			{
				list2.Add(part2);
			}
		}
		int count2 = list2.Count;
		DisjointSet disjointSet = new DisjointSet(count2);
		for (int j = 0; j < count2; j++)
		{
			for (int k = j + 1; k < count2; k++)
			{
				BasePart basePart = list2[j];
				BasePart basePart2 = list2[k];
				int num = basePart2.m_coordX - basePart.m_coordX;
				int num2 = basePart2.m_coordY - basePart.m_coordY;
				if (num * num + num2 * num2 == 1)
				{
					disjointSet.Union(j, k);
				}
			}
		}
		int componentCount;
		int[] componentIndexes = disjointSet.GetComponentIndexes(out componentCount);
		int[] array = new int[count];
		for (int l = 0; l < count2; l++)
		{
			int num3 = m_partIndexMap[list2[l]];
			int num4 = componentIndexes[l];
			array[num3] = num4;
		}
		Heap<(float, int)>[] array2 = new Heap<(float, int)>[count];
		for (int m = 0; m < count; m++)
		{
			array2[m] = new Heap<(float, int)>();
		}
		bool useMultithread = INUserSettings.Instance?.StarSeaSettings.UseMultithreadFrameSolver ?? false;
		if (useMultithread)
		{
			Vector3[] positions = new Vector3[count];
			byte[] codes = new byte[count];
			int[] comps = new int[count];
			int[] strictComps = new int[count];
			for (int i = 0; i < count; i++)
			{
				(BasePart, byte) tuple = list[i];
				positions[i] = tuple.Item1.transform.position;
				codes[i] = tuple.Item2;
				comps[i] = array[i];
				strictComps[i] = tuple.Item1.StrictConnectedComponent;
			}
			float[][] distRows = new float[count][];
			bool[][] validRows = new bool[count][];
			Parallel.For(0, count, new ParallelOptions { MaxDegreeOfParallelism = System.Environment.ProcessorCount }, n =>
			{
				float[] distRow = new float[count];
				bool[] validRow = new bool[count];
				byte bn = codes[n];
				for (int num5 = n + 1; num5 < count; num5++)
				{
					byte b2 = (byte)(bn & codes[num5]);
					if (b2 == 0 || !(((b2 & 8) > 0) ? (comps[n] == comps[num5]) : (strictComps[n] == strictComps[num5])))
					{
						continue;
					}
					Vector3 position = positions[n];
					Vector3 position2 = positions[num5];
					float num6 = Vector.DistanceSquared2(position, position2);
					float num8 = (((b2 & 3) > 0) ? 48f : 16f);
					if (num6 < num8 * num8)
					{
						distRow[num5] = num6;
						validRow[num5] = true;
					}
				}
				distRows[n] = distRow;
				validRows[n] = validRow;
			});
			for (int n = 0; n < count; n++)
			{
				float[] distRow = distRows[n];
				bool[] validRow = validRows[n];
				for (int num5 = n + 1; num5 < count; num5++)
				{
					if (!validRow[num5])
					{
						continue;
					}
					byte b2 = (byte)(codes[n] & codes[num5]);
					int num7 = (((b2 & 3) > 0) ? 120 : 40);
					float num6 = distRow[num5];
					Heap<(float, int)> heap = array2[n];
					if (heap.Count < num7)
					{
						heap.Push((0f - num6, num5));
					}
					else if (num6 < 0f - heap.Peek().Item1)
					{
						heap.PopAndPush((0f - num6, num5));
					}
					Heap<(float, int)> heap2 = array2[num5];
					if (heap2.Count < num7)
					{
						heap2.Push((0f - num6, n));
					}
					else if (num6 < 0f - heap2.Peek().Item1)
					{
						heap2.PopAndPush((0f - num6, n));
					}
				}
			}
		}
		else
		{
			for (int n = 0; n < count; n++)
			{
				for (int num5 = n + 1; num5 < count; num5++)
				{
					(BasePart, byte) tuple = list[n];
					(BasePart, byte) tuple2 = list[num5];
					BasePart item = tuple.Item1;
					BasePart item2 = tuple2.Item1;
					// Same-faction vehicles are allowed to overlap through each other; that must not weld them
					// into one contraption (a bracket frame only checks the grid cluster, so two overlapping
					// vehicles' frames are "adjacent" and used to be joined). Loose parts stay weldable.
					if (item.ConnectedComponent != item2.ConnectedComponent && MarkerManager.IsInSameTeamStatic(item, item2))
					{
						continue;
					}
					byte b2 = (byte)(tuple.Item2 & tuple2.Item2);
					if (b2 == 0 || !(((b2 & 8) > 0) ? (array[n] == array[num5]) : (item.StrictConnectedComponent == item2.StrictConnectedComponent)))
					{
						continue;
					}
					Vector3 position = item.transform.position;
					Vector3 position2 = item2.transform.position;
					float num6 = Vector.DistanceSquared2(position, position2);
					int num7 = (((b2 & 3) > 0) ? 120 : 40);
					float num8 = (((b2 & 3) > 0) ? 48f : 16f);
					if (num6 < num8 * num8)
					{
						Heap<(float, int)> heap = array2[n];
						if (heap.Count < num7)
						{
							heap.Push((0f - num6, num5));
						}
						else if (num6 < 0f - heap.Peek().Item1)
						{
							heap.PopAndPush((0f - num6, num5));
						}
						Heap<(float, int)> heap2 = array2[num5];
						if (heap2.Count < num7)
						{
							heap2.Push((0f - num6, n));
						}
						else if (num6 < 0f - heap2.Peek().Item1)
						{
							heap2.PopAndPush((0f - num6, n));
						}
					}
				}
			}
		}
		float breakForce = (Contraption.Instance.HasSuperGlue ? float.PositiveInfinity : (WPFMonoBehaviour.gameData.m_jointConnectionStrengthHigh * INSettings.GetFloat(INFeature.ConnectionStrength)));
		for (int num9 = 0; num9 < count; num9++)
		{
			foreach (var unorderedItem in array2[num9].UnorderedItems)
			{
				BasePart item3 = list[num9].Item1;
				BasePart item4 = list[unorderedItem.Item2].Item1;
				FixedJoint fixedJoint = item3.gameObject.AddComponent<FixedJoint>();
				fixedJoint.connectedBody = item4.rigidbody;
				fixedJoint.breakForce = breakForce;
				fixedJoint.enablePreprocessing = true;
				Contraption.Instance.AddJointToGraph(item3, item4, fixedJoint, Contraption.JointType.Frame);
			}
		}
		Clear();
	}

	private void Clear()
	{
		m_partIndexMap.Clear();
	}
}
