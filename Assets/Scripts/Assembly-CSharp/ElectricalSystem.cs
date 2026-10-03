using System;
using System.Collections.Generic;
using UnityEngine;

public class ElectricalSystem : PartManager
{
	private List<ElectricalPart> m_electricalParts;

	private List<CircuitElement> m_electricalElements;

	private List<InterfacePart> m_interfaces;

	private CircuitSimulator m_circuitSimulator;

	private bool m_simulatorOptimized;

	private float m_accumulator;

	private static int s_circuitFrameRate = 50;

	private static bool s_enableMultithreadCircuit;

	private static bool s_useOptimizedCircuit = true;

	// 优化路径复用的缓冲（避免每帧分配）
	private Dictionary<int, List<PowerTransmitterPart>> m_transmitterByChannel;

	private List<PowerTransmitterPart> m_transmitterReceivers;

	private List<PointChargePart> m_chargeParts;
	private readonly GpuPhysicsForces m_chargeForces = new GpuPhysicsForces(GpuPhysicsForces.ForceKind.PointCharge);
	private Rigidbody[] m_chargeBodies = Array.Empty<Rigidbody>();

	private List<ElectricFieldGenerator> m_electricFieldGenerators;

	private List<MagneticFieldGenerator> m_magneticFieldGenerators;

	private List<WirePartBase> m_magneticWires;

	private List<Collider> m_colliderBufferA;

	private List<Collider> m_colliderBufferB;

	private static HashSet<long> s_ignoredColliderPairs = new HashSet<long>();

	// 斜向发送器机制复用缓冲（仅当存在斜向发送器时才用到）
	private List<PowerTransmitterPart> m_diagonalSenders;

	private List<Electrode> m_equipElectrodes;

	private Dictionary<Electrode, int> m_equipIndex;

	private List<int> m_equipParent;

	private Dictionary<PowerTransmitterPart, int> m_diagonalLookup;

	private List<int> m_diagonalRoots;

	private List<string> m_diagonalKeys;

	private List<PowerTransmitterPart> m_diagonalPartnerBuffer;

	public static bool EnableMultithreadCircuit
	{
		get
		{
			return s_enableMultithreadCircuit;
		}
		set
		{
			s_enableMultithreadCircuit = value;
		}
	}

	public static int CircuitFrameRate
	{
		get
		{
			return s_circuitFrameRate;
		}
		set
		{
			if (value >= 1)
			{
				s_circuitFrameRate = value;
			}
		}
	}

	public static bool UseOptimizedCircuit
	{
		get
		{
			return s_useOptimizedCircuit;
		}
		set
		{
			s_useOptimizedCircuit = value;
		}
	}

	public override StatusCode Status => StatusCode.Running;

	public static ElectricalSystem Instance { get; private set; }

	protected override void Initialize()
	{
		base.Initialize();
		Instance = this;
	}

	public override void Start()
	{
		m_electricalParts = new List<ElectricalPart>();
		m_electricalElements = new List<CircuitElement>();
		m_interfaces = new List<InterfacePart>();
		EnsureSimulator();
		InitializeElectricalParts(Contraption.Instance.Parts);
		GpuPhysicsForces.Configure(INUserSettings.Instance?.StarSeaSettings?.GpuPhysicsCompute ?? false);
		int ordinaryCharges = 0;
		foreach (var part in Contraption.Instance.Parts)
			if (part is PointChargePart charge && !charge.IsSpecialCharge) ordinaryCharges++;
		m_chargeForces.Prepare(ordinaryCharges);
	}

	private void EnsureSimulator()
	{
		bool optimized = UseOptimizedCircuit;
		if (m_circuitSimulator == null || m_simulatorOptimized != optimized)
		{
			m_circuitSimulator = (optimized ? ((CircuitSimulator)new OptimizedCircuitSimulator(Time.fixedDeltaTime)) : new CircuitSimulator(Time.fixedDeltaTime));
			m_simulatorOptimized = optimized;
		}
	}

	public void InitializeElectricalParts(List<BasePart> parts)
	{
		GetElectricalParts(parts, m_electricalParts);
		foreach (ElectricalPart electricalPart in m_electricalParts)
		{
			electricalPart.CreateElectricalElements();
		}
		GetElectricalElements(m_electricalParts, m_electricalElements);
		foreach (ElectricalPart electricalPart2 in m_electricalParts)
		{
			electricalPart2.InitializeConnections();
		}
		foreach (ElectricalPart electricalPart3 in m_electricalParts)
		{
			electricalPart3.ConnectElectricalElements();
		}
		foreach (ElectricalPart electricalPart4 in m_electricalParts)
		{
			electricalPart4.InitializeElectricalElements();
		}
		m_electricalParts.Clear();
		m_electricalElements.Clear();
	}

	private void GetElectricalParts(List<BasePart> parts, List<ElectricalPart> electricalParts)
	{
		CheckList(electricalParts, "electricalParts");
		foreach (BasePart part in parts)
		{
			if (part is ElectricalPart item)
			{
				electricalParts.Add(item);
			}
		}
	}

	private void GetElectricalElements(List<ElectricalPart> electricalParts, List<CircuitElement> elements)
	{
		CheckList(elements, "elements");
		foreach (ElectricalPart electricalPart in electricalParts)
		{
			if (electricalPart.ElectricalElements == null)
			{
				continue;
			}
			foreach (CircuitElement electricalElement in electricalPart.ElectricalElements)
			{
				if (electricalElement != null)
				{
					elements.Add(electricalElement);
				}
			}
		}
	}

