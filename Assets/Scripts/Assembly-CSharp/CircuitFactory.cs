public static class CircuitFactory
{
	public static void Connect(Electrode left, Electrode right)
	{
		left.Connect(right);
		right.Connect(left);
	}

	public static void Disconnect(Electrode left, Electrode right)
	{
		left.Disconnect();
		right.Disconnect();
	}

	public static bool IsConnected(Electrode left, Electrode right)
	{
		if (left.IsConnected)
		{
			return right.IsConnected;
		}
		return false;
	}

	public static bool IsConnected(CircuitElement left, CircuitElement right)
	{
		return left.FindElectrode(right) != null;
	}
}
