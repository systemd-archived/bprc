public struct UIPartSliderButtonInfo
{
	public UIPartButtonInfo Value;

	public UIPartSliderButton.Range Range;

	public UIPartSliderButtonInfo(UIPartButtonInfo value, UIPartSliderButton.Range range)
	{
		Value = value;
		Range = range;
	}

	public UIPartSliderButtonInfo(UIPartButtonType buttonType, int buttonIndex, BasePart.PartType partType, int partIndex, int componentIndex, UIPartSliderButton.Range range)
	{
		Value = new UIPartButtonInfo(buttonType, buttonIndex, partType, partIndex, componentIndex);
		Range = range;
	}
}
