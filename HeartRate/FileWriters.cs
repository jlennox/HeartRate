using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HeartRate
{
    internal sealed class LogFileWriter : IDisposable
    {
        private readonly FileStream _fs;

        public LogFileWriter(string filename)
        {
            _fs = File.Open(filename, FileMode.Append,
                FileAccess.Write, FileShare.ReadWrite);
        }

        public void Write(string s)
        {
            var bytes = Encoding.UTF8.GetBytes(s);
            _fs.Write(bytes, 0, bytes.Length);
            _fs.Flush();
        }

        public void Dispose()
        {
            _fs.TryDispose();
        }
    }

    internal sealed class IBIFile : IDisposable
    {
        private readonly LogFileWriter _fs;

        public IBIFile(string filename)
        {
            if (filename == null) return;

            _fs = new LogFileWriter(filename);
        }

        public void Reading(HeartRateReading reading)
        {
            if (_fs == null) return;
            if (reading.RRIntervals == null) return;
            if (reading.RRIntervals.Length == 0) return;

            _fs.Write(AsMS(reading.RRIntervals) + "\r\n");
        }

        // rr intervals come from the device in units of 1/1024th of a second,
        // but IBI files require milliseconds.
        private static IEnumerable<string> AsMS(IEnumerable<int> rrintervals)
        {
            return rrintervals
                .Select(t => (int)Math.Round(t / 1024d * 1000d, 0))
                .Select(t => t.ToString());
        }

        public void Dispose()
        {
            _fs.TryDispose();
        }
    }

    internal sealed class LogFile : IDisposable
    {
        private readonly HeartRateSettings _settings;
        private readonly LogFileWriter _fs;

        public LogFile(HeartRateSettings settings, string filename)
        {
            _settings = settings;
            if (filename == null) return;

            _fs = new LogFileWriter(filename);
        }

        public void Reading(HeartRateReading reading)
        {
            if (_fs == null) return;

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
                _fs.Write(data);
            }
        }

        public void Dispose()
        {
            _fs.TryDispose();
        }
    }
}
