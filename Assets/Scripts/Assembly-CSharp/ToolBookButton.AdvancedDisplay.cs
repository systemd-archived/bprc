using System.Collections.Generic;
using UnityEngine;
using UButton = UnityEngine.UI.Button;
using UImage = UnityEngine.UI.Image;
using UText = UnityEngine.UI.Text;

// ToolBookButton 扩展：高级显示选项。
// 工具菜单“显示”按钮 = 总开关；星海设置“高级显示图形化” = 两套完全独立的表现方式：
//   · 图形化 ON ：纯图形(箭头/指针/标记/引力范围圈) + 就近字母读数
//   · 图形化 OFF：整块文字读数面板（无图形）
// 暂停状态下有 4 个可拖动把手（半透明圆盘，参考点光源“光盾”的观感）：
//   位置(质心) / 速度(箭头末端，绝对长度，不设上下限) / 角度(朝向线末端) / 角速度(弧末端)。
// 独立 partial 文件，避免对 3000+ 行的 ToolBookButton.cs 做大量分散修改。
public partial class ToolBookButton
{
	// 每项可视化：key / 按钮文字 / 默认开关
	private static readonly string[] s_advKeys = { "speed", "angvel", "angle", "com", "mass", "energy", "accel", "collider" };
	private static readonly string[] s_advLabels = { "速度", "角速度", "角度", "质心", "质量", "动能", "加速度", "碰撞箱" };
	private static readonly bool[] s_advDefaults = { true, true, true, true, false, false, false, false };
	private const int AdvItemCollider = 7;

	private const string AdvPrefMaster = "INAdvDisp_master";
	// 万有引力力阈值（与 UniversalGravityManager 一致）：低于该力视为范围外
	private const float AdvGravityForceThreshold = 0.01f;
	// 引力范围圈最大世界半径(超出则不绘制，避免超大圆)
	private const float AdvGravityMaxRadius = 400f;

	// —— 把手尺寸(屏幕像素，乘以 m_size) ——
	private const float AdvDiscCom = 28f;      // 质心圆盘半径
	private const float AdvDiscOther = 24f;    // 其它圆盘半径
	private const float AdvGrabMargin = 14f;   // 抓取半径 = 圆盘半径 + 该余量（略宽松，确保点得到）
	private const float AdvGrabVelSeg = 30f;   // 速度箭头后段的抓取半径
	// 速度把手距质心的最小距离(屏幕像素)：即使速度为 0 也偏移这么多，避免与质心把手重叠
	private const float AdvHandleMinOffset = 90f;
	// 图形尺寸
	private const float AdvAngleLineLen = 130f;   // 角度线长度
	private const float AdvAngvelArcBase = 150f;  // 角速度螺旋内圈半径(与速度把手 90 拉开 ≥60px)
	private const float AdvAngvelArcMax = 240f;   // 角速度螺旋外圈半径
	private const float AdvAngvelArcGrow = 45f;   // 螺旋由内向外增长的半径量
	private const float AdvAngvelMaxTurns = 8f;   // 螺旋最多显示圈数(防止过密)

	// 主开关按钮
	private GameObject m_advBtnObj;
	private UImage m_advBtnBg;
	private UText m_advBtnText;
	private bool m_advEnabled;

	// 参数面板
	private GameObject m_advPanelObj;
	private GameObject m_advTitleObj;
	private readonly List<GameObject> m_advItemObjs = new List<GameObject>();
	private readonly List<UImage> m_advItemBgs = new List<UImage>();
	private readonly List<UText> m_advItemTexts = new List<UText>();
	private readonly List<bool> m_advItemOn = new List<bool>();

	// 渲染数据
	private class AdvVehicleData
	{
		public int Index;
		public Vector2 Com;
		public Vector2 Vel;
		public float AngVel;
		public float Angle;
		public float Mass;
		public float Energy;
		public float Accel;
		// 碰撞箱轮廓（世界坐标）：凸四边形 + 圆
		public readonly List<Vector2[]> ColQuads = new List<Vector2[]>();
		public readonly List<Vector3> ColCircles = new List<Vector3>(); // x, y, radius
	}
	private readonly List<AdvVehicleData> m_advVehicles = new List<AdvVehicleData>();
	private readonly Dictionary<int, Vector2> m_advPrevVel = new Dictionary<int, Vector2>();
	private bool m_advOverlayActive;

	// 暂停时拖动指针（位置/速度/角度/角速度）
	// m_advVelocityDragging = 是否有高级显示拖动进行中（主文件 UpdatePauseSelection 守卫使用）
	private bool m_advVelocityDragging;
	private int m_advDragVehicleIdx = -1;
	private int m_advDragMode;            // 0=无 1=位置(COM) 2=速度 3=角度 4=角速度
	private readonly List<BasePart> m_advDragParts = new List<BasePart>();
	private Vector2 m_advDragLastWorld;
	private Vector2 m_advDragPivot;
	private float m_advDragBaseAngle;
	private float m_advDragBaseOmega;
	private float m_advDragPrevAngle;      // 角速度拖动：上一帧指针方位角
	private float m_advDragAccum;          // 角速度拖动：累计缠绕角(度，可分多圈)
	private readonly List<Vector3> m_advDragBasePos = new List<Vector3>();
	private readonly List<float> m_advDragBaseZ = new List<float>();

	private GUIStyle m_advLabelStyle;
	private Texture2D m_advDiscTex;

	// ===== UI 创建 =====

