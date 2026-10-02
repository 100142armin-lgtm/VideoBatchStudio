using System;
using System.Drawing;
using System.IO;

namespace VideoBatchMerger;

internal enum OverlayItemType
{
	Text,
	Image
}

internal sealed class CutOverlayItem
{
	public string Id { get; set; } = Guid.NewGuid().ToString("N");
	public OverlayItemType Type { get; set; } = OverlayItemType.Text;
	public bool Enabled { get; set; } = true;
	public string Name { get; set; } = "新建图文包装";

	// Timing (seconds)
	public double StartSeconds { get; set; } = 0.0;
	public double Duration { get; set; } = 5.0;

	// Text properties
	public string TextContent { get; set; } = "CINEMATIC MOMENTS";
	public string SubtitleContent { get; set; } = "A Story of Light and Motion";
	public string FontFamily { get; set; } = "Microsoft YaHei UI";
	public int FontSize { get; set; } = 52;
	public int TextColorIndex { get; set; } = 0; // 0=White, 1=Yellow, 2=Cyan, 3=Red, 4=Gold, 5=Green
	public int StrokeIndex { get; set; } = 0;    // 0=8px, 1=12px, 2=5px, 3=2.5px, 4=none
	public int BannerBgIndex { get; set; } = 0;  // 0=None, 1=Banner, 2=Pill, 3=Letterbox, 4=BlackCard
	public bool IsTitleCard { get; set; } = false;

	// Image properties
	public string ImagePath { get; set; } = "";
	public int ScalePercent { get; set; } = 80;   // 10% ~ 300%
	public int OpacityPercent { get; set; } = 100; // 10% ~ 100%

	// Layout & Position
	public string PositionPreset { get; set; } = "居中偏下"; // "居中偏下", "居中偏上", "画面居中", "左上角", "右上角", "左下角", "右下角", "自定义坐标"
	public int OffsetX { get; set; } = 0;
	public int OffsetY { get; set; } = 0;

	public string GetDisplayName()
	{
		string typeIcon = (Type == OverlayItemType.Image) ? "🖼️" : "📝";
		string desc;
		if (Type == OverlayItemType.Image)
		{
			desc = string.IsNullOrEmpty(ImagePath) ? "待选图片" : Path.GetFileName(ImagePath);
		}
		else
		{
			desc = string.IsNullOrEmpty(TextContent) ? "空文本" : (TextContent.Length > 10 ? TextContent.Substring(0, 10) + "..." : TextContent);
		}
		return $"{typeIcon} [{StartSeconds:0.0}s~{(StartSeconds + Duration):0.0}s] {desc}";
	}

	public CutOverlayItem Clone()
	{
		return new CutOverlayItem
		{
			Id = Guid.NewGuid().ToString("N"),
			Type = this.Type,
			Enabled = this.Enabled,
			Name = this.Name + " (副本)",
			StartSeconds = this.StartSeconds + 2.0,
			Duration = this.Duration,
			TextContent = this.TextContent,
			SubtitleContent = this.SubtitleContent,
			FontFamily = this.FontFamily,
			FontSize = this.FontSize,
			TextColorIndex = this.TextColorIndex,
			StrokeIndex = this.StrokeIndex,
			BannerBgIndex = this.BannerBgIndex,
			IsTitleCard = this.IsTitleCard,
			ImagePath = this.ImagePath,
			ScalePercent = this.ScalePercent,
			OpacityPercent = this.OpacityPercent,
			PositionPreset = this.PositionPreset,
			OffsetX = this.OffsetX + 20,
			OffsetY = this.OffsetY + 20
		};
	}
}
