using Eyeware.BeamEyeTracker;
using GazeStick.Models;
using System.Windows.Forms;

namespace GazeStick.Services;

public sealed class BeamTrackingService : ITrackingService
{
    private API? _api;
    private System.Threading.Timer? _pollTimer;
    private System.Threading.Timer? _geometryPollTimer;
    private System.Threading.Timer? _debounceTimer;
    private double _lastTimestamp = Constants.NullDataTimestamp;
    private bool _disposed;
    private bool _isConnected;

    private ViewportGeometry _currentGeometry;
    private string _targetDisplay = "";
    private System.Threading.Timer? _verifyTimer;

    public event Action<GazePoint>? GazeReceived;
    public event Action<bool>? ConnectionChanged;
    public event Action<string>? ErrorOccurred;
    public event Action<ViewportGeometry>? ViewportChanged;
    public event Action<string>? TargetDisplayMissing;

    public bool IsConnected => _isConnected;
    public ViewportGeometry CurrentGeometry => _currentGeometry;

    /// <summary>
    /// The display device name to target (e.g. "\\.\DISPLAY1"). Empty string means primary screen.
    /// </summary>
    public string TargetDisplay
    {
        get => _targetDisplay;
        set
        {
            if (_targetDisplay != value)
            {
                _targetDisplay = value ?? "";
                RequestGeometryUpdate();
            }
        }
    }

    public BeamTrackingService()
    {
        _currentGeometry = ComputeViewportGeometry(ResolveTargetDisplay(""));
    }


    /// <summary>Compares two ViewportGeometry structs field-by-field.</summary>
    private static bool GeometriesEqual(ViewportGeometry a, ViewportGeometry b)
        => a.Point00.X == b.Point00.X && a.Point00.Y == b.Point00.Y
        && a.Point11.X == b.Point11.X && a.Point11.Y == b.Point11.Y;

    // Pure geometry computation (testable without live screen enumeration)

    /// <summary>
    /// Computes the Beam viewport geometry from a display's bounds in unified-screen
    /// (Windows Virtual Screen) logical-pixel coordinates.
    /// </summary>
    public static ViewportGeometry ComputeViewportGeometry(Rectangle targetBounds)
    {
        return new ViewportGeometry(
            new Eyeware.BeamEyeTracker.Point(targetBounds.Left, targetBounds.Top),
            new Eyeware.BeamEyeTracker.Point(targetBounds.Right, targetBounds.Bottom));
    }

    /// <summary>
    /// Resolves a display device name to its bounds. Returns the primary screen's
    /// bounds when the name is empty or no match is found.
    /// </summary>
    public static Rectangle ResolveTargetDisplay(string? deviceName)
    {
        var primary = Screen.PrimaryScreen!;
        if (string.IsNullOrWhiteSpace(deviceName))
            return primary.Bounds;

        foreach (var s in Screen.AllScreens)
        {
            if (s.DeviceName.Equals(deviceName, StringComparison.OrdinalIgnoreCase))
                return s.Bounds;
        }
        return primary.Bounds;
    }

    // ITrackingService

    public void Start()
    {
        if (_disposed) return;

        Stop();

        try
        {
            var geom = ComputeViewportGeometry(ResolveTargetDisplay(_targetDisplay));
            _currentGeometry = geom;

            _api = new API("GazeStick", geom);

            var status = _api.GetTrackingDataReceptionStatus();
            SetConnected(status == TrackingDataReceptionStatus.ReceivingTrackingData);

            _pollTimer = new System.Threading.Timer(PollGaze, null, 0, 16);

            // Fallback geometry poll (10 s) - guards against missed WM_DISPLAYCHANGE events.
            _geometryPollTimer = new System.Threading.Timer((object? _) => CheckGeometryChange(), null, 10_000, 10_000);

            // Debounce timer (500 ms) - coalesces rapid display-change sequences.
            _debounceTimer = new System.Threading.Timer((object? _) => ApplyPendingUpdate(), null, Timeout.Infinite, Timeout.Infinite);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Beam API initialization failed: {ex.Message}. Make sure the Beam Eye Tracker app is running.");
            SetConnected(false);
        }
    }

    public void Stop()
    {
        _pollTimer?.Dispose();
        _pollTimer = null;

        _geometryPollTimer?.Dispose();
        _geometryPollTimer = null;

        _debounceTimer?.Dispose();
        _debounceTimer = null;

        _verifyTimer?.Dispose();
        _verifyTimer = null;

        _api?.Dispose();
        _api = null;

        SetConnected(false);
    }

