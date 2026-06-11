using System.Diagnostics;
using System.Drawing;
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
    private HotkeyManager? _hotkey;
    private PopupPanel? _popup;
    private bool _isActive;
    private bool _disposed;
    private System.Windows.Forms.Timer? _reconnectTimer;
    private int _reconnectAttempts;

    public TrayApplicationContext()
    {
        _settings = SettingsManager.Load();
        _mapper = new StickMapper();
        _pad = new VirtualPadService();
        _tracker = new BeamTrackingService();
        _hotkeyWindow = new HotkeyWindow();

        _trayIcon = new NotifyIcon
        {
            Icon = CreateIcon(Color.Orange),
            Text = "GazeStick — Beam 대기 중",
            Visible = true,
        };
        _trayIcon.MouseClick += OnTrayMouseClick;

        _tracker.GazeReceived += OnGazeReceived;
        _tracker.ConnectionChanged += OnConnectionChanged;
        _tracker.ErrorOccurred += OnError;

        _pad.ErrorOccurred += OnError;
        _pad.SlotChanged += OnSlotChanged;

        _hotkeyWindow.HotkeyPressed += ToggleActive;

        InitializeServices();
    }

    private void InitializeServices()
    {
        if (!_pad.Initialize(_settings.PadSlot))
        {
            _trayIcon.Icon = CreateIcon(Color.Red);
            _trayIcon.Text = "GazeStick — ViGEm 오류";
            var result = MessageBox.Show(
                "ViGEmBus 드라이버가 설치되지 않았거나 연결할 수 없습니다.\n\n" +
                "게임패드 가상화에 필요합니다. 설치하시겠습니까?",
                "GazeStick - ViGEmBus 필요",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Error);
            if (result == DialogResult.Yes)
                Process.Start(new ProcessStartInfo("https://github.com/nefarius/ViGEmBus/releases/latest") { UseShellExecute = true });
            return;
        }

        if (_settings.PadSlot != _pad.CurrentSlot)
        {
            _settings.PadSlot = _pad.CurrentSlot;
            SettingsManager.Save(_settings);
        }

        _reconnectAttempts = 0;
        _reconnectTimer = new System.Windows.Forms.Timer();
        _reconnectTimer.Tick += ReconnectTimerTick;

        _tracker.Start();

        _isActive = _settings.StartActive;
        UpdateTrayIcon();

        _hotkey = new HotkeyManager(_hotkeyWindow.Handle);
        if (!string.IsNullOrEmpty(_settings.ToggleHotkey))
        {
            _hotkey.Register(_settings.ToggleHotkey, ToggleActive);
        }

        AutoStartManager.SetEnabled(_settings.StartWithWindows);

        if (!_tracker.IsConnected)
            StartReconnectTimer();
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

        UpdateTrayIcon();
    }

    private void OnError(string message)
    {
        _trayIcon.Text = $"GazeStick — {message}";
        _trayIcon.ShowBalloonTip(3000, "GazeStick", message, ToolTipIcon.Warning);
    }

    private void OnSlotChanged(int slot)
    {
        _settings.PadSlot = slot;
        SettingsManager.Save(_settings);
        if (_popup != null && !_popup.IsDisposed)
            _popup.PadSlot = slot;
    }

    private void ToggleActive()
    {
        _isActive = !_isActive;
        if (!_isActive)
        {
            _mapper.Reset();
            _pad.Reset();
        }
        UpdateTrayIcon();
        if (_popup != null && !_popup.IsDisposed)
            _popup.IsActive = _isActive;
    }

    private void UpdateTrayIcon()
    {
        Color color;
        string status;

        if (!_tracker.IsConnected)
        {
            color = Color.Orange;
            status = "Beam 대기 중";
        }
        else if (!_isActive)
        {
            color = Color.Orange;
            status = "OFF";
        }
        else
        {
            color = Color.LimeGreen;
            status = "ON";
        }

        _trayIcon.Icon = CreateIcon(color);
        _trayIcon.Text = $"GazeStick — {status} (슬롯 #{_pad.CurrentSlot})";
    }

    private void OnTrayMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            ToggleActive();
        }
        else if (e.Button == MouseButtons.Right)
        {
            ShowPopup();
        }
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
            PadSlot = _pad.CurrentSlot,
            HotkeyText = _settings.ToggleHotkey,
            InvertY = _settings.InvertY,
            Curve = _settings.Curve,
            CurvePower = _settings.CurvePower,
            AutoStart = _settings.StartWithWindows,
        };

        _popup.DeadzoneChanged += v => { _settings.Deadzone = v; SettingsManager.Save(_settings); };
        _popup.SensitivityChanged += v => { _settings.Sensitivity = v; SettingsManager.Save(_settings); };
        _popup.SmoothingChanged += v => { _settings.Smoothing = v; SettingsManager.Save(_settings); };
        _popup.InvertYChanged += v => { _settings.InvertY = v; SettingsManager.Save(_settings); };
        _popup.CurveTypeChanged += v => { _settings.Curve = v; SettingsManager.Save(_settings); };
        _popup.CurvePowerChanged += v => { _settings.CurvePower = v; SettingsManager.Save(_settings); };
        _popup.AutoStartChanged += v =>
        {
            _settings.StartWithWindows = v;
            SettingsManager.Save(_settings);
            AutoStartManager.SetEnabled(v);
        };
        _popup.ResetRequested += ResetSettings;
        _popup.ToggleChanged += v => { _isActive = v; UpdateTrayIcon(); if (!v) _pad.Reset(); };
        _popup.ExitRequested += ExitApplication;
        _popup.HotkeyChanged += key =>
        {
            _settings.ToggleHotkey = key;
            SettingsManager.Save(_settings);
            _hotkey?.Unregister();
            if (!string.IsNullOrEmpty(key))
                _hotkey?.Register(key, ToggleActive);
        };
        _popup.SlotChangeRequested += () =>
        {
            int next = _pad.CurrentSlot % 4 + 1;
            _pad.SetSlot(next);
        };

        var cursorPos = Cursor.Position;
        _popup.Location = new Point(
            cursorPos.X - _popup.Width / 2,
            cursorPos.Y - _popup.Height - 10);

        _popup.Show();
    }

    private void ResetSettings()
    {
        var result = MessageBox.Show(
            "모든 설정을 기본값으로 되돌리겠습니까?",
            "GazeStick — 설정 초기화",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (result != DialogResult.Yes) return;

        _settings.Deadzone = 0.10;
        _settings.Sensitivity = 1.0;
        _settings.Smoothing = 0.30;
        _settings.InvertY = false;
        _settings.ToggleHotkey = "F9";
        _settings.Curve = CurveType.Linear;
        _settings.CurvePower = 2.0;
        _settings.StartActive = true;
        _settings.StartWithWindows = true;
        SettingsManager.Save(_settings);

        _hotkey?.Unregister();
        _hotkey?.Register("F9", ToggleActive);

        _popup?.Close();
    }

    private void StartReconnectTimer()
    {
        if (_reconnectTimer == null) return;
        int interval = Math.Min(2000 * (1 << _reconnectAttempts), 30000);
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

        Application.Exit();
    }

    private static Icon CreateIcon(Color color)
    {
        var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        int cx = 8, cy = 8;
        g.DrawEllipse(new Pen(color, 1.5f), 1, 4, 14, 8);
        g.FillEllipse(new SolidBrush(color), cx - 1, cy - 1, 3, 3);
        g.DrawLine(new Pen(color, 1.5f), 1, cy, 15, cy);
        g.DrawEllipse(new Pen(color, 1f), 0, 3, 16, 10);

        return Icon.FromHandle(bmp.GetHicon());
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
                ClassName = "GazeStickHotkeyWindow",
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