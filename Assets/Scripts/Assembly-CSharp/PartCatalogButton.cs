using System;
using UnityEngine;

// Uses the game's Widget raycasting so tabs also block build-grid clicks and work with touch.
public sealed class PartCatalogButton : Widget
{
    private static UnityEngine.Sprite s_white;
    private static Font s_font;
    private SpriteRenderer background;
    private SpriteRenderer underline;
    private TextMesh label;
    private Action clicked;
    private Vector2 size;
    private Vector3 pressPosition;
    private bool pressed;

    public static PartCatalogButton Create(Transform parent, string text, Vector2 size, Action clicked)
    {
        if (!s_white) s_white = UnityEngine.Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), Vector2.one * 0.5f, Texture2D.whiteTexture.width);
        if (!s_font)
        {
            var style = Resources.Load<PartCatalogStyle>("PartCatalogStyle");
            s_font = style && style.font ? style.font : Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Noto Sans CJK SC", "Arial" }, 32);
        }
        var go = new GameObject(text); go.transform.SetParent(parent, false);
        var button = go.AddComponent<PartCatalogButton>();
        button.size = size; button.clicked = clicked;
        go.AddComponent<BoxCollider>().size = new Vector3(size.x, size.y, 0.1f);
        button.background = button.AddPanel("Background", size, Vector3.zero);
        button.underline = button.AddPanel("Selection", new Vector2(size.x, 0.06f), new Vector3(0, -size.y * 0.5f + 0.03f, -0.02f));
        button.underline.color = new Color(0.3f, 0.85f, 1f);
        var labelObject = new GameObject("Label"); labelObject.transform.SetParent(go.transform, false);
        labelObject.transform.localPosition = new Vector3(0, 0, -0.05f);
        button.label = labelObject.AddComponent<TextMesh>();
        button.label.font = s_font; button.label.fontSize = 32; button.label.characterSize = 0.15f;
        button.label.anchor = TextAnchor.MiddleCenter; button.label.alignment = TextAlignment.Center;
        button.label.text = text; button.label.color = Color.white;
        var renderer = labelObject.GetComponent<MeshRenderer>(); renderer.sharedMaterial = s_font.material; renderer.sortingOrder = 5;
        button.SetSelected(false);
        return button;
    }

    private SpriteRenderer AddPanel(string name, Vector2 dimensions, Vector3 position)
    {
        var go = new GameObject(name); go.transform.SetParent(transform, false);
        go.transform.localPosition = position; go.transform.localScale = new Vector3(dimensions.x, dimensions.y, 1);
        var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = s_white; renderer.sortingOrder = 3;
        return renderer;
    }

    public void SetSelected(bool selected)
    {
        background.color = selected ? new Color(0.22f, 0.39f, 0.59f, 0.98f) : new Color(0.14f, 0.18f, 0.27f, 0.96f);
        underline.enabled = selected;
    }

    public void SetLabelAtBottom()
    {
        label.transform.localPosition = new Vector3(0, -size.y * 0.35f, -0.5f);
        label.characterSize = 0.09f;
        background.sortingOrder = 0;
    }

    protected override void OnInput(InputEvent input)
    {
        if (input.type == InputEvent.EventType.Press)
        {
            pressPosition = input.position; pressed = true;
        }
        else if (input.type == InputEvent.EventType.Release)
        {
            bool activate = pressed && Vector3.Distance(pressPosition, input.position) < Mathf.Max(12, Screen.height * 0.025f);
            pressed = false;
            if (activate) clicked?.Invoke();
        }
        else if (input.type == InputEvent.EventType.MouseLeave) pressed = false;
    }
}
