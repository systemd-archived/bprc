public class Electrode
{
	private CircuitElement m_element;

	private Electrode m_connectedElectrode;

	private bool m_closed;

	// 标记：本电极是本模组“斜向发送器”新增的无线链路电极，计算等电位分组时应忽略（避免自反馈）
	public bool IsWirelessLink;

	public CircuitElement Element => m_element;

	public CircuitElement ConnectedElement => m_connectedElectrode?.m_element;

	public Electrode ConnectedElectrode => m_connectedElectrode;

	public bool IsClosed => m_closed;

	public bool IsConnected
	{
		get
		{
			if (m_closed && m_connectedElectrode != null)
			{
				return m_connectedElectrode.m_closed;
			}
			return false;
		}
	}

	public Electrode(CircuitElement element, Electrode connectedElectrode)
		: this(element, connectedElectrode, closed: true)
	{
	}

	public Electrode(CircuitElement element, Electrode connectedElectrode, bool closed)
	{
		m_element = element;
		m_connectedElectrode = connectedElectrode;
		m_closed = closed;
	}

	public void Connect(Electrode connectedElectrode)
	{
		m_connectedElectrode = connectedElectrode;
	}

	public void Disconnect()
	{
		m_connectedElectrode = null;
	}

	public void Switch(bool closed)
	{
		m_closed = closed;
	}
}
