using System.Runtime.InteropServices;
using GazeStick.UI;

namespace GazeStick;

internal static class Program
{
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern IntPtr GetConsoleWindow();

    private const int SW_HIDE = 0;

    [STAThread]
    private static void Main()
    {
        var consoleWnd = GetConsoleWindow();
        if (consoleWnd != IntPtr.Zero)
            ShowWindow(consoleWnd, SW_HIDE);

        Application.ThreadException += OnThreadException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        using var mutex = new Mutex(true, "GazeStick-SingleInstance", out var createdNew);

        if (!createdNew)
        {
            MessageBox.Show("GazeStick가 이미 실행 중입니다.", "GazeStick",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());
    }

    private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
    {
        LogCrash(e.Exception);
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            LogCrash(ex);
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
        catch
        {
            // Silently fail - can't log if logging itself crashes
        }
    }
}