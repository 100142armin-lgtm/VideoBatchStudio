using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Speech.Synthesis;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using VideoBatchMerger.Transcriber;

namespace VideoBatchMerger;

internal static class SmartCutEngine
{
	private static readonly HttpClient s_http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

	public sealed class TimedWordSlice
	{
		public int WordIndex { get; set; }
		public string Word { get; set; } = "";
		public double StartSec { get; set; }
		public double EndSec { get; set; }
		public double Duration => Math.Max(0.05, EndSec - StartSec);
	}

	public sealed class ScriptAlignmentReport
	{
		public List<SubtitleSegment> KeptSegments { get; set; } = new List<SubtitleSegment>();
		public List<(double StartSec, double EndSec, string Reason)> BadTakeRangesToCut { get; set; } = new List<(double, double, string)>();
		public int MatchedClauseCount { get; set; }
		public int CorrectedDiffCount { get; set; }
		public int RetakeRemovedCount { get; set; }
		public int SkippedScriptClauseCount { get; set; }
		public int ExtraAdlibKeptCount { get; set; }
		public string SummaryText { get; set; } = "";
	}

	public sealed class TtsSynthesisResult
	{
		public string AudioWavPath { get; set; } = "";
		public double TotalDurationSeconds { get; set; }
		public List<SubtitleSegment> Segments { get; set; } = new List<SubtitleSegment>();
	}

	/// <summary>
	/// 将一句字幕拆分为带精确时间戳的“逐词切片”（支持英文/多语种单词及中文词汇节奏切分）
	/// </summary>
	public static List<TimedWordSlice> SplitIntoTimedWords(string text, double startSec, double duration)
	{
		var result = new List<TimedWordSlice>();
		if (string.IsNullOrWhiteSpace(text) || duration <= 0.01)
			return result;

		List<string> rawTokens = TokenizeIntoWords(text.Trim());
		if (rawTokens.Count == 0)
			return result;

		double totalWeight = 0.0;
		double[] weights = new double[rawTokens.Count];
		for (int i = 0; i < rawTokens.Count; i++)
		{
			string tk = rawTokens[i];
			// 依据词长分配发音时长权重，保证长词停留稍长、短词干脆卡点
			double w = Math.Max(1.0, Math.Pow(tk.Trim().Length, 0.65));
			if (tk.EndsWith(",") || tk.EndsWith(".") || tk.EndsWith("!") || tk.EndsWith("?") ||
				tk.EndsWith("，") || tk.EndsWith("。") || tk.EndsWith("！") || tk.EndsWith("？"))
			{
				w += 0.45;
			}
			weights[i] = w;
			totalWeight += w;
		}

		double cursor = startSec;
		for (int i = 0; i < rawTokens.Count; i++)
		{
			double sliceDur = (i == rawTokens.Count - 1)
				? Math.Max(0.04, (startSec + duration) - cursor)
				: Math.Max(0.04, duration * (weights[i] / Math.Max(0.001, totalWeight)));

			result.Add(new TimedWordSlice
			{
				WordIndex = i,
				Word = rawTokens[i],
				StartSec = cursor,
				EndSec = Math.Min(startSec + duration, cursor + sliceDur)
			});
			cursor += sliceDur;
		}

		return result;
	}

	/// <summary>
	/// 获取字幕块的逐词切片：若存在 Whisper 真实逐词时间且文本未被改动，则使用真实时间(随字幕块移动/裁剪自动平移与截断)，
	/// 否则回退为按字长权重估算。
	/// </summary>
	internal static List<TimedWordSlice> GetWordSlices(CutOverlayItem item, string text, double startSec, double duration)
	{
		if (item != null && item.WordTimings != null && item.WordTimings.Count > 0
			&& string.Equals(item.WordTimingsText, text, StringComparison.Ordinal))
		{
			var real = new List<TimedWordSlice>();
			double endLimit = startSec + duration;
			for (int i = 0; i < item.WordTimings.Count; i++)
			{
				var w = item.WordTimings[i];
				double s = startSec + w.StartOffset;
				if (s >= endLimit - 0.02) break;
				double e = startSec + w.EndOffset;
				if (i + 1 < item.WordTimings.Count)
				{
					double nextS = startSec + item.WordTimings[i + 1].StartOffset;
					e = Math.Min(nextS, Math.Max(e, s + 0.05) + 0.35); // 词间小空隙内保持当前词，避免闪烁
					e = Math.Max(e, s + 0.05);
				}
				e = Math.Min(e, endLimit);
				real.Add(new TimedWordSlice { WordIndex = i, Word = w.Word, StartSec = s, EndSec = Math.Max(e, s + 0.04) });
			}
			if (real.Count > 0) return real;
		}
		return SplitIntoTimedWords(text, startSec, duration);
	}


	public static List<string> TokenizeIntoWords(string text)
	{
		var tokens = new List<string>();
		if (string.IsNullOrWhiteSpace(text)) return tokens;

		string clean = text.Replace("\r\n", " ").Replace("\n", " ").Trim();
		bool hasSpaces = clean.Contains(' ');

		if (hasSpaces)
		{
			string[] parts = clean.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			foreach (var p in parts)
			{
				tokens.Add(p);
			}
			return tokens;
		}

		// 中文/日文等无空格文本：按 2~3 字语义节奏切分为词块（单字助词智能合并）
		int i = 0;
		while (i < clean.Length)
		{
			int remaining = clean.Length - i;
			int step = (remaining <= 3) ? remaining : 2;
			if (i + step < clean.Length && IsPunctuation(clean[i + step]))
			{
				step++;
			}
			string chunk = clean.Substring(i, step).Trim();
			if (!string.IsNullOrEmpty(chunk))
			{
				tokens.Add(chunk);
			}
			i += step;
		}
		return tokens;
	}

	private static bool IsPunctuation(char c)
	{
		return char.IsPunctuation(c) || "，。！？、；：“”‘’（）《》【】".IndexOf(c) >= 0;
	}

	/// <summary>
	/// 视频原声识别结果与【标准文案】进行智能容错锚点对齐（处理同义词差异、口误重念、多说话、少说话/漏读）
	/// alignMode: 0 = 以标准文案为准(意思相近时直接替换为标准文案书面语并纠错), 1 = 以人物原声为准(仅纠正同音错别字，保留原话词汇)
	/// </summary>
	public static ScriptAlignmentReport AlignWithStandardScript(
		List<SubtitleSegment> rawSegments,
		string standardScript,
		int alignMode,
		bool cutRetakesAndExtras)
	{
		var report = new ScriptAlignmentReport();
		if (rawSegments == null || rawSegments.Count == 0)
			return report;

		if (string.IsNullOrWhiteSpace(standardScript))
		{
			// 未提供标准文案时：仍然自动检测相邻重复口误重念与纯语气废话
			for (int i = 0; i < rawSegments.Count; i++)
			{
				var cur = rawSegments[i];
				if (IsPureFillerOrAbortedPhrase(cur.OriginalText))
				{
					report.RetakeRemovedCount++;
					report.BadTakeRangesToCut.Add((cur.StartTime.TotalSeconds, cur.EndTime.TotalSeconds, "语气停顿/口误废话"));
					if (!cutRetakesAndExtras) report.KeptSegments.Add(cur);
					continue;
				}

				if (i + 1 < rawSegments.Count && IsRetakeOfNext(cur.OriginalText, rawSegments[i + 1].OriginalText))
				{
					report.RetakeRemovedCount++;
					report.BadTakeRangesToCut.Add((cur.StartTime.TotalSeconds, cur.EndTime.TotalSeconds, "口误重念(前遍废片)"));
					if (!cutRetakesAndExtras) report.KeptSegments.Add(cur);
					continue;
				}

				report.KeptSegments.Add(cur);
			}

			report.SummaryText = $"智能检测完成：保留有效语音 {report.KeptSegments.Count} 句，识别口误/重复废话 {report.RetakeRemovedCount} 处。";
			return report;
		}

		List<string> scriptClauses = SplitScriptIntoClauses(standardScript);
		if (scriptClauses.Count == 0)
		{
			report.KeptSegments.AddRange(rawSegments);
			return report;
		}

		// 构建标准文案的有效词汇白名单表（用于全流程防误剪护航）
		var scriptContentVocab = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var cl in scriptClauses)
		{
			foreach (var tok in TokenizeIntoWords(cl))
			{
				string n = NormalizeForCompare(tok);
				if (n.Length >= 2) scriptContentVocab.Add(n);
			}
		}

