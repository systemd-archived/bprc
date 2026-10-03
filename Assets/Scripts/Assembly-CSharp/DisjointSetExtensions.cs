using System;

public static class DisjointSetExtensions
{
	public static int[] ToArray(this DisjointSet disjointSet)
	{
		int count = disjointSet.Count;
		int[] array = new int[count];
		for (int i = 0; i < count; i++)
		{
			array[i] = disjointSet.FindSet(i);
		}
		return array;
	}

	public static int[] GetComponentIndexes(this DisjointSet disjointSet, out int componentCount)
	{
		int[] array = new int[disjointSet.Count];
		disjointSet.GetComponentIndexes(array, out componentCount);
		return array;
	}

	public static void GetComponentIndexes(this DisjointSet disjointSet, int[] componentIndexes, out int componentCount)
	{
		int count = disjointSet.Count;
		if (componentIndexes.Length < count)
		{
			throw new ArgumentException("componentIndexes");
		}
		componentCount = 0;
		for (int i = 0; i < count; i++)
		{
			componentIndexes[i] = -1;
		}
		for (int j = 0; j < count; j++)
		{
			int num = disjointSet.FindSet(j);
			int num2 = componentIndexes[num];
			if (num2 == -1)
			{
				num2 = (componentIndexes[num] = componentCount++);
			}
			componentIndexes[j] = num2;
		}
	}

	public static int[] GetComponents(this DisjointSet disjointSet, out int[] size, out int componentCount)
	{
		int count = disjointSet.Count;
		int[] componentIndexes = disjointSet.GetComponentIndexes(out componentCount);
		int[] array = new int[componentCount];
		int[] array2 = new int[count];
		size = new int[componentCount];
		for (int i = 0; i < count; i++)
		{
			int num = componentIndexes[i];
			if (size[num] == 0)
			{
				disjointSet.FindSet(i, out size[num]);
			}
		}
		for (int j = 1; j < componentCount; j++)
		{
			array[j] = array[j - 1] + size[j - 1];
		}
		for (int k = 0; k < count; k++)
		{
			int num2 = componentIndexes[k];
			array2[array[num2]] = k;
			array[num2]++;
		}
		return array2;
	}
}
