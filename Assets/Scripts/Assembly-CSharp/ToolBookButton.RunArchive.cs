using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UButton = UnityEngine.UI.Button;
using UImage = UnityEngine.UI.Image;
using UText = UnityEngine.UI.Text;

// ToolBookButton 的扩展部分：运行时存档系统 + 运行时框选删除 + 复制粘贴继承“开关状态”
// 拆分到独立文件，避免对 2900+ 行的 ToolBookButton.cs 做大量分散修改。
public partial class ToolBookButton
{
	// ===== 运行时存档数据（长期保存；套用建造存档的槽位/命名/去重/JSON 逻辑） =====

	[Serializable]
	public class RunArchiveUnit
	{
		public int Type;        // SortedPartType
		public int Index;       // customPartIndex
		public float OffX;      // 相对锚点偏移
		public float OffY;
		public float RotZ;      // 世界 Z 轴旋转（度）
		public bool Flipped;
		public bool Enabled;    // 复制时的开关/启用状态
		public string Params;   // 每部件自定义参数 "key:value;key:value"（可空）
	}

	[Serializable]
	public class RunArchiveGroup
	{
		public List<RunArchiveUnit> Units = new List<RunArchiveUnit>();
	}

	[Serializable]
	public class RunArchiveWrapper
	{
		public List<RunArchiveGroup> Slots = new List<RunArchiveGroup>();
		public List<int> SlotIds = new List<int>();
		public List<string> SlotNames = new List<string>();
		public int NextId;
	}

	private readonly List<RunArchiveGroup> m_runArchiveSlots = new List<RunArchiveGroup>();
	private readonly List<int> m_runArchiveIds = new List<int>();
	private readonly List<string> m_runArchiveNames = new List<string>();
	private int m_runArchiveNextId = 1;
	private int m_runArchiveScrollOffset;
	private bool m_runArchiveLoaded;

	// 与被复制部件一一对应：启用状态 / 朝向（用于粘贴后继承“开关状态”，以及再次存档时保留朝向）
	private readonly List<bool> m_runCopyEnabled = new List<bool>();
	private readonly List<bool> m_runCopyFlipped = new List<bool>();
	// 与被复制部件一一对应：每部件自定义参数（克隆/存档时保留）
	private readonly List<string> m_runCopyParams = new List<string>();

	// 运行态第二列新增按钮：存档(写入运行存档) / 删除(把框拖到此处删除)
	private GameObject m_runArchiveSaveObj;
	private GameObject m_runDeleteObj;

	private bool m_renameIsRun;

	// “存档”按钮反馈
	private bool m_runArchiveSaveFlashOk;
	private float m_runArchiveSaveFlashUntil;
	// 拖动运行存档槽到“删除”按钮上时的悬停计时（须停留一段时间才真正删除，防误删）
	private float m_runSlotDeleteHoverStart = -1f;

	private static readonly Color s_runArchiveFilledBg = new Color(0.45f, 0.3f, 0.6f, 0.92f);

	public static bool NewRunSavePending;
	public static bool LastRunSaveDuplicate;
	public static float LastRunSaveDuplicateTime;

	private static string RunArchivePath => INUnity.SettingsPath + "/ToolBookRunClipboard.json";

	// ===== UI 创建 / 布局 =====

	private void CreateRunExtraButtons()
	{
		m_runArchiveSaveObj = CreateButtonObj("RunArchiveSave", m_mainRect.transform);
		m_runArchiveSaveObj.GetComponent<UImage>().color = s_normalBg;
		m_runArchiveSaveObj.GetComponent<UButton>().onClick.AddListener(OnRunArchiveSaveClicked);
		CreateLabel(m_runArchiveSaveObj, "存档", 12, Color.white);
		m_runArchiveSaveObj.SetActive(false);

		m_runDeleteObj = CreateButtonObj("RunDelete", m_mainRect.transform);
		m_runDeleteObj.GetComponent<UImage>().color = new Color(0.8f, 0.1f, 0.1f, 0.9f);
		m_runDeleteObj.GetComponent<UButton>().onClick.AddListener(OnRunDeleteClicked);
		CreateLabel(m_runDeleteObj, "删除", 12, Color.white);
		m_runDeleteObj.SetActive(false);

		LoadRunArchive();
	}

