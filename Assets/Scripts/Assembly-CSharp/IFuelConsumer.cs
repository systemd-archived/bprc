using Innovation;

public interface IFuelConsumer : IBasePart
{
	int FuelComponentIndex { get; set; }

	float RequiredFuelAmount { get; }

	void ConsumeFuel(float amount);
}
