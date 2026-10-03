using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine;

namespace BadPiggies.Multiplayer.Online
{
	/// <summary>Wire format of a single LAN beacon datagram.</summary>
	[Serializable]
	internal class LanBeaconPayload
	{
		public string k;
		public string c;
		public string n;
		public string h;
		public int p;
		public string i;
		public string e;
		public string x;
		public string t;
		public string a;
		public int m;
		public int max;
		public bool pw;
		public int v;
		public int l;
	}

	/// <summary>
	/// "Same Wi-Fi" discovery: the host shouts a small UDP beacon, everyone else listens and can
	/// join with one click, no room code typing. This is deliberately independent of EasyTier, so
	/// it also works when both players are on the same physical LAN and never start a VPN.
	///
	/// All socket work happens on background threads; results reach the main thread through a
	/// queue drained in <see cref="Update"/>.
	/// </summary>
	public sealed class OnlineLanDiscovery : MonoBehaviour
	{
		private const float RoomTimeoutSeconds = 4.5f;
		private const float BeaconIntervalSeconds = 1f;

		private static OnlineLanDiscovery s_instance;

		private readonly List<OnlineLanRoom> m_rooms = new List<OnlineLanRoom>();
		private readonly ConcurrentQueue<OnlineLanRoom> m_incoming = new ConcurrentQueue<OnlineLanRoom>();

		private Thread m_broadcastThread;
		private Thread m_listenThread;
		private UdpClient m_broadcastClient;
		private UdpClient m_listenClient;
		private volatile bool m_broadcastStop;
		private volatile bool m_listenStop;

		private string m_beaconJson = "";
		private string m_lastError = "";
		private bool m_roomsDirty;

		public static OnlineLanDiscovery GetOrCreate()
		{
			if (s_instance != null)
			{
				return s_instance;
			}
			GameObject host = new GameObject("[Online LAN Discovery]");
			UnityEngine.Object.DontDestroyOnLoad(host);
			s_instance = host.AddComponent<OnlineLanDiscovery>();
			return s_instance;
		}

		public static OnlineLanDiscovery Instance
		{
			get { return s_instance; }
		}

		public bool IsBroadcasting
		{
			get { return m_broadcastThread != null; }
		}

		public bool IsListening
		{
			get { return m_listenThread != null; }
		}

		public string LastError
		{
			get { return m_lastError; }
		}

		public event Action RoomsChanged;

		private void Awake()
		{
			if (s_instance != null && s_instance != this)
			{
				UnityEngine.Object.Destroy(base.gameObject);
				return;
			}
			s_instance = this;
		}

		private void OnDestroy()
		{
			StopBroadcast();
			StopListening();
			if (s_instance == this)
			{
				s_instance = null;
			}
		}

		private void OnApplicationQuit()
		{
			StopBroadcast();
			StopListening();
		}

		private void Update()
		{
			OnlineLanRoom incoming;
			while (m_incoming.TryDequeue(out incoming))
			{
				MergeRoom(incoming);
			}
			if (PruneRooms() | m_roomsDirty)
			{
				m_roomsDirty = false;
				Action handler = RoomsChanged;
				if (handler != null)
				{
					handler();
				}
			}
		}

		public List<OnlineLanRoom> Rooms
		{
			get { return m_rooms; }
		}

		/// <summary>
		/// The address this machine most likely appears as to another machine on the same network.
		/// Used to tell guests where to connect, which is what lets the whole flow work without a
		/// server. Returns "" when nothing usable is found; callers must degrade gracefully rather
		/// than treat that as a fatal error.
		/// </summary>
		public static string GetPrimaryLanAddress()
		{
			List<string> addresses = GetLanAddresses();
			return addresses.Count == 0 ? "" : addresses[0];
		}

