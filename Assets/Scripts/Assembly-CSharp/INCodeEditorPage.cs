using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UButton = UnityEngine.UI.Button;

// 应用内“代码编辑器”页面（侧栏入口由 prefab 注册，与其它页面同级）。
// 代码集存于 应用数据目录 Settings/codesets/<集名>/*.cs —— 打包成软件后依然可用。
// 支持：代码集/文件 切换、新建/删除、导入(文件夹/.zip/.cs)/导出(文件夹/.zip)；
// 代码区拖动/滚轮滚动；文本搜索(上一个/下一个)；运行(复用 INCommandManager 的脚本引擎)。
public class INCodeEditorPage : MonoBehaviour
{
	public const string PageName = "CodeEditorInterface";
	public const string LocaleId = "AppInterface_CodeEditor";

	public static INCodeEditorPage Instance { get; private set; }

	private Font m_font;

	private InputField m_code;
	private Text m_codeText;
	private InputField m_search;
	private InputField m_path;
	private Text m_setText;
	private Text m_fileText;
	private Text m_output;

	private readonly StringBuilder m_log = new StringBuilder();

	private readonly List<string> m_sets = new List<string>();
	private readonly List<string> m_files = new List<string>();
	private int m_setIndex = -1;
	private int m_fileIndex = -1;

	private float m_codeScroll;
	private string m_lastGoodCode;
	private int m_searchStart;

	private const float CodeRowTop = 182f;

	private void Awake()
	{
		Instance = this;
		BuildUI();
	}

	private void OnDestroy()
	{
		if (Instance == this)
		{
			Instance = null;
		}
	}

	private void OnDisable()
	{
		// 离开页面时把当前内容写回当前代码集文件（存于数据目录，安全）
		SaveCurrentFile(quiet: true);
	}

	// ===== UI =====

