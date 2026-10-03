using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// 数学表达式求值器，支持：
/// - y=sin(x), x^2, sqrt(x) 等函数表达式（y= 前缀可省略）
/// - x^2+y^2=25 等隐式方程（自动数值求解曲线）
/// - 多表达式用 ; 分隔（每个独立生成一条曲线）
/// - 隐式乘法：2x→2*x, 2(→2*(, )x→)*x
/// - 支持变量 x, y, 常量 pi, e
/// </summary>
public class INMathExpressionEvaluator
{
	private string m_expression;
	private int m_pos;
	private static float s_currentX;
	private static float s_currentY;
	private static bool s_useDegrees = true; // 默认角度制

	public static void SetAngleMode(bool degrees) { s_useDegrees = degrees; }
	public static bool IsDegrees() { return s_useDegrees; }
	private static float ToRad(float deg) { return deg * Mathf.Deg2Rad; }
	private static float FromRad(float rad) { return rad * Mathf.Rad2Deg; }

	// ===== 公共 API =====

	/// <summary>
	/// 将输入字符串解析为一条或多条曲线点数组。
	/// 支持格式：
	///   "sin(x)"          → y=sin(x)
	///   "y=sin(x)"        → y=sin(x)
	///   "x^2+y^2=25"      → 圆（隐式方程，数值求解）
	///   "y=x;y=-x"        → 两条线（分号分隔）
	/// </summary>
	/// <param name="input">用户输入的表达式</param>
	/// <param name="originX">原点X</param>
	/// <param name="originY">原点Y</param>
	/// <param name="rangeHalf">半宽（世界坐标范围的一半）</param>
	/// <param name="multiplier">Y缩放倍数</param>
	/// <param name="steps">每条曲线的采样点数</param>
	/// <returns>所有曲线的点数组列表</returns>
	public static List<Vector2[]> ParseAndGenerateCurves(string input, float originX, float originY, float rangeHalf, float multiplier, int steps)
	{
		List<Vector2[]> curves = new List<Vector2[]>();
		if (string.IsNullOrWhiteSpace(input)) return curves;

		// 分号分隔多个表达式
		string[] parts = input.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
		foreach (string part in parts)
		{
			string trimmed = part.Trim();
			if (string.IsNullOrEmpty(trimmed)) continue;

			List<Vector2[]> subCurves = ParseSingleExpression(trimmed, originX, originY, rangeHalf, multiplier, steps);
			curves.AddRange(subCurves);
		}
		return curves;
	}

	/// <summary>
	/// 解析单个表达式，返回0条或多条曲线（隐式方程可能返回多条）
	/// </summary>
	private static List<Vector2[]> ParseSingleExpression(string expr, float originX, float originY, float rangeHalf, float multiplier, int steps)
	{
		List<Vector2[]> curves = new List<Vector2[]>();
		string normalized = expr.ToLower(CultureInfo.InvariantCulture).Replace(" ", "");

		// 提取 [条件] 范围后缀，如 sin(x)[-1<x<10]
		string rangeExpr = "";
		int bracketStart = normalized.LastIndexOf('[');
		int bracketEnd = normalized.LastIndexOf(']');
		if (bracketStart >= 0 && bracketEnd > bracketStart)
		{
			rangeExpr = normalized.Substring(bracketStart + 1, bracketEnd - bracketStart - 1);
			normalized = normalized.Substring(0, bracketStart);
		}

		// 检测是否包含等号
		int eqIndex = FindMainEquals(normalized);
		if (eqIndex < 0)
		{
			curves.Add(GenerateExplicitCurve(normalized, originX, originY, rangeHalf, multiplier, steps));
		}
		else
		{
			string left = normalized.Substring(0, eqIndex);
			string right = normalized.Substring(eqIndex + 1);

			if (left == "y")
			{
				curves.Add(GenerateExplicitCurve(right, originX, originY, rangeHalf, multiplier, steps));
			}
			else if (left == "x")
			{
				curves.Add(GenerateExplicitCurveSwapped(right, originX, originY, rangeHalf, multiplier, steps));
			}
			else
			{
				// 隐式方程
				curves.AddRange(SolveImplicitEquation(left, right, originX, originY, rangeHalf, multiplier, steps));
			}
		}

		// 对每条曲线应用范围过滤
		if (!string.IsNullOrEmpty(rangeExpr))
		{
			List<Vector2[]> filtered = new List<Vector2[]>();
			foreach (Vector2[] curve in curves)
			{
				List<Vector2> kept = new List<Vector2>();
				foreach (Vector2 pt in curve)
				{
					float relX = pt.x - originX;
					float relY = (pt.y - originY) / (multiplier > 0f ? multiplier : 1f);
					if (EvaluateRangeCondition(rangeExpr, relX, relY))
						kept.Add(pt);
				}
				if (kept.Count >= 2) filtered.Add(kept.ToArray());
			}
			return filtered;
		}

		return curves;
	}

