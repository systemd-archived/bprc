using System.Collections.Generic;
using UnityEngine;

public class WirePart : WirePartBase
{
	[SerializeField]
	private int m_autoRotationIndex;

	private GameObject m_sprite;

	private Wire m_wire;

	private static Dictionary<int, int> s_rotationMap;

	public override IEnumerable<CircuitElement> ElectricalElements => m_wire.ToEnumerable();

	static WirePart()
	{
		s_rotationMap = new Dictionary<int, int>
		{
			[CombineBits(0, 0, 0, 0)] = 10,
			[CombineBits(1, 0, 0, 0)] = 0,
			[CombineBits(0, 1, 0, 0)] = 1,
			[CombineBits(0, 0, 1, 0)] = 0,
			[CombineBits(0, 0, 0, 1)] = 1,
			[CombineBits(1, 0, 1, 0)] = 0,
			[CombineBits(0, 1, 0, 1)] = 1,
			[CombineBits(1, 0, 0, 1)] = 2,
			[CombineBits(1, 1, 0, 0)] = 3,
			[CombineBits(0, 1, 1, 0)] = 4,
			[CombineBits(0, 0, 1, 1)] = 5,
			[CombineBits(1, 1, 1, 0)] = 6,
			[CombineBits(0, 1, 1, 1)] = 7,
			[CombineBits(1, 0, 1, 1)] = 8,
			[CombineBits(1, 1, 0, 1)] = 9,
			[CombineBits(1, 1, 1, 1)] = 10
		};
	}

	private static int CombineBits(bool bit0, bool bit1, bool bit2, bool bit3)
	{
		return (bit0 ? 1 : 0) | (bit1 ? 2 : 0) | (bit2 ? 4 : 0) | (bit3 ? 8 : 0);
	}

	private static int CombineBits(byte bit0, byte bit1, byte bit2, byte bit3)
	{
		return bit0 | (bit1 << 1) | (bit2 << 2) | (bit3 << 3);
	}

	public override void Awake()
	{
		base.Awake();
		m_autoAlign = (AutoAlignType)(-1);
		m_sprite = base.transform.Find("WireSprite").gameObject;
	}

	public override void ChangeVisualConnections()
	{
		int autoRotationIndex = GetAutoRotationIndex();
		SetAutoRotationIndex(autoRotationIndex);
	}

	public override void SetRotation(GridRotation rotation)
	{
		ApplyBuildRotation(Quaternion.identity, false);
	}

	private int GetAutoRotationIndex()
	{
		ElectricalPart electricalPart = FindConnectedPart(1, 0, BitDirection.Right);
		ElectricalPart electricalPart2 = FindConnectedPart(0, 1, BitDirection.Up);
		ElectricalPart electricalPart3 = FindConnectedPart(-1, 0, BitDirection.Left);
		int key = CombineBits(bit3: FindConnectedPart(0, -1, BitDirection.Down) != null, bit0: electricalPart != null, bit1: electricalPart2 != null, bit2: electricalPart3 != null);
		return s_rotationMap[key];
	}

	private void SetAutoRotationIndex(int rotationIndex)
	{
		if (rotationIndex == m_autoRotationIndex)
		{
			return;
		}
		float angle = ((rotationIndex <= 1) ? ((float)rotationIndex * 90f) : ((rotationIndex <= 5) ? ((float)(rotationIndex - 2) * 90f) : ((rotationIndex > 9) ? 0f : ((float)(rotationIndex - 6) * 90f))));
		m_sprite.transform.localRotation = Quaternion.AngleAxis(angle, Vector3.forward);
		int num = customPartIndex;
		int num2 = ((m_autoRotationIndex > 1) ? ((m_autoRotationIndex <= 5) ? 1 : ((m_autoRotationIndex <= 9) ? 2 : 3)) : 0);
		int num3 = ((rotationIndex > 1) ? ((rotationIndex <= 5) ? 1 : ((rotationIndex <= 9) ? 2 : 3)) : 0);
		if (num2 != num3 || m_autoRotationIndex == -1)
		{
			string spriteName = "Wire" + (num + 1) + "_Sprite_" + (num3 + 1);
			INSerializedSprite component = m_sprite.GetComponent<INSerializedSprite>();
			component.SpriteName = spriteName;
			component.UpdateMesh();
			BoxCollider component2 = GetComponent<BoxCollider>();
			switch (num3)
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

	protected override BitDirection GetConnectionDirection()
	{
		return BitDirection.Any;
	}

	public override bool IsElectromagnetic()
	{
		return customPartIndex == 2;
	}

	public override void CreateElectricalElements()
	{
		int count = m_connections.Count;
		Wire wire = new Wire(count);
		wire.ElementUpdated += OnElementUpdated;
		m_wire = wire;
		m_electrodeMap = new Electrode[4];
		for (int i = 0; i < count; i++)
		{
			int num = m_connections[i].Direction.ToIndex();
			if (num != -1)
			{
				m_electrodeMap[num] = m_wire.Electrodes[i];
			}
		}
	}

	private void OnElementUpdated(CircuitSimulator simulator, SimulationResult result)
	{
		OnElementUpdatedBase(simulator, result);
		float brightness = GetBrightness((float)result.U, result.IsGrounded);
		m_sprite.GetComponent<MeshRenderer>().material.color = new Color(brightness, brightness, brightness, 1f);
	}
}
