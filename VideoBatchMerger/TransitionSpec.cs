namespace VideoBatchMerger;

internal sealed class TransitionSpec
{
	public bool Enabled;

	public string DisplayName;

	public string FfmpegName;

	public string CustomExpression;

	public double DurationSeconds;

	public static TransitionSpec None()
	{
		TransitionSpec transitionSpec = new TransitionSpec();
		transitionSpec.Enabled = false;
		transitionSpec.DisplayName = "无转场";
		transitionSpec.FfmpegName = "fade";
		transitionSpec.DurationSeconds = 1.0;
		return transitionSpec;
	}
}