	private void BuildUI()
	{
		if (m_code != null)
		{
			return;
		}
		m_font = INUiUtil.UiFont;

		Image bg = NewImage(transform, "Bg", new Color(0.06f, 0.07f, 0.09f, 0.98f));
		Stretch(bg.rectTransform, 0f, 0f, 0f, 0f);

		Text title = NewText(transform, "Title", 22, TextAnchor.MiddleLeft, Color.white);
		RowRect(title.rectTransform, 14f, 14f, 6f, 30f);
		title.text = "代码编辑器 · 代码集存于应用数据目录（打包可用） · 拖拽/滚轮滚动，可搜索";

		// Row1 代码集
		Btn("◀", 0.010f, 0.050f, 38f, 32f, S_NEUTRAL, OnPrevSet);
		m_setText = NewText(transform, "SetName", 20, TextAnchor.MiddleLeft, new Color(0.9f, 0.95f, 1f, 1f));
		AnchorRow(m_setText.rectTransform, 0.058f, 0.220f, 38f, 32f);
		Btn("▶", 0.228f, 0.268f, 38f, 32f, S_NEUTRAL, OnNextSet);
		Btn("新建集", 0.280f, 0.380f, 38f, 32f, S_NEUTRAL, OnNewSet);
		Btn("删除集", 0.388f, 0.488f, 38f, 32f, S_DANGER, OnDeleteSet);
		Btn("导入", 0.500f, 0.590f, 38f, 32f, S_BLUE, OnImport);
		Btn("导出", 0.598f, 0.688f, 38f, 32f, S_BLUE, OnExport);
		Btn("刷新", 0.696f, 0.786f, 38f, 32f, S_NEUTRAL, OnRefreshAll);

		// Row2 路径（导入/导出用）
		Text pl = NewText(transform, "PathLabel", 18, TextAnchor.MiddleLeft, new Color(0.8f, 0.85f, 0.9f, 1f));
		AnchorRow(pl.rectTransform, 0.010f, 0.058f, 72f, 32f);
		pl.text = "路径";
		m_path = NewInput(transform, "Path", false);
		AnchorRow((RectTransform)m_path.transform, 0.058f, 0.990f, 72f, 32f);

		// Row3 文件
		Btn("◀", 0.010f, 0.050f, 106f, 32f, S_NEUTRAL, OnPrevFile);
		m_fileText = NewText(transform, "FileName", 20, TextAnchor.MiddleLeft, new Color(0.9f, 1f, 0.9f, 1f));
		AnchorRow(m_fileText.rectTransform, 0.058f, 0.320f, 106f, 32f);
		Btn("▶", 0.328f, 0.368f, 106f, 32f, S_NEUTRAL, OnNextFile);
		Btn("新建文件", 0.380f, 0.500f, 106f, 32f, S_NEUTRAL, OnNewFile);
		Btn("删除文件", 0.508f, 0.628f, 106f, 32f, S_DANGER, OnDeleteFile);
		Btn("保存", 0.640f, 0.740f, 106f, 32f, S_GREEN, OnSave);

		// Row4 搜索 + 运行
		m_search = NewInput(transform, "Search", false);
		AnchorRow((RectTransform)m_search.transform, 0.010f, 0.440f, 140f, 32f);
		Btn("上一个", 0.448f, 0.548f, 140f, 32f, S_NEUTRAL, OnSearchPrev);
		Btn("下一个", 0.556f, 0.656f, 140f, 32f, S_NEUTRAL, OnSearchNext);
		Btn("运行", 0.676f, 0.786f, 140f, 32f, S_GREEN, OnRun);
		Btn("清空输出", 0.794f, 0.990f, 140f, 32f, S_NEUTRAL, OnClearOutput);

		// 代码区（带遮罩 + 手动滚动）
		m_code = NewInput(transform, "Code", true);
		RectTransform crt = (RectTransform)m_code.transform;
		crt.anchorMin = new Vector2(0f, 0.30f);
		crt.anchorMax = new Vector2(1f, 1f);
		crt.pivot = new Vector2(0.5f, 0.5f);
		crt.offsetMin = new Vector2(14f, 8f);
		crt.offsetMax = new Vector2(-14f, -CodeRowTop);
		m_code.gameObject.AddComponent<RectMask2D>();
		m_codeText = m_code.textComponent;
		SetupScrollableText(m_codeText);

		INScrollArea codeScroll = m_code.gameObject.AddComponent<INScrollArea>();
		codeScroll.PixelsPerStep = 28f;
		codeScroll.OnStep = OnCodeScrollStep;

		// 输出
		Image outBg = NewImage(transform, "OutBg", new Color(0f, 0f, 0f, 0.4f));
		RectTransform obrt = outBg.rectTransform;
		obrt.anchorMin = new Vector2(0f, 0f);
		obrt.anchorMax = new Vector2(1f, 0.28f);
		obrt.offsetMin = new Vector2(14f, 10f);
		obrt.offsetMax = new Vector2(-14f, -4f);
		outBg.gameObject.AddComponent<RectMask2D>();
		m_output = NewText(outBg.transform, "Out", 18, TextAnchor.UpperLeft, new Color(0.85f, 0.9f, 0.8f, 1f));
		Stretch(m_output.rectTransform, 10f, 6f, 10f, 6f);
		m_output.horizontalOverflow = HorizontalWrapMode.Wrap;
		m_output.verticalOverflow = VerticalWrapMode.Overflow;

		// 初始化代码集
		INCodeSetStore.EnsureRoot();
		m_sets.Clear();
		m_sets.AddRange(INCodeSetStore.ListSets());
		if (m_sets.Count == 0)
		{
			string created = INCodeSetStore.CreateSet();
			m_sets.Add(created);
		}
		m_setIndex = 0;
		LoadSet(m_sets[0], true);
	}

	private void SetupScrollableText(Text text)
	{
		if (text == null)
		{
			return;
		}
		RectTransform rt = text.rectTransform;
		rt.anchorMin = new Vector2(0f, 1f);
		rt.anchorMax = new Vector2(1f, 1f);
		rt.pivot = new Vector2(0.5f, 1f);
		rt.offsetMin = new Vector2(12f, 0f);
		rt.offsetMax = new Vector2(-12f, 0f);
		rt.anchoredPosition = new Vector2(0f, 0f);
	}

	// ===== 代码集 / 文件 =====

