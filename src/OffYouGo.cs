using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Off You Go")]
[assembly: AssemblyDescription("Windows Shutdown, Restart & Log Off Timer Utility")]
[assembly: AssemblyCompany("Off You Go")]
[assembly: AssemblyProduct("Off You Go")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace OffYouGo
{
    static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main()
        {
            try { SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    public class MainForm : Form
    {
        private const string RegKeyPath = @"Software\OffYouGo";

        // Action selection
        private RadioButton rbShutdown;
        private RadioButton rbRestart;
        private RadioButton rbLogoff;
        private CheckBox chkForce;

        // Mode 1: Specific Time of Day
        private RadioButton rbModeTimeOfDay;
        private DateTimePicker dtpTimeOfDay;
        private Label lblNextOccurrenceHint;

        // Mode 2: Countdown (Days, Hours, Minutes)
        private RadioButton rbModeCountdown;
        private NumericUpDown nudDays;
        private NumericUpDown nudHours;
        private NumericUpDown nudMinutes;
        private Panel pnlCountdownInputs;

        // Buttons
        private Button btnSchedule;
        private Button btnCancelShutdown;
        private Button btnClose;

        // Status area & Timer
        private StatusStrip statusStrip;
        private ToolStripStatusLabel lblStatusIcon;
        private ToolStripStatusLabel lblStatusText;
        private Timer uiTimer;

        // Active Scheduled State
        private DateTime? scheduledTargetTime = null;
        private string scheduledActionName = "Shutdown";
        private string scheduledActionFlag = "/s";
        private bool scheduledForceFlag = false;
        private bool logoffTriggeredByApp = false;

        private Icon appIcon;

        public MainForm()
        {
            InitializeAssets();
            InitializeComponent();
            LoadPersistedSchedule();
            UpdateModeControls();
            UpdatePreviewAndStatus();
        }

        private void InitializeAssets()
        {
            using (Bitmap iconBmp = RenderPowerIconBitmap(32))
            {
                appIcon = Icon.FromHandle(iconBmp.GetHicon());
            }
        }

        private static Bitmap RenderPowerIconBitmap(int size)
        {
            Bitmap bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);

                float pad = size * 0.04f;
                float rectSize = size - (pad * 2f);
                float cornerRadius = size * 0.18f;

                using (GraphicsPath bgPath = CreateRoundedRect(pad, pad, rectSize, rectSize, cornerRadius))
                {
                    using (LinearGradientBrush bgBrush = new LinearGradientBrush(
                        new PointF(0, 0), new PointF(0, size),
                        Color.FromArgb(255, 42, 42, 42),
                        Color.FromArgb(255, 10, 10, 10)))
                    {
                        g.FillPath(bgBrush, bgPath);
                    }

                    using (Pen borderPen = new Pen(Color.FromArgb(210, 85, 85, 85), Math.Max(1f, size * 0.035f)))
                    {
                        g.DrawPath(borderPen, bgPath);
                    }
                }

                float center = size / 2f;
                float arcRadius = size * 0.27f;
                float strokeWidth = Math.Max(1.8f, size * 0.115f);

                using (Pen glowPen = new Pen(Color.FromArgb(75, 255, 25, 25), strokeWidth * 1.65f))
                {
                    glowPen.StartCap = LineCap.Round;
                    glowPen.EndCap = LineCap.Round;
                    RectangleF arcRect = new RectangleF(center - arcRadius, center - arcRadius + (size * 0.03f), arcRadius * 2f, arcRadius * 2f);
                    g.DrawArc(glowPen, arcRect, -50f, 280f);
                    g.DrawLine(glowPen, center, center - arcRadius - (size * 0.06f), center, center + (size * 0.01f));
                }

                using (Pen redPen = new Pen(Color.FromArgb(255, 235, 30, 30), strokeWidth))
                {
                    redPen.StartCap = LineCap.Round;
                    redPen.EndCap = LineCap.Round;
                    RectangleF arcRect = new RectangleF(center - arcRadius, center - arcRadius + (size * 0.03f), arcRadius * 2f, arcRadius * 2f);
                    g.DrawArc(redPen, arcRect, -50f, 280f);
                    g.DrawLine(redPen, center, center - arcRadius - (size * 0.06f), center, center + (size * 0.01f));
                }
            }
            return bmp;
        }

        private static GraphicsPath CreateRoundedRect(float x, float y, float width, float height, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float d = radius * 2f;
            path.AddArc(x, y, d, d, 180, 90);
            path.AddArc(x + width - d, y, d, d, 270, 90);
            path.AddArc(x + width - d, y + height - d, d, d, 0, 90);
            path.AddArc(x, y + height - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void InitializeComponent()
        {
            this.Text = "Off You Go";
            this.Icon = appIcon;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = true;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ClientSize = new Size(445, 338);
            this.BackColor = SystemColors.Control;

            // --- GroupBox 1: Action Type ---
            GroupBox grpAction = new GroupBox
            {
                Text = "1. Power Action",
                Location = new Point(14, 12),
                Size = new Size(417, 78),
                FlatStyle = FlatStyle.System
            };

            rbShutdown = new RadioButton
            {
                Text = "Shutdown (/s)",
                Location = new Point(16, 22),
                AutoSize = true,
                Checked = true,
                UseVisualStyleBackColor = true
            };
            rbRestart = new RadioButton
            {
                Text = "Restart (/r)",
                Location = new Point(145, 22),
                AutoSize = true,
                UseVisualStyleBackColor = true
            };
            rbLogoff = new RadioButton
            {
                Text = "Log Off (/l)",
                Location = new Point(265, 22),
                AutoSize = true,
                UseVisualStyleBackColor = true
            };

            rbShutdown.CheckedChanged += (s, e) => UpdatePreviewAndStatus();
            rbRestart.CheckedChanged += (s, e) => UpdatePreviewAndStatus();
            rbLogoff.CheckedChanged += (s, e) => UpdatePreviewAndStatus();

            chkForce = new CheckBox
            {
                Text = "Force close running applications without warning (/f)",
                Location = new Point(16, 48),
                AutoSize = true,
                UseVisualStyleBackColor = true
            };

            grpAction.Controls.Add(rbShutdown);
            grpAction.Controls.Add(rbRestart);
            grpAction.Controls.Add(rbLogoff);
            grpAction.Controls.Add(chkForce);
            this.Controls.Add(grpAction);

            // --- GroupBox 2: Trigger Time / Duration ---
            GroupBox grpTiming = new GroupBox
            {
                Text = "2. Schedule Timing",
                Location = new Point(14, 98),
                Size = new Size(417, 152),
                FlatStyle = FlatStyle.System
            };

            // Option A: Time of Day
            rbModeTimeOfDay = new RadioButton
            {
                Text = "At a specific time of day:",
                Location = new Point(16, 28),
                AutoSize = true,
                Checked = true,
                UseVisualStyleBackColor = true
            };
            rbModeTimeOfDay.CheckedChanged += (s, e) => { UpdateModeControls(); UpdatePreviewAndStatus(); };

            dtpTimeOfDay = new DateTimePicker
            {
                Format = DateTimePickerFormat.Time,
                ShowUpDown = true,
                Location = new Point(185, 25),
                Width = 115,
                Value = DateTime.Today.AddHours(23).AddMinutes(0) // Default 11:00 PM
            };
            // If 11:00 PM is already within 2 mins or past, default to 1 hour from now
            DateTime oneHourFromNow = DateTime.Now.AddHours(1);
            dtpTimeOfDay.Value = new DateTime(oneHourFromNow.Year, oneHourFromNow.Month, oneHourFromNow.Day, oneHourFromNow.Hour, oneHourFromNow.Minute, 0);
            dtpTimeOfDay.ValueChanged += (s, e) => UpdatePreviewAndStatus();

            lblNextOccurrenceHint = new Label
            {
                Text = "(Today)",
                Location = new Point(306, 29),
                AutoSize = true,
                ForeColor = Color.FromArgb(90, 90, 90)
            };

            // Divider line
            Label lblDivider = new Label
            {
                BorderStyle = BorderStyle.Fixed3D,
                Location = new Point(16, 63),
                Size = new Size(385, 2),
                Text = ""
            };

            // Option B: Days, Hours, Minutes Countdown
            rbModeCountdown = new RadioButton
            {
                Text = "After duration (countdown):",
                Location = new Point(16, 78),
                AutoSize = true,
                UseVisualStyleBackColor = true
            };
            rbModeCountdown.CheckedChanged += (s, e) => { UpdateModeControls(); UpdatePreviewAndStatus(); };

            pnlCountdownInputs = new Panel
            {
                Location = new Point(32, 103),
                Size = new Size(370, 36)
            };

            Label lblDays = new Label { Text = "Days:", Location = new Point(2, 5), AutoSize = true };
            nudDays = new NumericUpDown
            {
                Location = new Point(40, 2),
                Width = 54,
                Minimum = 0,
                Maximum = 365,
                Value = 0,
                TextAlign = HorizontalAlignment.Right
            };
            nudDays.ValueChanged += (s, e) => UpdatePreviewAndStatus();

            Label lblHours = new Label { Text = "Hours:", Location = new Point(112, 5), AutoSize = true };
            nudHours = new NumericUpDown
            {
                Location = new Point(156, 2),
                Width = 54,
                Minimum = 0,
                Maximum = 23,
                Value = 1,
                TextAlign = HorizontalAlignment.Right
            };
            nudHours.ValueChanged += (s, e) => UpdatePreviewAndStatus();

            Label lblMinutes = new Label { Text = "Minutes:", Location = new Point(228, 5), AutoSize = true };
            nudMinutes = new NumericUpDown
            {
                Location = new Point(284, 2),
                Width = 54,
                Minimum = 0,
                Maximum = 59,
                Value = 0,
                TextAlign = HorizontalAlignment.Right
            };
            nudMinutes.ValueChanged += (s, e) => UpdatePreviewAndStatus();

            pnlCountdownInputs.Controls.Add(lblDays);
            pnlCountdownInputs.Controls.Add(nudDays);
            pnlCountdownInputs.Controls.Add(lblHours);
            pnlCountdownInputs.Controls.Add(nudHours);
            pnlCountdownInputs.Controls.Add(lblMinutes);
            pnlCountdownInputs.Controls.Add(nudMinutes);

            grpTiming.Controls.Add(rbModeTimeOfDay);
            grpTiming.Controls.Add(dtpTimeOfDay);
            grpTiming.Controls.Add(lblNextOccurrenceHint);
            grpTiming.Controls.Add(lblDivider);
            grpTiming.Controls.Add(rbModeCountdown);
            grpTiming.Controls.Add(pnlCountdownInputs);
            this.Controls.Add(grpTiming);

            // --- Action Buttons (Classic Windows 7 command bar layout) ---
            btnSchedule = new Button
            {
                Text = "&Schedule Shutdown",
                Location = new Point(14, 262),
                Size = new Size(152, 32),
                UseVisualStyleBackColor = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            btnSchedule.Click += BtnSchedule_Click;

            btnCancelShutdown = new Button
            {
                Text = "&Cancel Shutdown",
                Location = new Point(174, 262),
                Size = new Size(142, 32),
                UseVisualStyleBackColor = true
            };
            btnCancelShutdown.Click += BtnCancelShutdown_Click;

            btnClose = new Button
            {
                Text = "Close &App",
                Location = new Point(324, 262),
                Size = new Size(107, 32),
                UseVisualStyleBackColor = true
            };
            btnClose.Click += (s, e) => this.Close();

            this.AcceptButton = btnSchedule;
            this.CancelButton = btnClose;

            this.Controls.Add(btnSchedule);
            this.Controls.Add(btnCancelShutdown);
            this.Controls.Add(btnClose);

            // --- Classic Status Bar ---
            statusStrip = new StatusStrip
            {
                SizingGrip = false,
                BackColor = Color.FromArgb(238, 241, 245)
            };

            lblStatusIcon = new ToolStripStatusLabel
            {
                Text = "●",
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            lblStatusText = new ToolStripStatusLabel
            {
                Text = "Ready — No shutdown currently scheduled.",
                Spring = true,
                TextAlign = ContentAlignment.MiddleLeft
            };

            statusStrip.Items.Add(lblStatusIcon);
            statusStrip.Items.Add(lblStatusText);
            this.Controls.Add(statusStrip);

            // --- UI Live Countdown Timer ---
            uiTimer = new Timer { Interval = 500 };
            uiTimer.Tick += UiTimer_Tick;
            uiTimer.Start();
        }

        private void UpdateModeControls()
        {
            bool isTimeOfDay = rbModeTimeOfDay.Checked;
            dtpTimeOfDay.Enabled = isTimeOfDay;
            lblNextOccurrenceHint.Enabled = isTimeOfDay;
            pnlCountdownInputs.Enabled = !isTimeOfDay;
        }

        private string GetSelectedActionName()
        {
            if (rbRestart.Checked) return "Restart";
            if (rbLogoff.Checked) return "Log Off";
            return "Shutdown";
        }

        private string GetSelectedActionFlag()
        {
            if (rbRestart.Checked) return "/r";
            if (rbLogoff.Checked) return "/l";
            return "/s";
        }

        private void UpdatePreviewAndStatus()
        {
            string actionName = GetSelectedActionName();
            btnSchedule.Text = "&Schedule " + actionName;

            // Update Time-of-Day hint ("Today" vs "Tomorrow")
            DateTime now = DateTime.Now;
            DateTime pickedTimeToday = new DateTime(
                now.Year, now.Month, now.Day,
                dtpTimeOfDay.Value.Hour, dtpTimeOfDay.Value.Minute, dtpTimeOfDay.Value.Second);

            if (pickedTimeToday <= now)
            {
                lblNextOccurrenceHint.Text = "(Tomorrow)";
            }
            else
            {
                lblNextOccurrenceHint.Text = "(Today)";
            }

            RefreshStatusDisplay();
        }

        private void UiTimer_Tick(object sender, EventArgs e)
        {
            UpdatePreviewAndStatus();

            // Note: Windows 'shutdown /l' (Log Off) does not natively support the '/t <seconds>' flag on Windows.
            // For Shutdown (/s) and Restart (/r), Windows handles the timer natively via 'shutdown /s /t <sec>'.
            // If the user selected Log Off (/l) and kept the app open, or we scheduled a background shutdown.exe call:
            if (scheduledTargetTime.HasValue)
            {
                TimeSpan remaining = scheduledTargetTime.Value - DateTime.Now;
                if (remaining.TotalSeconds <= 0)
                {
                    if (scheduledActionFlag == "/l" && !logoffTriggeredByApp)
                    {
                        logoffTriggeredByApp = true;
                        ClearPersistedSchedule();
                        scheduledTargetTime = null;
                        RunShutdownCommand(scheduledForceFlag ? "/l /f" : "/l");
                    }
                    else if (remaining.TotalSeconds < -2)
                    {
                        scheduledTargetTime = null;
                        ClearPersistedSchedule();
                    }
                }
            }
        }

        private void RefreshStatusDisplay()
        {
            if (scheduledTargetTime.HasValue)
            {
                TimeSpan remaining = scheduledTargetTime.Value - DateTime.Now;
                if (remaining.TotalSeconds > 0)
                {
                    lblStatusIcon.ForeColor = Color.FromArgb(210, 35, 35); // Active Red Indicator
                    string countdownStr = FormatCountdown(remaining);
                    string targetStr = scheduledTargetTime.Value.ToString("ddd h:mm:ss tt");
                    lblStatusText.Text = string.Format(
                        "{0} scheduled for {1}  (in {2})",
                        scheduledActionName, targetStr, countdownStr);
                    return;
                }
            }

            lblStatusIcon.ForeColor = Color.FromArgb(130, 140, 150);
            lblStatusText.Text = "Ready — No power action currently scheduled.";
        }

        private static string FormatCountdown(TimeSpan ts)
        {
            int totalDays = (int)Math.Floor(ts.TotalDays);
            int hours = ts.Hours;
            int mins = ts.Minutes;
            int secs = ts.Seconds;

            if (totalDays > 0)
            {
                return string.Format("{0}d {1:D2}h {2:D2}m {3:D2}s", totalDays, hours, mins, secs);
            }
            return string.Format("{0:D2}h {1:D2}m {2:D2}s", hours, mins, secs);
        }

        private void BtnSchedule_Click(object sender, EventArgs e)
        {
            DateTime now = DateTime.Now;
            DateTime targetTime;
            long totalSeconds;

            if (rbModeTimeOfDay.Checked)
            {
                targetTime = new DateTime(
                    now.Year, now.Month, now.Day,
                    dtpTimeOfDay.Value.Hour, dtpTimeOfDay.Value.Minute, dtpTimeOfDay.Value.Second);

                if (targetTime <= now)
                {
                    targetTime = targetTime.AddDays(1);
                }

                totalSeconds = (long)Math.Ceiling((targetTime - now).TotalSeconds);
            }
            else
            {
                int days = (int)nudDays.Value;
                int hours = (int)nudHours.Value;
                int mins = (int)nudMinutes.Value;

                totalSeconds = (days * 86400L) + (hours * 3600L) + (mins * 60L);
                if (totalSeconds <= 0)
                {
                    MessageBox.Show(
                        this,
                        "Please enter a duration of at least 1 minute.",
                        "Invalid Duration",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                targetTime = now.AddSeconds(totalSeconds);
            }

            // Windows shutdown.exe /t maximum valid timeout is 315,360,000 seconds (10 years)
            if (totalSeconds > 315360000L)
            {
                MessageBox.Show(
                    this,
                    "The maximum supported duration in Windows is 10 years (3,650 days).",
                    "Duration Too Long",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            string actionFlag = GetSelectedActionFlag();
            string actionName = GetSelectedActionName();
            bool force = chkForce.Checked;

            // Cancel any existing scheduled shutdown first so we can cleanly replace it
            RunShutdownCommand("/a");

            if (actionFlag == "/l")
            {
                // Note: Windows 'shutdown.exe /l' does not accept '/t xxx' directly.
                // To ensure Log Off still happens even if this app is closed, we schedule a silent background
                // command via Windows timeout/ping that invokes 'shutdown /l', or we use shutdown /s /r natively.
                ScheduleBackgroundLogoff(totalSeconds, force);
            }
            else
            {
                string comment = string.Format("Scheduled {0} by Off You Go at {1}", actionName, targetTime.ToString("g"));
                string args = string.Format("{0} /t {1}{2} /c \"{3}\"",
                    actionFlag,
                    totalSeconds,
                    force ? " /f" : "",
                    comment);

                int exitCode = RunShutdownCommand(args);
                if (exitCode != 0)
                {
                    MessageBox.Show(
                        this,
                        string.Format("Windows shutdown.exe returned code {0}. Please verify your settings.", exitCode),
                        "Schedule Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }
            }

            scheduledTargetTime = targetTime;
            scheduledActionName = actionName;
            scheduledActionFlag = actionFlag;
            scheduledForceFlag = force;
            logoffTriggeredByApp = false;

            SavePersistedSchedule(targetTime, actionName, actionFlag, force);
            RefreshStatusDisplay();
        }

        private void BtnCancelShutdown_Click(object sender, EventArgs e)
        {
            // Abort Windows native shutdown timer
            int exitCode = RunShutdownCommand("/a");
            // Also terminate any background logoff timer helper if one was started
            CancelBackgroundLogoff();

            bool hadLocalSchedule = scheduledTargetTime.HasValue;
            scheduledTargetTime = null;
            ClearPersistedSchedule();
            RefreshStatusDisplay();

            // Exit code 1116 means "Unable to abort the system shutdown because no shutdown was in progress."
            if (exitCode == 0 || hadLocalSchedule)
            {
                lblStatusIcon.ForeColor = Color.FromArgb(30, 140, 60);
                lblStatusText.Text = "Scheduled power action has been cancelled.";
                MessageBox.Show(
                    this,
                    "Any scheduled Windows shutdown, restart, or log off has been cancelled.",
                    "Shutdown Cancelled",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                lblStatusText.Text = "No active Windows shutdown was in progress.";
                MessageBox.Show(
                    this,
                    "No scheduled shutdown is currently active in Windows.",
                    "Nothing to Cancel",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        private static int RunShutdownCommand(string arguments)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "shutdown.exe",
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using (Process p = Process.Start(psi))
                {
                    p.WaitForExit(4000);
                    return p.ExitCode;
                }
            }
            catch
            {
                return -1;
            }
        }

        private void ScheduleBackgroundLogoff(long seconds, bool force)
        {
            CancelBackgroundLogoff();
            try
            {
                string forceArg = force ? " /f" : "";
                string psCommand = string.Format(
                    "$Host.UI.RawUI.WindowTitle = 'OffYouGo_LogoffTimer'; Start-Sleep -Seconds {0}; shutdown.exe /l{1}",
                    seconds, forceArg);

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -WindowStyle Hidden -Command \"" + psCommand + "\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process p = Process.Start(psi);
                if (p != null)
                {
                    SaveLogoffHelperPid(p.Id);
                }
            }
            catch { }
        }

        private void CancelBackgroundLogoff()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegKeyPath, true))
                {
                    if (key != null)
                    {
                        object pidObj = key.GetValue("LogoffPid");
                        if (pidObj != null)
                        {
                            int pid;
                            if (int.TryParse(pidObj.ToString(), out pid))
                            {
                                try
                                {
                                    Process p = Process.GetProcessById(pid);
                                    if (p != null && !p.HasExited && p.ProcessName.ToLowerInvariant().Contains("powershell"))
                                    {
                                        p.Kill();
                                    }
                                }
                                catch { }
                            }
                            key.DeleteValue("LogoffPid", false);
                        }
                    }
                }
            }
            catch { }
        }

        private void SaveLogoffHelperPid(int pid)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegKeyPath))
                {
                    if (key != null)
                    {
                        key.SetValue("LogoffPid", pid);
                    }
                }
            }
            catch { }
        }

        private void SavePersistedSchedule(DateTime targetUtcOrLocal, string actionName, string actionFlag, bool force)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegKeyPath))
                {
                    if (key != null)
                    {
                        key.SetValue("TargetTicks", targetUtcOrLocal.Ticks, RegistryValueKind.QWord);
                        key.SetValue("ActionName", actionName, RegistryValueKind.String);
                        key.SetValue("ActionFlag", actionFlag, RegistryValueKind.String);
                        key.SetValue("Force", force ? 1 : 0, RegistryValueKind.DWord);
                    }
                }
            }
            catch { }
        }

        private void LoadPersistedSchedule()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegKeyPath, false))
                {
                    if (key != null)
                    {
                        object ticksObj = key.GetValue("TargetTicks");
                        if (ticksObj != null)
                        {
                            long ticks = Convert.ToInt64(ticksObj);
                            DateTime savedTarget = new DateTime(ticks, DateTimeKind.Local);
                            if (savedTarget > DateTime.Now)
                            {
                                scheduledTargetTime = savedTarget;
                                scheduledActionName = (key.GetValue("ActionName") as string) ?? "Shutdown";
                                scheduledActionFlag = (key.GetValue("ActionFlag") as string) ?? "/s";
                                scheduledForceFlag = Convert.ToInt32(key.GetValue("Force", 0)) == 1;
                            }
                            else
                            {
                                ClearPersistedSchedule();
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private void ClearPersistedSchedule()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegKeyPath))
                {
                    if (key != null)
                    {
                        key.DeleteValue("TargetTicks", false);
                        key.DeleteValue("ActionName", false);
                        key.DeleteValue("ActionFlag", false);
                        key.DeleteValue("Force", false);
                    }
                }
            }
            catch { }
        }
    }
}