	private void CreateAdvancedDisplayUI()
	{
		m_advEnabled = PlayerPrefs.GetInt(AdvPrefMaster, 0) != 0;

		m_advBtnObj = CreateButtonObj("AdvDisplay", m_mainRect.transform);
		m_advBtnBg = m_advBtnObj.GetComponent<UImage>();
		m_advBtnBg.color = s_normalBg;
		m_advBtnObj.GetComponent<UButton>().onClick.AddListener(OnAdvDisplayToggleClicked);
		m_advBtnText = CreateLabel(m_advBtnObj, "显示", 12, Color.white);
		m_advBtnObj.SetActive(false);

		// 参数面板（标题 + 若干开关按钮）
		m_advPanelObj = new GameObject("AdvPanel");
		m_advPanelObj.transform.SetParent(m_mainRect.transform, false);
		RectTransform pr = m_advPanelObj.AddComponent<RectTransform>();
		pr.anchorMin = new Vector2(1f, 1f);
		pr.anchorMax = new Vector2(1f, 1f);
		pr.pivot = new Vector2(1f, 1f);
		pr.sizeDelta = new Vector2(48f * m_size, 36f * m_size);

		m_advTitleObj = new GameObject("AdvTitle");
		m_advTitleObj.transform.SetParent(m_mainRect.transform, false);
		RectTransform tr = m_advTitleObj.AddComponent<RectTransform>();
		tr.anchorMin = new Vector2(1f, 1f);
		tr.anchorMax = new Vector2(1f, 1f);
		tr.pivot = new Vector2(1f, 1f);
		tr.sizeDelta = new Vector2(84f * m_size, 26f * m_size);
		CreateLabel(m_advTitleObj, "高级显示", 14, new Color(1f, 0.9f, 0.4f, 1f));
		m_advTitleObj.SetActive(false);

		for (int i = 0; i < s_advKeys.Length; i++)
		{
			GameObject item = CreateButtonObj("Adv_" + s_advKeys[i], m_mainRect.transform);
			UImage bg = item.GetComponent<UImage>();
			bg.color = s_normalBg;
			int idx = i;
			item.GetComponent<UButton>().onClick.AddListener(() => OnAdvItemClicked(idx));
			UText t = CreateLabel(item, s_advLabels[i], 16, Color.white);

			m_advItemObjs.Add(item);
			m_advItemBgs.Add(bg);
			m_advItemTexts.Add(t);
			m_advItemOn.Add(PlayerPrefs.GetInt("INAdvDisp_" + s_advKeys[i], s_advDefaults[i] ? 1 : 0) != 0);

			item.SetActive(false);
		}
	}

	private void OnAdvDisplayToggleClicked()
	{
		m_advEnabled = !m_advEnabled;
		PlayerPrefs.SetInt(AdvPrefMaster, m_advEnabled ? 1 : 0);
		PlayerPrefs.Save();
		if (!m_advEnabled)
		{
			m_advVehicles.Clear();
			EndAdvDrag();
		}
	}

	private void OnAdvItemClicked(int idx)
	{
		if (idx < 0 || idx >= m_advItemOn.Count) return;
		m_advItemOn[idx] = !m_advItemOn[idx];
		PlayerPrefs.SetInt("INAdvDisp_" + s_advKeys[idx], m_advItemOn[idx] ? 1 : 0);
		PlayerPrefs.Save();
	}

	// 指针是否落在高级显示的 UI 上（供主文件的 IsPointerOverToolBook 调用）
	private bool IsPointerOverAdvUi()
	{
		if (m_advBtnObj != null && m_advBtnObj.activeInHierarchy)
		{
			RectTransform rt = m_advBtnObj.GetComponent<RectTransform>();
			if (rt != null && IsOverRect(rt)) return true;
		}
		for (int i = 0; i < m_advItemObjs.Count; i++)
		{
			GameObject o = m_advItemObjs[i];
			if (o == null || !o.activeInHierarchy) continue;
			RectTransform rt = o.GetComponent<RectTransform>();
			if (rt != null && IsOverRect(rt)) return true;
		}
		return false;
	}

	// 星海设置：图形化开关（默认开启）
	private bool AdvGraphical
	{
		get { return INUserSettings.Instance?.StarSeaSettings?.AdvancedDisplayGraphical ?? true; }
	}

	// 每帧刷新：按钮/面板显隐与布局、数据采集、暂停拖动
	private void UpdateAdvancedDisplayUI()
	{
		if (m_advBtnObj == null) return;

		float mainW = m_mainRect.sizeDelta.x;
		float spacing = 6f * m_size;
		float btnW = 48f * m_size;
		float btnH = 36f * m_size;
		float col1X = -(mainW + spacing);
		float col2X = -(mainW + spacing + btnW + spacing);

		// 主按钮：建造态放在第二列第 7 行（row6），运行态放在第 8 行（row7，避开运行按钮）
		bool showBtn = m_isOpen;
		if (m_advBtnObj.activeSelf != showBtn) m_advBtnObj.SetActive(showBtn);
		if (showBtn)
		{
			float row = (m_runStateActive && m_runOptionsShown) ? 7f : 6f;
			RectTransform rt = m_advBtnObj.GetComponent<RectTransform>();
			rt.sizeDelta = new Vector2(btnW, btnH);
			rt.anchoredPosition = new Vector2(col2X, -(row * (btnH + spacing)));
			if (m_advBtnText != null) m_advBtnText.fontSize = Mathf.RoundToInt(12f * m_size);
			Color c = m_advEnabled ? s_speedOnBg : s_normalBg;
			if (m_advBtnBg.color != c) m_advBtnBg.color = c;
			string label = m_advEnabled ? "显示*" : "显示";
			if (m_advBtnText != null && m_advBtnText.text != label) m_advBtnText.text = label;
		}

		// 参数面板紧贴在第一列下方；按钮与正常工具按钮同样大小(48×36)
		bool showPanel = showBtn && m_advEnabled;
		if (m_advPanelObj.activeSelf != showPanel) m_advPanelObj.SetActive(showPanel);
		if (m_advTitleObj.activeSelf != showPanel) m_advTitleObj.SetActive(showPanel);

		float itemW = btnW;
		float itemH = btnH;
		float itemPitch = itemH + spacing;

		if (m_advTitleObj != null)
			m_advTitleObj.GetComponent<RectTransform>().sizeDelta = new Vector2(btnW, 26f * m_size);

		if (showPanel)
		{
			float startY = -(9f * (btnH + spacing));
			if (m_advTitleObj != null)
				m_advTitleObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(col1X, startY);
			for (int i = 0; i < m_advItemObjs.Count; i++)
			{
				GameObject o = m_advItemObjs[i];
				if (!o.activeSelf) o.SetActive(true);
				RectTransform rt = o.GetComponent<RectTransform>();
				rt.sizeDelta = new Vector2(itemW, itemH);
				rt.anchoredPosition = new Vector2(col1X, startY - ((i + 1) * itemPitch));
				if (i < m_advItemTexts.Count && m_advItemTexts[i] != null)
					m_advItemTexts[i].fontSize = Mathf.RoundToInt(16f * m_size);
				if (i < m_advItemBgs.Count && m_advItemBgs[i] != null)
				{
					Color c = m_advItemOn[i] ? s_speedOnBg : s_normalBg;
					if (m_advItemBgs[i].color != c) m_advItemBgs[i].color = c;
				}
			}
		}
		else
		{
			for (int i = 0; i < m_advItemObjs.Count; i++)
				if (m_advItemObjs[i].activeSelf) m_advItemObjs[i].SetActive(false);
		}

		bool masterOn = INUserSettings.Instance?.StarSeaSettings?.ToolBookEnabled ?? false;
		m_advOverlayActive = m_advEnabled && masterOn;

		UpdateAdvancedDisplayData();
		UpdateAdvancedDisplayDrag();
	}

