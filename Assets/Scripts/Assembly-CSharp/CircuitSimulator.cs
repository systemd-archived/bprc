using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

public class CircuitSimulator
{
	private class CircuitEquationSolver
	{
		private int m_equationCount;

		private int m_variableCount;

		private int m_additionalVariableCount;

		private DisjointSet m_disjointSet;

		private int[] m_variableIndexes;

		private double[][] m_augmentedMatrix;

		private VariableValue[] m_variables;

		public VariableValue GetVariable(int index)
		{
			int num = m_variableIndexes[index];
			if (num == -1)
			{
				return new VariableValue(0.0, isConstant: true, isSolved: true);
			}
			return m_variables[num];
		}

		public VariableValue GetAdditionalVariable(int index)
		{
			return m_variables[m_variableCount + index];
		}

		public CircuitEquationSolver()
		{
			m_variableIndexes = Array.Empty<int>();
			m_augmentedMatrix = Array.Empty<double[]>();
			m_variables = Array.Empty<VariableValue>();
		}

		public void Solve(List<Node> nodes, Graph<Branch> nodeGraph, int additionalVariableCount)
		{
			Initialize(nodes, nodeGraph, additionalVariableCount);
			CreateEquations(nodes, nodeGraph);
			SolveEquations();
		}

		public void Clear()
		{
			Array.Clear(m_variableIndexes, 0, m_variableIndexes.Length);
			Array.Clear(m_variables, 0, m_variables.Length);
			double[][] augmentedMatrix = m_augmentedMatrix;
			foreach (double[] array in augmentedMatrix)
			{
				Array.Clear(array, 0, array.Length);
			}
		}

		private void Initialize(List<Node> nodes, Graph<Branch> nodeGraph, int additionalVariableCount)
		{
			int count = nodes.Count;
			m_disjointSet = new DisjointSet(count + 1);
			foreach (Node node in nodes)
			{
				foreach (Graph<Branch>.Edge edge in nodeGraph.GetEdges(node.Index))
				{
					Branch value = edge.Value;
					if (value.IsShortCircuit())
					{
						switch (value.Type)
						{
						case BranchType.Common:
							m_disjointSet.Union(node.Index, edge.To);
							break;
						case BranchType.Grounded:
							m_disjointSet.Union(node.Index, count);
							break;
						}
					}
				}
			}
			if (m_variableIndexes.Length < count + 1)
			{
				m_variableIndexes = new int[count + 1];
			}
			m_disjointSet.GetComponentIndexes(m_variableIndexes, out var componentCount);
			int num = m_variableIndexes[count];
			for (int i = 0; i < count; i++)
			{
				int num2 = m_variableIndexes[i];
				if (num2 > num)
				{
					m_variableIndexes[i] = num2 - 1;
				}
				else if (num2 == num)
				{
					m_variableIndexes[i] = -1;
				}
			}
			m_equationCount = count;
			m_variableCount = componentCount - 1;
			m_additionalVariableCount = additionalVariableCount;
		}

		private void CreateEquations(List<Node> nodes, Graph<Branch> nodeGraph)
		{
			double[][] array = m_augmentedMatrix;
			int equationCount = m_equationCount;
			int num = m_variableCount + m_additionalVariableCount;
			if (equationCount > array.Length || (array.Length != 0 && num + 1 > array[0].Length))
			{
				array = new double[equationCount][];
				for (int i = 0; i < equationCount; i++)
				{
					array[i] = new double[num + 1];
				}
				m_augmentedMatrix = array;
			}
			foreach (Node node in nodes)
			{
				double[] array2 = array[node.Index];
				foreach (Graph<Branch>.Edge edge in nodeGraph.GetEdges(node.Index))
				{
					Branch value = edge.Value;
					bool num2 = value.StartElement == node.ElementIndex;
					int num3 = value.AdditionalVariableIndex;
					if (num3 != -1)
					{
						num3 += m_variableCount;
					}
					int num4 = m_variableIndexes[node.Index];
					int num5 = ((edge.To != -1) ? m_variableIndexes[edge.To] : (-1));
					double num6 = 1.0;
					double num7 = value.InvR;
					if (!num2)
					{
						int num8 = num4;
						num4 = num5;
						num5 = num8;
						num6 = 0.0 - num6;
						num7 = 0.0 - num7;
					}
					BranchType type = value.Type;
					if (type != BranchType.Common && type != BranchType.Grounded)
					{
						continue;
					}
					if (num3 != -1)
					{
						array2[num3] += num6;
						continue;
					}
					if (num4 != -1)
					{
						array2[num4] += num7;
					}
					if (num5 != -1)
					{
						array2[num5] -= num7;
					}
					array2[num] -= value.U * num7;
				}
			}
		}

