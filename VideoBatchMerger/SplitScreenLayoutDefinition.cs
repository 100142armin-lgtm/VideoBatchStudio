using System.Drawing;

namespace VideoBatchMerger;

internal sealed class SplitScreenLayoutDefinition
{
	public string Id;

	public string DisplayName;

	public string Category;

	public RectangleF[] Regions;

	public bool PictureInPicture;

	public int RegionCount
	{
		get
		{
			if (Regions != null)
			{
				return Regions.Length;
			}
			return 0;
		}
	}
}
