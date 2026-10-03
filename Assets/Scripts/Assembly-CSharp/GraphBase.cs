using System;
using System.Collections.Generic;

public class GraphBase<TNode, TEdge, TEdgeList, TGraph> where TNode : IEquatable<TNode> where TEdge : IEdge<TNode> where TEdgeList : ICollection<TEdge> where TGraph : IDictionary<TNode, TEdgeList>
{
	private TGraph m_graph;

	public int Count => m_graph.Count;

	public GraphBase(TGraph graph)
	{
		m_graph = graph;
	}

	public void AddDirectedEdge(TNode u, TNode v, TEdge edge)
	{
		m_graph[u].Add(edge);
	}

	public void AddUndirectedEdge(TNode u, TNode v, TEdge edge)
	{
		AddDirectedEdge(u, v, edge);
		AddDirectedEdge(v, u, edge);
	}

	public bool Contains(TNode u, TNode v)
	{
		foreach (TEdge item in m_graph[u])
		{
			if (item.To.Equals(v))
			{
				return true;
			}
		}
		return false;
	}

	public TEdgeList GetEdges(TNode u)
	{
		return m_graph[u];
	}
}
