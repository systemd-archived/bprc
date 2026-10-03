using System;
using System.Collections.Generic;
using Innovation;
using UnityEngine;

public class INContraption : MonoBehaviour
{
	public class ComponentData
	{
		public Component[] ComponentList { get; set; }

		public float Time { get; set; }
		internal object Scratch;
		internal Contraption Owner;

		public ComponentData(Component[] components, float time)
		{
			ComponentList = components;
			Time = time;
		}
	}

	private bool m_initialized;

	private bool m_enabled;

	private bool m_running;

	private float m_startTime;

	private INBehaviour.StatusCode m_status;

	private List<INBehaviour> m_behaviours;

	private Dictionary<Type, ComponentData> m_componentListMap;

	private Dictionary<Rigidbody, INPhysicMaterial> m_materialMap;

	private Dictionary<Rigidbody, INBounds> m_boundsMap;

	private Dictionary<Rigidbody, (float, float)> m_dragMap;

	private Dictionary<Rigidbody, Vector3> m_previousPositionMap;

	private RaycastHit[] m_tunnelHits = new RaycastHit[32];

	private Rigidbody[] m_rigidbodies;

	private static float s_antiTunnelSpeedThreshold = 30f;

	private static float s_antiTunnelPullBackDistance = 3f;

	public static float AntiTunnelSpeedThreshold
	{
		get
		{
			return s_antiTunnelSpeedThreshold;
		}
		set
		{
			if (float.IsFinite(value) && value >= 0f)
			{
				s_antiTunnelSpeedThreshold = value;
			}
		}
	}

	public static float AntiTunnelPullBackDistance
	{
		get
		{
			return s_antiTunnelPullBackDistance;
		}
		set
		{
			if (float.IsFinite(value) && value >= 0f)
			{
				s_antiTunnelPullBackDistance = value;
			}
		}
	}

	public static INContraption Instance { get; private set; }

	public Rigidbody[] Rigidbodies
	{
		get
		{
			RefreshRigidbodies();
			return m_rigidbodies;
		}
	}

	public bool Enabled
	{
		get
		{
			return m_enabled;
		}
		set
		{
			m_enabled = value;
		}
	}

	public bool IsRunning
	{
		get
		{
			return m_running;
		}
		set
		{
			m_running = value;
		}
	}

	public static INContraption Create(Contraption contraption)
	{
		GameObject obj = contraption.gameObject;
		INContraption component = obj.GetComponent<INContraption>();
		if (component != null)
		{
			UnityEngine.Object.Destroy(component);
		}
		component = obj.AddComponent<INContraption>();
		component.m_enabled = WPFMonoBehaviour.levelManager != null;
		Instance = component;
		return component;
	}

	public void AddBehaviour(INBehaviour behaviour)
	{
		m_behaviours.Add(behaviour);
	}

	public IEnumerable<INBehaviour> GetBehaviours()
	{
		if (m_behaviours == null)
		{
			yield break;
		}
		foreach (INBehaviour behaviour in m_behaviours)
		{
			if ((behaviour.Status & m_status) != INBehaviour.StatusCode.None)
			{
				yield return behaviour;
			}
		}
	}

	public void OnInterfaceEnabled()
	{
		if (!m_running)
		{
			PropertyPanelBuilding.Instance?.OnDisable();
		}
		else
		{
			PropertyPanelRunning.Instance?.OnDisable();
		}
		bool active = false;
		IngameCamera ingameCamera = WPFMonoBehaviour.ingameCamera;
		LevelManager levelManager = WPFMonoBehaviour.levelManager;
		if (ingameCamera != null)
		{
			ingameCamera.enabled = active;
		}
		if (levelManager != null)
		{
			levelManager.InGameGUI.gameObject.SetActive(active);
		}
	}

	public void OnInterfaceDisabled()
	{
		if (!m_running)
		{
			PropertyPanelBuilding.Instance?.OnEnable();
		}
		else
		{
			PropertyPanelRunning.Instance?.OnEnable();
		}
		bool active = true;
		IngameCamera ingameCamera = WPFMonoBehaviour.ingameCamera;
		LevelManager levelManager = WPFMonoBehaviour.levelManager;
		if (ingameCamera != null)
		{
			ingameCamera.enabled = active;
		}
		if (levelManager != null)
		{
			levelManager.InGameGUI.gameObject.SetActive(active);
		}
	}

