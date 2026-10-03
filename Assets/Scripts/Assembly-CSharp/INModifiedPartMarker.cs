using System.Collections.Generic;
using UnityEngine;

// 显示修改：在建造界面给“数值不是原版（带自定义参数）”的部件，用方框框住它所在的格子。
public static class INModifiedPartMarker
{
	public const string MarkerName = "INModifiedMark";
	private const float Thickness = 0.05f;
	private const float Inset = 0.04f;
	private const float MarkerZ = -1.6f;

	private static Material s_mat;
	// 自己创建的所有标记（按部件实例ID）——销毁不依赖 ContraptionProto 是否还在。
	private static readonly Dictionary<int, GameObject> s_markers = new Dictionary<int, GameObject>();
	private static readonly HashSet<int> s_live = new HashSet<int>();
	private static readonly List<int> s_stale = new List<int>();

	public static bool Enabled
	{
		get { return INParamSystem.Enabled && PlayerPrefs.GetInt("INShowModifiedOn", 1) != 0; }
		set
		{
			PlayerPrefs.SetInt("INShowModifiedOn", value ? 1 : 0);
			PlayerPrefs.Save();
		}
	}

	private static Material GetMaterial()
	{
		if (s_mat == null)
		{
			Shader sh = Shader.Find("Sprites/Default");
			if (sh == null)
			{
				sh = Shader.Find("UI/Default");
			}
			s_mat = new Material(sh);
			s_mat.color = new Color(1f, 1f, 0f, 0.9f);
		}
		return s_mat;
	}

	public static bool IsBuildState()
	{
		LevelManager lm = WPFMonoBehaviour.levelManager;
		if (lm == null)
		{
			return false;
		}
		return lm.gameState == LevelManager.GameState.Building
			|| lm.gameState == LevelManager.GameState.PausedWhileBuilding
			|| lm.gameState == LevelManager.GameState.PreviewWhileBuilding;
	}

	public static void SyncAll()
	{
		LevelManager lm = WPFMonoBehaviour.levelManager;
		bool show = Enabled && IsBuildState();
		if (!show || lm == null || lm.ContraptionProto == null)
		{
			ClearAll();
			return;
		}
		var parts = lm.ContraptionProto.Parts;
		HashSet<int> live = s_live;
		live.Clear();
		for (int i = 0; i < parts.Count; i++)
		{
			if (parts[i] != null)
			{
				live.Add(parts[i].GetInstanceID());
			}
		}
		// 清掉已经不存在的部件对应的框
		List<int> stale = s_stale;
		stale.Clear();
		foreach (KeyValuePair<int, GameObject> kv in s_markers)
		{
			if (kv.Value == null || !live.Contains(kv.Key))
			{
				if (kv.Value != null)
				{
					UnityEngine.Object.Destroy(kv.Value);
				}
				stale.Add(kv.Key);
			}
		}
		for (int i = 0; i < stale.Count; i++)
		{
			s_markers.Remove(stale[i]);
		}

		for (int i = 0; i < parts.Count; i++)
		{
			Sync(parts[i], true);
		}
	}

	/// <summary>
	/// Whether the part carries a parameter the player chose, which is what the badge means.
	/// </summary>
	/// <remarks>
	/// Not <c>!string.IsNullOrEmpty(SerializeCustomParams())</c> any more: internal state is stored in the
	/// same place (it is the only per-part channel that survives a save) and a part whose only entry is
	/// internal has not been modified by anyone. Without this every part placed in 无网格模式 would wear a
	/// "modified" badge it did not earn.
	/// </remarks>
	private static bool HasPlayerParam(BasePart part)
	{
		if (part == null) return false;
		foreach (KeyValuePair<string, float> pair in part.CustomParams)
		{
			if (!BasePart.IsInternalParam(pair.Key)) return true;
		}
		return false;
	}

	public static void Sync(BasePart part, bool show)
	{		if (part == null)
		{
			return;
		}
		int id = part.GetInstanceID();
		bool marked = show && HasPlayerParam(part);
		if (!marked)
		{
			RemoveMarker(id);
			return;
		}
		GameObject marker;
		if (!s_markers.TryGetValue(id, out marker) || marker == null)
		{
			marker = CreateMarker(part);
			s_markers[id] = marker;
		}
		PositionMarker(marker.transform, part, WPFMonoBehaviour.levelManager);
	}

