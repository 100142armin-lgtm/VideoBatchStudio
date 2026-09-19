using System.Drawing;

namespace VideoBatchMerger;

internal sealed class WatermarkSettings
{
	public bool Enabled;

	public bool IsText;

	public string Text;

	public string ImagePath;

	public float FontSize;

	public int TextMaxWidthPercent;

	public int SafeMarginPercent;

	public bool AllowOverflow;

	public bool PlaceAboveImages;

	public string FontFamilyName;

	public bool FontBold;

	public bool FontItalic;

	public Color TextColor;

	public string TextAlignment;

	public bool TextOutlineEnabled;

	public Color TextOutlineColor;

	public int TextOutlineWidth;

	public double Opacity;

	public string Position;

	public int ImageWidthPercent;

	public int OffsetX;

	public int OffsetY;

	public double StartSeconds;

	public bool ShowUntilEnd;

	public double EndSeconds;

	public string EntryEffect;

	public double EntryDurationSeconds;

	public string ExitEffect;

	public double ExitDurationSeconds;

	public string StayEffect;

	public double StayIntensity;

	public double StayPeriodSeconds;

	public double StayPauseSeconds;

	public double SlideDurationSeconds;

	public bool TextBackgroundEnabled;

	public Color TextBackgroundColor;

	public double TextBackgroundOpacity;

	public int TextBackgroundPaddingX;

	public int TextBackgroundPaddingY;

	public int TextBackgroundCornerRadius;

	public string TextBackgroundStyle;
}