	// 找到主等号（不在比较运算符中的 =）
	private static int FindMainEquals(string s)
	{
		for (int i = 0; i < s.Length; i++)
		{
			if (s[i] == '=')
			{
				// 排除 >= 和 <= 中的 =
				if ((i > 0 && (s[i - 1] == '>' || s[i - 1] == '<')) ||
				    (i + 1 < s.Length && (s[i + 1] == '>' || s[i + 1] == '<')))
					continue;
				return i;
			}
		}
		return -1;
	}

	// ===== 显式函数 y=f(x) =====

	private static Vector2[] GenerateExplicitCurve(string rhsExpr, float originX, float originY, float rangeHalf, float multiplier, int steps)
	{
		Vector2[] points = new Vector2[steps];
		string expr = InsertImplicitMultiply(rhsExpr);
		for (int i = 0; i < steps; i++)
		{
			float t = (float)i / (float)(steps - 1);
			float worldX = (originX - rangeHalf) + t * (2f * rangeHalf);
			float realX = worldX - originX;
			s_currentX = realX;
			s_currentY = 0f;
			float y = EvalExpression(expr);
			if (float.IsNaN(y) || float.IsInfinity(y)) y = 0f;
			points[i] = new Vector2(worldX, originY + y * multiplier);
		}
		return points;
	}

	// x=f(y) → 交换轴
	private static Vector2[] GenerateExplicitCurveSwapped(string rhsExpr, float originX, float originY, float rangeHalf, float multiplier, int steps)
	{
		Vector2[] points = new Vector2[steps];
		string expr = InsertImplicitMultiply(rhsExpr);
		for (int i = 0; i < steps; i++)
		{
			float t = (float)i / (float)(steps - 1);
			float realY = -rangeHalf + t * (2f * rangeHalf);
			s_currentX = 0f;
			s_currentY = realY;
			float xVal = EvalExpression(expr);
			if (float.IsNaN(xVal) || float.IsInfinity(xVal)) xVal = 0f;
			float worldX = originX + xVal * multiplier;
			float worldY = originY + realY * multiplier;
			points[i] = new Vector2(worldX, worldY);
		}
		return points;
	}

	// ===== 隐式方程 F(x,y)=G(x,y) 数值求解 =====
	// 使用逐行扫描 + 零交叉检测