		// 第一步：先标记明显的相邻重念（例如人物一句话说了一半卡住，紧接着重新完整说了一遍）
		bool[] isRetake = new bool[rawSegments.Count];
		for (int i = 0; i < rawSegments.Count - 1; i++)
		{
			string s1 = rawSegments[i].OriginalText;
			string s2 = rawSegments[i + 1].OriginalText;
			if (IsPureFillerOrAbortedPhrase(s1) || IsRetakeOfNext(s1, s2))
			{
				isRetake[i] = true;
			}
		}

		// 第二步：利用单调锚点匹配算法将语音片段与标准文案分句对齐（支持多说插入、少说漏读跳过）
		int scriptCursor = 0;
		var usedScriptIndices = new HashSet<int>();

		for (int i = 0; i < rawSegments.Count; i++)
		{
			var seg = rawSegments[i];
			if (isRetake[i])
			{
				report.RetakeRemovedCount++;
				report.BadTakeRangesToCut.Add((seg.StartTime.TotalSeconds, seg.EndTime.TotalSeconds, "口误重念"));
				if (!cutRetakesAndExtras)
				{
					report.KeptSegments.Add(seg);
				}
				continue;
			}

			// 计算该原声片段中是否有词汇属于标准文案（白名单判定）
			int scriptWordHits = 0;
			int totalSegWords = 0;
			var segWords = TokenizeIntoWords(seg.OriginalText ?? "");
			foreach (var sw in segWords)
			{
				string nw = NormalizeForCompare(sw);
				if (nw.Length >= 2)
				{
					totalSegWords++;
					if (scriptContentVocab.Contains(nw)) scriptWordHits++;
				}
			}
			bool isScriptContent = scriptWordHits >= 1 && (totalSegWords <= 2 || (double)scriptWordHits / Math.Max(1, totalSegWords) >= 0.25);

			// 在标准文案中搜索最佳语义/字词匹配分句（全范围搜索 + 游标邻域顺承加权，防止因断句长短不一导致游标失步漏字幕）
			int bestScriptIdx = -1;
			double bestSim = 0.0;

			for (int j = 0; j < scriptClauses.Count; j++)
			{
				if (usedScriptIndices.Contains(j) && j < scriptCursor - 1) continue;
				double sim = ComputeClauseSimilarity(seg.OriginalText, scriptClauses[j]);
				if (j >= scriptCursor - 1 && j <= scriptCursor + 6) sim += 0.05;
				if (j == scriptCursor) sim += 0.04;
				if (sim > bestSim)
				{
					bestSim = sim;
					bestScriptIdx = j;
				}
			}

			// 若相似度未达标但片段明显属于标准文案词汇（短标号/短句子），在文案中查找子串包含
			if ((bestScriptIdx < 0 || bestSim < 0.30) && isScriptContent)
			{
				for (int j = 0; j < scriptClauses.Count; j++)
				{
					string clNorm = NormalizeForCompare(scriptClauses[j]);
					string segNorm = NormalizeForCompare(seg.OriginalText ?? "");
					if (clNorm.Contains(segNorm) || segNorm.Contains(clNorm))
					{
						bestScriptIdx = j;
						bestSim = 0.55;
						break;
					}
				}
			}

			// 判断是否与下一句重复念了同一句标准文案（即人物完整念了两遍同一句文案，后一遍通常是更满意的重录）
			if (bestScriptIdx >= 0 && bestSim >= 0.40 && i + 1 < rawSegments.Count)
			{
				double nextSimToSame = ComputeClauseSimilarity(rawSegments[i + 1].OriginalText, scriptClauses[bestScriptIdx]);
				if (nextSimToSame >= 0.52 && nextSimToSame >= bestSim - 0.06)
				{
					report.RetakeRemovedCount++;
					report.BadTakeRangesToCut.Add((seg.StartTime.TotalSeconds, seg.EndTime.TotalSeconds, "重复录制(自动保留后遍)"));
					if (!cutRetakesAndExtras)
					{
						report.KeptSegments.Add(seg);
					}
					continue;
				}
			}

			if (bestScriptIdx >= 0 && (bestSim >= 0.30 || isScriptContent))
			{
				if (bestScriptIdx > scriptCursor)
				{
					for (int skipped = scriptCursor; skipped < bestScriptIdx; skipped++)
					{
						if (!usedScriptIndices.Contains(skipped))
						{
							report.SkippedScriptClauseCount++;
						}
					}
				}

				usedScriptIndices.Add(bestScriptIdx);
				scriptCursor = Math.Max(scriptCursor, bestScriptIdx + 1);
				report.MatchedClauseCount++;

				string targetScriptClause = scriptClauses[bestScriptIdx];
				string originalSpoken = seg.OriginalText?.Trim() ?? "";

				var alignedSeg = new SubtitleSegment
				{
					Index = report.KeptSegments.Count + 1,
					StartTime = seg.StartTime,
					EndTime = seg.EndTime,
					OriginalText = seg.OriginalText,
					TranslatedText = seg.TranslatedText,
					Words = seg.Words,
					Confidence = seg.Confidence
				};

				if (!string.Equals(NormalizeForCompare(originalSpoken), NormalizeForCompare(targetScriptClause), StringComparison.OrdinalIgnoreCase))
				{
					report.CorrectedDiffCount++;
				}

				if (alignMode == 0)
				{
					// 模式 0：以标准文案为准（意思匹配时直接采用规范的标准文案，时间轴锁定人物发音）
					alignedSeg.OriginalText = targetScriptClause;
				}
				else
				{
					// 模式 1：以人物原声为准（保留主播说的每一句话，仅根据标准文案纠正错字/同音字）
					alignedSeg.OriginalText = CorrectHomophonesOnly(originalSpoken, targetScriptClause, bestSim);
				}

				report.KeptSegments.Add(alignedSeg);
			}
			else
			{
				// 白名单最高防护：只要片段中包含标准文案的词汇，绝对禁止作为“文案外多说内容”切除！
				if (alignMode == 1 || !cutRetakesAndExtras || isScriptContent)
				{
					report.ExtraAdlibKeptCount++;
					report.KeptSegments.Add(new SubtitleSegment
					{
						Index = report.KeptSegments.Count + 1,
						StartTime = seg.StartTime,
						EndTime = seg.EndTime,
						OriginalText = seg.OriginalText,
						TranslatedText = seg.TranslatedText,
						Words = seg.Words,
						Confidence = seg.Confidence
					});
				}
				else
				{
					report.RetakeRemovedCount++;
					report.BadTakeRangesToCut.Add((seg.StartTime.TotalSeconds, seg.EndTime.TotalSeconds, "文案外多说内容"));
				}
			}
		}

