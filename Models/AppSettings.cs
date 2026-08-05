using System.Text.Json.Serialization;

namespace GazeStick.Models;

public sealed class AppSettings
{
    public double Deadzone { get; set; } = 0.10;
    public double Sensitivity { get; set; } = 2.0;
    public double Smoothing { get; set; } = 0.30;
    public double BlinkClampThreshold { get; set; } = 0.0;
    public bool InvertY { get; set; } = false;
    public string ToggleHotkey { get; set; } = "F9";
    public bool StartWithWindows { get; set; } = false;
    public bool StartActive { get; set; } = true;
    public CurveType Curve { get; set; } = CurveType.Linear;
    public double CurvePower { get; set; } = 2.0;
    public bool ShowOnboarding { get; set; } = true;
    public OutputType OutputType { get; set; } = OutputType.Xbox360;
}