		private void SolveEquations()
		{
			double[][] augmentedMatrix = m_augmentedMatrix;
			int equationCount = m_equationCount;
			int num = m_variableCount + m_additionalVariableCount;
			if (num > m_variables.Length)
			{
				m_variables = new VariableValue[num];
			}
			int num2 = 0;
			int i = 0;
			while (num2 < equationCount)
			{
				int num3 = num2;
				double num4 = augmentedMatrix[num2][i];
				num4 = ((num4 >= 0.0) ? num4 : (0.0 - num4));
				for (; i < num; i++)
				{
					for (int j = num2 + 1; j < equationCount; j++)
					{
						double num5 = augmentedMatrix[j][i];
						num5 = ((num5 >= 0.0) ? num5 : (0.0 - num5));
						if (num5 > num4)
						{
							num3 = j;
							num4 = num5;
						}
					}
					if (num4 > 1E-08)
					{
						break;
					}
				}
				if (i == num)
				{
					break;
				}
				if (num3 != num2)
				{
					double[] array = augmentedMatrix[num2];
					augmentedMatrix[num2] = augmentedMatrix[num3];
					augmentedMatrix[num3] = array;
				}
				double num6 = augmentedMatrix[num2][i];
				for (int k = num2 + 1; k < equationCount; k++)
				{
					double num7 = augmentedMatrix[k][i] / num6;
					augmentedMatrix[k][i] = 0.0;
					for (int l = i + 1; l < num + 1; l++)
					{
						augmentedMatrix[k][l] -= augmentedMatrix[num2][l] * num7;
					}
				}
				num2++;
				i++;
			}
			for (int num8 = equationCount - 1; num8 >= 0; num8--)
			{
				int num9 = -1;
				double num10 = 0.0;
				bool flag = true;
				double num11 = augmentedMatrix[num8][num];
				for (int m = num8; m < num; m++)
				{
					double num12 = augmentedMatrix[num8][m];
					if (!(-1E-08 <= num12) || !(num12 <= 1E-08))
					{
						ref VariableValue reference = ref m_variables[m];
						if (reference.IsSolved)
						{
							num11 -= num12 * reference.Value;
							flag &= reference.IsConstant;
						}
						else if (num9 != -1)
						{
							reference.IsConstant = m >= m_variableCount;
							reference.IsSolved = true;
							flag &= reference.IsConstant;
						}
						else
						{
							num9 = m;
							num10 = num12;
						}
					}
				}
				if (num9 != -1)
				{
					m_variables[num9] = new VariableValue(num11 / num10, flag, isSolved: true);
				}
			}
			for (int n = 0; n < num; n++)
			{
				if (!m_variables[n].IsSolved)
				{
					m_variables[n] = new VariableValue(0.0, isConstant: false, isSolved: true);
				}
			}
		}
	}

	private struct Node
	{
		public CircuitElement Element;

		public int Index;

		public double Potential;

		public bool IsGrounded;

		public bool[] Visited;

		private static readonly Node s_empty = new Node(null, -1);

		public bool IsEmpty => Index == -1;

		public int ElementIndex => Element.ElementIndex;

		public static Node Empty => s_empty;

		public Node(CircuitElement element, int index)
		{
			Element = element;
			Index = index;
			Potential = 0.0;
			IsGrounded = false;
			Visited = null;
		}
	}

	private struct Branch
	{
		public BranchType Type;

		public double U;

		public double R;

		public double InvR;

		public int StartElement;

		public int StartElectrode;

		public int EndElement;

		public int EndElectrode;

		public int AdditionalVariableIndex;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsShortCircuit()
		{
			return R == 0.0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsOpenCircuit()
		{
			return double.IsPositiveInfinity(R);
		}

		public Branch(int startElement, int startElectrode, int endElement, int endElectrode)
			: this(BranchType.Unknown, 0.0, 0.0, 0.0, startElement, startElectrode, endElement, endElectrode)
		{
		}

		public Branch(BranchType type, double u, double r, double invR, int startElement, int startElectrode, int endElement, int endElectrode)
		{
			Type = type;
			U = u;
			R = r;
			InvR = invR;
			StartElement = startElement;
			StartElectrode = startElectrode;
			EndElement = endElement;
			EndElectrode = endElectrode;
			AdditionalVariableIndex = -1;
		}
	}