	private static List<Vector2[]> SolveImplicitEquation(string leftExpr, string rightExpr, float originX, float originY, float rangeHalf, float multiplier, int steps)
	{
		List<Vector2[]> curves = new List<Vector2[]>();
		string left = InsertImplicitMultiply(leftExpr);
		string right = InsertImplicitMultiply(rightExpr);

		int gx = Mathf.Clamp(steps, 50, 200);
		int gy = Mathf.Clamp(steps, 50, 150);
		float xMin = -rangeHalf, xMax = rangeHalf;
		float yMin = -rangeHalf, yMax = rangeHalf;

		// 计算网格 F(x,y) = left - right
		float[,] grid = new float[gx, gy];
		for (int ix = 0; ix < gx; ix++)
		{
			s_currentX = xMin + (xMax - xMin) * ix / (gx - 1);
			for (int iy = 0; iy < gy; iy++)
			{
				s_currentY = yMin + (yMax - yMin) * iy / (gy - 1);
				float lv = EvalExpression(left);
				float rv = EvalExpression(right);
				grid[ix, iy] = lv - rv;
			}
		}

		// 收集零交叉点
		List<Vector2> crossings = new List<Vector2>();
		for (int ix = 0; ix < gx; ix++)
		{
			float worldX = originX + (xMin + (xMax - xMin) * ix / (gx - 1)) * multiplier;
			for (int iy = 0; iy < gy - 1; iy++)
			{
				float v0 = grid[ix, iy], v1 = grid[ix, iy + 1];
				if ((v0 <= 0f && v1 > 0f) || (v0 >= 0f && v1 < 0f))
				{
					float t = Mathf.Abs(v0) / (Mathf.Abs(v0) + Mathf.Abs(v1) + 1e-6f);
					float realY = yMin + (yMax - yMin) * (iy + t) / (gy - 1);
					crossings.Add(new Vector2(worldX, originY + realY * multiplier));
				}
			}
		}
		// 也沿Y方向扫描
		for (int iy = 0; iy < gy; iy++)
		{
			float worldY = originY + (yMin + (yMax - yMin) * iy / (gy - 1)) * multiplier;
			for (int ix = 0; ix < gx - 1; ix++)
			{
				float v0 = grid[ix, iy], v1 = grid[ix + 1, iy];
				if ((v0 <= 0f && v1 > 0f) || (v0 >= 0f && v1 < 0f))
				{
					float t = Mathf.Abs(v0) / (Mathf.Abs(v0) + Mathf.Abs(v1) + 1e-6f);
					float realX = xMin + (xMax - xMin) * (ix + t) / (gx - 1);
					crossings.Add(new Vector2(originX + realX * multiplier, worldY));
				}
			}
		}

		if (crossings.Count < 3) return curves;

		// 去除过近的重复点
		List<Vector2> pts = new List<Vector2> { crossings[0] };
		for (int i = 1; i < crossings.Count; i++)
		{
			if (Vector2.Distance(crossings[i], pts[pts.Count - 1]) > 0.001f)
				pts.Add(crossings[i]);
		}
		if (pts.Count < 3) return curves;

		try
		{
			// 最近邻链追踪：从第一个点开始，每次找最近的未访问点
			bool[] used = new bool[pts.Count];
			List<Vector2> chain = new List<Vector2>();
			int cur = 0;
			used[0] = true;
			chain.Add(pts[0]);

			float closeThreshold = (xMax - xMin) * multiplier / gx * 3f;

			for (int step = 0; step < pts.Count - 1; step++)
			{
				int bestIdx = -1;
				float bestDist = float.MaxValue;
				for (int j = 0; j < pts.Count; j++)
				{
					if (used[j]) continue;
					float d = Vector2.Distance(pts[cur], pts[j]);
					if (d < bestDist) { bestDist = d; bestIdx = j; }
				}
				if (bestIdx < 0 || bestIdx >= pts.Count) break;
				used[bestIdx] = true;
				chain.Add(pts[bestIdx]);
				cur = bestIdx;

				if (chain.Count > 10 && Vector2.Distance(chain[chain.Count - 1], chain[0]) < closeThreshold)
				{
					chain.Add(chain[0]);
					break;
				}
			}

			if (chain.Count >= 3)
				curves.Add(chain.ToArray());
		}
		catch (System.Exception ex)
		{
			Debug.LogWarning("[FuncPlot] Implicit solve error: " + ex.Message);
		}

		return curves;
	}

	// ===== 表达式求值核心 =====

	private static float EvalExpression(string expr)
	{
		if (string.IsNullOrEmpty(expr)) return 0f;
		try
		{
			INMathExpressionEvaluator evaluator = new INMathExpressionEvaluator();
			evaluator.m_expression = expr;
			evaluator.m_pos = 0;
			float result = evaluator.ParseExpression();
			return float.IsNaN(result) || float.IsInfinity(result) ? 0f : result;
		}
		catch { return 0f; }
	}

	// ===== 隐式乘法 =====

	private static string InsertImplicitMultiply(string s)
	{
		if (s.Length < 2) return s;
		System.Text.StringBuilder sb = new System.Text.StringBuilder(s.Length * 2);
		for (int i = 0; i < s.Length; i++)
		{
			char c = s[i];
			if (i > 0)
			{
				char prev = s[i - 1];
				bool needMul = false;
				if ((char.IsDigit(prev) || prev == '.') && (char.IsLetter(c) || c == '(')) needMul = true;
				if ((char.IsLetter(prev) || prev == ')') && c == '(' && !IsFunctionEnd(s, i)) needMul = true;
				if (prev == ')' && (char.IsLetter(c) || char.IsDigit(c))) needMul = true;
				if ((prev == 'x' || prev == 'y') && char.IsDigit(c)) needMul = true;
				if (needMul) sb.Append('*');
			}
			sb.Append(c);
		}
		return sb.ToString();
	}

