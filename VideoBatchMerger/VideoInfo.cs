namespace VideoBatchMerger;

internal sealed class VideoInfo
{
	public string Path;

	public double DurationSeconds;

	public int Width;

	public int Height;

	public int CodedWidth;

	public int CodedHeight;

	public int RotationDegrees;

	public bool HasVideo;

	public bool HasAudio;
}
