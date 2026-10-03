using System;
using System.Collections.Generic;

public class JointManager : PartManager
{
	private List<JointBase> m_joints;

	private Graph<JointBase> m_jointGraph;

	private INPhysicsManager m_physicsManager;

	public List<JointBase> Joint => m_joints;

	public override StatusCode Status => StatusCode.Running;

	public static JointManager Instance { get; private set; }

	protected override void Initialize()
	{
		base.Initialize();
		m_joints = new List<JointBase>();
		Instance = this;
		m_physicsManager = INPhysicsManager.Instance;
		if (m_physicsManager != null)
		{
			m_physicsManager.BeforeSimulation += BeforeSimulation;
			m_physicsManager.AfterSimulation += AfterSimulation;
		}
	}

	public override void OnDestroy()
	{
		if (m_physicsManager != null)
		{
			m_physicsManager.BeforeSimulation -= BeforeSimulation;
			m_physicsManager.AfterSimulation -= AfterSimulation;
		}
		m_physicsManager = null;
		if (Instance == this) Instance = null;
	}

	public void Register(JointBase joint)
	{
		if (joint == null)
		{
			throw new ArgumentNullException("joint");
		}
		m_joints.Add(joint);
	}

	public override void FixedUpdate()
	{
		foreach (JointBase joint in m_joints)
		{
			joint.Prepare();
		}
	}

	private void BeforeSimulation()
	{
		if (m_joints.Count == 0) return;
		for (int i = 0; i < 8; i++)
		{
			foreach (JointBase joint in m_joints)
			{
				joint.PreSolve();
			}
		}
		for (int j = 0; j < 8; j++)
		{
			foreach (JointBase joint2 in m_joints)
			{
				joint2.PostSolve();
			}
		}
	}

	private void AfterSimulation()
	{
	}
}
