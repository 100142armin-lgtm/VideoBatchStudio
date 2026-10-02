using System;
using System.Drawing;

namespace VideoBatchMerger;

internal enum TrackType
{
	Video,
	Audio,
	Subtitle
}

internal sealed class TimelineTrack
{
	public string Id { get; set; }
	public string Name { get; set; }
	public TrackType Type { get; set; }
	public bool IsLocked { get; set; }
	public bool IsMuted { get; set; }
	public bool IsVisible { get; set; } = true;
	public int Height { get; set; } = 36;

	public Color GetBadgeColor()
	{
		return Type switch
		{
			TrackType.Video => Color.FromArgb(6, 182, 212),     // Cyan
			TrackType.Audio => Color.FromArgb(34, 197, 94),     // Emerald Green
			TrackType.Subtitle => Color.FromArgb(59, 130, 246), // Blue
			_ => Color.FromArgb(148, 163, 184)
		};
	}

	public Color GetTrackBodyColor()
	{
		return Type switch
		{
			TrackType.Video => Color.FromArgb(24, 38, 58),
			TrackType.Audio => Color.FromArgb(20, 42, 34),
			TrackType.Subtitle => Color.FromArgb(28, 34, 52),
			_ => Color.FromArgb(22, 28, 40)
		};
	}
}
