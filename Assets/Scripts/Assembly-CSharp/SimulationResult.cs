public struct SimulationResult
{
	public CircuitElement Element;

	public Electrode Electrode;

	public double U;

	public double I;

	public bool IsGrounded;

	public SimulationResult(CircuitElement element, Electrode electrode, double u, double i, bool isGrounded)
	{
		Element = element;
		Electrode = electrode;
		U = u;
		I = i;
		IsGrounded = isGrounded;
	}
}