	private List<T> SelectElectricalParts<T>() where T : ElectricalPart
	{
		List<T> list = new List<T>();
		SelectElectricalParts(list);
		return list;
	}

	private void SelectElectricalParts<T>(List<T> list) where T : ElectricalPart
	{
		foreach (ElectricalPart electricalPart in m_electricalParts)
		{
			if (electricalPart is T item)
			{
				list.Add(item);
			}
		}
	}

	public override void FixedUpdate()
	{
		float step = 1f / CircuitFrameRate;
		m_accumulator += Time.fixedDeltaTime;
		if (m_accumulator < step)
		{
			return;
		}
		do
		{
			m_accumulator -= step;
			SimulateOnce(step);
		}
		while (m_accumulator >= step);
	}

	private void SimulateOnce(float step)
	{
		List<BasePart> parts = Contraption.Instance.Parts;
		GetElectricalParts(parts, m_electricalParts);
		GetElectricalElements(m_electricalParts, m_electricalElements);
		if (m_electricalParts.Count == 0)
		{
			return;
		}
		EnsureSimulator();
		m_circuitSimulator.SetDeltaTime(step);
		foreach (ElectricalPart electricalPart in m_electricalParts)
		{
			electricalPart.RemoveInvalidConnections();
		}
		UpdateInterfaces();
		if (UseOptimizedCircuit)
		{
			UpdatePowerTransmittersOptimized();
		}
		else
		{
			UpdatePowerTransmitters();
		}
		UpdateDiagonalSenderLinks();
		foreach (ElectricalPart electricalPart2 in m_electricalParts)
		{
			electricalPart2.PreUpdateElements();
		}
		m_circuitSimulator.Simulate(m_electricalElements);
		foreach (ElectricalPart electricalPart3 in m_electricalParts)
		{
			electricalPart3.PostUpdateElements();
		}
		if (UseOptimizedCircuit)
		{
			UpdatePointChargesOptimized();
			UpdateMagneticFieldGeneratorsOptimized();
		}
		else
		{
			UpdatePointCharges();
			UpdateMagneticFieldGenerators();
		}
		m_electricalParts.Clear();
		m_electricalElements.Clear();
	}

	private void UpdateInterfaces()
	{
		foreach (ElectricalPart electricalPart in m_electricalParts)
		{
			if (electricalPart is InterfacePart item)
			{
				m_interfaces.Add(item);
			}
		}
		foreach (InterfacePart @interface in m_interfaces)
		{
			@interface.UpdateConnections();
		}
		m_interfaces.Clear();
	}

	private void UpdatePowerTransmitters()
	{
		Dictionary<int, List<PowerTransmitterPart>> dictionary = new Dictionary<int, List<PowerTransmitterPart>>();
		List<PowerTransmitterPart> list = new List<PowerTransmitterPart>();
		foreach (ElectricalPart electricalPart in m_electricalParts)
		{
			PowerTransmitterPart powerTransmitterPart = electricalPart as PowerTransmitterPart;
			if (powerTransmitterPart == null)
			{
				continue;
			}
			if (powerTransmitterPart.IsSender)
			{
				int channel = powerTransmitterPart.GetChannel();
				if (!dictionary.ContainsKey(channel))
				{
					dictionary[channel] = new List<PowerTransmitterPart>();
				}
				dictionary[channel].Add(powerTransmitterPart);
			}
			else
			{
				list.Add(powerTransmitterPart);
			}
		}
		foreach (PowerTransmitterPart powerTransmitterPart2 in list)
		{
			int channel2 = powerTransmitterPart2.GetChannel();
			Vector3 position = powerTransmitterPart2.transform.position;
			float num = float.MaxValue;
			PowerTransmitterPart powerTransmitterPart3 = null;
			List<PowerTransmitterPart> list2;
			if (dictionary.TryGetValue(channel2, out list2))
			{
				foreach (PowerTransmitterPart powerTransmitterPart4 in list2)
				{
					float num2 = Vector.DistanceSquared2(position, powerTransmitterPart4.transform.position);
					if (num2 < num)
					{
						num = num2;
						powerTransmitterPart3 = powerTransmitterPart4;
					}
				}
			}
			if (powerTransmitterPart3 != null)
			{
				powerTransmitterPart2.Connect(powerTransmitterPart3, Mathf.Sqrt(num));
			}
			else
			{
				powerTransmitterPart2.Disconnect();
			}
		}
	}

