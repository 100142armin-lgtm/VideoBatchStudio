using System.Collections.Generic;

namespace VideoBatchMerger;

internal sealed class BgmPlan
{
	public readonly List<string> Files = new List<string>();

	public bool RandomAssignment;

	public int VolumePercent;

	public bool Enabled
	{
		get
		{
			if (Files.Count > 0)
			{
				return VolumePercent > 0;
			}
			return false;
		}
	}
}