	public void Initialize()
	{
		if (!m_enabled)
		{
			return;
		}
		m_initialized = true;
		m_status = ((!m_running) ? INBehaviour.StatusCode.Building : INBehaviour.StatusCode.Running);
		m_behaviours = new List<INBehaviour>();
		if (INSettings.GetBool(INFeature.ColoredFrame))
		{
			PartManager.Create<ColoredFrameManager>();
		}
		if (!m_running)
		{
			if (INUserSettings.Instance.LevelSceneSettings.EnableCustomBackgroundColor)
			{
				GameObject gameObject = GameObject.FindGameObjectWithTag("MainCamera");
				if (gameObject != null)
				{
					Camera component = gameObject.GetComponent<Camera>();
					Color backgroundColor = (Color)INUserSettings.Instance.LevelSceneSettings.CustomBackgroundColor;
					component.backgroundColor = backgroundColor;
				}
			}
			if (INSettings.GetBool(INFeature.PropertyPanel) && INUserSettings.Instance.LevelSceneSettings.EnablePropertyPanel)
			{
				PropertyPanelBuilding.Create();
			}
			return;
		}
		m_startTime = Time.time;
		PartManager.Create<JointManager>();
		if (INSettings.GetBool(INFeature.UIPartButtonSystem) && UIPartButtonList.Enabled)
		{
			UIPartButtonList.RuntimeWrapper.Create();
		}
		if (INSettings.GetBool(INFeature.PropertyPanel) && INUserSettings.Instance.LevelSceneSettings.EnablePropertyPanel)
		{
			PropertyPanelRunning.Create();
		}
		if (INSettings.GetBool(INFeature.WaterSystem))
		{
			WaterSystem.Create();
		}
		if (INSettings.GetBool(INFeature.SeparatedFrame))
		{
			PartManager.Create<SeparatedFrameManager>();
		}
		if (INSettings.GetBool(INFeature.BracketFrame))
		{
			PartManager.Create<BracketFrameManager>();
		}
		if (INSettings.GetBool(INFeature.FrameJoint))
		{
			PartManager.Create<FrameJointManager>();
		}
		if (INSettings.GetBool(INFeature.FuelSystem))
		{
			PartManager.Create<FuelSystem>();
		}
		if (INSettings.GetBool(INFeature.PartGenerator))
		{
			PartManager.Create<PartGeneratorManager>();
		}
		if (INSettings.GetBool(INFeature.FixedPumpkin))
		{
			PartManager.Create<FixedPumpkinManager>();
		}
		if (INSettings.GetBool(INFeature.MarkerSeparator))
		{
			PartManager.Create<MarkerManager>();
		}
		if (INSettings.GetBool(INFeature.LightSystem))
		{
			PartManager.Create<EntityLightManager>();
		}
		if (INSettings.GetBool(INFeature.DecelerationLight))
		{
			PartManager.Create<DecelerationLightManager>();
		}
		if (INSettings.GetBool(INFeature.AutoControlLight))
		{
			PartManager.Create<AutoControlLightManager>();
		}
		if (INSettings.GetBool(INFeature.ElectricalSystem))
		{
			PartManager.Create<ElectricalSystem>();
		}
		if (INUserSettings.Instance?.StarSeaSettings?.UniversalGravity ?? false)
		{
			PartManager.Create<UniversalGravityManager>();
		}
		// 载具部件碰撞弹性（默认 0 = 原版；调大后更弹）
		PartManager.Create<CollisionBouncinessManager>();
	}

	public void PostInitialize()
	{
		if (m_enabled)
		{
			m_materialMap = new Dictionary<Rigidbody, INPhysicMaterial>();
			m_boundsMap = new Dictionary<Rigidbody, INBounds>();
			Rigidbody[] components = GetComponents<Rigidbody>();
			foreach (Rigidbody rigidbody in components)
			{
				GetMaterial(rigidbody);
				GetBounds(rigidbody);
			}
			if (INSettings.GetBool(INFeature.HingePlate))
			{
				HingePlate.InitializeStatic();
			}
		}
	}

	private void Start()
	{
		if (!m_enabled)
		{
			return;
		}
		if (!m_initialized)
		{
			Initialize();
		}
		StartSelf();
		foreach (INBehaviour behaviour in GetBehaviours())
		{
			behaviour.Start();
		}
	}

	private void StartSelf()
	{
		GameObject[] array = GameObject.FindGameObjectsWithTag("Ground");
		foreach (GameObject gameObject in array)
		{
			if (gameObject.name == "GroundCollider")
			{
				gameObject.layer = LayerMask.NameToLayer("Ground");
			}
		}
	}

