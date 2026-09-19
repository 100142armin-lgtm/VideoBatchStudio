namespace VideoBatchMerger;

internal sealed class MergeResult
{
	public bool Success;

	public bool Cancelled;

	public string Error;

	public double RenderedDurationSeconds;
}
