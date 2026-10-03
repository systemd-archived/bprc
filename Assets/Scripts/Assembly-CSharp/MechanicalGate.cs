using System;
using System.Collections.Generic;
using UnityEngine;

public class MechanicalGate : MechanicalPart
{
	private struct CurveController
	{
		private float m_start;

		private float m_end;

		private float m_delta;

		private float m_progress;

		private float m_value;

		public float Start => m_start;

		public float End => m_end;

		public float Progress => m_progress;

		public float Value => m_value;

		public CurveController(float start, float end, float delta)
		{
			this = default(CurveController);
			Set(start, end, delta);
		}

		public void Set(float start, float end, float delta)
		{
			m_start = start;
			m_end = end;
			m_delta = delta;
			m_progress = 0f;
			m_value = start;
		}

		public void Update()
		{
			float num = Math.Clamp(m_progress + m_delta, 0f, 1f);
			if (m_progress != num)
			{
				float num2 = 0.5f * (1f - MathF.Cos(MathF.PI * num));
				m_progress = num;
				m_value = (1f - num2) * m_start + num2 * m_end;
			}
		}
	}

	private BoxCollider m_gateCollider;

	private MeshRenderer m_gateRenderer;

	private bool m_enabled;

	private float m_targetLength;

	private CurveController m_controller;

	private Transform m_visual;

	private bool m_visualSeparated;

	public int CurrentType => customPartIndex;

	public override void Awake()
	{
		base.Awake();
		INSerializedSprite component = GetComponent<INSerializedSprite>();
		if (component == null)
		{
			return;
		}
		component.SpriteName = "MechanicalGate" + (CurrentType + 1) + "_Sprite";
		component.UpdateMesh();
	}