	private void OnEnable()
	{
		if (!m_enabled)
		{
			return;
		}
		foreach (INBehaviour behaviour in GetBehaviours())
		{
			behaviour.OnEnable();
		}
	}

	private void OnDisable()
	{
		if (!m_enabled)
		{
			return;
		}
		foreach (INBehaviour behaviour in GetBehaviours())
		{
			behaviour.OnDisable();
		}
	}

	private void RefreshRigidbodies()
	{
		Rigidbody[] components = GetComponents<Rigidbody>();
		m_rigidbodies = components;
	}

	private void FixedUpdate()
	{
		if (!m_enabled)
		{
			return;
		}
		FixedUpdateSelf();
		for (int i = 0; m_behaviours != null && i < m_behaviours.Count; i++)
		{
			INBehaviour behaviour = m_behaviours[i];
			if ((behaviour.Status & m_status) != INBehaviour.StatusCode.None) behaviour.FixedUpdate();
		}
	}

	private void FixedUpdateSelf()
	{
		if (!m_running)
		{
			return;
		}
		AntiTunnelCheck();
		if (INSettings.GetBool(INFeature.NoDrag))
		{
			if (m_dragMap == null)
			{
				m_dragMap = new Dictionary<Rigidbody, (float, float)>();
			}
			Rigidbody[] components = GetComponents<Rigidbody>();
			foreach (Rigidbody rigidbody in components)
			{
				m_dragMap.TryAdd(rigidbody, (rigidbody.drag, rigidbody.angularDrag));
				rigidbody.drag = 0f;
				rigidbody.angularDrag = 0f;
			}
		}
		else
		{
			if (m_dragMap == null || m_dragMap.Count <= 0)
			{
				return;
			}
			foreach (KeyValuePair<Rigidbody, (float, float)> item in m_dragMap)
			{
				Rigidbody key = item.Key;
				if (key != null)
				{
					key.drag = item.Value.Item1;
					key.angularDrag = item.Value.Item2;
				}
			}
			m_dragMap.Clear();
		}
	}

	private void AntiTunnelCheck()
	{
		if (AntiTunnelSpeedThreshold <= 0f)
		{
			return;
		}
		if (m_previousPositionMap == null)
		{
			m_previousPositionMap = new Dictionary<Rigidbody, Vector3>();
		}
		Rigidbody[] components = Rigidbodies;
		foreach (Rigidbody rigidbody in components)
		{
			if (rigidbody == null || rigidbody.isKinematic)
			{
				continue;
			}
			BasePart selfPart = rigidbody.GetComponentInParent<BasePart>();
			if (m_previousPositionMap.TryGetValue(rigidbody, out var previousPosition))
			{
				Vector3 vector = rigidbody.position - previousPosition;
				float magnitude = vector.magnitude;
				if (magnitude > 0.0001f)
				{
					Vector3 normalized = vector / magnitude;
					int hitCount = GetTunnelHits(previousPosition, normalized, magnitude);
					float nearestDistance = float.PositiveInfinity;
					Vector3 nearestPoint = default(Vector3);
					for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
					{
						RaycastHit raycastHit = m_tunnelHits[hitIndex];
						if (raycastHit.distance >= nearestDistance) continue;
						if (raycastHit.collider == null || raycastHit.collider.isTrigger)
						{
							if (!EntityLight.ShouldBlockTunnel(raycastHit.collider))
							{
								continue;
							}
						}
						// Skip colliders belonging to connected special-charge vehicles
						if (PointChargePart.AntiTunnelIgnoreColliders.Contains(raycastHit.collider))
						{
							continue;
						}
						// Also skip the vehicles this one is allowed to phase through: a marker-separator
						// vehicle ignores contacts but not raycasts, so without this the body is teleported
						// back to the hull it just passed through (position write below) and the joints turn
						// that into the up-and-back tilt reported for overlapping same-faction vehicles.
						if (selfPart != null && MarkerManager.IsPhasedVehicle(selfPart, raycastHit.collider))
						{
							continue;
						}
						Rigidbody hitBody = raycastHit.collider.attachedRigidbody;
						if (hitBody == null || hitBody == rigidbody)
						{
							continue;
						}
						float relativeSpeed = (rigidbody.velocity - hitBody.velocity).magnitude;
						if (relativeSpeed < AntiTunnelSpeedThreshold)
						{
							continue;
						}
						if (AntiTunnelPullBackDistance > 0f)
						{
							float penetration = magnitude - raycastHit.distance;
							if (penetration < AntiTunnelPullBackDistance)
							{
								continue;
							}
						}
						nearestDistance = raycastHit.distance;
						nearestPoint = raycastHit.point;
					}
					if (!float.IsPositiveInfinity(nearestDistance))
						rigidbody.position = nearestPoint - normalized * 0.01f;
				}
			}
			m_previousPositionMap[rigidbody] = rigidbody.position;
		}
		if (m_previousPositionMap.Count > components.Length)
		{
			HashSet<Rigidbody> hashSet = new HashSet<Rigidbody>(components);
			List<Rigidbody> list = null;
			foreach (Rigidbody key in m_previousPositionMap.Keys)
			{
				if (!hashSet.Contains(key))
				{
					(list ?? (list = new List<Rigidbody>())).Add(key);
				}
			}
			if (list != null)
			{
				foreach (Rigidbody item in list)
				{
					m_previousPositionMap.Remove(item);
				}
			}
		}
	}