	private string CurrentSet
	{
		get { return (m_setIndex >= 0 && m_setIndex < m_sets.Count) ? m_sets[m_setIndex] : null; }
	}

	private string CurrentFile
	{
		get { return (m_fileIndex >= 0 && m_fileIndex < m_files.Count) ? m_files[m_fileIndex] : null; }
	}

	private void OnPrevSet()
	{
		if (m_sets.Count == 0)
		{
			return;
		}
		SaveCurrentFile(quiet: true);
		m_setIndex = (m_setIndex - 1 + m_sets.Count) % m_sets.Count;
		LoadSet(m_sets[m_setIndex], true);
	}

	private void OnNextSet()
	{
		if (m_sets.Count == 0)
		{
			return;
		}
		SaveCurrentFile(quiet: true);
		m_setIndex = (m_setIndex + 1) % m_sets.Count;
		LoadSet(m_sets[m_setIndex], true);
	}

	private void OnNewSet()
	{
		SaveCurrentFile(quiet: true);
		string name = INCodeSetStore.CreateSet();
		m_sets.Clear();
		m_sets.AddRange(INCodeSetStore.ListSets());
		m_setIndex = m_sets.IndexOf(name);
		if (m_setIndex < 0)
		{
			m_setIndex = 0;
		}
		LoadSet(name, true);
		Append("[新建代码集] " + name);
	}

	private void OnDeleteSet()
	{
		string set = CurrentSet;
		if (string.IsNullOrEmpty(set))
		{
			return;
		}
		if (INCodeSetStore.DeleteSet(set))
		{
			Append("[已删除代码集] " + set);
		}
		m_sets.Clear();
		m_sets.AddRange(INCodeSetStore.ListSets());
		if (m_sets.Count == 0)
		{
			m_sets.Add(INCodeSetStore.CreateSet());
		}
		m_setIndex = 0;
		LoadSet(m_sets[0], true);
	}

	private void OnRefreshAll()
	{
		SaveCurrentFile(quiet: true);
		string cur = CurrentSet;
		m_sets.Clear();
		m_sets.AddRange(INCodeSetStore.ListSets());
		if (m_sets.Count == 0)
		{
			m_sets.Add(INCodeSetStore.CreateSet());
		}
		m_setIndex = Mathf.Max(0, m_sets.IndexOf(cur));
		LoadSet(m_sets[m_setIndex], true);
		Append("[已刷新]");
	}

	private void LoadSet(string set, bool loadFirstFile)
	{
		m_files.Clear();
		m_files.AddRange(INCodeSetStore.ListFiles(set));
		m_fileIndex = m_files.Count > 0 ? 0 : -1;
		if (m_setText != null)
		{
			m_setText.text = set + "  (" + m_files.Count + " 文件)";
		}
		if (loadFirstFile)
		{
			if (m_files.Count == 0)
			{
				// 自动建一个默认文件
				string f = "script1.cs";
				INCodeSetStore.WriteFile(set, f, DefaultTemplate());
				m_files.Add(f);
				m_fileIndex = 0;
			}
			LoadCurrentFile();
		}
	}

	private void OnPrevFile()
	{
		if (m_files.Count == 0)
		{
			return;
		}
		SaveCurrentFile(quiet: true);
		m_fileIndex = (m_fileIndex - 1 + m_files.Count) % m_files.Count;
		LoadCurrentFile();
	}

	private void OnNextFile()
	{
		if (m_files.Count == 0)
		{
			return;
		}
		SaveCurrentFile(quiet: true);
		m_fileIndex = (m_fileIndex + 1) % m_files.Count;
		LoadCurrentFile();
	}

	private void OnNewFile()
	{
		string set = CurrentSet;
		if (string.IsNullOrEmpty(set))
		{
			return;
		}
		SaveCurrentFile(quiet: true);
		for (int i = 1; i < 1000; i++)
		{
			string name = "script" + i + ".cs";
			if (!m_files.Contains(name))
			{
				INCodeSetStore.WriteFile(set, name, DefaultTemplate());
				m_files.Add(name);
				m_files.Sort(StringComparer.Ordinal);
				m_fileIndex = m_files.IndexOf(name);
				LoadCurrentFile();
				Append("[新建文件] " + name);
				return;
			}
		}
	}

