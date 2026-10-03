using System;
using Mirror;
using UnityEngine;

namespace BadPiggies.Multiplayer.Online
{
	/// <summary>
	/// Sent once by every guest right after Mirror connects: who they are and which machine they
	/// are on, so the host can match them against EasyTier telemetry. Every member of a room plays,
	/// so there is no "fight or watch" flag.
	/// </summary>
	public struct OnlineHelloMessage : NetworkMessage
	{
		public string displayName;
		public string machineName;
		public int protocolVersion;
	}

	/// <summary>Authoritative snapshot of the room settings. The host broadcasts it on every change.</summary>
	public struct OnlineRoomStateMessage : NetworkMessage
	{
		public string roomCode;
		public string roomName;
		public string hostName;
		public string announcement;
		public int maxPlayers;
		/// <summary>Members currently in the room; all of them play.</summary>
		public int players;
		public bool inMatch;
		public int round;
		public ushort gamePort;
	}

	/// <summary>One chat line. Guests send it to the host, the host relays it to everybody.</summary>
	public struct OnlineChatMessage : NetworkMessage
	{
		public uint senderNetId;
		public string sender;
		public string text;
		public long utcTicks;
		public bool isSystem;
	}

	/// <summary>Server telling a guest that it has been removed from the room.</summary>
	public struct OnlineKickMessage : NetworkMessage
	{
		public string reason;
	}

	/// <summary>Per-connection latency / packet loss, gathered from easytier-cli on the host.</summary>
	public struct OnlineMemberStat
	{
		/// <summary>Mirror netId of the member's lobby player; the only id clients can see.</summary>
		public uint netId;
		public string machineName;
		public float latencyMs;
		public float lossRate;
		public string tunnelProtocol;
		public string natType;
	}

	/// <summary>Periodic telemetry broadcast from the host.</summary>
	public struct OnlineMemberStatsMessage : NetworkMessage
	{
		public OnlineMemberStat[] entries;
	}

	/// <summary>
	/// Hand-written readers and writers for the online messages.
	///
	/// The project ships a pre-built Mirror DLL without the Weaver, so nothing generates these
	/// automatically; this is the same approach <c>Mirror.GeneratedNetworkCode</c> already uses
	/// for <c>MultiplayerLoadArenaMessage</c>. Nothing here may be removed or reordered without also
	/// regenerating both sides.
	/// </summary>
	public static class OnlineNetworkSerializers
	{
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		public static void InitReadWriters()
		{
			Writer<OnlineHelloMessage>.write = WriteHello;
			Reader<OnlineHelloMessage>.read = ReadHello;
			Writer<OnlineRoomStateMessage>.write = WriteRoomState;
			Reader<OnlineRoomStateMessage>.read = ReadRoomState;
			Writer<OnlineChatMessage>.write = WriteChat;
			Reader<OnlineChatMessage>.read = ReadChat;
			Writer<OnlineKickMessage>.write = WriteKick;
			Reader<OnlineKickMessage>.read = ReadKick;
			Writer<OnlineMemberStatsMessage>.write = WriteMemberStats;
			Reader<OnlineMemberStatsMessage>.read = ReadMemberStats;
		}

		private static void WriteHello(NetworkWriter writer, OnlineHelloMessage value)
		{
			writer.WriteString(value.displayName ?? "");
			writer.WriteString(value.machineName ?? "");
			writer.WriteVarInt(value.protocolVersion);
		}

		private static OnlineHelloMessage ReadHello(NetworkReader reader)
		{
			return new OnlineHelloMessage
			{
				displayName = reader.ReadString(),
				machineName = reader.ReadString(),
				protocolVersion = reader.ReadVarInt()
			};
		}

		private static void WriteRoomState(NetworkWriter writer, OnlineRoomStateMessage value)
		{
			writer.WriteString(value.roomCode ?? "");
			writer.WriteString(value.roomName ?? "");
			writer.WriteString(value.hostName ?? "");
			writer.WriteString(value.announcement ?? "");
			writer.WriteVarInt(value.maxPlayers);
			writer.WriteVarInt(value.players);
			writer.WriteBool(value.inMatch);
			writer.WriteVarInt(value.round);
			writer.WriteUShort(value.gamePort);
		}

		private static OnlineRoomStateMessage ReadRoomState(NetworkReader reader)
		{
			return new OnlineRoomStateMessage
			{
				roomCode = reader.ReadString(),
				roomName = reader.ReadString(),
				hostName = reader.ReadString(),
				announcement = reader.ReadString(),
				maxPlayers = reader.ReadVarInt(),
				players = reader.ReadVarInt(),
				inMatch = reader.ReadBool(),
				round = reader.ReadVarInt(),
				gamePort = reader.ReadUShort()
			};
		}

		private static void WriteChat(NetworkWriter writer, OnlineChatMessage value)
		{
			writer.WriteVarUInt(value.senderNetId);
			writer.WriteString(value.sender ?? "");
			writer.WriteString(value.text ?? "");
			writer.WriteLong(value.utcTicks);
			writer.WriteBool(value.isSystem);
		}

		private static OnlineChatMessage ReadChat(NetworkReader reader)
		{
			return new OnlineChatMessage
			{
				senderNetId = reader.ReadVarUInt(),
				sender = reader.ReadString(),
				text = reader.ReadString(),
				utcTicks = reader.ReadLong(),
				isSystem = reader.ReadBool()
			};
		}

		private static void WriteKick(NetworkWriter writer, OnlineKickMessage value)
		{
			writer.WriteString(value.reason ?? "");
		}

		private static OnlineKickMessage ReadKick(NetworkReader reader)
		{
			return new OnlineKickMessage
			{
				reason = reader.ReadString()
			};
		}

		private static void WriteMemberStats(NetworkWriter writer, OnlineMemberStatsMessage value)
		{
			OnlineMemberStat[] entries = value.entries ?? new OnlineMemberStat[0];
			if (entries.Length > OnlineNetConstants.MaxRoomPlayers)
			{
				Array.Resize(ref entries, OnlineNetConstants.MaxRoomPlayers);
			}
			writer.WriteVarInt(entries.Length);
			for (int i = 0; i < entries.Length; i++)
			{
				writer.WriteVarUInt(entries[i].netId);
				writer.WriteString(entries[i].machineName ?? "");
				writer.WriteFloat(entries[i].latencyMs);
				writer.WriteFloat(entries[i].lossRate);
				writer.WriteString(entries[i].tunnelProtocol ?? "");
				writer.WriteString(entries[i].natType ?? "");
			}
		}

		private static OnlineMemberStatsMessage ReadMemberStats(NetworkReader reader)
		{
			int count = reader.ReadVarInt();
			if (count < 0 || count > OnlineNetConstants.MaxRoomPlayers)
			{
				count = 0;
			}
			OnlineMemberStat[] entries = new OnlineMemberStat[count];
			for (int i = 0; i < count; i++)
			{
				entries[i] = new OnlineMemberStat
				{
					netId = reader.ReadVarUInt(),
					machineName = reader.ReadString(),
					latencyMs = reader.ReadFloat(),
					lossRate = reader.ReadFloat(),
					tunnelProtocol = reader.ReadString(),
					natType = reader.ReadString()
				};
			}
			return new OnlineMemberStatsMessage
			{
				entries = entries
			};
		}
	}
}
