using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using VideoBatchMerger.Transcriber;

namespace VideoBatchMerger;

/// <summary>质检风险等级</summary>
internal enum QcRisk
{
	Safe = 0,
	Review = 1,
	High = 2
}

/// <summary>时间线上保留的一段原片区间（只针对质检主素材）</summary>
internal sealed class QcKeptPiece
{
	public double SrcStart { get; set; }
	public double SrcEnd { get; set; }
	public double TimelineStart { get; set; }
}

internal sealed class QcWord
{
	public string Text { get; set; } = "";
	public string Norm { get; set; } = "";
	public double Start { get; set; }
	public double End { get; set; }
	public bool Kept { get; set; }
	public double TimelinePos { get; set; } = -1;
}

/// <summary>一处被剪掉的原片区间及其诊断</summary>
internal sealed class QcCutItem
{
	public int Index { get; set; }
	public double SrcStart { get; set; }
	public double SrcEnd { get; set; }
	public double Duration => Math.Max(0.0, SrcEnd - SrcStart);
	/// <summary>该剪切点在成片时间线上的位置（接缝处）</summary>
	public double TimelineSeam { get; set; }
	public string Text { get; set; } = "";
	public List<string> Words { get; set; } = new List<string>();
	public List<string> ContentWords { get; set; } = new List<string>();
	public string Category { get; set; } = "";
	public string Reason { get; set; } = "";
	public QcRisk Risk { get; set; }
	public List<string> LostScriptWords { get; set; } = new List<string>();
	public List<string> ClippedWords { get; set; } = new List<string>();
	public string LiteralZh { get; set; } = "";
	public string WordGlossZh { get; set; } = "";
}

/// <summary>标准文案中的一句及其在成片中的保留情况</summary>
internal sealed class QcScriptSentence
{
	public int Index { get; set; }
	public string Text { get; set; } = "";
	public List<string> DisplayTokens { get; set; } = new List<string>();
	/// <summary>0 = 成片中保留; 1 = 主播念了但被剪掉; 2 = 主播未念/未识别到</summary>
	public List<int> TokenState { get; set; } = new List<int>();
	public double KeptCoverage { get; set; }
	public double SpokenCoverage { get; set; }
	public string Status { get; set; } = "";
	public QcRisk Risk { get; set; }
	public string LiteralZh { get; set; } = "";
	public List<string> CutWords { get; set; } = new List<string>();
	public List<string> UnspokenWords { get; set; } = new List<string>();
}

/// <summary>原声识别的一句（原片顺序），带每个词是否保留</summary>
internal sealed class QcTranscriptSentence
{
	public double SrcStart { get; set; }
	public double SrcEnd { get; set; }
	public string Text { get; set; } = "";
	public List<QcWord> Words { get; set; } = new List<QcWord>();
	public bool AnyCut => Words.Any(w => !w.Kept);
	public bool AllCut => Words.Count > 0 && Words.All(w => !w.Kept);
	public string LiteralZh { get; set; } = "";
}

internal sealed class QcInput
{
	public List<SubtitleSegment> Segments { get; set; } = new List<SubtitleSegment>();
	public List<QcKeptPiece> Kept { get; set; } = new List<QcKeptPiece>();
	public double SourceDuration { get; set; }
	public string Script { get; set; } = "";
	public List<(double StartSec, double EndSec, string Reason)> BadTakes { get; set; } = new List<(double, double, string)>();
}