	private void OnDeleteFile()
	{
		string set = CurrentSet;
		string file = CurrentFile;
		if (string.IsNullOrEmpty(set) || string.IsNullOrEmpty(file))
		{
			return;
		}
		if (INCodeSetStore.DeleteFile(set, file))
		{
			Append("[已删除文件] " + file);
		}
		LoadSet(set, true);
	}

	private void LoadCurrentFile()
	{
		string set = CurrentSet;
		string file = CurrentFile;
		if (m_fileText != null)
		{
			m_fileText.text = file ?? "(无文件)";
		}
		string content = (set != null && file != null) ? INCodeSetStore.ReadFile(set, file) : null;
		if (content == null)
		{
			content = DefaultTemplate();
		}
		m_code.text = content;
		m_lastGoodCode = content;
		m_codeScroll = 0f;
		ApplyCodeScroll();
	}

	// 供“类目录”调用：切到指定代码集的指定文件
	public void LoadExternal(string set, string file)
	{
		if (string.IsNullOrEmpty(set) || string.IsNullOrEmpty(file))
		{
			return;
		}
		SaveCurrentFile(quiet: true);
		int si = m_sets.IndexOf(set);
		if (si < 0)
		{
			m_sets.Clear();
			m_sets.AddRange(INCodeSetStore.ListSets());
			si = m_sets.IndexOf(set);
		}
		if (si < 0)
		{
			return;
		}
		m_setIndex = si;
		LoadSet(set, false);
		int fi = m_files.IndexOf(file);
		m_fileIndex = fi >= 0 ? fi : (m_files.Count > 0 ? 0 : -1);
		LoadCurrentFile();
		OnClearOutput();
		Append("[已载入代码集] " + set + " / " + file);
	}

	private static string DefaultTemplate()
	{
		return "// 在此编写 C# 代码（Script 模式）。\n"
			+ "// 可访问游戏内部类型（如 BasePart / Contraption / WPFMonoBehaviour）。\n"
			+ "// 例：\n"
			+ "// BP.Feedback(\"BP> hello\");\n"
			+ "// return \"ok\";\n";
	}

	// ===== 保存 / 导入 / 导出 =====

	private void OnSave()
	{
		SaveCurrentFile(quiet: false);
	}

	private void SaveCurrentFile(bool quiet)
	{
		string set = CurrentSet;
		string file = CurrentFile;
		if (m_code == null || set == null || file == null)
		{
			return;
		}
		bool ok = INCodeSetStore.WriteFile(set, file, m_code.text);
		if (!quiet)
		{
			Append(ok ? ("[已保存] " + set + " / " + file) : "[保存失败]");
		}
		if (ok)
		{
			m_lastGoodCode = m_code.text;
		}
	}

	private void OnImport()
	{
		string set = CurrentSet;
		if (string.IsNullOrEmpty(set))
		{
			return;
		}
		string src = (m_path != null) ? m_path.text.Trim() : "";
		if (string.IsNullOrEmpty(src))
		{
			Append("[导入] 请在“路径”填写 文件夹 / .zip / .cs 的路径");
			return;
		}
		string err;
		int n = INCodeSetStore.Import(set, src, out err);
		if (!string.IsNullOrEmpty(err))
		{
			Append("[导入失败] " + err);
		}
		else
		{
			Append("[已导入 " + n + " 个 .cs 到代码集 " + set + "]");
		}
		LoadSet(set, m_files.Count == 0);
	}

	private void OnExport()
	{
		string set = CurrentSet;
		if (string.IsNullOrEmpty(set))
		{
			return;
		}
		string dst = (m_path != null) ? m_path.text.Trim() : "";
		if (string.IsNullOrEmpty(dst))
		{
			Append("[导出] 请在“路径”填写目标文件夹 或 以 .zip 结尾的路径");
			return;
		}
		string err;
		int n = INCodeSetStore.Export(set, dst, out err);
		if (!string.IsNullOrEmpty(err))
		{
			Append("[导出失败] " + err);
		}
		else
		{
			Append("[已导出 " + n + " 个 .cs → " + dst + "]");
		}
	}

