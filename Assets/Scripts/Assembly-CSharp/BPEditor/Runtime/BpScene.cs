using System.Collections.Generic;
using System.Text;

// 1:1 移植 badpiggies-editor 的 unity_runtime/{scene,reflection}.rs（P0 脚手架）。
// GameObject / Component 存于两个平行 arena，用 int 索引句柄（对应 Rust 的
// GameObjectId / ComponentId；Rust 用 u32 newtype，C# 用 int，-1 表示 None）。
// 本类同时实现 IBpRuntimeHost（= Rust `impl RuntimeHost for Scene`），
// 使 BpObjectDeserializer（walker）可以真正驱动它。

public class BpGameObject
{
	public string name = "";
	public int parent = -1;           // -1 = 无父（None）
	public List<int> children = new List<int>();
	public List<int> components = new List<int>();
}

public class BpComponentSlot
{
	public int owner;
	public BpUnityComponent behavior;
}

public class BpScene : IBpRuntimeHost
{
	private readonly List<BpGameObject> m_gameObjects = new List<BpGameObject>();
	private readonly List<BpComponentSlot> m_components = new List<BpComponentSlot>();

	// permissive：find_child 时自动补建缺失子对象（供 FromOverrideText 从纯文本重建场景图）
	public bool m_permissive;

	// ---------------- 分配 ----------------

	public int CreateGameObject(string name, int parent)
	{
		BpGameObject go = new BpGameObject();
		go.name = name ?? "";
		go.parent = parent;
		int id = m_gameObjects.Count;
		m_gameObjects.Add(go);
		if (parent >= 0) m_gameObjects[parent].children.Add(id);
		return id;
	}

	public int CreateGameObject(string name) { return CreateGameObject(name, -1); }

	public int AttachComponent(int owner, BpUnityComponent behavior)
	{
		BpComponentSlot slot = new BpComponentSlot();
		slot.owner = owner;
		slot.behavior = behavior;
		int id = m_components.Count;
		m_components.Add(slot);
		m_gameObjects[owner].components.Add(id);
		return id;
	}

	public int AttachUnknownComponent(int owner, string suffix)
	{
		return AttachComponent(owner, new BpUnknownComponent(suffix));
	}

	// ---------------- 查询 ----------------

	public BpGameObject GameObject(int id) { return m_gameObjects[id]; }
	public BpUnityComponent Behavior(int id) { return m_components[id].behavior; }
	public int ComponentOwner(int id) { return m_components[id].owner; }

	public int GameObjectCount { get { return m_gameObjects.Count; } }
	public int ComponentCount { get { return m_components.Count; } }

	// 所有组件 extra 字段数合计
	public int FieldCount
	{
		get
		{
			int n = 0;
			foreach (BpComponentSlot c in m_components)
				if (c.behavior != null && c.behavior.Extra != null) n += c.behavior.Extra.Count;
			return n;
		}
	}

	// transform.Find(name) —— 第一个同名直接子对象；-1 = 未找到
	public int FindChild(int parent, string name)
	{
		BpGameObject p = m_gameObjects[parent];
		foreach (int childId in p.children)
			if (m_gameObjects[childId].name == name) return childId;
		return -1;
	}

	// obj.GetComponent(suffix) —— 第一个 suffix 匹配的组件；-1 = 未找到
	public int GetComponent(int owner, string suffix)
	{
		BpGameObject o = m_gameObjects[owner];
		foreach (int cid in o.components)
			if (m_components[cid].behavior.ComponentSuffix == suffix) return cid;
		return -1;
	}

	// component_as::<T>() —— 下转（C# 用 as；Rust 用 Any::downcast_ref）
	public T ComponentAs<T>(int id) where T : class
	{
		return m_components[id].behavior as T;
	}

	// get_component_of::<T>() —— owner 上第一个可下转为 T 的组件；-1 = 无
	public int GetComponentOf<T>(int owner, out T component) where T : class
	{
		foreach (int cid in m_gameObjects[owner].components)
		{
			T t = m_components[cid].behavior as T;
			if (t != null) { component = t; return cid; }
		}
		component = null;
		return -1;
	}