	private void Update()
	{
		if (!m_enabled)
		{
			return;
		}
		for (int i = 0; m_behaviours != null && i < m_behaviours.Count; i++)
		{
			INBehaviour behaviour = m_behaviours[i];
			if ((behaviour.Status & m_status) != INBehaviour.StatusCode.None) behaviour.Update();
		}
	}

	private int GetTunnelHits(Vector3 origin, Vector3 direction, float distance)
	{
		while (true)
		{
			int count = Physics.RaycastNonAlloc(origin, direction, m_tunnelHits, distance);
			if (count < m_tunnelHits.Length) return count;
			// A full NonAlloc buffer is an arbitrary subset, not necessarily the nearest hits.
			// Grow and retry rather than silently dropping obstacles in dense contraptions.
			Array.Resize(ref m_tunnelHits, checked(m_tunnelHits.Length * 2));
		}
	}

	private void LateUpdate()
	{
		if (!m_enabled)
		{
			return;
		}
		for (int i = 0; m_behaviours != null && i < m_behaviours.Count; i++)
		{
			INBehaviour behaviour = m_behaviours[i];
			if ((behaviour.Status & m_status) != INBehaviour.StatusCode.None) behaviour.LateUpdate();
		}
	}

	private void OnDestroy()
	{
		if (Instance == this)
		{
			Instance = null;
		}
		// Registered behaviours own subscriptions and GPU/native resources even while disabled.
		// Destruction must not filter them by the current building/running status.
		if (m_behaviours == null) return;
		foreach (INBehaviour behaviour in m_behaviours)
		{
			try { behaviour.OnDestroy(); }
			catch (Exception error) { Debug.LogException(error); }
		}
		m_behaviours.Clear();
	}

	public float GetTime()
	{
		return Time.time - m_startTime;
	}

	public new T[] GetComponents<T>() where T : Component
	{
		return GetComponents<T>(Contraption.Instance);
	}

	private T[] GetComponents<T>(Contraption instance) where T : Component
	{
		if (m_componentListMap == null)
		{
			m_componentListMap = new Dictionary<Type, ComponentData>();
		}
		if (instance == null) return Array.Empty<T>();
		if (!m_componentListMap.TryGetValue(typeof(T), out var value))
		{
			value = new ComponentData(Array.Empty<T>(), -1f) { Scratch = new List<T>() };
			m_componentListMap.Add(typeof(T), value);
		}
		if (value.Owner != instance || value.Time < Time.fixedTime)
		{
			var scratch = (List<T>)value.Scratch;
			instance.GetComponentsInChildren(false, scratch);
			T[] previous = (T[])value.ComponentList;
			bool changed = value.Owner != instance || previous.Length != scratch.Count;
			for (int i = 0; !changed && i < scratch.Count; i++) changed = !ReferenceEquals(previous[i], scratch[i]);
			if (changed) value.ComponentList = scratch.ToArray();
			value.Owner = instance;
			value.Time = Time.fixedTime + Time.fixedDeltaTime * 0.5f;
		}
		return (T[])value.ComponentList;
	}

	public static INPhysicMaterial GetMaterial(Rigidbody rigidbody)
	{
		if (Instance.m_materialMap.TryGetValue(rigidbody, out var value))
		{
			return value;
		}
		Collider[] componentsInChildren = rigidbody.GetComponentsInChildren<Collider>();
		foreach (Collider collider in componentsInChildren)
		{
			INPhysicMaterial other = new INPhysicMaterial(collider.material);
			value.AddMaterial(other);
		}
		return Instance.m_materialMap[rigidbody] = value;
	}

