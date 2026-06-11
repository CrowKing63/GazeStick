using System.Drawing;
using System.Windows.Forms;
using GazeStick.Models;

namespace GazeStick.UI;

public class PopupPanel : Form
{
    private readonly Label _lblStatus;
    private readonly Label _lblBeamStatus;
    private readonly Label _lblSlotInfo;
    private readonly Label _lblHotkeyBadge;
    private readonly Label _lblDeadzoneTitle;
    private readonly Label _lblSensitivityTitle;
    private readonly Label _lblSmoothingTitle;
    private readonly NumericAdjuster _deadzoneCtrl;
    private readonly NumericAdjuster _sensitivityCtrl;
    private readonly NumericAdjuster _smoothingCtrl;
    private readonly Button _btnExit;
    private readonly ToggleButton _toggleBtn;
    private bool _isActive;
    private bool _isBeamConnected;
    private bool _awaitingHotkey;
    private int _padSlot;

    public event Action<bool>? ToggleChanged;
    public event Action<double>? DeadzoneChanged;
    public event Action<double>? SensitivityChanged;
    public event Action<double>? SmoothingChanged;
    public event Action? HotkeyResetRequested;
    public event Action? SlotChangeRequested;
    public event Action? ExitRequested;

    public bool IsActive
    {
        get => _isActive;
        set { _isActive = value; UpdateState(); }
    }

    public bool IsBeamConnected
    {
        get => _isBeamConnected;
        set { _isBeamConnected = value; UpdateBeamStatus(); }
    }

    public int PadSlot
    {
        get => _padSlot;
        set { _padSlot = value; _lblSlotInfo.Text = $"패드 슬롯 #{value}"; }
    }

    public string HotkeyText
    {
        set => _lblHotkeyBadge.Text = value;
    }

    public PopupPanel()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(256, 320);
        BackColor = Color.FromArgb(28, 28, 30);
        ShowInTaskbar = false;
        TopMost = true;
        Deactivate += (_, _) => Close();
        KeyPreview = true;
        KeyDown += OnPanelKeyDown;

        int y = 8;

        // Header
        var headerPanel = new Panel
        {
            Location = new Point(8, y),
            Size = new Size(240, 28),
            BackColor = Color.Transparent,
        };

        var appIcon = new Label
        {
            Text = "GazeStick",
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(0, 0),
            AutoSize = true,
        };
        _lblStatus = new Label
        {
            Text = "●",
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.LimeGreen,
            Location = new Point(appIcon.Width + 6, 4),
            AutoSize = true,
        };
        headerPanel.Controls.AddRange(new[] { appIcon, _lblStatus });
        y += 34;

        // Toggle Button
        _toggleBtn = new ToggleButton
        {
            Location = new Point(8, y),
            Size = new Size(240, 32),
        };
        _toggleBtn.Toggled += val => ToggleChanged?.Invoke(val);
        Controls.Add(_toggleBtn);
        y += 40;

        // Deadzone
        _lblDeadzoneTitle = CreateLabel("Deadzone", y);
        y += 20;
        _deadzoneCtrl = new NumericAdjuster { Location = new Point(8, y) };
        _deadzoneCtrl.Initialize(0.10, 0.0, 0.50, 0.01, 2);
        _deadzoneCtrl.ValueChanged += v => DeadzoneChanged?.Invoke(v);
        y += 42;

        // Sensitivity
        _lblSensitivityTitle = CreateLabel("Sensitivity", y);
        y += 20;
        _sensitivityCtrl = new NumericAdjuster { Location = new Point(8, y) };
        _sensitivityCtrl.Initialize(1.0, 0.1, 5.0, 0.1, 1);
        _sensitivityCtrl.ValueChanged += v => SensitivityChanged?.Invoke(v);
        y += 42;

        // Smoothing
        _lblSmoothingTitle = CreateLabel("Smoothing", y);
        y += 20;
        _smoothingCtrl = new NumericAdjuster { Location = new Point(8, y) };
        _smoothingCtrl.Initialize(0.30, 0.0, 0.9, 0.05, 2);
        _smoothingCtrl.ValueChanged += v => SmoothingChanged?.Invoke(v);
        y += 42;

        // Footer
        var footerPanel = new Panel
        {
            Location = new Point(8, y),
            Size = new Size(240, 44),
            BackColor = Color.Transparent,
        };

