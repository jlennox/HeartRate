using System;
using System.Text.RegularExpressions;

namespace HeartRate
{
    internal static class DateTimeFormatter
    {
        private static readonly Regex _tokenExp = new Regex(
            @"%date(?::([^%]+))?%",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public const string DefaultFilename = "yyyy-MM-dd hh-mm tt";
        public const string DefaultColumn = "OA";

        public static string FormatStringTokens(
            string input, DateTime datetime,
            string defaultFormat = DefaultFilename)
        {
            return _tokenExp.Replace(input, match =>
            {
                var formatter = match.Groups.Count > 0
                    ? match.Groups[1].Value
                    : null;

                return Format(formatter, datetime, defaultFormat);
            });
        }


        public static string Format(
            string formatter, DateTime datetime, string defaultFormat)
        {
            formatter = string.IsNullOrWhiteSpace(formatter)
                ? defaultFormat
                : formatter;

            switch ((formatter ?? "").ToLowerInvariant())
            {
                case "oa": return datetime.ToOADate().ToString();
            }

            return datetime.ToString(formatter);
        }
    }
}
