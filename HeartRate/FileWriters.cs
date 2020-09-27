using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HeartRate
{
    internal sealed class IBIFile
    {
        private readonly string _filename;

        public IBIFile(string filename)
        {
            _filename = filename;
        }

        public void Reading(HeartRateReading reading)
        {
            if (_filename == null) return;
            if (reading.RRIntervals == null) return;
            if (reading.RRIntervals.Length == 0) return;

            File.AppendAllLines(_filename, AsMS(reading.RRIntervals));
        }

        // rr intervals come from the device in units of 1/1024th of a second,
        // but IBI files require milliseconds.
        private static IEnumerable<string> AsMS(IEnumerable<int> rrintervals)
        {
            return rrintervals
                .Select(t => (int)Math.Round(t / 1024d * 1000d, 0))
                .Select(t => t.ToString());
        }
    }

    internal sealed class LogFile
    {
        private readonly HeartRateSettings _settings;
        private readonly string _filename;

        public LogFile(HeartRateSettings settings, string filename)
        {
            _settings = settings;
            _filename = filename;
        }

        public void Reading(HeartRateReading reading)
        {
            if (_filename == null) return;

            string data = null;

            var bpm = reading.BeatsPerMinute;
            var status = reading.BeatsPerMinute;
            var rrvalue = reading.RRIntervals == null
                ? ""
                : string.Join(",", reading.RRIntervals);

            var dateString = DateTimeFormatter.Format(
                _settings.LogDateFormat,
                DateTime.Now,
                DateTimeFormatter.DefaultColumn);

            switch ((_settings.LogFormat ?? "").ToLower())
            {
                case "csv":
                    data = $"{dateString},{bpm},{status},{reading.EnergyExpended},{rrvalue}\r\n";
                    break;
            }

            if (data != null)
            {
                File.AppendAllText(_filename, data);
            }
        }
    }
}
