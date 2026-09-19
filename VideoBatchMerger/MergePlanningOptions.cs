namespace VideoBatchMerger;

internal sealed class MergePlanningOptions
{
	public int MaxItemsPerGroup;

	public int MaxRepeatsPerSource;

	public int MaxOutputCount;

	public bool RandomCombinationStart;

	public bool LimitDuration;

	public double MaxDurationSeconds;

	public bool RandomClipRanges;

	public int OverlongMode;

	public bool SortBySimilarity;

	public bool DetailedSimilarity;

	public double SimilarityThreshold;
}