	private struct VariableValue
	{
		public double Value;

		public bool IsConstant;

		public bool IsSolved;

		public VariableValue(double value, bool isConstant, bool isSolved)
		{
			Value = value;
			IsConstant = isConstant;
			IsSolved = isSolved;
		}
	}

	private enum BranchType
	{
		Unknown = 0,
		Common = 1,
		Cyclic = 2,
		Grounded = 3,
		Floating = 4
	}

	private int m_frame;

	private double m_deltaTime;

	private List<Node> m_nodes;

	private Dictionary<CircuitElement, Node> m_nodeMap;

	private Graph<Branch> m_nodeGraph;

	private CircuitEquationSolver m_equationSolver;

	private List<KeyValuePair<CircuitElement, SimulationResult>> m_pendingUpdates;

	public int Frame => m_frame;

	public double DeltaTime => m_deltaTime;

	public CircuitSimulator(double deltaTime)
	{
		m_frame = -1;
		m_deltaTime = deltaTime;
		m_nodes = new List<Node>();
		m_nodeMap = new Dictionary<CircuitElement, Node>();
		m_equationSolver = new CircuitEquationSolver();
		m_pendingUpdates = new List<KeyValuePair<CircuitElement, SimulationResult>>();
	}

	public void SetDeltaTime(double deltaTime)
	{
		m_deltaTime = deltaTime;
	}

	public virtual void Clear()
	{
		m_nodes.Clear();
		m_nodeMap.Clear();
		m_equationSolver.Clear();
	}

	public virtual void Simulate(List<CircuitElement> elements)
	{
		m_frame++;
		int count = elements.Count;
		DisjointSet disjointSet = new DisjointSet(count);
		for (int i = 0; i < count; i++)
		{
			elements[i].ElementIndex = i;
		}
		foreach (CircuitElement element in elements)
		{
			foreach (Electrode electrode in element.Electrodes)
			{
				if (electrode.IsConnected)
				{
					disjointSet.Union(element.ElementIndex, electrode.ConnectedElement.ElementIndex);
				}
			}
		}
		int num = 0;
		int[] size;
		int componentCount;
		int[] components = disjointSet.GetComponents(out size, out componentCount);
		List<CircuitElement>[] componentLists = new List<CircuitElement>[componentCount];
		for (int j = 0; j < componentCount; j++)
		{
			List<CircuitElement> list = new List<CircuitElement>();
			for (int k = 0; k < size[j]; k++)
			{
				CircuitElement circuitElement = elements[components[num + k]];
				circuitElement.CircuitIndex = j;
				list.Add(circuitElement);
			}
			num += size[j];
			componentLists[j] = list;
		}
		CircuitSimulator[] simulators = new CircuitSimulator[componentCount];
		if (ElectricalSystem.EnableMultithreadCircuit && componentCount > 1)
		{
			Parallel.For(0, componentCount, j =>
			{
				simulators[j] = new CircuitSimulator(m_deltaTime);
				simulators[j].SimulateCircuit(componentLists[j]);
			});
		}
		else
		{
			for (int j = 0; j < componentCount; j++)
			{
				simulators[j] = this;
				SimulateCircuit(componentLists[j]);
				Clear();
			}
		}
		foreach (CircuitSimulator simulator in simulators)
		{
			if (simulator != null)
			{
				simulator.FlushUpdates(this);
			}
		}
		foreach (CircuitElement element2 in elements)
		{
			element2.Update();
		}
	}

	private void SimulateCircuit(List<CircuitElement> elements)
	{
		int num = 0;
		foreach (CircuitElement element in elements)
		{
			if (element.IsNode())
			{
				Node node = new Node(element, num);
				node.Visited = new bool[element.Electrodes.Count];
				m_nodes.Add(node);
				m_nodeMap.Add(element, node);
				num++;
			}
		}
		if (num == 0)
		{
			ScanSingleBranch(elements);
			return;
		}
		m_nodeGraph = new Graph<Branch>(num);
		int num2 = 0;
		foreach (Node node2 in m_nodes)
		{
			List<Electrode> electrodes = node2.Element.Electrodes;
			for (int i = 0; i < electrodes.Count; i++)
			{
				if (electrodes[i].IsConnected && !node2.Visited[i])
				{
					ScanBranch(node2, i, out var end, out var branch);
					if ((branch.Type == BranchType.Common || branch.Type == BranchType.Grounded) && branch.IsShortCircuit())
					{
						branch.AdditionalVariableIndex = num2;
						num2++;
					}
					if (end.Index == -1 || branch.Type == BranchType.Cyclic)
					{
						m_nodeGraph.AddDirectedEdge(node2.Index, end.Index, branch);
					}
					else
					{
						m_nodeGraph.AddUndirectedEdge(node2.Index, end.Index, branch);
					}
				}
			}
		}
		m_equationSolver.Solve(m_nodes, m_nodeGraph, num2);
		ProcessResults();
	}

