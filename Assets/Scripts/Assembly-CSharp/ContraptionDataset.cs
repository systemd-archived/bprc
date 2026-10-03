using System.Collections.Generic;
using System.Xml.Serialization;
using Innovation;

[XmlRoot("ContraptionDataset")]
public class ContraptionDataset : IContraptionData
{
	public class ContraptionDatasetUnit : IContraptionDataUnit
	{
		[XmlAttribute("x")]
		public int x;

		[XmlAttribute("y")]
		public int y;

		[XmlAttribute("partType")]
		public int partType;

		[XmlAttribute("customPartIndex")]
		public int customPartIndex;

		[XmlAttribute("rot")]
		public int rot;

		[XmlAttribute("flipped")]
		public bool flipped;

		// 每部件自定义参数，编码 "key:value;key:value"（空则省略）。仅本实现使用，旧存档无此属性时为 null。
		[XmlAttribute("customParams")]
		public string customParams;

		int IContraptionDataUnit.Type => (int)((BasePart.PartType)partType).ToSortedPartType();

		int IContraptionDataUnit.Index => customPartIndex;

		int IContraptionDataUnit.X => x;

		int IContraptionDataUnit.Y => y;

		int IContraptionDataUnit.Rotation => rot;

		bool IContraptionDataUnit.Flipped => flipped;

		public ContraptionDatasetUnit()
		{
		}

		public ContraptionDatasetUnit(int x, int y, int partType, int customPartIndex, int rot, bool flipped)
		{
			this.x = x;
			this.y = y;
			this.partType = partType;
			this.customPartIndex = customPartIndex;
			this.rot = rot;
			this.flipped = flipped;
		}
	}

	[XmlArray("ContraptionDatasetList")]
	[XmlArrayItem("ContraptionDatasetUnit")]
	protected List<ContraptionDatasetUnit> m_contraptionDataSet = new List<ContraptionDatasetUnit>();

	public List<ContraptionDatasetUnit> ContraptionDatasetList => m_contraptionDataSet;

	IReadOnlyList<IContraptionDataUnit> IContraptionData.Units => m_contraptionDataSet;

	public void AddPart(int x, int y, int partType, int customPartIndex, BasePart.GridRotation rotation, bool flipped)
	{
		AddPart(x, y, partType, customPartIndex, rotation, flipped, null);
	}

	public void AddPart(int x, int y, int partType, int customPartIndex, BasePart.GridRotation rotation, bool flipped, string customParams)
	{
		ContraptionDatasetUnit contraptionDatasetUnit = new ContraptionDatasetUnit();
		contraptionDatasetUnit.x = x;
		contraptionDatasetUnit.y = y;
		contraptionDatasetUnit.partType = partType;
		contraptionDatasetUnit.customPartIndex = customPartIndex;
		contraptionDatasetUnit.rot = (int)rotation;
		contraptionDatasetUnit.flipped = flipped;
		contraptionDatasetUnit.customParams = string.IsNullOrEmpty(customParams) ? null : customParams;
		m_contraptionDataSet.Add(contraptionDatasetUnit);
	}
}
