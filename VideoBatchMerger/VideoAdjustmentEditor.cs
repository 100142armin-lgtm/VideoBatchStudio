using System.Windows.Forms;

namespace VideoBatchMerger;

internal sealed class VideoAdjustmentEditor
{
	public Panel Root;

	public CheckBox HorizontalFlip;

	public CheckBox ReversePlayback;

	public CheckBox CenterCropPortrait;

	public NumericUpDown ScaleRatio;

	public NumericUpDown SpeedRatio;

	public NumericUpDown VolumePercent;

	public Button ApplySelected;

	public Button ApplyAll;

	public Button CopyAdjustment;

	public Button PasteAdjustment;

	public int DragRow = -1;

	public int DragColumn = -1;
}
