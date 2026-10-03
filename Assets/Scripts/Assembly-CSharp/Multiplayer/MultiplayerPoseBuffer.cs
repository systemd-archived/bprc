using System;
using UnityEngine;

namespace BadPiggies.Multiplayer
{
public sealed class MultiplayerPoseBuffer
{
	private sealed class Frame
	{
		public long Stream;

		public bool Used;

		public Vector3[] Positions;

		public float[] Angles;

		public bool[] Active;

		public bool[] Present;
	}

	private const int Capacity = 8;

	public const float RenderDelay = 0.1f;

	private const double SnapTicks = 6.0;

	private readonly Frame[] m_frames = new Frame[8];

	private int m_partCount;

	private bool m_haveTick;

	private ushort m_lastTick;

	private long m_lastStream;

	private double m_playhead;

	private bool m_playing;

	private Frame m_older;

	private Frame m_newer;

	private float m_blend;

	public bool HasPose => m_newer != null;

	public void Resize(int partCount)
	{
		m_partCount = Mathf.Max(0, partCount);
		for (int i = 0; i < 8; i++)
		{
			m_frames[i] = new Frame
			{
				Positions = new Vector3[m_partCount],
				Angles = new float[m_partCount],
				Active = new bool[m_partCount],
				Present = new bool[m_partCount]
			};
		}
		m_haveTick = false;
		m_playing = false;
		m_older = null;
		m_newer = null;
	}

	public void Write(byte[] blob, int start, int count, ushort tick)
	{
		if (m_partCount == 0 || start >= m_partCount)
		{
			return;
		}
		Frame frame = FrameFor(ToStream(tick));
		if (frame == null)
		{
			return;
		}
		int num = Mathf.Min(count, m_partCount - start);
		for (int i = 0; i < num; i++)
		{
			if (!MultiplayerSnapshot.ReadPart(blob, i, out var position, out var angleDegrees))
			{
				frame.Present[start + i] = false;
				continue;
			}
			frame.Positions[start + i] = position;
			frame.Angles[start + i] = angleDegrees;
			frame.Active[start + i] = MultiplayerSnapshot.ReadActive(blob, count, i);
			frame.Present[start + i] = true;
		}
	}

	private long ToStream(ushort tick)
	{
		if (!m_haveTick)
		{
			m_haveTick = true;
			m_lastTick = tick;
			m_lastStream = 0L;
			return 0L;
		}
		long num = m_lastStream + (short)(tick - m_lastTick);
		if (num > m_lastStream)
		{
			m_lastTick = tick;
			m_lastStream = num;
		}
		return num;
	}

	private Frame FrameFor(long stream)
	{
		for (int i = 0; i < 8; i++)
		{
			if (m_frames[i].Used && m_frames[i].Stream == stream)
			{
				return m_frames[i];
			}
		}
		int num = -1;
		for (int j = 0; j < 8; j++)
		{
			if (!m_frames[j].Used)
			{
				Reset(m_frames[j], stream);
				return m_frames[j];
			}
			if (num < 0 || m_frames[j].Stream < m_frames[num].Stream)
			{
				num = j;
			}
		}
		if (stream <= m_frames[num].Stream)
		{
			return null;
		}
		Reset(m_frames[num], stream);
		return m_frames[num];
	}

	private static void Reset(Frame frame, long stream)
	{
		frame.Stream = stream;
		frame.Used = true;
		for (int i = 0; i < frame.Present.Length; i++)
		{
			frame.Present[i] = false;
		}
	}

	public void Advance(float deltaTime)
	{
		m_older = null;
		m_newer = null;
		m_blend = 0f;
		if (m_haveTick && m_partCount != 0)
		{
			double num = (float)m_lastStream - 2f;
			if (!m_playing || Math.Abs(num - m_playhead) > 6.0)
			{
				m_playhead = num;
				m_playing = true;
			}
			else
			{
				double num2 = Math.Max(-0.1, Math.Min(0.1, num - m_playhead));
				m_playhead += (double)(deltaTime / 0.05f) * (1.0 + num2);
			}
			if (m_playhead > (double)m_lastStream)
			{
				m_playhead = m_lastStream;
			}
			Select();
		}
	}

	private void Select()
	{
		for (int i = 0; i < 8; i++)
		{
			Frame frame = m_frames[i];
			if (!frame.Used)
			{
				continue;
			}
			if ((double)frame.Stream <= m_playhead)
			{
				if (m_older == null || frame.Stream > m_older.Stream)
				{
					m_older = frame;
				}
			}
			else if (m_newer == null || frame.Stream < m_newer.Stream)
			{
				m_newer = frame;
			}
		}
		if (m_older == null)
		{
			m_older = m_newer;
		}
		if (m_newer == null)
		{
			m_newer = m_older;
		}
		if (m_older != null)
		{
			long num = m_newer.Stream - m_older.Stream;
			m_blend = ((num > 0) ? Mathf.Clamp01((float)((m_playhead - (double)m_older.Stream) / (double)num)) : 0f);
		}
	}

	public bool TryGetPose(int index, out Vector3 position, out float angleDegrees)
	{
		position = Vector3.zero;
		angleDegrees = 0f;
		if (m_newer == null || index < 0 || index >= m_partCount)
		{
			return false;
		}
		bool flag = m_newer.Present[index];
		bool flag2 = m_older != null && m_older.Present[index];
		if ((flag & flag2) && m_older != m_newer)
		{
			position = Vector3.Lerp(m_older.Positions[index], m_newer.Positions[index], m_blend);
			angleDegrees = Mathf.LerpAngle(m_older.Angles[index], m_newer.Angles[index], m_blend);
			return true;
		}
		if (flag)
		{
			position = m_newer.Positions[index];
			angleDegrees = m_newer.Angles[index];
			return true;
		}
		if (flag2)
		{
			position = m_older.Positions[index];
			angleDegrees = m_older.Angles[index];
			return true;
		}
		return false;
	}

	public bool ActiveAt(int index)
	{
		if (m_newer == null || index < 0 || index >= m_partCount)
		{
			return false;
		}
		if (m_newer.Present[index])
		{
			return m_newer.Active[index];
		}
		if (m_older != null && m_older.Present[index])
		{
			return m_older.Active[index];
		}
		return false;
	}
}
}
