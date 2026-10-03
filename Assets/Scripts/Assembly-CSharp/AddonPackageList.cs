using System;
using System.Collections.Generic;

[Serializable]
public class AddonPackageList
{
	public List<AddonPackageInfo> Packages { get; set; }

	public AddonPackageList()
	{
		Packages = new List<AddonPackageInfo>();
	}
}
