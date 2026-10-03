using System.Collections.Generic;
using UnityEngine;

// 1:1 移植 badpiggies-editor 的 domain/level/ops.rs：
// delete_object / move_object / collect_descendants / clone_subtree / paste_subtree。
// 说明：Rust 的 impl LevelData 在 C# 用静态类方法承载；返回 Option<ObjectIndex> 用 -1 表示 None。
public static class BpLevelOps
{
	private static void CollectDescendants(List<BpLevelObject> objects, int idx, HashSet<int> set)
	{
		set.Add(idx);
		if (idx < 0 || idx >= objects.Count) return;
		BpParentObject p = objects[idx].AsParent();
		if (p != null)
		{
			foreach (int child in p.children) CollectDescendants(objects, child, set);
		}
	}

	// 删除对象（若是父级则连后代一起删），并重映射 roots/children/parent
	public static void DeleteObject(BpLevelData level, int target)
	{
		if (target < 0 || target >= level.objects.Count) return;

		HashSet<int> toDelete = new HashSet<int>();
		CollectDescendants(level.objects, target, toDelete);

		// 旧索引 -> 新索引（被删者为 null）
		List<int> remap = new List<int>(level.objects.Count);
		int newIdx = 0;
		for (int i = 0; i < level.objects.Count; i++)
		{
			if (toDelete.Contains(i)) remap.Add(-1);
			else { remap.Add(newIdx); newIdx++; }
		}

		// roots
		List<int> newRoots = new List<int>();
		foreach (int r in level.roots)
		{
			if (toDelete.Contains(r)) continue;
			newRoots.Add(remap[r]);
		}
		level.roots = newRoots;

		List<BpLevelObject> old = new List<BpLevelObject>(level.objects);
		level.objects.Clear();
		for (int i = 0; i < old.Count; i++)
		{
			if (toDelete.Contains(i)) continue;
			BpLevelObject obj = old[i];
			if (obj.isPrefab)
			{
				BpPrefabInstance p = obj.prefab;
				if (p.hasParent) p.parent = remap[p.parent];
				level.objects.Add(obj);
			}
			else
			{
				BpParentObject p = obj.parentObj;
				List<int> kept = new List<int>();
				foreach (int c in p.children)
				{
					if (!toDelete.Contains(c)) kept.Add(remap[c]);
				}
				p.children = kept;
				if (p.hasParent) p.parent = remap[p.parent];
				level.objects.Add(obj);
			}
		}
	}

	// 把 source 移动到 dropPos 描述的位置；返回新索引，无效返回 -1
	public static int MoveObject(BpLevelData level, int source, BpDropPosition dropPos)
	{
		if (source < 0 || source >= level.objects.Count) return -1;

		int destIdx = dropPos.index;

		// 禁止把父级拖进自己的子树
		HashSet<int> ancestors = new HashSet<int>();
		CollectDescendants(level.objects, source, ancestors);
		if (ancestors.Contains(destIdx)) return -1;

		// 1. 从原父级（或 roots）摘下
		bool srcHasParent = level.objects[source].isPrefab
			? level.objects[source].prefab.hasParent
			: level.objects[source].parentObj.hasParent;
		int srcParent = level.objects[source].isPrefab
			? level.objects[source].prefab.parent
			: level.objects[source].parentObj.parent;

		if (srcHasParent)
		{
			BpParentObject pp = level.objects[srcParent].AsParent();
			if (pp != null) pp.children.Remove(source);
		}
		else
		{
			level.roots.Remove(source);
		}

		// 2. 目标父级与插入位置
		int target = dropPos.index;
		bool isBefore = dropPos.kind == BpDropKind.Before;
		bool intoParent = dropPos.kind == BpDropKind.IntoParent;

		if (intoParent)
		{
			if (level.objects[source].isPrefab)
			{
				level.objects[source].prefab.hasParent = true;
				level.objects[source].prefab.parent = target;
			}
			else
			{
				level.objects[source].parentObj.hasParent = true;
				level.objects[source].parentObj.parent = target;
			}
			BpParentObject tp = level.objects[target].AsParent();
			if (tp != null) tp.children.Add(source);
		}
		else
		{
			bool tgtHasParent = level.objects[target].isPrefab
				? level.objects[target].prefab.hasParent
				: level.objects[target].parentObj.hasParent;
			int targetParent = level.objects[target].isPrefab
				? level.objects[target].prefab.parent
				: level.objects[target].parentObj.parent;

			if (level.objects[source].isPrefab)
			{
				level.objects[source].prefab.hasParent = tgtHasParent;
				level.objects[source].prefab.parent = targetParent;
			}
			else
			{
				level.objects[source].parentObj.hasParent = tgtHasParent;
				level.objects[source].parentObj.parent = targetParent;
			}

			if (tgtHasParent)
			{
				BpParentObject tp = level.objects[targetParent].AsParent();
				if (tp != null)
				{
					int pos = tp.children.IndexOf(target);
					if (pos < 0) pos = 0;
					int insertAt = isBefore ? pos : pos + 1;
					tp.children.Insert(insertAt, source);
				}
			}
			else
			{
				int pos = level.roots.IndexOf(target);
				if (pos < 0) pos = 0;
				int insertAt = isBefore ? pos : pos + 1;
				level.roots.Insert(insertAt, source);
			}
		}
		return source;
	}

