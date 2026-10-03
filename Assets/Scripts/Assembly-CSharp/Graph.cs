using System;
using System.Collections.Generic;

public class Graph<T>
{
	public struct Edge
	{
		public int To;

		public T Value;

		public Edge(int to, T value)
		{
			To = to;
			Value = value;
		}
	}

	private int m_count;

	private List<Edge>[] m_graph;

	public int Count => m_count;

	public Graph(int count)
	{
		m_count = count;
		m_graph = new List<Edge>[count];
		for (int i = 0; i < count; i++)
		{
			m_graph[i] = new List<Edge>();
		}
	}

	public void AddDirectedEdge(int u, int v, T value)
	{
		m_graph[u].Add(new Edge(v, value));
	}

	public void AddUndirectedEdge(int u, int v, T value)
	{
		AddDirectedEdge(u, v, value);
		AddDirectedEdge(v, u, value);
	}

	public bool Contains(int u, int v)
	{
		foreach (Edge item in m_graph[u])
		{
			if (item.To == v)
			{
				return true;
			}
		}
		return false;
	}

	public List<Edge> GetEdges(int u)
	{
		return m_graph[u];
	}

	public void Resize(int newCount)
	{
		Array.Resize(ref m_graph, newCount);
		for (int i = m_count; i < newCount; i++)
		{
			m_graph[i] = new List<Edge>();
		}
		m_count = newCount;
	}

	public void Clear()
	{
		for (int i = 0; i < m_count; i++)
		{
			m_graph[i].Clear();
		}
	}
}
