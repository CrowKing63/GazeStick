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
}