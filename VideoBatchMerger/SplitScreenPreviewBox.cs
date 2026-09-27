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
		SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
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
		base.OnPaint(pe);
	}
}