	// ===== 代码滚动 =====

	private void OnCodeScrollStep(int steps)
	{
		float line = (m_codeText != null) ? Mathf.Max(1f, m_codeText.fontSize * m_codeText.lineSpacing) : 22f;
		m_codeScroll += steps * line;
		ApplyCodeScroll();
	}

	private void ApplyCodeScroll()
	{
		if (m_code == null || m_codeText == null)
		{
			return;
		}
		RectTransform vp = (RectTransform)m_code.transform;
		RectTransform tr = m_codeText.rectTransform;
		float viewportH = vp.rect.height;
		float contentH = GetContentHeight(tr.rect.width);
		tr.sizeDelta = new Vector2(tr.sizeDelta.x, contentH);
		float max = Mathf.Max(0f, contentH - viewportH);
		m_codeScroll = Mathf.Clamp(m_codeScroll, 0f, max);
		tr.anchoredPosition = new Vector2(tr.anchoredPosition.x, m_codeScroll);
	}

	private float GetContentHeight(float width)
	{
		try
		{
			if (width <= 1f)
			{
				width = 600f;
			}
			TextGenerationSettings settings = m_codeText.GetGenerationSettings(new Vector2(width, 0f));
			float h = m_codeText.cachedTextGeneratorForLayout.GetPreferredHeight(m_codeText.text, settings) / m_codeText.pixelsPerUnit;
			if (h <= 0f)
			{
				h = m_codeText.fontSize * m_codeText.lineSpacing;
			}
			return h + 8f;
		}
		catch
		{
			return m_codeText.fontSize * m_codeText.lineSpacing;
		}
	}

	// ===== 搜索 =====

	private void OnSearchPrev()
	{
		DoSearch(-1);
	}

	private void OnSearchNext()
	{
		DoSearch(1);
	}

	private void DoSearch(int dir)
	{
		string q = (m_search != null) ? m_search.text : null;
		string text = m_code.text;
		if (string.IsNullOrEmpty(q) || string.IsNullOrEmpty(text))
		{
			Append("[搜索] 请输入要查找的文本");
			return;
		}
		int idx = -1;
		if (dir > 0)
		{
			idx = text.IndexOf(q, Mathf.Clamp(m_searchStart, 0, text.Length), StringComparison.OrdinalIgnoreCase);
			if (idx < 0)
			{
				idx = text.IndexOf(q, 0, StringComparison.OrdinalIgnoreCase);
			}
		}
		else
		{
			int from = Mathf.Clamp(m_searchStart - q.Length - 1, 0, text.Length);
			idx = text.LastIndexOf(q, from, StringComparison.OrdinalIgnoreCase);
			if (idx < 0)
			{
				idx = text.LastIndexOf(q, text.Length - 1, StringComparison.OrdinalIgnoreCase);
			}
		}
		if (idx < 0)
		{
			Append("[搜索] 未找到：" + q);
			return;
		}
		m_searchStart = idx + q.Length;
		ScrollToIndex(idx);
		Append("[搜索] 找到于第 " + (CountLines(text, idx) + 1) + " 行");
	}

	private static int CountLines(string text, int index)
	{
		int n = 0;
		int lim = Mathf.Min(index, text.Length);
		for (int i = 0; i < lim; i++)
		{
			if (text[i] == '\n')
			{
				n++;
			}
		}
		return n;
	}

	private void ScrollToIndex(int index)
	{
		string text = m_code.text;
		int line = CountLines(text, index);
		float lineH = Mathf.Max(1f, m_codeText.fontSize * m_codeText.lineSpacing);
		m_codeScroll = line * lineH;
		ApplyCodeScroll();
	}

	// ===== 运行 =====

