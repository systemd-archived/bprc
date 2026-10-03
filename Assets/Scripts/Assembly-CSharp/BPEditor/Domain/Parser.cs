using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

// 1:1 移植 badpiggies-editor 的 domain/parser.rs（.bytes 关卡二进制读写，对应 LevelLoader.cs 的 BinaryReader/Writer 格式）
public class BpBinaryReader
{
	private readonly byte[] m_data;
	private int m_pos;

	public BpBinaryReader(byte[] data) { m_data = data ?? new byte[0]; }

	public int Position => m_pos;
	public int Length => m_data.Length;

	public byte ReadByte()
	{
		if (m_pos >= m_data.Length) throw new EndOfStreamException("BpBinaryReader: out of data");
		return m_data[m_pos++];
	}

	public bool ReadBoolean() => ReadByte() != 0;

	public short ReadInt16()
	{
		byte[] b = ReadRaw(2);
		return (short)(b[0] | (b[1] << 8));
	}

	public int ReadInt32() => BitConverter.ToInt32(ReadRaw(4), 0);

	public uint ReadUInt32() => BitConverter.ToUInt32(ReadRaw(4), 0);

	public float ReadSingle() => BitConverter.ToSingle(ReadRaw(4), 0);

	// .NET BinaryWriter 字符串：7-bit 长度前缀 + UTF-8
	public string ReadString()
	{
		uint len = Read7BitEncodedInt();
		return Encoding.UTF8.GetString(ReadRaw((int)len));
	}

	public byte[] ReadBytes(int count) => ReadRaw(count);

	private byte[] ReadRaw(int count)
	{
		if (count < 0 || m_pos + count > m_data.Length)
			throw new EndOfStreamException("BpBinaryReader: need " + count + " bytes, have " + (m_data.Length - m_pos));
		byte[] buf = new byte[count];
		Array.Copy(m_data, m_pos, buf, 0, count);
		m_pos += count;
		return buf;
	}

	private uint Read7BitEncodedInt()
	{
		uint result = 0;
		int shift = 0;
		while (true)
		{
			byte b = ReadByte();
			result |= (uint)(b & 0x7f) << shift;
			shift += 7;
			if ((b & 0x80) == 0) break;
			if (shift >= 35) throw new InvalidDataException("BpBinaryReader: bad 7-bit encoded int");
		}
		return result;
	}
}

public class BpBinaryWriter
{
	private readonly List<byte> m_buf = new List<byte>();

	public byte[] ToBytes() => m_buf.ToArray();
	public int Length => m_buf.Count;

	public void WriteByte(byte v) => m_buf.Add(v);
	public void WriteBoolean(bool v) => WriteByte(v ? (byte)1 : (byte)0);
	public void WriteInt16(short v) => m_buf.AddRange(BitConverter.GetBytes(v));
	public void WriteInt32(int v) => m_buf.AddRange(BitConverter.GetBytes(v));
	public void WriteUInt32(uint v) => m_buf.AddRange(BitConverter.GetBytes(v));
	public void WriteSingle(float v) => m_buf.AddRange(BitConverter.GetBytes(v));

	public void WriteString(string v)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(v ?? "");
		Write7BitEncodedInt((uint)bytes.Length);
		m_buf.AddRange(bytes);
	}

	public void WriteBytes(byte[] data) { if (data != null) m_buf.AddRange(data); }

	private void Write7BitEncodedInt(uint val)
	{
		do
		{
			byte b = (byte)(val & 0x7f);
			val >>= 7;
			if (val != 0) b |= 0x80;
			WriteByte(b);
		} while (val != 0);
	}
}

public static class BpParser
{
	// ---------------- 解析 ----------------

	public static bool TryParseLevel(byte[] data, out BpLevelData level, out string error)
	{
		level = null;
		error = null;
		try
		{
			level = ParseLevel(data);
			return true;
		}
		catch (Exception ex)
		{
			error = ex.Message;
			level = null;
			return false;
		}
	}

	public static BpLevelData ParseLevel(byte[] data)
	{
		BpBinaryReader reader = new BpBinaryReader(data);
		int objectCount = reader.ReadInt32();

		BpLevelData level = new BpLevelData();
		for (int i = 0; i < objectCount; i++)
		{
			int idx = ReadObject(reader, level, -1, false);
			level.roots.Add(idx);
		}
		return level;
	}

