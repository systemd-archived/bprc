using System.Collections.Generic;

public interface IPartFactory
{
	IEnumerable<IPartFactoryUnit> Units { get; }

	IPartFactoryUnit FindUnit(BasePart.PartType partType);

	BasePart FindPart(BasePart.PartType partType);

	BasePart FindCustomPart(BasePart.PartType partType, int partIndex);
}
