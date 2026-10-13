using System;

namespace VideoBatchMerger.Transcriber;

public sealed class SubtitleSegment
{
	public int Index { get; set; }
	public TimeSpan StartTime { get; set; }
	public TimeSpan EndTime { get; set; }
	public string OriginalText { get; set; } = string.Empty;
	public string TranslatedText { get; set; } = string.Empty;

	public string TimeRangeStr => $"{FormatTime(StartTime)} --> {FormatTime(EndTime)}";

	private static string FormatTime(TimeSpan ts)
	{
		return string.Format("{0:00}:{1:00}:{2:00}.{3:000}",
			(int)ts.TotalHours, ts.Minutes, ts.Seconds, ts.Milliseconds);
	}
}

public sealed class TranscribeItem
{
	public int Index { get; set; }
	public string SourceName { get; set; } = string.Empty;
	public string SourcePathOrUrl { get; set; } = string.Empty;
	public bool IsOnlineUrl { get; set; }
	public string DurationStr { get; set; } = "--:--";
	public string Status { get; set; } = "等待中";
	public int ProgressPercent { get; set; }
	public string DetectedLanguage { get; set; } = "未知";
	public string ErrorMessage { get; set; } = string.Empty;
	public string LocalMediaFilePath { get; set; } = string.Empty;
	public System.Collections.Generic.List<SubtitleSegment> Segments { get; set; } = new();
}