	private void ScanSingleBranch(List<CircuitElement> elements)
	{
		if (elements.Count <= 1)
		{
			return;
		}
		CircuitElement circuitElement = elements[0];
		foreach (CircuitElement element in elements)
		{
			if (element.GetConnectedElectrodeCount() <= 1)
			{
				circuitElement = element;
			}
		}
		Node start = new Node(circuitElement, -1);
		int startElectrode = -1;
		for (int i = 0; i < circuitElement.Electrodes.Count; i++)
		{
			if (circuitElement.Electrodes[i].IsConnected)
			{
				startElectrode = i;
				break;
			}
		}
		ScanBranch(start, startElectrode, out var _, out var branch);
		bool flag = circuitElement is Ground || circuitElement is Vcc;
		bool flag2 = branch.Type == BranchType.Grounded;
		start.IsGrounded = flag || flag2;
		if (!flag && flag2)
		{
			start.Potential = 0.0 - branch.U;
		}
		if (circuitElement.GetConnectedElectrodeCount() <= 1 && !flag)
		{
			branch.Type = BranchType.Floating;
		}
		ProcessBranchResults(start, Node.Empty, branch);
	}

	private void ScanBranch(Node start, int startElectrode, out Node end, out Branch branch)
	{
		end = Node.Empty;
		branch = new Branch(start.ElementIndex, startElectrode, -1, -1);
		CircuitElement element = start.Element;
		Electrode electrode = element.Electrodes[startElectrode];
		if (!start.IsEmpty)
		{
			start.Visited[startElectrode] = true;
		}
		ScanElement(element, electrode, direction: true, ref branch);
		if (element.GetConnectedElectrodeCount() == 0)
		{
			return;
		}
		electrode = electrode.ConnectedElectrode;
		element = electrode.Element;
		int num = 0;
		while (true)
		{
			if (element == start.Element)
			{
				branch.Type = BranchType.Cyclic;
				break;
			}
			ScanElement(element, electrode, direction: false, ref branch);
			int connectedElectrodeCount = element.GetConnectedElectrodeCount();
			if (connectedElectrodeCount == 0 || connectedElectrodeCount == 1)
			{
				if (element is Ground || element is Vcc)
				{
					branch.Type = BranchType.Grounded;
				}
				else
				{
					branch.Type = BranchType.Floating;
				}
				break;
			}
			if (connectedElectrodeCount >= 3)
			{
				branch.Type = BranchType.Common;
				break;
			}
			electrode = element.FindNextConnectedElectrode(electrode).ConnectedElectrode;
			element = electrode.Element;
			if (num++ > 10000)
			{
				throw new OverflowException("Infinite loops");
			}
		}
		branch.InvR = 1.0 / branch.R;
		if (element != null)
		{
			int electrodeIndex = element.GetElectrodeIndex(electrode);
			branch.EndElement = element.ElementIndex;
			branch.EndElectrode = electrodeIndex;
			if (element.IsNode())
			{
				end = m_nodeMap[element];
				end.Visited[electrodeIndex] = true;
			}
		}
	}

	private void ScanElement(CircuitElement element, Electrode electrode, bool direction, ref Branch branch)
	{
		ScanElement(element, electrode, direction, out var U, out var R);
		branch.U += U;
		branch.R += R;
	}

