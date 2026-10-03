using System;
using System.Collections.Generic;
using UnityEngine;

public class PowerTransmitterPart : ElectricalPart
{
	public enum PowerTransmitterType
	{
		Sender = 0,
		Receiver = 1
	}

	private CircuitElement m_element;

	private PowerTransmitterPart m_connectedPart;

	private Electrode m_electrode;

	private Transform m_visual;

	private bool m_visualSeparated;

	// ===== 斜向发送器专属无线链路（全新机制，与原有正向发送器/接收器/斜向接收器互不影响） =====
	// 一个斜向发送器可与范围内所有同频道的斜向发送器同时互连（无就近原则）
	private readonly List<Electrode> m_diagonalElectrodes = new List<Electrode>();

	private readonly List<PowerTransmitterPart> m_diagonalPartners = new List<PowerTransmitterPart>();

	internal int no90 => (m_gridRotation >= GridRotation.Deg_45) ? 1 : 0;

	// 斜向(45°/135°/225°/315°)的无线电发送器(Sender, 索引40)
	public bool IsDiagonalSender => IsSender && no90 == 1;

	// 该部件所包围染色框的标签(频道号)，未包围染色框时返回 -1
	public int ChannelLabel => GetChannel();

	public bool HasDiagonalSenderLink => m_diagonalPartners.Count > 0;

	public IReadOnlyList<PowerTransmitterPart> DiagonalSenderPartners => m_diagonalPartners;

	// 本发送器的“电位参考电极”(元素第 0 个电极，即向下的有线连接点)；用于计算等电位分组
	public Electrode DiagonalNodeElectrode => (m_element != null && m_element.Electrodes.Count > 0) ? m_element.Electrodes[0] : null;

	public bool IsSender => TransmitterType == PowerTransmitterType.Sender;

	public bool IsReceiver => TransmitterType == PowerTransmitterType.Receiver;

	public PowerTransmitterType TransmitterType => (PowerTransmitterType)(customPartIndex - 40);

	public override IEnumerable<CircuitElement> ElectricalElements => m_element.ToEnumerable();

	public override void Awake()
	{
		base.Awake();
	}

	public override void SetRotation(GridRotation rotation)
	{
		base.SetRotation(rotation);
		EnsureVisualSeparated();
		ApplyOrientation();
	}

	private void EnsureVisualSeparated()
	{
		if (m_visualSeparated)
		{
			return;
		}
		MeshFilter meshFilter = GetComponent<MeshFilter>();
		MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
		INSerializedSprite sprite = GetComponent<INSerializedSprite>();
		if (meshFilter == null || meshRenderer == null)
		{
			return;
		}
		// 确保 mesh 已生成再分离，避免空 mesh 导致建造界面隐身
		if (meshFilter.sharedMesh == null)
		{
			return;
		}
		m_visualSeparated = true;
		GameObject visualObject = new GameObject("Visual");
		visualObject.transform.SetParent(base.transform, worldPositionStays: false);
		MeshFilter visualMeshFilter = visualObject.AddComponent<MeshFilter>();
		visualMeshFilter.sharedMesh = meshFilter.sharedMesh;
		MeshRenderer visualMeshRenderer = visualObject.AddComponent<MeshRenderer>();
		visualMeshRenderer.sharedMaterials = meshRenderer.sharedMaterials;
		if (sprite != null)
		{
			INSerializedSprite visualSprite = visualObject.AddComponent<INSerializedSprite>();
			visualSprite.SpriteName = sprite.SpriteName;
			try
			{
				visualSprite.UpdateMesh();
			}
			catch
			{
			}
		}
		DestroyComponent(meshFilter);
		DestroyComponent(meshRenderer);
		if (sprite != null)
		{
			DestroyComponent(sprite);
		}
		m_visual = visualObject.transform;
	}

	private void DestroyComponent(Component component)
	{
		if (Application.isPlaying)
		{
			Destroy(component);
		}
		else
		{
			DestroyImmediate(component);
		}
	}

	private void ApplyOrientation()
	{
		if (no90 == 1)
		{
			GridRotation ortho = (GridRotation)(((int)m_gridRotation - (int)GridRotation.Deg_45) % 4);
			ApplyBuildRotation(Quaternion.AngleAxis(GetRotationAngle(ortho), Vector3.forward), false);
			if ((bool)m_visual)
			{
				m_visual.localRotation = Quaternion.AngleAxis(45f, Vector3.forward);
			}
		}
		else
		{
			ApplyBuildRotation(Quaternion.AngleAxis(GetRotationAngle(m_gridRotation), Vector3.forward), false);
			if ((bool)m_visual)
			{
				m_visual.localRotation = Quaternion.identity;
			}
		}
	}

	public int GetChannel()
	{
		if (m_enclosedInto != null && m_enclosedInto.IsColoredrame())
		{
			return m_enclosedInto.Index;
		}
		return -1;
	}

