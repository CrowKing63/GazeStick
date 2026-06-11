using GazeStick.Models;

namespace GazeStick.Services;

public interface IVirtualPadService : IDisposable
{
    bool IsConnected { get; }
    int CurrentSlot { get; }
    event Action<int>? SlotChanged;
    event Action<string>? ErrorOccurred;

    bool Initialize(int preferredSlot = 0);
    void SetSlot(int slot);
    void Update(StickOutput output);
    void Reset();
}