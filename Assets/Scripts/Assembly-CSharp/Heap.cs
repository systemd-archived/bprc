using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

public class Heap<T>
{
	private List<T> m_nodes;

	private IComparer<T> m_comparer;

	private ReadOnlyCollection<T> m_unorderedItems;

	public bool IsEmpty => m_nodes.Count == 0;

	public int Count => m_nodes.Count;

	public IComparer<T> Comparer => m_comparer;

	public IReadOnlyCollection<T> UnorderedItems => m_unorderedItems ?? (m_unorderedItems = new ReadOnlyCollection<T>(m_nodes));

	public Heap()
		: this((IComparer<T>)null)
	{
	}

	public Heap(IComparer<T> comparer)
	{
		m_comparer = comparer ?? Comparer<T>.Default;
		m_nodes = new List<T>();
	}

	public Heap(IEnumerable<T> nodes)
		: this(nodes, (IComparer<T>)null)
	{
	}

	public Heap(IEnumerable<T> nodes, IComparer<T> comparer)
	{
		m_comparer = comparer ?? Comparer<T>.Default;
		m_nodes = new List<T>(nodes);
		Heapify();
	}

	private void Heapify()
	{
		int num = m_nodes.Count - 1;
		if (num > 0)
		{
			for (int num2 = num - 1 >> 1; num2 >= 0; num2--)
			{
				MoveDown(num2);
			}
		}
	}

	public void Push(T node)
	{
		m_nodes.Add(node);
		MoveUp(m_nodes.Count - 1);
	}

	public void PushRange(IEnumerable<T> nodes)
	{
		if (m_nodes.Count == 0)
		{
			m_nodes.AddRange(nodes);
			Heapify();
			return;
		}
		foreach (T node in nodes)
		{
			Push(node);
		}
	}

	public T Peek()
	{
		if (m_nodes.Count == 0)
		{
			throw new InvalidOperationException();
		}
		return m_nodes[0];
	}

	public T Pop()
	{
		if (m_nodes.Count == 0)
		{
			throw new InvalidOperationException();
		}
		T result = m_nodes[0];
		int num = m_nodes.Count - 1;
		m_nodes[0] = m_nodes[num];
		m_nodes.RemoveAt(num);
		if (num > 0)
		{
			MoveDown(0);
		}
		return result;
	}

	public T PopAndPush(T node)
	{
		if (m_nodes.Count == 0)
		{
			throw new InvalidOperationException();
		}
		T result = m_nodes[0];
		m_nodes[0] = node;
		MoveDown(0);
		return result;
	}

	public void Clear()
	{
		m_nodes.Clear();
	}

	private void MoveUp(int index)
	{
		T val = m_nodes[index];
		while (index > 0)
		{
			int num = index - 1 >> 1;
			T val2 = m_nodes[num];
			if (m_comparer.Compare(val, val2) < 0)
			{
				m_nodes[num] = val;
				m_nodes[index] = val2;
				index = num;
				continue;
			}
			break;
		}
	}

	private void MoveDown(int index)
	{
		T val = m_nodes[index];
		int count = m_nodes.Count;
		while ((index << 1) + 1 < count)
		{
			int num = (index << 1) + 1;
			if (num + 1 < count && m_comparer.Compare(m_nodes[num + 1], m_nodes[num]) < 0)
			{
				num++;
			}
			T val2 = m_nodes[num];
			if (m_comparer.Compare(val, val2) > 0)
			{
				m_nodes[num] = val;
				m_nodes[index] = val2;
				index = num;
				continue;
			}
			break;
		}
	}
}
