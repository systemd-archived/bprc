public struct UIPartTriggerButtonInfo
{
	public UIPartButtonInfo Value;

	public bool Consistent;

	public bool Multiple;

	public UIPartTriggerButtonInfo(UIPartButtonInfo value, bool consistent = false, bool multiple = true)
	{
		Value = value;
		Consistent = consistent;
		Multiple = multiple;
	}

	public UIPartTriggerButtonInfo(UIPartButtonType buttonType, int buttonIndex, BasePart.PartType partType, int partIndex, int componentIndex, bool consistent = false, bool multiple = true)
	{
		Value = new UIPartButtonInfo(buttonType, buttonIndex, partType, partIndex, componentIndex);
		Consistent = consistent;
		Multiple = multiple;
	}
}
