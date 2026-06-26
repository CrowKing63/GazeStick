using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using GazeStick.Models;

namespace GazeStick.Services;

public sealed class VirtualPadService : IVirtualPadService
{
    private ViGEmClient? _client;
    private IXbox360Controller? _controller;
    private int _currentSlot;
    private bool _disposed;
    private readonly object _lock = new();

    public bool IsConnected => _controller != null;
    public int CurrentSlot => _currentSlot;
    public event Action<int>? SlotChanged;
    public event Action<string>? ErrorOccurred;

    public bool Initialize(int preferredSlot = 0)
    {
        try
        {
            _client = new ViGEmClient();
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"ViGEmBus initialization failed: {ex.Message}\nMake sure the driver is installed.");
            return false;
        }

        if (!TryCreateController())
        {
            ErrorOccurred?.Invoke("No available virtual pad slots.\nMake sure the ViGEmBus driver is installed.");
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

            var ctrl = _client!.CreateXbox360Controller();
            ctrl.Connect();

            int slot = 1;
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    slot = (int)ctrl.UserIndex + 1;
                    break;
                }
                catch
                {
                    Thread.Sleep(10);
                }
            }

            _controller = ctrl;
            _currentSlot = slot;

            ResetControllerState();

            if (oldCtrl != null)
            {
                try { oldCtrl.Disconnect(); } catch { }
                if (oldCtrl is IDisposable d) d.Dispose();
            }

            return true;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Virtual pad creation failed: {ex.Message}");
            return false;
        }
    }

    private void ResetControllerState()
    {
        if (_controller == null) return;

        // Explicitly initialize ALL controller state to prevent random
        // button presses or stick movement from uninitialized report buffer.

        // Reset all digital buttons (15 buttons)
        _controller.SetButtonState(Xbox360Button.Up, false);
        _controller.SetButtonState(Xbox360Button.Down, false);
        _controller.SetButtonState(Xbox360Button.Left, false);
        _controller.SetButtonState(Xbox360Button.Right, false);
        _controller.SetButtonState(Xbox360Button.Start, false);
        _controller.SetButtonState(Xbox360Button.Back, false);
        _controller.SetButtonState(Xbox360Button.Guide, false);
        _controller.SetButtonState(Xbox360Button.LeftThumb, false);
        _controller.SetButtonState(Xbox360Button.RightThumb, false);
        _controller.SetButtonState(Xbox360Button.A, false);
        _controller.SetButtonState(Xbox360Button.B, false);
        _controller.SetButtonState(Xbox360Button.X, false);
        _controller.SetButtonState(Xbox360Button.Y, false);
        _controller.SetButtonState(Xbox360Button.LeftShoulder, false);
        _controller.SetButtonState(Xbox360Button.RightShoulder, false);

        // Reset all analog axes to neutral
        _controller.SetAxisValue(Xbox360Axis.LeftThumbX, 0);
        _controller.SetAxisValue(Xbox360Axis.LeftThumbY, 0);
        _controller.SetAxisValue(Xbox360Axis.RightThumbX, 0);
        _controller.SetAxisValue(Xbox360Axis.RightThumbY, 0);

        // Reset triggers to released (0 = no pull)
        _controller.SetSliderValue(Xbox360Slider.LeftTrigger, (byte)0);
        _controller.SetSliderValue(Xbox360Slider.RightTrigger, (byte)0);

        // Submit the fully-initialized report once
        _controller.SubmitReport();
    }

    public void SetSlot(int slot)
    {
        if (TryCreateController())
            SlotChanged?.Invoke(_currentSlot);
    }

    public void Update(StickOutput output)
    {
        lock (_lock)
        {
            if (_controller == null) return;

            try
            {
                _controller.SetAxisValue(Xbox360Axis.RightThumbX, output.X);
                _controller.SetAxisValue(Xbox360Axis.RightThumbY, output.Y);
                _controller.SubmitReport();
            }
            catch
            {
            }
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            if (_controller == null) return;

            try
            {
                _controller.SetAxisValue(Xbox360Axis.RightThumbX, 0);
                _controller.SetAxisValue(Xbox360Axis.RightThumbY, 0);
                _controller.SubmitReport();
            }
            catch { }
        }
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
            _client?.Dispose();
            _disposed = true;
        }
    }
}