	// 深拷贝以 rootIdx 为根的子树为自包含列表（root 恒为 0），内部 parent/children 重映射
	public static List<BpLevelObject> CloneSubtree(BpLevelData level, int rootIdx)
	{
		List<int> indices = new List<int>();
		Queue<int> queue = new Queue<int>();
		queue.Enqueue(rootIdx);
		while (queue.Count > 0)
		{
			int idx = queue.Dequeue();
			indices.Add(idx);
			BpParentObject p = level.objects[idx].AsParent();
			if (p != null)
			{
				foreach (int c in p.children) queue.Enqueue(c);
			}
		}

		Dictionary<int, int> remap = new Dictionary<int, int>();
		for (int i = 0; i < indices.Count; i++) remap[indices[i]] = i;

		List<BpLevelObject> outList = new List<BpLevelObject>(indices.Count);
		foreach (int oldIdx in indices)
		{
			BpLevelObject obj = CloneObject(level.objects[oldIdx]);
			if (obj.isPrefab)
			{
				if (obj.prefab.hasParent && remap.TryGetValue(obj.prefab.parent, out int np))
				{
					obj.prefab.parent = np;
				}
				else obj.prefab.hasParent = false;
			}
			else
			{
				if (obj.parentObj.hasParent && remap.TryGetValue(obj.parentObj.parent, out int np2))
				{
					obj.parentObj.parent = np2;
				}
				else obj.parentObj.hasParent = false;
				List<int> kids = new List<int>();
				foreach (int c in obj.parentObj.children)
				{
					if (remap.TryGetValue(c, out int nc)) kids.Add(nc);
				}
				obj.parentObj.children = kids;
			}
			outList.Add(obj);
		}
		return outList;
	}

	// 把子树粘贴进关卡，返回粘贴根索引
	public static int PasteSubtree(BpLevelData level, List<BpLevelObject> subtree, BpPastePosition pastePosition)
	{
		bool rootHasParent = false;
		int rootParent = -1;
		if (pastePosition.kind == BpPasteKind.AppendTo)
		{
			rootHasParent = pastePosition.hasParent;
			rootParent = pastePosition.parent;
		}
		else if (pastePosition.exact.kind == BpDropKind.IntoParent)
		{
			rootHasParent = true;
			rootParent = pastePosition.exact.index;
		}
		else
		{
			int target = pastePosition.exact.index;
			if (target >= 0 && target < level.objects.Count)
			{
				rootHasParent = level.objects[target].isPrefab
					? level.objects[target].prefab.hasParent
					: level.objects[target].parentObj.hasParent;
				rootParent = level.objects[target].isPrefab
					? level.objects[target].prefab.parent
					: level.objects[target].parentObj.parent;
			}
		}

		int baseIdx = level.objects.Count;
		for (int i = 0; i < subtree.Count; i++)
		{
			BpLevelObject obj = CloneObject(subtree[i]);
			if (obj.isPrefab)
			{
				if (obj.prefab.hasParent) obj.prefab.parent += baseIdx;
				else obj.prefab.hasParent = false;
			}
			else
			{
				if (obj.parentObj.hasParent) obj.parentObj.parent += baseIdx;
				else obj.parentObj.hasParent = false;
				List<int> kids = new List<int>(obj.parentObj.children.Count);
				foreach (int c in obj.parentObj.children) kids.Add(c + baseIdx);
				obj.parentObj.children = kids;
			}
			if (i == 0)
			{
				if (obj.isPrefab)
				{
					obj.prefab.hasParent = rootHasParent;
					obj.prefab.parent = rootParent;
				}
				else
				{
					obj.parentObj.hasParent = rootHasParent;
					obj.parentObj.parent = rootParent;
				}
			}
			level.objects.Add(obj);
		}

		if (pastePosition.kind == BpPasteKind.AppendTo)
		{
			if (pastePosition.hasParent)
			{
				BpParentObject pp = (pastePosition.parent >= 0 && pastePosition.parent < level.objects.Count)
					? level.objects[pastePosition.parent].AsParent()
					: null;
				if (pp != null) pp.children.Add(baseIdx);
				else level.roots.Add(baseIdx);
			}
			else level.roots.Add(baseIdx);
		}
		else if (pastePosition.exact.kind == BpDropKind.IntoParent)
		{
			int target = pastePosition.exact.index;
			BpParentObject pp = (target >= 0 && target < level.objects.Count)
				? level.objects[target].AsParent()
				: null;
			if (pp != null) pp.children.Add(baseIdx);
			else level.roots.Add(baseIdx);
		}
		else
		{
			int target = pastePosition.exact.index;
			bool isBefore = pastePosition.exact.kind == BpDropKind.Before;
			bool tgtHasParent = false;
			int targetParent = -1;
			if (target >= 0 && target < level.objects.Count)
			{
				tgtHasParent = level.objects[target].isPrefab
					? level.objects[target].prefab.hasParent
					: level.objects[target].parentObj.hasParent;
				targetParent = level.objects[target].isPrefab
					? level.objects[target].prefab.parent
					: level.objects[target].parentObj.parent;
			}

			if (tgtHasParent)
			{
				BpParentObject pp = level.objects[targetParent].AsParent();
				if (pp != null)
				{
					int pos = pp.children.IndexOf(target);
					int insertAt = pos < 0 ? pp.children.Count : (isBefore ? pos : pos + 1);
					pp.children.Insert(insertAt, baseIdx);
				}
				else level.roots.Add(baseIdx);
			}
			else
			{
				int pos = level.roots.IndexOf(target);
				int insertAt = pos < 0 ? level.roots.Count : (isBefore ? pos : pos + 1);
				level.roots.Insert(insertAt, baseIdx);
			}
		}
		return baseIdx;
	}

