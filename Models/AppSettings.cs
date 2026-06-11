using System.Text.Json.Serialization;

namespace GazeStick.Models;

public sealed class AppSettings
{
    public double Deadzone { get; set; } = 0.10;
    public double Sensitivity { get; set; } = 1.0;
    public double Smoothing { get; set; } = 0.30;
    public bool InvertY { get; set; } = false;
    public string ToggleHotkey { get; set; } = "F9";
    public int PadSlot { get; set; } = 0; // 0 = auto
    public bool StartWithWindows { get; set; } = true;
    public bool StartActive { get; set; } = true;
}