using System;
using System.Collections.Generic;
using UnityEngine;

public class FuelTube : BasePart
{
	public enum TubeType
	{
		Common = 0,
		Auto = 1,
		Valve = 2
	}

	[SerializeField]
	private int m_autoRotationIndex;

	private bool m_enabled;

	private List<BasePart> m_connectedParts;

	private static Dictionary<(byte, byte, byte, byte), int> s_rotationMap;

	public TubeType CurrentTubeType => customPartIndex switch
	{
		2 => TubeType.Common, 
		3 => TubeType.Auto, 
		5 => TubeType.Valve, 
		_ => throw new InvalidOperationException(), 
	};

	static FuelTube()
	{
		s_rotationMap = new Dictionary<(byte, byte, byte, byte), int>
		{
			[(0, 0, 0, 0)] = 10,
			[(1, 0, 0, 0)] = 10,
			[(0, 1, 0, 0)] = 10,
			[(0, 0, 1, 0)] = 10,
			[(0, 0, 0, 1)] = 10,
			[(1, 0, 1, 0)] = 10,
			[(0, 1, 0, 1)] = 10,
			[(1, 0, 0, 1)] = 2,
			[(1, 1, 0, 0)] = 3,
			[(0, 1, 1, 0)] = 4,
			[(0, 0, 1, 1)] = 5,
			[(1, 1, 1, 0)] = 6,
			[(0, 1, 1, 1)] = 7,
			[(1, 0, 1, 1)] = 8,
			[(1, 1, 0, 1)] = 9,
			[(1, 1, 1, 1)] = 10
		};
	}

	public override void Awake()
	{
		base.Awake();
		if (CurrentTubeType != TubeType.Auto)
		{
			m_autoAlign = (AutoAlignType)(-1);
			return;
		}
		m_autoAlign = AutoAlignType.None;
		m_autoRotationIndex = -1;
	}

	public override bool IsEnabled()
	{
		return m_enabled;
	}

	public override bool IsTriggerable()
	{
		if (!base.HasGeneratorRef)
		{
			return CurrentTubeType == TubeType.Valve;
		}
		return false;
	}

	public override IEnumerable<UIPartTriggerButtonInfo> GetTriggerButtonInfo()
	{
		yield return new UIPartTriggerButtonInfo(UIPartButtonType.Trigger, 4, base.Type, 0, base.ConnectedComponent, consistent: true);
	}

	public IEnumerable<BasePart> GetConnectedParts()
	{
		if (CurrentTubeType == TubeType.Valve && !m_enabled)
		{
			yield break;
		}
		foreach (BasePart connectedPart in m_connectedParts)
		{
			if (!(connectedPart == null) && connectedPart.ConnectedComponent == base.ConnectedComponent && (!(connectedPart is FuelTube { CurrentTubeType: TubeType.Valve } fuelTube) || fuelTube.IsEnabled()))
			{
				yield return connectedPart;
			}
		}
	}

	public override void ChangeVisualConnections()
	{
		if (CurrentTubeType == TubeType.Auto)
		{
			int autoRotationIndex = GetAutoRotationIndex();
			SetAutoRotationIndex(autoRotationIndex);
		}
	}

	protected override void OnTouch()
	{
		if (CurrentTubeType == TubeType.Valve)
		{
			m_enabled = !m_enabled;
			FuelSystem.Instance.NeedsUpdate = true;
		}
	}

	public override void SetRotation(GridRotation rotation)
	{
		if (CurrentTubeType != TubeType.Auto)
		{
			SetRotation((int)rotation);
		}
		else if (m_autoRotationIndex >= 2)
		{
			SetAutoRotationIndex(m_autoRotationIndex);
		}
	}

	public override void SetRotation(int rotation)
	{
		int num = rotation % 2;
		float angle = ((num == 0) ? 0f : 90f);
		m_gridRotation = (GridRotation)num;
		ApplyBuildRotation(Quaternion.AngleAxis(angle, Vector3.forward), false);
	}

	private int GetAutoRotationIndex()
	{
		int coordX = m_coordX;
		int coordY = m_coordY;
		BasePart part = base.contraption.FindPartAt(coordX + 1, coordY, this);
		BasePart part2 = base.contraption.FindPartAt(coordX, coordY + 1, this);
		BasePart part3 = base.contraption.FindPartAt(coordX - 1, coordY, this);
		BasePart part4 = base.contraption.FindPartAt(coordX, coordY - 1, this);
		bool value = CheckPart(ref part);
		bool value2 = CheckPart(ref part2);
		bool value3 = CheckPart(ref part3);
		bool value4 = CheckPart(ref part4);
		return s_rotationMap[(Convert.ToByte(value), Convert.ToByte(value2), Convert.ToByte(value3), Convert.ToByte(value4))];
	}

