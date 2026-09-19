using System.Collections.Generic;
using System.Windows.Forms;

namespace VideoBatchMerger;

internal sealed class BgmControls
{
	public readonly List<string> Files = new List<string>();

	public Button Add;

	public Button Clear;

	public ComboBox AssignmentMode;

	public NumericUpDown VolumePercent;

	public Label CountLabel;
}
