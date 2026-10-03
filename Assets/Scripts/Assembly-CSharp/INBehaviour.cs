public abstract class INBehaviour
{
	public enum StatusCode
	{
		None = 0,
		Building = 1,
		Running = 2
	}

	public virtual StatusCode Status { get; }

	public virtual void Awake()
	{
	}

	public virtual void Start()
	{
	}

	public virtual void FixedUpdate()
	{
	}

	public virtual void Update()
	{
	}

	public virtual void LateUpdate()
	{
	}

	public virtual void OnEnable()
	{
	}

	public virtual void OnDisable()
	{
	}

	public virtual void OnDestroy()
	{
	}
}
