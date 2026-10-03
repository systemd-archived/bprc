using System.Collections.Generic;
using UnityEngine;

public class MarkerManager : PartManager
{
	private int[] m_indexes;
	private Contraption m_owner;
	private float m_nextRefresh;
	private readonly Dictionary<(Collider, Collider), bool> m_collisionPairs = new Dictionary<(Collider, Collider), bool>();
	private readonly List<(Collider collider, int component, int team)> m_entries = new List<(Collider, int, int)>();
	private readonly HashSet<Collider> m_seen = new HashSet<Collider>();
	private readonly HashSet<(Collider, Collider)> m_wantedPairs = new HashSet<(Collider, Collider)>();
	private readonly List<(Collider, Collider)> m_removedPairs = new List<(Collider, Collider)>();
	private readonly List<Collider> m_colliderScratch = new List<Collider>();

	public override StatusCode Status => StatusCode.Running;

	public static MarkerManager Instance { get; private set; }

	public static bool IsInstantiated { get; private set; }

	protected override void Initialize()
	{
		base.Initialize();
		Instance = this;
		IsInstantiated = true;
		m_owner = Contraption.Instance;
		m_owner.ConnectedComponentsChanged += OnConnectedComponentsChanged;
		OnConnectedComponentsChanged();
	}

	public override void OnDestroy()
	{
		if (m_owner != null) m_owner.ConnectedComponentsChanged -= OnConnectedComponentsChanged;
		foreach (var pair in m_collisionPairs)
			if (!pair.Value && pair.Key.Item1 != null && pair.Key.Item2 != null)
				GamePhysics.IgnoreCollision(pair.Key.Item1, pair.Key.Item2, false);
		m_collisionPairs.Clear();
		if (Instance == this)
		{
			Instance = null;
			IsInstantiated = false;
		}
	}

	public int GetTeamIndex(int connectedComponent)
	{
		return m_indexes != null && connectedComponent >= 0 && connectedComponent < m_indexes.Length ? m_indexes[connectedComponent] : 0;
	}

	public int GetTeamIndex(BasePart part)
	{
		return part == null ? 0 : GetTeamIndex(part.ConnectedComponent);
	}

	public bool IsInSameTeam(BasePart partA, BasePart partB)
	{
		int connectedComponent = partA.ConnectedComponent;
		int connectedComponent2 = partB.ConnectedComponent;
		if (connectedComponent == connectedComponent2)
		{
			return true;
		}
		int teamIndex = GetTeamIndex(connectedComponent);
		int teamIndex2 = GetTeamIndex(connectedComponent2);
		return (teamIndex & teamIndex2) != 0;
	}

	private void OnConnectedComponentsChanged()
	{
		if (m_owner == null) return;
		if (m_indexes == null || m_indexes.Length != m_owner.ConnectedComponentCount)
			m_indexes = new int[m_owner.ConnectedComponentCount];
		else
			System.Array.Clear(m_indexes, 0, m_indexes.Length);
		foreach (BasePart part in m_owner.Parts)
		{
			if (part != null && part.IsMarker() && part.ConnectedComponent >= 0 && part.ConnectedComponent < m_indexes.Length)
			{
				int num = part.ConnectedComponent;
				if (num >= 0 && num < m_indexes.Length)
				{
					m_indexes[num] |= 1 << (int)part.m_gridRotation;
				}
			}
		}
	}

	public override void FixedUpdate()
	{
		if (m_owner == null || Time.time < m_nextRefresh) return;
		m_nextRefresh = Time.time + 0.25f;
		OnConnectedComponentsChanged();
		var entries = m_entries;
		var seen = m_seen;
		entries.Clear();
		seen.Clear();
		foreach (BasePart part in m_owner.Parts)
		{
			if (part == null) continue;
			int team = GetTeamIndex(part);
			if (team == 0) continue;
			m_colliderScratch.Clear();
			part.GetComponentsInChildren(true, m_colliderScratch);
			foreach (Collider collider in m_colliderScratch)
				if (collider != null && !collider.isTrigger && seen.Add(collider))
					entries.Add((collider, part.ConnectedComponent, team));
		}
		var wanted = m_wantedPairs;
		wanted.Clear();
		for (int i = 0; i < entries.Count; i++)
		for (int j = i + 1; j < entries.Count; j++)
		{
			var a = entries[i]; var b = entries[j];
			if (a.component == b.component || (a.team & b.team) == 0) continue;
			var key = a.collider.GetInstanceID() < b.collider.GetInstanceID()
				? (a.collider, b.collider) : (b.collider, a.collider);
			wanted.Add(key);
			if (!m_collisionPairs.ContainsKey(key))
				m_collisionPairs.Add(key, Physics.GetIgnoreCollision(key.Item1, key.Item2));
			GamePhysics.IgnoreCollision(key.Item1, key.Item2, true);
		}
		var removed = m_removedPairs;
		removed.Clear();
		foreach (var pair in m_collisionPairs)
		{
			if (wanted.Contains(pair.Key)) continue;
			if (!pair.Value && pair.Key.Item1 != null && pair.Key.Item2 != null)
				GamePhysics.IgnoreCollision(pair.Key.Item1, pair.Key.Item2, false);
			removed.Add(pair.Key);
		}
		foreach (var key in removed) m_collisionPairs.Remove(key);
	}

	public static bool IsInSameTeamStatic(BasePart partA, BasePart partB)
	{
		if (IsInstantiated)
		{
			return Instance.IsInSameTeam(partA, partB);
		}
		return partA.ConnectedComponent == partB.ConnectedComponent;
	}

	/// <summary>
	/// True when <paramref name="hit"/> belongs to another vehicle that is phased through
	/// <paramref name="part"/>'s vehicle: a different connected component whose collider pair is
	/// already told to ignore collisions, or a marked vehicle of the same team. Contacts between such
	/// vehicles are suppressed, but raycasts still hit them — a query that treats a hit as ground,
	/// as a tunnel wall or as a light blocker would push the two hulls apart or yank one back, which
	/// is what makes an overlapping same-faction vehicle tilt or jump.
	/// </summary>
	public static bool IsPhasedVehicle(BasePart part, Collider hit)
	{
		if (part == null || hit == null)
		{
			return false;
		}
		BasePart other = hit.GetComponentInParent<BasePart>();
		if (other == null || other == part || other.ConnectedComponent == part.ConnectedComponent)
		{
			return false;
		}
		return GamePhysics.IsIgnoredPair(part.collider, hit) || IsInSameTeamStatic(part, other);
	}
}
