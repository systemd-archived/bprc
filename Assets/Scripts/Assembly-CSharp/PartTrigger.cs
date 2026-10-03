using System.Collections.Generic;
using UnityEngine;

public class PartTrigger : ElectricalPart
{
	private Wire m_wire;

	private LogicLevel m_level;

	private LogicLevel m_newLevel;

	private List<BasePart> m_connectedParts;

	private Transform m_visual;

	private bool m_visualSeparated;

	public bool IsMultiple => customPartIndex == 48;

	public override IEnumerable<CircuitElement> ElectricalElements => m_wire.ToEnumerable();

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
		Transform visualization = m_visual;
		if (m_gridRotation >= GridRotation.Deg_45)
		{
			GridRotation ortho = (GridRotation)(((int)m_gridRotation - (int)GridRotation.Deg_45) % 4);
			ApplyBuildRotation(Quaternion.AngleAxis(GetRotationAngle(ortho), Vector3.forward), false);
			if ((bool)visualization)
			{
				visualization.localRotation = Quaternion.AngleAxis(45f, Vector3.forward);
			}
		}
		else
		{
			ApplyBuildRotation(Quaternion.AngleAxis(GetRotationAngle(m_gridRotation), Vector3.forward), false);
			if ((bool)visualization)
			{
				visualization.localRotation = Quaternion.identity;
			}
		}
	}

	public override void CreateElectricalElements()
	{
		m_wire = new Wire(1);
		m_wire.ElementUpdated += OnElementUpdated;
	}

	protected override BitDirection GetConnectionDirection()
	{
		return BitDirection.Left.Rotate((int)m_gridRotation);
	}

	protected override Electrode FindElectrode(BitDirection direction)
	{
		direction = direction.Rotate(0 - m_gridRotation);
		if (direction == BitDirection.Left)
		{
			return m_wire.Electrodes[0];
		}
		return null;
	}

	public override void InitializeElectricalElements()
	{
		m_level = LogicLevel.Invalid;
		m_connectedParts = new List<BasePart>();
		(int, int) tuple = m_gridRotation.ToDirection();
		int item = tuple.Item1;
		int item2 = tuple.Item2;
		int num = ((!IsMultiple) ? 1 : 3);
		for (int i = 1; i <= num; i++)
		{
			BasePart basePart = base.contraption.FindPartAt(m_coordX + i * item, m_coordY + i * item2, this);
			if (basePart != null && basePart.ConnectedComponent == base.ConnectedComponent)
			{
				basePart = ((basePart.m_enclosedPart != null) ? basePart.m_enclosedPart : basePart);
				m_connectedParts.Add(basePart);
			}
		}
	}

	private void OnElementUpdated(CircuitSimulator simulator, SimulationResult result)
	{
		if (result.IsGrounded)
		{
			m_newLevel = ElectricalPart.GetLogicLevel(result.U);
		}
	}

	public override void PreUpdateElements()
	{
		m_newLevel = LogicLevel.Invalid;
	}

	public override void PostUpdateElements()
	{
		SetInvalid(m_newLevel == LogicLevel.Invalid);
		if (m_level != LogicLevel.Invalid && m_newLevel != LogicLevel.Invalid && m_level != m_newLevel)
		{
			foreach (BasePart connectedPart in m_connectedParts)
			{
				if (connectedPart != null && connectedPart.ConnectedComponent == base.ConnectedComponent)
				{
					connectedPart.ProcessTouch();
				}
			}
		}
		m_level = m_newLevel;
	}
}
