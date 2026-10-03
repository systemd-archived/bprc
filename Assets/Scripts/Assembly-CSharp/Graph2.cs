using System;
using System.Collections.Generic;

public class Graph2<TEdge> where TEdge : IEdge
{
	private int m_count;

	private List<TEdge>[] m_graph;

	public int Count => m_count;

	public Graph2(int count)
	{
		m_count = count;
		m_graph = new List<TEdge>[count];
		for (int i = 0; i < count; i++)
		{
			m_graph[i] = new List<TEdge>();
		}
	}

	public void AddDirectedEdge(int u, int v, TEdge edge)
	{
		m_graph[u].Add(edge);
	}

	public void AddUndirectedEdge(int u, int v, TEdge edge)
	{
		AddDirectedEdge(u, v, edge);
		AddDirectedEdge(v, u, edge);
	}

	public bool Contains(int u, int v)
	{
		foreach (TEdge item in m_graph[u])
		{
			if (item.To == v)
			{
				return true;
			}
		}
		return false;
	}

	public List<TEdge> GetEdges(int u)
	{
		return m_graph[u];
	}

	public void Resize(int newCount)
	{
		Array.Resize(ref m_graph, newCount);
		for (int i = m_count; i < newCount; i++)
		{
			m_graph[i] = new List<TEdge>();
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
