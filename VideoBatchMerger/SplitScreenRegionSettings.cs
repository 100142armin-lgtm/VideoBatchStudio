using System.Drawing;

namespace VideoBatchMerger;

internal sealed class SplitScreenRegionSettings
{
	public int PipSizePercent = 32;

	public int PipOffsetXPercent;

	public int PipOffsetYPercent;

	public string PipShape = "圆角矩形";

	public string PipAspect = "1:1";

	public string PipPosition = "右下";

	public int VolumePercent = 100;

	public int BorderPresetIndex = 1;

	public int BorderWidth;

	public Color BorderColor = Color.Transparent;

	public SplitScreenRegionSettings Clone()
	{
		return (SplitScreenRegionSettings)MemberwiseClone();
	}
}