	private void UpdatePointCharges()
	{
		List<PointChargePart> list = SelectElectricalParts<PointChargePart>();
		List<ElectricFieldGenerator> list2 = SelectElectricalParts<ElectricFieldGenerator>();
		List<MagneticFieldGenerator> list3 = SelectElectricalParts<MagneticFieldGenerator>();
		bool batchedCharges = TryUpdateGpuCharges(list);
		for (int i = 0; !batchedCharges && i < list.Count; i++)
		{
			PointChargePart pointChargePart = list[i];
			Vector3 position = pointChargePart.transform.position;
			for (int j = i + 1; j < list.Count; j++)
			{
				PointChargePart pointChargePart2 = list[j];
				if (pointChargePart.IsSpecialCharge || pointChargePart2.IsSpecialCharge)
				{
					continue;
				}
				Vector3 position2 = pointChargePart2.transform.position;
				float num = position2.x - position.x;
				float num2 = position2.y - position.y;
				float num3 = num * num + num2 * num2;
				if (num3 < 4096f)
				{
					float num4 = Mathf.Sqrt(num3);
					num4 = ((num4 > 1f) ? num4 : 1f);
					float num5 = 1.5f * pointChargePart.Charge * pointChargePart2.Charge / (num4 * num4 * num4);
					pointChargePart.rigidbody.EngineAddForce(new Vector3((0f - num5) * num, (0f - num5) * num2));
					pointChargePart2.rigidbody.EngineAddForce(new Vector3(num5 * num, num5 * num2));
				}
			}
		}
		for (int k = 0; k < list.Count; k++)
		{
			PointChargePart charge1 = list[k];
			if (!charge1.IsSpecialCharge || charge1.rigidbody == null) continue;
			for (int l = k + 1; l < list.Count; l++)
			{
				PointChargePart charge2 = list[l];
				if (!charge2.IsSpecialCharge || charge2.rigidbody == null) continue;
				if (charge1.IsDeg90 == charge2.IsDeg90) continue;
				if (charge1.HasAutoJoint || charge2.HasAutoJoint) continue;
				if (charge1.ConnectedComponent == charge2.ConnectedComponent) continue;
				if ((charge1.IsDeg270 && !charge1.IsEnabled()) || (charge2.IsDeg270 && !charge2.IsEnabled())) continue;
				int dx = charge2.m_coordX - charge1.m_coordX;
				int dy = charge2.m_coordY - charge1.m_coordY;
				float gridDist = Mathf.Sqrt(dx * dx + dy * dy);
				float forceRadius = INUserSettings.Instance?.StarSeaSettings?.SpecialChargeForceRadius ?? 4f;
				if (gridDist > forceRadius) continue;
				Vector3 dir = charge1.transform.position - charge2.transform.position;
				float worldDist = dir.magnitude;
				worldDist = ((worldDist > 0.1f) ? worldDist : 0.1f);
				float baseForce = INUserSettings.Instance?.StarSeaSettings?.SpecialChargeForce ?? 70f;
				float forceMagnitude = baseForce;
				float fx = forceMagnitude * dir.x / worldDist;
				float fy = forceMagnitude * dir.y / worldDist;
				charge1.rigidbody.EngineAddForce(new Vector3(-fx, -fy));
				charge2.rigidbody.EngineAddForce(new Vector3(fx, fy));
				// Remove vehicle collision on attraction (once per charge pair)
				if (INUserSettings.Instance?.StarSeaSettings?.SpecialChargeAttractionCollisionIgnore ?? true)
				{
					int id1 = charge1.GetInstanceID();
					int id2 = charge2.GetInstanceID();
					long pairKey = id1 < id2 ? ((long)id1 << 32 | (uint)id2) : ((long)id2 << 32 | (uint)id1);
					if (!s_ignoredColliderPairs.Contains(pairKey))
					{
						s_ignoredColliderPairs.Add(pairKey);
						int cc1 = charge1.ConnectedComponent;
						int cc2 = charge2.ConnectedComponent;
						var allParts = Contraption.Instance?.Parts;
						if (allParts != null)
						{
							var bufa = new List<Collider>();
							var bufb = new List<Collider>();
							for (int pi = 0; pi < allParts.Count; pi++)
							{
								if (allParts[pi].ConnectedComponent != cc1) continue;
								bufa.Clear();
								allParts[pi].GetComponentsInChildren<Collider>(bufa);
								for (int ci = 0; ci < bufa.Count; ci++)
								{
									if (bufa[ci] == null) continue;
									for (int pj = 0; pj < allParts.Count; pj++)
									{
										if (allParts[pj].ConnectedComponent != cc2) continue;
										bufb.Clear();
										allParts[pj].GetComponentsInChildren<Collider>(bufb);
										for (int cj = 0; cj < bufb.Count; cj++)
										{
											if (bufb[cj] != null) GamePhysics.IgnoreCollision(bufa[ci], bufb[cj], true);
										}
									}
								}
							}
						}
					}
				}
				float jointDist = INUserSettings.Instance?.StarSeaSettings?.SpecialChargeJointDistance ?? 0.5f;
				if (worldDist < jointDist && charge1.IsEnabled() && charge2.IsEnabled())
				{
					charge1.TryCreateRotatableJoint(charge2);
				}
			}
		}
		// ForceConnect: connect ANY two different-type special charges immediately
		// skips forceRadius check AND attraction force — only checks jointDist
		if (INUserSettings.Instance?.StarSeaSettings?.SpecialChargeForceConnect ?? false)
		{
			float fcJointDist = INUserSettings.Instance?.StarSeaSettings?.SpecialChargeJointDistance ?? 0.5f;
			for (int k = 0; k < list.Count; k++)
			{
				PointChargePart fc1 = list[k];
				if (!fc1.IsSpecialCharge || fc1.rigidbody == null) continue;
				if (!fc1.IsEnabled()) continue;
				for (int l = k + 1; l < list.Count; l++)
				{
					PointChargePart fc2 = list[l];
					if (!fc2.IsSpecialCharge || fc2.rigidbody == null) continue;
					if (!fc2.IsEnabled()) continue;
					if (fc1.IsDeg90 == fc2.IsDeg90) continue;
					if (fc1.ConnectedComponent == fc2.ConnectedComponent) continue;
					if (fc1.HasAutoJoint || fc2.HasAutoJoint) continue;
					float fWorldDist = Vector3.Distance(fc1.transform.position, fc2.transform.position);
					if (fWorldDist < fcJointDist)
					{
						fc1.TryCreateRotatableJoint(fc2);
					}
				}
			}
		}
		foreach (ElectricFieldGenerator item in list2)
		{
			if (!item.IsEnabled())
			{
				continue;
			}
			Vector3 up = item.transform.up;
			Vector3 position3 = item.transform.position;
			foreach (PointChargePart item2 in list)
			{
				Vector3 position4 = item2.transform.position;
				float num6 = Vector.DistanceSquared2(position3, position4);
				if (!(num6 > 64f))
				{
					float num7 = item.GetElectricFieldIntensityByDistance(Mathf.Sqrt(num6)) * item2.Charge;
					item2.rigidbody.EngineAddForce(new Vector3(num7 * up.x, num7 * up.y));
					item.rigidbody.EngineAddForce(new Vector3((0f - num7) * up.x, (0f - num7) * up.y));
				}
			}
		}
		foreach (MagneticFieldGenerator item3 in list3)
		{
			if (!item3.IsEnabled())
			{
				continue;
			}
			Vector3 position5 = item3.transform.position;
			Vector3 velocity = item3.rigidbody.velocity;
			_ = item3.transform.right;
			foreach (PointChargePart item4 in list)
			{
				Vector3 velocity2 = item4.rigidbody.velocity;
				float num8 = velocity2.x - velocity.x;
				float num9 = velocity2.y - velocity.y;
				float distance = Vector.Distance2(position5, item4.transform.position);
				float num10 = item3.GetMagneticFluxDensityByDistance(distance) * item4.Charge;
				item4.rigidbody.EngineAddForce(new Vector3((0f - num10) * num9, num10 * num8));
				item3.rigidbody.EngineAddForce(new Vector3(num10 * num9, (0f - num10) * num8));
			}
		}
	}

