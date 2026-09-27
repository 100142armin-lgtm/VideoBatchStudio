using System;
using System.IO;
using System.Windows.Forms;

namespace VideoBatchMerger;

internal static class Program
{
	[STAThread]
	private static void Main()
	{
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
			Application.Run(new MainForm());
		}
		catch (Exception ex)
		{
			ShowError(ex);
		}
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
