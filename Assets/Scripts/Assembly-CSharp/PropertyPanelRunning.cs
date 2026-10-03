using System;
using System.Collections.Generic;
using UnityEngine;

public class PropertyPanelRunning : PropertyPanel
{
	private static PropertyPanelRunning s_instance;

	private string m_text;

	private Rigidbody m_targetPart;

	private Vector3 m_velocity;

	private Camera m_camera;

	private Heap<(int, int)> m_heap;

	public override StatusCode Status => StatusCode.Running;

	public static PropertyPanelRunning Instance => s_instance;

	public static PropertyPanelRunning Create()
	{
		PropertyPanelRunning propertyPanelRunning = (s_instance = new PropertyPanelRunning());
		propertyPanelRunning.Initialize();
		return propertyPanelRunning;
	}

	protected override void Initialize()
	{
		base.Initialize();
		m_heap = new Heap<(int, int)>();
	}

	public override void Start()
	{
		CreateText("PropertyTextRunning", new Vector2(230f, -54f));
		m_targetPart = Contraption.Instance.m_cameraTarget.rigidbody;
		m_velocity = m_targetPart.velocity;
		m_camera = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>();
	}

	public override void FixedUpdate()
	{
		bool flag = INUnity.Language == SystemLanguage.Chinese;
		Contraption instance = Contraption.Instance;
		Vector3 velocity = m_targetPart.velocity;
		Vector3 vector = (velocity - m_velocity) / Time.fixedDeltaTime;
		Vector3 angularVelocity = m_targetPart.angularVelocity;
		float num = 0f;
		Vector2 zero = Vector2.zero;
		int num2 = 0;
		int num3 = 0;
		int num4 = 0;
		foreach (PartGraph<Contraption.JointInfo>.Edge allEdge in Contraption.Instance.JointGraph.GetAllEdges())
		{
			if (allEdge.Value.Type == Contraption.JointType.Common)
			{
				num3++;
			}
			else if (allEdge.Value.Type == Contraption.JointType.Frame)
			{
				num4++;
			}
		}
		num3 /= 2;
		num4 /= 2;
		// Count special charge connections and joints
		int specialConnectionCount = 0;
		int specialJointCount = 0;
		bool showSpecialCharge = INUserSettings.Instance?.StarSeaSettings?.ShowJointConnectionCount ?? false;
		if (showSpecialCharge)
		{
			var allParts = instance.Parts;
			for (int si = 0; si < allParts.Count; si++)
			{
				if (allParts[si] is PointChargePart pcp && pcp.IsDeg90 && pcp.HasAutoJoint)
				{
					specialConnectionCount++;
					var partner = pcp.ConnectionPartner;
					if (partner != null)
					{
						specialJointCount += partner.GetComponents<ConfigurableJoint>().Length;
					}
				}
			}
		}

		Rigidbody[] components = INContraption.Instance.Rigidbodies;
		foreach (Rigidbody obj in components)
		{
			float mass = obj.mass;
			Vector3 worldCenterOfMass = obj.worldCenterOfMass;
			num += mass;
			zero.x += worldCenterOfMass.x * mass;
			zero.y += worldCenterOfMass.y * mass;
			num2++;
		}
		string text4;
		if (flag)
		{
			string text = instance.Parts.Count.ToString();
			string text2 = instance.ConnectedComponentCount.ToString();
			string text3 = num3.ToString();
			string[] array = PropertyPanel.FormatStrings(text, text2, text3);
			text = array[0];
			text2 = array[1];
			text3 = array[2];
			text4 = m_versionText + "\n" + PropertyPanel.FormatHeading2("目标部件属性") + "\n" + m_prefix + "速度\u3000 " + velocity.magnitude.ToString(m_format) + " " + velocity.Vector2ToString(m_format) + "\n" + m_prefix + "加速度 " + vector.magnitude.ToString(m_format) + " " + vector.Vector2ToString(m_format) + "\n" + m_prefix + "位置\u3000 " + m_targetPart.position.Vector2ToString(m_format) + "\n" + m_prefix + "角度\u3000 " + m_targetPart.rotation.eulerAngles.z.ToString(m_format) + "\n" + m_prefix + "角速度 " + angularVelocity.z.ToString(m_format) + "\n\n" + PropertyPanel.FormatHeading2("视野属性") + "\n" + m_prefix + "大小\u3000 " + m_camera.orthographicSize.ToString(m_format) + "\n" + m_prefix + "位置\u3000 " + m_camera.transform.position.Vector2ToString(m_format) + "\n\n" + PropertyPanel.FormatHeading2("载具属性") + "\n" + m_prefix + "部件数 " + text + " | " + "刚体数 " + num2 + "\n" + m_prefix + "载具数 " + text2 + " | " + "总质量 " + num.ToString(m_format) + "\n" + m_prefix + "连接数 " + text3 + " | " + "框架连接数 " + num4 + "\n" + (specialConnectionCount > 0 ? m_prefix + "特殊连接 " + specialConnectionCount + " | " + "特殊关节 " + specialJointCount + "\n" : "");
		}
		else
		{
			text4 = m_versionText + "\n" + PropertyPanel.FormatHeading2("Camera Target Properties") + "\n" + m_prefix + "Velocity " + velocity.magnitude.ToString(m_format) + " " + velocity.Vector2ToString(m_format) + "\n" + m_prefix + "Acceleration " + vector.magnitude.ToString(m_format) + " " + vector.Vector2ToString(m_format) + "\n" + m_prefix + "Position " + m_targetPart.position.Vector2ToString(m_format) + "\n" + m_prefix + "Angle " + m_targetPart.rotation.eulerAngles.z.ToString(m_format) + "\n" + m_prefix + "Angular Velocity " + angularVelocity.z.ToString(m_format) + "\n\n" + PropertyPanel.FormatHeading2("Camera Properties") + "\n" + m_prefix + "Size " + m_camera.orthographicSize.ToString(m_format) + "\n" + m_prefix + "Position " + m_camera.transform.position.Vector2ToString(m_format) + "\n\n" + PropertyPanel.FormatHeading2("Contraption Properties") + "\n" + m_prefix + "Part Count " + instance.Parts.Count + " | " + "Rigidbody Count " + num2 + "\n" + m_prefix + "Vehicle Count " + instance.ConnectedComponentCount + " | " + "Total Mass " + num.ToString(m_format) + "\n" + m_prefix + "Joint Count " + num3.ToString().ToString() + " | " + "Frame Joint Count " + num4 + "\n" + (specialConnectionCount > 0 ? m_prefix + "Special Connections " + specialConnectionCount + " | " + "Special Joints " + specialJointCount + "\n" : "");
		}
		if (INSettings.GetBool(INFeature.EnhancedPropertyPanel))
		{
			float num5 = 0f;
			float num6 = 0f;
			components = INContraption.Instance.Rigidbodies;
			foreach (Rigidbody rigidbody in components)
			{
				Vector3 velocity2 = rigidbody.velocity;
				float num7 = velocity2.x * velocity2.x + velocity2.y * velocity2.y;
				float num8 = Mathf.Sqrt(num7);
				num5 += num8;
				num6 += 0.5f * rigidbody.mass * num7;
			}
			text4 += "\n";
			int num9 = Math.Min(Contraption.Instance.ConnectedComponentCount, 3);
			m_heap.PushRange(GetComponentInfo());
			for (int j = 0; j < num9; j++)
			{
				text4 += GetVehicleText(j + 1, m_heap.Pop().Item2);
			}
			m_heap.Clear();
		}
		m_text = text4;
		m_targetPart = Contraption.Instance.m_cameraTarget.rigidbody;
		m_velocity = m_targetPart.velocity;
		static IEnumerable<(int, int)> GetComponentInfo()
		{
			int componentCount = Contraption.Instance.ConnectedComponentCount;
			for (int k = 0; k < componentCount; k++)
			{
				yield return (-Contraption.Instance.ComponentPartCount(k), k);
			}
		}
	}

