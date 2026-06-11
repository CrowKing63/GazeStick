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
            return;
        }

        if (_settings.PadSlot != _pad.CurrentSlot)
        {
            _settings.PadSlot = _pad.CurrentSlot;
            SettingsManager.Save(_settings);
        }

        _tracker.Start();

        _isActive = _settings.StartActive;
        UpdateTrayIcon();

        _hotkey = new HotkeyManager(_hotkeyWindow.Handle);
        if (!string.IsNullOrEmpty(_settings.ToggleHotkey))
        {
            _hotkey.Register(_settings.ToggleHotkey, ToggleActive);
        }

        _reconnectTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        _reconnectTimer.Tick += (_, _) => { if (!_tracker.IsConnected) _tracker.Start(); };
        _reconnectTimer.Start();
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
        };

        _popup.DeadzoneChanged += v => { _settings.Deadzone = v; SettingsManager.Save(_settings); };
        _popup.SensitivityChanged += v => { _settings.Sensitivity = v; SettingsManager.Save(_settings); };
        _popup.SmoothingChanged += v => { _settings.Smoothing = v; SettingsManager.Save(_settings); };
        _popup.ToggleChanged += v => { _isActive = v; UpdateTrayIcon(); if (!v) _pad.Reset(); };
        _popup.ExitRequested += ExitApplication;
        _popup.HotkeyResetRequested += () => { SettingsManager.Save(_settings); };
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