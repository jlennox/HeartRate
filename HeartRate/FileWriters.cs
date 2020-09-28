using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HeartRate
{
    internal abstract class FileWriter : IDisposable
    {
        protected bool HasFileWriter => _fs != null;

        private readonly FileStream _fs;

        public FileWriter(string filename)
        {
            if (filename == null) return;

            _fs = File.Open(filename, FileMode.Append,
                FileAccess.Write, FileShare.ReadWrite);
        }

        public void WriteLine(string s)
        {
            if (_fs == null) return;

            var bytes = Encoding.UTF8.GetBytes(s + "\r\n");
            _fs.Write(bytes, 0, bytes.Length);
            _fs.Flush();
        }

        public void Dispose()
        {
            _fs.TryDispose();
        }
    }

    internal sealed class IBIFile : FileWriter
    {
        public IBIFile(string filename) : base(filename)
        {
        }

        public void Reading(HeartRateReading reading)
        {
            if (!HasFileWriter) return;
            if (reading.RRIntervals == null) return;
            if (reading.RRIntervals.Length == 0) return;

            WriteLine(string.Join("\r\n", AsMS(reading.RRIntervals)));
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

    internal sealed class LogFile : FileWriter
    {
        private readonly HeartRateSettings _settings;

        public LogFile(HeartRateSettings settings, string filename)
            : base(filename)
        {
            _settings = settings;
        }

        public void Reading(HeartRateReading reading)
        {
            if (!HasFileWriter) return;

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
                    data = $"{dateString},{bpm},{status},{reading.EnergyExpended},{rrvalue}";
                    break;
            }

            if (data != null)
            {
                WriteLine(data);
            }
        }
    }
}
