using System.Collections.Generic;
using UnityEngine;

public class PointChargePart : ElectricalPart
{
	private bool m_enabled;

	private MeshRenderer m_renderer;

	private bool m_invincibleApplied;

	private bool m_collisionIgnored;

	private ConfigurableJoint m_ringJoint;

	private SphereCollider m_circleCollider;

	private PointChargePart m_connectionPartner;

	private PointChargePart m_previousPartner;

	private bool m_needsRescan;

	private List<FixedJoint> m_reinforceJoints = new List<FixedJoint>();

	private readonly List<Collider> m_ownColliderBuffer = new List<Collider>();

	// Static buffers for GetComponents — safe because LateUpdate is sequential
	private static readonly List<Collider> s_colliderBufA = new List<Collider>();
	private static readonly List<Collider> s_colliderBufB = new List<Collider>();

	public bool HasAutoJoint => m_ringJoint != null;

	public PointChargePart ConnectionPartner => m_connectionPartner;

	public bool IsPositive
	{
		get
		{
			return m_gridRotation == GridRotation.Deg_0;
		}
	}

	public bool IsDeg90 => m_gridRotation == GridRotation.Deg_90;

	public bool IsDeg270 => m_gridRotation == GridRotation.Deg_270;

	public bool IsSpecialCharge => IsDeg90 || IsDeg270;

	public override bool IsCustomRotated()
	{
		return true;
	}

	public override void SetFlipped(bool flipped)
	{
	}

	public override void SetRotation(int rotation)
	{
		SetRotation((GridRotation)rotation);
	}

	protected override BitDirection GetConnectionDirection()
	{
		// 点电荷不参与电路/部件的“相邻连接”（原版：ElectricalPart 默认 None 且 Awake 里对点电荷也设 None）。
		// 之前被改成 Any，导致点电荷会和四周任何电路部件（含其它点电荷）建立 FixedJoint（互相“粘连”）。
		return BitDirection.None;
	}

	public float Charge
	{
		get
		{
			if (!m_enabled)
			{
				return 0f;
			}
			float amount = ChargeAmount;
			if (!IsPositive)
			{
				return 0f - amount;
			}
			return amount;
		}
	}

	// 电荷量（默认 20；可在长按面板里按部件覆盖）。
	// 注意：不要用上面的 Charge 来显示，因为部件关闭时它恒为 0。
	public float ChargeAmount
	{
		get
		{
			return GetCustomParam("ChargeAmount", GetCustomParam("Charge", 20f));
		}
	}

	// 额外可调项：电荷量（原本硬编码 ±20）
	public override void CollectExtraParams(List<string> keys)
	{
		keys.Add("ChargeAmount");
	}

	public override void Awake()
	{
		base.Awake();
		// 点电荷不可与部件自动连接（预制体原本就是 None）——恢复原版：电荷之间/与部件都不会自动连成一体
m_jointConnectionDirection = BasePart.JointConnectionDirection.None;
		m_renderer = GetComponent<MeshRenderer>();
	}

	public override bool IsEnabled()
	{
		return m_enabled;
	}

	public override bool IsTriggerable()
	{
		return !base.HasGeneratorRef;
	}

	public override IEnumerable<UIPartTriggerButtonInfo> GetTriggerButtonInfo()
	{
		int partIndex = IsSpecialCharge ? 7 : 4;
		yield return new UIPartTriggerButtonInfo(UIPartButtonType.Trigger, 0, base.Type, partIndex, base.ConnectedComponent);
	}

	public override void SetRotation(GridRotation rotation)
	{
		int r = (int)rotation % 4;
		if (r < 0) r += 4;
		m_gridRotation = (GridRotation)r;
		bool usePlus = m_gridRotation == GridRotation.Deg_0 || m_gridRotation == GridRotation.Deg_90;
		bool isRotated = m_gridRotation == GridRotation.Deg_90 || m_gridRotation == GridRotation.Deg_270;
		INSerializedSprite component = GetComponent<INSerializedSprite>();
		component.SpriteName = (usePlus ? "PointCharge1_Sprite" : "PointCharge2_Sprite");
		component.UpdateMesh();
		float rotZ = m_gridRotation == GridRotation.Deg_270 ? 90f : (m_gridRotation == GridRotation.Deg_90 ? 45f : 0f);
		ApplyBuildRotation(Quaternion.Euler(0f, 0f, rotZ), false);
	}

