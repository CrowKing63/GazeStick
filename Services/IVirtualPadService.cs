using GazeStick.Models;

namespace GazeStick.Services;

public interface IVirtualPadService : IDisposable
{
    bool IsConnected { get; }
    event Action<string>? ErrorOccurred;

    bool Initialize(OutputType outputType);
    bool SetOutputType(OutputType outputType);
    void Update(StickOutput output);
    void Reset();
}
