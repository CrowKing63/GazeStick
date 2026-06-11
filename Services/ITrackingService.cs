using GazeStick.Models;

namespace GazeStick.Services;

public interface ITrackingService : IDisposable
{
    event Action<GazePoint>? GazeReceived;
    event Action<bool>? ConnectionChanged;
    event Action<string>? ErrorOccurred;

    bool IsConnected { get; }
    void Start();
    void Stop();
}