	private static GameObject CreateMarker(BasePart part)
	{
		GameObject go = new GameObject(MarkerName + "_" + part.GetInstanceID());
		Transform parent = part.transform.parent != null ? part.transform.parent : part.transform;
		go.transform.SetParent(parent, false);
		go.transform.localRotation = Quaternion.identity;
		go.transform.localScale = Vector3.one;
		Material mat = GetMaterial();
		AddBar(go.transform, mat);
		AddBar(go.transform, mat);
		AddBar(go.transform, mat);
		AddBar(go.transform, mat);
		go.AddComponent<INModifiedMarkerKeeper>();
		return go;
	}

	private static void AddBar(Transform parent, Material mat)
	{
		GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Quad);
		bar.name = "Bar";
		Collider col = bar.GetComponent<Collider>();
		if (col != null)
		{
			UnityEngine.Object.Destroy(col);
		}
		bar.transform.SetParent(parent, false);
		bar.transform.localRotation = Quaternion.identity;
		Renderer r = bar.GetComponent<Renderer>();
		if (r != null)
		{
			r.sharedMaterial = mat;
			r.sortingOrder = 31000;
		}
	}

	private static void PositionMarker(Transform marker, BasePart part, LevelManager lm)
	{
		if (marker == null || lm == null)
		{
			return;
		}
		int minX = part.m_coordX + part.m_gridXmin;
		int maxX = part.m_coordX + part.m_gridXmax;
		int minY = part.m_coordY + part.m_gridYmin;
		int maxY = part.m_coordY + part.m_gridYmax;
		if (maxX < minX)
		{
			maxX = minX;
		}
		if (maxY < minY)
		{
			maxY = minY;
		}
		Vector3 origin = lm.StartingPosition;
		float cx = origin.x + (minX + maxX) * 0.5f;
		float cy = origin.y + (minY + maxY) * 0.5f;
		float w = maxX - minX + 1f;
		float h = maxY - minY + 1f;
		marker.position = new Vector3(cx, cy, MarkerZ);

		float fw = Mathf.Max(Thickness * 2f, w - Inset * 2f);
		float fh = Mathf.Max(Thickness * 2f, h - Inset * 2f);
		float t = Thickness;
		float halfW = fw * 0.5f;
		float halfH = fh * 0.5f;
		SetBar(marker, 0, new Vector2(0f, -(halfH - t * 0.5f)), new Vector2(fw, t));
		SetBar(marker, 1, new Vector2(0f, halfH - t * 0.5f), new Vector2(fw, t));
		SetBar(marker, 2, new Vector2(-(halfW - t * 0.5f), 0f), new Vector2(t, fh));
		SetBar(marker, 3, new Vector2(halfW - t * 0.5f, 0f), new Vector2(t, fh));
	}

	private static void SetBar(Transform marker, int index, Vector2 pos, Vector2 size)
	{
		if (index >= marker.childCount)
		{
			return;
		}
		Transform bar = marker.GetChild(index);
		bar.localPosition = new Vector3(pos.x, pos.y, 0f);
		bar.localScale = new Vector3(size.x, size.y, 1f);
	}

	private static void RemoveMarker(int id)
	{
		GameObject marker;
		if (s_markers.TryGetValue(id, out marker))
		{
			if (marker != null)
			{
				UnityEngine.Object.Destroy(marker);
			}
			s_markers.Remove(id);
		}
	}

	// 销毁所有标记（不依赖 ContraptionProto，游戏开始/关闭功能时调用）。
	public static void ClearAll()
	{
		foreach (KeyValuePair<int, GameObject> kv in s_markers)
		{
			if (kv.Value != null)
			{
				UnityEngine.Object.Destroy(kv.Value);
			}
		}
		s_markers.Clear();
	}
}

// 让标记自己销毁：只要不在建造状态 / 功能关闭，就删掉自己（不依赖其它组件是否在跑）。
public class INModifiedMarkerKeeper : MonoBehaviour
{
	private void Update()
	{
		if (!INModifiedPartMarker.Enabled || !INModifiedPartMarker.IsBuildState())
		{
			Destroy(gameObject);
		}
	}
}
