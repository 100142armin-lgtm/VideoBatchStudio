using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Microsoft.Win32;
using VideoBatchMerger.ScriptBatch;

namespace VideoBatchMerger.Components;

public sealed class ComponentSpec
{
	public string Id { get; set; }
	public string Name { get; set; }
	public string Description { get; set; }
	public string Kind { get; set; }
	public string Target { get; set; }
	public string Entry { get; set; }
	public string Url { get; set; }
	public string Sha256 { get; set; }
	public long Size { get; set; }
	public bool Recommended { get; set; }
}

public sealed class ComponentCatalog
{
	public int schema { get; set; }
	public List<ComponentSpec> components { get; set; }
}

public sealed class ComponentManager
{
	public readonly string Root;
	public readonly List<ComponentSpec> Components;
	public ComponentManager(string root = null)
	{
		Root = Path.GetFullPath(root ?? AppDomain.CurrentDomain.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar);
		var catalog = new JavaScriptSerializer().Deserialize<ComponentCatalog>(File.ReadAllText(Path.Combine(Root, "Setup", "components.json"), Encoding.UTF8));
		if (catalog.schema != 1) throw new InvalidDataException("组件目录版本无法识别，请重新下载完整软件包。");
		Components = catalog.components;
	}

	public static string Quote(string value)
	{
		var result = new StringBuilder("\""); int slashes = 0;
		foreach (char ch in value ?? "")
		{
			if (ch == '\\') { slashes++; continue; }
			result.Append('\\', ch == '"' ? slashes * 2 + 1 : slashes); result.Append(ch); slashes = 0;
		}
		return result.Append('\\', slashes * 2).Append('"').ToString();
	}

	public static bool BasicFilesMissing(string root) => new[] { "ffmpeg.exe", "yt-dlp.exe", "edge_runtime/python/python.exe", "edge_runtime/ready.json", "whisper/whisper-cli.exe", "whisper/models/ggml-base.bin" }
		.Any(file => !File.Exists(Path.Combine(root, file)));