	public override void SetRotation(GridRotation rotation)
	{
		m_gridRotation = rotation;
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
		// 根上的 Sprite 组件依赖 MeshFilter/MeshRenderer，需先移除才能安全销毁网格组件
		Sprite rootSprite = GetComponent<Sprite>();
		if (rootSprite != null)
		{
			DestroyComponent(rootSprite);
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
		if (m_gateCollider == null)
		{
			m_gateCollider = base.transform.Find("GateCollider").GetComponent<BoxCollider>();
		}
		if (m_gateRenderer == null)
		{
			m_gateRenderer = base.transform.Find("GateRenderer").GetComponent<MeshRenderer>();
		}
		if (diagonal)
		{
			GridRotation ortho = (GridRotation)(((int)m_gridRotation - (int)GridRotation.Deg_45) % 4);
			ApplyBuildRotation(Quaternion.AngleAxis(GetRotationAngle(ortho), Vector3.forward), false);
			if ((bool)visualization)
			{
				visualization.localRotation = Quaternion.AngleAxis(45f, Vector3.forward);
			}
			if (m_gateCollider != null)
			{
				m_gateCollider.transform.localRotation = Quaternion.AngleAxis(45f, Vector3.forward);
			}
			if (m_gateRenderer != null)
			{
				m_gateRenderer.transform.localRotation = Quaternion.AngleAxis(45f, Vector3.forward);
			}
		}
		else
		{
			ApplyBuildRotation(Quaternion.AngleAxis(GetRotationAngle(m_gridRotation), Vector3.forward), false);
			if ((bool)visualization)
			{
				visualization.localRotation = Quaternion.identity;
			}
			if (m_gateCollider != null)
			{
				m_gateCollider.transform.localRotation = Quaternion.identity;
			}
			if (m_gateRenderer != null)
			{
				m_gateRenderer.transform.localRotation = Quaternion.identity;
			}
		}
	}

	public override bool CanBeEnabled()
	{
		return true;
	}

	public override bool IsEnabled()
	{
		return m_enabled;
	}

	public override void Initialize()
	{
		base.Initialize();
		m_targetLength = GetTargetLength();
		// 允许按部件覆盖门伸出长度
		m_targetLength = GetCustomParam("GateLength", m_targetLength);
		m_gateCollider = base.transform.Find("GateCollider").GetComponent<BoxCollider>();
		m_gateRenderer = base.transform.Find("GateRenderer").GetComponent<MeshRenderer>();
		GamePhysics.IgnoreCollision(GetComponent<Collider>(), m_gateCollider);
		if (m_gateCollider != null)
		{
			m_gateCollider.enabled = m_enabled;
		}
	}

	private float GetTargetLength()
	{
		string lengths = INUserSettings.Instance?.StarSeaSettings?.GateDefaultLengths ?? "1,2,4,8";
		string[] parts = lengths.Split(',');
		if (CurrentType < parts.Length && float.TryParse(parts[CurrentType].Trim(), out float val))
		{
			return val;
		}
		return CurrentType switch
		{
			0 => 1f, 
			1 => 2f, 
			2 => 4f, 
			3 => 8f, 
			4 => 1f, 
			_ => throw new InvalidOperationException(), 
		};
	}

	public override IEnumerable<UIPartSliderButtonInfo> GetSliderButtonInfo()
	{
		if (CurrentType == 4)
		{
			float sliderMin = 0.1f, sliderMax = 8f, sliderStep = 0.1f;
			string range = INUserSettings.Instance?.StarSeaSettings?.GateAdjustableRange ?? "0.1,8,0.1";
			string[] rp = range.Split(',');
			if (rp.Length >= 3)
			{
				float.TryParse(rp[0].Trim(), out sliderMin);
				float.TryParse(rp[1].Trim(), out sliderMax);
				float.TryParse(rp[2].Trim(), out sliderStep);
			}
			yield return new UIPartSliderButtonInfo(UIPartButtonType.Slider, 1, base.Type, 0, base.ConnectedComponent, new UIPartSliderButton.Range(m_targetLength, 1f, sliderMin, sliderMax, sliderStep, 0.01f));
		}
	}

	public override void OnSliderButtonTriggered(UIPartSliderButton button)
	{
		m_targetLength = button.Value;
		if (m_enabled)
		{
			SetController(m_controller.Value, m_targetLength);
		}
	}

	protected override void OnTouch()
	{
		SetEnabled(!m_enabled);
	}

	public override void SetEnabled(bool enabled)
	{
		if (m_enabled != enabled)
		{
			m_enabled = enabled;
			float value = m_controller.Value;
			float end = (enabled ? m_targetLength : 0f);
			SetController(value, end);
		}
	}

	private void SetController(float start, float end)
	{
		float speed = GetCustomParam("GateSpeed", INUserSettings.Instance?.StarSeaSettings?.GateExtensionSpeed ?? 0.2f);
		float delta = Math.Min(Math.Abs(speed / (end - start)), 1f);
		m_controller.Set(start, end, delta);
	}

	private static float ParseCoeffAtIndex(string csv, int index, float fallback)
	{
		if (string.IsNullOrEmpty(csv)) return fallback;
		string[] parts = csv.Split(',');
		if (index < parts.Length && float.TryParse(parts[index].Trim(), out float val))
			return val;
		return fallback;
	}

	private void FixedUpdate()
	{
		if (!(base.contraption == null) && base.contraption.IsRunning)
		{
			StarSeaSettings starSea = INUserSettings.Instance?.StarSeaSettings;
			float widthCoeff = GetCustomParam("GateWidth", starSea?.GateWidthCoefficient ?? 1f);
			string lengthCsv = starSea?.GateLengthCoefficients ?? "1,1,1,1,1";
			float lengthCoeff = GetCustomParam("GateThickness", ParseCoeffAtIndex(lengthCsv, CurrentType, 1f));
			float opacity = GetCustomParam("GateOpacity", starSea?.GateOpacity ?? 0.8f);
			m_controller.Update();
			float value = m_controller.Value;
			float gateAngle = GetGateAngle();
			Vector3 localPos = new Vector3(0.5f + value * 0.5f, 0f, 0f);
			if (gateAngle != 0f)
			{
				localPos = Quaternion.AngleAxis(gateAngle, Vector3.forward) * localPos;
			}
			m_gateCollider.transform.localPosition = localPos;
			m_gateCollider.transform.localScale = new Vector3(value * widthCoeff, lengthCoeff, 1f);
			m_gateRenderer.transform.localPosition = new Vector3(localPos.x, localPos.y, 0.01f);
			m_gateRenderer.transform.localScale = new Vector3(value * widthCoeff, lengthCoeff, 1f);
			Color color = m_gateRenderer.material.color;
			color.a = opacity;
			m_gateRenderer.material.color = color;
			if (m_gateCollider != null)
			{
				m_gateCollider.enabled = value > 0f;
			}
		}
	}

	private float GetGateAngle()
	{
		if (m_gridRotation < GridRotation.Deg_45)
		{
			return 0f;
		}
		GridRotation ortho = (GridRotation)(((int)m_gridRotation - (int)GridRotation.Deg_45) % 4);
		return GetRotationAngle(m_gridRotation) - GetRotationAngle(ortho);
	}

	// 额外可调项：门长度 / 伸出速度 / 门宽度 / 门厚度 / 不透明度（原本来自全局星海设置）
	public override void CollectExtraParams(List<string> keys)
	{
		keys.Add("GateLength");
		keys.Add("GateSpeed");
		keys.Add("GateWidth");
		keys.Add("GateThickness");
		keys.Add("GateOpacity");
	}

	public override bool ApplyExtraParam(string key, float value)
	{
		if (key == "GateLength")
		{
			m_targetLength = value;
			if (m_controller.Value > 0f || m_enabled)
			{
				SetController(m_controller.Value, m_enabled ? m_targetLength : 0f);
			}
			return true;
		}
		if (key == "GateSpeed" || key == "GateWidth" || key == "GateThickness" || key == "GateOpacity")
		{
			return true;
		}
		return base.ApplyExtraParam(key, value);
	}

	public override bool TryGetExtraParamValue(string key, out float value)
	{
		value = 0f;
		StarSeaSettings starSea = INUserSettings.Instance?.StarSeaSettings;
		switch (key)
		{
			case "GateLength":
				value = (m_targetLength > 0f) ? m_targetLength : GetTargetLength();
				return true;
			case "GateSpeed":
				value = starSea?.GateExtensionSpeed ?? 0.2f;
				return true;
			case "GateWidth":
				value = starSea?.GateWidthCoefficient ?? 1f;
				return true;
			case "GateThickness":
				value = ParseCoeffAtIndex(starSea?.GateLengthCoefficients ?? "1,1,1,1,1", CurrentType, 1f);
				return true;
			case "GateOpacity":
				value = starSea?.GateOpacity ?? 0.8f;
				return true;
		}
		return base.TryGetExtraParamValue(key, out value);
	}
}