	// ---------------- IBpRuntimeHost（= reflection.rs）----------------

	public BpValue ReferencedObject(int index)
	{
		// 引用表由 LevelLoader 侧填充；裸 Scene 没有引用。
		return null;
	}

	public int FindChild(int parent, string name, out bool found)
	{
		int hit = FindChild(parent, name);
		if (hit >= 0) { found = true; return hit; }
		// permissive：补建缺失子对象，避免 walker 丢弃其子树
		if (m_permissive) { found = true; return CreateGameObject(name, parent); }
		found = false;
		return -1;
	}

	public int GetComponent(int obj, string name, out bool found)
	{
		int hit = GetComponent(obj, name);
		found = hit >= 0;
		return hit;
	}

	public int AddComponent(int obj, string name, out bool found)
	{
		BpUnityComponent behavior = BpComponentRegistry.MakeComponentBySuffix(name);
		if (behavior == null) behavior = new BpUnknownComponent(name);
		found = true;
		return AttachComponent(obj, behavior);
	}

	public string GameObjectName(int obj) { return m_gameObjects[obj].name; }

	public void SetField(int target, string field, BpValue value)
	{
		BpUnityComponent b = Behavior(target);
		if (!b.SetField(this, field, value))
			b.Extra.Add(new KeyValuePair<string, BpValue>(field, value));
	}

	public BpValue GetField(int target, string field, out bool found)
	{
		BpValue v = Behavior(target).GetField(this, field);
		found = v != null;
		return v;
	}

	// Rust 供宿主按需覆盖；walker 不调用。P0 无结构体目标 → false。
	public bool AsStructTarget(ref BpValue value, out int target)
	{
		target = -1;
		return false;
	}

	public void SendMessage(int obj, string message)
	{
		if (message != "OnDataLoaded") return;
		// BroadcastMessage 式传播：接收者 + 全部后代
		Stack<int> stack = new Stack<int>();
		stack.Push(obj);
		while (stack.Count > 0)
		{
			int go = stack.Pop();
			List<int> comps = new List<int>(m_gameObjects[go].components);
			foreach (int cid in comps)
				if (m_components[cid].behavior != null) m_components[cid].behavior.OnDataLoaded(this);
			foreach (int ch in m_gameObjects[go].children) stack.Push(ch);
		}
	}

	public void SetAnimationCurve(int target, string field, List<BpKeyframe> keys)
	{
		// 作为 Generic 落到常规 set_field 路径；拥有曲线的类型化组件可覆盖 set_field 拦截。
		BpValue value = BpValue.Generic();
		for (int i = 0; i < keys.Count; i++)
			value.generic.Add(new KeyValuePair<string, BpValue>(
				i.ToString(System.Globalization.CultureInfo.InvariantCulture),
				BpValue.OfKeyframe(keys[i])));
		SetField(target, field, value);
	}

	public void SetArray(int target, string field, int size, List<BpArrayElement> elements)
	{
		BpValue value = BpValue.Generic();
		value.generic.Add(new KeyValuePair<string, BpValue>("size", BpValue.Integer(size)));
		foreach (BpArrayElement el in elements)
			value.generic.Add(new KeyValuePair<string, BpValue>(
				el.index.ToString(System.Globalization.CultureInfo.InvariantCulture), el.value));
		SetField(target, field, value);
	}

	public void SetObjectReferenceIndex(int target, string field, int index)
	{
		BpUnityComponent b = Behavior(target);
		if (!b.SetObjectReferenceIndex(this, field, index))
		{
			// 溢出为显式 "(field, Generic{_unresolvedObjectReference=idx})"，供 P3 原样回写。
			BpValue synthetic = BpValue.Generic();
			synthetic.generic.Add(new KeyValuePair<string, BpValue>(
				"_unresolvedObjectReference", BpValue.Integer(index)));
			b.Extra.Add(new KeyValuePair<string, BpValue>(field, synthetic));
		}
	}

