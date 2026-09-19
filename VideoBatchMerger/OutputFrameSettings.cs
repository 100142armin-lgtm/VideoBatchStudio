namespace VideoBatchMerger;

internal sealed class OutputFrameSettings
{
	public int AspectMode;

	public int CropAnchor;

	public int CustomPositionPercent = 50;

	public bool ForcePortrait => AspectMode == 1;

	public bool ForceLandscape => AspectMode == 2;

	public bool ForceAspect
	{
		get
		{
			if (AspectMode != 1)
			{
				return AspectMode == 2;
			}
			return true;
		}
	}

	public OutputFrameSettings Clone()
	{
		OutputFrameSettings outputFrameSettings = new OutputFrameSettings();
		outputFrameSettings.AspectMode = AspectMode;
		outputFrameSettings.CropAnchor = CropAnchor;
		outputFrameSettings.CustomPositionPercent = CustomPositionPercent;
		return outputFrameSettings;
	}
}