	public static INBounds GetBounds(Rigidbody rigidbody)
	{
		if (Instance.m_boundsMap.TryGetValue(rigidbody, out var value))
		{
			return value;
		}
		Collider[] componentsInChildren = rigidbody.GetComponentsInChildren<Collider>();
		float num = 0f;
		float num2 = 0f;
		float num3 = 0f;
		float num4 = 0f;
		value.Type = 0;
		Collider[] array = componentsInChildren;
		foreach (Collider collider in array)
		{
			if (collider.enabled && !collider.isTrigger)
			{
				Vector2 vector = default(Vector2);
				Vector2 vector2 = default(Vector2);
				if (collider is BoxCollider boxCollider)
				{
					vector = boxCollider.center;
					vector2 = new Vector2(boxCollider.size.x * 0.5f, boxCollider.size.y * 0.5f);
				}
				else if (collider is CapsuleCollider capsuleCollider)
				{
					vector = capsuleCollider.center;
					vector2 = new Vector2(capsuleCollider.radius, capsuleCollider.height * 0.5f);
				}
				else if (collider is SphereCollider sphereCollider)
				{
					vector = sphereCollider.center;
					vector2 = new Vector2(sphereCollider.radius, sphereCollider.radius);
					value.Type = 1;
				}
				Transform parent = collider.transform;
				while (parent != null && parent != rigidbody.transform)
				{
					Vector3 localPosition = parent.localPosition;
					Vector3 vector3 = parent.localRotation * Vector3.right;
					float num5 = vector3.x * vector.x - vector3.y * vector.y;
					float num6 = vector3.x * vector.y + vector3.y * vector.x;
					vector.x = localPosition.x + num5;
					vector.y = localPosition.y + num6;
					parent = parent.parent;
				}
				if (vector.x - vector2.x < num)
				{
					num = vector.x - vector2.x;
				}
				if (vector.x + vector2.x > num2)
				{
					num2 = vector.x + vector2.x;
				}
				if (vector.y - vector2.y < num3)
				{
					num3 = vector.y - vector2.y;
				}
				if (vector.y + vector2.y > num4)
				{
					num4 = vector.y + vector2.y;
				}
			}
		}
		value.X = (num + num2) * 0.5f;
		value.Y = (num3 + num4) * 0.5f;
		value.A = (num2 - num) * 0.5f;
		value.B = (num4 - num3) * 0.5f;
		value.R = Mathf.Sqrt(value.A * value.A + value.B * value.B);
		if (value.Type == 1)
		{
			value.R = ((value.A > value.B) ? value.A : value.B);
			value.A = value.R;
			value.B = value.R;
		}
		return Instance.m_boundsMap[rigidbody] = value;
	}

	public static List<BasePart> GetAllParts()
	{
		return (WPFMonoBehaviour.levelManager?.ContraptionProto)?.Parts;
	}

	public static List<BasePart> GetAllRuntimeParts()
	{
		return (WPFMonoBehaviour.levelManager?.ContraptionRunning)?.Parts;
	}

	public static BasePart FindPart(BasePart.PartType partType)
	{
		List<BasePart> parts = Contraption.Instance.Parts;
		for (int num = parts.Count - 1; num >= 0; num--)
		{
			BasePart basePart = parts[num];
			if (partType == (BasePart.PartType)(-1) || basePart.Type == partType)
			{
				return basePart;
			}
		}
		return null;
	}

	public static BasePart SelectPart(int x, int y, SortedPartType partType, int partIndex)
	{
		List<BasePart> list = SelectParts(x, y, 1, 1, partType, partIndex);
		if (list.Count <= 0)
		{
			return null;
		}
		return list[0];
	}

	public static List<BasePart> SelectParts(int x, int y, int width, int height, SortedPartType partType, int partIndex)
	{
		List<BasePart> list = new List<BasePart>();
		BasePart.PartType partType2 = partType.ToPartType();
		foreach (BasePart part in WPFMonoBehaviour.levelManager.ContraptionProto.Parts)
		{
			int coordX = part.CoordX;
			int coordY = part.CoordY;
			if (ContraptionLayers.Editable(part) && coordX >= x && coordX <= x + width - 1 && coordY >= y && coordY <= y + height - 1 && (partType == SortedPartType.ALL || part.Type == partType2) && (partIndex == -1 || part.Index == partIndex))
			{
				list.Add(part);
			}
		}
		return list;
	}

