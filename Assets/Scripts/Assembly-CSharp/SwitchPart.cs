using System.Collections.Generic;
using UnityEngine;

public class SwitchPart : ElectricalPart
{
	private GameObject m_inactiveSprite;

	private GameObject m_activeSprite;

	private Switch m_switch;

	private bool m_closed;

	public override IEnumerable<CircuitElement> ElectricalElements => m_switch.ToEnumerable();

	public override void Awake()
	{
		base.Awake();
		m_inactiveSprite = base.transform.Find("InactiveSprite").gameObject;
		m_activeSprite = base.transform.Find("ActiveSprite").gameObject;
	}

	public override bool IsEnabled()
	{
		return m_closed;
	}

	public override bool IsTriggerable()
	{
		return !base.HasGeneratorRef;
	}

	public override IEnumerable<UIPartTriggerButtonInfo> GetTriggerButtonInfo()
	{
		yield return new UIPartTriggerButtonInfo(UIPartButtonType.Trigger, 0, base.Type, 0, base.ConnectedComponent);
	}

	protected override void OnTouch()
	{
		m_closed = !m_closed;
		m_activeSprite.SetActive(m_closed);
		m_inactiveSprite.SetActive(!m_closed);
		m_switch.Toggle(m_closed);
	}

	// Multiplayer：副本（kinematic）零件的开关状态由主机姿态流回放，刻意不动电路
	public override void SetEnabled(bool enabled)
	{
		if (IsMultiplayerReplica() && m_closed != enabled)
		{
			m_closed = enabled;
			m_activeSprite.SetActive(m_closed);
			m_inactiveSprite.SetActive(!m_closed);
		}
	}

	public override void CreateElectricalElements()
	{
		m_switch = new Switch();
	}

	protected override BitDirection GetConnectionDirection()
	{
		return BitDirection.LeftAndRight.Rotate((int)m_gridRotation);
	}

	protected override Electrode FindElectrode(BitDirection direction)
	{
		direction = direction.Rotate(0 - m_gridRotation);
		return direction switch
		{
			BitDirection.Left => m_switch.Pole, 
			BitDirection.Right => m_switch.Throw, 
			_ => null, 
		};
	}
}
