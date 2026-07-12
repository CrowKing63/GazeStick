using System.Drawing;
using System.Windows.Forms;

namespace GazeStick.UI;

public sealed class OnboardingForm : Form
{
    private readonly CheckBox _chkDontShow;
    public bool DontShowAgain => _chkDontShow.Checked;

    public OnboardingForm()
    {
        Text = "Welcome to GazeStick";
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
    }
}
