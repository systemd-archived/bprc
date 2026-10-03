using System;
using UnityEngine;

public class ColoredFrame : Frame
{
	[SerializeField]
	private bool m_initialized;

	[SerializeField]
	private Color m_color;

	[SerializeField]
	private Color m_transparentColor;

	private float m_alpha;

	private float m_foregroundAlpha;

	private float m_backgroundAlpha;

	private BasePart m_coloredPart;

	private MeshRenderer m_renderer;

	private MeshRenderer m_foregroundRenderer;

	private MeshRenderer m_backgroundRenderer;

	private (MeshRenderer, Material)[] m_coloredPartMaterials;

	public new Color Color
	{
		get
		{
			return m_color;
		}
		set
		{
			m_color = value;
		}
	}

	public Color TransparentColor
	{
		get
		{
			return m_transparentColor;
		}
		set
		{
			m_transparentColor = value;
		}
	}

	public override void Awake()
	{
		base.Awake();
		m_renderer = GetComponent<MeshRenderer>();
		m_foregroundRenderer = base.transform.Find("Foreground").GetComponent<MeshRenderer>();
		m_backgroundRenderer = base.transform.Find("Background").GetComponent<MeshRenderer>();
		m_alpha = ((customPartIndex != 133) ? INSettings.GetFloat(INFeature.ColoredFrameAlpha) : 0f);
		m_foregroundAlpha = INSettings.GetFloat(INFeature.ColoredFrameForegroundAlpha);
		m_backgroundAlpha = INSettings.GetFloat(INFeature.ColoredFrameBackgroundAlpha);
	}

	private void Start()
	{
		if (!m_initialized)
		{
			m_initialized = true;
			if (this.IsTransparentFrame())
			{
				float a = INSettings.GetFloat(INFeature.TransparentFrameAlpha);
				m_color.a = a;
				m_transparentColor = m_color;
			}
			UpdateRenderers();
		}
	}

	public void InitializeColor()
	{
		float num = ((customPartIndex != 133) ? INSettings.GetFloat(INFeature.ColoredFrameAlpha) : 0f);
		float num2 = INSettings.GetFloat(INFeature.ColoredFrameForegroundAlpha);
		float num3 = INSettings.GetFloat(INFeature.ColoredFrameBackgroundAlpha);
		if (this.IsTransparentFrame())
		{
			num *= 0.5f;
			num2 *= 0.5f;
			num3 *= 0.5f;
		}
		Color color = (m_color = GetColor(customPartIndex));
		Sprite constructionIconSprite = m_constructionIconSprite;
		MeshRenderer component = constructionIconSprite.GetComponent<MeshRenderer>();
		MeshRenderer component2 = constructionIconSprite.transform.Find("Background").GetComponent<MeshRenderer>();
		component.material.color = color.WithAlpha(num);
		component2.material.color = color.WithAlpha(num3);
	}

	public static Color GetColor(int partIndex)
	{
		if (new PartTypeInfo(PartType.MetalFrame, partIndex).BelongsTo(BasePart.TransparentFrames))
		{
			return Color.white;
		}
		int num = partIndex - 12;
		if (num >= 108)
		{
			float num2 = 1f - (float)(num - 108) / 10f;
			return new Color(num2, num2, num2);
		}
		PartSettings partSettings = INUserSettings.Instance.PartSettings;
		int num3 = num % 18;
		int num4 = num / 18 % 2;
		int num5 = num / 36;
		float h = (float)num3 / 18f;
		float s = ((num4 == 0) ? partSettings.ColoredFrameSaturation1 : partSettings.ColoredFrameSaturation2);
		return Color.HSVToRGB(h, s, num5 switch
		{
			1 => partSettings.ColoredFrameBrightness2, 
			0 => partSettings.ColoredFrameBrightness1, 
			_ => partSettings.ColoredFrameBrightness3, 
		});
	}

	public override void PostInitialize()
	{
		if (INSettings.GetBool(INFeature.NonCollisionColoredFrame))
		{
			GetComponent<Collider>().enabled = false;
		}
	}

	private void FixedUpdate()
	{
		bool flag = true;
		BasePart basePart = m_enclosedPart;
		if (basePart != null)
		{
			bool num = basePart.IsWoodenBox();
			bool flag2 = basePart.IsMetalBox();
			flag = !(num || flag2);
			if (m_coloredPart == null)
			{
				if (INSettings.GetBool(INFeature.CanColorEnclosedPart))
				{
					MeshRenderer[] componentsInChildren = basePart.GetComponentsInChildren<MeshRenderer>(includeInactive: true);
					m_coloredPartMaterials = new(MeshRenderer, Material)[componentsInChildren.Length];
					Shader shader = INUnity.LoadShader("Unlit_ColorTransparent_GrayOverlay");
					float value = INSettings.GetFloat(INFeature.EnclosedPartColorBlend);
					for (int i = 0; i < componentsInChildren.Length; i++)
					{
						MeshRenderer meshRenderer = componentsInChildren[i];
						if (meshRenderer.name != "INLight")
						{
							m_coloredPartMaterials[i] = (meshRenderer, meshRenderer.material);
							float num2 = Math.Max(m_color.a, 0.5f);
							Material material = new Material(meshRenderer.material);
							material.shader = shader;
							material.color = m_color.WithAlpha(num2 * meshRenderer.material.color.a);
							material.SetFloat("_Blend", value);
							meshRenderer.material = material;
						}
					}
				}
				else
				{
					m_coloredPartMaterials = null;
				}
				m_coloredPart = basePart;
			}
		}
		m_renderer.enabled = flag;
		m_foregroundRenderer.enabled = flag;
		m_backgroundRenderer.enabled = flag;
	}

	public void SetColorAndUpdateRenderers(Color color)
	{
		m_color = color;
		UpdateRenderers();
	}

	public void UpdateRenderers()
	{
		m_renderer.material.color = m_color.WithAlpha(m_color.a * m_alpha);
		m_foregroundRenderer.material.color = m_color.WithAlpha(m_color.a * m_foregroundAlpha);
		m_backgroundRenderer.material.color = m_color.WithAlpha(m_color.a * m_backgroundAlpha);
		if (m_coloredPartMaterials == null)
		{
			return;
		}
		(MeshRenderer, Material)[] coloredPartMaterials = m_coloredPartMaterials;
		for (int i = 0; i < coloredPartMaterials.Length; i++)
		{
			(MeshRenderer, Material) tuple = coloredPartMaterials[i];
			Renderer renderer;
			(renderer, _) = tuple;
			if (renderer != null)
			{
				float a = Math.Max(m_color.a, 0.5f) * tuple.Item2.color.a;
				renderer.material.color = m_color.WithAlpha(a);
			}
		}
	}

	private void OnDestroy()
	{
		if (!(m_coloredPart != null) || m_coloredPartMaterials == null)
		{
			return;
		}
		(MeshRenderer, Material)[] coloredPartMaterials = m_coloredPartMaterials;
		for (int i = 0; i < coloredPartMaterials.Length; i++)
		{
			(MeshRenderer, Material) tuple = coloredPartMaterials[i];
			if (tuple.Item1 != null)
			{
				tuple.Item1.material = tuple.Item2;
			}
		}
	}
}