        _lblHotkeyBadge = new Label
        {
            Text = "F9",
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 120, 200),
            BackColor = Color.FromArgb(40, 40, 45),
            AutoSize = true,
            Padding = new Padding(6, 2, 6, 2),
            TextAlign = ContentAlignment.MiddleCenter,
        };
        _lblHotkeyBadge.Click += (_, _) => StartHotkeyCapture();
        _lblHotkeyBadge.MouseEnter += (_, _) => _lblHotkeyBadge.BackColor = Color.FromArgb(55, 55, 60);
        _lblHotkeyBadge.MouseLeave += (_, _) => _lblHotkeyBadge.BackColor = Color.FromArgb(40, 40, 45);

        _lblSlotInfo = new Label
        {
            Text = "패드 슬롯 #2",
            Font = new Font("Segoe UI", 8f),
            ForeColor = Color.FromArgb(140, 140, 145),
            AutoSize = true,
            Location = new Point(0, 18),
        };
        _lblSlotInfo.Click += (_, _) => SlotChangeRequested?.Invoke();
        _lblSlotInfo.MouseEnter += (_, _) => _lblSlotInfo.ForeColor = Color.White;
        _lblSlotInfo.MouseLeave += (_, _) => _lblSlotInfo.ForeColor = Color.FromArgb(140, 140, 145);

        _lblBeamStatus = new Label
        {
            Text = "○ Beam 연결됨",
            Font = new Font("Segoe UI", 8f),
            ForeColor = Color.FromArgb(140, 140, 145),
            AutoSize = true,
            Location = new Point(0, 0),
        };

        _btnExit = new Button
        {
            Text = "종료",
            Font = new Font("Segoe UI", 8f),
            ForeColor = Color.FromArgb(200, 80, 80),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(180, 16),
            Cursor = Cursors.Hand,
        };
        _btnExit.FlatAppearance.BorderSize = 0;
        _btnExit.Click += (_, _) => ExitRequested?.Invoke();

        footerPanel.Controls.AddRange(new Control[] { _lblBeamStatus, _lblSlotInfo, _lblHotkeyBadge, _btnExit });
        Controls.AddRange(new Control[] { headerPanel, _lblDeadzoneTitle, _deadzoneCtrl, _lblSensitivityTitle, _sensitivityCtrl, _lblSmoothingTitle, _smoothingCtrl, footerPanel });

        UpdateState();
    }

    private Label CreateLabel(string text, int y)
    {
        var lbl = new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            ForeColor = Color.FromArgb(160, 160, 165),
            Location = new Point(12, y),
            AutoSize = true,
        };
        return lbl;
    }

    private void UpdateState()
    {
        _lblStatus.Text = _isActive ? "●" : "○";
        _lblStatus.ForeColor = _isActive ? Color.LimeGreen : Color.Gray;
        _toggleBtn.IsOn = _isActive;
        _deadzoneCtrl.SetEnabledState(_isActive);
        _sensitivityCtrl.SetEnabledState(_isActive);
        _smoothingCtrl.SetEnabledState(_isActive);
    }

    private void UpdateBeamStatus()
    {
        _lblBeamStatus.Text = _isBeamConnected ? "● Beam 연결됨" : "○ Beam 없음";
        _lblBeamStatus.ForeColor = _isBeamConnected ? Color.LimeGreen : Color.FromArgb(200, 150, 0);
    }

    private void StartHotkeyCapture()
    {
        _awaitingHotkey = true;
        _lblHotkeyBadge.Text = "키 입력...";
        _lblHotkeyBadge.ForeColor = Color.Yellow;
    }

    private void OnPanelKeyDown(object? sender, KeyEventArgs e)
    {
        if (_awaitingHotkey)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            var keyStr = e.KeyCode.ToString();
            if (e.Control) keyStr = "Ctrl+" + keyStr;
            if (e.Shift) keyStr = "Shift+" + keyStr;
            if (e.Alt) keyStr = "Alt+" + keyStr;

            _lblHotkeyBadge.Text = keyStr;
            _lblHotkeyBadge.ForeColor = Color.FromArgb(100, 120, 200);
            _awaitingHotkey = false;
            HotkeyResetRequested?.Invoke();
        }
        else if (e.KeyCode == Keys.Escape)
        {
            Close();
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        UpdateState();
        UpdateBeamStatus();
    }

    private class ToggleButton : Control
    {
        private bool _isOn;

        public bool IsOn
        {
            get => _isOn;
            set { _isOn = value; Invalidate(); }
        }

        public event Action<bool>? Toggled;

        public ToggleButton()
        {
            DoubleBuffered = true;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.StandardClick, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);

            using var bgBrush = new SolidBrush(_isOn ? Color.FromArgb(30, 60, 30) : Color.FromArgb(45, 45, 48));
            using var borderPen = new Pen(_isOn ? Color.FromArgb(50, 180, 50) : Color.FromArgb(80, 80, 80), 2);
            using var textBrush = new SolidBrush(_isOn ? Color.FromArgb(50, 220, 50) : Color.FromArgb(140, 140, 145));
            using var textFont = new Font("Segoe UI", 9f, FontStyle.Bold);

            g.FillRectangle(bgBrush, rect);
            g.DrawRectangle(borderPen, rect);

            var text = _isOn ? "ON  ●" : "OFF  ○";
            var textSize = g.MeasureString(text, textFont);
            g.DrawString(text, textFont, textBrush,
                (Width - textSize.Width) / 2, (Height - textSize.Height) / 2);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            _isOn = !_isOn;
            Invalidate();
            Toggled?.Invoke(_isOn);
        }

        protected override void OnMouseEnter(EventArgs e) { Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { Invalidate(); base.OnMouseLeave(e); }
    }
}