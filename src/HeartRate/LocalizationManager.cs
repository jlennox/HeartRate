using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HeartRate;

public static class LocalizationManager
{
    private static readonly Dictionary<string, string> _strings = new(StringComparer.OrdinalIgnoreCase);
    private static string _currentLanguage = "en";
    private static string _languageDirectory;

    /// <summary>
    /// Gets the currently active language code (e.g. "en", "zh-Hans").
    /// </summary>
    public static string CurrentLanguage => _currentLanguage;

    /// <summary>
    /// Raised after the language is changed via SetLanguage.
    /// </summary>
    public static event Action LanguageChanged;

    /// <summary>
    /// Initializes the localization manager: discovers available language files
    /// and loads the specified or auto-detected language once at startup.
    /// </summary>
    /// <param name="language">
    /// Language code to load. Pass null to auto-detect from the system culture.
    /// </param>
    public static void Initialize(string language = null)
    {
        _languageDirectory = GetLanguageDirectory();

        var lang = language ?? DetectSystemLanguage(_languageDirectory);
        LoadLanguage(lang);
    }

    /// <summary>
    /// Switches the active language at runtime.
    /// Falls back gracefully if the requested language file is missing.
    /// </summary>
    public static void SetLanguage(string language)
    {
        if (string.IsNullOrEmpty(language))
            return;

        if (string.Equals(_currentLanguage, language, StringComparison.OrdinalIgnoreCase))
            return;

        LoadLanguage(language);
        LanguageChanged?.Invoke();
    }

    /// <summary>
    /// Retrieves the translation for <paramref name="key"/>.
    /// Returns the key itself if no translation is found, so the UI
    /// always shows something meaningful.
    /// </summary>
    public static string GetString(string key)
    {
        if (_strings.TryGetValue(key, out var value))
            return value;

        return key;
    }

    /// <summary>
    /// Retrieves a formatted translation for <paramref name="key"/>.
    /// On format failure the raw translation is returned.
    /// </summary>
    public static string GetString(string key, params object[] args)
    {
        var format = GetString(key);
        try
        {
            return string.Format(format, args);
        }
        catch
        {
            return format;
        }
    }

    /// <summary>
    /// Returns all discovered language codes by scanning the Languages folder.
    /// The English fallback is always included even if the file is missing.
    /// </summary>
    public static string[] GetAvailableLanguages()
    {
        var dir = GetLanguageDirectory();
        var languages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Always include English as the ultimate fallback.
        languages.Add("en");

        if (Directory.Exists(dir))
        {
            try
            {
                foreach (var file in Directory.GetFiles(dir, "*.json"))
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    if (!string.IsNullOrEmpty(name))
                        languages.Add(name);
                }
            }
            catch
            {
                // Directory enumeration failed — still return at least "en".
            }
        }

        return languages.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    // ── internal helpers ──────────────────────────────────────────

    private static void LoadLanguage(string language)
    {
        var filePath = GetLanguageFilePath(language);

        // Fallback chain: requested → English → empty
        if (!File.Exists(filePath))
        {
            if (!string.Equals(language, "en", StringComparison.OrdinalIgnoreCase))
            {
                filePath = GetLanguageFilePath("en");
            }
        }

        Dictionary<string, string> parsed = null;

        if (File.Exists(filePath))
        {
            parsed = TryLoadJsonFile(filePath);
        }

        if (parsed == null)
        {
            parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        _strings.Clear();
        foreach (var kvp in parsed)
        {
            _strings[kvp.Key] = kvp.Value;
        }

        _currentLanguage = Path.GetFileNameWithoutExtension(filePath);
    }

    /// <summary>
    /// Attempts to read and parse a JSON language file.
    /// Returns null on any failure (missing file, invalid JSON, I/O error, etc.).
    /// </summary>
    private static Dictionary<string, string> TryLoadJsonFile(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath, Encoding.UTF8);
            return ParseSimpleJson(json);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Simple JSON parser for flat { "key": "value" } objects.
    /// Survives malformed input gracefully: skips tokens that can't be parsed
    /// and returns whatever key-value pairs were successfully extracted.
    /// </summary>
    private static Dictionary<string, string> ParseSimpleJson(string json)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(json))
            return result;

