using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using GazeStick.Helpers;
using GazeStick.Models;
using GazeStick.Services;

namespace GazeStick.UI;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly AppSettings _settings;
    private readonly StickMapper _mapper;
    private readonly IVirtualPadService _pad;
    private readonly ITrackingService _tracker;
    private readonly HotkeyWindow _hotkeyWindow;
    private readonly Icon _appIcon;
    private HotkeyManager? _hotkey;
    private PopupPanel? _popup;
    private bool _isActive;
    private bool _disposed;
    private Icon? _inactiveIcon;
    private bool _inactiveIconCreated;
    private System.Windows.Forms.Timer? _reconnectTimer;
    private int _reconnectAttempts;

    public TrayApplicationContext()
    {
        _appIcon = LoadAppIcon();

        _settings = SettingsManager.Load();
        _mapper = new StickMapper();
        _pad = new VirtualPadService();
        _tracker = new BeamTrackingService();
        _hotkeyWindow = new HotkeyWindow();

        _trayIcon = new NotifyIcon
        {
            Icon = _appIcon,
            Text = "GazeStick — Awaiting Beam",
            Visible = true,
        };
        _trayIcon.MouseClick += OnTrayMouseClick;

        _tracker.GazeReceived += OnGazeReceived;
        _tracker.ConnectionChanged += OnConnectionChanged;
        _tracker.ErrorOccurred += OnError;

        _pad.ErrorOccurred += OnError;

        _hotkeyWindow.HotkeyPressed += ToggleActive;

        InitializeServices();

        if (_settings.ShowOnboarding)
        {
            using var onboarding = new OnboardingForm();
            onboarding.ShowDialog();
            _settings.ShowOnboarding = !onboarding.DontShowAgain;
            SettingsManager.Save(_settings);
        }
    }

    private static Icon LoadAppIcon()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("GazeStick.Resources.icon.ico");
            if (stream != null)
                return new Icon(stream);
        }
        catch { }
        return SystemIcons.Application;
    }

    private void InitializeServices()
    {
        if (!_pad.Initialize(_settings.OutputType))
        {
            _trayIcon.Text = "GazeStick — ViGEm Error";
            var result = MessageBox.Show(
                "ViGEmBus driver is not installed or could not be reached.\n\n" +
                "It is required for gamepad virtualization. Would you like to install it?",
                "GazeStick - ViGEmBus Required",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Error);
            if (result == DialogResult.Yes)
                Process.Start(new ProcessStartInfo("https://github.com/nefarius/ViGEmBus/releases/latest") { UseShellExecute = true });
            return;
        }

        _reconnectAttempts = 0;
        _reconnectTimer = new System.Windows.Forms.Timer();
        _reconnectTimer.Tick += ReconnectTimerTick;

        _tracker.Start();

        _isActive = _settings.StartActive;
        _trayIcon.Icon = _isActive ? _appIcon : GetInactiveIcon();
        UpdateTrayText();

        _hotkey = new HotkeyManager(_hotkeyWindow.Handle);
        if (!string.IsNullOrEmpty(_settings.ToggleHotkey))
        {
            _hotkey.Register(_settings.ToggleHotkey, ToggleActive);
        }

        AutoStartManager.SetEnabled(_settings.StartWithWindows);

        if (!_tracker.IsConnected)
            StartReconnectTimer();

        CheckForUpdateAsync();
    }

    private async void CheckForUpdateAsync()
    {
        try
        {
            var latest = await UpdateChecker.CheckForUpdateAsync();
            if (UpdateChecker.IsNewerAvailable(latest))
            {
                _trayIcon.BalloonTipClicked += (_, _) =>
                    Process.Start(new ProcessStartInfo("https://github.com/CrowKing63/GazeStick/releases/latest")
                        { UseShellExecute = true });
                _trayIcon.ShowBalloonTip(5000, "Update Available",
                    $"GazeStick v{latest} is available. Click here to download.",
                    ToolTipIcon.Info);
            }
        }
        catch { }
    }

    private void OnGazeReceived(GazePoint gaze)
    {
        if (!_isActive) return;
        var output = _mapper.Map(gaze, _settings);
        _pad.Update(output);
    }

    private void OnConnectionChanged(bool connected)
    {
        if (_popup != null && !_popup.IsDisposed)
            _popup.IsBeamConnected = connected;

        if (connected)
        {
            _reconnectAttempts = 0;
            _reconnectTimer?.Stop();
        }
        else
        {
            StartReconnectTimer();
        }

        UpdateTrayText();
    }

    private void OnError(string message)
    {
        try
        {
            var text = $"GazeStick — {message}";
            if (text.Length >= 128)
                text = text[..124] + "...";
            _trayIcon.Text = text;
            _trayIcon.ShowBalloonTip(3000, "GazeStick", message, ToolTipIcon.Warning);
        }
        catch { }
    }

    private void ToggleActive()
    {
        _isActive = !_isActive;
        if (!_isActive)
        {
            _mapper.Reset();
            _pad.Reset();
        }
        _trayIcon.Icon = _isActive ? _appIcon : GetInactiveIcon();
        UpdateTrayText();
        if (_popup != null && !_popup.IsDisposed)
            _popup.IsActive = _isActive;
    }

    private Icon GetInactiveIcon()
    {
        if (_inactiveIconCreated)
            return _inactiveIcon!;

        try
        {
            using var bitmap = _appIcon.ToBitmap();
            for (int x = 0; x < bitmap.Width; x++)
            {
                for (int y = 0; y < bitmap.Height; y++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    int gray = (int)(pixel.R * 0.3 + pixel.G * 0.59 + pixel.B * 0.11);
                    gray = Math.Clamp(gray, 0, 255);
                    bitmap.SetPixel(x, y, Color.FromArgb(pixel.A, gray, gray, gray));
                }
            }
            _inactiveIcon = Icon.FromHandle(bitmap.GetHicon());
            _inactiveIconCreated = true;
        }
        catch
        {
            _inactiveIcon = _appIcon;
        }
        return _inactiveIcon!;
    }

    private void UpdateTrayText()
    {
        string status;

        if (!_tracker.IsConnected)
            status = "Waiting for Beam Eye Tracker...";
        else if (!_isActive)
            status = "OFF  |  Left-click or F9 to toggle";
        else
            status = "ON  |  Left-click or F9 to toggle";

        _trayIcon.Text = $"GazeStick — {status}";
    }

    private void OnTrayMouseClick(object? sender, MouseEventArgs e)
    {
        ShowPopup();
    }

    private void ShowPopup()
    {
        if (_popup != null && !_popup.IsDisposed)
        {
            _popup.BringToFront();
            return;
        }

        _popup = new PopupPanel
        {
            IsActive = _isActive,
            IsBeamConnected = _tracker.IsConnected,
            HotkeyText = _settings.ToggleHotkey,
            InvertY = _settings.InvertY,
            Curve = _settings.Curve,
            CurvePower = _settings.CurvePower,
            AutoStart = _settings.StartWithWindows,
            OutputType = _settings.OutputType,
            DeadzoneValue = _settings.Deadzone,
            SensitivityValue = _settings.Sensitivity,
            SmoothingValue = _settings.Smoothing,
        };

        _popup.DeadzoneChanged += v => { _settings.Deadzone = v; SettingsManager.Save(_settings); };
        _popup.SensitivityChanged += v => { _settings.Sensitivity = v; SettingsManager.Save(_settings); };
        _popup.SmoothingChanged += v => { _settings.Smoothing = v; SettingsManager.Save(_settings); };
        _popup.InvertYChanged += v => { _settings.InvertY = v; SettingsManager.Save(_settings); };
        _popup.CurveTypeChanged += v => { _settings.Curve = v; SettingsManager.Save(_settings); };
        _popup.CurvePowerChanged += v => { _settings.CurvePower = v; SettingsManager.Save(_settings); };
        _popup.AutoStartChanged += v =>
        {
            if (AutoStartManager.SetEnabled(v))
            {
                _settings.StartWithWindows = v;
                SettingsManager.Save(_settings);
                _popup.ShowSettingsNotice(v ? "Start with Windows enabled." : "Start with Windows disabled.");
            }
            else
            {
                _popup.AutoStart = _settings.StartWithWindows;
                OnError("Could not update the Windows startup setting.");
            }
        };
        _popup.OutputTypeChanged += outputType =>
        {
            if (_pad.SetOutputType(outputType))
            {
                _settings.OutputType = outputType;
                SettingsManager.Save(_settings);
            }
            else
            {
                _popup.OutputType = _settings.OutputType;
            }
        };
        _popup.ResetRequested += ResetSettings;
        _popup.ToggleChanged += v => { _isActive = v; UpdateTrayText(); if (!v) _pad.Reset(); };
        _popup.ExitRequested += ExitApplication;
        _popup.HotkeyChanged += key =>
        {
            _settings.ToggleHotkey = key;
            SettingsManager.Save(_settings);
            _hotkey?.Unregister();
            if (!string.IsNullOrEmpty(key))
                _hotkey?.Register(key, ToggleActive);
        };

        var cursorPos = Cursor.Position;
        var screen = Screen.FromPoint(cursorPos).WorkingArea;
        int x = cursorPos.X - _popup.Width / 2;
        int y = cursorPos.Y - _popup.Height - 10;
        x = Math.Clamp(x, screen.Left, screen.Right - _popup.Width);
        y = Math.Max(y, screen.Top);
        _popup.Location = new Point(x, y);

        _popup.TopMost = true;
        _popup.Show();
        _popup.BringToFront();
        _popup.Activate();
    }

    private void ResetSettings()
    {
        if (_popup != null && !_popup.IsDisposed)
            _popup.SuppressAutoClose = true;

        var result = MessageBox.Show(
            "Reset all settings to defaults?",
            "GazeStick — Reset Settings",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (_popup != null && !_popup.IsDisposed)
            _popup.SuppressAutoClose = false;
        if (result != DialogResult.Yes) return;

        _settings.Deadzone = 0.10;
        _settings.Sensitivity = 2.0;
        _settings.Smoothing = 0.30;
        _settings.InvertY = false;
        _settings.ToggleHotkey = "F9";
        _settings.Curve = CurveType.Linear;
        _settings.CurvePower = 2.0;
        _settings.StartActive = true;
        _settings.StartWithWindows = false;
        if (!_pad.SetOutputType(OutputType.Xbox360))
        {
            OnError("Could not restore the default Xbox 360 output mode.");
            return;
        }

        _settings.OutputType = OutputType.Xbox360;
        if (!AutoStartManager.SetEnabled(false))
            OnError("Could not disable the Windows startup setting.");
        SettingsManager.Save(_settings);

        _hotkey?.Unregister();
        _hotkey?.Register("F9", ToggleActive);

        _mapper.Reset();
        _isActive = true;
        _trayIcon.Icon = _appIcon;
        UpdateTrayText();

        if (_popup != null && !_popup.IsDisposed)
        {
            _popup.IsActive = true;
            _popup.DeadzoneValue = _settings.Deadzone;
            _popup.SensitivityValue = _settings.Sensitivity;
            _popup.SmoothingValue = _settings.Smoothing;
            _popup.InvertY = _settings.InvertY;
            _popup.Curve = _settings.Curve;
            _popup.CurvePower = _settings.CurvePower;
            _popup.AutoStart = _settings.StartWithWindows;
            _popup.OutputType = _settings.OutputType;
            _popup.HotkeyText = _settings.ToggleHotkey;
            _popup.ShowSettingsNotice("All settings restored to defaults.");
        }
    }

    private void StartReconnectTimer()
    {
        if (_reconnectTimer == null) return;
        int interval = Math.Clamp(2000 * (1 << Math.Min(_reconnectAttempts, 14)), 1, 30000);
        _reconnectTimer.Interval = interval;
        _reconnectTimer.Start();
    }

    private void ReconnectTimerTick(object? sender, EventArgs e)
    {
        _reconnectTimer?.Stop();
        if (!_tracker.IsConnected)
        {
            _reconnectAttempts++;
            _tracker.Start();
        }
        if (!_tracker.IsConnected)
            StartReconnectTimer();
    }

    private void ExitApplication()
    {
        _trayIcon.Visible = false;
        _trayIcon.Dispose();

        _hotkey?.Dispose();
        _hotkeyWindow.DestroyHandle();
        _reconnectTimer?.Dispose();
        _tracker.Dispose();
        _pad.Reset();
        _pad.Dispose();
        _appIcon.Dispose();

        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _trayIcon?.Dispose();
                _hotkey?.Dispose();
                _hotkeyWindow?.DestroyHandle();
                _reconnectTimer?.Dispose();
                _tracker?.Dispose();
                _pad?.Dispose();
                _appIcon?.Dispose();
            }
            _disposed = true;
        }
        base.Dispose(disposing);
    }

    private sealed class HotkeyWindow : NativeWindow
    {
        private const int WM_HOTKEY = 0x0312;
        public event Action? HotkeyPressed;

        public HotkeyWindow()
        {
            CreateHandle(new CreateParams
            {
                Style = 0,
                ExStyle = 0,
                Parent = IntPtr.Zero,
            });
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                HotkeyPressed?.Invoke();
            }
            base.WndProc(ref m);
        }
    }
}
