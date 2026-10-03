using System.Collections.Generic;
using Innovation;

public interface IFuelContainer : IBasePart
{
	int FuelComponentIndex { get; set; }

	float SupplyFuelAmount { get; }

	float RefuelingAmount { get; }

	void SupplyFuel(float amount);

	void Refuel(float amount);

	IEnumerable<BasePart> GetConnectedParts();
}