	private bool AdvOn(int idx)
	{
		return idx >= 0 && idx < m_advItemOn.Count && m_advItemOn[idx];
	}

	// ===== 数据采集 =====
	// 运行中/暂停/建造都采集（只要有带刚体的部件）；优先按连通组件分组，
	// 连通组件数为 0 时(如建造态)退回“全部部件视为一辆载具”。

	private void UpdateAdvancedDisplayData()
	{
		m_advVehicles.Clear();
		if (!m_advOverlayActive) { m_advPrevVel.Clear(); return; }

		Contraption cont = Contraption.Instance;
		if (cont == null) { m_advPrevVel.Clear(); return; }
		List<BasePart> allParts = cont.Parts;
		if (allParts == null || allParts.Count == 0) { m_advPrevVel.Clear(); return; }

		float dt = Time.deltaTime;
		int ccCount = cont.ConnectedComponentCount;
		if (ccCount > 0)
		{
			for (int ci = 0; ci < ccCount && m_advVehicles.Count < 12; ci++)
				BuildAdvVehicle(cont.GetConnectedParts(ci), ci, dt);
		}
		else
		{
			BuildAdvVehicle(allParts, 0, dt);
		}
	}

	private void BuildAdvVehicle(List<BasePart> parts, int index, float dt)
	{
		if (parts == null || parts.Count == 0) return;

		float mass = 0f;
		Vector2 com = Vector2.zero;
		Vector2 velW = Vector2.zero;
		float angW = 0f;
		float sSin = 0f, sCos = 0f;
		float energy = 0f;
		int rbCount = 0;

		for (int pi = 0; pi < parts.Count; pi++)
		{
			BasePart part = parts[pi];
			if (part == null) continue;
			foreach (Rigidbody rb in part.GetRigidbodies())
			{
				if (rb == null) continue;
				float m = rb.mass;
				Vector3 wc = rb.worldCenterOfMass;
				Vector3 v = rb.velocity;
				Vector3 av = rb.angularVelocity;

				mass += m;
				com += new Vector2(wc.x, wc.y) * m;
				velW += new Vector2(v.x, v.y) * m;
				angW += av.z * Mathf.Rad2Deg * m;   // 统一为 度/秒
				energy += 0.5f * m * (v.x * v.x + v.y * v.y);
				float ang = rb.rotation.eulerAngles.z * Mathf.Deg2Rad;
				sSin += Mathf.Sin(ang) * m;
				sCos += Mathf.Cos(ang) * m;
				rbCount++;
			}
		}

		if (rbCount == 0 || mass <= 0.0001f) return;

		Vector2 c = com / mass;
		Vector2 vel = velW / mass;
		float angVel = angW / mass;
		float angle = Mathf.Atan2(sSin, sCos) * Mathf.Rad2Deg;

		float accel = 0f;
		Vector2 prev;
		if (m_advPrevVel.TryGetValue(index, out prev) && dt > 0.00001f)
			accel = (vel - prev).magnitude / dt;
		m_advPrevVel[index] = vel;

		AdvVehicleData data = new AdvVehicleData
		{
			Index = index,
			Com = c,
			Vel = vel,
			AngVel = angVel,
			Angle = angle,
			Mass = mass,
			Energy = energy,
			Accel = accel
		};
		if (AdvOn(AdvItemCollider))
			CollectColliders(parts, data);
		m_advVehicles.Add(data);
	}

	// 收集该载具所有部件(含子对象)的碰撞箱轮廓（世界坐标）
	private void CollectColliders(List<BasePart> parts, AdvVehicleData data)
	{
		data.ColQuads.Clear();
		data.ColCircles.Clear();
		for (int pi = 0; pi < parts.Count; pi++)
		{
			BasePart part = parts[pi];
			if (part == null) continue;
			Collider[] cols = part.GetComponentsInChildren<Collider>();
			for (int ci = 0; ci < cols.Length; ci++)
			{
				Collider col = cols[ci];
				if (col == null || !col.enabled) continue;

				BoxCollider box = col as BoxCollider;
				if (box != null)
				{
					Transform t = box.transform;
					Vector3 ctr = box.center;
					Vector3 h = box.size * 0.5f;
					float z = ctr.z;
					Vector2[] q = new Vector2[4];
					q[0] = t.TransformPoint(new Vector3(ctr.x - h.x, ctr.y - h.y, z));
					q[1] = t.TransformPoint(new Vector3(ctr.x + h.x, ctr.y - h.y, z));
					q[2] = t.TransformPoint(new Vector3(ctr.x + h.x, ctr.y + h.y, z));
					q[3] = t.TransformPoint(new Vector3(ctr.x - h.x, ctr.y + h.y, z));
					data.ColQuads.Add(q);
					continue;
				}

				SphereCollider sph = col as SphereCollider;
				if (sph != null)
				{
					Vector3 cw = sph.transform.TransformPoint(sph.center);
					Vector3 ls = sph.transform.lossyScale;
					float r = sph.radius * Mathf.Max(Mathf.Abs(ls.x), Mathf.Abs(ls.y));
					data.ColCircles.Add(new Vector3(cw.x, cw.y, r));
					continue;
				}

				// 其它碰撞体（胶囊/Mesh等）：用世界 AABB 近似成矩形
				Bounds b = col.bounds;
				Vector2[] rq = new Vector2[4];
				rq[0] = new Vector2(b.min.x, b.min.y);
				rq[1] = new Vector2(b.max.x, b.min.y);
				rq[2] = new Vector2(b.max.x, b.max.y);
				rq[3] = new Vector2(b.min.x, b.max.y);
				data.ColQuads.Add(rq);
			}
		}
	}

	// ===== 暂停：拖动指针调整（位置 / 速度 / 角度 / 角速度）=====