	public override void CreateCustomJoints()
	{
		base.CreateCustomJoints();
		MakeInvincible();
	}

	private void MakeInvincible()
	{
		if (m_invincibleApplied) return;
		if (!IsSpecialCharge) return;
		if (!(INUserSettings.Instance?.StarSeaSettings?.SpecialPointChargeInvincible ?? true)) return;
		FixedJoint[] joints = GetComponents<FixedJoint>();
		if (joints.Length == 0) return;
		for (int i = 0; i < joints.Length; i++)
		{
			joints[i].breakForce = float.PositiveInfinity;
		}
		HingeJoint[] hinges = GetComponents<HingeJoint>();
		for (int j = 0; j < hinges.Length; j++)
		{
			hinges[j].breakForce = float.PositiveInfinity;
		}
		m_invincibleApplied = true;
	}

	private static HashSet<long> s_ignoredColliderPairs = new HashSet<long>();

	// Anti-tunnel: colliders from connected vehicles that should skip penetration checks
	private static Dictionary<Collider, int> s_antiTunnelRefCount = new Dictionary<Collider, int>();
	public static HashSet<Collider> AntiTunnelIgnoreColliders { get; } = new HashSet<Collider>();

	private static void AddAntiTunnelCollider(Collider col)
	{
		if (s_antiTunnelRefCount.TryGetValue(col, out int count))
			s_antiTunnelRefCount[col] = count + 1;
		else
		{
			s_antiTunnelRefCount[col] = 1;
			AntiTunnelIgnoreColliders.Add(col);
		}
	}

	private static void RemoveAntiTunnelCollider(Collider col)
	{
		if (s_antiTunnelRefCount.TryGetValue(col, out int count))
		{
			if (count <= 1)
			{
				s_antiTunnelRefCount.Remove(col);
				AntiTunnelIgnoreColliders.Remove(col);
			}
			else
				s_antiTunnelRefCount[col] = count - 1;
		}
	}