	private void UpdateMagneticFieldGenerators()
	{
		List<WirePartBase> list = new List<WirePartBase>();
		foreach (ElectricalPart electricalPart in m_electricalParts)
		{
			if (electricalPart is WirePartBase wirePartBase && wirePartBase.IsValid() && wirePartBase.IsElectromagnetic())
			{
				bool flag = false;
				bool flag2 = true;
				double[] currents = wirePartBase.Currents;
				foreach (double num in currents)
				{
					flag = flag || num != 0.0;
					flag2 &= !double.IsNaN(num);
				}
				if (flag && flag2)
				{
					list.Add(wirePartBase);
				}
			}
		}
		foreach (ElectricalPart electricalPart2 in m_electricalParts)
		{
			if (!(electricalPart2 is MagneticFieldGenerator magneticFieldGenerator) || !magneticFieldGenerator.IsEnabled())
			{
				continue;
			}
			Vector3 position = magneticFieldGenerator.transform.position;
			foreach (WirePartBase item in list)
			{
				Vector3 position2 = item.transform.position;
				float num2 = Vector.DistanceSquared2(position, position2);
				if (!(num2 > 64f))
				{
					Vector3 right = item.transform.right;
					Vector3 vector = new Vector2(0f - right.y, right.x);
					double[] currents2 = item.Currents;
					Vector3 vector2 = 0.5f * (float)(currents2[0] - currents2[2]) * vector + 0.5f * (float)(currents2[1] - currents2[3]) * right;
					vector2 *= magneticFieldGenerator.GetMagneticFluxDensityByDistance(Mathf.Sqrt(num2));
					item.rigidbody.EngineAddForce(vector2);
					magneticFieldGenerator.rigidbody.EngineAddForce(-vector2);
				}
			}
		}
	}

	// ===== 优化路径：与上方原方法逐行等价，仅把所有每帧临时容器改为复用缓冲 =====

	private void UpdatePowerTransmittersOptimized()
	{
		if (m_transmitterByChannel == null)
		{
			m_transmitterByChannel = new Dictionary<int, List<PowerTransmitterPart>>();
			m_transmitterReceivers = new List<PowerTransmitterPart>();
		}
		foreach (KeyValuePair<int, List<PowerTransmitterPart>> existing in m_transmitterByChannel)
		{
			existing.Value.Clear();
		}
		m_transmitterReceivers.Clear();
		foreach (ElectricalPart electricalPart in m_electricalParts)
		{
			PowerTransmitterPart powerTransmitterPart = electricalPart as PowerTransmitterPart;
			if (powerTransmitterPart == null)
			{
				continue;
			}
			if (powerTransmitterPart.IsSender)
			{
				int channel = powerTransmitterPart.GetChannel();
				if (!m_transmitterByChannel.TryGetValue(channel, out var senders))
				{
					senders = new List<PowerTransmitterPart>();
					m_transmitterByChannel[channel] = senders;
				}
				senders.Add(powerTransmitterPart);
			}
			else
			{
				m_transmitterReceivers.Add(powerTransmitterPart);
			}
		}
		foreach (PowerTransmitterPart receiver in m_transmitterReceivers)
		{
			int channel2 = receiver.GetChannel();
			Vector3 position = receiver.transform.position;
			float num = float.MaxValue;
			PowerTransmitterPart powerTransmitterPart3 = null;
			if (m_transmitterByChannel.TryGetValue(channel2, out var senders2))
			{
				foreach (PowerTransmitterPart sender in senders2)
				{
					float num2 = Vector.DistanceSquared2(position, sender.transform.position);
					if (num2 < num)
					{
						num = num2;
						powerTransmitterPart3 = sender;
					}
				}
			}
			if (powerTransmitterPart3 != null)
			{
				receiver.Connect(powerTransmitterPart3, Mathf.Sqrt(num));
			}
			else
			{
				receiver.Disconnect();
			}
		}
	}

