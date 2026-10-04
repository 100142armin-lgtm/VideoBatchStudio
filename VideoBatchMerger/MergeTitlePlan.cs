using System;

namespace VideoBatchMerger
{
	public class MergeTitlePlan
	{
		public bool Enabled { get; set; } = false;
		public string StyleId { get; set; } = "tiktok_yellow";
		public string MainTitleTemplate { get; set; } = "{文件名}";
		public string SubTitleTemplate { get; set; } = "";
		public double DurationSeconds { get; set; } = 4.0; // 0 = 全程常驻
		public string Position { get; set; } = "居中偏上"; // 居中偏上, 居中, 居中偏下, 顶部, 底部
		public float FontSizeScale { get; set; } = 1.0f;
		public int CustomOffsetY { get; set; } = 0;

		public MergeTitlePlan Clone()
		{
			return new MergeTitlePlan
			{
				Enabled = this.Enabled,
				StyleId = this.StyleId,
				MainTitleTemplate = this.MainTitleTemplate,
				SubTitleTemplate = this.SubTitleTemplate,
				DurationSeconds = this.DurationSeconds,
				Position = this.Position,
				FontSizeScale = this.FontSizeScale,
				CustomOffsetY = this.CustomOffsetY
			};
		}
	}
}
