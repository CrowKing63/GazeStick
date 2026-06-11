using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using GazeStick.Models;

namespace GazeStick.Services;

public sealed class VirtualPadService : IVirtualPadService
{
    private readonly ViGEmClient _client;
    private IXbox360Controller? _controller;
    private int _currentSlot = 0;
    private bool _disposed;

    public bool IsConnected => _controller != null;
    public int CurrentSlot => _currentSlot;
    public event Action<int>? SlotChanged;
    public event Action<string>? ErrorOccurred;

    public VirtualPadService()
    {
        _client = new ViGEmClient();
    }

    public bool Initialize(int preferredSlot = 0)
    {
        int slot = preferredSlot;
        if (slot == 0)
            slot = FindAvailableSlot();

        if (!TryCreateController(slot))
        {
            for (int i = 1; i <= 4; i++)
            {
                if (i == slot) continue;
                if (TryCreateController(i))
                {
                    slot = i;
                    break;
                }
            }
        }

        if (_controller == null)
        {
            ErrorOccurred?.Invoke("사용 가능한 가상 패드 슬롯이 없습니다.");
            return false;
        }

        _currentSlot = slot;
        SlotChanged?.Invoke(_currentSlot);
        return true;
    }

    private bool TryCreateController(int slot)
    {
        try
        {
            var ctrl = _client.CreateXbox360Controller();
            ctrl.Connect();

            var userIndex = ctrl.UserIndex;
            if (userIndex < 0 || userIndex > 3)
                userIndex = slot - 1;

            if (userIndex + 1 != slot)
            {
                ctrl.Disconnect();
                ((IDisposable)ctrl).Dispose();
                return false;
            }

            _controller?.Disconnect();
            if (_controller is IDisposable oldCtrl)
                oldCtrl.Dispose();

            _controller = ctrl;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private int FindAvailableSlot()
    {
        for (int i = 1; i <= 4; i++)
        {
            try
            {
                var ctrl = _client.CreateXbox360Controller();
                ctrl.Connect();
                _currentSlot = (int)ctrl.UserIndex + 1;
                _controller = ctrl;
                return _currentSlot;
            }
            catch { }
        }
        return 1;
    }

    public void SetSlot(int slot)
    {
        if (slot < 1 || slot > 4) return;
        if (slot == _currentSlot) return;

        if (TryCreateController(slot))
        {
            _currentSlot = slot;
            SlotChanged?.Invoke(_currentSlot);
        }
        else
        {
            int fallback = FindAvailableSlot();
            if (fallback != _currentSlot)
            {
                _currentSlot = fallback;
                SlotChanged?.Invoke(_currentSlot);
                ErrorOccurred?.Invoke($"슬롯 {slot} 사용 중. 슬롯 {fallback}으로 변경됨.");
            }
        }
    }

    public void Update(StickOutput output)
    {
        if (_controller == null) return;

        try
        {
            _controller.SetAxisValue(Xbox360Axis.RightThumbX.Id, output.X);
            _controller.SetAxisValue(Xbox360Axis.RightThumbY.Id, output.Y);
            _controller.SubmitReport();
        }
        catch
        {
            // Ignore transient errors
        }
    }

    public void Reset()
    {
        if (_controller == null) return;

        try
        {
            _controller.SetAxisValue(Xbox360Axis.RightThumbX.Id, (short)0);
            _controller.SetAxisValue(Xbox360Axis.RightThumbY.Id, (short)0);
            _controller.SubmitReport();
        }
        catch { }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_controller != null)
            {
                try { _controller.Disconnect(); } catch { }
                if (_controller is IDisposable ctrl)
                    ctrl.Dispose();
            }
            _client.Dispose();
            _disposed = true;
        }
    }
}