	// ===== 斜向发送器（全新机制）：同频道斜向发送器在范围内直接互连，电阻 0 =====
	// 频道 = 该发送器所在“等电位组(短路连通)”内所有斜向发送器染色框标签的多重集（顺序无关、数量敏感）
	private void UpdateDiagonalSenderLinks()
	{
		if (m_diagonalSenders == null)
		{
			m_diagonalSenders = new List<PowerTransmitterPart>();
			m_equipElectrodes = new List<Electrode>();
			m_equipIndex = new Dictionary<Electrode, int>();
			m_equipParent = new List<int>();
			m_diagonalLookup = new Dictionary<PowerTransmitterPart, int>();
			m_diagonalRoots = new List<int>();
			m_diagonalKeys = new List<string>();
			m_diagonalPartnerBuffer = new List<PowerTransmitterPart>();
		}
		m_diagonalSenders.Clear();
		for (int i = 0; i < m_electricalParts.Count; i++)
		{
			if (m_electricalParts[i] is PowerTransmitterPart transmitter)
			{
				if (transmitter.IsDiagonalSender)
				{
					m_diagonalSenders.Add(transmitter);
				}
				else if (transmitter.HasDiagonalSenderLink)
				{
					// 已不再是斜向发送器(旋转/类型变化)：清理残留链路
					transmitter.DisconnectAllDiagonalSenders();
				}
			}
		}
		if (m_diagonalSenders.Count == 0)
		{
			return;
		}
		// 1) 等电位(短路连通)并查集：只有 导线 / 闭合开关 / 0Ω电阻 视为同电位；
		//    电阻、断开的开关、电压源/电源等会隔断等电位。本机制新增的无线电极不计入（避免自反馈）。
		m_equipElectrodes.Clear();
		m_equipIndex.Clear();
		m_equipParent.Clear();
		for (int i = 0; i < m_electricalElements.Count; i++)
		{
			CircuitElement element = m_electricalElements[i];
			if (element == null)
			{
				continue;
			}
			List<Electrode> electrodes = element.Electrodes;
			for (int k = 0; k < electrodes.Count; k++)
			{
				Electrode electrode = electrodes[k];
				if (electrode == null || electrode.IsWirelessLink || m_equipIndex.ContainsKey(electrode))
				{
					continue;
				}
				m_equipIndex[electrode] = m_equipElectrodes.Count;
				m_equipElectrodes.Add(electrode);
				m_equipParent.Add(m_equipElectrodes.Count - 1);
			}
		}
		// 元件内部短路：导线、闭合开关(含单刀双掷的闭合端)、0Ω 电阻 → 其闭合电极互为同电位
		for (int i = 0; i < m_electricalElements.Count; i++)
		{
			CircuitElement element = m_electricalElements[i];
			if (element == null || !IsEquipotentialElement(element))
			{
				continue;
			}
			Electrode anchor = null;
			List<Electrode> electrodes = element.Electrodes;
			for (int k = 0; k < electrodes.Count; k++)
			{
				Electrode electrode = electrodes[k];
				if (electrode == null || electrode.IsWirelessLink || !electrode.IsClosed)
				{
					continue;
				}
				if (anchor == null)
				{
					anchor = electrode;
				}
				else
				{
					UnionEquip(m_equipIndex[anchor], m_equipIndex[electrode]);
				}
			}
		}
		// 元件之间直接接触(双方电极均闭合) → 同电位
		for (int i = 0; i < m_equipElectrodes.Count; i++)
		{
			Electrode electrode = m_equipElectrodes[i];
			if (!electrode.IsConnected)
			{
				continue;
			}
			Electrode other = electrode.ConnectedElectrode;
			if (other == null || other.IsWirelessLink)
			{
				continue;
			}
			if (m_equipIndex.TryGetValue(other, out int otherIndex))
			{
				UnionEquip(i, otherIndex);
			}
		}
		// 2) 每个等电位组的频道签名 = 组内所有斜向发送器标签的多重集
		Dictionary<int, List<int>> labelsByRoot = new Dictionary<int, List<int>>();
		for (int i = 0; i < m_diagonalSenders.Count; i++)
		{
			PowerTransmitterPart sender = m_diagonalSenders[i];
			if (sender == null)
			{
				continue;
			}
			Electrode electrode = sender.DiagonalNodeElectrode;
			if (electrode == null || !m_equipIndex.TryGetValue(electrode, out int index))
			{
				continue;
			}
			int root = FindEquip(index);
			if (!labelsByRoot.TryGetValue(root, out List<int> list))
			{
				list = new List<int>();
				labelsByRoot[root] = list;
			}
			list.Add(sender.ChannelLabel);
		}
		Dictionary<int, string> keyByRoot = new Dictionary<int, string>();
		foreach (KeyValuePair<int, List<int>> pair in labelsByRoot)
		{
			pair.Value.Sort();
			keyByRoot[pair.Key] = string.Join(",", pair.Value);
		}
		// 每个斜向发送器的(等电位组根, 频道签名)；roots/keys 与 m_diagonalSenders 一一对应
		m_diagonalLookup.Clear();
		m_diagonalRoots.Clear();
		m_diagonalKeys.Clear();
		for (int i = 0; i < m_diagonalSenders.Count; i++)
		{
			PowerTransmitterPart sender = m_diagonalSenders[i];
			int root = -1;
			string key = null;
			if (sender != null)
			{
				Electrode electrode = sender.DiagonalNodeElectrode;
				if (electrode != null && m_equipIndex.TryGetValue(electrode, out int index))
				{
					root = FindEquip(index);
					keyByRoot.TryGetValue(root, out key);
					m_diagonalLookup[sender] = i;
				}
			}
			m_diagonalRoots.Add(root);
			m_diagonalKeys.Add(key);
		}
		float range = INUserSettings.Instance?.StarSeaSettings?.DiagonalSenderRange ?? 32f;
		if (range <= 0f)
		{
			range = 32f;
		}
		float rangeSquared = range * range;
		// 3a) 先清理失效链路（对方已不是同频道斜向发送器 / 超出范围 / 同一连通组）
		for (int i = 0; i < m_diagonalSenders.Count; i++)
		{
			PowerTransmitterPart sender = m_diagonalSenders[i];
			if (sender == null || !m_diagonalLookup.ContainsKey(sender))
			{
				continue;
			}
			m_diagonalPartnerBuffer.Clear();
			m_diagonalPartnerBuffer.AddRange(sender.DiagonalSenderPartners);
			for (int k = 0; k < m_diagonalPartnerBuffer.Count; k++)
			{
				PowerTransmitterPart partner = m_diagonalPartnerBuffer[k];
				if (!IsDiagonalSenderPairValid(sender, partner, rangeSquared))
				{
					sender.DisconnectDiagonalSender(partner);
				}
			}
		}
		// 3b) 再补全：范围内所有同频道斜向发送器**两两互连**（无就近原则、数量不限）
		for (int i = 0; i < m_diagonalSenders.Count; i++)
		{
			PowerTransmitterPart a = m_diagonalSenders[i];
			if (a == null || !m_diagonalLookup.ContainsKey(a))
			{
				continue;
			}
			for (int j = i + 1; j < m_diagonalSenders.Count; j++)
			{
				PowerTransmitterPart b = m_diagonalSenders[j];
				if (b == null || !m_diagonalLookup.ContainsKey(b))
				{
					continue;
				}
				if (IsDiagonalSenderPairValid(a, b, rangeSquared))
				{
					a.ConnectDiagonalSender(b);
				}
			}
		}
	}

