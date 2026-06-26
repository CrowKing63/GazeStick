using Eyeware.BeamEyeTracker;
using GazeStick.Models;

namespace GazeStick.Services;

public sealed class BeamTrackingService : ITrackingService
{
    private API? _api;
    private System.Threading.Timer? _pollTimer;
    private double _lastTimestamp = Constants.NullDataTimestamp;
    private bool _disposed;
    private bool _isConnected;

    public event Action<GazePoint>? GazeReceived;
    public event Action<bool>? ConnectionChanged;
    public event Action<string>? ErrorOccurred;

    public bool IsConnected => _isConnected;

    public BeamTrackingService()
    {
    }

    public void Start()
    {
        if (_disposed) return;

        // Clean up existing resources before re-initializing
        Stop();

        try
        {
            var viewportGeom = new ViewportGeometry(
                new Eyeware.BeamEyeTracker.Point(0, 0),
                new Eyeware.BeamEyeTracker.Point(GetSystemMetrics(0), GetSystemMetrics(1))
            );

            _api = new API("GazeStick", viewportGeom);

            var status = _api.GetTrackingDataReceptionStatus();
            SetConnected(status == TrackingDataReceptionStatus.ReceivingTrackingData);

            _pollTimer = new System.Threading.Timer(PollGaze, null, 0, 16);
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

        _api?.Dispose();
        _api = null;

        SetConnected(false);
    }

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

            float x = gaze.NormalizedPointOfRegard.X;
            float y = gaze.NormalizedPointOfRegard.Y;

            if (x >= 0.0f && x <= 1.0f && y >= 0.0f && y <= 1.0f)
            {
                GazeReceived?.Invoke(new GazePoint(x, y));
            }
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

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    public void Dispose()
    {
        if (!_disposed)
        {
            Stop();
            _disposed = true;
        }
    }
}