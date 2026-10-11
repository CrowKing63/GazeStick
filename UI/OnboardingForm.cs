using System.Drawing;
using System.Windows.Forms;

namespace GazeStick.UI;

public sealed class OnboardingForm : Form
{
    private readonly CheckBox _chkDontShow;
    public bool DontShowAgain => _chkDontShow.Checked;

    private readonly Dictionary<Control, (Point Loc, Size Sz)> _designLayout = new();
    private Size _designSize;

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        // Scale the layout now that DeviceDpi is reliable (PMv2 applies the window's
        // DPI before OnShown). OnDpiChanged handles subsequent changes (monitor moves).
        ApplyScale(DeviceDpi / 96f);
    }

    /// <summary>
    /// Scales every control's location and size (and the form size) by the display
    /// DPI relative to the 96 dpi design baseline, matching PopupPanel. WinForms'
    /// PMv2 awareness already auto-scales fonts, so scaling the fixed pixel layout
    /// by the same factor keeps window, margins, and text growing together.
    /// </summary>
    private void ApplyScale(float scale)
    {
        if (Math.Abs(scale - 1f) < 0.001f) return;

        // Restore the design baseline first so a repeated DPI change never compounds.
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

    public OnboardingForm()
    {
        Text = "Welcome to GazeStick";
        // We scale the layout ourselves (see ScaleLayout) so AutoScaleMode stays None,
        // matching PopupPanel.
        AutoScaleMode = AutoScaleMode.None;
        Size = new Size(436, 300);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        BackColor = Color.FromArgb(28, 28, 30);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9f);

        var title = new Label
        {
            Text = "Welcome to GazeStick",
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            ForeColor = Color.FromArgb(63, 185, 80),
            Location = new Point(24, 20),
            AutoSize = true,
        };

        var body = new Label
        {
            Text = "GazeStick runs in your system tray — look for the eye icon near " +
                   "your clock.\n\n" +
                   "• Left-click the tray icon or press F9 to toggle eye tracking ON/OFF\n" +
                   "• Right-click to open the settings panel\n" +
                   "• Adjust Deadzone, Sensitivity, and Smoothing to match your " +
                   "preference\n\n" +
                   "Make sure Beam Eye Tracker is running and Gaming Extensions are " +
                   "activated before starting.",
            ForeColor = Color.FromArgb(200, 200, 205),
            Location = new Point(24, 56),
            Size = new Size(372, 150),
        };

        _chkDontShow = new CheckBox
        {
            Text = "Don't show this again",
            ForeColor = Color.FromArgb(160, 160, 165),
            Location = new Point(24, 218),
            AutoSize = true,
            BackColor = Color.Transparent,
        };

        var gotIt = new Button
        {
            Text = "Got it!",
            Location = new Point(300, 214),
            Size = new Size(96, 30),
            BackColor = Color.FromArgb(35, 100, 45),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
        };
        gotIt.FlatAppearance.BorderSize = 0;
        gotIt.Click += (_, _) => Close();

        Controls.AddRange(new Control[] { title, body, _chkDontShow, gotIt });
        CaptureDesignLayout();
    }
}
