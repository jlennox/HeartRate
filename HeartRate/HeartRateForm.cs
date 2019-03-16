using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace HeartRate
{
    public partial class HeartRateForm : Form
    {
        // Excessively call the main rendering function to force any leaks that
        // could happen.
        private const bool _leaktest = false;

        private readonly IHeartRateService _service;
        private readonly object _disposeSync = new object();
        private readonly object _updateSync = new object();
        private readonly Bitmap _iconBitmap;
        private readonly Graphics _iconGraphics;
        private readonly HeartRateSettings _settings;
        private readonly int _iconWidth = GetSystemMetrics(SystemMetric.SmallIconX);
        private readonly int _iconHeight = GetSystemMetrics(SystemMetric.SmallIconY);
        private readonly StringFormat _iconStringFormat = new StringFormat {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        private readonly Font _measurementFont;
        private readonly Stopwatch _alertTimeout = new Stopwatch();
        private readonly Stopwatch _disconnectedTimeout = new Stopwatch();
        private readonly DateTime _startedAt;
        private readonly HeartRateServiceWatchdog _watchdog;
        private string _logfilename;

        private string _iconText;
        private Font _lastFont;
        private IntPtr _oldIconHandle;

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(SystemMetric nIndex);

        [DllImport("user32.dll")]
        private static extern int SetForegroundWindow(int hWnd);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr handle);

        private enum SystemMetric
        {
            SmallIconX = 49, // SM_CXSMICON
            SmallIconY = 50, // SM_CYSMICON
        }

        public HeartRateForm() : this(
            Environment.CommandLine.Contains("--test")
                ? (IHeartRateService)new TestHeartRateService()
                : new HeartRateService(),
            HeartRateSettings.GetFilename(),
            DateTime.Now)
        {
        }

        internal HeartRateForm(
            IHeartRateService service,
            string settingsFilename,
            DateTime now)
        {
            try
            {
                _settings = HeartRateSettings.CreateDefault(settingsFilename);
                LoadSettingsLocked();
                _settings.Save();
                _service = service;
                _startedAt = now;
                _iconBitmap = new Bitmap(_iconWidth, _iconHeight);
                _iconGraphics = Graphics.FromImage(_iconBitmap);
                _measurementFont = new Font(
                    _settings.FontName, _iconWidth,
                    GraphicsUnit.Pixel);
                _watchdog = new HeartRateServiceWatchdog(
                    TimeSpan.FromSeconds(10), _service);

                InitializeComponent();

                FormBorderStyle = _settings.Sizable
                    ? FormBorderStyle.Sizable
                    : FormBorderStyle.SizableToolWindow;
            }
            catch
            {
                TryDispose(service);
                throw;
            }
        }

        private void HeartRateForm_Load(object sender, EventArgs e)
        {
            UpdateLabelFont();
            Hide();

            try
            {
                // InitiateDefault is blocking. A better UI would show some type
                // of status during this time, but it's not super important.
                _service.InitiateDefault();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Unable to initialize bluetooth service. Exiting.\n{ex.Message}",
                    "Fatal exception",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);

                Environment.Exit(-1);
            }

            _service.HeartRateUpdated += Service_HeartRateUpdated;
        }

        private void Service_HeartRateUpdated(
            ContactSensorStatus status,
            int bpm)
        {
            try
            {
                if (_leaktest)
                {
                    for (var i = 0; i < 4000; ++i)
                    {
                        Service_HeartRateUpdatedCore(status, bpm);
                    }

                    return;
                }

                Service_HeartRateUpdatedCore(status, bpm);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in Service_HeartRateUpdated {ex}");

                Debugger.Break();
            }
        }

        private void Service_HeartRateUpdatedCore(
            ContactSensorStatus status,
            int bpm)
        {
            var isDisconnected = bpm == 0 ||
                status == ContactSensorStatus.NoContact;

            var iconText = bpm.ToString();

            var warnLevel = _settings.WarnLevel;
            var alertLevel = _settings.AlertLevel;
            // <= 0 implies disabled.
            var isWarn = warnLevel > 0 && bpm >= warnLevel;
            var isAlert = alertLevel > 0 && bpm >= alertLevel;

            lock (_updateSync)
            {
                if (isDisconnected)
                {
                    uxBpmNotifyIcon.Text = $"Disconnected {status} ({bpm})";

                    if (!_disconnectedTimeout.IsRunning)
                    {
                        _disconnectedTimeout.Start();
                    }

                    if (_disconnectedTimeout.Elapsed >
                        _settings.DisconnectedTimeout)
                    {
                        // Originally this used " ⃠" (U+20E0, "Prohibition Symbol")
                        // but MeasureString was only returning ~half of the
                        // width.
                        iconText = "X";
                    }
                }
                else
                {
                    uxBpmNotifyIcon.Text = null;
                    _disconnectedTimeout.Stop();
                }

                _iconGraphics.Clear(Color.Transparent);

                var sizingMeasurement = _iconGraphics
                    .MeasureString(iconText, _measurementFont);

                var color = isWarn ? _settings.WarnColor : _settings.Color;

                using (var brush = new SolidBrush(color))
                using (var font = new Font(_settings.FontName,
                    _iconHeight * (_iconWidth / sizingMeasurement.Width),
                    GraphicsUnit.Pixel))
                {
                    _iconGraphics.DrawString(
                        iconText, font, brush,
                        new RectangleF(0, 0, _iconWidth, _iconHeight),
                        _iconStringFormat);
                }

                _iconText = iconText;

                Invoke(new Action(() => {
                    uxBpmLabel.Text = _iconText;
                    uxBpmLabel.ForeColor = isWarn
                        ? _settings.UIWarnColor
                        : _settings.UIColor;
                    uxBpmLabel.BackColor = _settings.UIBackgroundColor;

                    var fontx = _settings.UIFontName;

                    if (uxBpmLabel.Font.FontFamily.Name != fontx)
                    {
                        UpdateLabelFontLocked();
                    }
                }));

                var iconHandle = _iconBitmap.GetHicon();

                using (var icon = Icon.FromHandle(iconHandle))
                {
                    uxBpmNotifyIcon.Icon = icon;

                    if (isAlert && (!_alertTimeout.IsRunning ||
                        _alertTimeout.Elapsed >= _settings.AlertTimeout))
                    {
                        _alertTimeout.Restart();

                        var alertText = $"BPMs @ {bpm}";

                        uxBpmNotifyIcon.ShowBalloonTip(
                            (int)_settings.AlertTimeout.TotalMilliseconds,
                            alertText, alertText, ToolTipIcon.Warning);
                    }
                }

                if (_oldIconHandle != IntPtr.Zero)
                {
                    DestroyIcon(_oldIconHandle);
                }

                _oldIconHandle = iconHandle;

                if (_logfilename != null)
                {
                    string data = null;

                    var dateString = DateTimeFormatter.Format(
                        _settings.LogDateFormat,
                        DateTime.Now,
                        DateTimeFormatter.DefaultColumn);

                    switch ((_settings.LogFormat ?? "").ToLower())
                    {
                        case "csv":
                            data = $"{dateString},{bpm},{status}\r\n";
                            break;
                    }

                    if (data != null)
                    {
                        File.AppendAllText(_settings.LogFile, data);
                    }
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                TryDispose(components);

                lock (_disposeSync)
                {
                    TryDispose(_service);
                    TryDispose(_iconBitmap);
                    TryDispose(_iconGraphics);
                    TryDispose(_measurementFont);
                    TryDispose(_iconStringFormat);
                    TryDispose(_watchdog);
                }
            }

            base.Dispose(disposing);
        }

        private static void TryDispose(IDisposable disposable)
        {
            if (disposable == null)
            {
                return;
            }

            try
            {
                disposable.Dispose();
            }
            catch { }
        }

        private void uxBpmNotifyIcon_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Show();
                SetForegroundWindow(Handle.ToInt32());
            }
        }

        private void HeartRateForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
            }
        }

        private void HeartRateForm_ResizeEnd(object sender, EventArgs e)
        {
            UpdateLabelFont();
        }

        private void UpdateLabelFont()
        {
            lock (_updateSync)
            {
                UpdateLabelFontLocked();
            }
        }

        private void UpdateLabelFontLocked()
        {
            var newFont = new Font(
                _settings.UIFontName, uxBpmLabel.Height,
                GraphicsUnit.Pixel);

            uxBpmLabel.Font = newFont;
            TryDispose(_lastFont);
            _lastFont = newFont;
        }

        private void uxMenuEditSettings_Click(object sender, EventArgs e)
        {
            var thread = new Thread(() => {
                using (var process = Process.Start(new ProcessStartInfo {
                    FileName = HeartRateSettings.GetFilename(),
                    UseShellExecute = true,
                    Verb = "EDIT"
                }))
                {
                    process.WaitForExit();
                }

                lock (_updateSync)
                {
                    LoadSettingsLocked();
                }
            }) {
                IsBackground = true,
                Name = "Edit config"
            };

            thread.Start();
        }

        private void LoadSettingsLocked()
        {
            _settings.Load();

            var logfile = _settings.LogFile;

            _logfilename = string.IsNullOrWhiteSpace(logfile)
                ? null
                : DateTimeFormatter.FormatStringTokens(logfile, _startedAt);
        }

        private void uxExitMenuItem_Click(object sender, EventArgs e)
        {
            Environment.Exit(0);
        }

        private bool TryPromptColor(Color current, out Color color)
        {
            color = default(Color);

            using (var dlg = new ColorDialog())
            {
                dlg.Color = current;

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    color = dlg.Color;
                    return true;
                }
            }

            return false;
        }

        private bool TryPromptFont(string current, out string font)
        {
            font = default(string);

            using (var dlg = new FontDialog()
            {
                FontMustExist = true
            })
            {
                using (dlg.Font = new Font(current, 10, GraphicsUnit.Pixel))
                {
                    if (dlg.ShowDialog() == DialogResult.OK)
                    {
                        font = dlg.Font.Name;
                        return true;
                    }
                }
            }

            return false;
        }

        private void editFontColorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (TryPromptColor(_settings.Color, out var color))
            {
                lock (_updateSync)
                {
                    _settings.Color = color;
                    _settings.Save();
                }
            }
        }

        private void editIconFontWarningColorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (TryPromptColor(_settings.WarnColor, out var color))
            {
                lock (_updateSync)
                {
                    _settings.WarnColor = color;
                    _settings.Save();
                }
            }
        }

        private void editWindowFontColorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (TryPromptColor(_settings.UIColor, out var color))
            {
                lock (_updateSync)
                {
                    _settings.UIColor = color;
                    _settings.Save();
                }
            }
        }

        private void editWindowFontWarningColorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (TryPromptColor(_settings.UIWarnColor, out var color))
            {
                lock (_updateSync)
                {
                    _settings.UIWarnColor = color;
                    _settings.Save();
                }
            }
        }

        private void selectIconFontToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (TryPromptFont(_settings.FontName, out var font))
            {
                lock (_updateSync)
                {
                    _settings.FontName = font;
                    _settings.Save();
                }
            }
        }

        private void selectWindowFontToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (TryPromptFont(_settings.UIFontName, out var font))
            {
                lock (_updateSync)
                {
                    _settings.UIFontName = font;
                    _settings.Save();
                }
            }
        }
    }
}