    // Geometry change detection & update

    /// <summary>
    /// Called when a display-change event (WM_DISPLAYCHANGE) or the fallback poll
    /// detects a geometry change. Debounces rapid sequences before applying.
    /// </summary>
    public void RequestGeometryUpdate()
    {
        if (_disposed) return;

        var newGeom = ComputeViewportGeometry(ResolveTargetDisplay(_targetDisplay));
        if (GeometriesEqual(newGeom, _currentGeometry)) return; // no actual change

        // Detect target display disappearance before caching the fallback.
        if (!string.IsNullOrWhiteSpace(_targetDisplay))
        {
            bool found = false;
            foreach (var s in Screen.AllScreens)
            {
                if (s.DeviceName.Equals(_targetDisplay, StringComparison.OrdinalIgnoreCase))
                { found = true; break; }
            }
            if (!found)
                TargetDisplayMissing?.Invoke(_targetDisplay);
        }

        _currentGeometry = newGeom;

        // Debounce: wait for virtual-display drivers to settle on their final
        // resolution before applying geometry. Some virtual displays (e.g.
        // Sunshine) report an intermediate resolution right after
        // WM_DISPLAYCHANGE and only reach the real 4K a second or two later.
        _debounceTimer?.Dispose();
        _debounceTimer = new System.Threading.Timer((object? _) => ApplyPendingUpdate(), null, 2500, Timeout.Infinite);
    }

    private void CheckGeometryChange()
    {
        if (_disposed) return;
        RequestGeometryUpdate();
    }

    private void ApplyPendingUpdate()
    {
        if (_disposed || _api == null) return;

        try
        {
            var geom = ComputeViewportGeometry(ResolveTargetDisplay(_targetDisplay));
            if (!GeometriesEqual(geom, _currentGeometry))
                _currentGeometry = geom;

            // Only call the SDK when connected; otherwise the cached geometry is
            // used on the next Start().
            if (_isConnected)
            {
                _api.UpdateViewportGeometry(_currentGeometry);
            }

            ViewportChanged?.Invoke(_currentGeometry);

            // Virtual displays (e.g. Sunshine) can still be settling a few
            // seconds after WM_DISPLAYCHANGE: the resolution we just read may
            // not be the final one. Schedule one verification pass and let it
            // re-apply if the driver reports a different size by then.
            _verifyTimer?.Dispose();
            _verifyTimer = new System.Threading.Timer((object? _) => CheckGeometryChange(), null, 4000, Timeout.Infinite);
        }
        catch (ObjectDisposedException) { }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Viewport geometry update failed: {ex.Message}");
        }
    }

    // Gaze polling

    private void PollGaze(object? state)
    {
        var api = _api;
        if (api == null || _disposed) return;

        try
        {
            var status = api.GetTrackingDataReceptionStatus();
            SetConnected(status == TrackingDataReceptionStatus.ReceivingTrackingData);

            if (status != TrackingDataReceptionStatus.ReceivingTrackingData)
                return;

            bool hasNewData = api.WaitForNewTrackingData(ref _lastTimestamp, 1);
            if (!hasNewData) return;

            using var stateSet = api.GetLatestTrackingStateSet();
            var userState = stateSet.UserState;

            if (userState.TimestampInSeconds == Constants.NullDataTimestamp)
                return;

            var gaze = userState.ViewportGaze;
            if (gaze.Confidence == TrackingConfidence.LostTracking)
                return;

            // Clamp to the viewport so gaze outside the screen still produces a
            // full-deflection stick value instead of being dropped entirely.
            float x = Math.Clamp(gaze.NormalizedPointOfRegard.X, 0.0f, 1.0f);
            float y = Math.Clamp(gaze.NormalizedPointOfRegard.Y, 0.0f, 1.0f);

            GazeReceived?.Invoke(new GazePoint(x, y));
        }
        catch (ObjectDisposedException) { }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Gaze data polling error: {ex.Message}");
            SetConnected(false);
        }
    }

    private void SetConnected(bool connected)
    {
        if (_isConnected != connected)
        {
            _isConnected = connected;
            ConnectionChanged?.Invoke(connected);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Stop();
            _disposed = true;
        }
    }
}
