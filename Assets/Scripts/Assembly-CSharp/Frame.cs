using Innovation;
using UnityEngine;

public class Frame : BasePart, IFramePart, IBasePart
{
	public enum FrameMaterial
	{
		Wood = 0,
		Metal = 1
	}

	public FrameMaterial m_material;

	public Texture2D[] m_brokenTextures;

	private bool m_colored;

	private MeshRenderer[] m_renderers;

	public Color Color
	{
		get
		{
			return ((ColoredFrame)this).Color;
		}
		set
		{
			((ColoredFrame)this).SetColorAndUpdateRenderers(value);
		}
	}

	public override bool CanEncloseParts()
	{
		return true;
	}

	public override bool IsPartOfChassis()
	{
		return true;
	}

	public override void Initialize()
	{
		if ((bool)m_enclosedPart && !(m_enclosedPart is Rope) && !(m_enclosedPart is HingePlate))
		{
			FixedJoint fixedJoint = m_enclosedPart.gameObject.AddComponent<FixedJoint>();
			fixedJoint.connectedBody = base.rigidbody;
			float breakForce = base.contraption.GetJointConnectionStrength(GetJointConnectionStrength()) + base.contraption.GetJointConnectionStrength(m_enclosedPart.GetJointConnectionStrength());
			// 军事版功能：轻质框体对内部零件的固定关节强化 15 倍（INMilitaryFeatures.Enabled 关闭时不生效）
			if (INMilitaryFeatures.Enabled && this.IsLightFrame())
			{
				breakForce *= 15f;
			}
			fixedJoint.breakForce = breakForce;
			fixedJoint.enablePreprocessing = false;
			base.contraption.AddJointToMap(this, m_enclosedPart, fixedJoint);
			IgnoreCollisionRecursive(base.collider, m_enclosedPart.gameObject);
		}
	}

	private void IgnoreCollisionRecursive(Collider collider, GameObject part)
	{
		if (part.activeInHierarchy && (bool)part.GetComponent<Collider>())
		{
			GamePhysics.IgnoreCollision(collider, part.GetComponent<Collider>());
		}
		for (int i = 0; i < part.transform.childCount; i++)
		{
			IgnoreCollisionRecursive(collider, part.transform.GetChild(i).gameObject);
		}
	}

	public override void OnBreak()
	{
	}

	public bool IsColoredFrame()
	{
		return this.IsColoredrame();
	}

	private new void Awake()
	{
		base.Awake();
		m_renderers = GetComponentsInChildren<MeshRenderer>();
	}

	private void FixedUpdate()
	{
		if (base.contraption == null || !INSettings.GetBool(INFeature.ColoredFrame) || ((!INSettings.GetBool(INFeature.CanColorSpecialFrames) || (!this.IsAlienMetalFrame() && !this.IsLightFrame())) && !INSettings.GetBool(INFeature.CanColorAllFrames)))
		{
			return;
		}
		Color clear = Color.clear;
		float num = 0f;
		foreach (BasePart item in base.contraption.FindNeighboursYield(m_coordX, m_coordY, this))
		{
			if (item is ColoredFrame coloredFrame)
			{
				float a = coloredFrame.Color.a;
				clear += coloredFrame.Color * a;
				num += a;
			}
		}
		if (num > 0f)
		{
			clear /= num;
			MeshRenderer[] renderers = m_renderers;
			foreach (MeshRenderer meshRenderer in renderers)
			{
				if (!m_colored)
				{
					meshRenderer.material.shader = INUnity.LoadShader("Unlit_ColorTransparent_GrayOverlay");
				}
				meshRenderer.material.color = clear;
			}
			m_colored = true;
		}
		else if (m_colored)
		{
			MeshRenderer[] renderers = m_renderers;
			foreach (MeshRenderer obj in renderers)
			{
				obj.material.shader = INUnity.CustomTransparentShader;
				obj.material.color = Color.white;
			}
			m_colored = false;
		}
	}
}
