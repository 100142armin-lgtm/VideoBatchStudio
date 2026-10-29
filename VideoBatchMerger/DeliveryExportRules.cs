using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace VideoBatchMerger;

internal static class DeliveryExportRules
{
	public static string BuildFileName(string sourcePath, bool custom, string template, string extension, DateTime now)
	{
		string sourceName = Path.GetFileNameWithoutExtension(sourcePath ?? "");
		if (string.IsNullOrWhiteSpace(sourceName)) sourceName = "视频";
		string name = custom ? (template ?? "").Trim() : sourceName + "_已剪辑";
		if (custom && string.IsNullOrWhiteSpace(name)) throw new ArgumentException("请填写自定义成片名称。");
		string stamp = now.ToString("yyyyMMdd_HHmmss");
		name = name.Replace("{timestamp}", stamp).Replace("{time}", stamp)
			.Replace("{原名}", sourceName).Replace("{source}", sourceName);
		if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.EndsWith(".") ||
			name.EndsWith(" ") || name == "." || name == "..")
			throw new ArgumentException("成片名称不能包含路径或 \\ / : * ? \" < > | 等字符，也不能以空格或句点结尾。");
		string currentExt = Path.GetExtension(name);
		if (custom && new[] { ".mp4", ".mov", ".mkv" }.Contains(currentExt, StringComparer.OrdinalIgnoreCase))
			name = name.Substring(0, name.Length - currentExt.Length);
		if (Regex.IsMatch(name, @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)", RegexOptions.IgnoreCase))
			throw new ArgumentException("该名称是 Windows 保留名称，请换一个名称。");
		return name + extension;
	}

	// Never overwrite the source or a previous delivery, including on repeated exports.
	public static string GetAvailableOutputPath(string folder, string fileName)
	{
		string result = Path.Combine(folder, fileName);
		string stem = Path.GetFileNameWithoutExtension(fileName);
		string ext = Path.GetExtension(fileName);
		for (int suffix = 2; File.Exists(result) || Directory.Exists(result); suffix++)
			result = Path.Combine(folder, stem + " (" + suffix + ")" + ext);
		return result;
	}

	// Stream copy is exact only when the entire original audio is still on the timeline.
	// Titles, subtitles, and visual filters do not affect this decision.
	public static bool CanCopyOriginalAudio(IList<CutSegment> video, IList<CutSegment> audio,
		string fallbackSource, VideoInfo info, string extension, int volume, bool denoise,
		bool loudnorm, int voiceFx, bool ducking)
	{
		if (video.Count != 1 || !info.HasAudio || volume != 100 || denoise || loudnorm || voiceFx > 0)
			return false;
		bool compatible = info.AudioCodec == "aac" || info.AudioCodec == "mp3" ||
			(extension != ".mp4" && info.AudioCodec == "alac") ||
			(extension == ".mkv" && new[] { "opus", "vorbis", "flac", "pcm_s16le", "pcm_s24le" }.Contains(info.AudioCodec));
		if (!compatible || audio.Count > 1) return false;
		CutSegment v = video[0];
		if (!string.Equals(v.MediaType, "video", StringComparison.OrdinalIgnoreCase) ||
			Math.Abs(v.StartSeconds) > 0.001 || Math.Abs(v.TimelineStartSeconds) > 0.001 ||
			Math.Abs(v.EndSeconds - info.DurationSeconds) > 0.02) return false;
		if (audio.Count == 0) return true;
		CutSegment a = audio[0];
		string videoSource = string.IsNullOrEmpty(v.SourcePath) ? fallbackSource : v.SourcePath;
		string audioSource = string.IsNullOrEmpty(a.SourcePath) ? fallbackSource : a.SourcePath;
		return a.TrackId == "A1" && a.VolumePercent == 100 &&
			string.Equals(videoSource, audioSource, StringComparison.OrdinalIgnoreCase) &&
			Math.Abs(a.StartSeconds) <= 0.001 && Math.Abs(a.TimelineStartSeconds) <= 0.001 &&
			Math.Abs(a.EndSeconds - v.EndSeconds) <= 0.001;
	}
}
