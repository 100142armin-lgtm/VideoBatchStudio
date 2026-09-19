using System;

namespace VideoBatchMerger;

internal sealed class VideoAdjustmentSettings
{
	public bool HorizontalFlip;

	public bool ReversePlayback;

	public bool CenterCropPortrait = true;

	public double ScaleRatio = 1.0;

	public double SpeedRatio = 1.0;

	public int VolumePercent = 100;

	public bool IsIdentity
	{
		get
		{
			if (!HorizontalFlip && !ReversePlayback && Math.Abs(ScaleRatio - 1.0) < 0.0001 && Math.Abs(SpeedRatio - 1.0) < 0.0001)
			{
				return VolumePercent == 100;
			}
			return false;
		}
	}

	public VideoAdjustmentSettings Clone()
	{
		VideoAdjustmentSettings videoAdjustmentSettings = new VideoAdjustmentSettings();
		videoAdjustmentSettings.HorizontalFlip = HorizontalFlip;
		videoAdjustmentSettings.ReversePlayback = ReversePlayback;
		videoAdjustmentSettings.CenterCropPortrait = CenterCropPortrait;
		videoAdjustmentSettings.ScaleRatio = ScaleRatio;
		videoAdjustmentSettings.SpeedRatio = SpeedRatio;
		videoAdjustmentSettings.VolumePercent = VolumePercent;
		return videoAdjustmentSettings;
	}
}
