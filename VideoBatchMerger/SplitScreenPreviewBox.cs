using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VideoBatchMerger;

internal sealed class SplitScreenPreviewBox : PictureBox
{
	public SplitScreenPreviewBox()
	{
		DoubleBuffered = true;
		SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
	}

	protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
	{
		if (base.Parent != null && base.Parent.ClientSize.Width > 20 && base.Parent.ClientSize.Height > 20)
		{
			x = 0;
			y = 0;
			width = base.Parent.ClientSize.Width;
			height = base.Parent.ClientSize.Height;
		}
		base.SetBoundsCore(x, y, width, height, specified);
	}

	protected override void OnPaint(PaintEventArgs pe)
	{
		pe.Graphics.Clear(BackColor);
		if (Image != null)
		{
			pe.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
			pe.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
			pe.Graphics.DrawImageUnscaled(Image, 0, 0);
		}
	}
}