	// ---------------- 深拷贝（Rust 的 Clone 语义） ----------------

	public static BpLevelObject CloneObject(BpLevelObject obj)
	{
		if (obj == null) return null;
		if (obj.isPrefab)
		{
			BpPrefabInstance s = obj.prefab;
			BpPrefabInstance p = new BpPrefabInstance
			{
				name = s.name,
				position = s.position,
				prefabIndex = s.prefabIndex,
				rotation = s.rotation,
				scale = s.scale,
				dataType = s.dataType,
				hasParent = s.hasParent,
				parent = s.parent,
				terrainData = CloneTerrain(s.terrainData),
				overrideData = s.overrideData == null
					? null
					: new BpPrefabOverrideData { rawText = s.overrideData.rawText, rawBytes = (byte[])s.overrideData.rawBytes.Clone() }
			};
			return BpLevelObject.OfPrefab(p);
		}
		BpParentObject sp = obj.parentObj;
		BpParentObject pp = new BpParentObject
		{
			name = sp.name,
			position = sp.position,
			children = new List<int>(sp.children),
			hasParent = sp.hasParent,
			parent = sp.parent
		};
		return BpLevelObject.OfParent(pp);
	}

	public static TTerrData CloneTerrain(TTerrData t)
	{
		if (t == null) return null;
		TTerrData c = new TTerrData
		{
			fillTextureTileOffsetX = t.fillTextureTileOffsetX,
			fillTextureTileOffsetY = t.fillTextureTileOffsetY,
			fillColor = t.fillColor,
			fillTextureIndex = t.fillTextureIndex,
			fillTexture = t.fillTexture,
			controlTextureCount = t.controlTextureCount,
			controlTextureData = t.controlTextureData == null ? null : (byte[])t.controlTextureData.Clone(),
			hasCollider = t.hasCollider,
			hasFillBoundary = t.hasFillBoundary,
			fillBoundary = t.fillBoundary == null ? null : (float[])t.fillBoundary.Clone(),
			fillMesh = CloneMesh(t.fillMesh),
			curveMesh = CloneMesh(t.curveMesh)
		};
		c.curveTextures = new List<TCurveTexture>();
		if (t.curveTextures != null)
		{
			foreach (TCurveTexture ct in t.curveTextures)
			{
				c.curveTextures.Add(new TCurveTexture(ct.texture)
				{
					size = ct.size,
					fixedAngle = ct.fixedAngle,
					fadeThreshold = ct.fadeThreshold
				});
			}
		}
		return c;
	}

	private static TTerrMesh CloneMesh(TTerrMesh m)
	{
		return new TTerrMesh
		{
			vertices = m.vertices == null ? new List<Vector2>() : new List<Vector2>(m.vertices),
			indices = m.indices == null ? new List<int>() : new List<int>(m.indices)
		};
	}
}
