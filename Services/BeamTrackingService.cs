using System.Reflection;
using GazeStick.Models;

namespace GazeStick.Services;

public sealed class BeamTrackingService : ITrackingService
{
    private const string BeamSdkDllName = "EyetrackerGazeApi.dll";
    private Assembly? _beamAssembly;
    private object? _tracker;
    private System.Threading.Timer? _pollTimer;
    private bool _disposed;
    private bool _isConnected;

    public event Action<GazePoint>? GazeReceived;
    public event Action<bool>? ConnectionChanged;
    public event Action<string>? ErrorOccurred;

    public bool IsConnected => _isConnected;

    public BeamTrackingService()
    {
        LoadBeamSdk();
    }

    private void LoadBeamSdk()
    {
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string dllPath = Path.Combine(baseDir, "beam-sdk", BeamSdkDllName);
            
            if (!File.Exists(dllPath))
            {
                dllPath = Path.Combine(baseDir, BeamSdkDllName);
            }

            if (!File.Exists(dllPath))
            {
                ErrorOccurred?.Invoke($"Beam SDK DLL을 찾을 수 없습니다: {BeamSdkDllName}. beam-sdk 폴더에 파일을 배치하세요.");
                return;
            }

            _beamAssembly = Assembly.LoadFrom(dllPath);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Beam SDK 로드 실패: {ex.Message}");
        }
    }

    public void Start()
    {
        if (_beamAssembly == null || _disposed) return;

        try
        {
            var trackerType = _beamAssembly.GetType("EyetrackerGazeApi.Tracker");
            if (trackerType == null)
            {
                ErrorOccurred?.Invoke("Beam SDK에서 Tracker 타입을 찾을 수 없습니다.");
                return;
            }

            _tracker = Activator.CreateInstance(trackerType);
            if (_tracker == null)
            {
                ErrorOccurred?.Invoke("Tracker 인스턴스 생성 실패.");
                return;
            }

            var connectMethod = trackerType.GetMethod("Connect");
            connectMethod?.Invoke(_tracker, null);

            _pollTimer = new System.Threading.Timer(PollGaze, null, 0, 16); // ~60fps
            SetConnected(true);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Beam 연결 실패: {ex.Message}");
            SetConnected(false);
        }
    }

    public void Stop()
    {
        _pollTimer?.Dispose();
        _pollTimer = null;

        if (_tracker != null)
        {
            try
            {
                var disconnectMethod = _tracker.GetType().GetMethod("Disconnect");
                disconnectMethod?.Invoke(_tracker, null);
            }
            catch { }
            _tracker = null;
        }

        SetConnected(false);
    }

    private void PollGaze(object? state)
    {
        if (_tracker == null || _disposed) return;

        try
        {
            var getGazeMethod = _tracker.GetType().GetMethod("GetGaze");
            if (getGazeMethod == null) return;

            var gazeResult = getGazeMethod.Invoke(_tracker, null);
            if (gazeResult == null) return;

            var xProp = gazeResult.GetType().GetProperty("X");
            var yProp = gazeResult.GetType().GetProperty("Y");
            var isValidProp = gazeResult.GetType().GetProperty("IsValid");

            if (xProp == null || yProp == null) return;

            double x = Convert.ToDouble(xProp.GetValue(gazeResult));
            double y = Convert.ToDouble(yProp.GetValue(gazeResult));

            bool isValid = isValidProp != null && Convert.ToBoolean(isValidProp.GetValue(gazeResult));

            if (isValid && x >= 0.0 && x <= 1.0 && y >= 0.0 && y <= 1.0)
            {
                GazeReceived?.Invoke(new GazePoint(x, y));
            }
        }
        catch
        {
            // Ignore transient errors
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