	private static bool IsFunctionEnd(string s, int parenPos)
	{
		int start = parenPos - 1;
		while (start >= 0 && char.IsLetter(s[start])) start--;
		start++;
		if (start >= parenPos) return false;
		string word = s.Substring(start, parenPos - start);
		return word == "sin" || word == "cos" || word == "tan" || word == "asin" || word == "acos" ||
		       word == "atan" || word == "abs" || word == "sqrt" || word == "floor" || word == "ceil" ||
		       word == "round" || word == "log" || word == "log10" || word == "log2" || word == "exp" || word == "sign";
	}

	// ===== 递归下降解析器 =====

	private float ParseExpression()
	{
		float num = ParseTerm();
		while (m_pos < m_expression.Length)
		{
			char c = m_expression[m_pos];
			if (c == '+') { m_pos++; num += ParseTerm(); }
			else if (c == '-') { m_pos++; num -= ParseTerm(); }
			else if (c == '>' && m_pos + 1 < m_expression.Length && m_expression[m_pos + 1] == '=')
			{ m_pos += 2; num = num >= ParseTerm() ? 1f : 0f; }
			else if (c == '<' && m_pos + 1 < m_expression.Length && m_expression[m_pos + 1] == '=')
			{ m_pos += 2; num = num <= ParseTerm() ? 1f : 0f; }
			else if (c == '>' && (m_pos + 1 >= m_expression.Length || m_expression[m_pos + 1] != '>'))
			{ m_pos++; num = num > ParseTerm() ? 1f : 0f; }
			else if (c == '<' && (m_pos + 1 >= m_expression.Length || m_expression[m_pos + 1] != '<'))
			{ m_pos++; num = num < ParseTerm() ? 1f : 0f; }
			else break;
		}
		return num;
	}

	private float ParseTerm()
	{
		float num = ParseFactor();
		while (m_pos < m_expression.Length)
		{
			char c = m_expression[m_pos];
			if (c == '*') { m_pos++; num *= ParseFactor(); }
			else if (c == '/') { m_pos++; float d = ParseFactor(); num = d != 0f ? num / d : 0f; }
			else if (c == '%') { m_pos++; float d = ParseFactor(); num = d != 0f ? num % d : 0f; }
			else if (c == '^') { m_pos++; num = Mathf.Pow(num, ParseFactor()); }
			else break;
		}
		return num;
	}

	private float ParseFactor()
	{
		if (m_pos >= m_expression.Length) return 0f;
		if (m_expression[m_pos] == '+') { m_pos++; return ParseFactor(); }
		if (m_expression[m_pos] == '-') { m_pos++; return -ParseFactor(); }
		if (m_expression[m_pos] == '(')
		{
			m_pos++;
			float val = ParseExpression();
			if (m_pos < m_expression.Length && m_expression[m_pos] == ')') m_pos++;
			return val;
		}
		if (char.IsDigit(m_expression[m_pos]) || m_expression[m_pos] == '.') return ParseNumber();
		if (m_expression[m_pos] == 'x') { m_pos++; return s_currentX; }
		if (m_expression[m_pos] == 'y') { m_pos++; return s_currentY; }
		return ParseFunctionOrConstant();
	}

	private float ParseNumber()
	{
		int start = m_pos;
		while (m_pos < m_expression.Length && (char.IsDigit(m_expression[m_pos]) || m_expression[m_pos] == '.'))
			m_pos++;
		float.TryParse(m_expression.Substring(start, m_pos - start), NumberStyles.Float, CultureInfo.InvariantCulture, out float result);
		return result;
	}

