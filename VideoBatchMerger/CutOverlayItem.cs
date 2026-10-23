using System;
using System.Drawing;
using System.IO;

namespace VideoBatchMerger;

internal enum OverlayItemType
{
	Text,
	Image
}

internal sealed class OverlayWordTiming
{
	public string Word { get; set; } = "";
	public double StartOffset { get; set; }
	public double EndOffset { get; set; }
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
	public string CustomTextColorHex { get; set; } = null;
	public int StrokeIndex { get; set; } = 0;    // 0=8px, 1=12px, 2=5px, 3=2.5px, 4=none
	public string CustomStrokeColorHex { get; set; } = null;
	public int BannerBgIndex { get; set; } = 0;  // 0=None, 1=Banner, 2=Pill, 3=Letterbox, 4=BlackCard
	public bool IsTitleCard { get; set; } = false;
	public int WordAnimMode { get; set; } = 0;   // 0=整句呈现, 1=逐词单蹦, 2=随音逐词追加(打字机), 3=逐词变色高亮(卡拉OK)
	public bool IsAiCaption { get; set; } = false;
	public bool IsSubtitle { get; set; } = false;
	/// <summary>所属轨道 ID：T1=原文字幕/标题包装轨，T2=独立中文字幕轨</summary>
	public string TrackId { get; set; } = "T1";

	/// <summary>Whisper 真实逐词时间（相对字幕块起点的秒数）；为空则回退为按字长估算</summary>
	public System.Collections.Generic.List<OverlayWordTiming> WordTimings { get; set; } = null;
	/// <summary>生成 WordTimings 时的文本；文本被用户改动后与之不一致则自动回退估算</summary>
	public string WordTimingsText { get; set; } = null;
	/// <summary>识别置信度偏低，建议人工核对</summary>
	public bool LowConfidence { get; set; } = false;

	public bool IsSubtitleRole => Type == OverlayItemType.Text && (IsAiCaption || IsSubtitle || TrackId == "T2" || (Name != null && (Name.Contains("字幕") || Name.Contains("配音"))));

	// Image properties
	public string ImagePath { get; set; } = "";
	public int ScalePercent { get; set; } = 80;   // 10% ~ 300%
	public int OpacityPercent { get; set; } = 100; // 10% ~ 100%

	// Layout & Position
	public string PositionPreset { get; set; } = "底部字幕区 (防挡脸推荐)"; // "底部字幕区 (防挡脸推荐)", "居中偏下", "顶部字幕区", "居中偏上 (标题推荐)", "画面正中 (大标题)", "左上角", "右上角", "左下角", "右下角", "自定义坐标"
	public int OffsetX { get; set; } = 0;
	public int OffsetY { get; set; } = 0;

	public string GetDisplayName()
	{
		string typeIcon = (Type == OverlayItemType.Image)
			? "🖼️[贴图]"
			: (TrackId == "T2" ? "🇨🇳[中文字幕]" : (IsSubtitleRole ? "💬[字幕]" : "🎬[标题]"));
		string animTag = WordAnimMode switch
		{
			1 => "[逐词蹦]",
			2 => "[打字机]",
			3 => "[卡拉OK]",
			_ => ""
		};
		string desc;
		if (Type == OverlayItemType.Image)
		{
			desc = string.IsNullOrEmpty(ImagePath) ? "待选图片" : Path.GetFileName(ImagePath);
		}
		else
		{
			desc = string.IsNullOrEmpty(TextContent) ? "空文本" : (TextContent.Length > 14 ? TextContent.Substring(0, 14) + "..." : TextContent);
		}
		return $"{(LowConfidence ? "⚠" : "")}{typeIcon}{animTag} [{StartSeconds:0.0}s~{(StartSeconds + Duration):0.0}s] {desc}";
	}

	public CutOverlayItem Clone()
	{
		return new CutOverlayItem
		{
			Id = this.Id,
			Type = this.Type,
			Enabled = this.Enabled,
			Name = this.Name,
			TrackId = this.TrackId ?? "T1",
			StartSeconds = this.StartSeconds,
			Duration = this.Duration,
			TextContent = this.TextContent,
			SubtitleContent = this.SubtitleContent,
			FontFamily = this.FontFamily,
			FontSize = this.FontSize,
			TextColorIndex = this.TextColorIndex,
			CustomTextColorHex = this.CustomTextColorHex,
			StrokeIndex = this.StrokeIndex,
			CustomStrokeColorHex = this.CustomStrokeColorHex,
			BannerBgIndex = this.BannerBgIndex,
			IsTitleCard = this.IsTitleCard,
			WordAnimMode = this.WordAnimMode,
			IsAiCaption = this.IsAiCaption,
			IsSubtitle = this.IsSubtitle,
			ImagePath = this.ImagePath,
			ScalePercent = this.ScalePercent,
			OpacityPercent = this.OpacityPercent,
			PositionPreset = this.PositionPreset,
			OffsetX = this.OffsetX,
			OffsetY = this.OffsetY,
			WordTimings = this.WordTimings,
			WordTimingsText = this.WordTimingsText,
			LowConfidence = this.LowConfidence
		};
	}

	public CutOverlayItem Duplicate()
	{
		var copy = Clone();
		copy.Id = Guid.NewGuid().ToString("N");
		copy.Name = this.Name + " (副本)";
		copy.StartSeconds = this.StartSeconds + 2.0;
		copy.OffsetX = this.OffsetX + 20;
		copy.OffsetY = this.OffsetY + 20;
		return copy;
	}
}
