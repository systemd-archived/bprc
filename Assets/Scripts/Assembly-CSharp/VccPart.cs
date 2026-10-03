using System.Collections.Generic;
using UnityEngine;

public class VccPart : ElectricalPart
{
	private GameObject m_inactiveSprite;

	private GameObject m_activeSprite;

	private Vcc m_vcc;

	private bool m_enabled;

	public override IEnumerable<CircuitElement> ElectricalElements => m_vcc.ToEnumerable();

	public override void Awake()
	{
		base.Awake();
		m_inactiveSprite = base.transform.Find("InactiveSprite").gameObject;
		m_activeSprite = base.transform.Find("ActiveSprite").gameObject;
	}

	public override bool IsEnabled()
	{
		return m_enabled;
	}

	public override bool IsTriggerable()
	{
		return !base.HasGeneratorRef;
	}

	public override IEnumerable<UIPartTriggerButtonInfo> GetTriggerButtonInfo()
	{
		yield return new UIPartTriggerButtonInfo(UIPartButtonType.Trigger, 0, base.Type, 3, base.ConnectedComponent);
	}

	protected override void OnTouch()
	{
		m_enabled = !m_enabled;
		m_activeSprite.SetActive(m_enabled);
		m_inactiveSprite.SetActive(!m_enabled);
		m_vcc.Potential = (m_enabled ? 5.0 : 0.0);
		m_vcc.Resistance = (m_enabled ? 0.05 : 0.0);
	}

	// Multiplayer：副本（kinematic）零件的开关状态由主机姿态流回放，不同步电路仿真
	public override void SetEnabled(bool enabled)
	{
		if (IsMultiplayerReplica() && m_enabled != enabled)
		{
			m_enabled = enabled;
			m_activeSprite.SetActive(m_enabled);
			m_inactiveSprite.SetActive(!m_enabled);
		}
	}

	public override void CreateElectricalElements()
	{
		m_vcc = new Vcc(0.0);
	}

	protected override BitDirection GetConnectionDirection()
	{
		return BitDirection.Right.Rotate((int)m_gridRotation);
	}

	protected override Electrode FindElectrode(BitDirection direction)
	{
		direction = direction.Rotate(0 - m_gridRotation);
		if (direction == BitDirection.Right)
		{
			return m_vcc.Electrode;
		}
		return null;
	}
}
