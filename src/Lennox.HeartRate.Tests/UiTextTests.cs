using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HeartRate;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lennox.HeartRate.Tests;

[TestClass]
public class UiTextTests
{
    [TestMethod]
    public void LanguageMenuUsesFixedNativeNamesAndSelectsLanguageCodes()
    {
        using var menu = HeartRateForm.CreateLanguageMenu("ja", UiText.SelectLanguage);
        var names = new[] { "Automatic", "English", "日本語", "简体中文", "Deutsch",
            "हिन्दी", "தமிழ்", "తెలుగు", "Español", "Français", "Português (Brasil)" };
        var exits = new[] { "Exit", "Exit", "終了", "退出", "Beenden",
            "बंद करें", "வெளியேறு", "నిష్క్రమించు", "Salir", "Quitter", "Sair" };
        var oldCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            Assert.AreEqual("Language / 语言", menu.Text);
            Assert.AreEqual(names.Length, menu.DropDownItems.Count);
            for (var i = 0; i < names.Length; i++)
            {
                var item = (ToolStripMenuItem)menu.DropDownItems[i];
                Assert.AreEqual(names[i], item.Text);
                Assert.AreEqual(i == 2, item.Checked);
                item.PerformClick();
                Assert.AreEqual(exits[i], UiText.Get("Exit"));
                Assert.AreEqual("Language / 语言", menu.Text);
                Assert.AreEqual(names[i], item.Text);
            }
        }
        finally
        {
            UiText.SelectLanguage(null);
            CultureInfo.CurrentUICulture = oldCulture;
        }
    }

    [TestMethod]
    public void SavedLanguageRoundTripsAndOldSettingsUseWindowsLanguage()
    {
        using var file = new TempFile();
        var settings = HeartRateSettings.CreateDefault(file);
        settings.Save();
        settings.Load();
        Assert.IsNull(settings.Language);
        foreach (var language in new[] { "en", "ja", "zh-Hans", "de", "hi", "ta", "te", "es", "fr", "pt-BR", null })
        {
            settings.Language = language;
            settings.Save();
            var loaded = HeartRateSettings.CreateDefault(file);
            loaded.Load();
            Assert.AreEqual(language, loaded.Language);
            Assert.AreEqual(language, loaded.Clone().Language);
        }
        settings.Language = "invalid-language";
        settings.Save();
        settings.Load();
        Assert.IsNull(settings.Language);
    }

    [TestMethod]
    public void LanguageSwitchAppliesToExistingWorkersWithoutChangingTheirCulture()
    {
        using var ready = new ManualResetEventSlim();
        using var selected = new ManualResetEventSlim();
        var worker = Task.Run(() =>
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            ready.Set();
            Assert.IsTrue(selected.Wait(TimeSpan.FromSeconds(5)));
            Assert.AreEqual("終了", UiText.Get("Exit"));
            Assert.AreEqual("en-US", CultureInfo.CurrentUICulture.Name);
            Assert.AreEqual("en-US", CultureInfo.CurrentCulture.Name);
        });
        try
        {
            Assert.IsTrue(ready.Wait(TimeSpan.FromSeconds(5)));
            UiText.SelectLanguage("ja");
            selected.Set();
            worker.GetAwaiter().GetResult();
        }
        finally
        {
            selected.Set();
            UiText.SelectLanguage(null);
        }
    }

    [TestMethod]
    public void RegionalDisplayLanguagesUseTheirParentTranslation()
    {
        Assert.AreEqual("終了", UiText.Get("Exit", CultureInfo.GetCultureInfo("ja-JP")));
        Assert.AreEqual("Beenden", UiText.Get("Exit", CultureInfo.GetCultureInfo("de-AT")));
        foreach (var name in new[] { "zh-CN", "zh-SG", "zh-Hans" })
            Assert.AreEqual("退出", UiText.Get("Exit", CultureInfo.GetCultureInfo(name)));
        Assert.AreEqual("बंद करें", UiText.Get("Exit", CultureInfo.GetCultureInfo("hi-IN")));
        foreach (var name in new[] { "ta-IN", "ta-LK" })
            Assert.AreEqual("வெளியேறு", UiText.Get("Exit", CultureInfo.GetCultureInfo(name)));
        Assert.AreEqual("నిష్క్రమించు", UiText.Get("Exit", CultureInfo.GetCultureInfo("te-IN")));
        foreach (var name in new[] { "es-ES", "es-MX" })
            Assert.AreEqual("Salir", UiText.Get("Exit", CultureInfo.GetCultureInfo(name)));
        foreach (var name in new[] { "fr-FR", "fr-CA" })
            Assert.AreEqual("Quitter", UiText.Get("Exit", CultureInfo.GetCultureInfo(name)));
        Assert.AreEqual("Sair", UiText.Get("Exit", CultureInfo.GetCultureInfo("pt-BR")));
    }

    [TestMethod]
    public void UnsupportedCulturesAndUnknownTextFallBackToEnglish()
    {
        foreach (var name in new[] { "", "en-US", "it-IT", "zh-TW", "zh-HK", "pt-PT" })
            Assert.AreEqual("Exit", UiText.Get("Exit", CultureInfo.GetCultureInfo(name)));
        Assert.AreEqual("Unknown text", UiText.Get("Unknown text", CultureInfo.GetCultureInfo("ja-JP")));
    }

    [TestMethod]
    public void EveryTranslationPreservesFormatArgumentsAndDateTokens()
    {
        var translations = (Dictionary<string, string[]>)typeof(UiText)
            .GetField("Translations", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        foreach (var entry in translations)
        {
            Assert.AreEqual(9, entry.Value.Length, entry.Key);
            foreach (var value in entry.Value)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(value), entry.Key);
                var formatted = string.Format(CultureInfo.InvariantCulture, value, "ARG_ZERO", "ARG_ONE");
                for (var i = 0; i < 2; i++)
                    Assert.AreEqual(entry.Key.Contains("{" + i + "}"), formatted.Contains(i == 0 ? "ARG_ZERO" : "ARG_ONE"), entry.Key);
                foreach (var token in new[] { "%date%", "%date:MM-dd-yyyy%" })
                    Assert.AreEqual(entry.Key.Contains(token), value.Contains(token), entry.Key);
            }
        }
    }

    [TestMethod]
    public void EveryMenuLanguageHasCompleteTranslationsAndPreservesFilePatterns()
    {
        var languages = new[] { "ja", "zh-Hans", "de", "hi", "ta", "te", "es", "fr", "pt-BR" };
        var translations = (Dictionary<string, string[]>)typeof(UiText)
            .GetField("Translations", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        using var menu = HeartRateForm.CreateLanguageMenu(null, _ => { });
        try
        {
            foreach (ToolStripMenuItem item in menu.DropDownItems)
            {
                var code = (string)item.Tag;
                if (code == null || code == "en") continue;
                var culture = CultureInfo.GetCultureInfo(code);
                var column = Array.IndexOf(languages, code);
                Assert.IsTrue(column >= 0, code);
                foreach (var entry in translations)
                    Assert.AreEqual(entry.Value[column], UiText.Get(entry.Key, culture), code + ": " + entry.Key);
                UiText.SelectLanguage(code);
                var parts = UiText.FileFilter("Image files|*.bmp;*.gif;*.jpeg;*.png;*.tiff|All files (*.*)|*.*").Split('|');
                Assert.AreEqual(4, parts.Length, code);
                Assert.AreEqual("*.bmp;*.gif;*.jpeg;*.png;*.tiff", parts[1], code);
                Assert.AreEqual("*.*", parts[3], code);
            }
        }
        finally
        {
            UiText.SelectLanguage(null);
        }
    }

    [TestMethod]
    public void DisplayLanguageDoesNotChangeFilePatternsOrDataCulture()
    {
        var oldUiCulture = CultureInfo.CurrentUICulture;
        var oldCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            Assert.AreEqual("CSV-Dateien|*.csv|Alle Dateien (*.*)|*.*",
                UiText.FileFilter("CSV Files|*.csv|All files (*.*)|*.*"));
            Assert.AreEqual("Herzfrequenz: 72.5 Schläge/min", UiText.Format("BPMs @ {0}", 72.5));
            Assert.AreEqual("en-US", CultureInfo.CurrentCulture.Name);
        }
        finally
        {
            CultureInfo.CurrentUICulture = oldUiCulture;
            CultureInfo.CurrentCulture = oldCulture;
        }
    }
}
