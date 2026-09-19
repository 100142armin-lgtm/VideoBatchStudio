using System.Windows.Forms;

namespace VideoBatchMerger;

internal sealed class SplitScreenPreviewBox : PictureBox
{
	protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
	{
		if (base.Parent != null && base.Parent.ClientSize.Width > 100 && width < 100)
		{
			x = 0;
			width = base.Parent.ClientSize.Width;
		}
		if (base.Parent != null && base.Parent.ClientSize.Height > 100 && height < 100)
		{
			y = 0;
			height = base.Parent.ClientSize.Height;
		}
		base.SetBoundsCore(x, y, width, height, specified);
	}
}