internal sealed class QcReport
{
	public DateTime CreatedAt { get; set; } = DateTime.Now;
	public bool HasScript { get; set; }
	public double Score { get; set; }
	public string Verdict { get; set; } = "";
	public QcRisk VerdictLevel { get; set; }
	public double ScriptRecall { get; set; }
	public double SpokenRecall { get; set; }
	public double RetentionRate { get; set; }
	public int ScriptTokenCount { get; set; }
	public int KeptMatchedTokenCount { get; set; }
	public int SpokenMatchedTokenCount { get; set; }
	public int ExtraKeptTokenCount { get; set; }
	public double SourceDuration { get; set; }
	public double KeptDuration { get; set; }
	public double CutDuration { get; set; }
	public int SilenceCount { get; set; }
	public int FillerCount { get; set; }
	public int RetakeCount { get; set; }
	public int SpeechCutCount { get; set; }
	public int HighCount { get; set; }
	public int ReviewCount { get; set; }
	public int ClippedCount { get; set; }
	public int ScriptSentenceOk { get; set; }
	public int ScriptSentencePartial { get; set; }
	public int ScriptSentenceCut { get; set; }
	public int ScriptSentenceUnspoken { get; set; }
	public string Summary { get; set; } = "";
	public List<QcCutItem> CutItems { get; set; } = new List<QcCutItem>();
	public List<QcScriptSentence> ScriptSentences { get; set; } = new List<QcScriptSentence>();
	public List<QcTranscriptSentence> Transcript { get; set; } = new List<QcTranscriptSentence>();
}

/// <summary>
/// 成片质检引擎（纯计算、无 UI、无网络）：
/// 1) 依据时间线保留区间反推出所有被剪掉的原片区间，并结合逐词识别结果判断“剪掉了什么、为什么剪”；
/// 2) 与标准文案逐词做 LCS 对齐，计算文案契合度并逐句判定“完整保留 / 部分缺失 / 念了却被剪 / 主播未念”；
/// 3) 汇总评分与合格结论。
/// </summary>
internal static class QcEngine
{
	private static readonly HashSet<string> s_fillers = new HashSet<string>(StringComparer.Ordinal)
	{
		// 葡语/西语/通用语气词与口头禅
		"hum", "humm", "hmm", "hm", "hmmm", "ahn", "ah", "aah", "aham", "uh", "uhm", "um", "umm", "er", "erm", "eh", "ehh", "ee", "eee",
		"aa", "aaa", "oh", "tipo", "ne", "sabe", "entao", "pois", "olha", "bom", "assim", "pronto", "enfim", "tá", "ta", "pa", "pah",
		// 英语
		"like", "so", "well", "yeah", "uhh", "mm", "mmm",
		// 中文
		"嗯", "啊", "呃", "哦", "额", "那个", "就是", "然后", "这个"
	};

	private static readonly Dictionary<string, string> s_fillerGloss = new Dictionary<string, string>(StringComparer.Ordinal)
	{
		{ "hum", "嗯" }, { "humm", "嗯" }, { "hmm", "嗯" }, { "hm", "嗯" }, { "hmmm", "嗯" }, { "mm", "嗯" }, { "mmm", "嗯" },
		{ "ahn", "啊" }, { "ah", "啊" }, { "aah", "啊" }, { "aa", "啊" }, { "aaa", "啊" }, { "aham", "嗯哼" },
		{ "uh", "呃" }, { "uhh", "呃" }, { "uhm", "呃" }, { "um", "呃" }, { "umm", "呃" }, { "er", "呃" }, { "erm", "呃" }, { "eh", "呃" }, { "ehh", "呃" },
		{ "ee", "诶" }, { "eee", "诶" }, { "oh", "哦" }, { "tipo", "那种/像是" }, { "ne", "对吧" }, { "sabe", "你知道" }, { "entao", "那么" },
		{ "pois", "嗯/是的" }, { "olha", "你看" }, { "bom", "好" }, { "assim", "这样" }, { "pronto", "好了" }, { "enfim", "总之" },
		{ "ta", "好/行" }, { "tá", "好/行" }, { "pa", "嘛" }, { "pah", "嘛" }, { "like", "就像" }, { "so", "所以" }, { "well", "嗯" }, { "yeah", "是啊" }
	};

	public static bool IsFiller(string norm) => !string.IsNullOrEmpty(norm) && s_fillers.Contains(norm);

	public static string FillerGloss(string norm)
	{
		if (string.IsNullOrEmpty(norm)) return null;
		return s_fillerGloss.TryGetValue(norm, out var g) ? g : null;
	}

	/// <summary>归一化：小写、去重音符号、仅保留字母数字与汉字</summary>
	public static string Norm(string s)
	{
		if (string.IsNullOrWhiteSpace(s)) return "";
		string d = s.Normalize(NormalizationForm.FormD);
		var sb = new StringBuilder(d.Length);
		foreach (char c in d)
		{
			if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
			if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
		}
		return sb.ToString().Normalize(NormalizationForm.FormC);
	}

