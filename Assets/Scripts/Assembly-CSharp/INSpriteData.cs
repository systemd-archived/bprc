using UnityEngine;

public struct INSpriteData
{
	public Rect Rect;

	public Vector2 Scale;

	public int ScreenHeight;

	public Vector2Int TextureSize;

	public const float CameraSize = 10f;

	public Vector2 PixelSize => new Vector2(Rect.width * Scale.x, Rect.height * Scale.y);

	public Vector2 Size => PixelSize * (10f / (float)ScreenHeight);

	public Rect UVRect
	{
		get
		{
			float x = Rect.x / (float)TextureSize.x;
			float y = Rect.y / (float)TextureSize.y;
			float width = Rect.width / (float)TextureSize.x;
			float height = Rect.height / (float)TextureSize.y;
			return new Rect(x, y, width, height);
		}
	}

	public Rect FlippedUVRect
	{
		get
		{
			float x = Rect.x / (float)TextureSize.x;
			float num = Rect.y / (float)TextureSize.y;
			float width = Rect.width / (float)TextureSize.x;
			float num2 = Rect.height / (float)TextureSize.y;
			return new Rect(x, 1f - num - num2, width, num2);
		}
	}

	public INSpriteData(Rect rect, Vector2 scale, int screenHeight, Vector2Int textureSize)
	{
		Rect = rect;
		Scale = scale;
		ScreenHeight = screenHeight;
		TextureSize = textureSize;
	}
}