		// 统计末尾未念出的标准文案分句
		for (int rest = scriptCursor; rest < scriptClauses.Count; rest++)
		{
			if (!usedScriptIndices.Contains(rest))
			{
				report.SkippedScriptClauseCount++;
			}
		}

		report.SummaryText =
			$"对齐标准文案 {report.MatchedClauseCount} 句 | " +
			$"智能纠错 {report.CorrectedDiffCount} 处 | " +
			$"剔除重复/口误 {report.RetakeRemovedCount} 处 | " +
			$"保留原声共 {report.KeptSegments.Count} 句";

		return report;
	}

	/// <summary>
	/// 智能修复文案中缺失空格、标点粘连、驼峰紧贴（如 "Pai-NossoRecitamos", "céusQuando", "é.Segundo:"）的常见排版问题
	/// </summary>
	public static string PreprocessAndNormalizeScript(string script)
	{
		if (string.IsNullOrWhiteSpace(script)) return "";
		string s = script.Replace("\r\n", "\n").Trim();
		// 1. 标点符号紧接英文字母/西文重音字符/数字时，智能补全换行断句（如 "é.Segundo" -> "é.\nSegundo", "importantes?Primeiro" -> "importantes?\nPrimeiro"）
		s = Regex.Replace(s, @"([.?!:;。！？；：])([A-Za-z\u00C0-\u00FF\u4e00-\u9fa5\d])", "$1\n$2");
		// 2. 在多语种序数词/标号（如 Primeiro:, Segundo:, Terceiro:, Quarto:, 1., 第一、 等）前插入断句
		s = Regex.Replace(s, @"(?<!\n)(Primeiro|Segundo|Terceiro|Quarto|Quinto|Sexto|Sétimo|Oitavo|Nono|Décimo|第[一二三四五六七八九十\d]+[、.：:]|[1-9]\d*[\.、])", "\n$1", RegexOptions.IgnoreCase);
		// 3. 小写字母直接紧贴大写字母（跨行复制丢失空格的黏连，如 "céusQuando", "nomeIsto", "reinoO", "céuOrar"）断句
		s = Regex.Replace(s, @"([a-z\u00E0-\u00FF])([A-Z\u00C0-\u00DF])", "$1\n$2");
		return s;
	}

	public static List<string> SplitScriptIntoClauses(string script)
	{
		var clauses = new List<string>();
		if (string.IsNullOrWhiteSpace(script)) return clauses;

		string norm = PreprocessAndNormalizeScript(script);

		// 按换行或句末标点切分为自然字幕分句
		string[] rawLines = Regex.Split(norm, @"[\n。！？!?；;]+");
		foreach (var line in rawLines)
		{
			string trimmed = line.Trim();
			if (string.IsNullOrEmpty(trimmed)) continue;

			// 若单句包含冒号（如 "Primeiro: Pai nosso..."），按冒号拆开为序数词标签 + 正文
			if (trimmed.Contains(':') || trimmed.Contains('：'))
			{
				string[] colons = Regex.Split(trimmed, @"[:：]+");
				if (colons.Length == 2 && colons[0].Trim().Length <= 14 && !string.IsNullOrWhiteSpace(colons[1]))
				{
					clauses.Add(colons[0].Trim() + ":");
					trimmed = colons[1].Trim();
				}
			}

			// 若单句过长（如超过 24 个汉字或 12 个英文词），再按逗号细分以匹配 Whisper 自然断句
			bool hasSpaces = trimmed.Contains(' ');
			if ((!hasSpaces && trimmed.Length > 24) || (hasSpaces && trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length > 12))
			{
				string[] subParts = Regex.Split(trimmed, @"[，,]+");
				foreach (var sp in subParts)
				{
					if (!string.IsNullOrWhiteSpace(sp))
						clauses.Add(sp.Trim());
				}
			}
			else
			{
				clauses.Add(trimmed);
			}
		}
		return clauses;
	}

	private static bool IsPureFillerOrAbortedPhrase(string text)
	{
		if (string.IsNullOrWhiteSpace(text)) return true;
		string clean = NormalizeForCompare(text);
		if (clean.Length == 0) return true;

		string[] fillers =
		{
			"嗯", "啊", "呃", "哦", "那个", "就是", "不对", "重来", "说错了", "再来一遍", "卡了",
			"uh", "um", "er", "ah", "hmm", "oops", "sorry", "wait", "letmerestart"
		};
		foreach (var f in fillers)
		{
			if (clean.Equals(f, StringComparison.OrdinalIgnoreCase)) return true;
		}
		return false;
	}

	private static bool IsRetakeOfNext(string first, string second)
	{
		string n1 = NormalizeForCompare(first);
		string n2 = NormalizeForCompare(second);
		if (string.IsNullOrEmpty(n1) || string.IsNullOrEmpty(n2)) return false;

		// 葡萄牙语及通用序数词与高频语法词绝对豁免（绝不是口误重念）
		string[] ordinalsAndKeywords =
		{
			"primeiro", "segundo", "terceiro", "quarto", "quinto", "sexto", "sétimo", "oitavo", "nono", "décimo",
			"para", "como", "quando", "significa", "porque", "entao", "tambem", "deus", "senhor", "jesus", "pai", "amem"
		};
		if (ordinalsAndKeywords.Contains(n1.ToLowerInvariant())) return false;

		// 情况1：前一句确实是只有 1~3 个字母的断裂残音（如 "re...", "fa..."），且后一句以其开头，并且不是独立成词的完整词
		if (n1.Length >= 2 && n1.Length <= 3 && n2.Length >= n1.Length + 3 && n2.StartsWith(n1, StringComparison.OrdinalIgnoreCase))
		{
			string[] validShortWords = { "nao", "pai", "bem", "que", "com", "por", "deu", "ceu", "mal", "ver", "dar", "ele", "ela", "nos", "vos", "sim" };
			if (!validShortWords.Contains(n1.ToLowerInvariant()))
			{
				return true;
			}
		}

		// 情况2：前后两句相似度极高（>= 78% 且两者长度相近），即同一整句话重复录了两遍（如多次重录结尾 call to action）
		if (Math.Abs(n1.Length - n2.Length) <= Math.Max(4, Math.Max(n1.Length, n2.Length) / 3))
		{
			double sim = ComputeClauseSimilarity(first, second);
			if (sim >= 0.78) return true;
		}

		return false;
	}

	public static double ComputeClauseSimilarity(string a, string b)
	{
		string na = NormalizeForCompare(a);
		string nb = NormalizeForCompare(b);
		if (string.IsNullOrEmpty(na) || string.IsNullOrEmpty(nb)) return 0.0;
		if (string.Equals(na, nb, StringComparison.OrdinalIgnoreCase)) return 1.0;

		// 1. 字符集合 Dice 系数（容忍语序微调与多字/少字）
		var setA = na.ToLowerInvariant().ToCharArray();
		var setB = nb.ToLowerInvariant().ToCharArray();
		int common = 0;
		var usedB = new bool[setB.Length];
		for (int i = 0; i < setA.Length; i++)
		{
			for (int j = 0; j < setB.Length; j++)
			{
				if (!usedB[j] && setA[i] == setB[j])
				{
					usedB[j] = true;
					common++;
					break;
				}
			}
		}
		double dice = (2.0 * common) / (setA.Length + setB.Length);

		// 2. 编辑距离相似度（Levenshtein）
		int dist = ComputeLevenshteinDistance(na.ToLowerInvariant(), nb.ToLowerInvariant());
		int maxLen = Math.Max(na.Length, nb.Length);
		double levSim = 1.0 - ((double)dist / Math.Max(1, maxLen));

		return (dice * 0.55) + (levSim * 0.45);
	}

	private static string CorrectHomophonesOnly(string spoken, string standard, double similarity = 0.0)
	{
		if (string.IsNullOrWhiteSpace(spoken) || string.IsNullOrWhiteSpace(standard))
			return spoken;

		double sim = similarity > 0.0 ? similarity : ComputeClauseSimilarity(spoken, standard);
		// 当原声识别句与标准文案高度相似（>= 0.56）且长度相近时，用标准文案纠正识别出的同音错字/拼写错误
		if (sim >= 0.56 && Math.Abs(spoken.Length - standard.Length) <= Math.Max(4, standard.Length / 4))
		{
			return standard;
		}

		// 若为外语（空格分词），对逐个相近词做拼写纠偏，保留主播多说或少说的词
		if (spoken.Contains(' ') && standard.Contains(' '))
		{
			string[] spWords = spoken.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			string[] stWords = standard.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < spWords.Length; i++)
			{
				string cleanSp = NormalizeForCompare(spWords[i]);
				if (cleanSp.Length < 3) continue;
				foreach (var stW in stWords)
				{
					string cleanSt = NormalizeForCompare(stW);
					if (cleanSt.Length < 3) continue;
					if (!string.Equals(cleanSp, cleanSt, StringComparison.OrdinalIgnoreCase))
					{
						int d = ComputeLevenshteinDistance(cleanSp, cleanSt);
						if (d == 1 || (cleanSt.Length >= 6 && d <= 2))
						{
							spWords[i] = stW.Trim(',', '.', '!', '?', ';', ':');
							break;
						}
					}
				}
			}
			return string.Join(" ", spWords);
		}

		return spoken;
	}

	private static string NormalizeForCompare(string s)
	{
		if (string.IsNullOrWhiteSpace(s)) return "";
		var sb = new StringBuilder(s.Length);
		foreach (char c in s)
		{
			if (char.IsLetterOrDigit(c) || (c >= 0x4E00 && c <= 0x9FA5))
			{
				sb.Append(char.ToLowerInvariant(c));
			}
		}
		return sb.ToString();
	}

	private static int ComputeLevenshteinDistance(string s, string t)
	{
		int n = s.Length;
		int m = t.Length;
		if (n == 0) return m;
		if (m == 0) return n;

		int[,] d = new int[n + 1, m + 1];
		for (int i = 0; i <= n; i++) d[i, 0] = i;
		for (int j = 0; j <= m; j++) d[0, j] = j;

		for (int i = 1; i <= n; i++)
		{
			for (int j = 1; j <= m; j++)
			{
				int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
				d[i, j] = Math.Min(
					Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
					d[i - 1, j - 1] + cost);
			}
		}
		return d[n, m];
	}

	/// <summary>
	/// 一键检测视频/音频中的有声说话区间（自动切除静音气口与停顿空白，采用非对称首尾呼吸缓冲与微碎切口融合，避免吞尾音与生硬跳切）
	/// </summary>
	public static async Task<List<(double StartSec, double EndSec)>> DetectSpeechIntervalsAsync(
		string mediaPath,
		string ffmpegPath,
		double totalDuration,
		double silenceThresholdDb = -36.0,
		double minSilenceDuration = 0.45,
		double breathPadSec = 0.18,
		int preDenoiseMode = 1)
	{
		var speechIntervals = new List<(double StartSec, double EndSec)>();
		if (string.IsNullOrEmpty(mediaPath) || !File.Exists(mediaPath) || string.IsNullOrEmpty(ffmpegPath) || !File.Exists(ffmpegPath))
			return speechIntervals;

		var silenceRanges = new List<(double Start, double End)>();
		string dbStr = silenceThresholdDb.ToString("0.0", CultureInfo.InvariantCulture);
		string durStr = minSilenceDuration.ToString("0.00", CultureInfo.InvariantCulture);
		// 气口检测专用纯净人声带通滤镜（不加动态压缩增益，避免把底噪放大或把句尾余音误切）
		string preDenoise = preDenoiseMode > 0
			? "highpass=f=160:poles=2,lowpass=f=3800:poles=2,afftdn=nr=28:nf=-24:tn=1,"
			: "";
		string args = $"-hide_banner -nostdin -i {TranscribeEngine.QuoteArg(mediaPath)} -af \"{preDenoise}silencedetect=noise={dbStr}dB:d={durStr}\" -f null -";

		var psi = new ProcessStartInfo
		{
			FileName = ffmpegPath,
			Arguments = args,
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			StandardErrorEncoding = Encoding.UTF8
		};

		double curSilenceStart = -1.0;
		var rxStart = new Regex(@"silence_start:\s*([0-9\.]+)", RegexOptions.Compiled);
		var rxEnd = new Regex(@"silence_end:\s*([0-9\.]+)", RegexOptions.Compiled);

		await Task.Run(() =>
		{
			using var proc = new Process { StartInfo = psi };
			proc.ErrorDataReceived += (s, e) =>
			{
				if (string.IsNullOrWhiteSpace(e.Data)) return;
				var mStart = rxStart.Match(e.Data);
				if (mStart.Success && double.TryParse(mStart.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double st))
				{
					curSilenceStart = st;
					return;
				}
				var mEnd = rxEnd.Match(e.Data);
				if (mEnd.Success && double.TryParse(mEnd.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double et))
				{
					double stVal = curSilenceStart >= 0.0 ? curSilenceStart : Math.Max(0.0, et - minSilenceDuration);
					if (et > stVal + 0.05)
					{
						lock (silenceRanges)
						{
							silenceRanges.Add((stVal, et));
						}
					}
					curSilenceStart = -1.0;
				}
			};
			proc.Start();
			proc.BeginErrorReadLine();
			proc.WaitForExit(30000);
		});

		if (silenceRanges.Count == 0)
		{
			if (totalDuration > 0.1) speechIntervals.Add((0.0, totalDuration));
			return speechIntervals;
		}

		// 非对称自然呼吸留白：词尾保留更宽余音缓冲 (postSpeechPad)，词首保留起音缓冲 (preSpeechPad)，彻底杜绝“切太狠、吞尾音、衔接生硬”
		double postSpeechPad = Math.Max(0.14, breathPadSec * 1.25); // 说话结束后的尾音自然衰减保护 (如 0.22s~0.30s)
		double preSpeechPad = Math.Max(0.12, breathPadSec * 0.90);  // 下一句开口前的吸气起音保护 (如 0.16s~0.22s)

		double cursor = 0.0;
		foreach (var sil in silenceRanges.OrderBy(x => x.Start))
		{
			double effSilStart = Math.Min(totalDuration, sil.Start + postSpeechPad);
			double effSilEnd = Math.Max(0.0, sil.End - preSpeechPad);

			// 若扣除首尾呼吸缓冲后，实际能切掉的空白不足 0.22 秒，则不切断，保持原句连贯自然过渡
			if (effSilEnd <= effSilStart + 0.22)
				continue;

			if (effSilStart > cursor + 0.18)
			{
				speechIntervals.Add((cursor, effSilStart));
			}
			cursor = Math.Max(cursor, effSilEnd);
		}

		if (totalDuration - cursor > 0.18)
		{
			speechIntervals.Add((cursor, totalDuration));
		}

		// 二次融合保护：如果相邻两个有声片段之间的被切间隔小于 0.24 秒，直接合并为一个完整片段，消除零碎跳切感
		if (speechIntervals.Count > 1)
		{
			var merged = new List<(double StartSec, double EndSec)> { speechIntervals[0] };
			for (int i = 1; i < speechIntervals.Count; i++)
			{
				var prev = merged[merged.Count - 1];
				var cur = speechIntervals[i];
				if (cur.StartSec - prev.EndSec < 0.24)
				{
					merged[merged.Count - 1] = (prev.StartSec, cur.EndSec);
				}
				else
				{
					merged.Add(cur);
				}
			}
			speechIntervals = merged;
		}

		return speechIntervals;
	}

	/// <summary>
	/// 将纯文案合成为高保真语音音频（100% 海外多语种 TTS 引擎 + 本地 SAPI 离线兜底），并同步生成精确到每句话的字幕时间轴
	/// </summary>
	public static async Task<TtsSynthesisResult> SynthesizeScriptToAudioAsync(
		string scriptText,
		int voicePresetIndex,
		double speedRatio,
		string ffmpegPath,
		Action<string> logCallback,
		CancellationToken ct)
	{
		var clauses = SplitScriptIntoClauses(scriptText);
		if (clauses.Count == 0)
			throw new InvalidOperationException("请先输入有效的配音文案内容！");

		string workDir = Path.Combine(Path.GetTempPath(), "VBS_TTS_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(workDir);

		// 判断语言与海外发音人映射
		string langCode = DetectScriptLanguageCode(scriptText, voicePresetIndex);
		var segmentWavFiles = new List<string>();
		var timedSegments = new List<SubtitleSegment>();
		double timelineCursor = 0.0;

		logCallback?.Invoke($"[AI 配音引擎] 正在将 {clauses.Count} 句文案合成为自然人声配音 (语种: {langCode})...");

		for (int i = 0; i < clauses.Count; i++)
		{
			ct.ThrowIfCancellationRequested();
			string clause = clauses[i];
			string segRawPath = Path.Combine(workDir, $"seg_{i:000}_raw.mp3");
			string segWavPath = Path.Combine(workDir, $"seg_{i:000}.wav");

			bool onlineOk = await TryFetchOverseasNeuralTtsAsync(clause, langCode, segRawPath, ct);
			if (!onlineOk || !File.Exists(segRawPath) || new FileInfo(segRawPath).Length < 200)
			{
				// 100% 离线兜底：调用 Windows 原生 SpeechSynthesizer (美国 Microsoft SAPI 离线引擎)
				segRawPath = Path.Combine(workDir, $"seg_{i:000}_sapi.wav");
				SynthesizeOfflineSapiToWav(clause, voicePresetIndex, speedRatio, segRawPath);
			}

			// 使用 FFmpeg 标准化为 44100Hz 双声道 WAV 并应用语速与音色微调
			double spd = Math.Max(0.6, Math.Min(1.8, speedRatio));
			string pitchFilter = GetVoicePresetFilter(voicePresetIndex, spd, onlineOk);
			string ffArgs = $"-hide_banner -nostdin -y -i {TranscribeEngine.QuoteArg(segRawPath)} -af \"{pitchFilter}\" -ar 44100 -ac 2 -c:a pcm_s16le {TranscribeEngine.QuoteArg(segWavPath)}";
			RunProcessSimple(ffmpegPath, ffArgs, 15000);

			if (File.Exists(segWavPath) && new FileInfo(segWavPath).Length > 400)
			{
				double segDur = ProbeAudioDurationSeconds(segWavPath, ffmpegPath);
				if (segDur <= 0.1) segDur = Math.Max(1.2, clause.Length * 0.22);

				segmentWavFiles.Add(segWavPath);
				timedSegments.Add(new SubtitleSegment
				{
					Index = i + 1,
					StartTime = TimeSpan.FromSeconds(timelineCursor),
					EndTime = TimeSpan.FromSeconds(timelineCursor + segDur),
					OriginalText = clause,
					TranslatedText = ""
				});
				timelineCursor += segDur;
			}
		}

		if (segmentWavFiles.Count == 0)
			throw new InvalidOperationException("语音合成未能生成音频文件，请检查输入文案。");

		// 拼接所有分句音频为一个完整的高清配音文件（存放于用户输出或临时持久目录，供剪辑轨道长期引用）
		string outputDir = Path.Combine(Path.GetTempPath(), "VBS_StudioVoiceovers");
		Directory.CreateDirectory(outputDir);
		string finalWav = Path.Combine(outputDir, $"AI_Voiceover_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N").Substring(0, 4)}.wav");

		string concatTxt = Path.Combine(workDir, "concat.txt");
		var sbConcat = new StringBuilder();
		foreach (var wf in segmentWavFiles)
		{
			sbConcat.AppendLine($"file '{wf.Replace("'", "'\\''")}'");
		}
		File.WriteAllText(concatTxt, sbConcat.ToString(), new UTF8Encoding(false));

		string mergeArgs = $"-hide_banner -nostdin -y -f concat -safe 0 -i {TranscribeEngine.QuoteArg(concatTxt)} -c:a pcm_s16le {TranscribeEngine.QuoteArg(finalWav)}";
		RunProcessSimple(ffmpegPath, mergeArgs, 30000);

		try { Directory.Delete(workDir, true); } catch { }

		return new TtsSynthesisResult
		{
			AudioWavPath = finalWav,
			TotalDurationSeconds = Math.Max(0.5, timelineCursor),
			Segments = timedSegments
		};
	}

	private static string DetectScriptLanguageCode(string text, int voicePresetIndex)
	{
		if (voicePresetIndex == 2) return "en-US";
		if (voicePresetIndex == 3) return "en-GB";
		if (voicePresetIndex == 4) return "es-ES";
		if (voicePresetIndex == 5) return "pt-BR";
		if (voicePresetIndex == 6) return "ja-JP";

		if (TranscribeEngine.HasChinese(text)) return "zh-CN";
		return "en-US";
	}

	private static async Task<bool> TryFetchOverseasNeuralTtsAsync(string text, string langCode, string destMp3, CancellationToken ct)
	{
		try
		{
			ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
			string url = $"https://translate.googleapis.com/translate_tts?ie=UTF-8&client=tw-ob&tl={Uri.EscapeDataString(langCode)}&q={Uri.EscapeDataString(text)}";
			using var req = new HttpRequestMessage(HttpMethod.Get, url);
			req.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/122.0.0.0 Safari/537.36");
			using var resp = await s_http.SendAsync(req, ct);
			if (resp.IsSuccessStatusCode)
			{
				byte[] bytes = await resp.Content.ReadAsByteArrayAsync();
				if (bytes != null && bytes.Length > 256)
				{
					File.WriteAllBytes(destMp3, bytes);
					return true;
				}
			}
		}
		catch { }
		return false;
	}

	private static void SynthesizeOfflineSapiToWav(string text, int voicePresetIndex, double speedRatio, string destWav)
	{
		using var synth = new SpeechSynthesizer();
		synth.SetOutputToWaveFile(destWav);
		int rate = (int)Math.Round((speedRatio - 1.0) * 6.0);
		synth.Rate = Math.Max(-5, Math.Min(6, rate));
		synth.Volume = 100;

		try
		{
			var voices = synth.GetInstalledVoices().Where(v => v.Enabled).ToList();
			bool hasZh = TranscribeEngine.HasChinese(text);
			var match = voices.FirstOrDefault(v =>
				hasZh
					? v.VoiceInfo.Culture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
					: v.VoiceInfo.Culture.Name.StartsWith("en", StringComparison.OrdinalIgnoreCase));
			if (match != null)
			{
				synth.SelectVoice(match.VoiceInfo.Name);
			}
		}
		catch { }

		synth.Speak(text);
	}

	private static string GetVoicePresetFilter(int voicePresetIndex, double speedRatio, bool isOnlineSource)
	{
		string spdStr = speedRatio.ToString("0.00", CultureInfo.InvariantCulture);
		if (voicePresetIndex == 1)
		{
			double tempoComp = Math.Max(0.5, Math.Min(2.0, speedRatio / 0.88));
			return $"asetrate=24000*0.88,aresample=44100,atempo={tempoComp.ToString("0.000", CultureInfo.InvariantCulture)},loudnorm=I=-16:TP=-1.5:LRA=11,apad=pad_dur=0.12";
		}
		return $"atempo={spdStr},loudnorm=I=-16:TP=-1.5:LRA=11,apad=pad_dur=0.12";
	}

	public static double ProbeAudioDurationSeconds(string path, string ffmpegPath)
	{
		if (string.IsNullOrEmpty(path) || !File.Exists(path)) return 0.0;
		try
		{
			var psi = new ProcessStartInfo
			{
				FileName = ffmpegPath,
				Arguments = $"-hide_banner -nostdin -i {TranscribeEngine.QuoteArg(path)}",
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardError = true,
				StandardErrorEncoding = Encoding.UTF8
			};
			using var proc = Process.Start(psi);
			string err = proc.StandardError.ReadToEnd();
			proc.WaitForExit(5000);
			var m = Regex.Match(err, @"Duration:\s*(\d{2}):(\d{2}):(\d{2})\.(\d{2})");
			if (m.Success)
			{
				int hh = int.Parse(m.Groups[1].Value);
				int mm = int.Parse(m.Groups[2].Value);
				int ss = int.Parse(m.Groups[3].Value);
				int cs = int.Parse(m.Groups[4].Value);
				return hh * 3600 + mm * 60 + ss + (cs / 100.0);
			}
		}
		catch { }
		return 0.0;
	}

	private static bool RunProcessSimple(string exe, string args, int timeoutMs)
	{
		var psi = new ProcessStartInfo
		{
			FileName = exe,
			Arguments = args,
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			StandardOutputEncoding = Encoding.UTF8,
			StandardErrorEncoding = Encoding.UTF8
		};
		using var proc = new Process { StartInfo = psi };
		proc.OutputDataReceived += (s, e) => { };
		proc.ErrorDataReceived += (s, e) => { };
		try
		{
			proc.Start();
			proc.BeginOutputReadLine();
			proc.BeginErrorReadLine();
			bool exited = proc.WaitForExit(timeoutMs);
			if (!exited)
			{
				try { proc.Kill(); } catch { }
				return false;
			}
			return proc.ExitCode == 0;
		}
		catch
		{
			try { if (!proc.HasExited) proc.Kill(); } catch { }
			return false;
		}
	}

	/// <summary>
	/// 根据降噪档位返回 FFmpeg 广播级人声隔离与多级频谱/非局部均值降噪滤镜链（移除会放大底噪的动态压缩增益，加入人声带通墙与智能噪声门限，强力滤除背景小孩哭闹声、风雨声与环境嘈杂音）：
	/// 1 = 智能人声提纯降噪 (滤除环境杂音/风雨声/远端杂音 · 推荐)
	/// 2 = 强力人声隔离 (强压背景小孩吵闹声/人群喧哗/强风雨声 · 深度净化)
	/// 3 = 室外抗狂风大雨 + 电流麦专项净化 (强切 200Hz 以下风隆声与高频雨声/电流嘶声)
	/// 4 = 轻度自然降噪 (安静室内 · 仅滤除空调风扇底噪，保留原声细节)
	/// </summary>
	public static string GetDenoiseFilterChain(int denoiseMode)
	{
		return denoiseMode switch
		{
			2 => "highpass=f=185:poles=2,highpass=f=175:poles=2,lowpass=f=3400:poles=2,lowpass=f=3500:poles=2,afftdn=nr=55:nf=-20:tn=1,afftdn=nr=30:nf=-24:tn=1,anlmdn=s=7.0:p=0.004:r=0.015:m=15,agate=threshold=0.045:ratio=8.0:range=0.004:attack=6:release=140:knee=2.8,equalizer=f=2000:t=q:w=1.1:g=4",
			3 => "highpass=f=200:poles=2,highpass=f=190:poles=2,lowpass=f=3600:poles=2,adeclick,afftdn=nr=48:nf=-20:tn=1,anlmdn=s=6.0:p=0.004:r=0.015:m=15,agate=threshold=0.035:ratio=6.0:range=0.006:attack=8:release=160:knee=2.5,equalizer=f=2200:t=q:w=1.2:g=3.5",
			4 => "highpass=f=120:poles=2,lowpass=f=6500:poles=2,afftdn=nr=24:nf=-28:tn=1,agate=threshold=0.015:ratio=2.5:range=0.04:attack=10:release=220",
			_ => "highpass=f=165:poles=2,lowpass=f=3800:poles=2,afftdn=nr=38:nf=-22:tn=1,anlmdn=s=4.5:p=0.004:r=0.012:m=15,agate=threshold=0.025:ratio=4.5:range=0.01:attack=8:release=180:knee=2.5,equalizer=f=2100:t=q:w=1.2:g=3"
		};
	}

	/// <summary>
	/// 一键对当前素材执行环境噪音消除与人声净化，输出可实时在剪辑轨道上试听的高清降噪媒体文件（视频保留原画流秒级复制，仅重编码净化音轨）
	/// </summary>
	public static async Task<string> DenoiseMediaInPlaceAsync(
		string sourceMediaPath,
		string ffmpegPath,
		int denoiseMode,
		bool loudnorm)
	{
		if (string.IsNullOrEmpty(sourceMediaPath) || !File.Exists(sourceMediaPath) || string.IsNullOrEmpty(ffmpegPath) || !File.Exists(ffmpegPath))
			return null;

		string ext = Path.GetExtension(sourceMediaPath) ?? ".mp4";
		bool isAudioOnly = ext.Equals(".wav", StringComparison.OrdinalIgnoreCase) ||
		                   ext.Equals(".mp3", StringComparison.OrdinalIgnoreCase) ||
		                   ext.Equals(".m4a", StringComparison.OrdinalIgnoreCase) ||
		                   ext.Equals(".aac", StringComparison.OrdinalIgnoreCase) ||
		                   ext.Equals(".flac", StringComparison.OrdinalIgnoreCase);

		string baseDir = Path.GetDirectoryName(sourceMediaPath) ?? Path.GetTempPath();
		string baseName = Path.GetFileNameWithoutExtension(sourceMediaPath);
		if (baseName.Contains("_降噪净化_"))
		{
			baseName = baseName.Substring(0, baseName.IndexOf("_降噪净化_", StringComparison.Ordinal));
		}
		string outExt = isAudioOnly ? ".wav" : ".mp4";
		string outPath = Path.Combine(baseDir, $"{baseName}_降噪净化_{DateTime.Now:HHmmss}{outExt}");

		int effMode = denoiseMode <= 0 ? 1 : denoiseMode;
		string af = GetDenoiseFilterChain(effMode);
		if (loudnorm)
		{
			// 若开启响度平衡，在 loudnorm 之后再加一道轻柔噪声门，防止 loudnorm 把停顿间隙的微弱残余底噪抬高
			af += ",loudnorm=I=-16:TP=-1.5:LRA=11,agate=threshold=0.028:ratio=4.0:range=0.01:attack=8:release=160";
		}

		string args = isAudioOnly
			? $"-hide_banner -nostdin -y -i {TranscribeEngine.QuoteArg(sourceMediaPath)} -af \"{af}\" -ar 44100 -ac 2 -c:a pcm_s16le {TranscribeEngine.QuoteArg(outPath)}"
			: $"-hide_banner -nostdin -y -i {TranscribeEngine.QuoteArg(sourceMediaPath)} -c:v copy -af \"{af}\" -c:a aac -b:a 192k -movflags +faststart {TranscribeEngine.QuoteArg(outPath)}";

		bool ok = await Task.Run(() => RunProcessSimple(ffmpegPath, args, 120000));

		if (!ok || !File.Exists(outPath) || new FileInfo(outPath).Length <= 512)
		{
			try { if (File.Exists(outPath)) File.Delete(outPath); } catch { }

			// 兼容性兜底：若极个别旧版 FFmpeg 不支持 anlmdn，自动回退为纯高阶带通 + 双级 afftdn + agate
			string fallbackAf = "highpass=f=180:poles=2,lowpass=f=3500:poles=2,afftdn=nr=50:nf=-20:tn=1,agate=threshold=0.035:ratio=6.0:range=0.008:attack=8:release=150";
			string fbArgs = isAudioOnly
				? $"-hide_banner -nostdin -y -i {TranscribeEngine.QuoteArg(sourceMediaPath)} -af \"{fallbackAf}\" -ar 44100 -ac 2 -c:a pcm_s16le {TranscribeEngine.QuoteArg(outPath)}"
				: $"-hide_banner -nostdin -y -i {TranscribeEngine.QuoteArg(sourceMediaPath)} -c:v copy -af \"{fallbackAf}\" -c:a aac -b:a 192k -movflags +faststart {TranscribeEngine.QuoteArg(outPath)}";
			ok = await Task.Run(() => RunProcessSimple(ffmpegPath, fbArgs, 120000));
		}

		if (ok && File.Exists(outPath) && new FileInfo(outPath).Length > 512)
		{
			return outPath;
		}

		try { if (File.Exists(outPath)) File.Delete(outPath); } catch { }
		return null;
	}

	/// <summary>
	/// 构建欧美广播级人声净化（Mozilla/Xiph 频谱降噪 + EBU R128 响度平衡 + 趣味变声）FFmpeg 音频滤镜链
	/// </summary>
	public static string BuildAudioEnhancementFilter(bool denoise, bool loudnorm, int voiceFxMode, int denoiseMode = 1)
	{
		var filters = new List<string>();

		if (denoise && denoiseMode > 0)
		{
			filters.Add(GetDenoiseFilterChain(denoiseMode));
		}

		switch (voiceFxMode)
		{
			case 1: // 🎙️ 磁性深沉男声
				filters.Add("asetrate=44100*0.86,aresample=44100,atempo=1.1628");
				break;
			case 2: // 🌸 清脆活力女声
				filters.Add("asetrate=44100*1.18,aresample=44100,atempo=0.8475");
				break;
			case 3: // 🐿️ 趣味花栗鼠/卡通声
				filters.Add("asetrate=44100*1.36,aresample=44100,atempo=0.7353");
				break;
			case 4: // 🤖 赛博机械战警音
				filters.Add("aecho=0.8:0.88:12:0.6,flanger=delay=4:depth=4:speed=2");
				break;
			case 5: // 📞 复古电话电台音
				filters.Add("highpass=f=450,lowpass=f=3200,acrusher=level_in=2:level_out=1:bits=10:mode=log:aa=1");
				break;
		}

		if (loudnorm)
		{
			filters.Add("loudnorm=I=-16:TP=-1.5:LRA=11");
		}

		return filters.Count > 0 ? "," + string.Join(",", filters) : "";
	}

	/// <summary>
	/// 构建剪映同款实用视觉特效、电影调色与背景抠除 FFmpeg 视频滤镜链
	/// </summary>
	public static string BuildVisualEffectAndColorFilter(int visualEffectMode, int colorGradeMode, int cutoutMode, int targetW, int targetH)
	{
		var filters = new List<string>();

		// 1. 画面特效 (Visual Effects)
		switch (visualEffectMode)
		{
			case 1: // 🔍 镜头中心聚焦推进 (防呆板微推镜)
				filters.Add($"crop=iw*0.90:ih*0.90:(iw-ow)/2:(ih-oh)/2,scale={targetW}:{targetH}:flags=lanczos");
				break;
			case 2: // 💥 开场高燃震动冲击 (前 1.2 秒物理镜头震动)
				filters.Add($"crop=iw-16:ih-16:8+7*sin(48*t)*exp(-2.2*t):8+7*cos(40*t)*exp(-2.2*t),scale={targetW}:{targetH}");
				break;
			case 3: // ⚡ 赛博 RGB 故障色差 (抖音风高能炫光色差)
				filters.Add("rgbashift=rh=-5:bh=5:rv=2:bv=-2");
				break;
			case 4: // ✨ 柔光电影暗角 (四周暗角聚焦 + 柔光对比)
				filters.Add("vignette=PI/4.5,eq=contrast=1.06:brightness=0.02");
				break;
			case 5: // 🎞️ 复古胶片颗粒 (Vintage Film Grain)
				filters.Add("noise=alls=9:allf=t+u,eq=saturation=0.86:contrast=1.07");
				break;
			case 6: // 💎 超清画质锐化增强 (边缘细节强化)
				filters.Add("unsharp=5:5:1.15:5:5:0.0");
				break;
		}

		// 2. 电影级调色滤镜 (Color Grading)
		switch (colorGradeMode)
		{
			case 1: // 🎬 好莱坞冷暖青橙 (Teal & Orange)
				filters.Add("colorbalance=rs=-0.08:gs=-0.02:bs=0.10:rh=0.12:gh=0.03:bh=-0.10,eq=contrast=1.08:saturation=1.14");
				break;
			case 2: // ☀️ 明亮通透高饱和 (短视频爆款鲜亮)
				filters.Add("eq=brightness=0.04:contrast=1.06:saturation=1.26:gamma=1.05");
				break;
			case 3: // ☕ 日系清新暖阳 (Warm Film)
				filters.Add("colorbalance=rm=0.07:gm=0.03:bm=-0.06,eq=brightness=0.03:saturation=0.92");
				break;
			case 4: // ❄️ 北欧冷冽高级灰 (Nordic Cold)
				filters.Add("colorbalance=rs=-0.05:bs=0.09:rm=-0.04:bm=0.06,eq=saturation=0.78:contrast=1.10");
				break;
			case 5: // 🖤 经典高反差黑白 (Noir B&W)
				filters.Add("hue=s=0,eq=contrast=1.22:brightness=-0.02");
				break;
		}

		// 3. 智能抠像去背景 (Smart Background Key / Cutout)
		switch (cutoutMode)
		{
			case 1: // 🟢 绿幕背景一键抠除
				filters.Add("chromakey=0x00FF00:0.18:0.08,despill=type=green");
				break;
			case 2: // 🔵 蓝幕背景一键抠除
				filters.Add("chromakey=0x0055FF:0.18:0.08,despill=type=blue");
				break;
			case 3: // ⚪ 纯白/浅色背景人像抠除
				filters.Add("lumakey=threshold=0.88:tolerance=0.08:softness=0.05");
				break;
			case 4: // ⚫ 纯黑背景抠除
				filters.Add("lumakey=threshold=0.08:tolerance=0.06:softness=0.04");
				break;
		}

		return filters.Count > 0 ? "," + string.Join(",", filters) : "";
	}

	/// <summary>
	/// 导出 ASS 动态逐词花字字幕文件（支持：1=逐词单蹦、2=随音逐词打字机、3=卡拉OK逐词变色高亮）
	/// </summary>
	public static string ExportDynamicAssSubtitles(
		IEnumerable<SubtitleSegment> segments,
		int wordAnimMode,
		bool includeOriginal,
		bool includeTranslated)
	{
		var sb = new StringBuilder();
		sb.AppendLine("[Script Info]");
		sb.AppendLine("Title: VideoBatchStudio Dynamic Word-by-Word Subtitles");
		sb.AppendLine("ScriptType: v4.00+");
		sb.AppendLine("PlayResX: 1920");
		sb.AppendLine("PlayResY: 1080");
		sb.AppendLine("WrapStyle: 0");
		sb.AppendLine("ScaledBorderAndShadow: yes");
		sb.AppendLine();
		sb.AppendLine("[V4+ Styles]");
		sb.AppendLine("Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding");
		sb.AppendLine("Style: Default,Microsoft YaHei UI,64,&H0047E0FD,&H00FFFFFF,&H000E0A0A,&H96000000,-1,0,0,0,100,100,1,0,1,5,3,2,40,40,95,1");
		sb.AppendLine();
		sb.AppendLine("[Events]");
		sb.AppendLine("Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text");

		foreach (var seg in segments)
		{
			double startSec = seg.StartTime.TotalSeconds;
			double dur = Math.Max(0.1, (seg.EndTime - seg.StartTime).TotalSeconds);
			string mainText = (includeOriginal && !string.IsNullOrWhiteSpace(seg.OriginalText))
				? seg.OriginalText.Trim()
				: (seg.TranslatedText?.Trim() ?? "");
			string subText = (includeOriginal && includeTranslated && !string.IsNullOrWhiteSpace(seg.TranslatedText))
				? seg.TranslatedText.Trim()
				: "";

			if (string.IsNullOrEmpty(mainText)) continue;

			if (wordAnimMode == 1)
			{
				// 🔥 逐词单蹦：每个单词独立一条 Dialogue，带微弹入场动画
				var words = SplitIntoTimedWords(mainText, startSec, dur);
				foreach (var w in words)
				{
					string st = FormatAssTime(w.StartSec);
					string et = FormatAssTime(w.EndSec);
					string line = $"{{\\fscx118\\fscy118\\t(0,90,\\fscx100\\fscy100)}}{EscapeAss(w.Word)}";
					if (!string.IsNullOrEmpty(subText))
					{
						line += $"\\N{{\\fs38\\c&H00F9F5F1&}}{EscapeAss(subText)}";
					}
					sb.AppendLine($"Dialogue: 0,{st},{et},Default,,0,0,0,,{line}");
				}
			}
			else if (wordAnimMode == 2)
			{
				// ⌨️ 随音逐词追加（打字机跟读风）
				var words = SplitIntoTimedWords(mainText, startSec, dur);
				for (int i = 0; i < words.Count; i++)
				{
					string st = FormatAssTime(words[i].StartSec);
					string et = FormatAssTime(words[i].EndSec);
					bool hasSpaces = mainText.Contains(' ');
					string prefix = string.Join(hasSpaces ? " " : "", words.Take(i + 1).Select(x => x.Word));
					string line = EscapeAss(prefix);
					if (!string.IsNullOrEmpty(subText))
					{
						line += $"\\N{{\\fs38\\c&H00F9F5F1&}}{EscapeAss(subText)}";
					}
					sb.AppendLine($"Dialogue: 0,{st},{et},Default,,0,0,0,,{line}");
				}
			}
			else if (wordAnimMode == 3)
			{
				// 🎤 逐词变色高亮（卡拉OK式读到哪亮到哪）
				var words = SplitIntoTimedWords(mainText, startSec, dur);
				string st = FormatAssTime(startSec);
				string et = FormatAssTime(startSec + dur);
				bool hasSpaces = mainText.Contains(' ');
				var kLine = new StringBuilder();
				for (int i = 0; i < words.Count; i++)
				{
					int cs = Math.Max(5, (int)Math.Round(words[i].Duration * 100));
					kLine.Append($"{{\\kf{cs}}}{EscapeAss(words[i].Word)}");
					if (hasSpaces && i < words.Count - 1) kLine.Append(" ");
				}
				if (!string.IsNullOrEmpty(subText))
				{
					kLine.Append($"\\N{{\\fs38\\c&H00F9F5F1&}}{EscapeAss(subText)}");
				}
				sb.AppendLine($"Dialogue: 0,{st},{et},Default,,0,0,0,,{kLine}");
			}
			else
			{
				// 整句标准呈现
				string st = FormatAssTime(startSec);
				string et = FormatAssTime(startSec + dur);
				string line = EscapeAss(mainText);
				if (!string.IsNullOrEmpty(subText))
				{
					line += $"\\N{{\\fs38\\c&H00F9F5F1&}}{EscapeAss(subText)}";
				}
				sb.AppendLine($"Dialogue: 0,{st},{et},Default,,0,0,0,,{line}");
			}
		}

		return sb.ToString();
	}

	private static string FormatAssTime(double sec)
	{
		if (sec < 0) sec = 0;
		TimeSpan ts = TimeSpan.FromSeconds(sec);
		return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}.{3:00}",
			(int)ts.TotalHours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
	}

	private static string EscapeAss(string s)
	{
		if (string.IsNullOrEmpty(s)) return "";
		return s.Replace("{", "｛").Replace("}", "｝").Replace("\r\n", "\\N").Replace("\n", "\\N");
	}
}