	private string GetVehicleText(int index, int connectedComponent)
	{
		bool flag = INUnity.Language == SystemLanguage.Chinese;
		List<BasePart> connectedParts = Contraption.Instance.GetConnectedParts(connectedComponent);
		string text = null;
		if (MarkerManager.IsInstantiated)
		{
			int teamIndex = MarkerManager.Instance.GetTeamIndex(connectedComponent);
			for (int i = 0; i < 4; i++)
			{
				if ((teamIndex & (1 << i)) != 0)
				{
					text += (char)(65 + i);
				}
			}
			text = ((!string.IsNullOrEmpty(text)) ? ((flag ? "阵营" : "Team") + text) : (flag ? "无阵营" : "NonTeam"));
		}
		float num = 0f;
		Vector2 zero = Vector2.zero;
		float num2 = 0f;
		float num3 = 0f;
		foreach (BasePart item in connectedParts)
		{
			if (item == null)
			{
				continue;
			}
			foreach (Rigidbody rigidbody in item.GetRigidbodies())
			{
				float mass = rigidbody.mass;
				Vector3 worldCenterOfMass = rigidbody.worldCenterOfMass;
				num += mass;
				zero.x += worldCenterOfMass.x * mass;
				zero.y += worldCenterOfMass.y * mass;
				Vector3 velocity = rigidbody.velocity;
				float num4 = velocity.x * velocity.x + velocity.y * velocity.y;
				float num5 = Mathf.Sqrt(num4);
				num2 += num5;
				num3 += 0.5f * rigidbody.mass * num4;
			}
		}
		if (flag)
		{
			return PropertyPanel.FormatHeading2("载具" + index) + " " + text + "\n" + m_prefix + "部件数 " + connectedParts.Count + " | " + "总质量 " + num.ToString(m_format) + "\n" + m_prefix + "速度均值 " + (num2 / (float)connectedParts.Count).ToString(m_format) + " | " + "总动能 " + num3.ToString(m_format) + "\n" + m_prefix + "质心\u3000 " + (zero / num).Vector2ToString(m_format) + "\n";
		}
		return PropertyPanel.FormatHeading2("Vehicle" + index) + " " + text + "\n" + m_prefix + "Part Count " + connectedParts.Count + " | " + "Total Mass " + num.ToString(m_format) + "\n" + m_prefix + "Average Speed " + (num2 / (float)connectedParts.Count).ToString(m_format) + " | " + "Kinetic Energy " + num3.ToString(m_format) + "\n" + m_prefix + "Center of Mass " + (zero / num).Vector2ToString(m_format) + "\n";
	}

	public override void Update()
	{
		LevelManager.GameState gameState = WPFMonoBehaviour.levelManager.gameState;
		RectTransform component = m_textMesh.GetComponent<RectTransform>();
		switch (gameState)
		{
		case LevelManager.GameState.PausedWhileRunning:
			m_textMesh.text = m_text;
			component.anchoredPosition = new Vector2(288f, -54f);
			break;
		case LevelManager.GameState.Running:
			m_textMesh.text = m_text;
			component.anchoredPosition = new Vector2(230f, -54f);
			break;
		default:
			m_textMesh.text = string.Empty;
			break;
		}
	}
}
