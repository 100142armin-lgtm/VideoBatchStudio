namespace VideoBatchMerger;

internal sealed class SplitScreenVideoViewSettings
{
	public double ScaleRatio = 1.0;

	public int CropXPercent;

	public int CropYPercent;

	public SplitScreenVideoViewSettings Clone()
	{
		return (SplitScreenVideoViewSettings)MemberwiseClone();
	}
}
