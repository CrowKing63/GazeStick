using GazeStick.Models;

namespace GazeStick.Services;

public sealed class StickMapper
{
    private const bool UseCircularNormalization = true;

    private readonly object _lock = new();
    private double _prevX = 0.0;
    private double _prevY = 0.0;
    private bool _hasPrev = false;

    public StickOutput Map(GazePoint gaze, AppSettings settings)
    {
        if (!gaze.IsValid)
            return StickOutput.Neutral;

        double dx = gaze.X - 0.5;
        double dy = gaze.Y - 0.5;

        // This is the half-range of the normalized gaze coordinate space [0,1],
        // NOT screen pixel dimensions. Aspect ratio is already baked into gaze.X/Y by the SDK.
        const double normalizedHalfRangeX = 0.5;
        const double normalizedHalfRangeY = 0.5;

        double nxRaw = dx / normalizedHalfRangeX;
        double nyRaw = dy / normalizedHalfRangeY;

        double distance = Math.Sqrt(nxRaw * nxRaw + nyRaw * nyRaw);
        double deadzone = Math.Clamp(settings.Deadzone, 0.0, 0.5);

        if (distance < deadzone)
        {
            lock (_lock)
            {
                _prevX = 0.0;
                _prevY = 0.0;
                _hasPrev = true;
            }
            return StickOutput.Neutral;
        }

        double scale = (distance - deadzone) / (1.0 - deadzone);
        scale = ApplyCurve(scale, settings.Curve, settings.CurvePower);

        double nx = (nxRaw / distance) * scale;
        double ny = (nyRaw / distance) * scale;

        double sensitivity = Math.Clamp(settings.Sensitivity, 0.1, 5.0);
        nx = Math.Clamp(nx * sensitivity, -1.0, 1.0);
        ny = Math.Clamp(ny * sensitivity, -1.0, 1.0);

        if (UseCircularNormalization)
        {
            double mag = Math.Sqrt(nx * nx + ny * ny);
            if (mag > 1.0)
            {
                nx = nx / mag;
                ny = ny / mag;
            }
        }

        double smoothing = Math.Clamp(settings.Smoothing, 0.0, 0.9);

        lock (_lock)
        {
            if (_hasPrev && smoothing > 0.0)
            {
                nx = _prevX * smoothing + nx * (1.0 - smoothing);
                ny = _prevY * smoothing + ny * (1.0 - smoothing);
            }

            _prevX = nx;
            _prevY = ny;
            _hasPrev = true;
        }

        if (settings.InvertY)
            ny = -ny;

        int rawX = (int)Math.Round(nx * 32767);
        int rawY = (int)Math.Round(-ny * 32767);
        short stickX = (short)Math.Clamp(rawX, short.MinValue, short.MaxValue);
        short stickY = (short)Math.Clamp(rawY, short.MinValue, short.MaxValue);

        return new StickOutput(stickX, stickY);
    }

    private static double ApplyCurve(double value, CurveType curve, double power)
    {
        if (value <= 0.0) return 0.0;
        if (value >= 1.0) return 1.0;

        return curve switch
        {
            CurveType.Exponential => Math.Pow(value, Math.Max(power, 0.1)),
            CurveType.Logarithmic => Math.Pow(value, 1.0 / Math.Max(power, 0.1)),
            _ => value,
        };
    }

    public void Reset()
    {
        lock (_lock)
        {
            _prevX = 0.0;
            _prevY = 0.0;
            _hasPrev = false;
        }
    }
}