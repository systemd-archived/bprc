namespace BadPiggies.Multiplayer
{
public static class MultiplayerBlueprintMirror
{
	public static ContraptionDataset Mirror(ContraptionDataset source, int gridXMin, int gridXMax)
	{
		ContraptionDataset contraptionDataset = new ContraptionDataset();
		if (source == null)
		{
			return contraptionDataset;
		}
		int num = gridXMin + gridXMax;
		foreach (ContraptionDataset.ContraptionDatasetUnit contraptionDataset2 in source.ContraptionDatasetList)
		{
			BasePart.GridRotation rotation = (BasePart.GridRotation)contraptionDataset2.rot;
			bool flipped = contraptionDataset2.flipped;
			MirrorOrientation(contraptionDataset2.partType, contraptionDataset2.customPartIndex, ref rotation, ref flipped);
			contraptionDataset.AddPart(num - contraptionDataset2.x, contraptionDataset2.y, contraptionDataset2.partType, contraptionDataset2.customPartIndex, rotation, flipped);
		}
		return contraptionDataset;
	}

	private static void MirrorOrientation(int partType, int customPartIndex, ref BasePart.GridRotation rotation, ref bool flipped)
	{
		int axisAngle;
		string source;
		if (MultiplayerPartAxis.UsesPackedFlip(partType, customPartIndex))
		{
			rotation = MultiplayerPartAxis.AngleToRotation(360 - MultiplayerPartAxis.RotationToAngle(rotation));
			flipped = !flipped;
		}
		else if (MultiplayerPartAxis.TryResolveAxis(partType, customPartIndex, out axisAngle, out source))
		{
			int rotationAngle = MultiplayerPartAxis.RotationToAngle(rotation);
			rotation = MultiplayerPartAxis.AngleToRotation(MultiplayerPartAxis.MirrorAngle(axisAngle, rotationAngle));
		}
	}
}
}
