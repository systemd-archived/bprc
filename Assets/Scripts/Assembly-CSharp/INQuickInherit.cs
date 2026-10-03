using System.Collections.Generic;
using System.IO;
using UnityEngine;

// 快捷继承：记住“上一个部件”的自定义数值，并应用到“下一个同类部件”（建造界面）。
// 记忆表会持久化到 Settings/QuickInherit.json，因此域重载 / 重启后依然有效。
public static class INQuickInherit
{
	// key = 部件类型 + 皮肤索引；value = 自定义参数字符串
	private static readonly Dictionary<string, string> s_lastParams = new Dictionary<string, string>();
	private static bool s_loaded;

	[System.Serializable]
	private class Store
	{
		public List<string> keys = new List<string>();
		public List<string> values = new List<string>();
	}

	public static bool Enabled
	{
		get { return INParamSystem.Enabled && PlayerPrefs.GetInt("INQuickInheritOn", 1) != 0; }
		set
		{
			PlayerPrefs.SetInt("INQuickInheritOn", value ? 1 : 0);
			PlayerPrefs.Save();
		}
	}

	private static string FilePath
	{
		get
		{
			try
			{
				if (string.IsNullOrEmpty(INUnity.SettingsPath))
				{
					return null;
				}
				return INUnity.SettingsPath + "/QuickInherit.json";
			}
			catch
			{
				return null;
			}
		}
	}

	private static void EnsureLoaded()
	{
		if (s_loaded)
		{
			return;
		}
		s_loaded = true;
		try
		{
			string path = FilePath;
			if (path != null && File.Exists(path))
			{
				Store st = JsonUtility.FromJson<Store>(File.ReadAllText(path));
				if (st != null && st.keys != null && st.values != null)
				{
					int n = Mathf.Min(st.keys.Count, st.values.Count);
					for (int i = 0; i < n; i++)
					{
						if (!string.IsNullOrEmpty(st.keys[i]))
						{
							s_lastParams[st.keys[i]] = st.values[i];
						}
					}
				}
			}
		}
		catch
		{
		}
	}

	private static void Flush()
	{
		try
		{
			string path = FilePath;
			if (path == null)
			{
				return;
			}
			Store st = new Store();
			foreach (KeyValuePair<string, string> kv in s_lastParams)
			{
				st.keys.Add(kv.Key);
				st.values.Add(kv.Value);
			}
			File.WriteAllText(path, JsonUtility.ToJson(st));
		}
		catch
		{
		}
	}

	private static string KeyOf(BasePart part)
	{
		return ((int)part.m_partType).ToString() + "_" + part.customPartIndex;
	}

	// 记住某个部件当前的自定义数值（长按面板保存后调用）。
	public static void Remember(BasePart part)
	{
		if (part == null)
		{
			return;
		}
		EnsureLoaded();
		string key = KeyOf(part);
		string own = part.SerializeCustomParams();
		if (string.IsNullOrEmpty(own))
		{
			s_lastParams.Remove(key);
		}
		else
		{
			s_lastParams[key] = own;
		}
		Flush();
		Debug.Log("[QuickInherit] Remember key=" + key + " params='" + own + "'");
	}

	// 部件放置完成后调用：自带数值则记下，否则继承同类型记住的数值。
	public static void OnPlaced(BasePart part)
	{
		if (part == null)
		{
			return;
		}
		EnsureLoaded();
		string key = KeyOf(part);
		string own = part.SerializeCustomParams();
		string rem;
		bool hasRem = s_lastParams.TryGetValue(key, out rem) && !string.IsNullOrEmpty(rem);
		Debug.Log("[QuickInherit] OnPlaced key=" + key + " enabled=" + Enabled + " own='" + own + "' rem='" + (hasRem ? rem : "") + "'");
		if (!Enabled)
		{
			return;
		}
		if (!string.IsNullOrEmpty(own))
		{
			s_lastParams[key] = own;
			Flush();
			return;
		}
		if (hasRem)
		{
			part.DeserializeCustomParams(rem);
			Debug.Log("[QuickInherit] Applied '" + rem + "' -> " + key);
		}
	}

	public static void Clear()
	{
		s_lastParams.Clear();
		Flush();
	}
}
