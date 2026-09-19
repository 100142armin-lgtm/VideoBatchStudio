using System.Collections.Generic;

namespace VideoBatchMerger;

internal sealed class ImageVideoLayoutPlan
{
	public int ImagesPerPage = 1;

	public string DisplayName = "单图轮播";

	public string TileAnimation = "直接显示";

	public double TileAnimationDuration = 0.6;

	public bool SmartEnhance;

	public bool RandomLayouts = true;

	public bool RandomMotions = true;

	public int OutputCount = 1;

	public double TargetDurationSeconds = 30.0;

	public List<ImageLayoutChoice> LayoutChoices = new List<ImageLayoutChoice>();

	public List<string> MotionEffects = new List<string>();

	public List<ImageVideoPagePlan> Pages = new List<ImageVideoPagePlan>();

	public bool IsCollage => ImagesPerPage > 1;
}
