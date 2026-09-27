using System;

namespace VideoBatchMerger;

internal sealed class CutSegment
{
	public string SourcePath { get; set; }
	public double StartSeconds { get; set; }
	public double EndSeconds { get; set; }
	public bool IsKept { get; set; } = true;
	public double Duration => Math.Max(0.0, EndSeconds - StartSeconds);
}