	private void ScanElement(CircuitElement element, Electrode electrode, bool direction, out double U, out double R)
	{
		U = 0.0;
		R = 0.0;
		if (element is Resistor resistor)
		{
			R = resistor.Resistance;
		}
		else if (element is VoltageSource voltageSource)
		{
			bool flag = (electrode == voltageSource.Cathode) ^ direction;
			U = voltageSource.Voltage * (flag ? 1.0 : (-1.0));
			R = voltageSource.Resistance;
		}
		else if (element is Capacitor capacitor)
		{
			bool flag2 = (electrode == capacitor.Cathode) ^ direction;
			U = capacitor.Charge / capacitor.Capacitance * (flag2 ? 1.0 : (-1.0));
			R = capacitor.Resistance;
		}
		else if (element is Inductor inductor)
		{
			bool flag3 = (electrode == inductor.Cathode) ^ direction;
			double num = (0.0 - inductor.MagneticFlux) / inductor.Inductance;
			U = num * inductor.Resistance * (flag3 ? 1.0 : (-1.0));
			R = inductor.Resistance;
		}
		else if (element is Ground ground)
		{
			R = ground.Resistance;
		}
		else if (element is Vcc vcc)
		{
			bool flag4 = direction;
			U = vcc.Potential * (flag4 ? 1.0 : (-1.0));
			R = vcc.Resistance;
		}
	}

	private void ProcessResults()
	{
		for (int i = 0; i < m_nodes.Count; i++)
		{
			Node value = m_nodes[i];
			VariableValue variable = m_equationSolver.GetVariable(i);
			value.Potential = variable.Value;
			value.IsGrounded = variable.IsConstant;
			m_nodes[i] = value;
		}
		foreach (Node node in m_nodes)
		{
			foreach (Graph<Branch>.Edge edge in m_nodeGraph.GetEdges(node.Index))
			{
				Branch value2 = edge.Value;
				if (value2.StartElement == node.ElementIndex)
				{
					Node end = ((edge.To != -1) ? m_nodes[edge.To] : Node.Empty);
					ProcessBranchResults(node, end, value2);
				}
			}
		}
	}

	private void ProcessBranchResults(Node start, Node end, Branch branch)
	{
		double num = 0.0;
		double potential = start.Potential;
		bool isGrounded = start.IsGrounded;
		int additionalVariableIndex = branch.AdditionalVariableIndex;
		switch (branch.Type)
		{
		case BranchType.Common:
			num = ((additionalVariableIndex == -1) ? ((potential - end.Potential + branch.U) * branch.InvR) : m_equationSolver.GetAdditionalVariable(additionalVariableIndex).Value);
			break;
		case BranchType.Cyclic:
			num = (branch.IsShortCircuit() ? 0.0 : (branch.U * branch.InvR));
			break;
		case BranchType.Grounded:
			num = ((additionalVariableIndex == -1) ? ((potential + branch.U) * branch.InvR) : m_equationSolver.GetAdditionalVariable(additionalVariableIndex).Value);
			break;
		case BranchType.Floating:
			num = 0.0;
			break;
		}
		CircuitElement element = start.Element;
		Electrode electrode = element.Electrodes[branch.StartElectrode];
		SimulationResult result = new SimulationResult(element, electrode, potential, 0.0 - num, isGrounded);
		ScanElement(element, electrode, direction: true, out var U, out var R);
		result.Element = element;
		result.U += U - num * R;
		UpdateElement(element, result);
		if (element.GetConnectedElectrodeCount() == 0)
		{
			return;
		}
		electrode = electrode.ConnectedElectrode;
		element = electrode.Element;
		int num2 = 0;
		while (true)
		{
			result.Element = element;
			result.Electrode = electrode;
			result.I = num;
			UpdateElement(element, result);
			ScanElement(element, electrode, direction: false, out U, out R);
			result.U += U - num * R;
			int connectedElectrodeCount = element.GetConnectedElectrodeCount();
			if (connectedElectrodeCount == 0 || connectedElectrodeCount == 1 || element.ElementIndex == branch.EndElement)
			{
				break;
			}
			electrode = (result.Electrode = element.FindNextConnectedElectrode(electrode));
			result.I = 0.0 - num;
			UpdateElement(element, result);
			electrode = electrode.ConnectedElectrode;
			element = electrode.Element;
			if (num2++ > 10000)
			{
				throw new OverflowException("Infinite loops");
			}
		}
	}

	private void UpdateElement(CircuitElement element, SimulationResult result)
	{
		m_pendingUpdates.Add(new KeyValuePair<CircuitElement, SimulationResult>(element, result));
	}

	protected void EnqueueUpdate(CircuitElement element, SimulationResult result)
	{
		m_pendingUpdates.Add(new KeyValuePair<CircuitElement, SimulationResult>(element, result));
	}

	public void FlushUpdates(CircuitSimulator simulator)
	{
		foreach (KeyValuePair<CircuitElement, SimulationResult> pair in m_pendingUpdates)
		{
			pair.Key.UpdateElectrode(this, pair.Value);
		}
		m_pendingUpdates.Clear();
	}
}
