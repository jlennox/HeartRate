using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace HeartRate
{
    internal static class DateTimeFormatter
    {
        public const string DefaultFilename = "yyyy-MM-dd hh-mm tt";
        public const string DefaultColumn = "";

        private static readonly Regex _tokenExp = new Regex(
            @"%date(?::([^%]+))?%",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly HashSet<char> _invalidFileNameChars =
            Path.GetInvalidFileNameChars().ToHashSet();

        public static string FormatStringTokens(
            string input,
            DateTime datetime,
            string defaultFormat = DefaultFilename,
            bool forFilepath = false)
        {
            return _tokenExp.Replace(input, match =>
            {
                var formatter = match.Groups.Count > 0
                    ? match.Groups[1].Value
                    : null;

                var formated = Format(formatter, datetime, defaultFormat);
                return forFilepath ? SanatizePath(formated) : formated;
            });
        }

        internal static string SanatizePath(string path)
        {
            return new string(path
                .Select(t => _invalidFileNameChars.Contains(t) ? '-' : t)
                .ToArray());
        }

        public static string Format(
            string formatter,
            DateTime datetime,
            string defaultFormat)
        {
            formatter = string.IsNullOrWhiteSpace(formatter)
                ? defaultFormat
                : formatter;

            return (formatter ?? "").ToUpperInvariant() switch {
                "OA" => datetime.ToOADate().ToString(),
                "" => datetime.ToString(),
                _ => datetime.ToString(formatter)
            };
        }
    }
}
