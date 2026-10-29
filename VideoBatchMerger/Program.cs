using System;
using System.IO;
using System.Windows.Forms;

namespace VideoBatchMerger;

internal static class Program
{
	[STAThread]
	private static void Main(string[] args)
	{
		if (Array.Exists(args, arg => arg == "--prepare-package" || arg == "--verify-package"))
		{
			RunPackageTask(Array.Exists(args, arg => arg == "--prepare-package"));
			return;
		}
		string portableNode = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "node");
		Environment.SetEnvironmentVariable("PATH", portableNode + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"));
		CleanupOldUpdateFiles();
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(defaultValue: false);

		Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
		Application.ThreadException += (s, e) =>
		{
			ShowError(e.Exception);
		};
		AppDomain.CurrentDomain.UnhandledException += (s, e) =>
		{
			if (e.ExceptionObject is Exception ex)
			{
				ShowError(ex);
			}
		};

		try
		{
			if (Array.Exists(args, arg => string.Equals(arg, "--components", StringComparison.OrdinalIgnoreCase)))
			{
				Application.Run(new Components.ComponentSetupForm());
				return;
			}
			Application.Run(new MainForm());
		}
		catch (Exception ex)
		{
			ShowError(ex);
		}
	}

	private static void RunPackageTask(bool install)
	{
		string root = AppDomain.CurrentDomain.BaseDirectory;
		string log = Path.Combine(root, "package-validation.log");
		var results = new System.Collections.Generic.Dictionary<string, string>();
		try
		{
			var manager = new Components.ComponentManager(root);
			System.Threading.Tasks.Task.Run(async () =>
			{
				foreach (var spec in manager.Components)
				{
					if (!spec.Recommended || spec.Kind == "microsoft") continue;
					string state = await manager.CheckAsync(spec, System.Threading.CancellationToken.None);
					if (install && state != "已就绪")
						await manager.InstallAsync(spec, line => File.AppendAllText(log, line + Environment.NewLine), System.Threading.CancellationToken.None);
					state = await manager.CheckAsync(spec, System.Threading.CancellationToken.None);
					results[spec.Id] = state;
					if (state != "已就绪") throw new InvalidOperationException(spec.Name + "：" + state);
				}
			}).GetAwaiter().GetResult();
			File.WriteAllText(Path.Combine(root, "package-validation.json"), new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(results));
			Environment.ExitCode = 0;
		}
		catch (Exception ex)
		{
			File.AppendAllText(log, ex.ToString() + Environment.NewLine);
			Environment.ExitCode = 1;
		}
	}

	private static void CleanupOldUpdateFiles()
	{
		try
		{
			foreach (string oldFile in Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.old_update", SearchOption.AllDirectories))
			{
				try { File.Delete(oldFile); } catch { }
			}
		}
		catch { }
	}

	private static void ShowError(Exception ex)
	{
		try
		{
			File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"),
				$"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\r\n\r\n");
			MessageBox.Show("软件启动或运行出现异常：\r\n" + ex.Message + "\r\n\r\n详细错误已记录到 crash.log",
				"运行提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
		catch { }
	}
}