	private void UpdateRunExtraButtons()
	{
		if (m_runArchiveSaveObj == null) return;
		bool show = m_isOpen && m_runStateActive && m_runOptionsShown;
		if (m_runArchiveSaveObj.activeSelf != show) m_runArchiveSaveObj.SetActive(show);
		if (m_runDeleteObj.activeSelf != show) m_runDeleteObj.SetActive(show);
		if (!show) return;

		float mainW = m_mainRect.sizeDelta.x;
		float spacing = 6f * m_size;
		float btnW = 48f * m_size;
		float btnH = 36f * m_size;
		float col2X = -(mainW + spacing + btnW + spacing);
		// 第二列：角度旋钮(0) → 复制(1) → 粘贴(2) → 惯性(3) → 单帧(4) → 存档(5) → 删除(6)
		m_runArchiveSaveObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(col2X, -(5 * (btnH + spacing)));
		m_runDeleteObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(col2X, -(6 * (btnH + spacing)));

		// “存档”按钮反馈：成功=绿，失败(无可存档/重复)=红，约 1 秒后恢复
		UImage saveBg = m_runArchiveSaveObj.GetComponent<UImage>();
		if (saveBg != null)
		{
			Color c = s_normalBg;
			if (Time.unscaledTime < m_runArchiveSaveFlashUntil)
				c = m_runArchiveSaveFlashOk ? s_speedOnBg : new Color(0.8f, 0.1f, 0.1f, 0.9f);
			if (saveBg.color != c) saveBg.color = c;
		}
	}

	private bool IsOverRunDeleteButton()
	{
		if (m_runDeleteObj == null || !m_runDeleteObj.activeSelf) return false;
		RectTransform rt = m_runDeleteObj.GetComponent<RectTransform>();
		return rt != null && IsOverRect(rt);
	}

	// ===== 存档按钮 / 删除按钮 =====

	private void OnRunArchiveSaveClicked()
	{
		SaveRunCopyToArchive();
	}

	private void OnRunDeleteClicked()
	{
		if (m_pausePickedParts.Count > 0)
		{
			DeleteRunParts(m_pausePickedParts);
			m_pauseBoxAngle = 0f;
			ClearPauseBox();
		}
	}

	// ===== 运行时存档：保存 / 读取 / 删除 / 命名 =====

	private void SaveRunCopyToArchive()
	{
		// 没按过“复制”时，用当前框选；连框选也没有时，直接存档**整辆运行中的载具**
		if (m_runCopyParts.Count == 0 && m_pausePickedParts.Count > 0)
			CopyRunSelection();
		if (m_runCopyParts.Count == 0)
			CopyRunFull();

		if (m_runCopyParts.Count == 0)
		{
			Debug.Log("[RunArchive] nothing to save");
			FlashRunArchiveSave(false);
			return;
		}

		RunArchiveGroup group = new RunArchiveGroup();
		for (int i = 0; i < m_runCopyParts.Count; i++)
		{
			BasePart p = m_runCopyParts[i];
			if (p == null) continue;
			RunArchiveUnit u = new RunArchiveUnit();
			u.Type = (int)p.Type.ToSortedPartType();
			u.Index = p.Index;
			if (i < m_runCopyOffsets.Count) { u.OffX = m_runCopyOffsets[i].x; u.OffY = m_runCopyOffsets[i].y; }
			if (i < m_runCopyRots.Count) u.RotZ = m_runCopyRots[i].eulerAngles.z;
			u.Flipped = (i < m_runCopyFlipped.Count) ? m_runCopyFlipped[i] : p.Flipped;
			u.Enabled = (i < m_runCopyEnabled.Count) ? m_runCopyEnabled[i] : SafeIsEnabled(p);
			u.Params = (i < m_runCopyParams.Count) ? m_runCopyParams[i] : p.SerializeCustomParams();
			group.Units.Add(u);
		}
		if (group.Units.Count == 0) return;

		for (int i = 0; i < m_runArchiveSlots.Count; i++)
		{
			if (IsRunGroupDuplicate(group, m_runArchiveSlots[i]))
			{
				LastRunSaveDuplicate = true;
				LastRunSaveDuplicateTime = Time.unscaledTime;
				Debug.Log("[RunArchive] duplicate, skipped");
				FlashRunArchiveSave(false);
				return;
			}
		}
		LastRunSaveDuplicate = false;
		int id = m_runArchiveNextId++;
		m_runArchiveSlots.Add(group);
		m_runArchiveIds.Add(id);
		m_runArchiveNames.Add(id.ToString());
		NewRunSavePending = true;
		SaveRunArchive();
		FlashRunArchiveSave(true);
		Debug.Log("[RunArchive] saved #" + id + " (" + group.Units.Count + " units). Total: " + m_runArchiveSlots.Count);
	}