	public string UnderRoot(string relative)
	{
		string path = Path.GetFullPath(Path.Combine(Root, relative));
		if (path != Root && !path.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
			throw new InvalidDataException("组件路径超出软件目录。");
		return path;
	}

	public static string Hash(string file)
	{
		using (var hash = SHA256.Create()) using (var input = File.OpenRead(file))
			return BitConverter.ToString(hash.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
	}

	public static async Task<string> RunAsync(string executable, string arguments, string workingDirectory,
		Action<string> log, CancellationToken ct, int timeoutSeconds = 180)
	{
		var start = new ProcessStartInfo(executable, arguments) { UseShellExecute = false, CreateNoWindow = true,
			RedirectStandardError = true, RedirectStandardOutput = true, WorkingDirectory = workingDirectory,
			StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
		start.EnvironmentVariables["PYTHONUTF8"] = "1";
		start.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
		start.EnvironmentVariables["PATH"] = workingDirectory + Path.PathSeparator + Path.Combine(workingDirectory, "tools", "node")
			+ Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");
		var output = new StringBuilder(); var error = new StringBuilder();
		using (var process = new Process { StartInfo = start })
		{
			process.OutputDataReceived += (_, e) => { if (e.Data != null) { lock (output) output.AppendLine(e.Data); log?.Invoke(e.Data); } };
			process.ErrorDataReceived += (_, e) => { if (e.Data != null) { lock (error) error.AppendLine(e.Data); log?.Invoke(e.Data); } };
			process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine(); var timer = Stopwatch.StartNew();
			try
			{
				while (!process.HasExited)
				{
					ct.ThrowIfCancellationRequested();
					if (timer.Elapsed.TotalSeconds > timeoutSeconds) throw new TimeoutException("组件操作超时，请重试：" + Path.GetFileName(executable));
					await Task.Delay(100, ct).ConfigureAwait(false);
				}
				process.WaitForExit();
				if (process.ExitCode != 0) throw new InvalidOperationException(Path.GetFileName(executable) + " 退出码 " + process.ExitCode + "：" + error.ToString());
				return output.ToString() + error;
			}
			catch
			{
				try
				{
					using (var killer = Process.Start(new ProcessStartInfo("taskkill.exe", "/PID " + process.Id + " /T /F")
					{ UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true })) killer?.WaitForExit(4000);
				}
				catch { try { process.Kill(); } catch { } }
				throw;
			}
		}
	}

	public async Task<string> CheckAsync(ComponentSpec spec, CancellationToken ct)
	{
		if (spec.Kind == "file" && spec.Id != "ytdlp")
		{
			string path = UnderRoot(spec.Target);
			if (!File.Exists(path)) return "未安装";
			return new FileInfo(path).Length == spec.Size && Hash(path) == spec.Sha256 ? "已就绪" : "文件损坏 / 待修复";
		}
		try
		{
			if (spec.Id == "vcredist")
			{
				using (var registry = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
				using (var key = registry.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64"))
					return Convert.ToInt32(key?.GetValue("Installed", 0) ?? 0) == 1 ? "已就绪" : "未安装";
			}
			if (spec.Id == "webview2")
				return !string.IsNullOrEmpty(Microsoft.Web.WebView2.Core.CoreWebView2Environment.GetAvailableBrowserVersionString()) ? "已就绪" : "未安装";
			if (spec.Id == "chatterbox")
			{
				if (!File.Exists(UnderRoot("chatterbox_runtime/ready.json"))) return "未安装";
				await RunAsync(UnderRoot("chatterbox_runtime/python/python.exe"), "-u " + Quote(UnderRoot("ScriptBatch/chatterbox_install.py"))
					+ " --check-only --root " + Quote(UnderRoot("chatterbox_runtime")), Root, null, ct, 180).ConfigureAwait(false);
				return "已就绪";
			}
			if (spec.Id == "edge")
			{
				if (!File.Exists(UnderRoot("edge_runtime/ready.json"))) return "未安装";
				await RunAsync(UnderRoot("edge_runtime/python/python.exe"), "-c \"import edge_tts, aiohttp; print(edge_tts.__version__)\"", Root, null, ct, 30).ConfigureAwait(false);
				return File.Exists(UnderRoot("ScriptBatch/edge_tts_worker.py")) ? "已就绪" : "缺少配音脚本";
			}
			string binary = spec.Id == "ffmpeg" ? "ffmpeg.exe" : spec.Id == "ytdlp" ? "yt-dlp.exe" :
				spec.Id == "node" ? "tools/node/node.exe" : spec.Target + "/whisper-cli.exe";
			if (!File.Exists(UnderRoot(binary))) return "未安装";
			await RunAsync(UnderRoot(binary), spec.Id == "ytdlp" || spec.Id == "node" ? "--version" : spec.Id == "ffmpeg" ? "-version" : "--help", Root, null, ct, 30).ConfigureAwait(false);
			return "已就绪";
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) { return "未就绪：" + ex.Message.Split('\r', '\n')[0]; }
	}

	public async Task<string> DownloadAsync(string url, string expected, Action<string> log, CancellationToken ct)
	{
		if (string.IsNullOrEmpty(expected) || expected.Length != 64 || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https")
			throw new InvalidDataException("组件缺少官方 HTTPS 地址或 SHA256 校验值。");
		string folder = UnderRoot("component_cache"); Directory.CreateDirectory(folder);
		string path = Path.Combine(folder, expected.ToLowerInvariant() + Path.GetExtension(uri.AbsolutePath));
		if (File.Exists(path) && Hash(path) == expected) { log?.Invoke("复用已校验下载缓存"); return path; }
		string part = path + ".part";
		ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
		using (var handler = new HttpClientHandler { AllowAutoRedirect = false })
		using (var http = new HttpClient(handler) { Timeout = TimeSpan.FromHours(2) })
		{
			http.DefaultRequestHeaders.UserAgent.ParseAdd("VideoBatchStudio/9.5.2");
			for (int attempt = 0; attempt < 3; attempt++)
			{
				try
				{
					long existing = File.Exists(part) ? new FileInfo(part).Length : 0;
					string current = url;
					for (int redirects = 0; ; redirects++)
					{
						using (var request = new HttpRequestMessage(HttpMethod.Get, current))
						{
							if (existing > 0) request.Headers.Range = new RangeHeaderValue(existing, null);
							using (var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
							{
								int status = (int)response.StatusCode;
								if (status == 301 || status == 302 || status == 303 || status == 307 || status == 308)
								{
									if (redirects >= 10 || response.Headers.Location == null) throw new IOException("组件下载重定向异常。");
									var next = new Uri(new Uri(current), response.Headers.Location);
									if (next.Scheme != "https") throw new IOException("拒绝不安全的下载重定向。");
									current = next.AbsoluteUri; continue;
								}
								if (status == 416) { File.Delete(part); throw new IOException("重新开始未完成的下载。"); }
								response.EnsureSuccessStatusCode();
								bool append = status == 206 && response.Content.Headers.ContentRange?.From == existing;
								long done = append ? existing : 0, total = (response.Content.Headers.ContentLength ?? 0) + done;
								using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
								using (var output = new FileStream(part, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None))
								{
									byte[] buffer = new byte[1024 * 1024]; int count; var timer = Stopwatch.StartNew();
									while ((count = await input.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
									{
										await output.WriteAsync(buffer, 0, count, ct).ConfigureAwait(false); done += count;
										if (timer.ElapsedMilliseconds > 700) { log?.Invoke($"下载 {done / 1048576} / {total / 1048576} MB"); timer.Restart(); }
									}
								}
								break;
							}
						}
					}
					ct.ThrowIfCancellationRequested();
					if (Hash(part) != expected) { File.Delete(part); throw new InvalidDataException("组件 SHA256 校验不通过，已丢弃下载内容。"); }
					if (File.Exists(path)) File.Delete(path); File.Move(part, path); return path;
				}
				catch (OperationCanceledException) { throw; }
				catch when (attempt < 2) { log?.Invoke("下载中断，自动重试并续传…"); await Task.Delay(1000, ct).ConfigureAwait(false); }
			}
		}
		throw new IOException("组件下载失败，请检查网络后重试。");
	}

	public static void ExtractSafe(string archive, string directory, Func<ZipArchiveEntry, bool> include = null)
	{
		string root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
		Directory.CreateDirectory(root);
		using (var zip = ZipFile.OpenRead(archive)) foreach (var entry in zip.Entries)
		{
			if (include != null && !include(entry)) continue;
			string target = Path.GetFullPath(Path.Combine(root, entry.FullName));
			if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase) || ((entry.ExternalAttributes >> 16) & 0xf000) == 0xa000)
				throw new InvalidDataException("拒绝压缩包中的越界路径或符号链接。");
			if (string.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(target); continue; }
			Directory.CreateDirectory(Path.GetDirectoryName(target)); entry.ExtractToFile(target, true);
		}
	}

	public async Task InstallAsync(ComponentSpec spec, Action<string> log, CancellationToken ct)
	{
		if (!Environment.Is64BitOperatingSystem) throw new PlatformNotSupportedException("此完整包需要 64 位 Windows 10 / 11。");
		string probe = UnderRoot(".component-write-check");
		try { File.WriteAllText(probe, "test"); File.Delete(probe); }
		catch (UnauthorizedAccessException) { throw new IOException("当前目录无法写入。请将完整软件文件夹解压到桌面或个人文件夹，再运行安装。"); }
		var drive = new DriveInfo(Path.GetPathRoot(Root));
		long reserve = spec.Id == "chatterbox" ? 18L * 1024 * 1024 * 1024 : spec.Size * 3 + 300L * 1024 * 1024;
		if (drive.IsReady && drive.AvailableFreeSpace < reserve) throw new IOException("磁盘空间不足，请至少预留 " + reserve / (1024 * 1024 * 1024) + " GB 后重试。");
		if (spec.Kind == "microsoft")
		{
			await RunAsync(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell/v1.0/powershell.exe"),
				"-NoProfile -ExecutionPolicy Bypass -File " + Quote(UnderRoot("Setup/Install-MicrosoftRuntime.ps1")) + " -Component " + spec.Id,
				Root, log, ct, 1800).ConfigureAwait(false); return;
		}
		if (spec.Kind == "chatterbox")
		{
			if (!string.Equals(Root.TrimEnd('\\'), AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
				throw new InvalidOperationException("请从目标软件文件夹启动，再安装 Chatterbox。");
			await ChatterboxEngine.InstallAsync(log, ct).ConfigureAwait(false); return;
		}
		if (spec.Kind == "edge") { await InstallEdgeAsync(log, ct).ConfigureAwait(false); return; }
		string downloaded = await DownloadAsync(spec.Url, spec.Sha256, log, ct).ConfigureAwait(false);
		string destination = UnderRoot(spec.Target); Directory.CreateDirectory(spec.Kind == "file" ? Path.GetDirectoryName(destination) : destination);
		if (spec.Kind == "file") { File.Copy(downloaded, destination, true); return; }
		string temporary = UnderRoot("component_cache/extract-" + Guid.NewGuid().ToString("N"));
		try
		{
			ExtractSafe(downloaded, temporary, spec.Id == "node" ? (Func<ZipArchiveEntry, bool>)(entry =>
				entry.FullName.Replace('\\', '/').Split('/').Length == 2 && (entry.Name == "node.exe" || entry.Name == "LICENSE")) : null);
			ct.ThrowIfCancellationRequested();
			string binary = Directory.GetFiles(temporary, spec.Entry, SearchOption.AllDirectories).Single();
			string directory = Path.GetDirectoryName(binary);
			foreach (string file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
			{
				ct.ThrowIfCancellationRequested();
				if (spec.Id == "node" && Path.GetFileName(file) != "node.exe" && Path.GetFileName(file) != "LICENSE") continue;
				string target = Path.Combine(destination, file.Substring(directory.Length + 1));
				Directory.CreateDirectory(Path.GetDirectoryName(target)); File.Copy(file, target, true);
			}
			string licenses = UnderRoot("licenses/" + spec.Id); Directory.CreateDirectory(licenses);
			foreach (string file in Directory.GetFiles(temporary, "*", SearchOption.AllDirectories)
				.Where(f => Path.GetFileName(f).StartsWith("LICENSE", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(f).StartsWith("COPYING", StringComparison.OrdinalIgnoreCase)))
				File.Copy(file, Path.Combine(licenses, Path.GetFileName(file)), true);
			File.WriteAllText(Path.Combine(licenses, "SOURCE.txt"), spec.Url + "\nSHA256: " + spec.Sha256 + "\n", Encoding.UTF8);
		}
		finally { if (Directory.Exists(temporary)) Directory.Delete(temporary, true); }
	}

	private async Task InstallEdgeAsync(Action<string> log, CancellationToken ct)
	{
		string runtime = UnderRoot("edge_runtime"); Directory.CreateDirectory(runtime);
		string ready = Path.Combine(runtime, "ready.json"); if (File.Exists(ready)) File.Delete(ready);
		string pythonZip = await DownloadAsync("https://www.python.org/ftp/python/3.12.10/python-3.12.10-embed-amd64.zip",
			"4acbed6dd1c744b0376e3b1cf57ce906f9dc9e95e68824584c8099a63025a3c3", log, ct).ConfigureAwait(false);
		string pythonDir = Path.Combine(runtime, "python"); ExtractSafe(pythonZip, pythonDir);
		File.WriteAllText(Path.Combine(pythonDir, "python312._pth"), "python312.zip\n.\nLib\\site-packages\nimport site\n");
		string pip = await DownloadAsync("https://raw.githubusercontent.com/pypa/get-pip/37ae3c4daaf63a0c203176251fb99c0e238562b5/public/get-pip.py",
			"f4448a01e3ab2914c65f4ca59d4cacba6b2dbde19a483fbac46587f8bbaff424", log, ct).ConfigureAwait(false);
		string python = Path.Combine(pythonDir, "python.exe");
		await RunAsync(python, Quote(pip) + " --disable-pip-version-check", Root, log, ct, 900).ConfigureAwait(false);
		string wheelhouse = UnderRoot("Setup/edge-wheels");
		string install = Directory.Exists(wheelhouse) ? " --no-index --find-links " + Quote(wheelhouse) : " --index-url https://pypi.org/simple";
		await RunAsync(python, "-m pip --isolated --disable-pip-version-check install --require-hashes" + install + " -r "
			+ Quote(UnderRoot("Setup/edge-requirements.lock")), Root, log, ct, 1800).ConfigureAwait(false);
		await RunAsync(python, "-c \"import edge_tts, aiohttp; print(edge_tts.__version__)\"", Root, log, ct, 30).ConfigureAwait(false);
		File.WriteAllText(ready, "{\"schema\":1,\"engine\":\"edge-tts\"}", Encoding.UTF8);
	}
}