		/// <summary>
		/// Every usable private IPv4 address, best candidate first. Loopback, link-local and
		/// virtual-adapter addresses are filtered out so the host advertises something a guest can
		/// actually dial.
		/// </summary>
		public static List<string> GetLanAddresses()
		{
			List<string> preferred = new List<string>();
			List<string> others = new List<string>();
			try
			{
				NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
				for (int i = 0; i < interfaces.Length; i++)
				{
					NetworkInterface nic = interfaces[i];
					if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
					{
						continue;
					}
					UnicastIPAddressInformationCollection unicast = nic.GetIPProperties().UnicastAddresses;
					for (int j = 0; j < unicast.Count; j++)
					{
						IPAddress address = unicast[j].Address;
						if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address))
						{
							continue;
						}
						string text = address.ToString();
						if (text.StartsWith("169.254.", StringComparison.Ordinal))
						{
							continue;
						}
						// Skip EasyTier's own virtual address so we advertise the real LAN path.
						if (text == OnlineNetConstants.DefaultHostVirtualIpv4 || text.StartsWith("10.126.126.", StringComparison.Ordinal))
						{
							continue;
						}
						if (preferred.Contains(text) || others.Contains(text))
						{
							continue;
						}
						if (IsPrivateAddress(address))
						{
							preferred.Add(text);
						}
						else
						{
							others.Add(text);
						}
					}
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[Online] Could not enumerate local addresses: " + ex.Message);
			}
			preferred.AddRange(others);
			return preferred;
		}

		private static bool IsPrivateAddress(IPAddress address)
		{
			byte[] bytes = address.GetAddressBytes();
			if (bytes.Length != 4)
			{
				return false;
			}
			if (bytes[0] == 10)
			{
				return true;
			}
			if (bytes[0] == 192 && bytes[1] == 168)
			{
				return true;
			}
			return bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31;
		}

		/// <summary>
		/// Finds an already-discovered room by its code. This is the server-free answer to "join by
		/// room code": if the host is on the same network, its beacon already told us the address.
		/// </summary>
		public static OnlineLanRoom FindRoom(string roomCode)
		{
			string normalized = OnlineRoomCode.Normalize(roomCode);
			if (normalized.Length == 0 || s_instance == null)
			{
				return null;
			}
			List<OnlineLanRoom> rooms = s_instance.m_rooms;
			for (int i = 0; i < rooms.Count; i++)
			{
				OnlineLanRoom room = rooms[i];
				if (room != null && room.Info != null && OnlineRoomCode.Normalize(room.Info.RoomCode) == normalized)
				{
					return room;
				}
			}
			return null;
		}

		// ---------------------------------------------------------------- broadcast

		public void StartBroadcast(OnlineRoomInfo info, int memberCount, int maxPlayers)
		{
			if (info == null || string.IsNullOrEmpty(info.RoomCode))
			{
				return;
			}
			StopBroadcast();
			BuildBeacon(info, memberCount, maxPlayers);
			m_broadcastStop = false;
			m_broadcastThread = new Thread(BroadcastLoop);
			m_broadcastThread.IsBackground = true;
			m_broadcastThread.Name = "BPRC LAN beacon";
			m_broadcastThread.Start();
		}

		public void UpdateBroadcast(OnlineRoomInfo info, int memberCount, int maxPlayers)
		{
			if (info == null || string.IsNullOrEmpty(info.RoomCode))
			{
				return;
			}
			BuildBeacon(info, memberCount, maxPlayers);
		}

		public void StopBroadcast()
		{
			m_broadcastStop = true;
			if (m_broadcastClient != null)
			{
				try
				{
					m_broadcastClient.Close();
				}
				catch (Exception)
				{
				}
				m_broadcastClient = null;
			}
			if (m_broadcastThread != null)
			{
				try
				{
					m_broadcastThread.Join(500);
				}
				catch (Exception)
				{
				}
				m_broadcastThread = null;
			}
		}

		private void BuildBeacon(OnlineRoomInfo info, int memberCount, int maxPlayers)
		{
			LanBeaconPayload payload = new LanBeaconPayload
			{
				k = OnlineNetConstants.LanBeaconMagic,
				c = info.RoomCode,
				n = info.RoomName,
				h = info.HostName,
				p = info.GamePort,
				i = info.HostVirtualIpv4,
				e = info.ExternalNode,
				x = info.CustomPeer,
				t = info.Transport == OnlineTransportKind.Direct ? "d" : "v",
				a = info.DirectAddress,
				m = memberCount,
				max = maxPlayers,
				pw = info.PasswordRequired,
				v = info.ProtocolVersion,
				l = info.ListenerPort
			};
			m_beaconJson = JsonConvert.SerializeObject(payload);
		}

