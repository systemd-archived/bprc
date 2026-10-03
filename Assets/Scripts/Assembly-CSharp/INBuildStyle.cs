using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared steel, brass and ivory treatment for construction tools.</summary>
public static class INBuildStyle
{
    public static readonly Color Panel = new Color(0.16f, 0.17f, 0.17f, 0.98f);
    public static readonly Color Button = new Color(0.27f, 0.28f, 0.27f, 1f);
    public static readonly Color Brass = new Color(0.71f, 0.59f, 0.37f, 1f);
    public static readonly Color Ivory = new Color(0.93f, 0.89f, 0.79f, 1f);
    public static readonly Color Muted = new Color(0.65f, 0.65f, 0.59f, 1f);
    public static void Bevel(GameObject go)
    {
        Outline edge = go.AddComponent<Outline>();
        edge.effectColor = new Color(0.07f, 0.07f, 0.06f, 0.85f);
        edge.effectDistance = new Vector2(1f, -1f);
        edge.useGraphicAlpha = true;
    }
    public static void ButtonColors(UnityEngine.UI.Button button)
    {
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1.17f, 1.13f, 1.03f);
        colors.pressedColor = new Color(0.82f, 0.75f, 0.6f);
        colors.selectedColor = Color.white;
        button.colors = colors;
    }
}

/// <summary>Code-native tool symbols, independent of the reference game's assets.</summary>
[ExecuteAlways]
[RequireComponent(typeof(CanvasRenderer))]
public class INBuildToolIcon : MaskableGraphic
{
    public int symbol;
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (symbol == 1 || symbol == 5 || symbol == 10) // rotation arc
        {
            for (int i = 0; i < 22; i++)
            {
                float a = (i * 12f + 30f) * Mathf.Deg2Rad, b = (i * 12f + 42f) * Mathf.Deg2Rad;
                Stroke(vh, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 10f, new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * 10f);
            }
            Stroke(vh, new Vector2(8, -6), new Vector2(13, -7));
            Stroke(vh, new Vector2(8, -6), new Vector2(9, -12));
        }
        else if (symbol == 4 || symbol == 9)
        {
            Stroke(vh, new Vector2(-12, 0), new Vector2(12, 0)); Stroke(vh, new Vector2(0, -12), new Vector2(0, 12));
            Stroke(vh, new Vector2(12, 0), new Vector2(7, 5)); Stroke(vh, new Vector2(12, 0), new Vector2(7, -5));
            Stroke(vh, new Vector2(0, 12), new Vector2(5, 7)); Stroke(vh, new Vector2(0, 12), new Vector2(-5, 7));
        }
        else if (symbol == 2)
        {
            Stroke(vh, new Vector2(0, -13), new Vector2(0, 13));
            Stroke(vh, new Vector2(-4, -8), new Vector2(-13, -8)); Stroke(vh, new Vector2(-13, -8), new Vector2(-4, 9)); Stroke(vh, new Vector2(-4, 9), new Vector2(-4, -8));
            Stroke(vh, new Vector2(4, -8), new Vector2(13, -8)); Stroke(vh, new Vector2(13, -8), new Vector2(4, 9)); Stroke(vh, new Vector2(4, 9), new Vector2(4, -8));
        }
        else if (symbol == 3)
        {
            for (int i = 0; i < 3; i++)
            {
                float y = i * 9 - 9; float x = i == 1 ? 5 : -5;
                Stroke(vh, new Vector2(-13, y), new Vector2(13, y)); Stroke(vh, new Vector2(x, y - 3), new Vector2(x, y + 3), 5f);
            }
        }
        else if (symbol == 8)
        {
            Stroke(vh, new Vector2(-12, 6), new Vector2(4, 6)); Stroke(vh, new Vector2(4, 6), new Vector2(10, 0));
            Stroke(vh, new Vector2(10, 0), new Vector2(10, -7)); Stroke(vh, new Vector2(10, -7), new Vector2(3, -11));
            Stroke(vh, new Vector2(-12, 6), new Vector2(-5, 12)); Stroke(vh, new Vector2(-12, 6), new Vector2(-5, 0));
        }
        else if (symbol == 11)
        {
            for (int i = 0; i < 32; i++)
            {
                float a = i * Mathf.PI / 16f, b = (i + 1) * Mathf.PI / 16f;
                for (int side = 0; side < 2; side++)
                {
                    float x = side == 0 ? -5f : 5f;
                    Stroke(vh, new Vector2(x + Mathf.Cos(a) * 8f, Mathf.Sin(a) * 8f),
                        new Vector2(x + Mathf.Cos(b) * 8f, Mathf.Sin(b) * 8f));
                }
            }
        }
        else if (symbol == 12)
        {
            Stroke(vh, new Vector2(-13, -12), new Vector2(1, -12));
            Stroke(vh, new Vector2(1, -12), new Vector2(1, 2));
            Stroke(vh, new Vector2(1, 2), new Vector2(-13, 2));
            Stroke(vh, new Vector2(-13, 2), new Vector2(-13, -12));
            Stroke(vh, new Vector2(4, 6), new Vector2(12, 14));
            Stroke(vh, new Vector2(12, 14), new Vector2(5, 14));
            Stroke(vh, new Vector2(12, 14), new Vector2(12, 7));
        }
        else if (symbol == 6)
        {
            for (int i = 0; i < 4; i++)
            {
                Vector2 corner = new Vector2(i % 2 == 0 ? -11 : 11, i < 2 ? -11 : 11);
                Stroke(vh, corner, corner - new Vector2(Mathf.Sign(corner.x) * 6, 0));
                Stroke(vh, corner, corner - new Vector2(0, Mathf.Sign(corner.y) * 6));
            }
        }
        else
        {
            Stroke(vh, new Vector2(-10, -10), new Vector2(10, -10)); Stroke(vh, new Vector2(10, -10), new Vector2(10, 10));
            Stroke(vh, new Vector2(10, 10), new Vector2(-10, 10)); Stroke(vh, new Vector2(-10, 10), new Vector2(-10, -10));
            if (symbol == 7) { Stroke(vh, new Vector2(0, -10), new Vector2(0, 10)); Stroke(vh, new Vector2(-10, 0), new Vector2(10, 0)); }
        }
        if (symbol == 9 || symbol == 10)
        {
            Stroke(vh, new Vector2(-3, -3), new Vector2(3, -3)); Stroke(vh, new Vector2(3, -3), new Vector2(3, 3));
            Stroke(vh, new Vector2(3, 3), new Vector2(-3, 3)); Stroke(vh, new Vector2(-3, 3), new Vector2(-3, -3));
        }
    }
    private void Stroke(VertexHelper vh, Vector2 a, Vector2 b, float width = 2f)
    {
        Vector2 n = new Vector2(-(b - a).y, (b - a).x).normalized * width * 0.5f;
        int i = vh.currentVertCount;
        vh.AddVert(a - n, color, Vector2.zero); vh.AddVert(a + n, color, Vector2.zero);
        vh.AddVert(b + n, color, Vector2.zero); vh.AddVert(b - n, color, Vector2.zero);
        vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
    }
}
