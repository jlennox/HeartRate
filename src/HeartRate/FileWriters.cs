using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace HeartRate;

internal interface IReadingListener
{
    void Reading(HeartRateReading reading);
}

internal abstract class FileWriter : IReadingListener
{
    protected bool HasFileWriter => _filename != null;

    private readonly string _filename;

    public abstract void Reading(HeartRateReading reading);

    protected FileWriter(string filename)
    {
        _filename = filename;
    }

    protected void AppendLine(string s)
    {
        if (_filename == null) return;

        using var fs = SharedOpen(FileMode.Append);
        var bytes = Encoding.UTF8.GetBytes(s + "\r\n");
        fs.Write(bytes, 0, bytes.Length);
        fs.Flush();
    }

    protected void Write(string s)
    {
        if (_filename == null) return;

        using var fs = SharedOpen(FileMode.Create);
        var bytes = Encoding.UTF8.GetBytes(s);
        fs.Write(bytes, 0, bytes.Length);
        fs.Flush();
    }

    protected FileStream SharedOpen(FileMode mode)
    {
        return File.Open(_filename, mode, FileAccess.Write, FileShare.ReadWrite);
    }
}

internal sealed class IBIFile : FileWriter
{
    public IBIFile(string filename) : base(filename)
    {
    }

    public override void Reading(HeartRateReading reading)
    {
        if (!HasFileWriter) return;
        if (reading.RRIntervals == null) return;
        if (reading.RRIntervals.Length == 0) return;
        if (reading.IsError) return;

        AppendLine(string.Join("\r\n", AsMS(reading.RRIntervals)));
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
    [ThreadStatic] private static StringBuilder _stringBuilder;

    public LogFile(HeartRateSettings settings, string filename)
        : base(filename)
    {
        _settings = settings;
    }

    public override void Reading(HeartRateReading reading)
    {
        if (!HasFileWriter) return;
        var csv = GetCsv(_settings, reading);

        if (csv == null) return;
        AppendLine(csv);
    }

    public static string GetCsv(HeartRateSettings settings, HeartRateReading reading)
    {
        if (reading.IsError) return null;

        _stringBuilder ??= new StringBuilder();

        var bpm = reading.BeatsPerMinute;
        var status = reading.Status;
        var rrvalue = reading.RRIntervals == null
            ? ""
            : string.Join(",", reading.RRIntervals);

        var dateString = DateTimeFormatter.Format(
            settings.LogDateFormat,
            DateTime.Now,
            DateTimeFormatter.DefaultColumn);

        switch ((settings.LogFormat ?? "").ToLower())
        {
            case "csv":
                AppendCsvValue(_stringBuilder, dateString, false, true);
                AppendCsvValue(_stringBuilder, bpm, false, true);
                AppendCsvValue(_stringBuilder, status, false, true);
                AppendCsvValue(_stringBuilder, reading.EnergyExpended, false, true);
                AppendCsvValue(_stringBuilder, rrvalue, true, false);
                break;
        }

        if (_stringBuilder.Length > 0)
        {
            var csv = _stringBuilder.ToString();
            _stringBuilder.Clear();
            return csv;
        }

        return null;
    }

    private static void AppendCsvValue<T>(StringBuilder sb, T value, bool alwaysQuote, bool appendComma)
    {
        var stringed = value.ToString();
        var needsQuotes = alwaysQuote || stringed.Any(t => t is ',' or '\n');
        if (!needsQuotes)
        {
            sb.Append(stringed);
            if (appendComma) sb.Append(',');
            return;
        }

        sb.Append('"');
        foreach (var c in stringed)
        {
            if (c == '"') sb.Append('\\');
            sb.Append(c);
        }
        sb.Append('"');
        if (appendComma) sb.Append(',');
    }
}

internal sealed class HeartRateFile : FileWriter
{
    public HeartRateFile(string filename) : base(filename) { }

    public override void Reading(HeartRateReading reading)
    {
        if (!HasFileWriter) return;
        if (reading.IsError) return;
        Write(reading.BeatsPerMinute.ToString());
    }
}

internal sealed class UdpWriter : IReadingListener, IDisposable
{
    private readonly HeartRateSettings _settings;
    private readonly UdpClient? _client;
    [ThreadStatic] private static byte[] _buffer;

    public UdpWriter(HeartRateSettings settings)
    {
        _settings = settings;

        if (_settings.UDP.IsValid)
        {
            _client = new UdpClient(_settings.UDP.Hostname, _settings.UDP.Port);
        }
    }

    public void Reading(HeartRateReading reading)
    {
        if (_client == null) return;

        var csv = LogFile.GetCsv(_settings, reading);
        if (csv == null) return;

        // This should always be large enough...
        _buffer ??= new byte[1024 * 10];

        csv += "\n";

        var byteCount = Encoding.UTF8.GetBytes(csv, 0, csv.Length, _buffer, 0);
        _client.Send(_buffer, byteCount);
    }

    public void Dispose()
    {
        _client?.TryDispose();
    }
}