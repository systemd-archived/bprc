using System.Collections.Generic;
using UnityEngine;

public class ColoredFrameManager : PartManager
{
	public override StatusCode Status => (StatusCode)3;

	protected override void Initialize()
	{
		base.Initialize();
	}

	public override void FixedUpdate()
	{
		Contraption instance = Contraption.Instance;
		List<ColoredFrame> list = new List<ColoredFrame>();
		foreach (BasePart part in instance.Parts)
		{
			if (part.IsTransparentFrame())
			{
				list.Add(part as ColoredFrame);
			}
		}
		if (!INSettings.GetBool(INFeature.CanColorTransparentFrame) || list.Count <= 0)
		{
			return;
		}
		float t = INSettings.GetFloat(INFeature.TransparentFrameColorDecayRate);
		float num = INSettings.GetFloat(INFeature.TransparentFrameAlphaDecayRate);
		for (int i = 0; i < 2; i++)
		{
			foreach (ColoredFrame item in list)
			{
				Color a = item.Color * item.Color.a;
				float num2 = item.Color.a;
				foreach (BasePart item2 in instance.FindNeighboursYield(item.CoordX, item.CoordY, item))
				{
					if (item2 is ColoredFrame coloredFrame)
					{
						float a2 = coloredFrame.Color.a;
						a += coloredFrame.Color * a2;
						num2 += a2;
					}
				}
				a /= num2;
				a = Color.Lerp(a, item.TransparentColor, t);
				a.a = a.a * (1f - num) + item.TransparentColor.a * num;
				item.Color = a;
			}
		}
		foreach (ColoredFrame item3 in list)
		{
			item3.UpdateRenderers();
		}
	}
}