	private bool IsDiagonalSenderPairValid(PowerTransmitterPart a, PowerTransmitterPart b, float rangeSquared)
	{
		if (a == null || b == null || a == b)
		{
			return false;
		}
		if (!m_diagonalLookup.TryGetValue(a, out int ia) || !m_diagonalLookup.TryGetValue(b, out int ib))
		{
			return false;
		}
		if (m_diagonalRoots[ia] == m_diagonalRoots[ib])
		{
			return false;
		}
		string keyA = m_diagonalKeys[ia];
		if (keyA == null || keyA != m_diagonalKeys[ib])
		{
			return false;
		}
		return Vector.DistanceSquared2(a.transform.position, b.transform.position) <= rangeSquared;
	}

	private int FindEquip(int index)
	{
		while (m_equipParent[index] != index)
		{
			m_equipParent[index] = m_equipParent[m_equipParent[index]];
			index = m_equipParent[index];
		}
		return index;
	}

	private void UnionEquip(int a, int b)
	{
		int rootA = FindEquip(a);
		int rootB = FindEquip(b);
		if (rootA != rootB)
		{
			m_equipParent[rootB] = rootA;
		}
	}

	// 该元件内部是否为“短路/同电位”：导线、闭合开关(含单刀双掷的闭合端)、0Ω 电阻。
	// 电阻(R>0)、断开的开关、电压源/电源/电容/电感等会隔断等电位，返回 false。
	private static bool IsEquipotentialElement(CircuitElement element)
	{
		if (element is Wire)
		{
			return true;
		}
		if (element is Switch)
		{
			return true;
		}
		if (element is SPDTSwitch)
		{
			return true;
		}
		if (element is Resistor resistor)
		{
			return resistor.Resistance == 0.0;
		}
		return false;
	}

