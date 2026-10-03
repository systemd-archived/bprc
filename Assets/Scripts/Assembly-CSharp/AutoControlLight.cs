using UnityEngine;

public class AutoControlLight : PointLight
{
	private struct AreaRect
	{
		public float XMin;

		public float XMax;

		public float YMin;

		public float YMax;

		public AreaRect(float xMin, float xMax, float yMin, float yMax)
		{
			XMin = xMin;
			XMax = xMax;
			YMin = yMin;
			YMax = yMax;
		}

		public bool Contains(float x, float y)
		{
			if (XMin <= x && x < XMax && YMin <= y)
			{
				return y < YMax;
			}
			return false;
		}
	}

	public GameObject m_activeFullSprite;

	public GameObject m_inactiveFullSprite;

	private bool m_detected;

	private bool m_collided;

	private float m_delay;

	private float m_maxDelay;

	private AreaRect m_detectArea;

	private AreaRect m_collideArea;

	private Collider m_collider;

	public int CurrentType
	{
		get
		{
			if (m_gridRotation >= GridRotation.Deg_45)
			{
				return 1;
			}
			return 0;
		}
	}

	public override void Awake()
	{
		base.Awake();
		if (CurrentType == 0)
		{
			inactiveSprite.SetActive(value: true);
			m_inactiveFullSprite.SetActive(value: false);
		}
		else
		{
			inactiveSprite.SetActive(value: false);
			m_inactiveFullSprite.SetActive(value: true);
		}
	}

	public override void PrePlaced()
	{
		m_autoAlign = (AutoAlignType)(-1);
	}

	public override void Initialize()
	{
		m_collider = GetComponent<Collider>();
		m_detectArea = ((CurrentType == 0) ? new AreaRect(-2f, 6.5f, -2.5f, 2.5f) : new AreaRect(0f, 0f, 0f, 0f));
		m_collideArea = new AreaRect(-2.5f, 2.5f, -2.5f, 2.5f);
		if (m_enclosedInto != null && m_enclosedInto.IsAlienMetalFrame())
		{
			m_maxDelay = 0f;
		}
		else
		{
			m_maxDelay = 0.1f;
		}
	}

	public override void SetRotation(GridRotation rotation)
	{
		SetRotation((int)rotation);
	}

	public override void SetRotation(int rotation)
	{
		int num = (int)(m_gridRotation = (GridRotation)(rotation % 5));
		if (CurrentType == 0)
		{
			inactiveSprite.SetActive(value: true);
			m_inactiveFullSprite.SetActive(value: false);
		ApplyBuildRotation(Quaternion.AngleAxis(90f * (float)num, Vector3.forward), false);
		}
		else
		{
			inactiveSprite.SetActive(value: false);
			m_inactiveFullSprite.SetActive(value: true);
		ApplyBuildRotation(Quaternion.AngleAxis(90f * (float)(num - 4), Vector3.forward), false);
		}
	}

	protected override void OnTouch()
	{
		if (!activated || !m_collided)
		{
			activated = !activated;
			if (CurrentType == 0)
			{
				activeSprite.SetActive(activated);
				inactiveSprite.SetActive(!activated);
				m_activeFullSprite.SetActive(value: false);
				m_inactiveFullSprite.SetActive(value: false);
			}
			else
			{
				activeSprite.SetActive(value: false);
				inactiveSprite.SetActive(value: false);
				m_activeFullSprite.SetActive(activated);
				m_inactiveFullSprite.SetActive(!activated);
			}
			if ((bool)lightSource)
			{
				lightSource.isEnabled = activated;
			}
			if (!activated)
			{
				SetDetected(detected: false, force: true);
			}
		}
	}

	public bool IsDetected()
	{
		return m_detected;
	}

	public bool IsCollided()
	{
		return m_collided;
	}

	public void SetDetected(bool detected, bool force)
	{
		if (!detected && m_detected)
		{
			m_delay += Time.deltaTime;
			if (force || m_delay >= m_maxDelay)
			{
				m_delay = 0f;
				m_detected = false;
				m_collider.enabled = true;
			}
		}
		else if (detected && !m_detected)
		{
			m_delay += Time.deltaTime;
			if (force || m_delay >= m_maxDelay)
			{
				m_delay = 0f;
				m_detected = true;
				m_collider.enabled = false;
			}
		}
	}

	public void SetCollided(bool collided)
	{
		m_collided = collided;
	}

	public bool IsInDetectArea(float x, float y)
	{
		return m_detectArea.Contains(x, y);
	}

	public bool IsInCollideArea(float x, float y)
	{
		return m_collideArea.Contains(x, y);
	}
}