	public static List<BasePart> InvertSelection(IReadOnlyList<BasePart> parts)
	{
		Contraption contraptionProto = WPFMonoBehaviour.levelManager.ContraptionProto;
		HashSet<BasePart> hashSet = new HashSet<BasePart>(parts);
		List<BasePart> list = new List<BasePart>();
		foreach (BasePart part in contraptionProto.Parts)
		{
			if (ContraptionLayers.Editable(part) && !hashSet.Contains(part))
			{
				list.Add(part);
			}
		}
		return list;
	}

	private static bool SetPart(int x, int y, int rotation, bool flipped, BasePart part, out BasePart newPart)
	{
		bool num = SetPart(x, y, part, out newPart);
		if (num)
		{
			if (flipped)
			{
				newPart.SetFlipped(flipped: true);
			}
			newPart.SetRotation((BasePart.GridRotation)rotation);
		}
		return num;
	}

	private static bool SetPart(int x, int y, BasePart part, out BasePart newPart)
	{
		LevelManager levelManager = WPFMonoBehaviour.levelManager;
		if (levelManager.CanPlacePartAtGridCell(x, y) && levelManager.ContraptionProto.CanPlaceSpecificPartAt(x, y, part))
		{
			ConstructionUI constructionUI = levelManager.ConstructionUI;
			ConstructionUI.PartDesc partDesc = constructionUI.FindPartDesc(part.Type, part.customPartIndex);
			partDesc.useCount++;
			EventManager.Send(new PartCountChanged(partDesc.part.Type, partDesc.CurrentCount, partDesc.part.customPartIndex));
			newPart = constructionUI.SetPartAt(x, y, part);
			constructionUI.ContraptionPartChanged(x, y);
			newPart.OnPartPlaced();
			EventManager.Send(new PartPlacedEvent(partDesc.part.Type, partDesc.part.Tier, newPart.transform.position));
			return true;
		}
		newPart = null;
		return false;
	}

	public static BasePart SetPart(int x, int y, SortedPartType partType, int customIndex)
	{
		BasePart customPart = WPFMonoBehaviour.gameData.GetCustomPart(partType.ToPartType(), customIndex);
		SetPart(x, y, customPart, out var newPart);
		return newPart;
	}

	public static List<BasePart> SetParts(int x, int y, int width, int height, SortedPartType partType, int customIndex)
	{
		List<BasePart> list = new List<BasePart>();
		BasePart customPart = WPFMonoBehaviour.gameData.GetCustomPart(partType.ToPartType(), customIndex);
		for (int i = 0; i < width; i++)
		{
			for (int j = 0; j < height; j++)
			{
				if (SetPart(x + i, y + j, customPart, out var newPart))
				{
					list.Add(newPart);
				}
			}
		}
		return list;
	}

	public static List<BasePart> SetPartsInterval(int x, int y, int width, int height, int deltaX, int deltaY, SortedPartType partType, int customIndex)
	{
		if (deltaX <= 0)
		{
			throw new ArgumentOutOfRangeException("deltaX");
		}
		if (deltaY <= 0)
		{
			throw new ArgumentOutOfRangeException("deltaY");
		}
		List<BasePart> list = new List<BasePart>();
		BasePart customPart = WPFMonoBehaviour.gameData.GetCustomPart(partType.ToPartType(), customIndex);
		for (int i = 0; i < width; i += deltaX)
		{
			for (int j = 0; j < height; j += deltaY)
			{
				if (SetPart(x + i, y + j, customPart, out var newPart))
				{
					list.Add(newPart);
				}
			}
		}
		return list;
	}

	public static void MovePart(int x, int y, BasePart part)
	{
		part.CoordX = x;
		part.CoordY = y;
		float num = (float)(-(x + 2 * y)) / 100000f;
		part.transform.localPosition = new Vector3(x, y, -0.1f + part.m_ZOffset + num);
	}

	public static void MoveParts(IReadOnlyList<BasePart> parts, int x, int y, out int count)
	{
		count = 0;
		foreach (BasePart part in parts)
		{
			if (part != null)
			{
				count++;
				MovePart(part.CoordX + x, part.CoordY + y, part);
			}
		}
	}

