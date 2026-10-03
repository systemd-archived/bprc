using System.Collections.Generic;
using Innovation;

internal class PartService : IPartService
{
	public IReadOnlyList<IBasePart> GetAllParts()
	{
		return INContraption.GetAllParts();
	}

	public IReadOnlyList<IBasePart> GetAllRuntimeParts()
	{
		return INContraption.GetAllRuntimeParts();
	}

	public IBasePart SelectPart(int x, int y, PartTypeCode partType, int partIndex)
	{
		return INContraption.SelectPart(x, y, (SortedPartType)partType, partIndex);
	}

	public IReadOnlyList<IBasePart> SelectParts(int x, int y, int width, int height, PartTypeCode partType, int partIndex)
	{
		List<BasePart> list = INContraption.SelectParts(x, y, width, height, (SortedPartType)partType, partIndex);
		BP.Feedback("BP> Selected " + list.Count + " parts");
		return list;
	}

	public IReadOnlyList<IBasePart> InvertSelection(IReadOnlyList<IBasePart> parts)
	{
		List<BasePart> list = INContraption.InvertSelection((IReadOnlyList<BasePart>)parts);
		BP.Feedback("BP> Selected " + list.Count + " parts.");
		return list;
	}

	public IBasePart SetPart(int x, int y, PartTypeCode partType, int partIndex)
	{
		return INContraption.SetPart(x, y, (SortedPartType)partType, partIndex);
	}

	public IReadOnlyList<IBasePart> SetParts(int x, int y, int width, int height, PartTypeCode partType, int partIndex)
	{
		List<BasePart> list = INContraption.SetParts(x, y, width, height, (SortedPartType)partType, partIndex);
		BP.Feedback("BP> Placed " + list.Count + " parts");
		return list;
	}

	public IReadOnlyList<IBasePart> SetPartsInterval(int x, int y, int width, int height, int deltaX, int deltaY, PartTypeCode partType, int partIndex)
	{
		List<BasePart> list = INContraption.SetPartsInterval(x, y, width, height, deltaX, deltaY, (SortedPartType)partType, partIndex);
		BP.Feedback("BP> Placed " + list.Count + " parts");
		return list;
	}

	public void MoveParts(IReadOnlyList<IBasePart> parts, int x, int y)
	{
		INContraption.MoveParts((IReadOnlyList<BasePart>)parts, x, y, out var count);
		BP.Feedback("BP> Moved " + count + " parts");
	}

	public void RotateParts(IReadOnlyList<IBasePart> parts, int times)
	{
		INContraption.RotateParts((IReadOnlyList<BasePart>)parts, times, out var count);
		BP.Feedback("BP> Rotated " + count + " parts");
	}

	public IReadOnlyList<IBasePart> CopyParts(IReadOnlyList<IBasePart> parts, int x, int y)
	{
		int count;
		List<BasePart> result = INContraption.CopyParts((IReadOnlyList<BasePart>)parts, x, y, out count);
		BP.Feedback("BP> Copied " + count + " parts");
		return result;
	}

	public IReadOnlyList<IBasePart> ReplaceParts(IReadOnlyList<IBasePart> parts, PartTypeCode partType, int partIndex)
	{
		int count;
		List<BasePart> result = INContraption.ReplaceParts((IReadOnlyList<BasePart>)parts, (SortedPartType)partType, partIndex, out count);
		BP.Feedback("BP> Replaced " + count + " parts");
		return result;
	}

	public void RemoveParts(IReadOnlyList<IBasePart> parts)
	{
		INContraption.RemoveParts((IReadOnlyList<BasePart>)parts, out var count);
		BP.Feedback("BP> Removed " + count + " parts");
	}

	public IBasePart SetRuntimePart(int x, int y, int rotation, bool flipped, PartTypeCode partType, int partIndex)
	{
		return INContraption.SetRuntimePart(x, y, x, y, rotation, flipped, (SortedPartType)partType, partIndex);
	}

	public string GetContraptionName()
	{
		return INContraption.GetContraptionName();
	}

	public void SaveContraption()
	{
		INContraption.SaveContraption();
		BP.Feedback("BP> Saved contraption \"" + INContraption.GetContraptionName() + "\"");
	}

	public void MoveContraption(int x, int y)
	{
		INContraption.MoveContraption(x, y);
		BP.Feedback("BP> Moved contraption \"" + INContraption.GetContraptionName() + "\"");
	}

	public IContraptionData CopyContraption()
	{
		INContraptionData result = INContraption.CopyContraption();
		BP.Feedback("BP> Copied contraption \"" + INContraption.GetContraptionName() + "\"");
		return result;
	}

	public void PasteContraption(IContraptionData data, int x, int y, bool absolute)
	{
		INContraption.PasteContraption(data, x, y, absolute);
		BP.Feedback("BP> Pasted to contraption \"" + INContraption.GetContraptionName() + "\"");
	}
}
