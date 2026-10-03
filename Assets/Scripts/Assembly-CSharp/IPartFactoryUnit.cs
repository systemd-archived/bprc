using System.Collections.Generic;

public interface IPartFactoryUnit
{
	BasePart RegularPart { get; }

	IReadOnlyList<BasePart> CustomParts { get; }
}