	private void UpdateAdvancedDisplayDrag()
	{
		if (!m_advOverlayActive || !AdvGraphical || !m_runPaused || m_advVehicles.Count == 0)
		{
			EndAdvDrag();
			return;
		}
		Camera cam = Camera.main;
		if (cam == null) return;

		Vector2 smp = Input.mousePosition;
		Vector2 mp = new Vector2(smp.x, Screen.height - smp.y);

		if (m_advDragMode == 0)
		{
			if (!Input.GetMouseButtonDown(0)) return;
			float ppu = AdvPixelsPerUnit(cam);
			// 先用“相对距离”(d / 该把手半径) 在 3 个小圆盘(位置/角度/角速度)里挑最近的；
			// 若都没命中，再考虑速度(箭头长、范围大)，避免速度把手压过小圆盘
			// （此前“角速度不可调”就是因为速度线段把手把它盖住了）。
			int best = -1;
			int bestMode = 0;
			float bestScore = float.MaxValue;
			int velBest = -1;
			float velScore = float.MaxValue;

			for (int i = 0; i < m_advVehicles.Count; i++)
			{
				AdvVehicleData v = m_advVehicles[i];
				Vector2 org = AdvWorldToGui(cam, v.Com);
				float discGrab = (AdvDiscOther + AdvGrabMargin) * m_size;
				float comGrab = (AdvDiscCom + AdvGrabMargin) * m_size;
				float segGrab = AdvGrabVelSeg * m_size;

				// 1) 位置(质心)
				if (AdvOn(3))
				{
					float score = Vector2.Distance(mp, org) / comGrab;
					if (score <= 1f && score < bestScore) { bestScore = score; best = i; bestMode = 1; }
				}
				// 2) 角速度：弧末端（先于角度判定，平手时角速度优先）
				if (AdvOn(1))
				{
					float score = Vector2.Distance(mp, AdvAngvelHandleScreen(cam, v)) / discGrab;
					if (score <= 1f && score < bestScore) { bestScore = score; best = i; bestMode = 4; }
				}
				// 3) 角度：角度线末端
				if (AdvOn(2))
				{
					float score = Vector2.Distance(mp, AdvAngleEndScreen(cam, v)) / discGrab;
					if (score <= 1f && score < bestScore) { bestScore = score; best = i; bestMode = 3; }
				}
				// 4) 速度：末端把手 或 箭头后段（优先级最低）
				if (AdvOn(0))
				{
					Vector2 handle = AdvVelocityHandleScreen(cam, v, ppu);
					Vector2 tip = AdvWorldToGui(cam, AdvVelocityTipWorld(v, ppu));
					float dHandle = Vector2.Distance(mp, handle);
					float dSeg = AdvPointSegmentDistanceRange(mp, org, tip, 0.3f, 1f);
					float score = Mathf.Min(dHandle / discGrab, dSeg / segGrab);
					if (score <= 1f && score < velScore) { velScore = score; velBest = i; }
				}
			}

			if (best < 0 && velBest >= 0) { best = velBest; bestMode = 2; }

			if (best >= 0) BeginAdvDrag(bestMode, best, mp);
			return;
		}

		if (Input.GetMouseButton(0)) ApplyAdvDrag(mp);
		if (Input.GetMouseButtonUp(0)) EndAdvDrag();
	}

	private void BeginAdvDrag(int mode, int vehicleIdx, Vector2 mouse)
	{
		AdvVehicleData v = m_advVehicles[vehicleIdx];
		m_advDragMode = mode;
		m_advDragVehicleIdx = vehicleIdx;
		m_advVelocityDragging = true;
		m_advDragLastWorld = AdvGuiToWorld(mouse);
		m_advDragPivot = v.Com;
		INSelectionManager.PauseBoxDrawing = true;

		m_advDragParts.Clear();
		List<BasePart> parts = AdvPartsFor(v.Index);
		if (parts != null) m_advDragParts.AddRange(parts);

		m_advDragBasePos.Clear();
		m_advDragBaseZ.Clear();
		if (mode == 3)
		{
			for (int i = 0; i < m_advDragParts.Count; i++)
			{
				BasePart p = m_advDragParts[i];
				m_advDragBasePos.Add(p != null ? p.transform.position : Vector3.zero);
				m_advDragBaseZ.Add(p != null ? p.transform.eulerAngles.z : 0f);
			}
			float dx = m_advDragLastWorld.x - v.Com.x;
			float dy = m_advDragLastWorld.y - v.Com.y;
			m_advDragBaseAngle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
		}
		else if (mode == 4)
		{
			// 角速度按“缠绕圈数”调节：记录起始方位角与累计缠绕角
			m_advDragBaseOmega = v.AngVel;
			m_advDragAccum = 0f;
			m_advDragPrevAngle = Mathf.Atan2(m_advDragLastWorld.y - v.Com.y, m_advDragLastWorld.x - v.Com.x) * Mathf.Rad2Deg;
		}
	}

	private void ApplyAdvDrag(Vector2 mouse)
	{
		int mi = m_advDragVehicleIdx;
		if (mi < 0 || mi >= m_advVehicles.Count) return;
		AdvVehicleData v = m_advVehicles[mi];

		switch (m_advDragMode)
		{
			case 1: // 位置：整体平移
			{
				Vector3 wp = AdvGuiToWorld(mouse);
				Vector2 delta = new Vector2(wp.x - m_advDragLastWorld.x, wp.y - m_advDragLastWorld.y);
				m_advDragLastWorld = new Vector2(wp.x, wp.y);
				for (int i = 0; i < m_advDragParts.Count; i++)
				{
					BasePart p = m_advDragParts[i];
					if (p == null) continue;
					p.transform.position += new Vector3(delta.x, delta.y, 0f);
				}
				SyncAdvRigidbodies(m_advDragParts);
				v.Com += delta;
				break;
			}
			case 2: // 速度：绝对长度，不设上下限
			{
				Vector3 wp = AdvGuiToWorld(mouse);
				Vector2 nv = new Vector2(wp.x - v.Com.x, wp.y - v.Com.y);
				AdvApplyVelocity(v.Index, nv);
				v.Vel = nv;
				break;
			}
			case 3: // 角度：绕质心旋转
			{
				Vector3 wp = AdvGuiToWorld(mouse);
				float cur = Mathf.Atan2(wp.y - m_advDragPivot.y, wp.x - m_advDragPivot.x) * Mathf.Rad2Deg;
				float delta = Mathf.DeltaAngle(m_advDragBaseAngle, cur);
				float rad = delta * Mathf.Deg2Rad;
				float cs = Mathf.Cos(rad), sn = Mathf.Sin(rad);
				Vector3 pivot = new Vector3(m_advDragPivot.x, m_advDragPivot.y, 0f);
				int n = Mathf.Min(m_advDragParts.Count, m_advDragBasePos.Count);
				for (int i = 0; i < n; i++)
				{
					BasePart p = m_advDragParts[i];
					if (p == null) continue;
					Vector3 off = m_advDragBasePos[i] - pivot;
					Vector3 rot = new Vector3(off.x * cs - off.y * sn, off.x * sn + off.y * cs, 0f);
					p.transform.position = pivot + rot;
					p.transform.rotation = Quaternion.Euler(0f, 0f, m_advDragBaseZ[i] + delta);
				}
				SyncAdvRigidbodies(m_advDragParts);
				v.Angle += delta;
				break;
			}
			case 4: // 角速度：绕质心缠绕（圈数越多角速度越大），不限幅
			{
				Vector3 wp = AdvGuiToWorld(mouse);
				float cur = Mathf.Atan2(wp.y - v.Com.y, wp.x - v.Com.x) * Mathf.Rad2Deg;
				m_advDragAccum += Mathf.DeltaAngle(m_advDragPrevAngle, cur);
				m_advDragPrevAngle = cur;
				float omega = m_advDragBaseOmega + m_advDragAccum;   // 缠绕 1° → 1°/s
				AdvApplyAngularVelocity(v.Index, omega, v.Com);
				v.AngVel = omega;
				break;
			}
		}
	}