	[UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
	private static void ClearStaticCache()
	{
		s_ignoredColliderPairs.Clear();
		s_antiTunnelRefCount.Clear();
		AntiTunnelIgnoreColliders.Clear();
	}

	private void LateUpdate()
	{
		if (!m_invincibleApplied)
		{
			MakeInvincible();
		}
		if (!m_collisionIgnored)
		{
			SetupSpecialChargeCollisionIgnore();
			m_collisionIgnored = true;
		}
		if (m_ringJoint != null)
		{
			if (!m_enabled || m_connectionPartner == null || !m_connectionPartner.m_enabled)
			{
				DestroyConnection();
			}
			else
			{
				// Re-disable own colliders every frame (game may re-enable)
				GetComponents<Collider>(m_ownColliderBuffer);
				for (int i = 0; i < m_ownColliderBuffer.Count; i++)
				{
					m_ownColliderBuffer[i].enabled = false;
				}
				// Only ring charge (Deg_90) does collision scan to avoid duplicate work
				if (IsDeg90 && m_connectionPartner != null)
				{
					// NO cc1 != cc2 guard — always scan so new parts are covered
					// even after vehicles merge into one ConnectedComponent
					int cc1 = ConnectedComponent;
					int cc2 = m_connectionPartner.ConnectedComponent;
					var allParts = Contraption.Instance?.Parts;
					if (allParts != null)
					{
						for (int i = 0; i < allParts.Count; i++)
						{
							if (allParts[i].ConnectedComponent != cc1) continue;
							s_colliderBufA.Clear();
							allParts[i].GetComponentsInChildren<Collider>(s_colliderBufA);
							for (int ci = 0; ci < s_colliderBufA.Count; ci++)
							{
								var c1 = s_colliderBufA[ci];
								if (c1 == null) continue;
								int id1 = c1.GetInstanceID();
								for (int j = 0; j < allParts.Count; j++)
								{
									if (allParts[j].ConnectedComponent != cc2) continue;
									s_colliderBufB.Clear();
									allParts[j].GetComponentsInChildren<Collider>(s_colliderBufB);
									for (int cj = 0; cj < s_colliderBufB.Count; cj++)
									{
										var c2 = s_colliderBufB[cj];
										if (c2 == null) continue;
										int id2 = c2.GetInstanceID();
										long key = id1 < id2 ? ((long)id1 << 32 | (uint)id2) : ((long)id2 << 32 | (uint)id1);
										if (s_ignoredColliderPairs.Add(key))
										{
											GamePhysics.IgnoreCollision(c1, c2, true);
										}
									}
								}
							}
						}
					}
				}
			}
		}
	}

	public void TryCreateRotatableJoint(PointChargePart other)
	{
		if (other == null || other.rigidbody == null) return;
		if (!m_enabled || !other.m_enabled) return;
		if (m_ringJoint != null) return;

		var settings = INUserSettings.Instance?.StarSeaSettings;
		float sizeScale = settings?.ChargeJointSize ?? 1f;
		float ringTriggerRadius = 0.6f * sizeScale;
		float circleRadius = 0.3f * sizeScale;

		// Snap: align visual centers before locking (if enabled)
		if ((settings?.ChargeJointSnapEnabled ?? true) && m_renderer != null && other.m_renderer != null)
		{
			Vector3 offset = m_renderer.bounds.center - other.m_renderer.bounds.center;
			other.transform.position += offset;
		}

		// Ring: trigger sphere (no physics push) + disable original collider
		var ringGo = new GameObject("RingTrigger");
		ringGo.transform.SetParent(transform, false);
		var ringCol = ringGo.AddComponent<SphereCollider>();
		ringCol.isTrigger = true;
		ringCol.radius = ringTriggerRadius;

		// Circle: trigger sphere (no collision with anything)
		var circleGo = new GameObject("CircleCollider");
		circleGo.transform.SetParent(other.transform, false);
		var circleCol = circleGo.AddComponent<SphereCollider>();
		circleCol.isTrigger = true;
		circleCol.radius = circleRadius;

		// Disable ALL original colliders on both charges
		var ringOwnColliders = GetComponentsInChildren<Collider>();
		for (int i = 0; i < ringOwnColliders.Length; i++)
		{
			ringOwnColliders[i].enabled = false;
		}
		var circleOwnColliders = other.GetComponentsInChildren<Collider>();
		for (int i = 0; i < circleOwnColliders.Length; i++)
		{
			circleOwnColliders[i].enabled = false;
		}
		// Ignore ring trigger with all scene colliders (one-time cost)
		var allCols = FindObjectsOfType<Collider>();
		for (int i = 0; i < allCols.Length; i++)
		{
			if (allCols[i] == circleCol || allCols[i] == ringCol) continue;
			GamePhysics.IgnoreCollision(allCols[i], ringCol, true);
		}
		GamePhysics.IgnoreCollision(circleCol, ringCol, true);

		// ConfigurableJoint: lock all translation, free Z rotation
		var joint = other.gameObject.AddComponent<ConfigurableJoint>();
		joint.connectedBody = rigidbody;

		// Set joint pivot at visual center (not sprite pivot/top-left)
		if (m_renderer != null && other.m_renderer != null)
		{
			joint.anchor = other.transform.InverseTransformPoint(other.m_renderer.bounds.center);
			joint.connectedAnchor = transform.InverseTransformPoint(m_renderer.bounds.center);
		}

		var zeroDrive = new JointDrive { positionSpring = 0f, positionDamper = 0f, maximumForce = 0f };
		joint.xDrive = zeroDrive;
		joint.yDrive = zeroDrive;
		joint.zDrive = zeroDrive;
		joint.xMotion = ConfigurableJointMotion.Locked;
		joint.yMotion = ConfigurableJointMotion.Locked;
		joint.zMotion = ConfigurableJointMotion.Locked;
		joint.angularXMotion = ConfigurableJointMotion.Free;
		joint.angularYMotion = ConfigurableJointMotion.Free;
		joint.angularZMotion = ConfigurableJointMotion.Free;

		float breakForce = (INUserSettings.Instance?.StarSeaSettings?.SpecialChargeJointBreakForce ?? 3000f);
		if (breakForce < 0f) breakForce = float.PositiveInfinity;
		joint.breakForce = breakForce;
		joint.breakTorque = float.PositiveInfinity;

		m_circleCollider = circleCol;
		m_ringJoint = joint;
		m_connectionPartner = other;
		other.m_circleCollider = circleCol;
		other.m_ringJoint = joint;
		other.m_connectionPartner = this;

		// Reinforce: connect to nearby parts on same contraption
		TryCreateReinforceJoints();

		// Collision ignore between all connected vehicles (unconditional, infinite range)
		SetupVehicleCollisionIgnore(other);

		// Anti-tunnel: register all colliders from both vehicles
		var antiTunnelParts = Contraption.Instance?.Parts;
		if (antiTunnelParts != null)
		{
			int atCC1 = ConnectedComponent;
			int atCC2 = other.ConnectedComponent;
			for (int ai = 0; ai < antiTunnelParts.Count; ai++)
			{
				if (antiTunnelParts[ai].ConnectedComponent != atCC1 && antiTunnelParts[ai].ConnectedComponent != atCC2) continue;
				var atCols = antiTunnelParts[ai].GetComponentsInChildren<Collider>();
				for (int aj = 0; aj < atCols.Length; aj++)
					AddAntiTunnelCollider(atCols[aj]);
			}
		}
	}

	private void TryCreateReinforceJoints()
	{
		var settings = INUserSettings.Instance?.StarSeaSettings;
		float range = settings?.SpecialChargeReinforceRange ?? 5f;
		int maxCount = settings?.SpecialChargeReinforceMaxCount ?? 50;
		float reinforceBreak = settings?.SpecialChargeReinforceBreakForce ?? 1000f;
		if (reinforceBreak < 0f) reinforceBreak = float.PositiveInfinity;

		Vector3 myCenter = m_renderer != null ? m_renderer.bounds.center : transform.position;
		var allParts = Contraption.Instance?.Parts;
		if (allParts == null) return;

		// Track already-connected rigidbodies to avoid duplicates
		var connectedBodies = new HashSet<Rigidbody>();
		connectedBodies.Add(rigidbody);
		if (m_connectionPartner != null) connectedBodies.Add(m_connectionPartner.rigidbody);
		for (int i = 0; i < m_reinforceJoints.Count; i++)
		{
			if (m_reinforceJoints[i] != null && m_reinforceJoints[i].connectedBody != null)
				connectedBodies.Add(m_reinforceJoints[i].connectedBody);
		}

		// Collect candidates: frame first, boxing glove second, others last
		var candidates = new List<BasePart>();
		var frames = new List<BasePart>();
		var gloves = new List<BasePart>();
		for (int idx = 0; idx < allParts.Count; idx++)
		{
			var part = allParts[idx];
			if (part == this || part == m_connectionPartner) continue;
			if (part is PointChargePart) continue;
			if (part.ConnectedComponent != ConnectedComponent) continue;
			var partRb = part.GetComponent<Rigidbody>();
			if (partRb == null) continue;
			if (connectedBodies.Contains(partRb)) continue;
			float dist = Vector3.Distance(myCenter, part.transform.position);
			if (dist > range || dist < 0.01f) continue;
			if (part.m_partType == BasePart.PartType.WoodenFrame || part.m_partType == BasePart.PartType.MetalFrame)
				frames.Add(part);
			else if (part.m_partType == BasePart.PartType.SpringBoxingGlove)
				gloves.Add(part);
			else
				candidates.Add(part);
		}

		int created = 0;
		// Connect frame parts first
		foreach (var part in frames)
		{
			if (created >= maxCount) break;
			var fj = gameObject.AddComponent<FixedJoint>();
			fj.connectedBody = part.GetComponent<Rigidbody>();
			fj.breakForce = reinforceBreak;
			fj.breakTorque = float.PositiveInfinity;
			m_reinforceJoints.Add(fj);
			created++;
		}
		// Then boxing gloves
		foreach (var part in gloves)
		{
			if (created >= maxCount) break;
			var fj = gameObject.AddComponent<FixedJoint>();
			fj.connectedBody = part.GetComponent<Rigidbody>();
			fj.breakForce = reinforceBreak;
			fj.breakTorque = float.PositiveInfinity;
			m_reinforceJoints.Add(fj);
			created++;
		}
		// Then other parts
		foreach (var part in candidates)
		{
			if (created >= maxCount) break;
			var fj = gameObject.AddComponent<FixedJoint>();
			fj.connectedBody = part.GetComponent<Rigidbody>();
			fj.breakForce = reinforceBreak;
			fj.breakTorque = float.PositiveInfinity;
			m_reinforceJoints.Add(fj);
			created++;
		}
		if (created > 0)
		{
			Debug.Log($"[PointCharge] Reinforce: {created} joints (frames={frames.Count}, others={candidates.Count}, range={range}, max={maxCount})");
		}
	}

	private void SetupVehicleCollisionIgnore(PointChargePart other)
	{
		var allParts = Contraption.Instance?.Parts;
		if (allParts == null) return;

		// Collect ALL transitively connected ConnectedComponent values
		var group = new HashSet<int>();
		group.Add(ConnectedComponent);
		group.Add(other.ConnectedComponent);
		bool expanded = true;
		while (expanded)
		{
			expanded = false;
			for (int i = 0; i < allParts.Count; i++)
			{
				if (!(allParts[i] is PointChargePart charge)) continue;
				if (charge.m_connectionPartner == null) continue;
				int cc1 = charge.ConnectedComponent;
				int cc2 = charge.m_connectionPartner.ConnectedComponent;
				bool has1 = group.Contains(cc1);
				bool has2 = group.Contains(cc2);
				if (has1 && !has2) { group.Add(cc2); expanded = true; }
				else if (has2 && !has1) { group.Add(cc1); expanded = true; }
			}
		}
		// Ignore collision between ALL vehicles in the group
		// Use GetComponents to cover ALL colliders per part (not just first)
		var groupParts = new List<BasePart>();
		for (int i = 0; i < allParts.Count; i++)
		{
			if (group.Contains(allParts[i].ConnectedComponent))
				groupParts.Add(allParts[i]);
		}
		var bufa = new List<Collider>();
		var bufb = new List<Collider>();
		for (int i = 0; i < groupParts.Count; i++)
		{
			bufa.Clear();
			groupParts[i].GetComponentsInChildren<Collider>(bufa);
			for (int ci = 0; ci < bufa.Count; ci++)
			{
				if (bufa[ci] == null) continue;
				for (int j = i + 1; j < groupParts.Count; j++)
				{
					bufb.Clear();
					groupParts[j].GetComponentsInChildren<Collider>(bufb);
					for (int cj = 0; cj < bufb.Count; cj++)
					{
						if (bufb[cj] != null) GamePhysics.IgnoreCollision(bufa[ci], bufb[cj], true);
					}
				}
			}
		}
		// Also ignore circle collider with all group parts
		if (m_circleCollider != null)
		{
			foreach (var gp in groupParts)
			{
				bufa.Clear();
				gp.GetComponentsInChildren<Collider>(bufa);
				for (int ci = 0; ci < bufa.Count; ci++)
				{
					if (bufa[ci] != null) GamePhysics.IgnoreCollision(m_circleCollider, bufa[ci], true);
				}
			}
		}
	}

	private void OnDestroy()
	{
		DestroyConnection();
	}

	private static List<BasePart> FindConnectedParts(BasePart root)
	{
		var result = new List<BasePart>();
		var all = Contraption.Instance?.Parts;
		if (all == null) return result;
		int cc = root.ConnectedComponent;
		for (int i = 0; i < all.Count; i++)
		{
			if (all[i].ConnectedComponent == cc) result.Add(all[i]);
		}
		return result;
	}

	private void DestroyConnection()
	{
		// Anti-tunnel: remove colliders from both vehicles
		if (m_connectionPartner != null)
		{
			var antiTunnelParts = Contraption.Instance?.Parts;
			if (antiTunnelParts != null)
			{
				int atCC1 = ConnectedComponent;
				int atCC2 = m_connectionPartner.ConnectedComponent;
				for (int ai = 0; ai < antiTunnelParts.Count; ai++)
				{
					if (antiTunnelParts[ai].ConnectedComponent != atCC1 && antiTunnelParts[ai].ConnectedComponent != atCC2) continue;
					var atCols = antiTunnelParts[ai].GetComponentsInChildren<Collider>();
					for (int aj = 0; aj < atCols.Length; aj++)
						RemoveAntiTunnelCollider(atCols[aj]);
				}
			}
		}
		if (m_connectionPartner != null)
		{
			// Restore ring's original collider
			var ringOwnColliders = GetComponentsInChildren<Collider>();
			for (int i = 0; i < ringOwnColliders.Length; i++)
			{
				ringOwnColliders[i].enabled = true;
			}
			// Restore circle's original collider
			var circleOwnColliders = m_connectionPartner.GetComponentsInChildren<Collider>();
			for (int i = 0; i < circleOwnColliders.Length; i++)
			{
				circleOwnColliders[i].enabled = true;
			}
		}
		// Clean up reinforce joints
		for (int i = 0; i < m_reinforceJoints.Count; i++)
		{
			if (m_reinforceJoints[i] != null) Destroy(m_reinforceJoints[i]);
		}
		m_reinforceJoints.Clear();
		if (m_ringJoint != null)
		{
			Destroy(m_ringJoint);
			m_ringJoint = null;
		}
		if (m_circleCollider != null)
		{
			Destroy(m_circleCollider.gameObject);
			m_circleCollider = null;
		}
		if (m_connectionPartner != null)
		{
			m_connectionPartner.m_ringJoint = null;
			m_connectionPartner.m_circleCollider = null;
			m_connectionPartner.m_connectionPartner = null;
			m_connectionPartner.m_reinforceJoints.Clear();
		}
		m_connectionPartner = null;
	}

	private void SetupSpecialChargeCollisionIgnore()
	{
		if (!IsSpecialCharge) return;
		var myCollider = GetComponent<Collider>();
		if (myCollider == null) return;
		var allParts = Contraption.Instance?.Parts;
		if (allParts == null) return;
		for (int i = 0; i < allParts.Count; i++)
		{
			if (!(allParts[i] is PointChargePart other)) continue;
			if (other == this) continue;
			if (!other.IsSpecialCharge) continue;
			if (IsDeg90 == other.IsDeg90) continue;
			// Use GetComponents to cover ALL colliders
			s_colliderBufA.Clear();
			other.GetComponentsInChildren<Collider>(s_colliderBufA);
			for (int ci = 0; ci < s_colliderBufA.Count; ci++)
			{
				if (s_colliderBufA[ci] != null)
				{
					GamePhysics.IgnoreCollision(myCollider, s_colliderBufA[ci], true);
				}
			}
		}
	}

	public override void PostInitialize()
	{
		// 双重保险：确保点电荷永远不与部件自动连接（预制体为 None）
		m_jointConnectionDirection = BasePart.JointConnectionDirection.None;
		m_customJointConnectionDirection = BasePart.JointConnectionDirection.None;
		if (IsDeg270)
			m_enabled = INUserSettings.Instance?.StarSeaSettings?.SpecialChargeDefaultEnabled ?? false;
		else
			m_enabled = true;
		MakeInvincible();
	}

	protected override void OnTouch()
	{
		bool wasEnabled = m_enabled;
		m_enabled = !m_enabled;
		if (wasEnabled && !m_enabled)
		{
			// Turning off: if partner also off, mark both for rescan
			if (m_connectionPartner != null && !m_connectionPartner.m_enabled)
			{
				m_needsRescan = true;
				if (m_connectionPartner != null) m_connectionPartner.m_needsRescan = true;
			}
			// Save previous partner for potential reconnect
			m_previousPartner = m_connectionPartner;
		}
		else if (!wasEnabled && m_enabled)
		{
			// Turning on: try to reconnect to previous partner, but check distance first
			if (m_previousPartner != null
				&& m_previousPartner.m_enabled
				&& !m_previousPartner.HasAutoJoint
				&& m_previousPartner.m_connectionPartner == null)
			{
				float jointDist = INUserSettings.Instance?.StarSeaSettings?.SpecialChargeJointDistance ?? 0.5f;
				float dist = Vector3.Distance(
					m_renderer != null ? m_renderer.bounds.center : transform.position,
					m_previousPartner.m_renderer != null ? m_previousPartner.m_renderer.bounds.center : m_previousPartner.transform.position);
				if (dist <= jointDist)
				{
					TryCreateRotatableJoint(m_previousPartner);
				}
			}
			m_needsRescan = false;
		}
	}
}