	// 把“整辆运行中的载具”填入待存档缓冲（没框选也没复制时的兜底）
	private void CopyRunFull()
	{
		var lm = WPFMonoBehaviour.levelManager;
		var cr = lm != null ? lm.ContraptionRunning : null;
		if (cr == null || cr.Parts == null || cr.Parts.Count == 0) return;

		m_runCopyParts.Clear();
		m_runCopyOffsets.Clear();
		m_runCopyRots.Clear();
		m_runCopyVel.Clear();
		m_runCopyAngVel.Clear();
		m_runCopyGroup.Clear();
		m_runCopyEnabled.Clear();
		m_runCopyFlipped.Clear();
		m_runCopyParams.Clear();

		Vector3 sum = Vector3.zero;
		int n = 0;
		for (int i = 0; i < cr.Parts.Count; i++)
		{
			BasePart p = cr.Parts[i];
			if (p != null) { sum += p.transform.position; n++; }
		}
		if (n == 0) return;
		m_runCopyAnchor = sum / n;

		for (int i = 0; i < cr.Parts.Count; i++)
		{
			BasePart p = cr.Parts[i];
			if (p == null) continue;
			m_runCopyParts.Add(p);
			m_runCopyOffsets.Add(p.transform.position - m_runCopyAnchor);
			m_runCopyRots.Add(p.transform.rotation);
			m_runCopyGroup.Add(p.ConnectedComponent);
			m_runCopyEnabled.Add(SafeIsEnabled(p));
			m_runCopyFlipped.Add(p.Flipped);
			m_runCopyParams.Add(p.SerializeCustomParams());
			m_runCopyVel.Add(Vector3.zero);
			m_runCopyAngVel.Add(Vector3.zero);
		}
		Debug.Log("[RunArchive] buffered full contraption (" + m_runCopyParts.Count + " parts)");
	}

	private void FlashRunArchiveSave(bool ok)
	{
		m_runArchiveSaveFlashOk = ok;
		m_runArchiveSaveFlashUntil = Time.unscaledTime + 1f;
	}

	private static bool IsRunGroupDuplicate(RunArchiveGroup a, RunArchiveGroup b)
	{
		if (a == null || b == null) return false;
		if (a.Units.Count != b.Units.Count) return false;
		for (int i = 0; i < a.Units.Count; i++)
		{
			RunArchiveUnit ua = a.Units[i];
			RunArchiveUnit ub = b.Units[i];
			if (ua.Type != ub.Type || ua.Index != ub.Index) return false;
			if (Mathf.Abs(ua.OffX - ub.OffX) > 0.01f || Mathf.Abs(ua.OffY - ub.OffY) > 0.01f) return false;
		}
		return true;
	}