	private void OnRun()
	{
		OnClearOutput();
		SaveCurrentFile(quiet: true);

		INCommandManager cm = Singleton<INCommandManager>.Instance;
		if (cm == null)
		{
			Append("[无法运行] 指令系统（脚本引擎）不可用");
			return;
		}
		string code = m_code.text;
		if (string.IsNullOrWhiteSpace(code))
		{
			Append("[空代码]");
			return;
		}

		bool failed = false;
		int before = (cm.Writer != null && cm.Writer.Builder != null) ? cm.Writer.Builder.Length : 0;
		Application.LogCallback handler = OnUnityLog;
		Application.logMessageReceived += handler;
		try
		{
			Innovation.Script.ScriptEngine engine = cm.CreateEngine();
			var parse = engine.Parse(code);
			if (parse.Exception != null)
			{
				failed = true;
				Append("[解析错误] " + Describe(parse.Exception));
			}
			else
			{
				var result = engine.Run(parse.SyntaxTree);
				if (result.Exception != null)
				{
					failed = true;
					Append("[运行错误] " + Describe(result.Exception));
				}
				if (result.ReturnValue != null)
				{
					Append("[返回] " + SafeToString(result.ReturnValue));
				}
			}
		}
		catch (Exception e)
		{
			failed = true;
			Append("[异常] " + Describe(e));
		}
		finally
		{
			Application.logMessageReceived -= handler;
			try
			{
				int after = (cm.Writer != null && cm.Writer.Builder != null) ? cm.Writer.Builder.Length : before;
				if (after > before)
				{
					string added = cm.Writer.Builder.ToString(before, after - before);
					if (!string.IsNullOrEmpty(added))
					{
						Append("[输出] " + added.TrimEnd());
					}
				}
			}
			catch
			{
			}
		}

		if (failed)
		{
			// 出错导致不能运行：切回上一次可用版本（不写盘）
			if (!string.IsNullOrEmpty(m_lastGoodCode) && m_lastGoodCode != code)
			{
				m_code.text = m_lastGoodCode;
				m_codeScroll = 0f;
				ApplyCodeScroll();
				Append("[已还原] 已切回上次可用版本");
			}
			Feedback("BP> 代码有误，已还原为上次可用版本");
		}
		else
		{
			m_lastGoodCode = code;
		}
	}

	private void OnUnityLog(string condition, string stackTrace, LogType type)
	{
		if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
		{
			Append("[日志/" + type + "] " + condition);
		}
		else
		{
			Append("[日志] " + condition);
		}
	}

	private static string Describe(Exception e)
	{
		if (e == null)
		{
			return "(null)";
		}
		string s = e.GetType().Name;
		if (!string.IsNullOrEmpty(e.Message))
		{
			s += ": " + e.Message;
		}
		Exception inner = e.InnerException;
		int depth = 0;
		while (inner != null && depth < 3)
		{
			s += "\n  └ " + inner.GetType().Name + ": " + inner.Message;
			inner = inner.InnerException;
			depth++;
		}
		return s;
	}

	private static string SafeToString(object o)
	{
		try
		{
			return o.ToString();
		}
		catch
		{
			return o.GetType().ToString();
		}
	}

	private static void Feedback(string msg)
	{
		try
		{
			Innovation.BP.Feedback(msg);
		}
		catch
		{
		}
	}

	private void OnClearOutput()
	{
		m_log.Length = 0;
		if (m_output != null)
		{
			m_output.text = "";
		}
	}

	private void Append(string line)
	{
		if (m_log.Length > 20000)
		{
			m_log.Length = 0;
		}
		m_log.Append(line).Append('\n');
		if (m_output != null)
		{
			m_output.text = m_log.ToString();
		}
	}

	// ===== UI 辅助 =====

	private static readonly Color S_NEUTRAL = new Color(0.35f, 0.35f, 0.4f, 1f);
	private static readonly Color S_BLUE = new Color(0.22f, 0.44f, 0.86f, 1f);
	private static readonly Color S_GREEN = new Color(0.22f, 0.55f, 0.28f, 1f);
	private static readonly Color S_DANGER = new Color(0.6f, 0.25f, 0.25f, 1f);

	private void Stretch(RectTransform rt, float left, float bottom, float right, float top)
	{
		rt.anchorMin = Vector2.zero;
		rt.anchorMax = Vector2.one;
		rt.pivot = new Vector2(0.5f, 0.5f);
		rt.offsetMin = new Vector2(left, bottom);
		rt.offsetMax = new Vector2(-right, -top);
	}

