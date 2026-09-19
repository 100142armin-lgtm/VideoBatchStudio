using System.Collections.Generic;

namespace VideoBatchMerger;

internal sealed class WatermarkImageLibrary
{
	public int CandidateCount;

	public bool RandomAssignment;

	public bool Slideshow;

	public string SwitchEffect;

	public double SwitchDurationSeconds;

	public double SwitchIntervalSeconds;

	public List<WatermarkSettings> Images = new List<WatermarkSettings>();
}
