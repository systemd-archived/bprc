using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace BadPiggies.Multiplayer
{
public static class MultiplayerPartAxis
{
	private static readonly Dictionary<Type, bool> s_overridesEffect = new Dictionary<Type, bool>();

	public static bool UsesPackedFlip(int partType, int customPartIndex)
	{
		BasePart basePart = ResolvePrefab(partType, customPartIndex);
		if (basePart == null)
		{
			return false;
		}
		if (basePart.m_autoAlign != BasePart.AutoAlignType.FlipVertically)
		{
			return basePart.IsCustomRotated();
		}
		return true;
	}

	public static bool TryResolveAxis(int partType, int customPartIndex, out int axisAngle, out string source)
	{
		axisAngle = 0;
		source = "none";
		BasePart basePart = ResolvePrefab(partType, customPartIndex);
		if (basePart == null)
		{
			return false;
		}
		if (TryResolveSocketAxis(basePart, out axisAngle))
		{
			source = $"socket:{LocalSocket(basePart)}";
			return true;
		}
		if (TryResolveEffectAxis(basePart, out axisAngle))
		{
			source = "effect";
			return true;
		}
		return false;
	}

	public static bool TryResolveSocketAxis(BasePart prefab, out int axisAngle)
	{
		axisAngle = 0;
		if (prefab == null)
		{
			return false;
		}
		return TryDirectionAngle(LocalSocket(prefab), out axisAngle);
	}

	public static bool TryResolveEffectAxis(BasePart prefab, out int axisAngle)
	{
		axisAngle = 0;
		if (prefab == null || !OverridesEffectDirection(prefab))
		{
			return false;
		}
		return TryEffectAngle(prefab, out axisAngle);
	}

	private static BasePart.JointConnectionDirection LocalSocket(BasePart prefab)
	{
		if (prefab.m_jointConnectionDirection == BasePart.JointConnectionDirection.None)
		{
			return prefab.m_customJointConnectionDirection;
		}
		return prefab.m_jointConnectionDirection;
	}

	private static bool TryDirectionAngle(BasePart.JointConnectionDirection socket, out int angle)
	{
		switch (socket)
		{
		case BasePart.JointConnectionDirection.Right:
			angle = 0;
			return true;
		case BasePart.JointConnectionDirection.Up:
			angle = 90;
			return true;
		case BasePart.JointConnectionDirection.Left:
			angle = 180;
			return true;
		case BasePart.JointConnectionDirection.Down:
			angle = 270;
			return true;
		case BasePart.JointConnectionDirection.LeftAndRight:
			angle = 0;
			return true;
		case BasePart.JointConnectionDirection.UpAndDown:
			angle = 90;
			return true;
		default:
			angle = 0;
			return false;
		}
	}

	private static bool TryEffectAngle(BasePart prefab, out int angle)
	{
		angle = 0;
		try
		{
			return TryEffectDirectionAngle(prefab.EffectDirection(), out angle);
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"[Multiplayer] {prefab.m_partType}.EffectDirection() threw while classifying the " + "part for mirroring (" + ex.GetType().Name + "); its rotation will be left unmirrored.");
			return false;
		}
	}

	private static bool TryEffectDirectionAngle(BasePart.Direction direction, out int angle)
	{
		switch (direction)
		{
		case BasePart.Direction.Right:
			angle = 0;
			return true;
		case BasePart.Direction.Up:
			angle = 90;
			return true;
		case BasePart.Direction.Left:
			angle = 180;
			return true;
		case BasePart.Direction.Down:
			angle = 270;
			return true;
		case BasePart.Direction.UpRight:
			angle = 45;
			return true;
		case BasePart.Direction.UpLeft:
			angle = 135;
			return true;
		case BasePart.Direction.DownLeft:
			angle = 225;
			return true;
		case BasePart.Direction.DownRight:
			angle = 315;
			return true;
		default:
			angle = 0;
			return false;
		}
	}

	private static bool OverridesEffectDirection(BasePart prefab)
	{
		Type type = prefab.GetType();
		if (s_overridesEffect.TryGetValue(type, out var value))
		{
			return value;
		}
		bool flag = false;
		MethodInfo method = type.GetMethod("EffectDirection", BindingFlags.Instance | BindingFlags.Public);
		if (method != null)
		{
			flag = method.DeclaringType != typeof(BasePart);
		}
		s_overridesEffect[type] = flag;
		return flag;
	}

	public static BasePart ResolvePrefab(int partType, int customPartIndex)
	{
		GameData gameData = WPFMonoBehaviour.gameData;
		if (gameData == null)
		{
			return null;
		}
		return gameData.GetCustomPart((BasePart.PartType)partType, customPartIndex);
	}

	public static int MirrorAngle(int axisAngle, int rotationAngle)
	{
		return Mod360(180 - 2 * axisAngle - rotationAngle);
	}

	public static int Mod360(int angle)
	{
		angle %= 360;
		if (angle >= 0)
		{
			return angle;
		}
		return angle + 360;
	}

	public static int RotationToAngle(BasePart.GridRotation rotation)
	{
		return rotation switch
		{
			BasePart.GridRotation.Deg_0 => 0, 
			BasePart.GridRotation.Deg_90 => 90, 
			BasePart.GridRotation.Deg_180 => 180, 
			BasePart.GridRotation.Deg_270 => 270, 
			BasePart.GridRotation.Deg_45 => 45, 
			BasePart.GridRotation.Deg_135 => 135, 
			BasePart.GridRotation.Deg_225 => 225, 
			BasePart.GridRotation.Deg_315 => 315, 
			_ => 0, 
		};
	}

	public static BasePart.GridRotation AngleToRotation(int angle)
	{
		return Mod360(angle) switch
		{
			0 => BasePart.GridRotation.Deg_0, 
			90 => BasePart.GridRotation.Deg_90, 
			180 => BasePart.GridRotation.Deg_180, 
			270 => BasePart.GridRotation.Deg_270, 
			45 => BasePart.GridRotation.Deg_45, 
			135 => BasePart.GridRotation.Deg_135, 
			225 => BasePart.GridRotation.Deg_225, 
			315 => BasePart.GridRotation.Deg_315, 
			_ => BasePart.GridRotation.Deg_0, 
		};
	}

	public static BasePart.JointConnectionDirection WorldSocket(BasePart prefab, BasePart.GridRotation rotation, bool flipped)
	{
		BasePart.JointConnectionDirection jointConnectionDirection = LocalSocket(prefab);
		if (jointConnectionDirection == BasePart.JointConnectionDirection.Any || jointConnectionDirection == BasePart.JointConnectionDirection.None)
		{
			return jointConnectionDirection;
		}
		bool flag = rotation == BasePart.GridRotation.Deg_90 || rotation == BasePart.GridRotation.Deg_270;
		BasePart.JointConnectionDirection jointConnectionDirection2 = jointConnectionDirection switch
		{
			BasePart.JointConnectionDirection.LeftAndRight => flag ? BasePart.JointConnectionDirection.UpAndDown : BasePart.JointConnectionDirection.LeftAndRight, 
			BasePart.JointConnectionDirection.UpAndDown => flag ? BasePart.JointConnectionDirection.LeftAndRight : BasePart.JointConnectionDirection.UpAndDown, 
			_ => (BasePart.JointConnectionDirection)(((int)(jointConnectionDirection - 1) + (int)rotation) % 4 + 1), 
		};
		if (flipped)
		{
			switch (jointConnectionDirection2)
			{
			case BasePart.JointConnectionDirection.Left:
				return BasePart.JointConnectionDirection.Right;
			case BasePart.JointConnectionDirection.Right:
				return BasePart.JointConnectionDirection.Left;
			case BasePart.JointConnectionDirection.Up:
				return BasePart.JointConnectionDirection.Down;
			case BasePart.JointConnectionDirection.Down:
				return BasePart.JointConnectionDirection.Up;
			}
		}
		return jointConnectionDirection2;
	}

	public static BasePart.JointConnectionDirection ReflectSocket(BasePart.JointConnectionDirection direction)
	{
		return direction switch
		{
			BasePart.JointConnectionDirection.Left => BasePart.JointConnectionDirection.Right, 
			BasePart.JointConnectionDirection.Right => BasePart.JointConnectionDirection.Left, 
			_ => direction, 
		};
	}
}
}