		private void BroadcastLoop()
		{
			try
			{
				m_broadcastClient = new UdpClient();
				m_broadcastClient.EnableBroadcast = true;
				m_broadcastClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
				m_broadcastClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
				m_broadcastClient.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
			}
			catch (Exception ex)
			{
				m_lastError = "无法创建局域网广播套接字：" + ex.Message;
				Debug.LogWarning("[Online] LAN beacon socket failed: " + ex);
				return;
			}

			List<IPAddress> targets = CollectBroadcastTargets();
			while (!m_broadcastStop)
			{
				string json = m_beaconJson;
				if (!string.IsNullOrEmpty(json))
				{
					byte[] bytes = Encoding.UTF8.GetBytes(json);
					for (int i = 0; i < targets.Count; i++)
					{
						try
						{
							m_broadcastClient.Send(bytes, bytes.Length, new IPEndPoint(targets[i], OnlineNetConstants.LanBeaconPort));
						}
						catch (Exception)
						{
							// A dead interface must not stop the beacon.
						}
					}
				}
				for (int slept = 0; slept < (int)(BeaconIntervalSeconds * 1000f) && !m_broadcastStop; slept += 100)
				{
					Thread.Sleep(100);
				}
			}
		}

		private static List<IPAddress> CollectBroadcastTargets()
		{
			List<IPAddress> targets = new List<IPAddress>();
			targets.Add(IPAddress.Broadcast);
			try
			{
				NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
				for (int i = 0; i < interfaces.Length; i++)
				{
					NetworkInterface nic = interfaces[i];
					if (nic.OperationalStatus != OperationalStatus.Up)
					{
						continue;
					}
					IPInterfaceProperties properties = nic.GetIPProperties();
					UnicastIPAddressInformationCollection unicast = properties.UnicastAddresses;
					for (int j = 0; j < unicast.Count; j++)
					{
						UnicastIPAddressInformation address = unicast[j];
						if (address.Address.AddressFamily != AddressFamily.InterNetwork || address.IPv4Mask == null)
						{
							continue;
						}
						IPAddress directed = GetDirectedBroadcast(address.Address, address.IPv4Mask);
						if (directed != null && !targets.Contains(directed))
						{
							targets.Add(directed);
						}
					}
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[Online] Could not enumerate network interfaces: " + ex.Message);
			}
			// The EasyTier virtual adapter's own broadcast address, so virtual-LAN members see it
			// even when the TUN driver swallows 255.255.255.255.
			IPAddress virtualBroadcast = new IPAddress(new byte[] { 10, 126, 126, 255 });
			if (!targets.Contains(virtualBroadcast))
			{
				targets.Add(virtualBroadcast);
			}
			return targets;
		}

		private static IPAddress GetDirectedBroadcast(IPAddress address, IPAddress mask)
		{
			byte[] addressBytes = address.GetAddressBytes();
			byte[] maskBytes = mask.GetAddressBytes();
			if (addressBytes.Length != 4 || maskBytes.Length != 4)
			{
				return null;
			}
			byte[] broadcast = new byte[4];
			for (int i = 0; i < 4; i++)
			{
				broadcast[i] = (byte)(addressBytes[i] | (byte)~maskBytes[i]);
			}
			return new IPAddress(broadcast);
		}

		// ---------------------------------------------------------------- listening

		public void StartListening()
		{
			if (m_listenThread != null)
			{
				return;
			}
			m_listenStop = false;
			m_listenThread = new Thread(ListenLoop);
			m_listenThread.IsBackground = true;
			m_listenThread.Name = "BPRC LAN browser";
			m_listenThread.Start();
		}

		public void StopListening()
		{
			m_listenStop = true;
			if (m_listenClient != null)
			{
				try
				{
					m_listenClient.Close();
				}
				catch (Exception)
				{
				}
				m_listenClient = null;
			}
			if (m_listenThread != null)
			{
				try
				{
					m_listenThread.Join(500);
				}
				catch (Exception)
				{
				}
				m_listenThread = null;
			}
		}

		private void ListenLoop()
		{
			try
			{
				m_listenClient = new UdpClient();
				m_listenClient.ExclusiveAddressUse = false;
				m_listenClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
				m_listenClient.Client.Bind(new IPEndPoint(IPAddress.Any, OnlineNetConstants.LanBeaconPort));
				m_listenClient.EnableBroadcast = true;
			}
			catch (Exception ex)
			{
				m_lastError = "无法监听局域网房间广播：" + ex.Message;
				Debug.LogWarning("[Online] LAN browser socket failed: " + ex);
				return;
			}

			IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
			while (!m_listenStop)
			{
				try
				{
					byte[] data = m_listenClient.Receive(ref remote);
					HandleDatagram(data, remote);
				}
				catch (SocketException)
				{
					if (m_listenStop)
					{
						break;
					}
				}
				catch (ObjectDisposedException)
				{
					break;
				}
				catch (Exception ex)
				{
					if (m_listenStop)
					{
						break;
					}
					Debug.LogWarning("[Online] LAN beacon receive failed: " + ex.Message);
				}
			}
		}

		private void HandleDatagram(byte[] data, IPEndPoint remote)
		{
			try
			{
				string json = Encoding.UTF8.GetString(data);
				LanBeaconPayload payload = JsonConvert.DeserializeObject<LanBeaconPayload>(json);
				if (payload == null || payload.k != OnlineNetConstants.LanBeaconMagic)
				{
					return;
				}
				string code = OnlineRoomCode.Normalize(payload.c);
				if (code.Length == 0)
				{
					return;
				}
				OnlineLanRoom room = new OnlineLanRoom
				{
					Address = remote.Address.ToString(),
					MemberCount = payload.m,
					MaxPlayers = payload.max,
					PasswordRequired = payload.pw,
					LastSeenRealtime = Time.realtimeSinceStartup,
					Info = new OnlineRoomInfo
					{
						RoomCode = code,
						RoomName = payload.n,
						HostName = payload.h,
						GamePort = payload.p <= 0 ? OnlineNetConstants.DefaultGamePort : (ushort)payload.p,
						HostVirtualIpv4 = string.IsNullOrWhiteSpace(payload.i) ? OnlineNetConstants.DefaultHostVirtualIpv4 : payload.i,
						ExternalNode = payload.e,
						CustomPeer = payload.x,
						Transport = payload.t == "d" ? OnlineTransportKind.Direct : OnlineTransportKind.VirtualLan,
						DirectAddress = string.IsNullOrWhiteSpace(payload.a) ? remote.Address.ToString() : payload.a,
						ListenerPort = payload.l <= 0 ? OnlineNetConstants.DefaultListenerPort : (ushort)payload.l,
						PasswordRequired = payload.pw,
						ProtocolVersion = payload.v
					}
				};
				m_incoming.Enqueue(room);
			}
			catch (Exception)
			{
				// Garbage on the port is expected; ignore it.
			}
		}

		private void MergeRoom(OnlineLanRoom room)
		{
			for (int i = 0; i < m_rooms.Count; i++)
			{
				OnlineLanRoom existing = m_rooms[i];
				if (existing.Info != null && existing.Info.RoomCode == room.Info.RoomCode)
				{
					bool changed = existing.MemberCount != room.MemberCount ||
						existing.MaxPlayers != room.MaxPlayers ||
						existing.Info.RoomName != room.Info.RoomName ||
						existing.Address != room.Address;
					m_rooms[i] = room;
					if (changed)
					{
						m_roomsDirty = true;
					}
					return;
				}
			}
			m_rooms.Add(room);
			m_rooms.Sort((OnlineLanRoom a, OnlineLanRoom b) => string.CompareOrdinal(a.DisplayName, b.DisplayName));
			m_roomsDirty = true;
		}

		private bool PruneRooms()
		{
			bool removed = false;
			for (int i = m_rooms.Count - 1; i >= 0; i--)
			{
				if (m_rooms[i] == null || m_rooms[i].AgeSeconds > RoomTimeoutSeconds)
				{
					m_rooms.RemoveAt(i);
					removed = true;
				}
			}
			return removed;
		}
	}
}
