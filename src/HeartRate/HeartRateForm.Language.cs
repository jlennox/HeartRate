using System;
using System.Windows.Forms;

namespace HeartRate;

public partial class HeartRateForm
{
    private readonly ToolStripMenuItem _languageMenu;

    internal static ToolStripMenuItem CreateLanguageMenu(string selected, Action<string> select)
    {
        var menu = new ToolStripMenuItem("Language / 语言");
        Add("Automatic", null);
        Add("English", "en");
        Add("日本語", "ja");
        Add("简体中文", "zh-Hans");
        Add("Deutsch", "de");
        Add("हिन्दी", "hi");
        Add("தமிழ்", "ta");
        Add("తెలుగు", "te");
        Add("Español", "es");
        Add("Français", "fr");
        Add("Português (Brasil)", "pt-BR");
        return menu;

        void Add(string name, string code)
        {
            var item = new ToolStripMenuItem(name)
            {
                Tag = code,
                Checked = UiText.NormalizeLanguage(selected) == code
            };
            item.Click += (_, _) => select(code);
            menu.DropDownItems.Add(item);
        }
    }

    private void SelectLanguage(string language)
    {
        lock (_updateSync)
        {
            _settings.Language = language;
            _settings.Save();
            UiText.SelectLanguage(language);
            ApplyLanguage();
        }
    }

    private void ApplyLanguage()
    {
        uxNotifyIconContextMenu.Text = UiText.Get("Background image layout");
        selectIconFontToolStripMenuItem.Text = UiText.Get("Select icon font...");
        editFontColorToolStripMenuItem.Text = UiText.Get("Edit icon font color...");
        editIconFontWarningColorToolStripMenuItem.Text = UiText.Get("Edit icon font warning color...");
        selectWindowFontToolStripMenuItem.Text = UiText.Get("Select window font...");
        doNotScaleFontToolStripMenuItem.Text = UiText.Get("Do not scale font");
        doNotScaleFontToolStripMenuItem.ToolTipText = UiText.Get("Keep the font size fixed when resizing the window. Set the size in the font dialog.");
        editWindowFontColorToolStripMenuItem.Text = UiText.Get("Edit window font color...");
        editWindowFontWarningColorToolStripMenuItem.Text = UiText.Get("Edit window font warning color...");
        textAlignmentToolStripMenuItem.Text = UiText.Get("Text alignment");
        selectBackgroundImageToolStripMenuItem.Text = UiText.Get("Select background image...");
        removeBackgroundImageToolStripMenuItem.Text = UiText.Get("Remove background image");
        backgroundImagePositionToolStripMenuItem.Text = UiText.Get("Background image position");
        uxEditSettingsMenuItem.Text = UiText.Get("Edit settings XML...");
        uxExitMenuItem.Text = UiText.Get("Exit");
        setHeartRateFileToolStripMenuItem.Text = UiText.Get("Set heart rate file...");
        setHeartRateFileToolStripMenuItem.ToolTipText = UiText.Get("Write the latest heart rate to this file, replacing its previous contents. Supports date tokens such as %date:MM-dd-yyyy%.");
        unsetHeartRateFileToolStripMenuItem.Text = UiText.Get("Unset heart rate file");
        setCSVOutputFileToolStripMenuItem.Text = UiText.Get("Set CSV output file...");
        setCSVOutputFileToolStripMenuItem.ToolTipText = UiText.Get("Write recorded readings to this file. Leave empty to disable file logging. Supports %date% or a custom format such as %date:MM-dd-yyyy%.");
        unsetCSVOutputFileToolStripMenuItem.Text = UiText.Get("Unset CSV output file");
        setIBIFileToolStripMenuItem.Text = UiText.Get("Set IBI file...");
        setIBIFileToolStripMenuItem.ToolTipText = UiText.Get("Write RR intervals in milliseconds in IBI format. Supports date tokens such as %date:MM-dd-yyyy%.");
        unsetIBIFileToolStripMenuItem.Text = UiText.Get("Unset IBI file");
        Text = UiText.Get("Heart rate monitor");
        foreach (ToolStripMenuItem item in _languageMenu.DropDownItems)
            item.Checked = (string)item.Tag == UiText.NormalizeLanguage(_settings.Language);
        UpdateSubmenus();
        if (_iconText == null)
        {
            uxBpmLabel.Text = UiText.Get("Starting...");
            uxBpmNotifyIcon.Text = UiText.Get("Starting...");
        }
    }
}