	private static bool HasCjk(string s)
	{
		foreach (char c in s ?? "")
		{
			if (c >= 0x4E00 && c <= 0x9FFF) return true;
			if (c >= 0x3040 && c <= 0x30FF) return true;
		}
		return false;
	}

	/// <summary>分词：外语按空格；中日文按单字（便于逐字对齐）</summary>
	public static List<string> Tokenize(string text)
	{
		var list = new List<string>();
		if (string.IsNullOrWhiteSpace(text)) return list;
		string clean = text.Replace("\r", " ").Replace("\n", " ").Trim();
		if (HasCjk(clean) && clean.IndexOf(' ') < 0)
		{
			foreach (char c in clean)
			{
				string n = Norm(c.ToString());
				if (n.Length > 0) list.Add(c.ToString());
			}
			return list;
		}
		foreach (string p in clean.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
		{
			if (HasCjk(p))
			{
				foreach (char c in p)
				{
					if (Norm(c.ToString()).Length > 0) list.Add(c.ToString());
				}
			}
			else if (Norm(p).Length > 0)
			{
				list.Add(p);
			}
		}
		return list;
	}

	private static int Lev(string s, string t)
	{
		int n = s.Length, m = t.Length;
		if (n == 0) return m;
		if (m == 0) return n;
		var prev = new int[m + 1];
		var cur = new int[m + 1];
		for (int j = 0; j <= m; j++) prev[j] = j;
		for (int i = 1; i <= n; i++)
		{
			cur[0] = i;
			for (int j = 1; j <= m; j++)
			{
				int cost = s[i - 1] == t[j - 1] ? 0 : 1;
				cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
			}
			var tmp = prev; prev = cur; cur = tmp;
		}
		return prev[m];
	}

	/// <summary>容错词匹配（容忍识别拼写偏差 / 口音导致的 1~2 字母差异）</summary>
	public static bool WordEq(string a, string b)
	{
		if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
		if (a == b) return true;
		int len = Math.Max(a.Length, b.Length);
		if (len < 4) return false;
		if (Math.Abs(a.Length - b.Length) > 2) return false;
		int d = Lev(a, b);
		return d <= 1 || (len >= 7 && d <= 2);
	}

	/// <summary>LCS 对齐，返回 a 中每个元素是否匹配到 b</summary>
	public static bool[] AlignLcs(IList<string> a, IList<string> b)
	{
		int n = a.Count, m = b.Count;
		var matched = new bool[n];
		if (n == 0 || m == 0) return matched;

		long cells = (long)(n + 1) * (m + 1);
		if (cells > 30_000_000L)
		{
			// 超大文本兜底：贪心顺序匹配（带前瞻窗口）
			int j = 0;
			for (int i = 0; i < n && j < m; i++)
			{
				for (int k = j; k < Math.Min(m, j + 40); k++)
				{
					if (WordEq(a[i], b[k])) { matched[i] = true; j = k + 1; break; }
				}
			}
			return matched;
		}

		var eq = new Dictionary<long, bool>();
		var len = new int[n + 1, m + 1];
		for (int i = n - 1; i >= 0; i--)
		{
			for (int j = m - 1; j >= 0; j--)
			{
				if (WordEq(a[i], b[j])) len[i, j] = len[i + 1, j + 1] + 1;
				else len[i, j] = Math.Max(len[i + 1, j], len[i, j + 1]);
			}
		}
		int x = 0, y = 0;
		while (x < n && y < m)
		{
			if (WordEq(a[x], b[y])) { matched[x] = true; x++; y++; }
			else if (len[x + 1, y] >= len[x, y + 1]) x++;
			else y++;
		}
		return matched;
	}

	private static List<QcKeptPiece> MergeKept(List<QcKeptPiece> kept)
	{
		var sorted = (kept ?? new List<QcKeptPiece>())
			.Where(k => k != null && k.SrcEnd - k.SrcStart > 0.005)
			.OrderBy(k => k.SrcStart).ToList();
		var merged = new List<QcKeptPiece>();
		foreach (var k in sorted)
		{
			if (merged.Count > 0 && k.SrcStart <= merged[merged.Count - 1].SrcEnd + 0.04)
			{
				var last = merged[merged.Count - 1];
				last.SrcEnd = Math.Max(last.SrcEnd, k.SrcEnd);
			}
			else
			{
				merged.Add(new QcKeptPiece { SrcStart = k.SrcStart, SrcEnd = k.SrcEnd, TimelineStart = k.TimelineStart });
			}
		}
		return merged;
	}

	private static double Overlap(double a1, double a2, double b1, double b2) => Math.Max(0.0, Math.Min(a2, b2) - Math.Max(a1, b1));

	private static double KeptOverlap(List<QcKeptPiece> merged, double s, double e)
	{
		double sum = 0.0;
		foreach (var k in merged)
		{
			if (k.SrcStart >= e) break;
			sum += Overlap(s, e, k.SrcStart, k.SrcEnd);
		}
		return sum;
	}

	public static string FormatTime(double sec)
	{
		if (sec < 0) sec = 0;
		int m = (int)(sec / 60.0);
		double s = sec - m * 60.0;
		return $"{m:00}:{s.ToString("00.0", CultureInfo.InvariantCulture)}";
	}

	public static QcReport Analyze(QcInput input)
	{
		var report = new QcReport();
		if (input == null) return report;

		var rawKept = (input.Kept ?? new List<QcKeptPiece>()).Where(k => k.SrcEnd - k.SrcStart > 0.005).ToList();
		var merged = MergeKept(rawKept);

		// ---------- 1. 展开逐词 ----------
		var allWords = new List<QcWord>();
		foreach (var seg in (input.Segments ?? new List<SubtitleSegment>()).OrderBy(s => s.StartTime))
		{
			if (seg == null) continue;
			var sentence = new QcTranscriptSentence
			{
				SrcStart = seg.StartTime.TotalSeconds,
				SrcEnd = seg.EndTime.TotalSeconds,
				Text = (seg.OriginalText ?? "").Trim()
			};
			var words = new List<QcWord>();
			if (seg.Words != null && seg.Words.Count > 0)
			{
				foreach (var w in seg.Words)
				{
					string t = (w?.Text ?? "").Trim();
					if (t.Length == 0) continue;
					// 中文等无空格语种：Whisper 的 token 可能是多字，拆为单字便于对齐
					if (HasCjk(t) && t.Length > 1)
					{
						var chars = t.Where(c => Norm(c.ToString()).Length > 0).ToList();
						double span = Math.Max(0.02, w.EndSec - w.StartSec) / Math.Max(1, chars.Count);
						for (int ci = 0; ci < chars.Count; ci++)
						{
							words.Add(new QcWord { Text = chars[ci].ToString(), Norm = Norm(chars[ci].ToString()), Start = w.StartSec + ci * span, End = w.StartSec + (ci + 1) * span });
						}
						continue;
					}
					string n = Norm(t);
					if (n.Length == 0) continue;
					words.Add(new QcWord { Text = t, Norm = n, Start = w.StartSec, End = Math.Max(w.StartSec + 0.02, w.EndSec) });
				}
			}
			if (words.Count == 0 && !string.IsNullOrWhiteSpace(seg.OriginalText))
			{
				var toks = Tokenize(seg.OriginalText);
				double s0 = seg.StartTime.TotalSeconds;
				double span = Math.Max(0.05, seg.EndTime.TotalSeconds - s0) / Math.Max(1, toks.Count);
				for (int ti = 0; ti < toks.Count; ti++)
				{
					words.Add(new QcWord { Text = toks[ti], Norm = Norm(toks[ti]), Start = s0 + ti * span, End = s0 + (ti + 1) * span });
				}
			}
			sentence.Words = words;
			if (words.Count > 0) report.Transcript.Add(sentence);
			allWords.AddRange(words);
		}

		double srcDur = Math.Max(input.SourceDuration, allWords.Count > 0 ? allWords.Max(w => w.End) : 0.0);
		if (merged.Count > 0) srcDur = Math.Max(srcDur, merged[merged.Count - 1].SrcEnd);
		report.SourceDuration = srcDur;
		report.KeptDuration = merged.Sum(k => k.SrcEnd - k.SrcStart);

		// 每个词是否保留（≥50% 时长落在保留区间内） + 成片时间位置
		foreach (var w in allWords)
		{
			double dur = Math.Max(0.02, w.End - w.Start);
			double ov = KeptOverlap(merged, w.Start, w.End);
			w.Kept = ov >= dur * 0.5;
			if (w.Kept)
			{
				double mid = (w.Start + w.End) / 2.0;
				var piece = rawKept.FirstOrDefault(k => mid >= k.SrcStart - 0.02 && mid <= k.SrcEnd + 0.02)
					?? rawKept.OrderBy(k => Math.Min(Math.Abs(k.SrcStart - mid), Math.Abs(k.SrcEnd - mid))).FirstOrDefault();
				if (piece != null) w.TimelinePos = piece.TimelineStart + Math.Max(0.0, mid - piece.SrcStart);
			}
		}

		var keptWordsTimeline = allWords.Where(w => w.Kept).OrderBy(w => w.TimelinePos).ThenBy(w => w.Start).ToList();
		var keptNorms = keptWordsTimeline.Select(w => w.Norm).ToList();
		var allNorms = allWords.Select(w => w.Norm).ToList();

		// ---------- 2. 标准文案对齐 ----------
		string script = (input.Script ?? "").Trim();
		report.HasScript = script.Length > 0;
		var scriptDeficit = new Dictionary<string, int>(StringComparer.Ordinal);
		var scriptVocab = new HashSet<string>(StringComparer.Ordinal);
		if (report.HasScript)
		{
			var clauses = SmartCutEngine.SplitScriptIntoClauses(script);
			var flatNorm = new List<string>();
			var owner = new List<(int sentence, int token)>();
			for (int ci = 0; ci < clauses.Count; ci++)
			{
				var ss = new QcScriptSentence { Index = ci + 1, Text = clauses[ci] };
				foreach (var tk in Tokenize(clauses[ci]))
				{
					string n = Norm(tk);
					if (n.Length == 0) continue;
					ss.DisplayTokens.Add(tk);
					ss.TokenState.Add(0);
					flatNorm.Add(n);
					owner.Add((ci, ss.DisplayTokens.Count - 1));
					scriptVocab.Add(n);
				}
				report.ScriptSentences.Add(ss);
			}

			bool[] inKept = AlignLcs(flatNorm, keptNorms);
			bool[] inAll = AlignLcs(flatNorm, allNorms);
			bool[] keptUsed = AlignLcs(keptNorms, flatNorm);
			report.ScriptTokenCount = flatNorm.Count;
			report.KeptMatchedTokenCount = inKept.Count(b => b);
			report.SpokenMatchedTokenCount = inAll.Count(b => b);
			report.ExtraKeptTokenCount = keptUsed.Count(b => !b);

			for (int i = 0; i < flatNorm.Count; i++)
			{
				var (si, ti) = owner[i];
				var ss = report.ScriptSentences[si];
				int state = inKept[i] ? 0 : (inAll[i] ? 1 : 2);
				ss.TokenState[ti] = state;
				if (state == 1) ss.CutWords.Add(ss.DisplayTokens[ti]);
				else if (state == 2) ss.UnspokenWords.Add(ss.DisplayTokens[ti]);
			}

			foreach (var ss in report.ScriptSentences)
			{
				int total = Math.Max(1, ss.TokenState.Count);
				ss.KeptCoverage = ss.TokenState.Count(s => s == 0) / (double)total;
				ss.SpokenCoverage = ss.TokenState.Count(s => s != 2) / (double)total;
				var cutContent = ss.CutWords.Where(w => !IsFiller(Norm(w))).ToList();
				bool cutHasDigit = cutContent.Any(w => w.Any(char.IsDigit));
				if (ss.TokenState.Count == 0)
				{
					ss.Status = "✔ 完整保留"; ss.Risk = QcRisk.Safe; report.ScriptSentenceOk++;
				}
				else if (ss.KeptCoverage >= 0.85 && !(cutHasDigit))
				{
					ss.Status = "✔ 完整保留"; ss.Risk = QcRisk.Safe; report.ScriptSentenceOk++;
				}
				else if (ss.KeptCoverage >= 0.5)
				{
					ss.Status = cutContent.Count > 0 ? "◐ 部分缺失（有词被剪）" : "◐ 部分缺失（主播少念/未识别）";
					ss.Risk = (cutContent.Count >= 2 || cutHasDigit) ? QcRisk.High : QcRisk.Review;
					report.ScriptSentencePartial++;
				}
				else if (ss.SpokenCoverage >= 0.5)
				{
					ss.Status = "✖ 主播念了，但被剪掉"; ss.Risk = QcRisk.High; report.ScriptSentenceCut++;
				}
				else
				{
					ss.Status = "○ 主播未念 / 未识别到（非剪辑问题）"; ss.Risk = QcRisk.Review; report.ScriptSentenceUnspoken++;
				}
			}

			// 文案词频缺口：文案需要的次数 − 成片保留的次数（用于判断剪掉的词是否“成片里再也没有了”）
			var scriptCount = new Dictionary<string, int>(StringComparer.Ordinal);
			foreach (var n in flatNorm) scriptCount[n] = (scriptCount.TryGetValue(n, out var c) ? c : 0) + 1;
			var keptCount = new Dictionary<string, int>(StringComparer.Ordinal);
			foreach (var n in keptNorms) keptCount[n] = (keptCount.TryGetValue(n, out var c) ? c : 0) + 1;
			foreach (var kv in scriptCount)
			{
				int k = keptCount.TryGetValue(kv.Key, out var kc) ? kc : 0;
				if (kv.Value - k > 0) scriptDeficit[kv.Key] = kv.Value - k;
			}

			report.ScriptRecall = report.ScriptTokenCount > 0 ? report.KeptMatchedTokenCount / (double)report.ScriptTokenCount : 0.0;
			report.SpokenRecall = report.ScriptTokenCount > 0 ? report.SpokenMatchedTokenCount / (double)report.ScriptTokenCount : 0.0;
			report.RetentionRate = report.SpokenMatchedTokenCount > 0 ? Math.Min(1.0, report.KeptMatchedTokenCount / (double)report.SpokenMatchedTokenCount) : 1.0;
		}

		// ---------- 3. 反推被剪掉的区间 ----------
		var cuts = new List<(double s, double e)>();
		double cursor = 0.0;
		foreach (var k in merged)
		{
			if (k.SrcStart - cursor > 0.06) cuts.Add((cursor, k.SrcStart));
			cursor = Math.Max(cursor, k.SrcEnd);
		}
		if (srcDur - cursor > 0.06) cuts.Add((cursor, srcDur));
		if (merged.Count == 0) cuts.Clear(); // 时间线上没有主素材：无从比较

		var badTakes = input.BadTakes ?? new List<(double, double, string)>();
		int idx = 0;
		foreach (var (cs, ce) in cuts)
		{
			var item = new QcCutItem { Index = ++idx, SrcStart = cs, SrcEnd = ce };

			// 接缝在成片时间线上的位置：紧邻其前一段保留内容的结尾
			var prev = rawKept.Where(k => k.SrcEnd <= cs + 0.05).OrderByDescending(k => k.SrcEnd).FirstOrDefault();
			if (prev != null) item.TimelineSeam = prev.TimelineStart + (prev.SrcEnd - prev.SrcStart);
			else
			{
				var next = rawKept.Where(k => k.SrcStart >= ce - 0.05).OrderBy(k => k.SrcStart).FirstOrDefault();
				item.TimelineSeam = next?.TimelineStart ?? 0.0;
			}

			double cutDur = ce - cs;
			var inside = new List<QcWord>();
			foreach (var w in allWords)
			{
				if (w.End <= cs || w.Start >= ce) continue;
				double wd = Math.Max(0.02, w.End - w.Start);
				double ov = Overlap(w.Start, w.End, cs, ce);
				double frac = ov / wd;
				if (!w.Kept && frac >= 0.5) inside.Add(w);
				// 切点落在单词中间（被切掉 20%~80%），存在吞字/掉音风险
				if (frac > 0.2 && frac < 0.8 && wd >= 0.12) item.ClippedWords.Add(w.Text);
			}
			item.Words = inside.Select(w => w.Text).ToList();
			item.Text = string.Join(HasCjk(string.Concat(item.Words)) ? "" : " ", item.Words);
			item.ContentWords = inside.Where(w => !IsFiller(w.Norm)).Select(w => w.Text).ToList();

			// AI 智剪记录的剪切原因（口误重念 / 重复录制 / 文案外内容 等）
			string aiReason = null;
			foreach (var bt in badTakes)
			{
				double ov = Overlap(cs, ce, bt.StartSec, bt.EndSec);
				if (ov >= Math.Min(cutDur, bt.EndSec - bt.StartSec) * 0.5) { aiReason = bt.Reason; break; }
			}

			// 是否在附近的保留内容里出现了“另一遍”（口误重念、重复录制）
			bool repeatedNearby = false;
			if (item.ContentWords.Count > 0)
			{
				var near = allWords.Where(w => w.Kept && w.Start >= cs - 25.0 && w.End <= ce + 25.0).Select(w => w.Norm).ToList();
				int hit = 0;
				foreach (var cw in inside.Where(w => !IsFiller(w.Norm)))
				{
					if (near.Any(n => WordEq(n, cw.Norm))) hit++;
				}
				repeatedNearby = hit >= Math.Max(1, (int)Math.Ceiling(item.ContentWords.Count * 0.6));
			}

			// 剪掉的文案词，成片里是否再也没有出现
			if (report.HasScript)
			{
				foreach (var w in inside)
				{
					if (IsFiller(w.Norm)) continue;
					if (scriptDeficit.TryGetValue(w.Norm, out var d) && d > 0)
					{
						item.LostScriptWords.Add(w.Text);
						scriptDeficit[w.Norm] = d - 1;
					}
				}
			}
			bool lostHasDigit = item.LostScriptWords.Any(w => w.Any(char.IsDigit));
			bool contentHasDigit = item.ContentWords.Any(w => w.Any(char.IsDigit));

			if (inside.Count == 0)
			{
				item.Category = "静音 / 换气停顿";
				item.Reason = cutDur < 0.6 ? "这段没有人声，是很短的停顿，剪掉后语流更紧凑"
					: (cutDur < 2.5 ? "这段没有人声（主播停顿/换气），剪掉空档让节奏更紧凑"
					: "这段较长时间没有人声（可能是准备动作/空镜/低头看稿），已去除");
				item.Risk = QcRisk.Safe;
				report.SilenceCount++;
			}
			else if (item.ContentWords.Count == 0)
			{
				item.Category = "语气词 / 口头禅";
				item.Reason = "剪掉的只有无实际意义的语气词或口头禅，不影响表达";
				item.Risk = QcRisk.Safe;
				report.FillerCount++;
			}
			else if (report.HasScript && (item.LostScriptWords.Count >= 2 || lostHasDigit ||
				(item.LostScriptWords.Count >= 1 && item.LostScriptWords.Count * 2 >= item.ContentWords.Count)))
			{
				item.Category = "⚠ 疑似误剪（文案内容）";
				item.Reason = "剪掉的话包含标准文案里的词句，而且成片中没有再出现" + (lostHasDigit ? "（含数字/价格等关键信息）" : "") +
					"。建议试听；确认误剪可一键恢复";
				item.Risk = QcRisk.High;
				report.SpeechCutCount++;
			}
			else if (repeatedNearby || (aiReason != null && (aiReason.Contains("重") || aiReason.Contains("口误"))))
			{
				item.Category = "口误重念 / 重复录制";
				item.Reason = repeatedNearby
					? "主播把这句（或其中一部分）重说了一遍，成片保留了另一遍，这里剪掉的是重复/说错的那遍"
					: $"AI 判定为【{aiReason}】，但成片中未找到完全相同的另一遍，建议试听确认";
				item.Risk = repeatedNearby ? QcRisk.Safe : QcRisk.Review;
				report.RetakeCount++;
			}
			else if (aiReason != null && aiReason.Contains("文案外"))
			{
				item.Category = "文案外多说内容";
				item.Reason = "这段话不在标准文案中（主播临场多说），按文案为准被剪除" + (contentHasDigit ? "；其中含数字，建议试听确认" : "");
				item.Risk = contentHasDigit ? QcRisk.Review : QcRisk.Safe;
				report.SpeechCutCount++;
			}
			else if (aiReason != null && aiReason.Contains("语气"))
			{
				item.Category = "语气词 / 口误废话";
				item.Reason = "AI 判定为语气停顿或说了一半的废话";
				item.Risk = item.ContentWords.Count >= 3 ? QcRisk.Review : QcRisk.Safe;
				report.FillerCount++;
			}
			else
			{
				item.Category = "剪掉了说话内容";
				item.Reason = report.HasScript
					? "剪掉了一段有实际内容的话（不在文案中或文案中已有保留），建议试听确认是否需要"
					: "剪掉了一段有实际内容的话（未提供标准文案，无法自动核对），建议试听确认";
				item.Risk = (contentHasDigit || item.ContentWords.Count >= 4) ? QcRisk.Review : QcRisk.Safe;
				if (item.Risk == QcRisk.Safe) item.Reason = "剪掉了很短的几个词（多为停顿前后的残音/半截词），影响很小";
				report.SpeechCutCount++;
			}

			if (item.ClippedWords.Count > 0)
			{
				report.ClippedCount++;
				item.Reason += $"；⚠ 切点切到了单词「{string.Join(" / ", item.ClippedWords.Distinct().Take(3))}」中间，可能吞字/掉音";
				if (item.Risk == QcRisk.Safe) item.Risk = QcRisk.Review;
			}

			if (item.Risk == QcRisk.High) report.HighCount++;
			else if (item.Risk == QcRisk.Review) report.ReviewCount++;
			report.CutItems.Add(item);
		}
		report.CutDuration = report.CutItems.Sum(c => c.Duration);

		// 文案中“念了却被剪掉”的句子也计入高危
		int scriptHigh = report.ScriptSentences.Count(s => s.Risk == QcRisk.High);

		// ---------- 4. 评分与结论 ----------
		double score;
		if (report.HasScript)
		{
			score = report.RetentionRate * 100.0
				- 6.0 * Math.Max(report.HighCount, scriptHigh)
				- 1.5 * report.ReviewCount
				- 1.0 * report.ClippedCount;
		}
		else
		{
			score = 100.0 - 8.0 * report.HighCount - 3.0 * report.ReviewCount - 1.0 * report.ClippedCount;
		}
		score = Math.Max(0.0, Math.Min(100.0, score));
		report.Score = Math.Round(score, 1);

		int highTotal = Math.Max(report.HighCount, scriptHigh);
		if (merged.Count == 0)
		{
			report.VerdictLevel = QcRisk.High;
			report.Verdict = "无法质检";
		}
		else if (highTotal == 0 && score >= 90.0)
		{
			report.VerdictLevel = QcRisk.Safe;
			report.Verdict = "合格";
		}
		else if (score >= 75.0)
		{
			report.VerdictLevel = QcRisk.Review;
			report.Verdict = "需人工复核";
		}
		else
		{
			report.VerdictLevel = QcRisk.High;
			report.Verdict = "不合格";
		}

		var sb = new StringBuilder();
		if (merged.Count == 0)
		{
			sb.Append("时间线上没有找到这条素材的保留片段，无法质检。");
		}
		else
		{
			sb.Append($"共剪掉 {report.CutItems.Count} 处（{report.CutDuration:0.0} 秒）：静音停顿 {report.SilenceCount}、语气词 {report.FillerCount}、口误重念 {report.RetakeCount}、说话内容 {report.SpeechCutCount}。");
			if (report.HasScript)
				sb.Append($" 成片保留文案 {report.KeptMatchedTokenCount}/{report.ScriptTokenCount} 词（契合度 {report.ScriptRecall:P1}），主播已念文案的保留率 {report.RetentionRate:P1}。");
			else
				sb.Append(" 未提供标准文案，仅按剪掉的内容评估。");
			if (highTotal > 0) sb.Append($" 发现 {highTotal} 处疑似误剪，请重点核对。");
			if (report.ClippedCount > 0) sb.Append($" 有 {report.ClippedCount} 处切点可能吞字。");
		}
		report.Summary = sb.ToString();
		return report;
	}
}
