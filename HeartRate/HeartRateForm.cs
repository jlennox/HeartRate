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
        private LogFile _log;
        private IBIFile _ibi;
        private HeartRateSettings _lastSettings;

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
                // Order of operations -- _startedAt has to be set before
                // `LoadSettingsLocked` is called.
                _startedAt = now;

                _settings = HeartRateSettings.CreateDefault(settingsFilename);
                LoadSettingsLocked();
                _settings.Save();
                _service = service;
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

                CreateEnumSubmenu<ContentAlignment>(textAlignmentToolStripMenuItem,
                    textAlignmentToolStripMenuItemItem_Click);

                CreateEnumSubmenu<ImageLayout>(backgroundImagePositionToolStripMenuItem,
                    backgroundImagePositionToolStripMenuItemItem_Click);
            }
            catch
            {
                service.TryDispose();
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

            UpdateUI();
        }

        private void Service_HeartRateUpdated(HeartRateReading reading)
        {
            try
            {
                if (_leaktest)
                {
                    for (var i = 0; i < 4000; ++i)
                    {
                        Service_HeartRateUpdatedCore(reading);
                    }

                    return;
                }

                Service_HeartRateUpdatedCore(reading);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in Service_HeartRateUpdated {ex}");

                Debugger.Break();
            }
        }

        private void Service_HeartRateUpdatedCore(HeartRateReading reading)
        {
            _log?.Reading(reading);
            _ibi?.Reading(reading);

            var bpm = reading.BeatsPerMinute;
            var status = reading.Status;

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

                    UpdateUICore();
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
            }
        }

        private void UpdateUI()
        {
            Invoke(new Action(UpdateUICore));
        }

        private void UpdateUICore()
        {
            // Sometimes firstUpdate will be checked to update corresponding UI components.
            var firstUpdate = _lastSettings == null;

            if (uxBpmLabel.Font.FontFamily.Name != _settings.UIFontName ||
                uxBpmLabel.Font.Style != _settings.UIFontStyle)
            {
                UpdateLabelFontLocked();
            }

            if (uxBpmLabel.TextAlign != _settings.UITextAlignment)
            {
                uxBpmLabel.TextAlign = _settings.UITextAlignment;
                UpdateSubmenus();
            }

            if (uxBpmLabel.BackgroundImageLayout != _settings.UIBackgroundLayout)
            {
                uxBpmLabel.BackgroundImageLayout = _settings.UIBackgroundLayout;
                UpdateSubmenus();
            }

            if (uxBpmLabel.BackColor != _settings.UIBackgroundColor)
            {
                uxBpmLabel.BackColor = _settings.UIBackgroundColor;
            }

            if (_lastSettings?.UIBackgroundFile != _settings.UIBackgroundFile)
            {
                var oldBackgroundImage = uxBpmLabel.BackgroundImage;
                var backgroundFile = _settings.UIBackgroundFile;

                if (!string.IsNullOrWhiteSpace(backgroundFile) &&
                    File.Exists(backgroundFile))
                {
                    try
                    {
                        var image = Image.FromFile(backgroundFile);
                        uxBpmLabel.BackgroundImage = image;
                        oldBackgroundImage.TryDispose();
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show($"Unable to load background image file \"{backgroundFile}\" due to error: {e}");
                    }
                }
                else
                {
                    uxBpmLabel.BackgroundImage = null;
                    oldBackgroundImage.TryDispose();
                }
            }

            _lastSettings = _settings.Clone();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                components.TryDispose();

                lock (_disposeSync)
                {
                    _service.TryDispose();
                    _iconBitmap.TryDispose();
                    _iconGraphics.TryDispose();
                    _measurementFont.TryDispose();
                    _iconStringFormat.TryDispose();
                    _watchdog.TryDispose();
                    _log.TryDispose();
                    _ibi.TryDispose();
                }
            }

            base.Dispose(disposing);
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
                _settings.UIFontName, uxBpmLabel.Height * .8f,
                _settings.UIFontStyle, GraphicsUnit.Pixel);

            uxBpmLabel.Font = newFont;
            _lastFont.TryDispose();
            _lastFont = newFont;
        }

        private void LoadSettingsLocked()
        {
            _settings.Load();

            _log.TryDispose();
            _log = new LogFile(_settings, FormatFilename(_settings.LogFile));
            _ibi = new IBIFile(FormatFilename(_settings.IBIFile));
        }

        private string FormatFilename(string inputFilename)
        {
            return string.IsNullOrWhiteSpace(inputFilename)
                ? null
                : DateTimeFormatter.FormatStringTokens(
                    inputFilename, _startedAt, forFilepath: true);
        }

        private void UpdateSettingColor(ref Color settingColor)
        {
            if (!Prompt.TryColor(settingColor, out var color)) return;

            lock (_updateSync)
            {
                settingColor = color;
                _settings.Save();
            }

            UpdateUI();
        }

        private void UpdateSubmenus()
        {
            UpdateEnumSubmenu(_settings.UITextAlignment, textAlignmentToolStripMenuItem);
            UpdateEnumSubmenu(_settings.UIBackgroundLayout, backgroundImagePositionToolStripMenuItem);
        }

        private static void UpdateEnumSubmenu<TEnum>(TEnum value, ToolStripMenuItem parent)
        {
            var stringed = value.ToString();

            foreach (var item in parent.DropDownItems)
            {
                var menuItem = (ToolStripMenuItem)item;
                menuItem.CheckState = (string)menuItem.Tag == stringed
                    ? CheckState.Checked : CheckState.Unchecked;
            }
        }

        private static void CreateEnumSubmenu<TEnum>(ToolStripMenuItem parent, EventHandler click)
        {
            foreach (var align in Enum.GetNames(typeof(TEnum)))
            {
                var strip = new ToolStripMenuItem
                {
                    Text = align,
                    Tag = align
                };
                strip.Click += click;
                parent.DropDownItems.Add(strip);
            }
        }

        private static TEnum EnumFromMenuItemTag<TEnum>(object sender)
        {
            var menuItem = (ToolStripMenuItem)sender;
            return (TEnum)Enum.Parse(typeof(TEnum), menuItem.Tag.ToString());
        }

        #region UI events
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
            UpdateUI();
        }

        private void uxMenuEditSettings_Click(object sender, EventArgs e)
        {
            var thread = new Thread(() => {
                using (var process = Process.Start(new ProcessStartInfo
                {
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
            })
            {
                IsBackground = true,
                Name = "Edit config"
            };

            thread.Start();
        }

        private void uxExitMenuItem_Click(object sender, EventArgs e) => Environment.Exit(0);
        private void editFontColorToolStripMenuItem_Click(object sender, EventArgs e) => UpdateSettingColor(ref _settings.Color);
        private void editIconFontWarningColorToolStripMenuItem_Click(object sender, EventArgs e) => UpdateSettingColor(ref _settings.WarnColor);
        private void editWindowFontColorToolStripMenuItem_Click(object sender, EventArgs e) => UpdateSettingColor(ref _settings.UIColor);
        private void editWindowFontWarningColorToolStripMenuItem_Click(object sender, EventArgs e) => UpdateSettingColor(ref _settings.UIWarnColor);

        private void selectIconFontToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!Prompt.TryFont(_settings.FontName, default, out var font, out _)) return;

            lock (_updateSync)
            {
                _settings.FontName = font;
                _settings.Save();
            }

            UpdateUI();
        }

        private void selectWindowFontToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!Prompt.TryFont(_settings.UIFontName, _settings.UIFontStyle, out var font, out var style)) return;

            lock (_updateSync)
            {
                _settings.UIFontName = font;
                _settings.UIFontStyle = style;
                _settings.Save();
            }

            UpdateUI();
        }

        private void selectBackgroundImageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!Prompt.TryFile(_settings.UIBackgroundFile, "Image files|*.bmp;*.gif;*.jpeg;*.png;*.tiff|All files (*.*)|*.*", out var file)) return;

            lock (_updateSync)
            {
                _settings.UIBackgroundFile = file;
                _settings.Save();
            }

            UpdateUI();
        }

        private void removeBackgroundImageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            lock (_updateSync)
            {
                _settings.UIBackgroundFile = null;
                _settings.Save();
            }

            UpdateUI();
        }

        private void backgroundImagePositionToolStripMenuItemItem_Click(object sender, EventArgs e)
        {
            var layout = EnumFromMenuItemTag<ImageLayout>(sender);

            lock (_updateSync)
            {
                _settings.UIBackgroundLayout = layout;
                _settings.Save();
            }

            UpdateSubmenus();
            UpdateUI();
        }

        private void textAlignmentToolStripMenuItemItem_Click(object sender, EventArgs e)
        {
            var alignment = EnumFromMenuItemTag<ContentAlignment>(sender);

            lock (_updateSync)
            {
                _settings.UITextAlignment = alignment;
                _settings.Save();
            }

            UpdateSubmenus();
            UpdateUI();
        }
        #endregion
    }
}
