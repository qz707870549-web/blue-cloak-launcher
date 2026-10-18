using System;
using System.IO;
using System.Threading.Tasks;

namespace Luma;

public static class CrashLog
{
	private static readonly object Gate = new object();

	public static void Write(string phase, Exception exception = null)
	{
		try
		{
			lock (Gate)
			{
				Directory.CreateDirectory(Program.DataDir);
				File.AppendAllText(Path.Combine(Program.DataDir, "startup.log"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [" + phase + "] " + ((exception == null) ? "" : exception.ToString()) + Environment.NewLine);
			}
		}
		catch
		{
		}
	}

	public static void Install()
	{
		AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
		{
			Write("unhandled", e.ExceptionObject as Exception);
		};
		TaskScheduler.UnobservedTaskException += delegate(object s, UnobservedTaskExceptionEventArgs e)
		{
			Write("background-task", e.Exception);
			e.SetObserved();
		};
	}
}
