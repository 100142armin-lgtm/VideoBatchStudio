using System;

namespace VideoBatchMerger.Downloader;

/// <summary>
/// 下载任务数据实体
/// </summary>
public sealed class DownloadItem
{
	public int Index { get; set; }
	public string Url { get; set; } = string.Empty;
	public string Platform { get; set; } = "未知平台";
	public string SourceType { get; set; } = "单视频";
	public string Title { get; set; } = "等待解析...";
	public string DurationStr { get; set; } = "--:--";
	public string FileSizeStr { get; set; } = "--";
	public int ProgressPercent { get; set; }
	public string SpeedStr { get; set; } = "--";
	public string Status { get; set; } = "等待中";
	public string SaveFilePath { get; set; } = string.Empty;
	public string ErrorMessage { get; set; } = string.Empty;
	public DateTime CreatedTime { get; set; } = DateTime.Now;
}
