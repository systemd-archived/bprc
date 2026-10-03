public class TemperatureSystem : INBehaviour
{
	public override StatusCode Status => StatusCode.Running;

	public static void Create()
	{
		new TemperatureSystem().Initialize();
	}

	private void Initialize()
	{
		INContraption.Instance.AddBehaviour(this);
	}

	public override void FixedUpdate()
	{
		foreach (BasePart part in Contraption.Instance.Parts)
		{
			part.Temperature *= 0.01f;
		}
	}
}
