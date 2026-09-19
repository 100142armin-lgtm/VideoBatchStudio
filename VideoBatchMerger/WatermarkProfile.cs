using System.Collections.Generic;

namespace VideoBatchMerger;

internal sealed class WatermarkProfile
{
	public bool Enabled;

	public List<WatermarkSettings> Layers = new List<WatermarkSettings>();

	public List<WatermarkImageLibrary> ImageLibraries = new List<WatermarkImageLibrary>();

	public static WatermarkProfile FromSingle(WatermarkSettings settings)
	{
		WatermarkProfile watermarkProfile = new WatermarkProfile();
		watermarkProfile.Enabled = settings?.Enabled ?? false;
		if (watermarkProfile.Enabled)
		{
			watermarkProfile.Layers.Add(settings);
		}
		return watermarkProfile;
	}
}