	private float ParseFunctionOrConstant()
	{
		int start = m_pos;
		while (m_pos < m_expression.Length && char.IsLetter(m_expression[m_pos])) m_pos++;
		string word = m_expression.Substring(start, m_pos - start);
		if (m_pos < m_expression.Length && m_expression[m_pos] == '(')
		{
			m_pos++;
			float arg = ParseExpression();
			if (m_pos < m_expression.Length && m_expression[m_pos] == ')') m_pos++;
			switch (word)
			{
				case "sin": return s_useDegrees ? Mathf.Sin(ToRad(arg)) : Mathf.Sin(arg);
				case "cos": return s_useDegrees ? Mathf.Cos(ToRad(arg)) : Mathf.Cos(arg);
				case "tan": return s_useDegrees ? Mathf.Tan(ToRad(arg)) : Mathf.Tan(arg);
				case "asin": return s_useDegrees ? FromRad(Mathf.Asin(arg)) : Mathf.Asin(arg);
				case "acos": return s_useDegrees ? FromRad(Mathf.Acos(arg)) : Mathf.Acos(arg);
				case "atan": return s_useDegrees ? FromRad(Mathf.Atan(arg)) : Mathf.Atan(arg);
				case "abs": return Mathf.Abs(arg);
				case "sqrt": return Mathf.Sqrt(arg);
				case "floor": return Mathf.Floor(arg);
				case "ceil": return Mathf.Ceil(arg);
				case "round": return Mathf.Round(arg);
				case "log": return Mathf.Log(arg);
				case "log10": return Mathf.Log10(arg);
				case "log2": return Mathf.Log(arg, 2f);
				case "exp": return Mathf.Exp(arg);
				case "sign": return Mathf.Sign(arg);
				default: return 0f;
			}
		}
		switch (word)
		{
			case "pi": return Mathf.PI;
			case "e": return Mathf.Exp(1f);
			default: return 0f;
		}
	}

	// ===== 范围条件求值（处理链式比较如 10>x>=2） =====

	/// <summary>
	/// 判断 x 是否满足范围条件表达式。
	/// 支持链式比较：10>x>=2, -5<=x<=5, x>0, x<10
	/// 空表达式 = 无限制（返回true）
	/// </summary>
	public static bool EvaluateRangeCondition(string expr, float x, float y = 0f)
	{
		if (string.IsNullOrWhiteSpace(expr)) return true;
		string s = expr.Replace(" ", "").ToLower(CultureInfo.InvariantCulture);
		if (string.IsNullOrEmpty(s)) return true;

		// 检测链式比较：找所有比较运算符的位置
		List<int> opPositions = new List<int>();
		List<string> ops = new List<string>();
		for (int i = 0; i < s.Length; i++)
		{
			if (s[i] == '>' && i + 1 < s.Length && s[i + 1] == '=')
			{ ops.Add(">="); opPositions.Add(i); i++; }
			else if (s[i] == '<' && i + 1 < s.Length && s[i + 1] == '=')
			{ ops.Add("<="); opPositions.Add(i); i++; }
			else if (s[i] == '>' && (i + 1 >= s.Length || s[i + 1] != '>'))
			{ ops.Add(">"); opPositions.Add(i); }
			else if (s[i] == '<' && (i + 1 >= s.Length || s[i + 1] != '<'))
			{ ops.Add("<"); opPositions.Add(i); }
		}

		if (ops.Count == 0)
		{
			s_currentX = x;
			s_currentY = y;
			return EvalExpression(s) > 0.5f;
		}

		List<string> parts = new List<string>();
		int lastPos = 0;
		for (int i = 0; i < opPositions.Count; i++)
		{
			parts.Add(s.Substring(lastPos, opPositions[i] - lastPos));
			parts.Add(ops[i]);
			lastPos = opPositions[i] + ops[i].Length;
		}
		parts.Add(s.Substring(lastPos));

		string xStr = "(" + x.ToString(CultureInfo.InvariantCulture) + ")";
		string yStr = "(" + y.ToString(CultureInfo.InvariantCulture) + ")";

		bool allTrue = true;
		for (int i = 0; i < ops.Count; i++)
		{
			string leftStr = parts[i * 2];
			string rightStr = parts[i * 2 + 2];
			string op = parts[i * 2 + 1];

			// 替换x和y为实际值后求值（先替换y再替换x，避免xy子串干扰）
			string leftExpr = leftStr.Replace("y", yStr).Replace("x", xStr);
			string rightExpr = rightStr.Replace("y", yStr).Replace("x", xStr);

			float leftVal = EvalExpression(InsertImplicitMultiply(leftExpr));
			float rightVal = EvalExpression(InsertImplicitMultiply(rightExpr));

			bool result = op switch
			{
				">" => leftVal > rightVal,
				">=" => leftVal >= rightVal,
				"<" => leftVal < rightVal,
				"<=" => leftVal <= rightVal,
				_ => true
			};
			if (!result) { allTrue = false; break; }
		}
		return allTrue;
	}

	// ===== 兼容旧 API =====

	public static void SetCurrentX(float x) { s_currentX = x; }
	public static float Evaluate(string expression, float x)
	{
		s_currentX = x;
		return EvalExpression(InsertImplicitMultiply(expression.ToLower(CultureInfo.InvariantCulture).Replace(" ", "")));
	}
}
