using System;
using System.Collections.Generic;
using UnityEngine;

public class IndicatorPart : ElectricalPart
{
	private enum IndicatorType
	{
		Ammeter = 0,
		Voltmeter = 1
	}

	[SerializeField]
	private IndicatorType m_type;

	private Resistor m_indicator;

	private double m_I;

	private double m_U1;

	private double m_U2;

	private GameObject m_symbol;

	private TextMesh m_text;

	private VoltageSource m_ohmSource;

	private Wire m_leftProbe;

	private Wire m_rightProbe;

	private bool m_exactMode;

	private TextMesh m_exactText;

	private Transform m_visual;

	private bool m_visualSeparated;

	public override IEnumerable<CircuitElement> ElectricalElements
	{
		get
		{
			if (m_leftProbe != null)
			{
				yield return m_leftProbe;
				yield return m_rightProbe;
			}
			else
			{
				yield return m_indicator;
			}
			if (m_ohmSource != null)
			{
				yield return m_ohmSource;
			}
		}
	}

	public override void Awake()
	{
		base.Awake();
		m_symbol = base.transform.Find("Symbol").gameObject;
		m_text = base.transform.Find("Text").GetComponent<TextMesh>();
		m_I = double.NaN;
		m_U1 = double.NaN;
		m_U2 = double.NaN;
	}

	public override void CreateElectricalElements()
	{
		Resistor resistor = null;
		bool flag = m_gridRotation == GridRotation.Deg_45 || m_gridRotation == GridRotation.Deg_135 || m_gridRotation == GridRotation.Deg_225 || m_gridRotation == GridRotation.Deg_315;
		switch (m_type)
		{
		case IndicatorType.Ammeter:
			if (flag)
			{
				m_ohmSource = new VoltageSource(5.0, 0.0);
				resistor = new Resistor(100.0);
				CircuitFactory.Connect(m_ohmSource.Anode, resistor.Electrode1);
			}
			else
			{
				resistor = new Resistor(0.0);
			}
			break;
		case IndicatorType.Voltmeter:
			if (flag)
			{
				m_leftProbe = new Wire(1);
				m_rightProbe = new Wire(1);
				m_leftProbe.ElementUpdated += OnElementUpdated;
				m_rightProbe.ElementUpdated += OnElementUpdated;
			}
			else
			{
				resistor = new Resistor(1000000.0);
			}
			break;
		}
		if (resistor != null)
		{
			resistor.ElementUpdated += OnElementUpdated;
			m_indicator = resistor;
		}
	}

	private void OnElementUpdated(CircuitSimulator simulator, SimulationResult result)
	{
		if (m_leftProbe != null)
		{
			if (result.Element == m_leftProbe)
			{
				m_U1 = result.U;
			}
			else if (result.Element == m_rightProbe)
			{
				m_U2 = result.U;
			}
			return;
		}
		if (m_ohmSource != null)
		{
			m_I = Math.Abs(result.I);
			m_U1 = result.U;
			return;
		}
		if (result.Electrode == m_indicator.Electrode1)
		{
			m_I = result.I;
			m_U1 = result.U;
			return;
		}
		m_I = 0.0 - result.I;
		m_U2 = result.U;
	}

	protected override BitDirection GetConnectionDirection()
	{
		return BitDirection.LeftAndRight.Rotate((int)m_gridRotation);
	}

	protected override Electrode FindElectrode(BitDirection direction)
	{
		direction = direction.Rotate(0 - m_gridRotation);
		if (m_leftProbe != null)
		{
			return direction switch
			{
				BitDirection.Left => m_leftProbe.Electrodes[0],
				BitDirection.Right => m_rightProbe.Electrodes[0],
				_ => null,
			};
		}
		if (m_ohmSource != null)
		{
			return direction switch
			{
				BitDirection.Left => m_indicator.Electrode2, 
				BitDirection.Right => m_ohmSource.Cathode, 
				_ => null, 
			};
		}
		return direction switch
		{
			BitDirection.Left => m_indicator.Electrode1, 
			BitDirection.Right => m_indicator.Electrode2, 
			_ => null, 
		};
	}