	private void EndAdvDrag()
	{
		if (!m_advVelocityDragging && m_advDragMode == 0) return;
		m_advVelocityDragging = false;
		m_advDragMode = 0;
		m_advDragVehicleIdx = -1;
		m_advDragParts.Clear();
		m_advDragBasePos.Clear();
		m_advDragBaseZ.Clear();
		INSelectionManager.PauseBoxDrawing = false;
	}

	// 位置/旋转后同步刚体，避免取消暂停时被物理“弹回”（多刚体部件交由引擎自动同步）
	private static void SyncAdvRigidbodies(List<BasePart> parts)
	{
		for (int i = 0; i < parts.Count; i++)
		{
			BasePart p = parts[i];
			if (p == null) continue;
			Rigidbody only = null;
			int count = 0;
			foreach (Rigidbody rb in p.GetRigidbodies())
			{
				if (rb == null) continue;
				count++;
				only = rb;
			}
			if (count == 1 && only != null)
			{
				only.position = p.transform.position;
				only.rotation = p.transform.rotation;
			}
		}
		Physics.SyncTransforms();
	}

	private List<BasePart> AdvPartsFor(int ccIndex)
	{
		Contraption cont = Contraption.Instance;
		if (cont == null) return null;
		return (cont.ConnectedComponentCount > 0) ? cont.GetConnectedParts(ccIndex) : cont.Parts;
	}

	private void AdvApplyVelocity(int ccIndex, Vector2 vel)
	{
		List<BasePart> parts = AdvPartsFor(ccIndex);
		if (parts == null) return;
		for (int i = 0; i < parts.Count; i++)
		{
			BasePart part = parts[i];
			if (part == null) continue;
			foreach (Rigidbody rb in part.GetRigidbodies())
			{
				if (rb == null || rb.isKinematic) continue;
				rb.velocity = new Vector3(vel.x, vel.y, rb.velocity.z);
			}
		}
	}

	// 设置角速度（度/秒）。同时把各刚体线速度设为“绕质心纯旋转”的速度场，
	// 这样刚体簇在取消暂停后会真正整体旋转（只改 angularVelocity 时关节会互相抵消 → 看似无反应）。
	private void AdvApplyAngularVelocity(int ccIndex, float omegaDeg, Vector2 com)
	{
		List<BasePart> parts = AdvPartsFor(ccIndex);
		if (parts == null) return;
		float w = omegaDeg * Mathf.Deg2Rad;
		for (int i = 0; i < parts.Count; i++)
		{
			BasePart part = parts[i];
			if (part == null) continue;
			foreach (Rigidbody rb in part.GetRigidbodies())
			{
				if (rb == null || rb.isKinematic) continue;
				Vector3 c = rb.worldCenterOfMass;
				float rx = c.x - com.x;
				float ry = c.y - com.y;
				rb.velocity = new Vector3(-w * ry, w * rx, rb.velocity.z);
				// Unity 默认 maxAngularVelocity=7rad/s(≈401°/s) 会把高速旋转截断 → 必须抬高上限
				if (rb.maxAngularVelocity < Mathf.Abs(w)) rb.maxAngularVelocity = Mathf.Abs(w);
				rb.angularVelocity = new Vector3(0f, 0f, w);
			}
		}
	}

	// ===== 把手位置 =====

	// 角速度螺旋：圈数 = |ω|/360 圈（ω 为 度/秒），至少 0.25 圈，最多 AdvAngvelMaxTurns
	private float AdvAngvelTurns(AdvVehicleData v)
	{
		// 圈数 = |ω|/360（不设最小圈数）：ω=30 → 30° 弧，ω=90 → 90°，ω=360 → 1 圈
		return Mathf.Min(Mathf.Abs(v.AngVel) / 360f, AdvAngvelMaxTurns);
	}

	// 螺旋起始角(度，GUI 空间)
	private float AdvAngvelStartDeg(AdvVehicleData v)
	{
		Vector2 d = AdvAngvelDir(v);
		return Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
	}

	// 螺旋总扫角(度)：负角在屏幕上是逆时针；正转(ω>0)用负扫角
	private float AdvAngvelSpiralSweep(AdvVehicleData v)
	{
		float sign = (v.AngVel < -0.05f) ? 1f : -1f;
		return sign * AdvAngvelTurns(v) * 360f;
	}

	// 角速度把手方向：与“角度线”垂直（GUI 空间），从根上避免与角度把手重叠
	private Vector2 AdvAngvelDir(AdvVehicleData v)
	{
		float th = v.Angle * Mathf.Deg2Rad;
		return new Vector2(Mathf.Sin(th), Mathf.Cos(th));
	}

	private Vector2 AdvAngleEndScreen(Camera cam, AdvVehicleData v)
	{
		Vector2 org = AdvWorldToGui(cam, v.Com);
		float rad = v.Angle * Mathf.Deg2Rad;
		Vector2 dir = new Vector2(Mathf.Cos(rad), -Mathf.Sin(rad));
		return org + dir * AdvAngleLineLen;
	}

	private Vector2 AdvAngvelHandleScreen(Camera cam, AdvVehicleData v)
	{
		// 把手在螺旋外端（随圈数变化）
		Vector2 org = AdvWorldToGui(cam, v.Com);
		float endDeg = AdvAngvelStartDeg(v) + AdvAngvelSpiralSweep(v);
		return org + Deg2GuiVec(endDeg) * (AdvAngvelArcBase + AdvAngvelArcGrow);
	}