	private void RowRect(RectTransform rt, float left, float right, float top, float height)
	{
		rt.anchorMin = new Vector2(0f, 1f);
		rt.anchorMax = new Vector2(1f, 1f);
		rt.pivot = new Vector2(0.5f, 1f);
		rt.offsetMin = new Vector2(left, -(top + height));
		rt.offsetMax = new Vector2(-right, -top);
	}

	private void AnchorRow(RectTransform rt, float x0, float x1, float top, float height)
	{
		rt.anchorMin = new Vector2(x0, 1f);
		rt.anchorMax = new Vector2(x1, 1f);
		rt.pivot = new Vector2(0.5f, 1f);
		rt.offsetMin = new Vector2(0f, -(top + height));
		rt.offsetMax = new Vector2(0f, -top);
	}

	private Image NewImage(Transform parent, string name, Color color)
	{
		GameObject go = new GameObject(name, typeof(RectTransform));
		RectTransform rt = go.GetComponent<RectTransform>();
		rt.SetParent(parent, false);
		Image image = go.AddComponent<Image>();
		image.color = color;
		return image;
	}

	private Text NewText(Transform parent, string name, int fontSize, TextAnchor anchor, Color color)
	{
		GameObject go = new GameObject(name, typeof(RectTransform));
		RectTransform rt = go.GetComponent<RectTransform>();
		rt.SetParent(parent, false);
		Text text = go.AddComponent<Text>();
		text.font = m_font;
		text.fontSize = fontSize;
		text.alignment = anchor;
		text.color = color;
		text.raycastTarget = false;
		text.horizontalOverflow = HorizontalWrapMode.Overflow;
		text.verticalOverflow = VerticalWrapMode.Overflow;
		return text;
	}

	private InputField NewInput(Transform parent, string name, bool multiLine)
	{
		GameObject go = new GameObject(name, typeof(RectTransform));
		RectTransform rt = go.GetComponent<RectTransform>();
		rt.SetParent(parent, false);

		Image bg = go.AddComponent<Image>();
		bg.color = new Color(1f, 1f, 1f, 0.08f);
		InputField input = go.AddComponent<InputField>();
		input.targetGraphic = bg;
		input.lineType = multiLine ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
		input.contentType = InputField.ContentType.Standard;

		Text text = NewText(go.transform, "Text", 20, multiLine ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft, Color.white);
		RectTransform trt = text.rectTransform;
		trt.anchorMin = Vector2.zero;
		trt.anchorMax = Vector2.one;
		trt.offsetMin = new Vector2(12f, 8f);
		trt.offsetMax = new Vector2(-12f, -8f);
		text.horizontalOverflow = multiLine ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
		text.verticalOverflow = VerticalWrapMode.Overflow;
		text.supportRichText = false;
		input.textComponent = text;

		Text placeholder = NewText(go.transform, "Placeholder", 20, multiLine ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.25f));
		RectTransform phrt = placeholder.rectTransform;
		phrt.anchorMin = Vector2.zero;
		phrt.anchorMax = Vector2.one;
		phrt.offsetMin = new Vector2(12f, 8f);
		phrt.offsetMax = new Vector2(-12f, -8f);
		placeholder.text = "";
		input.placeholder = placeholder;

		return input;
	}

	private void Btn(string label, float x0, float x1, float top, float height, Color color, UnityEngine.Events.UnityAction onClick)
	{
		GameObject go = new GameObject("Btn_" + label, typeof(RectTransform));
		RectTransform rt = go.GetComponent<RectTransform>();
		rt.SetParent(transform, false);
		AnchorRow(rt, x0, x1, top, height);

		Image bg = go.AddComponent<Image>();
		bg.color = color;
		UButton button = go.AddComponent<UButton>();
		button.targetGraphic = bg;
		button.onClick.AddListener(onClick);

		Text text = NewText(go.transform, "Text", 18, TextAnchor.MiddleCenter, Color.white);
		Stretch(text.rectTransform, 2f, 0f, 2f, 0f);
		text.text = label;
	}
}