	public override void SetRotation(GridRotation rotation)
	{
		base.SetRotation(rotation);
		EnsureVisualSeparated();
		ApplyOrientation();
		m_symbol.transform.rotation = Quaternion.identity;
		m_text.transform.rotation = Quaternion.identity;
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
		bool diagonal = m_gridRotation >= GridRotation.Deg_45;
		if (diagonal)
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

	public override void PostUpdateElements()
	{
		double num = 0.0;
		if (m_ohmSource != null)
		{
			if (m_I != 0.0)
			{
				num = 5.0 / m_I - 100.0;
			}
		}
		else if (m_type == IndicatorType.Ammeter)
		{
			num = m_I;
		}
		else if (m_type == IndicatorType.Voltmeter)
		{
			num = m_U1 - m_U2;
		}
		if (double.IsNaN(num))
		{
			num = 0.0;
		}
		m_I = double.NaN;
		m_U1 = double.NaN;
		m_U2 = double.NaN;
		bool flag = num >= 0.0;
		num = (flag ? num : (0.0 - num));
		string text;
		if (num == 0.0)
		{
			text = "0";
		}
		else if (num >= 99000000000.0)
		{
			text = "---";
		}
		else if (num >= 1000.0)
		{
			int num2 = (int)Math.Floor(Math.Log10(num));
			int num3 = (int)Math.Floor(num / Math.Pow(10.0, (double)num2) * 10.0 + 0.5);
			if (num3 >= 100)
			{
				num3 = 10;
				num2++;
			}
			text = (num3 / 10).ToString() + "." + (num3 % 10).ToString() + "-" + num2.ToString();
		}
		else if (num < 0.01)
		{
			int num4 = (int)Math.Ceiling(-Math.Log10(num));
			int num5 = (int)Math.Floor(num * Math.Pow(10.0, (double)num4) * 10.0 + 0.5);
			if (num5 >= 100)
			{
				num5 = 10;
				num4--;
			}
			if (num5 <= 0)
			{
				text = "0";
			}
			else
			{
				text = num4.ToString() + "-" + (num5 / 10).ToString() + "." + (num5 % 10).ToString();
			}
		}
		else if (num < 1.0)
		{
			text = num.ToString(".000");
		}
		else if (num < 10.0)
		{
			text = num.ToString("0.00");
		}
		else if (num < 100.0)
		{
			text = num.ToString("00.0");
		}
		else
		{
			text = num.ToString("000");
		}
		m_text.text = (flag ? string.Empty : "-") + text;
		if (m_exactMode && m_exactText != null)
		{
			m_exactText.transform.rotation = Quaternion.identity;
			m_exactText.text = (flag ? string.Empty : "-") + num.ToString("G15");
		}
	}

	private void EnsureExactText()
	{
		if (m_exactText != null)
		{
			return;
		}
		m_exactText = new GameObject("ExactText")
		{
			transform =
			{
				parent = base.transform,
				localPosition = new Vector3(0f, 0f, -0.5f),
				localRotation = Quaternion.identity
			}
		}.AddComponent<TextMesh>();
		m_exactText.font = INUnity.ArialFont;
		m_exactText.fontSize = 40;
		m_exactText.characterSize = 0.05f;
		m_exactText.anchor = TextAnchor.MiddleCenter;
		m_exactText.alignment = TextAlignment.Center;
		m_exactText.color = Color.magenta;
	}

	protected override void OnTouch()
	{
		m_exactMode = !m_exactMode;
		if (m_exactMode)
		{
			EnsureExactText();
			m_exactText.gameObject.SetActive(true);
			return;
		}
		if (m_exactText != null)
		{
			m_exactText.gameObject.SetActive(false);
		}
	}
}
