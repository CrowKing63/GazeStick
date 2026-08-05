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
    private readonly Label _settingsNotice;
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
    public bool SuppressAutoClose { get; set; }

    public void ShowSettingsNotice(string message)
    {
        _settingsNotice.Text = message;
        _settingsNotice.Visible = true;
    }

    public PopupPanel()
    {
        FormBorderStyle = FormBorderStyle.FixedSingle;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(392, 860);
        MinimumSize = Size;
        MaximumSize = Size;
        BackColor = Color.FromArgb(28, 28, 30);
        ForeColor = Color.White;
        ShowInTaskbar = false;
        TopMost = true;
        KeyPreview = true;
        Font = new Font("Segoe UI", 9f);
        KeyDown += OnPanelKeyDown;
        Deactivate += (_, _) => BeginInvoke(CloseIfInactive);

        int y = 14;
        var title = new Label { Text = "GazeStick", Font = new Font("Segoe UI", 14f, FontStyle.Bold), ForeColor = Color.White, Location = new Point(16, y), AutoSize = true };
        _statusLabel = new Label { Location = new Point(260, y + 4), Size = new Size(96, 22), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        y += 32;
        _beamStatusLabel = new Label { Location = new Point(16, y), Size = new Size(340, 20), Font = new Font("Segoe UI", 9f), TextAlign = ContentAlignment.MiddleLeft };
        y += 28;

        _toggleButton = CreateButton("Tracking: ON", new Point(16, y), new Size(340, 40), Color.FromArgb(35, 100, 45));
        _toggleButton.AccessibleName = "Toggle eye tracking";
        _toggleButton.Click += (_, _) => ToggleChanged?.Invoke(!_isActive);
        y += 54;

        Controls.AddRange(new Control[] { title, _statusLabel, _beamStatusLabel, _toggleButton });
        AddSectionLabel("Tracking", ref y);

        _deadzoneControl = AddAdjuster("Deadzone", "Neutral radius before stick output begins.", 0.10, 0.0, 0.50, 0.01, 2, ref y);
        _deadzoneControl.ValueChanged += value => DeadzoneChanged?.Invoke(value);
        _sensitivityControl = AddAdjuster("Sensitivity", "How far you look for full stick deflection.", 2.0, 0.1, 5.0, 0.1, 1, ref y);
        _sensitivityControl.ValueChanged += value => SensitivityChanged?.Invoke(value);
        _smoothingControl = AddAdjuster("Smoothing", "Reduces small, rapid input changes.", 0.30, 0.0, 0.9, 0.05, 2, ref y);
        _smoothingControl.ValueChanged += value => SmoothingChanged?.Invoke(value);
        _blinkClampControl = AddAdjuster("Blink clamp (rec. 0.12)", "Suppresses sudden downward gaze spikes (blinks). 0 = off.", 0.0, 0.0, 0.50, 0.01, 2, ref y);
        _blinkClampControl.ValueChanged += value => BlinkClampChanged?.Invoke(value);

        AddSectionLabel("Response curve", ref y);
        _curveButton = CreateButton("Curve: Linear", new Point(16, y), new Size(160, 30), Color.FromArgb(45, 45, 50));
        _curveButton.Click += (_, _) => CycleCurve();
        Controls.Add(_curveButton);
        y += 34;
        _curvePowerControl = AddAdjuster("Curve power", "Used by exponential and logarithmic curves.", 2.0, 0.1, 5.0, 0.1, 1, ref y);
        _curvePowerControl.ValueChanged += value => CurvePowerChanged?.Invoke(value);

        AddSectionLabel("Virtual controller output", ref y);
        _xboxButton = CreateButton("Xbox 360", new Point(16, y), new Size(166, 32), Color.FromArgb(45, 45, 50));
        _ds4Button = CreateButton("DualShock 4", new Point(190, y), new Size(166, 32), Color.FromArgb(45, 45, 50));
        _xboxButton.AccessibleName = "Select Xbox 360 output";
        _ds4Button.AccessibleName = "Select DualShock 4 output";
        _xboxButton.Click += (_, _) => SelectOutput(OutputType.Xbox360);
        _ds4Button.Click += (_, _) => SelectOutput(OutputType.DualShock4);
        Controls.AddRange(new Control[] { _xboxButton, _ds4Button });
        y += 36;
        var outputHint = new Label { Text = "DualShock 4 mode does not use an XInput controller slot.", Location = new Point(16, y), Size = new Size(340, 18), ForeColor = Color.FromArgb(180, 180, 185), Font = new Font("Segoe UI", 8f) };
        Controls.Add(outputHint);
        y += 26;

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
        y += 40;

        _settingsNotice = new Label { Location = new Point(16, y), Size = new Size(340, 18), ForeColor = Color.FromArgb(130, 220, 150), Font = new Font("Segoe UI", 8f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter, Visible = false };
        Controls.Add(_settingsNotice);
        y += 24;

        var reset = CreateButton("Reset settings", new Point(16, y), new Size(164, 32), Color.FromArgb(85, 65, 30));
        var exit = CreateButton("Exit GazeStick", new Point(192, y), new Size(164, 32), Color.FromArgb(90, 40, 40));
        reset.Click += (_, _) => ResetRequested?.Invoke();
        exit.Click += (_, _) => ExitRequested?.Invoke();
        Controls.AddRange(new Control[] { reset, exit });

        UpdateState();
    }

    private void AddSectionLabel(string text, ref int y)
    {
        Controls.Add(new Label { Text = text.ToUpperInvariant(), Location = new Point(16, y), Size = new Size(340, 20), Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = Color.FromArgb(120, 190, 145) });
        y += 22;
    }

    private NumericAdjuster AddAdjuster(string label, string hint, double value, double min, double max, double step, int decimals, ref int y)
    {
        var title = new Label { Text = label, Location = new Point(16, y), Size = new Size(340, 18), Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = Color.FromArgb(220, 220, 225) };
        var control = new NumericAdjuster { Location = new Point(16, y + 18), Width = 340, AccessibleName = label, AccessibleDescription = hint };
        control.Initialize(value, min, max, step, decimals);
        Controls.AddRange(new Control[] { title, control });
        y += 62;
        return control;
    }

    private static Button CreateButton(string text, Point location, Size size, Color color)
    {
        var button = new Button { Text = text, Location = location, Size = size, FlatStyle = FlatStyle.Flat, BackColor = color, ForeColor = Color.White, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Cursor = Cursors.Hand, TabStop = true };
        button.FlatAppearance.BorderColor = Color.FromArgb(95, 95, 100);
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(Math.Min(color.R + 20, 255), Math.Min(color.G + 20, 255), Math.Min(color.B + 20, 255));
        return button;
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
