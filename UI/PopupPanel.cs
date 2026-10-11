using System.Drawing;
using System.Windows.Forms;
using GazeStick.Models;

namespace GazeStick.UI;

public sealed class PopupPanel : Form
{
    private readonly Label _statusLabel;
    private readonly Label _beamStatusLabel;
    private readonly NumericAdjuster _deadzoneControl;
    private readonly NumericAdjuster _sensitivityControl;
    private readonly NumericAdjuster _smoothingControl;
    private readonly NumericAdjuster _blinkClampControl;
    private readonly NumericAdjuster _curvePowerControl;
    private readonly Button _toggleButton;
    private readonly Button _xboxButton;
    private readonly Button _ds4Button;
    private readonly Button _invertYButton;
    private readonly Button _hotkeyButton;
    private readonly Button _autoStartButton;
    private readonly Button _curveButton;
#pragma warning disable CS0649
    private readonly ComboBox? _displayCombo;
#pragma warning restore CS0649
    private readonly Button[] _presetButtons;
    private int _presetIndex;
    private bool _isActive;
    private bool _isBeamConnected;
    private bool _awaitingHotkey;
    private bool _invertY;
    private bool _autoStart;
    private CurveType _curve;
    private OutputType _outputType;

    public event Action<bool>? ToggleChanged;
    public event Action<double>? DeadzoneChanged;
    public event Action<double>? SensitivityChanged;
    public event Action<double>? SmoothingChanged;
    public event Action<double>? BlinkClampChanged;
    public event Action<string>? HotkeyChanged;
    public event Action<bool>? InvertYChanged;
    public event Action<CurveType>? CurveTypeChanged;
    public event Action<double>? CurvePowerChanged;
    public event Action<bool>? AutoStartChanged;
    public event Action<OutputType>? OutputTypeChanged;
    // Kept for future restoration of the target display picker (currently hidden).
#pragma warning disable CS0067
    public event Action<string>? TargetDisplayChanged;
#pragma warning restore CS0067
    public event Action<int>? PresetChanged;
    public event Action? ResetRequested;
    public event Action? ExitRequested;

    public bool IsActive { get => _isActive; set { _isActive = value; UpdateState(); } }
    public bool IsBeamConnected { get => _isBeamConnected; set { _isBeamConnected = value; UpdateState(); } }
    public string HotkeyText { set { _hotkeyButton.Text = $"Toggle hotkey: {value} (click to change)"; } }
    public bool InvertY { get => _invertY; set { _invertY = value; UpdateState(); } }
    public bool AutoStart { get => _autoStart; set { _autoStart = value; UpdateState(); } }
    public CurveType Curve { get => _curve; set { _curve = value; UpdateState(); } }
    public OutputType OutputType { get => _outputType; set { _outputType = value; UpdateState(); } }
    public double CurvePower { set => _curvePowerControl.Value = value; }
    public double DeadzoneValue { set => _deadzoneControl.Value = value; }
    public double SensitivityValue { set => _sensitivityControl.Value = value; }
    public double SmoothingValue { set => _smoothingControl.Value = value; }
    public double BlinkClampValue { set => _blinkClampControl.Value = value; }
    /// <summary>
    /// Sets the highlighted preset button without firing PresetChanged (used on
    /// initial load and after a reset), mirroring SetTargetDisplay.
    /// </summary>
    public int PresetIndex
    {
        set => _presetIndex = Math.Clamp(value, 0, 2);
        get => _presetIndex;
    }
    public bool SuppressAutoClose { get; set; }

    /// <summary>
    /// Sets the selected target display by device name (empty = primary).
    /// Selects the matching combo item without firing TargetDisplayChanged.
    /// </summary>
    public void SetTargetDisplay(string deviceName)
    {
        if (_displayCombo == null) return;
        foreach (var item in _displayCombo.Items)
        {
            if (item is DisplayEntry e && e.DeviceName == (deviceName ?? ""))
            {
                _displayCombo.SelectedItem = item;
                return;
            }
        }
    }