	public static void RotateParts(IReadOnlyList<BasePart> parts, int times, out int count)
	{
		count = 0;
		Contraption contraptionProto = WPFMonoBehaviour.levelManager.ContraptionProto;
		foreach (BasePart part in parts)
		{
			if (part != null)
			{
				count++;
				for (int i = 0; i < times; i++)
				{
					contraptionProto.Flip(part);
				}
			}
		}
	}

	public static List<BasePart> CopyParts(IReadOnlyList<BasePart> parts, int x, int y, out int count)
	{
		count = 0;
		List<BasePart> list = new List<BasePart>();
		foreach (BasePart part in parts)
		{
			if (part != null)
			{
				BasePart customPart = WPFMonoBehaviour.gameData.GetCustomPart(part.Type, part.Index);
				if (SetPart(part.CoordX + x, part.CoordY + y, (int)part.Rotation, part.Flipped, customPart, out var newPart))
				{
					// 复制出来的部件保留源部件的自定义参数（复制器）
					if (newPart != null)
					{
						newPart.CopyCustomParamsFrom(part);
					}
					Debug.Log("[PARAMDIAG] CopyParts src=" + part.Type + "#" + part.Index + " params='" + part.SerializeCustomParams() + "' -> new='" + (newPart != null ? newPart.SerializeCustomParams() : "null") + "'");
					count++;
					list.Add(newPart);
				}
			}
		}
		return list;
	}

	public static List<BasePart> ReplaceParts(IReadOnlyList<BasePart> parts, SortedPartType partType, int partIndex, out int count)
	{
		count = 0;
		List<BasePart> list = new List<BasePart>();
		BasePart customPart = WPFMonoBehaviour.gameData.GetCustomPart(partType.ToPartType(), partIndex);
		foreach (BasePart part in parts)
		{
			if (part != null && SetPart(part.CoordX, part.CoordY, customPart, out var newPart))
			{
				count++;
				list.Add(newPart);
			}
		}
		return list;
	}

	public static bool RemovePart(int x, int y)
	{
		LevelManager levelManager = WPFMonoBehaviour.levelManager;
		ConstructionUI constructionUI = levelManager.ConstructionUI;
		BasePart basePart = levelManager.ContraptionProto.FindPartAt(x, y);
		if (basePart != null && basePart.enclosedPart != null) basePart = basePart.enclosedPart;
		return constructionUI != null && constructionUI.RemoveBuildPart(basePart);
	}

	public static void RemoveParts(IReadOnlyList<BasePart> parts, out int count)
	{
		count = 0;
		ConstructionUI constructionUI = WPFMonoBehaviour.levelManager.ConstructionUI;
		if (constructionUI == null) return;
		// The input may be the live Parts list, or contain both an outer frame and its inner part.
		foreach (BasePart part in new List<BasePart>(parts))
		{
			if (constructionUI.RemoveBuildPart(part))
			{
				count++;
			}
		}
	}

	public static BasePart SetRuntimePart(float x, float y, int coordX, int coordY, int rotation, bool flipped, SortedPartType partType, int customIndex)
	{
		return SetRuntimePartInternal(WPFMonoBehaviour.levelManager.ContraptionRunning.transform.position + new Vector3(x, y), new Vector2Int(coordX, coordY), (BasePart.GridRotation)rotation, flipped, partType.ToPartType(), customIndex);
	}

	public static BasePart SetRuntimePartInternal(Vector3 position, Vector2Int coord, BasePart.GridRotation gridRotation, bool flipped, BasePart.PartType partType, int customIndex)
	{
		Contraption contraptionRunning = WPFMonoBehaviour.levelManager.ContraptionRunning;
		BasePart basePart = UnityEngine.Object.Instantiate(WPFMonoBehaviour.gameData.GetCustomPart(partType, customIndex));
		basePart.transform.position = position;
		basePart.transform.parent = contraptionRunning.transform;
		basePart.CoordX = coord.x;
		basePart.CoordY = coord.y;
		basePart.SetRotation(gridRotation);
		if (flipped)
		{
			basePart.SetFlipped(flipped);
		}
		basePart.contraption = contraptionRunning;
		basePart.gameObject.SetActive(value: true);
		basePart.enabled = true;
		basePart.PrePlaced();
		basePart.ConnectedComponent = -1;
		basePart.gameObject.tag = "Contraption";
		for (int i = 0; i < basePart.transform.childCount; i++)
		{
			basePart.transform.GetChild(i).gameObject.tag = "Contraption";
		}
		basePart.ChangeVisualConnections();
		basePart.EnsureRigidbody();
		basePart.rigidbody.position = position;
		contraptionRunning.AddRuntimePart(basePart);
		basePart.Initialize();
		basePart.PostInitialize();
		contraptionRunning.UpdateConnectedComponents();
		return basePart;
	}

