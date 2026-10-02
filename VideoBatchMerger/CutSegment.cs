using System;

namespace VideoBatchMerger;

internal sealed class CutSegment
{
	public string Id { get; set; } = Guid.NewGuid().ToString("N");
	public string SourcePath { get; set; }
	public string TrackId { get; set; } = "V1";
	public string MediaType { get; set; } = "video"; // "video", "image", "audio", "title"
	public double StartSeconds { get; set; }
	public double EndSeconds { get; set; }
	public double TimelineStartSeconds { get; set; }
	public bool IsKept { get; set; } = true;
	public string Title { get; set; }
	public double Duration => Math.Max(0.0, EndSeconds - StartSeconds);

	public CutSegment Clone()
	{
		return new CutSegment
		{
			Id = this.Id,
			SourcePath = this.SourcePath,
			TrackId = this.TrackId,
			MediaType = this.MediaType,
			StartSeconds = this.StartSeconds,
			EndSeconds = this.EndSeconds,
			TimelineStartSeconds = this.TimelineStartSeconds,
			IsKept = this.IsKept,
			Title = this.Title
		};
	}
}