	// 速度箭头末端世界坐标（绝对长度：|v| 世界单位，不限幅）
	private Vector2 AdvVelocityTipWorld(AdvVehicleData v, float ppu)
	{
		Vector2 dir = v.Vel.sqrMagnitude > 0.0001f ? v.Vel.normalized : Vector2.right;
		return v.Com + dir * v.Vel.magnitude;
	}

	private Vector2 AdvVelocityTipScreen(Camera cam, AdvVehicleData v, float ppu)
	{
		return AdvWorldToGui(cam, AdvVelocityTipWorld(v, ppu));
	}

	// 速度把手屏幕坐标：沿速度方向、距质心 max(|v|*ppu, 最小偏移)
	private Vector2 AdvVelocityHandleScreen(Camera cam, AdvVehicleData v, float ppu)
	{
		Vector2 org = AdvWorldToGui(cam, v.Com);
		Vector2 tip = AdvVelocityTipScreen(cam, v, ppu);
		Vector2 dirGui = tip - org;
		if (dirGui.sqrMagnitude < 0.0001f) dirGui = Vector2.right;
		else dirGui = dirGui.normalized;
		float off = Mathf.Max(v.Vel.magnitude * ppu, AdvHandleMinOffset * m_size);
		return org + dirGui * off;
	}

	// 统一使用“左上角为原点、y 向下”的 GUI 空间：屏幕坐标 y 需要翻转。
	private static Vector2 AdvWorldToGui(Camera cam, Vector2 world)
	{
		Vector3 sp = cam.WorldToScreenPoint(new Vector3(world.x, world.y, 0f));
		return new Vector2(sp.x, Screen.height - sp.y);
	}

	// GUI 空间 → 屏幕坐标（y 再翻回去，供 GetBoxWorldPos 使用）
	private static Vector2 AdvGuiToScreen(Vector2 guiPos)
	{
		return new Vector2(guiPos.x, Screen.height - guiPos.y);
	}

	private Vector3 AdvGuiToWorld(Vector2 guiPos)
	{
		return GetBoxWorldPos(AdvGuiToScreen(guiPos));
	}

	private float AdvPixelsPerUnit(Camera cam)
	{
		Vector2 g0 = AdvWorldToGui(cam, Vector2.zero);
		Vector2 g1 = AdvWorldToGui(cam, new Vector2(1f, 0f));
		float ppu = Mathf.Abs(g1.x - g0.x);
		if (ppu < 0.001f)
			ppu = cam.orthographic ? (Screen.height / (2f * cam.orthographicSize)) : 50f;
		return ppu;
	}

	// 由力阈值反推“有效范围”半径：r = (G * m1 * m2 / 阈值)^(1/(2*衰减倍率))
	private float AdvGravityRange(float mass, float refMass, float g, float att)
	{
		if (g <= 0f || att <= 0f || mass <= 0f || refMass <= 0f) return -1f;
		float val = g * mass * refMass / AdvGravityForceThreshold;
		if (val <= 0f) return -1f;
		float r = Mathf.Pow(val, 1f / (2f * att));
		if (float.IsNaN(r) || float.IsInfinity(r) || r <= 0f) return -1f;
		r = Mathf.Max(r, 1f);
		if (r > AdvGravityMaxRadius) return -1f;
		return r;
	}

	// ===== IMGUI 绘制 =====