	public override void CreateElectricalElements()
	{
		switch (TransmitterType)
		{
		case PowerTransmitterType.Sender:
			m_element = new Wire(1);
			break;
		case PowerTransmitterType.Receiver:
			m_element = new Resistor(0.0);
			break;
		}
	}

	protected override BitDirection GetConnectionDirection()
	{
		return TransmitterType switch
		{
			PowerTransmitterType.Sender => BitDirection.Down.Rotate((int)m_gridRotation), 
			PowerTransmitterType.Receiver => BitDirection.Up.Rotate((int)m_gridRotation), 
			_ => BitDirection.None, 
		};
	}

	protected override Electrode FindElectrode(BitDirection direction)
	{
		direction = direction.Rotate(0 - m_gridRotation);
		if (direction == BitDirection.Down && TransmitterType == PowerTransmitterType.Sender)
		{
			return m_element.Electrodes[0];
		}
		if (direction == BitDirection.Up && TransmitterType == PowerTransmitterType.Receiver)
		{
			return m_element.Electrodes[0];
		}
		return null;
	}

	public void Connect(PowerTransmitterPart other, float distance)
	{
		if (m_connectedPart != other)
		{
			Disconnect();
			Electrode electrode = new Electrode(m_element, null);
			Electrode electrode2 = new Electrode(other.m_element, null);
			CircuitFactory.Connect(electrode, electrode2);
			electrode.Element.Electrodes.Add(electrode);
			electrode2.Element.Electrodes.Add(electrode2);
			m_electrode = electrode;
			m_connectedPart = other;
		}
		if (no90 == 1)
		{
			((Resistor)m_element).Resistance = 0.0;
		}
		if (no90 == 0)
		{
			Resistor resistor = (Resistor)m_element;
			distance = Math.Max(distance - 1f, 0f);
			if (distance <= 32f)
			{
				resistor.Resistance = 0.1f * distance * distance + 0.01f;
			}
			else
			{
				Disconnect();
			}
		}
	}

	public void Disconnect()
	{
		if (m_connectedPart != null)
		{
			Electrode electrode = m_electrode;
			Electrode connectedElectrode = m_electrode.ConnectedElectrode;
			CircuitFactory.Disconnect(electrode, connectedElectrode);
			electrode.Element.Electrodes.Remove(electrode);
			connectedElectrode.Element.Electrodes.Remove(connectedElectrode);
			m_electrode = null;
			m_connectedPart = null;
		}
		((Resistor)m_element).Resistance = 0.0;
	}

	// 斜向发送器之间的直接无线通路：把两个发送器的原无线元件电极直接连通，电阻为 0（元件本身即 Wire）
	// 支持多条链路：同一发送器可与范围内所有同频道斜向发送器同时相连
	public void ConnectDiagonalSender(PowerTransmitterPart other)
	{
		if (other == null || other == this || m_element == null || other.m_element == null)
		{
			return;
		}
		if (m_diagonalPartners.Contains(other) || other.m_diagonalPartners.Contains(this))
		{
			return;
		}
		Electrode mine = new Electrode(m_element, null);
		Electrode theirs = new Electrode(other.m_element, null);
		mine.IsWirelessLink = true;
		theirs.IsWirelessLink = true;
		CircuitFactory.Connect(mine, theirs);
		m_element.Electrodes.Add(mine);
		other.m_element.Electrodes.Add(theirs);
		m_diagonalElectrodes.Add(mine);
		m_diagonalPartners.Add(other);
		other.m_diagonalElectrodes.Add(theirs);
		other.m_diagonalPartners.Add(this);
	}

	public void DisconnectDiagonalSender(PowerTransmitterPart other)
	{
		if (other == null)
		{
			return;
		}
		int index = m_diagonalPartners.IndexOf(other);
		if (index < 0)
		{
			return;
		}
		Electrode mine = m_diagonalElectrodes[index];
		m_diagonalPartners.RemoveAt(index);
		m_diagonalElectrodes.RemoveAt(index);
		int otherIndex = other.m_diagonalPartners.IndexOf(this);
		if (otherIndex >= 0)
		{
			other.m_diagonalPartners.RemoveAt(otherIndex);
			other.m_diagonalElectrodes.RemoveAt(otherIndex);
		}
		ReleaseDiagonalElectrode(mine);
	}

	public void DisconnectAllDiagonalSenders()
	{
		while (m_diagonalPartners.Count > 0)
		{
			DisconnectDiagonalSender(m_diagonalPartners[m_diagonalPartners.Count - 1]);
		}
	}

	private static void ReleaseDiagonalElectrode(Electrode electrode)
	{
		if (electrode == null)
		{
			return;
		}
		Electrode connected = electrode.ConnectedElectrode;
		if (connected != null)
		{
			CircuitFactory.Disconnect(electrode, connected);
			if (connected.Element != null)
			{
				connected.Element.Electrodes.Remove(connected);
			}
		}
		if (electrode.Element != null)
		{
			electrode.Element.Electrodes.Remove(electrode);
		}
	}
}
