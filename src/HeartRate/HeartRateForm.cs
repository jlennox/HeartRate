using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static HeartRate.User32;

namespace HeartRate;

public partial class HeartRateForm : Form
{
    // Excessively call the main rendering function to force any leaks that
    // could happen.
    private const bool _leaktest = false;

    private readonly IHeartRateService _service;
    private readonly object _disposeSync = new();
    private readonly object _updateSync = new();
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
    private readonly Stopwatch _alertTimeout = new();
    private readonly Stopwatch _disconnectedTimeout = new();
    private readonly DateTime _startedAt;
    private readonly HeartRateServiceWatchdog _watchdog;
    private LogFile _log;
    private IBIFile _ibi;
    private UdpWriter _udp;
    private HeartRateFile _hrfile;
    private HeartRateSettings _lastSettings;

    private string _iconText;
    private readonly Queue<Font> _lastFonts = new();
    private IntPtr _oldIconHandle;

    public HeartRateForm() : this(
        Environment.CommandLine.Contains("--test")
            ? new TestHeartRateService()
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
            DebugLog.Initialize(HeartRateSettings.GetSettingsFile("logs.txt"));
            AppDomain.CurrentDomain.UnhandledException += UnhandledException;
            DebugLog.WriteLog("Starting up");
            // Order of operations -- _startedAt has to be set before
            // `LoadSettingsLocked` is called.
            _startedAt = now;

            _settings = HeartRateSettings.CreateDefault(settingsFilename);
            LoadSettingsLocked();
            _settings.Save();
            _service = service;
            _iconBitmap = new Bitmap(_iconWidth, _iconHeight);
            _iconGraphics = Graphics.FromImage(_iconBitmap);
            _measurementFont = new Font(_settings.FontName, _iconWidth, GraphicsUnit.Pixel);
            _watchdog = new HeartRateServiceWatchdog(TimeSpan.FromSeconds(10), _service);

            InitializeComponent();

            FormBorderStyle = _settings.Sizable
                ? FormBorderStyle.Sizable
                : FormBorderStyle.SizableToolWindow;

            CreateEnumSubmenu<ContentAlignment>(
                textAlignmentToolStripMenuItem,
                textAlignmentToolStripMenuItemItem_Click);

            CreateEnumSubmenu<ImageLayout>(
                backgroundImagePositionToolStripMenuItem,
                backgroundImagePositionToolStripMenuItemItem_Click);
        }
        catch
        {
            service.TryDispose();
            throw;
        }
    }

    private void UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exceptionFile = HeartRateSettings.GetSettingsFile("crash.txt");
        if (exceptionFile == null) return;
        File.WriteAllText(exceptionFile, e.ExceptionObject.ToString());

        DebugLog.WriteLog(e.ExceptionObject.ToString());
    }

    private void HeartRateForm_Load(object sender, EventArgs e)
    {
        UpdateLabelFont();
        Hide();

        Size = _settings.UIWindowSize;

        _service.HeartRateUpdated += Service_HeartRateUpdated;

        Task.Factory.StartNew(_service.InitiateDefault);

        UpdateUI();
    }

    private void Service_HeartRateUpdated(HeartRateReading reading)
    {
        try
        {
            // _leaktest is not set to true in program execution, just manually for testing
#pragma warning disable 162
            if (_leaktest)
            {
                for (var i = 0; i < 4000; ++i)
                {
                    Service_HeartRateUpdatedCore(reading);
                }

                return;
            }
#pragma warning restore 162

            Service_HeartRateUpdatedCore(reading);
        }
        catch (Exception ex)
        {
            DebugLog.WriteLog($"Exception in Service_HeartRateUpdated {ex}");

            Debugger.Break();
        }
    }

    private void Service_HeartRateUpdatedCore(HeartRateReading reading)
    {
        _log?.Reading(reading);
        _ibi?.Reading(reading);
        _udp?.Reading(reading);
        _hrfile?.Reading(reading);

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
            if (reading.IsError)
            {
                uxBpmNotifyIcon.Text = reading.Error.Truncate(60);
                iconText = reading.Error;
            }
            else if (isDisconnected)
            {
                var description = $"Disconnected {status} ({bpm})";
                uxBpmNotifyIcon.Text = description;

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
                    iconText = description;
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

        Invoke(new Action(() => {
            lock (_updateSync)
            {
                uxBpmLabel.Text = _iconText;
                uxBpmLabel.ForeColor = isWarn
                    ? _settings.UIWarnColor
                    : _settings.UIColor;

                UpdateUICore();
                Invalidate();
            }
        }));
    }

    private void UpdateUI()
    {
        Invoke(new Action(UpdateUICore));
    }

    private void UpdateUICore()
    {
        if (uxBpmLabel.Font.FontFamily.Name != _settings.UIFontName ||
            uxBpmLabel.Font.Style != _settings.UIFontStyle ||
            _lastSettings?.UIFontUseSize != _settings.UIFontUseSize ||
            _lastSettings?.UIFontSize != _settings.UIFontSize)
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

        Invalidate();
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

    private Font CreateUIFont()
    {
        if (_settings.UIFontUseSize)
        {
            return new Font(
                _settings.UIFontName, _settings.UIFontSize,
                _settings.UIFontStyle, GraphicsUnit.Pixel);
        }

        using var tempFont = new Font(
            _settings.UIFontName, ClientSize.Height,
            _settings.UIFontStyle, GraphicsUnit.Pixel);

        var size = TextRenderer.MeasureText("1234567890", tempFont);

        // If we divide by 2, we'll give a decent approximate gutter area
        // for the amount of whitespace fonts have around them.
        return new Font(
            _settings.UIFontName, size.Height / 2f,
            _settings.UIFontStyle, GraphicsUnit.Pixel);
    }

    private void UpdateLabelFontLocked()
    {
        var newFont = CreateUIFont();

        uxBpmLabel.Font = newFont;

        // This is horrible. I was having issues of potentially racing the
        // OnPaint of the label and disposing the font. So, ya. This is
        // the unfortunate result.
        lock (_lastFonts)
        {
            _lastFonts.Enqueue(newFont);

            if (_lastFonts.Count > 10)
            {
                var old = _lastFonts.Dequeue();
                old.TryDispose();
            }
        }
    }

    private void LoadSettingsLocked()
    {
        _settings.Load();
        LoadSettingsFilesLocked();
    }

    private void LoadSettingsFilesLocked()
    {
        _udp?.TryDispose();

        _log = new LogFile(_settings, FormatFilename(_settings.LogFile));
        _ibi = new IBIFile(FormatFilename(_settings.IBIFile));
        _udp = new UdpWriter(_settings);
        _hrfile = new HeartRateFile(FormatFilename(_settings.HeartRateFile));
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
        doNotScaleFontToolStripMenuItem.Checked = _settings.UIFontUseSize;
        unsetCSVOutputFileToolStripMenuItem.Visible = !string.IsNullOrWhiteSpace(_settings.LogFile);
        unsetIBIFileToolStripMenuItem.Visible = !string.IsNullOrWhiteSpace(_settings.IBIFile);
        unsetHeartRateFileToolStripMenuItem.Visible = !string.IsNullOrWhiteSpace(_settings.HeartRateFile);
        removeBackgroundImageToolStripMenuItem.Visible = !string.IsNullOrWhiteSpace(_settings.UIBackgroundFile);
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

    private void UpdateSaveFileSetting(ref string target, string filetypes)
    {
        if (!Prompt.TrySaveFile(target, filetypes, out var file)) return;

        lock (_settings)
        {
            target = file;
            _settings.Save();
            LoadSettingsFilesLocked();
        }

        UpdateSubmenus();
        UpdateUI();
    }

    private void UnsetFileSetting(ref string target)
    {
        lock (_settings)
        {
            target = " ";
            _settings.Save();
            LoadSettingsFilesLocked();
        }

        UpdateSubmenus();
        UpdateUI();
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
        lock (_updateSync)
        {
            _settings.UIWindowSizeX = Size.Width;
            _settings.UIWindowSizeY = Size.Height;
            _settings.Save();
        }

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

    private void uxExitMenuItem_Click(object sender, EventArgs e)
    {
        uxBpmNotifyIcon.Dispose();
        Environment.Exit(0);
    }

    private void editFontColorToolStripMenuItem_Click(object sender, EventArgs e) => UpdateSettingColor(ref _settings.Color);
    private void editIconFontWarningColorToolStripMenuItem_Click(object sender, EventArgs e) => UpdateSettingColor(ref _settings.WarnColor);
    private void editWindowFontColorToolStripMenuItem_Click(object sender, EventArgs e) => UpdateSettingColor(ref _settings.UIColor);
    private void editWindowFontWarningColorToolStripMenuItem_Click(object sender, EventArgs e) => UpdateSettingColor(ref _settings.UIWarnColor);
    private void setCSVOutputFileToolStripMenuItem_Click(object sender, EventArgs e) => UpdateSaveFileSetting(ref _settings.LogFile, "CSV Files|*.csv|All files (*.*)|*.*");
    private void unsetCSVOutputFileToolStripMenuItem_Click(object sender, EventArgs e) => UnsetFileSetting(ref _settings.LogFile);
    private void setHeartRateFileToolStripMenuItem_Click(object sender, EventArgs e) => UpdateSaveFileSetting(ref _settings.HeartRateFile, "Text Files|*.txt|All files (*.*)|*.*");
    private void unsetHeartRateFileToolStripMenuItem_Click(object sender, EventArgs e) => UnsetFileSetting(ref _settings.HeartRateFile);
    private void setIBIFileToolStripMenuItem_Click(object sender, EventArgs e) => UpdateSaveFileSetting(ref _settings.IBIFile, "Text Files|*.txt|All files (*.*)|*.*");
    private void unsetIBIFileToolStripMenuItem_Click(object sender, EventArgs e) => UnsetFileSetting(ref _settings.IBIFile);

    private void selectIconFontToolStripMenuItem_Click(object sender, EventArgs e)
    {
        if (!Prompt.TryFont(_settings.FontName, default, 10, out var font)) return;

        lock (_updateSync)
        {
            _settings.FontName = font.Name;
            _settings.Save();
        }

        UpdateUI();
    }

    private void selectWindowFontToolStripMenuItem_Click(object sender, EventArgs e)
    {
        if (!Prompt.TryFont(_settings.UIFontName, _settings.UIFontStyle, _settings.UIFontSize, out var font)) return;

        lock (_updateSync)
        {
            _settings.UIFontName = font.Name;
            _settings.UIFontStyle = font.Style;
            _settings.UIFontSize = (int)font.Size;
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

        UpdateSubmenus();
        UpdateUI();
    }

    private void removeBackgroundImageToolStripMenuItem_Click(object sender, EventArgs e)
    {
        lock (_updateSync)
        {
            _settings.UIBackgroundFile = null;
            _settings.Save();
        }

        UpdateSubmenus();
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

    private void doNotScaleFontToolStripMenuItem_Click(object sender, EventArgs e)
    {
        lock (_settings)
        {
            _settings.UIFontUseSize = !_settings.UIFontUseSize;
            _settings.Save();
        }

        UpdateSubmenus();
        UpdateUI();
    }

    //protected override void OnPaint(PaintEventArgs e)
    //{
    //    //base.OnPaint(e);
    //
    //    lock (_updateSync)
    //    {
    //        var flags = TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
    //
    //        switch (_settings.UITextAlignment)
    //        {
    //            case ContentAlignment.TopCenter:
    //            case ContentAlignment.TopLeft:
    //            case ContentAlignment.TopRight:
    //                flags |= TextFormatFlags.Top;
    //                break;
    //            case ContentAlignment.MiddleCenter:
    //            case ContentAlignment.MiddleLeft:
    //            case ContentAlignment.MiddleRight:
    //                flags |= TextFormatFlags.VerticalCenter;
    //                break;
    //            case ContentAlignment.BottomCenter:
    //            case ContentAlignment.BottomLeft:
    //            case ContentAlignment.BottomRight:
    //                flags |= TextFormatFlags.Bottom;
    //                break;
    //        }
    //
    //        switch (_settings.UITextAlignment)
    //        {
    //            case ContentAlignment.TopLeft:
    //            case ContentAlignment.MiddleLeft:
    //            case ContentAlignment.BottomLeft:
    //                flags |= TextFormatFlags.Left;
    //                break;
    //            case ContentAlignment.TopCenter:
    //            case ContentAlignment.MiddleCenter:
    //            case ContentAlignment.BottomCenter:
    //                flags |= TextFormatFlags.HorizontalCenter;
    //                break;
    //            case ContentAlignment.TopRight:
    //            case ContentAlignment.MiddleRight:
    //            case ContentAlignment.BottomRight:
    //                flags |= TextFormatFlags.Right;
    //                break;
    //        }
    //
    //        //TextRenderer.DrawText(
    //        //    e.Graphics, uxBpmLabel.Text, uxBpmLabel.Font,
    //        //    ClientRectangle, uxBpmLabel.ForeColor, Color.Transparent, flags);
    //        //
    //        TextRenderer.DrawText(
    //            e.Graphics, uxBpmLabel.Text, uxBpmLabel.Font,
    //            ClientRectangle, uxBpmLabel.ForeColor, Color.Transparent, flags);
    //    }
    //}
    #endregion
}