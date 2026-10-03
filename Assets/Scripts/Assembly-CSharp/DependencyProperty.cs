using System.Collections.Generic;

public class DependencyProperty<T>
{
	private string m_name;

	private T m_value;

	private Dependency m_dependency;

	public string Name => m_name;

	public T RawValue
	{
		get
		{
			return m_value;
		}
		set
		{
			m_value = value;
		}
	}

	public T Value
	{
		get
		{
			Binding.Track(m_dependency);
			return m_value;
		}
		set
		{
			if (!EqualityComparer<T>.Default.Equals(m_value, value))
			{
				m_value = value;
				Binding.Trigger(m_dependency);
			}
		}
	}

	public DependencyProperty()
	{
		m_dependency = new Dependency();
	}

	public DependencyProperty(T rawValue)
	{
		m_value = rawValue;
		m_dependency = new Dependency();
	}

	public DependencyProperty(string name, T rawValue)
	{
		m_name = name;
		m_value = rawValue;
		m_dependency = new Dependency();
	}
}