	private void UpdatePointChargesOptimized()
	{
		if (m_chargeParts == null)
		{
			m_chargeParts = new List<PointChargePart>();
			m_electricFieldGenerators = new List<ElectricFieldGenerator>();
			m_magneticFieldGenerators = new List<MagneticFieldGenerator>();
			m_colliderBufferA = new List<Collider>();
			m_colliderBufferB = new List<Collider>();
		}
		m_chargeParts.Clear();
		m_electricFieldGenerators.Clear();
		m_magneticFieldGenerators.Clear();
		foreach (ElectricalPart electricalPart in m_electricalParts)
		{
			if (electricalPart is PointChargePart pointCharge)
			{
				m_chargeParts.Add(pointCharge);
			}
			else if (electricalPart is ElectricFieldGenerator electricFieldGenerator)
			{
				m_electricFieldGenerators.Add(electricFieldGenerator);
			}
			else if (electricalPart is MagneticFieldGenerator magneticFieldGenerator)
			{
				m_magneticFieldGenerators.Add(magneticFieldGenerator);
			}
		}
		List<PointChargePart> list = m_chargeParts;
		List<ElectricFieldGenerator> list2 = m_electricFieldGenerators;
		List<MagneticFieldGenerator> list3 = m_magneticFieldGenerators;
		StarSeaSettings starSea = INUserSettings.Instance?.StarSeaSettings;
		float forceRadius = starSea?.SpecialChargeForceRadius ?? 4f;
		float baseForce = starSea?.SpecialChargeForce ?? 70f;
		bool attractionIgnore = starSea?.SpecialChargeAttractionCollisionIgnore ?? true;
		float jointDist = starSea?.SpecialChargeJointDistance ?? 0.5f;
		bool forceConnect = starSea?.SpecialChargeForceConnect ?? false;
		bool batchedCharges = TryUpdateGpuCharges(list);
		for (int i = 0; !batchedCharges && i < list.Count; i++)
		{
			PointChargePart pointChargePart = list[i];
			Vector3 position = pointChargePart.transform.position;
			for (int j = i + 1; j < list.Count; j++)
			{
				PointChargePart pointChargePart2 = list[j];
				if (pointChargePart.IsSpecialCharge || pointChargePart2.IsSpecialCharge)
				{
					continue;
				}
				Vector3 position2 = pointChargePart2.transform.position;
				float num = position2.x - position.x;
				float num2 = position2.y - position.y;
				float num3 = num * num + num2 * num2;
				if (num3 < 4096f)
				{
					float num4 = Mathf.Sqrt(num3);
					num4 = ((num4 > 1f) ? num4 : 1f);
					float num5 = 1.5f * pointChargePart.Charge * pointChargePart2.Charge / (num4 * num4 * num4);
					pointChargePart.rigidbody.AddForce(new Vector3((0f - num5) * num, (0f - num5) * num2));
					pointChargePart2.rigidbody.AddForce(new Vector3(num5 * num, num5 * num2));
				}
			}
		}
		for (int k = 0; k < list.Count; k++)
		{
			PointChargePart charge1 = list[k];
			if (!charge1.IsSpecialCharge || charge1.rigidbody == null) continue;
			for (int l = k + 1; l < list.Count; l++)
			{
				PointChargePart charge2 = list[l];
				if (!charge2.IsSpecialCharge || charge2.rigidbody == null) continue;
				if (charge1.IsDeg90 == charge2.IsDeg90) continue;
				if (charge1.HasAutoJoint || charge2.HasAutoJoint) continue;
				if (charge1.ConnectedComponent == charge2.ConnectedComponent) continue;
				if ((charge1.IsDeg270 && !charge1.IsEnabled()) || (charge2.IsDeg270 && !charge2.IsEnabled())) continue;
				int dx = charge2.m_coordX - charge1.m_coordX;
				int dy = charge2.m_coordY - charge1.m_coordY;
				float gridDist = Mathf.Sqrt(dx * dx + dy * dy);
				if (gridDist > forceRadius) continue;
				Vector3 dir = charge1.transform.position - charge2.transform.position;
				float worldDist = dir.magnitude;
				worldDist = ((worldDist > 0.1f) ? worldDist : 0.1f);
				float fx = baseForce * dir.x / worldDist;
				float fy = baseForce * dir.y / worldDist;
				charge1.rigidbody.AddForce(new Vector3(-fx, -fy));
				charge2.rigidbody.AddForce(new Vector3(fx, fy));
				if (attractionIgnore)
				{
					int id1 = charge1.GetInstanceID();
					int id2 = charge2.GetInstanceID();
					long pairKey = id1 < id2 ? ((long)id1 << 32 | (uint)id2) : ((long)id2 << 32 | (uint)id1);
					if (!s_ignoredColliderPairs.Contains(pairKey))
					{
						s_ignoredColliderPairs.Add(pairKey);
						int cc1 = charge1.ConnectedComponent;
						int cc2 = charge2.ConnectedComponent;
						List<BasePart> allParts = Contraption.Instance?.Parts;
						if (allParts != null)
						{
							List<Collider> bufa = m_colliderBufferA;
							List<Collider> bufb = m_colliderBufferB;
							for (int pi = 0; pi < allParts.Count; pi++)
							{
								if (allParts[pi].ConnectedComponent != cc1) continue;
								bufa.Clear();
								allParts[pi].GetComponentsInChildren<Collider>(bufa);
								for (int ci = 0; ci < bufa.Count; ci++)
								{
									if (bufa[ci] == null) continue;
									for (int pj = 0; pj < allParts.Count; pj++)
									{
										if (allParts[pj].ConnectedComponent != cc2) continue;
										bufb.Clear();
										allParts[pj].GetComponentsInChildren<Collider>(bufb);
										for (int cj = 0; cj < bufb.Count; cj++)
										{
											if (bufb[cj] != null) Physics.IgnoreCollision(bufa[ci], bufb[cj], true);
										}
									}
								}
							}
						}
					}
				}
				if (worldDist < jointDist && charge1.IsEnabled() && charge2.IsEnabled())
				{
					charge1.TryCreateRotatableJoint(charge2);
				}
			}
		}
		if (forceConnect)
		{
			for (int k = 0; k < list.Count; k++)
			{
				PointChargePart fc1 = list[k];
				if (!fc1.IsSpecialCharge || fc1.rigidbody == null) continue;
				if (!fc1.IsEnabled()) continue;
				for (int l = k + 1; l < list.Count; l++)
				{
					PointChargePart fc2 = list[l];
					if (!fc2.IsSpecialCharge || fc2.rigidbody == null) continue;
					if (!fc2.IsEnabled()) continue;
					if (fc1.IsDeg90 == fc2.IsDeg90) continue;
					if (fc1.ConnectedComponent == fc2.ConnectedComponent) continue;
					if (fc1.HasAutoJoint || fc2.HasAutoJoint) continue;
					float fWorldDist = Vector3.Distance(fc1.transform.position, fc2.transform.position);
					if (fWorldDist < jointDist)
					{
						fc1.TryCreateRotatableJoint(fc2);
					}
				}
			}
		}
		foreach (ElectricFieldGenerator item in list2)
		{
			if (!item.IsEnabled())
			{
				continue;
			}
			Vector3 up = item.transform.up;
			Vector3 position3 = item.transform.position;
			foreach (PointChargePart item2 in list)
			{
				Vector3 position4 = item2.transform.position;
				float num6 = Vector.DistanceSquared2(position3, position4);
				if (!(num6 > 64f))
				{
					float num7 = item.GetElectricFieldIntensityByDistance(Mathf.Sqrt(num6)) * item2.Charge;
					item2.rigidbody.AddForce(new Vector3(num7 * up.x, num7 * up.y));
					item.rigidbody.AddForce(new Vector3((0f - num7) * up.x, (0f - num7) * up.y));
				}
			}
		}
		foreach (MagneticFieldGenerator item3 in list3)
		{
			if (!item3.IsEnabled())
			{
				continue;
			}
			Vector3 position5 = item3.transform.position;
			Vector3 velocity = item3.rigidbody.velocity;
			_ = item3.transform.right;
			foreach (PointChargePart item4 in list)
			{
				Vector3 velocity2 = item4.rigidbody.velocity;
				float num8 = velocity2.x - velocity.x;
				float num9 = velocity2.y - velocity.y;
				float distance = Vector.Distance2(position5, item4.transform.position);
				float num10 = item3.GetMagneticFluxDensityByDistance(distance) * item4.Charge;
				item4.rigidbody.AddForce(new Vector3((0f - num10) * num9, num10 * num8));
				item3.rigidbody.AddForce(new Vector3(num10 * num9, (0f - num10) * num8));
			}
		}
	}

