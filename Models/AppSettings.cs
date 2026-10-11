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
    /// <summary>
    /// Device name of the display to target for viewport geometry (e.g. "\\.\DISPLAY1").
    /// Empty string means the primary screen. Old settings files without this key load with "".
    /// </summary>
    public string TargetDisplay { get; set; } = "";

    /// <summary>
    /// The three user presets. Each stores the six adjustable values (deadzone,
    /// sensitivity, smoothing, blink clamp, curve type, curve power). Defaults match
    /// the app defaults so old settings files without this key load with preset 1
    /// at stock values; SettingsManager then seeds preset 1 from the legacy
    /// top-level values on first run.
    /// </summary>
    public Preset[] Presets { get; set; } = DefaultPresets();

    /// <summary>Index (0-2) of the currently active preset. Clamped on load.</summary>
    public int ActivePreset { get; set; } = 0;

    private static Preset[] DefaultPresets() =>
        new[] { new Preset(), new Preset(), new Preset() };
}

/// <summary>
/// A named snapshot of the six adjustable tracking values. Stored per preset so the
/// user can switch between saved configurations without touching each control.
/// </summary>
public sealed class Preset
{
    public double Deadzone { get; set; } = 0.10;
    public double Sensitivity { get; set; } = 2.0;
    public double Smoothing { get; set; } = 0.30;
    public double BlinkClampThreshold { get; set; } = 0.0;
    public CurveType Curve { get; set; } = CurveType.Linear;
    public double CurvePower { get; set; } = 2.0;
}
