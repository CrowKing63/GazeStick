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
        // Just create a controller and accept whatever slot ViGEm assigns
        if (!TryCreateController())
        {
            ErrorOccurred?.Invoke("사용 가능한 가상 패드 슬롯이 없습니다.\nViGEmBus 드라이버가 설치되어 있는지 확인하세요.");
            return false;
        }

        SlotChanged?.Invoke(_currentSlot);
        return true;
    }

    private bool TryCreateController()
    {
        try
        {
            var oldCtrl = _controller;

            var ctrl = _client.CreateXbox360Controller();
            ctrl.Connect();

            _controller = ctrl;
            _currentSlot = (int)ctrl.UserIndex + 1;

            // Clean up old controller after successful new connection
            if (oldCtrl != null)
            {
                try { oldCtrl.Disconnect(); } catch { }
                if (oldCtrl is IDisposable d) d.Dispose();
            }

            return true;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"가상 패드 생성 실패: {ex.Message}");
            return false;
        }
    }

    public void SetSlot(int slot)
    {
        // ViGEm assigns slots automatically; we can't force a specific one.
        // Just create a fresh controller.
        if (TryCreateController())
            SlotChanged?.Invoke(_currentSlot);
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