using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HeartRate;

static class Program
{
    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // Load saved settings to restore the last language choice.
        var settingsFile = HeartRateSettings.GetFilename();
        var settings = HeartRateSettings.CreateDefault(settingsFile);
        settings.Load();
        var savedLanguage = settings.Language;

        // Initialize localization: use saved language if available, otherwise auto-detect.
        LocalizationManager.Initialize(savedLanguage);

        Application.Run(new HeartRateForm());
    }
}