	private static int ReadObject(BpBinaryReader reader, BpLevelData level, int parent, bool hasParent)
	{
		short childCount = reader.ReadInt16();
		if (childCount == 0) return ReadPrefabInstance(reader, level, parent, hasParent);
		return ReadParentObject(childCount, reader, level, parent, hasParent);
	}

	private static int ReadPrefabInstance(BpBinaryReader reader, BpLevelData level, int parent, bool hasParent)
	{
		BpPrefabInstance p = new BpPrefabInstance();
		p.name = reader.ReadString();
		p.prefabIndex = reader.ReadInt16();
		p.position = ReadVector3(reader);
		p.rotation = ReadVector3(reader);
		p.scale = ReadVector3(reader);

		byte dataTypeByte = reader.ReadByte();
		p.dataType = BpDataTypeExt.FromByte(dataTypeByte);

		if (p.dataType == BpDataType.Terrain) p.terrainData = ReadTerrain(reader);
		else if (p.dataType == BpDataType.PrefabOverrides) p.overrideData = ReadPrefabOverrides(reader);

		p.hasParent = hasParent;
		p.parent = parent;

		int idx = level.objects.Count;
		level.objects.Add(BpLevelObject.OfPrefab(p));
		return idx;
	}

	private static int ReadParentObject(short childCount, BpBinaryReader reader, BpLevelData level, int parent, bool hasParent)
	{
		BpParentObject p = new BpParentObject();
		p.name = reader.ReadString();
		p.position = ReadVector3(reader);
		p.hasParent = hasParent;
		p.parent = parent;
		p.children = new List<int>();

		int idx = level.objects.Count;
		level.objects.Add(BpLevelObject.OfParent(p));

		List<int> children = new List<int>();
		for (int i = 0; i < childCount; i++)
			children.Add(ReadObject(reader, level, idx, true));

		level.objects[idx].parentObj.children = children;
		return idx;
	}

	private static Vector2 ReadVector2(BpBinaryReader reader) => new Vector2(reader.ReadSingle(), reader.ReadSingle());

	private static Vector3 ReadVector3(BpBinaryReader reader) => new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

	private static Color ReadColor(BpBinaryReader reader) => BpColorPacked.FromPacked(reader.ReadUInt32());

	private static TTerrData ReadTerrain(BpBinaryReader reader)
	{
		TTerrData t = new TTerrData();
		t.fillTextureTileOffsetX = reader.ReadSingle();
		t.fillTextureTileOffsetY = reader.ReadSingle();
		t.fillMesh = ReadMesh(reader);
		t.fillColor = ReadColor(reader);
		t.fillTextureIndex = reader.ReadInt32();
		t.curveMesh = ReadMesh(reader);

		int curveTextureCount = reader.ReadInt32();
		t.curveTextures = new List<TCurveTexture>(curveTextureCount);
		for (int i = 0; i < curveTextureCount; i++)
		{
			TCurveTexture ct = new TCurveTexture(null);
			ct.textureIndex = reader.ReadInt32();
			ct.size = ReadVector2(reader);
			ct.fixedAngle = reader.ReadBoolean();
			ct.fadeThreshold = reader.ReadSingle();
			t.curveTextures.Add(ct);
		}

		t.controlTextureCount = reader.ReadInt32();
		if (t.controlTextureCount > 0)
		{
			int dataLength = reader.ReadInt32();
			t.controlTextureData = reader.ReadBytes(dataLength);
		}
		else t.controlTextureData = null;

		t.hasCollider = reader.ReadBoolean();
		t.hasFillBoundary = false;
		return t;
	}

	private static TTerrMesh ReadMesh(BpBinaryReader reader)
	{
		int vertexCount = reader.ReadInt32();
		List<Vector2> vertices = new List<Vector2>(vertexCount);
		for (int i = 0; i < vertexCount; i++) vertices.Add(ReadVector2(reader));

		int indexCount = reader.ReadInt32();
		List<int> indices = new List<int>(indexCount);
		for (int i = 0; i < indexCount; i++) indices.Add(reader.ReadInt16());

		return new TTerrMesh { vertices = vertices, indices = indices };
	}

	private static BpPrefabOverrideData ReadPrefabOverrides(BpBinaryReader reader)
	{
		int size = reader.ReadInt32();
		byte[] raw = reader.ReadBytes(size);
		return new BpPrefabOverrideData
		{
			rawText = Encoding.UTF8.GetString(raw),
			rawBytes = raw
		};
	}

