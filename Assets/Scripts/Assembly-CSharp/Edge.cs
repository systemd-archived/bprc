public struct Edge<T> : IEdge, IEdge<int>
{
	public int To { get; private set; }

	public T Value { get; private set; }

	public Edge(int to, T value)
	{
		To = to;
		Value = value;
	}
}
