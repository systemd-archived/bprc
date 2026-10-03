using System;

internal class ObjectPool<T> where T : class
{
	private struct Entry
	{
		public T Value;

		public bool IsRented;
	}

	private int m_size;

	private Func<T> m_factory;

	private Action<T> m_clear;

	private Entry[] m_items;

	public ObjectPool(Func<T> factory, Action<T> clear)
		: this(factory, clear, 4)
	{
	}

	public ObjectPool(Func<T> factory, Action<T> clear, int size)
	{
		m_size = size;
		m_factory = factory;
		m_clear = clear;
		m_items = new Entry[size];
	}

	public T Rent()
	{
		for (int i = 0; i < m_size; i++)
		{
			ref Entry reference = ref m_items[i];
			if (!reference.IsRented && reference.Value != null)
			{
				reference.IsRented = true;
				return reference.Value;
			}
		}
		for (int j = 0; j < m_size; j++)
		{
			ref Entry reference2 = ref m_items[j];
			if (!reference2.IsRented && reference2.Value == null)
			{
				reference2.IsRented = true;
				reference2.Value = m_factory();
				return reference2.Value;
			}
		}
		return m_factory();
	}

	public void Return(T value)
	{
		m_clear(value);
		for (int i = 0; i < m_size; i++)
		{
			ref Entry reference = ref m_items[i];
			if (reference.IsRented && reference.Value == value)
			{
				reference.IsRented = false;
				break;
			}
		}
	}
}