	// 把存档中的一组部件还原为“待粘贴源列表”（模板 prefab + 相对偏移/旋转/状态），
	// 之后复用运行态的 PasteRunClipboard 在指针处克隆落位。
	private void LoadRunArchiveSlot(int idx)
	{
		if (idx < 0 || idx >= m_runArchiveSlots.Count) return;
		RunArchiveGroup g = m_runArchiveSlots[idx];

		m_runCopyParts.Clear();
		m_runCopyOffsets.Clear();
		m_runCopyRots.Clear();
		m_runCopyVel.Clear();
		m_runCopyAngVel.Clear();
		m_runCopyGroup.Clear();
		m_runCopyEnabled.Clear();
		m_runCopyFlipped.Clear();
		m_runCopyParams.Clear();
		m_runCopyAnchor = Vector3.zero;

		var ui = WPFMonoBehaviour.levelManager != null ? WPFMonoBehaviour.levelManager.ConstructionUI : null;
		for (int i = 0; i < g.Units.Count; i++)
		{
			RunArchiveUnit u = g.Units[i];
			BasePart tpl = null;
			if (WPFMonoBehaviour.gameData != null)
			{
				// 直接用存档里的类型/索引取模板，不依赖 ConstructionUI（运行时它可能不可用）
				tpl = WPFMonoBehaviour.gameData.GetCustomPart(((SortedPartType)u.Type).ToPartType(), u.Index);
				if (tpl == null && ui != null)
				{
					ConstructionUI.PartDesc pd = ui.FindPartDesc(((SortedPartType)u.Type).ToPartType());
					if (pd != null && pd.part != null)
						tpl = WPFMonoBehaviour.gameData.GetCustomPart(pd.part.m_partType, u.Index);
				}
			}
			if (tpl == null) continue;

			m_runCopyParts.Add(tpl);
			m_runCopyOffsets.Add(new Vector3(u.OffX, u.OffY, 0f));
			m_runCopyRots.Add(Quaternion.Euler(0f, 0f, u.RotZ));
			m_runCopyVel.Add(Vector3.zero);
			m_runCopyAngVel.Add(Vector3.zero);
			m_runCopyGroup.Add(-1);
			m_runCopyEnabled.Add(u.Enabled);
			m_runCopyFlipped.Add(u.Flipped);
			m_runCopyParams.Add(u.Params);
		}

		// 存档组没有实时场景部件，无法生成幽灵预览，清掉可能的旧预览
		if (m_runPreviewTex != null) { UnityEngine.Object.Destroy(m_runPreviewTex); m_runPreviewTex = null; }
		Debug.Log("[RunArchive] loaded slot " + idx + " (" + m_runCopyParts.Count + " units)");
	}

	private void PasteRunFromArchive(int idx, Vector3 worldPos)
	{
		LoadRunArchiveSlot(idx);
		if (m_runCopyParts.Count == 0) return;
		PasteRunClipboard(worldPos);
	}

	private void DeleteRunArchiveSlot(int idx)
	{
		if (idx < 0 || idx >= m_runArchiveSlots.Count) return;
		m_runArchiveSlots.RemoveAt(idx);
		int removedId = (idx < m_runArchiveIds.Count) ? m_runArchiveIds[idx] : -1;
		if (idx < m_runArchiveIds.Count) m_runArchiveIds.RemoveAt(idx);
		if (idx < m_runArchiveNames.Count) m_runArchiveNames.RemoveAt(idx);
		SaveRunArchive();
		Debug.Log("[RunArchive] deleted slot #" + removedId);
	}

	private void RenameRunArchiveSlot(int idx, string name)
	{
		if (idx < 0 || idx >= m_runArchiveNames.Count) return;
		m_runArchiveNames[idx] = name ?? "";
		SaveRunArchive();
	}

	private void SaveRunArchive()
	{
		try
		{
			RunArchiveWrapper w = new RunArchiveWrapper();
			w.Slots = new List<RunArchiveGroup>(m_runArchiveSlots);
			w.SlotIds = new List<int>(m_runArchiveIds);
			w.SlotNames = new List<string>(m_runArchiveNames);
			w.NextId = m_runArchiveNextId;
			System.IO.File.WriteAllText(RunArchivePath, JsonUtility.ToJson(w, true));
			Debug.Log("[RunArchive] saved to " + RunArchivePath + " (" + w.Slots.Count + " slots)");
		}
		catch (Exception e)
		{
			Debug.LogWarning("[RunArchive] save failed: " + e.Message);
		}
	}

	private void LoadRunArchive()
	{
		if (m_runArchiveLoaded) return;
		m_runArchiveLoaded = true;
		try
		{
			string path = RunArchivePath;
			if (!System.IO.File.Exists(path)) return;
			RunArchiveWrapper w = JsonUtility.FromJson<RunArchiveWrapper>(System.IO.File.ReadAllText(path));
			if (w == null || w.Slots == null) return;
			m_runArchiveSlots.Clear();
			m_runArchiveIds.Clear();
			m_runArchiveNames.Clear();
			for (int i = 0; i < w.Slots.Count; i++)
			{
				if (w.Slots[i] == null) continue;
				m_runArchiveSlots.Add(w.Slots[i]);
			}
			int n = m_runArchiveSlots.Count;
			if (w.SlotIds != null && w.SlotIds.Count == n) m_runArchiveIds.AddRange(w.SlotIds);
			else { for (int i = 0; i < n; i++) m_runArchiveIds.Add(i + 1); }
			if (w.SlotNames != null && w.SlotNames.Count == n) m_runArchiveNames.AddRange(w.SlotNames);
			else { for (int i = 0; i < n; i++) m_runArchiveNames.Add(m_runArchiveIds[i].ToString()); }
			m_runArchiveNextId = w.NextId > 0 ? w.NextId : (n > 0 ? m_runArchiveIds[n - 1] + 1 : 1);
			Debug.Log("[RunArchive] loaded " + n + " slots from " + path);
		}
		catch (Exception e)
		{
			Debug.LogWarning("[RunArchive] load failed: " + e.Message);
		}
	}

