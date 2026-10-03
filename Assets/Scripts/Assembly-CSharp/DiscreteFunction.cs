using System;

public struct DiscreteFunction
{
	private float m_min;

	private float m_max;

	private float m_delta;

	private float[] m_values;

	public float Min => m_min;

	public float Max => m_max;

	public float Delta => m_delta;

	public float[] RawArray => m_values;

	public DiscreteFunction(float min, float max, float delta)
	{
		m_min = min;
		m_max = max;
		m_delta = delta;
		m_values = new float[(int)Math.Round((max - min) / delta) + 1];
	}

	public bool IsInDomain(float x)
	{
		if (m_min <= x)
		{
			return x <= m_max;
		}
		return false;
	}

	public float Get(float x)
	{
		if (!IsInDomain(x))
		{
			throw new ArgumentOutOfRangeException("x");
		}
		int num = (int)Math.Round((x - m_min) / m_delta);
		return m_values[num];
	}

	public void Set(float value)
	{
		for (int i = 0; i < m_values.Length; i++)
		{
			m_values[i] = value;
		}
	}

	public void Set(Func<float, float> f)
	{
		for (int i = 0; i < m_values.Length; i++)
		{
			float arg = m_min + m_delta * (float)i;
			m_values[i] = f(arg);
		}
	}

	public void Set(Func<float, float, float> f)
	{
		for (int i = 0; i < m_values.Length; i++)
		{
			float arg = m_min + m_delta * (float)i;
			float arg2 = m_values[i];
			m_values[i] = f(arg, arg2);
		}
	}
}
