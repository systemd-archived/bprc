using System;

public static class BitDirectionExtensions
{
	public static BitDirection Rotate(this BitDirection direction, int count)
	{
		count = (count % 4 + 4) % 4;
		int num = (int)direction << count;
		int num2 = num & 0xF;
		int num3 = num & 0xF0;
		return (BitDirection)(num2 | (num3 >> 4));
	}

	public static BitDirection Reverse(this BitDirection direction)
	{
		return (BitDirection)(((int)(direction & (BitDirection)3) << 2) | ((int)(direction & (BitDirection)12) >> 2));
	}

	public static int BitCount(this BitDirection direction)
	{
		int num = (int)direction;
		int num2 = 0;
		while (num != 0)
		{
			num2++;
			num &= num - 1;
		}
		return num2;
	}

	public static int ToIndex(this BitDirection direction)
	{
		return direction switch
		{
			BitDirection.Right => 0, 
			BitDirection.Up => 1, 
			BitDirection.Left => 2, 
			BitDirection.Down => 3, 
			_ => throw new InvalidCastException(), 
		};
	}

	public static (int, int) ToVector(this BitDirection direction)
	{
		return direction switch
		{
			BitDirection.Right => (1, 0), 
			BitDirection.Up => (0, 1), 
			BitDirection.Left => (-1, 0), 
			BitDirection.Down => (0, -1), 
			_ => throw new InvalidCastException(), 
		};
	}

	public static BasePart.GridRotation ToGridRotation(this BitDirection direction)
	{
		return direction switch
		{
			BitDirection.Right => BasePart.GridRotation.Deg_0, 
			BitDirection.Up => BasePart.GridRotation.Deg_90, 
			BitDirection.Left => BasePart.GridRotation.Deg_180, 
			BitDirection.Down => BasePart.GridRotation.Deg_270, 
			_ => throw new InvalidCastException(), 
		};
	}

	public static BitDirection ToBitDirection(this BasePart.GridRotation direction)
	{
		return direction switch
		{
			BasePart.GridRotation.Deg_0 => BitDirection.Right, 
			BasePart.GridRotation.Deg_90 => BitDirection.Up, 
			BasePart.GridRotation.Deg_180 => BitDirection.Left, 
			BasePart.GridRotation.Deg_270 => BitDirection.Down, 
			_ => throw new InvalidCastException(), 
		};
	}

	public static BasePart.JointConnectionDirection ToJointConnectionDirection(this BitDirection direction)
	{
		return direction switch
		{
			BitDirection.None => BasePart.JointConnectionDirection.None, 
			BitDirection.Right => BasePart.JointConnectionDirection.Right, 
			BitDirection.Up => BasePart.JointConnectionDirection.Up, 
			BitDirection.Left => BasePart.JointConnectionDirection.Left, 
			BitDirection.Down => BasePart.JointConnectionDirection.Down, 
			BitDirection.LeftAndRight => BasePart.JointConnectionDirection.LeftAndRight, 
			BitDirection.UpAndDown => BasePart.JointConnectionDirection.UpAndDown, 
			BitDirection.Any => BasePart.JointConnectionDirection.Any, 
			_ => throw new InvalidCastException(), 
		};
	}

	public static BitDirection ToBitDirection(this BasePart.JointConnectionDirection direction)
	{
		return direction switch
		{
			BasePart.JointConnectionDirection.None => BitDirection.None, 
			BasePart.JointConnectionDirection.Right => BitDirection.Right, 
			BasePart.JointConnectionDirection.Up => BitDirection.Up, 
			BasePart.JointConnectionDirection.Left => BitDirection.Left, 
			BasePart.JointConnectionDirection.Down => BitDirection.Down, 
			BasePart.JointConnectionDirection.LeftAndRight => BitDirection.LeftAndRight, 
			BasePart.JointConnectionDirection.UpAndDown => BitDirection.UpAndDown, 
			BasePart.JointConnectionDirection.Any => BitDirection.Any, 
			_ => throw new InvalidCastException(), 
		};
	}
}