	// ===== 运行态第三列：显示运行存档槽位 =====

	private void RefreshRunArchiveSlots()
	{
		if (!m_isOpen) return;
		int pageSize = m_clipSlotObjs.Count;
		if (pageSize <= 0) return;

		int total = m_runArchiveSlots.Count;
		int maxOffset = Mathf.Max(0, total - pageSize);
		if (m_runArchiveScrollOffset > maxOffset) m_runArchiveScrollOffset = maxOffset;
		if (m_runArchiveScrollOffset < 0) m_runArchiveScrollOffset = 0;
		if (NewRunSavePending)
		{
			NewRunSavePending = false;
			m_runArchiveScrollOffset = maxOffset;
		}

		for (int i = 0; i < pageSize; i++)
		{
			int dataIdx = m_runArchiveScrollOffset + i;
			if (dataIdx < total)
			{
				m_clipSlotObjs[i].SetActive(true);
				m_clipSlotBgs[i].color = s_runArchiveFilledBg;
				string nm = (dataIdx < m_runArchiveNames.Count) ? m_runArchiveNames[dataIdx] : null;
				m_clipSlotTexts[i].text = string.IsNullOrEmpty(nm)
					? (dataIdx < m_runArchiveIds.Count ? m_runArchiveIds[dataIdx].ToString() : "-")
					: nm;
			}
			else
			{
				m_clipSlotBgs[i].color = s_clipEmptyBg;
				m_clipSlotTexts[i].text = "-";
			}
		}

		// 复用建造态第三列布局
		float spacing = 6f * m_size;
		float btnH = 36f * m_size;
		float mainW = m_mainRect.sizeDelta.x;
		float btnW = 48f * m_size;
		float col3X = -(mainW + spacing + btnW + spacing + btnW + spacing);
		float scrollX = col3X - btnW;
		bool showPage = total > pageSize;
		float pageOffset = showPage ? (btnH + spacing) : 0f;

		if (m_clipPageUpObj != null)
		{
			m_clipPageUpObj.SetActive(showPage);
			m_clipPageUpObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(scrollX, 0f);
		}
		if (m_clipPageDownObj != null)
		{
			m_clipPageDownObj.SetActive(showPage);
			m_clipPageDownObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(scrollX, -(btnH + spacing));
		}
		if (m_clipPageTextObj != null && m_clipPageText != null)
		{
			m_clipPageTextObj.SetActive(showPage);
			if (showPage)
			{
				int first = m_runArchiveScrollOffset + 1;
				int last = Mathf.Min(m_runArchiveScrollOffset + pageSize, total);
				m_clipPageText.text = first + "-" + last + "/" + total;
				m_clipPageTextObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(scrollX, -(2 * btnH + 2 * spacing));
			}
		}
		for (int i = 0; i < pageSize; i++)
			if (m_clipSlotObjs[i].activeSelf)
				m_clipSlotRects[i].anchoredPosition = new Vector2(col3X, -(pageOffset + i * (btnH + spacing)));
	}

	private void OnRunArchivePageUp()
	{
		int pageSize = m_clipSlotObjs.Count;
		m_runArchiveScrollOffset = Mathf.Max(0, m_runArchiveScrollOffset - pageSize);
	}

	// 拖动运行存档槽时调用：记录指针是否停留在“删除”按钮上（需停留 0.3s 才允许删除，防误删）
	private void RunSlotDragTick(bool overDelete)
	{
		if (overDelete)
		{
			if (m_runSlotDeleteHoverStart < 0f) m_runSlotDeleteHoverStart = Time.unscaledTime;
		}
		else m_runSlotDeleteHoverStart = -1f;
	}

	private bool RunSlotDeleteArmed()
	{
		return m_runSlotDeleteHoverStart >= 0f && (Time.unscaledTime - m_runSlotDeleteHoverStart) >= 0.3f;
	}

