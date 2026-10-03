public static class PartTypeExtensions
{
	public static bool IsValid(this SortedPartType sortedPartType)
	{
		return sortedPartType switch
		{
			SortedPartType.JetEngine => INSettings.GetBool(INFeature.FuelSystem), 
			// The imported RC1 thrusters (L1_turbo/L1_jet) keep the JetEngine driver and its
			// fuel requirement but got their own sorted type, so they have to be gated the
			// same way as the stock jet; otherwise they would become available in every
			// version instead of only where the fuel system is on.
			SortedPartType.AndroidThruster => INSettings.GetBool(INFeature.FuelSystem), 
			SortedPartType.ElectricalPart => INSettings.GetBool(INFeature.ElectricalSystem), 
			_ => true, 
		};
	}
}