	private void DrawAdvancedDisplay()
	{
		if (!m_advOverlayActive) return;
		if (Event.current == null || Event.current.type != EventType.Repaint) return;

		EnsureAdvStyle();
		Camera cam = Camera.main;

		if (m_advVehicles.Count == 0 || cam == null)
		{
			DrawGuiLabel("高级显示：无载具数据", new Vector2(12f, 12f), new Color(1f, 0.9f, 0.4f, 1f));
			return;
		}

		float ppu = AdvPixelsPerUnit(cam);
		bool gfx = AdvGraphical;

		Color colSpeed = new Color(0.2f, 0.85f, 1f, 0.95f);
		Color colAng = new Color(1f, 0.6f, 0.1f, 0.95f);
		Color colAngle = new Color(0.5f, 1f, 0.4f, 0.95f);
		Color colCom = new Color(1f, 0.25f, 0.25f, 0.95f);
		Color colGravity = new Color(0.7f, 0.5f, 1f, 0.5f);
		Color colCol = new Color(1f, 0.9f, 0.2f, 0.95f);   // 碰撞箱(黄)

		bool multi = m_advVehicles.Count > 1;

		var ssG = INUserSettings.Instance?.StarSeaSettings;
		bool gravityOn = (ssG?.UniversalGravity ?? false) && gfx && m_advVehicles.Count >= 2;
		float gravG = ssG?.UniversalGravityConstant ?? 0.1f;
		float gravAtt = ssG?.UniversalGravityAttenuation ?? 0.333f;

		for (int i = 0; i < m_advVehicles.Count; i++)
		{
			AdvVehicleData v = m_advVehicles[i];
			Vector3 spw = cam.WorldToScreenPoint(new Vector3(v.Com.x, v.Com.y, 0f));
			if (spw.z < 0f) continue;
			Vector2 origin = AdvWorldToGui(cam, v.Com);
			float sx = origin.x;
			float sy = origin.y;

			if (gravityOn)
			{
				float refMass = 0f;
				for (int j = 0; j < m_advVehicles.Count; j++)
					if (j != i && m_advVehicles[j].Mass > refMass) refMass = m_advVehicles[j].Mass;
				float r = AdvGravityRange(v.Mass, refMass, gravG, gravAtt);
				if (r > 0f)
					DrawGuiWorldCircle(cam, v.Com, r, colGravity, 2f);
			}

			if (gfx)
			{
				// 碰撞箱范围
				if (AdvOn(AdvItemCollider))
				{
					for (int q = 0; q < v.ColQuads.Count; q++)
					{
						Vector2[] qd = v.ColQuads[q];
						for (int k = 0; k < 4; k++)
							DrawGuiLine(AdvWorldToGui(cam, qd[k]), AdvWorldToGui(cam, qd[(k + 1) & 3]), colCol, 1.5f);
					}
					for (int cc = 0; cc < v.ColCircles.Count; cc++)
					{
						Vector3 ci2 = v.ColCircles[cc];
						DrawGuiCircle(AdvWorldToGui(cam, new Vector2(ci2.x, ci2.y)), ci2.z * ppu, colCol, 1.5f);
					}
				}

				// 质心十字 + 光盾式把手
				if (AdvOn(3))
				{
					DrawGuiLine(new Vector2(sx - 11f, sy), new Vector2(sx + 11f, sy), colCom, 2f);
					DrawGuiLine(new Vector2(sx, sy - 11f), new Vector2(sx, sy + 11f), colCom, 2f);
					if (m_runPaused) DrawGuiDisc(origin, AdvDiscCom, colCom);
				}

				// 速度箭头（绝对长度） + 末端把手
				if (AdvOn(0))
				{
					Vector2 tipScreen = AdvWorldToGui(cam, AdvVelocityTipWorld(v, ppu));
					DrawGuiArrow(origin, tipScreen, colSpeed, 3f);
					if (m_runPaused) DrawGuiDisc(AdvVelocityHandleScreen(cam, v, ppu), AdvDiscOther, colSpeed);
				}

				// 角速度螺旋：圈数随角速度变化 + 外端把手
				// 角速度为 0 时不画螺旋，但把手仍在（调节依然可用）
				if (AdvOn(1))
				{
					if (Mathf.Abs(v.AngVel) > 0.05f)
						DrawGuiSpiral(origin, AdvAngvelStartDeg(v), AdvAngvelSpiralSweep(v),
							AdvAngvelArcBase, AdvAngvelArcBase + AdvAngvelArcGrow, colAng, 3f);
					if (m_runPaused) DrawGuiDisc(AdvAngvelHandleScreen(cam, v), AdvDiscOther, colAng);
				}

				// 角度线 + 末端把手
				if (AdvOn(2))
				{
					Vector2 aEnd = AdvAngleEndScreen(cam, v);
					DrawGuiLine(origin, aEnd, colAngle, 3f);
					if (m_runPaused) DrawGuiDisc(aEnd, AdvDiscOther, colAngle);
				}

				// 单个紧凑字母读数
				string gtxt = "";
				if (AdvOn(0)) gtxt += "v=" + v.Vel.magnitude.ToString("F1") + " m/s\n";
				if (AdvOn(1)) gtxt += "\u03c9=" + v.AngVel.ToString("F0") + " \u00b0/s\n";
				if (AdvOn(2)) gtxt += "\u03b8=" + v.Angle.ToString("F0") + "\u00b0\n";
				if (AdvOn(3)) gtxt += "COM=(" + v.Com.x.ToString("F1") + "," + v.Com.y.ToString("F1") + ")\n";
				if (AdvOn(4)) gtxt += "m=" + v.Mass.ToString("F2") + "\n";
				if (AdvOn(5)) gtxt += "E=" + v.Energy.ToString("F1") + "\n";
				if (AdvOn(6)) gtxt += "a=" + v.Accel.ToString("F1") + "\n";
				if (!string.IsNullOrEmpty(gtxt))
					DrawGuiStack(new Vector2(sx + 18f, sy + 18f), AdvPre(i, multi, "") + gtxt);
			}
			else
			{
				string txt = "";
				if (multi) txt += "载具" + v.Index + "\n";
				if (AdvOn(0)) txt += "v=" + v.Vel.magnitude.ToString("F2") + " m/s\n";
				if (AdvOn(1)) txt += "\u03c9=" + v.AngVel.ToString("F1") + " \u00b0/s\n";
				if (AdvOn(2)) txt += "\u03b8=" + v.Angle.ToString("F1") + "\u00b0\n";
				if (AdvOn(3)) txt += "COM=(" + v.Com.x.ToString("F2") + ", " + v.Com.y.ToString("F2") + ")\n";
				if (AdvOn(4)) txt += "m=" + v.Mass.ToString("F3") + "\n";
				if (AdvOn(5)) txt += "E=" + v.Energy.ToString("F2") + "\n";
				if (AdvOn(6)) txt += "a=" + v.Accel.ToString("F2") + "\n";
				if (!string.IsNullOrEmpty(txt))
					DrawGuiStack(new Vector2(sx + 18f, sy + 12f), txt);
			}
		}
	}

	private static string AdvPre(int index, bool multi, string s)
	{
		return multi ? ("[" + index + "] " + s) : s;
	}

	private void EnsureAdvStyle()
	{
		if (m_advLabelStyle != null) return;
		m_advLabelStyle = new GUIStyle(GUI.skin.label);
		m_advLabelStyle.fontSize = Mathf.RoundToInt(13f * Mathf.Clamp(m_size, 1f, 2f));
		m_advLabelStyle.normal.textColor = Color.white;
		m_advLabelStyle.alignment = TextAnchor.UpperLeft;
		m_advLabelStyle.wordWrap = false;
	}

	private void DrawGuiLine(Vector2 a, Vector2 b, Color col, float width)
	{
		Vector2 d = b - a;
		float len = d.magnitude;
		if (len < 0.1f) return;
		float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
		Matrix4x4 bak = GUI.matrix;
		GUIUtility.RotateAroundPivot(ang, a);
		GUI.color = col;
		GUI.DrawTexture(new Rect(a.x, a.y - width * 0.5f, len, width), Texture2D.whiteTexture);
		GUI.color = Color.white;
		GUI.matrix = bak;
	}

	private void DrawGuiArrow(Vector2 from, Vector2 to, Color col, float width)
	{
		DrawGuiLine(from, to, col, width);
		Vector2 d = (to - from);
		if (d.sqrMagnitude < 0.0001f) return;
		Vector2 dir = d.normalized;
		Vector2 perp = new Vector2(-dir.y, dir.x);
		float hl = 12f;
		DrawGuiLine(to, to - dir * hl + perp * (hl * 0.5f), col, width);
		DrawGuiLine(to, to - dir * hl - perp * (hl * 0.5f), col, width);
	}

	// 螺旋：从半径 r0 扫到 r1，扫角 sweepDeg（可为多圈）；末端带箭头
	private void DrawGuiSpiral(Vector2 center, float startDeg, float sweepDeg, float r0, float r1, Color col, float width)
	{
		int seg = Mathf.Clamp(Mathf.RoundToInt(Mathf.Abs(sweepDeg) / 12f), 8, 512);
		Vector2 prev = center + Deg2GuiVec(startDeg) * r0;
		for (int i = 1; i <= seg; i++)
		{
			float t = i / (float)seg;
			Vector2 p = center + Deg2GuiVec(startDeg + sweepDeg * t) * Mathf.Lerp(r0, r1, t);
			DrawGuiLine(prev, p, col, width);
			prev = p;
		}
		float endDeg = startDeg + sweepDeg;
		Vector2 dir = Deg2GuiVec(endDeg);
		Vector2 end = center + dir * r1;
		Vector2 tangent = new Vector2(-dir.y, dir.x) * Mathf.Sign(sweepDeg);
		DrawGuiArrow(end - tangent * 12f, end + tangent * 4f, col, width);
	}