    // Kept for future restoration of the target display picker (currently hidden).
#pragma warning disable CS8602
    private void PopulateDisplayCombo(string selectedDeviceName)
    {
        var items = new List<DisplayEntry>
        {
            new DisplayEntry("", "Primary (default)")
        };
        foreach (var s in System.Windows.Forms.Screen.AllScreens)
        {
            string label = $"{s.DeviceName} ({s.Bounds.Width}x{s.Bounds.Height}" +
                           (s.Primary ? ", primary" : "") + ")";
            items.Add(new DisplayEntry(s.DeviceName, label));
        }

        _displayCombo!.Items.Clear();
        foreach (var e in items)
            _displayCombo!.Items.Add(e);

        SetTargetDisplay(selectedDeviceName);
    }
#pragma warning restore CS8602

    private sealed record DisplayEntry(string DeviceName, string Label)
    {
        public override string ToString() => Label;
    }

    private readonly Dictionary<Control, (Point Loc, Size Sz)> _designLayout = new();
    private Size _designSize;

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        // Scale the layout first (DeviceDpi is reliable at OnShown under PMv2),
        // then center so the scaled size is used for positioning.
        ApplyScale(DeviceDpi / 96f);
        CenterOnScreen();
    }

    /// <summary>
    /// Scales every control's location and size (and the form size) by the display
    /// DPI relative to the 96 dpi design baseline. WinForms' PMv2 awareness already
    /// auto-scales fonts, so scaling the fixed pixel layout by the same factor makes
    /// the whole panel — window, margins included — follow the Windows display scale.
    /// </summary>
    private void ApplyScale(float scale)
    {
        if (Math.Abs(scale - 1f) < 0.001f) return;

        // Restore the design baseline first so a repeated DPI change (e.g. moving the
        // window to a monitor with a different scale) never compounds on the old scale.
        Size = _designSize;
        foreach (var kv in _designLayout)
        {
            kv.Key.Location = kv.Value.Loc;
            kv.Key.Size = kv.Value.Sz;
        }

        Size = new Size((int)Math.Round(_designSize.Width * scale), (int)Math.Round(_designSize.Height * scale));
        foreach (var kv in _designLayout)
        {
            kv.Key.Location = new Point((int)Math.Round(kv.Value.Loc.X * scale), (int)Math.Round(kv.Value.Loc.Y * scale));
            kv.Key.Size = new Size((int)Math.Round(kv.Value.Sz.Width * scale), (int)Math.Round(kv.Value.Sz.Height * scale));
        }
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        ApplyScale(e.DeviceDpiNew / 96f);
    }

    private void CaptureDesignLayout()
    {
        _designSize = Size;
        foreach (Control c in Controls)
            _designLayout[c] = (c.Location, c.Size);
    }

    /// <summary>Centers the panel in the working area of the screen under the cursor.</summary>
    private void CenterOnScreen()
    {
        var screen = Screen.FromPoint(Cursor.Position);
        var wa = screen.WorkingArea;
        int x = wa.Left + (wa.Width - Width) / 2;
        int y = wa.Top + (wa.Height - Height) / 2;
        x = Math.Clamp(x, wa.Left, wa.Right - Width);
        y = Math.Max(y, wa.Top);
        Location = new Point(x, y);
    }

    public PopupPanel()
    {
        FormBorderStyle = FormBorderStyle.FixedSingle;
        StartPosition = FormStartPosition.Manual;
        // We scale the layout ourselves (see ScaleLayout) so AutoScaleMode stays None:
        // WinForms' PMv2 awareness already auto-scales fonts, and letting it also
        // scale this fixed pixel layout would double-scale and fight Minimum/MaximumSize.
        AutoScaleMode = AutoScaleMode.None;
        Size = new Size(392, 650);
        BackColor = Color.FromArgb(28, 28, 30);
        ForeColor = Color.White;
        ShowInTaskbar = false;
        TopMost = true;
        KeyPreview = true;
        Font = new Font("Segoe UI", 10f);
        KeyDown += OnPanelKeyDown;
        Deactivate += (_, _) => BeginInvoke(CloseIfInactive);

        int y = 14;
        var title = new Label { Text = "GazeStick", Font = new Font("Segoe UI", 16f, FontStyle.Bold), ForeColor = Color.White, Location = new Point(16, y), AutoSize = true };
        _statusLabel = new Label { Location = new Point(260, y + 4), Size = new Size(96, 22), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI", 10f, FontStyle.Bold) };
        y += 32;
        _beamStatusLabel = new Label { Location = new Point(16, y), Size = new Size(340, 20), Font = new Font("Segoe UI", 10f), TextAlign = ContentAlignment.MiddleLeft };
        y += 28;

        _toggleButton = CreateButton("Tracking: ON", new Point(16, y), new Size(340, 40), Color.FromArgb(35, 100, 45));
        _toggleButton.AccessibleName = "Toggle eye tracking";
        _toggleButton.Click += (_, _) => ToggleChanged?.Invoke(!_isActive);
        y += 54;

        Controls.AddRange(new Control[] { title, _statusLabel, _beamStatusLabel, _toggleButton });

        // Target display selection is hidden: in practice the monitor Beam tracks
        // is always the Windows primary, so a separate picker only adds confusion.
        // Keep the field and SetTargetDisplay for future restoration if a
        // non-primary Beam target becomes necessary.
        // var displayTitle = new Label { Text = "Target display", Location = new Point(16, y), Size = new Size(340, 18), Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = Color.FromArgb(220, 220, 225) };
        // _displayCombo = new ComboBox { Location = new Point(16, y + 18), Width = 340, DropDownStyle = ComboBoxStyle.DropDownList, DrawMode = DrawMode.OwnerDrawFixed, Font = new Font("Segoe UI", 10f), AccessibleName = "Target display" };
        // _displayCombo.DrawItem += (_, e) =>
        // {
        //     if (e.Index < 0 || e.Index >= _displayCombo.Items.Count) return;
        //     bool selected = e.Index == _displayCombo.SelectedIndex;
        //     using var bg = new SolidBrush(selected ? Color.FromArgb(65, 130, 80) : Color.FromArgb(45, 45, 50));
        //     e.Graphics.FillRectangle(bg, e.Bounds);
        //     TextRenderer.DrawText(e.Graphics, _displayCombo.Items[e.Index]?.ToString() ?? string.Empty, _displayCombo.Font, e.Bounds,
        //         selected ? Color.White : Color.FromArgb(230, 230, 235),
        //         TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        // };
        // PopulateDisplayCombo("");
        // _displayCombo.SelectedIndexChanged += (_, _) =>
        // {
        //     if (_displayCombo.SelectedItem is string deviceName)
        //         TargetDisplayChanged?.Invoke(deviceName);
        // };
        // Controls.AddRange(new Control[] { displayTitle, _displayCombo });
        // y += 62;

        // Presets: three buttons that save/restore the six adjustable values. The
        // active preset is highlighted with the same green as the selected output.
        AddSectionLabel("Presets", ref y);
        _presetButtons = new Button[3];
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            var b = CreateButton($"Preset {i + 1}", new Point(16 + i * 116, y), new Size(108, 32), Color.FromArgb(45, 45, 50));
            b.AccessibleName = $"Select preset {i + 1}";
            b.Click += (_, _) => SelectPreset(idx);
            _presetButtons[i] = b;
        }
        Controls.AddRange(_presetButtons);
        y += 36;

        // Adjustable values in a two-column grid to halve the vertical footprint:
        // each row holds one left (x=16) and one right (x=192) adjuster at the same y,
        // so four adjusters occupy two rows instead of four.
        _deadzoneControl = AddAdjusterAt("Deadzone", "Neutral radius before stick output begins.", 0.10, 0.0, 0.50, 0.01, 2, 0, y);
        _sensitivityControl = AddAdjusterAt("Sensitivity", "How far you look for full stick deflection.", 2.0, 0.1, 5.0, 0.1, 1, 1, y);
        _deadzoneControl.ValueChanged += value => DeadzoneChanged?.Invoke(value);
        _sensitivityControl.ValueChanged += value => SensitivityChanged?.Invoke(value);
        y += 62;

        _smoothingControl = AddAdjusterAt("Smoothing", "Reduces small, rapid input changes.", 0.30, 0.0, 0.9, 0.05, 2, 0, y);
        _blinkClampControl = AddAdjusterAt("Blink clamp", "Suppresses sudden downward gaze spikes (blinks). Recommended 0.12, 0 = off.", 0.0, 0.0, 0.50, 0.01, 2, 1, y);
        _smoothingControl.ValueChanged += value => SmoothingChanged?.Invoke(value);
        _blinkClampControl.ValueChanged += value => BlinkClampChanged?.Invoke(value);
        y += 62;

        // Response curve: the curve-type button (left) and its power adjuster
        // (right) share a grid row, so no separate section label is needed.
        _curveButton = CreateButton("Curve: Linear", new Point(16, y), new Size(164, 32), Color.FromArgb(45, 45, 50));
        _curveButton.AccessibleName = "Cycle response curve";
        _curveButton.Click += (_, _) => CycleCurve();
        Controls.Add(_curveButton);
        var curvePowerTitle = new Label { Text = "Curve power", Location = new Point(192, y), Size = new Size(164, 18), Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = Color.FromArgb(220, 220, 225) };
        _curvePowerControl = new NumericAdjuster { Location = new Point(192, y + 18), Width = 164, AccessibleName = "Curve power", AccessibleDescription = "Used by exponential and logarithmic curves." };
        _curvePowerControl.Initialize(2.0, 0.1, 5.0, 0.1, 1);
        _curvePowerControl.ValueChanged += value => CurvePowerChanged?.Invoke(value);
        Controls.AddRange(new Control[] { curvePowerTitle, _curvePowerControl });
        y += 62;

        AddSectionLabel("Virtual controller output", ref y);
        _xboxButton = CreateButton("Xbox 360", new Point(16, y), new Size(166, 32), Color.FromArgb(45, 45, 50));
        _ds4Button = CreateButton("DualShock 4", new Point(190, y), new Size(166, 32), Color.FromArgb(45, 45, 50));
        _xboxButton.AccessibleName = "Select Xbox 360 output";
        _ds4Button.AccessibleName = "Select DualShock 4 output";
        _xboxButton.Click += (_, _) => SelectOutput(OutputType.Xbox360);
        _ds4Button.Click += (_, _) => SelectOutput(OutputType.DualShock4);
        Controls.AddRange(new Control[] { _xboxButton, _ds4Button });
        y += 36;

        AddSectionLabel("Quick settings", ref y);
        _invertYButton = CreateButton("Vertical camera: Normal (click to invert)", new Point(16, y), new Size(340, 32), Color.FromArgb(45, 45, 50));
        y += 36;
        _autoStartButton = CreateButton("Start with Windows: Off", new Point(16, y), new Size(340, 32), Color.FromArgb(45, 45, 50));
        y += 36;
        _hotkeyButton = CreateButton("Toggle hotkey: F9 (click to change)", new Point(16, y), new Size(340, 32), Color.FromArgb(45, 45, 50));
        _invertYButton.Click += (_, _) => { _invertY = !_invertY; UpdateState(); InvertYChanged?.Invoke(_invertY); };
        _hotkeyButton.Click += (_, _) => StartHotkeyCapture();
        _autoStartButton.Click += (_, _) => { _autoStart = !_autoStart; UpdateState(); AutoStartChanged?.Invoke(_autoStart); };
        Controls.AddRange(new Control[] { _invertYButton, _hotkeyButton, _autoStartButton });
        y += 36;

        var reset = CreateButton("Reset settings", new Point(16, y), new Size(164, 32), Color.FromArgb(85, 65, 30));
        var exit = CreateButton("Exit GazeStick", new Point(192, y), new Size(164, 32), Color.FromArgb(90, 40, 40));
        reset.Click += (_, _) => ResetRequested?.Invoke();
        exit.Click += (_, _) => ExitRequested?.Invoke();
        Controls.AddRange(new Control[] { reset, exit });

        CaptureDesignLayout();
        UpdateState();
    }

    private void AddSectionLabel(string text, ref int y)
    {
        Controls.Add(new Label { Text = text.ToUpperInvariant(), Location = new Point(16, y), Size = new Size(340, 20), Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = Color.FromArgb(120, 190, 145) });
        y += 22;
    }

    private NumericAdjuster AddAdjusterAt(string label, string hint, double value, double min, double max, double step, int decimals, int col, int y)
    {
        int x = col == 0 ? 16 : 192;
        var title = new Label { Text = label, Location = new Point(x, y), Size = new Size(164, 18), Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = Color.FromArgb(220, 220, 225) };
        var control = new NumericAdjuster { Location = new Point(x, y + 18), Width = 164, AccessibleName = label, AccessibleDescription = hint };
        control.Initialize(value, min, max, step, decimals);
        Controls.AddRange(new Control[] { title, control });
        return control;
    }

    private static Button CreateButton(string text, Point location, Size size, Color color)
    {
        var button = new Button { Text = text, Location = location, Size = size, FlatStyle = FlatStyle.Flat, BackColor = color, ForeColor = Color.White, Font = new Font("Segoe UI", 10f, FontStyle.Bold), Cursor = Cursors.Hand, TabStop = true };
        button.FlatAppearance.BorderColor = Color.FromArgb(95, 95, 100);
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(Math.Min(color.R + 20, 255), Math.Min(color.G + 20, 255), Math.Min(color.B + 20, 255));
        return button;
    }

    private void SelectPreset(int index)
    {
        index = Math.Clamp(index, 0, 2);
        if (_presetIndex == index) return;
        _presetIndex = index;
        UpdateState();
        PresetChanged?.Invoke(index);
    }

    private void SelectOutput(OutputType outputType)
    {
        if (_outputType == outputType) return;
        _outputType = outputType;
        UpdateState();
        OutputTypeChanged?.Invoke(outputType);
    }

    private void UpdateState()
    {
        _statusLabel.Text = _isActive ? "TRACKING ON" : "TRACKING OFF";
        _statusLabel.ForeColor = _isActive ? Color.FromArgb(90, 220, 120) : Color.FromArgb(190, 190, 195);
        _beamStatusLabel.Text = _isBeamConnected ? "Beam Eye Tracker: Connected" : "Beam Eye Tracker: Waiting for connection";
        _beamStatusLabel.ForeColor = _isBeamConnected ? Color.FromArgb(160, 220, 175) : Color.FromArgb(235, 190, 100);
        _toggleButton.Text = _isActive ? "Tracking: ON (click to turn off)" : "Tracking: OFF (click to turn on)";
        _toggleButton.BackColor = _isActive ? Color.FromArgb(35, 100, 45) : Color.FromArgb(65, 65, 70);
        _deadzoneControl.SetEnabledState(_isActive);
        _sensitivityControl.SetEnabledState(_isActive);
        _smoothingControl.SetEnabledState(_isActive);
        _blinkClampControl.SetEnabledState(_isActive);
        _curveButton.Text = $"Curve: {_curve switch { CurveType.Exponential => "Exponential", CurveType.Logarithmic => "Logarithmic", _ => "Linear" }}";
        _curvePowerControl.Visible = _curve != CurveType.Linear;
        _invertYButton.Text = _invertY ? "Vertical camera: Inverted (click to restore)" : "Vertical camera: Normal (click to invert)";
        _autoStartButton.Text = _autoStart ? "Start with Windows: On (click to disable)" : "Start with Windows: Off (click to enable)";
        SetSelected(_xboxButton, _outputType == OutputType.Xbox360);
        SetSelected(_ds4Button, _outputType == OutputType.DualShock4);
        for (int i = 0; i < _presetButtons.Length; i++)
            SetSelected(_presetButtons[i], i == _presetIndex);
    }

    private static void SetSelected(Button button, bool selected)
    {
        button.BackColor = selected ? Color.FromArgb(35, 100, 70) : Color.FromArgb(45, 45, 50);
        button.FlatAppearance.BorderColor = selected ? Color.FromArgb(95, 210, 135) : Color.FromArgb(95, 95, 100);
    }

    private void CycleCurve()
    {
        _curve = _curve switch { CurveType.Linear => CurveType.Exponential, CurveType.Exponential => CurveType.Logarithmic, _ => CurveType.Linear };
        UpdateState();
        CurveTypeChanged?.Invoke(_curve);
    }

    private void StartHotkeyCapture()
    {
        _awaitingHotkey = true;
        _hotkeyButton.Text = "Press a key...";
        _hotkeyButton.Focus();
    }

    private void OnPanelKeyDown(object? sender, KeyEventArgs e)
    {
        if (_awaitingHotkey)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            var hotkey = e.KeyCode.ToString();
            if (e.Control) hotkey = "Ctrl+" + hotkey;
            if (e.Shift) hotkey = "Shift+" + hotkey;
            if (e.Alt) hotkey = "Alt+" + hotkey;
            _hotkeyButton.Text = $"Toggle hotkey: {hotkey} (click to change)";
            _awaitingHotkey = false;
            HotkeyChanged?.Invoke(hotkey);
        }
        else if (e.KeyCode == Keys.Escape)
        {
            Close();
        }
    }

    private void CloseIfInactive()
    {
        if (!SuppressAutoClose && !IsDisposed && !ContainsFocus)
            Close();
    }
}
