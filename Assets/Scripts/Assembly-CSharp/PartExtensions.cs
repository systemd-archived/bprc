using System;

public static class PartExtensions
{
	public static int ToAngle(this BasePart.GridRotation rotation)
	{
		return rotation switch
		{
			BasePart.GridRotation.Deg_0 => 0, 
			BasePart.GridRotation.Deg_45 => 45, 
			BasePart.GridRotation.Deg_90 => 90, 
			BasePart.GridRotation.Deg_135 => 135, 
			BasePart.GridRotation.Deg_180 => 180, 
			BasePart.GridRotation.Deg_225 => 225, 
			BasePart.GridRotation.Deg_270 => 270, 
			BasePart.GridRotation.Deg_315 => 315, 
			BasePart.GridRotation.Deg_Max => 360, 
			_ => throw new InvalidCastException(), 
		};
	}

	public static (int, int) ToDirection(this BasePart.GridRotation rotation)
	{
		return rotation switch
		{
			BasePart.GridRotation.Deg_0 => (1, 0), 
			BasePart.GridRotation.Deg_45 => (1, 1), 
			BasePart.GridRotation.Deg_90 => (0, 1), 
			BasePart.GridRotation.Deg_135 => (-1, 1), 
			BasePart.GridRotation.Deg_180 => (-1, 0), 
			BasePart.GridRotation.Deg_225 => (-1, -1), 
			BasePart.GridRotation.Deg_270 => (0, -1), 
			BasePart.GridRotation.Deg_315 => (1, -1), 
			BasePart.GridRotation.Deg_Max => (1, 0), 
			_ => throw new InvalidCastException(), 
		};
	}

	/// <summary>
	/// Whether the part is on its own inside the contraption it belongs to.
	/// </summary>
	/// <remarks>
	/// A part that is not in a contraption at all - the part list builds its templates that way - has no
	/// connected component to count: nothing is known about its neighbours yet, so it is reported as not
	/// being a single part rather than throwing. The alternative, dereferencing the missing contraption,
	/// takes the whole part factory down with it.
	/// </remarks>
	public static bool IsSinglePart(this BasePart part)
	{
		if (part == null || part.contraption == null)
		{
			return false;
		}
		return part.contraption.ComponentPartCount(part.ConnectedComponent) == 1;
	}

	public static bool HasMultipleRigidbodies(this BasePart part)
	{
		return part.m_partType == BasePart.PartType.Rope;
	}

	public static bool IsSeparatedFrame(this BasePart part)
	{
		return part.BelongsTo(BasePart.SeparatedFrame);
	}

	public static bool IsLightFrame(this BasePart part)
	{
		return part.BelongsTo(BasePart.LightFrame);
	}

	public static bool IsAlienMetalFrame(this BasePart part)
	{
		return part.BelongsTo(BasePart.AlienMetalFrame);
	}

	public static bool IsColoredrame(this BasePart part)
	{
		return part.BelongsTo(BasePart.ColoredFrames, BasePart.TransparentFrames);
	}

	public static bool IsTransparentFrame(this BasePart part)
	{
		return part.BelongsTo(BasePart.TransparentFrames);
	}

	public static bool IsBracketFrame(this BasePart part)
	{
		return part.BelongsTo(BasePart.BracketFrame);
	}

	public static bool IsWoodenBox(this BasePart part)
	{
		return part.BelongsTo(BasePart.WoodenBox);
	}

	public static bool IsMetalBox(this BasePart part)
	{
		return part.BelongsTo(BasePart.MetalBox);
	}

	public static bool IsAvoidanceRocket(this BasePart part)
	{
		return part.BelongsTo(BasePart.AvoidanceRocketA, BasePart.AvoidanceRocketB);
	}

	public static bool IsTrackingRocket(this BasePart part)
	{
		return part.BelongsTo(BasePart.TrackingRocketA, BasePart.TrackingRocketB);
	}

	public static bool IsHingePlate(this BasePart part)
	{
		return part.BelongsTo(BasePart.HingePlates);
	}

	public static bool IsMultipartGenerator(this BasePart part)
	{
		return part.BelongsTo(BasePart.MultipartGenerators);
	}

	public static bool IsAutoGun(this BasePart part)
	{
		return part.BelongsTo(BasePart.AutoGun);
	}

	public static bool IsElasticConnector(this BasePart part)
	{
		return part.BelongsTo(BasePart.ElasticConnectorA, BasePart.ElasticConnectorB);
	}

	public static bool IsAutoConnector(this BasePart part)
	{
		return part.BelongsTo(BasePart.AutoConnector);
	}

	public static bool IsMarker(this BasePart part)
	{
		return part.BelongsTo(BasePart.Marker);
	}

	public static bool IsEntityLight(this BasePart part)
	{
		return part.BelongsTo(BasePart.EntityLightsA, BasePart.EntityLightsB);
	}

	public static bool IsDecelerationLight(this BasePart part)
	{
		return part.BelongsTo(BasePart.DecelerationLight);
	}

	public static bool IsAutoControlLight(this BasePart part)
	{
		return part.BelongsTo(BasePart.AutoControlLight);
	}

	// 军事版 24.7.12 有、主工程原先没有的分类谓词。主工程过去只在 TNT.cs 里直接比较
	// customPartIndex == 3 / 4，这里把同一批分类补成命名接口，供军事版功能使用。
	public static bool IsHeavyBomb(this BasePart part)
	{
		return part.BelongsTo(BasePart.HeavyBomb);
	}

	public static bool IsNuclearBomb(this BasePart part)
	{
		return part.BelongsTo(BasePart.NuclearBomb);
	}

	public static bool IsAcidBomb(this BasePart part)
	{
		return part.BelongsTo(BasePart.AcidBomb);
	}

	public static bool IsAlienBomb(this BasePart part)
	{
		return part.BelongsTo(BasePart.AlienBomb);
	}

	public static bool IsAPRocket(this BasePart part)
	{
		return part.BelongsTo(BasePart.APRocket);
	}

	/// <summary>军事版把 MetalFrame 12..129 称作 Neo 框；主工程的 ColoredFrames 正是同一区间。</summary>
	public static bool IsNeoFrame(this BasePart part)
	{
		return part.BelongsTo(BasePart.ColoredFrames);
	}
}