	private void UpdateMagneticFieldGeneratorsOptimized()
	{
		if (m_magneticWires == null)
		{
			m_magneticWires = new List<WirePartBase>();
		}
		List<WirePartBase> list = m_magneticWires;
		list.Clear();
		foreach (ElectricalPart electricalPart in m_electricalParts)
		{
			if (electricalPart is WirePartBase wirePartBase && wirePartBase.IsValid() && wirePartBase.IsElectromagnetic())
			{
				bool flag = false;
				bool flag2 = true;
				double[] currents = wirePartBase.Currents;
				foreach (double num in currents)
				{
					flag = flag || num != 0.0;
					flag2 &= !double.IsNaN(num);
				}
				if (flag && flag2)
				{
					list.Add(wirePartBase);
				}
			}
		}
		foreach (ElectricalPart electricalPart2 in m_electricalParts)
		{
			if (!(electricalPart2 is MagneticFieldGenerator magneticFieldGenerator) || !magneticFieldGenerator.IsEnabled())
			{
				continue;
			}
			Vector3 position = magneticFieldGenerator.transform.position;
			foreach (WirePartBase item in list)
			{
				Vector3 position2 = item.transform.position;
				float num2 = Vector.DistanceSquared2(position, position2);
				if (!(num2 > 64f))
				{
					Vector3 right = item.transform.right;
					Vector3 vector = new Vector2(0f - right.y, right.x);
					double[] currents2 = item.Currents;
					Vector3 vector2 = 0.5f * (float)(currents2[0] - currents2[2]) * vector + 0.5f * (float)(currents2[1] - currents2[3]) * right;
					vector2 *= magneticFieldGenerator.GetMagneticFluxDensityByDistance(Mathf.Sqrt(num2));
					item.rigidbody.AddForce(vector2);
					magneticFieldGenerator.rigidbody.AddForce(-vector2);
				}
			}
		}
	}

	private bool TryUpdateGpuCharges(List<PointChargePart> charges)
	{
		if (!(INUserSettings.Instance?.StarSeaSettings?.GpuPhysicsCompute ?? false)) return false;
		m_chargeForces.Reserve(charges.Count);
		if (m_chargeBodies.Length < m_chargeForces.Inputs.Length) m_chargeBodies = new Rigidbody[m_chargeForces.Inputs.Length];
		int count = 0;
		foreach (var charge in charges)
		{
			if (charge == null || charge.IsSpecialCharge || charge.rigidbody == null) continue;
			float amount = charge.Charge;
			if (amount == 0f) continue;
			Vector3 position = charge.transform.position;
			Rigidbody body = charge.rigidbody;
			m_chargeBodies[count] = body;
			m_chargeForces.Inputs[count++] = new GpuPhysicsForces.Body {
				state = new Vector4(position.x, position.y, body.mass, amount),
				flags = body.isKinematic ? GpuPhysicsForces.Kinematic : 0
			};
		}
		var values = m_chargeForces.Evaluate(count);
		for (int i = 0; i < count; i++)
			if ((m_chargeForces.Inputs[i].flags & GpuPhysicsForces.Kinematic) == 0 && (values[i].x != 0f || values[i].y != 0f))
				m_chargeBodies[i].EngineAddForce(new Vector3(values[i].x, values[i].y, 0f));
		return true;
	}

	public override void OnDestroy()
	{
		m_chargeForces.Dispose(); m_chargeBodies = Array.Empty<Rigidbody>();
		if (Instance == this)
		{
			Instance = null;
		}
	}

	private void CheckList<T>(List<T> list, string name)
	{
		if (list == null)
		{
			throw new ArgumentNullException(name);
		}
		if (list.Count > 0)
		{
			list.Clear();
		}
	}
}
