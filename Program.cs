using System.Runtime.InteropServices;
using GazeStick.UI;

namespace GazeStick;

internal static class Program
{
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    private const int SW_HIDE = 0;

    [STAThread]
    private static void Main()
    {
        Trace("Start");
        try
        {
            var consoleWnd = GetConsoleWindow();
            if (consoleWnd != IntPtr.Zero)
                ShowWindow(consoleWnd, SW_HIDE);

            Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            using var mutex = new Mutex(true, "GazeStick-SingleInstance", out var createdNew);

            if (!createdNew)
            {
                Trace("Already running");
                MessageBox.Show("GazeStick is already running.", "GazeStick",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ApplicationConfiguration.Initialize();
            Trace("Application.Run started");
            Application.Run(new TrayApplicationContext());
            Trace("Exit");
        }
        catch (Exception ex)
        {
            Trace($"예외: {ex.GetType().FullName}: {ex.Message}\n{ex.StackTrace}");
            LogCrash(ex);
        }
    }

    private static void Trace(string message)
    {
        try
        {
            var logDir = Path.GetTempPath();
            var logPath = Path.Combine(logDir, "GazeStick-startup.log");
            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
        }
        catch { }
    }

    private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
    {
        Trace($"ThreadException: {e.Exception.Message}");
        LogCrash(e.Exception);
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Trace($"UnhandledException: {ex.Message}");
            LogCrash(ex);
        }
    }

    private static void LogCrash(Exception ex)
    {
        try
        {
            var crashDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "GazeStick");
            if (!Directory.Exists(crashDir))
                Directory.CreateDirectory(crashDir);

            var logPath = Path.Combine(crashDir, "crash.log");
            var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex.GetType().FullName}: {ex.Message}\n{ex.StackTrace}\n\n";
            File.AppendAllText(logPath, entry);
        }
        catch { }
    }
}