	private void DrawGuiArc(Vector2 center, float radius, float startDeg, float sweepDeg, Color col, float width)
	{
		int seg = Mathf.Max(2, Mathf.RoundToInt(Mathf.Abs(sweepDeg) / 10f));
		float step = sweepDeg / seg;
		Vector2 prev = center + Deg2GuiVec(startDeg) * radius;
		for (int i = 1; i <= seg; i++)
		{
			Vector2 cur = center + Deg2GuiVec(startDeg + step * i) * radius;
			DrawGuiLine(prev, cur, col, width);
			prev = cur;
		}
		Vector2 endDir = Deg2GuiVec(startDeg + sweepDeg);
		Vector2 end = center + endDir * radius;
		Vector2 tangent = new Vector2(-endDir.y, endDir.x) * Mathf.Sign(sweepDeg);
		DrawGuiArrow(end - tangent * 12f, end + tangent * 4f, col, width);
	}

	private void DrawGuiWorldCircle(Camera cam, Vector2 center, float radius, Color col, float width)
	{
		const int seg = 48;
		bool hasPrev = false;
		Vector2 prev = Vector2.zero;
		for (int i = 0; i <= seg; i++)
		{
			float a = (i / (float)seg) * Mathf.PI * 2f;
			Vector2 wp = new Vector2(center.x + Mathf.Cos(a) * radius, center.y + Mathf.Sin(a) * radius);
			Vector3 sp = cam.WorldToScreenPoint(new Vector3(wp.x, wp.y, 0f));
			if (sp.z < 0f) { hasPrev = false; continue; }
			Vector2 cur = AdvWorldToGui(cam, wp);
			if (hasPrev) DrawGuiLine(prev, cur, col, width);
			prev = cur;
			hasPrev = true;
		}
	}

	// 半透明圆盘（参考点光源“光盾”的观感），作为把手更醒目、更好点
	private void DrawGuiDisc(Vector2 center, float radius, Color col)
	{
		float r = radius * m_size;
		if (r < 1f) return;
		GUI.color = col;
		GUI.DrawTexture(new Rect(center.x - r, center.y - r, r * 2f, r * 2f), AdvDiscTexture(), ScaleMode.StretchToFill, true);
		GUI.color = Color.white;
		DrawGuiCircle(center, r, new Color(col.r, col.g, col.b, 1f), 2f);
	}

	private Texture2D AdvDiscTexture()
	{
		if (m_advDiscTex != null) return m_advDiscTex;
		const int n = 64;
		Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
		t.hideFlags = HideFlags.HideAndDontSave;
		Color32[] px = new Color32[n * n];
		float c = (n - 1) * 0.5f;
		float R = c;
		for (int y = 0; y < n; y++)
		{
			for (int x = 0; x < n; x++)
			{
				float dx = x - c, dy = y - c;
				float d = Mathf.Sqrt(dx * dx + dy * dy);
				float a = Mathf.Clamp01((R - d) / 2f);      // 边缘 2px 渐隐
				byte b = (byte)(a * 180f);                   // 整体半透明
				px[y * n + x] = new Color32(255, 255, 255, b);
			}
		}
		t.SetPixels32(px);
		t.Apply();
		m_advDiscTex = t;
		return t;
	}

	private void DrawGuiCircle(Vector2 center, float radius, Color col, float width)
	{
		const int seg = 20;
		Vector2 prev = center + new Vector2(radius, 0f);
		for (int i = 1; i <= seg; i++)
		{
			float a = (i / (float)seg) * Mathf.PI * 2f;
			Vector2 cur = center + new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
			DrawGuiLine(prev, cur, col, width);
			prev = cur;
		}
	}

	private static Vector2 Deg2GuiVec(float deg)
	{
		float r = deg * Mathf.Deg2Rad;
		return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
	}

	private static float AdvPointSegmentDistanceRange(Vector2 p, Vector2 a, Vector2 b, float t0, float t1)
	{
		Vector2 ab = b - a;
		float len2 = ab.sqrMagnitude;
		if (len2 < 0.0001f) return Vector2.Distance(p, a);
		float t = Mathf.Clamp(Vector2.Dot(p - a, ab) / len2, t0, t1);
		return Vector2.Distance(p, a + ab * t);
	}

	private void DrawGuiLabel(string text, Vector2 pos, Color col)
	{
		if (string.IsNullOrEmpty(text)) return;
		Vector2 size = m_advLabelStyle.CalcSize(new GUIContent(text));
		float x = Mathf.Clamp(pos.x, 2f, Mathf.Max(2f, Screen.width - size.x - 6f));
		float y = Mathf.Clamp(pos.y, 2f, Mathf.Max(2f, Screen.height - size.y - 6f));
		DrawOutlineText(new Rect(x, y, size.x, size.y), text, col);
	}

	private void DrawGuiStack(Vector2 pos, string text)
	{
		Vector2 size = m_advLabelStyle.CalcSize(new GUIContent(text));
		float x = Mathf.Clamp(pos.x, 2f, Mathf.Max(2f, Screen.width - size.x - 6f));
		float y = Mathf.Clamp(pos.y, 2f, Mathf.Max(2f, Screen.height - size.y - 6f));
		DrawOutlineText(new Rect(x, y, size.x, size.y), text, Color.white);
	}

	// 无背景框：只给文字加黑色描边，保证在亮/暗背景上都可读
	private void DrawOutlineText(Rect r, string text, Color col)
	{
		Color bak = m_advLabelStyle.normal.textColor;
		m_advLabelStyle.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
		GUI.Label(new Rect(r.x - 1f, r.y, r.width, r.height), text, m_advLabelStyle);
		GUI.Label(new Rect(r.x + 1f, r.y, r.width, r.height), text, m_advLabelStyle);
		GUI.Label(new Rect(r.x, r.y - 1f, r.width, r.height), text, m_advLabelStyle);
		GUI.Label(new Rect(r.x, r.y + 1f, r.width, r.height), text, m_advLabelStyle);
		m_advLabelStyle.normal.textColor = col;
		GUI.Label(r, text, m_advLabelStyle);
		m_advLabelStyle.normal.textColor = bak;
	}
}
