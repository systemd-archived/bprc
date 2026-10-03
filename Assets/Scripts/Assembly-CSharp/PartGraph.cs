using System;
using System.Collections.Generic;

public class PartGraph<T>
{
	public readonly struct Edge
	{
		public readonly BasePart PartA;

		public readonly BasePart PartB;

		public readonly T Value;

		public Edge(BasePart partA, BasePart partB, T value)
		{
			PartA = partA;
			PartB = partB;
			Value = value;
		}
	}

	private Dictionary<BasePart, List<Edge>> m_graph;

	public PartGraph()
	{
		m_graph = new Dictionary<BasePart, List<Edge>>();
	}

	public bool IsConnected(BasePart partA, BasePart partB)
	{
		if (partA == null)
		{
			throw new ArgumentNullException("partA");
		}
		if (partB == null)
		{
			throw new ArgumentNullException("partB");
		}
		if (m_graph.TryGetValue(partA, out var value))
		{
			foreach (Edge item in value)
			{
				if (item.PartB == partB)
				{
					return true;
				}
			}
		}
		return false;
	}

	public void Connect(BasePart partA, BasePart partB, T value)
	{
		if (partA == null)
		{
			throw new ArgumentNullException("partA");
		}
		if (partB == null)
		{
			throw new ArgumentNullException("partB");
		}
		if (!m_graph.TryGetValue(partA, out var value2))
		{
			value2 = new List<Edge>();
			m_graph.Add(partA, value2);
		}
		value2.Add(new Edge(partA, partB, value));
		if (!m_graph.TryGetValue(partB, out value2))
		{
			value2 = new List<Edge>();
			m_graph.Add(partB, value2);
		}
		value2.Add(new Edge(partB, partA, value));
	}

	public void Disconnect(BasePart partA, BasePart partB)
	{
		RemoveEdges(partA, (Edge edge) => edge.PartB == partB);
		RemoveEdges(partB, (Edge edge) => edge.PartB == partA);
	}

	public IEnumerable<Edge> GetAllEdges()
	{
		foreach (List<Edge> value in m_graph.Values)
		{
			foreach (Edge item in value)
			{
				yield return item;
			}
		}
	}

	public IEnumerable<Edge> GetEdges(BasePart part)
	{
		if (part == null)
		{
			throw new ArgumentNullException("part");
		}
		if (!m_graph.TryGetValue(part, out var value))
		{
			yield break;
		}
		foreach (Edge item in value)
		{
			yield return item;
		}
	}

	public IEnumerable<Edge> FindEdges(BasePart part, Predicate<Edge> match)
	{
		if (part == null)
		{
			throw new ArgumentNullException("part");
		}
		if (match == null)
		{
			throw new ArgumentNullException("match");
		}
		if (!m_graph.TryGetValue(part, out var value))
		{
			yield break;
		}
		foreach (Edge item in value)
		{
			if (match(item))
			{
				yield return item;
			}
		}
	}

	public void RemoveEdges(BasePart part, Predicate<Edge> match)
	{
		if (part == null)
		{
			throw new ArgumentNullException("part");
		}
		if (match == null)
		{
			throw new ArgumentNullException("match");
		}
		if (m_graph.TryGetValue(part, out var value))
		{
			value.RemoveAll(match);
		}
	}

	public void RemoveEdges(Predicate<Edge> match)
	{
		if (match == null)
		{
			throw new ArgumentNullException("match");
		}
		foreach (List<Edge> value in m_graph.Values)
		{
			value.RemoveAll(match);
		}
	}

	public void RemoveParts(Predicate<BasePart> match)
	{
		if (match == null)
		{
			throw new ArgumentNullException("match");
		}
		List<BasePart> list = new List<BasePart>();
		foreach (BasePart key in m_graph.Keys)
		{
			if (match(key))
			{
				list.Add(key);
			}
		}
		foreach (BasePart item in list)
		{
			m_graph.Remove(item);
		}
	}

	public IEnumerable<T> GetValues(BasePart part)
	{
		if (part == null)
		{
			throw new ArgumentNullException("part");
		}
		if (!m_graph.TryGetValue(part, out var value))
		{
			yield break;
		}
		foreach (Edge item in value)
		{
			yield return item.Value;
		}
	}

	public IEnumerable<T> FindValues(BasePart part, Predicate<T> match)
	{
		if (part == null)
		{
			throw new ArgumentNullException("part");
		}
		if (match == null)
		{
			throw new ArgumentNullException("match");
		}
		if (!m_graph.TryGetValue(part, out var value))
		{
			yield break;
		}
		foreach (Edge item in value)
		{
			if (match(item.Value))
			{
				yield return item.Value;
			}
		}
	}
}