	// ---------- ParticleSystem 反射钩子 ----------
	public bool IsParticleSystem(int target) { return ComponentAs<BpParticleSystemComponent>(target) != null; }

	public void SetParticleStartLifetime(int target, float value)
	{
		BpParticleSystemComponent ps = ComponentAs<BpParticleSystemComponent>(target);
		if (ps != null) ps.startLifetime = value;
	}

	public void SetParticleStartSpeed(int target, float value)
	{
		BpParticleSystemComponent ps = ComponentAs<BpParticleSystemComponent>(target);
		if (ps != null) ps.startSpeed = value;
	}

	public void SetParticleEmissionRate(int target, float value)
	{
		BpParticleSystemComponent ps = ComponentAs<BpParticleSystemComponent>(target);
		if (ps != null) ps.emissionRate = value;
	}

	// ---------------- Scene::from_override_text ----------------

	// 从 prefab-override 文本以 permissive 模式合成场景：文本引用的任何子 GameObject
	// 都会被自动创建（而非丢弃），使消费者无需先实例化真实 prefab 即可读取类型化组件。
	// 文本不以 `GameObject <name>` 头开始时返回 null。
	public static BpScene FromOverrideText(string text, out int root)
	{
		root = -1;
		if (text == null) return null;
		// 容忍起始 UTF-8 BOM —— 随包 *.bytes 内含，读行器自身不剥离。
		if (text.Length > 0 && text[0] == '\uFEFF') text = text.Substring(1);

		string rootName = PeekRootGameObjectName(text);
		string ownedBuf = null;
		if (rootName == null)
		{
			if (PeekStartsWithKind(text, "Component"))
			{
				// 文本直接以 `Component …` 块开头（无 GameObject 头）：
				// 合成一个占位根，并把每行多缩进一个 tab，使裸 Component 块落在
				// walker 期望的深度上。
				rootName = "__synthetic_root";
				StringBuilder sb = new StringBuilder();
				sb.Append("GameObject __synthetic_root\n");
				foreach (string line in BpObjectDeserializer.SplitLinesLikeRust(text))
				{
					sb.Append('\t');
					sb.Append(line);
					sb.Append('\n');
				}
				ownedBuf = sb.ToString();
			}
			else
			{
				return null;
			}
		}

		string textForReader = ownedBuf != null ? ownedBuf : text;
		BpScene scene = new BpScene();
		scene.m_permissive = true;
		root = scene.CreateGameObject(rootName, -1);
		BpObjectDeserializer.ReadPrefabOverrides(scene, root, textForReader);
		scene.m_permissive = false;
		return scene;
	}

	// 第一个非空、零缩进行若形如 `GameObject <name>` 则返回 <name>。
	private static string PeekRootGameObjectName(string text)
	{
		foreach (string raw in BpObjectDeserializer.SplitLinesLikeRust(text))
		{
			if (raw.StartsWith("\t")) continue;
			string trimmed = raw.TrimEnd('\r');
			if (trimmed.Length == 0) continue;
			int sp = trimmed.IndexOf(' ');
			if (sp < 0) return null;
			if (trimmed.Substring(0, sp) != "GameObject") return null;
			return trimmed.Substring(sp + 1);
		}
		return null;
	}

	// 第一个非空、零缩进行是否以 `<kind> ` 开头。
	private static bool PeekStartsWithKind(string text, string kind)
	{
		foreach (string raw in BpObjectDeserializer.SplitLinesLikeRust(text))
		{
			if (raw.StartsWith("\t")) continue;
			string trimmed = raw.TrimEnd('\r');
			if (trimmed.Length == 0) continue;
			int sp = trimmed.IndexOf(' ');
			if (sp < 0) return false;
			return trimmed.Substring(0, sp) == kind;
		}
		return false;
	}
}
