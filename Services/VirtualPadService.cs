using GazeStick.Models;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.DualShock4;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace GazeStick.Services;

public sealed class VirtualPadService : IVirtualPadService
{
    private const ushort FixedVendorId = 0x1209;
    private const ushort Xbox360ProductId = 0xAB10;
    private const ushort DualShock4ProductId = 0xAB11;

    private ViGEmClient? _client;
    private IVirtualGamepad? _controller;
    private OutputType _outputType = OutputType.Xbox360;
    private bool _disposed;
    private readonly object _lock = new();

    public bool IsConnected => _controller != null;
    public event Action<string>? ErrorOccurred;

    public bool Initialize(OutputType outputType)
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

        return CreateController(outputType);
    }

    public bool SetOutputType(OutputType outputType)
    {
        lock (_lock)
        {
            if (_controller != null && _outputType == outputType)
                return true;

            DisconnectController();
            return CreateController(outputType);
        }
    }

    private bool CreateController(OutputType outputType)
    {
        try
        {
            _controller = outputType switch
            {
                OutputType.DualShock4 => _client!.CreateDualShock4Controller(FixedVendorId, DualShock4ProductId),
                _ => _client!.CreateXbox360Controller(FixedVendorId, Xbox360ProductId),
            };
            _controller.Connect();
            _outputType = outputType;
            ResetControllerState();
            return true;
        }
        catch (Exception ex)
        {
            DisconnectController();
            ErrorOccurred?.Invoke($"Virtual pad creation failed: {ex.Message}");
            return false;
        }
    }

    private void ResetControllerState()
    {
        switch (_controller)
        {
            case IXbox360Controller xbox:
                xbox.ResetReport();
                xbox.SubmitReport();
                break;
            case IDualShock4Controller ds4:
                ds4.ResetReport();
                ds4.SetAxisValue(DualShock4Axis.RightThumbX, 128);
                ds4.SetAxisValue(DualShock4Axis.RightThumbY, 128);
                ds4.SubmitReport();
                break;
        }
    }

    public void Update(StickOutput output)
    {
        lock (_lock)
        {
            try
            {
                switch (_controller)
                {
                    case IXbox360Controller xbox:
                        xbox.SetAxisValue(Xbox360Axis.RightThumbX, output.X);
                        xbox.SetAxisValue(Xbox360Axis.RightThumbY, output.Y);
                        xbox.SubmitReport();
                        break;
                    case IDualShock4Controller ds4:
                        ds4.SetAxisValue(DualShock4Axis.RightThumbX, VirtualPadAxisConverter.ToDualShock4X(output.X));
                        ds4.SetAxisValue(DualShock4Axis.RightThumbY, VirtualPadAxisConverter.ToDualShock4Y(output.Y));
                        ds4.SubmitReport();
                        break;
                }
            }
            catch
            {
                // Keep tracking alive if the driver disconnects unexpectedly.
            }
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            try
            {
                ResetControllerState();
            }
            catch { }
        }
    }

    private void DisconnectController()
    {
        if (_controller == null) return;

        try { ResetControllerState(); } catch { }
        try { _controller.Disconnect(); } catch { }
        if (_controller is IDisposable disposable)
            disposable.Dispose();
        _controller = null;
    }

    public void Dispose()
    {
        if (_disposed) return;

        lock (_lock)
        {
            DisconnectController();
            _client?.Dispose();
            _disposed = true;
        }
    }
}
