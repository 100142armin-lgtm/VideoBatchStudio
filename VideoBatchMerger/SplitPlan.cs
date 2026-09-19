namespace VideoBatchMerger;

internal sealed class SplitPlan
{
	public bool EqualPartsMode;

	public double SegmentSeconds;

	public int EqualParts;

	public bool DiscardShortTail;
}
