using GazeStick.Models;

namespace GazeStick.Services;

public sealed class StickMapper
{
    private double _prevX = 0.0;
    private double _prevY = 0.0;
    private bool _hasPrev = false;

    public StickOutput Map(GazePoint gaze, AppSettings settings)
    {
        if (!gaze.IsValid)
            return StickOutput.Neutral;

        double dx = gaze.X - 0.5;
        double dy = gaze.Y - 0.5;

        double distance = Math.Sqrt(dx * dx + dy * dy);
        double deadzone = Math.Clamp(settings.Deadzone, 0.0, 0.5);

        if (distance < deadzone)
        {
            _prevX = 0.0;
            _prevY = 0.0;
            _hasPrev = true;
            return StickOutput.Neutral;
        }

        double scale = (distance - deadzone) / (1.0 - deadzone);
        double nx = (dx / distance) * scale;
        double ny = (dy / distance) * scale;

        double sensitivity = Math.Clamp(settings.Sensitivity, 0.1, 5.0);
        nx = Math.Clamp(nx * sensitivity, -1.0, 1.0);
        ny = Math.Clamp(ny * sensitivity, -1.0, 1.0);

        double smoothing = Math.Clamp(settings.Smoothing, 0.0, 0.9);
        if (_hasPrev && smoothing > 0.0)
        {
            nx = _prevX * smoothing + nx * (1.0 - smoothing);
            ny = _prevY * smoothing + ny * (1.0 - smoothing);
        }

        _prevX = nx;
        _prevY = ny;
        _hasPrev = true;

        if (settings.InvertY)
            ny = -ny;

        short stickX = (short)Math.Round(nx * 32767);
        short stickY = (short)Math.Round(-ny * 32767);

        return new StickOutput(stickX, stickY);
    }

    public void Reset()
    {
        _prevX = 0.0;
        _prevY = 0.0;
        _hasPrev = false;
    }
}