using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace VideoBatchMerger.ScriptBatch
{
	public static class LocalCloneEngine
	{
		public static string FindPythonExe()
		{
			string venvPy = @"D:\工具\视频拼接\VideoBatchStudio\local_ai_env\Scripts\python.exe";
			if (File.Exists(venvPy)) return venvPy;

			string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			string py312 = Path.Combine(localApp, @"Programs\Python\Python312\python.exe");
			if (File.Exists(py312)) return py312;

			return "python.exe";
		}

		public static string FindWorkerScript()
		{
			string cand1 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "xtts_worker.py");
			if (File.Exists(cand1)) return cand1;

			string cand2 = @"D:\工具\视频拼接\VideoBatchStudio\xtts_worker.py";
			if (File.Exists(cand2)) return cand2;

			string cand3 = @"C:\Users\PcVkH\.gemini\antigravity\scratch\VideoBatchStudio\xtts_worker.py";
			if (File.Exists(cand3)) return cand3;

			return cand1;
		}

		public static string GetModelDir()
		{
			string cand1 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"xtts_models\v2.0.2");
			if (Directory.Exists(cand1)) return cand1;

			string cand2 = @"D:\工具\视频拼接\VideoBatchStudio\xtts_models\v2.0.2";
			if (Directory.Exists(cand2)) return cand2;

			return cand2;
		}

		public static string GetClonedVoicesDir()
		{
			string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cloned_voices");
			if (!Directory.Exists(dir))
			{
				try { Directory.CreateDirectory(dir); } catch { }
			}
			return dir;
		}

		public static string ResolveSpeakerPath(string speakerRef)
		{
			if (string.IsNullOrWhiteSpace(speakerRef))
			{
				return GetFallbackLatent();
			}

			// 1. 直接文件校验
			if (File.Exists(speakerRef)) return speakerRef;

			string safe = speakerRef;
			if (safe.StartsWith("local:", StringComparison.OrdinalIgnoreCase))
			{
				safe = safe.Substring(6).Trim();
			}

			if (File.Exists(safe)) return safe;

			// 2. 检查 cloned_voices 目录
			string vDir = GetClonedVoicesDir();
			string cand1 = Path.Combine(vDir, safe);
			if (File.Exists(cand1)) return cand1;
			if (File.Exists(cand1 + ".pth")) return cand1 + ".pth";
			if (File.Exists(cand1 + ".latent.pth")) return cand1 + ".latent.pth";

			// 3. 在克隆库中根据关键字模糊匹配
			if (Directory.Exists(vDir))
			{
				var files = Directory.GetFiles(vDir, "*.pth");
				var match = files.FirstOrDefault(f =>
					Path.GetFileNameWithoutExtension(f).IndexOf(safe, StringComparison.OrdinalIgnoreCase) >= 0 ||
					safe.IndexOf(Path.GetFileNameWithoutExtension(f), StringComparison.OrdinalIgnoreCase) >= 0);
				if (match != null) return match;

				// 拆分关键词匹配
				var tokens = safe.Split(new[] { '-', '_', ' ', '.', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
				foreach (var token in tokens)
				{
					if (token.Length >= 2)
					{
						var tokenMatch = files.FirstOrDefault(f => Path.GetFileName(f).IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
						if (tokenMatch != null) return tokenMatch;
					}
				}

				if (files.Length > 0) return files[0];
			}

			return GetFallbackLatent();
		}

		private static string GetFallbackLatent()
		{
			string p1 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cloned_voices", "priest_voice.latent.pth");
			if (File.Exists(p1)) return p1;

			string p2 = @"D:\工具\视频拼接\VideoBatchStudio\cloned_voices\priest_voice.latent.pth";
			if (File.Exists(p2)) return p2;

			// 备用寻找任意一个 .pth
			string vDir = GetClonedVoicesDir();
			if (Directory.Exists(vDir))
			{
				var first = Directory.GetFiles(vDir, "*.pth").FirstOrDefault();
				if (first != null) return first;
			}

			return p1;
		}

		private static void LogClone(string message)
		{
			try
			{
				string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "local_clone.log");
				File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\r\n", Encoding.UTF8);
			}
			catch { }
		}

		public static async Task<(bool Success, string LatentPath, string ErrorMessage)> ExtractSpeakerLatentAsync(
			string voiceName,
			string audioFilePath,
			CancellationToken ct = default)
		{
			if (string.IsNullOrWhiteSpace(audioFilePath) || !File.Exists(audioFilePath))
			{
				return (false, null, $"音频采样文件不存在: {audioFilePath}");
			}

			string pythonExe = FindPythonExe();
			string workerScript = FindWorkerScript();
			string modelDir = GetModelDir();
			string voicesDir = GetClonedVoicesDir();

			string safeName = string.Join("_", voiceName.Split(Path.GetInvalidFileNameChars())).Trim();
			if (string.IsNullOrEmpty(safeName)) safeName = $"voice_{DateTime.Now.Ticks}";
			string outLatentPath = Path.Combine(voicesDir, $"{safeName}.latent.pth");

			string cfgPath = Path.Combine(Path.GetTempPath(), $"cfg_extract_{DateTime.Now.Ticks}.json");
			var configObj = new
			{
				action = "extract_latent",
				model_dir = modelDir,
				speaker_wav = audioFilePath,
				output_latent = outLatentPath
			};
			File.WriteAllText(cfgPath, new JavaScriptSerializer().Serialize(configObj), new UTF8Encoding(false));

			string args = $"\"{workerScript}\" --config \"{cfgPath}\"";

			var psi = new ProcessStartInfo
			{
				FileName = pythonExe,
				Arguments = args,
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				StandardOutputEncoding = Encoding.UTF8,
				StandardErrorEncoding = Encoding.UTF8
			};

			try
			{
				using (var proc = new Process { StartInfo = psi })
				{
					var sbOut = new StringBuilder();
					var sbErr = new StringBuilder();

					proc.OutputDataReceived += (_, e) => { if (e.Data != null) sbOut.AppendLine(e.Data); };
					proc.ErrorDataReceived += (_, e) => { if (e.Data != null) sbErr.AppendLine(e.Data); };

					proc.Start();
					proc.BeginOutputReadLine();
					proc.BeginErrorReadLine();

					while (!proc.HasExited)
					{
						if (ct.IsCancellationRequested)
						{
							try { proc.Kill(); } catch { }
							return (false, null, "用户取消了特征提取");
						}
						await Task.Delay(150);
					}

					try { File.Delete(cfgPath); } catch { }

					string stdOut = sbOut.ToString().Trim();
					string stdErr = sbErr.ToString().Trim();
					LogClone($"Extract Latent Output:\n{stdOut}\nErr:\n{stdErr}");

					if (File.Exists(outLatentPath) && new FileInfo(outLatentPath).Length > 1000)
					{
						return (true, outLatentPath, null);
					}

					return (false, null, $"本地 GPU 提取声学特征失败:\n{stdErr}\n{stdOut}");
				}
			}
			catch (Exception ex)
			{
				LogClone($"Extract Exception: {ex.Message}");
				return (false, null, $"调用本地克隆引擎异常: {ex.Message}");
			}
		}

		public static async Task<TtsResult> SynthesizeAsync(
			string text,
			string speakerRef,
			string outputDir,
			int taskId,
			CancellationToken ct = default,
			string language = "pt",
			Action<int, string> progress = null)
		{
			var result = new TtsResult();
			Directory.CreateDirectory(outputDir);

			string audioPath = Path.Combine(outputDir, $"local_clone_{taskId}.wav");
			string srtPath = Path.Combine(outputDir, $"local_clone_sub_{taskId}.srt");
			string cfgPath = Path.Combine(outputDir, $"cfg_synth_{taskId}_{DateTime.Now.Ticks}.json");

			string pythonExe = FindPythonExe();
			string workerScript = FindWorkerScript();
			string modelDir = GetModelDir();

			string resolvedSpeaker = ResolveSpeakerPath(speakerRef);

			var configObj = new
			{
				action = "synthesize",
				model_dir = modelDir,
				speaker_wav = resolvedSpeaker,
				text = text,
				language = language,
				output_wav = audioPath,
				output_srt = srtPath
			};
			File.WriteAllText(cfgPath, new JavaScriptSerializer().Serialize(configObj), new UTF8Encoding(false));

			string args = $"\"{workerScript}\" --config \"{cfgPath}\"";

			LogClone($"Synthesizing Task #{taskId} with speaker: '{resolvedSpeaker}', text len: {text?.Length}");

			var psi = new ProcessStartInfo
			{
				FileName = pythonExe,
				Arguments = args,
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				StandardOutputEncoding = Encoding.UTF8,
				StandardErrorEncoding = Encoding.UTF8
			};

			try
			{
				using (var proc = new Process { StartInfo = psi })
				{
					var sbOut = new StringBuilder();
					var sbErr = new StringBuilder();

					proc.OutputDataReceived += (_, e) => { if (e.Data != null) sbOut.AppendLine(e.Data); };
					proc.ErrorDataReceived += (_, e) => { if (e.Data != null) sbErr.AppendLine(e.Data); };

					proc.Start();
					proc.BeginOutputReadLine();
					proc.BeginErrorReadLine();

					while (!proc.HasExited)
					{
						if (ct.IsCancellationRequested)
						{
							try { proc.Kill(); } catch { }
							result.ErrorMessage = "用户取消了生成任务";
							return result;
						}
						await Task.Delay(200);
					}

					try { File.Delete(cfgPath); } catch { }

					string stdOut = sbOut.ToString().Trim();
					string stdErr = sbErr.ToString().Trim();
					LogClone($"Synthesize Task #{taskId} ExitCode={proc.ExitCode}\nOut:\n{stdOut}\nErr:\n{stdErr}");

					if (File.Exists(audioPath) && new FileInfo(audioPath).Length > 1000)
					{
						result.Success = true;
						result.AudioPath = audioPath;
						result.SrtPath = srtPath;
						result.DurationSeconds = new FileInfo(audioPath).Length / (24000.0 * 2.0); // 24kHz 16-bit mono
						return result;
					}

					result.ErrorMessage = $"本地 RTX 5060 语音合成未生成有效文件:\n{stdErr}\n{stdOut}";
					return result;
				}
			}
			catch (Exception ex)
			{
				LogClone($"Synthesize Task #{taskId} Exception: {ex.Message}");
				result.ErrorMessage = $"本地模型合成执行异常: {ex.Message}";
				return result;
			}
		}
	}
}