	private void SetAutoRotationIndex(int rotationIndex)
	{
		float angle = ((rotationIndex <= 5) ? ((float)(rotationIndex - 2) * 90f) : ((rotationIndex > 9) ? 0f : ((float)(rotationIndex - 6) * 90f)));
		ApplyBuildRotation(Quaternion.AngleAxis(angle, Vector3.forward), false);
		if (rotationIndex == m_autoRotationIndex)
		{
			return;
		}
		int num = ((m_autoRotationIndex <= 5) ? 1 : ((m_autoRotationIndex <= 9) ? 2 : 3));
		int num2 = ((rotationIndex <= 5) ? 1 : ((rotationIndex <= 9) ? 2 : 3));
		if (num != num2 || m_autoRotationIndex == -1)
		{
			string spriteName = "FuelTube_Sprite_" + (num2 + 1);
			INSerializedSprite component = GetComponent<INSerializedSprite>();
			component.SpriteName = spriteName;
			component.UpdateMesh();
			BoxCollider component2 = GetComponent<BoxCollider>();
			switch (num2)
			{
			case 1:
				component2.center = new Vector3(0.15f, -0.15f, 0f);
				component2.size = new Vector3(0.7f, 0.7f, 1f);
				break;
			case 2:
				component2.center = new Vector3(0f, 0.1f, 0f);
				component2.size = new Vector3(1f, 0.8f, 1f);
				break;
			case 3:
				component2.center = new Vector3(0f, 0f, 0f);
				component2.size = new Vector3(1f, 1f, 1f);
				break;
			}
		}
		m_autoRotationIndex = rotationIndex;
	}

	public override void CreateCustomJoints()
	{
		FindConnectedParts();
		if (CurrentTubeType != TubeType.Auto)
		{
			return;
		}
		foreach (BasePart connectedPart in m_connectedParts)
		{
			base.contraption.AddFixedJoint(this, connectedPart);
			if (!(connectedPart is FuelTube { CurrentTubeType: TubeType.Auto }))
			{
				base.contraption.AddFixedJoint(connectedPart, this);
			}
		}
		// Auto pipes opt out of the generic grid joint pass. Their fuel-only list
		// excludes native motor wheels and cannons, but those still need a structural mount.
		for (int side = 0; side < 4; side++)
		{
			Direction direction = (Direction)side;
			BasePart attachment = contraption.GetPartAt(this, direction);
			if (!(attachment is OffRoadWheel) && !(attachment is ExplodingGrapplingHook) && !(attachment is Spring)) continue;
			if (!attachment.CanConnectTo(InverseDirection(direction))) continue;
			attachment.EnsureRigidbody();
			BitDirection custom = attachment.GetCustomJointConnectionDirection().ToBitDirection();
			if ((custom & (BitDirection)(1 << (int)InverseDirection(direction))) != BitDirection.None)
			{
				Joint mount = attachment.CustomConnectToPart(this);
				if (mount != null) contraption.AddJointToMap(attachment, this, mount);
			}
			else contraption.AddFixedJoint(this, attachment);
		}
	}

	private void FindConnectedParts()
	{
		int coordX = m_coordX;
		int coordY = m_coordY;
		BasePart part = base.contraption.FindPartAt(coordX + 1, coordY, this);
		BasePart part2 = base.contraption.FindPartAt(coordX, coordY + 1, this);
		BasePart part3 = base.contraption.FindPartAt(coordX - 1, coordY, this);
		BasePart part4 = base.contraption.FindPartAt(coordX, coordY - 1, this);
		bool flag = CheckPart(ref part);
		bool flag2 = CheckPart(ref part2);
		bool flag3 = CheckPart(ref part3);
		bool flag4 = CheckPart(ref part4);
		m_connectedParts = new List<BasePart>();
		if (CurrentTubeType != TubeType.Auto)
		{
			switch (GetRotation())
			{
			case 0:
				if (flag)
				{
					m_connectedParts.Add(part);
				}
				if (flag3)
				{
					m_connectedParts.Add(part3);
				}
				break;
			case 1:
				if (flag2)
				{
					m_connectedParts.Add(part2);
				}
				if (flag4)
				{
					m_connectedParts.Add(part4);
				}
				break;
			}
		}
		else
		{
			if (flag)
			{
				m_connectedParts.Add(part);
			}
			if (flag3)
			{
				m_connectedParts.Add(part3);
			}
			if (flag2)
			{
				m_connectedParts.Add(part2);
			}
			if (flag4)
			{
				m_connectedParts.Add(part4);
			}
		}
	}

	private bool CheckPart(ref BasePart part)
	{
		if (part == null)
		{
			return false;
		}
		if (part.m_enclosedPart != null)
		{
			part = part.m_enclosedPart;
		}
		if (part is FuelTube fuelTube)
		{
			if (CurrentTubeType == TubeType.Auto && fuelTube.CurrentTubeType != TubeType.Auto)
			{
				int num = part.m_coordX - m_coordX;
				int num2 = part.m_coordY - m_coordY;
				int rotation = fuelTube.GetRotation();
				if (num2 != 0 || rotation != 0)
				{
					if (num == 0)
					{
						return rotation == 1;
					}
					return false;
				}
				return true;
			}
			return true;
		}
		if (!(part is IFuelConsumer))
		{
			return part is IFuelContainer;
		}
		return true;
	}
}
