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

	// Audio settings
	public int VolumePercent { get; set; } = 100;
	public string LinkedPartnerId { get; set; }

	// Transition effects
	public string TransitionInType { get; set; } = "none";
	public double TransitionInDuration { get; set; } = 1.0;
	public string TransitionOutType { get; set; } = "none";
	public double TransitionOutDuration { get; set; } = 1.0;

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
			Title = this.Title,
			VolumePercent = this.VolumePercent,
			LinkedPartnerId = this.LinkedPartnerId,
			TransitionInType = this.TransitionInType,
			TransitionInDuration = this.TransitionInDuration,
			TransitionOutType = this.TransitionOutType,
			TransitionOutDuration = this.TransitionOutDuration
		};
	}
}