        var i = 0;
        SkipWhitespace(json, ref i);

        if (i >= json.Length || json[i] != '{')
            return result;

        i++; // skip '{'

        while (i < json.Length)
        {
            SkipWhitespace(json, ref i);

            if (i >= json.Length)
                break;

            var ch = json[i];

            if (ch == '}')
            {
                i++;
                break;
            }

            if (ch == ',')
            {
                i++;
                SkipWhitespace(json, ref i);
                continue;
            }

            // Expect a string key
            var key = ReadJsonString(json, ref i);
            if (string.IsNullOrEmpty(key))
            {
                // Malformed entry — advance to next potential key.
                SkipToNextEntry(json, ref i);
                continue;
            }

            SkipWhitespace(json, ref i);

            // Expect colon
            if (i < json.Length && json[i] == ':')
                i++;
            else
            {
                SkipToNextEntry(json, ref i);
                continue;
            }

            SkipWhitespace(json, ref i);

            // Expect a string value
            var value = ReadJsonString(json, ref i);

            result[key] = value;

            SkipWhitespace(json, ref i);
        }

        return result;
    }

    /// <summary>
    /// Advances the position past the current malformed token to the next
    /// comma or closing brace so parsing can continue.
    /// </summary>
    private static void SkipToNextEntry(string json, ref int i)
    {
        while (i < json.Length)
        {
            var ch = json[i];
            if (ch == ',' || ch == '}')
                return;
            i++;
        }
    }

    private static string ReadJsonString(string json, ref int i)
    {
        SkipWhitespace(json, ref i);

        if (i >= json.Length || json[i] != '"')
            return string.Empty;

        i++; // skip opening quote

        var sb = new StringBuilder();

        while (i < json.Length)
        {
            var ch = json[i];

            if (ch == '\\')
            {
                i++;
                if (i < json.Length)
                {
                    var escaped = json[i];
                    switch (escaped)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 < json.Length)
                            {
                                var hex = json.Substring(i + 1, 4);
                                if (int.TryParse(hex,
                                    System.Globalization.NumberStyles.HexNumber,
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    out var code))
                                {
                                    sb.Append((char)code);
                                    i += 4;
                                }
                                else
                                {
                                    sb.Append(escaped);
                                }
                            }
                            else
                            {
                                sb.Append(escaped);
                            }
                            break;
                        default:
                            sb.Append(escaped);
                            break;
                    }
                }

                i++;
                continue;
            }

            if (ch == '"')
            {
                i++; // skip closing quote
                break;
            }

            sb.Append(ch);
            i++;
        }

        return sb.ToString();
    }

    private static void SkipWhitespace(string s, ref int i)
    {
        while (i < s.Length && char.IsWhiteSpace(s[i]))
            i++;
    }

    private static string DetectSystemLanguage(string languageDir)
    {
        try
        {
            var culture = System.Globalization.CultureInfo.CurrentUICulture;
            var name = culture.Name.ToLowerInvariant();

            // Try full name first (e.g., "zh-hans")
            if (File.Exists(Path.Combine(languageDir, $"{name}.json")))
                return name;

            // Try two-letter code (e.g., "zh")
            var twoLetter = culture.TwoLetterISOLanguageName.ToLowerInvariant();
            if (twoLetter != name && File.Exists(Path.Combine(languageDir, $"{twoLetter}.json")))
                return twoLetter;

            // Try parent culture
            if (!Equals(culture, culture.Parent) && culture.Parent != null)
            {
                var parentName = culture.Parent.Name.ToLowerInvariant();
                if (parentName != name && File.Exists(Path.Combine(languageDir, $"{parentName}.json")))
                    return parentName;
            }
        }
        catch
        {
            // Fall through to default
        }

        return "en";
    }

    // ── path helpers ──────────────────────────────────────────────

    private static string GetLanguageFilePath(string language)
    {
        return Path.Combine(GetLanguageDirectory(), $"{language}.json");
    }

    private static string GetLanguageDirectory()
    {
        return _languageDirectory
            ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Languages");
    }
}
