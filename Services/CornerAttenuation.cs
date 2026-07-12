using System.Diagnostics;
using System.Globalization;

namespace GazeStick.Services;

internal enum CornerState
{
    Inside,
    Leaving,
    Outside,
    Entering,
}

/// <summary>
/// Corner attenuation layer.
///
/// Gaze inside the ellipse passing through the four cardinal edge-midpoints
/// (u^2 + v^2 <= 1, where u = 2*(x-0.5), v = 2*(y-0.5)) is passed through 1:1.
/// Once the gaze leaves that ellipse (entering screen-corner regions), the gain
/// is progressively attenuated so that direction jitter / noise in the corners
/// does not destabilize corner UI. The gain is a separate multiplier applied
/// after the existing stick-vector smoothing, so it never re-smooths the
/// underlying vector.
///
/// This is a state function: it must be called exactly once per frame so that
/// the accumulated delta-time is not applied twice.
///
/// Parameters are pre-measurement defaults (see docs/CornerAttenuation.md) and
/// are intentionally NOT exposed as user settings.
/// </summary>
internal sealed class CornerAttenuation
{
    // Pre-measurement default parameters (doc section 4). Tune after real-world
    // measurement; do not expose as user settings.
    private const double WindowMs = 130.0;   // LEAVING grace window before OUTSIDE is confirmed
    private const double DipDepth = 0.3;     // INSIDE(1.0) dips down to at most 1.0 - DipDepth (0.7)
    private const double EnterTauMs = 90.0;  // ENTERING exponential time constant
    private const double EnterConverge = 0.99; // gain threshold to switch back to INSIDE
    private const double MaxDtMs = 100.0;    // clamp dt to avoid jumps after stalls / pauses

    // Master kill-switch (internal only, not a user setting).
    private static readonly bool Enabled = true;

    // Debug log (CSV) gated by environment variable. No user-facing setting.
    private static readonly bool LogEnabled =
        Environment.GetEnvironmentVariable("GAZESTICK_CORNER_DEBUG") == "1";

    private const string LogPath =
        "GazeStick-corner.log"; // resolved against Path.GetTempPath()

    private const long CapBytes = 5 * 1024 * 1024; // 5 MB cap
    private const int FlushEvery = 30;             // flush buffered lines every N frames

    private CornerState _state = CornerState.Inside;
    private double _gain = 1.0;
    private double _t; // ms accumulated while in LEAVING

    private readonly object _logLock = new();
    private bool _logHeaderWritten;
    private int _logFrame;
    private readonly System.Text.StringBuilder _logBuffer = new();

    public double Compute(double u, double v, double dtMs)
    {
        if (!Enabled)
            return 1.0;

        double dt = Math.Clamp(dtMs, 0.0, MaxDtMs);
        bool inside = (u * u + v * v) <= 1.0;

        switch (_state)
        {
            case CornerState.Inside:
                if (!inside)
                {
                    _state = CornerState.Leaving;
                    _t = 0.0;
                    // gain stays at 1.0; the dip begins next frame
                }
                break;

            case CornerState.Leaving:
                _t += dt;
                if (inside)
                {
                    // Re-entered the ellipse: re-converge from the current gain.
                    _state = CornerState.Entering;
                }
                else if (_t >= WindowMs)
                {
                    _state = CornerState.Outside;
                    _gain = 0.0;
                }
                else
                {
                    _gain = 1.0 - (_t / WindowMs) * DipDepth;
                    double floor = 1.0 - DipDepth;
                    if (_gain < floor)
                        _gain = floor;
                }
                break;

            case CornerState.Outside:
                if (inside)
                {
                    // Confirmed re-entry: smooth gain up from 0.
                    _state = CornerState.Entering;
                }
                break;

            case CornerState.Entering:
                double alpha = 1.0 - Math.Exp(-dt / EnterTauMs);
                _gain += (1.0 - _gain) * alpha;
                if (!inside)
                {
                    // Interrupted: snap back to LEAVING, gain carries over.
                    _state = CornerState.Leaving;
                    _t = 0.0;
                }
                else if (_gain >= EnterConverge || dt <= 0.0)
                {
                    _gain = 1.0;
                    _state = CornerState.Inside;
                }
                break;
        }

        if (_gain > 1.0)
            _gain = 1.0;

        if (LogEnabled)
            WriteLog(u, v, dt);

        return _gain;
    }

    public void Reset()
    {
        _state = CornerState.Inside;
        _gain = 1.0;
        _t = 0.0;
    }

    private void WriteLog(double u, double v, double dt)
    {
        lock (_logLock)
        {
            if (!_logHeaderWritten)
            {
                // Session start: overwrite (not append) the file.
                try
                {
                    File.WriteAllText(
                        Path.Combine(Path.GetTempPath(), LogPath),
                        "timestamp,state,gain,t,u,v\n");
                }
                catch { }
                _logHeaderWritten = true;
                _logFrame = 0;
                _logBuffer.Clear();
            }

            var ts = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
            _logBuffer.Append(ts).Append(',')
                .Append(_state.ToString()).Append(',')
                .Append(_gain.ToString("F4", CultureInfo.InvariantCulture)).Append(',')
                .Append(_t.ToString("F1", CultureInfo.InvariantCulture)).Append(',')
                .Append(u.ToString("F4", CultureInfo.InvariantCulture)).Append(',')
                .Append(v.ToString("F4", CultureInfo.InvariantCulture)).Append('\n');

            if (++_logFrame >= FlushEvery)
            {
                FlushLog();
            }
        }
    }

    private void FlushLog()
    {
        if (_logBuffer.Length == 0)
            return;

        try
        {
            var full = Path.Combine(Path.GetTempPath(), LogPath);
            if (File.Exists(full))
            {
                var info = new FileInfo(full);
                if (info.Length > CapBytes)
                {
                    // Exceeded cap: restart the file (keeps growth bounded).
                    File.WriteAllText(full, "timestamp,state,gain,t,u,v\n");
                }
            }

            File.AppendAllText(full, _logBuffer.ToString());
            _logBuffer.Clear();
            _logFrame = 0;
        }
        catch { }
    }
}
