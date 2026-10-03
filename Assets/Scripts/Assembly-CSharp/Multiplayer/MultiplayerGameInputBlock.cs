using System.Collections.Generic;

namespace BadPiggies.Multiplayer
{
public static class MultiplayerGameInputBlock
{
	private static readonly HashSet<object> s_owners = new HashSet<object>();

	private static bool m_blocked;

	private static bool m_savedGuiEnabled;

	private static readonly object Token = new object();

	private static bool m_pointerGrabbed;

	private static bool m_keysGrabbed;

	public static bool IsBlocked => m_blocked;

	public static void Apply(object owner, bool block)
	{
		if (owner == null)
		{
			return;
		}
		bool flag = (block ? s_owners.Add(owner) : s_owners.Remove(owner));
		bool flag2 = s_owners.Count > 0;
		if (flag2 == m_blocked)
		{
			if (m_blocked && !flag)
			{
				Reassert();
			}
		}
		else if (flag2)
		{
			Acquire();
		}
		else
		{
			Release();
		}
	}

	public static void Release(object owner)
	{
		Apply(owner, block: false);
	}

	private static void Reassert()
	{
		GuiManager instance = Singleton<GuiManager>.Instance;
		if (instance != null)
		{
			if (instance.IsEnabled)
			{
				instance.IsEnabled = false;
				instance.ResetFocus();
			}
			if (!m_pointerGrabbed)
			{
				instance.ResetFocus();
				instance.GrabPointer(Token);
				m_pointerGrabbed = true;
			}
		}
		if (!m_keysGrabbed)
		{
			KeyListener instance2 = Singleton<KeyListener>.Instance;
			if (instance2 != null)
			{
				instance2.GrabFocus(Token);
				m_keysGrabbed = true;
			}
		}
	}

	private static void Acquire()
	{
		GuiManager instance = Singleton<GuiManager>.Instance;
		if (instance != null)
		{
			m_savedGuiEnabled = instance.IsEnabled;
			instance.IsEnabled = false;
			instance.ResetFocus();
			instance.GrabPointer(Token);
			m_pointerGrabbed = true;
		}
		else
		{
			m_savedGuiEnabled = true;
		}
		KeyListener instance2 = Singleton<KeyListener>.Instance;
		if (instance2 != null)
		{
			instance2.GrabFocus(Token);
			m_keysGrabbed = true;
		}
		m_blocked = true;
	}

	private static void Release()
	{
		GuiManager instance = Singleton<GuiManager>.Instance;
		if (instance != null)
		{
			if (m_pointerGrabbed)
			{
				instance.ReleasePointer(Token);
			}
			instance.IsEnabled = m_savedGuiEnabled;
		}
		KeyListener instance2 = Singleton<KeyListener>.Instance;
		if (instance2 != null && m_keysGrabbed)
		{
			instance2.ReleaseFocus(Token);
		}
		m_pointerGrabbed = false;
		m_keysGrabbed = false;
		m_blocked = false;
	}
}
}