	private void ResetRunSlotDeleteHover()
	{
		m_runSlotDeleteHoverStart = -1f;
	}

	private void OnRunArchivePageDown()
	{
		int pageSize = m_clipSlotObjs.Count;
		int maxOffset = Mathf.Max(0, m_runArchiveSlots.Count - pageSize);
		m_runArchiveScrollOffset = Mathf.Min(maxOffset, m_runArchiveScrollOffset + pageSize);
	}

	private void ShowRunRenameSlot(int displayIndex, int actualIndex)
	{
		if (m_renameObj == null) return;
		if (actualIndex < 0 || actualIndex >= m_runArchiveSlots.Count) return;

		m_renameSlotIndex = actualIndex;
		m_renameIsRun = true;

		float spacing = 6f * m_size;
		float btnH = 36f * m_size;
		float slotY = -(displayIndex * (btnH + spacing));
		float panelW = 240f * m_size;
		float panelH = 80f * m_size;
		float slotX = (displayIndex < m_clipSlotRects.Count) ? m_clipSlotRects[displayIndex].anchoredPosition.x : 0f;
		m_renameRect.anchoredPosition = new Vector2(slotX - panelW * 0.5f - 10f, slotY - panelH * 0.5f);

		string currentName = (actualIndex < m_runArchiveNames.Count) ? m_runArchiveNames[actualIndex] : "";
		m_renameInput.text = currentName;
		m_renameObj.SetActive(true);
		m_renameInput.ActivateInputField();
		m_renameInput.Select();
	}

	// ===== 框选删除（运行态） =====

	// 删除运行中载具的一批部件：断开关节 → 移出载具 → 销毁对象 → 重建连通
	private void DeleteRunParts(List<BasePart> parts)
	{
		var lm = WPFMonoBehaviour.levelManager;
		var cr = lm != null ? lm.ContraptionRunning : null;
		if (cr == null || parts == null) return;

		List<Vector2Int> coords = new List<Vector2Int>();
		int removed = 0;
		for (int i = parts.Count - 1; i >= 0; i--)
		{
			BasePart p = parts[i];
			if (p == null) continue;
			coords.Add(new Vector2Int(p.m_coordX, p.m_coordY));
			try
			{
				List<Joint> joints = cr.FindPartJoints(p);
				if (joints != null)
					for (int j = 0; j < joints.Count; j++)
						if (joints[j] != null) UnityEngine.Object.Destroy(joints[j]);
			}
			catch { }
			try { cr.RemovePart(p); } catch { }
			UnityEngine.Object.Destroy(p.gameObject);
			removed++;
		}
		parts.Clear();

		for (int i = 0; i < coords.Count; i++)
		{
			try { cr.RefreshNeighboursVisual(coords[i].x, coords[i].y); } catch { }
		}
		try { cr.UpdateConnectedComponents(); } catch { }
		Debug.Log("[RunDelete] removed " + removed + " parts");
	}

	// ===== 复制粘贴继承“开关状态” =====

	private static bool SafeIsEnabled(BasePart p)
	{
		if (p == null) return false;
		try { return p.IsEnabled(); } catch { return false; }
	}

	private static bool IsToggleablePart(BasePart p)
	{
		if (p == null) return false;
		try
		{
			if (p.HasOnOffToggle() || p.CanBeEnabled()) return true;
		}
		catch { }
		return p is SwitchPart || p is SPDTSwitchPart || p is VccPart;
	}

	// 让粘贴出来的部件继承源部件的开/关状态。
	// 先走官方 SetEnabled；对只实现 IsEnabled、没有 SetEnabled 的部件（如开关/变速箱/蘑菇灯），
	// 反射调用受保护的 OnTouch() 兜底切换。
	private static void ApplyInheritedEnabled(BasePart inst, bool want)
	{
		if (inst == null) return;
		if (!IsToggleablePart(inst)) return;
		bool have;
		try { have = inst.IsEnabled(); } catch { return; }
		if (have == want) return;

		try { inst.SetEnabled(want); } catch { }
		try { if (inst.IsEnabled() == want) return; } catch { return; }

		try
		{
			System.Reflection.MethodInfo mi = inst.GetType().GetMethod(
				"OnTouch", BindingFlags.Instance | BindingFlags.NonPublic, null, System.Type.EmptyTypes, null);
			if (mi != null) mi.Invoke(inst, null);
		}
		catch { }
	}
}
