using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace HeartRate
{
    public partial class HeartRateForm : Form
    {
        private readonly HeartRateService _service;
        private readonly object _disposeSync = new object();
        private readonly object _updateSync = new object();
        private readonly Bitmap _iconBitmap;
        private readonly Graphics _iconGraphics;
        private readonly HeartRateSettings _settings = HeartRateSettings.CreateDefault();
        private readonly int _iconWidth = GetSystemMetrics(SystemMetric.SmallIconX);
        private readonly int _iconHeight = GetSystemMetrics(SystemMetric.SmallIconY);
        private readonly StringFormat _iconStringFormat = new StringFormat {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        private readonly Font _measurementFont;
        private readonly Stopwatch _alertTimeout = new Stopwatch();
        private readonly Stopwatch _disconnectedTimeout = new Stopwatch();

        private string _iconText;

        [DllImport("User32.dll")]
        private static extern int GetSystemMetrics(SystemMetric nIndex);

        [DllImport("User32.dll")]
        private static extern int SetForegroundWindow(int hWnd);

        private enum SystemMetric
        {
            SmallIconX = 49, // SM_CXSMICON
            SmallIconY = 50, // SM_CYSMICON
        }

        public HeartRateForm()
        {
            _settings.Load();
            _settings.Save();
            _service = new HeartRateService();
            _iconBitmap = new Bitmap(_iconWidth, _iconHeight);
            _iconGraphics = Graphics.FromImage(_iconBitmap);
            _measurementFont = new Font(
                _settings.FontName, _iconWidth,
                GraphicsUnit.Pixel);

            InitializeComponent();
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
            var isDisconnected = bpm == 0 ||
                status != ContactSensorStatus.Contact;

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

                    var font = _settings.UIFontName;

                    if (uxBpmLabel.Font.FontFamily.Name != font)
                    {
                        UpdateLabelFont();
                    }
                }));

                using (var icon = Icon.FromHandle(_iconBitmap.GetHicon()))
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
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (components != null)
                {
                    components.Dispose();
                }

                lock (_disposeSync)
                {
                    TryDispose(_service);
                    TryDispose(_iconBitmap);
                    TryDispose(_iconGraphics);
                    TryDispose(_measurementFont);
                    TryDispose(_iconStringFormat);
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
            uxBpmLabel.Font = new Font(
                _settings.UIFontName, uxBpmLabel.Height,
                GraphicsUnit.Pixel);
        }

        private void uxMenuEditSettings_Click(object sender, EventArgs e)
        {
            var thread = new Thread(() => {
                lock (_updateSync)
                {
                    _settings.Save();
                }

                using (var process = Process.Start(new ProcessStartInfo {
                    FileName = HeartRateSettings.Filename,
                    UseShellExecute = true,
                    Verb = "EDIT"
                }))
                {
                    process.WaitForExit();
                }

                lock (_updateSync)
                {
                    _settings.Load();
                }
            }) {
                IsBackground = true,
                Name = "Edit config"
            };

            thread.Start();
        }

        private void uxExitMenuItem_Click(object sender, EventArgs e)
        {
            Environment.Exit(0);
        }
    }
}
