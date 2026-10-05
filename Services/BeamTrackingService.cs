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

            RecreateApi(geom);

            var status = _api!.GetTrackingDataReceptionStatus();
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

    /// <summary>
    /// Disposes the existing Beam SDK handle and creates a fresh one with the
    /// supplied viewport geometry. This is the same initialization path the app
    /// takes on startup (new API created against the current display bounds), so
    /// a monitor hot-swap re-runs exactly that path instead of relying on an
    /// in-place UpdateViewportGeometry call that leaves stale internal state.
    /// </summary>
    private void RecreateApi(ViewportGeometry geom)
    {
        _api?.Dispose();
        _api = null;
        _api = new API("GazeStick", geom);
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
        if (_disposed) return;

        try
        {
            // Full re-initialization: Stop() disposes all timers and the SDK
            // handle, then Start() re-runs the exact same initialization path
            // used at app startup (fresh geometry read, new API creation,
            // timer setup). This reproduces "app restart" behaviour without
            // actually restarting the process.
            Stop();
            Start();

            ViewportChanged?.Invoke(_currentGeometry);

            // Virtual displays (e.g. Sunshine) take 5-10 seconds to settle on
            // their final resolution after WM_DISPLAYCHANGE. A single verify at
            // 4 s is not enough. Schedule repeated checks every 2 s for up to
            // 10 s, stopping early if the geometry stops changing.
            _verifyTimer?.Dispose();
            _verifyTimer = new System.Threading.Timer((object? _) => VerifySettling(0), null, 2000, Timeout.Infinite);
        }
        catch (ObjectDisposedException) { }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Viewport geometry update failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Repeatedly re-checks the display geometry every 2 s for up to 10 s
    /// (5 passes). If the geometry changes between passes, re-applies the full
    /// Stop/Start re-init and continues checking. Stops when the geometry is
    /// stable or the budget is exhausted.
    /// </summary>
    private void VerifySettling(int pass)
    {
        if (_disposed) return;

        const int MaxPasses = 5; // 5 × 2 s = 10 s total window

        if (pass >= MaxPasses) return;

        try
        {
            var geom = ComputeViewportGeometry(ResolveTargetDisplay(_targetDisplay));
            if (!GeometriesEqual(geom, _currentGeometry))
            {
                // Geometry changed since last read — driver settled on a new value.
                _currentGeometry = geom;
                Stop();
                Start();
                ViewportChanged?.Invoke(_currentGeometry);
            }

            // Schedule the next check if we still have budget.
            _verifyTimer?.Dispose();
            _verifyTimer = new System.Threading.Timer((object? _) => VerifySettling(pass + 1), null, 2000, Timeout.Infinite);
        }
        catch (ObjectDisposedException) { }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Geometry verify failed: {ex.Message}");
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
