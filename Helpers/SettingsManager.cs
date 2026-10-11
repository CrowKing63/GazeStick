using System.Text.Json;
using GazeStick.Models;

namespace GazeStick.Helpers;

public static class SettingsManager
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GazeStick",
        "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static readonly object _lock = new();

    public static AppSettings Load()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    bool hasPresetsKey = json.Contains("\"Presets\"");
                    var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                    if (settings != null)
                    {
                        Normalize(settings);
                        // Migration: old settings files predate the Presets key. Seed
                        // preset 1 from the legacy top-level values so a user's custom
                        // configuration becomes their first preset instead of being
                        // replaced by defaults on upgrade.
                        if (!hasPresetsKey)
                            settings.Presets[0] = SnapshotOf(settings);
                        return settings;
                    }
                }
            }
            catch
            {
                // Ignore and return defaults
            }
        }
        return new AppSettings();
    }

    /// <summary>
    /// Repairs a loaded settings object: clamps ActivePreset to 0-2 and regenerates
    /// the preset array if it is missing or shorter than three entries (corrupt file).
    /// </summary>
    private static void Normalize(AppSettings settings)
    {
        settings.ActivePreset = Math.Clamp(settings.ActivePreset, 0, 2);
        if (settings.Presets == null || settings.Presets.Length != 3)
            settings.Presets = new[] { new Preset(), new Preset(), new Preset() };
    }

    /// <summary>Copies the top-level six adjustable values into a fresh Preset.</summary>
    private static Preset SnapshotOf(AppSettings s) => new()
    {
        Deadzone = s.Deadzone,
        Sensitivity = s.Sensitivity,
        Smoothing = s.Smoothing,
        BlinkClampThreshold = s.BlinkClampThreshold,
        Curve = s.Curve,
        CurvePower = s.CurvePower,
    };

    /// <summary>
    /// Copies the active preset's six values onto the top-level (live) fields.
    /// </summary>
    public static void ApplyPreset(AppSettings settings, int index)
    {
        var p = settings.Presets[Math.Clamp(index, 0, 2)];
        settings.Deadzone = p.Deadzone;
        settings.Sensitivity = p.Sensitivity;
        settings.Smoothing = p.Smoothing;
        settings.BlinkClampThreshold = p.BlinkClampThreshold;
        settings.Curve = p.Curve;
        settings.CurvePower = p.CurvePower;
        settings.ActivePreset = Math.Clamp(index, 0, 2);
    }

    /// <summary>
    /// Writes one changed live value into both the top-level field and the active
    /// preset, so real-time edits are stored per preset without a second save.
    /// </summary>
    public static void UpdateValue(AppSettings settings, string key, double value)
    {
        var p = settings.Presets[settings.ActivePreset];
        switch (key)
        {
            case "deadzone": settings.Deadzone = value; p.Deadzone = value; break;
            case "sensitivity": settings.Sensitivity = value; p.Sensitivity = value; break;
            case "smoothing": settings.Smoothing = value; p.Smoothing = value; break;
            case "blinkclamp": settings.BlinkClampThreshold = value; p.BlinkClampThreshold = value; break;
            case "curvepower": settings.CurvePower = value; p.CurvePower = value; break;
        }
    }

    /// <summary>Writes a changed curve type into both the live field and the active preset.</summary>
    public static void UpdateCurve(AppSettings settings, CurveType curve)
    {
        settings.Curve = curve;
        settings.Presets[settings.ActivePreset].Curve = curve;
    }

    public static void Save(AppSettings settings)
    {
        lock (_lock)
        {
            try
            {
                var dir = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(settings, JsonOptions);
                var tempPath = SettingsPath + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, SettingsPath, overwrite: true);
            }
            catch
            {
                // Ignore write failures
            }
        }
    }
}