using Eyeware.BeamEyeTracker;
using GazeStick.Models;

namespace GazeStick.Services;

public interface ITrackingService : IDisposable
{
    event Action<GazePoint>? GazeReceived;
    event Action<bool>? ConnectionChanged;
    event Action<string>? ErrorOccurred;
    event Action<ViewportGeometry>? ViewportChanged;
    event Action<string>? TargetDisplayMissing;

    bool IsConnected { get; }
    ViewportGeometry CurrentGeometry { get; }

    /// <summary>
    /// The display device name to target (empty = primary screen). Setting it
    /// re-maps the viewport geometry.
    /// </summary>
    string TargetDisplay { get; set; }

    void Start();
    void Stop();

    /// <summary>
    /// Requests a viewport-geometry re-computation and (debounced) update. Called
    /// on display-change events and by the fallback poll.
    /// </summary>
    void RequestGeometryUpdate();
}