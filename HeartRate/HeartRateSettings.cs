using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Xml.Serialization;

namespace HeartRate
{
    public class HeartRateSettings
    {
        public static readonly string Filename = GetFilename();

        // See note in Load for how to version the file.
        private const int _settingsVersion = 1;

        public int Version { get; set; }
        public string FontName { get; set; }
        public string UIFontName { get; set; }
        public int AlertLevel { get; set; }
        public int WarnLevel { get; set; }
        public TimeSpan AlertTimeout { get; set; }
        public TimeSpan DisconnectedTimeout { get; set; }
        public Color Color { get; set; }
        public Color WarnColor { get; set; }
        public Color UIColor { get; set; }
        public Color UIWarnColor { get; set; }
        public Color UIBackgroundColor { get; set; }
        public bool Sizable { get; set; }

        public static HeartRateSettings CreateDefault()
        {
            return new HeartRateSettings {
                Version = _settingsVersion,
                FontName = "Arial",
                UIFontName = "Arial",
                WarnLevel = 65,
                AlertLevel = 70,
                AlertTimeout = TimeSpan.FromMinutes(2),
                DisconnectedTimeout = TimeSpan.FromSeconds(10),
                Color = Color.LightBlue,
                WarnColor = Color.Red,
                UIColor = Color.DarkBlue,
                UIWarnColor = Color.Red,
                UIBackgroundColor = Color.Transparent,
                Sizable = true
            };
        }

        public void Save()
        {
            HeartRateSettingsProtocol.Save(this);
        }

        public void Load()
        {
            var protocol = HeartRateSettingsProtocol.Load();

            if (protocol == null)
            {
                return;
            }

            FontName = protocol.FontName;
            UIFontName = protocol.UIFontName;
            AlertLevel = protocol.AlertLevel;
            WarnLevel = protocol.WarnLevel;
            AlertTimeout = TimeSpan.FromMilliseconds(protocol.AlertTimeout);
            DisconnectedTimeout = TimeSpan.FromMilliseconds(protocol.DisconnectedTimeout);
            Color = ColorFromString(protocol.Color);
            WarnColor = ColorFromString(protocol.WarnColor);
            UIColor = ColorFromString(protocol.UIColor);
            UIWarnColor = ColorFromString(protocol.UIWarnColor);
            UIBackgroundColor = ColorFromString(protocol.UIBackgroundColor);
            Sizable = protocol.Sizable;

            // In the future:
            // if (protocol.Version >= 2) ...
        }

        private static Color ColorFromString(string s)
        {
            return Color.FromArgb(Convert.ToInt32(s, 16));
        }

        private static string GetFilename()
        {
            var dataPath = Environment.ExpandEnvironmentVariables("%appdata%");

            if (string.IsNullOrEmpty(dataPath))
            {
                // This is bad. Irl error handling is needed.
                return null;
            }

            var appDir = Path.Combine(dataPath, "HeartRate");

            // Arg, do this better.
            try
            {
                Directory.CreateDirectory(appDir);
            }
            catch
            {
                return null;
            }

            return Path.Combine(appDir, "settings.xml");
        }
    }

    // The object which is serialized to/from XML. XmlSerializer has poor
    // type support. HeartRateSettingsProtocol is public to appease
    // XmlSerializer.
    public class HeartRateSettingsProtocol
    {
        // XmlSerializer is used to avoid third party dependencies. It's not
        // pretty.
        private static readonly XmlSerializer _serializer =
            new XmlSerializer(typeof(HeartRateSettingsProtocol));

        private static readonly string _filename = HeartRateSettings.Filename;

        public int Version { get; set; }
        public string FontName { get; set; }
        public string UIFontName { get; set; }
        public int AlertLevel { get; set; }
        public int WarnLevel { get; set; }
        public int AlertTimeout { get; set; }
        public int DisconnectedTimeout { get; set; }
        public string Color { get; set; }
        public string WarnColor { get; set; }
        public string UIColor { get; set; }
        public string UIWarnColor { get; set; }
        public string UIBackgroundColor { get; set; }
        public bool Sizable { get; set; }

        public HeartRateSettingsProtocol() { }

        private HeartRateSettingsProtocol(HeartRateSettings settings)
        {
            Version = settings.Version;
            FontName = settings.FontName;
            AlertLevel = settings.AlertLevel;
            UIFontName = settings.UIFontName;
            WarnLevel = settings.WarnLevel;
            AlertTimeout = (int)settings.AlertTimeout.TotalMilliseconds;
            DisconnectedTimeout = (int)settings.DisconnectedTimeout.TotalMilliseconds;
            Color = ColorToString(settings.Color);
            WarnColor = ColorToString(settings.WarnColor);
            UIColor = ColorToString(settings.UIColor);
            UIWarnColor = ColorToString(settings.UIWarnColor);
            UIBackgroundColor = ColorToString(settings.UIBackgroundColor);
            Sizable = settings.Sizable;
        }

        private static string ColorToString(Color color)
        {
            return color.ToArgb().ToString("X").PadLeft(8, '0');
        }

        public static HeartRateSettingsProtocol Load()
        {
            Debug.WriteLine($"Loading from {_filename}");

            if (_filename == null)
            {
                throw new FileNotFoundException(
                    $"Unable to read file {_filename}");
            }

            if (!File.Exists(_filename))
            {
                return null;
            }

            // Exception timebomb #1
            using (var fs = File.OpenRead(_filename))
            {
                // Exception timebomb #2
                return _serializer.Deserialize(fs) as HeartRateSettingsProtocol;
            }
        }

        public static void Save(HeartRateSettings settings)
        {
            Debug.WriteLine($"Saving to {_filename}");

            var protocol = new HeartRateSettingsProtocol(settings);

            using (var fs = File.OpenWrite(_filename))
            {
                _serializer.Serialize(fs, protocol);
            }
        }
    }
}