	// ---------------- 序列化 ----------------

	public static byte[] SerializeLevel(BpLevelData level)
	{
		BpBinaryWriter w = new BpBinaryWriter();
		w.WriteInt32(level.roots.Count);
		foreach (int rootIdx in level.roots) WriteObject(w, level, rootIdx);
		return w.ToBytes();
	}

	private static void WriteObject(BpBinaryWriter w, BpLevelData level, int idx)
	{
		if (level.objects[idx].isPrefab)
		{
			w.WriteInt16(0);
			WritePrefabInstance(w, level, idx);
		}
		else
		{
			w.WriteInt16((short)level.objects[idx].parentObj.children.Count);
			WriteParentObject(w, level, idx);
		}
	}

	private static void WritePrefabInstance(BpBinaryWriter w, BpLevelData level, int idx)
	{
		BpPrefabInstance obj = level.objects[idx].AsPrefab();
		if (obj == null)
		{
			Debug.LogError("[BpParser] serialize_level invariant violated: object " + idx + " is not a prefab");
			return;
		}

		w.WriteString(obj.name);
		w.WriteInt16(obj.prefabIndex);
		WriteVector3(w, obj.position);
		WriteVector3(w, obj.rotation);
		WriteVector3(w, obj.scale);
		w.WriteByte((byte)obj.dataType);

		if (obj.dataType == BpDataType.Terrain)
		{
			if (obj.terrainData != null) WriteTerrain(w, obj.terrainData);
		}
		else if (obj.dataType == BpDataType.PrefabOverrides && obj.overrideData != null)
		{
			WritePrefabOverrides(w, obj.overrideData);
		}
	}

	private static void WriteParentObject(BpBinaryWriter w, BpLevelData level, int idx)
	{
		BpParentObject obj = level.objects[idx].AsParent();
		if (obj == null)
		{
			Debug.LogError("[BpParser] serialize_level invariant violated: object " + idx + " is not a parent");
			return;
		}
		w.WriteString(obj.name);
		WriteVector3(w, obj.position);
		foreach (int childIdx in obj.children) WriteObject(w, level, childIdx);
	}

	private static void WriteVector2(BpBinaryWriter w, Vector2 v) { w.WriteSingle(v.x); w.WriteSingle(v.y); }

	private static void WriteVector3(BpBinaryWriter w, Vector3 v) { w.WriteSingle(v.x); w.WriteSingle(v.y); w.WriteSingle(v.z); }

	private static void WriteColor(BpBinaryWriter w, Color c) => w.WriteUInt32(BpColorPacked.ToPacked(c));

	private static void WriteMesh(BpBinaryWriter w, TTerrMesh mesh)
	{
		w.WriteInt32(mesh.vertices.Count);
		foreach (Vector2 v in mesh.vertices) WriteVector2(w, v);
		w.WriteInt32(mesh.indices.Count);
		foreach (int i in mesh.indices) w.WriteInt16((short)i);
	}

	private static void WriteTerrain(BpBinaryWriter w, TTerrData t)
	{
		w.WriteSingle(t.fillTextureTileOffsetX);
		w.WriteSingle(t.fillTextureTileOffsetY);
		WriteMesh(w, t.fillMesh);
		WriteColor(w, t.fillColor);
		w.WriteInt32(t.fillTextureIndex);
		WriteMesh(w, t.curveMesh);

		w.WriteInt32(t.curveTextures.Count);
		foreach (TCurveTexture ct in t.curveTextures)
		{
			w.WriteInt32(ct.textureIndex);
			WriteVector2(w, ct.size);
			w.WriteBoolean(ct.fixedAngle);
			w.WriteSingle(ct.fadeThreshold);
		}

		w.WriteInt32(t.controlTextureCount);
		if (t.controlTextureCount > 0 && t.controlTextureData != null)
		{
			w.WriteInt32(t.controlTextureData.Length);
			w.WriteBytes(t.controlTextureData);
		}

		w.WriteBoolean(t.hasCollider);
	}

	private static void WritePrefabOverrides(BpBinaryWriter w, BpPrefabOverrideData data)
	{
		w.WriteInt32(data.rawBytes.Length);
		w.WriteBytes(data.rawBytes);
	}
}
