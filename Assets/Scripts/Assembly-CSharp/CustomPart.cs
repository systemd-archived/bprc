using Innovation;
using UnityEngine;

public class CustomPart : BasePart
{
	private class EmptyInjectionPart : IInjectionPart
	{
		public IBasePart BasePart { get; set; }
	}

	[SerializeField]
	private IInjectionPart m_injectionPart;

	public override void Inject(IInjectionPart part)
	{
		m_injectionPart = part;
		m_injectionPart.BasePart = this;
	}

	public override void Awake()
	{
		base.Awake();
		m_injectionPart = m_injectionPart ?? new EmptyInjectionPart();
		if (!(m_injectionPart is MonoBehaviour))
		{
			m_injectionPart.Awake();
		}
	}

	public void Start()
	{
		if (!(m_injectionPart is MonoBehaviour))
		{
			m_injectionPart.Start();
		}
	}

	public void FixedUpdate()
	{
		if (!(m_injectionPart is MonoBehaviour))
		{
			m_injectionPart.FixedUpdate();
		}
	}

	public void Update()
	{
		if (!(m_injectionPart is MonoBehaviour))
		{
			m_injectionPart.Update();
		}
	}

	public override void PrePlaced()
	{
		base.PrePlaced();
		m_injectionPart.PrePlaced();
	}

	public override void EnsureRigidbody()
	{
		base.EnsureRigidbody();
		m_injectionPart.EnsureRigidbody();
	}

	public override void Initialize()
	{
		base.Initialize();
		m_injectionPart.Initialize();
	}

	public override void InitializeEngine()
	{
		base.InitializeEngine();
		m_injectionPart.InitializeEngine();
	}

	public override void PostInitialize()
	{
		base.PostInitialize();
		m_injectionPart.PostInitialize();
	}

	public override bool CanBeEnabled()
	{
		return m_injectionPart.CanBeEnabled();
	}

	public override bool CanEncloseParts()
	{
		return m_injectionPart.CanEncloseParts();
	}

	public override bool CanBeEnclosed()
	{
		return m_injectionPart.CanBeEnclosed();
	}

	public override bool HasOnOffToggle()
	{
		return m_injectionPart.HasOnOffToggle();
	}

	public override bool IsEnabled()
	{
		return m_injectionPart.IsEnabled();
	}

	public override void SetEnabled(bool enabled)
	{
		base.SetEnabled(enabled);
		m_injectionPart.SetEnabled(enabled);
	}

	public override Direction EffectDirection()
	{
		return (Direction)m_injectionPart.EffectDirection();
	}

	public override Joint CustomConnectToPart(BasePart part)
	{
		return m_injectionPart.CustomConnectToPart(part);
	}

	protected override void OnTouch()
	{
		base.OnTouch();
		m_injectionPart.ProcessTouch();
	}
}