	public static List<BasePart> SetRuntimeParts(string path)
	{
		return SetRuntimeParts(INContraptionDataManager.Instance.Load(INAddonManager.DataPath + "/" + path));
	}

	public static List<BasePart> SetRuntimeParts(IContraptionData contraptionData)
	{
		List<BasePart> list = new List<BasePart>(contraptionData.Units.Count);
		foreach (IContraptionDataUnit unit in contraptionData.Units)
		{
			BasePart item = SetRuntimePart(unit.X, unit.Y, unit.X, unit.Y, unit.Rotation, unit.Flipped, (SortedPartType)unit.Type, unit.Index);
			string customParams = UnitCustomParams(unit);
			if (item != null && !string.IsNullOrEmpty(customParams))
			{
				item.DeserializeCustomParams(customParams);
			}
			list.Add(item);
		}
		return list;
	}

	public static string GetContraptionName()
	{
		return WPFMonoBehaviour.levelManager.CurrentGameMode.GetCurrentContraptionName();
	}

	public static void SaveContraption()
	{
		GameMode currentGameMode = WPFMonoBehaviour.levelManager.CurrentGameMode;
		string currentContraptionName = currentGameMode.GetCurrentContraptionName();
		currentGameMode.ContraptionProto.CreateAndSaveContraption(currentContraptionName);
	}

	public static void MoveContraption(int x, int y)
	{
		foreach (BasePart part in WPFMonoBehaviour.levelManager.CurrentGameMode.ContraptionProto.Parts)
		{
			MovePart(part.CoordX + x, part.CoordY + y, part);
		}
	}

	// 读取单位上的每部件自定义参数（接口不含该字段，故按具体实现类取）。
	private static string UnitCustomParams(IContraptionDataUnit unit)
	{
		INContraptionData.Unit inUnit = unit as INContraptionData.Unit;
		if (inUnit != null)
		{
			return inUnit.CustomParams;
		}
		ContraptionDataset.ContraptionDatasetUnit cdsUnit = unit as ContraptionDataset.ContraptionDatasetUnit;
		if (cdsUnit != null)
		{
			return cdsUnit.customParams;
		}
		return null;
	}

	public static INContraptionData CopyContraption()
	{
		List<BasePart> parts = WPFMonoBehaviour.levelManager.CurrentGameMode.ContraptionProto.Parts;
		INContraptionData iNContraptionData = new INContraptionData(parts.Count);
		foreach (BasePart item in parts)
		{
			INContraptionData.Unit unit = new INContraptionData.Unit((int)item.Type.ToSortedPartType(), item.Index, item.CoordX, item.CoordY, (int)item.Rotation, item.Flipped);
			unit.CustomParams = item.SerializeCustomParams();
			iNContraptionData.Units.Add(unit);
		}
		return iNContraptionData;
	}

	public static void PasteContraption(IContraptionData data, int x, int y, bool absolute)
	{
		int count = data.Units.Count;
		if (count == 0)
		{
			return;
		}
		if (absolute)
		{
			long num = 0L;
			long num2 = 0L;
			foreach (IContraptionDataUnit unit in data.Units)
			{
				num += unit.X;
				num2 += unit.Y;
			}
			x -= (int)(num / count);
			y -= (int)(num2 / count);
		}
		BuildContraption(data, x, y);
	}

	private static void BuildContraption(IContraptionData data, int x, int y)
	{
		foreach (IContraptionDataUnit unit in data.Units)
		{
			SortedPartType type = (SortedPartType)unit.Type;
			ConstructionUI.PartDesc partDesc = WPFMonoBehaviour.levelManager.ConstructionUI.FindPartDesc(type.ToPartType(), unit.Index);
			if (partDesc != null)
			{
				BasePart customPart = WPFMonoBehaviour.gameData.GetCustomPart(partDesc.part.m_partType, unit.Index);
				if (customPart != null)
				{
					string customParams = UnitCustomParams(unit);
					BasePart built = WPFMonoBehaviour.levelManager.BuildPart(unit.X + x, unit.Y + y, unit.Rotation, unit.Flipped, customPart, customParams);
					partDesc.useCount++;
					Debug.Log("[PARAMDIAG] BuildContraption unit params='" + customParams + "'");
				}
			}
		}
	}
}
