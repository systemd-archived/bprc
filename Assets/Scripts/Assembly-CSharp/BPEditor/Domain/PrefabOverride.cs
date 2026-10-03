using System;
using System.Collections.Generic;
using System.Text;

// 1:1 移植 badpiggies-editor 的 domain/prefab_override.rs
// （Unity ObjectDeserializer override 文本的解析/序列化）
public class BpOverrideNode
{
	public string nodeType = "";
	public string name = "";
	public string value; // null = 无值
	public List<BpOverrideNode> children = new List<BpOverrideNode>();

	public BpOverrideNode Child(string nodeType, string name)
	{
		foreach (BpOverrideNode n in children)
			if (n.nodeType == nodeType && n.name == name) return n;
		return null;
	}

	public List<BpOverrideNode> ChildrenOfType(string nodeType)
	{
		List<BpOverrideNode> list = new List<BpOverrideNode>();
		foreach (BpOverrideNode n in children) if (n.nodeType == nodeType) list.Add(n);
		return list;
	}

	// Component 名匹配：完全相等，或按 '.' 取最后一段相等
	public BpOverrideNode Component(string suffix)
	{
		foreach (BpOverrideNode n in children)
		{
			if (n.nodeType != "Component") continue;
			if (n.name == suffix) return n;
			int dot = n.name.LastIndexOf('.');
			if (dot >= 0 && n.name.Substring(dot + 1) == suffix) return n;
		}
		return null;
	}

	public float? ValueAsF32()
	{
		if (value == null) return null;
		float v;
		return float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v) ? v : (float?)null;
	}

	public int? ValueAsI32()
	{
		if (value == null) return null;
		int v;
		return int.TryParse(value, out v) ? v : (int?)null;
	}

	public bool? ValueAsBool()
	{
		if (value == null) return null;
		if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)) return true;
		if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)) return false;
		return null;
	}

	public BpOverrideNode FindDescendant(Func<BpOverrideNode, bool> pred)
	{
		if (pred(this)) return this;
		foreach (BpOverrideNode c in children)
		{
			BpOverrideNode found = c.FindDescendant(pred);
			if (found != null) return found;
		}
		return null;
	}
}

public static class BpPrefabOverride
{
	public static List<BpOverrideNode> ParseOverrideText(string raw)
	{
		string[] lines = (raw ?? "").Split('\n');
		for (int i = 0; i < lines.Length; i++) lines[i] = lines[i].TrimEnd('\r');
		return ParseOverrideRange(lines, 0, lines.Length, 0);
	}

	private static List<BpOverrideNode> ParseOverrideRange(string[] lines, int start, int end, int baseDepth)
	{
		List<BpOverrideNode> result = new List<BpOverrideNode>();
		int index = start;
		while (index < end)
		{
			string line = lines[index];
			int depth = Indentation(line);
			string trimmed = line.Trim();
			if (trimmed.Length == 0 || depth < baseDepth) { index++; continue; }
			if (depth > baseDepth) { index++; continue; }

			string nodeType, name;
			string value;
			ParseOverrideLine(trimmed, out nodeType, out name, out value);

			int childStart = index + 1;
			int childEnd = childStart;
			while (childEnd < end)
			{
				string childLine = lines[childEnd];
				int childDepth = Indentation(childLine);
				if (childLine.Trim().Length == 0) { childEnd++; continue; }
				if (childDepth <= depth) break;
				childEnd++;
			}

			List<BpOverrideNode> children = childStart < childEnd
				? ParseOverrideRange(lines, childStart, childEnd, depth + 1)
				: new List<BpOverrideNode>();

			result.Add(new BpOverrideNode { nodeType = nodeType, name = name, value = value, children = children });
			index = childEnd;
		}
		return result;
	}

	private static int Indentation(string line)
	{
		int n = 0;
		while (n < line.Length && line[n] == '\t') n++;
		return n;
	}

	private static void ParseOverrideLine(string trimmed, out string nodeType, out string name, out string value)
	{
		value = null;
		if (trimmed.Length > 0 && trimmed[0] == '\uFEFF') trimmed = trimmed.Substring(1);

		int eqPos = trimmed.IndexOf(" = ", StringComparison.Ordinal);
		if (eqPos < 0 && trimmed.EndsWith(" =", StringComparison.Ordinal)) eqPos = trimmed.Length - 2;
		if (eqPos >= 0)
		{
			string before = trimmed.Substring(0, eqPos);
			string after = eqPos + 3 <= trimmed.Length ? trimmed.Substring(eqPos + 3) : "";
			int sp = before.IndexOf(' ');
			nodeType = sp < 0 ? before : before.Substring(0, sp);
			name = sp < 0 ? "" : before.Substring(sp + 1);
			value = after;
			return;
		}

		int sp2 = trimmed.IndexOf(' ');
		nodeType = sp2 < 0 ? trimmed : trimmed.Substring(0, sp2);
		name = sp2 < 0 ? "" : trimmed.Substring(sp2 + 1);
		value = null;
	}

	public static string SerializeOverrideTree(List<BpOverrideNode> nodes)
	{
		return SerializeAtDepth(nodes, 0);
	}

	private static string SerializeAtDepth(List<BpOverrideNode> nodes, int depth)
	{
		string indent = new string('\t', depth);
		StringBuilder sb = new StringBuilder();
		foreach (BpOverrideNode n in nodes)
		{
			if (n.value != null) sb.Append(indent).Append(n.nodeType).Append(' ').Append(n.name).Append(" = ").Append(n.value).Append('\n');
			else sb.Append(indent).Append(n.nodeType).Append(' ').Append(n.name).Append('\n');
			sb.Append(SerializeAtDepth(n.children, depth + 1));
		}
		return sb.ToString();
	}

	public static BpOverrideNode FindFirstNode(List<BpOverrideNode> nodes, Func<BpOverrideNode, bool> pred)
	{
		foreach (BpOverrideNode n in nodes)
		{
			BpOverrideNode found = n.FindDescendant(pred);
			if (found != null) return found;
		}
		return null;
	}
}
