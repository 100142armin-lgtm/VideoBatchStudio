using System.Drawing;

namespace VideoBatchMerger;

internal sealed class SplitScreenRenderPlan
{
	public SplitScreenLayoutDefinition Layout;

	public int Width;

	public int Height;

	public double DurationSeconds;

	public int OutputCount;

	public bool RandomAssignment;

	public int AudioRegion;

	public int BorderWidth;

	public Color BorderColor;

	public string PipShape;

	public string PipAspect;

	public string PipPosition;

	public int PipSizePercent;

	public int PipOffsetXPercent;

	public int PipOffsetYPercent;

	public bool FollowMainDuration;

	public int MainRegion;

	public bool MixAllAudio;

	public bool[] EnabledRegions;

	public bool[] BorderRegions;

	public int[] RegionVolumePercents;

	public SplitScreenRegionSettings[] RegionSettings;

	public SplitScreenVideoViewSettings[] VideoViewSettings;
}
