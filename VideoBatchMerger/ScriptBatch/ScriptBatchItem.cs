using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace VideoBatchMerger.ScriptBatch
{
	public sealed class ScriptBatchItem
	{
		public int Id { get; set; }
		public string ScriptCode { get; set; } = string.Empty;   // 文案代码 (如 "43u0cm"，有则抓取，无则跳过)
		public string ChineseTitle { get; set; } = string.Empty; // 中文文案 / 标题 (如 "主耶稣即将再来-这 3 个警示直接关系到你灵魂的归宿1")
		public string Title { get; set; } = string.Empty;        // 备用标题
		public string ScriptText { get; set; } = string.Empty;   // 目标实际配音文案 (如葡萄牙语文案)
		public string VoiceKey { get; set; } = "pt-PT-DuarteNeural";
		public string Status { get; set; } = "等待中";
		public int ProgressPercent { get; set; } = 0;
		public string AudioPath { get; set; } = string.Empty;
		public string TtsCacheKey { get; set; } = string.Empty;
		public string SrtPath { get; set; } = string.Empty;
		public string AssPath { get; set; } = string.Empty;
		public string VideoOutputPath { get; set; } = string.Empty;
		public double DurationSeconds { get; set; } = 0.0;
		public string QcBadge { get; set; } = "⚪ 未检测";
		public string QcDetails { get; set; } = string.Empty;
		public string ErrorMessage { get; set; } = string.Empty;
		public DateTime CreatedAt { get; set; } = DateTime.Now;

		public string DisplayTitle
		{
			get
			{
				if (!string.IsNullOrWhiteSpace(ChineseTitle)) return ChineseTitle;
				if (!string.IsNullOrWhiteSpace(Title)) return Title;
				return $"视频_{Id:D3}";
			}
		}

		public string GenerateStandardFileName(string producerName)
		{
			// 命名规范：ZB-制作人-文案代码-中文文案第一句-当天日期-FF-PT-
			var parts = new List<string>();
			parts.Add("ZB");

			if (!string.IsNullOrWhiteSpace(producerName))
			{
				parts.Add(SanitizeSegment(producerName.Trim()));
			}

			if (!string.IsNullOrWhiteSpace(ScriptCode))
			{
				parts.Add(SanitizeSegment(ScriptCode.Trim()));
			}

			string chineseSentence = ExtractFirstSentence(ChineseTitle);
			if (string.IsNullOrWhiteSpace(chineseSentence))
			{
				chineseSentence = ExtractFirstSentence(Title);
			}
			if (string.IsNullOrWhiteSpace(chineseSentence))
			{
				chineseSentence = ExtractFirstSentence(ScriptText);
			}
			if (string.IsNullOrWhiteSpace(chineseSentence))
			{
				chineseSentence = $"视频_{Id:D3}";
			}

			parts.Add(SanitizeSegment(chineseSentence));
			parts.Add(DateTime.Now.ToString("yyyyMMdd"));
			parts.Add("FF");
			parts.Add("PT");

			return string.Join("-", parts) + "-";
		}

		private static string ExtractFirstSentence(string text)
		{
			if (string.IsNullOrWhiteSpace(text)) return string.Empty;

			// 去除两端换行和首尾引号、标点
			text = text.Trim().Trim('"', '\'', '“', '”', '‘', '’', '【', '】', '《', '》', '（', '）', '(', ')');

			// 1. 优先识别句号/感叹号/问号/分号/换行符作为第一句截止点
			int cutIndex = -1;
			for (int i = 0; i < text.Length; i++)
			{
				char c = text[i];
				if (c == '。' || c == '！' || c == '!' || c == '？' || c == '?' || c == '；' || c == ';' || c == '\r' || c == '\n')
				{
					if (i > 0)
					{
						cutIndex = i;
						break;
					}
				}
				else if (c == '.')
				{
					bool isDecimal = (i > 0 && char.IsDigit(text[i - 1])) && (i + 1 < text.Length && char.IsDigit(text[i + 1]));
					if (!isDecimal && i > 1)
					{
						cutIndex = i;
						break;
					}
				}
			}

			// 2. 若未找到句号，则检测连字符、破折号、冒号、逗号作为语义切分点
			if (cutIndex < 0)
			{
				int dashIdx = text.IndexOfAny(new[] { '-', '—', '：', ':' });
				if (dashIdx >= 3)
				{
					cutIndex = dashIdx;
				}
				else
				{
					int commaIdx = text.IndexOfAny(new[] { '，', ',' });
					if (commaIdx >= 6)
					{
						cutIndex = commaIdx;
					}
				}
			}

			string sentence = (cutIndex > 0) ? text.Substring(0, cutIndex) : text;
			sentence = sentence.Trim().Trim('"', '\'', '“', '”', '‘', '’', '，', ',', '、', '。', '.', '！', '!', '？', '?', '；', ';', '-', '—');

			// 3. 安全防线：单个句子最长限制在 28 个汉字，防止 Windows 文件名超长
			if (sentence.Length > 28)
			{
				sentence = sentence.Substring(0, 28).Trim();
			}

			return sentence.Trim();
		}

		private static string SanitizeSegment(string segment)
		{
			var invalids = Path.GetInvalidFileNameChars();
			var sb = new StringBuilder();
			foreach (char c in segment)
			{
				if (!invalids.Contains(c) && c != '/' && c != '\\' && c != ':' && c != '*' && c != '?' && c != '"' && c != '<' && c != '>' && c != '|')
				{
					sb.Append(c);
				}
				else
				{
					sb.Append('_');
				}
			}
			return sb.ToString().Trim();
		}
	}
}
