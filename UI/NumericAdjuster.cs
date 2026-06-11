using System.Drawing;
using System.Windows.Forms;

namespace GazeStick.UI;

public class NumericAdjuster : UserControl
{
    private readonly Button _btnMinus;
    private readonly Panel _valuePanel;
    private readonly Label _lblValue;
    private readonly Button _btnPlus;
    private readonly ToolTip _toolTip = new();

    private double _value;
    private double _min;
    private double _max;
    private double _step;
    private int _decimals;
    private string _format = "F2";
    private bool _dragging;
    private int _dragStartX;
    private double _dragStartValue;

    public event Action<double>? ValueChanged;

    public double Value
    {
        get => _value;
        set
        {
            _value = Math.Clamp(value, _min, _max);
            UpdateDisplay();
        }
    }

    public double Minimum { get => _min; set { _min = value; UpdateDisplay(); } }
    public double Maximum { get => _max; set { _max = value; UpdateDisplay(); } }
    public double Step { get => _step; set => _step = value; }
    public int Decimals { get => _decimals; set { _decimals = value; _format = "F" + value; UpdateDisplay(); } }

    public NumericAdjuster()
    {
        Height = 36;
        Width = 220;
        BackColor = Color.Transparent;

        _btnMinus = new Button
        {
            Text = "−",
            Width = 32,
            Height = 32,
            Location = new Point(0, 2),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.FromArgb(200, 200, 200),
            BackColor = Color.FromArgb(45, 45, 48),
        };
        _btnMinus.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
        _btnMinus.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 60, 65);
        _btnMinus.Click += (_, _) => AdjustValue(-_step);
        _btnMinus.MouseDown += (_, _) => _btnMinus.FlatAppearance.MouseDownBackColor = Color.FromArgb(70, 70, 75);

        _valuePanel = new Panel
        {
            Location = new Point(36, 2),
            Size = new Size(148, 32),
            BackColor = Color.FromArgb(35, 35, 38),
            BorderStyle = BorderStyle.FixedSingle,
        };
        _valuePanel.MouseDown += OnValuePanelMouseDown;
        _valuePanel.MouseMove += OnValuePanelMouseMove;
        _valuePanel.MouseUp += OnValuePanelMouseUp;
        _valuePanel.MouseLeave += (_, _) => { if (_dragging) OnValuePanelMouseUp(_valuePanel, new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0)); };

        _lblValue = new Label
        {
            Text = "0.00",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 10f, FontStyle.Regular),
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Cursor = Cursors.SizeWE,
        };
        _lblValue.MouseDown += OnValuePanelMouseDown;
        _lblValue.MouseMove += OnValuePanelMouseMove;
        _lblValue.MouseUp += OnValuePanelMouseUp;
        _valuePanel.Controls.Add(_lblValue);

        _btnPlus = new Button
        {
            Text = "+",
            Width = 32,
            Height = 32,
            Location = new Point(188, 2),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.FromArgb(200, 200, 200),
            BackColor = Color.FromArgb(45, 45, 48),
        };
        _btnPlus.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
        _btnPlus.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 60, 65);
        _btnPlus.Click += (_, _) => AdjustValue(_step);
        _btnPlus.MouseDown += (_, _) => _btnPlus.FlatAppearance.MouseDownBackColor = Color.FromArgb(70, 70, 75);

        _toolTip.SetToolTip(_valuePanel, "드래그하여 연속 조정");

        Controls.AddRange(new Control[] { _btnMinus, _valuePanel, _btnPlus });
    }

    public void Initialize(double value, double min, double max, double step, int decimals)
    {
        _min = min;
        _max = max;
        _step = step;
        _decimals = decimals;
        _format = "F" + decimals;
        Value = value;
    }

    private void AdjustValue(double delta)
    {
        Value = Math.Round(_value + delta, _decimals);
        ValueChanged?.Invoke(_value);
    }

    private void OnValuePanelMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _dragging = true;
            _dragStartX = e.X + _valuePanel.Left;
            _dragStartValue = _value;
            _valuePanel.Capture = true;
            Cursor = Cursors.SizeWE;
        }
    }

    private void OnValuePanelMouseMove(object? sender, MouseEventArgs e)
    {
        if (!_dragging) return;

        int currentX = e.X + _valuePanel.Left;
        int deltaX = currentX - _dragStartX;
        double change = deltaX * _step * 0.1;
        Value = Math.Round(Math.Clamp(_dragStartValue + change, _min, _max), _decimals);
        ValueChanged?.Invoke(_value);
    }

    private void OnValuePanelMouseUp(object? sender, MouseEventArgs e)
    {
        if (_dragging)
        {
            _dragging = false;
            _valuePanel.Capture = false;
            Cursor = Cursors.Default;
        }
    }

    private void UpdateDisplay()
    {
        _lblValue.Text = _value.ToString(_format);
    }

    public void SetEnabledState(bool enabled)
    {
        Enabled = enabled;
        _btnMinus.Enabled = enabled;
        _btnPlus.Enabled = enabled;
        _valuePanel.Enabled = enabled;
        var alpha = enabled ? 255 : 102;
        _btnMinus.ForeColor = Color.FromArgb(alpha, _btnMinus.ForeColor);
        _btnPlus.ForeColor = Color.FromArgb(alpha, _btnPlus.ForeColor);
        _lblValue.ForeColor = Color.FromArgb(alpha, _lblValue.ForeColor);
    }
}